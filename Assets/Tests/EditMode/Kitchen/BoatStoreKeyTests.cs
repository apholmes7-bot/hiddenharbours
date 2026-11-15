using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// A food store belongs to ONE BOAT, not to a hull (kitchens slice 1). Pins: the owned-boat id rule
    /// (<see cref="OwnedBoatId"/>: the first boat of a hull is the hull id, a second is minted
    /// <c>#2</c>), the store key that names it (<see cref="BoatInteriorKitchen.StoreKey(string, string, string)"/>),
    /// two Cape Islanders keeping separate coolers through the real save and load, and the sale rule
    /// (<see cref="KitchenStores.ClearOutBoat"/>): her food comes ashore with you and her unmelted ice goes
    /// back to the locker, while the other boat's stores are untouched.
    /// </summary>
    public class BoatStoreKeyTests
    {
        const float SecondsPerDay = 2400f;   // 100 game seconds to the game hour
        const string Hull = "boat.cape_islander";
        const string Interior = "interior.cape_islander_iso";
        const string Anchor = "cooler";
        const string Food = "food.test_chowder";

        static readonly SpoilPolicy Policy = new SpoilPolicy(1f, 0f, 0f, 0.25f, 0.9f);

        sealed class FixedPerishability : IFoodPerishability
        {
            public float SpoilPerDay(string itemId) => itemId == Food ? 0.5f : 0f;
        }

        readonly List<Object> _made = new List<Object>();
        readonly List<KitchenBoatClearedOut> _cleared = new List<KitchenBoatClearedOut>();
        System.Action<KitchenBoatClearedOut> _onCleared;

        [SetUp]
        public void SetUp()
        {
            _cleared.Clear();
            _onCleared = _cleared.Add;
            EventBus.Subscribe(_onCleared);
        }

        [TearDown]
        public void TearDown()
        {
            EventBus.Unsubscribe(_onCleared);
            foreach (Object o in _made) Object.DestroyImmediate(o);
            _made.Clear();
        }

        KitchenStorageDef Cooler()
        {
            var def = ScriptableObject.CreateInstance<KitchenStorageDef>();
            def.Id = "kitchen_storage.test_cooler";
            def.Kind = KitchenStorageKind.Cooler;
            def.CapacityItems = 12;
            def.ColdMode = StorageMode.Iced;
            def.IceCapacityUnits = 2;
            def.IceHoursPerUnit = 12f;
            _made.Add(def);
            return def;
        }

        static KitchenContext At(double t) => new KitchenContext(t, SecondsPerDay, Policy, new FixedPerishability());

        [Test]
        public void TheFirstBoatOfAHullIsTheHullId_AndASecondIsMintedApart()
        {
            string first = OwnedBoatId.FirstOf(Hull);
            Assert.AreEqual(Hull, first, "today's one-per-hull fleet lines up with every hull-keyed record");
            Assert.AreEqual(Hull, OwnedBoatId.Mint(Hull, new string[0]));
            string second = OwnedBoatId.Mint(Hull, new[] { first });
            Assert.AreEqual(Hull + "#2", second);
            Assert.AreEqual(Hull + "#3", OwnedBoatId.Mint(Hull, new[] { first, second }));
            Assert.AreEqual(Hull + "#2", OwnedBoatId.Mint(Hull, new[] { first, Hull + "#3" }), "the lowest free number");
            Assert.AreEqual(Hull, OwnedBoatId.HullOf(second));
            Assert.AreEqual(Hull, OwnedBoatId.HullOf(first));
            Assert.AreEqual("boat.odd#name", OwnedBoatId.HullOf("boat.odd#name"), "only a number is an instance");
        }

        [Test]
        public void TwoBoatsOfOneHull_HaveTwoStoreKeys_ThatNoPrefixConfuses()
        {
            string a = OwnedBoatId.FirstOf(Hull);
            string b = OwnedBoatId.Mint(Hull, new[] { a });
            string keyA = BoatInteriorKitchen.StoreKey(a, Interior, Anchor);
            string keyB = BoatInteriorKitchen.StoreKey(b, Interior, Anchor);
            Assert.AreNotEqual(keyA, keyB);
            Assert.AreEqual("fixture.boat.boat.cape_islander/interior.cape_islander_iso.cooler", keyA);
            Assert.IsFalse(keyB.StartsWith(BoatInteriorKitchen.StoresOfBoatPrefix(a), System.StringComparison.Ordinal),
                           "boat a's prefix never reaches boat a#2's stores");
            Assert.IsFalse(BoatInteriorKitchen.StoreKey("boat.cape.x", Interior, Anchor)
                               .StartsWith(BoatInteriorKitchen.StoresOfBoatPrefix("boat.cape"), System.StringComparison.Ordinal),
                           "nesting hull ids do not collide");
            Assert.AreEqual("", BoatInteriorKitchen.StoreKey("", Interior, Anchor), "no boat, no key two boats could share");
            Assert.AreEqual("", BoatInteriorKitchen.StoreKey("bad/id", Interior, Anchor));
        }

        [Test]
        public void TwoBoatsOfOneHull_KeepSeparateContents_ThroughSaveAndLoad()
        {
            KitchenStorageDef cooler = Cooler();
            SaveData save = SaveMigration.NewGame();
            string a = OwnedBoatId.FirstOf(Hull);
            string b = OwnedBoatId.Mint(Hull, new[] { a });
            string keyA = BoatInteriorKitchen.StoreKey(a, Interior, Anchor);
            string keyB = BoatInteriorKitchen.StoreKey(b, Interior, Anchor);

            Assert.AreEqual(3, KitchenStores.Store(save, keyA, cooler, At(0d), Food, 3, Freshness.Landed(0d)));
            Assert.AreEqual(1, KitchenStores.Store(save, keyB, cooler, At(0d), Food, 1, Freshness.Landed(0d)));

            SaveData back = SaveMigration.Migrate(SaveSerialization.FromJson(SaveSerialization.ToJson(save)));
            KitchenStorageStateDto readA = KitchenStores.Find(back, keyA);
            KitchenStorageStateDto readB = KitchenStores.Find(back, keyB);
            Assert.IsNotNull(readA);
            Assert.IsNotNull(readB);
            Assert.AreNotSame(readA, readB);
            Assert.AreEqual(3, KitchenStorageModel.Count(readA, Food), "her cooler is hers");
            Assert.AreEqual(1, KitchenStorageModel.Count(readB, Food), "…and the other Cape Islander's is its own");

            // Taking from one never touches the other.
            Assert.AreEqual(1, KitchenStores.Take(back, keyB, cooler, At(100d), Food, 5));
            Assert.AreEqual(3, KitchenStorageModel.Count(KitchenStores.Find(back, keyA), Food));
        }

        [Test]
        public void SellingABoat_HandsHerFoodBack_ReturnsHerWholeIce_AndLeavesTheOtherBoatAlone()
        {
            KitchenStorageDef cooler = Cooler();
            SaveData save = SaveMigration.NewGame();
            string a = OwnedBoatId.FirstOf(Hull);
            string b = OwnedBoatId.Mint(Hull, new[] { a });
            string keyA = BoatInteriorKitchen.StoreKey(a, Interior, Anchor);
            string keyB = BoatInteriorKitchen.StoreKey(b, Interior, Anchor);
            KitchenStores.Store(save, keyA, cooler, At(0d), Food, 3, Freshness.Landed(0d));
            KitchenStores.Store(save, keyB, cooler, At(0d), Food, 2, Freshness.Landed(0d));
            SupplyLocker.Add(save, cooler.IceSupplyId, 2);
            Assert.AreEqual(2, KitchenStores.PackIceFromLocker(save, keyA, cooler, At(0d), 2));
            Assert.AreEqual(0, SupplyLocker.Count(save, cooler.IceSupplyId));

            // Six hours on (two 12 h bags = 24 h of ice): one whole bag left, one melting.
            var handed = new List<KitchenStackDto>();
            int items = KitchenStores.ClearOutBoat(save, a, At(600d), KitchenStorageCatalogFind(cooler), handed);

            Assert.AreEqual(3, items, "not one item silently deleted");
            Assert.AreEqual(3, Sum(handed));
            Assert.AreEqual(600d, handed[0].LastSettleGameSeconds, "each lot carries its clock as it stood at the sale");
            Assert.AreEqual(1, SupplyLocker.Count(save, cooler.IceSupplyId), "the whole bag goes back to the locker");
            Assert.IsNull(KitchenStores.Find(save, keyA), "the sold boat's store is gone from the save");
            Assert.AreEqual(2, KitchenStorageModel.Count(KitchenStores.Find(save, keyB), Food), "the other boat keeps hers");
            Assert.AreEqual(1, _cleared.Count);
            Assert.AreEqual(a, _cleared[0].OwnedBoatId);
            Assert.AreEqual(1, _cleared[0].IceUnitsReturned);
        }

        static System.Func<string, KitchenStorageDef> KitchenStorageCatalogFind(KitchenStorageDef def)
            => id => id == def.Id ? def : null;

        static int Sum(List<KitchenStackDto> lots)
        {
            int n = 0;
            foreach (KitchenStackDto s in lots) n += s.Count;
            return n;
        }
    }
}
