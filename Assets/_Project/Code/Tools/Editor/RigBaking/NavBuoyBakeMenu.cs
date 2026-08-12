using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace HiddenHarbours.Tools.RigBaking
{
    /// <summary>
    /// The nav-buoy kit's two menu entries: MEASURE (emit the oracle from the live rig) and BAKE
    /// (assert against it, then write sheets).
    ///
    /// <para>They are separate on purpose. A baker that emits its own oracle as it goes cannot fail
    /// the comparison — the check is only worth something if the contract is committed first and the
    /// bake is refused when it stops reproducing.</para>
    /// </summary>
    public static class NavBuoyBakeMenu
    {
        /// <summary>
        /// Measures all 70 type×size cells from the live rig and writes the contract. Run this when
        /// the owner drops a revised rig — then read the diff before committing it.
        /// </summary>
        [MenuItem("Hidden Harbours/Art/Measure Nav Buoy Kit (emit contract)", priority = 68)]
        public static void MeasureAndEmitContract()
        {
            var sw = Stopwatch.StartNew();
            try
            {
                var entry = RigCatalog.Get(NavBuoyKit.RigKey);
                using IRigScriptHost host = RigScriptHostFactory.Create();

                // InstallModule, never Install: this rig exposes no W/H/pivot triple (its cell is per
                // type+size) and Install throws on the missing pivot. The prerequisite — the deck-loop
                // turntable — comes with it, depth first.
                RigCatalog.InstallModule(host, entry);

                var probe = NavBuoyRegistrationProbe.MeasureAndAssert(host, entry.DeclaredConvention);

                var contract = new NavBuoyContract
                {
                    generated = "measured from docs/art/rigs/nav-buoy-kit/navBuoyRig.js in the editor's " +
                                "V8 host, against the registered deckIsoSolid turntable",
                    convention = probe.Convention == AzimuthConvention.Clockwise
                                 ? "clockwise" : "counterClockwise",
                    groundStepDeg = Math.Round(probe.GroundStepDeg, 6),
                    screenMeanStepDeg = Math.Round(probe.ScreenMeanStepDeg, 4),
                    elev = probe.Elevation,
                };

                string g = entry.GlobalName;
                int total = NavBuoyKit.AllTypes.Count * NavBuoyKit.Sizes.Count, done = 0;

                foreach (string type in NavBuoyKit.AllTypes)
                {
                    foreach (string size in NavBuoyKit.Sizes)
                    {
                        done++;
                        EditorUtility.DisplayProgressBar("Measuring nav-buoy kit",
                            $"{type} {size} ({done}/{total})", done / (float)total);

                        host.Execute($"globalThis.__nbCell = {g}.cell('{type}','{size}');");
                        int nw = (int)host.EvaluateNumber("globalThis.__nbCell.W");
                        int nh = (int)host.EvaluateNumber("globalThis.__nbCell.H");
                        int npx = (int)host.EvaluateNumber("globalThis.__nbCell.cx");
                        int npy = (int)host.EvaluateNumber("globalThis.__nbCell.cy");

                        NavBuoyKit.MeasureCell(host, g, type, size, nw, nh, npx, npy,
                                               out int cropX, out int cropY, out int cw, out int ch,
                                               out int pivotX, out int pivotY, out bool inside);

                        if (!NavBuoyKit.PlanSheet(cw, ch, NavBuoyKit.Facings, NavBuoyKit.ImportSizeCap,
                                                  out int cols, out int rows,
                                                  out int sheetW, out int sheetH))
                            throw new InvalidOperationException(
                                $"no divisor grid of {NavBuoyKit.Facings} cells fits a {cw}×{ch} cell " +
                                $"under {NavBuoyKit.ImportSizeCap} px for {type}|{size}.");

                        // How many facings are actually distinct art, at the shipping wear. Recorded
                        // because it is NOT always 8 and a packer may act on it.
                        var seen = new HashSet<ulong>();
                        for (int d = 0; d < NavBuoyKit.Facings; d++)
                            seen.Add(NavBuoyKit.ContentHash(
                                NavBuoyKit.RenderRaw(host, g, type, d, size,
                                                     NavBuoyKit.BakeWear, NavBuoyKit.BakeLit)));

                        contract.cells.Add(new NavBuoyContract.Cell
                        {
                            key = NavBuoyKit.CellKey(type, size), type = type, size = size,
                            nativeW = nw, nativeH = nh, nativePivotX = npx, nativePivotY = npy,
                            cropX = cropX, cropY = cropY,
                            cellW = cw, cellH = ch, pivotX = pivotX, pivotY = pivotY,
                            pivotInsideInk = inside, distinctFacings = seen.Count,
                            cols = cols, rows = rows, sheetW = sheetW, sheetH = sheetH,
                        });
                    }
                }

                contract.AssertSelfConsistent();
                contract.Save();
                AssetDatabase.Refresh();

                var widest = contract.WidestSheet();
                sw.Stop();
                Debug.Log(
                    $"[NavBuoy] Measured {contract.cells.Count} cells in {sw.Elapsed.TotalSeconds:F1}s " +
                    $"on {probe.EngineName}. {probe}\n" +
                    $"  widest sheet: {widest.key} {widest.w}×{widest.h} " +
                    $"({widest.maxDim} px, {widest.headroom} px under the {contract.importSizeCap} px cap)\n" +
                    $"  keyline: exportsDefault={probe.ExportsKeylineDefault} " +
                    $"ringlessByDefault={probe.DefaultRenderIsRingless} " +
                    $"outlineAlias={probe.OutlineIsLiveAlias}\n" +
                    $"  → {NavBuoyKit.ContractPath}");
            }
            finally { EditorUtility.ClearProgressBar(); }
        }

        /// <summary>
        /// Bakes the shipping marks: <see cref="NavBuoyKit.BakeTypes"/> × all five sizes × 8 facings,
        /// at <see cref="NavBuoyKit.BakeWear"/>. 50 sheets, 400 renders.
        /// </summary>
        [MenuItem("Hidden Harbours/Art/Bake Nav Buoy Kit (4 cardinals + 4 laterals + mooring + danger)",
                  priority = 69)]
        public static void BakeNavBuoyKit()
        {
            var sw = Stopwatch.StartNew();
            var written = new List<string>();
            try
            {
                var entry = RigCatalog.Get(NavBuoyKit.RigKey);
                var contract = NavBuoyContract.Load();

                contract.AssertSelfConsistent();

                // The contract MEASURED a handedness and the catalog DECLARES one. A disagreement
                // writes eight good facings in reverse order rather than failing.
                contract.AssertConventionAgrees(entry.DeclaredConvention);

                using IRigScriptHost host = RigScriptHostFactory.Create();
                RigCatalog.InstallModule(host, entry);

                // Re-measure from the LIVE rig too, not just from the committed contract — a stale
                // contract that agrees with itself is exactly the failure the oracle cannot catch.
                var probe = NavBuoyRegistrationProbe.MeasureAndAssert(host, entry.DeclaredConvention);

                // ⚠️ NO AssertKeylineGated here, and that is deliberate. That gate probes
                // `<Global>.KEYLINE_DEFAULT` and this rig exports none (the shipyard kit's #477 state).
                // We cannot add it — the art director's file runs UNMODIFIED (ADR 0021 §5). So the
                // BEHAVIOUR is asserted instead: the default render must be the ringless one and the
                // ring must still be reachable. Both arms, because "no ring pixels" would also pass on
                // a renderer that had stopped drawing.
                if (!probe.DefaultRenderIsRingless)
                    throw new InvalidOperationException(
                        "nav-buoy default render is NOT ringless — ADR 0031 says art authored from " +
                        "2026-08 bakes without the ring. Refusing; {keyline:true} is the A/B, not the " +
                        "default.");

                int total = NavBuoyKit.BakeTypes.Count * NavBuoyKit.Sizes.Count, done = 0;
                int widestDim = 0; string widest = "";

                foreach (string type in NavBuoyKit.BakeTypes)
                {
                    foreach (string size in NavBuoyKit.Sizes)
                    {
                        done++;
                        EditorUtility.DisplayProgressBar("Baking nav-buoy kit",
                            $"{type} {size} ({done}/{total})", done / (float)total);

                        var r = NavBuoySheetBaker.Bake(type, size, NavBuoyKit.SheetFolder, host, contract);
                        written.Add(r.AssetPath);
                        if (Mathf.Max(r.SheetWidth, r.SheetHeight) > widestDim)
                        {
                            widestDim = Mathf.Max(r.SheetWidth, r.SheetHeight);
                            widest = $"{r.Key} {r.SheetWidth}×{r.SheetHeight}";
                        }
                    }
                }

                if (written.Count != total)
                    throw new InvalidOperationException(
                        $"nav-buoy bake wrote {written.Count} sheets, expected {total}. A silently " +
                        "skipped mark is worse than a loud failure.");

                sw.Stop();
                Debug.Log($"[NavBuoy] Baked {written.Count} sheets in {sw.Elapsed.TotalSeconds:F1}s " +
                          $"on {probe.EngineName}. Widest: {widest} " +
                          $"({NavBuoyKit.ImportSizeCap - widestDim} px under the cap). " +
                          $"Wear '{NavBuoyKit.BakeWear}', lit={NavBuoyKit.BakeLit}.");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                AssetDatabase.Refresh();
            }
        }

        /// <summary>Batch/headless entry: measure, bake, slice, in the one order that works.</summary>
        public static void BakeAndSliceFromCommandLine()
        {
            BakeNavBuoyKit();
            Art.Editor.NavBuoySheetSlicer.SliceAllFromCommandLine();
        }
    }
}
