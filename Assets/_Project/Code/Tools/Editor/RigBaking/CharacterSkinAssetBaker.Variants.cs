using System;
using System.Collections.Generic;
using HiddenHarbours.Core;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.Tools.RigBaking
{
    // Proposal skins: a preset baked from a kit that is NOT the live one, into its own folder under its
    // own id, beside the shipped skin of the same preset. Nothing links them: no CharacterVisualDef, no
    // scene and no table names a variant, so the game draws exactly what it drew. They exist to be looked
    // at next to what ships (the next-iteration art pass, 2026-11-11) and to be adopted or deleted.
    //
    // The compose is the shipped one (ComposeV9 on a host that holds the variant's kit). This file only
    // chooses the kit, the path and the id, and writes the asset; Bake() and the cast bake are untouched.
    public static partial class CharacterSkinAssetBaker
    {
        /// <summary>Where proposal skins live: never <see cref="AssetFolder"/>, which holds the cast's.</summary>
        public const string VariantFolder = "Assets/_Project/Data/Characters/SkinNext";

        /// <summary>The proposal skins the next-iteration pass bakes: the player and the elder woman
        /// from rig 11.0.</summary>
        public static (CharacterRigKit kit, string preset)[] NextVariants() => new[]
        {
            (CharacterRigKit.Rig11, "fisher"),
            (CharacterRigKit.Rig11, "nan"),
        };

        /// <summary>The variant's file stem and id tail: <c>fisher_rig11</c>.</summary>
        public static string VariantStem(CharacterRigKit kit, string preset) =>
            $"{preset}_rig{kit.Pass.ToString(System.Globalization.CultureInfo.InvariantCulture)}";

        public static string VariantPathFor(CharacterRigKit kit, string preset) =>
            $"{VariantFolder}/{VariantStem(kit, preset)}.asset";

        /// <summary>The variant's stable id (<c>charskin.fisher_rig11</c>): its own, never the shipped
        /// preset's.</summary>
        public static string VariantIdFor(CharacterRigKit kit, string preset) =>
            "charskin." + VariantStem(kit, preset);

        /// <summary>
        /// Bake <paramref name="preset"/> from <paramref name="kit"/> into its own asset. Refuses the
        /// live kit: a preset's skin from the live kit is the shipped one, and <see cref="Bake"/> writes
        /// it. A fresh variant draws every state of <see cref="CastMeshStates"/> it carries a clip for
        /// (the seated helm clips are calibrated against rig 10 alone and are not baked here); a
        /// refresh keeps the list its asset holds.
        /// </summary>
        public static CharacterSkinDef BakeVariant(CharacterRigKit kit, string preset,
                                                   Action<string, float> progress = null)
        {
            if (kit == null) throw new ArgumentNullException(nameof(kit));
            if (string.IsNullOrEmpty(preset)) throw new ArgumentNullException(nameof(preset));
            if (kit == LiveKit)
                throw new InvalidOperationException(
                    $"{kit} is the live kit; '{preset}' from it is the shipped skin. Bake it with Bake().");

            string path = VariantPathFor(kit, preset);
            EnsureFolder(VariantFolder);
            var existing = AssetDatabase.LoadAssetAtPath<CharacterSkinDef>(path);
            bool created = existing == null;

            SkinBake bake;
            using (IRigScriptHost host = RigScriptHostFactory.Create())
            {
                CharacterSkinExtractor.Load9(host, kit);
                bake = ComposeV9(host, preset, existing, progress);
            }

            CharacterSkinDef def = bake.Def;
            def.Id = VariantIdFor(kit, preset);
            def.ReachPx = CharacterSkinPose.MeasureReach(def);

            if (created)
            {
                var states = new List<string>();
                foreach (string state in CastMeshStates)
                    if (def.TryGetClip(state, out _)) states.Add(state);
                def.MeshStates = StatesWithClips(def, states.ToArray());
            }

            if (!created)
                foreach (UnityEngine.Object sub in AssetDatabase.LoadAllAssetsAtPath(path))
                    if (sub is Mesh old)
                    {
                        AssetDatabase.RemoveObjectFromAsset(old);
                        UnityEngine.Object.DestroyImmediate(old, allowDestroyingAssets: true);
                    }

            if (created) AssetDatabase.CreateAsset(def, path);
            AssetDatabase.AddObjectToAsset(def.BindMesh, def);
            EditorUtility.SetDirty(def);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(path);

            Debug.Log(
                $"[char-skin] variant {(created ? "Created" : "Refreshed")} {path} — {def.Id} from {kit}\n" +
                $"  bind mesh {bake.Built.Triangles:N0} tris / {bake.Built.Vertices:N0} verts, " +
                $"{def.BoneCount} bones, {def.Clips.Length} clips, {bake.TotalFrames} frames\n" +
                $"  TOTAL {bake.TotalBytes / 1024.0:N1} KB (bind {bake.BindBytes / 1024.0:N1} + " +
                $"clips {bake.ClipBytes / 1024.0:N1})\n" +
                $"  key ({def.KeyScreen.x:F4}, {def.KeyScreen.y:F4}, {def.KeyScreen.z:F4}), " +
                $"{def.Materials.Length} materials, MeshStates [{string.Join(", ", def.MeshStates)}]\n" +
                $"  rig {def.SourceRigPath} rev {def.SourceRigRevision} sha {def.SourceRigSha256[..12]}…\n" +
                $"  usable = {def.IsUsable()}");
            return def;
        }

        /// <summary>Headless entry (<c>-executeMethod</c>): every proposal skin of
        /// <see cref="NextVariants"/>. Fails loud on the first that fails and exits 1. Grep the log for
        /// the closing marker; a fresh worktree's first run skips <c>-executeMethod</c>.</summary>
        public static void BakeNextVariantsCli()
        {
            string current = null;
            try
            {
                foreach ((CharacterRigKit kit, string preset) in NextVariants())
                {
                    current = VariantStem(kit, preset);
                    BakeVariant(kit, preset);
                }
                Debug.Log($"[char-skin] CLI variant bake OK — {NextVariants().Length} skins.");
            }
            catch (Exception e)
            {
                Debug.LogError($"[char-skin] CLI variant bake FAILED at '{current}': {e}");
                EditorApplication.Exit(1);
            }
        }
    }
}
