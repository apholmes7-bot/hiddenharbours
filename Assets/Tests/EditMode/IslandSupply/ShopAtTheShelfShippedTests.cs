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
    /// The shipped buying-at-the-shelf wiring (slice 1), as content: the St Peters general store sells its
    /// groceries, ice and bait at the shelf, each such good has exactly one place on the shelf to be bought
    /// from, every slot that sells names a listing off its own line, the goods and empty-shelf art are wired,
    /// the gear, the sounder, the soap and the licence stay in the clerk's book, and every outcome has a line
    /// for the player's bubble.
    /// </summary>
    public class ShopAtTheShelfShippedTests
    {
        private const string ShelvesId = "islandshelves.st_peters_general";
        private const string SlotsId = "shelfslots.st_peters_general";

        // The goods the owner's ruling moves to the shelf in slice 1, and the ones that stay in the book.
        private static readonly string[] AtShelf =
            { "supply.bread", "supply.oatcakes", "supply.tinned_beans", "supply.ice", "bait.capelin" };
        private static readonly string[] StaysInBook = { "supply.soap" };

        private IslandStoreShelvesDef _shelves;
        private ShopShelfSlotsDef _slots;

        [OneTimeSetUp]
        public void Load()
        {
            _shelves = IslandSupplyTestData.LoadAll<IslandStoreShelvesDef>().Single(d => d.Id == ShelvesId);
            _slots = IslandSupplyTestData.LoadAll<ShopShelfSlotsDef>().Single(d => d.Id == SlotsId);
        }

        private HashSet<string> SoldAtShelf()
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (IslandShelfLine line in _shelves.Lines)
                if (line.SoldAtShelf)
                    foreach (string id in line.CounterListingIds) ids.Add(id);
            return ids;
        }

        [Test]
        public void TheGroceriesIceAndBaitAreSoldAtTheShelf_AndTheRestStaysInTheBook()
        {
            HashSet<string> atShelf = SoldAtShelf();
            Assert.That(atShelf, Is.EquivalentTo(AtShelf));
            foreach (string id in StaysInBook) Assert.IsFalse(atShelf.Contains(id), id + " stays in the clerk's book");
            foreach (IslandShelfLine line in _shelves.Lines)
                if (line.SoldAtShelf)
                    Assert.That(line.CounterListingIds, Is.Not.Empty, line.Id + " is sold at the shelf but sells nothing");
        }

        [Test]
        public void EveryShelfGoodHasExactlyOnePlaceToBuyIt_OffItsOwnLine()
        {
            var lines = _shelves.Lines.ToDictionary(l => l.Id);
            var sold = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (ShopShelfSlot slot in _slots.Slots)
            {
                if (string.IsNullOrEmpty(slot.ListingId)) continue;
                Assert.IsTrue(lines.TryGetValue(slot.LineId, out IslandShelfLine line), slot.Id + " stands on no line");
                Assert.IsTrue(line.SoldAtShelf, slot.Id + " sells " + slot.ListingId + ", but " + line.Id + " is sold from the book");
                Assert.That(line.CounterListingIds, Does.Contain(slot.ListingId), slot.Id + "'s listing does not come off " + line.Id);
                Assert.IsFalse(sold.ContainsKey(slot.ListingId), slot.ListingId + " is sold at two slots");
                sold[slot.ListingId] = slot.Id;
            }
            Assert.That(sold.Keys, Is.EquivalentTo(SoldAtShelf()), "every good sold at the shelf can be bought somewhere");
            Assert.That(_slots.ReachMeters, Is.GreaterThan(0f));
        }

        [Test]
        public void EveryLineSoldAtTheShelfShowsItsGoods_AndOneCardWhenEmpty()
        {
            foreach (IslandShelfLine line in _shelves.Lines.Where(l => l.SoldAtShelf))
            {
                List<ShopShelfSlot> on = _slots.Slots.Where(s => s.LineId == line.Id).ToList();
                Assert.That(on.Count(s => s.Goods != null), Is.GreaterThanOrEqualTo(1), line.Id + " draws no goods");
                Assert.That(on.Count(s => !s.HideSoldOutCard), Is.EqualTo(1), line.Id + " has one card");
            }
            Assert.That(_slots.CoverSortingOrderAboveRoom, Is.LessThan(_slots.GoodsSortingOrderAboveRoom), "the goods stand on the empty shelf");
            Assert.That(_slots.GoodsSortingOrderAboveRoom, Is.LessThan(_slots.SortingOrderAboveRoom), "the card stands in front of the goods");
            Assert.That(_slots.Slots.Any(s => s.Cover != null), "the painted gondola is covered by an empty one");
            Assert.That(_slots.Slots.All(s => !s.NeedsFreeHand), "slice 1's goods go to the locker and the tackle box, not the hands");
        }

        [Test]
        public void EveryOutcomeHasALineInThePlayersBubble()
        {
            List<InnerVoiceLineDef> lines = IslandSupplyTestData.LoadAll<InnerVoiceLineDef>();
            InnerVoiceLibrary library = UnityEngine.Resources.Load<InnerVoiceLibrary>(InnerVoiceLibrary.ResourcesPath);
            Assert.IsNotNull(library, "the inner-voice library loads");
            foreach (ShelfBuyResult r in Enum.GetValues(typeof(ShelfBuyResult)))
            {
                string cue = ShelfGood.CueFor(r);
                if (cue == null) continue;
                InnerVoiceLineDef line = lines.SingleOrDefault(l => l.Trigger == InnerVoiceTrigger.Cue && l.SubjectId == cue);
                Assert.IsNotNull(line, r + ": no line answers " + cue);
                Assert.That(line.Line, Is.Not.Empty);
                Assert.IsFalse(line.OnceOnly, r + " is said every time");
                Assert.That(library.Lines, Does.Contain(line), line.Id + " is not in the library, so it is never said");
            }
        }
    }
}
