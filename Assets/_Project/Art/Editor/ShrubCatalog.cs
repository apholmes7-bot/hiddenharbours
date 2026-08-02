#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace HiddenHarbours.Art.Editor
{
    /// <summary>
    /// The PLACEMENT CONTRACT for the Acadian Shrub kit — the schema of
    /// <c>Assets/_Project/Art/Foliage/Shrubs/shrubIsoRig.contract.json</c> and the one place anything
    /// downstream reads a species' habitat, union cell, root-crown pivot, sheet dimensions, the
    /// phenology calendar, the snow law or the material rules.
    ///
    /// <para><b>The gap this kit fills.</b> The tree rig owns anything with a leader,
    /// <see cref="ShorePlantCatalog"/> owns anything the tide reaches, the flower rig owns anything
    /// herbaceous. Everything woody, waist-high and multi-stemmed — the alder swale, the blueberry
    /// barren, the dogwood on the creek bank — was hand-drawn per season, which is why the same alder
    /// had a summer sprite twice the width of its winter one. Bayberry and Sweet Fern are deliberately
    /// NOT here: <c>shorePlantRig</c> owns both as dune/upland units, and a species living in two rigs
    /// is how two silhouettes happen.</para>
    ///
    /// <para><b>⭐ THIS KIT SHIPS NO PIXELS, AND THAT IS THE DESIGN</b> — the call the Rock Iso (#312)
    /// and Shore Plant (#318) kits already make. 20 species × 8 phases × 5 snow steps × 4 variants ×
    /// 3 growth stages is a matrix an import has no business choosing from, so the rig is the
    /// deliverable and sheets bake to order (<c>ShrubBaker</c>, menu <i>Hidden Harbours ▸ Art ▸ Bake
    /// Shrub Sheets</i>). The contract ships anyway because <b>the cell and the pivot are phase- and
    /// snow-independent by construction</b> (see <see cref="SheetPolicy.CellPolicy"/>), so one entry
    /// serves every state of a species and the engine can be wired against it before a PNG exists.</para>
    ///
    /// <para><b>⚠ PHENOLOGY, NOT SEASON — eight states, not three.</b> A shrub's identity lives in the
    /// FLOWER and the FRUIT, not the leaf: at 32 px/m a blueberry leaf is 1 px and a rhodora leaf is
    /// 1 px and they are the same 1 px. What tells them apart is that one is magenta in late May
    /// before it has any leaves at all and the other is scarlet in October. Winterberry is invisible
    /// for eleven months and then it is the loudest thing in the swamp. Two things fall out of the ONE
    /// (phase, species) table: <see cref="SpeciesEntry.BloomFirst"/> species flower on naked wood, and
    /// <see cref="SpeciesEntry.HoldsFruit"/> species carry fruit through the dormant frame — which is
    /// the whole reason to draw them.</para>
    ///
    /// <para><b>⚠ SNOW CLIPS, IT NEVER FLOODS.</b> A shrub is short enough for winter to ERASE it:
    /// 0.35 m of pack takes lowbush blueberry off the map and does nothing at all to a 4 m alder. One
    /// number — <c>depth = step.m × habitat.snowK</c> — decides four things (how many rows are clipped,
    /// whether a crust sits on the twigs, how much sway is left, and whether the species is legal to
    /// place at that depth), the way the tide does in the shore rig. At
    /// <see cref="SnowLaw.BuriedCutoff"/> the answer is <b>do not draw the shrub, draw a drift</b>. No
    /// sprite bakes ground — the same ruling as the rock rig's <c>awash</c> sheets.</para>
    ///
    /// <para><b>⚠ THE VEIL FLAG IS A DECLARATION, AND A CONSUMING SHADER MUST BRANCH ON IT FROM
    /// DATA.</b> A leafless alder is two hundred twigs 1 px wide, so bare twig volume is its own
    /// material (<see cref="Materials.Veil"/>): exempt from the body mass floor and, in exchange,
    /// forbidden BOTH a rim and a keyline — an outline round every strand collapses it into a solid.
    /// That is why <c>light.G</c> (back rim) is defined for BODY AND WOOD ONLY and is identically 0 on
    /// veil, fleck and bloom. <c>state.R</c> is 255 on veil pixels and is the branch:
    /// <b>gate every read of <c>light.G</c> on it, and never infer veil-vs-mass from the sprite.</b></para>
    ///
    /// <para><b>No water and no moving light are baked</b> (ADR 0010/0012/0023) — the live shader owns
    /// both, and the sun never goes behind (the tree-light rule). Pivot normalisation is ADR 0026's
    /// <c>(x/W, (H − y)/H)</c>, <b>not</b> the tree kit's <c>pad/cellH</c>: the tree's fraction has to
    /// double as the wind shader's <c>_TrunkAnchor</c> and a shrub's does not, so this kit follows the
    /// repo default the way <see cref="ShorePlantCatalog"/> does.</para>
    /// </summary>
    public static class ShrubCatalog
    {
        /// <summary>
        /// Where the sheets bake to, and where the contract lives beside them. A SIBLING of
        /// <c>Foliage/Trees/</c> and not under <c>Sprites/Shore/</c>: a shrub is foliage, and only
        /// three of the five habitats are anywhere near water. Outside
        /// <c>FoliageSheetSlicer.FlowersRoot</c> too, so no other slicer sees these sheets.
        /// </summary>
        public const string ShrubsRoot = "Assets/_Project/Art/Foliage/Shrubs/";

        public const string ContractFileName = "shrubIsoRig.contract.json";

        public static string ContractPath => ShrubsRoot + ContractFileName;

        /// <summary>The rig this kit renders from — the art-director's file, read-only from here.</summary>
        public const string RigScriptPath = "docs/art/rigs/shrubIsoRig.js";

        public const string RigGlobalName = "Shrubs";

        /// <summary>What the contract calls itself, pinned so a different rig cannot land here
        /// unnoticed.</summary>
        public const string RigName = "shrubIsoRig";

        /// <summary>32 px = 1 m, the locked scale (ADR 0006/0022).</summary>
        public const int Ppu = 32;

        /// <summary>
        /// 🔴 Over this Unity imports SILENTLY DOWNSCALED with the sprite COUNT still matching, so
        /// only a cell-size or pivot assert much later would catch it. The union-cell policy trades
        /// sheet area for pivot stability, which makes this cap the one number that can turn that
        /// trade into a silent bug — hence <c>ShrubBaker.AssertFits</c> and
        /// <see cref="SpeciesEntry.Fits2048"/> on every species.
        /// </summary>
        public const int ImportSizeCap = 2048;

        // ---- the axes, in the contract's own order ------------------------------------------------

        /// <summary>
        /// The twenty species, in the rig's own contract order (grouped by habitat: barren, bog,
        /// swale, edge, woods).
        ///
        /// <para>⚠️ <b>These are the rig's KEYS, not its display names, and two of them differ.</b>
        /// "Wild Raspberry" is <c>Raspberry</c> and "Winterberry" is <c>WinterberryHolly</c> — reading
        /// the README's species table and camel-casing the labels produces two keys the rig has never
        /// heard of, and the failure is silent right up until a sheet stem cannot be matched to a
        /// species. The list exists only as a CROSS-CHECK on the contract (a species silently vanishing
        /// must fail loudly rather than shorten a list); the geometry always comes from the contract.</para>
        /// </summary>
        public static readonly string[] SpeciesKeys =
        {
            "LowbushBlueberry", "SheepLaurel", "Rhodora", "BlackHuckleberry", "CommonJuniper",
            "Leatherleaf", "SweetGale", "WinterberryHolly",
            "SpeckledAlder", "PussyWillow", "RedOsierDogwood", "Meadowsweet", "Steeplebush",
            "Raspberry", "WildRose", "StaghornSumac", "Serviceberry",
            "BeakedHazelnut", "WildRaisin", "RedElderberry",
        };

        /// <summary>
        /// The eight calendar stations, in year order. This is the kit's headline axis and it replaces
        /// the tree rig's three seasons: <c>dormant</c> Feb · <c>catkin</c> early Apr · <c>bloom</c>
        /// late May · <c>leaf</c> Jun · <c>green</c> Jul · <c>fruit</c> Aug · <c>turn</c> early Oct ·
        /// <c>bare</c> Nov.
        /// </summary>
        public static readonly string[] PhaseKeys =
        {
            "dormant", "catkin", "bloom", "leaf", "green", "fruit", "turn", "bare",
        };

        /// <summary>The five sampled snow steps, none → drift. The runtime axis is CONTINUOUS in
        /// metres; these are the states a sheet holds.</summary>
        public static readonly string[] SnowKeys = { "none", "dust", "pack", "deep", "drift" };

        /// <summary>The five habitats. Each carries a <c>snowK</c> multiplier — the same pack lies
        /// deeper in a swale than on a wind-scoured barren.</summary>
        public static readonly string[] HabitatKeys = { "barren", "woods", "edge", "bog", "swale" };

        /// <summary>Growth stages, small → full. Asserted against a live read of the rig's own
        /// <c>STAGES</c> in the bake tests rather than trusted here.</summary>
        public static readonly string[] StageKeys = { "young", "half", "full" };

        public static readonly IReadOnlyDictionary<string, float> StageSizes =
            new Dictionary<string, float> { { "young", 0.42f }, { "half", 0.70f }, { "full", 1.00f } };

        /// <summary>Sway frames per sheet — the ROWS of both sheet axes. Play 5–7 fps.</summary>
        public const int SwayFrames = 4;

        /// <summary>Variants per species — the COLUMNS of the variant-axis sheet.</summary>
        public const int Variants = 4;

        /// <summary>What one sprite IS. <c>plant</c> = one individual · <c>clump</c> = a hummock of
        /// many stems · <c>mat</c> = one square metre of cover, tiling on its wrap axis.</summary>
        public static readonly string[] UnitKeys = { "plant", "clump", "mat" };

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
            /// <summary>R veil flag · G ornament · B snow load · A coverage. DATA — sRGB OFF.</summary>
            State,
        }

        public static string SuffixFor(Channel c) =>
            c switch
            {
                Channel.Albedo => "",
                Channel.Light => "_light",
                Channel.State => "_calendar",
                _ => throw new ArgumentOutOfRangeException(nameof(c)),
            };

        /// <summary>🔴 Only the albedo is colour. The other two are NUMBERS — sRGB on either of them
        /// gamma-mangles the channels while the sprite still looks fine in the inspector, so nothing
        /// but a numeric assert catches it.</summary>
        public static bool IsColourChannel(Channel c) => c == Channel.Albedo;

        /// <summary>Every channel, in bake order.</summary>
        public static readonly Channel[] Channels =
            { Channel.Albedo, Channel.Light, Channel.State };

        // ---- sheet naming -------------------------------------------------------------------------

        /// <summary>
        /// The PHASE-axis sheet: eight calendar stations across × four sway frames down, for ONE
        /// variant. This is the gameplay sheet — the calendar advances and every shrub follows it, so
        /// this axis is what a placed shrub actually samples.
        /// <para>Named for the fixed dimension (<c>_v0</c>), never the axis, so the stem can never end
        /// in a phase key and collide with a channel suffix.</para>
        /// </summary>
        public static string PhaseSheetStem(string species, string stage, int variant)
        {
            RequireOneOf(SpeciesKeys, species, "species");
            RequireOneOf(StageKeys, stage, "stage");
            if (variant < 0 || variant >= Variants)
                throw new ArgumentOutOfRangeException(
                    nameof(variant), $"variant {variant} is outside 0..{Variants - 1}.");
            return $"{species}_{stage}_v{variant}";
        }

        /// <summary>
        /// The VARIANT-axis sheet: four variants across × four sway frames down, at ONE phase. Visual
        /// variety within a thicket at a fixed calendar station. Named for the fixed dimension
        /// (<c>_atLeaf</c>) for the same reason as above.
        /// </summary>
        public static string VariantSheetStem(string species, string stage, string phase)
        {
            RequireOneOf(SpeciesKeys, species, "species");
            RequireOneOf(StageKeys, stage, "stage");
            RequireOneOf(PhaseKeys, phase, "phase");
            return $"{species}_{stage}_at{char.ToUpperInvariant(phase[0])}{phase.Substring(1)}";
        }

        public static string SheetPath(string stem, Channel channel) =>
            ShrubsRoot + stem + SuffixFor(channel) + ".png";

        /// <summary>
        /// The species a baked stem belongs to, or null. Matches on the leading key and requires a
        /// separator, then prefers the LONGEST match — so <c>WildRaspberry</c> can never be claimed by
        /// <c>WildRose</c>'s prefix, and <c>SweetGale</c> never by a hypothetical <c>Sweet</c>.
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
        /// <c>Foo_v0_calendar</c> → State, and the sheet stem the suffix hangs off.
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
            public Calendar Calendar;
            public SnowLaw Snow;
            public SwaySpec Sway;
            public SheetPolicy Sheets;

            public readonly List<HabitatEntry> Habitats = new List<HabitatEntry>();
            public readonly List<SpeciesEntry> Species = new List<SpeciesEntry>();
            public string[] Asserts = Array.Empty<string>();
            public string[] NotInThisRig = Array.Empty<string>();

            public SpeciesEntry Find(string key) =>
                Species.FirstOrDefault(s => string.Equals(s.Key, key, StringComparison.Ordinal));

            public HabitatEntry Habitat(string key) =>
                Habitats.FirstOrDefault(h => string.Equals(h.Key, key, StringComparison.Ordinal));

            /// <summary>Every species declared a <c>wrap</c> tile — the thicket units whose emitters
            /// are modulo the tile width so a row butt-joins with no seam.</summary>
            public IEnumerable<SpeciesEntry> WrapUnits => Species.Where(s => s.Wrap);
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
        /// floor polices body radii and a filament or a berry has none. <see cref="CarriesRimRaw"/> is
        /// tri-state because wood is <c>"thickness-gated"</c>: the rim gate decides per pixel.
        ///
        /// <para><see cref="Keyline"/> is the field this kit adds over the shore plants': veil is the
        /// only material in the world forbidden an outline, because a keyline round every 1 px strand
        /// is mush.</para>
        /// </summary>
        public sealed class Material
        {
            public string Key, CarriesRimRaw, Note;
            public int Id;
            public bool Linear, Keyline;
            public int? MassFloorPx;

            /// <summary>True only for a material the contract says carries a rim unconditionally.</summary>
            public bool CarriesRim => CarriesRimRaw == "true";

            /// <summary>True when the contract forbids a rim outright — the exemption's other half,
            /// and what <c>report.veilRimLeak == 0</c> and <c>report.ornRimLeak == 0</c> measure.</summary>
            public bool RimForbidden => CarriesRimRaw == "false";

            public bool RimThicknessGated => CarriesRimRaw == "thickness-gated";
        }

        /// <summary>
        /// The six declared materials. Body obeys the mass floor; the other five are linear or
        /// sub-pixel and are exempt BY DECLARATION — which is what makes the exemption auditable
        /// rather than a fudge.
        /// </summary>
        public sealed class Materials
        {
            public Material Body, Wood, Edge, Veil, Fleck, Bloom;

            public IEnumerable<Material> All
            {
                get
                {
                    yield return Body; yield return Wood; yield return Edge;
                    yield return Veil; yield return Fleck; yield return Bloom;
                }
            }

            public Material ByKey(string key) =>
                All.FirstOrDefault(m => m != null && m.Key == key);
        }

        /// <summary>The per-channel semantics of one bake, as prose the contract publishes. Pinned by
        /// test rather than paraphrased: the consuming shader branches on these.</summary>
        public sealed class BakeChannels
        {
            public string File, R, G, B, A;
        }

        public sealed class PhaseEntry
        {
            public string Key, Label, When;
        }

        public sealed class Calendar
        {
            public readonly List<PhaseEntry> Phases = new List<PhaseEntry>();
            public string[] Derives = Array.Empty<string>();
            public string Note;
        }

        public sealed class SnowStep
        {
            public string Key;
            public float Metres;
        }

        /// <summary>
        /// The snow law. <see cref="NominalM"/> × a habitat's <c>snowK</c> is the depth a step means
        /// on that ground; <see cref="ClipsNotFloods"/> is the flag an importer asserts.
        /// </summary>
        public sealed class SnowLaw
        {
            public float NominalM;
            public bool ClipsNotFloods;
            public string Note, SnowRow;
            public string[] Derives = Array.Empty<string>();
            public readonly List<SnowStep> Steps = new List<SnowStep>();

            /// <summary>At or above this buried fraction the shrub is NOT drawn — the scene draws a
            /// drift instead. The rig's own <c>buried &gt;= 0.95</c> ruling.</summary>
            public const float BuriedCutoff = 0.95f;

            public SnowStep Step(string key) =>
                Steps.FirstOrDefault(s => string.Equals(s.Key, key, StringComparison.Ordinal));
        }

        public sealed class SwaySpec
        {
            public int Frames;
            public string Note;
        }

        public sealed class SheetPolicy
        {
            public string AxisA, AxisB, CellPolicy;
            public int Cap;
            public float TotalMb;
            public bool AllFit;
        }

        public sealed class HabitatEntry
        {
            public string Key, Name, Substrate;
            public float SnowK;
        }

        /// <summary>One species: what it is, and the geometry every consumer needs.</summary>
        public sealed class SpeciesEntry
        {
            public string Key, Name, Latin, Habitat, Form, Unit, Grain, WorstInkPhase;
            public float HeightM;
            public bool Evergreen, BloomFirst, HoldsFruit, Wrap, Fits2048;

            /// <summary>The UNION cell — unioned over 4 variants × 8 phases at this stage. Snow is
            /// NOT in the union: it only ever clips, so it cannot change the cell.</summary>
            public int CellW, CellH;

            /// <summary>Ground contact at the root crown, in cell px from the TOP-LEFT.</summary>
            public int PivotX, PivotY;

            public int VariantSheetW, VariantSheetH;
            public int PhaseSheetW, PhaseSheetH;

            /// <summary>Worst ink-to-cell ratio across the eight phases, as a percentage — what the
            /// union-cell policy COSTS this species, measured rather than assumed.</summary>
            public int WorstInkPct;

            /// <summary>Fewest enclosed sky holes across the phases (rule 2). A non-mat shrub with
            /// zero windows is a bush-shaped rock.</summary>
            public int MinWindows;

            /// <summary>Distance from the largest sheet axis to the 2048 cap.</summary>
            public int Headroom =>
                ImportSizeCap - Mathf.Max(Mathf.Max(VariantSheetW, VariantSheetH),
                                          Mathf.Max(PhaseSheetW, PhaseSheetH));

            /// <summary>Uncompressed RGBA for BOTH sheet axes, in KiB — the memory the union policy
            /// costs for this species, for ONE channel.</summary>
            public int SheetKb =>
                (VariantSheetW * VariantSheetH + PhaseSheetW * PhaseSheetH) * 4 / 1024;
        }

        // =================================================================================
        // reading it back
        // =================================================================================

        /// <summary>
        /// The committed contract, or null with a logged error. Nested and dictionary-shaped, so
        /// <see cref="JsonUtility"/> cannot read it — hence <see cref="MiniJson"/>, the same choice
        /// <see cref="ShorePlantCatalog"/> made.
        /// </summary>
        public static Contract Load()
        {
            if (!File.Exists(ContractPath))
            {
                Debug.LogError(
                    $"[ShrubCatalog] No contract at '{ContractPath}'. It ships with the kit — if " +
                    "this fired, the branch predates the shrub import. Regenerate it with " +
                    "Hidden Harbours ▸ Dev ▸ Export Shrub Contract; never hand-write it.");
                return null;
            }

            object root;
            try { root = MiniJson.Parse(File.ReadAllText(ContractPath)); }
            catch (FormatException e)
            {
                Debug.LogError($"[ShrubCatalog] '{ContractPath}' is not valid JSON: {e.Message}");
                return null;
            }

            if (!(root is Dictionary<string, object> d))
            {
                Debug.LogError($"[ShrubCatalog] '{ContractPath}' did not parse to an object.");
                return null;
            }

            var proj = MiniJson.Dict(d, "projection");
            var mats = MiniJson.Dict(d, "materials");
            var bakes = MiniJson.Dict(d, "bakes");
            var cal = MiniJson.Dict(d, "calendar");
            var snow = MiniJson.Dict(d, "snow");
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
                    Wood = ReadMaterial(mats, "wood"),
                    Edge = ReadMaterial(mats, "edge"),
                    Veil = ReadMaterial(mats, "veil"),
                    Fleck = ReadMaterial(mats, "fleck"),
                    Bloom = ReadMaterial(mats, "bloom"),
                },
                Light = ReadBake(MiniJson.Dict(bakes, "light")),
                State = ReadBake(MiniJson.Dict(bakes, "state")),
                Sway = new SwaySpec
                {
                    Frames = MiniJson.Int(sway, "frames"),
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
                NotInThisRig = StrList(d, "notInThisRig"),
            };

            c.Calendar = new Calendar
            {
                Derives = StrList(cal, "derives"),
                Note = Str(cal, "note"),
            };
            foreach (var p in (MiniJson.List(cal, "phases") ?? new List<object>())
                              .OfType<Dictionary<string, object>>())
                c.Calendar.Phases.Add(new PhaseEntry
                {
                    Key = Str(p, "key"), Label = Str(p, "label"), When = Str(p, "when"),
                });

            c.Snow = new SnowLaw
            {
                NominalM = MiniJson.Float(snow, "nominalM"),
                ClipsNotFloods = Bool(snow, "clipsNotFloods"),
                Note = Str(snow, "note"),
                SnowRow = Str(snow, "snowRow"),
                Derives = StrList(snow, "derives"),
            };
            foreach (var s in (MiniJson.List(snow, "steps") ?? new List<object>())
                              .OfType<Dictionary<string, object>>())
                c.Snow.Steps.Add(new SnowStep { Key = Str(s, "key"), Metres = MiniJson.Float(s, "m") });

            // The habitat table lives at the top level AND its snowK is echoed inside snow.habitatK.
            // Read the top-level one (it carries name + substrate too) and let the tests prove the two
            // agree, rather than picking one and hoping.
            foreach (var h in (MiniJson.List(d, "habitats") ?? new List<object>())
                              .OfType<Dictionary<string, object>>())
                c.Habitats.Add(new HabitatEntry
                {
                    Key = Str(h, "key"),
                    Name = Str(h, "name"),
                    SnowK = MiniJson.Float(h, "snowK"),
                    Substrate = Str(h, "substrate"),
                });

            // Species is a LIST, so read it in the contract's own order and check the key set
            // afterwards — a species silently dropped must fail loudly rather than shorten the list.
            foreach (var s in (MiniJson.List(d, "species") ?? new List<object>())
                              .OfType<Dictionary<string, object>>())
                c.Species.Add(ReadSpecies(s));

            var missing = SpeciesKeys.Where(k => c.Find(k) == null).ToArray();
            if (missing.Length > 0)
            {
                Debug.LogError(
                    $"[ShrubCatalog] '{ContractPath}' is missing {missing.Length} species: " +
                    string.Join(", ", missing) + ". Re-export the contract from the rig — do not " +
                    "hand-edit it.");
                return null;
            }

            var strangers = c.Species.Select(s => s.Key)
                                     .Where(k => !SpeciesKeys.Contains(k, StringComparer.Ordinal))
                                     .ToArray();
            if (strangers.Length > 0)
            {
                Debug.LogError(
                    $"[ShrubCatalog] '{ContractPath}' declares {strangers.Length} species this " +
                    "catalog does not know: " + string.Join(", ", strangers) + ". Add them to " +
                    "SpeciesKeys deliberately — a stranger must not be placed by accident.");
                return null;
            }

            return c;
        }

        static Material ReadMaterial(Dictionary<string, object> mats, string key)
        {
            var m = MiniJson.Dict(mats, key);
            if (m == null) return null;

            // carriesRim is a JSON union: true | false | "thickness-gated". Keep the RAW form so the
            // tri-state survives — collapsing it to a bool is how "thickness-gated" silently becomes
            // "no rim" and a stem stops being lit.
            string raw;
            if (m.TryGetValue("carriesRim", out object cr))
                raw = cr is bool b ? (b ? "true" : "false") : cr as string;
            else raw = null;

            // massFloorPx is null for every linear material, and MiniJson.Int would flatten that to 0
            // — which reads as "a floor of zero" rather than "no floor applies".
            int? floor = null;
            if (m.TryGetValue("massFloorPx", out object mf) && mf != null)
                floor = MiniJson.Int(m, "massFloorPx");

            return new Material
            {
                Key = key,
                Id = MiniJson.Int(m, "id"),
                Linear = Bool(m, "linear"),
                Keyline = Bool(m, "keyline"),
                CarriesRimRaw = raw,
                MassFloorPx = floor,
                Note = Str(m, "note"),
            };
        }

        static BakeChannels ReadBake(Dictionary<string, object> b) =>
            b == null ? null : new BakeChannels
            {
                File = Str(b, "file"),
                R = Str(b, "R"), G = Str(b, "G"), B = Str(b, "B"), A = Str(b, "A"),
            };

        static SpeciesEntry ReadSpecies(Dictionary<string, object> s)
        {
            int[] cell = Pair(s, "cell"), pivot = Pair(s, "pivot");
            int[] vs = Pair(s, "variantSheet"), ps = Pair(s, "phaseSheet");
            return new SpeciesEntry
            {
                Key = Str(s, "key"),
                Name = Str(s, "name"),
                Latin = Str(s, "latin"),
                Habitat = Str(s, "habitat"),
                Form = Str(s, "form"),
                Unit = Str(s, "unit"),
                Grain = Str(s, "grain"),
                HeightM = MiniJson.Float(s, "heightM"),
                Evergreen = Bool(s, "evergreen"),
                BloomFirst = Bool(s, "bloomFirst"),
                HoldsFruit = Bool(s, "holdsFruit"),
                Wrap = Bool(s, "wrap"),
                CellW = cell[0], CellH = cell[1],
                PivotX = pivot[0], PivotY = pivot[1],
                VariantSheetW = vs[0], VariantSheetH = vs[1],
                PhaseSheetW = ps[0], PhaseSheetH = ps[1],
                WorstInkPct = MiniJson.Int(s, "worstInkPct"),
                WorstInkPhase = Str(s, "worstInkPhase"),
                MinWindows = MiniJson.Int(s, "minWindows"),
                Fits2048 = Bool(s, "fits2048"),
            };
        }

        // =================================================================================
        // geometry helpers
        // =================================================================================

        /// <summary>
        /// The Unity sprite pivot: normalised, BOTTOM-origin, ADR 0026's <c>(x/W, (H − y)/H)</c>.
        ///
        /// <para>⚠️ <b>NOT the tree kit's <c>pad/cellH</c>.</b> The two differ by exactly one row. The
        /// tree uses <c>pad/cellH</c> because that same fraction also has to serve as the wind
        /// shader's <c>_TrunkAnchor</c>; a shrub has no such second consumer, so this kit follows the
        /// repo default — the same call <see cref="ShorePlantCatalog"/> made and for the same reason.
        /// The rig does compute a <c>pad</c> internally; the contract publishes only the pivot.</para>
        /// </summary>
        public static Vector2 NormalizedPivot(SpeciesEntry e) =>
            e == null || e.CellW <= 0 || e.CellH <= 0
                ? new Vector2(0.5f, 0f)
                : new Vector2((float)e.PivotX / e.CellW, (float)(e.CellH - e.PivotY) / e.CellH);

        /// <summary>The pivot in cell px from the rect's own bottom-left, which is what
        /// <c>Sprite.pivot</c> reports.</summary>
        public static Vector2 PivotPixels(SpeciesEntry e) =>
            e == null ? Vector2.zero : new Vector2(e.PivotX, e.CellH - e.PivotY);

        /// <summary>
        /// The depth of snow a step means on a species' OWN ground:
        /// <c>step.m × habitat.snowK</c>. The one number that decides clipped rows, crust, sway
        /// damping and legality — read it from here rather than re-deriving it per consumer.
        /// </summary>
        public static float SnowDepthMetres(Contract contract, string speciesKey, string snowStep)
        {
            SpeciesEntry sp = contract?.Find(speciesKey);
            SnowStep step = contract?.Snow?.Step(snowStep);
            HabitatEntry hab = sp == null ? null : contract.Habitat(sp.Habitat);
            if (sp == null || step == null || hab == null) return 0f;
            return step.Metres * hab.SnowK;
        }

        /// <summary>
        /// The buried fraction of a species at a snow step — depth over its own standing height.
        /// At or above <see cref="SnowLaw.BuriedCutoff"/> <b>do not draw the shrub, draw a drift</b>.
        /// </summary>
        public static float BuriedFraction(Contract contract, string speciesKey, string snowStep)
        {
            SpeciesEntry sp = contract?.Find(speciesKey);
            if (sp == null || sp.HeightM <= 0f) return 0f;
            return SnowDepthMetres(contract, speciesKey, snowStep) / sp.HeightM;
        }

        /// <summary>Whether a species may legally be PLACED at a snow step, or whether the scene owes
        /// a drift tile there instead.</summary>
        public static bool IsLegalAtSnow(Contract contract, string speciesKey, string snowStep) =>
            BuriedFraction(contract, speciesKey, snowStep) < SnowLaw.BuriedCutoff;

        /// <summary>Total uncompressed RGBA for one channel across every species, both axes, in MiB —
        /// the atlas-budget number the owner decides the bake recipe on.</summary>
        public static float TotalSheetMib(Contract contract) =>
            contract?.Species == null ? 0f
                : contract.Species.Sum(s => s.SheetKb) / 1024f;

        // =================================================================================
        // shared
        // =================================================================================

        static void RequireOneOf(string[] allowed, string value, string what)
        {
            if (!allowed.Contains(value, StringComparer.Ordinal))
                throw new ArgumentException(
                    $"'{value}' is not a known {what}. Known: {string.Join(", ", allowed)}.", what);
        }

        static string Str(Dictionary<string, object> node, string key) =>
            node != null && node.TryGetValue(key, out object v) ? v as string : null;

        static bool Bool(Dictionary<string, object> node, string key) =>
            node != null && node.TryGetValue(key, out object v) && v is bool b && b;

        static string[] StrList(Dictionary<string, object> node, string key) =>
            (MiniJson.List(node, key) ?? new List<object>()).OfType<string>().ToArray();

        /// <summary>A two-number JSON array (<c>cell</c>, <c>pivot</c>, the sheet dims). Returns
        /// zeroes rather than throwing; the caller's key-set check reports the real problem.</summary>
        static int[] Pair(Dictionary<string, object> node, string key)
        {
            var list = MiniJson.List(node, key);
            if (list == null || list.Count < 2) return new[] { 0, 0 };
            return new[] { ToInt(list[0]), ToInt(list[1]) };
        }

        static int ToInt(object v) =>
            v is double d ? (int)Math.Round(d)
            : v is long l ? (int)l
            : v is int i ? i
            : 0;
    }
}
#endif
