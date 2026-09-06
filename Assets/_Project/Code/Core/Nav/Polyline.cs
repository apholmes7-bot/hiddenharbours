using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>Walking a line of points</b> — how long it is, where you are a given distance along it, and
    /// which way you are pointing when you get there. The primitives every scheduled body in the game
    /// reads its position off: a villager on a lane, a truck on a road.
    ///
    /// <para><b>Why it is here and not beside its first caller.</b> The routine engine
    /// (<c>World.RoutineLaneTree</c>) wrote these first, for walkers; the road fleet's scheduled trips
    /// need the identical arithmetic and live in a module that may not name World (rule 4). Two copies of
    /// "where is she along this line" is exactly the one-quantity-two-computations mistake that has cost
    /// this project a defect at a time, so the arithmetic moved down to Core and the lane tree delegates.
    /// Nothing about the maths changed; the callers did.</para>
    ///
    /// <para>⭐ <b><see cref="TangentAlong"/> returns a DIRECTION, never a bearing, and that is the whole
    /// reason it exists.</b> A walker's facing is a GROUND bearing (<see cref="IsoGround.BearingDegrees"/>
    /// — the iso squash un-done, because a character's facing row depicts a bearing across the ground) and
    /// a vehicle's heading is a WORLD-XY one (<c>BoatKinematics.BearingDegrees(transform.up)</c>, the
    /// fleet's convention, which the mesh driver reads back). The two disagree by up to 12.5°. Handing
    /// back the raw direction makes each caller state which convention it is in rather than inheriting the
    /// other one's by accident — a truck posed with a walker's bearing drives visibly crabbed down a road
    /// she is perfectly on.</para>
    ///
    /// <para>Allocation-free and total: a degenerate slice answers a defined value rather than a NaN, and
    /// a zero-length segment is skipped rather than divided by.</para>
    /// </summary>
    public static class Polyline
    {
        /// <summary>Total walked length of a polyline slice, in metres. 0 for fewer than two points.</summary>
        public static float Length(Vector2[] points, int start, int count)
        {
            if (points == null || count < 2) return 0f;
            float total = 0f;
            for (int i = start; i < start + count - 1; i++)
                total += Vector2.Distance(points[i], points[i + 1]);
            return total;
        }

        /// <summary>
        /// The point <paramref name="distance"/> metres along a polyline slice. Clamped at both ends:
        /// before the start you are at the start, past the end you are at the end — which is what
        /// standing at the destination IS, so arrival needs no branch of its own.
        /// </summary>
        public static Vector2 PointAlong(Vector2[] points, int start, int count, float distance)
        {
            if (points == null || count <= 0) return Vector2.zero;
            if (count == 1 || distance <= 0f) return points[start];

            float remaining = distance;
            for (int i = start; i < start + count - 1; i++)
            {
                Vector2 a = points[i], b = points[i + 1];
                float len = Vector2.Distance(a, b);
                if (len <= 0f) continue;
                if (remaining <= len) return Vector2.Lerp(a, b, remaining / len);
                remaining -= len;
            }
            return points[start + count - 1];
        }

        /// <summary>
        /// The UNIT direction of travel <paramref name="distance"/> metres along the slice — the direction
        /// of the segment you are on, clamped to the last real segment past the end.
        ///
        /// <para>Returns <see cref="Vector2.zero"/> for a slice with no length at all, which is the one
        /// honest answer: a body standing on a single point is not travelling anywhere, and inventing a
        /// direction for it would point somebody north. Callers hold their previous facing instead — see
        /// the class note on why this is not a bearing.</para>
        /// </summary>
        public static Vector2 TangentAlong(Vector2[] points, int start, int count, float distance)
        {
            if (points == null || count < 2) return Vector2.zero;

            float remaining = Mathf.Max(0f, distance);
            Vector2 last = Vector2.zero;
            for (int i = start; i < start + count - 1; i++)
            {
                Vector2 a = points[i], b = points[i + 1];
                Vector2 leg = b - a;
                float len = leg.magnitude;
                if (len <= 0f) continue;
                last = leg / len;
                if (remaining <= len) return last;
                remaining -= len;
            }
            return last;   // past the end: the direction she was travelling when she got there
        }

        /// <summary>
        /// The point on the slice nearest <paramref name="from"/>, and how far along the slice it sits.
        /// The projection every road-follower needs: "where on this road am I, and how far down it".
        ///
        /// <para>Exhaustive over segments rather than clever — a region's road is a handful of points and
        /// this is read once per frame per machine, so the cost is a few dozen float operations and the
        /// alternative is a spatial index that could be wrong.</para>
        /// </summary>
        public static Vector2 NearestPoint(Vector2[] points, int start, int count, Vector2 from,
                                           out float distanceAlong)
        {
            distanceAlong = 0f;
            if (points == null || count <= 0) return Vector2.zero;
            if (count == 1) return points[start];

            Vector2 best = points[start];
            float bestSqr = float.MaxValue;
            float travelled = 0f;

            for (int i = start; i < start + count - 1; i++)
            {
                Vector2 a = points[i], b = points[i + 1];
                Vector2 leg = b - a;
                float len = leg.magnitude;
                if (len <= 0f) continue;

                float t = Mathf.Clamp01(Vector2.Dot(from - a, leg) / (len * len));
                Vector2 p = a + leg * t;
                float sqr = (from - p).sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    best = p;
                    distanceAlong = travelled + len * t;
                }
                travelled += len;
            }
            return best;
        }
    }
}
