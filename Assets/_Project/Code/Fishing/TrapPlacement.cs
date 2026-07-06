using HiddenHarbours.Core;

namespace HiddenHarbours.Fishing
{
    /// <summary>
    /// The PURE, engine-light <b>depth gate</b> for dropping a baited trap (trap-fishing arc Build 4) — the
    /// <em>inverse</em> of the clam dig's exposure gate. A trap must sit in <b>deep enough water</b> (its
    /// Def's <see cref="TrapDef.MinSoakDepthMeters"/>), not on dry/bared ground: you can't set a pot on land.
    /// Split out (like <see cref="TrapSoak"/>) so the rule is EditMode-testable headless — a pure function of
    /// the deterministic water depth, no RNG, nothing saved (rule 5).
    ///
    /// <para><b>Same one number the whole shoreline reads.</b> Depth here is the exact
    /// <c>waterLevel − terrainElevation</c> the walkability sim, the boat-cross gate and the water shader
    /// read (<see cref="TidalExposure.WaterDepth"/> over the authored <see cref="ITidalTerrain"/>) — so a
    /// trap is placeable exactly where the sea is deep enough to float it, and the picture (the covered
    /// water) can never disagree with the gate. Where ClamDig requires <c>depth ≤ 0</c> (bared ground), the
    /// trap requires <c>depth ≥ MinSoakDepthMeters</c> (deep water). This is P1 read as gameplay: you learn
    /// to set your pots in the gut, not on the flat that'll bare at low water.</para>
    /// </summary>
    public static class TrapPlacement
    {
        /// <summary>
        /// Is the water at least <paramref name="minSoakDepthMeters"/> deep here? The core gate: a trap needs
        /// deep enough water to fish (not dry, not shoal). Pure — <paramref name="depthMeters"/> is
        /// <c>waterLevel − terrainElevation</c> (≤ 0 = dry/exposed). A non-positive
        /// <paramref name="minSoakDepthMeters"/> means "any submerged water will do" (depth &gt; 0).
        /// </summary>
        public static bool IsDeepEnough(float depthMeters, float minSoakDepthMeters)
            => minSoakDepthMeters > 0f ? depthMeters >= minSoakDepthMeters : depthMeters > 0f;

        /// <summary>
        /// Can a trap of <paramref name="trapDef"/> be dropped at <paramref name="worldPos"/> right now? Reads
        /// the deterministic water level for the active region at <paramref name="totalSeconds"/> from
        /// <paramref name="environment"/> and the authored ground elevation from <paramref name="terrain"/>,
        /// computes the water depth, and applies <see cref="IsDeepEnough"/> against the Def's
        /// <see cref="TrapDef.MinSoakDepthMeters"/>. Returns <c>false</c> (can't place — the safe default)
        /// when the Def, the environment, or the terrain is missing: a null terrain means "open water" with
        /// no authored height map, so we don't guess a depth and let a placement stand on nothing. Deterministic.
        /// </summary>
        public static bool CanPlaceAt(TrapDef trapDef, IEnvironmentService environment, ITidalTerrain terrain,
                                      double totalSeconds, UnityEngine.Vector2 worldPos)
        {
            if (trapDef == null || environment == null || terrain == null) return false;
            float depth = TidalExposure.WaterDepth(environment.WaterLevelAt(totalSeconds), terrain.ElevationAt(worldPos));
            return IsDeepEnough(depth, trapDef.MinSoakDepthMeters);
        }

        /// <summary>The water depth (m) at <paramref name="worldPos"/> right now — <c>waterLevel −
        /// terrainElevation</c>, the diagnostic behind <see cref="CanPlaceAt"/> (for the dev readout / the
        /// "too shallow to set here" prompt). Returns a large negative sentinel (treat as "no water / can't
        /// place") when the environment or terrain is missing, so a caller never reads a bogus 0-depth.</summary>
        public static float DepthAt(IEnvironmentService environment, ITidalTerrain terrain,
                                    double totalSeconds, UnityEngine.Vector2 worldPos)
        {
            if (environment == null || terrain == null) return -9999f;
            return TidalExposure.WaterDepth(environment.WaterLevelAt(totalSeconds), terrain.ElevationAt(worldPos));
        }
    }
}
