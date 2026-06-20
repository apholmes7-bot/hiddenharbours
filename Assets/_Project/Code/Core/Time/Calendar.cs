namespace HiddenHarbours.Core
{
    /// <summary>The four canon seasons (docs/vision-and-pillars.md §5.8). 28 days each.</summary>
    public enum Season
    {
        EarlySpring = 0,
        HighSummer  = 1,
        TheTurn     = 2,   // autumn
        HardWinter  = 3
    }

    /// <summary>
    /// Days of the week. The canon locks a 7-day week with one Market Day at Greywick;
    /// which day is Market Day is tunable in <see cref="GameConfig"/>.
    /// </summary>
    public enum Weekday
    {
        Monday = 0, Tuesday, Wednesday, Thursday, Friday, Saturday, Sunday
    }

    /// <summary>
    /// A lightweight wrapper around the master clock value (in-game seconds since the
    /// start of the game). Everything time-related derives from this. Kept as a struct
    /// so it is cheap to pass around. Calendar breakdown lives on <see cref="IGameClock"/>.
    /// </summary>
    public readonly struct GameTime
    {
        public readonly double TotalSeconds;

        public GameTime(double totalSeconds) { TotalSeconds = totalSeconds; }

        public static GameTime operator +(GameTime t, double seconds) => new GameTime(t.TotalSeconds + seconds);
        public override string ToString() => $"t={TotalSeconds:0.0}s";
    }
}
