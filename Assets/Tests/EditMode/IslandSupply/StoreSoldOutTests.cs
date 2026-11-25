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
    /// The store's shelves at the counter and on the shelves (owner ruling 2026-11-23: a sold-out sign where
    /// the product normally sits; the counter cannot sell what is not there). The seller's book refuses a good
    /// whose shelf is empty, with no money taken and nothing given; buying is capped by what the shelf holds and
    /// each sale takes one item off it; the sold-out card follows the same shelf read through a supply cycle
    /// (stocked, sold out, restocked); and every read is a pure function of the moment (rule 5).
    /// </summary>
    public class StoreSoldOutTests
    {
        private const string Seller = "seller.test_store";
        private const string Shop = "shop.test_store";
        private const string IceListing = "supply.test_ice";
        private const string IceLine = "line.test_ice_bag";
        private const int IcePrice = 6;

        // Synthetic shelf model, small numbers (the IslandStoreShelvesTests shapes).
        private const double SecondsPerDay = 1800.0;
        private const double Opens = 7.0, Closes = 19.0;
        private const int Cadence = 2, Lookback = 28;
        private const double BufferDays = 4.0;
        private static readonly MassVolume Plenty = new MassVolume(1_000_000.0, 1_000_000.0);

        private readonly List<UnityEngine.Object> _spawned = new List<UnityEngine.Object>();
        private readonly List<IslandShelvesChanged> _changes = new List<IslandShelvesChanged>();

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

        /// <summary>A shelf whose counts the test sets; time does not move it.</summary>
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

        [SetUp]
        public void SetUp()
        {
            EventBus.Clear<IslandShelvesChanged>();
            EventBus.Clear<SupplyPurchased>();
            IslandCounterShelves.Clear();
            IslandShelvesDirectory.Clear();
            _changes.Clear();
            EventBus.Subscribe<IslandShelvesChanged>(OnChanged);
        }

        [TearDown]
        public void TearDown()
        {
            EventBus.Unsubscribe<IslandShelvesChanged>(OnChanged);
            EventBus.Clear<IslandShelvesChanged>();
            EventBus.Clear<SupplyPurchased>();
            IslandCounterShelves.Clear();
            GameServices.Reset();
            foreach (UnityEngine.Object o in _spawned)
                if (o != null) UnityEngine.Object.DestroyImmediate(o);
            _spawned.Clear();
        }

        private void OnChanged(IslandShelvesChanged c) => _changes.Add(c);

        // ---- builders ----------------------------------------------------------------------------------------

        private SupplyDef Ice()
        {
            var s = ScriptableObject.CreateInstance<SupplyDef>();
            s.Id = IceListing; s.DisplayName = "Bag of Ice"; s.Price = IcePrice;
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

        /// <summary>The book's rows for one ice listing, counted against the bound shelves, as the game builds them.</summary>
        private static List<BuyRow> Rows(SupplyShop counter, SupplyDef ice, int money, double now)
        {
            var rows = new List<BuyRow>
            {
                new BuyRow(counter, ice, ice.Id, ice.DisplayName, "", "", BuyLogic.Supply(ice.Price, money),
                           CatalogSection.Gear),
            };
            BuyCatalog.ApplyShelves(Seller, now, rows);
            return rows;
        }

        private IslandGoodDef Good(string id, float units, float kg, float litres)
        {
            var g = ScriptableObject.CreateInstance<IslandGoodDef>();
            g.Id = id;
            g.Source = GoodSource.MainlandOverBar;
            g.DailyUnits = units;
            g.DailyMassKg = kg;
            g.DailyVolumeLitres = litres;
            g.PlayerAllowanceFraction = 0f;
            g.WinterMultiplier = 1f;
            _spawned.Add(g);
            return g;
        }

        /// <summary>Real shelves over a synthetic back room: 20 units of ice a day, 5 to a bag, 16 bags at most.</summary>
        private IslandStoreShelves RealShelves(Func<int, bool> possible)
        {
            IslandGoodDef ice = Good("islandgood.test_ice", 20f, 20f, 20f);
            var ledger = new IslandStoreLedger(new[] { ice }, Plenty, Cadence, BufferDays, Lookback, possible);
            var lines = new List<IslandShelfLine>
            {
                new IslandShelfLine { Id = IceLine, Good = ice, ItemName = "bag", UnitsPerItem = 5f, MaxOnShelf = 16 },
            };
            return new IslandStoreShelves(Shop, ledger, lines, SecondsPerDay, Opens, Closes);
        }

        private static double At(int day, double hour) => (day + hour / 24.0) * SecondsPerDay;

        private ShopShelfSlotsDef Slots()
        {
            var def = ScriptableObject.CreateInstance<ShopShelfSlotsDef>();
            def.Id = "shelfslots.test_store";
            def.ShopId = Shop;
            def.BuildingName = "testStore";
            def.Slots = new List<ShopShelfSlot>
            {
                new ShopShelfSlot { Id = "slot.ice", LineId = IceLine, Offset = new Vector2(1f, 0.5f) },
                new ShopShelfSlot { Id = "slot.unknown", LineId = "line.not_on_these_shelves", Offset = Vector2.zero },
            };
            _spawned.Add(def);
            return def;
        }

        private SoldOutSigns Signs(ShopShelfSlotsDef def)
        {
            var go = new GameObject("TestSigns");
            _spawned.Add(go);
            var signs = go.AddComponent<SoldOutSigns>();
            signs.Configure(def, null);
            return signs;
        }

        // ---- the counter ---------------------------------------------------------------------------------------

        [Test]
        public void TheBookCountsOnlyTheRowsAShelfHolds_AndSaysHowManyAreLeft()
        {
            var shelves = new FakeShelves();
            shelves.Counts[IceLine] = 3;
            IslandCounterShelves.Bind(Seller, IceListing, shelves, IceLine);
            SupplyDef ice = Ice();
            SupplyShop counter = Counter(new FakeWallet(100));

            List<BuyRow> rows = Rows(counter, ice, 100, 0.0);
            Assert.That(rows[0].Quote.OnShelf, Is.EqualTo(3));
            Assert.IsTrue(rows[0].Quote.CanBuy);
            Assert.That(rows[0].Note, Does.Contain("3 on the shelf"));
            Assert.That(rows[0].ShelfLine, Is.EqualTo(IceLine));

            IslandCounterShelves.Clear();
            List<BuyRow> unbound = Rows(counter, ice, 100, 0.0);
            Assert.That(unbound[0].Quote.OnShelf, Is.EqualTo(BuyQuote.NotCounted), "a listing no shelf counts is untouched");
            Assert.IsNull(unbound[0].Shelves);
            Assert.IsTrue(unbound[0].Quote.CanBuy);
        }

        [Test]
        public void AnEmptyShelfIsRefused_NoMoneyTaken_NothingGiven_AndTheBookSaysSoldOut()
        {
            var save = new SaveData();
            GameServices.Save = new FakeSave(save);
            var wallet = new FakeWallet(1000);
            var shelves = new FakeShelves();
            shelves.Counts[IceLine] = 0;
            IslandCounterShelves.Bind(Seller, IceListing, shelves, IceLine);
            SupplyShop counter = Counter(wallet);
            SupplyDef ice = Ice();

            BuyRow row = Rows(counter, ice, wallet.Money, 0.0)[0];
            Assert.IsTrue(row.Quote.SoldOut);
            Assert.IsTrue(row.Quote.Affordable, "a full purse does not buy what is not there");
            Assert.IsFalse(row.Quote.CanBuy);
            Assert.That(CatalogBook.StateOf(row), Is.EqualTo(CatalogRowState.SoldOut));
            Assert.That(new CatalogBook(8).StatusFor(row), Is.EqualTo(CatalogBook.SoldOutStatus));
            Assert.That(row.Note, Does.Contain("None left on the shelf"));

            Assert.IsFalse(BuyCatalog.Confirm(row, 0.0));
            Assert.That(wallet.Money, Is.EqualTo(1000), "no money taken");
            Assert.That(SupplyLocker.Count(save, IceListing), Is.EqualTo(0), "nothing given");
            Assert.That(shelves.Takes, Is.EqualTo(0));
            Assert.That(_changes, Is.Empty);
        }

        [Test]
        public void TheCounterAsksTheShelfAgainAtConfirm_SoABookOpenedBeforeItEmptiedStillRefuses()
        {
            var save = new SaveData();
            GameServices.Save = new FakeSave(save);
            var wallet = new FakeWallet(1000);
            var shelves = new FakeShelves();
            shelves.Counts[IceLine] = 1;
            IslandCounterShelves.Bind(Seller, IceListing, shelves, IceLine);
            BuyRow row = Rows(Counter(wallet), Ice(), wallet.Money, 0.0)[0];
            Assert.IsTrue(row.Quote.CanBuy);

            shelves.Counts[IceLine] = 0;   // the last bag went while the book was open
            Assert.IsFalse(BuyCatalog.Confirm(row, 0.0));
            Assert.That(wallet.Money, Is.EqualTo(1000));
            Assert.That(SupplyLocker.Count(save, IceListing), Is.EqualTo(0));
        }

        [Test]
        public void BuyingIsCappedByWhatTheShelfHolds_AndEachSaleTakesOneItemAndTellsTheWorld()
        {
            var save = new SaveData();
            GameServices.Save = new FakeSave(save);
            var wallet = new FakeWallet(1000);
            var shelves = new FakeShelves();
            shelves.Counts[IceLine] = 2;
            IslandCounterShelves.Bind(Seller, IceListing, shelves, IceLine);
            SupplyShop counter = Counter(wallet);
            SupplyDef ice = Ice();

            int bought = 0;
            for (int attempt = 0; attempt < 5; attempt++)
                if (BuyCatalog.Confirm(Rows(counter, ice, wallet.Money, 0.0)[0], 0.0)) bought++;

            Assert.That(bought, Is.EqualTo(2), "two bags on the shelf, two bags sold");
            Assert.That(wallet.Money, Is.EqualTo(1000 - 2 * IcePrice));
            Assert.That(SupplyLocker.Count(save, IceListing), Is.EqualTo(2));
            Assert.That(shelves.Counts[IceLine], Is.EqualTo(0));
            Assert.That(_changes.Count, Is.EqualTo(2));
            Assert.That(_changes[0].Count, Is.EqualTo(1));
            Assert.That(_changes[1].Count, Is.EqualTo(0));
            Assert.That(_changes[1].ShopId, Is.EqualTo(Shop));
            Assert.That(_changes[1].LineId, Is.EqualTo(IceLine));
        }

        [Test]
        public void ThePlayersPurchasesComeOffTheRecomputedShelf_ForTheSession()
        {
            var save = new SaveData();
            GameServices.Save = new FakeSave(save);
            var wallet = new FakeWallet(1000);
            IslandStoreShelves shelves = RealShelves(_ => true);
            IslandCounterShelves.Bind(Seller, IceListing, shelves, IceLine);
            SupplyShop counter = Counter(wallet);
            SupplyDef ice = Ice();

            double now = At(Lookback + 4, Opens);   // a stocked morning, past the replay's warm-up
            int before = shelves.CountAt(IceLine, now);
            Assert.That(before, Is.GreaterThan(1));
            Assert.IsTrue(BuyCatalog.Confirm(Rows(counter, ice, wallet.Money, now)[0], now));
            Assert.That(shelves.CountAt(IceLine, now), Is.EqualTo(before - 1));
            Assert.That(shelves.HeldDrawCount, Is.EqualTo(1), "held in memory: saving it waits for the save-backed slice");
        }

        // ---- the sign ---------------------------------------------------------------------------------------

        [Test]
        public void TheSignFollowsTheShelfThroughASupplyCycle_StockedSoldOutRestocked()
        {
            // The bar is shut from day 36 to day 45: the shelf thins to nothing, then the run refills it.
            const int shutFrom = Lookback + 8, shutTo = Lookback + 17;
            IslandStoreShelves shelves = RealShelves(d => d < shutFrom || d > shutTo);
            IslandShelvesDirectory.Register(shelves);
            SoldOutSigns signs = Signs(Slots());
            Assert.That(signs.Cards.Length, Is.EqualTo(2));

            int stockedDay = -1, soldOutDay = -1, restockedDay = -1;
            for (int day = Lookback; day <= shutTo + 4; day++)
            {
                double t = At(day, Opens);
                int count = shelves.CountAt(IceLine, t);
                signs.Refresh(t, inside: true);
                Assert.That(signs.Cards[0].enabled, Is.EqualTo(count == 0), "day " + day + ": the card is the empty shelf");
                Assert.IsFalse(signs.Cards[1].enabled, "a slot whose line these shelves do not hold shows nothing");
                Assert.That(SoldOutSignRule.IsSoldOut(shelves, IceLine, t), Is.EqualTo(count == 0));

                if (count > 0 && soldOutDay < 0) stockedDay = day;
                if (count == 0 && stockedDay >= 0 && soldOutDay < 0) soldOutDay = day;
                if (count > 0 && soldOutDay >= 0 && restockedDay < 0) restockedDay = day;
            }
            Assert.That(stockedDay, Is.GreaterThanOrEqualTo(0), "stocked before the gap");
            Assert.That(soldOutDay, Is.GreaterThan(stockedDay), "sold out in the gap");
            Assert.That(restockedDay, Is.GreaterThan(shutTo), "restocked once the run gets over again");
            TestContext.WriteLine($"stocked to day {stockedDay}, sold out day {soldOutDay}, restocked day {restockedDay}");
        }

        [Test]
        public void TheSignShowsOnlyInsideAndOnlyWhenTheShelvesAreKnown()
        {
            ShopShelfSlotsDef def = Slots();
            SoldOutSigns signs = Signs(def);
            signs.Refresh(0.0, inside: true);
            Assert.IsFalse(signs.Cards[0].enabled, "no shelves registered: the store looks as it always has");

            var shelves = new FakeShelves();
            shelves.Counts[IceLine] = 0;
            IslandShelvesDirectory.Register(shelves);
            signs.Refresh(0.0, inside: false);
            Assert.IsFalse(signs.Cards[0].enabled, "outside, the room and its cards are not drawn");
            signs.Refresh(0.0, inside: true);
            Assert.IsTrue(signs.Cards[0].enabled);
            Assert.That((Vector2)signs.Cards[0].transform.localPosition, Is.EqualTo(def.Slots[0].Offset));

            shelves.Counts[IceLine] = 4;
            signs.Refresh(0.0, inside: true);
            Assert.IsFalse(signs.Cards[0].enabled, "stock back, card gone");
        }

        // ---- determinism ------------------------------------------------------------------------------------

        [Test]
        public void TheCardsAreAPureFunctionOfTheMoment_WatchedOrSleptThrough()
        {
            Func<int, bool> possible = d => d % 9 < 4;
            IslandStoreShelves watched = RealShelves(possible);
            var seen = new Dictionary<int, bool>();
            for (int day = 0; day < Lookback + 30; day++)
                for (int h = 0; h < 24; h += 3)
                    seen[day * 24 + h] = SoldOutSignRule.IsSoldOut(watched, IceLine, At(day, h));

            IslandStoreShelves slept = RealShelves(possible);
            for (int day = Lookback + 29; day >= Lookback; day -= 5)
                Assert.That(SoldOutSignRule.IsSoldOut(slept, IceLine, At(day, 9)), Is.EqualTo(seen[day * 24 + 9]),
                            "day " + day);
        }
    }
}
