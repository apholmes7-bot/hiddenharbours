using System;
using System.Collections.Generic;
using UnityEngine;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// The measuring stick <see cref="AmbientFleetGroundsTests"/> holds the fleet's planner to: its own flood
    /// fill, its own distances, its own count of where the spots fall. Nothing here touches the Boats
    /// module, so the guard never asks the planner for its own bar. It is pure (a depth sampler in, numbers
    /// out), so the same code can judge another planner's spots outside the editor.
    /// </summary>
    public static class AmbientFleetGroundsMeasure
    {
        /// <summary>A place the grounds keep clear of, as the guard reads it from its source: every point
        /// within <see cref="HalfWidth"/> of the segment A→B, or of the box with corners A and B.</summary>
        public struct Feature
        {
            public string Name;
            public bool IsBox;
            public Vector2 A;
            public Vector2 B;
            public float HalfWidth;

            public static Feature Segment(string name, Vector2 a, Vector2 b, float halfWidth) =>
                new Feature { Name = name, A = a, B = b, HalfWidth = halfWidth };

            public static Feature Point(string name, Vector2 p, float radius) => Segment(name, p, p, radius);

            public static Feature Box(string name, Rect r) =>
                new Feature { Name = name, IsBox = true, A = r.min, B = r.max };

            private Rect Rect => Rect.MinMaxRect(Mathf.Min(A.x, B.x), Mathf.Min(A.y, B.y),
                                                 Mathf.Max(A.x, B.x), Mathf.Max(A.y, B.y));

            /// <summary>How far <paramref name="p"/> stands off the feature (negative inside it).</summary>
            public float Gap(Vector2 p) =>
                (IsBox ? RectToPoint(Rect, p) : PointToSegment(p, A, B)) - HalfWidth;

            /// <summary>How far the leg <paramref name="from"/>→<paramref name="to"/> stands off the feature
            /// at its nearest (negative when it passes inside).</summary>
            public float Gap(Vector2 from, Vector2 to) =>
                (IsBox ? RectToSegment(Rect, from, to) : SegmentToSegment(from, to, A, B)) - HalfWidth;
        }

        /// <summary>The sea as the guard floods it: which cells of a grid over the map join the seed through
        /// water that keeps the margin, judged at each cell's centre, four ways.</summary>
        public sealed class Sea
        {
            public Rect Map;
            public float Cell;
            public int Columns;
            public int Rows;
            public float[] Depth;
            public bool[] Reached;
            public int ReachedCount;
            public int WetCount;   // cells keeping the margin, reached or not

            public Vector2 Centre(int i) =>
                new Vector2(Map.xMin + (i % Columns + 0.5f) * Cell, Map.yMin + (i / Columns + 0.5f) * Cell);

            public int IndexAt(Vector2 p)
            {
                int col = Mathf.FloorToInt((p.x - Map.xMin) / Cell), row = Mathf.FloorToInt((p.y - Map.yMin) / Cell);
                return col < 0 || row < 0 || col >= Columns || row >= Rows ? -1 : row * Columns + col;
            }
        }

        /// <summary>Flood the map from <paramref name="seed"/> through cells whose centre keeps
        /// <paramref name="margin"/>.</summary>
        public static Sea Flood(Func<Vector2, float> depthAt, Rect map, float cell, Vector2 seed, float margin)
        {
            var sea = new Sea
            {
                Map = map, Cell = cell,
                Columns = Mathf.CeilToInt(map.width / cell), Rows = Mathf.CeilToInt(map.height / cell),
            };
            int n = sea.Columns * sea.Rows;
            sea.Depth = new float[n];
            sea.Reached = new bool[n];
            for (int i = 0; i < n; i++)
            {
                sea.Depth[i] = depthAt(sea.Centre(i));
                if (sea.Depth[i] >= margin) sea.WetCount++;
            }

            int start = sea.IndexAt(seed);
            if (start < 0 || sea.Depth[start] < margin) return sea;
            var queue = new Queue<int>();
            sea.Reached[start] = true;
            queue.Enqueue(start);
            while (queue.Count > 0)
            {
                int i = queue.Dequeue();
                sea.ReachedCount++;
                int col = i % sea.Columns, row = i / sea.Columns;
                for (int k = 0; k < 4; k++)
                {
                    int c = col + (k == 0 ? -1 : k == 1 ? 1 : 0), r = row + (k == 2 ? -1 : k == 3 ? 1 : 0);
                    if (c < 0 || r < 0 || c >= sea.Columns || r >= sea.Rows) continue;
                    int j = r * sea.Columns + c;
                    if (sea.Reached[j] || sea.Depth[j] < margin) continue;
                    sea.Reached[j] = true;
                    queue.Enqueue(j);
                }
            }
            return sea;
        }

        /// <summary>Does <paramref name="spot"/> join the flooded sea? True when a straight line from it to a
        /// reached cell centre among the nine around it keeps <paramref name="margin"/>, sampled every
        /// <paramref name="step"/>.</summary>
        public static bool Joins(Sea sea, Vector2 spot, Func<Vector2, float> depthAt, float margin, float step)
        {
            int at = sea.IndexAt(spot);
            if (at < 0) return false;
            int col = at % sea.Columns, row = at / sea.Columns;
            for (int dr = -1; dr <= 1; dr++)
            for (int dc = -1; dc <= 1; dc++)
            {
                int c = col + dc, r = row + dr;
                if (c < 0 || r < 0 || c >= sea.Columns || r >= sea.Rows) continue;
                int j = r * sea.Columns + c;
                if (sea.Reached[j] && ShallowestAlong(depthAt, spot, sea.Centre(j), step, out _) >= margin)
                    return true;
            }
            return false;
        }

        /// <summary>The shallowest depth along a segment, sampled every <paramref name="step"/>, ends included.</summary>
        public static float ShallowestAlong(Func<Vector2, float> depthAt, Vector2 from, Vector2 to, float step,
                                            out Vector2 at)
        {
            int steps = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(from, to) / step));
            float worst = float.MaxValue;
            at = from;
            for (int i = 0; i <= steps; i++)
            {
                Vector2 p = Vector2.Lerp(from, to, i / (float)steps);
                float d = depthAt(p);
                if (d < worst) { worst = d; at = p; }
            }
            return worst;
        }

        /// <summary>The guard's accessible water, cell by cell: reached, with its centre inside
        /// <paramref name="inner"/> and at least <paramref name="clearance"/> off every feature.</summary>
        public static bool[] Accessible(Sea sea, Rect inner, IList<Feature> features, float clearance)
        {
            var ok = new bool[sea.Reached.Length];
            for (int i = 0; i < ok.Length; i++)
            {
                if (!sea.Reached[i]) continue;
                Vector2 c = sea.Centre(i);
                if (!inner.Contains(c)) continue;
                bool clear = true;
                for (int f = 0; f < features.Count && clear; f++) clear = features[f].Gap(c) >= clearance;
                ok[i] = clear;
            }
            return ok;
        }

        /// <summary>Where the spots fell, counted on a coarse grid over the map.</summary>
        public sealed class Coverage
        {
            public float CoarseMeters;
            public int Columns;
            public int Rows;
            public double[] Area;    // accessible m² in each coarse cell
            public int[] Spots;      // spots that fell in each coarse cell
            public double AccessibleArea;
            public double CoveredArea;
            public int CellsWithWater;
            public int CellsWithSpots;
            public int Outside;      // spots that fell in no accessible coarse cell

            /// <summary>The share of the accessible water whose coarse cell holds at least one spot.</summary>
            public double Share => AccessibleArea > 0 ? CoveredArea / AccessibleArea : 0;
        }

        /// <summary>
        /// The spread: cut the map into <paramref name="coarseMeters"/> squares, weigh each by the accessible
        /// water in it, and count the water whose square received a spot. Weighing by water means a square
        /// holding a sliver of sea between the island and the edge counts for its sliver, not for a whole
        /// square.
        /// </summary>
        public static Coverage Spread(Sea sea, bool[] accessible, IEnumerable<Vector2> spots, float coarseMeters)
        {
            var cov = new Coverage
            {
                CoarseMeters = coarseMeters,
                Columns = Mathf.CeilToInt(sea.Map.width / coarseMeters),
                Rows = Mathf.CeilToInt(sea.Map.height / coarseMeters),
            };
            cov.Area = new double[cov.Columns * cov.Rows];
            cov.Spots = new int[cov.Columns * cov.Rows];
            double cellArea = sea.Cell * sea.Cell;
            for (int i = 0; i < accessible.Length; i++)
            {
                if (!accessible[i]) continue;
                int k = CoarseAt(cov, sea.Map, sea.Centre(i));
                if (k >= 0) cov.Area[k] += cellArea;
            }
            foreach (Vector2 s in spots)
            {
                int k = CoarseAt(cov, sea.Map, s);
                if (k >= 0 && cov.Area[k] > 0) cov.Spots[k]++;
                else cov.Outside++;
            }
            for (int k = 0; k < cov.Area.Length; k++)
            {
                if (cov.Area[k] <= 0) continue;
                cov.AccessibleArea += cov.Area[k];
                cov.CellsWithWater++;
                if (cov.Spots[k] == 0) continue;
                cov.CoveredArea += cov.Area[k];
                cov.CellsWithSpots++;
            }
            return cov;
        }

        private static int CoarseAt(Coverage cov, Rect map, Vector2 p)
        {
            int col = Mathf.FloorToInt((p.x - map.xMin) / cov.CoarseMeters);
            int row = Mathf.FloorToInt((p.y - map.yMin) / cov.CoarseMeters);
            return col < 0 || row < 0 || col >= cov.Columns || row >= cov.Rows ? -1 : row * cov.Columns + col;
        }

        // ---- distances (the guard's own) -------------------------------------------------------------

        public static float RectToPoint(Rect r, Vector2 p)
        {
            float dx = Mathf.Max(r.xMin - p.x, 0f, p.x - r.xMax);
            float dy = Mathf.Max(r.yMin - p.y, 0f, p.y - r.yMax);
            return Mathf.Sqrt(dx * dx + dy * dy);
        }

        public static float RectToRect(Rect a, Rect b)
        {
            float dx = Mathf.Max(a.xMin - b.xMax, 0f, b.xMin - a.xMax);
            float dy = Mathf.Max(a.yMin - b.yMax, 0f, b.yMin - a.yMax);
            return Mathf.Sqrt(dx * dx + dy * dy);
        }

        public static float PointToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float len2 = ab.sqrMagnitude;
            float t = len2 > 0f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / len2) : 0f;
            return Vector2.Distance(p, a + ab * t);
        }

        /// <summary>Shortest distance between two segments; zero when they cross.</summary>
        public static float SegmentToSegment(Vector2 p1, Vector2 p2, Vector2 q1, Vector2 q2)
        {
            if (SegmentsCross(p1, p2, q1, q2)) return 0f;
            return Mathf.Min(Mathf.Min(PointToSegment(p1, q1, q2), PointToSegment(p2, q1, q2)),
                             Mathf.Min(PointToSegment(q1, p1, p2), PointToSegment(q2, p1, p2)));
        }

        /// <summary>Shortest distance between a rectangle and a segment; zero when they touch.</summary>
        public static float RectToSegment(Rect r, Vector2 a, Vector2 b)
        {
            if (r.Contains(a) || r.Contains(b)) return 0f;
            var corners = new[]
            {
                new Vector2(r.xMin, r.yMin), new Vector2(r.xMax, r.yMin),
                new Vector2(r.xMax, r.yMax), new Vector2(r.xMin, r.yMax),
            };
            float best = float.MaxValue;
            for (int i = 0; i < 4; i++)
                best = Mathf.Min(best, SegmentToSegment(a, b, corners[i], corners[(i + 1) % 4]));
            return best;
        }

        public static bool SegmentsCross(Vector2 p1, Vector2 p2, Vector2 q1, Vector2 q2)
        {
            float Cross(Vector2 o, Vector2 u, Vector2 v) => (u.x - o.x) * (v.y - o.y) - (u.y - o.y) * (v.x - o.x);
            float d1 = Cross(q1, q2, p1), d2 = Cross(q1, q2, p2);
            float d3 = Cross(p1, p2, q1), d4 = Cross(p1, p2, q2);
            return ((d1 > 0f) != (d2 > 0f)) && ((d3 > 0f) != (d4 > 0f));
        }
    }
}
