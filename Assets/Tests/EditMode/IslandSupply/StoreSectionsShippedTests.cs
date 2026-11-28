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
                    }
                }
        }

        /// <summary>Every row a shipped section can show fits the chooser's widest line whole, its number and
        /// the chooser's own reason on a greyed row included: the first look cut the sold-out bait row to
        /// "sold out (not ...". Checked with the most the shelf can hold and the good's real name and price.</summary>
        [Test]
        public void EverySectionsRow_SoldOutOrNot_FitsTheChoosersLineWhole()
        {
            var supplies = IslandSupplyTestData.LoadAll<SupplyDef>();
            var baits = IslandSupplyTestData.LoadAll<BaitDef>();
            string reason = " " + HiddenHarbours.UI.InteractChooserStrings.ReasonOpen
                            + HiddenHarbours.UI.InteractChooserLayout.ReasonFor(InteractChoiceBlock.Unavailable)
                            + HiddenHarbours.UI.InteractChooserStrings.ReasonClose;
            int widest = HiddenHarbours.UI.InteractChooserLayout.MaxRowCharacters;
            int checkedRows = 0;
            foreach (ShopShelfSlotsDef def in _all)
                foreach (ShopShelfSection section in def.Sections)
                    for (int k = 0; k < section.SlotIds.Count; k++)
                    {
                        ShopShelfSlot slot = def.Slots.Single(s => s.Id == section.SlotIds[k]);
                        Assert.IsTrue(RowNameAndPrice(slot, supplies, baits, out string name, out int price),
                                      slot.ListingId + " is neither a supply nor a bait");
                        int most = _shelves.Where(s => s.ShopId == def.ShopId).SelectMany(s => s.Lines)
                                           .Where(l => l.Id == slot.LineId).Select(l => l.MaxOnShelf).DefaultIfEmpty(99).Max();
                        string number = (k + 1).ToString(System.Globalization.CultureInfo.InvariantCulture) + "  ";
                        string open = number + string.Format(ShelfGood.ChoiceLabelFormat, name, MoneyFormat.Balance(price), most);
                        string gone = number + string.Format(ShelfGood.ChoiceSoldOutFormat, name) + reason;
                        Assert.That(open.Length, Is.LessThanOrEqualTo(widest), "cut: \"" + open + "\"");
                        Assert.That(gone.Length, Is.LessThanOrEqualTo(widest), "cut: \"" + gone + "\"");
                        checkedRows++;
                    }
            Assert.That(checkedRows, Is.GreaterThan(0), "no section rows found");
        }

        // The row's name and price as the till quotes them: a supply's own, a bait's by the lot (BuyCatalog).
        private static bool RowNameAndPrice(ShopShelfSlot slot, List<SupplyDef> supplies, List<BaitDef> baits,
                                            out string name, out int price)
        {
            SupplyDef s = supplies.FirstOrDefault(d => d.Id == slot.ListingId);
            if (s != null) { name = s.DisplayName; price = s.Price; return true; }
            BaitDef b = baits.FirstOrDefault(d => d.Id == slot.ListingId);
            if (b != null)
            {
                int lot = BaitShop.LotSizeOf(b);
                name = lot > 1 ? b.DisplayName + " x" + lot.ToString(System.Globalization.CultureInfo.InvariantCulture) : b.DisplayName;
                price = BaitShop.LotPriceOf(b);
                return true;
            }
            name = null; price = 0;
            return false;
        }
    }
}
