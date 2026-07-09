using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Fishing;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// The PURE haul-with-the-swell maths (trap-fishing arc Build 4, redesigned Build 6, rule 5): the
    /// lift/drop read, the hold take-rate (calm = a steady wind-in; rough = fast on the lift, a slip on the
    /// drop), the swell-coupled forgiveness (calm forgiving ⇒ gale a fight — P5), the diegetic rope-load /
    /// strain reads, and the greybox rope curve. Engine-light — a pure function of the inputs, so these run
    /// headless with no scene/clock/Time. The catch determinism is Build 3's (PlacedTrapRuntimeTests) — NOT
    /// re-tested here.
    /// </summary>
    public class TrapHaulMathTests
    {
        // ---- the lift/drop read (the swell's vertical velocity under the buoy) ------------------

        [Test]
        public void LiftSignal_RisingSurfaceLifts_FallingSurfaceDrops()
        {
            // height climbing tick-over-tick → a positive (lifting) signal; falling → negative (dropping).
            float lifting = TrapHaulMath.LiftSignal(0.5f, 0.4f, 1f, 0.1f, 2f);
            float dropping = TrapHaulMath.LiftSignal(0.4f, 0.5f, 1f, 0.1f, 2f);
            Assert.Greater(lifting, 0f, "a rising surface reads as a LIFT");
            Assert.Less(dropping, 0f, "a falling surface reads as a DROP");
            Assert.AreEqual(-dropping, lifting, 1e-6f, "the read is symmetric about the direction of travel");
        }

        [Test]
        public void LiftSignal_GlassSea_IsAlwaysZero()
        {
            // Near-zero envelope (glass) → no swell to read → 0 (the calm falls to the steady wind-in path).
            Assert.AreEqual(0f, TrapHaulMath.LiftSignal(0.2f, -0.2f, 0f, 0.1f, 2f), 1e-6f);
            Assert.AreEqual(0f, TrapHaulMath.LiftSignal(0.2f, -0.2f, 1e-8f, 0.1f, 2f), 1e-6f);
        }

        [Test]
        public void LiftSignal_ClampsToUnit_AndGuardsZeroDt()
        {
            Assert.AreEqual(1f, TrapHaulMath.LiftSignal(1f, 0f, 1f, 0.01f, 2f), 1e-6f, "a fast heave saturates at +1");
            Assert.AreEqual(-1f, TrapHaulMath.LiftSignal(0f, 1f, 1f, 0.01f, 2f), 1e-6f, "a fast plunge saturates at −1");
            Assert.AreEqual(0f, TrapHaulMath.LiftSignal(1f, 0f, 1f, 0f, 2f), 1e-6f, "dt 0 → no signal (guard)");
        }

        // ---- the hold take-rate (calm forgiving, rough a fight — P5) ----------------------------

        [Test]
        public void HoldLineRate_Calm_TakesTheSteadyWindIn_RegardlessOfTiming()
        {
            const float calm = 0.55f, swell = 0.95f;
            // In a glassy calm the swell is irrelevant: holding takes calmHaulRate whether "lifting" or not.
            Assert.AreEqual(calm, TrapHaulMath.HoldLineRate(1f, 0f, 1f, calm, swell), 1e-6f, "calm + lift = wind-in");
            Assert.AreEqual(calm, TrapHaulMath.HoldLineRate(-1f, 0f, 1f, calm, swell), 1e-6f, "calm + drop = wind-in (forgiving)");
        }

        [Test]
        public void HoldLineRate_Rough_GainsFastOnTheLift_SlipsBackOnTheDrop()
        {
            const float calm = 0.2f, swell = 1.0f;
            float onLift = TrapHaulMath.HoldLineRate(1f, 1f, 1f, calm, swell);   // gale, holding on the lift
            float onDrop = TrapHaulMath.HoldLineRate(-1f, 1f, 1f, calm, swell);  // gale, holding through the drop

            Assert.AreEqual(swell, onLift, 1e-6f, "hold on the lift in a gale takes line fast");
            Assert.Less(onDrop, 0f, "hold THROUGH the drop and the rope slips line back (the fight)");
            Assert.AreEqual(-swell, onDrop, 1e-6f, "the slip is the full swell rate, negated");
        }

        [Test]
        public void HoldLineRate_CalmIsMoreForgivingThanRough_OnADrop()
        {
            const float calm = 0.5f, swell = 1.0f;
            float calmDrop = TrapHaulMath.HoldLineRate(-1f, 0f, 1f, calm, swell);   // still gains
            float roughDrop = TrapHaulMath.HoldLineRate(-1f, 1f, 1f, calm, swell);  // slips
            Assert.Greater(calmDrop, roughDrop, "a mistimed hold costs far less in a calm than in a gale (P5)");
            Assert.Greater(calmDrop, 0f, "a calm never punishes a mistimed hold — it just keeps winding in");
        }

        [Test]
        public void HoldLineRate_CouplingZero_IgnoresSeaState()
        {
            const float calm = 0.4f, swell = 1.0f;
            // coupling 0 = sea state never takes over; always the forgiving calm wind-in, even in a storm.
            Assert.AreEqual(calm, TrapHaulMath.HoldLineRate(1f, 1f, 0f, calm, swell), 1e-6f);
            Assert.AreEqual(calm, TrapHaulMath.HoldLineRate(-1f, 1f, 0f, calm, swell), 1e-6f, "no coupling → the drop can't slip");
        }

        // ---- a clean haul lands faster than a sloppy one (the whole point of the redesign) ------

        [Test]
        public void CleanHaul_HoldingTheLiftsOnly_SurfacesFasterThanHoldingThrough()
        {
            // A rough sea (sea = 1) with a real swell: lift(t) = sin(2π t / T). "Clean" holds only while
            // lifting and eases on the falls; "sloppy" holds through everything. The clean hauler surfaces;
            // the sloppy one fights the sea to a standstill and never does — the redesign's core promise.
            const float sea = 1f, coupling = 1f, calm = 0.2f, swell = 0.6f;
            const float period = 3f, dt = 0.01f, budget = 30f;

            float cleanLine = 0f, sloppyLine = 0f;
            float cleanTime = -1f, sloppyPeak = 0f;
            for (float t = 0f; t < budget; t += dt)
            {
                float lift = Mathf.Sin(2f * Mathf.PI * t / period);

                // clean: hold on the lift, ease on the fall
                if (lift > 0f)
                    cleanLine = Mathf.Clamp01(cleanLine + TrapHaulMath.HoldLineRate(lift, sea, coupling, calm, swell) * dt);
                if (cleanLine >= 1f && cleanTime < 0f) cleanTime = t;

                // sloppy: hold through the whole swing
                sloppyLine = Mathf.Clamp01(sloppyLine + TrapHaulMath.HoldLineRate(lift, sea, coupling, calm, swell) * dt);
                sloppyPeak = Mathf.Max(sloppyPeak, sloppyLine);
            }

            Assert.Greater(cleanTime, 0f, "a clean haul (hold the lifts, ease the falls) surfaces the pot");
            Assert.Less(cleanTime, budget, "…and does so well within the budget");
            Assert.Less(sloppyPeak, 1f, "holding through every drop fights the sea to a standstill — never surfaces");
        }

        // ---- the diegetic rope-load read (slack on the lift, taut on the drop) ------------------

        [Test]
        public void SwellRopeLoad_SlackOnTheLift_TautOnTheDrop_ScaledBySea()
        {
            Assert.AreEqual(0f, TrapHaulMath.SwellRopeLoad01(1f, 1f), 1e-6f, "on the LIFT the rope is slack (take now)");
            Assert.Greater(TrapHaulMath.SwellRopeLoad01(-1f, 1f), 0f, "on the DROP the rope loads up (ease off)");
            Assert.AreEqual(1f, TrapHaulMath.SwellRopeLoad01(-1f, 1f), 1e-6f, "a full drop in a gale is bar-taut");
            Assert.AreEqual(0f, TrapHaulMath.SwellRopeLoad01(-1f, 0f), 1e-6f, "a glassy calm always reads relaxed");
        }

        [Test]
        public void FightStrain_OnlyWhenHoldingThroughADrop()
        {
            Assert.AreEqual(0f, TrapHaulMath.FightStrain01(false, -1f, 1f), 1e-6f, "not holding → no fight");
            Assert.AreEqual(0f, TrapHaulMath.FightStrain01(true, 1f, 1f), 1e-6f, "holding on the LIFT → no fight");
            Assert.Greater(TrapHaulMath.FightStrain01(true, -1f, 1f), 0f, "holding THROUGH the drop → the fight strain");
        }

        // ---- the ambient strain baseline -------------------------------------------------------

        [Test]
        public void RopeStrain_RisesWithSea_EasesAsLineIsWon()
        {
            float rough0 = TrapHaulMath.RopeStrain01(1f, 0f, 0.5f);   // gale, pot on the bottom
            float rough1 = TrapHaulMath.RopeStrain01(1f, 1f, 0.5f);   // gale, pot surfaced
            float calm0 = TrapHaulMath.RopeStrain01(0f, 0f, 0.5f);    // glass

            Assert.Greater(rough0, calm0, "strain rises with sea state");
            Assert.Less(rough1, rough0, "a surfaced pot relieves strain");
            Assert.AreEqual(0f, calm0, 1e-6f, "a glassy calm has no strain");
        }

        // ---- the greybox rope curve (taut straight, slack drooping) ----------------------------

        [Test]
        public void SampleHaulRope_TautIsStraight_SlackDroops()
        {
            var rail = new Vector2(0f, 0f);
            var pot = new Vector2(4f, 0f);
            var buf = new Vector2[9];

            TrapHaulMath.SampleHaulRope(rail, pot, taut01: 1f, maxSag: 1f, buf);
            Assert.AreEqual(0f, buf[4].y, 1e-5f, "a taut rope is a straight line (no belly)");

            TrapHaulMath.SampleHaulRope(rail, pot, taut01: 0f, maxSag: 1f, buf);
            Assert.Less(buf[4].y, 0f, "a slack rope droops (belly sags down)");
            Assert.AreEqual(rail, buf[0], "the curve starts at the rail");
            Assert.AreEqual(pot, buf[buf.Length - 1], "and ends at the pot");
        }

        [Test]
        public void RopeTaut_TakesTheHigherOfLoadAndFight()
        {
            Assert.AreEqual(0.8f, TrapHaulMath.RopeTaut01(0.8f, 0f), 1e-6f, "the swell load holds it taut on the drop");
            Assert.AreEqual(1f, TrapHaulMath.RopeTaut01(0.2f, 1f), 1e-6f, "a hard fight snaps it taut over a low load");
        }
    }
}
