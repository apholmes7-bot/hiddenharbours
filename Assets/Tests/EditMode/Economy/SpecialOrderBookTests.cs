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
        private SaveData _save;
        private TestWallet _wallet;
        private TestClock _clock;
        private SpecialOrderBook _book;
        private List<BaitStock> _bait;

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
            TestWallet wallet = null, TestClock clock = null, SaveData save = null)
        {
            var book = new SpecialOrderBook(save ?? SaveMigration.NewGame(),
                new[] { new SpecialOrderLine(Line, Item, Seller, price, wait, limit) },
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
            _book.Capture().Orders[0].Lines[0].Count = 900;
            var captured = restored.Capture();
            captured.Orders[1].State = SpecialOrderState.ArrivedState;
            captured.Orders[0].Lines[0].PaidPrice = 900;
            Assert.That(Json(restored), Is.EqualTo(json));
            restored.RefreshArrivals();
            Assert.That(_arrived.Count, Is.EqualTo(1));
            Assert.That(restored.Place(Seller, Line, 1, out string id), Is.EqualTo(SpecialOrderRefusal.None));
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
            restored.Dispose();
            restored = Book(save: save);
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
            var loaded = SaveSerialization.FromJson(SaveSerialization.ToJson(_save));
            var restored = Book(price: 100, wait: 8, limit: 1, save: loaded);
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
            using var a = new SpecialOrderBook(SaveMigration.NewGame(), new[] { line, other }, new TestWallet(), _clock);
            using var b = new SpecialOrderBook(SaveMigration.NewGame(), new[] { other, line }, new TestWallet(), _clock);
            _clock.At(123, 17.5);
            foreach (var book in new[] { a, b })
            {
                Assert.That(book.Place(Seller, Line, 4, out _), Is.EqualTo(SpecialOrderRefusal.None));
                Assert.That(book.Place(Seller, other.Id, 2, out _), Is.EqualTo(SpecialOrderRefusal.None));
            }
            Assert.That(Json(a), Is.EqualTo(Json(b)));
            Assert.That(a.Capture().Orders.Select(row => row.OrderedDay), Is.EqualTo(new[] { 123, 123 }));
            Assert.That(a.Capture().Orders.Select(row => row.DueDay), Is.EqualTo(new[] { 125, 127 }));
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
            _save.NextSpecialOrderNumber = int.MaxValue;
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
            SpecialOrderSnapshot observed = null;
            Action<SpecialOrderCollected> reenter = _ =>
            {
                refusals.Add(_book.Collect(id, _bait));
                observed = _book.Capture();
                observed.NextSpecialOrderNumber = 1;
                observed.Orders = Array.Empty<SpecialOrderState>();
            };
            EventBus.Subscribe(reenter);
            try { Assert.That(_book.Collect(id, _bait), Is.EqualTo(SpecialOrderRefusal.None)); }
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
            Assert.Throws<ObjectDisposedException>(() => _book.Place(Seller, Line, 1, out _));
            using var other = new SpecialOrderBook(SaveMigration.NewGame(),
                new[] { new SpecialOrderLine(Line, Item, Seller, 3, 2, 20) }, _wallet, _clock);
            Assert.That(other.Place("seller.other", Line, 1, out _), Is.EqualTo(SpecialOrderRefusal.UnknownLine));
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
            var loaded = SaveSerialization.FromJson(SaveSerialization.ToJson(save));
            loaded.NextSpecialOrderNumber = 1;
            using var resumed = new SpecialOrderBook(loaded,
                new[] { new SpecialOrderLine(otherLine, "bait.other", otherSeller, 5, 1, 10) }, _wallet, _clock);
            Assert.That(resumed.Place(otherSeller, otherLine, 1, out string third), Is.EqualTo(SpecialOrderRefusal.None));
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
            Assert.That(book.Place(fault == "seller" ? "seller.other" : Seller,
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
            }, _wallet, _clock);
            Assert.That(book.Place(Seller, new[]
            {
                new SpecialOrderRequest(Line, 1), new SpecialOrderRequest(repeated ? Line : "specialorder.other", 1)
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
        public void DuplicateIds_KeepBothPayments_BlockBothRows_AndSurviveJson()
        {
            Place(2); Place(3); Place(1);
            var save = SaveSerialization.FromJson(SaveSerialization.ToJson(_save));
            save.SpecialOrders[1].OrderId = save.SpecialOrders[0].OrderId;
            var restored = Book(save: save);
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
            var save = SaveSerialization.FromJson(SaveSerialization.ToJson(_save));
            save.NextSpecialOrderNumber = supplied;
            var restored = Book(save: save);
            Assert.That(restored.Capture().Orders.Length, Is.EqualTo(3));
            Assert.That(restored.Capture().NextSpecialOrderNumber, Is.EqualTo(expected));
            restored.Dispose();
            restored = Book(save: save);
            Assert.That(restored.Capture().NextSpecialOrderNumber, Is.EqualTo(expected));
            Assert.That(_wallet.Calls, Is.EqualTo(3));
        }

        [Test]
        public void EmptyOrMissingOrderLists_DoNotResetUsedNumbersOrReplacePaidRows()
        {
            Place();
            string before = Json(_book);
            var view = _book.Capture();
            view.Orders = null;
            view.NextSpecialOrderNumber = 1;
            Assert.That(Json(_book), Is.EqualTo(before));
            Assert.That(_save.SpecialOrders.Single().PaidPrice, Is.EqualTo(3));
            var empty = SaveMigration.NewGame();
            empty.NextSpecialOrderNumber = 2;
            empty.SpecialOrders = null;
            var fresh = Book(save: empty);
            Assert.That(fresh.Capture().Orders, Is.Empty);
            Assert.That(fresh.Capture().NextSpecialOrderNumber, Is.EqualTo(2));
            empty.NextSpecialOrderNumber = -3;
            fresh.Dispose();
            fresh = Book(save: empty);
            Assert.That(fresh.Capture().NextSpecialOrderNumber, Is.EqualTo(1));
            Assert.Throws<ArgumentNullException>(() => new SpecialOrderBook(null,
                Array.Empty<SpecialOrderLine>(), _wallet, _clock));
        }

        [TestCase("order.seller.test.2147483647")]
        [TestCase("order.seller.test.2147483646")]
        public void HighestRestoredNumbers_ExhaustWithoutOverflowOrReuse(string id)
        {
            Place();
            _save.SpecialOrders[0].OrderId = id;
            _book.Dispose();
            _book = Book(save: _save);
            Assert.That(_book.Capture().NextSpecialOrderNumber, Is.EqualTo(int.MaxValue));
            Assert.That(_book.Capture().Orders.Single().OrderId, Is.EqualTo(id));
            Assert.That(_book.Place(Seller, Line, 1, out _), Is.EqualTo(SpecialOrderRefusal.OrderNumbersExhausted));
            Assert.That(_wallet.Calls, Is.EqualTo(1));
        }

        [Test]
        public void BookToSaveJsonToBook_IsEqualAcrossSellersAndMultipleLines()
        {
            _book.Dispose();
            var book = MultiLineBook(_save);
            _clock.At(4, 13.75);
            Assert.That(book.Place(Seller, new[]
            {
                new SpecialOrderRequest(Line, 2), new SpecialOrderRequest("specialorder.other", 3)
            }, out _), Is.EqualTo(SpecialOrderRefusal.None));
            _clock.At(8);
            book.RefreshArrivals();
            Assert.That(book.Place(Seller, Line, 1, out _), Is.EqualTo(SpecialOrderRefusal.None));
            Assert.That(book.Place("seller.other", "specialorder.foreign", 2, out _), Is.EqualTo(SpecialOrderRefusal.None));
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
            Assert.That(restored.Capture().NextSpecialOrderNumber, Is.EqualTo(10));
            Assert.That(restored.Capture().Orders.Select(row => row.State), Is.EqualTo(new[] { 1, 0, 0, 0 }));
            Assert.That(restored.Capture().Orders[2].OrderId, Is.EqualTo("order.seller.other.3"));
            Assert.That(restored.Capture().Orders[2].RequiresRefund, Is.False);
            Assert.That(restored.Capture().Orders[3].RefundAmount, Is.EqualTo(3));
            var exported = restored.Capture();
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
            var snapshot = _book.Capture();
            Assert.That(snapshot.Orders.Length, Is.EqualTo(2));
            Assert.That(snapshot.Orders.Select(row => row.RefundReasonKey),
                Is.EqualTo(new[] { "special_order.empty_id", "special_order.unknown_offer" }));
            Assert.That(snapshot.Orders.Select(row => row.RefundAmount), Is.EqualTo(new[] { 11, 0 }));
            Assert.That(rows.SpecialOrders.Count, Is.EqualTo(4));
            Assert.That(rows.SpecialOrders[1].State, Is.EqualTo(SpecialOrderDto.StateCollected));
            var exported = _book.Capture();
            Assert.That(exported.NextSpecialOrderNumber, Is.EqualTo(43));
            Assert.That(exported.Orders.Length, Is.EqualTo(2));
            Assert.That(exported.Orders[0].OrderId, Is.EqualTo(""));
            Assert.That(exported.Orders[0].PaidPrice, Is.EqualTo(11));
            Assert.That(exported.Orders[0].Lines, Is.Null);
            Assert.That(exported.Orders[1].PaidPrice, Is.Zero);
            Assert.That(exported.Orders[1].Lines.Select(line => line.PaidPrice), Is.EqualTo(new[] { 6, 15 }));
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
            _book.Dispose();
            _book = Book(save: SaveSerialization.FromJson("{\"SchemaVersion\":26," + text.Substring(1)));
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

        [TestCase(SpecialOrderDto.StateWaiting)]
        [TestCase(SpecialOrderDto.StateArrived)]
        [TestCase(SpecialOrderDto.StateCollected)]
        public void RepeatedListings_ProduceRowsUnchangedByJsonAndLoadRepair(int state)
        {
            _book.Dispose();
            _book = MultiLineBook(_save);
            var savedRows = _save.SpecialOrders;
            _clock.At(7, 13.75);
            Assert.That(_book.Place(Seller, new[]
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
            Assert.That(_book.Place("seller.other", "specialorder.foreign", 1, out _), Is.EqualTo(SpecialOrderRefusal.None));
            Assert.That(_book.Place(Seller, Line, 1, out _), Is.EqualTo(SpecialOrderRefusal.None));
            if (state != SpecialOrderDto.StateWaiting)
            {
                _clock.At(11);
                _book.RefreshArrivals();
            }
            if (state == SpecialOrderDto.StateCollected)
                Assert.That(_book.Collect(first, _save.BaitStock), Is.EqualTo(SpecialOrderRefusal.None));
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
            Assert.That(_book.Collect(id, _save.BaitStock), Is.EqualTo(SpecialOrderRefusal.None));
            Assert.That(_save.SpecialOrders.Single(), Is.SameAs(receipt));
            Assert.That(receipt.State, Is.EqualTo(SpecialOrderDto.StateCollected));
            Assert.That(receipt.PaidPrice, Is.EqualTo(9));
            Assert.That(receipt.Lines.Single().PaidPrice, Is.EqualTo(9));
            var loaded = SaveSerialization.FromJson(SaveSerialization.ToJson(_save));
            AssertSavedOrdersEqual(_save, loaded);
            var wallet = new TestWallet(loaded);
            var resumed = Book(save: loaded, wallet: wallet);
            Assert.That(resumed.Capture().Orders, Is.Empty);
            Assert.That(resumed.Collect(id, loaded.BaitStock), Is.EqualTo(SpecialOrderRefusal.UnknownOrder));
            resumed.RefreshArrivals();
            Assert.That(loaded.BaitStock, Is.EqualTo(new[] { new BaitStock(Item, 3) }));
            Assert.That(loaded.Money, Is.EqualTo(991));
            Assert.That(wallet.Calls, Is.Zero);
            Assert.That(_arrived.Count, Is.EqualTo(1));
            Assert.That(_collected.Count, Is.EqualTo(1));
            Assert.That(resumed.Place(Seller, Line, 1, out string next), Is.EqualTo(SpecialOrderRefusal.None));
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
            Assert.That(book.Capture().Orders, Is.Empty);
            Assert.That(loaded.SpecialOrders, Is.Empty);
            Assert.That(book.Capture().NextSpecialOrderNumber, Is.EqualTo(1));
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
            using var book = new SpecialOrderBook(loaded, offers, wallet, _clock);
            _clock.At(20);
            book.RefreshArrivals();
            Assert.That(book.Collect(id, loaded.BaitStock), Is.EqualTo(SpecialOrderRefusal.RefundRequired));
            var owed = book.Capture().Orders.Single();
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
            Assert.That(_book.Place(Seller, new[]
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
            Assert.That(book.Place(Seller, new[]
            {
                new SpecialOrderRequest(Line, int.MaxValue), new SpecialOrderRequest(Line, 1)
            }, out _), Is.EqualTo(SpecialOrderRefusal.LimitReached));
            Assert.That(_wallet.Calls, Is.Zero);
            Assert.That(book.Capture().Orders, Is.Empty);
            Assert.That(book.Capture().NextSpecialOrderNumber, Is.EqualTo(1));
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
            var row = book.Capture().Orders.Single();
            Assert.That(row.Lines.Select(line => line.ListingId), Is.EqualTo(new[] { Line, "specialorder.other" }));
            Assert.That(row.Lines.Select(line => line.Count), Is.EqualTo(new[] { 5, 1 }));
            Assert.That(row.Lines.Select(line => line.PaidPrice), Is.EqualTo(new[] { 60, 10 }));
            Assert.That(row.PaidPrice, Is.EqualTo(total));
            Assert.That(row.RequiresRefund, Is.EqualTo(refund));
            Assert.That(row.RefundAmount, Is.EqualTo(refund ? total : 0));
            Assert.That(row.RefundReasonKey, Is.EqualTo(refund ? "special_order.invalid_payment" : ""));
            Assert.That(book.Capture().NextSpecialOrderNumber, Is.EqualTo(10));
            Assert.That(SaveSerialization.ToJson(loaded), Is.EqualTo(before));
            AssertSavedOrdersEqual(loaded, SaveSerialization.FromJson(SaveSerialization.ToJson(loaded)));
            Assert.That(_wallet.Calls, Is.Zero);
            Assert.That(_placed, Is.Empty);
            Assert.That(_arrived, Is.Empty);
            Assert.That(_collected, Is.Empty);
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
