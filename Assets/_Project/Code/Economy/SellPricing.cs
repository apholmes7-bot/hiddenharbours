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
            => UnitPrice(baseValue, elasticity, supply, demand, 1f);

        /// <summary>₲ a single unit fetches with the FRESHNESS value multiplier folded in (M1 §7.3:
        /// fresh pays full, value falls linearly toward nothing). At multiplier 1 this is bit-identical
        /// to the freshness-blind price. ⚠ The ≥1 floor means this multiplier alone can NEVER make a
        /// unit worthless — refusal is a hard gate the caller applies BEFORE pricing
        /// (<c>SpoilContext.IsSellable</c>); an unsellable unit must never reach this method.</summary>
        public static int UnitPrice(int baseValue, float elasticity, float supply, float demand,
                                    float valueMultiplier01)
            => UnitPrice(baseValue, elasticity, supply, demand, valueMultiplier01, 1f);

        /// <summary>
        /// ₲ a single unit fetches with the OUTLET'S PRICE LEVEL folded in as well (plan-to-m1 §7.5) — what
        /// this buyer pays per unit before any glut, as a multiplier on base value (<see cref="Market.PriceLevel"/>).
        /// At level 1 this is bit-identical to the level-blind price, so every existing caller is unchanged.
        ///
        /// <para><b>Why a level and not just demand.</b> Demand D only appears as <c>S/D</c> in the supply
        /// curve, so at zero supply it cannot move the price at all — two outlets with wildly different
        /// demand quote the same coin for the first unit anyone sells them. The level is what makes the
        /// island general store genuinely pay worse than Nine Mile Creek from the very first clam.</para>
        ///
        /// <para>⚠ The ≥1 floor still applies last, so on a low-base-value species (a 2₲ clam) the level has
        /// very little room to move the number — the gap the player can actually *read* is bounded by the
        /// species' base value, not by this multiplier.</para>
        /// </summary>
        public static int UnitPrice(int baseValue, float elasticity, float supply, float demand,
                                    float valueMultiplier01, float priceLevel)
            => Mathf.Max(1, Mathf.RoundToInt(baseValue * MarketMath.PriceMultiplier(supply, elasticity, demand)
                                             * Mathf.Clamp01(valueMultiplier01)
                                             * Mathf.Max(0f, priceLevel)));

        /// <summary>₲ the <paramref name="unitIndex"/>-th *additional* unit fetches (0-based), each prior
        /// unit having already glutted the market — i.e. the marginal price as you drag the slider.</summary>
        public static int MarginalPrice(int baseValue, float elasticity, float supplyBefore, int unitIndex)
            => MarginalPrice(baseValue, elasticity, supplyBefore, unitIndex, 1f);

        /// <summary>The marginal price of the <paramref name="unitIndex"/>-th additional unit at the
        /// category's demand D — what the demand-aware sell path charges.</summary>
        public static int MarginalPrice(int baseValue, float elasticity, float supplyBefore, int unitIndex, float demand)
            => MarginalPrice(baseValue, elasticity, supplyBefore, unitIndex, demand, 1f);

        /// <summary>The marginal price of one specific unit with ITS OWN freshness multiplier — two
        /// units of the same species can carry different spoil, so the multiplier is per-unit, not
        /// per-species. See the freshness <see cref="UnitPrice(int,float,float,float,float)"/> overload
        /// for the floor/refusal contract.</summary>
        public static int MarginalPrice(int baseValue, float elasticity, float supplyBefore, int unitIndex,
                                        float demand, float valueMultiplier01)
            => MarginalPrice(baseValue, elasticity, supplyBefore, unitIndex, demand, valueMultiplier01, 1f);

        /// <summary>The marginal price of one specific unit at this OUTLET'S price level as well — the full
        /// per-unit price the till pays (supply curve × demand × freshness × outlet level, floored at ₲1).
        /// At level 1 it is bit-identical to the level-blind overload.</summary>
        public static int MarginalPrice(int baseValue, float elasticity, float supplyBefore, int unitIndex,
                                        float demand, float valueMultiplier01, float priceLevel)
            => UnitPrice(baseValue, elasticity, supplyBefore + Mathf.Max(0, unitIndex) * SupplyPerUnit,
                         demand, valueMultiplier01, priceLevel);

        /// <summary>₲ for selling <paramref name="quantity"/> units into the current supply — the running
        /// total the screen shows, summing each unit's self-glutted marginal price. This is exactly what
        /// <see cref="SellService"/> pays out, so the displayed total equals the coin received.</summary>
        public static int RunningTotal(int baseValue, float elasticity, float supplyBefore, int quantity)
            => RunningTotal(baseValue, elasticity, supplyBefore, quantity, 1f);

        /// <summary>Running total for selling <paramref name="quantity"/> units at the category's demand D.</summary>
        public static int RunningTotal(int baseValue, float elasticity, float supplyBefore, int quantity, float demand)
            => RunningTotal(baseValue, elasticity, supplyBefore, quantity, demand, 1f);

        /// <summary>Running total at the category's demand D <i>and</i> this outlet's price level — what a
        /// whole lot fetches at one buyer. The number that differs between the island store and the Creek.</summary>
        public static int RunningTotal(int baseValue, float elasticity, float supplyBefore, int quantity,
                                       float demand, float priceLevel)
        {
            int q = Mathf.Max(0, quantity);
            int total = 0;
            for (int j = 0; j < q; j++)
                total += MarginalPrice(baseValue, elasticity, supplyBefore, j, demand, 1f, priceLevel);
            return total;
        }
    }
}
