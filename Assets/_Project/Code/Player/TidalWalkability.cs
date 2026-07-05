using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Player
{
    /// <summary>
    /// The on-foot <b>falling-tide walkability</b> rule (St Peters opening; P1 The Sea Has Moods): the
    /// fisher walks freely on exposed ground and can WADE shallow water, but deeper water slows them and
    /// the deepest water is boat-only. The pure boolean <see cref="IsWalkable"/> still answers the classic
    /// exposure question (ground at/above the surface — exactly <see cref="TidalExposure.IsExposed(float,float)"/>);
    /// the wade model is layered on via <see cref="DepthAt"/> / <see cref="BandAt"/>, which return the water
    /// depth and its <see cref="DepthBand"/> (Dry/Wade/Swim/Deep) so <see cref="PlayerWalkController"/> can
    /// scale feel and soft-wall the boat-only band. As the tide falls more of the seabed bares and the
    /// sandbar path to Greywick emerges; as it rises that path re-submerges. The boat-cross gate
    /// (<c>BoatCrossing</c>, Boats lane) reads the SAME single number (water level − ground elevation) —
    /// render==sim, they can never disagree.
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
        /// The water <b>depth</b> (m) over a position — the number the wade model scales feel and gates on
        /// (≤ 0 dry; &gt; 0 is metres of water over the ground). Composes the deterministic water level
        /// (<see cref="IEnvironmentService.WaterLevelAt"/>) with the authored ground
        /// (<see cref="ITidalTerrain.ElevationAt"/>) via <see cref="TidalExposure.WaterDepth"/>. When either
        /// service is absent the region isn't tide-gated, so it returns <see cref="float.NegativeInfinity"/>
        /// ("as dry as can be" — everywhere fully walkable, gate off, never trap the walker — the depth
        /// analogue of <see cref="IsWalkable"/> returning <c>true</c>).
        /// </summary>
        public static float DepthAt(ITidalTerrain terrain, IEnvironmentService environment,
                                    double totalSeconds, Vector2 worldPos)
        {
            if (terrain == null || environment == null) return float.NegativeInfinity;
            float waterLevel = environment.WaterLevelAt(totalSeconds);
            float ground = terrain.ElevationAt(worldPos);
            return TidalExposure.WaterDepth(waterLevel, ground);
        }

        /// <summary>
        /// The on-foot <see cref="DepthBand"/> at a position (Dry/Wade/Swim/Deep) from the two owner
        /// thresholds — the single read the controller uses to (a) soft-wall the Deep band, (b) scale move
        /// speed, and (c) drive the on-foot water-state signal. Gate-off regions (no terrain/tide) read
        /// <see cref="DepthBand.Dry"/> (everywhere walkable at full speed).
        /// </summary>
        public static DepthBand BandAt(ITidalTerrain terrain, IEnvironmentService environment,
                                       double totalSeconds, Vector2 worldPos, float wadeDepth, float swimLimit)
            => TidalExposure.BandForDepth(DepthAt(terrain, environment, totalSeconds, worldPos), wadeDepth, swimLimit);

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

        /// <summary>Live water depth (m) over a position, over the current Core services + clock. Used by
        /// <see cref="PlayerWalkController"/> to scale wade feel and gate the boat-only soft wall; tests drive
        /// the explicit <see cref="DepthAt(ITidalTerrain,IEnvironmentService,double,Vector2)"/> overload.</summary>
        public static float DepthNow(Vector2 worldPos)
        {
            IGameClock clock = GameServices.Clock;
            double now = clock != null ? clock.TotalSeconds : 0.0;
            return DepthAt(GameServices.TidalTerrain, GameServices.Environment, now, worldPos);
        }
    }
}
