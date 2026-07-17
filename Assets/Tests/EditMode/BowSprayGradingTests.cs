using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Boats;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// The PURE-logic guard for the graded BOW SPRAY (the owner's brief: spray is a SPEED phenomenon, and the
    /// dory — the slowest boat in the game — "will only be gradual compared to faster moving boats"). Every
    /// claim is exercised on the side-effect-free <see cref="BowSprayGrading"/> math headless, modelled on
    /// <c>WakeGradingTests</c>:
    /// <list type="bullet">
    /// <item><description><b>The dory stays gentle</b> — pinned to the REAL hull data: the dory is 4.5 m /
    /// 400 kg (`Data/Boats/Dory.asset`) with a rowed terminal speed of <b>2.0 m/s, MEASURED</b> (see
    /// <see cref="DoryTopSpeed"/> — the old ratio-derived 2.5 was wrong, and she really did 2.95). No spray at
    /// cruise, a barely-there wisp at a flat-out row, never past the Small tier, and FULL spray is unreachable
    /// at any speed the dory can make.</description></item>
    /// <item><description><b>Speed-forward by construction</b> — the speed weight dominates size + weight
    /// combined, and the onset sits between the dory's cruise and its top speed.</description></item>
    /// <item><description><b>Faster/heavier hulls earn the sheet</b> — an engine-speed hull grades up and
    /// reaches full onset; a big fast hull reaches Huge.</description></item>
    /// <item><description><b>Monotone + bounded</b> — magnitude/tier/scale/onset never decrease with any
    /// input and stay in range, sharing the defensively-sorted cores the wake proved out.</description></item>
    /// </list>
    /// </summary>
    public class BowSprayGradingTests
    {
        private static BowSprayGradeConfig Cfg() => BowSprayGradeConfig.Default;

        // The dory's REAL stats (Data/Boats/Dory.asset) — the inputs, not hard-coded feel.
        private const float DoryLength = 4.5f;
        private const float DoryMass = 400f;
        private const float DoryCruise = 1.4f;                    // a steady, unhurried row

        /// <summary>
        /// The dory's rowed terminal speed — <b>MEASURED</b> on real physics
        /// (<c>PilotableFleetPlayTests.TheDory_IsTheSlowestBoatAfloat</c>), never derived here.
        ///
        /// <para>This used to read <c>300f / 120f</c> — "OarPower / ForwardDrag ≈ 2.5 m/s" — and that ratio is
        /// wrong TWICE. (1) BOTH oars pull: <c>BoatController.OarThrust</c> sums them, so a flat-out row is
        /// 600 N, not 300. (2) The rigidbody's own <c>linearDamping</c> is ~40–50% of the dory's resistance and
        /// appears in no stat on the asset. She actually did <b>2.95</b> m/s — which is how the boat the owner
        /// calls the slowest in the game came to out-run three others and cross into bow spray she was never
        /// meant to throw. She is now tuned (ForwardDrag 215) and MEASURES 2.0.</para>
        ///
        /// <para><b>Do not re-derive it.</b> A ratio here would be a second copy of a model that is already
        /// wrong; the PlayMode test runs the real hull on the real integrator and is the only thing that knows.</para>
        /// </summary>
        private const float DoryTopSpeed = 2.0f;

        // ==== the owner's brief: the dory's spray is gradual/subtle at its REAL speeds =====================

        [Test]
        public void Dory_AtRestAndAtCruise_ShowsEssentiallyNoSpray()
        {
            var c = Cfg();
            Assert.AreEqual(0f, BowSprayGrading.SpeedOnset(0f, c), 1e-5f, "no spray at rest");
            Assert.AreEqual(0f, BowSprayGrading.SpeedOnset(1.5f, c), 1e-5f, "no spray at an easy row (1.5 m/s)");
            float atCruise = BowSprayGrading.SpeedOnset(DoryCruise, c);
            Assert.That(atCruise, Is.GreaterThanOrEqualTo(0f).And.LessThan(0.25f),
                "a rowing cruise sits at the very bottom of the ramp — at most a barely-there wisp");
        }

        [Test]
        public void Dory_AtAFlatOutRow_SprayIsSubtleAndPartial_NeverFull()
        {
            var c = Cfg();
            float onset = BowSprayGrading.SpeedOnset(DoryTopSpeed, c);
            Assert.Greater(onset, 0f, "rowing hard JUST starts to show spray (the owner's 'gradual')");
            Assert.Less(onset, 0.25f,
                "even flat-out, the dory sees only the very bottom of the onset ramp — a barely-there wisp. " +
                "This was 0.5 when her top speed was believed to be 2.5; at her REAL 2.95 she was seeing 62% " +
                "of the ramp and this assertion was passing anyway, which is why it is tightened now that she " +
                "is measured at 2.0.");
            Assert.Less(BowSprayGrading.SpeedOnset(DoryTopSpeed, c), 1f,
                "FULL spray needs a speed the dory cannot reach — it belongs to the faster hulls to come");
        }

        [Test]
        public void Dory_NeverLeavesTheSmallestSprayTier_AtAnySpeedItCanMake()
        {
            var c = Cfg();
            for (float v = 0f; v <= DoryTopSpeed + 1e-3f; v += 0.1f)
            {
                Assert.AreEqual(0, BowSprayGrading.SelectTier(DoryLength, DoryMass, v, c),
                    $"the dory at {v:0.0} m/s stays on the Small spray — prominence is for faster boats");
            }
        }

        [Test]
        public void FasterHulls_EarnTheSpray_TheDoryCannot()
        {
            var c = Cfg();
            // The sport skiff's MEASURED terminal speed (PilotableFleetPlayTests) — not a ratio. The figure this
            // replaced, "EnginePower 500 / ForwardDrag 120 ≈ 4.2", was the same bad derivation the dory's was.
            const float skiffTop = 4.57f;
            Assert.AreEqual(1f, BowSprayGrading.SpeedOnset(skiffTop, c), 1e-5f,
                "an engine hull reaches FULL spray opacity — the ramp tops out beyond the dory but within reach " +
                "of the next tier");
            Assert.Greater(BowSprayGrading.Magnitude01(DoryLength, DoryMass, skiffTop, c),
                           BowSprayGrading.Magnitude01(DoryLength, DoryMass, DoryTopSpeed, c),
                "the SAME hull pushed to engine speed grades a bigger spray (the speed lever)");

            // A representative future big hull driven hard — not a hard-coded hull, just size/mass/speed inputs.
            Assert.AreEqual(3, BowSprayGrading.SelectTier(14f, 6000f, 6f, c),
                "a big, heavy hull at full speed throws the Huge spray sheet");
        }

        // ==== speed-forward by construction ================================================================

        [Test]
        public void Defaults_AreSpeedForward_SpeedWeightDominatesSizePlusMass()
        {
            var c = Cfg();
            Assert.Greater(c.WeightSpeed, c.WeightSize + c.WeightMass,
                "spray is a SPEED phenomenon: its weight must dominate size + weight combined (owner's brief)");
            Assert.Greater(c.SpraySpeedOnset, DoryCruise * 0.5f, "the onset is a real bar, not a giveaway");
            Assert.Less(c.SpraySpeedOnset, DoryTopSpeed,
                "…but sits below the dory's top speed so a hard row can JUST start to show it");
        }

        [Test]
        public void Defaults_MatchTheAuthoredSprayArtOrientation()
        {
            var c = Cfg();
            Assert.AreEqual(0f, c.SprayPivotY, 1e-5f,
                "the spray art's impact churn is at the image BOTTOM (pixel-verified by WakeArtOrientationTests) " +
                "→ pivot 0 pins the impact at the bow");
            Assert.IsFalse(c.SprayFlip, "the current art is authored impact-at-bottom → no flip");
        }

        // ==== monotone + bounded (shares the wake's defensively-sorted cores) =============================

        [Test]
        public void Magnitude01_IsMonotonicNonDecreasingInEachInput_OverAGrid()
        {
            var c = Cfg();
            float prev = -1f;
            for (float len = 3f; len <= 24f; len += 1f)
            {
                float m = BowSprayGrading.Magnitude01(len, 2000f, 3f, c);
                Assert.GreaterOrEqual(m, prev - 1e-6f, "magnitude never falls as length rises");
                prev = m;
            }
            prev = -1f;
            for (float kg = 200f; kg <= 9000f; kg += 300f)
            {
                float m = BowSprayGrading.Magnitude01(9f, kg, 3f, c);
                Assert.GreaterOrEqual(m, prev - 1e-6f, "magnitude never falls as mass rises");
                prev = m;
            }
            prev = -1f;
            for (float sp = 0f; sp <= 7f; sp += 0.25f)
            {
                float m = BowSprayGrading.Magnitude01(9f, 2000f, sp, c);
                Assert.GreaterOrEqual(m, prev - 1e-6f, "magnitude never falls as speed rises");
                prev = m;
            }
        }

        [Test]
        public void TierIndex_MonotonicAndInRange_EvenWithScrambledThresholds()
        {
            var c = Cfg();
            c.Threshold1 = 0.7f; c.Threshold2 = 0.2f; c.Threshold3 = 0.5f;   // deliberately scrambled
            int prev = -1;
            for (float m = 0f; m <= 1f + 1e-4f; m += 0.02f)
            {
                int tier = BowSprayGrading.TierIndex(m, c);
                Assert.GreaterOrEqual(tier, prev, "scrambled thresholds are sorted defensively → still monotone");
                Assert.That(tier, Is.InRange(0, WakeGrading.TierCount - 1));
                prev = tier;
            }
        }

        [Test]
        public void SprayScale_RampsMinToMax_Clamped_NeverNegative()
        {
            var c = Cfg();
            Assert.AreEqual(c.SprayMinScale, BowSprayGrading.SprayScale(0f, c), 1e-5f);
            Assert.AreEqual(c.SprayMaxScale, BowSprayGrading.SprayScale(1f, c), 1e-5f);
            Assert.AreEqual(c.SprayMinScale, BowSprayGrading.SprayScale(-2f, c), 1e-5f, "clamps below");
            Assert.AreEqual(c.SprayMaxScale, BowSprayGrading.SprayScale(3f, c), 1e-5f, "clamps above");
            c.SprayMinScale = -4f; c.SprayMaxScale = -1f;
            Assert.GreaterOrEqual(BowSprayGrading.SprayScale(0.5f, c), 0f, "floored at 0 — never a mirrored sprite");
        }

        [Test]
        public void SpeedOnset_MonotonicAndSaturating()
        {
            var c = Cfg();
            float prev = -1f;
            for (float s = 0f; s <= c.SpraySpeedOnset + c.SpraySpeedOnsetRange + 2f; s += 0.1f)
            {
                float o = BowSprayGrading.SpeedOnset(s, c);
                Assert.GreaterOrEqual(o, prev - 1e-6f, "the onset ramp never decreases with speed");
                Assert.That(o, Is.InRange(0f, 1f));
                prev = o;
            }
            Assert.AreEqual(1f, BowSprayGrading.SpeedOnset(c.SpraySpeedOnset + c.SpraySpeedOnsetRange + 5f, c),
                1e-5f, "saturates at 1");
        }

        [Test]
        public void Magnitude01_AllZeroWeights_IsZero_NoNaN()
        {
            var c = Cfg();
            c.WeightSize = 0f; c.WeightMass = 0f; c.WeightSpeed = 0f;
            Assert.AreEqual(0f, BowSprayGrading.Magnitude01(20f, 8000f, 6f, c), 1e-6f,
                "degenerate all-zero weights collapse to 0, never a divide-by-zero");
        }

        // ==== the bow anchor (the placement mirror of the stern fix) ======================================

        /// <summary>A plan view — no foreshortening, i.e. the top-down placement these cases were written
        /// against. The ¾ projection is pinned in WakeProjectionTests.</summary>
        private const float PlanViewElev = 90f;

        [Test]
        public void BowAnchor_SitsAheadOfTheBowTip()
        {
            Vector2 a = BowSprayGrading.BowAnchor(Vector2.zero, Vector2.up, DoryLength, 0.05f, PlanViewElev);
            Assert.AreEqual(0f, a.x, 1e-4f);
            Assert.AreEqual(2.3f, a.y, 1e-4f, "half the hull + the nudge — AT the cutwater, ahead of the origin");

            Vector2 bow = new Vector2(-1f, 2f).normalized;
            Vector2 b = BowSprayGrading.BowAnchor(new Vector2(5f, 1f), bow, 6f, 0.1f, PlanViewElev);
            float ahead = Vector2.Dot(b - new Vector2(5f, 1f), bow);
            Assert.GreaterOrEqual(ahead, 3f, "anchor is ahead of the hull's front edge (≥ length/2 along the bow)");
        }

        [Test]
        public void BowAnchor_DegenerateBow_FallsBackToUp_NoNaN()
        {
            Vector2 a = BowSprayGrading.BowAnchor(Vector2.zero, Vector2.zero, DoryLength, 0.05f, PlanViewElev);
            Assert.IsFalse(float.IsNaN(a.x) || float.IsNaN(a.y), "zero bow never yields NaN");
            Assert.AreEqual(2.3f, a.y, 1e-4f, "falls back to +Y as the bow");
        }
    }
}
