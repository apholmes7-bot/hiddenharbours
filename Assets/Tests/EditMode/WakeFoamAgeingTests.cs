using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// The AGE RAMP's laws (owner ask 2026-08-27: churned water is white only at the churn, then walks
    /// through the sea's blues and fades into the ambient ocean).
    ///
    /// <para>All of this is pure maths, so it is pinned by DERIVED bounds rather than by eyeballed
    /// numbers: the ramp is a convex combination of the sea's own anchors, so "it never leaves the
    /// palette" is a provable containment; Rec.601 luma is linear, so "it never brightens as it ages" is a
    /// provable monotonicity. The render side is the owner's eyeball — no pixel is claimed here.</para>
    /// </summary>
    public class WakeFoamAgeingTests
    {
        /// <summary>The SHIPPED Water.mat anchors, verbatim — so these tests measure the ramp against the
        /// palette the game actually draws with rather than against a convenient invention.</summary>
        static SeaPaletteState ShippedPalette() => new SeaPaletteState(
            deep:    new Color(0.02f, 0.08f, 0.26f, 1f),
            mid:     new Color(0.08f, 0.26f, 0.50f, 1f),
            shallow: new Color(0.16f, 0.50f, 0.72f, 1f),
            foam:    new Color(0.92f, 0.97f, 1.00f, 1f));

        /// <summary>The legacy serialized foam tint on <c>BoatWakeEmitter</c> — the thing strength 0 must
        /// return untouched.</summary>
        static readonly Color LegacyFoam = new Color(0.92f, 0.96f, 1f, 0.7f);

        // ==== the A/B contract ========================================================================

        [Test]
        public void StrengthZero_ReturnsTheLegacyColour_BitExact()
        {
            var ramp = WakeAgeRamp.Off;
            var palette = ShippedPalette();

            for (int i = 0; i <= 20; i++)
            for (int s = 0; s < 8; s++)
            {
                float life = i / 20f;
                float seed = s / 8f;
                Color got = WakeFoamAgeing.Shade(LegacyFoam, life, seed, in ramp, in palette);
                Assert.AreEqual(LegacyFoam.r, got.r,
                    $"Strength 0 must be a BIT-EXACT passthrough (life {life}, seed {seed}). It is the one " +
                    "knob that reverts the whole lane, and 'close' is not a revert.");
                Assert.AreEqual(LegacyFoam.g, got.g);
                Assert.AreEqual(LegacyFoam.b, got.b);
                Assert.AreEqual(LegacyFoam.a, got.a);
            }
        }

        [Test]
        public void ShadeFresh_IsBitExactAtStrengthZero_AndTheFoamAnchorAtFull()
        {
            var palette = ShippedPalette();

            Color off = WakeFoamAgeing.ShadeFresh(LegacyFoam, WakeAgeRamp.Off, in palette);
            Assert.AreEqual(LegacyFoam.r, off.r);
            Assert.AreEqual(LegacyFoam.g, off.g);
            Assert.AreEqual(LegacyFoam.b, off.b);

            var full = WakeAgeRamp.Default;
            full.Strength = 1f;
            Color on = WakeFoamAgeing.ShadeFresh(LegacyFoam, in full, in palette);
            Assert.AreEqual(palette.Foam.r, on.r, 1e-6f,
                "The boat-attached churn sprites are the moment of contact, continuously — they must draw " +
                "at the sea's OWN white, so a preset swap carries them instead of leaving a hard-coded " +
                "near-white sitting in a re-graded sea.");
            Assert.AreEqual(palette.Foam.g, on.g, 1e-6f);
            Assert.AreEqual(palette.Foam.b, on.b, 1e-6f);
            Assert.AreEqual(LegacyFoam.a, on.a, 1e-6f,
                "Alpha is the caller's life fade. Colour must never quietly restate it.");
        }

        // ==== born white, at the SEA's white ==========================================================

        [Test]
        public void BornAtTheChurn_IsTheSeasOwnFoamAnchor()
        {
            var ramp = WakeAgeRamp.Default;
            ramp.AgeScatter = 0f;
            ramp.ShadeJitter = 0f;
            var palette = ShippedPalette();

            Color got = WakeFoamAgeing.Shade(LegacyFoam, 0f, 0.5f, in ramp, in palette);
            Assert.AreEqual(palette.Foam.r, got.r, 1e-6f,
                "Foam is born WHITE at the churn — and the white it is born at is the sea's, never a hex " +
                "invented on a particle component (ADR 0015).");
            Assert.AreEqual(palette.Foam.g, got.g, 1e-6f);
            Assert.AreEqual(palette.Foam.b, got.b, 1e-6f);
        }

        [Test]
        public void ItWalksAllTheWayDown_ToTheSeasMidBlue()
        {
            var ramp = WakeAgeRamp.Default;
            ramp.AgeScatter = 0f;
            ramp.ShadeJitter = 0f;
            var palette = ShippedPalette();

            Color old = WakeFoamAgeing.Shade(LegacyFoam, 1f, 0.5f, in ramp, in palette);
            Assert.AreEqual(palette.Mid.r, old.r, 1e-6f,
                "The end of the ramp is the sea's MID blue. The last leg into the TRUE local sea is the " +
                "alpha fade's job — the ambient ocean at that spot is whatever depth and light make it.");
            Assert.AreEqual(palette.Mid.g, old.g, 1e-6f);
            Assert.AreEqual(palette.Mid.b, old.b, 1e-6f);
        }

        // ==== the defect this lane retires ============================================================

        [Test]
        public void TheDefect_IsGone_FoamIsNotOneColourForItsWholeLife()
        {
            var ramp = WakeAgeRamp.Default;
            ramp.AgeScatter = 0f;
            ramp.ShadeJitter = 0f;
            var palette = ShippedPalette();

            Color young = WakeFoamAgeing.Shade(LegacyFoam, 0f, 0.5f, in ramp, in palette);
            Color old = WakeFoamAgeing.Shade(LegacyFoam, 1f, 0.5f, in ramp, in palette);

            // The whole complaint was "a solid white foam" — one RGB from birth to death, with only alpha
            // moving. A ramp that did not actually move the colour would pass every other test here.
            float travelled = Mathf.Abs(young.r - old.r) + Mathf.Abs(young.g - old.g)
                              + Mathf.Abs(young.b - old.b);
            float paletteSpan = Mathf.Abs(palette.Foam.r - palette.Mid.r)
                                + Mathf.Abs(palette.Foam.g - palette.Mid.g)
                                + Mathf.Abs(palette.Foam.b - palette.Mid.b);
            Assert.AreEqual(paletteSpan, travelled, 1e-5f,
                "Fresh churn and dead churn must be the full width of the sea's ramp apart. If this " +
                "collapses, the wake is back to being one white that only fades — the exact defect the " +
                "owner reported on 2026-08-27.");
        }

        [Test]
        public void Luminance_NeverRises_AsFoamAges()
        {
            var ramp = WakeAgeRamp.Default;
            ramp.ShadeJitter = 0f;          // jitter is per-particle, not per-age; it cannot be monotone
            ramp.AgeScatter = 0f;           // scatter shifts the curve, it does not shape it
            var palette = ShippedPalette();

            // The claim is provable, not empirical: the ramp is a convex combination walking
            // foam -> shallow -> mid, and Rec.601 luma is linear, so luma is piecewise-linear in age and
            // non-increasing exactly when the anchors descend in luma. Assert the premise first.
            Assert.Greater(WakeFoamAgeing.Luminance(palette.Foam), WakeFoamAgeing.Luminance(palette.Shallow),
                "The shipped palette's anchors must descend in luma for the monotonicity below to hold.");
            Assert.Greater(WakeFoamAgeing.Luminance(palette.Shallow), WakeFoamAgeing.Luminance(palette.Mid));

            float prev = float.MaxValue;
            for (int i = 0; i <= 200; i++)
            {
                float life = i / 200f;
                float luma = WakeFoamAgeing.Luminance(
                    WakeFoamAgeing.Shade(LegacyFoam, life, 0.5f, in ramp, in palette));
                Assert.LessOrEqual(luma, prev + 1e-6f,
                    $"Foam brightened as it aged at life {life}. Water that has been churned does not get " +
                    "whiter again — a non-monotone ramp reads as the wake flickering.");
                prev = luma;
            }
        }

        // ==== ADR 0015: the blues come from the water's own bounded ramp ==============================

        [Test]
        public void EveryShade_StaysInsideTheSeasOwnPalette()
        {
            var ramp = WakeAgeRamp.Default;
            var palette = ShippedPalette();

            // The DERIVED bound, not a tuned tolerance: every returned colour is a convex combination of
            // the three anchors (so per channel it lies between the smallest and largest anchor value),
            // scaled by at most (1 +/- ShadeJitter), then lerped toward the legacy colour. Include the
            // legacy colour in the interval because Strength < 1 blends toward it.
            float j = ramp.ShadeJitter;
            for (int c = 0; c < 3; c++)
            {
                float lo = Mathf.Min(Mathf.Min(Ch(palette.Foam, c), Ch(palette.Shallow, c)), Ch(palette.Mid, c));
                float hi = Mathf.Max(Mathf.Max(Ch(palette.Foam, c), Ch(palette.Shallow, c)), Ch(palette.Mid, c));
                lo = Mathf.Min(lo * (1f - j), Ch(LegacyFoam, c));
                hi = Mathf.Max(hi * (1f + j), Ch(LegacyFoam, c));

                for (int i = 0; i <= 40; i++)
                for (int s = 0; s < 32; s++)
                {
                    Color got = WakeFoamAgeing.Shade(LegacyFoam, i / 40f, s / 32f, in ramp, in palette);
                    float v = Ch(got, c);
                    Assert.GreaterOrEqual(v, lo - 1e-5f,
                        "The wake left the sea's palette. ADR 0015's whole point is that the sea's output " +
                        "stays inside an art-directed palette — a wake that invents its own colour breaks " +
                        "the guard-rail from outside the shader, where nothing would catch it.");
                    Assert.LessOrEqual(v, hi + 1e-5f);
                }
            }
        }

        static float Ch(Color c, int i) => i == 0 ? c.r : i == 1 ? c.g : c.b;

        // ==== the cure for "organized and shader-like" ================================================

        [Test]
        public void TheScatter_PutsNeighbouringPuffsAtDifferentAges()
        {
            var ramp = WakeAgeRamp.Default;
            var palette = ShippedPalette();

            // One churn, one instant: 64 puffs all at the same life. If they all draw the same colour the
            // churn is a sheet, which is the "very organized and shader-like" the owner reported.
            const int n = 64;
            float lo = float.MaxValue, hi = float.MinValue;
            for (int s = 0; s < n; s++)
            {
                float age = WakeFoamAgeing.Age01(0.5f, s / (float)n, in ramp);
                lo = Mathf.Min(lo, age);
                hi = Mathf.Max(hi, age);
            }

            // DERIVED expectation: the offset is uniform on [-scatter, +scatter] in LIFE, and at life 0.5
            // the knot curve is locally linear with slope 0.5/(BlueReach - WhiteHold) at worst and
            // 0.5/(DeepReach - BlueReach) at best. Take the shallower of the two as the floor, and ask for
            // half the theoretical spread so the test measures "visibly many ages" rather than the exact
            // draw of 64 hashes.
            float slope = 0.5f / Mathf.Max(ramp.BlueReach - ramp.WhiteHold, ramp.DeepReach - ramp.BlueReach);
            float expected = 2f * ramp.AgeScatter * slope;
            Assert.Greater(hi - lo, expected * 0.5f,
                "A churn must hold many ages at once. With every puff at the same point on the ramp the " +
                "wake is one blue sheet instead of many things — the ramp alone does not cure " +
                "'organized', the scatter does.");
        }

        [Test]
        public void ScatterZero_IsTheControl_EveryPuffTheSameAge()
        {
            var ramp = WakeAgeRamp.Default;
            ramp.AgeScatter = 0f;

            float first = WakeFoamAgeing.Age01(0.5f, 0f, in ramp);
            for (int s = 1; s < 32; s++)
                Assert.AreEqual(first, WakeFoamAgeing.Age01(0.5f, s / 32f, in ramp), 1e-7f,
                    "Scatter 0 must restore the exact shared curve — the control the test above measures " +
                    "the spread against.");
        }

        // ==== the knot curve (the half the shader transcribes) ========================================

        [Test]
        public void Knots_HitTheThreeStops_AndAreMonotone()
        {
            var r = WakeAgeRamp.Default;

            Assert.AreEqual(0f, WakeFoamAgeing.Knots(0f, r.WhiteHold, r.BlueReach, r.DeepReach), 1e-6f);
            Assert.AreEqual(0f, WakeFoamAgeing.Knots(r.WhiteHold, r.WhiteHold, r.BlueReach, r.DeepReach), 1e-6f,
                "White must be held all the way to the WhiteHold knot — that hold IS 'white only at the " +
                "moment of churn'.");
            Assert.AreEqual(0.5f, WakeFoamAgeing.Knots(r.BlueReach, r.WhiteHold, r.BlueReach, r.DeepReach), 1e-5f,
                "The BlueReach knot is where the foam has become the sea's shallow blue.");
            Assert.AreEqual(1f, WakeFoamAgeing.Knots(r.DeepReach, r.WhiteHold, r.BlueReach, r.DeepReach), 1e-5f);
            Assert.AreEqual(1f, WakeFoamAgeing.Knots(1f, r.WhiteHold, r.BlueReach, r.DeepReach), 1e-6f);

            float prev = -1f;
            for (int i = 0; i <= 500; i++)
            {
                float v = WakeFoamAgeing.Knots(i / 500f, r.WhiteHold, r.BlueReach, r.DeepReach);
                Assert.GreaterOrEqual(v, prev - 1e-6f, "The age curve must never run backwards.");
                prev = v;
            }
        }

        [Test]
        public void Knots_SurviveAMisTunedConfig()
        {
            // Inverted knots, out-of-range knots, all-equal knots: a config the owner can produce by
            // dragging sliders. None of them may invert the ramp, divide by zero, or return NaN.
            float[][] bad =
            {
                new[] { 0.9f, 0.2f, 0.1f },     // fully inverted
                new[] { -1f, 2f, -5f },         // out of range
                new[] { 0.5f, 0.5f, 0.5f },     // degenerate
                new[] { 1f, 1f, 1f },           // all at the top
            };

            foreach (float[] k in bad)
            {
                float prev = -1f;
                for (int i = 0; i <= 100; i++)
                {
                    float v = WakeFoamAgeing.Knots(i / 100f, k[0], k[1], k[2]);
                    Assert.IsFalse(float.IsNaN(v) || float.IsInfinity(v),
                        $"Knots({i / 100f}, {k[0]}, {k[1]}, {k[2]}) was not a number.");
                    Assert.GreaterOrEqual(v, 0f);
                    Assert.LessOrEqual(v, 1f);
                    Assert.GreaterOrEqual(v, prev - 1e-6f,
                        "A mis-tuned config must degrade the look, never invert the ramp.");
                    prev = v;
                }
            }
        }

        // ==== determinism (rule 5) ====================================================================

        [Test]
        public void Decorrelate_IsStable_AndItsSaltsDoNotCorrelate()
        {
            for (int s = 0; s < 16; s++)
            {
                float seed = s / 16f;
                Assert.AreEqual(WakeFoamAgeing.Decorrelate(seed, 0x51u),
                                WakeFoamAgeing.Decorrelate(seed, 0x51u),
                    "The same seed must always give the same variation — the wake is deterministic " +
                    "(rule 5), not System.Random.");
            }

            // The two salts the ramp uses must not walk together, or a puff's shade jitter would just
            // restate its age offset and the variance would be half what it looks like.
            int agree = 0;
            const int n = 256;
            for (int s = 0; s < n; s++)
            {
                float a = WakeFoamAgeing.Decorrelate(s / (float)n, 0x51u);
                float b = WakeFoamAgeing.Decorrelate(s / (float)n, 0xA7u);
                if (Mathf.Abs(a - b) < 0.02f) agree++;
            }
            // Two independent uniforms land within 0.02 of each other about 4% of the time; 15% is a
            // generous ceiling that still fails hard if the salts collapse onto one stream.
            Assert.Less(agree, n * 0.15f,
                "The ramp offset and the shade jitter came out correlated, so a puff's two 'independent' " +
                "variations are one variation wearing two names.");
        }

        [Test]
        public void Decorrelate_SpansItsRange()
        {
            float lo = float.MaxValue, hi = float.MinValue;
            for (int s = 0; s < 512; s++)
            {
                float v = WakeFoamAgeing.Decorrelate(s / 512f, 0x51u);
                lo = Mathf.Min(lo, v);
                hi = Mathf.Max(hi, v);
                Assert.GreaterOrEqual(v, 0f);
                Assert.Less(v, 1f);
            }
            Assert.Less(lo, 0.05f, "The hash must reach the bottom of its range or the scatter is one-sided.");
            Assert.Greater(hi, 0.95f, "The hash must reach the top of its range.");
        }
    }
}
