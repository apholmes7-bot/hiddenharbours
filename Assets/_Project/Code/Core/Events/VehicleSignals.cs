namespace HiddenHarbours.Core
{
    /// <summary>
    /// Raised when a land vehicle becomes the player's: the money is already spent and the deed is
    /// already recorded (<see cref="VehicleDeeds"/>), exactly as <see cref="HomePurchased"/> is raised
    /// after its deed. Economy publishes it; the Vehicles module (which machine to hand over, where it
    /// parks), world-content (a story beat) and ui-ux (a toast) subscribe without referencing Economy
    /// (rule 4).
    ///
    /// <para><b>Keyed by the OFFER, not the model.</b> <see cref="OfferId"/> (e.g.
    /// <c>vehicleoffer.nmc_dealer_modern_3500</c>) is what the deed records, so two offers for the same
    /// model are two machines. <see cref="VehicleId"/> is the <c>VehicleDef</c> id the Vehicles module
    /// spawns from. <see cref="SellerId"/> names the counter that sold it (<c>seller.snake_case</c>),
    /// as the goods purchase signals do, so a listener can tell the dealer from the industrial yard.</para>
    ///
    /// <para><see cref="StartsDamaged"/> is true for a used machine that must be repaired before it runs
    /// (<see cref="VehicleRepaired"/> follows when it is). A listener that hands over a vehicle should
    /// read it rather than assume every purchase drives away.</para>
    ///
    /// <para>Its own Core/Events file beside <c>HomeSignals.cs</c>, for the reason that file gives:
    /// <c>GameSignals.cs</c> is lead-architect's, so new signals land additively beside it.</para>
    /// </summary>
    public readonly struct VehiclePurchased
    {
        public readonly string OfferId;
        public readonly string VehicleId;
        public readonly string SellerId;
        public readonly int PricePaid;   // ₲
        public readonly bool StartsDamaged;
        public VehiclePurchased(string offerId, string vehicleId, string sellerId, int pricePaid, bool startsDamaged)
        {
            OfferId = offerId; VehicleId = vehicleId; SellerId = sellerId;
            PricePaid = pricePaid; StartsDamaged = startsDamaged;
        }
    }

    /// <summary>
    /// Raised when a damaged used vehicle the player owns has been paid for and repaired, so it now
    /// runs. The money is already spent and the repair already recorded (<see cref="VehicleDeeds"/>).
    /// The boat side's <see cref="BoatRepaired"/> is the precedent; same keys as
    /// <see cref="VehiclePurchased"/>.
    /// </summary>
    public readonly struct VehicleRepaired
    {
        public readonly string OfferId;
        public readonly string VehicleId;
        public readonly string SellerId;
        public readonly int PricePaid;   // ₲
        public VehicleRepaired(string offerId, string vehicleId, string sellerId, int pricePaid)
        {
            OfferId = offerId; VehicleId = vehicleId; SellerId = sellerId; PricePaid = pricePaid;
        }
    }
}
