using System;
using System.Text;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Art.Editor;

namespace HiddenHarbours.Tools.RigBaking
{
    /// <summary>
    /// Owner-facing entry points for the Acadian Shrub kit.
    ///
    /// <para><b>⭐ THE DEFAULT RECIPE IS A DELIBERATE SLICE, NOT A LOOP BOUND.</b> The rig can make
    /// 20 species × 8 phases × 5 snow steps × 4 variants × 3 stages, which is hundreds of MB. This menu
    /// bakes the ONE slice worth proving the path on — the twenty species at <c>full</c> stage on the
    /// PHASE axis, variant 0 — and prints the budget arithmetic for everything it did not bake, so the
    /// atlas decision is the owner's and is made on numbers (the #312 pattern).</para>
    ///
    /// <para><b>Nothing here is run to ship pixels yet.</b> The kit is committed as rig + contract with
    /// no PNGs, so <see cref="ReportBudget"/> is the item the owner is actually expected to run first.</para>
    /// </summary>
    public static class ShrubBakeMenu
    {
        public static string OutputFolder => ShrubBaker.DefaultOutputFolder;

        // =====================================================================================
        // the contract — regenerate, never hand-edit
        // =====================================================================================

        /// <summary>
        /// Re-serialises <c>shrubIsoRig.contract.json</c> out of the live rig.
        ///
        /// <para>Separate from the bake ON PURPOSE: the contract is the oracle
        /// <c>ShrubBaker.AssertMatchesContract</c> refuses against, and a baker that rewrote it on the
        /// way past could never refuse. Run this when the art director ships a new rig — then read the
        /// diff before committing it, because a changed cell or pivot means every placed shrub moved.</para>
        /// </summary>
        [MenuItem("Hidden Harbours/Dev/Export Shrub Contract (from the live rig)", priority = 83)]
        public static void ExportShrubContract()
        {
            try
            {
                string path = ShrubBaker.ExportContract();
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                Debug.Log($"[shrub-baker] Wrote {path} straight out of " +
                          $"{ShrubCatalog.RigScriptPath}. ⚠️ READ THE DIFF before committing: a " +
                          "changed cell or pivot means every placed shrub in every scene moved.");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[shrub-baker] contract export FAILED: {ex.Message}\n{ex}");
                throw;
            }
        }

        public static void ExportShrubContractFromCommandLine()
        {
            try
            {
                ShrubBaker.ExportContract();
                EditorApplication.Exit(0);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[shrub-baker] headless contract export failed: {ex}");
                EditorApplication.Exit(1);
            }
        }

        // =====================================================================================
        // the budget — the item to run BEFORE deciding what to bake
        // =====================================================================================

        /// <summary>
        /// Prints the rig's own <c>sheetAudit()</c>: per species union cell, worst ink ratio and which
        /// phase caused it, both sheet sizes, headroom under the 2048 cap, and the total MiB. Costs no
        /// disk and writes nothing.
        /// </summary>
        [MenuItem("Hidden Harbours/Dev/Report Shrub Sheet Budget", priority = 82)]
        public static void ReportBudget()
        {
            using IRigScriptHost host = RigScriptHostFactory.Create();
            ShrubBaker.InstallRig(host);

            var sb = new StringBuilder();
            foreach (string stage in ShrubCatalog.StageKeys)
                sb.AppendLine(ShrubBaker.SummariseBudget(host, stage)).AppendLine();

            sb.AppendLine(
                "⭐ This kit ships NO pixels. Which slice of 20 species × 8 phases × 5 snow steps × " +
                "4 variants × 3 stages actually ships is an ATLAS BUDGET decision — decide it on the " +
                "numbers above, then bake that slice.");
            Debug.Log(sb.ToString().TrimEnd());
        }

        // =====================================================================================
        // the bake
        // =====================================================================================

        [MenuItem("Hidden Harbours/Dev/Bake Shrub Sheets (20 species × 8 phases × 3 channels)",
                  priority = 146)]
        public static void BakeShrubSheets()
        {
            ShrubBakeResult result;
            try
            {
                result = ShrubBaker.BakePhaseAxis(progress: (label, t) =>
                    EditorUtility.DisplayProgressBar("Baking Acadian shrub sheets", label, t));
            }
            catch (Exception ex)
            {
                Debug.LogError($"[shrub-baker] shrub bake FAILED: {ex.Message}\n{ex}");
                throw;
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            Debug.Log(Summarise(result));

            AssetDatabase.Refresh();

            // ⚠️ Reload every written file from disk BEFORE anything reads a texture: the sheets were
            // written with File.WriteAllBytes behind the AssetDatabase's back, and a mid-build import
            // invalidates any in-memory reference taken before it.
            foreach (var sheet in result.Sheets)
            foreach (string path in sheet.AssetPaths)
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            ShrubSheetSlicer.SliceAllMenu();

            bool verified = ShrubSheetSlicer.VerifyAll(logEachPass: false);
            Debug.Log(verified
                ? "[shrub-baker] Shrub sheets verified: pivots, mesh type, colour space and cell " +
                  $"sizes all agree with {ShrubCatalog.ContractFileName}."
                : "[shrub-baker] ⚠️ Shrub sheet VERIFY reported problems — see the errors above. Do " +
                  "not commit until they are clear.");

            Debug.Log("[shrub-baker] Baked and sliced. ⚠️ These sheets are NOT committed by default — " +
                      "this kit ships to order. If a slice is meant to ship, commit every *.png + its " +
                      $".meta (LFS covers *.png) under {OutputFolder}/ and say so in the PR.");
        }

        /// <summary>Headless entry point for CI / <c>-executeMethod</c>.
        /// <para>⚠️ Never invoke with <c>-quit</c> alongside <c>-runTests</c> — the two race and exit 0
        /// with <c>total=0</c>, which reads as a pass (ADR 0021).</para></summary>
        public static void BakeShrubSheetsFromCommandLine()
        {
            try
            {
                BakeShrubSheets();
                EditorApplication.Exit(0);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[shrub-baker] headless shrub bake failed: {ex}");
                EditorApplication.Exit(1);
            }
        }

        /// <summary>The bake's provenance, logged so a run is never inferred from its output.</summary>
        public static string Summarise(ShrubBakeResult r)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"[shrub-baker] Acadian shrubs — {r.Sheets.Count} sheet(s), " +
                          $"{r.CellsRendered} cell render(s), {r.TotalPngBytes / 1024} KiB of PNG");
            sb.AppendLine($"  engine     : {r.EngineName}");
            sb.AppendLine($"  rig        : {ShrubCatalog.RigScriptPath}");
            sb.AppendLine($"  contract   : {ShrubCatalog.ContractPath} (the ORACLE — refused against, " +
                          "never rewritten by a bake)");
            sb.AppendLine($"  sway rows  : {ShrubBaker.SwayRowsBaked}, the rig's own SWAY — this kit's " +
                          "contract is serialised from the rig and cannot describe fewer " +
                          "(see ShrubBaker.SwayRowsBaked)");
            sb.AppendLine($"  render     : {r.RenderMilliseconds:F0} ms of {r.TotalMilliseconds:F0} ms total");

            foreach (var s in r.Sheets)
                sb.AppendLine($"  {s.Species,-20} {s.Width,4}×{s.Height,-4} " +
                              $"{s.Cols} {s.Axis} col(s) × {s.Rows} sway row(s)  [{s.Fixed}]  " +
                              $"{s.AssetPaths.Count} channel(s)");
            return sb.ToString().TrimEnd();
        }
    }
}
