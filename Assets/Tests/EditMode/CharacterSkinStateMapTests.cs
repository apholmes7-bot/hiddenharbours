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
    /// <para><b>Helm and Oars ask for the rig's own clips</b> (character PR 2a). Rig 9 bakes
    /// <c>idle_helm</c>, <c>walk_helm</c>, <c>idle_oars</c> and <c>walk_oars</c>, so the map asks for them
    /// and a def that lacks one — every rig 7 def — falls back to the free gait and SAYS so through
    /// <c>fellBack</c>. Rig 7's debt (the free body at the wheel while the sheet had a helm pose) is
    /// therefore paid, and visible where it is not. A run at either stance asks for the free <c>run</c> by
    /// design: no stance bakes a run on the rig or on the sprite (<c>CharacterVisualDef.SheetFor</c> gives a
    /// stance's run an empty sheet), so that is the sprite's own answer and not a fallback.</para>
    ///
    /// <para>The rig side of this — that these spellings are the rig's clip table and the ten committed
    /// defs resolve them — is <c>CharacterSkinCarryStateTests</c>; this file pins the map itself.</para>
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

        // What a rig 9 def carries for the stances, and what a rig 7 def carried.
        private static readonly string[] Rig9States =
            { "idle", "walk", "run", "balance", "idle_helm", "walk_helm", "idle_oars", "walk_oars" };

        private static readonly string[] Rig7States = { "idle", "walk", "run", "balance" };

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
        public void TheBalanceStanceWinsOverTheGait()
        {
            // DeckRiderVisual does not ask for balance while she is CROSSING the deck, so this mapping
            // never has to choose between bracing and travelling — it only honours the request.
            Assert.AreEqual(CharacterSkinStateMap.Balance,
                            CharacterSkinStateMap.StateKeyFor(CharacterStance.Balance, CharacterGait.Idle));
            Assert.AreEqual(CharacterSkinStateMap.Balance,
                            CharacterSkinStateMap.StateKeyFor(CharacterStance.Balance, CharacterGait.Walk));
        }

        [Test]
        public void TheWheelAndTheOarsAskForTheRigsCarryClips()
        {
            Assert.AreEqual("idle_helm", CharacterSkinStateMap.StateKeyFor(CharacterStance.Helm, CharacterGait.Idle));
            Assert.AreEqual("walk_helm", CharacterSkinStateMap.StateKeyFor(CharacterStance.Helm, CharacterGait.Walk));
            Assert.AreEqual("idle_oars", CharacterSkinStateMap.StateKeyFor(CharacterStance.Oars, CharacterGait.Idle));
            Assert.AreEqual("walk_oars", CharacterSkinStateMap.StateKeyFor(CharacterStance.Oars, CharacterGait.Walk));
        }

        [Test]
        public void ARunAtTheWheelOrTheOarsIsTheFreeRunAsOnTheSprite()
        {
            Assert.AreEqual(CharacterSkinStateMap.Run,
                            CharacterSkinStateMap.StateKeyFor(CharacterStance.Helm, CharacterGait.Run));
            Assert.AreEqual(CharacterSkinStateMap.Run,
                            CharacterSkinStateMap.StateKeyFor(CharacterStance.Oars, CharacterGait.Run));
        }

        [Test]
        public void ACarryIsTheAnimUnderscoreTheCarry()
        {
            Assert.AreEqual("walk_buckets", CharacterSkinStateMap.CarryKey("walk", "buckets"));
            Assert.AreEqual("walk", CharacterSkinStateMap.CarryKey("walk", null));
            Assert.AreEqual("walk", CharacterSkinStateMap.CarryKey("walk", string.Empty));

            Assert.AreEqual("idle_buckets",
                            CharacterSkinStateMap.StateKeyFor(CharacterStance.Free, CharacterGait.Idle, "buckets"));
            Assert.AreEqual("walk_buckets",
                            CharacterSkinStateMap.StateKeyFor(CharacterStance.Free, CharacterGait.Walk, "buckets"));
            Assert.AreEqual("run_buckets",
                            CharacterSkinStateMap.StateKeyFor(CharacterStance.Free, CharacterGait.Run, "buckets"));
            Assert.AreEqual("walk",
                            CharacterSkinStateMap.StateKeyFor(CharacterStance.Free, CharacterGait.Walk, null));
        }

        [Test]
        public void AStanceWinsOverACarry()
        {
            // The rig ignores a carry on a stance clip; so does the map.
            Assert.AreEqual("walk_helm",
                            CharacterSkinStateMap.StateKeyFor(CharacterStance.Helm, CharacterGait.Walk, "buckets"));
            Assert.AreEqual("idle_oars",
                            CharacterSkinStateMap.StateKeyFor(CharacterStance.Oars, CharacterGait.Idle, "buckets"));
            Assert.AreEqual(CharacterSkinStateMap.Balance,
                            CharacterSkinStateMap.StateKeyFor(CharacterStance.Balance, CharacterGait.Walk, "buckets"));
        }

        [Test]
        public void ACarryKeyIsBuiltOnceAndReused()
        {
            // Rule 7: a presenter resolves every frame, so the same carry must hand back the same string,
            // not a new concatenation per figure per frame.
            string first = CharacterSkinStateMap.StateKeyFor(CharacterStance.Free, CharacterGait.Walk, "buckets");
            string again = CharacterSkinStateMap.StateKeyFor(CharacterStance.Free, CharacterGait.Walk, "buckets");
            Assert.AreSame(first, again, "the carry key must be memoised, not rebuilt");
            Assert.AreSame(CharacterSkinStateMap.StateKeyFor(CharacterStance.Helm, CharacterGait.Idle),
                           CharacterSkinStateMap.StateKeyFor(CharacterStance.Helm, CharacterGait.Idle),
                           "the helm key must be memoised, not rebuilt");
        }

        // ------------------------------------------------------------------ the fallback chain

        [Test]
        public void AStanceTheBakeCarriesResolvesWithoutFallingBack()
        {
            CharacterSkinDef def = DefWith(Rig9States);

            Assert.IsTrue(CharacterSkinStateMap.Resolve(def, CharacterStance.Balance, CharacterGait.Idle,
                                                        out string key, out bool fellBack));
            Assert.AreEqual("balance", key);
            Assert.IsFalse(fellBack, "the clip was there — nothing should have been reported as missing");

            Assert.IsTrue(CharacterSkinStateMap.Resolve(def, CharacterStance.Oars, CharacterGait.Walk,
                                                        out key, out fellBack));
            Assert.AreEqual("walk_oars", key);
            Assert.IsFalse(fellBack, "rig 9 bakes walk_oars, so rowing is its own clip");
        }

        [Test]
        public void ARunAtTheWheelResolvesToTheFreeRunWithoutFallingBack()
        {
            CharacterSkinDef def = DefWith(Rig9States);

            Assert.IsTrue(CharacterSkinStateMap.Resolve(def, CharacterStance.Helm, CharacterGait.Run,
                                                        out string key, out bool fellBack));
            Assert.AreEqual("run", key);
            Assert.IsFalse(fellBack,
                "the free run is what the map ASKED for (no stance bakes a run), so nothing fell back");
        }

        [Test]
        public void ADefWithoutTheWheelClipFallsBackToTheGaitAndSaysSo()
        {
            CharacterSkinDef def = DefWith(Rig7States);

            Assert.IsTrue(CharacterSkinStateMap.Resolve(def, CharacterStance.Helm, CharacterGait.Walk,
                                                        out string key, out bool fellBack));
            Assert.AreEqual("walk", key, "a def with no walk_helm walks the free body");
            Assert.IsTrue(fellBack,
                "walk_helm was asked for and the def lacks it — the caller must be told, not left to " +
                "draw the free body as if it were the wheel");

            Assert.IsTrue(CharacterSkinStateMap.Resolve(def, CharacterStance.Oars, CharacterGait.Idle,
                                                        out key, out fellBack));
            Assert.AreEqual("idle", key);
            Assert.IsTrue(fellBack);
        }

        [Test]
        public void ACarryTheBakeLacksFallsBackToTheFreeGaitAndSaysSo()
        {
            CharacterSkinDef def = DefWith("idle", "walk", "run", "idle_buckets");

            Assert.IsTrue(CharacterSkinStateMap.Resolve(def, CharacterStance.Free, CharacterGait.Idle, "buckets",
                                                        out string key, out bool fellBack));
            Assert.AreEqual("idle_buckets", key);
            Assert.IsFalse(fellBack);

            Assert.IsTrue(CharacterSkinStateMap.Resolve(def, CharacterStance.Free, CharacterGait.Walk, "buckets",
                                                        out key, out fellBack));
            Assert.AreEqual("walk", key, "no walk_buckets, so she walks the free body");
            Assert.IsTrue(fellBack);
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
