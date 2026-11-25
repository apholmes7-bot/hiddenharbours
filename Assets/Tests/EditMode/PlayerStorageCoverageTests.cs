using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// The edges of the player's storage that <see cref="PlayerStorageOrderTests"/>,
    /// <see cref="StoredContainersTests"/> and <see cref="SaveMigrationV25Tests"/> leave open.
    /// <para><b>The list</b> (<see cref="PlayerStorage"/>): null and unlisted arguments are no-ops, reads past
    /// either end are null, one object is one entry whichever facet lists or unlists it, a destroyed scene
    /// container reads as null (and a live one under the same id is still found), and priority is read once,
    /// when the container is listed.</para>
    /// <para><b>The rows</b> (<see cref="StoredContainers"/>): world objects and counted lots share one
    /// capacity, a partial take leaves the rest, a forget finds the object wherever it is, the instance list
    /// is appended to in the order things went in, and an emptied catch keeps a row that still holds items.
    /// Then a round trip: what a listed container keeps under its id comes back from the save's JSON.</para>
    /// Tests only: nothing here changes behaviour.
    /// </summary>
    public class PlayerStorageCoverageTests
    {
        const string Rack = "storage.stpeters.ginny_rod_rack";
        const string Locker = "storage.stpeters.ginny_gear_locker";
        const string RackDef = "stowage.ginny_rod_rack";
        const string LockerDef = "stowage.ginny_gear_locker";

        class Named : IStorageContainer
        {
            public Named(string id, int priority) { StorageId = id; StoragePriority = priority; }
            public string StorageId { get; }
            public int StoragePriority { get; set; }
        }

        sealed class NamedHold : Named, IHold
        {
            public NamedHold(string id, int priority) : base(id, priority) { }
            public int CapacityUnits => 4;
            public int UsedUnits => 0;
            public IReadOnlyList<CatchItem> Items => System.Array.Empty<CatchItem>();
            public bool TryAdd(CatchItem item) => false;
            public void Clear() { }
        }

        /// <summary>A scene container, so a destroyed one can be listed (Unity's fake null).</summary>
        private sealed class SceneContainer : MonoBehaviour, IStorageContainer
        {
            public string Id = "";
            public int Priority = PlayerStorage.PriorityFixture;
            public string StorageId => Id;
            public int StoragePriority => Priority;
        }

        readonly List<GameObject> _made = new List<GameObject>();

        [SetUp] public void SetUp() => PlayerStorage.Clear();

        [TearDown]
        public void TearDown()
        {
            PlayerStorage.Clear();
            foreach (GameObject go in _made) if (go != null) Object.DestroyImmediate(go);
            _made.Clear();
        }

        SceneContainer MakeScene(string id)
        {
            var go = new GameObject("storage-test " + id);
            _made.Add(go);
            var c = go.AddComponent<SceneContainer>();
            c.Id = id;
            return c;
        }

        // ---- the list -----------------------------------------------------------------------------------

        [Test]
        public void NullAndUnlistedArguments_AreNoOps()
        {
            PlayerStorage.Register((IHold)null);
            PlayerStorage.Register((IStorageContainer)null);
            Assert.AreEqual(0, PlayerStorage.Count, "null is never listed");

            var rack = new Named(Rack, PlayerStorage.PriorityFixture);
            PlayerStorage.Register(rack);
            PlayerStorage.Unregister((IHold)null);
            PlayerStorage.Unregister((IStorageContainer)null);
            PlayerStorage.Unregister(new Named(Locker, PlayerStorage.PriorityFixture));
            Assert.AreEqual(1, PlayerStorage.Count, "unlisting what is not listed changes nothing");
            Assert.AreSame(rack, PlayerStorage.GetContainer(0));
        }

        [Test]
        public void ReadsPastEitherEnd_AreNull()
        {
            Assert.IsNull(PlayerStorage.Get(0), "an empty list");
            Assert.IsNull(PlayerStorage.GetContainer(0));
            Assert.IsNull(PlayerStorage.Find(null));

            PlayerStorage.Register<NamedHold>(new NamedHold(StorageIds.BoatHold, PlayerStorage.PriorityBoatHold));
            Assert.IsNull(PlayerStorage.Get(-1));
            Assert.IsNull(PlayerStorage.GetContainer(-1));
            Assert.IsNull(PlayerStorage.Get(1));
            Assert.IsNull(PlayerStorage.GetContainer(1));
            Assert.IsNotNull(PlayerStorage.Get(0));
        }

        [Test]
        public void OneObjectIsOneEntry_WhicheverFacetListsOrUnlistsIt()
        {
            var hold = new NamedHold(StorageIds.BoatHold, PlayerStorage.PriorityBoatHold);
            PlayerStorage.Register<NamedHold>(hold);
            PlayerStorage.Register((IHold)hold);
            PlayerStorage.Register((IStorageContainer)hold);
            Assert.AreEqual(1, PlayerStorage.Count, "listed three ways, one entry");
            Assert.AreSame(hold, PlayerStorage.Get(0));
            Assert.AreSame(hold, PlayerStorage.GetContainer(0), "both facets answer for the one entry");

            PlayerStorage.Unregister((IStorageContainer)hold);
            Assert.AreEqual(0, PlayerStorage.Count, "unlisted through the other facet");

            PlayerStorage.Register((IHold)hold);
            PlayerStorage.Unregister<NamedHold>(hold);
            Assert.AreEqual(0, PlayerStorage.Count);
        }

        [Test]
        public void ADestroyedSceneContainer_ReadsAsNull_UntilItIsUnlisted()
        {
            SceneContainer rack = MakeScene(Rack);
            PlayerStorage.Register(rack);
            Assert.AreSame(rack, PlayerStorage.Find(Rack));

            Object.DestroyImmediate(rack.gameObject);

            Assert.AreEqual(1, PlayerStorage.Count, "still counted until it is unlisted");
            Assert.IsNull(PlayerStorage.GetContainer(0), "a reader skips a destroyed container");
            Assert.IsNull(PlayerStorage.Get(0));
            Assert.IsNull(PlayerStorage.Find(Rack));

            PlayerStorage.Unregister(rack);
            Assert.AreEqual(0, PlayerStorage.Count, "the destroyed object is still unlisted by reference");
        }

        [Test]
        public void FindSkipsADestroyedContainer_AndFindsTheLiveOneUnderTheSameId()
        {
            SceneContainer old = MakeScene(Rack);
            PlayerStorage.Register(old);
            Object.DestroyImmediate(old.gameObject);

            // A rebuilt rack under the same id (a region hop back): no live container holds the id, so this is
            // not the repeated-id case, and the reader finds the new one.
            SceneContainer rebuilt = MakeScene(Rack);
            PlayerStorage.Register(rebuilt);

            Assert.AreEqual(2, PlayerStorage.Count);
            Assert.AreSame(rebuilt, PlayerStorage.Find(Rack));
        }

        [Test]
        public void PriorityIsReadOnce_WhenTheContainerIsListed()
        {
            var late = new Named(Locker, PlayerStorage.PriorityFixture);
            var hold = new NamedHold(StorageIds.BoatHold, PlayerStorage.PriorityBoatHold);
            PlayerStorage.Register(late);
            PlayerStorage.Register<NamedHold>(hold);
            Assert.AreSame(hold, PlayerStorage.GetContainer(0));

            late.StoragePriority = 0;
            Assert.AreSame(hold, PlayerStorage.GetContainer(0), "a changed priority does not reorder a listed entry");

            PlayerStorage.Unregister(late);
            PlayerStorage.Register(late);
            Assert.AreSame(late, PlayerStorage.GetContainer(0), "listed again, it sorts by what it says now");
        }

        // ---- the rows -----------------------------------------------------------------------------------

        [Test]
        public void WorldObjectsAndCountedLots_ShareOneCapacity()
        {
            SaveData save = SaveMigration.NewGame();
            Assert.IsTrue(StoredContainers.TryStoreInstance(save, Locker, LockerDef, "tool.rod", "rod.a", 3));
            Assert.AreEqual(2, StoredContainers.StoreCounted(save, Locker, LockerDef, "supply.bait", 5, 3),
                            "the rod takes one of the three");
            Assert.IsFalse(StoredContainers.TryStoreInstance(save, Locker, LockerDef, "tool.rod", "rod.b", 3),
                           "the bait fills the rest");
            Assert.AreEqual(3, StoredContainers.CountIn(save, Locker));
            Assert.AreEqual(2, StoredContainers.Find(save, Locker).Items.Count, "one row for the rod, one for the lot");
        }

        [Test]
        public void APartialTake_LeavesTheRest_AndAnInstanceKeepsTheRowAlive()
        {
            SaveData save = SaveMigration.NewGame();
            Assert.IsTrue(StoredContainers.TryStoreInstance(save, Locker, LockerDef, "tool.rod", "rod.a", 6));
            Assert.AreEqual(4, StoredContainers.StoreCounted(save, Locker, LockerDef, "supply.bait", 4, 6));

            Assert.AreEqual(3, StoredContainers.TakeCounted(save, Locker, "supply.bait", 3));
            Assert.AreEqual(2, StoredContainers.CountIn(save, Locker), "one bait and the rod");

            Assert.AreEqual(1, StoredContainers.TakeCounted(save, Locker, "supply.bait", 5), "only what is there");
            Assert.AreEqual(0, StoredContainers.TakeCounted(save, Locker, "supply.bait", 1), "none left");
            Assert.AreEqual(0, StoredContainers.TakeCounted(save, Locker, "tool.rod", 1),
                            "a world object is never taken as a counted lot");
            Assert.IsNotNull(StoredContainers.Find(save, Locker), "the rod keeps the row");
            Assert.AreEqual(Locker, StoredContainers.ContainerOf(save, "rod.a"));

            Assert.AreEqual(0, StoredContainers.TakeCounted(save, Locker, "supply.bait", 0));
            Assert.AreEqual(0, StoredContainers.TakeCounted(save, Locker, "", 1));
            Assert.AreEqual(0, StoredContainers.TakeCounted(save, "storage.nowhere", "supply.bait", 1));
        }

        [Test]
        public void ForgetFindsTheObjectWhereverItIs_AndAnEmptiedRowGoes()
        {
            SaveData save = SaveMigration.NewGame();
            Assert.IsTrue(StoredContainers.TryStoreInstance(save, Rack, RackDef, "tool.rod", "rod.a", 2));
            Assert.IsTrue(StoredContainers.TryStoreInstance(save, Locker, LockerDef, "tool.rod", "rod.b", 6));
            Assert.AreEqual(1, StoredContainers.StoreCounted(save, Locker, LockerDef, "supply.bait", 1, 6));

            Assert.IsTrue(StoredContainers.ForgetInstance(save, "rod.a"), "lifted from the rack");
            Assert.IsNull(StoredContainers.Find(save, Rack), "the emptied rack leaves no row");
            Assert.IsNull(StoredContainers.ContainerOf(save, "rod.a"));

            Assert.IsTrue(StoredContainers.ForgetInstance(save, "rod.b"));
            Assert.IsNotNull(StoredContainers.Find(save, Locker), "the bait keeps the locker's row");
            Assert.AreEqual(1, StoredContainers.CountIn(save, Locker));

            Assert.IsFalse(StoredContainers.ForgetInstance(save, "rod.b"), "not anywhere now");
            Assert.IsFalse(StoredContainers.ForgetInstance(save, ""));
            Assert.IsFalse(StoredContainers.ForgetInstance(null, "rod.b"));
        }

        [Test]
        public void TheInstanceList_IsAppendedInTheOrderThingsWentIn_AndSkipsLots()
        {
            SaveData save = SaveMigration.NewGame();
            Assert.IsTrue(StoredContainers.TryStoreInstance(save, Locker, LockerDef, "tool.rod", "rod.b", 6));
            Assert.AreEqual(2, StoredContainers.StoreCounted(save, Locker, LockerDef, "supply.bait", 2, 6));
            Assert.IsTrue(StoredContainers.TryStoreInstance(save, Locker, LockerDef, "tool.gaff", "gaff.a", 6));

            var into = new List<string> { "already.here" };
            Assert.AreEqual(2, StoredContainers.InstancesIn(save, Locker, into));
            CollectionAssert.AreEqual(new[] { "already.here", "rod.b", "gaff.a" }, into,
                                      "appended, never cleared, in the order they went in");

            Assert.AreEqual(0, StoredContainers.InstancesIn(save, Locker, null));
            Assert.AreEqual(0, StoredContainers.InstancesIn(save, "storage.nowhere", into));
        }

        [Test]
        public void ARebuiltFixture_KeepsItsContents_AndTakesTheNewDefId()
        {
            SaveData save = SaveMigration.NewGame();
            Assert.IsTrue(StoredContainers.TryStoreInstance(save, Rack, RackDef, "tool.rod", "rod.a", 2));
            Assert.IsTrue(StoredContainers.TryStoreInstance(save, Rack, "", "tool.rod", "rod.b", 2));
            Assert.AreEqual(RackDef, StoredContainers.Find(save, Rack).ContainerDefId, "an empty def id keeps the old");

            Assert.AreEqual(0, StoredContainers.StoreCounted(save, Rack, "stowage.bigger_rack", "supply.bait", 1, 2),
                            "full: nothing goes in");
            Assert.AreEqual(1, StoredContainers.StoreCounted(save, Rack, "stowage.bigger_rack", "supply.bait", 1, 3));
            Assert.AreEqual("stowage.bigger_rack", StoredContainers.Find(save, Rack).ContainerDefId);
            Assert.AreEqual(3, StoredContainers.CountIn(save, Rack), "what was in it stays");
        }

        [Test]
        public void AnEmptiedCatch_KeepsARowThatStillHoldsItems_AndMakesNoRowFromNothing()
        {
            SaveData save = SaveMigration.NewGame();
            StoredContainers.WriteCatch(save, Locker, LockerDef, System.Array.Empty<CatchItem>());
            Assert.IsNull(StoredContainers.Find(save, Locker), "an empty catch invents no row");

            Assert.AreEqual(2, StoredContainers.StoreCounted(save, Locker, LockerDef, "supply.bait", 2, 6));
            StoredContainers.WriteCatch(save, Locker, LockerDef, null);
            Assert.IsNotNull(StoredContainers.Find(save, Locker), "the bait keeps the row");
            Assert.AreEqual(2, StoredContainers.CountIn(save, Locker));

            StoredContainers.WriteCatch(null, Locker, LockerDef, null);
            StoredContainers.WriteCatch(save, "", LockerDef, null);
            Assert.AreEqual(1, save.StoredContainers.Count, "bad arguments change nothing");
        }

        // ---- the round trip -----------------------------------------------------------------------------

        [Test]
        public void WhatAListedContainerKeepsUnderItsId_ComesBackFromTheSave()
        {
            var locker = new Named(Locker, PlayerStorage.PriorityFixture);
            var rack = new Named(Rack, PlayerStorage.PriorityFixture);
            PlayerStorage.Register(rack);
            PlayerStorage.Register(locker);

            SaveData save = SaveMigration.NewGame();
            string lockerKey = PlayerStorage.Find(Locker).StorageId;
            string rackKey = PlayerStorage.Find(Rack).StorageId;
            Assert.IsTrue(StoredContainers.TryStoreInstance(save, lockerKey, LockerDef, "tool.gaff", "gaff.a", 6));
            Assert.AreEqual(3, StoredContainers.StoreCounted(save, lockerKey, LockerDef, "supply.bait", 3, 6));
            Assert.IsTrue(StoredContainers.TryStoreInstance(save, rackKey, RackDef, "tool.rod", "rod.a", 2));

            SaveData back = SaveSerialization.FromJson(SaveSerialization.ToJson(save));

            Assert.AreEqual(SaveMigration.CurrentVersion, back.SchemaVersion);
            Assert.AreEqual(2, back.StoredContainers.Count);
            for (int i = 0; i < PlayerStorage.Count; i++)
            {
                string key = PlayerStorage.GetContainer(i).StorageId;
                Assert.AreEqual(StoredContainers.CountIn(save, key), StoredContainers.CountIn(back, key),
                                "the same count under " + key);
            }

            StoredContainerDto lockerBack = StoredContainers.Find(back, Locker);
            Assert.AreEqual(LockerDef, lockerBack.ContainerDefId);
            Assert.AreEqual(2, lockerBack.Items.Count);
            Assert.IsTrue(lockerBack.Items[0].IsInstance);
            Assert.AreEqual("gaff.a", lockerBack.Items[0].InstanceId);
            Assert.IsFalse(lockerBack.Items[1].IsInstance, "the lot stays a lot");
            Assert.AreEqual("supply.bait", lockerBack.Items[1].ItemId);
            Assert.AreEqual(3, lockerBack.Items[1].Count);
            Assert.AreEqual(Rack, StoredContainers.ContainerOf(back, "rod.a"));

            Assert.AreEqual(3, StoredContainers.TakeCounted(back, Locker, "supply.bait", 3), "the reloaded lot comes out");
            Assert.IsTrue(StoredContainers.RemoveInstance(back, Locker, "gaff.a"));
            Assert.IsNull(StoredContainers.Find(back, Locker));
        }
    }
}
