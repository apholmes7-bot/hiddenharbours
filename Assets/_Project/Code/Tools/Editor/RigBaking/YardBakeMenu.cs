using System;
using System.Linq;
using System.Text;
using HiddenHarbours.Art.Editor;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.Tools.RigBaking
{
    /// <summary>
    /// Owner-facing entry points for the dooryard kit's boundary family.
    ///
    /// <para>Menu priority 76 continues the unbroken Art-menu run (…72/73 the shop fixtures, 75 the
    /// last of that group). Unity draws a separator at any priority gap greater than 10, so an item
    /// placed carelessly renders in a neighbouring group and reads as MISSING.</para>
    /// </summary>
    public static class YardBakeMenu
    {
        [MenuItem("Hidden Harbours/Art/Bake Yard Kit (fences, gates, hedges × 8 dir)", priority = 76)]
        public static void BakeAll()
        {
            try
            {
                var report = YardSheetBaker.BakeAll();
                AssetDatabase.Refresh();

                // ⚠️ The completion line, and it is what a headless run must grep for. A fresh
                // worktree's FIRST -executeMethod is skipped entirely by Unity and the process still
                // exits 0 — so an exit code proves the editor closed, never that this ran.
                Debug.Log(YardSheetBaker.CompletionLine + "\n" + Summarise(report));
            }
            catch (Exception e)
            {
                Debug.LogError($"Yard kit bake FAILED: {e.Message}\n{e}");
                throw;
            }
        }

        /// <summary>
        /// Bake, then slice, then verify — the whole chain in the order it has to run, so a headless
        /// invocation is one <c>-executeMethod</c> and cannot do half of it.
        /// </summary>
        public static void BakeAndSlice()
        {
            BakeAll();
            YardSheetSlicer.SliceAll();
            YardSheetSlicer.VerifyAll();
        }

        /// <summary>
        /// The facing table, measured and printed, WITHOUT writing anything. The owner-facing answer to
        /// "which way does cell 3 face" — and the check to run first if a fence ever stands the wrong
        /// way round, because it separates a mislabelled kit from a mistaken placer in one click.
        /// </summary>
        [MenuItem("Hidden Harbours/Dev/Report Yard Kit Facing Table", priority = 153)]
        public static void ReportFacingTable()
        {
            using var host = RigScriptHostFactory.Create();
            var entry = RigCatalog.Get(YardKit.RigKey);
            RigCatalog.Install(host, entry);

            var probe = YardRegistrationProbe.Measure(host, entry.GlobalName, YardKit.Facings,
                                                      entry.DeclaredConvention, YardKit.Elevation);
            Debug.Log("Yard kit facing table — MEASURED, never read off the sidecar.\n" + probe.Text);
        }

        /// <summary>
        /// What the family WOULD cost, and what of the kit is left on the table. The number the scope
        /// decision is made on.
        /// </summary>
        [MenuItem("Hidden Harbours/Dev/Report Yard Kit Budget", priority = 154)]
        public static void ReportBudget()
        {
            using var host = RigScriptHostFactory.Create();
            var entry = RigCatalog.Get(YardKit.RigKey);
            RigCatalog.Install(host, entry);
            string g = entry.GlobalName;

            var names = host.EvaluateString($"{g}.list().join(',')").Split(',');
            var sb = new StringBuilder();
            sb.AppendLine($"Yard kit — the rig draws {names.Length} pieces; " +
                          $"{YardKit.Builds.Length} are baked (the boundary family).");
            sb.AppendLine($"  cap {YardKit.ImportSizeCap} px · {YardKit.Facings} facings · " +
                          $"phase {YardKit.BakedPhase} · weather {YardKit.BakedWeather} · " +
                          $"elev {YardKit.Elevation}° · compose {YardKit.BakedCompose}");

            foreach (var b in YardKit.Builds)
                sb.AppendLine($"  BAKED  {b.Piece,-14} {b.Part,-5} len {b.Len:0.00}  kept {b.Kept:0.00}  " +
                              $"paint {b.Paint ?? "(none)"}");

            var notBaked = names.Where(n => YardKit.Builds.All(b => b.Piece != n)).ToArray();
            sb.AppendLine($"  not baked ({notBaked.Length}): {string.Join(" ", notBaked)}");
            sb.AppendLine("  These are dooryard DRESSING — which property gets which is one of the " +
                          "owner decisions batched on #604. Adding one is a row in YardKit.Builds; the " +
                          "baker, the slicer and the contract are already written against the table.");
            Debug.Log(sb.ToString());
        }

        public static string Summarise(YardSheetBaker.BakeReport r)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Yard kit baked — {r.Sheets.Count} sheet(s), {r.TotalPixels:N0} px, " +
                          $"{r.RuntimeMegabytes:F2} MB RGBA32 at runtime, " +
                          $"{r.TotalPngBytes / 1024.0:F1} KiB of PNG on disk " +
                          $"({r.TotalMilliseconds / 1000.0:F1} s, {r.EngineName})");

            foreach (var s in r.Sheets) sb.AppendLine($"  {s}");
            if (r.Probe != null) sb.Append(r.Probe.Text);
            return sb.ToString();
        }
    }
}
