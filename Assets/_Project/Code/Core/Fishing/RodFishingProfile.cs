using System;

namespace HiddenHarbours.Core
{
    /// <summary>Numeric rod capability, captured by value at the start of a cast. No saved state.</summary>
    [Serializable]
    public struct RodFishingProfile
    {
        public float CastMetres;
        public float LineMetres;
        public float ReelUpMetresPerSecond;
        public float MinBiteDelaySeconds;
        public float MaxBiteDelaySeconds;
        public float AutoHookSeconds;
        public float TakeWindowMultiplier;
        public float LineStrengthMultiplier;
        public float LandingMultiplier;

        // Endpoints for the legacy fight's species-strength interpolation.
        public float TensionRiseEasy;
        public float TensionRiseHard;
        public float TensionFallEasy;
        public float TensionFallHard;
        public float LandingEasy;
        public float LandingHard;
        public float SurgeIntervalEasy;
        public float SurgeIntervalHard;
        public float SurgeDurationSeconds;
        public float SurgePressureEasy;
        public float SurgePressureHard;

        public static RodFishingProfile Default => new RodFishingProfile
        {
            CastMetres = 12f, LineMetres = 60f, ReelUpMetresPerSecond = 1.5f,
            MinBiteDelaySeconds = 1.5f, MaxBiteDelaySeconds = 4f, AutoHookSeconds = 1.2f,
            TakeWindowMultiplier = 1f, LineStrengthMultiplier = 1f, LandingMultiplier = 1f,
            TensionRiseEasy = 0.60f, TensionRiseHard = 1.00f,
            TensionFallEasy = 0.70f, TensionFallHard = 0.50f,
            LandingEasy = 0.40f, LandingHard = 0.22f,
            SurgeIntervalEasy = 3f, SurgeIntervalHard = 1.6f,
            SurgeDurationSeconds = 0.8f, SurgePressureEasy = 0.15f, SurgePressureHard = 0.40f
        };

        public bool IsValid => Positive(CastMetres) && Positive(LineMetres)
            && Positive(ReelUpMetresPerSecond) && Positive(MinBiteDelaySeconds)
            && Positive(MaxBiteDelaySeconds) && MaxBiteDelaySeconds >= MinBiteDelaySeconds
            && Positive(AutoHookSeconds) && Positive(TakeWindowMultiplier)
            && Positive(LineStrengthMultiplier) && Positive(LandingMultiplier)
            && Positive(TensionRiseEasy) && Positive(TensionRiseHard)
            && Positive(TensionFallEasy) && Positive(TensionFallHard)
            && Positive(LandingEasy) && Positive(LandingHard)
            && Positive(SurgeIntervalEasy) && Positive(SurgeIntervalHard)
            && Positive(SurgeDurationSeconds) && Positive(SurgePressureEasy) && Positive(SurgePressureHard)
            && TensionFallEasy > SurgePressureEasy && TensionFallHard > SurgePressureHard;

        private static bool Positive(float value) => value > 0f && !float.IsInfinity(value);
    }

    /// <summary>The held object's capability, shared without a dependency on its owning module.</summary>
    public interface IRodFishingEquipment
    {
        bool IsFishingRod { get; }
        RodFishingProfile RodFishing { get; }
    }

    public static class CarriedRod
    {
        public static bool TryRead(ICarrier hands, out RodFishingProfile profile)
        {
            CarriedItem.Both(hands, out ICarriable first, out ICarriable second);
            if (TryRead(first, out profile)) return true;
            return TryRead(second, out profile);
        }

        private static bool TryRead(ICarriable item, out RodFishingProfile profile)
        {
            if (item is IRodFishingEquipment rod && rod.IsFishingRod)
            {
                profile = rod.RodFishing;
                if (profile.IsValid) return true;
            }
            profile = RodFishingProfile.Default;
            return false;
        }
    }
}
