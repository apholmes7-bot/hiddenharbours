namespace HiddenHarbours.Core
{
    /// <summary>
    /// The clock-derived state of the diegetic watch (the art director's <c>WatchRig</c>) — the instrument
    /// that, once owned, grants the time/date readout (the keystone diegetic-UI proof,
    /// <c>docs/design/diegetic-ui-and-inventory.md</c> §3.3: "hide the clock, sell a watch"). A pure
    /// mapper from <see cref="IGameClock"/>, unit-testable with a fake clock (no Unity runtime needed).
    ///
    /// <para>Every field here maps 1:1 onto a member <see cref="IGameClock"/> already exposes — there is no
    /// calendar math to reimplement. In particular do NOT re-derive the weekday / market day from an
    /// absolute-day index (the watch README's <c>di = (year-1)·112 + …</c>): <c>clock.Weekday</c> and
    /// <c>clock.IsMarketDay</c> already are that. The rig's <c>use24</c> and <c>light</c> params are display
    /// / momentary-input, not clock-derived, so the presenter supplies them — they are deliberately not
    /// here.</para>
    ///
    /// <para><b>Time canon</b> (verified against <c>GameConfig</c>/<c>GameClock</c>): 4 seasons × 28 days,
    /// Mon-first week, day = <c>GameConfig.SecondsPerDay</c> real seconds (ships at 1800 = 30 real min),
    /// new game starts Early Spring · Day 1 · 06:00. Season/Weekday
    /// enum orders match the rig's <c>SEASON_ABBR</c>/<c>WEEKDAYS</c> tables.</para>
    /// </summary>
    public readonly struct WatchFaceState
    {
        /// <summary>Default hour the day-panel lights (before this the watch shows its night backlight).</summary>
        public const float DefaultDayStartHour = 6f;
        /// <summary>Default hour night falls (matches the rig's <c>isNight</c> and <c>CottageDayNight</c>).</summary>
        public const float DefaultNightStartHour = 19f;

        public readonly int Hour;          // 0..23
        public readonly int Minute;        // 0..59
        public readonly int Second;        // 0..59 (cosmetic — the day runs fast)
        public readonly Weekday Weekday;   // Monday = 0 → rig WEEKDAYS[dow]
        public readonly int DayOfSeason;   // 1..28 → rig 'date'
        public readonly Season Season;     // EarlySpring = 0 → rig SEASON_ABBR[idx]
        public readonly int Year;          // 1-based → rig 'Y{n}'
        public readonly bool IsMarketDay;  // → rig 'market'
        public readonly bool IsNight;      // → rig 'night' (green backlight)

        public WatchFaceState(int hour, int minute, int second, Weekday weekday, int dayOfSeason,
                              Season season, int year, bool isMarketDay, bool isNight)
        {
            Hour = hour; Minute = minute; Second = second; Weekday = weekday; DayOfSeason = dayOfSeason;
            Season = season; Year = year; IsMarketDay = isMarketDay; IsNight = isNight;
        }

        /// <summary>Night per the watch rule: before day-start OR at/after night-start (default 06:00 / 19:00).</summary>
        public static bool NightAt(float hourOfDay,
                                   float dayStartHour = DefaultDayStartHour,
                                   float nightStartHour = DefaultNightStartHour)
            => hourOfDay < dayStartHour || hourOfDay >= nightStartHour;

        /// <summary>
        /// Map the live clock to a watch face. Hour/minute are TRUNCATED (a watch shows elapsed time, not
        /// rounded — <c>HudFormat.ClockHHMM</c> rounds and would tick a minute early against the shown
        /// seconds); the minute-of-day truncation matches the HUD's own change-detect
        /// (<c>(int)(HourOfDay·60)</c>), so watch and HUD never disagree by a minute. Night thresholds are
        /// named params (rule 6 — no buried magic numbers) so the owner can move dusk later.
        /// </summary>
        public static WatchFaceState FromClock(IGameClock clock,
                                               float dayStartHour = DefaultDayStartHour,
                                               float nightStartHour = DefaultNightStartHour)
        {
            float hod = clock.HourOfDay;                 // 0..24
            int totalMinutes = (int)(hod * 60f);         // 0..1439 (same truncation the HUD uses)
            int hour   = (totalMinutes / 60) % 24;
            int minute = totalMinutes % 60;
            int second = (int)(hod * 3600f) - totalMinutes * 60;
            if (second < 0) second = 0; else if (second > 59) second = 59;

            return new WatchFaceState(
                hour, minute, second,
                clock.Weekday, clock.DayOfSeason, clock.Season, clock.Year, clock.IsMarketDay,
                NightAt(hod, dayStartHour, nightStartHour));
        }
    }
}
