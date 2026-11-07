using System;
using System.Collections.Generic;
using UnityEngine;

namespace HiddenHarbours.Art
{
    /// <summary>Load-only shading data from painted elevations. No scene, clock, randomness or save state.
    /// Contours use the bilinear saddle determinant, stable edge keys and ordered arc length. A closed
    /// loop fits a whole number of kit repeats so its seam has the same phase on both sides.</summary>
    public static class ShoreLineDerivation
    {
        public const int KindCount = 5;
        public const float StripMetres = 4f;
        public const float RepeatMetres = 8f;
        public static readonly float[] Anchors = { .40f, .24f, .46f, .34f, .38f };

        public readonly struct Segment
        {
            public readonly Vector2 A, B, Seaward;
            public readonly float Arc, RepeatScale;
            public Segment(Vector2 a, Vector2 b, Vector2 seaward, float arc, float repeatScale)
            { A = a; B = b; Seaward = seaward; Arc = arc; RepeatScale = repeatScale; }
        }

        public sealed class Result
        {
            public readonly int Width, Height;
            public readonly Color[][] Addresses;
            public readonly Segment[][] Lines;
            public Result(int width, int height, Color[][] addresses, Segment[][] lines)
            { Width = width; Height = height; Addresses = addresses; Lines = lines; }
        }

        /// <summary>The shader's texel-centre bilinear mapping, in metres above datum.</summary>
        public sealed class Grid
        {
            public readonly float[] Elevations;
            public readonly int Width, Height;
            public readonly Vector2 Min, Size, Texel;
            public Grid(float[] elevations, int width, int height, Vector2 min, Vector2 size)
            {
                if (width < 2 || height < 2 || elevations == null || elevations.Length != width * height
                    || size.x <= 0 || size.y <= 0) throw new ArgumentException("Invalid shore height grid.");
                foreach (float e in elevations)
                    if (float.IsNaN(e) || float.IsInfinity(e)) throw new ArgumentException("Nonfinite shore height.");
                Elevations = elevations; Width = width; Height = height; Min = min; Size = size;
                Texel = new Vector2(size.x / width, size.y / height);
            }
            public float At(Vector2 q)
            {
                float fx = (q.x - Min.x) / Texel.x - .5f, fy = (q.y - Min.y) / Texel.y - .5f;
                int x = Mathf.FloorToInt(fx), y = Mathf.FloorToInt(fy);
                float E(int ix, int iy) => Elevations[Mathf.Clamp(iy, 0, Height - 1) * Width + Mathf.Clamp(ix, 0, Width - 1)];
                return Mathf.Lerp(Mathf.Lerp(E(x, y), E(x + 1, y), fx - x),
                    Mathf.Lerp(E(x, y + 1), E(x + 1, y + 1), fx - x), fy - y);
            }
            public Vector2 Gradient(Vector2 q) => new Vector2(
                (At(q + new Vector2(Texel.x, 0)) - At(q - new Vector2(Texel.x, 0))) / (2 * Texel.x),
                (At(q + new Vector2(0, Texel.y)) - At(q - new Vector2(0, Texel.y))) / (2 * Texel.y));
        }

        sealed class Line
        {
            public readonly List<Vector2> Points = new List<Vector2>();
            public bool Closed;
        }

        public static Result Build(Grid grid, TerrainShoreLinesDef def)
        {
            if (def == null || def.AddressTexelMetres <= 0 || def.BrowStepMetres <= 0
                || def.BrowSearchMetres < def.BrowStepMetres || def.TurfMinimumSlope < def.ScarpMinimumSlope)
                throw new ArgumentException("Invalid shore line definition.");
            int width = Mathf.Max(1, Mathf.CeilToInt(grid.Size.x / def.AddressTexelMetres));
            int height = Mathf.Max(1, Mathf.CeilToInt(grid.Size.y / def.AddressTexelMetres));
            List<Line> high = Contours(grid, def.SpringHigh);
            var lines = new Segment[KindCount][];
            lines[0] = Brows(grid, high, def, true);
            lines[1] = Brows(grid, high, def, false);
            lines[2] = Segments(grid, high);
            lines[3] = Segments(grid, Contours(grid, def.MeanHigh));
            lines[4] = Segments(grid, Contours(grid, def.MeanLow));
            var addresses = new Color[KindCount][];
            for (int kind = 0; kind < KindCount; kind++)
                addresses[kind] = Raster(grid, lines[kind], width, height);
            return new Result(width, height, addresses, lines);
        }

        static List<Line> Contours(Grid grid, float level)
        {
            int w = grid.Width, h = grid.Height;
            var e = grid.Elevations;
            var ids = new Dictionary<int, int>();
            var keys = new List<int>();
            var points = new List<Vector2>();
            var adj = new List<List<int>>();
            int Node(int key)
            {
                if (ids.TryGetValue(key, out int id)) return id;
                int i = key >> 1, x = i % w, y = i / w, j = (key & 1) == 0 ? i + 1 : i + w;
                float t = (level - e[i]) / (e[j] - e[i]);
                id = points.Count; ids.Add(key, id); keys.Add(key);
                points.Add(grid.Min + Vector2.Scale(new Vector2(x + .5f + ((key & 1) == 0 ? t : 0),
                    y + .5f + ((key & 1) == 1 ? t : 0)), grid.Texel));
                adj.Add(new List<int>(2)); return id;
            }
            void Link(int ka, int kb)
            { int a = Node(ka), b = Node(kb); adj[a].Add(b); adj[b].Add(a); }
            var cross = new int[4];
            for (int y = 0; y < h - 1; y++)
            for (int x = 0; x < w - 1; x++)
            {
                int i = y * w + x;
                float a = e[i] - level, b = e[i + 1] - level, c = e[i + w + 1] - level, d = e[i + w] - level;
                int mask = (a > 0 ? 1 : 0) | (b > 0 ? 2 : 0) | (c > 0 ? 4 : 0) | (d > 0 ? 8 : 0);
                if (mask == 0 || mask == 15) continue;
                int bottom = i * 2, right = (i + 1) * 2 + 1, top = (i + w) * 2, left = i * 2 + 1, n = 0;
                if ((a > 0) != (b > 0)) cross[n++] = bottom;
                if ((b > 0) != (c > 0)) cross[n++] = right;
                if ((c > 0) != (d > 0)) cross[n++] = top;
                if ((d > 0) != (a > 0)) cross[n++] = left;
                if (n == 2) Link(cross[0], cross[1]);
                else if ((double)a * c - (double)b * d >= 0) { Link(bottom, right); Link(top, left); }
                else { Link(left, bottom); Link(right, top); }
            }
            var order = new List<int>(points.Count);
            for (int i = 0; i < points.Count; i++) order.Add(i);
            // Open contours start at an endpoint. This also makes a cropped fixture deterministic.
            order.Sort((a, b) => adj[a].Count == adj[b].Count ? keys[a].CompareTo(keys[b]) : adj[a].Count.CompareTo(adj[b].Count));
            var seen = new bool[points.Count];
            var result = new List<Line>();
            foreach (int start in order)
            {
                if (seen[start]) continue;
                var line = new Line(); int current = start, previous = -1;
                while (!seen[current])
                {
                    seen[current] = true; line.Points.Add(points[current]);
                    int next = -1;
                    foreach (int candidate in adj[current])
                        if (candidate != previous && (next < 0 || keys[candidate] < keys[next])) next = candidate;
                    if (next < 0) break;
                    previous = current; current = next;
                }
                line.Closed = current == start && line.Points.Count > 2 && adj[start].Count == 2;
                if (line.Points.Count > 1) result.Add(line);
            }
            return result;
        }

        static Segment[] Segments(Grid grid, List<Line> lines)
        {
            var result = new List<Segment>();
            foreach (Line line in lines) AddSegments(grid, line, null, result);
            return result.ToArray();
        }

        static void AddSegments(Grid grid, Line line, bool[] valid, List<Segment> result)
        {
            int count = line.Closed ? line.Points.Count : line.Points.Count - 1;
            float length = 0;
            for (int i = 0; i < count; i++) length += Vector2.Distance(line.Points[i], line.Points[(i + 1) % line.Points.Count]);
            float scale = line.Closed && length > 0 ? Mathf.Max(1, Mathf.Round(length / RepeatMetres)) * RepeatMetres / length : 1;
            float arc = 0;
            for (int i = 0; i < count; i++)
            {
                int j = (i + 1) % line.Points.Count;
                Vector2 a = line.Points[i], b = line.Points[j], tangent = b - a;
                float len = tangent.magnitude;
                if (len > 1e-6f && (valid == null || (valid[i] && valid[j])))
                {
                    Vector2 seaward = new Vector2(tangent.y, -tangent.x) / len;
                    if (Vector2.Dot(seaward, grid.Gradient((a + b) * .5f)) > 0) seaward = -seaward;
                    result.Add(new Segment(a, b, seaward, arc, scale));
                }
                arc += len;
            }
        }

        static Segment[] Brows(Grid grid, List<Line> high, TerrainShoreLinesDef def, bool turf)
        {
            var segments = new List<Segment>();
            foreach (Line source in high)
            {
                var brow = new Line { Closed = source.Closed };
                var valid = new bool[source.Points.Count];
                for (int i = 0; i < source.Points.Count; i++)
                {
                    Vector2 q = source.Points[i], inland = grid.Gradient(q).normalized, found = q;
                    float peak = 0; bool broke = false;
                    for (float run = 0; run <= def.BrowSearchMetres; run += def.BrowStepMetres)
                    {
                        Vector2 p = q + inland * run;
                        float slope = Vector2.Dot(grid.Gradient(p), inland);
                        peak = Mathf.Max(peak, slope);
                        if (peak >= def.ScarpMinimumSlope && slope <= peak * def.BrowSlopeFallFraction
                            && grid.At(p) >= def.SpringHigh + def.BrowRiseMetres)
                        { found = p; broke = true; break; }
                    }
                    brow.Points.Add(found);
                    valid[i] = broke && (turf ? peak >= def.TurfMinimumSlope : peak < def.TurfMinimumSlope);
                }
                AddSegments(grid, brow, valid, segments);
            }
            return segments.ToArray();
        }

        public static bool InBand(int kind, float signedDistance)
        {
            float t = signedDistance / StripMetres + Anchors[kind];
            return t >= 0 && t <= 1;
        }

        static Color[] Raster(Grid grid, Segment[] segments, int width, int height)
        {
            Vector2 texel = new Vector2(grid.Size.x / width, grid.Size.y / height);
            float padding = StripMetres + texel.magnitude;
            var distance = new float[width * height];
            var result = new Color[distance.Length];
            for (int i = 0; i < result.Length; i++)
            { distance[i] = padding * padding; result[i] = new Color(padding * 2, 0, 1, 0); }
            foreach (Segment segment in segments)
            {
                Vector2 delta = segment.B - segment.A;
                float len2 = delta.sqrMagnitude, len = Mathf.Sqrt(len2);
                int x0 = Mathf.Clamp(Mathf.FloorToInt((Mathf.Min(segment.A.x, segment.B.x) - padding - grid.Min.x) / texel.x), 0, width - 1);
                int x1 = Mathf.Clamp(Mathf.CeilToInt((Mathf.Max(segment.A.x, segment.B.x) + padding - grid.Min.x) / texel.x), 0, width - 1);
                int y0 = Mathf.Clamp(Mathf.FloorToInt((Mathf.Min(segment.A.y, segment.B.y) - padding - grid.Min.y) / texel.y), 0, height - 1);
                int y1 = Mathf.Clamp(Mathf.CeilToInt((Mathf.Max(segment.A.y, segment.B.y) + padding - grid.Min.y) / texel.y), 0, height - 1);
                for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    Vector2 q = grid.Min + Vector2.Scale(new Vector2(x + .5f, y + .5f), texel);
                    float t = Mathf.Clamp01(Vector2.Dot(q - segment.A, delta) / len2);
                    Vector2 across = q - (segment.A + delta * t);
                    float d2 = across.sqrMagnitude; int index = y * width + x;
                    if (d2 >= distance[index]) continue;
                    distance[index] = d2;
                    float d = Mathf.Sqrt(d2) * (Vector2.Dot(across, segment.Seaward) < 0 ? -1 : 1);
                    float phase = (segment.Arc + t * len) * segment.RepeatScale * (Mathf.PI * 2 / RepeatMetres);
                    result[index] = new Color(d, Mathf.Sin(phase), Mathf.Cos(phase), 1);
                }
            }
            return result;
        }
    }
}
