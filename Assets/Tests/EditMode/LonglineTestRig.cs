using System;
using System.Collections.Generic;
using HiddenHarbours.Core;
using HiddenHarbours.Fishing;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.EditMode
{
    internal sealed class LonglineTestRig : IDisposable
    {
        internal sealed class Sea : IEnvironmentService
        {
            public Func<double, float> LevelAt = _ => 0f;
            public int WorldSeed => 4242;
            public TideProfile ActiveTideProfile { get; set; }
            public EnvironmentSample Sample() => default;
            public float TideHeightAt(double time) => LevelAt(time);
            public float WaterLevelAt(double time) => LevelAt(time);
        }

        internal sealed class Ground : ITidalTerrain
        {
            public Func<Vector2, float> At = _ => -10f;
            public float ElevationAt(Vector2 position) => At(position);
        }

        public readonly LonglineDef Def;
        public readonly Sea Environment = new Sea();
        public readonly Ground Terrain = new Ground();
        private readonly List<Object> _made = new List<Object>();
        public DepthDropSettings Depth = new DepthDropSettings
        {
            TidepoolMaxMeters = 1f, ShallowsMaxMeters = 5f, InshoreMaxMeters = 30f,
            MidwaterMaxMeters = 60f, DeepMaxMeters = 200f,
            InBandAffinity = 1f, OffBandAffinity = 0.2f, BottomWindowAffinity = 1f,
            BottomSweetWindowMeters = 1f,
        };

        public LonglineTestRig()
        {
            Def = ScriptableObject.CreateInstance<LonglineDef>();
            _made.Add(Def);
            Def.Id = "longline.fixture";
            Def.HooksPerTub = 10;
            Def.TubLengthMeters = 100f;
            Def.MaxTubsPerSet = 3;
            Def.MinSetDepthMeters = 3f;
            Def.MaxSetDepthMeters = 40f;
            Def.SoakHoursToPeak = 6f;
            Def.StripHours = 12f;
            Def.HooksPerBaitUnit = 5;
            Def.BaitFavourMultiplier = 3;
            Def.PartStrain01 = 0.95f;
            Def.DeckWorkId = "deckwork.fixture";
        }

        public PlacedLonglineDto Line(double setTime = 0d) => new PlacedLonglineDto
        {
            InstanceId = "line.fixture", LonglineDefId = Def.Id, Region = "region.fixture",
            EndAX = 0f, EndAY = 0f, EndBX = 300f, EndBY = 0f,
            SetGameTimeSeconds = setTime, BaitId = "bait.fixture", Tubs = 3,
        };

        public FishSpeciesDef Fish(string id, FishDepthBand bands = FishDepthBand.Inshore)
        {
            var fish = ScriptableObject.CreateInstance<FishSpeciesDef>();
            _made.Add(fish);
            fish.Id = id;
            fish.DisplayName = id;
            fish.RegionIds = new[] { "region.fixture" };
            fish.AllowedGear = Gear.Longline;
            fish.Seasons = SeasonMask.AllYear;
            fish.MinTide = -100f;
            fish.MaxTide = 100f;
            fish.StartHour = 0f;
            fish.EndHour = 24f;
            fish.DepthBands = bands;
            fish.MinWeightKg = 1f;
            fish.MaxWeightKg = 2f;
            fish.SpawnWeight = 1f;
            return fish;
        }

        public LonglineHookResult[] Haul(PlacedLonglineDto line, double haulTime,
                                        IReadOnlyList<FishSpeciesDef> pool, string[] favours = null)
        {
            int count = line.Tubs * Def.HooksPerTub;
            var results = new LonglineHookResult[count];
            for (int i = 0; i < count; i++)
                results[i] = LonglineCatch.ResolveHook(Def, in line, i, haulTime, 4242, pool, favours,
                    Environment, Terrain, ContextAt, in Depth);
            return results;
        }

        private CatchContext ContextAt(Vector2 hook, double time)
            => new CatchContext("region.deliberately_not_the_line", Environment.TideHeightAt(time),
                (float)((time / 3600d) % 24d), Season.HighSummer, Gear.Trap, -99f, -99f);

        public void Dispose()
        {
            foreach (Object made in _made) Object.DestroyImmediate(made);
        }
    }
}
