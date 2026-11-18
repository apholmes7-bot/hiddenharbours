using System.Collections.Generic;
using HiddenHarbours.Core;
using HiddenHarbours.World;
using NUnit.Framework;
using UnityEngine;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// The seat that only seats (kitchens and bathrooms plan, R1): the press sits the player on a SitOnly
    /// station through a Core <see cref="SeatSession"/>, a second press or walking off gets him up, it washes
    /// nothing, it refuses a station that washes, and a second seat takes over from the first. Real
    /// <see cref="CleanlinessService"/> (for the Def lookup) over an in-memory save and a hand-wound clock;
    /// every number is the test's own.
    /// </summary>
    public class SitPointTests
    {
        private const double Hour = 100d;

        private readonly List<Object> _made = new List<Object>();
        private readonly List<string> _notices = new List<string>();
        private readonly List<PlayerSat> _sat = new List<PlayerSat>();
        private readonly List<PlayerStood> _stood = new List<PlayerStood>();
        private TraitFixtures.MemorySave _save;
        private Clock _clock;
        private CleanlinessService _svc;
        private SeatSession _session;

        [SetUp]
        public void SetUp()
        {
            GameServices.Reset();
            EventBus.Clear<DevNotice>();
            EventBus.Clear<PlayerSat>();
            EventBus.Clear<PlayerStood>();
            EventBus.Subscribe<DevNotice>(OnNotice);
            EventBus.Subscribe<PlayerSat>(OnSat);
            EventBus.Subscribe<PlayerStood>(OnStood);
            _save = new TraitFixtures.MemorySave();
            _clock = new Clock { T = 500d };
            WashStationDef sink = CleanlinessMathTests.Station(_made, "washstation.sink", WashStationKind.Sink, 1f, 0.8f);
            WashStationDef toilet = CleanlinessMathTests.Station(_made, "washstation.toilet", WashStationKind.Toilet, 0f, 0f, sitOnly: true);
            CleanlinessSettings settings = CleanlinessMathTests.Settings();
            settings.Curve.RisePerGameHour = 0f;
            _svc = new CleanlinessService(() => _save, () => _clock, () => settings, () => new[] { sink, toilet }, () => Hour);
            GameServices.Cleanliness = _svc;
            _session = new SeatSession(null, () => _clock);
            SitPoint.UseSession(_session);
            _notices.Clear(); _sat.Clear(); _stood.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            SitPoint.UseSession(null);
            EventBus.Unsubscribe<DevNotice>(OnNotice);
            EventBus.Unsubscribe<PlayerSat>(OnSat);
            EventBus.Unsubscribe<PlayerStood>(OnStood);
            EventBus.Clear<DevNotice>();
            EventBus.Clear<PlayerSat>();
            EventBus.Clear<PlayerStood>();
            GameServices.Reset();
            foreach (Object o in _made) if (o != null) Object.DestroyImmediate(o);
            _made.Clear();
        }

        private void OnNotice(DevNotice n) => _notices.Add(n.Text);
        private void OnSat(PlayerSat s) => _sat.Add(s);
        private void OnStood(PlayerStood s) => _stood.Add(s);

        private SitPoint Seat(string id, string stationId, Vector2 at)
        {
            var go = new GameObject("seat " + id);
            _made.Add(go);
            go.transform.position = at;
            var p = go.AddComponent<SitPoint>();
            p.Configure(id, stationId, reachMeters: 1f, walkAwaySlackMeters: 0.5f, "sit", "sat");
            return p;
        }

        [Test]
        public void ThePressSitsHimDown_ASecondPressGetsHimUp()
        {
            SitPoint p = Seat("fixture.test.toilet", "washstation.toilet", Vector2.zero);
            Assert.IsTrue(p.IsAvailable);
            Assert.AreEqual("sit", p.VerbLabel);
            Assert.AreEqual(InteractPriority.Fixture, p.Priority);

            p.Toggle();
            Assert.IsTrue(p.IsSeatedHere);
            Assert.AreSame(p, SitPoint.Active);
            Assert.AreEqual("fixture.test.toilet", _session.SeatId);
            Assert.AreEqual(1, _sat.Count);
            Assert.IsFalse(_sat[0].AtDiningSeat, "a toilet is no dining seat");
            Assert.AreEqual(WorldStrings.SitGetUp, p.VerbLabel);
            Assert.AreEqual("sat", _notices[_notices.Count - 1]);

            _clock.T += 0.25d * Hour;
            p.Toggle();
            Assert.IsFalse(p.IsSeatedHere);
            Assert.IsNull(SitPoint.Active);
            Assert.IsFalse(_session.IsSeated);
            Assert.AreEqual(1, _stood.Count);
            Assert.AreEqual(0.25d * Hour, _stood[0].SeatedGameSeconds, 1e-9);
            Assert.AreEqual(WorldStrings.SitStood, _notices[_notices.Count - 1]);
        }

        [Test]
        public void SittingWashesNothing()
        {
            _save.Current.PlayerCleanliness = new PlayerCleanlinessDto { Dirt = 0.7f, SinceGameSeconds = _clock.T };
            SitPoint p = Seat("fixture.test.toilet", "washstation.toilet", Vector2.zero);
            p.Toggle();
            _clock.T += Hour;
            Assert.IsNull(_svc.WashingAt, "no wash began");
            Assert.AreEqual(0.7f, _svc.Dirt, 1e-4f);
        }

        [Test]
        public void WalkingOffGetsHimUp()
        {
            SitPoint p = Seat("fixture.test.toilet", "washstation.toilet", new Vector2(10f, 10f));
            p.Toggle();
            Assert.IsTrue(p.Watch(new Vector2(11.2f, 10f)), "a shuffle inside the slack");
            Assert.IsFalse(p.Watch(new Vector2(12f, 10f)));
            Assert.IsFalse(_session.IsSeated);
            Assert.IsNull(SitPoint.Active);
            Assert.AreEqual(1, _stood.Count);
        }

        [Test]
        public void AStationThatWashesIsNoSeat()
        {
            SitPoint p = Seat("fixture.test.sink", "washstation.sink", Vector2.zero);
            Assert.IsFalse(p.IsAvailable);
            p.Toggle();
            Assert.IsFalse(_session.IsSeated);
            Assert.IsEmpty(_sat);
        }

        [Test]
        public void AnUnshippedStationIsNoSeat()
        {
            SitPoint p = Seat("fixture.test.none", "washstation.none", Vector2.zero);
            Assert.IsFalse(p.IsAvailable);
            Assert.IsNull(p.Def);
        }

        [Test]
        public void ASecondSeatTakesOver()
        {
            SitPoint a = Seat("fixture.test.a", "washstation.toilet", Vector2.zero);
            SitPoint b = Seat("fixture.test.b", "washstation.toilet", new Vector2(30f, 0f));
            a.Toggle();
            b.Toggle();
            Assert.AreSame(b, SitPoint.Active);
            Assert.AreEqual("fixture.test.b", _session.SeatId);
            Assert.IsFalse(a.IsSeatedHere);
            Assert.IsFalse(a.Watch(new Vector2(30f, 0f)), "the first seat no longer watches");
            Assert.IsTrue(b.Watch(new Vector2(30f, 0f)));
            Assert.AreEqual(1, _stood.Count, "he got up from the first seat once");
        }

        [Test]
        public void AnotherRoadToStandingLetsGo()
        {
            SitPoint p = Seat("fixture.test.toilet", "washstation.toilet", Vector2.zero);
            p.Toggle();
            _session.Stand();
            Assert.IsFalse(p.Watch(Vector2.zero));
            Assert.IsNull(SitPoint.Active);
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
