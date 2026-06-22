using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Economy;

namespace HiddenHarbours.Tests.Economy
{
    /// <summary>
    /// VS-16 market depth (2): PER-CATEGORY demand isolation, and FishBuyer.SellAll routed through the
    /// within-lot MARGINAL path (so the instant "sell the lot" slides down each category's curve like
    /// the sell screen). Own asmdef so it never collides with the shared EditMode tests other roles
    /// edit in parallel. Deterministic given (sell history) — no RNG (rule 5).
    /// </summary>
    public class MarketDepth2Tests
    {
        // cod = a groundfish that holds value; mackerel = a schooling pelagic that gluts and crashes.
        private const FishCategory Cod = FishCategory.InshoreGroundfish;
        private const FishCategory Mackerel = FishCategory.Pelagic;

        private readonly List<Object> _spawned = new();

        // ---- fakes for the Core contracts ---------------------------------------------------
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
        public void TearDown()
        {
            EventBus.Clear<CatchSold>();
            foreach (var o in _spawned)
                if (o != null) Object.DestroyImmediate(o);
            _spawned.Clear();
        }

        // ---- builders -----------------------------------------------------------------------

        private GameConfig MakeConfig(float cove = 1f, float greywick = 1.4f, float recovery = 0.5f)
        {
            var c = ScriptableObject.CreateInstance<GameConfig>();
            c.MarketDemandCove = cove;
            c.MarketDemandGreywick = greywick;
            c.MarketDailyRecovery = recovery;
            _spawned.Add(c);
            return c;
        }

        private Market MakeMarket(GameConfig config = null, MarketId id = MarketId.Cove)
        {
            var go = new GameObject("Market");
            _spawned.Add(go);
            var m = go.AddComponent<Market>();
            m.Configure(config, id);
            return m;
        }

        private FishBuyer MakeBuyer(Market market)
        {
            var go = new GameObject("Buyer");
            _spawned.Add(go);
            var buyer = go.AddComponent<FishBuyer>();
            var f = typeof(FishBuyer).GetField("_market", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(f, "FishBuyer._market field not found");
            f.SetValue(buyer, market);
            return buyer;
        }

        private static FakeHold HoldOf(FishCategory cat, int n, int baseValue, float elasticity)
        {
            var h = new FakeHold();
            for (int i = 0; i < n; i++)
                h.TryAdd(new CatchItem($"fish.{cat}_{i}", cat.ToString(), cat, 5f, baseValue, elasticity));
            return h;
        }

        // ---- (1) per-category demand isolation ----------------------------------------------

        [Test]
        public void GluttingCod_DoesNotMoveMackerelPrice()
        {
            var market = MakeMarket();
            market.SetCategoryDemand(Cod, 2.0f);
            market.SetCategoryDemand(Mackerel, 0.8f);

            market.RegisterSale(Mackerel, 3);                 // mackerel carries its own supply
            float mackBefore = market.PriceMultiplier(Mackerel, 0.3f);

            market.RegisterSale(Cod, 40);                     // hammer cod
            float mackAfter = market.PriceMultiplier(Mackerel, 0.3f);

            Assert.AreEqual(mackBefore, mackAfter, 1e-6f,
                "glutting cod must not move mackerel — supply AND demand are per-category");
        }

        [Test]
        public void PerCategoryDemand_DifferentiatesPrice_AtTheSameSupply()
        {
            var market = MakeMarket();
            market.SetCategoryDemand(Cod, 2.0f);              // a buyer that wants cod
            market.SetCategoryDemand(Mackerel, 0.8f);         // and not much mackerel

            market.RegisterSale(Cod, 3);
            market.RegisterSale(Mackerel, 3);                 // identical supply

            Assert.Greater(market.PriceMultiplier(Cod, 0.3f), market.PriceMultiplier(Mackerel, 0.3f),
                "higher per-category demand holds cod's price above mackerel's at the same supply");
        }

        [Test]
        public void DemandFor_FallsBackToBaseline_ThenOverrideWins()
        {
            var market = MakeMarket(MakeConfig(cove: 1f, greywick: 1.4f), MarketId.Greywick);
            Assert.AreEqual(1.4f, market.DemandFor(Cod), 1e-6f, "an unlisted category uses the market baseline (Greywick=1.4)");

            market.SetCategoryDemand(Cod, 0.5f);
            Assert.AreEqual(0.5f, market.DemandFor(Cod), 1e-6f, "a per-category override wins over the baseline");
            Assert.AreEqual(1.4f, market.DemandFor(Mackerel), 1e-6f, "other categories still use the baseline");
        }

        // ---- (2) FishBuyer.SellAll routed through the marginal path --------------------------

        [Test]
        public void SellAll_Marginal_EqualsSumOfPerUnitMarginals()
        {
            const int n = 6, baseValue = 30;
            const float e = 0.3f;
            var market = MakeMarket();
            market.SetCategoryDemand(Mackerel, 1.5f);          // non-trivial demand — proves it's threaded through
            var buyer = MakeBuyer(market);

            // Independently sum the per-unit marginals (each unit prices into the supply prior units pushed
            // up, by the same increment SellService uses). Cross-checks the MarketMath helper agrees.
            float demand = market.DemandFor(Mackerel);
            int expected = 0;
            for (int k = 0; k < n; k++)
                expected += MarketMath.MarginalPrice(baseValue, k * SellPricing.SupplyPerUnit, e, demand);

            var hold = HoldOf(Mackerel, n, baseValue, e);
            var wallet = new FakeWallet();
            int paid = buyer.SellAll(hold, wallet);

            Assert.Greater(expected, 0);
            Assert.AreEqual(expected, paid,
                "SellAll pays the sum of per-unit self-glutting marginals (per-category demand applied)");
            Assert.AreEqual(paid, wallet.Money, "the wallet gains exactly the marginal total");
            Assert.AreEqual(0, hold.UsedUnits, "the hold is cleared");
        }

        [Test]
        public void SellAll_SlidesDownTheCurve_NotAPreGlutBatch()
        {
            // A multi-unit same-category lot must earn LESS than N × the first-unit price — it self-glutts
            // within the lot (the whole point of routing through the marginal path).
            const int n = 5, baseValue = 30;
            const float e = 0.4f;
            var market = MakeMarket();
            var buyer = MakeBuyer(market);

            int firstUnit = MarketMath.MarginalPrice(baseValue, 0f, e, market.DemandFor(Mackerel));
            var hold = HoldOf(Mackerel, n, baseValue, e);
            int paid = buyer.SellAll(hold, new FakeWallet());

            Assert.Less(paid, n * firstUnit, "a big lot slides down its own curve, not all at the pre-glut price");
        }

        [Test]
        public void SellAll_RegistersGlut_PerCategory_ForFutureSales()
        {
            var market = MakeMarket();
            var buyer = MakeBuyer(market);

            buyer.SellAll(HoldOf(Cod, 4, 20, 0.3f), new FakeWallet());

            Assert.AreEqual(4f, market.SupplyOf(Cod), 1e-4f, "the sold lot raised cod's supply (future prices drop)");
            Assert.AreEqual(0f, market.SupplyOf(Mackerel), 1e-4f, "and left mackerel's supply untouched");
        }
    }
}
