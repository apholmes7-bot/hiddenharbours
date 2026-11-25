using System;
using System.Collections.Generic;
using UnityEngine;

namespace HiddenHarbours.Boats
{
    public enum RaceRoundingSide { Port, Starboard }
    public enum SailRacePhase { BeforeWarning, Warning, Preparatory, Racing }

    /// <summary>Legal travel crosses from the right of A->B to its left, between the endpoints.</summary>
    [Serializable]
    public struct RaceLine
    {
        public Vector2 A;
        public Vector2 B;
        public RaceLine(Vector2 a, Vector2 b) { A = a; B = b; }
        public Vector2 Midpoint => (A + B) * 0.5f;
        public Vector2 Forward => new Vector2(-(B - A).y, (B - A).x).normalized;
    }

    /// <summary>Course-local buoy, with radial entry/exit gates in compass degrees.
    /// Port rounds anticlockwise; starboard rounds clockwise. The track must stay in the annulus.</summary>
    [Serializable]
    public struct RaceMark
    {
        public string Id;
        public Vector2 Position;
        public RaceRoundingSide Side;
        public float EntryBearingDeg;
        public float ExitBearingDeg;
        public float ClearanceMetres;
        public float ZoneRadiusMetres;

        public float Direction => Side == RaceRoundingSide.Port ? 1f : -1f;
        public float SweepDeg => Mathf.Repeat((EntryBearingDeg - ExitBearingDeg) * Direction, 360f);
    }

    /// <summary>All race thresholds are authored data, in simulation seconds/metres/degrees.</summary>
    [Serializable]
    public struct SailRaceRules
    {
        public float WarningSeconds;
        public float PreparatorySeconds;
        public float MaximumTrackGapSeconds;
        public float MaximumTrackSegmentMetres;
        public float ContactDistanceMetres;
        public float ContactReleaseMetres;
        public float OvertakingConeHalfDeg;
        public float CalmWindMetresPerSecond;
        public float RightOfWayPenaltySeconds;

        public void Validate()
        {
            RaceGeometry.RequirePositive(WarningSeconds, nameof(WarningSeconds));
            RaceGeometry.RequireNonNegative(PreparatorySeconds, nameof(PreparatorySeconds));
            if (PreparatorySeconds >= WarningSeconds) throw new ArgumentException("Preparatory must follow warning.");
            RaceGeometry.RequirePositive(MaximumTrackGapSeconds, nameof(MaximumTrackGapSeconds));
            RaceGeometry.RequirePositive(MaximumTrackSegmentMetres, nameof(MaximumTrackSegmentMetres));
            RaceGeometry.RequirePositive(ContactDistanceMetres, nameof(ContactDistanceMetres));
            RaceGeometry.RequirePositive(ContactReleaseMetres, nameof(ContactReleaseMetres));
            if (ContactReleaseMetres <= ContactDistanceMetres) throw new ArgumentException("Contact release must exceed entry distance.");
            RaceGeometry.RequirePositive(OvertakingConeHalfDeg, nameof(OvertakingConeHalfDeg));
            if (OvertakingConeHalfDeg >= 90f) throw new ArgumentException("Overtaking cone must be aft of the beam.");
            RaceGeometry.RequireNonNegative(CalmWindMetresPerSecond, nameof(CalmWindMetresPerSecond));
            RaceGeometry.RequireNonNegative(RightOfWayPenaltySeconds, nameof(RightOfWayPenaltySeconds));
        }
    }

    [CreateAssetMenu(menuName = "Hidden Harbours/Sailing/Race Course", fileName = "SailRaceCourse")]
    public sealed class SailRaceCourseDef : ScriptableObject
    {
        public string Id = "racecourse.unnamed";
        public RaceLine Start;
        public RaceMark[] Marks = Array.Empty<RaceMark>();
        public RaceLine Finish;
        public int Laps;
        public SailRaceRules Rules;

        // Club-specific parts of this course, not independent content entities. The original Build
        // remains valid without them; callers opt into the club model through BuildClubSession.
        public SailRaceClubRules ClubRules;
        public SailRaceHandicap[] ClubHandicaps = Array.Empty<SailRaceHandicap>();
        public int[] ClubPlacePoints = Array.Empty<int>();
        public int ClubNonFinisherPoints;

        public SailRaceCourse Build() => new SailRaceCourse(Id, Start, Marks, Finish, Laps, Rules);

        public SailRaceHandicap HandicapFor(string hullId)
        {
            RaceGeometry.RequireId(hullId, "boat.");
            if (ClubHandicaps == null) throw new ArgumentException("Author this course's club handicap table.");
            int found = -1;
            for (int i = 0; i < ClubHandicaps.Length; i++)
            {
                var h = ClubHandicaps[i]; _ = new SailRaceHandicap(h.HullId, h.TimeMultiplier);
                for (int j = 0; j < i; j++) if (ClubHandicaps[j].HullId == h.HullId) throw new ArgumentException("Duplicate hull handicap.");
                if (h.HullId == hullId) found = i;
            }
            if (found < 0) throw new ArgumentException("Hull has no authored club handicap.");
            return ClubHandicaps[found];
        }

        public SailRaceSession BuildClubSession(double gunTime, SailRaceEntry[] entries)
        {
            if (entries == null) throw new ArgumentNullException(nameof(entries));
            foreach (var entry in entries)
                if (HandicapFor(entry.Handicap.HullId).TimeMultiplier != entry.Handicap.TimeMultiplier)
                    throw new ArgumentException("Entry must use this course's authored hull rating.");
            return new SailRaceSession(Build(), ClubRules, gunTime, entries);
        }

        public SailRaceSeason BuildClubSeason(string[] raceIds, string[] racerIds)
            => new SailRaceSeason(raceIds, racerIds, ClubPlacePoints, ClubNonFinisherPoints);
    }

    /// <summary>Validated, immutable snapshot. Inspector edits cannot move an active race's gates.</summary>
    public sealed class SailRaceCourse
    {
        private readonly RaceMark[] _marks;
        public string Id { get; }
        public RaceLine Start { get; }
        public RaceLine Finish { get; }
        public int Laps { get; }
        public SailRaceRules Rules { get; }
        public int MarkCount => _marks.Length;
        public RaceMark Mark(int index) => _marks[index];

        public SailRaceCourse(string id, RaceLine start, RaceMark[] marks, RaceLine finish, int laps, SailRaceRules rules)
        {
            RaceGeometry.RequireId(id, "racecourse.");
            rules.Validate();
            RaceGeometry.ValidateLine(start);
            RaceGeometry.ValidateLine(finish);
            if (laps <= 0) throw new ArgumentOutOfRangeException(nameof(laps));
            if (marks == null || marks.Length == 0) throw new ArgumentException("A course needs ordered marks.");
            _marks = (RaceMark[])marks.Clone();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var mark in _marks)
            {
                RaceGeometry.RequireId(mark.Id, "racemark.");
                if (!ids.Add(mark.Id)) throw new ArgumentException("Course-local mark ids must be unique.");
                RaceGeometry.RequireFinite(mark.Position, nameof(mark.Position));
                RaceGeometry.RequirePositive(mark.ClearanceMetres, nameof(mark.ClearanceMetres));
                RaceGeometry.RequirePositive(mark.ZoneRadiusMetres, nameof(mark.ZoneRadiusMetres));
                if (mark.ZoneRadiusMetres <= mark.ClearanceMetres) throw new ArgumentException("Empty rounding annulus.");
                RaceGeometry.RequireFinite(mark.EntryBearingDeg, nameof(mark.EntryBearingDeg));
                RaceGeometry.RequireFinite(mark.ExitBearingDeg, nameof(mark.ExitBearingDeg));
                if (mark.Side != RaceRoundingSide.Port && mark.Side != RaceRoundingSide.Starboard)
                    throw new ArgumentException("Unknown rounding side.");
                if (mark.SweepDeg <= 0f || mark.SweepDeg > 180f)
                    throw new ArgumentException("A rounding must sweep more than zero and at most 180 degrees.");
            }
            Id = id; Start = start; Finish = finish; Laps = laps; Rules = rules;
        }

        public SailRacePhase PhaseAt(double gameTime, double gunTime)
        {
            RaceGeometry.RequireFinite(gameTime, nameof(gameTime));
            RaceGeometry.RequireFinite(gunTime, nameof(gunTime));
            if (gameTime < gunTime - Rules.WarningSeconds) return SailRacePhase.BeforeWarning;
            if (gameTime < gunTime - Rules.PreparatorySeconds) return SailRacePhase.Warning;
            return gameTime < gunTime ? SailRacePhase.Preparatory : SailRacePhase.Racing;
        }
    }
}
