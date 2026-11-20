using System;
using System.Collections.Generic;
using System.Linq;
using HiddenHarbours.Core;
using HiddenHarbours.Economy;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.Tests.Economy
{
    /// <summary>
    /// <b>WHAT THE TWO NINE MILE CREEK WHOLESALERS STOCK</b> (owner ruling 11-16: Loran and every boat
    /// electronic at a new boat/gear wholesaler, fishing stock at a separate fishing gear wholesaler; prices
    /// and routing approved from <c>docs/economy/nmc-wholesalers-stock.md</c>). Reads the shipped catalog.
    ///
    /// <para><b>On the shelf, or on file.</b> Both counters and keepers stand in the creek, and the four offers
    /// only a wholesaler sells (the three electronics and the rod rack) are listed at their vendors
    /// (<c>StoreWiringTests</c> guard 4 holds a listed ware to a committed vendor). The rest also name another
    /// seller and stay unlisted until that is settled; <c>docs/design/economy-item-sellers.md</c> §5.1 says why.</para>
    ///
    /// <para>The ids are spelled here rather than read from the Boats module's constants: this assembly
    /// references Core and Economy only (rule 4), and an id is a stable contract either way.</para>
    /// </summary>
    public class WholesalerStockTests
    {
        const string BoatGear = "seller.nmc_boat_gear_wholesaler";
        const string FishingGear = "seller.nmc_fishing_gear_wholesaler";
        const string Loran = "instrument.loran";

        /// <summary>Authored electronics: three existing prices and six unlisted price proposals.</summary>
        static readonly Dictionary<string, int> BoatGearStock = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            [Loran] = 900,
            ["instrument.fish_finder"] = 650,
            ["instrument.gps"] = 1800,
            ["instrument.autopilot"] = 6000,
            ["instrument.compass_dome"] = 70,
            ["instrument.compass_flush"] = 140,
            ["instrument.radar"] = 3600,
            ["instrument.wind_dial"] = 140,
            ["instrument.wind_display"] = 650,
        };

        /// <summary>The fishing stock that exists as unlisted offers today, at its retained prices. The rod,
        /// capelin and both pots are already on other counters' shelves and join when the counter stands.</summary>
        static readonly Dictionary<string, int> FishingGearStock = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["bait.fish_scrap"] = 1,
            ["bait.herring"] = 3,
            ["bait.mackerel"] = 4,
            ["bait.sea_worm"] = 4,
            ["bait.shucked_clam"] = 3,
            ["bait.squid_strip"] = 6,
            ["gear.bucket"] = 15,
            ["gear.mussel_sack"] = 10,
            ["gear.oyster_tongs"] = 45,
            ["gear.rod_rack"] = 25,
            ["gear.shellfish_basket"] = 20,
            ["gear.shellfish_rake"] = 30,
            ["gear.shovel"] = 25,
        };

        sealed class Offer
        {
            public string Id, Kind, Path;
            public int Price;
            public CatalogListing Catalog;
            public override string ToString() => $"{Id} ({Kind}, {Path})";
        }

        /// <summary>Every listing in the catalog, of every kind the books sell, with its price.</summary>
        static List<Offer> Catalog()
        {
            var all = new List<Offer>();
            void Add<T>(IEnumerable<T> items, Func<T, string> id, Func<T, int> price, Func<T, CatalogListing> cat) where T : UnityEngine.Object
            {
                foreach (T o in items)
                    all.Add(new Offer { Id = id(o), Kind = typeof(T).Name, Path = AssetDatabase.GetAssetPath(o), Price = price(o), Catalog = cat(o) });
            }
            Add(CatalogSource.All<InstrumentOffer>(), o => o.Id, o => o.Price, o => o.Catalog);
            Add(CatalogSource.All<GearOffer>(), o => o.Id, o => o.Price, o => o.Catalog);
            Add(CatalogSource.All<BaitDef>(), o => o.Id, o => o.Price, o => o.Catalog);
            Add(CatalogSource.All<PotOffer>(), o => o.Id, o => o.Price, o => o.Catalog);
            Add(CatalogSource.All<SupplyDef>(), o => o.Id, o => o.Price, o => o.Catalog);
            Add(CatalogSource.All<LicenseDef>(), o => o.Id, o => o.Price, o => o.Catalog);
            Add(CatalogSource.All<ShipwrightOffer>(), o => o.BoatId, o => o.Price, o => o.Catalog);
            Assert.That(all, Is.Not.Empty, "the catalog loaded nothing: every check here would pass on nothing");
            return all;
        }

        static bool Names(Offer o, string seller) => o.Catalog.Sellers != null && o.Catalog.Sellers.Contains(seller);

        // ---- the Loran -----------------------------------------------------------------------------------

        [Test]
        public void TheLoran_IsSoldOnlyAtTheBoatGearWholesaler_AtTheApprovedPrice()
        {
            List<Offer> loran = Catalog().Where(o => o.Id == Loran).ToList();
            Assert.AreEqual(1, loran.Count, "one Loran offer in the catalog: " + string.Join(", ", loran));
            Assert.AreEqual(900, loran[0].Price, "the approved wholesaler price, not the 380 placeholder");
            CollectionAssert.AreEqual(new[] { BoatGear }, loran[0].Catalog.Sellers,
                                      "the boat/gear wholesaler is its only seller: no placeholder counter keeps it");
            Assert.AreEqual(CatalogSection.Gear, loran[0].Catalog.Section, "the counter's everyday shelf");
        }

        [Test]
        public void NoOtherSellersBook_HasTheLoran()
        {
            foreach (SellerDef s in Resources.LoadAll<SellerDef>(SellerDirectory.ResourceFolder))
            {
                if (s == null || s.Id == BoatGear) continue;
                Assert.IsFalse(CatalogSource.For<InstrumentOffer>(s.Id).Any(o => o.Id == Loran), $"{s.Id}'s book still sells the Loran");
            }
        }

        // ---- the two lists -----------------------------------------------------------------------------------

        [Test]
        public void TheBoatGearWholesaler_NamesAllNineElectronics_AtTheirAuthoredPrices() =>
            StocksExactly(BoatGear, BoatGearStock);

        [Test]
        public void TheFishingGearWholesaler_StocksTheFishingList_AtTheRetainedPrices() =>
            StocksExactly(FishingGear, FishingGearStock);

        static void StocksExactly(string seller, Dictionary<string, int> expected)
        {
            List<Offer> named = Catalog().Where(o => Names(o, seller)).ToList();
            CollectionAssert.AreEquivalent(expected.Keys, named.Select(o => o.Id).ToList(),
                                           $"the offers naming {seller}: " + string.Join(", ", named));
            foreach (Offer o in named)
                Assert.AreEqual(expected[o.Id], o.Price, $"{o} is not at its authored price");
        }

        [Test]
        public void BoatElectronics_AreNotOnTheChandleryOrTheIslandPlaceholder()
        {
            foreach (string id in BoatGearStock.Keys)
                foreach (Offer o in Catalog().Where(o => o.Id == id))
                    foreach (string other in new[] { "seller.leblancs", "seller.nmc_chandlery" })
                        Assert.IsFalse(Names(o, other), $"{o} still names {other}; boat electronics are the boat/gear wholesaler's");
        }

        // ---- every listed wholesaler item resolves -----------------------------------------------------------

        [Test]
        public void EveryWholesalerItem_ResolvesToOneOfferAndARealSeller()
        {
            List<Offer> catalog = Catalog();
            foreach ((string seller, Dictionary<string, int> stock, string trade) in new[]
                     {
                         (BoatGear, BoatGearStock, "trade.boat_gear_wholesale"),
                         (FishingGear, FishingGearStock, "trade.fishing_gear_wholesale"),
                     })
            {
                TradeCounterparty who = SellerDirectory.Resolve(seller);
                Assert.AreEqual(seller, who.VendorId, $"{seller} does not resolve to its SellerDef");
                Assert.AreEqual(trade, who.TradeId, $"{seller} resolves to the wrong trade");

                foreach (string id in stock.Keys)
                {
                    List<Offer> hits = catalog.Where(o => o.Id == id && Names(o, seller)).ToList();
                    Assert.AreEqual(1, hits.Count, $"{id} on {seller}'s list resolves to {hits.Count} offers naming it");
                    Assert.Greater(hits[0].Price, 0, $"{hits[0]} has no price");
                    Assert.IsFalse(hits[0].Catalog.IsOrphaned, $"{hits[0]} is listed for nobody");
                    StringAssert.StartsWith(CatalogSource.CatalogFolderPath + "/", hits[0].Path,
                                            $"{hits[0]} sits outside the catalog folder, where no book can find it");
                }
            }
        }
    }
}
