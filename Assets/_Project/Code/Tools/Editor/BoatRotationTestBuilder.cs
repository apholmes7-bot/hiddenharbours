#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using HiddenHarbours.Boats;

namespace HiddenHarbours.Tools.Editor
{
    /// <summary>
    /// REVERSIBLE PROTOTYPE harness for the owner's "how should boats rotate?" decision. ONE menu item spawns
    /// a self-contained test boat into the current scene so a non-dev can press Play and FEEL two options
    /// back-to-back, WITHOUT wiring any Inspector:
    ///
    ///   • drivable with the existing helm (W/S throttle, A/D steer — the spawned boat uses Engine propulsion);
    ///   • a slow optional auto-yaw (on by default) so it turns hands-free the moment you hit Play;
    ///   • press <b>T</b> to toggle SnapDirectional (swap the 4 hand-drawn N/E/S/W facings, picture stays
    ///     screen-aligned) vs SmoothRotateSingle (one sprite rotates with the hull — today's behaviour);
    ///   • press <b>Y</b> to toggle the auto-yaw.
    ///
    /// It is ADDITIVE and surgical: it does NOT touch GreyboxBuilder / StPetersBuilder, the real Dory/Punt,
    /// or any committed Data asset. The test hull is an in-memory <see cref="BoatHullDef"/> (no asset is
    /// written); delete the spawned "BoatRotationTest" object to fully revert.
    ///
    /// Menu: <b>Hidden Harbours ▸ Dev ▸ Build Boat-Rotation Test</b>.
    /// </summary>
    public static class BoatRotationTestBuilder
    {
        private const string MenuPath = "Hidden Harbours/Dev/Build Boat-Rotation Test";
        private const string RootName = "BoatRotationTest";

        // The compass this harness wears. It used to carry four loose per-heading PNGs of its own; that
        // art was the owner's hand-drawn plan-view compass, retired 2026-09-06 (Core RetiredContentIds).
        // Pointing at a SHIPPED visual is the better shape anyway: the handedness and bake elevation come
        // with the sprites instead of being re-declared here, which is the whole point of
        // BoatVisualDef.CreateRuntimeFrom — a compass read with the wrong handedness looks correct at
        // north and south and is 180° out at east and west.
        private const string CompassVisualPath = "Assets/_Project/Data/Boats/Visuals/DoryIso.asset";

        [MenuItem(MenuPath, priority = 44)]
        public static void Build()
        {
            // Load the compass up-front so we fail loudly (with guidance) if the art isn't imported yet.
            var compass = AssetDatabase.LoadAssetAtPath<BoatVisualDef>(CompassVisualPath);
            if (compass == null || !compass.HasFullCompass())
            {
                EditorUtility.DisplayDialog(
                    "Boat-Rotation Test",
                    $"Couldn't load a complete hull compass from:\n{CompassVisualPath}\n\n" +
                    "Run Hidden Harbours \u25b8 Dev \u25b8 Build Boat Visual Library first (it slices the iso " +
                    "sheets and wires the facings), then run this menu item again.",
                    "OK");
                return;
            }

            // Remove a prior test rig so re-running is idempotent (no duplicate boats stacking up).
            var existing = GameObject.Find(RootName);
            if (existing != null) Object.DestroyImmediate(existing);

            // --- Root: the boat body (the transform that turns; bow = transform.up, as BoatController uses). ---
            var root = new GameObject(RootName);
            root.transform.position = Vector3.zero;

            // In-memory Engine hull so W/S/A/D steer naturally — NOT saved as an asset (keeps the test reversible).
            var hull = ScriptableObject.CreateInstance<BoatHullDef>();
            hull.Id = "boat.rotation_test";
            hull.DisplayName = "Rotation Test Boat";
            hull.Propulsion = PropulsionType.Engine;
            hull.LengthMeters = 4f;
            hull.DraughtMeters = 0.3f;
            hull.MassKg = 400f;
            hull.EnginePower = 1200f;
            hull.RudderAuthority = 600f;
            hull.hideFlags = HideFlags.DontSave;   // lives only for this session; never serialized into the scene

            // BoatController + DevBoatInput give the owner the real helm feel (the [RequireComponent]s add the
            // Rigidbody2D, CapsuleCollider2D and BoatMooring automatically; the mooring stays dormant/Stowed).
            var controller = root.AddComponent<BoatController>();
            controller.SetHull(hull);
            root.AddComponent<DevBoatInput>();

            // --- The picture: the SHARED skin rig (BoatHullSkinner), not a third hand-rolled copy of it.
            //     The child renderer is what SnapDirectional counter-rotates to stay screen-aligned, and
            //     what SmoothRotateSingle leaves at local identity so it inherits the body's yaw.
            //
            //     This harness has its own in-memory hull, so it reduces a shipped visual to a bare compass
            //     rather than pointing at the asset itself — DECOR TIER, so the source hull's rock grid,
            //     oars and outboard stay behind while her handedness and bake elevation come along. It
            //     keeps its historic child name ("Sprite") and opts out of the wave motion + oars the
            //     player's boat wants — this is an A/B rig for comparing snap vs smooth, nothing more. ---
            var skin = BoatVisualDef.CreateRuntimeFrom(compass, sortingOrder: 10);
            var directional = BoatHullSkinner.Apply(
                root, skin, controller,
                new BoatHullSkinner.Options { ChildName = "Sprite", SkipWaveMotion = true, SkipOars = true })
                .Directional;

            // --- The dev rig (mode toggle + auto-yaw), wired from code. ---

            var rig = root.AddComponent<BoatRotationTestRig>();
            // Wire the rig's DirectionalBoatSprite reference via SerializedObject (the field is private).
            var so = new SerializedObject(rig);
            var prop = so.FindProperty("_directional");
            if (prop != null) { prop.objectReferenceValue = directional; so.ApplyModifiedPropertiesWithoutUndo(); }

            // Select it + mark the scene dirty so the spawn isn't silently lost.
            Selection.activeGameObject = root;
            EditorSceneManager.MarkSceneDirty(root.scene);

            Debug.Log(
                "[BoatRotationTest] Spawned 'BoatRotationTest'. Press Play, then:\n" +
                "  • it auto-yaws slowly (watch it turn) — press Y to stop/start that;\n" +
                "  • drive it with W/S (throttle) + A/D (steer);\n" +
                "  • press T to toggle Snap (swap N/E/S/W facings, picture stays upright) vs " +
                "Smooth (one sprite rotates with the hull).\n" +
                "Delete the 'BoatRotationTest' object to fully revert.");
        }
    }
}
#endif
