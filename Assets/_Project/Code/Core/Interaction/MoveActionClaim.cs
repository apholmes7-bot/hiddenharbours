namespace HiddenHarbours.Core
{
    /// <summary>
    /// A tiny global claim on the <b>MOVE action</b>, raised by a UI that is currently steering with the
    /// same keys the player walks with — so one push of W cannot both move a cursor and walk the fisher.
    ///
    /// <para><b>Why it exists.</b> The dev-key ledger is exhausted A–Z (the 2026-08-17 sweep), so an
    /// in-world picker cannot bind a fresh selection key: it steers on the move axis, and Interact
    /// confirms. That is fine for a dialogue's two-to-four rows, where the picker is edge-latched and a
    /// tap nudges the fisher a centimetre — <c>WorldInteractor.ReadMoveAxis</c> documents exactly that
    /// wart. It is NOT fine for a wardrobe: you are standing at a fixture with a reach of 1.2 m,
    /// arrowing through eight outfits, and walking a centimetre per row would carry you out of your own
    /// wardrobe halfway down the list.</para>
    ///
    /// <para><b>Why it is not <see cref="InteractionGate"/>.</b> That gate is a modal's hold on the
    /// INTERACT key, and it deliberately leaves movement alone: a dialogue WANTS you to be able to walk
    /// away mid-sentence, because walking away is how you end a conversation. This says the narrower,
    /// different thing — that movement input is spoken for this frame — and the two are set together
    /// only by a UI that wants both.</para>
    ///
    /// <para><b>Deliberately NOT the general movement-claim system, and it must not grow into one
    /// here.</b> The general job — every movement reader honouring a claim, arbitration between several
    /// claimants, and retiring <c>WorldInteractor.ReadMoveAxis</c>'s double-drive — is gameplay-systems'
    /// own work item. This is the minimum that lets a picker be correct: ONE claimant (the wardrobe
    /// picker) and ONE honouring reader (<c>PlayerWalkController.ReadInput</c>, which is the only thing
    /// that can walk you off an interior fixture). A second claimant belongs in that work item, not on
    /// this flag.</para>
    ///
    /// <para><b>Claimed for the DURATION, not by the press</b> — unlike <see cref="InteractActionClaim"/>,
    /// which is raised by proximity every frame, this is raised when a picker opens and lowered when it
    /// closes. There is no frame-order race to avoid because there is no contest: while the flag is up
    /// the axis has exactly one reader, and the walk controller simply reads zero. Reset on teardown so
    /// a destroyed picker can never leave the player unable to move.</para>
    /// </summary>
    public static class MoveActionClaim
    {
        /// <summary>True while a UI owns the move axis. Set by that UI for as long as it is open; read
        /// by movement readers, which treat it as "no input this frame".</summary>
        public static bool IsClaimed { get; set; }

        /// <summary>Clear the claim (scene teardown / tests) — a torn-down picker must never leave the
        /// player wedged in place.</summary>
        public static void Reset() => IsClaimed = false;
    }
}
