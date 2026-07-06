using System.Collections.Generic;
using NUnit.Framework;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// Save schema v2 (St Peters opening): the licence wallet, per-boat repair state, and owned-gear
    /// wallet are new in v2. These verify the migration is additive and lossless — a v1 save upgrades to
    /// v2 with empty new lists and its scalars untouched, an already-owned (pre-v2) boat is marked
    /// repaired so it stays usable, and a v2 save round-trips through JSON. Pure save-layer tests, headless.
    /// </summary>
    public class SaveMigrationV2Tests
    {
        [Test]
        public void CurrentVersion_IsAtLeastV2()
        {
            Assert.GreaterOrEqual(SaveMigration.CurrentVersion, 2,
                "the St Peters licence/repair/gear fields land at schema v2");
        }

        [Test]
        public void V1Save_UpgradesToV2_WithEmptyNewLists_ScalarsUntouched()
        {
            var v1 = new SaveData
            {
                SchemaVersion   = 1,
                WorldSeed       = 999,
                GameTimeSeconds = 3600.5,
                Money           = 250,
                DayIndex        = 2,
                OwnedBoats      = new List<string>(),   // no boats yet
                ActiveHullId    = "",
                OnboardingFlags = new List<SaveFlag>(),
                OwnedLicenses   = null,                 // didn't exist pre-v2
                RepairedBoats   = null,
                OwnedGear       = null,
            };

            SaveData up = SaveMigration.Migrate(v1);

            Assert.AreEqual(SaveMigration.CurrentVersion, up.SchemaVersion, "a v1 save climbs to the current schema version");
            // Scalars carried through untouched.
            Assert.AreEqual(999, up.WorldSeed);
            Assert.AreEqual(3600.5, up.GameTimeSeconds, 1e-6);
            Assert.AreEqual(250, up.Money);
            Assert.AreEqual(2, up.DayIndex);
            // New-in-v2 lists exist + empty (not null) so the game can use them safely.
            Assert.IsNotNull(up.OwnedLicenses); Assert.IsEmpty(up.OwnedLicenses);
            Assert.IsNotNull(up.RepairedBoats); Assert.IsEmpty(up.RepairedBoats);
            Assert.IsNotNull(up.OwnedGear);     Assert.IsEmpty(up.OwnedGear);
        }

        [Test]
        public void V1Save_WithOwnedBoat_MarksItRepaired_SoItStaysUsable()
        {
            // A pre-v2 owned boat was always usable. After the upgrade, "usable" is gated on RepairedBoats,
            // so the migration must mark the existing boat repaired or it'd become unusable on load.
            var v1 = new SaveData
            {
                SchemaVersion = 1,
                OwnedBoats    = new List<string> { "boat.dory", "boat.punt" },
                ActiveHullId  = "boat.punt",
            };

            SaveData up = SaveMigration.Migrate(v1);

            Assert.AreEqual(SaveMigration.CurrentVersion, up.SchemaVersion);
            CollectionAssert.Contains(up.RepairedBoats, "boat.dory", "an already-owned boat stays usable after upgrade");
            CollectionAssert.Contains(up.RepairedBoats, "boat.punt");
        }

        [Test]
        public void V2Save_RoundTripsThroughJson_WithNewLists()
        {
            var original = new SaveData
            {
                SchemaVersion = SaveMigration.CurrentVersion,
                Money         = 500,
                OwnedBoats    = new List<string> { "boat.dory" },
                ActiveHullId  = "boat.dory",
                OwnedLicenses = new List<string> { "license.cod" },
                RepairedBoats = new List<string> { "boat.dory" },
                OwnedGear     = new List<string> { "gear.rod", "gear.shovel", "gear.bucket" },
            };

            string json = SaveSerialization.ToJson(original);
            SaveData reloaded = SaveSerialization.FromJson(json);

            Assert.IsNotNull(reloaded);
            Assert.AreEqual(SaveMigration.CurrentVersion, reloaded.SchemaVersion);
            CollectionAssert.AreEqual(original.OwnedLicenses, reloaded.OwnedLicenses);
            CollectionAssert.AreEqual(original.RepairedBoats, reloaded.RepairedBoats);
            CollectionAssert.AreEqual(original.OwnedGear, reloaded.OwnedGear);
        }

        [Test]
        public void V0Save_StillUpgradesAllTheWay_ToV2()
        {
            // The original v0 shape (no fleet/flags/licence lists at all) must climb every step to the
            // current schema version, filling each version's new lists as it goes.
            var v0 = new SaveData { SchemaVersion = 0, Money = 30 };

            SaveData up = SaveMigration.Migrate(v0);

            Assert.AreEqual(SaveMigration.CurrentVersion, up.SchemaVersion);
            Assert.AreEqual(30, up.Money);
            Assert.IsNotNull(up.OwnedBoats);
            Assert.IsNotNull(up.OwnedLicenses);
            Assert.IsNotNull(up.RepairedBoats);
            Assert.IsNotNull(up.OwnedGear);
            Assert.IsNotNull(up.PlacedTraps);
            Assert.IsNotNull(up.BaitStock);
        }
    }
}
