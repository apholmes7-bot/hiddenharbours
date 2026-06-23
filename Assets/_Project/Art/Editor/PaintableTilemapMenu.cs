#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace HiddenHarbours.Art.Editor
{
    /// <summary>
    /// One-click "give me something to paint on": adds a <see cref="Grid"/> with a child terrain
    /// <see cref="Tilemap"/> (1 m cells matching our 32 px @ PPU 32 tiles) to the currently open scene,
    /// selects it, and leaves it ready for the Tile Palette's brush. For a non-developer this removes the
    /// fiddly GameObject ▸ 2D Object ▸ Tilemap menu-hunt. The terrain layer sorts behind sprites/decor
    /// (sortingOrder −20) so painted ground never covers placed trees/buildings.
    /// <para>Menu: <c>Hidden Harbours ▸ Art ▸ Add Paintable Tilemap</c>. Safe to run more than once —
    /// each call adds a fresh, uniquely-named tilemap (e.g. for a separate "Path" or "Decoration" layer).</para>
    /// </summary>
    public static class PaintableTilemapMenu
    {
        /// <summary>Painted ground sits well behind world sprites (player ~10, decor 0..9, water ~−10).</summary>
        const int TerrainSortingOrder = -20;

        [MenuItem("Hidden Harbours/Art/Add Paintable Tilemap", priority = 21)]
        public static void AddPaintableTilemap()
        {
            // Root Grid — reuse an existing one in the scene if present so multiple layers share it.
            var grid = Object.FindFirstObjectByType<Grid>();
            if (grid == null)
            {
                var gridGo = new GameObject("Grid");
                grid = gridGo.AddComponent<Grid>();
                grid.cellSize = new Vector3(1f, 1f, 0f);   // 1 m cells = a 32 px tile at PPU 32
                Undo.RegisterCreatedObjectUndo(gridGo, "Add Grid");
            }

            // A new tilemap layer under the Grid.
            var tilemapGo = new GameObject(GameObjectUtility.GetUniqueNameForSibling(grid.transform, "TerrainTilemap"));
            tilemapGo.transform.SetParent(grid.transform, false);
            tilemapGo.AddComponent<Tilemap>();
            var renderer = tilemapGo.AddComponent<TilemapRenderer>();
            renderer.sortingOrder = TerrainSortingOrder;
            Undo.RegisterCreatedObjectUndo(tilemapGo, "Add Paintable Tilemap");

            Selection.activeGameObject = tilemapGo;
            EditorGUIUtility.PingObject(tilemapGo);
            Debug.Log("[PaintableTilemapMenu] Added a Grid + TerrainTilemap to the open scene. " +
                      "Open Window ▸ 2D ▸ Tile Palette, pick the 'HiddenHarboursTerrain' palette and a tile, " +
                      "then paint onto this tilemap. Remember to SAVE the scene (Ctrl+S). See docs/authoring-scenes.md.");
        }
    }
}
#endif
