using System;
using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// "Do this once the loaded save is the truth."
    ///
    /// <para><b>The problem it names.</b> Before the shell (M1 §7.8), boot went straight into play, so a
    /// component's <c>Start</c> was a fine place to read the save: <c>GameRoot.Start</c> ran first
    /// (execution order −1000) and the blob was already final. With a title state that is no longer true —
    /// at the title the loaded blob is the OUTGOING game, and the player may be about to write over it.
    /// A one-time grant that reads its flag there would consult the abandoned game and then decline to
    /// grant into the new one, and the player would start their fresh harbour without a shovel.</para>
    ///
    /// <para><b>The edge that is safe</b> is <see cref="GameLoaded"/> — published by
    /// <see cref="SaveRestore"/> at the exact moment the blob became authoritative, for a resumed game and
    /// a new one alike. This helper hides the two-case handling: run NOW if the world has already been
    /// entered (a dev core that skips the title, an EditMode rig, a region played directly), otherwise on
    /// that edge, exactly once.</para>
    ///
    /// <para>Allocates one closure per registration, at boot, and unsubscribes itself. A host that has been
    /// destroyed in the meantime is skipped rather than woken.</para>
    /// </summary>
    public static class SaveReady
    {
        /// <summary>
        /// Run <paramref name="action"/> when the loaded save is authoritative. <paramref name="host"/> is
        /// the component the work belongs to — if it is gone by the time the edge arrives, the work is
        /// dropped (it had nothing left to act on).
        /// </summary>
        public static void Run(MonoBehaviour host, Action action)
        {
            if (action == null) return;

            // Already in the world: the blob is final, so there is nothing to wait for.
            if (!ShellFlow.AtTitle) { action(); return; }

            void OnGameLoaded(GameLoaded _)
            {
                // Same closure instance on both sides, so the delegate compares equal and unsubscribes.
                EventBus.Unsubscribe<GameLoaded>(OnGameLoaded);
                if (host == null) return;   // the component went away while the player sat at the title
                action();
            }

            EventBus.Subscribe<GameLoaded>(OnGameLoaded);
        }
    }
}
