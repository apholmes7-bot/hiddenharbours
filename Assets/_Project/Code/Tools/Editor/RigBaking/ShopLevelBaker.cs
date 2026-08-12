#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace HiddenHarbours.Tools.RigBaking
{
    /// <summary>What one shop LEVEL bake produced.</summary>
    public sealed class ShopLevelBakeResult
    {
        public string AssetPath, EngineName, OptionsJs;
        public int NativeCellWidth, NativeCellHeight;
        public int CellWidth, CellHeight, CropX, CropY;
        public double PivotX, PivotY;
        public int Columns, Rows, Facings;
        public int SheetWidth, SheetHeight;
        public long PngBytes;
        public double FootprintWidthMetres, FootprintLengthMetres, LevelZ;
        public int PixelsPerMetre;
        public double[] DoorX, DoorY, BackDoorX, BackDoorY;
        public double RenderMilliseconds, TotalMilliseconds;

        public long RuntimeBytesRgba32 => (long)SheetWidth * SheetHeight * 4;
        public long UncroppedBytesRgba32 =>
            (long)NativeCellWidth * NativeCellHeight * 4 * Math.Max(1, Facings);
        public double CropSaving =>
            UncroppedBytesRgba32 == 0 ? 0 : 1.0 - RuntimeBytesRgba32 / (double)UncroppedBytesRgba32;
    }

    /// <summary>
    /// Bakes one LEVEL of one shop — the whole floor plan merged into a single 8-facing sheet.
    ///
    /// <para><b>Why this is not <see cref="BuildingRigBaker"/>.</b> That baker reads a rig's cell size and
    /// pivot ONCE, from the rig's own <c>W/H/pivot</c> triple, because a house rig has one cell for every
    /// build it can produce. <c>shopBuildingRig</c> does not: its cell and its pivot are per trade AND per
    /// level (a restaurant's ground floor is 723×595, its upper 496×455), and its own README says "ask
    /// <c>sheet(opts)</c>, don't assume". Its <c>render()</c> also returns <c>{rgba, W, H, pivot}</c>
    /// rather than a bare buffer. Both differences are small and both are load-bearing, so the pack, the
    /// crop and the PNG write are REUSED from that baker and only the geometry is read differently.</para>
    ///
    /// <para><b>The union crop is what makes this affordable.</b> A level's native cell is sized to hold
    /// the plan at its worst facing; at every other facing a good part of it is empty. Cropping to the
    /// union of the drawn pixels across all eight — one box, so the pivot stays put — turns the
    /// restaurant's ground floor from 8 × 723×595 (11.0 MB RGBA32) into a 3470×1114 sheet.</para>
    /// </summary>
    public static class ShopLevelBaker
    {
        /// <summary>Transparent margin kept around the art, matching <see cref="BuildingRigBaker.Padding"/>
        /// so a shop sheet and a house sheet crop the same way.</summary>
        public const int Padding = BuildingRigBaker.Padding;

        /// <summary>
        /// Bake <paramref name="trade"/>'s <paramref name="level"/> at <paramref name="size"/> into
        /// <paramref name="outputFolder"/>/<paramref name="baseName"/>.png.
        /// </summary>
        public static ShopLevelBakeResult Bake(string trade, string level, double size,
                                               string outputFolder, string baseName,
                                               int facings, int maxSheetDimension)
        {
            var total = Stopwatch.StartNew();

            using IRigScriptHost host = RigScriptHostFactory.Create();

            // Installs shopInterior, then shopfront, then this — declared as Prerequisites so the caller
            // cannot get the order wrong. Every failure of that order is silent (see RigCatalog).
            var entry = RigCatalog.Get("shopBuilding");
            RigCatalog.InstallModule(host, entry);
            string g = entry.GlobalName;

            string opts = OptionsLiteral(trade, level, size);

            // ---- geometry, ASKED not assumed --------------------------------------------------------
            int nativeW = (int)host.EvaluateNumber($"{g}.sheet({opts}).W");
            int nativeH = (int)host.EvaluateNumber($"{g}.sheet({opts}).H");
            double pivX = host.EvaluateNumber($"{g}.sheet({opts}).cx");
            double pivY = host.EvaluateNumber($"{g}.sheet({opts}).gy");

            if (nativeW <= 0 || nativeH <= 0)
                throw new InvalidOperationException(
                    $"[shops] {g}.sheet({opts}) reported a {nativeW}×{nativeH} cell. The build asked for " +
                    "a plan the rig does not have.");

            int rigDirs = (int)host.EvaluateNumber($"{g}.DIRS");
            if (rigDirs != facings)
                throw new InvalidOperationException(
                    $"[shops] the rig carries DIRS={rigDirs} but the bake asked for {facings} facings. " +
                    "The facing count is the owner's 2026-08-05 ruling and the rig's own turntable; a " +
                    "mismatch means one of them moved.");

            // ---- kit assert 1: the pivot may not shift as the model turns ---------------------------
            // "rotation must not shift the footprint" is the kit's first stated assert, and it is the one
            // whose failure is invisible: every facing draws correctly and the building slides on its
            // site as it turns.
            for (int d = 1; d < facings; d++)
            {
                double px = host.EvaluateNumber($"{g}.anchors({d},{opts}).pivot.x");
                double py = host.EvaluateNumber($"{g}.anchors({d},{opts}).pivot.y");
                if (Math.Abs(px - pivX) > 1e-9 || Math.Abs(py - pivY) > 1e-9)
                    throw new InvalidOperationException(
                        $"[shops] {trade}/{level}: the pivot moves between facings — dir 0 is " +
                        $"({pivX},{pivY}), dir {d} is ({px},{py}). The kit's own first assert is that the " +
                        "pivot is identical across the eight facings of one level bake, because it is what " +
                        "stops a building shifting on its site as it turns. Refusing to bake a sheet whose " +
                        "cells disagree about where the ground is.");
            }

            // ---- the azimuth correction, MEASURED ----------------------------------------------------
            // 🔴 THE BUG THIS BLOCK EXISTS TO CLOSE. This baker used to render `render(cell, opts)` — the
            // CELL INDEX handed straight in as the rig's dir, with no correction — while the SHELL goes
            // through BuildingRigBaker, which maps cell → dir via RigBaker.DirForCell and INVERTS the
            // order for a counter-clockwise rig. The two families therefore came out MIRRORED: measured
            // on the first real bake, the shells' door bearings stepped −45° per cell and the levels' +45°,
            // so six facings in eight had the interior turned the wrong way inside its own shell.
            //
            // ⚠️ AND EVERY EXISTING CHECK PASSED. ShopRegistrationProbe compares the two rigs in their own
            // dir units, where they genuinely agree (offset 0), and its gable test reads dir 0 against
            // dir 4 — the two facings a mirror leaves alone. The contract then reported "level facing =
            // shell facing + 0", which was true of DIRS and false of CELLS. Nothing renders wrong and
            // nothing throws; the player simply walks in the door and comes out facing the wrong way.
            //
            // So: measure the rig's rotation sense from its own door anchors, cross-check the catalog's
            // declaration, and REFUSE on a mismatch — the rule RigBaker.Bake already follows.
            int sense = ShopRegistrationProbe.DoorRotationSense(
                host, $"{g}.anchors($DIR,{opts})", $"{g}.anchors(0,{opts}).pivot", facings,
                out double meanStepDeg, out string senseReport);

            AzimuthConvention measured = sense > 0
                ? AzimuthConvention.CounterClockwise
                : AzimuthConvention.Clockwise;

            if (measured != entry.DeclaredConvention)
                throw new InvalidOperationException(
                    $"[shops] AZIMUTH MISMATCH on '{g}' for {trade}/{level}.\n" +
                    $"  RigCatalog declares : {entry.DeclaredConvention}\n" +
                    $"  the anchors say     : {measured} ({senseReport})\n\n" +
                    "The bake is refusing rather than guessing. A silent guess here is how this mislabel " +
                    "shipped defects in five kits — and how this kit's own levels shipped mirrored.");

            Debug.Log($"[shops] {trade}/{level}: {senseReport} ({meanStepDeg:+0.0;-0.0}°/dir) " +
                      $"⇒ {measured}, so cell i renders at dir (facings − i) mod {facings}.");

            // ---- render ------------------------------------------------------------------------------
            var render = Stopwatch.StartNew();
            var cells = new byte[facings][];
            for (int cell = 0; cell < facings; cell++)
            {
                string d = Dir(cell, facings, measured);

                // .rgba: this rig returns an object, unlike the house rigs' bare buffer.
                cells[cell] = host.EvaluateBytes($"{g}.render({d},{opts}).rgba");
                int expect = nativeW * nativeH * 4;
                if (cells[cell] == null || cells[cell].Length != expect)
                    throw new InvalidOperationException(
                        $"[shops] {trade}/{level} cell {cell} (dir {d}): render returned " +
                        $"{(cells[cell] == null ? "null" : cells[cell].Length.ToString())} bytes, expected " +
                        $"{expect} for a {nativeW}×{nativeH} RGBA cell.");
            }
            render.Stop();

            // ---- union crop --------------------------------------------------------------------------
            UnionCrop(cells, nativeW, nativeH, out int cropX, out int cropY, out int cw, out int ch);

            var result = new ShopLevelBakeResult
            {
                EngineName = host.EngineName,
                OptionsJs = opts,
                NativeCellWidth = nativeW,
                NativeCellHeight = nativeH,
                CropX = cropX,
                CropY = cropY,
                CellWidth = cw,
                CellHeight = ch,
                PivotX = pivX - cropX,
                PivotY = pivY - cropY,
                Facings = facings,
                PixelsPerMetre = (int)host.EvaluateNumber($"{g}.PX"),
                FootprintWidthMetres = host.EvaluateNumber($"{g}.dims({opts}).Wd"),
                FootprintLengthMetres = host.EvaluateNumber($"{g}.dims({opts}).Ln"),
                LevelZ = host.EvaluateNumber($"{g}.dims({opts}).levelZ"),
                RenderMilliseconds = render.Elapsed.TotalMilliseconds,
            };

            // ---- pack --------------------------------------------------------------------------------
            BuildingRigBaker.ChooseGrid(cw, ch, facings, out int cols, out int rows, maxSheetDimension);
            result.Columns = cols; result.Rows = rows;

            int pw = cols * cw, ph = rows * ch;
            result.SheetWidth = pw; result.SheetHeight = ph;

            if (pw > maxSheetDimension || ph > maxSheetDimension)
                throw new InvalidOperationException(
                    $"[shops] {trade}/{level} packs to {pw}×{ph}, past the {maxSheetDimension} px cap. " +
                    "Over the cap Unity downscales the sheet SILENTLY and the sprite count still comes " +
                    "out right, so nothing but a pivot assert would catch it. Raise ShopKit.ImportSizeCap " +
                    "— it is the one number the pack, the importer lock and the verify assert all read — " +
                    "or dial this build's size down.");

            var pixels = new Color32[pw * ph];
            for (int cell = 0; cell < facings; cell++)
                BuildingRigBaker.BlitCropped(cells[cell], nativeW, nativeH, cropX, cropY, cw, ch,
                                             pixels, pw, ph, col: cell % cols, rowFromTop: cell / cols);

            result.AssetPath = $"{outputFolder}/{baseName}.png";
            Directory.CreateDirectory(Path.Combine(RigCatalog.RepoRoot, outputFolder));

            var tex = new Texture2D(pw, ph, TextureFormat.RGBA32, mipChain: false, linear: false);
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

            // ---- anchors, per CELL, in CROPPED cell px -----------------------------------------------
            // ⚠️ INDEXED BY CELL AND READ AT THE CELL'S DIR. These arrays are what every consumer uses to
            // find the door of the sprite it just loaded, so they have to be in the SHEET's frame, not the
            // rig's. Reading them at the raw index — which is what this did while the render was
            // uncorrected — describes a cell the sheet does not contain, and the two mistakes cancel
            // exactly, which is why the anchors looked self-consistent while the art was mirrored.
            result.DoorX = new double[facings]; result.DoorY = new double[facings];
            result.BackDoorX = new double[facings]; result.BackDoorY = new double[facings];
            for (int cell = 0; cell < facings; cell++)
            {
                string d = Dir(cell, facings, measured);

                result.DoorX[cell] = host.EvaluateNumber($"{g}.anchors({d},{opts}).door.x") - cropX;
                result.DoorY[cell] = host.EvaluateNumber($"{g}.anchors({d},{opts}).door.y") - cropY;

                // NaN where the plan has no back door: zero is a legal coordinate, so a missing anchor
                // written as zero puts a doorway in the corner and nothing complains.
                bool hasBack = host.EvaluateBool(
                    $"(function(){{var a={g}.anchors({d},{opts});return !!(a.backDoor);}})()");
                result.BackDoorX[cell] = hasBack
                    ? host.EvaluateNumber($"{g}.anchors({d},{opts}).backDoor.x") - cropX : double.NaN;
                result.BackDoorY[cell] = hasBack
                    ? host.EvaluateNumber($"{g}.anchors({d},{opts}).backDoor.y") - cropY : double.NaN;
            }

            total.Stop();
            result.TotalMilliseconds = total.Elapsed.TotalMilliseconds;
            return result;
        }

        /// <summary>
        /// The rig <c>dir</c> that baked CELL <paramref name="cell"/> depicts, as a JS number literal.
        /// One definition, used by both the render and the anchor read, so the sheet and the contract
        /// cannot describe different facings — which is exactly how the mirror above hid itself.
        /// </summary>
        public static string Dir(int cell, int facings, AzimuthConvention convention) =>
            RigBaker.DirForCell(cell, facings, convention).ToString("R", CultureInfo.InvariantCulture);

        /// <summary>
        /// The options literal for a level bake. Invariant culture throughout — a comma decimal separator
        /// would split <c>size:0.35</c> into two options and the rig would silently take the rig-wide
        /// default for both.
        /// </summary>
        public static string OptionsLiteral(string trade, string level, double size) =>
            $"{{type:'{trade}',level:'{level}',size:{size.ToString("R", CultureInfo.InvariantCulture)}}}";

        /// <summary>
        /// The smallest box containing every drawn pixel across ALL facings, padded. One box for the whole
        /// sheet, never per facing: the pivot is a fixed offset into the cell, so a per-facing crop would
        /// move the ground under the building as it turns.
        /// </summary>
        public static void UnionCrop(byte[][] cells, int w, int h,
                                     out int cropX, out int cropY, out int cellW, out int cellH)
        {
            int x0 = int.MaxValue, y0 = int.MaxValue, x1 = -1, y1 = -1;

            foreach (var cell in cells)
            {
                if (cell == null) continue;
                for (int y = 0; y < h; y++)
                {
                    int row = y * w * 4;
                    for (int x = 0; x < w; x++)
                    {
                        if (cell[row + x * 4 + 3] == 0) continue;
                        if (x < x0) x0 = x;
                        if (x > x1) x1 = x;
                        if (y < y0) y0 = y;
                        if (y > y1) y1 = y;
                    }
                }
            }

            if (x1 < 0)
                throw new InvalidOperationException(
                    "[shops] every facing rendered fully transparent — there is nothing to crop. The " +
                    "options resolved to a plan with no rooms, or the interior rig is not loaded and the " +
                    "merged pass had nothing to merge.");

            x0 = Mathf.Max(0, x0 - Padding);
            y0 = Mathf.Max(0, y0 - Padding);
            x1 = Mathf.Min(w - 1, x1 + Padding);
            y1 = Mathf.Min(h - 1, y1 + Padding);

            cropX = x0; cropY = y0;
            cellW = x1 - x0 + 1; cellH = y1 - y0 + 1;
        }

        public static string Describe(string key, string level, ShopLevelBakeResult r) =>
            $"  ✓ {key}/{level}: {r.CellWidth}×{r.CellHeight} cell (native {r.NativeCellWidth}×" +
            $"{r.NativeCellHeight}, {r.CropSaving:P0} cropped) → {r.Columns}×{r.Rows} sheet " +
            $"{r.SheetWidth}×{r.SheetHeight}, pivot ({r.PivotX:F1},{r.PivotY:F1}), " +
            $"{r.FootprintWidthMetres:F2}×{r.FootprintLengthMetres:F2} m, levelZ {r.LevelZ:F2}, " +
            $"{r.PngBytes / 1024.0:F0} KiB";
    }
}
#endif
