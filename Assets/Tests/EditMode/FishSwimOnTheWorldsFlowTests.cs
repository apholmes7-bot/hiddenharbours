using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Fishing;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// THE FISH SWIM WHILE THE HOUR IS HELD. A held hour (<see cref="IGameClock.IsHeld"/>: the opening at
    /// sea, a held-light shot) stops <see cref="IGameClock.TotalSeconds"/> but the world keeps flowing on
    /// <see cref="IGameClock.FlowSeconds"/>. The swimmers used to be posed on the held hour, so every fish
    /// in view stood frozen mid-stroke for as long as the hour was held. They are now posed on the flow,
    /// which equals the hour at every other moment, so nothing changes in ordinary play.
    /// </summary>
    public sealed class FishSwimOnTheWorldsFlowTests
    {
        /// <summary>A clock whose hour and flow are set by hand.</summary>
        private sealed class HeldClock : IGameClock
        {
            public double Total;
            public double Flow;
            public bool Held;
            public double TotalSeconds => Total;
            public double FlowSeconds => Flow;
            public bool IsHeld { get => Held; set => Held = value; }
            public GameTime Now => new GameTime(Total);
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

        /// <summary>A clock that does not override FlowSeconds: the interface's default, the hour.</summary>
        private sealed class PlainClock : IGameClock
        {
            public double Total;
            public double TotalSeconds => Total;
            public GameTime Now => new GameTime(Total);
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

        private IGameClock _savedClock;

        [SetUp]
        public void SaveClock()
        {
            _savedClock = GameServices.Clock;
            EventBus.Clear<GullSplashed>();
        }

        [TearDown]
        public void RestoreClock()
        {
            GameServices.Clock = _savedClock;
            EventBus.Clear<GullSplashed>();
        }

        [Test]
        public void TheSwimmersArePosedOnTheFlowNotTheHeldHour()
        {
            var clock = new HeldClock { Total = 73800.0, Flow = 73812.5, Held = true };
            Assert.AreEqual(73812.5, FishSchoolPresenter.PoseSeconds(clock), 1e-9);
        }

        [Test]
        public void WithNoHeldSpanThePoseIsTheHourItAlwaysWas()
        {
            var plain = new PlainClock { Total = 5000.25 };
            Assert.AreEqual(5000.25, FishSchoolPresenter.PoseSeconds(plain), 1e-9,
                            "a clock with no held span must pose the fish exactly as before");
            Assert.AreEqual(0.0, FishSchoolPresenter.PoseSeconds(null), "no clock reads zero, as before");
        }

        [Test]
        public void AHeldHourStillMovesTheShoal()
        {
            var clock = new HeldClock { Total = 73800.0, Flow = 73800.0, Held = true };
            var a = new ShoalMath.Swimmer[12];
            var b = new ShoalMath.Swimmer[12];
            int seed = ShoalMath.SeedFor(0xC0FFEEu);

            int n = ShoalMath.Fill(0.3f, 12, FishSchoolPresenter.PoseSeconds(clock), seed, 1.2f,
                                   ShoalMath.DefaultSpeedMetresPerSecond, 1f, ShoalMath.DefaultZMetres, a);
            clock.Flow += 2.0;    // two seconds of world flow; the hour does not move
            ShoalMath.Fill(0.3f, 12, FishSchoolPresenter.PoseSeconds(clock), seed, 1.2f,
                           ShoalMath.DefaultSpeedMetresPerSecond, 1f, ShoalMath.DefaultZMetres, b);

            float moved = 0f;
            for (int i = 0; i < n; i++) moved = Mathf.Max(moved, Mathf.Sqrt(
                (a[i].X - b[i].X) * (a[i].X - b[i].X) + (a[i].Y - b[i].Y) * (a[i].Y - b[i].Y)));
            Assert.AreEqual(73800.0, clock.TotalSeconds, "the hour stayed held");
            Assert.Greater(moved, 0.1f, "two seconds of flow left every fish where it was");
        }

        [Test]
        public void ThePoseIsDeterministicInTheFlow()
        {
            var a = new ShoalMath.Swimmer[12];
            var b = new ShoalMath.Swimmer[12];
            int seed = ShoalMath.SeedFor(42u);
            var one = new HeldClock { Total = 100.0, Flow = 160.0, Held = true };
            var two = new HeldClock { Total = 160.0, Flow = 160.0 };
            int n = ShoalMath.Fill(0.3f, 12, FishSchoolPresenter.PoseSeconds(one), seed, 1.2f,
                                   ShoalMath.DefaultSpeedMetresPerSecond, 1f, ShoalMath.DefaultZMetres, a);
            ShoalMath.Fill(0.3f, 12, FishSchoolPresenter.PoseSeconds(two), seed, 1.2f,
                           ShoalMath.DefaultSpeedMetresPerSecond, 1f, ShoalMath.DefaultZMetres, b);
            for (int i = 0; i < n; i++)
            {
                Assert.AreEqual(a[i].X, b[i].X, 0f);
                Assert.AreEqual(a[i].Y, b[i].Y, 0f);
                Assert.AreEqual(a[i].Frame, b[i].Frame);
            }
        }

        [Test]
        public void ASplashIsStampedOnTheSameFlowTheFishSwimOn()
        {
            var clock = new HeldClock { Total = 1000.0, Flow = 1300.0, Held = true };
            GameServices.Clock = clock;
            var log = new GullSplashLog();
            log.Subscribe();
            try
            {
                EventBus.Publish(new GullSplashed(new Vector2(5f, 5f), GullSplashKind.Alight));
                var school = new FishSchool(new Vector2(5f, 5f), 4f, 2f, 6,
                                            new[] { "fish.atlantic_herring" }, 0.0, 1e9);
                double now = FishSchoolPresenter.PoseSeconds(clock)
                             + ShoalEventMath.DurationSeconds(ShoalEventKind.Dart) * 0.5;
                Assert.IsTrue(log.TryScatter(school, now, 2.5f, 0.5f, out _, out double start, out _),
                              "a splash during a held hour did not scatter the fish drawn on the flow");
                Assert.AreEqual(1300.0, start, 1e-9, "the splash was stamped on the held hour, not the flow");
            }
            finally { log.Unsubscribe(); }
        }
    }
}
