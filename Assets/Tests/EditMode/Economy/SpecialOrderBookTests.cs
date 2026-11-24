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
        private const string Seller = "seller.test", Line = "specialorder.herring", Item = "bait.herring";
        private readonly List<SpecialOrderBook> _books = new();
        private readonly List<SpecialOrderReceipt> _placed = new(), _arrived = new(), _collected = new();
        private TestWallet _wallet;
        private TestClock _clock;
        private SpecialOrderBook _book;
        private List<BaitStock> _bait;

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
            _bait = new List<BaitStock>();
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
            var book = new SpecialOrderBook(new[] { new SpecialOrderLine(Line, Item, Seller, price, wait, limit) },
                wallet ?? _wallet, clock ?? _clock);
            _books.Add(book);
            return book;
        }

        private string Place(int quantity = 1)
        {
            Assert.That(_book.Place(Seller, Line, quantity, out string id), Is.EqualTo(SpecialOrderRefusal.None));
            return id;
        }

        private static string Json(SpecialOrderBook book) => JsonUtility.ToJson(book.Capture());

        private static void AssertReceipt(SpecialOrderReceipt actual, SpecialOrderReceipt expected)
        {
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
            Assert.That(_book.Place(Seller, line, quantity, out string id), Is.EqualTo(expected));
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
            Assert.That(_book.Place(Seller, Line, 1, out string id), Is.EqualTo(SpecialOrderRefusal.CannotPay));
            Assert.That(id, Is.Null);
            Assert.That(_wallet.Money, Is.EqualTo(money));
            Assert.That(_wallet.Calls, Is.EqualTo(1));
            Assert.That(_book.Capture().Orders, Is.Empty);
            Assert.That(_book.Capture().NextSpecialOrderNumber, Is.EqualTo(1));
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
            var paid = _book.Capture().Orders.Single();
            Assert.That(paid.PaidPrice, Is.EqualTo(12));
            Assert.That(paid.Lines.Single().PaidPrice, Is.EqualTo(12));
            Assert.That(paid.OrderedAtSeconds, Is.EqualTo(_clock.TotalSeconds));
            _clock.StartDay(9);
            Assert.That(_book.Collect(id, _bait), Is.EqualTo(SpecialOrderRefusal.None));
            Assert.That(_wallet.Calls, Is.EqualTo(1));
            Assert.That(_wallet.Money, Is.EqualTo(988));
            Assert.That(_book.Capture().Orders, Is.Empty);
            Assert.That(_collected.Count, Is.EqualTo(1));
            AssertReceipt(_collected[0], receipt);
            Assert.That(_bait, Is.EqualTo(new[] { new BaitStock(Item, 4) }));
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
            Assert.That(_arrived[0].DueDay, Is.EqualTo(2));
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
            Assert.That(_arrived[0].DueDay, Is.EqualTo(7));
            Assert.That(_book.Capture().Orders[0].Arrived, Is.True);
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
            Assert.That(_book.Capture().Orders.Length, Is.EqualTo(2));
            Assert.That(_arrived.Count, Is.EqualTo(2));
            Assert.That(_book.Place(Seller, Line, 1, out _), Is.EqualTo(SpecialOrderRefusal.LimitReached));
            Assert.That(_book.Collect(second, _bait), Is.EqualTo(SpecialOrderRefusal.None));
            Assert.That(_book.Place(Seller, Line, 8, out string third), Is.EqualTo(SpecialOrderRefusal.None));
            Assert.That(third, Is.EqualTo("order.seller.test.3"));
            Assert.That(_bait.Single().Count, Is.EqualTo(8));
            Assert.That(_book.Place(Seller, Line, 1, out _), Is.EqualTo(SpecialOrderRefusal.LimitReached));
            Assert.That(_book.Capture().Orders[0].OrderId, Is.EqualTo(first));
        }

        [Test]
        public void CollectionRefusals_LeavePaidGoodsAndMoneyUntouched()
        {
            string id = Place(3);
            var full = new List<BaitStock> { new BaitStock(Item, int.MaxValue) };
            Assert.That(_book.Collect("order.seller.test.2", full), Is.EqualTo(SpecialOrderRefusal.UnknownOrder));
            Assert.That(_book.Collect(id, full), Is.EqualTo(SpecialOrderRefusal.NotArrived));
            Assert.That(full.Single().Count, Is.EqualTo(int.MaxValue));
            _clock.StartDay(2);
            string before = Json(_book);
            Assert.That(_book.Collect(id, null), Is.EqualTo(SpecialOrderRefusal.BaitStoreUnavailable));
            Assert.That(_book.Collect(id, full), Is.EqualTo(SpecialOrderRefusal.BaitStoreUnavailable));
            Assert.That(full.Single().Count, Is.EqualTo(int.MaxValue));
            Assert.That(Json(_book), Is.EqualTo(before));
            Assert.That(_wallet.Money, Is.EqualTo(991));
            Assert.That(_collected, Is.Empty);
            Assert.That(_book.Collect(id, _bait), Is.EqualTo(SpecialOrderRefusal.None));
            Assert.That(_book.Collect(id, _bait), Is.EqualTo(SpecialOrderRefusal.UnknownOrder));
            Assert.That(_bait.Single(), Is.EqualTo(new BaitStock(Item, 3)));
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
            decoded.Orders[0].Lines[0].Count = 900;
            var captured = restored.Capture();
            captured.Orders[1].State = SpecialOrderState.ArrivedState;
            captured.Orders[0].Lines[0].PaidPrice = 900;
            Assert.That(Json(restored), Is.EqualTo(json));
            restored.RefreshArrivals();
            Assert.That(_arrived.Count, Is.EqualTo(1));
            Assert.That(restored.Place(Seller, Line, 1, out string id), Is.EqualTo(SpecialOrderRefusal.None));
            Assert.That(id, Is.EqualTo("order.seller.test.3"));
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
            var snapshot = _book.Capture();
            var damaged = snapshot.Orders[1];
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
                case "empty_lines": damaged.Lines = Array.Empty<SpecialOrderLineState>(); break;
                case "null_line": damaged.Lines[0] = null; break;
                case "duplicate_line": damaged.Lines = new[] { damaged.Lines[0], damaged.Lines[0].Copy() }; break;
                case "payment": damaged.PaidPrice = 29; break;
                case "negative_payment": damaged.PaidPrice = -6; break;
                case "negative_share": damaged.Lines[0].PaidPrice = -6; break;
                case "nan_time": damaged.OrderedAtSeconds = double.NaN; break;
                case "negative_time": damaged.OrderedAtSeconds = -1; break;
                case "infinite_time": damaged.OrderedAtSeconds = double.PositiveInfinity; break;
                case "state": damaged.State = 91; break;
                case "destination": damaged.DeliverToId = "wharf.future"; break;
            }
            var restored = Book();
            Assert.DoesNotThrow(() => restored.Restore(snapshot));
            var rows = restored.Capture().Orders;
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
            restored.Restore(restored.Capture());
            Assert.That(restored.Capture().Orders[1].RefundReasonKey, Is.EqualTo(reason));
            _clock.At(8);
            restored.RefreshArrivals();
            Assert.That(restored.Collect(damaged.OrderId, _bait), Is.EqualTo(SpecialOrderRefusal.RefundRequired));
            Assert.That(_bait, Is.Empty);
            Assert.That(_arrived.Select(receipt => receipt.OrderId),
                Is.EqualTo(new[] { "order.seller.test.1", "order.seller.test.3" }));
            Assert.That(restored.Collect(rows[0].OrderId, _bait), Is.EqualTo(SpecialOrderRefusal.None));
            Assert.That(_wallet.Calls, Is.EqualTo(3));
            Assert.That(_wallet.Money, Is.EqualTo(988));
            Assert.That(_placed.Count, Is.EqualTo(3));
        }

        [Test]
        public void PaidTermsSurviveContentRetuning()
        {
            Place(10);
            var restored = Book(price: 100, wait: 8, limit: 1);
            restored.Restore(_book.Capture());
            Assert.That(Json(restored), Is.EqualTo(Json(_book)));
            Assert.That(restored.Place(Seller, Line, 1, out _), Is.EqualTo(SpecialOrderRefusal.LimitReached));
            _clock.StartDay(2);
            Assert.That(restored.Collect("order.seller.test.1", _bait), Is.EqualTo(SpecialOrderRefusal.None));
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
            using var a = new SpecialOrderBook(new[] { line, other }, new TestWallet(), _clock);
            using var b = new SpecialOrderBook(new[] { other, line }, new TestWallet(), _clock);
            _clock.At(123, 17.5);
            foreach (var book in new[] { a, b })
            {
                Assert.That(book.Place(Seller, Line, 4, out _), Is.EqualTo(SpecialOrderRefusal.None));
                Assert.That(book.Place(Seller, other.Id, 2, out _), Is.EqualTo(SpecialOrderRefusal.None));
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
            Assert.That(expensive.Place(Seller, Line, 2, out _), Is.EqualTo(SpecialOrderRefusal.PriceOutOfRange));
            _clock.At(int.MaxValue);
            Assert.That(_book.Place(Seller, Line, 1, out _), Is.EqualTo(SpecialOrderRefusal.DayOutOfRange));
            _clock.At(-1);
            Assert.That(_book.Place(Seller, Line, 1, out _), Is.EqualTo(SpecialOrderRefusal.DayOutOfRange));
            _clock.At(0);
            _book.Restore(new SpecialOrderSnapshot { NextSpecialOrderNumber = int.MaxValue });
            Assert.That(_book.Place(Seller, Line, 1, out _), Is.EqualTo(SpecialOrderRefusal.OrderNumbersExhausted));
            Assert.That(_wallet.Calls, Is.Zero);
            Assert.That(_placed, Is.Empty);
        }

        [Test]
        public void ReentrantCallbacksCannotSpendOrCollectTwice()
        {
            var refusals = new List<SpecialOrderRefusal>();
            _wallet.DuringSpend = () => refusals.Add(_book.Place(Seller, Line, 1, out _));
            string id = Place();
            _wallet.DuringSpend = null;
            _clock.StartDay(2);
            Exception restoreError = null;
            Action<SpecialOrderCollected> reenter = _ =>
            {
                refusals.Add(_book.Collect(id, _bait));
                try { _book.Restore(_book.Capture()); }
                catch (Exception error) { restoreError = error; }
            };
            EventBus.Subscribe(reenter);
            try { Assert.That(_book.Collect(id, _bait), Is.EqualTo(SpecialOrderRefusal.None)); }
            finally { EventBus.Unsubscribe(reenter); }
            Assert.That(refusals, Is.EqualTo(new[] { SpecialOrderRefusal.Busy, SpecialOrderRefusal.Busy }));
            Assert.That(restoreError, Is.TypeOf<InvalidOperationException>());
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
            Assert.Throws<ObjectDisposedException>(() => _book.Place(Seller, Line, 1, out _));
            using var other = new SpecialOrderBook(
                new[] { new SpecialOrderLine(Line, Item, Seller, 3, 2, 20) }, _wallet, _clock);
            Assert.That(other.Place("seller.other", Line, 1, out _), Is.EqualTo(SpecialOrderRefusal.UnknownLine));
        }

        [Test]
        public void SellersShareOneCounter_AndCollectionNeverReusesANumber()
        {
            const string otherSeller = "seller.other", otherLine = "specialorder.other";
            using var book = new SpecialOrderBook(new[]
            {
                new SpecialOrderLine(Line, Item, Seller, 3, 2, 20),
                new SpecialOrderLine(otherLine, "bait.other", otherSeller, 5, 1, 10)
            }, _wallet, _clock);
            Assert.That(book.Place(Seller, Line, 1, out string first), Is.EqualTo(SpecialOrderRefusal.None));
            Assert.That(book.Place(otherSeller, otherLine, 2, out string second), Is.EqualTo(SpecialOrderRefusal.None));
            Assert.That(first, Is.EqualTo("order.seller.test.1"));
            Assert.That(second, Is.EqualTo("order.seller.other.2"));
            _clock.At(4);
            Assert.That(book.Collect(first, _bait), Is.EqualTo(SpecialOrderRefusal.None));
            Assert.That(book.Collect(second, _bait), Is.EqualTo(SpecialOrderRefusal.None));
            Assert.That(book.Capture().Orders, Is.Empty);
            Assert.That(book.Capture().NextSpecialOrderNumber, Is.EqualTo(3));
            book.Restore(new SpecialOrderSnapshot { NextSpecialOrderNumber = 1 });
            Assert.That(book.Place(otherSeller, otherLine, 1, out string third), Is.EqualTo(SpecialOrderRefusal.None));
            Assert.That(third, Is.EqualTo("order.seller.other.3"));
            Assert.That(_wallet.Money, Is.EqualTo(982));
        }

        private SpecialOrderBook MultiLineBook()
        {
            var book = new SpecialOrderBook(new[]
            {
                new SpecialOrderLine(Line, Item, Seller, 3, 2, 20),
                new SpecialOrderLine("specialorder.other", "bait.other", Seller, 5, 4, 10),
                new SpecialOrderLine("specialorder.foreign", "bait.foreign", "seller.other", 7, 3, 6)
            }, _wallet, _clock);
            _books.Add(book);
            return book;
        }

        [Test]
        public void MultipleLines_PayOnce_ArriveTogether_AndCollectEveryBaitAtomically()
        {
            var book = MultiLineBook();
            _clock.At(3, 17.25);
            Assert.That(book.Place(Seller, new[]
            {
                new SpecialOrderRequest(Line, 2), new SpecialOrderRequest("specialorder.other", 3)
            }, out string id), Is.EqualTo(SpecialOrderRefusal.None));
            var row = book.Capture().Orders.Single();
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
            Assert.That(book.Collect(id, _bait), Is.EqualTo(SpecialOrderRefusal.NotArrived));
            Assert.That(_bait, Is.Empty);
            _clock.At(7);
            book.RefreshArrivals();
            Assert.That(_bait, Is.Empty, "Arrival holds goods at the counter.");
            var full = new List<BaitStock> { new BaitStock("bait.other", int.MaxValue) };
            Assert.That(book.Collect(id, full), Is.EqualTo(SpecialOrderRefusal.BaitStoreUnavailable));
            Assert.That(full, Is.EqualTo(new[] { new BaitStock("bait.other", int.MaxValue) }));
            Assert.That(book.Capture().Orders.Length, Is.EqualTo(1));
            Assert.That(book.Collect(id, _bait), Is.EqualTo(SpecialOrderRefusal.None));
            Assert.That(_bait, Is.EqualTo(new[] { new BaitStock(Item, 2), new BaitStock("bait.other", 3) }));
            Assert.That(book.Capture().Orders, Is.Empty);
            AssertReceipt(_collected.Single(), _placed.Single());
        }

        [TestCase("unknown", SpecialOrderRefusal.UnknownLine)]
        [TestCase("count", SpecialOrderRefusal.InvalidQuantity)]
        [TestCase("duplicate", SpecialOrderRefusal.InvalidQuantity)]
        [TestCase("limit", SpecialOrderRefusal.LimitReached)]
        [TestCase("seller", SpecialOrderRefusal.UnknownLine)]
        public void LaterLineRefusal_ChargesNothingAndReservesNoNumber(string fault, SpecialOrderRefusal refusal)
        {
            var book = MultiLineBook();
            var second = new SpecialOrderRequest("specialorder.other", 1);
            if (fault == "unknown") second = new SpecialOrderRequest("specialorder.missing", 1);
            if (fault == "count") second = new SpecialOrderRequest("specialorder.other", 0);
            if (fault == "duplicate") second = new SpecialOrderRequest(Line, 1);
            if (fault == "limit") second = new SpecialOrderRequest("specialorder.other", 11);
            string before = Json(book);
            Assert.That(book.Place(fault == "seller" ? "seller.other" : Seller,
                new[] { new SpecialOrderRequest(Line, 1), second }, out string id), Is.EqualTo(refusal));
            Assert.That(id, Is.Null);
            Assert.That(Json(book), Is.EqualTo(before));
            Assert.That(_wallet.Calls, Is.Zero);
            Assert.That(_wallet.Money, Is.EqualTo(1000));
        }

        [Test]
        public void WholeOrderPriceOverflow_RefusesEvenWhenEachLineFits()
        {
            using var book = new SpecialOrderBook(new[]
            {
                new SpecialOrderLine(Line, Item, Seller, int.MaxValue, 1, 2),
                new SpecialOrderLine("specialorder.other", "bait.other", Seller, 1, 1, 2)
            }, _wallet, _clock);
            Assert.That(book.Place(Seller, new[]
            {
                new SpecialOrderRequest(Line, 1), new SpecialOrderRequest("specialorder.other", 1)
            }, out _), Is.EqualTo(SpecialOrderRefusal.PriceOutOfRange));
            Assert.That(_wallet.Calls, Is.Zero);
            Assert.That(book.Capture().NextSpecialOrderNumber, Is.EqualTo(1));
        }

        [Test]
        public void NullAndEmptyRequests_RefuseWithoutPayment()
        {
            Assert.That(_book.Place(Seller, null, out _), Is.EqualTo(SpecialOrderRefusal.InvalidQuantity));
            Assert.That(_book.Place(Seller, Array.Empty<SpecialOrderRequest>(), out _), Is.EqualTo(SpecialOrderRefusal.InvalidQuantity));
            Assert.That(_wallet.Calls, Is.Zero);
        }

        [Test]
        public void DuplicateIds_KeepBothPayments_BlockBothRows_AndSurviveTheAdapter()
        {
            Place(2); Place(3); Place(1);
            var snapshot = _book.Capture();
            snapshot.Orders[1].OrderId = snapshot.Orders[0].OrderId;
            var restored = Book();
            restored.Restore(snapshot);
            var rows = restored.Capture().Orders;
            Assert.That(rows.Length, Is.EqualTo(3));
            Assert.That(rows.Take(2).Select(row => row.RefundReasonKey),
                Is.EqualTo(new[] { "special_order.duplicate_id", "special_order.duplicate_id" }));
            Assert.That(rows.Select(row => row.PaidPrice), Is.EqualTo(new[] { 6, 9, 3 }));
            Assert.That(rows.Take(2).Select(row => row.RefundAmount), Is.EqualTo(new[] { 6, 9 }));
            Assert.That(rows[2].RequiresRefund, Is.False);
            _clock.At(8);
            Assert.That(restored.Collect(rows[0].OrderId, _bait), Is.EqualTo(SpecialOrderRefusal.RefundRequired));
            Assert.That(_bait, Is.Empty);
            string before = Json(restored);
            SpecialOrderRowAdapter.Restore(restored, JsonUtility.FromJson<SpecialOrderRows>(
                JsonUtility.ToJson(SpecialOrderRowAdapter.ToRows(restored))));
            Assert.That(Json(restored), Is.EqualTo(before));
            Assert.That(_wallet.Money, Is.EqualTo(982));
        }

        [Test]
        public void NullRowsAndCollectedRows_AreSkipped_ButCollectedNumbersStayUsed()
        {
            Place(); Place(2); Place(3);
            var snapshot = _book.Capture();
            snapshot.NextSpecialOrderNumber = 0;
            snapshot.Orders[1].State = SpecialOrderState.Collected;
            snapshot.Orders[1].OrderId = "order.seller.other.90";
            snapshot.Orders = new[] { null, snapshot.Orders[0], snapshot.Orders[1], snapshot.Orders[2], null };
            var restored = Book();
            Assert.DoesNotThrow(() => restored.Restore(snapshot));
            Assert.That(restored.Capture().Orders.Select(row => row.OrderId),
                Is.EqualTo(new[] { "order.seller.test.1", "order.seller.test.3" }));
            Assert.That(restored.Capture().NextSpecialOrderNumber, Is.EqualTo(91));
            Assert.That(restored.Place(Seller, Line, 1, out string id), Is.EqualTo(SpecialOrderRefusal.None));
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
            var snapshot = _book.Capture();
            snapshot.NextSpecialOrderNumber = supplied;
            var restored = Book();
            restored.Restore(snapshot);
            Assert.That(restored.Capture().Orders.Length, Is.EqualTo(3));
            Assert.That(restored.Capture().NextSpecialOrderNumber, Is.EqualTo(expected));
            restored.Restore(snapshot);
            Assert.That(restored.Capture().NextSpecialOrderNumber, Is.EqualTo(expected));
            Assert.That(_wallet.Calls, Is.EqualTo(3));
        }

        [Test]
        public void EmptyOrMissingSnapshots_DoNotResetUsedNumbers()
        {
            Place();
            string before = Json(_book);
            Assert.DoesNotThrow(() => _book.Restore(null));
            Assert.That(Json(_book), Is.EqualTo(before));
            Assert.DoesNotThrow(() => _book.Restore(new SpecialOrderSnapshot { Orders = null }));
            Assert.That(_book.Capture().Orders, Is.Empty);
            Assert.That(_book.Capture().NextSpecialOrderNumber, Is.EqualTo(2));
            var fresh = Book();
            fresh.Restore(new SpecialOrderSnapshot { NextSpecialOrderNumber = -3, Orders = null });
            Assert.That(fresh.Capture().NextSpecialOrderNumber, Is.EqualTo(1));
        }

        [TestCase("order.seller.test.2147483647")]
        [TestCase("order.seller.test.2147483646")]
        public void HighestRestoredNumbers_ExhaustWithoutOverflowOrReuse(string id)
        {
            Place();
            var snapshot = _book.Capture();
            snapshot.Orders[0].OrderId = id;
            _book.Restore(snapshot);
            Assert.That(_book.Capture().NextSpecialOrderNumber, Is.EqualTo(int.MaxValue));
            Assert.That(_book.Capture().Orders.Single().OrderId, Is.EqualTo(id));
            Assert.That(_book.Place(Seller, Line, 1, out _), Is.EqualTo(SpecialOrderRefusal.OrderNumbersExhausted));
            Assert.That(_wallet.Calls, Is.EqualTo(1));
        }

        [Test]
        public void BookToRowsToJsonToRowsToBook_IsEqualAcrossSellersAndMultipleLines()
        {
            var book = MultiLineBook();
            _clock.At(4, 13.75);
            Assert.That(book.Place(Seller, new[]
            {
                new SpecialOrderRequest(Line, 2), new SpecialOrderRequest("specialorder.other", 3)
            }, out _), Is.EqualTo(SpecialOrderRefusal.None));
            _clock.At(8);
            book.RefreshArrivals();
            Assert.That(book.Place(Seller, Line, 1, out _), Is.EqualTo(SpecialOrderRefusal.None));
            Assert.That(book.Place("seller.other", "specialorder.foreign", 2, out _), Is.EqualTo(SpecialOrderRefusal.None));
            var initial = book.Capture();
            var foreign = initial.Orders[1].Copy();
            foreign.OrderId = "order.seller.retired.9";
            foreign.SellerId = "seller.retired";
            initial.Orders = new[] { initial.Orders[0], initial.Orders[1], initial.Orders[2], foreign };
            book.Restore(initial);
            string before = Json(book);
            string text = JsonUtility.ToJson(SpecialOrderRowAdapter.ToRows(book));
            var decoded = JsonUtility.FromJson<SpecialOrderRows>(text);
            var restored = MultiLineBook();
            SpecialOrderRowAdapter.Restore(restored, decoded);
            Assert.That(Json(restored), Is.EqualTo(before));
            Assert.That(JsonUtility.ToJson(SpecialOrderRowAdapter.ToRows(restored)), Is.EqualTo(text));
            Assert.That(restored.Capture().NextSpecialOrderNumber, Is.EqualTo(10));
            Assert.That(restored.Capture().Orders.Select(row => row.State), Is.EqualTo(new[] { 1, 0, 0, 0 }));
            Assert.That(restored.Capture().Orders[2].OrderId, Is.EqualTo("order.seller.other.3"));
            Assert.That(restored.Capture().Orders[2].RequiresRefund, Is.False);
            Assert.That(restored.Capture().Orders[3].RefundAmount, Is.EqualTo(3));
            decoded.SpecialOrders[0].Lines[0].PaidPrice = 99;
            var exported = SpecialOrderRowAdapter.ToRows(restored);
            exported.SpecialOrders[0].Lines[0].Count = 99;
            Assert.That(Json(restored), Is.EqualTo(before));
            Assert.That(_wallet.Calls, Is.EqualTo(3));
            Assert.That(_placed.Count, Is.EqualTo(3));
            Assert.That(_arrived.Count, Is.EqualTo(1));
            Assert.That(_collected, Is.Empty);
        }

        [Test]
        public void RowAdapter_RetainsPaidDamagedLinesAndTotals_AndSkipsCollectedRows()
        {
            var rows = new SpecialOrderRows
            {
                NextSpecialOrderNumber = 2,
                SpecialOrders = new List<SpecialOrderRow>
                {
                    null,
                    new SpecialOrderRow { OrderId = "order.seller.test.42", State = SpecialOrderState.Collected },
                    new SpecialOrderRow { OrderId = "", PaidPrice = 11, Lines = null },
                    new SpecialOrderRow
                    {
                        OrderId = "order.seller.test.1", SellerId = Seller, OrderedDay = 1, DueDay = 3,
                        PaidPrice = 0, Lines = new List<SpecialOrderLineRow>
                        {
                            new SpecialOrderLineRow { ListingId = Line, Count = 2, PaidPrice = 6 },
                            new SpecialOrderLineRow { ListingId = "specialorder.retired", Count = 3, PaidPrice = 15 }
                        }
                    }
                }
            };
            Assert.DoesNotThrow(() => SpecialOrderRowAdapter.Restore(_book, rows));
            var snapshot = _book.Capture();
            Assert.That(snapshot.Orders.Length, Is.EqualTo(2));
            Assert.That(snapshot.Orders.Select(row => row.RefundReasonKey),
                Is.EqualTo(new[] { "special_order.empty_id", "special_order.unknown_offer" }));
            Assert.That(snapshot.Orders.Select(row => row.RefundAmount), Is.EqualTo(new[] { 11, 0 }));
            var exported = SpecialOrderRowAdapter.ToRows(_book);
            Assert.That(exported.NextSpecialOrderNumber, Is.EqualTo(43));
            Assert.That(exported.SpecialOrders.Count, Is.EqualTo(2));
            Assert.That(exported.SpecialOrders[0].OrderId, Is.EqualTo(""));
            Assert.That(exported.SpecialOrders[0].PaidPrice, Is.EqualTo(11));
            Assert.That(exported.SpecialOrders[0].Lines, Is.Null);
            Assert.That(exported.SpecialOrders[1].PaidPrice, Is.Zero);
            Assert.That(exported.SpecialOrders[1].Lines.Select(line => line.PaidPrice), Is.EqualTo(new[] { 6, 15 }));
            _clock.At(9);
            Assert.That(_book.Collect("order.seller.test.1", _bait), Is.EqualTo(SpecialOrderRefusal.RefundRequired));
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
            SpecialOrderRowAdapter.Restore(_book, JsonUtility.FromJson<SpecialOrderRows>(text));
            var row = _book.Capture().Orders.Single();
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
            Assert.That(_book.Capture().NextSpecialOrderNumber, Is.EqualTo(12));
            Assert.That(_wallet.Calls, Is.Zero);
            Assert.That(_placed, Is.Empty);
            Assert.That(_arrived, Is.Empty);
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
