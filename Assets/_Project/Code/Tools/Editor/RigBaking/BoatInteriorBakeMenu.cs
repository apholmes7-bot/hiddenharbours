using System;
using System.Linq;
using System.Text;
using HiddenHarbours.Art.Editor;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.Tools.RigBaking
{
    /// <summary>
    /// Owner-facing entry points for the boat-interiors kit.
    ///
    /// <para>Menu priority 78 continues the unbroken Art-menu bake run (…76 dialogue bubbles,
    /// 77 notebook). Unity draws a separator at any priority gap greater than 10, so an item placed
    /// carelessly renders in a neighbouring group and reads as MISSING.</para>
    /// </summary>
    public static class BoatInteriorBakeMenu
    {
        [MenuItem("Hidden Harbours/Art/Bake Boat Interior Sheets (24 hulls × levels × 8 dir)",
                  priority = 78)]
        public static void BakeAll()
        {
            var log = new StringBuilder("[BoatInteriorSheetBaker]\n");
            try
            {
                var report = BoatInteriorSheetBaker.BakeAll(log);
                AssetDatabase.Refresh();
                Debug.Log(log + "\n" + Summarise(report));

                if (report.ClearedFailures > 0)
                    Debug.LogError($"[BoatInteriorSheetBaker] {report.ClearedFailures} hull(s) the S0 " +
                                   "ledger CLEARED did not bake — see the report above. Those are real " +
                                   "failures, not the expected ledger refusals.");

                EditorUtility.DisplayDialog(
                    "Bake boat interior sheets",
                    $"{report.Sheets.Count} hulls baked to {report.PageCount} page(s).\n" +
                    $"{report.LedgerRefusals} refused by the S0 ledger (expected).\n" +
                    $"{report.ClearedFailures} cleared hull(s) failed.\n\n" +
                    "Full report in the Console. Slice next.", "OK");
            }
            catch (Exception e)
            {
                Debug.LogError($"{log}\nBoat interior bake FAILED: {e.Message}\n{e}");
                throw;
            }
        }

        /// <summary>
        /// Reports what a bake WOULD cost without writing a pixel — the number that decides whether
        /// the cells stay full-size (CLAUDE.md rule 7).
        /// </summary>
        [MenuItem("Hidden Harbours/Dev/Report Boat Interior Sheet Budget", priority = 155)]
        public static void ReportBudget()
        {
            var contract = BoatInteriorSheetSlicer.LoadContract();
            if (contract == null) return;
            Debug.Log(SummariseContract(contract));
        }

        /// <summary>
        /// Headless entry (<c>-executeMethod</c>).
        ///
        /// <para>⚠️ <b>Its exit code means something, and that is a deliberate divergence from
        /// <see cref="BoatInteriorDefBuilder.BuildAllCli"/>.</b> That one exits 1 on ANY refusal, so
        /// while the ledger holds its three adjudicated refusals its exit code is permanently 1 and a
        /// caller has to parse the log instead. A ledger refusal is this kit's expected steady state —
        /// the two sport fishers and the cape join later, by upstream PRs — so it is not a failure of
        /// the bake. A CLEARED hull that did not bake is, and that is what decides the code here.</para>
        ///
        /// <para>Never invoke this with <c>-quit</c> alongside <c>-runTests</c>: the two race, Unity
        /// exits 0 having written total=0, and that reads as a pass.</para>
        /// </summary>
        public static void BakeAllCli()
        {
            var log = new StringBuilder("[BoatInteriorSheetBaker]\n");
            try
            {
                var report = BoatInteriorSheetBaker.BakeAll(log);
                AssetDatabase.Refresh();
                Debug.Log(log + "\n" + Summarise(report));

                if (report.ClearedFailures > 0)
                {
                    Debug.LogError($"[BoatInteriorSheetBaker] {report.ClearedFailures} cleared hull(s) " +
                                   "failed to bake.");
                    EditorApplication.Exit(1);
                    return;
                }
                if (report.Sheets.Count == 0)
                {
                    Debug.LogError("[BoatInteriorSheetBaker] baked nothing at all — the kit was not " +
                                   "found where it was expected.");
                    EditorApplication.Exit(1);
                    return;
                }
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogError($"{log}\n[BoatInteriorSheetBaker] {e}");
                EditorApplication.Exit(1);
            }
        }

        /// <summary>Bake then slice, in one headless run, so a CI or batch invocation does not have to
        /// know they are two steps. Same exit-code rule as <see cref="BakeAllCli"/>.</summary>
        public static void BakeAndSliceCli()
        {
            var log = new StringBuilder("[BoatInteriorSheetBaker]\n");
            try
            {
                var report = BoatInteriorSheetBaker.BakeAll(log);
                AssetDatabase.Refresh();
                Debug.Log(log + "\n" + Summarise(report));

                bool sliced = BoatInteriorSheetSlicer.SliceAllInternal(out string sliceReport);
                Debug.Log(sliceReport);

                bool verified = BoatInteriorSheetSlicer.VerifyAllInternal(out string verifyReport);
                Debug.Log(verifyReport);

                bool ok = report.ClearedFailures == 0 && report.Sheets.Count > 0 && sliced && verified;
                if (!ok) Debug.LogError("[BoatInteriorSheetBaker] bake+slice did not come out clean.");
                EditorApplication.Exit(ok ? 0 : 1);
            }
            catch (Exception e)
            {
                Debug.LogError($"{log}\n[BoatInteriorSheetBaker] {e}");
                EditorApplication.Exit(1);
            }
        }

        // ---- reporting --------------------------------------------------------------------------

        public static string Summarise(BoatInteriorSheetBaker.BakeReport r)
        {
            var sb = new StringBuilder();

            // The line a caller parses. Kept in one place and in one shape on purpose.
            sb.AppendLine($"{r.Sheets.Count} baked, {r.LedgerRefusals} refused by the ledger, " +
                          $"{r.ClearedFailures} cleared-hull failures.");
            sb.AppendLine($"  {r.PageCount} page(s), {r.TotalPixels:N0} px, " +
                          $"{r.RuntimeMegabytes:F1} MB RGBA32 if every sheet were resident, " +
                          $"{r.TotalPngBytes / 1024.0 / 1024.0:F2} MiB of PNG on disk " +
                          $"({r.TotalMilliseconds / 1000.0:F1} s, {r.EngineName})");

            foreach (var s in r.Sheets.OrderByDescending(x => x.Pixels).Take(6))
                sb.AppendLine($"  heaviest: {s.Entry.hullFileStem,-38} {s.RuntimeMegabytes,7:F1} MB  " +
                              $"({s.Entry.pages.Length} page(s) of {s.Entry.cellW}×{s.Entry.cellH})");

            var probes = r.Sheets.Select(s => s.Probe).Where(p => p != null).ToList();
            if (probes.Count > 0)
                sb.AppendLine("  " + BoatInteriorRegistrationProbe.Summarise(probes));

            foreach (var refused in r.Refusals)
                sb.AppendLine($"  refused: {refused.HullStem} — {refused.Verdict}" +
                              (refused.IsLedgerRefusal ? " (S0 ledger; upstream work)" : " (CLEARED — a real failure)"));

            return sb.ToString();
        }

        public static string SummariseContract(BoatInteriorKit.Contract c)
        {
            var sb = new StringBuilder();
            long px = c.sheets.Sum(s => s.pages.Sum(p => (long)p.sheetW * p.sheetH));
            sb.AppendLine($"Boat interiors — {c.sheets.Length} hull(s), " +
                          $"{c.sheets.Sum(s => s.pages.Length)} page(s), " +
                          $"{px * 4.0 / (1024 * 1024):F1} MB RGBA32 if all were resident");
            sb.AppendLine($"  renderer {c.interiorRigSha256.Substring(0, Math.Min(12, c.interiorRigSha256.Length))}… · " +
                          $"cap {c.importSizeCap} · baked doorOpen {c.sheets.FirstOrDefault()?.doorOpen ?? 0}");

            foreach (var g in c.sheets.GroupBy(s => $"{s.cellW}×{s.cellH} @{s.pixelsPerMetre}px/m")
                                      .OrderByDescending(g => g.First().cellW * g.First().cellH))
            {
                long groupPx = g.Sum(s => s.pages.Sum(p => (long)p.sheetW * p.sheetH));
                sb.AppendLine($"  {g.Key,-24} {g.Count(),2} hull(s)  {groupPx * 4.0 / (1024 * 1024),7:F1} MB  " +
                              $"[{string.Join(", ", g.Select(s => s.hullFileStem).OrderBy(x => x, StringComparer.Ordinal).Take(3))}" +
                              (g.Count() > 3 ? $", +{g.Count() - 3} more]" : "]"));
            }

            if (c.refused.Length > 0)
                sb.AppendLine($"  NOT covered ({c.refused.Length}): " +
                              string.Join(", ", c.refused.Select(x => $"{x.hullStem} [{x.verdict}]")));
            return sb.ToString();
        }
    }
}
