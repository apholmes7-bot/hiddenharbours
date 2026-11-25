using System;

namespace HiddenHarbours.Boats
{
    public readonly struct SailRaceSeasonRow
    {
        public readonly string RacerId;
        public readonly long Points;
        public readonly int CompletedRaces;
        internal SailRaceSeasonRow(string id, long points, int races) { RacerId = id; Points = points; CompletedRaces = races; }
    }

    /// <summary>Low-point table, no discards, complete results in the authored race order. No save/calendar dependency.</summary>
    public sealed class SailRaceSeason
    {
        private readonly string[] _races, _racers;
        private readonly int[] _placePoints;
        private readonly int _nonFinisherPoints;
        private readonly long[] _points;
        public int CompletedRaces { get; private set; }
        public SailRaceSeason(string[] raceIds, string[] racerIds, int[] placePoints, int nonFinisherPoints)
        {
            ValidateIds(raceIds, "race."); ValidateIds(racerIds, "racer.");
            if (placePoints == null || placePoints.Length < racerIds.Length || nonFinisherPoints < 0)
                throw new ArgumentException("Provide points for every place and non-finisher.");
            for (int i = 0; i < placePoints.Length; i++)
                if (placePoints[i] < 0 || placePoints[i] > nonFinisherPoints || (i > 0 && placePoints[i] < placePoints[i - 1]))
                    throw new ArgumentException("Low points must increase by place, with non-finishers last.");
            _races = (string[])raceIds.Clone(); _racers = (string[])racerIds.Clone();
            _placePoints = (int[])placePoints.Clone(); _nonFinisherPoints = nonFinisherPoints;
            _points = new long[_racers.Length];
        }

        private static void ValidateIds(string[] ids, string prefix)
        {
            if (ids == null || ids.Length == 0) throw new ArgumentException("An ordered nonempty identity list is required.");
            for (int i = 0; i < ids.Length; i++)
            {
                RaceGeometry.RequireId(ids[i], prefix);
                for (int j = 0; j < i; j++) if (ids[j] == ids[i]) throw new ArgumentException("Duplicate identity.");
            }
        }

        public void Record(string raceId, SailRaceResult[] results)
        {
            if (CompletedRaces >= _races.Length || raceId != _races[CompletedRaces])
                throw new ArgumentException("Record each race once in the authored order.");
            var ordered = SailRaceResults.Order(results);
            if (ordered.Length != _racers.Length) throw new ArgumentException("Every entrant needs a result.");
            for (int i = 0; i < ordered.Length; i++)
                if (Array.IndexOf(_racers, ordered[i].RacerId) < 0 ||
                    (ordered[i].Status != SailRaceStatus.Finished && ordered[i].Status != SailRaceStatus.Retired))
                    throw new ArgumentException("Complete results must contain only registered, finished or retired entrants.");
            // All validation precedes mutation.
            for (int i = 0; i < ordered.Length; i++)
                _points[Array.IndexOf(_racers, ordered[i].RacerId)] += ordered[i].Status == SailRaceStatus.Finished
                    ? _placePoints[i] : _nonFinisherPoints;
            CompletedRaces++;
        }

        public SailRaceSeasonRow[] Table()
        {
            var rows = new SailRaceSeasonRow[_racers.Length];
            for (int i = 0; i < rows.Length; i++) rows[i] = new SailRaceSeasonRow(_racers[i], _points[i], CompletedRaces);
            // Small club tables; stable explicit comparison avoids cultural string ordering.
            for (int i = 1; i < rows.Length; i++)
                for (int j = i; j > 0 && (rows[j].Points < rows[j - 1].Points ||
                    (rows[j].Points == rows[j - 1].Points && string.CompareOrdinal(rows[j].RacerId, rows[j - 1].RacerId) < 0)); j--)
                { var swap = rows[j]; rows[j] = rows[j - 1]; rows[j - 1] = swap; }
            return rows;
        }
    }
}
