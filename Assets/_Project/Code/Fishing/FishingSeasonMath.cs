using System.Collections.Generic;
using HiddenHarbours.Core;

namespace HiddenHarbours.Fishing
{
    /// <summary>
    /// A calendar day as the season check reads it: the four-season calendar label plus the season length
    /// it was cut from. The default value is UNKNOWN (no clock wired, e.g. an EditMode fixture); an unknown
    /// date never closes a fishery, so nothing that runs without a clock changes behaviour.
    /// </summary>
    public readonly struct SeasonDate
    {
        public readonly bool IsKnown;
        public readonly Season Season;
        public readonly int DayOfSeason;     // 1-based
        public readonly int DaysPerSeason;   // the calendar's season length (GameConfig.DaysPerSeason)

        public SeasonDate(Season season, int dayOfSeason, int daysPerSeason)
        {
            IsKnown = true;
            Season = season;
            DayOfSeason = dayOfSeason;
            DaysPerSeason = daysPerSeason;
        }

        /// <summary>The date read off a clock, or <see cref="IsKnown"/> false without one.</summary>
        public static SeasonDate From(IGameClock clock, int daysPerSeason)
            => clock != null ? new SeasonDate(clock.Season, clock.DayOfSeason, daysPerSeason) : default;
    }

    /// <summary>
    /// The pure, deterministic open/closed read for per-port fishing seasons (<see cref="FishingSeasonDef"/>).
    /// Open or closed is a function of (species, region, calendar day) only: no randomness, nothing saved,
    /// recomputed on every read (rule 5).
    ///
    /// <para><b>Day of year.</b> A window is authored as season + day-of-season at each end, which is a
    /// day of the four-season year that survives an owner retune of <c>GameConfig.DaysPerSeason</c>
    /// (an authored day past the season's length clamps to its last day). Both ends are INCLUSIVE. A window
    /// whose open day falls after its close day wraps the year end (open through the winter turn).</para>
    ///
    /// <para><b>No entry means open.</b> A species/region pair with no season Def is open all year, so
    /// every fishery that has not been given a season keeps working exactly as before. Several entries
    /// for one pair are a union (open if any window holds the day), which is how a split spring + fall
    /// district is authored.</para>
    /// </summary>
    public static class FishingSeasonMath
    {
        /// <summary>0-based day of the four-season year for a season + 1-based day-of-season. The day
        /// clamps into [1, daysPerSeason]; a non-positive season length counts as 1.</summary>
        public static int DayOfYear(Season season, int dayOfSeason, int daysPerSeason)
        {
            int len = daysPerSeason < 1 ? 1 : daysPerSeason;
            int day = dayOfSeason < 1 ? 1 : (dayOfSeason > len ? len : dayOfSeason);
            return (int)season * len + day - 1;
        }

        /// <summary>Whether <paramref name="day"/> falls inside the INCLUSIVE window
        /// [<paramref name="openDay"/>, <paramref name="closeDay"/>]; an open day after the close day wraps
        /// the year end. Equal ends are a one-day window.</summary>
        public static bool IsDayInWindow(int openDay, int closeDay, int day)
            => openDay <= closeDay
                ? day >= openDay && day <= closeDay
                : day >= openDay || day <= closeDay;

        /// <summary>Whether one authored window holds the date. An unknown date is always inside.</summary>
        public static bool IsOpen(FishingSeasonDef season, in SeasonDate date)
        {
            if (season == null || !date.IsKnown) return true;
            int len = date.DaysPerSeason;
            return IsDayInWindow(DayOfYear(season.OpenSeason, season.OpenDayOfSeason, len),
                                 DayOfYear(season.CloseSeason, season.CloseDayOfSeason, len),
                                 DayOfYear(date.Season, date.DayOfSeason, len));
        }

        /// <summary>Whether <paramref name="speciesId"/> may be KEPT in <paramref name="regionId"/> on
        /// <paramref name="date"/>. Open when the date is unknown, when no entry names this species and
        /// region, or when any matching entry's window holds the day. Ids compare ordinal-ignore-case
        /// (the id convention). No allocation.</summary>
        public static bool IsOpen(IReadOnlyList<FishingSeasonDef> seasons, string speciesId, string regionId,
                                  in SeasonDate date)
        {
            if (!date.IsKnown || seasons == null || string.IsNullOrEmpty(speciesId)) return true;
            bool anyEntry = false;
            for (int i = 0; i < seasons.Count; i++)
            {
                FishingSeasonDef s = seasons[i];
                if (s == null || !s.Covers(speciesId, regionId)) continue;
                if (IsOpen(s, in date)) return true;
                anyEntry = true;
            }
            return !anyEntry;
        }
    }
}
