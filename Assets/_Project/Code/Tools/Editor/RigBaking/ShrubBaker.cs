using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using HiddenHarbours.Art.Editor;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace HiddenHarbours.Tools.RigBaking
{
    /// <summary>One baked shrub sheet: a species at one growth stage, on one axis.</summary>
    public sealed class ShrubSheetBake
    {
        public string Stem;
        public string Species, Stage, Axis;
        /// <summary>The dimension held FIXED on this sheet — a variant index on a phase-axis sheet,
        /// a phase key on a variant-axis sheet.</summary>
        public string Fixed;
        public int Width, Height, Cols, Rows;
        public readonly List<string> AssetPaths = new List<string>();

        public override string ToString() =>
            $"{Stem}  {Width}×{Height}  ({Cols} {Axis} col(s) × {Rows} sway row(s))  " +
            $"[{AssetPaths.Count} channel(s)]";
    }

    public sealed class ShrubBakeResult
    {
        public string EngineName;
        public readonly List<ShrubSheetBake> Sheets = new List<ShrubSheetBake>();
        public int CellsRendered;
        public double RenderMilliseconds;
        public double TotalMilliseconds;
        public long TotalPngBytes;
    }

    /// <summary>
    /// Bakes the Acadian Shrub kit from <c>docs/art/rigs/shrubIsoRig.js</c> — twenty woody,
    /// waist-high, multi-stemmed species across five habitats, from lowbush blueberry on the barren to
    /// speckled alder in the swale.
    ///
    /// <para><b>⭐ THIS KIT BAKES TO ORDER, AND THAT IS WHY THE BAKER EXISTS.</b> 20 species × two
    /// sheet axes × 8 phases × 5 snow steps × 3 growth stages is a matrix nobody should pick from by
    /// accident, so the drop ships the RIG and no pixels at all — the call the Rock Iso (#312) and
    /// Shore Plant (#318) kits already make. Which slice actually ships is an ATLAS BUDGET decision for
    /// the owner; <see cref="ShrubBakeMenu"/> makes the default one deliberate and prints the
    /// arithmetic.</para>
    ///
    /// <para><b>Three PNGs per sheet, and two of them are DATA.</b> The albedo, then
    /// <c>&lt;stem&gt;_light.png</c> (R key · G rim · B depth · A coverage) and
    /// <c>&lt;stem&gt;_calendar.png</c> (<b>R veil flag</b> · G ornament · B snow · A coverage) — the
    /// contract's own filenames, not renamed here. All three come from ONE <c>render()</c> per cell:
    /// the result is parked on a JS global and the three buffers are read off it, because re-rendering
    /// a cell to fetch its mask would treble the most expensive thing this baker does.</para>
    ///
    /// <para><b>⭐ THIS RIG NEEDS NO SHIM AND NO CATALOG ENTRY.</b> <c>render</c>, <c>packMask</c>,
    /// <c>packState</c>, <c>sheetSpec</c>, <c>contract</c> and the tables are all on
    /// <c>globalThis.Shrubs</c>, so its source runs UNMODIFIED (ADR 0021 §5). It is deliberately absent
    /// from <see cref="RigCatalog"/>: that struct is built around <see cref="AzimuthConvention"/> for
    /// 8-direction turntables, and <b>a shrub has no heading</b> — its sheet axes are phase × sway and
    /// variant × sway. Nothing here calls <c>RigBaker.DirForCell</c>.</para>
    ///
    /// <para><b>⚠ THE COMMITTED CONTRACT IS THE ORACLE, AND THIS BAKER REFUSES RATHER THAN
    /// REWRITES.</b> <see cref="AssertMatchesContract"/> compares the rig's LIVE cell, pivot and both
    /// sheet dimensions against the committed contract before a single pixel is written. The cell is
    /// UNIONED over 4 variants × 8 phases, so a drifted cell moves the ground-contact pivot of every
    /// phase of that species at once — and no sheet exists yet to disagree with it. A rig change
    /// surfaces here as a loud refusal pointing at the art director, never as sheets whose pivots
    /// quietly no longer match their own contract. Regenerating the contract is a SEPARATE, deliberate
    /// act (<see cref="ExportContract"/>).</para>
    ///
    /// <para><b>No water, no baked caustics and no baked moving light</b> (ADR 0010/0012/0023) — the
    /// live shader owns all three, and the sun never goes behind (the tree-light rule). <b>Snow CLIPS,
    /// it never floods</b>: rows below the surface are removed and the scene lines its own drift tile
    /// up with the reported <c>snowRow</c>. No sprite bakes ground.</para>
    ///
    /// <para>Like every sibling baker this stops at "PNGs on disk"; slicing and import settings belong
    /// to <see cref="ShrubSheetSlicer"/>, which <see cref="ShrubBakeMenu"/> chains.</para>
    /// </summary>
    public static class ShrubBaker
    {
        /// <summary>The phase-axis sheet is the gameplay sheet: the calendar advances and every shrub
        /// follows it, so a placed shrub samples this axis. The variant axis is dressing.</summary>
        public const string PhaseAxis = "phase";

        public const string VariantAxis = "variant";

        public const string DefaultStage = "full";
        public const string DefaultPhase = "leaf";
        public const int DefaultVariant = 0;

        public static string DefaultOutputFolder => ShrubCatalog.ShrubsRoot.TrimEnd('/');

        /// <summary>The JS global a render is parked on so its three channels can be read without
        /// rendering three times. Underscored to stay clear of the rig's own namespace.</summary>
        const string ResultGlobal = "__hhShrubRender";

        /// <summary>
        /// ⚠️ <b>The <c>generated</c> field the rig emits is an ISO TIMESTAMP, and we replace it.</b>
        /// A timestamp makes the committed JSON churn on every export for no informational gain, and
        /// the diff noise is exactly what hides a real geometry change. Substituting a deterministic
        /// provenance string keeps the file diff-stable — the same divergence the shore-plant contract
        /// records, and the reason the tests compare FIELDS rather than bytes.
        /// </summary>
        public static string GeneratedNote(string stage) =>
            $"serialised out of {ShrubCatalog.RigScriptPath} by ShrubBaker.ExportContract at " +
            $"{stage} growth stage (the rig's ISO timestamp is deliberately replaced so the " +
            "committed file is diff-stable)";

        // =====================================================================================
        // the contract
        // =====================================================================================

        /// <summary>
        /// Serialises the contract out of the LIVE rig and writes it to
        /// <see cref="ShrubCatalog.ContractPath"/>.
        ///
        /// <para><b>⭐ THE RIG SERIALISES ITS OWN CONTRACT, AND THAT IS THE POINT.</b> The drop ships
        /// no JSON — the kit's README is explicit that the contract comes out of the live rig rather
        /// than being hand-written, so it cannot drift from the bake. <c>Shrubs.contract(size)</c>
        /// carries the projection, six materials with their floor/rim/keyline rules, both bakes'
        /// per-channel semantics, the calendar, the snow law, the habitat table, per-species sheet
        /// numbers and the importer asserts. Hand-typing any of that would be inventing a second
        /// source of truth for numbers the rig already measures.</para>
        ///
        /// <para>This is deliberately NOT part of <see cref="Bake"/>. The contract is the oracle the
        /// bake is checked against; a baker that rewrote it on the way past could never refuse, which
        /// is the whole mechanism <see cref="AssertMatchesContract"/> relies on.</para>
        /// </summary>
        public static string ExportContract(string stage = DefaultStage)
        {
            using IRigScriptHost host = RigScriptHostFactory.Create();
            InstallRig(host);
            AssertStage(host, stage);

            float size = StageSize(host, stage);

            // Pretty-print through the rig's own JSON.stringify: the contract is nested and
            // dictionary-shaped, so round-tripping it through a C# type would mean declaring every
            // field twice and silently dropping anything the rig added.
            string json = host.EvaluateString(
                $"JSON.stringify((function(){{" +
                $"var c = {ShrubCatalog.RigGlobalName}.contract({size.ToString("R", Inv)});" +
                $"c.generated = {Js(GeneratedNote(stage))};" +
                $"return c;}})(), null, 2)");

            if (string.IsNullOrEmpty(json) || json.Length < 512)
                throw new InvalidOperationException(
                    $"{ShrubCatalog.RigGlobalName}.contract() serialised {json?.Length ?? 0} chars — " +
                    "far too small to be the real contract. Do not write it.");

            string full = Path.Combine(RigCatalog.RepoRoot, ShrubCatalog.ContractPath);
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            File.WriteAllText(full, json + "\n");
            return ShrubCatalog.ContractPath;
        }

        // =====================================================================================
        // the bake
        // =====================================================================================

        /// <summary>
        /// Bakes the PHASE-axis sheet (8 calendar stations × 4 sway frames) for each requested species
        /// at one stage × variant. Null species means the kit's own twenty.
        /// </summary>
        public static ShrubBakeResult BakePhaseAxis(IReadOnlyList<string> species = null,
                                                    string stage = DefaultStage,
                                                    int variant = DefaultVariant,
                                                    string outputFolder = null,
                                                    Action<string, float> progress = null)
            => Bake(PhaseAxis, species, stage, variant, null, outputFolder, progress);

        /// <summary>
        /// Bakes the VARIANT-axis sheet (4 variants × 4 sway frames) for each requested species at one
        /// stage × phase.
        /// </summary>
        public static ShrubBakeResult BakeVariantAxis(IReadOnlyList<string> species = null,
                                                      string stage = DefaultStage,
                                                      string phase = DefaultPhase,
                                                      string outputFolder = null,
                                                      Action<string, float> progress = null)
            => Bake(VariantAxis, species, stage, 0, phase, outputFolder, progress);

        static ShrubBakeResult Bake(string axis, IReadOnlyList<string> species, string stage,
                                    int variant, string phase, string outputFolder,
                                    Action<string, float> progress)
        {
            outputFolder ??= DefaultOutputFolder;
            var total = Stopwatch.StartNew();

            using IRigScriptHost host = RigScriptHostFactory.Create();
            InstallRig(host);

            var result = new ShrubBakeResult { EngineName = host.EngineName };
            species ??= ReadSpeciesKeys(host);

            AssertStage(host, stage);
            if (phase != null) AssertPhase(host, phase);

            var contract = ShrubCatalog.Load();
            if (contract == null)
                throw new InvalidOperationException(
                    $"[shrub-baker] {ShrubCatalog.ContractPath} is missing or unreadable — it is the " +
                    "oracle this bake is checked against, so there is nothing to bake against. Run " +
                    "Hidden Harbours ▸ Dev ▸ Export Shrub Contract first.");

            // Validate the WHOLE recipe against the rig AND the contract before writing anything: a
            // species whose union cell drifted must fail with zero files on disk, not half a set of
            // sheets whose pivots no longer match the contract the engine reads.
            var specs = new Dictionary<string, SheetSpec>(StringComparer.Ordinal);
            foreach (string key in species)
            {
                var spec = ReadSheetSpec(host, key, stage, axis);
                AssertFits(key, stage, spec);
                AssertMatchesContract(key, spec, axis, contract);
                specs[key] = spec;
            }

            var renderClock = new Stopwatch();
            Directory.CreateDirectory(Path.Combine(RigCatalog.RepoRoot, outputFolder));

            int done = 0, plan = species.Count;
            foreach (string key in species)
            {
                SheetSpec spec = specs[key];
                progress?.Invoke($"{key}_{stage} ({axis})", (float)done++ / plan);

                // ACROSS = the axis (calendar station or variant), DOWN = the sway frame, always —
                // the same layout as the shore-plant sheets, and the layout the contract's own
                // dimensions describe.
                var cells = new Dictionary<ShrubCatalog.Channel, byte[][][]>();
                foreach (var channel in ShrubCatalog.Channels)
                {
                    cells[channel] = new byte[spec.SwayRows][][];
                    for (int r = 0; r < spec.SwayRows; r++)
                        cells[channel][r] = new byte[spec.Cols][];
                }

                for (int r = 0; r < spec.SwayRows; r++)
                for (int c = 0; c < spec.Cols; c++)
                {
                    // On the phase axis the column IS the phase and the variant is fixed; on the
                    // variant axis it is the other way round. The ROW is the sway frame on both.
                    string cellPhase = axis == PhaseAxis ? ShrubCatalog.PhaseKeys[c] : phase;
                    int cellVariant = axis == PhaseAxis ? variant : c;

                    string expr = ResultExpr(key, stage, cellPhase, cellVariant, frame: r);
                    RenderCell(host, expr, spec, renderClock, result, cells, row: r, col: c);
                }

                string stem = axis == PhaseAxis
                    ? ShrubCatalog.PhaseSheetStem(key, stage, variant)
                    : ShrubCatalog.VariantSheetStem(key, stage, phase);

                var bake = new ShrubSheetBake
                {
                    Stem = stem, Species = key, Stage = stage, Axis = axis,
                    Fixed = axis == PhaseAxis ? $"v{variant}" : phase,
                    Width = spec.SheetW, Height = spec.SheetH,
                    Cols = spec.Cols, Rows = spec.SwayRows,
                };
                foreach (var channel in ShrubCatalog.Channels)
                    bake.AssetPaths.Add(
                        WriteSheet(outputFolder, stem, channel, spec, cells[channel], result));
                result.Sheets.Add(bake);
            }

            result.RenderMilliseconds = renderClock.Elapsed.TotalMilliseconds;
            total.Stop();
            result.TotalMilliseconds = total.Elapsed.TotalMilliseconds;
            return result;
        }

        /// <summary>
        /// <b>ALL FOUR sway rows — because on this kit the contract has no way to say otherwise.</b>
        ///
        /// <para>🔴 This used to be 1, copied across from <c>TreeRigBaker.SwayRowsBaked</c> ("the shader
        /// owns the swaying"), and it made the kit unable to produce a usable sprite. The tree kit gets
        /// to bake one row because <b>its</b> contract records the difference on purpose — it publishes
        /// <c>sheet.rows</c> (what was baked) alongside <c>sheet.rigSwayRows</c> (what the rig can
        /// make), so its slicer knows a one-row sheet is the intended shape. <b>This kit's contract is
        /// serialised straight out of the rig</b> (<see cref="ExportContract"/>): it publishes exactly
        /// one pair of numbers per axis, <c>cols × cellW</c> by <c>SWAY × cellH</c>, and it has no field
        /// in which to record a baker that laid out fewer rows. So a one-row sheet was not a leaner
        /// bake — it was a sheet its own oracle rejects, and <see cref="ShrubSheetSlicer"/> refused all
        /// eighteen of them ("Sliced 0, FAILED 18"), correctly.</para>
        ///
        /// <para>The sibling to follow is the shore-plant kit (#318), whose contract is serialised the
        /// same way and whose baker lays out all four rows for the same reason. It is also the better
        /// art call here: this rig's sway is pinned at the pivot row AND <b>at the snow line</b>, so a
        /// shrub half-buried in pack sways about the drift rather than about its root crown — motion no
        /// uv-space wind shader reading <c>_WindWorld</c> can reproduce, because the shader does not
        /// know where the snow cut the sprite.</para>
        ///
        /// <para>⚠️ This is the count this baker EXPECTS; the count it USES is the rig's live
        /// <c>sheetSpec().rows</c> (see <see cref="SheetSpec.SwayRows"/>), cross-checked against this
        /// in <see cref="ReadSheetSpec"/>. Four rows is 4× the sheet area of one — and that is already
        /// the arithmetic <see cref="SummariseBudget"/> prints, because the rig's own
        /// <c>sheetAudit()</c> has always measured the full four-row sheet.</para>
        /// </summary>
        public const int SwayRowsBaked = ShrubCatalog.SwayFrames;

        // =====================================================================================
        // the rig
        // =====================================================================================

        /// <summary>
        /// Loads <c>shrubIsoRig.js</c> into a host and asserts the global installed. Deliberately not
        /// <c>RigCatalog.Install</c>: that reads <c>W/H/pivot</c> globals a shrub does not have (its
        /// cell is per species per stage) and would throw on the missing <c>pivot</c>. The source is
        /// read and handed over UNMODIFIED.
        /// </summary>
        public static void InstallRig(IRigScriptHost host)
        {
            string full = Path.Combine(RigCatalog.RepoRoot, ShrubCatalog.RigScriptPath);
            if (!File.Exists(full))
                throw new FileNotFoundException(
                    $"Shrub rig source missing at {full}. It is committed under docs/art/rigs/ — if " +
                    "this fired, the branch predates that import.", full);

            host.Execute(File.ReadAllText(full));

            string g = ShrubCatalog.RigGlobalName;
            if (!host.EvaluateBool($"typeof {g} === 'object' && {g} !== null"))
                throw new InvalidOperationException(
                    $"'{ShrubCatalog.RigScriptPath}' ran but did not install globalThis.{g} — the " +
                    "rig changed shape.");

            // The entry points this baker calls. Asserted rather than discovered mid-bake, because a
            // missing one would otherwise surface as a confusing type error on sheet 14.
            foreach (string fn in new[] { "render", "packMask", "packState", "sheetSpec", "cellOf",
                                          "sheetAudit", "contract", "phaseOf", "snowOf" })
                if (!host.EvaluateBool($"typeof {g}.{fn} === 'function'"))
                    throw new InvalidOperationException(
                        $"{g}.{fn}() is missing — this baker calls the rig's public API directly (no " +
                        "shim), so a renamed export must fail loudly here.");
        }

        public static IReadOnlyList<string> ReadSpeciesKeys(IRigScriptHost host) =>
            ReadStringArray(host, $"{ShrubCatalog.RigGlobalName}.SPECIES.map(function(s){{return s.key;}})");

        public static IReadOnlyList<string> ReadPhaseKeys(IRigScriptHost host) =>
            ReadStringArray(host, $"{ShrubCatalog.RigGlobalName}.PHASE_KEYS");

        public static IReadOnlyList<string> ReadSnowKeys(IRigScriptHost host) =>
            ReadStringArray(host, $"{ShrubCatalog.RigGlobalName}.SNOW_KEYS");

        public static IReadOnlyList<string> ReadStageKeys(IRigScriptHost host) =>
            ReadStringArray(host, $"{ShrubCatalog.RigGlobalName}.STAGE_KEYS");

        public static float StageSize(IRigScriptHost host, string stage) =>
            (float)host.EvaluateNumber($"{ShrubCatalog.RigGlobalName}.STAGES[{Js(stage)}]");

        static void AssertStage(IRigScriptHost host, string stage)
        {
            string g = ShrubCatalog.RigGlobalName;
            if (!host.EvaluateBool($"typeof {g}.STAGES[{Js(stage)}] === 'number'"))
                throw new ArgumentException(
                    $"{g} declares no stage '{stage}'. Known: " +
                    string.Join(", ", ReadStageKeys(host)) + ".");
        }

        static void AssertPhase(IRigScriptHost host, string phase)
        {
            string g = ShrubCatalog.RigGlobalName;
            if (!host.EvaluateBool($"{g}.PHASE_KEYS.indexOf({Js(phase)}) >= 0"))
                throw new ArgumentException(
                    $"{g} declares no phase '{phase}'. Known: " +
                    string.Join(", ", ReadPhaseKeys(host)) + ".");
        }

        /// <summary>The rig's own <c>sheetSpec()</c> for a species at a stage on an axis. The sheet we
        /// lay out is the sheet the rig describes: <see cref="Cols"/> across by
        /// <see cref="SwayRows"/> down.</summary>
        public readonly struct SheetSpec
        {
            public readonly int CellW, CellH, PivotX, PivotY, Pad, Cols;
            /// <summary>The rig's live <c>rows</c> — its <c>SWAY</c> frame count, and the number of
            /// rows this bake lays out. Read, never assumed; cross-checked against
            /// <see cref="SwayRowsBaked"/> in <see cref="ReadSheetSpec"/>.</summary>
            public readonly int SwayRows;
            /// <summary>What the rig reports for the whole sheet, and its 2048 verdict.</summary>
            public readonly int RigSheetW, RigSheetH;
            public readonly bool RigFits, Wrap;
            public readonly float Metres;

            public SheetSpec(int cellW, int cellH, int pivotX, int pivotY, int pad, int cols,
                             int swayRows, int rigSheetW, int rigSheetH, bool rigFits, bool wrap,
                             float metres)
            {
                CellW = cellW; CellH = cellH; PivotX = pivotX; PivotY = pivotY; Pad = pad;
                Cols = cols; SwayRows = swayRows;
                RigSheetW = rigSheetW; RigSheetH = rigSheetH; RigFits = rigFits; Wrap = wrap;
                Metres = metres;
            }

            public int SheetW => Cols * CellW;
            public int SheetH => SwayRows * CellH;
        }

        public static SheetSpec ReadSheetSpec(IRigScriptHost host, string species, string stage,
                                              string axis)
        {
            string g = ShrubCatalog.RigGlobalName;
            string axisArg = axis == PhaseAxis ? ",'phase'" : "";
            string sp = $"{g}.sheetSpec({Js(species)}, {g}.STAGES[{Js(stage)}]{axisArg})";

            int cellH = (int)host.EvaluateNumber($"{sp}.cell[1]");
            int pivotY = (int)host.EvaluateNumber($"{sp}.pivot[1]");
            int pad = (int)host.EvaluateNumber($"{sp}.pad");
            int swayRows = (int)host.EvaluateNumber($"{sp}.rows");

            // The rig defines pad = cellH − 1 − pivotY. Cross-check rather than trust: if the two ever
            // disagree, the pivot convention changed under us and every sheet this bake writes would
            // be planted wrong, with nothing downstream to notice.
            if (pad != cellH - 1 - pivotY)
                throw new InvalidOperationException(
                    $"{species}/{stage}: the rig reports pad {pad} but cellH−1−pivotY is " +
                    $"{cellH - 1 - pivotY}. The pivot convention changed — stop and read cellOf() " +
                    "before baking.");

            // 🔴 The row count is READ, and then cross-checked — the same discipline as the pad above,
            // and for a sharper reason. A baker that laid out fewer rows than the rig publishes writes
            // a sheet whose own contract rejects it, and the failure surfaces a whole stage later as
            // "Sliced 0, FAILED 18" rather than here. See SwayRowsBaked.
            if (swayRows != SwayRowsBaked)
                throw new InvalidOperationException(
                    $"{species}/{stage}: the rig publishes {swayRows} sway row(s) but this baker " +
                    $"expects {SwayRowsBaked}. The contract is serialised straight out of the rig and " +
                    "cannot record a bake with fewer rows, so a sheet laid out to the other number is " +
                    "one the slicer will refuse. Re-export the contract and update SwayRowsBaked " +
                    "together, deliberately.");

            return new SheetSpec(
                cellW: (int)host.EvaluateNumber($"{sp}.cell[0]"),
                cellH: cellH,
                pivotX: (int)host.EvaluateNumber($"{sp}.pivot[0]"),
                pivotY: pivotY,
                pad: pad,
                cols: (int)host.EvaluateNumber($"{sp}.cols"),
                swayRows: swayRows,
                rigSheetW: (int)host.EvaluateNumber($"{sp}.w"),
                rigSheetH: (int)host.EvaluateNumber($"{sp}.h"),
                rigFits: host.EvaluateBool($"{sp}.fits"),
                wrap: host.EvaluateBool($"{sp}.wrap"),
                metres: (float)host.EvaluateNumber($"{sp}.metres"));
        }

        /// <summary>
        /// The 2048 guard, asserted TWICE and never inferred: once via the rig's own
        /// <c>sheetSpec().fits</c> and once on the sheet we actually lay out. The two now describe the
        /// same sheet, which is the point — while this baker wrote one row the second check was
        /// measuring a sheet a quarter the height of the one the contract promised, and could not have
        /// caught anything the first missed. Over the cap Unity imports SILENTLY DOWNSCALED with a
        /// matching sprite count, so only a pivot or cell-size assert much later would catch it.
        /// </summary>
        public static void AssertFits(string species, string stage, in SheetSpec spec)
        {
            if (!spec.RigFits)
                throw new InvalidOperationException(
                    $"{species}/{stage}: the rig's own sheetSpec().fits is FALSE — its " +
                    $"{spec.RigSheetW}×{spec.RigSheetH} sheet is over Unity's " +
                    $"{ShrubCatalog.ImportSizeCap} px cap. The recipe outgrew the importer: page the " +
                    "sheet or bake a smaller stage, deliberately rather than past a refusal.");

            if (spec.SheetW > ShrubCatalog.ImportSizeCap || spec.SheetH > ShrubCatalog.ImportSizeCap)
                throw new InvalidOperationException(
                    $"{species}/{stage} would bake to {spec.SheetW}×{spec.SheetH}, over the " +
                    $"{ShrubCatalog.ImportSizeCap} px default import cap. Unity would import it " +
                    "DOWNSCALED with the sprite COUNT still matching, so this would only surface as a " +
                    "cell-size failure much later. Page the sheet before raising the limit.");
        }

        /// <summary>
        /// The rig's live geometry vs the committed contract. The cell is UNIONED over 4 variants × 8
        /// phases, so a drift here moves the ground-contact pivot of every phase of the species at
        /// once. <b>This refuses; it does not rewrite.</b> Regenerating the contract is
        /// <see cref="ExportContract"/>'s deliberate job.
        /// </summary>
        public static void AssertMatchesContract(string species, in SheetSpec spec, string axis,
                                                 ShrubCatalog.Contract contract)
        {
            var entry = contract.Find(species);
            if (entry == null)
                throw new InvalidOperationException(
                    $"The rig declares '{species}' but {ShrubCatalog.ContractFileName} does not. " +
                    "Re-export the contract from the rig rather than baking a species the engine " +
                    "cannot place.");

            if (entry.CellW != spec.CellW || entry.CellH != spec.CellH)
                throw new InvalidOperationException(
                    $"{species}: the rig's union cell is {spec.CellW}×{spec.CellH} but the contract " +
                    $"says {entry.CellW}×{entry.CellH}. The cell and the pivot move together — " +
                    "re-export the contract from the rig rather than baking sheets that disagree " +
                    "with it.");

            if (entry.PivotX != spec.PivotX || entry.PivotY != spec.PivotY)
                throw new InvalidOperationException(
                    $"{species}: the rig's root-crown pivot is ({spec.PivotX},{spec.PivotY}) but the " +
                    $"contract says ({entry.PivotX},{entry.PivotY}). Every phase of this species " +
                    "would be planted wrong by the difference.");

            int expectedW = axis == PhaseAxis ? entry.PhaseSheetW : entry.VariantSheetW;
            if (spec.SheetW != expectedW)
                throw new InvalidOperationException(
                    $"{species}: the rig lays out {spec.Cols}×{spec.CellW} = {spec.SheetW} px on the " +
                    $"{axis}-axis sheet but the contract says {expectedW}. The column count changed — " +
                    "re-export the contract.");

            // 🔴 THE HEIGHT, AND THIS IS THE CHECK THAT WAS MISSING. Only the width was compared, so
            // this guard was blind to the one axis the baker got wrong: it waved through sheets a
            // quarter of their contracted height for as long as their columns lined up, and the
            // refusal landed on ShrubSheetSlicer instead — a stage later, in the consumer's lap,
            // reading as a slicer problem. Both dimensions or neither.
            int expectedH = axis == PhaseAxis ? entry.PhaseSheetH : entry.VariantSheetH;
            if (spec.SheetH != expectedH)
                throw new InvalidOperationException(
                    $"{species}: the rig lays out {spec.SwayRows} sway row(s) × {spec.CellH} = " +
                    $"{spec.SheetH} px down the {axis}-axis sheet but the contract says {expectedH} " +
                    $"({expectedH / spec.CellH} row(s)). A sheet of the wrong height is one the " +
                    "slicer refuses — it matches neither published axis, and every rect it would cut " +
                    "is numbered against rows that are not there.");
        }

        // =====================================================================================
        // the pixels
        // =====================================================================================

        /// <summary>One <c>render()</c> call, as a JS expression. <paramref name="frame"/> is the sway
        /// frame and therefore the sheet ROW — 0..<see cref="SwayRowsBaked"/>−1. <b>No <c>snow</c>
        /// argument:</b> snow only ever CLIPS, so it is outside the union cell by construction and a
        /// snow-laden sheet is a separate, deliberate bake rather than an axis of this one.</summary>
        public static string ResultExpr(string species, string stage, string phase, int variant,
                                        int frame) =>
            $"{ShrubCatalog.RigGlobalName}.render({Js(species)},{{variant:{variant}," +
            $"phase:{Js(phase)},frame:{frame},stage:{Js(stage)}}})";

        static void RenderCell(IRigScriptHost host, string expr, in SheetSpec spec,
                               Stopwatch renderClock, ShrubBakeResult result,
                               Dictionary<ShrubCatalog.Channel, byte[][][]> cells, int row, int col)
        {
            string g = ShrubCatalog.RigGlobalName;
            renderClock.Start();

            // ONE render, three reads. Parking it on a global is what keeps the three channels
            // co-registered: three separate render() calls would be three subtly different geometries
            // if anything in the rig were ever non-deterministic.
            host.Execute($"globalThis.{ResultGlobal} = {expr};");
            byte[] rgba = host.EvaluateBytes($"{ResultGlobal}.rgba");
            byte[] light = host.EvaluateBytes($"{g}.packMask({ResultGlobal})");
            byte[] state = host.EvaluateBytes($"{g}.packState({ResultGlobal})");

            renderClock.Stop();
            result.CellsRendered++;

            int expected = spec.CellW * spec.CellH * 4;
            foreach (var (name, buf) in new[]
                     { ("rgba", rgba), ("packMask", light), ("packState", state) })
                if (buf.Length != expected)
                    throw new InvalidOperationException(
                        $"`{name}` came back {buf.Length} bytes, expected {expected} for " +
                        $"{spec.CellW}×{spec.CellH} RGBA. The cell the rig rendered does not match " +
                        "the cell sheetSpec() promised — do not blit it into a grid.");

            cells[ShrubCatalog.Channel.Albedo][row][col] = rgba;
            cells[ShrubCatalog.Channel.Light][row][col] = light;
            cells[ShrubCatalog.Channel.State][row][col] = state;
        }

        /// <summary>
        /// Lays the cells out axis-across × sway-down and writes the PNG. <c>RigBaker.Blit</c> does
        /// the cell copy, so the row/col convention (row-major from the TOP-LEFT, matching every
        /// slicer in the repo) exists in exactly one place.
        /// </summary>
        static string WriteSheet(string outputFolder, string stem, ShrubCatalog.Channel channel,
                                 in SheetSpec spec, byte[][][] cells, ShrubBakeResult result)
        {
            int pw = spec.SheetW, ph = spec.SheetH;
            var pixels = new Color32[pw * ph];
            for (int r = 0; r < spec.SwayRows; r++)
            for (int c = 0; c < spec.Cols; c++)
                RigBaker.Blit(cells[r][c], spec.CellW, spec.CellH, pixels, pw, ph,
                              col: c, rowFromTop: r);

            string assetPath = $"{outputFolder}/{stem}{ShrubCatalog.SuffixFor(channel)}.png";

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
            return assetPath;
        }

        // =====================================================================================
        // the budget, so the atlas call is made on numbers
        // =====================================================================================

        /// <summary>
        /// The rig's own <c>sheetAudit()</c> as a printable table — per species: union cell, worst ink
        /// ratio and the phase that caused it, both sheet sizes, headroom under the cap and KiB.
        ///
        /// <para>This exists so the owner's atlas decision is made on arithmetic rather than on a
        /// guess, the pattern #312 established. The union-cell policy trades sheet AREA for pivot
        /// STABILITY, and the worst-ink column is exactly what that trade costs.</para>
        /// </summary>
        public static string SummariseBudget(IRigScriptHost host, string stage = DefaultStage)
        {
            string g = ShrubCatalog.RigGlobalName;
            float size = StageSize(host, stage);
            string a = $"{g}.sheetAudit({size.ToString("R", Inv)})";

            var sb = new StringBuilder();
            sb.AppendLine($"[shrub-baker] sheet budget at stage '{stage}' (size {size}):");
            sb.AppendLine("  species              hab     unit   cell      worst ink       " +
                          "variant sheet  phase sheet   head   KiB");

            int rows = (int)host.EvaluateNumber($"{a}.rows.length");
            for (int i = 0; i < rows; i++)
            {
                string r = $"{a}.rows[{i}]";
                sb.AppendLine(string.Format(
                    "  {0,-20} {1,-7} {2,-6} {3,3}×{4,-4}  {5,3}% ({6,-7})  {7,4}×{8,-4}     " +
                    "{9,4}×{10,-4}    {11,4}  {12,5}",
                    host.EvaluateString($"{r}.key"),
                    host.EvaluateString($"{r}.hab"),
                    host.EvaluateString($"{r}.unit"),
                    (int)host.EvaluateNumber($"{r}.cell[0]"),
                    (int)host.EvaluateNumber($"{r}.cell[1]"),
                    (int)host.EvaluateNumber($"{r}.worstInk"),
                    host.EvaluateString($"{r}.worstPhase"),
                    (int)host.EvaluateNumber($"{r}.variantSheet[0]"),
                    (int)host.EvaluateNumber($"{r}.variantSheet[1]"),
                    (int)host.EvaluateNumber($"{r}.phaseSheet[0]"),
                    (int)host.EvaluateNumber($"{r}.phaseSheet[1]"),
                    (int)host.EvaluateNumber($"{r}.headroom"),
                    (int)host.EvaluateNumber($"{r}.kb")));
            }

            double mb = host.EvaluateNumber($"{a}.mb");
            bool fits = host.EvaluateBool($"{a}.fits");
            int worstInk = (int)host.EvaluateNumber($"{a}.worstInk");
            string worstKey = host.EvaluateString($"{a}.worstInkSpecies");

            sb.AppendLine($"  ── {mb:F2} MiB for BOTH axes, ALBEDO ONLY — a full three-channel bake " +
                          $"is 3× = {mb * 3:F2} MiB.");
            sb.AppendLine($"  ── every sheet inside the {ShrubCatalog.ImportSizeCap} px cap: {fits}. " +
                          $"Worst ink is {worstKey} at {worstInk}% — what the union cell costs it.");
            return sb.ToString().TrimEnd();
        }

        // =====================================================================================
        // shared
        // =====================================================================================

        static readonly System.Globalization.CultureInfo Inv =
            System.Globalization.CultureInfo.InvariantCulture;

        /// <summary>Single-quoted JS string literal — species keys are plain identifiers, but escape
        /// defensively: a quote in a name must not become an injection.</summary>
        static string Js(string s) => FishingKitBaker.Js(s);

        /// <summary>A JS string array, read through <c>JSON.stringify</c> so the host only has to
        /// return one scalar.</summary>
        static IReadOnlyList<string> ReadStringArray(IRigScriptHost host, string arrayExpr)
        {
            var outp = new List<string>();
            int n = (int)host.EvaluateNumber($"({arrayExpr}).length");
            for (int i = 0; i < n; i++)
                outp.Add(host.EvaluateString($"({arrayExpr})[{i}]"));
            return outp;
        }
    }
}
