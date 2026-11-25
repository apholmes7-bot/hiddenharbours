using System.Collections.Generic;
using System.Reflection;
using HiddenHarbours.Core;
using HiddenHarbours.Fishing;
using NUnit.Framework;
using UnityEngine;

namespace HiddenHarbours.Tests.EditMode
{
    public class CatchRollDeterminismTests
    {
        private const string RegionId = "region.catch_test";
        private const string HoleId = "fixture.clam_test";
        private readonly List<Object> _spawned = new();
        private FakeClock _clock;
        private FakeEnvironment _environment;
        private FishSpeciesDef _clam;

        private sealed class FakeHold : IHold
        {
            private readonly List<CatchItem> _items = new();
            public int CapacityUnits => 20;
            public int UsedUnits => _items.Count;
            public IReadOnlyList<CatchItem> Items => _items;
            public bool TryAdd(CatchItem item) { _items.Add(item); return true; }
            public void Clear() => _items.Clear();
        }

        private sealed class FakeClock : IGameClock
        {
            public double Seconds = 12345.125;
            public double TotalSeconds => Seconds;
            public GameTime Now => new GameTime(Seconds);
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
        }

        private sealed class FakeEnvironment : IEnvironmentService
        {
            public int Seed = 4242;
            public int WorldSeed => Seed;
            public TideProfile ActiveTideProfile { get; set; }
            public EnvironmentSample Sample() => default;
            public float TideHeightAt(double seconds) => 0f;
            public float WaterLevelAt(double seconds) => 0f;
        }

        private sealed class FlatTerrain : ITidalTerrain
        {
            public float ElevationAt(Vector2 position) => 1f;
        }

        private sealed class FakeSave : ISaveService
        {
            public SaveData Current { get; } = SaveMigration.NewGame();
            public bool GetFlag(string key) => false;
            public void SetFlag(string key, bool value) { }
            public void Save() { }
        }

        [SetUp]
        public void SetUp()
        {
            GameServices.Reset();
            _clock = new FakeClock();
            _environment = new FakeEnvironment();
            GameServices.Clock = _clock;
            GameServices.Environment = _environment;
            GameServices.TidalTerrain = new FlatTerrain();
            var save = new FakeSave();
            save.Current.OwnedGear.Add("gear.shovel");
            GameServices.Save = save;
            _clam = ScriptableObject.CreateInstance<FishSpeciesDef>();
            _spawned.Add(_clam);
            _clam.Id = "fish.catch_test";
            _clam.DisplayName = "Test catch";
            _clam.Category = FishCategory.Shellfish;
            _clam.MinWeightKg = 0.05f;
            _clam.MaxWeightKg = 0.2f;
            _clam.RegionIds = new[] { RegionId };
            _clam.AllowedGear = Gear.Handline;
            _clam.Seasons = SeasonMask.AllYear;
            _clam.MinTide = -10f;
            _clam.MaxTide = 10f;
            _clam.StartHour = 0f;
            _clam.EndHour = 24f;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (Object item in _spawned)
                if (item != null) Object.DestroyImmediate(item);
            _spawned.Clear();
            GameServices.Reset();
        }

        private T Make<T>() where T : Component
        {
            var go = new GameObject(typeof(T).Name);
            go.SetActive(false);
            _spawned.Add(go);
            return go.AddComponent<T>();
        }

        private FishingController Rod(int seed, IHold hold = null)
        {
            var rod = Make<FishingController>();
            rod.Configure(hold ?? new FakeHold(), new[] { _clam }, RegionId, Gear.Handline, seed);
            return rod;
        }

        private ClamDig Hole(int seed, IHold hold = null, string id = HoleId)
        {
            var hole = Make<ClamDig>();
            hole.ConfigureInteract(id, null, landInHand: false);
            hole.Configure(_clam, hold ?? new FakeHold(), hole.transform, "gear.shovel", seed);
            return hole;
        }

        private static T Field<T>(object target, string name)
            => (T)target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(target);

        private static int[] Trace(System.Random random)
        {
            var trace = new int[16];
            for (int i = 0; i < trace.Length; i++) trace[i] = random.Next();
            return trace;
        }

        [TestCase(0, "", 0.0, 1870016737)]
        [TestCase(4242, "rod:region.catch_test", 12345.125, 1433507006)]
        [TestCase(4242, "clam:fixture.clam_test", 12345.125, -1149428879)]
        [TestCase(-19, "trap.test", 5000.000001, 1274957044)]
        [TestCase(int.MaxValue, "fish.test", 123456789.25, -1986469563)]
        public void CatchSeed_MatchesGoldenVectorsAndLegacyTrapSeed(int world, string id, double time, int expected)
        {
            Assert.AreEqual(expected, StableHash.CatchSeed(world, id, time));
            Assert.AreEqual(expected, StableHash.TrapCatchSeed(world, id, time));
        }

        // EditMode has no scene lifecycle. Invoke the actual Awake to cover both serialized entry
        // points as well as Configure, including the exact legacy stream for positive/negative seeds.
        [TestCase(false, false, 0)]
        [TestCase(false, true, 0)]
        [TestCase(true, false, 0)]
        [TestCase(true, true, 0)]
        [TestCase(false, false, 7)]
        [TestCase(false, true, 7)]
        [TestCase(true, false, 7)]
        [TestCase(true, true, 7)]
        [TestCase(false, false, -19)]
        [TestCase(false, true, -19)]
        [TestCase(true, false, -19)]
        [TestCase(true, true, -19)]
        public void Initialization_UsesWorldTimeOrExactExplicitSeed(bool clam, bool awake, int seed)
        {
            object target = clam ? (object)Hole(seed) : Rod(seed);
            if (awake)
                target.GetType().GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(target, null);
            int expected = seed != 0 ? seed : StableHash.CatchSeed(_environment.Seed,
                clam ? "clam:" + HoleId : "rod:" + RegionId, _clock.Seconds);
            CollectionAssert.AreEqual(Trace(new System.Random(expected)), Trace(Field<System.Random>(target, "_rng")));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ZeroSeed_RecreationUsesTheSameStream(bool clam)
        {
            object first = clam ? (object)Hole(0) : Rod(0);
            object restored = clam ? (object)Hole(0) : Rod(0);
            CollectionAssert.AreEqual(Trace(Field<System.Random>(first, "_rng")),
                Trace(Field<System.Random>(restored, "_rng")));
        }

        [TestCase(false, false)]
        [TestCase(false, true)]
        [TestCase(true, false)]
        [TestCase(true, true)]
        public void ZeroSeed_ChangingWorldOrFractionalTimeChangesStream(bool clam, bool changeTime)
        {
            object first = clam ? (object)Hole(0) : Rod(0);
            if (changeTime) _clock.Seconds += 0.000001;
            else _environment.Seed++;
            object changed = clam ? (object)Hole(0) : Rod(0);
            CollectionAssert.AreNotEqual(Trace(Field<System.Random>(first, "_rng")),
                Trace(Field<System.Random>(changed, "_rng")));
        }

        [Test]
        public void ZeroSeed_DifferentHoleIdsHaveDifferentStreams()
        {
            CollectionAssert.AreNotEqual(Trace(Field<System.Random>(Hole(0), "_rng")),
                Trace(Field<System.Random>(Hole(0, id: "fixture.other_hole"), "_rng")));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Rod_ZeroSeed_ReadsLateServicesAtCastOrDrop(bool weighted)
        {
            GameServices.Environment = null;
            GameServices.Clock = null;
            var rod = Rod(0);
            Trace(Field<System.Random>(rod, "_rng")); // Previous local history must not affect this action.
            GameServices.Environment = _environment;
            GameServices.Clock = _clock;
            if (weighted) rod.ConfigureDepthDrop(1f, 10f);
            rod.Tick(0.02f, true, new Vector2(0f, -0.5f), true);
            Assert.AreEqual(weighted ? FishingPhase.Sinking : FishingPhase.WindBack, rod.Phase);
            int expected = StableHash.CatchSeed(_environment.Seed, "rod:" + RegionId, _clock.Seconds);
            CollectionAssert.AreEqual(Trace(new System.Random(expected)), Trace(Field<System.Random>(rod, "_rng")));
        }

        [Test]
        public void Clam_ZeroSeed_ReadsLateServicesAtDigAndReplaysWeight()
        {
            GameServices.Environment = null;
            GameServices.Clock = null;
            var hold = new FakeHold();
            var hole = Hole(0, hold);
            Trace(Field<System.Random>(hole, "_rng"));
            GameServices.Environment = _environment;
            GameServices.Clock = _clock;
            Assert.IsTrue(hole.TryDig());
            Assert.AreEqual(1, hold.UsedUnits);
            int seed = StableHash.CatchSeed(_environment.Seed, "clam:" + HoleId, _clock.Seconds);
            Assert.AreEqual(CatchResolver.RollWeight(_clam, new System.Random(seed)), hold.Items[0].WeightKg);

            var restoredHold = new FakeHold();
            Assert.IsTrue(Hole(0, restoredHold).TryDig());
            Assert.AreEqual(1, restoredHold.UsedUnits);
            Assert.AreEqual(hold.Items[0].WeightKg, restoredHold.Items[0].WeightKg);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ExplicitSeed_ActionKeepsTheExistingStream(bool clam)
        {
            object target = clam ? (object)Hole(7) : Rod(7);
            var random = Field<System.Random>(target, "_rng");
            var expected = new System.Random(7);
            Assert.AreEqual(expected.Next(), random.Next());
            _clock.Seconds += 100;
            _environment.Seed++;
            if (clam)
            {
                Assert.IsTrue(((ClamDig)target).TryDig());
                CatchResolver.RollWeight(_clam, expected);
            }
            else ((FishingController)target).Tick(0.02f, true, new Vector2(0f, -0.5f), true);
            Assert.AreSame(random, Field<System.Random>(target, "_rng"));
            CollectionAssert.AreEqual(Trace(expected), Trace(random));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Rod_ZeroSeed_RecreatedCatchReplaysCastOrDrop(bool weighted)
        {
            float[] CatchTrace()
            {
                var hold = new FakeHold();
                var rod = Rod(0, hold);
                // The rod needs open water; the clam tests instead use the exposed flat.
                GameServices.TidalTerrain = null;
                if (weighted)
                {
                    rod.ConfigureDepthDrop(1f, 10f);
                    rod.Tick(0.02f, true, Vector2.zero, true);
                }
                else FlickGestures.CastLine(rod);
                var trace = new List<float>();
                for (int i = 0; i < 2400 && rod.Phase != FishingPhase.Landed; i++)
                {
                    trace.Add((float)rod.Phase);
                    rod.Tick(0.05f, true);
                }
                Assert.AreEqual(FishingPhase.Landed, rod.Phase);
                Assert.AreEqual(1, hold.UsedUnits);
                trace.Add(hold.Items[0].WeightKg);
                return trace.ToArray();
            }
            CollectionAssert.AreEqual(CatchTrace(), CatchTrace());
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ZeroSeed_WithoutServicesHasAStableZeroWorldFallback(bool clam)
        {
            GameServices.Environment = null;
            GameServices.Clock = null;
            object target = clam ? (object)Hole(0) : Rod(0);
            int expected = StableHash.CatchSeed(0, clam ? "clam:" + HoleId : "rod:" + RegionId, 0.0);
            CollectionAssert.AreEqual(Trace(new System.Random(expected)), Trace(Field<System.Random>(target, "_rng")));
        }
    }
}
