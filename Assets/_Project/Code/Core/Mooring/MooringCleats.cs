using System.Collections.Generic;
using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// The <b>registry of tie-off points</b> in the active region — every wharf bollard and every hull's
    /// rig cleats at once — plus the one lookup the rope gameplay makes: "what is the nearest cleat on the
    /// OTHER side of the water?".
    ///
    /// <para><b>A registry, not a <see cref="GameServices"/> slot</b>, for exactly the reason
    /// <see cref="StandableSurfaces"/> is one: there are many at a time, they appear and disappear with
    /// their components, and no installer should have to know about them. Same shape, same scene-scoped
    /// register-on-enable / relinquish-on-disable contract, same "empty registry = nothing to tie to,
    /// which is a valid answer rather than a fault".</para>
    ///
    /// <para><b>Pure &amp; deterministic (rule 5).</b> Every query takes its list explicitly and has a
    /// <c>Now</c> twin that reads the live registrants, so the whole selection rule is EditMode-testable
    /// with plain doubles and no scene. The walk is by index with no allocation, no enumerator and no
    /// hashing (rule 7) — the list is small by construction (one wharf's bollards plus the cleats of the
    /// hulls actually loaded).</para>
    /// </summary>
    public static class MooringCleats
    {
        // A handful of wharf fittings plus one hull's cleats is the realistic size; a plain list walked by
        // index is both the cheapest and the most predictable thing here.
        private static readonly List<IMooringCleat> Cleats = new List<IMooringCleat>(16);

        /// <summary>How many cleats are registered right now (0 = nothing in this region can be tied to).</summary>
        public static int Count => Cleats.Count;

        /// <summary>The live registry, for a consumer driving a pure overload with it (and for
        /// diagnostics). Read-only: add and remove through <see cref="Register"/> /
        /// <see cref="Unregister"/> so the two can never diverge.</summary>
        public static IReadOnlyList<IMooringCleat> Active => Cleats;

        /// <summary>Add a cleat (a registrant's <c>OnEnable</c>). Null and double registration are no-ops —
        /// a component enabled twice without a disable between must not appear twice, or a toss could
        /// resolve onto the same fitting from two entries.</summary>
        public static void Register(IMooringCleat cleat)
        {
            if (cleat == null || Cleats.Contains(cleat)) return;
            Cleats.Add(cleat);
        }

        /// <summary>Remove a cleat (a registrant's <c>OnDisable</c>). Unregistered is a no-op — a teardown
        /// must never have to check first.</summary>
        public static void Unregister(IMooringCleat cleat)
        {
            if (cleat == null) return;
            Cleats.Remove(cleat);
        }

        /// <summary>Empty the registry (scene teardown / test isolation). A leaked registration would let a
        /// line be made fast to a wharf that is no longer loaded, so tests clear this in their fixture.</summary>
        public static void Clear() => Cleats.Clear();

        // ---- the lookups ------------------------------------------------------------------------

        /// <summary>
        /// The nearest cleat on <paramref name="side"/> within <paramref name="maxRadiusMetres"/> of
        /// <paramref name="worldPos"/>, measured in the horizontal plane. Used twice, for two different
        /// questions, which is why it takes both a side and a radius:
        /// <list type="bullet">
        ///   <item><b>"Which cleat am I standing at?"</b> — small radius, the side the player is on.</item>
        ///   <item><b>"Did the toss catch?"</b> — the catch radius, the OPPOSING side, measured from where
        ///   the line landed.</item>
        /// </list>
        /// Horizontal distance only: a toss is aimed across the water, and the vertical gap is the tide's
        /// business (<see cref="MooringLineMath"/>), not the throw's. Pure — the caller supplies the list,
        /// so tests need no registry. Allocation-free.
        /// </summary>
        public static bool TryFindNearest(IReadOnlyList<IMooringCleat> cleats, Vector2 worldPos,
                                          CleatSide side, float maxRadiusMetres, out IMooringCleat nearest)
        {
            nearest = null;
            if (cleats == null) return false;

            float limit = Mathf.Max(0f, maxRadiusMetres);
            float bestSqr = limit * limit;
            for (int i = 0; i < cleats.Count; i++)
            {
                IMooringCleat c = cleats[i];
                if (c == null || c.Side != side) continue;
                float sqr = (c.WorldPosition - worldPos).sqrMagnitude;
                if (sqr > bestSqr) continue;
                bestSqr = sqr;
                nearest = c;
            }
            return nearest != null;
        }

        /// <summary>The <c>Now</c> twin of <see cref="TryFindNearest"/>, over the live registrants — the
        /// one entry point the runtime rope controller calls.</summary>
        public static bool TryFindNearestNow(Vector2 worldPos, CleatSide side, float maxRadiusMetres,
                                             out IMooringCleat nearest)
            => TryFindNearest(Cleats, worldPos, side, maxRadiusMetres, out nearest);

        /// <summary>The cleat with this id among <paramref name="cleats"/>, or null. Exists for restoring a
        /// made-fast line after a load, where the ids are what survived and the instances are new.</summary>
        public static IMooringCleat FindById(IReadOnlyList<IMooringCleat> cleats, string id)
        {
            if (cleats == null || string.IsNullOrEmpty(id)) return null;
            for (int i = 0; i < cleats.Count; i++)
            {
                IMooringCleat c = cleats[i];
                if (c != null && c.Id == id) return c;
            }
            return null;
        }

        /// <summary>The <c>Now</c> twin of <see cref="FindById"/>, over the live registrants.</summary>
        public static IMooringCleat FindByIdNow(string id) => FindById(Cleats, id);
    }
}
