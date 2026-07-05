using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// The on-foot <b>depth band</b> a position falls in for the tide-gated walk model (P1/P5) — the
    /// owner's three-band on-foot water-travel rule, layered over the <em>same</em> seabed height the
    /// water render and boats read (render==sim), so they can never disagree. Ordered by depth so a
    /// simple <c>&gt;</c>/<c>&lt;</c> compares "how deep am I in": <see cref="Dry"/> &lt; <see cref="Wade"/>
    /// &lt; <see cref="Swim"/> &lt; <see cref="Deep"/>.
    /// </summary>
    public enum DepthBand
    {
        /// <summary>Exposed ground (depth ≤ 0). Full walking speed. Exactly <see cref="TidalExposure.IsExposed"/>.</summary>
        Dry = 0,
        /// <summary>Shallow water (0 &lt; depth ≤ WadeDepth). Walkable on foot but slowed, more as it deepens.</summary>
        Wade = 1,
        /// <summary>The escape valve (WadeDepth &lt; depth ≤ SwimLimit). Very slow + vulnerable — used to swim
        /// OUT toward shallower ground so a rising tide never traps you, never to cross.</summary>
        Swim = 2,
        /// <summary>Boat-only water (depth &gt; SwimLimit). On-foot travel is blocked — a soft wall stops the
        /// player stepping in. Water travel here is boats only (the owner's hard rule).</summary>
        Deep = 3,
    }

    /// <summary>
    /// The <b>one shared rule</b> for "is this spot submerged or exposed at the current tide?" — the
    /// additive Core seam the world (terrain authoring) and gameplay (on-foot walkability sim) both
    /// read so they can never disagree about the falling-tide shoreline (P1). It backs the St Peters
    /// opening's tide-gated sandbar and the Drownded Lands' walkable seabed
    /// (design/world-and-regions.md §7, design/time-tides-weather.md §3.5).
    ///
    /// <para><b>Determinism (the invariant this seam protects).</b> Exposure is a pure function of the
    /// deterministic <em>water level</em> (the tide surface in metres above chart datum, recomputed from
    /// <c>(worldSeed, gameTime)</c> via <see cref="IEnvironmentService.WaterLevelAt"/>) and the
    /// <em>authored terrain elevation</em> at the position. There is <b>no RNG</b> here and <b>nothing is
    /// saved</b> — the tide is never serialized; it is reconstructed from seed + time (CLAUDE.md rule 5).
    /// Same inputs → same answer, forever.</para>
    ///
    /// <para><b>Sign convention.</b> Both quantities are <em>metres above chart datum</em>, the same
    /// frame the tide model uses (datum = lowest astronomical tide ≈ 0). A position is <b>exposed</b>
    /// when its ground sits at or above the water surface; it is <b>submerged</b> when the water is
    /// above the ground. Water <em>depth</em> over a submerged position is <c>waterLevel −
    /// terrainElevation</c> (≤ 0 means dry/exposed) — the same single number boat grounding will compare
    /// against draught in the next wave (design/time-tides-weather.md §3.5, §5.1).</para>
    ///
    /// <para><b>Ownership.</b> This contract + the minimal maths are Core's. The <em>world</em> authors
    /// the per-tile/per-feature terrain-elevation field (its tilemap/heightfield); the <em>gameplay</em>
    /// walkability sim consumes <see cref="IsExposed(IEnvironmentService, double, float)"/> — both built
    /// in the <b>next</b> wave. This wave defines only the seam.</para>
    /// </summary>
    public static class TidalExposure
    {
        /// <summary>
        /// Water depth (metres) over a position whose ground is at <paramref name="terrainElevation"/>
        /// when the tide surface is at <paramref name="waterLevel"/> (both metres above chart datum).
        /// <b>≤ 0 means the position is dry/exposed</b>; &gt; 0 is the depth of water over it.
        /// </summary>
        public static float WaterDepth(float waterLevel, float terrainElevation)
            => waterLevel - terrainElevation;

        /// <summary>
        /// True when a position is <b>exposed</b> (out of the water) at the given tide surface: its
        /// ground is at or above the water level. The pure form — callers that already hold the water
        /// level (e.g. having sampled it once for a whole tile batch) use this; the convenience overload
        /// below pulls the level from the environment service.
        /// </summary>
        /// <param name="waterLevel">Tide surface, metres above chart datum.</param>
        /// <param name="terrainElevation">Authored ground height at the position, metres above chart datum.</param>
        public static bool IsExposed(float waterLevel, float terrainElevation)
            => terrainElevation >= waterLevel;

        /// <summary>True when a position is <b>submerged</b> at the given tide surface (the negation of
        /// <see cref="IsExposed(float,float)"/>).</summary>
        public static bool IsSubmerged(float waterLevel, float terrainElevation)
            => terrainElevation < waterLevel;

        // ---- The on-foot depth bands (the owner's three-band water-travel model, P1/P5) -----------

        /// <summary>
        /// True when the on-foot player may <b>stand and walk</b> at a position: the water over it is no
        /// deeper than <paramref name="wadeDepth"/> — i.e. it is dry OR shallow enough to wade. This is a
        /// strict <b>superset of <see cref="IsExposed(float,float)"/></b>: at
        /// <paramref name="wadeDepth"/> = 0 it is exactly the old exposure rule (depth ≤ 0), so every
        /// existing walkability behaviour is preserved; with a positive wade depth it additionally admits
        /// the shallow wade band. The wade band is <em>walkable but slowed</em> — the speed penalty is
        /// <see cref="MoveScaleForBand"/> / <see cref="MoveScaleForDepth"/>, not this gate.
        /// </summary>
        /// <param name="waterLevel">Tide surface, metres above chart datum.</param>
        /// <param name="terrainElevation">Authored ground height at the position, metres above chart datum.</param>
        /// <param name="wadeDepth">Deepest water still walkable on foot (m). 0 collapses this to
        /// <see cref="IsExposed(float,float)"/>.</param>
        public static bool IsWalkable(float waterLevel, float terrainElevation, float wadeDepth)
            => WaterDepth(waterLevel, terrainElevation) <= wadeDepth;

        /// <summary>
        /// Classify a raw water <paramref name="depth"/> (m; ≤ 0 is dry) into its on-foot
        /// <see cref="DepthBand"/> using the two owner thresholds. Boundaries are inclusive on the
        /// shallower side so the bands tile without gaps or overlap: depth ≤ 0 → <see cref="DepthBand.Dry"/>;
        /// (0, <paramref name="wadeDepth"/>] → <see cref="DepthBand.Wade"/>; (wadeDepth,
        /// <paramref name="swimLimit"/>] → <see cref="DepthBand.Swim"/>; &gt; swimLimit →
        /// <see cref="DepthBand.Deep"/>. Pure, deterministic, no RNG.
        /// </summary>
        public static DepthBand BandForDepth(float depth, float wadeDepth, float swimLimit)
        {
            if (depth <= 0f) return DepthBand.Dry;
            if (depth <= wadeDepth) return DepthBand.Wade;
            if (depth <= swimLimit) return DepthBand.Swim;
            return DepthBand.Deep;
        }

        /// <summary>Convenience: the <see cref="DepthBand"/> at a position from the water level + ground
        /// elevation (composes <see cref="WaterDepth"/> with <see cref="BandForDepth"/>).</summary>
        public static DepthBand BandAt(float waterLevel, float terrainElevation, float wadeDepth, float swimLimit)
            => BandForDepth(WaterDepth(waterLevel, terrainElevation), wadeDepth, swimLimit);

        /// <summary>
        /// The on-foot <b>move-speed multiplier</b> (0..1) for a raw water <paramref name="depth"/> — the
        /// "more slow as it deepens" feel curve. <b>Full (1)</b> on dry ground; ramps <em>linearly</em>
        /// from 1 down to <paramref name="wadeSlowFactor"/> across the wade band (0→<paramref name="wadeDepth"/>);
        /// then ramps from <paramref name="wadeSlowFactor"/> down to <paramref name="swimSlowFactor"/> across
        /// the swim band (wadeDepth→<paramref name="swimLimit"/>); and holds at
        /// <paramref name="swimSlowFactor"/> beyond <paramref name="swimLimit"/> (the deep band is soft-walled
        /// by the controller, but the escape-from-deep forgiveness still moves at the swim factor). Pure,
        /// continuous, deterministic — no engine, no RNG.
        /// </summary>
        public static float MoveScaleForDepth(float depth, float wadeDepth, float swimLimit,
                                              float wadeSlowFactor, float swimSlowFactor)
        {
            if (depth <= 0f) return 1f;                              // dry: full speed
            if (depth <= wadeDepth)                                  // wade: 1 → wadeSlowFactor
            {
                float t = wadeDepth > 0f ? depth / wadeDepth : 1f;
                return Mathf.Lerp(1f, wadeSlowFactor, t);
            }
            if (depth <= swimLimit)                                  // swim: wadeSlowFactor → swimSlowFactor
            {
                float span = swimLimit - wadeDepth;
                float t = span > 0f ? (depth - wadeDepth) / span : 1f;
                return Mathf.Lerp(wadeSlowFactor, swimSlowFactor, t);
            }
            return swimSlowFactor;                                   // deep: crawl (escape-only)
        }

        /// <summary>Discrete move-speed multiplier for a <see cref="DepthBand"/> — the band's <em>entry</em>
        /// (shallow-edge) speed: Dry=1, Wade=1 (its slow edge is <paramref name="wadeSlowFactor"/> via the
        /// continuous <see cref="MoveScaleForDepth"/>), Swim=<paramref name="wadeSlowFactor"/>,
        /// Deep=<paramref name="swimSlowFactor"/>. The controller uses the continuous curve for feel; this is
        /// a helper for band-only callers/tests.</summary>
        public static float MoveScaleForBand(DepthBand band, float wadeSlowFactor, float swimSlowFactor)
            => band switch
            {
                DepthBand.Dry => 1f,
                DepthBand.Wade => 1f,
                DepthBand.Swim => wadeSlowFactor,
                _ => swimSlowFactor,
            };

        /// <summary>
        /// Convenience: is the position exposed <em>right now</em>? Reads the deterministic water level
        /// for the active region at <paramref name="totalSeconds"/> from <paramref name="environment"/>
        /// and compares against the authored <paramref name="terrainElevation"/>. Returns
        /// <c>false</c> (treat as submerged — the safe default for a walkability gate) when no
        /// environment service is wired, so a null service never throws in the hot path.
        /// </summary>
        public static bool IsExposed(IEnvironmentService environment, double totalSeconds, float terrainElevation)
        {
            if (environment == null) return false;
            return IsExposed(environment.WaterLevelAt(totalSeconds), terrainElevation);
        }
    }
}
