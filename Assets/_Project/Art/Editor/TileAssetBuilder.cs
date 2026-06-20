#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;   // Tile (engine TilemapModule)
// RuleTile is in namespace UnityEngine, assembly Unity.2D.Tilemap.Extras (referenced in the asmdef).

namespace HiddenHarbours.Art.Editor
{
    /// <summary>
    /// VS-24 — generates paintable Unity tile assets from the imported Coddle Cove sprites: a plain
    /// <see cref="Tile"/> per terrain sprite, plus an autotiling <see cref="RuleTile"/> for the shoreline
    /// that picks edge/corner sprites by neighbour. <b>art-pipeline builds the tile assets here; the
    /// actual terrain painting of the region tilemap is world-content's lane</b> (Art/imported-assets.md).
    /// Menu: <c>Hidden Harbours ▸ Art ▸ Build Coddle Cove Tiles</c>. Re-runnable.
    /// </summary>
    public static class TileAssetBuilder
    {
        const string Tilesets = "Assets/_Project/Art/Tilesets";
        const string OutDir   = Tilesets + "/Tiles";

        // Terrain fill sprites → one plain Tile each (single sprite, no autotiling).
        static readonly string[] Terrain = { "Sand", "Rock", "Grass", "Dirt", "WharfDeck", "Foam" };

        [MenuItem("Hidden Harbours/Art/Build Coddle Cove Tiles")]
        public static void Build()
        {
            if (!AssetDatabase.IsValidFolder(OutDir))
                AssetDatabase.CreateFolder(Tilesets, "Tiles");

            int n = 0;
            foreach (var name in Terrain)
            {
                var sprite = LoadSprite($"{Tilesets}/{name}.png");
                if (sprite == null) { Debug.LogWarning($"[TileAssetBuilder] missing {name}.png — skipped."); continue; }
                var tile = ScriptableObject.CreateInstance<Tile>();
                tile.sprite = sprite;
                tile.colliderType = Tile.ColliderType.None;
                CreateOrReplace(tile, $"{OutDir}/{name}.asset");
                n++;
            }

            BuildShorelineRuleTile();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[TileAssetBuilder] Built {n} terrain Tiles + a Shoreline RuleTile under {OutDir}. " +
                      "world-content paints the Coddle Cove tilemap with these (see Art/imported-assets.md).");
        }

        // The shoreline autotiles its edge: where the painted shore tile is ABSENT (open water) on a
        // side, draw the wet edge; two adjacent water sides => outer corner; a single diagonal of water
        // with orthogonal shore => inner corner. Rotated rules cover all four orientations from one
        // sprite. A sensible starting ruleset — tune sprite orientation in the Tile Palette if needed.
        static void BuildShorelineRuleTile()
        {
            var edge  = LoadSprite($"{Tilesets}/ShoreEdge.png");
            var outer = LoadSprite($"{Tilesets}/ShoreCornerOuter.png");
            var inner = LoadSprite($"{Tilesets}/ShoreCornerInner.png");
            if (edge == null) { Debug.LogWarning("[TileAssetBuilder] ShoreEdge.png missing — RuleTile skipped."); return; }

            var rt = ScriptableObject.CreateInstance<RuleTile>();
            rt.m_DefaultSprite = edge;
            rt.m_DefaultColliderType = Tile.ColliderType.None;

            const int Shore = RuleTile.TilingRule.Neighbor.This;     // shore tile present
            const int Water = RuleTile.TilingRule.Neighbor.NotThis;  // open water (shore absent)
            var up = new Vector3Int(0, 1, 0);
            var right = new Vector3Int(1, 0, 0);
            var upRight = new Vector3Int(1, 1, 0);

            rt.m_TilingRules.Add(Rule(edge,  new[] { up },           new[] { Water }));
            if (outer != null)
                rt.m_TilingRules.Add(Rule(outer, new[] { up, right },    new[] { Water, Water }));
            if (inner != null)
                rt.m_TilingRules.Add(Rule(inner, new[] { right, up, upRight }, new[] { Shore, Shore, Water }));

            CreateOrReplace(rt, $"{OutDir}/Shoreline.asset");
        }

        static RuleTile.TilingRule Rule(Sprite sprite, Vector3Int[] positions, int[] neighbors) => new()
        {
            m_Sprites = new[] { sprite },
            m_ColliderType = Tile.ColliderType.None,
            m_Output = RuleTile.TilingRuleOutput.OutputSprite.Single,
            m_RuleTransform = RuleTile.TilingRuleOutput.Transform.Rotated,
            m_NeighborPositions = new List<Vector3Int>(positions),
            m_Neighbors = new List<int>(neighbors),
        };

        static Sprite LoadSprite(string path) => AssetDatabase.LoadAssetAtPath<Sprite>(path);

        static void CreateOrReplace(Object asset, string path)
        {
            if (AssetDatabase.LoadAssetAtPath<Object>(path) != null) AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(asset, path);
        }
    }
}
#endif
