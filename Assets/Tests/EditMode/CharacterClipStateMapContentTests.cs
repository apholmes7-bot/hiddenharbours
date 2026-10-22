using System;
using System.IO;
using HiddenHarbours.Core;
using NUnit.Framework;
using UnityEditor;

namespace HiddenHarbours.Tests.EditMode
{
    public sealed class CharacterClipStateMapContentTests
    {
        [Test]
        public void ClipStateMap_EveryMappedClip_IsInEverySkin()
        {
            var visual = AssetDatabase.LoadAssetAtPath<CharacterVisualDef>(
                "Assets/_Project/Data/Characters/FisherIso.asset");
            Assert.IsNotNull(visual);
            string[] files = Directory.GetFiles("Assets/_Project/Data/Characters/Skin", "*.asset");
            Assert.AreEqual(10, files.Length);
            foreach (string file in files)
            {
                var skin = AssetDatabase.LoadAssetAtPath<CharacterSkinDef>(file.Replace('\\', '/'));
                Assert.IsNotNull(skin, file);
                int mapped = 0;
                foreach (CharacterClip move in Enum.GetValues(typeof(CharacterClip)))
                    foreach (string carry in move == CharacterClip.Board || move == CharacterClip.BoardDown
                        ? new[] { null, "buckets", "tray", "pot" } : new string[] { null })
                    {
                        string key = CharacterClipStateMap.StateKeyFor(move, carry);
                        if (key == null) continue;
                        mapped++;
                        Assert.IsTrue(skin.TryGetClip(key, out var clip), file + ": " + key);
                        Assert.IsTrue(skin.DrawsAsMesh(key), file + ": " + key + " switch");
                        Assert.AreEqual(visual.ClipFrameCount(move), clip.FrameCount, file + ": " + key);
                        Assert.AreEqual(visual.ClipFramesPerSecond(move), clip.FramesPerSecond, 0.001f, key);
                        CharacterSkinPose.BuildHonestFrameMap(clip, skin.BoneCount,
                            CharacterSkinPose.FenceMetres, out int fenced);
                        Assert.Zero(fenced, file + ": " + key + " may not substitute a different frame");
                    }
                Assert.AreEqual(16, mapped, file);
                Assert.AreEqual(27, skin.MeshStates.Length, file);
            }
        }
    }
}
