using System.Collections.Generic;
using HiddenHarbours.Core;
using HiddenHarbours.World;
using NUnit.Framework;
using UnityEngine;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// A placed wash station's wiring (washrooms plan section 3): the press begins a wash at the station's
    /// Def through the Core service, a second press or walking off ends it, it ends itself at the Def's own
    /// floor, it refuses when there is nothing left to wash off, and it lets go when the service stopped by
    /// another road. Real <see cref="CleanlinessService"/> over an in-memory save and a hand-wound clock; every
    /// number is the test's own. One game hour is <see cref="Hour"/> clock seconds.
    /// </summary>
    public class WashStationPointTests
    {
        private const double Hour = 100d;
        private const float Eps = 1e-4f;

        private readonly List<Object> _made = new List<Object>();
        private readonly List<string> _notices = new List<string>();
        private TraitFixtures.MemorySave _save;
        private Clock _clock;
        private CleanlinessService _svc;
        private WashStationDef _sink, _shower, _toilet;

        [SetUp]
        public void SetUp()
        {
            GameServices.Reset();
            EventBus.Clear<DevNotice>();
            EventBus.Subscribe<DevNotice>(OnNotice);
            _save = new TraitFixtures.MemorySave();
            _clock = new Clock();
            _sink = CleanlinessMathTests.Station(_made, "washstation.sink", WashStationKind.Sink, 1f, 0.8f);
            _shower = CleanlinessMathTests.Station(_made, "washstation.shower", WashStationKind.Shower, 3f, 1f);
            _toilet = CleanlinessMathTests.Station(_made, "washstation.toilet", WashStationKind.Toilet, 0f, 0f, sitOnly: true);
            CleanlinessSettings settings = CleanlinessMathTests.Settings();
            settings.Curve.RisePerGameHour = 0f;   // only washing moves dirt here
            _svc = new CleanlinessService(() => _save, () => _clock, () => settings,
                                          () => new[] { _sink, _shower, _toilet }, () => Hour);
            GameServices.Cleanliness = _svc;
            _notices.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            _svc.EndWash();
            WashStationPoint.Active?.Watch(null);   // lets go: the service is no longer washing
            EventBus.Unsubscribe<DevNotice>(OnNotice);
            EventBus.Clear<DevNotice>();
            GameServices.Reset();
            foreach (Object o in _made) if (o != null) Object.DestroyImmediate(o);
            _made.Clear();
        }

        private void OnNotice(DevNotice n) => _notices.Add(n.Text);

        private void SetDirt(float dirt) => _save.Current.PlayerCleanliness = new PlayerCleanlinessDto
        {
            Dirt = dirt, SinceGameSeconds = _clock.T,
        };

        private WashStationPoint Station(string stationId, Vector2 at, string done = "done")
        {
            var go = new GameObject("station " + stationId);
            _made.Add(go);
            go.transform.position = at;
            var p = go.AddComponent<WashStationPoint>();
            p.Configure("fixture.test." + stationId, stationId, reachMeters: 1f, walkAwaySlackMeters: 0.5f,
                        "wash", "begin", done);
            return p;
        }

        // ---- the pure rules ------------------------------------------------------------------

        [Test]
        public void TheFloorIsOneLessTheCeiling()
        {
            Assert.AreEqual(0.2f, WashStationRules.FloorOf(_sink), Eps, "a basin leaves you 20 % dirty");
            Assert.AreEqual(0f, WashStationRules.FloorOf(_shower), Eps, "a shower gets you fully clean");
            Assert.AreEqual(1f, WashStationRules.FloorOf(null), Eps);
            Assert.IsTrue(WashStationRules.IsDone(0.2f, _sink));
            Assert.IsFalse(WashStationRules.IsDone(0.21f, _sink));
            Assert.IsTrue(WashStationRules.IsDone(0.5f, null), "no Def, nothing to do");
        }

        [Test]
        public void WalkingAwayIsPastReachPlusSlack()
        {
            Assert.IsFalse(WashStationRules.WalkedAway(Vector2.zero, new Vector2(1.5f, 0f), 1f, 0.5f), "on the line");
            Assert.IsTrue(WashStationRules.WalkedAway(Vector2.zero, new Vector2(1.51f, 0f), 1f, 0.5f));
            Assert.IsFalse(WashStationRules.WalkedAway(Vector2.zero, new Vector2(0.3f, 0.3f), 1f, 0f));
        }

        // ---- the placed station --------------------------------------------------------------

        [Test]
        public void ThePressBeginsAWashAtTheStationsDef()
        {
            SetDirt(0.9f);
            WashStationPoint p = Station("washstation.sink", Vector2.zero);
            Assert.IsTrue(p.IsAvailable);
            Assert.AreEqual("wash", p.VerbLabel);
            Assert.AreEqual(InteractPriority.Fixture, p.Priority);

            p.Toggle();

            Assert.AreSame(_sink, _svc.WashingAt);
            Assert.AreSame(p, WashStationPoint.Active);
            Assert.IsTrue(p.IsWashingHere);
            Assert.AreEqual(WorldStrings.WashStop, p.VerbLabel, "the popup now offers to stop");
            CollectionAssert.AreEqual(new[] { "begin" }, _notices);
        }

        [Test]
        public void ASecondPressStops_AndDirtCameOffForTheTimeStood()
        {
            SetDirt(0.9f);
            WashStationPoint p = Station("washstation.sink", Vector2.zero);
            p.Toggle();
            _clock.T += 0.25d * Hour;
            Assert.IsTrue(p.Watch(Vector2.zero), "still at the basin, still washing");

            p.Toggle();

            Assert.IsNull(_svc.WashingAt);
            Assert.IsNull(WashStationPoint.Active);
            Assert.AreEqual(0.65f, _svc.Dirt, Eps, "a quarter hour at 1.0 per hour");
            Assert.AreEqual(WorldStrings.WashStopped, _notices[_notices.Count - 1]);
        }

        [Test]
        public void WalkingOffEndsTheWash()
        {
            SetDirt(0.9f);
            WashStationPoint p = Station("washstation.sink", new Vector2(10f, 10f));
            p.Toggle();
            Assert.IsTrue(p.Watch(new Vector2(11.2f, 10f)), "a shuffle inside the slack");

            Assert.IsFalse(p.Watch(new Vector2(12f, 10f)));

            Assert.IsNull(_svc.WashingAt);
            Assert.IsNull(WashStationPoint.Active);
            Assert.AreEqual(WorldStrings.WashWalkedOff, _notices[_notices.Count - 1]);
        }

        [Test]
        public void TheStationStopsItselfAtItsOwnFloor()
        {
            SetDirt(0.9f);
            WashStationPoint p = Station("washstation.sink", Vector2.zero, done: "basin done");
            p.Toggle();
            _clock.T += 1d * Hour;   // 0.7 h of washing reaches the basin's 0.2 floor

            Assert.IsFalse(p.Watch(Vector2.zero));

            Assert.IsNull(_svc.WashingAt);
            Assert.AreEqual(0.2f, _svc.Dirt, Eps, "a basin cannot get him fully clean");
            Assert.AreEqual("basin done", _notices[_notices.Count - 1]);
        }

        [Test]
        public void AShowerRinsesHimFullyClean()
        {
            SetDirt(0.9f);
            WashStationPoint p = Station("washstation.shower", Vector2.zero);
            p.Toggle();
            _clock.T += 0.5d * Hour;

            Assert.IsFalse(p.Watch(Vector2.zero));
            Assert.AreEqual(0f, _svc.Dirt, Eps);
        }

        [Test]
        public void NothingLeftToWashOff_RefusesWithTheDoneLine()
        {
            SetDirt(0.15f);
            WashStationPoint p = Station("washstation.sink", Vector2.zero, done: "basin done");

            p.Toggle();

            Assert.IsNull(_svc.WashingAt, "already as clean as a basin gets him");
            Assert.IsNull(WashStationPoint.Active);
            CollectionAssert.AreEqual(new[] { "basin done" }, _notices);
        }

        [Test]
        public void LetsGoWhenTheServiceStoppedByAnotherRoad()
        {
            SetDirt(0.9f);
            WashStationPoint p = Station("washstation.sink", Vector2.zero);
            p.Toggle();
            _svc.EndWash();   // the sea, a clock seek, the guard window: not this station's doing

            Assert.IsFalse(p.Watch(Vector2.zero));
            Assert.IsNull(WashStationPoint.Active);
            Assert.IsFalse(p.IsWashingHere);
        }

        [Test]
        public void ASecondBasinOfTheSameKindTakesTheWashOver()
        {
            SetDirt(0.9f);
            WashStationPoint a = Station("washstation.sink", Vector2.zero);
            WashStationPoint b = Station("washstation.sink", new Vector2(30f, 0f));
            a.Toggle();
            b.Toggle();

            Assert.AreSame(b, WashStationPoint.Active, "the basin last pressed is the one being watched");
            Assert.IsFalse(a.Watch(new Vector2(30f, 0f)), "the first basin no longer watches");
            Assert.IsTrue(b.Watch(new Vector2(30f, 0f)));
            Assert.AreSame(_sink, _svc.WashingAt);
        }

        [Test]
        public void AToiletIsNotAWashStation()
        {
            WashStationPoint p = Station("washstation.toilet", Vector2.zero);
            Assert.IsFalse(p.IsAvailable);
            p.Toggle();
            Assert.IsNull(_svc.WashingAt);
        }

        [Test]
        public void AnUnknownStationIdIsUnavailable()
        {
            WashStationPoint p = Station("washstation.nonesuch", Vector2.zero);
            Assert.IsFalse(p.IsAvailable);
            Assert.IsNull(p.Def);
        }

        private sealed class Clock : IGameClock
        {
            public double T;
            public double TotalSeconds => T;
            public GameTime Now => new GameTime(T);
            public Season Season => Season.HighSummer;
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
    }
}
