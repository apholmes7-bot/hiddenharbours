using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.Tools.RigBaking
{
    /// <summary>
    /// Owner-facing entry points for the catch-STORAGE sheets — containers that visibly fill with
    /// the player's actual catch. One click bakes, force-reimports and slices; nothing further to
    /// run. Same philosophy as <see cref="FishingKitBakeMenu"/>: the recipes are fixed decisions,
    /// the geometry comes from the rigs at bake time.
    /// </summary>
    public static class CatchStorageBakeMenu
    {
        public const string OutputFolder = CatchStorageBaker.DefaultOutputFolder;

        [MenuItem("Hidden Harbours/Art/Bake Catch Storage Kit (items + tote + buckets)", priority = 47)]
        public static void BakeCatchStorageKit()
        {
            RunBakes(BakeItemsInternal, BakeToteInternal, BakeBucketsInternal);
        }

        [MenuItem("Hidden Harbours/Art/Bake Catch Item Sheets (lobster, crab, mussel, clam)", priority = 48)]
        public static void BakeCatchItemSheets() => RunBakes(BakeItemsInternal);

        [MenuItem("Hidden Harbours/Art/Bake Fish Tote Sheets (5 colours × 3 lids × 8 dir + mask + anchors)", priority = 49)]
        public static void BakeFishToteSheets() => RunBakes(BakeToteInternal);

        [MenuItem("Hidden Harbours/Art/Bake Bucket Sheets (3 tiers × fills × catches × 8 dir)", priority = 50)]
        public static void BakeBucketSheets() => RunBakes(BakeBucketsInternal);

        static FishingBakeResult BakeItemsInternal() =>
            CatchStorageBaker.BakeCatchItems(progress: (label, t) =>
                EditorUtility.DisplayProgressBar("Baking catch item sheets", label, t));

        static FishingBakeResult BakeToteInternal() =>
            CatchStorageBaker.BakeTote(progress: (label, t) =>
                EditorUtility.DisplayProgressBar("Baking fish tote sheets", label, t));

        static FishingBakeResult BakeBucketsInternal() =>
            CatchStorageBaker.BakeBuckets(progress: (label, t) =>
                EditorUtility.DisplayProgressBar("Baking bucket sheets", label, t));

        static void RunBakes(params Func<FishingBakeResult>[] bakes)
        {
            var results = new List<FishingBakeResult>();
            try
            {
                foreach (var bake in bakes)
                {
                    var r = bake();
                    results.Add(r);
                    Debug.Log(FishingKitBakeMenu.Summarise(r));
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[rig-baker] catch storage bake FAILED: {ex.Message}\n{ex}");
                throw;
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            AssetDatabase.Refresh();

            // Reload from disk before anything wires a serialized reference to these sprites —
            // the sheets were written by File.WriteAllBytes behind the AssetDatabase's back.
            foreach (var r in results)
            {
                foreach (var sheet in r.Sheets)
                    AssetDatabase.ImportAsset(sheet.AssetPath, ImportAssetOptions.ForceUpdate);
                if (!string.IsNullOrEmpty(r.AnchorJsonPath))
                    AssetDatabase.ImportAsset(r.AnchorJsonPath, ImportAssetOptions.ForceUpdate);
            }

            // Slice in the same operation. ArtImportPipeline stamps the pixel-art import lock on
            // first import; CatchStorageSheetSlicer adds the Multiple-mode grid and each kit's
            // own pivot. Every sheet here is ≤ 2048 px (the widest is 256 px; the tallest 576).
            Art.Editor.CatchStorageSheetSlicer.SliceAllMenu();

            Debug.Log("[rig-baker] Baked and sliced. Nothing further to run — but the results " +
                      "must be COMMITTED: every *.png + its .meta (LFS covers *.png) and " +
                      $"{CatchStorageBaker.AnchorsFileName} + its .meta, all under {OutputFolder}/.");
        }

        /// <summary>
        /// Headless entry point for CI / -executeMethod.
        ///
        /// ⚠️ Never invoke this with -quit alongside -runTests — the two race and exit 0 with
        /// total=0, which reads as a pass (ADR 0021).
        /// </summary>
        public static void BakeCatchStorageFromCommandLine()
        {
            try
            {
                BakeCatchStorageKit();
                EditorApplication.Exit(0);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[rig-baker] headless catch storage bake failed: {ex}");
                EditorApplication.Exit(1);
            }
        }
    }
}
