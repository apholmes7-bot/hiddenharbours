using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>One body's day as a set of legs</b> — for each block of a timetable, the line it covers and how
    /// fast it covers it. Flat parallel arrays, one allocation at construction and none afterwards
    /// (rule 7); everything after that is a pure read.
    ///
    /// <para><b>Why a body and not a plan.</b> A scheduled trip has TWO bodies moving on ONE timetable —
    /// the machine on her road and her driver on his few metres of gravel — and they are the same
    /// arithmetic over different geometry and different speeds. One class, two instances, one set of
    /// bugs. <c>VehicleTripPlan</c> holds a pair of these and the departure hours they share.</para>
    ///
    /// <para>⭐ <b>The heading is handed back as a DIRECTION, never a bearing.</b> A machine's heading is
    /// world-XY and a walker's is a ground bearing (the iso squash un-done); they differ by up to 12.5°.
    /// Publishing a bearing here would force one convention on both bodies and the loser walks crabbed.
    /// See <see cref="Polyline.TangentAlong"/>.</para>
    ///
    /// <para><b>A standing leg is a one-point route.</b> A body that is not going anywhere this block has
    /// a single waypoint and a zero speed, and <see cref="Sample"/> answers "there, not moving, holding
    /// the stated facing" with no branch of its own — the same trick <c>RoutinePlan</c> uses to make
    /// arrival fall out of a clamp.</para>
    /// </summary>
    public sealed class ScheduledLegs
    {
        /// <summary>All legs' routes, flattened. Leg <c>i</c> is the slice
        /// <c>[Start[i], Start[i] + Count[i])</c>.</summary>
        public readonly Vector2[] Waypoints;

        public readonly int[] Start;
        public readonly int[] Count;

        /// <summary>Length of each leg's route in metres. Precomputed: it never changes, and re-measuring
        /// a polyline every frame would be the one avoidable cost here.</summary>
        public readonly float[] LengthMetres;

        /// <summary>How fast the body covers leg <c>i</c>, m/s. Zero means it stands there.</summary>
        public readonly float[] SpeedMetresPerSecond;

        /// <summary>Which way the body is turned once the leg's travel is done, as a unit world delta —
        /// the stance it holds while it waits for the next departure.</summary>
        public readonly Vector2[] StandFacing;

        public int LegCount => Start.Length;

        private ScheduledLegs(Vector2[] waypoints, int[] start, int[] count, float[] lengthMetres,
                              float[] speed, Vector2[] standFacing)
        {
            Waypoints = waypoints;
            Start = start;
            Count = count;
            LengthMetres = lengthMetres;
            SpeedMetresPerSecond = speed;
            StandFacing = standFacing;
        }

        /// <summary>
        /// Flatten a set of per-leg routes into one body. Every array must be the same length; a route
        /// with no points at all becomes a single point at the origin rather than an index that throws,
        /// because a half-authored timetable should leave something standing in the wrong place where it
        /// can be seen, not take the region down.
        /// </summary>
        public static ScheduledLegs Build(Vector2[][] routes, float[] speeds, Vector2[] standFacing)
        {
            int legs = routes != null ? routes.Length : 0;
            var start = new int[legs];
            var count = new int[legs];
            var length = new float[legs];
            var speed = new float[legs];
            var facing = new Vector2[legs];

            int total = 0;
            for (int i = 0; i < legs; i++) total += routes[i] != null && routes[i].Length > 0 ? routes[i].Length : 1;

            var flat = new Vector2[total];
            int n = 0;
            for (int i = 0; i < legs; i++)
            {
                Vector2[] r = routes[i];
                start[i] = n;
                if (r == null || r.Length == 0) { flat[n++] = Vector2.zero; count[i] = 1; }
                else { for (int j = 0; j < r.Length; j++) flat[n++] = r[j]; count[i] = r.Length; }

                length[i] = Polyline.Length(flat, start[i], count[i]);
                speed[i] = speeds != null && i < speeds.Length ? Mathf.Max(0f, speeds[i]) : 0f;
                Vector2 f = standFacing != null && i < standFacing.Length ? standFacing[i] : Vector2.zero;
                facing[i] = f.sqrMagnitude > 1e-6f ? f.normalized : Vector2.up;
            }
            return new ScheduledLegs(flat, start, count, length, speed, facing);
        }

        /// <summary>How many game hours leg <paramref name="leg"/> takes. Zero for a standing leg, which
        /// is what makes a timetable's derived hours collapse correctly when nobody has to walk.</summary>
        public float TravelHours(int leg, float secondsPerGameHour)
            => InRange(leg)
                ? DaySchedule.TravelHours(LengthMetres[leg], SpeedMetresPerSecond[leg], secondsPerGameHour)
                : 0f;

        /// <summary>The point <paramref name="distance"/> metres along leg <paramref name="leg"/> — the
        /// raw geometry read, for content tests that want to walk a route without a clock.</summary>
        public Vector2 PointAt(int leg, float distance)
            => InRange(leg) ? Polyline.PointAlong(Waypoints, Start[leg], Count[leg], distance) : Vector2.zero;

        /// <summary>
        /// Where the body is, which way it is pointing, and whether it is still going, after
        /// <paramref name="elapsedHours"/> of leg <paramref name="leg"/>.
        ///
        /// <para>Past the end of the route the body stands at its last point holding
        /// <see cref="StandFacing"/> — arrival is the clamp, and the facing swaps from the route's to the
        /// stance's on exactly the frame the travel finishes.</para>
        /// </summary>
        public void Sample(int leg, float elapsedHours, float secondsPerGameHour, out Vector2 position,
                           out Vector2 direction, out bool moving)
        {
            if (!InRange(leg))
            {
                position = Vector2.zero; direction = Vector2.up; moving = false; return;
            }

            float travel = TravelHours(leg, secondsPerGameHour);
            moving = travel > 0f && elapsedHours < travel;

            float distance = moving
                ? DaySchedule.DistanceTravelled(elapsedHours, SpeedMetresPerSecond[leg], secondsPerGameHour)
                : LengthMetres[leg];

            position = Polyline.PointAlong(Waypoints, Start[leg], Count[leg], distance);

            if (!moving) { direction = StandFacing[leg]; return; }

            Vector2 tangent = Polyline.TangentAlong(Waypoints, Start[leg], Count[leg], distance);
            direction = tangent == Vector2.zero ? StandFacing[leg] : tangent;
        }

        private bool InRange(int leg) => leg >= 0 && leg < Start.Length;
    }
}
