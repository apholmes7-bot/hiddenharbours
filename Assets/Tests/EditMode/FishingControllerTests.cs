using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Fishing;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// VS-13 — the FishingController's integration with the hold and the catch path. Drives the full
    /// one-thumb FSM (cast → bite → hook → fight) via Tick and asserts the cozy outcomes: a paced fight
    /// lands EXACTLY one item; a sustained hold snaps and adds nothing; hand-gather species tend in;
    /// a no-match cast says "nothing biting"; a full hold refuses the cast. CatchResolver is reused
    /// unchanged (its own tests stay green).
    /// </summary>
    public class FishingControllerTests
    {
        // In-test hold (the Core contract), so we don't pull in Boats' ShipHold lifecycle.
        private sealed class FakeHold : IHold
        {
            private readonly List<CatchItem> _items = new();
            public int Capacity = 6;
            public int CapacityUnits => Capacity;
            public int UsedUnits => _items.Count;
            public IReadOnlyList<CatchItem> Items => _items;
            public bool TryAdd(CatchItem item)
            {
                if (_items.Count >= Capacity) return false;
                _items.Add(item);
                return true;
            }
            public void Clear() => _items.Clear();
        }

        private readonly List<Object> _spawned = new();
        private int _fishCaught;
        private void OnFishCaught(FishCaught e) => _fishCaught++;

        [SetUp]
        public void SetUp()
        {
            _fishCaught = 0;
            EventBus.Clear<FishCaught>();
            EventBus.Clear<FishingStateChanged>();
            EventBus.Subscribe<FishCaught>(OnFishCaught);
        }

        [TearDown]
        public void TearDown()
        {
            EventBus.Unsubscribe<FishCaught>(OnFishCaught);
            EventBus.Clear<FishCaught>();
            EventBus.Clear<FishingStateChanged>();
            foreach (var o in _spawned)
                if (o != null) Object.DestroyImmediate(o);
            _spawned.Clear();
        }

        // ---- helpers ------------------------------------------------------------------------

        private FishSpeciesDef MakeFish(string id, FishCategory cat, float minKg, float maxKg,
                                        string region = "region.coddle_cove")
        {
            var f = ScriptableObject.CreateInstance<FishSpeciesDef>();
            f.Id = id; f.DisplayName = id; f.Category = cat;
            f.RegionIds = new[] { region };
            f.AllowedGear = Gear.Handline | Gear.Longline;
            f.Seasons = SeasonMask.AllYear;
            f.MinTide = -10f; f.MaxTide = 10f; f.StartHour = 0f; f.EndHour = 24f;
            f.MinWeightKg = minKg; f.MaxWeightKg = maxKg;
            f.BaseValue = 12; f.SupplyElasticity = 0.2f; f.SpawnWeight = 1f;
            _spawned.Add(f);
            return f;
        }

        private FishingController MakeController(IHold hold, FishSpeciesDef[] pool, int seed)
        {
            var go = new GameObject("Fisher");
            _spawned.Add(go);
            var c = go.AddComponent<FishingController>();
            c.Configure(hold, pool, "region.coddle_cove", Gear.Handline, seed);
            return c;
        }

        // Press to cast, then release and advance until the fight (or tend) begins (auto-hook handles the bite).
        private void AdvanceIntoFight(FishingController c, float maxSeconds = 30f)
        {
            c.Tick(0.05f, true); // rising edge → cast
            float t = 0f;
            while (c.Phase != FishingPhase.Fighting && c.Phase != FishingPhase.Tending && t < maxSeconds)
            {
                c.Tick(0.05f, false);
                t += 0.05f;
            }
        }

        private static bool IsResult(FishingPhase p)
            => p == FishingPhase.Landed || p == FishingPhase.Snapped || p == FishingPhase.NoBite || p == FishingPhase.Idle;

        // ---- tests --------------------------------------------------------------------------

        [Test]
        public void PacedFight_Lands_AddsExactlyOneItem()
        {
            var hold = new FakeHold();
            var cod = MakeFish("fish.cod", FishCategory.InshoreGroundfish, 1f, 6f);
            var c = MakeController(hold, new[] { cod }, seed: 999);

            AdvanceIntoFight(c);
            Assert.AreEqual(FishingPhase.Fighting, c.Phase, "a finfish should start a tension fight");

            float t = 0f;
            while (!IsResult(c.Phase) && t < 120f)
            {
                c.Tick(0.05f, actionHeld: c.State.Tension01 < 0.5f); // forgiving pulse
                t += 0.05f;
            }

            Assert.AreEqual(FishingPhase.Landed, c.Phase, "a paced fight should land the fish");
            Assert.AreEqual(1, hold.UsedUnits, "landing adds exactly one item to the hold");
            Assert.AreEqual(1, _fishCaught, "exactly one FishCaught should be raised");
        }

        [Test]
        public void SustainedHold_Snaps_AddsNothing()
        {
            var hold = new FakeHold();
            var cod = MakeFish("fish.cod", FishCategory.InshoreGroundfish, 1f, 6f);
            var c = MakeController(hold, new[] { cod }, seed: 999);

            AdvanceIntoFight(c);

            float t = 0f;
            while (!IsResult(c.Phase) && t < 60f)
            {
                c.Tick(0.05f, actionHeld: true); // just hold → snap
                t += 0.05f;
            }

            Assert.AreEqual(FishingPhase.Snapped, c.Phase, "a sustained hold should snap the line");
            Assert.AreEqual(0, hold.UsedUnits, "a snap is cozy — nothing is added to the hold");
            Assert.AreEqual(0, _fishCaught, "no FishCaught on a snap");
        }

        [Test]
        public void HandGather_Tends_AndLands()
        {
            var hold = new FakeHold();
            var crab = MakeFish("fish.crab", FishCategory.Shellfish, 0.4f, 3f);
            var c = MakeController(hold, new[] { crab }, seed: 5);

            AdvanceIntoFight(c);
            Assert.AreEqual(FishingPhase.Tending, c.Phase, "shellfish use the lighter hand-gather variant");

            float t = 0f;
            while (!IsResult(c.Phase) && t < 30f)
            {
                c.Tick(0.05f, actionHeld: true); // hold to tend — no snap risk
                t += 0.05f;
            }

            Assert.AreEqual(FishingPhase.Landed, c.Phase, "tending should land the crab");
            Assert.AreEqual(1, hold.UsedUnits);
        }

        [Test]
        public void NoMatch_ReportsNothingBiting_AddsNothing()
        {
            var hold = new FakeHold();
            // A fish that lives somewhere else never matches the coddle_cove context → no bite.
            var elsewhere = MakeFish("fish.elsewhere", FishCategory.InshoreGroundfish, 1f, 6f, "region.elsewhere");
            var c = MakeController(hold, new[] { elsewhere }, seed: 1);

            c.Tick(0.05f, true); // cast
            float t = 0f;
            while (c.Phase == FishingPhase.Waiting && t < 30f) { c.Tick(0.05f, false); t += 0.05f; }

            Assert.AreEqual(FishingPhase.NoBite, c.Phase, "a no-match cast reports nothing biting");
            Assert.AreEqual(0, hold.UsedUnits);
            Assert.AreEqual(0, _fishCaught);
        }

        [Test]
        public void FullHold_RefusesTheCast()
        {
            var hold = new FakeHold { Capacity = 0 }; // already full
            var cod = MakeFish("fish.cod", FishCategory.InshoreGroundfish, 1f, 6f);
            var c = MakeController(hold, new[] { cod }, seed: 1);

            c.Tick(0.05f, true); // attempt to cast

            Assert.AreEqual(FishingPhase.Idle, c.Phase, "a full hold should refuse the cast (stays idle)");
        }
    }
}
