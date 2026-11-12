using System;
using UnityEngine;

namespace HiddenHarbours.Fishing
{
    public enum TrawlTowStatus { Complete, UnknownGround, InvalidSurvey, BudgetExceeded }

    public readonly struct TrawlCatchLot
    {
        public readonly string SpeciesId;
        public readonly uint SchoolKey;
        public readonly int Count;
        public readonly bool IsBycatch;
        public readonly double UnitWeightKg;
        public double WeightKg => UnitWeightKg * Count;
        internal TrawlCatchLot(string speciesId, uint schoolKey, int count, bool isBycatch, double unitWeightKg)
        { SpeciesId = speciesId; SchoolKey = schoolKey; Count = count; IsBycatch = isBycatch; UnitWeightKg = unitWeightKg; }
    }

    /// <summary>A replayable PROPOSAL of codend contents, never an ownership/depletion receipt.</summary>
    public sealed class TrawlTowResult
    {
        private readonly TrawlCatchLot[] _lots;
        public TrawlTowStatus Status { get; }
        public int LotCount => _lots.Length;
        public TrawlCatchLot this[int index] => _lots[index];
        public int Count { get; }
        public int BycatchCount { get; }
        public double WeightKg { get; }
        public double EndIntegrity01 { get; }
        public double SweptSquareMetres { get; }
        public int Samples { get; }
        internal TrawlTowResult(TrawlTowStatus status, TrawlCatchLot[] lots, double integrity, double swept, int samples)
        {
            Status = status; _lots = lots; EndIntegrity01 = integrity; SweptSquareMetres = swept; Samples = samples;
            foreach (var lot in lots)
            { Count += lot.Count; if (lot.IsBycatch) BycatchCount += lot.Count; WeightKg += lot.WeightKg; }
        }
    }

    /// <summary>Bounded batch calculation on the tow-start time lattice, independent of frame/tick
    /// history. Repeated passes share a per-tow school ceiling. All world reads are explicit.</summary>
    public static class TrawlTowSolver
    {
        private struct Encounter
        {
            internal TrawlSpeciesFacts Species;
            internal uint Key;
            internal int Individuals;
            internal int Captured;
            internal double Survival;
        }

        public static TrawlTowResult Resolve(in TrawlGearDef gear, int seed, string towId, TrawlTrack track,
            ITrawlSurvey survey, string[] targetSpeciesIds, double initialIntegrity01)
        {
            if (!gear.IsValid) throw new ArgumentException("Invalid trawl gear definition.", nameof(gear));
            if (string.IsNullOrWhiteSpace(towId)) throw new ArgumentException("A stable tow id is required.", nameof(towId));
            if (track == null) throw new ArgumentNullException(nameof(track));
            if (survey == null) throw new ArgumentNullException(nameof(survey));
            if (!TrawlNumbers.Unit(initialIntegrity01)) throw new ArgumentOutOfRangeException(nameof(initialIntegrity01));
            double duration = track.End - track.Start;
            double bins = Math.Ceiling(duration / gear.SampleGameSeconds);
            if (duration > gear.MaxTowGameSeconds || bins > gear.MaxSamples || track.Count > gear.MaxTrackKnots)
                return Failed(TrawlTowStatus.BudgetExceeded, initialIntegrity01);
            int samples = (int)bins;
            var buffer = new TrawlPopulationSample[gear.MaxSchoolsPerSample];
            var encountered = new Encounter[gear.MaxEncounteredSchools];
            int rows = 0;
            int remaining = gear.CodendCapacityIndividuals;
            uint towKey = unchecked((uint)StableHash.CatchSeed(seed, towId, track.Start));
            double integrity = initialIntegrity01, swept = 0d;
            for (int bin = 0; bin < samples; bin++)
            {
                double from = track.Start + bin * gear.SampleGameSeconds;
                double to = bin == samples - 1 ? track.End : track.Start + (bin + 1d) * gear.SampleGameSeconds;
                double time = from + (to - from) / 2d;
                if (!(from < time && time < to)) return Failed(TrawlTowStatus.InvalidSurvey, initialIntegrity01);
                var control = track.At(time);
                int found = survey.Fill(track, from, to, control.DoorSpreadMetres / 2d, time, buffer, out var ground);
                if (found < 0 || !ground.IsValid) return Failed(TrawlTowStatus.UnknownGround, initialIntegrity01);
                if (found > buffer.Length) return Failed(TrawlTowStatus.BudgetExceeded, initialIntegrity01);
                for (int i = 0; i < found; i++)
                {
                    if (!Valid(buffer[i])) return Failed(TrawlTowStatus.InvalidSurvey, initialIntegrity01);
                    for (int j = 0; j < i; j++)
                        if (SameIdentity(buffer[i].School, buffer[j].School))
                            return Failed(TrawlTowStatus.InvalidSurvey, initialIntegrity01);
                }

                double distance = track.Distance(from, to);
                double ratio = control.WarpMetres / ground.BottomDepthMetres;
                bool contact = ratio >= gear.MinWarpDepthRatio;
                double wear = contact ? distance * Math.Max(0d, ground.Hardness01 - gear.HardBottomThreshold01)
                    * gear.DamagePerHardMetre : 0d;
                if (!TrawlNumbers.Finite(wear)) return Failed(TrawlTowStatus.InvalidSurvey, initialIntegrity01);
                // Only the pre-break part of this bin can catch. Existing codend contents stay there.
                double intactFraction = integrity <= 0d ? 0d : wear > integrity ? integrity / wear : 1d;
                integrity = Math.Max(0d, integrity - wear);
                bool fishing = contact
                    && ground.BottomDepthMetres >= gear.MinDepthMetres && ground.BottomDepthMetres <= gear.MaxDepthMetres
                    && track.WithinFishingWindow(from, to, ground.BottomDepthMetres, gear);
                if (!fishing || intactFraction == 0d) continue;
                double area = distance * control.DoorSpreadMetres * intactFraction;
                swept += area;
                if (!TrawlNumbers.Finite(swept)) return Failed(TrawlTowStatus.InvalidSurvey, initialIntegrity01);
                if (remaining == 0) continue;
                double fishingTo = intactFraction < 1d ? track.EndAfterDistance(from, to, distance * intactFraction) : to;
                for (int i = 0; i < found; i++)
                {
                    var population = buffer[i]; var school = population.School; var species = population.Species;
                    if (!species.TrawlEligible || !species.SeasonAllowed(ground.Season)
                        || school.DepthMetres < Math.Max(0d, ground.BottomDepthMetres - gear.MouthHeightMetres)
                        || school.DepthMetres > ground.BottomDepthMetres) continue;
                    double intercepted = track.DistanceInside(from, fishingTo, school.Centre,
                        school.FootprintMetres + control.DoorSpreadMetres / 2d);
                    if (intercepted <= 0d) continue;
                    int row = Find(encountered, rows, school);
                    if (row < 0)
                    {
                        if (rows == encountered.Length) return Failed(TrawlTowStatus.BudgetExceeded, initialIntegrity01);
                        row = rows++;
                        encountered[row] = new Encounter { Species = species, Key = school.Key,
                            Individuals = school.Individuals, Survival = 1d };
                    }
                    else if (!SameFacts(encountered[row].Species, species) || encountered[row].Individuals != school.Individuals)
                        return Failed(TrawlTowStatus.InvalidSurvey, initialIntegrity01);
                    double schoolArea = Math.PI * school.FootprintMetres * school.FootprintMetres;
                    double coverage = schoolArea == 0d ? 1d : Math.Min(1d, intercepted * control.DoorSpreadMetres / schoolArea);
                    encountered[row].Survival *= 1d - coverage * gear.CaptureFraction01;
                }
                // Fill in time order. Within one canonical bin use a stable tie-break, so a provider
                // cannot favour a species by enumerating it first. A later school displaces nothing.
                Sort(encountered, rows);
                for (int i = 0; i < rows && remaining > 0; i++)
                {
                    var e = encountered[i];
                    double expected = e.Individuals * (1d - e.Survival);
                    int cumulative = (int)Math.Min(e.Individuals, Math.Floor(expected + Draw(Key(towKey, e), 0)));
                    int added = Math.Min(remaining, Math.Max(0, cumulative - e.Captured));
                    encountered[i].Captured += added;
                    remaining -= added;
                }
            }

            var lots = new TrawlCatchLot[rows];
            int written = 0;
            for (int i = 0; i < rows; i++)
            {
                var e = encountered[i];
                uint key = Key(towKey, e);
                int count = e.Captured;
                if (count <= 0) continue;
                double weight = e.Species.MinWeightKg + (e.Species.MaxWeightKg - e.Species.MinWeightKg) * Draw(key, 1);
                if (!TrawlNumbers.Finite(weight * count)) return Failed(TrawlTowStatus.InvalidSurvey, initialIntegrity01);
                lots[written++] = new TrawlCatchLot(e.Species.Id, e.Key, count, !IsTarget(e.Species.Id, targetSpeciesIds), weight);
            }
            Array.Resize(ref lots, written);
            var result = new TrawlTowResult(TrawlTowStatus.Complete, lots, integrity, swept, samples);
            return TrawlNumbers.Finite(result.WeightKg) ? result : Failed(TrawlTowStatus.InvalidSurvey, initialIntegrity01);
        }

        private static TrawlTowResult Failed(TrawlTowStatus status, double initialIntegrity)
            => new TrawlTowResult(status, Array.Empty<TrawlCatchLot>(), initialIntegrity, 0d, 0);
        private static double Draw(uint key, int stream) => StableHash.Finalize(StableHash.Fold(key, stream)) / 4294967296d;
        private static uint Key(uint towKey, in Encounter e)
            => StableHash.Fold(StableHash.Fold(towKey, e.Species.Id), unchecked((int)e.Key));
        private static void Sort(Encounter[] encountered, int count)
        {
            for (int i = 1; i < count; i++)
            {
                var row = encountered[i]; int j = i - 1;
                while (j >= 0 && Compare(encountered[j], row) > 0)
                { encountered[j + 1] = encountered[j]; j--; }
                encountered[j + 1] = row;
            }
        }
        private static bool Valid(in TrawlPopulationSample p) => p.Species.IsValid
            && string.Equals(p.Species.Id, p.School.SpeciesId, StringComparison.Ordinal) && p.School.Individuals > 0
            && TrawlNumbers.Finite(p.School.Centre.x) && TrawlNumbers.Finite(p.School.Centre.y)
            && TrawlNumbers.NonNegative(p.School.DepthMetres) && TrawlNumbers.NonNegative(p.School.FootprintMetres);
        private static bool SameIdentity(in SchoolFieldSample a, in SchoolFieldSample b)
            => a.Key == b.Key && string.Equals(a.SpeciesId, b.SpeciesId, StringComparison.Ordinal);
        private static bool SameFacts(in TrawlSpeciesFacts a, in TrawlSpeciesFacts b)
            => a.Id == b.Id && a.Seasons == b.Seasons && a.MinWeightKg == b.MinWeightKg
                && a.MaxWeightKg == b.MaxWeightKg && a.TrawlEligible == b.TrawlEligible;
        private static int Find(Encounter[] rows, int count, in SchoolFieldSample s)
        {
            for (int i = 0; i < count; i++) if (rows[i].Key == s.Key && rows[i].Species.Id == s.SpeciesId) return i;
            return -1;
        }
        private static int Compare(in Encounter a, in Encounter b)
        { int order = string.CompareOrdinal(a.Species.Id, b.Species.Id); return order == 0 ? a.Key.CompareTo(b.Key) : order; }
        private static bool IsTarget(string id, string[] targets)
        { if (targets != null) foreach (string target in targets) if (string.Equals(id, target, StringComparison.Ordinal)) return true; return false; }
    }
}
