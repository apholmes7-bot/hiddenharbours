using System.Collections.Generic;
using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>Which single thing does this press belong to?</b> — the whole selection rule of the one interact
    /// verb, as a pure function of the candidates and where the actor is standing.
    ///
    /// <para><b>Pure and total, on purpose.</b> No input device, no components, no scene, no time, no
    /// randomness — feed it a list and an <see cref="InteractActor"/> and it returns the same candidate
    /// every time (rule 5). That is what makes M2-39's central claim — "exactly one candidate" — provable
    /// headless in EditMode, which is the only place it can be proved by an agent with no editor.</para>
    ///
    /// <para><b>The order of business.</b> Filters first, then a ranking. A candidate must pass ALL of:
    /// <list type="number">
    ///   <item><b>Context</b> — the actor is somewhere the candidate declares it can be worked from.</item>
    ///   <item><b>Availability</b> — the candidate's own gate says yes.</item>
    ///   <item><b>Reach</b> — horizontal distance ≤ the candidate's own <see cref="IInteractable.ReachMeters"/>.</item>
    ///   <item><b>Arc</b> — if the candidate <see cref="IInteractable.RequiresFacing"/> and the actor's
    ///   facing is known, it lies within <paramref name="facingArcDegrees"/> centred on that facing. The
    ///   test itself is <see cref="InteractArc"/>, shared with the conversation filter.</item>
    /// </list>
    /// Survivors are then ranked, and the winner is the first of these to differ:
    /// <list type="number">
    ///   <item><b>Higher <see cref="IInteractable.Priority"/></b> — a thing in your hands beats a thing at
    ///   your feet beats a thing bolted to the wharf, whatever the distances are.</item>
    ///   <item><b>Nearer</b> (squared horizontal distance).</item>
    ///   <item><b>Lower <see cref="IInteractable.Id"/> by ordinal comparison.</b></item>
    /// </list></para>
    ///
    /// <para><b>Why the id is the last rung, and why the distances are compared exactly.</b> Those three
    /// rules together are a <i>total</i> order over candidates with distinct ids, and the single-pass scan
    /// below finds the maximum of a total order — which is the same answer whatever order the list is in.
    /// So the winner cannot depend on which component happened to enable first. That property is fragile
    /// in one specific way and it is worth naming: a <i>tolerance</i> on the distance comparison would
    /// destroy it, because "within ε" is not transitive (A ties B, B ties C, yet A beats C), and a
    /// non-transitive comparator makes a single-pass scan order-dependent again. So distances are compared
    /// bit-exactly and the id — which is exact — is what actually breaks ties. Duplicate ids are the one
    /// hole, and they are an authoring error (see <see cref="IInteractable.Id"/>); with duplicates, the
    /// earlier registrant is kept.</para>
    ///
    /// <para><b>Allocation-free (rule 7).</b> Walked by index, no enumerator, no LINQ, no temporaries — it
    /// runs once per frame to keep the highlight honest, not just on the press.</para>
    /// </summary>
    public static class InteractResolver
    {
        /// <summary>
        /// The default arc width in degrees — the full angle, centred on the actor's facing, so 180° means
        /// "the half-plane you are looking at" (±90°). Chosen forgiving (P5 cozy) and to suit the fisher's
        /// FOUR-way facing: at ±90° two adjacent facings overlap exactly on the diagonal, so a candidate
        /// off your bow-quarter is reachable whichever of the two you happen to be drawn in, rather than
        /// falling into a dead wedge between them. Callers pass their own tunable; this is the documented
        /// default, not a hidden constant (rule 6).
        /// </summary>
        public const float DefaultFacingArcDegrees = 180f;

        // The arc test itself — the same-spot guard, the unknown-facing rule and the un-normalised angle
        // comparison — lives in InteractArc, because WorldInteractor now asks the identical question of
        // its conversation partners and two hand-written copies of an angle test is one too many.

        /// <summary>
        /// The one selection rule, over a caller-supplied list. Returns false — with
        /// <paramref name="best"/> null — when nothing qualifies, which is the ordinary case and not a
        /// fault.
        /// </summary>
        /// <param name="candidates">The candidates to consider. Null is treated as empty; null entries are
        /// skipped.</param>
        /// <param name="actor">Where the actor is, which way they face, and which place they are in.</param>
        /// <param name="facingArcDegrees">Full arc width in degrees for candidates that require facing.
        /// Clamped to [0, 360]; at 360 the arc test cannot exclude anything.</param>
        /// <param name="best">The single winning candidate, or null.</param>
        public static bool TryResolve(IReadOnlyList<IInteractable> candidates, in InteractActor actor,
                                      float facingArcDegrees, out IInteractable best)
        {
            best = null;
            if (candidates == null || candidates.Count == 0) return false;

            // The half-angle as a cosine, computed ONCE for the whole scan — never per candidate, which
            // would be a Mathf.Cos on a hot path for no gain (rule 7). A facing of exactly zero means
            // "unknown", and InteractArc then passes everything (see InteractActor.Facing).
            float cosHalfArc = InteractArc.CosHalfArc(facingArcDegrees);
            Vector2 facing = actor.Facing;

            int bestPriority = 0;
            float bestSqr = 0f;

            for (int i = 0; i < candidates.Count; i++)
            {
                IInteractable c = candidates[i];
                if (c == null) continue;

                // 1) Context — is the actor even somewhere this can be worked from?
                if ((c.Contexts & actor.Context) == 0) continue;

                // 2) The candidate's own gate.
                if (!c.IsAvailable) continue;

                // 3) Reach, horizontally. A negative reach is treated as zero rather than as an error:
                //    an un-tuned registrant should be unreachable, never universally reachable.
                Vector2 delta = c.WorldPosition - actor.Position;
                float sqr = delta.sqrMagnitude;
                float reach = Mathf.Max(0f, c.ReachMeters);
                if (sqr > reach * reach) continue;

                // 4) The forward arc, for candidates that ask for it — InteractArc.Contains, which is the
                //    same rule WorldInteractor's conversation filter runs, stated once.
                if (c.RequiresFacing && !InteractArc.Contains(delta, facing, cosHalfArc)) continue;

                if (best == null || Beats(c, c.Priority, sqr, best, bestPriority, bestSqr))
                {
                    best = c;
                    bestPriority = c.Priority;
                    bestSqr = sqr;
                }
            }

            return best != null;
        }

        /// <summary>The <c>Now</c> twin of <see cref="TryResolve"/>, over the live
        /// <see cref="Interactables"/> registrants — the one entry point the runtime driver calls.</summary>
        public static bool TryResolveNow(in InteractActor actor, float facingArcDegrees,
                                         out IInteractable best)
            => TryResolve(Interactables.Active, actor, facingArcDegrees, out best);

        /// <summary>
        /// The ranking, spelled out once: higher priority, then nearer, then lower id by ordinal. Exposed
        /// so a test can assert the ladder directly rather than only through a resolved scene.
        /// </summary>
        public static bool Beats(IInteractable challenger, int challengerPriority, float challengerSqr,
                                 IInteractable holder, int holderPriority, float holderSqr)
        {
            if (challengerPriority != holderPriority) return challengerPriority > holderPriority;
            if (challengerSqr != holderSqr) return challengerSqr < holderSqr;
            // Ordinal, not culture-aware: a tie-break that changes with the machine's locale is not a
            // tie-break. Equal ids (an authoring error) keep the incumbent.
            return string.CompareOrdinal(challenger?.Id, holder?.Id) < 0;
        }
    }
}
