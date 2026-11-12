using System;
using HiddenHarbours.Core;
using HiddenHarbours.Fishing;
using UnityEngine;

namespace HiddenHarbours.Tests.EditMode
{
    internal static class TrawlTestData
    {
        internal static TrawlGearDef Gear => new TrawlGearDef
        {
            Id = "trawl.test_gear", MinDepthMetres = 10d, MaxDepthMetres = 100d,
            MinWarpDepthRatio = 2d, MaxWarpDepthRatio = 5d,
            MinTowMetresPerGameSecond = 1d, MaxTowMetresPerGameSecond = 3d,
            MinDoorSpreadMetres = 4d, MaxDoorSpreadMetres = 8d, MouthHeightMetres = 3d,
            CaptureFraction01 = 1d, HardBottomThreshold01 = 0.5d, DamagePerHardMetre = 0.01d,
            SampleGameSeconds = 2d, MaxTowGameSeconds = 100d, CodendCapacityIndividuals = 100,
            MaxSamples = 50, MaxSchoolsPerSample = 8, MaxEncounteredSchools = 16, MaxTrackKnots = 16
        };

        internal static TrawlHandlingDef Stern => new TrawlHandlingDef
        {
            Id = "trawl_handling.test_stern", Kind = TrawlHandlingKind.Stern, WorkStation = TrawlWorkStation.Astern,
            ShootingGameSeconds = 4d, HaulingGameSeconds = 6d, MaxShootingMetresPerGameSecond = 2d,
            MaxHaulingMetresPerGameSecond = 1d, MaxWorkingSeaState01 = 0.5d, SortIndividualsPerGameSecond = 2d
        };

        internal static TrawlHandlingDef Side
        {
            get
            {
                var d = Stern; d.Id = "trawl_handling.test_side"; d.Kind = TrawlHandlingKind.Side;
                d.WorkStation = TrawlWorkStation.Starboard; d.ShootingGameSeconds = 8d;
                d.HaulingGameSeconds = 12d; d.MaxShootingMetresPerGameSecond = 0d;
                d.MaxHaulingMetresPerGameSecond = 0d; return d;
            }
        }

        internal static TrawlTrack Track(double speed = 2d, double duration = 10d, double warp = 60d,
            double spread = 4d, double start = 0d) => new TrawlTrack(new[]
        {
            new TrawlTrackKnot(start, Vector2.zero, warp, spread),
            new TrawlTrackKnot(start + duration, new Vector2((float)(speed * duration), 0f), warp, spread)
        });

        internal static TrawlPopulationSample Population(string id = "fish.test_cod", uint key = 1,
            int count = 12, float x = 10f, float y = 0f, float depth = 19f, float radius = 1f,
            SeasonMask seasons = SeasonMask.AllYear, bool eligible = true, double minWeight = 1d, double maxWeight = 2d)
        {
            var school = new SchoolFieldSample(id, key, new Vector2(x, y), depth, radius, 1f, count, 0d);
            var facts = new TrawlSpeciesFacts(id, seasons, minWeight, maxWeight, eligible);
            return new TrawlPopulationSample(school, facts);
        }

        internal sealed class Survey : ITrawlSurvey
        {
            internal TrawlPopulationSample[] Populations = { Population() };
            internal TrawlGroundSample Ground = new TrawlGroundSample("ground.test_bank", 20d, 0d, Season.EarlySpring);
            internal bool Reverse;
            internal int? ReturnCount;
            internal double UnknownAfter = double.PositiveInfinity;
            internal Func<double, TrawlPopulationSample[]> AtTime;
            internal int Calls;
            public int Fill(TrawlTrack track, double from, double to, double halfWidth, double time,
                TrawlPopulationSample[] into, out TrawlGroundSample ground)
            {
                Calls++; ground = Ground;
                if (time >= UnknownAfter) return -1;
                var samples = AtTime == null ? Populations : AtTime(time);
                for (int i = 0; i < Math.Min(into.Length, samples.Length); i++)
                    into[i] = samples[Reverse ? samples.Length - i - 1 : i];
                return ReturnCount ?? samples.Length;
            }
        }

        internal static TrawlTowResult Resolve(Survey survey = null, TrawlTrack track = null,
            TrawlGearDef? gear = null, int seed = 7, string[] targets = null, double integrity = 1d)
            => TrawlTowSolver.Resolve(gear ?? Gear, seed, "tow.test", track ?? Track(), survey ?? new Survey(),
                targets ?? new[] { "fish.test_cod" }, integrity);
    }
}
