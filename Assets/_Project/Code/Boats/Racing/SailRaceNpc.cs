using System;
using System.Collections.Generic;
using HiddenHarbours.Core;
using UnityEngine;

namespace HiddenHarbours.Boats
{
    public readonly struct SailRaceNpcSample
    {
        public readonly RaceBoatMotion Motion;
        public readonly SailRaceEntrant Race;
        public SailRaceNpcSample(RaceBoatMotion motion, SailRaceEntrant race) { Motion = motion; Race = race; }
    }

    /// <summary>Bounded replay model, intentionally unwired. Fixed ticks are anchored at the gun;
    /// wind is supplied as a pure (seed,time)->downwind m/s function. Each query replays from origin,
    /// so query order/render rate cannot change a racer. Kinematic polar navigation, not hull physics.</summary>
    public sealed class SailRaceNpc
    {
        private readonly string _id;
        private readonly SailRaceCourse _course;
        private readonly SailPolarDef _polar;
        private readonly int _seed, _headingSamples, _headingOffset;
        private readonly double _gunTime;
        private readonly float _noGo, _step, _horizon, _speedFraction, _initialBearing;
        private readonly Vector2 _start;
        private readonly Waypoint[] _route;
        private readonly struct Waypoint
        {
            public readonly Vector2 Position;
            public readonly int RequiredLap, RequiredMark, RetryIndex;
            public readonly bool RequiresStart;
            public Waypoint(Vector2 position, int lap = -1, int mark = -1, int retry = -1, bool requiresStart = false)
            { Position = position; RequiredLap = lap; RequiredMark = mark; RetryIndex = retry; RequiresStart = requiresStart; }
        }

        public SailRaceNpc(string id, SailRaceCourse course, int worldSeed, double gunTime, SailPolarDef polar,
            float noGoTrueDeg, Vector2 start, float initialBearingDeg, float fixedStepSeconds, float maximumReplaySeconds,
            int headingSamples, float minimumSpeedFraction, float maximumSpeedFraction, float gateClearanceMetres,
            float roundingLaneFraction, float roundingStepDeg, float gateOvershootDeg)
        {
            RaceGeometry.RequireId(id, "racer.");
            _course = course ?? throw new ArgumentNullException(nameof(course));
            RaceGeometry.RequireFinite(gunTime, nameof(gunTime));
            RaceGeometry.RequireFinite(start, nameof(start));
            RaceGeometry.RequireFinite(initialBearingDeg, nameof(initialBearingDeg));
            if (polar == null || !polar.IsUsable()) throw new ArgumentException("NPC polar is missing or malformed.");
            RaceGeometry.RequireNonNegative(noGoTrueDeg, nameof(noGoTrueDeg));
            if (noGoTrueDeg >= 180f) throw new ArgumentOutOfRangeException(nameof(noGoTrueDeg));
            RaceGeometry.RequirePositive(fixedStepSeconds, nameof(fixedStepSeconds));
            RaceGeometry.RequirePositive(maximumReplaySeconds, nameof(maximumReplaySeconds));
            if (headingSamples < 4) throw new ArgumentOutOfRangeException(nameof(headingSamples));
            RaceGeometry.RequirePositive(minimumSpeedFraction, nameof(minimumSpeedFraction));
            RaceGeometry.RequirePositive(maximumSpeedFraction, nameof(maximumSpeedFraction));
            if (minimumSpeedFraction > maximumSpeedFraction || maximumSpeedFraction > 1f)
                throw new ArgumentException("Skill fractions must lie in (0,1].");
            RaceGeometry.RequirePositive(gateClearanceMetres, nameof(gateClearanceMetres));
            RaceGeometry.RequirePositive(roundingLaneFraction, nameof(roundingLaneFraction));
            if (roundingLaneFraction >= 1f) throw new ArgumentOutOfRangeException(nameof(roundingLaneFraction));
            RaceGeometry.RequirePositive(roundingStepDeg, nameof(roundingStepDeg));
            RaceGeometry.RequirePositive(gateOvershootDeg, nameof(gateOvershootDeg));
            if (roundingStepDeg >= 180f || gateOvershootDeg >= 90f) throw new ArgumentException("Unsafe rounding polygon.");
            if (fixedStepSeconds > course.Rules.MaximumTrackGapSeconds || maximumReplaySeconds < fixedStepSeconds)
                throw new ArgumentException("NPC tick/horizon outside course sampling bounds.");
            if (RaceGeometry.Side(course.Start, start) >= 0f) throw new ArgumentException("NPC must start behind the line.");
            // Validate the polar's axes and speeds more strictly than IsUsable's shape-only check.
            ValidatePolar(polar);
            float maxKn = 0f;
            foreach (float kn in polar.BoatSpeedKn) maxKn = Mathf.Max(maxKn, kn);
            if (SailDrive.ToMetresPerSecond(maxKn) * maximumSpeedFraction * fixedStepSeconds > course.Rules.MaximumTrackSegmentMetres)
                throw new ArgumentException("NPC tick can exceed track distance bound.");
            _id = id; _seed = worldSeed; _gunTime = gunTime; _polar = polar; _noGo = noGoTrueDeg;
            _start = start; _initialBearing = initialBearingDeg; _step = fixedStepSeconds; _horizon = maximumReplaySeconds;
            _headingSamples = headingSamples;
            uint hash = StableHash(id, worldSeed);
            _headingOffset = (int)(hash % (uint)headingSamples);
            _speedFraction = Mathf.Lerp(minimumSpeedFraction, maximumSpeedFraction, (hash & 0xffffff) / (float)0xffffff);
            _route = BuildRoute(course, gateClearanceMetres, roundingLaneFraction, roundingStepDeg, gateOvershootDeg);
        }

        public SailRaceNpcSample At(double gameTime, Func<int, double, Vector2> windAt)
        {
            RaceGeometry.RequireFinite(gameTime, nameof(gameTime));
            if (windAt == null) throw new ArgumentNullException(nameof(windAt));
            if (gameTime - _gunTime > _horizon) throw new ArgumentOutOfRangeException(nameof(gameTime), "Replay horizon exceeded.");
            double initialTime = Math.Min(gameTime, _gunTime);
            var race = new SailRaceEntrant(_id, _course, _gunTime, new RaceTrackSample(initialTime, _start));
            Vector2 position = _start, forward = RaceGeometry.Bearing(_initialBearing), velocity = Vector2.zero;
            int waypoint = 0;
            long tick = 0;
            while (_gunTime + tick * (double)_step < gameTime && !race.Finished)
            {
                double time = _gunTime + tick * (double)_step;
                double end = Math.Min(gameTime, _gunTime + (tick + 1) * (double)_step);
                Vector2 wind = windAt(_seed, time);
                RaceGeometry.RequireFinite(wind, "windAt");
                while (waypoint < _route.Length && position == _route[waypoint].Position)
                {
                    Waypoint reached = _route[waypoint];
                    if ((reached.RequiresStart && !race.Started) || (reached.RequiredLap >= 0 &&
                        (race.CompletedLaps < reached.RequiredLap ||
                        (race.CompletedLaps == reached.RequiredLap && race.NextMarkIndex < reached.RequiredMark))))
                    { waypoint = reached.RetryIndex; break; }
                    waypoint++;
                }
                if (waypoint == _route.Length) break;
                Vector2 delta = _route[waypoint].Position - position;
                velocity = BestVelocity(wind, delta.normalized, out forward);
                // Plan a FULL fixed tick, then interpolate it. A partial query cannot choose a
                // different velocity/cap than a later full-tick query of the same trajectory.
                Vector2 fullMove = velocity * _step;
                if (fullMove.magnitude > delta.magnitude) fullMove *= delta.magnitude / fullMove.magnitude;
                Vector2 move = fullMove * (float)((end - time) / _step);
                velocity = fullMove / _step;
                Vector2 previousPosition = position;
                position += move;
                race.Advance(new RaceTrackSample(end, position));
                if (race.Finished)
                {
                    position = Vector2.LerpUnclamped(previousPosition, position, (float)((race.FinishTime - time) / (end - time)));
                    velocity = Vector2.zero;
                }
                tick++;
            }
            return new SailRaceNpcSample(new RaceBoatMotion(_id, position, forward, velocity), race);
        }

        private Vector2 BestVelocity(Vector2 wind, Vector2 desired, out Vector2 forward)
        {
            forward = desired;
            float bestSpeed = Speed(wind, desired), bestVmg = bestSpeed;
            // Follow the course polygon whenever it is sailable. Optimising VMG on every reach
            // drifts across the buoy gates and can miss an otherwise navigable rounding.
            if (bestSpeed > 0f) return desired * bestSpeed;
            // In the no-go, choose a sailable tack with positive progress toward the waypoint.
            for (int i = 0; i < _headingSamples; i++)
            {
                Vector2 candidate = RaceGeometry.Bearing(((i + _headingOffset) % _headingSamples) * 360f / _headingSamples);
                float speed = Speed(wind, candidate);
                float vmg = Vector2.Dot(candidate, desired) * speed;
                if (vmg <= bestVmg) continue;
                bestVmg = vmg; bestSpeed = speed; forward = candidate;
            }
            return forward * bestSpeed;
        }

        private float Speed(Vector2 wind, Vector2 heading)
        {
            SailWind resolved = SailDrive.ResolveWind(wind, heading, Vector2.zero);
            return SailDrive.ToMetresPerSecond(SailDrive.TargetSpeedKn(_polar, resolved.TrueAngleDeg,
                resolved.TrueKn, _noGo)) * _speedFraction;
        }

        private static Waypoint[] BuildRoute(SailRaceCourse course, float gateClearance, float laneFraction, float step, float overshoot)
        {
            var route = new List<Waypoint>
            {
                new Waypoint(course.Start.Midpoint - course.Start.Forward * gateClearance),
                new Waypoint(course.Start.Midpoint + course.Start.Forward * gateClearance, retry: 0, requiresStart: true)
            };
            for (int lap = 0; lap < course.Laps; lap++)
            {
                for (int i = 0; i < course.MarkCount; i++)
                {
                    RaceMark mark = course.Mark(i);
                    float radius = Mathf.Lerp(mark.ClearanceMetres, mark.ZoneRadiusMetres, laneFraction);
                    int segments = (int)Math.Ceiling((mark.SweepDeg + 2f * overshoot) / step);
                    float arcStep = (mark.SweepDeg + 2f * overshoot) / segments;
                    if (radius * Mathf.Cos(arcStep * 0.5f * Mathf.Deg2Rad) <= mark.ClearanceMetres)
                        throw new ArgumentException("NPC rounding chords cut the buoy clearance.");
                    int retry = route.Count;
                    for (int j = 0; j <= segments; j++)
                    {
                        float bearing = mark.EntryBearingDeg + mark.Direction * (overshoot - j * arcStep);
                        route.Add(new Waypoint(mark.Position + RaceGeometry.Bearing(bearing) * radius,
                            j == segments ? lap : -1, i + 1, retry));
                    }
                }
                int finishRetry = route.Count;
                route.Add(new Waypoint(course.Finish.Midpoint - course.Finish.Forward * gateClearance));
                route.Add(new Waypoint(course.Finish.Midpoint + course.Finish.Forward * gateClearance, lap + 1, 0, finishRetry));
            }
            return route.ToArray();
        }

        private static void ValidatePolar(SailPolarDef polar)
        {
            foreach (var axis in new[] { polar.TrueWindAngleDeg, polar.TrueWindKn })
                for (int i = 0; i < axis.Length; i++)
                {
                    RaceGeometry.RequireNonNegative(axis[i], "polar axis");
                    if (i > 0 && axis[i] <= axis[i - 1]) throw new ArgumentException("Polar axes must increase strictly.");
                }
            if (polar.TrueWindAngleDeg[polar.TrueWindAngleDeg.Length - 1] > 180f)
                throw new ArgumentException("Polar angles exceed 180 degrees.");
            foreach (float speed in polar.BoatSpeedKn) RaceGeometry.RequireNonNegative(speed, "polar speed");
        }

        private static uint StableHash(string id, int seed)
        {
            unchecked
            {
                uint hash = 2166136261u ^ (uint)seed;
                foreach (char c in id) hash = (hash ^ c) * 16777619u;
                return hash;
            }
        }
    }
}
