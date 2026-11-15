using System;
using System.Linq;
using HiddenHarbours.Core;
using HiddenHarbours.Player;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.Tests.EditMode
{
    public sealed class FishingMeshStateTests
    {
        static readonly string[] Presets =
            { "fisher", "ginny", "skipper", "nan", "hand", "cutter", "packer", "deckboss", "boy", "girl" };
        static readonly string[] Required =
            { "hold", "cast", "bite", "strike", "reel", "land", "castBack", "castRelease", "dig" };

        [TestCaseSource(nameof(Presets))]
        public void EveryRig103SkinCarriesAndEnablesEveryFishingAndDigClip(string preset)
        {
            var skin = AssetDatabase.LoadAssetAtPath<CharacterSkinDef>(
                $"Assets/_Project/Data/Characters/Skin/{preset}.asset");
            Assert.IsNotNull(skin, preset);
            Assert.AreEqual("10.3", skin.SourceRigRevision, preset);
            Assert.IsTrue(skin.IsUsable(), preset);
            Assert.AreEqual(40, skin.MeshStates.Length, "31 adopted states plus the nine fishing/dig states");
            Assert.AreEqual(40, skin.MeshStates.Distinct(StringComparer.Ordinal).Count());
            foreach (string key in Required)
            {
                Assert.IsTrue(skin.DrawsAsMesh(key), preset + ": " + key);
                Assert.IsTrue(skin.TryGetClip(key, out var clip), preset + ": " + key);
                Assert.IsTrue(clip.KeysWellFormed(skin.BoneCount), preset + ": " + key);
            }
            foreach (FishingPose pose in Enum.GetValues(typeof(FishingPose)))
                if (pose != FishingPose.None)
                    Assert.Contains(PlayerFishingAnimMath.StateKeyFor(pose), Required);
            Assert.AreEqual("dig", PlayerFishingAnimMath.DigStateKey);
        }

        [TestCase(FishingPhase.WindBack, 0f, "castBack")]
        [TestCase(FishingPhase.Cast, 0f, "castRelease")]
        [TestCase(FishingPhase.Waiting, 0f, "hold")]
        [TestCase(FishingPhase.Sinking, 0f, "hold")]
        [TestCase(FishingPhase.BiteNibble, 0f, "bite")]
        [TestCase(FishingPhase.Bite, 0f, "bite")]
        [TestCase(FishingPhase.Fighting, 0f, "strike")]
        [TestCase(FishingPhase.FightDeep, 0f, "strike")]
        [TestCase(FishingPhase.FightSurface, 0f, "strike")]
        [TestCase(FishingPhase.Fighting, 1f, "reel")]
        [TestCase(FishingPhase.FightDeep, 1f, "reel")]
        [TestCase(FishingPhase.FightSurface, 1f, "reel")]
        [TestCase(FishingPhase.Landed, 0f, "land")]
        [TestCase(FishingPhase.Idle, 0f, null)]
        [TestCase(FishingPhase.Tending, 0f, null)]
        public void TheExistingSpritePhaseMappingNamesTheMeshClip(FishingPhase phase, float seconds, string expected)
        {
            var pose = PlayerFishingAnimMath.PoseFor(phase, seconds, 0.5f, stationary: true);
            Assert.AreEqual(expected, PlayerFishingAnimMath.StateKeyFor(pose));
        }

        [Test]
        public void TheOneSwitchShipsOnInCodeAndData()
        {
            var config = ScriptableObject.CreateInstance<GameConfig>();
            try
            {
                Assert.IsTrue(config.MeshFishingAndDig);
                var shipped = AssetDatabase.LoadAssetAtPath<GameConfig>("Assets/_Project/Data/Config/GameConfig.asset");
                Assert.IsNotNull(shipped);
                Assert.IsTrue(shipped.MeshFishingAndDig);
            }
            finally { UnityEngine.Object.DestroyImmediate(config); }
        }
    }
}
