using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.World;

namespace HiddenHarbours.Tests.World.EditMode
{
    /// <summary>
    /// <b>Talk and Give are two rows</b> when the player faces somebody he has met with a giftable catch in
    /// his hand: "Talk" first and selected, "Give &lt;item&gt;" beneath it. With nothing giftable held there is
    /// no list and the press talks. A bare press never gives: the catch stays in his hand until the Give row
    /// itself is picked.
    ///
    /// <para>Driven through <c>BeginInteract</c> (the input-free half of a press) and the chooser's own Core
    /// calls (<see cref="InteractChooser.Confirm"/>, <see cref="InteractChooser.Pick"/>), so what is asserted
    /// is what the keys do.</para>
    /// </summary>
    public class TalkGiveChooserTests
    {
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

        private const string CodName = "Atlantic Cod";

        private readonly List<Object> _spawned = new List<Object>();
        private ICatchHands _savedHands;
        private IGiftableFacts _savedFacts;
        private GameConfig _savedConfig;
        private int _frame;

        [SetUp]
        public void SetUp()
        {
            _savedHands = GameServices.CatchHands;
            _savedFacts = GameServices.GiftableFacts;
            _savedConfig = GameServices.Config;
            GameServices.CatchHands = null;
            GameServices.GiftableFacts = null;
            GameServices.Config = null;
            ResetSeams();
            _frame = 100;
            InteractChooser.FrameClock = () => _frame;
        }

        [TearDown]
        public void TearDown()
        {
            ResetSeams();
            GameServices.CatchHands = _savedHands;
            GameServices.GiftableFacts = _savedFacts;
            GameServices.Config = _savedConfig;
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

        private static CatchItem Cod() => new CatchItem("fish.atlantic_cod", CodName, FishCategory.InshoreGroundfish,
                                                         2f, 10, 0.1f);

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
            // No presenter: a talk bails before speaking, which is all these tests need — they assert
            // what the press offers and whether the catch moved, not the bubble.
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

        // ---- the rows, as data -------------------------------------------------------------------

        [Test]
        public void With_nothing_giftable_the_one_row_is_Talk_and_it_is_not_a_choice()
        {
            var choice = new ConversationChoice(null, null);
            choice.Bind(null, null, 2f);
            var rows = new InteractChoiceList();
            rows.BuildActionsOf(choice, GameConfig.DefaultInteractChooserMaxRows);

            Assert.AreEqual(1, rows.Count);
            Assert.AreEqual(WorldStrings.TalkVerb, rows[0].Label);
            Assert.IsFalse(rows.IsChoice, "one row is not a list: the press just talks");
        }

        [Test]
        public void With_a_giftable_catch_Talk_comes_first_and_selected_and_Give_names_the_item()
        {
            var choice = new ConversationChoice(null, null);
            choice.Bind(null, WorldStrings.GiveRowLabel(CodName), 2f);
            var rows = new InteractChoiceList();
            rows.BuildActionsOf(choice, GameConfig.DefaultInteractChooserMaxRows);

            Assert.AreEqual(2, rows.Count);
            Assert.AreEqual("Talk", rows[0].Label);
            Assert.AreEqual("Give " + CodName, rows[1].Label);
            Assert.AreEqual(0, rows.Selected, "Talk is the default row");
            Assert.IsTrue(rows.IsChoice);
        }

        [Test]
        public void Each_row_calls_its_own_act()
        {
            int talks = 0, gives = 0;
            var choice = new ConversationChoice(_ => talks++, _ => gives++);
            var it = Spawn("v", Vector3.zero).AddComponent<Interactable>();
            choice.Bind(it, WorldStrings.GiveRowLabel(CodName), 2f);

            choice.Interact(default, ConversationChoice.TalkAction);
            choice.Interact(default(InteractActor));
            Assert.AreEqual(2, talks, "the talk row and the plain press both talk");
            Assert.AreEqual(0, gives);

            choice.Interact(default, ConversationChoice.GiveAction);
            Assert.AreEqual(1, gives);

            choice.Bind(it, null, 2f);
            choice.Interact(default, ConversationChoice.GiveAction);
            Assert.AreEqual(1, gives, "with nothing giftable there is no give to do");
        }

        // ---- the press -----------------------------------------------------------------------------

        [Test]
        public void Empty_hands_a_press_talks_and_opens_no_list()
        {
            var interactor = StandBeforeAVillager(new FakeHands());

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
        public void Confirming_the_default_talks_and_the_catch_stays_in_hand()
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

        [Test]
        public void An_unregistered_list_confirms_on_a_thing_that_is_not_registered()
        {
            int gives = 0;
            var choice = new ConversationChoice(_ => { }, _ => gives++);
            var it = Spawn("v", new Vector3(0.5f, 0f, 0f)).AddComponent<Interactable>();
            choice.Bind(it, WorldStrings.GiveRowLabel(CodName), 2f);
            var rows = new InteractChoiceList();
            rows.BuildActionsOf(choice, GameConfig.DefaultInteractChooserMaxRows);
            var actor = new InteractActor(Vector2.zero, Vector2.zero, InteractContext.OnFoot);

            Assert.IsTrue(InteractChooser.OpenUnregistered(rows, actor, WorldInteractor.DefaultFacingArcDegrees));
            _frame++;
            Assert.IsTrue(InteractChooser.Pick(ConversationChoice.GiveAction));
            Assert.AreEqual(1, gives);
        }

        [Test]
        public void A_plain_list_still_refuses_a_thing_that_is_not_registered()
        {
            int gives = 0;
            var choice = new ConversationChoice(_ => { }, _ => gives++);
            var it = Spawn("v", new Vector3(0.5f, 0f, 0f)).AddComponent<Interactable>();
            choice.Bind(it, WorldStrings.GiveRowLabel(CodName), 2f);
            var rows = new InteractChoiceList();
            rows.BuildActionsOf(choice, GameConfig.DefaultInteractChooserMaxRows);
            var actor = new InteractActor(Vector2.zero, Vector2.zero, InteractContext.OnFoot);

            Assert.IsTrue(InteractChooser.Open(rows, actor, WorldInteractor.DefaultFacingArcDegrees));
            _frame++;
            InteractChooser.Pick(ConversationChoice.GiveAction);
            Assert.AreEqual(0, gives, "the registry re-check is unchanged for everything else");
        }

        [Test]
        public void An_unregistered_row_whose_villager_walked_out_of_reach_does_nothing()
        {
            int gives = 0;
            var choice = new ConversationChoice(_ => { }, _ => gives++);
            var it = Spawn("v", new Vector3(0.5f, 0f, 0f)).AddComponent<Interactable>();
            choice.Bind(it, WorldStrings.GiveRowLabel(CodName), 2f);
            var rows = new InteractChoiceList();
            rows.BuildActionsOf(choice, GameConfig.DefaultInteractChooserMaxRows);
            var actor = new InteractActor(Vector2.zero, Vector2.zero, InteractContext.OnFoot);
            InteractChooser.OpenUnregistered(rows, actor, WorldInteractor.DefaultFacingArcDegrees);
            _frame++;

            it.transform.position = new Vector3(10f, 0f, 0f);
            InteractChooser.Pick(ConversationChoice.GiveAction);

            Assert.AreEqual(0, gives);
            Assert.IsFalse(InteractChooser.IsOpen);
        }
    }
}
