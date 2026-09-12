using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>THE MESH READS THE SPRITE'S INPUTS, AND ONLY THE SPRITE'S INPUTS.</b>
    ///
    /// <para><c>DeckRiderVisual</c> is the single authority on what the player is doing; it hands
    /// <c>(stance, gait)</c> to <c>IsoCharacterSprite</c> and the mesh presenter takes the SAME
    /// pair at the SAME seam. That is why this mapping is a pure function and lives in Core with no
    /// reference to a boat, an input stack or a clock — if the mesh and the sheets ever disagree about
    /// what she is doing, the bug must be provably upstream of both.</para>
    ///
    /// <para><b>The fallback is the interesting half.</b> Rig 7's clip table is the ANIMS rows, and of
    /// the four stances only <c>balance</c> is one of them: there is no <c>helm</c> clip and no
    /// <c>oars</c> clip. Helm and Oars therefore draw the FREE BODY — and the player's sprite does NOT,
    /// because <c>FisherIso.asset</c> carries a helm and an oars sheet of its own. The mesh is a step
    /// BACK on those two, a stated debt rather than an inherited gap.</para>
    ///
    /// <para>Note which of the tests below can and cannot see that. <c>fellBack</c> catches a def the
    /// BAKE left short; helm maps to the gait key before the def is consulted, so nothing falls back
    /// and nothing reports. An ANIMS row is the only fix, and no flag here substitutes for it.</para>
    /// </summary>
    public sealed class CharacterSkinStateMapTests
    {
        private CharacterSkinDef _def;

        [TearDown]
        public void TearDown()
        {
            if (_def != null) Object.DestroyImmediate(_def);
            _def = null;
        }

        private CharacterSkinDef DefWith(params string[] stateKeys)
        {
            _def = ScriptableObject.CreateInstance<CharacterSkinDef>();
            var clips = new CharacterSkinDef.SkinClip[stateKeys.Length];
            for (int i = 0; i < stateKeys.Length; i++)
                clips[i] = new CharacterSkinDef.SkinClip
                {
                    Anim = stateKeys[i],
                    State = stateKeys[i],
                    FramesPerSecond = 12f,
                    FrameCount = 1,
                    Keys = new[]
                    {
                        new CharacterSkinDef.BoneKey
                        {
                            Position = Vector3.zero, Rotation = Quaternion.identity,
                        },
                    },
                };
            _def.Clips = clips;
            return _def;
        }

        // ------------------------------------------------------------------ the plain mapping

        [Test]
        public void TheGaitPicksTheFreeBodyClip()
        {
            Assert.AreEqual(CharacterSkinStateMap.Idle,
                            CharacterSkinStateMap.StateKeyFor(CharacterStance.Free, CharacterGait.Idle));
            Assert.AreEqual(CharacterSkinStateMap.Walk,
                            CharacterSkinStateMap.StateKeyFor(CharacterStance.Free, CharacterGait.Walk));
            Assert.AreEqual(CharacterSkinStateMap.Run,
                            CharacterSkinStateMap.StateKeyFor(CharacterStance.Free, CharacterGait.Run));
        }

        [Test]
        public void TheOneBakedStanceWinsOverTheGait()
        {
            // `balance` is the only stance rig 7 exports as a clip of its own. DeckRiderVisual does
            // not ask for it while she is CROSSING the deck, so this mapping never has to choose
            // between bracing and travelling — it only has to honour the request it was handed.
            Assert.AreEqual(CharacterSkinStateMap.Balance,
                            CharacterSkinStateMap.StateKeyFor(CharacterStance.Balance, CharacterGait.Idle));
        }

        [Test]
        public void TheUnbakedStancesAskForTheirGaitClip()
        {
            Assert.AreEqual(CharacterSkinStateMap.Idle,
                            CharacterSkinStateMap.StateKeyFor(CharacterStance.Helm, CharacterGait.Idle));
            Assert.AreEqual(CharacterSkinStateMap.Walk,
                            CharacterSkinStateMap.StateKeyFor(CharacterStance.Oars, CharacterGait.Walk));
        }

        // ------------------------------------------------------------------ the fallback chain

        [Test]
        public void AStanceTheBakeCarriesResolvesWithoutFallingBack()
        {
            CharacterSkinDef def = DefWith("idle", "walk", "run", "balance");

            Assert.IsTrue(CharacterSkinStateMap.Resolve(def, CharacterStance.Balance, CharacterGait.Idle,
                                                        out string key, out bool fellBack));
            Assert.AreEqual("balance", key);
            Assert.IsFalse(fellBack, "the clip was there — nothing should have been reported as missing");
        }

        [Test]
        public void AnUnbakedStanceAsksForItsGaitClipAndThatIsNotAFallback()
        {
            CharacterSkinDef def = DefWith("idle", "walk", "run", "balance");

            Assert.IsTrue(CharacterSkinStateMap.Resolve(def, CharacterStance.Helm, CharacterGait.Walk,
                                                        out string key, out bool fellBack));
            Assert.AreEqual("walk", key, "helm is not an exported clip, so she must walk the free body");
            Assert.IsFalse(fellBack,
                "helm ALREADY maps to the gait key before the def is consulted — the def carried the " +
                "clip that was asked for, so nothing fell back");
        }

        [Test]
        public void AGaitTheBakeForgotFallsBackToIdleAndSaysSo()
        {
            CharacterSkinDef def = DefWith("idle");

            Assert.IsTrue(CharacterSkinStateMap.Resolve(def, CharacterStance.Free, CharacterGait.Run,
                                                        out string key, out bool fellBack));
            Assert.AreEqual("idle", key);
            Assert.IsTrue(fellBack,
                "a silent idle standing in for a missing run clip is the hardest art gap to find — " +
                "the caller must be told");
        }

        [Test]
        public void AMissingStanceClipFallsThroughTheGaitToIdle()
        {
            // `balance` is asked for, the def has neither balance nor the walk it would fall to.
            CharacterSkinDef def = DefWith("idle");

            Assert.IsTrue(CharacterSkinStateMap.Resolve(def, CharacterStance.Balance, CharacterGait.Walk,
                                                        out string key, out bool fellBack));
            Assert.AreEqual("idle", key);
            Assert.IsTrue(fellBack);
        }

        [Test]
        public void ADefWithNothingToDrawResolvesToNothingRatherThanAnEmptyKey()
        {
            CharacterSkinDef def = DefWith();

            Assert.IsFalse(CharacterSkinStateMap.Resolve(def, CharacterStance.Free, CharacterGait.Idle,
                                                         out string key, out bool fellBack));
            Assert.IsNull(key,
                "an unresolved state must come back NULL — an empty string would sail through " +
                "TryGetClip's ordinal compare and pose whatever clip happened to be first");
            Assert.IsTrue(fellBack);
        }

        [Test]
        public void ANullDefIsNotAnException()
        {
            Assert.IsFalse(CharacterSkinStateMap.Resolve(null, CharacterStance.Free, CharacterGait.Idle,
                                                         out string key, out bool fellBack));
            Assert.IsNull(key);
            Assert.IsFalse(fellBack);
        }
    }
}
