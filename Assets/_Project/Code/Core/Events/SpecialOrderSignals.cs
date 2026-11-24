namespace HiddenHarbours.Core
{
    /// <summary>Immutable receipt for a paid order; quantities are individual delivered units.</summary>
    public readonly struct SpecialOrderReceipt
    {
        public readonly int OrderId, Quantity, DayPlaced, DayDue;
        public readonly string SellerId, LineId, ItemId;

        public SpecialOrderReceipt(int orderId, string sellerId, string lineId, string itemId,
            int quantity, int dayPlaced, int dayDue)
        {
            OrderId = orderId; SellerId = sellerId; LineId = lineId; ItemId = itemId;
            Quantity = quantity; DayPlaced = dayPlaced; DayDue = dayDue;
        }
    }

    public readonly struct SpecialOrderPlaced
    {
        public readonly SpecialOrderReceipt Order;
        public SpecialOrderPlaced(SpecialOrderReceipt order) { Order = order; }
    }

    public readonly struct SpecialOrderArrived
    {
        public readonly SpecialOrderReceipt Order;
        public SpecialOrderArrived(SpecialOrderReceipt order) { Order = order; }
    }

    public readonly struct SpecialOrderCollected
    {
        public readonly SpecialOrderReceipt Order;
        public SpecialOrderCollected(SpecialOrderReceipt order) { Order = order; }
    }
}
