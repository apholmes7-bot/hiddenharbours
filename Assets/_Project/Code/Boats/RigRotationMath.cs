using UnityEngine;

namespace HiddenHarbours.Boats
{
    /// <summary>
    /// <b>Rotation matrix → quaternion, for poses transcribed out of an art-director rig.</b>
    ///
    /// <para>Every fitting on the mesh path (ADR 0022 phase 7) is posed by a rotation the rig states
    /// as ARITHMETIC — an oar's two orthonormal frames, an outboard's steer-then-tilt composition —
    /// never as a Unity quaternion. Building the 3×3 explicitly and converting once, here, is what
    /// keeps a convention mismatch from becoming a mirrored fitting that still looks like a fitting.
    /// </para>
    ///
    /// <para>⚠️ <b>Not <c>Quaternion.LookRotation</c>, and not <c>Quaternion.AngleAxis</c> chains.</b>
    /// LookRotation re-orthonormalises against ITS conventions for which axis is forward and which is
    /// up; an AngleAxis chain silently encodes Unity's handedness where the rig has its own. The
    /// standard trace method below is the one conversion that asserts nothing beyond the matrix it is
    /// handed — and it is the one the dory's oars are measured through, at zero pixel differences
    /// against the rig's own <c>renderOars</c>.</para>
    /// </summary>
    public static class RigRotationMath
    {
        /// <summary>
        /// The quaternion equivalent to the upper-left 3×3 of <paramref name="m"/>, by the standard
        /// trace method with its three fallback branches (the trace form loses precision, and then
        /// sign, as the trace approaches −1).
        ///
        /// <para>The caller owns the matrix being a PROPER rotation. A composite of two rig frames may
        /// be built from left-handed halves — the dory's are — and that is fine: the product's
        /// determinant is +1. Converting either half on its own is what produces a mirror.</para>
        /// </summary>
        public static Quaternion FromMatrix(in Matrix4x4 m)
        {
            float trace = m[0, 0] + m[1, 1] + m[2, 2];
            Quaternion q;
            if (trace > 0f)
            {
                float s = Mathf.Sqrt(trace + 1f) * 2f;
                q = new Quaternion((m[2, 1] - m[1, 2]) / s, (m[0, 2] - m[2, 0]) / s,
                                   (m[1, 0] - m[0, 1]) / s, 0.25f * s);
            }
            else if (m[0, 0] > m[1, 1] && m[0, 0] > m[2, 2])
            {
                float s = Mathf.Sqrt(1f + m[0, 0] - m[1, 1] - m[2, 2]) * 2f;
                q = new Quaternion(0.25f * s, (m[0, 1] + m[1, 0]) / s,
                                   (m[0, 2] + m[2, 0]) / s, (m[2, 1] - m[1, 2]) / s);
            }
            else if (m[1, 1] > m[2, 2])
            {
                float s = Mathf.Sqrt(1f + m[1, 1] - m[0, 0] - m[2, 2]) * 2f;
                q = new Quaternion((m[0, 1] + m[1, 0]) / s, 0.25f * s,
                                   (m[1, 2] + m[2, 1]) / s, (m[0, 2] - m[2, 0]) / s);
            }
            else
            {
                float s = Mathf.Sqrt(1f + m[2, 2] - m[0, 0] - m[1, 1]) * 2f;
                q = new Quaternion((m[0, 2] + m[2, 0]) / s, (m[1, 2] + m[2, 1]) / s,
                                   0.25f * s, (m[1, 0] - m[0, 1]) / s);
            }
            return Quaternion.Normalize(q);
        }
    }
}
