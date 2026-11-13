using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Economy;
using HiddenHarbours.Fishing;

namespace HiddenHarbours.Tests.Economy
{
    /// <summary>
    /// The hand-shellfish licence and the four catalog rows that go with it, read from the shipped assets.
    ///
    /// <para><b>The licence decision this pins.</b> There is one licence for everything taken by hand off
    /// the St Peters shore: <c>license.clam</c>, widened to permit the blue mussel and the American oyster
    /// beside the soft-shell clam. It is the licence already on Marguerite's counter and the one Aunt
    /// Ginny fronts, so the mussel and the oyster open on day one without a second fee and without a
    /// second vendor. The sea scallop is deliberately in no licence: it waits for the trawl arc.</para>
    ///
    /// <para><b>The rows.</b> A shellfish rake, oyster tongs, a mussel sack and a peck basket exist as
    /// <see cref="GearOffer"/> assets with their prices and capacities in data. They are authored
    /// UNLISTED: the hand-gathering that uses them is a later change, and a listed ware needs a committed
    /// vendor in the scene (StoreWiringTests guard 4). The store can already sell each one at its data
    /// price the moment it is listed, which is what <see cref="TheStore_SellsEachTool_AtItsDataPrice"/>
    /// proves.</para>
    ///
    /// <para>No balance number is repeated here. Every expectation is read from the Defs or from
    /// <see cref="GameConfig"/>, so the owner can retune a price, a capacity or a market level without
    /// this file being edited.</para>
    /// </summary>
    public class ShellfishLicenceAndToolsTests
    {
        private const string CatalogDir = "Assets/_Project/Data/Resources/Catalog/";
        private const string FishDir = "Assets/_Project/Data/Fish/";
        private const string ConfigPath = "Assets/_Project/Data/Config/GameConfig.asset";

        private const string ShoreLicenceId = "license.clam";
        private const string CodLicenceId = "license.cod";
        private const string StoreSellerId = "seller.leblancs";

        private const string ClamId = "fish.soft_shell_clam";
        private const string MusselId = "fish.blue_mussel";
        private const string OysterId = "fish.american_oyster";
        private const string ScallopId = "fish.sea_scallop";
        private const string CodId = "fish.atlantic_cod";

        private static readonly string[] HandGathered = { ClamId, MusselId, OysterId };

        /// <summary>Asset file name → the stable id it must carry. Ids are append-only.</summary>
        private static readonly (string File, string Id)[] ToolRows =
        {
            ("ShellfishRake", "gear.shellfish_rake"),
            ("OysterTongs", "gear.oyster_tongs"),
            ("MusselSack", "gear.mussel_sack"),
            ("ShellfishBasket", "gear.shellfish_basket"),
        };

        private static readonly string[] Containers = { "gear.mussel_sack", "gear.shellfish_basket" };

        // ---- fakes for the Core contracts ---------------------------------------------------------

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

        private sealed class FakeHold : IHold
        {
            private readonly List<CatchItem> _items = new();
            public int CapacityUnits => int.MaxValue;
            public int UsedUnits => _items.Count;
            public IReadOnlyList<CatchItem> Items => _items;
            public bool TryAdd(CatchItem item) { _items.Add(item); return true; }
            public void Clear() => _items.Clear();
        }

        private sealed class FakeLicenses : ILicenseService
        {
            private readonly HashSet<string> _held = new();
            public bool IsLicensed(string id) => string.IsNullOrEmpty(id) || _held.Contains(id);
            public void Grant(string id) { if (!string.IsNullOrEmpty(id)) _held.Add(id); }
            public int Count => _held.Count;
        }

        private readonly List<Object> _spawned = new();

        [SetUp]
        public void SetUp()
        {
            EventBus.Clear<CatchSold>();
            EventBus.Clear<GearPurchased>();
        }

        [TearDown]
        public void TearDown()
        {
            EventBus.Clear<CatchSold>();
            EventBus.Clear<GearPurchased>();
            GameServices.Reset();
            foreach (var o in _spawned) if (o != null) Object.DestroyImmediate(o);
            _spawned.Clear();
        }

        // ---- loaders ------------------------------------------------------------------------------

        private static List<LicenseDef> ShippedLicences()
        {
            var list = new List<LicenseDef>();
            foreach (string guid in AssetDatabase.FindAssets("t:" + nameof(LicenseDef), new[] { "Assets/_Project/Data" }))
            {
                var def = AssetDatabase.LoadAssetAtPath<LicenseDef>(AssetDatabase.GUIDToAssetPath(guid));
                if (def != null) list.Add(def);
            }
            Assert.IsNotEmpty(list, "the shipped licences must load, or every gate below passes on nothing");
            return list;
        }

        private static LicenseDef Licence(string id)
        {
            foreach (var l in ShippedLicences())
                if (l.Id == id) return l;
            Assert.Fail("no shipped LicenseDef carries the id " + id);
            return null;
        }

        private static GearOffer Tool(string file)
        {
            var offer = AssetDatabase.LoadAssetAtPath<GearOffer>(CatalogDir + "Gear/" + file + ".asset");
            Assert.IsNotNull(offer, file + ".asset must exist in the catalog's Gear folder");
            return offer;
        }

        private static FishSpeciesDef Fish(string file)
        {
            var def = AssetDatabase.LoadAssetAtPath<FishSpeciesDef>(FishDir + file + ".asset");
            Assert.IsNotNull(def, file + ".asset must exist in Data/Fish");
            return def;
        }

        private static GameConfig Config()
        {
            var config = AssetDatabase.LoadAssetAtPath<GameConfig>(ConfigPath);
            Assert.IsNotNull(config, "the shared GameConfig asset exists");
            return config;
        }

        private Market MakeMarket(MarketId id)
        {
            var go = new GameObject(id + "Market");
            _spawned.Add(go);
            var market = go.AddComponent<Market>();
            market.Configure(Config(), id);
            return market;
        }

        private FishBuyer MakeBuyer(Market market)
        {
            var buyer = market.gameObject.AddComponent<FishBuyer>();
            typeof(FishBuyer).GetField("_market", System.Reflection.BindingFlags.NonPublic |
                                                  System.Reflection.BindingFlags.Instance)
                             .SetValue(buyer, market);
            return buyer;
        }

        private static CatchItem ItemOf(FishSpeciesDef fish) =>
            new CatchItem(fish.Id, fish.DisplayName, fish.Category, fish.MinWeightKg,
                          fish.BaseValue, fish.SupplyElasticity);

        // =====================================================================================
        //  1. THE LICENCE GATE
        // =====================================================================================

        [Test]
        public void TheShoreLicence_PermitsTheClam_TheMussel_AndTheOyster()
        {
            LicenseDef shore = Licence(ShoreLicenceId);
            foreach (string species in HandGathered)
                CollectionAssert.Contains(shore.PermittedSpeciesIds, species,
                    $"{ShoreLicenceId} is the one hand-shellfish licence and must permit {species}");

            CollectionAssert.DoesNotContain(shore.PermittedSpeciesIds, ScallopId,
                "the scallop waits for the trawl arc and is in no hand-gathering licence");
            CollectionAssert.DoesNotContain(shore.PermittedSpeciesIds, CodId,
                "the shore licence must not quietly unlock cod: that licence is the earned one");
        }

        [Test]
        public void WithoutTheLicence_NoHandGatheredShellfishMayBeLanded()
        {
            List<LicenseDef> all = ShippedLicences();
            var empty = new FakeLicenses();

            foreach (string species in HandGathered)
            {
                Assert.AreEqual(ShoreLicenceId, CatchLicensePolicy.RequiredLicenseFor(species, all),
                    $"{species} must be gated by {ShoreLicenceId}");
                Assert.IsFalse(CatchLicensePolicy.MayLand(species, all, empty),
                    $"{species} must not be landable with an empty licence wallet");
                Assert.IsFalse(CatchLicensePolicy.MayLand(species, all, null),
                    $"{species} must not be landable before a wallet exists (the gate fails closed)");
            }
        }

        [Test]
        public void BuyingTheLicenceAtItsDataPrice_OpensAllThree_AndNothingElse()
        {
            List<LicenseDef> all = ShippedLicences();
            LicenseDef shore = Licence(ShoreLicenceId);
            var licences = new FakeLicenses();
            var wallet = new FakeWallet(shore.Price);

            var go = new GameObject("StoreCounter");
            _spawned.Add(go);
            var vendor = go.AddComponent<LicenseVendor>();

            Assert.IsTrue(vendor.TryBuy(shore, wallet, licences), "the fee in the Def is enough to buy it");
            Assert.AreEqual(0, wallet.Money, "the vendor charges exactly the Def's price");

            foreach (string species in HandGathered)
                Assert.IsTrue(CatchLicensePolicy.MayLand(species, all, licences),
                    $"holding {ShoreLicenceId} must open {species}");

            Assert.AreEqual(CodLicenceId, CatchLicensePolicy.RequiredLicenseFor(CodId, all));
            Assert.IsFalse(CatchLicensePolicy.MayLand(CodId, all, licences),
                "the shore licence must not open cod");
        }

        [Test]
        public void ADayOneWallet_AffordsTheLicence_SoTheWidenedGateCannotSoftLock()
        {
            // The mussel and the oyster now sit behind the same fail-closed gate as the clam. Ginny's
            // fronted fee is what guarantees the player can pass it.
            Assert.GreaterOrEqual(Config().FrontedLicenceFee, Licence(ShoreLicenceId).Price,
                "the fee Ginny fronts must cover the licence that gates all hand-gathered shellfish");
        }

        [Test]
        public void EveryGatedSpecies_IsGatedByExactlyOneLicence()
        {
            // CatchLicensePolicy returns the first licence that lists a species. Two licences listing the
            // same one would make the gate depend on asset load order.
            var owner = new Dictionary<string, string>();
            foreach (LicenseDef l in ShippedLicences())
            {
                if (l.PermittedSpeciesIds == null) continue;
                foreach (string species in l.PermittedSpeciesIds)
                {
                    Assert.IsFalse(owner.TryGetValue(species, out string first),
                        $"{species} is listed by both {first} and {l.Id}");
                    owner[species] = l.Id;
                }
            }
        }

        [Test]
        public void TheScallop_IsInNoLicence_AndNoScallopGearIsInTheCatalog()
        {
            Assert.IsNull(CatchLicensePolicy.RequiredLicenseFor(ScallopId, ShippedLicences()),
                "no scallop licence ships before the trawl arc");

            foreach (string guid in AssetDatabase.FindAssets("t:" + nameof(GearOffer), new[] { "Assets/_Project/Data" }))
            {
                var offer = AssetDatabase.LoadAssetAtPath<GearOffer>(AssetDatabase.GUIDToAssetPath(guid));
                if (offer == null || string.IsNullOrEmpty(offer.Id)) continue;
                StringAssert.DoesNotContain("scallop", offer.Id, "no scallop gear ships before the trawl arc");
            }
        }

        // =====================================================================================
        //  2. THE TOOLS AND CONTAINERS
        // =====================================================================================

        [Test]
        public void EachRow_Resolves_WithItsStableId_AName_AndAPriceFromData()
        {
            var seen = new HashSet<string>();
            foreach ((string file, string id) in ToolRows)
            {
                GearOffer offer = Tool(file);
                Assert.AreEqual(id, offer.Id, file + ": ids are stable and append-only");
                Assert.IsTrue(seen.Add(offer.Id), "duplicate id " + offer.Id);
                Assert.IsFalse(string.IsNullOrWhiteSpace(offer.DisplayName), id + " needs a player-facing name");
                Assert.IsFalse(string.IsNullOrWhiteSpace(offer.Flavor), id + " needs a line of flavour");
                Assert.Greater(offer.Price, 0, id + " must cost something: a free tool is an authoring slip");
            }
        }

        [Test]
        public void TheSackAndTheBasket_CarryACapacity_AndTheHandToolsDoNot()
        {
            foreach ((string file, string id) in ToolRows)
            {
                GearOffer offer = Tool(file);
                if (System.Array.IndexOf(Containers, id) >= 0)
                    Assert.Greater(offer.CarryCapacityUnits, 0, id + " is a container and must say what it holds");
                else
                    Assert.AreEqual(0, offer.CarryCapacityUnits, id + " is a hand tool, not a container");
            }
        }

        [Test]
        public void TheStore_SellsEachTool_AtItsDataPrice()
        {
            var go = new GameObject("StoreCounter");
            _spawned.Add(go);
            var shop = go.AddComponent<GearShop>();

            foreach ((string file, string id) in ToolRows)
            {
                GearOffer offer = Tool(file);

                var shortSave = new SaveData();
                var shortWallet = new FakeWallet(offer.Price - 1);
                Assert.IsFalse(shop.TryBuy(offer, shortWallet, shortSave), id + ": one coin short must not buy it");
                Assert.AreEqual(offer.Price - 1, shortWallet.Money, id + ": a refused sale takes nothing");

                var save = new SaveData();
                var wallet = new FakeWallet(offer.Price);
                Assert.IsTrue(shop.TryBuy(offer, wallet, save), id + ": the Def's price buys it");
                Assert.AreEqual(0, wallet.Money, id + ": the shop charges exactly the Def's price");
                CollectionAssert.Contains(save.OwnedGear, id, id + ": ownership is recorded by stable id");

                Assert.IsFalse(shop.TryBuy(offer, new FakeWallet(offer.Price), save), id + ": owned once, not sold twice");
            }
        }

        /// <summary>
        /// The four rows are authored unlisted on purpose. Nothing in the game can use a rake, tongs, a
        /// sack or a basket until hand-gathering is data, and a listed ware with no committed vendor fails
        /// StoreWiringTests. Each row already names its eventual seller, so listing it later is one tick
        /// on the asset plus the store patch.
        /// </summary>
        [Test]
        public void TheRows_AreNotOnAnyCounterYet_ButAlreadyNameTheStore()
        {
            foreach ((string file, string id) in ToolRows)
            {
                GearOffer offer = Tool(file);
                Assert.IsFalse(offer.Catalog.IsListed, id + " must stay unlisted until the player can use it");
                Assert.IsFalse(offer.Catalog.IsStockedBy(StoreSellerId), id + " must not be stocked by any seller yet");
                CollectionAssert.Contains(offer.Catalog.Sellers, StoreSellerId,
                    id + " is the island store's ware once it is listed");
                Assert.AreEqual(CatalogSection.Gear, offer.Catalog.Section, id + " sits on the everyday shelf");
            }

            foreach (GearOffer stocked in CatalogSource.For<GearOffer>(StoreSellerId))
                foreach ((string _, string id) in ToolRows)
                    Assert.AreNotEqual(id, stocked.Id, id + " must not appear in the store's wares book yet");
        }

        // =====================================================================================
        //  3. THE COUNTERS BUY THE SPECIES
        // =====================================================================================

        [Test]
        public void TheCounters_QuoteTheMusselAndTheOyster_FromTheirDefs()
        {
            Market store = MakeMarket(MarketId.StPetersStore);
            Market creek = MakeMarket(MarketId.NineMileCreek);

            foreach (FishSpeciesDef fish in new[] { Fish("BlueMussel"), Fish("AmericanOyster") })
            {
                Assert.IsTrue(fish.IsShellfish, fish.Id + " sells in the shellfish category");

                int atStore = store.NextUnitPrice(fish.Category, fish.BaseValue, fish.SupplyElasticity);
                int atCreek = creek.NextUnitPrice(fish.Category, fish.BaseValue, fish.SupplyElasticity);

                Assert.AreEqual(SellPricing.UnitPrice(fish.BaseValue, fish.SupplyElasticity, 0f,
                                                      store.DemandFor(fish.Category), 1f, store.PriceLevel),
                                atStore, fish.Id + ": the store's quote is the price formula over the Def's own numbers");
                Assert.GreaterOrEqual(atStore, 1, fish.Id + " must be worth at least a coin at the island counter");
                Assert.LessOrEqual(atStore, atCreek, fish.Id + ": the island counter never out-pays the Creek");
            }

            FishSpeciesDef mussel = Fish("BlueMussel"), oyster = Fish("AmericanOyster");
            Assert.Greater(creek.NextUnitPrice(oyster.Category, oyster.BaseValue, oyster.SupplyElasticity),
                           creek.NextUnitPrice(mussel.Category, mussel.BaseValue, mussel.SupplyElasticity),
                "the oyster is the slower, dearer pick; the mussel is the cheap, reliable one");
        }

        [Test]
        public void ASackOfMussels_AndABasketOfOysters_SellOverTheCounter_ForTheQuotedCoin()
        {
            GearOffer sack = Tool("MusselSack"), basket = Tool("ShellfishBasket");

            foreach ((FishSpeciesDef fish, int count) in new[]
                     {
                         (Fish("BlueMussel"), sack.CarryCapacityUnits),
                         (Fish("AmericanOyster"), basket.CarryCapacityUnits),
                     })
            {
                Market store = MakeMarket(MarketId.StPetersStore);
                var hold = new FakeHold();
                for (int i = 0; i < count; i++) hold.TryAdd(ItemOf(fish));

                int expected = SellPricing.RunningTotal(fish.BaseValue, fish.SupplyElasticity, 0f, count,
                                                        store.DemandFor(fish.Category), store.PriceLevel);
                var wallet = new FakeWallet(0);
                int paid = MakeBuyer(store).SellAll(hold, wallet);

                Assert.AreEqual(expected, paid, fish.Id + ": the till pays the sum of the per-unit marginals");
                Assert.AreEqual(paid, wallet.Money, fish.Id + ": and the coin reaches the wallet");
                Assert.Greater(paid, 0, fish.Id + ": a full container is worth something");
                Assert.AreEqual(0, hold.UsedUnits, fish.Id + ": the counter takes the whole container");
            }
        }
    }
}
