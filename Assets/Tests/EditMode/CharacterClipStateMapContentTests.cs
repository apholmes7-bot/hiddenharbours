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
            // The ten rig presets plus one cast member's own outfit over a preset's body (charskin.basil).
            Assert.AreEqual(11, files.Length);
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
                string[] seated = { "drive_helm_go_fast", "drive_helm_bowrider", "drive_helm_express_cruiser",
                                    "astride_helm_jet_ski" };
                foreach (string state in seated)
                {
                    Assert.IsTrue(skin.TryGetClip(state, out _), file + ": " + state);
                    Assert.IsTrue(skin.DrawsAsMesh(state), file + ": " + state + " switch");
                }
                // The one-stroke rowing clip is not a CharacterClip: RowingFigure picks it from the oar's
                // phase, so it is held here by name rather than through the map.
                Assert.AreEqual("row_oars", RowingFigure.RowStrokeKey);
                Assert.IsTrue(skin.TryGetClip(RowingFigure.RowStrokeKey, out _), file + ": row_oars");
                Assert.IsTrue(skin.DrawsAsMesh(RowingFigure.RowStrokeKey), file + ": row_oars switch");
                // The idle fidget one-shots are not CharacterClips either: CharacterFigureLife swaps them in for
                // the plain idle by the shipped library's Clip fidgets, so each is held here by its clip key.
                int fidgetClips = 0;
                foreach (IdleFidgetDef fidget in IdleFidgetLibraryDef.Shipped.Fidgets)
                {
                    if (fidget == null || fidget.Kind != IdleFidgetDef.Channel.Clip) continue;
                    fidgetClips++;
                    Assert.IsTrue(skin.TryGetClip(fidget.ClipKey, out _), file + ": " + fidget.ClipKey);
                    Assert.IsTrue(skin.DrawsAsMesh(fidget.ClipKey), file + ": " + fidget.ClipKey + " switch");
                }
                Assert.AreEqual(3, fidgetClips, "the shipped library's clip fidgets: idleShift, idleScratch, idleHips");
                Assert.AreEqual(40, Array.FindAll(skin.MeshStates, s => Array.IndexOf(seated, s) < 0).Length,
                    file + ": the 27 existing states plus eight rod clips, dig, row_oars and the three idle fidgets");
                Assert.AreEqual(44, skin.MeshStates.Length, file);
            }
        }
    }
}
