using System;
using System.Collections.Generic;
using HiddenHarbours.Core;
using HiddenHarbours.Economy;
using NUnit.Framework;

namespace HiddenHarbours.Tests.EditMode.Economy
{
    public class ShopStockRulesTests
    {
        private const string Shop = "shop.test";
        private const string Line = "line.test";

        private sealed class Clock : IGameClock
        {
            public int Day;
            public double TotalSeconds => Day * 86400d;
            public GameTime Now => new GameTime(TotalSeconds);
            public Season Season => Season.EarlySpring;
            public int Year => 1;
            public int DayIndex => Day;
            public int DayOfSeason => Day + 1;
            public Weekday Weekday => (Weekday)(Day % 7);
            public bool IsMarketDay => false;
            public float HourOfDay => 0;
            public float DayFraction => 0;
            public bool IsPaused { get; set; }
            public float TimeScale { get; set; }
        }

        private static ShopStockSpec Spec(int maximum = 5, int floor = 0, int delivery = 2) => new ShopStockSpec
        {
            Id = Shop, SellerId = "seller.test", PremisesId = "premises.test",
            Shelves = new[] { new ShopShelfSlots { FixtureId = "fixture.test", SlotCount = maximum } },
            Stock = new[] { new ShopStockLine
            {
                Id = Line, ListingId = "gear.test", FixtureId = "fixture.test", FirstSlot = 0,
                MaxOnShelf = maximum, UnitsPerItem = 1, Floor = floor, ItemsPerDelivery = delivery,
                DeliveryWeekdays = ShopDeliveryDays.Monday | ShopDeliveryDays.Thursday
            } }
        };

        private static ShopStockService Service(ShopStockSpec spec, Clock clock, MemoryShopStockStateStore store = null,
            uint seed = 17, Action<ShopStockChanged> notify = null) =>
            new ShopStockService(new[] { spec }, seed, clock, store ?? new MemoryShopStockStateStore(), notify ?? (_ => { }));

        [Test]
        public void TakingTheLastItemSellsOut_AndPuttingItBackRestoresOne()
        {
            var stock = Service(Spec(1), new Clock());
            Assert.That(stock.TryTakeOne(Shop, Line), Is.True);
            Assert.That(stock.CountOf(Shop, Line), Is.Zero);
            Assert.That(stock.IsSoldOut(Shop, Line), Is.True);
            Assert.That(stock.TryPutBackOne(Shop, Line), Is.True);
            Assert.That(stock.CountOf(Shop, Line), Is.EqualTo(1));
            Assert.That(stock.IsSoldOut(Shop, Line), Is.False);
        }

        [Test]
        public void EmptyAndFullShelvesRefuseChangesWithoutCrossingTheirBounds()
        {
            var stock = Service(Spec(2), new Clock());
            Assert.That(stock.TryPutBackOne(Shop, Line), Is.False);
            Assert.That(stock.TryTakeOne(Shop, Line), Is.True);
            Assert.That(stock.TryTakeOne(Shop, Line), Is.True);
            for (int i = 0; i < 10; i++) Assert.That(stock.TryTakeOne(Shop, Line), Is.False);
            Assert.That(stock.CountOf(Shop, Line), Is.Zero);
            Assert.That(stock.TryPutBackOne(Shop, Line), Is.True);
            Assert.That(stock.TryPutBackOne(Shop, Line), Is.True);
            Assert.That(stock.TryPutBackOne(Shop, Line), Is.False);
            Assert.That(stock.CountOf(Shop, Line), Is.EqualTo(2));
        }

        [TestCase("bait.capelin")]
        [TestCase("supply.ice")]
        public void EssentialsRemainTakeableAtTheirAuthoredFloor(string listingId)
        {
            var spec = Spec(3, 1); spec.Stock[0].ListingId = listingId;
            var stock = Service(spec, new Clock());
            for (int i = 0; i < 30; i++) Assert.That(stock.TryTakeOne(Shop, Line), Is.True);
            Assert.That(stock.CountOf(Shop, Line), Is.EqualTo(1));
            Assert.That(stock.IsSoldOut(Shop, Line), Is.False);
        }

        [Test]
        public void DeliveryArrivesOnThursday_AndRepeatedSlowTicksDoNotRepeatIt()
        {
            var clock = new Clock(); var changes = new List<ShopStockChanged>();
            var stock = Service(Spec(), clock, notify: changes.Add);
            for (int i = 0; i < 5; i++) stock.TryTakeOne(Shop, Line);
            clock.Day = 2; stock.SlowTick();
            Assert.That(stock.CountOf(Shop, Line), Is.Zero);
            changes.Clear(); clock.Day = 3; stock.SlowTick();
            Assert.That(stock.CountOf(Shop, Line), Is.EqualTo(2));
            Assert.That(changes.Count, Is.EqualTo(1));
            Assert.That(changes[0].Reason, Is.EqualTo(ShopStockChange.Restocked));
            Assert.That(changes[0].ShopId, Is.EqualTo(Shop));
            Assert.That(changes[0].LineId, Is.EqualTo(Line));
            Assert.That(changes[0].Count, Is.EqualTo(2));
            stock.TryTakeOne(Shop, Line);
            changes.Clear();
            for (int i = 0; i < 10; i++) stock.SlowTick();
            Assert.That(stock.CountOf(Shop, Line), Is.EqualTo(1));
            Assert.That(changes, Is.Empty);
            clock.Day = 7; stock.SlowTick();
            Assert.That(stock.CountOf(Shop, Line), Is.EqualTo(3));
        }

        [Test]
        public void ReadingBeforeTheSlowTickSettlesOnlyOnce()
        {
            var clock = new Clock(); var stock = Service(Spec(), clock);
            for (int i = 0; i < 5; i++) stock.TryTakeOne(Shop, Line);
            clock.Day = 3;
            Assert.That(stock.CountOf(Shop, Line), Is.EqualTo(2));
            stock.TryTakeOne(Shop, Line); stock.SlowTick();
            Assert.That(stock.CountOf(Shop, Line), Is.EqualTo(1));
        }

        [Test]
        public void TheSameSeedTimeAndPurchasesGiveTheSameStock()
        {
            var firstClock = new Clock(); var secondClock = new Clock();
            var first = Service(Spec(), firstClock, seed: 9841);
            var second = Service(Spec(), secondClock, seed: 9841);
            for (int day = 0; day < 28; day++)
            {
                firstClock.Day = secondClock.Day = day;
                first.SlowTick(); second.SlowTick(); second.SlowTick();
                Assert.That(first.TryTakeOne(Shop, Line), Is.EqualTo(second.TryTakeOne(Shop, Line)));
                Assert.That(first.CountOf(Shop, Line), Is.EqualTo(second.CountOf(Shop, Line)));
            }
        }

        [Test]
        public void TenDaysAtOnceEqualTenDailySettles()
        {
            var line = Spec(100).Stock[0];
            int daily = 0;
            for (int day = 1; day <= 10; day++) daily = ShopRestock.Settle(line, daily, day - 1, day, 42);
            Assert.That(daily, Is.EqualTo(6));
            Assert.That(ShopRestock.Settle(line, 0, 0, 10, 42), Is.EqualTo(daily));
        }

        [Test]
        public void EveryWeekdayScheduleMatchesAnEnumeratedCalendarAcrossWeekBoundaries()
        {
            for (int mask = 1; mask < 128; mask++)
                for (int start = 0; start < 14; start++)
                    for (int length = 0; length <= 30; length++)
                    {
                        int expected = 0;
                        for (int day = start + 1; day <= start + length; day++)
                            if ((mask & (1 << (day % 7))) != 0) expected++;
                        Assert.That(ShopRestock.DeliveriesBetween((ShopDeliveryDays)mask, start, start + length),
                            Is.EqualTo(expected), $"mask {mask}, start {start}, length {length}");
                    }
        }

        [Test]
        public void ALongAbsenceClampsWithoutOverflowOrWalkingEachDay()
        {
            var line = Spec(int.MaxValue, delivery: int.MaxValue).Stock[0];
            line.DeliveryWeekdays = (ShopDeliveryDays)127;
            Assert.That(ShopRestock.DeliveriesBetween(line.DeliveryWeekdays, 0, int.MaxValue), Is.EqualTo((long)int.MaxValue));
            Assert.That(ShopRestock.Settle(line, 0, 0, int.MaxValue, 1), Is.EqualTo(int.MaxValue));
        }

        [Test]
        public void FullShelvesSaveNothing_AndAFirstSaleUsesTodaysWatermark()
        {
            var clock = new Clock(); var state = new MemoryShopStockStateStore();
            var stock = Service(Spec(), clock, state);
            var records = new List<ShopStockRecord>(); state.CopyTo(records);
            Assert.That(records, Is.Empty);
            clock.Day = 7; stock.TryTakeOne(Shop, Line);
            Assert.That(state.TryRead(Shop, Line, out var record), Is.True);
            Assert.That(record.Count, Is.EqualTo(4));
            Assert.That(record.SettledThroughDay, Is.EqualTo(7));
            stock.SlowTick();
            Assert.That(stock.CountOf(Shop, Line), Is.EqualTo(4));
            stock.TryPutBackOne(Shop, Line);
            Assert.That(state.TryRead(Shop, Line, out _), Is.False);
        }

        [Test]
        public void SparseRecordsRestoreSoldOutStock_AndCatchUpMissedDeliveries()
        {
            var state = new MemoryShopStockStateStore();
            state.Write(new ShopStockRecord(Shop, Line, 0, 0));
            var clock = new Clock { Day = 2 };
            Assert.That(Service(Spec(), clock, state).IsSoldOut(Shop, Line), Is.True);
            Assert.That(state.TryRead(Shop, Line, out var record), Is.True);
            Assert.That(record.SettledThroughDay, Is.EqualTo(2));
            clock.Day = 10;
            Assert.That(Service(Spec(), clock, state).CountOf(Shop, Line), Is.EqualTo(5));
            Assert.That(state.TryRead(Shop, Line, out _), Is.False);
        }

        [Test]
        public void RestoringEssentialsRepairsTheirFloorEvenWithoutADelivery()
        {
            var state = new MemoryShopStockStateStore(); state.Write(new ShopStockRecord(Shop, Line, 0, 1));
            Assert.That(Service(Spec(5, 1), new Clock { Day = 1 }, state).CountOf(Shop, Line), Is.EqualTo(1));
        }

        [Test]
        public void RewindingTimeOrLoadingAFutureWatermarkIsRejected()
        {
            var clock = new Clock { Day = 2 }; var stock = Service(Spec(), clock);
            clock.Day = 1;
            Assert.Throws<InvalidOperationException>(() => stock.SlowTick());
            var state = new MemoryShopStockStateStore(); state.Write(new ShopStockRecord(Shop, Line, 0, 3));
            Assert.Throws<ArgumentException>(() => Service(Spec(), clock, state));
            Assert.Throws<ArgumentException>(() => state.Write(new ShopStockRecord(Shop, Line, -1, 0)));
            Assert.Throws<ArgumentException>(() => state.Write(new ShopStockRecord(Shop, Line, 0, -1)));
        }

        [Test]
        public void SameLineIdInDifferentShopsHasIndependentCounts()
        {
            var first = Spec(); var second = Spec(); second.Id = "shop.other";
            var stock = new ShopStockService(new[] { first, second }, 1, new Clock(), new MemoryShopStockStateStore(), _ => { });
            stock.TryTakeOne(Shop, Line);
            Assert.That(stock.CountOf(Shop, Line), Is.EqualTo(4));
            Assert.That(stock.CountOf(second.Id, Line), Is.EqualTo(5));
            Assert.Throws<KeyNotFoundException>(() => stock.CountOf("shop.unknown", Line));
            Assert.Throws<KeyNotFoundException>(() => stock.CountOf(Shop, "line.unknown"));
        }

        [Test]
        public void AuthoredSpecMutationsCannotChangeARunningService()
        {
            var spec = Spec(); var stock = Service(spec, new Clock());
            spec.Stock[0].MaxOnShelf = 1; spec.Stock[0].Floor = 1; spec.Id = "shop.changed";
            stock.TryTakeOne(Shop, Line);
            Assert.That(stock.CountOf(Shop, Line), Is.EqualTo(4));
        }

        [Test]
        public void WarmSlowTicksAllocateNoMemoryForSettlementOrRepeatedPolling()
        {
            var clock = new Clock(); var stock = Service(Spec(1000, delivery: 1), clock);
            for (int i = 0; i < 1000; i++) stock.TryTakeOne(Shop, Line);
            clock.Day = 3; stock.SlowTick();
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int day = 4; day <= 30; day++)
            {
                clock.Day = day; stock.SlowTick(); stock.SlowTick();
            }
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.That(allocated, Is.Zero);
            Assert.That(stock.CountOf(Shop, Line), Is.EqualTo(8));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void RepeatedLineOrListingIdentitiesAreRejectedEvenOnDistinctFixtures(bool repeatLine)
        {
            var spec = Spec(); var second = spec.Stock[0].Snapshot();
            second.FixtureId = "fixture.other";
            if (repeatLine) second.ListingId = "gear.other";
            else second.Id = "line.other";
            spec.Shelves = new[] { spec.Shelves[0], new ShopShelfSlots { FixtureId = second.FixtureId, SlotCount = 5 } };
            spec.Stock = new[] { spec.Stock[0], second };
            Assert.Throws<ArgumentException>(() => spec.Validate());
        }

        [Test]
        public void InvalidListingOrSellerIsRejectedByContentValidation()
        {
            var spec = Spec();
            Assert.DoesNotThrow(() => spec.Validate((seller, item) => seller == "seller.test" && item == "gear.test"));
            spec.Stock[0].ListingId = "gear.missing";
            Assert.Throws<ArgumentException>(() => spec.Validate((seller, item) => item == "gear.test"));
            spec.Stock[0].ListingId = "gear.test"; spec.SellerId = "seller.other";
            Assert.Throws<ArgumentException>(() => spec.Validate((seller, item) => seller == "seller.test"));
        }

        [Test]
        public void DuplicateSlotsOrUnknownFixturesAreRejected()
        {
            var spec = Spec(); var extra = spec.Stock[0].Snapshot();
            extra.Id = "line.other"; extra.ListingId = "gear.other";
            spec.Stock = new[] { spec.Stock[0], extra };
            Assert.Throws<ArgumentException>(() => spec.Validate());
            extra.FixtureId = "fixture.missing";
            Assert.Throws<ArgumentException>(() => spec.Validate());
        }

        [TestCase("capacity")]
        [TestCase("floor")]
        [TestCase("units")]
        [TestCase("days")]
        [TestCase("delivery")]
        [TestCase("line_id")]
        [TestCase("shop_id")]
        [TestCase("credit")]
        public void InvalidAuthoredStockFailsClosed(string field)
        {
            var spec = Spec(); var line = spec.Stock[0];
            switch (field)
            {
                case "capacity": line.FirstSlot = int.MaxValue; break;
                case "floor": line.Floor = 6; break;
                case "units": line.UnitsPerItem = 0; break;
                case "days": line.DeliveryWeekdays = (ShopDeliveryDays)128; break;
                case "delivery": line.ItemsPerDelivery = 0; break;
                case "line_id": line.Id = "line.BAD"; break;
                case "shop_id": spec.Id = "shop.BAD"; break;
                case "credit": spec.Credit.Enabled = true; break;
            }
            Assert.Throws<ArgumentException>(() => spec.Validate());
        }
    }
}
