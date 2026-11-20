using System.Linq;
using HiddenHarbours.Economy;
using NUnit.Framework;
using UnityEditor;

namespace HiddenHarbours.Tests.Economy
{
    public class ElectronicsOfferContentTests
    {
        const string Seller = "seller.nmc_boat_gear_wholesaler";

        [TestCase("instrument.autopilot", 6000)]
        [TestCase("instrument.compass_dome", 70)]
        [TestCase("instrument.compass_flush", 140)]
        [TestCase("instrument.radar", 3600)]
        [TestCase("instrument.wind_dial", 140)]
        [TestCase("instrument.wind_display", 650)]
        public void NewElectronic_HasOneRealItemAndSeller_AndWaitsForCounterWiring(string id, int price)
        {
            var offers = CatalogSource.All<InstrumentOffer>().Where(o => o.Id == id).ToArray();
            Assert.AreEqual(1, offers.Length, id);
            var offer = offers.Single();
            Assert.AreEqual(price, offer.Price);
            Assert.IsNotEmpty(offer.DisplayName);
            CollectionAssert.AreEqual(new[] { Seller }, offer.Catalog.Sellers);
            Assert.AreEqual(CatalogSection.Gear, offer.Catalog.Section);
            Assert.IsFalse(offer.Catalog.Listed, "a new ware needs its own committed vendor before listing");
            Assert.IsFalse(CatalogSource.For<InstrumentOffer>(Seller).Any(o => o.Id == id));
            var sellers = AssetDatabase.FindAssets("t:SellerDef", new[] { "Assets/_Project/Data" })
                .Select(g => AssetDatabase.LoadAssetAtPath<SellerDef>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(s => s.Id == Seller).ToArray();
            Assert.AreEqual(1, sellers.Length, "the offer names one existing seller identity");
        }

        [Test]
        public void WholesalersAndBoatyard_RetainSeparateSellerAndTradeIdentities()
        {
            var expected = new[] { "seller.nmc_boat_gear_wholesaler", "seller.nmc_fishing_gear_wholesaler", "seller.nmc_yard" };
            var sellers = AssetDatabase.FindAssets("t:SellerDef", new[] { "Assets/_Project/Data" })
                .Select(g => AssetDatabase.LoadAssetAtPath<SellerDef>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(s => expected.Contains(s.Id)).ToArray();
            CollectionAssert.AreEquivalent(expected, sellers.Select(s => s.Id));
            CollectionAssert.AllItemsAreUnique(sellers.Select(s => s.TradeId).ToArray());
            Assert.IsTrue(sellers.All(s => !string.IsNullOrEmpty(s.TradeId)));
        }

        [Test]
        public void DepthSounder_StaysAtTheIslandSeller()
        {
            var sounder = CatalogSource.All<InstrumentOffer>().Single(o => o.Id == "instrument.depth_sounder");
            CollectionAssert.AreEqual(new[] { "seller.leblancs" }, sounder.Catalog.Sellers);
            Assert.IsTrue(sounder.Catalog.Listed);
            Assert.AreEqual(140, sounder.Price);
        }
    }
}
