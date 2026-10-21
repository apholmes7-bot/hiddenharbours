using System;
using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>The rig's look-at (<c>LOOK</c> and <c>lookAt</c>, rig 9 README §5), ported verbatim.</b>
    ///
    /// <para><see cref="LookAt"/> is <c>CharacterIso9.lookAt(solvedFrame, target, share)</c> line for
    /// line, in doubles, in the rig's own frame and its own column-major matrix layout (the rig's
    /// <c>mV</c> and <c>mTV</c>), rounding its answers to three decimals as the rig's
    /// <c>toFixed(3)</c> does. A test runs the rig's own function in V8 over a grid and holds this to
    /// it; the golden check (share 1: the head's +y within 1.7° of a target 2 m away, inside the
    /// limits) runs against it too.</para>
    ///
    /// <para><b>Where the turn goes.</b> <see cref="TurnOf"/> is the rig's <c>lookE</c>:
    /// <c>E(y, p) = Rz(−y)·Rx(−p)</c> with the turn split 0.4 onto the neck and 0.6 onto the head
    /// (the def's <see cref="CharacterSkinDef.LookSplitNeck"/> and
    /// <see cref="CharacterSkinDef.LookSplitHead"/>), post-multiplied onto the clip's local rotations
    /// of the frame shown (<see cref="CharacterSkinPose.ApplyTurn"/>) — after the clip and before the
    /// rock, which is the hull's transform. In rig space <c>Rz(a)</c> is
    /// <c>AngleAxis(a, forward)</c> and <c>Rx(a)</c> is <c>AngleAxis(a, right)</c>: the rig's
    /// rotation matrices are the standard right-handed ones, and this repo keeps rig space
    /// verbatim.</para>
    ///
    /// <para><b>The eyes lead.</b> With the def's <see cref="CharacterSkinDef.LookHeadShare"/> (0.7)
    /// the head takes most of the needed turn and the gaze states <c>eyes.left</c> /
    /// <c>eyes.right</c> take over past <see cref="CharacterSkinDef.LookEyesBeyondDeg"/> of
    /// residual (<see cref="Result.Gaze"/>).</para>
    /// </summary>
    public static class CharacterFigureLook
    {
        /// <summary>The gaze: the clip's own eyes.</summary>
        public const int GazeOpen = 0;

        /// <summary>The gaze: <c>eyes.left</c>.</summary>
        public const int GazeLeft = -1;

        /// <summary>The gaze: <c>eyes.right</c>.</summary>
        public const int GazeRight = 1;

        const double Deg = Math.PI / 180.0;

        /// <summary>A point or direction in the rig's frame, in doubles.</summary>
        public readonly struct Vec
        {
            public readonly double X, Y, Z;

            public Vec(double x, double y, double z) { X = x; Y = y; Z = z; }

            public static Vec Of(Vector3 v) => new Vec(v.x, v.y, v.z);

            public static Vec operator +(Vec a, Vec b) => new Vec(a.X + b.X, a.Y + b.Y, a.Z + b.Z);

            public static Vec operator -(Vec a, Vec b) => new Vec(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        }

        /// <summary>A rotation in the rig's own layout: nine doubles, column-major, so
        /// <see cref="Apply"/> is the rig's <c>mV(R, v)</c> and <see cref="ApplyInverse"/> its
        /// <c>mTV(R, v)</c>.</summary>
        public readonly struct Rot
        {
            readonly double _0, _1, _2, _3, _4, _5, _6, _7, _8;

            public Rot(double m0, double m1, double m2, double m3, double m4, double m5,
                       double m6, double m7, double m8)
            {
                _0 = m0; _1 = m1; _2 = m2; _3 = m3; _4 = m4; _5 = m5; _6 = m6; _7 = m7; _8 = m8;
            }

            /// <summary>The rotation part of a TRS matrix, in the rig's column-major order.</summary>
            public static Rot Of(Matrix4x4 m) =>
                new Rot(m.m00, m.m10, m.m20, m.m01, m.m11, m.m21, m.m02, m.m12, m.m22);

            /// <summary>The rig's <c>mV</c>: R·v.</summary>
            public Vec Apply(Vec v) => new Vec(
                _0 * v.X + _3 * v.Y + _6 * v.Z,
                _1 * v.X + _4 * v.Y + _7 * v.Z,
                _2 * v.X + _5 * v.Y + _8 * v.Z);

            /// <summary>The rig's <c>mTV</c>: Rᵀ·v.</summary>
            public Vec ApplyInverse(Vec v) => new Vec(
                _0 * v.X + _1 * v.Y + _2 * v.Z,
                _3 * v.X + _4 * v.Y + _5 * v.Z,
                _6 * v.X + _7 * v.Y + _8 * v.Z);
        }

        /// <summary>The rig's <c>LOOK</c>, as the def baked it.</summary>
        public readonly struct Limits
        {
            public readonly double YawMin, YawMax, PitchMin, PitchMax, EyesBeyond, HeadShare;

            public Limits(double yawMin, double yawMax, double pitchMin, double pitchMax,
                          double eyesBeyond, double headShare)
            {
                YawMin = yawMin; YawMax = yawMax; PitchMin = pitchMin; PitchMax = pitchMax;
                EyesBeyond = eyesBeyond; HeadShare = headShare;
            }

            public static Limits Of(CharacterSkinDef def) => new Limits(
                def.LookYawLimits.x, def.LookYawLimits.y, def.LookPitchLimits.x, def.LookPitchLimits.y,
                def.LookEyesBeyondDeg, def.LookHeadShare);
        }

        /// <summary>What <c>lookAt</c> returns, in degrees, rounded as the rig rounds it.</summary>
        public struct Result
        {
            /// <summary>The turn's yaw: + turns the face toward the figure's right.</summary>
            public double Yaw;

            /// <summary>The turn's pitch: + tips the face down.</summary>
            public double Pitch;

            /// <summary>The whole turn the target needs, before the share and the limits.</summary>
            public double NeedYaw, NeedPitch;

            /// <summary>The yaw the head leaves to the eyes.</summary>
            public double ResidualYaw;

            /// <summary><see cref="GazeOpen"/>, <see cref="GazeLeft"/> or <see cref="GazeRight"/>.</summary>
            public int Gaze;
        }

        /// <summary>
        /// The rig's <c>lookAt(S, target, share)</c>. Every argument is in the figure's frame, unrocked,
        /// after the clip has posed the bones: the chest's world rotation, the head's world rotation and
        /// position, the head's mid point in the head's frame (<c>B.D.headMid</c>), and the target.
        /// <paramref name="share"/> is the head's share of the needed turn (the rig's default is
        /// <see cref="Limits.HeadShare"/>).
        /// </summary>
        public static Result LookAt(Rot chest, Rot head, Vec headPosition, Vec headMid, Vec target,
                                    double share, Limits limits)
        {
            Vec eye = headPosition + head.Apply(headMid);
            Vec d = chest.ApplyInverse(target - eye);
            Vec f = chest.ApplyInverse(head.Apply(new Vec(0, 1, 0)));

            double dy = YawOf(d) - YawOf(f);
            dy = ((dy % 360) + 540) % 360 - 180;
            double dp = PitchOf(d) - PitchOf(f);
            double yaw = Clamp(dy * share, limits.YawMin, limits.YawMax);
            double pitch = Clamp(dp * share, limits.PitchMin, limits.PitchMax);
            double ry = dy - yaw;

            return new Result
            {
                Yaw = Fixed3(yaw),
                Pitch = Fixed3(pitch),
                NeedYaw = Fixed3(dy),
                NeedPitch = Fixed3(dp),
                ResidualYaw = Fixed3(ry),
                Gaze = ry > limits.EyesBeyond ? GazeRight : ry < -limits.EyesBeyond ? GazeLeft : GazeOpen,
            };
        }

        /// <summary>
        /// The rig's <c>lookE</c> for one bone: <c>E(w·yaw, w·pitch) = Rz(−w·yaw)·Rx(−w·pitch)</c>, the
        /// turn clamped to the limits first as the rig clamps it, in rig space. <paramref name="share"/>
        /// is the bone's split (0.4 neck, 0.6 head).
        /// </summary>
        public static Quaternion TurnOf(double yaw, double pitch, double share, Limits limits)
        {
            double y = Clamp(yaw, limits.YawMin, limits.YawMax) * share;
            double p = Clamp(pitch, limits.PitchMin, limits.PitchMax) * share;
            return Quaternion.AngleAxis(-(float)y, Vector3.forward) *
                   Quaternion.AngleAxis(-(float)p, Vector3.right);
        }

        /// <summary>The rig's <c>yawOf</c>: <c>atan2(v.x, v.y)</c> in degrees.</summary>
        public static double YawOf(Vec v) => Math.Atan2(v.X, v.Y) / Deg;

        /// <summary>The rig's <c>pitchOf</c>: <c>−atan2(v.z, |v.xy|)</c> in degrees.</summary>
        public static double PitchOf(Vec v) => -Math.Atan2(v.Z, Hypot(v.X, v.Y)) / Deg;

        static double Clamp(double v, double a, double b) => v < a ? a : v > b ? b : v;

        // Math.hypot's scaling, as RigMeshBuilder.Hypot3 does it for three.
        static double Hypot(double x, double y)
        {
            x = Math.Abs(x); y = Math.Abs(y);
            double max = Math.Max(x, y);
            if (max == 0.0) return 0.0;
            x /= max; y /= max;
            return max * Math.Sqrt(x * x + y * y);
        }

        // The rig's +v.toFixed(3): three decimals, a half away from zero.
        static double Fixed3(double v) => Math.Round(v * 1000.0, MidpointRounding.AwayFromZero) / 1000.0;
    }
}
