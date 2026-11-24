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
        RefundRequired, Busy
    }

    /// <summary>
    /// One book per player, shared by every counter. Dispose with the session. No scene or save access.
    /// Delivery uses absolute game days, independent of seed: equal inputs always give equal dates.
    /// </summary>
    public sealed class SpecialOrderBook : IDisposable
    {
        private readonly IWallet _wallet;
        private readonly IGameClock _clock;
        private readonly Dictionary<string, SpecialOrderLine> _lines = new(StringComparer.Ordinal);
        private readonly List<SpecialOrderState> _orders = new();
        private int _nextSpecialOrderNumber = 1;
        private bool _busy, _disposed;

        public SpecialOrderBook(IEnumerable<SpecialOrderLine> lines, IWallet wallet, IGameClock clock)
        {
            _wallet = wallet ?? throw new ArgumentNullException(nameof(wallet));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            if (lines == null) throw new ArgumentNullException(nameof(lines));
            foreach (var line in lines)
            {
                if (line == null) throw new ArgumentException("Null order line.");
                _lines.Add(line.Id, line);
            }
            EventBus.Subscribe<DayStarted>(OnDayStarted);
        }

        public SpecialOrderRefusal Place(string sellerId, string lineId, int quantity, out string orderId)
            => Place(sellerId, new[] { new SpecialOrderRequest(lineId, quantity) }, out orderId);

        /// <summary>All lines are paid together and arrive on the latest promised day among them.</summary>
        public SpecialOrderRefusal Place(string sellerId, IReadOnlyList<SpecialOrderRequest> requests,
            out string orderId)
        {
            ThrowIfDisposed();
            orderId = null;
            if (_busy) return SpecialOrderRefusal.Busy;
            if (requests == null || requests.Count == 0) return SpecialOrderRefusal.InvalidQuantity;
            int day = _clock.DayIndex;
            double seconds = _clock.TotalSeconds;
            if (day < 0 || !FiniteNonNegative(seconds)) return SpecialOrderRefusal.DayOutOfRange;
            long cost = 0, due = day;
            var lines = new SpecialOrderLineState[requests.Count];
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < requests.Count; i++)
            {
                var request = requests[i];
                if (request.ListingId == null || !_lines.TryGetValue(request.ListingId, out var line)
                    || line.SellerId != sellerId) return SpecialOrderRefusal.UnknownLine;
                if (request.Count <= 0 || !seen.Add(line.Id)) return SpecialOrderRefusal.InvalidQuantity;
                long open = 0;
                foreach (var order in _orders)
                    if (!order.RequiresRefund)
                        foreach (var paidLine in order.Lines)
                            if (paidLine.ListingId == line.Id) open += paidLine.Count;
                if (open + request.Count > line.MaxOnOrder) return SpecialOrderRefusal.LimitReached;
                long share = (long)line.Price * request.Count;
                cost += share;
                if (cost > int.MaxValue) return SpecialOrderRefusal.PriceOutOfRange;
                due = Math.Max(due, (long)day + line.DeliveryDays);
                if (due > int.MaxValue) return SpecialOrderRefusal.DayOutOfRange;
                lines[i] = new SpecialOrderLineState
                    { ListingId = line.Id, Count = request.Count, PaidPrice = (int)share };
            }
            if (_nextSpecialOrderNumber == int.MaxValue) return SpecialOrderRefusal.OrderNumbersExhausted;
            _busy = true;
            try
            {
                if (!_wallet.TrySpend((int)cost)) return SpecialOrderRefusal.CannotPay;
                var order = new SpecialOrderState
                {
                    OrderId = "order." + sellerId + "." + _nextSpecialOrderNumber.ToString(CultureInfo.InvariantCulture),
                    SellerId = sellerId, Lines = lines, PaidPrice = (int)cost, OrderedAtSeconds = seconds,
                    OrderedDay = day, DueDay = (int)due
                };
                _nextSpecialOrderNumber++;
                _orders.Add(order);
                orderId = order.OrderId;
                EventBus.Publish(new SpecialOrderPlaced(Receipt(order)));
                return SpecialOrderRefusal.None;
            }
            finally { _busy = false; }
        }

        /// <summary>
        /// Called when he collects at the counter. Receives every line into detached session bait stock
        /// through the tackle box, or changes nothing. Arrival alone never grants bait.
        /// </summary>
        public SpecialOrderRefusal Collect(string orderId, List<BaitStock> sessionBaitStock)
        {
            ThrowIfDisposed();
            if (_busy) return SpecialOrderRefusal.Busy;
            RefreshArrivals();
            var order = _orders.Find(row => row.OrderId == orderId);
            if (order == null) return SpecialOrderRefusal.UnknownOrder;
            if (order.RequiresRefund) return SpecialOrderRefusal.RefundRequired;
            if (!order.Arrived) return SpecialOrderRefusal.NotArrived;
            _busy = true;
            try
            {
                var receipt = Receipt(order);
                var delivery = new BaitStock[order.Lines.Length];
                for (int i = 0; i < delivery.Length; i++)
                    delivery[i] = new BaitStock(receipt.Lines[i].BaitId, receipt.Lines[i].Count);
                if (!TackleBox.TryReceiveBait(sessionBaitStock, delivery)) return SpecialOrderRefusal.BaitStoreUnavailable;
                _orders.Remove(order);
                EventBus.Publish(new SpecialOrderCollected(receipt));
                return SpecialOrderRefusal.None;
            }
            finally { _busy = false; }
        }

        /// <summary>Also call after a clock seek: seeks deliberately do not publish day events.</summary>
        public void RefreshArrivals()
        {
            ThrowIfDisposed();
            if (_busy) return;
            _busy = true;
            try
            {
                foreach (var order in _orders)
                    if (!order.RequiresRefund && !order.Arrived && _clock.DayIndex >= order.DueDay)
                    {
                        order.State = SpecialOrderState.ArrivedState;
                        EventBus.Publish(new SpecialOrderArrived(Receipt(order)));
                    }
            }
            finally { _busy = false; }
        }

        // Capture is observational, including immediately after Restore.
        public SpecialOrderSnapshot Capture() => new SpecialOrderSnapshot
        {
            NextSpecialOrderNumber = _nextSpecialOrderNumber,
            Orders = _orders.ConvertAll(row => row.Copy()).ToArray()
        };

        /// <summary>
        /// Restore each row without spending, granting bait or publishing. Damaged paid rows remain
        /// with their original payment and a refund reason. Collected rows only reserve their number.
        /// </summary>
        public void Restore(SpecialOrderSnapshot snapshot)
        {
            ThrowIfDisposed();
            if (_busy) throw new InvalidOperationException("An order transaction is in progress.");
            if (snapshot == null) return;
            int next = Math.Max(_nextSpecialOrderNumber, Math.Max(1, snapshot.NextSpecialOrderNumber));
            var rows = snapshot.Orders ?? Array.Empty<SpecialOrderState>();
            var counts = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var row in rows)
            {
                if (row == null) continue;
                int used = NumberOf(row.OrderId);
                if (used >= next) next = used == int.MaxValue ? int.MaxValue : used + 1;
                if (row.State != SpecialOrderState.Collected && !string.IsNullOrEmpty(row.OrderId))
                    counts[row.OrderId] = counts.TryGetValue(row.OrderId, out int count) ? count + 1 : 1;
            }
            var copy = new List<SpecialOrderState>();
            foreach (var row in rows)
            {
                if (row == null || row.State == SpecialOrderState.Collected) continue;
                var order = row.Copy();
                order.RefundReasonKey = RefundReason(order, counts);
                copy.Add(order);
            }
            _orders.Clear();
            _orders.AddRange(copy);
            _nextSpecialOrderNumber = next;
        }

        private string RefundReason(SpecialOrderState order, Dictionary<string, int> counts)
        {
            if (string.IsNullOrWhiteSpace(order.OrderId)) return "special_order.empty_id";
            if (counts[order.OrderId] > 1) return "special_order.duplicate_id";
            int number = NumberOf(order.OrderId);
            if (number == 0 || order.OrderId != "order." + order.SellerId + "." + number.ToString(CultureInfo.InvariantCulture))
                return "special_order.invalid_id";
            if (order.OrderedDay < 0 || order.DueDay <= order.OrderedDay) return "special_order.invalid_due_day";
            if (!FiniteNonNegative(order.OrderedAtSeconds)) return "special_order.invalid_time";
            if (order.State != SpecialOrderState.Waiting && order.State != SpecialOrderState.ArrivedState)
                return "special_order.invalid_state";
            if (!string.IsNullOrEmpty(order.DeliverToId)) return "special_order.unsupported_destination";
            if (order.Lines == null || order.Lines.Length == 0) return "special_order.missing_lines";
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

        private SpecialOrderReceipt Receipt(SpecialOrderState row)
        {
            var lines = Array.ConvertAll(row.Lines, line => new SpecialOrderLineReceipt(
                line.ListingId, _lines[line.ListingId].ItemId, line.Count, line.PaidPrice));
            return new SpecialOrderReceipt(row.OrderId, row.SellerId, row.DeliverToId, row.PaidPrice,
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
