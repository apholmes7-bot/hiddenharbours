using System;
using System.Collections.Generic;
using HiddenHarbours.Core;
using NUnit.Framework;

namespace HiddenHarbours.Tests.Economy
{
    public class CustomLedgerTests
    {
        private static TradeCounterparty Party(string merchant, string trade = "trade.test", string vendor = "seller.test")
            => new TradeCounterparty(merchant, vendor, trade);

        [Test]
        public void SharesAreByTrade_NotByMoneyOrOwnedTotal()
        {
            var ledger = new CustomLedger();
            ledger.Record(2, new BaitPurchased("bait.test", 999, 3, 500, Party("npc.a")));
            ledger.Record(2, new SupplyPurchased("supply.test", 0, 999, Party("npc.b")));
            ledger.Record(2, new CatchSold(1234, 8, Party("npc.c", "trade.other")));
            Assert.AreEqual(3m, ledger.UnitsFor(2, "npc.a"));
            Assert.AreEqual(1m, ledger.UnitsFor(2, "npc.b"));
            Assert.AreEqual(0.75m, ledger.ShareFor(2, "trade.test", "npc.a"));
            Assert.AreEqual(0.25m, ledger.ShareFor(2, "trade.test", "npc.b"));
            Assert.AreEqual(1m, ledger.ShareFor(2, "trade.other", "npc.c"));
            Assert.AreEqual(0m, ledger.ShareFor(2, "trade.test", "npc.unvisited"));
        }

        [Test]
        public void EveryPermutationWithinDay_HasIdenticalSortedTallyAndShares()
        {
            ICustomSignal[] signals = {
                new FuelPurchased("station.test", "gas", 0.1f, 1, Party("npc.b")),
                new FuelPurchased("station.test", "gas", 0.2f, 1, Party("npc.a")),
                new FuelPurchased("station.test", "gas", 0.3f, 1, Party("npc.b")),
                new CatchSold(5, 2, Party("npc.a", "trade.other")),
                new GearPurchased("gear.test", 7, Party("npc.a")),
            };
            var expected = new CustomLedger();
            foreach (var signal in signals) expected.Record(6, signal);
            int permutations = 0;
            Permute(signals, 0, sequence => {
                var actual = new CustomLedger();
                foreach (var signal in sequence) actual.Record(6, signal);
                CollectionAssert.AreEqual(expected.Day(6), actual.Day(6));
                permutations++;
            });
            Assert.AreEqual(120, permutations);
            var rows = expected.Day(6);
            Assert.AreEqual(3, rows.Count);
            Assert.AreEqual("trade.other", rows[0].TradeId);
            Assert.AreEqual("npc.a", rows[1].MerchantId);
            Assert.AreEqual("npc.b", rows[2].MerchantId);
            Assert.AreEqual(0.4m, rows[2].Units);
            Assert.AreEqual(0.25m, rows[2].Share);
        }

        private static void Permute(ICustomSignal[] values, int start, Action<ICustomSignal[]> check)
        {
            if (start == values.Length) { check(values); return; }
            for (int i = start; i < values.Length; i++)
            {
                (values[start], values[i]) = (values[i], values[start]);
                Permute(values, start + 1, check);
                (values[start], values[i]) = (values[i], values[start]);
            }
        }

        [Test]
        public void DaysDoNotBleedTogether_EvenIfRecordedOutOfOrder()
        {
            var ledger = new CustomLedger();
            ledger.Record(1, new CatchSold(5, 4, Party("npc.a")));
            ledger.Record(0, new CatchSold(5, 2, Party("npc.a")));
            ledger.Record(1, new CatchSold(5, 3, Party("npc.b")));
            Assert.AreEqual(2m, ledger.UnitsFor(0, "npc.a"));
            Assert.AreEqual(4m, ledger.UnitsFor(1, "npc.a"));
            Assert.AreEqual(0m, ledger.UnitsFor(2, "npc.a"));
            Assert.AreEqual(1m, ledger.ShareFor(0, "trade.test", "npc.a"));
            Assert.AreEqual(4m / 7m, ledger.ShareFor(1, "trade.test", "npc.a"));
        }

        [Test]
        public void MerchantWithSeveralCounters_IsOneMerchantPerTrade()
        {
            var ledger = new CustomLedger();
            ledger.Record(0, new GearPurchased("gear.test", 1, Party("npc.a", vendor: "seller.one")));
            ledger.Record(0, new GearPurchased("gear.test", 1, Party("npc.a", vendor: "seller.two")));
            ledger.Record(0, new GearPurchased("gear.test", 1, Party("npc.a", "trade.other")));
            Assert.AreEqual(3m, ledger.UnitsFor(0, "npc.a"));
            Assert.AreEqual(2, ledger.Day(0).Count);
            Assert.AreEqual(2m, ledger.Day(0)[1].Units);
        }

        [Test]
        public void MissingKeeperCountsUnderUnknown_WithinAuthoredTrade()
        {
            var ledger = new CustomLedger();
            Assert.DoesNotThrow(() => ledger.Record(0, new BaitPurchased("bait.test", 10, 3, 3, Party(null))));
            ledger.Record(0, new GearPurchased("gear.test", 1, Party("npc.a")));
            Assert.AreEqual(3m, ledger.UnitsFor(0, "unknown"));
            Assert.AreEqual(3m, ledger.UnitsFor(0, null));
            Assert.AreEqual(0.75m, ledger.ShareFor(0, "trade.test", "unknown"));
            Assert.AreEqual(0.25m, ledger.ShareFor(0, "trade.test", "npc.a"));
        }

        [Test]
        public void LegacyTransactionsCountTogetherWithoutThrowing()
        {
            var ledger = new CustomLedger();
            Assert.DoesNotThrow(() => {
                ledger.Record(0, new CatchSold(20, 2));
                ledger.Record(0, new BaitPurchased("bait.test", 10, 3, 500));
                ledger.Record(0, new FuelPurchased("station.test", "gas", 0.5f, 1));
            });
            Assert.AreEqual(5.5m, ledger.UnitsFor(0, "unknown"));
            Assert.AreEqual(1m, ledger.ShareFor(0, "unknown", "unknown"));
        }

        [Test]
        public void SnapshotIsDetached_AndForgetRetiresOnlySelectedDay()
        {
            var ledger = new CustomLedger();
            ledger.Record(0, new GearPurchased("gear.test", 1, Party("npc.a")));
            var snapshot = ledger.Day(0);
            ledger.Record(0, new GearPurchased("gear.test", 1, Party("npc.b")));
            ledger.Record(1, new GearPurchased("gear.test", 1, Party("npc.a")));
            Assert.AreEqual(1, snapshot.Count);
            Assert.AreEqual(1m, snapshot[0].Share);
            Assert.Throws<NotSupportedException>(() => ((IList<DailyCustom>)snapshot).Clear());
            ledger.ForgetDay(0);
            Assert.AreEqual(0, ledger.Day(0).Count);
            Assert.AreEqual(1m, ledger.UnitsFor(1, "npc.a"));
        }

        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        [TestCase(float.MaxValue)]
        [TestCase(-1f)]
        [TestCase(0f)]
        public void InvalidFuelQuantityContributesNothing(float litres)
        {
            var ledger = new CustomLedger();
            Assert.DoesNotThrow(() => ledger.Record(0, new FuelPurchased("station.test", "gas", litres, 1)));
            Assert.AreEqual(0, ledger.Day(0).Count);
        }

        [Test]
        public void EmptyNegativeAndGrantSignalsDoNotCreateRows()
        {
            var ledger = new CustomLedger();
            ledger.Record(0, null);
            ledger.Record(0, new CatchSold(0, 0));
            ledger.Record(0, new CatchSold(0, -1));
            ledger.Record(0, new BaitPurchased("bait.test", 0, -1, 4));
            ledger.Record(0, new HomePurchased("home.test", 0, Party("npc.a"), isPurchase: false));
            Assert.AreEqual(0, ledger.Day(0).Count);
        }
    }
}
