using System.Collections.Generic;
using HiddenHarbours.Core;
using HiddenHarbours.Economy;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.Tests.Economy
{
    public class SellerContentTests
    {
        [Test]
        public void EveryCatalogAndConversationSeller_HasUniqueDataAndValidKeeper()
        {
            var sellers = Resources.LoadAll<SellerDef>(SellerDirectory.ResourceFolder);
            Assert.AreEqual(8, sellers.Length, "six existing counters and two authored vehicle sellers receive identities");
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
