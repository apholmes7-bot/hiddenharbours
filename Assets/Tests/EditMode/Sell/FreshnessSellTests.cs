using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Economy;

namespace HiddenHarbours.Tests.Sell
{
    /// <summary>
    /// M1 §7.3 at the till — the money consequence of the freshness clock. Pins the two contracts the
    /// arc leans on: (1) each unit's price is scaled by ITS OWN freshness (fresh pays full, value falls
    /// linearly), and (2) refusal is a HARD GATE in front of the pricing maths — the named #289 trap is
    /// that <see cref="SellPricing.UnitPrice(int,float,float,float)"/> floors every priced unit at 1₲,
    /// so the multiplier alone can never make rot worthless; a spoiled unit must never be priced at all.
    /// It stays in the hold (rubbish is carried, not laundered) and never raises a CatchSold.
    /// All contexts are explicit (<see cref="SpoilContext"/>) — no service wiring, exact numbers.
    /// </summary>
    public class FreshnessSellTests
    {
        private const float SecondsPerDay = 1200f;

        private sealed class FakeHold : IHold
        {
            private readonly List<CatchItem> _items = new();
            public int CapacityUnits => 999;
            public int UsedUnits => _items.Count;
            public IReadOnlyList<CatchItem> Items => _items;
            public bool TryAdd(CatchItem item) { _items.Add(item); return true; }
            public void Clear() => _items.Clear();
        }

        private sealed class FakeWallet : IWallet
        {
            public int Money { get; private set; }
            public void Add(int amount) => Money += amount;
            public bool TrySpend(int amount) { if (amount > Money) return false; Money -= amount; return true; }
        }

        [SetUp]
        public void SetUp() => EventBus.Clear<CatchSold>();

        [TearDown]
        public void TearDown() => EventBus.Clear<CatchSold>();

        // A cod landed at t (1 spoil/day: half gone in half a day, rubbish past 0.9 of a day).
        private static CatchItem CodLandedAt(double t)
            => new CatchItem("fish.cod", "Cod", FishCategory.InshoreGroundfish, 4f, 20, 0.3f,
                             1f, Freshness.Landed(t));

        private static SpoilContext At(double now)
            => new SpoilContext(now, SecondsPerDay, SpoilPolicy.Default);

        // ---- per-unit value scaling ----------------------------------------------------------

        [Test]
        public void FreshUnit_PricesExactlyAsBefore()
        {
            var hold = new FakeHold();
            hold.TryAdd(CodLandedAt(0d));

            int quoted = SellService.Quote(hold, null, "fish.cod", 1, At(0d));
            Assert.AreEqual(SellPricing.UnitPrice(20, 0.3f, 0f), quoted,
                            "spoil 0 → multiplier 1 → the pre-freshness price, bit-identical");
        }

        [Test]
        public void HalfSpoiled_SellsAtHalfValue()
        {
            var hold = new FakeHold();
            hold.TryAdd(CodLandedAt(0d));
            var halfDayOn = At(SecondsPerDay * 0.5d);   // spoil 0.5 → multiplier 0.5

            int quoted = SellService.Quote(hold, null, "fish.cod", 1, halfDayOn);
            Assert.AreEqual(SellPricing.UnitPrice(20, 0.3f, 0f, 1f, 0.5f), quoted);
            Assert.AreEqual(10, quoted, "half of the 20₲ base at neutral supply");
        }

        [Test]
        public void TwoUnitsOfOneSpecies_EachCarryTheirOwnFreshness()
        {
            // Landed half a day apart: at the till the older one is worth less than the newer one.
            var hold = new FakeHold();
            hold.TryAdd(CodLandedAt(0d));                       // older — spoil 0.5 at read time
            hold.TryAdd(CodLandedAt(SecondsPerDay * 0.5d));     // newer — spoil 0 at read time
            var now = At(SecondsPerDay * 0.5d);

            // Hold order is sale order: unit 0 (older, mult 0.5) at supply 0, unit 1 (fresh) at supply 1.
            int expected = SellPricing.MarginalPrice(20, 0.3f, 0f, 0, 1f, 0.5f)
                         + SellPricing.MarginalPrice(20, 0.3f, 0f, 1, 1f, 1f);
            Assert.AreEqual(expected, SellService.Quote(hold, null, "fish.cod", 2, now),
                            "the multiplier is per UNIT, not per species");
        }

        [Test]
        public void Quote_EqualsPaid_WithMixedFreshness()
        {
            var hold = new FakeHold();
            hold.TryAdd(CodLandedAt(0d));
            hold.TryAdd(CodLandedAt(SecondsPerDay * 0.25d));
            hold.TryAdd(CodLandedAt(SecondsPerDay * 0.5d));
            var wallet = new FakeWallet();
            var now = At(SecondsPerDay * 0.6d);

            int quoted = SellService.Quote(hold, null, "fish.cod", 3, now);
            int paid = SellService.SellSpecies(hold, wallet, null, "fish.cod", 3, now);

            Assert.Greater(quoted, 0);
            Assert.AreEqual(quoted, paid, "the displayed quote is the coin paid, freshness included");
            Assert.AreEqual(paid, wallet.Money);
        }

        // ---- refusal: the hard gate in front of the 1₲ floor ---------------------------------

        [Test]
        public void SpoiledUnit_IsRefused_NeverSoldForTheFloorCoin()
        {
            var hold = new FakeHold();
            hold.TryAdd(CodLandedAt(0d));
            var wallet = new FakeWallet();
            var aDayOn = At(SecondsPerDay);   // spoil 1.0 ≥ threshold 0.9 → rubbish

            int raised = 0;
            void OnSold(CatchSold _) => raised++;
            EventBus.Subscribe<CatchSold>(OnSold);
            int paid = SellService.SellSpecies(hold, wallet, null, "fish.cod", 1, aDayOn);
            EventBus.Unsubscribe<CatchSold>(OnSold);

            Assert.AreEqual(0, SellService.CountSellableOf(hold, "fish.cod", aDayOn));
            Assert.AreEqual(0, SellService.Quote(hold, null, "fish.cod", 1, aDayOn), "no price, not 1₲");
            Assert.AreEqual(0, paid, "the buyer waves it off at any price — the #289 floor trap");
            Assert.AreEqual(0, wallet.Money);
            Assert.AreEqual(1, hold.UsedUnits, "rubbish still occupies the hold until dumped");
            Assert.AreEqual(0, raised, "nothing sold → no CatchSold");
        }

        [Test]
        public void JustUnderTheThreshold_StillSells_ForAtLeastTheFloor()
        {
            var hold = new FakeHold();
            hold.TryAdd(CodLandedAt(0d));
            var wallet = new FakeWallet();
            var nearlyGone = At(SecondsPerDay * 0.85d);   // spoil 0.85 < 0.9 → sellable, poorly

            int paid = SellService.SellSpecies(hold, wallet, null, "fish.cod", 1, nearlyGone);
            Assert.GreaterOrEqual(paid, 1, "sellable means priced, and pricing floors at 1₲");
            Assert.AreEqual(0, hold.UsedUnits, "it sold and left the hold");
        }

        [Test]
        public void SellAll_TakesTheGood_LeavesTheRubbish()
        {
            var hold = new FakeHold();
            hold.TryAdd(CodLandedAt(0d));                      // rubbish by read time
            hold.TryAdd(CodLandedAt(SecondsPerDay));           // fresh at read time
            var wallet = new FakeWallet();
            var now = At(SecondsPerDay);

            int seenCount = -1;
            void OnSold(CatchSold e) => seenCount = e.Count;
            EventBus.Subscribe<CatchSold>(OnSold);
            int paid = SellService.SellAll(hold, wallet, null, now);
            EventBus.Unsubscribe<CatchSold>(OnSold);

            Assert.AreEqual(SellPricing.UnitPrice(20, 0.3f, 0f), paid, "the fresh one at full price");
            Assert.AreEqual(1, hold.UsedUnits, "the spoiled one is refused and stays aboard");
            Assert.AreEqual(1, seenCount, "CatchSold counts only what sold");
        }

        [Test]
        public void SellAll_AllRubbish_SellsNothing_RaisesNothing()
        {
            var hold = new FakeHold();
            hold.TryAdd(CodLandedAt(0d));
            hold.TryAdd(CodLandedAt(0d));
            var wallet = new FakeWallet();
            var aDayOn = At(SecondsPerDay);

            int raised = 0;
            void OnSold(CatchSold _) => raised++;
            EventBus.Subscribe<CatchSold>(OnSold);
            int paid = SellService.SellAll(hold, wallet, null, aDayOn);
            EventBus.Unsubscribe<CatchSold>(OnSold);

            Assert.AreEqual(0, paid);
            Assert.AreEqual(2, hold.UsedUnits, "a hold of pure rubbish is a dump job, not a sale");
            Assert.AreEqual(0, raised);
            Assert.AreEqual(2, SellService.CountSpoiled(hold, aDayOn), "and the screen can say so");
        }

        // ---- the legacy entry points stay exact ----------------------------------------------

        [Test]
        public void LegacyEntryPoints_MatchExplicitContext_ForPreFreshnessItems()
        {
            // No clock/config wired (EditMode): Capture() reads t = 0 + defaults. For a legacy item
            // (SpoilPerDay 0) every context is equivalent, so the old signatures price identically.
            var holdA = new FakeHold();
            var holdB = new FakeHold();
            var legacy = new CatchItem("fish.cod", "Cod", FishCategory.InshoreGroundfish, 4f, 20, 0.3f);
            holdA.TryAdd(legacy); holdB.TryAdd(legacy);

            Assert.AreEqual(SellService.Quote(holdA, null, "fish.cod", 1),
                            SellService.Quote(holdB, null, "fish.cod", 1, At(SecondsPerDay * 99)),
                            "legacy items price the same under any context — the exactness guarantee");
        }
    }
}
