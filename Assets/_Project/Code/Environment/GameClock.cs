using System;
using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Environment
{
    /// <summary>
    /// Advances and exposes the master clock, and publishes day/season rollovers on the
    /// <see cref="EventBus"/>. Implements <see cref="IGameClock"/>; registered into
    /// <see cref="GameServices"/> by the composition root.
    /// </summary>
    public class GameClock : MonoBehaviour, IGameClock
    {
        [SerializeField] private GameConfig _config;
        // Legacy scene YAML may still contain _startHour. There is no field to deserialize it into;
        // GameConfig.StartHour is the only Inspector control and the only value read.

        private double _t;
        private double _flow;
        private int _lastTotalDays = -1;
        private Season _lastSeason;
        private bool _advancing;
        private CalendarSettings _forwardCalendar;
        private CalendarDate _boundaryDate;

        // An accepted span owns these values until all synchronous notifications finish.
        private readonly struct CalendarSettings
        {
            public readonly double DayLength;
            public readonly int DaysPerSeason, StartDayOfYear, DaysPerWeek, MarketDayIndex;

            public CalendarSettings(GameConfig config, int startDayOfYear)
            {
                DayLength = config.SecondsPerDay;
                DaysPerSeason = config.DaysPerSeason;
                StartDayOfYear = startDayOfYear;
                DaysPerWeek = config.DaysPerWeek;
                MarketDayIndex = config.MarketDayIndex;
            }

            public CalendarDate AtDay(int day) => GameCalendar.AtDay(day, DaysPerSeason, StartDayOfYear);
        }

        /// <summary>The shared config this clock runs on — read by the composition root to wire
        /// <see cref="GameServices.Config"/> (the clock always has it; no scene re-wiring needed).</summary>
        public GameConfig Config => _config;

        public double TotalSeconds => _t;
        public double FlowSeconds => _flow;
        public bool IsHeld { get; set; }
        /// <summary>The authored new-game instant, also used by the opening's hour hold.</summary>
        public double StartSeconds => _config != null ? _config.SecondsPerDay * (_config.StartHour / 24d) : 0d;
        public GameTime Now => new GameTime(_t);
        public bool IsPaused { get; set; }
        public float TimeScale { get; set; } = 1f;

        private int TotalDays => _advancing ? _lastTotalDays
            : _config != null && TryDay(_t, _config.SecondsPerDay, out int day) ? day : 0;

        /// <summary>0-based absolute day since a new game began (the save's dayIndex). See
        /// <see cref="IGameClock.DayIndex"/>.</summary>
        public int DayIndex => TotalDays;

        private CalendarDate Calendar => _advancing ? _boundaryDate
            : _config != null ? GameCalendar.AtDay(TotalDays, _config.DaysPerSeason, _config.StartDayOfYear)
            : GameCalendar.AtSeconds(_t, null);
        public Season Season => Calendar.Season;
        public int Year => Calendar.Year;
        public int DayOfSeason => Calendar.DayOfSeason;
        public Weekday Weekday => _advancing ? (Weekday)(_lastTotalDays % _forwardCalendar.DaysPerWeek)
            : _config != null && _config.DaysPerWeek > 0 ? (Weekday)(TotalDays % _config.DaysPerWeek) : Weekday.Monday;
        public bool IsMarketDay => _advancing ? (int)Weekday == _forwardCalendar.MarketDayIndex
            : _config != null && (int)Weekday == _config.MarketDayIndex;

        public float DayFraction
        {
            get
            {
                if (_advancing) return 0f; // Callbacks only run at the canonical midnight.
                if (_config == null || !TryDay(_t, _config.SecondsPerDay, out int day)) return 0f;
                double length = _config.SecondsPerDay;
                return (float)((_t - day * length) / length);
            }
        }
        public float HourOfDay => DayFraction * 24f;

        private void Awake()
        {
            if (_config == null)
            {
                Debug.LogError("[GameClock] No GameConfig assigned. Clock will not run.", this);
                enabled = false;
                return;
            }
            SeekTo(StartSeconds);
        }

        /// <summary>
        /// Seek the master clock to an absolute game-time (<see cref="IGameClock.SeekTo"/>): how a loaded
        /// save resumes at the saved instant. We re-baseline the day/season rollover trackers to the new
        /// time so the next <see cref="Update"/> does NOT spuriously fire a DayStarted/SeasonChanged for
        /// the jumped span — a restore lands you on the day you saved, it isn't a fast-forward through it.
        /// Negative input clamps to 0 (time never runs before the start of the game).
        /// </summary>
        public void SeekTo(double totalSeconds)
        {
            if (_advancing || !Finite(totalSeconds)) return;
            double target = Math.Max(0d, totalSeconds);
            if (_config != null)
            {
                if (!TryCalendar(out var calendar) || !TryDay(target, calendar.DayLength, out int day)) return;
                _lastTotalDays = day;
                _lastSeason = calendar.AtDay(day).Season;
            }
            else if (!TryDay(target, GameConfig.DefaultSecondsPerDay, out _)) return;
            _t = target;
            _flow = _t;
        }

        /// <summary>
        /// Fast-forward to a later game-time as time PASSING (<see cref="IGameClock.FastForwardTo"/>): how
        /// a night's rest brings the morning. Unlike <see cref="SeekTo"/>, every midnight crossed
        /// publishes <see cref="DayStarted"/> once, in order, and <see cref="SeasonChanged"/> where a
        /// season turns, exactly as <see cref="Advance"/> would have had the hours been waited out.
        ///
        /// <para><b>Each day is announced from its own first instant.</b> The clock stands at that
        /// day's midnight while its <see cref="DayStarted"/> is out, so a subscriber that reads the clock
        /// inside its handler sees the day it is being told about, as it does on a waited rollover. The
        /// payload is taken from the day's index, not re-derived from seconds, so it cannot be off by a
        /// rounding of the very boundary it stands on.</para>
        ///
        /// <para><b>Flow moves with the hour.</b> <see cref="FlowSeconds"/> advances by the same span:
        /// the world ran through the night too, and whatever a held span had put between the two
        /// readings stays between them.</para>
        ///
        /// <para>Refused (false, nothing changed, nothing published) with no config, while the hour is
        /// held (<see cref="IsHeld"/>: the holder owns the hour), or for a target that is not later than
        /// now. <see cref="IsPaused"/> does not refuse: a pause stops the flow of frames, and this is
        /// not a frame.</para>
        /// </summary>
        public bool FastForwardTo(double totalSeconds)
        {
            if (_advancing || IsHeld || !(totalSeconds > _t) || !TryCalendar(out var calendar)) return false;
            return ForwardSpan(totalSeconds, totalSeconds - _t, calendar);
        }

        /// <summary>
        /// The clock reads WALL time, never <c>Time.timeScale</c> (rule 5; lead-architect ruling 2026-09-09).
        /// <c>Time.timeScale</c> is a FEEL channel (the landing hit-stop dips it); the world's time is
        /// <c>(seed, gameTime)</c> and a feel dip must not slow the tide. <see cref="IsPaused"/> is the only
        /// pause and <see cref="TimeScale"/> the only rate (<c>ShellPause</c>: "there is no second clock").
        /// </summary>
        private void Update() => Advance(Time.unscaledDeltaTime);

        /// <summary>
        /// Integrate <paramref name="wallSeconds"/> of real time at <see cref="TimeScale"/> and fire the
        /// day/season rollovers. Public so a test can drive the clock without a frame; a game passes
        /// <c>Time.unscaledDeltaTime</c> and nothing else.
        /// </summary>
        public void Advance(float wallSeconds)
        {
            if (_advancing || IsPaused || !Finite(wallSeconds) || wallSeconds < 0f
                || !Finite(TimeScale) || !TryCalendar(out var calendar)) return;
            // Preserve the existing float multiplication BEFORE widening to double.
            double step = wallSeconds * Mathf.Max(0f, TimeScale);
            if (IsHeld)
            {
                if (Finite(step) && Finite(_flow) && Finite(_flow + step)
                    && TryDay(_t, calendar.DayLength, out _)) _flow += step;
                return;
            }
            ForwardSpan(_t + step, step, calendar);
        }

        /// <summary>Both entry points converge here after their distinct pause/hold/rate gates.
        /// Validate the entire request before changing a reading or publishing a boundary.</summary>
        private bool ForwardSpan(double target, double span, CalendarSettings calendar)
        {
            if (!Finite(span) || span < 0d || !Finite(_flow) || !Finite(_flow + span)
                || !TryDay(_t, calendar.DayLength, out int firstDay)
                || !TryDay(target, calendar.DayLength, out int lastDay) || target < _t) return false;

            double start = _t, flow = _flow, finalFlow = flow + span;
            _lastTotalDays = firstDay;
            _lastSeason = calendar.AtDay(firstDay).Season;
            _forwardCalendar = calendar;
            _advancing = true;
            try
            {
                // Widen before incrementing, including the increment after int.MaxValue.
                for (long day = (long)firstDay + 1; day <= lastDay; day++)
                {
                    _t = day * calendar.DayLength;
                    _flow = flow + (_t - start);
                    _lastTotalDays = (int)day;
                    _boundaryDate = calendar.AtDay((int)day);
                    bool seasonChanged = _boundaryDate.Season != _lastSeason;
                    _lastSeason = _boundaryDate.Season;
                    EventBus.Publish(new DayStarted(_boundaryDate.DayOfSeason, _boundaryDate.Season, _boundaryDate.Year));
                    if (seasonChanged)
                        EventBus.Publish(new SeasonChanged(_boundaryDate.Season, _boundaryDate.Year));
                }
                _t = target;
                _flow = finalFlow;
                return true;
            }
            finally { _advancing = false; }
        }

        private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);

        private bool TryCalendar(out CalendarSettings calendar)
        {
            calendar = default;
            if (_config == null || !Finite(_config.SecondsPerDay) || _config.SecondsPerDay <= 0f
                || _config.DaysPerSeason <= 0 || _config.DaysPerWeek <= 0) return false;

            // Match StartDayOfYear's existing clamping, but reject an overflowing int offset.
            long offset = (long)(int)_config.StartSeason * _config.DaysPerSeason
                + Mathf.Clamp(_config.StartDayOfSeason, 1, _config.DaysPerSeason) - 1L;
            if (offset < int.MinValue || offset > int.MaxValue) return false;
            calendar = new CalendarSettings(_config, (int)offset);
            return true;
        }

        /// <summary>Classify against canonical products B(d)=d*length, not a rounded quotient alone.
        /// The same classification drives traversal endpoints and every calendar getter.</summary>
        private static bool TryDay(double seconds, double length, out int result)
        {
            result = 0;
            if (!Finite(seconds) || seconds < 0d || !Finite(length) || length <= 0d
                || seconds >= ((long)int.MaxValue + 1) * length) return false;
            long day = (long)(seconds / length);
            while (seconds < day * length) day--;
            while (seconds >= (day + 1) * length) day++;
            result = (int)day;
            return true;
        }
    }
}
