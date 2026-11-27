using System;
using System.Collections.Generic;
using System.Linq;
using HiddenHarbours.Core;
using HiddenHarbours.Economy;
using NUnit.Framework;
using UnityEngine;

namespace HiddenHarbours.Tests.Economy
{
    public sealed class SpecialOrderBookTests
    {
        private const string Player = "player.first";
        private const string Seller = "seller.test", Line = "specialorder.herring", Item = "bait.herring";
        private readonly List<SpecialOrderBook> _books = new();
        private readonly List<SpecialOrderReceipt> _placed = new(), _arrived = new(), _collected = new();
        private SaveData _save;
        private TestWallet _wallet;
        private TestClock _clock;
        private SpecialOrderBook _book;
        private List<BaitStock> _bait;

        // The pre-existing transaction/repair cases supply explicit external ownership for every
        // fixture row, including damaged ids. Session ownership is exercised separately below.
        private sealed class FixtureAccounts : ISpecialOrderAccounts
        {
            private readonly IWallet _wallet;
            public FixtureAccounts(IWallet wallet) => _wallet = wallet;
            public string OwnerOf(string orderId) => Player;
            public bool TryPay(string playerId, string orderId, int amount) =>
                playerId == Player && _wallet.TrySpend(amount);
        }

        private sealed class TestWallet : IWallet
        {
            private readonly SaveData _save;
            public TestWallet(SaveData save = null) => _save = save ?? new SaveData { Money = 1000 };
            public int Money => _save.Money;
            public int Calls;
            public bool Refuse;
            public Action DuringSpend;
            public void Add(int amount) => _save.Money += amount;
            public bool TrySpend(int amount)
            {
                Calls++;
                DuringSpend?.Invoke();
                if (Refuse || amount > Money) return false;
                _save.Money -= amount;
                return true;
            }
        }

        private sealed class TestClock : IGameClock
        {
            private const double SecondsInDay = 1200;
            public double TotalSeconds { get; private set; }
            public GameTime Now => new GameTime(TotalSeconds);
            public int DayIndex => (int)Math.Floor(TotalSeconds / SecondsInDay);
            public Season Season => Season.EarlySpring;
            public int Year => 1;
            public int DayOfSeason => DayIndex + 1;
            public Weekday Weekday => Weekday.Monday;
            public bool IsMarketDay => false;
            public float HourOfDay => (float)(TotalSeconds % SecondsInDay / SecondsInDay * 24);
            public float DayFraction => HourOfDay / 24;
            public bool IsPaused { get; set; }
            public float TimeScale { get; set; } = 1;
            public void SeekTo(double seconds) => TotalSeconds = seconds;
            public void At(int day, double hour = 0) => SeekTo(day * SecondsInDay + hour / 24 * SecondsInDay);
            public void StartDay(int day)
            {
                At(day);
                EventBus.Publish(new DayStarted(DayOfSeason, Season, Year));
            }
        }

        [SetUp]
        public void SetUp()
        {
            _save = SaveMigration.NewGame();
            _save.Money = 1000;
            _wallet = new TestWallet(_save); _clock = new TestClock();
            _bait = new List<BaitStock>();
            _placed.Clear(); _arrived.Clear(); _collected.Clear();
            EventBus.Subscribe<SpecialOrderPlaced>(OnPlaced);
            EventBus.Subscribe<SpecialOrderArrived>(OnArrived);
            EventBus.Subscribe<SpecialOrderCollected>(OnCollected);
            _book = Book(save: _save);
        }

        private void OnPlaced(SpecialOrderPlaced e) => _placed.Add(e.Order);
        private void OnArrived(SpecialOrderArrived e) => _arrived.Add(e.Order);
        private void OnCollected(SpecialOrderCollected e) => _collected.Add(e.Order);

        [TearDown]
        public void TearDown()
        {
            foreach (var book in _books) book.Dispose();
            _books.Clear();
            EventBus.Unsubscribe<SpecialOrderPlaced>(OnPlaced);
            EventBus.Unsubscribe<SpecialOrderArrived>(OnArrived);
            EventBus.Unsubscribe<SpecialOrderCollected>(OnCollected);
        }

        private SpecialOrderBook Book(int price = 3, int wait = 2, int limit = 20,
            TestWallet wallet = null, TestClock clock = null, SaveData save = null, SpecialOrderSupplySchedule supply = null)
        {
            var book = new SpecialOrderBook(save ?? SaveMigration.NewGame(),
                new[] { new SpecialOrderLine(Line, Item, Seller, price, wait, limit) },
                new FixtureAccounts(wallet ?? _wallet), clock ?? _clock, supply ?? Supply());
            _books.Add(book);
            return book;
        }

        // Most transaction cases use a daily test carrier confirmed at the day tick.
        // Calendar/boundary cases below use the two-day cadence and daylight unloading explicitly.
        private static SpecialOrderSupplySchedule Supply(int cadence = 1, double unloadHour = 0,
            Func<int, bool> possible = null)
        {
            possible ??= _ => true;
            bool Runs(int day) => IslandRunCalendar.RunsOn(day, cadence, possible);
            return new SpecialOrderSupplySchedule((seller, day) => Runs(day), (seller, seconds) =>
            {
                int day = (int)Math.Floor(seconds / 1200 - unloadHour / 24);
                while (day >= 0 && !Runs(day)) day--;
                return day >= 0 ? day : (int?)null;
            }, 28);
        }

        private string Place(int quantity = 1)
        {
            Assert.That(_book.Place(Player, Seller, Line, quantity, out string id), Is.EqualTo(SpecialOrderRefusal.None));
            return id;
        }

        private static string Json(SpecialOrderBook book) => JsonUtility.ToJson(book.Capture(Player));

        private static void AssertSavedOrdersEqual(SaveData expected, SaveData actual)
        {
            Assert.That(actual, Is.Not.Null);
            Assert.That(actual.NextSpecialOrderNumber, Is.EqualTo(expected.NextSpecialOrderNumber));
            Assert.That(actual.SpecialOrders.Count, Is.EqualTo(expected.SpecialOrders.Count));
            for (int i = 0; i < expected.SpecialOrders.Count; i++)
            {
                var a = actual.SpecialOrders[i];
                var e = expected.SpecialOrders[i];
                if (e == null) { Assert.That(a, Is.Null); continue; }
                Assert.That(a, Is.Not.Null);
                Assert.That(a.OrderId, Is.EqualTo(e.OrderId));
                Assert.That(a.SellerId, Is.EqualTo(e.SellerId));
                Assert.That(a.DeliverToId, Is.EqualTo(e.DeliverToId));
                Assert.That(a.PaidPrice, Is.EqualTo(e.PaidPrice));
                Assert.That(a.OrderedAtSeconds, Is.EqualTo(e.OrderedAtSeconds));
                Assert.That(a.OrderedDay, Is.EqualTo(e.OrderedDay));
                Assert.That(a.DueDay, Is.EqualTo(e.DueDay));
                Assert.That(a.State, Is.EqualTo(e.State));
                if (e.Lines == null) { Assert.That(a.Lines, Is.Null); continue; }
                Assert.That(a.Lines, Is.Not.Null);
                Assert.That(a.Lines.Count, Is.EqualTo(e.Lines.Count));
                for (int k = 0; k < e.Lines.Count; k++)
                {
                    if (e.Lines[k] == null) { Assert.That(a.Lines[k], Is.Null); continue; }
                    Assert.That(a.Lines[k], Is.Not.Null);
                    Assert.That(a.Lines[k].ListingId, Is.EqualTo(e.Lines[k].ListingId));
                    Assert.That(a.Lines[k].Count, Is.EqualTo(e.Lines[k].Count));
                    Assert.That(a.Lines[k].PaidPrice, Is.EqualTo(e.Lines[k].PaidPrice));
                }
            }
        }

        private static void AssertReceipt(SpecialOrderReceipt actual, SpecialOrderReceipt expected)
        {
            Assert.That(actual.PlayerId, Is.EqualTo(expected.PlayerId));
            Assert.That(actual.OrderId, Is.EqualTo(expected.OrderId));
            Assert.That(actual.SellerId, Is.EqualTo(expected.SellerId));
            Assert.That(actual.DeliverToId, Is.EqualTo(expected.DeliverToId));
            Assert.That(actual.PaidPrice, Is.EqualTo(expected.PaidPrice));
            Assert.That(actual.OrderedAtSeconds, Is.EqualTo(expected.OrderedAtSeconds));
            Assert.That(actual.OrderedDay, Is.EqualTo(expected.OrderedDay));
            Assert.That(actual.DueDay, Is.EqualTo(expected.DueDay));
            Assert.That(actual.Lines, Is.EqualTo(expected.Lines));
        }

        [TestCase(null, 1, SpecialOrderRefusal.UnknownLine)]
        [TestCase("specialorder.missing", 1, SpecialOrderRefusal.UnknownLine)]
        [TestCase(Line, 0, SpecialOrderRefusal.InvalidQuantity)]
        [TestCase(Line, -1, SpecialOrderRefusal.InvalidQuantity)]
        [TestCase(Line, 21, SpecialOrderRefusal.LimitReached)]
        [TestCase(Line, int.MaxValue, SpecialOrderRefusal.LimitReached)]
        public void BadRequests_ChangeNothing(string line, int quantity, SpecialOrderRefusal expected)
        {
            string before = Json(_book);
            Assert.That(_book.Place(Player, Seller, line, quantity, out string id), Is.EqualTo(expected));
            Assert.That(id, Is.Null);
            Assert.That(Json(_book), Is.EqualTo(before));
            Assert.That(_wallet.Money, Is.EqualTo(1000));
            Assert.That(_wallet.Calls, Is.Zero);
            Assert.That(_placed, Is.Empty);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void CannotPay_UsesWalletDecision_AndRetainsNoOrder(bool walletRefuses)
        {
            _wallet.Refuse = walletRefuses;
            if (!walletRefuses) _wallet.Add(-999);
            int money = _wallet.Money;
            Assert.That(_book.Place(Player, Seller, Line, 1, out string id), Is.EqualTo(SpecialOrderRefusal.CannotPay));
            Assert.That(id, Is.Null);
            Assert.That(_wallet.Money, Is.EqualTo(money));
            Assert.That(_wallet.Calls, Is.EqualTo(1));
            Assert.That(_book.Capture(Player).Orders, Is.Empty);
            Assert.That(_book.Capture(Player).NextSpecialOrderNumber, Is.EqualTo(1));
            Assert.That(_placed, Is.Empty);
        }

        [Test]
        public void PaymentIsOnceAtPlacement_WithExactReceipt()
        {
            _clock.At(7, 23.9);
            string id = Place(4);
            Assert.That(_wallet.Money, Is.EqualTo(988));
            Assert.That(_placed.Count, Is.EqualTo(1));
            var receipt = _placed[0];
            Assert.That(receipt.OrderId, Is.EqualTo(id));
            Assert.That(receipt.SellerId, Is.EqualTo(Seller));
            Assert.That(receipt.Lines[0].ListingId, Is.EqualTo(Line));
            Assert.That(receipt.Lines[0].BaitId, Is.EqualTo(Item));
            Assert.That(receipt.Lines[0].Count, Is.EqualTo(4));
            Assert.That(receipt.OrderedDay, Is.EqualTo(7));
            Assert.That(receipt.DueDay, Is.EqualTo(9));
            Assert.That(receipt.PaidPrice, Is.EqualTo(12));
            Assert.That(receipt.Lines[0].PaidPrice, Is.EqualTo(12));
            Assert.That(receipt.OrderedAtSeconds, Is.EqualTo(_clock.TotalSeconds));
            var paid = _book.Capture(Player).Orders.Single();
            Assert.That(paid.PaidPrice, Is.EqualTo(12));
            Assert.That(paid.Lines.Single().PaidPrice, Is.EqualTo(12));
            Assert.That(paid.OrderedAtSeconds, Is.EqualTo(_clock.TotalSeconds));
            _clock.StartDay(9);
            Assert.That(_book.Collect(Player, id, _bait), Is.EqualTo(SpecialOrderRefusal.None));
            Assert.That(_wallet.Calls, Is.EqualTo(1));
            Assert.That(_wallet.Money, Is.EqualTo(988));
            Assert.That(_book.Capture(Player).Orders, Is.Empty);
            Assert.That(_collected.Count, Is.EqualTo(1));
            AssertReceipt(_collected[0], receipt);
            Assert.That(_bait, Is.EqualTo(new[] { new BaitStock(Item, 4) }));
        }

        [Test]
        public void DueMidnightWaitsForDaylightUnloading_OnlyOnce()
        {
            _book.Dispose();
            _book = Book(save: _save, supply: Supply(cadence: 2, unloadHour: 12));
            _clock.At(0, 23.999);
            string id = Place();
            _clock.StartDay(1);
            _clock.At(1, 23.999);
            _book.RefreshArrivals();
            Assert.That(_arrived, Is.Empty);
            Assert.That(_book.Capture(Player).Orders[0].Arrived, Is.False);
            _clock.StartDay(2);
            Assert.That(_arrived, Is.Empty);
            Assert.That(_book.Collect(Player, id, _bait), Is.EqualTo(SpecialOrderRefusal.NotArrived));
            _clock.At(2, 11.999);
            _book.RefreshArrivals();
            Assert.That(_arrived, Is.Empty);
            _clock.At(2, 12);
            _book.RefreshArrivals();
            Assert.That(_arrived.Count, Is.EqualTo(1));
            Assert.That(_arrived[0].DueDay, Is.EqualTo(2));
            Assert.That(_book.Capture(Player).Orders[0].Arrived, Is.True);
            _clock.StartDay(3);
            _book.RefreshArrivals();
            Assert.That(_arrived.Count, Is.EqualTo(1));
        }

        [TestCase(false, 4)]
        [TestCase(true, 5)]
        public void DayOneOrder_UsesTheShelvesCalendarAndPreservesItsPromise(bool closedOnFour, int due)
        {
            _book.Dispose();
            var supply = Supply(cadence: 2, unloadHour: 12, possible: day => !closedOnFour || day != 4);
            _book = Book(save: _save, supply: supply);
            _clock.At(1, 15);
            string id = Place(2);
            Assert.That(_save.SpecialOrders.Single().DueDay, Is.EqualTo(due));
            Assert.That(_placed.Single().DueDay, Is.EqualTo(due));
            Assert.That(_wallet.Money, Is.EqualTo(994));
            Assert.That(_wallet.Calls, Is.EqualTo(1));
            _clock.At(due, 11.999);
            _book.RefreshArrivals();
            Assert.That(_arrived, Is.Empty);
            Assert.That(_book.Collect(Player, id, _bait), Is.EqualTo(SpecialOrderRefusal.NotArrived));
            var loaded = SaveSerialization.FromJson(SaveSerialization.ToJson(_save));
            AssertSavedOrdersEqual(_save, loaded);
            _book.Dispose();
            var resumed = Book(wait: 9, save: loaded, supply: supply);
            _clock.At(due, 12);
            resumed.RefreshArrivals();
            resumed.RefreshArrivals();
            Assert.That(_arrived.Single().DueDay, Is.EqualTo(due));
            Assert.That(_bait, Is.Empty);
            Assert.That(resumed.Collect(Player, id, _bait), Is.EqualTo(SpecialOrderRefusal.None));
            Assert.That(_bait, Is.EqualTo(new[] { new BaitStock(Item, 2) }));
            Assert.That(_collected.Count, Is.EqualTo(1));
            Assert.That(resumed.Collect(Player, id, _bait), Is.EqualTo(SpecialOrderRefusal.UnknownOrder));
            Assert.That(loaded.SpecialOrders.Single().DueDay, Is.EqualTo(due));
            Assert.That(loaded.SpecialOrders.Single().PaidPrice, Is.EqualTo(6));
            Assert.That(_wallet.Money, Is.EqualTo(994));
            Assert.That(_wallet.Calls, Is.EqualTo(1));
        }

        [Test]
        public void MissedUnloading_WaitsForALaterCompletedRunWithoutMovingThePaidPromise()
        {
            _book.Dispose();
            int? completed = null;
            var supply = new SpecialOrderSupplySchedule(
                (seller, day) => IslandRunCalendar.RunsOn(day, 2, _ => true),
                (seller, seconds) => completed, 28);
            _book = Book(save: _save, supply: supply);
            _clock.At(1);
            string id = Place(3);
            var paid = _placed.Single();
            Assert.That(paid.DueDay, Is.EqualTo(4));
            _clock.At(5, 18);
            string before = SaveSerialization.ToJson(_save);
            _book.RefreshArrivals();
            Assert.That(_book.Collect(Player, id, _bait), Is.EqualTo(SpecialOrderRefusal.NotArrived));
            Assert.That(SaveSerialization.ToJson(_save), Is.EqualTo(before));
            Assert.That(_arrived, Is.Empty);
            Assert.That(_bait, Is.Empty);
            var loaded = SaveSerialization.FromJson(before);
            _book.Dispose();
            var resumed = Book(save: loaded, supply: supply);
            _clock.At(6, 12);
            completed = 6;
            resumed.RefreshArrivals();
            resumed.RefreshArrivals();
            AssertReceipt(_arrived.Single(), paid);
            Assert.That(resumed.Collect(Player, id, loaded.BaitStock), Is.EqualTo(SpecialOrderRefusal.None));
            AssertReceipt(_collected.Single(), paid);
            Assert.That(loaded.BaitStock, Is.EqualTo(new[] { new BaitStock(Item, 3) }));
            Assert.That(loaded.Money, Is.EqualTo(991));
            Assert.That(_wallet.Calls, Is.EqualTo(1));
            Assert.That(loaded.SpecialOrders.Single().DueDay, Is.EqualTo(4));
        }

        [TestCase(null)]
        [TestCase(2)]
        [TestCase(5)]
        [TestCase(8)]
        public void MissingOldNonRunOrFutureCompletion_CannotReleaseAParcel(int? completed)
        {
            _book.Dispose();
            var supply = new SpecialOrderSupplySchedule(
                (seller, day) => IslandRunCalendar.RunsOn(day, 2, _ => true),
                (seller, seconds) => completed, 28);
            _book = Book(save: _save, supply: supply);
            _clock.At(1);
            string id = Place();
            _clock.At(6, 12);
            string before = SaveSerialization.ToJson(_save);
            _book.RefreshArrivals();
            Assert.That(_book.Collect(Player, id, _bait), Is.EqualTo(SpecialOrderRefusal.NotArrived));
            Assert.That(SaveSerialization.ToJson(_save), Is.EqualTo(before));
            Assert.That(_wallet.Money, Is.EqualTo(997));
            Assert.That(_wallet.Calls, Is.EqualTo(1));
            Assert.That(_arrived, Is.Empty);
            Assert.That(_collected, Is.Empty);
            Assert.That(_bait, Is.Empty);
        }

        [TestCase(1)]
        [TestCase(28)]
        public void NoRunInBoundedSearch_RefusesBeforePayment(int searchDays)
        {
            _book.Dispose();
            var checkedDays = new List<int>();
            var supply = new SpecialOrderSupplySchedule((seller, day) =>
                { checkedDays.Add(day); return false; }, (seller, seconds) => null, searchDays);
            _book = Book(save: _save, supply: supply);
            _clock.At(1);
            string before = SaveSerialization.ToJson(_save);
            Assert.That(_book.Place(Player, Seller, Line, 1, out var id), Is.EqualTo(SpecialOrderRefusal.NoSupplyRun));
            Assert.That(id, Is.Null);
            Assert.That(checkedDays, Is.EqualTo(Enumerable.Range(3, searchDays)));
            Assert.That(SaveSerialization.ToJson(_save), Is.EqualTo(before));
            Assert.That(_wallet.Calls, Is.Zero);
            Assert.That(_placed, Is.Empty);
        }

        [Test]
        public void PromiseSearchNearLastDay_DoesNotWrapOrSearchPastItsBound()
        {
            var days = new List<int>();
            var supply = new SpecialOrderSupplySchedule((seller, day) =>
                { days.Add(day); return false; }, (seller, seconds) => null, 28);
            Assert.That(supply.TryPromise(Seller, int.MaxValue - 1, out _), Is.False);
            Assert.That(days, Is.EqualTo(new[] { int.MaxValue - 1, int.MaxValue }));
        }

        [TestCase(SpecialOrderDto.StateWaiting)]
        [TestCase(SpecialOrderDto.StateArrived)]
        public void CollectedAndOutstandingIdCollision_SurvivesJsonWithoutAnotherGrant(int state)
        {
            _clock.At(1, 10);
            string id = Place(2);
            _clock.StartDay(3);
            Assert.That(_book.Collect(Player, id, _save.BaitStock), Is.EqualTo(SpecialOrderRefusal.None));
            var receipt = _collected.Single();
            _book.Dispose();
            var duplicate = JsonUtility.FromJson<SpecialOrderDto>(JsonUtility.ToJson(_save.SpecialOrders.Single()));
            duplicate.State = state;
            _save.SpecialOrders.Add(duplicate);
            var loaded = SaveSerialization.FromJson(SaveSerialization.ToJson(_save));
            AssertSavedOrdersEqual(_save, loaded);
            var wallet = new TestWallet(loaded);
            var resumed = Book(save: loaded, wallet: wallet);
            string before = SaveSerialization.ToJson(loaded);
            var row = resumed.Capture(Player).Orders.Single();
            Assert.That(row.RefundReasonKey, Is.EqualTo("special_order.duplicate_id"));
            Assert.That(row.RefundAmount, Is.EqualTo(6));
            _clock.At(10);
            resumed.RefreshArrivals();
            Assert.That(resumed.Collect(Player, id, loaded.BaitStock), Is.EqualTo(SpecialOrderRefusal.RefundRequired));
            resumed.RefreshArrivals();
            Assert.That(SaveSerialization.ToJson(loaded), Is.EqualTo(before));
            Assert.That(loaded.SpecialOrders.Select(r => r.State), Is.EqualTo(new[] { 2, state }));
            Assert.That(loaded.SpecialOrders.Select(r => r.PaidPrice), Is.EqualTo(new[] { 6, 6 }));
            Assert.That(loaded.BaitStock, Is.EqualTo(new[] { new BaitStock(Item, 2) }));
            Assert.That(loaded.Money, Is.EqualTo(994));
            Assert.That(wallet.Calls, Is.Zero);
            Assert.That(_placed.Count, Is.EqualTo(1));
            Assert.That(_arrived.Count, Is.EqualTo(1));
            Assert.That(_collected.Count, Is.EqualTo(1));
            AssertReceipt(_collected.Single(), receipt);
            AssertSavedOrdersEqual(loaded, SaveSerialization.FromJson(before));
        }

        [Test]
        public void SleepingPastDueDay_CatchesUpWithoutChangingPromisedDay()
        {
            _clock.At(5, 22);
            Place();
            _clock.At(9, 8);
            _book.RefreshArrivals();
            Assert.That(_arrived.Count, Is.EqualTo(1));
            Assert.That(_arrived[0].DueDay, Is.EqualTo(7));
            Assert.That(_book.Capture(Player).Orders[0].Arrived, Is.True);
            _book.RefreshArrivals();
            Assert.That(_arrived.Count, Is.EqualTo(1));
        }

        [Test]
        public void SameLineOrdersRemainSeparate_LimitIncludesUncollectedArrivals()
        {
            string first = Place(12), second = Place(8);
            Assert.That(first, Is.EqualTo("order.seller.test.1"));
            Assert.That(second, Is.EqualTo("order.seller.test.2"));
            _clock.StartDay(2);
            Assert.That(_book.Capture(Player).Orders.Length, Is.EqualTo(2));
            Assert.That(_arrived.Count, Is.EqualTo(2));
            Assert.That(_book.Place(Player, Seller, Line, 1, out _), Is.EqualTo(SpecialOrderRefusal.LimitReached));
            Assert.That(_book.Collect(Player, second, _bait), Is.EqualTo(SpecialOrderRefusal.None));
            Assert.That(_book.Place(Player, Seller, Line, 8, out string third), Is.EqualTo(SpecialOrderRefusal.None));
            Assert.That(third, Is.EqualTo("order.seller.test.3"));
            Assert.That(_bait.Single().Count, Is.EqualTo(8));
            Assert.That(_book.Place(Player, Seller, Line, 1, out _), Is.EqualTo(SpecialOrderRefusal.LimitReached));
            Assert.That(_book.Capture(Player).Orders[0].OrderId, Is.EqualTo(first));
        }

        [Test]
        public void CollectionRefusals_LeavePaidGoodsAndMoneyUntouched()
        {
            string id = Place(3);
            var full = new List<BaitStock> { new BaitStock(Item, int.MaxValue) };
            Assert.That(_book.Collect(Player, "order.seller.test.2", full), Is.EqualTo(SpecialOrderRefusal.UnknownOrder));
            Assert.That(_book.Collect(Player, id, full), Is.EqualTo(SpecialOrderRefusal.NotArrived));
            Assert.That(full.Single().Count, Is.EqualTo(int.MaxValue));
            _clock.StartDay(2);
            string before = Json(_book);
            Assert.That(_book.Collect(Player, id, null), Is.EqualTo(SpecialOrderRefusal.BaitStoreUnavailable));
            Assert.That(_book.Collect(Player, id, full), Is.EqualTo(SpecialOrderRefusal.BaitStoreUnavailable));
            Assert.That(full.Single().Count, Is.EqualTo(int.MaxValue));
            Assert.That(Json(_book), Is.EqualTo(before));
            Assert.That(_wallet.Money, Is.EqualTo(991));
            Assert.That(_collected, Is.Empty);
            Assert.That(_book.Collect(Player, id, _bait), Is.EqualTo(SpecialOrderRefusal.None));
            Assert.That(_book.Collect(Player, id, _bait), Is.EqualTo(SpecialOrderRefusal.UnknownOrder));
            Assert.That(_bait.Single(), Is.EqualTo(new BaitStock(Item, 3)));
            Assert.That(_collected.Count, Is.EqualTo(1));
        }

        [TestCase("missing", SpecialOrderRefusal.UnknownOrder)]
        [TestCase("null_id", SpecialOrderRefusal.UnknownOrder)]
        [TestCase("null_stock", SpecialOrderRefusal.NotArrived)]
        [TestCase("overflow", SpecialOrderRefusal.NotArrived)]
        [TestCase("available", SpecialOrderRefusal.NotArrived)]
        public void CollectionAfterSeek_LeavesAllDueOrdersUntouchedUntilExplicitRefresh(
            string request, SpecialOrderRefusal expected)
        {
            string first = Place(3), second = Place(2);
            _clock.At(4);
            var stock = new List<BaitStock>();
            if (request == "overflow") stock.Add(new BaitStock(Item, int.MaxValue));
            var stockBefore = stock.ToArray();
            string savedBefore = SaveSerialization.ToJson(_save), viewBefore = Json(_book);
            string id = request == "missing" ? "order.seller.test.99" : request == "null_id" ? null : first;

            Assert.That(_book.Collect(Player, id, request == "null_stock" ? null : stock), Is.EqualTo(expected));

            Assert.That(SaveSerialization.ToJson(_save), Is.EqualTo(savedBefore));
            Assert.That(Json(_book), Is.EqualTo(viewBefore));
            Assert.That(_save.SpecialOrders.Select(row => row.State),
                Is.EqualTo(new[] { SpecialOrderDto.StateWaiting, SpecialOrderDto.StateWaiting }));
            Assert.That(stock, Is.EqualTo(stockBefore));
            Assert.That(_wallet.Money, Is.EqualTo(985));
            Assert.That(_wallet.Calls, Is.EqualTo(2));
            Assert.That(_placed.Count, Is.EqualTo(2));
            Assert.That(_arrived, Is.Empty);
            Assert.That(_collected, Is.Empty);

            _book.RefreshArrivals();
            _book.RefreshArrivals();
            Assert.That(_arrived.Select(receipt => receipt.OrderId), Is.EqualTo(new[] { first, second }));
            Assert.That(_save.SpecialOrders.Select(row => row.State),
                Is.EqualTo(new[] { SpecialOrderDto.StateArrived, SpecialOrderDto.StateArrived }));
            Assert.That(_book.Collect(Player, first, _save.BaitStock), Is.EqualTo(SpecialOrderRefusal.None));
            Assert.That(_save.BaitStock, Is.EqualTo(new[] { new BaitStock(Item, 3) }));
            Assert.That(_collected.Single().OrderId, Is.EqualTo(first));
            Assert.That(_wallet.Money, Is.EqualTo(985));
            Assert.That(_wallet.Calls, Is.EqualTo(2));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void UnavailableCollection_LeavesAnArrivedOrderAndAnOverdueNeighbourUntouched(bool overflow)
        {
            string first = Place(3);
            _clock.StartDay(2);
            string second = Place(2);
            _clock.At(4);
            _save.BaitStock.Add(new BaitStock(Item, int.MaxValue));
            string savedBefore = SaveSerialization.ToJson(_save), viewBefore = Json(_book);

            Assert.That(_book.Collect(Player, first, overflow ? _save.BaitStock : null),
                Is.EqualTo(SpecialOrderRefusal.BaitStoreUnavailable));

            Assert.That(SaveSerialization.ToJson(_save), Is.EqualTo(savedBefore));
            Assert.That(Json(_book), Is.EqualTo(viewBefore));
            Assert.That(_save.BaitStock, Is.EqualTo(new[] { new BaitStock(Item, int.MaxValue) }));
            Assert.That(_save.SpecialOrders.Select(row => row.State),
                Is.EqualTo(new[] { SpecialOrderDto.StateArrived, SpecialOrderDto.StateWaiting }));
            Assert.That(_wallet.Money, Is.EqualTo(985));
            Assert.That(_wallet.Calls, Is.EqualTo(2));
            Assert.That(_placed.Count, Is.EqualTo(2));
            Assert.That(_arrived.Select(receipt => receipt.OrderId), Is.EqualTo(new[] { first }));
            Assert.That(_collected, Is.Empty);

            _save.BaitStock.Clear();
            Assert.That(_book.Collect(Player, first, _save.BaitStock), Is.EqualTo(SpecialOrderRefusal.None));
            Assert.That(_save.BaitStock, Is.EqualTo(new[] { new BaitStock(Item, 3) }));
            Assert.That(_save.SpecialOrders.Select(row => row.State),
                Is.EqualTo(new[] { SpecialOrderDto.StateCollected, SpecialOrderDto.StateWaiting }));
            Assert.That(_arrived.Select(receipt => receipt.OrderId), Is.EqualTo(new[] { first }));
            Assert.That(_collected.Single().OrderId, Is.EqualTo(first));
            _book.RefreshArrivals();
            Assert.That(_arrived.Select(receipt => receipt.OrderId), Is.EqualTo(new[] { first, second }));
            Assert.That(_save.SpecialOrders[1].State, Is.EqualTo(SpecialOrderDto.StateArrived));
            Assert.That(_wallet.Money, Is.EqualTo(985));
            Assert.That(_wallet.Calls, Is.EqualTo(2));
        }

        [Test]
        public void SavedBookRoundTrip_IsIdenticalAndSnapshotsDoNotReplayMoneyOrEvents()
        {
            Place(2);
            _clock.StartDay(2);
            Place(3);
            string json = Json(_book);
            var loaded = SaveSerialization.FromJson(SaveSerialization.ToJson(_save));
            AssertSavedOrdersEqual(_save, loaded);
            var resumedWallet = new TestWallet(loaded);
            var restored = Book(save: loaded, wallet: resumedWallet);
            Assert.That(Json(restored), Is.EqualTo(json));
            Assert.That(_wallet.Calls, Is.EqualTo(2));
            Assert.That(_placed.Count, Is.EqualTo(2));
            Assert.That(_arrived.Count, Is.EqualTo(1));
            Assert.That(loaded.Money, Is.EqualTo(985));
            Assert.That(resumedWallet.Calls, Is.Zero);
            _book.Capture(Player).Orders[0].Lines[0].Count = 900;
            var captured = restored.Capture(Player);
            captured.Orders[1].State = SpecialOrderState.ArrivedState;
            captured.Orders[0].Lines[0].PaidPrice = 900;
            Assert.That(Json(restored), Is.EqualTo(json));
            restored.RefreshArrivals();
            Assert.That(_arrived.Count, Is.EqualTo(1));
            Assert.That(restored.Place(Player, Seller, Line, 1, out string id), Is.EqualTo(SpecialOrderRefusal.None));
            Assert.That(id, Is.EqualTo("order.seller.test.3"));
            Assert.That(resumedWallet.Calls, Is.EqualTo(1));
            Assert.That(loaded.Money, Is.EqualTo(982));
        }

        [TestCase("seller", "special_order.unknown_offer")]
        [TestCase("missing", "special_order.unknown_offer")]
        [TestCase("quantity", "special_order.invalid_lines")]
        [TestCase("placed", "special_order.invalid_due_day")]
        [TestCase("due", "special_order.invalid_due_day")]
        [TestCase("same_day", "special_order.invalid_due_day")]
        [TestCase("empty_id", "special_order.empty_id")]
        [TestCase("null_id", "special_order.empty_id")]
        [TestCase("blank_id", "special_order.empty_id")]
        [TestCase("bad_id", "special_order.invalid_id")]
        [TestCase("null_lines", "special_order.missing_lines")]
        [TestCase("empty_lines", "special_order.missing_lines")]
        [TestCase("null_line", "special_order.unknown_offer")]
        [TestCase("duplicate_line", "special_order.invalid_lines")]
        [TestCase("payment", "special_order.invalid_payment")]
        [TestCase("negative_payment", "special_order.invalid_payment")]
        [TestCase("negative_share", "special_order.invalid_payment")]
        [TestCase("nan_time", "special_order.invalid_time")]
        [TestCase("negative_time", "special_order.invalid_time")]
        [TestCase("infinite_time", "special_order.invalid_time")]
        [TestCase("state", "special_order.invalid_state")]
        [TestCase("destination", "special_order.unsupported_destination")]
        public void MalformedRestore_KeepsPaidRowAndRestoresNeighbours(string corruption, string reason)
        {
            Place(); Place(2); Place();
            var save = SaveSerialization.FromJson(SaveSerialization.ToJson(_save));
            var damaged = save.SpecialOrders[1];
            switch (corruption)
            {
                case "seller": damaged.SellerId = "seller.other"; damaged.OrderId = "order.seller.other.2"; break;
                case "missing": damaged.Lines[0].ListingId = "specialorder.missing"; break;
                case "quantity": damaged.Lines[0].Count = 0; break;
                case "placed": damaged.OrderedDay = -1; break;
                case "due": damaged.DueDay = -1; break;
                case "same_day": damaged.DueDay = damaged.OrderedDay; break;
                case "empty_id": damaged.OrderId = ""; break;
                case "null_id": damaged.OrderId = null; break;
                case "blank_id": damaged.OrderId = " "; break;
                case "bad_id": damaged.OrderId = "order.seller.test.bad"; break;
                case "null_lines": damaged.Lines = null; break;
                case "empty_lines": damaged.Lines = new List<SpecialOrderLineDto>(); break;
                case "null_line": damaged.Lines[0] = null; break;
                case "duplicate_line": damaged.Lines.Add(new SpecialOrderLineDto { ListingId = Line, Count = 2, PaidPrice = 6 }); break;
                case "payment": damaged.PaidPrice = 29; break;
                case "negative_payment": damaged.PaidPrice = -6; break;
                case "negative_share": damaged.Lines[0].PaidPrice = -6; break;
                case "nan_time": damaged.OrderedAtSeconds = double.NaN; break;
                case "negative_time": damaged.OrderedAtSeconds = -1; break;
                case "infinite_time": damaged.OrderedAtSeconds = double.PositiveInfinity; break;
                case "state": damaged.State = 91; break;
                case "destination": damaged.DeliverToId = "wharf.future"; break;
            }
            SpecialOrderBook restored = null;
            Assert.DoesNotThrow(() => restored = Book(save: save));
            var rows = restored.Capture(Player).Orders;
            Assert.That(rows.Length, Is.EqualTo(3));
            Assert.That(rows[0].RequiresRefund, Is.False);
            Assert.That(rows[2].RequiresRefund, Is.False);
            Assert.That(rows[1].RefundReasonKey, Is.EqualTo(reason));
            Assert.That(rows[1].RequiresRefund, Is.True);
            Assert.That(rows[1].OrderId, Is.EqualTo(damaged.OrderId));
            Assert.That(rows[1].PaidPrice, Is.EqualTo(damaged.PaidPrice));
            Assert.That(rows[1].RefundAmount, Is.EqualTo(Math.Max(0, damaged.PaidPrice)));
            Assert.That(rows[1].Lines?.Select(line => line?.PaidPrice),
                Is.EqualTo(damaged.Lines?.Select(line => line?.PaidPrice)));
            restored.Dispose();
            restored = Book(save: save);
            Assert.That(restored.Capture(Player).Orders[1].RefundReasonKey, Is.EqualTo(reason));
            _clock.At(8);
            restored.RefreshArrivals();
            Assert.That(restored.Collect(Player, damaged.OrderId, _bait), Is.EqualTo(SpecialOrderRefusal.RefundRequired));
            Assert.That(_bait, Is.Empty);
            Assert.That(_arrived.Select(receipt => receipt.OrderId),
                Is.EqualTo(new[] { "order.seller.test.1", "order.seller.test.3" }));
            Assert.That(restored.Collect(Player, rows[0].OrderId, _bait), Is.EqualTo(SpecialOrderRefusal.None));
            Assert.That(_wallet.Calls, Is.EqualTo(3));
            Assert.That(_wallet.Money, Is.EqualTo(988));
            Assert.That(_placed.Count, Is.EqualTo(3));
        }

        [Test]
        public void PaidTermsSurviveContentRetuning()
        {
            Place(10);
            var loaded = SaveSerialization.FromJson(SaveSerialization.ToJson(_save));
            var restored = Book(price: 100, wait: 8, limit: 1, save: loaded);
            Assert.That(Json(restored), Is.EqualTo(Json(_book)));
            Assert.That(restored.Place(Player, Seller, Line, 1, out _), Is.EqualTo(SpecialOrderRefusal.LimitReached));
            _clock.StartDay(2);
            Assert.That(restored.Collect(Player, "order.seller.test.1", _bait), Is.EqualTo(SpecialOrderRefusal.None));
            Assert.That(_bait.Single().Count, Is.EqualTo(10));
            Assert.That(_collected.Single().PaidPrice, Is.EqualTo(30));
            Assert.That(_collected.Single().Lines.Single().PaidPrice, Is.EqualTo(30));
            Assert.That(_wallet.Calls, Is.EqualTo(1));
        }

        [Test]
        public void IdenticalClockAndRequests_AreDeterministic_RegardlessOfCatalogueOrder()
        {
            var other = new SpecialOrderLine("specialorder.other", "bait.other", Seller, 5, 4, 10);
            var line = new SpecialOrderLine(Line, Item, Seller, 3, 2, 20);
            using var a = new SpecialOrderBook(SaveMigration.NewGame(), new[] { line, other }, new FixtureAccounts(new TestWallet()), _clock, Supply());
            using var b = new SpecialOrderBook(SaveMigration.NewGame(), new[] { other, line }, new FixtureAccounts(new TestWallet()), _clock, Supply());
            _clock.At(123, 17.5);
            foreach (var book in new[] { a, b })
            {
                Assert.That(book.Place(Player, Seller, Line, 4, out _), Is.EqualTo(SpecialOrderRefusal.None));
                Assert.That(book.Place(Player, Seller, other.Id, 2, out _), Is.EqualTo(SpecialOrderRefusal.None));
            }
            Assert.That(Json(a), Is.EqualTo(Json(b)));
            Assert.That(a.Capture(Player).Orders.Select(row => row.OrderedDay), Is.EqualTo(new[] { 123, 123 }));
            Assert.That(a.Capture(Player).Orders.Select(row => row.DueDay), Is.EqualTo(new[] { 125, 127 }));
            _clock.StartDay(125);
            Assert.That(Json(a), Is.EqualTo(Json(b)));
            Assert.That(a.Capture(Player).Orders[0].Arrived, Is.True);
            Assert.That(a.Capture(Player).Orders[1].Arrived, Is.False);
        }

        [Test]
        public void NumericBoundsRefuseBeforePayment()
        {
            var expensive = Book(price: int.MaxValue);
            Assert.That(expensive.Place(Player, Seller, Line, 2, out _), Is.EqualTo(SpecialOrderRefusal.PriceOutOfRange));
            _clock.At(int.MaxValue);
            Assert.That(_book.Place(Player, Seller, Line, 1, out _), Is.EqualTo(SpecialOrderRefusal.DayOutOfRange));
            _clock.At(-1);
            Assert.That(_book.Place(Player, Seller, Line, 1, out _), Is.EqualTo(SpecialOrderRefusal.DayOutOfRange));
            _clock.At(0);
            _save.NextSpecialOrderNumber = int.MaxValue;
            Assert.That(_book.Place(Player, Seller, Line, 1, out _), Is.EqualTo(SpecialOrderRefusal.OrderNumbersExhausted));
            Assert.That(_wallet.Calls, Is.Zero);
            Assert.That(_placed, Is.Empty);
        }

        [Test]
        public void ReentrantCallbacksCannotSpendOrCollectTwice()
        {
            var refusals = new List<SpecialOrderRefusal>();
            _wallet.DuringSpend = () => refusals.Add(_book.Place(Player, Seller, Line, 1, out _));
            string id = Place();
            _wallet.DuringSpend = null;
            _clock.StartDay(2);
            SpecialOrderSnapshot observed = null;
            Action<SpecialOrderCollected> reenter = _ =>
            {
                refusals.Add(_book.Collect(Player, id, _bait));
                observed = _book.Capture(Player);
                observed.NextSpecialOrderNumber = 1;
                observed.Orders = Array.Empty<SpecialOrderState>();
            };
            EventBus.Subscribe(reenter);
            try { Assert.That(_book.Collect(Player, id, _bait), Is.EqualTo(SpecialOrderRefusal.None)); }
            finally { EventBus.Unsubscribe(reenter); }
            Assert.That(refusals, Is.EqualTo(new[] { SpecialOrderRefusal.Busy, SpecialOrderRefusal.Busy }));
            Assert.That(observed, Is.Not.Null);
            Assert.That(_save.NextSpecialOrderNumber, Is.EqualTo(2));
            Assert.That(_save.SpecialOrders.Single().State, Is.EqualTo(SpecialOrderDto.StateCollected));
            Assert.That(_save.SpecialOrders.Single().PaidPrice, Is.EqualTo(3));
            Assert.That(typeof(SpecialOrderBook).GetMethod("Restore"), Is.Null,
                "A snapshot cannot replace paid rows, including from a transaction callback.");
            Assert.That(_wallet.Calls, Is.EqualTo(1));
            Assert.That(_collected.Count, Is.EqualTo(1));
            Assert.That(_bait.Single().Count, Is.EqualTo(1));
        }

        [Test]
        public void DisposingUnsubscribesAndOtherCountersCannotPlaceThisLine()
        {
            Place();
            _book.Dispose(); _book.Dispose();
            _clock.StartDay(2);
            Assert.That(_arrived, Is.Empty);
            Assert.Throws<ObjectDisposedException>(() => _book.Place(Player, Seller, Line, 1, out _));
            using var other = new SpecialOrderBook(SaveMigration.NewGame(),
                new[] { new SpecialOrderLine(Line, Item, Seller, 3, 2, 20) }, new FixtureAccounts(_wallet), _clock, Supply());
            Assert.That(other.Place(Player, "seller.other", Line, 1, out _), Is.EqualTo(SpecialOrderRefusal.UnknownLine));
        }

        [Test]
        public void SellersShareOneCounter_AndCollectionNeverReusesANumber()
        {
            const string otherSeller = "seller.other", otherLine = "specialorder.other";
            var save = SaveMigration.NewGame();
            using var book = new SpecialOrderBook(save, new[]
            {
                new SpecialOrderLine(Line, Item, Seller, 3, 2, 20),
                new SpecialOrderLine(otherLine, "bait.other", otherSeller, 5, 1, 10)
            }, new FixtureAccounts(_wallet), _clock, Supply());
            Assert.That(book.Place(Player, Seller, Line, 1, out string first), Is.EqualTo(SpecialOrderRefusal.None));
            Assert.That(book.Place(Player, otherSeller, otherLine, 2, out string second), Is.EqualTo(SpecialOrderRefusal.None));
            Assert.That(first, Is.EqualTo("order.seller.test.1"));
            Assert.That(second, Is.EqualTo("order.seller.other.2"));
            _clock.At(4);
            book.RefreshArrivals();
            Assert.That(book.Collect(Player, first, _bait), Is.EqualTo(SpecialOrderRefusal.None));
            Assert.That(book.Collect(Player, second, _bait), Is.EqualTo(SpecialOrderRefusal.None));
            Assert.That(book.Capture(Player).Orders, Is.Empty);
            Assert.That(book.Capture(Player).NextSpecialOrderNumber, Is.EqualTo(3));
            var loaded = SaveSerialization.FromJson(SaveSerialization.ToJson(save));
            loaded.NextSpecialOrderNumber = 1;
            using var resumed = new SpecialOrderBook(loaded,
                new[] { new SpecialOrderLine(otherLine, "bait.other", otherSeller, 5, 1, 10) }, new FixtureAccounts(_wallet), _clock, Supply());
            Assert.That(resumed.Place(Player, otherSeller, otherLine, 1, out string third), Is.EqualTo(SpecialOrderRefusal.None));
            Assert.That(third, Is.EqualTo("order.seller.other.3"));
            Assert.That(_wallet.Money, Is.EqualTo(982));
        }

        private SpecialOrderBook MultiLineBook(SaveData save = null)
        {
            var book = new SpecialOrderBook(save ?? SaveMigration.NewGame(), new[]
            {
                new SpecialOrderLine(Line, Item, Seller, 3, 2, 20),
                new SpecialOrderLine("specialorder.other", "bait.other", Seller, 5, 4, 10),
                new SpecialOrderLine("specialorder.foreign", "bait.foreign", "seller.other", 7, 3, 6)
            }, new FixtureAccounts(_wallet), _clock, Supply());
            _books.Add(book);
            return book;
        }

        [Test]
        public void MultipleLines_PayOnce_ArriveTogether_AndCollectEveryBaitAtomically()
        {
            var book = MultiLineBook();
            _clock.At(3, 17.25);
            Assert.That(book.Place(Player, Seller, new[]
            {
                new SpecialOrderRequest(Line, 2), new SpecialOrderRequest("specialorder.other", 3)
            }, out string id), Is.EqualTo(SpecialOrderRefusal.None));
            var row = book.Capture(Player).Orders.Single();
            Assert.That(row.Lines.Select(line => line.ListingId), Is.EqualTo(new[] { Line, "specialorder.other" }));
            Assert.That(row.Lines.Select(line => line.Count), Is.EqualTo(new[] { 2, 3 }));
            Assert.That(row.Lines.Select(line => line.PaidPrice), Is.EqualTo(new[] { 6, 15 }));
            Assert.That(row.PaidPrice, Is.EqualTo(21));
            Assert.That(row.OrderedDay, Is.EqualTo(3));
            Assert.That(row.OrderedAtSeconds, Is.EqualTo(_clock.TotalSeconds));
            Assert.That(row.DueDay, Is.EqualTo(7));
            Assert.That(_wallet.Calls, Is.EqualTo(1));
            Assert.That(_wallet.Money, Is.EqualTo(979));
            _clock.At(5);
            Assert.That(book.Collect(Player, id, _bait), Is.EqualTo(SpecialOrderRefusal.NotArrived));
            Assert.That(_bait, Is.Empty);
            _clock.At(7);
            book.RefreshArrivals();
            Assert.That(_bait, Is.Empty, "Arrival holds goods at the counter.");
            var full = new List<BaitStock> { new BaitStock("bait.other", int.MaxValue) };
            Assert.That(book.Collect(Player, id, full), Is.EqualTo(SpecialOrderRefusal.BaitStoreUnavailable));
            Assert.That(full, Is.EqualTo(new[] { new BaitStock("bait.other", int.MaxValue) }));
            Assert.That(book.Capture(Player).Orders.Length, Is.EqualTo(1));
            Assert.That(book.Collect(Player, id, _bait), Is.EqualTo(SpecialOrderRefusal.None));
            Assert.That(_bait, Is.EqualTo(new[] { new BaitStock(Item, 2), new BaitStock("bait.other", 3) }));
            Assert.That(book.Capture(Player).Orders, Is.Empty);
            AssertReceipt(_collected.Single(), _placed.Single());
        }

        [TestCase("unknown", SpecialOrderRefusal.UnknownLine)]
        [TestCase("count", SpecialOrderRefusal.InvalidQuantity)]
        [TestCase("duplicate_limit", SpecialOrderRefusal.LimitReached)]
        [TestCase("limit", SpecialOrderRefusal.LimitReached)]
        [TestCase("seller", SpecialOrderRefusal.UnknownLine)]
        public void LaterLineRefusal_ChargesNothingAndReservesNoNumber(string fault, SpecialOrderRefusal refusal)
        {
            var book = MultiLineBook();
            var second = new SpecialOrderRequest("specialorder.other", 1);
            if (fault == "unknown") second = new SpecialOrderRequest("specialorder.missing", 1);
            if (fault == "count") second = new SpecialOrderRequest("specialorder.other", 0);
            if (fault == "duplicate_limit") second = new SpecialOrderRequest(Line, 20);
            if (fault == "limit") second = new SpecialOrderRequest("specialorder.other", 11);
            string before = Json(book);
            Assert.That(book.Place(Player, fault == "seller" ? "seller.other" : Seller,
                new[] { new SpecialOrderRequest(Line, 1), second }, out string id), Is.EqualTo(refusal));
            Assert.That(id, Is.Null);
            Assert.That(Json(book), Is.EqualTo(before));
            Assert.That(_wallet.Calls, Is.Zero);
            Assert.That(_wallet.Money, Is.EqualTo(1000));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void WholeOrderPriceOverflow_RefusesEvenWhenEachRequestFits(bool repeated)
        {
            using var book = new SpecialOrderBook(SaveMigration.NewGame(), new[]
            {
                new SpecialOrderLine(Line, Item, Seller, int.MaxValue, 1, 2),
                new SpecialOrderLine("specialorder.other", "bait.other", Seller, 1, 1, 2)
            }, new FixtureAccounts(_wallet), _clock, Supply());
            Assert.That(book.Place(Player, Seller, new[]
            {
                new SpecialOrderRequest(Line, 1), new SpecialOrderRequest(repeated ? Line : "specialorder.other", 1)
            }, out _), Is.EqualTo(SpecialOrderRefusal.PriceOutOfRange));
            Assert.That(_wallet.Calls, Is.Zero);
            Assert.That(book.Capture(Player).NextSpecialOrderNumber, Is.EqualTo(1));
        }

        [Test]
        public void NullAndEmptyRequests_RefuseWithoutPayment()
        {
            Assert.That(_book.Place(Player, Seller, null, out _), Is.EqualTo(SpecialOrderRefusal.InvalidQuantity));
            Assert.That(_book.Place(Player, Seller, Array.Empty<SpecialOrderRequest>(), out _), Is.EqualTo(SpecialOrderRefusal.InvalidQuantity));
            Assert.That(_wallet.Calls, Is.Zero);
        }

        [Test]
        public void DuplicateIds_KeepBothPayments_BlockBothRows_AndSurviveJson()
        {
            Place(2); Place(3); Place(1);
            var save = SaveSerialization.FromJson(SaveSerialization.ToJson(_save));
            save.SpecialOrders[1].OrderId = save.SpecialOrders[0].OrderId;
            var restored = Book(save: save);
            var rows = restored.Capture(Player).Orders;
            Assert.That(rows.Length, Is.EqualTo(3));
            Assert.That(rows.Take(2).Select(row => row.RefundReasonKey),
                Is.EqualTo(new[] { "special_order.duplicate_id", "special_order.duplicate_id" }));
            Assert.That(rows.Select(row => row.PaidPrice), Is.EqualTo(new[] { 6, 9, 3 }));
            Assert.That(rows.Take(2).Select(row => row.RefundAmount), Is.EqualTo(new[] { 6, 9 }));
            Assert.That(rows[2].RequiresRefund, Is.False);
            _clock.At(8);
            Assert.That(restored.Collect(Player, rows[0].OrderId, _bait), Is.EqualTo(SpecialOrderRefusal.RefundRequired));
            Assert.That(_bait, Is.Empty);
            string before = Json(restored);
            restored.Dispose();
            var loaded = SaveSerialization.FromJson(SaveSerialization.ToJson(save));
            AssertSavedOrdersEqual(save, loaded);
            restored = Book(save: loaded);
            Assert.That(Json(restored), Is.EqualTo(before));
            Assert.That(_wallet.Money, Is.EqualTo(982));
        }

        [Test]
        public void NullRowsAndCollectedRows_AreSkipped_ButCollectedNumbersStayUsed()
        {
            Place(); Place(2); Place(3);
            var save = SaveSerialization.FromJson(SaveSerialization.ToJson(_save));
            save.NextSpecialOrderNumber = 0;
            save.SpecialOrders[1].State = SpecialOrderDto.StateCollected;
            save.SpecialOrders[1].OrderId = "order.seller.other.90";
            save.SpecialOrders.Insert(0, null);
            save.SpecialOrders.Add(null);
            SpecialOrderBook restored = null;
            Assert.DoesNotThrow(() => restored = Book(save: save));
            Assert.That(restored.Capture(Player).Orders.Select(row => row.OrderId),
                Is.EqualTo(new[] { "order.seller.test.1", "order.seller.test.3" }));
            Assert.That(restored.Capture(Player).NextSpecialOrderNumber, Is.EqualTo(91));
            Assert.That(restored.Place(Player, Seller, Line, 1, out string id), Is.EqualTo(SpecialOrderRefusal.None));
            Assert.That(id, Is.EqualTo("order.seller.test.91"));
            Assert.That(_wallet.Calls, Is.EqualTo(4));
            Assert.That(_wallet.Money, Is.EqualTo(979));
        }

        [TestCase(-5, 4)]
        [TestCase(2, 4)]
        [TestCase(70, 70)]
        public void RestoreRepairsCounterWithoutRejectingPaidRows(int supplied, int expected)
        {
            Place(); Place(); Place();
            var save = SaveSerialization.FromJson(SaveSerialization.ToJson(_save));
            save.NextSpecialOrderNumber = supplied;
            var restored = Book(save: save);
            Assert.That(restored.Capture(Player).Orders.Length, Is.EqualTo(3));
            Assert.That(restored.Capture(Player).NextSpecialOrderNumber, Is.EqualTo(expected));
            restored.Dispose();
            restored = Book(save: save);
            Assert.That(restored.Capture(Player).NextSpecialOrderNumber, Is.EqualTo(expected));
            Assert.That(_wallet.Calls, Is.EqualTo(3));
        }

        [Test]
        public void EmptyOrMissingOrderLists_DoNotResetUsedNumbersOrReplacePaidRows()
        {
            Place();
            string before = Json(_book);
            var view = _book.Capture(Player);
            view.Orders = null;
            view.NextSpecialOrderNumber = 1;
            Assert.That(Json(_book), Is.EqualTo(before));
            Assert.That(_save.SpecialOrders.Single().PaidPrice, Is.EqualTo(3));
            var empty = SaveMigration.NewGame();
            empty.NextSpecialOrderNumber = 2;
            empty.SpecialOrders = null;
            var fresh = Book(save: empty);
            Assert.That(fresh.Capture(Player).Orders, Is.Empty);
            Assert.That(fresh.Capture(Player).NextSpecialOrderNumber, Is.EqualTo(2));
            empty.NextSpecialOrderNumber = -3;
            fresh.Dispose();
            fresh = Book(save: empty);
            Assert.That(fresh.Capture(Player).NextSpecialOrderNumber, Is.EqualTo(1));
            Assert.Throws<ArgumentNullException>(() => new SpecialOrderBook(null,
                Array.Empty<SpecialOrderLine>(), new FixtureAccounts(_wallet), _clock, Supply()));
        }

        [TestCase("order.seller.test.2147483647")]
        [TestCase("order.seller.test.2147483646")]
        public void HighestRestoredNumbers_ExhaustWithoutOverflowOrReuse(string id)
        {
            Place();
            _save.SpecialOrders[0].OrderId = id;
            _book.Dispose();
            _book = Book(save: _save);
            Assert.That(_book.Capture(Player).NextSpecialOrderNumber, Is.EqualTo(int.MaxValue));
            Assert.That(_book.Capture(Player).Orders.Single().OrderId, Is.EqualTo(id));
            Assert.That(_book.Place(Player, Seller, Line, 1, out _), Is.EqualTo(SpecialOrderRefusal.OrderNumbersExhausted));
            Assert.That(_wallet.Calls, Is.EqualTo(1));
        }

        [Test]
        public void BookToSaveJsonToBook_IsEqualAcrossSellersAndMultipleLines()
        {
            _book.Dispose();
            var book = MultiLineBook(_save);
            _clock.At(4, 13.75);
            Assert.That(book.Place(Player, Seller, new[]
            {
                new SpecialOrderRequest(Line, 2), new SpecialOrderRequest("specialorder.other", 3)
            }, out _), Is.EqualTo(SpecialOrderRefusal.None));
            _clock.At(8);
            book.RefreshArrivals();
            Assert.That(book.Place(Player, Seller, Line, 1, out _), Is.EqualTo(SpecialOrderRefusal.None));
            Assert.That(book.Place(Player, "seller.other", "specialorder.foreign", 2, out _), Is.EqualTo(SpecialOrderRefusal.None));
            var foreign = JsonUtility.FromJson<SpecialOrderDto>(JsonUtility.ToJson(_save.SpecialOrders[1]));
            foreign.OrderId = "order.seller.retired.9";
            foreign.SellerId = "seller.retired";
            _save.SpecialOrders.Add(foreign);
            book.Dispose();
            book = MultiLineBook(_save);
            string before = Json(book);
            string text = SaveSerialization.ToJson(_save);
            var loaded = SaveSerialization.FromJson(text);
            var restored = MultiLineBook(loaded);
            Assert.That(Json(restored), Is.EqualTo(before));
            AssertSavedOrdersEqual(_save, loaded);
            Assert.That(restored.Capture(Player).NextSpecialOrderNumber, Is.EqualTo(10));
            Assert.That(restored.Capture(Player).Orders.Select(row => row.State), Is.EqualTo(new[] { 1, 0, 0, 0 }));
            Assert.That(restored.Capture(Player).Orders[2].OrderId, Is.EqualTo("order.seller.other.3"));
            Assert.That(restored.Capture(Player).Orders[2].RequiresRefund, Is.False);
            Assert.That(restored.Capture(Player).Orders[3].RefundAmount, Is.EqualTo(3));
            var exported = restored.Capture(Player);
            exported.Orders[0].Lines[0].PaidPrice = 99;
            exported.Orders[0].Lines[0].Count = 99;
            Assert.That(Json(restored), Is.EqualTo(before));
            Assert.That(_wallet.Calls, Is.EqualTo(3));
            Assert.That(_wallet.Money, Is.EqualTo(962));
            Assert.That(loaded.Money, Is.EqualTo(962));
            Assert.That(_placed.Count, Is.EqualTo(3));
            Assert.That(_arrived.Count, Is.EqualTo(1));
            Assert.That(_collected, Is.Empty);
        }

        [Test]
        public void BoundRows_RetainPaidDamagedLinesAndTotals_AndHideCollectedReceipts()
        {
            var rows = new SaveData
            {
                NextSpecialOrderNumber = 2,
                SpecialOrders = new List<SpecialOrderDto>
                {
                    null,
                    new SpecialOrderDto { OrderId = "order.seller.test.42", State = SpecialOrderState.Collected },
                    new SpecialOrderDto { OrderId = "", PaidPrice = 11, Lines = null },
                    new SpecialOrderDto
                    {
                        OrderId = "order.seller.test.1", SellerId = Seller, OrderedDay = 1, DueDay = 3,
                        PaidPrice = 0, Lines = new List<SpecialOrderLineDto>
                        {
                            new SpecialOrderLineDto { ListingId = Line, Count = 2, PaidPrice = 6 },
                            new SpecialOrderLineDto { ListingId = "specialorder.retired", Count = 3, PaidPrice = 15 }
                        }
                    }
                }
            };
            _book.Dispose();
            Assert.DoesNotThrow(() => _book = Book(save: rows));
            var snapshot = _book.Capture(Player);
            Assert.That(snapshot.Orders.Length, Is.EqualTo(2));
            Assert.That(snapshot.Orders.Select(row => row.RefundReasonKey),
                Is.EqualTo(new[] { "special_order.empty_id", "special_order.unknown_offer" }));
            Assert.That(snapshot.Orders.Select(row => row.RefundAmount), Is.EqualTo(new[] { 11, 0 }));
            Assert.That(rows.SpecialOrders.Count, Is.EqualTo(4));
            Assert.That(rows.SpecialOrders[1].State, Is.EqualTo(SpecialOrderDto.StateCollected));
            var exported = _book.Capture(Player);
            Assert.That(exported.NextSpecialOrderNumber, Is.EqualTo(43));
            Assert.That(exported.Orders.Length, Is.EqualTo(2));
            Assert.That(exported.Orders[0].OrderId, Is.EqualTo(""));
            Assert.That(exported.Orders[0].PaidPrice, Is.EqualTo(11));
            Assert.That(exported.Orders[0].Lines, Is.Null);
            Assert.That(exported.Orders[1].PaidPrice, Is.Zero);
            Assert.That(exported.Orders[1].Lines.Select(line => line.PaidPrice), Is.EqualTo(new[] { 6, 15 }));
            _clock.At(9);
            Assert.That(_book.Collect(Player, "order.seller.test.1", _bait), Is.EqualTo(SpecialOrderRefusal.RefundRequired));
            Assert.That(_bait, Is.Empty);
            Assert.That(_wallet.Calls, Is.Zero);
            Assert.That(_wallet.Money, Is.EqualTo(1000));
            Assert.That(_arrived, Is.Empty);
        }

        [Test]
        public void ReservedRowJson_UsesOfferIdsLineSharesIntegerDaysAndSeconds()
        {
            const string text = "{\"NextSpecialOrderNumber\":12,\"SpecialOrders\":[{" +
                "\"OrderId\":\"order.seller.test.11\",\"SellerId\":\"seller.test\",\"DeliverToId\":\"\"," +
                "\"Lines\":[{\"ListingId\":\"specialorder.herring\",\"Count\":2,\"PaidPrice\":6}]," +
                "\"PaidPrice\":6,\"OrderedAtSeconds\":1200.125,\"OrderedDay\":1,\"DueDay\":3,\"State\":1}]}";
            _book.Dispose();
            _book = Book(save: SaveSerialization.FromJson("{\"SchemaVersion\":26," + text.Substring(1)));
            var row = _book.Capture(Player).Orders.Single();
            Assert.That(row.OrderId, Is.EqualTo("order.seller.test.11"));
            Assert.That(row.SellerId, Is.EqualTo(Seller));
            Assert.That(row.DeliverToId, Is.EqualTo(""));
            Assert.That(row.Lines.Single().ListingId, Is.EqualTo(Line));
            Assert.That(row.Lines.Single().Count, Is.EqualTo(2));
            Assert.That(row.Lines.Single().PaidPrice, Is.EqualTo(6));
            Assert.That(row.PaidPrice, Is.EqualTo(6));
            Assert.That(row.OrderedAtSeconds, Is.EqualTo(1200.125));
            Assert.That(row.OrderedDay, Is.EqualTo(1));
            Assert.That(row.DueDay, Is.EqualTo(3));
            Assert.That(row.Arrived, Is.True);
            Assert.That(row.RequiresRefund, Is.False);
            Assert.That(_book.Capture(Player).NextSpecialOrderNumber, Is.EqualTo(12));
            Assert.That(_wallet.Calls, Is.Zero);
            Assert.That(_placed, Is.Empty);
            Assert.That(_arrived, Is.Empty);
        }

        [TestCase(SpecialOrderDto.StateWaiting)]
        [TestCase(SpecialOrderDto.StateArrived)]
        [TestCase(SpecialOrderDto.StateCollected)]
        public void RepeatedListings_ProduceRowsUnchangedByJsonAndLoadRepair(int state)
        {
            _book.Dispose();
            _book = MultiLineBook(_save);
            var savedRows = _save.SpecialOrders;
            _clock.At(7, 13.75);
            Assert.That(_book.Place(Player, Seller, new[]
            {
                new SpecialOrderRequest(Line, 2), new SpecialOrderRequest("specialorder.other", 3),
                new SpecialOrderRequest(Line, 4), new SpecialOrderRequest("specialorder.other", 1)
            }, out string first), Is.EqualTo(SpecialOrderRefusal.None));
            Assert.That(_save.SpecialOrders, Is.SameAs(savedRows));
            var row = savedRows.Single();
            Assert.That(row.Lines.Select(line => line.ListingId), Is.EqualTo(new[] { Line, "specialorder.other" }));
            Assert.That(row.Lines.Select(line => line.Count), Is.EqualTo(new[] { 6, 4 }));
            Assert.That(row.Lines.Select(line => line.PaidPrice), Is.EqualTo(new[] { 18, 20 }));
            Assert.That(row.PaidPrice, Is.EqualTo(38));
            Assert.That(row.OrderedAtSeconds, Is.EqualTo(_clock.TotalSeconds));
            Assert.That(row.OrderedDay, Is.EqualTo(7));
            Assert.That(row.DueDay, Is.EqualTo(11));
            Assert.That(_wallet.Calls, Is.EqualTo(1));
            Assert.That(_placed.Single().Lines.Select(line => line.PaidPrice), Is.EqualTo(new[] { 18, 20 }));
            Assert.That(_book.Place(Player, "seller.other", "specialorder.foreign", 1, out _), Is.EqualTo(SpecialOrderRefusal.None));
            Assert.That(_book.Place(Player, Seller, Line, 1, out _), Is.EqualTo(SpecialOrderRefusal.None));
            if (state != SpecialOrderDto.StateWaiting)
            {
                _clock.At(11);
                _book.RefreshArrivals();
            }
            if (state == SpecialOrderDto.StateCollected)
                Assert.That(_book.Collect(Player, first, _save.BaitStock), Is.EqualTo(SpecialOrderRefusal.None));
            Assert.That(row.State, Is.EqualTo(state));
            Assert.That(row.PaidPrice, Is.EqualTo(38));
            Assert.That(row.Lines.Select(line => line.PaidPrice), Is.EqualTo(new[] { 18, 20 }));
            Assert.That(_save.SpecialOrders.Count, Is.EqualTo(3), "Separate orders never fold together.");
            Assert.That(_save.NextSpecialOrderNumber, Is.EqualTo(4));
            Assert.That(_wallet.Money, Is.EqualTo(952));
            int placed = _placed.Count, arrived = _arrived.Count, collected = _collected.Count;
            string view = Json(_book);
            var loaded = SaveSerialization.FromJson(SaveSerialization.ToJson(_save));
            AssertSavedOrdersEqual(_save, loaded);
            SaveMigration.Migrate(loaded);
            AssertSavedOrdersEqual(_save, loaded);
            var resumed = MultiLineBook(loaded);
            Assert.That(Json(resumed), Is.EqualTo(view));
            AssertSavedOrdersEqual(_save, loaded);
            Assert.That(loaded.Money, Is.EqualTo(952));
            Assert.That(loaded.BaitStock, Is.EqualTo(_save.BaitStock));
            Assert.That(_placed.Count, Is.EqualTo(placed));
            Assert.That(_arrived.Count, Is.EqualTo(arrived));
            Assert.That(_collected.Count, Is.EqualTo(collected));
        }

        [Test]
        public void CollectionSurvivesRestart_WithPaidReceiptAndNoSecondGrant()
        {
            string id = Place(3);
            var receipt = _save.SpecialOrders.Single();
            _clock.At(2);
            _book.RefreshArrivals();
            Assert.That(_book.Collect(Player, id, _save.BaitStock), Is.EqualTo(SpecialOrderRefusal.None));
            Assert.That(_save.SpecialOrders.Single(), Is.SameAs(receipt));
            Assert.That(receipt.State, Is.EqualTo(SpecialOrderDto.StateCollected));
            Assert.That(receipt.PaidPrice, Is.EqualTo(9));
            Assert.That(receipt.Lines.Single().PaidPrice, Is.EqualTo(9));
            var loaded = SaveSerialization.FromJson(SaveSerialization.ToJson(_save));
            AssertSavedOrdersEqual(_save, loaded);
            var wallet = new TestWallet(loaded);
            var resumed = Book(save: loaded, wallet: wallet);
            Assert.That(resumed.Capture(Player).Orders, Is.Empty);
            Assert.That(resumed.Collect(Player, id, loaded.BaitStock), Is.EqualTo(SpecialOrderRefusal.UnknownOrder));
            resumed.RefreshArrivals();
            Assert.That(loaded.BaitStock, Is.EqualTo(new[] { new BaitStock(Item, 3) }));
            Assert.That(loaded.Money, Is.EqualTo(991));
            Assert.That(wallet.Calls, Is.Zero);
            Assert.That(_arrived.Count, Is.EqualTo(1));
            Assert.That(_collected.Count, Is.EqualTo(1));
            Assert.That(resumed.Place(Player, Seller, Line, 1, out string next), Is.EqualTo(SpecialOrderRefusal.None));
            Assert.That(next, Is.EqualTo("order.seller.test.2"));
            Assert.That(loaded.SpecialOrders.Count, Is.EqualTo(2));
            Assert.That(loaded.Money, Is.EqualTo(988));
        }

        [TestCase("{\"SchemaVersion\":26}")]
        [TestCase("{\"SchemaVersion\":26,\"SpecialOrders\":[]}")]
        [TestCase("{\"SchemaVersion\":26,\"SpecialOrders\":null}")]
        public void SaveWithNoOrders_LoadsAnEmptyBook(string json)
        {
            var loaded = SaveSerialization.FromJson(json);
            var wallet = new TestWallet(loaded);
            var book = Book(save: loaded, wallet: wallet);
            Assert.That(book.Capture(Player).Orders, Is.Empty);
            Assert.That(loaded.SpecialOrders, Is.Empty);
            Assert.That(book.Capture(Player).NextSpecialOrderNumber, Is.EqualTo(1));
            Assert.That(wallet.Calls, Is.Zero);
            Assert.That(_placed, Is.Empty);
            Assert.That(_arrived, Is.Empty);
            Assert.That(_collected, Is.Empty);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void RetiredSellerOrListing_IsStillOwedAfterRestart(bool sellerRetired)
        {
            string id = Place(2);
            var loaded = SaveSerialization.FromJson(SaveSerialization.ToJson(_save));
            var offers = sellerRetired
                ? new[] { new SpecialOrderLine(Line, Item, "seller.other", 9, 6, 1) }
                : Array.Empty<SpecialOrderLine>();
            var wallet = new TestWallet(loaded);
            using var book = new SpecialOrderBook(loaded, offers, new FixtureAccounts(wallet), _clock, Supply());
            _clock.At(20);
            book.RefreshArrivals();
            Assert.That(book.Collect(Player, id, loaded.BaitStock), Is.EqualTo(SpecialOrderRefusal.RefundRequired));
            var owed = book.Capture(Player).Orders.Single();
            Assert.That(owed.RequiresRefund, Is.True);
            Assert.That(owed.RefundReasonKey, Is.EqualTo("special_order.unknown_offer"));
            Assert.That(owed.RefundAmount, Is.EqualTo(6));
            Assert.That(owed.Lines.Single().PaidPrice, Is.EqualTo(6));
            AssertSavedOrdersEqual(_save, loaded);
            AssertSavedOrdersEqual(loaded, SaveSerialization.FromJson(SaveSerialization.ToJson(loaded)));
            Assert.That(wallet.Money, Is.EqualTo(994));
            Assert.That(wallet.Calls, Is.Zero);
            Assert.That(loaded.BaitStock, Is.Empty);
            Assert.That(_placed.Count, Is.EqualTo(1));
            Assert.That(_arrived, Is.Empty);
            Assert.That(_collected, Is.Empty);
        }

        [Test]
        public void CombinedListingLimit_IncludesOtherPaidOrdersBeforeCharging()
        {
            Place(17);
            string before = SaveSerialization.ToJson(_save);
            Assert.That(_book.Place(Player, Seller, new[]
            {
                new SpecialOrderRequest(Line, 2), new SpecialOrderRequest(Line, 2)
            }, out string id), Is.EqualTo(SpecialOrderRefusal.LimitReached));
            Assert.That(id, Is.Null);
            Assert.That(SaveSerialization.ToJson(_save), Is.EqualTo(before));
            Assert.That(_wallet.Calls, Is.EqualTo(1));
            Assert.That(_wallet.Money, Is.EqualTo(949));
            Assert.That(_placed.Count, Is.EqualTo(1));
        }

        [Test]
        public void CombinedListingCountCannotOverflow()
        {
            var book = Book(price: 1, limit: int.MaxValue);
            Assert.That(book.Place(Player, Seller, new[]
            {
                new SpecialOrderRequest(Line, int.MaxValue), new SpecialOrderRequest(Line, 1)
            }, out _), Is.EqualTo(SpecialOrderRefusal.LimitReached));
            Assert.That(_wallet.Calls, Is.Zero);
            Assert.That(book.Capture(Player).Orders, Is.Empty);
            Assert.That(book.Capture(Player).NextSpecialOrderNumber, Is.EqualTo(1));
        }

        [TestCase(-1d, SpecialOrderDto.StateWaiting, 0d, SpecialOrderDto.StateWaiting)]
        [TestCase(1200.125d, 91, 1200.125d, SpecialOrderDto.StateWaiting)]
        [TestCase(-1d, 91, 0d, SpecialOrderDto.StateWaiting)]
        [TestCase(-1d, SpecialOrderDto.StateArrived, 0d, SpecialOrderDto.StateArrived)]
        public void Version26Json_RepairsTimeAndStateBeforeTheBookClassifiesPaidRows(
            double seconds, int state, double repairedSeconds, int repairedState)
        {
            _clock.At(1);
            string id = Place(2);
            _book.Dispose();
            var source = _save.SpecialOrders.Single();
            source.OrderedAtSeconds = seconds;
            source.State = state;
            // A file written by a v26 build: the orders' repair must hold through every later step too.
            Assert.That(SaveMigration.CurrentVersion, Is.GreaterThanOrEqualTo(26));
            _save.SchemaVersion = 26;
            string text = SaveSerialization.ToJson(_save);
            var loaded = SaveSerialization.FromJson(text);
            Assert.That(loaded, Is.Not.Null);
            Assert.That(loaded.SchemaVersion, Is.EqualTo(SaveMigration.CurrentVersion));
            var expected = JsonUtility.FromJson<SaveData>(text);
            expected.SpecialOrders.Single().OrderedAtSeconds = repairedSeconds;
            expected.SpecialOrders.Single().State = repairedState;
            AssertSavedOrdersEqual(expected, loaded);
            Assert.That(source.OrderedAtSeconds, Is.EqualTo(seconds));
            Assert.That(source.State, Is.EqualTo(state));

            var wallet = new TestWallet(loaded);
            var resumed = Book(save: loaded, wallet: wallet);
            var row = resumed.Capture(Player).Orders.Single();
            Assert.That(row.OrderId, Is.EqualTo(id));
            Assert.That(row.OrderedAtSeconds, Is.EqualTo(repairedSeconds));
            Assert.That(row.State, Is.EqualTo(repairedState));
            Assert.That(row.RequiresRefund, Is.False);
            Assert.That(row.RefundReasonKey, Is.Empty);
            Assert.That(row.PaidPrice, Is.EqualTo(6));
            Assert.That(row.Lines.Single().PaidPrice, Is.EqualTo(6));
            Assert.That(row.Lines.Single().Count, Is.EqualTo(2));
            Assert.That(wallet.Money, Is.EqualTo(994));
            Assert.That(wallet.Calls, Is.Zero);
            Assert.That(_placed.Count, Is.EqualTo(1));
            Assert.That(_arrived, Is.Empty);
            Assert.That(_collected, Is.Empty);
            AssertSavedOrdersEqual(expected, SaveSerialization.FromJson(SaveSerialization.ToJson(loaded)));

            _clock.At(3);
            resumed.RefreshArrivals();
            Assert.That(_arrived.Count, Is.EqualTo(repairedState == SpecialOrderDto.StateWaiting ? 1 : 0));
            Assert.That(resumed.Collect(Player, id, loaded.BaitStock), Is.EqualTo(SpecialOrderRefusal.None));
            Assert.That(loaded.SpecialOrders.Single().State, Is.EqualTo(SpecialOrderDto.StateCollected));
            Assert.That(loaded.BaitStock, Is.EqualTo(new[] { new BaitStock(Item, 2) }));
            Assert.That(wallet.Money, Is.EqualTo(994));
            Assert.That(wallet.Calls, Is.Zero);
            Assert.That(_collected.Single().OrderId, Is.EqualTo(id));
        }

        [TestCase(70, false)]
        [TestCase(90, true)]
        public void RepairedDuplicateListings_KeepOriginalTotalAndEachListingPayment(int total, bool refund)
        {
            var save = SaveMigration.NewGame();
            save.SpecialOrders.Add(new SpecialOrderDto
            {
                OrderId = "order.seller.test.9", SellerId = Seller, PaidPrice = total,
                OrderedDay = 1, DueDay = 5, OrderedAtSeconds = 1200.125,
                Lines = new List<SpecialOrderLineDto>
                {
                    new SpecialOrderLineDto { ListingId = Line, Count = 2, PaidPrice = 25 },
                    new SpecialOrderLineDto { ListingId = "specialorder.other", Count = 1, PaidPrice = 10 },
                    new SpecialOrderLineDto { ListingId = Line, Count = 3, PaidPrice = 35 }
                }
            });
            var loaded = SaveSerialization.FromJson(SaveSerialization.ToJson(save));
            string before = SaveSerialization.ToJson(loaded);
            var book = MultiLineBook(loaded);
            var row = book.Capture(Player).Orders.Single();
            Assert.That(row.Lines.Select(line => line.ListingId), Is.EqualTo(new[] { Line, "specialorder.other" }));
            Assert.That(row.Lines.Select(line => line.Count), Is.EqualTo(new[] { 5, 1 }));
            Assert.That(row.Lines.Select(line => line.PaidPrice), Is.EqualTo(new[] { 60, 10 }));
            Assert.That(row.PaidPrice, Is.EqualTo(total));
            Assert.That(row.RequiresRefund, Is.EqualTo(refund));
            Assert.That(row.RefundAmount, Is.EqualTo(refund ? total : 0));
            Assert.That(row.RefundReasonKey, Is.EqualTo(refund ? "special_order.invalid_payment" : ""));
            Assert.That(book.Capture(Player).NextSpecialOrderNumber, Is.EqualTo(10));
            Assert.That(SaveSerialization.ToJson(loaded), Is.EqualTo(before));
            AssertSavedOrdersEqual(loaded, SaveSerialization.FromJson(SaveSerialization.ToJson(loaded)));
            Assert.That(_wallet.Calls, Is.Zero);
            Assert.That(_placed, Is.Empty);
            Assert.That(_arrived, Is.Empty);
            Assert.That(_collected, Is.Empty);
        }

        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        public void EachPlayerPaysSeesAndCollectsOnlyHisOwnOrders(int playerCount)
        {
            _book.Dispose();
            var wallets = Enumerable.Range(1, playerCount).ToDictionary(i => "player." + i, i => new TestWallet());
            var accounts = new SessionSpecialOrderAccounts(wallets.ToDictionary(pair => pair.Key, pair => (IWallet)pair.Value));
            using var book = new SpecialOrderBook(_save,
                new[] { new SpecialOrderLine(Line, Item, Seller, 3, 2, 2) }, accounts, _clock,
                Supply(cadence: 2, unloadHour: 12));
            var ids = new Dictionary<string, string>();
            var stocks = wallets.Keys.ToDictionary(player => player, player => new List<BaitStock>());
            foreach (string player in wallets.Keys)
            {
                Assert.That(book.Place(player, Seller, Line, 2, out string id), Is.EqualTo(SpecialOrderRefusal.None));
                ids.Add(player, id);
                Assert.That(accounts.OwnerOf(id), Is.EqualTo(player));
                Assert.That(book.Capture(player).Orders.Select(row => row.OrderId), Is.EqualTo(new[] { id }));
                Assert.That(wallets[player].Money, Is.EqualTo(994));
                Assert.That(wallets[player].Calls, Is.EqualTo(1));
                Assert.That(book.Place(player, Seller, Line, 1, out _), Is.EqualTo(SpecialOrderRefusal.LimitReached));
            }
            Assert.That(ids.Values.Distinct().Count(), Is.EqualTo(playerCount));
            Assert.That(_save.NextSpecialOrderNumber, Is.EqualTo(playerCount + 1));
            Assert.That(_save.SpecialOrders.Count, Is.EqualTo(playerCount));
            Assert.That(_placed.Select(row => row.PlayerId), Is.EqualTo(wallets.Keys));
            _clock.At(2, 12);
            book.RefreshArrivals();
            Assert.That(_arrived.Count, Is.EqualTo(playerCount));
            Assert.That(_arrived.Select(row => row.PlayerId), Is.EqualTo(wallets.Keys));
            string before = SaveSerialization.ToJson(_save);
            foreach (string player in wallets.Keys)
                foreach (var other in ids.Where(pair => pair.Key != player))
                {
                    Assert.That(book.Collect(player, other.Value, stocks[player]), Is.EqualTo(SpecialOrderRefusal.UnknownOrder));
                    Assert.That(SaveSerialization.ToJson(_save), Is.EqualTo(before));
                    Assert.That(stocks[player], Is.Empty);
                    Assert.That(wallets[player].Money, Is.EqualTo(994));
                    Assert.That(wallets[player].Calls, Is.EqualTo(1));
                    Assert.That(book.Capture(player).Orders.Select(row => row.OrderId), Is.EqualTo(new[] { ids[player] }));
                    Assert.That(_collected, Is.Empty);
                }
            Assert.That(typeof(SpecialOrderBook).GetMethod("Cancel"), Is.Null,
                "No player can cancel or refund a paid order without a settlement contract.");
            foreach (string player in wallets.Keys)
            {
                Assert.That(book.Collect(player, ids[player], stocks[player]), Is.EqualTo(SpecialOrderRefusal.None));
                Assert.That(book.Collect(player, ids[player], stocks[player]), Is.EqualTo(SpecialOrderRefusal.UnknownOrder));
                Assert.That(stocks[player], Is.EqualTo(new[] { new BaitStock(Item, 2) }));
                Assert.That(book.Capture(player).Orders, Is.Empty);
                Assert.That(wallets[player].Money, Is.EqualTo(994));
                Assert.That(wallets[player].Calls, Is.EqualTo(1));
            }
            Assert.That(_collected.Count, Is.EqualTo(playerCount));
            Assert.That(_collected.Select(row => row.PlayerId), Is.EqualTo(wallets.Keys));
            Assert.That(_save.SpecialOrders.Select(row => row.State),
                Is.EqualTo(Enumerable.Repeat(SpecialOrderDto.StateCollected, playerCount)));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase(" ")]
        [TestCase("player.unknown")]
        public void UnknownPlayerCannotPayBrowseOrCollect(string player)
        {
            _book.Dispose();
            var accounts = new SessionSpecialOrderAccounts(new Dictionary<string, IWallet> { [Player] = _wallet });
            using var book = new SpecialOrderBook(_save,
                new[] { new SpecialOrderLine(Line, Item, Seller, 3, 2, 20) }, accounts, _clock, Supply());
            Assert.That(book.Place(Player, Seller, Line, 1, out string id), Is.EqualTo(SpecialOrderRefusal.None));
            _clock.At(2);
            book.RefreshArrivals();
            string before = SaveSerialization.ToJson(_save);
            Assert.That(book.Place(player, Seller, Line, 1, out var refused), Is.EqualTo(SpecialOrderRefusal.CannotPay));
            Assert.That(refused, Is.Null);
            Assert.That(book.Capture(player).Orders, Is.Empty);
            Assert.That(book.Collect(player, id, _bait), Is.EqualTo(SpecialOrderRefusal.UnknownOrder));
            Assert.That(SaveSerialization.ToJson(_save), Is.EqualTo(before));
            Assert.That(_wallet.Money, Is.EqualTo(997));
            Assert.That(_wallet.Calls, Is.EqualTo(1));
            Assert.That(accounts.CaptureOwnership().Length, Is.EqualTo(1));
            Assert.That(_placed.Count, Is.EqualTo(1));
            Assert.That(_arrived.Count, Is.EqualTo(1));
            Assert.That(_collected, Is.Empty);
            Assert.That(_bait, Is.Empty);
        }

        [TestCase(SpecialOrderDto.StateWaiting)]
        [TestCase(SpecialOrderDto.StateArrived)]
        [TestCase(SpecialOrderDto.StateCollected)]
        public void RestartWithoutPayerIdentityPreservesRowsButGrantsNoAccess(int state)
        {
            _book.Dispose();
            var wallets = new Dictionary<string, IWallet> { [Player] = _wallet, ["player.second"] = new TestWallet() };
            var accounts = new SessionSpecialOrderAccounts(wallets);
            using (var book = new SpecialOrderBook(_save,
                new[] { new SpecialOrderLine(Line, Item, Seller, 3, 2, 20) }, accounts, _clock, Supply()))
            {
                Assert.That(book.Place(Player, Seller, Line, 2, out _), Is.EqualTo(SpecialOrderRefusal.None));
                _save.SpecialOrders.Single().State = state;
            }
            var loaded = SaveSerialization.FromJson(SaveSerialization.ToJson(_save));
            AssertSavedOrdersEqual(_save, loaded);
            string before = SaveSerialization.ToJson(loaded);
            var freshAccounts = new SessionSpecialOrderAccounts(wallets);
            using var resumed = new SpecialOrderBook(loaded,
                new[] { new SpecialOrderLine(Line, Item, Seller, 3, 2, 20) }, freshAccounts, _clock, Supply());
            _clock.At(8);
            resumed.RefreshArrivals();
            foreach (string player in wallets.Keys)
            {
                Assert.That(resumed.Capture(player).Orders, Is.Empty);
                Assert.That(resumed.Collect(player, loaded.SpecialOrders.Single().OrderId, loaded.BaitStock),
                    Is.EqualTo(SpecialOrderRefusal.UnknownOrder));
            }
            Assert.That(SaveSerialization.ToJson(loaded), Is.EqualTo(before));
            Assert.That(loaded.BaitStock, Is.Empty);
            Assert.That(loaded.Money, Is.EqualTo(994));
            Assert.That(_wallet.Calls, Is.EqualTo(1));
            Assert.That(_placed.Count, Is.EqualTo(1));
            Assert.That(_arrived, Is.Empty);
            Assert.That(_collected, Is.Empty);
            Assert.That(freshAccounts.CaptureOwnership(), Is.Empty);
            Assert.That(accounts.CaptureOwnership().Single().PlayerId, Is.EqualTo(Player));
        }

        [Test]
        public void SameSessionReconstructionKeepsOwnerAndCollectedReceiptWithoutASecondGrant()
        {
            _book.Dispose();
            var other = new TestWallet();
            var wallets = new Dictionary<string, IWallet> { [Player] = _wallet, ["player.second"] = other };
            var accounts = new SessionSpecialOrderAccounts(wallets);
            var offers = new[] { new SpecialOrderLine(Line, Item, Seller, 3, 2, 20) };
            string id;
            using (var book = new SpecialOrderBook(_save, offers, accounts, _clock, Supply()))
                Assert.That(book.Place(Player, Seller, Line, 2, out id), Is.EqualTo(SpecialOrderRefusal.None));
            var snapshot = accounts.CaptureOwnership();
            snapshot.Single().PlayerId = "player.second";
            snapshot.Single().OrderId = "order.seller.test.99";
            wallets[Player] = other;
            var loaded = SaveSerialization.FromJson(SaveSerialization.ToJson(_save));
            using var resumed = new SpecialOrderBook(loaded, offers, accounts, _clock, Supply());
            _clock.At(2);
            resumed.RefreshArrivals();
            Assert.That(resumed.Capture(Player).Orders.Single().OrderId, Is.EqualTo(id));
            Assert.That(resumed.Capture("player.second").Orders, Is.Empty);
            Assert.That(resumed.Collect("player.second", id, loaded.BaitStock), Is.EqualTo(SpecialOrderRefusal.UnknownOrder));
            Assert.That(resumed.Collect(Player, id, loaded.BaitStock), Is.EqualTo(SpecialOrderRefusal.None));
            var again = SaveSerialization.FromJson(SaveSerialization.ToJson(loaded));
            using var final = new SpecialOrderBook(again, offers, accounts, _clock, Supply());
            Assert.That(final.Collect(Player, id, again.BaitStock), Is.EqualTo(SpecialOrderRefusal.UnknownOrder));
            Assert.That(again.BaitStock, Is.EqualTo(new[] { new BaitStock(Item, 2) }));
            Assert.That(accounts.OwnerOf(id), Is.EqualTo(Player));
            Assert.That(accounts.CaptureOwnership().Single().OrderId, Is.EqualTo(id));
            Assert.That(_wallet.Money, Is.EqualTo(994));
            Assert.That(other.Money, Is.EqualTo(1000));
            Assert.That(_arrived.Count, Is.EqualTo(1));
            Assert.That(_collected.Count, Is.EqualTo(1));
        }

        [Test]
        public void FailedPaymentLeavesNoOwnerAndAnotherPlayerCannotReenterTheSameTransaction()
        {
            _book.Dispose();
            var other = new TestWallet();
            var accounts = new SessionSpecialOrderAccounts(new Dictionary<string, IWallet>
                { [Player] = _wallet, ["player.second"] = other });
            using var book = new SpecialOrderBook(_save,
                new[] { new SpecialOrderLine(Line, Item, Seller, 3, 2, 20) }, accounts, _clock, Supply());
            _wallet.Refuse = true;
            Assert.That(book.Place(Player, Seller, Line, 1, out var refused), Is.EqualTo(SpecialOrderRefusal.CannotPay));
            Assert.That(refused, Is.Null);
            Assert.That(accounts.CaptureOwnership(), Is.Empty);
            Assert.That(_save.SpecialOrders, Is.Empty);
            Assert.That(_save.NextSpecialOrderNumber, Is.EqualTo(1));
            _wallet.Refuse = false;
            var refusals = new List<SpecialOrderRefusal>();
            _wallet.DuringSpend = () => refusals.Add(book.Place("player.second", Seller, Line, 1, out _));
            Assert.That(book.Place(Player, Seller, Line, 1, out string id), Is.EqualTo(SpecialOrderRefusal.None));
            _wallet.DuringSpend = null;
            Assert.That(refusals, Is.EqualTo(new[] { SpecialOrderRefusal.Busy }));
            Assert.That(accounts.TryPay("player.second", id, 3), Is.False);
            Assert.That(accounts.OwnerOf(id), Is.EqualTo(Player));
            Assert.That(book.Place("player.second", Seller, Line, 2, out string second), Is.EqualTo(SpecialOrderRefusal.None));
            Assert.That(id, Is.EqualTo("order.seller.test.1"));
            Assert.That(second, Is.EqualTo("order.seller.test.2"));
            Assert.That(_wallet.Money, Is.EqualTo(997));
            Assert.That(_wallet.Calls, Is.EqualTo(2));
            Assert.That(other.Money, Is.EqualTo(994));
            Assert.That(other.Calls, Is.EqualTo(1));
            Assert.That(accounts.CaptureOwnership().Select(row => row.PlayerId), Is.EqualTo(new[] { Player, "player.second" }));
            Assert.That(_placed.Count, Is.EqualTo(2));
        }

        [TestCase("specialorder.Bad", Item, Seller, 3, 2, 20)]
        [TestCase(Line, "missing", Seller, 3, 2, 20)]
        [TestCase(Line, "supply.ice", Seller, 3, 2, 20)]
        [TestCase(Line, Item, "other.test", 3, 2, 20)]
        [TestCase(Line, Item, Seller, 0, 2, 20)]
        [TestCase(Line, Item, Seller, -1, 2, 20)]
        [TestCase(Line, Item, Seller, 3, 0, 20)]
        [TestCase(Line, Item, Seller, 3, 2, 0)]
        public void InvalidLineDataIsRejected(string id, string item, string seller, int price, int wait, int limit)
        {
            Assert.Throws<ArgumentException>(() => new SpecialOrderLine(id, item, seller, price, wait, limit));
        }
    }
}
