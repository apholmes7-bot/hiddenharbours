using System.Reflection;
using HiddenHarbours.Art;
using HiddenHarbours.Core;
using HiddenHarbours.Environment;
using HiddenHarbours.Fishing;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.EditMode
{
    public class DayCalendarTests
    {
        [TestCase(0, Season.HighSummer, 1, 1)]
        [TestCase(27, Season.HighSummer, 28, 1)]
        [TestCase(28, Season.TheTurn, 1, 1)]
        [TestCase(56, Season.HardWinter, 1, 1)]
        [TestCase(83, Season.HardWinter, 28, 1)]
        [TestCase(84, Season.EarlySpring, 1, 2)]
        [TestCase(112, Season.HighSummer, 1, 2)]
        public void TheOffsetChangesLabelsAndTheFirstYearEndsAtEarlySpring(int day, Season season,
                                                                         int dayOfSeason, int year)
        {
            CalendarDate date = GameCalendar.AtDay(day, 28, 28);
            Assert.AreEqual(season, date.Season);
            Assert.AreEqual(dayOfSeason, date.DayOfSeason);
            Assert.AreEqual(year, date.Year);
        }

        [Test]
        public void TheCalendarAcceptsAnotherSeasonLengthAndANearbyPastDay()
        {
            CalendarDate shorter = GameCalendar.AtDay(21, 7, 7);
            Assert.AreEqual(Season.EarlySpring, shorter.Season);
            Assert.AreEqual(1, shorter.DayOfSeason);
            Assert.AreEqual(2, shorter.Year);
            CalendarDate yesterday = GameCalendar.AtDay(-1, 28, 28);
            Assert.AreEqual(Season.EarlySpring, yesterday.Season);
            Assert.AreEqual(28, yesterday.DayOfSeason);
        }

        [Test]
        public void TheShippedFirstMorningIsHighSummerDayOneWithoutTreeSnow()
        {
            GameConfig config = AssetDatabase.LoadAssetAtPath<GameConfig>("Assets/_Project/Data/Config/GameConfig.asset");
            Assert.IsNotNull(config);
            Assert.AreEqual(7.5f, config.StartHour);
            Assert.AreEqual(28, config.StartDayOfYear);
            CalendarDate date = GameCalendar.AtSeconds(config.StartHour / 24d * config.SecondsPerDay, config);
            Assert.AreEqual(Season.HighSummer, date.Season);
            Assert.AreEqual(1, date.DayOfSeason);
            Assert.AreEqual(1, date.Year);
            Assert.AreEqual(0f, FoliageSnowMath.Coverage(date.Season, date.DayOfSeason,
                                                      config.DaysPerSeason, config.FoliageSnow));
        }

        [Test]
        public void RestoringASaveKeepsItsExactSecondsAcrossTheCalendarOffset()
        {
            var config = ScriptableObject.CreateInstance<GameConfig>();
            var root = new GameObject("CalendarRestoreClock");
            try
            {
                var clock = root.AddComponent<GameClock>();
                typeof(GameClock).GetField("_config", BindingFlags.NonPublic | BindingFlags.Instance)
                    .SetValue(clock, config);
                var data = new SaveData { GameTimeSeconds = 138723.456789d };
                config.StartSeason = Season.EarlySpring;
                SaveRestore.RestoreClock(data, clock);
                double before = clock.TotalSeconds;
                int dayBefore = clock.DayIndex;
                Weekday weekdayBefore = clock.Weekday;
                config.StartSeason = Season.HighSummer;
                SaveRestore.RestoreClock(data, clock);
                Assert.AreEqual(138723.456789d, clock.TotalSeconds);
                Assert.AreEqual(before, clock.TotalSeconds);
                Assert.AreEqual(dayBefore, clock.DayIndex);
                Assert.AreEqual(weekdayBefore, clock.Weekday);
                Assert.AreEqual(138723.456789d, data.GameTimeSeconds);
            }
            finally { Object.DestroyImmediate(root); Object.DestroyImmediate(config); }
        }

        [Test]
        public void LegacySceneStartHoursCannotOverrideTheConfig()
        {
            var config = ScriptableObject.CreateInstance<GameConfig>();
            var root = new GameObject("ConfiguredStartClock");
            try
            {
                var clock = root.AddComponent<GameClock>();
                typeof(GameClock).GetField("_config", BindingFlags.NonPublic | BindingFlags.Instance)
                    .SetValue(clock, config);
                config.StartHour = 8.25f;
                EditorJsonUtility.FromJsonOverwrite("{\"_startHour\":3}", clock);
                Assert.AreEqual(config.SecondsPerDay * 8.25d / 24d, clock.StartSeconds);
            }
            finally { Object.DestroyImmediate(root); Object.DestroyImmediate(config); }
        }

        [Test]
        public void HistoricalFishSchoolsReadTheirFormationDayFromTheSameCalendar()
        {
            var config = ScriptableObject.CreateInstance<GameConfig>();
            try
            {
                var world = new LiveFishSchoolWorld(config, "region.test");
                Assert.AreEqual(Season.HighSummer, world.SeasonAt(0d));
                Assert.AreEqual(Season.HighSummer, world.SeasonAt(27.9d * config.SecondsPerDay));
                Assert.AreEqual(Season.TheTurn, world.SeasonAt(28d * config.SecondsPerDay));
            }
            finally { Object.DestroyImmediate(config); }
        }

        [Test]
        public void TheSummerStartFollowsTheConfiguredSeasonLength()
        {
            var config = ScriptableObject.CreateInstance<GameConfig>();
            try
            {
                config.DaysPerSeason = 21;
                Assert.AreEqual(21, config.StartDayOfYear);
                CalendarDate first = GameCalendar.AtSeconds(0d, config);
                Assert.AreEqual(Season.HighSummer, first.Season);
                Assert.AreEqual(1, first.DayOfSeason);
            }
            finally { Object.DestroyImmediate(config); }
        }

        [Test]
        public void GullsRollBackAcrossTheSeasonBoundaryFromTheirCalendarAnchor()
        {
            var calendar = new GullCalendar
            {
                SecondsPerHour = 75d, DaysPerSeason = 28, DaysPerWeek = 7,
                Season = 1, DayOfSeason = 1, Visibility = 1d
            };
            GullScheduleInputs before = GullSchedule.InputsAt(default, calendar, null, -75d);
            Assert.AreEqual(0, before.Season);
            Assert.AreEqual(27d / 28d, before.Progress);
            Assert.AreEqual(23d, before.Hour);
        }

        [Test]
        public void TheSkyWatchTintAndDarknessReadOneResource()
        {
            DayNightProfile sky = Resources.Load<DayNightProfile>("DayNightProfile");
            Assert.IsNotNull(sky);
            Assert.AreSame(sky, DaylightHours.Shared);
            Assert.AreEqual(5.5f, sky.SunriseHour);
            Assert.AreEqual(21.5f, sky.SunsetHour);
            Assert.AreEqual(22f, sky.NightHour);
            Assert.IsFalse(WatchFaceState.NightAt(20f));
            Assert.IsTrue(WatchFaceState.NightAt(21.5f));
            Assert.AreEqual(0.20f, DayNightMath.TintPhase(5.5f, sky), 1e-5f);
            Assert.AreEqual(0.75f, DayNightMath.TintPhase(21.5f, sky), 1e-5f);
            Assert.AreEqual(0.80f, DayNightMath.TintPhase(22f, sky), 1e-5f);
            Color afternoon = DayNightMath.DayNightTint(14.5f, sky, 1f, 0f);
            Assert.Greater(afternoon.b / afternoon.r, 0.95f, "the afternoon remains neutral daylight");
            Assert.Less(sky.Intensity.Evaluate(DayNightMath.TintPhase(22f, sky)), 0.19f);
            Assert.Less(DayNightMath.SunElevation(22f, sky.SunriseHour, sky.SunsetHour), 0f);
        }
    }
}
