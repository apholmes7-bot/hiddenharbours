#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using HiddenHarbours.Core;
using HiddenHarbours.Player;
using HiddenHarbours.Tools.RigBaking;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.App.Editor
{
    /// <summary>
    /// <b>The held-prop bake, end to end (ADR 0053, PR 2)</b> — bake every held state into
    /// <see cref="RigPropMeshBaker.HeldPropFolder"/>, then point what a figure holds at its meshes: the
    /// held tools' <see cref="ToolDef.HeldMeshes"/> here, and the fish and the bucket states through
    /// <see cref="CatchItemLibraryBuilder"/>, where the rest of that table is set (a reference set by
    /// hand there would be lost on its next rebuild).
    ///
    /// <para><b>Why here and not beside the bake.</b> The rig-baking assembly does not see the Player
    /// module that owns <see cref="ToolDef"/>; this one sees both, as it does for the other wiring
    /// steps.</para>
    ///
    /// <para><b>Nothing reads the meshes yet.</b> PR 4 presents them on the figure's hand sockets; until
    /// then the sprites draw and these references are inert data. No scene or prefab is written: the
    /// pail reaches its meshes through the library its presenter already holds.</para>
    /// </summary>
    public static class HeldPropBakeCli
    {
        const string MenuLabel = RigMeshGate.MenuRoot + "/Bake ALL held props (rods, spade, fish, buckets)";

        /// <summary>
        /// Which held family each held TOOL wears: the tools' ids against the families' names, both
        /// constants of their own types. A tool wears EVERY state its family bakes, in the rig's order
        /// (the rod all three tiers), so no tier is picked here. Which rod a figure holds is the holder's
        /// to say from its own data, and today two holders disagree: the fight presenter wires the cane
        /// (PersistentCoreBuilder), while the character rig trails the coast rod (rodTrail's rest tier
        /// in characterIsoRig6.hands.js).
        /// </summary>
        static readonly (string ToolId, string Family)[] HeldTools =
        {
            (ToolDef.RodId, HeldPropMeshDef.RodFamily),
            (ToolDef.ShovelId, HeldPropMeshDef.ShovelFamily),
        };

        [MenuItem(MenuLabel, priority = 217)]
        public static void BakeAndWire()
        {
            if (!Run()) Debug.LogError("[held-prop] The held-prop bake did not finish — see the errors above.");
        }

        [MenuItem(MenuLabel, validate = true)]
        static bool BakeAndWireValidate() => RigMeshGate.Enabled;

        /// <summary>Headless entry (-executeMethod). Exits 1 on any failure.</summary>
        public static void BakeAndWireCli()
        {
            try
            {
                if (!Run())
                {
                    Debug.LogError("[held-prop] CLI bake FAILED — see the errors above.");
                    EditorApplication.Exit(1);
                    return;
                }
                Debug.Log("[held-prop] CLI bake OK.");
            }
            catch (Exception e)
            {
                Debug.LogError($"[held-prop] CLI bake FAILED: {e}");
                EditorApplication.Exit(1);
            }
        }

        static bool Run()
        {
            if (RigPropMeshBaker.BakeAllHeld(out List<HeldPropMeshDef> baked) > 0) return false;
            if (!WireTools(baked)) return false;
            if (!CatchItemLibraryBuilder.BuildLibrary(out string summary))
            {
                Debug.LogError($"[held-prop] The catch item library did not rebuild: {summary}");
                return false;
            }
            Debug.Log($"[held-prop] {summary}");
            return true;
        }

        /// <summary>Point each held tool at its family's meshes. Field-scoped: it writes one field per
        /// tool and leaves every other field as authored.</summary>
        static bool WireTools(IReadOnlyList<HeldPropMeshDef> baked)
        {
            ToolDef[] tools = AssetDatabase.FindAssets($"t:{nameof(ToolDef)}", new[] { "Assets/_Project/Data" })
                .Select(g => AssetDatabase.LoadAssetAtPath<ToolDef>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(t => t != null)
                .ToArray();

            bool ok = true;
            foreach ((string toolId, string family) in HeldTools)
            {
                ToolDef[] matches = tools.Where(t => t.Id == toolId).ToArray();
                HeldPropMeshDef[] meshes = baked.Where(d => d.Family == family).ToArray();
                if (matches.Length != 1 || meshes.Length == 0)
                {
                    Debug.LogError($"[held-prop] {toolId}: {matches.Length} ToolDef(s) carry the id and the " +
                                   $"bake made {meshes.Length} '{family}' mesh(es); expected one, and at least one.");
                    ok = false;
                    continue;
                }
                matches[0].HeldMeshes = meshes;
                EditorUtility.SetDirty(matches[0]);
                Debug.Log($"[held-prop] {AssetDatabase.GetAssetPath(matches[0])} wears " +
                          $"{string.Join(", ", meshes.Select(m => m.Id))}.");
            }
            AssetDatabase.SaveAssets();
            return ok;
        }
    }
}
#endif
