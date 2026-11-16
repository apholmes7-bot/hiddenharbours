namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>Is this crossing open at this time?</b> The one question a cross-region NPC routine asks before
    /// it sets out across a gated passage, such as the St Peters bar, which is only walkable at low water
    /// (canon §5.8, owner ask 2026-11-15: islanders cross to Nine Mile Creek only while the bar is open).
    ///
    /// <para><b>Times are absolute GAME HOURS</b> since a new game: <c>DayIndex * 24 + HourOfDay</c>. A
    /// gate must be a pure function of its inputs (and of the world seed it was built from), never of
    /// what it was asked before (CLAUDE.md rule 5), because a routine is resolved from the clock and
    /// never saved: asked twice, it must answer the same.</para>
    ///
    /// <para>Core holds only the contract and the always-open default. The real bar window, read off the
    /// deterministic tide and the bar's crest elevation, is a later slice; World asks through
    /// <see cref="GameServices.CrossingGate"/> and never learns who answered (rule 4).</para>
    /// </summary>
    public interface ICrossingGate
    {
        /// <summary>True when <paramref name="crossingId"/> can be set out on at
        /// <paramref name="gameHour"/> (absolute game hours).</summary>
        bool IsOpen(string crossingId, double gameHour);

        /// <summary>
        /// The earliest absolute game hour at or after <paramref name="fromGameHour"/> at which
        /// <paramref name="crossingId"/> is open: <paramref name="fromGameHour"/> itself when it is open
        /// now, <see cref="double.PositiveInfinity"/> when it never opens again.
        /// </summary>
        double NextOpenHour(string crossingId, double fromGameHour);
    }

    /// <summary>The default gate: every crossing is open at every hour. What a crossing does until the
    /// tide-window slice registers a real gate, and what a headless test gets unless it assigns one.</summary>
    public sealed class AlwaysOpenCrossingGate : ICrossingGate
    {
        /// <summary>The one instance; the gate holds no state.</summary>
        public static readonly AlwaysOpenCrossingGate Instance = new AlwaysOpenCrossingGate();

        private AlwaysOpenCrossingGate() { }

        /// <inheritdoc/>
        public bool IsOpen(string crossingId, double gameHour) => true;

        /// <inheritdoc/>
        public double NextOpenHour(string crossingId, double fromGameHour) => fromGameHour;
    }
}
