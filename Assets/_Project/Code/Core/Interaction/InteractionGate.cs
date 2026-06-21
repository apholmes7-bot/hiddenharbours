namespace HiddenHarbours.Core
{
    /// <summary>
    /// A tiny global gate that suppresses world / gameplay INTERACT actions while a modal UI (the
    /// dialogue panel, VS-21) is open, so a single Interact press doesn't both advance the dialogue
    /// AND trigger something underneath it (boarding the dory, selling, etc.).
    ///
    /// Why it lives in Core: INTERACT (the dev key E) is shared across modules — the on-foot
    /// <c>ControlSwitcher</c> (Player) boards/disembarks with it, and the <c>WorldInteractor</c>
    /// (World) talks to NPCs / reads the logbook with it. Neither module may reference the other
    /// (project-structure.md §5), so the coordination point is a shared Core contract, the same way
    /// camera handoff goes through <see cref="ControlModeChanged"/>. The modal UI sets
    /// <see cref="IsBlocked"/> while it is up; interaction handlers early-out when it is set.
    ///
    /// Deliberately a plain static flag (not an event): readers just need a cheap per-frame check,
    /// and there is exactly one modal at a time in the greybox. Reset on scene teardown via
    /// <see cref="Reset"/> so a destroyed panel can never leave interaction wedged off.
    /// </summary>
    public static class InteractionGate
    {
        /// <summary>True while a modal UI owns the Interact key. Set by the modal; read by handlers.</summary>
        public static bool IsBlocked { get; set; }

        /// <summary>Clear the gate (scene teardown / tests).</summary>
        public static void Reset() => IsBlocked = false;
    }
}
