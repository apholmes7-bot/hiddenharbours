using System;
using System.Linq;
using System.Text;
using HiddenHarbours.Art.Editor;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.Tools.RigBaking
{
    /// <summary>
    /// Owner-facing entry points for the fuel storage kit.
    ///
    /// <para>Menu priority 68 continues the unbroken Art-menu bake run (…66 shipyard, 67 deck loop).
    /// Unity draws a separator at any priority gap greater than 10, so an item placed carelessly
    /// renders in a neighbouring group and reads as MISSING.</para>
    /// </summary>
    public static class FuelBakeMenu
    {
        [MenuItem("Hidden Harbours/Art/Bake Fuel Storage Kit (8 vessels × 21 sizes × 4 grades)",
                  priority = 68)]
        public static void BakeAll()
        {
            try
            {
                var report = FuelSheetBaker.BakeAll();
                AssetDatabase.Refresh();
                Debug.Log(Summarise(report));
            }
            catch (Exception e)
            {
                Debug.LogError($"Fuel storage bake FAILED: {e.Message}\n{e}");
                throw;
            }
        }

        /// <summary>
        /// Reports the budget WITHOUT writing anything — the number that decides the facing and fill
        /// axes, available before committing 84 sheets to disk.
        /// </summary>
        [MenuItem("Hidden Harbours/Dev/Report Fuel Storage Sheet Budget", priority = 149)]
        public static void ReportBudget()
        {
            var builds = FuelStorageKit.Builds;
            var sb = new StringBuilder();
            sb.AppendLine($"Fuel storage kit — {builds.Count} sheets planned");
            sb.AppendLine($"  carry facings {FuelStorageKit.CarryFacings} · " +
                          $"store facings {FuelStorageKit.StoreFacings} · " +
                          $"fills {string.Join("/", FuelStorageKit.Fills)} · " +
                          $"wear '{FuelStorageKit.BakedWear}'");

            foreach (var byType in builds.GroupBy(b => b.Type))
            {
                int facings = FuelStorageKit.FacingsFor(byType.Key);
                int fills = FuelStorageKit.FillsFor(byType.Key).Count;
                sb.AppendLine($"  {byType.Key,-7} {byType.Count(),2} sheets × " +
                              $"{facings} facings × {fills} fills");
            }
            Debug.Log(sb.ToString());
        }

        public static string Summarise(FuelSheetBaker.BakeReport r)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Fuel storage kit baked — {r.Sheets.Count} sheets, " +
                          $"{r.TotalPixels:N0} px, {r.RuntimeMegabytes:F1} MB RGBA32 at runtime, " +
                          $"{r.TotalPngBytes / 1024.0 / 1024.0:F2} MiB of PNG on disk " +
                          $"({r.TotalMilliseconds / 1000.0:F1} s, {r.EngineName})");
            sb.AppendLine($"  {r.Probe}");

            foreach (var byType in r.Sheets.GroupBy(s => s.Type))
            {
                long px = byType.Sum(s => (long)s.CroppedPixels);
                sb.AppendLine($"  {byType.Key,-7} {byType.Count(),2} sheets  {px,10:N0} px  " +
                              $"({100.0 * px / r.TotalPixels,4:F1}% of the kit)");
            }

            var worst = r.Sheets.OrderByDescending(s => s.SheetW * s.SheetH).First();
            sb.AppendLine($"  largest sheet: {worst.Key} at {worst.SheetW}×{worst.SheetH} " +
                          $"(cap {FuelStorageKit.ImportSizeCap})");
            return sb.ToString();
        }
    }
}
