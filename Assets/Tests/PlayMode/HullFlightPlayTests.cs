using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using HiddenHarbours.Core;
using HiddenHarbours.Boats;

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>
    /// <b>A HULL LEAVES THE WATER ON THE LIVE PHYSICS LOOP</b> (owner, 2026-11-12). The EditMode file
    /// next door pins the law. These prove the law reaches a real boat: a small fast hull driven into a
    /// strong sea is reported airborne, lands, and loses speed to the slam; a dory at walking pace in
    /// the same sea is never reported off the water; and the switch turns it all off.
    ///
    /// <para>Every wait is on elapsed <see cref="Time.time"/>, never a frame count.</para>
    /// </summary>
    public class HullFlightPlayTests
    {
        /// <summary>A strong onshore sea, deep water everywhere, a still tide.</summary>
        private sealed class StrongSea : IEnvironmentService
        {
            public int WorldSeed => 7;
            public TideProfile ActiveTideProfile { get; set; }
            public EnvironmentSample Sample()
                => new EnvironmentSample(new Vector2(12f, 0f), Vector2.zero, 0f, SeaState.Rough, 1f, 0.8f);
            public float TideHeightAt(double totalSeconds) => 0f;
            public float WaterLevelAt(double totalSeconds) => 0f;
        }

        private sealed class DeepWater : ITidalTerrain
        {
            public float ElevationAt(Vector2 worldPos) => -40f;
        }

        private sealed class TestClock : IGameClock
        {
            public double LiveOrigin;
            public double TotalSeconds => Time.timeAsDouble - LiveOrigin;
            public GameTime Now => new GameTime(TotalSeconds);
            public Season Season => Season.EarlySpring;
            public int Year => 1;
            public int DayIndex => 0;
            public int DayOfSeason => 1;
            public Weekday Weekday => Weekday.Monday;
            public bool IsMarketDay => false;
            public float HourOfDay => 0f;
            public float DayFraction => 0f;
            public bool IsPaused { get; set; }
            public float TimeScale { get; set; } = 1f;
        }

        private readonly List<Object> _spawned = new();
        private GameConfig _config;
        private int _left, _landed;
        private float _worstImpact, _speedAfterWorstSlam;
        private Rigidbody2D _watched;

        [SetUp]
        public void SetUp()
        {
            GameServices.Reset();
            InteractionGate.Reset();
            HelmKeyCapture.Reset();
            EventBus.Clear<ControlModeChanged>();
            EventBus.Clear<HullLeftWater>();
            EventBus.Clear<HullLanded>();

            _config = ScriptableObject.CreateInstance<GameConfig>();
            _spawned.Add(_config);
            GameServices.TidalTerrain = new DeepWater();
            GameServices.Environment = new StrongSea();
            GameServices.Clock = new TestClock { LiveOrigin = Time.timeAsDouble };
            GameServices.Config = _config;

            _left = _landed = 0;
            _worstImpact = 0f;
            _speedAfterWorstSlam = float.NaN;
            EventBus.Subscribe<HullLeftWater>(OnLeft);
            EventBus.Subscribe<HullLanded>(OnLanded);
        }

        [TearDown]
        public void TearDown()
        {
            EventBus.Unsubscribe<HullLeftWater>(OnLeft);
            EventBus.Unsubscribe<HullLanded>(OnLanded);
            EventBus.Clear<HullLeftWater>();
            EventBus.Clear<HullLanded>();
            EventBus.Clear<ControlModeChanged>();
            InteractionGate.Reset();
            HelmKeyCapture.Reset();
            GameServices.Reset();
            foreach (var o in _spawned) if (o != null) Object.Destroy(o);
            _spawned.Clear();
        }

        private void OnLeft(HullLeftWater e) => _left++;

        private void OnLanded(HullLanded e)
        {
            _landed++;
            if (e.Impact01 > _worstImpact && _watched != null)
            {
                _worstImpact = e.Impact01;
                _speedAfterWorstSlam = _watched.linearVelocity.magnitude;   // read after the slam took its share
            }
        }

        // A hull with no drag and no windage, so the only things that change her speed are the sea's
        // push and the slam.
        private BoatController NewBoat(string id, float length, float draught, float massKg, out Rigidbody2D rb)
        {
            var hull = ScriptableObject.CreateInstance<BoatHullDef>();
            hull.Id = id;
            hull.DisplayName = id;
            hull.LengthMeters = length;
            hull.DraughtMeters = draught;
            hull.MassKg = massKg;
            hull.ForwardDrag = 0f;
            hull.LateralDrag = 0f;
            hull.WindExposure = 0f;
            _spawned.Add(hull);

            var go = new GameObject("Boat");
            go.transform.rotation = Quaternion.Euler(0f, 0f, 90f);   // bow (transform.up) toward -X: into the sea
            _spawned.Add(go);
            var boat = go.AddComponent<BoatController>();
            rb = go.GetComponent<Rigidbody2D>();
            boat.SetHull(hull);
            GameServices.Helm.SetPlayersBoat(boat);
            return boat;
        }

        // Hold her at `speed` into the sea for `seconds`, and say whether she was ever reported airborne.
        private IEnumerator DriveIntoTheSea(BoatController boat, Rigidbody2D rb, float speed, float seconds,
                                            System.Action<bool, float> done)
        {
            bool everAirborne = false;
            float mostAir = 0f;
            float deadline = Time.time + seconds;
            while (Time.time < deadline)
            {
                rb.linearVelocity = new Vector2(-speed, 0f);
                rb.angularVelocity = 0f;
                rb.rotation = 90f;
                yield return new WaitForFixedUpdate();
                everAirborne |= boat.IsAirborne;
                mostAir = Mathf.Max(mostAir, boat.FlightClearanceMeters);
            }
            done(everAirborne, mostAir);
        }

        [UnityTest]
        public IEnumerator ASmallFastHull_DrivenIntoAStrongSea_Flies_Lands_AndLosesSpeedToTheSlam()
        {
            const float speed = 16f;
            BoatController boat = NewBoat("boat.test_small_fast", 3.2f, 0.3f, 250f, out Rigidbody2D rb);
            _watched = rb;
            yield return new WaitForFixedUpdate();

            bool flew = false; float mostAir = 0f;
            yield return DriveIntoTheSea(boat, rb, speed, 12f, (a, m) => { flew = a; mostAir = m; });

            TestContext.WriteLine($"left {_left}, landed {_landed}, most air {mostAir:F2} m, worst impact {_worstImpact:F2}, " +
                                  $"speed after it {_speedAfterWorstSlam:F2} m/s");
            Assert.Greater(_left, 0, "A 3.2 m hull at 16 m/s into a strong sea must leave the water.");
            Assert.IsTrue(flew, "BoatController.IsAirborne must read true during a flight.");
            Assert.Greater(mostAir, 0.05f, "…with air under her keel.");
            Assert.Less(mostAir, 6f, "…and not an absurd amount.");
            Assert.That(_landed, Is.InRange(_left - 1, _left), "Every flight ends in a landing.");
            Assert.Greater(_worstImpact, 0f, "At least one landing is a slam.");
            Assert.Less(_speedAfterWorstSlam, speed, "The slam takes speed off her.");
        }

        [UnityTest]
        public IEnumerator ADoryAtWalkingPace_InTheSameSea_NeverLeavesTheWater()
        {
            BoatController boat = NewBoat("boat.test_dory", 4.5f, 0.3f, 400f, out Rigidbody2D rb);
            _watched = rb;
            yield return new WaitForFixedUpdate();

            bool flew = true; float mostAir = 1f;
            yield return DriveIntoTheSea(boat, rb, 1.5f, 12f, (a, m) => { flew = a; mostAir = m; });

            Assert.AreEqual(0, _left + _landed, "A dory at walking pace raised a flight event.");
            Assert.IsFalse(flew, "…and was reported airborne.");
            Assert.AreEqual(0f, mostAir, "…and reported air under her keel.");
        }

        [UnityTest]
        public IEnumerator SwitchedOff_TheFastHullStaysOnTheWater()
        {
            HullFlightSettings off = HullFlightSettings.Default;
            off.Enabled = false;
            _config.HullFlight = off;
            BoatController boat = NewBoat("boat.test_small_fast", 3.2f, 0.3f, 250f, out Rigidbody2D rb);
            _watched = rb;
            yield return new WaitForFixedUpdate();

            bool flew = true; float mostAir = 1f;
            yield return DriveIntoTheSea(boat, rb, 16f, 6f, (a, m) => { flew = a; mostAir = m; });

            Assert.AreEqual(0, _left + _landed, "Switched off, nothing flies.");
            Assert.IsFalse(flew);
            Assert.AreEqual(0f, mostAir);
        }
    }
}
