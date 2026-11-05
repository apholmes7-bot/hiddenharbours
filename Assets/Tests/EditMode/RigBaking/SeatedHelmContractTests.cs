using System;
using System.Globalization;
using System.IO;
using System.Linq;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;
using HiddenHarbours.Tools.RigBaking;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.Tests.RigBaking
{
    public class SeatedHelmContractTests
    {
        [Serializable] class WalkPins { public AnchorPins anchors; }
        [Serializable] class AnchorPins { public Vector3 helm, pilot, wheel; }
        [Test]
        public void TenCommittedSkins_ReplayEveryCalibratedFrameAndContactFromTheSourceRig()
        {
            var contract = CharacterSkinAssetBaker.ReadSeatedHelmContract();
            Assert.AreEqual(3, contract.rows.Length);
            foreach (var row in contract.rows)
            {
                var a = JsonUtility.FromJson<WalkPins>(File.ReadAllText(Path.Combine(RigCatalog.RepoRoot,row.walk))).anchors;
                Assert.Less(Vector3.Distance(a.helm,row.seat), .00001f);
                Assert.Less(Vector3.Distance(a.wheel,row.wheel), .00001f);
                Assert.Less(Vector3.Distance(new Vector3(a.helm.x,a.helm.y+.02f,a.pilot.z),row.floorOrigin), .00001f);
                Assert.AreEqual(a.helm.z-a.pilot.z,row.seatZ,.00001f);
                Assert.AreEqual(a.wheel.z-a.pilot.z,row.wheelZ,.00001f);
                Assert.AreEqual(a.wheel.y-row.floorOrigin.y,row.wheelY,.00001f);
            }
            string hash = CharacterSkinExtractor.LfSha256File(Path.Combine(RigCatalog.RepoRoot,
                CharacterSkinAssetBaker.SeatedHelmContractPath));
            int skins = 0, frames = 0;
            using var host = RigScriptHostFactory.Create();
            CharacterSkinExtractor.Load9(host, CharacterRigKit.Rig10);
            foreach (string file in Directory.GetFiles("Assets/_Project/Data/Characters/Skin", "*.asset"))
            {
                var skin = AssetDatabase.LoadAssetAtPath<CharacterSkinDef>(file.Replace('\\', '/'));
                Assert.AreEqual(contract.rigSha256, skin.SourceRigSha256, file);
                Assert.AreEqual(contract.posesSha256, skin.BaseRigSha256, file);
                var bones = CharacterSkinExtractor.ReadSkeleton9(host, skin.Preset);
                Assert.AreEqual(skin.BoneCount, bones.Length);
                foreach (var row in contract.rows)
                {
                    var raw = CharacterSkinExtractor.ReadClip9(host, skin.Preset, "drive", bones, out _, row.OptionsJson);
                    Assert.IsTrue(skin.TryGetClip(row.state, out var clip), file + " " + row.state);
                    Assert.IsTrue(skin.DrawsAsMesh(row.state));
                    Assert.AreEqual(hash, clip.PoseContractSha256);
                    Assert.AreEqual(6, clip.FrameCount);
                    Assert.AreEqual(raw.Frames * bones.Length, clip.Keys.Length);
                    Assert.AreEqual(6, clip.SeatPins.Length);
                    Assert.AreEqual(6, clip.HandLeftPins.Length);
                    Assert.AreEqual(6, clip.HandRightPins.Length);
                    Assert.AreEqual(6, clip.FootLeftPins.Length);
                    Assert.AreEqual(6, clip.FootRightPins.Length);
                    for (int i = 0; i < clip.Keys.Length; i++)
                    {
                        Assert.Less(Vector3.Distance(raw.Pos[i].ToVector3(), clip.Keys[i].Position), .00001f);
                        var q = new Quaternion((float)raw.Rx[i], (float)raw.Ry[i], (float)raw.Rz[i], (float)raw.Rw[i]);
                        var baked = clip.Keys[i].Rotation;
                        Assert.Less(Mathf.Abs(q.x-baked.x), .000001f);
                        Assert.Less(Mathf.Abs(q.y-baked.y), .000001f);
                        Assert.Less(Mathf.Abs(q.z-baked.z), .000001f);
                        Assert.Less(Mathf.Abs(q.w-baked.w), .000001f);
                    }
                    for (int f = 0; f < 6; f++)
                    {
                        string u = (f / 6d).ToString("R", CultureInfo.InvariantCulture);
                        string packed = host.EvaluateString("(function(){var C=CharacterIso10,P=C.pinsOf(C.evalClip('drive'," +
                            u + ",__hhClip.build," + row.OptionsJson + "));return P.handL.concat(P.handR,P.footL,P.footR).join(',');})()");
                        var p = packed.Split(',').Select(v => float.Parse(v, CultureInfo.InvariantCulture)).ToArray();
                        Vector3[] actual = { clip.HandLeftPins[f], clip.HandRightPins[f], clip.FootLeftPins[f], clip.FootRightPins[f] };
                        for (int j = 0; j < 4; j++) Assert.Less(Vector3.Distance(actual[j], new Vector3(p[j*3],p[j*3+1],p[j*3+2])), .00001f);
                        Assert.Less(Vector3.Distance(row.floorOrigin + clip.SeatPins[f], row.seat), .00001f);
                        frames++;
                    }
                }
                skins++;
            }
            Assert.AreEqual(10, skins); Assert.AreEqual(180, frames);
        }

        [Test]
        public void OnlyThreeHullVisualsOptIn_AndStandingAnchorIsUnchanged()
        {
            int enrolled = 0;
            foreach (string file in Directory.GetFiles("Assets/_Project/Data/Boats/Visuals", "*.asset"))
            {
                var v = AssetDatabase.LoadAssetAtPath<BoatVisualDef>(file.Replace('\\', '/'));
                if (v != null && !string.IsNullOrEmpty(v.SeatedHelmState)) enrolled++;
            }
            Assert.AreEqual(3, enrolled);
            var gf = AssetDatabase.LoadAssetAtPath<BoatVisualDef>("Assets/_Project/Data/Boats/Visuals/GoFastIso.asset");
            Assert.AreEqual(new Vector3(.42f,-2.18f,.62f), gf.SeatedHelmFloorOrigin);
            Assert.AreEqual(new Vector3(.42f,-1.86f,.62f), gf.Interior.Anchors.Single(a => a.Id == "helm").ReachPoint);
        }
    }
}
