namespace HiddenHarbours.Core
{
    /// <summary>
    /// The master clock. Everything in the sim derives time from <see cref="TotalSeconds"/>.
    /// Implemented in the Environment module; consumed everywhere through this contract.
    /// Forward movement visits each canonical midnight in (start, endpoint], publishing DayStarted
    /// then SeasonChanged if that day turns a season. During the complete pair all calendar/time
    /// reads describe that midnight; after the call they describe the endpoint. Subscriber failures
    /// retain EventBus's log-and-continue policy; this is ordered notification, not a transaction.
    /// </summary>
    public interface IGameClock
    {
        double TotalSeconds { get; }   // in-game seconds since new game (the master value)
        GameTime Now { get; }

        Season Season { get; }
        int Year { get; }              // 1-based
        int DayIndex { get; }          // 0-based day d in [d*SecondsPerDay, (d+1)*SecondsPerDay)
        int DayOfSeason { get; }       // 1-based, 1..daysPerSeason
        Weekday Weekday { get; }
        bool IsMarketDay { get; }

        float HourOfDay { get; }       // 0..24
        float DayFraction { get; }     // 0..1 through the current day

        bool IsPaused { get; set; }
        float TimeScale { get; set; }  // 1 = normal; >1 fast-forward (sleep/wait)

        /// <summary>Hold the hour, calendar, tide, weather and routines while the world keeps flowing.
        /// This is independent of the pause menu; existing clocks default to an unheld hour.</summary>
        bool IsHeld { get => false; set { } }

        /// <summary>The world's running seconds, equal to TotalSeconds except for spans the hour was held.
        /// This reading is never saved and equals TotalSeconds again after any accepted SeekTo.
        /// At a crossed midnight B, subscribers read entryFlow + (B - entryTime), preserving any
        /// existing held gap; completion adds the entire accepted span to entryFlow.</summary>
        double FlowSeconds => TotalSeconds;

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
        /// <para>Non-finite or unrepresentable input is ignored. A nested seek during a forward
        /// notification is also ignored: the advancing call owns its span until it returns.</para>
        /// </summary>
        void SeekTo(double totalSeconds) { }

        /// <summary>
        /// <b>Fast-forward</b> the master clock to a LATER absolute game-time, as time passing: the
        /// opposite contract to <see cref="SeekTo"/>. Every midnight between now and the target publishes
        /// <see cref="DayStarted"/> once, in order (and <see cref="SeasonChanged"/> where a season turns),
        /// exactly as if the hours had been waited out, so once-a-day systems (the market's recovery, the
        /// snow cover) settle once per day slept through (time-tides-weather §1.3, §9.4). Nothing between
        /// is simulated step by step: the environment is a pure function of <c>(worldSeed, gameTime)</c>
        /// and is simply read at the new time (CLAUDE.md rule 5).
        ///
        /// <para>Returns true if the clock moved. A clock REFUSES, and returns false, when the target is
        /// not later than now (time never runs backwards through this door: that is <see cref="SeekTo"/>'s)
        /// or when the hour is held (<see cref="IsHeld"/>: whoever holds the hour owns it). A refusal
        /// changes nothing and publishes nothing.</para>
        /// <para>Missing/invalid calendar configuration, non-finite input/state and unrepresentable
        /// day/calendar values also refuse without partial movement. Pause does not refuse this
        /// absolute request, and TimeScale does not rescale it. The entry calendar and chosen span
        /// stay fixed during dispatch. Flag/rate changes made by handlers apply to the next call;
        /// handlers must not mutate calendar configuration.</para>
        /// <para>Nested Advance/SeekTo calls on the advancing clock are no-ops; nested FastForwardTo
        /// returns false. These calls are not queued. Work requiring the completed span, including
        /// saves and trades, runs after this method returns, outside the day/season handlers.</para>
        ///
        /// <para><b>Additive &amp; non-breaking</b>, like <see cref="SeekTo"/>: a default interface method
        /// that refuses, so a test fake with no backing store compiles unchanged and a rest taken against
        /// one is simply a rest with no morning.</para>
        /// </summary>
        bool FastForwardTo(double totalSeconds) => false;
    }
}
