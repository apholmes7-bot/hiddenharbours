#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace HiddenHarbours.Art.Editor
{
    /// <summary>
    /// The PLACEMENT CONTRACT for the Rock Iso kit — the schema of
    /// <c>Assets/_Project/Art/Sprites/Shore/Rock/RockIso.json</c> and the one place anything
    /// downstream reads a rock's cell, pivot, collision footprint, perch, snags, hazard radius or
    /// tide-pool rect.
    ///
    /// <para><b>⭐ THIS KIT SHIPS NO PIXELS, AND THAT IS THE DESIGN.</b> Six species × four stones ×
    /// three tides is 72 sheets, so the rig is the deliverable and sheets bake to order
    /// (<see cref="HiddenHarbours.Tools.RigBaking.RockIsoBaker"/>, menu <i>Hidden Harbours ▸ Art ▸
    /// Bake Rock Iso Sheets</i>). The sidecar ships anyway because the anchors are <b>stone- and
    /// tide-independent</b> — the geometry is seed-stable, so one entry serves all twelve bakes of a
    /// variant and the engine can be wired against this contract before a single PNG exists. Which
    /// stone × tide pairs actually ship is an ATLAS BUDGET decision for the owner, not something an
    /// import should quietly make.</para>
    ///
    /// <para><b>⚠ The pivot is the BOTTOM-CENTRE GROUND CONTACT, not the bbox centre</b> — the tile
    /// the rock stands on. Under the 40° camera a rock's near flank is drawn BELOW its ground
    /// contact, so a bottom-centre pivot would float every one of them by that overhang — measured
    /// <b>7–22 px = 0.219–0.688 m</b> across the twenty variants, positive in every case (see
    /// <see cref="GroundOverhangPx"/>). The value is read from <c>anchors.pivot</c> and never
    /// derived from alpha.</para>
    ///
    /// <para><b>The pivot normalisation is ADR 0026's, deliberately.</b> <c>anchors.pivot</c> is a
    /// continuous top-left-origin coordinate (the rig computes it as
    /// <c>round(baseY − depthSquash·y0)</c>), and the contract publishes <b>no <c>pad</c></b> — so
    /// the y term is <c>(H − y)/H</c>, the same formula as
    /// <c>RigGeometry.UnityNormalisedPivot</c> and
    /// <see cref="FishingSheetSlicer.KitSpec.NormalizedPivot"/>. This is NOT the tree kit's
    /// <c>pad/cellH</c> case: that one is a row index the tree rig defines itself and which doubles
    /// as the wind shader's <c>_TrunkAnchor</c>. Read the quantity the contract publishes and use
    /// exactly that.</para>
    ///
    /// <para>No water is described anywhere below, because the kit bakes none (ADR 0010/0012/0023).
    /// An <c>awash</c> sheet is CLIPPED at the sea plane with a wet-glint band at the cut — it is not
    /// flooded, and <see cref="VariantEntry.Pool"/> is an empty damp basin plus a rect for the shader
    /// to fill, never painted water.</para>
    /// </summary>
    public static class RockIsoCatalog
    {
        /// <summary>The kit's folder — where <c>_rockBake.js</c>'s default <c>dir</c> points, so an
        /// in-engine bake and the art director's own browser bake land in the same place.</summary>
        public const string RockRoot = "Assets/_Project/Art/Sprites/Shore/Rock/";

        public const string ContractFileName = "RockIso.json";

        public static string ContractPath => RockRoot + ContractFileName;

        /// <summary>The rig this kit renders from — the art-director's file, read-only from here.</summary>
        public const string RigScriptPath = "docs/art/rigs/rockIsoRig.js";

        public const string RigGlobalName = "RockIso";

        /// <summary>What the contract calls itself, pinned so a different kit cannot land here unnoticed.</summary>
        public const string KitName = "Rock Iso";

        /// <summary>32 px = 1 m, the locked scale (ADR 0006/0022).</summary>
        public const int Ppu = 32;

        /// <summary>Over this Unity imports SILENTLY DOWNSCALED with the sprite COUNT still matching.
        /// The largest sheet the kit can bake is Skerry at 416×156, so nothing here is close — a sheet
        /// that needed a lift would mean the bake recipe grew, which is a decision.</summary>
        public const int ImportSizeCap = 2048;

        // ---- the axes, in the contract's own order ------------------------------------------------

        /// <summary>The six species, in contract order.</summary>
        public static readonly string[] SpeciesKeys =
            { "Erratic", "Outcrop", "PoolLedge", "Skerry", "Cloven", "Cobble" };

        /// <summary>
        /// Stone is colour AND structure — bedding, angularity and grain are reweighted per stone, so
        /// they read apart in silhouette rather than only in hue. <c>sandstone</c> is the
        /// shore-matching one: it is ShoreIso2's <c>redrock</c> ramp verbatim, so a rock composites
        /// onto a cliff toe with no seam.
        /// </summary>
        public static readonly string[] Stones = { "sandstone", "granite", "basalt", "quartzite" };

        /// <summary>Tide state. <c>awash</c> is CLIPPED at the sea plane, never flooded.</summary>
        public static readonly string[] Tides = { "dry", "wet", "awash" };

        /// <summary>Dress is real pixels and occupies the sheet ROWS.</summary>
        public static readonly string[] Dress = { "bare", "barnacled", "weeded" };

        /// <summary>Sheet stem for one species × stone × tide, e.g. <c>Erratic_sandstone_dry</c>.</summary>
        public static string StemFor(string species, string stone, string tide) =>
            $"{RequireOneOf(SpeciesKeys, species, "species")}_" +
            $"{RequireOneOf(Stones, stone, "stone")}_" +
            $"{RequireOneOf(Tides, tide, "tide")}";

        public static string SheetPath(string species, string stone, string tide) =>
            RockRoot + StemFor(species, stone, tide) + ".png";

        /// <summary>Every sheet the full 72-way bake would produce, in bake order.</summary>
        public static IEnumerable<string> AllSheetStems()
        {
            foreach (string stone in Stones)
            foreach (string tide in Tides)
            foreach (string species in SpeciesKeys)
                yield return StemFor(species, stone, tide);
        }

        // =================================================================================
        // the contract schema
        // =================================================================================

        public sealed class Contract
        {
            public string Kit;
            public int Ppu;
            public float CameraElev, DepthSquash, HeightScale;
            public string Keyline;
            public string Water;
            public string[] Stones, Tides, Dress;
            public readonly List<SpeciesEntry> Species = new List<SpeciesEntry>();

            public SpeciesEntry Find(string key) =>
                Species.FirstOrDefault(s => string.Equals(s.Key, key, StringComparison.Ordinal));

            public int VariantCount => Species.Sum(s => s.Variants.Count);
        }

        public sealed class SpeciesEntry
        {
            public string Key, Name, Note;
            public int CellW, CellH;
            public int SheetCols, SheetRows, SheetW, SheetH;
            public readonly List<VariantEntry> Variants = new List<VariantEntry>();
        }

        public sealed class VariantEntry
        {
            public string Id;
            public int Col, DressRow;
            /// <summary>Highest point in metres — the rock's true height.</summary>
            public float TopM;
            public float SizeM, Waterline;
            public string Stone;

            /// <summary>Collision ellipse radii in METRES.</summary>
            public float FootprintRx, FootprintRy;
            /// <summary>Ground contact in cell px from the TOP-LEFT. Equals <see cref="Pivot"/>.</summary>
            public Vector2Int Ground;
            /// <summary>Bottom-centre ground contact, cell px from the TOP-LEFT.</summary>
            public Vector2Int Pivot;

            /// <summary>Highest standable point. ⚠ <see cref="PerchFlat"/> false ⇒ DECORATIVE —
            /// a spawner must honour the flag, not the point.</summary>
            public Vector2Int Perch;
            public float PerchZM;
            public bool PerchFlat;

            /// <summary>Outer silhouette catches for rope and pot lines.</summary>
            public readonly List<Vector2Int> Snags = new List<Vector2Int>();

            /// <summary>Awash danger radius in metres, or null when the rock is not awash.</summary>
            public float? HazardRM;
            public Vector2Int? Hazard;

            /// <summary>Tide-pool basin rect (cell px, top-left origin) + depth in m — the SHADER's
            /// fill target. The bake leaves the basin empty; it never paints water.</summary>
            public RectInt? Pool;
            public float PoolDepthM;

            /// <summary>The tide mark: screen row + height in m where rockweed drapes.</summary>
            public int WeedLineY;
            public float WeedLineZM;

            public bool IsAwash => Waterline > 0f;
        }

        // =================================================================================
        // reading it back
        // =================================================================================

        /// <summary>
        /// The committed contract, or null with a logged error. Dictionary-shaped (<c>species</c> is
        /// keyed by name), so <see cref="JsonUtility"/> cannot read it — hence <see cref="MiniJson"/>.
        /// </summary>
        public static Contract Load()
        {
            if (!File.Exists(ContractPath))
            {
                Debug.LogError(
                    $"[RockIsoCatalog] No contract at '{ContractPath}'. It ships with the kit — if " +
                    "this fired, the branch predates the Rock Iso import.");
                return null;
            }

            object root;
            try { root = MiniJson.Parse(File.ReadAllText(ContractPath)); }
            catch (FormatException e)
            {
                Debug.LogError($"[RockIsoCatalog] '{ContractPath}' is not valid JSON: {e.Message}");
                return null;
            }

            if (!(root is Dictionary<string, object> d))
            {
                Debug.LogError($"[RockIsoCatalog] '{ContractPath}' did not parse to an object.");
                return null;
            }

            var camera = MiniJson.Dict(d, "camera");
            var c = new Contract
            {
                Kit = Str(d, "kit"),
                Ppu = MiniJson.Int(d, "ppu"),
                CameraElev = MiniJson.Float(camera, "elev"),
                DepthSquash = MiniJson.Float(camera, "depthSquash"),
                HeightScale = MiniJson.Float(camera, "heightScale"),
                Keyline = Str(d, "keyline"),
                Water = Str(d, "water"),
                Stones = (MiniJson.List(d, "stones") ?? new List<object>())
                         .OfType<Dictionary<string, object>>().Select(s => Str(s, "key")).ToArray(),
                Tides = StrList(d, "tides"),
                Dress = StrList(d, "dress"),
            };

            var species = MiniJson.Dict(d, "species");
            if (species == null)
            {
                Debug.LogError($"[RockIsoCatalog] '{ContractPath}' carries no species block.");
                return null;
            }

            // Iterate OUR key order, not the dictionary's: a JSON object has no guaranteed order and
            // a species silently dropped from the contract must fail loudly, not shorten the list.
            foreach (string key in SpeciesKeys)
            {
                if (!species.TryGetValue(key, out object node) ||
                    !(node is Dictionary<string, object> sd))
                {
                    Debug.LogError($"[RockIsoCatalog] '{ContractPath}' has no '{key}' species.");
                    return null;
                }

                var cell = MiniJson.Dict(sd, "cell");
                var sheet = MiniJson.Dict(sd, "sheet");
                var entry = new SpeciesEntry
                {
                    Key = key,
                    Name = Str(sd, "name"),
                    Note = Str(sd, "note"),
                    CellW = MiniJson.Int(cell, "w"),
                    CellH = MiniJson.Int(cell, "h"),
                    SheetCols = MiniJson.Int(sheet, "cols"),
                    SheetRows = MiniJson.Int(sheet, "rows"),
                    SheetW = MiniJson.Int(sheet, "w"),
                    SheetH = MiniJson.Int(sheet, "h"),
                };

                foreach (var v in (MiniJson.List(sd, "variants") ?? new List<object>())
                                  .OfType<Dictionary<string, object>>())
                    entry.Variants.Add(ReadVariant(v));

                c.Species.Add(entry);
            }

            return c;
        }

        static VariantEntry ReadVariant(Dictionary<string, object> v)
        {
            var p = MiniJson.Dict(v, "params");
            var a = MiniJson.Dict(v, "anchors");
            var fp = MiniJson.Dict(a, "footprint");
            var ground = MiniJson.Dict(fp, "ground");
            var perch = MiniJson.Dict(a, "perch");
            var pivot = MiniJson.Dict(a, "pivot");
            var hazard = MiniJson.Dict(a, "hazard");
            var pool = MiniJson.Dict(a, "pool");
            var weed = MiniJson.Dict(a, "weedLine");

            var e = new VariantEntry
            {
                Id = Str(v, "id"),
                Col = MiniJson.Int(v, "col"),
                DressRow = MiniJson.Int(v, "dressRow"),
                TopM = MiniJson.Float(v, "topM"),
                SizeM = MiniJson.Float(p, "sizeM"),
                Waterline = MiniJson.Float(p, "waterline"),
                Stone = Str(p, "stone"),
                FootprintRx = MiniJson.Float(fp, "rx"),
                FootprintRy = MiniJson.Float(fp, "ry"),
                Ground = new Vector2Int(MiniJson.Int(ground, "x"), MiniJson.Int(ground, "y")),
                Pivot = new Vector2Int(MiniJson.Int(pivot, "x"), MiniJson.Int(pivot, "y")),
                Perch = new Vector2Int(MiniJson.Int(perch, "x"), MiniJson.Int(perch, "y")),
                PerchZM = MiniJson.Float(perch, "zM"),
                PerchFlat = Bool(perch, "flat"),
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

        /// <summary>
        /// The Unity sprite pivot: normalised, BOTTOM-origin, <c>(x/W, (H − y)/H)</c>.
        /// See the class remarks for why the y term is not the tree kit's <c>pad/H</c>.
        /// </summary>
        public static Vector2 NormalizedPivot(SpeciesEntry s, VariantEntry v) =>
            new Vector2((float)v.Pivot.x / s.CellW, (s.CellH - v.Pivot.y) / (float)s.CellH);

        /// <summary>The pivot in cell px from the rect's own bottom-left — what
        /// <c>Sprite.pivot</c> reports.</summary>
        public static Vector2 PivotPixels(SpeciesEntry s, VariantEntry v) =>
            new Vector2(v.Pivot.x, s.CellH - v.Pivot.y);

        /// <summary>
        /// Rows of near-flank art drawn BELOW the ground contact — what a bottom-centre pivot would
        /// float the rock by. This is the rock kit's version of the tree's flare pad, and it is the
        /// number that makes the pivot rule worth stating.
        /// </summary>
        public static int GroundOverhangPx(SpeciesEntry s, VariantEntry v) => s.CellH - v.Pivot.y;

        /// <summary>That overhang in metres at the locked PPU.</summary>
        public static float GroundOverhangMetres(SpeciesEntry s, VariantEntry v) =>
            GroundOverhangPx(s, v) / (float)Ppu;

        /// <summary>
        /// The cell rect of one variant × dress row within its sheet, in px from the sheet's
        /// TOP-LEFT — variant COLS × dress ROWS, the layout <c>_rockBake.js</c> writes.
        /// </summary>
        public static RectInt CellRect(SpeciesEntry s, VariantEntry v, int dressRow) =>
            new RectInt(v.Col * s.CellW, RequireDressRow(dressRow) * s.CellH, s.CellW, s.CellH);

        // ---- shared helpers -------------------------------------------------------------------

        static string Str(Dictionary<string, object> node, string key) =>
            node != null && node.TryGetValue(key, out object v) ? v as string : null;

        static bool Bool(Dictionary<string, object> node, string key) =>
            node != null && node.TryGetValue(key, out object v) && v is bool b && b;

        static string[] StrList(Dictionary<string, object> node, string key) =>
            (MiniJson.List(node, key) ?? new List<object>()).OfType<string>().ToArray();

        static int RequireDressRow(int row)
        {
            if (row < 0 || row >= Dress.Length)
                throw new ArgumentOutOfRangeException(
                    nameof(row), $"dress row {row} is outside 0..{Dress.Length - 1} " +
                                 $"({string.Join(", ", Dress)}).");
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
