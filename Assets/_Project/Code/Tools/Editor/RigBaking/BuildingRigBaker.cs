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
        /// True when <see cref="OptsJs"/> came from the rig's PRESETS table, so the bake can run the
        /// "did the preset actually apply?" tripwire. A hand-dialled build has nothing to compare
        /// against — matching the default is a legitimate thing for the owner to ask for.
        /// </summary>
        public readonly bool IsPreset;

        public BuildingBakeRequest(string rigKey, string optsJs, string label, string outputFolder,
                                   string baseName, int facings = 8, bool isPreset = false)
        {
            RigKey = rigKey; OptsJs = optsJs; Label = label; OutputFolder = outputFolder;
            BaseName = baseName; Facings = facings; IsPreset = isPreset;
        }

        /// <summary>
        /// Bake one of the rig's own presets.
        ///
        /// <para>⚠️ Note the options are SPREAD, not named. <c>render(dir,{preset:'netShed'})</c> is
        /// silently wrong — no building rig's <c>resolve()</c> reads a <c>preset</c> key, so it renders
        /// the DEFAULT build with no error at all. This is the one place that spelling is decided.</para>
        /// </summary>
        public static BuildingBakeRequest FromPreset(string rigKey, string preset, string globalName,
                                                     string outputFolder, string baseName, int facings = 8)
            => new BuildingBakeRequest(
                rigKey,
                $"Object.assign({{}},{globalName}.PRESETS['{preset.Replace("'", "\\'")}'])",
                preset, outputFolder, baseName, facings, isPreset: true);

        /// <summary>Bake a hand-dialled build (the Building Studio's "Bake this build").</summary>
        public static BuildingBakeRequest FromOptions(string rigKey, string optsJs, string label,
                                                      string outputFolder, string baseName, int facings = 8)
            => new BuildingBakeRequest(rigKey, optsJs, label, outputFolder, baseName, facings,
                                       isPreset: false);
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

        /// <summary>Pixels the crop saved, as a fraction of the native cell area.</summary>
        public double CropSaving =>
            1.0 - (CellWidth * (double)CellHeight) / (NativeCellWidth * (double)NativeCellHeight);

        public long RuntimeBytesRgba32 => (long)SheetWidth * SheetHeight * 4;

        /// <summary>What the same bake would have cost uncropped — the number that justifies the crop.</summary>
        public long UncroppedBytesRgba32 =>
            (long)(Columns * NativeCellWidth) * (Rows * NativeCellHeight) * 4;
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
            var entry = RigCatalog.Get(req.RigKey);

            using IRigScriptHost host = RigScriptHostFactory.Create();
            var geo = RigCatalog.Install(host, entry);
            string g = entry.GlobalName;

            // The options expression comes from the request — a spread of the rig's own PRESETS table,
            // or a build the owner dialled in the Building Studio. BuildingBakeRequest.FromPreset owns
            // the spelling of the preset case, including the {preset:'name'} trap documented there.
            string optsJs = req.OptsJs;
            if (req.IsPreset) AssertPresetExists(host, g, req.Label);

            var result = new BuildingBakeResult
            {
                RigKey = req.RigKey,
                Preset = req.Label,
                EngineName = host.EngineName,
                NativeCellWidth = geo.Width,
                NativeCellHeight = geo.Height,
                Facings = req.Facings,
            };

            // The did-it-actually-apply tripwire only makes sense for a preset: a hand-dialled build
            // that happens to match the rig default is a legitimate thing for the owner to ask for.
            if (req.IsPreset) AssertPresetApplies(host, g, optsJs, req.Label, geo.Width, geo.Height);
            else AssertRenders(host, g, optsJs, req.Label, geo.Width, geo.Height);

            // ---- MEASURE the convention, then cross-check the catalog's declaration ---------------
            var probe = BuildingRigAzimuthProbe.Measure(host, g, optsJs, geo.Width, geo.Height, geo.PivotX);
            result.MeasuredConvention = probe.Convention;
            result.ConventionReport = probe.Report;

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

            int cw = xMax - xMin + 1;
            int ch = yMax - yMin + 1;

            result.CropX = xMin; result.CropY = yMin;
            result.CellWidth = cw; result.CellHeight = ch;

            // THE PIVOT MOVES WITH THE CROP. The rig reports it in native-cell top-left space; after
            // cropping, the same world point sits (cropX, cropY) closer to the origin. Getting this
            // wrong does not fail loudly — every building simply stands in the wrong place, by the
            // crop offset, consistently enough to look like a placement bug rather than a bake one.
            result.PivotX = geo.PivotX - xMin;
            result.PivotY = geo.PivotY - yMin;

            // ---- Pack ------------------------------------------------------------------------------
            ChooseGrid(cw, ch, req.Facings, out int cols, out int rows);
            result.Columns = cols; result.Rows = rows;

            int pw = cols * cw, ph = rows * ch;
            result.SheetWidth = pw; result.SheetHeight = ph;

            var pixels = new Color32[pw * ph];
            for (int cell = 0; cell < req.Facings; cell++)
                BlitCropped(cells[cell], geo.Width, geo.Height, xMin, yMin, cw, ch,
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

            result.SidecarPath = WriteSidecar(host, g, optsJs, req, result, probe);

            result.RenderMilliseconds = renderClock.Elapsed.TotalMilliseconds;
            total.Stop();
            result.TotalMilliseconds = total.Elapsed.TotalMilliseconds;
            return result;
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
        /// Widest grid that keeps both sheet dimensions under the texture cap, preferring FEWER rows so
        /// the sheet stays wide and short (which is how every other turntable sheet in the repo reads).
        /// Throws if even one column per row is too big — silently emitting an over-cap sheet is the
        /// failure this whole baker exists to avoid.
        /// </summary>
        public static void ChooseGrid(int cellW, int cellH, int cells, out int cols, out int rows)
        {
            int maxCols = Mathf.Max(1, MaxTextureSize / Mathf.Max(1, cellW));

            for (int c = Mathf.Min(cells, maxCols); c >= 1; c--)
            {
                int r = Mathf.CeilToInt(cells / (float)c);
                if (r * cellH <= MaxTextureSize)
                {
                    cols = c; rows = r;
                    return;
                }
            }

            throw new InvalidOperationException(
                $"A {cellW}×{cellH} cell cannot hold {cells} facings under the {MaxTextureSize} px cap " +
                "in any grid. The crop did not shrink this build enough — check the preset actually " +
                "resolved, since an uncropped cell means the silhouette filled the frame.");
        }

        static void BlitCropped(byte[] src, int srcW, int srcH, int cropX, int cropY, int cw, int ch,
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

        /// <summary>
        /// Assert the preset actually CHANGED the render.
        ///
        /// <para>This exists for one specific, already-made mistake: <c>render(dir,{preset:'netShed'})</c>
        /// looks exactly like the right call and is silently wrong, because <c>resolve()</c> has no
        /// <c>preset</c> key and quietly falls through to the default build. Nothing throws; you just get
        /// seven identical sheds under seven different names, and the only way to notice is to look at
        /// all seven side by side.</para>
        ///
        /// <para>The check is a byte comparison of one facing against the rig's default. It is not a
        /// proof that every field applied — a preset differing from the default in some field this
        /// facing does not show would still pass — but it catches the whole-preset-ignored case, which
        /// is the one that has teeth. All twelve shipped presets differ from the default in size and
        /// body colour, so a pass here is meaningful for every build we actually bake.</para>
        /// </summary>
        static void AssertPresetApplies(IRigScriptHost host, string g, string optsJs, string preset,
                                        int width, int height)
        {
            if (host.EvaluateNumber($"Object.keys({g}.PRESETS['{Escape(preset)}']).length") <= 0)
                throw new InvalidOperationException(
                    $"Preset '{preset}' of {g} is an EMPTY options object — it cannot describe a build.");

            byte[] withPreset = host.EvaluateBytes($"{g}.render(0,{optsJs})");
            byte[] plain = host.EvaluateBytes($"{g}.render(0,{{}})");

            if (withPreset.Length == plain.Length && BytesEqual(withPreset, plain))
                throw new InvalidOperationException(
                    $"Preset '{preset}' rendered byte-identical to the rig's DEFAULT build.\n\n" +
                    "That almost certainly means the options are not reaching resolve() — the classic " +
                    "form of this bug is passing {preset:'name'}, which no building rig reads, instead " +
                    "of spreading PRESETS['name'] into the options. Refusing rather than baking seven " +
                    "identical sheds under seven different names.");

            // Guard the geometry once, here, so the probe and the crop can both assume it.
            if (withPreset.Length != width * height * 4)
                throw new InvalidOperationException(
                    $"Preset '{preset}' rendered {withPreset.Length} bytes, expected " +
                    $"{width * height * 4} for {width}×{height} RGBA.");
        }

        /// <summary>
        /// The geometry guard a hand-dialled build still needs: the preset tripwire is skipped for it,
        /// but the crop and the probe both assume the render came back at the rig's declared cell size.
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
