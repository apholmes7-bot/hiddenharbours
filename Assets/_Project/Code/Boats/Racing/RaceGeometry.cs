using System;
using UnityEngine;

namespace HiddenHarbours.Boats
{
    internal static class RaceGeometry
    {
        public static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;
        public static float Side(RaceLine line, Vector2 point) => Cross(line.B - line.A, point - line.A);
        public static Vector2 Bearing(float degrees)
            => new Vector2(Mathf.Sin(degrees * Mathf.Deg2Rad), Mathf.Cos(degrees * Mathf.Deg2Rad));

        // Half-open crossing: landing ON a gate waits for evidence of travel beyond it.
        // This credits a sampled gate point exactly once, with its interpolated boundary time.
        public static bool Crosses(RaceLine line, Vector2 from, Vector2 to, out float fraction)
        {
            fraction = 0f;
            float a = Side(line, from), b = Side(line, to);
            if (a > 0f || b <= 0f) return false;
            fraction = a / (a - b);
            Vector2 point = Vector2.LerpUnclamped(from, to, fraction);
            float along = Vector2.Dot(point - line.A, line.B - line.A) / (line.B - line.A).sqrMagnitude;
            return along >= 0f && along <= 1f;
        }

        public static RaceLine Gate(RaceMark mark, float bearing)
        {
            Vector2 ray = Bearing(bearing);
            Vector2 inner = mark.Position + ray * mark.ClearanceMetres;
            Vector2 outer = mark.Position + ray * mark.ZoneRadiusMetres;
            return mark.Side == RaceRoundingSide.Port ? new RaceLine(inner, outer) : new RaceLine(outer, inner);
        }

        public static float DistanceToSegment(Vector2 point, Vector2 from, Vector2 to)
        {
            Vector2 d = to - from;
            float t = d.sqrMagnitude == 0f ? 0f : Mathf.Clamp01(Vector2.Dot(point - from, d) / d.sqrMagnitude);
            return Vector2.Distance(point, from + d * t);
        }

        public static void ValidateLine(RaceLine line)
        {
            RequireFinite(line.A, nameof(line.A)); RequireFinite(line.B, nameof(line.B));
            if ((line.B - line.A).sqrMagnitude == 0f) throw new ArgumentException("A gate needs distinct endpoints.");
        }

        public static void RequireFinite(double value, string name)
        { if (double.IsNaN(value) || double.IsInfinity(value)) throw new ArgumentOutOfRangeException(name); }
        public static void RequireFinite(Vector2 value, string name)
        { RequireFinite(value.x, name); RequireFinite(value.y, name); }
        public static void RequirePositive(float value, string name)
        { RequireFinite(value, name); if (value <= 0f) throw new ArgumentOutOfRangeException(name); }
        public static void RequireNonNegative(float value, string name)
        { RequireFinite(value, name); if (value < 0f) throw new ArgumentOutOfRangeException(name); }
        public static void RequireId(string id, string prefix)
        {
            if (string.IsNullOrEmpty(id) || !id.StartsWith(prefix, StringComparison.Ordinal) || id.Length == prefix.Length)
                throw new ArgumentException("Expected a stable " + prefix + " id.");
            for (int i = prefix.Length; i < id.Length; i++)
                if (!(id[i] >= 'a' && id[i] <= 'z') && !(id[i] >= '0' && id[i] <= '9') && id[i] != '_')
                    throw new ArgumentException("Ids use lowercase snake_case.");
        }
    }

    /// <summary>One ordered mark only; failed clearance/direction/zone attempts require a fresh entry.</summary>
    public sealed class SailRaceRounding
    {
        private const double NumericalAngleToleranceDeg = 0.001;
        private bool _entered;
        private double _sweep;
        public void Reset() { _entered = false; _sweep = 0; }

        public bool Advance(RaceMark mark, Vector2 from, Vector2 to, out float fraction)
        {
            fraction = 0f;
            float entryFraction = 0f;
            if (!_entered)
            {
                if (!RaceGeometry.Crosses(RaceGeometry.Gate(mark, mark.EntryBearingDeg), from, to, out entryFraction))
                    return false;
                from = Vector2.LerpUnclamped(from, to, entryFraction);
                _entered = true;
                _sweep = 0;
            }
            bool exits = RaceGeometry.Crosses(RaceGeometry.Gate(mark, mark.ExitBearingDeg), from, to, out float exitFraction);
            Vector2 end = exits ? Vector2.LerpUnclamped(from, to, exitFraction) : to;
            Vector2 a = from - mark.Position, b = end - mark.Position;
            double turn = Math.Atan2(RaceGeometry.Cross(a, b), Vector2.Dot(a, b)) * Mathf.Rad2Deg * mark.Direction;
            RaceLine entry = RaceGeometry.Gate(mark, mark.EntryBearingDeg);
            bool returnsThroughEntry = RaceGeometry.Crosses(new RaceLine(entry.B, entry.A), from, end, out _);
            if (a.magnitude > mark.ZoneRadiusMetres || b.magnitude > mark.ZoneRadiusMetres || returnsThroughEntry ||
                RaceGeometry.DistanceToSegment(mark.Position, from, end) < mark.ClearanceMetres)
            { Reset(); return false; }
            _sweep += turn;
            if (!exits) return false;
            // Net signed sweep permits a tack within the zone, while rejecting the opposite trip around it.
            bool rounded = _sweep >= mark.SweepDeg - NumericalAngleToleranceDeg;
            fraction = entryFraction + (1f - entryFraction) * exitFraction;
            Reset();
            return rounded;
        }
    }
}
