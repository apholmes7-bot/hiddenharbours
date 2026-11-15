using System;
using System.Collections.Generic;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// The dates of one plot's work in one year, as 0-based days of the year (farm slice 1). A tender's
    /// schedule is seeded (<see cref="FarmMath.ScheduleFor"/>); a later player-worked plot builds one from
    /// the days its work was actually done, and <see cref="FarmMath.StateOn"/> reads either the same way.
    /// </summary>
    public readonly struct FarmPlotSchedule
    {
        /// <summary>False in a rest year (or with no crop): the ground stays fallow all year.</summary>
        public readonly bool HasCrop;
        public readonly int TillDay;
        public readonly int PlantDay;
        /// <summary>Sowing plus the crop's days to mature.</summary>
        public readonly int MatureDay;
        /// <summary>The first day the crop counts as ready: mature AND inside its harvest window.</summary>
        public readonly int ReadyDay;
        /// <summary>The day it is lifted; stubble from this day on. Always after <see cref="ReadyDay"/>.</summary>
        public readonly int HarvestDay;

        public FarmPlotSchedule(int tillDay, int plantDay, int matureDay, int readyDay, int harvestDay)
        {
            HasCrop = true;
            TillDay = tillDay;
            PlantDay = plantDay;
            MatureDay = matureDay;
            ReadyDay = readyDay;
            HarvestDay = harvestDay;
        }

        /// <summary>A rest year: no work, fallow throughout.</summary>
        public static FarmPlotSchedule Rest => default;
    }

    /// <summary>What a plot looks like on one day (farm slice 1). Recomputed, never saved (rule 5).</summary>
    public readonly struct FarmPlotState
    {
        public readonly FarmStage Stage;
        /// <summary>0 at sowing .. 1 at mature; 0 before sowing, 1 once harvested.</summary>
        public readonly float Growth01;
        /// <summary>The crop sown this year, or null in a rest year.</summary>
        public readonly string CropId;
        public readonly int Year;
        public readonly int DayOfYear;
        public readonly FarmPlotSchedule Schedule;

        public FarmPlotState(FarmStage stage, float growth01, string cropId, int year, int dayOfYear,
                             FarmPlotSchedule schedule)
        {
            Stage = stage;
            Growth01 = growth01;
            CropId = cropId;
            Year = year;
            DayOfYear = dayOfYear;
            Schedule = schedule;
        }

        public bool IsReady => Stage == FarmStage.Ready;
        public bool IsHarvested => Stage == FarmStage.Stubble;
    }

    /// <summary>
    /// <b>The farm, as arithmetic</b> (farm slice 1). A plot's stage on any day is a pure function of
    /// <c>(plot def, crop def, world seed, day)</c>: nothing is ticked and nothing is saved (rule 5). The
    /// only variation is seeded: which day inside the sowing window a plot is sown, how long a ready crop
    /// stands, and a per-cell growth wobble for the eye. Same inputs, same answer, in any query order.
    ///
    /// <para>Days are whole: a stage changes only at midnight, so a consumer that re-reads on
    /// <see cref="DayStarted"/> never misses one and never reads per frame (rule 7).</para>
    /// </summary>
    public static class FarmMath
    {
        // Independent seeded streams, so moving one draw never moves another.
        private const int PlantStream = 1;
        private const int HarvestStream = 2;
        private const int CellStream = 1000;

        /// <summary>A calendar day for the farm: the 1-based year and the 0-based day of that year.</summary>
        public readonly struct CalendarDay
        {
            public readonly int Year;
            public readonly int DayOfYear;
            public CalendarDay(int year, int dayOfYear) { Year = year; DayOfYear = dayOfYear; }
        }

        /// <summary>The calendar day of an elapsed day (<see cref="IGameClock.DayIndex"/>), through the one
        /// conversion the clock itself uses (<see cref="GameCalendar.AtDay"/>).</summary>
        public static CalendarDay DayAt(long elapsedDay, int daysPerSeason, int startDayOfYear)
        {
            int length = Math.Max(1, daysPerSeason);
            CalendarDate date = GameCalendar.AtDay(elapsedDay, length, startDayOfYear);
            return new CalendarDay(date.Year, (int)date.Season * length + date.DayOfSeason - 1);
        }

        /// <summary>The 0-based day of the year of an authored <see cref="SeasonDay"/>; a day past the season's
        /// end is held to its last day.</summary>
        public static int DayOfYear(SeasonDay day, int daysPerSeason)
        {
            int length = Math.Max(1, daysPerSeason);
            int season = Math.Max(0, Math.Min(3, (int)day.Season));
            return season * length + Math.Max(1, Math.Min(length, day.Day)) - 1;
        }

        /// <summary>The days in one year (four seasons).</summary>
        public static int DaysPerYear(int daysPerSeason) => 4 * Math.Max(1, daysPerSeason);

        /// <summary>The crop id the plot's rotation sows in <paramref name="year"/> (1-based), or null in a
        /// rest year or with no rotation.</summary>
        public static string CropIdForYear(FarmPlotDef plot, int year)
        {
            string[] rotation = plot != null ? plot.CropRotation : null;
            if (rotation == null || rotation.Length == 0) return null;
            int i = (int)(((long)year - 1) % rotation.Length);
            if (i < 0) i += rotation.Length;
            string id = rotation[i];
            return string.IsNullOrEmpty(id) ? null : id;
        }

        /// <summary>
        /// The seeded work dates for one plot in one year. Sown on a seeded day inside the crop's window,
        /// tilled <see cref="CropDef.TillLeadDays"/> before, ready once mature and inside the harvest window,
        /// lifted one to <see cref="CropDef.HarvestSpreadDays"/>+1 days after ready but never past the window.
        /// A null crop is a rest year.
        /// </summary>
        public static FarmPlotSchedule ScheduleFor(string plotId, CropDef crop, int worldSeed, int year,
                                                   int daysPerSeason)
        {
            if (crop == null) return FarmPlotSchedule.Rest;
            int plantFrom = DayOfYear(crop.PlantFrom, daysPerSeason);
            int plantTo = Math.Max(plantFrom, DayOfYear(crop.PlantTo, daysPerSeason));
            int harvestFrom = DayOfYear(crop.HarvestFrom, daysPerSeason);
            int harvestTo = Math.Max(harvestFrom, DayOfYear(crop.HarvestTo, daysPerSeason));

            int salt = StableHash(plotId);
            int plantDay = plantFrom + Pick(worldSeed, salt, year, PlantStream, plantTo - plantFrom + 1);
            int tillDay = Math.Max(0, plantDay - Math.Max(1, crop.TillLeadDays));
            int matureDay = plantDay + Math.Max(1, crop.DaysToMature);
            int readyDay = Math.Max(matureDay, harvestFrom);
            int spread = Math.Max(0, crop.HarvestSpreadDays);
            int harvestDay = readyDay + 1 + Pick(worldSeed, salt, year, HarvestStream, spread + 1);
            // The window's end is a hard stop, but a crop always stands ready for one day first.
            harvestDay = Math.Max(readyDay + 1, Math.Min(harvestDay, harvestTo));
            return new FarmPlotSchedule(tillDay, plantDay, matureDay, readyDay, harvestDay);
        }

        /// <summary>The plot's state on <paramref name="dayOfYear"/> under <paramref name="schedule"/>. The
        /// one place stage is decided, for a tender's schedule and a player's alike.</summary>
        public static FarmPlotState StateOn(in FarmPlotSchedule schedule, CropDef crop, int year, int dayOfYear)
        {
            if (!schedule.HasCrop || crop == null)
                return new FarmPlotState(FarmStage.Fallow, 0f, null, year, dayOfYear, FarmPlotSchedule.Rest);

            FarmStage stage;
            float growth;
            if (dayOfYear < schedule.TillDay) { stage = FarmStage.Fallow; growth = 0f; }
            else if (dayOfYear < schedule.PlantDay) { stage = FarmStage.Tilled; growth = 0f; }
            else if (dayOfYear >= schedule.HarvestDay) { stage = FarmStage.Stubble; growth = 1f; }
            else
            {
                growth = Growth(schedule, crop, dayOfYear);
                if (dayOfYear >= schedule.ReadyDay) stage = FarmStage.Ready;
                else if (growth < crop.SproutAt) stage = FarmStage.Planted;
                else if (growth < crop.GrowingAt) stage = FarmStage.Sprouting;
                else stage = FarmStage.Growing;
            }
            return new FarmPlotState(stage, growth, crop.Id, year, dayOfYear, schedule);
        }

        /// <summary>The plot's state on an elapsed day, from its defs and the world seed. <paramref name="crop"/>
        /// must be the crop the rotation sows that year (<see cref="CropIdForYear"/>), or null for a rest year.</summary>
        public static FarmPlotState StateAt(FarmPlotDef plot, CropDef crop, int worldSeed, long elapsedDay,
                                            int daysPerSeason, int startDayOfYear)
        {
            CalendarDay day = DayAt(elapsedDay, daysPerSeason, startDayOfYear);
            FarmPlotSchedule schedule = ScheduleFor(plot != null ? plot.Id : null, crop, worldSeed, day.Year,
                                                    daysPerSeason);
            return StateOn(schedule, crop, day.Year, day.DayOfYear);
        }

        /// <summary>
        /// The work due on <paramref name="dayOfYear"/>, appended to <paramref name="into"/> in chore order;
        /// returns how many. Till on the till day, plant on the sowing day, tend every
        /// <see cref="CropDef.TendEveryDays"/> after sowing while not yet ready, harvest on the harvest day.
        /// "What should this farmer be doing today" for a routine, and later the list a player can help with.
        /// </summary>
        public static int ChoresDue(in FarmPlotSchedule schedule, CropDef crop, int dayOfYear, List<FarmChore> into)
        {
            if (into == null || !schedule.HasCrop || crop == null) return 0;
            int before = into.Count;
            if (dayOfYear == schedule.TillDay) into.Add(FarmChore.Till);
            if (dayOfYear == schedule.PlantDay) into.Add(FarmChore.Plant);
            int every = Math.Max(1, crop.TendEveryDays);
            if (dayOfYear > schedule.PlantDay && dayOfYear < schedule.ReadyDay
                && (dayOfYear - schedule.PlantDay) % every == 0)
                into.Add(FarmChore.Tend);
            if (dayOfYear == schedule.HarvestDay) into.Add(FarmChore.Harvest);
            return into.Count - before;
        }

        /// <summary>The produce one harvest of <paramref name="crop"/> on <paramref name="plot"/> yields.</summary>
        public static int YieldOf(FarmPlotDef plot, CropDef crop)
            => plot == null || crop == null ? 0 : plot.CellCount * Math.Max(0, crop.YieldPerCell);

        /// <summary>Visual only: one cell's growth, wobbled by up to <see cref="CropDef.CellVariation"/> around
        /// the plot's, seeded per plot, year and cell. The plot's <see cref="FarmPlotState.Stage"/> never moves.</summary>
        public static float CellGrowth01(in FarmPlotState state, CropDef crop, string plotId, int worldSeed, int cell)
        {
            if (crop == null || state.Stage == FarmStage.Fallow || state.Stage == FarmStage.Tilled) return 0f;
            if (state.Stage == FarmStage.Stubble) return 1f;
            float wobble = (Hash01(worldSeed, StableHash(plotId), state.Year, CellStream + cell) * 2f - 1f)
                           * crop.CellVariation;
            return Clamp01(state.Growth01 + wobble);
        }

        private static float Growth(in FarmPlotSchedule schedule, CropDef crop, int dayOfYear)
            => Clamp01((dayOfYear - schedule.PlantDay) / (float)Math.Max(1, crop.DaysToMature));

        private static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;

        /// <summary>A seeded whole number in 0..count-1.</summary>
        private static int Pick(int seed, int salt, int year, int stream, int count)
        {
            if (count <= 1) return 0;
            int i = (int)(Hash01(seed, salt, year, stream) * count);
            return i >= count ? count - 1 : i;
        }

        /// <summary>A stable string hash: the same 31-polynomial the editor's woodland lots use, so an id
        /// hashes the same on every runtime (<c>string.GetHashCode</c> does not promise that).</summary>
        internal static int StableHash(string s)
        {
            unchecked
            {
                int h = 17;
                if (s != null) foreach (char c in s) h = h * 31 + c;
                return h & 0x7fffffff;
            }
        }

        /// <summary>0..1 from four integers, the avalanche mix the swell's seeded draws use.</summary>
        internal static float Hash01(int seed, int salt, int year, int stream)
        {
            unchecked
            {
                uint n = (uint)seed ^ ((uint)salt * 0x9e3779b9u) ^ ((uint)year * 0x85ebca6bu)
                         ^ ((uint)stream * 0xc2b2ae35u);
                n ^= n >> 16; n *= 0x7feb352du; n ^= n >> 15; n *= 0x846ca68bu; n ^= n >> 16;
                return (n >> 8) / 16777216f;
            }
        }
    }
}
