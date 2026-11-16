namespace HiddenHarbours.Core
{
    /// <summary>
    /// Raised when a home becomes the player's — the deed is already recorded
    /// (<see cref="HomeDeeds"/>) and any price already deducted, exactly as
    /// <see cref="LicensePurchased"/> is raised after the licence is granted. world-content can
    /// subscribe for a story beat and ui-ux for a toast, neither referencing Economy (rule 4). Keyed by
    /// stable home id (e.g. "home.ginny_lot_camper").
    ///
    /// <para><see cref="PricePaid"/> is 0 for a home the owner declared a STARTER HOME — the deed still
    /// changes hands, and a listener that wants to know whether money moved should read the number
    /// rather than assume one.</para>
    ///
    /// <para>Its own Core/Events file rather than a line in <c>GameSignals.cs</c>, for the reason
    /// <see cref="LicensePurchased"/>'s file gives: that file is lead-architect's, so new signals land
    /// additively beside it. Same EventBus, separate file.</para>
    /// </summary>
    public readonly struct HomePurchased : ICustomSignal
    {
        public readonly string HomeId;
        public readonly int PricePaid;   // ₲ — 0 for a starter home
        /// <summary>Data-authored keeper, vendor and trade; legacy/default signals report unknown.</summary>
        public TradeCounterparty Counterparty { get; }
        public string MerchantId => Counterparty.MerchantId;
        public string VendorId => Counterparty.VendorId;
        public string TradeId => Counterparty.TradeId;
        public decimal CustomUnits => IsPurchase ? 1m : 0m;

        public bool IsPurchase { get; }

        public HomePurchased(string homeId, int pricePaid)
            : this(homeId, pricePaid, default) { }

        public HomePurchased(string homeId, int pricePaid, TradeCounterparty counterparty, bool isPurchase = true)
        {
            HomeId = homeId; PricePaid = pricePaid;
            Counterparty = counterparty;
            IsPurchase = isPurchase;
        }
    }
}
