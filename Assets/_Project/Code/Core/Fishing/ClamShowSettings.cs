using System;
using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>The bar clam's show</b>: how often a buried clam squirts through its two siphon holes, how long
    /// and how high the jet stands, and how it reacts when the player walks up to it (owner ask
    /// 2026-11-22: the holes are small and sit in the sand, and the squirt is the main giveaway of where
    /// a clam is).
    ///
    /// <para><b>Cosmetic only.</b> None of this gates the dig or is saved. The periodic squirt is a pure
    /// function of (world seed, hole position, the hole's own clock); the startle squirt is a real-time
    /// reaction to the player's position. Read through <see cref="Current"/> so an unwired config falls
    /// back to <see cref="Default"/>.</para>
    ///
    /// <para>The interval span and phase span ship at the art rig's own numbers (2.6 to 7.8 s apart);
    /// the squirt is held a little longer than the rig's 420 ms so the jet reads from across the flat at
    /// the game lens.</para>
    /// </summary>
    [Serializable]
    public struct ClamShowSettings
    {
        [Tooltip("How long one squirt lasts, in seconds: the jet rises, crests and falls back over this. " +
                 "The art rig's own squirt is 0.42 s.")]
        [Min(0.05f)] public float SquirtSeconds;

        [Tooltip("The shortest gap between one clam's squirts, in seconds. Each clam rolls its own gap " +
                 "between this and MaxIntervalSeconds from the world seed and its position.")]
        [Min(0.1f)] public float MinIntervalSeconds;

        [Tooltip("The longest gap between one clam's squirts, in seconds. Kept at or above " +
                 "MinIntervalSeconds (and above SquirtSeconds) when read.")]
        [Min(0.1f)] public float MaxIntervalSeconds;

        [Tooltip("The span, in seconds, that clams' squirt phases are spread over, so neighbouring clams " +
                 "do not squirt together.")]
        [Min(0f)] public float PhaseSpanSeconds;

        [Tooltip("How tall the jet is drawn: 1 = the art's own height (about 0.75 m at the crest, one " +
                 "texel to a game pixel). Other values stretch it vertically, its foot stays on the hole.")]
        [Range(0.25f, 3f)] public float JetHeightScale;

        [Tooltip("On the way down, the jet keeps its crest frame until its height falls below this share " +
                 "of the crest, then shows the collapse frame (the droplets raining back).")]
        [Range(0f, 1f)] public float CollapseBelowRise;

        [Tooltip("A clam squirts at once when the player steps within this many metres of it, as real " +
                 "clams do when they feel footsteps. 0 = off.")]
        [Min(0f)] public float StartleRadiusMetres;

        [Tooltip("After a startle squirt, the same clam will not startle again for this many seconds, " +
                 "even if the player steps out and back in.")]
        [Min(0f)] public float StartleCooldownSeconds;

        /// <summary>The shipped starting numbers (for the owner to tune by feel).</summary>
        public static ClamShowSettings Default => new ClamShowSettings
        {
            SquirtSeconds = 0.6f,
            MinIntervalSeconds = 2.6f,      // the rig's period span
            MaxIntervalSeconds = 7.8f,
            PhaseSpanSeconds = 9f,          // the rig's phase span
            JetHeightScale = 1f,
            CollapseBelowRise = 0.6f,
            StartleRadiusMetres = 3f,
            StartleCooldownSeconds = 5f,
        };

        /// <summary>The live numbers: the config's, or <see cref="Default"/> with no config wired.</summary>
        public static ClamShowSettings Current
            => GameServices.Config != null ? GameServices.Config.ClamShow : Default;

        /// <summary>Squirt length in milliseconds, never below 50 ms.</summary>
        public float SquirtMilliseconds => Mathf.Max(0.05f, SquirtSeconds) * 1000f;

        /// <summary>Shortest gap in milliseconds, never shorter than the squirt itself (a gap shorter
        /// than the squirt would leave the jet standing for ever).</summary>
        public float MinIntervalMilliseconds
            => Mathf.Max(Mathf.Max(0.1f, MinIntervalSeconds) * 1000f, SquirtMilliseconds + 1f);

        /// <summary>Longest gap in milliseconds, never below the shortest.</summary>
        public float MaxIntervalMilliseconds
            => Mathf.Max(Mathf.Max(0.1f, MaxIntervalSeconds) * 1000f, MinIntervalMilliseconds);

        /// <summary>Phase span in milliseconds, never negative.</summary>
        public float PhaseSpanMilliseconds => Mathf.Max(0f, PhaseSpanSeconds) * 1000f;
    }
}
