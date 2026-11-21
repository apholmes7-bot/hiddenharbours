using System;
using System.Collections.Generic;
using System.IO;
using HiddenHarbours.Art;
using HiddenHarbours.Art.Editor;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.Tools.RigBaking
{
    /// <summary>
    /// <b>Bakes the chest freezers</b> out of <c>appliance-kit/applianceIsoRig.js</c> (owner 2026-11-17: detailed
    /// appliances with animations): one sheet per <see cref="ChestFreezerSheet.Pieces"/> entry, a row per state
    /// (<see cref="ChestFreezerSheet.OptsFor"/>: closed, the lid on its way up, open with each bed, and the open
    /// chest's near layer), a column per facing.
    ///
    /// <para>The same steps as <see cref="ApplianceSheetBaker.BakeOne"/> (row 0 measured against the room through
    /// the interior bake, the rest on the same host and turntable, one crop for the whole sheet so the floor
    /// pivot never moves between states), kept apart so baking a freezer never re-bakes the bathroom and
    /// kitchen sheets.</para>
    /// </summary>
    public static class ChestFreezerBaker
    {
        public const string CompletionLine = "[ChestFreezerBaker] DONE";

        /// <summary>Bake every freezer sheet; returns one report per sheet (not yet sliced).</summary>
        public static List<ApplianceSheetBaker.SheetReport> BakeAll()
        {
            var reports = new List<ApplianceSheetBaker.SheetReport>();
            using IRigScriptHost host = RigScriptHostFactory.Create();
            foreach ((string rig, string stem) in ChestFreezerSheet.Pieces) reports.Add(BakeOne(host, rig, stem));
            return reports;
        }

        public static ApplianceSheetBaker.SheetReport BakeOne(IRigScriptHost host, string rigName, string stem)
        {
            const string key = ApplianceSheetBaker.RigKey;
            string folder = ApplianceSheetBaker.OutputFolder;
            int facings = ChestFreezerSheet.Facings, rowsN = ChestFreezerSheet.Rows;

            var first = InteriorBakeRequest.PropOf(key, rigName, ChestFreezerSheet.OptsFor(0), stem + "_s0",
                                                   folder, stem, facings, ApplianceSheetSlicer.ImportSizeCap);
            InteriorCellSet set = InteriorRigBaker.RenderCells(first, host);
            string g = RigCatalog.Get(key).GlobalName;
            int w = set.Geometry.Width, h = set.Geometry.Height;

            var rows = new byte[rowsN][][];
            rows[0] = set.Cells;
            for (int r = 1; r < rowsN; r++)
            {
                var req = InteriorBakeRequest.PropOf(key, rigName, ChestFreezerSheet.OptsFor(r), $"{stem}_s{r}",
                                                     folder, stem, facings, ApplianceSheetSlicer.ImportSizeCap);
                rows[r] = new byte[facings][];
                for (int cell = 0; cell < facings; cell++)
                {
                    double dir = RigBaker.DirForCell(cell, facings, set.Convention);
                    rows[r][cell] = host.EvaluateBytes(req.RenderJs(g, dir));
                    if (rows[r][cell].Length != w * h * 4)
                        throw new InvalidOperationException($"{stem} row {r} cell {cell} came back the wrong size.");
                }
            }

            var all = new List<byte[]>();
            foreach (byte[][] row in rows) all.AddRange(row);
            BuildingRigBaker.UnionAlphaBounds(all, w, h, out int x0, out int y0, out int x1, out int y1);
            if (x1 < x0) throw new InvalidOperationException($"{stem} drew nothing in any state.");
            const int pad = 2;
            x0 = Mathf.Max(0, x0 - pad); y0 = Mathf.Max(0, y0 - pad);
            x1 = Mathf.Min(w - 1, x1 + pad); y1 = Mathf.Min(h - 1, y1 + pad);
            int cw = x1 - x0 + 1, ch = y1 - y0 + 1;

            int pw = facings * cw, ph = rowsN * ch;
            if (pw > ApplianceSheetSlicer.ImportSizeCap || ph > ApplianceSheetSlicer.ImportSizeCap)
                throw new InvalidOperationException($"{stem} packs to {pw}×{ph}, over the import cap.");

            var pixels = new Color32[pw * ph];
            for (int r = 0; r < rowsN; r++)
            for (int cell = 0; cell < facings; cell++)
                BuildingRigBaker.BlitCropped(rows[r][cell], w, h, x0, y0, cw, ch, pixels, pw, ph, col: cell, rowFromTop: r);

            string assetPath = $"{folder}/{stem}.png";
            Directory.CreateDirectory(Path.Combine(RigCatalog.RepoRoot, folder));
            var tex = new Texture2D(pw, ph, TextureFormat.RGBA32, mipChain: false, linear: false);
            byte[] png;
            try
            {
                tex.SetPixels32(pixels);
                tex.Apply(false, false);
                png = tex.EncodeToPNG();
                File.WriteAllBytes(Path.Combine(RigCatalog.RepoRoot, assetPath), png);
            }
            finally { UnityEngine.Object.DestroyImmediate(tex); }

            return new ApplianceSheetBaker.SheetReport
            {
                Stem = stem, AssetPath = assetPath, Rows = rowsN, CellWidth = cw, CellHeight = ch,
                PivotX = set.Geometry.PivotX - x0, PivotY = set.Geometry.PivotY - y0, PngBytes = png.Length,
                PixelsPerUnit = set.PixelsPerMetre,
            };
        }

        /// <summary>Slice the baked sheets into their named sprites.</summary>
        public static void SliceAll(List<ApplianceSheetBaker.SheetReport> reports)
        {
            AssetDatabase.Refresh();
            foreach (ApplianceSheetBaker.SheetReport r in reports)
            {
                string stem = r.Stem;
                if (!ApplianceSheetSlicer.Slice(r.AssetPath, r.Rows, ChestFreezerSheet.Facings, r.CellWidth, r.CellHeight,
                                                new Vector2((float)r.PivotX, (float)r.PivotY), r.PixelsPerUnit,
                                                (row, cell) => ChestFreezerSheet.SpriteName(stem, row, cell)))
                    throw new InvalidOperationException($"Slicing {stem} failed; see above.");
            }
            AssetDatabase.SaveAssets();
        }
    }
}
