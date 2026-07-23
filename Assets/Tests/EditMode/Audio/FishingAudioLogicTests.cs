using NUnit.Framework;
using HiddenHarbours.Core;
using HiddenHarbours.Audio;

namespace HiddenHarbours.Tests.Audio
{
    /// <summary>
    /// Rod Fishing v2 fight audio — the PURE decision logic (design rod-fishing-v2-brainstorm.md
    /// §2–3, §7). Pins the phase→cue table (the two bite tells, the splash-down, the cozy snap),
    /// the slack-edge cues (bottom settle vs "PULL now" release), and the continuous mix (the
    /// strain groan's "ease off!" curve, the depth-slowed pay-out, reel-on-gain, the surface
    /// thrash). No AudioSources, no Unity scene — everything here must hold in CI with no audio
    /// device.
    /// </summary>
    public class FishingAudioLogicTests
    {
        private const float Eps = 1e-4f;

        private static FishingState State(
            FishingPhase phase,
            float tension = 0f, float landing = 0f,
            float depth = 0f, bool slack = false, float bend = 0f,
            float fishX = 0f, float fishY = 0f)
            => new FishingState(phase, tension, landing, "fish.test", "Test Fish",
                                FishCategory.InshoreGroundfish, 1f, depth, slack, bend, fishX, fishY);

        // ---- phase → cue table --------------------------------------------------------------

        [Test]
        public void PhaseCue_EnteringCast_FiresTheWhoosh()
        {
            Assert.AreEqual(FishingCue.CastWhoosh,
                FishingAudioLogic.PhaseCue(FishingPhase.WindBack, State(FishingPhase.Cast)),
                "the flick released — whip whoosh + line whistle (§2.2)");
        }

        [Test]
        public void PhaseCue_LeavingCast_IsTheSplashDown()
        {
            Assert.AreEqual(FishingCue.SplashDown,
                FishingAudioLogic.PhaseCue(FishingPhase.Cast, State(FishingPhase.Waiting)),
                "cast → waiting: the line landed");
            Assert.AreEqual(FishingCue.SplashDown,
                FishingAudioLogic.PhaseCue(FishingPhase.Cast, State(FishingPhase.Sinking)),
                "cast → sinking: the line landed and the rig starts down");
        }

        [Test]
        public void PhaseCue_SamePhaseTick_IsSilent()
        {
            // The fight publishes every tick — the continuous layers carry those; a tick with no
            // phase change must never re-fire a one-shot.
            Assert.AreEqual(FishingCue.None,
                FishingAudioLogic.PhaseCue(FishingPhase.FightDeep, State(FishingPhase.FightDeep, tension: 0.8f)));
            Assert.AreEqual(FishingCue.None,
                FishingAudioLogic.PhaseCue(FishingPhase.Cast, State(FishingPhase.Cast)));
        }

        [Test]
        public void PhaseCue_BiteTell_BranchesOnTheTacklePath()
        {
            // §2.1: the CAST path's tell is the bobber (surface, Depth01 = 0) — the plop; the DEPTH
            // path has no bobber (rig down the column, Depth01 > 0) — the deep rod-tip KNOCK.
            Assert.AreEqual(FishingCue.BobberPlop,
                FishingAudioLogic.PhaseCue(FishingPhase.Waiting, State(FishingPhase.Bite, depth: 0f)),
                "surface bite → the bobber plop/dip");
            Assert.AreEqual(FishingCue.RodKnock,
                FishingAudioLogic.PhaseCue(FishingPhase.Waiting, State(FishingPhase.Bite, depth: 0.6f)),
                "a bite with the rig down the column → the rod-tip knock (audio/feel tell)");
        }

        [Test]
        public void PhaseCue_ResultBeats_SnapAndLanded()
        {
            Assert.AreEqual(FishingCue.SnapSting,
                FishingAudioLogic.PhaseCue(FishingPhase.FightSurface, State(FishingPhase.Snapped)),
                "threw the hook — the cozy sting");
            Assert.AreEqual(FishingCue.LandedFlourish,
                FishingAudioLogic.PhaseCue(FishingPhase.FightSurface, State(FishingPhase.Landed)),
                "she's aboard — flourish + wet slap");
            Assert.AreEqual(FishingCue.SnapSting,
                FishingAudioLogic.PhaseCue(FishingPhase.Fighting, State(FishingPhase.Snapped)),
                "the legacy fight snaps through the same table");
        }

        [Test]
        public void PhaseCue_QuietTransitions_StayQuiet()
        {
            Assert.AreEqual(FishingCue.None,
                FishingAudioLogic.PhaseCue(FishingPhase.Idle, State(FishingPhase.WindBack)),
                "the wind-back is voiced by the creak LOOP, not a one-shot");
            Assert.AreEqual(FishingCue.None,
                FishingAudioLogic.PhaseCue(FishingPhase.Bite, State(FishingPhase.FightDeep)),
                "hooking up flows into the fight layers, no extra sting");
            Assert.AreEqual(FishingCue.None,
                FishingAudioLogic.PhaseCue(FishingPhase.Landed, State(FishingPhase.Idle)),
                "putting the rod down is silent");
        }

        // ---- slack-window edges -------------------------------------------------------------

        [Test]
        public void SlackCue_BottomTell_WhenTheRigSettlesPreBite()
        {
            // §2.3: the pre-bite slack edge IS the "you felt bottom" read — the line settles.
            var prev = State(FishingPhase.Sinking, depth: 0.95f, slack: false);
            var next = State(FishingPhase.Sinking, depth: 1f, slack: true);
            Assert.AreEqual(FishingCue.BottomSettle, FishingAudioLogic.SlackCue(prev, next));

            var prevW = State(FishingPhase.Waiting, depth: 1f, slack: false);
            var nextW = State(FishingPhase.Waiting, depth: 1f, slack: true);
            Assert.AreEqual(FishingCue.BottomSettle, FishingAudioLogic.SlackCue(prevW, nextW),
                "the bottomed-out Waiting hold gets the same settle note");
        }

        [Test]
        public void SlackCue_PullNowRelease_WhenTheWindowOpensMidFight()
        {
            var prev = State(FishingPhase.FightDeep, tension: 0.7f, slack: false);
            var next = State(FishingPhase.FightDeep, tension: 0.3f, slack: true);
            Assert.AreEqual(FishingCue.SlackRelease, FishingAudioLogic.SlackCue(prev, next),
                "deep fight: she went slack — the diegetic PULL-now moment (§3)");

            var prevS = State(FishingPhase.FightSurface, slack: false);
            var nextS = State(FishingPhase.FightSurface, slack: true);
            Assert.AreEqual(FishingCue.SlackRelease, FishingAudioLogic.SlackCue(prevS, nextS),
                "surface fight: same release cue");
        }

        [Test]
        public void SlackCue_OnlyTheRisingEdgeSpeaks()
        {
            var open = State(FishingPhase.FightDeep, slack: true);
            Assert.AreEqual(FishingCue.None, FishingAudioLogic.SlackCue(open, open),
                "a held-open window does not re-cue every tick");

            var closed = State(FishingPhase.FightDeep, slack: false);
            Assert.AreEqual(FishingCue.None, FishingAudioLogic.SlackCue(open, closed),
                "the window CLOSING is read through the strain layer, not a cue");
            Assert.AreEqual(FishingCue.None, FishingAudioLogic.SlackCue(closed, closed));
        }

        [Test]
        public void SlackCue_LegacyFightingWouldGetTheSameRelease()
        {
            // The Core contract says SlackWindowOpen is always false in the legacy fight — but if a
            // publisher ever opens it there, the phase is grouped by IsFightPhase, so it voices the
            // same PULL-now release (no special casing, per the phase-mapping rule).
            var prev = State(FishingPhase.Fighting, slack: false);
            var next = State(FishingPhase.Fighting, slack: true);
            Assert.AreEqual(FishingCue.SlackRelease, FishingAudioLogic.SlackCue(prev, next));
        }

        // ---- continuous mix: wind-back creak ------------------------------------------------

        [Test]
        public void Mix_Creak_OnlyDuringWindBack_AndDeepensWithTheDraw()
        {
            var idle = FishingAudioLogic.MixFor(State(FishingPhase.Idle), 0f, 0f);
            Assert.AreEqual(0f, idle.CreakGain, Eps, "no creak at rest");

            var light = FishingAudioLogic.MixFor(State(FishingPhase.WindBack, bend: 0f), 0f, 0f);
            var deep  = FishingAudioLogic.MixFor(State(FishingPhase.WindBack, bend: 1f), 0f, 0f);
            Assert.AreEqual(FishingAudioLogic.CreakFloorGain, light.CreakGain, Eps,
                "the creak is audible from the first degree of draw (a floor, not silence)");
            Assert.AreEqual(1f, deep.CreakGain, Eps, "a full draw creaks at full voice");
            Assert.Greater(deep.CreakGain, light.CreakGain, "more draw → deeper creak");

            var fight = FishingAudioLogic.MixFor(State(FishingPhase.FightDeep, bend: 1f), 0f, 0f);
            Assert.AreEqual(0f, fight.CreakGain, Eps, "the fight does not creak — the strain groan owns it");
        }

        // ---- continuous mix: sinking pay-out ------------------------------------------------

        [Test]
        public void Mix_Payout_OnlyWhileSinking_AndSlowsTowardTheFloor()
        {
            var surface = FishingAudioLogic.MixFor(State(FishingPhase.Sinking, depth: 0f), 0f, 0f);
            var floor   = FishingAudioLogic.MixFor(State(FishingPhase.Sinking, depth: 1f), 0f, 0f);
            Assert.AreEqual(1f, surface.PayoutGain, Eps, "the pay-out ticks while the rig sinks");
            Assert.AreEqual(1f, surface.PayoutPitch, Eps, "full tick speed at the surface");
            Assert.AreEqual(FishingAudioLogic.PayoutPitchFloor, floor.PayoutPitch, Eps,
                "the tick slows right down as the rig reaches the floor — the no-gauge depth read (§2.3)");

            float prevPitch = float.MaxValue;
            for (float d = 0f; d <= 1f + Eps; d += 0.1f)
            {
                float pitch = FishingAudioLogic.MixFor(State(FishingPhase.Sinking, depth: d), 0f, 0f).PayoutPitch;
                Assert.Less(pitch, prevPitch + Eps, $"deeper must never tick FASTER (at depth {d})");
                prevPitch = pitch;
            }

            var waiting = FishingAudioLogic.MixFor(State(FishingPhase.Waiting, depth: 0.5f), 0f, 0f);
            Assert.AreEqual(0f, waiting.PayoutGain, Eps, "holding a band is quiet — nothing is paying out");
        }

        // ---- continuous mix: the strain groan (the "ease off!" voice) -----------------------

        [Test]
        public void Mix_Strain_SilentOutsideAFight_FullVoiceAtSnapPoint()
        {
            Assert.AreEqual(0f, FishingAudioLogic.MixFor(State(FishingPhase.Waiting, tension: 0.9f), 0f, 0f).StrainGain,
                Eps, "tension outside a fight phase is not voiced");
            Assert.AreEqual(0f, FishingAudioLogic.MixFor(State(FishingPhase.FightDeep, tension: 0f), 0f, 0f).StrainGain,
                Eps, "a slack line does not groan");
            Assert.AreEqual(1f, FishingAudioLogic.MixFor(State(FishingPhase.FightDeep, tension: 1f), 0f, 0f).StrainGain,
                Eps, "at the snap point the groan is at full voice");
        }

        [Test]
        public void Mix_Strain_IsContinuousAndSwellsHardestNearTheSnap()
        {
            // Continuous, not stepped: strictly increasing in tension, and (the >1 exponent) quieter
            // than linear through the safe band so the urgency lives near the top.
            float prev = -1f;
            for (float t = 0f; t <= 1f + Eps; t += 0.05f)
            {
                float g = FishingAudioLogic.MixFor(State(FishingPhase.FightSurface, tension: t), 0f, 0f).StrainGain;
                Assert.Greater(g, prev, $"the groan must swell monotonically with tension (at {t})");
                prev = g;
            }
            float half = FishingAudioLogic.MixFor(State(FishingPhase.FightDeep, tension: 0.5f), 0f, 0f).StrainGain;
            Assert.Less(half, 0.5f, "mid-tension whispers (< linear) — the voice is urgent only near the snap");
        }

        [Test]
        public void Mix_Strain_TightensInPitchWithTension()
        {
            var slack = FishingAudioLogic.MixFor(State(FishingPhase.FightDeep, tension: 0f), 0f, 0f);
            var taut  = FishingAudioLogic.MixFor(State(FishingPhase.FightDeep, tension: 1f), 0f, 0f);
            Assert.AreEqual(1f, slack.StrainPitch, Eps);
            Assert.AreEqual(1f + FishingAudioLogic.StrainPitchLift, taut.StrainPitch, Eps,
                "the line sounds literally tighter as it nears parting");
        }

        [Test]
        public void Mix_Strain_ServicesTheLegacyFightUnchanged()
        {
            // The VS-13 Fighting phase rides the SAME layers via IsFightPhase — no special casing.
            var legacy = FishingAudioLogic.MixFor(State(FishingPhase.Fighting, tension: 0.8f, landing: 0.4f), 0.1f, 0f);
            var v2     = FishingAudioLogic.MixFor(State(FishingPhase.FightDeep, tension: 0.8f, landing: 0.4f), 0.1f, 0f);
            Assert.AreEqual(v2.StrainGain, legacy.StrainGain, Eps, "same groan");
            Assert.AreEqual(v2.ReelGain, legacy.ReelGain, Eps, "same reel-on-gain");
        }

        // ---- continuous mix: reel clicks on gain --------------------------------------------

        [Test]
        public void Mix_Reel_SpeaksOnlyWhileGaining()
        {
            var still = FishingAudioLogic.MixFor(State(FishingPhase.FightDeep), 0f, 0f);
            Assert.AreEqual(0f, still.ReelGain, Eps, "no gain → no clicks");

            var half = FishingAudioLogic.MixFor(State(FishingPhase.FightDeep),
                FishingAudioLogic.ReelFullLandingPerSec * 0.5f, 0f);
            var full = FishingAudioLogic.MixFor(State(FishingPhase.FightDeep),
                FishingAudioLogic.ReelFullLandingPerSec, 0f);
            var over = FishingAudioLogic.MixFor(State(FishingPhase.FightDeep),
                FishingAudioLogic.ReelFullLandingPerSec * 3f, 0f);
            Assert.AreEqual(0.5f, half.ReelGain, Eps, "half rate → half voice");
            Assert.AreEqual(1f, full.ReelGain, Eps);
            Assert.AreEqual(1f, over.ReelGain, Eps, "clamped — a burst can't blow the mix");

            var sinking = FishingAudioLogic.MixFor(State(FishingPhase.Sinking), 0.5f, 0f);
            Assert.AreEqual(0f, sinking.ReelGain, Eps, "the sink is the pay-out's voice, not the reel's");
        }

        [Test]
        public void LandingPerSec_RisingOnly_AndGuardsDegenerateDt()
        {
            Assert.AreEqual(0.2f, FishingAudioLogic.LandingPerSec(0.4f, 0.5f, 0.5f), Eps, "gaining reads as a rate");
            Assert.AreEqual(0f, FishingAudioLogic.LandingPerSec(0.5f, 0.4f, 0.5f), Eps,
                "losing ground is the strain's story, not the reel's");
            Assert.AreEqual(0f, FishingAudioLogic.LandingPerSec(0f, 1f, 0f), Eps, "dt=0 → silent, not infinite");
            Assert.AreEqual(0f, FishingAudioLogic.LandingPerSec(0f, 1f, -1f), Eps, "negative dt → silent");
        }

        // ---- continuous mix: the surface thrash ---------------------------------------------

        [Test]
        public void Mix_Thrash_OnlyOnceSheIsUp_AndSwellsWithTheFight()
        {
            var deep = FishingAudioLogic.MixFor(State(FishingPhase.FightDeep, bend: 1f), 0f, 1f);
            Assert.AreEqual(0f, deep.ThrashGain, Eps, "unseen at depth — no surface thrash yet (§3)");

            var calm = FishingAudioLogic.MixFor(State(FishingPhase.FightSurface), 0f, 0f);
            var hard = FishingAudioLogic.MixFor(State(FishingPhase.FightSurface, bend: 1f), 0f, 1f);
            Assert.AreEqual(FishingAudioLogic.ThrashFloorGain, calm.ThrashGain, Eps,
                "she's up: the churn is audible even between darts");
            Assert.Greater(hard.ThrashGain, calm.ThrashGain, "harder darts + a loaded rod → louder thrash");
            Assert.LessOrEqual(hard.ThrashGain, 1f, "clamped");
        }

        [Test]
        public void Mix_Thrash_PansOnTheFishAndClamps()
        {
            float halfRange = FishingAudioLogic.ThrashPanRangeMetres * 0.5f;
            var left  = FishingAudioLogic.MixFor(State(FishingPhase.FightSurface, fishX: -halfRange), 0f, 0f);
            var right = FishingAudioLogic.MixFor(State(FishingPhase.FightSurface, fishX: halfRange), 0f, 0f);
            var far   = FishingAudioLogic.MixFor(State(FishingPhase.FightSurface, fishX: 99f), 0f, 0f);
            Assert.AreEqual(-0.5f, left.ThrashPan, Eps, "she darts left, the thrash leans left");
            Assert.AreEqual(0.5f, right.ThrashPan, Eps);
            Assert.AreEqual(1f, far.ThrashPan, Eps, "clamped at full pan");
        }

        [Test]
        public void DartSpeed01_NormalizesAndGuards()
        {
            Assert.AreEqual(0f, FishingAudioLogic.DartSpeed01(0f, 0f, 0.1f), Eps, "at rest");
            Assert.AreEqual(1f, FishingAudioLogic.DartSpeed01(FishingAudioLogic.DartFullSpeedMs, 0f, 1f), Eps,
                "a full-speed dart reads full");
            Assert.AreEqual(1f, FishingAudioLogic.DartSpeed01(99f, 99f, 0.1f), Eps, "clamped");
            Assert.AreEqual(0f, FishingAudioLogic.DartSpeed01(5f, 5f, 0f), Eps,
                "a phase edge after an idle gap (dt=0) must not read as an infinite dart");

            float diag = FishingAudioLogic.DartSpeed01(0.3f, 0.4f, 1f);   // |(0.3, 0.4)| = 0.5 m/s
            Assert.AreEqual(0.5f / FishingAudioLogic.DartFullSpeedMs, diag, Eps, "speed is the vector magnitude");
        }

        // ---- idle is silent all the way down ------------------------------------------------

        [Test]
        public void Mix_Idle_IsSilence()
        {
            var m = FishingAudioLogic.MixFor(FishingState.Idle, 0f, 0f);
            Assert.AreEqual(0f, m.CreakGain, Eps);
            Assert.AreEqual(0f, m.PayoutGain, Eps);
            Assert.AreEqual(0f, m.StrainGain, Eps);
            Assert.AreEqual(0f, m.ReelGain, Eps);
            Assert.AreEqual(0f, m.ThrashGain, Eps);
        }
    }
}
