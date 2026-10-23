using System;
using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>One ocean climate shared by all regions of a bay. Recomputed, never saved.</summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/Swell Climate", fileName = "SwellClimate")]
    public sealed class SwellClimateDef : ScriptableObject
    {
        public string Id = "swell.home_bay";
        public SwellClimateSettings Climate = SwellClimateSettings.Default;
    }

    [Serializable]
    public struct SwellClimateSettings
    {
        [Tooltip("Incoming bearing, degrees clockwise from north, 0..360. Zero comes from north.")]
        [Range(0f, 360f)] public float FromBearingDegrees;
        [Tooltip("Primary crest spacing, metres, 1..200. Zero resolves to 40 m; fixed through time for dispersion.")]
        [Range(1f, 200f)] public float WavelengthMeters;
        [Tooltip("Second train's wavelength / primary, 0.5..1. Zero resolves to 0.81; separates group frequencies.")]
        [Range(0.5f, 1f)] public float SecondaryWavelengthRatio;
        [Tooltip("Combined crest-to-trough envelope, metres, 0..2. Zero silences both swell slots.")]
        [Range(0f, 2f)] public float HeightMeters;
        [Tooltip("Primary share of amplitude, 0..1. Zero puts all height into the second train.")]
        [Range(0f, 1f)] public float PrimaryShare;
        [Tooltip("Bearing excursion either side of incoming bearing, degrees, 0..45. Zero fixes direction.")]
        [Range(0f, 45f)] public float DirectionDriftDegrees;
        [Tooltip("Height excursion as fraction of height, 0..1. Zero fixes height; 1 can reach silence.")]
        [Range(0f, 1f)] public float HeightDriftFraction;
        [Tooltip("Bearing cycle, game days, 1..60. Zero resolves to 6 days; independent of weather.")]
        [Range(1f, 60f)] public float DirectionCycleDays;
        [Tooltip("Height cycle, game days, 1..60. Zero resolves to 4 days; independent of bearing.")]
        [Range(1f, 60f)] public float HeightCycleDays;
        [Tooltip("Upstream shelter march step, metres, 0.05..16, 24 steps. Zero resolves to 4 m.")]
        [Range(0.05f, 16f)] public float ShelterStepMeters;
        [Tooltip("Depth over which each shelter tap opens, metres, 0.001..2. Zero resolves to 0.25 m.")]
        [Range(0.001f, 2f)] public float ShelterDepthBandMeters;

        public static SwellClimateSettings Default => new SwellClimateSettings
        {
            FromBearingDegrees = 145f, WavelengthMeters = 40f, SecondaryWavelengthRatio = 0.81f,
            HeightMeters = 0.24f, PrimaryShare = 0.65f, DirectionDriftDegrees = 12f,
            HeightDriftFraction = 0.35f, DirectionCycleDays = 6f, HeightCycleDays = 4f,
            ShelterStepMeters = 4f, ShelterDepthBandMeters = 0.25f,
        };
    }
}
