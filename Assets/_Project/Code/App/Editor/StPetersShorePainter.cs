#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;
using HiddenHarbours.Core;
using HiddenHarbours.Art;                 // YSortSprite — rocks interleave with the player by world Y
using HiddenHarbours.Art.Editor;          // ShorelineIso2Catalog, ShorelineIsoCatalog (the rock sheet)

namespace HiddenHarbours.App.Editor
{
    /// <summary>
    /// <b>Paints St Peters' coast</b> — the shoreline-ISO v8 ground under the whole island, the ragged fringe
    /// where one material laps onto another, the reef's rock tells, and the contact shade that seats them.
    /// Called from <see cref="StPetersBuilder"/>; <see cref="StPetersShoreMap"/> makes every decision and this
    /// class only does the Unity work, so the coast itself is testable without a scene.
    ///
    /// <para><b>Why a Tilemap and not sprites.</b> The island is ~450 × 260 m and the kit is a 1 m / 32 px
    /// grid, so the coast is six figures of cells. A <see cref="Tilemap"/> is the only vehicle that pays for
    /// that: it chunks the cells into one mesh per chunk and draws only the chunks on screen, where the same
    /// ground as individual <see cref="SpriteRenderer"/>s would be six figures of GameObjects (rule 7). It is
    /// also the surface the OWNER paints on — the same tiles land in his Tile Palette, so anything the builder
    /// lays down he can go over by hand.</para>
    ///
    /// <para><b>Where it sorts.</b> All three layers sit at large negative orders, BELOW the Sea plane
    /// (order −5). That is the whole trick of the tide reveal: the layered <c>WaterSurface</c> shader clips
    /// itself transparent wherever the authored ground is above the live water level, so the painting shows
    /// through on dry ground and depth-graded water covers it as the tide floods (ADR 0010/0012). The kit
    /// bakes ZERO water for exactly this reason — there is no wet edge to line up, because the shader owns
    /// the waterline.</para>
    ///
    /// <para><b>Cliffs are deliberately NOT painted.</b> See <see cref="StPetersShoreMap"/> and the PR that
    /// introduced this file: the kit draws a cliff as <c>cap + mid×N + toe</c> stacked in screen rows, ~1.3 m
    /// of vertical face per row, so a 6 m cliff occupies ~5 m of map. St Peters' coast is a ratified 30 m
    /// beach falloff (§5.1a) — a 1:5 ramp — so a drawn cliff would stand with its toe ~5 m from the plateau
    /// edge while the actual waterline sweeps between 12 m and 76 m out: a wall in a field. The weather coast
    /// is painted as the cobble-and-platform coast it is instead, and the cliff/cave stack waits on a ruling
    /// about a per-sector falloff (which moves ratified §5.1a numbers and is not a painting decision).</para>
    /// </summary>
    public static class StPetersShorePainter
    {
        /// <summary>The root the whole coast hangs under, so it is one object to find in the Hierarchy.</summary>
        public const string RootName = "Shoreline";

        // Layer names + sorting orders. −20 is the project's painted-ground order (PaintableTilemapMenu);
        // the two overlays sit just above it and everything stays well below the Sea plane at −5.
        public const string GroundLayerName  = "ShoreGround";
        public const string FringeLayerName  = "ShoreFringe";
        public const string ContactLayerName = "ShoreContact";
        public const int GroundSortingOrder  = -20;
        public const int FringeSortingOrder  = -19;
        public const int ContactSortingOrder = -18;

        /// <summary>Where the rock props hang.</summary>
        public const string RocksRootName = "ShoreRocks";

        /// <summary>The v7 kit's freestanding rock sheet — §5.1a's named source for the reef's visible tell
        /// ("keep a couple of the ShoreIsoSprites sea stacks ON the shelf"). Deliberately used INSTEAD of
        /// baking the Rock Iso kit: Rock Iso ships no pixels and the owner's stone×tide atlas-budget call is
        /// still open, so baking it now would spend an undecided budget to get rock we already have imported.</summary>
        public const string RockSheetPath =
            ShorelineIsoCatalog.ShorelineIsoDir + "/ShoreIsoSprites.png";

        /// <summary>
        /// Paint the coast into the open scene and return what it laid down (for the builder's log and for a
        /// test to hold the footprint to a budget). Safe to call before the kit is imported: it warns and
        /// paints nothing rather than half a coast.
        /// </summary>
        public static Result Paint(ITidalTerrain terrain, Vector2 regionCenter, Vector2 regionSize,
                                   string style = null, bool splatGround = false)
        {
            // ADR 0028: with the splat ground on, the TerrainSplat shader renders the ground/fringe
            // LOOK and this painter stamps neither layer — but it still classifies (the footprint
            // stats and the rock/contact placement read the same grid) and still lays the contact
            // shades and the reef rocks, which remain tile/sprite work on top of the field.
            var result = new Result();
            if (terrain == null)
            {
                Debug.LogWarning("[StPetersShorePainter] no terrain — coast not painted.");
                return result;
            }

            style = string.IsNullOrEmpty(style) ? ShoreIso2TileLibrary.DefaultStyle : style;

            // The tiles are DERIVED (sheet + catalog), generated on demand and never committed — so make
            // sure they exist before we paint with them, every run.
            if (ShoreIso2TileLibrary.Build(style) == 0)
            {
                Debug.LogWarning("[StPetersShorePainter] no shoreline-ISO tiles available — coast not " +
                                 "painted. The island will render as bare ground under the water shader.");
                return result;
            }

            // --- 1. CLASSIFY. One pass over the region rect, in metres, into a flat material grid. -----
            // Done before any Unity call so the expensive part is pure arithmetic on the authored terrain
            // and the tilemap sees exactly one bulk write per layer afterwards.
            int minCellX = Mathf.FloorToInt(regionCenter.x - regionSize.x * 0.5f);
            int minCellY = Mathf.FloorToInt(regionCenter.y - regionSize.y * 0.5f);
            int width  = Mathf.CeilToInt(regionSize.x);
            int height = Mathf.CeilToInt(regionSize.y);

            var materials = new ShoreMaterial[width * height];
            int paintedMinX = int.MaxValue, paintedMinY = int.MaxValue;
            int paintedMaxX = int.MinValue, paintedMaxY = int.MinValue;

            for (int iy = 0; iy < height; iy++)
            for (int ix = 0; ix < width; ix++)
            {
                // The cell CENTRE, which is where a 1 m tile with a centred pivot actually sits.
                var p = new Vector2(minCellX + ix + 0.5f, minCellY + iy + 0.5f);
                var m = StPetersShoreMap.MaterialAt(terrain, p);
                materials[iy * width + ix] = m;
                if (m == ShoreMaterial.None) continue;

                result.GroundTiles++;
                result.PerMaterial.TryGetValue(m, out int c);
                result.PerMaterial[m] = c + 1;
                if (ix < paintedMinX) paintedMinX = ix;
                if (iy < paintedMinY) paintedMinY = iy;
                if (ix > paintedMaxX) paintedMaxX = ix;
                if (iy > paintedMaxY) paintedMaxY = iy;
            }

            if (result.GroundTiles == 0)
            {
                Debug.LogWarning("[StPetersShorePainter] the classifier painted nothing — every cell came " +
                                 "back below the paint floor. Is the TidalTerrain configured?");
                return result;
            }

            // --- 2. BUILD THE LAYERS over the tight painted bounds (not the whole rect: two thirds of a
            //        760 × 520 m region is deep harbour that is never painted, and an empty chunk written
            //        with nulls is a chunk allocated for nothing). -----------------------------------------
            int bw = paintedMaxX - paintedMinX + 1;
            int bh = paintedMaxY - paintedMinY + 1;
            var ground  = new TileBase[bw * bh];
            var fringe  = new TileBase[bw * bh];

            var cache = new TileCache(style);

            if (!splatGround)
            for (int by = 0; by < bh; by++)
            for (int bx = 0; bx < bw; bx++)
            {
                int ix = paintedMinX + bx, iy = paintedMinY + by;
                var m = materials[iy * width + ix];
                if (m == ShoreMaterial.None) continue;

                int cellX = minCellX + ix;
                ground[by * bw + bx] = cache.Ground(StPetersShoreMap.MaterialName(m),
                                                    StPetersShoreMap.GroundVariant(cellX));

                var lapping = HighestLappingNeighbour(materials, width, height, ix, iy, m);
                if (lapping == ShoreMaterial.None) continue;

                string piece = FringePieceFor(materials, width, height, ix, iy, lapping);
                if (piece == null) continue;

                fringe[by * bw + bx] = cache.Fringe(StPetersShoreMap.MaterialName(lapping), piece);
                result.FringeTiles++;
            }

            // --- 3. WRITE. One bulk block per layer. ---------------------------------------------------
            var root = new GameObject(RootName);
            var grid = root.AddComponent<Grid>();
            grid.cellSize = new Vector3(1f, 1f, 0f);   // 1 m cells = a 32 px tile at the locked PPU 32

            var bounds = new BoundsInt(minCellX + paintedMinX, minCellY + paintedMinY, 0, bw, bh, 1);
            var groundMap  = MakeLayer(root.transform, GroundLayerName,  GroundSortingOrder);
            var fringeMap  = MakeLayer(root.transform, FringeLayerName,  FringeSortingOrder);
            var contactMap = MakeLayer(root.transform, ContactLayerName, ContactSortingOrder);
            if (!splatGround)
            {
                groundMap.SetTilesBlock(bounds, ground);
                fringeMap.SetTilesBlock(bounds, fringe);
            }

            // --- 4. THE REEF'S ROCK, and the contact shade that seats each one. ------------------------
            result.Rocks = PlaceRocks(root.transform, terrain, contactMap, cache);

            Debug.Log($"[StPetersShorePainter] Painted the coast in style '{style}': " +
                      (splatGround
                          ? $"{result.GroundTiles:N0} cells classified (ground/fringe rendered by the " +
                            "splat shader, ADR 0028) over "
                          : $"{result.GroundTiles:N0} ground tiles + {result.FringeTiles:N0} fringe overlays over ") +
                      $"{bw} x {bh} m, and {result.Rocks} rocks on the reef. " +
                      $"Materials: {result.MaterialSummary()}.");
            return result;
        }

        // =====================================================================================
        //  fringe
        // =====================================================================================

        /// <summary>
        /// The highest-ranked SOFT material among the eight neighbours that outranks this cell — the one
        /// whose tongue laps onto it. <see cref="ShoreMaterial.None"/> for "nothing laps here".
        ///
        /// <para><b>One overlay per cell, and the highest material wins.</b> A tilemap holds one tile per
        /// cell, so where three materials meet at a corner only the topmost tongue is drawn — grass laps
        /// over sand and the marram/sand seam beside it stays hard for that one metre. Taking the HIGHEST
        /// is the right tie-break because that is the tongue the eye goes to; a second fringe layer would
        /// buy the remaining case for another tilemap's worth of cells, which is not a trade worth making
        /// for a metre.</para>
        /// </summary>
        static ShoreMaterial HighestLappingNeighbour(ShoreMaterial[] materials, int width, int height,
                                                     int ix, int iy, ShoreMaterial mine)
        {
            var best = ShoreMaterial.None;
            int myRank = StPetersShoreMap.Rank(mine);

            for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
            {
                if (dx == 0 && dy == 0) continue;
                var n = At(materials, width, height, ix + dx, iy + dy);
                if (!StPetersShoreMap.HasFringe(n)) continue;
                if (StPetersShoreMap.Rank(n) <= myRank) continue;
                if (StPetersShoreMap.Rank(n) > StPetersShoreMap.Rank(best)) best = n;
            }
            return best;
        }

        /// <summary>Which fringe piece the lapping material's neighbourhood asks for. A neighbour counts as
        /// "lapping" if it ranks at or above the chosen material, so a grass tongue still reads correctly
        /// where marram sits between the grass and the sand.</summary>
        static string FringePieceFor(ShoreMaterial[] materials, int width, int height,
                                     int ix, int iy, ShoreMaterial lapping)
        {
            int threshold = StPetersShoreMap.Rank(lapping);
            bool Laps(int dx, int dy) =>
                StPetersShoreMap.Rank(At(materials, width, height, ix + dx, iy + dy)) >= threshold;

            return StPetersShoreMap.FringePiece(
                n: Laps(0, 1), e: Laps(1, 0), s: Laps(0, -1), w: Laps(-1, 0),
                ne: Laps(1, 1), se: Laps(1, -1), sw: Laps(-1, -1), nw: Laps(-1, 1));
        }

        static ShoreMaterial At(ShoreMaterial[] materials, int width, int height, int ix, int iy)
        {
            if (ix < 0 || iy < 0 || ix >= width || iy >= height) return ShoreMaterial.None;
            return materials[iy * width + ix];
        }

        // =====================================================================================
        //  rocks
        // =====================================================================================

        /// <summary>
        /// Place the scattered rock as plain sprites, each carrying a <see cref="YSortSprite"/> so it
        /// interleaves with the player and the boat by world Y (a stack up-screen is behind you, one
        /// down-screen is in front) and can never sink behind the water plane — the component's own order
        /// floor keeps it in the world-decor band. Static, so each computes its order once (rule 7).
        /// A contact-shade tile is stamped on the ground cell to the NORTH of each rock, which is what
        /// <c>Contact.png</c> is for: it seats the rock into the ground instead of letting it float.
        /// </summary>
        static int PlaceRocks(Transform parent, ITidalTerrain terrain, Tilemap contactMap, TileCache cache)
        {
            var rockSprites = LoadRockSprites();
            if (rockSprites.Count == 0)
            {
                Debug.LogWarning($"[StPetersShorePainter] no rock sprites at {RockSheetPath} — the reef has " +
                                 "no visible tell. Import/slice the shoreline-ISO v7 sprite sheet.");
                return 0;
            }

            var rocksRoot = new GameObject(RocksRootName);
            rocksRoot.transform.SetParent(parent, worldPositionStays: false);

            // The contact overlays are so few (one per rock) that a per-cell SetTile is cheaper than
            // another full block, and it cannot disturb cells the ground pass wrote.
            var contactTile = cache.Contact("n");

            int placed = 0;
            foreach (var site in StPetersShoreMap.ScatterRocks(terrain))
                placed += PlaceRock(rocksRoot.transform, contactMap, contactTile, rockSprites,
                                    site, "Rock");

            // The interior erratics (owner's 2026-08-01 "add rocks"): same sheet, same seating, their
            // own name prefix so a scene inspector can tell field from shore at a glance.
            foreach (var site in StPetersShoreMap.ScatterFieldRocks(terrain))
                placed += PlaceRock(rocksRoot.transform, contactMap, contactTile, rockSprites,
                                    site, "FieldRock");

            return placed;
        }

        /// <summary>One rock, seated: base-pivoted sprite + Y-sort + the contact-shade tile on the ground
        /// cell to its north — shared by the shore rings and the interior erratics so a rock is a rock.</summary>
        static int PlaceRock(Transform rocksRoot, Tilemap contactMap, TileBase contactTile,
                             Dictionary<string, Sprite> rockSprites,
                             StPetersShoreMap.RockSite site, string namePrefix)
        {
            if (!rockSprites.TryGetValue(site.Sprite, out Sprite sprite) || sprite == null) return 0;

            var go = new GameObject($"{namePrefix}_{site.Sprite}");
            go.transform.SetParent(rocksRoot, worldPositionStays: false);
            go.transform.position = new Vector3(site.Position.x, site.Position.y, 0f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            go.AddComponent<YSortSprite>();

            if (contactTile != null)
                contactMap.SetTile(
                    new Vector3Int(Mathf.FloorToInt(site.Position.x),
                                   Mathf.FloorToInt(site.Position.y) + 1, 0),
                    contactTile);

            return 1;
        }

        /// <summary>The rock sheet's named sub-sprites, keyed by the kit's own item name (<c>reef</c>,
        /// <c>s</c>, <c>bl</c>, …). They are pivoted at their base on import, so a rock placed at a world
        /// position stands ON it rather than through it.</summary>
        static Dictionary<string, Sprite> LoadRockSprites()
        {
            var byName = new Dictionary<string, Sprite>();
            foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(RockSheetPath))
            {
                if (!(obj is Sprite s)) continue;
                foreach (string item in ShorelineIsoCatalog.RockSprites)
                    if (s.name.EndsWith("_" + item, System.StringComparison.Ordinal)) byName[item] = s;
            }
            return byName;
        }

        // =====================================================================================
        //  plumbing
        // =====================================================================================

        static Tilemap MakeLayer(Transform parent, string name, int sortingOrder)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, worldPositionStays: false);
            var map = go.AddComponent<Tilemap>();
            var renderer = go.AddComponent<TilemapRenderer>();
            renderer.sortingOrder = sortingOrder;
            // Chunk mode is the point of using a Tilemap at this scale — one mesh per chunk, and only the
            // chunks on screen are drawn.
            renderer.mode = TilemapRenderer.Mode.Chunk;
            return map;
        }

        /// <summary>Loads each tile asset once. Painting six figures of cells re-resolves a handful of
        /// distinct tiles, and an <c>AssetDatabase</c> lookup per cell would dominate the whole build.</summary>
        sealed class TileCache
        {
            readonly string _style;
            readonly Dictionary<string, Tile> _byName = new Dictionary<string, Tile>();

            public TileCache(string style) { _style = style; }

            public Tile Ground(string material, int variant) =>
                Get(ShoreIso2TileLibrary.GroundName(material, variant));

            public Tile Fringe(string material, string piece) =>
                Get(ShoreIso2TileLibrary.FringeName(material, piece));

            public Tile Contact(string piece) => Get(ShoreIso2TileLibrary.ContactName(piece));

            Tile Get(string tileName)
            {
                if (_byName.TryGetValue(tileName, out Tile t)) return t;
                t = ShoreIso2TileLibrary.Load(_style, tileName);
                if (t == null)
                    Debug.LogWarning($"[StPetersShorePainter] tile '{tileName}' missing in style " +
                                     $"'{_style}' — those cells stay empty.");
                _byName[tileName] = t;
                return t;
            }
        }

        /// <summary>What one paint pass laid down — logged by the builder and asserted by the layout test
        /// so the footprint cannot quietly grow into a scene nobody can open.</summary>
        public sealed class Result
        {
            public int GroundTiles;
            public int FringeTiles;
            public int Rocks;
            public readonly Dictionary<ShoreMaterial, int> PerMaterial = new Dictionary<ShoreMaterial, int>();

            public string MaterialSummary()
            {
                var parts = new List<string>();
                foreach (var kv in PerMaterial)
                    parts.Add($"{StPetersShoreMap.MaterialName(kv.Key)} {kv.Value:N0}");
                parts.Sort();
                return string.Join(", ", parts);
            }
        }
    }
}
#endif
