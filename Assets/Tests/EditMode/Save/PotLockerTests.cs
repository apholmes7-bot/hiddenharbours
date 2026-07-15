using System.Collections.Generic;
using NUnit.Framework;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// The pot locker's derivation maths (schema v4, ADR 0020 addendum): OWNED is the only stored
    /// number; AVAILABLE = owned − deployed − aboard is recomputed from the save every time, so the
    /// haul → deck → re-set cycle is stock-neutral BY CONSTRUCTION and the count can never desync.
    /// Pure save-layer tests, headless (no scene, no GameServices).
    /// </summary>
    public class PotLockerTests
    {
        private static SaveData NewSave() => SaveMigration.NewGame();

        private static PlacedTrapDto Deployed(string kind, string instance) => new PlacedTrapDto
        {
            InstanceId = instance,
            TrapDefId = kind,
            PosX = 1f, PosY = 2f,
            BaitId = "bait.herring",
            PlacementGameTimeSeconds = 100.0,
            Region = "region.st_peters",
        };

        // ---- owned ------------------------------------------------------------------------------

        [Test]
        public void OwnedCount_NullSave_UnknownKind_EmptyId_AllReadZero()
        {
            Assert.AreEqual(0, PotLocker.OwnedCount(null, "trap.lobster"));
            Assert.AreEqual(0, PotLocker.OwnedCount(NewSave(), "trap.lobster"));
            Assert.AreEqual(0, PotLocker.OwnedCount(NewSave(), ""));
            Assert.AreEqual(0, PotLocker.OwnedCount(NewSave(), null));
        }

        [Test]
        public void AddOwned_AddsThenMerges_OneRecordPerKind_ReturnsNewTotal()
        {
            var save = NewSave();

            Assert.AreEqual(2, PotLocker.AddOwned(save, "trap.lobster", 2), "first add returns the total");
            Assert.AreEqual(3, PotLocker.AddOwned(save, "trap.lobster", 1), "a further buy merges (2+1)");
            Assert.AreEqual(1, PotLocker.AddOwned(save, "trap.crab", 1), "another kind gets its own record");

            Assert.AreEqual(3, PotLocker.OwnedCount(save, "trap.lobster"));
            Assert.AreEqual(1, PotLocker.OwnedCount(save, "trap.crab"));
            Assert.AreEqual(2, save.PotStock.Count, "one record per kind, merged");
        }

        [Test]
        public void AddOwned_RefusesNullSave_EmptyId_NonPositiveCount()
        {
            var save = NewSave();
            Assert.AreEqual(0, PotLocker.AddOwned(null, "trap.lobster", 1));
            Assert.AreEqual(0, PotLocker.AddOwned(save, "", 1));
            Assert.AreEqual(0, PotLocker.AddOwned(save, "trap.lobster", 0));
            Assert.AreEqual(0, PotLocker.AddOwned(save, "trap.lobster", -3), "owned stock only grows here");
            Assert.IsEmpty(save.PotStock, "nothing recorded by a refused add");
        }

        [Test]
        public void AddOwned_NullPotStockList_IsRepaired()
        {
            var save = NewSave();
            save.PotStock = null;   // a hand-edited / partial blob
            Assert.AreEqual(1, PotLocker.AddOwned(save, "trap.lobster", 1));
            Assert.AreEqual(1, PotLocker.OwnedCount(save, "trap.lobster"));
        }

        // ---- deployed (derived from PlacedTraps, never stored) -----------------------------------

        [Test]
        public void DeployedCount_CountsPlacedTrapsByKind()
        {
            var save = NewSave();
            save.PlacedTraps.Add(Deployed("trap.lobster", "a"));
            save.PlacedTraps.Add(Deployed("trap.lobster", "b"));
            save.PlacedTraps.Add(Deployed("trap.crab", "c"));

            Assert.AreEqual(2, PotLocker.DeployedCount(save, "trap.lobster"));
            Assert.AreEqual(1, PotLocker.DeployedCount(save, "trap.crab"));
            Assert.AreEqual(0, PotLocker.DeployedCount(save, "trap.eel"));
            Assert.AreEqual(0, PotLocker.DeployedCount(null, "trap.lobster"));
        }

        // ---- available = owned − deployed − aboard ------------------------------------------------

        [Test]
        public void AvailableCount_IsOwnedMinusDeployedMinusAboard()
        {
            var save = NewSave();
            PotLocker.AddOwned(save, "trap.lobster", 3);
            save.PlacedTraps.Add(Deployed("trap.lobster", "a"));

            Assert.AreEqual(2, PotLocker.AvailableCount(save, "trap.lobster", 0), "3 owned − 1 wet");
            Assert.AreEqual(1, PotLocker.AvailableCount(save, "trap.lobster", 1), "− 1 aboard");
            Assert.AreEqual(2, PotLocker.AvailableCount(save, "trap.lobster", -5),
                "a negative aboard count is clamped to 0, never a phantom grant");
        }

        /// <summary>The re-set flow's stock-neutrality, at the derivation level: haul (DTO out, pot
        /// aboard) and re-set (pot back in the water) each move the SAME owned pot between derived
        /// columns — available stays 0 throughout, with owned untouched. This is why #193's T-re-set
        /// needs no stock write and can never double-count.</summary>
        [Test]
        public void HaulToDeck_ThenReSet_IsStockNeutral_AvailableStaysZero()
        {
            var save = NewSave();
            PotLocker.AddOwned(save, "trap.lobster", 1);

            // Set: the one owned pot goes in the water.
            save.PlacedTraps.Add(Deployed("trap.lobster", "a"));
            Assert.AreEqual(0, PotLocker.AvailableCount(save, "trap.lobster", 0), "her buoy's down — no spare");

            // Haul aboard (#193): the DTO leaves the save, the pot rides the deck.
            save.PlacedTraps.Clear();
            Assert.AreEqual(0, PotLocker.AvailableCount(save, "trap.lobster", 1), "aboard, not spare");

            // Re-set (T sets HER): back in the water, nothing owed, nothing gained.
            save.PlacedTraps.Add(Deployed("trap.lobster", "a2"));
            Assert.AreEqual(0, PotLocker.AvailableCount(save, "trap.lobster", 0), "soaking again — still no spare");
            Assert.AreEqual(1, PotLocker.OwnedCount(save, "trap.lobster"), "owned never moved");
        }

        /// <summary>The legacy instant-land haul (a TrapDef without DeckWork): the pot leaves the water
        /// and nothing rides the deck — she's back in the locker, available again for a fresh set.</summary>
        [Test]
        public void LegacyInstantLandHaul_ReturnsThePotToTheLocker()
        {
            var save = NewSave();
            PotLocker.AddOwned(save, "trap.lobster", 1);
            save.PlacedTraps.Add(Deployed("trap.lobster", "a"));
            Assert.AreEqual(0, PotLocker.AvailableCount(save, "trap.lobster", 0));

            save.PlacedTraps.Clear();   // hauled, catch landed instantly, no deck pot
            Assert.AreEqual(1, PotLocker.AvailableCount(save, "trap.lobster", 0), "the pot is spare again");
        }

        [Test]
        public void AvailableCount_CanReadNegative_CallersGateOnPositive()
        {
            // A hand-edited blob (or a pre-v4 save that dodged adoption) can have more pots wet than
            // owned — available reads negative, which every gate treats as "none spare" (> 0 checks).
            var save = NewSave();
            save.PlacedTraps.Add(Deployed("trap.lobster", "a"));
            Assert.AreEqual(-1, PotLocker.AvailableCount(save, "trap.lobster", 0));
        }
    }
}
