using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using HiddenHarbours.Core;
using HiddenHarbours.UI;

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>
    /// VS-19 — a PlayMode smoke test for the HUD's nav cluster (heading compass + set-&-drift). It
    /// proves the always-on HUD reads the Core heading seam (<see cref="GameServices.ActiveBoat"/>) at
    /// its ~4 Hz cadence: the cluster is HIDDEN ashore, SHOWN at sea with the right heading/cardinal,
    /// and the set-&-drift read reflects course-over-ground vs heading (crabbing). Reads only Core — a
    /// tiny in-file fake <see cref="IActiveBoatService"/> stands in for the Boats producer.
    /// </summary>
    public class HudCompassSmokeTests
    {
        private sealed class FakeClock : IGameClock
        {
            public double TotalSeconds { get; set; }
            public GameTime Now => new GameTime(TotalSeconds);
            public Season Season => Season.EarlySpring;
            public int Year => 1;
            public int DayOfSeason => 1;
            public Weekday Weekday => Weekday.Monday;
            public bool IsMarketDay => false;
            public float HourOfDay => 6f;
            public float DayFraction => 0.25f;
            public bool IsPaused { get; set; }
            public float TimeScale { get; set; } = 1f;
        }

        private sealed class FakeEnv : IEnvironmentService
        {
            public int WorldSeed => 0;
            public TideProfile ActiveTideProfile { get; set; } = TideProfile.CoddleCove;
            public EnvironmentSample Sample()
                => new EnvironmentSample(new Vector2(0f, 3f), Vector2.zero, 1.2f, SeaState.Calm, 1f);
            public float TideHeightAt(double totalSeconds) => 0f;
        }

        private sealed class FakeActiveBoat : IActiveBoatService
        {
            public bool HasActiveBoat { get; set; }
            public BoatKinematics Next;
            public BoatKinematics Sample() => Next;
        }

        private GameObject _hudGo;

        [SetUp]
        public void SetUp()
        {
            GameServices.Reset();
            GameServices.Clock = new FakeClock();
            GameServices.Environment = new FakeEnv();
        }

        [TearDown]
        public void TearDown()
        {
            GameServices.Reset();
            if (_hudGo != null) Object.Destroy(_hudGo);
        }

        private static Text Label(HudController hud, string field)
        {
            var f = typeof(HudController).GetField(field, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(f, $"field '{field}' not found on HudController");
            return (Text)f.GetValue(hud);
        }

        private HudController MakeHud()
        {
            _hudGo = new GameObject("HUD");
            return _hudGo.AddComponent<HudController>(); // Awake builds the Canvas + labels
        }

        // One env-sample tick is throttled to ~4 Hz; cross it (unscaled) so UpdateNavReads runs.
        private static IEnumerator NextNavTick() => new WaitForSecondsRealtime(0.3f);

        [UnityTest]
        public IEnumerator NavCluster_IsHidden_Ashore()
        {
            GameServices.ActiveBoat = null; // on foot / no boat
            var hud = MakeHud();
            yield return NextNavTick();

            Assert.IsFalse(Label(hud, "_compassLabel").enabled, "no boat → compass hidden");
            Assert.IsFalse(Label(hud, "_setDriftLabel").enabled, "no boat → set-&-drift hidden");
        }

        [UnityTest]
        public IEnumerator NavCluster_ShowsHeading_Aboard()
        {
            var boat = new FakeActiveBoat
            {
                HasActiveBoat = true,
                Next = BoatKinematics.FromBow(Vector2.right, Vector2.zero), // bow East, at rest
            };
            GameServices.ActiveBoat = boat;

            var hud = MakeHud();
            yield return NextNavTick();

            Assert.IsTrue(Label(hud, "_compassLabel").enabled, "aboard → compass shows");
            Assert.AreEqual("→ 090°  E", Label(hud, "_compassLabel").text, "heading reads arrow + degrees + cardinal");
            Assert.AreEqual("COG —", Label(hud, "_setDriftLabel").text, "at rest there is no steady course");
        }

        [UnityTest]
        public IEnumerator SetDrift_ShowsCrabbing_WhenCourseDiffersFromHeading()
        {
            var boat = new FakeActiveBoat
            {
                HasActiveBoat = true,
                // Bow points North, but the hull is set to the NE by wind/current → crabbing to starboard.
                Next = BoatKinematics.FromBow(Vector2.up, new Vector2(1f, 1f)),
            };
            GameServices.ActiveBoat = boat;

            var hud = MakeHud();
            yield return NextNavTick();

            Assert.AreEqual("↑ 000°  N", Label(hud, "_compassLabel").text, "bow points North");
            Assert.AreEqual("COG 045°  → 45° stbd", Label(hud, "_setDriftLabel").text,
                "the player sees the true course-over-ground crab off the heading");
        }

        [UnityTest]
        public IEnumerator NavCluster_HidesAgain_WhenBoatLeftAshore()
        {
            var boat = new FakeActiveBoat
            {
                HasActiveBoat = true,
                Next = BoatKinematics.FromBow(Vector2.up, Vector2.zero),
            };
            GameServices.ActiveBoat = boat;

            var hud = MakeHud();
            yield return NextNavTick();
            Assert.IsTrue(Label(hud, "_compassLabel").enabled, "precondition: shown while aboard");

            boat.HasActiveBoat = false; // disembarked
            yield return NextNavTick();

            Assert.IsFalse(Label(hud, "_compassLabel").enabled, "disembarking hides the compass");
            Assert.IsFalse(Label(hud, "_setDriftLabel").enabled, "disembarking hides the set-&-drift read");
        }
    }
}
