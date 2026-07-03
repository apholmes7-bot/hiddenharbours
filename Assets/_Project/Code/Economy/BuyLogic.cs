namespace HiddenHarbours.Economy
{
    /// <summary>What a buy-screen row sells (which vendor seam Confirm invokes).</summary>
    public enum BuyRowKind
    {
        /// <summary>A boat purchase (<see cref="Shipwright"/>.TryBuy).</summary>
        Boat,
        /// <summary>A repair on an owned-but-damaged boat (<see cref="Shipwright"/>.TryRepair).</summary>
        BoatRepair,
        /// <summary>A gear purchase (<see cref="GearShop"/>.TryBuy).</summary>
        Gear,
        /// <summary>A licence purchase (<see cref="LicenseVendor"/>.TryBuy).</summary>
        License
    }

    /// <summary>
    /// The resolved state of one buy-screen row: what action it offers, at what effective price, and
    /// whether the player may take it. Pure value type — no Unity, fully EditMode-testable.
    /// </summary>
    public readonly struct BuyQuote
    {
        /// <summary>Which vendor action this row performs on Confirm.</summary>
        public readonly BuyRowKind Kind;
        /// <summary>Effective ₲ for the action (the REPAIR cost on a <see cref="BuyRowKind.BoatRepair"/> row).</summary>
        public readonly int Price;
        /// <summary>Already own/hold it — nothing left to buy at this row.</summary>
        public readonly bool Owned;
        /// <summary>The player's money covers <see cref="Price"/> (only meaningful when not owned).</summary>
        public readonly bool Affordable;

        /// <summary>True iff Confirm should be enabled: not already owned, and affordable.</summary>
        public bool CanBuy => !Owned && Affordable;

        public BuyQuote(BuyRowKind kind, int price, bool owned, bool affordable)
        {
            Kind = kind; Price = price; Owned = owned; Affordable = affordable;
        }
    }

    /// <summary>
    /// The pure affordability/state rules behind the buy screen (VS-16 ui-ux side). The screen renders
    /// these quotes and routes Confirm to the vendors' existing seams — it never re-implements purchase
    /// economics (those stay in <see cref="Shipwright"/>/<see cref="GearShop"/>/<see cref="LicenseVendor"/>,
    /// which remain the single owners of TrySpend + events). Static, no Unity state → EditMode-testable.
    /// </summary>
    public static class BuyLogic
    {
        /// <summary>Quote a gear offer: owned gear shows as owned; otherwise gate on money.</summary>
        public static BuyQuote Gear(int price, int money, bool owned)
            => new BuyQuote(BuyRowKind.Gear, price, owned, money >= price);

        /// <summary>Quote a licence: an already-held licence shows as owned; otherwise gate on money.</summary>
        public static BuyQuote License(int fee, int money, bool held)
            => new BuyQuote(BuyRowKind.License, fee, held, money >= fee);

        /// <summary>
        /// Quote a boat offer through its whole life:
        /// not owned → BUY at <paramref name="price"/>; owned + damaged + unrepaired → REPAIR at
        /// <paramref name="repairCost"/> (the St Peters dory flow); owned and usable → owned, nothing to buy.
        /// </summary>
        public static BuyQuote Boat(int price, int repairCost, int money,
            bool owned, bool startsDamaged, bool repaired)
        {
            if (!owned)
                return new BuyQuote(BuyRowKind.Boat, price, owned: false, affordable: money >= price);
            if (startsDamaged && !repaired)
                return new BuyQuote(BuyRowKind.BoatRepair, repairCost, owned: false, affordable: money >= repairCost);
            return new BuyQuote(BuyRowKind.Boat, price, owned: true, affordable: money >= price);
        }
    }
}
