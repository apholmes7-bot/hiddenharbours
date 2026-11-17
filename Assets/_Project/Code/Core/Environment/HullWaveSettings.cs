using System;
using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// The owner's dials for the waves a moving hull makes (the owner, 2026-11-17: "we need real waves
    /// from boats, from the stern, i also think the bow would cause wake waves"). A hull making way drops
    /// a trail of wave SOURCES behind her stern and at her bow; each source sends a ring of real waves
    /// outward through the water, and the rings together make the V-shaped Kelvin wake with transverse
    /// waves behind her. Every number that shapes, sizes, times or shades them lives here.
    ///
    /// <para>Presentation only. The water draws these waves and other hulls bob on them through their
    /// drawn ride; no force, no simulation state and nothing saved reads them.</para>
    /// </summary>
    [Serializable]
    public struct HullWaveSettings
    {
        [Tooltip("Hulls making way drop wave sources behind the stern and at the bow, and the water draws " +
                 "their waves. Off = no hull waves at all, and the older drawn wake train comes back.")]
        public bool Enabled;
        [Tooltip("While hull waves are on, the wake's painted crest and stern-roll sprites are not built " +
                 "(they were drawn lines). Off keeps both.")]
        public bool RetirePaintedCrests;

        [Header("Shape")]
        [Tooltip("Half-angle of the wake's V each side of the track, degrees. Deep water gives 19.47 at " +
                 "every speed (Kelvin). Each ring spreads at speed x sin(angle), which is what makes the V.")]
        [Range(5f, 35f)] public float KelvinHalfAngleDegrees;
        [Tooltip("Shortest wave a hull draws, metres. The wave that keeps pace with a slow hull is " +
                 "shorter than a pixel reads, so it is held here.")]
        [Range(0.1f, 5f)] public float MinWavelengthMetres;
        [Tooltip("Longest wave a hull draws, metres.")]
        [Range(1f, 80f)] public float MaxWavelengthMetres;
        [Tooltip("Width of each ring's band of crests, in wavelengths. Wider = more crests per ring.")]
        [Range(0.3f, 4f)] public float PacketWavelengths;
        [Tooltip("Metres over which a ring's height halves its energy as it spreads (height falls as " +
                 "1 / sqrt(1 + r / this)).")]
        [Range(0.5f, 50f)] public float SpreadMetres;

        [Header("Size (from her hull: length and speed)")]
        [Tooltip("Below this speed through the water, m/s, she makes no waves.")]
        [Range(0f, 3f)] public float MinSpeed;
        [Tooltip("Crest height per metre of her length at full response, metres. A 5 m dory and a 40 m " +
                 "cargo vessel differ by this times their lengths.")]
        [Range(0f, 0.1f)] public float HeightPerMetreLength;
        [Tooltip("Froude number (speed / sqrt(g x length)) at which her waves reach full height. Below " +
                 "it the height grows with the square of speed.")]
        [Range(0.05f, 2f)] public float FroudeKnee;
        [Tooltip("Tallest crest any hull makes, metres.")]
        [Range(0f, 3f)] public float MaxHeightMetres;
        [Tooltip("The bow's waves as a share of the stern's.")]
        [Range(0f, 2f)] public float BowShare;

        [Header("Life")]
        [Tooltip("How long her waves live, seconds per metre of her length.")]
        [Range(0f, 5f)] public float LifetimeSecondsPerMetreLength;
        [Tooltip("Shortest wave life, seconds.")]
        [Range(0.5f, 30f)] public float MinLifetimeSeconds;
        [Tooltip("Longest wave life, seconds.")]
        [Range(1f, 120f)] public float MaxLifetimeSeconds;
        [Tooltip("How fast a ring loses height, as a share of its life (height falls as exp(-age / " +
                 "(life x this)), and reaches exactly zero at the end of life).")]
        [Range(0.05f, 2f)] public float DecayShare;

        [Header("Budget (fixed; no allocations)")]
        [Tooltip("Sources one hull keeps alive at once, bow and stern together. A slow hull spaces them " +
                 "by SpacingWavelengths; a long-lived wake is spaced out to fit this.")]
        [Range(2, 64)] public int SourcesPerHull;
        [Tooltip("The bow's part of SourcesPerHull.")]
        [Range(0f, 1f)] public float BowSourceShare;
        [Tooltip("Sources alive at once across every hull and every splash (at most 64, the water " +
                 "shader's bound). When full, the weakest is replaced.")]
        [Range(0, 64)] public int MaxSources;
        [Tooltip("Spacing between a hull's sources along her track, in wavelengths. Under about a third " +
                 "and the rings blend into one wake; much more and single rings show.")]
        [Range(0.05f, 2f)] public float SpacingWavelengths;
        [Tooltip("Height each source carries, per wavelength of spacing, before overlap (keeps the summed " +
                 "crest near her crest height whatever the spacing).")]
        [Range(0f, 4f)] public float SourceGain;

        [Header("How the water draws them")]
        [Tooltip("Drawn lift of the displaced water per metre of wave height.")]
        [Range(0f, 6f)] public float DrawnLift;
        [Tooltip("Light on the faces of the waves: slope towards the sun brightens, away darkens.")]
        [Range(0f, 40f)] public float FaceLight;
        [Tooltip("Brightening on a crest, per metre of crest height above the sea.")]
        [Range(0f, 4f)] public float CrestLight;
        [Tooltip("Darkening in a trough, per metre of trough depth.")]
        [Range(0f, 4f)] public float TroughDark;
        [Tooltip("Crest height, metres, where a little foam starts to show on the crests.")]
        [Range(0f, 2f)] public float CrestFoamOnsetMetres;
        [Tooltip("Metres above the onset over which the crest foam comes in fully.")]
        [Range(0.01f, 2f)] public float CrestFoamRampMetres;
        [Tooltip("How white the crest foam gets at full (0..1).")]
        [Range(0f, 1f)] public float CrestFoam;

        [Header("Other hulls")]
        [Tooltip("How much another hull's drawn ride rises and falls on these waves (0 = not at all). " +
                 "Drawn only: no force. A hull never rides her own waves, and a long hull averages out " +
                 "short ones.")]
        [Range(0f, 2f)] public float RideScale;

        [Header("Small splashes (oars, things dropped in)")]
        [Tooltip("How long a splash ring lives, seconds.")]
        [Range(0.2f, 20f)] public float RippleLifetimeSeconds;

        public static HullWaveSettings Default => new HullWaveSettings
        {
            Enabled = true,
            RetirePaintedCrests = true,

            KelvinHalfAngleDegrees = 19.47f,
            MinWavelengthMetres = 0.6f,
            MaxWavelengthMetres = 30f,
            PacketWavelengths = 1.2f,
            SpreadMetres = 4f,

            MinSpeed = 0.3f,
            HeightPerMetreLength = 0.012f,
            FroudeKnee = 0.4f,
            MaxHeightMetres = 0.6f,
            BowShare = 0.7f,

            LifetimeSecondsPerMetreLength = 0.8f,
            MinLifetimeSeconds = 4f,
            MaxLifetimeSeconds = 30f,
            DecayShare = 0.45f,

            SourcesPerHull = 28,
            BowSourceShare = 0.35f,
            MaxSources = 64,
            SpacingWavelengths = 0.3f,
            SourceGain = 1f,

            DrawnLift = 1.5f,
            FaceLight = 6f,
            CrestLight = 0.3f,
            TroughDark = 0.12f,
            CrestFoamOnsetMetres = 0.12f,
            CrestFoamRampMetres = 0.15f,
            CrestFoam = 0.55f,

            RideScale = 1f,

            RippleLifetimeSeconds = 2.5f,
        };
    }
}
