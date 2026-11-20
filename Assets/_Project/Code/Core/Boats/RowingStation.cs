using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>Where the rower's body must be</b>, read off the oars that are being rowed, in the hull's
    /// rig frame (metres; +x starboard, +y forward, +z up): the point on each oar handle a hand holds,
    /// the thwart he sits on and the boards his feet brace against.
    ///
    /// <para>The oars are the truth. The rower's clip was authored as a standing stroke in the chest's
    /// frame and knows nothing of where the handles go, so a figure that only plays the clip holds
    /// the air beside them. With these points the figure is solved onto the oars each frame
    /// (<see cref="RowingPoseSolver"/>) instead.</para>
    /// </summary>
    public struct RowingTargets
    {
        /// <summary>True when the port oar is being rowed, so <see cref="PortGrip"/> is a handle a
        /// hand should hold. An oar that trails or ships is let go of: its handle is out of reach.</summary>
        public bool PortGripValid;

        /// <summary>True when the starboard oar is being rowed.</summary>
        public bool StarboardGripValid;

        /// <summary>The port oar's grip: the point on its loom <c>GripInboard</c> metres inboard of the
        /// oarlock, where it is drawn this frame.</summary>
        public Vector3 PortGrip;

        /// <summary>The starboard oar's grip.</summary>
        public Vector3 StarboardGrip;

        /// <summary>The top of the thwart under the rower's sitting bones.</summary>
        public Vector3 Seat;

        /// <summary>The surface the port foot braces against.</summary>
        public Vector3 PortBrace;

        /// <summary>The surface the starboard foot braces against.</summary>
        public Vector3 StarboardBrace;

        /// <summary>How far the rower lies back at the finish, degrees, on top of the clip's own lean.</summary>
        public float LaybackDegrees;

        /// <summary>The most extra forward lean he may take to bring a grip into reach, degrees.</summary>
        public float ReachLeanMaxDegrees;
    }

    /// <summary>
    /// <b>A rowing station</b>: whoever poses a boat's oars and knows where their handles are. A Core
    /// seam beside <see cref="IOarStroke"/> (which it is read through, by a cast, so the stroke's own
    /// fakes stay untouched) so the Player and Art modules never reach into the Boats module (rule 4).
    /// Presentation only: nothing in the simulation reads it (rule 5).
    /// </summary>
    public interface IRowingStation
    {
        /// <summary>The station's targets this frame, or false when it is not wired or nobody rows.</summary>
        bool TryGetRowingTargets(out RowingTargets targets);
    }

    /// <summary>The pure arithmetic of a rowing station. Static, allocation-free, deterministic.</summary>
    public static class RowingStationMath
    {
        /// <summary>
        /// The grip on an oar whose lock is at <paramref name="oarlock"/> and whose loom points from
        /// the lock toward the blade along <paramref name="bladeDirection"/> (a unit vector): the handle
        /// sits across the lock from the blade, so the grip is <paramref name="inboardMeters"/> back
        /// along the loom.
        /// </summary>
        public static Vector3 Grip(Vector3 oarlock, Vector3 bladeDirection, float inboardMeters) =>
            oarlock - inboardMeters * bladeDirection;

        /// <summary><paramref name="v"/> mirrored across the centreline (the hull rig's x = 0 plane):
        /// the port twin of a starboard point.</summary>
        public static Vector3 MirrorAcrossCentreline(Vector3 v) => new Vector3(-v.x, v.y, v.z);
    }
}
