using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Economy;
using HiddenHarbours.Vehicles;

namespace HiddenHarbours.Tests.Economy
{
    /// <summary>
    /// <b>BUYING A VEHICLE</b>: the offer data (<see cref="VehicleOffer"/>), the deed store
    /// (<see cref="VehicleDeeds"/>) and the pure purchase (<see cref="VehiclePurchase"/>). Pure logic
    /// over the Core contracts plus a read of the shipped offer assets; no scene, runs headless.
    /// </summary>
    public class VehicleOfferTests
    {
        const string DealerSeller = "seller.nmc_vehicle_dealer";
        const string YardSeller = "seller.nmc_industrial_yard";
        const string OfferIdPrefix = "vehicleoffer.";
        const string DataRoot = "Assets/_Project/Data";

        private sealed class FakeWallet : IWallet
        {
            public FakeWallet(int starting) { Money = starting; }
            public int Money { get; private set; }
            public void Add(int amount) => Money += amount;
            public bool TrySpend(int amount)
            {
                if (amount < 0 || amount > Money) return false;
                Money -= amount;
                return true;
            }
        }

        /// <summary>Flags written into <see cref="SaveData.OnboardingFlags"/>, the store the real
        /// <c>SaveService</c> uses, so a JSON round trip of <see cref="Current"/> carries them.</summary>
        private sealed class DataSaveService : ISaveService
        {
            public DataSaveService(SaveData data) { Current = data; }
            public SaveData Current { get; }
            public bool GetFlag(string key)
            {
                if (string.IsNullOrEmpty(key) || Current.OnboardingFlags == null) return false;
                foreach (SaveFlag f in Current.OnboardingFlags) if (f.Key == key) return f.Value;
                return false;
            }
            public void SetFlag(string key, bool value)
            {
                if (string.IsNullOrEmpty(key)) return;
                Current.OnboardingFlags ??= new List<SaveFlag>();
                Current.OnboardingFlags.RemoveAll(f => f.Key == key);
                Current.OnboardingFlags.Add(new SaveFlag(key, value));
            }
            public void Save() { }
        }

        private readonly List<Object> _spawned = new();
        private readonly List<VehiclePurchased> _bought = new();
        private readonly List<VehicleRepaired> _repaired = new();
        private void OnBought(VehiclePurchased e) => _bought.Add(e);
        private void OnRepaired(VehicleRepaired e) => _repaired.Add(e);
        private DataSaveService _save;

        [SetUp]
        public void SetUp()
        {
            _bought.Clear();
            _repaired.Clear();
            EventBus.Clear<VehiclePurchased>();
            EventBus.Clear<VehicleRepaired>();
            EventBus.Subscribe<VehiclePurchased>(OnBought);
            EventBus.Subscribe<VehicleRepaired>(OnRepaired);
            _save = new DataSaveService(SaveMigration.NewGame());
        }

        [TearDown]
        public void TearDown()
        {
            EventBus.Unsubscribe<VehiclePurchased>(OnBought);
            EventBus.Unsubscribe<VehicleRepaired>(OnRepaired);
            EventBus.Clear<VehiclePurchased>();
            EventBus.Clear<VehicleRepaired>();
            foreach (var o in _spawned) if (o != null) Object.DestroyImmediate(o);
            _spawned.Clear();
        }

        private VehicleOffer MakeOffer(int price, bool damaged = false, int repair = 0,
                                       string id = "vehicleoffer.test_pickup")
        {
            var offer = ScriptableObject.CreateInstance<VehicleOffer>();
            offer.Id = id;
            offer.VehicleId = "vehicle.modern_3500";
            offer.DisplayName = "Test pickup";
            offer.SellerId = DealerSeller;
            offer.Price = price;
            offer.StartsDamaged = damaged;
            offer.RepairCost = repair;
            _spawned.Add(offer);
            return offer;
        }

        private static List<(string Path, VehicleOffer Offer)> ShippedOffers()
        {
            var found = new List<(string, VehicleOffer)>();
            foreach (string guid in AssetDatabase.FindAssets("t:" + nameof(VehicleOffer), new[] { DataRoot }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var offer = AssetDatabase.LoadAssetAtPath<VehicleOffer>(path);
                if (offer != null) found.Add((path, offer));
            }
            return found;
        }

        // =================================================================================
        //  THE SHIPPED OFFERS
        // =================================================================================

        [Test]
        public void ShippedOffers_Exist_AndLiveInTheCatalogFolder()
        {
            var offers = ShippedOffers();
            Assert.IsNotEmpty(offers, "no VehicleOffer assets found; every rule below would pass vacuously");
            foreach (var (path, _) in offers)
                StringAssert.StartsWith(CatalogSource.CatalogFolderPath + "/", path,
                    "a listing outside the Resources catalog folder is invisible to every book");
        }

        [Test]
        public void ShippedOffers_HaveUniquePrefixedIds()
        {
            var seen = new Dictionary<string, string>();
            foreach (var (path, offer) in ShippedOffers())
            {
                StringAssert.StartsWith(OfferIdPrefix, offer.Id, path);
                Assert.IsFalse(seen.ContainsKey(offer.Id), $"{offer.Id} at {path} repeats {(seen.TryGetValue(offer.Id, out var p) ? p : "")}");
                seen.Add(offer.Id, path);
            }
        }

        [Test]
        public void ShippedOffers_HavePositivePrices_AndDamagedOnesCarryARepairCost()
        {
            int damaged = 0;
            foreach (var (path, offer) in ShippedOffers())
            {
                Assert.Greater(offer.Price, 0, path);
                if (!offer.StartsDamaged) continue;
                damaged++;
                Assert.Greater(offer.RepairCost, 0, $"{path} is sold damaged but costs nothing to repair");
            }
            Assert.GreaterOrEqual(damaged, 1, "the repair shop should stock at least one damaged used machine");
        }

        [Test]
        public void ShippedOffers_EachPointAtAnExistingVehicleDef()
        {
            var vehicleIds = new HashSet<string>();
            foreach (string guid in AssetDatabase.FindAssets("t:" + nameof(VehicleDef), new[] { DataRoot }))
            {
                var def = AssetDatabase.LoadAssetAtPath<VehicleDef>(AssetDatabase.GUIDToAssetPath(guid));
                if (def != null) vehicleIds.Add(def.Id);
            }
            Assert.IsNotEmpty(vehicleIds);
            foreach (var (path, offer) in ShippedOffers())
                Assert.IsTrue(vehicleIds.Contains(offer.VehicleId), $"{path} sells {offer.VehicleId}, which no VehicleDef has");
        }

        [Test]
        public void ShippedOffers_AreSoldByTheDealerOrTheYard_OnTheVehiclesShelf()
        {
            var offers = ShippedOffers();
            foreach (var (path, offer) in offers)
            {
                Assert.That(offer.SellerId, Is.EqualTo(DealerSeller).Or.EqualTo(YardSeller), path);
                Assert.AreEqual(CatalogSection.Vehicles, offer.Catalog.Section, path);
                Assert.IsTrue(offer.Catalog.Sellers != null && offer.Catalog.Sellers.Contains(offer.SellerId),
                    $"{path}: the catalog tag must name the same seller as SellerId");
            }
            Assert.IsTrue(offers.Any(o => o.Offer.SellerId == DealerSeller), "the dealer stocks nothing");
            Assert.IsTrue(offers.Any(o => o.Offer.SellerId == YardSeller), "the industrial yard stocks nothing");
        }

        // =================================================================================
        //  THE PURCHASE
        // =================================================================================

        [Test]
        public void Buying_WithFunds_SpendsThePrice_GrantsTheDeed_AndPublishesOnce()
        {
            var offer = MakeOffer(7000);
            var wallet = new FakeWallet(10000);

            Assert.AreEqual(VehiclePurchaseResult.Done, VehiclePurchase.TryBuy(offer, wallet, _save));
            Assert.AreEqual(3000, wallet.Money);
            Assert.IsTrue(VehicleDeeds.IsOwned(_save, offer.Id));
            Assert.IsTrue(VehicleDeeds.IsUsable(_save, offer.Id), "a sound machine runs the moment it is bought");
            Assert.AreEqual(1, _bought.Count);
            Assert.AreEqual(offer.Id, _bought[0].OfferId);
            Assert.AreEqual(offer.VehicleId, _bought[0].VehicleId);
            Assert.AreEqual(DealerSeller, _bought[0].SellerId);
            Assert.AreEqual(7000, _bought[0].PricePaid);
        }

        [Test]
        public void Buying_WithoutFunds_ChargesNothing_RecordsNothing_PublishesNothing()
        {
            var offer = MakeOffer(7000);
            var wallet = new FakeWallet(6999);

            Assert.AreEqual(VehiclePurchaseResult.CannotAfford, VehiclePurchase.TryBuy(offer, wallet, _save));
            Assert.AreEqual(6999, wallet.Money);
            Assert.IsFalse(VehicleDeeds.IsOwned(_save, offer.Id));
            Assert.IsEmpty(_bought);
        }

        [Test]
        public void BuyingTwice_IsRefused_AndChargesOnce()
        {
            var offer = MakeOffer(1000);
            var wallet = new FakeWallet(5000);
            VehiclePurchase.TryBuy(offer, wallet, _save);

            Assert.AreEqual(VehiclePurchaseResult.AlreadyOwned, VehiclePurchase.TryBuy(offer, wallet, _save));
            Assert.AreEqual(4000, wallet.Money);
            Assert.AreEqual(1, _bought.Count);
        }

        [Test]
        public void Buying_WithNoSaveToRecordTheDeed_IsRefusedBeforeTheWalletIsTouched()
        {
            var wallet = new FakeWallet(5000);
            Assert.AreEqual(VehiclePurchaseResult.Invalid, VehiclePurchase.TryBuy(MakeOffer(1000), wallet, null));
            Assert.AreEqual(VehiclePurchaseResult.Invalid, VehiclePurchase.TryBuy(MakeOffer(1000, id: ""), wallet, _save));
            Assert.AreEqual(VehiclePurchaseResult.Invalid, VehiclePurchase.TryBuy(null, wallet, _save));
            Assert.AreEqual(5000, wallet.Money);
            Assert.IsEmpty(_bought);
        }

        [Test]
        public void ADamagedMachine_IsOwnedButDoesNotRun_UntilItsRepairIsPaidFor()
        {
            var offer = MakeOffer(5000, damaged: true, repair: 4000);
            var wallet = new FakeWallet(12000);

            Assert.AreEqual(VehiclePurchaseResult.Done, VehiclePurchase.TryBuy(offer, wallet, _save));
            Assert.IsTrue(_bought[0].StartsDamaged);
            Assert.IsTrue(VehicleDeeds.IsOwned(_save, offer.Id));
            Assert.IsFalse(VehicleDeeds.IsUsable(_save, offer.Id), "a damaged machine must not run before repair");

            Assert.AreEqual(VehiclePurchaseResult.Done, VehiclePurchase.TryRepair(offer, wallet, _save));
            Assert.AreEqual(3000, wallet.Money);
            Assert.IsTrue(VehicleDeeds.IsUsable(_save, offer.Id));
            Assert.AreEqual(1, _repaired.Count);
            Assert.AreEqual(4000, _repaired[0].PricePaid);

            Assert.AreEqual(VehiclePurchaseResult.NothingToRepair, VehiclePurchase.TryRepair(offer, wallet, _save));
            Assert.AreEqual(3000, wallet.Money, "one paid repair, never two");
        }

        [Test]
        public void Repairing_IsRefusedWhenNotOwned_OrUnaffordable()
        {
            var offer = MakeOffer(5000, damaged: true, repair: 4000);
            var wallet = new FakeWallet(8000);
            Assert.AreEqual(VehiclePurchaseResult.NotOwned, VehiclePurchase.TryRepair(offer, wallet, _save));
            Assert.AreEqual(8000, wallet.Money);

            VehiclePurchase.TryBuy(offer, wallet, _save);
            Assert.AreEqual(VehiclePurchaseResult.CannotAfford, VehiclePurchase.TryRepair(offer, wallet, _save));
            Assert.AreEqual(3000, wallet.Money);
            Assert.IsFalse(VehicleDeeds.IsUsable(_save, offer.Id));
            Assert.IsEmpty(_repaired);
        }

        // =================================================================================
        //  THE DEED
        // =================================================================================

        [Test]
        public void ADeed_IsKeyedByTheOffer_InTheSamePrefixAsHomeDeeds()
        {
            Assert.AreEqual(HomeDeeds.KeyPrefix, VehicleDeeds.KeyPrefix);
            Assert.AreEqual("deed.vehicleoffer.x", VehicleDeeds.KeyFor("vehicleoffer.x"));
            Assert.AreEqual(string.Empty, VehicleDeeds.KeyFor(""));
            Assert.IsFalse(VehicleDeeds.IsOwned(_save, ""));
            Assert.IsFalse(VehicleDeeds.IsOwned((ISaveService)null, "vehicleoffer.x"));
        }

        [Test]
        public void ADeed_SurvivesASaveRoundTrip()
        {
            var sound = MakeOffer(1000, id: "vehicleoffer.test_sound");
            var broken = MakeOffer(500, damaged: true, repair: 300, id: "vehicleoffer.test_broken");
            var wallet = new FakeWallet(5000);
            VehiclePurchase.TryBuy(sound, wallet, _save);
            VehiclePurchase.TryBuy(broken, wallet, _save);

            SaveData loaded = SaveSerialization.FromJson(SaveSerialization.ToJson(_save.Current));
            Assert.IsNotNull(loaded);
            Assert.AreEqual(SaveMigration.CurrentVersion, loaded.SchemaVersion);
            var reloaded = new DataSaveService(loaded);

            Assert.IsTrue(VehicleDeeds.IsOwned(reloaded, sound.Id));
            Assert.IsTrue(VehicleDeeds.IsUsable(reloaded, sound.Id));
            Assert.IsTrue(VehicleDeeds.IsOwned(reloaded, broken.Id));
            Assert.IsFalse(VehicleDeeds.IsUsable(reloaded, broken.Id), "the damaged machine is still damaged after load");
            Assert.IsFalse(VehicleDeeds.IsOwned(reloaded, "vehicleoffer.never_bought"));
        }
    }
}
