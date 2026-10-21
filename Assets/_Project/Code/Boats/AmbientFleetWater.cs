using System;
using System.Collections.Generic;
using UnityEngine;

namespace HiddenHarbours.Boats
{
    /// <summary>How a <see cref="FleetKeepClearArea"/> reads its two points and its radius.</summary>
    public enum FleetKeepClearShape
    {
        /// <summary>Every point within <c>Radius</c> of the segment A→B. A = B is a disc; radius 0 a point.</summary>
        Capsule = 0,
        /// <summary>The axis-aligned box with corners A and B, grown by <c>Radius</c>.</summary>
        Box = 1,
    }

    /// <summary>
    /// One place the ambient fleet keeps clear of: somewhere the player works (the slip, the approach, the
    /// wharf, the arrival route, the marks, the passages) or somewhere a test photographs. Data, not code
    /// (rule 2): Boats may not read the Editor builders these positions come from (rule 4), so the fleet's
    /// Def carries them, and <c>AmbientFleetGroundsTests</c> ties every entry to its source. A feature that
    /// moves in its builder fails that guard until the asset moves with it.
    /// </summary>
    [Serializable]
    public struct FleetKeepClearArea
    {
        [Tooltip("What the area is. AmbientFleetGroundsTests matches each entry to its source by this name.")]
        public string Name;
        [Tooltip("Where the numbers come from (file and symbol). For the reader; the guard checks the numbers.")]
        public string Source;
        [Tooltip("Capsule: every point within Radius of the segment A to B (A = B is a disc). " +
                 "Box: the axis-aligned box with corners A and B, grown by Radius.")]
        public FleetKeepClearShape Shape;
        [Tooltip("One end of the segment, or one corner of the box (world units).")]
        public Vector2 A;
        [Tooltip("The other end of the segment, or the opposite corner of the box (world units).")]
        public Vector2 B;
        [Tooltip("The feature's own half-width (m): a channel's half-width, a passage's round. The fleet's " +
                 "clearance (BoatAvoidRadius + PlayerAvoidRadius) is added on top, never folded in here.")]
        [Min(0f)] public float Radius;

        /// <summary>Distance (m) from <paramref name="p"/> to the area; 0 on or inside it.</summary>
        public float DistanceTo(Vector2 p)
        {
            float d = Shape == FleetKeepClearShape.Box
                ? FleetGeometry.PointToBox(p, Vector2.Min(A, B), Vector2.Max(A, B))
                : FleetGeometry.PointToSegment(p, A, B);
            return Mathf.Max(0f, d - Radius);
        }

        /// <summary>Shortest distance (m) from the segment <paramref name="from"/>→<paramref name="to"/>
        /// to the area; 0 when they touch.</summary>
        public float DistanceTo(Vector2 from, Vector2 to)
        {
            float d = Shape == FleetKeepClearShape.Box
                ? FleetGeometry.SegmentToBox(from, to, Vector2.Min(A, B), Vector2.Max(A, B))
                : FleetGeometry.SegmentToSegment(from, to, A, B);
            return Mathf.Max(0f, d - Radius);
        }
    }

    /// <summary>
    /// <b>The water the ambient fleet may fish</b> (the owner's ruling on #886: the fleet fishes
    /// everywhere accessible). At spring low a point is ground when it (1) keeps the planner's depth
    /// margin, (2) joins <see cref="SeedPoint"/>, the open water the player arrives by, through water that
    /// keeps that margin, so a boat can always get there and back at any tide, (3) stands
    /// <see cref="Clearance"/> inside <see cref="Bounds"/>, and (4) stands <see cref="Clearance"/> off every
    /// <see cref="FleetKeepClearArea"/>. Water cut off behind a bar that dries at spring low is never ground.
    ///
    /// <para><b>Measured once, then read.</b> <see cref="Build"/> samples the spring-low depth at every
    /// cell centre one time, per region load and tide profile (the presenter caches the result). The flood
    /// fill behind (2) runs once for each margin the planner asks about, and is kept. Nothing here runs per
    /// frame, and the planner's per-candidate reads are array lookups.</para>
    ///
    /// <para><b>The grid finds candidates; it never vouches for them.</b> A cell is judged by its centre,
    /// so the planner re-checks every spot and every leg exactly against the terrain sampler
    /// (<see cref="ElevationAt"/>), the bounds and the areas (<see cref="IsClear"/>,
    /// <see cref="IsLegKeptClear"/>).</para>
    ///
    /// <para><b>Reachability ignores the keep-clear areas.</b> A boat can sail through the approach; keeping
    /// clear is a courtesy the planner pays with its spots and legs, not a wall in the sea. Pure and
    /// engine-light like <see cref="AmbientFleetPlan"/>: the terrain arrives as a sampler, through Core.</para>
    /// </summary>
    public sealed class AmbientFleetWater
    {
        private readonly float[] _depth;   // spring-low depth at each cell centre (m; negative = dry)
        private readonly bool[] _clear;    // the centre stands the clearance inside the bounds and off every area
        private readonly FleetKeepClearArea[] _keepClear;
        private readonly Rect _inner;      // the bounds less the clearance: where a spot may lie
        private readonly List<Reach> _reaches = new List<Reach>(4);

        private sealed class Reach
        {
            public float Margin;
            public bool[] Reachable;
            public int[] Eligible;         // reachable and clear, in cell order
        }

        /// <summary>The rectangle the water is measured inside (world units).</summary>
        public Rect Bounds { get; }
        /// <summary>The side of one grid cell (m).</summary>
        public float CellMeters { get; }
        public int Columns { get; }
        public int Rows { get; }
        /// <summary>The open water every spot must join (world units).</summary>
        public Vector2 SeedPoint { get; }
        /// <summary>The lowest water the tide reaches (spring low), the level depths are read against.</summary>
        public float MinWaterLevel { get; }
        /// <summary>How far (m) spots and legs stand off the areas and inside the bounds.</summary>
        public float Clearance { get; }
        /// <summary>The terrain sampler the water was measured with, for the planner's exact checks.</summary>
        public Func<Vector2, float> ElevationAt { get; }
        public int CellCount => _depth.Length;
        public int KeepClearCount => _keepClear.Length;

        private AmbientFleetWater(Rect bounds, float cellMeters, int columns, int rows, Vector2 seedPoint,
                                  Func<Vector2, float> elevationAt, float minWaterLevel,
                                  FleetKeepClearArea[] keepClear, float clearance)
        {
            Bounds = bounds;
            CellMeters = cellMeters;
            Columns = columns;
            Rows = rows;
            SeedPoint = seedPoint;
            ElevationAt = elevationAt;
            MinWaterLevel = minWaterLevel;
            Clearance = clearance;
            _keepClear = keepClear;
            _inner = Rect.MinMaxRect(bounds.xMin + clearance, bounds.yMin + clearance,
                                     bounds.xMax - clearance, bounds.yMax - clearance);
            _depth = new float[columns * rows];
            _clear = new bool[columns * rows];
        }

        /// <summary>
        /// Measure the water once: the spring-low depth and the keep-clear verdict at every cell centre of
        /// a <paramref name="cellMeters"/> grid over <paramref name="bounds"/>. Reachability is filled in
        /// lazily, per margin, by the first planner call that needs it.
        /// </summary>
        public static AmbientFleetWater Build(Rect bounds, float cellMeters, Vector2 seedPoint,
                                              Func<Vector2, float> elevationAt, float minWaterLevel,
                                              FleetKeepClearArea[] keepClear, float clearance)
        {
            if (elevationAt == null) throw new ArgumentNullException(nameof(elevationAt));
            float cell = Mathf.Max(0.5f, cellMeters);
            int columns = Mathf.Max(1, Mathf.CeilToInt(bounds.width / cell));
            int rows = Mathf.Max(1, Mathf.CeilToInt(bounds.height / cell));
            var water = new AmbientFleetWater(bounds, cell, columns, rows, seedPoint, elevationAt, minWaterLevel,
                                              keepClear ?? Array.Empty<FleetKeepClearArea>(),
                                              Mathf.Max(0f, clearance));

            for (int i = 0; i < water._depth.Length; i++)
            {
                Vector2 c = water.CellCentre(i);
                water._depth[i] = minWaterLevel - elevationAt(c);
                water._clear[i] = water.IsClear(c);
            }
            return water;
        }

        /// <summary>
        /// Measure <paramref name="def"/>'s water: its grounds rectangle, cell, seed and keep-clear areas,
        /// with the clearance <c>BoatAvoidRadius + PlayerAvoidRadius</c>. A fleet boat that her boat ring
        /// pushes off her mark must still stand outside her player ring from a player at the area's edge.
        /// The presenter and <c>AmbientFleetGroundsTests</c> both measure through this call.
        /// </summary>
        public static AmbientFleetWater For(AmbientFleetDef def, Func<Vector2, float> elevationAt,
                                            float minWaterLevel)
            => Build(new Rect(def.GroundsCenter - def.GroundsSize * 0.5f, def.GroundsSize), def.AccessCellMeters,
                     def.AccessSeedPoint, elevationAt, minWaterLevel, def.KeepClear,
                     def.BoatAvoidRadius + def.PlayerAvoidRadius);

        /// <summary>The centre of cell <paramref name="index"/> (row-major from the bounds' min corner).</summary>
        public Vector2 CellCentre(int index)
        {
            int col = index % Columns, row = index / Columns;
            return new Vector2(Bounds.xMin + (col + 0.5f) * CellMeters, Bounds.yMin + (row + 0.5f) * CellMeters);
        }

        /// <summary>The cell holding <paramref name="p"/>, or −1 outside the grid.</summary>
        public int CellAt(Vector2 p)
        {
            float fx = (p.x - Bounds.xMin) / CellMeters, fy = (p.y - Bounds.yMin) / CellMeters;
            if (fx < 0f || fy < 0f) return -1;
            int col = (int)fx, row = (int)fy;
            if (col >= Columns || row >= Rows) return -1;
            return row * Columns + col;
        }

        /// <summary>The spring-low depth (m) measured at the centre of cell <paramref name="index"/>.</summary>
        public float DepthAtCell(int index) => _depth[index];

        /// <summary>Does <paramref name="p"/> stand <see cref="Clearance"/> inside the bounds and off every
        /// area? Exact: this is the rule, not the grid's reading of it.</summary>
        public bool IsClear(Vector2 p)
        {
            if (p.x < _inner.xMin || p.x > _inner.xMax || p.y < _inner.yMin || p.y > _inner.yMax) return false;
            for (int i = 0; i < _keepClear.Length; i++)
                if (_keepClear[i].DistanceTo(p) < Clearance) return false;
            return true;
        }

        /// <summary>Does the whole leg <paramref name="from"/>→<paramref name="to"/> stand
        /// <see cref="Clearance"/> inside the bounds and off every area? Exact. The inner bounds are convex,
        /// so a leg whose ends are inside is inside.</summary>
        public bool IsLegKeptClear(Vector2 from, Vector2 to)
        {
            if (!InsideInner(from) || !InsideInner(to)) return false;
            for (int i = 0; i < _keepClear.Length; i++)
                if (_keepClear[i].DistanceTo(from, to) < Clearance) return false;
            return true;
        }

        /// <summary>Is <paramref name="p"/>'s cell joined to <see cref="SeedPoint"/> by water keeping
        /// <paramref name="margin"/> at spring low?</summary>
        public bool IsReachable(Vector2 p, float margin)
        {
            int cell = CellAt(p);
            return cell >= 0 && ReachFor(margin).Reachable[cell];
        }

        /// <summary>Is <paramref name="p"/>'s cell a candidate: reachable at <paramref name="margin"/>, and
        /// clear by its centre? The planner still checks the point itself exactly.</summary>
        public bool IsEligible(Vector2 p, float margin)
        {
            int cell = CellAt(p);
            return cell >= 0 && _clear[cell] && ReachFor(margin).Reachable[cell];
        }

        /// <summary>How many cells are candidates at <paramref name="margin"/>.</summary>
        public int EligibleCount(float margin) => ReachFor(margin).Eligible.Length;

        /// <summary>How many cells are reachable at <paramref name="margin"/>, whatever the areas.</summary>
        public int ReachableCount(float margin)
        {
            bool[] reach = ReachFor(margin).Reachable;
            int n = 0;
            for (int i = 0; i < reach.Length; i++) if (reach[i]) n++;
            return n;
        }

        /// <summary>The candidate cells at <paramref name="margin"/>, in cell order. Shared, never copy it
        /// per call; the planner only reads it.</summary>
        internal int[] EligibleCells(float margin) => ReachFor(margin).Eligible;

        /// <summary>Roughly what the measured water holds in memory (bytes): the depth and clear grids plus
        /// each margin's reach and candidate list.</summary>
        public long ApproximateBytes
        {
            get
            {
                long bytes = _depth.Length * (sizeof(float) + sizeof(bool));
                for (int i = 0; i < _reaches.Count; i++)
                    bytes += _reaches[i].Reachable.Length * sizeof(bool) + _reaches[i].Eligible.Length * sizeof(int);
                return bytes;
            }
        }

        private bool InsideInner(Vector2 p) =>
            p.x >= _inner.xMin && p.x <= _inner.xMax && p.y >= _inner.yMin && p.y <= _inner.yMax;

        private Reach ReachFor(float margin)
        {
            for (int i = 0; i < _reaches.Count; i++)
                if (_reaches[i].Margin == margin) return _reaches[i];

            // Four-way flood fill from the seed's cell through cells whose centre keeps the margin. Four-way,
            // not eight: two dry cells touching at a corner close the gap between them.
            int n = _depth.Length;
            var reachable = new bool[n];
            int seed = CellAt(SeedPoint);
            if (seed >= 0 && _depth[seed] >= margin)
            {
                var queue = new int[n];
                int head = 0, tail = 0;
                reachable[seed] = true;
                queue[tail++] = seed;
                while (head < tail)
                {
                    int i = queue[head++];
                    int col = i % Columns, row = i / Columns;
                    if (col > 0) Visit(i - 1);
                    if (col < Columns - 1) Visit(i + 1);
                    if (row > 0) Visit(i - Columns);
                    if (row < Rows - 1) Visit(i + Columns);
                }

                void Visit(int j)
                {
                    if (reachable[j] || _depth[j] < margin) return;
                    reachable[j] = true;
                    queue[tail++] = j;
                }
            }

            int count = 0;
            for (int i = 0; i < n; i++) if (reachable[i] && _clear[i]) count++;
            var eligible = new int[count];
            for (int i = 0, k = 0; i < n; i++) if (reachable[i] && _clear[i]) eligible[k++] = i;

            var reach = new Reach { Margin = margin, Reachable = reachable, Eligible = eligible };
            _reaches.Add(reach);
            return reach;
        }
    }

    /// <summary>The fleet's planar distances: point, segment and axis-aligned box, exact.</summary>
    internal static class FleetGeometry
    {
        public static float PointToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float len2 = ab.sqrMagnitude;
            float t = len2 > 0f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / len2) : 0f;
            return Vector2.Distance(p, a + ab * t);
        }

        public static float PointToBox(Vector2 p, Vector2 min, Vector2 max)
        {
            float dx = Mathf.Max(min.x - p.x, 0f, p.x - max.x);
            float dy = Mathf.Max(min.y - p.y, 0f, p.y - max.y);
            return Mathf.Sqrt(dx * dx + dy * dy);
        }

        /// <summary>Shortest distance between two segments; 0 when they cross or touch. In the plane, two
        /// segments that do not cross are closest at an end of one of them.</summary>
        public static float SegmentToSegment(Vector2 p1, Vector2 p2, Vector2 q1, Vector2 q2)
        {
            if (Cross(p1, p2, q1, q2)) return 0f;
            return Mathf.Min(Mathf.Min(PointToSegment(p1, q1, q2), PointToSegment(p2, q1, q2)),
                             Mathf.Min(PointToSegment(q1, p1, p2), PointToSegment(q2, p1, p2)));
        }

        /// <summary>Shortest distance between a segment and a box; 0 when the segment enters it.</summary>
        public static float SegmentToBox(Vector2 a, Vector2 b, Vector2 min, Vector2 max)
        {
            if (PointToBox(a, min, max) == 0f || PointToBox(b, min, max) == 0f) return 0f;
            var c0 = new Vector2(min.x, min.y);
            var c1 = new Vector2(max.x, min.y);
            var c2 = new Vector2(max.x, max.y);
            var c3 = new Vector2(min.x, max.y);
            return Mathf.Min(Mathf.Min(SegmentToSegment(a, b, c0, c1), SegmentToSegment(a, b, c1, c2)),
                             Mathf.Min(SegmentToSegment(a, b, c2, c3), SegmentToSegment(a, b, c3, c0)));
        }

        /// <summary>Do the segments cross at a point inside both? (Touching is left to the end distances.)</summary>
        private static bool Cross(Vector2 p1, Vector2 p2, Vector2 q1, Vector2 q2)
        {
            float d1 = Side(q1, q2, p1), d2 = Side(q1, q2, p2);
            float d3 = Side(p1, p2, q1), d4 = Side(p1, p2, q2);
            return ((d1 > 0f && d2 < 0f) || (d1 < 0f && d2 > 0f)) &&
                   ((d3 > 0f && d4 < 0f) || (d3 < 0f && d4 > 0f));
        }

        private static float Side(Vector2 o, Vector2 u, Vector2 v) =>
            (u.x - o.x) * (v.y - o.y) - (u.y - o.y) * (v.x - o.x);
    }
}
