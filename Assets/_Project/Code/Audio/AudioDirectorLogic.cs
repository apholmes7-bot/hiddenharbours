using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Audio
{
    /// <summary>One-shot cues fired by discrete game moments.</summary>
    public enum AudioCue
    {
        None = 0,
        CatchSting,   // a fish lands (FishCaught)
        HomeWarmth,   // sold the catch / made it back ashore (CatchSold / disembark)
    }

    /// <summary>The discrete game moments the director turns into a cue.</summary>
    public enum AudioMoment
    {
        FishLanded,   // FishCaught
        CatchSold,    // CatchSold
        CameAshore,   // ControlModeChanged Aboard -> OnFoot ("made it home")
    }

    /// <summary>The three independent mix buses, each with its own player-set volume.</summary>
    public enum AudioBus { Ambience, Sfx, Music }

    /// <summary>
    /// The PURE, engine-light decision logic for the adaptive audio director (VS-27/28). All the
    /// "what should we hear" maths live here — no AudioSources, no events — so they are EditMode
    /// testable and the MonoBehaviour stays a thin player.
    ///
    /// <para>Pillar 1: the rising-wind TELL must be audible BEFORE the sea turns dangerous, and the
    /// calm bed THINS to make room for it.</para>
    /// </summary>
    public static class AudioDirectorLogic
    {
        // ---- rising-wind tell (P1) ----------------------------------------------------------
        // Wind speed (m/s) where the warning STARTS to rise and where it's at FULL voice. The onset
        // sits well below the sea's danger band (the dory works safely to a 'Lively' sea), so the tell
        // is heard while there is still time to run for harbour — audible BEFORE trouble.
        public const float TellOnsetMs   = 8f;    // ~Beaufort 5 (fresh breeze) — the sea is getting lively
        public const float TellFullMs    = 16f;   // ~Beaufort 7 (near gale) — real trouble
        public const float TellReleaseMs = 6.5f;  // hysteresis: must drop below this to go quiet again

        // How far the calm ambient bed thins at a full tell (1 = untouched, floor = thinnest).
        public const float CalmBedFloor = 0.35f;

        /// <summary>0..1 intensity of the rising-wind tell for a wind speed (m/s). Ramps from
        /// <see cref="TellOnsetMs"/> to <see cref="TellFullMs"/>; flat below/above.</summary>
        public static float WindTell01(float windSpeedMs)
        {
            if (windSpeedMs <= TellOnsetMs) return 0f;
            if (windSpeedMs >= TellFullMs)  return 1f;
            return (windSpeedMs - TellOnsetMs) / (TellFullMs - TellOnsetMs);
        }

        /// <summary>Whether the tell is sounding, with on/off hysteresis so a gusty boundary doesn't
        /// chatter: it rises at <see cref="TellOnsetMs"/> and only goes quiet below
        /// <see cref="TellReleaseMs"/>.</summary>
        public static bool TellActive(float windSpeedMs, bool currentlyActive)
            => currentlyActive ? windSpeedMs > TellReleaseMs : windSpeedMs >= TellOnsetMs;

        /// <summary>Gain for the calm ambient bed given the tell intensity — it THINS as the wind
        /// rises so the warning reads through (P1).</summary>
        public static float CalmBedGain(float windTell01)
            => Mathf.Lerp(1f, CalmBedFloor, Mathf.Clamp01(windTell01));

        // ---- event -> cue map ---------------------------------------------------------------

        /// <summary>The cue a discrete game moment should fire.</summary>
        public static AudioCue CueFor(AudioMoment moment) => moment switch
        {
            AudioMoment.FishLanded => AudioCue.CatchSting,
            AudioMoment.CatchSold  => AudioCue.HomeWarmth,
            AudioMoment.CameAshore => AudioCue.HomeWarmth,
            _                      => AudioCue.None,
        };

        // ---- aboard layer + cue ducking -----------------------------------------------------

        /// <summary>The hull-slap/row layer plays only while aboard.</summary>
        public static bool HullLayerActive(ControlMode mode) => mode == ControlMode.Aboard;

        /// <summary>How much of the bed a cue removes at full duck (0..1), and how fast it recovers.</summary>
        public const float DuckDepth = 0.6f;
        public const float DuckRecoveryPerSec = 2.5f;

        /// <summary>Bed gain after ducking: a live cue pulls the ambience/music down under it.</summary>
        public static float DuckedGain(float baseGain, float duck01)
            => baseGain * (1f - DuckDepth * Mathf.Clamp01(duck01));
    }
}
