using System;
using System.Collections.Generic;
using System.Reflection;
using HiddenHarbours.Core;
using HiddenHarbours.Economy;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.Economy
{
    public class PurchasePublisherIdentityTests
    {
        private readonly List<Object> _created = new List<Object>();
        private ISaveService _previousSave;

        [SetUp]
        public void SetUp()
        {
            _previousSave = GameServices.Save;
            GameServices.Save = null; // These transaction fixtures never reach a live save service.
        }

        private sealed class Wallet : IWallet
        {
            public int Money { get; private set; } = 100000;
            public void Add(int amount) => Money += amount;
            public bool TrySpend(int amount)
            {
                if (amount < 0 || amount > Money) return false;
                Money -= amount;
                return true;
            }
        }

        private T Asset<T>() where T : ScriptableObject
        {
            var asset = ScriptableObject.CreateInstance<T>();
            _created.Add(asset);
            return asset;
        }

        private T Vendor<T>(string sellerId) where T : Component
        {
            var go = new GameObject("IdentityTest");
            go.SetActive(false); // No provider-resolution Awake needed for the explicit transaction seam.
            _created.Add(go);
            var vendor = go.AddComponent<T>();
            typeof(T).GetField("_sellerId", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(vendor, sellerId);
            return vendor;
        }

        private static T Capture<T>(Func<bool> transact)
        {
            T result = default;
            int count = 0;
            void Receive(T message) { result = message; count++; }
            EventBus.Subscribe<T>(Receive);
            try { Assert.IsTrue(transact()); }
            finally { EventBus.Unsubscribe<T>(Receive); }
            Assert.AreEqual(1, count, "one canonical transaction signal");
            return result;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var obj in _created) if (obj != null) Object.DestroyImmediate(obj);
            _created.Clear();
            GameServices.Save = _previousSave;
        }

        [TestCase("Supply")]
        [TestCase("Bait")]
        [TestCase("Instrument")]
        [TestCase("Gear")]
        [TestCase("License")]
        [TestCase("Pot")]
        [TestCase("Boat")]
        [TestCase("Repair")]
        public void CatalogTransaction_PublishesThisTillIdentity(string kind)
        {
            const string sellerId = "seller.nmc_chandlery";
            var wallet = new Wallet();
            var save = new SaveData { ActiveHullId = "boat.test" };
            ICustomSignal signal;
            switch (kind)
            {
                case "Supply":
                    var supply = Asset<SupplyDef>();
                    signal = Capture<SupplyPurchased>(() => Vendor<SupplyShop>(sellerId).TryBuy(supply, wallet, save));
                    break;
                case "Bait":
                    var bait = Asset<BaitDef>();
                    bait.LotSize = 7;
                    signal = Capture<BaitPurchased>(() => Vendor<BaitShop>(sellerId).TryBuy(bait, wallet, save));
                    Assert.AreEqual(7m, signal.CustomUnits);
                    break;
                case "Instrument":
                    var instrument = Asset<InstrumentOffer>();
                    signal = Capture<InstrumentPurchased>(() => Vendor<InstrumentShop>(sellerId).TryBuy(instrument, wallet, save));
                    break;
                case "Gear":
                    var gear = Asset<GearOffer>();
                    // Multi-seller listings must use the vendor the player chose, never Sellers[0].
                    gear.Catalog = new CatalogListing { Listed = true, Sellers = new[] { "seller.leblancs", sellerId } };
                    signal = Capture<GearPurchased>(() => Vendor<GearShop>(sellerId).TryBuy(gear, wallet, save));
                    break;
                case "License":
                    var license = Asset<LicenseDef>();
                    signal = Capture<LicensePurchased>(() => Vendor<LicenseVendor>(sellerId).TryBuy(license, wallet, null));
                    break;
                case "Pot":
                    var pot = Asset<PotOffer>();
                    signal = Capture<PotPurchased>(() => Vendor<PotShop>(sellerId).TryBuy(pot, wallet, save));
                    break;
                case "Boat":
                    var boat = Asset<ShipwrightOffer>();
                    signal = Capture<BoatPurchased>(() => Vendor<Shipwright>(sellerId).TryBuy(boat, wallet));
                    break;
                default:
                    var repair = Asset<ShipwrightOffer>();
                    repair.StartsDamaged = true;
                    signal = Capture<BoatRepaired>(() => Vendor<Shipwright>(sellerId).TryRepair(repair, wallet, save));
                    break;
            }
            Assert.AreEqual("npc.claudette_boudreau", signal.Counterparty.MerchantId);
            Assert.AreEqual(sellerId, signal.Counterparty.VendorId);
            Assert.AreEqual("trade.chandlery", signal.Counterparty.TradeId);
        }

        [Test]
        public void UnassignedDirectVendor_PublishesUnknown()
        {
            var offer = Asset<GearOffer>();
            var signal = Capture<GearPurchased>(() => Vendor<GearShop>("").TryBuy(offer, new Wallet(), new SaveData()));
            Assert.AreEqual("unknown", signal.MerchantId);
            Assert.AreEqual("unknown", signal.VendorId);
        }

        private sealed class Can : IFuelVessel
        {
            public string Grade => FuelGrades.Gas;
            public float CapacityLitres => 10f;
            public float Litres { get; private set; }
            public void Deliver(float litres) => Litres += litres;
            public float Draw(float litres) => 0f;
        }

        [TestCase("npc.test")]
        [TestCase("")]
        public void Pump_PublishesAuthoredStationIdentity(string keeper)
        {
            var station = Asset<FuelStationDef>();
            station.Id = "station.test";
            station.KeeperNpcId = keeper;
            station.TradeId = "trade.fuel";
            station.Grades = new[] { new FuelGradeOffer { Grade = FuelGrades.Gas, Available = true, PricePerLitre = 2f } };
            var go = new GameObject("PumpIdentityTest");
            go.SetActive(false);
            _created.Add(go);
            var pump = go.AddComponent<FuelPump>();
            pump.Configure(station, FuelGrades.Gas);
            var signal = Capture<FuelPurchased>(() => pump.TryFill(new Can(), new Wallet(), 2.5f));
            Assert.AreEqual(string.IsNullOrEmpty(keeper) ? "unknown" : keeper, signal.MerchantId);
            Assert.AreEqual("station.test", signal.VendorId);
            Assert.AreEqual("trade.fuel", signal.TradeId);
            Assert.AreEqual(2.5m, signal.CustomUnits);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void HomeTill_PublishesIdentity_AndSeparatesStarterGrant(bool starter)
        {
            var home = Asset<HomeDef>();
            home.Id = "home.custom_identity_test";
            home.KeeperNpcId = "npc.test";
            home.TradeId = "trade.housing";
            home.Acquisition = starter ? HomeDef.Mode.StarterHome : HomeDef.Mode.Purchasable;
            home.Price = 12;
            var go = new GameObject("HomeIdentityTest");
            go.SetActive(false);
            _created.Add(go);
            var door = go.AddComponent<HomeDoor>();
            door.Configure(home, false);
            var wallet = new Wallet();
            typeof(HomeDoor).GetField("_wallet", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(door, wallet);
            var signal = Capture<HomePurchased>(() => door.Interact() == HomeDoor.Outcome.Bought);
            Assert.AreEqual("npc.test", signal.MerchantId);
            Assert.AreEqual(home.Id, signal.VendorId);
            Assert.AreEqual("trade.housing", signal.TradeId);
            Assert.AreEqual(starter ? 0m : 1m, signal.CustomUnits);
            Assert.AreEqual(!starter, signal.IsPurchase);
            Assert.AreEqual(starter ? 100000 : 99988, wallet.Money);
        }
    }
}
