#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;               // Tile (engine TilemapModule)
using HiddenHarbours.Art.Editor;          // RoadKitV3 — the kit's own geometry, names and pivots

namespace HiddenHarbours.App.Editor
{
    /// <summary>
    /// Turns the sliced <b>road/path kit v3</b> atlases into paintable <see cref="Tile"/> assets — two per
    /// atlas cell, a TOP and a SKIRT, named for what they are: <c>Road_gravel_23_top</c>.
    /// <see cref="NineMileCreekRoadPainter"/> paints with them; the owner can pick the same tiles up in the
    /// Tile Palette and paint a lane by hand.
    ///
    /// <para><b>The direct descendant of <see cref="ShoreIso2TileLibrary"/></b>, and deliberately the same
    /// shape: generated rather than committed, idempotent, GUID-stable, and it refuses loudly on an
    /// unsliced sheet instead of building tiles with no pixels in them.</para>
    ///
    /// <para><b>⚠ TWO TILES PER CELL, because the kit's composite rule is two-pass.</b> A cell's SKIRT —
    /// its kerb faces, its south drop faces and its baked contact shadow — hangs 22 px below its own
    /// ground square and must draw IN FRONT of the ground square of the cell to its south. One
    /// <see cref="Tilemap"/> cannot express that at any sort order, so a road is two tilemaps on adjacent
    /// orders and every cell contributes a tile to each. <see cref="RoadKitSlicer"/> already cut the
    /// sprites that way and pinned both to the same world pivot; this only wraps them.</para>
    ///
    /// <para><b>⚠ ONLY THE SURFACES A REGION ASKS FOR.</b> The kit bakes eleven surfaces and 47 cells, so
    /// building the lot would be 1,034 tile assets for a region that uses four of them. <see cref="Build"/>
    /// takes the surfaces its caller actually paves; the menu item builds all eleven for the owner's
    /// palette, which is the one case where wanting them all is the point.</para>
    /// </summary>
    public static class RoadTileLibrary
    {
        /// <summary>Output root — beside <see cref="ShoreIso2TileLibrary.TilesRoot"/>, one folder for the
        /// whole kit (a road tile has no shading style to fork on).</summary>
        public const string TilesRoot = "Assets/_Project/Art/Tilesets/Tiles/RoadsV3";

        /// <summary>Asset path of one built tile.</summary>
        public static string TilePath(string tileName) => $"{TilesRoot}/{tileName}.asset";

        /// <summary>The one place a road tile's name is formed. <paramref name="suffix"/> is
        /// <see cref="RoadKitV3.TopSuffix"/> or <see cref="RoadKitV3.SkirtSuffix"/>.</summary>
        public static string TileName(string surface, int index, string suffix) =>
            $"Road_{surface}_{index}_{suffix}";

        /// <inheritdoc cref="TileName"/>
        public static string TopTileName(string surface, int index) =>
            TileName(surface, index, RoadKitV3.TopSuffix);

        /// <inheritdoc cref="TileName"/>
        public static string SkirtTileName(string surface, int index) =>
            TileName(surface, index, RoadKitV3.SkirtSuffix);

        [MenuItem("Hidden Harbours/Art/Import (after a new drop)/Build Road Kit v3 Tiles", priority = 238)]
        public static void BuildEverySurface() => Build(RoadKitV3.Surfaces);

        /// <summary>
        /// Build (or refresh) the tiles for <paramref name="surfaces"/>. Returns how many are on disk
        /// afterwards. Safe to re-run and safe to call before the kit is imported — it warns and builds
        /// nothing rather than throwing in the middle of a region build.
        /// </summary>
        public static int Build(IEnumerable<string> surfaces)
        {
            if (surfaces == null) return 0;
            EnsureFolder(TilesRoot);

            int built = 0;
            var done = new HashSet<string>();

            foreach (string surface in surfaces)
            {
                if (string.IsNullOrEmpty(surface) || !done.Add(surface)) continue;
                built += BuildSurface(surface);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (built == 0)
                Debug.LogWarning(
                    "[RoadTileLibrary] built no road tiles — the v3 atlases are missing or UNSLICED " +
                    $"under {RoadKitV3.RoadsDir}. Run 'Hidden Harbours ▸ Dev ▸ Bake Road Kit v3' (or " +
                    "re-slice), then re-run. Any region paved in the meantime lays its cells correctly " +
                    "and draws nothing.");
            else
                Debug.Log(
                    $"[RoadTileLibrary] {built} road tile(s) ready in {TilesRoot} (generated from the " +
                    "sliced v3 atlases — re-run any time). Pick them up in Window ▸ 2D ▸ Tile Palette to " +
                    "lay a lane by hand, or let a region builder paint with them.");

            return built;
        }

        static int BuildSurface(string surface)
        {
            string atlas;
            try { atlas = RoadKitV3.AtlasPath(surface); }
            catch (System.ArgumentException)
            {
                Debug.LogWarning(
                    $"[RoadTileLibrary] '{surface}' is not one of the kit's surfaces " +
                    $"({string.Join(", ", RoadKitV3.Surfaces)}) — no tiles built for it. A region naming " +
                    "a surface the kit does not bake is an authoring error, not a missing asset.");
                return 0;
            }

            var slices = LoadSlices(atlas);
            if (slices == null) return 0;

            string stem = Path.GetFileNameWithoutExtension(atlas);
            int built = 0;
            for (int i = 0; i < RoadKitV3.BlobCount; i++)
            {
                if (Upsert(TopTileName(surface, i), slices, RoadKitV3.TopSpriteName(stem, i))) built++;
                if (Upsert(SkirtTileName(surface, i), slices, RoadKitV3.SkirtSpriteName(stem, i))) built++;
            }
            return built;
        }

        /// <summary>
        /// One atlas's sub-sprites by name, or null with one warning.
        ///
        /// <para>⚠ The count is checked against the kit's own arithmetic (47 cells × 2 sub-sprites), which
        /// is the fresh-import trap this repo has been caught by before: an importer set to
        /// <c>spriteMode: Multiple</c> with an EMPTY rect table yields ONE sprite instead of ninety-four,
        /// and nothing downstream notices — the road simply draws blank.</para>
        /// </summary>
        static Dictionary<string, Sprite> LoadSlices(string atlasPath)
        {
            var sprites = AssetDatabase.LoadAllAssetsAtPath(atlasPath).OfType<Sprite>().ToArray();
            int expected = RoadKitV3.BlobCount * 2;

            if (sprites.Length < expected)
            {
                Debug.LogWarning(
                    $"[RoadTileLibrary] '{atlasPath}' yielded {sprites.Length} sprites, the kit slices " +
                    $"{expected} ({RoadKitV3.BlobCount} cells × top+skirt). The sheet is missing or " +
                    "UNSLICED — no tiles built for it.");
                return null;
            }

            var byName = new Dictionary<string, Sprite>(sprites.Length);
            foreach (var s in sprites) byName[s.name] = s;
            return byName;
        }

        /// <summary>Create or repoint one tile. REPOINTS an existing asset rather than replacing it: a
        /// fresh asset carries a fresh GUID, and every cell already painted with the old one would
        /// silently go blank.</summary>
        static bool Upsert(string tileName, Dictionary<string, Sprite> slices, string sliceName)
        {
            if (!slices.TryGetValue(sliceName, out Sprite sprite) || sprite == null)
            {
                Debug.LogWarning($"[RoadTileLibrary] slice '{sliceName}' not found — tile '{tileName}' " +
                                 "not built.");
                return false;
            }

            string path = TilePath(tileName);
            var existing = AssetDatabase.LoadAssetAtPath<Tile>(path);
            if (existing != null)
            {
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
            // Gameplay reads the authored terrain, never a tile shape — the same rule every other tile in
            // this project follows. A road is walkable because the ground under it is, not because a
            // collider says so.
            tile.colliderType = Tile.ColliderType.None;
            AssetDatabase.CreateAsset(tile, path);
            return true;
        }

        /// <summary>Load a built tile by name, or null.</summary>
        public static Tile Load(string tileName) => AssetDatabase.LoadAssetAtPath<Tile>(TilePath(tileName));

        static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
            string leaf = Path.GetFileName(folder);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
#endif
