using System.Collections.Generic;
using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Economy
{
    /// <summary>
    /// <b>THE CATCH IN THE BOAT AT THE WHARF</b> — which boat hold, if any, a counter may take along
    /// with the hold it was wired to.
    ///
    /// <para><b>Why it exists.</b> A fish landed from the deck goes into the boat's hold. A counter
    /// sells the one hold a builder serialized into it, and at the island store that is the pail — so
    /// a catch made from the dory had no buyer anywhere on the island. This is the rule that lets the
    /// counter see it: <b>a boat lying at a wharf has landed her catch</b>, and the person behind the
    /// counter takes it with whatever else you brought.</para>
    ///
    /// <para><b>"At a wharf" is measured, not declared.</b> She counts as berthed when her root lies
    /// within <see cref="ReachMeters"/> of a SHORE-side fitting in <see cref="MooringCleats"/> — a
    /// bollard, a quay cleat, a float cleat. That is the same registry the mooring line ties to, so
    /// "where you can make fast" and "where your catch is landed" cannot drift apart, and a region
    /// with no wharf fittings simply never lands a boat's catch. It deliberately does not ask whether
    /// a line is made fast: the painter dropped on the planks is berthed enough for a storekeeper.</para>
    ///
    /// <para><b>No new sell economics.</b> This only CHOOSES a hold. The sale is the same
    /// <see cref="FishBuyer"/> at the same <see cref="Market"/> through the same
    /// <see cref="SellService"/> the counter has always used, so every price is the outlet's own
    /// data-driven price and the glut carries from the pail into the boat's lot.</para>
    ///
    /// <para><b>Pure &amp; deterministic (rule 5).</b> Every lookup takes its lists and its reach
    /// explicitly and has a <c>Now</c> twin over the live registries, so the whole rule is
    /// EditMode-testable with no scene. Positions and a distance — no clock, no randomness. Read at
    /// event time only (a sale), never per frame (rule 7).</para>
    /// </summary>
    public static class BerthedCatch
    {
        /// <summary>The greybox line shown when a counter sale leaves a catch aboard because the boat
        /// is not at a wharf. Public so a test asserts the very string the player is shown.</summary>
        public const string NoticeBoatNotBerthed =
            "Your boat's not at the wharf — the catch in her stays aboard.";

        /// <summary>
        /// How near a shore fitting she must lie (m) — the owner's
        /// <see cref="GameConfig.CounterBerthReachMeters"/>, resolved per read so a slider dragged in
        /// play changes the next sale. Unwired (EditMode, a bare scene) it is the code default.
        /// <para>⚠ <c>!= null</c>, never <c>?.</c>: the config is a <c>UnityEngine.Object</c>.</para>
        /// </summary>
        public static float ReachMeters => GameServices.Config != null
            ? GameServices.Config.CounterBerthReachMeters
            : GameConfig.DefaultCounterBerthReachMeters;

        /// <summary>
        /// Is a boat at <paramref name="boatWorldPos"/> lying within <paramref name="reachMeters"/> of
        /// a shore-side fitting?
        ///
        /// <para>A reach of zero or less, or one that is not a finite number, is <b>false</b>: this
        /// predicate lets a buyer take a catch it cannot see, and a mis-authored number must close
        /// that, never open it across the whole region. Zero is also the owner's off switch.</para>
        /// </summary>
        public static bool IsBerthed(Vector2 boatWorldPos, IReadOnlyList<IMooringCleat> cleats,
                                     float reachMeters)
        {
            if (!(reachMeters > 0f) || float.IsInfinity(reachMeters)) return false;
            return MooringCleats.TryFindNearest(cleats, boatWorldPos, CleatSide.Shore, reachMeters, out _);
        }

        /// <summary>
        /// The boat hold a counter may take along with <paramref name="wired"/>, or null.
        ///
        /// <para>It is the first registered boat hold that (a) is not the wired hold itself, (b) has a
        /// catch aboard and (c) is berthed. Registry order, so the answer is the same every time for
        /// the same world — and an empty boat at the wharf never hides a laden one behind her.</para>
        ///
        /// <para><b>⚠ "Not the wired hold itself" is asked of the CATCH, not of the object.</b> A
        /// region's counter can be wired to a stand-in that forwards to the boat's hold (the travel
        /// proxy at Nine Mile Creek). The stand-in is a different object, so a reference check on the
        /// holds would pass — and the same fish would be sold twice. Both hand back the one list the
        /// catch lives in, so that list is what is compared.</para>
        /// </summary>
        public static IBoatHold Alongside(IHold wired, IReadOnlyList<IBoatHold> boats,
                                          IReadOnlyList<IMooringCleat> cleats, float reachMeters)
        {
            if (boats == null) return null;
            for (int i = 0; i < boats.Count; i++)
            {
                IBoatHold boat = boats[i];
                if (!IsAlive(boat) || IsSameCatch(wired, boat) || boat.UsedUnits == 0) continue;
                if (IsBerthed(boat.WorldPosition, cleats, reachMeters)) return boat;
            }
            return null;
        }

        /// <summary>Live twin of <see cref="Alongside"/> over the registered holds and cleats, at the
        /// owner's reach — the one the counter asks.</summary>
        public static IBoatHold AlongsideNow(IHold wired)
            => Alongside(wired, BoatHolds.Active, MooringCleats.Active, ReachMeters);

        /// <summary>
        /// Is there a boat with a catch aboard that a counter can NOT take, because she is not at a
        /// wharf? The counter says so (<see cref="NoticeBoatNotBerthed"/>) rather than leaving the
        /// player to wonder why the fish did not sell.
        /// </summary>
        public static bool CatchLeftAboard(IHold wired, IReadOnlyList<IBoatHold> boats,
                                           IReadOnlyList<IMooringCleat> cleats, float reachMeters)
        {
            if (boats == null) return false;
            for (int i = 0; i < boats.Count; i++)
            {
                IBoatHold boat = boats[i];
                if (!IsAlive(boat) || IsSameCatch(wired, boat) || boat.UsedUnits == 0) continue;
                if (!IsBerthed(boat.WorldPosition, cleats, reachMeters)) return true;
            }
            return false;
        }

        /// <summary>Live twin of <see cref="CatchLeftAboard"/>.</summary>
        public static bool CatchLeftAboardNow(IHold wired)
            => CatchLeftAboard(wired, BoatHolds.Active, MooringCleats.Active, ReachMeters);

        /// <summary>⚠ The registry holds interfaces, so a destroyed component would read as a live
        /// hold; launder Unity's fake-null explicitly (the <c>GameServices.PlayerHold</c> lesson).</summary>
        private static bool IsAlive(IBoatHold boat)
            => boat != null && !(boat is Object o && o == null);

        private static bool IsSameCatch(IHold wired, IBoatHold boat)
            => wired != null && (ReferenceEquals(wired, boat) || ReferenceEquals(wired.Items, boat.Items));
    }
}
