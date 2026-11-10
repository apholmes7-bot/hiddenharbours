using System.Collections.Generic;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// The <b>registry of boat holds</b> in the active region — how a buyer ashore finds the catch
    /// lying in a boat nobody wired to its counter.
    ///
    /// <para><b>What it is FOR.</b> A fish landed from the deck goes into the boat's hold, and a
    /// counter sells the one hold a builder serialized into it. At the island store that is the pail,
    /// so the boat's catch had no buyer at all. This is the seam that lets a counter ask "is there a
    /// boat with a catch in her lying at the wharf?" without knowing what a boat is.</para>
    ///
    /// <para><b>A registry, not a <see cref="GameServices"/> slot</b>, for the reason
    /// <see cref="HullPresences"/> and <see cref="MooringCleats"/> are: a hold comes and goes with its
    /// component, a region hop toggles it, and no installer should have to know about it. Same
    /// register-on-enable / relinquish-on-disable contract, and the same reading of an empty registry —
    /// <b>no boat here is a valid answer</b>, under which every asker behaves exactly as it did before
    /// this seam existed.</para>
    ///
    /// <para>A plain list walked by index: no allocation, no enumerator, no hashing (rule 7). It is
    /// read at event time only (a sale), never per frame.</para>
    /// </summary>
    public static class BoatHolds
    {
        // The player sails one boat at a time; the list is here so a second hull is a second entry
        // rather than a second seam.
        private static readonly List<IBoatHold> Holds = new List<IBoatHold>(2);

        /// <summary>How many boat holds are registered right now (0 = no boat here).</summary>
        public static int Count => Holds.Count;

        /// <summary>The live registry, for a consumer driving a pure overload with it. Read-only: add
        /// and remove through <see cref="Register"/> / <see cref="Unregister"/>.</summary>
        public static IReadOnlyList<IBoatHold> Active => Holds;

        /// <summary>Add a hold (a registrant's <c>OnEnable</c>). Null and double registration are
        /// no-ops — one boat registered twice would be offered to a buyer twice.</summary>
        public static void Register(IBoatHold hold)
        {
            if (hold == null || Holds.Contains(hold)) return;
            Holds.Add(hold);
        }

        /// <summary>Remove a hold (a registrant's <c>OnDisable</c>). Unregistered is a no-op.</summary>
        public static void Unregister(IBoatHold hold)
        {
            if (hold == null) return;
            Holds.Remove(hold);
        }

        /// <summary>Empty the registry (scene teardown / test isolation).</summary>
        public static void Clear() => Holds.Clear();
    }
}
