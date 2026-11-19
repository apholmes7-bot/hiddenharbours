using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>The rower, sat on his thwart with his hands on the oars.</b> The rowing clip is the rig's
    /// STANDING stroke, authored in the chest's frame: its hands trace their own ellipse and its feet
    /// stand on the boards, so a figure that only plays it holds the air beside the handles (0.22 to
    /// 0.77 m off them on the skipper) with his hips 0.58 m above the thwart. This takes the clip's
    /// composed frame and moves it onto the boat's own points (<see cref="RowingTargets"/>), after the
    /// clip and the look and before the skin:
    ///
    /// <list type="number">
    /// <item><b>Sit.</b> The pelvis moves onto the seat: the rig's own seated law (its <c>drive()</c>
    /// pose: <c>pelvis.p = [0, -0.02, seatZ + 0.10]</c>).</item>
    /// <item><b>Lie back</b> about the spine's own x axis by the layback, which the caller has already
    /// scaled by the rig's drive (peak at the finish).</item>
    /// <item><b>Brace.</b> Each leg is a two-bone solve, hip to ankle, onto its foot brace (lifted by the
    /// clip's own ankle height), knee forward and up; the foot keeps the clip's orientation.</item>
    /// <item><b>Reach.</b> If a grip is beyond the arm from where the shoulders now are, he leans
    /// forward from the spine, by the least angle (bisected) that brings both grips into reach, up to
    /// the station's limit.</item>
    /// <item><b>Hold.</b> Each arm is a two-bone solve, shoulder to palm (the <c>carry</c> socket, where
    /// the held props already sit, ADR 0053), onto its grip, the elbow kept in the clip's plane. A grip
    /// still out of reach is reached TOWARD, with the arm straight: the arm is never stretched.</item>
    /// </list>
    ///
    /// <para>Bone indices are resolved by id once; every solve is in place on the caller's world
    /// matrices, with no allocation, and is a pure function of the clip frame and the targets (rule 5).
    /// A skin missing any of the bones is left alone (<see cref="IsUsable"/> false).</para>
    /// </summary>
    public sealed class RowingPoseSolver
    {
        /// <summary>The pelvis bone's height above the seat surface, metres. A RIG FACT: the rig's
        /// seated law places <c>pelvis.p</c> at <c>seatZ + 0.10</c>.</summary>
        public const float PelvisAboveSeatMeters = 0.10f;

        /// <summary>The pelvis bone's offset along the figure's facing from the seat point, metres. A
        /// RIG FACT: the rig's seated law places <c>pelvis.p</c> at <c>y = -0.02</c>.</summary>
        public const float PelvisAlongFacingMeters = -0.02f;

        /// <summary>The knee's pole for the LEFT leg, as a direction from the hip in the figure's frame
        /// (the right leg mirrors x): forward and up, splayed a little outward, so a seated knee bends
        /// the way a knee does. Only its direction matters.</summary>
        public static readonly Vector3 LeftKneePole = new Vector3(-0.3f, 1f, 0.6f);

        /// <summary>Bisection steps for the reach lean: 10 halvings of a 30 degree limit is 0.03 degrees.</summary>
        public const int ReachLeanIterations = 10;

        /// <summary>The slack a two-bone solve keeps from a dead-straight (or fully folded) limb, metres,
        /// so the bend plane never degenerates. Far below a pixel.</summary>
        public const float LimbSlackMeters = 1e-4f;

        private const float Tiny = 1e-6f;

        /// <summary>The targets in the FIGURE's own frame (rig metres, +x right, +y forward, +z up,
        /// feet at the origin): what <see cref="Solve"/> reads. Hands and feet are already assigned to
        /// a side by where the points fall across the figure, not by oar names, so the same solve
        /// serves a rower facing aft or forward.</summary>
        public struct Targets
        {
            public bool HasGripLeft, HasGripRight;
            public Vector3 GripLeft, GripRight;
            /// <summary>Where the pelvis bone goes (the seat plus the rig's seated offset).</summary>
            public Vector3 Pelvis;
            public Vector3 BraceLeft, BraceRight;
            /// <summary>Layback this frame, degrees (already scaled by the drive).</summary>
            public float LaybackDegrees;
            public float ReachLeanMaxDegrees;
        }

        /// <summary>What one solve did, for tests and the inspector.</summary>
        public struct Result
        {
            /// <summary>True when the solve ran.</summary>
            public bool Solved;
            /// <summary>Extra forward lean taken to reach the grips, degrees.</summary>
            public float LeanDegrees;
            /// <summary>Grip distance from the shoulder over the arm's length, before the solve; above 1
            /// the grip was out of reach and the arm was left straight toward it.</summary>
            public float ReachUseLeft, ReachUseRight;
        }

        private readonly int[] _parent;
        private readonly bool[] _subtree;
        private readonly int _pelvis, _spine;
        private readonly int _hipL, _kneeL, _footL, _hipR, _kneeR, _footR;
        private readonly int _shoulderL, _elbowL, _palmL, _shoulderR, _elbowR, _palmR;

        /// <summary>The bone ids the solve needs. The palm is the hand's <c>carry</c> socket.</summary>
        public const string PelvisId = "pelvis", SpineId = "spine";
        public const string HipLId = "hip_L", KneeLId = "knee_L", FootLId = "foot_L";
        public const string HipRId = "hip_R", KneeRId = "knee_R", FootRId = "foot_R";
        public const string ShoulderLId = "shoulder_L", ElbowLId = "elbow_L", PalmLId = "carry_L";
        public const string ShoulderRId = "shoulder_R", ElbowRId = "elbow_R", PalmRId = "carry_R";

        /// <summary>True when the skin carries every bone the solve needs.</summary>
        public bool IsUsable { get; }

        /// <summary>The bone count this solver was bound to.</summary>
        public int BoneCount => _parent.Length;

        public int PalmLeft => _palmL;
        public int PalmRight => _palmR;
        public int ShoulderLeft => _shoulderL;
        public int ShoulderRight => _shoulderR;
        public int ElbowLeft => _elbowL;
        public int ElbowRight => _elbowR;
        public int PelvisBone => _pelvis;
        public int FootLeft => _footL;
        public int FootRight => _footR;

        public RowingPoseSolver(CharacterSkinDef.Bone[] bones)
        {
            int n = bones != null ? bones.Length : 0;
            _parent = new int[n];
            _subtree = new bool[n];
            for (int b = 0; b < n; b++) _parent[b] = bones[b].Parent;

            _pelvis = Find(bones, PelvisId); _spine = Find(bones, SpineId);
            _hipL = Find(bones, HipLId); _kneeL = Find(bones, KneeLId); _footL = Find(bones, FootLId);
            _hipR = Find(bones, HipRId); _kneeR = Find(bones, KneeRId); _footR = Find(bones, FootRId);
            _shoulderL = Find(bones, ShoulderLId); _elbowL = Find(bones, ElbowLId); _palmL = Find(bones, PalmLId);
            _shoulderR = Find(bones, ShoulderRId); _elbowR = Find(bones, ElbowRId); _palmR = Find(bones, PalmRId);

            IsUsable = n > 0 && _pelvis >= 0 && _spine >= 0
                       && _hipL >= 0 && _kneeL >= 0 && _footL >= 0 && _hipR >= 0 && _kneeR >= 0 && _footR >= 0
                       && _shoulderL >= 0 && _elbowL >= 0 && _palmL >= 0
                       && _shoulderR >= 0 && _elbowR >= 0 && _palmR >= 0;
        }

        private static int Find(CharacterSkinDef.Bone[] bones, string id)
        {
            if (bones == null) return -1;
            for (int b = 0; b < bones.Length; b++)
                if (string.Equals(bones[b].Id, id, System.StringComparison.Ordinal)) return b;
            return -1;
        }

        /// <summary>
        /// The hull-frame <paramref name="onHull"/> targets in the frame of a figure whose root sits at
        /// <paramref name="figureLocal"/> turned <paramref name="figureYawDegrees"/> about +z on the
        /// same hull frame (exactly the root's local transform), with the layback scaled by
        /// <paramref name="drive"/> (0 hands out, 1 hands in). The figure's RIGHT hand takes whichever
        /// grip falls further to his right, and likewise the feet.
        /// </summary>
        public static Targets ToFigure(in RowingTargets onHull, Vector3 figureLocal, float figureYawDegrees, float drive)
        {
            Quaternion inv = Quaternion.Inverse(Quaternion.AngleAxis(figureYawDegrees, Vector3.forward));
            Vector3 port = inv * (onHull.PortGrip - figureLocal);
            Vector3 star = inv * (onHull.StarboardGrip - figureLocal);
            Vector3 portBrace = inv * (onHull.PortBrace - figureLocal);
            Vector3 starBrace = inv * (onHull.StarboardBrace - figureLocal);
            Vector3 seat = inv * (onHull.Seat - figureLocal);

            var t = new Targets
            {
                Pelvis = seat + new Vector3(0f, PelvisAlongFacingMeters, PelvisAboveSeatMeters),
                LaybackDegrees = onHull.LaybackDegrees * Mathf.Clamp01(drive),
                ReachLeanMaxDegrees = Mathf.Max(0f, onHull.ReachLeanMaxDegrees),
            };

            bool starOnRight = star.x >= port.x;
            t.GripRight = starOnRight ? star : port;
            t.HasGripRight = starOnRight ? onHull.StarboardGripValid : onHull.PortGripValid;
            t.GripLeft = starOnRight ? port : star;
            t.HasGripLeft = starOnRight ? onHull.PortGripValid : onHull.StarboardGripValid;

            bool starBraceOnRight = starBrace.x >= portBrace.x;
            t.BraceRight = starBraceOnRight ? starBrace : portBrace;
            t.BraceLeft = starBraceOnRight ? portBrace : starBrace;
            return t;
        }

        /// <summary>
        /// Solve the composed frame <paramref name="world"/> (figure-space bone matrices, as
        /// <see cref="CharacterSkinPose.ComposeWorld"/> leaves them) onto <paramref name="t"/>, in place.
        /// </summary>
        public Result Solve(Matrix4x4[] world, in Targets t)
        {
            var r = default(Result);
            if (!IsUsable || world == null || world.Length < _parent.Length) return r;

            // The clip's own feet: their height above the sole and their orientation, kept on the brace.
            float ankleL = world[_footL].m23, ankleR = world[_footR].m23;
            Quaternion footRotL = world[_footL].rotation, footRotR = world[_footR].rotation;

            // 1. Sit.
            Translate(world, _pelvis, t.Pelvis - Pos(world, _pelvis));

            // 2. Lie back.
            if (t.LaybackDegrees != 0f)
                Rotate(world, _spine, Pos(world, _spine), Quaternion.AngleAxis(t.LaybackDegrees, AxisX(world, _spine)));

            // 3. Brace.
            Leg(world, _hipL, _kneeL, _footL, t.BraceLeft + new Vector3(0f, 0f, ankleL), footRotL, -1f);
            Leg(world, _hipR, _kneeR, _footR, t.BraceRight + new Vector3(0f, 0f, ankleR), footRotR, +1f);

            // 4. Reach.
            r.LeanDegrees = ReachLean(world, in t);
            if (r.LeanDegrees > 0f)
                Rotate(world, _spine, Pos(world, _spine), Quaternion.AngleAxis(-r.LeanDegrees, AxisX(world, _spine)));

            // 5. Hold.
            if (t.HasGripLeft)
                r.ReachUseLeft = TwoBone(world, _shoulderL, _elbowL, _palmL, t.GripLeft, Pos(world, _elbowL));
            if (t.HasGripRight)
                r.ReachUseRight = TwoBone(world, _shoulderR, _elbowR, _palmR, t.GripRight, Pos(world, _elbowR));

            r.Solved = true;
            return r;
        }

        /// <summary>The arm's full length on this frame: shoulder to elbow plus elbow to palm.</summary>
        public float ArmReach(Matrix4x4[] world, bool left)
        {
            int s = left ? _shoulderL : _shoulderR, e = left ? _elbowL : _elbowR, p = left ? _palmL : _palmR;
            return (Pos(world, e) - Pos(world, s)).magnitude + (Pos(world, p) - Pos(world, e)).magnitude;
        }

        // ---------------------------------------------------------------- the steps

        private void Leg(Matrix4x4[] world, int hip, int knee, int foot, Vector3 target, Quaternion footRot, float side)
        {
            Vector3 pole = Pos(world, hip) + new Vector3(side * -LeftKneePole.x, LeftKneePole.y, LeftKneePole.z);
            TwoBone(world, hip, knee, foot, target, pole);
            Quaternion now = world[foot].rotation;
            Rotate(world, foot, Pos(world, foot), footRot * Quaternion.Inverse(now));
        }

        private float ReachLean(Matrix4x4[] world, in Targets t)
        {
            if (!t.HasGripLeft && !t.HasGripRight) return 0f;
            Vector3 pivot = Pos(world, _spine), axis = AxisX(world, _spine);
            float reachL = ArmReach(world, true), reachR = ArmReach(world, false);
            if (InReach(world, in t, pivot, axis, 0f, reachL, reachR)) return 0f;

            float hi = t.ReachLeanMaxDegrees;
            if (hi <= 0f || !InReach(world, in t, pivot, axis, hi, reachL, reachR)) return hi;
            float lo = 0f;
            for (int i = 0; i < ReachLeanIterations; i++)
            {
                float mid = 0.5f * (lo + hi);
                if (InReach(world, in t, pivot, axis, mid, reachL, reachR)) hi = mid;
                else lo = mid;
            }
            return hi;
        }

        private bool InReach(Matrix4x4[] world, in Targets t, Vector3 pivot, Vector3 axis, float leanDegrees,
                             float reachL, float reachR)
        {
            Quaternion q = Quaternion.AngleAxis(-leanDegrees, axis);
            if (t.HasGripLeft)
            {
                Vector3 s = pivot + q * (Pos(world, _shoulderL) - pivot);
                if ((t.GripLeft - s).magnitude > reachL - LimbSlackMeters) return false;
            }
            if (t.HasGripRight)
            {
                Vector3 s = pivot + q * (Pos(world, _shoulderR) - pivot);
                if ((t.GripRight - s).magnitude > reachR - LimbSlackMeters) return false;
            }
            return true;
        }

        /// <summary>
        /// Bring bone <paramref name="c"/> to <paramref name="target"/> by turning <paramref name="a"/>
        /// and <paramref name="b"/> (each with everything below it), keeping the bend on
        /// <paramref name="pole"/>'s side. A target out of reach is reached toward with the limb straight;
        /// the segments keep their lengths. Answers the target's distance over the limb's length.
        /// </summary>
        private float TwoBone(Matrix4x4[] world, int a, int b, int c, Vector3 target, Vector3 pole)
        {
            Vector3 A = Pos(world, a), B = Pos(world, b), C = Pos(world, c);
            float la = (B - A).magnitude, lb = (C - B).magnitude, reach = la + lb;
            Vector3 toT = target - A;
            float dT = toT.magnitude;
            if (la < Tiny || lb < Tiny || dT < Tiny) return reach > Tiny ? dT / reach : 0f;

            float d = Mathf.Clamp(dT, Mathf.Abs(la - lb) + LimbSlackMeters, reach - LimbSlackMeters);
            Vector3 u = toT / dT;
            Vector3 v = Perpendicular(pole - A, u);
            if (v.sqrMagnitude < Tiny) v = Perpendicular(B - A, u);
            if (v.sqrMagnitude < Tiny) v = Perpendicular(Mathf.Abs(u.x) < 0.9f ? Vector3.right : Vector3.up, u);
            v.Normalize();

            float cosA = Mathf.Clamp((la * la + d * d - lb * lb) / (2f * la * d), -1f, 1f);
            float sinA = Mathf.Sqrt(Mathf.Max(0f, 1f - cosA * cosA));
            Vector3 elbow = A + u * (la * cosA) + v * (la * sinA);
            Rotate(world, a, A, Quaternion.FromToRotation(B - A, elbow - A));

            Vector3 reached = A + u * d;
            Vector3 e1 = Pos(world, b), c1 = Pos(world, c);
            Rotate(world, b, e1, Quaternion.FromToRotation(c1 - e1, reached - e1));
            return dT / reach;
        }

        private static Vector3 Perpendicular(Vector3 v, Vector3 unitAxis) => v - Vector3.Dot(v, unitAxis) * unitAxis;

        // ---------------------------------------------------------------- rigid moves of a subtree

        private void Translate(Matrix4x4[] world, int root, Vector3 delta)
        {
            if (delta.sqrMagnitude == 0f) return;
            Matrix4x4 m = Matrix4x4.Translate(delta);
            Apply(world, root, m);
        }

        private void Rotate(Matrix4x4[] world, int root, Vector3 pivot, Quaternion q)
        {
            Matrix4x4 m = Matrix4x4.TRS(pivot, q, Vector3.one) * Matrix4x4.Translate(-pivot);
            Apply(world, root, m);
        }

        /// <summary>Pre-multiply <paramref name="root"/> and every bone under it by <paramref name="m"/>.
        /// A parent precedes its child in the rig's bone order (the composition's own rule), so one pass
        /// marks the subtree.</summary>
        private void Apply(Matrix4x4[] world, int root, Matrix4x4 m)
        {
            int n = _parent.Length;
            for (int b = 0; b < n; b++)
            {
                int p = _parent[b];
                _subtree[b] = b == root || (b > root && p >= 0 && p < b && _subtree[p]);
                if (_subtree[b]) world[b] = m * world[b];
            }
        }

        private static Vector3 Pos(Matrix4x4[] world, int b) => new Vector3(world[b].m03, world[b].m13, world[b].m23);

        private static Vector3 AxisX(Matrix4x4[] world, int b) =>
            new Vector3(world[b].m00, world[b].m10, world[b].m20).normalized;
    }
}
