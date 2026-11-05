using UnityEngine;

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
    /// new game starts High Summer · Day 1 · 07:30. Season/Weekday
    /// enum orders match the rig's <c>SEASON_ABBR</c>/<c>WEEKDAYS</c> tables.</para>
    /// </summary>
    public readonly struct WatchFaceState
    {
        /// <summary>Default hour the day-panel lights (before this the watch shows its night backlight).</summary>
        public const float DefaultDayStartHour = DaylightHours.DefaultSunriseHour;
        /// <summary>Default hour night falls (matches the rig's <c>isNight</c> and <c>CottageDayNight</c>).</summary>
        public const float DefaultNightStartHour = DaylightHours.DefaultSunsetHour;

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

        /// <summary>The watch backlight follows the sky's dawn and sunset, including authored changes.</summary>
        public static bool NightAt(float hourOfDay, float? dayStartHour = null, float? nightStartHour = null)
        {
            DaylightHours hours = DaylightHours.Shared;
            float dawn = dayStartHour ?? (hours != null ? hours.SunriseHour : DefaultDayStartHour);
            float dusk = nightStartHour ?? (hours != null ? hours.SunsetHour : DefaultNightStartHour);
            return hourOfDay < dawn || hourOfDay >= dusk;
        }

        /// <summary>
        /// Map the live clock to a watch face. Hour/minute are TRUNCATED (a watch shows elapsed time, not
        /// rounded — <c>HudFormat.ClockHHMM</c> rounds and would tick a minute early against the shown
        /// seconds); the minute-of-day truncation matches the HUD's own change-detect
        /// (<c>(int)(HourOfDay·60)</c>), so watch and HUD never disagree by a minute. Night thresholds are
        /// named params (rule 6 — no buried magic numbers) so the owner can move dusk later.
        /// </summary>
        public static WatchFaceState FromClock(IGameClock clock,
                                               float? dayStartHour = null,
                                               float? nightStartHour = null)
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

    /// <summary>Core's read-only lighting contract. The shipping art profile supplies its serialized hours.</summary>
    public abstract class DaylightHours : ScriptableObject
    {
        public const float DefaultSunriseHour = 5.5f;
        public const float DefaultSunsetHour = 21.5f;
        public const float DefaultNightHour = 22f;
        public const float DefaultDawnBlendHours = 2f;

        [Header("One sun all year")]
        [Tooltip("First light and the sunrise horizon, in clock hours. The same in every season.")]
        [SerializeField, Range(0f, 24f)] protected float _sunriseHour = DefaultSunriseHour;
        [Tooltip("Sunset horizon. Shadows, the watch, windows and birds follow this hour.")]
        [SerializeField, Range(0f, 24f)] protected float _sunsetHour = DefaultSunsetHour;
        [Tooltip("End of dusk: the tint and mood grade have reached full night.")]
        [SerializeField, Range(0f, 24f)] protected float _nightHour = DefaultNightHour;
        [Tooltip("Hours from first light to full morning colour and brightness.")]
        [SerializeField, Min(0.01f)] protected float _dawnBlendHours = DefaultDawnBlendHours;

        public float SunriseHour => _sunriseHour;
        public float SunsetHour => _sunsetHour;
        public float NightHour => _nightHour;
        public float DawnBlendHours => _dawnBlendHours;

        static DaylightHours _shared;
        public static DaylightHours Shared
        {
            get
            {
                if (_shared == null) _shared = Resources.Load<DaylightHours>("DayNightProfile");
                return _shared;
            }
        }

        protected void ApplyHourDefaults()
        {
            _sunriseHour = DefaultSunriseHour;
            _sunsetHour = DefaultSunsetHour;
            _nightHour = DefaultNightHour;
            _dawnBlendHours = DefaultDawnBlendHours;
        }
    }

}
