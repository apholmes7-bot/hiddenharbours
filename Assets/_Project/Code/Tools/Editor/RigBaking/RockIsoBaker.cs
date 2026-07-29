using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using HiddenHarbours.Art.Editor;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace HiddenHarbours.Tools.RigBaking
{
    /// <summary>One baked rock sheet: a species at one stone × tide.</summary>
    public sealed class RockSheetBake
    {
        public string Stem;
        public string AssetPath;
        public string Species, Stone, Tide;
        public int Width, Height, Cols, Rows;

        public override string ToString() =>
            $"{AssetPath}  {Width}×{Height}  ({Cols} variant col(s) × {Rows} dress row(s))";
    }

    public sealed class RockBakeResult
    {
        public string EngineName;
        public readonly List<RockSheetBake> Sheets = new List<RockSheetBake>();
        public int CellsRendered;
        public double RenderMilliseconds;
        public double TotalMilliseconds;
        public long TotalPngBytes;
    }

    /// <summary>
    /// Bakes the Rock Iso kit from <c>docs/art/rigs/rockIsoRig.js</c> — shore boulders, outcrops,
    /// tide-pool ledges, awash skerries, cloven landmarks and cobble scatter.
    ///
    /// <para><b>⭐ THIS KIT BAKES TO ORDER, AND THAT IS WHY THE BAKER EXISTS.</b> Six species × four
    /// stones × three tides is 72 sheets, so the drop ships the RIG and the sidecar and no pixels at
    /// all. Which pairs actually ship is an atlas-budget decision for the owner —
    /// <c>sandstone</c> is the shore-matching stone (it is ShoreIso2's <c>redrock</c> ramp verbatim,
    /// so a rock composites onto a cliff toe with no seam) and the other three are regional. Bake
    /// what the game uses, when it uses it.</para>
    ///
    /// <para>Sheet layout is the kit's own: <b>variant COLS × dress ROWS</b>, cell =
    /// <c>SPECIES[k].cell</c>, one sheet per species × stone × tide named
    /// <c>&lt;Species&gt;_&lt;stone&gt;_&lt;tide&gt;.png</c>. Every cell is pivot-aligned on the
    /// bottom-centre ground contact, which is data in <c>RockIso.json</c> and never derived here.</para>
    ///
    /// <para><b>⭐ THIS RIG NEEDS NO SHIM AND NO CATALOG ENTRY.</b> <c>render</c> and the constants
    /// are all on <c>globalThis.RockIso</c>, so its source runs UNMODIFIED (ADR 0021 §5) — no
    /// in-memory string widening to reach closure-private symbols. It is deliberately absent from
    /// <see cref="RigCatalog"/> too: that struct is built around <see cref="AzimuthConvention"/> for
    /// 8-direction turntables, and <b>a rock has no heading</b> — its sheet axes are variant × dress.
    /// Nothing here calls <c>RigBaker.DirForCell</c>, so there is no azimuth to probe or refuse on.</para>
    ///
    /// <para><b>⚠ THE COMMITTED CONTRACT IS THE ORACLE, AND THIS BAKER REFUSES RATHER THAN
    /// REWRITES.</b> The art director's <c>_rockBake.js</c> re-emits <c>RockIso.json</c> on every
    /// run; this one does not. The sidecar that shipped with the drop describes all twenty variants
    /// and is stone- and tide-independent, so re-deriving it from a partial bake could only narrow
    /// it — and a C#-side re-serialisation would churn the art director's file for no gain. Instead
    /// <see cref="AssertMatchesContract"/> compares the rig's LIVE geometry against the committed
    /// contract before a single pixel is written, and throws on any disagreement. A rig change that
    /// moves a cell then surfaces as a loud refusal pointing at the art director, not as sheets that
    /// quietly no longer match their own anchors.</para>
    ///
    /// <para>No water is baked (ADR 0010/0012/0023). An <c>awash</c> sheet is CLIPPED at the sea
    /// plane with a wet-glint band at the cut, and a <c>PoolLedge</c> bakes an empty damp basin plus
    /// a rect for the shader to fill. Like every sibling baker this stops at "PNGs on disk" —
    /// slicing and import settings belong to the slicer, and <see cref="RockBakeMenu"/> chains them.</para>
    /// </summary>
    public static class RockIsoBaker
    {
        /// <summary>
        /// The shore-matching stone, and the only one this kit bakes unless told otherwise. It is
        /// ShoreIso2's <c>redrock</c> ramp verbatim, so a rock seats onto a cliff toe with no seam.
        /// Baking all four stones is a 4× atlas decision, not a loop bound.
        /// </summary>
        public const string DefaultStone = "sandstone";

        public static string DefaultOutputFolder => RockIsoCatalog.RockRoot.TrimEnd('/');

        // =====================================================================================
        // the bake
        // =====================================================================================

        /// <summary>
        /// Bakes every species the rig declares, across the requested stones × tides. Null lists
        /// mean "the kit's own axes" for tides and species, and <see cref="DefaultStone"/> for
        /// stone — never all four stones by accident.
        /// </summary>
        public static RockBakeResult Bake(IReadOnlyList<string> stones = null,
                                          IReadOnlyList<string> tides = null,
                                          IReadOnlyList<string> species = null,
                                          string outputFolder = null,
                                          Action<string, float> progress = null)
        {
            outputFolder ??= DefaultOutputFolder;
            stones ??= new[] { DefaultStone };
            var total = Stopwatch.StartNew();

            using IRigScriptHost host = RigScriptHostFactory.Create();
            InstallRig(host);

            var result = new RockBakeResult { EngineName = host.EngineName };

            species ??= ReadSpeciesKeys(host);
            tides ??= ReadStringArray(host, $"{RockIsoCatalog.RigGlobalName}.TIDES");

            foreach (string s in stones) AssertAxis(host, "STONES", s, isObjectMap: true);
            foreach (string t in tides) AssertAxis(host, "TIDES", t, isObjectMap: false);

            // Validate the WHOLE recipe against the rig AND the committed contract before writing
            // anything: a species whose cell drifted must fail with zero files on disk, not half a
            // set of sheets that no longer match their own anchors.
            var contract = RockIsoCatalog.Load();
            if (contract == null)
                throw new InvalidOperationException(
                    $"[rock-baker] {RockIsoCatalog.ContractPath} is missing or unreadable — it is " +
                    "the oracle this bake is checked against, so there is nothing to bake against.");

            var specs = new Dictionary<string, SpeciesSpec>(StringComparer.Ordinal);
            foreach (string key in species)
            {
                var spec = ReadSpeciesSpec(host, key);
                AssertFits(key, spec);
                AssertMatchesContract(key, spec, contract);
                specs[key] = spec;
            }

            var renderClock = new Stopwatch();
            Directory.CreateDirectory(Path.Combine(RigCatalog.RepoRoot, outputFolder));

            int done = 0, plan = stones.Count * tides.Count * species.Count;

            foreach (string stone in stones)
            foreach (string tide in tides)
            foreach (string key in species)
            {
                SpeciesSpec spec = specs[key];
                progress?.Invoke(RockIsoCatalog.StemFor(key, stone, tide), (float)done++ / plan);

                // variant COLS × dress ROWS — the kit's layout, and the one RockIso.json's
                // `col`/`dressRow` are numbered against.
                var cells = new byte[spec.Rows][][];
                for (int d = 0; d < spec.Rows; d++)
                {
                    cells[d] = new byte[spec.Cols][];
                    for (int v = 0; v < spec.Cols; v++)
                        cells[d][v] = RenderCell(host, key, v, d, stone, tide, spec,
                                                 renderClock, result);
                }

                result.Sheets.Add(WriteSheet(outputFolder, key, stone, tide, spec, cells, result));
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
        /// Loads <c>rockIsoRig.js</c> into a host and asserts the global installed. Deliberately not
        /// <c>RigCatalog.Install</c>: that reads <c>W/H/pivot</c> globals a rock kit does not have
        /// (its cell is per species) and would throw on the missing <c>pivot</c>. The source is read
        /// and handed over UNMODIFIED.
        /// </summary>
        public static void InstallRig(IRigScriptHost host)
        {
            string full = Path.Combine(RigCatalog.RepoRoot, RockIsoCatalog.RigScriptPath);
            if (!File.Exists(full))
                throw new FileNotFoundException(
                    $"Rock rig source missing at {full}. It is committed under docs/art/rigs/ — " +
                    "if this fired, the branch predates that import.", full);

            host.Execute(File.ReadAllText(full));

            string g = RockIsoCatalog.RigGlobalName;
            if (!host.EvaluateBool($"typeof {g} === 'object' && {g} !== null"))
                throw new InvalidOperationException(
                    $"'{RockIsoCatalog.RigScriptPath}' ran but did not install globalThis.{g} — " +
                    "the rig changed shape.");

            // Asserted up front rather than discovered mid-bake, where a renamed export would
            // surface as a confusing type error on sheet 7.
            if (!host.EvaluateBool($"typeof {g}.render === 'function'"))
                throw new InvalidOperationException(
                    $"{g}.render() is missing — this baker calls the rig's public API directly " +
                    "(no shim), so a renamed export must fail loudly here.");

            foreach (string table in new[] { "SPECIES", "STONES", "RAMPS" })
                if (!host.EvaluateBool($"typeof {g}.{table} === 'object' && {g}.{table} !== null"))
                    throw new InvalidOperationException($"{g}.{table} is missing — the rig changed shape.");

            foreach (string arr in new[] { "TIDES", "DRESS" })
                if (!host.EvaluateBool($"Array.isArray({g}.{arr})"))
                    throw new InvalidOperationException($"{g}.{arr} is missing — the rig changed shape.");
        }

        /// <summary>The species the rig declares, in its own order — never restated here.</summary>
        public static IReadOnlyList<string> ReadSpeciesKeys(IRigScriptHost host) =>
            ReadStringArray(host, $"Object.keys({RockIsoCatalog.RigGlobalName}.SPECIES)");

        /// <summary>Reads a JS string array through the host's JSON round-trip. Materialised to an
        /// array so callers can index and compare it directly.</summary>
        public static string[] ReadStringArray(IRigScriptHost host, string expression) =>
            FishingKitBaker.ReadStringArray(host, expression).ToArray();

        static void AssertAxis(IRigScriptHost host, string table, string key, bool isObjectMap)
        {
            string g = RockIsoCatalog.RigGlobalName;
            bool known = isObjectMap
                ? host.EvaluateBool($"Object.prototype.hasOwnProperty.call({g}.{table}, {Js(key)})")
                : host.EvaluateBool($"{g}.{table}.indexOf({Js(key)}) >= 0");
            if (!known)
            {
                string[] all = isObjectMap
                    ? ReadStringArray(host, $"Object.keys({g}.{table})")
                    : ReadStringArray(host, $"{g}.{table}");
                throw new ArgumentException(
                    $"RockIso declares no {table} entry '{key}'. Known: {string.Join(", ", all)}.");
            }
        }

        /// <summary>The rig's own cell and sheet axes for one species.</summary>
        public readonly struct SpeciesSpec
        {
            public readonly int CellW, CellH, Cols, Rows;

            public SpeciesSpec(int cellW, int cellH, int cols, int rows)
            {
                CellW = cellW; CellH = cellH; Cols = cols; Rows = rows;
            }

            public int SheetW => Cols * CellW;
            public int SheetH => Rows * CellH;
        }

        public static SpeciesSpec ReadSpeciesSpec(IRigScriptHost host, string species)
        {
            string g = RockIsoCatalog.RigGlobalName;
            string sp = $"{g}.SPECIES[{Js(species)}]";

            if (!host.EvaluateBool($"{sp} && typeof {sp} === 'object'"))
                throw new ArgumentException(
                    $"RockIso declares no species '{species}'. Known: " +
                    string.Join(", ", ReadSpeciesKeys(host)) + ".");

            return new SpeciesSpec(
                cellW: (int)host.EvaluateNumber($"{sp}.cell.w"),
                cellH: (int)host.EvaluateNumber($"{sp}.cell.h"),
                cols: (int)host.EvaluateNumber($"{sp}.variants.length"),
                rows: (int)host.EvaluateNumber($"{g}.DRESS.length"));
        }

        /// <summary>
        /// The 2048 guard. Over the cap Unity imports SILENTLY DOWNSCALED with a matching sprite
        /// count, so only a cell-size or pivot assert much later would catch it. The kit's own
        /// harness makes the same check — this is not a place to differ from it.
        /// </summary>
        static void AssertFits(string species, in SpeciesSpec spec)
        {
            if (spec.SheetW > RockIsoCatalog.ImportSizeCap || spec.SheetH > RockIsoCatalog.ImportSizeCap)
                throw new InvalidOperationException(
                    $"{species} would bake to {spec.SheetW}×{spec.SheetH}, over the " +
                    $"{RockIsoCatalog.ImportSizeCap} px import cap. Unity would import it DOWNSCALED " +
                    "with the sprite COUNT still matching, so this would only surface as a cell-size " +
                    "failure much later. Page the sheet before raising the limit.");
        }

        /// <summary>
        /// The rig's live geometry vs the committed <c>RockIso.json</c>. Every anchor in that file —
        /// pivot, footprint, perch, snags, pool — is expressed in CELL PIXELS, so a changed cell
        /// silently invalidates all of them at once. Refusing here is the difference between a loud
        /// stop and a set of sheets whose anchors point at the wrong pixels.
        /// </summary>
        public static void AssertMatchesContract(string species, in SpeciesSpec spec,
                                                 RockIsoCatalog.Contract contract)
        {
            var entry = contract.Find(species);
            if (entry == null)
                throw new InvalidOperationException(
                    $"[rock-baker] the rig declares species '{species}' but " +
                    $"{RockIsoCatalog.ContractFileName} does not. Re-export the sidecar from the " +
                    "rig — do not hand-edit it.");

            if (spec.CellW != entry.CellW || spec.CellH != entry.CellH)
                throw new InvalidOperationException(
                    $"[rock-baker] {species}: the rig renders a {spec.CellW}×{spec.CellH} cell but " +
                    $"{RockIsoCatalog.ContractFileName} says {entry.CellW}×{entry.CellH}. Every " +
                    "anchor in that file is in cell pixels, so all of them are now wrong. Re-export " +
                    "the sidecar from the rig rather than baking against a stale one.");

            if (spec.Cols != entry.SheetCols || spec.Rows != entry.SheetRows)
                throw new InvalidOperationException(
                    $"[rock-baker] {species}: the rig has {spec.Cols} variant(s) × {spec.Rows} dress " +
                    $"row(s) but the contract says {entry.SheetCols} × {entry.SheetRows}. The " +
                    "variant columns RockIso.json numbers its anchors against have moved.");
        }

        // =====================================================================================
        // the pixels
        // =====================================================================================

        /// <summary>One <c>render()</c> call, as a JS expression.</summary>
        public static string ResultExpr(string species, int variant, int dress, string stone,
                                        string tide) =>
            $"{RockIsoCatalog.RigGlobalName}.render({Js(species)},{{variant:{variant}," +
            $"dress:{dress},stone:{Js(stone)}}},{Js(tide)})";

        static byte[] RenderCell(IRigScriptHost host, string species, int variant, int dress,
                                 string stone, string tide, in SpeciesSpec spec,
                                 Stopwatch renderClock, RockBakeResult result)
        {
            string expr = ResultExpr(species, variant, dress, stone, tide) + ".rgba";
            renderClock.Start();
            byte[] rgba = host.EvaluateBytes(expr);
            renderClock.Stop();
            result.CellsRendered++;

            int expected = spec.CellW * spec.CellH * 4;
            if (rgba.Length != expected)
                throw new InvalidOperationException(
                    $"`{expr}` came back {rgba.Length} bytes, expected {expected} for " +
                    $"{spec.CellW}×{spec.CellH} RGBA. The cell the rig rendered does not match the " +
                    "cell SPECIES declared — do not blit it into a grid.");
            return rgba;
        }

        /// <summary>
        /// Lays the cells out variant-across × dress-down and writes the PNG.
        /// <c>RigBaker.Blit</c> does the cell copy, so the row/col convention (row-major from the
        /// TOP-LEFT, matching every slicer in the repo) exists in exactly one place.
        /// </summary>
        static RockSheetBake WriteSheet(string outputFolder, string species, string stone,
                                        string tide, in SpeciesSpec spec, byte[][][] cells,
                                        RockBakeResult result)
        {
            int pw = spec.SheetW, ph = spec.SheetH;
            var pixels = new Color32[pw * ph];
            for (int d = 0; d < spec.Rows; d++)
                for (int v = 0; v < spec.Cols; v++)
                    RigBaker.Blit(cells[d][v], spec.CellW, spec.CellH, pixels, pw, ph,
                                  col: v, rowFromTop: d);

            string stem = RockIsoCatalog.StemFor(species, stone, tide);
            string assetPath = $"{outputFolder}/{stem}.png";

            var tex = new Texture2D(pw, ph, TextureFormat.RGBA32, mipChain: false, linear: false);
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

            return new RockSheetBake
            {
                Stem = stem, AssetPath = assetPath,
                Species = species, Stone = stone, Tide = tide,
                Width = pw, Height = ph, Cols = spec.Cols, Rows = spec.Rows,
            };
        }

        static string Js(string s) => "'" + s.Replace("\\", "\\\\").Replace("'", "\\'") + "'";
    }
}
