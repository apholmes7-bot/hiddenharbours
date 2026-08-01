using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.World;

namespace HiddenHarbours.Tests.World.EditMode
{
    /// <summary>
    /// Which lines a <see cref="DialogueDef"/> hands over — the first/repeat split, and the flag-gated
    /// CONDITIONAL beat that lets a speaker acknowledge something that has actually happened to this
    /// player (Aunt Ginny mentioning the clam-licence fee she fronted, plan-to-m1 §7.5).
    ///
    /// <para>The load-bearing case is the negative one. A line about a debt is only true once the debt
    /// exists, and every other test here would still pass if the gate leaked — the conversation would
    /// simply have one more line in it, from the very first "hello", about money the player was never
    /// given. So the gate is asserted CLOSED under every way of not having the flag: no key authored, no
    /// store to ask, and a store that says no.</para>
    /// </summary>
    public class DialogueDefLinesTests
    {
        private DialogueDef _def;

        private const string Flag = "ginny_fronted_clam_fee";

        [SetUp]
        public void SetUp()
        {
            _def = ScriptableObject.CreateInstance<DialogueDef>();
            _def.Id = "dialogue.test";
            _def.FirstLines = new[] { "first a", "first b" };
            _def.RepeatLines = new[] { "again" };
            _def.ConditionalFlag = Flag;
            _def.ConditionalLines = new[] { "and the fee" };
        }

        [TearDown]
        public void TearDown()
        {
            if (_def != null) Object.DestroyImmediate(_def);
        }

        private static IFlagStore StoreWith(params string[] setKeys)
        {
            var store = new InMemoryFlagStore();
            foreach (string k in setKeys) store.Set(k, true);
            return store;
        }

        // ---- the pools, unchanged ------------------------------------------------------------

        [Test]
        public void FirstMeetingPlaysFirstLines_AndAReGreetPlaysRepeatLines()
        {
            CollectionAssert.AreEqual(new[] { "first a", "first b" }, _def.Lines(metBefore: false));
            CollectionAssert.AreEqual(new[] { "again" }, _def.Lines(metBefore: true));
        }

        [Test]
        public void EmptyRepeatLines_FallBackToFirstLines()
        {
            _def.RepeatLines = new string[0];
            CollectionAssert.AreEqual(new[] { "first a", "first b" }, _def.Lines(metBefore: true));
        }

        [Test]
        public void TheFlaglessOverloadIsUntouched_SoEveryOlderCallerBehavesExactlyAsBefore()
        {
            // Lines(bool) is the signature every pre-existing call site uses. It must never consult the
            // condition — there is no store to consult — so adding a conditional block to an asset can
            // never change what an older caller renders.
            CollectionAssert.AreEqual(new[] { "first a", "first b" }, _def.Lines(false));
            CollectionAssert.AreEqual(new[] { "again" }, _def.Lines(true));
        }

        // ---- the conditional beat ------------------------------------------------------------

        [Test]
        public void WithTheFlagSet_TheConditionalLinesAreAppendedToWhicheverPoolPlays()
        {
            IFlagStore flags = StoreWith(Flag);

            CollectionAssert.AreEqual(new[] { "first a", "first b", "and the fee" },
                _def.Lines(metBefore: false, flags: flags),
                "the extra beat is said as WELL as the opening, not instead of it");

            CollectionAssert.AreEqual(new[] { "again", "and the fee" },
                _def.Lines(metBefore: true, flags: flags),
                "and it keeps being said on the re-greet — a debt somebody is remembering is the point");
        }

        [Test]
        public void WithoutTheFlag_NotOneExtraLineIsSaid()
        {
            CollectionAssert.AreEqual(new[] { "first a", "first b" },
                _def.Lines(metBefore: false, flags: StoreWith()),
                "an unset flag means the thing has not happened, and she must not mention it");

            CollectionAssert.AreEqual(new[] { "first a", "first b" },
                _def.Lines(metBefore: false, flags: StoreWith("some_other_flag")),
                "and another flag being set is not this flag being set");

            CollectionAssert.AreEqual(new[] { "first a", "first b" },
                _def.Lines(metBefore: false, flags: null),
                "no store to ask (EditMode / pre-boot) fails CLOSED — a missing line beats a false one");
        }

        [Test]
        public void AnAssetWithNoConditionAuthored_NeverPlaysConditionalLines()
        {
            _def.ConditionalFlag = "";
            CollectionAssert.AreEqual(new[] { "first a", "first b" },
                _def.Lines(metBefore: false, flags: StoreWith(Flag, "")),
                "no key authored is no condition — the lines below it are unreachable, not always-on");

            Assert.IsFalse(_def.ConditionMet(StoreWith(Flag)));
        }

        [Test]
        public void AnEmptyConditionalBlock_ChangesNothing_EvenWithTheFlagSet()
        {
            _def.ConditionalLines = new string[0];
            CollectionAssert.AreEqual(new[] { "first a", "first b" },
                _def.Lines(metBefore: false, flags: StoreWith(Flag)));
        }

        [Test]
        public void TheGateIsReadEveryTime_SoTheLineAppearsTheMomentTheFlagDoes()
        {
            // The player talks to her, walks off, gets the fee, comes back. Nothing is cached at author
            // time or at first play: the same asset answers differently because the world changed.
            var store = new InMemoryFlagStore();
            Assert.AreEqual(2, _def.Lines(metBefore: false, flags: store).Length);

            store.Set(Flag, true);
            Assert.AreEqual(3, _def.Lines(metBefore: false, flags: store).Length);

            store.Set(Flag, false);
            Assert.AreEqual(2, _def.Lines(metBefore: false, flags: store).Length);
        }
    }
}
