using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// The (de)serialization layer for <see cref="SaveData"/> — JSON via Unity's <see cref="JsonUtility"/>
    /// behind a stable interface (tech-architecture.md §6: "JSON via a stable DTO layer … can move to a
    /// binary/compressed format later behind the same interface"). Pure and engine-light enough to unit
    /// test without a scene: round-trip and migration tests hit this directly.
    /// </summary>
    public static class SaveSerialization
    {
        /// <summary>Serialize a save to JSON. Pretty-printed so the M0 save file stays human-readable
        /// for debugging (tech-architecture.md §6).</summary>
        public static string ToJson(SaveData data) => JsonUtility.ToJson(data, prettyPrint: true);

        /// <summary>
        /// Parse JSON into a <see cref="SaveData"/> and run it through <see cref="SaveMigration.Migrate"/>
        /// so the caller always gets a current-shape blob. Returns null for null/blank/garbage input
        /// (load treats that as "no valid save" → start fresh, never throw).
        /// </summary>
        public static SaveData FromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;

            SaveData parsed;
            try
            {
                parsed = JsonUtility.FromJson<SaveData>(json);
            }
            catch
            {
                return null; // corrupt/non-save JSON — caller starts a new game rather than crashing.
            }

            return SaveMigration.Migrate(parsed);
        }
    }
}
