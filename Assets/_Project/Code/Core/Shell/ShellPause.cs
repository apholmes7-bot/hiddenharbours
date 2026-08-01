using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// Stopping the world for a moment (M1 §7.8's pause menu), on the pattern the tide table set: freeze
    /// through <see cref="IGameClock.IsPaused"/> — the project's ONE pause path — and, on resume, restore
    /// exactly what was found rather than assuming the world was running.
    ///
    /// <para><b>There is no second clock.</b> Not <c>Time.timeScale</c>, not a private stopwatch: the sim
    /// derives everything from <c>(worldSeed, gameTime)</c>, so a stopped game clock IS a stopped world
    /// (rule 5). A page opened during an already-stopped moment therefore cannot be the thing that starts
    /// the sea moving again when it closes.</para>
    ///
    /// <para><b>Why it is shared rather than another private freeze.</b> The tide table owns its own
    /// freeze/thaw pair and is welcome to; but pause is read by things OUTSIDE the page — the player rig
    /// must not be steerable under a pause menu — so "is the shell holding the world?" has to be a fact
    /// anyone can ask, not a private field. <see cref="ShellFlow.WorldInputBlocked"/> is that question.</para>
    /// </summary>
    public static class ShellPause
    {
        /// <summary>True while the shell is holding the world stopped.</summary>
        public static bool IsPaused { get; private set; }

        // What the clock was doing before we touched it, and whether we touched it at all.
        private static bool _clockWasPaused;
        private static bool _frozeClock;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            IsPaused = false;
            _frozeClock = false;
            _clockWasPaused = false;
        }

        /// <summary>Stop the world. Idempotent — a second call while already paused changes nothing, and
        /// in particular does not overwrite the remembered prior state.</summary>
        public static void Pause()
        {
            if (IsPaused) return;
            IsPaused = true;

            var clock = GameServices.Clock;
            if (clock == null) return;
            _clockWasPaused = clock.IsPaused;
            clock.IsPaused = true;
            _frozeClock = true;
        }

        /// <summary>Give the world back exactly as it was handed over. Idempotent.</summary>
        public static void Resume()
        {
            if (!IsPaused) return;
            IsPaused = false;

            if (!_frozeClock) return;
            var clock = GameServices.Clock;
            if (clock != null) clock.IsPaused = _clockWasPaused;
            _frozeClock = false;
        }

        /// <summary>Drop the pause without touching the clock (teardown / tests) — for the case where the
        /// clock itself is going away and there is nothing left to restore.</summary>
        public static void Reset()
        {
            IsPaused = false;
            _frozeClock = false;
            _clockWasPaused = false;
        }
    }
}
