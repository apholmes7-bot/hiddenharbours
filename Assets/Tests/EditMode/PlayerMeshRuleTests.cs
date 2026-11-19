using System;
using System.Collections.Generic;
using HiddenHarbours.Core;
using HiddenHarbours.Player;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// ADR 0060: the player is always the mesh. The guard the owner asked for: with the legacy switch
    /// off, every state the player can be in resolves to a baked mesh clip on every body he can wear,
    /// directly or through the fallback map, and nothing hands him back to a sprite sheet.
    /// </summary>
    public sealed class PlayerMeshRuleTests
    {
        private const string ShippedConfigPath = "Assets/_Project/Data/Config/GameConfig.asset";

        // Every baked body the player's look can resolve to (the presets the creator offers).
        private static readonly string[] Presets =
            { "fisher", "ginny", "skipper", "nan", "hand", "cutter", "packer", "deckboss", "boy", "girl" };

        private static readonly string[] Carries = { null, "", "buckets", "tray", "pot", "unoffered_carry" };

        private static CharacterSkinDef Skin(string preset)
        {
            var skin = AssetDatabase.LoadAssetAtPath<CharacterSkinDef>(
                $"Assets/_Project/Data/Characters/Skin/{preset}.asset");
            Assert.IsNotNull(skin, preset);
            Assert.IsTrue(skin.IsUsable(), preset + " is not usable");
            return skin;
        }

        private static GameConfig Shipped()
        {
            var config = AssetDatabase.LoadAssetAtPath<GameConfig>(ShippedConfigPath);
            Assert.IsNotNull(config, "the shipped config loads from " + ShippedConfigPath);
            return config;
        }

        /// <summary>Every state key the player's presenter can ask a skin for, read from the enums and
        /// maps that produce them rather than from a list written here.</summary>
        private static IEnumerable<string> PlayerWantedKeys(CharacterSkinDef skin)
        {
            foreach (CharacterClip clip in Enum.GetValues(typeof(CharacterClip)))
            {
                if (clip == CharacterClip.None) continue;
                foreach (string carry in Carries) yield return PlayerMeshRule.WantedKeyFor(clip, carry);
            }
            foreach (CharacterStance stance in Enum.GetValues(typeof(CharacterStance)))
            foreach (CharacterGait gait in Enum.GetValues(typeof(CharacterGait)))
            foreach (string carry in Carries)
                yield return CharacterSkinStateMap.StateKeyFor(stance, gait, carry);
            foreach (FishingPose pose in Enum.GetValues(typeof(FishingPose)))
                if (pose != FishingPose.None) yield return PlayerFishingAnimMath.StateKeyFor(pose);
            yield return PlayerFishingAnimMath.DigStateKey;
            // The seated helms and the oar stroke are asked for by the keys the skin lists.
            foreach (string key in skin.MeshStates) yield return key;
        }

        // ---------------------------------------------------------------- the switch

        [Test]
        public void TheShippedGame_NeverFallsBackToTheSpriteSheet()
        {
            GameConfig shipped = Shipped();
            Assert.IsFalse(shipped.PlayerLegacySpriteFallback,
                "the shipped config must keep the player on the mesh (ADR 0060)");
            Assert.IsTrue(DeckRiderMeshPresenter.MeshOnly(shipped));
        }

        [Test]
        public void AFreshConfig_KeepsTheOldHandOver_AndCarriesTheShippedMap()
        {
            Assert.IsTrue(GameConfig.DefaultPlayerLegacySpriteFallback, "the code default keeps the old hand-over");
            var fresh = ScriptableObject.CreateInstance<GameConfig>();
            try
            {
                Assert.IsTrue(fresh.PlayerLegacySpriteFallback);
                Assert.IsFalse(DeckRiderMeshPresenter.MeshOnly(fresh));
                Assert.IsFalse(DeckRiderMeshPresenter.MeshOnly(null), "no config: the old behaviour");

                MeshStateFallback[] code = fresh.PlayerMeshFallbacks;
                MeshStateFallback[] asset = Shipped().PlayerMeshFallbacks;
                Assert.AreEqual(code.Length, asset.Length, "the shipped map repeats the code's rows");
                for (int i = 0; i < code.Length; i++)
                {
                    Assert.AreEqual(code[i].From, asset[i].From, "row " + i);
                    Assert.AreEqual(code[i].To, asset[i].To, "row " + i);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(fresh);
            }
        }

        // ---------------------------------------------------------------- the guard

        [TestCaseSource(nameof(Presets))]
        public void EveryPlayerState_ResolvesToABakedMeshClip_OnEveryBody(string preset)
        {
            CharacterSkinDef skin = Skin(preset);
            MeshStateFallback[] map = Shipped().PlayerMeshFallbacks;
            int asked = 0;
            foreach (string wanted in PlayerWantedKeys(skin))
            {
                if (wanted == null) continue;
                asked++;
                Assert.IsTrue(PlayerMeshRule.TryResolve(skin, wanted, map, out string resolved, out _),
                              $"{preset}: '{wanted}' resolves to no mesh clip");
                Assert.IsTrue(PlayerMeshRule.Carries(skin, resolved),
                              $"{preset}: '{wanted}' resolved to '{resolved}', which the skin cannot pose");
            }
            Assert.Greater(asked, 100, "the guard must actually walk the player's states");
        }

        [TestCaseSource(nameof(Presets))]
        public void TheMovesThatUsedToTakeTheSprite_NowPlayTheirOwnMeshClip(string preset)
        {
            CharacterSkinDef skin = Skin(preset);
            MeshStateFallback[] map = Shipped().PlayerMeshFallbacks;
            var own = new (CharacterClip clip, string key)[]
            {
                (CharacterClip.Swim, "swim"),
                (CharacterClip.Tread, "tread"),
                (CharacterClip.Sleep, "sleep"),
                (CharacterClip.Drive, "drive"),
            };
            foreach (var (clip, key) in own)
            {
                Assert.AreEqual(key, PlayerMeshRule.WantedKeyFor(clip));
                Assert.IsTrue(PlayerMeshRule.TryResolve(skin, key, map, out string resolved, out bool fellBack));
                Assert.AreEqual(key, resolved, $"{preset}: {clip} must draw its own baked clip");
                Assert.IsFalse(fellBack, $"{preset}: {clip}");
            }
            foreach (CharacterClip reach in new[] { CharacterClip.ReachGround, CharacterClip.ReachStowV, CharacterClip.ReachStowH })
            {
                Assert.IsTrue(PlayerMeshRule.TryResolve(skin, PlayerMeshRule.WantedKeyFor(reach), map,
                                                        out string resolved, out bool fellBack), reach.ToString());
                Assert.AreEqual("reach", resolved, $"{preset}: {reach} takes the one baked reach");
                Assert.IsTrue(fellBack);
            }
            // The carried locomotion the cast list left out: his own carry clips, not the bare walk.
            foreach (string carryKey in new[] { "idle_tray", "walk_tray", "run_tray", "idle_pot", "walk_pot" })
            {
                Assert.IsTrue(PlayerMeshRule.TryResolve(skin, carryKey, map, out string resolved, out _), carryKey);
                Assert.AreEqual(carryKey, resolved, $"{preset}: {carryKey}");
            }
        }

        [Test]
        public void WithTheLegacySwitchOff_NothingAshoreHandsHimBack()
        {
            foreach (OnFootWaterState water in Enum.GetValues(typeof(OnFootWaterState)))
            foreach (bool clip in new[] { false, true })
            foreach (bool suspended in new[] { false, true })
            foreach (bool clipMesh in new[] { false, true })
                Assert.IsNull(DeckRiderMeshPresenter.WhyNotShownAshore(true, true, true, true, clip, water,
                                  suspended, clipMesh, spriteFallback: false),
                              $"water {water}, clip {clip}, suspended {suspended}, clip as mesh {clipMesh}");

            // The wiring fences and "inside something" are not sprite fallbacks and still stand.
            Assert.IsNotNull(DeckRiderMeshPresenter.WhyNotShownAshore(true, true, true, false, false,
                                 OnFootWaterState.Dry, spriteFallback: false), "inside a cab she draws neither");
            Assert.IsNotNull(DeckRiderMeshPresenter.WhyNotShownAshore(false, true, true, true, false,
                                 OnFootWaterState.Dry, spriteFallback: false), "a disabled rider poses nothing");

            // Left out, the old hand-over holds, word for word.
            Assert.IsNotNull(DeckRiderMeshPresenter.WhyNotShownAshore(true, true, true, true, false,
                                 OnFootWaterState.Swim));
        }

        // ---------------------------------------------------------------- the rule itself

        [Test]
        public void WantedKeys_KeepEveryMappedMove_AndNameTheBakedWaterSleepAndDrive()
        {
            foreach (CharacterClip clip in Enum.GetValues(typeof(CharacterClip)))
            foreach (string carry in Carries)
            {
                string mapped = CharacterClipStateMap.StateKeyFor(clip, carry);
                if (mapped != null) Assert.AreEqual(mapped, PlayerMeshRule.WantedKeyFor(clip, carry), clip + "/" + carry);
            }
            Assert.IsNull(PlayerMeshRule.WantedKeyFor(CharacterClip.None));
            Assert.IsNull(PlayerMeshRule.WantedKeyFor((CharacterClip)(-1)));
            Assert.AreEqual("reachGround", PlayerMeshRule.WantedKeyFor(CharacterClip.ReachGround));
            Assert.AreEqual("reachStowV", PlayerMeshRule.WantedKeyFor(CharacterClip.ReachStowV));
            Assert.AreEqual("reachStowH", PlayerMeshRule.WantedKeyFor(CharacterClip.ReachStowH));
        }

        [Test]
        public void TheMap_IsFollowedRowByRow_EndsAtIdle_AndSurvivesACycle()
        {
            CharacterSkinDef skin = Skin("fisher");
            var chain = new[]
            {
                new MeshStateFallback("no_such_a", "no_such_b"),
                new MeshStateFallback("no_such_b", "walk"),
            };
            Assert.IsTrue(PlayerMeshRule.TryResolve(skin, "no_such_a", chain, out string viaChain, out bool fell));
            Assert.AreEqual("walk", viaChain, "two hops down the map");
            Assert.IsTrue(fell);

            var cycle = new[]
            {
                new MeshStateFallback("no_such_a", "no_such_b"),
                new MeshStateFallback("no_such_b", "no_such_a"),
            };
            Assert.IsTrue(PlayerMeshRule.TryResolve(skin, "no_such_a", cycle, out string viaCycle, out _));
            Assert.AreEqual(CharacterSkinStateMap.Idle, viaCycle, "a cycle in the data ends at idle");

            Assert.IsTrue(PlayerMeshRule.TryResolve(skin, "no_such_state", null, out string bare, out _));
            Assert.AreEqual(CharacterSkinStateMap.Idle, bare, "no map: idle");

            Assert.IsTrue(PlayerMeshRule.TryResolve(skin, "walk", null, out string itself, out bool none));
            Assert.AreEqual("walk", itself);
            Assert.IsFalse(none, "a carried state is not a fallback");

            Assert.IsFalse(PlayerMeshRule.TryResolve(null, "walk", null, out _, out _), "no skin: nothing to draw");
        }

        [Test]
        public void AForeignFrame_WrapsOnALoop_AndHoldsOnAOneShot()
        {
            var loop = new CharacterSkinDef.SkinClip { FrameCount = 6, Loop = true };
            var once = new CharacterSkinDef.SkinClip { FrameCount = 6, Loop = false };
            Assert.AreEqual(1, PlayerMeshRule.FrameIn(loop, 7));
            Assert.AreEqual(5, PlayerMeshRule.FrameIn(once, 7));
            Assert.AreEqual(0, PlayerMeshRule.FrameIn(loop, -3));
            Assert.AreEqual(0, PlayerMeshRule.FrameIn(new CharacterSkinDef.SkinClip(), 4), "an empty clip draws frame 0");
        }
    }
}
