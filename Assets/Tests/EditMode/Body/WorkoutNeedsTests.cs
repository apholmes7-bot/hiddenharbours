using System;
using HiddenHarbours.Core;
using NUnit.Framework;

namespace HiddenHarbours.Tests.EditMode
{
    public class WorkoutNeedsTests
    {
        private MemorySave _save;
        private Clock _clock;
        private NeedsService _needs;
        private CleanlinessService _clean;
        private NeedsSettings _settings;
        private CleanlinessSettings _dirt;
        private const double Hour = 600d;

        [SetUp]
        public void SetUp()
        {
            _save = new MemorySave(); _clock = new Clock();
            _settings = NeedsSettings.Default;
            _settings.Hunger.RisePerGameHour = 0.1f;
            _settings.Tiredness.RisePerGameHour = 0.05f;
            _settings.Tiredness.MildAt = 0.4f;
            _settings.Tiredness.StrongAt = 0.8f;
            _dirt = CleanlinessSettings.Default;
            _dirt.Curve.RisePerGameHour = 0.02f;
            _dirt.Sources = new[] { new DirtSourceRate(DirtActivity.Gym, 0.15f, 0.9f) };
            _save.Current.PlayerNeeds = PlayerNeedsDto.FedAndRestedAt(0d);
            _save.Current.PlayerCleanliness = PlayerCleanlinessDto.CleanAt(0d);
            _clean = new CleanlinessService(() => _save, () => _clock, () => _dirt,
                                            () => Array.Empty<WashStationDef>(), () => Hour);
            _needs = new NeedsService(() => _save, () => _clock, () => _settings, () => null, () => Hour,
                                      () => null, () => _clean, () => _dirt);
        }

        [Test]
        public void Exert_SettlesPassiveRise_ClampsAndAnnouncesStage()
        {
            int changed = 0;
            Action<NeedStageChanged> listener = e => { if (e.Need == NeedKind.Tiredness) changed++; };
            EventBus.Subscribe(listener);
            try
            {
                _clock.T = Hour * 2d;
                _needs.Exert(0.35f);
                Assert.AreEqual(0.45f, _needs.Level(NeedKind.Tiredness), 1e-6f);
                Assert.AreEqual(1, changed);
                _needs.Exert(1f);
                Assert.AreEqual(1f, _needs.Level(NeedKind.Tiredness));
                Assert.AreEqual(2, changed);
                Assert.AreEqual(0.2f, _needs.Level(NeedKind.Hunger), 1e-6f);
                Assert.AreEqual(0, _save.Writes);
            }
            finally { EventBus.Unsubscribe(listener); }
        }

        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(-1f)]
        [TestCase(0f)]
        public void InvalidExertion_DoesNothing(float amount)
        {
            _needs.Exert(amount);
            Assert.AreEqual(0f, _needs.Level(NeedKind.Tiredness));
            Assert.AreEqual(0d, _save.Current.PlayerNeeds.Tiredness.SinceGameSeconds);
        }

        [Test]
        public void BoundedSweat_UsesHoursNotActions_AndLeavesNoLiveStretch()
        {
            _clock.T = Hour;
            _clean.ReportActivity(DirtActivity.Gym, 0.5d);
            Assert.AreEqual(0.095f, _clean.Dirt, 1e-6f);
            Assert.AreEqual(DirtActivity.None, _clean.Activity);
            _clock.T += Hour;
            Assert.AreEqual(0.115f, _clean.Dirt, 1e-6f);
            _clean.ReportActivity(DirtActivity.Gym, 100d);
            Assert.AreEqual(1f, _clean.Dirt);
            Assert.AreEqual(0, _save.Writes);
        }

        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        [TestCase(-1d)]
        [TestCase(0d)]
        public void InvalidSweatDuration_DoesNothing(double hours)
        {
            _clean.ReportActivity(DirtActivity.Gym, hours);
            Assert.AreEqual(0f, _clean.Dirt);
            Assert.AreEqual(0d, _save.Current.PlayerCleanliness.SinceGameSeconds);
        }

        [Test]
        public void MissingSave_IsHarmless()
        {
            _save.Data = null;
            Assert.DoesNotThrow(() => _needs.Exert(0.2f));
            Assert.DoesNotThrow(() => _clean.ReportActivity(DirtActivity.Gym, 1d));
            Assert.AreEqual(0, _save.Writes);
        }

        private sealed class MemorySave : ISaveService
        {
            public SaveData Data = new SaveData();
            public SaveData Current => Data;
            public int Writes;
            public bool GetFlag(string key) => false;
            public void SetFlag(string key, bool value) { }
            public void Save() => Writes++;
        }

        private sealed class Clock : IGameClock
        {
            public double T;
            public double TotalSeconds => T;
            public GameTime Now => new GameTime(T);
            public Season Season => Season.HighSummer;
            public int Year => 1;
            public int DayIndex => (int)(T / (Hour * 24d));
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
