using System.Collections.Generic;
using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// The <b>registry of wharf ladders</b> in the active region, plus the one lookup the boarding move
    /// makes: "is there a ladder within reach of where I am trying to get aboard?".
    ///
    /// <para><b>A registry, not a <see cref="GameServices"/> slot</b>, for exactly the reason
    /// <see cref="MooringCleats"/> and <see cref="StandableSurfaces"/> are: there are several at a time,
    /// they appear and disappear with their components, and no installer should have to know about them.
    /// Same scene-scoped register-on-enable / relinquish-on-disable contract, and the same reading of an
    /// empty registry — <b>a wharf with no ladder is a valid wharf</b>, not a fault. Where the gap is too
    /// big and there is no ladder, boarding falls back to the soft-clamped step exactly as it does on
    /// <c>main</c> today; the tide simply has no answer to offer there.</para>
    ///
    /// <para><b>Pure &amp; deterministic (rule 5).</b> The lookup takes its list explicitly and has a
    /// <c>Now</c> twin over the live registrants, so the selection rule is EditMode-testable with no
    /// scene. Walked by index — no allocation, no enumerator, no hashing (rule 7); one wharf carries a
    /// ladder or two.</para>
    /// </summary>
    public static class BoardingLadders
    {
        // A community wharf carries one or two; a big commercial quay a handful. A plain list walked by
        // index is both the cheapest and the most predictable thing at this size.
        private static readonly List<IBoardingLadder> Ladders = new List<IBoardingLadder>(4);

        /// <summary>How many ladders are registered right now (0 = this region has none, which is a valid
        /// answer — see the class doc).</summary>
        public static int Count => Ladders.Count;

        /// <summary>The live registry, for a consumer driving the pure overload with it (and for
        /// diagnostics). Read-only: add and remove through <see cref="Register"/> /
        /// <see cref="Unregister"/> so the two can never diverge.</summary>
        public static IReadOnlyList<IBoardingLadder> Active => Ladders;

        /// <summary>Add a ladder (a registrant's <c>OnEnable</c>). Null and double registration are no-ops
        /// — a component enabled twice without a disable between must not appear twice, or the nearest
        /// lookup would consider the same rungs from two entries.</summary>
        public static void Register(IBoardingLadder ladder)
        {
            if (ladder == null || Ladders.Contains(ladder)) return;
            Ladders.Add(ladder);
        }

        /// <summary>Remove a ladder (a registrant's <c>OnDisable</c>). Unregistered is a no-op — a teardown
        /// must never have to check first.</summary>
        public static void Unregister(IBoardingLadder ladder)
        {
            if (ladder == null) return;
            Ladders.Remove(ladder);
        }

        /// <summary>Empty the registry (scene teardown / test isolation). A leaked registration would let a
        /// fisher climb down a wharf that is no longer loaded, so tests clear this in their fixture.</summary>
        public static void Clear() => Ladders.Clear();

        // ---- the lookup -----------------------------------------------------------------------------

        /// <summary>
        /// The nearest ladder within <paramref name="maxRadiusMetres"/> of <paramref name="worldPos"/>,
        /// measured in the horizontal plane.
        ///
        /// <para>Horizontal only, deliberately: the whole point of the query is that the VERTICAL distance
        /// is the thing the ladder exists to cross, so counting it would refuse the deepest gaps — the very
        /// ones that need a ladder most.</para>
        ///
        /// <para>Pure — the caller supplies the list, so tests need no registry. Allocation-free.</para>
        /// </summary>
        public static bool TryFindNearest(IReadOnlyList<IBoardingLadder> ladders, Vector2 worldPos,
                                          float maxRadiusMetres, out IBoardingLadder nearest)
        {
            nearest = null;
            if (ladders == null) return false;

            float limit = Mathf.Max(0f, maxRadiusMetres);
            float bestSqr = limit * limit;

            for (int i = 0; i < ladders.Count; i++)
            {
                IBoardingLadder l = ladders[i];
                if (l == null) continue;
                float sqr = (l.WorldPosition - worldPos).sqrMagnitude;
                if (sqr > bestSqr) continue;
                bestSqr = sqr;
                nearest = l;
            }
            return nearest != null;
        }

        /// <summary>The nearest ladder among the LIVE registrants (the <c>Now</c> twin of
        /// <see cref="TryFindNearest(IReadOnlyList{IBoardingLadder},Vector2,float,out IBoardingLadder)"/>).</summary>
        public static bool TryFindNearestNow(Vector2 worldPos, float maxRadiusMetres,
                                             out IBoardingLadder nearest)
            => TryFindNearest(Ladders, worldPos, maxRadiusMetres, out nearest);
    }
}
