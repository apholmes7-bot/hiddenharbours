using System;
using System.Collections.Generic;
using HiddenHarbours.Core;

namespace HiddenHarbours.Economy
{
    /// <summary>In-memory only. A later save adapter can implement the same Core contract.</summary>
    public sealed class MemoryShopStockStateStore : IShopStockStateStore
    {
        private readonly Dictionary<(string shop, string line), ShopStockRecord> _records =
            new Dictionary<(string, string), ShopStockRecord>();

        public bool TryRead(string shopId, string lineId, out ShopStockRecord record) =>
            _records.TryGetValue((shopId, lineId), out record);

        public void Write(ShopStockRecord record)
        {
            if (string.IsNullOrWhiteSpace(record.ShopId) || string.IsNullOrWhiteSpace(record.LineId) ||
                record.Count < 0 || record.SettledThroughDay < 0)
                throw new ArgumentException("Stock records require ids, a nonnegative count and an absolute day.");
            _records[(record.ShopId, record.LineId)] = record;
        }

        public void Remove(string shopId, string lineId) => _records.Remove((shopId, lineId));

        /// <summary>Snapshot/export work only; callers choose the destination and any serialization.</summary>
        public void CopyTo(ICollection<ShopStockRecord> destination)
        {
            if (destination == null) throw new ArgumentNullException(nameof(destination));
            foreach (var record in _records.Values) destination.Add(record);
        }
    }
}
