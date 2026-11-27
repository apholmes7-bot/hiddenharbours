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

        /// <summary>The share of the face over the foot also drawn one shade tier down.</summary>
        public readonly float RampDeepShare;
        /// <summary>Ground texels per buttress across the screen; 0 = none.</summary>
        public readonly int ButtressPitchTexels;
        /// <summary>The shaded side's share of its buttress, min and max.</summary>
        public readonly float ButtressShadeMin, ButtressShadeMax;
        /// <summary>Rows per bed of a buttress edge, and how far the edge jogs at each bed.</summary>
        public readonly int ButtressBedTexels, ButtressJogTexels;
        /// <summary>Whether walls not laid on the screen grid take the ramp and the buttresses too.</summary>
        public readonly bool OnShearedWalls;

        public CliffHeight(int jointSpacingTexels, int jointRunTexels, float jointRunShare, float jointStepShare,
                           bool jointLitEdge, float rampTopShare, float rampFootShare, float rampJitter)
            : this(jointSpacingTexels, jointRunTexels, jointRunShare, jointStepShare, jointLitEdge,
                   rampTopShare, rampFootShare, rampJitter, 0f, 0, Vector2.zero, 1, 0, false) { }

        public CliffHeight(int jointSpacingTexels, int jointRunTexels, float jointRunShare, float jointStepShare,
                           bool jointLitEdge, float rampTopShare, float rampFootShare, float rampJitter,
                           float rampDeepShare, int buttressPitchTexels, Vector2 buttressShadeShare,
                           int buttressBedTexels, int buttressJogTexels, bool onShearedWalls)
        {
            RampDeepShare = Mathf.Clamp01(rampDeepShare);
            ButtressJogTexels = Mathf.Max(0, buttressJogTexels);
            // A buttress needs room for its shade and a jog's margin either side of it.
            ButtressPitchTexels = buttressPitchTexels <= 0 ? 0 : Mathf.Max(4 * ButtressJogTexels + 6, buttressPitchTexels);
            ButtressShadeMin = Mathf.Clamp01(Mathf.Min(buttressShadeShare.x, buttressShadeShare.y));
            ButtressShadeMax = Mathf.Clamp01(Mathf.Max(buttressShadeShare.x, buttressShadeShare.y));
            ButtressBedTexels = Mathf.Max(1, buttressBedTexels);
            OnShearedWalls = onShearedWalls;
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
        public bool RampOn => RampTopShare > 0f || RampFootShare > 0f || RampDeepShare > 0f;
        public bool ButtressOn => ButtressPitchTexels > 0 && ButtressShadeMax > 0f;

        public static CliffHeight From(CliffFaceMixDef mix) => mix == null
            ? default
            : new CliffHeight(mix.JointSpacingTexels, mix.JointRunTexels, mix.JointRunShare, mix.JointStepShare,
                              mix.JointLitEdge, mix.FaceRampTopShare, mix.FaceRampFootShare, mix.FaceRampJitter,
                              mix.FaceRampDeepShare, mix.ButtressPitchTexels, mix.ButtressShadeShare,
                              mix.ButtressBedTexels, mix.ButtressJogTexels, mix.HeightOnShearedWalls);

        /// <summary>Whether a wall's face takes the ramp and the buttresses: a face band (never a decal), laid on the
        /// screen grid, or not laid on it where the mix says <see cref="OnShearedWalls"/>. Joints stay screen grid only.</summary>
        public bool MassOn(bool planar, bool decal) => !decal && (planar || OnShearedWalls);

        /// <summary>The shader's _JointA: spacing, run, run share, step share.</summary>
        public Vector4 ShaderJointA => new Vector4(JointSpacingTexels, JointRunTexels, JointRunShare, JointStepShare);
        /// <summary>The shader's _JointB: lit edge, on (only where <paramref name="on"/>, a face laid on the screen grid).</summary>
        public Vector4 ShaderJointB(bool on) => new Vector4(JointLitEdge ? 1f : 0f, on && JointsOn ? 1f : 0f, 0f, 0f);
        /// <summary>The shader's _FaceRamp: top share, foot share, jitter, on.</summary>
        public Vector4 ShaderRamp(bool on) => new Vector4(RampTopShare, RampFootShare, RampJitter, on && RampOn ? 1f : 0f);
        /// <summary>The shader's _Buttress: pitch, shade min, shade max, on.</summary>
        public Vector4 ShaderButtress(bool on) =>
            new Vector4(ButtressPitchTexels, ButtressShadeMin, ButtressShadeMax, on && ButtressOn ? 1f : 0f);
        /// <summary>The shader's _ButtressB: bed rows, jog, the ramp's deep share, 0.</summary>
        public Vector4 ShaderButtressB => new Vector4(ButtressBedTexels, ButtressJogTexels, RampDeepShare, 0f);
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
        const float SaltRun = 41f, SaltStep = 42f, SaltSide = 43f, SaltPlace = 44f, SaltPhase = 45f, SaltRamp = 46f,
                    SaltButtress = 47f, SaltJogLeft = 48f, SaltJogRight = 49f;

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

        /// <summary>Shade tiers down the ramp's deep foot gives a texel: 1 in the bottom <see cref="CliffHeight.RampDeepShare"/>
        /// of the face (its edge wandering as the ramp's does), else 0.</summary>
        public static int RampDeep(in CliffHeight h, float faceT, int column)
        {
            if (h.RampDeepShare <= 0f) return 0;
            float wander = (Hash(column, 3f, SaltRamp) - 0.5f) * 2f * h.RampJitter;
            return faceT > 1f - h.RampDeepShare + wander ? 1 : 0;
        }

        static int Round(float v) => Mathf.FloorToInt(v + 0.5f);

        /// <summary>
        /// Whether ground texel (x, y) lies in the shaded side of its buttress (1) or not (0). The face is cut into
        /// buttresses <see cref="CliffHeight.ButtressPitchTexels"/> wide across the screen; each has a shaded strip of
        /// its own width (a hashed share between min and max) at its own place inside it, kept a jog's margin clear of
        /// the buttress's edges so no line ever runs at a fixed column. Both edges of the strip jog sideways by up to
        /// <see cref="CliffHeight.ButtressJogTexels"/> at every bed, each bed starting at the buttress's own phase.
        /// </summary>
        public static int ButtressShade(in CliffHeight h, int x, int y)
        {
            if (!h.ButtressOn) return 0;
            int p = h.ButtressPitchTexels, jog = h.ButtressJogTexels, margin = jog + 1;
            int cell = Mathf.FloorToInt(x / (float)p);
            float share = Mathf.Lerp(h.ButtressShadeMin, h.ButtressShadeMax, Hash(cell, 4f, SaltButtress));
            int w = Mathf.Clamp(Round(share * p), 1, p - 2 * margin);
            int slack = p - w - 2 * margin;
            int a = cell * p + margin + Mathf.FloorToInt(Hash(cell, 5f, SaltButtress) * (slack + 1));
            int bedT = h.ButtressBedTexels;
            int bed = Mathf.FloorToInt((y + Mathf.Floor(Hash(cell, 6f, SaltButtress) * bedT)) / bedT);
            int jl = Round((Hash(cell, bed, SaltJogLeft) - 0.5f) * 2f * jog);
            int jr = Round((Hash(cell, bed, SaltJogRight) - 0.5f) * 2f * jog);
            return x >= a + jl && x < a + w + jr ? 1 : 0;
        }
    }
}
