using System;
using System.Collections.Generic;
using System.Reflection;
using HiddenHarbours.Core;
using HiddenHarbours.Economy;
using HiddenHarbours.World;
using NUnit.Framework;
using UnityEngine;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// Buying at the shelf, slice 1 (owner ruling 2026-11-23: the player walks up to the item, its price
    /// appears, and accepting buys it; the clerk keeps a catalogue for special orders later). The shelf till
    /// sells exactly what the counter's book would have sold, through the same Confirm: one item, the price
    /// taken, the good put where it goes, one item off the shelf and the change told to the world. Refusals
    /// (an empty shelf, a short purse, full hands) move nothing and say why in the player's bubble. The book
    /// no longer lists what the shelf sells. And the shelf a player has bought from stays a pure function of
    /// the moment and his purchases through a supply cycle (rule 5).
    /// </summary>
    public class ShopAtTheShelfTests
    {
        private const string Seller = "seller.test_store";
        private const string Shop = "shop.test_store";
        private const string IceListing = "supply.test_ice";
        private const string SoapListing = "supply.test_soap";
        private const string IceLine = "line.test_ice_bag";
        private const string IceName = "Bag of Ice";
        private const int IcePrice = 6;
        private const int SoapPrice = 3;

        private const double SecondsPerDay = 1800.0;
        private const double Opens = 7.0, Closes = 19.0;
        private const int Cadence = 2, Lookback = 28;
        private const double BufferDays = 4.0;
        private static readonly MassVolume Plenty = new MassVolume(1_000_000.0, 1_000_000.0);

        private readonly List<UnityEngine.Object> _spawned = new List<UnityEngine.Object>();
        private readonly List<IslandShelvesChanged> _changes = new List<IslandShelvesChanged>();
        private readonly List<string> _cues = new List<string>();

        // ---- fakes -------------------------------------------------------------------------------------------

        private sealed class FakeWallet : IWallet
        {
            public FakeWallet(int money) { Money = money; }
            public int Money { get; private set; }
            public void Add(int amount) => Money += amount;
            public bool TrySpend(int amount)
            {
                if (amount < 0 || amount > Money) return false;
                Money -= amount;
                return true;
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

        private sealed class FakeShelves : IIslandShelves
        {
            public readonly Dictionary<string, int> Counts = new Dictionary<string, int>();
            public int Takes;
            public string ShopId => Shop;
            public bool HasLine(string lineId) => lineId != null && Counts.ContainsKey(lineId);
            public int MaxOnShelf(string lineId) => 20;
            public int CountAt(string lineId, double totalSeconds) => Counts[lineId];
            public bool RunCameOn(int day) => false;
            public int DaysSinceRun(int day) => -1;
            public bool TryTakeOne(string lineId, double totalSeconds)
            {
                if (Counts[lineId] < 1) return false;
                Counts[lineId]--;
                Takes++;
                return true;
            }
        }

        /// <summary>The counter's rows for ice and soap, as the game's build counts them.</summary>
        private sealed class CounterRows : IslandShelfTill.IRowSource
        {
            private readonly SupplyShop _counter;
            private readonly FakeWallet _wallet;
            private readonly SupplyDef _ice, _soap;

            public CounterRows(SupplyShop counter, FakeWallet wallet, SupplyDef ice, SupplyDef soap)
            {
                _counter = counter; _wallet = wallet; _ice = ice; _soap = soap;
            }

            public int Builds;

            public void Build(string sellerId, double now, List<BuyRow> into)
            {
                Builds++;
                into.Clear();
                into.Add(new BuyRow(_counter, _ice, _ice.Id, _ice.DisplayName, "", "",
                                    BuyLogic.Supply(_ice.Price, _wallet.Money), CatalogSection.Gear));
                into.Add(new BuyRow(_counter, _soap, _soap.Id, _soap.DisplayName, "", "",
                                    BuyLogic.Supply(_soap.Price, _wallet.Money), CatalogSection.Gear));
                BuyCatalog.ApplyShelves(sellerId, now, into);
            }
        }

        private sealed class Thing : ICarriable, IHandLoad
        {
            public Thing(HandLoad load) { HandLoad = load; }
            public HandLoad HandLoad { get; }
            public string DefId => "tool.test";
            public bool IsCarriable => true;
            public Transform Transform => null;
            public int BakedFacings => 0;
            public void ShowFacing(int facingIndex) { }
            public void RideSortingBand(int layer, int order) { }
            public void OnLifted(ICarrier carrier) { }
            public void OnPlaced() { }
        }

        private sealed class TwoHands : IHandsCarrier
        {
            public HandSlots Slots { get; } = new HandSlots();
            public ICarriable Carried => Slots.Right ?? Slots.Left;
            public bool IsCarrying => Carried != null;
        }

        /// <summary>The bag in his hands, as the shelf sees it through Core: answers the room it was told to.</summary>
        private sealed class FakeBagHands : IShoppingBagHands
        {
            public ShoppingBagRefusal Room = ShoppingBagRefusal.None;
            public readonly List<string> Bagged = new List<string>();
            public bool HoldsBag => Bagged.Count > 0;
            public ShoppingBagRefusal CanBag(ShoppingBagDef bag) => Room;
            public ShoppingBagRefusal TryBag(ShoppingBagDef bag, string supplyId)
            {
                if (Room != ShoppingBagRefusal.None) return Room;
                Bagged.Add(supplyId);
                return ShoppingBagRefusal.None;
            }
            public bool TryPutAway(string storageId) { Bagged.Clear(); return true; }
        }
        // ---- setup -------------------------------------------------------------------------------------------

        [SetUp]
        public void SetUp()
        {
            EventBus.Clear<IslandShelvesChanged>();
            EventBus.Clear<SupplyPurchased>();
            EventBus.Clear<InnerVoiceCue>();
            IslandCounterShelves.Clear();
            IslandShelvesDirectory.Clear();
            ShelfTills.Clear();
            _changes.Clear();
            _cues.Clear();
            EventBus.Subscribe<IslandShelvesChanged>(OnChanged);
            EventBus.Subscribe<InnerVoiceCue>(OnCue);
        }

        [TearDown]
        public void TearDown()
        {
            EventBus.Unsubscribe<IslandShelvesChanged>(OnChanged);
            EventBus.Unsubscribe<InnerVoiceCue>(OnCue);
            EventBus.Clear<IslandShelvesChanged>();
            EventBus.Clear<SupplyPurchased>();
            EventBus.Clear<InnerVoiceCue>();
            IslandCounterShelves.Clear();
            GameServices.Reset();
            foreach (UnityEngine.Object o in _spawned)
                if (o != null) UnityEngine.Object.DestroyImmediate(o);
            _spawned.Clear();
        }

        private void OnChanged(IslandShelvesChanged c) => _changes.Add(c);
        private void OnCue(InnerVoiceCue c) => _cues.Add(c.CueId);

        // ---- builders ----------------------------------------------------------------------------------------

        private SupplyDef Supply(string id, string name, int price)
        {
            var s = ScriptableObject.CreateInstance<SupplyDef>();
            s.Id = id; s.DisplayName = name; s.Price = price;
            _spawned.Add(s);
            return s;
        }

        private SupplyShop Counter(IWallet wallet)
        {
            var go = new GameObject("TestCounter");
            _spawned.Add(go);
            var shop = go.AddComponent<SupplyShop>();
            typeof(SupplyShop).GetField("_wallet", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(shop, wallet);
            typeof(SupplyShop).GetField("_sellerId", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(shop, Seller);
            return shop;
        }

        private sealed class Store
        {
            public SaveData Save;
            public FakeWallet Wallet;
            public CounterRows Rows;
            public IslandShelfTill Till;
        }

        /// <summary>A store whose ice is sold at the shelf (bound to <paramref name="shelves"/>) and whose
        /// soap is still sold from the book; its till registered for the shop.</summary>
        private Store MakeStore(IIslandShelves shelves, int money, bool iceAtShelf = true)
        {
            var store = new Store { Save = new SaveData(), Wallet = new FakeWallet(money) };
            GameServices.Save = new FakeSave(store.Save);
            IslandCounterShelves.Bind(Seller, IceListing, shelves, IceLine, iceAtShelf);
            store.Rows = new CounterRows(Counter(store.Wallet), store.Wallet, Supply(IceListing, IceName, IcePrice),
                                         Supply(SoapListing, "Bar of Soap", SoapPrice));
            store.Till = new IslandShelfTill(Shop, Seller, store.Rows);
            ShelfTills.Register(store.Till);
            return store;
        }

        private IslandGoodDef Good(string id, float units)
        {
            var g = ScriptableObject.CreateInstance<IslandGoodDef>();
            g.Id = id;
            g.Source = GoodSource.MainlandOverBar;
            g.DailyUnits = units;
            g.DailyMassKg = units;
            g.DailyVolumeLitres = units;
            g.PlayerAllowanceFraction = 0f;
            g.WinterMultiplier = 1f;
            _spawned.Add(g);
            return g;
        }

        /// <summary>Real shelves over a synthetic back room: 20 units of ice a day, 5 to a bag, 16 bags at most.</summary>
        private IslandStoreShelves RealShelves(Func<int, bool> possible)
        {
            IslandGoodDef ice = Good("islandgood.test_ice", 20f);
            var ledger = new IslandStoreLedger(new[] { ice }, Plenty, Cadence, BufferDays, Lookback, possible);
            var lines = new List<IslandShelfLine>
            {
                new IslandShelfLine { Id = IceLine, Good = ice, ItemName = "bag", UnitsPerItem = 5f, MaxOnShelf = 16,
                                      SoldAtShelf = true },
            };
            return new IslandStoreShelves(Shop, ledger, lines, SecondsPerDay, Opens, Closes);
        }

        private static double At(int day, double hour) => (day + hour / 24.0) * SecondsPerDay;

        private ShelfGood MakeGood(bool needsFreeHand, Func<bool> inside = null)
        {
            var go = new GameObject("TestShelfGood");
            _spawned.Add(go);
            go.SetActive(false);
            var good = go.AddComponent<ShelfGood>();
            good.Configure("shelfslots.test_store.slot.ice", Shop, IceListing, 1f, needsFreeHand, inside);
            go.SetActive(true);
            return good;
        }

        private ShelfGood MakeBaggedGood(ShoppingBagDef bag)
        {
            var go = new GameObject("TestBaggedShelfGood");
            _spawned.Add(go);
            go.SetActive(false);
            var good = go.AddComponent<ShelfGood>();
            good.Configure("shelfslots.test_store.slot.ice", Shop, IceListing, 1f, false, null, bag);
            go.SetActive(true);
            return good;
        }

        private ShoppingBagDef Bag()
        {
            var bag = ScriptableObject.CreateInstance<ShoppingBagDef>();
            bag.Id = "shoppingbag.test";
            bag.Capacity = 2;
            _spawned.Add(bag);
            return bag;
        }
        // ---- the till: buy one -------------------------------------------------------------------------------

        [Test]
        public void TheShelfQuotesNameAndPrice_AndBuyingTakesTheMoneyOneItemAndTellsTheWorld()
        {
            var shelves = new FakeShelves();
            shelves.Counts[IceLine] = 3;
            Store store = MakeStore(shelves, 100);

            Assert.IsTrue(store.Till.TryQuote(IceListing, 0.0, out ShelfQuote q));
            Assert.That(q.DisplayName, Is.EqualTo(IceName));
            Assert.That(q.Price, Is.EqualTo(IcePrice));
            Assert.That(q.OnShelf, Is.EqualTo(3));
            Assert.IsFalse(q.SoldOut);

            Assert.That(store.Till.TryBuyOne(IceListing, 0.0), Is.EqualTo(ShelfBuyResult.Bought));
            Assert.That(store.Wallet.Money, Is.EqualTo(100 - IcePrice), "the price is taken");
            Assert.That(SupplyLocker.Count(store.Save, IceListing), Is.EqualTo(1), "into the supply locker, as at the counter");
            Assert.That(shelves.Counts[IceLine], Is.EqualTo(2), "one item off the shelf");
            Assert.That(shelves.Takes, Is.EqualTo(1));
            Assert.That(_changes.Count, Is.EqualTo(1));
            Assert.That(_changes[0].ShopId, Is.EqualTo(Shop));
            Assert.That(_changes[0].LineId, Is.EqualTo(IceLine));
            Assert.That(_changes[0].Count, Is.EqualTo(2));

            Assert.IsTrue(store.Till.TryQuote(IceListing, 0.0, out q));
            Assert.That(q.OnShelf, Is.EqualTo(2), "the quote reads the live shelf");
        }

        [Test]
        public void AQuoteIsCheap_ItLearnsTheNameOnce_AndOnlyAPurchaseRebuildsTheRows()
        {
            var shelves = new FakeShelves();
            shelves.Counts[IceLine] = 3;
            Store store = MakeStore(shelves, 100);
            for (int i = 0; i < 10; i++) Assert.IsTrue(store.Till.TryQuote(IceListing, 0.0, out _));
            Assert.That(store.Rows.Builds, Is.EqualTo(1), "a quote reads data learned once, then the shelf");
            store.Till.TryBuyOne(IceListing, 0.0);
            Assert.That(store.Rows.Builds, Is.EqualTo(2));
        }

        [Test]
        public void AnEmptyShelfIsRefused_NothingMoves()
        {
            var shelves = new FakeShelves();
            shelves.Counts[IceLine] = 0;
            Store store = MakeStore(shelves, 1000);

            Assert.IsTrue(store.Till.TryQuote(IceListing, 0.0, out ShelfQuote q));
            Assert.IsTrue(q.SoldOut);
            Assert.That(store.Till.TryBuyOne(IceListing, 0.0), Is.EqualTo(ShelfBuyResult.SoldOut));
            Assert.That(store.Wallet.Money, Is.EqualTo(1000));
            Assert.That(SupplyLocker.Count(store.Save, IceListing), Is.EqualTo(0));
            Assert.That(shelves.Takes, Is.EqualTo(0));
            Assert.That(_changes, Is.Empty);
        }

        [Test]
        public void AShortPurseIsRefused_NothingMoves()
        {
            var shelves = new FakeShelves();
            shelves.Counts[IceLine] = 5;
            Store store = MakeStore(shelves, IcePrice - 1);

            Assert.That(store.Till.TryBuyOne(IceListing, 0.0), Is.EqualTo(ShelfBuyResult.TooDear));
            Assert.That(store.Wallet.Money, Is.EqualTo(IcePrice - 1));
            Assert.That(SupplyLocker.Count(store.Save, IceListing), Is.EqualTo(0));
            Assert.That(shelves.Counts[IceLine], Is.EqualTo(5));
            Assert.That(_changes, Is.Empty);
        }

        [Test]
        public void AGoodTheBookStillSellsIsNotSoldAtTheShelf()
        {
            var shelves = new FakeShelves();
            shelves.Counts[IceLine] = 5;
            Store store = MakeStore(shelves, 100, iceAtShelf: false);

            Assert.IsFalse(store.Till.TryQuote(IceListing, 0.0, out _));
            Assert.IsFalse(store.Till.TryQuote(SoapListing, 0.0, out _), "an uncounted good is the book's");
            Assert.That(store.Till.TryBuyOne(IceListing, 0.0), Is.EqualTo(ShelfBuyResult.NotSold));
            Assert.That(store.Till.TryBuyOne(SoapListing, 0.0), Is.EqualTo(ShelfBuyResult.NotSold));
            Assert.That(store.Wallet.Money, Is.EqualTo(100));
        }

        [Test]
        public void TheJudgeReadsTheRowAsTheBookBuiltIt()
        {
            var shelves = new FakeShelves();
            shelves.Counts[IceLine] = 1;
            Store store = MakeStore(shelves, 100);
            var rows = new List<BuyRow>();
            store.Rows.Build(Seller, 0.0, rows);
            Assert.That(IslandShelfTill.Judge(rows[0]), Is.EqualTo(ShelfBuyResult.Bought));
            Assert.That(IslandShelfTill.Judge(rows[1]), Is.EqualTo(ShelfBuyResult.NotSold), "soap is on no shelf");
        }

        // ---- the book ----------------------------------------------------------------------------------------

        [Test]
        public void TheBookDropsWhatTheShelfSells_AndKeepsEveryOtherRow()
        {
            var shelves = new FakeShelves();
            shelves.Counts[IceLine] = 5;
            Store store = MakeStore(shelves, 100);
            var rows = new List<BuyRow>();
            store.Rows.Build(Seller, 0.0, rows);
            Assert.That(rows.Count, Is.EqualTo(2));

            BuyCatalog.DropSoldAtShelf(Seller, rows);
            Assert.That(rows.Count, Is.EqualTo(1));
            Assert.That(rows[0].Id, Is.EqualTo(SoapListing), "the soap stays in the book");

            IslandCounterShelves.Clear();
            IslandCounterShelves.Bind(Seller, IceListing, shelves, IceLine, atShelf: false);
            store.Rows.Build(Seller, 0.0, rows);
            BuyCatalog.DropSoldAtShelf(Seller, rows);
            Assert.That(rows.Count, Is.EqualTo(2), "a counted good the counter still sells stays in the book");
        }

        // ---- the shelf good in the world ---------------------------------------------------------------------

        [Test]
        public void TheGoodSaysItsNameAndPrice_AndSoldOutWhenTheShelfIsEmpty()
        {
            var shelves = new FakeShelves();
            shelves.Counts[IceLine] = 1;
            MakeStore(shelves, 100);
            ShelfGood good = MakeGood(false);

            Assert.IsFalse(good.IsAvailable, "not offered before it has read the till");
            good.Refresh(0.0);
            Assert.IsTrue(good.IsAvailable);
            Assert.That(good.VerbLabel, Is.EqualTo("Buy " + IceName + " — " + MoneyFormat.Balance(IcePrice)));
            Assert.That(good.ListingId, Is.EqualTo(IceListing));

            Assert.That(good.Press(0.0), Is.EqualTo(ShelfBuyResult.Bought));
            Assert.That(_cues, Is.EqualTo(new[] { InnerVoiceCues.ShelfBought }));
            Assert.IsTrue(good.SoldOut);
            Assert.That(good.VerbLabel, Is.EqualTo(IceName + " — sold out"));
            Assert.IsTrue(good.IsAvailable, "an empty shelf is still pressed, and says so");

            Assert.That(good.Press(0.0), Is.EqualTo(ShelfBuyResult.SoldOut));
            Assert.That(_cues[1], Is.EqualTo(InnerVoiceCues.ShelfSoldOut));
        }

        [Test]
        public void TheGoodSaysWhyWhenThePurseIsShort()
        {
            var shelves = new FakeShelves();
            shelves.Counts[IceLine] = 4;
            Store store = MakeStore(shelves, 2);
            ShelfGood good = MakeGood(false);
            good.Refresh(0.0);

            Assert.That(good.Press(0.0), Is.EqualTo(ShelfBuyResult.TooDear));
            Assert.That(_cues, Is.EqualTo(new[] { InnerVoiceCues.ShelfTooDear }));
            Assert.That(store.Wallet.Money, Is.EqualTo(2));
            Assert.That(shelves.Counts[IceLine], Is.EqualTo(4));
        }

        [Test]
        public void AGoodCarriedOffInAHandIsRefusedWhenBothHandsAreFull_BeforeAnyMoneyMoves()
        {
            var shelves = new FakeShelves();
            shelves.Counts[IceLine] = 4;
            Store store = MakeStore(shelves, 100);
            var hands = new TwoHands();
            Assert.That(hands.Slots.TryTake(new Thing(HandLoad.Tool), false, CarryHandSide.Right, out _),
                        Is.EqualTo(HandSlotRefusal.None));
            Assert.That(hands.Slots.TryTake(new Thing(HandLoad.Container), false, CarryHandSide.Left, out _),
                        Is.EqualTo(HandSlotRefusal.None));
            GameServices.Hands = hands;

            ShelfGood good = MakeGood(needsFreeHand: true);
            good.Refresh(0.0);
            Assert.That(good.Press(0.0), Is.EqualTo(ShelfBuyResult.HandsFull));
            Assert.That(_cues, Is.EqualTo(new[] { InnerVoiceCues.ShelfHandsFull }));
            Assert.That(store.Wallet.Money, Is.EqualTo(100));
            Assert.That(shelves.Counts[IceLine], Is.EqualTo(4));

            ShelfGood toLocker = MakeGood(needsFreeHand: false);
            toLocker.Refresh(0.0);
            Assert.That(toLocker.Press(0.0), Is.EqualTo(ShelfBuyResult.Bought), "a good that goes to the locker needs no hand");

            var oneFree = new TwoHands();
            oneFree.Slots.TryTake(new Thing(HandLoad.Tool), false, CarryHandSide.Right, out _);
            GameServices.Hands = oneFree;
            Assert.That(good.Press(0.0), Is.EqualTo(ShelfBuyResult.Bought), "one free hand is enough");
        }

        // ---- slice 2: into the paper bag ---------------------------------------------------------------------

        [Test]
        public void ABaggedGoodIsBoughtAndGoesIntoTheBag_AndSaysSo()
        {
            var shelves = new FakeShelves();
            shelves.Counts[IceLine] = 4;
            Store store = MakeStore(shelves, 100);
            var hands = new FakeBagHands();
            GameServices.BagHands = hands;

            ShelfGood good = MakeBaggedGood(Bag());
            good.Refresh(0.0);
            Assert.That(good.Press(0.0), Is.EqualTo(ShelfBuyResult.Bought));
            Assert.That(hands.Bagged, Is.EqualTo(new[] { IceListing }), "into the bag");
            Assert.That(store.Wallet.Money, Is.EqualTo(100 - IcePrice));
            Assert.That(SupplyLocker.Owned(store.Save, IceListing), Is.EqualTo(1), "bought into his supplies, set aside in the bag");
            Assert.That(shelves.Counts[IceLine], Is.EqualTo(3));
            Assert.That(_cues, Is.EqualTo(new[] { InnerVoiceCues.ShelfBagged }));

            Assert.That(good.Press(0.0), Is.EqualTo(ShelfBuyResult.Bought), "a second good goes in the same bag");
            Assert.That(hands.Bagged.Count, Is.EqualTo(2));
        }

        [Test]
        public void AFullBagIsRefusedBeforeAnyMoneyMoves()
        {
            var shelves = new FakeShelves();
            shelves.Counts[IceLine] = 4;
            Store store = MakeStore(shelves, 100);
            GameServices.BagHands = new FakeBagHands { Room = ShoppingBagRefusal.BagFull };

            ShelfGood good = MakeBaggedGood(Bag());
            good.Refresh(0.0);
            Assert.That(good.Press(0.0), Is.EqualTo(ShelfBuyResult.BagFull));
            Assert.That(_cues, Is.EqualTo(new[] { InnerVoiceCues.ShelfBagFull }));
            Assert.That(store.Wallet.Money, Is.EqualTo(100));
            Assert.That(shelves.Counts[IceLine], Is.EqualTo(4));
            Assert.That(SupplyLocker.Owned(store.Save, IceListing), Is.EqualTo(0));
        }

        [Test]
        public void NoHandForABagIsRefusedAsHandsFull_BeforeAnyMoneyMoves()
        {
            var shelves = new FakeShelves();
            shelves.Counts[IceLine] = 4;
            Store store = MakeStore(shelves, 100);
            GameServices.BagHands = new FakeBagHands { Room = ShoppingBagRefusal.HandsFull };

            ShelfGood good = MakeBaggedGood(Bag());
            good.Refresh(0.0);
            Assert.That(good.Press(0.0), Is.EqualTo(ShelfBuyResult.HandsFull));
            Assert.That(_cues, Is.EqualTo(new[] { InnerVoiceCues.ShelfHandsFull }));
            Assert.That(store.Wallet.Money, Is.EqualTo(100));
            Assert.That(shelves.Counts[IceLine], Is.EqualTo(4));
        }

        [Test]
        public void WithNoHandsOrNoGameTheGoodIsBoughtIntoHisSupplies_AsBefore()
        {
            var shelves = new FakeShelves();
            shelves.Counts[IceLine] = 4;
            Store store = MakeStore(shelves, 100);
            ShelfGood good = MakeBaggedGood(Bag());
            good.Refresh(0.0);

            Assert.That(good.Press(0.0), Is.EqualTo(ShelfBuyResult.Bought), "no hands registered");
            Assert.That(_cues, Is.EqualTo(new[] { InnerVoiceCues.ShelfBought }));

            var hands = new FakeBagHands { Room = ShoppingBagRefusal.NoSave };
            GameServices.BagHands = hands;
            _cues.Clear();
            Assert.That(good.Press(0.0), Is.EqualTo(ShelfBuyResult.Bought), "no game loaded");
            Assert.That(hands.Bagged, Is.Empty);
            Assert.That(_cues, Is.EqualTo(new[] { InnerVoiceCues.ShelfBought }));
            Assert.That(SupplyLocker.Count(store.Save, IceListing), Is.EqualTo(2));
        }
        [Test]
        public void TheGoodIsOfferedOnlyInside_AndNotForAStoreWithNoTill()
        {
            var shelves = new FakeShelves();
            shelves.Counts[IceLine] = 4;
            MakeStore(shelves, 100);
            bool inside = false;
            ShelfGood good = MakeGood(false, () => inside);
            good.Refresh(0.0);
            Assert.IsFalse(good.IsAvailable, "not from outside or upstairs");
            inside = true;
            Assert.IsTrue(good.IsAvailable);

            ShelfTills.Clear();
            good.Refresh(0.0);
            Assert.IsFalse(good.IsAvailable, "no till, nothing offered");
            Assert.That(good.Press(0.0), Is.EqualTo(ShelfBuyResult.NotSold));
            Assert.That(_cues, Is.Empty, "nothing to say about a till that is not there");
        }

        [Test]
        public void TheSlotShowsGoodsWhileStocked_AnEmptyShelfBehindTheCard_AndHidesTheItemSlotsCards()
        {
            var shelves = new FakeShelves();
            shelves.Counts[IceLine] = 1;
            IslandShelvesDirectory.Register(shelves);
            Store store = MakeStore(shelves, 100);

            Sprite cover = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), Vector2.zero);
            Sprite goods = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), Vector2.zero);
            _spawned.Add(cover); _spawned.Add(goods);
            var def = ScriptableObject.CreateInstance<ShopShelfSlotsDef>();
            _spawned.Add(def);
            def.Id = "shelfslots.test_store";
            def.ShopId = Shop;
            def.ReachMeters = 1.25f;
            def.Slots = new List<ShopShelfSlot>
            {
                new ShopShelfSlot { Id = "slot.ice", LineId = IceLine, Offset = new Vector2(1f, 0.5f), Cover = cover, Goods = goods },
                new ShopShelfSlot { Id = "slot.ice_bag", LineId = IceLine, Offset = new Vector2(1.25f, 0.5f),
                                    ListingId = IceListing, HideSoldOutCard = true },
            };
            var go = new GameObject("TestSigns");
            _spawned.Add(go);
            var signs = go.AddComponent<SoldOutSigns>();
            signs.SetInsideTest(() => true);
            signs.Configure(def, null);

            Assert.IsNull(signs.ShelfGoods[0], "a slot with no listing sells nothing itself");
            ShelfGood good = signs.ShelfGoods[1];
            Assert.IsNotNull(good);
            Assert.That(good.Id, Is.EqualTo("shelfslots.test_store.slot.ice_bag"));
            Assert.That(good.ReachMeters, Is.EqualTo(1.25f));
            Assert.That((Vector2)good.transform.position, Is.EqualTo(new Vector2(1.25f, 0.5f)));

            signs.Refresh(0.0, inside: true);
            Assert.IsTrue(signs.Covers[0].enabled, "the empty shelf is drawn over the painted one");
            Assert.IsTrue(signs.Goods[0].enabled, "stock on the shelf, goods drawn");
            Assert.IsFalse(signs.Cards[0].enabled);
            Assert.That(good.VerbLabel, Does.StartWith("Buy "));

            Assert.That(good.Press(0.0), Is.EqualTo(ShelfBuyResult.Bought));
            Assert.That(store.Wallet.Money, Is.EqualTo(100 - IcePrice));
            signs.Refresh(0.0, inside: true);
            Assert.IsTrue(signs.Covers[0].enabled);
            Assert.IsFalse(signs.Goods[0].enabled, "sold out: the goods are gone, the shelf behind the card is empty");
            Assert.IsTrue(signs.Cards[0].enabled, "one card for the line");
            Assert.IsFalse(signs.Cards[1].enabled, "the item slot on the same line carries no second card");

            signs.Refresh(0.0, inside: false);
            Assert.IsFalse(signs.Covers[0].enabled || signs.Goods[0].enabled || signs.Cards[0].enabled, "outside, nothing is drawn");
        }

        // ---- determinism through a supply cycle --------------------------------------------------------------

        [Test]
        public void ShelfPurchasesAreAPureFunctionOfTheMomentAndThePurchases_ThroughASupplyCycle()
        {
            Func<int, bool> possible = _ => true;
            IslandStoreShelves watched = RealShelves(possible);
            IslandStoreShelves slept = RealShelves(possible);
            IslandStoreShelves untouched = RealShelves(possible);

            const int buyDay = Lookback + 4;
            double buyAt = At(buyDay, Opens + 1.0);
            int before = untouched.CountAt(IceLine, buyAt);
            Assert.That(before, Is.GreaterThan(2), "a stocked morning, under the shelf's cap");

            // The same two purchases at the same moment, through the till, on two copies of the shelves.
            Store store = MakeStore(watched, 1000);
            Assert.That(store.Till.TryBuyOne(IceListing, buyAt), Is.EqualTo(ShelfBuyResult.Bought));
            Assert.That(store.Till.TryBuyOne(IceListing, buyAt), Is.EqualTo(ShelfBuyResult.Bought));
            Assert.That(slept.TryTakeOne(IceLine, buyAt) && slept.TryTakeOne(IceLine, buyAt), Is.True);
            Assert.That(watched.CountAt(IceLine, buyAt), Is.EqualTo(before - 2), "two bought, two fewer on the shelf");

            // One copy is read every three hours for ten days; the other only now and then, out of order.
            var seen = new Dictionary<int, int>();
            for (int day = buyDay; day <= buyDay + 10; day++)
                for (int h = 0; h < 24; h += 3)
                    seen[day * 24 + h] = watched.CountAt(IceLine, At(day, h));
            for (int day = buyDay + 10; day >= buyDay; day -= 3)
                Assert.That(slept.CountAt(IceLine, At(day, 9)), Is.EqualTo(seen[day * 24 + 9]), "day " + day);

            // A run that tops the room up clears the purchases: from then on the shelf is the untouched one.
            int sameFrom = -1;
            for (int day = buyDay + 1; day <= buyDay + 10; day++)
            {
                bool same = true;
                for (int h = 0; h < 24; h += 3)
                    same &= seen[day * 24 + h] == untouched.CountAt(IceLine, At(day, h));
                if (same && sameFrom < 0) sameFrom = day;
                if (!same) sameFrom = -1;
            }
            Assert.That(sameFrom, Is.GreaterThan(buyDay).And.LessThanOrEqualTo(buyDay + Cadence + 1),
                        "the next run's top-up leaves the shelf as if nothing had been bought");
            TestContext.WriteLine($"bought day {buyDay}; the shelf matches the untouched one from day {sameFrom}");
        }
    }
}
