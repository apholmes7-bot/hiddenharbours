using System;
using System.Collections.Generic;
using UnityEngine;

namespace HiddenHarbours.Boats
{
    public readonly struct RaceTrackSample
    {
        public readonly double Time;
        public readonly Vector2 Position;
        public RaceTrackSample(double time, Vector2 position)
        { RaceGeometry.RequireFinite(time, nameof(time)); RaceGeometry.RequireFinite(position, nameof(position)); Time = time; Position = position; }
    }

    /// <summary>Track replay for one entrant. No engine objects, clock read, save, input or scene dependency.</summary>
    public sealed class SailRaceEntrant
    {
        private readonly SailRaceCourse _course;
        private readonly SailRaceRounding _rounding = new SailRaceRounding();
        private readonly List<double> _lapElapsed = new List<double>();
        private readonly HashSet<(string, double)> _penalties = new HashSet<(string, double)>();
        private RaceTrackSample _last;
        private double _lastLapTime;
        private bool _gunObserved;
        public string Id { get; }
        public double GunTime { get; }
        public bool OverEarly { get; private set; }
        public bool Started { get; private set; }
        public int NextMarkIndex { get; private set; }
        public int CompletedLaps => _lapElapsed.Count;
        public double StartTime { get; private set; } = double.NaN;
        public double FinishTime { get; private set; } = double.NaN;
        public bool Finished => !double.IsNaN(FinishTime);
        public double PenaltySeconds { get; private set; }
        public double ElapsedSeconds => Finished ? FinishTime - GunTime : double.NaN;
        public double CorrectedSeconds => ElapsedSeconds + PenaltySeconds;
        public RaceTrackSample LastSample => _last;
        public double LapElapsedSeconds(int lap) => _lapElapsed[lap];

        public SailRaceEntrant(string id, SailRaceCourse course, double gunTime, RaceTrackSample initial)
        {
            RaceGeometry.RequireId(id, "racer.");
            RaceGeometry.RequireFinite(gunTime, nameof(gunTime));
            _course = course ?? throw new ArgumentNullException(nameof(course));
            if (initial.Time > gunTime) throw new ArgumentException("Replay needs a sample at or before the gun.");
            Id = id; GunTime = gunTime; _last = initial; _lastLapTime = gunTime;
            if (initial.Time == gunTime) ObserveGun(initial.Position);
        }

        private void ObserveGun(Vector2 position)
        { _gunObserved = true; OverEarly = RaceGeometry.Side(_course.Start, position) > 0f; }

        public void Advance(RaceTrackSample sample)
        {
            // Reject discontinuities before mutating ANY progress, including a completed entrant.
            double dt = sample.Time - _last.Time;
            if (dt <= 0 || dt > _course.Rules.MaximumTrackGapSeconds ||
                Vector2.Distance(_last.Position, sample.Position) > _course.Rules.MaximumTrackSegmentMetres)
                throw new ArgumentException("Track must be increasing, continuous and within authored sampling bounds.");
            var from = _last;
            _last = sample;
            if (Finished || sample.Time < GunTime) return;
            if (!_gunObserved)
            {
                float atGun = (float)((GunTime - from.Time) / dt);
                ObserveGun(Vector2.LerpUnclamped(from.Position, sample.Position, atGun));
                from = new RaceTrackSample(GunTime, Vector2.LerpUnclamped(from.Position, sample.Position, atGun));
            }
            if (!Started)
            {
                if (OverEarly)
                {
                    if (RaceGeometry.Side(_course.Start, sample.Position) < 0f) OverEarly = false;
                    return;
                }
                if (!RaceGeometry.Crosses(_course.Start, from.Position, sample.Position, out float startFraction)) return;
                StartTime = from.Time + (sample.Time - from.Time) * startFraction;
                Started = true;
                from = new RaceTrackSample(StartTime, Vector2.LerpUnclamped(from.Position, sample.Position, startFraction));
            }
            // Consume remaining geometry in order. One segment may legally cross successive gates;
            // nothing before the last credited event can count toward the next one.
            while (true)
            {
                float fraction;
                if (NextMarkIndex < _course.MarkCount)
                {
                    if (!_rounding.Advance(_course.Mark(NextMarkIndex), from.Position, sample.Position, out fraction)) return;
                    NextMarkIndex++;
                }
                else
                {
                    if (!RaceGeometry.Crosses(_course.Finish, from.Position, sample.Position, out fraction)) return;
                    double crossing = from.Time + (sample.Time - from.Time) * fraction;
                    _lapElapsed.Add(crossing - _lastLapTime);
                    _lastLapTime = crossing;
                    if (CompletedLaps == _course.Laps) { FinishTime = crossing; return; }
                    NextMarkIndex = 0;
                }
                double time = from.Time + (sample.Time - from.Time) * fraction;
                Vector2 point = Vector2.LerpUnclamped(from.Position, sample.Position, fraction);
                // No zero-length remainder can prove another crossing.
                if (fraction >= 1f) return;
                from = new RaceTrackSample(time, point);
            }
        }

        public bool ApplyPenalty(RacePenalty penalty)
        {
            if (penalty.OffenderId != Id || penalty.Time < GunTime || (Finished && penalty.Time > FinishTime)) return false;
            if (!_penalties.Add((penalty.OtherId, penalty.Time))) return false;
            PenaltySeconds += penalty.Seconds;
            return true;
        }
    }
}
