using System.Text.RegularExpressions;
using HiddenHarbours.Core;
using UnityEngine;

namespace HiddenHarbours.Fishing
{
    /// <summary>One hand-picking bed. Its location and replenishment are content, with no scene dependency.</summary>
    [CreateAssetMenu(menuName = "Fishing/Picking Bed")]
    public sealed class PickingBedDef : ScriptableObject
    {
        public string Id;
        public string RegionId;
        public Vector2 Position;
        public FishSpeciesDef Species;
        [Min(1)] public int Capacity;
        [Min(0f)] public float RegrowDays;
        [Tooltip("Water level must be strictly below this height, in metres above chart datum.")]
        public float UncoveredBelowMetres;
        [Min(0f)] public float ReachMetres;

        public bool HasValidTuning => ValidId(Id, "pickingbed") && ValidId(RegionId, "region")
            && Finite(Position.x) && Finite(Position.y)
            && Capacity > 0 && Positive(RegrowDays) && Positive(ReachMetres)
            && Finite(UncoveredBelowMetres)
            && Species != null && ValidId(Species.Id, "fish")
            && Species.Category == FishCategory.Shellfish && Species.AllowedGear == Gear.ClamFork
            && Species.RegionAllowed(RegionId) && !Species.Protected
            && Positive(Species.MinWeightKg) && Finite(Species.MaxWeightKg)
            && Species.MaxWeightKg >= Species.MinWeightKg
            && Finite(Species.MinTide) && Finite(Species.MaxTide)
            && UncoveredBelowMetres > Species.MinTide && UncoveredBelowMetres <= Species.MaxTide;

        private static bool ValidId(string value, string prefix) => value != null
            && Regex.IsMatch(value, @"\A" + prefix + @"\.[a-z][a-z0-9_]*\z");
        private static bool Positive(float value) => Finite(value) && value > 0f;
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
