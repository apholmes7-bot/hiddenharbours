using HiddenHarbours.Art;
using HiddenHarbours.Core;
using HiddenHarbours.Player;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// While he swims, the tools he carries are stowed, not drawn (GameConfig.SwimStowsCarriedTools): the
    /// rod mesh has no waterline cut, so on the swim pose it stood out of the water at full strength.
    /// Dry ground and wading keep the rod in his hands.
    /// </summary>
    public sealed class CarryHandsSwimStowTests
    {
        [Test]
        public void OnlyTheSwimBandStows_AndOnlyWithTheSwitchOn()
        {
            var config = ScriptableObject.CreateInstance<GameConfig>();
            try
            {
                Assert.IsTrue(config.SwimStowsCarriedTools, "the code default stows");
                Assert.IsTrue(CarryHands.StowsTools(config, OnFootWaterState.Swim), "swimming stows the rod");
                Assert.IsFalse(CarryHands.StowsTools(config, OnFootWaterState.Wade), "wading keeps it in his hands");
                Assert.IsFalse(CarryHands.StowsTools(config, OnFootWaterState.Dry), "dry ground keeps it in his hands");

                config.SwimStowsCarriedTools = false;
                Assert.IsFalse(CarryHands.StowsTools(config, OnFootWaterState.Swim), "switched off, nothing is stowed");
            }
            finally { Object.DestroyImmediate(config); }
        }

        [Test]
        public void NoConfig_StowsNothing()
        {
            Assert.IsFalse(CarryHands.StowsTools(null, OnFootWaterState.Swim));
        }

        [Test]
        public void TheShippedConfig_StowsTheRodWhileHeSwims()
        {
            var shipped = AssetDatabase.LoadAssetAtPath<GameConfig>("Assets/_Project/Data/Config/GameConfig.asset");
            Assert.IsNotNull(shipped, "the shipped GameConfig");
            Assert.IsTrue(shipped.SwimStowsCarriedTools, "nothing hangs above the water that is not really there");
        }

        [Test]
        public void AToolDrawnElsewhere_CastsNoShadowEither_AndGetsItBack()
        {
            var go = new GameObject("tool");
            try
            {
                var sr = go.AddComponent<SpriteRenderer>();
                var shadow = go.AddComponent<SpriteShadow>();
                var tool = go.AddComponent<CarriableTool>();

                tool.SetDrawnElsewhere(true);
                Assert.IsTrue(sr.forceRenderingOff, "the sprite is held out of the picture");
                Assert.IsTrue(shadow.Quiet, "and so is its sun shadow: a hidden rod threw a ghost on the water");

                tool.SetDrawnElsewhere(false);
                Assert.IsFalse(sr.forceRenderingOff);
                Assert.IsFalse(shadow.Quiet, "drawn again, it casts again");
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}
