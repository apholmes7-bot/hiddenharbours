using System;
using System.Collections.Generic;
using System.IO;
using HiddenHarbours.Art.Editor;
using HiddenHarbours.World;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.Tools.RigBaking
{
    /// <summary>
    /// <b>Bakes the outhouse</b> out of <c>appliance-kit/applianceIsoRig.js</c>: one sheet, a row per
    /// <see cref="OuthouseSheet"/> door state (shut, ajar, open), a column per facing.
    ///
    /// <para>The same steps as <see cref="ApplianceSheetBaker.BakeOne"/> (row 0 measured against the room through
    /// the interior bake, the rest on the same host and turntable, one crop for the whole sheet so the floor
    /// pivot never moves as the door swings), kept apart so baking the outhouse never re-bakes the bathroom and
    /// kitchen sheets.</para>
    /// </summary>
    public static class OuthouseBaker
    {
        public static ApplianceSheetBaker.SheetReport Bake()
        {
            using IRigScriptHost host = RigScriptHostFactory.Create();
            return BakeOne(host);
        }

        public static ApplianceSheetBaker.SheetReport BakeOne(IRigScriptHost host)
        {
            const string key = ApplianceSheetBaker.RigKey;
            string folder = ApplianceSheetBaker.OutputFolder, stem = OuthouseSheet.Stem, rigName = OuthouseSheet.RigName;
            int facings = OuthouseSheet.Facings, rowsN = OuthouseSheet.Rows;

            var first = InteriorBakeRequest.PropOf(key, rigName, OuthouseSheet.OptsFor(0), stem + "_s0",
                                                   folder, stem, facings, ApplianceSheetSlicer.ImportSizeCap);
            InteriorCellSet set = InteriorRigBaker.RenderCells(first, host);
            string g = RigCatalog.Get(key).GlobalName;
            int w = set.Geometry.Width, h = set.Geometry.Height;

            var rows = new byte[rowsN][][];
            rows[0] = set.Cells;
            for (int r = 1; r < rowsN; r++)
            {
                var req = InteriorBakeRequest.PropOf(key, rigName, OuthouseSheet.OptsFor(r), $"{stem}_s{r}",
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
                throw new InvalidOperationException($"{stem} packs to {pw}x{ph}, over the import cap.");

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

        /// <summary>Slice the baked sheet into its named sprites.</summary>
        public static void Slice(ApplianceSheetBaker.SheetReport r)
        {
            AssetDatabase.Refresh();
            if (!ApplianceSheetSlicer.Slice(r.AssetPath, r.Rows, OuthouseSheet.Facings, r.CellWidth, r.CellHeight,
                                            new Vector2((float)r.PivotX, (float)r.PivotY), r.PixelsPerUnit,
                                            (row, cell) => OuthouseSheet.SpriteName(row, cell)))
                throw new InvalidOperationException($"Slicing {r.Stem} failed; see above.");
            AssetDatabase.SaveAssets();
        }
    }
}
