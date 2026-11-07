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
        // RGBAHalf offsets stay within +/-8m. Half's maximum rounding error there is
        // sqrt(2)/512m; this outward safety bound keeps quantization inside the band.
        public const float AddressSafetyMetres = 1f / 256f;
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
            public readonly Vector2 Min, Size;
            public Result(int width, int height, Vector2 min, Vector2 size, Color[][] addresses, Segment[][] lines)
            { Width = width; Height = height; Min = min; Size = size; Addresses = addresses; Lines = lines; }

            public Color Sample(int kind, Vector2 point)
            {
                int x = Mathf.Clamp(Mathf.FloorToInt((point.x - Min.x) / Size.x * Width), 0, Width - 1);
                int y = Mathf.Clamp(Mathf.FloorToInt((point.y - Min.y) / Size.y * Height), 0, Height - 1);
                Vector2 center = Min + Vector2.Scale(new Vector2((x + .5f) / Width, (y + .5f) / Height), Size);
                return Decode(Addresses[kind][y * Width + x], center, point);
            }
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
            return new Result(width, height, grid.Min, grid.Size, addresses, lines);
        }

        static List<Line> Contours(Grid grid, float level)
        {
            int w = grid.Width, h = grid.Height;
            var e = grid.Elevations;
            bool[] ocean = OceanConnected(grid, level);
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
            {
                bool OceanEdge(int key)
                {
                    int i = key >> 1, j = (key & 1) == 0 ? i + 1 : i + w;
                    return ocean[e[i] <= level ? i : j];
                }
                if (!OceanEdge(ka) || !OceanEdge(kb)) return;
                int a = Node(ka), b = Node(kb); adj[a].Add(b); adj[b].Add(a);
            }
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

        // An inland hollow is not a shore. Flood the wet vertices from the region boundary,
        // including only the diagonal links the bilinear saddle actually connects.
        static bool[] OceanConnected(Grid grid, float level)
        {
            int w = grid.Width, h = grid.Height;
            float[] e = grid.Elevations;
            var ocean = new bool[e.Length];
            var queue = new Queue<int>();
            void Visit(int x, int y)
            {
                if (x < 0 || y < 0 || x >= w || y >= h) return;
                int i = y * w + x;
                if (ocean[i] || e[i] > level) return;
                ocean[i] = true; queue.Enqueue(i);
            }
            for (int x = 0; x < w; x++) { Visit(x, 0); Visit(x, h - 1); }
            for (int y = 1; y < h - 1; y++) { Visit(0, y); Visit(w - 1, y); }
            while (queue.Count > 0)
            {
                int i = queue.Dequeue(), x = i % w, y = i / w;
                Visit(x - 1, y); Visit(x + 1, y); Visit(x, y - 1); Visit(x, y + 1);
                for (int sy = -1; sy <= 1; sy += 2)
                for (int sx = -1; sx <= 1; sx += 2)
                {
                    int nx = x + sx, ny = y + sy;
                    if (nx < 0 || ny < 0 || nx >= w || ny >= h || e[ny * w + nx] > level) continue;
                    int cell = Mathf.Min(y, ny) * w + Mathf.Min(x, nx);
                    float a = e[cell] - level, b = e[cell + 1] - level,
                        c = e[cell + w + 1] - level, d = e[cell + w] - level;
                    double determinant = (double)a * c - (double)b * d;
                    if ((a <= 0 && c <= 0 && determinant >= 0)
                        || (b <= 0 && d <= 0 && determinant < 0)) Visit(nx, ny);
                }
            }
            return ocean;
        }

        static Segment[] Segments(Grid grid, List<Line> lines)
        {
            var result = new List<Segment>();
            foreach (Line line in lines) AddSegments(grid, line, null, result);
            return result.ToArray();
        }

        static void AddSegments(Grid grid, Line line, bool[] valid, List<Segment> result, Vector2[] seawardHints = null)
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
                    Vector2 hint = seawardHints == null ? -grid.Gradient((a + b) * .5f) : seawardHints[i] + seawardHints[j];
                    if (Vector2.Dot(seaward, hint) < 0) seaward = -seaward;
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
                var hints = new Vector2[source.Points.Count];
                for (int i = 0; i < source.Points.Count; i++)
                {
                    Vector2 q = source.Points[i], inland = grid.Gradient(q).normalized, found = q;
                    hints[i] = -inland;
                    float peak = Vector2.Dot(grid.Gradient(q), inland); bool broke = false;
                    for (float run = def.BrowStepMetres; peak >= def.ScarpMinimumSlope && run <= def.BrowSearchMetres; run += def.BrowStepMetres)
                    {
                        Vector2 p = q + inland * run;
                        float slope = Vector2.Dot(grid.Gradient(p), inland);
                        peak = Mathf.Max(peak, slope);
                        // The FIRST break belongs to the shore profile. If it is too shallow,
                        // stop without a brow; never continue through a flat to an inland hill.
                        if (slope <= peak * def.BrowSlopeFallFraction)
                        { found = p; broke = grid.At(p) >= def.SpringHigh + def.BrowRiseMetres; break; }
                    }
                    brow.Points.Add(found);
                    valid[i] = broke && (turf ? peak >= def.TurfMinimumSlope : peak < def.TurfMinimumSlope);
                }
                // Neighbouring rays can reach different banks or fold across a concavity. Neither
                // a long connector nor a reversed connector is part of the shore's brow curve.
                var local = new List<Segment>();
                AddSegments(grid, brow, valid, local, hints);
                int count = source.Closed ? source.Points.Count : source.Points.Count - 1;
                var rejected = new HashSet<Vector2>();
                for (int i = 0; i < count; i++)
                {
                    int j = (i + 1) % source.Points.Count;
                    Vector2 original = source.Points[j] - source.Points[i], mapped = brow.Points[j] - brow.Points[i];
                    if (mapped.magnitude > original.magnitude + 2 * def.BrowStepMetres || Vector2.Dot(mapped, original) <= 0)
                        rejected.Add(brow.Points[i]);
                }
                foreach (Segment segment in local) if (!rejected.Contains(segment.A)) segments.Add(segment);
            }
            return segments.ToArray();
        }

        public static bool InBand(int kind, float signedDistance)
        {
            float t = signedDistance / StripMetres + Anchors[kind];
            return t >= 0 && t <= 1;
        }

        /// <summary>A complete decoded address also guards the unsigned distance to a shore point.</summary>
        public static bool InBand(int kind, Color address) => InBand(kind, address.r) && address.a >= 0
            && address.a <= StripMetres * (address.r < 0 ? Anchors[kind] : 1 - Anchors[kind]) - AddressSafetyMetres;

        public static Color Decode(Color packed, Vector2 cellCenter, Vector2 point)
        {
            if (Mathf.Abs(packed.a) < .5f) return new Color(8, 0, 1, 8);
            Vector2 nearest = cellCenter + new Vector2(packed.r, packed.g), delta = point - nearest;
            Vector2 normal = new Vector2(Mathf.Cos(packed.b), Mathf.Sin(packed.b));
            float distance = delta.magnitude, signed = distance * (Vector2.Dot(delta, normal) < 0 ? -1 : 1);
            Vector2 tangent = new Vector2(-normal.y, normal.x) * Mathf.Sign(packed.a);
            float phase = (Mathf.Abs(packed.a) - 1 + Vector2.Dot(delta, tangent) / RepeatMetres) * (2 * Mathf.PI);
            return new Color(signed, Mathf.Sin(phase), Mathf.Cos(phase), distance);
        }

        static Color[] Raster(Grid grid, Segment[] segments, int width, int height)
        {
            Vector2 texel = new Vector2(grid.Size.x / width, grid.Size.y / height);
            float padding = StripMetres + texel.magnitude;
            var distance = new float[width * height];
            var result = new Color[distance.Length];
            for (int i = 0; i < result.Length; i++)
            { distance[i] = padding * padding; result[i] = Color.clear; }
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
                    float arc = (segment.Arc + t * len) * segment.RepeatScale / RepeatMetres;
                    float direction = Vector2.Dot(delta, new Vector2(-segment.Seaward.y, segment.Seaward.x)) < 0 ? -1 : 1;
                    // Point-sampled offsets retain an actual point on the curve. Interpolating
                    // nearest points or signed distances would invent curves between branches.
                    result[index] = new Color(-across.x, -across.y, Mathf.Atan2(segment.Seaward.y, segment.Seaward.x),
                        direction * (1 + arc - Mathf.Floor(arc)));
                }
            }
            return result;
        }
    }
}
