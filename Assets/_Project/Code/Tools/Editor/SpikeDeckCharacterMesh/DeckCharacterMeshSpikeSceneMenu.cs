using HiddenHarbours.Art;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.SpikeDeckCharacterMesh.Editor
{
    /// <summary>
    /// ⚠️ SPIKE (deck-character-mesh, draft ADR 0024). Attaches / removes the demo rig on a
    /// MESH-hull boat in the open scene. Nothing here is wired into the shipping player — the rig
    /// exists only where this menu put it, and "Remove" deletes every trace.
    ///
    /// <para><b>Demo recipe (the owner's script):</b> Build St Peters → Play → board the lobster
    /// boat, sail clear, leave the helm → run "Attach demo rig" → press O (displaced sea ON),
    /// U (hold, forces the slow weathervane), J (A/B mesh ↔ ratcheting sprite), H (idle ↔ fishing
    /// hold stance).</para>
    /// </summary>
    public static class DeckCharacterMeshSpikeSceneMenu
    {
        const string RigName = "DeckCharacterMeshSpikeRig (SPIKE)";
        const string FisherVisualPath = "Assets/_Project/Data/Characters/FisherIso.asset";

        [MenuItem(DeckCharacterMeshSpikeBaker.MenuRoot + "/Attach demo rig to a mesh-hull boat (open scene)",
                  priority = 30)]
        public static void Attach()
        {
            var def = AssetDatabase.LoadAssetAtPath<DeckCharacterMeshSpikeDef>(
                DeckCharacterMeshSpikeBaker.AssetPath);
            if (def == null || !def.IsUsable())
            {
                Debug.LogError("[deck-char SPIKE] No baked def — run 'Bake Fisher pose meshes' first " +
                               $"(expected at {DeckCharacterMeshSpikeBaker.AssetPath}).");
                return;
            }

            // A boat = a MeshHullDriver whose renderer is configured. FindObjectsByType because
            // this is a dev menu, not a hot path.
            MeshHullDriver target = null;
            foreach (var driver in Object.FindObjectsByType<MeshHullDriver>(FindObjectsSortMode.None))
            {
                var r = driver.GetComponentInChildren<IsoFacetHullRenderer>(true);
                if (r != null && r.IsConfigured) { target = driver; break; }
            }
            if (target == null)
            {
                Debug.LogError("[deck-char SPIKE] No mesh-hull boat in the open scene. Build St Peters " +
                               "and sail the lobster boat (or dev-cycle to a mesh hull) first.");
                return;
            }

            var existing = target.transform.Find(RigName);
            if (existing != null)
            {
                Debug.Log("[deck-char SPIKE] Demo rig already attached; selecting it.");
                Selection.activeGameObject = existing.gameObject;
                return;
            }

            var go = new GameObject(RigName);
            go.transform.SetParent(target.transform, false);
            var rig = go.AddComponent<DeckCharacterMeshSpikeRig>();

            var so = new SerializedObject(rig);
            so.FindProperty("_def").objectReferenceValue = def;
            so.FindProperty("_spriteVisual").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<CharacterVisualDef>(FisherVisualPath);
            so.ApplyModifiedPropertiesWithoutUndo();

            Selection.activeGameObject = go;
            Debug.Log($"[deck-char SPIKE] Attached to '{target.name}'. Keys: J = mesh↔sprite A/B, " +
                      "H = idle↔hold, U (hold) = force weathervane, O = displaced sea. " +
                      "(Enter Play mode if not already — the rig builds its renderers in Start.)");
        }

        [MenuItem(DeckCharacterMeshSpikeBaker.MenuRoot + "/Remove demo rig", priority = 31)]
        public static void Remove()
        {
            int removed = 0;
            foreach (var rig in Object.FindObjectsByType<DeckCharacterMeshSpikeRig>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                Object.DestroyImmediate(rig.gameObject);
                removed++;
            }
            Debug.Log($"[deck-char SPIKE] Removed {removed} demo rig(s).");
        }
    }
}
