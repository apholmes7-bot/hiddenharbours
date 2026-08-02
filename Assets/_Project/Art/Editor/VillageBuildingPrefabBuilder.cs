#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HiddenHarbours.Art.Editor
{
    /// <summary>
    /// Turns the baked village building sheets into drag-and-drop <b>prefabs</b> under
    /// <see cref="VillageBuildingCatalog.PrefabRoot"/> — one per building, showing the facing whose door
    /// faces the camera. The building half of what <see cref="AcadianTreePrefabBuilder"/> does for trees,
    /// and deliberately the same shape: <b>the BUILDER is committed, its output is not.</b> The owner (or
    /// a region builder) runs the menu item; nothing generated is checked in.
    ///
    /// <para>Everything a building needs to sit right — the ground-centre pivot, honest metric scale,
    /// which facing shows the door — comes from the bake's own <c>Buildings.json</c> via
    /// <see cref="VillageBuildingCatalog"/>. Re-run after a re-bake and every prefab refreshes IN PLACE,
    /// so scene instances keep their prefab link and nobody re-places a village.</para>
    ///
    /// <para>Menu: <c>Hidden Harbours ▸ Art ▸ Import (after a new drop) ▸ Build Village Building
    /// Prefabs</c>.</para>
    /// </summary>
    public static class VillageBuildingPrefabBuilder
    {
        [MenuItem("Hidden Harbours/Art/Import (after a new drop)/Build Village Building Prefabs", priority = 233)]
        public static void BuildMenu()
        {
            int built = BuildAll(VillageBuildingCatalog.PrefabRoot);
            if (built == 0)
            {
                Debug.LogWarning(
                    "[VillageBuildingPrefabBuilder] Built nothing. Are the sheets baked and sliced? " +
                    "Hidden Harbours ▸ Art ▸ Bake Village Buildings, then ▸ Slice Village Building " +
                    "Sheets.");
                return;
            }

            var placements = VillageBuildingCatalog.Scan();
            Debug.Log(
                $"[VillageBuildingPrefabBuilder] Built {built} building prefab(s) under " +
                $"{VillageBuildingCatalog.PrefabRoot}. {VillageBuildingCatalog.Summary(placements)} " +
                "Drag one into a scene and it stands on its GROUND LINE, not the bottom of its cell. " +
                "To turn one, call VillageBuildingCatalog.SetFacing — never rotate the transform, the " +
                "art is baked at a fixed camera. To see the whole set, Hidden Harbours ▸ Dev ▸ Place " +
                "Village Lineup; to check a building does not shift as it turns, ▸ Place Village " +
                "Building Turntable.");
        }

        /// <summary>
        /// Build (or rebuild in place) one prefab per placeable building into <paramref name="folder"/>.
        /// Returns how many were built.
        ///
        /// <para><c>public</c> and folder-parameterised for the same reason
        /// <see cref="AcadianTreePrefabBuilder.BuildAll"/> is: the tests build into a scratch folder
        /// rather than churning the owner's real, untracked <c>Prefabs/Decor/</c> tree.</para>
        ///
        /// <para>Each prefab shows <see cref="VillageBuildingCatalog.Placement.FrontFacing"/> — the
        /// facing whose door faces the camera, derived from the baked door anchors. Cell 0 is the
        /// building's BACK (both rigs put the door on the <c>+Y</c> gable, and <c>+Y</c> projects away
        /// from the camera), so a prefab at facing 0 would show every building's back wall and look like
        /// the art was missing its door.</para>
        /// </summary>
        public static int BuildAll(string folder)
        {
            var placements = VillageBuildingCatalog.Scan();
            if (placements.Count == 0)
            {
                Debug.LogWarning($"[VillageBuildingPrefabBuilder] No buildings in " +
                                 $"{VillageBuildingKit.ContractPath} — nothing to build.");
                return 0;
            }

            EnsureFolder(folder);

            int built = 0;
            foreach (var placement in placements)
            {
                int facing = placement.FrontFacing;
                var sprite = VillageBuildingCatalog.LoadFacing(placement, facing);
                if (sprite == null)
                {
                    Debug.LogWarning(
                        $"[VillageBuildingPrefabBuilder] '{placement.Stem}' has no facing-{facing} " +
                        "sprite — skipped. Is it sliced? (Hidden Harbours ▸ Art ▸ Import (after a new " +
                        "drop) ▸ Slice Village Building Sheets.) Note the sheets import spriteMode " +
                        "Multiple, so a plain " +
                        "LoadAssetAtPath<Sprite> on them returns null.");
                    continue;
                }

                var go = new GameObject(placement.Stem);
                try
                {
                    VillageBuildingCatalog.Configure(go, placement, sprite);
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

        public const string LineupRootName = "VillageBuildingLineup";
        public const string TurntableRootName = "VillageBuildingTurntable";

        /// <summary>A stand-in for a person, for scale. "Is this school the right size" is unanswerable
        /// without something human-sized beside it.</summary>
        public const float ReferenceHeightMetres = 1.7f;

        /// <summary>Clear metres between one building's cell and the next, so roofs never touch and each
        /// silhouette reads on its own.</summary>
        public const float LineupGapMetres = 1.5f;

        /// <summary>
        /// Stand the whole M1 village in a row in the OPEN scene, in the kit's own order (school, store,
        /// then the three houses), each named for what it is and how big its footprint is, with a
        /// <see cref="ReferenceHeightMetres"/> m human-scale bar at the left.
        ///
        /// <para>This is the fastest way to answer the questions the kit is waiting on — is the scale
        /// right, does the school read as a school, do the three houses read as three different
        /// houses — and it is re-runnable: run it again after a re-bake and the row rebuilds from the new
        /// prefabs.</para>
        /// </summary>
        [MenuItem("Hidden Harbours/Dev/Place Village Lineup (M1 building set)", priority = 61)]
        public static void PlaceLineupMenu()
        {
            int placed = PlaceLineup(SceneViewPivot(), VillageBuildingCatalog.PrefabRoot);
            if (placed == 0)
                Debug.LogWarning(
                    "[VillageBuildingPrefabBuilder] Placed no buildings. Bake and slice the sheets first " +
                    "(Hidden Harbours ▸ Art ▸ Bake Village Buildings, then ▸ Slice Village Building " +
                    "Sheets).");
            else
                Debug.Log($"[VillageBuildingPrefabBuilder] Placed {placed} building(s) in a row under " +
                          $"'{LineupRootName}', with a {ReferenceHeightMetres} m human-scale reference " +
                          "bar at the left. Every one stands on y = 0 through its GROUND LINE.");
        }

        /// <summary>
        /// Build the prefabs, then instantiate one of each in a row starting at
        /// <paramref name="origin"/>. Returns how many were placed. Everything is registered with Undo as
        /// one step. <paramref name="folder"/> is parameterised so the tests can drive the lineup out of
        /// a scratch folder.
        /// </summary>
        public static int PlaceLineup(Vector3 origin, string folder)
        {
            var placements = VillageBuildingCatalog.Scan();
            if (placements.Count == 0) return 0;

            // The lineup places PREFAB INSTANCES, not loose objects, so a later re-bake + rebuild
            // refreshes the row in place instead of leaving stale copies behind.
            BuildAll(folder);

            int ppu = VillageBuildingCatalog.PixelsPerUnit();
            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Place village building lineup");
            int group = Undo.GetCurrentGroup();

            var existing = FindRoot(LineupRootName);
            if (existing != null) Undo.DestroyObjectImmediate(existing);

            var root = new GameObject(LineupRootName);
            Undo.RegisterCreatedObjectUndo(root, "Place village building lineup");
            root.transform.position = origin;

            float x = PlaceScaleReference(root, 0f);

            int placed = 0;
            foreach (var placement in placements)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                    $"{folder}/{placement.Stem}.prefab");
                if (prefab == null) continue;

                float halfWidth = VillageBuildingCatalog.DrawnWidthMetres(placement.Entry, ppu) * 0.5f;
                x += halfWidth;

                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root.scene);
                Undo.RegisterCreatedObjectUndo(instance, "Place village building lineup");
                instance.transform.SetParent(root.transform, false);
                instance.transform.localPosition = new Vector3(x, 0f, 0f);
                // Named for what the owner is judging: which building, and how big its footprint is.
                instance.name = $"{placement.Entry.label} " +
                                $"({placement.Entry.footprintWidthMetres:0.#}×" +
                                $"{placement.Entry.footprintLengthMetres:0.#} m)";

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
        /// Stand ONE building at all eight facings in a row — the owner-facing proof of the property the
        /// whole crop-packing bake is built around: <b>the pivot is identical across facings, so a
        /// building does not shift as it turns.</b>
        ///
        /// <para>The union crop exists for exactly this (one cell size, one pivot for all eight), and its
        /// failure mode is silent — a per-facing crop would look fine in the sheet and make the building
        /// jump when re-faced. Eight of them on one ground line, at equal spacing, is what makes a wrong
        /// pivot visible in one glance.</para>
        /// </summary>
        [MenuItem("Hidden Harbours/Dev/Place Village Building Turntable (8 facings)", priority = 62)]
        public static void PlaceTurntableMenu()
        {
            var placements = VillageBuildingCatalog.Scan();
            if (placements.Count == 0)
            {
                Debug.LogWarning("[VillageBuildingPrefabBuilder] Nothing baked — bake and slice first.");
                return;
            }

            // The store: the biggest of the dialled builds, with a porch and a bay, so any facing-to-
            // facing shift shows up against its own asymmetry rather than a symmetric box.
            var chosen = VillageBuildingCatalog.Find("generalStore");
            if (!chosen.IsValid) chosen = placements[0];

            int placed = PlaceTurntable(chosen, SceneViewPivot());
            Debug.Log($"[VillageBuildingPrefabBuilder] Placed {placed} facing(s) of " +
                      $"'{chosen.Entry.label}' under '{TurntableRootName}'. Every one shares one cell and " +
                      "one pivot, so the ground line should be dead straight and the building should not " +
                      "drift up or down the row. If it does, the union crop or the pivot arithmetic is " +
                      "wrong — not the placement code.");
        }

        /// <summary>Place every facing of one building in a row at <paramref name="origin"/>. Loose
        /// objects, not prefab instances: this is a diagnostic, and each facing is a different sprite of
        /// the same prefab.</summary>
        public static int PlaceTurntable(VillageBuildingCatalog.Placement placement, Vector3 origin)
        {
            if (!placement.IsValid) return 0;

            int ppu = VillageBuildingCatalog.PixelsPerUnit();
            float step = VillageBuildingCatalog.DrawnWidthMetres(placement.Entry, ppu) + LineupGapMetres;

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Place village building turntable");
            int group = Undo.GetCurrentGroup();

            var existing = FindRoot(TurntableRootName);
            if (existing != null) Undo.DestroyObjectImmediate(existing);

            var root = new GameObject($"{TurntableRootName} ({placement.Entry.label})");
            root.name = TurntableRootName;
            Undo.RegisterCreatedObjectUndo(root, "Place village building turntable");
            root.transform.position = origin;

            int placed = 0;
            for (int facing = 0; facing < placement.Entry.facings; facing++)
            {
                var sprite = VillageBuildingCatalog.LoadFacing(placement, facing);
                if (sprite == null) continue;

                var go = new GameObject($"{placement.Entry.label} d{facing} (+{facing * 45}°)" +
                                        (facing == placement.FrontFacing ? " — DOOR" : ""));
                Undo.RegisterCreatedObjectUndo(go, "Place village building turntable");
                go.transform.SetParent(root.transform, false);
                go.transform.localPosition = new Vector3(facing * step, 0f, 0f);
                VillageBuildingCatalog.Configure(go, placement, sprite);
                placed++;
            }

            EditorSceneManager.MarkSceneDirty(root.scene);
            Undo.CollapseUndoOperations(group);

            Selection.activeGameObject = root;
            SceneView.lastActiveSceneView?.FrameSelected();
            return placed;
        }

        /// <summary>
        /// A plain dark bar exactly <see cref="ReferenceHeightMetres"/> m tall at the head of the row.
        /// Greybox on purpose: the question is "how big is this building next to a person", and a bar of
        /// known height answers it without pretending to be character art. Returns the horizontal space
        /// it consumed (0 if the shared square sprite is unavailable).
        /// </summary>
        static float PlaceScaleReference(GameObject root, float x)
        {
            const float BarWidth = 0.45f;

            var square = TileAssetBuilder.LoadSpriteAny("Assets/_Project/Art/Sprites/Square.png");
            if (square == null) return 0f;

            var go = new GameObject($"SCALE REFERENCE ({ReferenceHeightMetres} m person)");
            Undo.RegisterCreatedObjectUndo(go, "Place village building lineup");
            go.transform.SetParent(root.transform, false);
            // The square sprite is CENTRE-pivoted, so lift it half its height to stand it on y = 0 —
            // the same ground line the buildings' ground centres sit on.
            go.transform.localPosition = new Vector3(x + BarWidth * 0.5f, ReferenceHeightMetres * 0.5f, 0f);
            go.transform.localScale = new Vector3(BarWidth, ReferenceHeightMetres, 1f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = square;
            sr.color = new Color(0.16f, 0.15f, 0.14f, 0.85f);
            sr.sortingOrder = VillageBuildingCatalog.SortingOrder + 1;

            return x + BarWidth + LineupGapMetres;
        }

        static GameObject FindRoot(string name)
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            foreach (var go in scene.GetRootGameObjects())
                if (go.name == name) return go;
            return null;
        }

        /// <summary>Where the owner is currently looking, so the row lands on screen rather than at the
        /// world origin on the other side of a 760 m region. Falls back to the origin headless.</summary>
        static Vector3 SceneViewPivot()
        {
            var view = SceneView.lastActiveSceneView;
            if (view == null) return Vector3.zero;
            Vector3 p = view.pivot;
            return new Vector3(p.x, p.y, 0f);
        }

        // =================================================================================

        static void SavePrefabReplacing(GameObject go, string prefabPath)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null)
                AssetDatabase.DeleteAsset(prefabPath);
            PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
        }

        static void EnsureFolder(string folder)
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
