using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Economy;

namespace HiddenHarbours.Tests.Economy
{
    /// <summary>
    /// <b>THE GAP THAT MAKES THE CROSSING PAY</b> — plan-to-m1 §7.5's "deliberately worse prices" at the
    /// island counter, and §7.2's "better prices than the island store" at the creek. #356 built the
    /// channel and the arithmetic; nothing has ever asserted the two ends against each other.
    ///
    /// <para><b>Why this is a real test and not a tautology.</b> Demand alone could never have done this
    /// job: D only appears as <c>S/D</c> inside <c>1/(1+e·S/D)</c>, so at zero supply that term is 1 for
    /// EVERY value of D and an un-glutted village counter quotes exactly what an un-glutted wharf does.
    /// The first bucket of clams in the game is sold at zero supply. So the thing that has to be true is
    /// about the price LEVEL, and it has to be true at zero supply — which is precisely where a
    /// demand-only test would have passed while the game was wrong.</para>
    ///
    /// <para>And it is a DATA gap, not a code branch: every number here comes off
    /// <see cref="GameConfig"/>, so the owner can tune the whole lesson without opening C#
    /// (CLAUDE.md rule 6).</para>
    /// </summary>
    public class MarketPriceGapTests
    {
        private const FishCategory Shellfish = FishCategory.Shellfish;   // the clam: the first thing sold

        private readonly List<Object> _spawned = new();

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

        /// <summary>A config carrying the SHIPPED defaults — deliberately not a fixture, because what is
        /// under test is the balance the game actually ships with.</summary>
        private GameConfig ShippedConfig()
        {
            var c = ScriptableObject.CreateInstance<GameConfig>();
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

        // ---- 1. the shipped balance ----------------------------------------------------------

        [Test]
        public void TheIslandCounterPaysLessThanTheCreeksWharf()
        {
            var config = ShippedConfig();

            Assert.Less(config.MarketPriceLevelStPetersStore, config.MarketPriceLevelNineMileCreek,
                "the island store must pay LESS than Nine Mile Creek, or the tide-gated crossing has no " +
                "economic reason on top of its story one and 'where you sell matters' is never taught");
            Assert.Greater(config.MarketPriceLevelStPetersStore, 0f,
                "…but it still buys: a counter that pays nothing is a closed shop, not a worse price");
        }

        [Test]
        public void TheGapIsVisibleAtZeroSupply_WhichIsWhereTheFirstSaleHappens()
        {
            var config = ShippedConfig();
            var island = MakeMarket(config, MarketId.StPetersStore);
            var creek = MakeMarket(config, MarketId.NineMileCreek);

            // Nothing sold anywhere: the glut term is 1 at both, so any difference here is the LEVEL.
            Assert.AreEqual(0f, island.SupplyOf(Shellfish), 1e-6f);
            Assert.AreEqual(0f, creek.SupplyOf(Shellfish), 1e-6f);

            Assert.Less(island.PriceLevel, creek.PriceLevel,
                "this is the exact case demand could not express — see the class remarks");
        }

        [Test]
        public void TheSameBucketOfClamsPaysMoreOnTheCreeksWharf()
        {
            var config = ShippedConfig();
            var island = MakeMarket(config, MarketId.StPetersStore);
            var creek = MakeMarket(config, MarketId.NineMileCreek);

            // A base value with room to move: the ₲1 unit floor squeezes a 2₲ clam almost flat, which is
            // a real balance note (m1-progression-pacing §5) and would make this assert nothing.
            const int baseValue = 40;
            const float elasticity = 0.3f;

            int islandPays = island.NextUnitPrice(Shellfish, baseValue, elasticity);
            int creekPays = creek.NextUnitPrice(Shellfish, baseValue, elasticity);

            Assert.Less(islandPays, creekPays,
                $"the same unit fetches {islandPays}₲ at the village counter and {creekPays}₲ on the " +
                "creek's wharf — if those are equal, the walk across the bar bought the player nothing");
        }

        // ---- 2. it is a tunable, not a branch --------------------------------------------------

        [Test]
        public void TheGapIsAGameConfigKnob_TheOwnerCanCloseOrWidenItWithoutCode()
        {
            var config = ShippedConfig();
            var island = MakeMarket(config, MarketId.StPetersStore);
            var creek = MakeMarket(config, MarketId.NineMileCreek);

            config.MarketPriceLevelStPetersStore = 0.25f;   // punishing
            Assert.AreEqual(0.25f, island.PriceLevel, 1e-4f, "the level is read LIVE off the config asset");

            config.MarketPriceLevelStPetersStore = creek.PriceLevel;   // and the lesson can be turned off
            Assert.AreEqual(creek.PriceLevel, island.PriceLevel, 1e-4f);
        }

        [Test]
        public void EachOutletsLevelIsItsOwnKnob()
        {
            var config = ShippedConfig();
            var cove = MakeMarket(config, MarketId.Cove);
            var creek = MakeMarket(config, MarketId.NineMileCreek);
            var island = MakeMarket(config, MarketId.StPetersStore);

            config.MarketPriceLevelNineMileCreek = 1.75f;

            Assert.AreEqual(1.75f, creek.PriceLevel, 1e-4f);
            Assert.AreNotEqual(1.75f, cove.PriceLevel,
                "the home cove and the creek must be independently tunable — they are different places");
            Assert.AreNotEqual(1.75f, island.PriceLevel);
        }

        /// <summary>
        /// The regression this suite is really guarding, and the one the creek's builder shipped for a
        /// year: a <see cref="Market"/> left on its DEFAULT id reads the home cove's numbers. At the
        /// shipped balance the cove and the creek happen to pay the same, so a mis-wired creek buyer
        /// still beats the island counter and every test about the gap still passes — while the outlet
        /// itself is the wrong one and every later tuning of the creek does nothing.
        /// </summary>
        [Test]
        public void AMarketLeftOnItsDefaultReadsTheCove_WhichIsWhyTheIdMustBeSaidOutLoud()
        {
            var config = ShippedConfig();
            config.MarketPriceLevelCove = 1f;
            config.MarketPriceLevelNineMileCreek = 1.4f;

            var go = new GameObject("UnwiredMarket");
            _spawned.Add(go);
            var unwired = go.AddComponent<Market>();
            // Only the config is wired — exactly the bug: no id, so it is silently the Cove.
            unwired.Configure(config, default);

            Assert.AreEqual(MarketId.Cove, default(MarketId), "the default is the cove, and that is the trap");
            Assert.AreEqual(config.MarketPriceLevelCove, unwired.PriceLevel, 1e-4f,
                "an un-identified market quotes the COVE. Nothing throws, nothing logs, and the creek " +
                "stops being the creek");
            Assert.AreNotEqual(config.MarketPriceLevelNineMileCreek, unwired.PriceLevel);
        }
    }
}
