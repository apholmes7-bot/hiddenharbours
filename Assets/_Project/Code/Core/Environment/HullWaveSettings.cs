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
        [Tooltip("Longest wave a hull draws, metres. The wave that keeps pace with a fast hull is longer " +
                 "than the screen, so it is held here; the V keeps its angle whatever the wave length.")]
        [Range(1f, 80f)] public float MaxWavelengthMetres;
        [Tooltip("Longest wave a hull draws, per metre of her length (0 = no length limit). Keeps a small " +
                 "hull's waves small and lets a long one draw long waves, so a dory and a cargo vessel " +
                 "at the same speed look different.")]
        [Range(0f, 4f)] public float MaxWavelengthPerMetreLength;
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
        [Tooltip("A short hull's lowest crest at full response, metres: her waves are raised to at least " +
                 "this times her Froude response (speed over the knee, not squared), so a rowed dory's soft V " +
                 "reads at game zoom. Only hulls no longer than SmallHullLengthMetres. 0 = off (the older height).")]
        [Range(0f, 0.3f)] public float SmallHullCrestMetres;
        [Tooltip("The longest hull, metres, that SmallHullCrestMetres raises. Longer hulls keep their height.")]
        [Range(0f, 20f)] public float SmallHullLengthMetres;
        [Tooltip("A short hull's source spacing along her track, in wavelengths, in place of " +
                 "SpacingWavelengths: closer sources blend her rings into a soft V instead of single loops. " +
                 "Only hulls no longer than SmallHullLengthMetres. 0 = her spacing is SpacingWavelengths.")]
        [Range(0f, 2f)] public float SmallHullSpacingWavelengths;
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
        [Tooltip("Share of a ring's life over which it rises from nothing to full height (0 = born at " +
                 "full). A newborn ring at full height paints a round blot round the hull, ahead of her " +
                 "bow too; rising in, the wake opens out of her as a V.")]
        [Range(0f, 0.5f)] public float RiseShare;
        [Tooltip("Share of a ring's life, at its end, over which it fades out (1 = it fades all its life). " +
                 "Smaller keeps the arms of the V at height further behind her before they go.")]
        [Range(0.05f, 1f)] public float EndFadeShare;

        [Header("Budget (fixed; no allocations)")]
        [Tooltip("Sources one hull keeps alive at once, bow and stern together. A slow hull spaces them " +
                 "by SpacingWavelengths; a long-lived wake is spaced out to fit this.")]
        [Range(2, 64)] public int SourcesPerHull;
        [Tooltip("The bow's part of SourcesPerHull.")]
        [Range(0f, 1f)] public float BowSourceShare;
        [Tooltip("Sources alive at once across every hull and every splash (at most 64, the water " +
                 "shader's bound). When full, the weakest is replaced.")]
        [Range(0, 64)] public int MaxSources;
        [Tooltip("Spacing between a hull's sources along her track, in wavelengths. Under one, each ring " +
                 "carries only part of her crest and the arms of the V go faint; much over two and single " +
                 "rings show.")]
        [Range(0.05f, 2f)] public float SpacingWavelengths;
        [Tooltip("When her budget cannot cover a full life at SpacingWavelengths, her waves live shorter " +
                 "instead of being laid further apart (never below MinLifetimeSeconds). On keeps the arms " +
                 "and transverse crests of a fast hull joined up; off spreads the rings apart.")]
        public bool KeepSpacing;
        [Tooltip("Height each source carries, per wavelength of spacing, before overlap (keeps the summed " +
                 "crest near her crest height whatever the spacing).")]
        [Range(0f, 4f)] public float SourceGain;

        [Header("How the water draws them")]
        [Tooltip("Drawn lift of the displaced water per metre of wave height.")]
        [Range(0f, 6f)] public float DrawnLift;
        [Tooltip("Light on the faces of the waves: slope towards the sun brightens, away darkens.")]
        [Range(0f, 40f)] public float FaceLight;
        [Tooltip("Brightening on a crest, per metre of crest height above the sea.")]
        [Range(0f, 40f)] public float CrestLight;
        [Tooltip("Darkening in a trough, per metre of trough depth.")]
        [Range(0f, 40f)] public float TroughDark;
        [Tooltip("Crest height, metres, where a little foam starts to show on the crests.")]
        [Range(0f, 2f)] public float CrestFoamOnsetMetres;
        [Tooltip("Metres above the onset over which the crest foam comes in fully.")]
        [Range(0.01f, 2f)] public float CrestFoamRampMetres;
        [Tooltip("How white the crest foam gets at full (0..1).")]
        [Range(0f, 1f)] public float CrestFoam;
        [Tooltip("The most a wave lifts or darkens the water's own colour, as a share of it (0.2 = a crest " +
                 "at most a fifth brighter, a trough at most a fifth darker). Approached softly, never " +
                 "clipped, so a trough is a gentle darkening of the sea, not a black line with a hard edge.")]
        [Range(0f, 1.5f)] public float ShadeCeiling;
        [Tooltip("How much the sea's own noise breaks her crests up (0 = even, unbroken rings; 1 = crests " +
                 "come and go in patches). Fixed in the world, so the waves run through it and crests " +
                 "rise and break as they go, like real water.")]
        [Range(0f, 1f)] public float BreakupShare;
        [Tooltip("Size of those patches, metres.")]
        [Range(0.5f, 40f)] public float BreakupMetres;
        [Tooltip("How far, metres, the crests wobble off true circles (the same noise bends every ring, " +
                 "so no ring shows a perfect centre).")]
        [Range(0f, 3f)] public float WobbleMetres;
        [Tooltip("The most a trough darkens, as a share of the most a crest lifts (ShadeCeiling). Under 1 keeps " +
                 "a dark sea from going near black in a deep trough.")]
        [Range(0f, 1f)] public float TroughCeilingShare;
        [Tooltip("Steepness (rise over run) where a crest starts to break and carry foam, whatever its height. " +
                 "The young crests at her quarters at speed are the steepest, so the first crests of each arm " +
                 "foam and the foam and the waves read as one thing.")]
        [Range(0f, 3f)] public float CrestFoamSteepness;
        [Tooltip("Steepness above the onset over which that crest foam comes in fully.")]
        [Range(0.01f, 2f)] public float CrestFoamSteepnessRamp;
        [Tooltip("How much more of the water's colour her waves take in low light, at full dimness (0 = the " +
                 "same share as at noon). As the sun's colour dims at dusk and night the sea is darker and a " +
                 "gentle wave reads as nothing; this keeps her V reading as gently as at noon. The whole " +
                 "soft shade, ShadeCeiling included, is scaled by 1 + this: dusk light is about half of " +
                 "noon's, so 1 gives about the same step.")]
        [Range(0f, 6f)] public float LowLightGain;
        [Tooltip("The sun's luminance (0..1, the day/night tint) below which the low-light lift starts. Noon " +
                 "sits above it.")]
        [Range(0f, 1f)] public float LowLightStartLuminance;
        [Tooltip("The sun's luminance (0..1) at which the low-light lift is full (dusk and darker).")]
        [Range(0f, 1f)] public float LowLightFullLuminance;

        [Header("Her foam while she draws waves")]
        [Tooltip("While hull waves are on, the trail foam's widening fan (the older drawn stand-in for " +
                 "the Kelvin wake) is not laid: the waves draw the V now. Off keeps the fan.")]
        public bool RetireFoamFan;
        [Tooltip("While hull waves are on and she makes way, the share of her churned trail foam she still " +
                 "lays (1 = all of it). Thinner foam lets the waves behind her show.")]
        [Range(0f, 1f)] public float TrailFoamShare;
        [Tooltip("While hull waves are on and she makes way, the widest her churned trail band is laid, " +
                 "metres either side of her track (0 = no limit). Her prop wash is narrower than a broad " +
                 "hull's beam, so a cargo vessel's trail no longer covers the waves beside it. Bites only " +
                 "hulls wider than this.")]
        [Range(0f, 10f)] public float TrailFoamMaxHalfWidthMetres;
        [Tooltip("While hull waves are on, the wake's own drawn foam puffs (the sprite centre lane and stern " +
                 "churn band) are not laid: her churn is the water's foam buffer and her breaking crests join " +
                 "it, drawn by the same foam path as every other foam on the sea. Off brings the puffs back.")]
        public bool RetireDrawnFoam;
        [Tooltip("While hull waves are on, how hard the sea's foam mat (the shared foam buffer: her churn, her " +
                 "breaking crests, every hull's marks) tears apart as it ages: 0 = it fades as one even mat, " +
                 "1 = foam half way through its age keeps only its densest hearts, so a trail is dense at her stern and breaks into " +
                 "drifting patches and flecks behind her.")]
        [Range(0f, 1f)] public float FoamAgeTear;

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
            MaxWavelengthMetres = 4f,
            MaxWavelengthPerMetreLength = 0.12f,
            PacketWavelengths = 0.8f,
            SpreadMetres = 4f,

            MinSpeed = 0.3f,
            HeightPerMetreLength = 0.018f,
            FroudeKnee = 0.4f,
            MaxHeightMetres = 0.6f,
            SmallHullCrestMetres = 0.07f,
            SmallHullLengthMetres = 6f,
            SmallHullSpacingWavelengths = 0.35f,
            BowShare = 0.7f,

            LifetimeSecondsPerMetreLength = 0.8f,
            MinLifetimeSeconds = 3f,
            MaxLifetimeSeconds = 30f,
            DecayShare = 1.8f,
            RiseShare = 0.12f,
            EndFadeShare = 0.8f,

            SourcesPerHull = 20,
            BowSourceShare = 0.25f,
            MaxSources = 64,
            SpacingWavelengths = 1f,
            KeepSpacing = true,
            SourceGain = 1f,

            DrawnLift = 0.6f,
            FaceLight = 30f,
            CrestLight = 14f,
            TroughDark = 12f,
            CrestFoamOnsetMetres = 0.5f,
            CrestFoamRampMetres = 0.3f,
            CrestFoam = 0.25f,
            ShadeCeiling = 0.65f,
            BreakupShare = 0.4f,
            BreakupMetres = 4f,
            WobbleMetres = 0.4f,
            TroughCeilingShare = 0.6f,
            LowLightGain = 2f,
            LowLightStartLuminance = 0.85f,
            LowLightFullLuminance = 0.5f,
            CrestFoamSteepness = 0.6f,
            CrestFoamSteepnessRamp = 0.3f,

            RetireFoamFan = true,
            TrailFoamShare = 0.2f,
            TrailFoamMaxHalfWidthMetres = 3f,
            RetireDrawnFoam = true,
            FoamAgeTear = 1f,

            RideScale = 1f,

            RippleLifetimeSeconds = 2.5f,
        };
    }
}
