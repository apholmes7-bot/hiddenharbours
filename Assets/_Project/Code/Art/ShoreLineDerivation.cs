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
            public readonly Segment[] Segments;
            public readonly Color[] SegmentTexels;
            public readonly int[] KindStarts;
            public readonly int[] SegmentIds;
            public readonly float[] IndexTexels;
            readonly Grid _grid;
            public Result(int width, int height, Grid grid, Color[][] addresses, Segment[][] lines, Segment[] segments, int[] segmentIds)
            {
                Width = width; Height = height; Min = grid.Min; Size = grid.Size;
                Addresses = addresses; Lines = lines; Segments = segments; _grid = grid;
                SegmentIds = segmentIds;
                IndexTexels = new float[Mathf.Max(1, Mathf.CeilToInt(segmentIds.Length / 1024f)) * 1024];
                for (int i = 0; i < segmentIds.Length; i++) IndexTexels[i] = segmentIds[i];
                KindStarts = new int[KindCount + 1]; KindStarts[0] = 1;
                for (int kind = 0; kind < KindCount; kind++) KindStarts[kind + 1] = KindStarts[kind] + lines[kind].Length;
                SegmentTexels = new Color[segments.Length * 2];
                for (int i = 1; i < segments.Length; i++)
                {
                    Segment s = segments[i];
                    SegmentTexels[i] = new Color(s.A.x, s.A.y, s.B.x, s.B.y);
                    SegmentTexels[segments.Length + i] = new Color(s.Seaward.x, s.Seaward.y, s.Arc, s.RepeatScale);
                }
            }

            public Color Sample(int kind, Vector2 point)
            {
                Color best = new Color(8, 0, 0, 8);
                Substrate cover = _grid.CoverAt(point);
                if (_grid.ProtectedAt(point) || (kind < 2 && !cover.QualifyingBrow)) return best;
                int x = Mathf.FloorToInt((point.x - Min.x) / Size.x * Width);
                int y = Mathf.FloorToInt((point.y - Min.y) / Size.y * Height);
                Color packed = Addresses[kind][Mathf.Clamp(y, 0, Height - 1) * Width + Mathf.Clamp(x, 0, Width - 1)];
                int start = Mathf.RoundToInt(packed.r) + 1024 * Mathf.RoundToInt(packed.g) + 1048576 * Mathf.RoundToInt(packed.b);
                for (int i = 0; i < Mathf.RoundToInt(packed.a); i++)
                {
                    Color a = Address(Segments[SegmentIds[start + i]], point);
                    if (a.a < best.a) best = a;
                }
                return best;
            }
        }

        public readonly struct Substrate
        {
            public readonly float Turf, Sand, Shingle, Talus, Ledge, Shelf;
            public Substrate(float turf, float sand = 0, float shingle = 0, float talus = 0, float ledge = 0, float shelf = 0)
            { Turf = turf; Sand = sand; Shingle = shingle; Talus = talus; Ledge = ledge; Shelf = shelf; }
            public bool ProtectedFoot => Talus > .5f || Ledge > .5f || Shelf > .5f;
            public bool QualifyingBrow => Turf >= .5f && Sand < .01f && Shingle < .01f && !ProtectedFoot;
        }

        /// <summary>The actual wall faces in world XY, expanded by the supplied guard at load.</summary>
        public sealed class WallFootprint
        {
            public readonly int Width, Height;
            public readonly Vector2 Min, Size;
            public readonly byte[] Pixels;
            readonly float _margin;
            public WallFootprint(Vector2 min, Vector2 size, float texel, float margin)
            {
                Min = min; Size = size; _margin = margin;
                Width = Mathf.CeilToInt(size.x / texel); Height = Mathf.CeilToInt(size.y / texel);
                Pixels = new byte[Width * Height];
            }
            public bool At(Vector2 p)
            {
                int x = Mathf.FloorToInt((p.x - Min.x) / Size.x * Width);
                int y = Mathf.FloorToInt((p.y - Min.y) / Size.y * Height);
                return x >= 0 && x < Width && y >= 0 && y < Height && Pixels[y * Width + x] != 0;
            }
            static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;
            static float DistanceSquared(Vector2 p, Vector2 a, Vector2 b)
            {
                Vector2 ab = b - a;
                return (p - a - ab * Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-12f))).sqrMagnitude;
            }
            public void AddTriangle(Vector2 a, Vector2 b, Vector2 c)
            {
                // Protect the drawn face triangles plus the supplied guard,
                // never a renderer's rectangular bounds or the whole painted foreshore.
                float rx = Size.x / Width, ry = Size.y / Height;
                int x0 = Mathf.Clamp(Mathf.FloorToInt((Mathf.Min(a.x, Mathf.Min(b.x, c.x)) - _margin - Min.x) / rx), 0, Width - 1);
                int x1 = Mathf.Clamp(Mathf.CeilToInt((Mathf.Max(a.x, Mathf.Max(b.x, c.x)) + _margin - Min.x) / rx), 0, Width - 1);
                int y0 = Mathf.Clamp(Mathf.FloorToInt((Mathf.Min(a.y, Mathf.Min(b.y, c.y)) - _margin - Min.y) / ry), 0, Height - 1);
                int y1 = Mathf.Clamp(Mathf.CeilToInt((Mathf.Max(a.y, Mathf.Max(b.y, c.y)) + _margin - Min.y) / ry), 0, Height - 1);
                float margin2 = _margin * _margin;
                for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    int i = y * Width + x;
                    if (Pixels[i] != 0) continue;
                    Vector2 p = Min + new Vector2((x + .5f) * rx, (y + .5f) * ry);
                    float u = Cross(b - a, p - a), v = Cross(c - b, p - b), w = Cross(a - c, p - c);
                    bool inside = Mathf.Abs(Cross(b - a, c - a)) > 1e-10f
                        && ((u >= 0 && v >= 0 && w >= 0) || (u <= 0 && v <= 0 && w <= 0));
                    if (inside || DistanceSquared(p, a, b) <= margin2 || DistanceSquared(p, b, c) <= margin2
                        || DistanceSquared(p, c, a) <= margin2) Pixels[i] = 255;
                }
            }
        }

        /// <summary>The brow decal's upper part is plan ground. Clip only its face portion,
        /// interpolating on the already drawn triangles; no wall vertex or material changes.</summary>
        public static Vector3[] FaceVertices(Vector3[] vertices, Vector2[] uv, int[] triangles, float browLine)
        {
            var result = new List<Vector3>();
            var polygon = new List<(Vector3 p, float v)>(4);
            for (int i = 0; i < triangles.Length; i += 3)
            {
                polygon.Clear();
                int previous = triangles[i + 2];
                for (int corner = 0; corner < 3; corner++)
                {
                    int current = triangles[i + corner];
                    float a = uv[previous].y, b = uv[current].y;
                    bool insideA = a >= browLine, insideB = b >= browLine;
                    if (insideA != insideB) polygon.Add((Vector3.LerpUnclamped(vertices[previous], vertices[current],
                        (browLine - a) / (b - a)), browLine));
                    if (insideB) polygon.Add((vertices[current], b));
                    previous = current;
                }
                for (int j = 1; j + 1 < polygon.Count; j++)
                { result.Add(polygon[0].p); result.Add(polygon[j].p); result.Add(polygon[j + 1].p); }
            }
            return result.ToArray();
        }

        /// <summary>A load-only snapshot of the six existing linear splats. No material slot is added.</summary>
        public sealed class SplatReader
        {
            readonly Color32[][] _pixels = new Color32[6][];
            readonly Vector2Int[] _sizes = new Vector2Int[6];
            public SplatReader(params Texture2D[] textures)
            {
                for (int i = 0; i < 6; i++)
                {
                    Texture2D texture = i < textures.Length ? textures[i] : null;
                    if (texture == null) continue;
                    if (!texture.isReadable) throw new ArgumentException("Shore splat must be readable.");
                    _pixels[i] = texture.GetPixels32(); _sizes[i] = new Vector2Int(texture.width, texture.height);
                }
            }
            Color Sample(int map, Vector2 uv)
            {
                if (_pixels[map] == null) return Color.clear;
                int w = _sizes[map].x, h = _sizes[map].y;
                float fx = uv.x * w - .5f, fy = uv.y * h - .5f;
                int x = Mathf.FloorToInt(fx), y = Mathf.FloorToInt(fy);
                Color Read(int ix, int iy) => _pixels[map][Mathf.Clamp(iy, 0, h - 1) * w + Mathf.Clamp(ix, 0, w - 1)];
                return Color.Lerp(Color.Lerp(Read(x, y), Read(x + 1, y), fx - x),
                    Color.Lerp(Read(x, y + 1), Read(x + 1, y + 1), fx - x), fy - y);
            }
            public Substrate At(Vector2 uv)
            {
                Color a = Sample(0, uv), b = Sample(1, uv), c = Sample(2, uv), d = Sample(3, uv),
                    e = Sample(4, uv), f = Sample(5, uv);
                float sum = a.r+a.g+a.b+a.a+b.r+b.g+b.b+b.a+c.r+c.g+c.b+c.a+d.r+d.g+d.b+d.a+e.r+e.g+e.b+e.a+f.r;
                float norm = 1 / Mathf.Max(1, sum);
                return new Substrate((a.r+b.a+e.b+e.a)*norm, a.b*norm, a.a*norm, c.a*norm, d.r*norm, b.g*norm);
            }
        }

        /// <summary>The shader's texel-centre bilinear mapping, in metres above datum.</summary>
        public sealed class Grid
        {
            public readonly float[] Elevations;
            public readonly int Width, Height;
            public readonly Vector2 Min, Size, Texel;
            readonly Func<Vector2, Substrate> _substrate;
            readonly WallFootprint _walls;
            Segment[] _profiles;
            bool[] _profileWalls;
            List<int>[] _profileBuckets;
            int _profileWidth, _profileHeight;
            float _profileCell;
            public bool HasWallFootprint => _walls != null;
            public Grid(float[] elevations, int width, int height, Vector2 min, Vector2 size,
                Func<Vector2, Substrate> substrate = null, WallFootprint walls = null)
            {
                if (width < 2 || height < 2 || elevations == null || elevations.Length != width * height
                    || size.x <= 0 || size.y <= 0) throw new ArgumentException("Invalid shore height grid.");
                foreach (float e in elevations)
                    if (float.IsNaN(e) || float.IsInfinity(e)) throw new ArgumentException("Nonfinite shore height.");
                Elevations = elevations; Width = width; Height = height; Min = min; Size = size;
                Texel = new Vector2(size.x / width, size.y / height);
                _substrate = substrate; _walls = walls;
            }
            // Geometry-only fixtures explicitly model a turf cap. Region bindings supply their splat reader.
            public Substrate CoverAt(Vector2 q) => _substrate == null ? new Substrate(1) : _substrate(q);
            // seat ruling s12, 11-07: region bindings supply real wall geometry; legacy geometry-only
            // fixtures keep their explicit painted-foot premise when no wall geometry is supplied.
            public bool ProtectedAt(Vector2 q) => _walls == null ? CoverAt(q).ProtectedFoot : _walls.At(q);
            public bool CliffCoast(Vector2 q, TerrainShoreLinesDef def)
            {
                if (_profileBuckets == null) return false;
                int x = Mathf.Clamp(Mathf.FloorToInt((q.x - Min.x) / _profileCell), 0, _profileWidth - 1);
                int y = Mathf.Clamp(Mathf.FloorToInt((q.y - Min.y) / _profileCell), 0, _profileHeight - 1);
                var bucket = _profileBuckets[y * _profileWidth + x];
                if (bucket == null) return false;
                float best = def.BrowSearchMetres * def.BrowSearchMetres; bool owned = false;
                foreach (int i in bucket)
                {
                    var s = _profiles[i]; Vector2 ab = s.B - s.A;
                    float t = Mathf.Clamp01(Vector2.Dot(q - s.A, ab) / ab.sqrMagnitude);
                    float d = (q - s.A - ab * t).sqrMagnitude;
                    if (d < best) { best = d; owned = _profileWalls[i]; }
                }
                return owned;
            }
            internal void PrepareCoast(Segment[] high, TerrainShoreLinesDef def)
            {
                if (_walls == null) return;
                // seat ruling s17: ownership follows a qualified high-water profile whose brow
                // actually meets a wall, never a remote hit from a noisy foreshore gradient.
                _profiles = high; _profileWalls = new bool[high.Length];
                _profileCell = def.BrowSearchMetres;
                _profileWidth = Mathf.Max(1, Mathf.CeilToInt(Size.x / _profileCell));
                _profileHeight = Mathf.Max(1, Mathf.CeilToInt(Size.y / _profileCell));
                _profileBuckets = new List<int>[_profileWidth * _profileHeight];
                for (int i = 0; i < high.Length; i++)
                {
                    var s = high[i];
                    if (FindBrow(this, (s.A + s.B) * .5f, def, out var found, out var inland, out _)
                        && CoverAt(found + inland * def.BrowStepMetres).QualifyingBrow)
                    {
                        for (float run = -def.BrowStepMetres; run <= def.BrowStepMetres; run += def.BrowStepMetres)
                            _profileWalls[i] |= _walls.At(found + inland * run);
                    }
                    float pad = def.BrowSearchMetres;
                    int x0 = Mathf.Clamp(Mathf.FloorToInt((Mathf.Min(s.A.x, s.B.x) - pad - Min.x) / _profileCell), 0, _profileWidth - 1);
                    int x1 = Mathf.Clamp(Mathf.FloorToInt((Mathf.Max(s.A.x, s.B.x) + pad - Min.x) / _profileCell), 0, _profileWidth - 1);
                    int y0 = Mathf.Clamp(Mathf.FloorToInt((Mathf.Min(s.A.y, s.B.y) - pad - Min.y) / _profileCell), 0, _profileHeight - 1);
                    int y1 = Mathf.Clamp(Mathf.FloorToInt((Mathf.Max(s.A.y, s.B.y) + pad - Min.y) / _profileCell), 0, _profileHeight - 1);
                    for (int y = y0; y <= y1; y++) for (int x = x0; x <= x1; x++)
                        (_profileBuckets[y * _profileWidth + x] ??= new List<int>()).Add(i);
                }
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
            var highSegments = Segments(grid, high);
            grid.PrepareCoast(highSegments, def);
            var lines = new Segment[KindCount][];
            lines[0] = Brows(grid, high, def, true);
            lines[1] = Brows(grid, high, def, false);
            lines[2] = highSegments;
            var weed = new List<Segment>();
            // seat ruling s17: the spring-low contour can be tens of metres offshore; the
            // exposed cliff foot follows the region's existing mean-low contour instead.
            // Soft shores retain their existing mean-high line, keeping fixture and beach coverage.
            foreach (var s in Segments(grid, Contours(grid, def.MeanHigh)))
                if (!grid.CliffCoast((s.A + s.B) * .5f, def)) weed.Add(s);
            foreach (var s in Segments(grid, Contours(grid, def.MeanLow)))
                if (grid.CliffCoast((s.A + s.B) * .5f, def)) weed.Add(s);
            lines[3] = weed.ToArray();
            lines[4] = Segments(grid, Contours(grid, def.MeanLow));
            var addresses = new Color[KindCount][];
            var all = new List<Segment> { default };
            var ids = new List<int>();
            var foot = new bool[width * height];
            for (int y = 0; y < height; y++)
            // seat ruling s17: a partially protected address cell still needs candidates.
            // The exact CPU sample / GPU fragment mask, not its cell centre, rejects faces.
            for (int x = 0; x < width; x++) foot[y * width + x] = !grid.HasWallFootprint && grid.ProtectedAt(grid.Min +
                Vector2.Scale(new Vector2((x + .5f) / width, (y + .5f) / height), grid.Size));
            for (int kind = 0; kind < KindCount; kind++)
            {
                addresses[kind] = Raster(grid, lines[kind], width, height, all.Count, foot, ids);
                all.AddRange(lines[kind]);
            }
            return new Result(width, height, grid, addresses, lines, all.ToArray(), ids.ToArray());
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

        static bool FindBrow(Grid grid, Vector2 q, TerrainShoreLinesDef def, out Vector2 found,
            out Vector2 inland, out float peak)
        {
            inland = grid.Gradient(q).normalized; found = q;
            peak = Vector2.Dot(grid.Gradient(q), inland);
            for (float run = def.BrowStepMetres; peak >= def.ScarpMinimumSlope && run <= def.BrowSearchMetres; run += def.BrowStepMetres)
            {
                Vector2 p = q + inland * run;
                float slope = Vector2.Dot(grid.Gradient(p), inland); peak = Mathf.Max(peak, slope);
                if (slope <= peak * def.BrowSlopeFallFraction)
                { found = p; return grid.At(p) >= def.SpringHigh + def.BrowRiseMetres; }
            }
            return false;
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
                    Vector2 q = source.Points[i];
                    bool broke = FindBrow(grid, q, def, out var found, out var inland, out var peak);
                    hints[i] = -inland;
                    brow.Points.Add(found);
                    // seat ruling s17: only a wall-owned brow uses the S12 landward turf.
                    // Soft banks keep S11's break-point qualifier and steep/gentle exclusion.
                    bool cliff = grid.CliffCoast(q, def);
                    valid[i] = broke && peak >= def.ScarpMinimumSlope
                        && grid.CoverAt(cliff ? found + inland * def.BrowStepMetres : found).QualifyingBrow
                        && (turf || peak < def.TurfMinimumSlope);
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

        public static Color Address(Segment segment, Vector2 point)
        {
            Vector2 ab = segment.B - segment.A;
            float t = Mathf.Clamp01(Vector2.Dot(point - segment.A, ab) / ab.sqrMagnitude);
            Vector2 delta = point - (segment.A + t * ab);
            float distance = delta.magnitude, signed = distance * (Vector2.Dot(delta, segment.Seaward) < 0 ? -1 : 1);
            return new Color(signed, segment.Arc + t * ab.magnitude, segment.RepeatScale, distance);
        }

        static Color[] Raster(Grid grid, Segment[] segments, int width, int height, int firstId, bool[] foot, List<int> ids)
        {
            Vector2 texel = new Vector2(grid.Size.x / width, grid.Size.y / height);
            float padding = StripMetres + texel.magnitude;
            var distance = new float[width * height];
            var result = new Color[distance.Length];
            for (int i = 0; i < result.Length; i++)
            { distance[i] = padding * padding; result[i] = Color.clear; }
            for (int si = 0; si < segments.Length; si++)
            {
                Segment segment = segments[si];
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
                    if (foot[y * width + x]) continue;
                    float t = Mathf.Clamp01(Vector2.Dot(q - segment.A, delta) / len2);
                    Vector2 across = q - (segment.A + delta * t);
                    float d2 = across.sqrMagnitude; int index = y * width + x;
                    if (d2 >= distance[index]) continue;
                    distance[index] = d2;
                    // Integers split at ten bits remain exact in RGBAHalf. Curve geometry is
                    // loaded separately as floats, then projected at the fragment, not quantised.
                    int id = firstId + si;
                    result[index] = new Color(id % 1024, id / 1024, 0, 1);
                }
            }
            // Distance is 1-Lipschitz. Any segment that wins anywhere within a cell must be
            // within d(center) + twice the cell radius at its centre. Retain the complete set.
            var buckets = new List<int>[result.Length];
            for (int si = 0; si < segments.Length; si++)
            {
                Segment s = segments[si]; Vector2 ab = s.B - s.A;
                int x0 = Mathf.Clamp(Mathf.FloorToInt((Mathf.Min(s.A.x,s.B.x)-padding-grid.Min.x)/texel.x),0,width-1);
                int x1 = Mathf.Clamp(Mathf.CeilToInt((Mathf.Max(s.A.x,s.B.x)+padding-grid.Min.x)/texel.x),0,width-1);
                int y0 = Mathf.Clamp(Mathf.FloorToInt((Mathf.Min(s.A.y,s.B.y)-padding-grid.Min.y)/texel.y),0,height-1);
                int y1 = Mathf.Clamp(Mathf.CeilToInt((Mathf.Max(s.A.y,s.B.y)+padding-grid.Min.y)/texel.y),0,height-1);
                for (int y=y0;y<=y1;y++) for(int x=x0;x<=x1;x++)
                {
                    int i=y*width+x;if(foot[i])continue;
                    Vector2 p=grid.Min+Vector2.Scale(new Vector2(x+.5f,y+.5f),texel);
                    float t=Mathf.Clamp01(Vector2.Dot(p-s.A,ab)/ab.sqrMagnitude);
                    if(Vector2.Distance(p,s.A+ab*t)>Mathf.Sqrt(distance[i])+texel.magnitude)continue;
                    (buckets[i]??=new List<int>()).Add(firstId+si);
                }
            }
            for(int i=0;i<result.Length;i++)
            {
                var bucket=buckets[i];if(bucket==null){result[i]=Color.clear;continue;}
                if(bucket.Count>2048)throw new ArgumentException("Shore candidate cell exceeds exact half-float count.");
                int start=ids.Count;ids.AddRange(bucket);
                result[i]=new Color(start%1024,(start/1024)%1024,start/1048576,bucket.Count);
            }
            return result;
        }
    }
}
