#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;               // Tile (engine TilemapModule)
using HiddenHarbours.Art.Editor;          // ShorelineIso2Catalog — the kit's published semantic map

namespace HiddenHarbours.App.Editor
{
    /// <summary>
    /// Turns the imported <b>shoreline-ISO v8</b> sheets into paintable <see cref="Tile"/> assets, one per
    /// meaningful slice, named for what it MEANS rather than where it sits — <c>Ground_grass_0</c>,
    /// <c>Fringe_sand_edN</c>, <c>Contact_n</c>. <see cref="StPetersShorePainter"/> paints with them; the
    /// owner can also pick them up in the Tile Palette and paint by hand.
    ///
    /// <para><b>Why the names are semantic here and geometric on the sheet.</b> Slices are named by GRID
    /// POSITION on purpose (<c>Ground_7</c> = the 8th cell, row-major) because compass-labelled art has been
    /// mislabelled in five kits in this repo, so a slice name must never claim a meaning. The meaning is
    /// resolved in exactly one place — <see cref="ShorelineIso2Catalog"/>, against the kit's own
    /// <c>ShorelineIso.json</c> — and this class is the one consumer that turns that resolution into an asset
    /// a human can pick. Nothing here computes an index itself; every lookup goes through the catalog, so a
    /// re-bake that moves a row moves these tiles with it.</para>
    ///
    /// <para><b>Generated, not committed</b> — the same standing arrangement as <c>TileAssetBuilder</c>'s
    /// Sand/Rock/Grass tiles, which live untracked on the owner's disk. A Tile asset is a pure function of
    /// (sheet + catalog), so committing 130 of them would put derived bytes under review and churn the whole
    /// set on every re-slice. The builder regenerates them before it paints, so <c>git pull</c> +
    /// <i>Build St Peters Scene</i> is all the owner ever does.</para>
    ///
    /// <para><b>Idempotent and GUID-stable.</b> A re-run REPOINTS an existing tile's sprite rather than
    /// deleting and recreating the asset, so a tilemap already painted with these tiles keeps its references
    /// (a fresh asset would get a fresh GUID and every painted cell would go blank).</para>
    ///
    /// <para>Menu: <c>Hidden Harbours ▸ Art ▸ Build Shoreline ISO Tiles</c>.</para>
    /// </summary>
    public static class ShoreIso2TileLibrary
    {
        /// <summary>Output root. Sits beside <c>TileAssetBuilder</c>'s output under
        /// <c>Tilesets/Tiles/</c>, one folder per shading style so the two never mix.</summary>
        public const string TilesRoot = "Assets/_Project/Art/Tilesets/Tiles/ShoreIso2";

        /// <summary>
        /// The style St Peters is painted in. <c>nat</c> (naturalist — 8/7/8-step ramps, dithered band
        /// transitions) is the default; <c>gfx</c> (graphic — hard band edges, unified keyline) is the kit's
        /// A/B. The kit's promise is that geometry, grid, piece names and pivots are IDENTICAL across the
        /// two, so flipping this constant and re-running the builder re-skins the whole coast without moving
        /// a single tile (<see cref="ShorelineIso2Catalog.ContractsAgreeOnGeometry(out string)"/> is what
        /// keeps that promise honest).
        /// </summary>
        public const string DefaultStyle = "nat";

        public static string StyleDir(string style) => $"{TilesRoot}/{style}";

        /// <summary>Asset path of one built tile.</summary>
        public static string TilePath(string style, string tileName) => $"{StyleDir(style)}/{tileName}.asset";

        // ---- semantic tile names (the one place these strings are formed) --------------------------

        public static string GroundName(string material, int variant) => $"Ground_{material}_{variant}";
        public static string FringeName(string material, string piece) => $"Fringe_{material}_{piece}";
        public static string CliffName(string band, string piece)     => $"Cliff_{band}_{piece}";
        public static string DuneName(string band, string piece)      => $"Dune_{band}_{piece}";
        public static string ContactName(string piece)                => $"Contact_{piece}";

        [MenuItem("Hidden Harbours/Art/Build Shoreline ISO Tiles", priority = 23)]
        public static void BuildDefaultStyle() => Build(DefaultStyle);

        /// <summary>
        /// Build (or refresh) every tile of one style. Returns the number of tiles now on disk. Safe to
        /// re-run; safe to call before the kit is imported (it warns and builds nothing rather than throwing
        /// mid-region-build).
        /// </summary>
        public static int Build(string style)
        {
            EnsureFolder(StyleDir(style));

            int n = 0;
            n += BuildGround(style);
            n += BuildFringe(style);
            n += BuildLandforms(style);
            n += BuildContact(style);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (n == 0)
                Debug.LogWarning($"[ShoreIso2TileLibrary] Built no tiles for style '{style}' — the kit's " +
                                 $"sheets are missing or unsliced under {ShorelineIso2Catalog.StyleDir(style)}. " +
                                 "Import the shoreline-ISO v8 kit (and slice it) first.");
            else
                Debug.Log($"[ShoreIso2TileLibrary] {n} shoreline-ISO v8 tiles ready in {StyleDir(style)} " +
                          "(generated, not committed — re-run any time). Pick them up in Window ▸ 2D ▸ Tile " +
                          "Palette to paint by hand, or let 'Build St Peters Scene' paint the coast with them.");
            return n;
        }

        // ---- per-sheet builders --------------------------------------------------------------------

        static int BuildGround(string style)
        {
            var slices = LoadSlices(style, ShorelineIso2Catalog.Ground);
            if (slices == null) return 0;

            int n = 0;
            foreach (string material in ShorelineIso2Catalog.GroundMaterials)
                for (int v = 0; v < ShorelineIso2Catalog.GroundVariants; v++)
                    if (Upsert(style, GroundName(material, v),
                               slices, ShorelineIso2Catalog.GroundIndex(material, v))) n++;
            return n;
        }

        static int BuildFringe(string style)
        {
            var slices = LoadSlices(style, ShorelineIso2Catalog.Fringe);
            if (slices == null) return 0;

            int n = 0;
            foreach (string material in ShorelineIso2Catalog.FringeMaterials)
                foreach (string piece in ShorelineIso2Catalog.FringePieces)
                    if (Upsert(style, FringeName(material, piece),
                               slices, ShorelineIso2Catalog.FringeIndex(material, piece))) n++;
            return n;
        }

        /// <summary>
        /// The cliff and dune stacks. Built even though the St Peters painter does not place them yet: they
        /// are what the owner needs in the palette to hand-author a cliff, and building them here is what
        /// proves the two PADDING cells are skipped. <c>Cliff.png</c> is a 10 × 3 RECTANGLE but only 28 of
        /// its 30 cells are tiles — <c>cap+cave</c> and <c>mid+cave</c> are fully transparent, because a sea
        /// cave is carved at the waterline and there is no cap-cave to reach for. The catalog THROWS for
        /// those two rather than handing back an index, so the loop asks it and skips.
        /// </summary>
        static int BuildLandforms(string style)
        {
            int n = 0;

            var cliff = LoadSlices(style, ShorelineIso2Catalog.Cliff);
            if (cliff != null)
            {
                foreach (string band in ShorelineIso2Catalog.CliffBands)
                foreach (string piece in ShorelineIso2Catalog.CliffPieces)
                {
                    // The two padding cells: only the toe row has a cave column.
                    if (piece == ShorelineIso2Catalog.CavePiece && band != "toe") continue;
                    if (Upsert(style, CliffName(band, piece),
                               cliff, ShorelineIso2Catalog.CliffIndex(band, piece))) n++;
                }
            }

            var dune = LoadSlices(style, ShorelineIso2Catalog.Dune);
            if (dune != null)
            {
                foreach (string band in ShorelineIso2Catalog.DuneBands)
                foreach (string piece in ShorelineIso2Catalog.DunePieces)
                    if (Upsert(style, DuneName(band, piece),
                               dune, ShorelineIso2Catalog.DuneIndex(band, piece))) n++;
            }

            return n;
        }

        static int BuildContact(string style)
        {
            var slices = LoadSlices(style, ShorelineIso2Catalog.Contact);
            if (slices == null) return 0;

            int n = 0;
            foreach (string piece in ShorelineIso2Catalog.ContactPieces)
                if (Upsert(style, ContactName(piece),
                           slices, ShorelineIso2Catalog.ContactIndex(piece))) n++;
            return n;
        }

        // ---- loading + upserting -------------------------------------------------------------------

        /// <summary>
        /// One sheet's sub-sprites, keyed by the slicer's geometric name (<c>Ground_7</c>). Returns null (and
        /// warns once) if the sheet is absent or carries no slices — the fresh-import trap where the importer
        /// reports <c>spriteMode: Multiple</c> with an EMPTY rect table, so the sheet yields ONE sprite
        /// instead of twenty-four and nothing downstream notices.
        /// </summary>
        static Dictionary<string, Sprite> LoadSlices(string style, string sheet)
        {
            string path = ShorelineIso2Catalog.SheetPath(style, sheet);
            var sprites = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToArray();

            var grid = ShorelineIso2Catalog.Grids[sheet];
            int expected = grid.cols * grid.rows;
            if (sprites.Length < expected)
            {
                Debug.LogWarning($"[ShoreIso2TileLibrary] '{path}' yielded {sprites.Length} sprites, expected " +
                                 $"{expected} ({grid.cols}x{grid.rows}). The sheet is missing or UNSLICED — " +
                                 "spriteMode Multiple with an empty rect table imports as one sprite. Slice " +
                                 "it before painting; no tiles built for this sheet.");
                return null;
            }

            var byName = new Dictionary<string, Sprite>(sprites.Length);
            foreach (var s in sprites) byName[s.name] = s;
            return byName;
        }

        /// <summary>Create or repoint one tile. Returns false (and warns) if the slice is missing.</summary>
        static bool Upsert(string style, string tileName, Dictionary<string, Sprite> slices, int sliceIndex)
        {
            // The sheet stem the slicer used is the sheet name itself (Ground_0, Fringe_11, ...).
            string sheetStem = tileName.Substring(0, tileName.IndexOf('_'));
            string sliceName = ShorelineIso2Catalog.SliceName(sheetStem, sliceIndex);

            if (!slices.TryGetValue(sliceName, out Sprite sprite) || sprite == null)
            {
                Debug.LogWarning($"[ShoreIso2TileLibrary] slice '{sliceName}' not found for tile " +
                                 $"'{tileName}' — tile not built.");
                return false;
            }

            string path = TilePath(style, tileName);
            var existing = AssetDatabase.LoadAssetAtPath<Tile>(path);
            if (existing != null)
            {
                // REPOINT, never replace: a fresh asset gets a fresh GUID and every cell already painted
                // with this tile would silently go blank.
                if (existing.sprite != sprite)
                {
                    existing.sprite = sprite;
                    EditorUtility.SetDirty(existing);
                }
                existing.colliderType = Tile.ColliderType.None;
                return true;
            }

            var tile = ScriptableObject.CreateInstance<Tile>();
            tile.sprite = sprite;
            tile.colliderType = Tile.ColliderType.None;   // gameplay reads the terrain, never a tile shape
            AssetDatabase.CreateAsset(tile, path);
            return true;
        }

        /// <summary>Load a built tile by its semantic name, or null.</summary>
        public static Tile Load(string style, string tileName) =>
            AssetDatabase.LoadAssetAtPath<Tile>(TilePath(style, tileName));

        static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            string parent = System.IO.Path.GetDirectoryName(folder).Replace('\\', '/');
            string leaf = System.IO.Path.GetFileName(folder);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
#endif
