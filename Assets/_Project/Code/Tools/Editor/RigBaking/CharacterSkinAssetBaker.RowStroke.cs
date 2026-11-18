using System;
using System.Collections.Generic;
using System.IO;
using HiddenHarbours.Core;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.Tools.RigBaking
{
    /// <summary>
    /// <b>One rowing stroke at four times the frames</b> (docs/art/rigs/character/rig10/row-stroke.md).
    /// The rig's <c>oars_row</c> clip holds TWO strokes in its eight frames, so a stroke plays on four
    /// poses. The contract names a finer sampling of the same clip: one stroke over
    /// <see cref="RowStrokeContract.frames"/> frames, read through the rig's own <c>evalClip</c>
    /// (<see cref="CharacterSkinExtractor.ReadSubLoopClip9"/>). The rig is not edited, so no committed
    /// skin's source pins move; the clip is added beside the others, as the seated helms were.
    /// </summary>
    public static partial class CharacterSkinAssetBaker
    {
        public const string RowStrokeContractPath = "docs/art/rigs/character/rig10/row-stroke.contract.json";
        public const string RowStrokeSchema = "row-stroke@1";

        /// <summary>The fewest frames a stroke may be baked at: the owner's floor for a smooth stroke
        /// (the rig's own clip gives four).</summary>
        public const int RowStrokeMinFrames = 8;

        [Serializable] public sealed class RowStrokeContract
        {
            public string schema, rig, poses, rigSha256, posesSha256, state, source;
            public int strokesInSource, frames;
            public float ms;
        }

        public static RowStrokeContract ReadRowStrokeContract()
        {
            string abs = Path.Combine(RigCatalog.RepoRoot, RowStrokeContractPath);
            var c = JsonUtility.FromJson<RowStrokeContract>(File.ReadAllText(abs));
            if (c == null || c.schema != RowStrokeSchema || string.IsNullOrEmpty(c.state) ||
                string.IsNullOrEmpty(c.source) || c.strokesInSource < 1 || c.frames < RowStrokeMinFrames || !(c.ms > 0))
                throw new InvalidOperationException("Invalid row stroke contract.");
            if (CharacterSkinExtractor.LfSha256File(Path.Combine(RigCatalog.RepoRoot, c.rig)) != c.rigSha256 ||
                CharacterSkinExtractor.LfSha256File(Path.Combine(RigCatalog.RepoRoot, c.poses)) != c.posesSha256)
                throw new InvalidOperationException("Row stroke source pins are stale: the rig changed, re-read its rowing clip.");
            return c;
        }

        static void AppendRowStroke(IRigScriptHost host, CharacterSkinDef def, RigBone[] bones, bool enable)
        {
            var contract = ReadRowStrokeContract();
            string sha = CharacterSkinExtractor.LfSha256File(Path.Combine(RigCatalog.RepoRoot, RowStrokeContractPath));
            var rc = CharacterSkinExtractor.ReadSubLoopClip9(host, def.Preset, contract.source, bones,
                                                             contract.frames, contract.strokesInSource, contract.ms);
            float worst = 0; string at = "";
            var clip = ToClip(rc, contract.state, bones.Length, ref worst, ref at);
            clip.Face = rc.Face; clip.Tool = rc.Tool; clip.PoseContractSha256 = sha;

            var clips = new List<CharacterSkinDef.SkinClip>(def.Clips ?? Array.Empty<CharacterSkinDef.SkinClip>());
            clips.RemoveAll(c => c.StateKey == contract.state);
            clips.Add(clip);
            def.Clips = clips.ToArray();
            if (!enable) return;
            var states = new List<string>(def.MeshStates ?? Array.Empty<string>());
            if (!states.Contains(contract.state)) states.Add(contract.state);
            def.MeshStates = StatesWithClips(def, states.ToArray());
            Debug.Log($"[row-stroke] {def.Preset}: {contract.state} {clip.FrameCount} frames, worst step {worst:F1} deg at {at}");
        }

        /// <summary>The batch entry: bake, then leave the editor (0 on success), so a lease is not held idle.</summary>
        public static void BakeRowStrokeBatchCli()
        {
            try { BakeRowStrokeCli(); EditorApplication.Exit(0); }
            catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }

        /// <summary>Add the one-stroke rowing clip to every committed skin and switch it on, without
        /// rebuilding the bind meshes or touching any other clip.</summary>
        public static void BakeRowStrokeCli()
        {
            var contract = ReadRowStrokeContract();
            int skins = 0;
            using (var host = RigScriptHostFactory.Create())
            {
                CharacterSkinExtractor.Load9(host, CharacterRigKit.Rig10);
                foreach (var entry in CastBakeOrder())
                {
                    var def = AssetDatabase.LoadAssetAtPath<CharacterSkinDef>(AssetPathFor(entry.preset));
                    if (def == null) throw new InvalidOperationException("Missing supported skin: " + entry.preset);
                    if (def.SourceRigSha256 != contract.rigSha256 || def.BaseRigSha256 != contract.posesSha256)
                        throw new InvalidOperationException("Committed skin source pins are stale: " + entry.preset);
                    var bones = CharacterSkinExtractor.ReadSkeleton9(host, def.Preset);
                    if (bones.Length != def.BoneCount) throw new InvalidOperationException("Skin skeleton changed.");
                    for (int i = 0; i < bones.Length; i++)
                        if (bones[i].Id != def.Bones[i].Id) throw new InvalidOperationException("Skin bone order changed.");
                    AppendRowStroke(host, def, bones, enable: true);
                    def.ReachPx = CharacterSkinPose.MeasureReach(def);
                    EditorUtility.SetDirty(def); skins++;
                }
            }
            AssetDatabase.SaveAssets();
            string report = Path.Combine(RigCatalog.RepoRoot, "Evidence~", "rig-facts", "production", "row-stroke-bake.json");
            Directory.CreateDirectory(Path.GetDirectoryName(report));
            File.WriteAllText(report, "{\"skins\":" + skins + ",\"state\":\"" + contract.state + "\",\"frames\":" + contract.frames + "}");
            Debug.Log("[row-stroke] baked " + skins + " skins, " + contract.state + " at " + contract.frames +
                      " frames; unchanged bind meshes.");
        }
    }
}
