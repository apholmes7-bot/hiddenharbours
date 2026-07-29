using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// Save schema v5 — the <b>Port Greywick → Nine Mile Creek</b> region rename (plan-to-m1 D1/§7.10).
    ///
    /// <para><c>PlacedTrapDto.Region</c> is the only save field that stores a region id as a STRING
    /// (scene-per-region, ADR 0004), so a trap dropped before the rename would name a region no def
    /// claims: it loads, sits in a region that never resolves, and is never haulable again. The v4→v5
    /// step rewrites exactly that id.</para>
    ///
    /// <para><b>⚠ This is the first migration step that REINTERPRETS a value rather than only adding
    /// one</b>, so these tests are deliberately fussy about how narrow it is: one field, one id, every
    /// other region string left alone, and no effect on a save that already knows the new name.</para>
    /// </summary>
    public class SaveMigrationV5Tests
    {
        [Test]
        public void CurrentVersion_IsAtLeastV5()
        {
            Assert.GreaterOrEqual(SaveMigration.CurrentVersion, 5,
                "the region rename lands at schema v5");
        }

        private static PlacedTrapDto Trap(string instance, string region) => new PlacedTrapDto
        {
            InstanceId = instance,
            TrapDefId = "trap.lobster_pot",
            PosX = 3f, PosY = -1f,
            BaitId = "bait.herring",
            PlacementGameTimeSeconds = 4200.5,
            Region = region,
        };

        // ---- the rename itself --------------------------------------------------------------------

        [Test]
        public void APreRenameTrap_IsMovedToTheNewRegionId_WithEverythingElseIntact()
        {
            var v4 = new SaveData
            {
                SchemaVersion = 4,
                WorldSeed = 777,
                GameTimeSeconds = 123.5,
                Money = 42,
                PlacedTraps = new List<PlacedTrapDto> { Trap("t1", SaveMigration.RenamedRegionFrom) },
            };

            SaveData up = SaveMigration.Migrate(v4);

            Assert.AreEqual(SaveMigration.CurrentVersion, up.SchemaVersion);
            Assert.AreEqual(1, up.PlacedTraps.Count, "the trap must survive, not be dropped");

            PlacedTrapDto t = up.PlacedTraps[0];
            Assert.AreEqual(SaveMigration.RenamedRegionTo, t.Region,
                "the trap must land in the renamed region — otherwise it sits in a region no def " +
                "claims and can never be hauled again");

            // Everything else about the trap is a placement FACT and must be untouched: the soak and
            // its contents recompute from these (rule 5), so a nudged position or timestamp silently
            // changes what the player pulls up.
            Assert.AreEqual("t1", t.InstanceId);
            Assert.AreEqual("trap.lobster_pot", t.TrapDefId);
            Assert.AreEqual("bait.herring", t.BaitId);
            Assert.AreEqual(3f, t.PosX, 1e-6f);
            Assert.AreEqual(-1f, t.PosY, 1e-6f);
            Assert.AreEqual(4200.5, t.PlacementGameTimeSeconds, 1e-9);

            Assert.AreEqual(777, up.WorldSeed);
            Assert.AreEqual(123.5, up.GameTimeSeconds, 1e-9);
            Assert.AreEqual(42, up.Money);
        }

        [Test]
        public void EveryPreRenameTrap_IsMoved_NotJustTheFirst()
        {
            var v4 = new SaveData
            {
                SchemaVersion = 4,
                PlacedTraps = new List<PlacedTrapDto>
                {
                    Trap("a", SaveMigration.RenamedRegionFrom),
                    Trap("b", "region.st_peters"),
                    Trap("c", SaveMigration.RenamedRegionFrom),
                },
            };

            SaveData up = SaveMigration.Migrate(v4);

            Assert.AreEqual(3, up.PlacedTraps.Count);
            Assert.AreEqual(SaveMigration.RenamedRegionTo, up.PlacedTraps[0].Region);
            Assert.AreEqual(SaveMigration.RenamedRegionTo, up.PlacedTraps[2].Region);
            Assert.IsFalse(up.PlacedTraps.Any(t => t.Region == SaveMigration.RenamedRegionFrom),
                "no trap may still name the old id after the migration");
        }

        // ---- ⚠ SABOTAGE-SHAPED: the step must be NARROW ------------------------------------------

        /// <summary>
        /// A struct in a <c>List&lt;T&gt;</c> is a copy on read. Mutating <c>list[i].Field</c> is a
        /// compile error in C#, but the near-miss — reading into a local, mutating it and forgetting to
        /// write it back — compiles and silently does nothing. This test is what catches that, because
        /// the migration would otherwise "pass" by bumping the version alone.
        /// </summary>
        [Test]
        public void TheRewriteActuallyPersists_NotJustOnACopyOfTheStruct()
        {
            var v4 = new SaveData
            {
                SchemaVersion = 4,
                PlacedTraps = new List<PlacedTrapDto> { Trap("t1", SaveMigration.RenamedRegionFrom) },
            };

            SaveData up = SaveMigration.Migrate(v4);

            Assert.AreNotEqual(SaveMigration.RenamedRegionFrom, up.PlacedTraps[0].Region,
                "SABOTAGE — PlacedTrapDto is a STRUCT, so a migration that mutated a local copy and " +
                "never wrote it back would leave the old id here while still bumping to v5.");
        }

        [Test]
        public void OtherRegions_AreLeftAlone()
        {
            var v4 = new SaveData
            {
                SchemaVersion = 4,
                PlacedTraps = new List<PlacedTrapDto>
                {
                    Trap("a", "region.st_peters"),
                    Trap("b", "region.coddle_cove"),
                    Trap("c", ""),
                    Trap("d", null),
                },
            };

            SaveData up = SaveMigration.Migrate(v4);

            Assert.AreEqual("region.st_peters", up.PlacedTraps[0].Region);
            Assert.AreEqual("region.coddle_cove", up.PlacedTraps[1].Region);
            Assert.AreEqual("", up.PlacedTraps[2].Region, "an empty region is not the renamed one");
            Assert.IsNull(up.PlacedTraps[3].Region, "…and neither is a null one — no NRE, no rewrite");
        }

        [Test]
        public void ASaveThatAlreadyKnowsTheNewName_IsUnchanged()
        {
            var current = new SaveData
            {
                SchemaVersion = SaveMigration.CurrentVersion,
                PlacedTraps = new List<PlacedTrapDto> { Trap("t1", SaveMigration.RenamedRegionTo) },
            };

            SaveData up = SaveMigration.Migrate(current);

            Assert.AreEqual(SaveMigration.RenamedRegionTo, up.PlacedTraps[0].Region);
            Assert.AreEqual(SaveMigration.CurrentVersion, up.SchemaVersion);
        }

        [Test]
        public void AnOldSaveWithNoTraps_UpgradesCleanly()
        {
            // The common case by far — the step must not require the list to exist.
            var v4 = new SaveData { SchemaVersion = 4, PlacedTraps = null };

            SaveData up = SaveMigration.Migrate(v4);

            Assert.AreEqual(SaveMigration.CurrentVersion, up.SchemaVersion);
            Assert.IsNotNull(up.PlacedTraps, "the null-repair still gives it an empty list");
            Assert.IsEmpty(up.PlacedTraps);
        }

        [Test]
        public void TheWholeChain_V0ToV5_StillRuns_AndRenamesOnTheWayThrough()
        {
            // A save from before ANY of the steps, with a trap in the old region. Every step must run
            // in order and the rename must still happen at the end of the chain.
            var v0 = new SaveData
            {
                SchemaVersion = 0,
                OwnedBoats = new List<string> { "boat.dory" },
                PlacedTraps = new List<PlacedTrapDto> { Trap("t1", SaveMigration.RenamedRegionFrom) },
            };

            SaveData up = SaveMigration.Migrate(v0);

            Assert.AreEqual(SaveMigration.CurrentVersion, up.SchemaVersion);
            Assert.Contains("boat.dory", up.RepairedBoats, "v1→v2 still marks owned boats repaired");
            Assert.AreEqual(1, up.PotStock.Count, "v3→v4 still adopts the deployed pot into owned");
            Assert.AreEqual(SaveMigration.RenamedRegionTo, up.PlacedTraps[0].Region,
                "…and v4→v5 still renames its region");
        }

        // ---- the id pair itself -------------------------------------------------------------------

        [Test]
        public void TheRenamedIds_AreTheOnesTheRegionDefActuallyUses()
        {
            Assert.AreEqual("region.port_greywick", SaveMigration.RenamedRegionFrom,
                "the id every pre-rename save was written with");
            Assert.AreEqual("region.nine_mile_creek", SaveMigration.RenamedRegionTo,
                "…and the id the region carries now. If these ever drift from the RegionDef the " +
                "migration moves traps to a region that does not exist.");

            var region = UnityEditor.AssetDatabase.LoadAssetAtPath<HiddenHarbours.World.RegionDef>(
                "Assets/_Project/Data/Regions/NineMileCreek.asset");
            Assert.IsNotNull(region, "the renamed region asset must exist at its new path");
            Assert.AreEqual(SaveMigration.RenamedRegionTo, region.Id,
                "the migration's target must be the id the shipped def actually carries");
            Assert.AreEqual("Nine Mile Creek", region.DisplayName);
            Assert.AreEqual("NineMileCreek", region.SceneName,
                "the SCENE name is an identifier, not the display name — it must not gain spaces");
        }
    }
}
