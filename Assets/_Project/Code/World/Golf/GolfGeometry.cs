using System;
using UnityEngine;

namespace HiddenHarbours.World.Golf
{
    internal static class GolfCheck
    {
        internal static void Finite(double n, string name)
        { if (double.IsNaN(n) || double.IsInfinity(n)) throw new ArgumentException("Nonfinite " + name); }
        internal static void Point(Vector2 p) { Finite(p.x, "x"); Finite(p.y, "y"); }
        internal static void Positive(float n, string name)
        { Finite(n, name); if (n <= 0) throw new ArgumentException("Nonpositive " + name); }
        internal static void Nonnegative(float n, string name)
        { Finite(n, name); if (n < 0) throw new ArgumentException("Negative " + name); }
        internal static void Unit(float n, string name)
        { Finite(n, name); if (n < 0 || n > 1) throw new ArgumentException("Outside unit range: " + name); }
        internal static void Id(string id, string prefix)
        {
            if (string.IsNullOrEmpty(id) || !id.StartsWith(prefix, StringComparison.Ordinal) || id.Length == prefix.Length)
                throw new ArgumentException("Invalid stable id: " + prefix);
            foreach (char c in id) if (!(c >= 'a' && c <= 'z') && !(c >= '0' && c <= '9') && c != '.' && c != '_')
                throw new ArgumentException("Invalid stable id character.");
        }
    }

    /// <summary>Simple polygons, either winding, edges included. Numeric tolerances are geometry precision, not balance.</summary>
    internal sealed class GolfPolygon
    {
        const double Epsilon = 1e-7;
        readonly Vector2[] _points;
        internal readonly GolfSurface Surface;
        internal GolfPolygon(Vector2[] points, GolfSurface surface = default)
        {
            if (points == null || points.Length < 3) throw new ArgumentException("Polygon needs at least three points.");
            _points = (Vector2[])points.Clone(); Surface = surface;
            double area = 0;
            for (int i = 0; i < _points.Length; i++)
            {
                Vector2 a = _points[i], b = _points[(i + 1) % _points.Length]; GolfCheck.Point(a);
                if ((a - b).sqrMagnitude <= Epsilon * Epsilon) throw new ArgumentException("Repeated polygon vertex.");
                area += Cross(a, b);
                for (int j = i + 1; j < _points.Length; j++)
                {
                    if (j == i + 1 || (i == 0 && j == _points.Length - 1)) continue;
                    if (Intersects(a, b, _points[j], _points[(j + 1) % _points.Length]))
                        throw new ArgumentException("Self-intersecting polygon.");
                }
            }
            if (Math.Abs(area) <= Epsilon) throw new ArgumentException("Zero-area polygon.");
        }

        static double Cross(Vector2 a, Vector2 b) => (double)a.x * b.y - (double)a.y * b.x;
        static bool On(Vector2 p, Vector2 a, Vector2 b) => Math.Abs(Cross(b - a, p - a)) <= Epsilon &&
            p.x >= Math.Min(a.x, b.x) - Epsilon && p.x <= Math.Max(a.x, b.x) + Epsilon &&
            p.y >= Math.Min(a.y, b.y) - Epsilon && p.y <= Math.Max(a.y, b.y) + Epsilon;
        static bool Intersects(Vector2 a, Vector2 b, Vector2 c, Vector2 d)
        {
            double x = Cross(b - a, c - a), y = Cross(b - a, d - a);
            double z = Cross(d - c, a - c), w = Cross(d - c, b - c);
            return ((x > 0 && y < 0 || x < 0 && y > 0) && (z > 0 && w < 0 || z < 0 && w > 0)) ||
                On(a, c, d) || On(b, c, d) || On(c, a, b) || On(d, a, b);
        }
        internal bool Contains(Vector2 p)
        {
            bool inside = false;
            for (int i = 0, j = _points.Length - 1; i < _points.Length; j = i++)
            {
                Vector2 a = _points[j], b = _points[i];
                if (On(p, a, b)) return true;
                if ((a.y > p.y) != (b.y > p.y) &&
                    p.x < (double)(b.x - a.x) * (p.y - a.y) / (b.y - a.y) + a.x) inside = !inside;
            }
            return inside;
        }

        internal float FirstCrossing(Vector2 a, Vector2 b)
        {
            double first = 1;
            Vector2 r = b - a;
            for (int i = 0; i < _points.Length; i++)
            {
                Vector2 c = _points[i], s = _points[(i + 1) % _points.Length] - c;
                double denom = Cross(r, s);
                if (Math.Abs(denom) <= Epsilon) continue;
                double t = Cross(c - a, s) / denom, u = Cross(c - a, r) / denom;
                if (t > Epsilon && t < first && u >= 0 && u <= 1)
                {
                    Vector2 crossing = a + r * (float)t;
                    // At region-scale coordinates an intersection can round back to the start.
                    // Re-clipping there makes no spatial progress. The outgoing-surface probe handles
                    // that shared edge; retain all crossings representable beyond the current point.
                    if (crossing.x != a.x || crossing.y != a.y) first = t;
                }
            }
            return (float)first;
        }
    }
}
