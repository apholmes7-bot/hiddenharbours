using System.Linq;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Art;
using HiddenHarbours.Player;

namespace HiddenHarbours.App.Editor
{
    /// <summary>
    /// <b>Hidden Harbours ▸ Dev ▸ Spawn Fuel Container At Player</b> — drops a carriable fuel container at
    /// the fisher's feet so the owner can feel the carry loop without a container being placed in any
    /// shipped region yet.
    ///
    /// <para><b>Why a MENU ITEM and not a key.</b> The dev key ledger is exhausted — every letter A–Z is
    /// claimed across four binding styles — which is the whole reason the interact verb exists. Menu items
    /// are free; keys are not. So the way in is a menu item, and the loop it lets you reach uses the ONE
    /// interact press the game already had.</para>
    ///
    /// <para><b>Choosing which one.</b> Select a <see cref="FuelContainerDef"/> in the Project window and
    /// this spawns that one; select nothing and it spawns the default 20 L gas can. A selected Def that is
    /// not carriable is refused with the reason, rather than spawning something the verb would then ignore
    /// — a can you cannot pick up is a worse first experience than a clear message.</para>
    ///
    /// <para><b>Editor-only scaffolding.</b> This spawns a plain GameObject carrying the same four
    /// components a placed container would have; it authors no asset, writes no scene and saves nothing.
    /// A container set down is session-local until the PR that actually stands containers on a wharf
    /// brings the world-placed wiring with it.</para>
    /// </summary>
    public static class FuelContainerSpawnMenu
    {
        private const string DefaultContainerPath =
            "Assets/_Project/Data/FuelContainers/jerry_s20_gas.asset";

        private const string ContainerFolder = "Assets/_Project/Data/FuelContainers";

        // Beside the other Dev entries; the exact number only orders the menu.
        [MenuItem("Hidden Harbours/Dev/Spawn Fuel Container At Player", priority = 150)]
        public static void SpawnAtPlayer()
        {
            if (!Application.isPlaying)
            {
                // Play mode is not strictly required to build the object, but the loop cannot be FELT
                // without it, and a container left in an open scene is an accidental scene edit.
                Debug.LogWarning("[FuelContainerSpawn] Enter Play mode first — the carry loop is a " +
                                 "runtime thing, and spawning into an open scene would dirty it.");
                return;
            }

            FuelContainerDef def = ChooseDef();
            if (def == null) return;

            if (!def.Carriable)
            {
                Debug.LogWarning($"[FuelContainerSpawn] '{def.Id}' is a {def.VesselType} — not carriable, " +
                                 "so the verb would never offer it. Select a jug, jerry or nozzle Def " +
                                 "(or nothing, for the default 20 L gas can).");
                return;
            }

            CarryHands hands = Object.FindAnyObjectByType<CarryHands>();
            Transform player = hands != null ? hands.transform : FindPlayer();
            if (player == null)
            {
                Debug.LogWarning("[FuelContainerSpawn] No player in the scene to drop it beside.");
                return;
            }

            if (hands == null)
                Debug.LogWarning("[FuelContainerSpawn] The player has no CarryHands — it can be spawned " +
                                 "but not picked up. Re-run Hidden Harbours ▸ Build Persistent Core: the " +
                                 "hands are builder-wired and this scene's player predates them.");

            GameObject go = Build(def, player.position);
            Debug.Log($"[FuelContainerSpawn] Dropped '{def.DisplayName}' ({def.Id}) at the fisher's feet. " +
                      "Press the interact key (E) to pick it up; press again to set it down.", go);
        }

        /// <summary>
        /// Build a container GameObject — the four components a placed container needs, and nothing else.
        /// Public so a test can build the same object the owner's menu does, rather than a hand-assembled
        /// look-alike that could drift from it.
        /// </summary>
        public static GameObject Build(FuelContainerDef def, Vector3 position)
        {
            var go = new GameObject(def != null ? $"FuelContainer_{def.Id}" : "FuelContainer");
            go.transform.position = position;

            var sr = go.AddComponent<SpriteRenderer>();

            var presenter = go.AddComponent<FuelLevelPresenter>();
            presenter.Container = def;
            // Half full: a level you can actually SEE change, and it proves the fill survives the carry.
            presenter.Fill = 0.5f;

            // Y-sorts itself into the decor band while it stands on the ground; the hands stand this down
            // while it is carried and hand it back on set-down (ADR 0032 — one sorting policy, no
            // authored orders).
            go.AddComponent<YSortSprite>();

            var carriable = go.AddComponent<CarriableFuelContainer>();
            // Readable AND unique: ids must not collide among live registrants, and _spawned counts the
            // cans this session has made. Without this they would fall back to the instance-scoped id,
            // which is unique but tells the owner nothing in a log line.
            carriable.Configure($"{(def != null ? def.Id : "fuelstore.unknown")}.spawn_{++_spawned}");

            if (sr.sprite == null)
                Debug.LogWarning($"[FuelContainerSpawn] '{(def != null ? def.Id : "null")}' has no baked " +
                                 "frame for that fill/facing — the object exists and is interactable but " +
                                 "draws nothing. Re-run the fuel bake if the sheets were re-imported.");

            return go;
        }

        private static int _spawned;

        private static FuelContainerDef ChooseDef()
        {
            var selected = Selection.activeObject as FuelContainerDef;
            if (selected != null) return selected;

            var def = AssetDatabase.LoadAssetAtPath<FuelContainerDef>(DefaultContainerPath);
            if (def != null) return def;

            // The default asset was renamed or the kit was re-baked under different ids — take any
            // carriable one rather than failing, and say which.
            def = AssetDatabase.FindAssets("t:FuelContainerDef", new[] { ContainerFolder })
                               .Select(AssetDatabase.GUIDToAssetPath)
                               .Select(AssetDatabase.LoadAssetAtPath<FuelContainerDef>)
                               .FirstOrDefault(d => d != null && d.Carriable);

            if (def == null)
                Debug.LogWarning($"[FuelContainerSpawn] No carriable FuelContainerDef found under " +
                                 $"'{ContainerFolder}'. Run the fuel bake first.");
            else
                Debug.Log($"[FuelContainerSpawn] '{DefaultContainerPath}' is gone — using '{def.Id}'.");

            return def;
        }

        private static Transform FindPlayer()
        {
            var walk = Object.FindAnyObjectByType<PlayerWalkController>();
            if (walk != null) return walk.transform;
            GameObject go = GameObject.Find("Player");   // the persistent core's name, as StallReach reads it
            return go != null ? go.transform : null;
        }
    }
}
