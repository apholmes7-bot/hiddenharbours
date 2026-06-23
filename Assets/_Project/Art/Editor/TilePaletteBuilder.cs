#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Tilemaps;   // GridPaletteUtility, GridPalette (Unity.2D.Tilemap.Editor)
using UnityEngine;
using UnityEngine.Tilemaps;

namespace HiddenHarbours.Art.Editor
{
    /// <summary>
    /// Builds a ready-to-use <b>Tile Palette</b> asset from the terrain tiles + shoreline RuleTile that
    /// <see cref="TileAssetBuilder"/> generates, so the owner can open <c>Window ▸ 2D ▸ Tile Palette</c>,
    /// pick the "Hidden Harbours Terrain" palette, click a tile, and paint it onto a scene's Tilemap —
    /// no developer step in between. A Tile Palette is itself a small prefab (a <see cref="Grid"/> with a
    /// child <see cref="Tilemap"/> holding the tiles, plus a <see cref="GridPalette"/> sub-asset); we
    /// create it with the engine's own <see cref="GridPaletteUtility"/> then lay the tiles out in a row.
    /// <para>Menu: <c>Hidden Harbours ▸ Art ▸ Build Tile Palette</c> (re-runnable — rebuilds the palette
    /// in place). Run <c>Build Terrain Tiles</c> first so the tiles exist. See <c>docs/authoring-scenes.md</c>.</para>
    /// </summary>
    public static class TilePaletteBuilder
    {
        const string PaletteDir  = "Assets/_Project/Art/Tilesets/Palettes";
        const string PaletteName = "HiddenHarboursTerrain";
        static string PalettePath => $"{PaletteDir}/{PaletteName}.prefab";

        [MenuItem("Hidden Harbours/Art/Build Tile Palette", priority = 20)]
        public static void Build()
        {
            // 1. Make sure the tiles exist (re-runnable; cheap if already built).
            if (AssetDatabase.LoadAssetAtPath<TileBase>(TileAssetBuilder.TilePath("Sand")) == null)
            {
                Debug.Log("[TilePaletteBuilder] Terrain tiles missing — building them first.");
                TileAssetBuilder.Build();
            }

            EnsureFolder(PaletteDir);

            // 2. A fresh palette every run keeps it in sync with the current tile set; delete any prior one.
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PalettePath) != null)
                AssetDatabase.DeleteAsset(PalettePath);

            // 3. Create the empty palette via the engine utility (1 m cells = our 32 px @ PPU 32 tiles).
            var palette = GridPaletteUtility.CreateNewPalette(
                PaletteDir, PaletteName,
                GridLayout.CellLayout.Rectangle,
                GridPalette.CellSizing.Automatic,
                Vector3.one,
                GridLayout.CellSwizzle.XYZ);

            if (palette == null)
            {
                Debug.LogError("[TilePaletteBuilder] GridPaletteUtility.CreateNewPalette returned null — palette not built.");
                return;
            }

            // 4. Gather the tiles to place: the terrain Tiles, then the autotiling Shoreline RuleTile.
            var tiles = new List<TileBase>();
            foreach (var (_, tileName) in TileAssetBuilder.Terrain)
            {
                var t = AssetDatabase.LoadAssetAtPath<TileBase>(TileAssetBuilder.TilePath(tileName));
                if (t != null) tiles.Add(t);
            }
            var shoreline = AssetDatabase.LoadAssetAtPath<TileBase>(TileAssetBuilder.ShorelinePath);
            if (shoreline != null) tiles.Add(shoreline);

            if (tiles.Count == 0)
            {
                Debug.LogWarning("[TilePaletteBuilder] No tiles found to add to the palette. Run Build Terrain Tiles.");
                return;
            }

            // 5. Paint the tiles into the palette prefab's Tilemap (a left-to-right strip).
            //    Edit a prefab INSTANCE and ApplyPrefabInstance back — the same flow the engine's
            //    CreateNewPalette uses internally. (Do NOT LoadPrefabContents + SaveAsPrefabAsset here:
            //    that replaces the asset and would drop the GridPalette sub-asset the palette window reads.)
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(palette);
            try
            {
                var tilemap = instance.GetComponentInChildren<Tilemap>();
                if (tilemap == null)
                {
                    Debug.LogError("[TilePaletteBuilder] Palette prefab has no Tilemap layer — cannot place tiles.");
                    return;
                }

                for (int i = 0; i < tiles.Count; i++)
                    tilemap.SetTile(new Vector3Int(i, 0, 0), tiles[i]);

                PrefabUtility.ApplyPrefabInstance(instance, InteractionMode.AutomatedAction);
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            var built = AssetDatabase.LoadAssetAtPath<GameObject>(PalettePath);
            Selection.activeObject = built;
            EditorGUIUtility.PingObject(built);
            Debug.Log($"[TilePaletteBuilder] Built the '{PaletteName}' Tile Palette ({tiles.Count} tiles) at {PalettePath}. " +
                      "Open Window ▸ 2D ▸ Tile Palette and select it from the palette dropdown to paint (see docs/authoring-scenes.md).");
        }

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
