using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Fishing;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// The PURE rhythm-haul maths (trap-fishing arc Build 4, rule 5): on-beat vs off-beat scoring, the
    /// swell-coupled timing window (calm forgiving ⇒ gale tight), the rope-strain read, and the greybox rope
    /// curve. Engine-light — a pure function of the inputs, so these run headless with no scene/clock/Time.
    /// The catch determinism is Build 3's (PlacedTrapRuntimeTests) — NOT re-tested here.
    /// </summary>
    public class TrapHaulMathTests
    {
        // ---- the swell-coupled window (calm forgiving, a gale tight — P5) ----------------------

        [Test]
        public void OnBeatWindow_IsWidestInCalm_AndTightensWithSeaState()
        {
            const float calm = 0.3f;
            const float coupling = 0.75f;

            float wCalm  = TrapHaulMath.OnBeatWindow(calm, 0f, coupling);   // glassy
            float wRough = TrapHaulMath.OnBeatWindow(calm, 1f, coupling);   // full storm

            Assert.AreEqual(calm, wCalm, 1e-6f, "glassy calm gives the full forgiving window");
            Assert.Less(wRough, wCalm, "a big sea tightens the window");
            Assert.AreEqual(calm * (1f - coupling), wRough, 1e-6f, "full storm shrinks it by the coupling factor");
        }

        [Test]
        public void OnBeatWindow_CouplingZero_IgnoresSeaState()
        {
            // coupling 0 = sea state doesn't matter; the window is always the calm width.
            Assert.AreEqual(0.25f, TrapHaulMath.OnBeatWindow(0.25f, 0f, 0f), 1e-6f);
            Assert.AreEqual(0.25f, TrapHaulMath.OnBeatWindow(0.25f, 1f, 0f), 1e-6f, "no coupling → sea state ignored");
        }

        [Test]
        public void OnBeatWindow_FullCoupling_ClosesTheWindowInAStorm()
        {
            Assert.AreEqual(0f, TrapHaulMath.OnBeatWindow(0.3f, 1f, 1f), 1e-6f, "full coupling + full storm = no window");
        }

        // ---- on-beat vs off-beat → line gain ---------------------------------------------------

        [Test]
        public void LineGain_DeadOnCrest_GainsTheMax()
        {
            float gain = TrapHaulMath.LineGain(0f, 0.3f, 0.14f, 0.35f);
            Assert.AreEqual(0.14f, gain, 1e-6f, "a dead-on-crest pull gains the full max");
        }

        [Test]
        public void LineGain_OffTheBeat_GainsNothing_NoPenalty()
        {
            // Just past the window → 0 gain (owner: a mistimed pull just doesn't gain line; never a penalty).
            float gain = TrapHaulMath.LineGain(0.31f, 0.3f, 0.14f, 0.35f);
            Assert.AreEqual(0f, gain, 1e-6f, "a pull outside the window gains nothing");
        }

        [Test]
        public void LineGain_AtTheWindowEdge_GainsTheEdgeFloor()
        {
            float max = 0.14f, edge = 0.35f;
            float gain = TrapHaulMath.LineGain(0.3f, 0.3f, max, edge);
            Assert.AreEqual(max * edge, gain, 1e-5f, "a just-in-window pull earns the taper floor, not zero");
        }

        [Test]
        public void LineGain_TapersMonotonically_CrestToEdge()
        {
            float window = 0.3f, max = 0.14f, edge = 0.3f;
            float gCrest = TrapHaulMath.LineGain(0f, window, max, edge);
            float gMid   = TrapHaulMath.LineGain(0.15f, window, max, edge);
            float gEdge  = TrapHaulMath.LineGain(0.3f, window, max, edge);
            Assert.Greater(gCrest, gMid);
            Assert.Greater(gMid, gEdge);
            Assert.Greater(gEdge, 0f, "still positive at the edge (the floor)");
        }

        [Test]
        public void IsOnBeat_TracksTheWindow()
        {
            Assert.IsTrue(TrapHaulMath.IsOnBeat(0.1f, 0.2f), "inside the window is on the beat");
            Assert.IsTrue(TrapHaulMath.IsOnBeat(0.2f, 0.2f), "exactly at the edge is on the beat");
            Assert.IsFalse(TrapHaulMath.IsOnBeat(0.21f, 0.2f), "past the edge is off the beat");
        }

        // ---- phase from the swell height -------------------------------------------------------

        [Test]
        public void PhaseFromHeight_CrestIsTheBeat_TroughIsFarthest()
        {
            // crest (+amp) → phase 0 (on the beat); trough (−amp) → phase 0.5 (farthest from the beat).
            Assert.AreEqual(0f, TrapHaulMath.PhaseFromHeight(1f, 1f), 1e-5f, "the crest is the beat");
            Assert.AreEqual(0.5f, TrapHaulMath.PhaseFromHeight(-1f, 1f), 1e-5f, "the trough is farthest off-beat");
            Assert.AreEqual(0.25f, TrapHaulMath.PhaseFromHeight(0f, 1f), 1e-5f, "the mean surface is mid-phase");
        }

        [Test]
        public void PhaseFromHeight_GlassSea_IsAlwaysOnTheBeat()
        {
            // Near-zero envelope (glass) → phase 0 → always on the beat (the forgiving calm the design wants).
            Assert.AreEqual(0f, TrapHaulMath.PhaseFromHeight(0f, 0f), 1e-6f);
            Assert.AreEqual(0f, TrapHaulMath.PhaseFromHeight(0.5f, 1e-8f), 1e-6f);
        }

        // ---- the diegetic strain read ----------------------------------------------------------

        [Test]
        public void RopeStrain_RisesWithSea_EasesAsLineIsWon()
        {
            float rough0 = TrapHaulMath.RopeStrain01(1f, 0f, 0.5f);   // gale, pot on the bottom
            float rough1 = TrapHaulMath.RopeStrain01(1f, 1f, 0.5f);   // gale, pot surfaced
            float calm0  = TrapHaulMath.RopeStrain01(0f, 0f, 0.5f);   // glass

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
        public void RopeTaut_TakesTheHigherOfStrainAndPull()
        {
            Assert.AreEqual(0.8f, TrapHaulMath.RopeTaut01(0.8f, 0f), 1e-6f, "strain holds it taut between pulls");
            Assert.AreEqual(1f, TrapHaulMath.RopeTaut01(0.2f, 1f), 1e-6f, "a pull snaps it taut over low strain");
        }
    }
}
