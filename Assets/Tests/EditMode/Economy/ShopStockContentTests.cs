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
