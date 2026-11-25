using System.Reflection;
using HiddenHarbours.Core;
using HiddenHarbours.Environment;
using NUnit.Framework;
using UnityEngine;

namespace HiddenHarbours.Tests.EditMode
{
    public class GameClockHoldTests
    {
        GameObject _root;
        GameConfig _config;
        GameClock _clock;
        IGameClock _previousClock;

        [SetUp]
        public void SetUp()
        {
            _config = ScriptableObject.CreateInstance<GameConfig>();
            _root = new GameObject("HeldHourClock");
            _clock = _root.AddComponent<GameClock>();
            typeof(GameClock).GetField("_config", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(_clock, _config);
            _clock.SeekTo(_clock.StartSeconds);
            _previousClock = GameServices.Clock;
            GameServices.Clock = _clock;
        }

        [TearDown]
        public void TearDown()
        {
            ShellPause.Resume();
            GameServices.Clock = _previousClock;
            Object.DestroyImmediate(_root);
            Object.DestroyImmediate(_config);
        }

        [Test]
        public void OrdinaryPlayAdvancesBothReadingsEqually()
        {
            double start = _clock.TotalSeconds;
            Assert.AreEqual(start, _clock.FlowSeconds);
            _clock.Advance(10f);
            Assert.AreEqual(start + 10d, _clock.TotalSeconds);
            Assert.AreEqual(_clock.TotalSeconds, _clock.FlowSeconds);
        }

        [Test]
        public void HoldingTheHourAdvancesOnlyFlow()
        {
            double start = _clock.TotalSeconds;
            float hour = _clock.HourOfDay;
            _clock.IsHeld = true;
            _clock.Advance(10f);
            Assert.AreEqual(start, _clock.TotalSeconds);
            Assert.AreEqual(hour, _clock.HourOfDay);
            Assert.AreEqual(start + 10d, _clock.FlowSeconds);
        }

        [Test]
        public void AHeldSpanCannotPublishADayRollover()
        {
            _clock.SeekTo(_config.SecondsPerDay - 1d);
            int days = 0;
            void Count(DayStarted _) => days++;
            EventBus.Subscribe<DayStarted>(Count);
            try
            {
                _clock.IsHeld = true;
                _clock.Advance(10f);
                Assert.AreEqual(0, days);
                Assert.AreEqual(0, _clock.DayIndex);
                _clock.IsHeld = false;
                _clock.Advance(2f);
                Assert.AreEqual(1, days);
            }
            finally { EventBus.Unsubscribe<DayStarted>(Count); }
        }

        [Test]
        public void PauseStopsBothReadingsInsideAHold()
        {
            _clock.IsHeld = true;
            double start = _clock.TotalSeconds;
            _clock.IsPaused = true;
            _clock.Advance(10f);
            Assert.AreEqual(start, _clock.TotalSeconds);
            Assert.AreEqual(start, _clock.FlowSeconds);
        }

        [Test]
        public void ReleaseRunsFromTheHeldHourWithContinuousFlow()
        {
            double start = _clock.TotalSeconds;
            _clock.IsHeld = true;
            _clock.Advance(10f);
            _clock.IsHeld = false;
            _clock.Advance(3f);
            Assert.AreEqual(start + 3d, _clock.TotalSeconds);
            Assert.AreEqual(start + 13d, _clock.FlowSeconds);
            Assert.AreEqual(10d, _clock.FlowSeconds - _clock.TotalSeconds);
        }

        [TestCase(120d)]
        [TestCase(-1d)]
        public void SeekEqualisesTheTwoReadings(double target)
        {
            _clock.IsHeld = true;
            _clock.Advance(10f);
            _clock.SeekTo(target);
            Assert.AreEqual(System.Math.Max(0d, target), _clock.TotalSeconds);
            Assert.AreEqual(_clock.TotalSeconds, _clock.FlowSeconds);
        }

        [Test]
        public void RateScalesFlowDuringAHold()
        {
            double start = _clock.TotalSeconds;
            _clock.IsHeld = true;
            _clock.TimeScale = 2f;
            _clock.Advance(10f);
            Assert.AreEqual(start, _clock.TotalSeconds);
            Assert.AreEqual(start + 20d, _clock.FlowSeconds);
        }

        [Test]
        public void ThePauseMenuResumesWithTheHourStillHeld()
        {
            _clock.IsHeld = true;
            double start = _clock.TotalSeconds;
            ShellPause.Pause();
            _clock.Advance(5f);
            ShellPause.Resume();
            Assert.IsTrue(_clock.IsHeld);
            Assert.IsFalse(_clock.IsPaused);
            _clock.Advance(5f);
            Assert.AreEqual(start, _clock.TotalSeconds);
            Assert.AreEqual(start + 5d, _clock.FlowSeconds);
        }
    }
}
