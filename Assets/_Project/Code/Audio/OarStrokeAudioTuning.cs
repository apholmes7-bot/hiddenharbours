using System;
using UnityEngine;

namespace HiddenHarbours.Audio
{
    /// <summary>
    /// How a rowed boat's strokes are heard (rule 6), owner-editable on the clip set
    /// (<c>Assets/_Project/Audio/Resources/AudioClipSet.asset</c>, block <c>OarStrokes</c>). Each stroke is
    /// played ON the stroke: one catch sound on the frame the blades go in (the Core
    /// <c>OarBladeWater</c> the oar splash publishes) and a lighter drip on the frame they come out. The
    /// two sounds are cut from the oar-stroke clip (the <c>HullRow</c> slot, or its procedural stand-in
    /// while that slot is empty), so the voice is the one the old loop had.
    ///
    /// <para>Nothing here is simulation: these numbers only shape what the player hears.</para>
    /// </summary>
    [Serializable]
    public sealed class OarStrokeAudioTuning
    {
        [Header("Level (on the ambience bus, like the old oar bed)")]
        [Tooltip("The catch-and-pull sound's level. The old looping bed played at 0.8; one stroke of the " +
                 "new sound at this gain matches one stroke of that bed.")]
        [Range(0f, 1f)] public float CatchGain = 0.8f;

        [Tooltip("The release drip's level.")]
        [Range(0f, 1f)] public float ReleaseGain = 0.8f;

        [Tooltip("A light pull's share of the full level: the gain runs from this (a gentle stroke) to 1 " +
                 "(a hard one), following the stroke's strength.")]
        [Range(0f, 1f)] public float LightStrokeGain = 0.75f;

        [Header("Per-stroke variation (deterministic, from the stroke's own count)")]
        [Tooltip("Each stroke's pitch, +/- this share, so repeats do not show.")]
        [Range(0f, 0.25f)] public float PitchVariation = 0.04f;

        [Tooltip("Each stroke's level, +/- this share.")]
        [Range(0f, 0.5f)] public float GainVariation = 0.1f;

        [Tooltip("Seeds the per-stroke variation. Audio is not simulation, but there is no global " +
                 "randomness either.")]
        public int Seed = 23;

        [Header("One sound a stroke")]
        [Tooltip("Seconds after one boat's catch in which another catch from the same boat is the other " +
                 "oar of the same stroke and stays silent. Must sit below the quickest stroke (about 1 s).")]
        [Min(0f)] public float MinGapSeconds = 0.3f;

        [Header("Who is heard")]
        [Tooltip("Full volume within this many metres of the listener.")]
        [Min(0f)] public float FullVolumeMetres = 12f;

        [Tooltip("Silent beyond this many metres: a rower further off is not voiced at all.")]
        [Min(0f)] public float EarshotMetres = 40f;

        [Tooltip("The widest pan (0..1): a rower at the edge of earshot is never hard left or right.")]
        [Range(0f, 1f)] public float PanWidth = 0.5f;

        [Tooltip("Stroke sounds that may sound at once (the voices are made once, at boot).")]
        [Range(1, 8)] public int Voices = 4;

        [Header("Where the two sounds are cut from the oar-stroke clip (seconds into it)")]
        [Tooltip("The catch sound starts here: the blade biting.")]
        [Min(0f)] public float CatchStartSeconds = 0f;

        [Tooltip("The catch sound's length: the bite, the creak of the load and the pull.")]
        [Min(0.01f)] public float CatchLengthSeconds = 0.95f;

        [Tooltip("The release sound starts here: the drips off the feathered blade.")]
        [Min(0f)] public float ReleaseStartSeconds = 1.0f;

        [Tooltip("The release sound's length.")]
        [Min(0.01f)] public float ReleaseLengthSeconds = 0.3f;

        [Tooltip("Fade at each end of a cut, seconds, so a cut never clicks.")]
        [Min(0f)] public float CutFadeSeconds = 0.005f;

        /// <summary>The shipped defaults (a fresh instance: the caller may change it).</summary>
        public static OarStrokeAudioTuning Default => new OarStrokeAudioTuning();
    }
}
