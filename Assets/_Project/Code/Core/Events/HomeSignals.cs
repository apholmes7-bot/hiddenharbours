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
    public readonly struct HomePurchased
    {
        public readonly string HomeId;
        public readonly int PricePaid;   // ₲ — 0 for a starter home
        public HomePurchased(string homeId, int pricePaid)
        {
            HomeId = homeId; PricePaid = pricePaid;
        }
    }
}
