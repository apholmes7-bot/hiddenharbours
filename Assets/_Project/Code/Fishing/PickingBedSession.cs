using System;
using System.Collections.Generic;
using HiddenHarbours.Core;
using UnityEngine;

namespace HiddenHarbours.Fishing
{
    public enum PickingBedStatus
    {
        Ready, Picked, ReleasedUnlicensed, Covered, Empty, WrongRegion, OutOfReach,
        InvalidTime, TideUnavailable, LicenceServiceUnavailable, HandsUnavailable, HandsFull, Busy,
    }

    public readonly struct PickingBedCheck
    {
        public readonly PickingBedStatus Status;
        public readonly int Remaining;
        public bool CanPick => Remaining > 0
            && (Status == PickingBedStatus.Ready || Status == PickingBedStatus.ReleasedUnlicensed);
        public bool CanKeep => Remaining > 0 && Status == PickingBedStatus.Ready;
        public bool Kept => Status == PickingBedStatus.Picked;

        public string Reason => Status switch
        {
            PickingBedStatus.Ready => "The bed is uncovered and there are shellfish to pick.",
            PickingBedStatus.Picked => "The shellfish is in his hand.",
            PickingBedStatus.ReleasedUnlicensed => "He has no licence for this shellfish, so it goes back.",
            PickingBedStatus.Covered => "The tide is covering this bed.",
            PickingBedStatus.Empty => "This bed needs time to grow back.",
            PickingBedStatus.WrongRegion => "This bed is on another shore.",
            PickingBedStatus.OutOfReach => "He needs to stand closer to the bed.",
            PickingBedStatus.InvalidTime => "The game time is invalid or earlier than the last pick.",
            PickingBedStatus.TideUnavailable => "The tide for this shore is unavailable.",
            PickingBedStatus.LicenceServiceUnavailable => "His licences are unavailable.",
            PickingBedStatus.HandsUnavailable => "His hands are unavailable.",
            PickingBedStatus.HandsFull => "He needs a free hand for the shellfish.",
            _ => "A pick is already in progress.",
        };

        internal PickingBedCheck(PickingBedStatus status, int remaining)
        {
            Status = status;
            Remaining = remaining;
        }
    }

    /// <summary>A detached view of session removals. This is not part of the save format.</summary>
    public sealed class PickingBedSnapshot
    {
        public string BedId { get; }
        public int WorldSeed { get; }
        public double AtSeconds { get; }
        public double LastPickSeconds { get; }
        public int Remaining { get; }
        public IReadOnlyList<double> TakenAtSeconds { get; }

        internal PickingBedSnapshot(string bedId, int worldSeed, double atSeconds,
            double lastPickSeconds, int remaining, double[] takenAtSeconds)
        {
            BedId = bedId;
            WorldSeed = worldSeed;
            AtSeconds = atSeconds;
            LastPickSeconds = lastPickSeconds;
            Remaining = remaining;
            TakenAtSeconds = Array.AsReadOnly(takenAtSeconds);
        }
    }

    /// <summary>
    /// Keep one instance per bed for the whole world session, including region exits. Initially full;
    /// each kept shellfish returns after RegrowDays. Queries do not advance or refill stored state.
    /// Stock has no random component; catch weights use the world seed, bed id, time and removal index.
    /// The caller supplies the active region's Core tide service and the configured seconds per day.
    /// No frame ticks, wall clock, inventory fallback or save writes are involved.
    /// </summary>
    public sealed class PickingBedSession
    {
        private readonly string _id, _regionId;
        private readonly Vector2 _position;
        private readonly int _capacity, _worldSeed;
        private readonly double _regrowSeconds, _reachMetres;
        private readonly float _uncoveredBelow;
        private readonly FishSpeciesDef _species;
        private readonly IEnvironmentService _environment;
        private readonly List<double> _takenAt = new();
        private double _lastPickSeconds;
        private bool _picking;

        public PickingBedSession(PickingBedDef bed, IEnvironmentService environment, double secondsPerDay)
        {
            if (bed == null || !bed.HasValidTuning)
                throw new ArgumentException("A valid picking bed is required.", nameof(bed));
            if (!Finite(secondsPerDay) || secondsPerDay <= 0d
                || !Finite(secondsPerDay * bed.RegrowDays) || secondsPerDay * bed.RegrowDays <= 0d)
                throw new ArgumentOutOfRangeException(nameof(secondsPerDay));
            _id = bed.Id;
            _regionId = bed.RegionId;
            _position = bed.Position;
            _capacity = bed.Capacity;
            _regrowSeconds = secondsPerDay * bed.RegrowDays;
            _reachMetres = bed.ReachMetres;
            _uncoveredBelow = bed.UncoveredBelowMetres;
            _species = bed.Species;
            _environment = environment;
            _worldSeed = environment?.WorldSeed ?? 0;
        }

        public PickingBedCheck Check(string activeRegionId, Vector2 playerPosition, double gameTimeSeconds)
        {
            if (!ValidTime(gameTimeSeconds)) return new PickingBedCheck(PickingBedStatus.InvalidTime, 0);
            int remaining = RemainingAt(gameTimeSeconds);
            if (_picking) return new PickingBedCheck(PickingBedStatus.Busy, remaining);
            if (!string.Equals(activeRegionId, _regionId, StringComparison.Ordinal))
                return new PickingBedCheck(PickingBedStatus.WrongRegion, remaining);
            double dx = (double)playerPosition.x - _position.x, dy = (double)playerPosition.y - _position.y;
            if (!Finite(dx) || !Finite(dy) || dx * dx + dy * dy > _reachMetres * _reachMetres)
                return new PickingBedCheck(PickingBedStatus.OutOfReach, remaining);
            if (_environment == null || _environment.WorldSeed != _worldSeed)
                return new PickingBedCheck(PickingBedStatus.TideUnavailable, remaining);
            float water = _environment.WaterLevelAt(gameTimeSeconds);
            if (!Finite(water)) return new PickingBedCheck(PickingBedStatus.TideUnavailable, remaining);
            if (water >= _uncoveredBelow)
                return new PickingBedCheck(PickingBedStatus.Covered, remaining);
            if (remaining == 0) return new PickingBedCheck(PickingBedStatus.Empty, remaining);
            if (GameServices.Licenses == null)
                return new PickingBedCheck(PickingBedStatus.LicenceServiceUnavailable, remaining);
            return new PickingBedCheck(UnlicensedCatch.MustRelease(_species.Id)
                ? PickingBedStatus.ReleasedUnlicensed : PickingBedStatus.Ready, remaining);
        }

        /// <summary>One attempt, one shellfish. The hands own CatchLanded; no hold event is emitted here.</summary>
        public PickingBedCheck TryPick(string activeRegionId, Vector2 playerPosition,
            double gameTimeSeconds, ICatchHands hands)
        {
            PickingBedCheck check = Check(activeRegionId, playerPosition, gameTimeSeconds);
            if (!check.CanPick) return check;
            _picking = true;
            try
            {
                uint seed = unchecked((uint)StableHash.CatchSeed(_worldSeed, _id, gameTimeSeconds));
                seed = StableHash.Finalize(StableHash.Fold(seed, _capacity - check.Remaining));
                float weight = CatchResolver.RollWeight(_species, new System.Random(unchecked((int)seed)));
                var item = new CatchItem(_species.Id, _species.DisplayName, _species.Category, weight,
                    _species.BaseValue, _species.SupplyElasticity, _species.SpoilPerDay,
                    Freshness.Landed(gameTimeSeconds));
                if (check.Status == PickingBedStatus.ReleasedUnlicensed)
                {
                    UnlicensedCatch.ReleaseAndSay(in item);
                    return check;
                }
                if (hands == null) return new PickingBedCheck(PickingBedStatus.HandsUnavailable, check.Remaining);
                if (!hands.TryPutInHand(in item))
                    return new PickingBedCheck(PickingBedStatus.HandsFull, check.Remaining);

                // Bound the retained history by capacity. Failed attempts and reads never prune it.
                _takenAt.RemoveAll(taken => gameTimeSeconds - taken >= _regrowSeconds);
                _takenAt.Add(gameTimeSeconds);
                _lastPickSeconds = gameTimeSeconds;
                return new PickingBedCheck(PickingBedStatus.Picked, check.Remaining - 1);
            }
            finally { _picking = false; }
        }

        public int RemainingAt(double gameTimeSeconds)
        {
            if (!ValidTime(gameTimeSeconds)) throw new ArgumentOutOfRangeException(nameof(gameTimeSeconds));
            int remaining = _capacity;
            for (int i = 0; i < _takenAt.Count; i++)
                if (gameTimeSeconds - _takenAt[i] < _regrowSeconds) remaining--;
            return remaining;
        }

        public PickingBedSnapshot SnapshotAt(double gameTimeSeconds)
        {
            int remaining = RemainingAt(gameTimeSeconds);
            var active = new List<double>();
            foreach (double taken in _takenAt)
                if (gameTimeSeconds - taken < _regrowSeconds) active.Add(taken);
            return new PickingBedSnapshot(_id, _worldSeed, gameTimeSeconds,
                _lastPickSeconds, remaining, active.ToArray());
        }

        private bool ValidTime(double time) => Finite(time) && time >= _lastPickSeconds;
        private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
