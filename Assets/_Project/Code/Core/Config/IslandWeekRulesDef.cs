using System;
using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>Progress requirements only; this data grants no passage or dialogue.</summary>
    [CreateAssetMenu(fileName = "IslandWeek", menuName = "Progress/Island Week Rules")]
    public sealed class IslandWeekRulesDef : ScriptableObject
    {
        public string Id;
        public string DoryId;
        public string LicenceId;
        [Min(1)] public int EarliestDay;
        public FishCategory[] FinfishCategories = Array.Empty<FishCategory>();
        public string DoryMendedTextKey;
        public string CodLicenceTextKey;
        public string FishFromDoryTextKey;
        public string ShellfishSoldTextKey;
        public string DayReachedTextKey;

        public string ValidationError()
        {
            if (!HasPrefix(Id, "island_week.")) return "Expected an island_week.* id.";
            if (!HasPrefix(DoryId, "boat.")) return "Expected a boat.* id.";
            if (!HasPrefix(LicenceId, "license.")) return "Expected a license.* id.";
            if (EarliestDay < 1) return "The earliest day is one-based.";
            if (FinfishCategories == null || FinfishCategories.Length == 0)
                return "At least one finfish category is required.";
            for (int i = 0; i < FinfishCategories.Length; i++)
            {
                FishCategory category = FinfishCategories[i];
                if (!Enum.IsDefined(typeof(FishCategory), category) || category == FishCategory.Shellfish)
                    return "Finfish categories must be known and cannot include shellfish.";
                for (int j = 0; j < i; j++)
                    if (FinfishCategories[j] == category) return "Duplicate finfish category.";
            }
            string[] keys = { DoryMendedTextKey, CodLicenceTextKey, FishFromDoryTextKey,
                              ShellfishSoldTextKey, DayReachedTextKey };
            for (int i = 0; i < keys.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(keys[i])) return "Every fact needs a text key.";
                for (int j = 0; j < i; j++)
                    if (keys[i] == keys[j]) return "Fact text keys must be distinct.";
            }
            return null;
        }

        private static bool HasPrefix(string id, string prefix)
            => !string.IsNullOrWhiteSpace(id) && id.StartsWith(prefix, StringComparison.Ordinal)
               && id.Length > prefix.Length;
    }
}
