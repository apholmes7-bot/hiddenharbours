#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.Art.Editor
{
    /// <summary>
    /// One-shot setup for the scene-painting toolkit: builds the terrain tiles, the Tile Palette, and the
    /// decor prefabs in the right order, so the owner runs <i>one</i> menu command and is then ready to
    /// paint. Each step is also available individually under <c>Hidden Harbours ▸ Art</c>. After this,
    /// open <c>Window ▸ 2D ▸ Tile Palette</c> and follow <c>docs/authoring-scenes.md</c>.
    /// <para>Menu: <c>Hidden Harbours ▸ Art ▸ Build Scene-Painting Toolkit (tiles + palette + decor)</c>.</para>
    /// </summary>
    public static class SceneAuthoringMenu
    {
        [MenuItem("Hidden Harbours/Art/Build Scene-Painting Toolkit (tiles + palette + decor)", priority = 0)]
        public static void BuildAll()
        {
            TileAssetBuilder.Build();
            TilePaletteBuilder.Build();
            DecorPrefabBuilder.Build();
            Debug.Log("[SceneAuthoringMenu] Scene-painting toolkit ready: terrain tiles + 'HiddenHarboursTerrain' " +
                      "Tile Palette + decor prefabs (Trees/Buildings/Props). Next: Window ▸ 2D ▸ Tile Palette, " +
                      "then Hidden Harbours ▸ Art ▸ Add Paintable Tilemap on a scratch scene. See docs/authoring-scenes.md.");
        }
    }
}
#endif
