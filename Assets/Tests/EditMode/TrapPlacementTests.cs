using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Economy;
using HiddenHarbours.Fishing;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// The depth-gated placement (trap-fishing arc Build 4): a trap may be set only where the water is deep
    /// enough for its <see cref="TrapDef.MinSoakDepthMeters"/> (the inverse of the clam dig's exposure gate),
    /// and — through the service — only if its bait is in stock (consuming one). The pure gate maths are
    /// engine-free; the service path drives fake clock/env/terrain/save. Deterministic, no RNG (rule 5).
    /// </summary>
    public class TrapPlacementTests
    {
        // ---- the pure depth gate ---------------------------------------------------------------

        [Test]
        public void IsDeepEnough_RequiresTheMinSoakDepth()
        {
            Assert.IsTrue(TrapPlacement.IsDeepEnough(3.5f, 3f), "3.5 m of water clears a 3 m min");
            Assert.IsTrue(TrapPlacement.IsDeepEnough(3f, 3f), "exactly at the min is deep enough");
            Assert.IsFalse(TrapPlacement.IsDeepEnough(2.9f, 3f), "a shade shallow is too shoal");
            Assert.IsFalse(TrapPlacement.IsDeepEnough(0f, 3f), "the waterline (dry) is not deep enough");
            Assert.IsFalse(TrapPlacement.IsDeepEnough(-1f, 3f), "bared ground (negative depth) is not deep enough");
        }

        [Test]
        public void IsDeepEnough_ZeroMin_JustNeedsSubmergedWater()
        {
            Assert.IsTrue(TrapPlacement.IsDeepEnough(0.1f, 0f), "any submerged water clears a zero min");
            Assert.IsFalse(TrapPlacement.IsDeepEnough(0f, 0f), "but dry ground does not");
        }

        // ---- the service-level gated placement (depth + bait) ----------------------------------

        private sealed class FakeClock : IGameClock
        {
            public double Seconds;
            public double TotalSeconds => Seconds;
            public GameTime Now => default;
            public Season Season => Season.HighSummer;
            public int Year => 1;
            public int DayIndex => 0;
            public int DayOfSeason => 1;
            public Weekday Weekday => default;
            public bool IsMarketDay => false;
            public float HourOfDay => 12f;
            public float DayFraction => 0.5f;
            public bool IsPaused { get; set; }
            public float TimeScale { get; set; } = 1f;
            public void SeekTo(double totalSeconds) => Seconds = totalSeconds;
        }

        // A fake environment whose WATER LEVEL is a settable constant (so depth = level − elevation is exact).
        private sealed class FakeEnv : IEnvironmentService
        {
            public int Seed = 4242;
            public float Level = 4f;
            public int WorldSeed => Seed;
            public TideProfile ActiveTideProfile { get; set; }
            public EnvironmentSample Sample() => default;
            public float TideHeightAt(double totalSeconds) => Level;
            public float WaterLevelAt(double totalSeconds) => Level;
        }

        // A fake terrain of one flat elevation everywhere (depth = env.Level − Elevation).
        private sealed class FlatTerrain : ITidalTerrain
        {
            public float Elevation;
            public float ElevationAt(Vector2 worldPos) => Elevation;
        }

        private sealed class FakeSaveService : ISaveService
        {
            private readonly Dictionary<string, bool> _flags = new();
            public FakeSaveService(SaveData data) { Current = data; }
            public SaveData Current { get; }
            public bool GetFlag(string key) => _flags.TryGetValue(key, out var v) && v;
            public void SetFlag(string key, bool value) => _flags[key] = value;
            public void Save() { }
        }

        private readonly List<Object> _spawned = new();
        private FakeClock _clock;
        private FakeEnv _env;
        private FlatTerrain _terrain;
        private SaveData _save;
        private TrapDef _trap;
        private BaitDef _bait;

        [SetUp]
        public void SetUp()
        {
            EventBus.Clear<TrapPlaced>();
            GameServices.Reset();

            _clock = new FakeClock { Seconds = 1000.0 };
            _env = new FakeEnv { Level = 4f };            // 4 m of water above datum
            _terrain = new FlatTerrain { Elevation = 0f };// flat seabed at datum → depth 4 m
            _save = SaveMigration.NewGame();

            GameServices.Clock = _clock;
            GameServices.Environment = _env;
            GameServices.TidalTerrain = _terrain;
            GameServices.Save = new FakeSaveService(_save);

            _trap = ScriptableObject.CreateInstance<TrapDef>();
            _trap.Id = "trap.lobster"; _trap.DisplayName = "Lobster Pot";
            _trap.AllowedCatchFishIds = new[] { "fish.lobster" };
            _trap.RequiredBaitId = "bait.herring";
            _trap.SoakHours = 12f; _trap.MinSoakDepthMeters = 3f; _trap.MaxSoakDepthMeters = 40f;
            _spawned.Add(_trap);

            _bait = ScriptableObject.CreateInstance<BaitDef>();
            _bait.Id = "bait.herring"; _bait.DisplayName = "Herring";
            _spawned.Add(_bait);
        }

        [TearDown]
        public void TearDown()
        {
            EventBus.Clear<TrapPlaced>();
            GameServices.Reset();
            foreach (var o in _spawned) if (o != null) Object.DestroyImmediate(o);
            _spawned.Clear();
        }

        private PlacedTrapService MakeService()
        {
            var go = new GameObject("PlacedTrapService");
            _spawned.Add(go);
            var svc = go.AddComponent<PlacedTrapService>();
            svc.Configure(new[] { _trap }, new[] { _bait }, go.transform);
            return svc;
        }

        [Test]
        public void CanPlaceAt_DeepWater_True_ShoalWater_False()
        {
            // Deep flat: level 4 − elevation 0 = 4 m ≥ 3 → placeable.
            Assert.IsTrue(TrapPlacement.CanPlaceAt(_trap, _env, _terrain, 0.0, Vector2.zero));

            // Raise the seabed to 2 m → depth 2 m < 3 → too shoal.
            _terrain.Elevation = 2f;
            Assert.IsFalse(TrapPlacement.CanPlaceAt(_trap, _env, _terrain, 0.0, Vector2.zero));

            // Bare it above the water → depth negative → dry, not placeable.
            _terrain.Elevation = 5f;
            Assert.IsFalse(TrapPlacement.CanPlaceAt(_trap, _env, _terrain, 0.0, Vector2.zero));
        }

        [Test]
        public void CanPlaceAt_NullTerrainOrEnv_IsFalse_SafeDefault()
        {
            Assert.IsFalse(TrapPlacement.CanPlaceAt(_trap, null, _terrain, 0.0, Vector2.zero));
            Assert.IsFalse(TrapPlacement.CanPlaceAt(_trap, _env, null, 0.0, Vector2.zero));
            Assert.IsFalse(TrapPlacement.CanPlaceAt(null, _env, _terrain, 0.0, Vector2.zero));
        }

        [Test]
        public void TryPlaceGated_DeepAndBaited_Places_AndConsumesBait()
        {
            _save.BaitStock.Add(new BaitStock("bait.herring", 2));
            var svc = MakeService();

            var result = svc.TryPlaceGated(_trap, _bait, Vector2.zero, "region.st_peters", out PlacedTrap placed);

            Assert.AreEqual(PlacedTrapService.PlaceResult.Placed, result);
            Assert.IsNotNull(placed, "the live trap was returned");
            Assert.AreEqual(1, svc.Live.Count, "the trap is down");
            Assert.AreEqual(1, _save.PlacedTraps.Count, "the placement mirrored into the save");
            Assert.AreEqual(1, _save.BaitStock[0].Count, "one bait consumed (2 → 1)");
        }

        [Test]
        public void TryPlaceGated_TooShallow_Refuses_NothingPlacedNoBaitSpent()
        {
            _save.BaitStock.Add(new BaitStock("bait.herring", 2));
            _terrain.Elevation = 2f;   // depth 2 m < 3 → too shoal
            var svc = MakeService();

            var result = svc.TryPlaceGated(_trap, _bait, Vector2.zero, "region.st_peters", out PlacedTrap placed);

            Assert.AreEqual(PlacedTrapService.PlaceResult.TooShallow, result);
            Assert.IsNull(placed);
            Assert.AreEqual(0, svc.Live.Count, "nothing placed in shoal water");
            Assert.AreEqual(0, _save.PlacedTraps.Count);
            Assert.AreEqual(2, _save.BaitStock[0].Count, "no bait spent on a refused placement");
        }

        [Test]
        public void TryPlaceGated_NoBaitInStock_Refuses()
        {
            // Deep water, but the locker is empty of herring.
            var svc = MakeService();

            var result = svc.TryPlaceGated(_trap, _bait, Vector2.zero, "region.st_peters", out PlacedTrap placed);

            Assert.AreEqual(PlacedTrapService.PlaceResult.NoBait, result);
            Assert.IsNull(placed);
            Assert.AreEqual(0, svc.Live.Count, "no pot set without bait to arm it");
        }
    }
}
