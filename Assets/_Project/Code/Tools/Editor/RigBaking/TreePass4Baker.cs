using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using HiddenHarbours.Art.Editor;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace HiddenHarbours.Tools.RigBaking
{
    /// <summary>What <see cref="TreePass4Baker.ExportContract"/> wrote.</summary>
    public sealed class TreePass4ExportResult
    {
        public string EngineName;
        public string ContractPath;
        public TreeKitCatalog.Contract Contract;
        public double SurveyMilliseconds;
        public double TotalMilliseconds;
    }

    /// <summary>What <see cref="TreePass4Baker.Bake"/> wrote.</summary>
    public sealed class TreePass4BakeResult
    {
        public string EngineName;
        public readonly List<TreeSheetBake> Sheets = new List<TreeSheetBake>();
        public string PalettePath;
        public int PaletteRows;
        public string ContractPath;
        public TreeKitCatalog.Contract Contract;
        public int CellsRead;
        public double GlueMilliseconds;
        public double TotalMilliseconds;
        public long TotalPngBytes;

        /// <summary>What the written sheets and the palette take on the GPU as the slicer imports
        /// them: 4 bytes a texel, except the snow sheet, which imports as R8 (1 byte). The MB the
        /// season options are priced in.</summary>
        public long TextureBytes;
    }

    /// <summary>
    /// Bakes the pass-4 tree kit: <c>docs/art/rigs/treeIsoRig4.js</c> and <c>treeMaps4.js</c>,
    /// read through the <see cref="TreePass4Glue"/>. Pass 3's <see cref="TreeRigBaker"/> stays
    /// untouched beside it; <see cref="TreeBakeMenu"/> picks one by
    /// <see cref="TreeKitCatalog.IsPass4Live"/>.
    ///
    /// <para><b>ShrubBaker's rule: the contract is exported, then the bake obeys it.</b>
    /// <see cref="ExportContract"/> surveys the rig and writes <c>Trees.json</c>: the pass-3
    /// placement fields plus the wind response, the season rows, the snow row and the palette
    /// layout. It writes no sheet. <see cref="Bake"/> reads that contract, checks the rig's cell,
    /// pivot and sheet against it, and hands the glue the contract's colour rows; the glue refuses
    /// any colour the rows do not hold. A refusal anywhere writes ZERO files, because every species
    /// is rendered and checked in memory before the first PNG goes to disk. Re-exporting is the
    /// deliberate act; baking never rewrites the contract.</para>
    ///
    /// <para>Per species × season it writes <see cref="TreeKitCatalog.ChannelsFor"/>: six sheets
    /// (albedo, mask, normal, wind, phase, snow) for a season with its own maps, the albedo alone
    /// for a deciduous autumn that keeps summer's maps, and nothing for an evergreen autumn. Each
    /// sheet is ONE row of the four variants at rest (<see cref="TreeRigBaker.SwayRowsBaked"/>);
    /// the shader draws the wind from the maps. Then <see cref="TreeKitCatalog.PaletteFileName"/>:
    /// 8 texels wide, row 0 the snow row, then every distinct gap row, bottom up.</para>
    ///
    /// <para>⚠️ <b>While the game draws pass 3, the live kit is off limits.</b> Both entry points
    /// refuse a path under <see cref="TreeKitCatalog.TreesRoot"/> until
    /// <see cref="TreeKitCatalog.RigScriptPath"/> names rig 4, so nothing can half-switch the kit:
    /// the tests bake into <c>Temp/</c>.</para>
    /// </summary>
    public static class TreePass4Baker
    {
        public static string DefaultOutputFolder => TreeRigBaker.DefaultOutputFolder;

        /// <summary>The seasons a contract covers until the owner rules on the MB (charter §6).
        /// Summer is also the only season whose sheets pass 3 shipped.</summary>
        public static readonly string[] DefaultSeasons = { TreeRigBaker.DefaultSeason };

        const string G = TreeKitCatalog.Pass4GlueGlobalName;
        const string R4 = TreeKitCatalog.Pass4RigGlobalName;
        const string M4 = TreeKitCatalog.Pass4MapsGlobalName;

        /// <summary>A host global that holds the glue's last JSON, parsed, so C# reads it field by
        /// field the way the other bakers read a rig.</summary>
        const string Last = "HHTreePass4Last";
        const string Spec = "HHTreePass4Spec";

        // =====================================================================================
        // the contract
        // =====================================================================================

        /// <summary>
        /// Surveys every species (null = everything <c>TreeRig4.SPECIES</c> declares) at
        /// <paramref name="stage"/> over <paramref name="seasons"/> and writes the pass-4 contract
        /// to <paramref name="contractPath"/> (null = the kit's <c>Trees.json</c>, refused while the
        /// game draws pass 3). Renders every variant at rest but writes no sheet.
        /// </summary>
        public static TreePass4ExportResult ExportContract(IReadOnlyList<string> species = null,
                                                           string stage = TreeRigBaker.DefaultStage,
                                                           IReadOnlyList<string> seasons = null,
                                                           string contractPath = null,
                                                           Action<string, float> progress = null)
        {
            contractPath ??= TreeKitCatalog.ContractPath;
            seasons ??= DefaultSeasons;
            RefuseTheLiveKitWhilePass3(contractPath, "write a pass-4 contract");
            var total = Stopwatch.StartNew();

            using IRigScriptHost host = RigScriptHostFactory.Create();
            InstallRig(host);
            AssertStage(host, stage);
            species ??= FishingKitBaker.ReadStringArray(host, $"{R4}.SPECIES.map(function (s) {{ return s.key; }})");

            var result = new TreePass4ExportResult { EngineName = host.EngineName, ContractPath = contractPath };
            var survey = new Stopwatch();
            var entries = new List<TreeKitCatalog.Entry>(species.Count);
            var snow = new SortedSet<int>();
            int loop = (int)host.EvaluateNumber($"{R4}.LOOP");

            int done = 0;
            foreach (string key in species)
            {
                progress?.Invoke($"{key}_{stage}", (float)done++ / species.Count);
                AssertSpecies(host, key);
                var spec = ReadSheetSpec(host, key, stage, out int windReach);
                TreeRigBaker.AssertFits(key, stage, spec);

                survey.Start();
                host.Execute($"globalThis.{Last} = JSON.parse({G}.survey({Js(key)}, {Js(stage)}, {JsArray(seasons)}));");
                survey.Stop();

                AssertCell(host, key, spec.CellW, spec.CellH, "sheetSpec()");
                foreach (string c in ReadList(host, $"{Last}.snow")) snow.Add(Hex(c, $"{key} snow"));

                string[] baked = ReadList(host, $"Object.keys({Last}.seasons)");
                var rows = new TreeKitCatalog.SeasonRow[baked.Length];
                for (int i = 0; i < baked.Length; i++)
                {
                    string s = $"{Last}.seasons[{Js(baked[i])}]";
                    rows[i] = new TreeKitCatalog.SeasonRow
                    {
                        season = baked[i],
                        maps = host.EvaluateString($"{s}.maps"),
                        albedo = host.EvaluateString($"{s}.albedo"),
                        gapRow = ReadList(host, $"{s}.gapRow"),
                    };
                }

                string cx = $"{Last}.constants";
                if ((int)host.EvaluateNumber($"{cx}.loop") != loop)
                    throw new InvalidOperationException(
                        $"{key}: TreeMaps4 loops in {host.EvaluateNumber($"{cx}.loop")} frames but " +
                        $"TreeRig4.LOOP is {loop}. The shader steps through ONE loop for every species.");
                var wind = new TreeKitCatalog.WindBlock
                {
                    H = Float(host, $"{cx}.H"),
                    bendPx = Float(host, $"{cx}.bendPx"),
                    limbPx = Float(host, $"{cx}.limbPx"),
                    bob = Float(host, $"{cx}.bob"),
                    flutter = Float(host, $"{cx}.flutter"),
                    shimmer = host.EvaluateBool($"{cx}.shimmer === null")
                        ? Array.Empty<float>()
                        : new[] { Float(host, $"{cx}.shimmer[0]"), Float(host, $"{cx}.shimmer[1]") },
                    conifer = host.EvaluateBool($"!!{cx}.conifer"),
                    windReach = windReach,
                };

                entries.Add(BuildEntry(host, key, stage, baked, spec, wind, rows));
            }
            host.Execute($"{G}.release(); delete globalThis.{Last}; delete globalThis.{Spec};");

            if (snow.Count > TreeKitCatalog.PaletteWidth)
                throw new InvalidOperationException(
                    $"The rig snows in {snow.Count} colours and a palette row holds " +
                    $"{TreeKitCatalog.PaletteWidth}. The snow row is shared by every species; widen the " +
                    "palette deliberately (the shader's index is 3 bits) rather than dropping colours.");
            string[] snowRow = PadRow(snow);
            int paletteRows = AssignPaletteRows(entries);

            result.Contract = BuildContract(host, stage, seasons, entries.ToArray(), snowRow, paletteRows, loop);
            WriteContract(contractPath, result.Contract);

            result.SurveyMilliseconds = survey.Elapsed.TotalMilliseconds;
            total.Stop();
            result.TotalMilliseconds = total.Elapsed.TotalMilliseconds;
            return result;
        }

        /// <summary>Row 0 is the snow row; every distinct gap row after it takes the next row, in
        /// contract order. Returns the row count.</summary>
        static int AssignPaletteRows(List<TreeKitCatalog.Entry> entries)
        {
            var rowOf = new Dictionary<string, int>(StringComparer.Ordinal);
            int next = 1;
            foreach (var e in entries)
                foreach (var row in e.seasonRows)
                {
                    string k = string.Join(",", row.gapRow);
                    if (!rowOf.TryGetValue(k, out int r)) rowOf[k] = r = next++;
                    row.paletteRow = r;
                }
            return next;
        }

        static TreeKitCatalog.Entry BuildEntry(IRigScriptHost host, string species, string stage,
                                               string[] seasons, in TreeRigBaker.SheetSpec spec,
                                               TreeKitCatalog.WindBlock wind,
                                               TreeKitCatalog.SeasonRow[] rows)
        {
            string sp = $"{R4}.byKey[{Js(species)}]";
            return new TreeKitCatalog.Entry
            {
                species = species,
                name = host.EvaluateString($"{sp}.name"),
                latin = host.EvaluateString($"{sp}.latin"),
                form = host.EvaluateString($"{sp}.form"),
                stage = stage,
                seasons = seasons,
                metres = spec.Metres,
                cellW = spec.CellW,
                cellH = spec.CellH,
                pivotX = spec.PivotX,
                pivotY = spec.PivotY,
                nearFlarePad = spec.Pad,
                trunkAnchor = spec.TrunkAnchor,
                unityPivotX = (float)spec.PivotX / spec.CellW,
                unityPivotY = (float)spec.Pad / spec.CellH,
                sheetW = spec.SheetW,
                sheetH = spec.SheetH,
                rigSheetW = spec.RigSheetW,
                rigSheetH = spec.RigSheetH,
                rigFitsUnity2048 = spec.RigFits,
                // Rig 4 reports no rule audit: its render() carries no thinPct and its mass report
                // is internal. Left empty on purpose, and said so in the contract's note.
                audit = new TreeKitCatalog.Audit(),
                wind = wind,
                seasonRows = rows,
            };
        }

        static TreeKitCatalog.Contract BuildContract(IRigScriptHost host, string stage,
                                                     IReadOnlyList<string> seasons,
                                                     TreeKitCatalog.Entry[] entries,
                                                     string[] snowRow, int paletteRows, int loop)
        {
            return new TreeKitCatalog.Contract
            {
                note = GeneratedNote(stage, seasons),
                rig = TreeKitCatalog.Pass4RigScriptPath,
                global = R4,
                ppu = (int)host.EvaluateNumber($"{R4}.PPU"),
                camera = new TreeKitCatalog.CameraBlock
                {
                    name = "ADR-0006/0022",
                    view = "3/4 from S, orthographic",
                    elevDeg = Float(host, $"{R4}.ELEV"),
                    heightScale = Float(host, $"{R4}.CE"),
                    depthScale = Float(host, $"{R4}.SE"),
                },
                light = new TreeKitCatalog.LightBlock
                {
                    key = ReadVec3(host, $"{G}.KEY"),
                    rim = ReadVec3(host, $"{G}.RIM"),
                    note = "the MASK's fixed upper-left key + back rim, screen space (art bible " +
                           "section 1): rig 3's LIGHT, which rig 4 keeps as its own. The albedo " +
                           "is relit once under TreeRig4.REF_SKY; the game's light comes from the " +
                           "mask and normal, as in pass 3",
                },
                rules = new TreeKitCatalog.RulesBlock
                {
                    rimPx = (int)host.EvaluateNumber($"{R4}.RIM_PX"),
                    minBodyPx = (int)host.EvaluateNumber($"{R4}.MIN_BODY"),
                    minClumpRadiusPx = (int)host.EvaluateNumber($"{R4}.MIN_R"),
                    note = "a 2 px rim must leave 6 px of interior; nothing is emitted under a " +
                           "5 px clump radius; the rim is gated on local mass thickness",
                },
                sheet = new TreeKitCatalog.SheetBlock
                {
                    cols = (int)host.EvaluateNumber($"{R4}.VARIANTS"),
                    rows = TreeRigBaker.SwayRowsBaked,
                    colAxis = "variant",
                    rowAxis = "sway frame",
                    rigSwayRows = loop / 4,
                    swayNote = "ONE row is baked: the four variants at REST. The rig's " +
                               $"{loop}-frame wind loop is not baked as frames; " +
                               "HiddenHarboursTreeWind.shader replays the rig's own response from " +
                               "the wind and phase maps, off the shared deterministic wind " +
                               "(_WindWorld) the grass and the water read, so one gust moves all " +
                               "three together.",
                },
                channels = new TreeKitCatalog.ChannelBlock
                {
                    albedo = "RGBA: TreeRig4.relight at rest under REF_SKY, binary alpha, no AA, " +
                             "no keyline (ADR 0031)",
                    mask = "R=key light, G=back rim, B=depth, A=coverage: pass 3's order, by rig " +
                           "3's formulas on the rest frame",
                    normal = "view-space normal, R=x, G=y-up, B=toward camera",
                    coverageNote = "albedo, mask, normal and snow cover the same pixels at rest. " +
                                   "The wind and phase sheets are DATA: wind.B, wind.A and phase.A " +
                                   "carry the packed word W, never alpha, so they import with " +
                                   "alphaIsTransparency OFF. A leaf the wind moves is gathered " +
                                   "from its rest pixel by the shader, so every channel is " +
                                   "sampled at the one source texel it finds.",
                },
                trees = entries,
                maps = new TreeKitCatalog.MapsBlock
                {
                    script = TreeKitCatalog.Pass4MapsScriptPath,
                    global = M4,
                    glue = G,
                    glueVersion = TreePass4Glue.Version,
                    loop = loop,
                    windNote = "wind: R=lean, G=sway as TreeMaps4 writes them. phase: R=wave, " +
                               "G=play, B=depth. Per species the response is the tree's wind " +
                               "block (bendPx, limbPx, bob, flutter, shimmer, conifer), read by " +
                               "the shader per renderer the way _TrunkAnchor is.",
                    snowNote = "snow: R=G=B = the cover x 254 at which the pixel turns to snow; " +
                               "255 never snows, 0 is outside. The shader thresholds it by the " +
                               "global _FoliageSnow; a snowed pixel takes its colour from the " +
                               "palette's snow row.",
                    packNote = "W, 24 bits = wind.B << 16 | wind.A << 8 | phase.A, high to low: " +
                               "class 2 (0 outside, 1 wood, 2 between leaves, 3 leaf) | gapSnow 3 " +
                               "| gap 3 | snow 3 | stamp id + 1 13. snow and gapSnow index the " +
                               "palette's snow row, gap indexes the season's gap row. Read the " +
                               "class from W, never wind.B > 127.",
                },
                snowRow = snowRow,
                palette = new TreeKitCatalog.PaletteBlock
                {
                    file = TreeKitCatalog.PaletteFileName,
                    width = TreeKitCatalog.PaletteWidth,
                    rows = paletteRows,
                    note = "sRGB colours, point-sampled. Row 0 (the bottom texel row, as " +
                           "Texture2D.GetPixel and the shader's Load number it) is the snow row; " +
                           "each season row's paletteRow names its gap row. Rows are sorted by " +
                           "packed RGB and padded by repeating the last colour.",
                },
            };
        }

        /// <summary>The contract's provenance. Deterministic: no clock, no machine name, so a
        /// re-export of an unchanged rig is a zero diff.</summary>
        public static string GeneratedNote(string stage, IReadOnlyList<string> seasons) =>
            "Written by TreePass4Baker.ExportContract from docs/art/rigs/treeIsoRig4.js and " +
            $"treeMaps4.js through the {G} glue (version {TreePass4Glue.Version}), stage {stage}, " +
            $"seasons {string.Join(", ", seasons)}. REGENERATE rather than hand-edit (Hidden " +
            "Harbours > Art > Export Acadian Tree Contract, then Bake Acadian Trees); the bake " +
            "obeys this file and refuses a rig that drifted from it. pivotX/pivotY are the TRUNK " +
            "FOOT in cell px from the top-left; nearFlarePad rows of near-root flare sit BELOW " +
            "it. unityPivot is the same point normalised bottom-origin, and trunkAnchor is that " +
            "height as a fraction of the cell. audit is empty: rig 4 reports no rule audit.";

        static void WriteContract(string contractPath, TreeKitCatalog.Contract contract)
        {
            string full = Path.Combine(RigCatalog.RepoRoot, contractPath);
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            File.WriteAllText(full, JsonUtility.ToJson(contract, prettyPrint: true) + "\n");
        }

        // =====================================================================================
        // the bake
        // =====================================================================================

        /// <summary>
        /// Bakes what the contract at <paramref name="contractPath"/> declares (null = the kit's
        /// <c>Trees.json</c>) into <paramref name="outputFolder"/> (null = the kit, refused while
        /// the game draws pass 3). <paramref name="species"/> narrows it; the palette is always
        /// written whole, because it is shared.
        /// </summary>
        public static TreePass4BakeResult Bake(IReadOnlyList<string> species = null,
                                               string outputFolder = null,
                                               string contractPath = null,
                                               Action<string, float> progress = null)
        {
            outputFolder ??= DefaultOutputFolder;
            contractPath ??= TreeKitCatalog.ContractPath;
            RefuseTheLiveKitWhilePass3(outputFolder, "bake pass-4 sheets");
            var total = Stopwatch.StartNew();

            var contract = ReadContract(contractPath);
            var entries = Pick(contract, species);
            string[][] palette = PaletteRows(contract);

            using IRigScriptHost host = RigScriptHostFactory.Create();
            InstallRig(host);

            var result = new TreePass4BakeResult
            {
                EngineName = host.EngineName, ContractPath = contractPath, Contract = contract,
                PaletteRows = palette.Length,
            };
            var glue = new Stopwatch();
            var pending = new List<(TreeKitCatalog.Entry e, string season, TreeKitCatalog.Channel c,
                                    TreeRigBaker.SheetSpec spec, byte[][] cells)>();

            // Everything is rendered and checked before the first file is written.
            int done = 0;
            foreach (var e in entries)
            {
                progress?.Invoke($"{e.species}_{e.stage}", (float)done++ / entries.Count);

                AssertSpecies(host, e.species);
                AssertStage(host, e.stage);
                var spec = ReadSheetSpec(host, e.species, e.stage, out int windReach);
                AssertMatchesContract(e, spec, windReach);
                TreeRigBaker.AssertFits(e.species, e.stage, spec);

                glue.Start();
                try
                {
                    host.Execute($"globalThis.{Last} = JSON.parse({G}.bake({Js(e.species)}, " +
                                 $"{Js(e.stage)}, {JsArray(e.seasons)}, {JsArray(contract.snowRow)}, " +
                                 $"{GapRowsJs(e)}));");
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException(
                        $"{e.species}/{e.stage}: the glue refused the bake, and nothing was " +
                        $"written. {ex.Message}", ex);
                }
                finally
                {
                    glue.Stop();
                }

                AssertCell(host, e.species, e.cellW, e.cellH, "the contract");
                AssertSheetsMatch(host, e);

                foreach (string season in e.seasons)
                    foreach (var channel in TreeKitCatalog.ChannelsFor(e, season))
                    {
                        var cells = new byte[spec.Cols][];
                        for (int v = 0; v < spec.Cols; v++)
                        {
                            string expr = $"{G}.cell({Js(season)}, {Js(GlueChannel(channel))}, {v})";
                            glue.Start();
                            cells[v] = host.EvaluateBytes(expr);
                            glue.Stop();
                            if (cells[v].Length != spec.CellW * spec.CellH * 4)
                                throw new InvalidOperationException(
                                    $"`{expr}` came back {cells[v].Length} bytes, expected " +
                                    $"{spec.CellW * spec.CellH * 4} for {spec.CellW}×{spec.CellH} RGBA.");
                            result.CellsRead++;
                        }
                        pending.Add((e, season, channel, spec, cells));
                    }
                host.Execute($"{G}.release();");
            }
            host.Execute($"delete globalThis.{Last}; delete globalThis.{Spec};");

            Directory.CreateDirectory(Path.Combine(RigCatalog.RepoRoot, outputFolder));
            foreach (var p in pending)
                result.Sheets.Add(WriteSheet(outputFolder, p.e, p.season, p.c, p.spec, p.cells, result));
            result.PalettePath = WritePalette(outputFolder, palette, result);

            result.GlueMilliseconds = glue.Elapsed.TotalMilliseconds;
            total.Stop();
            result.TotalMilliseconds = total.Elapsed.TotalMilliseconds;
            return result;
        }

        static TreeKitCatalog.Contract ReadContract(string contractPath)
        {
            string full = Path.Combine(RigCatalog.RepoRoot, contractPath);
            if (!File.Exists(full))
                throw new FileNotFoundException(
                    $"No tree contract at {contractPath}. Export it first " +
                    "(TreePass4Baker.ExportContract): the bake obeys the contract, never the reverse.",
                    full);

            var contract = JsonUtility.FromJson<TreeKitCatalog.Contract>(File.ReadAllText(full));
            if (!TreeKitCatalog.HasPass4(contract))
                throw new InvalidOperationException(
                    $"{contractPath} is not a pass-4 contract (no snow row or maps block). Export " +
                    "the pass-4 contract before baking pass 4.");
            if (contract.maps.glueVersion != TreePass4Glue.Version ||
                !string.Equals(contract.maps.script, TreeKitCatalog.Pass4MapsScriptPath, StringComparison.Ordinal) ||
                !string.Equals(contract.rig, TreeKitCatalog.Pass4RigScriptPath, StringComparison.Ordinal))
                throw new InvalidOperationException(
                    $"{contractPath} was written from {contract.rig} + {contract.maps.script} by glue " +
                    $"version {contract.maps.glueVersion}; this baker reads " +
                    $"{TreeKitCatalog.Pass4RigScriptPath} + {TreeKitCatalog.Pass4MapsScriptPath} with " +
                    $"glue version {TreePass4Glue.Version}. Export the contract again.");
            if (contract.trees == null || contract.trees.Length == 0)
                throw new InvalidOperationException($"{contractPath} carries no trees.");
            return contract;
        }

        static List<TreeKitCatalog.Entry> Pick(TreeKitCatalog.Contract contract,
                                               IReadOnlyList<string> species)
        {
            var picked = new List<TreeKitCatalog.Entry>();
            if (species == null)
            {
                picked.AddRange(contract.trees);
            }
            else
            {
                foreach (string key in species)
                {
                    int before = picked.Count;
                    foreach (var e in contract.trees)
                        if (string.Equals(e.species, key, StringComparison.Ordinal)) picked.Add(e);
                    if (picked.Count == before)
                        throw new ArgumentException(
                            $"The contract has no tree '{key}'. Export it into the contract before " +
                            "baking it: the bake only obeys.");
                }
            }
            foreach (var e in picked)
                if (!TreeKitCatalog.HasPass4(e))
                    throw new InvalidOperationException(
                        $"{e.species}/{e.stage} carries no wind block or season rows. Export the " +
                        "contract again.");
            return picked;
        }

        /// <summary>
        /// The rig's live geometry vs the committed contract, ShrubBaker's rule: <b>this refuses; it
        /// does not rewrite.</b> The cell, the trunk foot and the flare pad move the tree's planted
        /// point and its <c>_TrunkAnchor</c> together, so any one of them drifting is a re-export.
        /// </summary>
        public static void AssertMatchesContract(TreeKitCatalog.Entry e, in TreeRigBaker.SheetSpec spec,
                                                 int windReach)
        {
            string who = $"{e.species}/{e.stage}";
            if (e.cellW != spec.CellW || e.cellH != spec.CellH)
                throw new InvalidOperationException(
                    $"{who}: the rig's cell is {spec.CellW}×{spec.CellH} but the contract says " +
                    $"{e.cellW}×{e.cellH}. The cell, the trunk foot and _TrunkAnchor move together — " +
                    "export the contract again rather than baking sheets that disagree with it.");
            if (e.pivotX != spec.PivotX || e.pivotY != spec.PivotY || e.nearFlarePad != spec.Pad)
                throw new InvalidOperationException(
                    $"{who}: the rig's trunk foot is ({spec.PivotX},{spec.PivotY}) over {spec.Pad} " +
                    $"rows of flare but the contract says ({e.pivotX},{e.pivotY}) over " +
                    $"{e.nearFlarePad}. Every tree of this species would be planted wrong by the " +
                    "difference — export the contract again.");
            if (e.sheetW != spec.SheetW || e.sheetH != spec.SheetH)
                throw new InvalidOperationException(
                    $"{who}: the rig lays out {spec.SheetW}×{spec.SheetH} but the contract says " +
                    $"{e.sheetW}×{e.sheetH}. Both dimensions or neither — export the contract again.");
            if (e.wind.windReach != windReach)
                throw new InvalidOperationException(
                    $"{who}: the rig pads the cell by {windReach} px for the wind but the contract " +
                    $"says {e.wind.windReach}. Export the contract again.");
        }

        /// <summary>The sheets the glue baked must be exactly the ones the contract routes: an
        /// autumn albedo that started (or stopped) matching summer's changes which files exist.</summary>
        static void AssertSheetsMatch(IRigScriptHost host, TreeKitCatalog.Entry e)
        {
            var baked = new HashSet<string>(
                ReadList(host, $"{Last}.sheets.map(function (s) {{ return s.season + '/' + s.channel; }})"),
                StringComparer.Ordinal);
            var routed = new HashSet<string>(StringComparer.Ordinal);
            foreach (string season in e.seasons)
                foreach (var c in TreeKitCatalog.ChannelsFor(e, season))
                    routed.Add($"{season}/{GlueChannel(c)}");
            if (!baked.SetEquals(routed))
                throw new InvalidOperationException(
                    $"{e.species}/{e.stage}: the contract routes [{string.Join(" ", Sorted(routed))}] " +
                    $"but the rig bakes [{string.Join(" ", Sorted(baked))}]. A season's albedo or " +
                    "maps changed hands — export the contract again.");
        }

        /// <summary>Row 0 is the snow row; every other row is the gap row the season rows point at.
        /// Refuses a hole, an out-of-range row, or one row claimed by two different gap rows.</summary>
        public static string[][] PaletteRows(TreeKitCatalog.Contract contract)
        {
            int n = contract.palette?.rows ?? 0;
            if (n < 1 || n > TreeKitCatalog.ImportSizeCap)
                throw new InvalidOperationException($"The contract's palette has {n} rows.");
            var rows = new string[n][];
            rows[0] = CheckRow(contract.snowRow, "the snow row");
            foreach (var e in contract.trees)
                foreach (var sr in e.seasonRows ?? Array.Empty<TreeKitCatalog.SeasonRow>())
                {
                    string who = $"{e.species} {sr.season}";
                    if (sr.paletteRow < 1 || sr.paletteRow >= n)
                        throw new InvalidOperationException(
                            $"{who}: palette row {sr.paletteRow} is outside 1..{n - 1}. Export the contract again.");
                    CheckRow(sr.gapRow, who);
                    if (rows[sr.paletteRow] == null) rows[sr.paletteRow] = sr.gapRow;
                    else if (string.Join(",", rows[sr.paletteRow]) != string.Join(",", sr.gapRow))
                        throw new InvalidOperationException(
                            $"{who}: palette row {sr.paletteRow} holds two different gap rows. Export " +
                            "the contract again.");
                }
            for (int r = 0; r < n; r++)
                if (rows[r] == null)
                    throw new InvalidOperationException(
                        $"Palette row {r} is claimed by no season. Export the contract again.");
            return rows;
        }

        static string[] CheckRow(string[] row, string who)
        {
            if (row == null || row.Length != TreeKitCatalog.PaletteWidth)
                throw new InvalidOperationException(
                    $"{who}: a palette row is {TreeKitCatalog.PaletteWidth} colours, found " +
                    $"{row?.Length ?? 0}. Export the contract again.");
            foreach (string c in row) Hex(c, who);
            return row;
        }

        // =====================================================================================
        // the rig
        // =====================================================================================

        /// <summary>
        /// Loads rig 4, then its maps, then the glue, and asserts each installed what this baker
        /// and the glue call. The rig's and the maps' sources run UNMODIFIED (ADR 0021 §5); the
        /// glue is host code.
        /// </summary>
        public static void InstallRig(IRigScriptHost host)
        {
            Run(host, TreeKitCatalog.Pass4RigScriptPath);
            AssertApi(host, R4, "sheetSpec", "relight", "view", "cellOf", "clearCache");
            Run(host, TreeKitCatalog.Pass4MapsScriptPath);
            AssertApi(host, M4, "rest", "snowMap", "windMaps", "constants", "shade");
            host.Execute(TreePass4Glue.Js);
            AssertApi(host, G, "survey", "bake", "cell", "release");

            int version = (int)host.EvaluateNumber($"{G}.VERSION");
            if (version != TreePass4Glue.Version)
                throw new InvalidOperationException(
                    $"The glue reports version {version} but TreePass4Glue.Version is " +
                    $"{TreePass4Glue.Version}: the embedded source and its constant disagree.");
            if ((int)host.EvaluateNumber($"{G}.PAL") != TreeKitCatalog.PaletteWidth)
                throw new InvalidOperationException(
                    $"The glue's palette is {host.EvaluateNumber($"{G}.PAL")} wide but " +
                    $"TreeKitCatalog.PaletteWidth is {TreeKitCatalog.PaletteWidth}.");
        }

        static void Run(IRigScriptHost host, string scriptPath)
        {
            string full = Path.Combine(RigCatalog.RepoRoot, scriptPath);
            if (!File.Exists(full))
                throw new FileNotFoundException(
                    $"Pass-4 tree source missing at {full}. It is committed under docs/art/rigs/.", full);
            host.Execute(File.ReadAllText(full));
        }

        static void AssertApi(IRigScriptHost host, string global, params string[] fns)
        {
            if (!host.EvaluateBool($"typeof {global} === 'object' && {global} !== null"))
                throw new InvalidOperationException(
                    $"globalThis.{global} did not install — the source changed shape.");
            foreach (string fn in fns)
                if (!host.EvaluateBool($"typeof {global}.{fn} === 'function'"))
                    throw new InvalidOperationException(
                        $"{global}.{fn}() is missing — a renamed export must fail here, not mid-bake.");
        }

        static void AssertStage(IRigScriptHost host, string stage)
        {
            if (!host.EvaluateBool($"typeof {R4}.STAGES[{Js(stage)}] === 'number'"))
                throw new ArgumentException(
                    $"TreeRig4 declares no stage '{stage}'. Known: " +
                    string.Join(", ", FishingKitBaker.ReadStringArray(host, $"{R4}.STAGE_KEYS")) + ".");
        }

        static void AssertSpecies(IRigScriptHost host, string species)
        {
            if (!host.EvaluateBool($"!!{R4}.byKey[{Js(species)}]"))
                throw new ArgumentException($"TreeRig4 declares no species '{species}'.");
        }

        /// <summary>Rig 4's own <c>sheetSpec()</c>, laid out as we bake it: one row of the four
        /// variants. <paramref name="windReach"/> is the padding the rig gives the cell each side
        /// so a gale has room.</summary>
        public static TreeRigBaker.SheetSpec ReadSheetSpec(IRigScriptHost host, string species,
                                                           string stage, out int windReach)
        {
            host.Execute($"globalThis.{Spec} = {R4}.sheetSpec({Js(species)}, {R4}.STAGES[{Js(stage)}]);");
            int cellH = (int)host.EvaluateNumber($"{Spec}.cell[1]");
            int pivotY = (int)host.EvaluateNumber($"{Spec}.pivot[1]");
            int pad = (int)host.EvaluateNumber($"{Spec}.pad");
            if (pad != cellH - 1 - pivotY)
                throw new InvalidOperationException(
                    $"{species}/{stage}: the rig reports pad {pad} but cellH−1−pivotY is " +
                    $"{cellH - 1 - pivotY}. The pivot convention changed — read cellOf() before baking.");
            windReach = (int)host.EvaluateNumber($"{Spec}.windReach");
            return new TreeRigBaker.SheetSpec(
                cellW: (int)host.EvaluateNumber($"{Spec}.cell[0]"),
                cellH: cellH,
                pivotX: (int)host.EvaluateNumber($"{Spec}.pivot[0]"),
                pivotY: pivotY,
                pad: pad,
                cols: (int)host.EvaluateNumber($"{Spec}.cols"),
                rows: TreeRigBaker.SwayRowsBaked,
                rigSheetW: (int)host.EvaluateNumber($"{Spec}.w"),
                rigSheetH: (int)host.EvaluateNumber($"{Spec}.h"),
                rigFits: host.EvaluateBool($"{Spec}.fits"),
                metres: Float(host, $"{Spec}.metres"));
        }

        static void AssertCell(IRigScriptHost host, string species, int w, int h, string against)
        {
            int cw = (int)host.EvaluateNumber($"{Last}.cell[0]");
            int ch = (int)host.EvaluateNumber($"{Last}.cell[1]");
            if (cw != w || ch != h)
                throw new InvalidOperationException(
                    $"{species}: the maps render {cw}×{ch} cells but {against} says {w}×{h}. The " +
                    "rig and its maps disagree about the cell — stop and report it to the art director.");
        }

        // =====================================================================================
        // the files
        // =====================================================================================

        static TreeSheetBake WriteSheet(string outputFolder, TreeKitCatalog.Entry e, string season,
                                        TreeKitCatalog.Channel channel, in TreeRigBaker.SheetSpec spec,
                                        byte[][] cells, TreePass4BakeResult result)
        {
            int pw = spec.SheetW, ph = spec.SheetH;
            var pixels = new Color32[pw * ph];
            for (int v = 0; v < spec.Cols; v++)
                RigBaker.Blit(cells[v], spec.CellW, spec.CellH, pixels, pw, ph, col: v, rowFromTop: 0);

            string stem = TreeKitCatalog.StemFor(e.species, e.stage, season, channel);
            string assetPath = $"{outputFolder}/{stem}.png";
            result.TotalPngBytes += WritePng(assetPath, pw, ph, pixels);
            result.TextureBytes += (long)pw * ph * (channel == TreeKitCatalog.Channel.Snow ? 1 : 4);

            return new TreeSheetBake
            {
                Stem = stem, AssetPath = assetPath, Species = e.species, Stage = e.stage,
                Season = season, Channel = channel,
                Width = pw, Height = ph, Cols = spec.Cols, Rows = spec.Rows,
            };
        }

        /// <summary>
        /// <see cref="TreeKitCatalog.PaletteWidth"/> texels × one row per palette row. Texel
        /// <c>(x, r)</c> as <c>Texture2D.GetPixel</c> and the shader's <c>Load</c> number it is
        /// colour <c>x</c> of row <c>r</c>: <c>SetPixels32</c> fills from the bottom row up, so
        /// row 0, the snow row, is the bottom line of the PNG.
        /// </summary>
        static string WritePalette(string outputFolder, string[][] rows, TreePass4BakeResult result)
        {
            int w = TreeKitCatalog.PaletteWidth, h = rows.Length;
            var pixels = new Color32[w * h];
            for (int r = 0; r < h; r++)
                for (int x = 0; x < w; x++)
                {
                    int c = Hex(rows[r][x], $"palette row {r}");
                    pixels[r * w + x] = new Color32((byte)(c >> 16), (byte)(c >> 8), (byte)c, 255);
                }

            string assetPath = $"{outputFolder}/{TreeKitCatalog.PaletteFileName}";
            result.TotalPngBytes += WritePng(assetPath, w, h, pixels);
            result.TextureBytes += (long)w * h * 4;
            return assetPath;
        }

        static long WritePng(string assetPath, int w, int h, Color32[] pixels)
        {
            // linear: false only matters if the texture were a render target; the PNG holds the
            // exact bytes either way. The colour-space decision lives on the importer.
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, mipChain: false, linear: false);
            try
            {
                tex.SetPixels32(pixels);
                tex.Apply(false, false);
                byte[] png = tex.EncodeToPNG();
                File.WriteAllBytes(Path.Combine(RigCatalog.RepoRoot, assetPath), png);
                return png.Length;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(tex);
            }
        }

        /// <summary>
        /// Refuses a path inside the live kit while <see cref="TreeKitCatalog.RigScriptPath"/>
        /// still names pass 3: pass-4 files there would sit beside pass-3 sheets the game draws,
        /// under a contract that no longer describes them.
        /// </summary>
        public static void RefuseTheLiveKitWhilePass3(string path, string what)
        {
            if (TreeKitCatalog.IsPass4Live) return;
            if (!IsInsideTheLiveKit(path)) return;
            throw new InvalidOperationException(
                $"The game still draws pass 3 (TreeKitCatalog.RigScriptPath is " +
                $"{TreeKitCatalog.RigScriptPath}), so this will not {what} under " +
                $"{TreeKitCatalog.TreesRoot}. Switch RigScriptPath to " +
                $"{TreeKitCatalog.Pass4RigScriptPath} first, or name a folder outside the kit.");
        }

        public static bool IsInsideTheLiveKit(string path)
        {
            string full = Normal(Path.Combine(RigCatalog.RepoRoot, path));
            string kit = Normal(Path.Combine(RigCatalog.RepoRoot, TreeKitCatalog.TreesRoot));
            return string.Equals(full, kit, StringComparison.OrdinalIgnoreCase) ||
                   full.StartsWith(kit + "/", StringComparison.OrdinalIgnoreCase);
        }

        static string Normal(string p) => Path.GetFullPath(p).Replace('\\', '/').TrimEnd('/');

        // =====================================================================================
        // shared
        // =====================================================================================

        /// <summary>The glue's name for a channel: its <c>CHANNELS</c> array.</summary>
        public static string GlueChannel(TreeKitCatalog.Channel c) => c switch
        {
            TreeKitCatalog.Channel.Albedo => "albedo",
            TreeKitCatalog.Channel.Mask => "mask",
            TreeKitCatalog.Channel.Normal => "normal",
            TreeKitCatalog.Channel.Wind => "wind",
            TreeKitCatalog.Channel.Phase => "phase",
            TreeKitCatalog.Channel.Snow => "snow",
            _ => throw new ArgumentOutOfRangeException(nameof(c), c, null),
        };

        /// <summary>Pads a sorted colour set to a palette row by repeating the last colour, the
        /// glue's own rule; an empty set is a row of black.</summary>
        static string[] PadRow(SortedSet<int> colours)
        {
            var sorted = new List<int>(colours);
            var row = new string[TreeKitCatalog.PaletteWidth];
            for (int j = 0; j < row.Length; j++)
                row[j] = sorted.Count == 0 ? "000000"
                       : sorted[Math.Min(j, sorted.Count - 1)].ToString("x6", CultureInfo.InvariantCulture);
            return row;
        }

        static int Hex(string s, string who)
        {
            if (s == null || s.Length != 6 ||
                !int.TryParse(s, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out int c) ||
                s != s.ToLowerInvariant())
                throw new InvalidOperationException($"{who}: '{s}' is not an rrggbb colour.");
            return c;
        }

        static string GapRowsJs(TreeKitCatalog.Entry e)
        {
            var sb = new StringBuilder("{");
            for (int i = 0; i < e.seasonRows.Length; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append(Js(e.seasonRows[i].season)).Append(": ").Append(JsArray(e.seasonRows[i].gapRow));
            }
            return sb.Append('}').ToString();
        }

        static string JsArray(IEnumerable<string> items)
        {
            var sb = new StringBuilder("[");
            foreach (string s in items)
            {
                if (sb.Length > 1) sb.Append(", ");
                sb.Append(Js(s));
            }
            return sb.Append(']').ToString();
        }

        /// <summary>A JS array of plain strings (seasons, colours) as C# strings; empty is fine,
        /// where <see cref="FishingKitBaker.ReadStringArray"/> refuses it.</summary>
        static string[] ReadList(IRigScriptHost host, string arrayExpr)
        {
            string joined = host.EvaluateString($"({arrayExpr}).join(',')");
            return joined.Length == 0 ? Array.Empty<string>() : joined.Split(',');
        }

        static List<string> Sorted(HashSet<string> set)
        {
            var list = new List<string>(set);
            list.Sort(StringComparer.Ordinal);
            return list;
        }

        static float Float(IRigScriptHost host, string expr) => (float)host.EvaluateNumber(expr);

        static float[] ReadVec3(IRigScriptHost host, string arrayExpr) => new[]
        {
            Float(host, $"{arrayExpr}[0]"), Float(host, $"{arrayExpr}[1]"), Float(host, $"{arrayExpr}[2]"),
        };

        static string Js(string s) => FishingKitBaker.Js(s);
    }
}
