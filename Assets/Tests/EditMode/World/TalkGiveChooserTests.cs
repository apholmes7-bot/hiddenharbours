using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.World;

namespace HiddenHarbours.Tests.World.EditMode
{
    /// <summary>
    /// <b>Talk, then one Give row per item type he has anywhere</b>, when the player faces somebody he has
    /// met: "Talk" first and selected, then "Give &lt;item&gt;" for each giftable type in his hands, in what he
    /// carries and in his storage containers, with a count when he has more than one. With nothing giftable
    /// there is no list and the press talks. A bare press never gives; a Give pick gives exactly ONE, from
    /// his hands first, then what he carries, then storage; picking Give again gives another, under the
    /// daily limit.
    ///
    /// <para>Driven through <c>BeginInteract</c> (the input-free half of a press) and the chooser's own Core
    /// calls (<see cref="InteractChooser.Confirm"/>, <see cref="InteractChooser.Pick"/>), so what is asserted
    /// is what the keys do.</para>
    /// </summary>
    public class TalkGiveChooserTests
    {
        // ---- fakes -----------------------------------------------------------------------------------

        private sealed class FakeHands : ICatchHands
        {
            public CatchItem? Held;
            public int Takes;

            public bool TryPutInHand(in CatchItem item)
            {
                if (Held.HasValue) return false;
                Held = item;
                return true;
            }

            public bool TryTakeFromHand(out CatchItem item)
            {
                item = Held ?? default;
                if (!Held.HasValue) return false;
                Held = null;
                Takes++;
                return true;
            }

            public bool TryPeekHand(out CatchItem item)
            {
                item = Held ?? default;
                return Held.HasValue;
            }
        }

        private class FakeHold : IHold
        {
            public readonly List<CatchItem> List = new List<CatchItem>();
            public int Capacity = 20;

            public FakeHold(params CatchItem[] items) => List.AddRange(items);

            public int CapacityUnits => Capacity;
            public int UsedUnits => List.Count;
            public IReadOnlyList<CatchItem> Items => List;

            public bool TryAdd(CatchItem item)
            {
                if (List.Count >= Capacity) return false;
                List.Add(item);
                return true;
            }

            public void Clear() => List.Clear();
        }

        /// <summary>A pail he has picked up: a container in a hand.</summary>
        private sealed class FakePail : FakeHold, ICarriable, IHandLoad
        {
            public FakePail(params CatchItem[] items) : base(items) { }
            public string DefId => "container.bucket";
            public bool IsCarriable => true;
            public Transform Transform => null;
            public int BakedFacings => 0;
            public void ShowFacing(int facingIndex) { }
            public void RideSortingBand(int sortingLayerId, int sortingOrder) { }
            public void OnLifted(ICarrier carrier) { }
            public void OnPlaced() { }
            public HandLoad HandLoad => HandLoad.Container;
        }

        private sealed class FakeCarrier : ICarrier
        {
            public ICarriable Carried { get; set; }
            public bool IsCarrying => Carried != null;
        }

        private sealed class NotGiftable : IGiftableFacts
        {
            private readonly string _id;
            public NotGiftable(string id) => _id = id;

            public bool TryIsGiftable(string itemId, out bool giftable)
            {
                giftable = false;
                return itemId == _id;
            }
        }

        private sealed class DayClock : IGameClock
        {
            public int Day;
            public double TotalSeconds => 0d;
            public GameTime Now => new GameTime(TotalSeconds);
            public Season Season => Season.HighSummer;
            public int Year => 1;
            public int DayIndex => Day;
            public int DayOfSeason => 1;
            public Weekday Weekday => Weekday.Monday;
            public bool IsMarketDay => false;
            public float HourOfDay => 12f;
            public float DayFraction => 0.5f;
            public bool IsPaused { get; set; }
            public float TimeScale { get; set; } = 1f;
        }

        private sealed class CountingRelationships : IRelationshipService
        {
            public int Records;
            public RelationshipStage Stage(string a, string b) => RelationshipStage.Stranger;
            public bool Record(in RelationshipEvent e) { Records++; return true; }
            public int ApplyDay() => 0;
        }

        // ---- rig -------------------------------------------------------------------------------------

        private const string CodName = "Atlantic Cod";
        private const string CodId = "fish.atlantic_cod";

        private readonly List<Object> _spawned = new List<Object>();
        private ICatchHands _savedHands;
        private ICarrier _savedCarrier;
        private IHold _savedPlayerHold;
        private IGiftableFacts _savedFacts;
        private GameConfig _savedConfig;
        private IGameClock _savedClock;
        private IRelationshipService _savedRelationships;
        private DayClock _clock;
        private CountingRelationships _relationships;
        private int _frame;

        [SetUp]
        public void SetUp()
        {
            _savedHands = GameServices.CatchHands;
            _savedCarrier = GameServices.Hands;
            _savedPlayerHold = GameServices.PlayerHold;
            _savedFacts = GameServices.GiftableFacts;
            _savedConfig = GameServices.Config;
            _savedClock = GameServices.Clock;
            _savedRelationships = GameServices.Relationships;
            GameServices.CatchHands = null;
            GameServices.Hands = null;
            GameServices.PlayerHold = null;
            GameServices.GiftableFacts = null;
            GameServices.Config = null;
            _clock = new DayClock();
            GameServices.Clock = _clock;
            _relationships = new CountingRelationships();
            GameServices.Relationships = _relationships;
            PlayerStorage.Clear();
            GiftLedger.Shared.Clear();
            ResetSeams();
            _frame = 100;
            InteractChooser.FrameClock = () => _frame;
        }

        [TearDown]
        public void TearDown()
        {
            ResetSeams();
            PlayerStorage.Clear();
            GiftLedger.Shared.Clear();
            GameServices.CatchHands = _savedHands;
            GameServices.Hands = _savedCarrier;
            GameServices.PlayerHold = _savedPlayerHold;
            GameServices.GiftableFacts = _savedFacts;
            GameServices.Config = _savedConfig;
            GameServices.Clock = _savedClock;
            GameServices.Relationships = _savedRelationships;
            foreach (Object o in _spawned) if (o != null) Object.DestroyImmediate(o);
            _spawned.Clear();
        }

        private static void ResetSeams()
        {
            InteractChooser.Reset();
            InteractVerb.Reset();
            Interactables.Clear();
            InteractionGate.Reset();
            InteractActionClaim.Reset();
            MoveActionClaim.Reset();
            CancelKeyClaim.Reset();
            InteractActorProbe.Reset();
            InteractOffer.Reset();
        }

        private GameObject Spawn(string name, Vector3 at)
        {
            var go = new GameObject(name);
            go.transform.position = at;
            _spawned.Add(go);
            return go;
        }

        private static CatchItem Cod() => new CatchItem(CodId, CodName, FishCategory.InshoreGroundfish, 2f, 10, 0.1f);
        private static CatchItem Mackerel() => new CatchItem("fish.atlantic_mackerel", "Atlantic Mackerel",
                                                             FishCategory.Pelagic, 0.5f, 4, 0.1f);
        private static CatchItem Clam() => new CatchItem("fish.soft_shell_clam", "Soft-shell Clam",
                                                         FishCategory.Shellfish, 0.05f, 1, 0.1f);
        private static CatchItem Haddock() => new CatchItem("fish.haddock", "Haddock",
                                                            FishCategory.InshoreGroundfish, 1.5f, 8, 0.1f);

        /// <summary>A player, and a villager he has met (no completion flag) standing in reach.</summary>
        private WorldInteractor StandBeforeAVillager(FakeHands hands)
        {
            GameServices.CatchHands = hands;
            var player = Spawn("player", Vector3.zero);

            var def = ScriptableObject.CreateInstance<NpcDef>();
            _spawned.Add(def);
            def.Id = "npc.test_neighbour";
            def.DisplayName = "Neighbour";
            def.Kind = InteractKind.Talk;
            def.CompletionFlag = "";

            var npc = Spawn("neighbour", new Vector3(0.5f, 0f, 0f)).AddComponent<Interactable>();
            npc.Configure(def);

            var interactor = Spawn("interactor", Vector3.zero).AddComponent<WorldInteractor>();
            // No presenter: a talk bails before speaking and a gift goes unsaid, which is all these tests
            // need. They assert what the press offers and what moved, not the bubble.
            interactor.Configure(player.transform, null, new[] { npc }, 1.8f);
            return interactor;
        }

        private static string[] Shown()
        {
            InteractChoiceList rows = InteractChooser.Rows;
            var labels = new string[rows.Count];
            for (int i = 0; i < rows.Count; i++) labels[i] = rows[i].Label;
            return labels;
        }

        private GiftInventory Gathered(FakeHands hands, ICarrier carrier, IHold carried)
        {
            var gifts = new GiftInventory();
            gifts.Gather(hands, carrier, carried, GameServices.GiftableFacts);
            return gifts;
        }

        // ---- the rows, as data -------------------------------------------------------------------

        [Test]
        public void With_nothing_giftable_the_one_row_is_Talk_and_it_is_not_a_choice()
        {
            var choice = new ConversationChoice(null, null);
            choice.Bind(null, 2f);
            var rows = new InteractChoiceList();
            rows.BuildActionsOf(choice, GameConfig.DefaultInteractChooserMaxRows);

            Assert.AreEqual(1, rows.Count);
            Assert.AreEqual(WorldStrings.TalkVerb, rows[0].Label);
            Assert.IsFalse(rows.IsChoice, "one row is not a list: the press just talks");
        }

        [Test]
        public void Talk_comes_first_and_selected_then_one_Give_row_per_type()
        {
            var choice = new ConversationChoice(null, null);
            choice.Bind(null, 2f);
            choice.AddGive(CodId, WorldStrings.GiveRowLabel(CodName, 3));
            choice.AddGive("fish.haddock", WorldStrings.GiveRowLabel("Haddock", 1));
            var rows = new InteractChoiceList();
            rows.BuildActionsOf(choice, GameConfig.DefaultInteractChooserMaxRows);

            Assert.AreEqual(3, rows.Count);
            Assert.AreEqual("Talk", rows[0].Label);
            Assert.AreEqual("Give " + CodName + " (3)", rows[1].Label, "more than one shows a count");
            Assert.AreEqual("Give Haddock", rows[2].Label, "one shows no count");
            Assert.AreEqual(0, rows.Selected, "Talk is the default row");
            Assert.IsTrue(rows.IsChoice);
        }

        [Test]
        public void Each_row_calls_its_own_act_with_its_item_type()
        {
            int talks = 0;
            var given = new List<string>();
            var choice = new ConversationChoice(_ => talks++, (_, id) => given.Add(id));
            var it = Spawn("v", Vector3.zero).AddComponent<Interactable>();
            choice.Bind(it, 2f);
            choice.AddGive(CodId, "Give Atlantic Cod");
            choice.AddGive("fish.haddock", "Give Haddock");

            choice.Interact(default, ConversationChoice.TalkAction);
            choice.Interact(default(InteractActor));
            Assert.AreEqual(2, talks, "the talk row and the plain press both talk");
            Assert.AreEqual(0, given.Count);

            choice.Interact(default, ConversationChoice.FirstGiveAction + 1);
            CollectionAssert.AreEqual(new[] { "fish.haddock" }, given);

            choice.Bind(it, 2f);
            choice.Interact(default, ConversationChoice.FirstGiveAction);
            Assert.AreEqual(1, given.Count, "with nothing giftable there is no give to do");
        }

        // ---- what he has, wherever he keeps it ---------------------------------------------------

        [Test]
        public void Items_from_his_hands_his_held_pail_his_belt_pail_and_his_storage_all_appear()
        {
            var hands = new FakeHands { Held = Cod() };
            var carrier = new FakeCarrier { Carried = new FakePail(Mackerel()) };
            var belt = new FakeHold(Clam());
            PlayerStorage.Register(new FakeHold(Haddock()));

            GiftInventory gifts = Gathered(hands, carrier, belt);

            Assert.AreEqual(4, gifts.KindCount);
            CollectionAssert.AreEqual(
                new[] { CodId, "fish.atlantic_mackerel", "fish.soft_shell_clam", "fish.haddock" },
                new[] { gifts.KindId(0), gifts.KindId(1), gifts.KindId(2), gifts.KindId(3) },
                "listed in the walk's order: hands, then what he carries, then storage");
        }

        [Test]
        public void One_row_per_type_however_many_places_it_is_in()
        {
            var hands = new FakeHands { Held = Cod() };
            var belt = new FakeHold(Cod(), Cod());
            PlayerStorage.Register(new FakeHold(Cod(), Haddock()));

            GiftInventory gifts = Gathered(hands, null, belt);

            Assert.AreEqual(2, gifts.KindCount);
            Assert.AreEqual(CodId, gifts.KindId(0));
            Assert.AreEqual(4, gifts.CountOf(0), "one in hand, two on the belt, one in storage");
            Assert.AreEqual(1, gifts.CountOf(1));
        }

        [Test]
        public void The_same_container_reached_twice_is_counted_once()
        {
            var belt = new FakeHold(Cod(), Cod());
            PlayerStorage.Register(belt);

            GiftInventory gifts = Gathered(new FakeHands(), null, belt);

            Assert.AreEqual(2, gifts.CountOf(0));
        }

        [Test]
        public void One_is_taken_per_take_from_hands_then_what_he_carries_then_storage()
        {
            var hands = new FakeHands { Held = Cod() };
            var pail = new FakePail(Cod());
            var belt = new FakeHold(Cod(), Haddock());
            var store = new FakeHold(Cod());
            PlayerStorage.Register(store);
            GiftInventory gifts = Gathered(hands, new FakeCarrier { Carried = pail }, belt);

            Assert.IsTrue(gifts.TryTakeOne(CodId, out _));
            Assert.IsFalse(hands.Held.HasValue, "the hands give first");
            Assert.AreEqual(1, pail.UsedUnits);
            Assert.AreEqual(2, belt.UsedUnits);
            Assert.AreEqual(1, store.UsedUnits);

            Assert.IsTrue(gifts.TryTakeOne(CodId, out _));
            Assert.AreEqual(0, pail.UsedUnits, "then the pail in his hand");
            Assert.AreEqual(2, belt.UsedUnits);

            Assert.IsTrue(gifts.TryTakeOne(CodId, out _));
            Assert.AreEqual(1, belt.UsedUnits, "then what he carries");
            Assert.AreEqual("fish.haddock", belt.List[0].SpeciesId, "the rest of the belt pail is untouched");
            Assert.AreEqual(1, store.UsedUnits);

            Assert.IsTrue(gifts.TryTakeOne(CodId, out _));
            Assert.AreEqual(0, store.UsedUnits, "then storage");

            Assert.IsFalse(gifts.TryTakeOne(CodId, out _), "none left anywhere");
            Assert.AreEqual(1, gifts.KindCount, "only the haddock is left to give");
        }

        [Test]
        public void Nothing_giftable_lists_nothing()
        {
            Assert.AreEqual(0, Gathered(new FakeHands(), new FakeCarrier(), new FakeHold()).KindCount,
                            "empty hands, an empty pail, no storage");

            GameServices.GiftableFacts = new NotGiftable(CodId);
            PlayerStorage.Register(new FakeHold(Cod()));
            GiftInventory gifts = Gathered(new FakeHands { Held = Cod() }, null, new FakeHold(Cod()));

            Assert.AreEqual(0, gifts.KindCount, "a species that may not be given is never listed");
            Assert.IsFalse(gifts.TryTakeOne(CodId, out _));
        }

        [Test]
        public void A_container_that_could_not_take_back_the_rest_is_left_alone()
        {
            var belt = new FakeHold(Cod(), Cod(), Cod()) { Capacity = 0 };   // the pail's gear was lost

            GiftInventory gifts = Gathered(new FakeHands(), null, belt);

            Assert.AreEqual(0, gifts.KindCount);
            Assert.IsFalse(gifts.TryTakeOne(CodId, out _));
            Assert.AreEqual(3, belt.UsedUnits, "nothing taken, nothing lost");
        }

        // ---- the press -----------------------------------------------------------------------------

        [Test]
        public void Nothing_giftable_a_press_talks_and_opens_no_list()
        {
            var interactor = StandBeforeAVillager(new FakeHands());
            GameServices.PlayerHold = new FakeHold();

            Assert.IsTrue(interactor.BeginInteract());
            Assert.IsFalse(InteractChooser.IsOpen, "only Talk applies, so there is nothing to choose");
        }

        [Test]
        public void A_giftable_catch_in_hand_opens_Talk_and_Give_and_the_press_gives_nothing()
        {
            var hands = new FakeHands { Held = Cod() };
            var interactor = StandBeforeAVillager(hands);

            Assert.IsTrue(interactor.BeginInteract());

            Assert.IsTrue(InteractChooser.IsOpen);
            CollectionAssert.AreEqual(new[] { "Talk", "Give " + CodName }, Shown());
            Assert.AreEqual(0, InteractChooser.Rows.Selected, "the list opens on Talk");
            Assert.AreEqual(0, hands.Takes, "⭐ a bare press never gives");
            Assert.IsTrue(hands.Held.HasValue);
        }

        [Test]
        public void Something_only_in_storage_still_gets_a_Give_row()
        {
            var interactor = StandBeforeAVillager(new FakeHands());
            PlayerStorage.Register(new FakeHold(Haddock(), Haddock()));

            Assert.IsTrue(interactor.BeginInteract());

            CollectionAssert.AreEqual(new[] { "Talk", "Give Haddock (2)" }, Shown());
        }

        [Test]
        public void Confirming_the_default_talks_and_nothing_moves()
        {
            var hands = new FakeHands { Held = Cod() };
            var interactor = StandBeforeAVillager(hands);
            interactor.BeginInteract();
            _frame++;

            Assert.IsTrue(InteractChooser.Confirm());

            Assert.IsFalse(InteractChooser.IsOpen);
            Assert.AreEqual(0, hands.Takes, "Talk is not Give");
            Assert.IsTrue(hands.Held.HasValue);
        }

        [Test]
        public void Picking_Give_gives_exactly_one_from_his_hands_first()
        {
            var hands = new FakeHands { Held = Cod() };
            var interactor = StandBeforeAVillager(hands);
            var belt = new FakeHold(Cod(), Cod());
            GameServices.PlayerHold = belt;
            interactor.BeginInteract();
            CollectionAssert.AreEqual(new[] { "Talk", "Give " + CodName + " (3)" }, Shown());
            _frame++;

            Assert.IsTrue(InteractChooser.Pick(ConversationChoice.FirstGiveAction));

            Assert.AreEqual(1, hands.Takes, "the one in his hand went");
            Assert.AreEqual(2, belt.UsedUnits, "the belt pail kept both: one per pick");
            Assert.AreEqual(1, _relationships.Records, "one gift recorded");
        }

        [Test]
        public void Giving_again_is_held_to_the_daily_limit_and_gives_another_the_next_day()
        {
            var interactor = StandBeforeAVillager(new FakeHands());
            var belt = new FakeHold(Cod(), Cod(), Cod());
            GameServices.PlayerHold = belt;

            interactor.BeginInteract();
            _frame++;
            InteractChooser.Pick(ConversationChoice.FirstGiveAction);
            Assert.AreEqual(2, belt.UsedUnits, "the first gift of the day: one went");

            _frame++;
            interactor.BeginInteract();
            CollectionAssert.AreEqual(new[] { "Talk", "Give " + CodName + " (2)" }, Shown(),
                                      "the row is offered again, one fewer");
            _frame++;
            InteractChooser.Pick(ConversationChoice.FirstGiveAction);
            Assert.AreEqual(2, belt.UsedUnits, "⭐ the daily limit still holds: declined, nothing moved");
            Assert.AreEqual(1, _relationships.Records);

            _clock.Day = 1;
            _frame++;
            interactor.BeginInteract();
            _frame++;
            InteractChooser.Pick(ConversationChoice.FirstGiveAction);
            Assert.AreEqual(1, belt.UsedUnits, "a new day: picking Give again gives another");
            Assert.AreEqual(2, _relationships.Records);
        }

        [Test]
        public void The_talk_give_list_holds_the_key_while_up_and_lets_it_go_on_cancel()
        {
            var interactor = StandBeforeAVillager(new FakeHands { Held = Cod() });
            interactor.BeginInteract();

            Assert.IsTrue(InteractionGate.IsBlocked, "no other reader of the key acts under the list");
            Assert.IsFalse(interactor.BeginInteract(), "a second press does not stack a conversation on it");

            InteractChooser.Cancel();
            Assert.IsFalse(InteractionGate.IsBlocked);
        }

        [Test]
        public void With_the_chooser_switched_off_a_giftable_catch_still_only_talks()
        {
            var config = ScriptableObject.CreateInstance<GameConfig>();
            _spawned.Add(config);
            config.InteractChooserEnabled = false;
            GameServices.Config = config;
            var hands = new FakeHands { Held = Cod() };
            var interactor = StandBeforeAVillager(hands);

            Assert.IsTrue(interactor.BeginInteract());

            Assert.IsFalse(InteractChooser.IsOpen);
            Assert.AreEqual(0, hands.Takes, "no list means no give, never a give on the bare press");
        }

        // ---- the chooser's unregistered rows --------------------------------------------------------------

        private ConversationChoice ChoiceWithOneGive(System.Action onGive, out Interactable it)
        {
            var choice = new ConversationChoice(_ => { }, (_, __) => onGive());
            it = Spawn("v", new Vector3(0.5f, 0f, 0f)).AddComponent<Interactable>();
            choice.Bind(it, 2f);
            choice.AddGive(CodId, WorldStrings.GiveRowLabel(CodName));
            return choice;
        }

        [Test]
        public void An_unregistered_list_confirms_on_a_thing_that_is_not_registered()
        {
            int gives = 0;
            ConversationChoice choice = ChoiceWithOneGive(() => gives++, out _);
            var rows = new InteractChoiceList();
            rows.BuildActionsOf(choice, GameConfig.DefaultInteractChooserMaxRows);
            var actor = new InteractActor(Vector2.zero, Vector2.zero, InteractContext.OnFoot);

            Assert.IsTrue(InteractChooser.OpenUnregistered(rows, actor, WorldInteractor.DefaultFacingArcDegrees));
            _frame++;
            Assert.IsTrue(InteractChooser.Pick(ConversationChoice.FirstGiveAction));
            Assert.AreEqual(1, gives);
        }

        [Test]
        public void A_plain_list_still_refuses_a_thing_that_is_not_registered()
        {
            int gives = 0;
            ConversationChoice choice = ChoiceWithOneGive(() => gives++, out _);
            var rows = new InteractChoiceList();
            rows.BuildActionsOf(choice, GameConfig.DefaultInteractChooserMaxRows);
            var actor = new InteractActor(Vector2.zero, Vector2.zero, InteractContext.OnFoot);

            Assert.IsTrue(InteractChooser.Open(rows, actor, WorldInteractor.DefaultFacingArcDegrees));
            _frame++;
            InteractChooser.Pick(ConversationChoice.FirstGiveAction);
            Assert.AreEqual(0, gives, "the registry re-check is unchanged for everything else");
        }

        [Test]
        public void An_unregistered_row_whose_villager_walked_out_of_reach_does_nothing()
        {
            int gives = 0;
            ConversationChoice choice = ChoiceWithOneGive(() => gives++, out Interactable it);
            var rows = new InteractChoiceList();
            rows.BuildActionsOf(choice, GameConfig.DefaultInteractChooserMaxRows);
            var actor = new InteractActor(Vector2.zero, Vector2.zero, InteractContext.OnFoot);
            InteractChooser.OpenUnregistered(rows, actor, WorldInteractor.DefaultFacingArcDegrees);
            _frame++;

            it.transform.position = new Vector3(10f, 0f, 0f);
            InteractChooser.Pick(ConversationChoice.FirstGiveAction);

            Assert.AreEqual(0, gives);
            Assert.IsFalse(InteractChooser.IsOpen);
        }
    }
}
