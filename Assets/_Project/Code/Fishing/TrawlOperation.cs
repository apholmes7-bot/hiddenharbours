using System;

namespace HiddenHarbours.Fishing
{
    public enum TrawlPhase { Stowed, Shooting, Towing, Hauling, CodendOnDeck }

    /// <summary>Command model only. Does not move a boat, gear, occupant, goods or an inventory.</summary>
    public sealed class TrawlOperation
    {
        private readonly TrawlHandlingDef _handling;
        private double _lastTime, _dueTime;
        public TrawlPhase Phase { get; private set; }
        public double TowStartGameSeconds { get; private set; }
        public double TowEndGameSeconds { get; private set; }

        public TrawlOperation(in TrawlHandlingDef handling)
        {
            if (!handling.IsValid) throw new ArgumentException("Invalid trawl handling definition.", nameof(handling));
            _handling = handling;
        }

        public bool BeginShooting(double gameSeconds, double speed, double seaState01, TrawlWorkStation station)
        {
            if (Phase != TrawlPhase.Stowed || !TimeAllowed(gameSeconds)
                || !WorkAllowed(speed, seaState01, station, _handling.MaxShootingMetresPerGameSecond)
                || !Deadline(gameSeconds, _handling.ShootingGameSeconds, out double due)) return false;
            _lastTime = gameSeconds; _dueTime = due; Phase = TrawlPhase.Shooting;
            return true;
        }

        public bool CompleteShooting(double gameSeconds)
        {
            if (Phase != TrawlPhase.Shooting || !TimeAllowed(gameSeconds) || gameSeconds < _dueTime) return false;
            TowStartGameSeconds = _dueTime; // A late poll never extends the shooting job.
            _lastTime = gameSeconds; Phase = TrawlPhase.Towing;
            return true;
        }

        public bool BeginHauling(double gameSeconds, double speed, double seaState01, TrawlWorkStation station)
        {
            if (Phase != TrawlPhase.Towing || !TimeAllowed(gameSeconds) || gameSeconds <= TowStartGameSeconds
                || !WorkAllowed(speed, seaState01, station, _handling.MaxHaulingMetresPerGameSecond)
                || !Deadline(gameSeconds, _handling.HaulingGameSeconds, out double due)) return false;
            TowEndGameSeconds = gameSeconds;
            _lastTime = gameSeconds; _dueTime = due; Phase = TrawlPhase.Hauling;
            return true;
        }

        public bool CompleteHauling(double gameSeconds)
        {
            if (Phase != TrawlPhase.Hauling || !TimeAllowed(gameSeconds) || gameSeconds < _dueTime) return false;
            _lastTime = gameSeconds; Phase = TrawlPhase.CodendOnDeck;
            return true;
        }

        private bool TimeAllowed(double time) => TrawlNumbers.NonNegative(time) && time >= _lastTime;
        private bool WorkAllowed(double speed, double sea, TrawlWorkStation station, double maxSpeed)
            => TrawlNumbers.NonNegative(speed) && speed <= maxSpeed && TrawlNumbers.Unit(sea)
                && sea <= _handling.MaxWorkingSeaState01 && station == _handling.WorkStation;
        private static bool Deadline(double time, double duration, out double due)
        { due = time + duration; return TrawlNumbers.Finite(due) && due > time; }
    }
}
