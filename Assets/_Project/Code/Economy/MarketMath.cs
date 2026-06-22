using UnityEngine;

namespace HiddenHarbours.Economy
{
    /// <summary>
    /// Pure supply-and-demand pricing (no Unity state) so it is unit-testable. Price falls as a
    /// category's supply rises (gluts crash prices) and recovers as supply decays (scarcity). A
    /// market's <b>demand</b> D sets how much supply it can absorb before the price slides, so two
    /// markets with different D price the same glut differently — the reason to choose WHERE to sell
    /// (VS-16). See design/economy-and-business.md §1.2–§1.4.
    ///
    /// <para><b>API stability:</b> the existing members are kept verbatim and the new demand-aware
    /// paths are purely additive (the 2-arg <see cref="PriceMultiplier(float,float)"/> is exactly the
    /// D=1 case), so the sell screen / callers building against this in parallel never break.</para>
    /// </summary>
    public static class MarketMath
    {
        public const float MinMultiplier = 0.25f;  // a glut floor
        public const float MaxMultiplier = 1.6f;   // a scarcity ceiling
        public const float MinDemand     = 0.01f;  // guards the S/D divide; demand is never ≤ 0

        /// <summary>
        /// Price multiplier on base value for a local supply, the item's elasticity, and the market's
        /// demand: <c>clamp( 1 / (1 + e·S/D) , Min, Max )</c> (economy §1.2). Higher demand D absorbs
        /// more supply before the price slides; higher elasticity e crashes it faster.
        /// </summary>
        public static float PriceMultiplier(float supply, float elasticity, float demand)
        {
            float s = Mathf.Max(0f, supply);
            float e = Mathf.Max(0f, elasticity);
            float d = Mathf.Max(MinDemand, demand);
            return Mathf.Clamp(1f / (1f + s * e / d), MinMultiplier, MaxMultiplier);
        }

        /// <summary>Price multiplier at neutral demand (D = 1). The original 2-arg contract, unchanged.</summary>
        public static float PriceMultiplier(float supply, float elasticity)
            => PriceMultiplier(supply, elasticity, 1f);

        /// <summary>
        /// The ₲ price of ONE unit sold at a given supply level — the marginal price the sell screen
        /// shows as you drag the quantity (each extra unit pushes supply up by the market's per-sale
        /// amount, so the next unit is worth a little less: you slide down your own curve). Rounds and
        /// floors exactly like the buyer pays (<see cref="FishBuyer"/>), so the screen total matches the
        /// coin received. Non-increasing in <paramref name="supplyBefore"/>.
        /// </summary>
        public static int MarginalPrice(int baseValue, float supplyBefore, float elasticity, float demand)
            => Mathf.Max(1, Mathf.RoundToInt(baseValue * PriceMultiplier(supplyBefore, elasticity, demand)));

        /// <summary>Marginal unit price at neutral demand (D = 1).</summary>
        public static int MarginalPrice(int baseValue, float supplyBefore, float elasticity)
            => MarginalPrice(baseValue, supplyBefore, elasticity, 1f);

        /// <summary>Decay a supply level toward zero (price recovers over time). Continuous form.</summary>
        public static float DecaySupply(float supply, float perSecond, float dt)
            => Mathf.Max(0f, supply - Mathf.Max(0f, perSecond) * dt);

        /// <summary>
        /// The daily settle: clear a fraction of a category's accumulated supply (glut) at each day
        /// rollover, so a price crashed today recovers over the next day(s) (economy §1.3). Deterministic
        /// — a function of the supply and the recovery fraction only, NOT of frame timing.
        /// <paramref name="dailyRecoveryFraction"/> in [0,1]: 0 keeps the glut, 1 clears it fully.
        /// </summary>
        public static float SettleSupplyDaily(float supply, float dailyRecoveryFraction)
            => Mathf.Max(0f, supply * (1f - Mathf.Clamp01(dailyRecoveryFraction)));
    }
}
