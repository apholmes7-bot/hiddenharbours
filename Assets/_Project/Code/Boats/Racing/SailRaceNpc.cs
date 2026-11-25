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

        public Skipper CreateSkipper(IRaceWindSource wind, SailRaceSkipperRules rules) => new Skipper(this, wind, rules);

        /// <summary>Reusable incremental course runner. One Tick advances one complete authored step,
        /// anchored at the gun. The existing At replay retains its original navigation/query contract.
        /// Polar/profile inputs must remain unchanged for this runner's lifetime.</summary>
        public sealed class Skipper
        {
            private readonly SailRaceNpc _model;
            private readonly IRaceWindSource _wind;
            private readonly SailRaceSkipperRules _rules;
            private Vector2 _position, _forward, _velocity;
            private int _waypoint, _tack;
            private long _tick;
            private double _lastTack = double.NegativeInfinity;
            private float _windFromAtTack;
            public SailRaceEntrant Race { get; }
            public int TackCount { get; private set; }
            public int HeaderTackCount { get; private set; }
            public double Time => _model._gunTime + _tick * (double)_model._step;
            public RaceBoatMotion Motion => new RaceBoatMotion(_model._id, _position, _forward, _velocity);

            internal Skipper(SailRaceNpc model, IRaceWindSource wind, SailRaceSkipperRules rules)
            {
                rules.Validate(); _model = model; _wind = wind ?? throw new ArgumentNullException(nameof(wind)); _rules = rules;
                _position = model._start; _forward = RaceGeometry.Bearing(model._initialBearing);
                Race = new SailRaceEntrant(model._id, model._course, model._gunTime, new RaceTrackSample(model._gunTime, _position));
            }

            public SailRaceNpcSample Tick()
            {
                if (Race.Finished) return new SailRaceNpcSample(Motion, Race);
                double time = Time, end = _model._gunTime + (_tick + 1) * (double)_model._step;
                if (end - _model._gunTime > _model._horizon) throw new InvalidOperationException("Skipper horizon exceeded.");
                Vector2 wind = _wind.WindAt(_model._seed, time);
                RaceGeometry.RequireFinite(wind, nameof(wind));
                while (_waypoint < _model._route.Length && _position == _model._route[_waypoint].Position)
                {
                    Waypoint reached = _model._route[_waypoint];
                    if ((reached.RequiresStart && !Race.Started) || (reached.RequiredLap >= 0 &&
                        (Race.CompletedLaps < reached.RequiredLap ||
                        (Race.CompletedLaps == reached.RequiredLap && Race.NextMarkIndex < reached.RequiredMark))))
                    { _waypoint = reached.RetryIndex; break; }
                    _waypoint++;
                }
                if (_waypoint == _model._route.Length) throw new InvalidOperationException("Route ended before finish.");
                Vector2 delta = _model._route[_waypoint].Position - _position;
                _velocity = ChooseVelocity(wind, delta, time);
                Vector2 move = _velocity * _model._step;
                if (move.magnitude > delta.magnitude) move *= delta.magnitude / move.magnitude;
                _velocity = move / _model._step;
                Vector2 previous = _position;
                _position += move;
                Race.Advance(new RaceTrackSample(end, _position));
                if (Race.Finished)
                {
                    _position = Vector2.LerpUnclamped(previous, _position, (float)((Race.FinishTime - time) / (end - time)));
                    _velocity = Vector2.zero;
                }
                _tick++;
                return new SailRaceNpcSample(Motion, Race);
            }

            private Vector2 ChooseVelocity(Vector2 wind, Vector2 delta, double time)
            {
                if (wind.magnitude <= _model._course.Rules.CalmWindMetresPerSecond || delta.sqrMagnitude == 0f)
                    return Vector2.zero;
                Vector2 desired = delta.normalized;
                float directSpeed = _model.Speed(wind, desired);
                int directTack = Tack(wind, desired);
                if (directSpeed > 0f && (_tack == 0 || directTack == _tack ||
                    time - _lastTack >= _rules.MinimumTackSeconds))
                {
                    SelectTack(directTack, wind, time, false);
                    _forward = desired;
                    return desired * directSpeed;
                }

                // Find the polar's best close-hauled upwind VMG. Search relative to THIS wind,
                // so a shifted wind never leaves a selected moving heading inside the no-go.
                float from = BoatKinematics.BearingDegrees(-wind), bestVmg = 0f, angle = 0f;
                for (int i = 0; i < _model._headingSamples; i++)
                {
                    float candidateAngle = _model._noGo + 0.001f + (90f - _model._noGo - 0.001f) * i / _model._headingSamples;
                    if (candidateAngle >= 90f) continue;
                    Vector2 candidate = RaceGeometry.Bearing(from + candidateAngle);
                    float vmg = _model.Speed(wind, candidate) * Mathf.Cos(candidateAngle * Mathf.Deg2Rad);
                    if (vmg > bestVmg) { bestVmg = vmg; angle = candidateAngle; }
                }
                if (bestVmg == 0f) return Vector2.zero;
                // Positive relative true angle means starboard: heading counterclockwise from wind-from.
                Vector2 starboard = RaceGeometry.Bearing(from - angle), port = RaceGeometry.Bearing(from + angle);
                float starVmg = Vector2.Dot(starboard, desired) * _model.Speed(wind, starboard);
                float portVmg = Vector2.Dot(port, desired) * _model.Speed(wind, port);
                int chosen = _tack;
                bool headerTack = false;
                if (chosen == 0)
                    chosen = starVmg == portVmg ? ((_model._headingOffset & 1) == 0 ? 1 : -1) : starVmg > portVmg ? 1 : -1;
                else if (time - _lastTack >= _rules.MinimumTackSeconds)
                {
                    Vector2 current = chosen > 0 ? starboard : port, other = chosen > 0 ? port : starboard;
                    float currentVmg = chosen > 0 ? starVmg : portVmg, otherVmg = chosen > 0 ? portVmg : starVmg;
                    float denominator = RaceGeometry.Cross(current, other);
                    float remaining = denominator == 0f ? float.PositiveInfinity : RaceGeometry.Cross(delta, other) / denominator;
                    float headerDegrees = -chosen * BoatKinematics.RelativeBearingDegrees(_windFromAtTack, from);
                    bool header = headerDegrees >= _rules.HeaderDegrees && otherVmg > currentVmg;
                    if ((remaining <= _rules.LaylineMarginMetres || header) && otherVmg > 0f)
                    { chosen = -chosen; headerTack = header; }
                }
                Vector2 heading = chosen > 0 ? starboard : port;
                if (Vector2.Dot(heading, desired) <= 0f) return Vector2.zero;
                SelectTack(chosen, wind, time, headerTack);
                _forward = heading;
                return heading * _model.Speed(wind, heading);
            }

            private static int Tack(Vector2 wind, Vector2 heading)
            {
                float angle = SailDrive.ResolveWind(wind, heading, Vector2.zero).TrueAngleDeg;
                return angle == 0f || Mathf.Abs(angle) == 180f ? 0 : angle > 0f ? 1 : -1;
            }

            private void SelectTack(int tack, Vector2 wind, double time, bool header)
            {
                if (tack == 0 || tack == _tack) return;
                if (_tack != 0) { TackCount++; if (header) HeaderTackCount++; }
                _tack = tack; _lastTack = time;
                _windFromAtTack = BoatKinematics.BearingDegrees(-wind);
            }
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
