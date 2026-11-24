using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HiddenHarbours.Core;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.EditMode
{
    public class DemoEndingTests
    {
        const double DaySeconds = 2400;
        GameConfig _config;
        Clock _clock;
        MemorySave _save;
        int _ended;
        bool _savedWhenEnded;
        SleepPhase _phaseWhenEnded;
        readonly List<string> _order = new List<string>();
        Action<DemoEnded> _onEnded;
        Action<GameSaved> _onSaved;

        sealed class Clock : IGameClock
        {
            public double TotalSeconds { get; private set; } = 6 * DaySeconds + 22 * DaySeconds / 24;
            public GameTime Now => new GameTime(TotalSeconds);
            public Season Season => Season.HighSummer;
            public int Year => 1;
            public int DayIndex => (int)(TotalSeconds / DaySeconds);
            public int DayOfSeason => DayIndex + 1;
            public Weekday Weekday => Weekday.Monday;
            public bool IsMarketDay => false;
            public float HourOfDay => DayFraction * 24;
            public float DayFraction => (float)(TotalSeconds % DaySeconds / DaySeconds);
            public bool IsPaused { get; set; }
            public bool IsHeld { get; set; }
            public float TimeScale { get; set; } = 1;
            public Action OnAdvance;
            public void SeekTo(double time) => TotalSeconds = time;
            public bool FastForwardTo(double time)
            {
                if (IsHeld || time <= TotalSeconds) return false;
                TotalSeconds = time;
                OnAdvance?.Invoke();
                return true;
            }
        }

        sealed class MemorySave : ISaveService
        {
            public SaveData Current { get; set; } = SaveMigration.NewGame();
            public bool LoadedExistingSave { get; set; }
            public long CompletedWrites { get; private set; }
            public bool AcceptWrites = true;
            public bool ThrowOnWrite;
            public int FlagReads;
            public int Attempts;
            public string SavedJson;
            public bool GetFlag(string key)
            {
                FlagReads++;
                return Current.OnboardingFlags.Any(f => f.Key == key && f.Value);
            }
            public void SetFlag(string key, bool value)
            {
                Current.OnboardingFlags.RemoveAll(f => f.Key == key);
                Current.OnboardingFlags.Add(new SaveFlag(key, value));
                Save();
            }
            public void Save()
            {
                Attempts++;
                if (ThrowOnWrite) throw new IOException("Fixture write failed.");
                if (!AcceptWrites || SleepFlow.InProgress) return;
                Current.GameTimeSeconds = GameServices.Clock.TotalSeconds;
                Current.DayIndex = GameServices.Clock.DayIndex;
                SavedJson = SaveSerialization.ToJson(Current);
                LoadedExistingSave = true;
                CompletedWrites++;
            }
        }

        [SetUp]
        public void SetUp()
        {
            GameServices.Reset();
            ShellFlow.Reset();
            DemoBuild.Override = true;
            _config = ScriptableObject.CreateInstance<GameConfig>();
            _config.SecondsPerDay = (float)DaySeconds;
            _config.SleepMinHours = 1;
            _config.SleepMaxHours = 8;
            _config.SleepGameHoursPerRealSecond = 1;
            _config.WakeHour = 6;
            GameServices.Config = _config;
            GameServices.Clock = _clock = new Clock();
            GameServices.Save = _save = new MemorySave();
            GameServices.CurrentRegionId = "region.st_peters";
            _ended = 0;
            _savedWhenEnded = false;
            _order.Clear();
            _onSaved = _ => _order.Add("saved");
            _onEnded = _ =>
            {
                _ended++;
                _order.Add("ended");
                _savedWhenEnded = _save.SavedJson != null &&
                    SaveSerialization.FromJson(_save.SavedJson).GameTimeSeconds == _clock.TotalSeconds;
                _phaseWhenEnded = SleepFlow.Phase;
            };
            EventBus.Subscribe(_onSaved);
            EventBus.Subscribe(_onEnded);
        }

        [TearDown]
        public void TearDown()
        {
            EventBus.Unsubscribe(_onSaved);
            EventBus.Unsubscribe(_onEnded);
            DemoBuild.Override = null;
            GameServices.Reset();
            ShellFlow.Reset();
            Object.DestroyImmediate(_config);
        }

        static RestSaveRequested Bed() => new RestSaveRequested("Fixture bed", new Vector2(2, 3), 0, "fixture.demo_bed");
        void OpenWay() => _save.SetFlag(DemoEnding.WeatherTurnedFlag, true);
        void StartSleep()
        {
            Assert.That(SleepFlow.Begin(Bed()), Is.True);
            SleepFlow.Confirm();
            Assert.That(SleepFlow.Phase, Is.EqualTo(SleepPhase.Asleep));
        }
        void ChosenSleep()
        {
            StartSleep();
            SleepFlow.Tick(8);
            Assert.That(SleepFlow.Phase, Is.EqualTo(SleepPhase.Awake));
        }

        [Test]
        public void FlagUnset_SleepAndSaveDoNotEndTheDemo()
        {
            ChosenSleep();
            Assert.That(_save.CompletedWrites, Is.EqualTo(1));
            Assert.That(_ended, Is.Zero);
            Assert.That(DemoEndingSession.Capture(_save).Ended, Is.False);
        }

        [Test]
        public void FlagSetWithoutSleep_AndAnAutosave_DoNotEndTheDemo()
        {
            OpenWay();
            _save.Save();
            EventBus.Publish(new GameSaved(true, "Fixture save"));
            Assert.That(_save.CompletedWrites, Is.EqualTo(2));
            Assert.That(_ended, Is.Zero);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void FirstSleepAfterFlag_EndsAfterTheWriteAndWake_InBothSleepPaths(bool chosen)
        {
            int version = _save.Current.SchemaVersion;
            OpenWay();
            if (chosen) ChosenSleep();
            else RestSaveResponder.Fulfil(Bed());
            Assert.That(_ended, Is.EqualTo(1));
            Assert.That(_savedWhenEnded, Is.True);
            Assert.That(_phaseWhenEnded, Is.EqualTo(SleepPhase.Awake));
            CollectionAssert.AreEqual(new[] { "saved", "ended" }, _order);
            Assert.That(DemoEndingSession.Capture(_save).Ended, Is.True);
            Assert.That(_save.Current.SchemaVersion, Is.EqualTo(version));
            Assert.That(_save.Current.OnboardingFlags.Count, Is.EqualTo(1));
            Assert.That(DemoEnding.TextKey, Is.EqualTo("demo.island_week_complete"));
        }

        [Test]
        public void SecondSleep_DoesNotRaiseTheEndingAgain()
        {
            OpenWay();
            ChosenSleep();
            ChosenSleep();
            Assert.That(_ended, Is.EqualTo(1));
            Assert.That(_save.CompletedWrites, Is.EqualTo(3));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void FullGame_StillSleepsAndSaves_WithoutReadingTheEndingFlag(bool chosen)
        {
            DemoBuild.Override = false;
            OpenWay();
            double before = _clock.TotalSeconds;
            if (chosen) ChosenSleep();
            else RestSaveResponder.Fulfil(Bed());
            Assert.That(_ended, Is.Zero);
            Assert.That(_save.FlagReads, Is.Zero);
            Assert.That(_clock.TotalSeconds, Is.EqualTo(before + 8 * DaySeconds / 24));
            Assert.That(_save.CompletedWrites, Is.EqualTo(2));
            CollectionAssert.AreEqual(new[] { "saved" }, _order);
        }

        [Test]
        public void ContinueAfterTheEnd_InTheSameSession_DoesNotReplayIt()
        {
            OpenWay();
            ChosenSleep();
            SaveRestore.ApplyToLiveServices(_save.Current, _clock, null, null);
            Assert.That(_ended, Is.EqualTo(1));
            ChosenSleep();
            Assert.That(_ended, Is.EqualTo(1));
        }

        [Test]
        public void LoadedSaveWithSessionSnapshot_DoesNotReplayTheEnd()
        {
            OpenWay();
            ChosenSleep();
            DemoEndingSnapshot snapshot = DemoEndingSession.Capture(_save);
            _save.Current = SaveSerialization.FromJson(_save.SavedJson);
            DemoEndingSession.Restore(_save, snapshot);
            SaveRestore.ApplyToLiveServices(_save.Current, _clock, null, null);
            Assert.That(_ended, Is.EqualTo(1));
            ChosenSleep();
            Assert.That(_ended, Is.EqualTo(1));
        }

        [Test]
        public void V25SaveAlone_HasNoCompletionFact_LoadIsSilent_AndTheNextSavedSleepCanEndAgain()
        {
            OpenWay();
            ChosenSleep();
            _save.Current = SaveSerialization.FromJson(_save.SavedJson);
            Assert.That(DemoEndingSession.Capture(_save).Ended, Is.False);
            SaveRestore.ApplyToLiveServices(_save.Current, _clock, null, null);
            Assert.That(_ended, Is.EqualTo(1));
            ChosenSleep();
            Assert.That(_ended, Is.EqualTo(2));
        }

        [Test]
        public void NewGameInSameProcess_GetsItsOwnEnding()
        {
            OpenWay();
            ChosenSleep();
            _save.Current = SaveMigration.NewGame();
            ChosenSleep();
            Assert.That(_ended, Is.EqualTo(1));
            OpenWay();
            ChosenSleep();
            Assert.That(_ended, Is.EqualTo(2));
        }

        [Test]
        public void ChoosingOrDecliningSleep_DoesNotEndTheDemo()
        {
            OpenWay();
            Assert.That(SleepFlow.Begin(Bed()), Is.True);
            SleepFlow.Decline();
            Assert.That(_ended, Is.Zero);
            Assert.That(_save.CompletedWrites, Is.EqualTo(1));
        }

        [Test]
        public void CancelWithoutTimePassing_DoesNotEnd_ButActualEarlySleepCan()
        {
            OpenWay();
            StartSleep();
            SleepFlow.Cancel();
            Assert.That(_ended, Is.Zero);
            StartSleep();
            SleepFlow.Tick(1);
            SleepFlow.Cancel();
            Assert.That(_ended, Is.EqualTo(1));
        }

        [Test]
        public void HeldClock_SaveOnlyRest_DoesNotEndTheDemo()
        {
            OpenWay();
            _clock.IsHeld = true;
            RestSaveResponder.Fulfil(Bed());
            Assert.That(_save.CompletedWrites, Is.EqualTo(2));
            Assert.That(_ended, Is.Zero);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void FlagBecomesSetDuringSleep_WaitsForTheFollowingSleep(bool chosen)
        {
            _clock.OnAdvance = () =>
            {
                _clock.OnAdvance = null;
                OpenWay();
            };
            if (chosen) ChosenSleep();
            else RestSaveResponder.Fulfil(Bed());
            Assert.That(_ended, Is.Zero);
            ChosenSleep();
            Assert.That(_ended, Is.EqualTo(1));
        }

        [Test]
        public void RefusedOrDeferredWrite_DoesNotUseAnEarlierSaveAsAReceipt()
        {
            OpenWay();
            Assert.That(_save.LoadedExistingSave, Is.True);
            _save.AcceptWrites = false;
            ChosenSleep();
            Assert.That(_ended, Is.Zero);
            Assert.That(_save.CompletedWrites, Is.EqualTo(1));
            _save.AcceptWrites = true;
            ChosenSleep();
            Assert.That(_ended, Is.EqualTo(1));
        }

        [Test]
        public void FailedWrite_DoesNotConsumeTheEnding()
        {
            OpenWay();
            _save.ThrowOnWrite = true;
            Assert.Throws<IOException>(() => RestSaveResponder.Fulfil(Bed()));
            Assert.That(_ended, Is.Zero);
            _save.ThrowOnWrite = false;
            ChosenSleep();
            Assert.That(_ended, Is.EqualTo(1));
        }

        [Test]
        public void ReentrantEndingListener_CannotRaiseTheEventTwice()
        {
            var ending = new DemoEnding();
            Action<DemoEnded> repeat = _ => ending.AfterSleep(true, true, true, true);
            EventBus.Subscribe(repeat);
            try
            {
                Assert.That(ending.AfterSleep(true, true, true, true), Is.True);
                Assert.That(_ended, Is.EqualTo(1));
                Assert.That(new DemoEnding(ending.Snapshot).AfterSleep(true, true, true, true), Is.False);
            }
            finally { EventBus.Unsubscribe(repeat); }
        }

        [Test]
        public void SwitchingSaveDuringSleep_CannotEndTheReplacementGame()
        {
            OpenWay();
            StartSleep();
            _save.Current = SaveMigration.NewGame();
            SleepFlow.Tick(8);
            Assert.That(_ended, Is.Zero);
            Assert.That(DemoEndingSession.Capture(_save).Ended, Is.False);
        }
    }
}
