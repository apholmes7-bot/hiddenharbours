#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;   // Tile (engine TilemapModule)
// RuleTile is in namespace UnityEngine, assembly Unity.2D.Tilemap.Extras (referenced in the asmdef).

namespace HiddenHarbours.Art.Editor
{
    /// <summary>
    /// Generates paintable Unity tile assets from the imported terrain sprites: a plain
    /// <see cref="Tile"/> per terrain sprite (sand/rock/grass/dirt/wharf/foam + the seamless water
    /// SeaTile), plus an autotiling <see cref="RuleTile"/> for the shoreline that picks edge/corner
    /// sprites by neighbour. These are the tiles the owner picks in the <b>Tile Palette</b>
    /// (Window ▸ 2D ▸ Tile Palette) to paint a scene's Tilemap — see <c>TilePaletteBuilder</c> for the
    /// palette asset and <c>docs/authoring-scenes.md</c> for the non-developer painting workflow.
    /// <para>Menu: <c>Hidden Harbours ▸ Art ▸ Build Terrain Tiles</c> (re-runnable). The legacy
    /// "Build Coddle Cove Tiles" label still works.</para>
    /// </summary>
    public static class TileAssetBuilder
    {
        const string Tilesets = "Assets/_Project/Art/Tilesets";
        public const string OutDir = Tilesets + "/Tiles";

        /// <summary>
        /// Terrain fill sprites → one plain <see cref="Tile"/> each (single sprite, no autotiling).
        /// "name" is both the .png under Tilesets/ and the output Tile asset name. <c>Water/SeaTile</c>
        /// is the seamless open-water tile (it lives in the Tilesets/Water/ subfolder so the import lock
        /// sets Repeat wrap). Exposed so the palette builder paints the same set.
        /// </summary>
        public static readonly (string sprite, string tile)[] Terrain =
        {
            ("Sand",            "Sand"),
            ("Rock",            "Rock"),
            ("Grass",           "Grass"),
            ("Dirt",            "Dirt"),
            ("WharfDeck",       "WharfDeck"),
            ("Foam",            "Foam"),
            ("Water/SeaTile",   "Water"),
        };

        /// <summary>Output Tile asset path for a built terrain tile name (e.g. "Sand" → .../Tiles/Sand.asset).</summary>
        public static string TilePath(string tileName) => $"{OutDir}/{tileName}.asset";

        /// <summary>Output path of the autotiling shoreline RuleTile.</summary>
        public static string ShorelinePath => $"{OutDir}/Shoreline.asset";

        [MenuItem("Hidden Harbours/Art/Build Terrain Tiles")]
        public static void Build()
        {
            if (!AssetDatabase.IsValidFolder(OutDir))
                AssetDatabase.CreateFolder(Tilesets, "Tiles");

            int n = 0;
            foreach (var (spriteName, tileName) in Terrain)
            {
                var sprite = LoadSpriteAny($"{Tilesets}/{spriteName}.png");
                if (sprite == null) { Debug.LogWarning($"[TileAssetBuilder] missing {spriteName}.png — skipped."); continue; }
                var tile = ScriptableObject.CreateInstance<Tile>();
                tile.sprite = sprite;
                tile.colliderType = Tile.ColliderType.None;
                CreateOrReplace(tile, TilePath(tileName));
                n++;
            }

            BuildShorelineRuleTile();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[TileAssetBuilder] Built {n} terrain Tiles + a Shoreline RuleTile under {OutDir}. " +
                      "Now run Hidden Harbours ▸ Art ▸ Build Tile Palette, then paint in Window ▸ 2D ▸ Tile Palette " +
                      "(see docs/authoring-scenes.md).");
        }

        // Backward-compatible alias for the original menu path documented in Art/imported-assets.md (VS-24).
        [MenuItem("Hidden Harbours/Art/Build Coddle Cove Tiles")]
        public static void BuildLegacyAlias() => Build();

        // The shoreline autotiles its edge: where the painted shore tile is ABSENT (open water) on a
        // side, draw the wet edge; two adjacent water sides => outer corner; a single diagonal of water
        // with orthogonal shore => inner corner. Rotated rules cover all four orientations from one
        // sprite. A sensible starting ruleset — tune sprite orientation in the Tile Palette if needed.
        static void BuildShorelineRuleTile()
        {
            var edge  = LoadSpriteAny($"{Tilesets}/ShoreEdge.png");
            var outer = LoadSpriteAny($"{Tilesets}/ShoreCornerOuter.png");
            var inner = LoadSpriteAny($"{Tilesets}/ShoreCornerInner.png");
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

            CreateOrReplace(rt, ShorelinePath);
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

        /// <summary>
        /// Loads the sprite at <paramref name="path"/>, tolerant of the project's Sprite Mode = Multiple
        /// import (the imported PNGs slice to a single <c>Name_0</c> sub-sprite, so a direct
        /// <see cref="AssetDatabase.LoadAssetAtPath{Sprite}"/> returns null — we fall back to the first
        /// sub-sprite). Same pattern GreyboxBuilder uses (see memory: imported-art-spritemode-multiple).
        /// </summary>
        public static Sprite LoadSpriteAny(string path)
        {
            var direct = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (direct != null) return direct;
            return AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>()
                                 .OrderBy(s => s.name).FirstOrDefault();
        }

        static void CreateOrReplace(Object asset, string path)
        {
            if (AssetDatabase.LoadAssetAtPath<Object>(path) != null) AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(asset, path);
        }
    }
}
#endif
