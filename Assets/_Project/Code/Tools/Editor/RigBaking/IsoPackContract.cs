using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace HiddenHarbours.Tools.RigBaking
{
    /// <summary>
    /// The committed measurement of one ISO-rig-pack family, and the ONLY thing its baker is allowed
    /// to believe about cell sizes, pivots and packing.
    ///
    /// <para><b>The contract is the oracle; a baker validates against it and refuses rather than
    /// rewrites.</b> Same arrangement the shore-plant and shrub kits use. If a render disagrees with
    /// the numbers here, that is a finding — the rig changed, or the baker's rule is wrong — and it
    /// must surface as a failed bake, never as a quietly different sheet.</para>
    ///
    /// <para><b>⚠️ The cell RULE is not the same for every family</b>, which is the trap this
    /// class exists to keep out of every caller. Each contract states its own in
    /// <c>projection.cellRule</c>, and <see cref="Rule"/> resolves it to a <see cref="CellRule"/>:</para>
    /// <list type="bullet">
    ///   <item><b>wharfIso · shipyardIso</b> — union of the rig's RETURNED BUFFER extents. Measuring
    ///         the ink bbox instead gives <c>floatSet</c> 751×556 against the committed 757×592, and
    ///         then every one of the 17 keys disagrees. The rig sizes its own buffer per bake and
    ///         reports a FRACTIONAL <c>px,py</c> with it.</item>
    ///   <item><b>wharfDecor · utilityIso · the deck-loop kit's five</b> — pivot-INCLUSIVE union of the
    ///         INK bbox on the fixed native buffer, seeded at the pivot. The seeding is load-bearing
    ///         for exactly one piece in the repo (<c>fireCabinet</c>, wall-hung, ink stops 6 px above
    ///         its own pivot); for the other 84 the ink spans the pivot and the seeding changes
    ///         nothing — which is why <c>pivotInsideInk</c> is recorded per piece rather than assumed.</item>
    ///   <item><b>shoreFinds</b> — <c>cellOf(key)</c> verbatim. ANALYTIC: computed from the find's form
    ///         parameters, never rasterised, so no renderer change can move it — the keyline gate
    ///         included.</item>
    /// </list>
    ///
    /// <para><b>⚠️ A shared cell rule is not a shared TURNTABLE.</b> The deck-loop kit takes the
    /// wharfDecor/utilityIso rule and turns the OPPOSITE way — it is the repo's first
    /// measured-clockwise family, and it even shares those two families' −46.75°/step screen figure.
    /// <see cref="AssertConventionAgrees"/> is what holds the contract's measurement and the catalog's
    /// declaration together, because getting it wrong mirrors every cell of every piece silently.</para>
    ///
    /// <para><b>One contract file may hold several families.</b> The pack's own each own a file; the
    /// deck-loop kit's five share one, addressed by <see cref="Registration.Section"/>. Everything
    /// downstream sees one flat family either way.</para>
    ///
    /// <para><b>Sizing import headroom: read <see cref="WorstSheetByMaxDim"/>, never
    /// <see cref="WorstSheet"/>.</b> A texture cap binds on the LONGEST SIDE; <c>worstSheet</c> is the
    /// worst by AREA, and the two disagree in two of the four families. Over the cap a sheet does not
    /// fail — it imports SILENTLY DOWNSCALED with the sprite count still coming out right, which is
    /// the failure the whole packing discipline exists to avoid.</para>
    /// </summary>
    public sealed class IsoPackContract
    {
        /// <summary>How a family's committed cell was measured. See the class docs for why it differs.</summary>
        public enum CellRule
        {
            /// <summary>wharfIso — union of the rig's returned buffer extents, floor/ceil.</summary>
            BufferUnion,
            /// <summary>wharfDecor / utilityIso — pivot-inclusive ink union on a fixed native buffer.</summary>
            InkUnionFixed,
            /// <summary>shoreFinds — <c>cellOf(key)</c> verbatim, analytic.</summary>
            Analytic,
        }

        // ---- the committed shape, as JsonUtility sees it -------------------------------------------
        // JsonUtility reads only the fields named here and ignores the rest, so a contract may carry
        // documentation keys (footprintW, zone, rarity …) without this class listing them.

        [Serializable]
        public sealed class SheetPlan
        {
            public int cols, rows, sheetW, sheetH;
        }

        [Serializable]
        public sealed class Cell
        {
            public string key;
            public int cellW, cellH, pivotX, pivotY;

            /// <summary>
            /// Present on the fixed-sheet families only. <c>false</c> means the piece's ink does not
            /// span its own pivot, so a crop merely "tight to ink" would put its ground contact
            /// OUTSIDE its own cell. Assert it per piece rather than assuming it.
            /// <para>⚠️ Absent on wharfIso/shoreFinds, where JsonUtility defaults it to <c>false</c> —
            /// which is why <see cref="IsoPackContract.ChecksPivotInsideInk"/> gates the check on the
            /// rule rather than on the value.</para>
            /// </summary>
            public bool pivotInsideInk;

            public SheetPlan sheet;

            /// <summary>Present on <c>shipyardIso</c> only: the px/m THIS key bakes at — three sites
            /// cannot hold 8 facings under the 4096 cap at the kit's native 32, so the contract
            /// records each key's own scale and the baker passes it to <c>render()</c>. Absent
            /// (JsonUtility-defaulted to 0) on every other family = "use the rig's default".</summary>
            public int pxPerM;

            // ---- what the piece IS, as opposed to how it is packed -----------------------------
            // Declared here because JsonUtility drops any JSON field it has no field for, silently:
            // a consumer reading `zone` off an undeclared member gets null and scatters every find
            // into the same band. Each block is populated for the families that publish it and left
            // at its default for the others, which is why a reader must know what it is asking.

            /// <summary>Footprint in metres and drawn height — <c>wharfDecor</c> / <c>utilityIso</c>.
            /// The honest size of a prop: its cell is larger, because the ¾ camera draws the near half
            /// of the footprint and the whole of the height outside the ground box.</summary>
            public float footprintW, footprintD, heightM;

            /// <summary>Where a find belongs on the shore — <c>shoreFinds</c>: <c>tide</c> (below the
            /// wrack line), <c>wrack</c> (the line itself), <c>upper</c> (upper beach), <c>dune</c>.
            /// The kit bakes three STATES per find for exactly this axis.</summary>
            public string zone;

            /// <summary>How often a find turns up — <c>common</c> / <c>often</c> / <c>scarce</c> /
            /// <c>rare</c>. <c>shoreFinds</c>.</summary>
            public string rarity;

            /// <summary>The find's category (<c>shell</c>, <c>remains</c>, <c>wood</c>, …).</summary>
            public string cat;

            /// <summary>The pile cap: how many of this find read as a heap before the shape goes to
            /// mush. <c>shoreFinds</c>.</summary>
            public int stack;

            /// <summary>The find's TRUE size in centimetres, against which the drawn size is a
            /// deliberate compression (<c>drawn px = 4.7·cm^0.62</c>). <c>shoreFinds</c>.</summary>
            public float trueCm;

            /// <summary>How far a find's silhouette reaches beyond its cell, as a multiple.
            /// <c>shoreFinds</c>.</summary>
            public float reach;
        }

        [Serializable]
        public sealed class Projection
        {
            public int ppu;
            public double elev, depthScale, groundForeshorten;
            public bool antialias;
            public int nativeSheetW, nativeSheetH, nativePivotX, nativePivotY;
            public string pivot, keyline, cellRule;
            public bool keylineDefault;
        }

        [Serializable]
        public sealed class Azimuth
        {
            public string convention, measured, order;
        }

        [Serializable]
        public sealed class WorstSheetEntry
        {
            public string key;
            public int w, h, maxDim, headroomToCap;
        }

        [Serializable]
        private sealed class Dto
        {
            public string rig, generated, families;
            // shoreFinds is not directional and carries these instead of `facings`.
            public string lieAngles, states, sheetAxes;
            public int version, facings, importSizeCap, count, variants;
            public Projection projection;
            public Azimuth azimuth;
            public WorstSheetEntry worstSheet;
            public WorstSheetEntry worstSheetByMaxDim;
            public List<Cell> cells;
        }

        // ---- the loaded contract -------------------------------------------------------------------

        readonly Dto _dto;
        readonly Dictionary<string, Cell> _byKey;

        /// <summary>
        /// Project-relative path the contract was read from. <b>Always a real, openable path</b> —
        /// callers do re-read the file off it (<c>ShipyardIsoBakeTests</c> reads the per-key scales
        /// that this class's cell DTO does not carry). Use <see cref="SourceLabel"/> in messages.
        /// </summary>
        public string SourcePath { get; }

        /// <summary>The family's section in a shared KIT contract, or null when the file holds one
        /// family.</summary>
        public string Section { get; }

        /// <summary>What to print when naming this contract — the path, plus the section when one
        /// file holds several families. Never a path to open.</summary>
        public string SourceLabel =>
            string.IsNullOrEmpty(Section) ? SourcePath : $"{SourcePath}#{Section}";

        public string RigName => _dto.rig;
        public int Version => _dto.version;
        public Projection Proj => _dto.projection;
        public Azimuth Azi => _dto.azimuth;

        /// <summary>
        /// True for the three families that turn on the 8-facing turntable. <c>shoreFinds</c> is false:
        /// the finds lie FLAT on sand, so there is no facing and no <c>project()</c> at all — only a lie
        /// angle, rotated in the generator before projection, never a pixel rotate.
        /// </summary>
        public bool IsDirectional => Rule != CellRule.Analytic;

        /// <summary>
        /// Facings the family bakes. <b>Take it from HERE, never from
        /// <see cref="RigGeometry.NativeDirs"/>.</b> Three of the four rigs report 0 native facings:
        /// <c>ShoreFinds.DIRS</c> is a string array and <c>WharfDecor</c>/<c>UtilityIso</c> declare no
        /// <c>DIRS</c> global at all, so <c>RigCatalog.Install</c>'s <c>typeof DIRS === 'number'</c>
        /// test is false and reports 0 — silently, because 0 legitimately means "the rig does not say".
        ///
        /// <para>Throws for <c>shoreFinds</c> rather than returning its absent field as 0, so a caller
        /// that asks a non-directional family for facings finds out instead of baking one cell.</para>
        /// </summary>
        public int Facings => IsDirectional
            ? _dto.facings
            : throw new InvalidOperationException(
                  $"{RigName} is not directional — it declares no facings. It bakes " +
                  $"{LieAngleCount} lie angles × {Variants} variants per sheet, one sheet per find per " +
                  $"state ({string.Join(", ", StateNames)}). See sheetAxes in {SourceLabel}.");

        /// <summary>Lie angles the finds bake across, parsed from the contract's JSON-string field.</summary>
        public IReadOnlyList<string> LieAngles => ParseJsonStringArray(_dto.lieAngles);

        /// <summary>Weathering states the finds bake — one SHEET per find per state.</summary>
        public IReadOnlyList<string> StateNames => ParseJsonStringArray(_dto.states);

        public int LieAngleCount => LieAngles.Count;

        /// <summary>Silhouette variants per find. 3 — the cell is sized for the largest.</summary>
        public int Variants => _dto.variants;

        /// <summary>
        /// Cells on one packed sheet. <see cref="Facings"/> for the three directional families; for
        /// <c>shoreFinds</c> it is <see cref="LieAngleCount"/> × <see cref="Variants"/> = 24, because a
        /// finds sheet's axes are lie angle and variant rather than facing.
        /// </summary>
        public int CellsPerSheet => IsDirectional ? _dto.facings : LieAngleCount * Variants;

        /// <summary>Prose describing how a finds sheet is laid out.</summary>
        public string SheetAxes => _dto.sheetAxes;

        /// <summary>
        /// The contracts store array-valued documentation as JSON STRINGS (<c>"[\"N\",\"NE\",…]"</c>)
        /// rather than arrays, so JsonUtility can carry them. Parsed here rather than at each call site.
        /// </summary>
        static IReadOnlyList<string> ParseJsonStringArray(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return Array.Empty<string>();
            return json.Trim().TrimStart('[').TrimEnd(']')
                       .Split(',')
                       .Select(s => s.Trim().Trim('"'))
                       .Where(s => s.Length > 0)
                       .ToArray();
        }

        /// <summary>
        /// The cap this family's sheets must import at, and therefore the cap the pack must be chosen
        /// against. Handed to <c>ChooseGrid</c> so the bake and the import cannot disagree.
        /// </summary>
        public int ImportSizeCap => _dto.importSizeCap;

        /// <summary>Worst sheet by AREA. <b>Not</b> the field to size import headroom off.</summary>
        public WorstSheetEntry WorstSheet => _dto.worstSheet;

        /// <summary>Worst sheet by LONGEST SIDE — the measure a texture cap actually binds on.</summary>
        public WorstSheetEntry WorstSheetByMaxDim => _dto.worstSheetByMaxDim;

        public IReadOnlyList<Cell> Cells => _dto.cells;
        public int Count => _dto.count;

        /// <summary>Keyline colour this family rings with, as the rig spells it.</summary>
        public string Keyline => _dto.projection?.keyline;

        /// <summary>
        /// Whether the contract was measured with the ring ON. While the art-director's
        /// <c>KEYLINE_DEFAULT=false</c> gate is outstanding this is <c>true</c> for all four; once the
        /// gated rigs land it goes false for the families whose cells move — <b>wharfDecor and
        /// utilityIso only</b>. wharfIso's 17 cells and shoreFinds' 36 do not move, because the former
        /// measures a buffer the geometry sizes before the ring pass runs and the latter is analytic.
        /// </summary>
        public bool KeylineDefault => _dto.projection?.keylineDefault ?? true;

        /// <summary>Ground foreshorten, PER FAMILY. 0.72 for the finds against 0.6428 for the rest —
        /// carrying one across families is the un-squash mistake this repo keeps making.</summary>
        public double GroundForeshorten =>
            _dto.projection == null ? 0d
            : _dto.projection.groundForeshorten > 0 ? _dto.projection.groundForeshorten
            : _dto.projection.depthScale;

        IsoPackContract(Dto dto, string sourcePath, string section = null)
        {
            _dto = dto;
            SourcePath = sourcePath;
            Section = section;
            _byKey = dto.cells.ToDictionary(c => c.key, StringComparer.Ordinal);
        }

        // ---- loading -------------------------------------------------------------------------------

        /// <summary>
        /// Where one family's contract is committed, which part of that file is the family's, and
        /// where its sheets bake.
        ///
        /// <para><b><see cref="Section"/> is what makes a KIT contract addressable.</b> The pack's own
        /// families each own a whole file; the deck-loop kit is one file holding FIVE families, because
        /// they were measured together in one harness and share a projection, a turntable and a
        /// handedness. Registering the file five times without a section would hand every family the
        /// same cells.</para>
        ///
        /// <para><b><see cref="SheetFolder"/> is stated, not derived, for the same reason.</b> It
        /// defaults to the contract's own folder — true for every family whose contract sits beside its
        /// sheets — but the kit's five share a contract and must NOT share a sprite folder: one folder
        /// of 24 sheets makes every family's slice pass warn about the other four's PNGs, and a stem
        /// collision between two families would silently slice one against the other's grid.</para>
        /// </summary>
        public readonly struct Registration
        {
            /// <summary>Project-relative path to the committed contract.</summary>
            public readonly string ContractPath;

            /// <summary>The family's section in a multi-family kit contract, or null when the file
            /// holds exactly one family.</summary>
            public readonly string Section;

            /// <summary>Project-relative folder the family's sheets bake into.</summary>
            public readonly string SheetFolder;

            public Registration(string contractPath, string section = null, string sheetFolder = null)
            {
                ContractPath = contractPath;
                Section = section;
                int slash = contractPath.LastIndexOf('/');
                SheetFolder = !string.IsNullOrEmpty(sheetFolder) ? sheetFolder
                            : slash < 0 ? contractPath
                            : contractPath.Substring(0, slash);
            }

            public bool IsKitSection => !string.IsNullOrEmpty(Section);
        }

        const string DeckLoopKit = "Assets/_Project/Art/Sprites/DeckGear/deckLoopKit.contract.json";
        const string DeckLoopSheets = "Assets/_Project/Art/Sprites/DeckGear";

        /// <summary>
        /// Every family this loader covers, keyed by <see cref="RigCatalog"/> key. The catalog key is
        /// the key everywhere — a family whose contract section happens to be spelled differently
        /// (<c>fishTray2</c> → <c>tray</c>) is registered under the CATALOG's spelling, so one string
        /// addresses the rig, the contract and the sheets.
        /// </summary>
        public static readonly IReadOnlyDictionary<string, Registration> Registry =
            new Dictionary<string, Registration>(StringComparer.Ordinal)
            {
                ["wharfIso"]   = new Registration("Assets/_Project/Art/Sprites/Wharf/Iso/wharfIsoRig.contract.json"),
                ["wharfDecor"] = new Registration("Assets/_Project/Art/Sprites/Wharf/Decor/wharfDecorRig.contract.json"),
                ["utilityIso"] = new Registration("Assets/_Project/Art/Sprites/Utility/utilityIsoRig.contract.json"),
                ["shoreFinds"] = new Registration("Assets/_Project/Art/Sprites/Shore/FindsIso/shoreFindsRig.contract.json"),
                ["shipyardIso"] = new Registration("Assets/_Project/Art/Sprites/Shipyard/Iso/shipyardIsoRig.contract.json"),

                // ---- the deck-loop kit: ONE contract, five families, five sprite folders ----------
                // ⚠️ CLOCKWISE, unlike everything above it. The handedness is the contract's to state
                // and the catalog's to declare; AssertConventionAgrees holds the two together, because
                // applying the fleet's counter-clockwise correction here mirrors all eight cells of
                // every piece and nothing fails.
                ["deckGear"]  = new Registration(DeckLoopKit, "deckGear",  DeckLoopSheets + "/Gear"),
                ["trap"]      = new Registration(DeckLoopKit, "trap",      DeckLoopSheets + "/Traps"),
                ["fishTray2"] = new Registration(DeckLoopKit, "tray",      DeckLoopSheets + "/Trays"),
                ["buoyIso"]   = new Registration(DeckLoopKit, "buoy",      DeckLoopSheets + "/Buoys"),
                ["trapFauna"] = new Registration(DeckLoopKit, "trapFauna", DeckLoopSheets + "/Fauna"),
            };

        /// <summary>Where each family's contract is committed, keyed by <see cref="RigCatalog"/> key.
        /// The kit's five all name the SAME file — <see cref="Registration.Section"/> is what tells
        /// them apart.</summary>
        public static readonly IReadOnlyDictionary<string, string> Paths =
            Registry.ToDictionary(kv => kv.Key, kv => kv.Value.ContractPath, StringComparer.Ordinal);

        /// <summary>
        /// The families this loader covers, in the order their READMEs list them. The first four are
        /// the ISO rig pack proper; <c>shipyardIso</c> is a later kit that was MEASURED to ride the
        /// same turntable and share wharfIso's cell rule, and the deck-loop five are a kit that rides
        /// its OWN (clockwise) turntable but shares the wharfDecor/utilityIso cell rule — so all of
        /// them use this loader rather than another copy of it. <see cref="IsoPackBakeTests"/> pins the
        /// pack's own four in its own array and is deliberately not driven off this property.
        /// </summary>
        public static IEnumerable<string> Families => Registry.Keys;

        /// <summary>
        /// Where a family's sheets bake. <b>Ask here rather than deriving it from
        /// <see cref="Paths"/>.</b> The two agree for every family whose contract sits beside its
        /// sheets, and disagree for all five of the deck-loop kit's — whose contract sits one level
        /// above the folders it governs.
        /// </summary>
        public static string SheetFolderFor(string rigKey) => Get(rigKey).SheetFolder;

        static Registration Get(string rigKey) =>
            Registry.TryGetValue(rigKey, out var reg)
                ? reg
                : throw new ArgumentException(
                      $"'{rigKey}' is not an ISO-rig-pack family. Known: {string.Join(", ", Registry.Keys)}.");

        public static IsoPackContract Load(string rigKey)
        {
            var reg = Get(rigKey);
            string rel = reg.ContractPath;

            string abs = Path.Combine(RigCatalog.RepoRoot, rel);
            if (!File.Exists(abs))
                throw new FileNotFoundException(
                    $"Contract missing at {abs}. Every contract is committed beside where its " +
                    "family's sheets bake; a baker cannot proceed without its oracle.", abs);

            string json = File.ReadAllText(abs);
            var dto = reg.IsKitSection ? ReadKitSection(json, reg, rel) : JsonUtility.FromJson<Dto>(json);

            if (dto?.cells == null || dto.cells.Count == 0)
                throw new InvalidOperationException($"{rel} parsed but carries no cells.");
            if (dto.cells.Count != dto.count)
                throw new InvalidOperationException(
                    $"{rel} declares count={dto.count} but carries {dto.cells.Count} cells.");

            return new IsoPackContract(dto, rel, reg.Section);
        }

        // ---- kit contracts: one file, several families ----------------------------------------------

        /// <summary>
        /// A kit contract's per-family section. It carries its OWN native buffer and pivot — the
        /// single-family contracts put those on <c>projection</c>, but five families that share a
        /// projection do not share a buffer, so they are read from here and pushed onto the projection
        /// this loader hands downstream.
        /// </summary>
        [Serializable]
        sealed class KitFamilyDto
        {
            public string key, global, rig;
            public int facings, nativeW, nativeH, nativePivotX, nativePivotY, count;
            public WorstSheetEntry worstSheet, worstSheetByMaxDim;
            public List<Cell> cells;
        }

        /// <summary>
        /// ⚠️ A SEPARATE DTO, not a widened <see cref="Dto"/>. Two fields collide outright:
        /// <c>families</c> is a JSON string of family names on <c>wharfIso</c> and an ARRAY OF OBJECTS
        /// here, and <c>azimuth.order</c> is a JSON string there and a real array here. JsonUtility does
        /// not tolerate either mismatch, so the two shapes are parsed by two readers and only the
        /// resolved, flat shape travels onward.
        /// </summary>
        [Serializable]
        sealed class KitDto
        {
            public string kit, generated;
            public int version, importSizeCap;
            public Projection projection;
            public KitAzimuth azimuth;
            public List<KitFamilyDto> families;
        }

        [Serializable]
        sealed class KitAzimuth
        {
            public string convention, measured, warning;
            public List<string> order;
        }

        /// <summary>
        /// Project one family of a kit contract into the flat shape the rest of this class speaks.
        ///
        /// <para>The family's <c>rig</c> is spelled with its <c>.js</c> extension in a kit contract
        /// (<c>deckGearRig.js</c>) and without one in the single-family contracts
        /// (<c>wharfDecorRig</c>). It is normalised HERE, because <see cref="Rule"/> switches on it and
        /// a rig name that carries an extension falls through to the "no cell rule known" throw.</para>
        /// </summary>
        static Dto ReadKitSection(string json, in Registration reg, string rel)
        {
            var kit = JsonUtility.FromJson<KitDto>(json);
            if (kit?.families == null || kit.families.Count == 0)
                throw new InvalidOperationException(
                    $"{rel} is registered as a kit contract but parsed with no families section.");

            var fam = kit.families.FirstOrDefault(f => string.Equals(f.key, reg.Section, StringComparison.Ordinal))
                ?? throw new InvalidOperationException(
                    $"{rel} carries no family '{reg.Section}'. It has: " +
                    $"{string.Join(", ", kit.families.Select(f => f.key))}.");

            var proj = kit.projection ?? new Projection();
            // The buffer is per FAMILY; the projection they share says nothing about it. Pushed onto
            // the projection so IsoPropSheetBaker's native-sheet guard reads the same field for every
            // family it serves rather than growing a branch for the kit.
            proj.nativeSheetW = fam.nativeW;
            proj.nativeSheetH = fam.nativeH;
            proj.nativePivotX = fam.nativePivotX;
            proj.nativePivotY = fam.nativePivotY;

            return new Dto
            {
                rig = StripJs(fam.rig),
                generated = kit.generated,
                version = kit.version,
                facings = fam.facings,
                importSizeCap = kit.importSizeCap,
                count = fam.count,
                projection = proj,
                azimuth = new Azimuth
                {
                    convention = kit.azimuth?.convention,
                    measured = kit.azimuth?.measured,
                    order = kit.azimuth?.order == null
                        ? null
                        : "[" + string.Join(",", kit.azimuth.order.Select(o => $"\"{o}\"")) + "]",
                },
                worstSheet = fam.worstSheet,
                worstSheetByMaxDim = fam.worstSheetByMaxDim,
                cells = fam.cells,
            };
        }

        static string StripJs(string rig) =>
            string.IsNullOrEmpty(rig) || !rig.EndsWith(".js", StringComparison.Ordinal)
                ? rig
                : rig.Substring(0, rig.Length - 3);

        // ---- the rule ------------------------------------------------------------------------------

        /// <summary>
        /// Which measurement rule produced this family's cells. Resolved from the rig name rather than
        /// by parsing <c>projection.cellRule</c>, which is prose for humans — but the two are checked
        /// against each other by <see cref="AssertCellRuleRecorded"/> so the prose cannot drift.
        /// </summary>
        public CellRule Rule => RigName switch
        {
            "wharfIsoRig"   => CellRule.BufferUnion,
            "wharfDecorRig" => CellRule.InkUnionFixed,
            "utilityIsoRig" => CellRule.InkUnionFixed,
            "shoreFindsRig" => CellRule.Analytic,
            // Sites and parts both size themselves from the projected bbox and report px,py — the
            // same tight-cell rule as its measured turntable-sibling wharfIsoRig, not the fixed
            // sheet the two prop families use.
            "shipyardIsoRig" => CellRule.BufferUnion,

            // ---- the deck-loop kit ---------------------------------------------------------------
            // All five render into a FIXED nativeW × nativeH buffer at a fixed integer pivot and
            // return a bare RGBA array, so they take the wharfDecor/utilityIso rule — pivot-INCLUSIVE
            // ink union, seeded at the pivot. Taken from each contract's own projection.cellRule,
            // which states exactly that, and re-derived key-by-key by the kit's verifier: 24/24.
            //
            // ⚠️ Sharing a cell rule with the pack says NOTHING about sharing its handedness. This kit
            // turns the opposite way — see AssertConventionAgrees.
            "deckGearRig"  => CellRule.InkUnionFixed,
            "trapIsoRig"   => CellRule.InkUnionFixed,
            "trayIsoRig"   => CellRule.InkUnionFixed,
            "buoyIsoRig"   => CellRule.InkUnionFixed,
            "trapFaunaRig" => CellRule.InkUnionFixed,

            _ => throw new InvalidOperationException(
                     $"No cell rule known for rig '{RigName}'. A new family must state its rule here " +
                     "AND in its contract's projection.cellRule — the rule is per family and guessing " +
                     "it produces a cell that disagrees with the oracle on every key."),
        };

        /// <summary>True for the families whose contract records <c>pivotInsideInk</c> per piece.</summary>
        public bool ChecksPivotInsideInk => Rule == CellRule.InkUnionFixed;

        /// <summary>True when the rig must load with <c>InstallModule</c> — it exposes no pivot, so
        /// <c>RigCatalog.Install</c> would throw.</summary>
        public bool NeedsInstallModule => Rule != CellRule.InkUnionFixed;

        // ---- the asserts a baker runs before it writes a pixel --------------------------------------

        public Cell this[string key] =>
            _byKey.TryGetValue(key, out var c)
                ? c
                : throw new KeyNotFoundException(
                      $"'{key}' is not in {SourceLabel}. The contract carries {_byKey.Count} keys; a " +
                      "bake of something it has never measured has no oracle to validate against.");

        public bool TryGet(string key, out Cell cell) => _byKey.TryGetValue(key, out cell);

        /// <summary>
        /// THE assert. A rendered cell must match the committed one exactly — size and pivot both.
        ///
        /// <para>It refuses rather than adapting, and the message says which rule was applied, because
        /// the realistic failure is not a rig regression but a baker measuring the WRONG QUANTITY:
        /// ink where the contract measured the buffer, or a crop tight to ink where the contract
        /// seeded at the pivot. Both produce a plausible cell that is wrong on every key.</para>
        /// </summary>
        public void AssertMatchesContract(string key, int cellW, int cellH, int pivotX, int pivotY)
        {
            var c = this[key];
            if (c.cellW == cellW && c.cellH == cellH && c.pivotX == pivotX && c.pivotY == pivotY) return;

            throw new InvalidOperationException(
                $"CONTRACT MISMATCH on {RigName}.{key} — refusing to bake.\n" +
                $"  contract : {c.cellW}×{c.cellH}, pivot {c.pivotX},{c.pivotY}\n" +
                $"  rendered : {cellW}×{cellH}, pivot {pivotX},{pivotY}\n" +
                $"  rule     : {Rule} ({SourceLabel})\n\n" +
                "Check the RULE before suspecting the rig: the four families do not share one, and " +
                "measuring the wrong quantity fails on every key rather than one. If the rig genuinely " +
                "changed, regenerate the contract in the same commit — do not relax this check.\n\n" +
                "Expected, if the KEYLINE_DEFAULT gate has just landed: wharfDecor and utilityIso cells " +
                "shrink by 2×2 px (fireCabinet by 2×1). wharfIso and shoreFinds do NOT move. Regenerate " +
                "those two families' contracts with the gated rigs.");
        }

        /// <summary>
        /// Assert a piece's ink either does or does not span its own pivot, as the contract measured.
        /// No-op for the families that do not record it.
        /// </summary>
        public void AssertPivotInsideInk(string key, bool measured)
        {
            if (!ChecksPivotInsideInk) return;
            var c = this[key];
            if (c.pivotInsideInk == measured) return;

            throw new InvalidOperationException(
                $"pivotInsideInk MISMATCH on {RigName}.{key}: contract says {c.pivotInsideInk}, the " +
                $"render says {measured}. A piece whose ink stops short of its own pivot needs a " +
                "pivot-INCLUSIVE cell or its ground contact falls outside its own sprite — which does " +
                "not fail loudly, it just stands in the wrong place.");
        }

        /// <summary>
        /// The grid a sheet packs at, <b>read from the committed plan</b> rather than re-derived.
        ///
        /// <para><b>⚠️ Do NOT reach for <c>BuildingRigBaker.ChooseGrid</c> here.</b> ChooseGrid picks the
        /// WIDEST grid under the cap, and that is not the rule these contracts were packed with. The
        /// committed plans follow "the largest DIVISOR of the cell count that fits the cap" — which is
        /// also why no sheet in this pack has a ragged last row — and then <c>timberQuay</c> carries a
        /// ruled override on top of it. ChooseGrid reproduces only 10 of wharfIso's 17 plans and 31 of
        /// shoreFinds' 36.</para>
        ///
        /// <para><b>And the divergence is silent, because it is not an error.</b>
        /// <see cref="AssertSheetFits"/> used to check only <c>sheetW == cols*cellW</c> and the cap, both
        /// of which a differently-packed sheet satisfies. The worst case is exactly the one §2 of
        /// VERIFICATION.md was written to close: ChooseGrid packs <c>timberQuay</c> 8×1 = <b>4048 px</b>,
        /// undoing the coordinator's 4×2 repack, landing 48 px from the 4096 cap — and passing every
        /// check. Reading the plan is what makes that ruling stick.</para>
        /// </summary>
        public void GridFor(string key, out int cols, out int rows)
        {
            var c = this[key];
            if (c.sheet == null || c.sheet.cols <= 0 || c.sheet.rows <= 0)
                throw new InvalidOperationException(
                    $"{RigName}.{key} carries no sheet plan in {SourceLabel}. The plan is the oracle for " +
                    "packing as much as the cell is for size — a baker must not invent one.");

            if (c.sheet.cols * c.sheet.rows < CellsPerSheet)
                throw new InvalidOperationException(
                    $"{RigName}.{key} plans a {c.sheet.cols}×{c.sheet.rows} grid = " +
                    $"{c.sheet.cols * c.sheet.rows} slots, which cannot hold {CellsPerSheet} cells.");

            cols = c.sheet.cols;
            rows = c.sheet.rows;
        }

        /// <summary>The px/m THIS key bakes at, or 0 = "the rig's default". Non-zero only where the
        /// contract records it (<c>shipyardIso</c>: three sites cannot hold 8 facings under the cap at
        /// native scale). The baker passes a non-zero value into <c>render()</c>; a zero must NOT be
        /// passed — an explicit <c>pxPerM:0</c> is not the same as an absent option.</summary>
        public int PxPerMFor(string key) => this[key].pxPerM;

        /// <summary>
        /// Assert a packed sheet fits the cap this family imports at, and that the pack agrees with the
        /// committed plan. <paramref name="sheetW"/>/<paramref name="sheetH"/> are the real packed size.
        /// </summary>
        public void AssertSheetFits(string key, int cols, int rows, int sheetW, int sheetH)
        {
            var c = this[key];

            if (c.sheet != null && (cols != c.sheet.cols || rows != c.sheet.rows))
                throw new InvalidOperationException(
                    $"SHEET PLAN MISMATCH on {RigName}.{key}: packed {cols}×{rows}, contract plans " +
                    $"{c.sheet.cols}×{c.sheet.rows}.\n\n" +
                    "This does NOT fail on its own — a differently-packed sheet still satisfies the " +
                    "arithmetic and the cap, which is why the check is here. Pack from " +
                    $"{nameof(GridFor)}, not from a re-derived grid: ChooseGrid would put timberQuay " +
                    "back on one row at 4048 px and quietly undo its ruled 4×2 repack.");

            if (sheetW != cols * c.cellW || sheetH != rows * c.cellH)
                throw new InvalidOperationException(
                    $"PACK ARITHMETIC WRONG on {RigName}.{key}: {cols}×{rows} of {c.cellW}×{c.cellH} " +
                    $"is {cols * c.cellW}×{rows * c.cellH}, but the sheet came out {sheetW}×{sheetH}.");

            int maxDim = Mathf.Max(sheetW, sheetH);
            if (maxDim > ImportSizeCap)
                throw new InvalidOperationException(
                    $"SHEET OVER CAP on {RigName}.{key}: {sheetW}×{sheetH} has a longest side of " +
                    $"{maxDim} px against this family's {ImportSizeCap} px import cap.\n\n" +
                    "Over the cap a sheet does NOT fail to import — it imports silently downscaled, " +
                    "with the sprite count still correct, and every slice rect then lands wrong. " +
                    "Re-pack into more rows (ChooseGrid does this given the cap) rather than raising " +
                    "the cap: timberQuay went 8×1 → 4×2 for exactly this reason.");
        }

        /// <summary>
        /// Assert the contract's own headroom summary still holds across every key it lists.
        ///
        /// <para>This is the check that turns the import-downscale trap into a failing test rather than
        /// tribal knowledge. It reads <see cref="WorstSheetByMaxDim"/> — <see cref="WorstSheet"/> is the
        /// worst by AREA and names a different key in two of the four families, so sizing headroom off
        /// it reports slack that is not there.</para>
        /// </summary>
        public void AssertWorstSheetByMaxDimRecorded()
        {
            if (WorstSheetByMaxDim == null)
                throw new InvalidOperationException(
                    $"{SourceLabel} declares no worstSheetByMaxDim. Import headroom cannot be sized off " +
                    "worstSheet — that is the worst by AREA, and a texture cap binds on the longest side.");

            Cell worst = null;
            int worstMax = 0;
            foreach (var c in _dto.cells)
            {
                int m = Mathf.Max(c.sheet.sheetW, c.sheet.sheetH);
                if (m <= worstMax) continue;
                worstMax = m; worst = c;
            }

            var d = WorstSheetByMaxDim;
            if (worst == null || d.key != worst.key || d.maxDim != worstMax ||
                d.headroomToCap != ImportSizeCap - worstMax)
                throw new InvalidOperationException(
                    $"{SourceLabel}: worstSheetByMaxDim says {d.key} at {d.maxDim} px " +
                    $"({d.headroomToCap} px headroom), but the cells say " +
                    $"{worst?.key ?? "(none)"} at {worstMax} px " +
                    $"({ImportSizeCap - worstMax} px headroom). Regenerate it with the cells.");
        }

        /// <summary>
        /// Assert the contract states its cell rule in prose, and that the prose matches
        /// <see cref="Rule"/>. Cheap, and it keeps the human-readable note from drifting away from the
        /// code path a baker actually takes.
        /// </summary>
        public void AssertCellRuleRecorded()
        {
            string prose = Proj?.cellRule;
            if (string.IsNullOrWhiteSpace(prose))
                throw new InvalidOperationException(
                    $"{SourceLabel} states no projection.cellRule. The rule differs per family and was " +
                    "recovered by measurement — leaving it unwritten is how the next reader re-derives " +
                    "it, or worse, assumes a sibling's.");

            bool agrees = Rule switch
            {
                CellRule.BufferUnion   => prose.IndexOf("BUFFER", StringComparison.OrdinalIgnoreCase) >= 0,
                CellRule.InkUnionFixed => prose.IndexOf("INK", StringComparison.OrdinalIgnoreCase) >= 0 &&
                                          prose.IndexOf("INCLUSIVE", StringComparison.OrdinalIgnoreCase) >= 0,
                CellRule.Analytic      => prose.IndexOf("ANALYTIC", StringComparison.OrdinalIgnoreCase) >= 0,
                _ => false,
            };

            if (!agrees)
                throw new InvalidOperationException(
                    $"{SourceLabel}: projection.cellRule reads \"{prose}\" but this family bakes as " +
                    $"{Rule}. One of the two is stale.");
        }

        /// <summary>
        /// Assert the contract's measured handedness and the catalog's declared one are the same
        /// claim, before a bake turns that claim into a facing correction.
        ///
        /// <para><b>This is the check that stops a whole family shipping mirrored.</b>
        /// <c>RigBaker.DirForCell</c> applies a correction for CounterClockwise and none for
        /// Clockwise, so getting it wrong does not fail — it silently writes eight cells in the
        /// reverse order and every piece in the kit faces the wrong way. That mislabel has shipped
        /// defects in five separate kits.</para>
        ///
        /// <para>It matters here more than anywhere: the deck-loop kit is the first MEASURED-clockwise
        /// family in the repo, it shares its cell rule with two counter-clockwise siblings, and it
        /// shares its −46.75°/step SCREEN figure with them too — so every cheap signal says "same as
        /// the pack" and only the un-squashed ground-plane bearing says otherwise.</para>
        ///
        /// <para>No-op for a family whose contract records no convention at all: <c>shoreFinds</c>
        /// writes <c>"none"</c>, because the finds lie flat and have no facing to get backwards.</para>
        /// </summary>
        public void AssertConventionAgrees(AzimuthConvention declared, string rigKey)
        {
            string stated = Azi?.convention;
            if (string.IsNullOrWhiteSpace(stated) ||
                stated.Equals("none", StringComparison.OrdinalIgnoreCase)) return;

            bool agrees =
                stated.Equals(declared == AzimuthConvention.Clockwise ? "clockwise" : "counterClockwise",
                              StringComparison.OrdinalIgnoreCase);
            if (agrees) return;

            throw new InvalidOperationException(
                $"HANDEDNESS MISMATCH on {rigKey} — refusing to bake.\n" +
                $"  contract ({SourceLabel}) measured : {stated}\n" +
                $"  RigCatalog declares              : {declared}\n\n" +
                "One of the two is stale, and the bake cannot pick. A wrong answer here does NOT " +
                "fail: DirForCell simply writes the eight facings in the reverse order, and every " +
                "piece in the family then faces the wrong way with a correct-looking sheet.\n\n" +
                "Do not settle it from the rig's sign term or from a screen °/step figure — neither " +
                "is a handedness test. Re-measure the un-squashed GROUND-PLANE bearing against an " +
                "already-registered sibling (the kits' _verify.js do exactly this) and correct " +
                "whichever side the measurement contradicts.");
        }

        /// <summary>
        /// Refuse to bake art that still carries its 1 px keyline ring.
        ///
        /// <para>The owner ruled (2026-08-06) that all four pack rigs gain a
        /// <c>KEYLINE_DEFAULT = false</c> gate BEFORE anything bakes, because baking pre-gate art bakes
        /// the ring in and forces the re-bake this pack was explicitly steered away from. That ruling is
        /// a process gate everywhere else; here it is mechanical, so a bake cannot quietly land ringed
        /// sheets if the gate has not shipped.</para>
        ///
        /// <para><b>The ring is a PASS, not a colour.</b> Whoever implements the gate upstream must skip
        /// the pass and never filter by keyline colour: the art legitimately carries interior pixels at
        /// exactly that value with no transparent neighbour — <c>radioMast</c> alone has 551, in its
        /// lattice — and a colour match would punch holes straight through them.</para>
        ///
        /// <para>Lives here rather than on one baker so all three call the SAME gate. A second copy is
        /// the kind of thing that stays right until one of the two is touched.</para>
        /// </summary>
        public void AssertKeylineGated(IRigScriptHost host, string globalName, string rigKey)
        {
            if (host.EvaluateBool(
                    $"typeof {globalName} !== 'undefined' && " +
                    $"typeof {globalName}.KEYLINE_DEFAULT !== 'undefined'"))
                return;

            throw new InvalidOperationException(
                $"REFUSING TO BAKE {rigKey}: its rig source exposes no KEYLINE_DEFAULT gate, so every " +
                "sheet would bake with the 1 px ring in it.\n\n" +
                "The owner ruled on 2026-08-06 that the art director adds KEYLINE_DEFAULT = false to " +
                "all four ISO-pack rigs BEFORE anything bakes, precisely so this pack does not need the " +
                "re-bake that #444 did. docs/art/rigs/** is the art director's lane — do NOT edit the " +
                "rig here to get past this.\n\n" +
                "When the gated rigs land, regenerate the contracts for wharfDecor and utilityIso in " +
                "the same commit: their cells shrink 2×2 px (fireCabinet 2×1). wharfIso's 17 cells and " +
                "shoreFinds' 36 do NOT move — wharfIso measures a buffer the geometry sizes before the " +
                "ring pass runs, and shoreFinds' cellOf() is analytic.\n\n" +
                $"(This contract was measured with the ring {(KeylineDefault ? "ON" : "OFF")}.)");
        }

        /// <summary>Every standing check, for a caller that just wants the contract vetted.</summary>
        public void AssertSelfConsistent()
        {
            AssertCellRuleRecorded();
            AssertWorstSheetByMaxDimRecorded();

            foreach (var c in _dto.cells)
                AssertSheetFits(c.key, c.sheet.cols, c.sheet.rows, c.sheet.sheetW, c.sheet.sheetH);
        }

        /// <summary>
        /// ⚠️ Describes the sheet axes through <see cref="CellsPerSheet"/>, NOT through
        /// <see cref="Facings"/>. Facings deliberately THROWS for <c>shoreFinds</c>, and a
        /// <c>ToString</c> that throws takes out the log line, the debugger watch and — worst — the
        /// message of whatever exception was being reported when someone interpolated the contract
        /// into it.
        /// </summary>
        public override string ToString() =>
            $"{RigName} v{Version} — {Count} cells, " +
            (IsDirectional
                ? $"{CellsPerSheet} facings"
                : $"{LieAngleCount}×{Variants} = {CellsPerSheet} cells/sheet × {StateNames.Count} states") +
            $", rule {Rule}, cap {ImportSizeCap}" +
            (WorstSheetByMaxDim == null
                ? ""
                : $", worst {WorstSheetByMaxDim.key} {WorstSheetByMaxDim.maxDim} px " +
                  $"({WorstSheetByMaxDim.headroomToCap} px headroom)") +
            $" [{SourceLabel}]";
    }
}
