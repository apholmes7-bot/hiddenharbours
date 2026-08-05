using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace HiddenHarbours.Tools.RigBaking
{
    /// <summary>
    /// What to bake: one BUILD of one building rig, through the 8-facing turntable.
    ///
    /// <para>A "build" is a JS options expression, not a preset name — because the Building Studio lets
    /// the owner dial every axis by hand and bake the result, and a preset is just the special case
    /// where those options come from the rig's own table. <see cref="FromPreset"/> makes that case a
    /// one-liner; <see cref="FromOptions"/> takes a hand-dialled build.</para>
    /// </summary>
    public readonly struct BuildingBakeRequest
    {
        /// <summary>Catalog key — <c>"house"</c> or <c>"wharfBuilding"</c>.</summary>
        public readonly string RigKey;

        /// <summary>
        /// The JS expression evaluating to the options object handed to <c>render()</c>. For a preset
        /// this spreads the rig's own table; for a studio build it is a literal of the dialled axes.
        /// </summary>
        public readonly string OptsJs;

        /// <summary>Human label for logs and the sidecar — a preset name, or the studio's build name.</summary>
        public readonly string Label;

        /// <summary>Project-relative output folder, e.g. <c>"Assets/_Project/Art/Sprites/Buildings"</c>.</summary>
        public readonly string OutputFolder;

        /// <summary>Sheet stem (no extension).</summary>
        public readonly string BaseName;

        /// <summary>Facings to bake. 8 is the ADR-0006 recipe and what these rigs are drawn for.</summary>
        public readonly int Facings;

        /// <summary>
        /// True when <see cref="OptsJs"/> came from the rig's PRESETS table, so the bake can check the
        /// name exists and the table entry is not empty.
        /// </summary>
        public readonly bool IsPreset;

        /// <summary>
        /// Refuse the bake if this build renders byte-identical to the rig's DEFAULT.
        ///
        /// <para>Always on for a preset (see <see cref="FromPreset"/> for the bug it exists to catch).
        /// OFF by default for a hand-dialled build, because the Building Studio's owner may legitimately
        /// bake the default — but a build that is <i>committed to a kit</i> should opt in: a dialled set
        /// that resolves to the default means every key in it was silently ignored, which is the same
        /// failure wearing different clothes.</para>
        /// </summary>
        public readonly bool RequireDistinctFromDefault;

        /// <summary>
        /// Largest sheet dimension the pack may produce, or 0 for
        /// <see cref="BuildingRigBaker.MaxTextureSize"/> (4096 — Unity's hard cap).
        ///
        /// <para>A kit that means to import at Unity's DEFAULT 2048 cap must say 2048 here, because the
        /// two limits fail in opposite directions: over 4096 the bake refuses, but between 2048 and 4096
        /// the bake succeeds and the IMPORT silently downscales — with the sprite count still coming out
        /// right. Choosing the grid against the number the consumer will actually import at is the only
        /// way the two cannot disagree.</para>
        /// </summary>
        public readonly int MaxSheetDimension;

        public BuildingBakeRequest(string rigKey, string optsJs, string label, string outputFolder,
                                   string baseName, int facings = 8, bool isPreset = false,
                                   bool requireDistinctFromDefault = false, int maxSheetDimension = 0)
        {
            RigKey = rigKey; OptsJs = optsJs; Label = label; OutputFolder = outputFolder;
            BaseName = baseName; Facings = facings; IsPreset = isPreset;
            RequireDistinctFromDefault = requireDistinctFromDefault;
            MaxSheetDimension = maxSheetDimension;
        }

        /// <summary>
        /// Bake one of the rig's own presets.
        ///
        /// <para>⚠️ Note the options are SPREAD, not named. <c>render(dir,{preset:'netShed'})</c> is
        /// silently wrong — no building rig's <c>resolve()</c> reads a <c>preset</c> key, so it renders
        /// the DEFAULT build with no error at all. This is the one place that spelling is decided.</para>
        /// </summary>
        public static BuildingBakeRequest FromPreset(string rigKey, string preset, string globalName,
                                                     string outputFolder, string baseName, int facings = 8,
                                                     int maxSheetDimension = 0)
            => new BuildingBakeRequest(
                rigKey,
                $"Object.assign({{}},{globalName}.PRESETS['{preset.Replace("'", "\\'")}'])",
                preset, outputFolder, baseName, facings, isPreset: true,
                requireDistinctFromDefault: true, maxSheetDimension: maxSheetDimension);

        /// <summary>
        /// Bake a hand-dialled build (the Building Studio's "Bake this build", or a kit's committed
        /// build table). <paramref name="requireDistinctFromDefault"/> opts into the
        /// did-the-options-actually-apply tripwire — a kit should pass true, the studio should not.
        /// </summary>
        public static BuildingBakeRequest FromOptions(string rigKey, string optsJs, string label,
                                                      string outputFolder, string baseName,
                                                      int facings = 8,
                                                      bool requireDistinctFromDefault = false,
                                                      int maxSheetDimension = 0)
            => new BuildingBakeRequest(rigKey, optsJs, label, outputFolder, baseName, facings,
                                       isPreset: false,
                                       requireDistinctFromDefault: requireDistinctFromDefault,
                                       maxSheetDimension: maxSheetDimension);
    }

    public sealed class BuildingBakeResult
    {
        public string RigKey, Preset, AssetPath, SidecarPath, EngineName;
        public AzimuthConvention MeasuredConvention;
        public string ConventionReport;

        /// <summary>The rig's native cell, before cropping.</summary>
        public int NativeCellWidth, NativeCellHeight;

        /// <summary>The cropped cell every facing is packed at.</summary>
        public int CellWidth, CellHeight;

        /// <summary>Crop origin in the native cell (top-left space) — what the pivot shifts by.</summary>
        public int CropX, CropY;

        /// <summary>Pivot in the CROPPED cell, top-left origin px.</summary>
        public double PivotX, PivotY;

        public int Columns, Rows, Facings;
        public int SheetWidth, SheetHeight;
        public long PngBytes;
        public double RenderMilliseconds, TotalMilliseconds;

        /// <summary>Building footprint the rig reports, in metres — the honest-scale number a
        /// consumer checks its placement against.</summary>
        public double FootprintWidthMeters, FootprintLengthMeters;

        /// <summary>The rig's own <c>PX</c> (pixels per metre). Read from the rig rather than taken
        /// from <c>ArtImportPipeline.PixelsPerUnit</c>, so a kit contract records the scale the sheet
        /// was actually drawn at instead of the scale we hope it was.</summary>
        public int PixelsPerMetre;

        /// <summary>
        /// Per-facing door anchor in CROPPED cell px, top-left origin — the same values the sidecar
        /// carries, surfaced on the result so a kit contract can be written from one bake call without
        /// re-parsing the JSON it just wrote.
        /// </summary>
        public double[] DoorX, DoorY;

        /// <summary>Pixels the crop saved, as a fraction of the native cell area.</summary>
        public double CropSaving =>
            1.0 - (CellWidth * (double)CellHeight) / (NativeCellWidth * (double)NativeCellHeight);

        public long RuntimeBytesRgba32 => (long)SheetWidth * SheetHeight * 4;

        /// <summary>What the same bake would have cost uncropped — the number that justifies the crop.</summary>
        public long UncroppedBytesRgba32 =>
            (long)(Columns * NativeCellWidth) * (Rows * NativeCellHeight) * 4;
    }

    /// <summary>
    /// The rendered-and-cropped state of one build — every facing's native cell, plus the union crop
    /// and the pivot that moved with it. What <see cref="BuildingRigBaker.RenderCells"/> returns.
    ///
    /// <para><b>This type exists so a PREVIEW and a BAKE cannot disagree.</b> The Rig Studio shows the
    /// owner one cell of this set; the bake packs the same set into a sheet. Both come from ONE call
    /// path — <see cref="BuildingRigBaker.RenderCells"/> — so the pixels the owner approves are, by
    /// construction, the pixels a bake writes (the flick-cast lesson: a second renderer that can
    /// disagree with the bake means the owner approves art that never ships). A test pins the
    /// bit-identity anyway, so a refactor that splits the paths fails loudly.</para>
    /// </summary>
    public sealed class BuildingCellSet
    {
        public string RigKey;
        public RigGeometry Geometry;
        public BuildingRigAzimuthProbe.Result Probe;

        /// <summary>The rig's own <c>PX</c> (pixels per metre).</summary>
        public int PixelsPerMetre;

        /// <summary>One NATIVE cell per facing — RGBA, top-left-origin rows, exactly as the rig's
        /// <c>render()</c> returned them. Cell <c>i</c> depicts +45°·i (the measured convention is
        /// already applied via <see cref="RigBaker.DirForCell"/>).</summary>
        public byte[][] Cells;

        /// <summary>The union crop over all facings, padded by <see cref="BuildingRigBaker.Padding"/> —
        /// ONE rect for the whole set, so every facing shares a cell size and a pivot.</summary>
        public int CropX, CropY, CellWidth, CellHeight;

        /// <summary>Pivot in the CROPPED cell, top-left origin px — the ground centre.</summary>
        public double PivotX, PivotY;

        public double RenderMilliseconds;

        /// <summary>
        /// One facing, cropped — RGBA, top-left-origin rows, <see cref="CellWidth"/>×<see cref="CellHeight"/>.
        /// The exact bytes the bake blits into that facing's cell of the sheet.
        /// </summary>
        public byte[] CroppedCell(int facing)
        {
            if (facing < 0 || facing >= Cells.Length)
                throw new ArgumentOutOfRangeException(nameof(facing),
                    $"facing {facing} of a {Cells.Length}-facing set.");

            byte[] src = Cells[facing];
            int srcW = Geometry.Width;
            var dst = new byte[CellWidth * CellHeight * 4];

            for (int y = 0; y < CellHeight; y++)
            {
                int srcRow = ((CropY + y) * srcW + CropX) * 4;
                Buffer.BlockCopy(src, srcRow, dst, y * CellWidth * 4, CellWidth * 4);
            }
            return dst;
        }
    }

    /// <summary>
    /// Bakes the two BUILDING rigs — <c>houseIsoRig</c> and <c>wharfBuildingRig</c> — through the shared
    /// ¾ turntable, and <b>tight-crops the result</b>.
    ///
    /// <para><b>The crop is not an optimisation here; it is the reason a bake is possible at all.</b>
    /// Both rigs use one cell big enough for their LARGEST build — the house at 992×1060, the wharf
    /// building at 1200×1160 (it must hold the <c>cannery</c>). A net shed drawn in that cell is a small
    /// object in a 37 m × 36 m frame. Uncropped, eight facings of a wharf building need 3600×3480 —
    /// 50 MB of RGBA32 for ONE preset of ONE building, and the kit ships seven presets. That is why the
    /// zip's own reference sheet (9600×1160) was left in <c>docs/</c> and never imported: over Unity's
    /// 2048 default cap it imports silently downscaled, and lifting the cap to hold it means a 16384 px
    /// texture, ≈134 MB. Cropping to the drawn pixels turns each preset into a few hundred KB.</para>
    ///
    /// <para><b>ONE crop rect for all eight facings, not one per cell.</b> Two reasons, and both are
    /// load-bearing: a grid slice requires every cell to be the same size, and — the one that actually
    /// bites — <b>the pivot must be identical across facings or the building shifts when it turns</b>.
    /// That is the same rule the boat kits state as "so a heading swap never shifts the boat". So the
    /// crop is the UNION of all eight silhouettes, and the pivot moves with it by exactly the crop
    /// origin.</para>
    ///
    /// <para><b>The pivot therefore becomes DATA, not a constant.</b> Every other sheet in this repo
    /// pins its pivot with a named const (<c>DoryWaterline</c>, <c>PuntOrigin</c>) because the cell is
    /// fixed by the kit. Here the cell depends on the preset — a cannery crops differently from a
    /// shack — so the pivot is written to a sidecar JSON beside the PNG and the slicer reads it. Baking
    /// a preset and hard-coding its pivot in C# would be wrong the first time the preset was re-baked at
    /// a different size.</para>
    ///
    /// <para>Everything else follows <see cref="RigBaker"/>: the convention is MEASURED (by
    /// <see cref="BuildingRigAzimuthProbe"/>, which reads the door rather than a bow taper — a building
    /// has no bow), the bake REFUSES on a mismatch with the catalog's declaration, and the rig source
    /// runs unmodified per ADR 0021 §5.</para>
    /// </summary>
    public static class BuildingRigBaker
    {
        /// <summary>Unity's hard texture cap. Over it, a sheet imports SILENTLY downscaled.</summary>
        public const int MaxTextureSize = RigBaker.MaxTextureSize;

        /// <summary>
        /// Transparent pixels kept around the art. One pixel of margin stops a neighbouring cell's
        /// colour bleeding in under bilinear sampling — irrelevant at Point filter, which this project
        /// locks, but free insurance and it also keeps the keyline from touching the cell edge.
        /// </summary>
        public const int Padding = 1;

        public static BuildingBakeResult Bake(BuildingBakeRequest req)
        {
            var total = Stopwatch.StartNew();

            using IRigScriptHost host = RigScriptHostFactory.Create();
            BuildingCellSet set = RenderCells(req, host);

            var geo = set.Geometry;
            string g = RigCatalog.Get(req.RigKey).GlobalName;
            string optsJs = req.OptsJs;

            var result = new BuildingBakeResult
            {
                RigKey = req.RigKey,
                Preset = req.Label,
                EngineName = host.EngineName,
                NativeCellWidth = geo.Width,
                NativeCellHeight = geo.Height,
                Facings = req.Facings,
                PixelsPerMetre = set.PixelsPerMetre,
                MeasuredConvention = set.Probe.Convention,
                ConventionReport = set.Probe.Report,
                FootprintWidthMeters = set.Probe.WidthMeters,
                FootprintLengthMeters = set.Probe.LengthMeters,
                CropX = set.CropX,
                CropY = set.CropY,
                CellWidth = set.CellWidth,
                CellHeight = set.CellHeight,
                PivotX = set.PivotX,
                PivotY = set.PivotY,
            };

            int cw = set.CellWidth, ch = set.CellHeight;

            // ---- Pack ------------------------------------------------------------------------------
            ChooseGrid(cw, ch, req.Facings, out int cols, out int rows, req.MaxSheetDimension);
            result.Columns = cols; result.Rows = rows;

            int pw = cols * cw, ph = rows * ch;
            result.SheetWidth = pw; result.SheetHeight = ph;

            var pixels = new Color32[pw * ph];
            for (int cell = 0; cell < req.Facings; cell++)
                BlitCropped(set.Cells[cell], geo.Width, geo.Height, set.CropX, set.CropY, cw, ch,
                            pixels, pw, ph, col: cell % cols, rowFromTop: cell / cols);

            string fileName = $"{req.BaseName}.png";
            result.AssetPath = $"{req.OutputFolder}/{fileName}";

            string outAbs = Path.Combine(RigCatalog.RepoRoot, req.OutputFolder);
            Directory.CreateDirectory(outAbs);

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

            result.SidecarPath = WriteSidecar(host, g, optsJs, req, result, set.Probe);

            result.RenderMilliseconds = set.RenderMilliseconds;
            total.Stop();
            result.TotalMilliseconds = total.Elapsed.TotalMilliseconds;
            return result;
        }

        /// <summary>
        /// <b>THE render path</b> — install the rig, run every guard, MEASURE the convention, render
        /// all facings and compute the union crop. <see cref="Bake"/> packs this into a sheet; the Rig
        /// Studio's preview shows one cell of it. There is deliberately no second way to render a
        /// building: a preview renderer that could disagree with the bake means the owner approves art
        /// that never ships, so both callers share this method and a test pins the bit-identity.
        ///
        /// <para><paramref name="host"/> is caller-owned (the studio keeps one alive across previews;
        /// the bake creates and disposes its own). The rig source is (re)executed into it here, so a
        /// host that last held a different rig is fine.</para>
        /// </summary>
        public static BuildingCellSet RenderCells(BuildingBakeRequest req, IRigScriptHost host)
        {
            var entry = RigCatalog.Get(req.RigKey);
            var geo = RigCatalog.Install(host, entry);
            string g = entry.GlobalName;

            // The options expression comes from the request — a spread of the rig's own PRESETS table,
            // or a build the owner dialled in the Rig Studio. BuildingBakeRequest.FromPreset owns
            // the spelling of the preset case, including the {preset:'name'} trap documented there.
            string optsJs = req.OptsJs;
            if (req.IsPreset) AssertPresetExists(host, g, req.Label);

            // The geometry guard runs for EVERY build, so the probe and the crop can both assume the
            // render came back at the rig's declared cell size.
            AssertRenders(host, g, optsJs, req.Label, geo.Width, geo.Height);

            // The did-it-actually-apply tripwire. Always on for a preset; opt-in for a dialled build,
            // because the Studio may legitimately bake the rig default (see the request's docs).
            if (req.IsPreset) AssertPresetIsNotEmpty(host, g, req.Label);
            if (req.RequireDistinctFromDefault)
                AssertDiffersFromDefault(host, g, optsJs, req.Label, req.IsPreset);

            // ---- MEASURE the convention, then cross-check the catalog's declaration ---------------
            var probe = BuildingRigAzimuthProbe.Measure(host, g, optsJs, geo.Width, geo.Height, geo.PivotX);

            if (probe.Convention != entry.DeclaredConvention)
                throw new InvalidOperationException(
                    $"AZIMUTH MISMATCH on rig '{req.RigKey}'.\n" +
                    $"  docs/art/rigs/README.md declares : {entry.DeclaredConvention}\n" +
                    $"  the measurement says             : {probe.Convention}\n\n" +
                    probe.Report + "\n\n" +
                    "The bake is refusing rather than guessing. A silent guess here is how this " +
                    "mislabel shipped defects in five kits. Decide which is right and correct the " +
                    "README (or the catalog) — do not relax this check.");

            // ---- Render every facing ONCE, keep them, then crop ----------------------------------
            // Held in memory rather than rendered twice: a wharf building cell is 5.6 MB, so eight are
            // 45 MB for the length of one bake — cheap next to re-running the rasteriser, which is the
            // expensive half (the rig z-buffers and dithers every face in JS).
            var renderClock = new Stopwatch();
            var cells = new byte[req.Facings][];

            for (int cell = 0; cell < req.Facings; cell++)
            {
                double dir = RigBaker.DirForCell(cell, req.Facings, probe.Convention);
                string d = dir.ToString("R", CultureInfo.InvariantCulture);

                renderClock.Start();
                cells[cell] = host.EvaluateBytes($"{g}.render({d},{optsJs})");
                renderClock.Stop();

                if (cells[cell].Length != geo.Width * geo.Height * 4)
                    throw new InvalidOperationException(
                        $"Cell {cell} came back {cells[cell].Length} bytes, expected " +
                        $"{geo.Width * geo.Height * 4} for {geo.Width}×{geo.Height} RGBA.");
            }

            UnionAlphaBounds(cells, geo.Width, geo.Height,
                             out int xMin, out int yMin, out int xMax, out int yMax);

            if (xMax < xMin)
                throw new InvalidOperationException(
                    $"Every facing of '{req.Label}' rendered fully transparent. Nothing to bake — " +
                    "the preset name resolved but drew no pixels.");

            // Pad, then clamp back inside the native cell.
            xMin = Mathf.Max(0, xMin - Padding);
            yMin = Mathf.Max(0, yMin - Padding);
            xMax = Mathf.Min(geo.Width - 1, xMax + Padding);
            yMax = Mathf.Min(geo.Height - 1, yMax + Padding);

            // THE PIVOT MOVES WITH THE CROP. The rig reports it in native-cell top-left space; after
            // cropping, the same world point sits (cropX, cropY) closer to the origin. Getting this
            // wrong does not fail loudly — every building simply stands in the wrong place, by the
            // crop offset, consistently enough to look like a placement bug rather than a bake one.
            return new BuildingCellSet
            {
                RigKey = req.RigKey,
                Geometry = geo,
                Probe = probe,
                PixelsPerMetre = (int)host.EvaluateNumber($"{g}.PX"),
                Cells = cells,
                CropX = xMin,
                CropY = yMin,
                CellWidth = xMax - xMin + 1,
                CellHeight = yMax - yMin + 1,
                PivotX = geo.PivotX - xMin,
                PivotY = geo.PivotY - yMin,
                RenderMilliseconds = renderClock.Elapsed.TotalMilliseconds,
            };
        }

        // ---- the crop ------------------------------------------------------------------------------

        /// <summary>
        /// Union of every facing's alpha bounds. UNION, not per-cell: all eight must share one cell size
        /// (a grid slice needs it) and one pivot (or the building jumps as it turns).
        /// </summary>
        public static void UnionAlphaBounds(IReadOnlyList<byte[]> cells, int width, int height,
                                            out int xMin, out int yMin, out int xMax, out int yMax)
        {
            xMin = width; yMin = height; xMax = -1; yMax = -1;

            foreach (var rgba in cells)
            {
                BuildingRigAzimuthProbe.AlphaBounds(rgba, width, height,
                                                    out int x0, out int y0, out int x1, out int y1);
                if (x1 < x0) continue;                       // fully transparent facing
                if (x0 < xMin) xMin = x0;
                if (y0 < yMin) yMin = y0;
                if (x1 > xMax) xMax = x1;
                if (y1 > yMax) yMax = y1;
            }
        }

        /// <summary>
        /// Widest grid that keeps both sheet dimensions under the cap, preferring FEWER rows so the sheet
        /// stays wide and short (which is how every other turntable sheet in the repo reads). Throws if
        /// even one column per row is too big — silently emitting an over-cap sheet is the failure this
        /// whole baker exists to avoid.
        ///
        /// <para><paramref name="maxDimension"/> defaults to <see cref="MaxTextureSize"/> (4096, Unity's
        /// hard limit). A kit whose consumer imports at the DEFAULT 2048 cap must pass 2048 —
        /// see <see cref="BuildingBakeRequest.MaxSheetDimension"/> for why the two numbers cannot be
        /// allowed to differ.</para>
        /// </summary>
        public static void ChooseGrid(int cellW, int cellH, int cells, out int cols, out int rows,
                                      int maxDimension = 0)
        {
            int cap = maxDimension > 0 ? Mathf.Min(maxDimension, MaxTextureSize) : MaxTextureSize;
            int maxCols = Mathf.Max(1, cap / Mathf.Max(1, cellW));

            for (int c = Mathf.Min(cells, maxCols); c >= 1; c--)
            {
                int r = Mathf.CeilToInt(cells / (float)c);
                if (r * cellH <= cap && c * cellW <= cap)
                {
                    cols = c; rows = r;
                    return;
                }
            }

            throw new InvalidOperationException(
                $"A {cellW}×{cellH} cell cannot hold {cells} facings under the {cap} px cap in any grid. " +
                "Either the crop did not shrink this build enough (check the options actually resolved — " +
                "an uncropped cell means the silhouette filled the frame), or the kit is asking for more " +
                "facings than one texture of this cell size can carry.");
        }

        /// <summary>
        /// Copy one cropped native cell into a packed sheet, flipping the rig's TOP-LEFT-origin rows to
        /// Unity's bottom-origin ones.
        ///
        /// <para><c>public</c> so <c>InteriorRigBaker</c> packs through the SAME flip rather than
        /// carrying a second copy of it. A duplicated row flip is the kind of thing that stays right
        /// until one of the two is touched, and then the two kits disagree by a mirror.</para>
        /// </summary>
        public static void BlitCropped(byte[] src, int srcW, int srcH, int cropX, int cropY, int cw, int ch,
                                       Color32[] dst, int pw, int ph, int col, int rowFromTop)
        {
            int x0 = col * cw;
            int yTop = rowFromTop * ch;

            for (int y = 0; y < ch; y++)
            {
                int srcY = cropY + y;
                if (srcY < 0 || srcY >= srcH) continue;

                // The rigs hand back TOP-LEFT-origin rows; Unity textures are bottom-origin. Same flip
                // as RigBaker.Blit — kept here rather than shared because that one crops nothing and
                // widening its signature to take a crop rect would complicate every existing caller.
                int unityY = ph - 1 - (yTop + y);
                int dstRow = unityY * pw + x0;
                int srcRow = srcY * srcW * 4;

                for (int x = 0; x < cw; x++)
                {
                    int srcX = cropX + x;
                    if (srcX < 0 || srcX >= srcW) continue;
                    int s = srcRow + srcX * 4;
                    dst[dstRow + x] = new Color32(src[s], src[s + 1], src[s + 2], src[s + 3]);
                }
            }
        }

        // ---- the sidecar ---------------------------------------------------------------------------

        /// <summary>
        /// Writes the bake's contract beside the PNG: cell geometry, the CROPPED pivot, the measured
        /// convention, and the rig's per-facing overlay anchors (chimney/stack tops, door, ridge) in
        /// CROPPED cell pixels so a runtime overlay lands without re-deriving the crop.
        /// </summary>
        static string WriteSidecar(IRigScriptHost host, string g, string optsJs,
                                   in BuildingBakeRequest req, BuildingBakeResult r,
                                   in BuildingRigAzimuthProbe.Result probe)
        {
            // houseIsoRig calls its roof stacks "chimneys"; wharfBuildingRig calls them "stacks". Same
            // concept, different key — ask the rig which it has rather than keying off the rig name, so
            // a future building rig with either spelling works untouched.
            bool hasStacks = host.EvaluateBool($"Array.isArray({g}.anchors(0,{optsJs}).stacks)");
            string stackKey = hasStacks ? "stacks" : "chimneys";

            // Surfaced on the result as well as written to the sidecar, so a kit contract can be built
            // from one bake call instead of re-parsing the JSON this method has just written.
            r.DoorX = new double[req.Facings];
            r.DoorY = new double[req.Facings];

            var sb = new StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine($"  \"sheet\": \"{Path.GetFileName(r.AssetPath)}\",");
            sb.AppendLine($"  \"rig\": \"{req.RigKey}\",");
            sb.AppendLine($"  \"build\": \"{Escape(req.Label)}\",");
            sb.AppendLine($"  \"facings\": {r.Facings},");
            sb.AppendLine($"  \"cols\": {r.Columns},");
            sb.AppendLine($"  \"rows\": {r.Rows},");
            sb.AppendLine($"  \"cellW\": {r.CellWidth},");
            sb.AppendLine($"  \"cellH\": {r.CellHeight},");
            sb.AppendLine("  \"pivotNote\": \"cell px from the cell's TOP-LEFT; Unity wants " +
                          "(x/cellW, (cellH-y)/cellH)\",");
            sb.AppendLine($"  \"pivotX\": {Num(r.PivotX)},");
            sb.AppendLine($"  \"pivotY\": {Num(r.PivotY)},");
            sb.AppendLine($"  \"nativeCellW\": {r.NativeCellWidth},");
            sb.AppendLine($"  \"nativeCellH\": {r.NativeCellHeight},");
            sb.AppendLine($"  \"cropX\": {r.CropX},");
            sb.AppendLine($"  \"cropY\": {r.CropY},");
            sb.AppendLine($"  \"convention\": \"{r.MeasuredConvention}\",");
            sb.AppendLine("  \"conventionNote\": \"MEASURED at bake time from the door anchor; the " +
                          "correction is already applied, so cell i genuinely depicts +45*i\",");
            sb.AppendLine($"  \"footprintWd\": {Num(probe.WidthMeters)},");
            sb.AppendLine($"  \"footprintLn\": {Num(probe.LengthMeters)},");
            sb.AppendLine($"  \"stackKey\": \"{stackKey}\",");
            sb.AppendLine("  \"anchors\": [");

            for (int cell = 0; cell < req.Facings; cell++)
            {
                double dir = RigBaker.DirForCell(cell, req.Facings, r.MeasuredConvention);
                string d = dir.ToString("R", CultureInfo.InvariantCulture);
                string a = $"{g}.anchors({d},{optsJs})";

                double doorX = host.EvaluateNumber($"{a}.door.x") - r.CropX;
                double doorY = host.EvaluateNumber($"{a}.door.y") - r.CropY;
                r.DoorX[cell] = doorX;
                r.DoorY[cell] = doorY;
                double ridgeX = host.EvaluateNumber($"{a}.ridge.x") - r.CropX;
                double ridgeY = host.EvaluateNumber($"{a}.ridge.y") - r.CropY;
                int stackCount = (int)host.EvaluateNumber($"{a}.{stackKey}.length");

                var stacks = new StringBuilder();
                for (int s = 0; s < stackCount; s++)
                {
                    if (s > 0) stacks.Append(", ");
                    double sx = host.EvaluateNumber($"{a}.{stackKey}[{s}].x") - r.CropX;
                    double sy = host.EvaluateNumber($"{a}.{stackKey}[{s}].y") - r.CropY;
                    stacks.Append($"{{\"x\": {Num(sx)}, \"y\": {Num(sy)}}}");
                }

                sb.Append($"    {{ \"cell\": {cell}, \"door\": {{\"x\": {Num(doorX)}, \"y\": {Num(doorY)}}}, " +
                          $"\"ridge\": {{\"x\": {Num(ridgeX)}, \"y\": {Num(ridgeY)}}}, " +
                          $"\"{stackKey}\": [{stacks}] }}");
                sb.AppendLine(cell == req.Facings - 1 ? "" : ",");
            }

            sb.AppendLine("  ]");
            sb.AppendLine("}");

            string path = $"{req.OutputFolder}/{req.BaseName}.json";
            File.WriteAllText(Path.Combine(RigCatalog.RepoRoot, path), sb.ToString());
            return path;
        }

        // ---- helpers -------------------------------------------------------------------------------

        /// <summary>An empty PRESETS entry cannot describe a build, and would pass the
        /// differs-from-default check only by accident.</summary>
        static void AssertPresetIsNotEmpty(IRigScriptHost host, string g, string preset)
        {
            if (host.EvaluateNumber($"Object.keys({g}.PRESETS['{Escape(preset)}']).length") <= 0)
                throw new InvalidOperationException(
                    $"Preset '{preset}' of {g} is an EMPTY options object — it cannot describe a build.");
        }

        /// <summary>
        /// Assert the options actually CHANGED the render.
        ///
        /// <para>This exists for one specific, already-made mistake: <c>render(dir,{preset:'netShed'})</c>
        /// looks exactly like the right call and is silently wrong, because <c>resolve()</c> has no
        /// <c>preset</c> key and quietly falls through to the default build. Nothing throws; you just get
        /// seven identical sheds under seven different names, and the only way to notice is to look at
        /// all seven side by side. A dialled build has the same failure through a different door: both
        /// rigs read options as <c>opts[k] != null ? opts[k] : fallback</c>, so a misspelled KEY is
        /// ignored in silence too.</para>
        ///
        /// <para>The check is a byte comparison of one facing against the rig's default. It is not a
        /// proof that every field applied — a build differing from the default only in some field this
        /// facing does not show would still pass — but it catches the whole-options-ignored case, which
        /// is the one that has teeth. Per-KEY coverage is a separate matter, and it is handled where it
        /// belongs: every key a kit dials is a <c>BuildingAxes</c> key, and <c>BuildingAxesTests</c>
        /// greps each of those out of the rig source.</para>
        /// </summary>
        static void AssertDiffersFromDefault(IRigScriptHost host, string g, string optsJs, string label,
                                             bool isPreset)
        {
            byte[] dialled = host.EvaluateBytes($"{g}.render(0,{optsJs})");
            byte[] plain = host.EvaluateBytes($"{g}.render(0,{{}})");

            if (dialled.Length != plain.Length || !BytesEqual(dialled, plain)) return;

            throw new InvalidOperationException(
                $"Build '{label}' rendered byte-identical to the rig's DEFAULT build.\n\n" +
                (isPreset
                    ? "That almost certainly means the options are not reaching resolve() — the classic " +
                      "form of this bug is passing {preset:'name'}, which no building rig reads, instead " +
                      "of spreading PRESETS['name'] into the options. Refusing rather than baking seven " +
                      "identical sheds under seven different names."
                    : "Every dialled key was therefore ignored. These rigs resolve options as " +
                      "opts[k] != null ? opts[k] : fallback, so an unknown KEY is accepted in silence — " +
                      "the recorded worked example is winD (the rig's internal field) versus winDensity " +
                      "(the option it actually reads). Check the keys against BuildingAxes, which is " +
                      "grep-verified against the rig source."));
        }

        /// <summary>
        /// The geometry guard every build needs: the crop and the probe both assume the render came back
        /// at the rig's declared cell size.
        /// </summary>
        static void AssertRenders(IRigScriptHost host, string g, string optsJs, string label,
                                  int width, int height)
        {
            byte[] rgba = host.EvaluateBytes($"{g}.render(0,{optsJs})");
            if (rgba.Length != width * height * 4)
                throw new InvalidOperationException(
                    $"Build '{label}' rendered {rgba.Length} bytes, expected {width * height * 4} " +
                    $"for {width}×{height} RGBA.");
        }

        static bool BytesEqual(byte[] a, byte[] b)
        {
            for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
            return true;
        }

        static void AssertPresetExists(IRigScriptHost host, string g, string preset)
        {
            if (host.EvaluateBool($"typeof {g}.PRESETS === 'object' && " +
                                  $"{g}.PRESETS['{Escape(preset)}'] !== undefined"))
                return;

            string known = host.EvaluateBool($"typeof {g}.PRESETS === 'object'")
                ? host.EvaluateString($"Object.keys({g}.PRESETS).join(', ')")
                : "(the rig exposes no PRESETS table)";

            throw new ArgumentException(
                $"'{preset}' is not a preset of {g}. Known: {known}.");
        }

        static string Escape(string s) => s?.Replace("\\", "\\\\").Replace("'", "\\'") ?? "";

        static string Num(double v) => v.ToString("0.####", CultureInfo.InvariantCulture);
    }
}
