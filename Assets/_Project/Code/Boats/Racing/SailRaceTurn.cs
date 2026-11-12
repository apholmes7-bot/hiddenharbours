using System;
using UnityEngine;

namespace HiddenHarbours.Boats
{
    /// <summary>One continuous signed rotation per owed turn. Direction reversals restart the sweep.
    /// Queuing/observing and completion allocate nothing. The host suppresses course progress while owed.</summary>
    public sealed class SailRaceTurn
    {
        private readonly SailRaceClubRules _rules;
        private double _lastTime, _turnStart;
        private float _lastHeading, _sweep;
        public int Owed { get; private set; }
        public int Completed { get; private set; }
        public float SweepDegrees => _sweep;

        public SailRaceTurn(SailRaceClubRules rules) { rules.Validate(); _rules = rules; }

        public void Queue(double time, float headingDeg)
        {
            RaceGeometry.RequireFinite(time, nameof(time));
            RaceGeometry.RequireFinite(headingDeg, nameof(headingDeg));
            if (Owed > 0 && time < _lastTime) throw new ArgumentException("Turn observations cannot go backwards.");
            if (Owed == int.MaxValue) throw new InvalidOperationException("Too many owed turns.");
            if (Owed++ == 0)
            { _lastTime = _turnStart = time; _lastHeading = headingDeg; _sweep = 0f; }
        }

        internal void ValidateObservation(double time, float headingDeg)
        {
            RaceGeometry.RequireFinite(time, nameof(time));
            RaceGeometry.RequireFinite(headingDeg, nameof(headingDeg));
            if (Owed == 0) return;
            if (time < _lastTime || (time == _lastTime && Mathf.DeltaAngle(_lastHeading, headingDeg) != 0f) ||
                Mathf.Abs(Mathf.DeltaAngle(_lastHeading, headingDeg)) > _rules.MaximumHeadingStepDegrees)
                throw new ArgumentException("Turn samples must be chronological and resolve the rotation.");
        }

        public bool Advance(double time, float headingDeg)
        {
            ValidateObservation(time, headingDeg);
            if (Owed == 0 || time == _lastTime) return false;
            float delta = Mathf.DeltaAngle(_lastHeading, headingDeg);
            // A reversal begins a new continuous turn at the preceding sample, not at the offence.
            if (delta * _sweep < 0f) { _sweep = 0f; _turnStart = _lastTime; }
            _sweep += delta;
            _lastHeading = headingDeg; _lastTime = time;
            if (Mathf.Abs(_sweep) < _rules.TurnDegrees || time - _turnStart < _rules.MinimumTurnSeconds) return false;
            Owed--; Completed++; _sweep = 0f; _turnStart = time;
            return true;
        }
    }
}
