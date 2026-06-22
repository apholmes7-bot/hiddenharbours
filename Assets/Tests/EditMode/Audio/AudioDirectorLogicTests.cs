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

        // ---- aboard layer -------------------------------------------------------------------

        [Test]
        public void HullLayer_OnlyWhenAboard()
        {
            Assert.IsTrue(AudioDirectorLogic.HullLayerActive(ControlMode.Aboard), "hull/row plays aboard");
            Assert.IsFalse(AudioDirectorLogic.HullLayerActive(ControlMode.OnFoot), "silent ashore");
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
