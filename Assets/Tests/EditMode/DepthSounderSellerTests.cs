using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.App.Editor;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;
using HiddenHarbours.Economy;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>WHO SELLS THE DEPTH SOUNDER.</b> Owner ruling 11-23: the St Peters island store sells basic fishing
    /// gear and no boat hardware; boat hardware belongs to the mainland town. The sounder's offer therefore names
    /// one seller, the Nine Mile Creek boat/gear wholesaler, at its unchanged price, exactly as the Loran did
    /// before it. No St Peters seller offers it: not in the catalog, and not on any vendor in the committed
    /// StPeters.unity. The wholesaler's counter in the committed NineMileCreek.unity holds the one
    /// <see cref="InstrumentShop"/> that stocks it, selling as the wholesaler, paying into the counter's wallet.
    /// Ownership is per hull and names no seller, so a save that already owns a sounder keeps it.
    /// </summary>
    public class DepthSounderSellerTests
    {
        const string OfferPath = "Assets/_Project/Data/Resources/Catalog/Instruments/DepthSounderOffer.asset";
        const string StPetersPath = StPetersLayerRefreshTests.ScenePath;
        const string NineMileCreekPath = StoreWiringPatch.NineMileCreekScenePath;
        const string Wholesaler = NmcWholesalerKeepers.BoatGearSellerId;
        const string Skiff = "boat.skiff";

        readonly List<Object> _spawned = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (Object o in _spawned) if (o != null) Object.DestroyImmediate(o);
            _spawned.Clear();
        }

        static InstrumentOffer Offer()
        {
            var offer = AssetDatabase.LoadAssetAtPath<InstrumentOffer>(OfferPath);
            Assert.IsNotNull(offer, OfferPath);
            return offer;
        }

        static string OfferGuid() => AssetDatabase.AssetPathToGUID(OfferPath);

        /// <summary>Every seller id a vendor in the committed St Peters scene sells as, and every seller the St
        /// Peters store wiring names.</summary>
        static HashSet<string> StPetersSellers()
        {
            var ids = new HashSet<string>(StoreWiringPatch.StPeters.Sellers.Select(s => s.SellerId));
            var t = new LayerRefreshSceneText(File.ReadAllText(StPetersPath));
            foreach (long id in t.Order)
            {
                if (t.ClassOf(id) != 114 || t.IsStripped(id)) continue;
                string seller = t.Field(id, "_sellerId");
                if (!string.IsNullOrEmpty(seller)) ids.Add(seller);
            }
            return ids;
        }

        [Test]
        public void TheOffer_NamesOnlyTheBoatGearWholesaler_Listed_WithItsIdUnchanged()
        {
            InstrumentOffer offer = Offer();
            Assert.AreEqual(BoatEquipment.DepthSounderId, offer.Id, "the id the helm fit and every save read");
            CollectionAssert.AreEqual(new[] { Wholesaler }, offer.Catalog.Sellers, "the boat/gear wholesaler is the sounder's only seller");
            Assert.IsTrue(offer.Catalog.Listed, "the sounder stays buyable");
            Assert.AreEqual(CatalogSection.Gear, offer.Catalog.Section, "a counter's book opens on its everyday shelf");
            Assert.AreEqual(1, CatalogSource.For<InstrumentOffer>(Wholesaler).Count(o => o.Id == BoatEquipment.DepthSounderId),
                            "the wholesaler's book lists the sounder once");
        }

        [Test]
        public void NoStPetersSeller_OffersTheSounder_InTheCatalogOrTheScene()
        {
            HashSet<string> sellers = StPetersSellers();
            Assert.That(sellers, Does.Contain("seller.leblancs"), "the guard would pass on a scene with no island store");
            foreach (string seller in sellers)
                Assert.IsFalse(CatalogSource.For<InstrumentOffer>(seller).Any(o => o.Id == BoatEquipment.DepthSounderId),
                               $"{seller} sells at St Peters and still lists the sounder");
            StringAssert.DoesNotContain(OfferGuid(), File.ReadAllText(StPetersPath), "something in StPeters.unity still stocks the sounder");
        }

        [Test]
        public void TheWholesalersCounter_HoldsTheOneSounderShop_SellingAsTheWholesaler_IntoTheCountersWallet()
        {
            var t = new LayerRefreshSceneText(File.ReadAllText(NineMileCreekPath));
            string guid = OfferGuid();
            List<long> stocking = t.Order.Where(id => t.ClassOf(id) == 114 && !t.IsStripped(id) && t.Text(id).Contains(guid)).ToList();
            Assert.AreEqual(1, stocking.Count, "NineMileCreek.unity holds one vendor stocking the sounder");
            long shop = stocking[0];
            long counter = t.RootGameObject(NmcWholesalerKeepers.BoatGearCounterRoot);
            Assert.AreEqual(counter, t.Ref(shop, "m_GameObject"), "the sounder shop stands on the boat/gear wholesaler's counter");
            StringAssert.EndsWith("HiddenHarbours.Economy.InstrumentShop", t.Field(shop, "m_EditorClassIdentifier"));
            Assert.AreEqual(Wholesaler, t.Field(shop, "_sellerId"));
            StringAssert.Contains(guid, t.Field(shop, "_offer"));

            string loranGuid = AssetDatabase.AssetPathToGUID("Assets/_Project/Data/Resources/Catalog/Instruments/LoranOffer.asset");
            long loran = t.Order.Single(id => t.ClassOf(id) == 114 && t.Ref(id, "m_GameObject") == counter && t.Text(id).Contains(loranGuid));
            Assert.AreEqual(t.Field(loran, "_walletProvider"), t.Field(shop, "_walletProvider"), "the sounder pays where the Loran pays");
        }

        [Test]
        public void ASaveThatOwnsASounder_KeepsIt_AndTheWholesalerWillNotSellThatHullASecond()
        {
            SaveData save = SaveMigration.NewGame();
            save.ActiveHullId = Skiff;
            save.OwnedBoats.Add(Skiff);
            InstrumentLocker.Add(save, Skiff, BoatEquipment.DepthSounderId);
            Assert.IsTrue(InstrumentLocker.Owns(save, Skiff, BoatEquipment.DepthSounderId), "ownership is the hull and the id, no seller");

            var go = new GameObject("NmcBoatGearWholesalerCounter");
            _spawned.Add(go);
            InstrumentShop shop = go.AddComponent<InstrumentShop>();
            var arms = new BuyCatalog.SellerArms(null, null, null, null, null, shop, null);
            var rows = new List<BuyRow>();
            BuyCatalog.Build(Wholesaler, arms, Offer().Price * 2, save, null, rows);
            BuyRow row = rows.Single(r => r.Id == BoatEquipment.DepthSounderId);
            Assert.AreEqual(Offer().Price, row.Quote.Price, "the row quotes the asset's price");
            Assert.IsFalse(row.Quote.CanBuy, "the hull already carries one");

            save.ActiveHullId = "boat.punt";
            save.OwnedBoats.Add("boat.punt");
            rows.Clear();
            BuyCatalog.Build(Wholesaler, arms, Offer().Price, save, null, rows);
            Assert.IsTrue(rows.Single(r => r.Id == BoatEquipment.DepthSounderId).Quote.CanBuy, "another hull can still have one fitted");
        }
    }
}
