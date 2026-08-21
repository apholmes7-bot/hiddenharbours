namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>Is the standing offer entitled to be SHOWN right now?</b> — the one computation behind every
    /// surface that presents <see cref="InteractOfferChanged"/>, so that no two of them can disagree.
    ///
    /// <para><b>Why this exists as a shared answer rather than a rule each surface keeps.</b> The
    /// interaction popup (<c>HiddenHarbours.UI.InteractPopup</c>) names what you are facing; the
    /// affordance lane (<c>HiddenHarbours.Art.InteractAffordancePresenter</c>) lights the thing itself.
    /// They are two halves of ONE statement to the player — "this, here, is what the key would do" — and
    /// the failure mode of splitting them is not cosmetic: a lit fixture under a blank corner, or a named
    /// fixture with nothing lit, teaches the player that the highlight and the words mean different
    /// things. They must rise and fall together, and the cheapest guarantee of that is that there is only
    /// one rule and both read it. This is that rule, lifted out of the popup unchanged.</para>
    ///
    /// <para><b>This is presentation, and only presentation.</b> Nothing here gates the PRESS. Every
    /// suppression below describes a moment when telling the player about an offer would be a lie or a
    /// distraction — never a moment when the offer stopped existing. A caller that used this to decide
    /// whether an interaction may happen would be the second, lagging opinion the whole offer seam was
    /// built to prevent (see <see cref="InteractOffer"/>).</para>
    ///
    /// <para><b>Rule 7.</b> Static reads only — three bools and an enum compare, no allocation, no
    /// subscription to go stale, and safe to call every frame from every surface.</para>
    ///
    /// <para>FLAG lead-architect: Core contract widened (the presentation half of the offer seam).</para>
    /// </summary>
    public static class InteractOfferVisibility
    {
        /// <summary>
        /// Is anything entitled to stop an offer being presented, regardless of what is offered?
        ///
        /// <para><b>The gate and the shell</b> — a dialogue bubble or a picker owns the interact key, or
        /// the title page / pause menu owns the world. In both cases the press being described cannot
        /// happen, and a surface that outlives its own actionability teaches the player a lie.</para>
        ///
        /// <para><b>The helm and the cab.</b> Not because the press is dead there — it steps you back —
        /// but because the owner's 2026-08-19 ruling gives those two views to the boat's own instruments.
        /// <see cref="InteractContext.OnDeck"/> is deliberately NOT suppressed: the deck is a place you
        /// work, there is no dash up, and its offers are ordinary offers.</para>
        ///
        /// <para><b>No probe means no suppression</b>, matching <see cref="InteractActorProbe"/>'s own
        /// fail-open contract: a region scene with no persistent rig has nobody at a helm, so guessing
        /// "suppressed" there would blank every surface for the whole scene.</para>
        /// </summary>
        public static bool Suppressed
        {
            get
            {
                if (InteractionGate.IsBlocked || ShellFlow.WorldInputBlocked) return true;
                if (!InteractActorProbe.Has) return false;
                InteractContext where = InteractActorProbe.Current.Context;
                return where == InteractContext.AtHelm || where == InteractContext.Driving;
            }
        }

        /// <summary>
        /// Should <paramref name="offer"/> be presented right now — i.e. is there something offered AND is
        /// nothing suppressing it? The whole of the visibility decision in one call, which is what a
        /// surface should ask rather than assembling the two halves itself.
        /// </summary>
        public static bool ShouldPresent(in InteractOfferChanged offer) => offer.Has && !Suppressed;

        /// <summary>Convenience for a surface with no held copy: should whatever is standing on
        /// <see cref="InteractOffer.Current"/> be presented?</summary>
        public static bool ShouldPresentCurrent() => ShouldPresent(InteractOffer.Current);
    }
}
