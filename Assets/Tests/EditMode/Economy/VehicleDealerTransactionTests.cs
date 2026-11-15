using System.Collections.Generic;
using HiddenHarbours.Core;
using HiddenHarbours.Economy;
using NUnit.Framework;

namespace HiddenHarbours.Tests.Economy
{
    /// <summary>Core contract tests: no Unity objects, scene, clock, or disk save.</summary>
    public class VehicleDealerTransactionTests
    {
        private sealed class Sale : IVehicleSale
        {
            public string Id { get; set; } = "vehicleoffer.test";
            public string VehicleId { get; set; } = "vehicle.test";
            public string SellerId { get; set; } = "seller.test";
            public int Price { get; set; } = 1200;
            public bool StartsDamaged { get; set; }
            public int RepairCost { get; set; } = 300;
        }

        private sealed class Wallet : IWallet
        {
            public int Money { get; private set; }
            public int SpendAttempts { get; private set; }
            public Wallet(int money) => Money = money;
            public void Add(int amount) => Money = checked(Money + amount);
            public bool TrySpend(int amount)
            {
                SpendAttempts++;
                if (amount < 0 || Money < amount) return false;
                Money -= amount;
                return true;
            }
        }

        private sealed class Save : ISaveService
        {
            private readonly Dictionary<string, bool> _flags = new Dictionary<string, bool>();
            public SaveData Current { get; } = new SaveData();
            public bool GetFlag(string key) => _flags.TryGetValue(key, out bool value) && value;
            public void SetFlag(string key, bool value) => _flags[key] = value;
            void ISaveService.Save() { }
        }

        private static readonly TradeCounterparty Party = new TradeCounterparty("npc.test", "seller.test", "trade.vehicles");
        private Sale _offer;
        private Wallet _buyer;
        private Wallet _till;
        private Save _save;
        private VehicleSellerAccount _seller;
        private readonly List<VehiclePurchased> _purchases = new List<VehiclePurchased>();
        private readonly List<VehicleRepaired> _repairs = new List<VehicleRepaired>();
        private void Bought(VehiclePurchased signal) => _purchases.Add(signal);
        private void Repaired(VehicleRepaired signal) => _repairs.Add(signal);

        [SetUp]
        public void SetUp()
        {
            _offer = new Sale();
            _buyer = new Wallet(2000);
            _till = new Wallet(100);
            _save = new Save();
            _seller = new VehicleSellerAccount(Party, _till);
            _purchases.Clear();
            _repairs.Clear();
            EventBus.Subscribe<VehiclePurchased>(Bought);
            EventBus.Subscribe<VehicleRepaired>(Repaired);
        }

        [TearDown]
        public void TearDown()
        {
            EventBus.Unsubscribe<VehiclePurchased>(Bought);
            EventBus.Unsubscribe<VehicleRepaired>(Repaired);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Purchase_ConservesMoney_GrantsBuyerOwnership_AndAttributesOneUnit(bool damaged)
        {
            _offer.StartsDamaged = damaged;
            var otherBuyer = new Save();
            Assert.AreEqual(VehiclePurchaseResult.Done, VehiclePurchase.TryBuy(_offer, _buyer, _save, _seller));
            Assert.AreEqual(800, _buyer.Money);
            Assert.AreEqual(1300, _till.Money);
            Assert.AreEqual(2100, _buyer.Money + _till.Money);
            Assert.IsTrue(VehicleDeeds.IsOwned(_save, _offer.Id));
            Assert.IsFalse(VehicleDeeds.IsOwned(otherBuyer, _offer.Id));
            Assert.AreEqual(!damaged, VehicleDeeds.IsUsable(_save, _offer.Id));
            Assert.AreEqual(1, _purchases.Count);
            var signal = _purchases[0];
            Assert.AreEqual(_offer.Id, signal.OfferId);
            Assert.AreEqual(_offer.VehicleId, signal.VehicleId);
            Assert.AreEqual(_offer.SellerId, signal.SellerId);
            Assert.AreEqual(1200, signal.PricePaid);
            Assert.AreEqual(damaged, signal.StartsDamaged);
            Assert.AreEqual(Party.MerchantId, signal.MerchantId);
            Assert.AreEqual(Party.VendorId, signal.VendorId);
            Assert.AreEqual(Party.TradeId, signal.TradeId);
            var ledger = new CustomLedger();
            ledger.Record(7, signal);
            Assert.AreEqual(1m, ledger.UnitsFor(7, Party.MerchantId));
            Assert.AreEqual(1m, ledger.ShareFor(7, Party.TradeId, Party.MerchantId));
            Assert.AreEqual(0m, ledger.UnitsFor(8, Party.MerchantId));
        }

        [Test]
        public void PurchaseCompletion_IsPublishedAfterBothBalancesAndDeed()
        {
            int notifications = 0;
            void Observe(VehiclePurchased signal)
            {
                Assert.AreEqual(800, _buyer.Money);
                Assert.AreEqual(1300, _till.Money);
                Assert.IsTrue(VehicleDeeds.IsUsable(_save, signal.OfferId));
                notifications++;
            }
            EventBus.Subscribe<VehiclePurchased>(Observe);
            try { Assert.AreEqual(VehiclePurchaseResult.Done, VehiclePurchase.TryBuy(_offer, _buyer, _save, _seller)); }
            finally { EventBus.Unsubscribe<VehiclePurchased>(Observe); }
            Assert.AreEqual(1, notifications);
        }

        [Test]
        public void RepeatedPurchase_DoesNotDebitCreditOrPublishTwice()
        {
            Assert.AreEqual(VehiclePurchaseResult.Done, VehiclePurchase.TryBuy(_offer, _buyer, _save, _seller));
            Assert.AreEqual(VehiclePurchaseResult.AlreadyOwned, VehiclePurchase.TryBuy(_offer, _buyer, _save, _seller));
            Assert.AreEqual(800, _buyer.Money);
            Assert.AreEqual(1300, _till.Money);
            Assert.AreEqual(1, _buyer.SpendAttempts);
            Assert.AreEqual(1, _purchases.Count);
        }

        [Test]
        public void InsufficientFunds_LeaveBothPartiesAndOwnershipUntouched()
        {
            _offer.Price = 2001;
            Assert.AreEqual(VehiclePurchaseResult.CannotAfford, VehiclePurchase.TryBuy(_offer, _buyer, _save, _seller));
            AssertUnchanged();
        }

        [TestCase("offer")]
        [TestCase("buyer")]
        [TestCase("save")]
        [TestCase("seller")]
        [TestCase("till")]
        [TestCase("same-wallet")]
        [TestCase("wrong-seller")]
        [TestCase("unknown-seller")]
        [TestCase("unknown-trade")]
        [TestCase("offer-id")]
        [TestCase("vehicle-id")]
        [TestCase("seller-id")]
        [TestCase("negative-price")]
        [TestCase("negative-repair")]
        [TestCase("negative-till")]
        [TestCase("overflow")]
        public void InvalidPurchase_IsRejectedBeforeAnySpend(string fault)
        {
            IVehicleSale offer = _offer;
            IWallet buyer = _buyer;
            ISaveService save = _save;
            IVehicleSeller seller = _seller;
            switch (fault)
            {
                case "offer": offer = null; break;
                case "buyer": buyer = null; break;
                case "save": save = null; break;
                case "seller": seller = null; break;
                case "till": seller = new VehicleSellerAccount(Party, null); break;
                case "same-wallet": seller = new VehicleSellerAccount(Party, _buyer); break;
                case "wrong-seller": seller = new VehicleSellerAccount(new TradeCounterparty("npc.test", "seller.other", "trade.vehicles"), _till); break;
                case "unknown-seller": seller = new VehicleSellerAccount(default, _till); break;
                case "unknown-trade": seller = new VehicleSellerAccount(new TradeCounterparty(null, _offer.SellerId, null), _till); break;
                case "offer-id": _offer.Id = " "; break;
                case "vehicle-id": _offer.VehicleId = " "; break;
                case "seller-id": _offer.SellerId = " "; break;
                case "negative-price": _offer.Price = -1; break;
                case "negative-repair": _offer.StartsDamaged = true; _offer.RepairCost = -1; break;
                case "negative-till": seller = new VehicleSellerAccount(Party, new Wallet(-1)); break;
                case "overflow": seller = new VehicleSellerAccount(Party, new Wallet(int.MaxValue - _offer.Price + 1)); break;
            }
            int? recipientBefore = seller?.Wallet?.Money;
            Assert.AreEqual(VehiclePurchaseResult.Invalid, VehiclePurchase.TryBuy(offer, buyer, save, seller));
            Assert.AreEqual(recipientBefore, seller?.Wallet?.Money);
            Assert.AreEqual(0, _buyer.SpendAttempts);
            AssertUnchanged();
        }

        [TestCase(0)]
        [TestCase(1200)]
        public void ExactSellerBalanceLimit_StillAcceptsPayment(int price)
        {
            _offer.Price = price;
            var till = new Wallet(int.MaxValue - price);
            var seller = new VehicleSellerAccount(Party, till);
            Assert.AreEqual(VehiclePurchaseResult.Done, VehiclePurchase.TryBuy(_offer, _buyer, _save, seller));
            Assert.AreEqual(int.MaxValue, till.Money);
            Assert.AreEqual(2000 - price, _buyer.Money);
            Assert.AreEqual(1m, _purchases[0].CustomUnits);
        }

        [Test]
        public void UnassignedKeeper_RetainsAuthoredSellerAndTrade()
        {
            var seller = new VehicleSellerAccount(new TradeCounterparty(null, _offer.SellerId, Party.TradeId), _till);
            Assert.AreEqual(VehiclePurchaseResult.Done, VehiclePurchase.TryBuy(_offer, _buyer, _save, seller));
            Assert.AreEqual(TradeCounterparty.UnknownId, _purchases[0].MerchantId);
            Assert.AreEqual(_offer.SellerId, _purchases[0].VendorId);
            Assert.AreEqual(Party.TradeId, _purchases[0].TradeId);
            Assert.AreEqual(1300, _till.Money);
        }

        [Test]
        public void Repair_PaysTheSellerOnce_AndMakesTheOwnedMachineUsable()
        {
            _offer.StartsDamaged = true;
            Assert.AreEqual(VehiclePurchaseResult.Done, VehiclePurchase.TryBuy(_offer, _buyer, _save, _seller));
            Assert.AreEqual(VehiclePurchaseResult.Done, VehiclePurchase.TryRepair(_offer, _buyer, _save, _seller));
            Assert.AreEqual(VehiclePurchaseResult.NothingToRepair, VehiclePurchase.TryRepair(_offer, _buyer, _save, _seller));
            Assert.AreEqual(500, _buyer.Money);
            Assert.AreEqual(1600, _till.Money);
            Assert.IsTrue(VehicleDeeds.IsUsable(_save, _offer.Id));
            Assert.AreEqual(1, _repairs.Count);
            Assert.AreEqual(300, _repairs[0].PricePaid);
            Assert.AreEqual(Party.VendorId, _repairs[0].VendorId);
            Assert.AreEqual(Party.MerchantId, _repairs[0].MerchantId);
            Assert.AreEqual(Party.TradeId, _repairs[0].TradeId);
            var ledger = new CustomLedger();
            ledger.Record(7, _purchases[0]);
            ledger.Record(7, _repairs[0]);
            Assert.AreEqual(2m, ledger.UnitsFor(7, Party.MerchantId));
        }

        [TestCase("unowned", VehiclePurchaseResult.NotOwned)]
        [TestCase("sound", VehiclePurchaseResult.NothingToRepair)]
        [TestCase("unaffordable", VehiclePurchaseResult.CannotAfford)]
        [TestCase("negative", VehiclePurchaseResult.Invalid)]
        [TestCase("overflow", VehiclePurchaseResult.Invalid)]
        [TestCase("seller", VehiclePurchaseResult.Invalid)]
        public void RefusedRepair_LeavesMoneyAndRepairStateUntouched(string fault, VehiclePurchaseResult expected)
        {
            _offer.StartsDamaged = true;
            if (fault != "unowned") VehicleDeeds.Grant(_save, _offer.Id);
            if (fault == "sound") VehicleDeeds.MarkRepaired(_save, _offer.Id);
            if (fault == "unaffordable") _offer.RepairCost = 2001;
            if (fault == "negative") _offer.RepairCost = -1;
            if (fault == "overflow") _till = new Wallet(int.MaxValue);
            _seller = new VehicleSellerAccount(Party, _till);
            int before = _till.Money;
            Assert.AreEqual(expected, VehiclePurchase.TryRepair(_offer, _buyer, _save, fault == "seller" ? null : _seller));
            Assert.AreEqual(2000, _buyer.Money);
            Assert.AreEqual(before, _till.Money);
            Assert.AreEqual(fault == "sound", VehicleDeeds.IsRepaired(_save, _offer.Id));
            Assert.IsEmpty(_repairs);
        }

        [Test]
        public void LegacySignals_PreserveKnownSeller_AndDoNotGuessKeeperOrTrade()
        {
            ICustomSignal bought = new VehiclePurchased("offer", "model", "seller.test", 1200, false);
            ICustomSignal repaired = new VehicleRepaired("offer", "model", "seller.test", 300);
            foreach (var signal in new[] { bought, repaired })
            {
                Assert.AreEqual("seller.test", signal.Counterparty.VendorId);
                Assert.AreEqual(TradeCounterparty.UnknownId, signal.Counterparty.MerchantId);
                Assert.AreEqual(TradeCounterparty.UnknownId, signal.Counterparty.TradeId);
                Assert.AreEqual(1m, signal.CustomUnits);
            }
            Assert.AreEqual(TradeCounterparty.UnknownId, default(VehiclePurchased).VendorId);
            Assert.AreEqual(TradeCounterparty.UnknownId, default(VehicleRepaired).VendorId);
        }

        private void AssertUnchanged()
        {
            Assert.AreEqual(2000, _buyer.Money);
            Assert.AreEqual(100, _till.Money);
            Assert.IsFalse(VehicleDeeds.IsOwned(_save, _offer.Id));
            Assert.IsFalse(VehicleDeeds.IsRepaired(_save, _offer.Id));
            Assert.IsEmpty(_purchases);
            Assert.IsEmpty(_repairs);
        }
    }
}
