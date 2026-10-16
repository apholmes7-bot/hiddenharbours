using System;
using UnityEngine;

namespace HiddenHarbours.Audio
{
    /// <summary>
    /// How the gulls are heard (rule 6), owner-editable on the <see cref="AudioDirector"/>. The calls
    /// follow the birds the flock has near the view (<c>GameServices.BirdPresence</c>): single calls,
    /// each panned to its bird, denser with a bigger flock and a busier one, capped, and silent when no
    /// bird is near. Nothing here is simulation: these numbers only shape what the player hears.
    /// </summary>
    [Serializable]
    public sealed class GullCallTuning
    {
        [Tooltip("The bird these calls are, by its bird. id. Only its birds are voiced.")]
        public string SpeciesId = "bird.herring_gull";

        [Header("Hearing")]
        [Tooltip("Full volume inside the view, fading to silence this many metres beyond its edge. Keep it " +
                 "at the flock's ShowMarginMetres, so every bird heard is one the flock follows.")]
        [Min(0f)] public float HearingMarginMetres = 15f;

        [Tooltip("The widest pan (0..1): a bird at the edge of the view is never hard left or right.")]
        [Range(0f, 1f)] public float PanWidth = 0.8f;

        [Header("How many calls")]
        [Tooltip("The most calls at once (the voices are made once, at boot).")]
        [Range(1, GullCallLogic.MaxVoiceCap)] public int MaxVoices = 6;

        [Tooltip("The chorus never calls faster than this, however many birds there are.")]
        [Min(0f)] public float MaxCallsPerSecond = 3f;

        [Tooltip("Calls per second from one bird sitting or roosting.")]
        [Min(0f)] public float RestingCallsPerSecond = 0.03f;

        [Tooltip("Calls per second from one bird in the air.")]
        [Min(0f)] public float AloftCallsPerSecond = 0.08f;

        [Tooltip("Calls per second from one bird working food: a school, or the plant or the wharf in its hours.")]
        [Min(0f)] public float FeedingCallsPerSecond = 0.15f;

        [Header("Each call")]
        [Tooltip("Each call's pitch, +/- this share, so repeats do not show.")]
        [Range(0f, 0.5f)] public float PitchJitter = 0.06f;

        [Tooltip("The gulls' level on the ambience bus.")]
        [Range(0f, 1f)] public float CallGain = 0.7f;

        [Tooltip("Seconds of fade in and out on every call.")]
        [Min(0f)] public float FadeSeconds = 0.06f;

        [Header("A recording in the slot")]
        [Tooltip("The shortest excerpt played from a recording, seconds.")]
        [Min(0.1f)] public float ExcerptSecondsMin = 1.5f;

        [Tooltip("The longest excerpt played from a recording, seconds.")]
        [Min(0.1f)] public float ExcerptSecondsMax = 3f;

        [Tooltip("Seeds when the calls come and which bird makes them. Audio is not simulation, but there " +
                 "is no global randomness either.")]
        public int Seed = 11;

        /// <summary>The shipped defaults (a fresh instance: the caller may change it).</summary>
        public static GullCallTuning Default => new GullCallTuning();
    }
}
