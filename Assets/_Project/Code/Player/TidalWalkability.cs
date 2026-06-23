using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Player
{
    /// <summary>
    /// The on-foot <b>falling-tide walkability</b> rule (St Peters opening; P1 The Sea Has Moods): the
    /// fisher may only stand on ground that is <em>exposed</em> at the current tide. A position is
    /// walkable when its authored terrain elevation is at or above the deterministic water surface —
    /// exactly <see cref="TidalExposure.IsExposed(float,float)"/>. As the tide falls more of the seabed
    /// bares and the sandbar path to Greywick emerges; as it rises that path re-submerges. The boat-cross
    /// gate (<c>BoatCrossing</c>, Boats lane) is the inverse: a channel deep enough to float a hull is the
    /// channel a walker can't cross — the two read the SAME single number (water level − ground elevation).
    ///
    /// <para><b>Pure &amp; deterministic.</b> Walkability is a pure function of the deterministic water
    /// level (<see cref="IEnvironmentService.WaterLevelAt"/>, recomputed from <c>(worldSeed, gameTime)</c>)
    /// and the authored ground height (<see cref="ITidalTerrain.ElevationAt"/>) — no RNG, nothing saved
    /// (CLAUDE.md rule 5). The static helpers are fully EditMode-testable with fake terrain/environment
    /// doubles; <see cref="PlayerWalkController"/> consults them each physics tick.</para>
    ///
    /// <para><b>Seam discipline (CLAUDE.md rule 4).</b> Reads the world's terrain and the environment only
    /// through the Core <see cref="GameServices.TidalTerrain"/> / <see cref="GameServices.Environment"/>
    /// accessors — never the World or Environment concrete classes. Both are optional and scene-scoped: a
    /// <b>null terrain means "open water"</b> (no authored height map) and a <b>null environment means "no
    /// tide service"</b>. In either case there is no falling-tide shoreline to enforce, so the gate is
    /// <b>disabled</b> (everywhere walkable) rather than locking the player in place — the safe default for
    /// a region that simply isn't tide-gated (e.g. a normal land scene, or EditMode before wiring).</para>
    /// </summary>
    public static class TidalWalkability
    {
        /// <summary>
        /// True when the on-foot player may stand at <paramref name="worldPos"/> right now. Resolves the
        /// authored ground elevation from <paramref name="terrain"/> and the deterministic water surface
        /// from <paramref name="environment"/> at <paramref name="totalSeconds"/>, then asks
        /// <see cref="TidalExposure.IsExposed(float,float)"/>. When either service is absent the region
        /// isn't tide-gated, so this returns <c>true</c> (the gate is off — never trap the walker).
        /// </summary>
        public static bool IsWalkable(ITidalTerrain terrain, IEnvironmentService environment,
                                      double totalSeconds, Vector2 worldPos)
        {
            // No height map or no tide service → this region has no falling-tide shoreline to enforce.
            if (terrain == null || environment == null) return true;

            float waterLevel = environment.WaterLevelAt(totalSeconds);
            float ground = terrain.ElevationAt(worldPos);
            return TidalExposure.IsExposed(waterLevel, ground);
        }

        /// <summary>
        /// Convenience over the live Core services (<see cref="GameServices.TidalTerrain"/> /
        /// <see cref="GameServices.Environment"/> at the current <see cref="IGameClock.TotalSeconds"/>).
        /// Used by <see cref="PlayerWalkController"/>; tests drive the explicit overload with doubles.
        /// </summary>
        public static bool IsWalkableNow(Vector2 worldPos)
        {
            IGameClock clock = GameServices.Clock;
            double now = clock != null ? clock.TotalSeconds : 0.0;
            return IsWalkable(GameServices.TidalTerrain, GameServices.Environment, now, worldPos);
        }
    }
}
