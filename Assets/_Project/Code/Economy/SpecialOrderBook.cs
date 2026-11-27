using System;
using System.Collections.Generic;
using System.Globalization;
using HiddenHarbours.Core;

namespace HiddenHarbours.Economy
{
    public enum SpecialOrderRefusal
    {
        None, UnknownLine, InvalidQuantity, LimitReached, CannotPay, PriceOutOfRange,
        DayOutOfRange, OrderNumbersExhausted, UnknownOrder, NotArrived, BaitStoreUnavailable,
        RefundRequired, Busy, NoSupplyRun
    }

    /// <summary>
    /// One book per saved world, shared by all players and counters, bound to the existing rows.
    /// Dispose before changing saves. Construction reads paid orders without replaying transactions.
    /// Delivery follows the supplied calendar and completed runs; equal inputs give equal dates.
    /// </summary>
    public sealed class SpecialOrderBook : IDisposable
    {
        private readonly ISpecialOrderAccounts _accounts;
        private readonly IGameClock _clock;
        private readonly SpecialOrderSupplySchedule _supply;
        private readonly Dictionary<string, SpecialOrderLine> _lines = new(StringComparer.Ordinal);
        private readonly SaveData _save;
        private bool _busy, _disposed;

        public SpecialOrderBook(SaveData save, IEnumerable<SpecialOrderLine> lines, ISpecialOrderAccounts accounts, IGameClock clock,
            SpecialOrderSupplySchedule supply)
        {
            _save = save ?? throw new ArgumentNullException(nameof(save));
            _accounts = accounts ?? throw new ArgumentNullException(nameof(accounts));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _supply = supply ?? throw new ArgumentNullException(nameof(supply));
            if (lines == null) throw new ArgumentNullException(nameof(lines));
            foreach (var line in lines)
            {
                if (line == null) throw new ArgumentException("Null order line.");
                _lines.Add(line.Id, line);
            }
            _save.SpecialOrders ??= new List<SpecialOrderDto>();
            ReserveUsedNumbers();
            EventBus.Subscribe<DayStarted>(OnDayStarted);
        }

        public SpecialOrderRefusal Place(string playerId, string sellerId, string lineId, int quantity, out string orderId)
            => Place(playerId, sellerId, new[] { new SpecialOrderRequest(lineId, quantity) }, out orderId);

        /// <summary>All lines are paid together and arrive on the latest promised day among them.</summary>
        public SpecialOrderRefusal Place(string playerId, string sellerId, IReadOnlyList<SpecialOrderRequest> requests,
            out string orderId)
        {
            ThrowIfDisposed();
            orderId = null;
            if (_busy) return SpecialOrderRefusal.Busy;
            if (string.IsNullOrWhiteSpace(playerId)) return SpecialOrderRefusal.CannotPay;
            if (requests == null || requests.Count == 0) return SpecialOrderRefusal.InvalidQuantity;
            int day = _clock.DayIndex;
            double seconds = _clock.TotalSeconds;
            if (day < 0 || !FiniteNonNegative(seconds)) return SpecialOrderRefusal.DayOutOfRange;
            long cost = 0, due = day;
            var lines = new List<SpecialOrderLineDto>();
            var requested = new Dictionary<string, SpecialOrderLineDto>(StringComparer.Ordinal);
            var openCounts = RetainedIdCounts();
            for (int i = 0; i < requests.Count; i++)
            {
                var request = requests[i];
                if (request.ListingId == null || !_lines.TryGetValue(request.ListingId, out var line)
                    || line.SellerId != sellerId) return SpecialOrderRefusal.UnknownLine;
                if (request.Count <= 0) return SpecialOrderRefusal.InvalidQuantity;
                requested.TryGetValue(line.Id, out var combined);
                long count = (long)(combined?.Count ?? 0) + request.Count;
                long open = 0;
                foreach (var order in _save.SpecialOrders)
                    if (IsOpen(order) && Owns(playerId, order.OrderId) && RefundReason(order, openCounts).Length == 0)
                        foreach (var paidLine in order.Lines)
                            if (paidLine.ListingId == line.Id) open += paidLine.Count;
                if (open + count > line.MaxOnOrder) return SpecialOrderRefusal.LimitReached;
                long share = (long)line.Price * request.Count;
                cost += share;
                if (cost > int.MaxValue) return SpecialOrderRefusal.PriceOutOfRange;
                due = Math.Max(due, (long)day + line.DeliveryDays);
                if (due > int.MaxValue) return SpecialOrderRefusal.DayOutOfRange;
                if (combined == null)
                {
                    combined = new SpecialOrderLineDto { ListingId = line.Id, Count = 0 };
                    requested.Add(line.Id, combined);
                    lines.Add(combined);
                }
                combined.Count = (int)count;
                combined.PaidPrice += (int)share;
            }
            if (!_supply.TryPromise(sellerId, (int)due, out int promisedDay)) return SpecialOrderRefusal.NoSupplyRun;
            ReserveUsedNumbers();
            if (_save.NextSpecialOrderNumber == int.MaxValue) return SpecialOrderRefusal.OrderNumbersExhausted;
            _busy = true;
            try
            {
                string nextId = "order." + sellerId + "." + _save.NextSpecialOrderNumber.ToString(CultureInfo.InvariantCulture);
                if (!_accounts.TryPay(playerId, nextId, (int)cost)) return SpecialOrderRefusal.CannotPay;
                var order = new SpecialOrderDto
                {
                    OrderId = nextId,
                    SellerId = sellerId, Lines = lines, PaidPrice = (int)cost, OrderedAtSeconds = seconds,
                    OrderedDay = day, DueDay = promisedDay
                };
                _save.NextSpecialOrderNumber++;
                _save.SpecialOrders.Add(order);
                orderId = order.OrderId;
                EventBus.Publish(new SpecialOrderPlaced(Receipt(order)));
                return SpecialOrderRefusal.None;
            }
            finally { _busy = false; }
        }

        /// <summary>
        /// Called after he completes the counter handover. Pass the acting player's bait stock; the
        /// host must persist goods and the collected receipt together. Arrival alone never grants bait.
        /// Refresh arrivals separately after a clock seek. A refusal changes no rows, goods or money
        /// and publishes no events; collection never advances other orders.
        /// </summary>
        public SpecialOrderRefusal Collect(string playerId, string orderId, List<BaitStock> baitStock)
        {
            ThrowIfDisposed();
            if (_busy) return SpecialOrderRefusal.Busy;
            if (!Owns(playerId, orderId)) return SpecialOrderRefusal.UnknownOrder;
            var order = _save.SpecialOrders.Find(row => IsOpen(row) && row.OrderId == orderId);
            if (order == null) return SpecialOrderRefusal.UnknownOrder;
            if (RefundReason(order, RetainedIdCounts()).Length != 0) return SpecialOrderRefusal.RefundRequired;
            if (order.State != SpecialOrderDto.StateArrived) return SpecialOrderRefusal.NotArrived;
            _busy = true;
            try
            {
                var receipt = Receipt(order);
                var delivery = new BaitStock[order.Lines.Count];
                for (int i = 0; i < delivery.Length; i++)
                    delivery[i] = new BaitStock(receipt.Lines[i].BaitId, receipt.Lines[i].Count);
                if (!TackleBox.TryReceiveBait(baitStock, delivery)) return SpecialOrderRefusal.BaitStoreUnavailable;
                order.State = SpecialOrderDto.StateCollected;
                EventBus.Publish(new SpecialOrderCollected(receipt));
                return SpecialOrderRefusal.None;
            }
            finally { _busy = false; }
        }

        /// <summary>Call after unloading or a clock seek. A day event alone never proves delivery.</summary>
        public void RefreshArrivals()
        {
            ThrowIfDisposed();
            if (_busy) return;
            _busy = true;
            try
            {
                var counts = RetainedIdCounts();
                foreach (var order in _save.SpecialOrders)
                    if (IsOpen(order) && order.State == SpecialOrderDto.StateWaiting
                        && !string.IsNullOrWhiteSpace(_accounts.OwnerOf(order.OrderId))
                        && RefundReason(order, counts).Length == 0
                        && _supply.HasDelivered(order.SellerId, order.DueDay, _clock.DayIndex, _clock.TotalSeconds))
                    {
                        order.State = SpecialOrderDto.StateArrived;
                        EventBus.Publish(new SpecialOrderArrived(Receipt(order)));
                    }
            }
            finally { _busy = false; }
        }

        /// <summary>A detached view of outstanding rows; reading it never changes the save.</summary>
        public SpecialOrderSnapshot Capture(string playerId)
        {
            ThrowIfDisposed();
            var counts = RetainedIdCounts();
            var orders = new List<SpecialOrderState>();
            foreach (var row in _save.SpecialOrders)
            {
                if (!IsOpen(row) || !Owns(playerId, row.OrderId)) continue;
                var order = SpecialOrderRowAdapter.Capture(row);
                order.RefundReasonKey = RefundReason(row, counts);
                orders.Add(order);
            }
            return new SpecialOrderSnapshot
                { NextSpecialOrderNumber = _save.NextSpecialOrderNumber, Orders = orders.ToArray() };
        }

        private void ReserveUsedNumbers()
        {
            int next = Math.Max(1, _save.NextSpecialOrderNumber);
            foreach (var row in _save.SpecialOrders)
            {
                if (row == null) continue;
                int used = NumberOf(row.OrderId);
                if (used >= next) next = used == int.MaxValue ? int.MaxValue : used + 1;
            }
            _save.NextSpecialOrderNumber = next;
        }

        private static bool IsOpen(SpecialOrderDto row) => row != null && row.State != SpecialOrderDto.StateCollected;

        private bool Owns(string playerId, string orderId) => !string.IsNullOrWhiteSpace(playerId)
            && string.Equals(_accounts.OwnerOf(orderId), playerId, StringComparison.Ordinal);

        private Dictionary<string, int> RetainedIdCounts()
        {
            var counts = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var row in _save.SpecialOrders)
                if (row != null && !string.IsNullOrEmpty(row.OrderId))
                    counts[row.OrderId] = counts.TryGetValue(row.OrderId, out int count) ? count + 1 : 1;
            return counts;
        }

        private string RefundReason(SpecialOrderDto order, Dictionary<string, int> counts)
        {
            if (string.IsNullOrWhiteSpace(order.OrderId)) return "special_order.empty_id";
            if (counts[order.OrderId] > 1) return "special_order.duplicate_id";
            int number = NumberOf(order.OrderId);
            if (number == 0 || order.OrderId != "order." + order.SellerId + "." + number.ToString(CultureInfo.InvariantCulture))
                return "special_order.invalid_id";
            if (order.OrderedDay < 0 || order.DueDay <= order.OrderedDay) return "special_order.invalid_due_day";
            if (!FiniteNonNegative(order.OrderedAtSeconds)) return "special_order.invalid_time";
            if (order.State != SpecialOrderDto.StateWaiting && order.State != SpecialOrderDto.StateArrived)
                return "special_order.invalid_state";
            if (!string.IsNullOrEmpty(order.DeliverToId)) return "special_order.unsupported_destination";
            if (order.Lines == null || order.Lines.Count == 0) return "special_order.missing_lines";
            long shares = 0;
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var line in order.Lines)
            {
                if (line == null || line.ListingId == null || !_lines.TryGetValue(line.ListingId, out var offer)
                    || offer.SellerId != order.SellerId) return "special_order.unknown_offer";
                if (line.Count <= 0 || !seen.Add(line.ListingId)) return "special_order.invalid_lines";
                if (line.PaidPrice < 0) return "special_order.invalid_payment";
                shares += line.PaidPrice;
            }
            return order.PaidPrice < 0 || shares != order.PaidPrice ? "special_order.invalid_payment" : "";
        }

        private static bool FiniteNonNegative(double value) => value >= 0 && !double.IsInfinity(value);

        private static int NumberOf(string id)
        {
            if (string.IsNullOrEmpty(id)) return 0;
            int dot = id.LastIndexOf('.');
            if (dot < 0 || dot == id.Length - 1) return 0;
            return int.TryParse(id.Substring(dot + 1), NumberStyles.None, CultureInfo.InvariantCulture, out int number)
                && number > 0 ? number : 0;
        }

        private SpecialOrderReceipt Receipt(SpecialOrderDto row)
        {
            var lines = row.Lines.ConvertAll(line => new SpecialOrderLineReceipt(
                line.ListingId, _lines[line.ListingId].ItemId, line.Count, line.PaidPrice)).ToArray();
            return new SpecialOrderReceipt(_accounts.OwnerOf(row.OrderId), row.OrderId, row.SellerId, row.DeliverToId, row.PaidPrice,
                row.OrderedAtSeconds, row.OrderedDay, row.DueDay, lines);
        }

        private void OnDayStarted(DayStarted _) => RefreshArrivals();
        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(SpecialOrderBook));
        }
        public void Dispose()
        {
            if (_disposed) return;
            EventBus.Unsubscribe<DayStarted>(OnDayStarted);
            _disposed = true;
        }
    }
}
