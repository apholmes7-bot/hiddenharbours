#if UNITY_EDITOR
using System;
using UnityEngine;

namespace HiddenHarbours.Art.Editor
{
    /// <summary>
    /// The ROAD / PATH / SIDEWALK kit v3 — its identity in one place: cell geometry, the bake
    /// parameters that were SOLVED against the drop's reference atlases, the pivot, and the
    /// top/skirt split that the two-pass composite rule forces.
    ///
    /// <para><b>v3 supersedes v1, it does not extend it.</b> v1 (<c>RoadIso_&lt;surface&gt;_new_blob47.png</c>,
    /// 7 surfaces, 32×32 cells) drew its height at 12 px/m — a kerb was 1.5 px and nothing cast a
    /// shadow. v3 re-bases the family on <b>ZS = cos(40°)·32 ≈ 24.5 px/m</b>, the same ruler as every
    /// building wall and ShoreIso2 cliff band, so a sidewalk kerb is 4 px and a quay apron drops the
    /// 16 px face wharfKit's own quay cap shows. Two road families at different vertical scales in one
    /// village is a bug that only the eye would ever catch, so v1's sheets are retired by the same PR
    /// that lands these. v1 had no consumer — nothing painted with it — which is what made the
    /// retirement cheap. <c>docs/art/rigs/roadPathRig.js</c> stays as history.</para>
    ///
    /// <para><b>Meaning lives here, not in slice names.</b> Same rule and same reason as
    /// <see cref="ShorelineIsoCatalog"/>: slices are named by GEOMETRY (<c>&lt;stem&gt;_&lt;index&gt;</c>)
    /// and what index 23 depicts is resolved in this file, so a wrong claim is a one-line fix rather
    /// than a re-slice.</para>
    /// </summary>
    public static class RoadKitV3
    {
        // ---- the rig ------------------------------------------------------------------------------

        /// <summary>
        /// The art director's file, in-repo and run UNMODIFIED (ADR 0021 §5).
        /// </summary>
        public const string RigScriptPath = "docs/art/rigs/road-path-kit-v3/roadPathRig3.js";

        /// <summary>The global the rig's IIFE installs.</summary>
        public const string RigGlobalName = "RoadKit3";

        /// <summary>
        /// sha256 of <see cref="RigScriptPath"/>, LF-normalised — the number git stores and the one
        /// that is the same on every platform. Pinned so a silently-edited rig fails a test instead of
        /// re-baking different pixels under the same name.
        /// </summary>
        public const string RigSha256Lf =
            "d45e9ac657eb42daf5dc22f61068bf0ff1f0ee65152d6a4ca827a77ef0a5ee3c";

        /// <summary>
        /// ⭐ <b>DELIBERATELY ABSENT FROM <see cref="RigCatalog"/>.</b> That struct is built around
        /// <c>AzimuthConvention</c> for 8-direction turntables and <b>a road tile has no heading</b> —
        /// its atlas axes are neighbour-mask × nothing. Same call, same reason, as the shore-plant and
        /// grass-library kits, whose bakers say so in the same words. Nothing here calls
        /// <c>RigBaker.DirForCell</c> and no azimuth probe can be run against a static tile.
        /// </summary>
        public const string WhyNoRigCatalogEntry =
            "no heading — blob-47 tiles are indexed by neighbour mask, not by facing";

        // ---- cell geometry (READ FROM THE RIG at bake time; these are the cross-check) ------------
        //
        // The rig exports TILE 32 · CELL_H 64 · HEAD 10 · SKIRT 42 · ZS 24.5. RoadKitSheetBaker asserts
        // every one of them against the loaded rig rather than trusting this table — ADR 0021 §4 says
        // cell geometry comes from the rig, not from a README, and this kit is the case that proves it
        // (v1's README and v1's rig disagreed about nothing only because v1 had no faces).

        /// <summary>Cell width in pixels. One cell is one square metre of ground.</summary>
        public const int Tile = 32;

        /// <summary>Cell height in pixels — 2× the width, to carry headroom and the skirt.</summary>
        public const int CellH = 64;

        /// <summary>
        /// Rows 0..9 are HEADROOM above the ground square: a boardwalk deck stands 0.35 m ≈ 9 px proud
        /// and would otherwise be clipped off the top of its own cell.
        /// </summary>
        public const int Head = 10;

        /// <summary>
        /// Row 42 is where the SKIRT starts: rows 42..63 carry the south drop faces, kerb faces and the
        /// baked alpha contact shadows. The skirt of a cell overlaps the ground square of the cell
        /// SOUTH of it — which is the whole reason for <see cref="TopRectHeight"/> below.
        /// </summary>
        public const int Skirt = Head + Tile;   // 42

        /// <summary>Vertical scale: px per metre. cos(40°)·32 — the turntable's own ruler.</summary>
        public const float ZScale = 24.5f;

        /// <summary>The ground square (z = 0) occupies cell-local rows <see cref="Head"/>..41.</summary>
        public const int GroundRows = Tile;     // 32

        // ---- atlas layout ------------------------------------------------------------------------

        /// <summary>Atlas columns/rows: 12 × 4 = 48 cells holding 47 tiles + one spare.</summary>
        public const int Cols = 12;
        public const int Rows = 4;

        /// <summary>
        /// The canonical blob autotiler set is 47 tiles, but the atlas is a 12×4 = 48-cell rectangle,
        /// so cell index 47 is PADDING and not a road tile. Anything walking the atlas stops here.
        /// </summary>
        public const int BlobCount = 47;

        public const int SheetWidth = Cols * Tile;    // 384
        public const int SheetHeight = Rows * CellH;  // 256

        /// <summary>
        /// The eleven surfaces, in the rig's own <c>SURFACES</c> order. The baker cross-checks this
        /// against the rig and refuses on a mismatch, so a rig that gains a surface fails loudly here
        /// instead of silently shipping ten sheets and a stale table.
        /// </summary>
        public static readonly string[] Surfaces =
        {
            "dirt", "oiledDirt", "gravel", "shell", "sand",
            "concrete", "apron", "asphalt", "cobble", "brick", "boardwalk",
        };

        /// <summary>Where the baked atlases land.</summary>
        public const string RoadsDir = "Assets/_Project/Art/Tilesets/RoadsV3";

        /// <summary>
        /// The kit's contract — the 47 neighbour masks in ATLAS ORDER, exported FROM the rig at bake
        /// time. See <c>RoadKitSheetBaker.WriteContract</c> for why C# must never re-derive it.
        /// </summary>
        public const string ContractPath = "docs/art/rigs/road-path-kit-v3/road.contract.json";

        // ---- the neighbour mask ---------------------------------------------------------------------
        //
        // The rig's own bit assignment (BIT in roadPathRig3.js), restated here ONLY so a caller can
        // BUILD a mask. Resolving a mask to an atlas index is the contract's job, never ours.

        public const int BitN = 1, BitE = 2, BitS = 4, BitW = 8;
        public const int BitNE = 16, BitSE = 32, BitSW = 64, BitNW = 128;

        /// <summary>
        /// Build the raw neighbour mask for a cell. Diagonals are only meaningful when BOTH their
        /// adjacent cardinals are set; the rig's <c>canon()</c> folds the rest away, and the contract
        /// is keyed on the CANONICAL mask — so a caller passes the raw mask to
        /// <c>RoadKitContract.IndexForMask</c> and lets the lookup canonicalise.
        /// </summary>
        public static int Mask(bool n, bool e, bool s, bool w,
                              bool ne = false, bool se = false, bool sw = false, bool nw = false)
        {
            int m = 0;
            if (n) m |= BitN; if (e) m |= BitE; if (s) m |= BitS; if (w) m |= BitW;
            if (ne) m |= BitNE; if (se) m |= BitSE; if (sw) m |= BitSW; if (nw) m |= BitNW;
            return m;
        }

        /// <summary>Asset path of a surface's baked blob-47 atlas.</summary>
        public static string AtlasPath(string surface)
        {
            if (Array.IndexOf(Surfaces, surface) < 0)
                throw new ArgumentException($"unknown road surface '{surface}'", nameof(surface));
            return $"{RoadsDir}/RoadIso3_{surface}_new_blob47.png";
        }

        // ---- the SOLVED bake parameters -----------------------------------------------------------
        //
        // ⭐ MEASURED, NOT READ OFF THE README. Every one of these was solved by running the rig
        // unmodified in the repo's own ClearScript V8 and comparing to the drop's eleven reference
        // atlases; the combination below is the ONLY one in the swept space that reproduces all
        // eleven. See docs/art/rigs/road-path-kit-v3/IMPORT.md for the run and the sabotage check.

        /// <summary>Wear state of the shipped atlases. <c>worn</c>/<c>cracked</c> re-bake from the rig.</summary>
        public const string BakedWear = "new";

        /// <summary>Piece of the shipped atlases. <c>apron</c>/<c>slipway</c>/… re-bake from the rig.</summary>
        public const string BakedPiece = "road";

        /// <summary>
        /// Profile of the shipped atlases.
        ///
        /// <para>⚠️ <b>PROVABLY undetermined by the sheets</b> — and that is a fact about the rig, not a
        /// gap in the measurement. <c>PROFILE_Z</c> gives <c>lane</c> and <c>road2</c> the SAME 0.02 m
        /// lift, and the shipped atlases carry no markings, so the two render identically down to the
        /// byte. The README declares "lane profile", so that is what is baked; a future markings or
        /// wear variant WOULD distinguish them, which is why this is recorded rather than shrugged at.</para>
        /// </summary>
        public const string BakedProfile = "lane";

        /// <summary>
        /// Spill substrate of the shipped atlases. ⚠️ Also provably undetermined: the rig's
        /// <c>SPILL</c> table maps <c>grass</c> and <c>dirt</c> to the same <c>{ramp:DIRT,i:2}</c>
        /// entry. The README declares grass-soil spill, so that is what is baked.
        /// </summary>
        public const string BakedGround = "grass";

        /// <summary>
        /// Pattern seed. ⚠️ <b>Passing 0 does NOT give you seed 0</b> — the rig resolves it as
        /// <c>(opts.seed|0)||7</c>, so a caller who passes 0 (or omits it) silently gets 7. Recorded
        /// because a placement pass that wants an unseeded look cannot get one by passing zero.
        /// </summary>
        public const int BakedSeed = 7;

        /// <summary>
        /// Pattern-space step between atlas cells, in metres.
        ///
        /// <para>The reference atlases place cell (col,row) at <c>gx = 2·col, gy = 2·row</c> — two
        /// metres apart, not one, so neighbouring cells of a REFERENCE sheet never share a patch of the
        /// global-metre pattern and the sheet reads as 47 samples rather than one continuous road.
        /// Solved per cell: with a one-metre step the sheet misses by ~106 k bytes.</para>
        /// </summary>
        public const int PatternStep = 2;

        /// <summary>
        /// Whether the bake passes each cell's <c>axis</c> explicitly from <c>BLOB47[i].axis</c>.
        ///
        /// <para>⚠️ It must. <c>render()</c> derives its own axis when none is given, and the two
        /// disagree for the isolated and cap cells (<c>fromMask</c> returns <c>'i'</c> where the
        /// fallback returns <c>'h'</c>). Measured: deriving instead of passing leaves <b>628 fully
        /// opaque pixels</b> wrong across the eleven sheets — small, silent, and exactly the kind of
        /// thing that reads as "the art is a bit off" forever.</para>
        /// </summary>
        public const bool PassAxisFromMask = true;

        // ---- pivot + the top/skirt split ----------------------------------------------------------

        /// <summary>
        /// The pivot row, measured from the cell's TOP in pixels: the centre of the ground square,
        /// <c>Head + Tile/2 = 26</c>. Every sprite this kit slices — top and skirt alike — pins this
        /// same world point, so the two tilemaps register with each other and with the terrain.
        /// </summary>
        public const float PivotRowFromTop = Head + Tile / 2f;   // 26

        /// <summary>
        /// The whole-cell pivot in Unity's bottom-left normalized space, per ADR 0026's
        /// <c>(H − pivotY)/H</c>: (64 − 26)/64 = 0.59375.
        /// </summary>
        public static readonly Vector2 CellPivot =
            new Vector2(0.5f, (CellH - PivotRowFromTop) / CellH);

        /// <summary>
        /// Height of the TOP sub-sprite: cell rows 0..41 — headroom plus the ground square, everything
        /// that belongs to this cell's own plan square.
        /// </summary>
        public const int TopRectHeight = Skirt;          // 42

        /// <summary>Height of the SKIRT sub-sprite: cell rows 42..63.</summary>
        public const int SkirtRectHeight = CellH - Skirt; // 22

        /// <summary>
        /// ⭐ <b>WHY EVERY CELL SLICES INTO TWO SPRITES.</b>
        ///
        /// <para>The kit's composite rule is two-pass, painter N→S: <i>all</i> tops first, then
        /// <i>all</i> skirts. That is not what any single <c>Tilemap</c> does at any
        /// <c>TilemapRenderer.SortOrder</c>. Painter N→S over whole 32×64 sprites draws the southern
        /// cell AFTER the northern one, and the southern cell's ground square lands exactly on top of
        /// the northern cell's skirt — so every kerb face and every drop face would be sliced off by
        /// its own neighbour. Measured from the geometry: a cell's skirt occupies 16..38 px below its
        /// pivot and the next cell south occupies 16..48 px below that same pivot, so they overlap
        /// over the skirt's full 22 px.</para>
        ///
        /// <para>So a road is TWO tilemaps sharing one cell grid — a top layer and a skirt layer on
        /// adjacent sorting orders — exactly the way <c>StPetersShorePainter</c> already stacks
        /// ShoreGround/ShoreFringe/ShoreContact. Both sprites carry the SAME pivot world point, which
        /// is what keeps the two layers registered; the skirt's pivot therefore sits 16 px ABOVE its
        /// own rect (a normalized y of 38/22 ≈ 1.727), which is legal and deliberate.</para>
        /// </summary>
        public const string TopSuffix = "top";

        /// <inheritdoc cref="TopSuffix"/>
        public const string SkirtSuffix = "skirt";

        /// <summary>Pivot of a TOP sub-sprite, normalized to its own 32×42 rect.</summary>
        public static readonly Vector2 TopPivot =
            new Vector2(0.5f, (TopRectHeight - PivotRowFromTop) / TopRectHeight);

        /// <summary>
        /// Pivot of a SKIRT sub-sprite, normalized to its own 32×22 rect. Greater than 1 on purpose:
        /// the point it pins is the ground-square centre, which is above the skirt.
        /// </summary>
        public static readonly Vector2 SkirtPivot =
            new Vector2(0.5f, (CellH - PivotRowFromTop) / SkirtRectHeight);

        // ---- where a road SORTS ----------------------------------------------------------------------

        /// <summary>
        /// Sorting order of the road's TOP tilemap.
        ///
        /// <para><b>These tiles are GROUND and sort under everything — no YSort games.</b> The band
        /// sits just ABOVE <c>StPetersShorePainter</c>'s three shore layers (ShoreGround −20 ·
        /// ShoreFringe −19 · ShoreContact −18), because a lane is laid ON the terrain the shore
        /// painter draws — and well BELOW the Sea plane at −5, so the tide reveal keeps working
        /// exactly as it does over painted ground: the layered <c>WaterSurface</c> shader clips itself
        /// transparent wherever the authored ground is above the live water level (ADR 0010/0012), so
        /// a road that floods is covered by depth-graded water rather than fighting it.</para>
        ///
        /// <para>Nothing in this kit ever takes a <c>YSortSprite</c>. A road does not sort against the
        /// player — the player walks on it.</para>
        /// </summary>
        public const int TopSortingOrder = -17;

        /// <summary>
        /// Sorting order of the road's SKIRT tilemap — exactly one above <see cref="TopSortingOrder"/>.
        /// That single step IS the two-pass rule: every skirt draws after every top, so a kerb or drop
        /// face is never covered by the flat ground square of the cell to its south.
        /// </summary>
        public const int SkirtSortingOrder = TopSortingOrder + 1;

        /// <summary>Conventional name for the top tilemap object a placement pass creates.</summary>
        public const string TopLayerName = "RoadTop";

        /// <inheritdoc cref="TopLayerName"/>
        public const string SkirtLayerName = "RoadSkirt";

        /// <summary>Slice name for a cell's top sprite, e.g. <c>RoadIso3_dirt_new_blob47_23_top</c>.</summary>
        public static string TopSpriteName(string stem, int index) => $"{stem}_{index}_{TopSuffix}";

        /// <summary>Slice name for a cell's skirt sprite.</summary>
        public static string SkirtSpriteName(string stem, int index) => $"{stem}_{index}_{SkirtSuffix}";

        /// <summary>Atlas column of blob index <paramref name="index"/> (row-major from top-left).</summary>
        public static int ColOf(int index) => index % Cols;

        /// <summary>Atlas row of blob index <paramref name="index"/>.</summary>
        public static int RowOf(int index) => index / Cols;

        /// <summary>Pattern-space X the bake passes for blob index <paramref name="index"/>.</summary>
        public static int PatternX(int index) => PatternStep * ColOf(index);

        /// <summary>Pattern-space Y the bake passes for blob index <paramref name="index"/>.</summary>
        public static int PatternY(int index) => PatternStep * RowOf(index);
    }
}
#endif
