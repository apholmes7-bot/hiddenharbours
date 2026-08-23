using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>THE SLOT TRUTH TABLE</b> — the whole of "she has two hands, and this is what may share them",
    /// pinned as a table rather than as a list of anecdotes.
    ///
    /// <para>The claims, in the order they would break:
    /// <list type="number">
    ///   <item><b>THE OWNER'S TWO PAIRINGS.</b> A fish in one hand and the rod in the other. A pail in one
    ///   hand and the rod in the other. Neither was possible with one slot.</item>
    ///   <item><b>THE ONE NOBODY NAMED.</b> A pail in one hand and a fish in the other — without which a
    ///   caught fish could never get into a CARRIED pail, and the whole feature would be a picture with no
    ///   loop behind it.</item>
    ///   <item><b>ONE OF EACH.</b> One working tool, one pail, one catch. Every "at a time" rule the game
    ///   already had survives the second slot; what changed is which pairs, not how many of a kind.</item>
    ///   <item><b>THE SIZE RULE.</b> A fish past the owner's length dial is cradled, so it takes BOTH
    ///   hands — and will not displace what is already held to get them.</item>
    ///   <item><b>⭐ THE CONTINUITY LAW.</b> A refusal changes NOTHING, and taking something up never
    ///   moves what is already held. Both halves, because a half-applied refusal is the failure this rule
    ///   exists to prevent and it is invisible from the return value alone.</item>
    /// </list></para>
    /// </summary>
    public class HandSlotsTests
    {
        // A carriable with no scene, no renderer and no transform work — the rule under test touches none
        // of those, and a MonoBehaviour here would drag EditMode's "no Awake" problem into a pure table.
        private sealed class Thing : ICarriable, IHandLoad
        {
            public Thing(HandLoad load, string defId = "thing") { HandLoad = load; DefId = defId; }
            public HandLoad HandLoad { get; }
            public string DefId { get; }
            public bool IsCarriable => true;
            public Transform Transform => null;
            public int BakedFacings => 0;
            public void ShowFacing(int facingIndex) { }
            public void RideSortingBand(int layer, int order) { }
            public void OnLifted(ICarrier carrier) { }
            public void OnPlaced() { }
        }

        private static Thing Rod() => new Thing(HandLoad.Tool, "tool.rod");
        private static Thing Shovel() => new Thing(HandLoad.Tool, "tool.shovel");
        private static Thing Pail() => new Thing(HandLoad.Container, "container.bucket");
        private static Thing Fish() => new Thing(HandLoad.Catch, "fish.atlantic_cod");

        private static HandSlots Holding(params Thing[] things)
        {
            var slots = new HandSlots();
            foreach (Thing t in things)
                Assert.That(slots.TryTake(t, needsBothHands: false, CarryHandSide.Right, out _),
                            Is.EqualTo(HandSlotRefusal.None), $"fixture could not hold {t.DefId}");
            return slots;
        }

        // ---- 1 + 2 + 3. THE TABLE --------------------------------------------------------------------

        [Test]
        public void TheSharingRule_IsTheWholeTruthTable_DifferentKindsShare_SameKindNever()
        {
            // Read this as the table it is. Rows and columns are the three kinds a hand can hold; the
            // diagonal is false because every "one at a time" rule in the game lives on it.
            var kinds = new[] { HandLoad.Tool, HandLoad.Container, HandLoad.Catch };
            foreach (HandLoad a in kinds)
                foreach (HandLoad b in kinds)
                    Assert.That(HandSlots.MayShare(a, b), Is.EqualTo(a != b),
                                $"{a} + {b}: different kinds share the pair, the same kind never does");

            // An empty hand is not an occupant, so it shares with nothing — a caller asking otherwise has
            // asked the wrong question and must not get a "yes".
            foreach (HandLoad k in kinds)
            {
                Assert.That(HandSlots.MayShare(HandLoad.Empty, k), Is.False);
                Assert.That(HandSlots.MayShare(k, HandLoad.Empty), Is.False);
            }
        }

        [Test]
        public void AFishGoesInOneHand_AndTheRodStaysInTheOther()
        {
            HandSlots slots = Holding(Rod());
            Thing fish = Fish();

            Assert.That(slots.TryTake(fish, needsBothHands: false, CarryHandSide.Right,
                                      out CarryHandSide side),
                        Is.EqualTo(HandSlotRefusal.None), "the owner's first pairing");

            Assert.That(slots.UsedHands, Is.EqualTo(2));
            Assert.That(side, Is.EqualTo(CarryHandSide.Left),
                        "the rod had the right hand, so the fish takes the free one");
            Assert.That(slots.Holds(HandLoad.Tool), Is.True, "the rod is still held");
            Assert.That(slots.Holds(HandLoad.Catch), Is.True);
        }

        [Test]
        public void APailGoesInOneHand_AndTheRodStaysInTheOther()
        {
            HandSlots slots = Holding(Rod());
            Assert.That(slots.TryTake(Pail(), false, CarryHandSide.Right, out _),
                        Is.EqualTo(HandSlotRefusal.None), "the owner's second pairing");
            Assert.That(slots.UsedHands, Is.EqualTo(2));
        }

        [Test]
        public void APailAndAFish_ShareToo_OrTheCarriedPailCouldNeverBeFilled()
        {
            // ⚠️ Nobody named this pairing, and without it the feature is a picture with no loop: you
            // carry the pail, you land a fish, and there is nowhere for the fish to be while you press E.
            HandSlots slots = Holding(Pail());
            Assert.That(slots.TryTake(Fish(), false, CarryHandSide.Right, out _),
                        Is.EqualTo(HandSlotRefusal.None));
            Assert.That(slots.UsedHands, Is.EqualTo(2));
        }

        [Test]
        public void OneOfEachKind_ASecondRodPailOrFishIsRefusedByNAME()
        {
            // The refusal MATTERS as much as the fact: "you've got one of those already" is a different
            // sentence from "your hands are full", and with a free hand the second one is the true one.
            Assert.That(Holding(Rod()).TryTake(Shovel(), false, CarryHandSide.Right, out _),
                        Is.EqualTo(HandSlotRefusal.OneOfThoseAtATime), "one working tool at a time");
            Assert.That(Holding(Pail()).TryTake(Pail(), false, CarryHandSide.Right, out _),
                        Is.EqualTo(HandSlotRefusal.OneOfThoseAtATime), "one pail at a time");
            Assert.That(Holding(Fish()).TryTake(Fish(), false, CarryHandSide.Right, out _),
                        Is.EqualTo(HandSlotRefusal.OneOfThoseAtATime),
                        "one catch at a time — full is full, the over-encumbrance rule unchanged");
        }

        [Test]
        public void ThreeThingsDoNotFit_AndTheRefusalSaysWhy()
        {
            HandSlots slots = Holding(Rod(), Pail());
            Assert.That(slots.UsedHands, Is.EqualTo(2));
            Assert.That(slots.TryTake(Fish(), false, CarryHandSide.Right, out _),
                        Is.EqualTo(HandSlotRefusal.HandsFull), "two hands, not three");
        }

        [Test]
        public void ThePreferredHandIsHonouredWhenFree_AndIgnoredWhenNot()
        {
            var slots = new HandSlots();
            Assert.That(slots.TryTake(Rod(), false, CarryHandSide.Left, out CarryHandSide first),
                        Is.EqualTo(HandSlotRefusal.None));
            Assert.That(first, Is.EqualTo(CarryHandSide.Left), "the rig asked for the left hand");

            Assert.That(slots.TryTake(Fish(), false, CarryHandSide.Left, out CarryHandSide second),
                        Is.EqualTo(HandSlotRefusal.None));
            Assert.That(second, Is.EqualTo(CarryHandSide.Right),
                        "it asked for the left too — but the left is taken and nothing already held moves");
        }

        [Test]
        public void NoPreference_FallsToTheRightHand_TheRigsOwnDefault()
        {
            foreach (CarryHandSide none in new[] { CarryHandSide.None, CarryHandSide.Both })
            {
                var slots = new HandSlots();
                Assert.That(slots.TryTake(Rod(), false, none, out CarryHandSide side),
                            Is.EqualTo(HandSlotRefusal.None));
                Assert.That(side, Is.EqualTo(CarryHandSide.Right), $"preference {none}");
            }
        }

        // ---- 4. THE SIZE RULE -------------------------------------------------------------------------

        [Test]
        public void TheSizeRule_IsALength_AndTheThresholdIsInclusive()
        {
            const float max = 0.62f;
            Assert.That(HandSlots.NeedsBothHands(0.45f, max), Is.False, "a mackerel hangs from a fist");
            Assert.That(HandSlots.NeedsBothHands(max, max), Is.False,
                        "exactly at the dial is still ONE hand — 'up to' includes the number");
            Assert.That(HandSlots.NeedsBothHands(max + 0.001f, max), Is.True, "a hair over needs both");
            Assert.That(HandSlots.NeedsBothHands(1.09f, max), Is.True, "a twelve-kilo cod is a cradle");
        }

        [Test]
        public void AnUnwiredThreshold_FailsOPEN_SoNoFishEverJamsTheHands()
        {
            // The tide gate's and the walkability read's rule, applied here: a service that is not
            // installed must not be the thing that stops the game. A zero dial means "no fish needs both
            // hands", never "every fish does".
            Assert.That(HandSlots.NeedsBothHands(9f, 0f), Is.False);
            Assert.That(HandSlots.NeedsBothHands(9f, -1f), Is.False);
        }

        [Test]
        public void AnOverSizeFish_RefusesAHand_WhileAnythingAtAllIsHeld()
        {
            foreach (Thing occupant in new Thing[] { Rod(), Pail() })
            {
                HandSlots slots = Holding(occupant);
                Assert.That(slots.TryTake(Fish(), needsBothHands: true, CarryHandSide.Right, out _),
                            Is.EqualTo(HandSlotRefusal.NeedsBothHands),
                            $"a cradled fish will not displace the {occupant.DefId} to make room");
                Assert.That(slots.UsedHands, Is.EqualTo(1), "and nothing moved");
            }
        }

        [Test]
        public void AnOverSizeFish_TakesBOTHHands_WhenSheIsEmptyHanded()
        {
            var slots = new HandSlots();
            Thing cod = Fish();

            Assert.That(slots.TryTake(cod, needsBothHands: true, CarryHandSide.Right,
                                      out CarryHandSide side),
                        Is.EqualTo(HandSlotRefusal.None));

            Assert.That(side, Is.EqualTo(CarryHandSide.Both));
            Assert.That(slots.SpansBoth, Is.True);
            Assert.That(slots.UsedHands, Is.EqualTo(2), "a cradle commits the pair");
            Assert.That(slots.Left, Is.SameAs(cod));
            Assert.That(slots.Right, Is.SameAs(cod), "one object, both hands — not two occupants");
            Assert.That(slots.SideOf(cod), Is.EqualTo(CarryHandSide.Both));
        }

        [Test]
        public void NothingJoinsACradle_AndTheRefusalReadsFULL_NotOneOfThose()
        {
            var slots = new HandSlots();
            Assert.That(slots.TryTake(Fish(), true, CarryHandSide.Right, out _),
                        Is.EqualTo(HandSlotRefusal.None));

            // Even for a kind that WOULD share: both hands are on the fish, so "full" is the true word.
            Assert.That(slots.TryTake(Rod(), false, CarryHandSide.Right, out _),
                        Is.EqualTo(HandSlotRefusal.HandsFull));
            Assert.That(slots.TryTake(Fish(), false, CarryHandSide.Right, out _),
                        Is.EqualTo(HandSlotRefusal.HandsFull));
        }

        // ---- 5. ⭐ THE CONTINUITY LAW -----------------------------------------------------------------

        [Test]
        public void ARefusal_ChangesNOTHING()
        {
            Thing rod = Rod(), pail = Pail();
            HandSlots slots = Holding(rod, pail);
            CarryHandSide rodSide = slots.SideOf(rod), pailSide = slots.SideOf(pail);

            foreach (bool cradle in new[] { false, true })
                Assert.That(slots.TryTake(Fish(), cradle, CarryHandSide.Right, out _),
                            Is.Not.EqualTo(HandSlotRefusal.None),
                            $"a third thing must be refused (cradle {cradle})");

            Assert.That(slots.SideOf(rod), Is.EqualTo(rodSide), "the rod did not change hands");
            Assert.That(slots.SideOf(pail), Is.EqualTo(pailSide), "nor did the pail");
            Assert.That(slots.UsedHands, Is.EqualTo(2));
            Assert.That(slots.SpansBoth, Is.False, "and nothing became a cradle on the way out");
        }

        [Test]
        public void TakingSomethingUp_NeverMovesWhatIsAlreadyHeld()
        {
            // The law's positive half, over every order the two hands can be filled in.
            foreach (CarryHandSide firstPreference in new[] { CarryHandSide.Left, CarryHandSide.Right })
            {
                var slots = new HandSlots();
                Thing rod = Rod();
                Assert.That(slots.TryTake(rod, false, firstPreference, out CarryHandSide before),
                            Is.EqualTo(HandSlotRefusal.None));

                foreach (CarryHandSide secondPreference in new[] { CarryHandSide.Left, CarryHandSide.Right })
                {
                    var probe = new HandSlots();
                    probe.TryTake(rod, false, firstPreference, out _);
                    probe.TryTake(Fish(), false, secondPreference, out _);
                    Assert.That(probe.SideOf(rod), Is.EqualTo(before),
                                $"rod asked for {firstPreference}, fish asked for {secondPreference} — " +
                                "the rod must not have moved");
                }
            }
        }

        [Test]
        public void ReleasingOneHand_LeavesTheOtherExactlyAsItWas()
        {
            Thing rod = Rod(), fish = Fish();
            HandSlots slots = Holding(rod, fish);
            CarryHandSide rodSide = slots.SideOf(rod);

            Assert.That(slots.Release(fish), Is.True);

            Assert.That(slots.SideOf(rod), Is.EqualTo(rodSide), "the rod stayed put");
            Assert.That(slots.UsedHands, Is.EqualTo(1));
            Assert.That(slots.Holds(HandLoad.Catch), Is.False);
            Assert.That(slots.Release(fish), Is.False, "releasing what is not held is an ordinary 'no'");
        }

        [Test]
        public void ReleasingACradle_FreesBOTHHands()
        {
            var slots = new HandSlots();
            Thing cod = Fish();
            slots.TryTake(cod, true, CarryHandSide.Right, out _);

            Assert.That(slots.Release(cod), Is.True);
            Assert.That(slots.IsEmpty, Is.True);
            Assert.That(slots.SpansBoth, Is.False);
            Assert.That(slots.UsedHands, Is.EqualTo(0));
        }

        // ---- housekeeping ------------------------------------------------------------------------------

        [Test]
        public void CanTake_AndTryTake_AgreeOnEveryState_SoThereIsOnlyONERule()
        {
            // The two-caller trap: a catch is SPAWNED, so the carrier must ask before it builds. If the
            // asking form and the doing form ever disagree, the carrier creates an object it then has to
            // destroy — or worse, refuses one it could have taken.
            var kinds = new[] { HandLoad.Tool, HandLoad.Container, HandLoad.Catch };
            foreach (HandLoad held in kinds)
                foreach (HandLoad want in kinds)
                    foreach (bool cradle in new[] { false, true })
                    {
                        var a = Holding(new Thing(held));
                        var b = Holding(new Thing(held));
                        Assert.That(a.CanTake(want, cradle),
                                    Is.EqualTo(b.TryTake(new Thing(want), cradle, CarryHandSide.Right, out _)),
                                    $"holding {held}, offered {want} (cradle {cradle})");
                    }
        }

        [Test]
        public void EveryRefusal_HasSomethingToSay()
        {
            foreach (HandSlotRefusal r in Enum.GetValues(typeof(HandSlotRefusal)))
            {
                if (r == HandSlotRefusal.None)
                {
                    Assert.That(HandSlots.Message(r), Is.Null, "nothing was refused");
                    continue;
                }
                Assert.That(HandSlots.Message(r), Is.Not.Null.And.Not.Empty,
                            $"{r} would refuse the press in silence");
            }
        }

        [Test]
        public void NothingIsTaken_WhenNothingIsOffered()
        {
            var slots = new HandSlots();
            Assert.That(slots.TryTake(null, false, CarryHandSide.Right, out CarryHandSide side),
                        Is.EqualTo(HandSlotRefusal.NothingToHold));
            Assert.That(side, Is.EqualTo(CarryHandSide.None));
            Assert.That(slots.IsEmpty, Is.True);
        }

        [Test]
        public void AThingThatSaysNothing_IsAToolUnlessItIsAHold()
        {
            Assert.That(HandSlots.LoadOf(null), Is.EqualTo(HandLoad.Empty));
            Assert.That(HandSlots.LoadOf(new Plain()), Is.EqualTo(HandLoad.Tool),
                        "a shovel, a jerry can, anything with no opinion");
            Assert.That(HandSlots.LoadOf(new PlainHold()), Is.EqualTo(HandLoad.Container),
                        "an IHold is a container without having to say so twice");
            Assert.That(HandSlots.LoadOf(new Thing(HandLoad.Catch)), Is.EqualTo(HandLoad.Catch),
                        "and a thing that DOES say so is taken at its word");
        }

        private class Plain : ICarriable
        {
            public string DefId => "plain";
            public bool IsCarriable => true;
            public Transform Transform => null;
            public int BakedFacings => 0;
            public void ShowFacing(int facingIndex) { }
            public void RideSortingBand(int layer, int order) { }
            public void OnLifted(ICarrier carrier) { }
            public void OnPlaced() { }
        }

        private sealed class PlainHold : Plain, IHold
        {
            private readonly List<CatchItem> _items = new List<CatchItem>();
            public int CapacityUnits => 4;
            public int UsedUnits => _items.Count;
            public IReadOnlyList<CatchItem> Items => _items;
            public bool TryAdd(CatchItem item) { _items.Add(item); return true; }
            public void Clear() => _items.Clear();
        }
    }
}
