using System;
using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// How much snow sits in the trees on a given day: 0 (bare) .. 1 (every branch that can hold snow
    /// holds it). The TREE snow of the pass-4 rig, brought forward from M2 on its own; ground, roof and
    /// shrub snow stay M2 and may read the same number later.
    ///
    /// <para><b>A pure function of the calendar.</b> <c>(Season, DayOfSeason)</c> plus the owner's
    /// tunables in, one float out — no clock read, no RNG, no state. The world is
    /// <c>(worldSeed, gameTime)</c> (CLAUDE.md rule 5), so the snow is RECOMPUTED from the day the clock
    /// is on and never saved: a load lands on the day it was saved and the trees wear that day's snow.</para>
    ///
    /// <para><b>The shape</b> is one year on one line. Bare through High Summer; the first snow settles
    /// on <see cref="FoliageSnowSettings.FirstSnowDayOfTheTurn"/> and the cover climbs a step a day for
    /// <see cref="FoliageSnowSettings.SnowfallDays"/> days; full through Hard Winter; the melt starts on
    /// <see cref="FoliageSnowSettings.MeltStartDayOfEarlySpring"/> and takes
    /// <see cref="FoliageSnowSettings.MeltDays"/> days to bare the branches. A step a day, because the
    /// consumer is published once a day (<c>FoliageSnowBridge</c>) — the curve never needs the hour.</para>
    ///
    /// <para><b>Continuous for any tuning.</b> The melt starts from whatever cover the winter actually
    /// reached (a late first snow can leave Hard Winter short of full), and it can never run past the
    /// next first snow, so the value never jumps at a season boundary or at the turn of the year.</para>
    /// </summary>
    public static class FoliageSnowMath
    {
        private const int SeasonsPerYear = 4;

        /// <summary>
        /// The tree snow cover for a calendar day, 0..1.
        /// </summary>
        /// <param name="season">The clock's season.</param>
        /// <param name="dayOfSeason">The clock's 1-based day of the season. Clamped into 1..<paramref name="daysPerSeason"/>.</param>
        /// <param name="daysPerSeason">The calendar's season length (<c>GameConfig.DaysPerSeason</c>, 28 in canon).</param>
        /// <param name="settings">The owner's curve.</param>
        public static float Coverage(Season season, int dayOfSeason, int daysPerSeason,
                                     in FoliageSnowSettings settings)
        {
            int dps = Math.Max(1, daysPerSeason);
            int s = Mathf.Clamp((int)season, 0, SeasonsPerYear - 1);
            int d = Mathf.Clamp(dayOfSeason, 1, dps);
            int t = s * dps + (d - 1);                       // 0-based day of the year

            int snowfall = Math.Max(1, settings.SnowfallDays);
            int firstSnow = (int)Season.TheTurn * dps + (Mathf.Clamp(settings.FirstSnowDayOfTheTurn, 1, dps) - 1);

            if (t >= firstSnow)
                return Rise(t, firstSnow, snowfall);

            // Before the first snow of the year is the tail of LAST winter: start the melt from the cover
            // that winter reached on its final day, not from an assumed 1.
            float peak = Rise(SeasonsPerYear * dps - 1, firstSnow, snowfall);
            int meltStart = (int)Season.EarlySpring * dps + (Mathf.Clamp(settings.MeltStartDayOfEarlySpring, 1, dps) - 1);
            if (t < meltStart) return peak;

            // The melt is done by the day before the first snow at the latest, so the curve cannot jump
            // back up from a half-melted value when the autumn begins.
            int melt = Math.Min(Math.Max(1, settings.MeltDays), firstSnow - meltStart);
            float left = 1f - (t - meltStart + 1) / (float)melt;
            return peak * Mathf.Clamp01(left);
        }

        /// <summary>The same curve read off a clock, for the publishers. A null clock is bare (0).</summary>
        public static float Coverage(IGameClock clock, int daysPerSeason, in FoliageSnowSettings settings)
            => clock == null ? 0f : Coverage(clock.Season, clock.DayOfSeason, daysPerSeason, settings);

        private static float Rise(int t, int firstSnow, int snowfall)
            => Mathf.Clamp01((t - firstSnow + 1) / (float)snowfall);
    }

    /// <summary>
    /// The owner's tree-snow calendar (<see cref="FoliageSnowMath"/>). Days, not fractions, because the
    /// owner reads the calendar in days — "the first snow is the 19th of The Turn".
    /// </summary>
    [Serializable]
    public struct FoliageSnowSettings
    {
        [Tooltip("The day of The Turn on which the first snow settles in the trees. The cover climbs a " +
                 "step a day from here. 19 puts the first snow in the last third of the autumn.")]
        [Min(1)] public int FirstSnowDayOfTheTurn;

        [Tooltip("How many days the cover takes to build, counting the first-snow day. 11 from the 19th " +
                 "of The Turn reaches full cover on the first day of Hard Winter. Conifers load up at 60% " +
                 "cover (a ruled jump, not a fade), which this default lands on the 25th of The Turn.")]
        [Min(1)] public int SnowfallDays;

        [Tooltip("The day of Early Spring on which the melt begins. Every day before it keeps the winter's " +
                 "full cover.")]
        [Min(1)] public int MeltStartDayOfEarlySpring;

        [Tooltip("How many days the melt takes to bare the branches, counting its first day. 28 from the " +
                 "1st of Early Spring leaves the trees bare on the last day of spring.")]
        [Min(1)] public int MeltDays;

        /// <summary>
        /// The proposed default (the owner rules the shape): bare through High Summer, first snow on the
        /// 19th of The Turn, full cover from the 1st of Hard Winter and through it, then a slow melt that
        /// bares the trees by the end of Early Spring.
        /// </summary>
        public static FoliageSnowSettings Default => new FoliageSnowSettings
        {
            FirstSnowDayOfTheTurn = 19,
            SnowfallDays = 11,
            MeltStartDayOfEarlySpring = 1,
            MeltDays = 28,
        };
    }
}
