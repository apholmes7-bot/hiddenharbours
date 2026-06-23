namespace HiddenHarbours.Core
{
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
