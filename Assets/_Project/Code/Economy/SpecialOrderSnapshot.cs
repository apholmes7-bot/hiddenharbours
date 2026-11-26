using System;
using HiddenHarbours.Core;

namespace HiddenHarbours.Economy
{
    /// <summary>Detached view of outstanding saved orders, including derived refund reasons.</summary>
    [Serializable]
    public sealed class SpecialOrderSnapshot
    {
        public int NextSpecialOrderNumber = 1;
        public SpecialOrderState[] Orders = Array.Empty<SpecialOrderState>();
    }

    [Serializable]
    public sealed class SpecialOrderState
    {
        public const int Waiting = SpecialOrderDto.StateWaiting, ArrivedState = SpecialOrderDto.StateArrived,
            Collected = SpecialOrderDto.StateCollected;
        public string OrderId = "";
        public string SellerId = "";
        public string DeliverToId = "";
        public SpecialOrderLineState[] Lines = Array.Empty<SpecialOrderLineState>();
        public int PaidPrice;
        public double OrderedAtSeconds;
        public int OrderedDay;
        public int DueDay;
        public int State;
        public string RefundReasonKey = "";

        public bool Arrived => State == ArrivedState;
        public bool RequiresRefund => !string.IsNullOrEmpty(RefundReasonKey);
        public int RefundAmount => RequiresRefund ? Math.Max(0, PaidPrice) : 0;

        public SpecialOrderState Copy()
        {
            var copy = (SpecialOrderState)MemberwiseClone();
            copy.Lines = Lines == null ? null : Array.ConvertAll(Lines, line => line?.Copy());
            return copy;
        }
    }

    [Serializable]
    public sealed class SpecialOrderLineState
    {
        public string ListingId = "";
        public int Count;
        // The whole line's share of the payment, not a current unit price.
        public int PaidPrice;
        public SpecialOrderLineState Copy() => (SpecialOrderLineState)MemberwiseClone();
    }

    public readonly struct SpecialOrderRequest
    {
        public readonly string ListingId;
        public readonly int Count;
        public SpecialOrderRequest(string listingId, int count) { ListingId = listingId; Count = count; }
    }
}
