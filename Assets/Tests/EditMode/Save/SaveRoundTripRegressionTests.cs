using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.EditMode.Regression
{
    /// <summary>
    /// qa-test regression net (expand net 2) over the VS-08 save system, covering migration + disk paths
    /// BEYOND the #40 unit tests (which pinned round-trip, v0→v1, owned-boat/flag persistence, and
    /// corrupt/missing-file safety). New ground here: a future (newer-than-this-build) save is clamped
    /// rather than trusted, migration is idempotent and null-safe, a NewGame blob round-trips, the JSON
    /// is stable under re-serialization (a guard against silent schema drift), an existing save file is
    /// overwritten atomically (the File.Replace path, not the first-write Move), and owned-boat ORDER
    /// survives disk. Pure Core save layer; a temp file, no scene.
    /// </summary>
    public class SaveRoundTripRegressionTests
    {
        private string _dir;

        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "hh_save_regression");
            Directory.CreateDirectory(_dir);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
        }

        private string TempPath(string name) => Path.Combine(_dir, name);

        // ---- migration robustness -----------------------------------------------------------

        [Test]
        public void Migrate_FutureVersion_IsClampedToCurrent_NotTrustedBlindly()
        {
            // A save written by a NEWER build than this one: we must not claim to understand a schema we
            // don't. Clamp to the version this build actually handles instead of crashing or running it raw.
            var future = new SaveData { SchemaVersion = SaveMigration.CurrentVersion + 5, Money = 99 };

            SaveData migrated = SaveMigration.Migrate(future);

            Assert.AreEqual(SaveMigration.CurrentVersion, migrated.SchemaVersion,
                "a future-version save is clamped to the version this build understands");
            Assert.AreEqual(99, migrated.Money, "scalar payload is left intact");
            Assert.IsNotNull(migrated.OwnedBoats);
            Assert.IsNotNull(migrated.OnboardingFlags);
        }

        [Test]
        public void Migrate_IsIdempotent_OnACurrentVersionSave()
        {
            var data = new SaveData
            {
                SchemaVersion = SaveMigration.CurrentVersion,
                Money = 50,
                GameTimeSeconds = 123.5,
                OwnedBoats = new List<string> { "boat.dory" },
                OnboardingFlags = new List<SaveFlag> { new SaveFlag("met_ginny", true) },
                ActiveHullId = "boat.dory",
            };

            SaveData once = SaveMigration.Migrate(data);
            string afterOnce = SaveSerialization.ToJson(once);
            SaveData twice = SaveMigration.Migrate(once);

            Assert.AreEqual(SaveMigration.CurrentVersion, twice.SchemaVersion, "version doesn't drift on re-migrate");
            Assert.AreEqual(afterOnce, SaveSerialization.ToJson(twice),
                "migrating an already-current save again changes nothing (idempotent)");
        }

        [Test]
        public void Migrate_Null_ReturnsNull_TheNoSaveContract()
        {
            // SaveStore.Read returns null for "no file"; Migrate(null) must stay null so launch starts a
            // new game rather than throwing.
            Assert.IsNull(SaveMigration.Migrate(null));
        }

        // ---- NewGame + serialization stability ----------------------------------------------

        [Test]
        public void NewGame_IsCurrentVersion_WithEmptyContainers_AndRoundTrips()
        {
            SaveData fresh = SaveMigration.NewGame();

            Assert.AreEqual(SaveMigration.CurrentVersion, fresh.SchemaVersion);
            Assert.IsNotNull(fresh.OwnedBoats);
            Assert.IsEmpty(fresh.OwnedBoats);
            Assert.IsNotNull(fresh.OnboardingFlags);
            Assert.IsEmpty(fresh.OnboardingFlags);

            SaveData reloaded = SaveSerialization.FromJson(SaveSerialization.ToJson(fresh));
            Assert.IsNotNull(reloaded);
            Assert.AreEqual(fresh.SchemaVersion, reloaded.SchemaVersion);
            Assert.IsEmpty(reloaded.OwnedBoats);
            Assert.IsEmpty(reloaded.OnboardingFlags);
        }

        [Test]
        public void Json_IsStable_UnderReserialization_GuardingSchemaDrift()
        {
            var data = new SaveData
            {
                SchemaVersion = SaveMigration.CurrentVersion,
                WorldSeed = 4242,
                GameTimeSeconds = 9876.5,
                Money = 321,
                DayIndex = 8,
                OwnedBoats = new List<string> { "boat.dory", "boat.punt" },
                ActiveHullId = "boat.punt",
                OnboardingFlags = new List<SaveFlag>
                {
                    new SaveFlag("met_ginny", true),
                    new SaveFlag("onboarded", false),
                },
            };

            string first = SaveSerialization.ToJson(data);
            string second = SaveSerialization.ToJson(SaveSerialization.FromJson(first));

            Assert.AreEqual(first, second,
                "serialize → parse → serialize is a fixed point; any drift signals an unintended schema change");
        }

        // ---- disk: atomic overwrite of an existing save (File.Replace path) ------------------

        [Test]
        public void DiskWrite_OverwritesAnExistingSave_Atomically()
        {
            string path = TempPath("savegame.json");

            var first = new SaveData { SchemaVersion = SaveMigration.CurrentVersion, Money = 100, DayIndex = 1 };
            SaveStore.Write(first, path);
            Assert.IsTrue(File.Exists(path));

            // Second write hits the File.Replace branch (target already exists) — the #40 tests only
            // exercised the first-write Move. The new value must fully replace the old.
            var second = new SaveData { SchemaVersion = SaveMigration.CurrentVersion, Money = 777, DayIndex = 9 };
            SaveStore.Write(second, path);

            SaveData reloaded = SaveStore.Read(path);
            Assert.IsNotNull(reloaded);
            Assert.AreEqual(777, reloaded.Money, "the second save replaced the first");
            Assert.AreEqual(9, reloaded.DayIndex);
            Assert.IsFalse(File.Exists(path + ".tmp"), "the temp file is swapped away, not left behind");
        }

        [Test]
        public void OwnedBoats_PreserveOrder_AcrossADiskRoundTrip()
        {
            string path = TempPath("fleet.json");
            var save = new SaveData
            {
                SchemaVersion = SaveMigration.CurrentVersion,
                OwnedBoats = new List<string> { "boat.dory", "boat.punt", "boat.cape_islander" },
                ActiveHullId = "boat.cape_islander",
            };

            SaveStore.Write(save, path);
            SaveData reloaded = SaveStore.Read(path);

            Assert.IsNotNull(reloaded);
            CollectionAssert.AreEqual(save.OwnedBoats, reloaded.OwnedBoats,
                "acquisition order is part of the contract (SaveData documents OwnedBoats as acquired-order)");
            Assert.AreEqual("boat.cape_islander", reloaded.ActiveHullId);
        }
    }
}
