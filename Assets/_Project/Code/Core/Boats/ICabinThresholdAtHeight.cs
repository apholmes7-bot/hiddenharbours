using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>A DOORWAY THAT ALSO ASKS WHICH FLOOR SHE IS STANDING ON</b> — <see cref="ICabinThreshold"/>
    /// with her height, for the hulls where floors stack over one doorway.
    ///
    /// <para><b>Why the plan alone is not enough (Phase A, 2026-09-18).</b> A threshold is a point on a
    /// FLOOR, and on the sport fishers the plan under a doorway is three floors deep. Inside the
    /// Convertible's main-door band there is a deck 2.88 m above her sill and a cockpit 0.53 m below it;
    /// the Skybridge's skylounge slider stands 4.45 m straight over her main door. Asked in plan, a walker
    /// up there would walk into the saloon through its deckhead. The walker knows which floor she is on,
    /// so she says so, and the door compares it with its own sill
    /// (<see cref="BoatInteriorDoor.ThresholdPoint"/>'s z) at the def's own
    /// <see cref="BoatInteriorDef.FloorTolerance"/> — the comparison the importer made when it placed
    /// her routes, so "one floor" means one thing at both ends of the pipeline.</para>
    ///
    /// <para><b>A SECOND interface, not a new member</b> — <see cref="ICabinThreshold"/>'s own rule: a
    /// member added there stops every test double compiling in files nobody touched. A walker that
    /// knows her floor asks <c>is ICabinThresholdAtHeight</c> of the doorway she already resolved; a
    /// doorway without it, and a walker that does not know her floor, keep the plan question exactly as
    /// before.</para>
    /// </summary>
    public interface ICabinThresholdAtHeight : ICabinThreshold
    {
        /// <summary>
        /// <see cref="ICabinThreshold.TryWalkThrough"/>, with <paramref name="hullLocalMetres"/>'s z the
        /// height of the floor she is standing on (hull metres above the keel). A doorway refuses a
        /// walker whose floor is not its sill's; every other gate is asked exactly as in plan.
        /// </summary>
        bool TryWalkThroughAt(Vector3 hullLocalMetres);
    }
}
