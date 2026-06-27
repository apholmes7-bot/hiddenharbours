#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.Art.Editor
{
    /// <summary>
    /// Turns the imported decor sprites (trees, buildings, props) into drag-and-drop placeable
    /// <b>prefabs</b> under <c>Assets/_Project/Prefabs/Decor/</c>, grouped <c>Trees/ Buildings/ Props/</c>.
    /// Each prefab is a single GameObject with a <see cref="SpriteRenderer"/> already pointed at the right
    /// sprite, at our true metric scale (PPU 32, scale 1) and a sensible sorting order, so the owner drags
    /// one into a scene and it sits right — trees plant at the trunk (the sprites are BottomCenter-pivoted
    /// on import), buildings/props centre. The owner never touches a SpriteRenderer by hand.
    /// <para>Menu: <c>Hidden Harbours ▸ Art ▸ Build Decor Prefabs</c> (re-runnable — rebuilds each prefab
    /// in place, so re-importing better art and re-running just refreshes the sprite ref). See
    /// <c>docs/authoring-scenes.md</c> for the placement workflow.</para>
    /// </summary>
    public static class DecorPrefabBuilder
    {
        const string PrefabRoot = "Assets/_Project/Prefabs/Decor";
        const string ArtSprites = "Assets/_Project/Art/Sprites";

        // Sorting orders: ground tiles sit at −20 (PaintableTilemapMenu), the player at 10. Decor lands
        // between, above ground and below the player. The owner fine-tunes per-instance for ¾ overlap
        // (see docs/authoring-scenes.md "Sorting"). Buildings sit a touch above ground props.
        const int TreeSortingOrder     = 5;
        const int BuildingSortingOrder = 4;
        const int PropSortingOrder     = 3;

        [MenuItem("Hidden Harbours/Art/Build Decor Prefabs", priority = 22)]
        public static void Build()
        {
            EnsureFolder(PrefabRoot);
            int n = 0;

            // --- Trees: Tree01..Tree40 (BottomCenter pivot on import — they plant at the trunk).
            //     Tree38..Tree40 are the reference-style painterly evergreens (tall, tiered, left-lit). ---
            EnsureFolder($"{PrefabRoot}/Trees");
            for (int i = 1; i <= 40; i++)
            {
                string name = $"Tree{i:00}";
                string sprite = $"{ArtSprites}/Environment/Trees/{name}.png";
                if (BuildDecorPrefab(name, sprite, $"{PrefabRoot}/Trees/{name}.prefab", TreeSortingOrder)) n++;
            }

            // --- Buildings (centre pivot). Cottage day/night, Greywick houses, shipwright, buyer stall. ---
            EnsureFolder($"{PrefabRoot}/Buildings");
            string[] buildings =
            {
                "Cottage", "CottageNight", "ShipwrightShed", "FishBuyerStall",
                "GreywickHouseRed", "GreywickHouseTeal",
            };
            foreach (var b in buildings)
                if (BuildDecorPrefab(b, $"{ArtSprites}/Buildings/{b}.png", $"{PrefabRoot}/Buildings/{b}.prefab", BuildingSortingOrder)) n++;

            // --- Props (centre pivot). Wharf/cove dressing + the clam-flat decor visuals. ---
            EnsureFolder($"{PrefabRoot}/Props");
            string[] props =
            {
                "Barrel", "Crate", "WharfPost", "LobsterBuoy", "LobsterTrap",
                "FishingSpot", "ClamHole",
            };
            foreach (var p in props)
                if (BuildDecorPrefab(p, $"{ArtSprites}/{p}.png", $"{PrefabRoot}/Props/{p}.prefab", PropSortingOrder)) n++;

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[DecorPrefabBuilder] Built {n} decor prefabs under {PrefabRoot} (Trees/ Buildings/ Props/). " +
                      "Drag one from the Project window into a scene to place it (see docs/authoring-scenes.md).");
        }

        /// <summary>
        /// Builds (or rebuilds in place) one decor prefab: a GameObject named <paramref name="name"/> with a
        /// SpriteRenderer pointed at the first sub-sprite of <paramref name="spritePath"/> at metric scale.
        /// Returns false (with a warning) if the source sprite is missing, so a partial art set still builds
        /// what it can.
        /// </summary>
        static bool BuildDecorPrefab(string name, string spritePath, string prefabPath, int sortingOrder)
        {
            var sprite = TileAssetBuilder.LoadSpriteAny(spritePath);
            if (sprite == null) { Debug.LogWarning($"[DecorPrefabBuilder] missing sprite {spritePath} — {name} skipped."); return false; }

            var go = new GameObject(name);
            try
            {
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = sprite;                 // pivot is baked into the sub-sprite by the import lock
                sr.sortingOrder = sortingOrder;
                go.transform.localScale = Vector3.one;   // honest metric size — never scale a real sprite

                if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null)
                    AssetDatabase.DeleteAsset(prefabPath);
                PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
                return true;
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

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
