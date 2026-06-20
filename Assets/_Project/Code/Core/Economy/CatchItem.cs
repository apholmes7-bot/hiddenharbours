namespace HiddenHarbours.Core
{
    /// <summary>
    /// One landed fish, as it sits in a hold and goes to market. Carries just enough cached data
    /// (id, category, value, elasticity) that the hold and the market never need to reference the
    /// Fishing module's <c>FishSpeciesDef</c> — keeps Boats/Economy depending only on Core.
    /// One CatchItem = one hold unit (HU) in the greybox.
    /// </summary>
    public readonly struct CatchItem
    {
        public readonly string SpeciesId;
        public readonly string DisplayName;
        public readonly FishCategory Category;
        public readonly float WeightKg;
        public readonly int BaseValue;          // ₲ at a neutral market
        public readonly float SupplyElasticity; // how much landing it depresses the price

        public CatchItem(string speciesId, string displayName, FishCategory category,
                         float weightKg, int baseValue, float supplyElasticity)
        {
            SpeciesId = speciesId;
            DisplayName = displayName;
            Category = category;
            WeightKg = weightKg;
            BaseValue = baseValue;
            SupplyElasticity = supplyElasticity;
        }

        public override string ToString() => $"{DisplayName} ({WeightKg:0.0} kg)";
    }
}
