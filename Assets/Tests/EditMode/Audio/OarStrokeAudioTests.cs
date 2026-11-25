using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Audio;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.Audio
{
    /// <summary>
    /// <b>The oars are heard on the stroke.</b> The old oar sound was a loop with a clock of its own
    /// (2.6 s a stroke) that played whether or not the oars moved; these pin the replacement: the boat's
    /// stroke clock drives the drawn oar, the oar splash fires its catch on the frame the blade goes in,
    /// publishes it through Core (<see cref="OarBladeWater"/>), and <see cref="OarStrokeVoicing"/> turns
    /// that into ONE catch sound a stroke, on that frame, at any stroke rate, and nothing while nobody rows.
    ///
    /// <para>The chain is the game's own: <see cref="RowStrokeClock"/> for the phase,
    /// <see cref="DoryOarMeshPose.Row"/> for the pose, <see cref="DoryOarSplashSystem"/> for the catch and
    /// <see cref="DoryOarSplashEmitter.PublishBladeWater"/> onto the real <see cref="EventBus"/>.</para>
    /// </summary>
    public class OarStrokeAudioTests
    {
        // The dory's own facts (as DoryOarSplashTests): oarlock z 0.58808 m over a 0.11 m resting draught.
        private const float LockHeight = 0.58808f - 0.11f;
        private const float Dt = 1f / 60f;
        private const int HullId = 7;

        // The casual rowing style's tempo (the one the owner's clip shows: about 1.78 s a stroke).
        private const float CasualTempo = 0.5f;

        private sealed class Run
        {
            public readonly List<int> CatchFrames = new List<int>();       // frames a catch SOUND started
            public readonly List<int> ReleaseFrames = new List<int>();     // frames a release SOUND started
            public readonly List<int> BladeEntryFrames = new List<int>();  // frames the port blade went under
            public readonly List<float> CatchPhases = new List<float>();   // the port oar's stroke phase there
            public readonly List<OarStrokeVoice> Voices = new List<OarStrokeVoice>();
            public int Published;
            public float StrokeSeconds;
        }

        /// <summary>
        /// Row <paramref name="frames"/> frames with both oars at <paramref name="effort"/> (or not at all
        /// when <paramref name="rowing"/> is false: the oars trail and the clock holds), and hear every
        /// published blade-water moment with a fresh voicing.
        /// </summary>
        private static Run RowAndListen(float effort, float tempoScale, int frames, bool rowing = true,
                                        OarStrokeAudioTuning tuning = null)
        {
            var cfg = OarSplashConfig.Default;
            var sys = new DoryOarSplashSystem(cfg.FoamPoolSize, cfg.DropletPoolSize);
            var voicing = new OarStrokeVoicing();
            tuning ??= OarStrokeAudioTuning.Default;
            var tempo = RowStrokeTempo.Default;
            var run = new Run();
            float fps = DoryOarMath.StrokeFramesPerSecond(tempo.FramesPerSecond * tempoScale, effort, tempo.EffortInfluence);
            run.StrokeSeconds = DoryOarMath.StrokeColumns / fps;

            // Start at the top of the recovery, blade clear of the water (as the splash tests do).
            var clock = new RowStrokeClock
            {
                PortPhase = DoryOarMath.StrokeColumns * 0.5f,
                StarboardPhase = DoryOarMath.StrokeColumns * 0.5f,
            };
            int frame = 0;
            float portPhaseNow = clock.PortPhase;
            void Hear(OarBladeWater e)
            {
                run.Published++;
                if (!voicing.TryVoice(in e, frame * (double)Dt, Vector2.zero, tuning, out OarStrokeVoice v)) return;
                run.Voices.Add(v);
                if (v.Catch)
                {
                    run.CatchFrames.Add(frame);
                    run.CatchPhases.Add(RowStrokeClock.StrokePhaseOfColumns(portPhaseNow));
                }
                else run.ReleaseFrames.Add(frame);
            }

            EventBus.Subscribe<OarBladeWater>(Hear);
            try
            {
                bool wasUnder = false;
                for (frame = 1; frame <= frames; frame++)
                {
                    float state = rowing ? effort : 0f;
                    clock.Step(state, state, Dt, in tempo, tempoScale);
                    portPhaseNow = clock.PortPhase;
                    var port = Frame(clock.PortPhase, rowing, effort, cfg);
                    var star = Frame(clock.StarboardPhase, rowing, effort, cfg);
                    bool under = DoryOarSplashMath.IsImmersed(port.BladeHeightMeters);
                    if (under && !wasUnder && frame > 1) run.BladeEntryFrames.Add(frame);
                    wasUnder = under;

                    sys.Step(in port, in star, 1f, Vector2.zero, Dt, in cfg);
                    DoryOarSplashEmitter.PublishBladeWater(sys, HullId);
                }
            }
            finally { EventBus.Unsubscribe<OarBladeWater>(Hear); }
            return run;
        }

        private static DoryOarSplashSystem.OarFrame Frame(float phaseColumns, bool stroking, float effort,
                                                          in OarSplashConfig cfg)
        {
            DoryOarMeshPose.Pose pose = DoryOarMeshPose.Row(phaseColumns / DoryOarMath.StrokeColumns);
            return new DoryOarSplashSystem.OarFrame
            {
                Stroking = stroking,
                BladeHeightMeters = DoryOarSplashMath.BladeHeightMeters(LockHeight, cfg.BladeReachMeters, pose.DipDegrees),
                BladeWorld = new Vector2(1f, 0f),
                Outboard = Vector2.right,
                Aft = Vector2.down,
                Effort01 = effort,
                StrokeDirection = 1f,
            };
        }

        private static int FramesFor(float seconds) => Mathf.RoundToInt(seconds / Dt);

        // ---- once a stroke ------------------------------------------------------------------------

        [Test]
        public void ACatchSound_FiresOncePerStroke_NotOncePerOar()
        {
            float stroke = RowAndListen(1f, CasualTempo, 1).StrokeSeconds;
            Run run = RowAndListen(1f, CasualTempo, Mathf.FloorToInt(6f * stroke / Dt));

            Assert.AreEqual(6, run.CatchFrames.Count, "six strokes rowed: six catch sounds, one a stroke");
            Assert.AreEqual(6, run.ReleaseFrames.Count, "and one release drip a stroke");
            Assert.AreEqual(4 * 6, run.Published, "both oars publish a catch and a release each stroke; " +
                "the second oar of the same stroke is merged into the first, not voiced twice");
        }

        [Test]
        public void NothingSounds_WhenNotRowing()
        {
            Run run = RowAndListen(1f, CasualTempo, FramesFor(10f), rowing: false);

            Assert.AreEqual(0, run.Published, "a boat whose oars are not being rowed publishes no blade moments");
            Assert.AreEqual(0, run.Voices.Count, "and nothing is heard: no loop, no stroke");
        }

        [Test]
        public void AnOarDrawnUnderButNotRowed_IsSilent()
        {
            // The blade pose still sweeps through the water, but the oar is not being rowed (trailing,
            // shipped, under power, aground): the splash's gate, and with it the sound's.
            var cfg = OarSplashConfig.Default;
            var sys = new DoryOarSplashSystem(cfg.FoamPoolSize, cfg.DropletPoolSize);
            int published = 0;
            void Count(OarBladeWater e) => published++;
            EventBus.Subscribe<OarBladeWater>(Count);
            try
            {
                float phase = DoryOarMath.StrokeColumns * 0.5f;
                for (int i = 0; i < FramesFor(5f); i++)
                {
                    phase = DoryOarMath.AdvanceStrokePhase(phase, 1f, DoryOarMath.DefaultStrokeFramesPerSecond, Dt,
                                                           DoryOarMath.StrokeColumns);
                    var f = Frame(phase, false, 1f, cfg);
                    sys.Step(in f, in f, 1f, Vector2.zero, Dt, in cfg);
                    DoryOarSplashEmitter.PublishBladeWater(sys, HullId);
                }
            }
            finally { EventBus.Unsubscribe<OarBladeWater>(Count); }
            Assert.AreEqual(0, published);
        }

        // ---- on the stroke ------------------------------------------------------------------------

        [Test]
        public void TheCatchSound_FollowsTheStrokePhase_AtTwoStrokeRates()
        {
            Run hard = RowAndListen(1f, CasualTempo, FramesFor(12f));
            Run gentle = RowAndListen(0.4f, CasualTempo, FramesFor(12f));

            Assert.Greater(gentle.StrokeSeconds, hard.StrokeSeconds * 1.2f, "the two runs really are different rates");

            foreach (Run run in new[] { hard, gentle })
            {
                Assert.GreaterOrEqual(run.CatchFrames.Count, 5);
                // Every catch sound lands on the very frame the drawn blade went under, and on no other.
                CollectionAssert.AreEqual(run.BladeEntryFrames, run.CatchFrames,
                    "each catch sound must start on the frame the blade enters the water");

                // ...so the gap between sounds is the stroke's own period, whatever the rate.
                float periodFrames = run.StrokeSeconds / Dt;
                for (int i = 1; i < run.CatchFrames.Count; i++)
                    Assert.AreEqual(periodFrames, run.CatchFrames[i] - run.CatchFrames[i - 1], 1.01f,
                        $"catch {i}: the sound follows the stroke ({run.StrokeSeconds:0.###} s), not a clock of its own");
            }

            // And it is the same point of the stroke at both rates: within one frame's travel of phase.
            float step = Mathf.Max(Dt / hard.StrokeSeconds, Dt / gentle.StrokeSeconds);
            float reference = hard.CatchPhases[0];
            foreach (float p in hard.CatchPhases) Assert.AreEqual(0f, Wrapped(p - reference), step + 1e-4f);
            foreach (float p in gentle.CatchPhases) Assert.AreEqual(0f, Wrapped(p - reference), step + 1e-4f);
        }

        private static float Wrapped(float d) => d - Mathf.Round(d);

        // ---- the variation ------------------------------------------------------------------------

        [Test]
        public void ThePerStrokeVariation_IsDeterministic_AndSmall()
        {
            var t = OarStrokeAudioTuning.Default;
            Run a = RowAndListen(1f, CasualTempo, FramesFor(15f), tuning: t);
            Run b = RowAndListen(1f, CasualTempo, FramesFor(15f), tuning: t);

            Assert.AreEqual(a.Voices.Count, b.Voices.Count);
            var pitches = new HashSet<float>();
            for (int i = 0; i < a.Voices.Count; i++)
            {
                Assert.AreEqual(a.Voices[i].Pitch, b.Voices[i].Pitch, 0f, "the same rowing sounds the same, every run");
                Assert.AreEqual(a.Voices[i].Gain, b.Voices[i].Gain, 0f);
                Assert.AreEqual(1f, a.Voices[i].Pitch, t.PitchVariation + 1e-6f, "pitch stays within its variation");
                if (a.Voices[i].Catch) pitches.Add(a.Voices[i].Pitch);
            }
            Assert.Greater(pitches.Count, 3, "but the strokes are not all identical");

            for (int s = 1; s < 200; s++)
            {
                float v = OarStrokeVoicing.Signed(t.Seed, s, 1);
                Assert.That(v, Is.InRange(-1f, 1f));
                Assert.AreEqual(v, OarStrokeVoicing.Signed(t.Seed, s, 1), 0f);
            }
        }

        // ---- who is heard -------------------------------------------------------------------------

        [Test]
        public void ARowerOutOfEarshot_IsNotHeard_AndANearOneIsAtFullVolume()
        {
            var t = OarStrokeAudioTuning.Default;
            var voicing = new OarStrokeVoicing();
            var far = new OarBladeWater(OarStrokePart.Catch, new Vector2(t.EarshotMetres + 1f, 0f), 1f, 1, 0, 1);
            Assert.IsFalse(voicing.TryVoice(in far, 0, Vector2.zero, t, out _), "beyond earshot: silent");

            var near = new OarBladeWater(OarStrokePart.Catch, new Vector2(1f, 0f), 1f, 2, 0, 1);
            Assert.IsTrue(voicing.TryVoice(in near, 0, Vector2.zero, t, out OarStrokeVoice v));
            Assert.AreEqual(t.CatchGain, v.Gain, t.CatchGain * t.GainVariation + 1e-5f);

            Assert.AreEqual(1f, OarStrokeVoicing.DistanceGain(t.FullVolumeMetres, t.FullVolumeMetres, t.EarshotMetres));
            Assert.AreEqual(0.5f, OarStrokeVoicing.DistanceGain(0.5f * (t.FullVolumeMetres + t.EarshotMetres),
                                                                t.FullVolumeMetres, t.EarshotMetres), 1e-5f);
        }

        [Test]
        public void TwoBoats_AreEachHeard_OnTheirOwnStrokes()
        {
            var t = OarStrokeAudioTuning.Default;
            var voicing = new OarStrokeVoicing();
            var mine = new OarBladeWater(OarStrokePart.Catch, Vector2.zero, 1f, 10, 0, 1);
            var theirs = new OarBladeWater(OarStrokePart.Catch, new Vector2(5f, 0f), 1f, 11, 0, 1);
            Assert.IsTrue(voicing.TryVoice(in mine, 1.0, Vector2.zero, t, out _));
            Assert.IsTrue(voicing.TryVoice(in theirs, 1.0, Vector2.zero, t, out _),
                "another rower's catch in the same instant is a different stroke, not the other oar");
        }
    }
}
