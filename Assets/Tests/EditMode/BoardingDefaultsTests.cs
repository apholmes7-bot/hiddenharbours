using HiddenHarbours.Core;
using NUnit.Framework;
using UnityEngine;

namespace HiddenHarbours.Tests.EditMode
{
    public class BoardingDefaultsTests
    {
        [Test]
        public void FreshConfig_PreservesTheExistingReachAndFacingRules()
        {
            var config = ScriptableObject.CreateInstance<GameConfig>();
            try
            {
                Assert.AreEqual(3.5f, config.BoardReachMetres);
                Assert.AreEqual(1.5f, config.StepAshoreReachMetres);
                Assert.AreEqual(120f, config.StepAshoreFacingArcDegrees);
                Assert.AreEqual(0.45f, config.WashboardWidthMetres);
                Assert.AreEqual(0.5f, config.WashboardSlowFactor);
                Assert.AreEqual(0.7f, config.OverTheSideBodyWidthMetres);
            }
            finally { Object.DestroyImmediate(config); }
        }
    }
}
