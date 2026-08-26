using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>Which reader of the interact key is offering this press</b> — and, because the values are ranks,
    /// which offer the player is shown when more than one is standing.
    ///
    /// <para><b>The ranks are not a new policy.</b> They are the precedence
    /// <c>ControlSwitcher.BeginInteract</c> already dispatches in, written down: getting your whole body
    /// somewhere else is answered before anything else, the registry is consulted after it, and a
    /// conversation partner in range stands the registry down through
    /// <see cref="InteractActionClaim"/>. A popup that ranked them differently would be advertising a
    /// press the game will not perform.</para>
    ///
    /// <para><b>One transit does NOT outrank the registry, and it does not need a rank to say so.</b>
    /// Since the 2026-08-25 ruling, stepping ashore yields to a resolving fixture — so on deck the
    /// switcher simply makes no <see cref="Transit"/> offer while one would take the press, and
    /// <see cref="Fixture"/> wins by being the only offer standing. That is deliberate: a feeder that
    /// will not perform the press must not state the offer at all, which keeps this ladder a plain
    /// ordering of what CAN happen rather than a table of exceptions about what will.</para>
    ///
    /// <para>Values are <b>append-only</b> and deliberately gapped, so a later source can land between two
    /// of these without renumbering anything.</para>
    /// </summary>
    public enum InteractOfferSource
    {
        /// <summary>Nothing is offering. Only ever the value of an empty slot.</summary>
        None = 0,

        /// <summary>A registered <see cref="IInteractable"/> — a bed, a wardrobe, a stair, a clam hole, a
        /// pail at your feet, a boat's cabin door. Lowest, because <c>ControlSwitcher</c> consults the
        /// registry after the transitions that can still take the press off it — boarding, and taking the
        /// helm. It nevertheless wins the popup on a docked deck: not by outranking the step ashore, but
        /// because that offer stands itself down there (see <see cref="Transit"/>).</summary>
        Fixture = 10,

        /// <summary>Somebody to talk to, or something to read (<c>WorldInteractor</c>). Outranks a fixture
        /// because it already takes the press off the registry via
        /// <see cref="InteractActionClaim"/>.</summary>
        Conversation = 20,

        /// <summary>Getting your whole body somewhere else — climbing aboard, taking the helm, stepping
        /// ashore, getting out of the truck. Highest, because the switcher answers these before it
        /// consults the registry — with ONE exception, which it keeps by silence rather than by rank:
        /// stepping ashore yields to a resolving fixture (2026-08-25), so on a deck where one is standing
        /// this slot is left empty. The rank therefore still means exactly what it says — when a transit
        /// is on the table, it is what the press does.</summary>
        Transit = 30,
    }

    /// <summary>
    /// <b>The one thing the game is offering to do if you press interact right now</b> — the single
    /// arbitrated answer, held here and published on CHANGE only.
    ///
    /// <para><b>What this is FOR.</b> The owner's 2026-08-19 ruling is that the whole of the UI is four
    /// surfaces — boat instruments, watch face, notebook, dialogue — plus one small popup, lower right,
    /// naming the interaction available on the trigger the player is FACING. That popup cannot compute its
    /// own answer: candidacy is decided in three different places (the switcher's transitions, the
    /// conversation interactor, the interact registry), each of which already owns a piece of the press.
    /// A popup that recomputed any of it would be a second, lagging opinion about what the key does — the
    /// precise failure <see cref="InteractCandidateChanged"/>'s remarks forbid. So the computers of
    /// candidacy PUSH their answer here, and the popup only draws.</para>
    ///
    /// <para><b>One slot per source, and a source may only write its own.</b> Each feeder states its own
    /// answer every frame — including "nothing", via <see cref="Clear"/> — and this class picks the winner
    /// by <see cref="InteractOfferSource"/> rank, then by nearness, then by ordinal id. That is
    /// <see cref="InteractResolver.Beats"/>'s ladder with the source rank standing in for priority, and it
    /// is a <i>total</i> order for the same reason: exact comparisons all the way down, so the winner
    /// cannot depend on which feeder happened to run first in the frame.</para>
    ///
    /// <para><b>Edge-triggered, never per-frame</b> (rule 7). Writing a slot recomputes the winner, and
    /// <see cref="InteractOfferChanged"/> is published only when the winner is not the one already
    /// standing — compared field by field, so a moving candidate at a NEW position does republish (the
    /// affordance lane needs that) while a settled one costs three compares a frame and no event traffic.
    /// Nothing here allocates: the slots are a fixed array of structs written in place.</para>
    ///
    /// <para><b>Nothing downstream may make a gameplay decision off this.</b> Like
    /// <see cref="InteractCandidateChanged"/>, it is a picture of decisions already made. A listener that
    /// acted on it would be a fourth reader of the interact key.</para>
    ///
    /// <para>FLAG lead-architect: new Core contract (the offer seam behind the interaction popup).</para>
    /// </summary>
    public static class InteractOffer
    {
        // The ranks above, in slot order. Fixed and tiny: three feeders, indexed by their own source, so a
        // write is an array store and the arbitration is a three-iteration loop over structs.
        private static readonly InteractOfferSource[] Sources =
        {
            InteractOfferSource.Fixture,
            InteractOfferSource.Conversation,
            InteractOfferSource.Transit,
        };

        private static readonly InteractOfferChanged[] Slots = new InteractOfferChanged[Sources.Length];

        /// <summary>
        /// The offer standing right now — the last thing published, so a late subscriber (or a test) can
        /// read the current state instead of waiting for the next change. <see cref="InteractOfferChanged.Has"/>
        /// is false when nothing is offered.
        /// </summary>
        public static InteractOfferChanged Current { get; private set; }

        /// <summary>
        /// State one source's answer. An empty <paramref name="label"/> is the same as
        /// <see cref="Clear"/> — a feeder with nothing to offer says so by calling this with null rather
        /// than by going quiet, because a slot nobody rewrites is a slot that keeps offering a stale
        /// interaction.
        /// </summary>
        /// <param name="source">The caller's own source. One feeder, one slot.</param>
        /// <param name="id">A stable id for the thing being offered — <see cref="IInteractable.Id"/>, an
        /// NPC id, or a transit key. Diagnostics, and what the affordance lane matches its own registrant
        /// on.</param>
        /// <param name="label">The ONE line the popup shows ("Talk — Aunt Ginny", "Climb aboard",
        /// "Sleep"). Null or empty clears the slot.</param>
        /// <param name="worldPosition">Where the thing is, in world metres — for the affordance lane,
        /// which draws AT it. Read live by the feeder, so a pail on a rocking deck republishes as it
        /// moves.</param>
        /// <param name="distanceSqr">Squared distance from the actor to the thing, for the tie-break
        /// between two sources of equal rank. Feeders have already computed it; asking for it here keeps
        /// this class from needing to know where the player is.</param>
        public static void Set(InteractOfferSource source, string id, string label,
                               Vector2 worldPosition, float distanceSqr)
        {
            if (string.IsNullOrEmpty(label)) { Clear(source); return; }

            int i = IndexOf(source);
            if (i < 0) return;              // an unknown source writes nothing rather than throwing
            Slots[i] = new InteractOfferChanged(source, id, label, worldPosition, distanceSqr);
            Recompute();
        }

        /// <summary>This source has nothing to offer. Called every frame by a feeder with no candidate —
        /// see <see cref="Set"/> on why silence is not the same thing.</summary>
        public static void Clear(InteractOfferSource source)
        {
            int i = IndexOf(source);
            if (i < 0) return;
            if (Slots[i].Source == InteractOfferSource.None) return;   // already empty; nothing changed
            Slots[i] = default;
            Recompute();
        }

        /// <summary>Empty every slot and publish the clearing change if there was one (scene teardown /
        /// test isolation). A torn-down region must never leave a popup naming a fixture that is gone.</summary>
        public static void Reset()
        {
            for (int i = 0; i < Slots.Length; i++) Slots[i] = default;
            Recompute();
        }

        private static int IndexOf(InteractOfferSource source)
        {
            for (int i = 0; i < Sources.Length; i++)
                if (Sources[i] == source) return i;
            return -1;
        }

        /// <summary>
        /// Pick the winner and publish it if it is new. The ladder: higher <see cref="InteractOfferSource"/>
        /// rank, then nearer, then lower id by ordinal — <see cref="InteractResolver.Beats"/>'s shape, for
        /// its reason (a total order is order-independent).
        /// </summary>
        private static void Recompute()
        {
            InteractOfferChanged best = default;
            for (int i = 0; i < Slots.Length; i++)
            {
                InteractOfferChanged c = Slots[i];
                if (c.Source == InteractOfferSource.None) continue;
                if (best.Source == InteractOfferSource.None || Beats(c, best)) best = c;
            }

            if (best.SameAs(Current)) return;
            Current = best;
            EventBus.Publish(best);
        }

        /// <summary>The ranking, spelled out once and exposed so a test can assert the ladder directly.
        /// Ordinal id comparison, not culture-aware: a tie-break that changes with the machine's locale is
        /// not a tie-break.</summary>
        public static bool Beats(in InteractOfferChanged challenger, in InteractOfferChanged holder)
        {
            if (challenger.Source != holder.Source) return challenger.Source > holder.Source;
            if (challenger.DistanceSqr != holder.DistanceSqr) return challenger.DistanceSqr < holder.DistanceSqr;
            return string.CompareOrdinal(challenger.Id, holder.Id) < 0;
        }
    }
}
