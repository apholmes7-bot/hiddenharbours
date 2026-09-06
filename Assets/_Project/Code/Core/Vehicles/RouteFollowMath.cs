using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>How hard to press, and which way to turn, to follow a line of points</b> — the pure driving
    /// maths, lifted out of the PlayMode journey that first measured it (#701) so that the game and the
    /// fixture cannot disagree about how a machine is driven.
    ///
    /// <para><b>Three rules, each of which cost a six-minute run to find</b> (memory
    /// <c>a-scripted-driver-lands-the-demand-and-passes-the-waypoint</c>):</para>
    /// <list type="number">
    ///   <item><b>Switch on the PERPENDICULAR, not on the reach.</b> Pure pursuit with a 3 m reach put
    ///   five machines in a permanent orbit of the laydown's gate: a machine that overshoots a waypoint at
    ///   an angle cannot come back to it when her turning circle (8–13 m at a slow throttle, because
    ///   <c>VehicleController.EffectiveSteer</c> widens with speed) is wider than the reach. A waypoint is
    ///   passed when she crosses the plane through it perpendicular to the leg — see
    ///   <see cref="HasReached"/>.</item>
    ///   <item><b>An along-road distance does not mean "on the road".</b> Ending a leg on the projection
    ///   alone parked machines 3–16 m off the centre-line and still converging. A leg on a road ends when
    ///   she is far enough along AND inside the carriageway's own half-width — see
    ///   <see cref="LegEnded"/>.</item>
    ///   <item><b>Steer at a LOOKAHEAD point re-derived every step</b> (<see cref="LookaheadTarget"/>),
    ///   not at one fixed point on the line, so she converges onto the road rather than crossing it at
    ///   whatever angle she arrived.</item>
    /// </list>
    ///
    /// <para><b>Deliberately unchanged from the measured driver.</b> The throttle switches between cruise
    /// and turn rather than easing between them, which is not what a smoother implementation would do —
    /// but the binary switch is what was measured to get every machine in the fleet through the yard, and
    /// a "better" curve here would be an unmeasured change to the one thing this file exists to pin.</para>
    ///
    /// <para>Engine-light statics over <see cref="Vector2"/>: no allocation, no RNG, no state, no
    /// <c>Transform</c>. EditMode-testable headless, and the same arithmetic whether it is a live
    /// <c>RouteDriver</c> pressing pedals or a test's held demand.</para>
    /// </summary>
    public static class RouteFollowMath
    {
        /// <summary>
        /// <b>What a driver following a route asks for</b>, in one frame: how far she looks ahead, how
        /// close counts as arrived, and how hard she presses on the straight and through a turn.
        ///
        /// <para>These are FEEL, and every one of them lives on <c>VehicleDef</c> (rule 6) so that the
        /// owner tunes a truck's driving the same way she tunes its top speed. The defaults here are the
        /// numbers the journey fixture measured across all seven machines, and they are what a def
        /// authored before the fields existed deserializes to — <see cref="Sane"/> is what makes that
        /// true.</para>
        /// </summary>
        public readonly struct RouteFollowTuning
        {
            /// <summary>How close (m) to a waypoint counts as arrived, BEFORE the perpendicular rule.
            /// Small on its own — the perpendicular is what actually switches most waypoints.</summary>
            public readonly float WaypointReachMetres;

            /// <summary>How far ahead (m) along the line she aims. Too short and she saws; too long and
            /// she cuts corners. 12 m is a little under two truck lengths.</summary>
            public readonly float LookaheadMetres;

            /// <summary>Throttle fraction on the straight (0…1 of her ceiling).</summary>
            public readonly float CruiseThrottle;

            /// <summary>Throttle fraction through a turn. Well under cruise: speed-sensitive steering
            /// widens every machine's circle, so a driver hunting a waypoint at full throttle orbits
            /// it.</summary>
            public readonly float TurnThrottle;

            /// <summary>How far off (degrees) her nose has to be from the target before she comes off
            /// cruise — the "brake for the turn ahead" knob.
            ///
            /// <para>⚠️ An ANGLE, not a radius, and deliberately: a driver aiming at a lookahead point has
            /// a heading error already in hand and no radius at all. Turning it into a radius would mean
            /// fitting a circle to three points every step to recover a number the error carries.</para>
            /// </summary>
            public readonly float SlowForTurnDegrees;

            /// <summary>Degrees of heading error that mean full lock. Smaller is twitchier.</summary>
            public readonly float SteerGainDegrees;

            public RouteFollowTuning(float waypointReachMetres, float lookaheadMetres, float cruiseThrottle,
                                     float turnThrottle, float slowForTurnDegrees, float steerGainDegrees)
            {
                WaypointReachMetres = waypointReachMetres;
                LookaheadMetres = lookaheadMetres;
                CruiseThrottle = cruiseThrottle;
                TurnThrottle = turnThrottle;
                SlowForTurnDegrees = slowForTurnDegrees;
                SteerGainDegrees = steerGainDegrees;
            }

            /// <summary>The measured driver's numbers — what every machine in the fleet was driven
            /// through the laydown and out onto Wharf Road on.</summary>
            public static RouteFollowTuning Measured => new(3f, 12f, 0.6f, 0.15f, 12f, 20f);

            /// <summary>
            /// This tuning with every nonsensical field replaced by the measured one. A def baked before
            /// these fields existed deserializes them all to zero, and a zero lookahead is a driver aiming
            /// at her own bumper — she would saw on the spot for ever with nothing in the log. Filling in
            /// is the cozy answer (P5) and it is also the only one that keeps an old asset driveable.
            /// </summary>
            public RouteFollowTuning Sane()
            {
                RouteFollowTuning m = Measured;
                return new RouteFollowTuning(
                    WaypointReachMetres > 0f ? WaypointReachMetres : m.WaypointReachMetres,
                    LookaheadMetres > 0f ? LookaheadMetres : m.LookaheadMetres,
                    CruiseThrottle > 0f ? Mathf.Min(CruiseThrottle, 1f) : m.CruiseThrottle,
                    TurnThrottle > 0f ? Mathf.Min(TurnThrottle, 1f) : m.TurnThrottle,
                    SlowForTurnDegrees > 0f ? SlowForTurnDegrees : m.SlowForTurnDegrees,
                    SteerGainDegrees > 0f ? SteerGainDegrees : m.SteerGainDegrees);
            }
        }

        /// <summary>
        /// <b>One frame of pure pursuit, in compass terms.</b> The error is the target's bearing less her
        /// heading, positive when the target lies clockwise (to her RIGHT) — and right is −1 on the wheel
        /// (the rig's own sense, +1 = full LEFT lock), so the steer is the negated, gained error.
        ///
        /// <para><paramref name="headingDegrees"/> is her WORLD-XY heading —
        /// <c>BoatKinematics.BearingDegrees(transform.up)</c>, the fleet's one convention and the same
        /// number <c>VehicleMeshDriver</c> reads back to pick her picture. Never a ground bearing: the two
        /// differ by up to 12.5° and a driver fed the wrong one steers a permanent bias into every
        /// straight.</para>
        /// </summary>
        public static DriveDemand Toward(float headingDegrees, Vector2 pos, Vector2 target, bool slow,
                                         in RouteFollowTuning tuning)
        {
            float want = BoatKinematics.BearingDegrees(target - pos);
            float error = Mathf.DeltaAngle(headingDegrees, want);
            float steer = -Mathf.Clamp(error / Mathf.Max(1e-3f, tuning.SteerGainDegrees), -1f, 1f);
            float throttle = slow || Mathf.Abs(error) > tuning.SlowForTurnDegrees
                ? tuning.TurnThrottle
                : tuning.CruiseThrottle;
            return new DriveDemand(throttle, steer, false);
        }

        /// <summary>
        /// A waypoint is reached when she is within <paramref name="reachMetres"/> of it — OR when she has
        /// passed the plane through it perpendicular to the leg she was on. See rule 1 in the class note:
        /// without the second clause a machine whose turning circle is wider than the reach orbits an
        /// overshot waypoint for ever.
        /// </summary>
        public static bool HasReached(Vector2 from, Vector2 target, Vector2 pos, float reachMetres)
        {
            if (Vector2.Distance(pos, target) <= reachMetres) return true;
            Vector2 leg = target - from;
            return leg.sqrMagnitude > 1e-6f && Vector2.Dot(pos - target, leg.normalized) >= 0f;
        }

        /// <summary>
        /// The point on the line a lookahead ahead of her own projection onto it — re-derived every step,
        /// which is what makes her CONVERGE onto a road rather than cross it once.
        /// </summary>
        public static Vector2 LookaheadTarget(Vector2[] route, int start, int count, Vector2 pos,
                                              float lookaheadMetres)
        {
            if (route == null || count <= 0) return pos;
            Vector2 onLine = Polyline.NearestPoint(route, start, count, pos, out float along);
            Vector2 dir = Polyline.TangentAlong(route, start, count, along);
            return dir == Vector2.zero ? onLine : onLine + dir * lookaheadMetres;
        }

        /// <summary>The line's direction where she is standing — the direction of the segment her nearest
        /// point sits on, in the order the polyline is published.</summary>
        public static Vector2 DirectionAt(Vector2[] route, int start, int count, Vector2 at)
        {
            if (route == null || count < 2) return Vector2.zero;
            Polyline.NearestPoint(route, start, count, at, out float along);
            return Polyline.TangentAlong(route, start, count, along);
        }

        /// <summary>How far she is from the line's centre, in metres — the second half of "on the
        /// road".</summary>
        public static float OffCentreLineMetres(Vector2[] route, int start, int count, Vector2 pos)
        {
            if (route == null || count <= 0) return float.PositiveInfinity;
            return Vector2.Distance(pos, Polyline.NearestPoint(route, start, count, pos, out _));
        }

        /// <summary>
        /// <b>Has the leg ended?</b> She is at least <paramref name="requiredAlongMetres"/> past
        /// <paramref name="join"/> in the road's own direction AND inside
        /// <paramref name="laneHalfWidthMetres"/> of its centre-line. See rule 2 in the class note: the
        /// second clause is what makes "parked on the road" a thing she has DONE rather than a thing she
        /// was near.
        /// </summary>
        public static bool LegEnded(Vector2[] route, int start, int count, Vector2 pos, Vector2 join,
                                    Vector2 dirAtJoin, float requiredAlongMetres, float laneHalfWidthMetres)
        {
            float along = Vector2.Dot(pos - join, dirAtJoin);
            if (along < requiredAlongMetres) return false;
            return OffCentreLineMetres(route, start, count, pos) <= laneHalfWidthMetres;
        }
    }
}
