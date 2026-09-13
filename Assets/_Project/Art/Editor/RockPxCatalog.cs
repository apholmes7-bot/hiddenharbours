#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace HiddenHarbours.Art.Editor
{
    /// <summary>
    /// The PLACEMENT CONTRACT for the <b>Rock Px</b> kit (Rock Iso, pass three) — the schema of
    /// <c>Assets/_Project/Art/Sprites/Shore/RockPx/RockPx.json</c>, its sixteen per-form sidecars
    /// and its seat-decal index, and the one place anything downstream reads a rock's cell, pivot,
    /// collision footprint, perch, snags, hazard radius or tide-pool rect.
    ///
    /// <para><b>This kit SHIPS PIXELS.</b> Unlike pass two (<see cref="RockIsoCatalog"/>, which bakes
    /// to order and ships none), pass three arrives as 225 baked sheets × four co-registered
    /// channels = 900 PNGs, plus 28 seat decals × two channels. Pass two is NOT retired by this file
    /// and nothing here reads it: the two contracts sit side by side until a later PR rules on the
    /// changeover.</para>
    ///
    /// <para><b>⚠ The sheet is 8 cols × 3 rows and the last four cols are MIRRORS.</b> Cols 0–3 are
    /// variants A–D; cols 4–7 are the same four flipped about the cell's vertical centre. Rows are
    /// TIDE (dry / wet / awash) — not dress. Dress is a separate SHEET (<c>bare</c> unsuffixed, then
    /// <c>_barnacled</c>, <c>_weeded</c>), which is why the census is 75 (form × stone) pairs × 3
    /// dress = 225 sheets and not 75.</para>
    ///
    /// <para><b>⚠ The pivot is the BOTTOM-CENTRE GROUND CONTACT, never the bbox centre and never
    /// derived from alpha</b> — it is read from the sidecar's <c>anchors.footprint.ground</c>, which
    /// every variant in this drop also mirrors into <c>anchors.pivot</c>. Under the camera a rock's
    /// near flank is drawn BELOW its ground contact, so a bottom-centre-of-cell pivot would float
    /// every one of them by that overhang — measured <b>5–18 px = 0.16–0.56 m</b> across the drop,
    /// positive in every case (see <see cref="GroundOverhangPx"/>).</para>
    ///
    /// <para>The pivot normalisation is ADR 0026's: <c>anchors.footprint.ground</c> is a top-left
    /// origin cell coordinate and the contract publishes <b>no <c>pad</c></b>, so the y term is
    /// <c>(H − y)/H</c> — the same formula as <see cref="RockIsoCatalog.NormalizedPivot"/>, and NOT
    /// the tree kit's <c>pad/cellH</c> case.</para>
    ///
    /// <para><b>Four channels are co-registered, and two of them are DATA.</b> <c>_mask</c> and
    /// <c>_normal</c> (and the seat decals' <c>_blend</c>) carry numbers, not colour: they import
    /// linear with <c>alphaIsTransparency</c> off, exactly as the tree kit's data channels do
    /// (<see cref="TreeSheetSlicer.ApplyImportSettings"/>). Their alpha is the SAME coverage as the
    /// albedo's, measured pixel-for-pixel over all 225 sheets — <c>RockPxKitContractTests</c> pins
    /// EQUALITY rather than the tree kit's superset, because equality is what this drop
    /// measures.</para>
    /// </summary>
    public static class RockPxCatalog
    {
        // =================================================================================
        // where it lives
        // =================================================================================

        /// <summary>The kit's folder. Pass two lives in <c>Shore/Rock/</c> and is untouched.</summary>
        public const string KitRoot = "Assets/_Project/Art/Sprites/Shore/RockPx/";

        public const string ContractFileName = "RockPx.json";
        public const string TexDir = KitRoot + "tex/";
        public const string SidecarDir = KitRoot + "sidecar/";
        public const string SeatDir = KitRoot + "seat/";
        public const string SeatsFileName = "seats.json";

        public static string ContractPath => KitRoot + ContractFileName;
        public static string SeatsPath => SeatDir + SeatsFileName;

        /// <summary>The art-director's rig pair, read-only from here. The kit is baked FROM these two
        /// files and every JSON in it stamps <see cref="RigSha256"/>; landing the sheets without the
        /// rig would land 900 PNGs nothing can ever regenerate.</summary>
        public const string RigDir = "docs/art/rigs/rock-px-kit/";

        public const string RigScriptPath = RigDir + "pxRockIso2.js";
        public const string RigExportPath = RigDir + "_pxRock2Export.js";

        /// <summary>SHA-256 of <see cref="RigScriptPath"/> over its LF bytes — the digest every JSON
        /// in the drop carries as <c>derivedFromRigSha256</c>. <c>.gitattributes</c> pins the rig
        /// <c>text eol=lf</c> so this verifies identically on Windows and on CI.</summary>
        public const string RigSha256 =
            "3b6ec36d7891893c1b970859b5e9de4c744c6404540d3b888ab3a0119615d58c";

        /// <summary>What the export calls itself, pinned so a different kit cannot land here
        /// unnoticed.</summary>
        public const string ExportSymbol = "PxRockIso2";

        public const string KitName = "Rock Iso, pass three";

        // =================================================================================
        // the locked numbers
        // =================================================================================

        /// <summary>32 px = 1 m, the locked scale (ADR 0006/0022).</summary>
        public const int Ppu = 32;

        /// <summary>Over this Unity imports SILENTLY DOWNSCALED with the sprite COUNT still matching,
        /// so a slice would pass while every rect addressed the wrong texels. The widest sheet in the
        /// drop is <c>Spine</c> at 976×126 and the tallest is <c>Cloven</c> at 496×258, so 1024 clears
        /// both; a sheet that needed a lift would mean the cell table grew, which is a decision and
        /// not something an import default gets to make.</summary>
        public const int ImportSizeCap = 1024;

        /// <summary>4 variants, then the same 4 MIRRORED.</summary>
        public const int SheetCols = 8;

        /// <summary>Tide rows: dry / wet / awash.</summary>
        public const int SheetRows = 3;

        /// <summary>Distinct variants per form — half of <see cref="SheetCols"/>.</summary>
        public const int VariantCols = 4;

        /// <summary>225 sheets = 75 (form × stone) pairs × 3 dress.</summary>
        public const int SheetCount = 225;

        /// <summary>900 sheet PNGs = <see cref="SheetCount"/> × 4 channels.</summary>
        public const int SheetPngCount = SheetCount * 4;

        /// <summary>28 seat decals, each an albedo + a <c>_blend</c>. ⚠ The intake handoff's census
        /// said 30 decals / 960 PNGs; the drop declares and ships 28, so 956 PNGs land.</summary>
        public const int SeatDecalCount = 28;

        public const int SeatPngCount = SeatDecalCount * 2;

        public const int TotalPngCount = SheetPngCount + SeatPngCount;

        // =================================================================================
        // the axes, in the contract's own order
        // =================================================================================

        /// <summary>The sixteen forms, in contract order. The key is lower-case; the sheet stem
        /// capitalises it (see <see cref="SheetPrefix"/>).</summary>
        public static readonly string[] FormKeys =
        {
            "erratic", "block", "perched", "slab", "fin", "wedge", "cloven", "knuckle",
            "bench", "shelf", "prisms", "scree", "cobbles", "skerry", "spine", "apron",
        };

        /// <summary>Stone is colour AND structure. ⚠ Not every form carries every stone — read
        /// <see cref="FormEntry.Stones"/>, never this table, when enumerating sheets.</summary>
        public static readonly string[] Stones =
            { "sandstone", "till", "basalt", "granite", "quartzite" };

        /// <summary>Tide state, and it is the sheet's ROW axis.</summary>
        public static readonly string[] Tides = { "dry", "wet", "awash" };

        /// <summary>Dress is a separate SHEET, and <c>bare</c> is UNSUFFIXED.</summary>
        public static readonly string[] Dress = { "bare", "barnacled", "weeded" };

        /// <summary>Ground materials a seat decal can be painted in.</summary>
        public static readonly string[] Seats =
        {
            "grass", "dirt", "path", "mud", "sand", "shingle", "ledge", "marram",
            "foreshore", "silt", "shelf", "talus", "rockweed", "water",
        };

        // =================================================================================
        // channels
        // =================================================================================

        /// <summary>Channel suffix on a stem. The albedo carries no suffix — it is what the sheet is
        /// called.</summary>
        public const string UnlitSuffix = "_unlit";

        public const string MaskSuffix = "_mask";
        public const string NormalSuffix = "_normal";

        /// <summary>Seat decals only: R material weight · G mark lead · B mark id.</summary>
        public const string BlendSuffix = "_blend";

        public enum Channel { Albedo, Unlit, Mask, Normal, Blend }

        /// <summary>The four channels every SHEET ships, in export order.</summary>
        public static readonly Channel[] SheetChannels =
            { Channel.Albedo, Channel.Unlit, Channel.Mask, Channel.Normal };

        /// <summary>The two channels every SEAT DECAL ships.</summary>
        public static readonly Channel[] SeatChannels = { Channel.Albedo, Channel.Blend };

        public static string SuffixFor(Channel c) => c switch
        {
            Channel.Albedo => "",
            Channel.Unlit => UnlitSuffix,
            Channel.Mask => MaskSuffix,
            Channel.Normal => NormalSuffix,
            Channel.Blend => BlendSuffix,
            _ => throw new ArgumentOutOfRangeException(nameof(c)),
        };

        /// <summary>
        /// True for the channels that are PICTURES. <c>_mask</c>, <c>_normal</c> and <c>_blend</c>
        /// are NUMBERS: sRGB-decoding them would bend every value on the way in, and
        /// <c>alphaIsTransparency</c> would bleed their RGB into the transparent margin. Both must be
        /// off — the same rule as the tree kit's data channels.
        /// </summary>
        public static bool IsColourChannel(Channel c) => c == Channel.Albedo || c == Channel.Unlit;

        /// <summary>The <see cref="Channel"/> a stem belongs to, by its suffix. No suffix here is a
        /// suffix of another, so the order of these tests carries no meaning.</summary>
        public static Channel ChannelOf(string stem) =>
            stem == null ? Channel.Albedo
            : stem.EndsWith(UnlitSuffix, StringComparison.Ordinal) ? Channel.Unlit
            : stem.EndsWith(MaskSuffix, StringComparison.Ordinal) ? Channel.Mask
            : stem.EndsWith(NormalSuffix, StringComparison.Ordinal) ? Channel.Normal
            : stem.EndsWith(BlendSuffix, StringComparison.Ordinal) ? Channel.Blend
            : Channel.Albedo;

        // =================================================================================
        // names and paths
        // =================================================================================

        /// <summary>The sheet stem's form prefix: the contract's lower-case key with its first letter
        /// capitalised (<c>erratic</c> → <c>Erratic</c>). Every one of the sixteen keys is a single
        /// word, and <c>RockPxKitContractTests</c> asserts all 225 stems built this way exist on
        /// disk — so a key that stopped being one word would fail loudly, not silently.</summary>
        public static string SheetPrefix(string formKey)
        {
            RequireOneOf(FormKeys, formKey, "form");
            return char.ToUpperInvariant(formKey[0]) + formKey.Substring(1);
        }

        /// <summary>The variant letter a sidecar id and a seat-decal file use: A–D.</summary>
        public static char VariantLetter(int variant)
        {
            if (variant < 0 || variant >= VariantCols)
                throw new ArgumentOutOfRangeException(
                    nameof(variant), $"variant {variant} is outside 0..{VariantCols - 1}.");
            return (char)('A' + variant);
        }

        /// <summary>Sheet stem for one form × stone × dress, e.g. <c>Erratic_granite</c> (bare) or
        /// <c>Erratic_granite_weeded</c>. ⚠ <c>bare</c> is UNSUFFIXED.</summary>
        public static string StemFor(string formKey, string stone, string dress)
        {
            RequireOneOf(Stones, stone, "stone");
            RequireOneOf(Dress, dress, "dress");
            string bare = $"{SheetPrefix(formKey)}_{stone}";
            return string.Equals(dress, "bare", StringComparison.Ordinal) ? bare : $"{bare}_{dress}";
        }

        public static string SheetPath(string formKey, string stone, string dress, Channel channel) =>
            TexDir + StemFor(formKey, stone, dress) + SuffixFor(channel) + ".png";

        /// <summary>The sidecar for one form.</summary>
        public static string SidecarPath(string formKey) =>
            SidecarDir + RequireOneOf(FormKeys, formKey, "form") + ".json";

        /// <summary>Every sheet stem the drop ships, in contract order — form, then that form's OWN
        /// stones, then dress.</summary>
        public static IEnumerable<string> AllSheetStems(Contract c)
        {
            if (c == null) yield break;
            foreach (FormEntry f in c.Forms)
            foreach (string stone in f.Stones)
            foreach (string dress in Dress)
                yield return StemFor(f.Key, stone, dress);
        }

        // =================================================================================
        // the contract schema
        // =================================================================================

        public sealed class Contract
        {
            public string ExportSymbol, RigSha256, Kit, Rig, PivotNote;
            public int Ppu, Texel;
            public int SheetCols, SheetRows;
            public int SheetCount, PngCount;
            public string[] Stones, Tides, Dress, Seats;
            public readonly List<FormEntry> Forms = new List<FormEntry>();

            /// <summary>How deep a rock sits into each ground material, in px.</summary>
            public readonly Dictionary<string, int> Bury =
                new Dictionary<string, int>(StringComparer.Ordinal);

            public FormEntry Find(string key) =>
                Forms.FirstOrDefault(f => string.Equals(f.Key, key, StringComparison.Ordinal));

            /// <summary>(form × stone) pairs — 75 in this drop.</summary>
            public int PairCount => Forms.Sum(f => f.Stones.Length);
        }

        public sealed class FormEntry
        {
            public string Key, Name, Role, NativeStone, SidecarFile;
            public int CellW, CellH;

            /// <summary>The stones THIS form ships, in contract order — the native stone first. Not
            /// every form carries all five.</summary>
            public string[] Stones = Array.Empty<string>();

            public int SheetW => CellW * SheetCols;
            public int SheetH => CellH * SheetRows;
        }

        /// <summary>One form's sidecar: the anchors, keyed by stone then by variant. Anchors are per
        /// (form, stone, variant) in this pass — unlike pass two, where one entry served every bake
        /// of a variant because the geometry was stone- and tide-independent.</summary>
        public sealed class Sidecar
        {
            public string ExportSymbol, RigSha256, Form, Name, Role, Note, PivotNote;
            public int CellW, CellH;

            public readonly Dictionary<string, List<VariantEntry>> Stones =
                new Dictionary<string, List<VariantEntry>>(StringComparer.Ordinal);

            public List<VariantEntry> For(string stone) =>
                Stones.TryGetValue(stone, out var v) ? v : null;

            public int VariantCount => Stones.Values.Sum(v => v.Count);
        }

        public sealed class VariantEntry
        {
            public string Id;

            /// <summary>Variant index 0–3 — the sheet COLUMN it occupies unmirrored.</summary>
            public int Variant;

            public int CellW, CellH;

            /// <summary>Highest point in metres — the rock's true height.</summary>
            public float TopM;
            public float SizeM;

            /// <summary>Collision ellipse radii in METRES.</summary>
            public float FootprintRx, FootprintRy;

            /// <summary>Ground contact in cell px from the TOP-LEFT — the pivot, and the only
            /// quantity a slice is allowed to pivot on.</summary>
            public Vector2Int Ground;

            /// <summary>What the sidecar publishes as <c>anchors.pivot</c>. It equals
            /// <see cref="Ground"/> throughout this drop and the contract tests pin that; read
            /// <see cref="Ground"/>, which is the one the README names.</summary>
            public Vector2Int Pivot;

            /// <summary>Highest standable point. ⚠ <see cref="PerchFlat"/> false ⇒ DECORATIVE — a
            /// spawner must honour the flag, not the point.</summary>
            public Vector2Int Perch;
            public float PerchZM;
            public bool PerchFlat;

            /// <summary>Outer silhouette catches for rope and pot lines.</summary>
            public readonly List<Vector2Int> Snags = new List<Vector2Int>();

            /// <summary>Awash danger point + radius in metres, or null when the rock is not a
            /// hazard.</summary>
            public Vector2Int? Hazard;
            public float HazardRM;

            /// <summary>Tide-pool basin rect (cell px, top-left origin) + depth in m — the SHADER's
            /// fill target. The bake leaves the basin empty; it never paints water.</summary>
            public RectInt? Pool;
            public float PoolDepthM;

            /// <summary>The tide mark: screen row + height in m where rockweed drapes.</summary>
            public int WeedLineY;
            public float WeedLineZM;
        }

        /// <summary>One seat decal: the ground-material skirt painted in the terrain's own material,
        /// drawn OVER the rock on the same pivot.</summary>
        public sealed class SeatDecal
        {
            /// <summary>As declared: <c>seat/&lt;name&gt;</c>, WITHOUT the extension.</summary>
            public string File;

            public string Form;
            public int Variant;

            /// <summary>The ground material. <see cref="Seat2"/> is non-null on a SEAM decal, whose
            /// frontier must be placed on the level's own material boundary — the decal dresses a
            /// boundary, it does not invent one.</summary>
            public string Seat, Seat2;

            /// <summary>How far the skirt reaches past the footprint, in px.</summary>
            public int Reach;

            public int CellW, CellH;

            /// <summary>The rock's ground contact, in cell px from the TOP-LEFT — the decal shares
            /// it.</summary>
            public Vector2Int Pivot;

            /// <summary>The decal's own file name, e.g. <c>KnuckleA_skirt_grass_sand</c>.</summary>
            public string Stem => File == null ? null : File.Substring(File.LastIndexOf('/') + 1);

            public string Path(Channel channel) => SeatDir + Stem + SuffixFor(channel) + ".png";

            public bool IsSeam => !string.IsNullOrEmpty(Seat2);
        }

        public sealed class SeatSet
        {
            public string ExportSymbol, RigSha256, Note;
            public readonly List<SeatDecal> Decals = new List<SeatDecal>();
        }

        // =================================================================================
        // reading it back
        // =================================================================================

        /// <summary>
        /// The committed contract, or null with a logged error. Dictionary-shaped (<c>bury</c> is
        /// keyed by material), so <see cref="JsonUtility"/> cannot read it — hence
        /// <see cref="MiniJson"/>.
        /// </summary>
        public static Contract Load()
        {
            var d = ReadObject(ContractPath, "contract");
            if (d == null) return null;

            var sheet = MiniJson.Dict(d, "sheet");
            var c = new Contract
            {
                ExportSymbol = MiniJson.String(d, "exportSymbol"),
                RigSha256 = MiniJson.String(d, "derivedFromRigSha256"),
                Kit = MiniJson.String(d, "kit"),
                Rig = MiniJson.String(d, "rig"),
                PivotNote = MiniJson.String(d, "pivot"),
                Ppu = MiniJson.Int(d, "ppu"),
                Texel = MiniJson.Int(d, "texel"),
                SheetCols = MiniJson.Int(sheet, "cols"),
                SheetRows = MiniJson.Int(sheet, "rows"),
                SheetCount = MiniJson.Int(d, "sheetCount"),
                PngCount = MiniJson.Int(d, "pngCount"),
                Stones = (MiniJson.List(d, "stones") ?? new List<object>())
                         .OfType<Dictionary<string, object>>()
                         .Select(s => MiniJson.String(s, "key")).ToArray(),
                Tides = StrList(d, "tides"),
                Dress = StrList(d, "dress"),
                Seats = StrList(d, "seats"),
            };

            var bury = MiniJson.Dict(d, "bury");
            if (bury != null)
                foreach (var kv in bury)
                    c.Bury[kv.Key] = ToInt(kv.Value);

            var forms = MiniJson.List(d, "forms");
            if (forms == null)
            {
                Debug.LogError($"[RockPxCatalog] '{ContractPath}' carries no forms block.");
                return null;
            }

            // Index the export's own list by key, then walk OUR key order: a rebake may reorder the
            // list without meaning anything by it, but a form silently DROPPED from the contract must
            // fail loudly rather than quietly shorten the census.
            var byKey = new Dictionary<string, Dictionary<string, object>>(StringComparer.Ordinal);
            foreach (var f in forms.OfType<Dictionary<string, object>>())
            {
                string k = MiniJson.String(f, "key");
                if (!string.IsNullOrEmpty(k)) byKey[k] = f;
            }

            foreach (string key in FormKeys)
            {
                if (!byKey.TryGetValue(key, out var fd))
                {
                    Debug.LogError($"[RockPxCatalog] '{ContractPath}' has no '{key}' form.");
                    return null;
                }

                var cell = MiniJson.List(fd, "cell") ?? new List<object>();
                c.Forms.Add(new FormEntry
                {
                    Key = key,
                    Name = MiniJson.String(fd, "name"),
                    Role = MiniJson.String(fd, "role"),
                    NativeStone = MiniJson.String(fd, "nativeStone"),
                    SidecarFile = MiniJson.String(fd, "sidecar"),
                    CellW = cell.Count > 0 ? ToInt(cell[0]) : 0,
                    CellH = cell.Count > 1 ? ToInt(cell[1]) : 0,
                    Stones = (MiniJson.List(fd, "stones") ?? new List<object>())
                             .OfType<string>().ToArray(),
                });
            }

            return c;
        }

        /// <summary>One form's sidecar, or null with a logged error.</summary>
        public static Sidecar LoadSidecar(string formKey)
        {
            string path = SidecarPath(formKey);
            var d = ReadObject(path, "sidecar");
            if (d == null) return null;

            var cell = MiniJson.List(d, "cell") ?? new List<object>();
            var sc = new Sidecar
            {
                ExportSymbol = MiniJson.String(d, "exportSymbol"),
                RigSha256 = MiniJson.String(d, "derivedFromRigSha256"),
                Form = MiniJson.String(d, "form"),
                Name = MiniJson.String(d, "name"),
                Role = MiniJson.String(d, "role"),
                Note = MiniJson.String(d, "note"),
                PivotNote = MiniJson.String(d, "pivot"),
                CellW = cell.Count > 0 ? ToInt(cell[0]) : 0,
                CellH = cell.Count > 1 ? ToInt(cell[1]) : 0,
            };

            var stones = MiniJson.Dict(d, "stones");
            if (stones == null)
            {
                Debug.LogError($"[RockPxCatalog] '{path}' carries no stones block.");
                return null;
            }

            // The stone keys are the sidecar's OWN — a (form, stone) pair the contract declares but
            // the sidecar omits is an upstream defect, not something to paper over with an empty
            // list. RockPxKitContractTests is what catches it.
            foreach (var kv in stones)
            {
                var list = new List<VariantEntry>();
                foreach (var v in (kv.Value as List<object> ?? new List<object>())
                                  .OfType<Dictionary<string, object>>())
                    list.Add(ReadVariant(v));
                sc.Stones[kv.Key] = list;
            }

            return sc;
        }

        /// <summary>Every sidecar, keyed by form, or null if any one of them failed to read.</summary>
        public static Dictionary<string, Sidecar> LoadAllSidecars()
        {
            var all = new Dictionary<string, Sidecar>(StringComparer.Ordinal);
            foreach (string key in FormKeys)
            {
                Sidecar sc = LoadSidecar(key);
                if (sc == null) return null;
                all[key] = sc;
            }
            return all;
        }

        /// <summary>The seat-decal index, or null with a logged error.</summary>
        public static SeatSet LoadSeats()
        {
            var d = ReadObject(SeatsPath, "seat index");
            if (d == null) return null;

            var set = new SeatSet
            {
                ExportSymbol = MiniJson.String(d, "exportSymbol"),
                RigSha256 = MiniJson.String(d, "derivedFromRigSha256"),
                Note = MiniJson.String(d, "note"),
            };

            var decals = MiniJson.List(d, "decals");
            if (decals == null)
            {
                Debug.LogError($"[RockPxCatalog] '{SeatsPath}' carries no decals block.");
                return null;
            }

            foreach (var dd in decals.OfType<Dictionary<string, object>>())
            {
                var cell = MiniJson.List(dd, "cell") ?? new List<object>();
                var pivot = MiniJson.Dict(dd, "pivot");
                set.Decals.Add(new SeatDecal
                {
                    File = MiniJson.String(dd, "file"),
                    Form = MiniJson.String(dd, "form"),
                    Variant = MiniJson.Int(dd, "variant"),
                    Seat = MiniJson.String(dd, "seat"),
                    Seat2 = MiniJson.String(dd, "seat2"),
                    Reach = MiniJson.Int(dd, "reach"),
                    CellW = cell.Count > 0 ? ToInt(cell[0]) : 0,
                    CellH = cell.Count > 1 ? ToInt(cell[1]) : 0,
                    Pivot = new Vector2Int(MiniJson.Int(pivot, "x"), MiniJson.Int(pivot, "y")),
                });
            }

            return set;
        }

        static VariantEntry ReadVariant(Dictionary<string, object> v)
        {
            var a = MiniJson.Dict(v, "anchors");
            var fp = MiniJson.Dict(a, "footprint");
            var ground = MiniJson.Dict(fp, "ground");
            var perch = MiniJson.Dict(a, "perch");
            var pivot = MiniJson.Dict(a, "pivot");
            var hazard = MiniJson.Dict(a, "hazard");
            var pool = MiniJson.Dict(a, "pool");
            var weed = MiniJson.Dict(a, "weedLine");
            var cell = MiniJson.List(v, "cell") ?? new List<object>();

            var e = new VariantEntry
            {
                Id = MiniJson.String(v, "id"),
                Variant = MiniJson.Int(v, "variant"),
                CellW = cell.Count > 0 ? ToInt(cell[0]) : 0,
                CellH = cell.Count > 1 ? ToInt(cell[1]) : 0,
                TopM = MiniJson.Float(v, "topM"),
                SizeM = MiniJson.Float(v, "sizeM"),
                FootprintRx = MiniJson.Float(fp, "rx"),
                FootprintRy = MiniJson.Float(fp, "ry"),
                Ground = new Vector2Int(MiniJson.Int(ground, "x"), MiniJson.Int(ground, "y")),
                Pivot = new Vector2Int(MiniJson.Int(pivot, "x"), MiniJson.Int(pivot, "y")),
                Perch = new Vector2Int(MiniJson.Int(perch, "x"), MiniJson.Int(perch, "y")),
                PerchZM = MiniJson.Float(perch, "zM"),
                PerchFlat = MiniJson.Bool(perch, "flat"),
                WeedLineY = MiniJson.Int(weed, "y"),
                WeedLineZM = MiniJson.Float(weed, "zM"),
            };

            foreach (var s in (MiniJson.List(a, "snags") ?? new List<object>())
                              .OfType<Dictionary<string, object>>())
                e.Snags.Add(new Vector2Int(MiniJson.Int(s, "x"), MiniJson.Int(s, "y")));

            if (hazard != null)
            {
                e.Hazard = new Vector2Int(MiniJson.Int(hazard, "x"), MiniJson.Int(hazard, "y"));
                e.HazardRM = MiniJson.Float(hazard, "rM");
            }

            if (pool != null)
            {
                e.Pool = new RectInt(MiniJson.Int(pool, "x"), MiniJson.Int(pool, "y"),
                                     MiniJson.Int(pool, "w"), MiniJson.Int(pool, "h"));
                e.PoolDepthM = MiniJson.Float(pool, "depthM");
            }

            return e;
        }

        // =================================================================================
        // derived placement numbers
        // =================================================================================

        /// <summary>The variant a sheet COLUMN draws — cols 4–7 repeat cols 0–3.</summary>
        public static int VariantForColumn(int col) => RequireColumn(col) % VariantCols;

        /// <summary>True for the four MIRRORED columns.</summary>
        public static bool IsMirroredColumn(int col) => RequireColumn(col) >= VariantCols;

        /// <summary>
        /// The pivot of one COLUMN in cell px from the TOP-LEFT. A mirrored column's art is flipped
        /// about the cell's vertical centre, so its ground contact is at <c>cellW − x</c>.
        ///
        /// <para>⚠ In THIS drop that changes nothing: every one of the 300 variants the kit
        /// declares — the 276 of the first intake plus the 24 the 2026-09-13 re-export added —
        /// puts its ground contact exactly on the centre line (measured displacement 0 px),
        /// which is what "bottom-centre ground contact" means and what
        /// <c>RockPxKitContractTests</c> pins. The rule
        /// is written out anyway because the pivot is DATA: a rebake that moved one contact off
        /// centre would otherwise float four columns of that sheet by twice the offset, silently.</para>
        /// </summary>
        public static Vector2Int PivotPxForColumn(VariantEntry v, int cellW, int col) =>
            IsMirroredColumn(col) ? new Vector2Int(cellW - v.Ground.x, v.Ground.y) : v.Ground;

        /// <summary>
        /// The Unity sprite pivot for one column: normalised, BOTTOM-origin,
        /// <c>(x/W, (H − y)/H)</c>. See the class remarks for why the y term is not the tree kit's
        /// <c>pad/H</c>.
        /// </summary>
        public static Vector2 NormalizedPivot(VariantEntry v, int cellW, int cellH, int col)
        {
            Vector2Int p = PivotPxForColumn(v, cellW, col);
            return new Vector2((float)p.x / cellW, (cellH - p.y) / (float)cellH);
        }

        /// <summary>The seat decal's pivot, normalised the same way. A decal is a SINGLE sprite, not
        /// a grid, and it is never mirrored.</summary>
        public static Vector2 NormalizedPivot(SeatDecal d) =>
            new Vector2((float)d.Pivot.x / d.CellW, (d.CellH - d.Pivot.y) / (float)d.CellH);

        /// <summary>
        /// Rows of near-flank art drawn BELOW the ground contact — what a bottom-of-cell pivot would
        /// float the rock by. Measured 5–18 px = 0.16–0.56 m across this drop, positive every time.
        /// </summary>
        public static int GroundOverhangPx(VariantEntry v, int cellH) => cellH - v.Ground.y;

        /// <summary>That overhang in metres at the locked PPU.</summary>
        public static float GroundOverhangMetres(VariantEntry v, int cellH) =>
            GroundOverhangPx(v, cellH) / (float)Ppu;

        /// <summary>
        /// The cell rect of one column × tide row within its sheet, in px from the sheet's
        /// TOP-LEFT — variant COLS × tide ROWS, the layout the export writes.
        /// </summary>
        public static RectInt CellRect(int cellW, int cellH, int col, int row) =>
            new RectInt(RequireColumn(col) * cellW, RequireRow(row) * cellH, cellW, cellH);

        // ---- shared helpers -------------------------------------------------------------------

        static Dictionary<string, object> ReadObject(string path, string what)
        {
            if (!File.Exists(path))
            {
                Debug.LogError(
                    $"[RockPxCatalog] No {what} at '{path}'. It ships with the kit — if this fired, " +
                    "the branch predates the Rock Px import.");
                return null;
            }

            object root;
            try { root = MiniJson.Parse(File.ReadAllText(path)); }
            catch (FormatException e)
            {
                Debug.LogError($"[RockPxCatalog] '{path}' is not valid JSON: {e.Message}");
                return null;
            }

            if (root is Dictionary<string, object> d) return d;

            Debug.LogError($"[RockPxCatalog] '{path}' did not parse to an object.");
            return null;
        }

        static int ToInt(object o) => o is double d ? (int)Math.Round(d) : 0;

        static string[] StrList(Dictionary<string, object> node, string key) =>
            (MiniJson.List(node, key) ?? new List<object>()).OfType<string>().ToArray();

        static int RequireColumn(int col)
        {
            if (col < 0 || col >= SheetCols)
                throw new ArgumentOutOfRangeException(
                    nameof(col), $"column {col} is outside 0..{SheetCols - 1}.");
            return col;
        }

        static int RequireRow(int row)
        {
            if (row < 0 || row >= SheetRows)
                throw new ArgumentOutOfRangeException(
                    nameof(row), $"tide row {row} is outside 0..{SheetRows - 1} " +
                                 $"({string.Join(", ", Tides)}).");
            return row;
        }

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
