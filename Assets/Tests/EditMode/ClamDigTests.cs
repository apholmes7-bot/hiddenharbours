using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Fishing;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// St Peters CLAM DIG (P4 by-hand): at a clam-hole spot, when the flats are bared by the tide, one
    /// Interact digs a single clam into the bucket — gated by exposure (tide), owning the shovel, and the
    /// bucket having room. Reuses the clam FishSpeciesDef + the FishCaught land path; no rod FSM. Drives
    /// TryDig() directly with fake terrain/env/save/hold — no scene.
    /// </summary>
    public class ClamDigTests
    {
        private sealed class FakeHold : IHold
        {
            private readonly List<CatchItem> _items = new();
            public int Capacity = 20;
            public int CapacityUnits => Capacity;
            public int UsedUnits => _items.Count;
            public IReadOnlyList<CatchItem> Items => _items;
            public bool TryAdd(CatchItem item) { if (_items.Count >= Capacity) return false; _items.Add(item); return true; }
            public void Clear() => _items.Clear();
        }

        private sealed class FlatTerrain : ITidalTerrain
        {
            public float Elevation;
            public float ElevationAt(Vector2 worldPos) => Elevation;
        }

        private sealed class FlatEnv : IEnvironmentService
        {
            public float Level;
            public int WorldSeed => 0;
            public TideProfile ActiveTideProfile { get; set; }
            public EnvironmentSample Sample() => default;
            public float TideHeightAt(double totalSeconds) => Level;
            public float WaterLevelAt(double totalSeconds) => Level;
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
        private int _caught;
        private void OnCaught(FishCaught e) => _caught++;

        private FlatTerrain _terrain;
        private FlatEnv _env;
        private SaveData _save;

        [SetUp]
        public void SetUp()
        {
            _caught = 0;
            EventBus.Clear<FishCaught>();
            EventBus.Subscribe<FishCaught>(OnCaught);
            GameServices.Reset();

            _terrain = new FlatTerrain { Elevation = 1.0f };   // the flat sits 1.0 m above datum
            _env = new FlatEnv { Level = 0.5f };               // low water 0.5 m → the flat is bared (exposed)
            _save = SaveMigration.NewGame();
            _save.OwnedGear.Add("gear.shovel");                // owns the shovel by default in these tests

            GameServices.TidalTerrain = _terrain;
            GameServices.Environment = _env;
            GameServices.Save = new FakeSaveService(_save);
        }

        [TearDown]
        public void TearDown()
        {
            EventBus.Unsubscribe<FishCaught>(OnCaught);
            EventBus.Clear<FishCaught>();
            GameServices.Reset();
            foreach (var o in _spawned) if (o != null) Object.DestroyImmediate(o);
            _spawned.Clear();
        }

        private FishSpeciesDef MakeClam()
        {
            var f = ScriptableObject.CreateInstance<FishSpeciesDef>();
            f.Id = "fish.soft_shell_clam"; f.DisplayName = "Soft-shell Clam";
            f.Category = FishCategory.Shellfish;
            f.MinWeightKg = 0.05f; f.MaxWeightKg = 0.2f; f.BaseValue = 2; f.SupplyElasticity = 0.45f;
            _spawned.Add(f);
            return f;
        }

        private ClamDig MakeDig(IHold bucket, FishSpeciesDef clam, int seed = 7)
        {
            var go = new GameObject("ClamHole");
            _spawned.Add(go);
            go.transform.position = Vector2.zero;          // spot at origin; terrain is flat anyway
            var d = go.AddComponent<ClamDig>();
            d.Configure(clam, bucket, go.transform, "gear.shovel", seed);
            return d;
        }

        [Test]
        public void Dig_WhenExposed_WithShovel_AddsOneClam()
        {
            var hold = new FakeHold();
            var dig = MakeDig(hold, MakeClam());

            Assert.IsTrue(dig.IsExposedNow(), "low water bares the flat");
            Assert.IsTrue(dig.TryDig(), "a dig on the bared flat with a shovel succeeds");
            Assert.AreEqual(1, hold.UsedUnits, "one clam in the bucket");
            Assert.AreEqual(1, _caught, "FishCaught fired (same land path as the rod)");
            Assert.AreEqual("fish.soft_shell_clam", hold.Items[0].SpeciesId);
        }

        [Test]
        public void Dig_WhenSubmerged_YieldsNothing()
        {
            _env.Level = 2.0f;   // tide floods over the 1.0 m flat → not exposed
            var hold = new FakeHold();
            var dig = MakeDig(hold, MakeClam());

            Assert.IsFalse(dig.IsExposedNow(), "the flat is under water at high tide");
            Assert.IsFalse(dig.TryDig(), "you can't dig submerged ground");
            Assert.AreEqual(0, hold.UsedUnits);
            Assert.AreEqual(0, _caught);
        }

        [Test]
        public void Dig_WithoutShovel_YieldsNothing()
        {
            _save.OwnedGear.Remove("gear.shovel");   // no shovel
            var hold = new FakeHold();
            var dig = MakeDig(hold, MakeClam());

            Assert.IsFalse(dig.TryDig(), "no shovel → no dig");
            Assert.AreEqual(0, hold.UsedUnits);
        }

        [Test]
        public void Dig_FullBucket_RefusesAndCapsAt20()
        {
            var hold = new FakeHold { Capacity = 20 };
            var dig = MakeDig(hold, MakeClam());

            // Dig 20 clams (the cap), then a 21st must refuse.
            for (int i = 0; i < 20; i++) Assert.IsTrue(dig.TryDig(), $"dig {i + 1} should succeed");
            Assert.AreEqual(20, hold.UsedUnits, "the bucket caps at 20");
            Assert.IsFalse(dig.TryDig(), "the 21st dig refuses — head to Greywick and sell");
            Assert.AreEqual(20, hold.UsedUnits, "still 20 — never over the cap");
        }

        [Test]
        public void Dig_NoTerrain_TreatedAsOpenWater_NotDiggable()
        {
            GameServices.TidalTerrain = null;   // open water — no bared flat
            var hold = new FakeHold();
            var dig = MakeDig(hold, MakeClam());

            Assert.IsFalse(dig.IsExposedNow(), "no height map → submerged default");
            Assert.IsFalse(dig.TryDig());
        }
    }
}
