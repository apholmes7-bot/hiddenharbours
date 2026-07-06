using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Art;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// Headless guard for <see cref="PlayerSubmergeMath.WaterlineFraction"/> — the depth→waterline mapping the
    /// on-foot submersion shader clips on (how far UP the body the water reaches, from the feet toward the
    /// head). The whole point is that DRY is a pixel-identical passthrough (frac 0), the line RISES with depth
    /// (feet→shins→knees→chest), and it CAPS at the neck so the head + hat never submerge (even in the swim
    /// band at ~2 m). No Unity scene needed — pure, deterministic feel-math, so a regression (or a NaN at an
    /// edge like the un-gated NegativeInfinity depth) is caught in CI without opening Unity. Mirrors
    /// <see cref="WadeSplashRateTests"/>.
    /// </summary>
    public class PlayerSubmergeFractionTests
    {
        private const float Eps = 1e-4f;

        // Representative tunables mirroring PlayerSubmergeVisual's defaults.
        private const float BodyHeight = 1.8f;
        private const float MaxSubmerge = 0.85f;

        private static float Frac(float depth)
            => PlayerSubmergeMath.WaterlineFraction(depth, BodyHeight, MaxSubmerge);

        // ---- DRY → passthrough (frac 0): dry land looks exactly as today --------------------------------

        [Test]
        public void ZeroDepth_IsPassthrough()
        {
            Assert.AreEqual(0f, Frac(0f), Eps, "Depth 0 → waterline at the feet → passthrough (dry).");
        }

        [Test]
        public void NegativeDepth_IsPassthrough()
        {
            Assert.AreEqual(0f, Frac(-0.5f), Eps, "Exposed ground (negative depth) → passthrough (dry).");
        }

        [Test]
        public void UnGatedRegion_NegativeInfinityDepth_IsPassthrough()
        {
            // A region with no tide-gated terrain reports NegativeInfinity depth ("as dry as can be").
            Assert.AreEqual(0f, Frac(float.NegativeInfinity), Eps,
                "An un-tide-gated region (NegativeInfinity depth) → passthrough, never a submerged body.");
        }

        // ---- WADING → the line rises partway up the body ------------------------------------------------

        [Test]
        public void ShallowWade_IsPartialUpTheBody()
        {
            float f = Frac(0.25f);   // ankle/shin-deep
            Assert.Greater(f, 0f, "A shallow wade lifts the waterline off the feet.");
            Assert.Less(f, MaxSubmerge, "A shallow wade is nowhere near the neck cap.");
            Assert.AreEqual(0.25f / BodyHeight, f, Eps, "Shallow wade = depth / body height.");
        }

        [Test]
        public void KneeToChest_RisesWithDepth()
        {
            float shin  = Frac(0.3f);
            float knee  = Frac(0.6f);
            float chest = Frac(1.2f);
            Assert.Greater(knee, shin, "Deeper water lifts the line higher (shin → knee).");
            Assert.Greater(chest, knee, "Deeper still lifts it to the chest (knee → chest).");
        }

        // ---- NECK-DEEP CAP → the head never submerges ---------------------------------------------------

        [Test]
        public void AtBodyHeight_IsCappedAtNeck_NeverFull()
        {
            float f = Frac(BodyHeight);   // water at the (uncapped) top of the body
            Assert.AreEqual(MaxSubmerge, f, Eps, "At a full body-height of water the line is CLAMPED to the neck cap.");
            Assert.Less(f, 1f, "The line never reaches the head (frac 1) — the head + hat stay above.");
        }

        [Test]
        public void SwimBandDepth_TwoMetres_StaysAtNeckCap()
        {
            // The swim band at ~2 m is deeper than the body — the cap still holds (head above water).
            Assert.AreEqual(MaxSubmerge, Frac(2.0f), Eps, "Neck-deep even in the swim band; the head never goes under.");
            Assert.AreEqual(MaxSubmerge, Frac(50f), Eps, "Absurd depth still caps at the neck — no runaway.");
        }

        // ---- monotonic non-decreasing in depth ----------------------------------------------------------

        [Test]
        public void MonotonicNonDecreasingInDepth()
        {
            float prev = -1f;
            for (int i = 0; i <= 60; i++)
            {
                float depth = i / 60f * (BodyHeight * 1.5f);   // walk past the cap
                float f = Frac(depth);
                Assert.GreaterOrEqual(f + Eps, prev, "The waterline must not drop as the water deepens.");
                Assert.LessOrEqual(f, MaxSubmerge + Eps, "Never above the neck cap.");
                prev = f;
            }
        }

        // ---- robust: clamps garbage, never NaN, never above the cap -------------------------------------

        [Test]
        public void NaNDepth_IsPassthrough_NotNaN()
        {
            float f = Frac(float.NaN);
            Assert.IsFalse(float.IsNaN(f), "A NaN depth must not propagate — it clamps to a dry passthrough.");
            Assert.AreEqual(0f, f, Eps, "NaN → dry (0), the safe passthrough.");
        }

        [Test]
        public void ZeroBodyHeight_DoesNotDivideByZero()
        {
            // A degenerate body height of 0 must not NaN/Inf — any positive depth collapses to the cap.
            float f = PlayerSubmergeMath.WaterlineFraction(1.0f, 0f, MaxSubmerge);
            Assert.IsFalse(float.IsNaN(f), "A zero body height must not divide by zero.");
            Assert.AreEqual(MaxSubmerge, f, Eps, "With no body height any positive depth reads as fully (neck) submerged.");
        }

        [Test]
        public void MaxSubmergeIsClampedTo01()
        {
            // An out-of-range cap is clamped, so the line can never exceed the head even if mis-tuned.
            float f = PlayerSubmergeMath.WaterlineFraction(5f, BodyHeight, 2f);   // cap > 1
            Assert.LessOrEqual(f, 1f + Eps, "An over-1 cap is clamped to 1 — the line never leaves the sprite.");
            float g = PlayerSubmergeMath.WaterlineFraction(5f, BodyHeight, -1f);  // cap < 0
            Assert.AreEqual(0f, g, Eps, "A negative cap clamps to 0 — no submersion.");
        }

        [Test]
        public void NeverNegative_NeverNaN_AcrossTheGrid()
        {
            for (int i = -10; i <= 60; i++)
            {
                float depth = i / 10f;
                float f = Frac(depth);
                Assert.IsFalse(float.IsNaN(f), "Waterline fraction is never NaN.");
                Assert.GreaterOrEqual(f, -Eps, "Waterline fraction is never negative.");
                Assert.LessOrEqual(f, MaxSubmerge + Eps, "Waterline fraction never exceeds the neck cap.");
            }
        }

        // ---- deterministic (rule 5) ---------------------------------------------------------------------

        [Test]
        public void IsDeterministic()
        {
            float a = Frac(0.7f);
            float b = Frac(0.7f);
            Assert.AreEqual(a, b, 0f, "Same inputs must produce the exact same output (no RNG, rule 5).");
        }
    }
}
