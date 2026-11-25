using System;
using System.Collections.Generic;
using UnityEngine;

namespace HiddenHarbours.World.Golf
{
    /// <summary>In-memory stroke play. No wallet, persistence, input, view, or scene ownership.</summary>
    public sealed class GolfRound
    {
        readonly GolfHole[] _holes;
        readonly GolfRules _rules;
        readonly int[] _strokes;
        public int HoleIndex { get; private set; }
        public int ShotIndex { get; private set; }
        public bool Complete => HoleIndex == _holes.Length;
        public Vector2 BallPosition { get; private set; }
        public GolfHole CurrentHole => Complete ? null : _holes[HoleIndex];
        public GolfLie Lie => Complete ? GolfLie.Holed : CurrentHole.LieAt(BallPosition);
        public int TotalStrokes { get; private set; }
        public int CompletedPar { get; private set; }
        public int CompletedStrokes { get; private set; }
        public int ScoreToPar => CompletedStrokes - CompletedPar;
        public int StrokesAt(int holeIndex) => _strokes[holeIndex];

        public GolfRound(GolfHole[] holes, in GolfRules rules)
        {
            if (holes == null || holes.Length == 0) throw new ArgumentException("Round needs holes.");
            GolfCheck.Positive(rules.StepSeconds, nameof(rules));
            _holes = (GolfHole[])holes.Clone(); _rules = rules; _strokes = new int[holes.Length];
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var numbers = new HashSet<int>();
            foreach (GolfHole hole in _holes)
            {
                if (hole == null || !ids.Add(hole.Id) || !numbers.Add(hole.Number) ||
                    hole.CourseId != _holes[0].CourseId || hole.RegionId != _holes[0].RegionId)
                    throw new ArgumentException("Round needs unique holes from one region and course.");
            }
            BallPosition = _holes[0].TeePosition;
        }

        public GolfShotResult Play(in GolfClub club, in GolfSwing swing, in GolfShotContext context)
        {
            if (Complete) throw new InvalidOperationException("Round is complete.");
            if (context.ShotIndex != ShotIndex) throw new ArgumentException("Shot context is stale.");
            GolfShotResult result = GolfSimulation.Shoot(CurrentHole, club, _rules, BallPosition, swing, context);
            if (result.Status == GolfShotStatus.Unresolved) return result; // Iteration guard is not a made-up rest or score.
            int penalty = result.Status == GolfShotStatus.Water ? _rules.WaterPenalty :
                result.Status == GolfShotStatus.OutOfBounds ? _rules.OutOfBoundsPenalty : 0;
            int strokes = checked(1 + penalty);
            int newHoleStrokes = checked(_strokes[HoleIndex] + strokes), total = checked(TotalStrokes + strokes);
            int nextShot = checked(ShotIndex + 1);
            int completedPar = result.Status == GolfShotStatus.Holed ? checked(CompletedPar + CurrentHole.Par) : CompletedPar;
            int completedStrokes = result.Status == GolfShotStatus.Holed ? checked(CompletedStrokes + newHoleStrokes) : CompletedStrokes;
            _strokes[HoleIndex] = newHoleStrokes; TotalStrokes = total; ShotIndex = nextShot;
            // Explicit stroke-and-distance rule for both penalties. The result still reports the hazard contact.
            if (penalty > 0 || result.Status == GolfShotStatus.Water || result.Status == GolfShotStatus.OutOfBounds)
                return result;
            BallPosition = result.Position;
            if (result.Status == GolfShotStatus.Holed)
            {
                CompletedPar = completedPar; CompletedStrokes = completedStrokes; HoleIndex++;
                if (!Complete) BallPosition = CurrentHole.TeePosition;
            }
            return result;
        }
    }
}
