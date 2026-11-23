using System;
using System.Collections.Generic;
using HiddenHarbours.Core;

namespace HiddenHarbours.Economy
{
    /// <summary>
    /// <b>Which days the supply run goes</b>, as a pure function of the day (rule 5). A run is scheduled every
    /// <c>cadenceDays</c> counted from day 0. If a scheduled day is impossible (no daylight round trip over the
    /// bar), the run goes on the first possible day after it, and at most once, before the next scheduled day;
    /// if none is possible, that run is missed. So whether day <c>d</c> has a run depends only on the days back
    /// to the last scheduled one: nothing is remembered or saved.
    /// </summary>
    public static class IslandRunCalendar
    {
        /// <summary>True when a run is scheduled for <paramref name="day"/>.</summary>
        public static bool IsScheduled(int day, int cadenceDays) =>
            cadenceDays > 0 && day >= 0 && day % cadenceDays == 0;

        /// <summary>True when the run goes on <paramref name="day"/>: the day is possible, and either it is
        /// scheduled or no day since the last scheduled one was possible.</summary>
        public static bool RunsOn(int day, int cadenceDays, Func<int, bool> possible)
        {
            if (cadenceDays <= 0 || day < 0 || possible == null) return false;
            if (!possible(day)) return false;
            int scheduled = day - day % cadenceDays;
            for (int t = scheduled; t < day; t++)
                if (possible(t)) return false;
            return true;
        }

        /// <summary>
        /// Days <paramref name="firstDay"/>..<paramref name="lastDay"/> (inclusive) on which the runner can go
        /// over the bar and back inside one low-water window within the route's working day, from the island's
        /// real water level. The tide is passed in as <paramref name="waterAt"/> (clock seconds to metres), so
        /// this module never computes its own tide. Days outside the range read as impossible.
        /// </summary>
        public static Func<int, bool> DaylightRunDays(Func<double, float> waterAt, SupplyRoleDef runner,
                                                      IslandSupplyConfig route, GameConfig cfg,
                                                      int firstDay, int lastDay)
        {
            if (waterAt == null || runner == null || route == null || cfg == null || lastDay < firstDay)
                return _ => false;
            int from = Math.Max(0, firstDay);
            if (lastDay < from) return _ => false;
            double secondsPerDay = cfg.SecondsPerDay;
            // One day of margin each side so a window that straddles midnight is found whole.
            List<TideWindow> windows = CrossingMath.BarWindows(waterAt, route, cfg,
                                                               Math.Max(0, from - 1) * secondsPerDay, (lastDay + 2) * secondsPerDay);
            var ok = new bool[lastDay - from + 1];
            for (int d = from; d <= lastDay; d++)
                ok[d - from] = CrossingMath.TryDaylightRoundTrip(waterAt, windows, d, runner, route, cfg, out _);
            return day => day >= from && day <= lastDay && ok[day - from];
        }
    }
}
