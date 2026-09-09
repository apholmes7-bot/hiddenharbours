#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Art;

namespace HiddenHarbours.App.Editor
{
    /// <summary>
    /// <b>Ships the owner's grade levers</b> — creates <c>Assets/_Project/Resources/MoodGradeProfile.asset</c>
    /// (the five looks, at <see cref="MoodGradeProfile.ApplyDefaults"/>) and the per-region overrides under
    /// <c>Assets/_Project/Resources/MoodGrade/</c> when they are missing.
    ///
    /// <para>⚠️ <b>CREATE-ONLY, like <see cref="DayNightProfileBuilder"/>.</b> These assets are the owner's
    /// TUNING surface, not derived data: a builder that re-stamped the defaults over them would discard an
    /// evening of his art direction the next time anybody ran the menu. To take new code defaults
    /// deliberately, delete the asset and run this again.</para>
    ///
    /// <para>The shipped assets were hand-authored in the juice PR 1 from exactly these values; the asset
    /// test pins the FEATURE facts, not the numbers, so the owner can tune freely.</para>
    /// </summary>
    public static class MoodGradeProfileBuilder
    {
        const string MenuPath = "Hidden Harbours/Lighting/Create the Mood Grade Profile + regions (only if missing)";

        /// <summary>Where the profile lives — the path <c>MoodGradeDirector</c> loads by name.</summary>
        public const string ProfilePath = "Assets/_Project/Resources/MoodGradeProfile.asset";

        /// <summary>The folder the director loads every region override from.</summary>
        public const string RegionFolder = "Assets/_Project/Resources/MoodGrade";

        /// <summary>The coast as shipped (art bible §4.3): region id, tint, saturation, contrast, exposure,
        /// vignette, bloom. St Peters is the identity — the file exists so the owner has one to edit.</summary>
        public static readonly (string file, string id, Color tint, float sat, float con, float exp, float vig, float bloom)[] ShippedRegions =
        {
            ("StPeters",      "region.st_peters",       Color.white,                      0f,  0f, 0f,  0f,    0f),
            ("CoddleCove",    "region.coddle_cove",     new Color(1f, 0.98f, 0.95f, 1f),  4f,  0f, 0f,  0f,    0f),
            ("NineMileCreek", "region.nine_mile_creek", new Color(1f, 0.97f, 0.92f, 1f), 10f,  0f, 0f, -0.03f, 0.05f),
        };

        [MenuItem(MenuPath)]
        public static void Build()
        {
            var existing = AssetDatabase.LoadAssetAtPath<MoodGradeProfile>(ProfilePath);
            if (existing != null)
            {
                Debug.Log($"[mood-grade] {ProfilePath} already exists — left untouched (this builder never " +
                          "overwrites the owner's tuning). Delete it and re-run to take the code defaults.");
            }
            else
            {
                var profile = ScriptableObject.CreateInstance<MoodGradeProfile>();
                profile.ApplyDefaults();
                AssetDatabase.CreateAsset(profile, ProfilePath);
                Debug.Log($"[mood-grade] created {ProfilePath} at the shipped defaults.");
            }

            if (!AssetDatabase.IsValidFolder(RegionFolder))
                AssetDatabase.CreateFolder(Path.GetDirectoryName(RegionFolder).Replace('\\', '/'), Path.GetFileName(RegionFolder));

            foreach (var r in ShippedRegions)
            {
                string path = $"{RegionFolder}/{r.file}.asset";
                if (AssetDatabase.LoadAssetAtPath<MoodGradeRegionOverride>(path) != null)
                {
                    Debug.Log($"[mood-grade] {path} already exists — left untouched.");
                    continue;
                }
                var o = ScriptableObject.CreateInstance<MoodGradeRegionOverride>();
                o.Set(r.id, r.tint, r.sat, r.con, r.exp, r.vig, r.bloom);
                AssetDatabase.CreateAsset(o, path);
                Debug.Log($"[mood-grade] created {path} for {r.id}.");
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
    }
}
#endif
