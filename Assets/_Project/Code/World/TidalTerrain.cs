using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.World
{
    /// <summary>
    /// The world's authored <b>height map</b> for a region — the concrete <see cref="ITidalTerrain"/>
    /// the St Peters opening hangs on. It publishes a per-position ground/seabed elevation (metres above
    /// chart datum, higher = drier) and registers itself into <see cref="GameServices.TidalTerrain"/> on
    /// enable so gameplay (the on-foot walkability sim) and the future depth-gradient water shader read it
    /// through Core WITHOUT referencing the World module (CLAUDE.md rule 4; ADR 0009). It clears the
    /// accessor on disable so a region teardown leaves "open water" (a null terrain) behind.
    ///
    /// <para><b>Authored ELEVATION ZONES (deterministic, never saved).</b> Elevation is a pure function of
    /// world position composed from a few authored zones — there is <b>no RNG</b> and <b>nothing is
    /// serialized at runtime</b> (the field is reconstructed geometry, recomputed not persisted — CLAUDE.md
    /// rule 5). The St Peters showcase, evaluated against the deterministic water level
    /// (<see cref="IEnvironmentService.WaterLevelAt"/>) via <see cref="TidalExposure"/>:</para>
    /// <list type="bullet">
    /// <item><description><b>Island</b> — a high plateau (always exposed; you can't tide it under).</description></item>
    /// <item><description><b>Sandbar</b> — a ridge crest just BELOW high water that bridges the island to
    /// Greywick: covered at high tide, exposing as the tide falls (widest walkable flat at low water). The
    /// showcase's walker path.</description></item>
    /// <item><description><b>Channel</b> — a deeper trough cut THROUGH the sandbar: boat-crossable at higher
    /// tide, narrowing as the tide falls. The showcase's boat passage — inverse of the flats over the tide.</description></item>
    /// <item><description><b>Deep harbour</b> — a low seabed everywhere else (never bares; always boatable).</description></item>
    /// </list>
    /// The zones are smoothly blended (the ridge and channel are raised-cosine bumps, not hard steps) so the
    /// shoreline and channel banks creep across the flats continuously as the tide moves — the continuous
    /// low→high transformation §7 calls for, not a staircase.
    ///
    /// <para><b>Pull, not push.</b> Sampled on demand by <see cref="ElevationAt"/>; nothing is precomputed
    /// per tile per tick. Authored geometry lives in the serialized zone fields so the owner can tune the
    /// showcase from the inspector (no magic numbers in code — CLAUDE.md rule 6).</para>
    /// </summary>
    public sealed class TidalTerrain : MonoBehaviour, ITidalTerrain
    {
        [Header("Deep harbour (the floor everywhere else)")]
        [Tooltip("Seabed elevation of the open/deep water, metres above chart datum. Well below the lowest " +
                 "tide so the harbour never bares and a boat never grounds there.")]
        [SerializeField] private float _deepHarbourElevation = -4f;

        [Header("Island (the high home ground)")]
        [Tooltip("Centre of the island plateau (world XY).")]
        [SerializeField] private Vector2 _islandCenter = new Vector2(-40f, 0f);
        [Tooltip("Radius (m) of the flat island plateau (inside this it sits at the plateau height).")]
        [SerializeField] private float _islandRadius = 22f;
        [Tooltip("How far (m) the island's beach slopes down from the plateau edge into the sea.")]
        [SerializeField] private float _islandFalloff = 10f;
        [Tooltip("Island plateau elevation, metres above chart datum. High enough to stay dry at every " +
                 "tide (always exposed).")]
        [SerializeField] private float _islandElevation = 6f;

        [Header("Sandbar ridge (the tide-gated walking path to Greywick)")]
        [Tooltip("One end of the sandbar's centre-line (world XY) — toward the island.")]
        [SerializeField] private Vector2 _sandbarFrom = new Vector2(-22f, 0f);
        [Tooltip("Other end of the sandbar's centre-line (world XY) — toward Greywick.")]
        [SerializeField] private Vector2 _sandbarTo = new Vector2(34f, 0f);
        [Tooltip("Half-width (m) of the sandbar either side of its centre-line — the flats bare out to here.")]
        [SerializeField] private float _sandbarHalfWidth = 9f;
        [Tooltip("Crest elevation of the sandbar, metres above chart datum. Authored JUST BELOW the " +
                 "region's high water so the bar covers at high tide and emerges as the tide falls — the " +
                 "widest walkable flat at low water.")]
        [SerializeField] private float _sandbarCrestElevation = 1.6f;

        [Header("Channel (the boat passage cut through the sandbar)")]
        [Tooltip("Where the channel crosses the sandbar, as a fraction (0..1) along the From→To centre-line.")]
        [Range(0f, 1f)]
        [SerializeField] private float _channelAlong = 0.62f;
        [Tooltip("Half-width (m) of the channel cut. Boat-crossable at higher tide; the flats either side " +
                 "bare first as the tide falls, narrowing the safe gap.")]
        [SerializeField] private float _channelHalfWidth = 4.5f;
        [Tooltip("Channel-bed elevation, metres above chart datum. Below the crest (so water lingers in the " +
                 "gut), but shallower than the deep harbour so it narrows / shoals as the tide drops.")]
        [SerializeField] private float _channelBedElevation = -0.6f;

        private void OnEnable() => GameServices.TidalTerrain = this;

        private void OnDisable()
        {
            // Only relinquish the accessor if it still points at us (don't stomp a region that
            // registered after we did during an additive scene swap).
            if (ReferenceEquals(GameServices.TidalTerrain, this))
                GameServices.TidalTerrain = null;
        }

        /// <inheritdoc/>
        public float ElevationAt(Vector2 worldPos) => ElevationAtZones(worldPos);

        /// <summary>
        /// The pure zone composition (no Unity calls, no RNG) — exposed so an EditMode test can assert the
        /// authored zones without a scene. Composes the deep-harbour floor with the island plateau, the
        /// sandbar ridge, and the channel trough by taking the max ground (whichever feature is highest at
        /// the position wins), then carving the channel back down through the bar.
        /// </summary>
        public float ElevationAtZones(Vector2 worldPos)
        {
            // Deep harbour is the floor; the island and sandbar raise the ground above it where present.
            float e = _deepHarbourElevation;

            // Island: a flat plateau inside the radius, sloping down to the deep floor across the falloff.
            float dIsland = Vector2.Distance(worldPos, _islandCenter);
            float island = Lerped(dIsland, _islandRadius, _islandFalloff, _islandElevation, _deepHarbourElevation);
            if (island > e) e = island;

            // Sandbar: a ridge along the From→To segment. Raise toward the crest near the centre-line,
            // falling to the deep floor at the half-width edge (so the flats' shoreline creeps as tide moves).
            float dBar = DistanceToSegment(worldPos, _sandbarFrom, _sandbarTo);
            float bar = Lerped(dBar, 0f, _sandbarHalfWidth, _sandbarCrestElevation, _deepHarbourElevation);
            if (bar > e) e = bar;

            // Channel: a trough cut across the bar at the crossing point. Where the cut applies, pull the
            // ground DOWN toward the channel bed (a boat-crossable gut) — but never below the deep floor.
            Vector2 crossing = Vector2.Lerp(_sandbarFrom, _sandbarTo, _channelAlong);
            float dChannel = DistanceToSegmentPerpendicular(worldPos, _sandbarFrom, _sandbarTo, crossing);
            if (dChannel < _channelHalfWidth && e > _channelBedElevation)
            {
                // 1 at the channel centre-line, easing to 0 at the channel edge — carve smoothly.
                float carve = SmoothFalloff(dChannel, _channelHalfWidth);
                float carved = Mathf.Lerp(e, _channelBedElevation, carve);
                e = Mathf.Max(carved, _deepHarbourElevation);
            }

            return e;
        }

        // --- pure helpers (static, testable) ----------------------------------------------------------

        /// <summary>
        /// A plateau-with-falloff profile by distance: <paramref name="inner"/> at/inside
        /// <paramref name="flatRadius"/>, easing (smoothstep) to <paramref name="outer"/> by
        /// <c>flatRadius + falloff</c>, then flat at <paramref name="outer"/> beyond.
        /// </summary>
        private static float Lerped(float distance, float flatRadius, float falloff, float inner, float outer)
        {
            if (distance <= flatRadius) return inner;
            if (falloff <= 0f) return outer;
            float u = Mathf.Clamp01((distance - flatRadius) / falloff);
            return Mathf.Lerp(inner, outer, Mathf.SmoothStep(0f, 1f, u));
        }

        /// <summary>1 at d=0, smoothly easing to 0 at d=half (and 0 beyond). A raised-cosine-ish bump.</summary>
        private static float SmoothFalloff(float d, float half)
        {
            if (half <= 0f) return 0f;
            float u = Mathf.Clamp01(d / half);
            return 1f - Mathf.SmoothStep(0f, 1f, u);
        }

        /// <summary>Shortest distance from <paramref name="p"/> to the segment a→b.</summary>
        private static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float len2 = ab.sqrMagnitude;
            float t = len2 <= 1e-6f ? 0f : Mathf.Clamp01(Vector2.Dot(p - a, ab) / len2);
            Vector2 proj = a + t * ab;
            return Vector2.Distance(p, proj);
        }

        /// <summary>Distance from <paramref name="p"/> to the line that crosses the bar at
        /// <paramref name="crossing"/> PERPENDICULAR to the bar's a→b axis — i.e. how far "along" the bar
        /// the point is from the channel cut. This is what gives the channel its width ACROSS the bar.</summary>
        private static float DistanceToSegmentPerpendicular(Vector2 p, Vector2 a, Vector2 b, Vector2 crossing)
        {
            Vector2 axis = (b - a);
            if (axis.sqrMagnitude <= 1e-6f) return Vector2.Distance(p, crossing);
            axis.Normalize();
            // Signed distance along the bar axis from the crossing point.
            return Mathf.Abs(Vector2.Dot(p - crossing, axis));
        }
    }
}
