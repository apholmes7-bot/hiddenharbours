using System;
using UnityEngine;

namespace HiddenHarbours.Fishing
{
    /// <summary>Timing and risk for one shore angler's visit. No species table or saved state.</summary>
    [CreateAssetMenu(menuName = "Fishing/Wharf Visit", fileName = "WharfVisit")]
    public sealed class WharfVisitDef : ScriptableObject
    {
        public string Id = "wharf_visit.st_peters";
        [Min(1)] public int CastsPerVisit = 3;
        [Min(0f)] public float MinBiteWaitSeconds = 8f;
        [Min(0f)] public float MaxBiteWaitSeconds = 24f;
        [Min(0f)] public float BetweenCastsSeconds = 4f;
        [Min(0f)] public float MinFightSeconds = 4f;
        [Min(0f)] public float MaxFightSeconds = 12f;
        [Range(0f, 1f)] public float LightStrengthCeiling = 0.35f;
        [Range(0f, 1f)] public float StrongStrengthFloor = 0.7f;
        [Range(0f, 1f)] public float LightLossChance = 0.05f;
        [Range(0f, 1f)] public float SteadyLossChance = 0.15f;
        [Range(0f, 1f)] public float StrongLossChance = 0.35f;

        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(Id) || !Id.StartsWith("wharf_visit.", StringComparison.Ordinal)
                || Id.Length == "wharf_visit.".Length || CastsPerVisit < 1
                || !Finite(MinBiteWaitSeconds) || MinBiteWaitSeconds <= 0f
                || !Finite(MaxBiteWaitSeconds) || MaxBiteWaitSeconds < MinBiteWaitSeconds
                || !Finite(BetweenCastsSeconds) || BetweenCastsSeconds < 0f
                || !Finite(MinFightSeconds) || MinFightSeconds <= 0f
                || !Finite(MaxFightSeconds) || MaxFightSeconds < MinFightSeconds
                || !Unit(LightStrengthCeiling) || !Unit(StrongStrengthFloor)
                || StrongStrengthFloor <= LightStrengthCeiling
                || !Unit(LightLossChance) || !Unit(SteadyLossChance) || !Unit(StrongLossChance)
                || LightLossChance > SteadyLossChance || SteadyLossChance > StrongLossChance)
                throw new ArgumentException("Invalid wharf visit timing or risk.");
        }

        private bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private bool Unit(float value) => Finite(value) && value >= 0f && value <= 1f;
    }
}
