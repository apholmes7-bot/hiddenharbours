using System;

namespace HiddenHarbours.Economy
{
    /// <summary>One player's session book across all sellers. Not connected to a save.</summary>
    [Serializable]
    public sealed class SpecialOrderSnapshot
    {
        public int NextSpecialOrderNumber = 1;
        public SpecialOrderState[] Orders = Array.Empty<SpecialOrderState>();
    }

    [Serializable]
    public sealed class SpecialOrderState
    {
        public const int Waiting = 0, ArrivedState = 1, Collected = 2;
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
