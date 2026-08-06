namespace HiddenHarbours.Core
{
    /// <summary>
    /// THE ONE PLACE the game's sprite sorting-order axis is partitioned. Every renderer in Hidden Harbours
    /// — painted seabed, sea, wharf deck, decor, characters, rain, the day/night tint — shares a SINGLE
    /// sorting layer ("Default" is the only one <c>TagManager.asset</c> defines), so a sprite's order is its
    /// only claim to a place in the stack. Before this file that partition lived in COMMENTS, restated in
    /// about ten files that each solved against a number they could not see; this class makes it code, so a
    /// tier can be moved once and a test can assert the tiers do not overlap. (ADR 0032.)
    ///
    /// <para><b>Why it lives in Core.</b> Art owns <c>YSortSprite</c>, but Boats (mooring rope) and Fishing
    /// (trap-haul rope) also need to sit above the decor band, and neither references Art — rule 4 sends
    /// shared contracts through Core.</para>
    ///
    /// <para><b>Reading the ladder.</b> Low draws first (behind), high draws last (in front). The
    /// <b>decor band</b> is the only stretch that is COMPUTED rather than assigned: <c>YSortSprite</c> maps
    /// world Y across it, so anything wanting to be reliably under or over all decor must sit outside it.
    /// Everything below <see cref="DecorFloor"/> is untouched by the band's width and always has been —
    /// that is exactly why the 2026-08-05 re-base could widen the band without re-deriving the deck, the
    /// sea or the seabed.</para>
    /// </summary>
    public static class SortingBands
    {
        // --- BELOW THE DECOR BAND ---------------------------------------------------------------------
        // These are the numbers the wharf kit, the water stack and the interiors already solved against.
        // The decor band's floor is pinned at 2 precisely so this whole region keeps its meaning; moving
        // any of it means re-deriving the others, which is the work the re-base was able to avoid.

        /// <summary>Painted seabed, deepest rung — under the sea plane, seen through the wet-dry clip.</summary>
        public const int PaintedSeabedMin = -21;
        /// <summary>Painted seabed, shallowest rung.</summary>
        public const int PaintedSeabedMax = -18;

        /// <summary>A shore plant while SUBMERGED — deliberately below the sea so the water reads over it.
        /// (<c>ShorePlantDef.SubmergedSortingOrder</c>; the plant returns to the decor band when emergent.)</summary>
        public const int SubmergedShorePlant = -8;

        /// <summary>The sea plane itself.</summary>
        public const int Sea = -5;

        /// <summary>Wharf deck, north (far) row — a pier stands over water, so the whole deck is above
        /// <see cref="Sea"/> and below <see cref="DecorFloor"/>. Six orders for six rows, north to south.</summary>
        public const int WharfDeckMin = -4;
        /// <summary>Wharf deck, south (near) row — the last thing under the decor band.</summary>
        public const int WharfDeckMax = 1;

        /// <summary>An interior room sheet: one below the decor band, so no occupant can end up behind
        /// the floor they stand on.</summary>
        public const int InteriorFloor = 1;

        // --- THE DECOR BAND (YSortSprite's) -----------------------------------------------------------

        /// <summary>
        /// The lowest order Y-sorted decor may emit — one above <see cref="WharfDeckMax"/>. HELD AT 2
        /// FOREVER, or at least never moved casually: every constant above is positioned relative to it.
        /// </summary>
        public const int DecorFloor = 2;

        /// <summary>
        /// Sorting-order steps per world metre of Y — a depth step every 0.25 m. Unchanged by the re-base:
        /// it is the FEEL of the layering (how far you must walk before you pass behind something), and the
        /// defect was never resolution, only reach.
        /// </summary>
        public const float OrdersPerMetre = 4f;

        /// <summary>
        /// How far either side of world Y = 0 the decor band RESOLVES, in metres. This is the number the
        /// 2026-08-05 defect was really about: it used to be an implicit 4.75 m (a 2…40 clamp), while
        /// St Peters is 520 m tall — so 98% of the island's decor, and the player standing in it, saturated
        /// on one order and interleaved by draw order instead of by position.
        ///
        /// <para>300 m covers St Peters' 520 m scene (<c>StPetersBuilder.RegionWorldSize</c>, ±260 m) with
        /// room for a larger region, and costs nothing: the resulting ceiling is ~7% of the short that
        /// <c>sortingOrder</c> is stored in. <c>SortingBandsTests</c> asserts every built region fits, and
        /// fails with the number to raise this to.</para>
        /// </summary>
        public const int DecorHalfExtentMetres = 300;

        /// <summary>Order for decor at world Y = 0 — the middle of the band, derived so that Y =
        /// +<see cref="DecorHalfExtentMetres"/> lands exactly on <see cref="DecorFloor"/>. (1202.)</summary>
        public const int DecorBase = DecorFloor + (int)(DecorHalfExtentMetres * OrdersPerMetre);

        /// <summary>The highest order Y-sorted decor may emit, at Y = −<see cref="DecorHalfExtentMetres"/>.
        /// (2402.) Nothing that must draw over all decor may sit at or below this — see
        /// <see cref="AboveDecor"/>.</summary>
        public const int DecorCeiling = DecorBase + (int)(DecorHalfExtentMetres * OrdersPerMetre);

        // --- ABOVE THE DECOR BAND ---------------------------------------------------------------------

        /// <summary>
        /// For world sprites that must draw over ALL decor whatever their Y — the mooring rope, the
        /// trap-haul rope, rain. Each of these used to sit at 50, which cleared the old 40 ceiling; this
        /// constant is what 50 MEANT, so they keep their exact behaviour now the ceiling has moved.
        ///
        /// <para>⚠ Not a licence to park things here. A sprite above the band cannot be walked in front of,
        /// so anything a character can stand south of belongs IN the band with a <c>YSortSprite</c>.</para>
        /// </summary>
        public const int AboveDecor = DecorCeiling + AboveDecorGap;

        /// <summary>Clear air between the decor ceiling and the first tier above it — room to slot further
        /// above-decor tiers in later without touching the band. A round hundred, chosen for legibility in
        /// the inspector; nothing depends on its exact size.</summary>
        public const int AboveDecorGap = 100;

        /// <summary>The day/night tint overlay — over every world sprite, under the (Canvas) HUD. Near the
        /// top of the short <c>sortingOrder</c> is stored in; 32767 is the true maximum.</summary>
        public const int WorldTint = 32760;

        /// <summary>
        /// Whether the decor band RESOLVES this world Y, or saturates on it. False means two sprites at
        /// this height tie and interleave by draw order rather than by position — the 2026-08-05 defect.
        /// </summary>
        public static bool ResolvesWorldY(float worldY) =>
            worldY >= -DecorHalfExtentMetres && worldY <= DecorHalfExtentMetres;
    }
}
