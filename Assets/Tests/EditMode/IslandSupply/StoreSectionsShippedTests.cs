using System.Collections.Generic;
using System.Linq;
using HiddenHarbours.Core;
using HiddenHarbours.Economy;
using HiddenHarbours.World;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// The shipped store sections, as content (owner ask 2026-11-25: several foods on one shelf, picked with
    /// the quick chooser; every item with its own art). Every section has a stable id, words for the bubble
    /// and goods that are real slots selling a listing, each in one section only; no section holds more goods
    /// than the chooser shows rows; the goods stand at their section; the St Peters food section sells the
    /// bread, oatcakes and beans; and every good sold in a store has its OWN picture, shared with no other.
    /// </summary>
    public class StoreSectionsShippedTests
    {
        private const string SlotsId = "shelfslots.st_peters_general";
        private const string ConfigPath = "Assets/_Project/Data/Config/GameConfig.asset";

        private ShopShelfSlotsDef[] _all;
        private ShopShelfSlotsDef _stPeters;
        private IslandStoreShelvesDef[] _shelves;

        [OneTimeSetUp]
        public void Load()
        {
            _all = IslandSupplyTestData.LoadAll<ShopShelfSlotsDef>().ToArray();
            _stPeters = _all.Single(d => d.Id == SlotsId);
            _shelves = IslandSupplyTestData.LoadAll<IslandStoreShelvesDef>().ToArray();
        }

        [Test]
        public void EverySection_HasAStableIdAndWords_AndSellsRealSlotsInNoOtherSection()
        {
            foreach (ShopShelfSlotsDef def in _all)
            {
                var slots = def.Slots.Where(s => s != null).ToDictionary(s => s.Id);
                var ids = new HashSet<string>();
                var taken = new Dictionary<string, string>();
                foreach (ShopShelfSection section in def.Sections)
                {
                    Assert.IsNotNull(section, def.Id + " has an empty section entry");
                    Assert.That(section.Id, Does.Match("^section\\.[a-z0-9_]+$"), def.Id + ": " + section.Id);
                    Assert.IsTrue(ids.Add(section.Id), def.Id + ": duplicate " + section.Id);
                    Assert.That(section.VerbLabel, Is.Not.Null.And.Not.Empty, section.Id + " says nothing in the bubble");
                    Assert.That(section.SlotIds, Is.Not.Empty, section.Id + " sells nothing");
                    foreach (string slotId in section.SlotIds)
                    {
                        Assert.IsTrue(slots.TryGetValue(slotId, out ShopShelfSlot slot), section.Id + " names " + slotId + ", which is no slot");
                        Assert.That(slot.ListingId, Is.Not.Null.And.Not.Empty, section.Id + ": " + slotId + " sells no listing");
                        Assert.IsFalse(taken.ContainsKey(slotId), slotId + " is in " + (taken.TryGetValue(slotId, out string other) ? other : "") + " and " + section.Id);
                        taken[slotId] = section.Id;
                    }
                }
            }
        }

        [Test]
        public void NoSectionHoldsMoreGoodsThanTheChooserShows()
        {
            var config = AssetDatabase.LoadAssetAtPath<GameConfig>(ConfigPath);
            Assert.IsNotNull(config, ConfigPath);
            int rows = Mathf.Clamp(config.InteractChooserMaxRows, 1, InteractChoiceList.HardCap);
            foreach (ShopShelfSlotsDef def in _all)
                foreach (ShopShelfSection section in def.Sections)
                    Assert.That(section.SlotIds.Count, Is.LessThanOrEqualTo(rows),
                                section.Id + " lists more goods than the chooser has rows (" + rows + ")");
        }

        [Test]
        public void TheGoodsStandAtTheirSection_WithinReach()
        {
            foreach (ShopShelfSlotsDef def in _all)
                foreach (ShopShelfSection section in def.Sections)
                    foreach (string slotId in section.SlotIds)
                    {
                        ShopShelfSlot slot = def.Slots.Single(s => s.Id == slotId);
                        Assert.That(Vector2.Distance(slot.Offset, section.Offset), Is.LessThanOrEqualTo(def.ReachMeters),
                                    slotId + " stands too far from " + section.Id + " to be its goods");
                    }
        }

        [Test]
        public void TheStPetersFoodSection_SellsTheBreadOatcakesAndBeans_EachIntoTheBag()
        {
            ShopShelfSection food = _stPeters.Sections.Single(s => s.Id == "section.food");
            Assert.That(food.SlotIds, Is.EqualTo(new[] { "slot.bread", "slot.oatcakes", "slot.beans" }));
            foreach (string slotId in food.SlotIds)
            {
                ShopShelfSlot slot = _stPeters.Slots.Single(s => s.Id == slotId);
                Assert.IsTrue(slot.IntoBag, slotId + " goes into the paper bag");
                Assert.IsFalse(slot.HideSoldOutCard, slotId + " still shows its own sold-out card");
            }
            Assert.That(_stPeters.SectionOf("slot.bread"), Is.SameAs(food));
            Assert.IsNull(_stPeters.SectionOf("slot.ice"), "the ice chest sells on its own");
        }

        [Test]
        public void EveryGoodSoldInAStore_HasItsOwnPicture_SharedWithNoOther()
        {
            foreach (ShopShelfSlotsDef def in _all)
            {
                var byTexture = new Dictionary<Texture2D, string>();
                var sells = new HashSet<string>();
                foreach (ShopShelfSlot slot in def.Slots)
                {
                    if (slot == null || string.IsNullOrEmpty(slot.ListingId)) continue;
                    sells.Add(slot.ListingId);
                    Assert.IsNotNull(slot.Goods, slot.Id + " sells " + slot.ListingId + " with no picture of it");
                    Texture2D tex = slot.Goods.texture;
                    Assert.IsNotNull(tex, slot.Id + "'s picture has no texture");
                    Assert.IsFalse(byTexture.ContainsKey(tex),
                                   slot.ListingId + " shares its picture (" + AssetDatabase.GetAssetPath(tex) + ") with "
                                   + (byTexture.TryGetValue(tex, out string other) ? other : ""));
                    byTexture[tex] = slot.ListingId;
                }

                // Every good the store's shelves sell at the shelf is one of those pictured goods.
                foreach (IslandStoreShelvesDef shelves in _shelves.Where(s => s.ShopId == def.ShopId))
                    foreach (IslandShelfLine line in shelves.Lines)
                    {
                        if (!line.SoldAtShelf) continue;
                        foreach (string listing in line.CounterListingIds)
                            Assert.IsTrue(sells.Contains(listing), listing + " is sold at the shelf but has no slot with its picture");
                    }
            }
        }

        /// <summary>The chooser draws its rows in the game's pixel face, and a glyph it lacks draws nothing
        /// (the first in-game look lost the dash and the dot between price and count). The section's words,
        /// the row formats and the names of the goods it lists use only glyphs the face has. The currency
        /// sign is the game's own money format, shared with every price, and is left to that format.</summary>
        [Test]
        public void TheChoosersWords_UseOnlyGlyphsThePixelFaceDraws()
        {
            var face = Resources.Load<Font>(HarbourType.ResourceKey);
            Assert.IsNotNull(face, HarbourType.ResourceKey);
            var glyphs = new HashSet<char>(face.characterInfo.Select(c => (char)c.index));
            void Check(string words, string what)
            {
                string bare = System.Text.RegularExpressions.Regex.Replace(words ?? "", "\\{\\d\\}", "")
                                     .Replace(MoneyFormat.Currency, "");
                foreach (char ch in bare)
                    Assert.IsTrue(glyphs.Contains(ch), what + ": '" + ch + "' (U+" + ((int)ch).ToString("X4") +
                                                       ") is not in the chooser's face, so it draws nothing: \"" + words + "\"");
            }
            Check(ShelfGood.ChoiceLabelFormat, "the row format");
            Check(ShelfGood.ChoiceSoldOutFormat, "the sold-out row format");
            var supplies = AssetDatabase.FindAssets("t:SupplyDef")
                .Select(g => AssetDatabase.LoadAssetAtPath<SupplyDef>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(s => s != null).ToList();
            foreach (ShopShelfSlotsDef def in _all)
                foreach (ShopShelfSection section in def.Sections)
                {
                    Check(section.VerbLabel, section.Id + "'s words");
                    foreach (string slotId in section.SlotIds)
                    {
                        string listing = def.Slots.Single(s => s.Id == slotId).ListingId;
                        foreach (SupplyDef s in supplies.Where(s => s.Id == listing))
                            Check(s.DisplayName, listing + "'s name");
                        foreach (BaitDef b in Baits().Where(b => b.Id == listing))
                            Check(b.DisplayName, listing + "'s name");
                    }
                }
        }

        /// <summary>Every good a store plans but does not shelve yet has its own picture too: none shared with
        /// another planned good or with a good on the shelves, none planned twice or already shelved, and every
        /// planned id the catalog already authors (gear., supply., bait.) is a real listing.</summary>
        [Test]
        public void EveryPlannedGood_HasItsOwnPicture_SharedWithNoOther()
        {
            var plans = AssetDatabase.FindAssets("t:StoreItemPicturesDef")
                .Select(g => AssetDatabase.LoadAssetAtPath<StoreItemPicturesDef>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(p => p != null).ToList();
            Assert.That(plans.Select(p => p.ShopId), Has.Member(_stPeters.ShopId), "the St Peters store's planned goods are missing");
            var listings = new HashSet<string>(AssetDatabase.FindAssets("t:ScriptableObject", new[] { "Assets/_Project/Data/Resources/Catalog" })
                .Select(g => AssetDatabase.LoadAssetAtPath<ScriptableObject>(AssetDatabase.GUIDToAssetPath(g)))
                .OfType<ICatalogListing>().Select(l => l.ListingId));
            Assert.That(listings, Has.Member("gear.rod"), "the catalog sweep found nothing: the check would pass on nothing");
            foreach (StoreItemPicturesDef plan in plans)
            {
                Assert.That(plan.Id, Does.Match("^storepictures\\.[a-z0-9_]+$"), plan.name);
                ShopShelfSlotsDef shelves = _all.SingleOrDefault(d => d.ShopId == plan.ShopId);
                Assert.IsNotNull(shelves, plan.Id + " plans for " + plan.ShopId + ", which has no shelves");
                var byTexture = new Dictionary<Texture2D, string>();
                foreach (ShopShelfSlot slot in shelves.Slots.Where(s => s?.Goods != null && !string.IsNullOrEmpty(s.ListingId)))
                    byTexture[slot.Goods.texture] = slot.ListingId;
                var ids = new HashSet<string>();
                foreach (StoreItemPicture item in plan.Items)
                {
                    Assert.IsTrue(ids.Add(item.ItemId), plan.Id + " plans " + item.ItemId + " twice");
                    Assert.IsFalse(shelves.Slots.Any(s => s.ListingId == item.ItemId), item.ItemId + " is shelved already; its slot carries its picture");
                    Assert.IsNotNull(item.Picture, item.ItemId + " has no picture");
                    Texture2D tex = item.Picture.texture;
                    Assert.IsFalse(byTexture.ContainsKey(tex), item.ItemId + " shares its picture (" + AssetDatabase.GetAssetPath(tex) + ") with "
                                                               + (byTexture.TryGetValue(tex, out string other) ? other : ""));
                    byTexture[tex] = item.ItemId;
                    if (item.ItemId.StartsWith("gear.") || item.ItemId.StartsWith("bait.") || item.ItemId.StartsWith("supply."))
                        Assert.That(listings, Has.Member(item.ItemId), item.ItemId + " names no catalog listing");
                }
            }
        }

        private static List<BaitDef> Baits() => AssetDatabase.FindAssets("t:BaitDef")
            .Select(g => AssetDatabase.LoadAssetAtPath<BaitDef>(AssetDatabase.GUIDToAssetPath(g)))
            .Where(b => b != null).ToList();

        /// <summary>The fishing section (owner 2026-11-25: baits for what the store sells gear for and what each
        /// season's fish take): the four hook baits, each its own listed good with its own shelf line, none in the
        /// paper bag; and every one is taken by a fish a St Peters rod catches in some season, read from the data.</summary>
        [Test]
        public void TheStPetersFishingSection_SellsHookBaitsThatStPetersRodFishTake()
        {
            ShopShelfSection fishing = _stPeters.Sections.Single(s => s.Id == "section.fishing");
            Assert.That(fishing.SlotIds, Is.EqualTo(new[] { "slot.bait", "slot.sea_worm", "slot.squid_strip", "slot.shucked_clam" }));
            IslandStoreShelvesDef shelves = _shelves.Single(s => s.ShopId == _stPeters.ShopId);
            var rodFish = AssetDatabase.FindAssets("t:FishSpeciesDef")
                .Select(g => AssetDatabase.LoadAssetAtPath<HiddenHarbours.Fishing.FishSpeciesDef>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(f => f != null && f.RegionIds != null && f.RegionIds.Contains("region.st_peters")
                            && f.GearAllowed(HiddenHarbours.Fishing.Gear.Handline) && f.Seasons != 0)
                .Select(f => f.Id).ToList();
            Assert.That(rodFish, Is.Not.Empty, "no St Peters fish takes a rod: the check below would pass on nothing");
            var lines = new HashSet<string>();
            foreach (string slotId in fishing.SlotIds)
            {
                ShopShelfSlot slot = _stPeters.Slots.Single(s => s.Id == slotId);
                Assert.IsFalse(slot.IntoBag, slotId + ": bait goes to the tackle box, not the paper bag");
                Assert.IsTrue(lines.Add(slot.LineId), slotId + " shares its shelf line; each bait runs out on its own");
                IslandShelfLine line = shelves.Lines.Single(l => l.Id == slot.LineId);
                Assert.IsTrue(line.SoldAtShelf, slot.LineId + " is not sold at the shelf");
                Assert.That(line.CounterListingIds, Is.EqualTo(new[] { slot.ListingId }), slot.LineId + " counts another listing");
                BaitDef bait = Baits().Single(b => b.Id == slot.ListingId);
                Assert.IsTrue(bait.Catalog.Listed, bait.Id + " is on the shelf but not listed, so the till cannot sell it");
                Assert.That(bait.FavorsSpeciesIds.Intersect(rodFish), Is.Not.Empty,
                            bait.Id + " is taken by no fish a St Peters rod catches");
            }
        }
    }
}
