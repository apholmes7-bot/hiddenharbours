namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>Which single thing the interact verb would act on right now</b> — the signal M2-39's diegetic
    /// highlight rides, published ONLY when the answer changes.
    ///
    /// <para><b>Why this is the right shape for a no-UI affordance.</b> The minimal-HUD canon forbids a
    /// floating "[E] interact" label; the outline-interaction-language proposal (§4.3) asks instead that
    /// the highlight be driven by the interaction system's own answer, never by a hand-kept list of
    /// prefabs — "if the interaction layer would let the player act on it right now, it outlines". This
    /// struct <i>is</i> that answer. The art-side presenter (art-pipeline's half of M2-39) subscribes and
    /// matches its own registrant by <see cref="Id"/>; the interaction lane never references a renderer
    /// and the art lane never references a registrant (rule 4) — the same one-way Core handoff
    /// <see cref="TrapPlaced"/> uses.</para>
    ///
    /// <para><b>Id only, and no position.</b> A position would have to be republished every frame to stay
    /// true of a candidate that moves, and a per-frame publish is exactly what rule 7 rules out. The
    /// presenter already owns the thing it is drawing, so it reads the position off its own transform. The
    /// id is enough to say <i>which</i>.</para>
    ///
    /// <para><b>Nothing downstream may make a gameplay decision off this.</b> It is a picture of a
    /// decision that has already been made by <see cref="InteractResolver"/>; a listener that acted on it
    /// would be a second, lagging copy of the selection rule.</para>
    /// </summary>
    public readonly struct InteractCandidateChanged
    {
        /// <summary>The winning candidate's <see cref="IInteractable.Id"/>, or null when there is no
        /// candidate — which is the signal to drop the highlight.</summary>
        public readonly string Id;

        /// <summary>True when there is a candidate (i.e. <see cref="Id"/> is set).</summary>
        public bool Has => !string.IsNullOrEmpty(Id);

        public InteractCandidateChanged(string id) { Id = id; }

        /// <summary>Nothing is a candidate — drop the highlight.</summary>
        public static InteractCandidateChanged None => new InteractCandidateChanged(null);
    }

    /// <summary>
    /// The interact verb fired, and this is the candidate it fired on. Published AFTER
    /// <see cref="IInteractable.Interact"/> has run, so a listener sees the world as the action left it.
    ///
    /// <para>For audio (a latch, a lid, a rope creak), for the toast channel, and — the reason it exists
    /// at all here — so a PlayMode test can assert that a press reached exactly one candidate without
    /// reaching into either lane's internals.</para>
    /// </summary>
    public readonly struct InteractPerformed
    {
        /// <summary>The <see cref="IInteractable.Id"/> that was acted on.</summary>
        public readonly string Id;

        public InteractPerformed(string id) { Id = id; }
    }
}
