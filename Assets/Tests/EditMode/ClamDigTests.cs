using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Fishing;
using HiddenHarbours.Player;   // ToolDef — the shovel the dig now needs IN HAND

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
            public bool RejectAdds;
            public int CapacityUnits => Capacity;
            public int UsedUnits => _items.Count;
            public IReadOnlyList<CatchItem> Items => _items;
            public bool TryAdd(CatchItem item) { if (RejectAdds || _items.Count >= Capacity) return false; _items.Add(item); return true; }
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

            // …AND HOLDS IT. The owner's ruling (2026-08-13) made digging need the shovel IN HAND, not
            // merely owned — so every pre-existing test below, which was written when owning was enough,
            // needs the second precondition to go on testing what it was written to test. Real hands
            // holding a real CarriableTool, not a fake (see ToolInHand on why that distinction matters).
            // The tests that pin the NEW gate deliberately undo this.
            ToolInHand.Holding(ToolDef.ShovelId, _spawned);
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
            // ⚠️ landInHand: FALSE, deliberately. This suite pins the dig's GATES — exposure, ownership,
            // the shovel in hand, bucket room, one-clam-per-press — and asserts them by looking in the
            // pail. Where the clam GOES is a different subject with its own suite (CatchInHandTests), and
            // running it here would mean every assertion below had to stack the clam first just to see it.
            // This is the fallback path that ships wherever no hands are published, so it is a real
            // configuration, not a test-only fiction.
            d.ConfigureInteract($"fixture.clam_hole#{_spawned.Count}", ToolDef.ShovelId, landInHand: false);
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

        // A clock pinned at one instant, for the freshness-stamp check (M1 §7.3).
        private sealed class StampClock : IGameClock
        {
            public double TotalSeconds { get; set; }
            public GameTime Now => new GameTime(TotalSeconds);
            public Season Season => Season.EarlySpring;
            public int Year => 1;
            public int DayIndex => 0;
            public int DayOfSeason => 1;
            public Weekday Weekday => Weekday.Monday;
            public bool IsMarketDay => false;
            public float HourOfDay => 0f;
            public float DayFraction => 0f;
            public bool IsPaused { get; set; }
            public float TimeScale { get; set; } = 1f;
        }

        [Test]
        public void Dig_StampsTheFreshnessClock()
        {
            GameServices.Clock = new StampClock { TotalSeconds = 777d };
            var clam = MakeClam();
            clam.SpoilPerDay = 0.5f;
            var hold = new FakeHold();

            Assert.IsTrue(MakeDig(hold, clam).TryDig());
            Assert.AreEqual(777d, hold.Items[0].Freshness.LastSettleGameSeconds, 1e-9,
                            "a dug clam goes on the clock at the dig (M1 §7.3)");
            Assert.AreEqual(0.5f, hold.Items[0].SpoilPerDay, 1e-6f, "the species' rate rides the item");
        }

        [Test]
        public void Dig_FullBucket_RefusesAndCapsAt20()
        {
            var hold = new FakeHold { Capacity = 20 };
            var clam = MakeClam();

            // A hole yields ONCE (the clam's lifted out), so fill the 20-cap bucket from 20 separate holes —
            // each a single clam — then a 21st hole must refuse because the bucket is full.
            for (int i = 0; i < 20; i++)
                Assert.IsTrue(MakeDig(hold, clam).TryDig(), $"hole {i + 1} yields its one clam");
            Assert.AreEqual(20, hold.UsedUnits, "the bucket caps at 20");
            Assert.IsFalse(MakeDig(hold, clam).TryDig(), "the 21st hole refuses — the bucket's full");
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

        // ---- BUG 1: a dig is gated to standing AT the hole (within reach), via the ClamDigger -----------

        // A clam hole at a given world spot with a reach radius; the digger reads the player position to
        // decide which (if any) hole a press digs.
        private ClamDig MakeDigAt(IHold bucket, FishSpeciesDef clam, Vector2 spotPos, float reach = 1.25f, int seed = 7)
        {
            var go = new GameObject("ClamHole");
            _spawned.Add(go);
            go.transform.position = spotPos;
            var d = go.AddComponent<ClamDig>();
            d.Configure(clam, bucket, go.transform, "gear.shovel", seed, reach);
            // landInHand: false — same reason as MakeDig above: these pin WHICH hole a press digs
            // (nearest, in reach, one per press), read out of the pail. The destination is
            // CatchInHandTests' subject.
            d.ConfigureInteract($"fixture.clam_hole.{spotPos.x}_{spotPos.y}", ToolDef.ShovelId,
                                landInHand: false);
            return d;
        }

        private ClamDigger MakeDigger(Vector2 playerPos)
        {
            var go = new GameObject("Player");
            _spawned.Add(go);
            go.transform.position = playerPos;
            var digger = go.AddComponent<ClamDigger>();
            digger.Configure(go.transform);
            return digger;
        }

        [Test]
        public void Digger_WhenPlayerAtHole_DigsOneClam()
        {
            var hold = new FakeHold();
            MakeDigAt(hold, MakeClam(), new Vector2(5f, 0f), reach: 1.25f);
            var digger = MakeDigger(new Vector2(5f, 0.5f));   // 0.5 m from the hole → in reach

            Assert.IsTrue(digger.TryDigNearest(), "a press while standing at the bared hole digs one clam");
            Assert.AreEqual(1, hold.UsedUnits, "exactly one clam per press");
            Assert.AreEqual(1, _caught, "FishCaught fired once");
        }

        [Test]
        public void Digger_WhenPlayerNotAtAnyHole_DigsNothing()
        {
            var hold = new FakeHold();
            MakeDigAt(hold, MakeClam(), new Vector2(5f, 0f), reach: 1.25f);
            var digger = MakeDigger(new Vector2(20f, 0f));    // 15 m away → no hole in reach

            Assert.IsFalse(digger.TryDigNearest(), "pressing E away from any hole yields nothing");
            Assert.AreEqual(0, hold.UsedUnits, "nothing dug when not at a hole");
            Assert.AreEqual(0, _caught);
        }

        [Test]
        public void Dig_OutOfReach_WithinReachIsFalse()
        {
            var hold = new FakeHold();
            var dig = MakeDigAt(hold, MakeClam(), new Vector2(5f, 0f), reach: 1.25f);

            Assert.IsTrue(dig.WithinReach(new Vector2(5f, 1.0f)), "1.0 m away is within the 1.25 m reach");
            Assert.IsFalse(dig.WithinReach(new Vector2(5f, 3.0f)), "3.0 m away is out of the 1.25 m reach");
        }

        [Test]
        public void Digger_OnePressDigsExactlyOneClam_NotEveryExposedHole()
        {
            // BUG 2 root cause: every exposed hole used to dig on each press, filling the 20-cap bucket in a
            // press or two. With the digger, a press digs ONE hole — the nearest in reach.
            var hold = new FakeHold();
            // A whole bar's worth of exposed holes, all far apart; the player stands at exactly one of them.
            for (int i = 0; i < 10; i++) MakeDigAt(hold, MakeClam(), new Vector2(i * 5f, 0f), reach: 1.25f);
            var digger = MakeDigger(new Vector2(0f, 0f));     // at the first hole only

            Assert.IsTrue(digger.TryDigNearest());
            Assert.AreEqual(1, hold.UsedUnits, "one press digs ONE clam, not all 10 exposed holes");
        }

        [Test]
        public void Digger_PicksTheNearestInRangeHole()
        {
            var holdA = new FakeHold();
            var holdB = new FakeHold();
            // Two holes within reach of the player; the NEARER one (B) is the one dug.
            MakeDigAt(holdA, MakeClam(), new Vector2(0.0f, 1.0f), reach: 2.0f);   // 1.0 m away
            MakeDigAt(holdB, MakeClam(), new Vector2(0.0f, 0.3f), reach: 2.0f);   // 0.3 m away (nearer)
            var digger = MakeDigger(new Vector2(0f, 0f));

            Assert.IsTrue(digger.TryDigNearest());
            Assert.AreEqual(0, holdA.UsedUnits, "the farther hole is untouched");
            Assert.AreEqual(1, holdB.UsedUnits, "the nearest in-range hole is the one dug");
        }

        [Test]
        public void Digger_SkipsASpentNearHole_AndTakesTheNextFreshOne()
        {
            var spentHold = new FakeHold();
            var freshHold = new FakeHold();
            var spent = MakeDigAt(spentHold, MakeClam(), Vector2.zero);
            var fresh = MakeDigAt(freshHold, MakeClam(), new Vector2(0.5f, 0f));
            spent.MarkConsumed();
            var digger = MakeDigger(Vector2.zero);

            Assert.AreSame(fresh, ClamDigger.NearestDiggable(Vector2.zero));
            Assert.IsTrue(digger.TryDigNearest());
            Assert.AreEqual(0, spentHold.UsedUnits);
            Assert.AreEqual(1, freshHold.UsedUnits);
            Assert.AreEqual(1, _caught);
            Assert.IsNull(ClamDigger.NearestDiggable(Vector2.zero));
            Assert.IsFalse(digger.TryDigNearest());
            Assert.AreEqual(1, _caught);
        }

        [TestCase(0.999f, true)]
        [TestCase(1f, true)]
        [TestCase(1.001f, false)]
        public void Dig_ExposureIncludesGroundExactlyAtWaterLevel(float water, bool expected)
        {
            _env.Level = water;
            var hold = new FakeHold();
            var dig = MakeDig(hold, MakeClam());
            Assert.AreEqual(expected, dig.IsExposedNow());
            Assert.AreEqual(expected, dig.TryDig());
            Assert.AreEqual(expected ? 1 : 0, hold.UsedUnits);
            Assert.AreEqual(expected ? 1 : 0, _caught);
            Assert.AreEqual(expected, dig.Consumed);
        }

        [TestCase(1.249f, true)]
        [TestCase(1.25f, true)]
        [TestCase(1.251f, false)]
        public void Digger_ReachIncludesTheConfiguredBoundary(float distance, bool expected)
        {
            var hold = new FakeHold();
            var dig = MakeDigAt(hold, MakeClam(), Vector2.zero, 1.25f);
            var digger = MakeDigger(new Vector2(distance, 0f));
            Assert.AreEqual(expected, dig.WithinReach(new Vector2(distance, 0f)));
            Assert.AreEqual(expected, digger.TryDigNearest());
            Assert.AreEqual(expected ? 1 : 0, hold.UsedUnits);
        }

        [Test]
        public void MissingEnvironment_RefusesWithoutSpendingTheHole()
        {
            GameServices.Environment = null;
            var hold = new FakeHold();
            var dig = MakeDig(hold, MakeClam());
            Assert.IsFalse(dig.IsAvailable);
            Assert.IsFalse(dig.TryDig());
            Assert.IsFalse(dig.Consumed);
            Assert.AreEqual(0, hold.UsedUnits);
            Assert.AreEqual(0, _caught);
        }

        [Test]
        public void RefusedInsertion_LeavesTheHoleFreshForARetry()
        {
            var hold = new FakeHold { RejectAdds = true };
            var dig = MakeDig(hold, MakeClam());
            Assert.IsFalse(dig.TryDig());
            Assert.IsFalse(dig.Consumed);
            Assert.AreEqual(0, _save.SpentClamHoles.Count);
            Assert.AreEqual(0, _caught);
            hold.RejectAdds = false;
            Assert.IsTrue(dig.TryDig());
            Assert.IsTrue(dig.Consumed);
            Assert.AreEqual(1, hold.UsedUnits);
            Assert.AreEqual(1, _save.SpentClamHoles.Count);
            Assert.AreEqual(1, _caught);
        }

        [Test]
        public void QuietHole_CanBeDugWithoutWaitingForItsSquirt()
        {
            var hold = new FakeHold();
            var dig = MakeDig(hold, MakeClam());
            Assert.IsFalse(dig.ShowingSquirt);
            Assert.IsTrue(dig.IsAvailable);
            Assert.IsTrue(dig.TryDig());
            Assert.AreEqual(1, hold.UsedUnits);
            Assert.IsFalse(dig.IsAvailable);
        }

        [TestCase("SoftShellClam", "fish.soft_shell_clam", 0.05f, 0.2f)]
        [TestCase("AtlanticSurfClam", "fish.atlantic_surf_clam", 0.2f, 0.5f)]
        public void AuthoredClam_YieldsOneOfItsOwnSpeciesAndWeightRange(
            string asset, string speciesId, float minKg, float maxKg)
        {
            var species = UnityEditor.AssetDatabase.LoadAssetAtPath<FishSpeciesDef>(
                "Assets/_Project/Data/Fish/" + asset + ".asset");
            Assert.IsNotNull(species);
            Assert.AreEqual(speciesId, species.Id);
            Assert.AreEqual(minKg, species.MinWeightKg);
            Assert.AreEqual(maxKg, species.MaxWeightKg);
            var hold = new FakeHold();
            var dig = MakeDig(hold, species, seed: 7);
            Assert.IsTrue(dig.TryDig());
            Assert.AreEqual(1, hold.UsedUnits);
            Assert.AreEqual(speciesId, hold.Items[0].SpeciesId);
            Assert.AreEqual(CatchResolver.RollWeight(species, new System.Random(7)), hold.Items[0].WeightKg);
            Assert.AreEqual(species.BaseValue, hold.Items[0].BaseValue);
            Assert.AreEqual(species.SpoilPerDay, hold.Items[0].SpoilPerDay);
        }

        [Test]
        public void DirectDig_CurrentlyUsesExposureRatherThanSpeciesCatchWindows()
        {
            var species = MakeClam();
            species.RegionIds = new[] { "region.other" };
            species.Seasons = SeasonMask.HardWinter;
            species.StartHour = 6f;
            species.EndHour = 20f;
            species.MaxTide = -0.44f;
            species.AllowedGear = Gear.None;
            GameServices.Clock = new StampClock { TotalSeconds = 123d };
            var context = new CatchContext("region.st_peters", _env.Level, 0f, Season.EarlySpring, Gear.ClamFork);
            Assert.IsFalse(CatchResolver.Matches(species, context));
            var hold = new FakeHold();
            Assert.IsTrue(MakeDig(hold, species).TryDig());
            Assert.AreEqual(1, hold.UsedUnits);
        }

        [Test]
        public void Digger_SkipsSubmergedHole_EvenIfInReach()
        {
            _env.Level = 2.0f;   // floods the 1.0 m flat → every hole submerged (not diggable)
            var hold = new FakeHold();
            MakeDigAt(hold, MakeClam(), new Vector2(0f, 0f), reach: 1.25f);
            var digger = MakeDigger(new Vector2(0f, 0.2f));   // standing right at the hole, but it's underwater

            Assert.IsFalse(digger.TryDigNearest(), "an in-reach hole that's still underwater doesn't dig");
            Assert.AreEqual(0, hold.UsedUnits);
        }
    }
}
