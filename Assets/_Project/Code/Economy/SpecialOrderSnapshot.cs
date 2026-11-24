using System;

namespace HiddenHarbours.Economy
{
    /// <summary>Paid open orders only. This is not connected to the save format.</summary>
    [Serializable]
    public sealed class SpecialOrderSnapshot
    {
        public string SellerId;
        public int NextOrderId = 1;
        public SpecialOrderState[] Orders = Array.Empty<SpecialOrderState>();
    }

    [Serializable]
    public sealed class SpecialOrderState
    {
        public int OrderId;
        public string LineId;
        public int Quantity;
        public int DayPlaced;
        public int DayDue;
        public bool Arrived;

        public SpecialOrderState Copy() => (SpecialOrderState)MemberwiseClone();
    }
}
