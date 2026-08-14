#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using HiddenHarbours.Core;                // ITidalTerrain
using HiddenHarbours.Art.Editor;          // RoadKitV3 — layer names, sorting orders, the two-pass rule

namespace HiddenHarbours.App.Editor
{
    /// <summary>
    /// <b>Lays Nine Mile Creek's roads.</b> <see cref="NineMileCreekRoads"/> decides every cell and every
    /// surface; this class only does the Unity work — two tilemaps on adjacent sorting orders, one bulk
    /// write each — so the network itself stays testable without a scene, the same split
    /// <see cref="StPetersShoreMap"/> / <see cref="StPetersShorePainter"/> already uses for the coast.
    ///
    /// <para><b>Why a Tilemap.</b> Six thousand square metres of carriageway is six thousand sprites if
    /// each is a GameObject. A <see cref="Tilemap"/> chunks them into one mesh per chunk and draws only
    /// the chunks on screen (rule 7) — and it is the surface the OWNER paints on, so the same tiles land
    /// in his Tile Palette and anything the builder lays down he can go over by hand.</para>
    ///
    /// <para><b>⚠ TWO LAYERS, AND THE STEP BETWEEN THEM IS THE KIT'S COMPOSITE RULE.</b> Every road cell
    /// contributes a TOP (its headroom and its ground square) and a SKIRT (its kerb faces, its south drop
    /// faces and the baked alpha contact shadow that seats them). The skirt hangs 22 px below its own
    /// ground square and must draw in front of the ground square of the cell to its SOUTH — which no
    /// single tilemap can do at any sort order, because painter N→S draws that southern cell later and its
    /// flat top would slice every face off. So: all tops at
    /// <see cref="RoadKitV3.TopSortingOrder"/>, all skirts one step above at
    /// <see cref="RoadKitV3.SkirtSortingOrder"/>. Both numbers are the KIT's, not this file's.</para>
    ///
    /// <para><b>Where it sorts, and why the tide still works.</b> −17/−16 sits just above the shore
    /// painter's three layers and the splat ground (−21), and well below the Sea plane (−5). A road is
    /// therefore laid ON the ground and covered BY the water: the layered <c>WaterSurface</c> shader clips
    /// itself transparent wherever the authored ground is above the live water level (ADR 0010/0012), so
    /// a lane that floods is covered by depth-graded water rather than fighting it. Nothing here takes a
    /// <c>YSortSprite</c> — a road does not sort against the player, the player walks on it.</para>
    ///
    /// <para><b>⚠ WHAT IS DELIBERATELY NOT DRAWN — the boardwalk.</b> The kit bakes a boardwalk surface
    /// (a 0.35 m deck on piles) and it is not used anywhere in this region, on purpose. The only decking
    /// the wharf plan names is the FLOATING dock and its gangway
    /// (<see cref="NineMileCreekMainland.FloatRunY"/>), and a float rises and falls through 4.4 m of tide
    /// — a tilemap cell is nailed to one elevation, so a boardwalk laid there would be swallowed at high
    /// water or left standing over dry mud at low. The float wants the same wave-field treatment the
    /// moored hulls already get, which is a Boats seam and not a road one. The breakwater's plank catwalk
    /// is the plan's other decking and the ISO crib run already draws that structure
    /// (<see cref="NineMileCreekDressing.FacePieces"/>); a second surface under a drawn sprite is either
    /// invisible or fighting it. Both are reported rather than faked.</para>
    /// </summary>
    public static class NineMileCreekRoadPainter
    {
        /// <summary>The root the whole network hangs under — one object the owner can hide.</summary>
        public const string RootName = "NineMileCreekRoads";

        /// <summary>
        /// Paint the roads into the open scene and return what was laid. Safe to call before the kit is
        /// imported or sliced: it lays the same cells, reports that they have no pixels, and leaves the
        /// region's roads undrawn rather than half-drawn — the <see cref="NineMileCreekDressing"/>
        /// arrangement, for the same reason (the PLACEMENT is unaffected and still correct; re-run after
        /// the sheets import and the same road lands in the same place).
        /// </summary>
        public static Result Paint(ITidalTerrain terrain)
        {
            var result = new Result();

            if (terrain == null)
                Debug.LogWarning(
                    "[NineMileCreekRoadPainter] no terrain — the roads are laid at their full claimed " +
                    "width with no dry-ground rule applied, so a carriageway may run out over the flats. " +
                    "This should never happen from the builder, which configures the terrain first.");

            NineMileCreekRoads.Paving paving;
            try
            {
                paving = NineMileCreekRoads.Pave(terrain);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[NineMileCreekRoadPainter] the road network could not be paved: {e.Message}");
                return result;
            }

            result.Paving = paving;
            if (paving.Cells.Count == 0)
            {
                Debug.LogWarning("[NineMileCreekRoadPainter] the network paved no cells at all — is the " +
                                 "terrain configured? Roads not laid.");
                return result;
            }

            // The tiles are DERIVED (atlas + contract), generated on demand and never authored, so make
            // sure the ones this region needs exist before painting with them — every run.
            RoadTileLibrary.Build(SurfacesUsed(paving));

            // --- resolve every cell to its atlas index BEFORE touching Unity ---------------------------
            // The mask depends on the whole paved set, so this cannot be folded into the claim pass; and
            // doing it here keeps the expensive part pure arithmetic with exactly one bulk write per
            // layer afterwards.
            var tops = new Dictionary<Vector3Int, TileBase>(paving.Cells.Count);
            var skirts = new Dictionary<Vector3Int, TileBase>(paving.Cells.Count);
            var cache = new TileCache();

            foreach (var cell in paving.Cells)
            {
                int index;
                try
                {
                    index = NineMileCreekRoads.AtlasIndexAt(paving, cell.Cell.x, cell.Cell.y);
                }
                catch (System.Exception e)
                {
                    // A mask that canonicalises outside the 47 means the contract and the rig have drifted
                    // apart — a kit fault, not a content one. Say so once and stop, rather than laying
                    // 6,000 cells of the wrong tile.
                    Debug.LogError($"[NineMileCreekRoadPainter] {e.Message} Roads not laid.");
                    return result;
                }

                result.PerAtlasIndex.TryGetValue(index, out int seen);
                result.PerAtlasIndex[index] = seen + 1;

                var at = new Vector3Int(cell.Cell.x, cell.Cell.y, 0);
                Tile top = cache.Top(cell.Surface, index);
                Tile skirt = cache.Skirt(cell.Surface, index);
                if (top != null) { tops[at] = top; result.TopTiles++; }
                if (skirt != null) { skirts[at] = skirt; result.SkirtTiles++; }
            }

            // --- write -------------------------------------------------------------------------------
            var root = new GameObject(RootName);
            var grid = root.AddComponent<Grid>();
            grid.cellSize = new Vector3(1f, 1f, 0f);   // 1 m cells — one kit cell is one square metre

            Tilemap topMap = MakeLayer(root.transform, RoadKitV3.TopLayerName, RoadKitV3.TopSortingOrder);
            Tilemap skirtMap = MakeLayer(root.transform, RoadKitV3.SkirtLayerName,
                                         RoadKitV3.SkirtSortingOrder);

            Write(topMap, tops);
            Write(skirtMap, skirts);

            Report(result);
            return result;
        }

        /// <summary>Only the surfaces this region actually paves — see <see cref="RoadTileLibrary.Build"/>
        /// for why the whole eleven would be a thousand assets for nothing.</summary>
        static IEnumerable<string> SurfacesUsed(NineMileCreekRoads.Paving paving) => paving.PerSurface.Keys;

        static Tilemap MakeLayer(Transform parent, string name, int sortingOrder)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, worldPositionStays: false);
            var map = go.AddComponent<Tilemap>();
            var renderer = go.AddComponent<TilemapRenderer>();
            renderer.sortingOrder = sortingOrder;
            // Chunk mode is the whole reason to use a Tilemap at this scale — one mesh per chunk, and
            // only the chunks on screen are drawn.
            renderer.mode = TilemapRenderer.Mode.Chunk;
            // ⚠ PAINTER N→S, which is the kit's own composite order and NOT Unity's default. Sorting from
            // the top-left draws the northern cell first, so a southern cell's headroom lands over its
            // neighbour's ground square instead of under it. Left at the default BottomLeft, a raised
            // deck's piles would be clipped by the cell behind them.
            renderer.sortOrder = TilemapRenderer.SortOrder.TopLeft;
            return map;
        }

        static void Write(Tilemap map, Dictionary<Vector3Int, TileBase> tiles)
        {
            if (tiles.Count == 0) return;

            // One call, not one per cell: SetTiles takes the whole set and touches the tilemap's chunk
            // bookkeeping once. A per-cell SetTile over six thousand cells is the difference between a
            // build that blinks and one that hangs.
            var positions = new Vector3Int[tiles.Count];
            var values = new TileBase[tiles.Count];
            int i = 0;
            foreach (var kv in tiles) { positions[i] = kv.Key; values[i] = kv.Value; i++; }
            map.SetTiles(positions, values);
        }

        static void Report(Result result)
        {
            var paving = result.Paving;

            if (result.TopTiles == 0)
            {
                Debug.LogWarning(
                    $"[NineMileCreekRoadPainter] {paving.Cells.Count:N0} m² of road was laid out and none " +
                    $"of it had pixels — the road kit v3 has not been sliced under {RoadKitV3.RoadsDir}. " +
                    "The PLACEMENT is unaffected and still correct: re-run the builder after the sheets " +
                    "import and the same roads land in the same cells.");
                return;
            }

            Debug.Log(
                $"[NineMileCreekRoadPainter] Laid {paving.Cells.Count:N0} m² of road in " +
                $"{result.TopTiles:N0} top + {result.SkirtTiles:N0} skirt tiles across two layers " +
                $"({RoadKitV3.TopLayerName} at {RoadKitV3.TopSortingOrder}, {RoadKitV3.SkirtLayerName} at " +
                $"{RoadKitV3.SkirtSortingOrder} — the kit's two-pass rule, not a hand-picked order). " +
                $"Surfaces: {paving.SurfaceSummary()}. " +
                $"{paving.Trimmed:N0} claimed cell(s) were refused by the dry-ground rule — a road does " +
                "not ford, so a carriageway stops where the ground goes under rather than running out " +
                "over the flats. Every cell derives from the routes NineMileCreekMainland §9 publishes, " +
                "so re-siting a road there re-lays the tiles here.\n" +
                "REPORTED, not fixed — three art gaps this pass reads around:\n" +
                "  · the kit's `abut` edge (road meets a DIFFERENT paved surface: shared kerb, no verge) " +
                "is not baked, so junctions between two surfaces butt flush instead of sharing a kerb " +
                "face — the mask counts any paved neighbour to get as near as the committed sheets allow;\n" +
                "  · the `path` and `driveway` PIECES are not baked, so the gully path and the town walks " +
                "are drawn from the `lane`-profile road sheets at footpath width;\n" +
                "  · the boardwalk surface is deliberately unused — see NineMileCreekRoadPainter's class " +
                "note: a floating dock cannot be a tilemap.");
        }

        /// <summary>Loads each tile once. Six thousand cells resolve to a few hundred distinct tiles, and
        /// an <c>AssetDatabase</c> lookup per cell would dominate the whole build.</summary>
        sealed class TileCache
        {
            readonly Dictionary<string, Tile> _byName = new Dictionary<string, Tile>();

            public Tile Top(string surface, int index) =>
                Get(RoadTileLibrary.TopTileName(surface, index));

            public Tile Skirt(string surface, int index) =>
                Get(RoadTileLibrary.SkirtTileName(surface, index));

            Tile Get(string tileName)
            {
                if (_byName.TryGetValue(tileName, out Tile t)) return t;
                t = RoadTileLibrary.Load(tileName);
                _byName[tileName] = t;
                return t;
            }
        }

        /// <summary>What one paint pass laid down — logged for the owner and available to a test that
        /// wants to hold the footprint to a budget.</summary>
        public sealed class Result
        {
            public NineMileCreekRoads.Paving Paving = new NineMileCreekRoads.Paving();
            public int TopTiles;
            public int SkirtTiles;

            /// <summary>How many cells resolved to each atlas index — the number that says whether the
            /// network is drawing as a network (a spread of junction and corner cells) or as a row of
            /// isolated stubs (everything on one index).</summary>
            public readonly Dictionary<int, int> PerAtlasIndex = new Dictionary<int, int>();
        }
    }
}
#endif
