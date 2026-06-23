namespace HiddenHarbours.Core
{
    /// <summary>
    /// Raised when the player buys a license at a vendor (St Peters opening). The economy side has
    /// already deducted the fee and granted the license; ui-ux can subscribe for a "licensed!" toast
    /// and world-content for a story beat — without referencing the Economy module (cross-module talk
    /// via Core/EventBus). Keyed by stable license id (e.g. "license.cod").
    ///
    /// <para>This lives in its OWN Core/Events file rather than in <c>GameSignals.cs</c>: that file is
    /// owned by lead-architect this wave, so the St Peters economy signals are added additively here
    /// (coordination.md §1 — "keep it additive: new interface/event"). Same EventBus, separate file.</para>
    /// </summary>
    public readonly struct LicensePurchased
    {
        public readonly string LicenseId;
        public readonly int PricePaid;   // ₲
        public LicensePurchased(string licenseId, int pricePaid)
        {
            LicenseId = licenseId; PricePaid = pricePaid;
        }
    }

    /// <summary>
    /// Raised when the player pays the shipwright to REPAIR a damaged boat they own (St Peters opening:
    /// buy the damaged dory, then pay to repair it into a usable boat). The fee is already deducted and
    /// the boat marked repaired; gameplay-systems listens to make the now-usable hull boardable, and
    /// ui-ux for a toast — neither references Economy. Keyed by stable hull id (e.g. "boat.dory").
    /// </summary>
    public readonly struct BoatRepaired
    {
        public readonly string BoatId;
        public readonly int PricePaid;   // ₲
        public BoatRepaired(string boatId, int pricePaid)
        {
            BoatId = boatId; PricePaid = pricePaid;
        }
    }

    /// <summary>
    /// Raised when the player buys a piece of gear/equipment at a shop (St Peters opening: the rod, the
    /// shovel). The fee is already deducted and the gear id recorded as owned; gameplay-systems listens
    /// to enable the gear's capability (map the id to a Gear flag), and ui-ux for a toast — neither
    /// references Economy. Keyed by stable gear id (e.g. "gear.rod").
    /// </summary>
    public readonly struct GearPurchased
    {
        public readonly string GearId;
        public readonly int PricePaid;   // ₲
        public GearPurchased(string gearId, int pricePaid)
        {
            GearId = gearId; PricePaid = pricePaid;
        }
    }
}
