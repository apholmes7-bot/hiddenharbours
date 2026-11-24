using System;
using System.Collections.Generic;
using HiddenHarbours.Core;
using HiddenHarbours.Economy;
using NUnit.Framework;
using UnityEngine;

namespace HiddenHarbours.Tests.Economy
{
    public sealed class SpecialOrderBookTests
    {
        private const string Seller = "seller.test", Line = "specialorder.herring", Item = "bait.herring";
        private readonly List<SpecialOrderBook> _books = new();
        private readonly List<SpecialOrderReceipt> _placed = new(), _arrived = new(), _collected = new();
        private TestWallet _wallet;
        private TestClock _clock;
        private SpecialOrderBook _book;

        private sealed class TestWallet : IWallet
        {
            public int Money { get; private set; } = 1000;
            public int Calls;
            public bool Refuse;
            public Action DuringSpend;
            public void Add(int amount) => Money += amount;
            public bool TrySpend(int amount)
            {
                Calls++;
                DuringSpend?.Invoke();
                if (Refuse || amount > Money) return false;
                Money -= amount;
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
            _wallet = new TestWallet(); _clock = new TestClock();
            _placed.Clear(); _arrived.Clear(); _collected.Clear();
            EventBus.Subscribe<SpecialOrderPlaced>(OnPlaced);
            EventBus.Subscribe<SpecialOrderArrived>(OnArrived);
            EventBus.Subscribe<SpecialOrderCollected>(OnCollected);
            _book = Book();
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
            TestWallet wallet = null, TestClock clock = null)
        {
            var book = new SpecialOrderBook(Seller, new[] { new SpecialOrderLine(Line, Item, Seller, price, wait, limit) },
                wallet ?? _wallet, clock ?? _clock);
            _books.Add(book);
            return book;
        }

        private int Place(int quantity = 1)
        {
            Assert.That(_book.Place(Line, quantity, out int id), Is.EqualTo(SpecialOrderRefusal.None));
            return id;
        }

        private static string Json(SpecialOrderBook book) => JsonUtility.ToJson(book.Capture());

        [TestCase(null, 1, SpecialOrderRefusal.UnknownLine)]
        [TestCase("specialorder.missing", 1, SpecialOrderRefusal.UnknownLine)]
        [TestCase(Line, 0, SpecialOrderRefusal.InvalidQuantity)]
        [TestCase(Line, -1, SpecialOrderRefusal.InvalidQuantity)]
        [TestCase(Line, 21, SpecialOrderRefusal.LimitReached)]
        [TestCase(Line, int.MaxValue, SpecialOrderRefusal.LimitReached)]
        public void BadRequests_ChangeNothing(string line, int quantity, SpecialOrderRefusal expected)
        {
            string before = Json(_book);
            Assert.That(_book.Place(line, quantity, out int id), Is.EqualTo(expected));
            Assert.That(id, Is.Zero);
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
            Assert.That(_book.Place(Line, 1, out int id), Is.EqualTo(SpecialOrderRefusal.CannotPay));
            Assert.That(id, Is.Zero);
            Assert.That(_wallet.Money, Is.EqualTo(money));
            Assert.That(_wallet.Calls, Is.EqualTo(1));
            Assert.That(_book.Capture().Orders, Is.Empty);
            Assert.That(_book.Capture().NextOrderId, Is.EqualTo(1));
            Assert.That(_placed, Is.Empty);
        }

        [Test]
        public void PaymentIsOnceAtPlacement_WithExactReceipt()
        {
            _clock.At(7, 23.9);
            int id = Place(4);
            Assert.That(_wallet.Money, Is.EqualTo(988));
            Assert.That(_placed.Count, Is.EqualTo(1));
            var receipt = _placed[0];
            Assert.That(receipt.OrderId, Is.EqualTo(id));
            Assert.That(receipt.SellerId, Is.EqualTo(Seller));
            Assert.That(receipt.LineId, Is.EqualTo(Line));
            Assert.That(receipt.ItemId, Is.EqualTo(Item));
            Assert.That(receipt.Quantity, Is.EqualTo(4));
            Assert.That(receipt.DayPlaced, Is.EqualTo(7));
            Assert.That(receipt.DayDue, Is.EqualTo(9));
            _clock.StartDay(9);
            Assert.That(_book.Collect(id, (item, count) => item == Item && count == 4), Is.EqualTo(SpecialOrderRefusal.None));
            Assert.That(_wallet.Calls, Is.EqualTo(1));
            Assert.That(_wallet.Money, Is.EqualTo(988));
            Assert.That(_book.Capture().Orders, Is.Empty);
            Assert.That(_collected.Count, Is.EqualTo(1));
            Assert.That(_collected[0], Is.EqualTo(receipt));
        }

        [Test]
        public void ArrivesExactlyAtDueMidnight_OnlyOnce()
        {
            _clock.At(0, 23.999);
            Place();
            _clock.StartDay(1);
            _clock.At(1, 23.999);
            _book.RefreshArrivals();
            Assert.That(_arrived, Is.Empty);
            Assert.That(_book.Capture().Orders[0].Arrived, Is.False);
            _clock.StartDay(2);
            Assert.That(_arrived.Count, Is.EqualTo(1));
            Assert.That(_arrived[0].DayDue, Is.EqualTo(2));
            Assert.That(_book.Capture().Orders[0].Arrived, Is.True);
            _clock.StartDay(3);
            _book.RefreshArrivals();
            Assert.That(_arrived.Count, Is.EqualTo(1));
        }

        [Test]
        public void SleepingPastDueDay_CatchesUpWithoutChangingPromisedDay()
        {
            _clock.At(5, 22);
            Place();
            _clock.At(9, 8);
            _book.RefreshArrivals();
            Assert.That(_arrived.Count, Is.EqualTo(1));
            Assert.That(_arrived[0].DayDue, Is.EqualTo(7));
            Assert.That(_book.Capture().Orders[0].Arrived, Is.True);
            _book.RefreshArrivals();
            Assert.That(_arrived.Count, Is.EqualTo(1));
        }

        [Test]
        public void SameLineOrdersRemainSeparate_LimitIncludesUncollectedArrivals()
        {
            int first = Place(12), second = Place(8);
            Assert.That(second, Is.EqualTo(first + 1));
            _clock.StartDay(2);
            Assert.That(_book.Capture().Orders.Length, Is.EqualTo(2));
            Assert.That(_arrived.Count, Is.EqualTo(2));
            Assert.That(_book.Place(Line, 1, out _), Is.EqualTo(SpecialOrderRefusal.LimitReached));
            Assert.That(_book.Collect(second, (_, count) => count == 8), Is.EqualTo(SpecialOrderRefusal.None));
            Assert.That(_book.Place(Line, 8, out int third), Is.EqualTo(SpecialOrderRefusal.None));
            Assert.That(third, Is.EqualTo(second + 1));
            Assert.That(_book.Place(Line, 1, out _), Is.EqualTo(SpecialOrderRefusal.LimitReached));
            Assert.That(_book.Capture().Orders[0].OrderId, Is.EqualTo(first));
        }

        [Test]
        public void CollectionRefusals_LeavePaidGoodsAndMoneyUntouched()
        {
            int id = Place(3), calls = 0;
            bool Full(string _, int __) { calls++; return false; }
            Assert.That(_book.Collect(id + 1, Full), Is.EqualTo(SpecialOrderRefusal.UnknownOrder));
            Assert.That(_book.Collect(id, Full), Is.EqualTo(SpecialOrderRefusal.NotArrived));
            Assert.That(calls, Is.Zero);
            _clock.StartDay(2);
            string before = Json(_book);
            Assert.That(_book.Collect(id, null), Is.EqualTo(SpecialOrderRefusal.HandsAndBagFull));
            Assert.That(_book.Collect(id, Full), Is.EqualTo(SpecialOrderRefusal.HandsAndBagFull));
            Assert.That(calls, Is.EqualTo(1));
            Assert.That(Json(_book), Is.EqualTo(before));
            Assert.That(_wallet.Money, Is.EqualTo(991));
            Assert.That(_collected, Is.Empty);
            Assert.That(_book.Collect(id, (_, __) => true), Is.EqualTo(SpecialOrderRefusal.None));
            Assert.That(_book.Collect(id, Full), Is.EqualTo(SpecialOrderRefusal.UnknownOrder));
            Assert.That(calls, Is.EqualTo(1));
            Assert.That(_collected.Count, Is.EqualTo(1));
        }

        [Test]
        public void SnapshotRoundTrip_IsIdenticalDeepCopiedAndDoesNotReplayMoneyOrEvents()
        {
            Place(2);
            _clock.StartDay(2);
            Place(3);
            string json = Json(_book);
            var decoded = JsonUtility.FromJson<SpecialOrderSnapshot>(json);
            var restored = Book();
            restored.Restore(decoded);
            Assert.That(Json(restored), Is.EqualTo(json));
            Assert.That(_wallet.Calls, Is.EqualTo(2));
            Assert.That(_placed.Count, Is.EqualTo(2));
            Assert.That(_arrived.Count, Is.EqualTo(1));
            decoded.Orders[0].Quantity = 900;
            var captured = restored.Capture();
            captured.Orders[1].Arrived = true;
            Assert.That(Json(restored), Is.EqualTo(json));
            restored.RefreshArrivals();
            Assert.That(_arrived.Count, Is.EqualTo(1));
            Assert.That(restored.Place(Line, 1, out int id), Is.EqualTo(SpecialOrderRefusal.None));
            Assert.That(id, Is.EqualTo(3));
        }

        [TestCase("seller")]
        [TestCase("next")]
        [TestCase("missing")]
        [TestCase("quantity")]
        [TestCase("placed")]
        [TestCase("due")]
        [TestCase("duplicate")]
        [TestCase("null_row")]
        [TestCase("null_orders")]
        public void MalformedRestoreIsAtomic(string corruption)
        {
            Place(); Place();
            string before = Json(_book);
            var snapshot = _book.Capture();
            switch (corruption)
            {
                case "seller": snapshot.SellerId = "seller.other"; break;
                case "next": snapshot.NextOrderId = 2; break;
                case "missing": snapshot.Orders[1].LineId = "specialorder.missing"; break;
                case "quantity": snapshot.Orders[1].Quantity = 0; break;
                case "placed": snapshot.Orders[1].DayPlaced = -1; break;
                case "due": snapshot.Orders[1].DayDue = 0; break;
                case "duplicate": snapshot.Orders[1].OrderId = 1; break;
                case "null_row": snapshot.Orders[1] = null; break;
                case "null_orders": snapshot.Orders = null; break;
            }
            Assert.Throws<ArgumentException>(() => _book.Restore(snapshot));
            Assert.That(Json(_book), Is.EqualTo(before));
            Assert.That(_wallet.Calls, Is.EqualTo(2));
            Assert.That(_placed.Count, Is.EqualTo(2));
        }

        [Test]
        public void PaidTermsSurviveContentRetuning()
        {
            Place(10);
            var restored = Book(price: 100, wait: 8, limit: 1);
            restored.Restore(_book.Capture());
            Assert.That(Json(restored), Is.EqualTo(Json(_book)));
            Assert.That(restored.Place(Line, 1, out _), Is.EqualTo(SpecialOrderRefusal.LimitReached));
            _clock.StartDay(2);
            Assert.That(restored.Collect(1, (_, count) => count == 10), Is.EqualTo(SpecialOrderRefusal.None));
            Assert.That(_wallet.Calls, Is.EqualTo(1));
        }

        [Test]
        public void IdenticalClockAndRequests_AreDeterministic_RegardlessOfCatalogueOrder()
        {
            var other = new SpecialOrderLine("specialorder.other", "bait.other", Seller, 5, 4, 10);
            var line = new SpecialOrderLine(Line, Item, Seller, 3, 2, 20);
            using var a = new SpecialOrderBook(Seller, new[] { line, other }, new TestWallet(), _clock);
            using var b = new SpecialOrderBook(Seller, new[] { other, line }, new TestWallet(), _clock);
            _clock.At(123, 17.5);
            foreach (var book in new[] { a, b })
            {
                Assert.That(book.Place(Line, 4, out _), Is.EqualTo(SpecialOrderRefusal.None));
                Assert.That(book.Place(other.Id, 2, out _), Is.EqualTo(SpecialOrderRefusal.None));
            }
            Assert.That(Json(a), Is.EqualTo(Json(b)));
            _clock.StartDay(125);
            Assert.That(Json(a), Is.EqualTo(Json(b)));
            Assert.That(a.Capture().Orders[0].Arrived, Is.True);
            Assert.That(a.Capture().Orders[1].Arrived, Is.False);
        }

        [Test]
        public void NumericBoundsRefuseBeforePayment()
        {
            var expensive = Book(price: int.MaxValue);
            Assert.That(expensive.Place(Line, 2, out _), Is.EqualTo(SpecialOrderRefusal.PriceOutOfRange));
            _clock.At(int.MaxValue);
            Assert.That(_book.Place(Line, 1, out _), Is.EqualTo(SpecialOrderRefusal.DayOutOfRange));
            _clock.At(-1);
            Assert.That(_book.Place(Line, 1, out _), Is.EqualTo(SpecialOrderRefusal.DayOutOfRange));
            _clock.At(0);
            _book.Restore(new SpecialOrderSnapshot { SellerId = Seller, NextOrderId = int.MaxValue });
            Assert.That(_book.Place(Line, 1, out _), Is.EqualTo(SpecialOrderRefusal.OrderNumbersExhausted));
            Assert.That(_wallet.Calls, Is.Zero);
            Assert.That(_placed, Is.Empty);
        }

        [Test]
        public void ReentrantCallbacksCannotSpendOrCollectTwice()
        {
            var refusals = new List<SpecialOrderRefusal>();
            _wallet.DuringSpend = () => refusals.Add(_book.Place(Line, 1, out _));
            int id = Place();
            _wallet.DuringSpend = null;
            _clock.StartDay(2);
            Assert.That(_book.Collect(id, (_, __) =>
            {
                refusals.Add(_book.Collect(id, (item, count) => true));
                Assert.Throws<InvalidOperationException>(() => _book.Restore(_book.Capture()));
                return true;
            }), Is.EqualTo(SpecialOrderRefusal.None));
            Assert.That(refusals, Is.EqualTo(new[] { SpecialOrderRefusal.Busy, SpecialOrderRefusal.Busy }));
            Assert.That(_wallet.Calls, Is.EqualTo(1));
            Assert.That(_collected.Count, Is.EqualTo(1));
        }

        [Test]
        public void DisposingUnsubscribesAndOtherCountersCannotPlaceThisLine()
        {
            Place();
            _book.Dispose(); _book.Dispose();
            _clock.StartDay(2);
            Assert.That(_arrived, Is.Empty);
            Assert.Throws<ObjectDisposedException>(() => _book.Place(Line, 1, out _));
            using var other = new SpecialOrderBook("seller.other",
                new[] { new SpecialOrderLine(Line, Item, Seller, 3, 2, 20) }, _wallet, _clock);
            Assert.That(other.Place(Line, 1, out _), Is.EqualTo(SpecialOrderRefusal.UnknownLine));
        }

        [TestCase("specialorder.Bad", Item, Seller, 3, 2, 20)]
        [TestCase(Line, "missing", Seller, 3, 2, 20)]
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
