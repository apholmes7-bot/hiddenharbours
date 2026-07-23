using System;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Fishing;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// The PURE rod-fight maths (Rod Fishing v2 — the deep→surface arc, brainstorm §3/§8, rule 5): the two
    /// signed per-second rates (tension toward the snap, landing toward aboard), the counter-steer axis (Surface
    /// only), the deep→surface crossing, the diegetic rod/line reads, and — the load-bearing part — the two
    /// forgiving-cove invariants held across the tuning range (a blind pull snaps before it lands; a maintain
    /// always bleeds). Engine-light: a pure function of the inputs, so these run headless with no scene/clock/Time.
    /// The fish's run↔slack rhythm (fishEffort01) is generated elsewhere; here it's just an input we drive.
    /// </summary>
    public class RodFightMathTests
    {
        // The cove-forgiving reference tuning — mirrors RodFightSettings.Default (GameConfig.RodFight). Both invariants
        // hold: Rise (0.55) > Fill (0.35); RunP (0.35) < Fall (0.70).
        const float Rise = 0.55f, Fall = 0.70f, Fill = 0.35f, RunP = 0.35f, Relief = 0.45f, Surf = 0.5f;

        static float Tension(bool reeling, float effort, float steer, RodFightPhase phase)
            => RodFightMath.TensionRatePerSec(reeling, effort, steer, phase, Rise, Fall, RunP, Relief);

        static float Landing(bool reeling, float effort, float steer, RodFightPhase phase)
            => RodFightMath.LandingRatePerSec(reeling, effort, steer, phase, Fill);

        // ---- pull-on-slack / maintain-on-run (the timing axis, both phases) ----------------------

        [Test]
        public void PullIntoARun_ClimbsTensionTowardSnap()
        {
            float pullRun = Tension(reeling: true, 1f, 0f, RodFightPhase.Deep);   // reel against a hard run
            float pullSlack = Tension(reeling: true, 0f, 0f, RodFightPhase.Deep); // reel in the slack
            Assert.Greater(pullRun, 0f, "reeling raises tension");
            Assert.Greater(pullRun, pullSlack, "reeling INTO a run loads faster than reeling in the slack (the run adds pressure)");
        }

        [Test]
        public void MaintainInARun_BleedsTension()
        {
            float maintainRun = Tension(reeling: false, 1f, 0f, RodFightPhase.Deep);
            Assert.Less(maintainRun, 0f, "MAINTAIN through a full run must still net tension DOWN (a run is a 'back off' tell)");
        }

        [Test]
        public void PullInTheSlackWindow_GainsLanding_ReelingIntoARunDoesNot()
        {
            float inSlack = Landing(reeling: true, 0f, 0f, RodFightPhase.Deep);
            float inRun = Landing(reeling: true, 1f, 0f, RodFightPhase.Deep);
            Assert.Greater(inSlack, 0f, "PULL in the slack window wins line");
            Assert.AreEqual(0f, inRun, 1e-6f, "reeling INTO a full run wins no line (that's tension, not landing)");
            Assert.Greater(inSlack, inRun, "the slack window is where landing is won");
        }

        [Test]
        public void MaintainAlone_GainsNoLanding_InEitherPhase()
        {
            Assert.AreEqual(0f, Landing(reeling: false, 0.5f, 0f, RodFightPhase.Deep), 1e-6f, "deep maintain wins nothing");
            Assert.AreEqual(0f, Landing(reeling: false, 0.5f, 0f, RodFightPhase.Surface), 1e-6f,
                "surface maintain with NEUTRAL steer wins nothing (only a counter-steer tires her)");
        }

        // ---- counter-steer (Surface only) --------------------------------------------------------

        [Test]
        public void CounterSteerOpposite_TiresHer_BleedsTensionAndGainsLanding()
        {
            const float effort = 0.8f;
            // Tension: steering OPPOSITE (−1) bleeds more than neutral, which bleeds more than steering INTO (+1).
            float opp = Tension(reeling: false, effort, -1f, RodFightPhase.Surface);
            float neu = Tension(reeling: false, effort, 0f, RodFightPhase.Surface);
            float into = Tension(reeling: false, effort, +1f, RodFightPhase.Surface);
            Assert.Less(opp, neu, "counter-steer OPPOSITE bleeds tension harder (tires her)");
            Assert.Less(neu, into, "steering INTO her bleeds least (climbs relative to neutral)");

            // Landing: a counter-steer tires her — landing creeps even while MAINTAINING.
            float landOpp = Landing(reeling: false, effort, -1f, RodFightPhase.Surface);
            float landNeu = Landing(reeling: false, effort, 0f, RodFightPhase.Surface);
            Assert.Greater(landOpp, 0f, "counter-steer during a dart tires her → landing gains, even on a MAINTAIN");
            Assert.Greater(landOpp, landNeu, "…more than doing nothing");
        }

        [Test]
        public void SteerIntoHerRun_ClimbsTension()
        {
            const float effort = 0.8f;
            float into = Tension(reeling: true, effort, +1f, RodFightPhase.Surface);  // reel + steer WITH her
            float neu = Tension(reeling: true, effort, 0f, RodFightPhase.Surface);
            float opp = Tension(reeling: true, effort, -1f, RodFightPhase.Surface);
            Assert.Greater(into, neu, "steering INTO her run climbs tension toward the snap");
            Assert.Greater(neu, opp, "…and a counter-steer relieves it");
        }

        [Test]
        public void DeepPhase_IgnoresSteerEntirely()
        {
            const float effort = 0.8f;
            // Tension is identical for any steer in Deep.
            Assert.AreEqual(Tension(true, effort, 0f, RodFightPhase.Deep), Tension(true, effort, -1f, RodFightPhase.Deep), 1e-6f);
            Assert.AreEqual(Tension(true, effort, 0f, RodFightPhase.Deep), Tension(true, effort, +1f, RodFightPhase.Deep), 1e-6f);
            // Landing too.
            Assert.AreEqual(Landing(true, effort, 0f, RodFightPhase.Deep), Landing(true, effort, -1f, RodFightPhase.Deep), 1e-6f);
            Assert.AreEqual(Landing(true, effort, 0f, RodFightPhase.Deep), Landing(true, effort, +1f, RodFightPhase.Deep), 1e-6f);
            // And the diegetic reads.
            Assert.AreEqual(RodFightMath.RodBend01(effort, -1f, RodFightPhase.Deep), RodFightMath.RodBend01(effort, +1f, RodFightPhase.Deep), 1e-6f);
            Assert.AreEqual(RodFightMath.LineStrain01(true, effort, -1f, RodFightPhase.Deep), RodFightMath.LineStrain01(true, effort, +1f, RodFightPhase.Deep), 1e-6f);
        }

        // ---- the deep→surface crossing -----------------------------------------------------------

        [Test]
        public void PhaseFor_CrossesToSurfaceAtTheThreshold()
        {
            Assert.AreEqual(RodFightPhase.Deep, RodFightMath.PhaseFor(0.0f, Surf));
            Assert.AreEqual(RodFightPhase.Deep, RodFightMath.PhaseFor(Surf - 0.01f, Surf));
            Assert.AreEqual(RodFightPhase.Surface, RodFightMath.PhaseFor(Surf, Surf), "at the threshold she breaks the surface");
            Assert.AreEqual(RodFightPhase.Surface, RodFightMath.PhaseFor(1f, Surf));
        }

        // ---- the forgiving-cove invariants, held ACROSS the tuning range -------------------------

        [Test]
        public void Invariant1_ABlindPullSnapsBeforeItLands_AcrossTheRange()
        {
            // The binding case for "you can't just hold to win" is a fish that never runs (effort ≡ 0): there a
            // blind pull climbs tension at `rise` and fills landing at `fill`, so rise > fill is exactly the
            // guarantee. Sweep cove→toothy; every set with the predicate true must snap before landing in sim.
            foreach (float rise in new[] { 0.5f, 0.7f, 0.9f, 1.1f })
            foreach (float fill in new[] { 0.20f, 0.35f, 0.49f })
            {
                Assert.IsTrue(RodFightMath.PullAloneSnapsBeforeLanding(rise, fill), $"rise {rise} must exceed fill {fill}");

                float tension = 0f, landing = 0f;
                for (int i = 0; i < 100000 && tension < 1f && landing < 1f; i++)
                {
                    tension = Mathf.Clamp01(tension + RodFightMath.TensionRatePerSec(true, 0f, 0f, RodFightPhase.Deep, rise, Fall, RunP, Relief) * 0.02f);
                    landing = Mathf.Clamp01(landing + RodFightMath.LandingRatePerSec(true, 0f, 0f, RodFightPhase.Deep, fill) * 0.02f);
                }
                Assert.GreaterOrEqual(tension, 1f, $"a blind hold must SNAP (rise {rise}, fill {fill})");
                Assert.Less(landing, 1f, $"…before the fish lands (rise {rise}, fill {fill})");
            }
        }

        [Test]
        public void Invariant2_MaintainAlwaysBleeds_EvenAtAFullRun_AcrossTheRange()
        {
            // With neutral-or-counter steer, MAINTAIN nets tension down at ANY run intensity iff runP < fall.
            foreach (float fall in new[] { 0.5f, 0.7f, 0.9f })
            foreach (float runP in new[] { 0.1f, 0.34f, fall - 0.05f })
            {
                Assert.IsTrue(RodFightMath.MaintainOutbleedsTheRun(runP, fall), $"runP {runP} must stay below fall {fall}");

                // Worst case: a full run (effort = 1). Deep, and Surface with a neutral / counter steer.
                float deep = RodFightMath.TensionRatePerSec(false, 1f, 0f, RodFightPhase.Deep, Rise, fall, runP, Relief);
                float surfNeutral = RodFightMath.TensionRatePerSec(false, 1f, 0f, RodFightPhase.Surface, Rise, fall, runP, Relief);
                float surfCounter = RodFightMath.TensionRatePerSec(false, 1f, -1f, RodFightPhase.Surface, Rise, fall, runP, Relief);
                Assert.Less(deep, 0f, $"deep maintain must bleed (fall {fall}, runP {runP})");
                Assert.Less(surfNeutral, 0f, $"surface maintain + neutral steer must bleed (fall {fall}, runP {runP})");
                Assert.Less(surfCounter, deep, "a counter-steer bleeds even harder than a bare maintain");
            }
        }

        [Test]
        public void MaintainCanOnlyClimb_BySteeringIntoHer_TheAvoidableMistake()
        {
            // The one way a MAINTAIN climbs tension is an ACTIVE mistake — steering INTO her during a run — which
            // is gentle and avoidable (steer neutral or opposite and it always bleeds). This documents, not a bug.
            float mistake = Tension(reeling: false, 1f, +1f, RodFightPhase.Surface);
            Assert.Greater(mistake, 0f, "maintain + steer INTO her CAN creep tension up (the avoidable mistake)");
            Assert.Less(Tension(false, 1f, 0f, RodFightPhase.Surface), 0f, "…but neutral steer always bleeds");
            Assert.Less(Tension(false, 1f, -1f, RodFightPhase.Surface), 0f, "…and a counter-steer bleeds hardest");
        }

        // ---- integration: the fight is winnable, and a blind pull throws the hook ----------------

        struct Outcome { public bool Snapped, Landed; public float Tension, Landing; }

        // Drive a full fight under a run↔slack rhythm and a policy. effortOf(t) is the fish; reelOf(effort,
        // tension) is PULL/MAINTAIN; steerOf(phase) is the rod steer. Integrates the pure rates (caller-side
        // clamp), exactly as Wave-3 wiring will.
        static Outcome RunFight(Func<float, float> effortOf, Func<float, float, bool> reelOf,
                                Func<RodFightPhase, float> steerOf, float dt = 0.02f, float budget = 60f)
        {
            float tension = 0f, landing = 0f;
            for (float t = 0f; t < budget; t += dt)
            {
                float effort = Mathf.Clamp01(effortOf(t));
                var phase = RodFightMath.PhaseFor(landing, Surf);
                bool reeling = reelOf(effort, tension);
                float steer = steerOf(phase);
                tension = Mathf.Clamp01(tension + RodFightMath.TensionRatePerSec(reeling, effort, steer, phase, Rise, Fall, RunP, Relief) * dt);
                landing = Mathf.Clamp01(landing + RodFightMath.LandingRatePerSec(reeling, effort, steer, phase, Fill) * dt);
                if (tension >= 1f) return new Outcome { Snapped = true, Tension = tension, Landing = landing };
                if (landing >= 1f) return new Outcome { Landed = true, Tension = tension, Landing = landing };
            }
            return new Outcome { Tension = tension, Landing = landing };
        }

        // A fish that alternates hard runs (effort→1) and slack windows (effort→0) on a steady rhythm.
        static float RunSlackRhythm(float t) => 0.5f + 0.5f * Mathf.Sin(2f * Mathf.PI * t / 3f);

        [Test]
        public void SkilledPolicy_PulsesAndCounterSteers_LandsTheFish()
        {
            // Pull in the slack (and only while the line isn't loading), maintain through runs, counter-steer
            // opposite once she surfaces. This is the intended skill expression — it must land.
            var o = RunFight(
                effortOf: RunSlackRhythm,
                reelOf: (effort, tension) => effort < 0.45f && tension < 0.6f,
                steerOf: phase => phase == RodFightPhase.Surface ? -1f : 0f);

            Assert.IsTrue(o.Landed, $"a paced pulse + counter-steer should LAND (t {o.Tension:0.00}, l {o.Landing:0.00})");
            Assert.IsFalse(o.Snapped, "…without snapping");
        }

        [Test]
        public void BlindPull_HeldToTheWall_Snaps_WithoutLanding()
        {
            // Never let go, never steer — the wrong way to fight. It must throw the hook (snap) before landing.
            var o = RunFight(
                effortOf: RunSlackRhythm,
                reelOf: (effort, tension) => true,
                steerOf: phase => 0f);

            Assert.IsTrue(o.Snapped, "a blind, sustained pull must SNAP (skill is a pulse, not a pin)");
            Assert.Less(o.Landing, 1f, "…before the fish is landed");
        }

        // ---- the diegetic reads (no HUD — the rod & line are the instrument) ---------------------

        [Test]
        public void RodBend_TracksHerRun_AndSurfaceSteerIntoTightensIt()
        {
            Assert.AreEqual(0f, RodFightMath.RodBend01(0f, 0f, RodFightPhase.Surface), 1e-6f, "a slack fish → a straight rod");
            Assert.AreEqual(1f, RodFightMath.RodBend01(1f, 0f, RodFightPhase.Surface), 1e-6f, "a full run → a bar-taut rod");
            Assert.Greater(
                RodFightMath.RodBend01(0.5f, +1f, RodFightPhase.Surface),
                RodFightMath.RodBend01(0.5f, 0f, RodFightPhase.Surface),
                "steering INTO her tightens the arc further (Surface)");
        }

        [Test]
        public void LineStrain_HighOnlyWhenOverloadingTheLine()
        {
            Assert.AreEqual(0f, RodFightMath.LineStrain01(false, 1f, 0f, RodFightPhase.Deep), 1e-6f,
                "MAINTAIN through a run → doing it right → no over-strain");
            Assert.AreEqual(0f, RodFightMath.LineStrain01(true, 0f, 0f, RodFightPhase.Deep), 1e-6f,
                "PULL in dead slack → safe → no over-strain");
            Assert.AreEqual(1f, RodFightMath.LineStrain01(true, 1f, 0f, RodFightPhase.Deep), 1e-6f,
                "PULL into a full run → the 'ease off!' read maxes out");
            Assert.Greater(
                RodFightMath.LineStrain01(false, 0.8f, +1f, RodFightPhase.Surface), 0f,
                "steering INTO her over-strains even on a maintain (Surface)");
            Assert.Less(
                RodFightMath.LineStrain01(true, 0.8f, -1f, RodFightPhase.Surface),
                RodFightMath.LineStrain01(true, 0.8f, 0f, RodFightPhase.Surface),
                "a counter-steer bleeds the over-strain back down");
        }

        [Test]
        public void SlackWindowOpen_IsTheInverseOfHerEffort()
        {
            Assert.AreEqual(1f, RodFightMath.SlackWindowOpen01(0f), 1e-6f, "dead slack → the window is wide open (PULL)");
            Assert.AreEqual(0f, RodFightMath.SlackWindowOpen01(1f), 1e-6f, "a hard run → the window is shut (MAINTAIN)");
            Assert.AreEqual(0.7f, RodFightMath.SlackWindowOpen01(0.3f), 1e-6f);
        }

        // ---- NaN / zero-dt safety (rule 5) -------------------------------------------------------

        [Test]
        public void NaNInputs_AreAllSafe_NeutralizedToZero()
        {
            float nan = float.NaN;
            // Every entry point must return a finite value with NaN anywhere in its inputs.
            Assert.IsFalse(float.IsNaN(RodFightMath.TensionRatePerSec(true, nan, nan, RodFightPhase.Surface, nan, nan, nan, nan)));
            Assert.IsFalse(float.IsNaN(RodFightMath.LandingRatePerSec(true, nan, nan, RodFightPhase.Surface, nan)));
            Assert.IsFalse(float.IsNaN(RodFightMath.RodBend01(nan, nan, RodFightPhase.Surface)));
            Assert.IsFalse(float.IsNaN(RodFightMath.LineStrain01(true, nan, nan, RodFightPhase.Surface)));
            Assert.IsFalse(float.IsNaN(RodFightMath.SlackWindowOpen01(nan)));
            Assert.AreEqual(RodFightPhase.Deep, RodFightMath.PhaseFor(nan, Surf), "a NaN landing reads as the safe Deep default");

            // A NaN effort behaves exactly like a slack (0) effort — no hidden spike.
            Assert.AreEqual(
                Tension(true, 0f, 0f, RodFightPhase.Deep),
                RodFightMath.TensionRatePerSec(true, nan, 0f, RodFightPhase.Deep, Rise, Fall, RunP, Relief), 1e-6f);
        }

        [Test]
        public void ZeroDt_ChangesNothing_TheCallerIntegrationContract()
        {
            // The rates are per-second; the caller does accum += rate · dt. A zero dt must move no accumulator.
            float rate = Tension(true, 1f, +1f, RodFightPhase.Surface);
            Assert.AreNotEqual(0f, rate, "there is a real rate to integrate");
            float acc = 0.3f;
            Assert.AreEqual(acc, Mathf.Clamp01(acc + rate * 0f), 1e-6f, "dt = 0 → the accumulator is unchanged");
        }

        // ---- the shipped GameConfig defaults keep the cove forgiving -----------------------------

        [Test]
        public void GameConfigRodFightDefaults_SatisfyBothInvariants()
        {
            var cfg = ScriptableObject.CreateInstance<GameConfig>();
            var d = cfg.RodFight;
            Assert.IsTrue(RodFightMath.PullAloneSnapsBeforeLanding(d.TensionRisePerSec, d.LandingFillPerSec),
                "the shipped default rise must exceed its fill (invariant 1)");
            Assert.IsTrue(RodFightMath.MaintainOutbleedsTheRun(d.RunTensionPressure, d.TensionFallPerSec),
                "the shipped default run pressure must stay below its fall (invariant 2)");
            Assert.IsTrue(RodFightMath.MaintainOutbleedsTheRunAtTheWorstStance(
                    d.RunTensionPressure, d.DeckAngleFactor, d.TensionFallPerSec),
                "the shipped deck-angle factor must leave a maintain net-negative even at the worst deck " +
                "stance mid-run (the Wave-4 on-deck guard-rail — a bad angle is a nudge, never a snap)");
            Assert.That(d.SurfaceThreshold01, Is.InRange(0f, 1f), "the surface threshold is a 0..1 fraction");
            ScriptableObject.DestroyImmediate(cfg);
        }

        // ---- the deck-angle term (Wave 4 — the reserved seam, design §4.2) -----------------------

        static float TensionOnDeck(bool reeling, float effort, float steer, RodFightPhase phase, float deckPressure)
            => RodFightMath.TensionRatePerSec(reeling, effort, steer, phase, Rise, Fall, RunP, Relief, deckPressure);

        [Test]
        public void DeckAnglePressure_AtZero_IsBitForBitTheDockModel()
        {
            // The dock-parity contract: passing 0 through the 9-arg overload must equal the 8-arg dock
            // model EXACTLY (no epsilon) — every phase, action and steer, so the dock path can route
            // through the grown seam without a bit of drift.
            foreach (var phase in new[] { RodFightPhase.Deep, RodFightPhase.Surface })
            foreach (bool reeling in new[] { true, false })
            foreach (float effort in new[] { 0f, 0.4f, 1f })
            foreach (float steer in new[] { -1f, 0f, 1f })
                Assert.AreEqual(
                    Tension(reeling, effort, steer, phase),
                    TensionOnDeck(reeling, effort, steer, phase, 0f),
                    $"deck pressure 0 must be the dock model exactly ({phase}, reel {reeling}, e {effort}, s {steer})");
        }

        [Test]
        public void DeckAnglePressure_AddsToTension_Linearly_InEveryPhaseAndAction()
        {
            // The term is a plain additive rate — independent of phase, action, effort and steer (the
            // stance is the boat's geometry, not the fish's) — and LandingRatePerSec has no deck input
            // at all (API shape): a bad stance can never pay the angler line, only load it.
            const float pressure = 0.15f;
            foreach (var phase in new[] { RodFightPhase.Deep, RodFightPhase.Surface })
            foreach (bool reeling in new[] { true, false })
            foreach (float effort in new[] { 0f, 1f })
                Assert.AreEqual(Tension(reeling, effort, 0f, phase) + pressure,
                    TensionOnDeck(reeling, effort, 0f, phase, pressure), 1e-6f,
                    $"the deck term adds linearly ({phase}, reel {reeling}, e {effort})");
        }

        [Test]
        public void DeckAnglePressure_NegativeOrNaN_ReadsAsZero()
        {
            float dock = Tension(false, 1f, 0f, RodFightPhase.Deep);
            Assert.AreEqual(dock, TensionOnDeck(false, 1f, 0f, RodFightPhase.Deep, -0.4f), 1e-6f,
                "a negative stance pressure is clamped to 0 — the deck never PAYS tension off");
            Assert.AreEqual(dock, TensionOnDeck(false, 1f, 0f, RodFightPhase.Deep, float.NaN), 1e-6f,
                "NaN reads as the safe 0 (rule 5)");
        }

        [Test]
        public void Invariant2_HoldsOnDeck_AtTheWorstStance_WithShippedDefaults()
        {
            // The on-deck extension of invariant 2, integrated: at the worst stance (a line fully across
            // the hull) through her hardest run, MAINTAIN must still net tension DOWN with the shipped
            // defaults — cozy: the bad angle nudges you to walk the rail, it never forces a snap.
            float worst = RodFightSettings.Default.DeckAngleFactor;   // pressure ceiling: factor × across(1)
            Assert.IsTrue(RodFightMath.MaintainOutbleedsTheRunAtTheWorstStance(RunP, worst, Fall));
            Assert.Less(TensionOnDeck(false, 1f, 0f, RodFightPhase.Deep, worst), 0f,
                "deep maintain must bleed at the worst stance mid-run");
            Assert.Less(TensionOnDeck(false, 1f, 0f, RodFightPhase.Surface, worst), 0f,
                "surface maintain (neutral steer) must bleed at the worst stance mid-run");

            // And invariant 1 only gets SAFER on deck: the term adds tension, never landing, so a blind
            // pull still snaps before it lands — sooner, if anything.
            float tension = 0f, landing = 0f;
            int snapTick = -1;
            for (int i = 0; i < 100000 && landing < 1f; i++)
            {
                tension = Mathf.Clamp01(tension + TensionOnDeck(true, 0f, 0f, RodFightPhase.Deep, worst) * 0.02f);
                landing = Mathf.Clamp01(landing + Landing(true, 0f, 0f, RodFightPhase.Deep) * 0.02f);
                if (tension >= 1f) { snapTick = i; break; }
            }
            Assert.GreaterOrEqual(snapTick, 0, "a blind pull on deck must still SNAP before it lands");
            Assert.Less(landing, 1f, "…before the fish lands");
        }
    }
}
