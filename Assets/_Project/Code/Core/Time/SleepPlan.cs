namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>How long a sleep is, and where the clock lands</b> (owner ruling 2026-11-15: "Player chooses
    /// sleep duration. Time moves fast with player/s able to cancel with a button. Maximum 8 hours sleep
    /// before auto waking."). Pure arithmetic over the config's numbers, so every rule here is a test and
    /// not a thing read off a screen.
    ///
    /// <para><b>The default is still the morning.</b> A sleep begun with no choice made (the chooser's
    /// first offer, and the instant path a test rig or a scene with no chooser takes) lasts until the next
    /// wake hour (<see cref="RestMorning"/>), held inside the chosen bounds: turned in at 22:00 you sleep
    /// eight hours to 06:00 exactly as before; turned in at noon you wake at 20:00, because no sleep is
    /// ever longer than eight hours.</para>
    ///
    /// <para><b>Deterministic.</b> Where a sleep lands is a function of when it began and how long it
    /// was, nothing else. The fast flow (<see cref="NextSeconds"/>, a step per frame) and the instant
    /// path (one jump) end on the same instant, and the world at an instant is a function of the instant
    /// (rule 5), so the two leave the same world behind.</para>
    /// </summary>
    public static class SleepPlan
    {
        /// <summary>The owner's ceiling, in in-game hours: no config value can make a sleep longer. A
        /// ruling, not a tunable; the tunable <see cref="GameConfig.SleepMaxHours"/> lives under it.</summary>
        public const float RulingMaxHours = 8f;

        /// <summary>The smallest step or minimum the arithmetic accepts, in hours: a guard against a zero
        /// or negative config value turning a press into nothing or a loop into forever.</summary>
        public const float SmallestHours = 1f / 60f;

        public static float MinHours(GameConfig config) =>
            Bounds(config != null ? config.SleepMinHours : GameConfig.DefaultSleepMinHours,
                   config != null ? config.SleepMaxHours : GameConfig.DefaultSleepMaxHours, out _);

        public static float MaxHours(GameConfig config)
        {
            Bounds(config != null ? config.SleepMinHours : GameConfig.DefaultSleepMinHours,
                   config != null ? config.SleepMaxHours : GameConfig.DefaultSleepMaxHours, out float max);
            return max;
        }

        public static float StepHours(GameConfig config)
        {
            float step = config != null ? config.SleepStepHours : GameConfig.DefaultSleepStepHours;
            return step >= SmallestHours ? step : SmallestHours;   // NaN and non-positive land here too
        }

        public static float GameHoursPerRealSecond(GameConfig config)
        {
            float rate = config != null ? config.SleepGameHoursPerRealSecond
                                        : GameConfig.DefaultSleepGameHoursPerRealSecond;
            return rate > 0f ? rate : GameConfig.DefaultSleepGameHoursPerRealSecond;
        }

        /// <summary>
        /// The config's (min, max) made safe: max at most <see cref="RulingMaxHours"/>, min at least
        /// <see cref="SmallestHours"/> and never above max. Returns min.
        /// </summary>
        public static float Bounds(float minHours, float maxHours, out float max)
        {
            max = maxHours > RulingMaxHours || float.IsNaN(maxHours) ? RulingMaxHours : maxHours;
            if (max < SmallestHours) max = SmallestHours;
            float min = minHours >= SmallestHours ? minHours : SmallestHours;
            if (min > max) min = max;
            return min;
        }

        /// <summary><paramref name="hours"/> held inside [min, max] (made safe by <see cref="Bounds"/>).
        /// NaN reads as the minimum.</summary>
        public static float ClampHours(float hours, float minHours, float maxHours)
        {
            float min = Bounds(minHours, maxHours, out float max);
            if (float.IsNaN(hours) || hours < min) return min;
            return hours > max ? max : hours;
        }

        /// <summary><see cref="ClampHours(float, float, float)"/> under <paramref name="config"/>.</summary>
        public static float ClampHours(float hours, GameConfig config) =>
            ClampHours(hours, MinHours(config), MaxHours(config));

        /// <summary>
        /// One press of the chooser: <paramref name="direction"/> (its sign only) steps of
        /// <paramref name="stepHours"/>, held inside the bounds.
        /// </summary>
        public static float Step(float hours, int direction, float stepHours, float minHours, float maxHours)
        {
            if (direction == 0) return ClampHours(hours, minHours, maxHours);
            float step = stepHours >= SmallestHours ? stepHours : SmallestHours;
            return ClampHours(hours + (direction > 0 ? step : -step), minHours, maxHours);
        }

        /// <summary>The length of one in-game hour, in clock seconds.</summary>
        public static double SecondsPerGameHour(double secondsPerDay) => secondsPerDay / 24d;

        /// <summary>
        /// The sleep offered before any choice: the hours from <paramref name="nowSeconds"/> to the next
        /// wake hour, held inside the bounds. Not rounded to the step: the first offer is "until morning",
        /// and a press moves from there.
        /// </summary>
        public static float DefaultHours(double nowSeconds, double secondsPerDay, float wakeHour,
                                         float minHours, float maxHours)
        {
            double hour = SecondsPerGameHour(secondsPerDay);
            if (!(hour > 0d)) return ClampHours(maxHours, minHours, maxHours);
            double wake = RestMorning.WakeSeconds(nowSeconds, secondsPerDay, wakeHour);
            return ClampHours((float)((wake - nowSeconds) / hour), minHours, maxHours);
        }

        /// <summary><see cref="DefaultHours(double, double, float, float, float)"/> under
        /// <paramref name="config"/> (or the shipped defaults with none).</summary>
        public static float DefaultHours(double nowSeconds, GameConfig config) =>
            DefaultHours(nowSeconds, DayLength(config), WakeHourOf(config), MinHours(config), MaxHours(config));

        /// <summary>
        /// Where a sleep of <paramref name="hours"/> begun at <paramref name="startSeconds"/> wakes. A sleep
        /// that reaches the wake hour lands ON the wake instant, bit for bit, so a night slept by the
        /// chooser and a night slept by the old one-press rest wake on the same instant (and the next rest
        /// measures itself against the same number, <see cref="RestMorning.WakeSeconds(double, double, float)"/>).
        /// A day length that is not positive answers the start unchanged.
        /// </summary>
        public static double WakeSeconds(double startSeconds, float hours, double secondsPerDay, float wakeHour)
        {
            double hour = SecondsPerGameHour(secondsPerDay);
            if (!(hour > 0d) || !(hours > 0f)) return startSeconds;
            double target = startSeconds + hours * hour;
            double morning = RestMorning.WakeSeconds(startSeconds, secondsPerDay, wakeHour);
            // A millionth of a game hour: far under a frame, far over a float's rounding of the hours.
            double slack = hour * 1e-6;
            return System.Math.Abs(target - morning) <= slack ? morning : target;
        }

        /// <summary><see cref="WakeSeconds(double, float, double, float)"/> under <paramref name="config"/>.</summary>
        public static double WakeSeconds(double startSeconds, float hours, GameConfig config) =>
            WakeSeconds(startSeconds, hours, DayLength(config), WakeHourOf(config));

        /// <summary>
        /// The fast flow's next instant: <paramref name="wallSeconds"/> of real time at
        /// <paramref name="gameHoursPerRealSecond"/>, never past <paramref name="targetSeconds"/>. A clock
        /// already at or past the target answers the target.
        /// </summary>
        public static double NextSeconds(double nowSeconds, double targetSeconds, float wallSeconds,
                                         float gameHoursPerRealSecond, double secondsPerDay)
        {
            if (nowSeconds >= targetSeconds) return targetSeconds;
            double step = (wallSeconds > 0f ? wallSeconds : 0f)
                          * (gameHoursPerRealSecond > 0f ? gameHoursPerRealSecond : 0f)
                          * SecondsPerGameHour(secondsPerDay);
            double next = nowSeconds + step;
            return next >= targetSeconds ? targetSeconds : next;
        }

        /// <summary>
        /// The instant path: sleep the default hours in one jump on <paramref name="clock"/>. True if the
        /// clock moved; false with no clock, a held hour, or a fake that refuses. The successor of
        /// <see cref="RestMorning.Bring"/>, now held to the eight-hour cap.
        /// </summary>
        public static bool SleepDefault(IGameClock clock, GameConfig config)
        {
            if (clock == null) return false;
            if (clock is UnityEngine.Object unityObject && unityObject == null) return false;
            double now = clock.TotalSeconds;
            double wake = WakeSeconds(now, DefaultHours(now, config), config);
            return wake > now && clock.FastForwardTo(wake);
        }

        static double DayLength(GameConfig config) =>
            config != null ? config.SecondsPerDay : GameServices.SecondsPerDay;

        static float WakeHourOf(GameConfig config) =>
            config != null ? config.WakeHour : GameConfig.DefaultWakeHour;
    }
}
