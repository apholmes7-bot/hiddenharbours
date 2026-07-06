using System.Collections.Generic;
using NUnit.Framework;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// Save schema v3 (trap-fishing arc Build 0 — world-placed-object persistence, ADR 0020): the
    /// placed-traps list and the counted bait stock are new in v3. These pin that the migration is
    /// additive and lossless — a v2 (or older) save upgrades to v3 with the two new lists empty-but-not-null
    /// and every scalar/older list untouched — and that a PlacedTrapDto round-trips through JSON with all
    /// its placement facts intact. Pure save-layer tests, headless (no scene, no GameServices).
    ///
    /// <para>These fields are UNUSED until the trap runtime lands (arc Build 3); this build is the durable
    /// schema groundwork, so the tests cover only the persistence contract, not any trap behaviour.</para>
    /// </summary>
    public class SaveMigrationV3Tests
    {
        [Test]
        public void CurrentVersion_IsAtLeastV3()
        {
            Assert.GreaterOrEqual(SaveMigration.CurrentVersion, 3,
                "the world-placed-object fields (placed traps + bait stock) land at schema v3");
        }

        [Test]
        public void V2Save_UpgradesToV3_WithEmptyNewLists_ScalarsAndOlderListsUntouched()
        {
            var v2 = new SaveData
            {
                SchemaVersion   = 2,
                WorldSeed       = 4242,
                GameTimeSeconds = 7200.25,
                Money           = 175,
                DayIndex        = 5,
                OwnedBoats      = new List<string> { "boat.dory" },
                ActiveHullId    = "boat.dory",
                OnboardingFlags = new List<SaveFlag> { new SaveFlag("met_ned", true) },
                OwnedLicenses   = new List<string> { "license.cod" },
                RepairedBoats   = new List<string> { "boat.dory" },
                OwnedGear       = new List<string> { "gear.rod" },
                PlacedTraps     = null,   // didn't exist pre-v3
                BaitStock       = null,   // didn't exist pre-v3
            };

            SaveData up = SaveMigration.Migrate(v2);

            Assert.AreEqual(3, up.SchemaVersion, "v2 is stamped v3");
            // Scalars carried through untouched.
            Assert.AreEqual(4242, up.WorldSeed);
            Assert.AreEqual(7200.25, up.GameTimeSeconds, 1e-6);
            Assert.AreEqual(175, up.Money);
            Assert.AreEqual(5, up.DayIndex);
            // Older (v1/v2) lists carried through untouched.
            CollectionAssert.AreEqual(new List<string> { "boat.dory" }, up.OwnedBoats);
            CollectionAssert.AreEqual(new List<string> { "license.cod" }, up.OwnedLicenses);
            CollectionAssert.AreEqual(new List<string> { "boat.dory" }, up.RepairedBoats);
            CollectionAssert.AreEqual(new List<string> { "gear.rod" }, up.OwnedGear);
            // New-in-v3 lists exist + empty (not null) so the game can use them safely.
            Assert.IsNotNull(up.PlacedTraps); Assert.IsEmpty(up.PlacedTraps);
            Assert.IsNotNull(up.BaitStock);   Assert.IsEmpty(up.BaitStock);
        }

        [Test]
        public void V0Save_StillUpgradesAllTheWay_ToV3()
        {
            // The original v0 shape (no fleet/flags/licence/gear lists at all) must climb every step to v3.
            var v0 = new SaveData { SchemaVersion = 0, Money = 40 };

            SaveData up = SaveMigration.Migrate(v0);

            Assert.AreEqual(3, up.SchemaVersion);
            Assert.AreEqual(40, up.Money);
            // Every list from every schema step is non-null after a full climb.
            Assert.IsNotNull(up.OwnedBoats);
            Assert.IsNotNull(up.OwnedLicenses);
            Assert.IsNotNull(up.RepairedBoats);
            Assert.IsNotNull(up.OwnedGear);
            Assert.IsNotNull(up.PlacedTraps); Assert.IsEmpty(up.PlacedTraps);
            Assert.IsNotNull(up.BaitStock);   Assert.IsEmpty(up.BaitStock);
        }

        [Test]
        public void NewGame_IsCurrentVersion_WithNonNullEmptyWorldPlacedLists()
        {
            SaveData fresh = SaveMigration.NewGame();

            Assert.AreEqual(SaveMigration.CurrentVersion, fresh.SchemaVersion);
            Assert.IsNotNull(fresh.PlacedTraps); Assert.IsEmpty(fresh.PlacedTraps);
            Assert.IsNotNull(fresh.BaitStock);   Assert.IsEmpty(fresh.BaitStock);
        }

        [Test]
        public void V3Save_RoundTripsThroughJson_WithPlacedTrapsAndBaitStock()
        {
            var original = new SaveData
            {
                SchemaVersion = SaveMigration.CurrentVersion,
                Money         = 500,
                OwnedBoats    = new List<string> { "boat.dory" },
                ActiveHullId  = "boat.dory",
                PlacedTraps   = new List<PlacedTrapDto>
                {
                    new PlacedTrapDto
                    {
                        InstanceId               = "trap-inst-0001",
                        TrapDefId                = "trap.lobster_pot",
                        PosX                     = 12.5f,
                        PosY                     = -3.25f,
                        BaitId                   = "bait.herring",
                        PlacementGameTimeSeconds = 3600.5,
                        Region                   = "region.st_peters",
                    },
                },
                BaitStock = new List<BaitStock>
                {
                    new BaitStock("bait.herring", 8),
                    new BaitStock("bait.squid", 3),
                },
            };

            string json = SaveSerialization.ToJson(original);
            SaveData reloaded = SaveSerialization.FromJson(json);

            Assert.IsNotNull(reloaded);
            Assert.AreEqual(SaveMigration.CurrentVersion, reloaded.SchemaVersion);

            // Placed-trap facts survive the round-trip exactly (irreducible placement record).
            Assert.AreEqual(1, reloaded.PlacedTraps.Count);
            PlacedTrapDto t = reloaded.PlacedTraps[0];
            Assert.AreEqual("trap-inst-0001", t.InstanceId);
            Assert.AreEqual("trap.lobster_pot", t.TrapDefId);
            Assert.AreEqual(12.5f, t.PosX, 1e-4f);
            Assert.AreEqual(-3.25f, t.PosY, 1e-4f);
            Assert.AreEqual("bait.herring", t.BaitId);
            Assert.AreEqual(3600.5, t.PlacementGameTimeSeconds, 1e-6);
            Assert.AreEqual("region.st_peters", t.Region);

            // Bait stock survives with counts intact.
            Assert.AreEqual(2, reloaded.BaitStock.Count);
            Assert.AreEqual("bait.herring", reloaded.BaitStock[0].BaitId);
            Assert.AreEqual(8, reloaded.BaitStock[0].Count);
            Assert.AreEqual("bait.squid", reloaded.BaitStock[1].BaitId);
            Assert.AreEqual(3, reloaded.BaitStock[1].Count);
        }
    }
}
