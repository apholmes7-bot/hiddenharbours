using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>Where a fitting's mesh actually sits on its hull</b> — the one piece of arithmetic behind
    /// <see cref="IHullPropRenderer"/>, pulled out of the renderer so it can be proven without a GPU.
    ///
    /// <para>A fitting is baked in its HULL's rig space with the rig's own mount offset already
    /// injected, so drawing it is a rigid motion about the fitting's pivot: the rotation Boats hands
    /// down, the lateral clamp offset of a multi-engine fit, and — for a BORROWED fitting — the shift
    /// that hangs another boat's part on this boat's transom. Three inputs, one transform.</para>
    ///
    /// <para><b>Why it is not three lines at the call site.</b> A Transform applies rotation and THEN
    /// translation (<c>v' = R·v + t</c>) while what a fitting needs is <c>v' = P + R·(v − P)</c>, so
    /// the position it must be given is <c>P − R·P</c> — the classic rotate-about-a-point, and the
    /// classic place to get it wrong. This project has shipped that error once already (an outboard
    /// pivoting about the hull origin, swinging clean through the boat), and it shipped a second,
    /// quieter version of it in the lateral mount: folding the clamp offset INTO the rotated pivot
    /// (<c>(P+m) − R·(P+m)</c>) cancels the offset exactly at dead ahead and grows it as the engine
    /// swivels, so a twin's two engines sat on top of each other until she turned. The mount is a
    /// translation of the whole fitting, not part of what it turns about — which is what the pose
    /// contract said all along (<c>out = P + (mx,0,0) + R·(v − P)</c>, see
    /// <c>HiddenHarbours.Boats.OutboardMotorMeshPose</c>).</para>
    /// </summary>
    public static class HullPropFitment
    {
        /// <summary>
        /// The local position to give a fitting's mesh child, for a fitting that turns about
        /// <paramref name="pivotLocalMeters"/> (the def's <see cref="HullPropMeshDef.PivotLocalMeters"/>)
        /// and is currently rotated by <paramref name="rotation"/>.
        ///
        /// <para><paramref name="lateralOffsetMeters"/> is the clamp position along the beam (a twin
        /// outboard's ±0.34; 0 for anything on the centreline) and
        /// <paramref name="fitmentOffsetMeters"/> is the borrowed-fitting shift
        /// (<c>Vector3.zero</c> for a fitting worn by the boat it was baked for). Both TRANSLATE the
        /// whole fitting, pivot included — so the part still turns about its own clamp once it has
        /// been moved, which is the only thing that keeps a swivelling engine on its bracket.</para>
        /// </summary>
        public static Vector3 LocalPosition(Vector3 pivotLocalMeters, float lateralOffsetMeters,
                                            Vector3 fitmentOffsetMeters, Quaternion rotation)
            => pivotLocalMeters + Mount(lateralOffsetMeters, fitmentOffsetMeters)
             - rotation * pivotLocalMeters;

        /// <summary>
        /// The local position for the part of a fitting that does NOT articulate — the outboard's
        /// clamp bracket, bolted to the transom while the engine swivels on it. It takes the same
        /// translation and no rotation at all, which is the whole reason it is a separate child.
        /// </summary>
        public static Vector3 FixedLocalPosition(float lateralOffsetMeters, Vector3 fitmentOffsetMeters)
            => Mount(lateralOffsetMeters, fitmentOffsetMeters);

        /// <summary>The total translation applied to a fitting: its clamp position along the beam plus
        /// the borrowed-fitting shift.</summary>
        public static Vector3 Mount(float lateralOffsetMeters, Vector3 fitmentOffsetMeters)
            => new Vector3(lateralOffsetMeters + fitmentOffsetMeters.x,
                           fitmentOffsetMeters.y,
                           fitmentOffsetMeters.z);
    }
}
