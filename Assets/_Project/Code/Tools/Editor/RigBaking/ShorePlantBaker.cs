using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using HiddenHarbours.Art.Editor;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace HiddenHarbours.Tools.RigBaking
{
    /// <summary>One baked plant sheet: a species at one season × stage, on one axis.</summary>
    public sealed class PlantSheetBake
    {
        public string Stem;
        public string Species, Season, Stage, Axis;
        /// <summary>The dimension held FIXED on this sheet — a variant index on a tide-axis sheet,
        /// a tide key on a variant-axis sheet.</summary>
        public string Fixed;
        public int Width, Height, Cols, Rows;
        public readonly List<string> AssetPaths = new List<string>();

        public override string ToString() =>
            $"{Stem}  {Width}×{Height}  ({Cols} {Axis} col(s) × {Rows} sway row(s))  " +
            $"[{AssetPaths.Count} channel(s)]";
    }

    public sealed class PlantBakeResult
    {
        public string EngineName;
        public readonly List<PlantSheetBake> Sheets = new List<PlantSheetBake>();
        public int CellsRendered;
        public double RenderMilliseconds;
        public double TotalMilliseconds;
        public long TotalPngBytes;
    }

    /// <summary>
    /// Bakes the Shoreline Plant kit from <c>docs/art/rigs/shorePlantRig.js</c> — sixteen species
    /// across five tidal zones, from sugar kelp on the subtidal fringe to beach pea on the dune.
    ///
    /// <para><b>⭐ THIS KIT BAKES TO ORDER, AND THAT IS WHY THE BAKER EXISTS.</b> 16 species × two
    /// sheet axes × 3 seasons × 3 growth stages is a matrix nobody should pick from by accident, so
    /// the drop ships the RIG and the contract and no pixels at all — the same call the Rock Iso kit
    /// makes. Which slice actually ships is an ATLAS BUDGET decision for the owner;
    /// <see cref="ShorePlantBakeMenu"/> makes the default one deliberate.</para>
    ///
    /// <para><b>Three PNGs per sheet, and two of them are DATA.</b> The albedo, then
    /// <c>&lt;stem&gt;_light.png</c> (R key · G rim · B depth · A coverage) and
    /// <c>&lt;stem&gt;_tide.png</c> (R wet · G submerged · <b>B strap flag</b> · A coverage) — the
    /// contract's own filenames, not renamed here. All three come from ONE
    /// <c>render()</c> per cell: the result is parked on a JS global and the three buffers are read
    /// off it, because re-rendering a cell to fetch its mask would treble the most expensive thing
    /// this baker does.</para>
    ///
    /// <para><b>⭐ THIS RIG NEEDS NO SHIM AND NO CATALOG ENTRY.</b> <c>render</c>, <c>packMask</c>,
    /// <c>packState</c>, <c>sheetSpec</c> and the tables are all on <c>globalThis.ShorePlants</c>, so
    /// its source runs UNMODIFIED (ADR 0021 §5). It is deliberately absent from
    /// <see cref="RigCatalog"/>: that struct is built around <see cref="AzimuthConvention"/> for
    /// 8-direction turntables, and <b>a plant has no heading</b> — its sheet axes are tide × sway and
    /// variant × sway. Nothing here calls <c>RigBaker.DirForCell</c>.</para>
    ///
    /// <para><b>⚠ THE COMMITTED CONTRACT IS THE ORACLE, AND THIS BAKER REFUSES RATHER THAN
    /// REWRITES.</b> <see cref="AssertMatchesContract"/> compares the rig's LIVE cell, pivot and
    /// sheet dimensions against the committed contract before a single pixel is written. The cell is
    /// UNIONED over the tide states, so a drifted cell moves the ground-contact pivot of all five
    /// baked states of that species at once — and no sheet exists yet to disagree with it. A rig
    /// change surfaces here as a loud refusal pointing at the art director, never as sheets whose
    /// pivots quietly no longer match their own contract.</para>
    ///
    /// <para>No water and no moving light are baked (ADR 0010/0012/0023). Submergence is COLOUR
    /// ONLY — no dapple, no spatial pattern, nothing per-frame — because the live sprite-light
    /// shader's caustics are swell-driven and two uncorrelated patterns on one frond is worse than
    /// none. Like every sibling baker this stops at "PNGs on disk"; slicing and import settings
    /// belong to <see cref="ShorePlantSheetSlicer"/>, which <see cref="ShorePlantBakeMenu"/>
    /// chains.</para>
    /// </summary>
    public static class ShorePlantBaker
    {
        /// <summary>The tide-axis sheet is the gameplay sheet: the water moves continuously and every
        /// plant follows it, so a placed plant samples this axis. The variant axis is dressing.</summary>
        public const string TideAxis = "tide";

        public const string VariantAxis = "variant";

        public const string DefaultSeason = "summer";
        public const string DefaultStage = "full";
        public const int DefaultVariant = 0;

        public static string DefaultOutputFolder => ShorePlantCatalog.PlantsRoot.TrimEnd('/');

        /// <summary>The JS global a render is parked on so its three channels can be read without
        /// rendering three times. Underscored to stay clear of the rig's own namespace.</summary>
        const string ResultGlobal = "__hhPlantRender";

        // =====================================================================================
        // the bake
        // =====================================================================================

        /// <summary>
        /// Bakes the TIDE-axis sheet (5 tide states × 4 sway frames) for each requested species at
        /// one season × stage × variant. Null species means the kit's own sixteen.
        /// </summary>
        public static PlantBakeResult BakeTideAxis(IReadOnlyList<string> species = null,
                                                   string season = DefaultSeason,
                                                   string stage = DefaultStage,
                                                   int variant = DefaultVariant,
                                                   string outputFolder = null,
                                                   Action<string, float> progress = null)
            => Bake(TideAxis, species, season, stage, variant, null, outputFolder, progress);

        /// <summary>
        /// Bakes the VARIANT-axis sheet (4 variants × 4 sway frames) for each requested species at
        /// one season × stage × tide state.
        /// </summary>
        public static PlantBakeResult BakeVariantAxis(IReadOnlyList<string> species = null,
                                                      string season = DefaultSeason,
                                                      string stage = DefaultStage,
                                                      string tide = "half",
                                                      string outputFolder = null,
                                                      Action<string, float> progress = null)
            => Bake(VariantAxis, species, season, stage, 0, tide, outputFolder, progress);

        static PlantBakeResult Bake(string axis, IReadOnlyList<string> species, string season,
                                    string stage, int variant, string tide, string outputFolder,
                                    Action<string, float> progress)
        {
            outputFolder ??= DefaultOutputFolder;
            var total = Stopwatch.StartNew();

            using IRigScriptHost host = RigScriptHostFactory.Create();
            InstallRig(host);

            var result = new PlantBakeResult { EngineName = host.EngineName };
            species ??= ReadSpeciesKeys(host);

            var contract = ShorePlantCatalog.Load();
            if (contract == null)
                throw new InvalidOperationException(
                    $"[plant-baker] {ShorePlantCatalog.ContractPath} is missing or unreadable — it " +
                    "is the oracle this bake is checked against, so there is nothing to bake " +
                    "against.");

            // Validate the WHOLE recipe against the rig AND the contract before writing anything: a
            // species whose union cell drifted must fail with zero files on disk, not half a set of
            // sheets whose pivots no longer match the contract the engine reads.
            var specs = new Dictionary<string, SheetSpec>(StringComparer.Ordinal);
            foreach (string key in species)
            {
                var spec = ReadSheetSpec(host, key, stage, axis);
                AssertFits(key, spec);
                AssertMatchesContract(key, spec, axis, contract);
                specs[key] = spec;
            }

            var renderClock = new Stopwatch();
            Directory.CreateDirectory(Path.Combine(RigCatalog.RepoRoot, outputFolder));

            int done = 0;
            foreach (string key in species)
            {
                SheetSpec spec = specs[key];
                string stem = axis == TideAxis
                    ? ShorePlantCatalog.TideSheetStem(key, season, stage, variant)
                    : ShorePlantCatalog.VariantSheetStem(key, season, stage, tide);

                progress?.Invoke(stem, (float)done++ / species.Count);

                // ACROSS = the axis (tide state or variant), DOWN = sway frame, always. One
                // render() per cell; three channels read off it.
                var cells = new Dictionary<ShorePlantCatalog.Channel, byte[][][]>();
                foreach (var ch in ShorePlantCatalog.Channels)
                {
                    cells[ch] = new byte[spec.Rows][][];
                    for (int r = 0; r < spec.Rows; r++) cells[ch][r] = new byte[spec.Cols][];
                }

                for (int r = 0; r < spec.Rows; r++)
                for (int c = 0; c < spec.Cols; c++)
                {
                    int v = axis == TideAxis ? variant : c;
                    string tideKey = axis == TideAxis ? ShorePlantCatalog.TideKeys[c] : tide;
                    RenderCell(host, key, v, season, stage, tideKey, frame: r, spec,
                               renderClock, result, cells, row: r, col: c);
                }

                result.Sheets.Add(WriteSheet(outputFolder, stem, key, season, stage, axis,
                                             axis == TideAxis ? $"v{variant}" : tide,
                                             spec, cells, result));
            }

            result.RenderMilliseconds = renderClock.Elapsed.TotalMilliseconds;
            total.Stop();
            result.TotalMilliseconds = total.Elapsed.TotalMilliseconds;
            return result;
        }

        // =====================================================================================
        // the rig
        // =====================================================================================

        /// <summary>
        /// Loads <c>shorePlantRig.js</c> into a host and asserts the global installed. Deliberately
        /// not <c>RigCatalog.Install</c>: that reads <c>W/H/pivot</c> globals a plant kit does not
        /// have (its cell is per species AND per growth stage) and would throw on the missing
        /// <c>pivot</c>. The source is read and handed over UNMODIFIED.
        /// </summary>
        public static void InstallRig(IRigScriptHost host)
        {
            string full = Path.Combine(RigCatalog.RepoRoot, ShorePlantCatalog.RigScriptPath);
            if (!File.Exists(full))
                throw new FileNotFoundException(
                    $"Shore plant rig source missing at {full}. It is committed under " +
                    "docs/art/rigs/ — if this fired, the branch predates that import.", full);

            host.Execute(File.ReadAllText(full));

            string g = ShorePlantCatalog.RigGlobalName;
            if (!host.EvaluateBool($"typeof {g} === 'object' && {g} !== null"))
                throw new InvalidOperationException(
                    $"'{ShorePlantCatalog.RigScriptPath}' ran but did not install globalThis.{g} — " +
                    "the rig changed shape.");

            // Asserted up front rather than discovered mid-bake, where a renamed export would
            // surface as a confusing type error on sheet 11.
            foreach (string fn in new[] { "render", "packMask", "packState", "sheetSpec", "cellOf",
                                          "tideOf", "sheetAudit", "contract" })
                if (!host.EvaluateBool($"typeof {g}.{fn} === 'function'"))
                    throw new InvalidOperationException(
                        $"{g}.{fn}() is missing — this baker calls the rig's public API directly " +
                        "(no shim), so a renamed export must fail loudly here.");

            foreach (string arr in new[] { "SPECIES", "TIDES", "ZONES", "SEASONS", "STAGE_KEYS",
                                           "TIDE_KEYS" })
                if (!host.EvaluateBool($"Array.isArray({g}.{arr})"))
                    throw new InvalidOperationException($"{g}.{arr} is missing — the rig changed shape.");

            foreach (string obj in new[] { "STAGES", "M" })
                if (!host.EvaluateBool($"typeof {g}.{obj} === 'object' && {g}.{obj} !== null"))
                    throw new InvalidOperationException($"{g}.{obj} is missing — the rig changed shape.");
        }

        /// <summary>The species the rig declares, in its own order — never restated here.</summary>
        public static IReadOnlyList<string> ReadSpeciesKeys(IRigScriptHost host) =>
            ReadStringArray(host, $"{ShorePlantCatalog.RigGlobalName}.SPECIES.map(s => s.key)");

        /// <summary>Reads a JS string array through the host's JSON round-trip.</summary>
        public static string[] ReadStringArray(IRigScriptHost host, string expression) =>
            FishingKitBaker.ReadStringArray(host, expression).ToArray();

        /// <summary>The rig's own union cell, pivot and sheet axes for one species at one stage.</summary>
        public readonly struct SheetSpec
        {
            public readonly int CellW, CellH, Cols, Rows, PivotX, PivotY, Pad;

            public SheetSpec(int cellW, int cellH, int cols, int rows, int pivotX, int pivotY, int pad)
            {
                CellW = cellW; CellH = cellH; Cols = cols; Rows = rows;
                PivotX = pivotX; PivotY = pivotY; Pad = pad;
            }

            public int SheetW => Cols * CellW;
            public int SheetH => Rows * CellH;
        }

        /// <summary>
        /// One live <c>sheetSpec()</c> read. The rig publishes BOTH a <c>pivot</c> (top-left-origin
        /// ground contact) and a <c>pad</c> here; the contract publishes only the pivot, so that is
        /// what normalises (ADR 0026 — see <see cref="ShorePlantCatalog.NormalizedPivot"/>). The pad
        /// is carried so a test can pin <c>pad == cellH − 1 − pivotY</c> and prove the two quantities
        /// have not been confused for one another.
        /// </summary>
        public static SheetSpec ReadSheetSpec(IRigScriptHost host, string species, string stage,
                                              string axis)
        {
            string g = ShorePlantCatalog.RigGlobalName;

            if (!host.EvaluateBool($"!!{g}.byKey[{Js(species)}]"))
                throw new ArgumentException(
                    $"ShorePlants declares no species '{species}'. Known: " +
                    string.Join(", ", ReadSpeciesKeys(host)) + ".");

            string spec = SheetSpecExpr(species, stage, axis);
            return new SheetSpec(
                cellW: (int)host.EvaluateNumber($"{spec}.cell[0]"),
                cellH: (int)host.EvaluateNumber($"{spec}.cell[1]"),
                cols: (int)host.EvaluateNumber($"{spec}.cols"),
                rows: (int)host.EvaluateNumber($"{spec}.rows"),
                pivotX: (int)host.EvaluateNumber($"{spec}.pivot[0]"),
                pivotY: (int)host.EvaluateNumber($"{spec}.pivot[1]"),
                pad: (int)host.EvaluateNumber($"{spec}.pad"));
        }

        /// <summary>The rig's <c>sheetSpec()</c> call as a JS expression. The stage is resolved to
        /// the rig's OWN size scalar via its <c>STAGES</c> table, never a literal restated here.</summary>
        public static string SheetSpecExpr(string species, string stage, string axis)
        {
            string g = ShorePlantCatalog.RigGlobalName;
            string axisArg = axis == TideAxis ? ",'tide'" : "";
            return $"{g}.sheetSpec({Js(species)},{g}.STAGES[{Js(stage)}]{axisArg})";
        }

        /// <summary>
        /// 🔴 The 2048 guard. Over the cap Unity imports SILENTLY DOWNSCALED with the sprite count
        /// still matching, so only a cell-size or pivot assert much later would catch it. The union
        /// cell trades sheet area for pivot stability, which makes this the one number that could
        /// turn that trade into a silent bug — the kit's own <c>sheetAudit()</c> makes the same
        /// check, and this is not a place to differ from it.
        /// </summary>
        public static void AssertFits(string species, in SheetSpec spec)
        {
            if (spec.SheetW > ShorePlantCatalog.ImportSizeCap ||
                spec.SheetH > ShorePlantCatalog.ImportSizeCap)
                throw new InvalidOperationException(
                    $"{species} would bake to {spec.SheetW}×{spec.SheetH}, over the " +
                    $"{ShorePlantCatalog.ImportSizeCap} px import cap. Unity would import it " +
                    "DOWNSCALED with the sprite COUNT still matching, so this would only surface as " +
                    "a cell-size failure much later. This species is the one to flip to per-tide " +
                    "cells — an owner decision made on the audit numbers, not a limit to raise.");
        }

        /// <summary>
        /// The rig's live geometry vs the committed contract. The cell is UNIONED over 4 variants ×
        /// 2 seasons × 5 tides, so a drifted cell moves the ground-contact pivot of every baked state
        /// of that species at once — and with no sheets on disk yet, nothing else would notice.
        /// </summary>
        public static void AssertMatchesContract(string species, in SheetSpec spec, string axis,
                                                 ShorePlantCatalog.Contract contract)
        {
            var entry = contract.Find(species);
            if (entry == null)
                throw new InvalidOperationException(
                    $"[plant-baker] the rig declares species '{species}' but " +
                    $"{ShorePlantCatalog.ContractFileName} does not. Re-export the contract from " +
                    "the rig — do not hand-edit it.");

            if (spec.CellW != entry.CellW || spec.CellH != entry.CellH)
                throw new InvalidOperationException(
                    $"[plant-baker] {species}: the rig renders a {spec.CellW}×{spec.CellH} union " +
                    $"cell but {ShorePlantCatalog.ContractFileName} says {entry.CellW}×{entry.CellH}. " +
                    "The cell is unioned over every tide state, so all five baked states' pivots " +
                    "have moved together. Re-export the contract from the rig rather than baking " +
                    "against a stale one.");

            if (spec.PivotX != entry.Pivot.x || spec.PivotY != entry.Pivot.y)
                throw new InvalidOperationException(
                    $"[plant-baker] {species}: the rig's ground contact is " +
                    $"({spec.PivotX},{spec.PivotY}) but the contract says " +
                    $"({entry.Pivot.x},{entry.Pivot.y}). One pivot per species is what makes a " +
                    "runtime tide-state swap anchored by construction — a disagreement here is a " +
                    "visible hop the moment the water crosses a threshold.");

            int expectedW = axis == TideAxis ? entry.TideSheetW : entry.VariantSheetW;
            int expectedH = axis == TideAxis ? entry.TideSheetH : entry.VariantSheetH;
            if (spec.SheetW != expectedW || spec.SheetH != expectedH)
                throw new InvalidOperationException(
                    $"[plant-baker] {species}: the rig would bake a {spec.SheetW}×{spec.SheetH} " +
                    $"{axis}-axis sheet but the contract says {expectedW}×{expectedH}. The columns " +
                    "the slicer numbers its rects against have moved.");
        }

        // =====================================================================================
        // the pixels
        // =====================================================================================

        /// <summary>One <c>render()</c> call, as a JS expression. The stage resolves through the
        /// rig's own <c>STAGES</c> table and the tide through its own <c>TIDES</c>, so neither a
        /// size scalar nor a tide fraction is ever restated on the C# side.</summary>
        public static string RenderExpr(string species, int variant, string season, string stage,
                                        string tideKey, int frame)
        {
            string g = ShorePlantCatalog.RigGlobalName;
            return $"{g}.render({Js(species)},{{variant:{variant},season:{Js(season)}," +
                   $"size:{g}.STAGES[{Js(stage)}]," +
                   $"tide:{g}.TIDES[{g}.TIDE_KEYS.indexOf({Js(tideKey)})].t,frame:{frame}}})";
        }

        static void RenderCell(IRigScriptHost host, string species, int variant, string season,
                               string stage, string tideKey, int frame, in SheetSpec spec,
                               Stopwatch renderClock, PlantBakeResult result,
                               Dictionary<ShorePlantCatalog.Channel, byte[][][]> cells,
                               int row, int col)
        {
            // ⚠️ ONE render, three reads. Parking the result on a global is what stops the mask
            // channels costing two extra renders per cell — the rig recomputes every channel from
            // the geometry on each call, so re-rendering to fetch a mask trebles the bake.
            string expr = RenderExpr(species, variant, season, stage, tideKey, frame);
            renderClock.Start();
            host.Execute($"globalThis.{ResultGlobal} = {expr};");
            byte[] rgba = host.EvaluateBytes($"{ResultGlobal}.rgba");
            byte[] light = host.EvaluateBytes(
                $"{ShorePlantCatalog.RigGlobalName}.packMask({ResultGlobal})");
            byte[] state = host.EvaluateBytes(
                $"{ShorePlantCatalog.RigGlobalName}.packState({ResultGlobal})");
            renderClock.Stop();
            result.CellsRendered++;

            int expected = spec.CellW * spec.CellH * 4;
            foreach (var (buf, what) in new[] { (rgba, "rgba"), (light, "light mask"),
                                                (state, "state mask") })
                if (buf.Length != expected)
                    throw new InvalidOperationException(
                        $"`{expr}` {what} came back {buf.Length} bytes, expected {expected} for " +
                        $"{spec.CellW}×{spec.CellH} RGBA. The cell the rig rendered does not match " +
                        "the union cell it declared — do not blit it into a grid.");

            cells[ShorePlantCatalog.Channel.Albedo][row][col] = rgba;
            cells[ShorePlantCatalog.Channel.Light][row][col] = light;
            cells[ShorePlantCatalog.Channel.State][row][col] = state;
        }

        /// <summary>
        /// Lays the cells out axis-across × sway-down and writes one PNG per channel.
        /// <c>RigBaker.Blit</c> does the cell copy, so the row/col convention (row-major from the
        /// TOP-LEFT, matching every slicer in the repo) exists in exactly one place.
        /// </summary>
        static PlantSheetBake WriteSheet(string outputFolder, string stem, string species,
                                         string season, string stage, string axis, string fixedDim,
                                         in SheetSpec spec,
                                         Dictionary<ShorePlantCatalog.Channel, byte[][][]> cells,
                                         PlantBakeResult result)
        {
            var bake = new PlantSheetBake
            {
                Stem = stem, Species = species, Season = season, Stage = stage,
                Axis = axis, Fixed = fixedDim,
                Width = spec.SheetW, Height = spec.SheetH, Cols = spec.Cols, Rows = spec.Rows,
            };

            foreach (var channel in ShorePlantCatalog.Channels)
            {
                int pw = spec.SheetW, ph = spec.SheetH;
                var pixels = new Color32[pw * ph];
                for (int r = 0; r < spec.Rows; r++)
                    for (int c = 0; c < spec.Cols; c++)
                        RigBaker.Blit(cells[channel][r][c], spec.CellW, spec.CellH, pixels, pw, ph,
                                      col: c, rowFromTop: r);

                string assetPath = $"{outputFolder}/{stem}{ShorePlantCatalog.SuffixFor(channel)}.png";

                // linear: true on the DATA channels — they are numbers, and a Texture2D that thinks
                // they are colour would gamma them on the way out of EncodeToPNG.
                var tex = new Texture2D(pw, ph, TextureFormat.RGBA32, mipChain: false,
                                        linear: !ShorePlantCatalog.IsColourChannel(channel));
                try
                {
                    tex.SetPixels32(pixels);
                    tex.Apply(false, false);
                    byte[] png = tex.EncodeToPNG();
                    File.WriteAllBytes(Path.Combine(RigCatalog.RepoRoot, assetPath), png);
                    result.TotalPngBytes += png.Length;
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(tex);
                }

                bake.AssetPaths.Add(assetPath);
            }

            return bake;
        }

        static string Js(string s) => "'" + s.Replace("\\", "\\\\").Replace("'", "\\'") + "'";
    }
}
