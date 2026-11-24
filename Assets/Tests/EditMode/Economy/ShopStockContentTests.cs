using System;
using System.Linq;
using HiddenHarbours.Economy;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.Tests.EditMode.Economy
{
    public class ShopStockContentTests
    {
        private const string PilotPath = "Assets/_Project/Data/Shops/StPetersGeneral.asset";

        [Test]
        public void SpecialOrders_AreSixExistingUnstockedBaits_WithValidCounterAndTerms()
        {
            var lines = Resources.LoadAll<SpecialOrderLineDef>("SpecialOrders");
            Assert.That(lines.Select(line => line.ItemId), Is.EquivalentTo(new[]
            {
                "bait.fish_scrap", "bait.herring", "bait.mackerel", "bait.sea_worm",
                "bait.shucked_clam", "bait.squid_strip"
            }));
            Assert.That(AssetDatabase.FindAssets("t:SpecialOrderLineDef", new[] { "Assets/_Project/Data" }),
                Has.Length.EqualTo(6), "Every authored line must be in the discoverable directory.");
            Assert.That(lines.Select(line => line.Id).Distinct().Count(), Is.EqualTo(6));
            var baits = Resources.LoadAll<BaitDef>("Catalog").ToDictionary(bait => bait.Id);
            var sellers = Resources.LoadAll<SellerDef>("Sellers").Select(seller => seller.Id).ToArray();
            var shop = AssetDatabase.LoadAssetAtPath<ShopDef>(PilotPath);
            var shelves = Resources.LoadAll<IslandStoreShelvesDef>("IslandSupply/Shelves")
                .Single(shelf => shelf.SellerId == shop.Spec.SellerId);
            foreach (var line in lines)
            {
                Assert.DoesNotThrow(() => line.BuildLine(), line.Id);
                Assert.That(line.SellerId, Is.EqualTo(shop.Spec.SellerId));
                Assert.That(sellers, Does.Contain(line.SellerId));
                Assert.That(baits.ContainsKey(line.ItemId), Is.True, line.Id);
                Assert.That(line.Bait, Is.SameAs(baits[line.ItemId]));
                Assert.That(baits[line.ItemId].Catalog.Listed, Is.False, "No existing stock is moved to special order.");
                Assert.That(line.Price, Is.EqualTo(baits[line.ItemId].Price), "One individual bait per unit.");
                Assert.That(line.BuildLine().Price, Is.EqualTo(baits[line.ItemId].Price));
                Assert.That(line.DeliveryDays, Is.EqualTo(2));
                Assert.That(line.MaxOnOrder, Is.EqualTo(20));
                Assert.That(shop.Spec.Stock.Select(stock => stock.ListingId), Does.Not.Contain(line.ItemId));
                Assert.That(shelves.Lines.SelectMany(shelf => shelf.CounterListingIds), Does.Not.Contain(line.ItemId));
            }
        }

        [Test]
        public void SpecialOrderPrice_ReadsItsBaitDefWithoutASerializedPriceCopy()
        {
            var bait = ScriptableObject.CreateInstance<BaitDef>();
            var offer = ScriptableObject.CreateInstance<SpecialOrderLineDef>();
            try
            {
                bait.Id = "bait.test";
                bait.Price = 3;
                offer.Id = "specialorder.test";
                offer.SellerId = "seller.test";
                offer.Bait = bait;
                offer.DeliveryDays = 2;
                offer.MaxOnOrder = 20;
                Assert.That(offer.BuildLine().Price, Is.EqualTo(3));
                bait.Price = 17;
                Assert.That(offer.Price, Is.EqualTo(17));
                Assert.That(offer.BuildLine().Price, Is.EqualTo(17));
                Assert.That(offer.BuildLine().ItemId, Is.EqualTo(bait.Id));
                Assert.That(new SerializedObject(offer).FindProperty("Price"), Is.Null);
                Assert.That(new SerializedObject(offer).FindProperty("ItemId"), Is.Null);
                offer.Bait = null;
                Assert.Throws<ArgumentException>(() => offer.BuildLine());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(offer);
                UnityEngine.Object.DestroyImmediate(bait);
            }
        }

        [Test]
        public void EveryShopLineNamesARealSellerListingAndUniqueDeclaredSlots()
        {
            var paths = AssetDatabase.FindAssets("t:ShopDef", new[] { "Assets/_Project/Data" })
                .Select(AssetDatabase.GUIDToAssetPath).ToArray();
            Assert.That(paths, Is.EquivalentTo(new[] { PilotPath }), "Only the pilot is authored in this slice.");
            var listings = Resources.LoadAll<ScriptableObject>("Catalog").OfType<ICatalogListing>().ToArray();
            foreach (var path in paths)
            {
                var shop = AssetDatabase.LoadAssetAtPath<ShopDef>(path);
                Assert.That(shop, Is.Not.Null);
                Assert.DoesNotThrow(() => shop.BuildSpec(listings), path);
            }
        }

        [Test]
        public void PilotStocksExactlyThePortableWaresWithExistingPackSizesAndEssentialFloors()
        {
            var shop = AssetDatabase.LoadAssetAtPath<ShopDef>(PilotPath);
            Assert.That(shop, Is.Not.Null);
            var spec = shop.BuildSpec(Resources.LoadAll<ScriptableObject>("Catalog").OfType<ICatalogListing>());
            Assert.That(spec.Id, Is.EqualTo("shop.st_peters_general"));
            Assert.That(spec.Stock.Select(line => line.ListingId), Is.EquivalentTo(new[] { "gear.rod", "bait.capelin", "supply.ice" }));
            Assert.That(spec.Stock.Select(line => line.MaxOnShelf), Is.EqualTo(new[] { 4, 20, 20 }));
            Assert.That(spec.Stock.Select(line => line.UnitsPerItem), Is.EqualTo(new[] { 1, 10, 1 }));
            Assert.That(spec.Stock.Select(line => line.Floor), Is.EqualTo(new[] { 0, 1, 1 }));
            Assert.That(spec.Stock.Select(line => line.DeliveryWeekdays), Is.All.EqualTo(ShopDeliveryDays.Monday | ShopDeliveryDays.Thursday));
            Assert.That(spec.Shelves.Sum(shelf => shelf.SlotCount), Is.EqualTo(44));
        }

        [Test]
        public void AChangedPackSizeOrCounterOnlyListingIsRejected()
        {
            var source = AssetDatabase.LoadAssetAtPath<ShopDef>(PilotPath);
            Assert.That(source, Is.Not.Null);
            var copy = ScriptableObject.CreateInstance<ShopDef>();
            try
            {
                copy.Spec = source.Spec.Snapshot();
                var listings = Resources.LoadAll<ScriptableObject>("Catalog").OfType<ICatalogListing>().ToArray();
                copy.Spec.Stock[1].UnitsPerItem = 1;
                Assert.Throws<ArgumentException>(() => copy.BuildSpec(listings));
                copy.Spec.Stock[1].UnitsPerItem = 10;
                var licence = listings.Single(listing => listing.ListingId == "license.clam");
                Assert.That(licence.Catalog.IsStockedBy(copy.Spec.SellerId), Is.True);
                copy.Spec.Stock[0].ListingId = licence.ListingId;
                Assert.Throws<ArgumentException>(() => copy.BuildSpec(listings));
            }
            finally { UnityEngine.Object.DestroyImmediate(copy); }
        }
    }
}
