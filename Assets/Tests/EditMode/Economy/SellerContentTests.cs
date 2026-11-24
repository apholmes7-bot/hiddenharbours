using System.Collections.Generic;
using System.Linq;
using HiddenHarbours.Core;
using HiddenHarbours.Economy;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.Tests.Economy
{
    public class SellerContentTests
    {
        const string IslandSeller = "seller.leblancs";
        const string BoatGearSeller = "seller.nmc_boat_gear_wholesaler";
        const string SounderId = "instrument.depth_sounder";
        const string OutboardId = "boat.dory_outboard";

        static ICatalogListing[] Offers() => AssetDatabase.FindAssets("t:ScriptableObject",
                new[] { "Assets/_Project/Data" })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadMainAssetAtPath).OfType<ICatalogListing>().ToArray();

        [Test]
        public void EveryOffer_NamesRealSellers_AndEachSellerOffersAnItemOnce()
        {
            var sellers = Resources.LoadAll<SellerDef>(SellerDirectory.ResourceFolder).ToDictionary(s => s.Id);
            var pairs = new HashSet<string>();
            var offers = Offers();
            Assert.That(offers, Is.Not.Empty);
            foreach (var offer in offers)
            {
                // This unlisted, free identity supplies the starting kit's label, not a sale.
                if (offer.ListingId == "gear.headlamp")
                {
                    Assert.That(offer.Catalog.Listed, Is.False);
                    Assert.That(((GearOffer)offer).Price, Is.Zero);
                    Assert.That(offer.Catalog.Sellers, Is.Empty);
                    continue;
                }
                Assert.That(offer.Catalog.Sellers, Is.Not.Null.And.Not.Empty, offer.ListingId + " names no seller");
                string item = offer is VehicleOffer vehicle ? vehicle.VehicleId :
                    offer is PotOffer pot ? pot.TrapDefId : offer.ListingId;
                Assert.That(item, Is.Not.Null.And.Not.Empty, offer.ListingId + " names no item");
                foreach (string seller in offer.Catalog.Sellers)
                {
                    Assert.That(seller, Is.Not.Null.And.Not.Empty, offer.ListingId);
                    Assert.That(sellers.ContainsKey(seller), Is.True, offer.ListingId + " names missing seller " + seller);
                    Assert.That(pairs.Add(seller + "|" + item), Is.True,
                        "Duplicate offer for item " + item + " at " + seller);
                }
                if (offer is VehicleOffer v)
                    Assert.That(offer.Catalog.Sellers, Does.Contain(v.SellerId), v.Id + " purchase seller differs from catalog");
            }
        }

        [Test]
        public void IslandSellers_OfferNoBoatHardware_ExceptSounderBeforeItsMove()
        {
            var sellers = Resources.LoadAll<SellerDef>(SellerDirectory.ResourceFolder).ToDictionary(s => s.Id);
            var regions = new HashSet<string>(AssetDatabase.FindAssets("t:RegionDef", new[] { "Assets/_Project/Data" })
                .Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadMainAssetAtPath)
                .Select(a => new SerializedObject(a).FindProperty("Id").stringValue));
            foreach (var seller in sellers.Values)
                Assert.That(regions.Contains(seller.RegionId), Is.True, seller.Id + " needs a real region");
            Assert.That(sellers["seller.leblancs"].RegionId, Is.EqualTo("region.st_peters"));
            Assert.That(sellers[BoatGearSeller].RegionId, Is.EqualTo("region.nine_mile_creek"));
            AssertHardwareSellers(Offers(), sellers);
        }

        static void AssertHardwareSellers(ICatalogListing[] offers, Dictionary<string, SellerDef> sellers)
        {
            var sounder = offers.Single(o => o.ListingId == SounderId);
            // The counter relocation has exactly two supported states, never a second simultaneous seller.
            Assert.That(sounder.Catalog.Sellers,
                Is.EquivalentTo(new[] { IslandSeller }).Or.EquivalentTo(new[] { BoatGearSeller }),
                "The sounder must have its single existing seller or its single mainland replacement");
            bool beforeMove = sounder.Catalog.Sellers.Single() == IslandSeller;
            string[] expected = beforeMove ? new[] { IslandSeller + "|" + SounderId } : new string[0];
            var found = new HashSet<string>();
            foreach (var offer in offers)
            {
                if (offer is InstrumentOffer || offer.ListingId == OutboardId)
                    Assert.That(offer.Catalog.BoatHardware, Is.True, offer.ListingId + " boat fitting needs hardware classification");
                if (!offer.Catalog.BoatHardware) continue;
                foreach (string seller in offer.Catalog.Sellers ?? new string[0])
                {
                    Assert.That(sellers.ContainsKey(seller), Is.True, offer.ListingId + " names missing seller " + seller);
                    if (sellers[seller].RegionId != "region.st_peters") continue;
                    string key = seller + "|" + offer.ListingId;
                    Assert.That(expected, Does.Contain(key),
                        "Island seller " + seller + " offers boat hardware " + offer.ListingId + "; move the offer to a mainland seller");
                    found.Add(key);
                }
            }
            Assert.That(found, Is.EquivalentTo(expected),
                "Only the existing sounder pair is allowed before relocation; no island hardware is allowed afterward");
        }

        [TestCase(IslandSeller)]
        [TestCase(BoatGearSeller)]
        public void SounderRelocation_AcceptsEitherExactSingleSeller(string seller)
        {
            WithSounderSellers(new[] { seller }, (offers, sellers) => AssertHardwareSellers(offers, sellers));
        }

        static IEnumerable<TestCaseData> InvalidSounderSellers()
        {
            yield return new TestCaseData((object)new string[0]);
            yield return new TestCaseData((object)new[] { IslandSeller, BoatGearSeller });
            yield return new TestCaseData((object)new[] { IslandSeller, IslandSeller });
            yield return new TestCaseData((object)new[] { "seller.nmc_chandlery" });
        }

        [TestCaseSource(nameof(InvalidSounderSellers))]
        public void SounderRelocation_RejectsMissingDuplicateOrUnrelatedSellers(string[] sellerIds)
        {
            WithSounderSellers(sellerIds, (offers, sellers) =>
                Assert.Throws<AssertionException>(() => AssertHardwareSellers(offers, sellers)));
        }

        static void WithSounderSellers(string[] sellerIds,
            System.Action<ICatalogListing[], Dictionary<string, SellerDef>> check)
        {
            var offers = Offers();
            int index = System.Array.FindIndex(offers, o => o.ListingId == SounderId);
            var copy = Object.Instantiate((InstrumentOffer)offers[index]);
            try
            {
                copy.Catalog.Sellers = sellerIds;
                offers[index] = copy;
                check(offers, Resources.LoadAll<SellerDef>(SellerDirectory.ResourceFolder).ToDictionary(s => s.Id));
            }
            finally { Object.DestroyImmediate(copy); }
        }

        [TestCase(false, "seller.hectors_barrel")]
        [TestCase(true, IslandSeller)]
        public void OutboardFitting_RejectsMissingClassificationOrAnIslandSeller(bool hardware, string seller)
        {
            var offers = Offers();
            int index = System.Array.FindIndex(offers, o => o.ListingId == OutboardId);
            var copy = Object.Instantiate((ShipwrightOffer)offers[index]);
            try
            {
                copy.Catalog.BoatHardware = hardware;
                copy.Catalog.Sellers = new[] { seller };
                offers[index] = copy;
                var sellers = Resources.LoadAll<SellerDef>(SellerDirectory.ResourceFolder).ToDictionary(s => s.Id);
                Assert.Throws<AssertionException>(() => AssertHardwareSellers(offers, sellers));
            }
            finally { Object.DestroyImmediate(copy); }
        }

        [Test]
        public void EveryCatalogAndConversationSeller_HasUniqueDataAndValidKeeper()
        {
            var sellers = Resources.LoadAll<SellerDef>(SellerDirectory.ResourceFolder);
            Assert.AreEqual(10, sellers.Length,
                "six existing counters, two authored vehicle sellers and the two NMC wholesalers receive identities");
            var byId = new Dictionary<string, SellerDef>();
            foreach (var seller in sellers)
            {
                Assert.IsFalse(byId.ContainsKey(seller.Id), "duplicate seller " + seller.Id);
                byId.Add(seller.Id, seller);
                StringAssert.StartsWith("seller.", seller.Id);
                StringAssert.StartsWith("trade.", seller.TradeId);
                Assert.IsTrue(AssetDatabase.GetAssetPath(seller).StartsWith(SellerDirectory.AssetFolder + "/"));
            }

            var npcIds = new HashSet<string>();
            foreach (string guid in AssetDatabase.FindAssets("t:NpcDef", new[] { "Assets/_Project/Data" }))
            {
                var npc = new SerializedObject(AssetDatabase.LoadMainAssetAtPath(AssetDatabase.GUIDToAssetPath(guid)));
                string npcId = npc.FindProperty("Id").stringValue;
                npcIds.Add(npcId);
                Object dialogue = npc.FindProperty("Dialogue").objectReferenceValue;
                if (dialogue == null) continue;
                var options = new SerializedObject(dialogue).FindProperty("Options");
                for (int i = 0; i < options.arraySize; i++)
                    foreach (string field in new[] { "CatalogSellerId", "SellAtSellerId" })
                    {
                        string sellerId = options.GetArrayElementAtIndex(i).FindPropertyRelative(field).stringValue;
                        if (string.IsNullOrEmpty(sellerId)) continue;
                        Assert.IsTrue(byId.ContainsKey(sellerId), "conversation seller missing: " + sellerId);
                        Assert.AreEqual(npcId, byId[sellerId].KeeperNpcId, "keeper comes from the existing speaker data");
                    }
            }
            foreach (var seller in sellers)
                if (!string.IsNullOrEmpty(seller.KeeperNpcId))
                    Assert.IsTrue(npcIds.Contains(seller.KeeperNpcId), "keeper must resolve: " + seller.Id);

            foreach (string sellerId in new[] { "seller.nmc_vehicle_dealer", "seller.nmc_industrial_yard" })
            {
                Assert.IsTrue(byId.ContainsKey(sellerId), "authored vehicle seller missing: " + sellerId);
                Assert.AreEqual("", byId[sellerId].KeeperNpcId, "vehicle keepers have not been authored");
                Assert.AreEqual("trade.vehicles", byId[sellerId].TradeId);
            }

            // The two wholesalers (ruling 11-16): their buildings, identities and offers, and now their keepers.
            foreach (var (sellerId, tradeId, keeperId) in new[]
                     {
                         ("seller.nmc_boat_gear_wholesaler", "trade.boat_gear_wholesale", "npc.lionel_cormier"),
                         ("seller.nmc_fishing_gear_wholesaler", "trade.fishing_gear_wholesale", "npc.bernadette_pitre"),
                     })
            {
                Assert.IsTrue(byId.ContainsKey(sellerId), "wholesaler seller missing: " + sellerId);
                Assert.AreEqual(keeperId, byId[sellerId].KeeperNpcId, "the wholesaler's keeper");
                Assert.AreEqual(tradeId, byId[sellerId].TradeId);
            }

            foreach (string guid in AssetDatabase.FindAssets("t:ScriptableObject", new[] { CatalogSource.CatalogFolderPath }))
            {
                var listing = AssetDatabase.LoadMainAssetAtPath(AssetDatabase.GUIDToAssetPath(guid)) as ICatalogListing;
                if (listing == null || listing.Catalog.Sellers == null) continue;
                foreach (string sellerId in listing.Catalog.Sellers)
                    Assert.IsTrue(byId.ContainsKey(sellerId), "catalog seller missing: " + sellerId);
            }
        }

        [Test]
        public void ExistingFuelAndHomeTills_KeepUnassignedKeepersAndAuthoredTrades()
        {
            foreach (string type in new[] { "FuelStationDef", "HomeDef" })
            {
                var assets = AssetDatabase.FindAssets("t:" + type, new[] { "Assets/_Project/Data" });
                Assert.AreEqual(type == "FuelStationDef" ? 2 : 8, assets.Length);
                foreach (string guid in assets)
                {
                    var asset = new SerializedObject(AssetDatabase.LoadMainAssetAtPath(AssetDatabase.GUIDToAssetPath(guid)));
                    Assert.AreEqual("", asset.FindProperty("KeeperNpcId").stringValue, "do not guess a keeper");
                    Assert.AreEqual(type == "FuelStationDef" ? "trade.fuel" : "trade.housing", asset.FindProperty("TradeId").stringValue);
                }
            }
        }

        [Test]
        public void MissingAndDuplicateSellerDefinitions_FailToUnknown()
        {
            Assert.AreEqual("unknown", SellerDirectory.Resolve("seller.missing", null).MerchantId);
            var one = ScriptableObject.CreateInstance<SellerDef>();
            var two = ScriptableObject.CreateInstance<SellerDef>();
            try
            {
                one.Id = two.Id = "seller.test";
                one.KeeperNpcId = "npc.one";
                two.KeeperNpcId = "npc.two";
                var resolved = SellerDirectory.Resolve("seller.test", new[] { one, two });
                Assert.AreEqual(TradeCounterparty.UnknownId, resolved.MerchantId);
                Assert.AreEqual("seller.test", resolved.VendorId);
                Assert.AreEqual(TradeCounterparty.UnknownId, resolved.TradeId);
            }
            finally
            {
                Object.DestroyImmediate(one);
                Object.DestroyImmediate(two);
            }
        }
    }
}
