namespace HiddenHarbours.Core
{
    /// <summary>
    /// The shell moved between the title and the world (M1 §7.8). Published by <see cref="ShellFlow"/>
    /// on every transition, including the one at boot, so the surfaces that must react — the title page
    /// itself, the HUD that has no business being on screen over it — react on a single well-defined
    /// edge instead of polling a global each frame (rule 7).
    ///
    /// <para>Cross-module by design: the composition root (App) drives the phase and the UI renders it,
    /// with neither referencing the other (rule 4). It carries the phase rather than making subscribers
    /// read <see cref="ShellFlow.Phase"/> back, so a handler cannot see a phase newer than the edge it
    /// was called for.</para>
    /// </summary>
    public readonly struct ShellPhaseChanged
    {
        public readonly ShellPhase Phase;

        public ShellPhaseChanged(ShellPhase phase) => Phase = phase;
    }

    /// <summary>
    /// The player asked to leave the world and go back to the title (M1 §7.8's pause menu). Published by
    /// <see cref="ShellFlow.QuitToTitle"/> AFTER the game has been saved.
    ///
    /// <para><b>It is a request, not the act.</b> Going back to the title means putting the world back to
    /// its boot state, and only the App layer can do that — it owns the persistent core and the scenes.
    /// Core states the intent and App carries it out, the same direction of travel as every other
    /// cross-module signal (rule 4). A context with no handler (an EditMode rig) simply stays where it
    /// is, which is the honest outcome of "nobody knows how to rebuild this world".</para>
    /// </summary>
    public readonly struct ReturnToTitleRequested
    {
    }
}
