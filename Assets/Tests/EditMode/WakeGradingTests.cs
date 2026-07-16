using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Boats;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// The PURE-logic guard for the GRADED wake selection (the owner's brief: "bigger/heavier hulls and higher
    /// speed → a bigger wake"). Every claim is exercised on the side-effect-free <see cref="WakeGrading"/> math
    /// headless — no Unity scene, no sprites:
    /// <list type="bullet">
    /// <item><description><b>Monotone in every input</b> — more size, more weight, more speed never SHRINKS the
    /// blended magnitude or the chosen tier.</description></item>
    /// <item><description><b>Tier thresholds</b> — the magnitude→tier mapping steps at the tunable thresholds,
    /// stays in [0,3], and is monotone even if the owner mis-orders the thresholds.</description></item>
    /// <item><description><b>Sensible defaults</b> — the current small hulls (dory ≈ 4.5 m / 400 kg) grade Small
    /// at a crawl → Medium underway, and a heavy hull driven hard reaches Large/Huge; a heavy-slow hull and a
    /// light-fast hull resolve sensibly.</description></item>
    /// <item><description><b>Plume/foam ramps</b> — the plume scale, speed onset and foam-growth factor are
    /// bounded, monotone, and neutral at their off settings.</description></item>
    /// </list>
    /// </summary>
    public class WakeGradingTests
    {
        private static WakeGradeConfig Cfg() => WakeGradeConfig.Default;

        // ==== Normalize01 ================================================================================

        [Test]
        public void Normalize01_ClampsToUnitRange()
        {
            Assert.AreEqual(0f, WakeGrading.Normalize01(-5f, 0f, 10f), 1e-5f, "below min clamps to 0");
            Assert.AreEqual(1f, WakeGrading.Normalize01(50f, 0f, 10f), 1e-5f, "above max clamps to 1");
            Assert.AreEqual(0.5f, WakeGrading.Normalize01(5f, 0f, 10f), 1e-5f, "mid maps linearly");
        }

        [Test]
        public void Normalize01_DegenerateRange_IsAStepNotADivideByZero()
        {
            Assert.AreEqual(0f, WakeGrading.Normalize01(5f, 10f, 10f), 1e-5f, "value ≤ min → 0");
            Assert.AreEqual(1f, WakeGrading.Normalize01(11f, 10f, 10f), 1e-5f, "value > min → 1 (no NaN/Inf)");
        }

        // ==== Magnitude01 monotonicity (the heart of the grade) =========================================

        [Test]
        public void Magnitude01_RisesWithSize()
        {
            var c = Cfg();
            float small = WakeGrading.Magnitude01(4.5f, 400f, 2f, c);
            float big   = WakeGrading.Magnitude01(16f, 400f, 2f, c);
            Assert.Greater(big, small, "a longer hull grades a bigger wake, all else equal");
        }

        [Test]
        public void Magnitude01_RisesWithWeight()
        {
            var c = Cfg();
            float light = WakeGrading.Magnitude01(6f, 500f, 2f, c);
            float heavy = WakeGrading.Magnitude01(6f, 6000f, 2f, c);
            Assert.Greater(heavy, light, "a heavier hull grades a bigger wake, all else equal");
        }

        [Test]
        public void Magnitude01_RisesWithSpeed()
        {
            var c = Cfg();
            float slow = WakeGrading.Magnitude01(6f, 500f, 1f, c);
            float fast = WakeGrading.Magnitude01(6f, 500f, 4.5f, c);
            Assert.Greater(fast, slow, "the SAME boat throws a bigger wake pushed hard (dynamic speed term)");
        }

        [Test]
        public void Magnitude01_IsMonotonicNonDecreasingInEachInput_OverAGrid()
        {
            var c = Cfg();
            // Sweep each axis holding the others; the magnitude must never decrease.
            float prev = -1f;
            for (float len = 3f; len <= 24f; len += 1f)
            {
                float m = WakeGrading.Magnitude01(len, 2000f, 2.5f, c);
                Assert.GreaterOrEqual(m, prev - 1e-6f, "magnitude never falls as length rises");
                prev = m;
            }
            prev = -1f;
            for (float kg = 200f; kg <= 9000f; kg += 300f)
            {
                float m = WakeGrading.Magnitude01(9f, kg, 2.5f, c);
                Assert.GreaterOrEqual(m, prev - 1e-6f, "magnitude never falls as mass rises");
                prev = m;
            }
            prev = -1f;
            for (float sp = 0f; sp <= 6f; sp += 0.25f)
            {
                float m = WakeGrading.Magnitude01(9f, 2000f, sp, c);
                Assert.GreaterOrEqual(m, prev - 1e-6f, "magnitude never falls as speed rises");
                prev = m;
            }
        }

        [Test]
        public void Magnitude01_StaysInUnitRange()
        {
            var c = Cfg();
            Assert.AreEqual(0f, WakeGrading.Magnitude01(0f, 0f, 0f, c), 1e-5f, "nothing pushing → 0");
            Assert.AreEqual(1f, WakeGrading.Magnitude01(1000f, 1e6f, 1000f, c), 1e-5f, "everything maxed → 1");
        }

        [Test]
        public void Magnitude01_AllZeroWeights_IsZero_NoNaN()
        {
            var c = Cfg();
            c.WeightSize = 0f; c.WeightMass = 0f; c.WeightSpeed = 0f;
            Assert.AreEqual(0f, WakeGrading.Magnitude01(20f, 8000f, 5f, c), 1e-6f,
                "degenerate all-zero weights collapse to 0, never a divide-by-zero");
        }

        [Test]
        public void Magnitude01_WeightsAreRelative_NormalizedInternally()
        {
            // Doubling every weight is the same balance → the same magnitude (weights are normalized to sum 1).
            var a = Cfg();
            var b = a;
            b.WeightSize *= 2f; b.WeightMass *= 2f; b.WeightSpeed *= 2f;
            float ma = WakeGrading.Magnitude01(10f, 3000f, 3f, a);
            float mb = WakeGrading.Magnitude01(10f, 3000f, 3f, b);
            Assert.AreEqual(ma, mb, 1e-5f, "only the RELATIVE weights matter — the total is normalized out");
        }

        // ==== TierIndex ==================================================================================

        [Test]
        public void TierIndex_StepsAtThresholds_AndStaysInRange()
        {
            var c = Cfg();
            Assert.AreEqual(0, WakeGrading.TierIndex(0f, c), "min magnitude → Small");
            Assert.AreEqual(0, WakeGrading.TierIndex(c.Threshold1 - 0.01f, c), "just below t1 → Small");
            Assert.AreEqual(1, WakeGrading.TierIndex(c.Threshold1, c), "at t1 → Medium (>= is inclusive)");
            Assert.AreEqual(2, WakeGrading.TierIndex(c.Threshold2, c), "at t2 → Large");
            Assert.AreEqual(3, WakeGrading.TierIndex(c.Threshold3, c), "at t3 → Huge");
            Assert.AreEqual(3, WakeGrading.TierIndex(1f, c), "max magnitude → Huge (clamped)");
        }

        [Test]
        public void TierIndex_IsMonotonicNonDecreasingInMagnitude()
        {
            var c = Cfg();
            int prev = -1;
            for (float m = 0f; m <= 1f + 1e-4f; m += 0.02f)
            {
                int tier = WakeGrading.TierIndex(m, c);
                Assert.GreaterOrEqual(tier, prev, "the tier never drops as the magnitude rises");
                Assert.That(tier, Is.InRange(0, WakeGrading.TierCount - 1), "tier stays a valid index");
                prev = tier;
            }
        }

        [Test]
        public void TierIndex_RobustToMisorderedThresholds_StillMonotonic()
        {
            // An owner fat-fingers the thresholds out of order — the mapping must still never invert.
            var c = Cfg();
            c.Threshold1 = 0.7f; c.Threshold2 = 0.2f; c.Threshold3 = 0.5f;   // deliberately scrambled
            int prev = -1;
            for (float m = 0f; m <= 1f + 1e-4f; m += 0.02f)
            {
                int tier = WakeGrading.TierIndex(m, c);
                Assert.GreaterOrEqual(tier, prev, "scrambled thresholds are sorted defensively → still monotone");
                Assert.That(tier, Is.InRange(0, WakeGrading.TierCount - 1));
                prev = tier;
            }
        }

        // ==== SelectTier monotonicity ===================================================================

        [Test]
        public void SelectTier_NeverDecreasesWhenAnyInputRises()
        {
            var c = Cfg();
            // Baseline small/light/slow.
            int baseTier = WakeGrading.SelectTier(4.5f, 400f, 1f, c);
            Assert.LessOrEqual(baseTier, WakeGrading.SelectTier(4.5f, 400f, 4.5f, c), "more speed never lowers tier");
            Assert.LessOrEqual(baseTier, WakeGrading.SelectTier(16f, 400f, 1f, c), "more size never lowers tier");
            Assert.LessOrEqual(baseTier, WakeGrading.SelectTier(4.5f, 6000f, 1f, c), "more weight never lowers tier");
        }

        // ==== Sensible defaults (drives off LengthMeters/MassKg — no per-hull hard-code) =================

        [Test]
        public void Defaults_DoryCrawl_IsSmall_DoryUnderway_IsAtLeastMedium()
        {
            var c = Cfg();
            // The dory hull (BoatHullDef defaults): 4.5 m, 400 kg.
            int crawl = WakeGrading.SelectTier(4.5f, 400f, 0.5f, c);   // barely making way
            Assert.AreEqual(0, crawl, "a dory at a crawl leaves the Small wake");

            int underway = WakeGrading.SelectTier(4.5f, 400f, 3f, c);  // pushed along
            Assert.GreaterOrEqual(underway, 1, "the SAME dory pushed hard steps up to at least Medium (speed lever)");
        }

        [Test]
        public void Defaults_HeavyHullDrivenHard_ReachesLargeOrHuge()
        {
            var c = Cfg();
            // A representative future T2+ hull (bigger + heavier) — not a hard-coded hull, just size/mass inputs.
            int atSpeed = WakeGrading.SelectTier(14f, 6000f, 3f, c);
            Assert.GreaterOrEqual(atSpeed, 2, "a big heavy hull underway reaches at least Large");
            int flatOut = WakeGrading.SelectTier(14f, 6000f, 5f, c);
            Assert.AreEqual(3, flatOut, "the biggest, fastest hull throws the Huge wake");
        }

        [Test]
        public void Defaults_HeavySlow_Beats_LightSlow_ByStaticSizeAndWeight()
        {
            var c = Cfg();
            // At the SAME low speed, the laden hull already grades a bigger wake than the dory — static size/weight
            // carry it (the brief: a laden trader shoves a bigger wake even at a crawl).
            float dory = WakeGrading.Magnitude01(4.5f, 400f, 0.6f, c);
            float trader = WakeGrading.Magnitude01(14f, 6000f, 0.6f, c);
            Assert.Greater(trader, dory, "size + weight give the heavy hull a bigger wake even when both dawdle");
            Assert.GreaterOrEqual(WakeGrading.TierIndex(trader, c), WakeGrading.TierIndex(dory, c),
                "and its TIER is no lower");
        }

        [Test]
        public void Defaults_LightFast_Grades_UpFromRest_ViaSpeedAlone()
        {
            var c = Cfg();
            // The dory grades UP purely from speed (dynamic lever) even though its size/weight are tiny.
            int rest = WakeGrading.SelectTier(4.5f, 400f, 0f, c);
            int fast = WakeGrading.SelectTier(4.5f, 400f, 5f, c);
            Assert.AreEqual(0, rest, "at rest the tiny hull is Small");
            Assert.Greater(fast, rest, "flat-out the same hull grades up on speed alone");
        }

        // ==== Plume + foam ramps ========================================================================

        [Test]
        public void PlumeScale_RampsMinToMax_Clamped()
        {
            var c = Cfg();
            Assert.AreEqual(c.PlumeMinScale, WakeGrading.PlumeScale(0f, c), 1e-5f, "magnitude 0 → min scale");
            Assert.AreEqual(c.PlumeMaxScale, WakeGrading.PlumeScale(1f, c), 1e-5f, "magnitude 1 → max scale");
            Assert.AreEqual(c.PlumeMinScale, WakeGrading.PlumeScale(-1f, c), 1e-5f, "clamps below");
            Assert.AreEqual(c.PlumeMaxScale, WakeGrading.PlumeScale(2f, c), 1e-5f, "clamps above");
            float mid = WakeGrading.PlumeScale(0.5f, c);
            Assert.That(mid, Is.GreaterThan(c.PlumeMinScale).And.LessThan(c.PlumeMaxScale), "grows through the middle");
        }

        [Test]
        public void PlumeScale_IsNeverNegative_EvenWithOddConfig()
        {
            var c = Cfg();
            c.PlumeMinScale = -3f; c.PlumeMaxScale = -1f;
            Assert.GreaterOrEqual(WakeGrading.PlumeScale(0.5f, c), 0f, "scale is floored at 0 — never a mirrored sprite");
        }

        [Test]
        public void SpeedOnset_ZeroAtRest_RampsToOne_Monotonic()
        {
            var c = Cfg();
            Assert.AreEqual(0f, WakeGrading.SpeedOnset(0f, c), 1e-5f, "no plume at rest");
            Assert.AreEqual(0f, WakeGrading.SpeedOnset(c.PlumeSpeedOnset, c), 1e-5f, "still 0 right at the onset speed");
            Assert.AreEqual(1f, WakeGrading.SpeedOnset(c.PlumeSpeedOnset + c.PlumeSpeedOnsetRange, c), 1e-5f,
                "full at the top of the ramp");
            Assert.AreEqual(1f, WakeGrading.SpeedOnset(c.PlumeSpeedOnset + c.PlumeSpeedOnsetRange * 3f, c), 1e-5f,
                "saturates (clamped) above");
            float prev = -1f;
            for (float s = 0f; s <= c.PlumeSpeedOnset + c.PlumeSpeedOnsetRange + 2f; s += 0.1f)
            {
                float o = WakeGrading.SpeedOnset(s, c);
                Assert.GreaterOrEqual(o, prev - 1e-6f, "the onset ramp never decreases with speed");
                prev = o;
            }
        }

        [Test]
        public void FoamExtentFactor_AtLeastOne_GrowsWithMagnitude_NeutralAtZeroInfluence()
        {
            var c = Cfg();
            Assert.AreEqual(1f, WakeGrading.FoamExtentFactor(0f, c), 1e-5f, "no magnitude → foam exactly as tuned");
            Assert.Greater(WakeGrading.FoamExtentFactor(1f, c), 1f, "full magnitude grows the foam footprint");
            Assert.GreaterOrEqual(WakeGrading.FoamExtentFactor(0.5f, c), 1f, "never shrinks the foam below tuned");

            var neutral = c;
            neutral.FoamMagnitudeInfluence = 0f;
            Assert.AreEqual(1f, WakeGrading.FoamExtentFactor(1f, neutral), 1e-5f,
                "influence 0 leaves the foam untouched at any magnitude");
        }
    }
}
