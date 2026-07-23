using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.Tools.RigBaking
{
    /// <summary>
    /// Owner-facing entry points for the fishing kit's fight sheets — Rod Fishing v2 wave 3. One
    /// click per rig (or one for the whole kit) bakes, force-reimports and slices; nothing further
    /// to run. Fixed recipes, same philosophy as <see cref="CharacterRigBakeMenu"/>: the dials that
    /// matter (which species / states / tiers) are decisions already made, and the geometry (cells,
    /// frame counts, pivots) comes from the rigs at bake time.
    /// </summary>
    public static class FishingKitBakeMenu
    {
        public const string OutputFolder = FishingKitBaker.DefaultOutputFolder;

        [MenuItem("Hidden Harbours/Art/Bake Fishing Kit (fish + bobber + rod)", priority = 43)]
        public static void BakeFishingKit()
        {
            RunBakes(BakeFishInternal, BakeBobberInternal, BakeRodInternal);
        }

        [MenuItem("Hidden Harbours/Art/Bake Fish Sheets (4 species × 8 states × 8 dir)", priority = 44)]
        public static void BakeFishSheets() => RunBakes(BakeFishInternal);

        [MenuItem("Hidden Harbours/Art/Bake Bobber Sheets (4 states)", priority = 45)]
        public static void BakeBobberSheets() => RunBakes(BakeBobberInternal);

        [MenuItem("Hidden Harbours/Art/Bake Rod Sheets (3 tiers × 9 states × 8 dir)", priority = 46)]
        public static void BakeRodSheets() => RunBakes(BakeRodInternal);

        static FishingBakeResult BakeFishInternal() =>
            FishingKitBaker.BakeFish(progress: (label, t) =>
                EditorUtility.DisplayProgressBar("Baking fish sheets", label, t));

        static FishingBakeResult BakeBobberInternal() =>
            FishingKitBaker.BakeBobber(progress: (label, t) =>
                EditorUtility.DisplayProgressBar("Baking bobber sheets", label, t));

        static FishingBakeResult BakeRodInternal() =>
            FishingKitBaker.BakeRod(progress: (label, t) =>
                EditorUtility.DisplayProgressBar("Baking rod sheets", label, t));

        static void RunBakes(params Func<FishingBakeResult>[] bakes)
        {
            var results = new List<FishingBakeResult>();
            try
            {
                foreach (var bake in bakes)
                {
                    var r = bake();
                    results.Add(r);
                    Debug.Log(Summarise(r));
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[rig-baker] fishing kit bake FAILED: {ex.Message}\n{ex}");
                throw;
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            AssetDatabase.Refresh();

            // Reload from disk before anything wires a serialized reference to these sprites: a
            // mid-build import can invalidate in-memory refs, and the sheets were written by
            // File.WriteAllBytes behind the AssetDatabase's back.
            foreach (var r in results)
            {
                foreach (var sheet in r.Sheets)
                    AssetDatabase.ImportAsset(sheet.AssetPath, ImportAssetOptions.ForceUpdate);
                if (!string.IsNullOrEmpty(r.AnchorJsonPath))
                    AssetDatabase.ImportAsset(r.AnchorJsonPath, ImportAssetOptions.ForceUpdate);
            }

            // Slice in the same operation rather than leaving it as a step the owner has to
            // remember. ArtImportPipeline stamps the pixel-art import lock (PPU 32, Point,
            // Uncompressed) on first import; FishingSheetSlicer adds the Multiple-mode grid and
            // each kit's own pivot (fish = water-surface point, bobber = waterline, rod = grip).
            // Every sheet is ≤ 2048 px — no cap lift needed (and FishingKitBaker refuses anything
            // over the default cap at bake time).
            Art.Editor.FishingSheetSlicer.SliceAllMenu();

            Debug.Log("[rig-baker] Baked and sliced. Nothing further to run — but the results must " +
                      "be COMMITTED: every *.png + its .meta (LFS covers *.png) and every " +
                      $"*Anchors.json + its .meta, all under {OutputFolder}/.");
        }

        public static string Summarise(FishingBakeResult r)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"[rig-baker] fishing kit — {r.RigKey}");
            sb.AppendLine($"  engine      : {r.EngineName}");
            sb.AppendLine($"  rig         : {r.RigKey}  {r.Geometry}");
            sb.AppendLine(r.MeasuredConvention is { } c
                ? $"  convention  : MEASURED {c} -> emitted with the one-place correction " +
                  "(FacingsAreCounterClockwise = false)"
                : "  convention  : not directional (no azimuth term, never probed)");
            sb.AppendLine($"  cells       : {r.CellsRendered} across {r.Sheets.Count} sheets");
            foreach (var s in r.Sheets) sb.AppendLine($"  sheet       : {s}");
            sb.AppendLine($"  anchors     : {r.AnchorJsonPath}");
            sb.AppendLine($"  render time : {r.RenderMilliseconds:F0} ms " +
                          $"({r.RenderMilliseconds / Mathf.Max(1, r.CellsRendered):F2} ms/cell)");
            sb.AppendLine($"  total time  : {r.TotalMilliseconds / 1000.0:F2} s");
            sb.Append    ($"  png on disk : {r.TotalPngBytes / 1024.0:F0} KB");
            return sb.ToString();
        }

        /// <summary>
        /// Headless entry point for CI / -executeMethod.
        ///
        /// ⚠️ Never invoke this with -quit alongside -runTests. The two race: Unity exits 0 having
        /// written total=0, which reads as a pass. That trap is recorded in ADR 0021 and it would
        /// quietly poison any job written that way.
        /// </summary>
        public static void BakeFishingKitFromCommandLine()
        {
            try
            {
                BakeFishingKit();
                EditorApplication.Exit(0);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[rig-baker] headless fishing kit bake failed: {ex}");
                EditorApplication.Exit(1);
            }
        }
    }
}
