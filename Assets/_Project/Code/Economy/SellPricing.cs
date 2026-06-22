using UnityEngine;

namespace HiddenHarbours.Economy
{
    /// <summary>
    /// Pure marginal-pricing maths for the wharf sell screen (VS-18). Landing each unit floods the
    /// local market a little, so the NEXT identical unit fetches less — the player watches the price
    /// "self-glutt" as they drag the quantity slider. Built on the same <see cref="MarketMath"/> the
    /// buyer uses, so the running total the screen shows is exactly what the sale pays out. No Unity
    /// state → EditMode-testable.
    /// </summary>
    public static class SellPricing
    {
        /// <summary>The supply one sold unit adds to its category — the rate the screen previews the
        /// self-glutt at. Matches Market's default <c>_supplyPerSale</c> (1 per unit); the single
        /// tunable for the preview, kept here so the preview and the registered glut stay in step.</summary>
        public const float SupplyPerUnit = 1f;

        /// <summary>₲ a single unit fetches at a given category supply (≥1, mirrors FishBuyer's rounding).
        /// Neutral demand (D=1); the demand-aware overload is below.</summary>
        public static int UnitPrice(int baseValue, float elasticity, float supply)
            => UnitPrice(baseValue, elasticity, supply, 1f);

        /// <summary>₲ a single unit fetches at a given category supply AND per-category demand D
        /// (≥1, mirrors FishBuyer's rounding). Higher D holds the price up at the same supply.</summary>
        public static int UnitPrice(int baseValue, float elasticity, float supply, float demand)
            => Mathf.Max(1, Mathf.RoundToInt(baseValue * MarketMath.PriceMultiplier(supply, elasticity, demand)));

        /// <summary>₲ the <paramref name="unitIndex"/>-th *additional* unit fetches (0-based), each prior
        /// unit having already glutted the market — i.e. the marginal price as you drag the slider.</summary>
        public static int MarginalPrice(int baseValue, float elasticity, float supplyBefore, int unitIndex)
            => MarginalPrice(baseValue, elasticity, supplyBefore, unitIndex, 1f);

        /// <summary>The marginal price of the <paramref name="unitIndex"/>-th additional unit at the
        /// category's demand D — what the demand-aware sell path charges.</summary>
        public static int MarginalPrice(int baseValue, float elasticity, float supplyBefore, int unitIndex, float demand)
            => UnitPrice(baseValue, elasticity, supplyBefore + Mathf.Max(0, unitIndex) * SupplyPerUnit, demand);

        /// <summary>₲ for selling <paramref name="quantity"/> units into the current supply — the running
        /// total the screen shows, summing each unit's self-glutted marginal price. This is exactly what
        /// <see cref="SellService"/> pays out, so the displayed total equals the coin received.</summary>
        public static int RunningTotal(int baseValue, float elasticity, float supplyBefore, int quantity)
            => RunningTotal(baseValue, elasticity, supplyBefore, quantity, 1f);

        /// <summary>Running total for selling <paramref name="quantity"/> units at the category's demand D.</summary>
        public static int RunningTotal(int baseValue, float elasticity, float supplyBefore, int quantity, float demand)
        {
            int q = Mathf.Max(0, quantity);
            int total = 0;
            for (int j = 0; j < q; j++)
                total += MarginalPrice(baseValue, elasticity, supplyBefore, j, demand);
            return total;
        }
    }
}
