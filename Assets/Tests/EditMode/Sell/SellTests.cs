using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Economy;

namespace HiddenHarbours.Tests.Sell
{
    /// <summary>
    /// VS-18 — the wharf sell screen's economics. Pins the marginal "self-glutt" pricing
    /// (<see cref="SellPricing"/>) and the sale execution (<see cref="SellService"/>): that the running
    /// total the screen DISPLAYS is exactly the coin RECEIVED, that selling floods supply so later
    /// units (and "sell all") earn less, and that partial / by-type / sell-all removal is correct.
    /// Deterministic given (hold, market supply) — no RNG.
    /// </summary>
    public class SellTests
    {
        // ---- in-file fakes for the Core contracts (mirror WharfSellTests) --------------------
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

        private readonly List<GameObject> _spawned = new();

        [SetUp]
        public void SetUp() => EventBus.Clear<CatchSold>();

        [TearDown]
        public void TearDown()
        {
            EventBus.Clear<CatchSold>();
            foreach (var go in _spawned) if (go != null) Object.DestroyImmediate(go);
            _spawned.Clear();
        }

        // ---- helpers ------------------------------------------------------------------------

        private Market MakeMarket()
        {
            var go = new GameObject("Market");
            _spawned.Add(go);
            return go.AddComponent<Market>(); // Update() never runs in EditMode → supply is stable
        }

        private static void Add(FakeHold hold, string id, string name, FishCategory cat, int n,
                                int baseValue, float elasticity)
        {
            for (int i = 0; i < n; i++)
                hold.TryAdd(new CatchItem(id, name, cat, 4f, baseValue, elasticity));
        }

        // ---- marginal price / running total -------------------------------------------------

        [Test]
        public void UnitPrice_AtZeroSupply_IsBaseValue()
        {
            // Supply 0 → multiplier 1.0 → the unit is worth its base value (min 1).
            Assert.AreEqual(20, SellPricing.UnitPrice(20, 0.5f, 0f));
            Assert.AreEqual(1, SellPricing.UnitPrice(0, 0.5f, 0f), "rounds up to a 1-coin floor");
        }

        [Test]
        public void MarginalPrice_FallsAsYouSellMore()
        {
            int p0 = SellPricing.MarginalPrice(20, 0.5f, 0f, 0);
            int p1 = SellPricing.MarginalPrice(20, 0.5f, 0f, 1);
            int p2 = SellPricing.MarginalPrice(20, 0.5f, 0f, 2);

            Assert.Greater(p0, p1, "each extra unit floods the market, so the next is worth less");
            Assert.Greater(p1, p2, "and less again");
        }

        [Test]
        public void RunningTotal_EqualsSumOfMarginals()
        {
            const int bv = 20; const float e = 0.4f; const float s = 1f; const int q = 4;
            int sum = 0;
            for (int j = 0; j < q; j++) sum += SellPricing.MarginalPrice(bv, e, s, j);

            Assert.AreEqual(sum, SellPricing.RunningTotal(bv, e, s, q));
            Assert.AreEqual(0, SellPricing.RunningTotal(bv, e, s, 0), "selling nothing is worth nothing");
            Assert.AreEqual(SellPricing.UnitPrice(bv, e, s), SellPricing.RunningTotal(bv, e, s, 1),
                "one unit's running total is just its unit price");
        }

        [Test]
        public void RunningTotal_IsLessThanFlatBatch_WhenElastic()
        {
            const int bv = 20; const float e = 0.5f; const int q = 5;
            int marginal = SellPricing.RunningTotal(bv, e, 0f, q);
            int flat = q * SellPricing.UnitPrice(bv, e, 0f); // all at the opening price (no glutt)

            Assert.Less(marginal, flat, "self-glutting earns less than selling all at the opening price");
        }

        // ---- displayed total == coin received -----------------------------------------------

        [Test]
        public void Quote_EqualsPaidTotal_AndWalletGain()
        {
            var market = MakeMarket();
            var hold = new FakeHold();
            Add(hold, "fish.cod", "Cod", FishCategory.InshoreGroundfish, 5, 20, 0.3f);
            var wallet = new FakeWallet();

            int quoted = SellService.Quote(hold, market, "fish.cod", 3);
            int paid = SellService.SellSpecies(hold, wallet, market, "fish.cod", 3);

            Assert.Greater(quoted, 0);
            Assert.AreEqual(quoted, paid, "the displayed quote must equal the coin paid");
            Assert.AreEqual(paid, wallet.Money, "the wallet gains exactly the paid total");
            Assert.AreEqual(2, hold.UsedUnits, "selling 3 of 5 leaves 2");
        }

        // ---- selling removes the right catch and glutts the market --------------------------

        [Test]
        public void SellSpecies_RemovesOnlyThatSpecies_AndGluttsItsCategory()
        {
            var market = MakeMarket();
            var hold = new FakeHold();
            Add(hold, "fish.cod", "Cod", FishCategory.InshoreGroundfish, 3, 18, 0.3f);
            Add(hold, "fish.mack", "Mackerel", FishCategory.Pelagic, 2, 10, 0.4f);
            var wallet = new FakeWallet();

            float supplyBefore = market.SupplyOf(FishCategory.InshoreGroundfish);
            int paid = SellService.SellSpecies(hold, wallet, market, "fish.cod", 2);

            Assert.AreEqual(3, hold.UsedUnits, "5 − 2 sold = 3 left");
            Assert.AreEqual(1, SellService.CountOf(hold, "fish.cod"), "2 of 3 cod sold, 1 remains");
            Assert.AreEqual(2, SellService.CountOf(hold, "fish.mack"), "mackerel untouched");
            Assert.Greater(market.SupplyOf(FishCategory.InshoreGroundfish), supplyBefore,
                "selling floods the category's supply");
            Assert.AreEqual(paid, wallet.Money);
        }

        [Test]
        public void Glut_DropsTheNextQuote()
        {
            var market = MakeMarket();
            var hold = new FakeHold();
            Add(hold, "fish.cod", "Cod", FishCategory.InshoreGroundfish, 6, 20, 0.5f);
            var wallet = new FakeWallet();

            int quoteFirstUnit = SellService.Quote(hold, market, "fish.cod", 1);   // at supply 0
            SellService.SellSpecies(hold, wallet, market, "fish.cod", 3);           // floods +3
            int quoteAfterGlut = SellService.Quote(hold, market, "fish.cod", 1);    // at supply 3

            Assert.Less(quoteAfterGlut, quoteFirstUnit, "after flooding supply the next unit is worth less");
        }

        [Test]
        public void SellAllOfType_SellsTheWholeSpecies_LeavesOthers()
        {
            var market = MakeMarket();
            var hold = new FakeHold();
            Add(hold, "fish.cod", "Cod", FishCategory.InshoreGroundfish, 4, 20, 0.5f);
            Add(hold, "fish.mack", "Mackerel", FishCategory.Pelagic, 2, 10, 0.4f);
            var wallet = new FakeWallet();

            int all = SellService.CountOf(hold, "fish.cod");
            int paid = SellService.SellSpecies(hold, wallet, market, "fish.cod", all);

            Assert.AreEqual(0, SellService.CountOf(hold, "fish.cod"), "all cod sold");
            Assert.AreEqual(2, SellService.CountOf(hold, "fish.mack"), "mackerel remain");
            Assert.AreEqual(paid, wallet.Money);
        }

        // ---- sell all ------------------------------------------------------------------------

        [Test]
        public void SellAll_SellsEverything_PaysMarginalTotal_ClearsHold_RaisesOneCatchSold()
        {
            var market = MakeMarket();
            var hold = new FakeHold();
            Add(hold, "fish.cod", "Cod", FishCategory.InshoreGroundfish, 3, 20, 0.5f);
            Add(hold, "fish.mack", "Mackerel", FishCategory.Pelagic, 2, 10, 0.4f);
            var wallet = new FakeWallet();

            // Each category self-glutts independently: cod at supply 0/1/2, mackerel at supply 0/1.
            int expected =
                SellPricing.MarginalPrice(20, 0.5f, 0f, 0) + SellPricing.MarginalPrice(20, 0.5f, 0f, 1)
                + SellPricing.MarginalPrice(20, 0.5f, 0f, 2)
                + SellPricing.MarginalPrice(10, 0.4f, 0f, 0) + SellPricing.MarginalPrice(10, 0.4f, 0f, 1);

            int seenTotal = -1, seenCount = -1, raised = 0;
            void OnSold(CatchSold e) { seenTotal = e.TotalPaid; seenCount = e.Count; raised++; }
            EventBus.Subscribe<CatchSold>(OnSold);

            int paid = SellService.SellAll(hold, wallet, market);
            EventBus.Unsubscribe<CatchSold>(OnSold);

            Assert.AreEqual(expected, paid, "sell-all pays the per-category marginal grand total");
            Assert.AreEqual(paid, wallet.Money, "the wallet gains exactly that");
            Assert.AreEqual(0, hold.UsedUnits, "the whole hold is cleared");
            Assert.AreEqual(1, raised, "one CatchSold covers the whole sell-all");
            Assert.AreEqual(5, seenCount, "all five units");
            Assert.AreEqual(paid, seenTotal);
        }

        [Test]
        public void SellAll_EmptyHold_PaysNothing()
        {
            var market = MakeMarket();
            var hold = new FakeHold();
            var wallet = new FakeWallet();

            int raised = 0;
            void OnSold(CatchSold _) => raised++;
            EventBus.Subscribe<CatchSold>(OnSold);
            int paid = SellService.SellAll(hold, wallet, market);
            EventBus.Unsubscribe<CatchSold>(OnSold);

            Assert.AreEqual(0, paid);
            Assert.AreEqual(0, wallet.Money);
            Assert.AreEqual(0, raised, "no CatchSold on an empty sell-all");
        }
    }
}
