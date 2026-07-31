#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Core;                // ITidalTerrain — the deck height is MEASURED off the terrain
using HiddenHarbours.Art.Editor;          // WharfKitCatalog — the kit's published semantic map

namespace HiddenHarbours.App.Editor
{
    /// <summary>
    /// <b>St Peters' one dock</b> — the modest finger pier that dresses the ratified berth on the island's
    /// east end (§5.1a: <i>"There is one dock on the far end of the island opposite the sandbar. It's
    /// modest, but can take powerboats there."</i>). Geometry and the auto-tile decisions are pure and
    /// public so a test can assert the pier without a scene; <see cref="Place"/> does the Unity work.
    ///
    /// <para><b>Sprites, not a tilemap — and the kit's cell is why.</b> A wharf cell is <b>32 × 56 px</b>,
    /// not 32 × 32: the top 32 rows are the deck (the tile) and the bottom 24 are the vertical face and its
    /// waterline, which <b>overhang downward over the cell below</b>. So the kit only reads right if it is
    /// drawn strictly BACK TO FRONT — north rows first — or a face overdraws the deck of its own southern
    /// neighbour. A single chunked <see cref="UnityEngine.Tilemaps.Tilemap"/> does not give per-row draw
    /// order for free, and this deck is ~230 cells rather than the coast's six figures, so plain sprites
    /// with an explicit per-row sorting order are both cheaper to reason about and exactly correct. (The
    /// coast uses a tilemap for the opposite reason — see <see cref="StPetersShorePainter"/>.)</para>
    ///
    /// <para><b>Pivot is TOP-LEFT (0, 1).</b> Centre is right for every other tile sheet in this repo and
    /// would sink every wharf tile 12 px into the water — consistently enough to read as an art bug rather
    /// than a placement one. The kit's own importer sets it; this class places against it.</para>
    ///
    /// <para><b>The deck is FLOOR, not dressing.</b> It used to be dressing only: <c>TidalWalkability</c>
    /// answered purely from (terrain elevation vs water level), so the simulation saw 4.5 m of open water
    /// over the dredged −1.0 m slip at the ratified disembark point for most of every tide, and sailing home
    /// meant swimming across your own pier. The deck now registers a <see cref="StandablePlatform"/> — the
    /// Core standable-structure seam — so the on-foot sim reads the PLANKS as the standing surface
    /// (<see cref="DeckFootprint"/> + <see cref="DeckElevationFrom"/>). The seabed under the pier is
    /// untouched: the slip is dredged BY DESIGN and a boat still floats in exactly the water the terrain
    /// authored.</para>
    /// </summary>
    public static class StPetersWharf
    {
        /// <summary>The root every piece of the dock hangs under.</summary>
        public const string RootName = "StPetersWharf";

        // --- THE PIER, sized off the ratified berth (do not re-derive these from feel) ----------------
        // The berth is a dredged slip along (240, 0) → (190, 0) at half-width 8 and a −1.0 m bed
        // (StPetersBuilder / §5.1a — re-sited by the 2026-07-30 island shrink; the depths and the whole
        // arrangement are unchanged). Its head stops one metre short of the mooring at (215, 0) so the
        // boat lies just off the planks, and the ratified disembark at (213, 0) lands ON the deck rather
        // than beside it.
        //
        // ⚠ THE ROOT IS MEASURED, NOT ESTIMATED. The carve reaches PAST the slip's shoreward end — it
        // falls off smoothly over the half-width, so it is still pulling the ground down several metres
        // inland of x = 190. Rooting the pier just two cells further east, at 185, puts the bed at
        // **+3.15 m** — flooded at every spring high water, the exact failure the pre-shrink pier's first
        // attempt shipped at its own scale. 183 measures +5.35 m, comfortably dry at every tide (the
        // carve profile is the same shape as before the shrink, so the measured numbers carry over).
        // The margin is thin enough that this is asserted against the terrain (StPetersVillageTests)
        // rather than trusted to a comment — which is exactly how the original error was caught.

        /// <summary>Westmost deck column — the pier root, on the last ground that is dry at EVERY tide.</summary>
        public const int RootCellX = 183;

        /// <summary>Eastmost deck column — the pier head, just short of the mooring at x = 215.</summary>
        public const int HeadCellX = 213;

        /// <summary>Half-width in whole metres: cells −3..2, so 6 m of deck centred on the slip's
        /// centre-line. Wide enough to walk two abreast and land a catch, narrow enough to read as the
        /// modest community wharf §5.1a asks for rather than a commercial quay.</summary>
        public const int HalfWidthCells = 3;

        public static int MinCellY => -HalfWidthCells;
        public static int MaxCellY => HalfWidthCells - 1;
        public static int LengthCells => HeadCellX - RootCellX + 1;
        public static int WidthCells => MaxCellY - MinCellY + 1;

        /// <summary>
        /// The deck material. <c>lowpier</c> — timber on piles, the third rung of the kit's age/means
        /// gradient below <c>quay</c> (concrete) and above nothing. A small island community builds crib
        /// and timber; sheet pile and concrete are "what money and machinery look like"
        /// (<see cref="WharfKitCatalog.ArmourTypes"/>'s own note), and St Peters has neither.
        /// Deliberately NOT <c>float</c>: a floating dock animates (four bob frames) and would need a
        /// driver, and the slip DRIES near spring low — a float would sit on the mud.
        /// </summary>
        public const string DeckMaterial = "lowpier";

        // --- SORTING (the back-to-front rule, as numbers) ---------------------------------------------
        // The deck must sit ABOVE the Sea plane (−5) because a pier stands over water, and BELOW the
        // on-foot characters (YSortSprite's band starts at 2). That leaves −4..1, which is exactly six
        // orders for six rows — so the whole pier fits the band with one order per row and the southern
        // row draws last.
        public const int SortingOrderMin = -4;
        public const int SortingOrderMax = 1;

        /// <summary>
        /// Sorting order for a deck row. North (high Y) draws FIRST/behind, south draws LAST/in front —
        /// the kit's back-to-front rule expressed as the only thing a sprite renderer understands.
        /// </summary>
        public static int SortingOrderFor(int cellY) =>
            Mathf.Clamp(-cellY - 2, SortingOrderMin, SortingOrderMax);

        /// <summary>
        /// Which auto-tile variant a deck cell takes. An "open" side is one that drops to water
        /// (<see cref="WharfKitCatalog.DeckVariants"/>) — so the pier's four outside edges are open and its
        /// interior is centre. The WEST end counts as open too: it meets sand rather than more deck, and a
        /// curb where planks meet a beach is right.
        ///
        /// <para>Only <c>ctr</c>, the four edges and the four outer corners are ever returned. A plain
        /// rectangle six cells wide never needs an end CAP (three sides open at once) or a 45° DIAGONAL,
        /// and reaching for one would mean the pier had stopped being a rectangle.</para>
        /// </summary>
        public static string DeckVariantAt(int cellX, int cellY)
        {
            bool w = cellX == RootCellX;
            bool e = cellX == HeadCellX;
            bool s = cellY == MinCellY;
            bool n = cellY == MaxCellY;

            if (n && w) return "coNW";
            if (n && e) return "coNE";
            if (s && w) return "coSW";
            if (s && e) return "coSE";
            if (n) return "edN";
            if (s) return "edS";
            if (e) return "edE";
            if (w) return "edW";
            return "ctr";
        }

        /// <summary>Every deck cell of the pier, in BACK-TO-FRONT order (north rows first). The order is
        /// the contract, not a convenience: a caller that iterates this and places as it goes gets the
        /// kit's draw rule for free.</summary>
        public static IEnumerable<Vector2Int> DeckCellsBackToFront()
        {
            for (int y = MaxCellY; y >= MinCellY; y--)
            for (int x = RootCellX; x <= HeadCellX; x++)
                yield return new Vector2Int(x, y);
        }

        // --- THE DECK AS FLOOR (the standable-structure registration) ----------------------------------

        /// <summary>Stable id of the structure this pier registers as a standable surface.</summary>
        public const string SurfaceId = "wharf.st_peters";

        /// <summary>
        /// The deck's world-space footprint — the rectangle of planks the on-foot sim treats as floor.
        /// Derived from the same cell constants the sprites are placed from (never a second copy of the
        /// geometry): cells <c>[RootCellX, HeadCellX]</c> × <c>[MinCellY, MaxCellY]</c>, each one metre, so
        /// the rect spans <c>HeadCellX + 1</c> and <c>MaxCellY + 1</c> at its far edges.
        /// </summary>
        public static Rect DeckFootprint() => new Rect(
            RootCellX, MinCellY,
            LengthCells, WidthCells);

        /// <summary>
        /// The deck's standing height (m above chart datum), <b>MEASURED off the terrain</b> at the pier
        /// root — never a literal. A wharf's planks are level with the ground they launch from, so the deck
        /// is exactly the elevation at the root cell (+5.35 m: see the root's own note above, and the
        /// berth carve is still pulling the ground down there, which is why this is measured). That puts it
        /// ~1.85 m clear of spring high water (+3.5 m), so the pier is dry at every tide — and if a terrain
        /// edit ever lowered the root, the deck follows it instead of silently lying.
        ///
        /// <para>One number for the whole deck because a deck is LEVEL. The root centre-line is the sample
        /// point because it is the one place where the planks meet walkable ground.</para>
        /// </summary>
        public static float DeckElevationFrom(ITidalTerrain terrain)
            => terrain.ElevationAt(new Vector2(RootCellX + 0.5f, 0f));

        // --- FITTINGS ---------------------------------------------------------------------------------
        // Modest, and each one earns its place: you moor to the bollards, you climb the ladder from a
        // dinghy at low water, the tyres are what stops a hull rubbing, the pileheads are the pier's own
        // structure showing at its head. No gangway (that is a float's fitting) and no dolphin (that is a
        // commercial berth's).
        //
        // ⚠ Fitting pivots mean DIFFERENT things and were measured, not assumed, when the kit landed:
        // standers (bollard/pilehead) sit 1 px above their base; hangers (ladder/tyre) pivot at the TOP and
        // fall away from what they attach to. So a stander is placed at the deck point it stands on, and a
        // hanger at the deck EDGE it hangs from — which is why the table below carries both kinds and the
        // placer does not try to be clever about either.

        /// <summary>One placed fitting: which kit slice, and where on the deck it goes.</summary>
        public struct Fitting
        {
            public string Name;
            public Vector2 Position;
        }

        /// <summary>Spacing (m) between bollards down the mooring edge — far enough apart to read as a
        /// working wharf, close enough that a 5 m dory and a 13 m Cape Islander both find one.</summary>
        public const int BollardSpacing = 9;

        /// <summary>
        /// The pier's fittings. All on the SOUTH edge, because that is the side the camera sees and the
        /// side whose tall face the kit actually draws — fittings on the north curb would be hidden behind
        /// the deck they stand on.
        /// </summary>
        public static List<Fitting> Fittings()
        {
            var list = new List<Fitting>();
            float southEdge = MinCellY;          // the deck's south lip, in world metres

            // Bollards down the mooring edge, from the head back toward the shore.
            for (int x = HeadCellX - 1; x > RootCellX + 1; x -= BollardSpacing)
                list.Add(new Fitting { Name = "bollard", Position = new Vector2(x + 0.5f, southEdge + 0.5f) });

            // Tyre fenders, offset from the bollards so they read as a separate run of gear.
            for (int x = HeadCellX - 5; x > RootCellX + 2; x -= BollardSpacing * 2)
                list.Add(new Fitting { Name = "tyre", Position = new Vector2(x + 0.5f, southEdge) });

            // A ladder at the head — how you get aboard a shallow boat at low water, and the one fitting
            // the "modest but takes powerboats" brief genuinely requires.
            list.Add(new Fitting { Name = "ladder", Position = new Vector2(HeadCellX - 1.5f, southEdge) });

            // The pier's own structure showing at the exposed corners.
            list.Add(new Fitting { Name = "pilehead", Position = new Vector2(HeadCellX + 0.5f, southEdge + 0.5f) });
            list.Add(new Fitting { Name = "pilehead", Position = new Vector2(HeadCellX + 0.5f, MaxCellY + 0.5f) });

            // One cleat by the ladder, for a painter line.
            list.Add(new Fitting { Name = "cleat", Position = new Vector2(HeadCellX - 3.5f, southEdge + 0.5f) });

            return list;
        }

        // =====================================================================================
        //  PLACEMENT
        // =====================================================================================

        /// <summary>
        /// Build the pier into the open scene. Returns the number of deck tiles placed (0 if the kit is not
        /// imported — it warns and places nothing rather than half a wharf).
        /// </summary>
        /// <param name="terrain">The region's authored terrain, used to MEASURE the deck height at the pier
        /// root (<see cref="DeckElevationFrom"/>). Required: a wharf whose deck height was guessed rather
        /// than measured is a wharf that floods without anything failing.</param>
        public static int Place(ITidalTerrain terrain)
        {
            var deck = LoadSheet(WharfKitCatalog.AtlasPath, "WharfAtlas");
            if (deck.Count == 0)
            {
                Debug.LogWarning($"[StPetersWharf] no slices at {WharfKitCatalog.AtlasPath} — the dock was " +
                                 "not built. Import/slice the wharf tile kit first.");
                return 0;
            }

            var root = new GameObject(RootName);
            var deckRoot = new GameObject("Deck");
            deckRoot.transform.SetParent(root.transform, worldPositionStays: false);

            // THE DECK IS FLOOR: register the planks as a standable surface so the on-foot sim stands on
            // them rather than in the dredged slip beneath (Core's StandableSurfaces seam). This goes on the
            // pier's own root, so it lives and dies with the pier and needs no builder wiring elsewhere.
            var floor = root.AddComponent<HiddenHarbours.World.StandablePlatform>();
            float deckElevation = DeckElevationFrom(terrain);
            floor.Configure(SurfaceId, DeckFootprint(), deckElevation);

            int placed = 0;
            foreach (var cell in DeckCellsBackToFront())
            {
                int index = WharfKitCatalog.AtlasIndex(DeckMaterial, DeckVariantAt(cell.x, cell.y));
                if (!deck.TryGetValue(index, out Sprite sprite) || sprite == null) continue;

                var go = new GameObject($"Deck_{cell.x}_{cell.y}");
                go.transform.SetParent(deckRoot.transform, worldPositionStays: false);
                // TOP-LEFT pivot: put the sprite's origin at the cell's top-left corner, so the deck fills
                // the cell and the 24 px face hangs into the cell below (the kit's whole 32x56 contract).
                go.transform.position = new Vector3(cell.x, cell.y + 1f, 0f);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = sprite;
                sr.sortingOrder = SortingOrderFor(cell.y);
                placed++;
            }

            // Fittings, on their own root and one order above the southernmost deck row so a bollard is
            // never swallowed by the planks it stands on.
            var fittingSprites = LoadNamedSheet(WharfKitCatalog.OverlaysPath);
            if (fittingSprites.Count > 0)
            {
                var fittingRoot = new GameObject("Fittings");
                fittingRoot.transform.SetParent(root.transform, worldPositionStays: false);
                foreach (var f in Fittings())
                {
                    string slice = WharfKitCatalog.FittingSlice(f.Name);
                    if (!fittingSprites.TryGetValue(slice, out Sprite sprite) || sprite == null) continue;

                    var go = new GameObject($"Fitting_{f.Name}");
                    go.transform.SetParent(fittingRoot.transform, worldPositionStays: false);
                    go.transform.position = new Vector3(f.Position.x, f.Position.y, 0f);
                    var sr = go.AddComponent<SpriteRenderer>();
                    sr.sprite = sprite;
                    sr.sortingOrder = SortingOrderMax + 1;
                }
            }
            else
            {
                Debug.LogWarning($"[StPetersWharf] no fitting slices at {WharfKitCatalog.OverlaysPath} — " +
                                 "the deck is bare (no bollards, ladder or fenders).");
            }

            Debug.Log($"[StPetersWharf] Built the one dock: {placed} '{DeckMaterial}' deck tiles over " +
                      $"{LengthCells} x {WidthCells} m at the ratified berth, drawn back to front, plus " +
                      $"{Fittings().Count} fittings. The deck is FLOOR: registered as standable surface " +
                      $"'{SurfaceId}' with its deck measured at {deckElevation:0.00} m above datum, so the " +
                      "on-foot sim stands on the planks and not in the -1.0 m slip beneath them.");
            return placed;
        }

        /// <summary>A sliced sheet's sub-sprites keyed by their trailing slice INDEX (the slicer names them
        /// <c>&lt;stem&gt;_&lt;index&gt;</c>, row-major from the top-left).</summary>
        static Dictionary<int, Sprite> LoadSheet(string path, string stem)
        {
            var byIndex = new Dictionary<int, Sprite>();
            foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (!(obj is Sprite s)) continue;
                int u = s.name.LastIndexOf('_');
                if (u >= 0 && int.TryParse(s.name.Substring(u + 1), out int n)) byIndex[n] = s;
            }
            return byIndex;
        }

        /// <summary>A sidecar-sliced sheet's sub-sprites keyed by their full slice NAME (the overlays are
        /// named, not indexed, because each fitting has its own rect and its own kind of pivot).</summary>
        static Dictionary<string, Sprite> LoadNamedSheet(string path) =>
            AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>()
                         .GroupBy(s => s.name).ToDictionary(g => g.Key, g => g.First());
    }
}
#endif
