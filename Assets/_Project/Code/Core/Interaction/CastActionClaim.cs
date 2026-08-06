namespace HiddenHarbours.Core
{
    /// <summary>
    /// A tiny global claim on the <b>CAST action</b> — the flick gesture that throws a line — so that when
    /// the player is standing at a cleat with a rope in reach, the flick throws THAT rope instead of also
    /// flinging a handline into the wharf.
    ///
    /// <para><b>Why it lives in Core, and why it is not <see cref="InteractionGate"/>.</b> The cast action
    /// is shared across modules: <c>DevFishingInput</c> (Fishing) works the rod with it, and
    /// <c>MooringController</c> (Player) throws a mooring line with it — one verb, two contexts, exactly as
    /// design/deck-boarding-cleats-and-interact-capture.md §3 asks for. Neither module may reference the
    /// other (project-structure.md §5), so the coordination point is a shared Core contract; this is the
    /// same reasoning <see cref="InteractionGate"/> spells out for the INTERACT key. It is a SEPARATE flag
    /// because the two keys are separate: <see cref="InteractionGate"/> also gates boarding and dialogue,
    /// and a player standing at a bollard must still be able to press E and step aboard.</para>
    ///
    /// <para><b>Claimed by PROXIMITY, not by the press.</b> The rope controller sets this while the player
    /// is simply within reach of a workable cleat — before any button goes down. That is deliberate: if the
    /// claim were raised by the press itself, whether the rod also saw that press would depend on component
    /// execution order within the frame, which is exactly the kind of race that produces an
    /// occasionally-flung handline. Claiming on proximity makes the arbitration a function of where the
    /// player is standing, which is deterministic and, more usefully, legible — at a cleat your hands are
    /// on the rope; step a metre off it and the rod is yours again.</para>
    ///
    /// <para>Deliberately a plain static flag, like its sibling: readers need a cheap per-frame check and
    /// there is one player. <see cref="Reset"/> on scene teardown / in test fixtures so a destroyed
    /// controller can never leave the rod wedged off.</para>
    /// </summary>
    public static class CastActionClaim
    {
        /// <summary>True while a rope (not a rod) owns the cast flick. Set by the mooring controller while
        /// the player is in reach of a workable cleat; read by the fishing input.</summary>
        public static bool IsClaimed { get; set; }

        /// <summary>Clear the claim (scene teardown / tests).</summary>
        public static void Reset() => IsClaimed = false;
    }
}
