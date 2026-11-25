using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// v26 -> v27: CLOTHES, THE CLOTHING RACK'S SALES AND SKILLS (ADR 0064; one bump for the three lists, data
    /// only). Pins the obligations of a schema bump: a real v26 file loads as v27 with an empty wardrobe, a full
    /// rack and every skill at zero, and nothing else in it moves; a v27 file round-trips byte for byte; a v27
    /// save holding clothes and a skill comes back exactly; the load repair never takes away what he owns or
    /// has earned; the per-player rows are keyed so more players add rows; both save files (the full game's
    /// and the demo's) read and write the same format.
    /// </summary>
    public class SaveMigrationV27Tests
    {
        const string Local = SavePlayerKeys.Local;
        const string Guest = "player.guest.1";
        const string Rack = "clothing_rack.st_peters_general";
        const string Oilskin = "clothing.oilskin_jacket";
        const string Boots = "clothing.rubber_boots";
        const string Cap = "clothing.watch_cap";
        const string RodSkill = "skill.rod_fishing";

        /// <summary>A v26 file as main writes it today: purchases, a held paper bag, a paid special order and
        /// the older rows around them. No v27 key appears in it.</summary>
        const string V26File =
            "{\"SchemaVersion\":26,\"WorldSeed\":77,\"GameTimeSeconds\":4321.5,\"Money\":250,\"DayIndex\":3," +
            "\"OwnedBoats\":[\"boat.dory\"],\"ActiveHullId\":\"boat.dory\"," +
            "\"OnboardingFlags\":[{\"Key\":\"met_ginny\",\"Value\":true}]," +
            "\"OwnedGear\":[\"tool.rod\"],\"WornOutfitId\":\"outfit.oilskin_yellow\"," +
            "\"SupplyStock\":[{\"SupplyId\":\"supply.ice\",\"Count\":4},{\"SupplyId\":\"supply.bread\",\"Count\":2}]," +
            "\"CaughtSpecies\":[{\"SpeciesId\":\"fish.atlantic_cod\",\"DisplayName\":\"Atlantic cod\"," +
                "\"Count\":3,\"ReleasedCount\":1,\"RecordWeightKg\":4.5}]," +
            "\"StoredContainers\":[{\"Key\":\"storage.stpeters.ginny_rod_rack\"," +
                "\"ContainerDefId\":\"stowage.ginny_rod_rack\"," +
                "\"Items\":[{\"ItemId\":\"tool.rod\",\"InstanceId\":\"tool.st_peters.rod\",\"Count\":1}]," +
                "\"Catch\":[]}]," +
            "\"StorePurchases\":[{\"ShopId\":\"shop.st_peters_general\",\"ShelfLineId\":\"line.bread\"," +
                "\"Units\":2.0,\"Day\":3,\"Seconds\":4000.25}]," +
            "\"SpecialOrders\":[{\"OrderId\":\"order.seller.leblancs.1\",\"SellerId\":\"seller.leblancs\"," +
                "\"DeliverToId\":\"\",\"Lines\":[{\"ListingId\":\"specialorder.net\",\"Count\":1,\"PaidPrice\":40}]," +
                "\"PaidPrice\":40,\"OrderedAtSeconds\":4100.0,\"OrderedDay\":3,\"DueDay\":5,\"State\":0}]," +
            "\"NextSpecialOrderNumber\":2," +
            "\"ShoppingBags\":[{\"BagDefId\":\"shoppingbag.paper\",\"Held\":true,\"Region\":\"\",\"PosX\":0.0," +
                "\"PosY\":0.0,\"Level\":0,\"HullId\":\"\",\"ByTheBed\":false," +
                "\"Items\":[{\"SupplyId\":\"supply.bread\",\"Count\":2}]}]}";

        // ---- 1. an older save ---------------------------------------------------------------------------

        [Test]
        public void TheSchemaIsAtLeast27()
        {
            Assert.GreaterOrEqual(SaveMigration.CurrentVersion, 27);
        }

        [Test]
        public void ARealV26File_LoadsAsV27_WithAnEmptyWardrobe_AFullRack_AndSkillZero()
        {
            SaveData save = SaveSerialization.FromJson(V26File);

            Assert.AreEqual(SaveMigration.CurrentVersion, save.SchemaVersion);
            Assert.IsNotNull(save.PlayerClothing);
            Assert.IsNotNull(save.ClothingRackSales);
            Assert.IsNotNull(save.PlayerSkills);
            Assert.AreEqual(0, save.PlayerClothing.Count, "no garment is invented");
            Assert.AreEqual(0, save.ClothingRackSales.Count, "every rack is full");
            Assert.AreEqual(0, save.PlayerSkills.Count, "every skill is at zero");
            Assert.AreEqual(0, PlayerClothingRows.Owned(save, Local).Count);
            Assert.AreEqual(0, PlayerClothingRows.Worn(save, Local).Count);
            Assert.AreEqual(0, ClothingRackSaleRows.Sold(save, Rack, Oilskin));
            Assert.AreEqual(0, PlayerSkillRows.Experience(save, Local, RodSkill));
        }

        [Test]
        public void ARealV26File_KeepsEverythingItHeld()
        {
            SaveData save = SaveSerialization.FromJson(V26File);

            Assert.AreEqual(77, save.WorldSeed);
            Assert.AreEqual(4321.5d, save.GameTimeSeconds);
            Assert.AreEqual(250, save.Money);
            Assert.AreEqual(3, save.DayIndex);
            CollectionAssert.AreEqual(new[] { "boat.dory" }, save.OwnedBoats);
            CollectionAssert.AreEqual(new[] { "tool.rod" }, save.OwnedGear);
            Assert.AreEqual("outfit.oilskin_yellow", save.WornOutfitId, "his outfit colours are untouched");
            Assert.AreEqual(1, save.OnboardingFlags.Count);
            Assert.AreEqual(4, SupplyLocker.Owned(save, "supply.ice"));
            Assert.AreEqual(2, SupplyLocker.Owned(save, "supply.bread"));
            Assert.AreEqual(1, save.CaughtSpecies.Count);
            Assert.AreEqual("tool.st_peters.rod", save.StoredContainers[0].Items[0].InstanceId);
            Assert.AreEqual(1, save.StorePurchases.Count);
            Assert.AreEqual(2.0d, save.StorePurchases[0].Units);
            Assert.AreEqual(1, save.SpecialOrders.Count);
            Assert.AreEqual(40, save.SpecialOrders[0].PaidPrice);
            Assert.AreEqual(2, save.NextSpecialOrderNumber);
            Assert.AreEqual(1, save.ShoppingBags.Count);
            Assert.AreEqual(2, save.ShoppingBags[0].Items[0].Count);
        }

        [Test]
        public void TheStep_MovesNothingElseInTheWholeFile()
        {
            // The v26 file loaded, then written twice: once as it stands, once stamped v26 and walked through
            // the step again. With the three new lists empty in both, the two files must be the same text.
            SaveData save = SaveSerialization.FromJson(V26File);
            string asItStands = SaveSerialization.ToJson(save);

            save.SchemaVersion = 26;
            SaveMigration.Migrate(save);

            Assert.AreEqual(SaveMigration.CurrentVersion, save.SchemaVersion);
            Assert.AreEqual(asItStands, SaveSerialization.ToJson(save));
        }

        [Test]
        public void TheStep_ClearsNothingAV27FileAlreadyHolds()
        {
            SaveData save = Dressed();
            string json = SaveSerialization.ToJson(save);
            SaveData loaded = SaveSerialization.FromJson(json);
            Assert.AreEqual(1, loaded.PlayerClothing.Count, "a v27 file is not stepped again");
            Assert.AreEqual(json, SaveSerialization.ToJson(loaded));
        }

        [Test]
        public void AVersionlessSave_WalksTheWholeChain()
        {
            SaveData save = SaveSerialization.FromJson("{\"SchemaVersion\":0,\"WorldSeed\":42,\"Money\":15}");
            Assert.AreEqual(SaveMigration.CurrentVersion, save.SchemaVersion);
            Assert.AreEqual(42, save.WorldSeed);
            Assert.AreEqual(15, save.Money);
            Assert.IsNotNull(save.StorePurchases, "the v26 step still ran");
            Assert.AreEqual(0, save.PlayerClothing.Count);
            Assert.AreEqual(0, save.ClothingRackSales.Count);
            Assert.AreEqual(0, save.PlayerSkills.Count);
        }

        [Test]
        public void NullLists_AreRepairedAtCurrentVersion()
        {
            SaveData save = SaveMigration.Migrate(new SaveData
            {
                SchemaVersion = SaveMigration.CurrentVersion,
                PlayerClothing = null, ClothingRackSales = null, PlayerSkills = null,
            });
            Assert.IsNotNull(save.PlayerClothing);
            Assert.IsNotNull(save.ClothingRackSales);
            Assert.IsNotNull(save.PlayerSkills);
        }

        // ---- 2. a v27 save ------------------------------------------------------------------------------

        [Test]
        public void AV27Save_RoundTripsByteStable()
        {
            string first = SaveSerialization.ToJson(Dressed());
            string second = SaveSerialization.ToJson(SaveSerialization.FromJson(first));
            string third = SaveSerialization.ToJson(SaveSerialization.FromJson(second));
            Assert.AreEqual(first, second);
            Assert.AreEqual(second, third);
        }

        [Test]
        public void AV27SaveWithClothesAndASkill_ComesBackExactly()
        {
            SaveData loaded = SaveSerialization.FromJson(SaveSerialization.ToJson(Dressed()));

            CollectionAssert.AreEqual(new[] { Oilskin, Boots, Cap }, PlayerClothingRows.Owned(loaded, Local));
            CollectionAssert.AreEqual(new[] { Oilskin, Boots }, PlayerClothingRows.Worn(loaded, Local));
            Assert.AreEqual(1, ClothingRackSaleRows.Sold(loaded, Rack, Oilskin));
            Assert.AreEqual(2, ClothingRackSaleRows.Sold(loaded, Rack, Boots));
            Assert.AreEqual(0, ClothingRackSaleRows.Sold(loaded, Rack, Cap));
            Assert.AreEqual(57, PlayerSkillRows.Experience(loaded, Local, RodSkill));
        }

        [Test]
        public void TheNewKeys_AreInTheFile_AndNoTideWindOrWeather()
        {
            string json = SaveSerialization.ToJson(Dressed());
            foreach (string key in new[] { "\"PlayerClothing\"", "\"OwnedClothing\"", "\"WornClothing\"",
                                           "\"ClothingRackSales\"", "\"PlayerSkills\"", "\"PlayerKey\"" })
                StringAssert.Contains(key, json);
            foreach (string forbidden in new[] { "tide", "wind", "weather" })
                Assert.AreEqual(-1, json.IndexOf(forbidden, StringComparison.OrdinalIgnoreCase), forbidden);
        }

        // ---- 3. the load repair -------------------------------------------------------------------------

        [Test]
        public void Repair_AWornGarmentHeDoesNotOwn_IsOwned_NotDropped()
        {
            SaveData save = Migrated("\"PlayerClothing\":[{\"PlayerKey\":\"player.local\"," +
                                     "\"OwnedClothing\":[\"clothing.watch_cap\"],\"WornClothing\":[\"clothing.oilskin_jacket\"]}]");
            CollectionAssert.AreEqual(new[] { Cap, Oilskin }, PlayerClothingRows.Owned(save, Local));
            CollectionAssert.AreEqual(new[] { Oilskin }, PlayerClothingRows.Worn(save, Local));
        }

        [Test]
        public void Repair_EmptyIdsAndRepeats_AreDropped_AndAnEmptyKeyIsTheLocalPlayer()
        {
            SaveData save = Migrated("\"PlayerClothing\":[{\"PlayerKey\":\"\"," +
                                     "\"OwnedClothing\":[\"clothing.watch_cap\",\"\",\"clothing.watch_cap\"],\"WornClothing\":[\"\"]}]");
            Assert.AreEqual(Local, save.PlayerClothing[0].PlayerKey);
            CollectionAssert.AreEqual(new[] { Cap }, PlayerClothingRows.Owned(save, Local));
            Assert.AreEqual(0, PlayerClothingRows.Worn(save, Local).Count);
        }

        [Test]
        public void Repair_TwoRowsForOnePlayer_FoldIntoOne_LosingNothing()
        {
            SaveData save = Migrated("\"PlayerClothing\":[" +
                "{\"PlayerKey\":\"player.local\",\"OwnedClothing\":[\"clothing.watch_cap\"],\"WornClothing\":[]}," +
                "{\"PlayerKey\":\"player.guest.1\",\"OwnedClothing\":[\"clothing.overalls\"],\"WornClothing\":[]}," +
                "{\"PlayerKey\":\"player.local\",\"OwnedClothing\":[\"clothing.rubber_boots\"],\"WornClothing\":[\"clothing.rubber_boots\"]}]");
            Assert.AreEqual(2, save.PlayerClothing.Count);
            CollectionAssert.AreEqual(new[] { Cap, Boots }, PlayerClothingRows.Owned(save, Local));
            CollectionAssert.AreEqual(new[] { Boots }, PlayerClothingRows.Worn(save, Local));
            CollectionAssert.AreEqual(new[] { "clothing.overalls" }, PlayerClothingRows.Owned(save, Guest));
        }

        [Test]
        public void Repair_RackSales_DropsBadRows_Sorts_AndFolds()
        {
            SaveData save = Migrated("\"ClothingRackSales\":[" +
                "{\"RackId\":\"clothing_rack.st_peters_general\",\"ClothingId\":\"clothing.rubber_boots\",\"Units\":1,\"Day\":4}," +
                "{\"RackId\":\"\",\"ClothingId\":\"clothing.rubber_boots\",\"Units\":1,\"Day\":1}," +
                "{\"RackId\":\"clothing_rack.st_peters_general\",\"ClothingId\":\"clothing.watch_cap\",\"Units\":0,\"Day\":1}," +
                "{\"RackId\":\"clothing_rack.st_peters_general\",\"ClothingId\":\"clothing.watch_cap\",\"Units\":1,\"Day\":-1}," +
                "{\"RackId\":\"clothing_rack.st_peters_general\",\"ClothingId\":\"clothing.oilskin_jacket\",\"Units\":1,\"Day\":2}," +
                "{\"RackId\":\"clothing_rack.st_peters_general\",\"ClothingId\":\"clothing.rubber_boots\",\"Units\":2,\"Day\":4}]");
            Assert.AreEqual(2, save.ClothingRackSales.Count);
            Assert.AreEqual(Oilskin, save.ClothingRackSales[0].ClothingId, "day 2 before day 4");
            Assert.AreEqual(Boots, save.ClothingRackSales[1].ClothingId);
            Assert.AreEqual(3, save.ClothingRackSales[1].Units, "two rows of one day fold, units added");
            Assert.AreEqual(0, ClothingRackSaleRows.Sold(save, Rack, Cap));
        }

        [Test]
        public void Repair_Skills_NegativeReadsZero_RepeatsKeepTheLarger_NoIdIsDropped()
        {
            SaveData save = Migrated("\"PlayerSkills\":[" +
                "{\"PlayerKey\":\"player.local\",\"SkillId\":\"skill.rod_fishing\",\"Experience\":40}," +
                "{\"PlayerKey\":\"\",\"SkillId\":\"skill.rod_fishing\",\"Experience\":90}," +
                "{\"PlayerKey\":\"player.local\",\"SkillId\":\"\",\"Experience\":5}," +
                "{\"PlayerKey\":\"player.guest.1\",\"SkillId\":\"skill.rod_fishing\",\"Experience\":-3}]");
            Assert.AreEqual(2, save.PlayerSkills.Count);
            Assert.AreEqual(90, PlayerSkillRows.Experience(save, Local, RodSkill), "never adds two copies, never loses the larger");
            Assert.AreEqual(0, PlayerSkillRows.Experience(save, Guest, RodSkill));
        }

        [Test]
        public void Repair_IsIdempotent_OnADamagedFile()
        {
            string damaged = "{\"SchemaVersion\":27," +
                "\"PlayerClothing\":[{\"PlayerKey\":\"\",\"OwnedClothing\":[\"clothing.watch_cap\",\"\"],\"WornClothing\":[\"clothing.oilskin_jacket\"]}," +
                    "{\"PlayerKey\":\"player.local\",\"OwnedClothing\":[\"clothing.rubber_boots\"],\"WornClothing\":[]}]," +
                "\"ClothingRackSales\":[{\"RackId\":\"clothing_rack.st_peters_general\",\"ClothingId\":\"clothing.watch_cap\",\"Units\":1,\"Day\":3}," +
                    "{\"RackId\":\"clothing_rack.st_peters_general\",\"ClothingId\":\"clothing.watch_cap\",\"Units\":1,\"Day\":1}]," +
                "\"PlayerSkills\":[{\"PlayerKey\":\"\",\"SkillId\":\"skill.rod_fishing\",\"Experience\":-1}]}";
            string once = SaveSerialization.ToJson(SaveSerialization.FromJson(damaged));
            string twice = SaveSerialization.ToJson(SaveSerialization.FromJson(once));
            Assert.AreEqual(once, twice);
        }

        // ---- 4. the accessors ---------------------------------------------------------------------------

        [Test]
        public void Clothes_AreKeptPerPlayer_SoASecondPlayerIsOnlyAnotherRow()
        {
            SaveData save = SaveMigration.NewGame();
            Assert.IsTrue(PlayerClothingRows.Write(save, Local, new[] { Cap }, new[] { Cap }));
            Assert.IsTrue(PlayerClothingRows.Write(save, Guest, new[] { Boots }, Array.Empty<string>()));
            Assert.IsTrue(PlayerClothingRows.Write(save, "", new[] { Cap, Oilskin }, new[] { Oilskin }));

            Assert.AreEqual(2, save.PlayerClothing.Count, "an empty key is the local player's row, rewritten");
            CollectionAssert.AreEqual(new[] { Cap, Oilskin }, PlayerClothingRows.Owned(save, Local));
            CollectionAssert.AreEqual(new[] { Oilskin }, PlayerClothingRows.Worn(save, Local));
            CollectionAssert.AreEqual(new[] { Boots }, PlayerClothingRows.Owned(save, Guest));
        }

        [Test]
        public void Clothes_AWrite_IsWhatTheRepairWouldLeave()
        {
            SaveData save = SaveMigration.NewGame();
            PlayerClothingRows.Write(save, Local, new[] { Cap, "", Cap }, new[] { Oilskin, Oilskin });
            string written = SaveSerialization.ToJson(save);
            Assert.AreEqual(written, SaveSerialization.ToJson(SaveSerialization.FromJson(written)));
            CollectionAssert.AreEqual(new[] { Cap, Oilskin }, PlayerClothingRows.Owned(save, Local));
        }

        [Test]
        public void RackSales_AreRecordedInOrder_AndSummedOverDays()
        {
            SaveData save = SaveMigration.NewGame();
            Assert.IsTrue(ClothingRackSaleRows.Record(save, Rack, Boots, 1, 4));
            Assert.IsTrue(ClothingRackSaleRows.Record(save, Rack, Oilskin, 1, 2));
            Assert.IsTrue(ClothingRackSaleRows.Record(save, Rack, Boots, 1, 4));
            Assert.IsTrue(ClothingRackSaleRows.Record(save, Rack, Boots, 1, 5));
            Assert.IsFalse(ClothingRackSaleRows.Record(save, Rack, Boots, 0, 5));
            Assert.IsFalse(ClothingRackSaleRows.Record(save, Rack, Boots, 1, -1));
            Assert.IsFalse(ClothingRackSaleRows.Record(save, "", Boots, 1, 5));

            Assert.AreEqual(3, save.ClothingRackSales.Count);
            Assert.AreEqual(Oilskin, save.ClothingRackSales[0].ClothingId);
            Assert.AreEqual(2, save.ClothingRackSales[1].Units);
            Assert.AreEqual(3, ClothingRackSaleRows.Sold(save, Rack, Boots));
            string written = SaveSerialization.ToJson(save);
            Assert.AreEqual(written, SaveSerialization.ToJson(SaveSerialization.FromJson(written)),
                            "a recorded list is already in the repair's order");
        }

        [Test]
        public void Skills_AreKeptPerPlayerAndSkill()
        {
            SaveData save = SaveMigration.NewGame();
            Assert.IsTrue(PlayerSkillRows.SetExperience(save, Local, RodSkill, 12));
            Assert.IsTrue(PlayerSkillRows.SetExperience(save, Guest, RodSkill, 3));
            Assert.IsTrue(PlayerSkillRows.SetExperience(save, null, RodSkill, 15));
            Assert.IsTrue(PlayerSkillRows.SetExperience(save, Local, "skill.other", -4));
            Assert.IsFalse(PlayerSkillRows.SetExperience(save, Local, "", 1));

            Assert.AreEqual(3, save.PlayerSkills.Count);
            Assert.AreEqual(15, PlayerSkillRows.Experience(save, Local, RodSkill));
            Assert.AreEqual(3, PlayerSkillRows.Experience(save, Guest, RodSkill));
            Assert.AreEqual(0, PlayerSkillRows.Experience(save, Local, "skill.other"));
        }

        [Test]
        public void TheAccessors_AreNullSafe()
        {
            Assert.AreEqual(0, PlayerClothingRows.Owned(null, Local).Count);
            Assert.AreEqual(0, PlayerClothingRows.Worn(null, Local).Count);
            Assert.IsFalse(PlayerClothingRows.Write(null, Local, new[] { Cap }, null));
            Assert.AreEqual(0, ClothingRackSaleRows.Sold(null, Rack, Cap));
            Assert.IsFalse(ClothingRackSaleRows.Record(null, Rack, Cap, 1, 0));
            Assert.AreEqual(0, PlayerSkillRows.Experience(null, Local, RodSkill));
            Assert.IsFalse(PlayerSkillRows.SetExperience(null, Local, RodSkill, 1));
        }

        // ---- 5. both save files -------------------------------------------------------------------------

        [Test]
        public void TheFullGameFile_AndTheDemoFile_ReadAndWriteTheSameFormat()
        {
            // The demo keeps its own file in a "demo" folder beside the full game's, same name, same format.
            // Neither path is the player's real save: both live in a fresh temporary folder.
            string dir = Path.Combine(Path.GetTempPath(), "save-v27-" + Guid.NewGuid().ToString("N"));
            string full = Path.Combine(dir, SaveStore.DefaultFileName);
            string demo = Path.Combine(dir, "demo", SaveStore.DefaultFileName);
            try
            {
                SaveData save = Dressed();
                string expected = SaveSerialization.ToJson(save);
                foreach (string path in new[] { full, demo })
                {
                    SaveStore.Write(save, path);
                    SaveStore.Write(save, path);   // the second save replaces the first
                    SaveData loaded = SaveStore.Read(path);
                    Assert.IsNotNull(loaded, path);
                    Assert.AreEqual(expected, SaveSerialization.ToJson(loaded), path);
                }
            }
            finally
            {
                if (Directory.Exists(dir)) Directory.Delete(dir, true);
            }
        }

        [Test]
        public void AV26DemoFile_OnDisk_LoadsAsV27()
        {
            string dir = Path.Combine(Path.GetTempPath(), "save-v27-" + Guid.NewGuid().ToString("N"));
            string demo = Path.Combine(dir, "demo", SaveStore.DefaultFileName);
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(demo));
                File.WriteAllText(demo, V26File);
                SaveData loaded = SaveStore.Read(demo);
                Assert.IsNotNull(loaded);
                Assert.AreEqual(SaveMigration.CurrentVersion, loaded.SchemaVersion);
                Assert.AreEqual(0, loaded.PlayerClothing.Count);
                Assert.AreEqual(0, loaded.PlayerSkills.Count);
                Assert.AreEqual(250, loaded.Money);
            }
            finally
            {
                if (Directory.Exists(dir)) Directory.Delete(dir, true);
            }
        }

        // ---- helpers ------------------------------------------------------------------------------------

        /// <summary>A v27 game with clothes on, two rack sales and some rod experience.</summary>
        static SaveData Dressed()
        {
            SaveData save = SaveSerialization.FromJson(V26File);
            PlayerClothingRows.Write(save, Local, new[] { Oilskin, Boots, Cap }, new[] { Oilskin, Boots });
            ClothingRackSaleRows.Record(save, Rack, Oilskin, 1, 3);
            ClothingRackSaleRows.Record(save, Rack, Boots, 2, 3);
            PlayerSkillRows.SetExperience(save, Local, RodSkill, 57);
            return save;
        }

        static SaveData Migrated(string fields) =>
            SaveSerialization.FromJson("{\"SchemaVersion\":27," + fields + "}");
    }
}
