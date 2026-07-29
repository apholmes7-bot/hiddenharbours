#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace HiddenHarbours.Art.Editor
{
    /// <summary>
    /// The SEMANTIC map for the shoreline-ISO **v8** tile kit (<c>ShoreIso2</c>) under
    /// <see cref="KitRoot"/> — which slice index of which sheet is "sand, edge-north" or
    /// "cliff mid, corner-SW".
    ///
    /// <para><b>Why a second catalog beside <see cref="ShorelineIsoCatalog"/> rather than more
    /// members on it.</b> v8 is not a re-bake of v7, it is a different grid: Ground gained a fourth
    /// world-tile column, the Dune grew from one band to <c>cap + toe</c>, <c>Contact.png</c> is a
    /// wholly new sheet, and the whole kit ships TWICE — once per shading style. Folding four
    /// changed constants into the v7 catalog would leave two kits sharing one set of names with
    /// different values, which is precisely how a painted cliff ends up one row off. v7 stays
    /// untouched and keeps working; this file owns v8. (Same one-catalog-per-kit shape as
    /// <see cref="TreeKitCatalog"/> and <see cref="WharfKitCatalog"/>.)</para>
    ///
    /// <para><b>Two styles, one geometry.</b> <c>nat</c> (naturalist — 8/7/8-step ramps, Bayer
    /// dither in the band-transition zone) and <c>gfx</c> (graphic — 6/5/6-step ramps, hard band
    /// edges, unified keyline) are the kit's A/B. The drop's own README is explicit that
    /// <i>geometry, grid, piece names, pivots and API are identical</i> and only the shading law
    /// differs, so a map authored against one style drops straight onto the other. That is why the
    /// row/column tables below are NOT per style — and why
    /// <see cref="ContractsAgreeOnGeometry"/> exists to keep it that way.</para>
    ///
    /// <para><b>⚠ The compass labels are the kit's CLAIM, not a measurement.</b> Same standing
    /// warning as v7: nothing here has been checked against rendered pixels, and this repo has
    /// shipped mislabelled compass art in five separate kits. Slices are named by GRID POSITION
    /// (<c>Cliff_7</c> = the 8th cell, row-major from the top-left) and the meaning is resolved
    /// here, against the kit's own <c>ShorelineIso.json</c>. If a painted cliff faces the wrong way
    /// in-scene, correct the label→column mapping in this file — never renumber the slices.</para>
    ///
    /// <para><b>Water is absent from every list below because the kit bakes none</b> (ADR
    /// 0010/0012/0023). The shader owns the waterline, foam, swash and the tide-pool fill; the tide
    /// sweeps over whatever ground is painted. Verified on import, not assumed: all ten delivered
    /// sheets were scanned and carry zero blue-dominant pixels.</para>
    /// </summary>
    public static class ShorelineIso2Catalog
    {
        /// <summary>
        /// The kit's folder. Deliberately the path the kit's OWN bake harness writes to
        /// (<c>_shoreBake.js</c> → <c>Art/Tilesets/ShorelineIso2/&lt;style&gt;/</c>): re-baking the
        /// kit must land on the committed files, not fork a second copy beside them.
        /// </summary>
        public const string KitRoot = "Assets/_Project/Art/Tilesets/ShorelineIso2";

        /// <summary>The kit's contract sidecar, one per style, shipped beside that style's sheets.</summary>
        public const string ContractFileName = "ShorelineIso.json";

        /// <summary>The rig this kit is baked from — the art-director's file (<c>docs/art/rigs/**</c>),
        /// read-only from here.</summary>
        public const string RigScriptPath = "docs/art/rigs/shoreIsoKitRig2.js";

        public const string RigGlobalName = "ShoreIso2";

        /// <summary>What the contract calls itself. Pinned so a v9 drop cannot land in the v8 folder
        /// unnoticed.</summary>
        public const string KitVersion = "ShorelineIso v8";

        /// <summary>The two shading styles. Identical geometry — see the class remarks.</summary>
        public static readonly string[] Styles = { "nat", "gfx" };

        /// <summary>One cell is 32 px is 1 m at the locked PPU (ADR 0006/0022).</summary>
        public const int Cell = 32;

        /// <summary>
        /// Over this Unity imports SILENTLY DOWNSCALED and the sprite COUNT still comes out right —
        /// only a cell-size assert catches it. The widest sheet here is Fringe at 384 px, so a sheet
        /// that needed a lift would mean the bake recipe grew, which is a decision and not an import
        /// setting.
        /// </summary>
        public const int ImportSizeCap = 2048;

        // ---- the five sheets ----------------------------------------------------------------------

        public const string Ground  = "Ground";
        public const string Fringe  = "Fringe";
        public const string Cliff   = "Cliff";
        public const string Dune    = "Dune";
        public const string Contact = "Contact";

        /// <summary>Every sheet the kit ships, per style.</summary>
        public static readonly string[] Sheets = { Ground, Fringe, Cliff, Dune, Contact };

        /// <summary>Cell grid of each sheet as <c>(cols, rows)</c> — the contract's own row/col lists.</summary>
        public static readonly IReadOnlyDictionary<string, (int cols, int rows)> Grids =
            new Dictionary<string, (int, int)>
            {
                [Ground]  = (4, 6),   // 128×192
                [Fringe]  = (12, 3),  // 384×96
                [Cliff]   = (10, 3),  // 320×96
                [Dune]    = (9, 2),   // 288×64
                [Contact] = (5, 1),   // 160×32
            };

        public static string StyleDir(string style) =>
            $"{KitRoot}/{RequireStyle(style)}";

        public static string SheetPath(string style, string sheet) =>
            $"{StyleDir(style)}/{RequireSheet(sheet)}.png";

        public static string ContractPath(string style) =>
            $"{StyleDir(style)}/{ContractFileName}";

        /// <summary>Sheet dimensions in px implied by the grid — what the PNG must actually be.</summary>
        public static (int w, int h) SheetSize(string sheet)
        {
            var g = Grids[RequireSheet(sheet)];
            return (g.cols * Cell, g.rows * Cell);
        }

        // ---- Ground.png — 4 cols × 6 rows ----------------------------------------------------------

        /// <summary>
        /// Ground rows, top to bottom. Opaque terrain fill; every material is authored to read right
        /// DRY AND SUBMERGED, because the tide sweeps whole flats over it and the shader — not a
        /// tile — decides which.
        /// </summary>
        public static readonly string[] GroundMaterials =
            { "grass", "marram", "sand", "ripple", "shingle", "shelf" };

        /// <summary>
        /// Columns of <c>Ground.png</c>: FOUR ADJACENT WORLD TILES, not four art variants (v7 shipped
        /// three). The rig's noise is a pure function of world coords, so neighbours butt seamlessly
        /// and a run never repeats — these four are a sample of that field and may be painted in any
        /// order. Re-bake from the rig for more.
        /// </summary>
        public const int GroundVariants = 4;

        /// <summary>Slice index into <c>Ground.png</c> for a material row + variant column.</summary>
        public static int GroundIndex(string material, int variant = 0) =>
            RowMajor(IndexOfOrThrow(GroundMaterials, material, "ground material"),
                     Clamp(variant, GroundVariants), GroundVariants);

        // ---- Fringe.png — 12 cols × 3 rows ---------------------------------------------------------

        /// <summary>
        /// Fringe rows: which material's ragged tongue this overlay draws. Only the three SOFT
        /// materials get one — you stamp it OVER the LOWER material's ground tile where two terrain
        /// types meet, and the higher material laps onto it.
        /// </summary>
        public static readonly string[] FringeMaterials = { "grass", "marram", "sand" };

        /// <summary>
        /// Fringe columns in contract order: 4 edges, 4 outer corners, 4 inner corners.
        /// ⚠ Compass letters are the kit's claim (see the class note) — the ORDER is what this file
        /// guarantees.
        /// </summary>
        public static readonly string[] FringePieces =
        {
            "edN", "edE", "edS", "edW",
            "coNE", "coSE", "coSW", "coNW",
            "inNE", "inSE", "inSW", "inNW",
        };

        /// <summary>Slice index into <c>Fringe.png</c> for a material row + piece column.</summary>
        public static int FringeIndex(string material, string piece) =>
            RowMajor(IndexOfOrThrow(FringeMaterials, material, "fringe material"),
                     IndexOfOrThrow(FringePieces, piece, "fringe piece"),
                     FringePieces.Length);

        // ---- Cliff.png — 10 cols × 3 rows, TWO OF WHICH ARE PADDING --------------------------------

        /// <summary>
        /// Cliff bands, top row down. A cliff of any height is <c>cap + mid×N + toe</c>. Strata key on
        /// the GLOBAL row Y, so bands painted at the same world height line up across a whole coast
        /// without hand-matching.
        /// </summary>
        public static readonly string[] CliffBands = { "cap", "mid", "toe" };

        /// <summary>
        /// The nine landform pieces shared by the cliff and the dune. Corners wrap a lit-W / shaded-E
        /// facet; side pieces are the thin edge-on strip; diagonals are 45° only (a kit limit carried
        /// over from v7).
        /// </summary>
        public static readonly string[] LandformPieces =
        {
            "faceS", "cornSW", "cornSE", "sideW", "sideE",
            "innSW", "innSE", "diagSW", "diagSE",
        };

        /// <summary>
        /// The cave arch, verbatim as the contract spells it. ⚠ The name carries its own restriction:
        /// it is <b>toe</b>+cave, and column 9 EXISTS ONLY ON THE TOE ROW — a sea cave gets its mouth
        /// at the waterline, so there is no cap-cave and no mid-cave to reach for.
        /// </summary>
        public const string CavePiece = "toe+cave";

        /// <summary>
        /// Cliff columns in contract order: the nine landform pieces, then the cave.
        /// ⚠ <b>This list is the RECTANGLE, not the tile set.</b> Column 9 is filled on the toe row
        /// only; on the cap and mid rows it is a fully transparent PADDING cell — verified on import,
        /// where cells 9 and 19 of both styles' sheets scanned 100% alpha-zero. Same trap as the road
        /// blob-47 atlas, whose 48th cell is spare: anything walking this sheet by its rectangle would
        /// paint empty air as a cliff. <see cref="CliffIndex"/> refuses the two padding cells rather
        /// than handing back an index that resolves to nothing.
        /// </summary>
        public static readonly string[] CliffPieces =
            LandformPieces.Concat(new[] { CavePiece }).ToArray();

        /// <summary>The two cells of <c>Cliff.png</c> that are padding, not tiles: cap+cave and
        /// mid+cave. Named so a test can assert they are transparent and a painter can skip them.</summary>
        public static readonly int[] CliffPaddingIndices =
        {
            0 * 10 + 9,   //  9 — cap row, cave column
            1 * 10 + 9,   // 19 — mid row, cave column
        };

        /// <summary>
        /// Slice index into <c>Cliff.png</c> for a band row + piece column.
        /// Throws for <c>cap</c>/<c>mid</c> + <see cref="CavePiece"/>: those cells are empty padding,
        /// and returning their index would silently paint a hole in a cliff face.
        /// </summary>
        public static int CliffIndex(string band, string piece)
        {
            int row = IndexOfOrThrow(CliffBands, band, "cliff band");
            int col = IndexOfOrThrow(CliffPieces, piece, "cliff piece");

            if (piece == CavePiece && band != "toe")
                throw new ArgumentOutOfRangeException(
                    nameof(piece),
                    $"'{CavePiece}' exists on the TOE row only — cell {RowMajor(row, col, CliffPieces.Length)} " +
                    $"('{band}' + cave) is transparent padding, not a tile. A sea cave is carved at the " +
                    "waterline; stack a plain cap/mid above the cave toe instead.");

            return RowMajor(row, col, CliffPieces.Length);
        }

        /// <summary>True if this flat slice index of <c>Cliff.png</c> is a padding cell.</summary>
        public static bool IsCliffPadding(int index) => CliffPaddingIndices.Contains(index);

        // ---- Dune.png — 9 cols × 2 rows ------------------------------------------------------------

        /// <summary>
        /// Dune bands. ⚠ <b>v7's dune was a single band; v8's stacks.</b> <c>cap</c> alone is a 1 m
        /// bank, <c>cap + toe</c> a 2 m one — so a v7 dune index is NOT a v8 dune index, which is the
        /// sharpest reason these two kits do not share a catalog.
        /// </summary>
        public static readonly string[] DuneBands = { "cap", "toe" };

        /// <summary>
        /// The dune's columns: the same nine landform pieces as the cliff, minus the cave — a dune has
        /// no arch. Unlike the cliff sheet there is no tenth column, so this rectangle is full.
        /// </summary>
        public static readonly string[] DunePieces = LandformPieces;

        /// <summary>Slice index into <c>Dune.png</c> for a band row + piece column.</summary>
        public static int DuneIndex(string band, string piece) =>
            RowMajor(IndexOfOrThrow(DuneBands, band, "dune band"),
                     IndexOfOrThrow(DunePieces, piece, "dune piece"),
                     DunePieces.Length);

        // ---- Contact.png — 5 cols × 1 row ----------------------------------------------------------

        /// <summary>
        /// The ambient-occlusion overlays v7 had none of: stamp one on the GROUND tile that a landform
        /// stands next to, and the landform seats into the ground instead of floating on it. Five
        /// pieces, all of the NORTHERN half — at a camera looking from the south, the south side of a
        /// landform is its own lit face and needs no contact shade.
        ///
        /// <para>⚠ These carry SOFT alpha (measured 92–200 across the two styles), unlike every other
        /// sheet in the kit, which is binary. That is the whole point of an AO overlay — and it is why
        /// the import lock's <c>Compression = None</c> is load-bearing here and not merely tidy: block
        /// compression quantises exactly this gradient.</para>
        /// </summary>
        public static readonly string[] ContactPieces = { "n", "ne", "e", "nw", "w" };

        /// <summary>Slice index into <c>Contact.png</c> — a single row, so the index IS the piece.</summary>
        public static int ContactIndex(string piece) =>
            IndexOfOrThrow(ContactPieces, piece, "contact piece");

        // ---- reading the contract back --------------------------------------------------------------

        /// <summary>
        /// One style's contract, parsed. Dictionary-shaped (sheets are keyed by FILENAME) and
        /// mixed-typed (<c>Ground.cols</c> is prose where the others are arrays), so
        /// <see cref="UnityEngine.JsonUtility"/> cannot read it — hence <see cref="MiniJson"/>.
        /// Returns null and logs if the file is missing or unparseable.
        /// </summary>
        public static Dictionary<string, object> LoadContract(string style)
        {
            string path = ContractPath(style);
            if (!File.Exists(path))
            {
                UnityEngine.Debug.LogError(
                    $"[ShorelineIso2Catalog] No contract at '{path}'. The sheets and this file ship " +
                    "together from one bake and are only meaningful together.");
                return null;
            }

            try
            {
                return MiniJson.Parse(File.ReadAllText(path)) as Dictionary<string, object>;
            }
            catch (FormatException e)
            {
                UnityEngine.Debug.LogError($"[ShorelineIso2Catalog] '{path}' is not valid JSON: {e.Message}");
                return null;
            }
        }

        /// <summary>
        /// The row list a contract publishes for one sheet, or null if that sheet declares none.
        /// (<c>Contact.png</c> is a single unnamed row and has only <c>cols</c>.)
        /// </summary>
        public static string[] ContractRows(Dictionary<string, object> contract, string sheet) =>
            StringList(SheetNode(contract, sheet), "rows");

        /// <summary>
        /// The column list a contract publishes for one sheet, or null if it publishes prose instead.
        /// <c>Ground.cols</c> is deliberately prose ("gx 0..3 (adjacent world tiles…)") because its
        /// columns are world positions, not named pieces — a caller wanting that count reads
        /// <see cref="GroundVariants"/>.
        /// </summary>
        public static string[] ContractCols(Dictionary<string, object> contract, string sheet) =>
            StringList(SheetNode(contract, sheet), "cols");

        /// <summary>The <c>[w,h]</c> cell size a contract publishes for one sheet.</summary>
        public static (int w, int h)? ContractCell(Dictionary<string, object> contract, string sheet)
        {
            var node = SheetNode(contract, sheet);
            var cell = node != null && node.TryGetValue("cell", out object v) ? v as List<object> : null;
            if (cell == null || cell.Count != 2) return null;
            return ((int)Math.Round((double)cell[0]), (int)Math.Round((double)cell[1]));
        }

        /// <summary>
        /// True if both styles' contracts describe the SAME geometry. The kit's promise is that a map
        /// authored against <c>nat</c> drops straight onto <c>gfx</c>; the moment a re-bake changes one
        /// style's grid and not the other, every map built on the pair is silently wrong. Cheap to
        /// check, so it is checked.
        /// </summary>
        public static bool ContractsAgreeOnGeometry(out string difference)
        {
            var a = LoadContract(Styles[0]);
            var b = LoadContract(Styles[1]);
            if (a == null || b == null) { difference = "a contract failed to load"; return false; }
            return ContractsAgreeOnGeometry(a, b, out difference);
        }

        /// <summary>
        /// The IO-free half of <see cref="ContractsAgreeOnGeometry(out string)"/>, comparing two
        /// already-parsed contracts. Split out so a test can feed it a deliberately mutated contract
        /// and prove the comparison actually bites — a geometry check that has never been shown to
        /// fail is decoration.
        /// </summary>
        public static bool ContractsAgreeOnGeometry(Dictionary<string, object> a,
                                                    Dictionary<string, object> b,
                                                    out string difference)
        {
            difference = null;
            if (a == null || b == null) { difference = "a contract failed to load"; return false; }

            foreach (string sheet in Sheets)
            {
                if (ContractCell(a, sheet) != ContractCell(b, sheet))
                {
                    difference = $"{sheet}: cell {ContractCell(a, sheet)} vs {ContractCell(b, sheet)}";
                    return false;
                }
                if (!SameList(ContractRows(a, sheet), ContractRows(b, sheet)))
                {
                    difference = $"{sheet}: row lists differ between styles";
                    return false;
                }
                if (!SameList(ContractCols(a, sheet), ContractCols(b, sheet)))
                {
                    difference = $"{sheet}: column lists differ between styles";
                    return false;
                }
            }
            return true;
        }

        // ---- shared helpers -------------------------------------------------------------------------

        /// <summary>Slice names are geometric: <c>&lt;stem&gt;_&lt;index&gt;</c>, row-major from the
        /// top-left — the same rule every slicer in this repo follows.</summary>
        public static string SliceName(string sheet, int index) => $"{sheet}_{index}";

        static Dictionary<string, object> SheetNode(Dictionary<string, object> contract, string sheet)
        {
            var sheets = MiniJson.Dict(contract, "sheets");
            return sheets != null && sheets.TryGetValue($"{RequireSheet(sheet)}.png", out object v)
                   ? v as Dictionary<string, object> : null;
        }

        static string[] StringList(Dictionary<string, object> node, string key)
        {
            var list = node != null && node.TryGetValue(key, out object v) ? v as List<object> : null;
            return list?.OfType<string>().ToArray();
        }

        static bool SameList(string[] a, string[] b) =>
            a == null ? b == null : b != null && a.SequenceEqual(b);

        static int RowMajor(int row, int col, int cols) => row * cols + col;

        static int Clamp(int v, int count) => v < 0 ? 0 : (v >= count ? count - 1 : v);

        static string RequireStyle(string style)
        {
            if (Array.IndexOf(Styles, style) < 0)
                throw new ArgumentOutOfRangeException(
                    nameof(style), $"'{style}' is not a kit style. Known: {string.Join(", ", Styles)}.");
            return style;
        }

        static string RequireSheet(string sheet)
        {
            if (Array.IndexOf(Sheets, sheet) < 0)
                throw new ArgumentOutOfRangeException(
                    nameof(sheet), $"'{sheet}' is not a kit sheet. Known: {string.Join(", ", Sheets)}.");
            return sheet;
        }

        static int IndexOfOrThrow(string[] table, string key, string what)
        {
            int i = Array.IndexOf(table, key);
            if (i < 0)
                throw new ArgumentOutOfRangeException(
                    nameof(key), $"'{key}' is not a known {what}. Known: {string.Join(", ", table)}.");
            return i;
        }
    }
}
#endif
