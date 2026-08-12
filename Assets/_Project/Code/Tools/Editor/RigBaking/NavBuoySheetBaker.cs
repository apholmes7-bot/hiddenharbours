using System;
using System.IO;
using UnityEngine;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace HiddenHarbours.Tools.RigBaking
{
    public sealed class NavBuoyBakeResult
    {
        public string Type, Size, Key, AssetPath, EngineName;
        public int NativeWidth, NativeHeight, NativePivotX, NativePivotY;
        public int CropX, CropY, CellWidth, CellHeight, PivotX, PivotY;
        public int Columns, Rows, SheetWidth, SheetHeight, Facings, PngBytes, DistinctFacings;
        public bool PivotInsideInk;
        public double RenderMilliseconds, TotalMilliseconds;

        /// <summary>Unity wants the pivot normalised from the BOTTOM-left; the rigs report it from the
        /// TOP-left. ADR 0026: the y term is <c>(H − pivotY)/H</c>, not <c>(H − 1 − pivotY)/H</c> —
        /// a rig pivot is a continuous coordinate, not a pixel index. Getting it upside-down is easy
        /// and silent.</summary>
        public Vector2 NormalisedPivot =>
            new Vector2(PivotX / (float)CellWidth, (CellHeight - PivotY) / (float)CellHeight);

        public override string ToString() =>
            $"{Key}: native {NativeWidth}×{NativeHeight} → cell {CellWidth}×{CellHeight} " +
            $"pivot {PivotX},{PivotY} → {Columns}×{Rows} sheet {SheetWidth}×{SheetHeight} " +
            $"({PngBytes:N0} B, {DistinctFacings}/{Facings} distinct) in {TotalMilliseconds:F0} ms";
    }

    /// <summary>
    /// One nav mark, one size, through the 8-facing turntable, cropped to its wear-invariant cell
    /// and packed to the committed plan.
    ///
    /// <para><b>The contract is the oracle</b> (<see cref="NavBuoyContract"/>): every cell is checked
    /// against it before a pixel is written, and a disagreement refuses the bake.</para>
    /// </summary>
    public static class NavBuoySheetBaker
    {
        public static NavBuoyBakeResult Bake(string type, string size, string outputFolder,
                                             IRigScriptHost host, NavBuoyContract contract)
        {
            var total = Stopwatch.StartNew();
            var clock = new Stopwatch();
            var entry = RigCatalog.Get(NavBuoyKit.RigKey);
            string g = entry.GlobalName;
            string key = NavBuoyKit.CellKey(type, size);

            // The rig sizes its own cell per type+size and exposes no W/H/pivot triple, so it reports
            // geometry through cell() rather than through Install.
            host.Execute($"globalThis.__nbCell = {g}.cell('{type}','{size}');");
            int nw = (int)host.EvaluateNumber("globalThis.__nbCell.W");
            int nh = (int)host.EvaluateNumber("globalThis.__nbCell.H");
            int npx = (int)host.EvaluateNumber("globalThis.__nbCell.cx");
            int npy = (int)host.EvaluateNumber("globalThis.__nbCell.cy");

            if (nw <= 0 || nh <= 0)
                throw new InvalidOperationException(
                    $"nav-buoy {key} reported a {nw}×{nh} cell. The type/size resolved but sized " +
                    "nothing — refusing rather than writing an empty sheet.");

            NavBuoyKit.MeasureCell(host, g, type, size, nw, nh, npx, npy,
                                   out int cropX, out int cropY, out int cw, out int ch,
                                   out int pivotX, out int pivotY, out bool pivotInside);

            // ---- THE ORACLE ---------------------------------------------------------------------
            contract.AssertMatches(key, cropX, cropY, cw, ch, pivotX, pivotY);

            var c = contract.Get(key);
            contract.AssertSheetFits(key, c.sheetW, c.sheetH);

            // ---- render the eight, at the shipping wear -------------------------------------------
            var pixels = new Color32[c.sheetW * c.sheetH];
            var seen = new System.Collections.Generic.HashSet<ulong>();

            for (int cell = 0; cell < NavBuoyKit.Facings; cell++)
            {
                // Clockwise ⇒ DirForCell is the identity. Routed through it anyway so the day this
                // kit's handedness is re-measured, one constant moves and the loop does not.
                int dir = (int)RigBaker.DirForCell(cell, NavBuoyKit.Facings, entry.DeclaredConvention);

                clock.Start();
                byte[] buf = NavBuoyKit.RenderRaw(host, g, type, dir, size,
                                                  NavBuoyKit.BakeWear, NavBuoyKit.BakeLit);
                clock.Stop();

                if (buf.Length != nw * nh * 4)
                    throw new InvalidOperationException(
                        $"nav-buoy {key} facing {cell} returned {buf.Length} bytes for a {nw}×{nh} " +
                        $"cell (expected {nw * nh * 4}). The rig's cell() and render() disagree.");

                seen.Add(NavBuoyKit.ContentHash(buf));

                BuildingRigBaker.BlitCropped(buf, nw, nh, cropX, cropY, cw, ch,
                                             pixels, c.sheetW, c.sheetH,
                                             col: cell % c.cols, rowFromTop: cell / c.cols);
            }

            var result = new NavBuoyBakeResult
            {
                Type = type, Size = size, Key = key,
                EngineName = host.EngineName,
                NativeWidth = nw, NativeHeight = nh, NativePivotX = npx, NativePivotY = npy,
                CropX = cropX, CropY = cropY,
                CellWidth = cw, CellHeight = ch, PivotX = pivotX, PivotY = pivotY,
                PivotInsideInk = pivotInside,
                Columns = c.cols, Rows = c.rows, SheetWidth = c.sheetW, SheetHeight = c.sheetH,
                Facings = NavBuoyKit.Facings, DistinctFacings = seen.Count,
                RenderMilliseconds = clock.Elapsed.TotalMilliseconds,
                AssetPath = $"{outputFolder}/{NavBuoyKit.SheetName(type, size)}.png",
            };

            WritePng(pixels, c.sheetW, c.sheetH, result);

            total.Stop();
            result.TotalMilliseconds = total.Elapsed.TotalMilliseconds;
            return result;
        }

        static void WritePng(Color32[] pixels, int w, int h, NavBuoyBakeResult result)
        {
            string outAbs = Path.Combine(RigCatalog.RepoRoot, Path.GetDirectoryName(result.AssetPath) ?? "");
            Directory.CreateDirectory(outAbs);

            var tex = new Texture2D(w, h, TextureFormat.RGBA32, mipChain: false, linear: false);
            try
            {
                tex.SetPixels32(pixels);
                tex.Apply(false, false);
                byte[] png = tex.EncodeToPNG();
                File.WriteAllBytes(Path.Combine(RigCatalog.RepoRoot, result.AssetPath), png);
                result.PngBytes = png.Length;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(tex);
            }
        }
    }
}
