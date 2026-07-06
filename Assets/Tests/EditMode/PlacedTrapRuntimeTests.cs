using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Economy;
using HiddenHarbours.Fishing;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// The placed-trap RUNTIME end-to-end (trap-fishing arc Build 3): the deterministic catch survives a
    /// save→load (the headline guarantee — "reload = identical catch"), the soak gate blocks a haul until
    /// the trap has soaked, the runtime id→def registry resolves the catch pool, and the service mirrors
    /// placements into the save + restores them off the load edge. Drives the service/trap directly with
    /// fake clock/env/save/hold — no scene, no play lifecycle.
    /// </summary>
    public class PlacedTrapRuntimeTests
    {
        private sealed class FakeHold : IHold
        {
            private readonly List<CatchItem> _items = new();
            public int Capacity = 6;
            public int CapacityUnits => Capacity;
            public int UsedUnits => _items.Count;
            public IReadOnlyList<CatchItem> Items => _items;
            public bool TryAdd(CatchItem item) { if (_items.Count >= Capacity) return false; _items.Add(item); return true; }
            public void Clear() => _items.Clear();
        }

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

        private sealed class FakeEnv : IEnvironmentService
        {
            public int Seed = 4242;
            public int WorldSeed => Seed;
            public TideProfile ActiveTideProfile { get; set; }
            public EnvironmentSample Sample() => default;
            public float TideHeightAt(double totalSeconds) => 2f;
            public float WaterLevelAt(double totalSeconds) => 2f;
        }

        private sealed class FakeSaveService : ISaveService
        {
            public FakeSaveService(SaveData data) { Current = data; }
            public SaveData Current { get; }
            public bool GetFlag(string key) => false;
            public void SetFlag(string key, bool value) { }
            public void Save() { }
        }

        private readonly List<Object> _spawned = new();
        private FakeClock _clock;
        private FakeEnv _env;
        private SaveData _save;

        // Cached Def instances so the service registry and each placement share the SAME objects (restore
        // resolves by id from the registry — with one instance per id there's no ambiguity).
        private TrapDef _lobsterTrap;
        private BaitDef _herring;

        private const double PlaceTime = 5000.0;
        private static readonly double LobsterSoakSpan = 12.0 * 3600.0;   // TrapDef.SoakHours = 12

        [SetUp]
        public void SetUp()
        {
            EventBus.Clear<FishCaught>();
            EventBus.Clear<TrapPlaced>();
            EventBus.Clear<TrapRemoved>();
            GameServices.Reset();
            FishSpeciesRegistry.Reset();

            _clock = new FakeClock { Seconds = PlaceTime };
            _env = new FakeEnv { Seed = 4242 };
            _save = SaveMigration.NewGame();

            GameServices.Clock = _clock;
            GameServices.Environment = _env;
            GameServices.Save = new FakeSaveService(_save);

            _lobsterTrap = MakeLobsterTrap();
            _herring = MakeHerring();
        }

        [TearDown]
        public void TearDown()
        {
            EventBus.Clear<FishCaught>();
            EventBus.Clear<TrapPlaced>();
            EventBus.Clear<TrapRemoved>();
            GameServices.Reset();
            FishSpeciesRegistry.Reset();
            foreach (var o in _spawned) if (o != null) Object.DestroyImmediate(o);
            _spawned.Clear();
        }

        // ---- fixtures --------------------------------------------------------------------------

        private FishSpeciesDef MakeSpecies(string id, string name, int baseValue)
        {
            var f = ScriptableObject.CreateInstance<FishSpeciesDef>();
            f.Id = id; f.DisplayName = name; f.Category = FishCategory.Shellfish;
            f.RegionIds = new[] { "region.coddle_cove" };
            f.AllowedGear = Gear.Trap; f.Seasons = SeasonMask.AllYear;
            f.MinTide = -10f; f.MaxTide = 10f; f.StartHour = 0f; f.EndHour = 24f;
            f.MinWeightKg = 0.5f; f.MaxWeightKg = 1.5f; f.BaseValue = baseValue; f.SupplyElasticity = 0.2f;
            f.SpawnWeight = 1f;
            _spawned.Add(f);
            return f;
        }

        private TrapDef MakeLobsterTrap()
        {
            var t = ScriptableObject.CreateInstance<TrapDef>();
            t.Id = "trap.lobster"; t.DisplayName = "Lobster Pot";
            t.AllowedCatchFishIds = new[] { "fish.lobster", "fish.rock_crab" };
            t.RequiredBaitId = "bait.herring";
            t.SoakHours = 12f; t.CapacityUnits = 4;
            t.MinSoakDepthMeters = 3f; t.MaxSoakDepthMeters = 40f;
            _spawned.Add(t);
            return t;
        }

        private BaitDef MakeHerring()
        {
            var b = ScriptableObject.CreateInstance<BaitDef>();
            b.Id = "bait.herring"; b.DisplayName = "Herring";
            b.FavorsSpeciesIds = new[] { "fish.lobster" };
            _spawned.Add(b);
            return b;
        }

        private void RegisterCatchSpecies()
        {
            FishSpeciesRegistry.Register(MakeSpecies("fish.lobster", "Lobster", 40));
            FishSpeciesRegistry.Register(MakeSpecies("fish.rock_crab", "Crab", 12));
        }

        private PlacedTrapService MakeService()
        {
            var go = new GameObject("PlacedTrapService");
            _spawned.Add(go);
            var svc = go.AddComponent<PlacedTrapService>();
            svc.Configure(new[] { _lobsterTrap }, new[] { _herring }, go.transform);
            return svc;
        }

        private PlacedTrap MakeTrapObject(string instanceId, double placeTime)
        {
            var go = new GameObject("PlacedTrap");
            _spawned.Add(go);
            var trap = go.AddComponent<PlacedTrap>();
            trap.Configure(_lobsterTrap, _herring, instanceId, "region.coddle_cove", placeTime, _env.WorldSeed);
            return trap;
        }

        private static CatchContext TrapContext()
            => new CatchContext("region.coddle_cove", 2f, 12f, Season.HighSummer, Gear.Trap);

        // ---- tests -----------------------------------------------------------------------------

        [Test]
        public void Registry_ResolvesFishIdsToDefs()
        {
            RegisterCatchSpecies();
            var pool = FishSpeciesRegistry.Resolve(new[] { "fish.lobster", "fish.rock_crab", "fish.unknown" });
            Assert.AreEqual(2, pool.Count, "known ids resolve; the unknown id is skipped");
            Assert.AreEqual("fish.lobster", pool[0].Id);
        }

        [Test]
        public void Trap_NotReady_ResolvesNothing_ThenReadyResolvesACatch()
        {
            RegisterCatchSpecies();
            var trap = MakeTrapObject("trap.lobster#1", PlaceTime);
            var ctx = TrapContext();

            double midSoak = PlaceTime + LobsterSoakSpan * 0.5;   // mid-soak → not ready
            Assert.IsFalse(trap.IsReady(midSoak));
            Assert.IsNull(trap.ResolveCatch(midSoak, in ctx), "a not-ready trap resolves no catch");

            double soaked = PlaceTime + LobsterSoakSpan;          // fully soaked → ready
            Assert.IsTrue(trap.IsReady(soaked));
            Assert.IsNotNull(trap.ResolveCatch(soaked, in ctx), "a soaked trap resolves a catch");
        }

        [Test]
        public void SaveLoad_YieldsIdenticalCatch()
        {
            // THE headline guarantee. Place a trap, resolve its catch (pre-save), then reconstruct it from the
            // saved DTO (as a load would) and resolve again — the CatchItem must be identical.
            RegisterCatchSpecies();
            var svc = MakeService();

            PlacedTrap live = svc.PlaceTrap(_lobsterTrap, _herring, new Vector2(3f, -4f), "region.coddle_cove");
            Assert.IsNotNull(live);
            Assert.AreEqual(1, _save.PlacedTraps.Count, "the placement was mirrored into the save");

            double soaked = live.PlacementGameTimeSeconds + LobsterSoakSpan;
            var ctx = TrapContext();
            CatchItem? before = live.ResolveCatch(soaked, in ctx);
            Assert.IsTrue(before.HasValue, "the placed trap resolves a catch once soaked");

            // "Reload": rebuild the live traps purely from the saved DTOs (+ the saved world seed).
            svc.RestoreFromSave(_save);
            Assert.AreEqual(1, svc.Live.Count, "one trap restored from the save");
            PlacedTrap restored = svc.Live[0];

            CatchItem? after = restored.ResolveCatch(soaked, in ctx);
            Assert.IsTrue(after.HasValue, "the restored trap resolves a catch");
            Assert.AreEqual(before.Value.SpeciesId, after.Value.SpeciesId, "same species across save→load");
            Assert.AreEqual(before.Value.WeightKg, after.Value.WeightKg, 1e-6f, "same size across save→load");
            Assert.AreEqual(before.Value.BaseValue, after.Value.BaseValue, "same value across save→load");
        }

        [Test]
        public void Place_ConsumesOneBaitFromStock()
        {
            RegisterCatchSpecies();
            _save.BaitStock.Add(new BaitStock("bait.herring", 2));
            var svc = MakeService();

            svc.PlaceTrap(_lobsterTrap, _herring, new Vector2(0, 0), "region.coddle_cove");
            Assert.AreEqual(1, _save.BaitStock[0].Count, "one bait consumed on placement (2 → 1)");

            svc.PlaceTrap(_lobsterTrap, _herring, new Vector2(1, 1), "region.coddle_cove");
            Assert.AreEqual(0, _save.BaitStock.Count, "the last bait spent → the stock record is dropped");
        }

        [Test]
        public void Haul_LandsCatch_RemovesTrapAndDto()
        {
            RegisterCatchSpecies();
            var svc = MakeService();
            PlacedTrap live = svc.PlaceTrap(_lobsterTrap, _herring, new Vector2(2f, 2f), "region.coddle_cove");

            _clock.Seconds = live.PlacementGameTimeSeconds + LobsterSoakSpan;   // advance past the soak

            int caught = 0;
            void OnCaught(FishCaught _) => caught++;
            EventBus.Subscribe<FishCaught>(OnCaught);

            var hold = new FakeHold();
            bool landed = svc.HaulTrap(live, hold, TrapContext());
            EventBus.Unsubscribe<FishCaught>(OnCaught);

            Assert.IsTrue(landed, "a soaked trap hauls a catch");
            Assert.AreEqual(1, hold.UsedUnits, "the catch landed in the hold");
            Assert.AreEqual(1, caught, "FishCaught fired (same land path as the rod)");
            Assert.AreEqual(0, svc.Live.Count, "the trap left the world after the haul");
            Assert.AreEqual(0, _save.PlacedTraps.Count, "the DTO was dropped from the save");
        }

        [Test]
        public void Haul_BeforeSoak_YieldsNothing_AndLeavesTrapDown()
        {
            RegisterCatchSpecies();
            var svc = MakeService();
            PlacedTrap live = svc.PlaceTrap(_lobsterTrap, _herring, new Vector2(0, 0), "region.coddle_cove");

            _clock.Seconds = live.PlacementGameTimeSeconds + LobsterSoakSpan * 0.25;   // still soaking

            var hold = new FakeHold();
            Assert.IsFalse(svc.HaulTrap(live, hold, TrapContext()), "you can't haul a trap that hasn't soaked");
            Assert.AreEqual(0, hold.UsedUnits);
            Assert.AreEqual(1, svc.Live.Count, "the trap stays down to keep soaking");
            Assert.AreEqual(1, _save.PlacedTraps.Count, "its DTO stays in the save");
        }

        [Test]
        public void EmptyRegistry_SoakedTrap_HaulsEmpty()
        {
            // No species registered → the pool is empty → a soaked trap hauls nothing (graceful, no throw).
            var svc = MakeService();
            PlacedTrap live = svc.PlaceTrap(_lobsterTrap, _herring, new Vector2(0, 0), "region.coddle_cove");
            _clock.Seconds = live.PlacementGameTimeSeconds + LobsterSoakSpan;

            var hold = new FakeHold();
            Assert.IsFalse(svc.HaulTrap(live, hold, TrapContext()), "empty pool → empty haul");
            Assert.AreEqual(0, hold.UsedUnits);
        }

        [Test]
        public void Restore_RepublishesTrapPlaced_ForTheBuoy()
        {
            RegisterCatchSpecies();
            var svc = MakeService();
            svc.PlaceTrap(_lobsterTrap, _herring, new Vector2(7f, 8f), "region.coddle_cove");

            int placedSignals = 0;
            void OnPlaced(TrapPlaced _) => placedSignals++;
            EventBus.Subscribe<TrapPlaced>(OnPlaced);
            svc.RestoreFromSave(_save);   // a load rebuilds the world
            EventBus.Unsubscribe<TrapPlaced>(OnPlaced);

            Assert.AreEqual(1, placedSignals, "each restored trap re-publishes TrapPlaced so the buoy reappears");
        }
    }
}
