using NUnit.Framework;
using HiddenHarbours.Economy;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>Supply/demand pricing behaves: gluts crash prices, scarcity recovers them, bounded.</summary>
    public class MarketMathTests
    {
        [Test]
        public void Price_FallsAsSupplyRises()
        {
            float atZero = MarketMath.PriceMultiplier(0f, 0.3f);
            float atTen = MarketMath.PriceMultiplier(10f, 0.3f);
            Assert.Greater(atZero, atTen);
        }

        [Test]
        public void HigherElasticity_MeansLowerPriceAtSameSupply()
        {
            float gentle = MarketMath.PriceMultiplier(5f, 0.1f);
            float steep = MarketMath.PriceMultiplier(5f, 0.5f);
            Assert.Greater(gentle, steep);
        }

        [Test]
        public void Multiplier_StaysWithinBounds()
        {
            for (int s = 0; s < 100; s++)
            {
                float m = MarketMath.PriceMultiplier(s, 1f);
                Assert.GreaterOrEqual(m, MarketMath.MinMultiplier);
                Assert.LessOrEqual(m, MarketMath.MaxMultiplier);
            }
        }

        [Test]
        public void Supply_DecaysTowardZero()
        {
            Assert.AreEqual(7f, MarketMath.DecaySupply(10f, 1f, 3f), 0.001f);
            Assert.AreEqual(0f, MarketMath.DecaySupply(0.5f, 1f, 3f));  // clamped, never negative
        }
    }
}
