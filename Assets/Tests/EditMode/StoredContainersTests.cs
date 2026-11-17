using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// The v25 storage rows (<see cref="StoredContainers"/>) and who may use a store
    /// (<see cref="StorageAccess"/>, owner 2026-11-17: more storage in the camper and the shed, once the
    /// camper is his). One object is in one place; capacity is the caller's and is never passed; an emptied
    /// container leaves no row; a gated food store takes nothing and gives nothing until the deed is held.
    /// </summary>
    public class StoredContainersTests
    {
        const string Rack = "storage.stpeters.ginny_rod_rack";
        const string Camper = "storage.stpeters.camper_rod_rack";
        const string CamperDeed = "home.ginny_lot_camper";
        const string Food = "food.test_chowder";
        const float SecondsPerDay = 2400f;

        static readonly SpoilPolicy Policy = new SpoilPolicy(1f, 0f, 0f, 0.25f, 0.9f);

        sealed class FixedPerishability : IFoodPerishability
        {
            public float SpoilPerDay(string itemId) => 0.5f;
        }

        readonly List<Object> _made = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (Object o in _made) Object.DestroyImmediate(o);
            _made.Clear();
        }

        static KitchenContext At(double t) => new KitchenContext(t, SecondsPerDay, Policy, new FixedPerishability());

        static void GrantDeed(SaveData save, string homeId)
        {
            save.OnboardingFlags ??= new List<SaveFlag>();
            save.OnboardingFlags.Add(new SaveFlag(HomeDeeds.KeyFor(homeId), true));
        }

        KitchenStorageDef Freezer(string deed)
        {
            var def = ScriptableObject.CreateInstance<KitchenStorageDef>();
            def.Id = "kitchen_storage.test_freezer";
            def.Kind = KitchenStorageKind.Fridge;
            def.ColdMode = StorageMode.Frozen;
            def.CapacityItems = 40;
            def.DeedHomeId = deed;
            _made.Add(def);
            return def;
        }

        // ---- the rows -----------------------------------------------------------------------------------

        [Test]
        public void AWorldObjectGoesIn_AndComesBackOut()
        {
            SaveData save = SaveMigration.NewGame();
            Assert.IsTrue(StoredContainers.TryStoreInstance(save, Rack, "stowage.ginny_rod_rack", "tool.rod", "rod.a", 2));
            Assert.AreEqual(1, StoredContainers.CountIn(save, Rack));
            Assert.AreEqual(Rack, StoredContainers.ContainerOf(save, "rod.a"));

            Assert.IsTrue(StoredContainers.RemoveInstance(save, Rack, "rod.a"));
            Assert.AreEqual(0, StoredContainers.CountIn(save, Rack));
            Assert.IsNull(StoredContainers.Find(save, Rack), "an emptied container leaves no row");
            Assert.IsFalse(StoredContainers.RemoveInstance(save, Rack, "rod.a"), "not there twice");
        }

        [Test]
        public void OneObjectIsInOnePlace()
        {
            SaveData save = SaveMigration.NewGame();
            Assert.IsTrue(StoredContainers.TryStoreInstance(save, Rack, "stowage.ginny_rod_rack", "tool.rod", "rod.a", 2));
            Assert.IsTrue(StoredContainers.TryStoreInstance(save, Camper, "stowage.camper_rod_rack", "tool.rod", "rod.a", 4));

            Assert.AreEqual(Camper, StoredContainers.ContainerOf(save, "rod.a"));
            Assert.AreEqual(0, StoredContainers.CountIn(save, Rack), "moved, never copied");
            Assert.AreEqual(1, save.StoredContainers.Count);

            Assert.IsTrue(StoredContainers.TryStoreInstance(save, Camper, "stowage.camper_rod_rack", "tool.rod", "rod.a", 4),
                          "already there is a success");
            Assert.AreEqual(1, StoredContainers.CountIn(save, Camper), "and changes nothing");
        }

        [Test]
        public void AFullContainerRefuses_AndNothingMoves()
        {
            SaveData save = SaveMigration.NewGame();
            Assert.IsTrue(StoredContainers.TryStoreInstance(save, Rack, "stowage.ginny_rod_rack", "tool.rod", "rod.a", 2));
            Assert.IsTrue(StoredContainers.TryStoreInstance(save, Rack, "stowage.ginny_rod_rack", "tool.rod", "rod.b", 2));
            Assert.IsTrue(StoredContainers.TryStoreInstance(save, Camper, "stowage.camper_rod_rack", "tool.rod", "rod.c", 4));

            Assert.IsFalse(StoredContainers.TryStoreInstance(save, Rack, "stowage.ginny_rod_rack", "tool.rod", "rod.c", 2));
            Assert.AreEqual(Camper, StoredContainers.ContainerOf(save, "rod.c"), "a refused store leaves it where it was");
            Assert.AreEqual(2, StoredContainers.CountIn(save, Rack));
        }

        [Test]
        public void CountedLotsStack_NeverPastCapacity_AndComeBackOut()
        {
            SaveData save = SaveMigration.NewGame();
            const string Locker = "storage.stpeters.ginny_gear_locker";
            Assert.AreEqual(4, StoredContainers.StoreCounted(save, Locker, "stowage.ginny_gear_locker", "supply.bait", 4, 6));
            Assert.AreEqual(2, StoredContainers.StoreCounted(save, Locker, "stowage.ginny_gear_locker", "supply.bait", 5, 6),
                            "only what fits");
            Assert.AreEqual(0, StoredContainers.StoreCounted(save, Locker, "stowage.ginny_gear_locker", "supply.ice", 1, 6));
            Assert.AreEqual(6, StoredContainers.CountIn(save, Locker));
            Assert.AreEqual(1, StoredContainers.Find(save, Locker).Items.Count, "one row per kind");

            Assert.AreEqual(6, StoredContainers.TakeCounted(save, Locker, "supply.bait", 10));
            Assert.IsNull(StoredContainers.Find(save, Locker));
        }

        [Test]
        public void BadArgumentsAreRefusals_NeverThrows()
        {
            Assert.IsFalse(StoredContainers.TryStoreInstance(null, Rack, "d", "tool.rod", "rod.a", 2));
            SaveData save = SaveMigration.NewGame();
            Assert.IsFalse(StoredContainers.TryStoreInstance(save, "", "d", "tool.rod", "rod.a", 2));
            Assert.IsFalse(StoredContainers.TryStoreInstance(save, Rack, "d", "", "rod.a", 2));
            Assert.IsFalse(StoredContainers.TryStoreInstance(save, Rack, "d", "tool.rod", "", 2));
            Assert.IsFalse(StoredContainers.TryStoreInstance(save, Rack, "d", "tool.rod", "rod.a", 0), "no room is no room");
            Assert.AreEqual(0, StoredContainers.StoreCounted(save, Rack, "d", "supply.bait", 0, 6));
            Assert.AreEqual(0, StoredContainers.CountIn(null, Rack));
            Assert.IsNull(StoredContainers.ContainerOf(null, "rod.a"));
        }

        // ---- who may use it -----------------------------------------------------------------------------

        [Test]
        public void AStoreWithNoDeedIsOpen_AGatedOneOpensWithItsDeed()
        {
            SaveData save = SaveMigration.NewGame();
            Assert.IsTrue(StorageAccess.IsOpen(save, ""), "lodging needs no deed");
            Assert.IsTrue(StorageAccess.IsOpen((SaveData)null, null));
            Assert.IsFalse(StorageAccess.IsOpen(save, CamperDeed));
            Assert.IsFalse(StorageAccess.IsOpen((SaveData)null, CamperDeed), "no save: a gated store stays shut");

            GrantDeed(save, CamperDeed);
            Assert.IsTrue(StorageAccess.IsOpen(save, CamperDeed));
        }

        [Test]
        public void TheCampersFreezerTakesNothing_UntilTheCamperIsHis()
        {
            SaveData save = SaveMigration.NewGame();
            KitchenStorageDef freezer = Freezer(CamperDeed);
            const string Key = "storage.stpeters.camper_freezer";

            Assert.AreEqual(0, KitchenStores.Store(save, Key, freezer, At(0d), Food, 3, Freshness.Landed(0d)));
            Assert.IsNull(KitchenStores.Find(save, Key), "a refused store opens no row");

            GrantDeed(save, CamperDeed);
            Assert.AreEqual(3, KitchenStores.Store(save, Key, freezer, At(0d), Food, 3, Freshness.Landed(0d)));
            Assert.AreEqual(1, KitchenStores.Take(save, Key, freezer, At(10d), Food, 1));
        }

        [Test]
        public void AFreezerHoldsFoodFrozen_AndAnUngatedOneNeedsNoDeed()
        {
            SaveData save = SaveMigration.NewGame();
            KitchenStorageDef freezer = Freezer("");
            const string Key = "storage.test.freezer";
            Assert.AreEqual(2, KitchenStores.Store(save, Key, freezer, At(0d), Food, 2, Freshness.Landed(0d)));
            KitchenStorageStateDto store = KitchenStores.Find(save, Key);
            Assert.IsNotNull(store);
            Assert.AreEqual((int)StorageMode.Frozen, store.Stacks[0].Mode, "a powered freezer holds it Frozen");
        }
    }
}
