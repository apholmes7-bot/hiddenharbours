using System.Collections;
using System.Collections.Generic;
using HiddenHarbours.Core;
using HiddenHarbours.Player;
using HiddenHarbours.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>
    /// <b>Fill a paper bag at the shelf and put the shopping away</b> (buying at the shelf, slice 2; owner ruling
    /// 2026-11-23), joined up in a running scene with the real two-hand carry: the first press on a bagged good
    /// puts a paper bag in one of his hands, a second goes into the same bag, a full bag refuses before any money
    /// moves, and walking to his own storage offers "Put the shopping away", which empties the bag into his
    /// supplies and the bag leaves his hand. A full pair of hands with no bag among them refuses the good. The
    /// bag can be set down with the goods inside and picked up again (slice 2b): away from a shelf, a bed and
    /// his storage the press stands it at his feet, a shelf sells nothing while it stands there, lying down to
    /// sleep ashore leaves it by the bed, a new game does not inherit it, and the bag is in the save (v26): a
    /// loaded game has it where it was left, the picture is made again when it is missing, and after the game is
    /// closed and started again he picks it up from beside the bed with the same goods. The
    /// till is a plain double that puts what it sells into the save's supplies, as the store's real till does.
    /// Headless-safe: nothing renders.
    /// </summary>
    public class ShoppingBagPlayTests
    {
        private const string ShopId = "shop.test_store";
        private const string Listing = "supply.test_bread";
        private const string PantryId = "storage.test_pantry";

        private sealed class Till : IShelfTill
        {
            public SaveData Save;
            public int OnShelf = 5;
            public int Price = 4;
            public int Money = 100;
            public int Buys;
            public string ShopId => ShoppingBagPlayTests.ShopId;

            public bool TryQuote(string listingId, double totalSeconds, out ShelfQuote quote)
            {
                quote = new ShelfQuote(listingId, "Loaf of Bread", Price, OnShelf);
                return listingId == Listing;
            }

            public ShelfBuyResult TryBuyOne(string listingId, double totalSeconds)
            {
                if (listingId != Listing) return ShelfBuyResult.NotSold;
                if (OnShelf <= 0) return ShelfBuyResult.SoldOut;
                if (Money < Price) return ShelfBuyResult.TooDear;
                Money -= Price; OnShelf--; Buys++;
                SupplyLocker.Add(Save, listingId, 1);
                return ShelfBuyResult.Bought;
            }
        }

        private sealed class FakeSave : ISaveService
        {
            public FakeSave(SaveData data) { Current = data; }
            public SaveData Current { get; }
            public int Saves;
            public bool GetFlag(string key) => false;
            public void SetFlag(string key, bool value) { }
            public void Save() { Saves++; }
        }

        /// <summary>His storage, as any of his containers registers it.</summary>
        private sealed class Pantry : MonoBehaviour, IStorageContainer
        {
            public string StorageId => PantryId;
            public int StoragePriority => 0;
        }

        /// <summary>A pail: a container that takes a hand, so a pail and a bag cannot share the pair.</summary>
        private sealed class Pail : MonoBehaviour, ICarriable, IHandLoad
        {
            public HandLoad HandLoad => HandLoad.Container;
            public string DefId => "tool.test_pail";
            public bool IsCarriable => true;
            public Transform Transform => transform;
            public int BakedFacings => 1;
            public void ShowFacing(int facingIndex) { }
            public void RideSortingBand(int layer, int order) { }
            public void OnLifted(ICarrier carrier) { }
            public void OnPlaced() { }
        }

        private readonly List<Object> _spawned = new();
        private readonly List<string> _cues = new();
        private readonly List<ShoppingPutAway> _putAway = new();
        private System.Action<InnerVoiceCue> _onCue;
        private System.Action<ShoppingPutAway> _onPutAway;
        private readonly List<ShoppingBagSetDown> _setDown = new();
        private readonly List<ShoppingBagPickedUp> _pickedUp = new();
        private System.Action<ShoppingBagSetDown> _onSetDown;
        private System.Action<ShoppingBagPickedUp> _onPickedUp;
        private SaveData _save;
        private Till _till;
        private CarryHands _hands;
        private ShelfGood _bread;
        private Pantry _pantry;

        private T Track<T>(T o) where T : Object { _spawned.Add(o); return o; }

        [SetUp]
        public void SetUp()
        {
            GameServices.Reset();
            Interactables.Clear();
            PlayerStorage.Clear();
            _cues.Clear();
            _putAway.Clear();
            _onCue = c => _cues.Add(c.CueId);
            _onPutAway = p => _putAway.Add(p);
            EventBus.Subscribe(_onCue);
            EventBus.Subscribe(_onPutAway);
            ShoppingBagContents.Clear();
            PlayerStorey.Clear();
            _setDown.Clear();
            _pickedUp.Clear();
            _onSetDown = s => _setDown.Add(s);
            _onPickedUp = p => _pickedUp.Add(p);
            EventBus.Subscribe(_onSetDown);
            EventBus.Subscribe(_onPickedUp);

            _save = new SaveData();
            GameServices.Save = new FakeSave(_save);
            _till = new Till { Save = _save };
            ShelfTills.Register(_till);

            var bag = Track(ScriptableObject.CreateInstance<ShoppingBagDef>());
            bag.Id = "shoppingbag.test_paper";
            bag.DisplayName = "Paper bag";
            bag.Capacity = 2;
            bag.PutAwayReachMeters = 1.5f;
            _bagDef = bag;
            // After a restart the bag's kind is found from its saved id.
            ShoppingBagContents.BagSource = id => id == bag.Id ? bag : null;

            _hands = NewPlayer(Vector2.zero);

            var shelf = Track(new GameObject("Bread"));
            shelf.SetActive(false);
            shelf.transform.position = new Vector3(0.5f, 0f, 0f);
            _bread = shelf.AddComponent<ShelfGood>();
            _bread.Configure("shelfslots.test.slot.bread", ShopId, Listing, 1.5f, false, () => true, bag);
            shelf.SetActive(true);
            _bread.Refresh(0.0);

            var pantry = Track(new GameObject("Pantry"));
            pantry.transform.position = new Vector3(12f, 0f, 0f);
            _pantry = pantry.AddComponent<Pantry>();
            PlayerStorage.Register(_pantry);
        }

        [TearDown]
        public void TearDown()
        {
            EventBus.Unsubscribe(_onCue);
            EventBus.Unsubscribe(_onPutAway);
            EventBus.Unsubscribe(_onSetDown);
            EventBus.Unsubscribe(_onPickedUp);
            // A bag set down stands at the scene root, not under the player: it is cleared by hand.
            if (_hands != null && _hands.Bag != null) Object.Destroy(_hands.Bag.gameObject);
            if (CarriableShoppingBag.Standing != null) Object.Destroy(CarriableShoppingBag.Standing.gameObject);
            ShoppingBagContents.BagSource = null;
            ShoppingBagContents.Clear();
            PlayerStorey.Clear();
            foreach (Object o in _spawned)
                if (o != null) Object.Destroy(o);
            _spawned.Clear();
            ShelfTills.Clear();
            PlayerStorage.Clear();
            Interactables.Clear();
            GameServices.Reset();
        }

        private ShoppingBagDef _bagDef;

        private CarryHands NewPlayer(Vector2 at)
        {
            var player = Track(new GameObject("Player"));
            player.transform.position = at;
            player.AddComponent<SpriteRenderer>();
            return player.AddComponent<CarryHands>();
        }

        /// <summary>The game is closed and started again: the save goes through its real JSON, nothing in
        /// memory survives (the player, the bag's picture, what the session knew), and the game is loaded
        /// with the player standing at <paramref name="wakeAt"/>.</summary>
        private IEnumerator CloseTheGameAndStartItAgain(Vector2 wakeAt)
        {
            string onDisk = SaveSerialization.ToJson(_save);
            if (CarriableShoppingBag.Standing != null) Object.Destroy(CarriableShoppingBag.Standing.gameObject);
            Object.Destroy(_hands.gameObject);
            yield return null;
            ShoppingBagContents.Clear();
            PlayerStorey.Clear();

            _save = SaveSerialization.FromJson(onDisk);
            GameServices.Save = new FakeSave(_save);
            _till.Save = _save;
            _hands = NewPlayer(wakeAt);
            EventBus.Publish(new GameLoaded());
            yield return null;
            yield return null;
        }

        private Vector2 At => _hands.transform.position;

        private IInteractable Best()
        {
            var actor = new InteractActor(At, Vector2.zero, InteractContext.OnFoot);
            return InteractResolver.TryResolveNow(actor, 120f, out IInteractable best) ? best : null;
        }

        private void Press()
        {
            var actor = new InteractActor(At, Vector2.zero, InteractContext.OnFoot);
            Assert.IsTrue(InteractResolver.TryResolveNow(actor, 120f, out IInteractable best), "something is offered");
            best.Interact(actor);
        }

        [UnityTest]
        public IEnumerator HeFillsTheBag_WalksHome_AndPutsTheShoppingAway()
        {
            yield return null;
            Assert.IsFalse(_hands.HoldsBag, "empty-handed to start");

            Press();
            yield return null;
            Assert.IsTrue(_hands.HoldsBag, "a paper bag appears in his hand");
            CarriableShoppingBag bag = _hands.HeldBag;
            Assert.IsNotNull(bag);
            Assert.That(_hands.Slots.SideOf(bag), Is.Not.EqualTo(CarryHandSide.None), "it is in one hand");
            Assert.That(ShoppingBagContents.InBag(_save, Listing), Is.EqualTo(1));
            Assert.That(SupplyLocker.Count(_save, Listing), Is.EqualTo(0), "in the bag, not yet in his storage");
            Assert.That(_cues, Is.EqualTo(new[] { InnerVoiceCues.ShelfBagged }));

            Press();
            yield return null;
            Assert.That(_hands.HeldBag, Is.SameAs(bag), "the second loaf goes in the same bag");
            Assert.That(ShoppingBagContents.Total(_save), Is.EqualTo(2));
            Assert.That(_till.Buys, Is.EqualTo(2));

            Press();
            yield return null;
            Assert.That(_till.Buys, Is.EqualTo(2), "a full bag buys nothing");
            Assert.That(_till.Money, Is.EqualTo(100 - 2 * _till.Price));
            Assert.That(_cues[_cues.Count - 1], Is.EqualTo(InnerVoiceCues.ShelfBagFull));

            // Walk home a step a frame; the put-away is offered once he is in reach of his storage.
            string offered = null;
            for (int frame = 0; frame < 200 && offered != CarriableShoppingBag.PutAwayLabel; frame++)
            {
                _hands.transform.position = Vector2.MoveTowards(At, _pantry.transform.position, 0.1f);
                yield return null;
                offered = Best()?.VerbLabel;
            }
            Assert.That(offered, Is.EqualTo(CarriableShoppingBag.PutAwayLabel), "his storage offers the put-away");
            Assert.That(Vector2.Distance(At, _pantry.transform.position), Is.LessThanOrEqualTo(1.5f + 1e-3f));
            Assert.IsTrue(_hands.HoldsBag, "he carried the bag the whole way");

            Press();
            yield return null;
            Assert.IsFalse(_hands.HoldsBag, "the bag leaves his hand");
            Assert.IsTrue(bag == null, "and is gone");
            Assert.That(SupplyLocker.Count(_save, Listing), Is.EqualTo(2), "both loaves are in his storage");
            Assert.IsFalse(ShoppingBagContents.HoldsAny(_save));
            Assert.That(_putAway.Count, Is.EqualTo(1));
            Assert.That(_putAway[0].StorageId, Is.EqualTo(PantryId));
            Assert.That(_putAway[0].Count, Is.EqualTo(2));
            Assert.That(_cues[_cues.Count - 1], Is.EqualTo(InnerVoiceCues.ShoppingPutAway));
        }

        [UnityTest]
        public IEnumerator APailInHandRefusesTheBag_BeforeAnyMoneyMoves()
        {
            var pail = Track(new GameObject("Pail")).AddComponent<Pail>();
            Assert.That(_hands.TryPickUp(pail), Is.EqualTo(CarryRefusal.None));
            yield return null;

            Press();
            yield return null;
            Assert.IsFalse(_hands.HoldsBag);
            Assert.That(_till.Buys, Is.EqualTo(0));
            Assert.That(_till.Money, Is.EqualTo(100));
            Assert.That(_cues, Is.EqualTo(new[] { InnerVoiceCues.ShelfHandsFull }));
        }

        // ---- the bag set down (owner ruling 2026-11-23: droppable with the items inside) -------------------------

        private static readonly Vector2 OpenGround = new Vector2(-6f, 0f);
        private static readonly Vector2 ByTheBed = new Vector2(-9f, 3f);

        private IEnumerator StandAt(Vector2 at)
        {
            _hands.transform.position = at;
            yield return null;
        }

        [UnityTest]
        public IEnumerator HeSetsTheBagDown_WalksOff_AndPicksItUpWithEverythingInIt()
        {
            yield return null;
            Press();
            yield return null;
            CarriableShoppingBag bag = _hands.HeldBag;
            Assert.IsNotNull(bag);
            Assert.AreSame(_bread, Best(), "at the shelf the press still buys: the bag is not set down there");

            yield return StandAt(OpenGround);
            Assert.That(Best()?.VerbLabel, Is.EqualTo(CarriableShoppingBag.SetDownLabel), "nothing else in reach");
            Press();
            yield return null;
            Assert.IsFalse(_hands.HoldsBag, "the bag leaves his hand");
            Assert.IsTrue(_hands.BagIsSetDown);
            Assert.IsTrue(bag != null, "and stands on the ground");
            Assert.That(_hands.Slots.SideOf(bag), Is.EqualTo(CarryHandSide.None));
            Assert.That((Vector2)bag.transform.position, Is.EqualTo(OpenGround), "at his feet");
            Assert.IsFalse(bag.transform.IsChildOf(_hands.transform), "it does not follow him");
            Assert.That(ShoppingBagContents.InBag(_save, Listing), Is.EqualTo(1), "the loaf is still in it");
            Assert.That(SupplyLocker.Count(_save, Listing), Is.EqualTo(0), "and not put away behind his back");
            Assert.That(_setDown.Count, Is.EqualTo(1));
            Assert.That(_setDown[0].Position, Is.EqualTo(OpenGround));
            Assert.That(_setDown[0].Count, Is.EqualTo(1));
            Assert.IsFalse(_setDown[0].ByTheBed);

            // Back at the shelf without it: one bag at a time, and no money moves.
            yield return StandAt(Vector2.zero);
            Assert.That((Vector2)bag.transform.position, Is.EqualTo(OpenGround), "the bag stayed where he left it");
            Press();
            yield return null;
            Assert.That(_till.Buys, Is.EqualTo(1), "nothing bought");
            Assert.That(_till.Money, Is.EqualTo(100 - _till.Price));
            Assert.That(_cues[_cues.Count - 1], Is.EqualTo(InnerVoiceCues.ShelfBagElsewhere));
            Assert.IsFalse(_hands.HoldsBag, "and no second bag appears");

            yield return StandAt(OpenGround);
            Assert.That(Best()?.VerbLabel, Is.EqualTo(CarriableShoppingBag.PickUpLabel));
            Press();
            yield return null;
            Assert.That(_hands.HeldBag, Is.SameAs(bag), "the same bag, back in his hand");
            Assert.IsFalse(_hands.BagIsSetDown);
            Assert.That(ShoppingBagContents.InBag(_save, Listing), Is.EqualTo(1), "with the loaf in it");
            Assert.That(_pickedUp.Count, Is.EqualTo(1));
            Assert.That(_pickedUp[0].Count, Is.EqualTo(1));

            yield return StandAt(Vector2.zero);
            Press();
            yield return null;
            Assert.That(_till.Buys, Is.EqualTo(2), "and the shelf sells into it again");
            Assert.That(ShoppingBagContents.Total(_save), Is.EqualTo(2));
        }

        [UnityTest]
        public IEnumerator LyingDownToSleepLeavesTheBagByTheBed_AndABunkAboardKeepsItInHisHand()
        {
            yield return null;
            Press();
            yield return null;
            CarriableShoppingBag bag = _hands.HeldBag;
            yield return StandAt(ByTheBed);

            // A bunk aboard: nothing set down on a deck rides the hull, so the bag stays in his hand.
            EventBus.Publish(new RestSaveRequested("the bunk", ByTheBed, 0, true, "", true, "hull.test",
                                                   Vector2.zero, 0f));
            yield return null;
            Assert.IsTrue(_hands.HoldsBag, "aboard, he keeps hold of it");
            Assert.That(_setDown.Count, Is.EqualTo(0));

            // A bed ashore: he lies down and the bag is left where he stood to turn in.
            EventBus.Publish(new RestSaveRequested("his bed", ByTheBed, 0, "bed.test"));
            yield return null;
            Assert.IsFalse(_hands.HoldsBag, "he does not sleep holding the shopping");
            Assert.IsTrue(_hands.BagIsSetDown);
            Assert.IsTrue(ShoppingBagContents.SetDownByTheBed);
            Assert.That((Vector2)bag.transform.position, Is.EqualTo(ByTheBed), "by the bed");
            Assert.That(_setDown.Count, Is.EqualTo(1));
            Assert.IsTrue(_setDown[0].ByTheBed);
            Assert.That(ShoppingBagContents.InBag(_save, Listing), Is.EqualTo(1), "with the loaf still in it");
            Assert.That(SupplyLocker.Count(_save, Listing), Is.EqualTo(0), "not put away for him");

            // A second request with nothing in hand changes nothing (the bed asks once per lie-down).
            EventBus.Publish(new RestSaveRequested("his bed", ByTheBed, 0, "bed.test"));
            yield return null;
            Assert.That(_setDown.Count, Is.EqualTo(1));

            // He wakes beside it and picks it up.
            Assert.That(Best()?.VerbLabel, Is.EqualTo(CarriableShoppingBag.PickUpLabel));
            Press();
            yield return null;
            Assert.That(_hands.HeldBag, Is.SameAs(bag));
            Assert.That(ShoppingBagContents.Total(_save), Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator WithBothHandsTaken_TheBagOnTheGroundStaysThere()
        {
            yield return null;
            Press();
            yield return null;
            CarriableShoppingBag bag = _hands.HeldBag;
            yield return StandAt(OpenGround);
            Assert.That(_hands.TrySetBagDown(), Is.EqualTo(CarryRefusal.None));

            var pail = Track(new GameObject("Pail")).AddComponent<Pail>();
            Assert.That(_hands.TryPickUp(pail), Is.EqualTo(CarryRefusal.None));
            yield return null;

            Assert.IsFalse(bag.PickUp(), "a pail and a bag do not share the pair");
            Assert.IsFalse(_hands.HoldsBag);
            Assert.IsTrue(_hands.BagIsSetDown, "the bag is still where he left it");
            Assert.That(_cues[_cues.Count - 1], Is.EqualTo(InnerVoiceCues.ShelfHandsFull));
            Assert.That(ShoppingBagContents.Total(_save), Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator ComingBackToTheGame_TheBagStandsWhereHeLeftIt_AndALostPictureIsMadeAgainThere()
        {
            yield return null;
            Press();
            yield return null;
            CarriableShoppingBag bag = _hands.HeldBag;
            yield return StandAt(OpenGround);
            Assert.That(_hands.TrySetBagDown(), Is.EqualTo(CarryRefusal.None));
            yield return StandAt(ByTheBed);
            Assert.IsFalse(_hands.HoldsBag);

            // The title and back: the same game, loaded again. The bag is where it was left.
            EventBus.Publish(new GameLoaded());
            yield return null;
            yield return null;
            Assert.IsFalse(_hands.HoldsBag, "it is not put back in his hand");
            Assert.IsTrue(_hands.BagIsSetDown);
            Assert.That(_hands.Bag, Is.SameAs(bag), "the same bag");
            Assert.That((Vector2)bag.transform.position, Is.EqualTo(OpenGround), "where he left it");
            Assert.That(ShoppingBagContents.InBag(_save, Listing), Is.EqualTo(1));

            // The place the bag stood in is unloaded under it: the picture is made again where the save says.
            Object.Destroy(bag.gameObject);
            yield return null;
            yield return null;
            CarriableShoppingBag again = _hands.Bag;
            Assert.IsNotNull(again, "the bag is drawn again");
            Assert.IsFalse(_hands.HoldsBag, "still on the ground");
            Assert.IsTrue(_hands.BagIsSetDown);
            Assert.That((Vector2)again.transform.position, Is.EqualTo(OpenGround));
            Assert.That(ShoppingBagContents.InBag(_save, Listing), Is.EqualTo(1), "nothing lost");
            Assert.That(SupplyLocker.Owned(_save, Listing), Is.EqualTo(1), "and nothing doubled");

            yield return StandAt(OpenGround);
            Assert.That(Best()?.VerbLabel, Is.EqualTo(CarriableShoppingBag.PickUpLabel));
            Press();
            yield return null;
            Assert.That(_hands.HeldBag, Is.SameAs(again));
        }

        // ---- the bag across a restart (save v26) -----------------------------------------------------------------

        [UnityTest]
        public IEnumerator HeBuysAtTheShelf_SetsTheBagDown_Sleeps_RestartsTheGame_AndPicksItUpWithTheSameGoods()
        {
            yield return null;
            Press();
            yield return null;
            Press();
            yield return null;
            Assert.That(ShoppingBagContents.Total(_save), Is.EqualTo(2), "two loaves in the bag");
            int moneyLeft = _till.Money;

            // He walks to the bed and lies down: the bag is left by the bed, and the sleep is the save.
            yield return StandAt(ByTheBed);
            EventBus.Publish(new RestSaveRequested("his bed", ByTheBed, 0, "bed.test"));
            yield return null;
            Assert.IsTrue(_hands.BagIsSetDown);

            yield return CloseTheGameAndStartItAgain(ByTheBed + new Vector2(0.4f, 0f));

            Assert.IsFalse(_hands.HoldsBag, "he wakes with empty hands");
            Assert.IsTrue(_hands.BagIsSetDown, "the bag is in the save, set down");
            Assert.IsTrue(ShoppingBagContents.SetDownByTheBed);
            CarriableShoppingBag bag = _hands.Bag;
            Assert.IsNotNull(bag, "and stands in the world again");
            Assert.That(bag.Def, Is.SameAs(_bagDef), "the same kind of bag");
            Assert.That((Vector2)bag.transform.position, Is.EqualTo(ByTheBed), "by the bed, where he left it");
            Assert.That(ShoppingBagContents.InBag(_save, Listing), Is.EqualTo(2), "with both loaves in it");
            Assert.That(SupplyLocker.Owned(_save, Listing), Is.EqualTo(2), "nothing doubled");
            Assert.That(SupplyLocker.Count(_save, Listing), Is.EqualTo(0), "and nothing put away behind his back");

            // One bag at a time survives the restart too.
            Assert.That(_hands.CanBag(_bagDef), Is.EqualTo(ShoppingBagRefusal.BagElsewhere));

            yield return StandAt(ByTheBed);
            Assert.That(Best()?.VerbLabel, Is.EqualTo(CarriableShoppingBag.PickUpLabel));
            Press();
            yield return null;
            Assert.That(_hands.HeldBag, Is.SameAs(bag), "he picks it up");
            Assert.That(ShoppingBagContents.InBag(_save, Listing), Is.EqualTo(2), "with the same goods");
            Assert.That(_pickedUp.Count, Is.EqualTo(1));
            Assert.That(_pickedUp[0].Count, Is.EqualTo(2));
            Assert.That(_till.Money, Is.EqualTo(moneyLeft), "nothing was paid twice");

            // And home it goes.
            yield return StandAt(_pantry.transform.position);
            Press();
            yield return null;
            Assert.That(SupplyLocker.Count(_save, Listing), Is.EqualTo(2), "both loaves are in his storage");
            Assert.That(_save.ShoppingBags.Count, Is.EqualTo(0));
        }

        [UnityTest]
        public IEnumerator ABagInHisHandIsInHisHandAfterARestart()
        {
            yield return null;
            Press();
            yield return null;
            Assert.IsTrue(_hands.HoldsBag);

            yield return CloseTheGameAndStartItAgain(OpenGround);

            Assert.IsTrue(_hands.HoldsBag, "in his hand, as it was");
            Assert.IsFalse(_hands.BagIsSetDown);
            Assert.That(_hands.HeldBag.Def, Is.SameAs(_bagDef));
            Assert.That(ShoppingBagContents.InBag(_save, Listing), Is.EqualTo(1));
            Assert.That(SupplyLocker.Count(_save, Listing), Is.EqualTo(0), "still set aside");

            yield return StandAt(Vector2.zero);
            Press();
            yield return null;
            Assert.That(ShoppingBagContents.Total(_save), Is.EqualTo(2), "and the shelf sells into it again");
        }

        [UnityTest]
        public IEnumerator ABagLeftUpstairsIsNotOfferedDownstairsAfterARestart()
        {
            yield return null;
            Press();
            yield return null;
            yield return StandAt(OpenGround);
            var house = new object();
            PlayerStorey.Report(house, true, 1);
            Assert.That(_hands.TrySetBagDown(), Is.EqualTo(CarryRefusal.None));
            Assert.That(_save.ShoppingBags[0].Level, Is.EqualTo(1), "set down on the floor above");

            yield return CloseTheGameAndStartItAgain(OpenGround);

            Assert.IsNotNull(_hands.Bag, "the bag stands in the house");
            Assert.IsFalse(_hands.Bag.IsAvailable, "but he is downstairs: it is not offered");
            PlayerStorey.Report(house, true, 1);
            yield return null;
            Assert.IsTrue(_hands.Bag.IsAvailable, "upstairs it is");
            Assert.That(Best()?.VerbLabel, Is.EqualTo(CarriableShoppingBag.PickUpLabel));
        }

        [UnityTest]
        public IEnumerator ABuyAtTheShelfAsksForOneWrite_WithTheBagInIt()
        {
            yield return null;
            var save = (FakeSave)GameServices.Save;
            int before = save.Saves;
            Press();
            yield return null;
            Assert.That(save.Saves, Is.GreaterThan(before), "the bagged purchase is written");
            Assert.That(_save.ShoppingBags.Count, Is.EqualTo(1));
            Assert.That(_save.ShoppingBags[0].BagDefId, Is.EqualTo(_bagDef.Id), "the row names its kind, so the load repair keeps it");
        }

        [UnityTest]
        public IEnumerator ANewGameDoesNotInheritTheBag()
        {
            yield return null;
            Press();
            yield return null;
            yield return StandAt(OpenGround);
            Assert.That(_hands.TrySetBagDown(), Is.EqualTo(CarryRefusal.None));
            CarriableShoppingBag bag = _hands.Bag;
            Assert.IsNotNull(bag);

            // A new game is begun under him: another save altogether.
            var fresh = new SaveData();
            GameServices.Save = new FakeSave(fresh);
            _till.Save = fresh;
            EventBus.Publish(new GameLoaded());
            yield return null;
            yield return null;
            Assert.IsFalse(_hands.HoldsBag, "the bag of the game that was left is not his");
            Assert.IsNull(_hands.Bag);
            Assert.IsTrue(bag == null, "and it no longer stands in the world");
            Assert.IsFalse(ShoppingBagContents.Any);
            Assert.That(SupplyLocker.Owned(fresh, Listing), Is.EqualTo(0), "the new game owns none of it");

            yield return StandAt(Vector2.zero);
            Press();
            yield return null;
            Assert.IsTrue(_hands.HoldsBag, "and the shelf starts a new bag for it");
            Assert.That(ShoppingBagContents.InBag(fresh, Listing), Is.EqualTo(1));
        }
    }
}
