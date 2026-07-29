namespace HiddenHarbours.Core
{
    /// <summary>
    /// One landed fish, as it sits in a hold and goes to market. Carries just enough cached data
    /// (id, category, value, elasticity) that the hold and the market never need to reference the
    /// Fishing module's <c>FishSpeciesDef</c> — keeps Boats/Economy depending only on Core.
    /// One CatchItem = one hold unit (HU) in the greybox.
    ///
    /// <para><b>Freshness (M1 §7.3).</b> A catch carries its own clock: <see cref="SpoilPerDay"/>
    /// (the species' ambient rot rate, cached off the Def like <see cref="BaseValue"/> is) and a
    /// <see cref="Core.FreshnessState"/> stamped at landing (<see cref="Freshness.Landed"/>). Every
    /// path that creates a catch stamps it via <see cref="WithFreshness"/>; the legacy constructor
    /// leaves <see cref="SpoilPerDay"/> at 0, so an unstamped item never rots — old tests and
    /// callers behave exactly as before this field existed.</para>
    /// </summary>
    public readonly struct CatchItem
    {
        public readonly string SpeciesId;
        public readonly string DisplayName;
        public readonly FishCategory Category;
        public readonly float WeightKg;
        public readonly int BaseValue;          // ₲ at a neutral market
        public readonly float SupplyElasticity; // how much landing it depresses the price

        /// <summary>The species' ambient rot rate (spoil per in-game day; 1 = ruined in one day left
        /// in the open, 0 = never rots). Cached off <c>FishSpeciesDef.SpoilPerDay</c> at creation so
        /// downstream readers (the sell gate, the hold read) never reference the Fishing module.</summary>
        public readonly float SpoilPerDay;

        /// <summary>This catch's freshness clock: spoil banked, the instant it was banked, the mode
        /// held since (see <see cref="Core.FreshnessState"/>). Stamped at landing.</summary>
        public readonly FreshnessState Freshness;

        /// <summary>Full constructor — a catch carrying its freshness clock (M1 §7.3).</summary>
        public CatchItem(string speciesId, string displayName, FishCategory category,
                         float weightKg, int baseValue, float supplyElasticity,
                         float spoilPerDay, in FreshnessState freshness)
        {
            SpeciesId = speciesId;
            DisplayName = displayName;
            Category = category;
            WeightKg = weightKg;
            BaseValue = baseValue;
            SupplyElasticity = supplyElasticity;
            SpoilPerDay = spoilPerDay;
            Freshness = freshness;
        }

        /// <summary>Legacy constructor (preserved): no perishability — <see cref="SpoilPerDay"/> is 0,
        /// so the item never rots and prices exactly as it did before freshness existed.</summary>
        public CatchItem(string speciesId, string displayName, FishCategory category,
                         float weightKg, int baseValue, float supplyElasticity)
            : this(speciesId, displayName, category, weightKg, baseValue, supplyElasticity,
                   0f, default)
        {
        }

        /// <summary>The same catch with its freshness clock replaced — how a landing path stamps
        /// <see cref="Core.Freshness.Landed"/> onto an item, and how a mode change writes the settled
        /// state back (readonly struct: replace, never mutate).</summary>
        public CatchItem WithFreshness(in FreshnessState freshness)
            => new CatchItem(SpeciesId, DisplayName, Category, WeightKg, BaseValue, SupplyElasticity,
                             SpoilPerDay, freshness);

        public override string ToString() => $"{DisplayName} ({WeightKg:0.0} kg)";
    }
}
