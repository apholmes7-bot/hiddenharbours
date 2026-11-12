using System;
using HiddenHarbours.Core;
using UnityEngine;

namespace HiddenHarbours.Boats
{
    public readonly struct SailRaceEntry
    {
        public readonly string RacerId;
        public readonly SailRaceHandicap Handicap;
        public readonly RaceTrackSample Initial;
        public readonly Vector2 Forward;
        public readonly float OverlapLengthMetres;
        public SailRaceEntry(string racerId, SailRaceHandicap handicap, RaceTrackSample initial, Vector2 forward,
            float overlapLengthMetres)
        {
            _ = new SailRaceClubBoat(new RaceBoatMotion(racerId, initial.Position, forward, Vector2.zero), 0, 0, overlapLengthMetres);
            _ = new SailRaceHandicap(handicap.HullId, handicap.TimeMultiplier);
            RacerId = racerId; Handicap = handicap; Initial = initial; Forward = forward; OverlapLengthMetres = overlapLengthMetres;
        }
    }

    /// <summary>Fixed roster, sampled club race. Setup owns arrays; valid ticks, first contact,
    /// penalty turns and lap completions allocate zero. No presentation or persistence is wired.</summary>
    public sealed class SailRaceSession
    {
        private readonly SailRaceCourse _course;
        private readonly SailRaceEntry[] _entries;
        private readonly SailRaceEntrant[] _racers;
        private readonly SailRaceTurn[] _turns;
        private readonly bool[] _retired, _contacts, _blocked;
        private readonly double _gun;
        private double _lastTime;
        public int Count => _racers.Length;
        public SailRaceSession(SailRaceCourse course, SailRaceClubRules rules, double gunTime, SailRaceEntry[] entries)
        {
            _course = course ?? throw new ArgumentNullException(nameof(course)); rules.Validate();
            RaceGeometry.RequireFinite(gunTime, nameof(gunTime));
            if (entries == null || entries.Length == 0) throw new ArgumentException("Register a nonempty roster.");
            _entries = (SailRaceEntry[])entries.Clone(); _gun = gunTime; _lastTime = entries[0].Initial.Time;
            _racers = new SailRaceEntrant[entries.Length]; _turns = new SailRaceTurn[entries.Length];
            _retired = new bool[entries.Length]; _blocked = new bool[entries.Length];
            _contacts = new bool[checked(entries.Length * (entries.Length - 1) / 2)];
            for (int i = 0; i < entries.Length; i++)
            {
                var e = entries[i];
                _ = new SailRaceEntry(e.RacerId, e.Handicap, e.Initial, e.Forward, e.OverlapLengthMetres);
                if (e.Initial.Time != _lastTime) throw new ArgumentException("All initial samples must share one time.");
                for (int j = 0; j < i; j++) if (entries[j].RacerId == e.RacerId) throw new ArgumentException("Duplicate entrant.");
                _racers[i] = new SailRaceEntrant(e.RacerId, course, gunTime, e.Initial);
                _turns[i] = new SailRaceTurn(rules);
            }
        }

        public SailRaceEntrant Entrant(int index) => _racers[index];
        public int OwedTurns(int index) => _turns[index].Owed;
        public SailRacePhase Phase => _course.PhaseAt(_lastTime, _gun);
        public SailRaceStatus Status(int index) => _retired[index] ? SailRaceStatus.Retired :
            _racers[index].Finished ? SailRaceStatus.Finished : _racers[index].OverEarly ? SailRaceStatus.OverEarly :
            _racers[index].Started ? SailRaceStatus.Racing : SailRaceStatus.Waiting;
        public bool Complete
        {
            get { for (int i = 0; i < Count; i++) if (!_retired[i] && !_racers[i].Finished) return false; return true; }
        }
        public void Retire(int index)
        {
            if (_racers[index].Finished) throw new InvalidOperationException("A finisher cannot retire.");
            _retired[index] = true;
        }

        private SailRaceClubBoat State(int i, RaceBoatMotion motion) => new SailRaceClubBoat(motion,
            _racers[i].CompletedLaps, _racers[i].NextMarkIndex, _entries[i].OverlapLengthMetres);

        // Motions are in registration order, at the same absolute simulation time.
        // Contacts at this observation are adjudicated before any gate earned by this segment.
        public void Tick(double time, RaceBoatMotion[] motions, Vector2 wind)
        {
            RaceGeometry.RequireFinite(time, nameof(time)); RaceGeometry.RequireFinite(wind, nameof(wind));
            if (time <= _lastTime || motions == null || motions.Length != Count) throw new ArgumentException("One chronological sample per entrant required.");
            for (int i = 0; i < Count; i++)
            {
                SailRaceClubBoat.ValidateMotion(motions[i]);
                if (motions[i].Id != _entries[i].RacerId) throw new ArgumentException("Motion roster/order mismatch.");
                _racers[i].ValidateSample(new RaceTrackSample(time, motions[i].Position));
                _turns[i].ValidateObservation(time, BoatKinematics.BearingDegrees(motions[i].Forward));
            }
            for (int i = 0; i < Count; i++) _blocked[i] = _turns[i].Owed > 0 || _retired[i];
            int pair = 0;
            for (int i = 0; i < Count; i++)
                for (int j = i + 1; j < Count; j++, pair++)
                {
                    float distance = Vector2.Distance(motions[i].Position, motions[j].Position);
                    if (distance > _course.Rules.ContactReleaseMetres) _contacts[pair] = false;
                    if (_contacts[pair] || distance > _course.Rules.ContactDistanceMetres || time < _gun ||
                        _retired[i] || _retired[j] || _racers[i].Finished || _racers[j].Finished) continue;
                    _contacts[pair] = true;
                    var judgement = SailRaceClubJudge.Judge(State(i, motions[i]), State(j, motions[j]), wind, _course);
                    if (judgement.Rule == SailRaceClubRule.None) continue;
                    int offender = judgement.GiveWayId == motions[i].Id ? i : j;
                    _turns[offender].Queue(time, BoatKinematics.BearingDegrees(motions[offender].Forward));
                    _blocked[offender] = true;
                }
            for (int i = 0; i < Count; i++)
            {
                _turns[i].Advance(time, BoatKinematics.BearingDegrees(motions[i].Forward));
                _racers[i].Advance(new RaceTrackSample(time, motions[i].Position), !_blocked[i]);
            }
            _lastTime = time;
        }

        public SailRaceResult[] Results()
        {
            var results = new SailRaceResult[Count];
            for (int i = 0; i < Count; i++) results[i] = new SailRaceResult(_entries[i].RacerId, _entries[i].Handicap,
                Status(i), _racers[i].Finished ? _racers[i].ElapsedSeconds : double.NaN, _racers[i].PenaltySeconds);
            return SailRaceResults.Order(results);
        }
    }
}
