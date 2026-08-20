using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// RUNNING DRY — the states, the event and the field that decides who gets rescued
    /// (<c>docs/design/fuel-and-refuelling.md</c> §9.9, acceptance sheet §9.12).
    ///
    /// <para><b>⭐ The load-bearing test in this file is the `OarPower: 300` trap.</b> Six engine hulls in
    /// the shipped fleet — including a 90-tonne side dragger — carry a non-zero `OarPower` that nobody
    /// ever zeroed, because 300 is the field's default. Anything that reads `OarPower > 0` as "she has
    /// oars" lets a dragger row home from a dead engine, and the bug looks like a physics bug for a week.
    /// `CanRowHome` exists to be that answer instead, and these tests pin both ends of it.</para>
    ///
    /// <para><b>What is NOT here, deliberately:</b> drift, stranding, and the tow. Those are
    /// <c>gameplay-systems</c>' (§9.0) — this lane raises a fuel fact and prices nothing towed. If this
    /// suite ever grows a stranding assertion, the handoff failed.</para>
    /// </summary>
    public class BoatFuelStateTests
    {
        private readonly List<Object> _spawned = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            GameServices.ActiveBoatFuel = null;
            GameServices.Save = null;
            for (int i = 0; i < _spawned.Count; i++)
                if (_spawned[i] != null) Object.DestroyImmediate(_spawned[i]);
            _spawned.Clear();
        }

        private const float Low = 0.15f;      // GameConfig.Fuel.LowFuelFraction, as shipped

        // ---- the classification -----------------------------------------------------------------

        [Test]
        public void AFullTank_IsRunning_AndAnEmptyOneIsDry()
        {
            Assert.AreEqual(BoatFuelState.Running, BoatFuel.Classify(10f, 10f, Low));
            Assert.AreEqual(BoatFuelState.Dry, BoatFuel.Classify(0f, 10f, Low));
        }

        [Test]
        public void LowFuelArmsAtTheConfiguredFraction_Inclusive()
        {
            // At the line she is low, not still running: the warning should arrive a moment early rather
            // than a moment late, which is the forgiving direction on the one axis that can strand you.
            Assert.AreEqual(BoatFuelState.Running, BoatFuel.Classify(1.6f, 10f, Low));
            Assert.AreEqual(BoatFuelState.LowFuel, BoatFuel.Classify(1.5f, 10f, Low));
            Assert.AreEqual(BoatFuelState.LowFuel, BoatFuel.Classify(0.4f, 10f, Low));
        }

        [Test]
        public void AHullWithNoTank_IsRunning_NotDry()
        {
            // ⭐ She is not dry — she has nothing to be dry ABOUT. Reporting the rowed dory as Dry would
            // hand the danger lane a breakdown on the first tick of every new game.
            Assert.AreEqual(BoatFuelState.Running, BoatFuel.Classify(0f, 0f, Low));
        }

        [Test]
        public void TheShippedLowFraction_LeavesRealRoomToTurnBack()
        {
            // The warning is only worth having if it arrives while the choice is still open. At the
            // §9.6.2 reference cruise duty, a dory's last 15% is minutes of running, not seconds.
            var cfg = FuelSettings.Default;
            float cruiseLitresPerHour = FuelBurn.LitresPerHour(23f, 0.7f, 0.5f, 0f, 3f / 7f, cfg);
            float minutesLeft = (10f * cfg.LowFuelFraction) / cruiseLitresPerHour * 60f;

            Assert.Greater(minutesLeft, 3f,
                "the low-fuel telegraph must leave the player time to DO something about it — that is " +
                "the whole difference between a warning and an announcement");
        }

        // ---- the OarPower trap, pinned both ways -------------------------------------------------

        [Test]
        public void CanRowHome_IsItsOwnField_AndDefaultsToFalse()
        {
            var fresh = ScriptableObject.CreateInstance<BoatHullDef>();
            _spawned.Add(fresh);
            Assert.IsFalse(fresh.CanRowHome,
                "a hull nobody has thought about must never be accidentally rescued");
            Assert.AreEqual(300f, fresh.OarPower,
                "…and OarPower's default is exactly the 300 that makes inferring from it a trap");
        }

        [Test]
        public void TheShippedFleet_HonoursTheOarPowerTrap()
        {
            // ⭐⭐ THE PIN §9.12 ASKS FOR, on the real assets. boat.dory_outboard matters most: she is
            // PropulsionType.Engine, so a naive "does she use the oar helm" test would strand her — and
            // she is the one boat canon promises can always get you home, on the player's first motor,
            // at the moment the fuel economy introduces itself.
            Assert.IsTrue(HullById("boat.dory_outboard").CanRowHome,
                "the motorised dory MUST be able to row home — getting this wrong breaks the opening");
            Assert.IsFalse(HullById("boat.side_dragger").CanRowHome,
                "a 90-tonne side dragger does not row home, whatever her OarPower says");

            Assert.Greater(HullById("boat.side_dragger").OarPower, 0f,
                "…and the trap is still LIVE in the data — which is why CanRowHome is a field and not " +
                "an inference. (Zeroing OarPower on the hulls that never row is gameplay-systems', " +
                "independently of this feature; §9.14 flags it.)");
        }

        [Test]
        public void ExactlyTheSmallCraft_CanRowHome()
        {
            string[] expected =
            {
                "boat.dory", "boat.dory_outboard", "boat.punt", "boat.punt_upgraded", "boat.fishing_skiff",
            };

            var rowable = new List<string>();
            foreach (var h in AllHulls())
                if (h.CanRowHome) rowable.Add(h.Id);
            rowable.Sort(System.StringComparer.Ordinal);

            var want = new List<string>(expected);
            want.Sort(System.StringComparer.Ordinal);

            CollectionAssert.AreEqual(want, rowable,
                "canon: the dory and the punt row or sail home fuel-free, bigger boats cannot " +
                "(boats-and-navigation.md §3.6). The fishing skiff is the one debatable entry and is " +
                "included as a PROPOSAL — §9.15 owner call 5.");
        }

        // ---- the event ---------------------------------------------------------------------------

        [Test]
        public void TheEventCarriesWhatTheDangerLaneNeeds_WithoutItReachingIntoBoats()
        {
            var e = new BoatFuelStateChanged("boat.dory_outboard", BoatFuelState.LowFuel,
                                             BoatFuelState.Dry, 0f, 0f, canRowHome: true);

            Assert.AreEqual("boat.dory_outboard", e.HullId);
            Assert.AreEqual(BoatFuelState.Dry, e.To);
            Assert.IsTrue(e.CanRowHome,
                "ADRIFT vs STRANDED is decided from this flag, so it rides on the beat — otherwise the " +
                "danger lane would have to find a BoatHullDef, which is a module it cannot reference");
        }

        [Test]
        public void PouringACanIn_AnnouncesTheWayBackToRunning()
        {
            // Dry -> Running is the one transition the player CAUSES, and the one a listening engine
            // note most wants. It must fire on the pour, not on the next tick.
            var tank = Tank(Hull("boat.dory_outboard", 10f, rowsHome: true));
            tank.SetLitres(0f);
            Assert.AreEqual(BoatFuelState.Dry, tank.State);

            var seen = new List<BoatFuelStateChanged>();
            void Handler(BoatFuelStateChanged e) => seen.Add(e);
            EventBus.Subscribe<BoatFuelStateChanged>(Handler);
            try
            {
                tank.Deliver(9f);

                Assert.AreEqual(1, seen.Count, "one beat, for one transition");
                Assert.AreEqual(BoatFuelState.Dry, seen[0].From);
                Assert.AreEqual(BoatFuelState.Running, seen[0].To);
                Assert.AreEqual(9f, seen[0].Litres, 1e-3f);
                Assert.IsTrue(seen[0].CanRowHome);
            }
            finally { EventBus.Unsubscribe<BoatFuelStateChanged>(Handler); }
        }

        [Test]
        public void ToppingUpAFullBoat_AnnouncesNothing()
        {
            // ⚠ Not a transition, so not a beat. EventBus.Unsubscribe rather than Clear<T> in the
            // teardown: Clear<T>() removes EVERY handler, including ones other suites installed.
            var tank = Tank(Hull("boat.punt", 25f, rowsHome: true));
            Assert.AreEqual(BoatFuelState.Running, tank.State);

            int beats = 0;
            void Handler(BoatFuelStateChanged _) => beats++;
            EventBus.Subscribe<BoatFuelStateChanged>(Handler);
            try
            {
                tank.Deliver(5f);
                tank.Deliver(5f);
                Assert.AreEqual(0, beats, "she was full and is still full — nothing happened");
            }
            finally { EventBus.Unsubscribe<BoatFuelStateChanged>(Handler); }
        }

        [Test]
        public void AHullWithNoTank_NeverAnnouncesAnything()
        {
            var tank = Tank(Hull("boat.dory", 0f, rowsHome: true));

            int beats = 0;
            void Handler(BoatFuelStateChanged _) => beats++;
            EventBus.Subscribe<BoatFuelStateChanged>(Handler);
            try
            {
                tank.Deliver(5f);
                tank.Draw(5f);
                Assert.AreEqual(0, beats, "the rowed dory has no fuel state to change");
            }
            finally { EventBus.Unsubscribe<BoatFuelStateChanged>(Handler); }
        }

        // ---- helpers ------------------------------------------------------------------------------

        private static List<BoatHullDef> AllHulls()
        {
            var list = new List<BoatHullDef>();
            foreach (string guid in UnityEditor.AssetDatabase.FindAssets(
                         "t:BoatHullDef", new[] { "Assets/_Project/Data" }))
            {
                var h = UnityEditor.AssetDatabase.LoadAssetAtPath<BoatHullDef>(
                    UnityEditor.AssetDatabase.GUIDToAssetPath(guid));
                if (h != null && !string.IsNullOrEmpty(h.Id)) list.Add(h);
            }
            return list;
        }

        private static BoatHullDef HullById(string id)
        {
            foreach (var h in AllHulls())
                if (h.Id == id) return h;
            Assert.Fail($"{id} is not in Data/ — this test names shipped hulls by their stable ids");
            return null;
        }

        private BoatHullDef Hull(string id, float capacity, bool rowsHome)
        {
            var h = ScriptableObject.CreateInstance<BoatHullDef>();
            h.Id = id;
            h.FuelCapacityLitres = capacity;
            h.FuelGrade = capacity > 0f ? FuelGrades.Gas : "";
            h.FullThrottleLitresPerHour = capacity > 0f ? 23f : 0f;
            h.CanRowHome = rowsHome;
            _spawned.Add(h);
            return h;
        }

        private BoatFuelTank Tank(BoatHullDef hull)
        {
            var go = new GameObject("boat");
            _spawned.Add(go);
            var tank = go.AddComponent<BoatFuelTank>();
            tank.Configure(hull);
            return tank;
        }
    }
}
