using System.Reflection;
using HiddenHarbours.Art;
using HiddenHarbours.Core;
using HiddenHarbours.Environment;
using HiddenHarbours.Fishing;
using HiddenHarbours.Tests.Support;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.EditMode
{
    public class DayCalendarTests
    {
        [Test]
        public void PlateDatesFollowEverySeasonBoundary(
            [Values(Season.EarlySpring, Season.HighSummer, Season.TheTurn, Season.HardWinter)] Season season,
            [Values(1, 7, 28)] int daysPerSeason,
            [Values(Season.HighSummer, Season.HardWinter)] Season startSeason,
            [Values(false, true)] bool startOnLastDay)
        {
            var config = ScriptableObject.CreateInstance<GameConfig>();
            try
            {
                config.DaysPerSeason = daysPerSeason;
                config.StartSeason = startSeason;
                config.StartDayOfSeason = startOnLastDay ? daysPerSeason : 1;
                int elapsedDay = PlateCalendar.ElapsedDay(config, season, 1);
                Assert.That(elapsedDay, Is.InRange(0, 4 * daysPerSeason - 1));
                double seconds = PlateCalendar.SecondsAt(config, season, 1, 0d);
                Assert.AreEqual(elapsedDay * (double)config.SecondsPerDay, seconds);
                CalendarDate first = GameCalendar.AtSeconds(seconds, config);
                Assert.AreEqual(season, first.Season);
                Assert.AreEqual(1, first.DayOfSeason);

                // Check both sides in the next cycle, so the prior hour is never before elapsed day zero.
                double boundary = seconds + config.SecondsPerYear;
                double hour = config.SecondsPerDay / (double)DaySchedule.HoursPerDay;
                CalendarDate before = GameCalendar.AtSeconds(boundary - hour, config);
                CalendarDate at = GameCalendar.AtSeconds(boundary, config);
                CalendarDate after = GameCalendar.AtSeconds(boundary + hour, config);
                Assert.AreEqual((Season)(((int)season + 3) % 4), before.Season);
                Assert.AreEqual(daysPerSeason, before.DayOfSeason);
                Assert.AreEqual(season, at.Season);
                Assert.AreEqual(1, at.DayOfSeason);
                Assert.AreEqual(first.Year + 1, at.Year);
                Assert.AreEqual(season == Season.EarlySpring ? at.Year - 1 : at.Year, before.Year);
                Assert.AreEqual(season, after.Season);
                Assert.AreEqual(1, after.DayOfSeason);
                Assert.AreEqual(at.Year, after.Year);
            }
            finally { Object.DestroyImmediate(config); }
        }

        [TestCase(13d, 1, 13d)]
        [TestCase(17.75d, 1, 17.75d)]
        [TestCase(26d, 2, 2d)]
        public void TreePlateHoursStartOnHighSummerDayOne(double hours, int dayOfSeason, double hourOfDay)
        {
            var config = ScriptableObject.CreateInstance<GameConfig>();
            try
            {
                Assert.AreEqual(0, PlateCalendar.ElapsedDay(config, Season.HighSummer, 1));
                double seconds = PlateCalendar.SecondsAt(config, Season.HighSummer, 1, hours);
                Assert.AreEqual(hours / 24d * config.SecondsPerDay, seconds);
                CalendarDate date = GameCalendar.AtSeconds(seconds, config);
                Assert.AreEqual(Season.HighSummer, date.Season);
                Assert.AreEqual(dayOfSeason, date.DayOfSeason);
                Assert.AreEqual(1, date.Year);
                Assert.AreEqual(hourOfDay, seconds % config.SecondsPerDay / config.SecondsPerDay * 24d, 1e-9);
            }
            finally { Object.DestroyImmediate(config); }
        }

        [TestCase(Season.TheTurn, 2, 29)]
        [TestCase(Season.TheTurn, 12, 39)]
        [TestCase(Season.TheTurn, 25, 52)]
        [TestCase(Season.TheTurn, 17, 44)]
        public void PlantPlateDatesKeepTheirElapsedDays(Season season, int dayOfSeason, int elapsedDay)
        {
            var config = ScriptableObject.CreateInstance<GameConfig>();
            try
            {
                Assert.AreEqual(elapsedDay, PlateCalendar.ElapsedDay(config, season, dayOfSeason));
                CalendarDate date = GameCalendar.AtDay(elapsedDay, config.DaysPerSeason, config.StartDayOfYear);
                Assert.AreEqual(season, date.Season);
                Assert.AreEqual(dayOfSeason, date.DayOfSeason);
                Assert.AreEqual(1, date.Year);
            }
            finally { Object.DestroyImmediate(config); }
        }

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
            Assert.AreEqual(0.75f, DayNightMath.TintPhase(sky.SunsetHour - sky.WarmKeyBeforeSunsetHours, sky), 1e-5f);
            Assert.AreEqual(0.80f, DayNightMath.TintPhase(22f, sky), 1e-5f);
            Color afternoon = DayNightMath.DayNightTint(14.5f, sky, 1f, 0f);
            Assert.Greater(afternoon.b / afternoon.r, 0.95f, "the afternoon remains neutral daylight");
            Assert.Less(sky.Intensity.Evaluate(DayNightMath.TintPhase(22f, sky)), 0.19f);
            Assert.Less(DayNightMath.SunElevation(22f, sky.SunriseHour, sky.SunsetHour), 0f);
        }
    }
}
