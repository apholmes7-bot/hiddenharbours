using System;
using System.Collections.Generic;
using HiddenHarbours.Core;
using HiddenHarbours.Environment;
using NUnit.Framework;
using UnityEngine;

namespace HiddenHarbours.Tests.EditMode
{
    public sealed class TideWorkWindowTests
    {
        private sealed class Tide : IEnvironmentService
        {
            public GameConfig Config;
            public int WorldSeed { get; set; }
            public TideProfile ActiveTideProfile { get; set; } = TideProfile.CoddleCove;
            public EnvironmentSample Sample() => default;
            public float TideHeightAt(double t) => TideModel.Height(t, ActiveTideProfile, Config);
            public float TideHeightAt(double t, TideProfile place) => TideModel.Height(t, place, Config);
        }

        private sealed class ActiveOnlyTide : IEnvironmentService
        {
            public int WorldSeed => 0;
            public TideProfile ActiveTideProfile { get; set; }
            public EnvironmentSample Sample() => default;
            public float TideHeightAt(double t) => 0;
        }

        private GameConfig _config;
        private Tide _tide;
        private double Day => _config.SecondsPerDay;
        private double Step => Day / (24 * 60);

        [SetUp]
        public void SetUp()
        {
            _config = ScriptableObject.CreateInstance<GameConfig>();
            _tide = new Tide { Config = _config };
        }

        [TearDown]
        public void TearDown() => UnityEngine.Object.DestroyImmediate(_config);

        [TestCase(0, false)]
        [TestCase(1, true)]
        [TestCase(-41, false)]
        [TestCase(92381, true)]
        public void WindowsAgreeWithFineSamplesAcrossSpringAndNeap(int seed, bool above)
        {
            _tide.WorldSeed = seed;
            var windows = new List<TideWindow>();
            foreach (TideProfile place in new[] { TideProfile.CoddleCove, TideProfile.FundyRips })
            foreach (int day in new[] { 0, 1, 6, 7, 13, 27 })
            {
                const float height = 0.2f;
                CrossingMath.FindWorkWindows(_tide, place, day, height, above, _config, windows);
                double previousEnd = day * Day;
                foreach (TideWindow window in windows)
                {
                    Assert.That(window.StartSeconds, Is.GreaterThanOrEqualTo(previousEnd));
                    Assert.That(window.EndSeconds, Is.GreaterThan(window.StartSeconds));
                    Assert.That(window.EndSeconds, Is.LessThanOrEqualTo((day + 1) * Day));
                    previousEnd = window.EndSeconds;
                }
                for (int sample = 0; sample < 14400; sample++)
                {
                    double t = (day + (sample + 0.5) / 14400.0) * Day;
                    bool listed = false;
                    foreach (TideWindow window in windows)
                        listed |= t > window.StartSeconds && t < window.EndSeconds;
                    float water = TideModel.Height(t, place, _config);
                    Assert.That(listed, Is.EqualTo(above ? water > height : water < height),
                                $"day {day}, sample {sample}, seed {seed}, above {above}");
                }
            }
        }

        [Test]
        public void MidnightSplitsOneContinuousWindowWithoutLosingTime()
        {
            var place = new TideProfile { Amplitude = 1.6f, PhaseHours = 9f };
            var whole = new List<TideWindow>();
            var firstDay = new List<TideWindow>();
            var secondDay = new List<TideWindow>();
            CrossingMath.FindWorkWindows(_tide, place, Day - Day / 8, Day + Day / 8,
                                         0f, false, Step, whole);
            Assert.That(whole.Count, Is.EqualTo(1));
            Assert.That(whole[0].StartSeconds, Is.LessThan(Day));
            Assert.That(whole[0].EndSeconds, Is.GreaterThan(Day));
            CrossingMath.FindWorkWindows(_tide, place, 0, 0, false, _config, firstDay);
            CrossingMath.FindWorkWindows(_tide, place, 1, 0, false, _config, secondDay);
            Assert.That(firstDay[firstDay.Count - 1].EndSeconds, Is.EqualTo(Day));
            Assert.That(secondDay[0].StartSeconds, Is.EqualTo(Day));
            Assert.That(CrossingMath.TryWorkSecondsLeft(_tide, place, Day - Step, 2 * Day,
                        0, false, Step, out double left), Is.True);
            Assert.That(left, Is.EqualTo(secondDay[0].EndSeconds - (Day - Step)).Within(1e-5));
        }

        [Test]
        public void CalendarCanContainOneLowWaterOrTwo()
        {
            _config.NeapAmplitudeFraction = 1f;
            var windows = new List<TideWindow>();
            var counts = new HashSet<int>();
            for (int day = 0; day < 28; day++)
            {
                CrossingMath.FindWorkWindows(_tide, TideProfile.CoddleCove, day,
                                             -1.59f, false, _config, windows);
                counts.Add(windows.Count);
            }
            CollectionAssert.AreEquivalent(new[] { 1, 2 }, counts);
        }

        [Test]
        public void NextOpeningAndTimeLeftMatchTheSameWindow()
        {
            TideProfile place = TideProfile.CoddleCove;
            var windows = new List<TideWindow>();
            CrossingMath.FindWorkWindows(_tide, place, 0, 0, false, _config, windows);
            Assert.That(windows.Count, Is.EqualTo(2));
            Assert.That(CrossingMath.TryNextWorkOpening(_tide, place, 0, Day, 0, false,
                        Step, out double opening), Is.True);
            Assert.That(opening, Is.EqualTo(windows[0].StartSeconds).Within(1e-5));
            double now = opening + Step;
            Assert.That(CrossingMath.TryNextWorkOpening(_tide, place, now, Day, 0, false,
                        Step, out double alreadyOpen), Is.True);
            Assert.That(alreadyOpen, Is.EqualTo(now));
            Assert.That(CrossingMath.TryWorkSecondsLeft(_tide, place, now, Day, 0, false,
                        Step, out double left), Is.True);
            Assert.That(left, Is.EqualTo(windows[0].EndSeconds - now).Within(1e-5));
        }

        [Test]
        public void FlatAndUnreachableTidesHaveHonestBoundedAnswers()
        {
            var flat = new TideProfile { MeanLevel = 2f };
            var windows = new List<TideWindow>();
            CrossingMath.FindWorkWindows(_tide, flat, 0, 3, false, _config, windows);
            Assert.That(windows.Count, Is.EqualTo(1));
            Assert.That(windows[0].DurationSeconds, Is.EqualTo(Day));
            Assert.That(CrossingMath.TryWorkSecondsLeft(_tide, flat, 0, Day, 3, false,
                        Step, out double unknown), Is.False);
            Assert.That(double.IsNaN(unknown), Is.True);
            Assert.That(CrossingMath.TryNextWorkOpening(_tide, flat, 0, Day, 1, false,
                        Step, out double absent), Is.False);
            Assert.That(double.IsNaN(absent), Is.True);
            Assert.That(CrossingMath.TryWorkSecondsLeft(_tide, flat, 0, Day, 1, false,
                        Step, out double closed), Is.True);
            Assert.That(closed, Is.Zero);
            CrossingMath.FindWorkWindows(_tide, flat, 0, 2, true, _config, windows);
            Assert.That(windows.Count, Is.Zero, "equality is closed in both directions");
            Assert.That(CrossingMath.IsWorkableNow(_tide, flat, 0, 2), Is.False);
        }

        [Test]
        public void QueryHasNoStaleCacheAndDoesNotChangeActivePlace()
        {
            var place = TideProfile.FundyRips;
            TideProfile active = _tide.ActiveTideProfile;
            var before = new List<TideWindow>();
            var after = new List<TideWindow>();
            CrossingMath.FindWorkWindows(_tide, place, 4, 1, false, _config, before);
            CrossingMath.FindWorkWindows(_tide, active, 7, 1, false, _config, after);
            CrossingMath.FindWorkWindows(_tide, place, 4, 1, false, _config, after);
            CollectionAssert.AreEqual(before, after);
            Assert.That(_tide.ActiveTideProfile, Is.EqualTo(active));
            Assert.That(CrossingMath.IsWorkableNow(_tide, new TideProfile(), 0, 1), Is.True);
            Assert.That(CrossingMath.IsWorkableNow(_tide, new TideProfile { MeanLevel = 2 }, 0, 1), Is.False);
        }

        [Test]
        public void NowQueryAllocatesNoManagedMemoryAfterWarmup()
        {
            TideProfile place = TideProfile.CoddleCove;
            for (int i = 0; i < 100; i++) CrossingMath.IsWorkableNow(_tide, place, i, 0);
            long before = GC.GetAllocatedBytesForCurrentThread();
            bool answer = false;
            for (int i = 0; i < 1000; i++) answer ^= CrossingMath.IsWorkableNow(_tide, place, i, 0);
            long bytes = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.That(bytes, Is.Zero);
            GC.KeepAlive(answer);
        }

        [Test]
        public void ActiveOnlyServicesCannotSilentlyForecastAnotherPlace()
        {
            IEnvironmentService tide = new ActiveOnlyTide();
            Assert.That(tide.TideHeightAt(0, default), Is.Zero);
            Assert.Throws<NotSupportedException>(() => tide.TideHeightAt(0, TideProfile.FundyRips));
        }

        [Test]
        public void RealServicePredictsRequestedProfileWithoutChangingActiveProfile()
        {
            var go = new GameObject("Tide query test");
            go.SetActive(false);
            try
            {
                var service = go.AddComponent<EnvironmentService>();
                var serialized = new UnityEditor.SerializedObject(service);
                serialized.FindProperty("_config").objectReferenceValue = _config;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                IEnvironmentService tide = service;
                TideProfile active = tide.ActiveTideProfile;
                Assert.That(tide.TideHeightAt(35, TideProfile.FundyRips),
                            Is.EqualTo(TideModel.Height(35, TideProfile.FundyRips, _config)));
                Assert.That(tide.ActiveTideProfile, Is.EqualTo(active));
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        [TestCase(double.NaN, 100, 1)]
        [TestCase(0, double.PositiveInfinity, 1)]
        [TestCase(0, 100, 0)]
        [TestCase(0, 100, -1)]
        [TestCase(100, 100, 1)]
        public void InvalidSearchCannotLoopForever(double from, double to, double step)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => CrossingMath.FindWorkWindows(
                _tide, default, from, to, 0, false, step, new List<TideWindow>()));
        }
    }
}
