#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace HiddenHarbours.Art.Editor
{
    /// <summary>
    /// The PLACEMENT CONTRACT for the Shoreline Plant kit — the schema of
    /// <c>Assets/_Project/Art/Sprites/Shore/Plants/shorePlantRig.contract.json</c> and the one place
    /// anything downstream reads a species' zone, union cell, ground-contact pivot, sheet dimensions
    /// or material rules.
    ///
    /// <para><b>⭐ THIS KIT SHIPS NO PIXELS, AND THAT IS THE DESIGN</b> — the same call the Rock Iso
    /// kit makes (<see cref="RockIsoCatalog"/>). 16 species × 2 sheet axes × 3 seasons × 3 growth
    /// stages is a matrix an import has no business choosing from, so the rig is the deliverable and
    /// sheets bake to order (<c>ShorePlantBaker</c>, menu <i>Hidden Harbours ▸ Art ▸ Bake Shore Plant
    /// Sheets</i>). The contract ships anyway because <b>the cell and the pivot are tide-independent
    /// by construction</b> (see <see cref="CellPolicy"/>), so one entry serves all five tide states
    /// of a species and the engine can be wired against it before a single PNG exists.</para>
    ///
    /// <para><b>⚠ THE TIDE IS AN AXIS, NOT A VARIANT.</b> Water height over a plant's OWN ground
    /// (<c>tideM − zone.baseM</c>) drives lay, wetness, submergence colour and sway mode together, so
    /// a plant swaps sprite state constantly as the water rises. <see cref="Zones"/> is the
    /// staircase those states are resolved against; it maps straight onto the painted seabed height
    /// plus the deterministic tide, and adds no new simulation (rule 5).</para>
    ///
    /// <para><b>⭐ CELLS ARE UNIONED OVER THE TIDE STATES — one cell, one pivot per species per
    /// growth stage.</b> Per-tide cells would give every state its own pivot, and a 1 px disagreement
    /// becomes a visible hop the instant the tide crosses a threshold. Sheet waste is only memory; a
    /// state hop is an artifact. The cost is MEASURED, not assumed:
    /// <see cref="SpeciesEntry.WorstInkPct"/> carries the worst ink-to-cell ratio across the tide
    /// states and <see cref="SpeciesEntry.Headroom"/> the distance to the 2048 cap. Any per-species
    /// exception is decided on those numbers by the owner, never here.</para>
    ///
    /// <para><b>⚠ THE STRAP MATERIAL IS A DECLARATION, AND A CONSUMING SHADER MUST BRANCH ON IT
    /// FROM DATA.</b> A grass blade is 2 px wide, so blades, culms, fronds and sheets are declared
    /// <see cref="Materials.Strap"/>: exempt from the body mass floor and, in exchange, forbidden a
    /// rim. That is why <c>light.G</c> (back rim) is defined for BODY AND WOOD ONLY and is
    /// identically 0 on strap and fleck — it is <b>not</b> re-used as a spine channel, and no channel
    /// changes meaning per material. <c>state.B</c> is 255 on strap pixels and is the branch:
    /// <b>gate every read of <c>light.G</c> on it, never infer strap-vs-mass from the sprite.</b>
    /// The strap's lit spine is real N·L off its bowed cross-section normal, so it arrives in
    /// <c>light.R</c> with everything else.</para>
    ///
    /// <para><b>No water and no moving light are baked</b> (ADR 0010/0012/0023). Submergence bakes
    /// COLOUR only — darker, cooler, contrast falling monotonically with depth. There is no dapple
    /// and nothing per-frame, because the live sprite-light shader's caustics are swell-driven and
    /// two uncorrelated patterns on one frond is worse than none. <see cref="WaterLaw"/> carries the
    /// three flags an importer asserts.</para>
    ///
    /// <para>Pivot normalisation is ADR 0026's <c>(x/W, (H − y)/H)</c> — see
    /// <see cref="NormalizedPivot"/> for why this is not the tree kit's <c>pad/cellH</c> case even
    /// though this rig computes a <c>pad</c> internally.</para>
    /// </summary>
    public static class ShorePlantCatalog
    {
        /// <summary>Where the sheets bake to, and where the contract lives beside them.</summary>
        public const string PlantsRoot = "Assets/_Project/Art/Sprites/Shore/Plants/";

        public const string ContractFileName = "shorePlantRig.contract.json";

        public static string ContractPath => PlantsRoot + ContractFileName;

        /// <summary>The rig this kit renders from — the art-director's file, read-only from here.</summary>
        public const string RigScriptPath = "docs/art/rigs/shorePlantRig.js";

        public const string RigGlobalName = "ShorePlants";

        /// <summary>What the contract calls itself, pinned so a different rig cannot land here unnoticed.</summary>
        public const string RigName = "shorePlantRig";

        /// <summary>32 px = 1 m, the locked scale (ADR 0006/0022).</summary>
        public const int Ppu = 32;

        /// <summary>
        /// 🔴 Over this Unity imports SILENTLY DOWNSCALED with the sprite COUNT still matching, so
        /// only a cell-size or pivot assert much later would catch it. The union-cell policy trades
        /// sheet area for pivot stability, which makes this cap the one number that can turn that
        /// trade into a silent bug — hence <c>ShorePlantBaker.AssertFits</c> and
        /// <see cref="SpeciesEntry.Fits2048"/> on every species.
        /// </summary>
        public const int ImportSizeCap = 2048;

        // ---- the axes, in the contract's own order ------------------------------------------------

        /// <summary>The sixteen species, in contract order (fringe → mid → low → high → upland).</summary>
        public static readonly string[] SpeciesKeys =
        {
            "SugarKelp", "IrishMoss", "Eelgrass",
            "KnottedWrack", "Bladderwrack", "SeaLettuce",
            "Cordgrass", "Glasswort",
            "SaltmeadowHay", "BlackRush", "Cattail", "Threesquare",
            "MarramGrass", "Bayberry", "SweetFern", "BeachPea",
        };

        /// <summary>The five tide steps the sheets bake, low → high. The runtime axis is CONTINUOUS;
        /// these are the sampled states a sheet holds.</summary>
        public static readonly string[] TideKeys = { "low", "ebb", "half", "flood", "high" };

        /// <summary>The tidal staircase, in metres above chart datum.</summary>
        public static readonly string[] ZoneKeys = { "fringe", "mid", "low", "high", "upland" };

        public static readonly string[] SeasonKeys = { "summer", "autumn", "winter" };

        public static readonly string[] StageKeys = { "new", "grown", "full" };

        /// <summary>Growth stage → the rig's <c>size</c> scalar. The rig's own <c>STAGES</c> table;
        /// asserted against a live read in the bake tests rather than trusted here.</summary>
        public static readonly IReadOnlyDictionary<string, float> StageSizes =
            new Dictionary<string, float> { { "new", 0.36f }, { "grown", 0.68f }, { "full", 1.00f } };

        /// <summary>Sway frames per sheet — the ROWS of both sheet axes. Play 5–7 fps.</summary>
        public const int SwayFrames = 4;

        /// <summary>Variants per species — the COLUMNS of the variant-axis sheet.</summary>
        public const int Variants = 4;

        // ---- channels -----------------------------------------------------------------------------

        /// <summary>
        /// The three bakes a sheet stem produces. The two suffixes are the contract's own
        /// (<c>bakes.light.file</c>, <c>bakes.state.file</c>) and are not renamed here.
        /// </summary>
        public enum Channel
        {
            /// <summary>The composited sprite. COLOUR — sRGB stays ON.</summary>
            Albedo,
            /// <summary>R key · G rim (body+wood only) · B depth · A coverage. DATA — sRGB OFF.</summary>
            Light,
            /// <summary>R wet · G submerged · B strap flag · A coverage. DATA — sRGB OFF.</summary>
            State,
        }

        public static string SuffixFor(Channel c) =>
            c switch
            {
                Channel.Albedo => "",
                Channel.Light => "_light",
                Channel.State => "_tide",
                _ => throw new ArgumentOutOfRangeException(nameof(c)),
            };

        /// <summary>🔴 Only the albedo is colour. The other two are NUMBERS — sRGB on either of them
        /// gamma-mangles the channels while the sprite still looks fine, so nothing but a numeric
        /// assert catches it.</summary>
        public static bool IsColourChannel(Channel c) => c == Channel.Albedo;

        /// <summary>Every channel, in bake order.</summary>
        public static readonly Channel[] Channels =
            { Channel.Albedo, Channel.Light, Channel.State };

        // ---- sheet naming -------------------------------------------------------------------------

        /// <summary>
        /// The TIDE-axis sheet: five tide states across × four sway frames down, for ONE variant.
        /// This is the gameplay sheet — the tide moves continuously and every plant follows it, so
        /// this axis is what a placed plant actually samples.
        /// <para>Named for the fixed dimension (<c>_v0</c>), never the axis, so the stem can never
        /// end in a tide key and collide with the <c>_tide</c> state-channel suffix.</para>
        /// </summary>
        public static string TideSheetStem(string species, string season, string stage, int variant)
        {
            RequireOneOf(SpeciesKeys, species, "species");
            RequireOneOf(SeasonKeys, season, "season");
            RequireOneOf(StageKeys, stage, "stage");
            if (variant < 0 || variant >= Variants)
                throw new ArgumentOutOfRangeException(
                    nameof(variant), $"variant {variant} is outside 0..{Variants - 1}.");
            return $"{species}_{season}_{stage}_v{variant}";
        }

        /// <summary>
        /// The VARIANT-axis sheet: four variants across × four sway frames down, at ONE tide state.
        /// Visual variety within a bed at a fixed tide. Named for the fixed dimension
        /// (<c>_atLow</c>) for the same reason as above.
        /// </summary>
        public static string VariantSheetStem(string species, string season, string stage, string tide)
        {
            RequireOneOf(SpeciesKeys, species, "species");
            RequireOneOf(SeasonKeys, season, "season");
            RequireOneOf(StageKeys, stage, "stage");
            RequireOneOf(TideKeys, tide, "tide");
            return $"{species}_{season}_{stage}_at{char.ToUpperInvariant(tide[0])}{tide.Substring(1)}";
        }

        public static string SheetPath(string stem, Channel channel) =>
            PlantsRoot + stem + SuffixFor(channel) + ".png";

        /// <summary>
        /// The species a baked stem belongs to, or null. Matches on the leading key and requires a
        /// separator, so <c>SeaLettuce</c> can never be claimed by a hypothetical <c>Sea</c>.
        /// </summary>
        public static string SpeciesForStem(string stem)
        {
            if (string.IsNullOrEmpty(stem)) return null;
            return SpeciesKeys
                   .Where(k => stem.StartsWith(k, StringComparison.Ordinal) &&
                               stem.Length > k.Length && stem[k.Length] == '_')
                   .OrderByDescending(k => k.Length)
                   .FirstOrDefault();
        }

        /// <summary>
        /// The channel a file stem carries. <c>Foo_v0</c> → Albedo, <c>Foo_v0_light</c> → Light,
        /// <c>Foo_v0_tide</c> → State, and the sheet stem the suffix hangs off.
        /// </summary>
        public static Channel ChannelForFileStem(string fileStem, out string sheetStem)
        {
            foreach (Channel c in new[] { Channel.Light, Channel.State })
            {
                string suffix = SuffixFor(c);
                if (fileStem.EndsWith(suffix, StringComparison.Ordinal))
                {
                    sheetStem = fileStem.Substring(0, fileStem.Length - suffix.Length);
                    return c;
                }
            }
            sheetStem = fileStem;
            return Channel.Albedo;
        }

        // =================================================================================
        // the contract schema
        // =================================================================================

        public sealed class Contract
        {
            public string Rig;
            public int Version;
            public string Generated;

            public Projection Projection;
            public Materials Materials;
            public BakeChannels Light, State;
            public WaterLaw Water;
            public TideModel Tide;
            public SwaySpec Sway;
            public SheetPolicy Sheets;

            public readonly List<ZoneEntry> Zones = new List<ZoneEntry>();
            public readonly List<SpeciesEntry> Species = new List<SpeciesEntry>();
            public string[] Asserts = Array.Empty<string>();

            public SpeciesEntry Find(string key) =>
                Species.FirstOrDefault(s => string.Equals(s.Key, key, StringComparison.Ordinal));

            public ZoneEntry Zone(string key) =>
                Zones.FirstOrDefault(z => string.Equals(z.Key, key, StringComparison.Ordinal));
        }

        public sealed class Projection
        {
            public int Ppu;
            public float ElevDeg, HeightScale, DepthScale;
            public string Pivot, Alpha, Keyline;
            public bool Antialias;
        }

        /// <summary>
        /// One declared material. <see cref="MassFloorPx"/> is null for every LINEAR material — the
        /// floor polices body radii and a strap or a culm has none. <see cref="CarriesRim"/> is
        /// tri-state because wood is <c>"thickness-gated"</c>: the rim gate decides per pixel.
        /// </summary>
        public sealed class Material
        {
            public string Key, CarriesRimRaw, Note;
            public int Id;
            public bool Linear;
            public int? MassFloorPx;

            /// <summary>True only for a material the contract says carries a rim unconditionally.</summary>
            public bool CarriesRim => CarriesRimRaw == "true";

            /// <summary>True when the contract forbids a rim outright — the strap exemption's
            /// other half, and what <c>report.strapRimLeak == 0</c> measures.</summary>
            public bool RimForbidden => CarriesRimRaw == "false";

            public bool RimThicknessGated => CarriesRimRaw == "thickness-gated";
        }

        public sealed class Materials
        {
            public Material Body, Strap, Wood, Fleck;

            public IEnumerable<Material> All
            {
                get { yield return Body; yield return Strap; yield return Wood; yield return Fleck; }
            }

            public Material ByKey(string key) =>
                All.FirstOrDefault(m => m != null && m.Key == key);
        }

        /// <summary>The per-channel semantics of one bake, as prose the contract publishes. Pinned
        /// by test rather than paraphrased: the consuming shader branches on these.</summary>
        public sealed class BakeChannels
        {
            public string File, R, G, B, A;
        }

        public sealed class WaterLaw
        {
            public bool BakesWater, BakesCaustics, BakesMovingLight;
            public string Note;
        }

        public sealed class TideStep
        {
            public string Key;
            public float T, WaterM;
        }

        public sealed class TideModel
        {
            public float RangeM, DryFallM;
            public string Input, WaterRow;
            public string[] Derives = Array.Empty<string>();
            public readonly List<TideStep> Steps = new List<TideStep>();

            public TideStep Step(string key) =>
                Steps.FirstOrDefault(s => string.Equals(s.Key, key, StringComparison.Ordinal));
        }

        public sealed class ZoneEntry
        {
            public string Key, Name, Substrate;
            /// <summary>Metres above chart datum — the plant's own ground.</summary>
            public float BaseM;
        }

        public sealed class SwaySpec
        {
            public int Frames;
            public string[] Modes = Array.Empty<string>();
            public string Note;
        }

        public sealed class SheetPolicy
        {
            public string AxisA, AxisB, CellPolicy;
            public int Cap;
            public float TotalMb;
            public bool AllFit;
        }

        public sealed class SpeciesEntry
        {
            public string Key, Name, Latin, Zone, Unit, WorstInkTide;
            /// <summary>The zone's ground height in metres above chart datum.</summary>
            public float BaseM;
            /// <summary>TRUE standing world height in metres at full growth.</summary>
            public float HeightM;
            /// <summary>0 = self-supporting; &gt;0 = owes its height to the water and folds when drained.</summary>
            public float Limp;
            public bool Algae, Brackish, FreshTolerant;

            /// <summary>The UNION cell — one per species per growth stage, over 4 variants ×
            /// 2 seasons × 5 tides.</summary>
            public int CellW, CellH;
            /// <summary>Ground contact (holdfast / root crown) in cell px from the TOP-LEFT.</summary>
            public Vector2Int Pivot;

            public int VariantSheetW, VariantSheetH, TideSheetW, TideSheetH;

            /// <summary>Ink-to-cell ratio at the WORST tide state, as a percentage.</summary>
            public int WorstInkPct;
            public bool Fits2048;

            /// <summary>Cell area the union policy pays for and the worst tide state does not use.
            /// This is the number a per-tide-cell exception would be argued on.</summary>
            public int WorstWastePct => 100 - WorstInkPct;

            /// <summary>Distance from the largest sheet axis to the import cap.</summary>
            public int Headroom =>
                ImportSizeCap - Mathf.Max(Mathf.Max(VariantSheetW, VariantSheetH),
                                          Mathf.Max(TideSheetW, TideSheetH));

            /// <summary>Uncompressed RGBA for BOTH sheet axes, in KiB — the memory the union policy
            /// costs for this species.</summary>
            public int SheetKb =>
                (VariantSheetW * VariantSheetH + TideSheetW * TideSheetH) * 4 / 1024;
        }

        // =================================================================================
        // reading it back
        // =================================================================================

        /// <summary>
        /// The committed contract, or null with a logged error. Nested and dictionary-shaped, so
        /// <see cref="JsonUtility"/> cannot read it — hence <see cref="MiniJson"/>.
        /// </summary>
        public static Contract Load()
        {
            if (!File.Exists(ContractPath))
            {
                Debug.LogError(
                    $"[ShorePlantCatalog] No contract at '{ContractPath}'. It ships with the kit — " +
                    "if this fired, the branch predates the shore plant import.");
                return null;
            }

            object root;
            try { root = MiniJson.Parse(File.ReadAllText(ContractPath)); }
            catch (FormatException e)
            {
                Debug.LogError($"[ShorePlantCatalog] '{ContractPath}' is not valid JSON: {e.Message}");
                return null;
            }

            if (!(root is Dictionary<string, object> d))
            {
                Debug.LogError($"[ShorePlantCatalog] '{ContractPath}' did not parse to an object.");
                return null;
            }

            var proj = MiniJson.Dict(d, "projection");
            var mats = MiniJson.Dict(d, "materials");
            var bakes = MiniJson.Dict(d, "bakes");
            var water = MiniJson.Dict(d, "water");
            var tide = MiniJson.Dict(d, "tide");
            var sway = MiniJson.Dict(d, "sway");
            var sheets = MiniJson.Dict(d, "sheets");

            var c = new Contract
            {
                Rig = Str(d, "rig"),
                Version = MiniJson.Int(d, "version"),
                Generated = Str(d, "generated"),
                Projection = new Projection
                {
                    Ppu = MiniJson.Int(proj, "ppu"),
                    ElevDeg = MiniJson.Float(proj, "elev"),
                    HeightScale = MiniJson.Float(proj, "heightScale"),
                    DepthScale = MiniJson.Float(proj, "depthScale"),
                    Pivot = Str(proj, "pivot"),
                    Alpha = Str(proj, "alpha"),
                    Keyline = Str(proj, "keyline"),
                    Antialias = Bool(proj, "antialias"),
                },
                Materials = new Materials
                {
                    Body = ReadMaterial(mats, "body"),
                    Strap = ReadMaterial(mats, "strap"),
                    Wood = ReadMaterial(mats, "wood"),
                    Fleck = ReadMaterial(mats, "fleck"),
                },
                Light = ReadBake(MiniJson.Dict(bakes, "light")),
                State = ReadBake(MiniJson.Dict(bakes, "state")),
                Water = new WaterLaw
                {
                    BakesWater = Bool(water, "bakesWater"),
                    BakesCaustics = Bool(water, "bakesCaustics"),
                    BakesMovingLight = Bool(water, "bakesMovingLight"),
                    Note = Str(water, "note"),
                },
                Sway = new SwaySpec
                {
                    Frames = MiniJson.Int(sway, "frames"),
                    Modes = StrList(sway, "modes"),
                    Note = Str(sway, "note"),
                },
                Sheets = new SheetPolicy
                {
                    AxisA = Str(sheets, "axisA"),
                    AxisB = Str(sheets, "axisB"),
                    Cap = MiniJson.Int(sheets, "cap"),
                    CellPolicy = Str(sheets, "cellPolicy"),
                    TotalMb = MiniJson.Float(sheets, "totalMb"),
                    AllFit = Bool(sheets, "allFit"),
                },
                Asserts = StrList(d, "asserts"),
            };

            c.Tide = new TideModel
            {
                RangeM = MiniJson.Float(tide, "rangeM"),
                DryFallM = MiniJson.Float(tide, "dryFallM"),
                Input = Str(tide, "input"),
                WaterRow = Str(tide, "waterRow"),
                Derives = StrList(tide, "derives"),
            };
            foreach (var s in (MiniJson.List(tide, "steps") ?? new List<object>())
                              .OfType<Dictionary<string, object>>())
                c.Tide.Steps.Add(new TideStep
                {
                    Key = Str(s, "key"),
                    T = MiniJson.Float(s, "t"),
                    WaterM = MiniJson.Float(s, "waterM"),
                });

            foreach (var z in (MiniJson.List(d, "zones") ?? new List<object>())
                              .OfType<Dictionary<string, object>>())
                c.Zones.Add(new ZoneEntry
                {
                    Key = Str(z, "key"),
                    Name = Str(z, "name"),
                    BaseM = MiniJson.Float(z, "baseM"),
                    Substrate = Str(z, "substrate"),
                });

            // Species is a LIST here (unlike the rock contract's map), so read it in the contract's
            // own order and check the key set afterwards — a species silently dropped must fail
            // loudly rather than shorten the list.
            foreach (var s in (MiniJson.List(d, "species") ?? new List<object>())
                              .OfType<Dictionary<string, object>>())
                c.Species.Add(ReadSpecies(s));

            var missing = SpeciesKeys.Where(k => c.Find(k) == null).ToArray();
            if (missing.Length > 0)
            {
                Debug.LogError(
                    $"[ShorePlantCatalog] '{ContractPath}' is missing {missing.Length} species: " +
                    string.Join(", ", missing) + ". Re-export the contract from the rig — do not " +
                    "hand-edit it.");
                return null;
            }

            return c;
        }

        static Material ReadMaterial(Dictionary<string, object> mats, string key)
        {
            var m = MiniJson.Dict(mats, key);
            if (m == null) return null;

            // massFloorPx is deliberately null on every linear material; MiniJson.Int would fold that
            // into 0, which reads as "a floor of zero" instead of "no floor applies".
            int? floor = m.TryGetValue("massFloorPx", out object f) && f is double n
                ? (int)Math.Round(n)
                : (int?)null;

            // carriesRim is bool on three materials and the string "thickness-gated" on wood, so it
            // is kept raw and interpreted by the Material accessors.
            string rim = m.TryGetValue("carriesRim", out object r)
                ? (r is bool b ? (b ? "true" : "false") : r as string)
                : null;

            return new Material
            {
                Key = key,
                Id = MiniJson.Int(m, "id"),
                Linear = Bool(m, "linear"),
                MassFloorPx = floor,
                CarriesRimRaw = rim,
                Note = Str(m, "note"),
            };
        }

        static BakeChannels ReadBake(Dictionary<string, object> b) =>
            b == null ? null : new BakeChannels
            {
                File = Str(b, "file"), R = Str(b, "R"), G = Str(b, "G"),
                B = Str(b, "B"), A = Str(b, "A"),
            };

        static SpeciesEntry ReadSpecies(Dictionary<string, object> s)
        {
            int[] cell = IntPair(s, "cell");
            int[] pivot = IntPair(s, "pivot");
            int[] vs = IntPair(s, "variantSheet");
            int[] ts = IntPair(s, "tideSheet");

            return new SpeciesEntry
            {
                Key = Str(s, "key"),
                Name = Str(s, "name"),
                Latin = Str(s, "latin"),
                Zone = Str(s, "zone"),
                BaseM = MiniJson.Float(s, "baseM"),
                HeightM = MiniJson.Float(s, "heightM"),
                Unit = Str(s, "unit"),
                Limp = MiniJson.Float(s, "limp"),
                Algae = Bool(s, "algae"),
                Brackish = Bool(s, "brackish"),
                FreshTolerant = Bool(s, "freshTolerant"),
                CellW = cell[0], CellH = cell[1],
                Pivot = new Vector2Int(pivot[0], pivot[1]),
                VariantSheetW = vs[0], VariantSheetH = vs[1],
                TideSheetW = ts[0], TideSheetH = ts[1],
                WorstInkPct = MiniJson.Int(s, "worstInkPct"),
                WorstInkTide = Str(s, "worstInkTide"),
                Fits2048 = Bool(s, "fits2048"),
            };
        }

        // =================================================================================
        // derived placement numbers
        // =================================================================================

        /// <summary>
        /// The Unity sprite pivot: normalised, BOTTOM-origin, <c>(x/W, (H − y)/H)</c> — ADR 0026's
        /// shared formula.
        ///
        /// <para><b>⚠ This is NOT the tree kit's <c>pad/cellH</c> case, even though this rig computes
        /// a <c>pad</c> internally.</b> ADR 0026 / #300 settled the rule that resolves the two: read
        /// the quantity the contract itself PUBLISHES and use exactly that. The committed contract
        /// publishes <c>pivot</c> — a top-left-origin ground-contact coordinate — and no <c>pad</c>,
        /// exactly like the rock kit. The tree case is different because its contract publishes
        /// <c>nearFlarePad</c> and the wind shader consumes that same row as <c>_TrunkAnchor</c>;
        /// nothing here consumes a pad. <see cref="PadPx"/> exists only so a test can pin the
        /// relation and prove the two have not been confused.</para>
        /// </summary>
        public static Vector2 NormalizedPivot(SpeciesEntry s) =>
            new Vector2((float)s.Pivot.x / s.CellW, (s.CellH - s.Pivot.y) / (float)s.CellH);

        /// <summary>The pivot in cell px from the rect's own bottom-left — what <c>Sprite.pivot</c>
        /// reports.</summary>
        public static Vector2 PivotPixels(SpeciesEntry s) =>
            new Vector2(s.Pivot.x, s.CellH - s.Pivot.y);

        /// <summary>
        /// The rig's own <c>pad = cellH − 1 − pivotY</c>: rows of drape that project BELOW the ground
        /// contact. Documented and pinned, never used to normalise — see <see cref="NormalizedPivot"/>.
        /// </summary>
        public static int PadPx(SpeciesEntry s) => s.CellH - 1 - s.Pivot.y;

        /// <summary>That drape in metres at the locked PPU — how far below its own ground a plant
        /// draws, and therefore how far a bottom-of-cell pivot would float it.</summary>
        public static float DrapeMetres(SpeciesEntry s) => (s.CellH - s.Pivot.y) / (float)Ppu;

        /// <summary>
        /// The cell rect of one column × sway row within its sheet, in px from the sheet's TOP-LEFT.
        /// Both axes share this layout: the ACROSS axis is tide or variant, the DOWN axis is always
        /// sway.
        /// </summary>
        public static RectInt CellRect(SpeciesEntry s, int col, int swayRow)
        {
            if (swayRow < 0 || swayRow >= SwayFrames)
                throw new ArgumentOutOfRangeException(
                    nameof(swayRow), $"sway row {swayRow} is outside 0..{SwayFrames - 1}.");
            return new RectInt(col * s.CellW, swayRow * s.CellH, s.CellW, s.CellH);
        }

        /// <summary>
        /// Water standing over a species' OWN ground, in metres, at a tide of <paramref name="t"/>
        /// (0 = mean low water, 1 = mean high water). Negative means drained. This is the single
        /// input the rig resolves lay, wetness, submergence and sway mode from, restated here so
        /// placement code can pick a tide column without a V8 host.
        /// </summary>
        public static float WaterOverGroundM(Contract c, SpeciesEntry s, float t) =>
            Mathf.Clamp01(t) * c.Tide.RangeM - s.BaseM;

        /// <summary>
        /// The baked tide state nearest a continuous tide — which COLUMN of the tide-axis sheet a
        /// plant draws at time <paramref name="t"/>. Nearest-in-t rather than a floor, so a plant
        /// does not lag the water on the flood.
        /// </summary>
        public static string NearestTideKey(Contract c, float t)
        {
            float tt = Mathf.Clamp01(t);
            TideStep best = null;
            float bestD = float.MaxValue;
            foreach (var step in c.Tide.Steps)
            {
                float d = Mathf.Abs(step.T - tt);
                if (d < bestD) { bestD = d; best = step; }
            }
            return best?.Key;
        }

        public static int TideColumn(string tideKey) => Array.IndexOf(TideKeys, tideKey);

        // ---- shared helpers -------------------------------------------------------------------

        static string Str(Dictionary<string, object> node, string key) =>
            node != null && node.TryGetValue(key, out object v) ? v as string : null;

        static bool Bool(Dictionary<string, object> node, string key) =>
            node != null && node.TryGetValue(key, out object v) && v is bool b && b;

        static string[] StrList(Dictionary<string, object> node, string key) =>
            (MiniJson.List(node, key) ?? new List<object>()).OfType<string>().ToArray();

        /// <summary>A two-number JSON array (<c>cell</c>, <c>pivot</c>, the sheet dims). Returns
        /// zeroes rather than throwing; the caller's key-set check reports the real problem.</summary>
        static int[] IntPair(Dictionary<string, object> node, string key)
        {
            var list = MiniJson.List(node, key);
            if (list == null || list.Count < 2) return new[] { 0, 0 };
            return new[] { ToInt(list[0]), ToInt(list[1]) };
        }

        static int ToInt(object v) => v is double d ? (int)Math.Round(d) : 0;

        static string RequireOneOf(string[] table, string key, string what)
        {
            if (Array.IndexOf(table, key) < 0)
                throw new ArgumentOutOfRangeException(
                    nameof(key), $"'{key}' is not a known {what}. Known: {string.Join(", ", table)}.");
            return key;
        }
    }
}
#endif
