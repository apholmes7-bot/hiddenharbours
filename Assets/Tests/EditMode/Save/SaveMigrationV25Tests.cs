using System.Collections.Generic;
using NUnit.Framework;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// v24 -> v25: THE PLAYER'S STORAGE CONTENTS (owner 2026-11-17, storage with memory). Pins the three
    /// obligations of a schema bump: an older save survives and starts with no stored containers (nothing
    /// invented), a missing or damaged list is repaired, and full rows come back from the real JSON exactly.
    /// </summary>
    public class SaveMigrationV25Tests
    {
        const string Rack = "storage.stpeters.ginny_rod_rack";
        const string Locker = "storage.stpeters.ginny_gear_locker";

        [Test]
        public void TheSchemaIsAtLeast25()
        {
            Assert.GreaterOrEqual(SaveMigration.CurrentVersion, 25);
        }

        [Test]
        public void AV24Save_LoadsWithNoStoredContainers_AndNothingElseMoves()
        {
            SaveData save = SaveSerialization.FromJson(
                "{\"SchemaVersion\":24,\"GameTimeSeconds\":4321.0," +
                "\"OnboardingFlags\":[{\"Key\":\"deed.home.ginny_lot_camper\",\"Value\":true}]}");
            Assert.AreEqual(SaveMigration.CurrentVersion, save.SchemaVersion);
            Assert.IsNotNull(save.StoredContainers);
            Assert.AreEqual(0, save.StoredContainers.Count, "a v24 save had nothing stored: nothing is invented");
            Assert.AreEqual(4321d, save.GameTimeSeconds, "nothing else moved");
            Assert.IsTrue(StorageAccess.IsOpen(save, "home.ginny_lot_camper"), "the deed survives the step");
        }

        [Test]
        public void AV20Save_WalksTheWholeChain()
        {
            SaveData save = SaveSerialization.FromJson("{\"SchemaVersion\":20,\"GameTimeSeconds\":4321.0}");
            Assert.AreEqual(SaveMigration.CurrentVersion, save.SchemaVersion);
            Assert.IsNotNull(save.StoredContainers);
            Assert.IsNotNull(save.KitchenStores, "the food stores' v23 step still ran");
            Assert.IsNotNull(save.CaughtSpecies, "the catch record's v24 step still ran");
        }

        [Test]
        public void AV24ObjectWithANullList_IsRepaired()
        {
            SaveData save = SaveMigration.Migrate(new SaveData { SchemaVersion = 24, StoredContainers = null });
            Assert.AreEqual(SaveMigration.CurrentVersion, save.SchemaVersion);
            Assert.IsNotNull(save.StoredContainers);
            Assert.AreEqual(0, save.StoredContainers.Count);
        }

        [Test]
        public void ACurrentSaveWithANullList_IsRepaired()
        {
            SaveData save = SaveMigration.Migrate(new SaveData
            {
                SchemaVersion = SaveMigration.CurrentVersion, StoredContainers = null,
            });
            Assert.IsNotNull(save.StoredContainers);
        }

        [Test]
        public void ANewGameStartsWithNothingStored()
        {
            SaveData save = SaveMigration.NewGame();
            Assert.IsNotNull(save.StoredContainers);
            Assert.AreEqual(0, save.StoredContainers.Count);
        }

        [Test]
        public void FullRowsComeBackFromTheJsonExactly()
        {
            SaveData save = SaveMigration.NewGame();
            Assert.IsTrue(StoredContainers.TryStoreInstance(save, Rack, "stowage.ginny_rod_rack", "tool.rod",
                                                            "tool.st_peters.rod", 2));
            Assert.AreEqual(3, StoredContainers.StoreCounted(save, Locker, "stowage.ginny_gear_locker",
                                                             "supply.bait", 3, 6));

            SaveData back = SaveSerialization.FromJson(SaveSerialization.ToJson(save));

            Assert.AreEqual(2, back.StoredContainers.Count);
            StoredContainerDto rack = StoredContainers.Find(back, Rack);
            Assert.AreEqual("stowage.ginny_rod_rack", rack.ContainerDefId);
            Assert.AreEqual(1, rack.Items.Count);
            Assert.AreEqual("tool.rod", rack.Items[0].ItemId);
            Assert.AreEqual("tool.st_peters.rod", rack.Items[0].InstanceId);
            Assert.AreEqual(Rack, StoredContainers.ContainerOf(back, "tool.st_peters.rod"),
                            "the rod is found in the rack after the reload");
            Assert.AreEqual(3, StoredContainers.CountIn(back, Locker));
        }

        [Test]
        public void DamagedRowsAreDropped_DuplicatesFold_AndGoodOnesKept()
        {
            SaveData save = SaveMigration.NewGame();
            save.StoredContainers = new List<StoredContainerDto>
            {
                null,
                new StoredContainerDto { Key = "", Items = { new StoredItemDto { ItemId = "tool.rod", InstanceId = "a" } } },
                new StoredContainerDto
                {
                    Key = Rack, ContainerDefId = null,
                    Items =
                    {
                        null,
                        new StoredItemDto { ItemId = "", InstanceId = "b" },
                        new StoredItemDto { ItemId = "tool.rod", InstanceId = "tool.st_peters.rod" },
                    },
                },
                new StoredContainerDto { Key = Rack, Items = { new StoredItemDto { ItemId = "tool.rod", InstanceId = "rod.two" } } },
                new StoredContainerDto
                {
                    Key = Locker,
                    Items =
                    {
                        new StoredItemDto { ItemId = "supply.bait", InstanceId = null, Count = 0 },
                        new StoredItemDto { ItemId = "supply.bait", InstanceId = null, Count = -2 },
                    },
                },
                new StoredContainerDto { Key = "storage.empty", Items = null },
            };

            SaveMigration.Migrate(save);

            Assert.AreEqual(1, save.StoredContainers.Count, "keyless, emptied and empty containers are dropped");
            StoredContainerDto rack = save.StoredContainers[0];
            Assert.AreEqual(Rack, rack.Key);
            Assert.AreEqual("", rack.ContainerDefId, "a null def id is repaired, never invented");
            Assert.AreEqual(2, rack.Items.Count, "the second row for the key folds into the first: nothing lost");
            Assert.AreEqual("tool.st_peters.rod", rack.Items[0].InstanceId);
            Assert.AreEqual("rod.two", rack.Items[1].InstanceId);

            SaveMigration.Migrate(save);
            Assert.AreEqual(2, save.StoredContainers[0].Items.Count, "idempotent");
        }
    }
}
