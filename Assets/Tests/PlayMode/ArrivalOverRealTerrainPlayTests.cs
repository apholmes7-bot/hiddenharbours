using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using HiddenHarbours.Core;
using HiddenHarbours.App;
using HiddenHarbours.App.Editor;
using HiddenHarbours.Boats;
using HiddenHarbours.World;

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>
    /// ⭐⭐ <b>THE ARRIVAL OVER THE REGION'S OWN WATER — the test that was missing.</b>
    ///
    /// <para><c>ArrivalOpeningPlayTests</c> drives the sequence over a 20 m synthetic route in an empty
    /// scene, and it passes. The owner then built St Peters in a real editor and the boat sailed NORTH
    /// past everything and never docked. Both were true at once, which is the whole lesson: the fixture
    /// proved the STATE MACHINE and nothing about the PASSAGE. A synthetic straight line 20 m long, with
    /// no terrain under it, no environment around it and no turn in it, cannot fail the way a 200 m
    /// buoyed fairway across a real tide does.</para>
    ///
    /// <para>So this one changes exactly the things the fixture faked: the <b>real route</b>
    /// (<see cref="StPetersArrivalOpening.Route"/>), the <b>real seabed</b>
    /// (<see cref="StPetersBuilder.ConfigureTidalTerrain"/>), and a <b>real tide</b> under her — and it
    /// asserts the owner's own acceptance: she gets to the berth, in about the time he was promised.</para>
    ///
    /// <para><b>⚠ It still is not the editor.</b> Nothing here draws, so it cannot catch a pose, a
    /// sorting order or a camera. What it can catch is a boat that does not arrive, which is what it is
    /// for; the eyeball stays the owner's.</para>
    /// </summary>
    public class ArrivalOverRealTerrainPlayTests
    {
        /// <summary>The owner was promised "~30 s passage". Give it three times that before calling it a
        /// failure — this is a regression guard on ARRIVING, not a stopwatch on the pacing, and a red
        /// that is really a loaded machine teaches nobody anything.</summary>
        private const float TimeoutSeconds = 90f;

        private sealed class FakeSave : ISaveService
        {
            private readonly Dictionary<string, bool> _flags = new Dictionary<string, bool>();
            public SaveData Current { get; } = new SaveData();
            public bool GetFlag(string key) => _flags.TryGetValue(key, out bool v) && v;
            public void SetFlag(string key, bool value) => _flags[key] = value;
            public void Save() { }
        }

        /// <summary>A still tide at a chosen level — the region's own swing, held where a test wants it,
        /// so "she arrives at low water" and "she arrives at high water" are two runs rather than a
        /// coin toss.</summary>
        private sealed class HeldTide : IEnvironmentService
        {
            public float Level;
            public int WorldSeed => 0;
            public TideProfile ActiveTideProfile { get; set; }
            public EnvironmentSample Sample() =>
                new EnvironmentSample(Vector2.zero, Vector2.zero, Level, SeaState.Calm, 1f, 0f);
            public float TideHeightAt(double totalSeconds) => Level;
            public float WaterLevelAt(double totalSeconds) => Level;
        }

        private GameObject _root;
        private GameObject _player;
        private TidalTerrain _terrain;
        private ArrivalOpening _opening;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("ArrivalRealFixture");
            _root.AddComponent<AudioListener>();

            var terrainGo = new GameObject("TidalTerrain");
            terrainGo.transform.SetParent(_root.transform);
            _terrain = terrainGo.AddComponent<TidalTerrain>();
            StPetersBuilder.ConfigureTidalTerrain(_terrain);
            GameServices.TidalTerrain = _terrain;

            // ⭐⭐ SHE HAS A BODY, and that is the whole reason this fixture exists beside the other one.
            // PersistentCoreBuilder gives the player a Rigidbody2D and a 0.35 m foot collider; the older
            // arrival fixture gives her a bare transform. The arrival plants her INSIDE the hull's own
            // capsule every frame, so with a body there is a contact for the solver to resolve and
            // without one there is not — which is exactly why a green fixture and a boat that sailed off
            // the map were both true at once. Same shape as the real core, so the same physics happens.
            _player = new GameObject("Player");
            _player.transform.SetParent(_root.transform);
            var prb = _player.AddComponent<Rigidbody2D>();
            prb.gravityScale = 0f;
            var foot = _player.AddComponent<CircleCollider2D>();
            foot.radius = 0.35f;
            foot.offset = new Vector2(0f, -0.7f);
            GameServices.PlayerTransform = _player.transform;
            GameServices.Save = new FakeSave();
        }

        [TearDown]
        public void TearDown()
        {
            if (_root != null) Object.DestroyImmediate(_root);
            GameServices.Save = null;
            GameServices.PlayerTransform = null;
            GameServices.Environment = null;
            GameServices.Reset();
        }

        private ArrivalOpening Begin(float tideLevel)
        {
            GameServices.Environment = new HeldTide { Level = tideLevel };

            var go = new GameObject("ArrivalOpening");
            go.transform.SetParent(_root.transform);
            go.SetActive(false);
            _opening = go.AddComponent<ArrivalOpening>();
            _opening.Configure(
                UnityEditor.AssetDatabase.LoadAssetAtPath<BoatOwnerDef>(
                    StPetersArrivalOpening.SkipperPath),
                StPetersArrivalOpening.Route(),
                StPetersArrivalOpening.Berth(),
                StPetersArrivalOpening.BerthHeadingDegrees(),
                StPetersArrivalOpening.StepAshore(),
                StPetersBuilder.ApproachBedElevation);
            go.SetActive(true);

            Assert.IsTrue(_opening.TryBegin(), "the arrival must start on a fresh save");
            return _opening;
        }

        /// <summary>Her track, sampled once a second — the thing that tells you WHERE it went wrong
        /// rather than only that it did. Printed on failure, because a timeout that says "stuck" cannot
        /// distinguish a boat steering the wrong way from one that never got a throttle.</summary>
        private string Track(List<Vector2> track)
        {
            var sb = new System.Text.StringBuilder("\n  her track, once a second:\n");
            foreach (Vector2 p in track) sb.AppendLine($"    ({p.x,7:F1}, {p.y,7:F1})");
            return sb.ToString();
        }

        private IEnumerator RunToBerth(float tideLevel, string what)
        {
            ArrivalOpening opening = Begin(tideLevel);
            Vector2 berth = StPetersArrivalOpening.Berth();

            var track = new List<Vector2>();
            float deadline = Time.realtimeSinceStartup + TimeoutSeconds;
            float nextSample = 0f;

            while (opening.Current != ArrivalOpening.Phase.HandedOver &&
                   Time.realtimeSinceStartup < deadline)
            {
                if (Time.realtimeSinceStartup >= nextSample && opening.Boat != null)
                {
                    track.Add(opening.Boat.transform.position);
                    nextSample = Time.realtimeSinceStartup + 1f;
                }
                yield return null;
            }

            Assert.AreEqual(ArrivalOpening.Phase.HandedOver, opening.Current,
                $"at {what} the arrival never finished — it is stuck in {opening.Current}. " +
                (opening.Boat != null
                    ? $"She is at {(Vector2)opening.Boat.transform.position}, " +
                      $"{Vector2.Distance(opening.Boat.transform.position, berth):F0} m from the berth " +
                      $"{berth}, heading {ArrivalPilot.HeadingOf(opening.Boat.transform):F0}°, making " +
                      $"{opening.Boat.Velocity.magnitude:F2} m/s, throttle {opening.Boat.Throttle:F2}, " +
                      $"steer {opening.Boat.Steer:F2}, aground={opening.Boat.IsAground}."
                    : "There is no boat.") + Track(track));

            Assert.Less(Vector2.Distance(opening.Boat.transform.position, berth), 2f,
                $"at {what} she came to rest {Vector2.Distance(opening.Boat.transform.position, berth):F1} m " +
                $"from the berth." + Track(track));

            Debug.Log($"[arrival/real] at {what}: docked after " +
                      $"{TimeoutSeconds - (deadline - Time.realtimeSinceStartup):F1} s of real time, " +
                      $"{track.Count} s of track." + Track(track));
        }

        // =============================================================================================

        /// <summary>⭐ The owner's own acceptance, at the tide that makes the passage hardest: she runs
        /// the fairway and ties up at the east berth.</summary>
        [UnityTest]
        public IEnumerator SheRunsTheRealFairwayAndDocks_AtSpringLow()
        {
            yield return RunToBerth(StPetersBuilder.TideMean - StPetersBuilder.TideAmplitude,
                                    "spring low");
        }

        /// <summary>…and at the top of the tide, where the reef is flooded and there is nothing to hold
        /// her to the channel but the helm.</summary>
        [UnityTest]
        public IEnumerator SheRunsTheRealFairwayAndDocks_AtSpringHigh()
        {
            yield return RunToBerth(StPetersBuilder.TideMean + StPetersBuilder.TideAmplitude,
                                    "spring high");
        }

        /// <summary>
        /// 🔴 <b>The passenger is CARGO for the passage, not a body.</b> She keeps a rigidbody and a
        /// foot collider, and the arrival plants her inside the hull's capsule — so unless she is taken
        /// out of the simulation the solver spends the whole passage shoving a 60 kg boat apart from
        /// her, every fixed step. That is a boat thrown off her track and a passenger pinned in place:
        /// the two defects reported off the owner's walk, from one cause.
        /// </summary>
        [UnityTest]
        public IEnumerator ThePassengerIsTakenOutOfThePhysicsWorld_SoSheCannotShoveTheBoat()
        {
            var body = _player.GetComponent<Rigidbody2D>();
            Assert.IsTrue(body.simulated, "precondition: she starts as a simulated body");

            ArrivalOpening opening = Begin(0f);
            yield return new WaitForFixedUpdate();

            Assert.IsFalse(body.simulated,
                "the passenger is still simulated while being carried — she is inside the hull's " +
                "collider, so every fixed step the solver will shove the boat off her track");

            // …and she is handed back a body when she steps ashore, or she can never walk again.
            float deadline = Time.realtimeSinceStartup + TimeoutSeconds;
            while (opening.Current != ArrivalOpening.Phase.HandedOver &&
                   Time.realtimeSinceStartup < deadline) yield return null;

            Assert.AreEqual(ArrivalOpening.Phase.HandedOver, opening.Current,
                "the arrival had to finish for the release to mean anything");
            Assert.IsTrue(body.simulated,
                "she was left un-simulated after stepping ashore — she would walk through the world");
        }

        /// <summary>
        /// 🔴 <b>She points where she is GOING, from the first frame.</b> A boat spawned on the default
        /// identity rotation points due NORTH, and with the helm's authority scaling on way she would
        /// run a long way north before she could turn — which is exactly what the owner watched. Pinned
        /// against the route rather than against a number, so re-routing the fairway re-checks it.
        /// </summary>
        [UnityTest]
        public IEnumerator SheStartsPointedDownTheRoute_NotDueNorth()
        {
            ArrivalOpening opening = Begin(0f);
            yield return null;

            Vector2[] route = StPetersArrivalOpening.Route();
            float wanted = ArrivalPilot.CompassOf(route[1] - route[0]);
            float actual = ArrivalPilot.HeadingOf(opening.Boat.transform);

            Assert.Less(Mathf.Abs(ArrivalPilot.Wrap180(actual - wanted)), 5f,
                $"she starts heading {actual:F1}° when the first leg bears {wanted:F1}° — " +
                (Mathf.Abs(ArrivalPilot.Wrap180(actual)) < 5f
                    ? "and DUE NORTH is the identity rotation, i.e. nothing set her heading at all."
                    : "something set it wrong."));

            Assert.Greater(Vector2.Dot(opening.Boat.Velocity.normalized,
                                       (route[1] - route[0]).normalized), 0.9f,
                "…and she must be making way ALONG the first leg, not across it");
        }
    }
}
