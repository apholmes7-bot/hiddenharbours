using System.Collections.Generic;
using HiddenHarbours.Core;
using NUnit.Framework;

namespace HiddenHarbours.Tests.Economy
{
    public class PurchaseSignalTests
    {
        private static readonly TradeCounterparty Party = new TradeCounterparty("npc.keeper", "seller.counter", "trade.test");

        private static IEnumerable<TestCaseData> Identified()
        {
            yield return new TestCaseData(new SupplyPurchased("supply.test", 8, 99, Party), 1m).SetName("Supply_identity_and_units");
            yield return new TestCaseData(new BaitPurchased("bait.test", 8, 10, 99, Party), 10m).SetName("Bait_identity_and_lot_units");
            yield return new TestCaseData(new InstrumentPurchased("instrument.test", "boat.test", 8, Party), 1m).SetName("Instrument_identity_and_units");
            yield return new TestCaseData(new FuelPurchased("station.test", "gas", 2.5f, 8, Party), 2.5m).SetName("Fuel_identity_and_litres");
            yield return new TestCaseData(new LicensePurchased("license.test", 8, Party), 1m).SetName("License_identity_and_units");
            yield return new TestCaseData(new GearPurchased("gear.test", 8, Party), 1m).SetName("Gear_identity_and_units");
            yield return new TestCaseData(new PotPurchased("trap.test", 8, 99, Party), 1m).SetName("Pot_identity_and_units");
            yield return new TestCaseData(new BoatPurchased("boat.test", 8, Party), 1m).SetName("Boat_identity_and_units");
            yield return new TestCaseData(new BoatRepaired("boat.test", 8, Party), 1m).SetName("Repair_identity_and_units");
            yield return new TestCaseData(new CatchSold(8, 12, Party), 12m).SetName("Catch_identity_and_sold_units");
            yield return new TestCaseData(new HomePurchased("home.test", 8, Party), 1m).SetName("Home_identity_and_units");
        }

        [TestCaseSource(nameof(Identified))]
        public void CompletedTransaction_CarriesIdentityAndActualUnits(ICustomSignal signal, decimal units)
        {
            Assert.AreEqual("npc.keeper", signal.Counterparty.MerchantId);
            Assert.AreEqual("seller.counter", signal.Counterparty.VendorId);
            Assert.AreEqual("trade.test", signal.Counterparty.TradeId);
            Assert.AreEqual(units, signal.CustomUnits);
        }

        private static IEnumerable<ICustomSignal> Legacy()
        {
            yield return new SupplyPurchased("s", 8, 99);
            yield return new BaitPurchased("b", 8, 10, 99);
            yield return new InstrumentPurchased("i", "h", 8);
            yield return new FuelPurchased("f", "gas", 2.5f, 8);
            yield return new LicensePurchased("l", 8);
            yield return new GearPurchased("g", 8);
            yield return new PotPurchased("p", 8, 99);
            yield return new BoatPurchased("h", 8);
            yield return new BoatRepaired("h", 8);
            yield return new CatchSold(8, 12);
            yield return new HomePurchased("h", 8);
        }

        [TestCaseSource(nameof(Legacy))]
        public void OldConstructor_RemainsUnknown(ICustomSignal signal)
        {
            Assert.AreEqual("unknown", signal.Counterparty.MerchantId);
            Assert.AreEqual("unknown", signal.Counterparty.VendorId);
            Assert.AreEqual("unknown", signal.Counterparty.TradeId);
        }

        [Test]
        public void ExistingFields_RemainUnchanged()
        {
            var bait = new BaitPurchased("bait.test", 17, 10, 42, Party);
            Assert.AreEqual("bait.test", bait.BaitId);
            Assert.AreEqual(17, bait.PricePaid);
            Assert.AreEqual(10, bait.CountBought);
            Assert.AreEqual(42, bait.OwnedCount);
            var fuel = new FuelPurchased("station.test", "gas", 2.5f, 17, Party);
            Assert.AreEqual("station.test", fuel.StationId);
            Assert.AreEqual("gas", fuel.Grade);
            Assert.AreEqual(2.5f, fuel.Litres);
            Assert.AreEqual(17, fuel.PricePaid);
            var sale = new CatchSold(17, 12, Party);
            Assert.AreEqual(17, sale.TotalPaid);
            Assert.AreEqual(12, sale.Count);
        }

        [Test]
        public void DefaultAndEmptyIdentity_AreUnknown()
        {
            Assert.AreEqual("unknown", default(GearPurchased).MerchantId);
            Assert.AreEqual("unknown", default(CatchSold).VendorId);
            var party = new TradeCounterparty(null, "", " ");
            Assert.AreEqual("unknown", party.MerchantId);
            Assert.AreEqual("unknown", party.VendorId);
            Assert.AreEqual("unknown", party.TradeId);
        }

        [Test]
        public void SummaryCarriesIdentity_ButCannotDoubleCountSale()
        {
            var report = new CounterSellReported("seller.counter", 17, 12, Party);
            Assert.AreEqual("npc.keeper", report.MerchantId);
            Assert.AreEqual("seller.counter", report.VendorId);
            Assert.AreEqual("trade.test", report.TradeId);
            Assert.IsTrue(report.SoldAnything);
            Assert.IsFalse(typeof(ICustomSignal).IsAssignableFrom(typeof(CounterSellReported)));
            Assert.AreEqual("unknown", new CounterSellReported("seller.counter", 17, 12).MerchantId);
        }

        [Test]
        public void StarterHomeGrant_IsNotCustom()
        {
            Assert.AreEqual(0m, new HomePurchased("home.test", 0, Party, isPurchase: false).CustomUnits);
            Assert.AreEqual(1m, new HomePurchased("home.test", 0, Party).CustomUnits, "a free sale is still a sale");
        }
    }
}
