using System;
using HiddenHarbours.Core;

namespace HiddenHarbours.Economy
{
    /// <summary>Manual work at one slip, bound to one loaded save and one tide profile. No scene
    /// dependencies. The placing interaction owns this session and calls WorkFor only while he works.</summary>
    public sealed class DoryMendingSession
    {
        private readonly DoryMendingDef _def;
        private readonly DoryMendingJobDef[] _jobs;
        private readonly ISaveService _save;
        private readonly SaveData _data;
        private readonly IGameClock _clock;
        private readonly IEnvironmentService _tide;
        private readonly IMendingCarry _carry;
        private readonly GameConfig _config;
        private readonly TideProfile _place;
        private readonly Func<double, float> _water;
        private DoryMendingJobDef _active;
        private double _worked;
        private bool _working, _busy, _readyAnnounced;

        public DoryMendingSession(DoryMendingDef def, ISaveService save, IGameClock clock,
                                 IEnvironmentService tide, IMendingCarry carry, GameConfig config)
        {
            _def = def != null ? def : throw new ArgumentNullException(nameof(def));
            def.Validate();
            _save = save ?? throw new ArgumentNullException(nameof(save));
            _data = save.Current ?? throw new ArgumentException("A loaded save is required.");
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _tide = tide ?? throw new ArgumentNullException(nameof(tide));
            _carry = carry ?? throw new ArgumentNullException(nameof(carry));
            _config = config != null ? config : throw new ArgumentNullException(nameof(config));
            if (!(config.SecondsPerDay > 0) || float.IsInfinity(config.SecondsPerDay) ||
                !(config.TidalPeriodHours > 0) || float.IsInfinity(config.TidalPeriodHours))
                throw new ArgumentException("A finite day and tide period are required.");
            _place = tide.ActiveTideProfile;
            _water = tide.TideHeightAt;
            _jobs = (DoryMendingJobDef[])def.Jobs.Clone();
            Array.Sort(_jobs, (a, b) => a.Order.CompareTo(b.Order));
        }

        public DoryMendingSnapshot Snapshot => new DoryMendingSnapshot(
            _def.BoatId, _active != null ? _active.Id : null, _worked, _active != null, _working);

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
            if (_working) return DoryMendReason.JobInProgress;
            if (_clock.IsHeld || _clock.IsPaused) return DoryMendReason.ClockUnavailable;
            if (job.NeedsLowWater && !LowWater) return DoryMendReason.LowWaterRequired;
            if (_active == null)
                foreach (var material in job.Materials)
                    if (_carry.Count(material.ItemId) < material.Count) return DoryMendReason.MissingMaterials;
            return DoryMendReason.None;
        }

        /// <summary>Materials are applied once, at first start. Resuming after water interrupts the
        /// work uses the same committed materials and publishes no second start.</summary>
        public bool TryStart(string jobId, string placeId, out DoryMendReason reason)
        {
            reason = CheckStart(jobId, placeId);
            if (reason != DoryMendReason.None) return false;
            bool firstStart = _active == null;
            _busy = true;
            try
            {
                if (firstStart)
                {
                    var job = NextJob;
                    if (!_carry.TryConsume(job.Materials))
                    { reason = DoryMendReason.MissingMaterials; return false; }
                    _active = job;
                    _worked = 0;
                }
                _working = true;
                if (firstStart) EventBus.Publish(new DoryMendingJobStarted(_def.BoatId, _active.Id, _clock.TotalSeconds));
                return true;
            }
            finally { _busy = false; }
        }

        public void Pause() { if (!_busy) _working = false; }

        /// <summary>Advance the real game clock by the work actually possible. Stops at the first
        /// flood edge or job completion, even when the request spans several tides. No unattended work.</summary>
        public bool WorkFor(double gameMinutes, string placeId, out DoryMendReason reason)
        {
            reason = CheckPlaceAndOwnership(placeId);
            if (reason != DoryMendReason.None) return false;
            if (!(gameMinutes > 0) || double.IsInfinity(gameMinutes))
            { reason = DoryMendReason.InvalidWorkDuration; return false; }
            if (_active == null || !_working) { reason = DoryMendReason.NotStarted; return false; }
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
                double available = LowWater ? LowWaterSecondsLeft(now) : 0;
                if (!(available > 0))
                { _working = false; reason = DoryMendReason.LowWaterRequired; return false; }
                if (available < amount) { amount = available; interrupted = true; }
            }
            double target = now + amount;
            if (!(target > now) || double.IsInfinity(target))
            { reason = DoryMendReason.InvalidWorkDuration; return false; }
            _busy = true;
            try
            {
                if (!_clock.FastForwardTo(target))
                { reason = DoryMendReason.ClockUnavailable; return false; }
                if (!ReferenceEquals(_data, _save.Current))
                { _working = false; reason = DoryMendReason.SessionChanged; return false; }
                _worked += amount;
                if (_worked >= duration) FinishActiveJob();
                else if (interrupted) _working = false;
                return true;
            }
            finally { _busy = false; }
        }

        public DoryMendReason CheckLaunch(string placeId)
        {
            DoryMendReason common = CheckPlaceAndOwnership(placeId);
            if (common != DoryMendReason.None) return common;
            if (!RepairLedger.IsRepaired(_data, _def.BoatId)) return DoryMendReason.NotRepaired;
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

        private double LowWaterSecondsLeft(double now)
        {
            double period = _config.TidalPeriodHours * _config.SecondsPerDay / 24.0;
            double step = _def.SearchStepGameMinutes * _config.SecondsPerDay / (24.0 * 60.0);
            var windows = CrossingMath.FindWindows(_water, _def.WorkBelowMetres,
                                                   now - period, now + period, step, _config);
            foreach (var window in windows)
                if (window.StartSeconds <= now && now < window.EndSeconds) return window.EndSeconds - now;
            return 0;
        }

        private DoryMendReason CheckPlaceAndOwnership(string placeId)
        {
            if (_busy) return DoryMendReason.Busy;
            if (!ReferenceEquals(_data, _save.Current)) return DoryMendReason.SessionChanged;
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

        private void FinishActiveJob()
        {
            var done = _active;
            bool repaired = false;
            using (_save.DeferWrites())
            {
                if (done == _jobs[_jobs.Length - 1]) repaired = RepairLedger.MarkRepaired(_data, _def.BoatId);
                _save.SetFlag(done.CompletedFlag, true);
                _active = null;
                _worked = 0;
                _working = false;
            }
            EventBus.Publish(new DoryMendingJobDone(_def.BoatId, done.Id, _clock.TotalSeconds));
            if (!repaired) return;
            EventBus.Publish(new BoatRepaired(_def.BoatId, 0));
            EventBus.Publish(new DoryMended(_def.BoatId, _clock.TotalSeconds));
        }
    }
}
