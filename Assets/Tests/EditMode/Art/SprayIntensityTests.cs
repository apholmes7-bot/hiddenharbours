using NUnit.Framework;
using HiddenHarbours.Art;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// Headless guard for <see cref="AmbientParticleMath.SprayIntensity"/> — the DERIVED, art-only signal the
    /// wind-blown spray emitter scales its spawn-rate + opacity by: a scene-level "is the sea whitecapping?"
    /// gate keyed off the ONE sea-state axis (there is no whitecap/spray axis in the sim). A glassy or gently
    /// rippled sea throws NO spray; past a whitecap threshold it comes on STEEPLY into a gale. No Unity scene
    /// needed — pure, deterministic feel-math, so a regression (or a NaN at an edge) is caught in CI without
    /// opening Unity. Mirrors <see cref="RainIntensityTests"/>.
    /// </summary>
    public class SprayIntensityTests
    {
        private const float Eps = 1e-4f;

        // ---- ~0 below the whitecap threshold (a calm / lightly-rippled sea throws nothing) ----------------

        [Test]
        public void SprayIntensity_GlassySea_IsBaseline()
        {
            // Sea 0 (glass): only the baseline. Default baseline 0 → no spray at all.
            float s = AmbientParticleMath.SprayIntensity(0f, baseline: 0f, seaStateWeight: 1f, threshold: 0.55f);
            Assert.AreEqual(0f, s, Eps, "A glassy sea with baseline 0 → no spray (feature OFF).");
        }

        [Test]
        public void SprayIntensity_BelowThreshold_IsBaseline()
        {
            // A breezy-but-unbroken sea just under the whitecap onset still throws nothing above the baseline.
            float s = AmbientParticleMath.SprayIntensity(0.5f, baseline: 0f, seaStateWeight: 1f, threshold: 0.55f);
            Assert.AreEqual(0f, s, Eps, "Below the whitecap threshold the onset is 0 → no spray beyond the baseline.");
        }

        [Test]
        public void SprayIntensity_AtThreshold_IsBaseline()
        {
            // Exactly at the onset the smootherstep foot is 0, so we are still on the baseline (no spray yet).
            float s = AmbientParticleMath.SprayIntensity(0.55f, baseline: 0f, seaStateWeight: 1f, threshold: 0.55f);
            Assert.AreEqual(0f, s, Eps, "At the threshold the onset is exactly 0 (the flat foot of the ramp).");
        }

        [Test]
        public void SprayIntensity_BelowThreshold_HonoursBaseline()
        {
            float s = AmbientParticleMath.SprayIntensity(0.2f, baseline: 0.15f, seaStateWeight: 1f, threshold: 0.55f);
            Assert.AreEqual(0.15f, s, Eps, "With a baseline dialled in, the calm-sea floor is exactly that baseline.");
        }

        // ---- rises PAST the threshold ---------------------------------------------------------------------

        [Test]
        public void SprayIntensity_PastThreshold_ThrowsSpray()
        {
            float below = AmbientParticleMath.SprayIntensity(0.5f, 0f, 1f, 0.55f);
            float above = AmbientParticleMath.SprayIntensity(0.8f, 0f, 1f, 0.55f);
            Assert.AreEqual(0f, below, Eps, "Below the onset → nothing.");
            Assert.Greater(above, 0.05f, "Well past the onset the sea whitecaps and throws real spray.");
        }

        [Test]
        public void SprayIntensity_FullGale_Saturates()
        {
            // A full gale (sea 1) past the onset drives the onset to 1, so spray = baseline + seaStateWeight.
            float s = AmbientParticleMath.SprayIntensity(1f, 0f, 1f, 0.55f);
            Assert.AreEqual(1f, s, Eps, "A full gale flings the maximum spray (onset saturates at 1).");
        }

        // ---- monotonic non-decreasing in sea-state (never LESS spray as the sea builds) -------------------

        [Test]
        public void SprayIntensity_MonotonicInSeaState()
        {
            float prev = -1f;
            for (int i = 0; i <= 40; i++)
            {
                float sea = i / 40f;
                float s = AmbientParticleMath.SprayIntensity(sea, 0f, 1f, 0.55f);
                Assert.GreaterOrEqual(s + Eps, prev, "Spray must not decrease as the sea builds.");
                prev = s;
            }
        }

        [Test]
        public void SprayIntensity_MonotonicAboveThreshold_StrictlyRising()
        {
            // Above the onset the ramp is strictly increasing (the whole point — spray comes on with the sea).
            float prev = -1f;
            for (int i = 0; i <= 20; i++)
            {
                float sea = 0.55f + (1f - 0.55f) * (i / 20f);   // walk from the threshold to a full gale
                float s = AmbientParticleMath.SprayIntensity(sea, 0f, 1f, 0.55f);
                Assert.GreaterOrEqual(s + Eps, prev, "Above the onset, more sea = at least as much spray.");
                prev = s;
            }
            // And the top of the band is meaningfully more than the foot.
            float foot = AmbientParticleMath.SprayIntensity(0.6f, 0f, 1f, 0.55f);
            float crest = AmbientParticleMath.SprayIntensity(1f, 0f, 1f, 0.55f);
            Assert.Greater(crest, foot, "A gale flings much more spray than the first breaking crests.");
        }

        // ---- the threshold is a real gate: raising it delays the onset ------------------------------------

        [Test]
        public void SprayIntensity_HigherThreshold_DelaysOnset()
        {
            // The same building sea throws LESS (or no) spray when the whitecap onset is set later.
            float low = AmbientParticleMath.SprayIntensity(0.6f, 0f, 1f, threshold: 0.4f);
            float high = AmbientParticleMath.SprayIntensity(0.6f, 0f, 1f, threshold: 0.7f);
            Assert.Greater(low, high, "A later whitecap threshold means the same sea throws less spray.");
            Assert.AreEqual(0f, high, Eps, "With the onset above the current sea-state there is no spray yet.");
        }

        [Test]
        public void SprayIntensity_ThresholdAtOne_NeverWhitecaps()
        {
            // Degenerate: a threshold pinned at 1 leaves no band, so even a full gale throws only the baseline.
            float s = AmbientParticleMath.SprayIntensity(1f, 0f, 1f, threshold: 1f);
            Assert.AreEqual(0f, s, Eps, "Threshold 1 → no band → never whitecaps (onset is always 0).");
        }

        // ---- clamped to [0,1] -----------------------------------------------------------------------------

        [Test]
        public void SprayIntensity_ClampsToOne()
        {
            float s = AmbientParticleMath.SprayIntensity(1f, 0.5f, 2f, 0.55f);
            Assert.AreEqual(1f, s, Eps, "Intensity saturates at 1.");
        }

        [Test]
        public void SprayIntensity_NeverNegative_AndNeverNaN_AcrossTheGrid()
        {
            for (int si = 0; si <= 20; si++)
            for (int ti = 0; ti <= 10; ti++)
            {
                float sea = si / 20f;
                float th = ti / 10f;
                float s = AmbientParticleMath.SprayIntensity(sea, 0.1f, 1.2f, th);
                Assert.IsFalse(float.IsNaN(s), "Spray intensity is never NaN.");
                Assert.GreaterOrEqual(s, -Eps, "Spray intensity is never negative.");
                Assert.LessOrEqual(s, 1f + Eps, "Spray intensity never exceeds 1.");
            }
        }

        [Test]
        public void SprayIntensity_ClampsNegativeInputs()
        {
            // Out-of-range inputs (negative sea-state / threshold) are clamped, not propagated as garbage.
            float s = AmbientParticleMath.SprayIntensity(-0.5f, 0f, 1f, -0.2f);
            Assert.GreaterOrEqual(s, -Eps, "A clamped (<0) sea-state must not produce a negative intensity.");
            Assert.LessOrEqual(s, 1f + Eps, "A clamped intensity never exceeds 1.");
        }

        // ---- deterministic (same inputs → same output) ---------------------------------------------------

        [Test]
        public void SprayIntensity_IsDeterministic()
        {
            float a = AmbientParticleMath.SprayIntensity(0.72f, 0.05f, 1.1f, 0.55f);
            float b = AmbientParticleMath.SprayIntensity(0.72f, 0.05f, 1.1f, 0.55f);
            Assert.AreEqual(a, b, 0f, "Same inputs must produce the exact same output (no RNG, rule 5).");
        }
    }
}
