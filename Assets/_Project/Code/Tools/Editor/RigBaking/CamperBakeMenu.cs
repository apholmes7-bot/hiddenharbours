using System;
using System.Linq;
using System.Text;
using HiddenHarbours.Art.Editor;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.Tools.RigBaking
{
    /// <summary>
    /// Owner-facing entry points for the camper kit.
    ///
    /// <para>Menu priority 74 continues the unbroken Art-menu bake run (…68 fuel, 70 fuel defs,
    /// 73 shop fixtures). Unity draws a separator at any priority gap greater than 10, so an item
    /// placed carelessly renders in a neighbouring group and reads as MISSING.</para>
    /// </summary>
    public static class CamperBakeMenu
    {
        [MenuItem("Hidden Harbours/Art/Bake Camper Kit (2 lengths × rest + door cue × 8 dir)",
                  priority = 74)]
        public static void BakeAll()
        {
            try
            {
                var report = CamperSheetBaker.BakeAll();
                AssetDatabase.Refresh();
                Debug.Log(Summarise(report));
            }
            catch (Exception e)
            {
                Debug.LogError($"Camper bake FAILED: {e.Message}\n{e}");
                throw;
            }
        }

        /// <summary>
        /// Bake then slice, in one editor invocation — the <c>-executeMethod</c> entry point.
        ///
        /// <para>Deliberately NOT a menu item: the owner's Art menu already has the two halves, and a
        /// third entry that just runs both would be clutter. This exists because a headless run takes
        /// one method, and doing it as two batch invocations pays the editor's start-up twice for
        /// nothing.</para>
        ///
        /// <para>The marker on the last line is what a run is verified by — <b>a Unity exit code does
        /// not tell you whether <c>-executeMethod</c> fired</b>, and a first run after a large pending
        /// import can import only and exit 0 with the method never called.</para>
        /// </summary>
        public static void BakeAndSliceHeadless()
        {
            BakeAll();
            CamperSheetSlicer.SliceAll();
            CamperSheetSlicer.VerifyAll();
            Debug.Log("[camper-bake] BAKE + SLICE + VERIFY COMPLETE");
        }

        /// <summary>
        /// Reports the plan WITHOUT writing anything — the numbers that decide the frame count, before
        /// four sheets go to disk.
        /// </summary>
        [MenuItem("Hidden Harbours/Dev/Report Camper Sheet Budget", priority = 151)]
        public static void ReportBudget()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Camper kit — {CamperKit.Builds.Count} sheets planned");
            sb.AppendLine($"  facings {CamperKit.Facings} · cue frames {CamperKit.CueFrames} · " +
                          $"weather {CamperKit.BakedWeather} · paint {CamperKit.BakedPaint} · " +
                          $"cap {CamperKit.ImportSizeCap} px");

            foreach (var b in CamperKit.Builds)
                sb.AppendLine($"  {b.Key,-24} {b.Frames} frame(s) × {CamperKit.Facings} facings, " +
                              $"awning {(b.AwningOverride.HasValue ? b.AwningOverride.Value.ToString() : "(the variant's own default)")}");

            sb.AppendLine($"  ⚠ {CamperKit.AwningPop}");
            Debug.Log(sb.ToString());
        }

        public static string Summarise(CamperSheetBaker.BakeReport r)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Camper kit baked — {r.Sheets.Count} sheets, {r.TotalPixels:N0} px, " +
                          $"{r.RuntimeMegabytes:F1} MB RGBA32 at runtime, " +
                          $"{r.TotalPngBytes / 1024.0 / 1024.0:F2} MiB of PNG on disk " +
                          $"({r.TotalMilliseconds / 1000.0:F1} s total, " +
                          $"{r.RenderMilliseconds / 1000.0:F1} s rendering, {r.EngineName})");
            sb.AppendLine($"  convention: {r.MeasuredConvention} (measured) — sheets are clockwise");

            foreach (var s in r.Sheets)
                sb.AppendLine($"  {s.Key,-24} cell {s.CellW,3}×{s.CellH,3} pivot {s.PivotX,3},{s.PivotY,3} " +
                              $"crop ({s.CropX,3},{s.CropY,3})  {s.Columns}×{s.Rows} → " +
                              $"{s.SheetW,4}×{s.SheetH,4}  awning {s.Awning,-5} " +
                              $"cue {s.MaxCueDeltaPx,5} px  {s.PngBytes / 1024.0,7:F0} KiB");

            var worst = r.Sheets.OrderByDescending(s => s.CroppedPixels).First();
            sb.AppendLine($"  largest sheet: {worst.Key} at {worst.SheetW}×{worst.SheetH} " +
                          $"(cap {CamperKit.ImportSizeCap})");
            sb.AppendLine($"  ⚠ {CamperKit.AwningPop}");
            return sb.ToString();
        }
    }
}
