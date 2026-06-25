namespace HiddenHarbours.Core
{
    /// <summary>
    /// The master clock. Everything in the sim derives time from <see cref="TotalSeconds"/>.
    /// Implemented in the Environment module; consumed everywhere through this contract.
    /// </summary>
    public interface IGameClock
    {
        double TotalSeconds { get; }   // in-game seconds since new game (the master value)
        GameTime Now { get; }

        Season Season { get; }
        int Year { get; }              // 1-based
        int DayIndex { get; }          // 0-based absolute day since new game (TotalSeconds / SecondsPerDay)
        int DayOfSeason { get; }       // 1-based, 1..daysPerSeason
        Weekday Weekday { get; }
        bool IsMarketDay { get; }

        float HourOfDay { get; }       // 0..24
        float DayFraction { get; }     // 0..1 through the current day

        bool IsPaused { get; set; }
        float TimeScale { get; set; }  // 1 = normal; >1 fast-forward (sleep/wait)

        /// <summary>
        /// Seek the master clock to an <b>absolute</b> game-time (in-game seconds since a new game), the
        /// inverse of reading <see cref="TotalSeconds"/>. This is how a loaded save resumes at the saved
        /// instant (VS-08 load-restore): the environment is then recomputed from <c>(worldSeed, this
        /// time)</c> — tide/wind/weather are never restored, only the clock that drives them (CLAUDE.md
        /// rule 5). A negative value is clamped to 0 (time never runs before the start of the game).
        ///
        /// <para><b>Additive &amp; non-breaking.</b> A <i>default interface method</i> so existing
        /// implementers (e.g. HUD test fakes) compile unchanged; the real <c>GameClock</c> overrides it to
        /// move its backing time. The default is a safe no-op — a clock with no backing store simply can't
        /// seek — so a fake never throws. Seeking only sets the value; it deliberately does NOT replay the
        /// day/season rollover events for the skipped span (a restore is not a fast-forward — the day is
        /// already where the player left it), matching how the save records <see cref="DayIndex"/> as
        /// derived state rather than a thing to re-fire.</para>
        /// </summary>
        void SeekTo(double totalSeconds) { }
    }
}
