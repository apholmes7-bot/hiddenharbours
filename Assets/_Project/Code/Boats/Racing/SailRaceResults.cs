using System;
using System.Collections.Generic;

namespace HiddenHarbours.Boats
{
    public enum SailRaceStatus { Waiting, OverEarly, Racing, Finished, Retired }

    public readonly struct SailRaceResult
    {
        public readonly string RacerId, HullId;
        public readonly SailRaceStatus Status;
        public readonly double ElapsedSeconds, TimePenaltySeconds, CorrectedSeconds;
        public readonly float TimeMultiplier;
        public SailRaceResult(string racerId, SailRaceHandicap handicap, SailRaceStatus status, double elapsedSeconds,
            double timePenaltySeconds = 0)
        {
            RaceGeometry.RequireId(racerId, "racer.");
            _ = new SailRaceHandicap(handicap.HullId, handicap.TimeMultiplier);
            if (status < SailRaceStatus.Waiting || status > SailRaceStatus.Retired) throw new ArgumentOutOfRangeException(nameof(status));
            RaceGeometry.RequireFinite(timePenaltySeconds, nameof(timePenaltySeconds));
            if (timePenaltySeconds < 0) throw new ArgumentOutOfRangeException(nameof(timePenaltySeconds));
            if (status == SailRaceStatus.Finished)
            {
                RaceGeometry.RequireFinite(elapsedSeconds, nameof(elapsedSeconds));
                if (elapsedSeconds < 0) throw new ArgumentOutOfRangeException(nameof(elapsedSeconds));
                CorrectedSeconds = (elapsedSeconds + timePenaltySeconds) * handicap.TimeMultiplier;
                RaceGeometry.RequireFinite(CorrectedSeconds, nameof(CorrectedSeconds));
            }
            else
            {
                if (!double.IsNaN(elapsedSeconds)) throw new ArgumentException("Unfinished boats have no finish time.");
                CorrectedSeconds = double.NaN;
            }
            RacerId = racerId; HullId = handicap.HullId; Status = status; ElapsedSeconds = elapsedSeconds;
            TimeMultiplier = handicap.TimeMultiplier; TimePenaltySeconds = timePenaltySeconds;
        }
    }

    public static class SailRaceResults
    {
        private sealed class ResultComparer : IComparer<SailRaceResult>
        {
            public int Compare(SailRaceResult a, SailRaceResult b)
            {
                bool af = a.Status == SailRaceStatus.Finished, bf = b.Status == SailRaceStatus.Finished;
                if (af != bf) return af ? -1 : 1;
                int time = af ? a.CorrectedSeconds.CompareTo(b.CorrectedSeconds) : 0;
                return time != 0 ? time : string.CompareOrdinal(a.RacerId, b.RacerId);
            }
        }
        private static readonly IComparer<SailRaceResult> Comparer = new ResultComparer();

        // Results are built outside the tick. Never sort the caller's array in place.
        public static SailRaceResult[] Order(SailRaceResult[] results)
        {
            if (results == null) throw new ArgumentNullException(nameof(results));
            for (int i = 0; i < results.Length; i++)
            {
                var r = results[i];
                _ = new SailRaceResult(r.RacerId, new SailRaceHandicap(r.HullId, r.TimeMultiplier), r.Status,
                    r.ElapsedSeconds, r.TimePenaltySeconds);
                for (int j = 0; j < i; j++)
                    if (results[j].RacerId == r.RacerId) throw new ArgumentException("Duplicate result.");
            }
            var ordered = (SailRaceResult[])results.Clone();
            Array.Sort(ordered, Comparer);
            return ordered;
        }
    }
}
