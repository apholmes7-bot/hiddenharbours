using System;
using System.IO;
using HiddenHarbours.Core;
using HiddenHarbours.Tools.RigBaking;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.Tests.RigBaking
{
    /// <summary>
    /// The one-stroke rowing clip (docs/art/rigs/character/rig10/row-stroke.md): the contract reads and
    /// its rig pins are current, the rig's rowing clip really holds identical strokes (so one of them
    /// loops seamlessly), the finer reader reproduces the rig's own frames, and, once baked, every
    /// committed skin carries the clip the contract names.
    /// </summary>
    public class RowStrokeContractTests
    {
        private const double PosTolerance = 1e-6;
        private const double DotTolerance = 1e-9;

        [Test]
        public void TheContractReads_ItsRigPinsAreCurrent_AndItAsksForAtLeastEightFramesAStroke()
        {
            var c = CharacterSkinAssetBaker.ReadRowStrokeContract();   // throws on a stale pin
            Assert.AreEqual("row_oars", c.state, "the gameplay side looks this state up by name");
            Assert.AreEqual("oars_row", c.source);
            Assert.AreEqual(2, c.strokesInSource, "the rig's oars_row reads ph = frac(2*u)");
            Assert.GreaterOrEqual(c.frames, CharacterSkinAssetBaker.RowStrokeMinFrames);
            var seated = CharacterSkinAssetBaker.ReadSeatedHelmContract();
            Assert.AreEqual(seated.rigSha256, c.rigSha256, "both contracts pin the same live rig");
            Assert.AreEqual(seated.posesSha256, c.posesSha256);
        }

        [Test]
        public void TheRigsRowingClip_RepeatsItsStroke_SoOneStrokeLoopsWithoutASeam()
        {
            var c = CharacterSkinAssetBaker.ReadRowStrokeContract();
            using var host = RigScriptHostFactory.Create();
            CharacterSkinExtractor.Load9(host, CharacterRigKit.Rig10);
            RigBone[] bones = CharacterSkinExtractor.ReadSkeleton9(host, CharacterRigBakeMenu.PlayerPreset);

            // Two frames over one whole loop: u = 0 and u = 1/strokes, the start of the second stroke.
            RigSkinClip seam = CharacterSkinExtractor.ReadSubLoopClip9(
                host, CharacterRigBakeMenu.PlayerPreset, c.source, bones, c.strokesInSource, 1, c.ms);
            int n = bones.Length;
            for (int b = 0; b < n; b++)
                AssertSamePose(seam, b, seam, n + b, $"bone '{bones[b].Id}': stroke 2 does not start where stroke 1 did");

            // And the stroke moves: mid-stroke is not the start.
            RigSkinClip half = CharacterSkinExtractor.ReadSubLoopClip9(
                host, CharacterRigBakeMenu.PlayerPreset, c.source, bones, 2, c.strokesInSource, c.ms);
            double moved = 0;
            for (int b = 0; b < n; b++) moved = Math.Max(moved, Dist(half.Pos[n + b], half.Pos[b]) + QuatGap(half, b, half, n + b));
            Assert.Greater(moved, 1e-3, "harness: the rowing clip did not move between its start and mid-stroke");
        }

        [Test]
        public void TheFinerReader_ReproducesTheRigsOwnFrames_EveryFourthFrame()
        {
            var c = CharacterSkinAssetBaker.ReadRowStrokeContract();
            using var host = RigScriptHostFactory.Create();
            CharacterSkinExtractor.Load9(host, CharacterRigKit.Rig10);
            string preset = CharacterRigBakeMenu.PlayerPreset;
            RigBone[] bones = CharacterSkinExtractor.ReadSkeleton9(host, preset);

            RigSkinClip rig = CharacterSkinExtractor.ReadClip9(host, preset, c.source, bones, out string rigKey);
            RigSkinClip fine = CharacterSkinExtractor.ReadSubLoopClip9(host, preset, c.source, bones,
                                                                       c.frames, c.strokesInSource, c.ms);
            Assert.AreEqual("walk_oars", rigKey, "the source clip is the one every skin bakes as walk_oars");
            Assert.AreEqual(c.frames, fine.Frames);
            Assert.AreEqual(rig.Anim, fine.Anim);
            Assert.AreEqual(rig.Carry, fine.Carry);
            Assert.IsTrue(fine.Loop);

            // Rig frame j sits at u = j / rig.Frames; fine frame k at u = k / (frames x strokes).
            int rigPerStroke = rig.Frames / c.strokesInSource;
            Assert.AreEqual(0, c.frames % rigPerStroke, "the fine frames must land on the rig's own");
            int step = c.frames / rigPerStroke;
            int n = bones.Length, slots = CharacterSkinDef.FaceSlotNames.Length;
            for (int j = 0; j < rigPerStroke; j++)
            {
                for (int b = 0; b < n; b++)
                    AssertSamePose(rig, j * n + b, fine, j * step * n + b, $"rig frame {j}, bone '{bones[b].Id}'");
                for (int s = 0; s < slots; s++)
                    Assert.AreEqual(rig.Face[j * slots + s], fine.Face[j * step * slots + s], $"rig frame {j} face slot {s}");
            }
        }

        [Test]
        public void EveryCommittedSkin_CarriesTheContractsClip_OnceBaked()
        {
            var c = CharacterSkinAssetBaker.ReadRowStrokeContract();
            string hash = CharacterSkinExtractor.LfSha256File(Path.Combine(RigCatalog.RepoRoot,
                CharacterSkinAssetBaker.RowStrokeContractPath));
            string[] files = Directory.GetFiles("Assets/_Project/Data/Characters/Skin", "*.asset");
            int carrying = 0;
            foreach (string file in files)
            {
                var skin = AssetDatabase.LoadAssetAtPath<CharacterSkinDef>(file.Replace('\\', '/'));
                if (skin == null || !skin.TryGetClip(c.state, out CharacterSkinDef.SkinClip clip)) continue;
                carrying++;
                Assert.IsTrue(skin.DrawsAsMesh(c.state), $"{file}: carries {c.state} but does not switch it on");
                Assert.AreEqual(c.frames, clip.FrameCount, file);
                Assert.AreEqual(c.frames * skin.BoneCount, clip.Keys.Length, file);
                Assert.AreEqual(hash, clip.PoseContractSha256, $"{file}: baked from another contract; re-run the bake");
                Assert.IsTrue(clip.Loop, file);
            }
            if (carrying == 0)
                Assert.Ignore($"{c.state} is not baked yet: run CharacterSkinAssetBaker.BakeRowStrokeBatchCli on a warm box (row-stroke.md).");
            Assert.AreEqual(files.Length, carrying, "some committed skins carry the rowing clip and some do not");
        }

        private static void AssertSamePose(RigSkinClip a, int i, RigSkinClip b, int j, string what)
        {
            Assert.Less(Dist(a.Pos[i], b.Pos[j]), PosTolerance, what + " (position)");
            Assert.Less(QuatGap(a, i, b, j), DotTolerance, what + " (rotation)");
        }

        private static double Dist(Vector3d a, Vector3d b)
        {
            double dx = a.X - b.X, dy = a.Y - b.Y, dz = a.Z - b.Z;
            return Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        // 1 - |q.q'|: zero for the same rotation whichever hemisphere each quaternion is in.
        private static double QuatGap(RigSkinClip a, int i, RigSkinClip b, int j) =>
            1.0 - Math.Abs(a.Rx[i] * b.Rx[j] + a.Ry[i] * b.Ry[j] + a.Rz[i] * b.Rz[j] + a.Rw[i] * b.Rw[j]);
    }
}
