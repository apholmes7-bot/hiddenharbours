using System;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Art.Editor;

namespace HiddenHarbours.Tools.RigBaking
{
    /// <summary>
    /// Owner-facing entry points for the Rock Iso kit. One click bakes the sheets, force-reimports
    /// and slices them with their per-variant ground-contact pivots; nothing further to run.
    ///
    /// <para><b>Two menu items, because the atlas budget is a real decision.</b> The default bakes
    /// <c>sandstone</c> only — the shore-matching stone, ShoreIso2's <c>redrock</c> ramp verbatim —
    /// across all three tides: 6 species × 3 tides = 18 sheets. The full item bakes all four stones
    /// (72 sheets) and exists so that choice is made deliberately rather than by a default nobody
    /// looked at. The anchors are stone-independent either way, so the engine wiring does not change
    /// with the answer; only texture memory does.</para>
    /// </summary>
    public static class RockBakeMenu
    {
        public static string OutputFolder => RockIsoBaker.DefaultOutputFolder;

        [MenuItem("Hidden Harbours/Dev/Bake Rock Iso Sheets (sandstone × 3 tides = 18)",
                  priority = 140)]
        public static void BakeSandstone() =>
            Run(new[] { RockIsoBaker.DefaultStone }, "sandstone × 3 tides");

        [MenuItem("Hidden Harbours/Dev/Bake Rock Iso Sheets — ALL 4 stones (72 sheets)",
                  priority = 141)]
        public static void BakeEveryStone()
        {
            if (!EditorUtility.DisplayDialog(
                    "Bake every stone?",
                    "This writes 72 sheets — all six species across sandstone, granite, basalt and " +
                    "quartzite at three tides.\n\nThe anchors are stone-independent, so a shader " +
                    "tint may already cover some of the regional stones. Only sandstone matches the " +
                    "shoreline ramp.\n\nBake all four?",
                    "Bake all 72", "Cancel"))
                return;

            Run(RockIsoCatalog.Stones, "all 4 stones × 3 tides");
        }

        static void Run(string[] stones, string what)
        {
            RockBakeResult result;
            try
            {
                result = RockIsoBaker.Bake(stones, progress: (label, t) =>
                    EditorUtility.DisplayProgressBar($"Baking Rock Iso sheets ({what})", label, t));
            }
            catch (Exception ex)
            {
                Debug.LogError($"[rig-baker] rock bake FAILED: {ex.Message}\n{ex}");
                throw;
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            Debug.Log(Summarise(result, what));

            AssetDatabase.Refresh();

            // ⚠️ Reload every written file from disk BEFORE anything reads a texture: the sheets were
            // written with File.WriteAllBytes behind the AssetDatabase's back, and a mid-build import
            // invalidates any in-memory reference taken before it.
            foreach (var sheet in result.Sheets)
                AssetDatabase.ImportAsset(sheet.AssetPath, ImportAssetOptions.ForceUpdate);

            // Slice in the same operation. ArtImportPipeline stamps the pixel-art floor on first
            // import; RockSheetSlicer adds the Multiple-mode grid and the PER-VARIANT ground-contact
            // pivots, which the shared manifest cannot express.
            RockSheetSlicer.SliceAllMenu();

            bool verified = RockSheetSlicer.VerifyAll(logEachPass: false, out int checkedCount);
            Debug.Log(verified
                ? $"[rig-baker] Rock sheets verified: {checkedCount} sheet(s) — pivots, slice counts " +
                  "and import settings all agree with RockIso.json."
                : "[rig-baker] ⚠️ Rock sheet verification FAILED — see the errors above.");
        }

        static string Summarise(RockBakeResult result, string what)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"[rig-baker] Rock Iso bake ({what}) on {result.EngineName}:");
            foreach (var s in result.Sheets) sb.AppendLine("  " + s);
            sb.AppendLine(
                $"  {result.Sheets.Count} sheet(s), {result.CellsRendered} cell(s) rendered in " +
                $"{result.RenderMilliseconds:F0} ms (total {result.TotalMilliseconds:F0} ms), " +
                $"{result.TotalPngBytes / 1024.0:F0} KiB of PNG.");
            sb.Append(
                "  RockIso.json is NOT rewritten — the committed sidecar is the oracle this bake was " +
                "checked against (see RockIsoBaker.AssertMatchesContract).");
            return sb.ToString();
        }

        /// <summary>Batch entry point for <c>-executeMethod</c>: bake sandstone, then slice.</summary>
        public static void BakeSandstoneFromCommandLine()
        {
            try
            {
                var result = RockIsoBaker.Bake(new[] { RockIsoBaker.DefaultStone });
                Debug.Log(Summarise(result, "sandstone × 3 tides"));
                AssetDatabase.Refresh();
                foreach (var sheet in result.Sheets)
                    AssetDatabase.ImportAsset(sheet.AssetPath, ImportAssetOptions.ForceUpdate);
                RockSheetSlicer.SliceAllFromCommandLine();
            }
            catch (Exception e)
            {
                Debug.LogError($"[rig-baker] batch rock bake threw: {e}");
                EditorApplication.Exit(1);
            }
        }
    }
}
