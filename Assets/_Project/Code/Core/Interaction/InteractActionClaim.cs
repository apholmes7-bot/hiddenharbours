namespace HiddenHarbours.Core
{
    /// <summary>
    /// A tiny global claim on the <b>INTERACT action</b>, raised by a consumer that reads the interact key
    /// directly and predates the <see cref="InteractVerb"/> seam — so that a press which is already
    /// spoken for cannot ALSO be spent on a registered candidate standing in the same spot.
    ///
    /// <para><b>Why it exists at all.</b> Interact (dev key E) is read today in two places that coordinate
    /// only by a hand-asserted "our ranges don't overlap": <c>ControlSwitcher</c> (board / helm / step
    /// ashore) and <c>WorldInteractor</c> (talk to an NPC, read the logbook). That assertion is checked by
    /// nobody, and at St Peters it is <b>already false</b> — Aunt Ginny stands 1.80 m from her freezer
    /// against the interactor's 1.8 m radius, and the seawater spot's 4 m reach overlaps Junior Poirier's.
    /// The switcher's half is safe because it is dispatched in one method; the verb, arriving as a third
    /// reader, would otherwise fire on the same press that starts a conversation.</para>
    ///
    /// <para><b>Why it is not <see cref="InteractionGate"/>.</b> That gate is a MODAL's hold — a dialogue
    /// panel that is up owns the key completely, boarding included. This is weaker and narrower: it says
    /// "an older consumer has a target right now", and it stands down only the verb. You may still press E
    /// and step aboard your dory while standing next to a neighbour.</para>
    ///
    /// <para><b>Claimed by PROXIMITY, never by the press</b> — the doctrine <see cref="CastActionClaim"/>
    /// spells out, for the same reason: a claim raised by the press itself would make the outcome depend
    /// on component execution order within the frame, which is exactly the race that produces an
    /// occasional double-fire. The incumbent raises this while it merely HAS a candidate, which is a
    /// function of where you are standing — deterministic, and legible.</para>
    ///
    /// <para><b>The one edge, stated rather than hidden.</b> Claimant and reader are separate
    /// <c>Update</c>s in undefined order, so on the single frame the player crosses an NPC's radius the
    /// verb may read the previous frame's answer. That is the whole exposure, and it is why the claim is
    /// raised by standing there rather than by pressing: in any settled position — which is every position
    /// a player presses a key from — the flag is already correct, so a press next to a neighbour cannot be
    /// spent twice. A claim raised by the press would have no such settled state and would be a genuine
    /// race.</para>
    ///
    /// <para><b>This is transitional and it is meant to die.</b> It is the shim that lets the seam land
    /// without restructuring the two incumbents. When they migrate onto <see cref="IInteractable"/> — an
    /// NPC becomes a candidate, boarding becomes a candidate — the resolver arbitrates them by the same
    /// rules as everything else and this flag is deleted, not extended. Do not add a third claimant to it:
    /// register a candidate instead.</para>
    /// </summary>
    public static class InteractActionClaim
    {
        /// <summary>True while an older direct reader of the interact key has a target in range. Set by
        /// that reader every frame from its own proximity answer; read by <see cref="InteractVerb"/>.</summary>
        public static bool IsClaimed { get; set; }

        /// <summary>Clear the claim (scene teardown / tests) — a destroyed reader must never leave the
        /// verb wedged off.</summary>
        public static void Reset() => IsClaimed = false;
    }
}
