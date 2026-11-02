using System;
using UnityEngine;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// A region's water as data (ADR 0027 #7, gains 2): what it carries when dry and how a wet spell muddies
    /// it. One type per file with a stable <c>water.snake_case</c> id. A region's <see cref="WaterSurface"/>
    /// holds its type (never the shared Water.mat) and a <see cref="WaterPlumeDef"/> carries one to a creek
    /// or river mouth; a region with no type draws exactly as before. The law is <see cref="WaterType"/>.
    /// </summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/Water Type", fileName = "WaterType")]
    public sealed class WaterTypeDef : ScriptableObject
    {
        public const string IdPrefix = "water.";

        [Tooltip("Stable id, water.snake_case. Ids are append-only.")]
        public string Id = "water.example";

        [Tooltip("What the water carries, how rain adds silt, and how far the body takes their colour. " +
                 "Each field names its default, unit and range.")]
        public WaterTypeValues Water = WaterTypeValues.Default;

        [Tooltip("Optional crest-glow review arm. Unassigned ships the glow off. Select by eye from the plates; " +
                 "each water type can choose its own arm and absorption still supplies its colour.")]
        public CrestGlowDef CrestGlow;

        /// <summary>A stable id with the type's prefix and every value inside its range.</summary>
        public bool IsValid() => !string.IsNullOrWhiteSpace(Id)
            && Id.StartsWith(IdPrefix, StringComparison.Ordinal) && Water.IsValid()
            && (CrestGlow == null || CrestGlow.IsValid());
    }

    /// <summary>
    /// The numbers of a water type. <see cref="Default"/> is today's water: no constituent, no rain response
    /// and no tint, so its extinction is the mood's alone. Constituents are extinction in 1/m at their
    /// spectrum's strongest channel; a spectrum is per channel (linear RGB), each 0 to 1.
    /// </summary>
    [Serializable]
    public struct WaterTypeValues
    {
        /// <summary>The most a dry constituent may carry, 1/m.</summary>
        public const float MaxConcentration = 2f;
        /// <summary>The most silt a full wet spell may add, 1/m per unit of mean rain.</summary>
        public const float MaxSiltResponse = 4f;
        /// <summary>The clearing window's range, game days.</summary>
        public const float MinWindowDays = 0.25f;
        public const float MaxWindowDays = 14f;
        /// <summary>Three game days: a creek's runoff settles and flushes over a few days after the rain stops,
        /// and the window is both how long a wet spell is remembered and how long the water takes to clear.</summary>
        public const float DefaultWindowDays = 3f;
        /// <summary>Taps across the window: WaveMemory's own clamp, 4 to 64.</summary>
        public const int MinTaps = 4;
        public const int MaxTaps = 64;
        /// <summary>24 taps: one every three game hours across the default window, fine enough that a squall
        /// of a few hours lands on a tap, and well inside the slow tick's budget.</summary>
        public const int DefaultTaps = 24;

        [Tooltip("Dissolved stain (tea) the water carries. Default 0; 1/m at the spectrum's strongest " +
                 "channel; 0 to 2.")]
        [Range(0f, MaxConcentration)] public float Stain;
        [Tooltip("Where the stain takes its light, per channel (linear RGB, each 0 to 1). " +
                 "Default (0.10, 0.28, 1): it eats blue first.")]
        public Vector3 StainSpectrum;
        [Tooltip("Algae (green) the water carries. Default 0; 1/m at the spectrum's strongest channel; 0 to 2.")]
        [Range(0f, MaxConcentration)] public float Algae;
        [Tooltip("Where the algae takes its light, per channel (each 0 to 1). " +
                 "Default (0.40, 0.20, 1): it spares green.")]
        public Vector3 AlgaeSpectrum;
        [Tooltip("Silt (brown) the water carries when dry. Default 0; 1/m at the spectrum's strongest " +
                 "channel; 0 to 2.")]
        [Range(0f, MaxConcentration)] public float DrySilt;
        [Tooltip("Where the silt takes its light, per channel (each 0 to 1). " +
                 "Default (0.80, 0.88, 1): it dims every channel.")]
        public Vector3 SiltSpectrum;
        [Tooltip("Silt a wet spell adds: 1/m per unit of the rain (0 to 1) averaged over the window. " +
                 "Default 0 (rain changes nothing); 0 to 4.")]
        [Range(0f, MaxSiltResponse)] public float SiltResponse;
        [Tooltip("The clearing window: how far back the silt remembers the rain, in game days. " +
                 "Default 3; 0.25 to 14.")]
        [Range(MinWindowDays, MaxWindowDays)] public float SiltWindowDays;
        [Tooltip("Taps across the window, on WaveMemory's fixed world-time grid. Default 24; 4 to 64.")]
        [Range(MinTaps, MaxTaps)] public int SiltTaps;
        [Tooltip("The colour stain lends the water's body (sRGB, each channel 0 to 1). " +
                 "Default tea (0.42, 0.27, 0.12).")]
        public Color StainColour;
        [Tooltip("The colour silt lends the water's body (sRGB, each channel 0 to 1). " +
                 "Default mud (0.52, 0.40, 0.27).")]
        public Color SiltColour;
        [Tooltip("How far a column thick with stain and silt moves the water toward their colours. " +
                 "Default 0 (no tint); 0 to 1.")]
        [Range(0f, 1f)] public float BodyStrength;

        /// <summary>Today's water: every constituent, the response and the tint at 0.</summary>
        public static WaterTypeValues Default => new WaterTypeValues
        {
            StainSpectrum = new Vector3(0.10f, 0.28f, 1f),
            AlgaeSpectrum = new Vector3(0.40f, 0.20f, 1f),
            SiltSpectrum = new Vector3(0.80f, 0.88f, 1f),
            SiltWindowDays = DefaultWindowDays,
            SiltTaps = DefaultTaps,
            StainColour = new Color(0.42f, 0.27f, 0.12f, 1f),
            SiltColour = new Color(0.52f, 0.40f, 0.27f, 1f),
        };

        /// <summary>Every field inside its range (NaN is outside every range).</summary>
        public bool IsValid()
            => In(Stain, 0f, MaxConcentration) && In(Algae, 0f, MaxConcentration)
               && In(DrySilt, 0f, MaxConcentration) && In(SiltResponse, 0f, MaxSiltResponse)
               && In(SiltWindowDays, MinWindowDays, MaxWindowDays)
               && SiltTaps >= MinTaps && SiltTaps <= MaxTaps && In(BodyStrength, 0f, 1f)
               && Spectrum(StainSpectrum) && Spectrum(AlgaeSpectrum) && Spectrum(SiltSpectrum)
               && Colour(StainColour) && Colour(SiltColour);

        private static bool In(float v, float lo, float hi) => v >= lo && v <= hi;
        private static bool Spectrum(Vector3 s) => In(s.x, 0f, 1f) && In(s.y, 0f, 1f) && In(s.z, 0f, 1f);
        private static bool Colour(Color c) => In(c.r, 0f, 1f) && In(c.g, 0f, 1f) && In(c.b, 0f, 1f);
    }
}
