using System;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.Support
{
    /// <summary>Resolve a plate's date through the same calendar that labels the saved clock.</summary>
    public static class PlateCalendar
    {
        /// <summary>The first nonnegative elapsed day with this date, within one calendar cycle.</summary>
        public static int ElapsedDay(GameConfig config, Season season, int dayOfSeason)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            int daysPerYear = Math.Max(1, config.DaysPerSeason) * Enum.GetValues(typeof(Season)).Length;
            for (int day = 0; day < daysPerYear; day++)
            {
                CalendarDate candidate = GameCalendar.AtDay(day, config.DaysPerSeason, config.StartDayOfYear);
                if (candidate.Season == season && candidate.DayOfSeason == dayOfSeason)
                    return day;
            }
            throw new ArgumentOutOfRangeException(nameof(dayOfSeason),
                $"No day in the calendar cycle names {season} {dayOfSeason}.");
        }

        /// <summary>Hours are measured from that date's midnight; 26 means 02:00 the following day.</summary>
        public static double SecondsAt(GameConfig config, Season season, int dayOfSeason, double hours) =>
            (ElapsedDay(config, season, dayOfSeason) + hours / DaySchedule.HoursPerDay) * config.SecondsPerDay;
    }
}
