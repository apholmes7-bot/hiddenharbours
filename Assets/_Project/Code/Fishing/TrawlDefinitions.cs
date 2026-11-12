using System;

namespace HiddenHarbours.Fishing
{
    // Data-only schemas for the unwired prototype. No authored content or asset defaults.
    // Asset wrappers/identity registration are an explicit activation gate, not a ledger bypass.
    [Serializable]
    public struct TrawlGearDef
    {
        public string Id;
        public double MinDepthMetres, MaxDepthMetres;
        public double MinWarpDepthRatio, MaxWarpDepthRatio;
        public double MinTowMetresPerGameSecond, MaxTowMetresPerGameSecond;
        public double MinDoorSpreadMetres, MaxDoorSpreadMetres, MouthHeightMetres;
        public double CaptureFraction01, HardBottomThreshold01, DamagePerHardMetre;
        public double SampleGameSeconds, MaxTowGameSeconds;
        public int CodendCapacityIndividuals, MaxSamples, MaxSchoolsPerSample, MaxEncounteredSchools, MaxTrackKnots;

        public bool IsValid => !string.IsNullOrWhiteSpace(Id)
            && TrawlNumbers.Positive(MinDepthMetres) && TrawlNumbers.Positive(MaxDepthMetres) && MaxDepthMetres >= MinDepthMetres
            && TrawlNumbers.Positive(MinWarpDepthRatio) && MinWarpDepthRatio >= 1d
            && TrawlNumbers.Positive(MaxWarpDepthRatio) && MaxWarpDepthRatio >= MinWarpDepthRatio
            && TrawlNumbers.Positive(MinTowMetresPerGameSecond) && TrawlNumbers.Positive(MaxTowMetresPerGameSecond)
            && MaxTowMetresPerGameSecond >= MinTowMetresPerGameSecond
            && TrawlNumbers.Positive(MinDoorSpreadMetres) && TrawlNumbers.Positive(MaxDoorSpreadMetres)
            && MaxDoorSpreadMetres >= MinDoorSpreadMetres && TrawlNumbers.Positive(MouthHeightMetres)
            && TrawlNumbers.Unit(CaptureFraction01) && TrawlNumbers.Unit(HardBottomThreshold01)
            && TrawlNumbers.NonNegative(DamagePerHardMetre) && TrawlNumbers.Positive(SampleGameSeconds)
            && TrawlNumbers.Positive(MaxTowGameSeconds) && MaxTowGameSeconds >= SampleGameSeconds
            && CodendCapacityIndividuals > 0 && MaxSamples > 0 && MaxSchoolsPerSample > 0
            && MaxEncounteredSchools > 0 && MaxTrackKnots >= 2;
    }

    public enum TrawlHandlingKind { Unstated, Stern, Side }
    public enum TrawlWorkStation { Unstated, Astern, Port, Starboard }

    [Serializable]
    public struct TrawlHandlingDef
    {
        public string Id;
        public TrawlHandlingKind Kind;
        public TrawlWorkStation WorkStation;
        public double ShootingGameSeconds, HaulingGameSeconds;
        public double MaxShootingMetresPerGameSecond, MaxHaulingMetresPerGameSecond;
        public double MaxWorkingSeaState01, SortIndividualsPerGameSecond;

        public bool IsValid => !string.IsNullOrWhiteSpace(Id)
            && ((Kind == TrawlHandlingKind.Stern && WorkStation == TrawlWorkStation.Astern)
                || (Kind == TrawlHandlingKind.Side && (WorkStation == TrawlWorkStation.Port || WorkStation == TrawlWorkStation.Starboard)))
            && TrawlNumbers.Positive(ShootingGameSeconds) && TrawlNumbers.Positive(HaulingGameSeconds)
            && TrawlNumbers.NonNegative(MaxShootingMetresPerGameSecond)
            && TrawlNumbers.NonNegative(MaxHaulingMetresPerGameSecond)
            && TrawlNumbers.Unit(MaxWorkingSeaState01) && TrawlNumbers.Positive(SortIndividualsPerGameSecond);
    }

    internal static class TrawlNumbers
    {
        internal static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
        internal static bool Positive(double value) => Finite(value) && value > 0d;
        internal static bool NonNegative(double value) => Finite(value) && value >= 0d;
        internal static bool Unit(double value) => NonNegative(value) && value <= 1d;
    }
}
