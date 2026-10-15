using System;
using System.Collections.Generic;
using System.Linq;
using HiddenHarbours.Boats;
using UnityEngine;

namespace HiddenHarbours.Tools.RigBaking
{
    /// <summary>Splits intersecting edges, removes covered spans and stitches the boundary.</summary>
    public static class HullWalkPolygonUnion
    {
        const float Epsilon = 0.00001f;
        struct Edge { public Vector2 A, B; }

        public static Vector2[][] Union(IEnumerable<Vector2[]> polygons)
        {
            Vector2[][] rings = polygons.ToArray();
            if (rings.Length == 0) throw new FormatException("walk polygons: empty union");
            return Boundary(rings, p => rings.Any(r => DeckAreaMath.Contains(r, p)));
        }

        public static Vector2[][] Union(IEnumerable<HullWalkArea> areas)
        {
            var surfaces = areas.ToArray();
            var rings = surfaces.SelectMany(a => new[] { a.Polygon }.Concat(a.Holes)).ToArray();
            return Boundary(rings, p => surfaces.Any(a => DeckAreaMath.Contains(a.Polygon, p) &&
                !a.Holes.Any(h => DeckAreaMath.Contains(h, p))));
        }

        static Vector2[][] Boundary(Vector2[][] rings, Func<Vector2, bool> contains)
        {
            var edges = new List<Edge>();
            foreach (var ring in rings)
                for (int i = 0; i < ring.Length; i++)
                    edges.Add(new Edge { A = ring[i], B = ring[(i + 1) % ring.Length] });
            var boundary = new List<Edge>();
            foreach (Edge e in edges)
            {
                Vector2 delta = e.B - e.A;
                if (delta.sqrMagnitude < Epsilon * Epsilon) continue;
                var splits = new List<float> { 0, 1 };
                foreach (Edge other in edges)
                {
                    Vector2 d = other.B - other.A, offset = other.A - e.A;
                    float cross = Cross(delta, d);
                    if (Math.Abs(cross) > Epsilon * Epsilon)
                    {
                        float t = Cross(offset, d) / cross, u = Cross(offset, delta) / cross;
                        if (t > 0 && t < 1 && u >= -Epsilon && u <= 1 + Epsilon) splits.Add(t);
                    }
                    else if (Math.Abs(Cross(offset, delta)) < Epsilon * delta.magnitude)
                    {
                        foreach (Vector2 p in new[] { other.A, other.B })
                        {
                            float t = Vector2.Dot(p - e.A, delta) / delta.sqrMagnitude;
                            if (t > 0 && t < 1) splits.Add(t);
                        }
                    }
                }
                splits.Sort();
                for (int i = 1; i < splits.Count; i++)
                {
                    Vector2 a = e.A + delta * splits[i - 1], b = e.A + delta * splits[i];
                    if ((b - a).sqrMagnitude < Epsilon * Epsilon) continue;
                    Vector2 middle = (a + b) * 0.5f;
                    Vector2 normal = new Vector2(-delta.y, delta.x).normalized * Epsilon;
                    bool left = contains(middle + normal);
                    bool right = contains(middle - normal);
                    if (left == right) continue;
                    var part = left ? new Edge { A = a, B = b } : new Edge { A = b, B = a };
                    if (!boundary.Any(v => Near(v.A, part.A) && Near(v.B, part.B))) boundary.Add(part);
                }
            }
            var output = new List<Vector2[]>();
            while (boundary.Count > 0)
            {
                Edge first = boundary[0]; boundary.RemoveAt(0);
                var ring = new List<Vector2> { first.A };
                Vector2 end = first.B;
                while (!Near(end, first.A))
                {
                    ring.Add(end);
                    int next = boundary.FindIndex(e => Near(e.A, end));
                    if (next < 0) throw new FormatException("walk polygons: boundary does not close");
                    end = boundary[next].B; boundary.RemoveAt(next);
                }
                output.Add(ring.ToArray());
            }
            if (output.Count(r => Area(r) > 0) != 1)
                throw new FormatException("walk polygons: disconnected floor requires separate levels");
            return output.OrderByDescending(Area).ToArray();
        }

        static bool Near(Vector2 a, Vector2 b) => (a - b).sqrMagnitude < Epsilon * Epsilon;
        static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;
        static float Area(Vector2[] p)
        {
            float sum = 0;
            for (int i = 0; i < p.Length; i++) sum += Cross(p[i], p[(i + 1) % p.Length]);
            return sum * 0.5f;
        }
    }
}
