using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Economy;
using HiddenHarbours.Player;
using HiddenHarbours.World;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>THE HEADLAMP IS A PROPER PIECE OF GEAR</b>: he has it from the first morning and never buys it
    /// (owner ruling 2026-11-22), so its item entry is an unlisted <see cref="GearOffer"/> no seller names,
    /// and the Notebook's KIT page letters it by name, under the carried things, with its own icon rather
    /// than the "Other gear" placeholder. <c>GearIconTests</c> pins the icon's import settings.
    /// </summary>
    public class HeadlampGearDefTests
    {
        const string IconPath = "Assets/_Project/Art/Fishing/Icons/GearIcon_headlamp.png";
        const string OfferPath = "Assets/_Project/Data/Resources/Catalog/Gear/Headlamp.asset";

        static GearOffer Offer()
        {
            GearOffer[] hits = CatalogSource.Gear().Where(o => o != null && o.Id == PlayerGear.HeadlampId).ToArray();
            Assert.AreEqual(1, hits.Length, $"exactly one gear entry declares {PlayerGear.HeadlampId}");
            return hits[0];
        }

        [Test]
        public void TheHeadlamp_HasOneGearEntry_WithTheOwnedIdNameAndWords()
        {
            GearOffer offer = Offer();
            Assert.AreEqual(OfferPath, AssetDatabase.GetAssetPath(offer), "one entity per file, beside the other gear");
            Assert.AreEqual("gear.headlamp", offer.Id, "the id the player owns from the start");
            var config = AssetDatabase.LoadAssetAtPath<GameConfig>("Assets/_Project/Data/Config/GameConfig.asset");
            Assert.IsNotNull(config, "the shipped GameConfig");
            Assert.IsTrue(GameConfig.CarriesFromStart(config, offer.Id),
                "the shipped GameConfig carries the headlamp from the start (CarriedFromStartGearIds)");
            Assert.AreEqual("Headlamp", offer.DisplayName);
            Assert.IsFalse(string.IsNullOrWhiteSpace(offer.Flavor), "a short description");
        }

        [Test]
        public void TheHeadlamp_IsInNoShopsStock_AndNoSellersBook()
        {
            GearOffer offer = Offer();
            Assert.IsFalse(offer.Catalog.Listed, "never on a shelf");
            Assert.IsTrue(offer.Catalog.Sellers == null || offer.Catalog.Sellers.Length == 0, "no seller is named for it");
            Assert.IsFalse(offer.Catalog.IsOrphaned);

            SellerDef[] sellers = Resources.LoadAll<SellerDef>(SellerDirectory.ResourceFolder);
            Assert.IsNotEmpty(sellers, "no sellers loaded: the check would pass on nothing");
            foreach (SellerDef s in sellers)
            {
                if (s == null) continue;
                Assert.IsFalse(CatalogSource.For<GearOffer>(s.Id).Any(o => o.Id == PlayerGear.HeadlampId),
                               $"{s.Id}'s wares book sells the headlamp");
            }

            var listings = Resources.LoadAll<ScriptableObject>("Catalog").OfType<ICatalogListing>().ToArray();
            string[] shops = AssetDatabase.FindAssets("t:ShopDef", new[] { "Assets/_Project/Data" });
            Assert.IsNotEmpty(shops, "no shops found: the check would pass on nothing");
            foreach (string guid in shops)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var shop = AssetDatabase.LoadAssetAtPath<ShopDef>(path);
                Assert.IsNotNull(shop, path);
                Assert.IsFalse(shop.BuildSpec(listings).Stock.Any(line => line.ListingId == PlayerGear.HeadlampId),
                               $"{path} stocks the headlamp");
            }
        }

        [Test]
        public void TheKitPage_LettersTheHeadlampByName_UnderCarried_WithItsOwnIcon()
        {
            NotebookGearPageDef page = NotebookContentSource.GearPage();
            Assert.IsNotNull(page, "the shipped gear page must load");

            var held = new[] { new NotebookGearHolding(PlayerGear.HeadlampId, NotebookGearCount.Presence) };
            var blocks = NotebookGearPage.Blocks(held, page, NotebookKit.MinCols);
            Assert.AreEqual(1, blocks.Count, "one row for the one thing he holds");

            string[] text = blocks[0].Rows.Select(r => r.Text).ToArray();
            Assert.AreEqual("CARRIED", text[0], "listed with the watch, not under the catch-all heading");
            CollectionAssert.DoesNotContain(text, page.OtherHeading.ToUpperInvariant());
            Assert.AreEqual("Headlamp", text[1], "lettered with its Def's name");
            Assert.IsTrue(blocks[0].Rows.Skip(1).All(r => r.IconId == PlayerGear.HeadlampId), "the item lines carry its icon id");

            var lib = Resources.Load<IconLibrary>(IconRegistrar.ResourcesPath);
            Assert.IsNotNull(lib, "Resources/IconLibrary must ship");
            IconLibrary.Entry[] hits = lib.Entries.Where(e => e.Id == PlayerGear.HeadlampId).ToArray();
            Assert.AreEqual(1, hits.Length, "exactly one icon library entry for the headlamp");
            var icon = AssetDatabase.LoadAssetAtPath<Sprite>(IconPath);
            Assert.IsNotNull(icon, $"{IconPath}: missing or not a sprite");
            Assert.AreSame(icon, hits[0].Icon, "the entry is the headlamp's own icon, so the page draws a picture, not the placeholder");
        }
    }
}
