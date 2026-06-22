using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Economy;

namespace HiddenHarbours.Tests.EditMode.Regression
{
    /// <summary>
    /// qa-test regression net (expand net 2) over the VS-16 second market — that Port Greywick prices a
    /// glut differently from the cove. The demand lever is real on the <see cref="Market"/> class
    /// (<see cref="Market.NextUnitPrice"/> is demand-aware), proven here via the integer per-unit price
    /// (distinct from the float <c>PriceMultiplier</c> GreywickMarketTests already covers).
    ///
    /// <para><b>Demand now reaches the sell screen too (market depth 2):</b> <see cref="SellService"/> /
    /// <see cref="SellPricing"/> were threaded with the market's per-category demand, so selling the same
    /// hold at Greywick pays MORE than the cove — closing the gap this net originally flagged (the test
    /// below was flipped from a same-coin characterization to assert the fix). Test demand comes from a
    /// test GameConfig; the production GameConfig stays the single source of truth for shipped balance.</para>
    /// </summary>
    public class MarketComparisonTests
    {
        private const FishCategory Cat = FishCategory.InshoreGroundfish;

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

        private readonly List<Object> _spawned = new();

        [SetUp]
        public void SetUp() => EventBus.Clear<CatchSold>();

        [TearDown]
        public void TearDown()
        {
            EventBus.Clear<CatchSold>();
            foreach (var o in _spawned) if (o != null) Object.DestroyImmediate(o);
            _spawned.Clear();
        }

        // A config where Greywick demands more than the cove (the production intent; values are the
        // test's own so the real GameConfig asset stays the single source of truth for shipped balance).
        private GameConfig MakeConfig(float cove = 1f, float greywick = 1.5f)
        {
            var c = ScriptableObject.CreateInstance<GameConfig>();
            c.MarketDemandCove = cove;
            c.MarketDemandGreywick = greywick;
            c.MarketDailyRecovery = 0.5f;
            _spawned.Add(c);
            return c;
        }

        private Market MakeMarket(GameConfig config, MarketId id)
        {
            var go = new GameObject(id + "Market");
            _spawned.Add(go);
            var m = go.AddComponent<Market>();
            m.Configure(config, id);
            return m;
        }

        private static FakeHold HoldOf(int n, int baseValue, float elasticity)
        {
            var hold = new FakeHold();
            for (int i = 0; i < n; i++)
                hold.TryAdd(new CatchItem("fish.cod", "Cod", Cat, 4f, baseValue, elasticity));
            return hold;
        }

        // ---- the market's quoted unit price differs by buyer, at the SAME glut ---------------

        [Test]
        public void NextUnitPrice_IsDemandAware_GreywickBeatsCove_ForTheSameGlut()
        {
            var config = MakeConfig(cove: 1f, greywick: 1.5f);
            var cove = MakeMarket(config, MarketId.Cove);
            var greywick = MakeMarket(config, MarketId.Greywick);

            // Identical glut at both buyers.
            cove.RegisterSale(Cat, 5);
            greywick.RegisterSale(Cat, 5);
            Assert.AreEqual(cove.SupplyOf(Cat), greywick.SupplyOf(Cat), 1e-4f, "identical glut by construction");

            int covePrice = cove.NextUnitPrice(Cat, baseValue: 30, elasticity: 0.4f);
            int greywickPrice = greywick.NextUnitPrice(Cat, baseValue: 30, elasticity: 0.4f);

            Assert.Greater(greywickPrice, covePrice,
                "for the same glut, Greywick's higher demand quotes the next unit higher than the cove");
        }

        [Test]
        public void NextUnitPrice_EqualDemand_QuotesTheSame_IsolatingDemandAsTheLever()
        {
            var config = MakeConfig(cove: 1.2f, greywick: 1.2f); // same D at both
            var cove = MakeMarket(config, MarketId.Cove);
            var greywick = MakeMarket(config, MarketId.Greywick);
            cove.RegisterSale(Cat, 5);
            greywick.RegisterSale(Cat, 5);

            Assert.AreEqual(cove.NextUnitPrice(Cat, 30, 0.4f), greywick.NextUnitPrice(Cat, 30, 0.4f),
                "with equal demand both buyers quote the same — the gap above is demand, not market identity");
        }

        // ---- the sell SCREEN path now honours market demand (market depth 2 closed the gap) --

        [Test]
        public void SellScreenPath_IsDemandAware_GreywickPaysMoreThanCove()
        {
            var config = MakeConfig(cove: 1f, greywick: 1.5f);
            var cove = MakeMarket(config, MarketId.Cove);
            var greywick = MakeMarket(config, MarketId.Greywick);

            int covePaid     = SellService.SellAll(HoldOf(5, 24, 0.4f), new FakeWallet(), cove);
            int greywickPaid = SellService.SellAll(HoldOf(5, 24, 0.4f), new FakeWallet(), greywick);

            // Market depth 2 threaded per-category demand through SellPricing/SellService, so the screen's
            // payout (and the instant FishBuyer.SellAll that now routes through it) honours Greywick's
            // higher demand: selling the same hold at Greywick pays MORE. This was the documented gap this
            // net flagged — the characterization here was flipped from AreEqual once the gap was closed.
            Assert.Greater(covePaid, 0);
            Assert.Greater(greywickPaid, covePaid,
                "selling via the VS-18 screen now honours market demand — Greywick pays more than the cove");
        }
    }
}
