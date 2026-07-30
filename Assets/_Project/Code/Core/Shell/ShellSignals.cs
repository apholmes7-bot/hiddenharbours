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
}
