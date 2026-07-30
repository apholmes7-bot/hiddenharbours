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
    /// <b>THE WORKING QUAY AT NINE MILE CREEK</b> — the region's one public wharf, rebuilt out of the
    /// baked wharf tile kit instead of the flat <c>WharfDeck.png</c> rectangle it has been since VS-22.
    ///
    /// <para><b>The footprint does not move, on purpose.</b> Three things already agree on this
    /// rectangle and one of them is a fixed owner-playtest bug: the shoreline fence dips around
    /// x ∈ [−4, 4] × y ∈ [−3, 3] to make the wharf a solid peninsula, the dock zone sits on its east
    /// tip, and the boat parks 3 m off that tip so <c>ControlSwitcher</c>'s pure distance test lets you
    /// disembark (gap #52 — <see cref="NineMileCreekBuilder.ArrivalPos"/>'s own note says keep it). So
    /// this file re-DRESSES the exact rectangle the region was authored around rather than re-siting it,
    /// and <see cref="DeckFootprint"/> is asserted against the fence and the dock geometry rather than
    /// restated beside them.</para>
    ///
    /// <para><b>Sprites, not a tilemap — and the kit's cell is why.</b> A wharf cell is <b>32 × 56 px</b>:
    /// the top 32 rows are the deck and the bottom 24 are the vertical face and its waterline, which
    /// <b>overhang downward over the cell below</b>. The kit only reads right drawn strictly BACK TO
    /// FRONT — north rows first — or a face overdraws the deck of its own southern neighbour. This deck
    /// is 48 cells; per-row sorting on plain sprites is both cheaper to reason about and exactly correct.
    /// (Same call, and the same reasoning, as the island's one dock.)</para>
    ///
    /// <para><b>Pivot is TOP-LEFT (0, 1).</b> Centre is right for every other tile sheet in this repo and
    /// would sink every wharf tile 12 px into the water. The kit's own importer sets it; this class
    /// places against it.</para>
    ///
    /// <para><b>Why <c>quay</c> and not <c>lowpier</c>.</b> <c>design/nine-mile-creek-wharf.md</c> §3's
    /// build table names the <c>quay</c> row — concrete, tide-stained face, algae, waterline foam — for
    /// this wharf's deck, and it is the read the place needs: Nine Mile Creek is where the sea is
    /// somebody's JOB (§2), the mainland port the island graduates to. The island's own dock is
    /// deliberately the rung below it in the same kit's age/means gradient (timber on piles), so the two
    /// wharves are told apart by their material before a single label is read.</para>
    ///
    /// <para><b>The deck is FLOOR, not dressing.</b> It registers a <see cref="StandablePlatform"/> — the
    /// Core standable-structure seam #350 added — so the on-foot sim reads the PLANKS as the standing
    /// surface. Nine Mile Creek is a DREDGED harbour whose floor is −6 m; without this the sim would see
    /// the whole quay as open water at every state of the tide.</para>
    /// </summary>
    public static class NineMileCreekWharf
    {
        /// <summary>The root every piece of the wharf hangs under.</summary>
        public const string RootName = "NineMileCreekWharf";

        // --- THE QUAY, sized off the rectangle the region is already authored around ------------------
        // NineMileCreekBuilder.NineMileCreekWharfCenter (0,0) / HalfSize (4,3) → x ∈ [−4,4], y ∈ [−3,3]:
        // the terrain plateau, the shoreline dip and the dock zone all read that rectangle. In CELLS
        // (one metre each, addressed by their south-west corner) that is x = −4..3 and y = −3..2.

        /// <summary>Westmost deck column — the quay root, where the planks meet the land at the
        /// waterline (x = −4, the fence line).</summary>
        public const int RootCellX = -4;

        /// <summary>Eastmost deck column. Its far edge is x = 4 — the seaward HEAD, and the dock
        /// zone.</summary>
        public const int HeadCellX = 3;

        /// <summary>Half-width in whole metres: cells −3..2, so 6 m of deck centred on the harbour
        /// centre-line. It is what the region already reserves, and it is wide enough to land a catch
        /// and stand a buyer's truck on without becoming the commercial quay this creek is not.</summary>
        public const int HalfWidthCells = 3;

        public static int MinCellY => -HalfWidthCells;
        public static int MaxCellY => HalfWidthCells - 1;
        public static int LengthCells => HeadCellX - RootCellX + 1;
        public static int WidthCells => MaxCellY - MinCellY + 1;

        /// <summary>The deck material — the kit's concrete row. See the class remarks.</summary>
        public const string DeckMaterial = "quay";

        // --- SORTING (the back-to-front rule, as numbers) ---------------------------------------------
        // The deck must sit ABOVE the Sea plane and BELOW the on-foot characters (YSortSprite's band
        // starts at 2). That leaves −4..1, which is exactly six orders for six rows — so the whole quay
        // fits the band with one order per row and the southern row draws last.
        //
        // ⚠ This is why NineMileCreekBuilder moves the Sea plane from −4 to −5 (the island's number).
        // The Sea used to sit at −4 because the WHARF was a floodable ground strip beneath it at −5;
        // now the deck is a structure standing OVER the water, so it has to be above it. The remaining
        // ground strips (Quay −7, QuayEdge −6) are still below the sea and still show through the
        // shader's wet-dry clip where they are dry.
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
        /// (<see cref="WharfKitCatalog.DeckVariants"/>), so this rectangle's four outside edges are open
        /// and its interior is centre. The WEST end counts as open too: it meets the beach rather than
        /// more deck, and a curb where concrete meets sand is right.
        ///
        /// <para>Only <c>ctr</c>, the four edges and the four outer corners are ever returned — a plain
        /// rectangle never needs an end CAP (three sides open at once) or a 45° DIAGONAL. The rule is the
        /// same one the island's dock uses because it is the rule for a RECTANGLE, not a rule about
        /// either wharf; when a third one lands it belongs in the kit's own catalog rather than a third
        /// copy here.</para>
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

        /// <summary>Every deck cell of the quay, in BACK-TO-FRONT order (north rows first). The order is
        /// the contract, not a convenience: a caller that iterates this and places as it goes gets the
        /// kit's draw rule for free.</summary>
        public static IEnumerable<Vector2Int> DeckCellsBackToFront()
        {
            for (int y = MaxCellY; y >= MinCellY; y--)
            for (int x = RootCellX; x <= HeadCellX; x++)
                yield return new Vector2Int(x, y);
        }

        // --- THE DECK AS FLOOR (the standable-structure registration) ----------------------------------

        /// <summary>Stable id of the structure this quay registers as a standable surface.</summary>
        public const string SurfaceId = "wharf.nine_mile_creek";

        /// <summary>
        /// The deck's world-space footprint — the rectangle of concrete the on-foot sim treats as floor.
        /// Derived from the same cell constants the sprites are placed from (never a second copy of the
        /// geometry): cells <c>[RootCellX, HeadCellX]</c> × <c>[MinCellY, MaxCellY]</c>, each one metre.
        /// </summary>
        public static Rect DeckFootprint() => new Rect(
            RootCellX, MinCellY,
            LengthCells, WidthCells);

        /// <summary>
        /// The deck's standing height (m above chart datum), <b>MEASURED off the terrain</b> at the quay
        /// root — never a literal. A wharf's deck is level with the ground it launches from, so it is the
        /// elevation at the root cell's centre-line, which is where the concrete meets walkable land. One
        /// number for the whole deck because a deck is LEVEL, and measured so that if a terrain edit ever
        /// lowered the root the deck follows it instead of silently lying.
        /// </summary>
        public static float DeckElevationFrom(ITidalTerrain terrain)
            => terrain.ElevationAt(new Vector2(RootCellX + 0.5f, 0f));

        // --- FITTINGS ---------------------------------------------------------------------------------
        // A working creek's gear, and each piece earns its place: you moor to the bollards, the tyres are
        // what stops a hull rubbing against concrete, you climb the ladder from a punt at low water, and
        // the pileheads are the quay's own structure showing at its exposed corners. No dolphin and no
        // gangway — those are a commercial berth's and a float's, and this creek has neither yet.
        //
        // ⚠ Fitting pivots mean DIFFERENT things and were measured, not assumed, when the kit landed:
        // standers (bollard/pilehead) sit 1 px above their base; hangers (ladder/tyre) pivot at the TOP
        // and fall away from what they attach to. So a stander goes at the deck point it stands on and a
        // hanger at the deck EDGE it hangs from.

        /// <summary>One placed piece of the wharf — a deck fitting or a block of breakwater armour:
        /// which kit slice, and where it goes. One shape for both because both are "a named slice at a
        /// world point"; what differs is only which sheet resolves the name.</summary>
        public struct Piece
        {
            public string Name;
            public Vector2 Position;
        }

        /// <summary>
        /// Spacing (m) between bollards down the mooring edge. Closer than the island's nine, because
        /// this is the wall a working fleet rafts against rather than one community pier — three bollards
        /// on eight metres is what "there is always one where you come alongside" looks like.
        /// </summary>
        public const int BollardSpacing = 2;

        /// <summary>
        /// The quay's fittings. All on the SOUTH edge, because that is the side the camera sees and the
        /// side whose tall face the kit actually draws — fittings on the north curb would be hidden
        /// behind the deck they stand on. It is also the face the design doc moors the fleet against
        /// (§3: the money shot is boats along the south face of a wall).
        /// </summary>
        public static List<Piece> Fittings()
        {
            var list = new List<Piece>();
            float southEdge = MinCellY;          // the deck's south lip, in world metres

            // Bollards down the mooring edge, from the head back toward the shore.
            for (int x = HeadCellX - 1; x >= RootCellX + 1; x -= BollardSpacing)
                list.Add(new Piece { Name = "bollard", Position = new Vector2(x + 0.5f, southEdge + 0.5f) });

            // Tyre fenders hung off the face, offset from the bollards so they read as a separate run of
            // gear rather than a matched pair at every post.
            for (int x = HeadCellX - 2; x >= RootCellX + 1; x -= BollardSpacing * 2)
                list.Add(new Piece { Name = "tyre", Position = new Vector2(x + 0.5f, southEdge) });

            // A ladder mid-wall — how you get down to a small boat at low water, and the one fitting a
            // creek that takes punts as well as lobster boats genuinely requires. Set between two
            // bollards and clear of both tyre runs so no two fittings share a metre of lip.
            list.Add(new Piece { Name = "ladder", Position = new Vector2(-0.5f, southEdge) });

            // The quay's own structure showing at the exposed seaward corners.
            list.Add(new Piece { Name = "pilehead", Position = new Vector2(HeadCellX + 0.5f, southEdge + 0.5f) });
            list.Add(new Piece { Name = "pilehead", Position = new Vector2(HeadCellX + 0.5f, MaxCellY + 0.5f) });

            // One cleat at the shore end of the mooring edge — a painter line for whatever is small
            // enough to lie in against the beach rather than take a bollard.
            list.Add(new Piece { Name = "cleat", Position = new Vector2(RootCellX + 0.5f, southEdge + 0.5f) });

            return list;
        }

        // --- THE BREAKWATER --------------------------------------------------------------------------
        // design/nine-mile-creek-wharf.md §3 and its open question 4: a SOUTH arm, and `crib` armour —
        // log boxes filled with stone, which is what a small community wharf actually builds and what the
        // owner's reference photograph unmistakably shows. `sheet` pile is saved for the commercial quay
        // money and machinery would build.
        //
        // It shelters the basin from the south while leaving the EAST approach — the way you come in —
        // wide open. Its crest carries an EdgeCollider2D for the same reason the shoreline does: a
        // breakwater a boat sails through is scenery, and this region has already been through that
        // playtest note once.

        /// <summary>Armour type. See the note above.</summary>
        public const string BreakwaterArmour = "crib";

        /// <summary>The arm's crest line (m), south of the basin. Its west end butts the beach; its east
        /// end stops well short of the harbour mouth so the approach and the dock zone stay clear.</summary>
        public const float BreakwaterY = -8f;
        public const float BreakwaterWestX = -4f;
        public const float BreakwaterEastX = 8f;

        /// <summary>Armour cell width in metres — 48 px at the locked 32 px = 1 m.</summary>
        public const float ArmourWidthMetres = 1.5f;

        /// <summary>How many armour blocks the arm takes — derived from its length, never typed in.</summary>
        public static int BreakwaterBlockCount =>
            Mathf.Max(1, Mathf.FloorToInt((BreakwaterEastX - BreakwaterWestX) / ArmourWidthMetres));

        /// <summary>
        /// Sorting order for the breakwater. The one thing it genuinely has to be is ABOVE the Sea plane,
        /// or the harbour is drawn over its own armour — so it takes the first order above the water,
        /// which is the deck band's own floor.
        ///
        /// <para><b>Sharing that order with the deck's north row is harmless and deliberate.</b> The arm
        /// lies 5 m south of the quay's face across open basin, so the two never overlap a pixel and
        /// their relative order is never asked. It is static rather than Y-sorted for the same reason:
        /// nothing stands on a breakwater. If a boat ever needs to pass BEHIND it, the arm should take a
        /// <c>YSortSprite</c> like any other world decor — with the pivot offset set, because the armour
        /// pivots TOP-centre and its ground contact is a block-height below the transform.</para>
        /// </summary>
        public const int BreakwaterSortingOrder = SortingOrderMin;

        /// <summary>
        /// The armour blocks along the arm, west to east, with the seaward tip capped. Pure, so the run
        /// can be asserted without a scene.
        ///
        /// <para><b>The positions are CENTRES.</b> The armour sheet pivots TOP-CENTRE, not top-left like
        /// the deck atlas — so a block placed at <c>x</c> spans <c>x ± 0.75 m</c>, and the run is laid out
        /// from the arm's west end plus half a block rather than from the end itself. Getting that wrong
        /// puts half a crib on the beach.</para>
        ///
        /// <para>⚠ <c>end</c>'s handedness is the KIT's claim and has never been checked against pixels
        /// (<see cref="WharfKitCatalog"/> says so of its own directional letters). If the cap turns out
        /// to face the wrong way it is a one-line swap here, never a re-slice.</para>
        /// </summary>
        public static List<Piece> BreakwaterBlocks()
        {
            var list = new List<Piece>();
            int count = BreakwaterBlockCount;
            for (int i = 0; i < count; i++)
            {
                float x = BreakwaterWestX + ArmourWidthMetres * (i + 0.5f);
                // The seaward tip is battered; everything behind it is the tileable run.
                string variant = i == count - 1 ? "end" : "straight";
                list.Add(new Piece { Name = variant, Position = new Vector2(x, BreakwaterY) });
            }
            return list;
        }

        /// <summary>The arm's collision line — the crest, as the boat meets it.</summary>
        public static readonly Vector2[] BreakwaterPoints =
        {
            new Vector2(BreakwaterWestX, BreakwaterY),
            new Vector2(BreakwaterEastX, BreakwaterY),
        };

        // =====================================================================================
        //  PLACEMENT
        // =====================================================================================

        /// <summary>
        /// Build the quay into the open scene. Returns the number of deck tiles placed (0 if the kit is
        /// not imported — it warns and places nothing rather than half a wharf).
        /// </summary>
        /// <param name="terrain">The region's authored terrain, used to MEASURE the deck height at the
        /// quay root (<see cref="DeckElevationFrom"/>). Required: a wharf whose deck height was guessed
        /// rather than measured is a wharf that floods without anything failing.</param>
        public static int Place(ITidalTerrain terrain)
        {
            var deck = LoadIndexedSheet(WharfKitCatalog.AtlasPath);
            if (deck.Count == 0)
            {
                Debug.LogWarning($"[NineMileCreekWharf] no slices at {WharfKitCatalog.AtlasPath} — the quay " +
                                 "was not built. Import/slice the wharf tile kit first.");
                return 0;
            }

            var root = new GameObject(RootName);
            var deckRoot = new GameObject("Deck");
            deckRoot.transform.SetParent(root.transform, worldPositionStays: false);

            // THE DECK IS FLOOR: register the concrete as a standable surface so the on-foot sim stands
            // on it rather than in the dredged −6 m harbour beneath (Core's StandableSurfaces seam). It
            // goes on the wharf's own root so it lives and dies with the wharf and needs no wiring.
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
                // TOP-LEFT pivot: put the sprite's origin at the cell's top-left corner, so the deck
                // fills the cell and the 24 px face hangs into the cell below (the 32×56 contract).
                go.transform.position = new Vector3(cell.x, cell.y + 1f, 0f);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = sprite;
                sr.sortingOrder = SortingOrderFor(cell.y);
                placed++;
            }

            int fittings = PlaceFittings(root);
            int armour = PlaceBreakwater(root);

            Debug.Log($"[NineMileCreekWharf] Built the working quay: {placed} '{DeckMaterial}' deck tiles " +
                      $"over {LengthCells} × {WidthCells} m, drawn back to front, plus {fittings} fitting(s) " +
                      $"and a {armour}-block '{BreakwaterArmour}' breakwater arm along y={BreakwaterY:0.#}. " +
                      $"The deck is FLOOR: registered as standable surface '{SurfaceId}' with its deck " +
                      $"measured at {deckElevation:0.00} m above datum, so the on-foot sim stands on the " +
                      "concrete and not in the dredged harbour beneath it.");
            return placed;
        }

        static int PlaceFittings(GameObject root)
        {
            var sprites = LoadNamedSheet(WharfKitCatalog.OverlaysPath);
            if (sprites.Count == 0)
            {
                Debug.LogWarning($"[NineMileCreekWharf] no fitting slices at {WharfKitCatalog.OverlaysPath} — " +
                                 "the quay is bare (no bollards, ladder or fenders).");
                return 0;
            }

            var fittingRoot = new GameObject("Fittings");
            fittingRoot.transform.SetParent(root.transform, worldPositionStays: false);

            int placed = 0;
            foreach (var f in Fittings())
            {
                string slice = WharfKitCatalog.FittingSlice(f.Name);
                if (!sprites.TryGetValue(slice, out Sprite sprite) || sprite == null) continue;

                var go = new GameObject($"Fitting_{f.Name}");
                go.transform.SetParent(fittingRoot.transform, worldPositionStays: false);
                go.transform.position = new Vector3(f.Position.x, f.Position.y, 0f);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = sprite;
                // One order above the southernmost deck row, so a bollard is never swallowed by the
                // concrete it stands on.
                sr.sortingOrder = SortingOrderMax + 1;
                placed++;
            }
            return placed;
        }

        static int PlaceBreakwater(GameObject root)
        {
            var sprites = LoadIndexedSheet(WharfKitCatalog.BreakwatersPath);
            if (sprites.Count == 0)
            {
                Debug.LogWarning($"[NineMileCreekWharf] no armour slices at {WharfKitCatalog.BreakwatersPath} — " +
                                 "the basin has no breakwater. The quay still builds; re-run after the sheet " +
                                 "imports.");
                return 0;
            }

            var armRoot = new GameObject("Breakwater");
            armRoot.transform.SetParent(root.transform, worldPositionStays: false);

            int placed = 0;
            foreach (var block in BreakwaterBlocks())
            {
                int index = WharfKitCatalog.BreakwaterIndex(BreakwaterArmour, block.Name);
                if (!sprites.TryGetValue(index, out Sprite sprite) || sprite == null) continue;

                var go = new GameObject($"Crib_{block.Position.x:0.#}");
                go.transform.SetParent(armRoot.transform, worldPositionStays: false);
                // TOP-CENTRE pivot (the kit's importer sets it): the block hangs BELOW its crest point,
                // the same downward overhang the deck's face has.
                go.transform.position = new Vector3(block.Position.x, block.Position.y, 0f);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = sprite;
                sr.sortingOrder = BreakwaterSortingOrder;
                placed++;
            }

            // Solid, for the same reason the shoreline is: a breakwater a boat sails through is scenery.
            var edge = armRoot.AddComponent<EdgeCollider2D>();
            edge.points = BreakwaterPoints;

            return placed;
        }

        /// <summary>A grid-sliced sheet's sub-sprites keyed by their trailing slice INDEX (the slicer
        /// names them <c>&lt;stem&gt;_&lt;index&gt;</c>, row-major from the top-left).</summary>
        static Dictionary<int, Sprite> LoadIndexedSheet(string path)
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
