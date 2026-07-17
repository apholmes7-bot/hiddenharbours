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
    /// Menu: <b>Hidden Harbours ▸ Build Boat-Rotation Test</b>.
    /// </summary>
    public static class BoatRotationTestBuilder
    {
        private const string MenuPath = "Hidden Harbours/Build Boat-Rotation Test";
        private const string RootName = "BoatRotationTest";

        // The four facing PNGs, in CLOCKWISE order from the zero heading (North): N, E, S, W.
        private static readonly string[] FacingPaths =
        {
            "Assets/_Project/Art/Boats/FishingBoat_N.png",
            "Assets/_Project/Art/Boats/FishingBoat_E.png",
            "Assets/_Project/Art/Boats/FishingBoat_S.png",
            "Assets/_Project/Art/Boats/FishingBoat_W.png",
        };

        [MenuItem(MenuPath)]
        public static void Build()
        {
            // Load the four facings up-front so we fail loudly (with guidance) if the art isn't imported yet.
            var facings = new Sprite[FacingPaths.Length];
            for (int i = 0; i < FacingPaths.Length; i++)
            {
                facings[i] = LoadSpriteAny(FacingPaths[i]);
                if (facings[i] == null)
                {
                    EditorUtility.DisplayDialog(
                        "Boat-Rotation Test",
                        $"Couldn't load the facing sprite:\n{FacingPaths[i]}\n\n" +
                        "Make sure the FishingBoat_N/E/S/W PNGs are imported (open Unity so it imports them), " +
                        "then run the menu item again.",
                        "OK");
                    return;
                }
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
            //     This harness carries its own 4 facings and its own in-memory hull, so it adapts them into
            //     a runtime binding rather than pointing at a BoatVisualDef asset. It keeps its historic
            //     child name ("Sprite") and opts out of the wave motion + oars the player's boat wants —
            //     this is an A/B rig for comparing snap vs smooth, nothing more. ---
            var skin = BoatVisualDef.CreateRuntime(facings, sortingOrder: 10);
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

        /// <summary>
        /// Load a Sprite at a path whether the texture imported as a single Sprite (our Single-mode metas) OR
        /// as a Multiple-mode sheet (where <c>LoadAssetAtPath&lt;Sprite&gt;</c> can return null and the sprite
        /// is a sub-asset). Mirrors the greybox builder's robust sprite lookup (memory: imported art is often
        /// spriteMode Multiple).
        /// </summary>
        private static Sprite LoadSpriteAny(string path)
        {
            var direct = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (direct != null) return direct;
            foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(path))
                if (obj is Sprite s) return s;
            return null;
        }
    }
}
#endif
