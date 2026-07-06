using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Economy;
using HiddenHarbours.Fishing;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// The rhythm-haul CONTROLLER end-to-end at the seam level (trap-fishing arc Build 4): the ready-gate
    /// (a soaked trap surfaces + LANDS its catch, an unsoaked one comes up EMPTY and stays down), and that
    /// enough on-beat pulls surface the pot. The scoring MATH is pinned in TrapHaulMathTests and the catch
    /// determinism in PlacedTrapRuntimeTests — this drives the live controller/service against fakes (glass
    /// sea → every pull is on the beat, so the rhythm is deterministic here). No scene, no input, no Time.
    /// </summary>
    public class TrapHaulControllerTests
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

        // A glass-calm env (default sample → SeaState01 = 0 → the swell envelope is 0 → every pull is on the
        // beat), so the controller's rhythm is deterministic in the test.
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
        private TrapDef _trap;
        private BaitDef _bait;

        private const double PlaceTime = 5000.0;
        private static readonly double SoakSpan = 12.0 * 3600.0;   // TrapDef.SoakHours = 12

        [SetUp]
        public void SetUp()
        {
            EventBus.Clear<FishCaught>();
            EventBus.Clear<TrapPlaced>();
            EventBus.Clear<TrapRemoved>();
            EventBus.Clear<TrapHaulStateChanged>();
            GameServices.Reset();
            FishSpeciesRegistry.Reset();

            _clock = new FakeClock { Seconds = PlaceTime };
            _env = new FakeEnv { Seed = 4242 };
            _save = SaveMigration.NewGame();

            GameServices.Clock = _clock;
            GameServices.Environment = _env;
            GameServices.Save = new FakeSaveService(_save);

            FishSpeciesRegistry.Register(MakeSpecies("fish.lobster", "Lobster", 40));

            _trap = ScriptableObject.CreateInstance<TrapDef>();
            _trap.Id = "trap.lobster"; _trap.DisplayName = "Lobster Pot";
            _trap.AllowedCatchFishIds = new[] { "fish.lobster" };
            _trap.RequiredBaitId = "bait.herring";
            _trap.SoakHours = 12f; _trap.MinSoakDepthMeters = 3f; _trap.MaxSoakDepthMeters = 40f;
            _spawned.Add(_trap);

            _bait = ScriptableObject.CreateInstance<BaitDef>();
            _bait.Id = "bait.herring"; _bait.DisplayName = "Herring";
            _bait.FavorsSpeciesIds = new[] { "fish.lobster" };
            _spawned.Add(_bait);
        }

        [TearDown]
        public void TearDown()
        {
            EventBus.Clear<FishCaught>();
            EventBus.Clear<TrapPlaced>();
            EventBus.Clear<TrapRemoved>();
            EventBus.Clear<TrapHaulStateChanged>();
            GameServices.Reset();
            FishSpeciesRegistry.Reset();
            foreach (var o in _spawned) if (o != null) Object.DestroyImmediate(o);
            _spawned.Clear();
        }

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

        private PlacedTrapService MakeService()
        {
            var go = new GameObject("PlacedTrapService");
            _spawned.Add(go);
            var svc = go.AddComponent<PlacedTrapService>();
            svc.Configure(new[] { _trap }, new[] { _bait }, go.transform);
            return svc;
        }

        // A haul controller wired to the service, with the rail AT the trap (in reach) and a big per-pull gain
        // + no debounce so the test surfaces the pot in a few direct Pull() calls. Catch region matches the
        // registered species (region.coddle_cove).
        private TrapHaulController MakeController(PlacedTrapService svc, FakeHold hold, Vector2 railPos)
        {
            var railGo = new GameObject("Rail");
            railGo.transform.position = railPos;
            _spawned.Add(railGo);

            var go = new GameObject("TrapHaul");
            _spawned.Add(go);
            var ctrl = go.AddComponent<TrapHaulController>();
            ctrl.Configure(svc, railGo.transform, hold, "region.coddle_cove",
                           maxGainPerPull: 0.5f, pullCooldownSeconds: 0f);
            return ctrl;
        }

        private PlacedTrap PlaceAt(PlacedTrapService svc, Vector2 pos)
            => svc.PlaceTrap(_trap, _bait, pos, "region.st_peters");

        [Test]
        public void ReadyTrap_HauledToSurface_LandsTheCatch_AndTrapLeaves()
        {
            var svc = MakeService();
            PlacedTrap trap = PlaceAt(svc, new Vector2(1f, 1f));
            _clock.Seconds = PlaceTime + SoakSpan;              // soaked → ready

            var hold = new FakeHold();
            var ctrl = MakeController(svc, hold, new Vector2(1f, 1f));   // rail on the buoy → in reach

            int caught = 0;
            void OnCaught(FishCaught _) => caught++;
            EventBus.Subscribe<FishCaught>(OnCaught);

            Assert.IsTrue(ctrl.TryStartHaul(), "a pot alongside starts a haul");
            // Glass sea → every pull is on the beat; 0.5 gain/pull → 2 pulls surface it.
            ctrl.Pull();
            ctrl.Pull();
            EventBus.Unsubscribe<FishCaught>(OnCaught);

            Assert.IsFalse(ctrl.IsHauling, "the haul ended on surface");
            Assert.AreEqual(1, hold.UsedUnits, "the catch landed in the hold");
            Assert.AreEqual(1, caught, "FishCaught fired (same land path as the rod)");
            Assert.AreEqual(0, svc.Live.Count, "the trap left the world after surfacing");
        }

        [Test]
        public void UnreadyTrap_SurfacesEmpty_AndStaysDown()
        {
            var svc = MakeService();
            PlaceAt(svc, new Vector2(2f, 0f));
            _clock.Seconds = PlaceTime + SoakSpan * 0.25;       // still soaking → NOT ready

            var hold = new FakeHold();
            var ctrl = MakeController(svc, hold, new Vector2(2f, 0f));

            var phases = new List<TrapHaulPhase>();
            void OnState(TrapHaulStateChanged e) => phases.Add(e.State.Phase);
            EventBus.Subscribe<TrapHaulStateChanged>(OnState);

            Assert.IsTrue(ctrl.TryStartHaul());
            ctrl.Pull();
            ctrl.Pull();   // surfaces (line reaches 1) — but the trap wasn't ready
            EventBus.Unsubscribe<TrapHaulStateChanged>(OnState);

            Assert.AreEqual(0, hold.UsedUnits, "an unready pot lands nothing");
            Assert.AreEqual(1, svc.Live.Count, "the unready trap stays down to keep soaking");
            Assert.Contains(TrapHaulPhase.Empty, phases, "the haul reported the Empty (not-ready) beat");
        }

        [Test]
        public void NoTrapInReach_DoesNotStart()
        {
            var svc = MakeService();
            PlaceAt(svc, new Vector2(50f, 50f));                // far away
            _clock.Seconds = PlaceTime + SoakSpan;

            var hold = new FakeHold();
            var ctrl = MakeController(svc, hold, new Vector2(0f, 0f));   // rail far from the buoy

            Assert.IsFalse(ctrl.TryStartHaul(), "no pot in reach → no haul starts");
            Assert.IsFalse(ctrl.IsHauling);
        }

        [Test]
        public void Pull_AddsLine_TowardTheSurface()
        {
            var svc = MakeService();
            PlaceAt(svc, new Vector2(0f, 0f));
            _clock.Seconds = PlaceTime + SoakSpan;

            var hold = new FakeHold();
            var ctrl = MakeController(svc, hold, new Vector2(0f, 0f));

            ctrl.TryStartHaul();
            Assert.AreEqual(0f, ctrl.Line01, 1e-6f, "starts at the bottom");
            ctrl.Pull();
            Assert.Greater(ctrl.Line01, 0f, "an on-beat pull wins line");
        }
    }
}
