using HiddenHarbours.Core;
using NUnit.Framework;

namespace HiddenHarbours.Tests.EditMode
{
    public class BoardingDefaultsTests
    {
        [Test]
        public void SharedBoardingDefaults_PreserveTheExistingReachAndFacingRules()
        {
            Assert.AreEqual(3.5f, GameConfig.DefaultBoardReachMetres);
            Assert.AreEqual(1.5f, GameConfig.DefaultStepAshoreReachMetres);
            Assert.AreEqual(120f, GameConfig.DefaultStepAshoreFacingArcDegrees);
            Assert.AreEqual(0.45f, GameConfig.DefaultWashboardWidthMetres);
            Assert.AreEqual(0.5f, GameConfig.DefaultWashboardSlowFactor);
            Assert.AreEqual(0.7f, GameConfig.DefaultOverTheSideBodyWidthMetres);
        }
    }
}
