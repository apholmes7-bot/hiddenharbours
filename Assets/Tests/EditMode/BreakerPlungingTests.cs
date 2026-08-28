using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>The plunging band (ADR 0040, PR 2 drop 2) — the lip, the barrel and the pocket appear ONLY
    /// where the bathymetry earns them.</b>
    ///
    /// <para>That sentence is the load-bearing claim of the whole arc. Every knob in the shader's
    /// plunging block multiplies by <see cref="BreakerMath.PlungingWeight01"/>, so if that weight can be
    /// coaxed above zero on a gentle shoal then barrels become decoration and the claim is false. These
    /// tests exist to make that impossible to do by accident.</para>
    ///
    /// <para>The weight is a SOFTENED <see cref="BreakerMath.ClassFor"/> — the same Battjes thresholds,
    /// feathered so the anatomy fades instead of snapping along a contour as the sampled bed gradient
    /// crosses a threshold (the seabed is an 8-bit texture; its gradient is a quantized quantity). The
    /// softening must not become a widening, and the difference between those two is exactly what is
    /// pinned below.</para>
    /// </summary>
    public class BreakerPlungingTests
    {
        private const float G = 9.81f;

        // H0 = 1 m over L0 = 18 m, so xi = tanB / 0.2357 — the same working sea the other files use.
        private const float H0 = 1f;
        private const float L0 = 18f;

        private static float XiFor(float bedSlope) => BreakerMath.Iribarren(bedSlope, H0, L0);

        // =========================================================================================
        //  The weight is the classification, not a relaxation of it
        // =========================================================================================

        [Test]
        public void AGentleShoal_EarnsNoLipAtAll()
        {
            // ⭐ The claim. Every real sandy shore in this game is in this range.
            var settings = BreakerSettings.Default;
            foreach (var (slope, place) in new[]
            {
                (0.005f, "a tidal flat"), (0.02f, "1:50 sand"), (0.04f, "1:25 shoal"), (0.08f, "1:12 sand"),
            })
            {
                float weight = BreakerMath.PlungingWeight01(XiFor(slope), in settings);
                Assert.AreEqual(0f, weight, 1e-4f,
                    $"{place} (tanB {slope}) must earn no plunging anatomy whatsoever — it spills");
                Assert.AreEqual(BreakerClass.Spilling, BreakerMath.ClassFor(XiFor(slope), in settings),
                    $"{place} must also CLASSIFY as spilling — the weight and the table must agree");
            }
        }

        [Test]
        public void AReefEdge_EarnsAFullLip()
        {
            var settings = BreakerSettings.Default;
            foreach (var (slope, place) in new[] { (0.25f, "a shingle bank"), (0.4f, "a reef edge") })
            {
                Assert.AreEqual(1f, BreakerMath.PlungingWeight01(XiFor(slope), in settings), 1e-3f,
                    $"{place} (tanB {slope}) must plunge fully");
                Assert.AreEqual(BreakerClass.Plunging, BreakerMath.ClassFor(XiFor(slope), in settings),
                    $"{place} must classify as plunging too");
            }
        }

        [Test]
        public void AQuayWall_EarnsNoLipEither_BecauseItSurgesRatherThanBreaks()
        {
            // The upper edge matters as much as the lower one: a near-vertical face surges up and back
            // almost without breaking, and drawing a barrel against a harbour wall would be as wrong as
            // drawing one on a mudflat.
            var settings = BreakerSettings.Default;
            foreach (float slope in new[] { 1.2f, 2f, 5f })
            {
                Assert.AreEqual(0f, BreakerMath.PlungingWeight01(XiFor(slope), in settings), 1e-4f,
                    $"a wall (tanB {slope}) must earn no barrel — it surges");
            }
        }

        [Test]
        public void TheWeight_IsZeroEverywhereTheTableSaysSpillingOrSurging()
        {
            // The general form of the three tests above, swept rather than sampled: wherever the hard
            // classification is NOT plunging, the soft weight must be 0 — except inside the feather
            // right at the boundary, which is the whole point of the softening and is bounded here.
            var settings = BreakerSettings.Default;
            float band = settings.PlungingLimit - settings.SpillingLimit;
            float half = band * 0.05f;

            for (float xi = 0f; xi < 8f; xi += 0.005f)
            {
                float weight = BreakerMath.PlungingWeight01(xi, in settings);
                BreakerClass cls = BreakerMath.ClassFor(xi, in settings);
                if (cls == BreakerClass.Plunging) continue;

                // The feather STRADDLES both thresholds, so a sliver either side of each carries partial
                // weight by design. Everywhere else outside the band must be exactly zero.
                bool insideAFeather = (xi > settings.SpillingLimit - half && xi < settings.SpillingLimit + half)
                                   || (xi > settings.PlungingLimit - half && xi < settings.PlungingLimit + half);
                if (insideAFeather) continue;

                Assert.AreEqual(0f, weight, 1e-4f,
                    $"xi {xi:0.000} classifies as {cls} but carries plunging weight {weight:0.000}");
            }
        }

        [Test]
        public void TheFeather_IsNarrowEnoughToStillBeBattjes()
        {
            // The softening is allowed to blur the boundary. It is NOT allowed to move it. Measured: the
            // weight passes 0.5 within a few hundredths of the published threshold.
            var settings = BreakerSettings.Default;
            float crossing = float.NaN;
            for (float xi = 0f; xi < 8f; xi += 0.001f)
            {
                if (BreakerMath.PlungingWeight01(xi, in settings) >= 0.5f) { crossing = xi; break; }
            }

            Assert.IsFalse(float.IsNaN(crossing), "the weight must reach a half somewhere");
            // ⭐ THE REGRESSION THIS FILE EXISTS FOR. The first implementation feathered UPWARD from the
            // threshold and put this crossing at ξ 0.641 — a 28 % shift of the spilling/plunging boundary,
            // which would have suppressed barrels on slopes that had genuinely earned them, silently and
            // for a reason no reviewer would have found by reading. A tight tolerance here is the whole
            // difference between softening a boundary and moving one.
            Assert.AreEqual(settings.SpillingLimit, crossing, 0.02f,
                $"the half-weight crossing is at ξ {crossing:0.000} but Battjes' threshold is " +
                $"{settings.SpillingLimit} — the feather has MOVED the classification, not blurred it");
        }

        [Test]
        public void TheWeightIsSmooth_SoTheAnatomyCannotPopAlongAContour()
        {
            // The seabed is an 8-bit texture and its gradient is a quantized quantity, so a hard
            // classification would switch the lip and the barrel on and off along a contour as the
            // sampled slope wobbled across a threshold.
            var settings = BreakerSettings.Default;
            float previous = BreakerMath.PlungingWeight01(0f, in settings);
            for (float xi = 0f; xi < 8f; xi += 0.01f)
            {
                float now = BreakerMath.PlungingWeight01(xi, in settings);
                Assert.LessOrEqual(Mathf.Abs(now - previous), 0.1f,
                    $"the plunging weight jumped at xi {xi:0.00} — the anatomy would pop there");
                previous = now;
            }
        }

        // =========================================================================================
        //  The lip throw
        // =========================================================================================

        [Test]
        public void ASpillingBreakerThrowsNothing()
        {
            Assert.AreEqual(0f, BreakerMath.LipThrowMeters(1.5f, 0f, 1.1f), 1e-6f,
                "no plunging weight means no thrown lip — that is what makes a spilling break spill");
        }

        [Test]
        public void TheThrowScalesWithTheStandingWave_NotTheDeepWaterOne()
        {
            // A broken wave is only as tall as the water it is running over, so the throw shrinks as the
            // bore runs up the beach. Feeding the deep-water height instead would have a two-metre swell
            // throwing a two-metre lip in ankle-deep water.
            float deep = BreakerMath.LipThrowMeters(2f, 1f, 1.1f);
            float shallow = BreakerMath.LipThrowMeters(0.4f, 1f, 1.1f);
            Assert.Greater(deep, shallow, "a taller standing wave throws further");
            Assert.AreEqual(2f * 1.1f, deep, 1e-4f, "and the scaling is the stated metres-per-metre");
        }

        [Test]
        public void TheThrow_IsNeverNegative_OnAnyHostileInput()
        {
            foreach (float h in new[] { -5f, 0f, 3f })
            foreach (float w in new[] { -1f, 0f, 0.5f, 2f })
            foreach (float k in new[] { -2f, 0f, 1.1f })
            {
                float t = BreakerMath.LipThrowMeters(h, w, k);
                Assert.GreaterOrEqual(t, 0f, "a lip cannot be thrown backwards");
                Assert.IsFalse(float.IsNaN(t), "and never NaN");
            }
        }

        // =========================================================================================
        //  Determinism
        // =========================================================================================

        [Test]
        public void TheWeightAndTheThrow_AreDeterministic()
        {
            var settings = BreakerSettings.Default;
            for (float xi = 0f; xi < 8f; xi += 0.13f)
            {
                Assert.AreEqual(BreakerMath.PlungingWeight01(xi, in settings),
                                BreakerMath.PlungingWeight01(xi, in settings), "bit-stable");
                Assert.AreEqual(BreakerMath.LipThrowMeters(xi, 0.5f, 1.1f),
                                BreakerMath.LipThrowMeters(xi, 0.5f, 1.1f), "bit-stable");
            }
        }

        [Test]
        public void AStaleSettingsStruct_EarnsNoAnatomy()
        {
            // A GameConfig serialized before today reads every threshold as 0. That must mean "no
            // barrels", not "barrels everywhere" — the safe-stale direction.
            var zeroed = default(BreakerSettings);
            for (float xi = 0f; xi < 8f; xi += 0.25f)
            {
                float weight = BreakerMath.PlungingWeight01(xi, in zeroed);
                Assert.That(weight, Is.InRange(0f, 1f), "a zeroed struct must still produce a legal weight");
            }
            // With every limit at 0 the band is degenerate; what matters is that nothing blows up and
            // that the contour above it reports Breaks = false, which is what actually silences the draw.
            Assert.IsFalse(BreakerMath.ContourFor(new WaveTrain(Vector2.right, 18f, 0.5f, 0f, G),
                                                  1f, in zeroed).Breaks,
                           "and the contour is inert, which is what really turns the anatomy off");
        }
    }
}
