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
        int DayOfSeason { get; }       // 1-based, 1..daysPerSeason
        Weekday Weekday { get; }
        bool IsMarketDay { get; }

        float HourOfDay { get; }       // 0..24
        float DayFraction { get; }     // 0..1 through the current day

        bool IsPaused { get; set; }
        float TimeScale { get; set; }  // 1 = normal; >1 fast-forward (sleep/wait)
    }
}
