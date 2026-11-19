#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Art;
using HiddenHarbours.Fishing;
using HiddenHarbours.Player;

namespace HiddenHarbours.App.Editor
{
    /// <summary>
    /// <b>The shared fish art table, built into a committed asset</b>
    /// (<c>Resources/FishArtLibrary.asset</c>, see <see cref="FishArtLibrary"/>): every species with its
    /// own rig art, its fight size ladder, and the catch art table, in one place every scene reaches
    /// without being rebuilt.
    ///
    /// <para><b>Everything is READ.</b> The ladders come from <see cref="RodKitImporter.BuildFishSpecies"/>,
    /// the very call the region builders make, handed the WHOLE shipped roster (every
    /// <see cref="FishSpeciesDef"/> under <c>Data/Fish</c>) rather than one region's; the catch art table
    /// is the committed <see cref="CatchItemLibraryBuilder.LibraryPath"/>. Re-run after a fish re-bake,
    /// after <c>Build Catch Item Library</c>.</para>
    /// </summary>
    public static class FishArtLibraryBuilder
    {
        const string MenuPath = "Hidden Harbours/Art/Import (after a new drop)/Build Fish Art Library";
        const string DataFish = "Assets/_Project/Data/Fish";

        /// <summary>Where the built asset lands: the Resources path <see cref="FishArtLibrary.Shared"/>
        /// loads.</summary>
        public const string LibraryPath = "Assets/_Project/Resources/" + FishArtLibrary.ResourcesPath + ".asset";

        [MenuItem(MenuPath, priority = 234)]
        public static void Build()
        {
            if (BuildLibrary(out string summary)) Debug.Log($"[FishArtLibraryBuilder] {summary}");
            else Debug.LogError($"[FishArtLibraryBuilder] {summary}");
        }

        /// <summary>
        /// Create or refresh <see cref="LibraryPath"/> in place (its guid kept). False, with nothing
        /// written, when no species ladder could be read at all: an empty table would quietly turn the
        /// fallback off everywhere.
        /// </summary>
        public static bool BuildLibrary(out string summary)
        {
            FishSpeciesDef[] roster = AssetDatabase.FindAssets("t:FishSpeciesDef", new[] { DataFish })
                .Select(g => AssetDatabase.LoadAssetAtPath<FishSpeciesDef>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(d => d != null && !string.IsNullOrEmpty(d.Id))
                .OrderBy(d => d.Id, System.StringComparer.Ordinal)
                .ToArray();

            FishSpeciesVisual[] species = RodKitImporter.BuildFishSpecies(roster)
                .OrderBy(s => s.FishId, System.StringComparer.Ordinal)
                .ToArray();
            if (species.Length == 0)
            {
                summary = "No fish size ladders could be read (sidecar or sheets missing) — refusing to " +
                          "write an empty table. Run the catch pass 2 bake and both slicers first.";
                return false;
            }

            var catchArt = AssetDatabase.LoadAssetAtPath<CatchItemLibrary>(CatchItemLibraryBuilder.LibraryPath);
            if (catchArt == null)
                Debug.LogWarning($"[FishArtLibraryBuilder] No catch art table at " +
                                 $"'{CatchItemLibraryBuilder.LibraryPath}': the hands' fallback stays empty. " +
                                 "Run Build Catch Item Library, then this.");

            var library = AssetDatabase.LoadAssetAtPath<FishArtLibrary>(LibraryPath);
            bool created = library == null;
            if (created) library = ScriptableObject.CreateInstance<FishArtLibrary>();
            library.Configure(species, catchArt);

            if (created) AssetDatabase.CreateAsset(library, LibraryPath);
            else EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();

            string[] without = roster.Where(d => d.Category != Core.FishCategory.Shellfish &&
                                                 species.All(s => s.FishId != d.Id))
                                     .Select(d => d.Id).ToArray();
            summary = $"{(created ? "Created" : "Refreshed")} '{LibraryPath}': {species.Length} species " +
                      $"ladders ({string.Join(", ", species.Select(s => $"{s.FishId}×{s.Rungs.Length}"))}), " +
                      $"catch art {(catchArt != null ? "wired" : "MISSING")}" +
                      (without.Length > 0 ? $"; finfish with no rig art of their own: {string.Join(", ", without)}" : "") +
                      ". Commit it.";
            return true;
        }
    }
}
#endif
