#if UNITY_EDITOR
using System;

namespace HiddenHarbours.Art.Editor
{
    /// <summary>
    /// The SEMANTIC map for the wharf / dock tile kit: which slice index of <c>WharfAtlas.png</c> is
    /// "tall pier, south edge", and which of <c>WharfBreakwaters.png</c> is "timber crib, 45° run".
    ///
    /// <para>Same split as <see cref="ShorelineIsoCatalog"/>, for the same reason: the sheets are sliced
    /// by GRID POSITION (<c>WharfAtlas_37</c> = the 38th cell, row-major from the top-left) and the
    /// meaning is resolved here, in one file, against the kit's own <c>WharfAtlas.json</c> /
    /// <c>WharfBreakwaters.json</c> contracts. A slice name that claimed a compass direction would be a
    /// slice name that could lie, and this repo has shipped mislabelled directional art five times.</para>
    ///
    /// <para><b>⚠ The compass letters here ARE load-bearing, and they are the kit's claim.</b> Unlike the
    /// shoreline tiles — where a wrong label just faces a cliff the wrong way — the wharf atlas's edge
    /// variants encode which side <b>drops a vertical face over the water</b>. The kit's convention is
    /// that the camera looks from the SOUTH, so only S (and the SE/SW diagonals) carry a tall face; N/E/W
    /// open edges get a raised curb only. If those letters are rotated, decks will grow faces on their
    /// landward side and none on the water side, which is loud and obvious the first time a wharf is
    /// painted. Nothing has verified them against pixels; fix the mapping here, never the slice
    /// numbering.</para>
    /// </summary>
    public static class WharfKitCatalog
    {
        public const string WharfDir = ShorelineIsoCatalog.TilesetRoot + "/Wharf";

        public const string AtlasPath = WharfDir + "/WharfAtlas.png";
        public const string BreakwatersPath = WharfDir + "/WharfBreakwaters.png";
        public const string OverlaysPath = WharfDir + "/WharfOverlays.png";

        // ---- WharfAtlas.png — 17 cols × 7 rows of 32×56 ------------------------------------------

        /// <summary>Deck tile width — one metre at the locked PPU, same grid as the ground tiles.</summary>
        public const int DeckTileWidth = 32;

        /// <summary>
        /// Full cell height. <b>32 deck + 24 face</b> — the extra 24 rows are the vertical face and
        /// waterline foam, which hang BELOW the tile the deck occupies. This is why the atlas pivots
        /// top-left instead of centre.
        /// </summary>
        public const int DeckCellHeight = 56;

        /// <summary>Rows of the deck the tile itself covers; the rest overhangs.</summary>
        public const int DeckHeight = 32;

        /// <summary>Rows of face + foam that hang past the tile, over whatever is drawn below.</summary>
        public const int FaceHeight = 24;

        /// <summary>
        /// Deck materials, in row order. <b>The float occupies FOUR rows, not one</b> — see
        /// <see cref="FloatBobFrames"/>. Use <see cref="AtlasRow"/> rather than indexing this directly.
        /// </summary>
        public static readonly string[] DeckMaterials = { "quay", "lowpier", "tallpier", "float" };

        /// <summary>
        /// The float's bob loop: 4 frames at ~6 fps, vertical offsets 0, −1, 0, +1 px. The three fixed
        /// materials have no frames — a floating dock moves with the water, a concrete quay does not,
        /// and that difference is the kit's one piece of animation.
        /// </summary>
        public const int FloatBobFrames = 4;

        /// <summary>
        /// Auto-tile variants, in column order: centre · 4 straight edges · 4 outer corners · 4 end caps
        /// (three sides open, deck connects on one) · 4 diagonal 45° cuts.
        /// <b>An "open" side is one that drops to water.</b> S-ish sides get the tall face; N/E/W get a
        /// curb only (single-camera convention — see the class note).
        /// </summary>
        public static readonly string[] DeckVariants =
        {
            "ctr",
            "edN", "edE", "edS", "edW",
            "coNE", "coSE", "coSW", "coNW",
            "capN", "capS", "capE", "capW",
            "diNE", "diSE", "diSW", "diNW",
        };

        /// <summary>Columns of the atlas — one per auto-tile variant.</summary>
        public static int AtlasCols => DeckVariants.Length;

        /// <summary>
        /// Rows of the atlas: quay, lowpier, tallpier, then the float's four bob frames = 7.
        /// </summary>
        public static int AtlasRows => (DeckMaterials.Length - 1) + FloatBobFrames;

        /// <summary>
        /// Which atlas ROW a material sits on. The three fixed materials are rows 0–2; the float's
        /// <paramref name="bobFrame"/> selects among rows 3–6. Passing a bob frame for a fixed material
        /// throws rather than silently ignoring it — a caller animating a concrete quay has a bug.
        /// </summary>
        public static int AtlasRow(string material, int bobFrame = 0)
        {
            int m = IndexOfOrThrow(DeckMaterials, material, "deck material");

            if (m < DeckMaterials.Length - 1)
            {
                if (bobFrame != 0)
                    throw new ArgumentOutOfRangeException(
                        nameof(bobFrame),
                        $"'{material}' is a FIXED deck and has no bob frames — only 'float' animates.");
                return m;
            }

            if (bobFrame < 0 || bobFrame >= FloatBobFrames)
                throw new ArgumentOutOfRangeException(
                    nameof(bobFrame), $"float bob frame must be 0..{FloatBobFrames - 1}, got {bobFrame}.");

            return (DeckMaterials.Length - 1) + bobFrame;
        }

        /// <summary>Slice index into <c>WharfAtlas.png</c> for a material + variant (+ float bob frame).</summary>
        public static int AtlasIndex(string material, string variant, int bobFrame = 0)
            => AtlasRow(material, bobFrame) * AtlasCols
               + IndexOfOrThrow(DeckVariants, variant, "deck variant");

        // ---- WharfBreakwaters.png — 3 cols × 4 rows of 48×60 --------------------------------------

        /// <summary>
        /// Armour types, in row order: rock rubble mound · timber crib (log box filled with stone) ·
        /// concrete wave wall with a recurve crest · steel sheet pile with a capping beam and bollards.
        /// Roughly an age/means gradient — a small community wharf builds crib and riprap; sheet pile is
        /// what money and machinery look like.
        /// </summary>
        public static readonly string[] ArmourTypes = { "riprap", "crib", "wall", "sheet" };

        /// <summary>Armour variants, in column order: tileable E–W run · 45° turn · battered end cap.</summary>
        public static readonly string[] ArmourVariants = { "straight", "diag", "end" };

        /// <summary>Slice index into <c>WharfBreakwaters.png</c> for an armour type + variant.</summary>
        public static int BreakwaterIndex(string type, string variant)
            => IndexOfOrThrow(ArmourTypes, type, "armour type") * ArmourVariants.Length
               + IndexOfOrThrow(ArmourVariants, variant, "armour variant");

        // ---- WharfOverlays.png — packed, named ----------------------------------------------------

        /// <summary>
        /// The 14 deck fittings, by sidecar name. NOT a grid — each has its own rect and its own KIND of
        /// pivot (see <see cref="WharfOverlaySlicer"/>), which is why they are named rather than indexed.
        /// <para>The rail suffixes do encode orientation: <c>_h</c> runs along an N/S edge, <c>_v</c>
        /// along an E/W edge, <c>_d</c> along a 45° edge. That is the kit's claim, unverified against
        /// pixels; if rails land across their edges instead of along them, correct it here.</para>
        /// </summary>
        public static readonly string[] Fittings =
        {
            "rail_wood_h", "rail_wood_v", "rail_wood_d",
            "rail_pipe_h", "rail_pipe_v", "rail_pipe_d",
            "cleat", "bollard", "ring", "dolphin",
            "ladder", "tyre", "pilehead", "gangway",
        };

        /// <summary>Slice name of a fitting, as <see cref="WharfOverlaySlicer"/> writes it.</summary>
        public static string FittingSlice(string fitting)
        {
            IndexOfOrThrow(Fittings, fitting, "wharf fitting");
            return $"WharfOverlays_{fitting}";
        }

        // ---- shared -------------------------------------------------------------------------------

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
