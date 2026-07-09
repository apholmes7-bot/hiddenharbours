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

    /// <summary>Which aboard propulsion bed is heard: the hand-rowed oar/water bed, the looping
    /// outboard-engine bed, or none (ashore).</summary>
    public enum BoatAudioLayer { None, Oars, Engine }

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

        // ---- "made it home" warmth is EARNED, not constant (P5) -----------------------------
        // The home-exhale resolves only when the trip was worth exhaling about. Coming ashore is the
        // SAME disembark whether you crossed a building blow or hopped to the next beach in flat calm —
        // so an unconditional warmth on every disembark cheapens the cue (charter guardrail: "warmth is
        // earned, not constant"; bible §8.3 "the home exhale"). We gate it on whether the SEA had become
        // a worry while you were out: if the rising-wind tell ever rose past a small threshold this trip,
        // coming ashore reads as "the sea warned me → I made it" and the warmth lands; a calm pootle ends
        // quietly. The peak tell is tracked aboard (the director already polls the tell at 4 Hz) and reset
        // each time you board, so the gate is per-trip. (A SALE — CatchSold — still warms unconditionally;
        // that's a reward beat, not the home-exhale, and arguably wants its own cue — flagged in the
        // manifest.) Reading the trip's peak tell needs no new signal: it falls out of the wind poll.

        /// <summary>How worrying the sea must have gotten this trip (peak rising-wind tell, 0..1) for the
        /// home-exhale to be earned on coming ashore. Sits low: any real freshening counts — but a flat
        /// calm hop does not. A SMALL non-zero value so the gate is "the wind picked up at all", not
        /// "it was a full gale".</summary>
        public const float HomeWarmthTellThreshold = 0.15f;

        /// <summary>
        /// Whether coming ashore should resolve to "made it home" warmth, given the PEAK rising-wind tell
        /// (0..1) experienced during the just-ended trip. The warmth is earned only if the sea had become a
        /// worry — i.e. the peak tell reached <see cref="HomeWarmthTellThreshold"/>. A calm trip ends quietly.
        /// Pure and deterministic — no AudioSources, no clock.
        /// </summary>
        public static bool HomeWarmthOnAshore(float peakTell01)
            => Mathf.Clamp01(peakTell01) >= HomeWarmthTellThreshold;

        // ---- aboard layer + cue ducking -----------------------------------------------------

        /// <summary>The hull-slap/row layer plays only while AT THE HELM (piloting). Standing on the
        /// deck (Build 5 <see cref="ControlMode.OnDeck"/>) is quiet — nobody's rowing/driving.</summary>
        public static bool HullLayerActive(ControlMode mode) => mode == ControlMode.Aboard;

        /// <summary>
        /// True while the player is physically ON THE BOAT — on deck or at the helm (Build 5 split the
        /// old binary Aboard). This is the "a trip is in progress" read: the wind-worry high-water mark
        /// accrues and the came-ashore beat resolves against BEING ON THE BOAT, not just piloting —
        /// stepping from the helm to the deck is NOT coming ashore.
        /// </summary>
        public static bool IsOnBoat(ControlMode mode)
            => mode == ControlMode.Aboard || mode == ControlMode.OnDeck;

        // ---- aboard propulsion bed: Dory oars vs Punt engine --------------------------------
        // The boat you hear depends on how the active hull is driven: the hand-rowed dory gets an
        // oar-stroke/water bed; an engine boat gets a looping outboard bed. The two crossfade on a
        // swap. Both ride the AMBIENCE bus (they're the aboard soundscape, not one-shot SFX).

        /// <summary>
        /// Stable id of the starting hand-rowed dory (mirrors <c>BoatHullDef.Id</c> content). The dory
        /// is the ONLY oar-driven hull — P4 "Earn It Then Automate It": every boat you BUY (the punt and
        /// up) runs an engine.
        /// <para>FLAGGED v1: we anchor on the id because <see cref="ActiveBoatChanged"/> carries the
        /// BoatId but NOT a propulsion type — that enum lives in the Boats module, which the Audio lane
        /// must not reference (asmdef is Core-only; CLAUDE.md rule 4). The robust fix is a Core
        /// propulsion field on <see cref="ActiveBoatChanged"/> / <c>IActiveBoatService</c>, which is a
        /// Boats/Player + Core change outside this lane.</para>
        /// </summary>
        public const string DoryBoatId = "boat.dory";

        /// <summary>
        /// Pick the aboard propulsion bed from control mode + the active hull's stable id. Ashore → None.
        /// Aboard: the dory (and any unknown/empty id — the cosy starter default) rows; every other hull
        /// is an engine boat.
        /// </summary>
        public static BoatAudioLayer BoatLayerFor(ControlMode mode, string boatId)
        {
            if (mode != ControlMode.Aboard) return BoatAudioLayer.None;
            bool engine = !string.IsNullOrEmpty(boatId)
                          && !string.Equals(boatId, DoryBoatId, System.StringComparison.Ordinal);
            return engine ? BoatAudioLayer.Engine : BoatAudioLayer.Oars;
        }

        /// <summary>How fast the oar/engine beds crossfade on a boat swap (gain units/sec; ~0.3 s).</summary>
        public const float BoatLayerCrossfadePerSec = 3f;

        // ---- engine bed is speed-reactive (uses the Core IActiveBoatService SOG read) -------
        // No throttle signal is exposed, but course-over-ground speed IS (BoatKinematics.SpeedOverGround),
        // so the outboard idles when moored/slow and swells + lifts in pitch underway. A fair v1 proxy.

        public const float EngineIdleSpeedMs  = 0.2f;  // moored / barely moving → idle rumble
        public const float EngineFullSpeedMs  = 4.0f;  // a small outboard punt's working speed
        public const float EngineIdleGainFrac = 0.55f; // idle loudness as a fraction of the full bed
        public const float EngineIdlePitch    = 0.85f;
        public const float EngineFullPitch    = 1.25f;

        /// <summary>0..1 throttle proxy from speed over ground (idle→full), clamped.</summary>
        public static float EngineThrottle01(float speedOverGroundMs)
        {
            if (EngineFullSpeedMs <= EngineIdleSpeedMs) return 0f;
            return Mathf.Clamp01((speedOverGroundMs - EngineIdleSpeedMs) / (EngineFullSpeedMs - EngineIdleSpeedMs));
        }

        /// <summary>Engine bed gain: idles quietly, swells to the full bed level underway.</summary>
        public static float EngineGain(float baseGain, float throttle01)
            => baseGain * Mathf.Lerp(EngineIdleGainFrac, 1f, Mathf.Clamp01(throttle01));

        /// <summary>Engine pitch: lifts with throttle so revs read as speed.</summary>
        public static float EnginePitch(float throttle01)
            => Mathf.Lerp(EngineIdlePitch, EngineFullPitch, Mathf.Clamp01(throttle01));

        /// <summary>How much of the bed a cue removes at full duck (0..1), and how fast it recovers.</summary>
        public const float DuckDepth = 0.6f;
        public const float DuckRecoveryPerSec = 2.5f;

        /// <summary>Bed gain after ducking: a live cue pulls the ambience/music down under it.</summary>
        public static float DuckedGain(float baseGain, float duck01)
            => baseGain * (1f - DuckDepth * Mathf.Clamp01(duck01));
    }
}
