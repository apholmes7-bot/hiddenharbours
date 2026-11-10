using System;

namespace HiddenHarbours.Fishing
{
    public enum LivingFishKind { Schooling, Ambusher, BottomDweller, Crawler, Sessile }

    [Flags]
    public enum FishHabitat
    {
        Unknown = 0, OpenWater = 1, Sand = 2, Mud = 4, Rock = 8, WharfPilings = 16,
        All = OpenWater | Sand | Mud | Rock | WharfPilings
    }

    [Flags]
    public enum FishVegetation { None = 0, Eelgrass = 1, Kelp = 2, All = Eelgrass | Kelp }

    /// <summary>
    /// Opt-in occupancy data. A zero/default profile is disabled and changes no existing school,
    /// bite, rendering or save behaviour. Times use the Core clock, not wall time. Enabling requires
    /// authored school counts/density and a certified habitat patch supplied to SchoolField.
    /// </summary>
    [Serializable]
    public struct LivingFishBehaviour
    {
        // Half a single-precision ULP at one: accept a budget that rounds to 1f, irrespective of
        // Mono retaining wider intermediates (0.6f + 0.4f can otherwise compare greater than 1f).
        // This is numeric precision, not a gameplay/balance tolerance.
        private const double SingleUnitRoundoff = 1d / 16777216d;
        public bool Enabled;
        public LivingFishKind Kind;
        public FishHabitat AllowedHabitats;
        public FishVegetation PreferredVegetation;
        public float VegetationPreferenceMultiplier;
        public float MinDepthMetres;
        public float MaxDepthMetres;
        public float BedClearanceMetres;
        public float PatrolRadiusMetres;
        public float PatrolMetresPerGameSecond;
        public float NormalFootprintMetres;
        public float ScatteredFootprintMetres;
        public float ScatterDistanceMetres;
        public double ReactionDurationGameSeconds;
        public float DailyDepthStrength01;
        public float TidalDepthStrength01;

        /// <summary>Malformed enabled data fails closed; it never invents habitat or balance values.</summary>
        public bool IsValid =>
            Kind >= LivingFishKind.Schooling && Kind <= LivingFishKind.Sessile &&
            AllowedHabitats != FishHabitat.Unknown && (AllowedHabitats & ~FishHabitat.All) == 0 &&
            (PreferredVegetation & ~FishVegetation.All) == 0 &&
            Finite(VegetationPreferenceMultiplier) && VegetationPreferenceMultiplier >= 1f &&
            NonNegative(MinDepthMetres) && NonNegative(MaxDepthMetres) && MaxDepthMetres >= MinDepthMetres &&
            NonNegative(BedClearanceMetres) && NonNegative(PatrolRadiusMetres) &&
            NonNegative(PatrolMetresPerGameSecond) && NonNegative(NormalFootprintMetres) &&
            NonNegative(ScatteredFootprintMetres) && ScatteredFootprintMetres >= NormalFootprintMetres &&
            NonNegative(ScatterDistanceMetres) && Finite(ReactionDurationGameSeconds) &&
            ReactionDurationGameSeconds > 0d && NonNegative(DailyDepthStrength01) &&
            NonNegative(TidalDepthStrength01) &&
            (double)DailyDepthStrength01 + TidalDepthStrength01 <= 1d + SingleUnitRoundoff;

        internal static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
        private static bool NonNegative(float value) => Finite(value) && value >= 0f;
    }
}
