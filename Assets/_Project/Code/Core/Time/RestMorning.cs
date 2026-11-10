namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>Lying down in your own bed brings the morning.</b> The one rule for WHEN a night's rest ends,
    /// and the one call that ends it: the master clock is fast-forwarded to the next time it reads the
    /// wake hour (<see cref="GameConfig.WakeHour"/>, the design's 06:00, time-tides-weather §1.3).
    ///
    /// <para><b>Why this sits in Core and moves nothing itself.</b> The clock lives in the Environment
    /// module; the bed lives in World; the write lives in <see cref="RestSaveResponder"/>. None of them
    /// may reach the others (CLAUDE.md rule 4), so the rule is stated here against the
    /// <see cref="IGameClock"/> contract and the clock does its own moving behind
    /// <see cref="IGameClock.FastForwardTo"/>: the day rollovers it publishes on the way are the same
    /// <see cref="DayStarted"/> a waited-out night publishes, so the market's recovery and every other
    /// once-a-day system follow with no new wiring.</para>
    ///
    /// <para><b>Nothing is simulated through the night and nothing new is saved.</b> Tide, wind, weather,
    /// trap soak, catch freshness and the villagers' routines are functions of <c>(worldSeed,
    /// gameTime)</c>; they are simply read at the woken time (rule 5). The save keeps the clock it always
    /// kept, which is now the morning's.</para>
    ///
    /// <para><b>The edges, decided here and pinned by <c>BedBringsTheMorningTests</c>:</b></para>
    /// <list type="bullet">
    ///   <item><b>Evening or daytime</b> (22:00, noon): tomorrow's wake hour. One midnight is slept
    ///   through, so <see cref="DayStarted"/> fires once. A bed at noon is still a night's sleep.</item>
    ///   <item><b>After midnight, before the wake hour</b> (02:00, 05:50): THIS morning's wake hour. The
    ///   day already started at midnight, in front of the player, so no rollover fires again. Ten minutes
    ///   before the wake hour is therefore a ten-minute lie-down; the alternative (sleeping the whole
    ///   next day away) costs the player a day they cannot get back.</item>
    ///   <item><b>Exactly at the wake hour</b> (a second rest straight after the first): tomorrow's. A
    ///   rest always moves the clock forward, so two rests in a row are two mornings.</item>
    ///   <item><b>The hour is held</b> (the opening holds it): the clock refuses and the rest is a rest
    ///   with no morning. Whoever holds the hour owns it.</item>
    /// </list>
    /// </summary>
    public static class RestMorning
    {
        /// <summary>
        /// How close to the wake instant still counts as standing ON it, in game seconds. Not a tunable:
        /// it is the slack that keeps a clock which woke at exactly the wake hour from being asked to
        /// "sleep" the rounding error of its own arithmetic instead of a night. A microsecond is far
        /// above a double's spacing at any game time a save will reach, and far below a frame.
        /// </summary>
        public const double InstantToleranceSeconds = 1e-6;

        /// <summary>
        /// The game-time a rest begun at <paramref name="nowSeconds"/> wakes at: the first instant
        /// strictly after now at which the clock reads <paramref name="wakeHour"/>. Pure, so the whole
        /// rule is assertable without a clock.
        ///
        /// <para>A day length that is not positive has no hours to wake at, and answers
        /// <paramref name="nowSeconds"/> unchanged (the caller then has nowhere to move to). A wake hour
        /// outside 0..24 wraps onto the dial; a time before the start of the game is read as the start.</para>
        /// </summary>
        public static double WakeSeconds(double nowSeconds, double secondsPerDay, float wakeHour)
        {
            if (!(secondsPerDay > 0d) || double.IsNaN(nowSeconds) || float.IsNaN(wakeHour)) return nowSeconds;

            double now = nowSeconds < 0d ? 0d : nowSeconds;
            double hour = wakeHour % 24d;
            if (hour < 0d) hour += 24d;
            double offset = secondsPerDay * (hour / 24d);

            // Always the same expression, (whole day) x (day length) + (offset), so the instant a rest
            // lands on is bit-for-bit the instant the next rest measures itself against.
            double day = System.Math.Floor(now / secondsPerDay);
            double wake = day * secondsPerDay + offset;
            while (wake <= now + InstantToleranceSeconds)
            {
                day += 1d;
                wake = day * secondsPerDay + offset;
            }
            return wake;
        }

        /// <summary>The wake instant under <paramref name="config"/>, or under the shipped defaults when
        /// no config is wired (<see cref="GameServices.SecondsPerDay"/>,
        /// <see cref="GameConfig.DefaultWakeHour"/>).</summary>
        public static double WakeSeconds(double nowSeconds, GameConfig config) =>
            WakeSeconds(nowSeconds,
                        config != null ? config.SecondsPerDay : GameServices.SecondsPerDay,
                        config != null ? config.WakeHour : GameConfig.DefaultWakeHour);

        /// <summary>
        /// Bring the morning on <paramref name="clock"/>. True if the clock moved; false when there is no
        /// clock, or it refused (held hour, a fake with no backing store). Never throws and never
        /// publishes anything itself: the day rollovers are the clock's own.
        ///
        /// <para>⚠ The explicit Unity null check matters: <see cref="GameServices.Clock"/> can hand back a
        /// destroyed <c>MonoBehaviour</c> behind the interface, which is not C#-null and would otherwise
        /// publish a day from a clock that no longer exists.</para>
        /// </summary>
        public static bool Bring(IGameClock clock, GameConfig config)
        {
            if (clock == null) return false;
            if (clock is UnityEngine.Object unityObject && unityObject == null) return false;

            double now = clock.TotalSeconds;
            double wake = WakeSeconds(now, config);
            return wake > now && clock.FastForwardTo(wake);
        }

        /// <summary>
        /// Whether a rest asked for right now is going to move <paramref name="clock"/>: there is a
        /// living clock, its hour is not held, and the wake instant is ahead of it. The question a
        /// presenter asks BEFORE the morning comes, so the screen can be dark while the light changes.
        ///
        /// <para>It is a forecast, not a promise. A clock with no backing store (most test fakes) answers
        /// true here and still refuses <see cref="Bring"/>, so a caller must act on what
        /// <see cref="Bring"/> returned, never on this.</para>
        /// </summary>
        public static bool WillBring(IGameClock clock, GameConfig config)
        {
            if (clock == null) return false;
            if (clock is UnityEngine.Object unityObject && unityObject == null) return false;
            if (clock.IsHeld) return false;

            double now = clock.TotalSeconds;
            return WakeSeconds(now, config) > now;
        }
    }
}
