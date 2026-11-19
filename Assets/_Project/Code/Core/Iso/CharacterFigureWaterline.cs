using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>How the sea meets a figure this frame.</summary>
    public enum FigureWater
    {
        /// <summary>On dry ground or a deck: no waterline.</summary>
        Dry = 0,
        /// <summary>Feet on the bottom: the line is the water's depth over his feet.</summary>
        Wade = 1,
        /// <summary>Prone and making way: the line rides just over his pelvis.</summary>
        Swim = 2,
        /// <summary>Upright, holding station: the line sits just under his shoulders.</summary>
        Tread = 3,
    }

    /// <summary>
    /// <b>Where the sea cuts the mesh figure</b>, in the figure's own frame (metres, z up from the
    /// ground plane the rig stands him on). The rig's own rule, read from the posed bones rather than
    /// from a table of numbers measured off an older sheet:
    ///
    /// <list type="bullet">
    /// <item><b>swim</b>: the pelvis height plus a small margin (rig 10 poses: <c>waterZ = Pz + 0.09</c>).</item>
    /// <item><b>tread</b>: the mean of the two shoulders, less a margin (rig 10 poses: <c>mean(shoulder z) − 0.05</c>).</item>
    /// <item><b>wade</b>: the water's depth over his feet, never above the tread line, so a deep wade
    /// can never drown his head.</item>
    /// </list>
    ///
    /// <para>The margins are <see cref="GameConfig"/> values; the bones are the skin's. Everything
    /// here is a pure function: no allocation (the caller owns the scratch array), no clock.</para>
    /// </summary>
    public static class CharacterFigureWaterline
    {
        public const string PelvisBone = "pelvis";
        public const string ShoulderLeftBone = "shoulder_L";
        public const string ShoulderRightBone = "shoulder_R";

        /// <summary>The mode for the band his feet are in and the clip being drawn. In the swim band
        /// the drawn state decides: the tread clip is upright, everything else is taken as prone.</summary>
        public static FigureWater ModeFor(OnFootWaterState state, string drawnKey)
        {
            if (state == OnFootWaterState.Wade) return FigureWater.Wade;
            if (state != OnFootWaterState.Swim) return FigureWater.Dry;
            return string.Equals(drawnKey, PlayerMeshRule.Tread, System.StringComparison.Ordinal)
                ? FigureWater.Tread : FigureWater.Swim;
        }

        /// <summary>
        /// The line from already-measured heights. False when there is no line to draw: dry, a wade
        /// with no water over his feet, or any input that is not a finite number.
        /// </summary>
        public static bool TryLine(FigureWater mode, float pelvisZ, float shoulderMeanZ, float wadeDepth,
                                   float swimAbovePelvis, float treadBelowShoulders, out float lineZ)
        {
            lineZ = 0f;
            float neck = shoulderMeanZ - treadBelowShoulders;
            switch (mode)
            {
                case FigureWater.Swim:
                    lineZ = pelvisZ + swimAbovePelvis;
                    break;
                case FigureWater.Tread:
                    lineZ = neck;
                    break;
                case FigureWater.Wade:
                    if (!Finite(wadeDepth) || wadeDepth <= 0f) return false;
                    lineZ = Mathf.Min(wadeDepth, neck);
                    break;
                default:
                    return false;
            }
            if (!Finite(lineZ)) { lineZ = 0f; return false; }
            return true;
        }

        /// <summary>
        /// The line for one frame of one clip of a skin: composes the frame into
        /// <paramref name="world"/> (at least <see cref="CharacterSkinDef.BoneCount"/> long) and reads
        /// the pelvis and shoulders from it. False when the skin lacks those bones or the clip is
        /// malformed, and then the caller draws no line.
        /// </summary>
        public static bool TryLine(CharacterSkinDef skin, in CharacterSkinDef.SkinClip clip, int frame,
                                   Matrix4x4[] world, FigureWater mode, float wadeDepth,
                                   float swimAbovePelvis, float treadBelowShoulders, out float lineZ)
        {
            lineZ = 0f;
            if (mode == FigureWater.Dry || skin == null || world == null) return false;
            int n = skin.BoneCount;
            if (n <= 0 || world.Length < n || !clip.KeysWellFormed(n)) return false;
            int pelvis = skin.IndexOfBone(PelvisBone);
            int left = skin.IndexOfBone(ShoulderLeftBone);
            int right = skin.IndexOfBone(ShoulderRightBone);
            if (pelvis < 0 || left < 0 || right < 0) return false;

            frame = Mathf.Clamp(frame, 0, clip.FrameCount - 1);
            CharacterSkinPose.ComposeWorld(clip, frame, skin.Bones, world);
            float pelvisZ = world[pelvis].m23;
            float shoulders = 0.5f * (world[left].m23 + world[right].m23);
            return TryLine(mode, pelvisZ, shoulders, wadeDepth, swimAbovePelvis, treadBelowShoulders, out lineZ);
        }

        private static bool Finite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);
    }

    /// <summary>
    /// <b>How the sea looks on the figure</b> where the waterline cuts it (ADR 0061). The default is
    /// the hard cut: nothing drawn under the line and no foam. The player's comes from
    /// <see cref="GameConfig"/> (<see cref="FromConfig"/>), so the owner tunes it with no code.
    /// </summary>
    public struct FigureWaterLook
    {
        /// <summary>True: the part under the line is drawn, tinted and dimmed toward the sea (the old
        /// sprite's submerged look). False: it is hidden.</summary>
        public bool ShowsUnderwater;
        /// <summary>The sea colour the submerged part tints toward.</summary>
        public Color Tint;
        /// <summary>0..1, how far the submerged part takes <see cref="Tint"/>.</summary>
        public float TintAmount;
        /// <summary>0..1, how much darker the submerged part draws.</summary>
        public float Dim;
        /// <summary>Height (m) of the pale foam band fading up from the line; 0 is a hard edge.</summary>
        public float FoamMetres;
        /// <summary>0..1, how pale the foam is right at the line.</summary>
        public float Foam;

        /// <summary>The look the shipped config asks for; the hard cut with no config.</summary>
        public static FigureWaterLook FromConfig(GameConfig config) => config == null ? default : new FigureWaterLook
        {
            ShowsUnderwater = config.FigureShowsUnderwater,
            Tint = config.FigureUnderwaterTint,
            TintAmount = config.FigureUnderwaterTintAmount,
            Dim = config.FigureUnderwaterDim,
            FoamMetres = config.FigureWaterlineFoamMetres,
            Foam = config.FigureWaterlineFoam,
        };
    }
}
