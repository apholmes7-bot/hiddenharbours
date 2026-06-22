using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// VS-08 save schema v1 acceptance: the DTO survives a serialize→deserialize round-trip at full
    /// precision (a mid-minute save reloads to the same instant), an older (v0) save migrates forward
    /// through a no-op upgrade instead of crashing, and the owned fleet + onboarding flags persist across
    /// a real disk save/load. These hit the pure save layers (no scene), so they run headless.
    /// </summary>
    public class SaveServiceTests
    {
        private string _dir;

        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "hh_save_tests");
            Directory.CreateDirectory(_dir);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
        }

        private string TempPath(string name) => Path.Combine(_dir, name);

        // ---- round-trip ------------------------------------------------------------------------

        [Test]
        public void RoundTrip_PreservesAllFields_IncludingSubMinuteGameTime()
        {
            var original = new SaveData
            {
                SchemaVersion   = SaveMigration.CurrentVersion,
                WorldSeed       = 12345,
                GameTimeSeconds = 1234.567,        // sub-minute AND sub-second: must not round to whole minutes
                Money           = 250,
                DayIndex        = 1,
                OwnedBoats      = new List<string> { "boat.dory", "boat.punt" },
                ActiveHullId    = "boat.punt",
                OnboardingFlags = new List<SaveFlag>
                {
                    new SaveFlag("met_ginny", true),
                    new SaveFlag("onboarded", false),
                },
            };

            string json = SaveSerialization.ToJson(original);
            SaveData reloaded = SaveSerialization.FromJson(json);

            Assert.IsNotNull(reloaded);
            Assert.AreEqual(original.SchemaVersion, reloaded.SchemaVersion);
            Assert.AreEqual(original.WorldSeed, reloaded.WorldSeed);
            Assert.AreEqual(original.GameTimeSeconds, reloaded.GameTimeSeconds, 1e-6,
                "gameTime must survive at sub-minute precision (it's the master double clock).");
            Assert.AreEqual(original.Money, reloaded.Money);
            Assert.AreEqual(original.DayIndex, reloaded.DayIndex);
            CollectionAssert.AreEqual(original.OwnedBoats, reloaded.OwnedBoats);
            Assert.AreEqual(original.ActiveHullId, reloaded.ActiveHullId);

            Assert.AreEqual(2, reloaded.OnboardingFlags.Count);
            Assert.IsTrue(FlagValue(reloaded, "met_ginny"));
            Assert.IsFalse(FlagValue(reloaded, "onboarded"));
        }

        // ---- v0 → v1 migration -----------------------------------------------------------------

        [Test]
        public void Migration_V0Object_UpgradesToV1_WithEmptyContainers_NoCrash()
        {
            // A pre-v1 save: it predates the owned-fleet + flags lists (here truly absent → null) and
            // carries only the original scalars. The no-op upgrade must not touch those scalars.
            var v0 = new SaveData
            {
                SchemaVersion   = 0,
                WorldSeed       = 777,
                GameTimeSeconds = 4242.5,
                Money           = 120,
                DayIndex        = 3,
                OwnedBoats      = null,
                ActiveHullId    = null,
                OnboardingFlags = null,
            };

            SaveData upgraded = SaveMigration.Migrate(v0);

            Assert.IsNotNull(upgraded);
            Assert.AreEqual(1, upgraded.SchemaVersion, "v0 must be stamped v1.");
            // Scalars carried through untouched (no-op upgrade).
            Assert.AreEqual(777, upgraded.WorldSeed);
            Assert.AreEqual(4242.5, upgraded.GameTimeSeconds, 1e-6);
            Assert.AreEqual(120, upgraded.Money);
            Assert.AreEqual(3, upgraded.DayIndex);
            // New-in-v1 containers exist and are empty (not null) so the game can use them safely.
            Assert.IsNotNull(upgraded.OwnedBoats);
            Assert.IsEmpty(upgraded.OwnedBoats);
            Assert.IsNotNull(upgraded.OnboardingFlags);
            Assert.IsEmpty(upgraded.OnboardingFlags);
            Assert.AreEqual("", upgraded.ActiveHullId);
        }

        [Test]
        public void Migration_V0Json_LoadsThroughFromJson_NoCrash()
        {
            // A v0 JSON blob with no owned-boats / flags keys at all — the on-disk shape an older build
            // would have written. FromJson must parse + migrate it rather than throw.
            const string v0Json =
                "{\"SchemaVersion\":0,\"WorldSeed\":42,\"GameTimeSeconds\":90.25,\"Money\":15,\"DayIndex\":0}";

            SaveData loaded = SaveSerialization.FromJson(v0Json);

            Assert.IsNotNull(loaded);
            Assert.AreEqual(1, loaded.SchemaVersion);
            Assert.AreEqual(42, loaded.WorldSeed);
            Assert.AreEqual(90.25, loaded.GameTimeSeconds, 1e-6);
            Assert.AreEqual(15, loaded.Money);
            Assert.IsNotNull(loaded.OwnedBoats);
            Assert.IsNotNull(loaded.OnboardingFlags);
        }

        [Test]
        public void FromJson_NullOrGarbage_ReturnsNull_NotThrow()
        {
            Assert.IsNull(SaveSerialization.FromJson(null));
            Assert.IsNull(SaveSerialization.FromJson("   "));
            Assert.IsNull(SaveSerialization.FromJson("this is not json"));
        }

        // ---- owned boat + flags persist across a real disk save/load ---------------------------

        [Test]
        public void OwnedBoat_PersistsAcrossDiskSaveLoad()
        {
            string path = TempPath("owned.json");
            var save = new SaveData
            {
                SchemaVersion = SaveMigration.CurrentVersion,
                OwnedBoats    = new List<string> { "boat.dory", "boat.punt" },
                ActiveHullId  = "boat.punt",
            };

            SaveStore.Write(save, path);
            Assert.IsTrue(File.Exists(path), "the save file should be written to disk.");

            SaveData reloaded = SaveStore.Read(path);

            Assert.IsNotNull(reloaded);
            CollectionAssert.Contains(reloaded.OwnedBoats, "boat.punt", "the bought Punt must persist.");
            CollectionAssert.Contains(reloaded.OwnedBoats, "boat.dory");
            Assert.AreEqual("boat.punt", reloaded.ActiveHullId);
        }

        [Test]
        public void OnboardingFlags_PersistAcrossDiskSaveLoad()
        {
            string path = TempPath("flags.json");
            var save = new SaveData
            {
                SchemaVersion   = SaveMigration.CurrentVersion,
                OnboardingFlags = new List<SaveFlag>
                {
                    new SaveFlag("met_ginny", true),
                    new SaveFlag("read_logbook", true),
                    new SaveFlag("onboarded", true),
                },
            };

            SaveStore.Write(save, path);
            SaveData reloaded = SaveStore.Read(path);

            Assert.IsNotNull(reloaded);
            Assert.IsTrue(FlagValue(reloaded, "met_ginny"),    "met_ginny should survive a save/load.");
            Assert.IsTrue(FlagValue(reloaded, "read_logbook"), "read_logbook should survive a save/load.");
            Assert.IsTrue(FlagValue(reloaded, "onboarded"),    "onboarded should survive a save/load.");
        }

        [Test]
        public void Read_MissingFile_ReturnsNull()
        {
            Assert.IsNull(SaveStore.Read(TempPath("does-not-exist.json")));
        }

        // ---- helper ----------------------------------------------------------------------------

        private static bool FlagValue(SaveData data, string key)
        {
            foreach (var f in data.OnboardingFlags)
                if (f.Key == key) return f.Value;
            return false;
        }
    }
}
