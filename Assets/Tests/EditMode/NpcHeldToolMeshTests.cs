using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Player;
using HiddenHarbours.World;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>A villager at work holds her tool as its facet mesh</b> (ADR 0053 §10): the data side. The clam dig
    /// names the rig's dig clip and the SAME baked shovel the player's shovel draws; every cast skin can play
    /// that clip as a mesh and aims the shovel from the right-hand tool socket on every frame; and a villager
    /// is at work only while she stands at a place of today's outing. The drawing side (grip, aim, no sprite
    /// on a mesh villager, no per-frame allocation) is <c>NpcHeldToolFigureTests</c> in the Art test assembly.
    ///
    /// <para>Measured on the SHIPPED cast skins and the SHIPPED clam-dig activity, so a re-bake or a data
    /// edit that breaks the hold fails here rather than in a picture.</para>
    /// </summary>
    public sealed class NpcHeldToolMeshTests
    {
        private const string SkinFolder = "Assets/_Project/Data/Characters/Skin";
        private const string ClamDigPath = "Assets/_Project/Data/Beaches/BeachClamDig.asset";
        private const string PlayerShovelPath = "Assets/_Project/Data/Tools/Shovel.asset";
        private const string DigState = "dig";

        [Test]
        public void TheShippedClamDig_WorksTheDigClip_WithThePlayersOwnShovelMesh()
        {
            BeachActivityDef dig = Load<BeachActivityDef>(ClamDigPath);
            Assert.AreEqual(BeachActivityKind.Dig, dig.Kind);
            Assert.AreEqual(DigState, dig.WorkState, "the clam dig must name the rig's dig clip as its work");
            HeldPropMeshDef shovel = dig.HeldProp;
            Assert.IsNotNull(shovel, "the clam dig must name its tool");
            Assert.AreEqual(HeldPropMeshDef.ShovelFamily, shovel.Family);
            Assert.IsTrue(shovel.IsUsable(), "the shovel mesh is not usable: nothing to draw");
            Assert.IsTrue(HeldPropPose.TryParseSourcePose(shovel.SourcePose, out _, out _),
                          "the shovel's baked aim cannot be read off its provenance line");
            Assert.IsTrue(HeldPropPose.TryTipLocal(shovel, out _), "the shovel plots no shaft");

            // ONE shovel in the game: the villager's is the very asset the player's tool resolves to.
            ToolDef playerShovel = Load<ToolDef>(PlayerShovelPath);
            Assert.AreSame(playerShovel.HeldMeshFor(playerShovel.HeldMeshVariant), shovel,
                           "a villager must hold the same baked shovel the player holds, not a copy");
        }

        [Test]
        public void EveryCastSkin_DrawsTheDigAsAMesh_AndAimsTheShovelFromTheRightHandOnEveryFrame()
        {
            string[] guids = AssetDatabase.FindAssets("t:CharacterSkinDef", new[] { SkinFolder });
            Assert.IsNotEmpty(guids, "harness: no cast skins found");
            var failures = new List<string>();
            int checkedSkins = 0;
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var skin = AssetDatabase.LoadAssetAtPath<CharacterSkinDef>(path);
                if (skin == null) continue;
                checkedSkins++;
                if (!skin.DrawsAsMesh(DigState)) { failures.Add($"{path}: 'dig' is not a mesh state"); continue; }
                if (skin.IndexOfBone(HeldPropSockets.RightHandTool) == CharacterSkinDef.NoBone)
                    failures.Add($"{path}: no {HeldPropSockets.RightHandTool} socket");
                if (!skin.TryGetClip(DigState, out CharacterSkinDef.SkinClip clip))
                {
                    failures.Add($"{path}: no dig clip");
                    continue;
                }
                if (!clip.Loop) failures.Add($"{path}: the dig must loop to be worked at a place");
                if (clip.Tool == null || clip.Tool.Length != clip.FrameCount)
                {
                    failures.Add($"{path}: the dig has no tool key per frame");
                    continue;
                }
                for (int f = 0; f < clip.FrameCount; f++)
                    if (!clip.Tool[f].Held || clip.Tool[f].Kind != HeldPropMeshDef.ShovelFamily)
                        failures.Add($"{path}: dig frame {f} does not hold the shovel");
            }
            Assert.Greater(checkedSkins, 0, "harness: no skin loaded");
            Assert.IsEmpty(failures, string.Join("\n", failures));
        }

        [Test]
        public void AtAPlace_IsTheOutingsBlocks_StandingStill_AndNothingElse()
        {
            BeachActivityDef dig = Load<BeachActivityDef>(ClamDigPath);
            var outing = new BeachOuting { Activity = dig, FirstStop = 2, StopCount = 2 };
            Assert.IsTrue(BeachPlanning.IsAtPlace(outing, Pose(2, walking: false)));
            Assert.IsTrue(BeachPlanning.IsAtPlace(outing, Pose(3, walking: false)));
            Assert.IsFalse(BeachPlanning.IsAtPlace(outing, Pose(2, walking: true)), "on the walk out she is not at work");
            Assert.IsFalse(BeachPlanning.IsAtPlace(outing, Pose(1, walking: false)), "a block before the outing");
            Assert.IsFalse(BeachPlanning.IsAtPlace(outing, Pose(4, walking: false)), "the walk back is not the outing");
            var none = new BeachOuting { Activity = null, FirstStop = 2, StopCount = 2 };
            Assert.IsFalse(BeachPlanning.IsAtPlace(none, Pose(2, walking: false)));
            var empty = new BeachOuting { Activity = dig, FirstStop = 2, StopCount = 0 };
            Assert.IsFalse(BeachPlanning.IsAtPlace(empty, Pose(2, walking: false)));
        }

        private static RoutinePose Pose(int block, bool walking) =>
            new RoutinePose(Vector2.zero, block, RoutineActivity.Recreation, walking, 0f, default);

        private static T Load<T>(string path) where T : Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            Assert.IsNotNull(asset, $"harness: no {typeof(T).Name} at {path}");
            return asset;
        }
    }
}
