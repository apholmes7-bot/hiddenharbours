using System.Collections.Generic;
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
    /// sandbar path to Nine Mile Creek emerges; as it rises that path re-submerges. The boat-cross gate
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
    ///
    /// <para><b>Standable structures come FIRST (the pier fix).</b> Before the elevation-vs-water answer,
    /// every read here resolves the on-foot <em>standing</em> elevation through
    /// <see cref="StandableSurfaces"/>: on a registered deck your feet are on the DECK, not on the seabed
    /// under it. Without this the St Peters wharf — 246 planks over a dredged −1.0 m slip in a tide-gated
    /// region — read as 4.5 m of open water at the ratified disembark point, so sailing home meant arriving
    /// at your own island and swimming across your own pier. It is a substitution of one number, not a
    /// second rule: the depth is still <c>waterLevel − standingElevation</c>, so a deck clear of the
    /// highest water is dry at every tide, and away from any registered surface the answer is bit-identical
    /// to what it was before the seam existed.</para>
    /// </summary>
    public static class TidalWalkability
    {
        /// <summary>
        /// True when the on-foot player may stand at <paramref name="worldPos"/> right now. Resolves the
        /// on-foot standing elevation (a registered <see cref="IStandableSurface"/>'s deck, else the
        /// authored ground from <paramref name="terrain"/>) and the deterministic water surface from
        /// <paramref name="environment"/> at <paramref name="totalSeconds"/>, then asks
        /// <see cref="TidalExposure.IsExposed(float,float)"/>. When either service is absent the region
        /// isn't tide-gated, so this returns <c>true</c> (the gate is off — never trap the walker).
        /// </summary>
        public static bool IsWalkable(ITidalTerrain terrain, IEnvironmentService environment,
                                      IReadOnlyList<IStandableSurface> surfaces,
                                      double totalSeconds, Vector2 worldPos)
        {
            // No height map or no tide service → this region has no falling-tide shoreline to enforce.
            if (terrain == null || environment == null) return true;

            float waterLevel = environment.WaterLevelAt(totalSeconds);
            float standing = StandableSurfaces.StandingElevation(terrain.ElevationAt(worldPos), surfaces, worldPos);
            return TidalExposure.IsExposed(waterLevel, standing);
        }

        /// <summary>The no-structures overload — the pre-seam behaviour, kept so every existing call site
        /// and test means exactly what it meant (a region with no standable surfaces).</summary>
        public static bool IsWalkable(ITidalTerrain terrain, IEnvironmentService environment,
                                      double totalSeconds, Vector2 worldPos)
            => IsWalkable(terrain, environment, null, totalSeconds, worldPos);

        /// <summary>
        /// The water <b>depth</b> (m) over a position — the number the wade model scales feel and gates on
        /// (≤ 0 dry; &gt; 0 is metres of water over the standing surface). Delegates to the ONE on-foot
        /// composition, <see cref="StandableSurfaces.OnFootDepth"/>: the deterministic water level
        /// (<see cref="IEnvironmentService.WaterLevelAt"/>) over the standing elevation (a registered deck,
        /// else the authored ground from <see cref="ITidalTerrain.ElevationAt"/>). When either service is
        /// absent the region isn't tide-gated, so it returns <see cref="float.NegativeInfinity"/>
        /// ("as dry as can be" — everywhere fully walkable, gate off, never trap the walker — the depth
        /// analogue of <see cref="IsWalkable"/> returning <c>true</c>).
        /// </summary>
        public static float DepthAt(ITidalTerrain terrain, IEnvironmentService environment,
                                    IReadOnlyList<IStandableSurface> surfaces,
                                    double totalSeconds, Vector2 worldPos)
            => StandableSurfaces.OnFootDepth(terrain, environment, surfaces, totalSeconds, worldPos);

        /// <summary>The no-structures overload of <see cref="DepthAt"/> — the pre-seam read, unchanged.</summary>
        public static float DepthAt(ITidalTerrain terrain, IEnvironmentService environment,
                                    double totalSeconds, Vector2 worldPos)
            => StandableSurfaces.OnFootDepth(terrain, environment, null, totalSeconds, worldPos);

        /// <summary>
        /// The on-foot <see cref="DepthBand"/> at a position (Dry/Wade/Swim/Deep) from the two owner
        /// thresholds — the single read the controller uses to (a) soft-wall the Deep band, (b) scale move
        /// speed, and (c) drive the on-foot water-state signal. Gate-off regions (no terrain/tide) read
        /// <see cref="DepthBand.Dry"/> (everywhere walkable at full speed).
        /// </summary>
        public static DepthBand BandAt(ITidalTerrain terrain, IEnvironmentService environment,
                                       IReadOnlyList<IStandableSurface> surfaces,
                                       double totalSeconds, Vector2 worldPos, float wadeDepth, float swimLimit)
            => TidalExposure.BandForDepth(DepthAt(terrain, environment, surfaces, totalSeconds, worldPos),
                                          wadeDepth, swimLimit);

        /// <summary>The no-structures overload of <see cref="BandAt"/> — the pre-seam read, unchanged.</summary>
        public static DepthBand BandAt(ITidalTerrain terrain, IEnvironmentService environment,
                                       double totalSeconds, Vector2 worldPos, float wadeDepth, float swimLimit)
            => BandAt(terrain, environment, null, totalSeconds, worldPos, wadeDepth, swimLimit);

        /// <summary>
        /// Convenience over the live Core services (<see cref="GameServices.TidalTerrain"/> /
        /// <see cref="GameServices.Environment"/> at the current <see cref="IGameClock.TotalSeconds"/>) and
        /// the live <see cref="StandableSurfaces"/> registry. Used by <see cref="PlayerWalkController"/>;
        /// tests drive the explicit overloads with doubles.
        /// </summary>
        public static bool IsWalkableNow(Vector2 worldPos)
        {
            IGameClock clock = GameServices.Clock;
            double now = clock != null ? clock.TotalSeconds : 0.0;
            return IsWalkable(GameServices.TidalTerrain, GameServices.Environment,
                              StandableSurfaces.Active, now, worldPos);
        }

        /// <summary>Live water depth (m) over a position, over the current Core services + clock + surface
        /// registry. Used by <see cref="PlayerWalkController"/> to scale wade feel and gate the boat-only
        /// soft wall; tests drive the explicit
        /// <see cref="DepthAt(ITidalTerrain,IEnvironmentService,IReadOnlyList{IStandableSurface},double,Vector2)"/>
        /// overload.</summary>
        public static float DepthNow(Vector2 worldPos) => StandableSurfaces.OnFootDepthNow(worldPos);
    }
}
