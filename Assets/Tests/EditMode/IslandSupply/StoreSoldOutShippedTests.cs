using System;
using System.Collections.Generic;
using System.Linq;
using HiddenHarbours.Core;
using HiddenHarbours.Economy;
using HiddenHarbours.World;
using NUnit.Framework;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// The shipped store sold-out wiring: the St Peters shelves name the store's real seller and counter
    /// listings, the shelf-slot Def names lines those shelves hold and carries its card, and the game's
    /// windowed run calendar answers exactly what one long calendar answers on the island's real tide, so
    /// the shelves the game builds count the same as the shelves the slice-2 tests pin.
    /// </summary>
    public class StoreSoldOutShippedTests
    {
        private const string ShelvesId = "islandshelves.st_peters_general";
        private const string SlotsId = "shelfslots.st_peters_general";

        private GameConfig _cfg;
        private IslandSupplyConfig _route;
        private IslandStoreShelvesDef _def;
        private Dictionary<string, CarrierDef> _carriers;

        [OneTimeSetUp]
        public void Load()
        {
            _cfg = IslandSupplyTestData.Config();
            _route = IslandSupplyTestData.Route();
            _carriers = IslandSupplyTestData.CarriersById();
            _def = IslandSupplyTestData.LoadAll<IslandStoreShelvesDef>().Single(d => d.Id == ShelvesId);
        }

        [Test]
        public void TheShelvesSellAtTheStoresOwnCounter_EachListingOnOneShelf()
        {
            ShopDef shop = IslandSupplyTestData.LoadAll<ShopDef>().Single(s => s.Spec.Id == _def.ShopId);
            Assert.That(_def.SellerId, Is.Not.Empty);
            Assert.That(_def.SellerId, Is.EqualTo(shop.Spec.SellerId), "the shelves are the counter of the store they stand in");
            Assert.That(_def.RecheckGameMinutes, Is.GreaterThanOrEqualTo(1f));

            var listings = new Dictionary<string, ICatalogListing>(StringComparer.Ordinal);
            foreach (SupplyDef s in IslandSupplyTestData.LoadAll<SupplyDef>()) listings[s.Id] = s;
            foreach (BaitDef b in IslandSupplyTestData.LoadAll<BaitDef>()) listings[b.Id] = b;
            foreach (GearOffer g in IslandSupplyTestData.LoadAll<GearOffer>()) listings[g.Id] = g;

            var seen = new HashSet<string>(StringComparer.Ordinal);
            int counted = 0;
            foreach (IslandShelfLine line in _def.Lines)
                foreach (string id in line.CounterListingIds)
                {
                    Assert.IsTrue(seen.Add(id), id + " is counted on two shelves");
                    Assert.IsTrue(listings.TryGetValue(id, out ICatalogListing listing), id + " is not a shipped supply, bait or gear listing");
                    Assert.IsTrue(listing.Catalog.IsStockedBy(_def.SellerId), id + " is not sold at " + _def.SellerId);
                    counted++;
                }
            Assert.That(counted, Is.GreaterThan(0), "at least one counter listing comes off a shelf");
        }

        [Test]
        public void TheCountedRodStaysInTheBookUntilItHasAPurchaseSlot()
        {
            IslandShelfLine rod = _def.Lines.Single(l => l.CounterListingIds.Contains("gear.rod"));
            IslandShelfLine bait = _def.Lines.Single(l => l.CounterListingIds.Contains("bait.capelin"));
            ShopShelfSlotsDef slots = IslandSupplyTestData.LoadAll<ShopShelfSlotsDef>().Single(d => d.Id == SlotsId);
            GearOffer offer = IslandSupplyTestData.LoadAll<GearOffer>().Single(g => g.Id == "gear.rod");
            BaitDef capelin = IslandSupplyTestData.LoadAll<BaitDef>().Single(b => b.Id == "bait.capelin");
            ShopDef shop = IslandSupplyTestData.LoadAll<ShopDef>().Single(s => s.Spec.Id == _def.ShopId);

            Assert.That(rod.Id, Is.EqualTo("line.fishing_rod"));
            Assert.That(rod.CounterListingIds, Is.EqualTo(new[] { offer.Id }));
            Assert.That(rod.MaxOnShelf, Is.EqualTo(shop.Spec.Stock.Single(l => l.ListingId == offer.Id).MaxOnShelf));
            Assert.That(rod.MaxOnShelf, Is.EqualTo(4));
            Assert.That(rod.UnitsPerItem, Is.EqualTo(1.25f));
            Assert.That(rod.ShareOfGood, Is.EqualTo(0.1f));
            Assert.IsTrue(rod.ReservedForSales, "rod stock changes only on a sale or delivery");
            Assert.That(rod.Good.Id, Is.EqualTo("islandgood.hardware"));
            Assert.That(bait.Good, Is.Not.SameAs(rod.Good));
            Assert.IsFalse(rod.SoldAtShelf, "a listing without a purchase slot must remain reachable in the book");
            Assert.IsTrue(bait.SoldAtShelf);
            Assert.That(slots.Slots.Count(s => s.ListingId == offer.Id), Is.Zero);
            Assert.That(slots.Slots.Single(s => s.ListingId == capelin.Id).LineId, Is.EqualTo(bait.Id));
            Assert.That(offer.Price, Is.EqualTo(60));
            Assert.That(capelin.Price, Is.EqualTo(2));
            Assert.That(capelin.LotSize, Is.EqualTo(10));
            Assert.That(BaitShop.LotPriceOf(capelin), Is.EqualTo(20));
        }

        [Test]
        public void TheSlotsStandOnLinesTheShelvesHold_AndCarryTheirCard()
        {
            ShopShelfSlotsDef slots = IslandSupplyTestData.LoadAll<ShopShelfSlotsDef>().Single(d => d.Id == SlotsId);
            Assert.That(slots.Id, Does.Match("^shelfslots\\.[a-z0-9_]+$"));
            Assert.That(slots.ShopId, Is.EqualTo(_def.ShopId));
            Assert.That(slots.BuildingName, Is.Not.Empty);
            Assert.That(slots.SoldOutSign, Is.Not.Null, "the card art is wired");
            Assert.That(slots.Slots.Count, Is.GreaterThan(0));

            var lines = new HashSet<string>(_def.Lines.Select(l => l.Id));
            var ids = new HashSet<string>();
            var onLine = new HashSet<string>();
            foreach (ShopShelfSlot slot in slots.Slots)
            {
                Assert.That(slot.Id, Does.Match("^slot\\.[a-z0-9_]+$"), slot.Id);
                Assert.IsTrue(ids.Add(slot.Id), "duplicate slot " + slot.Id);
                Assert.IsTrue(lines.Contains(slot.LineId), slot.Id + " stands on " + slot.LineId + ", which the shelves do not hold");
                // A line may have several slots (one per good bought at the shelf), but only one card.
                if (!slot.HideSoldOutCard) Assert.IsTrue(onLine.Add(slot.LineId), "two cards for " + slot.LineId);
            }
        }

        [Test]
        public void TheWindowedRunCalendarAnswersWhatOneLongCalendarAnswers()
        {
            Func<double, float> water = IslandSupplyTestData.Water(_route, _cfg);
            const int first = -40, last = 160;
            Func<int, bool> whole = IslandRunCalendar.DaylightRunDays(water, _def.Ledger.Runner, _route, _cfg, first, last);
            var windowed = new IslandRunDays(water, _def.Ledger.Runner, _route, _cfg, _def.Ledger.LookbackDays, 3);
            int runs = 0;
            for (int day = first; day <= last; day++)
            {
                Assert.That(windowed.Possible(day), Is.EqualTo(whole(day)), "day " + day);
                if (whole(day)) runs++;
            }
            Assert.That(runs, Is.GreaterThan(0));
            Assert.That(windowed.HeldBlocks, Is.LessThanOrEqualTo(3), "the window stays bounded");
        }

        [Test]
        public void TheShelvesTheGameBuildsCountAsTheSliceTwoShelvesCount()
        {
            Func<double, float> water = IslandSupplyTestData.Water(_route, _cfg);
            Func<int, bool> possible = IslandRunCalendar.DaylightRunDays(water, _def.Ledger.Runner, _route, _cfg, -40, 120);
            IslandStoreShelves pinned = IslandStoreShelves.FromDef(_def, _route, _carriers, possible, _cfg.SecondsPerDay);
            IslandStoreShelves built = IslandShelvesHost.Build(_def, _route, _carriers, _cfg, water);
            Assert.That(built.ShopId, Is.EqualTo(_def.ShopId));
            for (int day = 0; day < 90; day += 3)
                foreach (IslandShelfLine line in _def.Lines)
                {
                    double t = (day + 10.0 / 24.0) * _cfg.SecondsPerDay;
                    Assert.That(built.CountAt(line.Id, t), Is.EqualTo(pinned.CountAt(line.Id, t)), line.Id + " day " + day);
                }
        }
    }
}
