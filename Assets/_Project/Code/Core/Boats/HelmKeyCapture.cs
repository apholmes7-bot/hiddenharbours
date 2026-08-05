namespace HiddenHarbours.Core
{
    /// <summary>
    /// A tiny global gate that says <b>a text field owns the keyboard right now</b>, so the helm's own
    /// key reads go deaf for as long as it does (ADR 0025 S6 PR 4 — naming a waypoint on the
    /// chartplotter's MAX face).
    ///
    /// <para><b>Why it has to exist.</b> W/A/S/D are live helm keys and they are also four of the
    /// letters in "WRECK". Without a gate, typing a waypoint name steers the boat — the trap the MAX
    /// face's waypoint manager introduces the moment it accepts a character. Nothing in the project
    /// suppressed the helm before this: <see cref="ShellFlow.WorldInputBlocked"/> is the STRONGER
    /// statement (the world is stopped — the title page and the pause menu, and the clock with it),
    /// which is exactly wrong here because the boat must keep sailing while you name a mark on the
    /// chart. <see cref="InteractionGate"/> is the right SHAPE but the wrong key: it owns the shared
    /// INTERACT key and the gear keys, not the helm.</para>
    ///
    /// <para><b>Why it lives in Core.</b> The setter is UI (the plotter's name editor) and the reader is
    /// Boats (<c>DevBoatInput</c>); neither module may reference the other
    /// (project-structure.md §5), so the coordination point is a shared Core contract — the same
    /// reasoning, and the same plain-static shape, as <see cref="InteractionGate"/>.</para>
    ///
    /// <para><b>The RAW key state, never the eased tail</b> (the <c>DevBoatInput.ArbitrateSteer</c>
    /// precedent). A reader gates the momentary read it takes from the device, BEFORE the steer ease
    /// and before the wheel-session arbitration. That ordering matters twice: an eased tail winding out
    /// after a key release is the helm's own follow-through and must not be frozen mid-swing by a text
    /// field opening; and a zero raw read is precisely what lets a live wheel drag KEEP its held steer
    /// through the edit rather than being stomped to centre.</para>
    ///
    /// <para><b>Counted, not toggled.</b> <see cref="Begin"/>/<see cref="End"/> nest, so two editors
    /// (or an editor and a future rename field) cannot have the first one to close hand the helm back
    /// while the second is still taking characters. Clamped at zero: a stray <see cref="End"/> can
    /// never drive the count negative and wedge the gate permanently open.</para>
    ///
    /// <para>Reset at subsystem registration and via <see cref="Reset"/> (teardown / tests) so a
    /// destroyed editor can never leave the helm wedged off — the failure this gate would otherwise
    /// make possible is a boat you cannot steer, which is worse than the bug it prevents.</para>
    /// FLAG lead-architect: new Core contract + one read in <c>DevBoatInput</c> (Boats). See the PR body.
    /// </summary>
    public static class HelmKeyCapture
    {
        private static int _depth;

        /// <summary>True while a text field owns the keyboard and helm KEY reads must read as idle.
        /// Gamepad input is deliberately untouched — you cannot type on a gamepad, so there is nothing
        /// to arbitrate there and taking it away would be a bug, not a safeguard.</summary>
        public static bool IsCapturing => _depth > 0;

        /// <summary>How many editors are holding the keyboard. Exposed so a test can prove the nesting
        /// balances rather than merely that the flag ended up false.</summary>
        public static int Depth => _depth;

        /// <summary>Take the keyboard for a text field.</summary>
        public static void Begin() => _depth++;

        /// <summary>Give it back. Clamped at zero — an unmatched call is a no-op, never a negative
        /// count that would leave <see cref="IsCapturing"/> stuck true.</summary>
        public static void End()
        {
            if (_depth > 0) _depth--;
        }

        /// <summary>Drop every claim (scene teardown / tests).</summary>
        public static void Reset() => _depth = 0;

        // Statics survive a play session when the editor's domain reload is disabled, so a session that
        // ended mid-edit would start the next one with a dead helm. Re-seed before any scene loads.
        [UnityEngine.RuntimeInitializeOnLoadMethod(
            UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => _depth = 0;
    }
}
