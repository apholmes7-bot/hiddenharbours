using System;
using UnityEngine;

namespace HiddenHarbours.Boats
{
    /// <summary>
    /// PURE local steering for the ambient fisher fleet (canon M2-33): seek-the-spot plus soft
    /// separation from other boats / the player / the player's buoys, plus a tide-aware shoal
    /// look-ahead. Greybox seamanship, not pathfinding — the world's truth is the painted seabed
    /// height field, so there is no NavMesh: the plan-time gate (<see cref="AmbientFleetPlan"/>)
    /// guarantees the ROUTE is safe at every tide, and these probes keep the moment-to-moment track
    /// looking like competent small-boat handling when something (usually the player) pushes a boat
    /// off it. Engine-light statics over <see cref="Vector2"/>, no allocation, no RNG, no state —
    /// EditMode-testable headless.
    /// </summary>
    public static class AmbientFleetSteering
    {
        /// <summary>
        /// The summed repulsion away from every obstacle in <paramref name="obstacles"/> (first
        /// <paramref name="count"/> entries) closer than <paramref name="radius"/>: each contributes
        /// away-from-it, weighted linearly from 1 at contact to 0 at the radius edge. A co-located
        /// obstacle (distance ~0 — e.g. the boat itself in a shared list) is skipped: it has no
        /// direction to push. Zero when nothing is near.
        /// </summary>
        public static Vector2 Repulsion(Vector2 pos, Vector2[] obstacles, int count, float radius)
        {
            Vector2 sum = Vector2.zero;
            if (obstacles == null || radius <= 0f) return sum;
            int n = Mathf.Min(count, obstacles.Length);
            for (int i = 0; i < n; i++)
            {
                Vector2 away = pos - obstacles[i];
                float d = away.magnitude;
                if (d < 1e-4f || d >= radius) continue;
                sum += (away / d) * (1f - d / radius);
            }
            return sum;
        }

        /// <summary>
        /// Compose the seek direction with an avoidance sum into the desired heading (unit vector).
        /// A starboard bias (a clockwise-perpendicular component of the avoidance) is folded in so two
        /// boats meeting head-on — where seek and avoid cancel exactly — curl the same way around each
        /// other instead of deadlocking bow-to-bow; if the sum still degenerates, the boat falls off
        /// 45° to starboard of her seek. Deterministic, and reads as boats that agree how to pass.
        /// </summary>
        public static Vector2 ComposeHeading(Vector2 seek, Vector2 avoid, float starboardBias = 0.35f)
        {
            // Clockwise perpendicular of the avoidance — "put the danger to port".
            Vector2 bias = new Vector2(avoid.y, -avoid.x) * starboardBias;
            Vector2 sum = seek + avoid + bias;
            if (sum.sqrMagnitude > 1e-8f) return sum.normalized;
            if (seek.sqrMagnitude > 1e-8f)
            {
                // Dead standoff: bear away 45° to starboard of the seek (deterministic tiebreak).
                Vector2 s = seek.normalized;
                const float Cos45 = 0.70710678f;
                return new Vector2(s.x * Cos45 + s.y * Cos45, -s.x * Cos45 + s.y * Cos45);
            }
            return Vector2.up;
        }

        /// <summary>
        /// The tide-aware shoal look-ahead — the live twin of the plan-time depth gate. Probes the
        /// water depth (at the CURRENT water level — the caller samples it) ahead of the bow and off
        /// both bows at <paramref name="sideDegrees"/>. All deep → no correction, full speed. Shoal
        /// ahead → a correction toward the deeper bow (or hard astern-ward if both bows shoal too) and
        /// <paramref name="speedScale"/> eases her down in proportion — she feels her way off a bar,
        /// never plows onto it. Pure: depth comes through the sampler.
        /// </summary>
        /// <param name="depthAt">Water depth (m) at a world point — <c>waterLevel − elevation</c>, tide-aware.</param>
        public static Vector2 DepthAvoid(Vector2 pos, Vector2 heading, float lookAheadMeters,
                                         float sideDegrees, Func<Vector2, float> depthAt,
                                         float minDepthMeters, out float speedScale)
        {
            speedScale = 1f;
            if (depthAt == null || heading.sqrMagnitude < 1e-8f) return Vector2.zero;

            Vector2 fwd = heading.normalized;
            Vector2 left = Rotate(fwd, sideDegrees);
            Vector2 right = Rotate(fwd, -sideDegrees);

            float dAhead = depthAt(pos + fwd * lookAheadMeters);
            if (dAhead >= minDepthMeters) return Vector2.zero;   // clear water ahead — steer nothing

            float dLeft = depthAt(pos + left * lookAheadMeters);
            float dRight = depthAt(pos + right * lookAheadMeters);

            // Ease down in proportion to how bad it is ahead (never below a crawl — she keeps steerage).
            speedScale = Mathf.Clamp(dAhead / Mathf.Max(0.01f, minDepthMeters), 0.15f, 1f);

            float shortfall = 1f - Mathf.Clamp01(dAhead / Mathf.Max(0.01f, minDepthMeters));
            if (dLeft < minDepthMeters && dRight < minDepthMeters)
                return -fwd * shortfall;                          // shoal all round the bow — back off
            return (dLeft >= dRight ? left : right) * shortfall;  // swing toward the deeper bow
        }

        /// <summary>Rotate <paramref name="current"/> toward <paramref name="desired"/> by at most
        /// <paramref name="maxDegrees"/> — the bow swings at a small boat's rate, never snaps. Both
        /// treated as directions; returns a unit vector (falls back to current/up when degenerate).</summary>
        public static Vector2 RotateToward(Vector2 current, Vector2 desired, float maxDegrees)
        {
            if (current.sqrMagnitude < 1e-8f) return desired.sqrMagnitude > 1e-8f ? desired.normalized : Vector2.up;
            if (desired.sqrMagnitude < 1e-8f) return current.normalized;
            float angle = Vector2.SignedAngle(current, desired);
            float step = Mathf.Clamp(angle, -maxDegrees, maxDegrees);
            return (Rotate(current, step)).normalized;
        }

        /// <summary>Rotate a vector counter-clockwise by <paramref name="degrees"/> (negative = clockwise).</summary>
        public static Vector2 Rotate(Vector2 v, float degrees)
        {
            float rad = degrees * Mathf.Deg2Rad;
            float cos = Mathf.Cos(rad);
            float sin = Mathf.Sin(rad);
            return new Vector2(v.x * cos - v.y * sin, v.x * sin + v.y * cos);
        }
    }
}
