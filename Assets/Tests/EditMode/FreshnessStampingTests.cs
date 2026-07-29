using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Fishing;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// M1 §7.3 — the freshness clock is STAMPED at every path that creates a catch. These pin the
    /// wiring on top of the #289 Core seam: the CatchItem carries its species' rate + a landing stamp,
    /// the trap resolver caches the rate while staying pure, the deck-pot stow stamps the Core clock,
    /// and the clam dig stamps at the dig — and that an item from the LEGACY constructor is inert
    /// (SpoilPerDay 0 ⇒ never rots), the guarantee that keeps every pre-freshness test and payout exact.
    /// </summary>
    public class FreshnessStampingTests
    {
        private sealed class FakeHold : IHold
        {
            private readonly List<CatchItem> _items = new();
            public int CapacityUnits => 999;
            public int UsedUnits => _items.Count;
            public IReadOnlyList<CatchItem> Items => _items;
            public bool TryAdd(CatchItem item) { _items.Add(item); return true; }
            public void Clear() => _items.Clear();
        }

        /// <summary>A clock pinned at one instant (SeekTo keeps its interface default no-op).</summary>
        private sealed class FakeClock : IGameClock
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

        private readonly List<Object> _spawned = new();

        [SetUp]
        public void SetUp() => GameServices.Reset();

        [TearDown]
        public void TearDown()
        {
            GameServices.Reset();
            foreach (var o in _spawned) if (o != null) Object.DestroyImmediate(o);
            _spawned.Clear();
        }

        private FishSpeciesDef MakeSpecies(string id, float spoilPerDay, Gear gear = Gear.Trap)
        {
            var f = ScriptableObject.CreateInstance<FishSpeciesDef>();
            f.Id = id;
            f.DisplayName = id;
            f.Category = FishCategory.Shellfish;
            f.RegionIds = new[] { "region.test" };
            f.AllowedGear = gear;
            f.SpoilPerDay = spoilPerDay;
            _spawned.Add(f);
            return f;
        }

        private static SpoilContext At(double now)
            => new SpoilContext(now, 1200f, SpoilPolicy.Default);

        // ---- the legacy constructor stays inert (the exactness guarantee) ----------------------

        [Test]
        public void LegacyCatchItem_NeverRots_UnderAnyContext()
        {
            var item = new CatchItem("fish.cod", "Cod", FishCategory.InshoreGroundfish, 4f, 20, 0.3f);

            var tenDaysOn = At(1200d * 10);
            Assert.AreEqual(0f, tenDaysOn.SpoilOf(item), 1e-6f, "SpoilPerDay 0 banks nothing, ever");
            Assert.IsTrue(tenDaysOn.IsSellable(item));
            Assert.AreEqual(1f, tenDaysOn.ValueMultiplier(item), 1e-6f,
                            "so pre-freshness payouts stay bit-identical");
        }

        [Test]
        public void WithFreshness_ReplacesOnlyTheClock()
        {
            var item = new CatchItem("fish.cod", "Cod", FishCategory.InshoreGroundfish, 4f, 20, 0.3f,
                                     0.75f, Freshness.Landed(100d));
            var restamped = item.WithFreshness(Freshness.Landed(900d, StorageMode.Live));

            Assert.AreEqual(item.SpeciesId, restamped.SpeciesId);
            Assert.AreEqual(item.WeightKg, restamped.WeightKg);
            Assert.AreEqual(item.BaseValue, restamped.BaseValue);
            Assert.AreEqual(item.SpoilPerDay, restamped.SpoilPerDay, "the rate rides along");
            Assert.AreEqual(900d, restamped.Freshness.LastSettleGameSeconds, 1e-9);
            Assert.AreEqual(StorageMode.Live, restamped.Freshness.Mode);
        }

        // ---- the trap resolver caches the rate; the stow stamps the clock ----------------------

        [Test]
        public void PlacedTrapCatch_CarriesTheSpeciesRate()
        {
            var lobster = MakeSpecies("fish.lobster", 0.35f);
            var ctx = new CatchContext("region.test", 0f, 12f, Season.EarlySpring, Gear.Trap);

            CatchItem? caught = PlacedTrapCatch.Resolve(
                new[] { lobster }, in ctx, null, 1, new System.Random(42));

            Assert.IsTrue(caught.HasValue, "the one matching species is caught");
            Assert.AreEqual(0.35f, caught.Value.SpoilPerDay, 1e-6f,
                            "the rate is cached off the Def like BaseValue is");
        }

        [Test]
        public void DeckPot_Stow_StampsTheCoreClock()
        {
            var clock = new FakeClock { TotalSeconds = 555d };
            GameServices.Clock = clock;

            // A rule-less species is always a keeper; AutoResolve stows it (the same Stamped path
            // TryBandNext uses).
            var item = new CatchItem("fish.lobster", "Lobster", FishCategory.Shellfish, 1.1f, 28, 0.35f,
                                     0.35f, default);
            var pot = new DeckPot(null, null, "trap-1", 0d, 7, new[] { item });
            var hold = new FakeHold();

            pot.AutoResolve(hold, null, out int kept, out _, out _);

            Assert.AreEqual(1, kept);
            Assert.AreEqual(555d, hold.Items[0].Freshness.LastSettleGameSeconds, 1e-9,
                            "stowing is when it goes on the clock");
            Assert.AreEqual(StorageMode.Ambient, hold.Items[0].Freshness.Mode);
            Assert.AreEqual(0.35f, hold.Items[0].SpoilPerDay, 1e-6f, "the resolver's rate rode through");
        }

        [Test]
        public void DeckPot_Stow_WithNoClock_StampsZero_Harmlessly()
        {
            // EditMode reality: no clock wired. The stamp is t = 0 — harmless for the legacy items
            // tests build (SpoilPerDay 0 never rots), and never a throw.
            var item = new CatchItem("fish.lobster", "Lobster", FishCategory.Shellfish, 1.1f, 28, 0.35f);
            var pot = new DeckPot(null, null, "trap-1", 0d, 7, new[] { item });
            var hold = new FakeHold();

            pot.AutoResolve(hold, null, out int kept, out _, out _);

            Assert.AreEqual(1, kept);
            Assert.AreEqual(0d, hold.Items[0].Freshness.LastSettleGameSeconds, 1e-9);
        }
    }
}
