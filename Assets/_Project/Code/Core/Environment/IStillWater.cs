using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// The <b>still-water source</b> for the active region (ADR 0046) — the fresh water that stands
    /// <b>above the tide</b>: a pond in the bog, a brook's fresh reach, a pan on the marsh. The tide model
    /// (<see cref="IEnvironmentService.WaterLevelAt"/>) gives ONE level for a whole region, so above the
    /// highest spring tide nothing could hold water; this seam gives a level per plan point wherever the
    /// region's data says water stands, and <see cref="StillWaterLevels.Compose"/> is the one rule that
    /// joins the two: <b>water = max(tide, still)</b>.
    ///
    /// <para><b>Frame &amp; sign.</b> <see cref="StillLevelAt"/> returns the still surface in <b>metres
    /// above chart datum</b> — the frame <see cref="ITidalTerrain.ElevationAt"/> and the tide already use —
    /// or <see cref="StillWaterLevels.None"/> (negative infinity) where no still water stands. A level at or
    /// below the ground is dry ground: the depth is still <c>water − ground</c>, so ground that rises
    /// through a level simply stops being under water, and no consumer needs a second rule.</para>
    ///
    /// <para><b>Deterministic, never saved.</b> The still level is DERIVED data: a pure function of the plan
    /// position, recomputed from the region's authored data and never written to the save (CLAUDE.md rule
    /// 5). Same position → same level, forever. It does not move with the tide; the tide rises through it
    /// (where the tide stands higher, the max is the tide).</para>
    ///
    /// <para><b>Pull, not push; scene-scoped; never null.</b> Sampled on demand, like
    /// <see cref="ITidalTerrain"/>. The region's terrain registers its still water through
    /// <see cref="GameServices.StillWater"/> and clears it on teardown; absent a registrant the accessor
    /// reads <see cref="EmptyStillWater.Instance"/> — no still water anywhere — under which every
    /// composition is bit-identical to the tide-only read that predates this seam.</para>
    ///
    /// <para><b>Who reads it (ADR 0046 §6).</b> The on-foot composition
    /// (<see cref="StandableSurfaces.OnFootDepth(ITidalTerrain,IEnvironmentService,IStillWater,System.Collections.Generic.IReadOnlyList{IStandableSurface},double,Vector2)"/>),
    /// and, through <see cref="Map"/>, the water shader's drawn edge and the tidal faces. Boats, clams,
    /// traps, vehicles and the presenters stay on the tide; the ADR says why for each.</para>
    ///
    /// <para><b>Ownership.</b> This contract is Core's. The <b>world</b> implements it over its painted still
    /// map; gameplay and the render consume it; neither references the other's module (CLAUDE.md rule
    /// 4).</para>
    /// </summary>
    public interface IStillWater
    {
        /// <summary>
        /// The still-water surface at <paramref name="worldPos"/>, in <b>metres above chart datum</b>, or
        /// <see cref="StillWaterLevels.None"/> where no still water stands. Deterministic — a pure function
        /// of the position. Compose it with the tide through <see cref="StillWaterLevels.Compose"/>; never
        /// read it alone as "the water".
        /// </summary>
        /// <param name="worldPos">World-space XY position to sample (world units).</param>
        float StillLevelAt(Vector2 worldPos);

        /// <summary>
        /// The map this still water is decoded from, for the render: the SAME texture over the same rect
        /// and range, so the ponds the water draws and the ponds the fisher wades are one set of bytes.
        /// Unbound (<see cref="StillWaterMap.IsBound"/> false) when there is no map.
        /// </summary>
        StillWaterMap Map { get; }
    }

    /// <summary>
    /// The still-water MAP a render consumer samples (ADR 0046 §5): a 16-bit texture on the region's
    /// height scale in which <b>code 0 means "no still water"</b>, over a world rectangle, decoding a
    /// filtered code <c>r</c> in (0, 1] as <c>lerp(MinLevel, MaxLevel, r)</c>
    /// (<see cref="StillWaterLevels.DecodeCode01"/>). Off the rectangle there is no still water — unlike
    /// the height map, whose edge clamps, because a pond at the map's edge must not extend to the horizon.
    /// <c>default</c> is the unbound map.
    /// </summary>
    public readonly struct StillWaterMap
    {
        /// <summary>The still-level texture (R channel, 0 = none). Null when unbound.</summary>
        public readonly Texture Texture;
        /// <summary>Bottom-left corner of the covered world rectangle (world units).</summary>
        public readonly Vector2 WorldMin;
        /// <summary>Size of the covered world rectangle (world units).</summary>
        public readonly Vector2 WorldSize;
        /// <summary>The level (m above datum) code 0 would decode to — the height map's own minimum.</summary>
        public readonly float MinLevel;
        /// <summary>The level (m above datum) code 1 decodes to — the height map's own maximum.</summary>
        public readonly float MaxLevel;

        public StillWaterMap(Texture texture, Vector2 worldMin, Vector2 worldSize, float minLevel, float maxLevel)
        {
            Texture = texture;
            WorldMin = worldMin;
            WorldSize = worldSize;
            MinLevel = minLevel;
            MaxLevel = maxLevel;
        }

        /// <summary>True when there is a map to sample. Unity's own null test, so a destroyed texture reads
        /// as unbound rather than handing the shader a dead reference.</summary>
        public bool IsBound => Texture != null;
    }

    /// <summary>
    /// The honest "no still water anywhere" — what <see cref="GameServices.StillWater"/> reads when nothing
    /// is registered (a bare scene, EditMode, a region without a still map). Every level is
    /// <see cref="StillWaterLevels.None"/>, so every composition returns the tide exactly. Stateless and
    /// shared.
    /// </summary>
    public sealed class EmptyStillWater : IStillWater
    {
        /// <summary>The one instance.</summary>
        public static readonly EmptyStillWater Instance = new EmptyStillWater();

        private EmptyStillWater() { }

        /// <inheritdoc/>
        public float StillLevelAt(Vector2 worldPos) => StillWaterLevels.None;

        /// <inheritdoc/>
        public StillWaterMap Map => default;
    }

    /// <summary>
    /// The still-water rules every consumer shares (ADR 0046): the one composition with the tide, and the
    /// map's code. Pure and allocation-free, so the whole rule is EditMode-testable with doubles.
    /// </summary>
    public static class StillWaterLevels
    {
        /// <summary>"No still water here": negative infinity, so it loses every max and any depth read
        /// from it alone is −∞ (as dry as can be).</summary>
        public const float None = float.NegativeInfinity;

        /// <summary>The largest code the map stores (16 bits, the height map's own precision).</summary>
        public const int CodeCount = 65535;

        /// <summary>
        /// The ONE composition: <b>water = max(tide, still)</b>. Written as a comparison rather than
        /// <see cref="Mathf.Max(float,float)"/> so that a <see cref="None"/> (or NaN) still level returns
        /// the tide <b>bit for bit</b>, whatever the tide is — the neutrality every region without a still
        /// map depends on.
        /// </summary>
        public static float Compose(float tideLevel, float stillLevel)
            => stillLevel > tideLevel ? stillLevel : tideLevel;

        /// <summary>
        /// The water level at a plan point: the deterministic tide from <paramref name="environment"/> at
        /// <paramref name="totalSeconds"/>, composed with the still water there. A null
        /// <paramref name="still"/> is the empty sea (the tide alone). The caller owns the null check on
        /// <paramref name="environment"/>, as every tide read does.
        /// </summary>
        public static float WaterLevelAt(IEnvironmentService environment, IStillWater still,
                                         double totalSeconds, Vector2 worldPos)
        {
            float tide = environment.WaterLevelAt(totalSeconds);
            return still != null ? Compose(tide, still.StillLevelAt(worldPos)) : tide;
        }

        /// <summary>
        /// Decode a (filtered) normalized map sample: 0 → <see cref="None"/>; anything above it →
        /// <c>lerp(min, max, r)</c>. The water and tidal-face shaders' <c>StillLevelAt</c> is this line in
        /// HLSL, so the sim and the render apply the same "none" rule to the same filtered value.
        /// </summary>
        public static float DecodeCode01(float r01, float minLevel, float maxLevel)
            => r01 > 0f ? Mathf.Lerp(minLevel, maxLevel, r01) : None;

        /// <summary>
        /// Encode a still level to the map's 16-bit code — the inverse of <see cref="DecodeCode01"/> for
        /// whatever writes a still map. <see cref="None"/>, NaN and any level at or below
        /// <paramref name="minLevel"/> encode to 0 (no still water); a real level encodes to at least 1, so
        /// it can never be read back as none.
        /// </summary>
        public static ushort EncodeCode(float stillLevel, float minLevel, float maxLevel)
        {
            if (!(stillLevel > minLevel)) return 0;
            float span = Mathf.Max(maxLevel - minLevel, 1e-3f);
            int code = Mathf.RoundToInt((stillLevel - minLevel) / span * CodeCount);
            return (ushort)Mathf.Clamp(code, 1, CodeCount);
        }
    }
}
