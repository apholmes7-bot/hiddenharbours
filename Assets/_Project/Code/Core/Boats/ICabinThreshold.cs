using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>A DOORWAY ON A BOAT THAT A WALKER CAN CROSS</b> — the seam between whoever is walking and
    /// whoever owns the door, so that the owner's 2026-08-28 ruling (<i>"a player can walk through
    /// freely when opened"</i>) reaches the player's own deck walk without the Player lane naming a
    /// single Boats type.
    ///
    /// <para><b>The division it draws.</b> The walker knows exactly one thing the door cannot know —
    /// <b>where she is standing</b> — and the door knows everything else: whether its leaf is open,
    /// whether that point is inside the measured opening, whether this approach has already been spent,
    /// which way the crossing goes, and whether the room behind it will accept her. So the whole seam is
    /// one call that both ASKS and ACTS. A split seam (<c>MayCross</c> then <c>Cross</c>) would put the
    /// door's own latch in the caller's hands, and two walkers sharing one doorway would then spend it
    /// twice — see <c>BoatCabinDoor</c>'s latch remarks for why there is exactly one.</para>
    ///
    /// <para><b>Rule 4, in the direction it actually matters.</b> <c>DeckWalkController</c> lives in
    /// Player and the door lives in Boats; a feature module talks through a named seam rather than
    /// through another module's fields. Player asks "did that step take me through a door?" and Boats
    /// answers — and the App lane, being the composition root, may still hold the concrete door
    /// (the arrival does, for its own two walkers).</para>
    ///
    /// <para><b>A SECOND interface rather than a member on an existing one</b> — <see cref="IHullCutaway"/>'s
    /// reasoning, verbatim: a member added to a widely-implemented Core interface stops every test double
    /// compiling, in files nobody touched, and this project has paid for that once already. Ask for the
    /// capability with <c>GetComponentInChildren&lt;ICabinThreshold&gt;()</c>; a hull that has not got one
    /// says so by being null, which is most of the fleet and is DATA rather than a fault.</para>
    ///
    /// <para><b>⚠ Resolve it, do not cache it blind.</b> An interface reference does not go through
    /// <c>UnityEngine.Object</c>'s <c>==</c>, so a destroyed door still answers through this type and
    /// keeps reporting whatever it last held. Every consumer must test liveness with
    /// <c>is UnityEngine.Object o &amp;&amp; o != null</c> — the pattern <c>BoatCutaway.Renderer</c>
    /// already keeps for the same reason.</para>
    /// </summary>
    public interface ICabinThreshold
    {
        /// <summary>
        /// <b>Walk through it, if that is what just happened.</b> <paramref name="hullLocalMetres"/> is
        /// where the walker is standing in the HULL's own frame (+x starboard, +y bow) — the frame every
        /// authored deck polygon, cabin sole and door threshold already speaks, which is what lets the
        /// deck and the sole ask one doorway one question with no projection on either side.
        ///
        /// <para>Returns true only on the tick a crossing actually happened, so a caller never has to
        /// hold a second copy of "is she inside". False is the ordinary answer, on almost every tick:
        /// the leaf is shut, or she is nowhere near it, or she is still standing in the doorway she came
        /// through.</para>
        ///
        /// <para><b>Nothing here moves her.</b> An interior is a layer swap and not a place you travel
        /// to (ADR 0038): the frame she is placed in changes underneath her and her world point does
        /// not. That is why this returns a bool rather than a position.</para>
        /// </summary>
        bool TryWalkThrough(Vector2 hullLocalMetres);
    }
}
