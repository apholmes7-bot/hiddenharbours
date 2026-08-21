using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>
    /// ⭐⭐ THE ACCEPTANCE CRITERION §9.12 NAMES OUTRIGHT: <b>a long steady run raises ZERO fuel events.</b>
    ///
    /// <para><c>BoatFuelStateChanged</c> is a TRANSITION beat, following <c>MooringLineChanged</c>'s stated
    /// convention. The level itself changes every fixed tick — so the failure mode this test exists for is
    /// somebody, quite reasonably, publishing the level so a gauge can watch it. That would be a publish
    /// per tick per boat on the hot path, forever, and it would look completely fine in play. Only a
    /// counter over real fixed steps catches it.</para>
    ///
    /// <para>PlayMode rather than EditMode because the burn runs on <c>FixedUpdate</c> and the whole
    /// question is what happens over a great many of them. Waits are in <b>real seconds of fixed steps</b>,
    /// never frame counts — a frame count is not time.</para>
    ///
    /// <para><b>What this file does NOT test, on purpose:</b> drift, stranding, or a tow. Those belong to
    /// <c>gameplay-systems</c> (§9.0); this lane raises the fact and stops.</para>
    /// </summary>
    public class FuelStateEventsPlayTests
    {
        private readonly List<Object> _spawned = new List<Object>();
        private readonly List<BoatFuelStateChanged> _beats = new List<BoatFuelStateChanged>();

        private void Listen() => EventBus.Subscribe<BoatFuelStateChanged>(OnBeat);

        // ⚠ Unsubscribe, never EventBus.Clear<T>() — Clear removes EVERY handler for the type, including
        // ones the live scene installed, and a test that tears down other people's subscriptions leaves
        // damage that surfaces three suites later as a feature that "just stopped working".
        private void StopListening() => EventBus.Unsubscribe<BoatFuelStateChanged>(OnBeat);

        private void OnBeat(BoatFuelStateChanged e) => _beats.Add(e);

        [SetUp]
        public void SetUp()
        {
            _beats.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            StopListening();
            GameServices.ActiveBoatFuel = null;
            for (int i = 0; i < _spawned.Count; i++)
                if (_spawned[i] != null) Object.DestroyImmediate(_spawned[i]);
            _spawned.Clear();
        }

        [UnityTest]
        public IEnumerator ASteadyRun_RaisesNotOneEvent()
        {
            // A big tank and a modest burn, so nothing can legitimately transition inside the window:
            // the point is that the 100+ fixed steps in between are silent.
            BoatFuelTank tank = NewBoat(Hull("boat.test_steady", capacity: 4000f, burn: 60f));
            yield return null;                                  // Awake / OnEnable across the rig

            tank.SetLitres(4000f);
            Assert.AreEqual(BoatFuelState.Running, tank.State);

            Listen();
            yield return WaitSeconds(3f);                       // ~150 fixed steps at the default rate

            Assert.IsEmpty(_beats,
                $"a steady run raised {_beats.Count} fuel events. BoatFuelStateChanged is a TRANSITION " +
                "beat — the live level is read off the boat, never pushed. A publish per tick is the " +
                "exact per-tick hot-path cost the convention forbids.");
            Assert.AreEqual(BoatFuelState.Running, tank.State, "and she is still simply running");
        }

        [UnityTest]
        public IEnumerator AHullWithNoTank_IsSilentAndBurnsNothing()
        {
            // The standing rule: the playable build runs with ZERO hulls authored. Every hull is in this
            // state today, so "inert" has to mean inert over time, not just at rest.
            BoatFuelTank tank = NewBoat(Hull("boat.test_no_tank", capacity: 0f, burn: 0f));
            yield return null;

            Listen();
            yield return WaitSeconds(2f);

            Assert.IsEmpty(_beats, "a hull with no tank has no fuel state to announce");
            Assert.AreEqual(0f, tank.Litres);
            Assert.IsFalse(tank.HasTank);
        }

        [UnityTest]
        public IEnumerator CrossingTheLowMark_RaisesExactlyOneBeat()
        {
            // The other half of "transitions only": it must still fire ONCE when the state really moves,
            // and then go quiet again while the level keeps falling inside the same band.
            BoatFuelTank tank = NewBoat(Hull("boat.test_low", capacity: 100f, burn: 3600f));
            yield return null;

            // Just above the 15% mark, with a burn of a litre per real second at full throttle.
            tank.SetLitres(16f);
            Assert.AreEqual(BoatFuelState.Running, tank.State);

            Listen();
            yield return WaitSeconds(3f);                       // ~3 L gone: through 15, well clear of 0

            Assert.AreEqual(1, _beats.Count,
                $"expected exactly one Running -> LowFuel beat, got {_beats.Count}");
            Assert.AreEqual(BoatFuelState.Running, _beats[0].From);
            Assert.AreEqual(BoatFuelState.LowFuel, _beats[0].To);
            Assert.AreEqual("boat.test_low", _beats[0].HullId);
            Assert.Less(tank.Litres, 16f, "and she really did burn on the way there");
        }

        // ---- the rig ------------------------------------------------------------------------------

        /// <summary>Wait real seconds of FIXED steps. A frame count is not time.</summary>
        private static IEnumerator WaitSeconds(float seconds)
        {
            float deadline = Time.time + seconds;
            while (Time.time < deadline) yield return new WaitForFixedUpdate();
        }

        private BoatHullDef Hull(string id, float capacity, float burn)
        {
            var h = ScriptableObject.CreateInstance<BoatHullDef>();
            h.Id = id;
            h.Propulsion = PropulsionType.Engine;
            h.FuelCapacityLitres = capacity;
            h.FuelGrade = capacity > 0f ? FuelGrades.Gas : "";
            h.FullThrottleLitresPerHour = burn;
            h.WindExposure = 0f;                                // this suite is about fuel, not weather
            _spawned.Add(h);
            return h;
        }

        /// <summary>A live boat with the throttle wide open — BoatController self-installs the tank in
        /// play mode (the BoatAnchor pattern), which is also what this asserts.</summary>
        private BoatFuelTank NewBoat(BoatHullDef hull)
        {
            var go = new GameObject("boat");
            _spawned.Add(go);
            var boat = go.AddComponent<BoatController>();
            boat.SetHull(hull);

            var tank = go.GetComponent<BoatFuelTank>();
            Assert.IsNotNull(tank, "BoatController must self-install the tank in play mode");

            boat.SetControl(1f, 0f);   // nothing polls input, so this stands for the whole run
            return tank;
        }
    }
}
