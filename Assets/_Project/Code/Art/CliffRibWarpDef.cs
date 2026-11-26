using System;
using UnityEngine;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// One wave of the cliff rib warp: a straight sine ridge across the plan, so many metres from crest to crest, so
    /// strong, running at an angle, starting at a phase. See <see cref="CliffRibWarpDef"/>.
    /// </summary>
    [Serializable]
    public struct CliffRibWarpWave
    {
        [Tooltip("Plan metres from crest to crest. Keep it off every whole fraction and multiple of the kit's 12 m tile " +
                 "(3, 4, 6, 12, 24...) or the wave lines up with the ribs it is meant to break.")]
        [Min(0f)] public float WavelengthMetres;
        [Tooltip("For a shift wave: metres along the shore the tile is slid either way. For a depth wave: the share the " +
                 "relief is deepened or flattened either way.")]
        [Min(0f)] public float Amplitude;
        [Tooltip("The direction the wave travels across the plan, degrees from +x. Three waves at 0, 60 and 120 per scale " +
                 "mean no run of coast, whichever way it faces, slips between them.")]
        public float AngleDegrees;
        [Tooltip("Where the wave starts, in turns (0..1).")]
        public float PhaseTurns;

        public CliffRibWarpWave(float wavelengthMetres, float amplitude, float angleDegrees, float phaseTurns)
        {
            WavelengthMetres = wavelengthMetres;
            Amplitude = amplitude;
            AngleDegrees = angleDegrees;
            PhaseTurns = phaseTurns;
        }
    }

    /// <summary>The warp a cliff chunk lays its face with (see <see cref="CliffRibWarpDef"/>). <c>default</c> is off: every
    /// face, decal and UV is laid exactly as before the warp, bit for bit.</summary>
    public struct CliffRibWarp
    {
        /// <summary>Waves that slide the tile coordinate along the shore, metres. Null = no slide.</summary>
        public CliffRibWarpWave[] Shift;
        /// <summary>Waves that deepen or flatten the profile's relief, as a share. Null = the kit's own depth.</summary>
        public CliffRibWarpWave[] Depth;
        /// <summary>The depth gain is held between these.</summary>
        public float DepthMin, DepthMax;
        /// <summary>The slide steps in whole multiples of this, metres: the profile's own texel along the shore, so a
        /// rib only ever moves by whole art texels (no mixels). 0 = unstepped (tests only).</summary>
        public float StepMetres;

        public bool IsOff => Shift == null && Depth == null;
    }

    /// <summary>
    /// ⭐ <b>THE CLIFF RIB WARP: THE KIT'S 4 m RIB, NEVER AT A FIXED PITCH.</b> The cliff kit's plan-displacement profile is
    /// periodic in its 12 m tile, and its strongest harmonic is the third: a rib (a scallop in the foot, seen from above)
    /// every 4.00 m, about 0.4 to 0.7 m peak to peak, in every profile the kit bakes. Laid down a long run, that is a row
    /// of identical scallops at one pitch: the repeat the owner flagged on St Peters' east bank (11-20).
    ///
    /// <para><b>Two dials break it, both functions of the plan position alone</b>, so the two copies of a station that
    /// neighbouring chunks share agree to the bit and nothing tears at a cut (the rule
    /// <see cref="CliffWallGeometry.TileU"/> states):</para>
    /// <list type="number">
    /// <item><description><b>Shift</b>, a domain warp of the coordinate the PROFILE is read at along the shore:
    /// <c>uRelief = (along + Shift(plan)) / faceMetresS</c>, the shift stepped in whole profile texels. The painted face
    /// keeps its own unwarped <c>u</c>, so no art texel is ever stretched or squeezed (one pixel size on screen, the
    /// no-mixels rule); only where each rib's relief falls moves, so the spacing wanders and the 12 m tile never comes
    /// round again in the outline.</description></item>
    /// <item><description><b>Depth</b>, a gain on the profile's displacement, so one rib stands proud and the next is
    /// nearly flush: the scallops stop being the same shape.</description></item>
    /// </list>
    ///
    /// <para><b>The one law:</b> the shift must never fold the relief back on itself, so its steepest slope along any
    /// line (<see cref="CliffRibWarpMath.MaxSlope"/>, an upper bound) is held to <see cref="MaxSlopeAllowed"/>, the most
    /// a rib is ever widened or narrowed. Waves that sum steeper are scaled down to it, never trusted.</para>
    ///
    /// <para>Presentation only: no collider, walk rule or save reads it; the blockers stand on the stations as before.
    /// Deterministic: plan position in, metres out, no randomness. Read once per chunk rebuild, never per frame.</para>
    /// </summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/Cliff Rib Warp", fileName = "CliffRibWarpDef")]
    public sealed class CliffRibWarpDef : ScriptableObject
    {
        /// <summary>The Resources path the cliff walls load it from.</summary>
        public const string ResourcePath = "CliffRibWarpDef";

        /// <summary>The hard ceiling on <see cref="MaxSlopeAllowed"/>: 1 would fold the face.</summary>
        public const float SlopeCeiling = 0.9f;

        public string Id = "cliff_rib_warp.default";

        [Tooltip("Off draws every cliff exactly as before the warp, bit for bit.")]
        public bool Enabled = true;

        [Header("Shift: where along the shore each rib falls")]
        [Tooltip("The steepest the shift may slide the tile along the shore, metres per metre: the most a texel is " +
                 "stretched or squeezed. Waves whose sum is steeper are scaled down to it.")]
        [Range(0f, SlopeCeiling)] public float MaxSlopeAllowed = 0.45f;
        [Tooltip("The shift waves, summed. Three per scale, at 0, 60 and 120 degrees.")]
        public CliffRibWarpWave[] ShiftWaves = new CliffRibWarpWave[0];

        [Header("Depth: how proud each rib stands")]
        [Tooltip("The depth waves, summed onto a gain of 1. Three per scale, at 0, 60 and 120 degrees.")]
        public CliffRibWarpWave[] DepthWaves = new CliffRibWarpWave[0];
        [Tooltip("The gain is never below this (0 = a rib may go flush).")]
        [Min(0f)] public float DepthGainMin = 0.35f;
        [Tooltip("The gain is never above this.")]
        [Min(0f)] public float DepthGainMax = 1.3f;

        [TextArea] public string Why = "";

        /// <summary>The warp the walls draw with: off when disabled, the shift scaled down to
        /// <see cref="MaxSlopeAllowed"/>. Fresh arrays; call it once per build, never per frame.</summary>
        public CliffRibWarp Drawn()
        {
            if (!Enabled) return default;
            var warp = new CliffRibWarp
            {
                Shift = ShiftWaves != null && ShiftWaves.Length > 0 ? (CliffRibWarpWave[])ShiftWaves.Clone() : null,
                Depth = DepthWaves != null && DepthWaves.Length > 0 ? (CliffRibWarpWave[])DepthWaves.Clone() : null,
                DepthMin = Mathf.Max(0f, DepthGainMin),
                DepthMax = Mathf.Max(Mathf.Max(0f, DepthGainMin), DepthGainMax),
            };
            if (warp.Shift != null)
            {
                float slope = CliffRibWarpMath.MaxSlope(warp.Shift);
                float cap = Mathf.Clamp(MaxSlopeAllowed, 0f, SlopeCeiling);
                if (slope > cap)
                {
                    float k = cap / slope;
                    for (int i = 0; i < warp.Shift.Length; i++) warp.Shift[i].Amplitude *= k;
                }
            }
            return warp;
        }

        private static CliffRibWarpDef s_Current;

        /// <summary>The project's warp, loaded once from Resources. Null when none ships (then nothing warps).</summary>
        public static CliffRibWarpDef Current
        {
            get
            {
                if (s_Current == null) s_Current = Resources.Load<CliffRibWarpDef>(ResourcePath);
                return s_Current;
            }
        }

        /// <summary>The project's warp as the walls draw it; off when none ships.</summary>
        public static CliffRibWarp CurrentDrawn()
        {
            CliffRibWarpDef def = Current;
            return def != null ? def.Drawn() : default;
        }
    }

    /// <summary>The cliff rib warp's arithmetic, pure: see <see cref="CliffRibWarpDef"/>.</summary>
    public static class CliffRibWarpMath
    {
        /// <summary>The waves' sum at this plan point. Exactly 0 with no waves.</summary>
        public static float Sum(CliffRibWarpWave[] waves, Vector2 plan)
        {
            if (waves == null) return 0f;
            float sum = 0f;
            for (int i = 0; i < waves.Length; i++)
            {
                CliffRibWarpWave w = waves[i];
                if (!(w.WavelengthMetres > 0f) || !(w.Amplitude > 0f)) continue;
                float a = w.AngleDegrees * Mathf.Deg2Rad;
                float along = plan.x * Mathf.Cos(a) + plan.y * Mathf.Sin(a);
                sum += w.Amplitude * Mathf.Sin(2f * Mathf.PI * (along / w.WavelengthMetres + w.PhaseTurns));
            }
            return sum;
        }

        /// <summary>Metres along the shore the face's tile coordinate is slid at this plan point. Exactly 0 when off.</summary>
        public static float ShiftMetres(in CliffRibWarp warp, Vector2 plan)
        {
            if (warp.Shift == null) return 0f;
            float s = Sum(warp.Shift, plan);
            return warp.StepMetres > 0f ? Mathf.Round(s / warp.StepMetres) * warp.StepMetres : s;
        }

        /// <summary>The gain on the profile's displacement at this plan point. Exactly 1 when off.</summary>
        public static float DepthGain(in CliffRibWarp warp, Vector2 plan) =>
            warp.Depth == null ? 1f : Mathf.Clamp(1f + Sum(warp.Depth, plan), warp.DepthMin, warp.DepthMax);

        /// <summary>
        /// An upper bound on the waves' slope along ANY line in the plan, per metre: the sum of amplitude times
        /// wavenumber (each wave's slope along a line is that times the cosine to its direction, at most 1).
        /// </summary>
        public static float MaxSlope(CliffRibWarpWave[] waves)
        {
            if (waves == null) return 0f;
            float s = 0f;
            for (int i = 0; i < waves.Length; i++)
                if (waves[i].WavelengthMetres > 0f && waves[i].Amplitude > 0f)
                    s += waves[i].Amplitude * 2f * Mathf.PI / waves[i].WavelengthMetres;
            return s;
        }
    }
}
