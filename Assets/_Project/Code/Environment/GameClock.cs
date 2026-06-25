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
        [Tooltip("Hour of day a new game starts at (0..24).")]
        [Range(0f, 24f)] [SerializeField] private float _startHour = 6f;

        private double _t;
        private int _lastTotalDays = -1;
        private Season _lastSeason;

        public double TotalSeconds => _t;
        public GameTime Now => new GameTime(_t);
        public bool IsPaused { get; set; }
        public float TimeScale { get; set; } = 1f;

        private int TotalDays => _config != null ? (int)(_t / _config.SecondsPerDay) : 0;

        /// <summary>0-based absolute day since a new game began (the save's dayIndex). See
        /// <see cref="IGameClock.DayIndex"/>.</summary>
        public int DayIndex => TotalDays;

        public Season Season => _config != null ? (Season)(TotalDays / _config.DaysPerSeason % 4) : Season.EarlySpring;
        public int Year => _config != null ? TotalDays / _config.DaysPerSeason / 4 + 1 : 1;
        public int DayOfSeason => _config != null ? TotalDays % _config.DaysPerSeason + 1 : 1;
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
            _t = _config.SecondsPerDay * (_startHour / 24f);
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
            // Keep the rollover guards in step with where we landed (only meaningful once configured).
            if (_config != null)
            {
                _lastTotalDays = TotalDays;
                _lastSeason = Season;
            }
        }

        private void Update()
        {
            if (_config == null || IsPaused) return;
            _t += Time.deltaTime * Mathf.Max(0f, TimeScale);

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
