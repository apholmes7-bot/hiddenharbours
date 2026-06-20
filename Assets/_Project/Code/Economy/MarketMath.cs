using UnityEngine;

namespace HiddenHarbours.Economy
{
    /// <summary>
    /// Pure supply-and-demand pricing (no Unity state) so it is unit-testable. Price falls as a
    /// category's supply rises (gluts crash prices) and recovers as supply decays (scarcity).
    /// See design/economy-and-business.md §1.
    /// </summary>
    public static class MarketMath
    {
        public const float MinMultiplier = 0.25f;  // a glut floor
        public const float MaxMultiplier = 1.6f;   // a scarcity ceiling

        /// <summary>Price multiplier on base value for a given local supply and the item's elasticity.</summary>
        public static float PriceMultiplier(float supply, float elasticity)
        {
            float s = Mathf.Max(0f, supply);
            float e = Mathf.Max(0f, elasticity);
            return Mathf.Clamp(1f / (1f + s * e), MinMultiplier, MaxMultiplier);
        }

        /// <summary>Decay a supply level toward zero (price recovers over time).</summary>
        public static float DecaySupply(float supply, float perSecond, float dt)
            => Mathf.Max(0f, supply - Mathf.Max(0f, perSecond) * dt);
    }
}
