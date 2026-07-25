using UnityEngine;

namespace HiddenHarbours.Boats
{
    /// <summary>
    /// <b>Where an oar points, for the MESH path (ADR 0022 phase 7)</b> — a transcription of
    /// <c>doryIsoRig.js</c>'s own oar kinematics, and nothing more.
    ///
    /// <para><b>Why a transcription at all.</b> The sprite path never needed this: it picked a baked
    /// cell and the rig's arithmetic stayed inside the bake. A mesh oar is posed at runtime, where
    /// the rig is not available, so the kinematics have to exist in C#. That is a risk this project
    /// has been burned by — reading a rig and declaring what it means is how it shipped mirrored
    /// boats five times — so every number below is adjudicated against the rig itself by
    /// <c>DoryOarMeshPoseTests</c>, which runs <c>oarPose</c> in V8 and compares. Transcribe, then
    /// prove; never transcribe and trust.</para>
    ///
    /// <para><b>The subtle part, and the reason this is not just "rotate the oar".</b> The rig builds
    /// the oar's cross-section frame from a FIXED world up-vector:</para>
    /// <code>
    ///   ax = oarDir(side, sweep, dip);  up = |ax.z| &gt; 0.9 ? [0,1,0] : [0,0,1];
    ///   r  = normalize(cross(ax, up));  u = cross(r, ax);
    /// </code>
    /// <para>so the blade — a flat paddle lying in <c>r</c>, up to 0.24 m across — FEATHERS as a
    /// function of where the oar points, rather than carrying its roll rigidly with it. Rotating the
    /// baked mesh by the naive shortest arc from one direction to the next therefore does NOT
    /// reproduce the rig: the loom would land correctly and the blade would sit at the wrong angle,
    /// worst at the catch and the finish where the blade is most side-on to the camera. Reproducing
    /// the frame construction and taking the rotation BETWEEN the two frames is exact.</para>
    ///
    /// <para>⚠️ Each individual frame is LEFT-handed as ordered — <c>cross(ax, r) = −u</c>, so its
    /// determinant is −1 and it is a reflection, not a rotation. That is fine and is why the maths
    /// below never converts a single frame to a quaternion: the COMPOSITE <c>M₁·M₀ᵀ</c> has
    /// determinant <c>(−1)(−1) = +1</c> and is a proper rotation. Converting either frame on its own
    /// would produce a mirrored oar and a plausible-looking one.</para>
    /// </summary>
    public static class DoryOarMeshPose
    {
        /// <summary>The rig's <c>oarPose('row', t)</c> ellipse: the blade traces sweep against dip
        /// once per stroke. Transcribed from the rig; proven against it.</summary>
        public const float RowSweepAmplitudeDegrees = 30f;
        public const float RowDipCentreDegrees = 6f;
        public const float RowDipAmplitudeDegrees = 22f;

        /// <summary>Shipped fore, lying along the gunwale — the rig's 'resting' state.</summary>
        public const float RestingSweepDegrees = -82f, RestingDipDegrees = -7f;

        /// <summary>Dragging aft in the water — the rig's 'trailing' state.</summary>
        public const float TrailingSweepDegrees = 76f, TrailingDipDegrees = 15f;

        /// <summary>The rig's <c>|ax.z| &gt; 0.9</c> switch to a different up-vector, kept because a
        /// near-vertical oar would otherwise take the cross product of two parallel vectors. It does
        /// not fire in any reachable pose (the steepest dip is 28°), but it is transcribed so that a
        /// future rig change cannot make C# and the rig disagree silently.</summary>
        public const float VerticalUpSwitch = 0.9f;

        /// <summary>Sweep and dip in degrees, the rig's own two angles.</summary>
        public readonly struct Pose
        {
            public readonly float SweepDegrees, DipDegrees;
            public Pose(float sweepDegrees, float dipDegrees)
            {
                SweepDegrees = sweepDegrees;
                DipDegrees = dipDegrees;
            }
        }

        /// <summary>
        /// The rowing stroke at continuous phase <paramref name="t"/> (turns; wrapped like the rig's
        /// <c>((t%1)+1)%1</c>, so a negative phase from a backing stroke is handled, not clamped).
        ///
        /// <para><b>This is the whole point of the mesh path.</b> The sprite oar quantised this to one
        /// of 8 columns; here it is read continuously. Same tuned stroke — the phase comes from the
        /// same <c>DoryOarMath.AdvanceStrokePhase</c> the owner already signed off — with the
        /// rounding taken out, exactly as #243 did for the hull's rock.</para>
        /// </summary>
        public static Pose Row(float t)
        {
            float tt = t % 1f;
            if (tt < 0f) tt += 1f;
            float a = 2f * Mathf.PI * tt;
            return new Pose(RowSweepAmplitudeDegrees * Mathf.Sin(a),
                            RowDipCentreDegrees + RowDipAmplitudeDegrees * Mathf.Cos(a));
        }

        public static Pose Resting => new Pose(RestingSweepDegrees, RestingDipDegrees);
        public static Pose Trailing => new Pose(TrailingSweepDegrees, TrailingDipDegrees);

        /// <summary>
        /// The rig's <c>oarDir</c>: a unit vector from the oarlock toward the blade, in rig space
        /// (x abeam — signed by <paramref name="side"/>, y forward, z up).
        /// </summary>
        public static Vector3 Direction(int side, float sweepDegrees, float dipDegrees)
        {
            float f = sweepDegrees * Mathf.Deg2Rad, p = dipDegrees * Mathf.Deg2Rad;
            float cf = Mathf.Cos(f), sf = Mathf.Sin(f), cp = Mathf.Cos(p), sp = Mathf.Sin(p);
            return new Vector3(Mathf.Sign(side) * cf * cp, -sf * cp, -sp);
        }

        /// <summary>
        /// The rotation carrying the oar from its BAKED pose (sweep 0, dip 0 — see
        /// <c>HullPropFleet</c>) to <paramref name="pose"/>, about its oarlock. Hand this to
        /// <c>IHullPropRenderer.LocalRotation</c>; the oarlock itself is the def's
        /// <c>PivotLocalMeters</c>, so the renderer needs no translation.
        /// </summary>
        public static Quaternion Rotation(int side, in Pose pose)
        {
            Frame(Direction(side, 0f, 0f), out Vector3 ax0, out Vector3 r0, out Vector3 u0);
            Frame(Direction(side, pose.SweepDegrees, pose.DipDegrees),
                  out Vector3 ax1, out Vector3 r1, out Vector3 u1);

            // R = M₁·M₀ᵀ, i.e. R[i][j] = ax1[i]·ax0[j] + r1[i]·r0[j] + u1[i]·u0[j]. Written with both
            // indices explicit: the first draft folded the inner sum into three lines and silently
            // produced the TRANSPOSE, which is the inverse rotation — the oar swung the wrong way
            // around its lock and the acceptance fixture read 99.7% of pixels differing.
            var m = Matrix4x4.identity;
            for (int i = 0; i < 3; i++)
                for (int j = 0; j < 3; j++)
                    m[i, j] = ax1[i] * ax0[j] + r1[i] * r0[j] + u1[i] * u0[j];
            return RigRotationMath.FromMatrix(m);
        }

        /// <summary>The rig's frame construction, verbatim. See the class note on its handedness.</summary>
        public static void Frame(Vector3 ax, out Vector3 axis, out Vector3 right, out Vector3 up)
        {
            Vector3 world = Mathf.Abs(ax.z) > VerticalUpSwitch ? new Vector3(0f, 1f, 0f)
                                                               : new Vector3(0f, 0f, 1f);
            axis = ax;
            right = Vector3.Normalize(Vector3.Cross(ax, world));
            up = Vector3.Cross(right, ax);
        }

    }
}
