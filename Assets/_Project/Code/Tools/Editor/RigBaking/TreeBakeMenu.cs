using System;
using System.Collections.Generic;
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
    ///
    /// <para><b>Pass 4</b> splits that click in two, ShrubBaker's way: <c>Export Acadian Tree
    /// Contract</c> surveys rig 4 and writes <c>Trees.json</c>; <c>Bake Acadian Trees</c> then
    /// obeys it and refuses a rig that drifted from it. Which kit the bake builds follows
    /// <see cref="TreeKitCatalog.IsPass4Live"/>, so until <see cref="TreeKitCatalog.RigScriptPath"/>
    /// names rig 4 this menu bakes pass 3 exactly as before and the export stays greyed out.</para>
    /// </summary>
    public static class TreeBakeMenu
    {
        public static string OutputFolder => TreeRigBaker.DefaultOutputFolder;

        const string ExportMenu = "Hidden Harbours/Art/Export Acadian Tree Contract (pass 4)";

        /// <summary>The headless seasons switch: <c>-treeSeasons summer,winter</c>. Without it the
        /// export covers <see cref="TreePass4Baker.DefaultSeasons"/>.</summary>
        public const string SeasonsArg = "-treeSeasons";

        [MenuItem("Hidden Harbours/Art/Bake Acadian Trees", priority = 51)]
        public static void BakeAcadianTrees()
        {
            if (TreeKitCatalog.IsPass4Live)
            {
                BakePass4();
                return;
            }

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

            SliceAndVerify();
        }

        static void BakePass4()
        {
            TreePass4BakeResult result;
            try
            {
                result = TreePass4Baker.Bake(progress: (label, t) =>
                    EditorUtility.DisplayProgressBar("Baking Acadian tree sheets (pass 4)", label, t));
            }
            catch (Exception ex)
            {
                Debug.LogError($"[rig-baker] pass-4 tree bake FAILED — nothing was written: {ex.Message}\n{ex}");
                throw;
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            Debug.Log(Summarise(result));

            AssetDatabase.Refresh();

            // The same discipline as pass 3: everything written behind the AssetDatabase's back is
            // re-imported before anything reads it. The contract was not written by the bake.
            foreach (var sheet in result.Sheets)
                AssetDatabase.ImportAsset(sheet.AssetPath, ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(result.PalettePath, ImportAssetOptions.ForceUpdate);

            SliceAndVerify();
        }

        static void SliceAndVerify()
        {
            // Slice in the same operation. ArtImportPipeline stamps the pixel-art floor on first
            // import; TreeSheetSlicer adds the Multiple-mode grid, the trunk-foot pivot, the mesh
            // type and — for the data channels — sRGB OFF.
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

        [MenuItem(ExportMenu, priority = 50)]
        public static void ExportAcadianTreeContract() => ExportContract(SeasonsFromCommandLine());

        /// <summary>Greyed out while the game draws pass 3: the export writes the live
        /// <c>Trees.json</c>, and <see cref="TreePass4Baker"/> refuses that until the switch.</summary>
        [MenuItem(ExportMenu, true)]
        static bool CanExportAcadianTreeContract() => TreeKitCatalog.IsPass4Live;

        /// <summary>Surveys rig 4 over <paramref name="seasons"/> and writes the live contract.
        /// Writes no sheet: <see cref="BakeAcadianTrees"/> is the next step.</summary>
        public static TreePass4ExportResult ExportContract(IReadOnlyList<string> seasons)
        {
            TreePass4ExportResult result;
            try
            {
                result = TreePass4Baker.ExportContract(seasons: seasons, progress: (label, t) =>
                    EditorUtility.DisplayProgressBar("Exporting the Acadian tree contract", label, t));
            }
            catch (Exception ex)
            {
                Debug.LogError($"[rig-baker] tree contract export FAILED: {ex.Message}\n{ex}");
                throw;
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            AssetDatabase.ImportAsset(result.ContractPath, ImportAssetOptions.ForceUpdate);
            Debug.Log(Summarise(result));
            return result;
        }

        /// <summary>The seasons named by <see cref="SeasonsArg"/>, or the default set.</summary>
        public static IReadOnlyList<string> SeasonsFromCommandLine()
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (string.Equals(args[i], SeasonsArg, StringComparison.Ordinal))
                    return args[i + 1].Split(',');
            return TreePass4Baker.DefaultSeasons;
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

        /// <summary>
        /// Headless pass-4 entry point: export the contract, then bake it, in one editor run
        /// (<c>-executeMethod … -treeSeasons summer,winter</c>). Refused, like the menu, until the
        /// switch.
        /// </summary>
        public static void ExportAndBakeAcadianTreesFromCommandLine()
        {
            try
            {
                if (!TreeKitCatalog.IsPass4Live)
                    throw new InvalidOperationException(
                        $"The game still draws pass 3 ({TreeKitCatalog.RigScriptPath}); switch " +
                        $"TreeKitCatalog.RigScriptPath to {TreeKitCatalog.Pass4RigScriptPath} first.");
                ExportContract(SeasonsFromCommandLine());
                BakeAcadianTrees();
                EditorApplication.Exit(0);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[rig-baker] headless tree export + bake failed: {ex}");
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

        /// <summary>The pass-4 bake's provenance: what was written and what it costs the GPU.</summary>
        public static string Summarise(TreePass4BakeResult r)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"[rig-baker] Acadian trees, pass 4 — {r.Sheets.Count} sheet(s) + the " +
                          $"palette ({r.PaletteRows} row(s)), {r.CellsRead} cell(s) read, " +
                          $"{r.TotalPngBytes / 1024} KiB of PNG, " +
                          $"{r.TextureBytes / 1e6:F2} MB as imported");
            sb.AppendLine($"  engine     : {r.EngineName}");
            sb.AppendLine($"  rig        : {TreeKitCatalog.Pass4RigScriptPath} + {TreeKitCatalog.Pass4MapsScriptPath}");
            sb.AppendLine($"  glue       : {TreeKitCatalog.Pass4GlueGlobalName} v{TreePass4Glue.Version}");
            sb.AppendLine($"  contract   : {r.ContractPath} (read, not written)");
            sb.AppendLine($"  palette    : {r.PalettePath}");
            sb.AppendLine($"  glue time  : {r.GlueMilliseconds:F0} ms of {r.TotalMilliseconds:F0} ms total");
            AppendSpecies(sb, r.Contract);
            return sb.ToString().TrimEnd();
        }

        /// <summary>The export's provenance: the geometry and the wind response it recorded.</summary>
        public static string Summarise(TreePass4ExportResult r)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"[rig-baker] Acadian tree contract, pass 4 — {r.Contract?.trees?.Length ?? 0} " +
                          $"species, {r.Contract?.palette?.rows ?? 0} palette row(s)");
            sb.AppendLine($"  engine     : {r.EngineName}");
            sb.AppendLine($"  contract   : {r.ContractPath}");
            sb.AppendLine($"  snow row   : {string.Join(" ", r.Contract?.snowRow ?? Array.Empty<string>())}");
            sb.AppendLine($"  survey     : {r.SurveyMilliseconds:F0} ms of {r.TotalMilliseconds:F0} ms total");
            AppendSpecies(sb, r.Contract);
            return sb.ToString().TrimEnd();
        }

        static void AppendSpecies(StringBuilder sb, TreeKitCatalog.Contract contract)
        {
            if (contract?.trees == null) return;
            sb.AppendLine("  species            cell     trunk foot   pad  _TrunkAnchor  reach  bendPx  limbPx  sheet      seasons");
            foreach (var e in contract.trees)
            {
                var seasons = new StringBuilder();
                foreach (var row in e.seasonRows ?? Array.Empty<TreeKitCatalog.SeasonRow>())
                    seasons.Append($"{row.season}(maps {row.maps}, albedo {row.albedo}, row {row.paletteRow}) ");
                sb.AppendLine($"  {e.species,-16} {e.cellW,4}×{e.cellH,-4} " +
                              $"({e.pivotX,3},{e.pivotY,3})  {e.nearFlarePad,4}  " +
                              $"{e.trunkAnchor,12:F4}  {e.wind?.windReach ?? 0,5}  " +
                              $"{e.wind?.bendPx ?? 0f,6:F2}  {e.wind?.limbPx ?? 0f,6:F3}  " +
                              $"{e.sheetW,4}×{e.sheetH,-4}  {seasons.ToString().TrimEnd()}");
            }
        }
    }
}
