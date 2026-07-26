using System;
using System.Text;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Art.Editor;

namespace HiddenHarbours.Tools.RigBaking
{
    /// <summary>
    /// Owner-facing entry point for the Acadian tree kit. One click bakes the sheets, writes
    /// <c>Trees.json</c>, force-reimports and slices; nothing further to run. Same philosophy as
    /// <see cref="CatchStorageBakeMenu"/> — the recipe is a fixed decision, the geometry comes from
    /// the rig at bake time.
    /// </summary>
    public static class TreeBakeMenu
    {
        public static string OutputFolder => TreeRigBaker.DefaultOutputFolder;

        [MenuItem("Hidden Harbours/Art/Bake Acadian Trees (10 species × 4 variants × 3 channels)",
                  priority = 51)]
        public static void BakeAcadianTrees()
        {
            TreeBakeResult result;
            try
            {
                result = TreeRigBaker.Bake(progress: (label, t) =>
                    EditorUtility.DisplayProgressBar("Baking Acadian tree sheets", label, t));
            }
            catch (Exception ex)
            {
                Debug.LogError($"[rig-baker] tree bake FAILED: {ex.Message}\n{ex}");
                throw;
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            Debug.Log(Summarise(result));

            AssetDatabase.Refresh();

            // ⚠️ Reload every written file from disk BEFORE anything reads a texture or wires a
            // serialized reference: the sheets were written with File.WriteAllBytes behind the
            // AssetDatabase's back, and a mid-build import invalidates any in-memory reference
            // taken before it.
            foreach (var sheet in result.Sheets)
                AssetDatabase.ImportAsset(sheet.AssetPath, ImportAssetOptions.ForceUpdate);
            if (!string.IsNullOrEmpty(result.ContractPath))
                AssetDatabase.ImportAsset(result.ContractPath, ImportAssetOptions.ForceUpdate);

            // Slice in the same operation. ArtImportPipeline stamps the pixel-art floor on first
            // import; TreeSheetSlicer adds the Multiple-mode grid, the trunk-foot pivot, Tight mesh
            // and — for the two data channels — sRGB OFF.
            TreeSheetSlicer.SliceAllMenu();

            bool verified = TreeSheetSlicer.VerifyAll(logEachPass: false);
            Debug.Log(verified
                ? "[rig-baker] Tree sheets verified: pivots, mesh type, colour space and slice " +
                  "counts all agree with Trees.json."
                : "[rig-baker] ⚠️ Tree sheet VERIFY reported problems — see the errors above. Do " +
                  "not commit until they are clear.");

            Debug.Log("[rig-baker] Baked and sliced. Nothing further to run — but the results must " +
                      $"be COMMITTED: every *.png + its .meta (LFS covers *.png) and " +
                      $"{TreeKitCatalog.ContractFileName} + its .meta, all under {OutputFolder}/.");
        }

        /// <summary>
        /// Headless entry point for CI / <c>-executeMethod</c>.
        ///
        /// <para>⚠️ Never invoke this with <c>-quit</c> alongside <c>-runTests</c> — the two race and
        /// exit 0 with <c>total=0</c>, which reads as a pass (ADR 0021).</para>
        /// </summary>
        public static void BakeAcadianTreesFromCommandLine()
        {
            try
            {
                BakeAcadianTrees();
                EditorApplication.Exit(0);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[rig-baker] headless tree bake failed: {ex}");
                EditorApplication.Exit(1);
            }
        }

        /// <summary>The bake's provenance, logged so a run is never inferred from its output.</summary>
        public static string Summarise(TreeBakeResult r)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"[rig-baker] Acadian trees — {r.Sheets.Count} sheet(s), " +
                          $"{r.CellsRendered} cell render(s), {r.TotalPngBytes / 1024} KiB of PNG");
            sb.AppendLine($"  engine     : {r.EngineName}");
            sb.AppendLine($"  rig        : {TreeKitCatalog.RigScriptPath}");
            sb.AppendLine($"  contract   : {r.ContractPath}");
            sb.AppendLine($"  sway rows  : {TreeRigBaker.SwayRowsBaked} of " +
                          $"{r.Contract?.sheet?.rigSwayRows ?? 0} the rig can make " +
                          "(the shader owns the swaying — see TreeRigBaker.SwayRowsBaked)");
            sb.AppendLine($"  render     : {r.RenderMilliseconds:F0} ms of {r.TotalMilliseconds:F0} ms total");

            if (r.Contract?.trees != null)
            {
                sb.AppendLine("  species            cell     trunk foot   pad  _TrunkAnchor  metres  sheet");
                foreach (var e in r.Contract.trees)
                    sb.AppendLine($"  {e.species,-16} {e.cellW,4}×{e.cellH,-4} " +
                                  $"({e.pivotX,3},{e.pivotY,3})  {e.nearFlarePad,4}  " +
                                  $"{e.trunkAnchor,12:F4}  {e.metres,6:F1}  {e.sheetW}×{e.sheetH}");
            }
            return sb.ToString().TrimEnd();
        }
    }
}
