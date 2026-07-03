using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.World
{
    /// <summary>
    /// A rectangular-plateau <see cref="ITidalTerrain"/> — the analytic seabed for regions whose land is
    /// quays, docks and coast STRIPS rather than St Peters' island/bar/channel shapes (ADR 0012
    /// recommendation 4: "the default for any tide-gated coast is the shader" — this is the height source
    /// that converges Coddle Cove and Port Greywick onto the St Peters tide-driven water model).
    ///
    /// <para><b>Authored ELEVATION ZONES (deterministic, never saved).</b> Elevation is a pure function of
    /// world position composed from a deep floor plus a list of axis-aligned rectangular LAND ZONES: flat
    /// at the zone's elevation INSIDE the rectangle, easing (smoothstep) down to the deep floor across the
    /// zone's falloff distance OUTSIDE it — a beach/quay-edge profile. Zones max-compose (the highest
    /// feature wins), mirroring <see cref="TidalTerrain.ElevationAtZones"/>'s composition. There is
    /// <b>no RNG</b> and <b>nothing serialized at runtime</b> (CLAUDE.md rule 5): the same one height rule
    /// feeds the water render (via <c>WaterSurface</c>'s bake), the on-foot walkability
    /// (<c>TidalWalkability</c>) and the boat-cross/grounding (<c>BoatCrossing</c>) — what you SEE is what
    /// you can SAIL/WALK (P1, ADR 0009/0010/0012).</para>
    ///
    /// <para><b>Falloff = the visible tide.</b> The falloff band is where the waterline lives: a GENTLE
    /// falloff (the cove's south beach) makes the shoreline visibly advance/retreat metres over the tide;
    /// a STEEP one (Greywick's dredged quay edge, canon "deep sheltered harbour") keeps the sweep modest.
    /// All values are serialized tunables (rule 6) the owner can dial in the Inspector; hand-painting
    /// later replaces this via the Terrain Paint Tool's Adopt step (ADR 0014), exactly like St Peters.</para>
    ///
    /// <para>Registers itself into <see cref="GameServices.TidalTerrain"/> on enable and relinquishes on
    /// disable (guarded, so an additive scene swap's last writer wins) — the same self-installing pattern
    /// as <see cref="TidalTerrain"/>. Pull, not push: sampled on demand, nothing precomputed per tick.</para>
    /// </summary>
    public sealed class RectTidalTerrain : MonoBehaviour, ITidalTerrain
    {
        /// <summary>
        /// One rectangular land feature: flat at <see cref="Elevation"/> inside the rect (centre ±
        /// half-size), smoothstepping down to the region's deep floor across <see cref="Falloff"/> metres
        /// outside it. Plain serializable struct so the zone list is Inspector-tunable data (rule 6).
        /// </summary>
        [System.Serializable]
        public struct LandZone
        {
            [Tooltip("Centre of the rectangular land feature (world XY).")]
            public Vector2 Center;
            [Tooltip("Half-size (m) of the flat top either side of the centre (x = half-width, y = half-height).")]
            public Vector2 HalfSize;
            [Tooltip("Elevation (m above chart datum) of the flat top. Above the highest water = always dry.")]
            public float Elevation;
            [Tooltip("How far (m) the ground slopes from the rectangle's edge down to the deep floor — the " +
                     "beach/quay-edge band the tide's waterline sweeps across. Gentle = a wide visible " +
                     "intertidal beach; steep = a dredged edge whose waterline barely moves.")]
            public float Falloff;

            public LandZone(Vector2 center, Vector2 halfSize, float elevation, float falloff)
            {
                Center = center; HalfSize = halfSize; Elevation = elevation; Falloff = falloff;
            }
        }

        [Header("Deep floor (the seabed everywhere no land zone reaches)")]
        [Tooltip("Seabed elevation of the open/deep water, metres above chart datum. Keep it below the " +
                 "lowest tide so the open water never bares and a boat never grounds there.")]
        [SerializeField] private float _deepElevation = -4f;

        [Header("Land zones (rectangular plateaus; the highest feature wins)")]
        [Tooltip("The region's land features — quay strips, dock spurs, wharf decks. Each is flat inside " +
                 "its rectangle and slopes to the deep floor across its falloff.")]
        [SerializeField] private LandZone[] _zones = System.Array.Empty<LandZone>();

        private void OnEnable() => GameServices.TidalTerrain = this;

        private void OnDisable()
        {
            // Only relinquish the accessor if it still points at us (don't stomp a region that
            // registered after we did during an additive scene swap) — mirrors TidalTerrain.
            if (ReferenceEquals(GameServices.TidalTerrain, this))
                GameServices.TidalTerrain = null;
        }

        /// <inheritdoc/>
        public float ElevationAt(Vector2 worldPos) => ElevationAtZones(worldPos, _deepElevation, _zones);

        /// <summary>
        /// Author the zones in one call (the region builders use this before the scene is saved, the same
        /// direct-configure convention as <c>RegionAnchor.Configure</c>). Null zones = an empty list.
        /// </summary>
        public void Configure(float deepElevation, LandZone[] zones)
        {
            _deepElevation = deepElevation;
            _zones = zones ?? System.Array.Empty<LandZone>();
        }

        // --- pure composition (static, headless-testable; no Unity object state) ----------------------

        /// <summary>
        /// The pure zone composition (no Unity calls, no RNG) — exposed so EditMode tests can assert the
        /// authored coast without a scene. Deep floor everywhere; each land zone raises the ground where
        /// present (max-compose, like <see cref="TidalTerrain.ElevationAtZones"/>).
        /// </summary>
        public static float ElevationAtZones(Vector2 worldPos, float deepElevation, LandZone[] zones)
        {
            float e = deepElevation;
            if (zones == null) return e;
            for (int i = 0; i < zones.Length; i++)
            {
                float d = DistanceOutsideRect(worldPos, zones[i].Center, zones[i].HalfSize);
                float z = Plateau(d, zones[i].Falloff, zones[i].Elevation, deepElevation);
                if (z > e) e = z;
            }
            return e;
        }

        /// <summary>
        /// Distance (m) from <paramref name="p"/> to the edge of the axis-aligned rectangle
        /// (<paramref name="center"/> ± <paramref name="halfSize"/>); 0 anywhere inside it.
        /// </summary>
        public static float DistanceOutsideRect(Vector2 p, Vector2 center, Vector2 halfSize)
        {
            float dx = Mathf.Max(Mathf.Abs(p.x - center.x) - Mathf.Max(halfSize.x, 0f), 0f);
            float dy = Mathf.Max(Mathf.Abs(p.y - center.y) - Mathf.Max(halfSize.y, 0f), 0f);
            return Mathf.Sqrt(dx * dx + dy * dy);
        }

        /// <summary>
        /// A plateau-with-falloff profile by distance: <paramref name="inner"/> at distance 0 (inside the
        /// rect), easing (smoothstep) to <paramref name="outer"/> by <paramref name="falloff"/>, then flat
        /// at <paramref name="outer"/> beyond — the same profile shape as <c>TidalTerrain.Lerped</c> with a
        /// zero flat-radius, so the two analytic terrains shelve identically.
        /// </summary>
        private static float Plateau(float distance, float falloff, float inner, float outer)
        {
            if (distance <= 0f) return inner;
            if (falloff <= 0f) return outer;
            float u = Mathf.Clamp01(distance / falloff);
            return Mathf.Lerp(inner, outer, Mathf.SmoothStep(0f, 1f, u));
        }
    }
}
