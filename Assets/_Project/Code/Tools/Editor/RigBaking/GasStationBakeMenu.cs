using System;
using System.Linq;
using System.Text;
using HiddenHarbours.Art.Editor;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.Tools.RigBaking
{
    /// <summary>
    /// Owner-facing entry points for the gas-station kit.
    ///
    /// <para>Menu priority 78 continues the Art-menu bake run (…72 shop fixtures, 74 camper, 76
    /// bubbles, 77 notebook). Unity draws a separator at any priority gap greater than 10, so an item
    /// placed carelessly renders in a neighbouring group and reads as MISSING.</para>
    /// </summary>
    public static class GasStationBakeMenu
    {
        [MenuItem("Hidden Harbours/Art/Bake Gas Station Kit (21 pieces + 2 sales floors)",
                  priority = 78)]
        public static void BakeAll()
        {
            try
            {
                var report = GasStationSheetBaker.BakeAll();

                AssetDatabase.Refresh();

                // Reload from disk before anything reads or slices these sprites — the sheets were
                // written by File.WriteAllBytes behind the AssetDatabase's back, so without this the
                // slicer measures the PREVIOUS import.
                foreach (var s in report.Sheets)
                    AssetDatabase.ImportAsset(s.AssetPath, ImportAssetOptions.ForceUpdate);
                AssetDatabase.ImportAsset(GasStationKit.ContractPath, ImportAssetOptions.ForceUpdate);

                // Slice in the same operation. ArtImportPipeline stamps the pixel-art import lock on
                // first import; the slicer adds the Multiple-mode grid and the contract's pivots.
                GasStationSheetSlicer.SliceAll();

                Debug.Log(Summarise(report));
                Debug.Log("[rig-baker] Baked and sliced. The results must be COMMITTED: every *.png " +
                          "+ its .meta (LFS covers *.png) and GasStation.json + its .meta, all under " +
                          $"{GasStationKit.OutputFolder}/.");
            }
            catch (Exception e)
            {
                Debug.LogError($"Gas station bake FAILED: {e.Message}\n{e}");
                throw;
            }
        }

        /// <summary>
        /// Reports the budget WITHOUT writing anything — the numbers that decide the facing axis,
        /// available before committing 23 sheets to disk.
        /// </summary>
        [MenuItem("Hidden Harbours/Dev/Report Gas Station Sheet Budget", priority = 148)]
        public static void ReportBudget()
        {
            var builds = GasStationKit.Builds;
            var sb = new StringBuilder();
            sb.AppendLine($"Gas station kit — {builds.Count} sheets planned");
            sb.AppendLine($"  grades [{string.Join(", ", GasStationKit.BakedGrades)}] · " +
                          $"wear '{GasStationKit.BakedWear}' · lit {GasStationKit.BakedLit} · " +
                          $"facings {GasStationKit.Facings} " +
                          $"(folded {GasStationKit.FoldedFacings}: " +
                          $"{string.Join(", ", GasStationKit.Fold180Types)})");

            foreach (var byType in builds.GroupBy(b => b.Type))
            {
                int facings = byType.Key == "interior"
                                  ? GasStationKit.Facings
                                  : GasStationKit.FacingsFor(byType.Key);
                string fold = GasStationKit.IsFolded(byType.Key) ? "  (180° fold)" : "";
                sb.AppendLine($"  {byType.Key,-10} {byType.Count(),2} sheets × {facings} facings{fold}");
            }

            Debug.Log(sb.ToString());
        }

        public static string Summarise(GasStationSheetBaker.BakeReport r)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Gas station kit baked — {r.Sheets.Count} sheets, {r.TotalPixels:N0} px, " +
                          $"{r.RuntimeMegabytes:F1} MB RGBA32 at runtime, " +
                          $"{r.TotalPngBytes / 1024.0 / 1024.0:F2} MiB of PNG on disk " +
                          $"({r.TotalMilliseconds / 1000.0:F1} s, {r.EngineName})");
            sb.AppendLine($"  {r.Probe}");

            foreach (var byType in r.Sheets.GroupBy(s => s.Type))
            {
                long px = byType.Sum(s => (long)s.CroppedPixels);
                sb.AppendLine($"  {byType.Key,-10} {byType.Count(),2} sheets  {px,10:N0} px  " +
                              $"({100.0 * px / r.TotalPixels,4:F1}% of the kit)");
            }

            var worst = r.Sheets.OrderByDescending(s => s.SheetW * s.SheetH).First();
            sb.AppendLine($"  largest sheet: {worst.Key} at {worst.SheetW}×{worst.SheetH} " +
                          $"in a {worst.Columns}×{worst.Rows} grid (cap {GasStationKit.ImportSizeCap})");

            var packed = r.Sheets.Where(s => s.Rows > 1).ToArray();
            sb.AppendLine(packed.Length == 0
                              ? "  every sheet is a single strip"
                              : $"  grid-packed into rows: {string.Join(", ", packed.Select(s => s.Key))}");
            return sb.ToString();
        }

        /// <summary>
        /// Headless entry point for CI / <c>-executeMethod</c>.
        ///
        /// <para>⚠️ <c>-executeMethod</c> REQUIRES <c>-quit</c> — without it the editor runs this,
        /// logs the whole summary and then stays up forever holding the project lock, so the next run
        /// dies with "another Unity instance is running" while the shell still reports exit 0. The
        /// mirror of the <c>-runTests</c> rule, where <c>-quit</c> is forbidden.</para>
        /// </summary>
        public static void BakeGasStationFromCommandLine()
        {
            try
            {
                BakeAll();
                EditorApplication.Exit(0);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[rig-baker] headless gas station bake failed: {ex}");
                EditorApplication.Exit(1);
            }
        }
    }
}
