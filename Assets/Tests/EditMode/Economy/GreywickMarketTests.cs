using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Economy;

namespace HiddenHarbours.Tests.Economy
{
    /// <summary>
    /// VS-16 — the deepened market: a second buyer (Port Greywick) with different demand than the cove,
    /// deterministic daily price recovery, and the additive marginal-price helper the sell screen reads.
    /// In its own assembly so it never collides with the shared EditMode tests other roles edit in
    /// parallel. Pricing is deterministic given (sell history, day count) — no RNG (rule 5).
    /// </summary>
    public class GreywickMarketTests
    {
        private const FishCategory Cat = FishCategory.Pelagic;

        private readonly List<Object> _spawned = new();

        private GameConfig MakeConfig(float cove = 1f, float greywick = 1.4f, float recovery = 0.5f)
        {
            var c = ScriptableObject.CreateInstance<GameConfig>();
            c.MarketDemandCove = cove;
            c.MarketDemandGreywick = greywick;
            c.MarketDailyRecovery = recovery;
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

        [SetUp]
        public void SetUp() => EventBus.Clear<DayStarted>();

        [TearDown]
        public void TearDown()
        {
            EventBus.Clear<DayStarted>();
            foreach (var o in _spawned)
                if (o != null) Object.DestroyImmediate(o);
            _spawned.Clear();
        }

        // ---- 1. a big lot depresses the price, which then recovers over days -----------------

        [Test]
        public void BigLot_DepressesPrice_ThenRecoversOverDays()
        {
            var market = MakeMarket(MakeConfig(recovery: 0.5f), MarketId.Cove);

            float baseMult = market.PriceMultiplier(Cat, 0.3f);   // supply 0 → full price
            market.RegisterSale(Cat, 20);                          // dump a big lot
            float glutMult = market.PriceMultiplier(Cat, 0.3f);
            Assert.Less(glutMult, baseMult, "a big lot floods supply and depresses the price");

            for (int day = 0; day < 10; day++) market.SettleDaily(); // patience pays
            float recovered = market.PriceMultiplier(Cat, 0.3f);
            Assert.Greater(recovered, glutMult, "the price recovers over the following days");
            Assert.AreEqual(baseMult, recovered, 0.05f, "and climbs back toward the pre-glut price");
        }

        [Test]
        public void DailySettle_IsDeterministic_OnDayRollover()
        {
            var market = MakeMarket(MakeConfig(recovery: 0.5f), MarketId.Cove);
            market.RegisterSale(Cat, 8);
            float before = market.SupplyOf(Cat);

            // The day-rollover handler (same path the DayStarted signal drives) clears half the glut.
            market.OnDayStarted(new DayStarted(2, Season.EarlySpring, 1));

            Assert.AreEqual(before * 0.5f, market.SupplyOf(Cat), 1e-4f,
                "half the glut clears each day — deterministic, frame-rate independent");
        }

        // ---- 2. Greywick prices differently than the cove (a reason to choose where to sell) --

        [Test]
        public void Greywick_PricesDifferentlyThanCove_AtTheSameSupply()
        {
            var config = MakeConfig(cove: 1f, greywick: 1.4f);
            var cove = MakeMarket(config, MarketId.Cove);
            var greywick = MakeMarket(config, MarketId.Greywick);

            cove.RegisterSale(Cat, 5);
            greywick.RegisterSale(Cat, 5);                         // identical glut at both

            float covePrice = cove.PriceMultiplier(Cat, 0.2f);
            float greywickPrice = greywick.PriceMultiplier(Cat, 0.2f);

            Assert.AreNotEqual(covePrice, greywickPrice, "different demand → different price");
            Assert.Greater(greywickPrice, covePrice,
                "Greywick's higher demand absorbs the glut better → pays more (worth the hop)");
        }

        // ---- 3. mackerel (high elasticity) crashes faster than cod (low elasticity) ----------

        [Test]
        public void Mackerel_CrashesFasterThanCod_UnderTheSameGlut()
        {
            const int baseValue = 50;
            const float eMackerel = 0.6f;  // schooling pelagic — high elasticity (economy §1.5)
            const float eCod = 0.2f;       // groundfish — holds value
            const float supply = 3f;

            Assert.AreEqual(MarketMath.MarginalPrice(baseValue, 0f, eCod),
                            MarketMath.MarginalPrice(baseValue, 0f, eMackerel),
                            "with no glut both fetch the base price");

            int codGlut = MarketMath.MarginalPrice(baseValue, supply, eCod);
            int macGlut = MarketMath.MarginalPrice(baseValue, supply, eMackerel);

            Assert.Less(macGlut, codGlut, "under the same glut mackerel crashes lower than cod");
            Assert.Greater(baseValue - macGlut, baseValue - codGlut, "mackerel's price falls FASTER");
        }

        // ---- 4. marginal price is monotonic non-increasing as you sell more ------------------

        [Test]
        public void MarginalPrice_IsMonotonicNonIncreasing_AsYouSellMore()
        {
            const int baseValue = 40;
            const float e = 0.3f;

            int prev = int.MaxValue;
            for (float supply = 0f; supply <= 30f; supply += 1f)
            {
                int price = MarketMath.MarginalPrice(baseValue, supply, e);
                Assert.LessOrEqual(price, prev, $"the next unit must not pay MORE than the last (supply {supply})");
                Assert.GreaterOrEqual(price, 1, "a unit always fetches at least ₲1");
                prev = price;
            }
        }

        // ---- additive/stability: the demand-aware paths don't disturb the existing contract --

        [Test]
        public void DemandAwarePrice_AtNeutralDemand_EqualsTheOriginalTwoArg()
        {
            for (float s = 0f; s <= 20f; s += 2f)
                Assert.AreEqual(MarketMath.PriceMultiplier(s, 0.3f), MarketMath.PriceMultiplier(s, 0.3f, 1f), 1e-6f,
                    "D=1 must match the original 2-arg contract (callers building in parallel stay stable)");
        }

        [Test]
        public void HigherDemand_HoldsPriceUp_AtTheSameSupply()
        {
            float low = MarketMath.PriceMultiplier(5f, 0.3f, 1.0f);
            float high = MarketMath.PriceMultiplier(5f, 0.3f, 1.5f);
            Assert.Greater(high, low, "more demand absorbs the same supply → a higher price (the Greywick lever)");
        }
    }
}
