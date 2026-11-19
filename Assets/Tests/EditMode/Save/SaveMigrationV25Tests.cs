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

        [Test]
        public void OneObjectOnePlace_ARepeatedInstanceKeepsOnlyItsFirstOccurrence()
        {
            SaveData save = SaveMigration.NewGame();
            save.StoredContainers = new List<StoredContainerDto>
            {
                new StoredContainerDto
                {
                    Key = Rack,
                    Items =
                    {
                        new StoredItemDto { ItemId = "tool.rod", InstanceId = "tool.st_peters.rod" },
                        new StoredItemDto { ItemId = "tool.rod", InstanceId = "tool.st_peters.rod" },   // twice in one
                        new StoredItemDto { ItemId = "tool.rod", InstanceId = "rod.two" },
                    },
                },
                new StoredContainerDto
                {
                    Key = Locker,
                    Items =
                    {
                        new StoredItemDto { ItemId = "tool.rod", InstanceId = "rod.two" },              // in two containers
                        new StoredItemDto { ItemId = "supply.bait", Count = 3 },
                        new StoredItemDto { ItemId = "supply.bait", Count = 2 },                        // counted lots never dedupe
                    },
                },
                new StoredContainerDto
                {
                    Key = "storage.other",
                    Items = { new StoredItemDto { ItemId = "tool.shovel", InstanceId = "tool.st_peters.rod" } },
                },
            };

            SaveMigration.Migrate(save);

            Assert.AreEqual(2, save.StoredContainers.Count, "a container left with only repeats is dropped");
            StoredContainerDto rack = StoredContainers.Find(save, Rack);
            Assert.AreEqual(2, rack.Items.Count);
            Assert.AreEqual("tool.st_peters.rod", rack.Items[0].InstanceId, "the first occurrence is kept");
            Assert.AreEqual("rod.two", rack.Items[1].InstanceId);
            StoredContainerDto locker = StoredContainers.Find(save, Locker);
            Assert.AreEqual(2, locker.Items.Count, "the rod that is already in the rack goes; both bait lots stay");
            Assert.IsFalse(locker.Items[0].IsInstance);
            Assert.AreEqual(5, locker.Items[0].Count + locker.Items[1].Count);
            Assert.IsNull(StoredContainers.Find(save, "storage.other"));
            Assert.AreEqual(Rack, StoredContainers.ContainerOf(save, "rod.two"));

            string before = Rows(save);
            SaveMigration.Migrate(save);
            Assert.AreEqual(before, Rows(save), "a second heal changes nothing");
        }

        static string Rows(SaveData save)
        {
            var sb = new System.Text.StringBuilder();
            foreach (StoredContainerDto c in save.StoredContainers)
            {
                sb.Append(c.Key).Append('|').Append(c.ContainerDefId).Append(':');
                foreach (StoredItemDto i in c.Items) sb.Append(i.ItemId).Append('/').Append(i.InstanceId).Append('x').Append(i.Count).Append(',');
                sb.Append(';');
            }
            return sb.ToString();
        }

        [Test]
        public void AV24SaveWithItsOwnRowsFilledIn_KeepsThemAllThroughTheStep()
        {
            SaveData save = SaveSerialization.FromJson(
                "{\"SchemaVersion\":24,\"GameTimeSeconds\":4321.0," +
                "\"CaughtSpecies\":[{\"SpeciesId\":\"fish.atlantic_cod\",\"DisplayName\":\"Atlantic cod\"," +
                    "\"Count\":3,\"ReleasedCount\":1,\"RecordWeightKg\":4.5}]," +
                "\"NpcGiftDays\":[{\"NpcId\":\"npc.ginny\",\"DayIndex\":2,\"Count\":1}]," +
                "\"CookJobs\":[{\"StationKey\":\"stove.ginny\",\"StationId\":\"station.stove\"," +
                    "\"RecipeId\":\"recipe.fish_cakes\",\"ReadyAtGameSeconds\":900.0}]," +
                "\"HullLoranWaypoints\":[{\"HullId\":\"boat.dory\",\"Number\":1,\"ChainId\":\"loran.9960\"," +
                    "\"SecondaryA\":0,\"SecondaryB\":1,\"TdA\":13250.5,\"TdB\":27980.25}]," +
                "\"FreezerCatch\":[{\"SpeciesId\":\"fish.atlantic_cod\",\"Quantity\":2,\"SpoilAccrued\":0.25," +
                    "\"LastSettleGameSeconds\":50.0,\"Mode\":2}]}");

            Assert.AreEqual(SaveMigration.CurrentVersion, save.SchemaVersion);

            Assert.AreEqual(1, save.CaughtSpecies.Count);
            CaughtSpeciesDto caught = save.CaughtSpecies[0];
            Assert.AreEqual("fish.atlantic_cod", caught.SpeciesId);
            Assert.AreEqual("Atlantic cod", caught.DisplayName);
            Assert.AreEqual(3, caught.Count);
            Assert.AreEqual(1, caught.ReleasedCount);
            Assert.AreEqual(4.5f, caught.RecordWeightKg);

            Assert.AreEqual(1, save.NpcGiftDays.Count);
            Assert.AreEqual("npc.ginny", save.NpcGiftDays[0].NpcId);

            Assert.AreEqual(1, save.CookJobs.Count);
            CookJobDto job = save.CookJobs[0];
            Assert.AreEqual("stove.ginny", job.StationKey);
            Assert.AreEqual("station.stove", job.StationId);
            Assert.AreEqual("recipe.fish_cakes", job.RecipeId);
            Assert.AreEqual(900d, job.ReadyAtGameSeconds);

            Assert.AreEqual(1, save.HullLoranWaypoints.Count);
            LoranWaypointDto w = save.HullLoranWaypoints[0];
            Assert.AreEqual("boat.dory", w.HullId);
            Assert.AreEqual(1, w.Number);
            Assert.AreEqual("loran.9960", w.ChainId);
            Assert.AreEqual(0, w.SecondaryA);
            Assert.AreEqual(1, w.SecondaryB);
            Assert.AreEqual(13250.5d, w.TdA);
            Assert.AreEqual(27980.25d, w.TdB);

            Assert.AreEqual(1, save.FreezerCatch.Count);
            HeldCatchDto r = save.FreezerCatch[0];
            Assert.AreEqual("fish.atlantic_cod", r.SpeciesId);
            Assert.AreEqual(2, r.Quantity);
            Assert.AreEqual(0.25f, r.SpoilAccrued);
            Assert.AreEqual(50d, r.LastSettleGameSeconds);
            Assert.AreEqual(2, r.Mode);

            Assert.AreEqual(0, save.StoredContainers.Count, "nothing is invented in the v25 rows");
        }
    }
}
