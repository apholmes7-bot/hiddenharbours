namespace HiddenHarbours.Player
{
    /// <summary>
    /// The single home for every player-facing string the <see cref="ControlSwitcher"/> shows — the
    /// transit offers ("Climb aboard", "Take the helm") and the refusals that answer a press it cannot
    /// honour. Same convention as <c>HudStrings</c>, <c>WorldStrings</c> and <c>ShellStrings</c>: there is
    /// no runtime localization system wired yet, so centralising the copy here is the seam. When loc
    /// tables land these become lookups and no call site changes.
    ///
    /// <para><b>None of these names a key</b> (the owner's 2026-08-19 ruling). The strings the switcher
    /// used to park over the world — "E: Board", "E: Dock", "E: Leave the helm" — spelled the button
    /// because they were a floating prompt with nowhere else to put the instruction. They are now OFFERS
    /// on the Core offer channel, drawn by the one interaction popup, which names the ACTION: there is a
    /// single interact key and the game does not make the player hunt for it in a label.</para>
    ///
    /// <para><b>Kept allocation-free.</b> Every member is an interned literal — the offer is stated every
    /// frame the switcher has one, so a composed string here would be per-frame garbage (rule 7).</para>
    /// </summary>
    public static class ControlStrings
    {
        // ---- the transit offers (what the popup shows) --------------------------------------

        /// <summary>On foot, at a boat you may board.</summary>
        public const string Board = "Climb aboard";

        /// <summary>On deck, standing at the helm station.</summary>
        public const string TakeHelm = "Take the helm";

        /// <summary>At the helm — the press steps back onto the deck.
        ///
        /// <para>⚠ <b>Not shown today</b>, and neither is <see cref="GetOut"/>: the popup stands down at
        /// the helm and in a cab, where the owner's ruling gives the view to the boat's own instruments
        /// (see <c>InteractPopup.Suppressed</c>). Both are kept because the press they name still works,
        /// because un-suppressing either view is then a one-line change rather than a copy hunt, and
        /// because a localization table with holes in it is worse than one with two spare rows.</para></summary>
        public const string LeaveHelm = "Leave the helm";

        /// <summary>On deck, over the authored dock/wharf.</summary>
        public const string Dock = "Step onto the dock";

        /// <summary>On deck, over land — the same step ashore, said for a beach rather than a wharf.</summary>
        public const string StepAshore = "Step ashore";

        /// <summary>Behind the wheel of a road vehicle.</summary>
        public const string GetOut = "Get out";

        // ---- the transit ids (what a listener matches on) ------------------------------------
        //
        // A transit offer is not a registered IInteractable, so it has no IInteractable.Id to borrow. These
        // stand in: stable, id-shaped (type.snake_case, CLAUDE.md §5), and append-only, so the affordance
        // lane can match a transit the same way it matches a fixture.

        public const string IdBoard      = "transit.board";
        public const string IdTakeHelm   = "transit.take_helm";
        public const string IdLeaveHelm  = "transit.leave_helm";
        public const string IdStepAshore = "transit.step_ashore";
        public const string IdGetOut     = "transit.get_out";

        // ---- refusals -----------------------------------------------------------------------

        /// <summary>
        /// Standing at a boat you own but have not repaired. A REFUSAL, not an offer: it says why the
        /// press will do nothing and points at the shipwright, which is the opening's whole nudge.
        ///
        /// <para><b>It no longer rides the interaction popup, and must not.</b> The popup names what a
        /// press WILL do; a line in it that means "nothing, and here is why" is the plain-text habit
        /// wearing the popup's clothes. It goes out on the notice channel — with the rest of the
        /// refusals — pending the owner's ruling on where refusals finally live (the PR's disposition
        /// table argues for the fisher's own voice in the dialogue surface).</para>
        /// </summary>
        public const string BoatNeedsRepairs = "She needs repairs before she'll sail";
    }
}
