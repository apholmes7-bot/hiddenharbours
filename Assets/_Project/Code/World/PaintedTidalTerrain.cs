using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.World
{
    /// <summary>
    /// The PAINTED-map concrete <see cref="ITidalTerrain"/> (ADR 0014) — the alternative to the analytic
    /// <see cref="TidalTerrain"/>: instead of composing elevation from authored zones in code, it samples a
    /// hand-painted <see cref="PaintedHeightMap"/>. Drop this on a region (pointing at the region's painted
    /// map) and it registers itself into <see cref="GameServices.TidalTerrain"/> on enable — so the SIM
    /// (the on-foot walkability gate, the clam-baring, the boat-cross, the clam-hole scatter) reads the
    /// PAINTED heights through the SAME Core seam everything already uses (<see cref="TidalExposure"/> /
    /// <see cref="IEnvironmentService.WaterLevelAt"/>). What the owner paints is what bares, floods, and
    /// grounds — <b>painted == sailed/walked</b> (the P1 integrity rule, CLAUDE.md rule 4).
    ///
    /// <para><b>Same data as the render.</b> The water shader (<see cref="HiddenHarbours.Art.WaterSurface"/>'s
    /// painted path) feeds the SAME <see cref="PaintedHeightMap"/> texture to the GPU, and this samples the
    /// CPU-decoded twin of those bytes through <see cref="PaintedHeightField.ElevationAt"/> with the shader's
    /// exact world→uv mapping — so the visible depth and the gameplay depth cannot diverge (ADR 0010
    /// "one height map, three consumers"; the rejected "separate visual/physics height fields" is
    /// structurally impossible here).</para>
    ///
    /// <para><b>Optional &amp; scene-scoped (ADR 0009).</b> Last-writer-wins via <see cref="OnEnable"/>; a
    /// region carries at most one <see cref="ITidalTerrain"/>. With no map assigned (or a non-readable
    /// texture) <see cref="ElevationAt"/> reports the deep floor / the map's min — never throws — and a
    /// null registration leaves "open water". Clears the accessor on disable only if it still points here
    /// (don't stomp a region that registered after us during an additive swap).</para>
    ///
    /// <para><b>Determinism (rule 5).</b> Authored data read at runtime, sampled purely; no RNG, nothing
    /// saved at runtime. The tide is still recomputed from <c>(worldSeed, gameTime)</c>; only the elevation
    /// source changed.</para>
    /// </summary>
    public sealed class PaintedTidalTerrain : MonoBehaviour, ITidalTerrain
    {
        [Header("The hand-painted height map this region uses (ADR 0014)")]
        [Tooltip("The PaintedHeightMap asset the owner painted for this region. The sim samples its decoded " +
                 "elevation; the water shader feeds its texture. If empty, this terrain reports the fallback " +
                 "elevation everywhere (treated as open/deep water).")]
        [SerializeField] private PaintedHeightMap _map;

        [Tooltip("Elevation (m above datum) returned when no painted map is assigned / readable. Well below " +
                 "the lowest tide so the absence reads as deep open water, never accidentally walkable.")]
        [SerializeField] private float _fallbackElevation = -10f;

        /// <summary>The painted map this terrain samples (assigned by the builder / paint-tool adoption).</summary>
        public PaintedHeightMap Map
        {
            get => _map;
            set => _map = value;
        }

        private void OnEnable()
        {
            // Decode the painted field up front (off the hot path) so the first ElevationAt is cheap.
            if (_map != null) _map.Rebuild();
            GameServices.TidalTerrain = this;
        }

        private void OnDisable()
        {
            if (ReferenceEquals(GameServices.TidalTerrain, this))
                GameServices.TidalTerrain = null;
        }

        /// <inheritdoc/>
        public float ElevationAt(Vector2 worldPos)
        {
            var field = _map != null ? _map.Field : null;
            return field != null ? field.ElevationAt(worldPos) : _fallbackElevation;
        }
    }
}
