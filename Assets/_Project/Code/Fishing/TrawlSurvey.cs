using System;
using HiddenHarbours.Core;
using UnityEngine;

namespace HiddenHarbours.Fishing
{
    /// <summary>Immutable species facts, copied from content. Trawl eligibility needs explicit review:
    /// main has no distinct Trawl gear bit, and Net is not silently treated as permission.</summary>
    public readonly struct TrawlSpeciesFacts
    {
        public readonly string Id;
        public readonly SeasonMask Seasons;
        public readonly double MinWeightKg, MaxWeightKg;
        public readonly bool TrawlEligible;
        public TrawlSpeciesFacts(string id, SeasonMask seasons, double minWeightKg, double maxWeightKg, bool trawlEligible)
        { Id = id; Seasons = seasons; MinWeightKg = minWeightKg; MaxWeightKg = maxWeightKg; TrawlEligible = trawlEligible; }
        public bool IsValid => !string.IsNullOrWhiteSpace(Id) && (Seasons & ~SeasonMask.AllYear) == 0
            && TrawlNumbers.Positive(MinWeightKg) && TrawlNumbers.Positive(MaxWeightKg) && MaxWeightKg >= MinWeightKg;
        public bool SeasonAllowed(Season season) => season >= Season.EarlySpring && season <= Season.HardWinter
            && ((int)Seasons & (1 << (int)season)) != 0;
    }

    public readonly struct TrawlPopulationSample
    {
        public readonly SchoolFieldSample School;
        public readonly TrawlSpeciesFacts Species;
        public TrawlPopulationSample(in SchoolFieldSample school, in TrawlSpeciesFacts species)
        { School = school; Species = species; }
    }

    public readonly struct TrawlGroundSample
    {
        public readonly string GroundId;
        public readonly double BottomDepthMetres, Hardness01;
        public readonly Season Season;
        public TrawlGroundSample(string groundId, double bottomDepthMetres, double hardness01, Season season)
        { GroundId = groundId; BottomDepthMetres = bottomDepthMetres; Hardness01 = hardness01; Season = season; }
        public bool IsValid => !string.IsNullOrWhiteSpace(GroundId) && TrawlNumbers.Positive(BottomDepthMetres)
            && TrawlNumbers.Unit(Hardness01) && Season >= Season.EarlySpring && Season <= Season.HardWinter;
    }

    /// <summary>Fishing-local seam. A future World adapter must go through Core. Return every school
    /// overlapping this bin's swept track (including school footprints), at the exact query time.
    /// Ground must certify the WHOLE bin; split a ground boundary into separately certified tows.
    /// Return -1 for unknown or truncated coverage, never a plausible partial list. Keep facts fixed
    /// throughout a solve. No mutation, service lookup, RNG or per-query allocation is permitted.</summary>
    public interface ITrawlSurvey
    {
        int Fill(TrawlTrack track, double fromGameSeconds, double toGameSeconds, double halfWidthMetres,
            double sampleGameSeconds, TrawlPopulationSample[] into, out TrawlGroundSample ground);
    }

    /// <summary>Uses the EXISTING field; never fabricates a second school or changes its tuning.</summary>
    public static class TrawlSchoolFieldBridge
    {
        public static bool TrySample(SchoolField field, FishSpeciesDef species, bool reviewedTrawlEligibility,
            int cellX, int cellY, double gameSeconds, in SchoolHabitatPatch patch,
            in SchoolFieldEnvironment environment, in SchoolReaction reaction, out TrawlPopulationSample sample)
        {
            sample = default;
            if (field == null || species == null || !reviewedTrawlEligibility) return false;
            var facts = new TrawlSpeciesFacts(species.Id, species.Seasons, species.MinWeightKg, species.MaxWeightKg, true);
            if (!facts.IsValid || !field.TrySample(species, cellX, cellY, gameSeconds, patch, environment, reaction, out var school))
                return false;
            sample = new TrawlPopulationSample(school, facts);
            return true;
        }
    }

    public readonly struct TrawlTrackKnot
    {
        public readonly double GameSeconds, WarpMetres, DoorSpreadMetres;
        public readonly Vector2 NetCentre;
        public TrawlTrackKnot(double gameSeconds, Vector2 netCentre, double warpMetres, double doorSpreadMetres)
        { GameSeconds = gameSeconds; NetCentre = netCentre; WarpMetres = warpMetres; DoorSpreadMetres = doorSpreadMetres; }
    }

    /// <summary>The NET's metre track, not a hull trail pretending gear instantly follows a boat.
    /// Owns a copy; collinear knots do not change the fixed tow-time sampling lattice.</summary>
    public sealed class TrawlTrack
    {
        private readonly TrawlTrackKnot[] _knots;
        public int Count => _knots.Length;
        public double Start => _knots[0].GameSeconds;
        public double End => _knots[_knots.Length - 1].GameSeconds;
        public TrawlTrack(TrawlTrackKnot[] knots)
        {
            if (knots == null || knots.Length < 2) throw new ArgumentException("A tow needs at least two knots.", nameof(knots));
            _knots = (TrawlTrackKnot[])knots.Clone();
            for (int i = 0; i < _knots.Length; i++)
            {
                var k = _knots[i];
                if (!TrawlNumbers.NonNegative(k.GameSeconds) || !TrawlNumbers.Finite(k.NetCentre.x)
                    || !TrawlNumbers.Finite(k.NetCentre.y) || !TrawlNumbers.NonNegative(k.WarpMetres)
                    || !TrawlNumbers.NonNegative(k.DoorSpreadMetres) || (i > 0 && k.GameSeconds <= _knots[i - 1].GameSeconds))
                    throw new ArgumentException("Track knots must be finite and strictly chronological.", nameof(knots));
            }
        }

        public TrawlTrackKnot At(double time)
        {
            if (!TrawlNumbers.Finite(time) || time < Start || time > End) throw new ArgumentOutOfRangeException(nameof(time));
            int i = 1;
            while (i < _knots.Length - 1 && time > _knots[i].GameSeconds) i++;
            var a = _knots[i - 1]; var b = _knots[i];
            double u = (time - a.GameSeconds) / (b.GameSeconds - a.GameSeconds);
            return new TrawlTrackKnot(time,
                new Vector2((float)(a.NetCentre.x + ((double)b.NetCentre.x - a.NetCentre.x) * u),
                            (float)(a.NetCentre.y + ((double)b.NetCentre.y - a.NetCentre.y) * u)),
                a.WarpMetres + (b.WarpMetres - a.WarpMetres) * u,
                a.DoorSpreadMetres + (b.DoorSpreadMetres - a.DoorSpreadMetres) * u);
        }

        // Exact arc length over the authored polyline, including corners inside a sampling bin.
        public double Distance(double from, double to) => DistanceInside(from, to, default, double.PositiveInfinity);

        internal bool WithinFishingWindow(double from, double to, double depth, in TrawlGearDef gear)
        {
            var a = At(from);
            double start = from;
            for (int i = 1; i < _knots.Length; i++)
            {
                if (_knots[i].GameSeconds <= from) continue;
                double end = Math.Min(to, _knots[i].GameSeconds);
                var b = At(end);
                double speed = LengthInside(a.NetCentre, b.NetCentre, default, double.PositiveInfinity) / (end - start);
                if (speed < gear.MinTowMetresPerGameSecond || speed > gear.MaxTowMetresPerGameSecond
                    || !ControlAllowed(a, depth, gear) || !ControlAllowed(b, depth, gear)) return false;
                if (end >= to) return true;
                start = end; a = b;
            }
            return false;
        }

        private static bool ControlAllowed(in TrawlTrackKnot knot, double depth, in TrawlGearDef gear)
            => knot.WarpMetres / depth >= gear.MinWarpDepthRatio && knot.WarpMetres / depth <= gear.MaxWarpDepthRatio
                && knot.DoorSpreadMetres >= gear.MinDoorSpreadMetres && knot.DoorSpreadMetres <= gear.MaxDoorSpreadMetres;

        internal double DistanceInside(double from, double to, Vector2 centre, double radius)
        {
            if (!TrawlNumbers.Finite(to) || from > to || to > End || to < Start)
                throw new ArgumentOutOfRangeException(nameof(to));
            var a = At(from).NetCentre;
            double distance = 0d;
            for (int i = 1; i < _knots.Length; i++)
            {
                if (_knots[i].GameSeconds <= from) continue;
                double end = Math.Min(to, _knots[i].GameSeconds);
                var b = At(end).NetCentre;
                distance += LengthInside(a, b, centre, radius);
                if (end >= to) break;
                a = b;
            }
            return distance;
        }

        internal double EndAfterDistance(double from, double to, double distance)
        {
            var a = At(from).NetCentre;
            double start = from;
            for (int i = 1; i < _knots.Length; i++)
            {
                if (_knots[i].GameSeconds <= from) continue;
                double end = Math.Min(to, _knots[i].GameSeconds);
                var b = At(end).NetCentre;
                double length = LengthInside(a, b, default, double.PositiveInfinity);
                if (length > 0d && distance <= length) return start + (end - start) * distance / length;
                distance -= length;
                if (end >= to) break;
                start = end; a = b;
            }
            return to;
        }

        private static double LengthInside(Vector2 a, Vector2 b, Vector2 centre, double radius)
        {
            double dx = (double)b.x - a.x, dy = (double)b.y - a.y, length2 = dx * dx + dy * dy;
            if (length2 == 0d) return 0d;
            double length = Math.Sqrt(length2);
            if (double.IsPositiveInfinity(radius)) return length;
            double x = (double)a.x - centre.x, y = (double)a.y - centre.y;
            double projection = -(x * dx + y * dy) / length2;
            double closest2 = x * x + y * y - projection * projection * length2;
            double half2 = (radius * radius - Math.Max(0d, closest2)) / length2;
            if (half2 <= 0d) return 0d;
            double half = Math.Sqrt(half2);
            return length * Math.Max(0d, Math.Min(1d, projection + half) - Math.Max(0d, projection - half));
        }
    }
}
