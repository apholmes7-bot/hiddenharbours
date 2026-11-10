using System;
using System.Collections.Generic;
using System.Reflection;
using HiddenHarbours.Core;
using HiddenHarbours.Environment;
using HiddenHarbours.UI;
using NUnit.Framework;
using UnityEngine;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>The night passes under a dark screen.</b> A rest moves hours of light in one call, and seen
    /// bare that reads as a fault. These pin the order the rest now runs in: the screen cover is raised
    /// and held, THEN the clock moves, THEN the save is written, and only then is the cover let go, which
    /// is what starts its fade back to clear.
    ///
    /// <para>The order is read off one shared log that the cover's listener, the clock, the save and
    /// the outcome's listener all write to, so "before" and "after" are positions in a list and not
    /// timings. Nothing here waits on a frame: the whole rest is one synchronous call, which is itself
    /// the property under test (there is no wait that could fail to end).</para>
    /// </summary>
    public class RestUnderTheCoverTests
    {
        const string Dark = "dark";
        const string Light = "light";
        const string Moved = "clock";
        const string Day = "day";
        const string Written = "save";
        const string Told = "told";
        const double Exact = 1e-9;

        /// <summary>A clock that moves when fast-forwarded and says so in the shared log.</summary>
        sealed class RecordingClock : IGameClock
        {
            readonly List<string> _log;
            readonly double _dayLength;
            public bool Refuses;
            public RecordingClock(List<string> log, double dayLength, double start)
            {
                _log = log;
                _dayLength = dayLength;
                TotalSeconds = start;
            }

            public double TotalSeconds { get; private set; }
            public GameTime Now => new GameTime(TotalSeconds);
            public Season Season => Season.HighSummer;
            public int Year => 1;
            public int DayIndex => (int)(TotalSeconds / _dayLength);
            public int DayOfSeason => DayIndex + 1;
            public Weekday Weekday => Weekday.Monday;
            public bool IsMarketDay => false;
            public float DayFraction => (float)(TotalSeconds % _dayLength / _dayLength);
            public float HourOfDay => DayFraction * 24f;
            public bool IsPaused { get; set; }
            public float TimeScale { get; set; } = 1f;
            public bool IsHeld { get; set; }

            public bool FastForwardTo(double totalSeconds)
            {
                if (Refuses || IsHeld || !(totalSeconds > TotalSeconds)) return false;
                TotalSeconds = totalSeconds;
                _log.Add(Moved);
                return true;
            }
        }

        /// <summary>A save that says when it was asked to write, counts the asks, and can be told to
        /// fail the way a full disk does.</summary>
        sealed class RecordingSave : ISaveService
        {
            readonly List<string> _log;
            public int Saves;
            public bool Throws;
            public double ClockAtWrite = double.NaN;
            public RecordingSave(List<string> log) => _log = log;

            public SaveData Current { get; } = SaveMigration.NewGame();
            public bool LoadedExistingSave { get; private set; }
            public bool GetFlag(string key) => false;
            public void SetFlag(string key, bool value) { }

            public void Save()
            {
                Saves++;
                _log.Add(Written);
                IGameClock clock = GameServices.Clock;
                if (clock != null) ClockAtWrite = clock.TotalSeconds;
                if (Throws) throw new InvalidOperationException("the disk is full");
                LoadedExistingSave = true;
            }
        }

        readonly List<string> _log = new List<string>();
        readonly List<UnityEngine.Object> _spawned = new List<UnityEngine.Object>();
        Action<RegionTravelCover> _onCover;
        Action<GameSaved> _onSaved;
        Action<DayStarted> _onDay;
        bool _coverHeard;

        GameConfig _config;
        RecordingSave _save;

        IGameClock _previousClock;
        GameConfig _previousConfig;
        ISaveService _previousSave;
        string _previousRegion;
        bool _responderWasInstalled;

        [SetUp]
        public void SetUp()
        {
            _previousClock = GameServices.Clock;
            _previousConfig = GameServices.Config;
            _previousSave = GameServices.Save;
            _previousRegion = GameServices.CurrentRegionId;
            _responderWasInstalled = RestSaveResponder.Installed;

            _config = ScriptableObject.CreateInstance<GameConfig>();
            _spawned.Add(_config);
            _log.Clear();
            _save = new RecordingSave(_log);

            GameServices.Config = _config;
            GameServices.Save = _save;
            GameServices.CurrentRegionId = null;

            _onCover = cover => _log.Add(cover.IsLoading ? Dark : Light);
            _onSaved = _ => _log.Add(Told);
            _onDay = _ => _log.Add(Day);
            EventBus.Subscribe(_onSaved);
            EventBus.Subscribe(_onDay);
            ListenForTheCover();
        }

        [TearDown]
        public void TearDown()
        {
            StopListeningForTheCover();
            EventBus.Unsubscribe(_onSaved);
            EventBus.Unsubscribe(_onDay);
            if (!_responderWasInstalled) RestSaveResponder.Uninstall();

            GameServices.Clock = _previousClock;
            GameServices.Config = _previousConfig;
            GameServices.Save = _previousSave;
            GameServices.CurrentRegionId = _previousRegion;

            for (int i = 0; i < _spawned.Count; i++)
                if (_spawned[i] != null) UnityEngine.Object.DestroyImmediate(_spawned[i]);
            _spawned.Clear();
        }

        // ---- rig ------------------------------------------------------------------------------

        void ListenForTheCover()
        {
            if (_coverHeard) return;
            EventBus.Subscribe(_onCover);
            _coverHeard = true;
        }

        void StopListeningForTheCover()
        {
            if (!_coverHeard) return;
            EventBus.Unsubscribe(_onCover);
            _coverHeard = false;
        }

        double At(int day, double hour) =>
            day * (double)_config.SecondsPerDay + _config.SecondsPerDay * (hour / 24d);

        RecordingClock FakeClockAt(double hour)
        {
            var clock = new RecordingClock(_log, _config.SecondsPerDay, At(0, hour));
            GameServices.Clock = clock;
            return clock;
        }

        GameClock RealClockAt(double hour)
        {
            var go = new GameObject("RestCoverClock");
            _spawned.Add(go);
            var clock = go.AddComponent<GameClock>();
            FieldInfo field = typeof(GameClock).GetField("_config", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, "GameClock has no field _config");
            field.SetValue(clock, _config);
            clock.SeekTo(At(0, hour));
            GameServices.Clock = clock;
            return clock;
        }

        static GameSaved Rest() => RestSaveResponder.Fulfil(new RestSaveRequested("your bed at Ginny's"));

        // ---- the order --------------------------------------------------------------------------

        [Test]
        public void The_screen_is_dark_before_the_clock_moves_and_the_light_returns_after_the_save()
        {
            FakeClockAt(22);

            GameSaved outcome = Rest();

            CollectionAssert.AreEqual(new[] { Dark, Moved, Written, Light, Told }, _log);
            Assert.IsTrue(outcome.Ok);
            Assert.AreEqual(RestSaveResponder.WokeText, outcome.Reason);
        }

        [Test]
        public void The_bed_signal_through_the_bus_runs_the_same_order()
        {
            FakeClockAt(22);
            RestSaveResponder.Install();

            EventBus.Publish(new RestSaveRequested("your bed at Ginny's"));

            CollectionAssert.AreEqual(new[] { Dark, Moved, Written, Light, Told }, _log);
        }

        [Test]
        public void On_the_real_clock_the_day_starts_and_the_save_is_written_inside_the_dark()
        {
            GameClock clock = RealClockAt(22);
            double clockWhenDark = double.NaN;
            double clockWhenLight = double.NaN;
            Action<RegionTravelCover> read = cover =>
            {
                if (cover.IsLoading) clockWhenDark = clock.TotalSeconds;
                else clockWhenLight = clock.TotalSeconds;
            };
            EventBus.Subscribe(read);

            try { Rest(); }
            finally { EventBus.Unsubscribe(read); }

            CollectionAssert.AreEqual(new[] { Dark, Day, Written, Light, Told }, _log);
            Assert.AreEqual(At(0, 22), clockWhenDark, Exact, "the screen went dark on last night's clock");
            Assert.AreEqual(At(1, _config.WakeHour), clockWhenLight, Exact, "and the light came back on the morning's");
            Assert.AreEqual(At(1, _config.WakeHour), _save.ClockAtWrite, Exact, "the save was written on the morning");
        }

        [Test]
        public void The_save_is_written_exactly_once_and_the_cover_raised_and_let_go_exactly_once()
        {
            FakeClockAt(22);

            Rest();

            Assert.AreEqual(1, _save.Saves);
            Assert.AreEqual(1, _log.FindAll(entry => entry == Dark).Count);
            Assert.AreEqual(1, _log.FindAll(entry => entry == Light).Count);
        }

        [Test]
        public void Two_rests_raise_two_covers_and_each_is_let_go_before_the_next()
        {
            GameClock clock = RealClockAt(22);

            Rest();
            Rest();

            CollectionAssert.AreEqual(
                new[] { Dark, Day, Written, Light, Told, Dark, Day, Written, Light, Told }, _log);
            Assert.AreEqual(At(2, _config.WakeHour), clock.TotalSeconds, Exact);
        }

        // ---- nobody listening: the rest is the rest it was ----------------------------------------

        [Test]
        public void With_nobody_listening_for_the_cover_the_rest_is_the_rest_it_was()
        {
            GameClock heard = RealClockAt(22);
            GameSaved withCover = Rest();
            double clockWithCover = heard.TotalSeconds;
            double writtenWithCover = _save.ClockAtWrite;
            int savesWithCover = _save.Saves;

            StopListeningForTheCover();
            _log.Clear();
            _save = new RecordingSave(_log);
            GameServices.Save = _save;
            GameClock unheard = RealClockAt(22);
            GameSaved withoutCover = Rest();

            CollectionAssert.AreEqual(new[] { Day, Written, Told }, _log, "no cover, and nothing waits for one");
            Assert.AreEqual(clockWithCover, unheard.TotalSeconds, Exact, "the same morning");
            Assert.AreEqual(writtenWithCover, _save.ClockAtWrite, Exact, "the same clock in the save");
            Assert.AreEqual(savesWithCover, _save.Saves, "the same single write");
            Assert.AreEqual(withCover.Ok, withoutCover.Ok);
            Assert.AreEqual(withCover.Reason, withoutCover.Reason);
        }

        // ---- the cover is always let go ---------------------------------------------------------

        [Test]
        public void A_save_that_throws_still_lets_the_light_back()
        {
            FakeClockAt(22);
            _save.Throws = true;

            Assert.Throws<InvalidOperationException>(() => Rest());

            CollectionAssert.AreEqual(new[] { Dark, Moved, Written, Light }, _log,
                "a cover that is held and never released is a black screen the player cannot leave");
        }

        [Test]
        public void A_clock_that_refuses_after_the_forecast_still_gets_its_cover_let_go()
        {
            RecordingClock clock = FakeClockAt(22);
            clock.Refuses = true;

            GameSaved outcome = Rest();

            CollectionAssert.AreEqual(new[] { Dark, Written, Light, Told }, _log);
            Assert.AreEqual(At(0, 22), clock.TotalSeconds, Exact, "the clock did not move");
            Assert.AreEqual(RestSaveResponder.SavedText, outcome.Reason, "and the player is told only what happened");
        }

        [Test]
        public void With_nothing_to_keep_the_day_the_cover_is_still_let_go()
        {
            RecordingClock clock = FakeClockAt(22);
            GameServices.Save = null;

            GameSaved outcome = Rest();

            CollectionAssert.AreEqual(new[] { Dark, Moved, Light, Told }, _log);
            Assert.AreEqual(At(1, _config.WakeHour), clock.TotalSeconds, Exact);
            Assert.IsFalse(outcome.Ok);
        }

        // ---- no morning coming: no cover ----------------------------------------------------------

        [Test]
        public void A_held_hour_raises_no_cover()
        {
            RecordingClock clock = FakeClockAt(22);
            clock.IsHeld = true;

            GameSaved outcome = Rest();

            CollectionAssert.AreEqual(new[] { Written, Told }, _log);
            Assert.AreEqual(RestSaveResponder.SavedText, outcome.Reason);
        }

        [Test]
        public void With_no_clock_no_cover_is_raised()
        {
            GameServices.Clock = null;

            Rest();

            CollectionAssert.AreEqual(new[] { Written, Told }, _log);
        }

        [Test]
        public void The_forecast_is_no_for_no_clock_a_held_clock_and_a_destroyed_one()
        {
            Assert.IsFalse(RestMorning.WillBring(null, _config), "no clock");

            RecordingClock held = FakeClockAt(22);
            held.IsHeld = true;
            Assert.IsFalse(RestMorning.WillBring(held, _config), "a held hour");
            held.IsHeld = false;
            Assert.IsTrue(RestMorning.WillBring(held, _config), "a running clock at ten at night");

            GameClock destroyed = RealClockAt(22);
            UnityEngine.Object.DestroyImmediate(destroyed.gameObject);
            Assert.IsFalse(RestMorning.WillBring(destroyed, _config), "a destroyed clock behind the interface");
        }

        [Test]
        public void The_forecast_moves_nothing_and_announces_nothing()
        {
            GameClock clock = RealClockAt(22);

            Assert.IsTrue(RestMorning.WillBring(clock, _config));

            Assert.AreEqual(At(0, 22), clock.TotalSeconds, Exact);
            CollectionAssert.IsEmpty(_log);
        }

        // ---- the fade back is the cover's own, and it ends ----------------------------------------

        [Test]
        public void The_fade_back_starts_black_and_is_clear_after_the_covers_own_duration()
        {
            float duration = RegionFade.DefaultFadeSeconds;

            Assert.AreEqual(1f, RegionFade.AlphaAfter(0f, duration), 1e-6f, "black at the release");
            Assert.AreEqual(0f, RegionFade.AlphaAfter(duration, duration), 1e-6f, "clear when its own time is up");
            Assert.AreEqual(0f, RegionFade.AlphaAfter(duration * 100f, duration), 1e-6f, "and it stays clear");
        }
    }
}
