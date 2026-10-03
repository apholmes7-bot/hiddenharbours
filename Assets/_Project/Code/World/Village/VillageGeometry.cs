using System;
using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.World
{
    /// <summary>Village picture coordinates remain unchanged; only distances are measured on ground.</summary>
    public static class VillageGeometry
    {
        public static Vector2 Ground(Vector2 p) => new Vector2(p.x, p.y / IsoGround.GroundDepthScale);
        public static float Distance(Vector2 a, Vector2 b) => (Ground(a) - Ground(b)).magnitude;

        public static float PointSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            p = Ground(p); a = Ground(a); b = Ground(b);
            var d = b - a;
            float t = d.sqrMagnitude == 0 ? 0 : Math.Max(0, Math.Min(1, Vector2.Dot(p - a, d) / d.sqrMagnitude));
            return (p - a - t * d).magnitude;
        }

        private static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;

        public static bool Intersects(Vector2 a, Vector2 b, Vector2 c, Vector2 d)
        {
            float abC = Cross(b - a, c - a), abD = Cross(b - a, d - a);
            float cdA = Cross(d - c, a - c), cdB = Cross(d - c, b - c);
            if (abC == 0 && abD == 0)
                return Math.Max(Math.Min(a.x, b.x), Math.Min(c.x, d.x)) <= Math.Min(Math.Max(a.x, b.x), Math.Max(c.x, d.x)) &&
                       Math.Max(Math.Min(a.y, b.y), Math.Min(c.y, d.y)) <= Math.Min(Math.Max(a.y, b.y), Math.Max(c.y, d.y));
            return abC * abD <= 0 && cdA * cdB <= 0;
        }

        public static float SegmentDistance(Vector2 a, Vector2 b, Vector2 c, Vector2 d) =>
            Intersects(a, b, c, d) ? 0 : Math.Min(Math.Min(PointSegment(a, c, d), PointSegment(b, c, d)),
                                                Math.Min(PointSegment(c, a, b), PointSegment(d, a, b)));

        public static bool Contains(Vector2[] polygon, Vector2 p)
        {
            bool inside = false;
            for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
            {
                var a = polygon[j]; var b = polygon[i];
                if (Cross(b - a, p - a) == 0 && p.x >= Math.Min(a.x, b.x) && p.x <= Math.Max(a.x, b.x) &&
                    p.y >= Math.Min(a.y, b.y) && p.y <= Math.Max(a.y, b.y)) return true;
                if ((a.y > p.y) != (b.y > p.y) && p.x < (b.x - a.x) * (p.y - a.y) / (b.y - a.y) + a.x)
                    inside = !inside;
            }
            return inside;
        }

        public static float PolygonDistance(Vector2[] a, Vector2[] b)
        {
            if (a.Length == 0 || b.Length == 0) throw new ArgumentException("An outline needs vertices.");
            if (Contains(a, b[0]) || Contains(b, a[0])) return 0;
            float distance = float.PositiveInfinity;
            for (int i = 0; i < a.Length; i++)
                for (int j = 0; j < b.Length; j++)
                    distance = Math.Min(distance, SegmentDistance(a[i], a[(i + 1) % a.Length], b[j], b[(j + 1) % b.Length]));
            return distance;
        }

        public static Vector2[] Rectangle(Vector4 bounds) => new[]
        {
            new Vector2(bounds.x, bounds.y), new Vector2(bounds.z, bounds.y),
            new Vector2(bounds.z, bounds.w), new Vector2(bounds.x, bounds.w)
        };

        public static bool InBounds(Vector4 b, Vector2 p) => p.x >= b.x && p.x <= b.z && p.y >= b.y && p.y <= b.w;
        public static bool Overlaps(Vector4 a, Vector4 b) => a.x <= b.z && a.z >= b.x && a.y <= b.w && a.w >= b.y;

        public static bool Crosses(Vector4 bounds, Vector2[] points)
        {
            var corners = Rectangle(bounds);
            for (int i = 0; i < points.Length; i++)
            {
                if (InBounds(bounds, points[i])) return true;
                if (i == 0) continue;
                for (int j = 0; j < corners.Length; j++)
                    if (Intersects(points[i - 1], points[i], corners[j], corners[(j + 1) % corners.Length])) return true;
            }
            return false;
        }

        public static float DistanceToBounds(Vector2 p, Vector4 b) => Distance(p,
            new Vector2(Math.Max(b.x, Math.Min(b.z, p.x)), Math.Max(b.y, Math.Min(b.w, p.y))));
    }
}
