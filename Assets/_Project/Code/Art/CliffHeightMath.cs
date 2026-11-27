using UnityEngine;

namespace HiddenHarbours.Art
{
    /// <summary>The numbers a region's walls are given height with (<see cref="CliffFaceMixDef"/>, the Joint and
    /// FaceRamp fields): upright joints and a top-light, foot-dark ramp, both cut on the ground's texel grid.</summary>
    public readonly struct CliffHeight
    {
        /// <summary>Ground texels between two joints across the screen; 0 = no joints.</summary>
        public readonly int JointSpacingTexels;
        /// <summary>Rows one run of a joint covers.</summary>
        public readonly int JointRunTexels;
        /// <summary>The share of runs drawn, and the share that step a texel aside.</summary>
        public readonly float JointRunShare, JointStepShare;
        /// <summary>Whether the texel right of a joint is drawn one band up.</summary>
        public readonly bool JointLitEdge;
        /// <summary>The shares of the face, from the brow and from the foot, drawn one band up and one band down.</summary>
        public readonly float RampTopShare, RampFootShare;
        /// <summary>How far a ramp's edge wanders column by column, as a share of the face.</summary>
        public readonly float RampJitter;

        public CliffHeight(int jointSpacingTexels, int jointRunTexels, float jointRunShare, float jointStepShare,
                           bool jointLitEdge, float rampTopShare, float rampFootShare, float rampJitter)
        {
            // A joint needs a texel either side of it inside its stretch, so a stretch is at least three wide.
            JointSpacingTexels = jointSpacingTexels <= 0 ? 0 : Mathf.Max(3, jointSpacingTexels);
            JointRunTexels = Mathf.Max(1, jointRunTexels);
            JointRunShare = Mathf.Clamp01(jointRunShare);
            JointStepShare = Mathf.Clamp01(jointStepShare);
            JointLitEdge = jointLitEdge;
            RampTopShare = Mathf.Clamp01(rampTopShare);
            RampFootShare = Mathf.Clamp01(rampFootShare);
            RampJitter = Mathf.Clamp(rampJitter, 0f, 0.5f);
        }

        public bool JointsOn => JointSpacingTexels > 0 && JointRunShare > 0f;
        public bool RampOn => RampTopShare > 0f || RampFootShare > 0f;

        public static CliffHeight From(CliffFaceMixDef mix) => mix == null
            ? default
            : new CliffHeight(mix.JointSpacingTexels, mix.JointRunTexels, mix.JointRunShare, mix.JointStepShare,
                              mix.JointLitEdge, mix.FaceRampTopShare, mix.FaceRampFootShare, mix.FaceRampJitter);

        /// <summary>The shader's _JointA: spacing, run, run share, step share.</summary>
        public Vector4 ShaderJointA => new Vector4(JointSpacingTexels, JointRunTexels, JointRunShare, JointStepShare);
        /// <summary>The shader's _JointB: lit edge, on (only where <paramref name="on"/>, a face laid on the screen grid).</summary>
        public Vector4 ShaderJointB(bool on) => new Vector4(JointLitEdge ? 1f : 0f, on && JointsOn ? 1f : 0f, 0f, 0f);
        /// <summary>The shader's _FaceRamp: top share, foot share, jitter, on.</summary>
        public Vector4 ShaderRamp(bool on) => new Vector4(RampTopShare, RampFootShare, RampJitter, on && RampOn ? 1f : 0f);
    }

    /// <summary>
    /// Height on a wall laid on the screen grid (owner 11 27: the walls lost their height; keep the new look).
    ///
    /// <para>A wall laid on the screen grid has level bedding and square texels, so nothing on it runs DOWN the face:
    /// it reads as a flat band of brickwork. Two things give it back its height, both whole texels on the ground's
    /// grid. Upright JOINTS: one-texel cracks that run down the face in long runs, break off and pick up, and now
    /// and then step a texel aside, each at a place fixed by the hash of its stretch of world x. And a RAMP: the top
    /// of the face one palette band lighter and its foot one band darker, judged on the whole face (brow 0, foot 1)
    /// with a ragged edge column by column. Both are colours the palette already holds.</para>
    ///
    /// <para>Pure and deterministic. Twin of the HiddenHarboursCliffFace shader's CliffJoint and CliffRampShift.</para>
    /// </summary>
    public static class CliffHeightMath
    {
        public const int None = 0, Crack = 1, LitEdge = 2;
        const float SaltRun = 41f, SaltStep = 42f, SaltSide = 43f, SaltPlace = 44f, SaltPhase = 45f, SaltRamp = 46f;

        static float Hash(float x, float y, float salt) => CliffWaterlineMath.Hash01(new Vector2(x, y), salt);

        /// <summary>The joint's column in a run of its stretch, or int.MinValue where that run is a gap.</summary>
        public static int JointColumn(in CliffHeight h, int zone, int run)
        {
            if (!h.JointsOn) return int.MinValue;
            if (Hash(zone, run, SaltRun) >= h.JointRunShare) return int.MinValue;
            int place = 1 + Mathf.FloorToInt(Hash(zone, 0f, SaltPlace) * (h.JointSpacingTexels - 2));
            int step = Hash(zone, run, SaltStep) < h.JointStepShare ? (Hash(zone, run, SaltSide) < 0.5f ? -1 : 1) : 0;
            return zone * h.JointSpacingTexels + place + step;
        }

        /// <summary>What the ground texel (x, y) draws: <see cref="None"/>, <see cref="Crack"/> or <see cref="LitEdge"/>.
        /// y counts texel rows of world y; each stretch's runs start at their own phase, so no two break level.</summary>
        public static int JointAt(in CliffHeight h, int x, int y)
        {
            if (!h.JointsOn) return None;
            int s = h.JointSpacingTexels;
            int zone = Mathf.FloorToInt(x / (float)s);
            for (int dz = 0; dz >= -1; dz--)
            {
                // A step can carry a joint one texel past its stretch's edge, so the stretch to the left is asked too.
                int z = zone + dz;
                int run = Mathf.FloorToInt((y + Mathf.Floor(Hash(z, 1f, SaltPhase) * h.JointRunTexels)) / h.JointRunTexels);
                int jx = JointColumn(in h, z, run);
                if (jx == int.MinValue) continue;
                if (x == jx) return Crack;
                if (h.JointLitEdge && x == jx + 1) return LitEdge;
            }
            return None;
        }

        /// <summary>The palette band step the ramp gives a texel at face depth <paramref name="faceT"/> (0 brow, 1 foot)
        /// in ground texel column <paramref name="column"/>: +1, 0 or -1.</summary>
        public static int RampShift(in CliffHeight h, float faceT, int column)
        {
            if (!h.RampOn) return 0;
            float wander = (Hash(column, 2f, SaltRamp) - 0.5f) * 2f * h.RampJitter;
            if (h.RampTopShare > 0f && faceT < h.RampTopShare + wander) return 1;
            if (h.RampFootShare > 0f && faceT > 1f - h.RampFootShare + wander) return -1;
            return 0;
        }
    }
}
