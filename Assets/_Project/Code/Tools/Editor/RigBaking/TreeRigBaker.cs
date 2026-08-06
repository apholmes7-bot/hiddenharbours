using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using HiddenHarbours.Art.Editor;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace HiddenHarbours.Tools.RigBaking
{
    /// <summary>One baked tree sheet.</summary>
    public sealed class TreeSheetBake
    {
        public string Stem;
        public string AssetPath;
        public string Species;
        public string Stage;
        public string Season;
        public TreeKitCatalog.Channel Channel;
        public int Width, Height, Cols, Rows;

        public override string ToString() =>
            $"{AssetPath}  {Width}×{Height}  ({Cols} variant col(s) × {Rows} row(s))";
    }

    public sealed class TreeBakeResult
    {
        public string EngineName;
        public readonly List<TreeSheetBake> Sheets = new List<TreeSheetBake>();
        public string ContractPath;
        public TreeKitCatalog.Contract Contract;
        public int CellsRendered;
        public double RenderMilliseconds;
        public double TotalMilliseconds;
        public long TotalPngBytes;
    }

    /// <summary>
    /// Bakes the Acadian tree kit from <c>docs/art/rigs/treeIsoRig.js</c> — the first TREE art in
    /// this project that comes off a rig instead of a hand-drawn PNG, and the first prop family
    /// whose light masks are exact bake outputs rather than guesses from alpha.
    ///
    /// <para>Per species × stage × season it writes three co-registered sheets — albedo, mask,
    /// normal — of <c>VARIANTS</c> columns × <see cref="SwayRowsBaked"/> row(s), at the species'
    /// own measured cell, plus <c>Trees.json</c>: the placement contract
    /// (<see cref="TreeKitCatalog.Contract"/>) that carries cell, trunk-foot pivot, flare pad,
    /// per-species trunk anchor, true metres and the camera/rule constants the pixels were built
    /// under.</para>
    ///
    /// <para><b>⭐ THIS RIG NEEDS NO SHIM AND NO CATALOG ENTRY.</b> <c>render</c>, <c>packMask</c>,
    /// <c>normalView</c>, <c>sheetSpec</c> and the constants are all on <c>globalThis.TreeRig</c>,
    /// so its source runs UNMODIFIED (ADR 0021 §5) and is called directly — no in-memory string
    /// widening to reach closure-private symbols, no canvas mailbox like <c>catchKit</c>. It is
    /// deliberately absent from <see cref="RigCatalog"/> too: that struct is built around
    /// <see cref="AzimuthConvention"/> for 8-direction turntables, and <b>a tree has no
    /// heading</b> — its sheet axes are variant × sway. Nothing here calls
    /// <c>RigBaker.DirForCell</c>, and there is no azimuth to probe or refuse on.</para>
    ///
    /// <para>⚠️ <b>THE PIVOT IS THE TRUNK FOOT, NOT THE BOTTOM OF THE CELL.</b> The near-root flare
    /// projects BELOW the trunk foot under the 40° camera, so every cell carries <c>pad</c> rows
    /// underneath the pivot (Red Spruce mature, pass-2 rig: cell 126×159, pivot (63,148), 10 px of
    /// flare below — pass 1 measured 110×166, (54,145), 20 px, the pads halving because pass 2 draws
    /// three splayed buttresses where pass 1 drew a broad skirt).
    /// Every other tree sprite in this repo pivots bottom-centre, and <c>ArtImportPipeline</c> even
    /// defaults <c>/foliage/</c> to <c>BottomCenter</c> — which would sink all ten species into the
    /// ground by their own <c>pad</c> and read as an art bug rather than an import bug. The value is
    /// read from <c>sheetSpec().pivot</c> at bake time and never hardcoded.</para>
    ///
    /// <para>Like every sibling baker this stops at "PNGs + contract JSON on disk": slicing and
    /// import settings are <see cref="TreeSheetSlicer"/>'s job, and <see cref="TreeBakeMenu"/>
    /// chains the two.</para>
    /// </summary>
    public static class TreeRigBaker
    {
        /// <summary>
        /// <b>ONE sway row. The shader owns the swaying.</b> Measured 2026-07-25, and this is the
        /// reasoning rather than a shortcut:
        ///
        /// <list type="bullet">
        /// <item><c>HiddenHarboursTreeWind.shader</c> sways the canopy off <c>_WindWorld</c> — the
        /// same shared deterministic wind the grass and the water read, so one gust moves all three
        /// together. Baked frames would loop on a 5–7 fps timer and know nothing about the
        /// wind.</item>
        /// <item><b>Mask co-registration is free</b> under shader sway: the shader displaces vertex
        /// POSITIONS and passes <c>OUT.uv = IN.uv</c> through untouched, so albedo, mask and normal
        /// sampled with the same UV stay aligned by construction. Baked sway instead shears each
        /// channel separately and relies on them shearing identically.</item>
        /// <item>The shear introduces about 1.39° of tilt at full wind — negligible error for a
        /// baked normal map.</item>
        /// <item>Four rows is 4× the sheet area for motion we already have a better version of.</item>
        /// </list>
        ///
        /// <para>⚠️ This is precisely why the bake loop here is ours rather than the art director's
        /// <c>docs/art/rigs/_treeBake.js</c>: that harness loops <c>R.SWAY</c> unconditionally.</para>
        /// </summary>
        public const int SwayRowsBaked = 1;

        /// <summary>The one stage this wave bakes. The other three
        /// (<c>sapling/young/pole</c>) are a decision about hundreds of MB, not a loop
        /// bound — prove the path first.</summary>
        public const string DefaultStage = "mature";

        /// <summary>The one season this wave bakes. Autumn and winter are the same argument.</summary>
        public const string DefaultSeason = "summer";

        public static string DefaultOutputFolder => TreeKitCatalog.TreesRoot.TrimEnd('/');

        // =====================================================================================
        // the bake
        // =====================================================================================

        /// <summary>
        /// Bakes every species the rig declares at <paramref name="stage"/> × <paramref name="season"/>,
        /// all three channels, then writes the contract. Null lists mean "everything the rig
        /// declares" — species order comes from <c>TreeRig.SPECIES</c>, never restated here.
        /// </summary>
        public static TreeBakeResult Bake(IReadOnlyList<string> species = null,
                                          string stage = DefaultStage,
                                          string season = DefaultSeason,
                                          string outputFolder = null,
                                          Action<string, float> progress = null)
        {
            outputFolder ??= DefaultOutputFolder;
            var total = Stopwatch.StartNew();

            using IRigScriptHost host = RigScriptHostFactory.Create();
            InstallRig(host);

            var result = new TreeBakeResult { EngineName = host.EngineName };

            // 🔴 The default recipe is every species the rig declares MINUS the held-back ones. A
            // held-back species is one whose pass-2 output fails the RIG'S OWN rule audit, so baking it
            // would put art the rig itself rejects into the kit — see TreeKitCatalog.HeldBackSpecies for
            // the measurement and the ruling. An explicit `species` list is honoured as given, so a
            // deliberate one-off bake of a held-back species is still possible.
            if (species == null)
            {
                var all = ReadSpeciesKeys(host);
                var kept = new List<string>(all.Count);
                var held = new List<string>();
                foreach (string key in all)
                    (TreeKitCatalog.IsHeldBack(key) ? held : kept).Add(key);
                species = kept;

                if (held.Count > 0)
                    Debug.LogWarning(
                        $"[rig-baker] HOLDING BACK {held.Count} species: {string.Join(", ", held)}. " +
                        "Their committed sheets stay at the previous pass because the current rig's own " +
                        "rule audit rejects them — see TreeKitCatalog.HeldBackSpecies. Baking " +
                        $"{kept.Count} of {all.Count}.");
            }
            AssertStage(host, stage);
            AssertSeason(host, season);

            // Validate the WHOLE recipe before writing anything — a species that would import
            // downscaled must fail with zero files on disk, not half a set.
            var specs = new Dictionary<string, SheetSpec>(StringComparer.Ordinal);
            foreach (string key in species)
            {
                var spec = ReadSheetSpec(host, key, stage);
                AssertFits(key, stage, spec);
                specs[key] = spec;
            }

            var renderClock = new Stopwatch();
            Directory.CreateDirectory(Path.Combine(RigCatalog.RepoRoot, outputFolder));

            var entries = new List<TreeKitCatalog.Entry>();
            int done = 0, plan = species.Count;

            foreach (string key in species)
            {
                SheetSpec spec = specs[key];
                progress?.Invoke($"{key}_{stage}_{season}", (float)done++ / plan);

                // Render each variant ONCE and reuse the result for all three channels: the three
                // are different reads of one render, and re-rendering per channel would triple the
                // cost while opening the door to three subtly different geometries.
                var cells = new Dictionary<TreeKitCatalog.Channel, byte[][]>();
                foreach (var channel in TreeKitCatalog.Channels)
                    cells[channel] = new byte[spec.Cols][];

                for (int v = 0; v < spec.Cols; v++)
                {
                    string res = ResultExpr(key, stage, season, variant: v, frame: 0);
                    foreach (var channel in TreeKitCatalog.Channels)
                        cells[channel][v] = RenderChannel(host, res, channel, spec, renderClock, result);
                }

                foreach (var channel in TreeKitCatalog.Channels)
                    result.Sheets.Add(WriteSheet(outputFolder, key, stage, season, channel,
                                                 spec, cells[channel], result));

                entries.Add(BuildEntry(host, key, stage, season, spec));
            }

            result.Contract = BuildContract(host, entries.ToArray());
            result.ContractPath = WriteContract(outputFolder, result.Contract);

            result.RenderMilliseconds = renderClock.Elapsed.TotalMilliseconds;
            total.Stop();
            result.TotalMilliseconds = total.Elapsed.TotalMilliseconds;
            return result;
        }

        // =====================================================================================
        // the rig
        // =====================================================================================

        /// <summary>
        /// Loads <c>treeIsoRig.js</c> into a host and asserts the global installed. Deliberately
        /// not <c>RigCatalog.Install</c>: that reads <c>W/H/pivot</c> globals a tree does not have
        /// (its cell is per species) and would throw on the missing <c>pivot</c>. The source is
        /// read and handed over UNMODIFIED, exactly as <see cref="RigCatalog.ReadSource"/> does.
        /// </summary>
        public static void InstallRig(IRigScriptHost host)
        {
            string full = Path.Combine(RigCatalog.RepoRoot, TreeKitCatalog.RigScriptPath);
            if (!File.Exists(full))
                throw new FileNotFoundException(
                    $"Tree rig source missing at {full}. It is committed under docs/art/rigs/ — " +
                    "if this fired, the branch predates that import.", full);

            host.Execute(File.ReadAllText(full));

            string g = TreeKitCatalog.RigGlobalName;
            if (!host.EvaluateBool($"typeof {g} === 'object' && {g} !== null"))
                throw new InvalidOperationException(
                    $"'{TreeKitCatalog.RigScriptPath}' ran but did not install globalThis.{g} — " +
                    "the rig changed shape.");

            // The five entry points this baker calls. Asserted rather than discovered mid-bake,
            // because a missing one would otherwise surface as a confusing type error on sheet 7.
            foreach (string fn in new[] { "render", "packMask", "normalView", "sheetSpec", "cellOf" })
                if (!host.EvaluateBool($"typeof {g}.{fn} === 'function'"))
                    throw new InvalidOperationException(
                        $"{g}.{fn}() is missing — this baker calls the rig's public API directly " +
                        "(no shim), so a renamed export must fail loudly here.");
        }

        public static IReadOnlyList<string> ReadSpeciesKeys(IRigScriptHost host)
        {
            string g = TreeKitCatalog.RigGlobalName;
            return FishingKitBaker.ReadStringArray(host, $"{g}.SPECIES.map(function(s){{return s.key;}})");
        }

        static void AssertStage(IRigScriptHost host, string stage)
        {
            string g = TreeKitCatalog.RigGlobalName;
            if (!host.EvaluateBool($"typeof {g}.STAGES[{Js(stage)}] === 'number'"))
                throw new ArgumentException(
                    $"TreeRig declares no stage '{stage}'. Known: " +
                    string.Join(", ", FishingKitBaker.ReadStringArray(host, $"{g}.STAGE_KEYS")) + ".");
        }

        static void AssertSeason(IRigScriptHost host, string season)
        {
            string g = TreeKitCatalog.RigGlobalName;
            if (!host.EvaluateBool($"{g}.SEASONS.indexOf({Js(season)}) >= 0"))
                throw new ArgumentException(
                    $"TreeRig declares no season '{season}'. Known: " +
                    string.Join(", ", FishingKitBaker.ReadStringArray(host, $"{g}.SEASONS")) + ".");
        }

        /// <summary>The rig's own <c>sheetSpec()</c> for a species at a stage, plus the dimensions
        /// of the sheet WE lay out (<see cref="SwayRowsBaked"/> rows, not the rig's four).</summary>
        public readonly struct SheetSpec
        {
            public readonly int CellW, CellH, PivotX, PivotY, Pad, Cols, Rows;
            /// <summary>What the rig reports for the FULL 4-row sheet, and its 2048 verdict.</summary>
            public readonly int RigSheetW, RigSheetH;
            public readonly bool RigFits;
            public readonly float Metres;

            public SheetSpec(int cellW, int cellH, int pivotX, int pivotY, int pad, int cols,
                             int rows, int rigSheetW, int rigSheetH, bool rigFits, float metres)
            {
                CellW = cellW; CellH = cellH; PivotX = pivotX; PivotY = pivotY; Pad = pad;
                Cols = cols; Rows = rows;
                RigSheetW = rigSheetW; RigSheetH = rigSheetH; RigFits = rigFits; Metres = metres;
            }

            public int SheetW => Cols * CellW;
            public int SheetH => Rows * CellH;
            /// <summary>The trunk foot as a fraction of cell height — this species'
            /// <c>_TrunkAnchor</c>.</summary>
            public float TrunkAnchor => (float)Pad / CellH;
        }

        public static SheetSpec ReadSheetSpec(IRigScriptHost host, string species, string stage)
        {
            string g = TreeKitCatalog.RigGlobalName;
            string sp = $"{g}.sheetSpec({Js(species)}, {g}.STAGES[{Js(stage)}])";

            int cellH = (int)host.EvaluateNumber($"{sp}.cell[1]");
            int pivotY = (int)host.EvaluateNumber($"{sp}.pivot[1]");
            int pad = (int)host.EvaluateNumber($"{sp}.pad");

            // The rig defines pad = h − 1 − pivotY. Cross-check rather than trust: if the two ever
            // disagree, every pivot this bake writes is wrong and nothing downstream would notice.
            if (pad != cellH - 1 - pivotY)
                throw new InvalidOperationException(
                    $"{species}/{stage}: the rig reports pad {pad} but cellH−1−pivotY is " +
                    $"{cellH - 1 - pivotY}. The pivot convention changed — stop and read cellOf() " +
                    "before baking, because the trunk-foot pivot is derived from pad.");

            return new SheetSpec(
                cellW: (int)host.EvaluateNumber($"{sp}.cell[0]"),
                cellH: cellH,
                pivotX: (int)host.EvaluateNumber($"{sp}.pivot[0]"),
                pivotY: pivotY,
                pad: pad,
                cols: (int)host.EvaluateNumber($"{sp}.cols"),
                rows: SwayRowsBaked,
                rigSheetW: (int)host.EvaluateNumber($"{sp}.w"),
                rigSheetH: (int)host.EvaluateNumber($"{sp}.h"),
                rigFits: host.EvaluateBool($"{sp}.fits"),
                metres: (float)host.EvaluateNumber($"{sp}.metres"));
        }

        /// <summary>
        /// The 2048 guard, asserted TWICE and never inferred: once via the rig's own
        /// <c>sheetSpec().fits</c> (its verdict on the full 4-row sheet) and once on the sheet we
        /// actually lay out. Over the cap Unity imports SILENTLY DOWNSCALED with a matching sprite
        /// count, so only a pivot or cell-size assert much later would catch it.
        /// </summary>
        public static void AssertFits(string species, string stage, in SheetSpec spec)
        {
            if (!spec.RigFits)
                throw new InvalidOperationException(
                    $"{species}/{stage}: the rig's own sheetSpec().fits is FALSE — its full " +
                    $"{spec.RigSheetW}×{spec.RigSheetH} sheet is over Unity's " +
                    $"{TreeKitCatalog.ImportSizeCap} px cap. We bake {SwayRowsBaked} sway row, so " +
                    "this bake would still fit, but the rig is telling you the recipe outgrew the " +
                    "importer — decide that deliberately rather than past a refusal.");

            if (spec.SheetW > TreeKitCatalog.ImportSizeCap || spec.SheetH > TreeKitCatalog.ImportSizeCap)
                throw new InvalidOperationException(
                    $"{species}/{stage} would bake to {spec.SheetW}×{spec.SheetH}, over the " +
                    $"{TreeKitCatalog.ImportSizeCap} px default import cap. Unity would import it " +
                    "DOWNSCALED with the sprite COUNT still matching, so this would only surface " +
                    "as a cell-size failure much later. Page the sheet before raising the limit.");
        }

        // =====================================================================================
        // the pixels
        // =====================================================================================

        /// <summary>One <c>render()</c> call, as a JS expression. Frame 0 always — see
        /// <see cref="SwayRowsBaked"/>.</summary>
        public static string ResultExpr(string species, string stage, string season, int variant,
                                        int frame) =>
            $"{TreeKitCatalog.RigGlobalName}.render({Js(species)},{{variant:{variant}," +
            $"season:{Js(season)},frame:{frame},stage:{Js(stage)}}})";

        /// <summary>
        /// The RGBA bytes of one channel, from a <c>render()</c> result expression.
        ///
        /// <para>⚠️ <b>THE MASK'S CHANNEL ORDER IS THE RIG'S, NOT THE REFERENCE TECHNIQUE'S.</b>
        /// <c>TreeRig.packMask()</c> emits <b>R = key light · G = back rim · B = depth · A =
        /// coverage</b>. Every description of the sprite-light technique this serves says "green =
        /// front, blue = rim", so anyone porting a snippet will swap two channels and get something
        /// that looks subtly wrong rather than obviously broken. The order is pinned by
        /// <c>TreeRigBakeTests.MaskChannels_AreKeyRimDepthCoverage_NotTheReferenceTechniquesOrder</c>,
        /// with a measured sabotage: swapping R and G changes 4,907 px = 24.49% of a Red Spruce cell
        /// on the pass-2 rig (5,405 px = 29.60% on pass 1).</para>
        /// </summary>
        public static string ChannelExpr(string resultExpr, TreeKitCatalog.Channel channel)
        {
            string g = TreeKitCatalog.RigGlobalName;
            return channel switch
            {
                // R=key light, G=back rim, B=depth, A=coverage — the rig's own pack.
                TreeKitCatalog.Channel.Mask => $"{g}.packMask({resultExpr})",
                // View-space normals: R=x, G=y-up, B=toward camera.
                TreeKitCatalog.Channel.Normal => $"{g}.normalView({resultExpr})",
                // The composited sprite: binary alpha, no AA, keyline included.
                _ => $"{resultExpr}.rgba",
            };
        }

        static byte[] RenderChannel(IRigScriptHost host, string resultExpr,
                                    TreeKitCatalog.Channel channel, in SheetSpec spec,
                                    Stopwatch renderClock, TreeBakeResult result)
        {
            string expr = ChannelExpr(resultExpr, channel);
            renderClock.Start();
            byte[] rgba = host.EvaluateBytes(expr);
            renderClock.Stop();
            result.CellsRendered++;

            int expected = spec.CellW * spec.CellH * 4;
            if (rgba.Length != expected)
                throw new InvalidOperationException(
                    $"`{expr}` came back {rgba.Length} bytes, expected {expected} for " +
                    $"{spec.CellW}×{spec.CellH} RGBA. The cell the rig rendered does not match the " +
                    "cell sheetSpec() promised — do not blit it into a grid.");
            return rgba;
        }

        /// <summary>
        /// Lays the variant cells out left-to-right in one row and writes the PNG.
        /// <c>RigBaker.Blit</c> does the cell copy, so the row/col convention (row-major from the
        /// TOP-LEFT, matching every slicer in the repo) exists in exactly one place.
        /// </summary>
        static TreeSheetBake WriteSheet(string outputFolder, string species, string stage,
                                        string season, TreeKitCatalog.Channel channel,
                                        in SheetSpec spec, byte[][] cells, TreeBakeResult result)
        {
            int pw = spec.SheetW, ph = spec.SheetH;
            var pixels = new Color32[pw * ph];
            for (int v = 0; v < spec.Cols; v++)
                RigBaker.Blit(cells[v], spec.CellW, spec.CellH, pixels, pw, ph,
                              col: v, rowFromTop: 0);

            string stem = TreeKitCatalog.StemFor(species, stage, season, channel);
            string assetPath = $"{outputFolder}/{stem}.png";

            // linear: false only affects how Unity would interpret the texture if it were used as
            // a render target — the PNG we encode is the exact bytes the rig produced either way.
            // The sRGB/linear DECISION that matters lives on the importer (TreeSheetSlicer).
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

            return new TreeSheetBake
            {
                Stem = stem, AssetPath = assetPath, Species = species, Stage = stage,
                Season = season, Channel = channel,
                Width = pw, Height = ph, Cols = spec.Cols, Rows = spec.Rows,
            };
        }

        // =====================================================================================
        // the contract
        // =====================================================================================

        static TreeKitCatalog.Entry BuildEntry(IRigScriptHost host, string species, string stage,
                                               string season, in SheetSpec spec)
        {
            string g = TreeKitCatalog.RigGlobalName;
            string sp = $"{g}.byKey[{Js(species)}]";

            // The worst (highest thinPct) variant's report — the rule-1 audit number that matters,
            // recorded per species so a rig change that thins the foliage is visible in the diff.
            TreeKitCatalog.Audit worst = null;
            bool allPass = true;
            for (int v = 0; v < spec.Cols; v++)
            {
                string rep = ResultExpr(species, stage, season, v, frame: 0) + ".report";
                var audit = new TreeKitCatalog.Audit
                {
                    thinPct = (float)host.EvaluateNumber($"{rep}.thinPct"),
                    bodyRatio = (int)host.EvaluateNumber($"{rep}.bodyRatio"),
                    despeckled = (int)host.EvaluateNumber($"{rep}.despeckled"),
                    pass = host.EvaluateBool($"{rep}.pass"),
                    underFloor = host.EvaluateBool($"{rep}.underFloor"),
                };
                if (worst == null || audit.thinPct > worst.thinPct) worst = audit;
                allPass &= audit.pass;
            }
            // `pass` is an AND across the variants, tracked separately so a later, thinner variant
            // replacing `worst` cannot lose an earlier failure.
            worst.pass = allPass;

            Vector2 unityPivot = new Vector2((float)spec.PivotX / spec.CellW,
                                             (float)spec.Pad / spec.CellH);

            return new TreeKitCatalog.Entry
            {
                species = species,
                name = host.EvaluateString($"{sp}.name"),
                latin = host.EvaluateString($"{sp}.latin"),
                form = host.EvaluateString($"{sp}.form"),
                stage = stage,
                seasons = new[] { season },
                metres = spec.Metres,
                cellW = spec.CellW,
                cellH = spec.CellH,
                pivotX = spec.PivotX,
                pivotY = spec.PivotY,
                nearFlarePad = spec.Pad,
                trunkAnchor = spec.TrunkAnchor,
                unityPivotX = unityPivot.x,
                unityPivotY = unityPivot.y,
                sheetW = spec.SheetW,
                sheetH = spec.SheetH,
                rigSheetW = spec.RigSheetW,
                rigSheetH = spec.RigSheetH,
                rigFitsUnity2048 = spec.RigFits,
                audit = worst,
            };
        }

        static TreeKitCatalog.Contract BuildContract(IRigScriptHost host,
                                                     TreeKitCatalog.Entry[] entries)
        {
            string g = TreeKitCatalog.RigGlobalName;
            return new TreeKitCatalog.Contract
            {
                note = "Baked in-engine by TreeRigBaker from docs/art/rigs/treeIsoRig.js — " +
                       "REGENERATE rather than hand-edit (Hidden Harbours > Art > Bake Acadian " +
                       "Trees). pivotX/pivotY are the TRUNK FOOT in cell px from the top-left; " +
                       "nearFlarePad rows of near-root flare sit BELOW it, which is why a tree " +
                       "does not pivot bottom-centre. unityPivot is the same point normalised " +
                       "bottom-origin, and trunkAnchor is that height as a fraction of the cell — " +
                       "the wind shader's _TrunkAnchor for this species.",
                rig = TreeKitCatalog.RigScriptPath,
                global = g,
                ppu = (int)host.EvaluateNumber($"{g}.PPU"),
                camera = new TreeKitCatalog.CameraBlock
                {
                    name = "ADR-0006/0022",
                    view = "3/4 from S, orthographic",
                    elevDeg = (float)host.EvaluateNumber($"{g}.ELEV"),
                    heightScale = (float)host.EvaluateNumber($"{g}.CE"),
                    depthScale = (float)host.EvaluateNumber($"{g}.SE"),
                },
                light = new TreeKitCatalog.LightBlock
                {
                    key = ReadVec3(host, $"{g}.LIGHT.key"),
                    rim = ReadVec3(host, $"{g}.LIGHT.rim"),
                    note = "fixed upper-left key + back rim, screen space (art bible section 1)",
                },
                rules = new TreeKitCatalog.RulesBlock
                {
                    rimPx = (int)host.EvaluateNumber($"{g}.RIM_PX"),
                    minBodyPx = (int)host.EvaluateNumber($"{g}.MIN_BODY"),
                    minClumpRadiusPx = (int)host.EvaluateNumber($"{g}.MIN_R"),
                    note = "a 2 px rim must leave 6 px of interior; nothing is emitted under a " +
                           "5 px clump radius; the rim is gated on local mass thickness",
                },
                sheet = new TreeKitCatalog.SheetBlock
                {
                    cols = (int)host.EvaluateNumber($"{g}.VARIANTS"),
                    rows = SwayRowsBaked,
                    colAxis = "variant",
                    rowAxis = "sway frame",
                    rigSwayRows = (int)host.EvaluateNumber($"{g}.SWAY"),
                    swayNote = "ONE sway row is baked, on purpose: HiddenHarboursTreeWind.shader " +
                               "sways the canopy off the shared deterministic wind (_WindWorld) " +
                               "that the grass and water also read, so one gust moves all three " +
                               "together — and because the shader passes uv through untouched, " +
                               "albedo/mask/normal stay co-registered by construction. Baked " +
                               "frames would loop on a timer that knows nothing about the wind, " +
                               "at 4x the sheet area.",
                },
                channels = new TreeKitCatalog.ChannelBlock
                {
                    albedo = "RGBA, binary alpha, no AA, NO keyline (ADR 0031)",
                    mask = "R=key light, G=back rim, B=depth, A=coverage",
                    normal = "view-space normal, R=x, G=y-up, B=toward camera",
                    coverageNote = "all THREE sheets cover exactly the same pixels — the rig's " +
                                   "geometry. That is new: ADR 0031 wave 2 retired the 1 px " +
                                   "keyline ring, which used to be composited into rgba (and so " +
                                   "into the mask's A) while carrying no surface normal, leaving " +
                                   "the NORMAL 11% smaller. Measured on Red Spruce mature: " +
                                   "albedo/mask 6570 px against normal 5848 px, a difference of " +
                                   "722 px = exactly the ring; retiring it took albedo/mask to " +
                                   "5848 (pass 1 was 7601 / 6990 / 611 px = 8%). " +
                                   "Sample all three through the ALBEDO's mesh and uv anyway, and " +
                                   "keep the no-normal fallback: the sheets import Tight, so the " +
                                   "meshes can still differ, and the lighting include is shared " +
                                   "with rigs and older sheets that do carry a ring.",
                },
                trees = entries,
            };
        }

        static float[] ReadVec3(IRigScriptHost host, string arrayExpr) => new[]
        {
            (float)host.EvaluateNumber($"{arrayExpr}[0]"),
            (float)host.EvaluateNumber($"{arrayExpr}[1]"),
            (float)host.EvaluateNumber($"{arrayExpr}[2]"),
        };

        static string WriteContract(string outputFolder, TreeKitCatalog.Contract contract)
        {
            string assetPath = $"{outputFolder}/{TreeKitCatalog.ContractFileName}";
            File.WriteAllText(Path.Combine(RigCatalog.RepoRoot, assetPath),
                              JsonUtility.ToJson(contract, prettyPrint: true) + "\n");
            return assetPath;
        }

        // =====================================================================================
        // shared
        // =====================================================================================

        /// <summary>Single-quoted JS string literal (species keys are plain identifiers, but escape
        /// defensively — a quote in a name must not become an injection).</summary>
        static string Js(string s) => FishingKitBaker.Js(s);
    }
}
