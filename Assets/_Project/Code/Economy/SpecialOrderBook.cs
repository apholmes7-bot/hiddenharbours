using System;
using System.Collections.Generic;
using HiddenHarbours.Core;

namespace HiddenHarbours.Economy
{
    public enum SpecialOrderRefusal
    {
        None, UnknownLine, InvalidQuantity, LimitReached, CannotPay, PriceOutOfRange,
        DayOutOfRange, OrderNumbersExhausted, UnknownOrder, NotArrived, HandsAndBagFull, Busy
    }

    /// <summary>
    /// One player's paid orders at one counter. Dispose with the session. No scene or save access.
    /// Delivery uses absolute game days, independent of seed: equal inputs always give equal dates.
    /// </summary>
    public sealed class SpecialOrderBook : IDisposable
    {
        private readonly string _sellerId;
        private readonly IWallet _wallet;
        private readonly IGameClock _clock;
        private readonly Dictionary<string, SpecialOrderLine> _lines = new(StringComparer.Ordinal);
        private readonly List<SpecialOrderState> _orders = new();
        private int _nextOrderId = 1;
        private bool _busy, _disposed;

        public SpecialOrderBook(string sellerId, IEnumerable<SpecialOrderLine> lines,
            IWallet wallet, IGameClock clock)
        {
            if (string.IsNullOrWhiteSpace(sellerId)) throw new ArgumentException(nameof(sellerId));
            _sellerId = sellerId;
            _wallet = wallet ?? throw new ArgumentNullException(nameof(wallet));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            if (lines == null) throw new ArgumentNullException(nameof(lines));
            foreach (var line in lines)
            {
                if (line == null) throw new ArgumentException("Null order line.");
                if (line.SellerId == sellerId) _lines.Add(line.Id, line);
            }
            EventBus.Subscribe<DayStarted>(OnDayStarted);
        }

        public SpecialOrderRefusal Place(string lineId, int quantity, out int orderId)
        {
            ThrowIfDisposed();
            orderId = 0;
            if (_busy) return SpecialOrderRefusal.Busy;
            if (lineId == null || !_lines.TryGetValue(lineId, out var line)) return SpecialOrderRefusal.UnknownLine;
            if (quantity <= 0) return SpecialOrderRefusal.InvalidQuantity;
            long open = 0;
            foreach (var order in _orders) if (order.LineId == lineId) open += order.Quantity;
            if (open + quantity > line.MaxOnOrder) return SpecialOrderRefusal.LimitReached;
            long cost = (long)line.Price * quantity;
            if (cost > int.MaxValue) return SpecialOrderRefusal.PriceOutOfRange;
            int day = _clock.DayIndex;
            long due = (long)day + line.DeliveryDays;
            if (day < 0 || due > int.MaxValue) return SpecialOrderRefusal.DayOutOfRange;
            if (_nextOrderId == int.MaxValue) return SpecialOrderRefusal.OrderNumbersExhausted;
            _busy = true;
            try
            {
                if (!_wallet.TrySpend((int)cost)) return SpecialOrderRefusal.CannotPay;
                var order = new SpecialOrderState { OrderId = _nextOrderId++, LineId = lineId,
                    Quantity = quantity, DayPlaced = day, DayDue = (int)due };
                _orders.Add(order);
                orderId = order.OrderId;
                EventBus.Publish(new SpecialOrderPlaced(Receipt(order)));
                return SpecialOrderRefusal.None;
            }
            finally { _busy = false; }
        }

        /// <summary>
        /// Calls tryReceive once with item id and quantity. It must either hand over ALL units to
        /// hands/bag and return true, or leave them at the counter and return false. It must not throw
        /// after transferring goods. A capacity-only probe is not a handover. No delivery by event.
        /// </summary>
        public SpecialOrderRefusal Collect(int orderId, Func<string, int, bool> tryReceive)
        {
            ThrowIfDisposed();
            if (_busy) return SpecialOrderRefusal.Busy;
            RefreshArrivals();
            var order = _orders.Find(row => row.OrderId == orderId);
            if (order == null) return SpecialOrderRefusal.UnknownOrder;
            if (!order.Arrived) return SpecialOrderRefusal.NotArrived;
            if (tryReceive == null) return SpecialOrderRefusal.HandsAndBagFull;
            _busy = true;
            try
            {
                var receipt = Receipt(order);
                if (!tryReceive(receipt.ItemId, order.Quantity)) return SpecialOrderRefusal.HandsAndBagFull;
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
                    if (!order.Arrived && _clock.DayIndex >= order.DayDue)
                    {
                        order.Arrived = true;
                        EventBus.Publish(new SpecialOrderArrived(Receipt(order)));
                    }
            }
            finally { _busy = false; }
        }

        // Capture is deliberately observational, including immediately after Restore.
        public SpecialOrderSnapshot Capture() => new SpecialOrderSnapshot
        {
            SellerId = _sellerId, NextOrderId = _nextOrderId,
            Orders = _orders.ConvertAll(row => row.Copy()).ToArray()
        };

        /// <summary>
        /// Validate the entire snapshot before replacing state. Does not spend, deliver or publish.
        /// Due dates and paid quantities survive later changes to offer waits and limits.
        /// Unknown line ids require an explicit content migration, never silent loss of paid goods.
        /// </summary>
        public void Restore(SpecialOrderSnapshot snapshot)
        {
            ThrowIfDisposed();
            if (_busy) throw new InvalidOperationException("An order transaction is in progress.");
            if (snapshot == null || snapshot.SellerId != _sellerId || snapshot.NextOrderId <= 0 || snapshot.Orders == null)
                throw new ArgumentException("Invalid order snapshot.");
            var ids = new HashSet<int>();
            var copy = new List<SpecialOrderState>();
            foreach (var row in snapshot.Orders)
            {
                if (row == null || row.OrderId <= 0 || row.OrderId >= snapshot.NextOrderId || !ids.Add(row.OrderId)
                    || row.LineId == null || !_lines.ContainsKey(row.LineId) || row.Quantity <= 0
                    || row.DayPlaced < 0 || row.DayDue <= row.DayPlaced)
                    throw new ArgumentException("Invalid paid order.");
                copy.Add(row.Copy());
            }
            _orders.Clear();
            _orders.AddRange(copy);
            _nextOrderId = snapshot.NextOrderId;
        }

        private SpecialOrderReceipt Receipt(SpecialOrderState row) => new SpecialOrderReceipt(
            row.OrderId, _sellerId, row.LineId, _lines[row.LineId].ItemId, row.Quantity, row.DayPlaced, row.DayDue);

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
