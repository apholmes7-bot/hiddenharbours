#if UNITY_EDITOR
using System;
using System.Linq;

namespace HiddenHarbours.Art.Editor
{
    /// <summary>
    /// The SEMANTIC map for the shoreline-ISO and road/path tile kits: which slice index of which sheet
    /// is "sand, edge-north" or "cliff mid, cave toe".
    ///
    /// <para><b>Why this is a separate class from the slicer.</b> Slice names in this repo state
    /// GEOMETRY — <c>ShoreIsoCliff_7</c> means "the 8th cell, row-major from the top-left" and nothing
    /// else. That rule exists because compass-labelled art has been mislabelled in five separate kits
    /// here (the counter-clockwise bake), and a name that claims a direction is a name that can lie.
    /// So the sheets are sliced by position and the meaning is resolved HERE, in one place, against the
    /// kit's own <c>ShorelineIso.json</c> contract — which is the art director's source of truth and
    /// ships beside the PNGs.</para>
    ///
    /// <para><b>⚠ The compass labels in <see cref="CliffPieces"/> / <see cref="FringePieces"/> are the
    /// kit's CLAIM, not a measurement.</b> Unlike the boat turntables there is no azimuth probe for a
    /// static tile — nothing here has been checked against rendered pixels. They are laid out in the
    /// contract's column order, so the INDEX arithmetic is sound whatever the labels turn out to mean;
    /// if a painted cliff faces the wrong way in-scene, the fix is to correct the label→column mapping
    /// in this file (and the kit README), never to renumber the slices.</para>
    ///
    /// <para>Water is deliberately absent from every list below. The kit bakes none (ADR 0010/0012/0023):
    /// the shader owns the waterline, foam and swash, and the tide sweeps over whatever ground is
    /// painted. There is no wet-edge tile to look up because there is no wet-edge tile.</para>
    /// </summary>
    public static class ShorelineIsoCatalog
    {
        public const string TilesetRoot = "Assets/_Project/Art/Tilesets";
        public const string ShorelineIsoDir = TilesetRoot + "/ShorelineIso";
        public const string RoadsDir = TilesetRoot + "/Roads";

        /// <summary>The kit's contract sidecar, shipped beside the sheets. The lists below mirror it.</summary>
        public const string ContractPath = ShorelineIsoDir + "/ShorelineIso.json";

        /// <summary>Every sheet in both kits is a 32 px cell — the kit grid, and 1 m at the locked PPU.</summary>
        public const int Cell = 32;

        // ---- ShoreIsoGround.png — 3 cols × 6 rows -------------------------------------------------

        /// <summary>
        /// Ground rows, top to bottom. Opaque terrain fill; each material is authored to read right
        /// DRY AND SUBMERGED because the tide sweeps whole flats over it.
        /// </summary>
        public static readonly string[] GroundMaterials =
            { "grass", "marram", "sand", "ripple", "shingle", "shelf" };

        /// <summary>
        /// Columns of <c>ShoreIsoGround.png</c>: three ADJACENT world tiles, not three art variants.
        /// The rig's noise is a pure function of world coords (gx,gy), so neighbours butt seamlessly and
        /// a run never repeats — these three are a sample of that field, and painting them in any order
        /// is fine. Re-bake from <c>shoreIsoKitRig.js</c> for more.
        /// </summary>
        public const int GroundVariants = 3;

        /// <summary>Slice index into <c>ShoreIsoGround.png</c> for a material row + variant column.</summary>
        public static int GroundIndex(string material, int variant = 0)
            => RowMajor(IndexOfOrThrow(GroundMaterials, material, "ground material"),
                        Clamp(variant, GroundVariants), GroundVariants);

        // ---- ShoreIsoFringe.png — 12 cols × 3 rows ------------------------------------------------

        /// <summary>
        /// Fringe rows: which material's ragged tongue this overlay draws. Only the three SOFT
        /// materials get a fringe — you stamp it OVER the neighbour's ground tile where two terrain
        /// types meet. (grass/marram carry a 1 px soil under-shadow on camera-facing edges.)
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

        /// <summary>Slice index into <c>ShoreIsoFringe.png</c> for a material row + piece column.</summary>
        public static int FringeIndex(string material, string piece)
            => RowMajor(IndexOfOrThrow(FringeMaterials, material, "fringe material"),
                        IndexOfOrThrow(FringePieces, piece, "fringe piece"),
                        FringePieces.Length);

        // ---- ShoreIsoCliff.png — 10 cols × 3 rows -------------------------------------------------

        /// <summary>
        /// Cliff bands, top row down. A cliff of any height is <c>cap + mid×N + toe</c> — roughly 1.3 m
        /// of drawn face per band at the 40° camera. Strata key on the GLOBAL row Y, so bands painted at
        /// the same world height line up across a whole coast without hand-matching.
        /// </summary>
        public static readonly string[] CliffBands = { "cap", "mid", "toe" };

        /// <summary>
        /// Cliff columns in contract order. The first nine are the shared landform pieces (also the
        /// dune's nine); <c>caveToe</c> is cliff-only — it is the arch carved into a toe band, which is
        /// how a sea cave gets its mouth. Corners wrap a lit-W / shaded-E facet; side pieces are the
        /// thin edge-on strip; diagonals are 45° only (kit limit v7).
        /// </summary>
        public static readonly string[] CliffPieces =
        {
            "faceS", "cornSW", "cornSE", "sideW", "sideE",
            "innSW", "innSE", "diagSW", "diagSE", "caveToe",
        };

        /// <summary>Slice index into <c>ShoreIsoCliff.png</c> for a band row + piece column.</summary>
        public static int CliffIndex(string band, string piece)
            => RowMajor(IndexOfOrThrow(CliffBands, band, "cliff band"),
                        IndexOfOrThrow(CliffPieces, piece, "cliff piece"),
                        CliffPieces.Length);

        // ---- ShoreIsoDune.png — 9 cols × 1 row ----------------------------------------------------

        /// <summary>
        /// The marram dune bank: ONE band, no cap/mid/toe stack, and the same nine corner/edge pieces
        /// as the cliff minus <c>caveToe</c> (a dune has no cave). Slice index IS the piece index.
        /// </summary>
        public static readonly string[] DunePieces = CliffPieces.Take(9).ToArray();

        /// <summary>Slice index into <c>ShoreIsoDune.png</c> for a piece.</summary>
        public static int DuneIndex(string piece) => IndexOfOrThrow(DunePieces, piece, "dune piece");

        // ---- ShoreIsoSprites.png — packed, irregular ----------------------------------------------

        /// <summary>
        /// Freestanding pure-rock sprites: four sea stacks then three slab boulders. NOT a grid — each
        /// item has its own rect and base-centre pivot in <c>ShoreIsoSprites.json</c>, which is what
        /// <see cref="ShorelineIsoSpriteSlicer"/> slices from. Named, not indexed, for that reason.
        /// </summary>
        public static readonly string[] RockSprites = { "reef", "s", "m", "l", "bs", "bm", "bl" };

        // ---- RoadIso_<surface>_new_blob47.png — 12 cols × 4 rows ----------------------------------

        /// <summary>
        /// The seven road/path surfaces, one pre-baked atlas each at <c>new</c> wear over a grass verge
        /// with no markings. <c>worn</c>/<c>cracked</c> wear, dirt/sand verges and lane markings are all
        /// live in <c>roadPathRig.js</c> — re-bake rather than hand-editing a sheet.
        /// </summary>
        public static readonly string[] RoadSurfaces =
            { "dirt", "gravel", "concrete", "asphalt", "cobble", "sand", "brick" };

        /// <summary>Atlas columns/rows: 12 × 4 = 48 cells holding 47 tiles + one spare.</summary>
        public const int RoadCols = 12;
        public const int RoadRows = 4;

        /// <summary>
        /// The canonical blob-autotiler set is 47 tiles, but the atlas is a 12×4 = 48-cell rectangle,
        /// so cell index 47 is PADDING and not a road tile. Anything walking the atlas must stop here.
        /// </summary>
        public const int RoadBlobCount = 47;

        /// <summary>
        /// Asset path of a surface's pre-baked blob-47 atlas. Throws on an unknown surface rather than
        /// handing back a path that silently doesn't exist.
        /// </summary>
        public static string RoadAtlasPath(string surface)
        {
            IndexOfOrThrow(RoadSurfaces, surface, "road surface");
            return $"{RoadsDir}/RoadIso_{surface}_new_blob47.png";
        }

        // ---- shared helpers -----------------------------------------------------------------------

        /// <summary>Slice names are geometric: <c>&lt;stem&gt;_&lt;index&gt;</c>, row-major from top-left.</summary>
        public static string SliceName(string sheetStem, int index) => $"{sheetStem}_{index}";

        static int RowMajor(int row, int col, int cols) => row * cols + col;

        static int Clamp(int v, int count) => v < 0 ? 0 : (v >= count ? count - 1 : v);

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
