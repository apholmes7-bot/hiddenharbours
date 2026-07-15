using System.Collections.Generic;
using NUnit.Framework;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// Save schema v4 (pots are OWNED, counted stock — ADR 0020 addendum): the <c>PotStock</c> list is
    /// new in v4, and the v3→v4 step ADOPTS already-deployed pots into owned (a pre-v4 player's set
    /// gear is physically theirs — the availability derivation must start at zero spare, never
    /// negative). These pin that the migration is additive and lossless, that adoption is exact and
    /// never re-runs, and that the new field round-trips through JSON. Pure save-layer tests, headless.
    /// </summary>
    public class SaveMigrationV4Tests
    {
        [Test]
        public void CurrentVersion_IsAtLeastV4()
        {
            Assert.GreaterOrEqual(SaveMigration.CurrentVersion, 4,
                "the owned-pot-stock field lands at schema v4");
        }

        private static PlacedTrapDto Deployed(string kind, string instance) => new PlacedTrapDto
        {
            InstanceId = instance,
            TrapDefId = kind,
            PosX = 3f, PosY = -1f,
            BaitId = "bait.herring",
            PlacementGameTimeSeconds = 4200.5,
            Region = "region.st_peters",
        };

        [Test]
        public void V3Save_NoDeployedPots_UpgradesToV4_WithEmptyPotStock_EverythingElseUntouched()
        {
            var v3 = new SaveData
            {
                SchemaVersion   = 3,
                WorldSeed       = 777,
                GameTimeSeconds = 123.5,
                Money           = 250,
                OwnedBoats      = new List<string> { "boat.dory" },
                ActiveHullId    = "boat.dory",
                OwnedLicenses   = new List<string> { "license.cod" },
                RepairedBoats   = new List<string> { "boat.dory" },
                OwnedGear       = new List<string> { "gear.rod" },
                PlacedTraps     = new List<PlacedTrapDto>(),
                BaitStock       = new List<BaitStock> { new BaitStock("bait.herring", 4) },
                PotStock        = null,   // didn't exist pre-v4
            };

            SaveData up = SaveMigration.Migrate(v3);

            Assert.AreEqual(SaveMigration.CurrentVersion, up.SchemaVersion, "a v3 save climbs to current");
            Assert.IsNotNull(up.PotStock);
            Assert.IsEmpty(up.PotStock, "no pots in the water → nothing adopted, empty stock");
            // Everything older carries through untouched.
            Assert.AreEqual(777, up.WorldSeed);
            Assert.AreEqual(250, up.Money);
            CollectionAssert.AreEqual(new List<string> { "boat.dory" }, up.OwnedBoats);
            Assert.AreEqual(4, up.BaitStock[0].Count, "bait stock untouched");
        }

        [Test]
        public void V3Save_WithDeployedPots_AdoptsThemIntoOwned_ZeroSpare()
        {
            var v3 = new SaveData
            {
                SchemaVersion = 3,
                PlacedTraps = new List<PlacedTrapDto>
                {
                    Deployed("trap.lobster", "a"),
                    Deployed("trap.lobster", "b"),
                    Deployed("trap.crab", "c"),
                },
            };

            SaveData up = SaveMigration.Migrate(v3);

            Assert.AreEqual(2, PotLocker.OwnedCount(up, "trap.lobster"), "both wet lobster pots are theirs");
            Assert.AreEqual(1, PotLocker.OwnedCount(up, "trap.crab"), "so is the crab pot");
            Assert.AreEqual(3, up.PlacedTraps.Count, "adoption never touches the placements themselves");
            // The derivation starts honest: everything owned is in the water, nothing spare, nothing owed.
            Assert.AreEqual(0, PotLocker.AvailableCount(up, "trap.lobster", 0));
            Assert.AreEqual(0, PotLocker.AvailableCount(up, "trap.crab", 0));
        }

        [Test]
        public void Migrate_IsIdempotent_AdoptionNeverRunsTwice()
        {
            var v3 = new SaveData
            {
                SchemaVersion = 3,
                PlacedTraps = new List<PlacedTrapDto> { Deployed("trap.lobster", "a") },
            };

            SaveData once = SaveMigration.Migrate(v3);
            Assert.AreEqual(1, PotLocker.OwnedCount(once, "trap.lobster"));

            SaveData twice = SaveMigration.Migrate(once);
            Assert.AreEqual(1, PotLocker.OwnedCount(twice, "trap.lobster"),
                "re-migrating a current-version save must not re-adopt (version-gated step)");
        }

        [Test]
        public void V0Save_ClimbsAllTheWay_WithNonNullPotStock()
        {
            var v0 = new SaveData { SchemaVersion = 0, Money = 40 };

            SaveData up = SaveMigration.Migrate(v0);

            Assert.AreEqual(SaveMigration.CurrentVersion, up.SchemaVersion);
            Assert.IsNotNull(up.PotStock);
            Assert.IsEmpty(up.PotStock);
            Assert.AreEqual(40, up.Money);
        }

        [Test]
        public void NewGame_IsCurrentVersion_WithNonNullEmptyPotStock()
        {
            SaveData fresh = SaveMigration.NewGame();

            Assert.AreEqual(SaveMigration.CurrentVersion, fresh.SchemaVersion);
            Assert.IsNotNull(fresh.PotStock);
            Assert.IsEmpty(fresh.PotStock,
                "the schema starts empty — the cozy starter kit is StartingPots' flag-guarded grant, not schema");
        }

        [Test]
        public void PotStock_RoundTripsThroughJson_CountsIntact()
        {
            var original = SaveMigration.NewGame();
            PotLocker.AddOwned(original, "trap.lobster", 3);
            PotLocker.AddOwned(original, "trap.crab", 1);

            string json = SaveSerialization.ToJson(original);
            SaveData reloaded = SaveSerialization.FromJson(json);

            Assert.IsNotNull(reloaded);
            Assert.AreEqual(SaveMigration.CurrentVersion, reloaded.SchemaVersion);
            Assert.AreEqual(3, PotLocker.OwnedCount(reloaded, "trap.lobster"));
            Assert.AreEqual(1, PotLocker.OwnedCount(reloaded, "trap.crab"));
            Assert.AreEqual(2, reloaded.PotStock.Count, "one record per kind survives the trip");
        }

        [Test]
        public void CurrentVersionBlob_MissingPotStockInJson_IsNullRepaired()
        {
            // A current-version save written by hand (or truncated) without the PotStock field must
            // load usable — the unconditional null-repair tail of Migrate covers it.
            var data = new SaveData { SchemaVersion = SaveMigration.CurrentVersion, PotStock = null };
            SaveData up = SaveMigration.Migrate(data);
            Assert.IsNotNull(up.PotStock);
            Assert.IsEmpty(up.PotStock);
        }
    }
}
