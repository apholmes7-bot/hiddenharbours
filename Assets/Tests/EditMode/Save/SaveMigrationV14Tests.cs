using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// SAVE v14 — what is in each hull's tank (<c>docs/design/fuel-and-refuelling.md</c> §9.3).
    ///
    /// <para><b>⭐ The whole schema is one sparse list and one rule: a hull with no row is BRIM-FULL.</b>
    /// That rule does two jobs, which is why it is worth this many tests. It is how a v13 save loads —
    /// that player's boat had no tank, burned nothing and could not run dry, so deciding on load that she
    /// is empty would <i>invent a consequence</i> for them — and it is also how
    /// <c>GameConfig.Fuel.NewBoatArrivesFull</c> is honoured, because a boat you just bought has no row
    /// yet. One mechanism, two requirements, nothing to drift.</para>
    ///
    /// <para><b>And it is why the writer must REMOVE a row rather than leave a stale full one:</b> "no
    /// row" must never come to mean two different things.</para>
    /// </summary>
    public class SaveMigrationV14Tests
    {
        private readonly List<Object> _spawned = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            GameServices.Save = null;
            GameServices.ActiveBoatFuel = null;
            for (int i = 0; i < _spawned.Count; i++)
                if (_spawned[i] != null) Object.DestroyImmediate(_spawned[i]);
            _spawned.Clear();
        }

        // ---- the migration itself ---------------------------------------------------------------

        [Test]
        public void CurrentVersion_IsAtLeastFourteen_BecauseTheTankShipped()
        {
            Assert.GreaterOrEqual(SaveMigration.CurrentVersion, 14,
                "the fuel level is saved state (§9.5.3: path-dependent, not recomputable), so the schema " +
                "carries it and the version says so");
        }

        [Test]
        public void AV13Save_MigratesToCurrent_AndGainsAnEmptyFuelList()
        {
            var data = SaveMigration.Migrate(new SaveData { SchemaVersion = 13 });

            Assert.AreEqual(SaveMigration.CurrentVersion, data.SchemaVersion);
            Assert.IsNotNull(data.HullFuel);
            Assert.IsEmpty(data.HullFuel,
                "⭐ the empty list IS the migration: it reads as 'every boat you own is full', which is " +
                "exactly what a player who never had a tank should get back");
        }

        [Test]
        public void TheMigration_BackFillsNothing_EvenWithBoatsOwned()
        {
            // ⚠ The contract: a step only ADDS. Walking OwnedBoats to write a "full" row for each would
            // say what the absence already says — and a row is DERIVED state, one re-tune away from being
            // wrong (a hull whose capacity the owner later raises would stay pinned at the old number
            // instead of reading as full).
            var data = SaveMigration.Migrate(new SaveData
            {
                SchemaVersion = 13,
                OwnedBoats = new List<string> { "boat.dory", "boat.dory_outboard", "boat.punt" },
                ActiveHullId = "boat.dory_outboard",
            });

            Assert.IsEmpty(data.HullFuel, "three owned boats, and still not one invented row");
            Assert.AreEqual(3, data.OwnedBoats.Count, "and nothing existing was touched");
            Assert.AreEqual("boat.dory_outboard", data.ActiveHullId);
        }

        [Test]
        public void TheMigration_ReinterpretsNoExistingValue()
        {
            // The v11→v12 discipline, applied here: everything a v13 save carried must survive verbatim.
            var data = SaveMigration.Migrate(new SaveData
            {
                SchemaVersion = 13,
                Money = 417,
                WornOutfitId = "outfit.oilskins",
                RestRegion = "region.st_peters",
            });

            Assert.AreEqual(417, data.Money);
            Assert.AreEqual("outfit.oilskins", data.WornOutfitId);
            Assert.AreEqual("region.st_peters", data.RestRegion);
        }

        [Test]
        public void MigratingTwice_ChangesNothingTheSecondTime()
        {
            var first = SaveMigration.Migrate(new SaveData { SchemaVersion = 13 });
            first.HullFuel.Add(new HullFuelDto("boat.dory_outboard", 3.5f));

            var second = SaveMigration.Migrate(first);

            Assert.AreEqual(SaveMigration.CurrentVersion, second.SchemaVersion);
            Assert.AreEqual(1, second.HullFuel.Count, "idempotent — a re-run must not wipe or duplicate");
            Assert.AreEqual(3.5f, second.HullFuel[0].Litres, 1e-4f);
        }

        [Test]
        public void ANullFuelList_IsRepaired_NotThrownOn()
        {
            // A hand-edited or partial JSON can omit a reference-typed field entirely.
            var data = SaveMigration.Migrate(new SaveData
            {
                SchemaVersion = SaveMigration.CurrentVersion,
                HullFuel = null,
            });

            Assert.IsNotNull(data.HullFuel);
        }

        // ---- the tank against a real save blob ---------------------------------------------------

        [Test]
        public void AHullWithNoRow_LoadsFull()
        {
            var tank = TankFor(Hull("boat.dory_outboard", 10f), FreshSave());
            Assert.AreEqual(10f, tank.Litres, 1e-4f);
        }

        [Test]
        public void ADryBoat_RoundTripsAsAnExplicitZero()
        {
            // ⭐ The reason the writer records a row for anything that is NOT full: without it, "no row"
            // would have to mean both "full" and "empty", and a dry boat would silently refuel herself
            // across a reload — which is the single most consequential state in the feature.
            var save = FreshSave();
            var tank = TankFor(Hull("boat.dory_outboard", 10f), save);

            tank.Draw(10f);
            Assert.AreEqual(0f, tank.Litres, 1e-4f);

            Assert.AreEqual(1, save.Current.HullFuel.Count, "a dry boat MUST be written down");
            Assert.AreEqual("boat.dory_outboard", save.Current.HullFuel[0].HullId);
            Assert.AreEqual(0f, save.Current.HullFuel[0].Litres, 1e-4f);

            var reloaded = TankFor(Hull("boat.dory_outboard", 10f), save);
            Assert.AreEqual(0f, reloaded.Litres, 1e-4f, "and she must still be dry after a reload");
        }

        [Test]
        public void APartTank_RoundTrips()
        {
            var save = FreshSave();
            var tank = TankFor(Hull("boat.punt", 25f), save);
            tank.Draw(9f);

            Assert.AreEqual(16f, TankFor(Hull("boat.punt", 25f), save).Litres, 1e-3f);
        }

        [Test]
        public void FillingHerToTheBrim_REMOVESHerRow()
        {
            // The writer rule's second half. A stale full row is not merely redundant: a hull whose
            // capacity the owner later RAISES would stay pinned at the old litre count instead of
            // reading as full, and the boat would quietly lose the range the re-tune gave her.
            var save = FreshSave();
            var tank = TankFor(Hull("boat.punt", 25f), save);

            tank.Draw(10f);
            Assert.AreEqual(1, save.Current.HullFuel.Count, "part-full, so she is written down");

            tank.Deliver(10f);
            Assert.IsEmpty(save.Current.HullFuel, "brim-full again, so the row goes away");

            var bigger = TankFor(Hull("boat.punt", 40f), save);   // the owner re-tuned her tank up
            Assert.AreEqual(40f, bigger.Litres, 1e-4f, "and she reads as full at the NEW capacity");
        }

        [Test]
        public void AStaleRow_IsClampedToTheHullsCurrentCapacity()
        {
            // A row is only as good as the Def it was written against. Re-tune a tank SMALLER and the
            // saved litres must not survive as more than she can hold.
            var save = FreshSave();
            save.Current.HullFuel.Add(new HullFuelDto("boat.dory_outboard", 900f));

            Assert.AreEqual(10f, TankFor(Hull("boat.dory_outboard", 10f), save).Litres, 1e-4f);
        }

        [Test]
        public void AHullWithNoTank_NeverWritesARow()
        {
            // The rowed dory has no tank, so she is not "full" — she simply has none, and a row for her
            // would be a fiction a later authoring pass would silently inherit.
            var save = FreshSave();
            var tank = TankFor(Hull("boat.dory", 0f), save);

            tank.Deliver(5f);
            tank.Draw(5f);

            Assert.IsEmpty(save.Current.HullFuel);
        }

        [Test]
        public void TheTankIsPerBOAT_SoTradingUpAndBackFindsTheFuelWhereYouLeftIt()
        {
            // ⭐ What keying by hull id buys: half a tank left in the dory is still there when you come
            // back to her, and the Cape Islander you bought in between arrives full on her own row's
            // absence. This is also the ordering guard — OwnedFleet re-points the CONTROLLER before the
            // tank hears about the swap, so banking under "the hull she is wearing" would file the
            // dory's fuel under the Cape Islander's id.
            var save = FreshSave();
            var dory = Hull("boat.dory_outboard", 10f);
            var cape = Hull("boat.cape_islander", 90f);

            var tank = TankFor(dory, save);
            tank.Draw(6f);                                  // 4 L left in the dory
            Assert.AreEqual(4f, tank.Litres, 1e-4f);

            tank.SwitchToHull(cape);
            Assert.AreEqual(90f, tank.Litres, 1e-4f, "the new boat arrives full — she has no row");

            tank.Draw(40f);                                 // 50 L in the Cape Islander
            tank.SwitchToHull(dory);

            Assert.AreEqual(4f, tank.Litres, 1e-4f, "the dory still has the 4 L you left in her");

            tank.SwitchToHull(cape);
            Assert.AreEqual(50f, tank.Litres, 1e-4f, "and the Cape Islander still has her 50 L");
        }

        [Test]
        public void TheSaveCarriesOneRowPerHull_NotOnePerSwap()
        {
            var save = FreshSave();
            var dory = Hull("boat.dory_outboard", 10f);
            var cape = Hull("boat.cape_islander", 90f);
            var tank = TankFor(dory, save);

            for (int i = 0; i < 5; i++)
            {
                tank.Draw(0.5f);
                tank.SwitchToHull(cape);
                tank.Draw(0.5f);
                tank.SwitchToHull(dory);
            }

            Assert.AreEqual(2, save.Current.HullFuel.Count,
                "ten swaps, two boats, two rows — the list is keyed, not appended to");
        }

        // ---- helpers -----------------------------------------------------------------------------

        private BoatHullDef Hull(string id, float capacity)
        {
            var h = ScriptableObject.CreateInstance<BoatHullDef>();
            h.Id = id;
            h.FuelCapacityLitres = capacity;
            h.FuelGrade = capacity > 0f ? FuelGrades.Gas : "";
            h.FullThrottleLitresPerHour = capacity > 0f ? 23f : 0f;
            _spawned.Add(h);
            return h;
        }

        /// <summary>A tank wired to a save blob, freshly primed — i.e. what a scene load produces.</summary>
        private BoatFuelTank TankFor(BoatHullDef hull, ISaveService save)
        {
            GameServices.Save = save;
            var go = new GameObject("boat");
            _spawned.Add(go);
            var tank = go.AddComponent<BoatFuelTank>();
            tank.Configure(hull);
            return tank;
        }

        private static StubSave FreshSave() =>
            new StubSave(SaveMigration.Migrate(new SaveData { SchemaVersion = 13 }));

        /// <summary>A save service that is only a blob — which is all the tank asks of it.</summary>
        private sealed class StubSave : ISaveService
        {
            public StubSave(SaveData data) { Current = data; }
            public SaveData Current { get; }
            public bool GetFlag(string key) => false;
            public void SetFlag(string key, bool value) { }
            public void Save() { }
        }
    }
}
