using System.Collections.Generic;

namespace HiddenHarbours.Core
{
    /// <summary>Counts are shelf objects. Reads settle missed delivery days before returning.</summary>
    public interface IShopStockView
    {
        int CountOf(string shopId, string lineId);
        bool IsSoldOut(string shopId, string lineId);
    }

    public interface IShopStock : IShopStockView
    {
        /// <summary>Committed stock operation, not an unpaid hand reservation or a payment.</summary>
        bool TryTakeOne(string shopId, string lineId);
        bool TryPutBackOne(string shopId, string lineId);
        /// <summary>Call from the slow/day-start tick. Repeated calls on the same day are inert.</summary>
        void SlowTick();
    }

    /// <summary>Slice 7's sparse persistence seam. No save format or serializer is defined here.</summary>
    public interface IShopStockStateStore
    {
        bool TryRead(string shopId, string lineId, out ShopStockRecord record);
        void Write(ShopStockRecord record);
        void Remove(string shopId, string lineId);
        void CopyTo(ICollection<ShopStockRecord> destination);
    }

    /// <summary>Only below-full lines need a record. An absent record means a full shelf now.</summary>
    public readonly struct ShopStockRecord
    {
        public readonly string ShopId;
        public readonly string LineId;
        public readonly int Count;
        public readonly int SettledThroughDay;

        public ShopStockRecord(string shopId, string lineId, int count, int settledThroughDay)
        {
            ShopId = shopId; LineId = lineId; Count = count; SettledThroughDay = settledThroughDay;
        }
    }

    public enum ShopStockChange { Taken, Returned, Restocked }

    /// <summary>Notification after a count changes; it neither charges money nor changes ownership.</summary>
    public readonly struct ShopStockChanged
    {
        public readonly string ShopId;
        public readonly string LineId;
        public readonly int Count;
        public readonly ShopStockChange Reason;

        public ShopStockChanged(string shopId, string lineId, int count, ShopStockChange reason)
        {
            ShopId = shopId; LineId = lineId; Count = count; Reason = reason;
        }
    }
}
