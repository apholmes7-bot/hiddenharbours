using System;
using System.Collections.Generic;

namespace HiddenHarbours.Core
{
    public readonly struct SpecialOrderLineReceipt
    {
        public readonly string ListingId, BaitId;
        public readonly int Count, PaidPrice;
        public SpecialOrderLineReceipt(string listingId, string baitId, int count, int paidPrice)
        {
            ListingId = listingId; BaitId = baitId; Count = count; PaidPrice = paidPrice;
        }
    }

    /// <summary>Immutable receipt; line prices are paid shares, quantities are individual baits.</summary>
    public readonly struct SpecialOrderReceipt
    {
        public readonly string PlayerId, OrderId, SellerId, DeliverToId;
        public readonly int PaidPrice, OrderedDay, DueDay;
        public readonly double OrderedAtSeconds;
        public readonly IReadOnlyList<SpecialOrderLineReceipt> Lines;

        public SpecialOrderReceipt(string playerId, string orderId, string sellerId, string deliverToId, int paidPrice,
            double orderedAtSeconds, int orderedDay, int dueDay, SpecialOrderLineReceipt[] lines)
        {
            PlayerId = playerId; OrderId = orderId; SellerId = sellerId; DeliverToId = deliverToId; PaidPrice = paidPrice;
            OrderedAtSeconds = orderedAtSeconds; OrderedDay = orderedDay; DueDay = dueDay;
            Lines = Array.AsReadOnly((SpecialOrderLineReceipt[])lines.Clone());
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
