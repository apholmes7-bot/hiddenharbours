using System;
using System.Collections.Generic;
using HiddenHarbours.Core;

namespace HiddenHarbours.Economy
{
    /// <summary>Manual work at one slip, bound to one loaded save and one tide profile. No scene
    /// dependencies. The placing interaction calls WorkFor only while he works and disposes this
    /// session on leaving play, including a reload of the same save. Partial time is then discarded;
    /// reserved materials remain owned. A replacement session starts the unfinished job afresh.</summary>
    public sealed class DoryMendingSession : IDisposable
    {
        private readonly DoryMendingDef _def;
        private readonly DoryMendingJobDef[] _jobs;
        private readonly ISaveService _save;
        private readonly SaveData _data;
        private readonly IGameClock _clock;
        private readonly IEnvironmentService _tide;
        private readonly IMendingCarry _carry;
        private readonly DoryMendingClaims _claims;
        public string PlayerId { get; }
        private readonly GameConfig _config;
        private readonly TideProfile _place;
        private readonly Func<double, float> _water;
        private readonly double _tideRateBound;
        private readonly float _daySeconds, _tideHours, _lunarDays, _neapFraction;
        private IMendingReservation _reservation;
        private DoryMendingJobDef _active;
        private double _worked;
        private bool _working, _busy, _readyAnnounced, _disposed;
        // Numerical refinement, matching the existing crossing search's bisection depth.
        private const int EdgeBisections = 40;

        public DoryMendingSession(DoryMendingDef def, ISaveService save, IGameClock clock,
                                 IEnvironmentService tide, IMendingCarry carry, GameConfig config,
                                 DoryMendingClaims claims, string playerId)
        {
            _def = def != null ? def : throw new ArgumentNullException(nameof(def));
            def.Validate();
            _save = save ?? throw new ArgumentNullException(nameof(save));
            _data = save.Current ?? throw new ArgumentException("A loaded save is required.");
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _tide = tide ?? throw new ArgumentNullException(nameof(tide));
            _carry = carry ?? throw new ArgumentNullException(nameof(carry));
            _claims = claims ?? throw new ArgumentNullException(nameof(claims));
            if (string.IsNullOrWhiteSpace(playerId)) throw new ArgumentException("A player identity is required.");
            PlayerId = playerId;
            _config = config != null ? config : throw new ArgumentNullException(nameof(config));
            if (!(config.SecondsPerDay > 0) || float.IsInfinity(config.SecondsPerDay) ||
                !(config.TidalPeriodHours > 0) || float.IsInfinity(config.TidalPeriodHours))
                throw new ArgumentException("A finite day and tide period are required.");
            _place = tide.ActiveTideProfile;
            _daySeconds = config.SecondsPerDay;
            _tideHours = config.TidalPeriodHours;
            _lunarDays = config.LunarMonthDays;
            _neapFraction = config.NeapAmplitudeFraction;
            if (!Finite(_place.MeanLevel) || !Finite(_place.Amplitude) || !Finite(_place.PhaseHours) ||
                !Finite(config.LunarMonthDays) || !(config.LunarMonthDays > 0) ||
                !Finite(config.NeapAmplitudeFraction) || config.NeapAmplitudeFraction < 0 ||
                config.NeapAmplitudeFraction > 1)
                throw new ArgumentException("A finite tide profile and lunar envelope are required.");
            // Triangle inequality for the existing tide's sine carrier times its lunar envelope.
            // This bounds change; all actual water heights still come from the environment service.
            double period = (double)config.TidalPeriodHours * config.SecondsPerDay / 24.0;
            double lunarPeriod = (double)config.LunarMonthDays * config.SecondsPerDay;
            _tideRateBound = Math.Abs(_place.Amplitude) * 2 * Math.PI *
                (1 / period + (1.0 - config.NeapAmplitudeFraction) / lunarPeriod);
            _water = tide.TideHeightAt;
            _jobs = (DoryMendingJobDef[])def.Jobs.Clone();
            Array.Sort(_jobs, (a, b) => a.Order.CompareTo(b.Order));
        }

        public DoryMendingSnapshot Snapshot => new DoryMendingSnapshot(
            _def.BoatId, _active != null ? _active.Id : null, _worked,
            _reservation != null && _reservation.IsValid, _working, _active != null ? PlayerId : null);

        public DoryMendingJobDef NextJob
        {
            get
            {
                foreach (var job in _jobs) if (!_save.GetFlag(job.CompletedFlag)) return job;
                return null;
            }
        }

        public DoryMendReason CheckStart(string jobId, string placeId)
        {
            DoryMendReason common = CheckPlaceAndOwnership(placeId);
            if (common != DoryMendReason.None) return common;
            if (RepairLedger.IsRepaired(_data, _def.BoatId)) return DoryMendReason.BoatAlreadyRepaired;
            if (!ProgressIsOrdered()) return DoryMendReason.InvalidProgress;
            DoryMendingJobDef job = Array.Find(_jobs, j => j.Id == jobId);
            if (job == null) return DoryMendReason.UnknownJob;
            if (_save.GetFlag(job.CompletedFlag)) return DoryMendReason.JobAlreadyDone;
            if (job != NextJob) return DoryMendReason.EarlierJobsIncomplete;
            if (_working || _claims.HeldByOther(_def.BoatId, this)) return DoryMendReason.JobInProgress;
            if (_clock.IsHeld || _clock.IsPaused) return DoryMendReason.ClockUnavailable;
            if (job.NeedsLowWater && !LowWater) return DoryMendReason.LowWaterRequired;
            if (_active == null)
                foreach (var material in job.Materials)
                    if (_carry.Count(material.ItemId) < material.Count) return DoryMendReason.MissingMaterials;
            if (_active != null && (_reservation == null || !_reservation.IsValid))
                return DoryMendReason.MissingMaterials;
            return DoryMendReason.None;
        }

        /// <summary>Reserve at first start; debit only when the job completes. Resuming retains the
        /// same reservation and publishes no second start. Unfinished goods remain in saved contents.</summary>
        public bool TryStart(string jobId, string placeId, out DoryMendReason reason)
        {
            reason = CheckStart(jobId, placeId);
            if (reason != DoryMendReason.None) return false;
            bool firstStart = _active == null;
            if (!_claims.TryClaim(_def.BoatId, this))
            { reason = DoryMendReason.JobInProgress; return false; }
            _busy = true;
            try
            {
                if (firstStart)
                {
                    var job = NextJob;
                    if (!_carry.TryReserve((MendingMaterial[])job.Materials.Clone(), out _reservation))
                    { reason = DoryMendReason.MissingMaterials; return false; }
                    reason = CheckPlaceAndOwnership(placeId, duringWork: true);
                    if (reason != DoryMendReason.None) { Dispose(); return false; }
                    _active = job;
                    _worked = 0;
                }
                _working = true;
                if (firstStart) EventBus.Publish(new DoryMendingJobStarted(_def.BoatId, _active.Id, _clock.TotalSeconds, PlayerId));
                return true;
            }
            finally
            {
                if (_active == null) _claims.Release(_def.BoatId, this);
                _busy = false;
            }
        }

        public void Pause() { if (!_busy) _working = false; }

        public void Dispose()
        {
            _disposed = true;
            _reservation?.Dispose();
            _reservation = null;
            _active = null;
            _worked = 0;
            _working = false;
            _claims.Release(_def.BoatId, this);
        }

        /// <summary>Advance the real game clock by the work actually possible. Stops at the first
        /// flood edge or job completion, even when the request spans several tides. No unattended work.</summary>
        public bool WorkFor(double gameMinutes, string placeId, out DoryMendReason reason)
        {
            reason = CheckPlaceAndOwnership(placeId);
            if (reason != DoryMendReason.None) return false;
            if (!(gameMinutes > 0) || double.IsInfinity(gameMinutes))
            { reason = DoryMendReason.InvalidWorkDuration; return false; }
            if (_active == null || !_working) { reason = DoryMendReason.NotStarted; return false; }
            if (_reservation == null || !_reservation.IsValid)
            { _working = false; reason = DoryMendReason.MissingMaterials; return false; }
            if (_clock.IsHeld || _clock.IsPaused) { reason = DoryMendReason.ClockUnavailable; return false; }
            if (RepairLedger.IsRepaired(_data, _def.BoatId))
            { _working = false; reason = DoryMendReason.BoatAlreadyRepaired; return false; }
            if (_active != NextJob || !ProgressIsOrdered())
            { _working = false; reason = DoryMendReason.InvalidProgress; return false; }
            double now = _clock.TotalSeconds;
            double secondsPerMinute = _config.SecondsPerDay / (24.0 * 60.0);
            if (double.IsNaN(now) || double.IsInfinity(now) ||
                !(now + _def.SearchStepGameMinutes * secondsPerMinute > now))
            { reason = DoryMendReason.InvalidProgress; return false; }
            double duration = _active.WorkGameMinutes * secondsPerMinute;
            double amount = Math.Min(gameMinutes * secondsPerMinute, duration - _worked);
            bool interrupted = false;
            if (_active.NeedsLowWater)
            {
                double available = LowWater ? LowWaterSecondsLeft(now, amount) : 0;
                if (!(available > 0) && amount > 0)
                { _working = false; reason = DoryMendReason.LowWaterRequired; return false; }
                if (available < amount) { amount = available; interrupted = true; }
            }
            double target = now + amount;
            if ((amount > 0 && !(target > now)) || double.IsInfinity(target))
            { reason = DoryMendReason.InvalidWorkDuration; return false; }
            _busy = true;
            try
            {
                DoryMendingJobDef done = null;
                bool repaired = false;
                // Clock callbacks may request autosaves. Publish one coherent completion snapshot
                // after clock, goods and flags agree; deferral itself is not rollback.
                using (_save.DeferWrites())
                {
                    if (amount > 0 && !_clock.FastForwardTo(target))
                    { reason = DoryMendReason.ClockUnavailable; return false; }
                    reason = CheckPlaceAndOwnership(placeId, duringWork: true);
                    if (reason != DoryMendReason.None) { Dispose(); return false; }
                    if (_active != NextJob || !ProgressIsOrdered() || RepairLedger.IsRepaired(_data, _def.BoatId))
                    { Dispose(); reason = DoryMendReason.InvalidProgress; return false; }
                    _worked += amount;
                    if (_worked >= duration)
                    {
                        if (!FinishActiveJob(out done, out repaired))
                        { _working = false; reason = DoryMendReason.MissingMaterials; return false; }
                    }
                    else if (interrupted) _working = false;
                }
                if (done != null)
                {
                    EventBus.Publish(new DoryMendingJobDone(_def.BoatId, done.Id, _clock.TotalSeconds, PlayerId));
                    if (repaired)
                    {
                        EventBus.Publish(new BoatRepaired(_def.BoatId, 0));
                        EventBus.Publish(new DoryMended(_def.BoatId, _clock.TotalSeconds));
                    }
                }
                return true;
            }
            finally { _busy = false; }
        }

        public DoryMendReason CheckLaunch(string placeId)
        {
            DoryMendReason common = CheckPlaceAndOwnership(placeId);
            if (common != DoryMendReason.None) return common;
            if (!RepairLedger.IsRepaired(_data, _def.BoatId)) return DoryMendReason.NotRepaired;
            if (_def.RequiresSupportedLaunch) return DoryMendReason.SupportedLaunchRequired;
            return _water(_clock.TotalSeconds) > _def.LaunchAboveMetres
                ? DoryMendReason.None : DoryMendReason.HighWaterRequired;
        }

        /// <summary>Call while the slip is present. Publishes once on entering readiness; a closed
        /// interval rearms the notification. This checks permission, never moves or boards the boat.</summary>
        public void PollLaunch(string placeId)
        {
            if (_busy) return;
            bool ready = CheckLaunch(placeId) == DoryMendReason.None;
            bool publish = ready && !_readyAnnounced;
            _readyAnnounced = ready;
            if (!publish) return;
            _busy = true;
            try { EventBus.Publish(new DoryReadyToLaunch(_def.BoatId, _clock.TotalSeconds)); }
            finally { _busy = false; }
        }

        private bool LowWater => _water(_clock.TotalSeconds) < _def.WorkBelowMetres;

        private double LowWaterSecondsLeft(double now, double requested)
        {
            double step = _def.SearchStepGameMinutes * _config.SecondsPerDay / (24.0 * 60.0);
            double end = now + requested;
            double time = now;
            while (time < end)
            {
                double next = Math.Min(time + step, end);
                if (!(next > time)) return time - now;
                double safe = DryEnd(time, next, _water(time), _water(next), EdgeBisections);
                if (safe < next) return safe - now;
                time = next;
            }
            return requested;
        }

        private double DryEnd(double from, double to, float first, float last, int depth)
        {
            if (!Finite(first) || !Finite(last) || !(first < _def.WorkBelowMetres)) return from;
            double gap = (double)_def.WorkBelowMetres - Math.Max(first, last);
            // Every interior point is within half the span of an endpoint. Reserve half the
            // float gap for endpoint rounding, half for the rate bound. Equal/dry endpoints alone
            // never certify an interval: a complete flood may lie between them.
            if (gap > 0 && _tideRateBound * (to - from) < gap) return to;
            double middle = from + (to - from) * 0.5;
            if (depth == 0 || !(middle > from) || !(middle < to)) return from;
            float height = _water(middle);
            double left = DryEnd(from, middle, first, height, depth - 1);
            return left < middle ? left : DryEnd(middle, to, height, last, depth - 1);
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        private DoryMendReason CheckPlaceAndOwnership(string placeId, bool duringWork = false)
        {
            if (_busy && !duringWork) return DoryMendReason.Busy;
            if (_disposed || !ReferenceEquals(_data, _save.Current))
            { Dispose(); return DoryMendReason.SessionChanged; }
            if (_config.SecondsPerDay != _daySeconds || _config.TidalPeriodHours != _tideHours ||
                _config.LunarMonthDays != _lunarDays || _config.NeapAmplitudeFraction != _neapFraction)
            { Dispose(); return DoryMendReason.SessionChanged; }
            TideProfile current = _tide.ActiveTideProfile;
            if (placeId != _def.PlaceId || current.MeanLevel != _place.MeanLevel ||
                current.Amplitude != _place.Amplitude || current.PhaseHours != _place.PhaseHours)
                return DoryMendReason.WrongPlace;
            return _data.OwnedBoats != null && _data.OwnedBoats.Contains(_def.BoatId)
                ? DoryMendReason.None : DoryMendReason.BoatNotOwned;
        }

        private bool ProgressIsOrdered()
        {
            bool sawUnfinished = false;
            foreach (var job in _jobs)
            {
                if (!_save.GetFlag(job.CompletedFlag)) sawUnfinished = true;
                else if (sawUnfinished) return false;
            }
            return true;
        }

        private bool FinishActiveJob(out DoryMendingJobDef done, out bool repaired)
        {
            done = null;
            repaired = false;
            // Prepare all allocations before the material debit. The commit below then changes
            // only prepared in-memory state before any save or event callback can observe it.
            var flags = _data.OnboardingFlags == null ? new List<SaveFlag>() : new List<SaveFlag>(_data.OnboardingFlags);
            int index = flags.FindIndex(flag => flag.Key == _active.CompletedFlag);
            var completed = new SaveFlag(_active.CompletedFlag, true);
            if (index < 0) flags.Add(completed); else flags[index] = completed;
            bool final = _active == _jobs[_jobs.Length - 1];
            var boats = _data.RepairedBoats == null ? new List<string>() : new List<string>(_data.RepairedBoats);
            if (final && !RepairLedger.IsRepaired(_data, _def.BoatId)) boats.Add(_def.BoatId);
            if (!_reservation.IsValid || !_reservation.TryConsume()) return false;
            done = _active;
            repaired = final;
            _data.OnboardingFlags = flags;
            if (final) _data.RepairedBoats = boats;
            _active = null;
            _worked = 0;
            _working = false;
            _reservation.Dispose();
            _reservation = null;
            _claims.Release(_def.BoatId, this);
            _save.Save();
            return true;
        }
    }
}
