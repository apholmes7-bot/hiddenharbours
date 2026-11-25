using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;
using HiddenHarbours.World;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>The interaction chooser</b> (owner ask 2026-11-15): "when a player is standing in a place with
    /// multiple options a choice will appear … a bunk occupied is unable to be selected at that time, or a
    /// nearby bunk will automatically be selected", and "when a player is too tired to do an action they
    /// will say im too tired or im too hungry or both".
    ///
    ///   • ONE OPTION IS UNCHANGED — the press acts at once, no list, exactly as before. Pinned first.
    ///   • THE LIST — two or more pickable rows open it; its order is the resolver's ladder, the same for
    ///     every registration order (rule 5); it starts on what a single press would have done; it wraps
    ///     and skips greyed rows; it caps.
    ///   • THE BUNK RULE — a taken bunk is never picked; a free one in reach stands in for it, for any
    ///     number of bunks; all taken shows one greyed row.
    ///   • THE HANDS ON CORE — confirm, cancel, a number pick, the gate and the move held only while up.
    ///   • NEEDS — the truth table, the cue ids, and a gated action refused out loud and not done.
    /// </summary>
    public class InteractChooserTests
    {
        private class Fake : IInteractable, IExclusiveInteractable
        {
            public string Id { get; set; } = "fake";
            public Vector2 WorldPosition { get; set; }
            public float ReachMeters { get; set; } = 2f;
            public int Priority { get; set; } = InteractPriority.Fixture;
            public InteractContext Contexts { get; set; } = InteractContext.OnFoot;
            public bool RequiresFacing { get; set; }
            public bool IsAvailable { get; set; } = true;
            public string VerbLabel { get; set; } = "Do the thing";
            public string ExclusiveGroup { get; set; }
            public bool IsOccupied { get; set; }

            public int Calls;
            public virtual void Interact(in InteractActor actor) => Calls++;
        }

        private sealed class Gated : Fake, INeedsGated
        {
            public NeedsRequirement NeedsRequirement { get; set; }
        }

        private sealed class Multi : Fake, IMultiActionInteractable
        {
            public string[] Labels = { "Buy", "Sell", "Talk" };
            public bool[] Available = { true, true, true };
            public int LastAction = -2;

            public int ActionCount => Labels.Length;
            public string ActionLabel(int index) => Labels[index];
            public bool ActionAvailable(int index) => Available[index];
            public void Interact(in InteractActor actor, int index) { Calls++; LastAction = index; }
        }

        private sealed class FakeNeeds : INeedsService
        {
            public NeedStage Tired, Hungry;
            public float Level(NeedKind need) => 0f;
            public NeedStage Stage(NeedKind need) => need == NeedKind.Tiredness ? Tired : Hungry;
            public float PlayerMultiplier(TraitStat stat) => 1f;
            public void Eat(float amount) { }
            public float Spend(float amount) => 0f;
            public void Sleep() { }
            public void Tick() { }
        }

        private static Fake At(string id, float x, string label = null, int priority = InteractPriority.Fixture)
            => new Fake { Id = id, WorldPosition = new Vector2(x, 0f), Priority = priority, VerbLabel = label ?? id };

        private static Fake Bunk(string id, float x, bool occupied)
            => new Fake { Id = id, WorldPosition = new Vector2(x, 0f), VerbLabel = "Sleep",
                          ExclusiveGroup = InteractExclusiveGroups.Rest, IsOccupied = occupied,
                          Priority = InteractPriority.Berth };

        private static InteractActor Actor() => InteractActor.For(Vector2.zero, Vector2.right, ControlMode.OnFoot);

        private const float Arc = InteractResolver.DefaultFacingArcDegrees;

        private static void Register(params IInteractable[] things)
        {
            foreach (IInteractable t in things) Interactables.Register(t);
        }

        private static InteractChoiceList Build(IReadOnlyList<IInteractable> list, int maxRows = 6,
                                                int maxPriority = InteractPriority.Fixture)
        {
            var rows = new InteractChoiceList();
            rows.Build(list, Actor(), Arc, maxPriority, maxRows);
            return rows;
        }

        private static string[] Labels(InteractChoiceList rows)
            => Enumerable.Range(0, rows.Count).Select(i => rows[i].Label).ToArray();

        private int _frame;
        private GameConfig _savedConfig;
        private INeedsService _savedNeeds;
        private readonly List<InteractPerformed> _performed = new List<InteractPerformed>();
        private readonly List<InnerVoiceCue> _cues = new List<InnerVoiceCue>();

        private void OnPerformed(InteractPerformed e) => _performed.Add(e);
        private void OnCue(InnerVoiceCue e) => _cues.Add(e);

        [SetUp]
        public void SetUp()
        {
            _savedConfig = GameServices.Config;
            _savedNeeds = GameServices.Needs;
            GameServices.Config = null;
            GameServices.Needs = new FakeNeeds();
            Interactables.Clear();
            InteractVerb.Reset();
            InteractionGate.Reset();
            InteractActionClaim.Reset();
            MoveActionClaim.Reset();
            CancelKeyClaim.Reset();
            InteractActorProbe.Reset();
            InteractChooser.Reset();
            _frame = 100;
            InteractChooser.FrameClock = () => _frame;
            _performed.Clear();
            _cues.Clear();
            EventBus.Subscribe<InteractPerformed>(OnPerformed);
            EventBus.Subscribe<InnerVoiceCue>(OnCue);
        }

        [TearDown]
        public void TearDown()
        {
            EventBus.Unsubscribe<InteractPerformed>(OnPerformed);
            EventBus.Unsubscribe<InnerVoiceCue>(OnCue);
            InteractChooser.Reset();
            InteractVerb.Reset();
            Interactables.Clear();
            InteractionGate.Reset();
            InteractActionClaim.Reset();
            MoveActionClaim.Reset();
            CancelKeyClaim.Reset();
            InteractActorProbe.Reset();
            GameServices.Config = _savedConfig;
            GameServices.Needs = _savedNeeds;
        }

        // =====================================================================================
        //  ONE OPTION IS UNCHANGED
        // =====================================================================================

        [Test]
        public void OneOption_ThePressActsAtOnce_AndNoListAppears()
        {
            var door = At("fixture.door", 1f, "Open the door");
            Register(door);

            Assert.IsTrue(InteractVerb.TryPress(Actor(), Arc), "the press is taken, as it always was");
            Assert.AreEqual(1, door.Calls, "the one thing in reach is acted on at once");
            Assert.IsFalse(InteractChooser.IsOpen, "one option never opens a list");
            Assert.IsFalse(InteractionGate.IsBlocked, "and holds nothing");
            Assert.IsFalse(MoveActionClaim.IsClaimed);
            Assert.AreEqual(1, _performed.Count);
            Assert.AreEqual("fixture.door", _performed[0].Id);
        }

        [Test]
        public void NothingInReach_ThePressIsNotTaken()
        {
            Register(At("far", 9f));
            Assert.IsFalse(InteractVerb.TryPress(Actor(), Arc));
            Assert.IsFalse(InteractChooser.IsOpen);
        }

        [Test]
        public void TheChooserOff_TwoOptions_ActOnTheLadderWinner_AsBefore()
        {
            var config = ScriptableObject.CreateInstance<GameConfig>();
            try
            {
                config.InteractChooserEnabled = false;
                GameServices.Config = config;
                var near = At("a.near", 1f);
                var far = At("b.far", 1.5f);
                Register(far, near);

                Assert.IsTrue(InteractVerb.TryPress(Actor(), Arc));
                Assert.AreEqual(1, near.Calls, "the owner's switch off restores the single press exactly");
                Assert.AreEqual(0, far.Calls);
                Assert.IsFalse(InteractChooser.IsOpen);
            }
            finally { Object.DestroyImmediate(config); }
        }

        [Test]
        public void HeldThings_KeepTheLadder_NoListWhileYourHandsAreFull()
        {
            var held = At("held.pail", 0.5f, "Set the pail down", InteractPriority.Held);
            var door = At("fixture.door", 1f, "Open the door");
            var lid = At("fixture.lid", 1.2f, "Open the lid");
            Register(door, held, lid);

            Assert.IsTrue(InteractVerb.TryPress(Actor(), Arc));
            Assert.AreEqual(1, held.Calls, "what you are holding is what you meant");
            Assert.IsFalse(InteractChooser.IsOpen, "carrying is not taxed with a list");
        }

        // =====================================================================================
        //  THE LIST
        // =====================================================================================

        [Test]
        public void TwoOptions_OpenTheList_ActOnNothing_AndHoldTheKeyAndTheAxis()
        {
            var door = At("fixture.door", 1f, "Open the door");
            var lid = At("fixture.lid", 1.5f, "Open the lid");
            Register(door, lid);

            Assert.IsTrue(InteractVerb.TryPress(Actor(), Arc));
            Assert.IsTrue(InteractChooser.IsOpen, "two options in reach put the list up");
            Assert.AreEqual(0, door.Calls + lid.Calls, "opening the list acts on nothing");
            Assert.IsTrue(InteractionGate.IsBlocked, "the list owns the interact key while it is up");
            Assert.IsTrue(MoveActionClaim.IsClaimed, "and the move axis, so up/down move the selection");
            Assert.IsTrue(CancelKeyClaim.IsHeld, "and Esc, so putting it down does not also pause the game");
            Assert.AreEqual(0, InteractChooser.Rows.Selected,
                "the list starts on what a single press would have done (the nearer)");
            Assert.IsTrue(InteractChooser.OpenedThisFrame, "the opening frame is marked for the view");
        }

        [Test]
        public void TheOrder_IsTheLadder_WhateverOrderThingsRegisteredIn()
        {
            IInteractable[] things =
            {
                At("c.bench", 1.5f, "Sit"), At("a.door", 1f, "Open the door"),
                At("b.lid", 1f, "Open the lid"), Bunk("bunk.1", 1.8f, false),
            };
            string[] first = null;
            foreach (IInteractable[] order in Permutations(things))
            {
                string[] labels = Labels(Build(order));
                if (first == null) first = labels;
                CollectionAssert.AreEqual(first, labels, "registration order decided the list (rule 5)");
            }
            // Fixtures (0) above the berth (-5); then nearer; then ordinal id breaks the tie at 1 m.
            CollectionAssert.AreEqual(new[] { "Open the door", "Open the lid", "Sit", "Sleep" }, first);
        }

        [Test]
        public void Step_Wraps_AndSkipsGreyedRows()
        {
            var rows = Build(new IInteractable[]
            {
                Bunk("bunk.taken", 0.5f, true), At("a.door", 1f), At("b.lid", 1.2f),
            });
            // The two fixtures, then the berth rung: all bunks in reach taken, so one greyed row for it.
            Assert.AreEqual(3, rows.Count);
            Assert.IsFalse(rows[2].Enabled);
            Assert.AreEqual(InteractChoiceBlock.Occupied, rows[2].Block);
            Assert.AreEqual(2, rows.EnabledCount);
            Assert.AreEqual(0, rows.Selected);

            Assert.IsTrue(rows.Step(+1));
            Assert.AreEqual(1, rows.Selected);
            Assert.IsTrue(rows.Step(+1), "down past the greyed bottom row wraps…");
            Assert.AreEqual(0, rows.Selected, "…to the top");
            Assert.IsTrue(rows.Step(-1), "up from the top wraps…");
            Assert.AreEqual(1, rows.Selected, "…skipping the greyed row");
            Assert.IsFalse(rows.TrySelect(2), "a greyed row cannot be selected directly either");
            Assert.AreEqual(1, rows.Selected);
        }

        [Test]
        public void TwoThingsThatReadTheSame_AreOneRow_TheNearer_AndAloneAreNoChoice()
        {
            // An ATV's two mount sides both say the one verb: two identical lines are not a choice.
            var near = At("atv.door", 0.6f, "Ride");
            var far = At("atv.door.alt", 1.2f, "Ride");
            InteractChoiceList rows = Build(new IInteractable[] { far, near });
            Assert.AreEqual(1, rows.Count);
            Assert.AreSame(near, rows[0].Candidate);
            Assert.IsFalse(rows.IsChoice, "so the press acts at once, as it always did");

            rows = Build(new IInteractable[] { far, At("a.door", 1f, "Open the door"), near });
            CollectionAssert.AreEqual(new[] { "Open the door", "Ride" }, Labels(rows).OrderBy(s => s).ToArray());
        }

        [Test]
        public void ALastResortThing_ActsOnlyAlone_AndIsNeverARowBesideSomethingElse()
        {
            // Setting down the bag in his hand: at a shelf the press buys and no list opens.
            var setDown = At("bag", 0.2f, "Set the bag down", InteractPriority.LastResort);
            var shelf = At("shelf.bread", 1.0f, "Loaf of bread");

            InteractChoiceList rows = Build(new IInteractable[] { setDown, shelf });
            Assert.AreSame(shelf, rows.Default, "the fixture outranks it though the bag is nearer");
            CollectionAssert.AreEqual(new[] { "Loaf of bread" }, Labels(rows));
            Assert.IsFalse(rows.IsChoice, "one row: the press acts at once");

            rows = Build(new IInteractable[] { setDown, At("a.bunk", 1.0f, "Sleep", InteractPriority.Berth) });
            CollectionAssert.AreEqual(new[] { "Sleep" }, Labels(rows), "a bed outranks it too");

            rows = Build(new IInteractable[] { setDown });
            Assert.AreSame(setDown, rows.Default, "alone, it takes the press");
            CollectionAssert.AreEqual(new[] { "Set the bag down" }, Labels(rows));
        }

        [Test]
        public void TheRows_AreCapped()
        {
            var many = new List<IInteractable>();
            for (int i = 0; i < 12; i++) many.Add(At("thing." + i.ToString("00"), 0.1f * i + 0.1f));
            Assert.AreEqual(4, Build(many, maxRows: 4).Count, "the owner's row cap");
            Assert.AreEqual(InteractChoiceList.HardCap, Build(many, maxRows: 99).Count,
                "never more rows than number keys");
            Assert.AreEqual("thing.00", Build(many, maxRows: 4)[0].Candidate.Id, "the nearest survive the cap");
        }

        [Test]
        public void AMultiActionThing_IsOneRowPerAction_AndAnUnavailableActionIsGreyed()
        {
            var counter = new Multi { Id = "fixture.counter", WorldPosition = new Vector2(1f, 0f) };
            counter.Available[1] = false;
            Register(counter);

            Assert.IsTrue(InteractVerb.TryPress(Actor(), Arc));
            Assert.IsTrue(InteractChooser.IsOpen, "one thing with several actions is a choice");
            CollectionAssert.AreEqual(new[] { "Buy", "Sell", "Talk" }, Labels(InteractChooser.Rows));
            Assert.AreEqual(InteractChoiceBlock.Unavailable, InteractChooser.Rows[1].Block);

            _frame++;
            Assert.IsTrue(InteractChooser.Pick(2));
            Assert.AreEqual(2, counter.LastAction, "the picked action is the one done");
            Assert.AreEqual(1, counter.Calls);
        }

        // =====================================================================================
        //  THE BUNK RULE
        // =====================================================================================

        [Test]
        public void ATakenBunk_BesideAFreeOne_TheSinglePressTurnsInAtTheFreeOne()
        {
            var taken = Bunk("bunk.port", 0.6f, true);
            var free = Bunk("bunk.starboard", 1.4f, false);
            Register(taken, free);

            var rows = Build(Interactables.Active);
            Assert.AreSame(free, rows.Default, "the free bunk stands in for the nearer taken one");
            Assert.AreEqual(1, rows.Count, "one row for the group: somewhere to sleep, not a list of bunks");

            Assert.IsTrue(InteractVerb.TryPress(Actor(), Arc));
            Assert.IsFalse(InteractChooser.IsOpen);
            Assert.AreEqual(1, free.Calls);
            Assert.AreEqual(0, taken.Calls, "the taken bunk is never acted on while a free one is in reach");
        }

        [Test]
        public void NBunks_TheNearestFreeOneIsChosen_ForAnyNumber()
        {
            for (int n = 2; n <= 6; n++)
            {
                var bunks = new List<IInteractable>();
                for (int i = 0; i < n; i++) bunks.Add(Bunk("bunk." + i, 0.2f + 0.25f * i, occupied: i < n - 1));
                Assert.IsTrue(InteractChoiceList.TryResolveDefault(bunks, Actor(), Arc, out IInteractable target));
                Assert.AreEqual("bunk." + (n - 1), target.Id, $"with {n} bunks and only the last free");

                ((Fake)bunks[n - 1]).IsOccupied = true;
                ((Fake)bunks[1]).IsOccupied = false;
                Assert.IsTrue(InteractChoiceList.TryResolveDefault(bunks, Actor(), Arc, out target));
                Assert.AreEqual("bunk.1", target.Id, "the free one wherever it is in the row");
            }
        }

        [Test]
        public void AllBunksTaken_IsOneGreyedRow_AndTheBunkStillAnswersAlone()
        {
            var a = Bunk("bunk.a", 0.6f, true);
            var b = Bunk("bunk.b", 1.0f, true);
            Register(a, b);

            var rows = Build(Interactables.Active);
            Assert.AreEqual(1, rows.Count);
            Assert.IsFalse(rows[0].Enabled);
            Assert.AreEqual(-1, rows.Selected, "nothing pickable");

            // With nothing else in reach the press reaches the bunk as it always did, and the bunk gives
            // its own refusal (BoatBunk: SleepFlow.OccupiedText) — feedback, not silence.
            Assert.IsTrue(InteractVerb.TryPress(Actor(), Arc));
            Assert.IsFalse(InteractChooser.IsOpen);
            Assert.AreEqual(1, a.Calls);
        }

        [Test]
        public void ATakenBunk_InTheList_CannotBePicked_AndTheListStaysUp()
        {
            Register(Bunk("bunk.taken", 0.5f, true), At("a.door", 1f), At("b.lid", 1.2f));
            Assert.IsTrue(InteractVerb.TryPress(Actor(), Arc));
            Assert.IsTrue(InteractChooser.IsOpen);
            _frame++;

            Assert.AreEqual(InteractChoiceBlock.Occupied, InteractChooser.Rows[2].Block);
            Assert.IsFalse(InteractChooser.Pick(2), "the number key on a taken bunk does nothing");
            Assert.IsTrue(InteractChooser.IsOpen, "and the list stays up for another pick");
            Assert.AreEqual(0, _performed.Count);
        }

        [Test]
        public void TheRealBunkAndTheRealBed_AnswerTheOccupancySeam()
        {
            Assert.IsTrue(typeof(IExclusiveInteractable).IsAssignableFrom(typeof(BoatBunk)),
                "a boat bunk tells the list when someone is in it");
            Assert.IsTrue(typeof(IExclusiveInteractable).IsAssignableFrom(typeof(InteriorBed)),
                "so does a bed in a house");
        }

        // =====================================================================================
        //  THE HANDS, ON CORE
        // =====================================================================================

        [Test]
        public void Confirm_DoesTheSelectedRow_AndLetsGoOfTheKeyAndTheAxis()
        {
            var door = At("a.door", 1f);
            var lid = At("b.lid", 1.5f);
            Register(door, lid);
            InteractVerb.TryPress(Actor(), Arc);
            _frame++;

            Assert.IsTrue(InteractChooser.Step(+1));
            Assert.IsTrue(InteractChooser.Confirm());
            Assert.AreEqual(1, lid.Calls, "the selected row is the one done");
            Assert.AreEqual(0, door.Calls);
            Assert.IsFalse(InteractChooser.IsOpen);
            Assert.IsFalse(InteractionGate.IsBlocked, "the key is given back");
            Assert.IsFalse(MoveActionClaim.IsClaimed, "and the axis");
            Assert.AreEqual("b.lid", _performed.Single().Id);
        }

        [Test]
        public void TheConfirmPress_IsSwallowed_SoTheSameFrameCannotReopenTheList()
        {
            var door = At("a.door", 1f);
            var lid = At("b.lid", 1.5f);
            Register(door, lid);
            InteractVerb.TryPress(Actor(), Arc);
            _frame++;
            InteractChooser.Confirm();

            Assert.IsFalse(InteractVerb.TryPress(Actor(), Arc),
                "a later reader of the same E press this frame must not reopen the list");
            Assert.IsFalse(InteractChooser.IsOpen);
            _frame++;
            Assert.IsTrue(InteractVerb.TryPress(Actor(), Arc), "the next press is a press again");
            Assert.IsTrue(InteractChooser.IsOpen);
        }

        [Test]
        public void Cancel_DoesNothing_AndLetsGo()
        {
            var door = At("a.door", 1f);
            var lid = At("b.lid", 1.5f);
            Register(door, lid);
            InteractVerb.TryPress(Actor(), Arc);
            _frame++;

            InteractChooser.Cancel();
            Assert.IsFalse(InteractChooser.IsOpen);
            Assert.AreEqual(0, door.Calls + lid.Calls);
            Assert.IsFalse(InteractionGate.IsBlocked);
            Assert.IsFalse(MoveActionClaim.IsClaimed);
            Assert.IsFalse(CancelKeyClaim.IsHeld);
            Assert.IsTrue(CancelKeyClaim.OwnedThisFrame, "the closing Esc is still the list's this frame");
        }

        [Test]
        public void TheList_LetsGoOnlyOfWhatItTook()
        {
            MoveActionClaim.IsClaimed = true;   // somebody else already holds the axis
            Register(At("a.door", 1f), At("b.lid", 1.5f));
            Assert.IsTrue(InteractVerb.TryPress(Actor(), Arc));
            InteractChooser.Cancel();
            Assert.IsTrue(MoveActionClaim.IsClaimed, "the other holder's claim survives the list");
        }

        [Test]
        public void AModalUp_ThePressStandsDown_AndNoListOpens()
        {
            Register(At("a.door", 1f), At("b.lid", 1.5f));
            InteractionGate.IsBlocked = true;
            Assert.IsFalse(InteractVerb.TryPress(Actor(), Arc));
            Assert.IsFalse(InteractChooser.IsOpen);
        }

        [Test]
        public void AThingThatWentAway_BeforeTheConfirm_IsNotActedOn()
        {
            var door = At("a.door", 1f);
            var lid = At("b.lid", 1.5f);
            Register(door, lid);
            InteractVerb.TryPress(Actor(), Arc);
            _frame++;
            Interactables.Unregister(door);

            Assert.IsTrue(InteractChooser.Confirm(), "the confirm is taken");
            Assert.AreEqual(0, door.Calls, "but a thing no longer in the world is not acted on");
            Assert.IsFalse(InteractChooser.IsOpen);
        }

        [Test]
        public void ABunkTakenWhileTheListWasUp_IsNotActedOn()
        {
            var bunk = Bunk("bunk.a", 0.6f, false);
            var door = At("a.door", 1f);
            Register(bunk, door);
            InteractVerb.TryPress(Actor(), Arc);
            Assert.IsTrue(InteractChooser.Step(+1));
            Assert.AreSame(bunk, InteractChooser.Rows[InteractChooser.Rows.Selected].Candidate);
            _frame++;
            bunk.IsOccupied = true;

            InteractChooser.Confirm();
            Assert.AreEqual(0, bunk.Calls, "somebody got there first: the live check refuses it");
        }

        // =====================================================================================
        //  NEEDS
        // =====================================================================================

        [Test]
        public void NeedsJudge_TruthTable()
        {
            var bar = new NeedsRequirement(NeedStage.Strong, NeedStage.Strong);
            Assert.AreEqual(NeedsRefusal.None, NeedsGate.Judge(NeedStage.None, NeedStage.None, bar));
            Assert.AreEqual(NeedsRefusal.None, NeedsGate.Judge(NeedStage.Mild, NeedStage.Mild, bar));
            Assert.AreEqual(NeedsRefusal.TooTired, NeedsGate.Judge(NeedStage.Strong, NeedStage.Mild, bar));
            Assert.AreEqual(NeedsRefusal.TooHungry, NeedsGate.Judge(NeedStage.None, NeedStage.Strong, bar));
            Assert.AreEqual(NeedsRefusal.TooTiredAndHungry, NeedsGate.Judge(NeedStage.Strong, NeedStage.Strong, bar));

            var hungerOnly = new NeedsRequirement(NeedStage.None, NeedStage.Mild);
            Assert.AreEqual(NeedsRefusal.TooHungry, NeedsGate.Judge(NeedStage.Strong, NeedStage.Mild, hungerOnly),
                "a half set to None never refuses, however tired");
            Assert.AreEqual(NeedsRefusal.None,
                NeedsGate.Judge(NeedStage.Strong, NeedStage.Strong, NeedsRequirement.Never),
                "the default requirement never refuses anything");
        }

        [Test]
        public void EachRefusal_HasItsOwnCue_AndItsOwnLineAsData()
        {
            Assert.IsNull(NeedsGate.CueFor(NeedsRefusal.None));
            var cues = new[]
            {
                (NeedsRefusal.TooTired, "TooTired.asset", "I'm too tired."),
                (NeedsRefusal.TooHungry, "TooHungry.asset", "I'm too hungry."),
                (NeedsRefusal.TooTiredAndHungry, "TooTiredAndHungry.asset", "I'm too tired and too hungry."),
            };
            foreach (var (refusal, file, line) in cues)
            {
                string cue = NeedsGate.CueFor(refusal);
                StringAssert.StartsWith("cue.", cue);
                string asset = File.ReadAllText("Assets/_Project/Data/NPCs/InnerVoice/" + file);
                StringAssert.Contains("SubjectId: " + cue, asset, $"{file} answers the {refusal} cue");
                StringAssert.Contains("Line: " + line, asset, $"{file} says the owner's words");
                StringAssert.Contains("Trigger: 2", asset, $"{file} is spoken on a cue");

                string guid = File.ReadAllLines("Assets/_Project/Data/NPCs/InnerVoice/" + file + ".meta")
                                  .First(l => l.StartsWith("guid: ")).Substring(6).Trim();
                StringAssert.Contains(guid, File.ReadAllText("Assets/_Project/Resources/InnerVoiceLibrary.asset"),
                    $"{file} is in the library, so the director can find it in a build");
            }
        }

        [Test]
        public void AGatedAction_TooTired_IsRefusedOutLoud_AndNotDone()
        {
            var needs = (FakeNeeds)GameServices.Needs;
            var nets = new Gated { Id = "fixture.nets", WorldPosition = new Vector2(1f, 0f),
                                   NeedsRequirement = new NeedsRequirement(NeedStage.Strong, NeedStage.Strong) };
            Register(nets);

            needs.Tired = NeedStage.Strong;
            Assert.IsTrue(InteractVerb.TryPress(Actor(), Arc), "the press is taken (she answers it)");
            Assert.AreEqual(0, nets.Calls, "too tired: the work is not done");
            Assert.AreEqual(InnerVoiceCues.TooTired, _cues.Single().CueId, "and she says why");
            Assert.AreEqual(0, _performed.Count);

            needs.Hungry = NeedStage.Strong;
            InteractVerb.TryPress(Actor(), Arc);
            Assert.AreEqual(InnerVoiceCues.TooTiredAndHungry, _cues.Last().CueId, "both, in one line");

            needs.Tired = NeedStage.Mild;
            needs.Hungry = NeedStage.Mild;
            InteractVerb.TryPress(Actor(), Arc);
            Assert.AreEqual(1, nets.Calls, "rested enough, the work goes ahead");
        }

        [Test]
        public void TheHeavyWorkBar_DefaultsToStrong_AndUngatedThingsAreNeverRefused()
        {
            NeedsRequirement bar = NeedsRequirement.HeavyWork(null);
            Assert.AreEqual(NeedStage.Strong, bar.RefusedAtTiredness);
            Assert.AreEqual(NeedStage.Strong, bar.RefusedAtHunger);

            var needs = (FakeNeeds)GameServices.Needs;
            needs.Tired = NeedStage.Strong;
            needs.Hungry = NeedStage.Strong;
            var door = At("a.door", 1f);
            Register(door);
            InteractVerb.TryPress(Actor(), Arc);
            Assert.AreEqual(1, door.Calls, "the needs ruling: nothing is refused unless it opted in");
            Assert.AreEqual(0, _cues.Count);
        }

        // =====================================================================================

        private static IEnumerable<IInteractable[]> Permutations(IInteractable[] items)
        {
            if (items.Length <= 1) { yield return items; yield break; }
            for (int i = 0; i < items.Length; i++)
            {
                IInteractable[] rest = items.Where((_, j) => j != i).ToArray();
                foreach (IInteractable[] tail in Permutations(rest))
                    yield return new[] { items[i] }.Concat(tail).ToArray();
            }
        }
    }
}
