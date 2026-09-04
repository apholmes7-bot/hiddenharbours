using System.Collections.Generic;
using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// The <b>registry of hulls on the water</b> in the active region, plus the one lookup the wading
    /// model makes of it: <b>"am I alongside a boat?"</b>.
    ///
    /// <para><b>What it is FOR (the owner's 2026-09-02 words).</b> <i>"For now a player should be able
    /// to swim up to a hull and climb aboard anywhere."</i> The ratified water-travel model says water
    /// travel is boats only and soft-walls a person out of anything deeper than
    /// <c>GameConfig.SwimLimit</c>; a boat lies in exactly that water, so the wall was also the thing
    /// standing between a swimmer and the gunwale she is supposed to be able to reach. This registry is
    /// how the water finds out a hull is there, and <c>GameConfig.SwimBoardReachMetres</c> is how far
    /// her presence carries. <b>Everywhere else the wall stands</b> — the relaxation is deliberately the
    /// narrowest one that satisfies his sentence, and open-water swimming remains a ruling nobody has
    /// made.</para>
    ///
    /// <para><b>A registry, not a <see cref="GameServices"/> slot</b>, for the reason
    /// <see cref="BoardingLadders"/>, <see cref="MooringCleats"/> and <see cref="StandableSurfaces"/>
    /// are: there are several at once, they come and go with their components, and no installer should
    /// have to know about them. Same scene-scoped register-on-enable / relinquish-on-disable contract,
    /// and the same reading of an empty registry — <b>a region with no boats in it is a valid region</b>,
    /// under which every lookup below is bit-identical to the plain "no lift" answer that predates this
    /// seam.</para>
    ///
    /// <para><b>Pure &amp; deterministic (rule 5).</b> Every lookup takes its list explicitly and has a
    /// <c>Now</c> twin over the live registrants, so the whole rule is EditMode-testable with no scene
    /// and no boat. Walked by index — no allocation, no enumerator, no hashing (rule 7); a harbour
    /// carries a handful of hulls and this is read once per fixed tick.</para>
    /// </summary>
    public static class HullPresences
    {
        // A working basin holds a handful at once. A plain list walked by index is the cheapest and the
        // most predictable thing at this size, and the query runs on the player's FixedUpdate.
        private static readonly List<IHullPresence> Hulls = new List<IHullPresence>(8);

        /// <summary>How many hulls are registered right now (0 = no boats here, which is a valid answer
        /// and not a fault — see the class doc).</summary>
        public static int Count => Hulls.Count;

        /// <summary>The live registry, for a consumer driving the pure overloads with it (and for
        /// diagnostics). Read-only: add and remove through <see cref="Register"/> /
        /// <see cref="Unregister"/> so the two can never diverge.</summary>
        public static IReadOnlyList<IHullPresence> Active => Hulls;

        /// <summary>Add a hull (a registrant's <c>OnEnable</c>). Null and double registration are no-ops
        /// — a component enabled twice without a disable between must not appear twice, or one boat
        /// would be measured as two.</summary>
        public static void Register(IHullPresence hull)
        {
            if (hull == null || Hulls.Contains(hull)) return;
            Hulls.Add(hull);
        }

        /// <summary>Remove a hull (a registrant's <c>OnDisable</c>). Unregistered is a no-op — a teardown
        /// must never have to check first.</summary>
        public static void Unregister(IHullPresence hull)
        {
            if (hull == null) return;
            Hulls.Remove(hull);
        }

        /// <summary>Empty the registry (scene teardown / test isolation). A leaked registration would
        /// hold open a hole in the boat-only wall around a boat that is no longer there, so tests clear
        /// this in their fixture.</summary>
        public static void Clear() => Hulls.Clear();

        // ---- the lookups ----------------------------------------------------------------------------

        /// <summary>
        /// Metres from <paramref name="worldPos"/> to the nearest hull's OUTLINE — <b>0 inside one</b>,
        /// and <see cref="float.PositiveInfinity"/> when no hull is registered at all.
        ///
        /// <para><b>To the outline, never to the root</b> — the law this arc has already paid for twice
        /// (<see cref="HullFootprint"/>'s own note, and the boarding gate before it). A 12.9 m cape
        /// islander measured to her origin is "6 m away" from a swimmer holding on to her quarter, and a
        /// 4.5 m dory measured the same way is "alongside" from open water off her bow. One number, two
        /// opposite wrong answers.</para>
        ///
        /// <para>Pure — the caller supplies the list, so tests need no registry. Allocation-free; null
        /// entries are skipped rather than thrown on, because a registrant destroyed mid-frame must not
        /// take the wading model down with it.</para>
        /// </summary>
        public static float DistanceToNearestOutline(IReadOnlyList<IHullPresence> hulls, Vector2 worldPos)
        {
            float best = float.PositiveInfinity;
            if (hulls == null) return best;

            for (int i = 0; i < hulls.Count; i++)
            {
                IHullPresence hull = hulls[i];
                if (hull == null) continue;
                float d = hull.Footprint.DistanceTo(worldPos);
                if (d < best) best = d;
                if (best <= 0f) return 0f;      // inside a hull: nothing can beat it
            }
            return best;
        }

        /// <summary>
        /// Is <paramref name="worldPos"/> within <paramref name="reachMetres"/> of some hull's outline?
        ///
        /// <para>A reach of zero or less, or one that is not a finite number, is <b>false</b> rather than
        /// "touching counts": this predicate opens a hole in the owner's boat-only wall, and a
        /// mis-authored tunable must close it, never open it everywhere.</para>
        /// </summary>
        public static bool WithinReachOf(IReadOnlyList<IHullPresence> hulls, Vector2 worldPos,
                                         float reachMetres)
        {
            if (!(reachMetres > 0f) || float.IsInfinity(reachMetres)) return false;
            return DistanceToNearestOutline(hulls, worldPos) <= reachMetres;
        }

        /// <summary>Live twin of <see cref="DistanceToNearestOutline"/> over the registered hulls.</summary>
        public static float DistanceToNearestOutlineNow(Vector2 worldPos)
            => DistanceToNearestOutline(Hulls, worldPos);

        /// <summary>Live twin of <see cref="WithinReachOf"/> over the registered hulls — the one the
        /// walk model asks each tick.</summary>
        public static bool WithinReachNow(Vector2 worldPos, float reachMetres)
            => WithinReachOf(Hulls, worldPos, reachMetres);
    }
}
