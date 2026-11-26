using System;
using System.Linq;
using System.Collections.Generic;
using HiddenHarbours.Core;
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
        public void EveryCountedIslandListing_HasStartingStockAndADataRestockRule()
        {
            var defs = Resources.LoadAll<IslandStoreShelvesDef>("IslandSupply/Shelves");
            var route = Resources.Load<IslandSupplyConfig>(IslandSupplyConfig.ResourcesPath);
            var carriers = Resources.LoadAll<CarrierDef>("IslandSupply").ToDictionary(c => c.Id);
            var sellers = Resources.LoadAll<SellerDef>(SellerDirectory.ResourceFolder).Select(s => s.Id).ToArray();
            var offers = Resources.LoadAll<ScriptableObject>("Catalog").OfType<ICatalogListing>().ToArray();
            Assert.That(defs, Is.Not.Empty);
            Assert.That(route, Is.Not.Null);
            Assert.That(route.BufferDays, Is.GreaterThan(0));
            Assert.That(carriers, Is.Not.Empty);
            foreach (var def in defs)
            {
                Assert.That(sellers, Does.Contain(def.SellerId), def.Id);
                Assert.That(def.Ledger, Is.Not.Null, def.Id);
                Assert.That(def.Ledger.IslandRegionId, Is.EqualTo(route.IslandRegionId), def.Id);
                Assert.That(def.Ledger.Runner, Is.Not.Null, def.Id);
                Assert.That(def.Ledger.Runner.CadenceDays, Is.GreaterThan(0), def.Id);
                var shelves = IslandStoreShelves.FromDef(def, route, carriers, _ => true, GameConfig.DefaultSecondsPerDay);
                var seen = new HashSet<string>();
                foreach (var line in def.Lines)
                {
                    Assert.That(line.Good, Is.Not.Null, line.Id);
                    Assert.That(line.Good.Source, Is.EqualTo(GoodSource.MainlandOverBar), line.Id);
                    Assert.That(def.Ledger.Goods, Does.Contain(line.Good), line.Id);
                    Assert.That(line.Good.DailyUnits, Is.GreaterThan(0), line.Id);
                    Assert.That(line.UnitsPerItem, Is.GreaterThan(0), line.Id);
                    Assert.That(line.MaxOnShelf, Is.GreaterThan(0), line.Id);
                    int expected = Math.Min(line.MaxOnShelf, (int)Math.Floor(line.Good.DailyUnits *
                        (1.0 + line.Good.PlayerAllowanceFraction) * route.BufferDays / line.UnitsPerItem + 1e-9));
                    Assert.That(expected, Is.GreaterThan(0), line.Id + " has no starting item");
                    Assert.That(shelves.CountAt(line.Id, 0), Is.EqualTo(expected), line.Id + " starting count");
                    foreach (string id in line.CounterListingIds)
                    {
                        Assert.That(seen.Add(id), Is.True, def.Id + " counts " + id + " twice");
                        Assert.That(offers.Any(o => o.ListingId == id && o.Catalog.IsStockedBy(def.SellerId)),
                            Is.True, def.Id + " counts missing seller offer " + id);
                    }
                }
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
