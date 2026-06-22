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

        /// <summary>₲ a single unit fetches at a given category supply AND the market's demand
        /// <paramref name="demand"/> (≥1, mirrors FishBuyer's rounding). Demand defaults to 1 (neutral) so
        /// callers that don't care WHERE the sale lands are unchanged; the sell path passes the live
        /// <see cref="Market.DemandFactor"/> so a higher-demand buyer (Greywick) pays more (VS-16 + VS-18).</summary>
        public static int UnitPrice(int baseValue, float elasticity, float supply, float demand = 1f)
            => Mathf.Max(1, Mathf.RoundToInt(baseValue * MarketMath.PriceMultiplier(supply, elasticity, demand)));

        /// <summary>₲ the <paramref name="unitIndex"/>-th *additional* unit fetches (0-based), each prior
        /// unit having already glutted the market — i.e. the marginal price as you drag the slider, priced
        /// at the market's <paramref name="demand"/> (default 1 = neutral).</summary>
        public static int MarginalPrice(int baseValue, float elasticity, float supplyBefore, int unitIndex, float demand = 1f)
            => UnitPrice(baseValue, elasticity, supplyBefore + Mathf.Max(0, unitIndex) * SupplyPerUnit, demand);

        /// <summary>₲ for selling <paramref name="quantity"/> units into the current supply at the market's
        /// <paramref name="demand"/> (default 1 = neutral) — the running total the screen shows, summing each
        /// unit's self-glutted marginal price. This is exactly what <see cref="SellService"/> pays out, so the
        /// displayed total equals the coin received.</summary>
        public static int RunningTotal(int baseValue, float elasticity, float supplyBefore, int quantity, float demand = 1f)
        {
            int q = Mathf.Max(0, quantity);
            int total = 0;
            for (int j = 0; j < q; j++)
                total += MarginalPrice(baseValue, elasticity, supplyBefore, j, demand);
            return total;
        }
    }
}
