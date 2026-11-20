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
    /// the held props already sit, ADR 0053), onto its grip, the elbow dropped down and back. A grip
    /// still out of reach is reached TOWARD, with the arm straight: the arm is never stretched. Resting
    /// with no grip, the hand rests on its thigh instead, the elbow hanging down and out.</item>
    /// <item><b>Clear.</b> With the skin's torso (<see cref="RowingBodyVolume"/>), no forearm, palm or
    /// elbow passes inside it closer than the station's margin: the elbow swings back and out first,
    /// and only if that is not enough does he lean back (bisected, up to the station's limit).</item>
    /// </list>
    /// <para>Resting (the station's rest weight), the lie-back step slumps him forward a few degrees
    /// and rocks him with his breath, so a rest is never a held frame.</para>
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

        /// <summary>The elbow's pole for the LEFT arm, as a direction from the shoulder in the figure's
        /// frame (the right arm mirrors x): down and back (barely out), so the elbows drop and draw back
        /// past the ribs at the finish the way a rower's do, instead of flaring out level with the
        /// shoulders (the clip's standing elbows did, with the hands drawn in to the chest). Only its
        /// direction matters.</summary>
        public static readonly Vector3 LeftElbowPole = new Vector3(-0.15f, -0.7f, -0.7f);

        /// <summary>The elbow's pole for the LEFT arm when it must clear his body: back and OUT, so at the
        /// finish the hands stop at the body and the elbows go back past the ribs, outside them. The
        /// clear step blends from <see cref="LeftElbowPole"/> toward this one, as far as it needs.</summary>
        public static readonly Vector3 LeftElbowOutPole = new Vector3(-0.85f, -0.5f, -0.25f);

        /// <summary>The elbow's pole for the LEFT arm with the hand resting on the thigh: down, out and
        /// a little back, the arm hanging loose from a dropped shoulder.</summary>
        public static readonly Vector3 LeftRestElbowPole = new Vector3(-0.55f, -0.35f, -0.75f);

        /// <summary>Steps of the elbow's swing outward in the clear step.</summary>
        public const int ElbowOutSteps = 6;

        /// <summary>Points sampled along the forearm (elbow to palm) when measuring its clearance from
        /// the torso; the elbow half of the upper arm is sampled at half as many.</summary>
        public const int ArmClearSamples = 6;

        /// <summary>Where the upper-arm samples start, as a share of the shoulder-to-elbow bone: the
        /// shoulder's own half sits in the deltoid, which is the torso's edge by construction.</summary>
        public const float UpperArmClearFrom = 0.6f;

        /// <summary>How much further out of reach (as a share of the arm) a clearing turn may take a held
        /// grip: none beyond rounding, so a hand on its handle stays on it.</summary>
        public const float ReachUseSlack = 1e-4f;

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
            /// <summary>The handle ends of the oars those hands hold, kept out of his body too.</summary>
            public Vector3 HandleEndLeft, HandleEndRight;
            /// <summary>Where the pelvis bone goes (the seat plus the rig's seated offset).</summary>
            public Vector3 Pelvis;
            public Vector3 BraceLeft, BraceRight;
            /// <summary>Layback this frame, degrees (already scaled by the drive).</summary>
            public float LaybackDegrees;
            public float ReachLeanMaxDegrees;
            /// <summary>How far the arms stay outside the torso, metres (0 = unchecked).</summary>
            public float BodyClearanceMeters;
            /// <summary>The most extra lean back the clear step may take, degrees.</summary>
            public float BodyClearLeanMaxDegrees;
            /// <summary>The most shoulder turn the clear step may take, degrees either way.</summary>
            public float BodyClearTwistMaxDegrees;
            /// <summary>The rest's own lean this frame, degrees, positive back: the slump forward plus
            /// the breath, both already scaled by the rest's weight.</summary>
            public float RestLeanDegrees;
            /// <summary>True when a hand with no grip rests on its thigh instead of playing the clip.</summary>
            public bool ThighRest;
            /// <summary>Where a free hand rests along the thigh (0 hip, 1 knee).</summary>
            public float RestHandAlongThigh;
            /// <summary>The free palm's height above the thigh bone, metres.</summary>
            public float RestHandAboveThighMeters;
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
            /// <summary>Extra lean back taken to keep the arms out of the body, degrees.</summary>
            public float ClearLeanDegrees;
            /// <summary>Shoulder turn (about the vertical) taken to keep the arms out of the body, degrees.</summary>
            public float ClearTwistDegrees;
            /// <summary>How far the elbows swung out to clear the body (0 not at all, 1 fully).</summary>
            public float ElbowOutLeft, ElbowOutRight;
            /// <summary>The arm's least clearance from the torso after the solve, metres (+inf when the
            /// arm was not solved or there is no body volume).</summary>
            public float ClearanceLeft, ClearanceRight;
        }

        private readonly RowingBodyVolume _body;
        private readonly Matrix4x4[] _scratch;
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

        /// <summary>The torso the arms are kept out of, or null.</summary>
        public RowingBodyVolume Body => _body;

        /// <summary>Bind a solver to <paramref name="bones"/>; with <paramref name="body"/> (the skin's
        /// own torso, <see cref="RowingBodyVolume.Of"/>) the arms are also kept out of it.</summary>
        public RowingPoseSolver(CharacterSkinDef.Bone[] bones, RowingBodyVolume body = null)
        {
            int n = bones != null ? bones.Length : 0;
            _body = body != null && body.IsUsable ? body : null;
            _parent = new int[n];
            _subtree = new bool[n];
            _scratch = new Matrix4x4[n];
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
                BodyClearanceMeters = Mathf.Max(0f, onHull.BodyClearanceMeters),
                BodyClearLeanMaxDegrees = Mathf.Max(0f, onHull.BodyClearLeanMaxDegrees),
                BodyClearTwistMaxDegrees = Mathf.Max(0f, onHull.BodyClearTwistMaxDegrees),
                RestHandAlongThigh = Mathf.Clamp01(onHull.RestHandAlongThigh),
                RestHandAboveThighMeters = Mathf.Max(0f, onHull.RestHandAboveThighMeters),
            };
            float w = Mathf.Clamp01(onHull.RestWeight);
            t.RestLeanDegrees = w * (Mathf.Clamp(onHull.RestBreath, -1f, 1f) * onHull.RestBreathDegrees - onHull.RestSlumpDegrees);
            t.ThighRest = w > 0f;

            bool starOnRight = star.x >= port.x;
            Vector3 portEnd = inv * (onHull.PortHandleEnd - figureLocal);
            Vector3 starEnd = inv * (onHull.StarboardHandleEnd - figureLocal);
            t.HandleEndRight = starOnRight ? starEnd : portEnd;
            t.HandleEndLeft = starOnRight ? portEnd : starEnd;
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

            // 2. Lie back (or, resting, slump and breathe).
            float back = t.LaybackDegrees + t.RestLeanDegrees;
            if (back != 0f)
                Rotate(world, _spine, Pos(world, _spine), Quaternion.AngleAxis(back, AxisX(world, _spine)));

            // 3. Brace.
            Leg(world, _hipL, _kneeL, _footL, t.BraceLeft + new Vector3(0f, 0f, ankleL), footRotL, -1f);
            Leg(world, _hipR, _kneeR, _footR, t.BraceRight + new Vector3(0f, 0f, ankleR), footRotR, +1f);

            // 4. Reach.
            r.LeanDegrees = ReachLean(world, in t);
            if (r.LeanDegrees > 0f)
                Rotate(world, _spine, Pos(world, _spine), Quaternion.AngleAxis(-r.LeanDegrees, AxisX(world, _spine)));

            // 5. Hold: each hand on its grip, or, resting with no grip, on its thigh.
            bool armL = HandTarget(world, in t, true, out Vector3 handL, out bool restL);
            bool armR = HandTarget(world, in t, false, out Vector3 handR, out bool restR);
            if (armL) r.ReachUseLeft = Arm(world, true, handL, restL, 0f);
            if (armR) r.ReachUseRight = Arm(world, false, handR, restR, 0f);

            // 6. Clear: the arms stay out of his own body. Elbows out first, then, if that is not
            // enough, the least lean back that clears them, then the least turn of the shoulders.
            r.ClearanceLeft = r.ClearanceRight = float.PositiveInfinity;
            if (_body != null && t.BodyClearanceMeters > 0f && (armL || armR))
                Clear(world, in t, armL, handL, restL, armR, handR, restR, ref r);

            r.Solved = true;
            return r;
        }

        /// <summary>The least clearance of the left or right arm's palm and forearm (elbow to palm) from
        /// the torso, metres; +inf with no body volume. Held to the station's margin.</summary>
        public float ForearmClearance(Matrix4x4[] world, bool left)
        {
            if (_body == null) return float.PositiveInfinity;
            Vector3 E = Pos(world, left ? _elbowL : _elbowR), P = Pos(world, left ? _palmL : _palmR);
            float c = float.PositiveInfinity;
            for (int i = 0; i <= ArmClearSamples; i++)
                c = Mathf.Min(c, _body.Outside(world, Vector3.Lerp(E, P, i / (float)ArmClearSamples)));
            return c;
        }

        /// <summary>The least clearance of the elbow end of the left or right upper arm from the torso,
        /// metres; +inf with no body volume. An elbow may rest against the ribs (0), never sink in.</summary>
        public float UpperArmClearance(Matrix4x4[] world, bool left)
        {
            if (_body == null) return float.PositiveInfinity;
            Vector3 S = Pos(world, left ? _shoulderL : _shoulderR), E = Pos(world, left ? _elbowL : _elbowR);
            float c = float.PositiveInfinity;
            int upper = ArmClearSamples / 2;
            for (int i = 0; i < upper; i++)
                c = Mathf.Min(c, _body.Outside(world, Vector3.Lerp(S, E, Mathf.Lerp(UpperArmClearFrom, 1f, i / (float)upper))));
            return c;
        }

        /// <summary>The least clearance of the whole checked arm (forearm, palm, elbow end of the upper
        /// arm), metres.</summary>
        public float ArmClearance(Matrix4x4[] world, bool left) =>
            Mathf.Min(ForearmClearance(world, left), UpperArmClearance(world, left));

        /// <summary>How far the arm is from failing the clear rule, metres: the forearm against
        /// <paramref name="need"/>, the upper arm against zero. Non-negative is clear.</summary>
        private float ArmSlack(Matrix4x4[] world, bool left, float need) =>
            Mathf.Min(ForearmClearance(world, left) - need, UpperArmClearance(world, left));

        private float Slack(Matrix4x4[] world, in Targets t, bool armL, bool armR, float need)
        {
            float s = HandleEndClearance(world, in t) - need;
            if (armL) s = Mathf.Min(s, ArmSlack(world, true, need));
            if (armR) s = Mathf.Min(s, ArmSlack(world, false, need));
            return s;
        }

        private bool HandTarget(Matrix4x4[] world, in Targets t, bool left, out Vector3 hand, out bool rest)
        {
            rest = false;
            hand = default;
            if (left ? t.HasGripLeft : t.HasGripRight)
            {
                hand = left ? t.GripLeft : t.GripRight;
                return true;
            }
            if (!t.ThighRest) return false;
            Vector3 hip = Pos(world, left ? _hipL : _hipR), knee = Pos(world, left ? _kneeL : _kneeR);
            hand = Vector3.Lerp(hip, knee, t.RestHandAlongThigh) + new Vector3(0f, 0f, t.RestHandAboveThighMeters);
            rest = true;
            return true;
        }

        /// <summary>Solve one arm onto <paramref name="hand"/>, its elbow swung <paramref name="outward"/>
        /// (0..1) from its usual pole toward <see cref="LeftElbowOutPole"/>.</summary>
        private float Arm(Matrix4x4[] world, bool left, Vector3 hand, bool rest, float outward)
        {
            Vector3 pole = Vector3.Lerp(rest ? LeftRestElbowPole : LeftElbowPole, LeftElbowOutPole, outward);
            if (!left) pole.x = -pole.x;
            int s = left ? _shoulderL : _shoulderR;
            return TwoBone(world, s, left ? _elbowL : _elbowR, left ? _palmL : _palmR, hand, Pos(world, s) + pole);
        }

        // The arms being cleared, for the probes below (set by Clear; read only inside it).
        private bool _armL, _armR, _restL, _restR;
        private Vector3 _handL, _handR;
        private float _outL, _outR;

        private void Clear(Matrix4x4[] world, in Targets t, bool armL, Vector3 handL, bool restL,
                           bool armR, Vector3 handR, bool restR, ref Result r)
        {
            float need = t.BodyClearanceMeters;
            _armL = armL; _armR = armR; _restL = restL; _restR = restR; _handL = handL; _handR = handR;
            _outL = _outR = 0f;

            // a. Elbows out, each arm only as far as it needs (the least step that clears, else the best).
            if (armL && ArmSlack(world, true, need) < 0f) _outL = SwingOut(world, true, handL, restL, need);
            if (armR && ArmSlack(world, false, need) < 0f) _outR = SwingOut(world, false, handR, restR, need);
            r.ElbowOutLeft = _outL;
            r.ElbowOutRight = _outR;

            float slack = Slack(world, in t, armL, armR, need);
            if (slack < 0f)
            {
                // b. Lean back, by the least angle (bisected) that clears, within the reach of both grips.
                Vector3 pivot = Pos(world, _spine), axis = AxisX(world, _spine);
                float cap = ReachCap(world, in t, Quaternion.identity, axis, t.BodyClearLeanMaxDegrees, need);
                float lean = Least(world, in t, Quaternion.identity, axis, cap, need, out slack);

                // c. Still not clear (a figure turned on the hull, a broad trunk): the shoulders turn with
                // the handles, by the least twist either way that clears, within reach.
                float twist = 0f;
                if (slack < 0f && t.BodyClearTwistMaxDegrees > 0f)
                {
                    Quaternion leanQ = Quaternion.AngleAxis(lean, axis);
                    float bestSlack = slack;
                    for (int sign = -1; sign <= 1; sign += 2)
                    {
                        float tcap = ReachCap(world, in t, leanQ, sign * Vector3.forward, t.BodyClearTwistMaxDegrees, need);
                        float tw = Least(world, in t, leanQ, sign * Vector3.forward, tcap, need, out float s);
                        if (s > bestSlack + Tiny || (s >= 0f && bestSlack >= 0f && tw < Mathf.Abs(twist)))
                        {
                            bestSlack = s;
                            twist = sign * tw;
                        }
                    }
                    slack = bestSlack;
                }

                if (lean != 0f || twist != 0f)
                {
                    Quaternion q = Quaternion.AngleAxis(twist, Vector3.forward) * Quaternion.AngleAxis(lean, axis);
                    TurnAndHold(world, pivot, q);
                }
                r.ClearLeanDegrees = lean;
                r.ClearTwistDegrees = twist;
            }
            r.ClearanceLeft = armL ? ArmClearance(world, true) : float.PositiveInfinity;
            r.ClearanceRight = armR ? ArmClearance(world, false) : float.PositiveInfinity;
        }

        /// <summary>The least clearance from the torso of the handle ends of the oars he holds, metres;
        /// +inf with no body volume or no grip.</summary>
        public float HandleEndClearance(Matrix4x4[] world, in Targets t)
        {
            float c = float.PositiveInfinity;
            if (_body == null) return c;
            if (t.HasGripLeft) c = Mathf.Min(c, _body.Outside(world, t.HandleEndLeft));
            if (t.HasGripRight) c = Mathf.Min(c, _body.Outside(world, t.HandleEndRight));
            return c;
        }

        /// <summary>
        /// Turn the trunk on a copy of <paramref name="world"/> by <paramref name="degrees"/> about
        /// <paramref name="axis"/> on top of <paramref name="before"/>, re-solve the arms, and answer the
        /// clear slack; <paramref name="reachUse"/> is the held grips' worst distance over the arm's length.
        /// </summary>
        private float Probe(Matrix4x4[] world, in Targets t, Quaternion before, Vector3 axis, float degrees,
                            float need, out float reachUse)
        {
            System.Array.Copy(world, _scratch, _parent.Length);
            reachUse = TurnAndHold(_scratch, Pos(world, _spine), Quaternion.AngleAxis(degrees, axis) * before);
            return Slack(_scratch, in t, _armL, _armR, need);
        }

        /// <summary>The most turn (up to <paramref name="max"/>) that takes no held grip further out of
        /// reach than it already was (a grip in reach stays in reach).</summary>
        private float ReachCap(Matrix4x4[] world, in Targets t, Quaternion before, Vector3 axis, float max, float need)
        {
            if (max <= 0f) return 0f;
            Probe(world, in t, before, axis, 0f, need, out float baseUse);
            float allowed = Mathf.Max(1f, baseUse) + ReachUseSlack;
            Probe(world, in t, before, axis, max, need, out float use);
            if (use <= allowed) return max;
            float lo = 0f, hi = max;
            for (int i = 0; i < ReachLeanIterations; i++)
            {
                float mid = 0.5f * (lo + hi);
                Probe(world, in t, before, axis, mid, need, out use);
                if (use <= allowed) lo = mid;
                else hi = mid;
            }
            return lo;
        }

        /// <summary>The least turn in [0, <paramref name="cap"/>] that clears (bisected), or the cap when
        /// none does and it helps, else none.</summary>
        private float Least(Matrix4x4[] world, in Targets t, Quaternion before, Vector3 axis, float cap, float need,
                            out float slack)
        {
            float none = Probe(world, in t, before, axis, 0f, need, out _);
            if (cap <= 0f) { slack = none; return 0f; }
            float atCap = Probe(world, in t, before, axis, cap, need, out _);
            if (atCap < 0f)
            {
                if (atCap > none) { slack = atCap; return cap; }
                slack = none;
                return 0f;
            }
            float lo = 0f, hi = cap;
            for (int i = 0; i < ReachLeanIterations; i++)
            {
                float mid = 0.5f * (lo + hi);
                if (Probe(world, in t, before, axis, mid, need, out _) >= 0f) hi = mid;
                else lo = mid;
            }
            slack = Probe(world, in t, before, axis, hi, need, out _);
            return hi;
        }

        private float SwingOut(Matrix4x4[] world, bool left, Vector3 hand, bool rest, float need)
        {
            float best = ArmSlack(world, left, need), bestOut = 0f;
            for (int k = 1; k <= ElbowOutSteps; k++)
            {
                float f = k / (float)ElbowOutSteps;
                Arm(world, left, hand, rest, f);
                float c = ArmSlack(world, left, need);
                if (c > best) { best = c; bestOut = f; }
                if (c >= 0f) break;
            }
            Arm(world, left, hand, rest, bestOut);
            return bestOut;
        }

        /// <summary>Turn the trunk by <paramref name="q"/> about <paramref name="pivot"/> and re-solve the
        /// arms being cleared; false when a held grip is then out of reach.</summary>
        private float TurnAndHold(Matrix4x4[] world, Vector3 pivot, Quaternion q)
        {
            Rotate(world, _spine, pivot, q);
            float use = 0f;
            if (_armL) { float u = Arm(world, true, _handL, _restL, _outL); if (!_restL) use = Mathf.Max(use, u); }
            if (_armR) { float u = Arm(world, false, _handR, _restR, _outR); if (!_restR) use = Mathf.Max(use, u); }
            return use;
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
