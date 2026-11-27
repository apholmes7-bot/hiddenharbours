using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>The one contextual interact verb</b> — the Core dispatch that turns a single press into an action
    /// on exactly one registered <see cref="IInteractable"/>, and publishes what that candidate is so the
    /// world can show it without a screen-space prompt.
    ///
    /// <para><b>What this is FOR.</b> The dev key ledger is spent — A–Z are all claimed, across four
    /// different binding styles — so "add a key for it" is no longer an option for anything. This is the
    /// pressure valve: a feature that wants a button registers a candidate and gets the existing interact
    /// press, contextually, with no new binding and no new <c>Update()</c> reading the keyboard. The verb
    /// generalises the key the game already calls Interact; it does not introduce one.</para>
    ///
    /// <para><b>Three things can stop it, and they are different things.</b>
    /// <list type="bullet">
    ///   <item><see cref="InteractionGate"/> — a modal dialogue owns the key completely. Nothing world-side
    ///   may fire, verb included.</item>
    ///   <item><see cref="InteractActionClaim"/> — an older direct reader of the key has a target in range
    ///   (an NPC you are standing next to). The verb stands down for that press only; boarding does
    ///   not.</item>
    ///   <item>Nothing resolved — no candidate passed <see cref="InteractResolver"/>'s filters. This is the
    ///   ordinary case and the reason the seam is safe to land: with an empty registry the verb never
    ///   fires, so every INTERACT behaviour that predates it is reached exactly as before.</item>
    /// </list></para>
    ///
    /// <para><b>Why the verb is a PRESS and not a HOLD — the cast-latch lesson (#181), answered by
    /// construction.</b> <c>FishingController</c> starts a cast on the RISING EDGE of "held", so any verb
    /// that can finish while the key is still down must require a real release before it can arm again, or
    /// it re-triggers the cast bug that fix closed. This verb sidesteps that entire class of bug by never
    /// looking at a held state: its driver calls <see cref="TryPerform"/> once per <i>press edge</i>, so a
    /// key kept down produces exactly one action, and there is no latch to get wrong. That is a deliberate
    /// design constraint on the seam, not an accident of the first driver — <b>if a future candidate wants
    /// a hold</b> (a hold-to-pick, as Space already does on deck), the hold belongs INSIDE that candidate's
    /// own <see cref="IInteractable.Interact"/> implementation, which must then carry its own
    /// require-release latch exactly as <c>PotDeckWorkController</c> does. The verb must not grow a
    /// hold-aware overload that fires repeatedly; it is pinned by a test that a held key fires once.</para>
    /// </summary>
    public static class InteractVerb
    {
        /// <summary>The candidate the verb would act on right now, or null. Mirrors the last
        /// <see cref="InteractCandidateChanged"/> published, so a late subscriber (or a test) can read the
        /// current state instead of waiting for the next change.</summary>
        public static string CurrentCandidateId { get; private set; }

        /// <summary>
        /// Resolve and act — the whole press, in one call.
        ///
        /// <para>Returns false when the press was not spent, which the caller must treat as "carry on":
        /// the driver's own pre-existing interact behaviours run in that case. Returns true when exactly
        /// one candidate was acted on; <see cref="InteractPerformed"/> is published after the action, so a
        /// listener sees the world as the action left it.</para>
        /// </summary>
        public static bool TryPerform(in InteractActor actor, float facingArcDegrees)
        {
            if (InteractionGate.IsBlocked) return false;
            if (InteractActionClaim.IsClaimed) return false;
            if (!InteractResolver.TryResolveNow(actor, facingArcDegrees, out IInteractable best)) return false;

            return Perform(best, -1, actor);
        }

        /// <summary>
        /// <b>The player's press</b> (owner ask 2026-11-15) — what the switcher calls on E.
        ///
        /// <para>With one thing to act on, exactly <see cref="TryPerform"/>'s answer, and exactly as
        /// before — except that an occupied bunk with a free one in reach is swapped for the free one
        /// (<see cref="InteractChoiceList.Default"/>). With two or more pickable actions in reach (and
        /// <see cref="GameConfig.InteractChooserEnabled"/> on), it opens <see cref="InteractChooser"/>
        /// instead and spends the press on that; the action happens on the chooser's confirm.</para>
        ///
        /// <para><see cref="TryPerform"/> stays the direct, chooser-less resolution: the single-candidate
        /// path the fixtures and journeys drive with a hand-built actor, and the ladder's answer.</para>
        /// </summary>
        public static bool TryPress(in InteractActor actor, float facingArcDegrees)
        {
            if (InteractionGate.IsBlocked) return false;
            if (InteractActionClaim.IsClaimed) return false;
            // The press that just picked from (or put down) the list is still "pressed this frame".
            if (InteractChooser.ClosedThisFrame) return false;

            BuildFor(PressList, actor, facingArcDegrees);
            IInteractable target = PressList.Default;
            if (target == null) return false;

            if (ChooserEnabled() && PressList.IsChoice && InteractChooser.Open(PressList, actor, facingArcDegrees))
                return true;

            return Perform(target, -1, actor);
        }

        /// <summary>
        /// Act on one thing — the single body every press path ends in (<see cref="TryPerform"/>,
        /// <see cref="TryPress"/>, the chooser's confirm). An <see cref="INeedsGated"/> thing whose bar the
        /// player does not meet is refused out loud (<see cref="NeedsGate.TryRefuse"/>) and not acted on;
        /// otherwise the action runs and <see cref="InteractPerformed"/> follows it.
        /// </summary>
        /// <param name="action">An <see cref="IMultiActionInteractable"/> action index, or -1 for the plain
        /// <see cref="IInteractable.Interact"/>.</param>
        public static bool Perform(IInteractable target, int action, in InteractActor actor)
        {
            if (target == null || !RoomFixtureReach.Allows(target, actor)) return false;
            if (target is INeedsGated gated && NeedsGate.TryRefuse(gated.NeedsRequirement)) return true;

            if (action >= 0 && target is IMultiActionInteractable multi) multi.Interact(actor, action);
            else target.Interact(actor);
            EventBus.Publish(new InteractPerformed(target.Id));
            return true;
        }

        // The press's scratch list. The chooser copies what it opens on, so this is free to be rebuilt.
        private static readonly InteractChoiceList PressList = new InteractChoiceList();

        private static void BuildFor(InteractChoiceList list, in InteractActor actor, float facingArcDegrees)
        {
            GameConfig config = GameServices.Config;
            int maxPriority = config != null ? config.InteractChooserMaxPriority
                                             : GameConfig.DefaultInteractChooserMaxPriority;
            int maxRows = config != null ? config.InteractChooserMaxRows : GameConfig.DefaultInteractChooserMaxRows;
            list.Build(Interactables.Active, actor, facingArcDegrees, maxPriority, maxRows);
        }

        private static bool ChooserEnabled()
        {
            GameConfig config = GameServices.Config;
            return config != null ? config.InteractChooserEnabled : GameConfig.DefaultInteractChooserEnabled;
        }

        /// <summary>
        /// Recompute the current candidate and, <b>only if it changed</b>, publish
        /// <see cref="InteractCandidateChanged"/> for the diegetic highlight.
        ///
        /// <para>Driven every frame by the same component that owns the press, so the highlight is the
        /// truth about what the press would do rather than a second opinion about it. The resolve itself
        /// is an allocation-free index walk over a short list (rule 7); the PUBLISH is edge-triggered, so
        /// standing still costs one comparison a frame and no event traffic.</para>
        ///
        /// <para>Honours the same two stand-downs as <see cref="TryPerform"/>: if the press could not
        /// reach a candidate, nothing is highlighted. A highlight that outlives its own actionability is
        /// worse than none — it teaches the player a lie.</para>
        /// </summary>
        public static void PublishCandidate(in InteractActor actor, float facingArcDegrees)
        {
            string id = null;
            IInteractable best = null;
            if (!InteractionGate.IsBlocked && !InteractActionClaim.IsClaimed)
            {
                // The same default the press acts on — the resolver's winner, with the free bunk swapped
                // in for a taken one. Rows are not built here: the highlight needs one answer, not a list.
                if (InteractChoiceList.TryResolveDefault(Interactables.Active, actor, facingArcDegrees, out best))
                    id = best.Id;
            }

            SetCandidate(id);
            OfferCandidate(actor, best);
        }

        /// <summary>Drop the candidate (and publish the change, if there was one). Called by the driver
        /// whenever it is not in a position to offer the verb at all — paused, mid-boarding-move, disabled
        /// — so a highlight can never be left burning on a thing you can no longer act on.</summary>
        public static void ClearCandidate()
        {
            SetCandidate(null);
            InteractOffer.Clear(InteractOfferSource.Fixture);
        }

        /// <summary>Clear the verb's own state (scene teardown / test isolation). Does NOT touch the
        /// registry — <see cref="Interactables.Clear"/> is its own call, so a fixture can reset one without
        /// the other. It DOES release the verb's offer slot, because a stale line in the popup naming a
        /// fixture nobody can reach is exactly the lie the highlight rules forbid.</summary>
        public static void Reset()
        {
            InteractChooser.Reset();
            PressList.Clear();
            CurrentCandidateId = null;
            InteractOffer.Clear(InteractOfferSource.Fixture);
        }

        /// <summary>
        /// State the verb's half of the interaction popup — the same resolved candidate, said in words
        /// (<see cref="IInteractable.VerbLabel"/>) on the <see cref="InteractOfferSource.Fixture"/> slot.
        ///
        /// <para><b>The resolve is not repeated.</b> It rides the answer <see cref="PublishCandidate"/> has
        /// already computed this frame, which is the whole point of the seam: the popup, the highlight and
        /// the press are three views of ONE resolution, never three opinions about it.</para>
        ///
        /// <para>A candidate with no label offers nothing — see <see cref="IInteractable.VerbLabel"/>. It
        /// still resolves and still acts; the player is simply not told, which is the honest state for a
        /// registrant that has not been given words yet.</para>
        /// </summary>
        private static void OfferCandidate(in InteractActor actor, IInteractable best)
        {
            if (best == null) { InteractOffer.Clear(InteractOfferSource.Fixture); return; }

            Vector2 at = best.WorldPosition;
            InteractOffer.Set(InteractOfferSource.Fixture, best.Id, best.VerbLabel, at,
                              (at - actor.Position).sqrMagnitude);
        }

        private static void SetCandidate(string id)
        {
            if (string.Equals(CurrentCandidateId, id, System.StringComparison.Ordinal)) return;
            CurrentCandidateId = id;
            EventBus.Publish(new InteractCandidateChanged(id));
        }
    }
}
