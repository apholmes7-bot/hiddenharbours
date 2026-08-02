#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HiddenHarbours.Art.Editor
{
    /// <summary>
    /// Turns the baked Acadian tree sheets into drag-and-drop <b>prefabs</b> under
    /// <see cref="AcadianTreeCatalog.PrefabRoot"/> — one per species (per season, once the other
    /// seasons are baked). The tree half of what <see cref="DecorPrefabBuilder"/> does for flowers,
    /// and deliberately the same shape: the BUILDER is committed, its output is not. The owner runs
    /// the menu item; nothing generated is checked in.
    ///
    /// <para>Everything a tree needs to sit right — the trunk-foot pivot, the per-species trunk
    /// anchor, honest metric scale — comes from the bake's own <c>Trees.json</c> via
    /// <see cref="AcadianTreeCatalog"/>. Re-run after a re-bake and every prefab refreshes IN PLACE,
    /// so scene instances keep their prefab link and the owner does not re-place a forest.</para>
    ///
    /// <para>Menu: <c>Hidden Harbours ▸ Art ▸ Import (after a new drop) ▸ Build Acadian Tree
    /// Prefabs</c>, and it is also run by
    /// <c>Build Decor Prefabs</c> so the one-shot toolkit command covers it.</para>
    /// </summary>
    public static class AcadianTreePrefabBuilder
    {
        [MenuItem("Hidden Harbours/Art/Import (after a new drop)/Build Acadian Tree Prefabs", priority = 232)]
        public static void BuildMenu()
        {
            int built = BuildAll(AcadianTreeCatalog.PrefabRoot);
            if (built == 0)
            {
                Debug.LogWarning(
                    "[AcadianTreePrefabBuilder] Built nothing. Are the sheets baked and sliced? " +
                    "Hidden Harbours ▸ Art ▸ Bake Acadian Trees, then ▸ Import (after a new drop) ▸ Slice Acadian Tree Sheets.");
                return;
            }

            var placements = AcadianTreeCatalog.Scan();
            Debug.Log(
                $"[AcadianTreePrefabBuilder] Built {built} tree prefab(s) under " +
                $"{AcadianTreeCatalog.PrefabRoot}. {AcadianTreeCatalog.Summary(placements)} " +
                "Drag one into a scene and it plants at its TRUNK (not the bottom of its cell) and " +
                "sways off the same wind as the grass and water. To scatter a stand, use " +
                "Hidden Harbours ▸ Tools ▸ Tree Paint Tool; to compare all ten side by side, " +
                "Hidden Harbours ▸ Dev ▸ Place Acadian Tree Lineup.");
        }

        /// <summary>
        /// Build (or rebuild in place) one prefab per placeable tree into <paramref name="folder"/>.
        /// Returns how many were built. <c>public</c> and folder-parameterised for the same reason
        /// <c>DecorPrefabBuilder.BuildFlowerPrefabs</c> is: the tests build into a scratch folder
        /// rather than churning the owner's real, untracked <c>Prefabs/Decor/</c> tree.
        /// </summary>
        public static int BuildAll(string folder)
        {
            var placements = AcadianTreeCatalog.Scan();
            if (placements.Count == 0)
            {
                Debug.LogWarning(
                    $"[AcadianTreePrefabBuilder] No trees in {TreeKitCatalog.ContractPath} — nothing to build.");
                return 0;
            }

            var material = AcadianTreeCatalog.LoadMaterial();
            if (material == null)
                Debug.LogWarning(
                    $"[AcadianTreePrefabBuilder] canopy material {AcadianTreeCatalog.MaterialPath} " +
                    "missing — trees built WITHOUT wind-sway (open Unity so it imports the TreeWind " +
                    "shader + material, then re-run).");

            EnsureFolder(folder);

            int built = 0;
            foreach (var placement in placements)
            {
                // Variant 0 of the ALBEDO sheet. The other variants are different individual trees
                // of the same species — a prefab can only hold one, and mixing them is the paint
                // tool's job (the same division the flowers use for their bloom rows).
                var sprite = AcadianTreeCatalog.LoadVariant(placement, 0);
                if (sprite == null)
                {
                    Debug.LogWarning(
                        $"[AcadianTreePrefabBuilder] '{placement.Stem}' has no variant-0 sprite — " +
                        "skipped. Is it sliced? (Hidden Harbours ▸ Art ▸ Import (after a new drop) ▸ " +
                        "Slice Acadian Tree Sheets.) " +
                        "Note the sheets import spriteMode Multiple, so a plain " +
                        "LoadAssetAtPath<Sprite> on them returns null.");
                    continue;
                }

                var go = new GameObject(placement.Stem);
                try
                {
                    AcadianTreeCatalog.Configure(go, placement, sprite, material);
                    SavePrefabReplacing(go, $"{folder}/{placement.Stem}.prefab");
                    built++;
                }
                finally { Object.DestroyImmediate(go); }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return built;
        }

        // =================================================================================
        // the lineup — what the owner actually looks at
        // =================================================================================

        /// <summary>The lineup's root object, so re-running replaces it instead of stacking.</summary>
        public const string LineupRootName = "AcadianTreeLineup";

        /// <summary>A stand-in for a person, for scale. The player is not a placeable sprite yet, and
        /// "is this tree the right size" is unanswerable without something human-sized beside it.</summary>
        public const float ReferenceHeightMetres = 1.7f;

        /// <summary>Clear metres between one tree's cell and the next, so crowns never touch and each
        /// silhouette reads on its own.</summary>
        public const float LineupGapMetres = 0.6f;

        /// <summary>
        /// Stand all ten species in a row in the OPEN scene, tallest-drawing to shortest left to
        /// right is NOT what this does — it keeps the rig's own order (conifers then hardwoods) so
        /// like forms sit together and the comparison is about silhouette rather than height. Each
        /// tree is named for its species and its true height in metres, and a plain
        /// <see cref="ReferenceHeightMetres"/> m bar stands at the left as a human-scale reference.
        ///
        /// <para>This is the fastest way to answer the three questions the kit is waiting on — is the
        /// scale right, do the species read differently, do they sit properly on the ground — and it
        /// is re-runnable: run it again after a re-bake and the row is rebuilt from the new
        /// prefabs.</para>
        ///
        /// <para>Menu: <c>Hidden Harbours ▸ Dev ▸ Place Acadian Tree Lineup (all 10 species)</c>.</para>
        /// </summary>
        [MenuItem("Hidden Harbours/Dev/Place Acadian Tree Lineup (all 10 species)", priority = 60)]
        public static void PlaceLineupMenu()
        {
            int placed = PlaceLineup(SceneViewPivot(), AcadianTreeCatalog.PrefabRoot);
            if (placed == 0)
                Debug.LogWarning(
                    "[AcadianTreePrefabBuilder] Placed no trees. Bake and slice the sheets first " +
                    "(Hidden Harbours ▸ Art ▸ Bake Acadian Trees, then ▸ Import (after a new drop) ▸ Slice Acadian Tree Sheets).");
            else
                Debug.Log(
                    $"[AcadianTreePrefabBuilder] Placed {placed} tree(s) in a row under " +
                    $"'{LineupRootName}' in the open scene, with a {ReferenceHeightMetres} m " +
                    "human-scale reference bar at the left. Drag the root onto grass to judge them " +
                    "against the terrain; press Play to see the canopies move.");
        }

        /// <summary>
        /// Build the prefabs, then instantiate one of each in a row starting at
        /// <paramref name="origin"/>. Returns how many trees were placed. Everything is registered
        /// with Undo as one step. <paramref name="folder"/> is parameterised so the tests can drive
        /// the lineup out of a scratch folder rather than churning the owner's real, untracked
        /// <c>Prefabs/Decor/</c> tree.
        /// </summary>
        public static int PlaceLineup(Vector3 origin, string folder)
        {
            var placements = AcadianTreeCatalog.Scan();
            if (placements.Count == 0) return 0;

            // The lineup places PREFAB INSTANCES, not loose objects, so a later re-bake + rebuild
            // refreshes the row in place instead of leaving ten stale copies behind.
            BuildAll(folder);

            int ppu = PixelsPerUnit();
            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Place Acadian tree lineup");
            int group = Undo.GetCurrentGroup();

            var existing = FindLineupRoot();
            if (existing != null) Undo.DestroyObjectImmediate(existing);

            var root = new GameObject(LineupRootName);
            Undo.RegisterCreatedObjectUndo(root, "Place Acadian tree lineup");
            root.transform.position = origin;

            float x = 0f;
            x += PlaceScaleReference(root, x);

            int placed = 0;
            foreach (var placement in placements)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                    $"{folder}/{placement.Stem}.prefab");
                if (prefab == null) continue;

                float halfWidth = AcadianTreeCatalog.DrawnWidthMetres(placement.Entry, ppu) * 0.5f;
                x += halfWidth;

                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root.scene);
                Undo.RegisterCreatedObjectUndo(instance, "Place Acadian tree lineup");
                instance.transform.SetParent(root.transform, false);
                instance.transform.localPosition = new Vector3(x, 0f, 0f);
                // Named for what the owner is judging: which species, and how tall it is MEANT to be.
                instance.name = $"{placement.Entry.name} ({placement.Entry.metres:0.#} m)";

                x += halfWidth + LineupGapMetres;
                placed++;
            }

            EditorSceneManager.MarkSceneDirty(root.scene);
            Undo.CollapseUndoOperations(group);

            Selection.activeGameObject = root;
            SceneView.lastActiveSceneView?.FrameSelected();
            return placed;
        }

        /// <summary>
        /// A plain dark bar exactly <see cref="ReferenceHeightMetres"/> m tall at the head of the
        /// row. Greybox on purpose: the question is "how big is this tree next to a person", and a
        /// bar of known height answers it without pretending to be character art. Returns the
        /// horizontal space it consumed (0 if the shared square sprite is unavailable, in which case
        /// the lineup simply has no reference).
        /// </summary>
        private static float PlaceScaleReference(GameObject root, float x)
        {
            const float BarWidth = 0.45f;

            var square = TileAssetBuilder.LoadSpriteAny("Assets/_Project/Art/Sprites/Square.png");
            if (square == null) return 0f;

            var go = new GameObject($"SCALE REFERENCE ({ReferenceHeightMetres} m person)");
            Undo.RegisterCreatedObjectUndo(go, "Place Acadian tree lineup");
            go.transform.SetParent(root.transform, false);
            // The square sprite is CENTRE-pivoted, so lift it half its height to stand it on y = 0 —
            // the same ground line the trees' trunk feet sit on.
            go.transform.localPosition = new Vector3(x + BarWidth * 0.5f, ReferenceHeightMetres * 0.5f, 0f);
            go.transform.localScale = new Vector3(BarWidth, ReferenceHeightMetres, 1f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = square;
            sr.color = new Color(0.16f, 0.15f, 0.14f, 0.85f);
            sr.sortingOrder = AcadianTreeCatalog.SortingOrder;

            return BarWidth + LineupGapMetres;
        }

        private static GameObject FindLineupRoot()
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            foreach (var go in scene.GetRootGameObjects())
                if (go.name == LineupRootName) return go;
            return null;
        }

        /// <summary>Where the owner is currently looking, so the row lands on screen rather than at
        /// the world origin on the other side of a 760 m region. Falls back to the origin headless.</summary>
        private static Vector3 SceneViewPivot()
        {
            var view = SceneView.lastActiveSceneView;
            if (view == null) return Vector3.zero;
            Vector3 p = view.pivot;
            return new Vector3(p.x, p.y, 0f);
        }

        /// <summary>PPU from the bake's own contract — the number the sheets were baked at, not the
        /// import default, so the two cannot disagree silently.</summary>
        private static int PixelsPerUnit()
        {
            TreeKitCatalog.Contract contract = TreeKitCatalog.Load();
            return contract != null && contract.ppu > 0 ? contract.ppu : 32;
        }

        // =================================================================================

        private static void SavePrefabReplacing(GameObject go, string prefabPath)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null)
                AssetDatabase.DeleteAsset(prefabPath);
            PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            string parent = Path.GetDirectoryName(folder)?.Replace('\\', '/');
            string leaf = Path.GetFileName(folder);
            if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(leaf)) return;
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
#endif
