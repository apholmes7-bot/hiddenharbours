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
            public bool GetFlag(string key) => false;
            public void SetFlag(string key, bool value) { }
            public void Save() { }
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

            _save = new SaveData();
            GameServices.Save = new FakeSave(_save);
            _till = new Till { Save = _save };
            ShelfTills.Register(_till);

            var bag = Track(ScriptableObject.CreateInstance<ShoppingBagDef>());
            bag.Id = "shoppingbag.test_paper";
            bag.DisplayName = "Paper bag";
            bag.Capacity = 2;
            bag.PutAwayReachMeters = 1.5f;

            var player = Track(new GameObject("Player"));
            player.AddComponent<SpriteRenderer>();
            _hands = player.AddComponent<CarryHands>();

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
            foreach (Object o in _spawned)
                if (o != null) Object.Destroy(o);
            _spawned.Clear();
            ShelfTills.Clear();
            PlayerStorage.Clear();
            Interactables.Clear();
            GameServices.Reset();
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

        [UnityTest]
        public IEnumerator AReloadMidShopFindsTheGoodsPutAway_AndTheBagLeavesHisHand()
        {
            yield return null;
            Press();
            yield return null;
            Assert.IsTrue(_hands.HoldsBag);

            // A different game is loaded under him (the save never holds the bag).
            var reloaded = new SaveData();
            SupplyLocker.Add(reloaded, Listing, 1);
            GameServices.Save = new FakeSave(reloaded);
            yield return null;
            yield return null;
            Assert.IsFalse(_hands.HoldsBag, "the bag of a game no longer loaded is not held");
            Assert.That(SupplyLocker.Count(reloaded, Listing), Is.EqualTo(1), "the loaf is found put away");
        }
    }
}
