using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;
using HiddenHarbours.Economy;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>WHO SELLS THE LORAN.</b> The receiver's offer names one seller, the Nine Mile Creek boat/gear
    /// wholesaler (<c>seller.nmc_boat_gear_wholesaler</c>, owner ruling 11-16), at the approved price; the
    /// St Peters placeholder counter no longer stocks it. It is bought through the same
    /// <see cref="InstrumentShop"/> seam as the sounder: the book quotes the offer's own price, a buy charges
    /// exactly that, records the unit against the hull the player is aboard in the save's instrument locker,
    /// and raises <see cref="InstrumentPurchased"/>. Owning it for a hull is what lights the receiver on a
    /// helm with room for it, and nothing else does. The purchase checks read the price from the shipped
    /// asset; the approved figure itself is pinned once, in <c>WholesalerStockTests</c>.
    ///
    /// <para>The wholesaler has no keeper or counter vendor in a scene yet, so the offer is on file but
    /// unlisted; the row tests hand the book the offer directly rather than sweeping a shelf.</para>
    /// </summary>
    public class LoranSellerTests
    {
        private const string OfferPath = "Assets/_Project/Data/Resources/Catalog/Instruments/LoranOffer.asset";
        private const string CapeHelmPath = "Assets/_Project/Data/Boats/Helms/CapeIslanderHelm.asset";
        private const string BoatGearWholesaler = "seller.nmc_boat_gear_wholesaler";
        private const string Cape = "boat.cape_islander";

        private sealed class FakeWallet : IWallet
        {
            public FakeWallet(int starting) { Money = starting; }
            public int Money { get; private set; }
            public int Debits { get; private set; }
            public void Add(int amount) => Money += amount;
            public bool TrySpend(int amount)
            {
                if (amount < 0 || amount > Money) return false;
                Money -= amount;
                Debits++;
                return true;
            }
        }

        private readonly List<Object> _spawned = new();
        private readonly List<InstrumentPurchased> _events = new();
        private void OnPurchased(InstrumentPurchased e) => _events.Add(e);

        [SetUp]
        public void SetUp()
        {
            _events.Clear();
            EventBus.Clear<InstrumentPurchased>();
            EventBus.Subscribe<InstrumentPurchased>(OnPurchased);
        }

        [TearDown]
        public void TearDown()
        {
            EventBus.Unsubscribe<InstrumentPurchased>(OnPurchased);
            EventBus.Clear<InstrumentPurchased>();
            foreach (var o in _spawned) if (o != null) Object.DestroyImmediate(o);
            _spawned.Clear();
        }

        private static InstrumentOffer Offer()
        {
            var offer = AssetDatabase.LoadAssetAtPath<InstrumentOffer>(OfferPath);
            Assert.IsNotNull(offer, OfferPath);
            return offer;
        }

        private InstrumentShop MakeShop()
        {
            var go = new GameObject("BoatGearWholesaleCounter");
            _spawned.Add(go);
            return go.AddComponent<InstrumentShop>();
        }

        private static SaveData Aboard(string hullId)
        {
            SaveData save = SaveMigration.NewGame();
            save.ActiveHullId = hullId;
            if (!string.IsNullOrEmpty(hullId)) save.OwnedBoats.Add(hullId);
            return save;
        }

        private static HelmFit FitFor(HelmConsoleDef console, SaveData save, string hullId)
            => BoatEquipment.EffectiveFit(console, InstrumentLocker.OwnedFor(save, hullId, new List<string>()));

        // ---- the offer, as data ----------------------------------------------------------------------

        [Test]
        public void TheOffer_IsTheLoranInstrument_NamingOnlyTheBoatGearWholesaler_OnTheEverydayShelf()
        {
            InstrumentOffer offer = Offer();
            Assert.AreEqual(BoatEquipment.LoranId, offer.Id, "the offer's id is the one the helm fit reads");
            Assert.Greater(offer.Price, 0, "a priced offer: the price lives on the asset");
            CollectionAssert.AreEqual(new[] { BoatGearWholesaler }, offer.Catalog.Sellers,
                                      "the boat/gear wholesaler is the receiver's only seller");
            Assert.AreEqual(CatalogSection.Gear, offer.Catalog.Section,
                            "a counter's book opens on its everyday shelf; an instrument on another shelf would never be seen");
        }

        [Test]
        public void TheIslandCounter_NoLongerSellsIt()
        {
            Assert.IsFalse(CatalogSource.For<InstrumentOffer>("seller.leblancs").Any(o => o.Id == BoatEquipment.LoranId),
                           "the St Peters placeholder still lists the Loran");
            foreach (string other in new[] { "seller.nmc_chandlery", "seller.hectors_barrel", "seller.nmc_yard" })
                Assert.IsFalse(CatalogSource.For<InstrumentOffer>(other).Any(o => o.Id == BoatEquipment.LoranId),
                               $"{other} is not the boat/gear wholesaler");
        }

        // ---- the book's row ---------------------------------------------------------------------------

        private List<BuyRow> RowsAtTheWholesaler(InstrumentShop shop, int money, SaveData save)
        {
            var arms = new BuyCatalog.SellerArms(null, null, null, null, null, shop, null);
            var stock = new BuyCatalog.CatalogStock(null, null, null, null, null, new[] { Offer() }, null);
            var rows = new List<BuyRow>();
            BuyCatalog.BuildFrom(arms, stock, money, save, null, rows);
            return rows;
        }

        [Test]
        public void TheRow_QuotesTheAssetsPrice_AndGatesOnMoneyAndABoat()
        {
            InstrumentOffer offer = Offer();
            InstrumentShop shop = MakeShop();

            BuyRow rich = RowsAtTheWholesaler(shop, offer.Price, Aboard(Cape)).Single(r => r.Id == BoatEquipment.LoranId);
            Assert.AreEqual(BuyRowKind.Instrument, rich.Quote.Kind);
            Assert.AreEqual(offer.Price, rich.Quote.Price, "the row quotes the price on the asset, not a number in code");
            Assert.IsTrue(rich.Quote.CanBuy, "exactly the price buys it");
            Assert.AreSame(shop, rich.Vendor, "Confirm goes to the counter's instrument seam");

            BuyRow poor = RowsAtTheWholesaler(shop, offer.Price - 1, Aboard(Cape)).Single(r => r.Id == BoatEquipment.LoranId);
            Assert.IsFalse(poor.Quote.CanBuy, "one short does not");

            BuyRow ashore = RowsAtTheWholesaler(shop, offer.Price * 2, Aboard("")).Single(r => r.Id == BoatEquipment.LoranId);
            Assert.IsFalse(ashore.Quote.CanBuy, "with no boat there is nothing to bolt it into");
        }

        // ---- the purchase -----------------------------------------------------------------------------

        [Test]
        public void Confirm_ChargesThePriceOnce_FitsItToTheBoatAboard_AndRaisesTheCoreEvent()
        {
            InstrumentOffer offer = Offer();
            InstrumentShop shop = MakeShop();
            var wallet = new FakeWallet(offer.Price + 25);
            SaveData save = Aboard(Cape);

            Assert.IsTrue(shop.TryBuy(offer, wallet, save));
            Assert.AreEqual(25, wallet.Money, "the asset's price came out of the purse");
            Assert.AreEqual(1, wallet.Debits, "once");
            Assert.IsTrue(InstrumentLocker.Owns(save, Cape, BoatEquipment.LoranId));
            Assert.AreEqual(1, _events.Count);
            Assert.AreEqual(BoatEquipment.LoranId, _events[0].InstrumentId);
            Assert.AreEqual(Cape, _events[0].HullId);
            Assert.AreEqual(offer.Price, _events[0].PricePaid);

            Assert.IsFalse(shop.TryBuy(offer, wallet, save), "no second receiver in the same dash");
            Assert.AreEqual(1, wallet.Debits, "and no second charge");
        }

        [Test]
        public void CannotAfford_ChargesNothing_OwnsNothing_SaysNothing()
        {
            InstrumentOffer offer = Offer();
            InstrumentShop shop = MakeShop();
            var wallet = new FakeWallet(offer.Price - 1);
            SaveData save = Aboard(Cape);

            Assert.IsFalse(shop.TryBuy(offer, wallet, save));
            Assert.AreEqual(offer.Price - 1, wallet.Money);
            Assert.IsFalse(InstrumentLocker.Owns(save, Cape, BoatEquipment.LoranId));
            Assert.IsEmpty(_events);
        }

        // ---- owning it is what lights it --------------------------------------------------------------

        [Test]
        public void OwningIt_IsWhatLightsTheReceiver_OnTheCapeHelm()
        {
            var cape = AssetDatabase.LoadAssetAtPath<HelmConsoleDef>(CapeHelmPath);
            Assert.IsNotNull(cape, CapeHelmPath);
            Assert.IsFalse(cape.DefaultLoran, "the Cape does not ship with one: it has to be bought");

            InstrumentOffer offer = Offer();
            InstrumentShop shop = MakeShop();
            var wallet = new FakeWallet(offer.Price);
            SaveData save = Aboard(Cape);

            Assert.IsFalse(FitFor(cape, save, Cape).Loran, "not bought, not lit");
            Assert.IsTrue(shop.TryBuy(offer, wallet, save));
            Assert.IsTrue(FitFor(cape, save, Cape).Loran, "bought for this hull, lit on this helm");
            Assert.IsFalse(FitFor(cape, save, "boat.punt").Loran, "and only on the hull it was fitted to");
        }

        [Test]
        public void OwningIt_DoesNothing_OnAHelmWithNoRoomForIt()
        {
            var bare = ScriptableObject.CreateInstance<HelmConsoleDef>();
            _spawned.Add(bare);
            Assert.IsFalse(bare.SupportsLoran);

            SaveData save = Aboard("boat.skiff");
            InstrumentLocker.Add(save, "boat.skiff", BoatEquipment.LoranId);
            Assert.IsFalse(FitFor(bare, save, "boat.skiff").Loran, "money spent on a dash with no cutout lights nothing");
        }

        [Test]
        public void TheFitment_RidesTheSaveAsItStands()
        {
            InstrumentOffer offer = Offer();
            InstrumentShop shop = MakeShop();
            SaveData save = Aboard(Cape);
            Assert.IsTrue(shop.TryBuy(offer, new FakeWallet(offer.Price), save));

            SaveData back = SaveSerialization.FromJson(SaveSerialization.ToJson(save));
            Assert.IsNotNull(back);
            Assert.AreEqual(SaveMigration.CurrentVersion, back.SchemaVersion,
                            "no save version step: the locker already carries any instrument id");
            Assert.IsTrue(InstrumentLocker.Owns(back, Cape, BoatEquipment.LoranId), "the receiver is still in the Cape's dash after a reload");
        }
    }
}
