using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Art;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// Headless guard for <see cref="AmbientParticleMath.RainIntensity"/> — the SHARED, DERIVED rain signal the
    /// falling-rain emitter and (later) the water-shader rain-rings both read, so drops and rings agree. Rain is
    /// art-only (there is NO precipitation axis in the sim): it rises with sea-state (chop building) and is
    /// GATED by low visibility (a squall = chop AND murk). No Unity scene needed — pure, deterministic feel-math,
    /// so a regression (or a NaN at an edge) is caught in CI without opening Unity. Mirrors
    /// <c>AmbientParticleMathTests</c> (the MistIntensity block).
    /// </summary>
    public class RainIntensityTests
    {
        private const float Eps = 1e-4f;

        // ---- ~0 on a calm clear day (feature off with baseline 0) ----------------------------------------

        [Test]
        public void RainIntensity_ClearGlassyDay_IsBaseline()
        {
            // Glass (sea 0) + clear (visibility 1): only the baseline. Default baseline 0 → no rain at all.
            float r = AmbientParticleMath.RainIntensity(1f, 0f, baseline: 0f, seaStateWeight: 1f, visibilityGate: 0.6f);
            Assert.AreEqual(0f, r, Eps, "Calm clear day with baseline 0 → no rain (feature OFF).");
        }

        [Test]
        public void RainIntensity_ClearGlassyDay_HonoursBaseline()
        {
            float r = AmbientParticleMath.RainIntensity(1f, 0f, baseline: 0.2f, seaStateWeight: 1f, visibilityGate: 0.6f);
            Assert.AreEqual(0.2f, r, Eps, "With a baseline dialled in, the glassy-clear floor is exactly that baseline.");
        }

        // ---- monotonic increasing in sea-state -----------------------------------------------------------

        [Test]
        public void RainIntensity_MonotonicInSeaState()
        {
            float prev = -1f;
            for (int i = 0; i <= 20; i++)
            {
                float sea = i / 20f;
                // Hold visibility murky so the gate is open and the sea-state drive shows through.
                float r = AmbientParticleMath.RainIntensity(0.2f, sea, 0f, 1f, 0.6f);
                Assert.GreaterOrEqual(r + Eps, prev, "Rain must not decrease as the sea builds.");
                prev = r;
            }
        }

        [Test]
        public void RainIntensity_HigherSeaState_MoreRain()
        {
            float calm = AmbientParticleMath.RainIntensity(0.3f, 0.1f, 0f, 1f, 0.6f);
            float rough = AmbientParticleMath.RainIntensity(0.3f, 0.9f, 0f, 1f, 0.6f);
            Assert.Greater(rough, calm, "A rougher sea (more chop) drives more rain.");
        }

        // ---- increased by LOWER visibility (the murk gate opens) -----------------------------------------

        [Test]
        public void RainIntensity_LowerVisibility_MoreRain()
        {
            // Same building sea; the murkier sky opens the gate wider → more rain.
            float clear = AmbientParticleMath.RainIntensity(1f, 0.7f, 0f, 1f, 0.6f);
            float murky = AmbientParticleMath.RainIntensity(0.1f, 0.7f, 0f, 1f, 0.6f);
            Assert.Greater(murky, clear, "Low visibility (murk) must unlock more rain for the same chop.");
        }

        [Test]
        public void RainIntensity_MonotonicAsVisibilityFalls()
        {
            float prev = -1f;
            for (int i = 0; i <= 20; i++)
            {
                float vis = 1f - i / 20f;   // visibility falls → murk rises
                float r = AmbientParticleMath.RainIntensity(vis, 0.6f, 0f, 1f, 0.6f);
                Assert.GreaterOrEqual(r + Eps, prev, "Rain must not decrease as the sky goes murkier.");
                prev = r;
            }
        }

        [Test]
        public void RainIntensity_ClearRoughSea_IsThrottledByTheGate()
        {
            // A blustery BRIGHT day (rough but clear) is NOT a downpour: with a full gate the clear-air rain is
            // only (1 - gate) of the murky-air rain, so it stays a fraction of a full squall.
            float clearRough = AmbientParticleMath.RainIntensity(1f, 1f, 0f, 1f, visibilityGate: 1f);
            float murkyRough = AmbientParticleMath.RainIntensity(0f, 1f, 0f, 1f, visibilityGate: 1f);
            Assert.AreEqual(0f, clearRough, Eps, "Full gate + clear sky → the chop alone brings no rain.");
            Assert.AreEqual(1f, murkyRough, Eps, "Full gate + full murk + full chop → a full downpour.");
        }

        [Test]
        public void RainIntensity_ZeroGate_RainsPurelyOnChop()
        {
            // Gate 0 removes the visibility requirement: rain tracks sea-state regardless of the sky.
            float clear = AmbientParticleMath.RainIntensity(1f, 0.5f, 0f, 1f, visibilityGate: 0f);
            float murky = AmbientParticleMath.RainIntensity(0f, 0.5f, 0f, 1f, visibilityGate: 0f);
            Assert.AreEqual(0.5f, clear, Eps, "No gate → rain = seaStateWeight*sea, visibility irrelevant.");
            Assert.AreEqual(clear, murky, Eps, "No gate → visibility does not change the rain.");
        }

        // ---- clamped to [0,1] -----------------------------------------------------------------------------

        [Test]
        public void RainIntensity_ClampsToOne()
        {
            float r = AmbientParticleMath.RainIntensity(0f, 1f, 0.5f, 2f, 1f);
            Assert.AreEqual(1f, r, Eps, "Intensity saturates at 1.");
        }

        [Test]
        public void RainIntensity_NeverNegative_AndNeverNaN_AcrossTheGrid()
        {
            for (int vi = 0; vi <= 10; vi++)
            for (int si = 0; si <= 10; si++)
            {
                float vis = vi / 10f;
                float sea = si / 10f;
                float r = AmbientParticleMath.RainIntensity(vis, sea, 0.1f, 1.2f, 0.7f);
                Assert.IsFalse(float.IsNaN(r), "Rain intensity is never NaN.");
                Assert.GreaterOrEqual(r, -Eps, "Rain intensity is never negative.");
                Assert.LessOrEqual(r, 1f + Eps, "Rain intensity never exceeds 1.");
            }
        }

        // ---- deterministic (same inputs → same output) ---------------------------------------------------

        [Test]
        public void RainIntensity_IsDeterministic()
        {
            float a = AmbientParticleMath.RainIntensity(0.35f, 0.62f, 0.05f, 1.1f, 0.65f);
            float b = AmbientParticleMath.RainIntensity(0.35f, 0.62f, 0.05f, 1.1f, 0.65f);
            Assert.AreEqual(a, b, 0f, "Same inputs must produce the exact same output (no RNG, rule 5).");
        }
    }
}
