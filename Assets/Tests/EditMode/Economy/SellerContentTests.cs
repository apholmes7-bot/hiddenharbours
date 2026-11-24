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
        static readonly Dictionary<string, string> KnownIslandHardwareExceptions = new Dictionary<string, string>
        {
            ["seller.leblancs|instrument.depth_sounder"] =
                "Existing island counter template; relocation requires a counter and scene change."
        };

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
        public void IslandSellers_OfferNoBoatHardware_ExceptNamedExistingCounter()
        {
            var sellers = Resources.LoadAll<SellerDef>(SellerDirectory.ResourceFolder).ToDictionary(s => s.Id);
            var regions = new HashSet<string>(AssetDatabase.FindAssets("t:RegionDef", new[] { "Assets/_Project/Data" })
                .Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadMainAssetAtPath)
                .Select(a => new SerializedObject(a).FindProperty("Id").stringValue));
            foreach (var seller in sellers.Values)
                Assert.That(regions.Contains(seller.RegionId), Is.True, seller.Id + " needs a real region");
            Assert.That(sellers["seller.leblancs"].RegionId, Is.EqualTo("region.st_peters"));
            var found = new HashSet<string>();
            foreach (var offer in Offers())
            {
                if (offer is InstrumentOffer)
                    Assert.That(offer.Catalog.BoatHardware, Is.True, offer.ListingId + " helm fitting needs hardware classification");
                if (!offer.Catalog.BoatHardware) continue;
                foreach (string seller in offer.Catalog.Sellers ?? new string[0])
                {
                    Assert.That(sellers.ContainsKey(seller), Is.True, offer.ListingId + " names missing seller " + seller);
                    if (sellers[seller].RegionId != "region.st_peters") continue;
                    string key = seller + "|" + offer.ListingId;
                    Assert.That(KnownIslandHardwareExceptions.ContainsKey(key), Is.True,
                        "Island seller " + seller + " offers boat hardware " + offer.ListingId + "; move the offer to a mainland seller");
                    Assert.That(KnownIslandHardwareExceptions[key], Is.Not.Empty);
                    found.Add(key);
                }
            }
            Assert.That(found, Is.EquivalentTo(KnownIslandHardwareExceptions.Keys),
                "Remove the named exception once its island offer has gone");
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
