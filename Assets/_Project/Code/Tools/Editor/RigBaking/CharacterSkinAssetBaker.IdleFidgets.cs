using System;
using System.Collections.Generic;
using HiddenHarbours.Core;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.Tools.RigBaking
{
    public static partial class CharacterSkinAssetBaker
    {
        /// <summary>The rig's idle fidget one-shots (rig 10 ANIMS idleShift, idleScratch, idleHips),
        /// spelled as the skin keys them. Each starts and ends on idle frame 0, so the figure can drop
        /// into one from idle and back without a pop. The game picks them through IdleFidgetDef.ClipKey.</summary>
        public static string[] IdleFidgetStates() => new[] { "idleShift", "idleScratch", "idleHips" };

        static string[] WithIdleFidgetStates(string[] existing)
        {
            var states = new List<string>(existing);
            states.AddRange(IdleFidgetStates());
            return states.ToArray();
        }

        /// <summary>The batch entry: switch the fidgets on, then leave the editor (0 on success).</summary>
        public static void EnableIdleFidgetStatesBatchCli()
        {
            try { EnableIdleFidgetStatesCli(); EditorApplication.Exit(0); }
            catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }

        /// <summary>
        /// Switch the idle fidget states on for every committed cast skin that carries their clips. A
        /// re-bake of a committed def keeps its MeshStates (the <c>??=</c> rule in Compose), so a state
        /// the rig gained after the def was created needs this one explicit step, as the seated helms
        /// and the row stroke have. An EMPTY list is left alone: emptying it is how a character goes
        /// back to sprites. A def missing a fidget clip is a stale bake and stops the run.
        /// </summary>
        public static void EnableIdleFidgetStatesCli()
        {
            int skins = 0;
            var presets = new List<string>();
            foreach (var entry in CastBakeOrder()) presets.Add(entry.preset);
            // The outfits (Basil) are baked by CharacterOutfitsBuilder, not the cast order, and draw the
            // same idle, so they get the same switch.
            foreach (CharacterOutfits.Outfit outfit in CharacterOutfits.All)
                if (outfit.Kit == LiveKit) presets.Add(outfit.Key);
            foreach (string preset in presets)
            {
                var def = AssetDatabase.LoadAssetAtPath<CharacterSkinDef>(AssetPathFor(preset));
                if (def == null) throw new InvalidOperationException("Missing supported skin: " + preset);
                var states = new List<string>(def.MeshStates ?? Array.Empty<string>());
                if (states.Count == 0)
                {
                    Debug.Log($"[idle-fidgets] {preset}: MeshStates empty (sprites), left as found");
                    continue;
                }
                foreach (string state in IdleFidgetStates())
                {
                    if (!def.TryGetClip(state, out _))
                        throw new InvalidOperationException(
                            $"{preset} has no '{state}' clip: re-bake the cast from the current rig first.");
                    if (!states.Contains(state)) states.Add(state);
                }
                def.MeshStates = StatesWithClips(def, states.ToArray());
                EditorUtility.SetDirty(def);
                skins++;
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[idle-fidgets] switched on {string.Join(", ", IdleFidgetStates())} for {skins} skins.");
        }
    }
}
