using UnityEngine;

namespace HiddenHarbours.World
{
    /// <summary>
    /// <b>A way across from one region to the next, on foot or by a carrier</b>, as DATA (ADR 0003): one
    /// asset per crossing under <c>Data/Regions/Crossings</c>, keyed by a stable, append-only
    /// <see cref="Id"/> (<c>crossing.snake_case</c>). What a cross-region routine walks when one of its
    /// blocks names a different region from the block before it (owner ask 2026-11-15: St Peters people
    /// cross the bar to Nine Mile Creek to run errands).
    ///
    /// <para><b>Two paths, one per region, meeting at the seam.</b> Every region has its own local frame
    /// (every <see cref="RegionDef.WorldCenter"/> is the origin), so one polyline cannot span two of them.
    /// <see cref="FromPath"/> runs in the FROM region's frame, from the near station out to that region's
    /// edge passage; <see cref="ToPath"/> runs in the TO region's frame, from its edge passage in to the far
    /// station. Walked end to end, the crossing is <see cref="LengthMetres"/> long, and the walker is in the
    /// FROM region until they have walked <see cref="FromLengthMetres"/> and in the TO region after it,
    /// which is the property that keeps an NPC in exactly one region at every instant. The seam is where
    /// the player's own scene changes too (the <c>RegionPassage</c> at each region's edge).</para>
    ///
    /// <para><b>Either direction.</b> A crossing is walked forwards (FROM to TO) or backwards (TO to FROM);
    /// one asset serves the trip out and the trip home.</para>
    ///
    /// <para><b>Nothing here is saved and nothing ticks.</b> Where a crossing walker IS is a pure function
    /// of this asset, the routine, the gate and the clock (<see cref="RoutineLegs"/>).</para>
    /// </summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/Region Crossing", fileName = "RegionCrossing")]
    public class RegionCrossingDef : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Stable id, append-only (crossing.snake_case, e.g. crossing.st_peters_nine_mile_creek_bar). " +
                 "Also the id a crossing gate is asked about (Core ICrossingGate).")]
        public string Id = "crossing.example";

        [Header("Ends")]
        [Tooltip("The region the forward walk starts in (region.snake_case).")]
        public string FromRegionId = "";

        [Tooltip("The station, in the FROM region's table, that FromPath starts at. A routine block that " +
                 "sets out across this crossing forwards must be leaving from here.")]
        public string FromStationId = "";

        [Tooltip("The region the forward walk ends in (region.snake_case).")]
        public string ToRegionId = "";

        [Tooltip("The station, in the TO region's table, that ToPath ends at. A routine block that crosses " +
                 "forwards names this as its station.")]
        public string ToStationId = "";

        [Header("The ground (metres, each in its own region's frame)")]
        [Tooltip("FROM region frame: from the near station out to the region's edge passage. At least two " +
                 "points; the last one sits at the seam.")]
        public Vector2[] FromPath = System.Array.Empty<Vector2>();

        [Tooltip("TO region frame: from the region's edge passage in to the far station. At least two " +
                 "points; the first one sits at the seam.")]
        public Vector2[] ToPath = System.Array.Empty<Vector2>();

        /// <summary>Metres walked on the FROM side, near station to seam.</summary>
        public float FromLengthMetres => PathLength(FromPath);

        /// <summary>Metres walked on the TO side, seam to far station.</summary>
        public float ToLengthMetres => PathLength(ToPath);

        /// <summary>The whole crossing, end to end. Derived, never stored, so it cannot disagree with the
        /// ground it measures.</summary>
        public float LengthMetres => FromLengthMetres + ToLengthMetres;

        /// <summary>True when both paths can be walked: two points or more each.</summary>
        public bool IsWalkable => FromPath != null && FromPath.Length >= 2 && ToPath != null && ToPath.Length >= 2;

        /// <summary>
        /// Does this crossing join <paramref name="fromRegion"/> to <paramref name="toRegion"/>? True for
        /// the forward walk (<paramref name="reversed"/> false) or the walk back (true).
        /// </summary>
        public bool Joins(string fromRegion, string toRegion, out bool reversed)
        {
            reversed = false;
            if (string.IsNullOrEmpty(fromRegion) || string.IsNullOrEmpty(toRegion)) return false;
            if (fromRegion == FromRegionId && toRegion == ToRegionId) return true;
            if (fromRegion == ToRegionId && toRegion == FromRegionId) { reversed = true; return true; }
            return false;
        }

        /// <summary>The station the walk leaves from, in the direction given.</summary>
        public string NearStationId(bool reversed) => reversed ? ToStationId : FromStationId;

        /// <summary>The station the walk arrives at, in the direction given.</summary>
        public string FarStationId(bool reversed) => reversed ? FromStationId : ToStationId;

        /// <summary>
        /// Where someone <paramref name="metres"/> into the crossing is, in the direction given: the region
        /// they are in, the point in THAT region's frame, and the ground heading of travel (compass degrees,
        /// as <see cref="RoutineLaneTree.HeadingAlong"/> states it). Clamped to the two ends.
        /// The seam belongs to the side being walked into, so exactly one region answers at every distance.
        /// </summary>
        public Vector2 PointAt(float metres, bool reversed, out string regionId, out float headingDegrees)
        {
            float fromLen = FromLengthMetres, toLen = ToLengthMetres;
            float d = Mathf.Clamp(metres, 0f, fromLen + toLen);
            if (!reversed)
            {
                if (d < fromLen)
                {
                    regionId = FromRegionId;
                    return Along(FromPath, d, false, out headingDegrees);
                }
                regionId = ToRegionId;
                return Along(ToPath, d - fromLen, false, out headingDegrees);
            }

            if (d < toLen)
            {
                regionId = ToRegionId;
                return Along(ToPath, toLen - d, true, out headingDegrees);
            }
            regionId = FromRegionId;
            return Along(FromPath, fromLen - (d - toLen), true, out headingDegrees);
        }

        /// <summary>The point the walk starts at, in the direction given (its region's frame).</summary>
        public Vector2 NearPoint(bool reversed) =>
            reversed ? Last(ToPath) : First(FromPath);

        static Vector2 Along(Vector2[] path, float distance, bool backwards, out float headingDegrees)
        {
            headingDegrees = 0f;
            if (path == null || path.Length == 0) return Vector2.zero;
            if (path.Length == 1) return path[0];
            Vector2 p = RoutineLaneTree.PointAlong(path, 0, path.Length, distance);
            float h = RoutineLaneTree.HeadingAlong(path, 0, path.Length, distance, 0f);
            headingDegrees = backwards ? Mathf.Repeat(h + 180f, 360f) : h;
            return p;
        }

        static Vector2 First(Vector2[] path) => path != null && path.Length > 0 ? path[0] : Vector2.zero;
        static Vector2 Last(Vector2[] path) => path != null && path.Length > 0 ? path[path.Length - 1] : Vector2.zero;

        static float PathLength(Vector2[] path) =>
            path == null || path.Length < 2 ? 0f : RoutineLaneTree.PolylineLength(path, 0, path.Length);
    }
}
