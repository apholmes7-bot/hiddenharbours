using System;
using System.Collections.Generic;
using System.Reflection;
using HiddenHarbours.Core;
using HiddenHarbours.Economy;
using HiddenHarbours.Environment;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>Lying down in your own bed brings the next morning.</b> Before this, the bed saved and did
    /// not sleep: the only way to a new day (and to the market's recovery) was to wait out half an
    /// hour of real time. These pin the whole chain a rest now runs, in order: the clock is
    /// fast-forwarded to the wake hour, <see cref="DayStarted"/> fires once per midnight slept through,
    /// the once-a-day systems settle off it, and THEN the save is written, so a reload wakes on the
    /// same morning.
    ///
    /// <para>The clock under test is the real <see cref="GameClock"/> and the market the real
    /// <see cref="Market"/>; only the save service is a stand-in, which snapshots the clock at the
    /// write exactly as <c>SaveService.SnapshotLiveState</c> does and never goes near a disk.</para>
    /// </summary>
    public class BedBringsTheMorningTests
    {
        const FishCategory Cat = FishCategory.Pelagic;
        const string ConfigAssetPath = "Assets/_Project/Data/Config/GameConfig.asset";
        const double Exact = 1e-9;

        /// <summary>Snapshots the live clock into the blob when asked to write, as the real service
        /// does, and remembers what the clock read at that moment: the order under test.</summary>
        sealed class SnapshottingSave : ISaveService
        {
            public int Saves;
            public double ClockAtWrite = double.NaN;
            public string Json;
            public SaveData Current { get; } = SaveMigration.NewGame();
            public bool LoadedExistingSave { get; private set; }
            public bool GetFlag(string key) => false;
            public void SetFlag(string key, bool value) { }

            public void Save()
            {
                Saves++;
                IGameClock clock = GameServices.Clock;
                if (clock != null)
                {
                    Current.GameTimeSeconds = clock.TotalSeconds;
                    Current.DayIndex = clock.DayIndex;
                    ClockAtWrite = clock.TotalSeconds;
                }
                Json = SaveSerialization.ToJson(Current);
                LoadedExistingSave = true;
            }
        }

        /// <summary>A clock with no backing store, as most test fakes are: it inherits the interface's
        /// refusing <see cref="IGameClock.FastForwardTo"/>.</summary>
        sealed class StoppedClock : IGameClock
        {
            public double TotalSeconds => 1650d;
            public GameTime Now => new GameTime(TotalSeconds);
            public Season Season => Season.HighSummer;
            public int Year => 1;
            public int DayIndex => 0;
            public int DayOfSeason => 1;
            public Weekday Weekday => Weekday.Monday;
            public bool IsMarketDay => false;
            public float HourOfDay => 22f;
            public float DayFraction => 22f / 24f;
            public bool IsPaused { get; set; }
            public float TimeScale { get; set; } = 1f;
        }

        readonly List<UnityEngine.Object> _spawned = new List<UnityEngine.Object>();
        readonly List<DayStarted> _days = new List<DayStarted>();
        readonly List<SeasonChanged> _seasons = new List<SeasonChanged>();
        readonly List<GameSaved> _saved = new List<GameSaved>();
        Action<DayStarted> _onDay;
        Action<SeasonChanged> _onSeason;
        Action<GameSaved> _onSaved;

        GameConfig _config;
        GameClock _clock;
        SnapshottingSave _save;
        Market _market;

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
            _clock = NewClock("BedMorningClock");
            _save = new SnapshottingSave();

            GameServices.Clock = _clock;
            GameServices.Config = _config;
            GameServices.Save = _save;
            GameServices.CurrentRegionId = null;

            _days.Clear();
            _seasons.Clear();
            _saved.Clear();
            _onDay = _days.Add;
            _onSeason = _seasons.Add;
            _onSaved = _saved.Add;
            EventBus.Subscribe(_onDay);
            EventBus.Subscribe(_onSeason);
            EventBus.Subscribe(_onSaved);
        }

        [TearDown]
        public void TearDown()
        {
            EventBus.Unsubscribe(_onDay);
            EventBus.Unsubscribe(_onSeason);
            EventBus.Unsubscribe(_onSaved);
            if (_market != null) Lifecycle(_market, "OnDisable");
            _market = null;
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

        GameClock NewClock(string name)
        {
            var go = new GameObject(name);
            _spawned.Add(go);
            var clock = go.AddComponent<GameClock>();
            SetField(clock, "_config", _config);
            clock.SeekTo(clock.StartSeconds);
            return clock;
        }

        EnvironmentService NewSea(string name)
        {
            var go = new GameObject(name);
            _spawned.Add(go);
            var sea = go.AddComponent<EnvironmentService>();
            SetField(sea, "_config", _config);
            return sea;
        }

        /// <summary>A real market, listening through its OWN subscription. EditMode never runs a
        /// component's <c>OnEnable</c>, so it is run here; the handler under test is the market's.</summary>
        Market NewListeningMarket()
        {
            var go = new GameObject("BedMorningMarket");
            _spawned.Add(go);
            _market = go.AddComponent<Market>();
            _market.Configure(_config, MarketId.NineMileCreek);
            Lifecycle(_market, "OnEnable");
            return _market;
        }

        static void SetField(object target, string field, object value)
        {
            FieldInfo info = target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(info, $"{target.GetType().Name} has no field {field}");
            info.SetValue(target, value);
        }

        static void Lifecycle(object target, string method)
        {
            MethodInfo info = target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(info, $"{target.GetType().Name} has no {method}");
            info.Invoke(target, null);
        }

        /// <summary>The game-time of <paramref name="hour"/> on elapsed day <paramref name="day"/>.</summary>
        double At(int day, double hour) =>
            day * (double)_config.SecondsPerDay + _config.SecondsPerDay * (hour / 24d);

        static GameSaved Rest() => RestSaveResponder.Fulfil(new RestSaveRequested("your bed at Ginny's"));

        void AssertDay(int elapsedDay, DayStarted actual)
        {
            CalendarDate expected = GameCalendar.AtDay(elapsedDay, _config.DaysPerSeason, _config.StartDayOfYear);
            Assert.AreEqual(expected.DayOfSeason, actual.DayOfSeason, $"day of season for elapsed day {elapsedDay}");
            Assert.AreEqual(expected.Season, actual.Season, $"season for elapsed day {elapsedDay}");
            Assert.AreEqual(expected.Year, actual.Year, $"year for elapsed day {elapsedDay}");
        }

        // ---- the night ------------------------------------------------------------------------

        [Test]
        public void Resting_at_ten_at_night_wakes_at_the_wake_hour_of_the_next_day()
        {
            _clock.SeekTo(At(0, 22));

            GameSaved outcome = Rest();

            Assert.AreEqual(1, _clock.DayIndex, "the next day");
            Assert.AreEqual(At(1, _config.WakeHour), _clock.TotalSeconds, Exact, "at the wake hour");
            Assert.AreEqual(_config.WakeHour, _clock.HourOfDay, 1e-3f);
            Assert.IsTrue(outcome.Ok);
            Assert.AreEqual(RestSaveResponder.WokeText, outcome.Reason);
        }

        [Test]
        public void The_night_starts_exactly_one_day_and_names_it()
        {
            _clock.SeekTo(At(0, 22));

            Rest();

            Assert.AreEqual(1, _days.Count, "DayStarted fires once for the one midnight slept through");
            AssertDay(1, _days[0]);
            Assert.AreEqual(0, _seasons.Count, "no season turned");

            _clock.Advance(1f);
            Assert.AreEqual(1, _days.Count, "and the first frame of the morning does not start the day again");
        }

        [Test]
        public void The_day_is_announced_from_its_own_midnight()
        {
            _clock.SeekTo(At(0, 22));
            double clockAtAnnouncement = double.NaN;
            int dayAtAnnouncement = -1;
            Action<DayStarted> read = _ =>
            {
                clockAtAnnouncement = _clock.TotalSeconds;
                dayAtAnnouncement = _clock.DayIndex;
            };
            EventBus.Subscribe(read);
            try { Rest(); }
            finally { EventBus.Unsubscribe(read); }

            Assert.AreEqual(At(1, 0), clockAtAnnouncement, Exact, "a subscriber reading the clock sees the new day's first instant");
            Assert.AreEqual(1, dayAtAnnouncement);
            Assert.AreEqual(At(1, _config.WakeHour), _clock.TotalSeconds, Exact, "and the clock then lands on the morning");
        }

        [Test]
        public void A_night_that_ends_a_season_announces_the_new_season_once()
        {
            int lastDayOfSeason = _config.DaysPerSeason - _config.StartDayOfSeason;
            _clock.SeekTo(At(lastDayOfSeason, 22));
            Season before = _clock.Season;

            Rest();

            Assert.AreEqual(1, _days.Count);
            AssertDay(lastDayOfSeason + 1, _days[0]);
            Assert.AreEqual(1, _seasons.Count, "the season turned once");
            Assert.AreNotEqual(before, _seasons[0].Season);
            Assert.AreEqual(_clock.Season, _seasons[0].Season);

            _clock.Advance(1f);
            Assert.AreEqual(1, _seasons.Count, "and the morning's first frame does not turn it again");
        }

        [Test]
        public void The_markets_daily_recovery_is_applied_by_the_night()
        {
            Market market = NewListeningMarket();
            _clock.SeekTo(At(0, 22));
            market.RegisterSale(Cat, 8);
            float glut = market.SupplyOf(Cat);
            Assert.Greater(glut, 0f, "a sold lot leaves a glut");

            Rest();

            float expected = MarketMath.SettleSupplyDaily(glut, _config.MarketDailyRecovery);
            Assert.AreEqual(expected, market.SupplyOf(Cat), "one daily settle, the config's fraction");
            Assert.Less(market.SupplyOf(Cat), glut, "so tomorrow's price is better than tonight's");
        }

        [Test]
        public void Resting_twice_in_a_row_brings_one_morning_each()
        {
            Market market = NewListeningMarket();
            _clock.SeekTo(At(0, 22));
            market.RegisterSale(Cat, 8);
            float glut = market.SupplyOf(Cat);

            Rest();
            Assert.AreEqual(1, _clock.DayIndex);
            Assert.AreEqual(At(1, _config.WakeHour), _clock.TotalSeconds, Exact);

            Rest();
            Assert.AreEqual(2, _clock.DayIndex, "a rest at the wake hour itself is still a night");
            Assert.AreEqual(At(2, _config.WakeHour), _clock.TotalSeconds, Exact);

            Assert.AreEqual(2, _days.Count, "one day started per night");
            AssertDay(1, _days[0]);
            AssertDay(2, _days[1]);
            Assert.AreEqual(2, _save.Saves, "one write per rest");
            float twice = MarketMath.SettleSupplyDaily(
                MarketMath.SettleSupplyDaily(glut, _config.MarketDailyRecovery), _config.MarketDailyRecovery);
            Assert.AreEqual(twice, market.SupplyOf(Cat), "and one settle per night");
        }

        // ---- the save -------------------------------------------------------------------------

        [Test]
        public void The_save_is_written_after_the_morning_comes()
        {
            _clock.SeekTo(At(0, 22));

            Rest();

            Assert.AreEqual(1, _save.Saves);
            Assert.AreEqual(At(1, _config.WakeHour), _save.ClockAtWrite, Exact,
                "the write snapshots the clock, so it must see the morning and not last night");
            Assert.AreEqual(At(1, _config.WakeHour), _save.Current.GameTimeSeconds, Exact);
            Assert.AreEqual(1, _save.Current.DayIndex);
        }

        [Test]
        public void A_save_written_by_rest_reloads_to_the_same_clock()
        {
            _clock.SeekTo(At(0, 22));
            Rest();
            double wokenAt = _clock.TotalSeconds;
            float wokenHour = _clock.HourOfDay;
            _days.Clear();

            SaveData loaded = SaveSerialization.FromJson(_save.Json);
            GameClock reloaded = NewClock("ReloadedClock");
            SaveRestore.RestoreClock(loaded, reloaded);

            Assert.AreEqual(wokenAt, reloaded.TotalSeconds, "the reload wakes on the same instant");
            Assert.AreEqual(1, reloaded.DayIndex);
            Assert.AreEqual(wokenHour, reloaded.HourOfDay);
            Assert.AreEqual(reloaded.TotalSeconds, reloaded.FlowSeconds);

            reloaded.Advance(1f);
            Assert.AreEqual(0, _days.Count, "a reload is not a second night: the day does not start again");
        }

        [Test]
        public void The_bed_signal_through_the_bus_brings_the_morning_before_the_write()
        {
            _clock.SeekTo(At(0, 22));
            RestSaveResponder.Install();

            EventBus.Publish(new RestSaveRequested("your bed at Ginny's"));

            Assert.AreEqual(1, _saved.Count);
            Assert.IsTrue(_saved[0].Ok);
            Assert.AreEqual(RestSaveResponder.WokeText, _saved[0].Reason);
            Assert.AreEqual(At(1, _config.WakeHour), _save.ClockAtWrite, Exact);
            Assert.AreEqual(1, _days.Count);
        }

        // ---- determinism ----------------------------------------------------------------------

        [Test]
        public void The_sea_woken_into_is_the_sea_a_fresh_clock_reads_at_that_hour()
        {
            _clock.SeekTo(At(0, 22));
            EnvironmentService slept = NewSea("SleptSea");
            double morning = At(1, _config.WakeHour);
            float tideForecastLastNight = slept.TideHeightAt(morning);

            Rest();
            EnvironmentSample woken = slept.Sample();

            // A second world that never slept: its clock is simply put at the same instant.
            GameClock freshClock = NewClock("FreshClock");
            freshClock.SeekTo(morning);
            GameServices.Clock = freshClock;
            EnvironmentSample fresh = NewSea("FreshSea").Sample();

            Assert.AreEqual(fresh.TideHeight, woken.TideHeight, "tide");
            Assert.AreEqual(fresh.WindVector, woken.WindVector, "wind");
            Assert.AreEqual(fresh.CurrentVector, woken.CurrentVector, "current");
            Assert.AreEqual(fresh.SeaState, woken.SeaState, "sea state");
            Assert.AreEqual(fresh.SeaState01, woken.SeaState01, "sea state axis");
            Assert.AreEqual(fresh.Visibility, woken.Visibility, "visibility");

            // And both are the pure functions of (seed, time): nothing rode through the night.
            Assert.AreEqual(TideModel.Height(morning, slept.ActiveTideProfile, _config), woken.TideHeight);
            WeatherModel.Sample(morning, slept.WorldSeed, _config, slept.ActiveWindProfile,
                                out Vector2 wind, out SeaState sea, out float visibility);
            Assert.AreEqual(wind, woken.WindVector);
            Assert.AreEqual(sea, woken.SeaState);
            Assert.AreEqual(visibility, woken.Visibility);
            Assert.AreEqual(tideForecastLastNight, woken.TideHeight, "the tide read ahead last night is the tide woken into");
        }

        // ---- the edges ------------------------------------------------------------------------

        [TestCase(0, 22d, 1)]
        [TestCase(0, 12d, 1)]
        [TestCase(0, 7.5d, 1)]
        [TestCase(1, 0d, 1)]
        [TestCase(1, 2d, 1)]
        [TestCase(1, 5.8333333d, 1)]
        [TestCase(1, 6d, 2)]
        public void The_wake_instant_is_the_next_time_the_clock_reads_the_wake_hour(int day, double hour, int wakeDay)
        {
            double wake = RestMorning.WakeSeconds(At(day, hour), _config);

            Assert.AreEqual(At(wakeDay, GameConfig.DefaultWakeHour), wake, Exact);
            Assert.Greater(wake, At(day, hour), "a rest always moves the clock forward");
        }

        [Test]
        public void The_wake_rule_is_safe_at_its_own_boundaries()
        {
            const double day = 1800d;
            Assert.AreEqual(450d, RestMorning.WakeSeconds(-50d, day, 6f), Exact, "before the game began reads as its start");
            Assert.AreEqual(1800d + 450d, RestMorning.WakeSeconds(450d + 1e-7, day, 6f), Exact,
                "standing within rounding of the wake instant is standing on it");
            Assert.AreEqual(1800d, RestMorning.WakeSeconds(1650d, day, 24f), Exact, "hour 24 is midnight");
            Assert.AreEqual(1800d, RestMorning.WakeSeconds(1650d, day, 0f), Exact);
            Assert.AreEqual(1800d + 412.5d, RestMorning.WakeSeconds(1650d, day, 5.5f), Exact, "a half hour is honoured");
            Assert.AreEqual(1650d, RestMorning.WakeSeconds(1650d, 0d, 6f), "no day length, nowhere to wake");
            Assert.AreEqual(1650d, RestMorning.WakeSeconds(1650d, -1d, 6f));
        }

        [Test]
        public void A_lie_down_after_midnight_wakes_the_same_morning_and_starts_no_second_day()
        {
            Market market = NewListeningMarket();
            _clock.SeekTo(At(1, 2));
            market.RegisterSale(Cat, 8);
            float glut = market.SupplyOf(Cat);

            GameSaved outcome = Rest();

            Assert.AreEqual(1, _clock.DayIndex, "the same calendar day");
            Assert.AreEqual(At(1, _config.WakeHour), _clock.TotalSeconds, Exact);
            Assert.AreEqual(0, _days.Count, "the day already started at midnight, in front of the player");
            Assert.AreEqual(glut, market.SupplyOf(Cat), "so the market does not settle twice for one day");
            Assert.AreEqual(RestSaveResponder.WokeText, outcome.Reason);
            Assert.AreEqual(At(1, _config.WakeHour), _save.ClockAtWrite, Exact);
        }

        [Test]
        public void The_wake_hour_is_the_configs_to_set()
        {
            _config.WakeHour = 5f;
            _clock.SeekTo(At(0, 22));

            Rest();

            Assert.AreEqual(At(1, 5), _clock.TotalSeconds, Exact);
        }

        [Test]
        public void The_shipped_config_wakes_at_the_designs_hour()
        {
            var shipped = AssetDatabase.LoadAssetAtPath<GameConfig>(ConfigAssetPath);
            Assert.IsNotNull(shipped, $"missing: {ConfigAssetPath}");
            Assert.AreEqual(GameConfig.DefaultWakeHour, shipped.WakeHour,
                "a key the asset does not carry reads as 0, which would wake the player at midnight");
        }

        [Test]
        public void A_held_hour_is_not_moved_by_a_rest_and_the_rest_still_saves()
        {
            _clock.SeekTo(At(0, 22));
            _clock.IsHeld = true;

            GameSaved outcome = Rest();

            Assert.AreEqual(At(0, 22), _clock.TotalSeconds, "whoever holds the hour owns it");
            Assert.AreEqual(0, _days.Count);
            Assert.AreEqual(1, _save.Saves);
            Assert.IsTrue(outcome.Ok);
            Assert.AreEqual(RestSaveResponder.SavedText, outcome.Reason, "a rest with no morning says only that the day is kept");
        }

        [Test]
        public void A_clock_that_cannot_fast_forward_leaves_the_rest_a_save()
        {
            var stopped = new StoppedClock();
            GameServices.Clock = stopped;

            GameSaved outcome = Rest();

            Assert.AreEqual(0, _days.Count);
            Assert.AreEqual(1, _save.Saves);
            Assert.AreEqual(RestSaveResponder.SavedText, outcome.Reason);
        }

        [Test]
        public void With_no_clock_at_all_the_rest_is_the_save_it_always_was()
        {
            GameServices.Clock = null;

            GameSaved outcome = Rest();

            Assert.AreEqual(0, _days.Count);
            Assert.AreEqual(1, _save.Saves);
            Assert.IsTrue(outcome.Ok);
            Assert.AreEqual(RestSaveResponder.SavedText, outcome.Reason);
        }

        [Test]
        public void A_destroyed_clock_left_in_the_services_starts_no_day()
        {
            GameClock dead = NewClock("DeadClock");
            dead.SeekTo(At(0, 22));
            GameServices.Clock = dead;
            UnityEngine.Object.DestroyImmediate(dead.gameObject);

            Assert.IsFalse(RestMorning.Bring(GameServices.Clock, _config));
            Assert.AreEqual(0, _days.Count);
        }

        [Test]
        public void With_nothing_to_keep_the_day_the_morning_still_comes()
        {
            _clock.SeekTo(At(0, 22));
            GameServices.Save = null;

            GameSaved outcome = Rest();

            Assert.IsFalse(outcome.Ok);
            Assert.AreEqual(RestSaveResponder.NoServiceText, outcome.Reason);
            Assert.AreEqual(At(1, _config.WakeHour), _clock.TotalSeconds, Exact, "sleeping needs no disk");
            Assert.AreEqual(1, _days.Count);
        }

        // ---- the clock's own door -------------------------------------------------------------

        [Test]
        public void Fast_forward_across_three_midnights_starts_three_days_in_order()
        {
            _clock.SeekTo(At(0, 22));
            double flowBefore = _clock.FlowSeconds;
            double span = At(3, 6) - At(0, 22);

            Assert.IsTrue(_clock.FastForwardTo(At(3, 6)));

            Assert.AreEqual(3, _days.Count, "once per crossed midnight");
            AssertDay(1, _days[0]);
            AssertDay(2, _days[1]);
            AssertDay(3, _days[2]);
            Assert.AreEqual(At(3, 6), _clock.TotalSeconds, Exact);
            Assert.AreEqual(3, _clock.DayIndex);
            Assert.AreEqual(flowBefore + span, _clock.FlowSeconds, Exact, "the world flowed through the nights too");
        }

        [Test]
        public void Fast_forward_refuses_to_stand_still_or_run_backwards()
        {
            _clock.SeekTo(At(1, 6));

            Assert.IsFalse(_clock.FastForwardTo(At(1, 6)));
            Assert.IsFalse(_clock.FastForwardTo(At(0, 22)));
            Assert.IsFalse(_clock.FastForwardTo(double.NaN));

            Assert.AreEqual(At(1, 6), _clock.TotalSeconds, "a refusal changes nothing");
            Assert.AreEqual(0, _days.Count, "and publishes nothing");
        }

        [Test]
        public void Fast_forward_keeps_what_a_held_span_put_between_flow_and_hour()
        {
            _clock.SeekTo(At(0, 22));
            _clock.IsHeld = true;
            _clock.Advance(10f);
            _clock.IsHeld = false;
            Assert.AreEqual(10d, _clock.FlowSeconds - _clock.TotalSeconds, Exact);

            Assert.IsTrue(_clock.FastForwardTo(At(1, 6)));

            Assert.AreEqual(10d, _clock.FlowSeconds - _clock.TotalSeconds, Exact);
        }
    }
}
