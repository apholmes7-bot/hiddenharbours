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
    /// <para><b>Gap this net surfaces:</b> the VS-18 sell SCREEN does NOT use that lever — <see cref="SellService"/>
    /// prices through <see cref="SellPricing"/>, which reads only the market's SUPPLY at neutral demand,
    /// so selling the same hold at Greywick pays the SAME as the cove. The characterization test below
    /// pins that current behavior and is flagged for economy-sim (see the PR). Test demand comes from a
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

        // ---- DOCUMENTED GAP: the sell SCREEN path ignores market demand ---------------------

        [Test]
        public void SellScreenPath_CurrentlyIgnoresMarketDemand_SameCoinAtBothMarkets_FLAGGED()
        {
            var config = MakeConfig(cove: 1f, greywick: 1.5f);
            var cove = MakeMarket(config, MarketId.Cove);
            var greywick = MakeMarket(config, MarketId.Greywick);

            int covePaid     = SellService.SellAll(HoldOf(5, 24, 0.4f), new FakeWallet(), cove);
            int greywickPaid = SellService.SellAll(HoldOf(5, 24, 0.4f), new FakeWallet(), greywick);

            // CHARACTERIZATION — documents current behavior, NOT an endorsement. The VS-18 sell screen
            // prices via SellPricing → MarketMath.PriceMultiplier(supply, elasticity) at NEUTRAL demand,
            // and SellService reads only Market.SupplyOf, never DemandFactor. So Greywick's VS-16 demand
            // advantage (real on Market.NextUnitPrice, asserted above, and on the older instant
            // FishBuyer.SellAll) does NOT reach the screen's payout. Flagged for economy-sim in the PR.
            // When the sell path is made demand-aware, flip this to Assert.Greater(greywickPaid, covePaid).
            Assert.Greater(covePaid, 0);
            Assert.AreEqual(covePaid, greywickPaid,
                "DOCUMENTED GAP: selling via the VS-18 screen ignores market demand — Greywick pays the same as the cove");
        }
    }
}
