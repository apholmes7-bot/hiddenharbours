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
    /// <b>The one line the interaction popup shows</b> — what the game is offering to do if you press
    /// interact right now, published by <see cref="InteractOffer"/> on CHANGE only.
    ///
    /// <para><b>Why this is not <see cref="InteractCandidateChanged"/>, which it otherwise resembles.</b>
    /// That signal is the <see cref="InteractResolver"/>'s answer and nothing else's: one publisher
    /// (<see cref="InteractVerb"/>), one kind of candidate (<see cref="IInteractable"/>), an id and
    /// deliberately no position, and an art-side highlight riding it. The popup has to name things the
    /// resolver has never heard of — a neighbour you can talk to, a rail you can climb over — so it needs
    /// a channel that all three readers of the interact key can feed, and it needs a LABEL, which no
    /// candidate contract carried before. Widening the existing signal would have made two publishers
    /// fight over one edge-triggered channel and quietly broken the highlight. So the two coexist: this
    /// one is the OFFER (what the player is told), that one is the CANDIDATE (what the resolver picked).</para>
    ///
    /// <para><b>The position IS carried here, where the candidate signal refuses to carry one</b> — and the
    /// reason the refusal was right does not apply. That signal republishes only when the winning ID
    /// changes, so a position on it would go stale the moment the winner moved; this one republishes
    /// whenever any field differs, the moving-candidate case included, because the affordance lane draws
    /// AT the thing and a position it cannot trust is worse than none.</para>
    ///
    /// <para><b>Nothing downstream may make a gameplay decision off this</b> — see
    /// <see cref="InteractOffer"/>. It is a picture of decisions already made.</para>
    /// </summary>
    public readonly struct InteractOfferChanged
    {
        /// <summary>Which reader of the interact key is offering, or <see cref="InteractOfferSource.None"/>
        /// when nothing is — which is the signal to hide the popup.</summary>
        public readonly InteractOfferSource Source;

        /// <summary>A stable id for the thing offered — an <see cref="IInteractable.Id"/>, an NPC id, or a
        /// transit key. What the affordance lane matches its own registrant on.</summary>
        public readonly string Id;

        /// <summary>The ONE line to show: "Talk — Aunt Ginny", "Climb aboard", "Sleep". Never a key name —
        /// there is one interact key and naming it is the plain-text habit this replaced.</summary>
        public readonly string Label;

        /// <summary>Where the thing is, world metres. Read live by the feeder.</summary>
        public readonly UnityEngine.Vector2 WorldPosition;

        /// <summary>Squared distance from the actor to it — the tie-break between two equal-rank sources,
        /// and useful to a listener that wants to fade with range.</summary>
        public readonly float DistanceSqr;

        /// <summary>True when something is actually being offered.</summary>
        public bool Has => Source != InteractOfferSource.None && !string.IsNullOrEmpty(Label);

        public InteractOfferChanged(InteractOfferSource source, string id, string label,
                                    UnityEngine.Vector2 worldPosition, float distanceSqr)
        {
            Source = source;
            Id = id;
            Label = label;
            WorldPosition = worldPosition;
            DistanceSqr = distanceSqr;
        }

        /// <summary>Nothing is offered — hide the popup.</summary>
        public static InteractOfferChanged None => default;

        /// <summary>
        /// Field-by-field equality, which is what "did the offer change?" actually means.
        ///
        /// <para>Spelled out rather than left to <c>Equals</c>: the default struct equality on a type with
        /// a <c>string</c> and a <c>Vector2</c> in it goes through reflection-backed boxing on some
        /// runtimes, and this runs on every slot write. Strings compare ordinal for the same reason the
        /// tie-break does.</para>
        /// </summary>
        public bool SameAs(in InteractOfferChanged other)
            => Source == other.Source
               && string.Equals(Id, other.Id, System.StringComparison.Ordinal)
               && string.Equals(Label, other.Label, System.StringComparison.Ordinal)
               && WorldPosition == other.WorldPosition
               && DistanceSqr == other.DistanceSqr;
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
