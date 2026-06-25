using NUnit.Framework;
using HiddenHarbours.Core;
using HiddenHarbours.Audio;

namespace HiddenHarbours.Tests.Audio
{
    /// <summary>
    /// VS-27/28 — the adaptive audio director's PURE decision logic. Pins the SACRED rising-wind tell
    /// (it must be audible BEFORE the sea turns dangerous, and the calm bed thins to make room — P1),
    /// the event→cue map, the aboard layer, and cue ducking. No AudioSources, no Unity scene.
    /// </summary>
    public class AudioDirectorLogicTests
    {
        private const float Eps = 1e-4f;

        // ---- rising-wind tell (P1) ----------------------------------------------------------

        [Test]
        public void WindTell01_RampsFromOnsetToFull()
        {
            Assert.AreEqual(0f, AudioDirectorLogic.WindTell01(0f), Eps, "dead calm is silent");
            Assert.AreEqual(0f, AudioDirectorLogic.WindTell01(AudioDirectorLogic.TellOnsetMs), Eps,
                "at onset the tell is still silent (it starts to rise just past it)");
            Assert.AreEqual(1f, AudioDirectorLogic.WindTell01(AudioDirectorLogic.TellFullMs), Eps,
                "at the full wind the tell is at full voice");
            Assert.AreEqual(1f, AudioDirectorLogic.WindTell01(40f), Eps, "and it holds at full above that");

            float mid = 0.5f * (AudioDirectorLogic.TellOnsetMs + AudioDirectorLogic.TellFullMs);
            Assert.AreEqual(0.5f, AudioDirectorLogic.WindTell01(mid), Eps, "halfway is half voice");
        }

        [Test]
        public void WindTell_IsAudibleBeforeTrouble()
        {
            // The whole point (P1): the warning is already rising at a wind well BELOW the "real
            // trouble" level, so the player hears it with time to run for harbour.
            float earlyWarning = AudioDirectorLogic.TellOnsetMs + 2f; // a touch past onset, far below full
            float warn = AudioDirectorLogic.WindTell01(earlyWarning);

            Assert.Greater(warn, 0f, "the tell must already be sounding before the sea is dangerous");
            Assert.Less(warn, 1f, "but not yet at full — there's headroom as it worsens");
            Assert.Less(earlyWarning, AudioDirectorLogic.TellFullMs, "precondition: that wind is below trouble");
        }

        [Test]
        public void WindTell01_IsMonotonicNonDecreasing()
        {
            float prev = -1f;
            for (float w = 0f; w <= 24f; w += 0.5f)
            {
                float v = AudioDirectorLogic.WindTell01(w);
                Assert.GreaterOrEqual(v, prev, $"the tell must never quieten as the wind rises (at {w} m/s)");
                prev = v;
            }
        }

        [Test]
        public void TellActive_HasHysteresis_NoChatterAtTheBoundary()
        {
            // Rises only at/after onset.
            Assert.IsTrue(AudioDirectorLogic.TellActive(AudioDirectorLogic.TellOnsetMs, false), "rises at onset");
            Assert.IsFalse(AudioDirectorLogic.TellActive(AudioDirectorLogic.TellOnsetMs - 0.1f, false),
                "stays quiet just below onset");

            // Once on, it lingers down to the lower release threshold (no flicker).
            Assert.IsTrue(AudioDirectorLogic.TellActive(AudioDirectorLogic.TellReleaseMs + 0.1f, true),
                "stays on above the release threshold");
            Assert.IsFalse(AudioDirectorLogic.TellActive(AudioDirectorLogic.TellReleaseMs, true),
                "only goes quiet at/below the release threshold");

            Assert.Less(AudioDirectorLogic.TellReleaseMs, AudioDirectorLogic.TellOnsetMs,
                "release is below onset — that gap IS the hysteresis");
        }

        [Test]
        public void CalmBed_ThinsAsTheTellRises()
        {
            Assert.AreEqual(1f, AudioDirectorLogic.CalmBedGain(0f), Eps, "no wind → full bed");
            Assert.AreEqual(AudioDirectorLogic.CalmBedFloor, AudioDirectorLogic.CalmBedGain(1f), Eps,
                "full tell → bed thinned to its floor");
            Assert.Less(AudioDirectorLogic.CalmBedGain(1f), AudioDirectorLogic.CalmBedGain(0.5f),
                "more wind → thinner bed");
            Assert.Less(AudioDirectorLogic.CalmBedGain(0.5f), AudioDirectorLogic.CalmBedGain(0f),
                "the bed makes room for the warning (P1)");
        }

        // ---- event → cue map ----------------------------------------------------------------

        [Test]
        public void CueFor_MapsMomentsToCues()
        {
            Assert.AreEqual(AudioCue.CatchSting, AudioDirectorLogic.CueFor(AudioMoment.FishLanded),
                "a landed fish stings");
            Assert.AreEqual(AudioCue.HomeWarmth, AudioDirectorLogic.CueFor(AudioMoment.CatchSold),
                "a sale is 'made it home' warmth");
            Assert.AreEqual(AudioCue.HomeWarmth, AudioDirectorLogic.CueFor(AudioMoment.CameAshore),
                "coming ashore is the same warmth");
        }

        // ---- "made it home" warmth is EARNED, not constant (P5) -----------------------------

        [Test]
        public void HomeWarmth_DoesNotFireAfterACalmTrip()
        {
            // A flat-calm hop to the next beach should end QUIETLY — the home-exhale is not free.
            Assert.IsFalse(AudioDirectorLogic.HomeWarmthOnAshore(0f),
                "dead-calm trip: coming ashore does NOT swell to warmth");
            Assert.IsFalse(AudioDirectorLogic.HomeWarmthOnAshore(AudioDirectorLogic.HomeWarmthTellThreshold - 0.01f),
                "barely-a-breeze trip: still below the bar — quiet");
        }

        [Test]
        public void HomeWarmth_FiresWhenTheSeaHadBecomeAWorry()
        {
            // Coming in from a building blow IS the "the sea warned me → I made it" beat (P5).
            Assert.IsTrue(AudioDirectorLogic.HomeWarmthOnAshore(AudioDirectorLogic.HomeWarmthTellThreshold),
                "at the threshold the exhale is earned");
            Assert.IsTrue(AudioDirectorLogic.HomeWarmthOnAshore(1f),
                "coming in from a full blow definitely warms");
        }

        [Test]
        public void HomeWarmth_ThresholdIsLowButNonZero()
        {
            // The gate is "the wind picked up at all", not "it was a gale" — small, but not zero (else
            // every disembark fires and the cue is constant again).
            Assert.Greater(AudioDirectorLogic.HomeWarmthTellThreshold, 0f,
                "a non-zero bar is what makes the warmth earned");
            Assert.Less(AudioDirectorLogic.HomeWarmthTellThreshold, 0.5f,
                "but low enough that any real freshening counts, not just a gale");
        }

        [Test]
        public void HomeWarmth_IsMonotonic_WorseSeaNeverUnEarnsTheExhale()
        {
            // Determinism + sanity: a worse peak sea can never turn a YES into a NO.
            bool prev = false;
            for (float p = 0f; p <= 1f + Eps; p += 0.05f)
            {
                bool now = AudioDirectorLogic.HomeWarmthOnAshore(p);
                Assert.IsFalse(prev && !now, $"a worse sea must not un-earn the warmth (at peak {p})");
                prev = now;
            }
            Assert.IsTrue(prev, "by a full-tell trip the warmth is certainly earned");
        }

        [Test]
        public void HomeWarmth_ClampsNegativePeak()
        {
            // A peak should never be negative, but the gate must be robust if it is.
            Assert.IsFalse(AudioDirectorLogic.HomeWarmthOnAshore(-1f),
                "a nonsense negative peak reads as 'no worry' — quiet, not a crash");
        }

        // ---- aboard layer -------------------------------------------------------------------

        [Test]
        public void HullLayer_OnlyWhenAboard()
        {
            Assert.IsTrue(AudioDirectorLogic.HullLayerActive(ControlMode.Aboard), "hull/row plays aboard");
            Assert.IsFalse(AudioDirectorLogic.HullLayerActive(ControlMode.OnFoot), "silent ashore");
        }

        // ---- aboard propulsion bed: Dory oars vs Punt engine --------------------------------

        [Test]
        public void BoatLayer_NoneWhenAshore()
        {
            Assert.AreEqual(BoatAudioLayer.None,
                AudioDirectorLogic.BoatLayerFor(ControlMode.OnFoot, AudioDirectorLogic.DoryBoatId),
                "ashore there is no boat bed, even with a boat remembered");
            Assert.AreEqual(BoatAudioLayer.None,
                AudioDirectorLogic.BoatLayerFor(ControlMode.OnFoot, "boat.punt"));
        }

        [Test]
        public void BoatLayer_DoryRows_EngineBoatsRunEngines()
        {
            Assert.AreEqual(BoatAudioLayer.Oars,
                AudioDirectorLogic.BoatLayerFor(ControlMode.Aboard, AudioDirectorLogic.DoryBoatId),
                "the hand-rowed dory gets the oar/water bed");
            Assert.AreEqual(BoatAudioLayer.Engine,
                AudioDirectorLogic.BoatLayerFor(ControlMode.Aboard, "boat.punt"),
                "the punt is an engine boat → outboard bed");
            Assert.AreEqual(BoatAudioLayer.Engine,
                AudioDirectorLogic.BoatLayerFor(ControlMode.Aboard, "boat.cape_islander"),
                "every bought hull up the ladder runs an engine");
        }

        [Test]
        public void BoatLayer_UnknownAboardDefaultsToOars_TheCosyStarter()
        {
            Assert.AreEqual(BoatAudioLayer.Oars, AudioDirectorLogic.BoatLayerFor(ControlMode.Aboard, null),
                "a null id aboard falls back to the rowed starter, not a phantom engine");
            Assert.AreEqual(BoatAudioLayer.Oars, AudioDirectorLogic.BoatLayerFor(ControlMode.Aboard, ""));
        }

        [Test]
        public void BoatLayer_SoundsIffTheAboardHullLayerIsActive()
        {
            // The boat bed plays exactly when the aboard layer is active — they agree.
            foreach (var mode in new[] { ControlMode.OnFoot, ControlMode.Aboard })
                Assert.AreEqual(AudioDirectorLogic.HullLayerActive(mode),
                    AudioDirectorLogic.BoatLayerFor(mode, "boat.punt") != BoatAudioLayer.None,
                    $"layer-active and boat-bed-present must agree ({mode})");
        }

        // ---- engine bed: speed-reactive (course-over-ground proxy) --------------------------

        [Test]
        public void EngineThrottle01_RampsWithSpeed_Clamped()
        {
            Assert.AreEqual(0f, AudioDirectorLogic.EngineThrottle01(0f), Eps, "moored → idle");
            Assert.AreEqual(0f, AudioDirectorLogic.EngineThrottle01(-5f), Eps, "negative speed clamps to idle");
            Assert.AreEqual(1f, AudioDirectorLogic.EngineThrottle01(AudioDirectorLogic.EngineFullSpeedMs), Eps,
                "full revs at the working speed");
            Assert.AreEqual(1f, AudioDirectorLogic.EngineThrottle01(99f), Eps, "and holds at full above it");

            float mid = 0.5f * (AudioDirectorLogic.EngineIdleSpeedMs + AudioDirectorLogic.EngineFullSpeedMs);
            float t = AudioDirectorLogic.EngineThrottle01(mid);
            Assert.Greater(t, 0f); Assert.Less(t, 1f);
        }

        [Test]
        public void Engine_SwellsAndLiftsPitchWithThrottle()
        {
            Assert.AreEqual(AudioDirectorLogic.EngineIdleGainFrac, AudioDirectorLogic.EngineGain(1f, 0f), Eps,
                "idles at the idle fraction of the bed");
            Assert.AreEqual(1f, AudioDirectorLogic.EngineGain(1f, 1f), Eps, "full bed at full throttle");
            Assert.Less(AudioDirectorLogic.EngineGain(1f, 0f), AudioDirectorLogic.EngineGain(1f, 1f),
                "the engine swells underway");

            Assert.AreEqual(AudioDirectorLogic.EngineIdlePitch, AudioDirectorLogic.EnginePitch(0f), Eps);
            Assert.AreEqual(AudioDirectorLogic.EngineFullPitch, AudioDirectorLogic.EnginePitch(1f), Eps);
            Assert.Less(AudioDirectorLogic.EnginePitch(0f), AudioDirectorLogic.EnginePitch(1f),
                "revs lift the pitch with speed");
        }

        // ---- cue ducking --------------------------------------------------------------------

        [Test]
        public void DuckedGain_PullsBedsDownUnderACue()
        {
            Assert.AreEqual(1f, AudioDirectorLogic.DuckedGain(1f, 0f), Eps, "no cue → no duck");
            Assert.AreEqual(1f - AudioDirectorLogic.DuckDepth, AudioDirectorLogic.DuckedGain(1f, 1f), Eps,
                "full duck removes DuckDepth of the bed");
            Assert.Less(AudioDirectorLogic.DuckedGain(1f, 1f), AudioDirectorLogic.DuckedGain(1f, 0.5f),
                "more duck → quieter bed");
            Assert.AreEqual(0.8f * (1f - AudioDirectorLogic.DuckDepth),
                AudioDirectorLogic.DuckedGain(0.8f, 1f), Eps, "ducking scales the bus level, not replaces it");
        }
    }
}
