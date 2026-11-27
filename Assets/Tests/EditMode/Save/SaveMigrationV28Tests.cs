using System;
using System.IO;
using NUnit.Framework;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// v27 -> v28: THE PHOTO ALBUM (ADR 0066; one bump for one list, data only). Pins the obligations of a
    /// schema bump: a real v27 file loads as v28 with an empty album and nothing else in it moves; a v28 file
    /// round-trips byte for byte; an album comes back exactly; the load repair never invents or improves a
    /// picture; pictures are kept per player; both save files read and write the same format.
    /// </summary>
    public class SaveMigrationV28Tests
    {
        const string Local = SavePlayerKeys.Local;
        const string Guest = "player.guest.1";
        const string Seal = "wildlife.grey_seal";
        const string Gull = "wildlife.herring_gull";
        const string Region = "region.st_peters";

        /// <summary>A v27 file as the v27 build writes it: clothes, a rack sale, a skill and a fitted rod
        /// around the older rows. No v28 key appears in it.</summary>
        const string V27File =
            "{\"SchemaVersion\":27,\"WorldSeed\":77,\"GameTimeSeconds\":4321.5,\"Money\":250,\"DayIndex\":3," +
            "\"OwnedBoats\":[\"boat.dory\"],\"ActiveHullId\":\"boat.dory\"," +
            "\"OnboardingFlags\":[{\"Key\":\"met_ginny\",\"Value\":true}]," +
            "\"OwnedGear\":[\"tool.rod\"],\"WornOutfitId\":\"outfit.oilskin_yellow\"," +
            "\"SupplyStock\":[{\"SupplyId\":\"supply.ice\",\"Count\":4}]," +
            "\"PlayerClothing\":[{\"PlayerKey\":\"player.local\",\"OwnedClothing\":[\"clothing.oilskin_jacket\"]," +
                "\"WornClothing\":[\"clothing.oilskin_jacket\"]}]," +
            "\"ClothingRackSales\":[{\"RackId\":\"clothing_rack.st_peters_general\"," +
                "\"ClothingId\":\"clothing.oilskin_jacket\",\"Units\":1,\"Day\":3}]," +
            "\"PlayerSkills\":[{\"PlayerKey\":\"player.local\",\"SkillId\":\"skill.rod_fishing\",\"Experience\":57}]," +
            "\"RodFittings\":[{\"PlayerKey\":\"player.local\",\"RodKey\":\"tool.rod_coast\"," +
                "\"LineId\":\"fishing_line.heavy\",\"ReelId\":\"\"}]}";

        // ---- 1. an older save ---------------------------------------------------------------------------

        [Test]
        public void TheSchemaIsAtLeast28()
        {
            Assert.GreaterOrEqual(SaveMigration.CurrentVersion, 28);
        }

        [Test]
        public void ARealV27File_LoadsAsV28_WithAnEmptyAlbum()
        {
            SaveData save = SaveSerialization.FromJson(V27File);

            Assert.AreEqual(SaveMigration.CurrentVersion, save.SchemaVersion);
            Assert.IsNotNull(save.Photos);
            Assert.AreEqual(0, save.Photos.Count, "no picture is invented");
            Assert.AreEqual(0, PhotoAlbumRows.Count(save, Local));
            Assert.IsFalse(PhotoAlbumRows.HasQualifying(save, Local, Seal, 1, WildlifePose.Diving, 0));
        }

        [Test]
        public void ARealV27File_KeepsEverythingItHeld()
        {
            SaveData save = SaveSerialization.FromJson(V27File);

            Assert.AreEqual(77, save.WorldSeed);
            Assert.AreEqual(4321.5d, save.GameTimeSeconds);
            Assert.AreEqual(250, save.Money);
            Assert.AreEqual(3, save.DayIndex);
            CollectionAssert.AreEqual(new[] { "clothing.oilskin_jacket" }, PlayerClothingRows.Worn(save, Local));
            Assert.AreEqual(1, ClothingRackSaleRows.Sold(save, "clothing_rack.st_peters_general",
                                                         "clothing.oilskin_jacket"));
            Assert.AreEqual(57, PlayerSkillRows.Experience(save, Local, "skill.rod_fishing"));
            Assert.AreEqual("fishing_line.heavy", RodFittingRows.LineOf(save, Local, "tool.rod_coast"));
        }

        [Test]
        public void TheStep_MovesNothingElseInTheWholeFile()
        {
            SaveData save = SaveSerialization.FromJson(V27File);
            string asItStands = SaveSerialization.ToJson(save);

            save.SchemaVersion = 27;
            SaveMigration.Migrate(save);

            Assert.AreEqual(SaveMigration.CurrentVersion, save.SchemaVersion);
            Assert.AreEqual(asItStands, SaveSerialization.ToJson(save));
        }

        [Test]
        public void TheStep_ClearsNothingAV28FileAlreadyHolds()
        {
            SaveData save = WithPictures();
            string json = SaveSerialization.ToJson(save);
            SaveData loaded = SaveSerialization.FromJson(json);
            Assert.AreEqual(3, loaded.Photos.Count, "a v28 file is not stepped again");
            Assert.AreEqual(json, SaveSerialization.ToJson(loaded));
        }

        [Test]
        public void AVersionlessSave_WalksTheWholeChain()
        {
            SaveData save = SaveSerialization.FromJson("{\"SchemaVersion\":0,\"WorldSeed\":42,\"Money\":15}");
            Assert.AreEqual(SaveMigration.CurrentVersion, save.SchemaVersion);
            Assert.AreEqual(42, save.WorldSeed);
            Assert.IsNotNull(save.RodFittings, "the v27 step still ran");
            Assert.AreEqual(0, save.Photos.Count);
        }

        [Test]
        public void ANullAlbum_IsRepairedAtCurrentVersion()
        {
            SaveData save = SaveMigration.Migrate(new SaveData
            {
                SchemaVersion = SaveMigration.CurrentVersion,
                Photos = null,
            });
            Assert.IsNotNull(save.Photos);
        }

        // ---- 2. a v28 save ------------------------------------------------------------------------------

        [Test]
        public void AV28Save_RoundTripsByteStable()
        {
            string first = SaveSerialization.ToJson(WithPictures());
            string second = SaveSerialization.ToJson(SaveSerialization.FromJson(first));
            string third = SaveSerialization.ToJson(SaveSerialization.FromJson(second));
            Assert.AreEqual(first, second);
            Assert.AreEqual(second, third);
        }

        [Test]
        public void AnAlbum_ComesBackExactly()
        {
            SaveData loaded = SaveSerialization.FromJson(SaveSerialization.ToJson(WithPictures()));

            Assert.AreEqual(3, loaded.Photos.Count);
            PhotoRecordDto first = loaded.Photos[0];
            Assert.AreEqual(1, first.Number);
            Assert.AreEqual(Local, first.PlayerKey);
            Assert.AreEqual(5000.25d, first.GameTimeSeconds);
            Assert.AreEqual(Region, first.RegionId);
            Assert.AreEqual(83, first.Score);
            Assert.AreEqual(2, first.Subjects.Count);
            Assert.AreEqual(Seal, first.Subjects[0].SubjectId);
            Assert.AreEqual(3, first.Subjects[0].Count);
            Assert.AreEqual((int)WildlifePose.Resting, first.Subjects[0].Pose);
            Assert.AreEqual(83, first.Subjects[0].Score);
            Assert.AreEqual(Gull, first.Subjects[1].SubjectId);
            Assert.AreEqual(2, loaded.Photos[1].Number);
            Assert.AreEqual(Guest, loaded.Photos[2].PlayerKey);
        }

        [Test]
        public void TheNewKeys_AreInTheFile_AndNoImageNorTideWindOrWeather()
        {
            string json = SaveSerialization.ToJson(WithPictures());
            foreach (string key in new[] { "\"Photos\"", "\"Number\"", "\"RegionId\"", "\"Subjects\"",
                                           "\"SubjectId\"", "\"Pose\"", "\"Score\"" })
                StringAssert.Contains(key, json);
            foreach (string forbidden in new[] { "tide", "wind", "weather", "png", "jpg", "Bytes" })
                Assert.AreEqual(-1, json.IndexOf(forbidden, StringComparison.OrdinalIgnoreCase), forbidden);
        }

        // ---- 3. the album's rows ------------------------------------------------------------------------

        [Test]
        public void Add_NumbersFromOne_AndNeverReusesANumber()
        {
            SaveData save = SaveMigration.NewGame();
            Assert.AreEqual(1, Add(save, Local, Seal, 1, WildlifePose.Resting, 50).Number);
            Assert.AreEqual(2, Add(save, Local, Seal, 1, WildlifePose.Resting, 50).Number);
            save.Photos.RemoveAt(0);
            Assert.AreEqual(3, Add(save, Guest, Seal, 1, WildlifePose.Resting, 50).Number);
        }

        [Test]
        public void Add_APictureOfNothing_IsNotKept()
        {
            SaveData save = SaveMigration.NewGame();
            Assert.IsNull(PhotoAlbumRows.Add(save, Local, 10d, Region, new PhotoSubjectResult[4], 0, 120));
            Assert.IsNull(PhotoAlbumRows.Add(save, Local, 10d, Region,
                                             new[] { new PhotoSubjectResult { SubjectId = "", Count = 1 } }, 1, 120));
            Assert.AreEqual(0, save.Photos.Count);
        }

        [Test]
        public void Add_AFullAlbum_KeepsNoMore_ButAnotherPlayersIsHisOwn()
        {
            SaveData save = SaveMigration.NewGame();
            for (int i = 0; i < 3; i++) Assert.IsNotNull(Add(save, Local, Seal, 1, WildlifePose.Resting, 50, 3));
            Assert.IsNull(Add(save, Local, Seal, 1, WildlifePose.Resting, 50, 3));
            Assert.IsNotNull(Add(save, Guest, Seal, 1, WildlifePose.Resting, 50, 3));
            Assert.AreEqual(3, PhotoAlbumRows.Count(save, Local));
            Assert.AreEqual(1, PhotoAlbumRows.Count(save, Guest));
        }

        [Test]
        public void HasQualifying_AsksCountPoseAndScore_ForThatPlayerOnly()
        {
            SaveData save = SaveMigration.NewGame();
            Add(save, Guest, Seal, 3, WildlifePose.Resting, 90);
            Add(save, Local, Seal, 1, WildlifePose.Resting, 90);
            Add(save, Local, Seal, 2, WildlifePose.Swimming, 90);
            Add(save, Local, Seal, 2, WildlifePose.Alert, 20);

            Assert.IsFalse(PhotoAlbumRows.HasQualifying(save, Local, Seal, 2, WildlifePose.Alert, 30),
                "one seal is too few, a swimming one too poor a pose, a score of 20 too low");
            Assert.IsTrue(PhotoAlbumRows.HasQualifying(save, Guest, Seal, 2, WildlifePose.Alert, 30));
            Add(save, Local, Seal, 2, WildlifePose.Alert, 30);
            Assert.IsTrue(PhotoAlbumRows.HasQualifying(save, Local, Seal, 2, WildlifePose.Alert, 30));
            Assert.IsFalse(PhotoAlbumRows.HasQualifying(save, Local, Gull, 1, WildlifePose.Diving, 0));
        }

        [Test]
        public void TheAccessors_AreNullSafe()
        {
            Assert.AreEqual(0, PhotoAlbumRows.Count(null, Local));
            Assert.AreEqual(1, PhotoAlbumRows.NextNumber(null));
            Assert.IsNull(PhotoAlbumRows.Find(null, 1));
            Assert.IsFalse(PhotoAlbumRows.HasQualifying(null, Local, Seal, 1, WildlifePose.Diving, 0));
            Assert.IsNull(PhotoAlbumRows.Add(null, Local, 0d, Region, new PhotoSubjectResult[1], 1, 120));
        }

        // ---- 4. the load repair -------------------------------------------------------------------------

        [Test]
        public void Repair_DropsNothingRows_ClampsValues_AndNeverImprovesAPicture()
        {
            SaveData save = Migrated(
                "\"Photos\":[" +
                "null," +
                "{\"PlayerKey\":\"\",\"Number\":4,\"GameTimeSeconds\":-5.0,\"Score\":150,\"Subjects\":[" +
                    "{\"SubjectId\":\"wildlife.grey_seal\",\"Count\":0,\"Pose\":9,\"Score\":140}," +
                    "{\"SubjectId\":\"\",\"Count\":2,\"Pose\":0,\"Score\":50}]}," +
                "{\"PlayerKey\":\"player.local\",\"Number\":5,\"Score\":10,\"Subjects\":[]}" +
                "]");

            Assert.AreEqual(1, save.Photos.Count, "the null row and the picture of nothing are dropped");
            PhotoRecordDto p = save.Photos[0];
            Assert.AreEqual(Local, p.PlayerKey);
            Assert.AreEqual(0d, p.GameTimeSeconds);
            Assert.AreEqual("", p.RegionId);
            Assert.AreEqual(100, p.Score);
            Assert.AreEqual(1, p.Subjects.Count, "a subject with no id is dropped");
            Assert.AreEqual(1, p.Subjects[0].Count);
            Assert.AreEqual((int)WildlifePose.Diving, p.Subjects[0].Pose, "an unknown pose reads the least picture");
            Assert.AreEqual(100, p.Subjects[0].Score);
        }

        [Test]
        public void Repair_RepeatedAndBadNumbers_AreRenumbered_AndTheAlbumIsInOrder()
        {
            SaveData save = Migrated(
                "\"Photos\":[" + Row(7) + "," + Row(2) + "," + Row(7) + "," + Row(0) + "]");
            CollectionAssert.AreEqual(new[] { 2, 7, 8, 9 }, Numbers(save));
            Assert.AreEqual(10, PhotoAlbumRows.NextNumber(save));
        }

        [Test]
        public void Repair_IsIdempotent_OnADamagedFile()
        {
            string damaged = "{\"SchemaVersion\":28,\"Photos\":[" + Row(3) + "," + Row(3) + "," +
                             "{\"Number\":-1,\"Score\":200,\"Subjects\":[{\"SubjectId\":\"wildlife.grey_seal\"," +
                             "\"Count\":-4,\"Pose\":-2,\"Score\":-3}]}]}";
            SaveData once = SaveSerialization.FromJson(damaged);
            string first = SaveSerialization.ToJson(once);
            SaveMigration.Migrate(once);
            Assert.AreEqual(first, SaveSerialization.ToJson(once));
            Assert.AreEqual(first, SaveSerialization.ToJson(SaveSerialization.FromJson(first)));
        }

        [Test]
        public void Repair_LeavesAWrittenAlbumUntouched()
        {
            SaveData save = WithPictures();
            string written = SaveSerialization.ToJson(save);
            SaveMigration.Migrate(save);
            Assert.AreEqual(written, SaveSerialization.ToJson(save));
        }

        // ---- 5. both save files -------------------------------------------------------------------------

        [Test]
        public void TheFullGameFile_AndTheDemoFile_ReadAndWriteTheSameFormat()
        {
            string dir = Path.Combine(Path.GetTempPath(), "save-v28-" + Guid.NewGuid().ToString("N"));
            string full = Path.Combine(dir, SaveStore.DefaultFileName);
            string demo = Path.Combine(dir, "demo", SaveStore.DefaultFileName);
            try
            {
                SaveData save = WithPictures();
                string expected = SaveSerialization.ToJson(save);
                foreach (string path in new[] { full, demo })
                {
                    SaveStore.Write(save, path);
                    SaveStore.Write(save, path);
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
        public void AV27DemoFile_OnDisk_LoadsAsV28()
        {
            string dir = Path.Combine(Path.GetTempPath(), "save-v28-" + Guid.NewGuid().ToString("N"));
            string demo = Path.Combine(dir, "demo", SaveStore.DefaultFileName);
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(demo));
                File.WriteAllText(demo, V27File);
                SaveData loaded = SaveStore.Read(demo);
                Assert.IsNotNull(loaded);
                Assert.AreEqual(SaveMigration.CurrentVersion, loaded.SchemaVersion);
                Assert.AreEqual(0, loaded.Photos.Count);
                Assert.AreEqual(250, loaded.Money);
            }
            finally
            {
                if (Directory.Exists(dir)) Directory.Delete(dir, true);
            }
        }

        // ---- helpers ------------------------------------------------------------------------------------

        /// <summary>A v28 game with three pictures: two of his, one a guest's.</summary>
        static SaveData WithPictures()
        {
            SaveData save = SaveSerialization.FromJson(V27File);
            var subjects = new[]
            {
                new PhotoSubjectResult { SubjectId = Seal, Count = 3, Pose = WildlifePose.Resting, Quality = 0.75f, Score = 83 },
                new PhotoSubjectResult { SubjectId = Gull, Count = 1, Pose = WildlifePose.Alert, Quality = 0.4f, Score = 40 },
            };
            Assert.IsNotNull(PhotoAlbumRows.Add(save, Local, 5000.25d, Region, subjects, 2, 120));
            Add(save, Local, Seal, 1, WildlifePose.Sliding, 37);
            Add(save, Guest, Seal, 2, WildlifePose.Swimming, 22);
            return save;
        }

        static PhotoRecordDto Add(SaveData save, string key, string subject, int count, WildlifePose pose, int score,
                                  int capacity = 120) =>
            PhotoAlbumRows.Add(save, key, 6000d, Region,
                               new[] { new PhotoSubjectResult { SubjectId = subject, Count = count, Pose = pose, Score = score } },
                               1, capacity);

        static string Row(int number) =>
            "{\"PlayerKey\":\"player.local\",\"Number\":" + number + ",\"GameTimeSeconds\":1.0," +
            "\"RegionId\":\"region.st_peters\",\"Score\":50,\"Subjects\":[{\"SubjectId\":\"wildlife.grey_seal\"," +
            "\"Count\":1,\"Pose\":0,\"Score\":50}]}";

        static int[] Numbers(SaveData save)
        {
            var n = new int[save.Photos.Count];
            for (int i = 0; i < n.Length; i++) n[i] = save.Photos[i].Number;
            return n;
        }

        /// <summary>A v28 file holding <paramref name="fields"/>, through the load repair. (A file stamped
        /// v27 would carry no album: the step starts it empty.)</summary>
        static SaveData Migrated(string fields) =>
            SaveSerialization.FromJson("{\"SchemaVersion\":28," + fields + "}");
    }
}
