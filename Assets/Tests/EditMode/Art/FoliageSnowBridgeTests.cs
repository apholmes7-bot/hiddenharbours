using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools.Constraints;
using HiddenHarbours.Art;
using HiddenHarbours.Core;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// The snow bridge (<see cref="FoliageSnowBridge"/>) — what reaches the <c>_FoliageSnow</c> global the
    /// pass-4 tree shader thresholds its snow map against. The same calendar day must publish the same
    /// number every time (the snow is recomputed, never saved), the day event must publish the day it
    /// carries, publishing must not allocate, and the plate hook must pin the cover without the clock and
    /// hand it back afterwards.
    ///
    /// <para>No scene: EditMode never runs <c>OnEnable</c>, so the event path is driven through the
    /// bridge's own <c>Listen</c>/<c>StopListening</c>. The global, the clock and the config are restored
    /// after every test.</para>
    /// </summary>
    public class FoliageSnowBridgeTests
    {
        private float _savedGlobal;
        private IGameClock _savedClock;
        private GameConfig _savedConfig;

        [SetUp]
        public void SetUp()
        {
            _savedGlobal = FoliageSnowBridge.Published;
            _savedClock = GameServices.Clock;
            _savedConfig = GameServices.Config;
            GameServices.Clock = null;
            GameServices.Config = null;
            FoliageSnowBridge.ClearOverride();
        }

        [TearDown]
        public void TearDown()
        {
            FoliageSnowBridge.StopListening();
            FoliageSnowBridge.ClearOverride();
            GameServices.Clock = _savedClock;
            GameServices.Config = _savedConfig;
            Shader.SetGlobalFloat(FoliageSnowBridge.CoverageProperty, _savedGlobal);
        }

        private static float Curve(Season season, int day)
            => FoliageSnowMath.Coverage(season, day, GameConfig.DefaultDaysPerSeason, FoliageSnowSettings.Default);

        [Test]
        public void Publish_WritesTheCalendarsCover_ForEveryDayOfTheYear()
        {
            for (int s = 0; s < 4; s++)
            for (int d = 1; d <= GameConfig.DefaultDaysPerSeason; d++)
            {
                FoliageSnowBridge.Publish((Season)s, d);
                Assert.AreEqual(Curve((Season)s, d), FoliageSnowBridge.Published, 0f, $"{(Season)s} day {d}");
            }
        }

        [Test]
        public void Publish_TheSameDayTwice_PublishesTheSameNumber()
        {
            FoliageSnowBridge.Publish(Season.TheTurn, 24);
            float first = FoliageSnowBridge.Published;
            FoliageSnowBridge.Publish(Season.HardWinter, 3);
            FoliageSnowBridge.Publish(Season.TheTurn, 24);
            Assert.AreEqual(first, FoliageSnowBridge.Published, 0f);
        }

        [Test]
        public void Publish_DoesNotAllocate()
        {
            FoliageSnowBridge.Publish(Season.TheTurn, 24);   // warm the statics and the property id
            Assert.That(() => FoliageSnowBridge.Publish(Season.TheTurn, 25), Is.Not.AllocatingGCMemory());
        }

        [Test]
        public void ADayStartedEvent_PublishesTheDayItCarries()
        {
            FoliageSnowBridge.Listen();
            EventBus.Publish(new DayStarted(25, Season.TheTurn, 1));
            Assert.AreEqual(Curve(Season.TheTurn, 25), FoliageSnowBridge.Published, 0f);

            EventBus.Publish(new DayStarted(10, Season.HighSummer, 2));
            Assert.AreEqual(0f, FoliageSnowBridge.Published, 0f, "a summer day is bare");
        }

        [Test]
        public void StopListening_LeavesTheGlobalAlone()
        {
            FoliageSnowBridge.Listen();
            EventBus.Publish(new DayStarted(5, Season.HardWinter, 1));
            FoliageSnowBridge.StopListening();
            EventBus.Publish(new DayStarted(5, Season.HighSummer, 1));
            Assert.AreEqual(1f, FoliageSnowBridge.Published, 1e-6f, "an unsubscribed bridge must not keep publishing");
        }

        [Test]
        public void WithNoClock_PublishFromClock_LeavesTheGlobalAlone()
        {
            // A sentinel, so "publishes nothing" cannot pass by writing 0. In the game the global's default
            // IS 0, so a scene with no clock draws its trees bare, as they drew before the bridge.
            Shader.SetGlobalFloat(FoliageSnowBridge.CoverageProperty, 0.37f);
            FoliageSnowBridge.PublishFromClock();
            Assert.AreEqual(0.37f, FoliageSnowBridge.Published, 0f, "with no clock the bridge must publish nothing");
        }

        [Test]
        public void TheOverride_PinsTheCover_ThroughTheDayEvents_AndClearHandsItBack()
        {
            FoliageSnowBridge.Listen();
            FoliageSnowBridge.OverrideCoverage(0.5f);
            Assert.IsTrue(FoliageSnowBridge.IsOverridden);
            Assert.AreEqual(0.5f, FoliageSnowBridge.Published, 0f);

            EventBus.Publish(new DayStarted(10, Season.HighSummer, 1));
            Assert.AreEqual(0.5f, FoliageSnowBridge.Published, 0f, "a plate's pinned cover survives the day events");

            FoliageSnowBridge.OverrideCoverage(7f);
            Assert.AreEqual(1f, FoliageSnowBridge.Published, 0f, "the pin is clamped to full cover");

            FoliageSnowBridge.ClearOverride();
            Assert.IsFalse(FoliageSnowBridge.IsOverridden);
            Assert.AreEqual(0f, FoliageSnowBridge.Published, 0f, "with no clock the global goes back to bare");
        }
    }
}
