using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// The <b>terrain-elevation source</b> for the active region — the per-position "height map" the
    /// tidal-exposure seam was missing. ADR 0009 added the deterministic <em>water level</em>
    /// (<see cref="IEnvironmentService.WaterLevelAt"/>) and the one exposure <em>rule</em>
    /// (<see cref="TidalExposure"/>), but left the <em>terrain elevation</em> as a value the caller had
    /// to supply by hand — its "Within-region elevation source" open question. This contract closes
    /// that gap: it is the single Core seam through which the <b>world</b> publishes authored ground
    /// height and <b>gameplay</b> (the on-foot walkability sim) and the future <b>water depth-gradient
    /// shader</b> read it — neither referencing the other's module (CLAUDE.md rule 4).
    ///
    /// <para><b>Frame &amp; sign.</b> <see cref="ElevationAt"/> returns ground height in <b>metres above
    /// chart datum</b> — the <em>same</em> frame the tide model and <see cref="TidalExposure"/> use.
    /// <b>Higher = drier</b> (more likely exposed). With the deterministic water level for the active
    /// region this composes directly into the two reads the St Peters falling tide needs, evaluated at
    /// a world position <c>pos</c>:</para>
    /// <list type="bullet">
    /// <item><description><b>Walkable now?</b> —
    /// <c>TidalExposure.IsExposed(waterLevel, ElevationAt(pos))</c> (ground at/above the surface is
    /// exposed → on foot you can stand there).</description></item>
    /// <item><description><b>Boat-cross depth.</b> —
    /// <c>depth = waterLevel - ElevationAt(pos)</c> (i.e. <see cref="TidalExposure.WaterDepth"/>); ≤ 0
    /// is dry, and a boat grounds when its draught exceeds this depth — the same single number on-foot
    /// walkability and boat draught both compare against (design/time-tides-weather.md §3.5/§5.1).
    /// </description></item>
    /// </list>
    ///
    /// <para><b>Deterministic, never saved.</b> The elevation field is <b>authored geometry</b>: a pure
    /// function of world position, with <b>no RNG</b> and <b>nothing serialized</b> (authored geometry
    /// is recomputed/reloaded, not persisted — CLAUDE.md rule 5, save §6). Same <paramref name="worldPos"/>
    /// → same elevation, forever; combined with the deterministic water level the whole exposure query
    /// is reproducible.</para>
    ///
    /// <para><b>Pull, not push; optional &amp; scene-scoped.</b> Like <see cref="IActiveBoatService"/>
    /// (ADR 0007) this is sampled on demand, not pushed per tile per tick. The active region's terrain
    /// registers itself via <see cref="GameServices.TidalTerrain"/> when its scene loads and clears on
    /// teardown; before any region is wired (EditMode, the persistent boot before the first scene) the
    /// accessor is <c>null</c>. <b>A null terrain means "open water"</b> — callers treat the absence of
    /// a height map as everywhere-submerged / no walkable ground rather than throwing.</para>
    ///
    /// <para><b>Ownership.</b> This contract is Core's. The <b>world</b> implements it over its
    /// tilemap/heightfield (its call on tile-heightfield vs per-feature zones — ADR 0009 open question);
    /// <b>gameplay</b> and the <b>water shader</b> consume it. Both are downstream lanes — this seam
    /// defines only the contract.</para>
    /// </summary>
    public interface ITidalTerrain
    {
        /// <summary>
        /// Authored ground/seabed elevation at <paramref name="worldPos"/>, in <b>metres above chart
        /// datum</b> (higher = drier / more likely exposed). Deterministic — a pure function of the
        /// position, no RNG. Feed the result to <see cref="TidalExposure"/> together with the active
        /// region's <see cref="IEnvironmentService.WaterLevelAt"/> to answer "exposed (walkable) here,
        /// now?" or to compute boat-cross water depth.
        /// </summary>
        /// <param name="worldPos">World-space XY position to sample (world units).</param>
        float ElevationAt(Vector2 worldPos);
    }
}
