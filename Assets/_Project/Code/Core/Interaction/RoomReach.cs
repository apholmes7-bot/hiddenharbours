using System;
using System.Collections.Generic;
using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>Closed wall polygons block even a tangent or zero-length press. No physics query.</summary>
    public static class RoomReach
    {
        public static bool SameFloor(int actorLevel, int fixtureLevel) =>
            actorLevel >= 0 && fixtureLevel >= 0 && actorLevel == fixtureLevel;

        public static bool Finite(Vector2 p) =>
            !float.IsNaN(p.x) && !float.IsInfinity(p.x) &&
            !float.IsNaN(p.y) && !float.IsInfinity(p.y);

        public static bool Clear(Vector2 actor, Vector2 target, IReadOnlyList<Vector2> wall)
        {
            if (!Finite(actor) || !Finite(target) || wall == null || wall.Count < 3) return false;
            for (int i = 0; i < wall.Count; i++)
                if (!Finite(wall[i])) return false;
            if (Inside(actor, wall) || Inside(target, wall)) return false;
            for (int i = 0; i < wall.Count; i++)
                if (Crosses(actor, target, wall[i], wall[(i + 1) % wall.Count])) return false;
            return true;
        }

        static double Turn(Vector2 a, Vector2 b, Vector2 c) =>
            ((double)b.x - a.x) * ((double)c.y - a.y) -
            ((double)b.y - a.y) * ((double)c.x - a.x);

        static bool On(Vector2 a, Vector2 b, Vector2 p) => Turn(a, b, p) == 0d &&
            p.x >= Math.Min(a.x, b.x) && p.x <= Math.Max(a.x, b.x) &&
            p.y >= Math.Min(a.y, b.y) && p.y <= Math.Max(a.y, b.y);

        static bool Crosses(Vector2 a, Vector2 b, Vector2 c, Vector2 d)
        {
            double ac = Turn(a, b, c), ad = Turn(a, b, d);
            double ca = Turn(c, d, a), cb = Turn(c, d, b);
            if (On(a, b, c) || On(a, b, d) || On(c, d, a) || On(c, d, b)) return true;
            return ((ac < 0d && ad > 0d) || (ac > 0d && ad < 0d)) &&
                   ((ca < 0d && cb > 0d) || (ca > 0d && cb < 0d));
        }

        static bool Inside(Vector2 p, IReadOnlyList<Vector2> wall)
        {
            bool inside = false;
            for (int i = 0, j = wall.Count - 1; i < wall.Count; j = i++)
            {
                Vector2 a = wall[j], b = wall[i];
                if (On(a, b, p)) return true;
                if ((a.y > p.y) != (b.y > p.y) &&
                    p.x < ((double)b.x - a.x) * ((double)p.y - a.y) / ((double)b.y - a.y) + a.x)
                    inside = !inside;
            }
            return inside;
        }
    }
}
