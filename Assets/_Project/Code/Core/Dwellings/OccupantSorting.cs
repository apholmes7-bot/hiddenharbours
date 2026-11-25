using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>The one sort input for a body standing inside a parked dwelling (ADR 0060 section 5).</b>
    ///
    /// <para>The dwelling side (World, on the inside transitions <c>BuildingInterior</c> already computes)
    /// says who is inside and at what dwelling order; the body's own sort writer (Art, the walker's
    /// Y-sort) asks with <see cref="TryOrderFor"/> and, when it answers, writes that order instead of its
    /// Y-sort. Neither side names the other's classes (rule 4), and neither is a second writer of the
    /// other's order.</para>
    ///
    /// <para><b>One slot.</b> Only one body can be inside a dwelling under this rule (the player; a
    /// building's inside test watches one occupant), and he can be inside one dwelling at a time. The
    /// owner token is there for the frame he steps from one dwelling straight into another: the one he
    /// left may run its transition after the one he entered, and must not clear the newer entry.</para>
    ///
    /// <para>Not saved and not a <c>GameServices</c> member: it is recomputed from where he stands on
    /// every inside transition (ADR 0060 section 7). <see cref="Reset"/> is for teardown and tests.
    /// Allocation-free.</para>
    /// </summary>
    public static class OccupantSorting
    {
        private static object s_owner;
        private static Transform s_occupant;
        private static int s_order;

        /// <summary>True while some dwelling has a body inside it under this rule.</summary>
        public static bool IsOccupied => s_owner != null && s_occupant != null;

        /// <summary>
        /// <paramref name="occupant"/> is inside the dwelling <paramref name="owner"/>, which sorts at
        /// <paramref name="dwellingOrder"/>. Replaces any earlier entry: he is inside one dwelling at a time.
        /// A null owner or occupant clears nothing and records nothing.
        /// </summary>
        public static void Enter(object owner, Transform occupant, int dwellingOrder)
        {
            if (owner == null || occupant == null) return;
            s_owner = owner;
            s_occupant = occupant;
            s_order = DwellingSorting.OccupantOrder(dwellingOrder);
        }

        /// <summary>The dwelling <paramref name="owner"/> no longer has him inside. Clears the slot only if
        /// that dwelling holds it, so a late leave from the dwelling he left cannot undo the one he entered.</summary>
        public static void Leave(object owner)
        {
            if (owner == null || !ReferenceEquals(owner, s_owner)) return;
            Reset();
        }

        /// <summary>
        /// True, with the order to draw at, when <paramref name="body"/> is the body inside a dwelling; false
        /// to leave the body's own Y-sort. A destroyed occupant answers false (explicit Unity null check).
        /// </summary>
        public static bool TryOrderFor(Transform body, out int order)
        {
            if (body != null && s_owner != null && s_occupant != null && ReferenceEquals(body, s_occupant))
            {
                order = s_order;
                return true;
            }
            order = 0;
            return false;
        }

        /// <summary>Clear the slot (scene teardown, tests).</summary>
        public static void Reset()
        {
            s_owner = null;
            s_occupant = null;
            s_order = 0;
        }
    }
}
