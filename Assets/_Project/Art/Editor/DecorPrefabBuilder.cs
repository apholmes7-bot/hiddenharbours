#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.Art.Editor
{
    /// <summary>
    /// Turns the imported decor sprites (trees, buildings, props, grass) into drag-and-drop placeable
    /// <b>prefabs</b> under <c>Assets/_Project/Prefabs/Decor/</c>, grouped <c>Trees/ Buildings/ Props/ Grass/</c>.
    /// Each prefab is a single GameObject with a <see cref="SpriteRenderer"/> already pointed at the right
    /// sprite, at our true metric scale (PPU 32, scale 1) and a sensible sorting order, so the owner drags
    /// one into a scene and it sits right — trees plant at the trunk (the sprites are BottomCenter-pivoted
    /// on import), buildings/props centre. The owner never touches a SpriteRenderer by hand.
    /// <para><b>Grass</b> is the wind-swaying living grass (PR #102): a <c>GrassClump</c> that stamps a dense
    /// pre-scattered patch (a mix of the tuft variants on the <c>Grass</c> material) and a single
    /// <c>GrassTuft</c> for edges/detail. They sway automatically in Play (the self-installing
    /// <c>GrassWindBridge</c> feeds the shared wind); the footstep bend needs the player to carry a
    /// <c>GrassFootstep</c> component.</para>
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
        // Grass is low ground-cover: above the ground tiles (−20) and below props/trees (so a barrel or a
        // trunk overlaps the grass in front of it). A constant order keeps a clump from ever poking above
        // other decor; within a clump the back-to-front draw order gives the ¾ depth read (see scatter below).
        const int GrassSortingOrder    = 2;

        const string GrassMaterialPath = "Assets/_Project/Art/Materials/Grass.mat";
        // The grass tuft variants (medium / short / tall) — mixed for a dense, painterly clump.
        static readonly string[] GrassTuftSprites =
        {
            $"{ArtSprites}/GrassTuft.png",
            $"{ArtSprites}/GrassTuft_Short.png",
            $"{ArtSprites}/GrassTuft_Tall.png",
        };

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

            // --- Grass (wind-swaying living grass, PR #102): a stamp-a-patch GrassClump + a single GrassTuft. ---
            EnsureFolder($"{PrefabRoot}/Grass");
            n += BuildGrassPrefabs($"{PrefabRoot}/Grass");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[DecorPrefabBuilder] Built {n} decor prefabs under {PrefabRoot} (Trees/ Buildings/ Props/ Grass/). " +
                      "Drag one from the Project window into a scene to place it (see docs/authoring-scenes.md). " +
                      "Grass sways in Play automatically; for the footstep bend, put a GrassFootstep on the player.");
        }

        /// <summary>
        /// Builds the grass prefabs: a single <c>GrassTuft</c> (one tuft, for edges/detail) and a
        /// <c>GrassClump</c> (a dense pre-scattered patch you stamp down to fill a clearing). Both point at the
        /// shared <c>Grass</c> material so they sway off the same wind as the water (the self-installing
        /// <c>GrassWindBridge</c> drives it — no per-object wiring). Returns the number built; warns and skips
        /// gracefully if the grass material or tuft sprites aren't imported yet.
        /// </summary>
        static int BuildGrassPrefabs(string grassDir)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(GrassMaterialPath);
            if (material == null)
            {
                Debug.LogWarning($"[DecorPrefabBuilder] grass material {GrassMaterialPath} missing — grass skipped " +
                                 "(open Unity so it imports the grass shader + material, then re-run).");
                return 0;
            }

            var tufts = new List<Sprite>();
            foreach (string p in GrassTuftSprites)
            {
                var s = TileAssetBuilder.LoadSpriteAny(p);
                if (s != null) tufts.Add(s);
            }
            if (tufts.Count == 0)
            {
                Debug.LogWarning("[DecorPrefabBuilder] no grass tuft sprites found — grass skipped.");
                return 0;
            }

            int built = 0;
            if (BuildGrassTuftPrefab("GrassTuft", tufts[0], material, $"{grassDir}/GrassTuft.prefab")) built++;
            if (BuildGrassClumpPrefab("GrassClump", tufts, material, $"{grassDir}/GrassClump.prefab")) built++;
            return built;
        }

        /// <summary>One tuft with the grass material — for hand-placing detail or thinning the edge of a patch.</summary>
        static bool BuildGrassTuftPrefab(string name, Sprite sprite, Material material, string prefabPath)
        {
            var go = new GameObject(name);
            try
            {
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = sprite;
                sr.sharedMaterial = material;     // the wind-swaying Grass material
                sr.sortingOrder = GrassSortingOrder;
                go.transform.localScale = Vector3.one;
                SavePrefabReplacing(go, prefabPath);
                return true;
            }
            finally { Object.DestroyImmediate(go); }
        }

        /// <summary>
        /// A dense, pre-scattered patch (~2.5 m across) of mixed tuft variants under one root, with per-tuft
        /// scale + tint jitter for a painterly read — the owner drags a few of these to fill a clearing. The
        /// scatter is DETERMINISTIC (fixed seed) so re-running the builder rebuilds the same clump (clean diffs,
        /// stable scenes). Children are emitted back-to-front (highest Y first) so the front tufts draw on top —
        /// the ¾ depth read — while every tuft shares one sorting order so the clump never crosses other decor.
        /// </summary>
        static bool BuildGrassClumpPrefab(string name, List<Sprite> tufts, Material material, string prefabPath)
        {
            const int Count = 22;
            const float Radius = 1.25f;   // metres → a ~2.5 m patch

            var root = new GameObject(name);
            try
            {
                var rng = new System.Random(20240626);

                // Scatter positions in a disc, then sort back-to-front (descending Y) for the depth read.
                var positions = new List<Vector2>(Count);
                for (int i = 0; i < Count; i++)
                {
                    double ang = rng.NextDouble() * 2.0 * System.Math.PI;
                    float r = Radius * Mathf.Sqrt((float)rng.NextDouble());   // uniform over the disc
                    positions.Add(new Vector2(Mathf.Cos((float)ang) * r, Mathf.Sin((float)ang) * r));
                }
                positions.Sort((a, b) => b.y.CompareTo(a.y));   // back (high Y) first → front drawn last/on top

                foreach (var pos in positions)
                {
                    var tuftGo = new GameObject("Tuft");
                    tuftGo.transform.SetParent(root.transform, false);
                    tuftGo.transform.localPosition = new Vector3(pos.x, pos.y, 0f);
                    float s = 0.85f + (float)rng.NextDouble() * 0.4f;
                    tuftGo.transform.localScale = new Vector3(s, s, 1f);

                    var sr = tuftGo.AddComponent<SpriteRenderer>();
                    sr.sprite = tufts[rng.Next(tufts.Count)];
                    sr.sharedMaterial = material;
                    sr.sortingOrder = GrassSortingOrder;
                    // per-tuft tint jitter (the shader multiplies vertex colour) so the patch reads painterly.
                    float v = 0.82f + (float)rng.NextDouble() * 0.30f;
                    float warm = (float)(rng.NextDouble() * 0.10 - 0.05);
                    sr.color = new Color(Mathf.Clamp01(v + warm), Mathf.Clamp01(v), Mathf.Clamp01(v - warm * 0.5f), 1f);
                }

                SavePrefabReplacing(root, prefabPath);
                return true;
            }
            finally { Object.DestroyImmediate(root); }
        }

        /// <summary>Save <paramref name="go"/> as a prefab, replacing any existing one in place.</summary>
        static void SavePrefabReplacing(GameObject go, string prefabPath)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null)
                AssetDatabase.DeleteAsset(prefabPath);
            PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
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
