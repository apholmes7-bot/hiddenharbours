using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Boats
{
    /// <summary>
    /// The <b>boat-cross gate</b> (St Peters opening; P1/P5): a hull may only float where the water is
    /// deeper than its draught — <c>depth = waterLevel − groundElevation ≥ draught</c>. It is the exact
    /// inverse of the on-foot walkability gate (<c>TidalWalkability</c>, Player lane): both read the SAME
    /// single number (water level minus the authored ground elevation, i.e.
    /// <see cref="TidalExposure.WaterDepth"/>). So the sandbar channel that's crossable by boat at higher
    /// water is the very path that bares for a walker as the tide falls, and as the tide keeps falling the
    /// channel closes to the boat too — the breathing shoreline that makes the opening read (the dory
    /// crosses the channel at a higher tide; the walker crosses the bar at low tide).
    ///
    /// <para><b>Non-punishing (cozy, P5).</b> This is a passability gate, NOT the grounding/damage model:
    /// too-shallow water simply can't be entered (the hull eases to a stop at the shallows), there's no
    /// hull damage, no stranding, no list — the opening world stays forgiving. The richer grounding /
    /// take-on-water danger model is a later wave (boats-and-navigation.md §3); this is only "can I pass
    /// here right now?".</para>
    ///
    /// <para><b>Pure &amp; deterministic.</b> Depth is a pure function of the deterministic water level
    /// (<see cref="IEnvironmentService.WaterLevelAt"/>) and authored ground height
    /// (<see cref="ITidalTerrain.ElevationAt"/>) — no RNG, nothing saved (CLAUDE.md rule 5). The static
    /// helpers are EditMode-testable with fakes; <see cref="BoatController"/> consults them each physics
    /// tick. Reads the world/environment ONLY through the Core <see cref="GameServices"/> accessors
    /// (CLAUDE.md rule 4): a <b>null terrain means "open water"</b> — no height map, so no shallows to
    /// gate, and the boat passes freely (the safe default for a normal open-water region).</para>
    /// </summary>
    public static class BoatCrossing
    {
        /// <summary>
        /// Water depth (m) over <paramref name="worldPos"/> at <paramref name="totalSeconds"/>, resolved
        /// from the authored terrain and the deterministic water level. Returns
        /// <see cref="float.PositiveInfinity"/> when no terrain/environment is wired ("open water" — no
        /// bottom to be shallow over), so a missing height map never falsely blocks a boat.
        /// </summary>
        public static float DepthAt(ITidalTerrain terrain, IEnvironmentService environment,
                                    double totalSeconds, Vector2 worldPos)
        {
            if (terrain == null || environment == null) return float.PositiveInfinity;
            float waterLevel = environment.WaterLevelAt(totalSeconds);
            float ground = terrain.ElevationAt(worldPos);
            return TidalExposure.WaterDepth(waterLevel, ground);
        }

        /// <summary>
        /// True when a hull of <paramref name="draughtMeters"/> can float at <paramref name="worldPos"/>
        /// right now (depth ≥ draught). Open water (null terrain) is always passable. The inverse, in
        /// effect, of <c>TidalWalkability.IsWalkable</c> over the same water level + ground elevation.
        /// </summary>
        public static bool CanFloat(ITidalTerrain terrain, IEnvironmentService environment,
                                    double totalSeconds, Vector2 worldPos, float draughtMeters)
            => DepthAt(terrain, environment, totalSeconds, worldPos) >= draughtMeters;

        /// <summary>
        /// Convenience over the live Core services at the current clock time — used by
        /// <see cref="BoatController"/>; tests drive the explicit overloads with doubles.
        /// </summary>
        public static bool CanFloatNow(Vector2 worldPos, float draughtMeters)
        {
            IGameClock clock = GameServices.Clock;
            double now = clock != null ? clock.TotalSeconds : 0.0;
            return CanFloat(GameServices.TidalTerrain, GameServices.Environment, now, worldPos, draughtMeters);
        }
    }
}
