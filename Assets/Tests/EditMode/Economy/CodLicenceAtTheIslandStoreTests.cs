using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Economy;

namespace HiddenHarbours.Tests.Economy
{
    /// <summary>
    /// <b>The cod licence is sold at LeBlanc's counter on St Peters</b> (owner ruling 11-25: "cod license
    /// can be sold at leblancs"). The first week happens on the island only, and the end of it needs
    /// <c>license.cod</c> in the save's owned licences, so the island has to sell it.
    ///
    /// <para>It is sold exactly the way the clam licence already is: the listing names the store's seller
    /// id in its own data, and a <see cref="LicenseVendor"/> wired with it stands on Marguerite's counter
    /// (one vendor per listed ware, the store wiring's rule). No new saved field; the price is the
    /// licence's own. The Nine Mile Creek harbourmaster keeps selling it too.</para>
    ///
    /// <para>These run the real book over the shipped catalog: <see cref="BuyCatalog.Build(string, in BuyCatalog.SellerArms, int, SaveData, ILicenseService, List{BuyRow})"/>
    /// for the counter's seller id, then <see cref="BuyCatalog.Confirm(in BuyRow)"/>, which spends through
    /// the vendor's wallet and grants through the save-backed <see cref="LicenseService"/>.</para>
    /// </summary>
    public class CodLicenceAtTheIslandStoreTests
    {
        private const string CodId = "license.cod";
        private const string ClamId = "license.clam";
        private const string LeBlancs = "seller.leblancs";
        private const string Harbourmaster = "seller.nmc_harbourmaster";

        private sealed class FakeWallet : IWallet
        {
            public FakeWallet(int starting) { Money = starting; }
            public int Money { get; private set; }
            public void Add(int amount) => Money += amount;
            public bool TrySpend(int amount)
            {
                if (amount < 0 || amount > Money) return false;
                Money -= amount;
                return true;
            }
        }

        private sealed class CountingSave : ISaveService
        {
            public CountingSave(SaveData data) { Current = data; }
            public SaveData Current { get; }
            public int Saves { get; private set; }
            public bool GetFlag(string key) => false;
            public void SetFlag(string key, bool value) { }
            public void Save() => Saves++;
        }

        private readonly List<Object> _spawned = new();
        private ISaveService _oldSave;
        private ILicenseService _oldLicenses;
        private IWallet _oldWallet;

        private SaveData _save;
        private CountingSave _saver;
        private LicenseService _licenses;

        [SetUp]
        public void SetUp()
        {
            _oldSave = GameServices.Save;
            _oldLicenses = GameServices.Licenses;
            _oldWallet = GameServices.Wallet;

            _save = new SaveData();
            _save.OwnedLicenses = new List<string>();
            _saver = new CountingSave(_save);
            GameServices.Save = _saver;

            var go = new GameObject("[LicenseService(test)]");
            _spawned.Add(go);
            _licenses = go.AddComponent<LicenseService>();
            _licenses.Register();   // EditMode fires no OnEnable for AddComponent
        }

        [TearDown]
        public void TearDown()
        {
            foreach (Object o in _spawned) if (o != null) Object.DestroyImmediate(o);
            _spawned.Clear();
            GameServices.Save = _oldSave;
            GameServices.Licenses = _oldLicenses;
            GameServices.Wallet = _oldWallet;
        }

        private static LicenseDef Shipped(string id)
        {
            LicenseDef def = CatalogSource.All<LicenseDef>().SingleOrDefault(l => l.Id == id);
            Assert.IsNotNull(def, $"the shipped catalog holds {id}");
            return def;
        }

        /// <summary>Marguerite's counter as the scene has it: one LicenseVendor selling as LeBlanc's, wired
        /// with the clam licence (the scene's own wiring), and a wallet.</summary>
        private LicenseVendor Counter(IWallet wallet)
        {
            var go = new GameObject("GeneralStoreCounter");
            _spawned.Add(go);
            var vendor = go.AddComponent<LicenseVendor>();
            Set(vendor, "_license", Shipped(ClamId));
            Set(vendor, "_sellerId", LeBlancs);
            Set(vendor, "_wallet", wallet);   // Awake does not run in EditMode
            return vendor;
        }

        private static void Set(object target, string field, object value)
        {
            FieldInfo f = target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(f, $"{target.GetType().Name}.{field}");
            f.SetValue(target, value);
        }

        private BuyRow CodRowAt(LicenseVendor counter, int money)
        {
            var arms = new BuyCatalog.SellerArms(null, null, null, null, null, null, counter);
            var rows = new List<BuyRow>();
            BuyCatalog.Build(LeBlancs, arms, money, _save, _licenses, rows);
            List<BuyRow> cod = rows.Where(r => r.Id == CodId).ToList();
            Assert.AreEqual(1, cod.Count, "LeBlanc's book shows the cod licence exactly once");
            return cod[0];
        }

        private int Held(string id) => _save.OwnedLicenses.Count(x => x == id);

        // ---- who sells it ---------------------------------------------------------------------------

        [Test]
        public void TheCodLicence_HasASellerOnStPeters_AndTheHarbourmasterKeepsSellingIt()
        {
            LicenseDef cod = Shipped(CodId);
            Assert.IsTrue(cod.Catalog.Listed, "listed, so a book shows it");
            Assert.IsTrue(cod.Catalog.IsStockedBy(LeBlancs), "LeBlanc's, on St Peters, sells it");
            Assert.IsTrue(cod.Catalog.IsStockedBy(Harbourmaster), "and the Nine Mile Creek harbourmaster still does");

            Assert.IsTrue(Shipped(ClamId).Catalog.IsStockedBy(LeBlancs),
                "the same seller id the clam licence is sold under: the same counter");
            CollectionAssert.Contains(CatalogSource.For<LicenseDef>(LeBlancs).Select(l => l.Id).ToList(), CodId,
                "her book lists it");
            Assert.AreEqual(1, cod.Catalog.Sellers.Count(s => s == LeBlancs), "named once");

            // And the committed island scene holds the vendor that sells it, on her counter.
            string guid = UnityEditor.AssetDatabase.AssetPathToGUID(UnityEditor.AssetDatabase.GetAssetPath(cod));
            string scene = System.IO.File.ReadAllText(StPetersScene).Replace("\r", "");
            int vendors = scene.Split(new[] { "\n--- " }, System.StringSplitOptions.None).Count(doc =>
                doc.Contains("HiddenHarbours.Economy.LicenseVendor") &&
                doc.Contains("_license: {fileID: 11400000, guid: " + guid + ", type: 2}") &&
                doc.Contains("_sellerId: " + LeBlancs));
            Assert.AreEqual(1, vendors, "one licence vendor in StPeters.unity sells the cod licence as LeBlanc's");
        }

        private const string StPetersScene = "Assets/_Project/Scenes/StPeters.unity";

        // ---- buying it at the counter ----------------------------------------------------------------

        [Test]
        public void BuyingItAtTheCounter_OwnsItOnce_AndDebitsItsDataPrice()
        {
            int price = Shipped(CodId).Price;
            Assert.Greater(price, 0, "the licence has a fee in data");
            var wallet = new FakeWallet(price + 7);
            LicenseVendor counter = Counter(wallet);

            BuyRow row = CodRowAt(counter, wallet.Money);
            Assert.AreEqual(BuyRowKind.License, row.Quote.Kind);
            Assert.AreEqual(price, row.Quote.Price, "the row quotes the licence's own price");
            Assert.AreSame(counter, row.Vendor, "sold through the counter's licence vendor");
            Assert.IsNull(row.Shelves, "a counter sale, not a shelf good");
            Assert.IsTrue(row.Quote.CanBuy);

            Assert.IsTrue(BuyCatalog.Confirm(row), "the counter sells it");
            Assert.AreEqual(7, wallet.Money, "exactly the data price left the purse");
            Assert.AreEqual(1, Held(CodId), "owned once, in the saved licence list");
            Assert.AreEqual(0, Held(ClamId), "and only the licence that was bought");
            Assert.IsTrue(GameServices.Licenses.IsLicensed(CodId));
            Assert.GreaterOrEqual(_saver.Saves, 1, "the grant persists");
        }

        [Test]
        public void ItCannotBeBoughtTwice()
        {
            int price = Shipped(CodId).Price;
            var wallet = new FakeWallet(price * 3);
            LicenseVendor counter = Counter(wallet);

            Assert.IsTrue(BuyCatalog.Confirm(CodRowAt(counter, wallet.Money)));
            int after = wallet.Money;

            BuyRow again = CodRowAt(counter, wallet.Money);
            Assert.IsTrue(again.Quote.Owned, "the book says he holds it");
            Assert.IsFalse(again.Quote.CanBuy);
            Assert.IsFalse(BuyCatalog.Confirm(again), "the book refuses a second sale");
            Assert.IsFalse(counter.TryBuy(Shipped(CodId)), "and so does the vendor asked directly");
            Assert.AreEqual(after, wallet.Money, "nothing more is spent");
            Assert.AreEqual(1, Held(CodId), "still held once");
        }

        [Test]
        public void TooLittleMoney_RefusesAndSpendsNothing()
        {
            int price = Shipped(CodId).Price;
            var wallet = new FakeWallet(price - 1);
            LicenseVendor counter = Counter(wallet);

            BuyRow row = CodRowAt(counter, wallet.Money);
            Assert.IsFalse(row.Quote.CanBuy, "one short is not enough");
            Assert.IsFalse(BuyCatalog.Confirm(row), "the book refuses");
            Assert.IsFalse(counter.TryBuy(Shipped(CodId)), "and so does the vendor asked directly");
            Assert.AreEqual(price - 1, wallet.Money, "not a coin spent");
            Assert.AreEqual(0, Held(CodId), "and no licence granted");
            Assert.IsFalse(GameServices.Licenses.IsLicensed(CodId));
        }
    }
}
