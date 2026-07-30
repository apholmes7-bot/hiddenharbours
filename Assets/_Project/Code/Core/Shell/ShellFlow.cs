using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// The shell's state machine (M1 §7.8) — the one place that knows whether the player is at the
    /// title or in the world, and the only thing allowed to move them between the two.
    ///
    /// <para><b>Why this lives in Core.</b> Entering the world IS the load-restore that
    /// <c>GameRoot.Start</c> used to run inline, and every piece of it is already a Core seam:
    /// <see cref="ISaveService"/> holds the blob, <see cref="SaveRestore"/> pushes it into the live
    /// services, <see cref="IGameClock"/> is stopped and started. Putting the sequence here means the
    /// composition root (App) and the title page (UI) drive the same code down to the line, rather than
    /// two lanes each half-implementing a boot.</para>
    ///
    /// <para><b>Stopping the world is the clock's pause flag</b> — the project's one pause path, the same
    /// one <c>TidePanel</c> freezes time with. There is deliberately no second clock and no
    /// <c>Time.timeScale</c> here: the sim derives everything from <c>(worldSeed, gameTime)</c>, so a
    /// stopped game clock IS a stopped world (rule 5).</para>
    ///
    /// <para><b>Pausing is not a phase.</b> A pause menu opens over <see cref="ShellPhase.Playing"/> and
    /// freezes the clock itself, restoring what it found on close — exactly as the tide table does. Only
    /// leaving the world entirely comes back through here.</para>
    /// </summary>
    public static class ShellFlow
    {
        /// <summary>
        /// Where the shell is. Defaults to <see cref="ShellPhase.Playing"/> — "no shell is running" —
        /// because the shell is OPT-IN: it exists only once a composition root calls
        /// <see cref="EnterTitle"/>. A context with no boot flow (an EditMode rig, a PlayMode test, the
        /// owner pressing Play straight into a region scene) is by definition already in the world, and
        /// must not be told it is at a title that will never be drawn.
        /// </summary>
        public static ShellPhase Phase { get; private set; } = ShellPhase.Playing;

        /// <summary>True while the world has not been entered yet.</summary>
        public static bool AtTitle => Phase == ShellPhase.Title;

        /// <summary>
        /// Is there a game on disk to go back to? Drives whether the title page offers Continue at all —
        /// a first launch must not dangle a button that loads nothing.
        /// </summary>
        public static bool HasGameToContinue
            => GameServices.Save != null && GameServices.Save.LoadedExistingSave;

        // Statics survive a play session when the editor's domain reload is disabled, so a session that
        // ended at the title would leave Phase == Title for the next one. Re-seed at subsystem
        // registration (before any scene loads) so every launch starts from the same slate.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Phase = ShellPhase.Playing;

        /// <summary>
        /// Park at the title: stop the clock and announce the phase, WITHOUT applying the save. Called by
        /// the composition root once the services are wired (<c>GameRoot.Start</c>) — the world exists
        /// and renders behind the page, but it has not been entered.
        ///
        /// <para>Not guarded on the current phase: a re-entry must still stop the clock and re-announce,
        /// or a stale phase (see <see cref="ResetStatics"/>) would swallow the one edge the title page
        /// listens for and leave a stopped world with nothing on screen.</para>
        /// </summary>
        public static void EnterTitle()
        {
            var clock = GameServices.Clock;
            if (clock != null) clock.IsPaused = true;

            Phase = ShellPhase.Title;
            EventBus.Publish(new ShellPhaseChanged(ShellPhase.Title));
        }

        /// <summary>
        /// Throw away the game in progress and enter the world on a fresh blob (M1 §7.8's forcing item:
        /// without this a tester cannot get a second first-impression, and cannot recover from a bug
        /// except by hand-deleting a file).
        ///
        /// <para><b>Destructive, and it commits immediately.</b> The fresh save is written to disk by
        /// <see cref="ISaveService.BeginNewGame"/> before the world is entered, so "I started over" is
        /// true even if the process dies in the next second. The CONFIRM that guards it is the title
        /// page's job, not this method's — a call here means the player has already said yes.</para>
        /// </summary>
        public static void StartNewGame()
        {
            var save = GameServices.Save;
            if (save != null) save.BeginNewGame();
            EnterWorld();
        }

        /// <summary>
        /// Enter the world on the save that is already loaded — Continue, and equally the boot path for a
        /// core configured to skip the title. With no save on disk this is a new game by definition (the
        /// service minted a fresh blob at launch), which is why it is the same call.
        /// </summary>
        public static void ContinueGame() => EnterWorld();

        /// <summary>
        /// Apply the loaded save to the live services, then start the clock. Verbatim the sequence
        /// <c>GameRoot.Start</c> ran before the shell existed, including the reason it passes null for a
        /// new game: a fresh blob's <c>gameTime</c> is 0, and seeking to it would drag the authored start
        /// hour back to midnight. Either way <see cref="GameLoaded"/> fires, so subscribers (the owned
        /// fleet) keep their single code path.
        ///
        /// <para>Restore lands BEFORE the clock is released, so the first frame that ticks already holds
        /// the restored instant — the environment is then recomputed from it (rule 5).</para>
        /// </summary>
        private static void EnterWorld()
        {
            var save = GameServices.Save;
            bool resumed = save != null && save.LoadedExistingSave;
            SaveData data = resumed ? save.Current : null;

            SaveRestore.ApplyToLiveServices(
                data,
                GameServices.Clock,
                GameServices.Wallet,
                GameServices.Licenses);

            var clock = GameServices.Clock;
            if (clock != null) clock.IsPaused = false;

            Phase = ShellPhase.Playing;
            EventBus.Publish(new ShellPhaseChanged(ShellPhase.Playing));
        }

        /// <summary>Back to "no shell is running" (scene teardown / tests), matching
        /// <see cref="GameServices.Reset"/>. Publishes nothing — this is a slate wipe, not a transition
        /// anyone should react to.</summary>
        public static void Reset() => Phase = ShellPhase.Playing;
    }
}
