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

        private int TotalDays => _config != null ? (int)(_t / _config.SecondsPerDay) : 0;

        /// <summary>0-based absolute day since a new game began (the save's dayIndex). See
        /// <see cref="IGameClock.DayIndex"/>.</summary>
        public int DayIndex => TotalDays;

        private CalendarDate Calendar => GameCalendar.AtSeconds(_t, _config);
        public Season Season => Calendar.Season;
        public int Year => Calendar.Year;
        public int DayOfSeason => Calendar.DayOfSeason;
        public Weekday Weekday => _config != null ? (Weekday)(TotalDays % _config.DaysPerWeek) : Weekday.Monday;
        public bool IsMarketDay => _config != null && (int)Weekday == _config.MarketDayIndex;

        public float DayFraction => _config != null ? (float)(_t % _config.SecondsPerDay / _config.SecondsPerDay) : 0f;
        public float HourOfDay => DayFraction * 24f;

        private void Awake()
        {
            if (_config == null)
            {
                Debug.LogError("[GameClock] No GameConfig assigned. Clock will not run.", this);
                enabled = false;
                return;
            }
            _t = StartSeconds;
            _flow = _t;
            _lastTotalDays = TotalDays;
            _lastSeason = Season;
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
            _t = totalSeconds < 0d ? 0d : totalSeconds;
            _flow = _t;
            // Keep the rollover guards in step with where we landed (only meaningful once configured).
            if (_config != null)
            {
                _lastTotalDays = TotalDays;
                _lastSeason = Season;
            }
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
            if (_config == null || IsHeld) return false;
            double dayLength = _config.SecondsPerDay;
            if (!(dayLength > 0d) || !(totalSeconds > _t)) return false;

            double span = totalSeconds - _t;
            int lastDay = (int)(totalSeconds / dayLength);
            for (int day = _lastTotalDays + 1; day <= lastDay; day++)
            {
                _t = day * dayLength;
                _lastTotalDays = day;
                CalendarDate date = GameCalendar.AtDay(day, _config.DaysPerSeason, _config.StartDayOfYear);
                EventBus.Publish(new DayStarted(date.DayOfSeason, date.Season, date.Year));
                if (date.Season != _lastSeason)
                {
                    _lastSeason = date.Season;
                    EventBus.Publish(new SeasonChanged(date.Season, date.Year));
                }
            }

            _t = totalSeconds;
            _flow += span;
            return true;
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
            if (_config == null || IsPaused) return;
            double step = wallSeconds * Mathf.Max(0f, TimeScale);
            _flow += step;
            if (!IsHeld) _t += step;

            int today = TotalDays;
            if (today != _lastTotalDays)
            {
                _lastTotalDays = today;
                EventBus.Publish(new DayStarted(DayOfSeason, Season, Year));
                if (Season != _lastSeason)
                {
                    _lastSeason = Season;
                    EventBus.Publish(new SeasonChanged(Season, Year));
                }
            }
        }
    }
}
