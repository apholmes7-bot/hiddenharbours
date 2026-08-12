using System;
using System.Linq;
using System.Text;
using HiddenHarbours.Art.Editor;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.Tools.RigBaking
{
    /// <summary>
    /// Owner-facing entry points for the shop kit's FIXTURE family.
    ///
    /// <para>Menu priority 72 continues the unbroken Art-menu run (…68 fuel bake, 69 fuel slice,
    /// 70/71 the Def builders). Unity draws a separator at any priority gap greater than 10, so an
    /// item placed carelessly renders in a neighbouring group and reads as MISSING.</para>
    /// </summary>
    public static class ShopFixtureBakeMenu
    {
        [MenuItem("Hidden Harbours/Art/Bake Shop Fixtures (standalone props)", priority = 72)]
        public static void BakeAll()
        {
            try
            {
                var report = ShopFixtureSheetBaker.BakeAll();
                AssetDatabase.Refresh();

                // ⚠️ The completion line, and it is what a headless run must grep for. A fresh
                // worktree's FIRST -executeMethod is skipped entirely by Unity and the process still
                // exits 0 — so an exit code proves the editor closed, never that this ran.
                Debug.Log(ShopFixtureSheetBaker.CompletionLine + "\n" + Summarise(report));
            }
            catch (Exception e)
            {
                Debug.LogError($"Shop fixture bake FAILED: {e.Message}\n{e}");
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
            ShopFixtureSheetSlicer.SliceAll();
            ShopFixtureSheetSlicer.VerifyAll();
        }

        /// <summary>
        /// What the family WOULD cost, without writing anything: every fixture the rig draws, at its
        /// union cell, packed one row wide. The number the scope decision is made on.
        /// </summary>
        [MenuItem("Hidden Harbours/Dev/Report Shop Fixture Budget", priority = 151)]
        public static void ReportBudget()
        {
            using var host = RigScriptHostFactory.Create();
            var entry = RigCatalog.Get(ShopFixtureKit.RigKey);
            RigCatalog.InstallModule(host, entry);
            string g = entry.GlobalName;

            var sb = new StringBuilder();
            var names = host.EvaluateString($"{g}.list().join(',')").Split(',');
            sb.AppendLine($"Shop fixture family — the rig draws {names.Length}; " +
                          $"{ShopFixtureKit.Builds.Length} are baked.");
            sb.AppendLine($"  cap {ShopFixtureKit.ImportSizeCap} px · {ShopFixtureKit.Facings} facings · " +
                          $"rot {ShopFixtureKit.BakedRot} (measured redundant with dir) · " +
                          $"elev {ShopFixtureKit.Elevation}°");

            foreach (var b in ShopFixtureKit.Builds)
                sb.AppendLine($"  BAKED  {b.Key,-28} size {b.Size:0.00}  stock {b.Stock:0.00}  " +
                              $"weather {b.Weather:0.00}  seed {b.Seed}");

            var notBaked = names.Where(n => !ShopFixtureKit.Builds.Any(b => b.Fixture == n)).ToArray();
            sb.AppendLine($"  not baked ({notBaked.Length}): {string.Join(" ", notBaked)}");
            sb.AppendLine("  Adding one is a row in ShopFixtureKit.Builds — the baker, the slicer and " +
                          "the contract are already written against the table.");
            Debug.Log(sb.ToString());
        }

        public static string Summarise(ShopFixtureSheetBaker.BakeReport r)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Shop fixtures baked — {r.Sheets.Count} sheet(s), {r.TotalPixels:N0} px, " +
                          $"{r.RuntimeMegabytes:F2} MB RGBA32 at runtime, " +
                          $"{r.TotalPngBytes / 1024.0:F1} KiB of PNG on disk " +
                          $"({r.TotalMilliseconds / 1000.0:F1} s, {r.EngineName})");

            foreach (var s in r.Sheets)
                sb.AppendLine($"  {s}");

            if (r.Probe != null) sb.Append(r.Probe.Text);
            return sb.ToString();
        }
    }
}
