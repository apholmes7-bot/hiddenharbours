namespace HiddenHarbours.Core
{
    /// <summary>
    /// Where the player is in the game's <b>shell</b> — the frame around play (M1 §7.8). There are
    /// exactly two states, and deliberately no third: the shell is a doorway, not a place.
    ///
    /// <para>ONE boot path (lead-architect's ruling for §7.8): the persistent core comes up exactly as
    /// it always has, and the shell is a STATE it starts in rather than a separate title scene — the
    /// persistent-core + additive-region contract (ADR 0004) must not fork.</para>
    /// </summary>
    public enum ShellPhase
    {
        /// <summary>Booted, services wired, world NOT yet entered: the clock is stopped and the title
        /// page is up. New Game / Continue are the only ways out of here.</summary>
        Title = 0,

        /// <summary>In the world. The save has been applied, the clock runs, the game is the game.
        /// A pause menu opens over this without leaving it (pausing is not a phase — see
        /// <see cref="ShellFlow"/>).</summary>
        Playing = 1,
    }
}
