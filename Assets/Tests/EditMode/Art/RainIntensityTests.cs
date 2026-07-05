using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Art;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// Headless guard for <see cref="AmbientParticleMath.RainIntensity"/> — the SHARED, DERIVED rain signal the
    /// falling-rain emitter and the water-shader rain-rings both read, so drops and rings agree. Rain is
    /// art-only (there is NO precipitation axis in the sim) and an OCCASIONAL SQUALL: it needs BOTH genuine low
    /// visibility (real murk, the murk gate) AND real chop (the sea-state onset) — two onsets, not a leaky
    /// linear gate. So a clear or lightly-choppy night stays DRY (the owner-playtest fix). No Unity scene needed
    /// — pure, deterministic feel-math, so a regression (or a NaN at an edge) is caught in CI without opening
    /// Unity. Mirrors <c>AmbientParticleMathTests</c> (the MistIntensity block).
    /// </summary>
    public class RainIntensityTests
    {
        private const float Eps = 1e-4f;

        // The shipped RainConfig.Default onsets — the tests pin the behaviour AT these defaults.
        private const float VisOnset = 0.65f;
        private const float VisFull  = 0.40f;
        private const float SeaOnset = 0.30f;

        private static float Rain(float vis, float sea, float baseline = 0f, float seaStateWeight = 1f,
                                  float visOnset = VisOnset, float visFull = VisFull, float seaOnset = SeaOnset)
            => AmbientParticleMath.RainIntensity(vis, sea, baseline, seaStateWeight, visOnset, visFull, seaOnset);

        // ---- ~0 on a calm clear day (feature off with baseline 0) ----------------------------------------

        [Test]
        public void RainIntensity_ClearGlassyDay_IsBaseline()
        {
            // Glass (sea 0) + clear (visibility 1): only the baseline. Default baseline 0 → no rain at all.
            float r = Rain(1f, 0f);
            Assert.AreEqual(0f, r, Eps, "Calm clear day with baseline 0 → no rain (feature OFF).");
        }

        [Test]
        public void RainIntensity_ClearGlassyDay_HonoursBaseline()
        {
            float r = Rain(1f, 0f, baseline: 0.2f);
            Assert.AreEqual(0.2f, r, Eps, "With a baseline dialled in, the glassy-clear floor is exactly that baseline.");
        }

        // ---- CLEAR AIR RAINS NOTHING regardless of chop (the murk gate is shut) ---------------------------

        [Test]
        public void RainIntensity_ClearAir_IsZero_RegardlessOfSeaState()
        {
            // At/above VisOnset the murk gate is SHUT: no matter how rough the sea, a bright blustery day/night
            // rains NOTHING. This is the crux of the owner-playtest fix (was ~0.18-0.21 on an ordinary Moderate
            // sea in clear air).
            for (int si = 0; si <= 10; si++)
            {
                float sea = si / 10f;
                float clearAtOnset = Rain(VisOnset, sea);            // exactly at the onset
                float crystalClear = Rain(1f, sea);                 // perfectly clear
                Assert.AreEqual(0f, clearAtOnset, Eps, $"Clear air at the onset must rain nothing (sea {sea}).");
                Assert.AreEqual(0f, crystalClear, Eps, $"Crystal-clear air must rain nothing (sea {sea}).");
            }
        }

        [Test]
        public void RainIntensity_TheComplaintCase_ModerateSeaClearNight_IsDry()
        {
            // The exact owner complaint: an ordinary Moderate sea on a clear night. Old leaky gate gave ~0.21;
            // the squall model gives ZERO because the air is well above the murk onset.
            float complaint = Rain(0.85f, 0.43f);
            Assert.AreEqual(0f, complaint, Eps, "Moderate sea on a clear night must be DRY (the fix).");
            float clearish = Rain(0.85f, 0.36f);
            Assert.AreEqual(0f, clearish, Eps, "A lightly-choppy clear night must be DRY too.");
        }

        // ---- NEAR-GLASS RAINS NOTHING even in thick murk (the sea-state onset) ----------------------------

        [Test]
        public void RainIntensity_NearGlass_IsZero_EvenInThickMurk()
        {
            // At/below SeaOnset the sea is too glassy to rain even in the thickest murk — a foggy dead-calm
            // morning is fog, not a squall.
            float glassInMurk   = Rain(0f, 0f);
            float onsetInMurk   = Rain(0f, SeaOnset);
            Assert.AreEqual(0f, glassInMurk, Eps, "Dead-glass sea in thick murk still rains nothing.");
            Assert.AreEqual(0f, onsetInMurk, Eps, "At the sea onset in thick murk, still nothing.");
        }

        // ---- a genuine squall: real murk AND real chop → rising rain -------------------------------------

        [Test]
        public void RainIntensity_RealMurkAndChop_Rains()
        {
            // Both onsets crossed (murk below VisFull, sea well above SeaOnset) → a real squall.
            float squall = Rain(VisFull, 0.7f);
            Assert.Greater(squall, 0.1f, "Real murk + real chop must produce a genuine squall.");
        }

        [Test]
        public void RainIntensity_TargetConditions_MatchDiagnosis()
        {
            // The diagnosis' target curve at the shipped defaults — clear/light nights DRY, rain only as a squall.
            Assert.AreEqual(0.00f, Rain(0.85f, 0.36f), 0.005f, "clear-ish night → ~0");
            Assert.AreEqual(0.00f, Rain(0.85f, 0.43f), 0.005f, "complaint case → ~0");
            Assert.AreEqual(0.03f, Rain(0.55f, 0.43f), 0.01f,  "moderate haze → ~0.03");
            Assert.AreEqual(0.09f, Rain(0.40f, 0.43f), 0.01f,  "real murk+chop → ~0.09");
            Assert.AreEqual(0.27f, Rain(0.35f, 0.54f), 0.01f,  "blow + thick murk → ~0.27");
        }

        // ---- monotonic non-decreasing in sea-state (above the onset, in murk) ----------------------------

        [Test]
        public void RainIntensity_MonotonicInSeaState()
        {
            float prev = -1f;
            for (int i = 0; i <= 20; i++)
            {
                float sea = i / 20f;
                // Hold visibility murky (below VisFull) so the murk gate is fully open and the sea-state onset shows.
                float r = Rain(0.2f, sea);
                Assert.GreaterOrEqual(r + Eps, prev, "Rain must not decrease as the sea builds.");
                prev = r;
            }
        }

        [Test]
        public void RainIntensity_HigherSeaState_MoreRain_AboveOnset()
        {
            // Both above the sea onset, both in full murk: rougher → more rain.
            float lighter = Rain(0.2f, 0.5f);
            float rougher = Rain(0.2f, 0.9f);
            Assert.Greater(rougher, lighter, "A rougher sea (past the onset, in murk) drives more rain.");
        }

        // ---- non-decreasing as visibility FALLS (the murk gate opens) ------------------------------------

        [Test]
        public void RainIntensity_LowerVisibility_MoreRain()
        {
            // Same building sea past the sea onset; the murkier sky opens the gate → more rain. Compare a point
            // inside the gate band against thick murk (clear air would be a trivial 0 == 0).
            float hazy  = Rain(0.55f, 0.7f);
            float murky = Rain(0.1f, 0.7f);
            Assert.Greater(murky, hazy, "Lower visibility (more murk) must unlock more rain for the same chop.");
        }

        [Test]
        public void RainIntensity_MonotonicAsVisibilityFalls()
        {
            float prev = -1f;
            for (int i = 0; i <= 20; i++)
            {
                float vis = 1f - i / 20f;   // visibility falls → murk rises
                float r = Rain(vis, 0.7f);  // sea well above the onset
                Assert.GreaterOrEqual(r + Eps, prev, "Rain must not decrease as the sky goes murkier.");
                prev = r;
            }
        }

        // ---- saturates at a full downpour (both onsets fully crossed) -------------------------------------

        [Test]
        public void RainIntensity_FullMurkFullChop_IsFullDownpour()
        {
            float downpour = Rain(0f, 1f);
            Assert.AreEqual(1f, downpour, Eps, "Full murk + full gale → a full downpour (seaStateWeight 1).");
        }

        [Test]
        public void RainIntensity_ClampsToOne()
        {
            float r = Rain(0f, 1f, baseline: 0.5f, seaStateWeight: 2f);
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
                float r = Rain(vis, sea, baseline: 0.1f, seaStateWeight: 1.2f);
                Assert.IsFalse(float.IsNaN(r), "Rain intensity is never NaN.");
                Assert.GreaterOrEqual(r, -Eps, "Rain intensity is never negative.");
                Assert.LessOrEqual(r, 1f + Eps, "Rain intensity never exceeds 1.");
            }
        }

        // ---- deterministic (same inputs → same output) ---------------------------------------------------

        [Test]
        public void RainIntensity_IsDeterministic()
        {
            float a = Rain(0.35f, 0.62f, baseline: 0.05f, seaStateWeight: 1.1f);
            float b = Rain(0.35f, 0.62f, baseline: 0.05f, seaStateWeight: 1.1f);
            Assert.AreEqual(a, b, 0f, "Same inputs must produce the exact same output (no RNG, rule 5).");
        }
    }
}
