using NUnit.Framework;
using HiddenHarbours.Fishing;
using HiddenHarbours.Core;
using UnityEngine;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// The pure soak math for a placed trap (trap-fishing arc Build 3, rule 5): readiness + progress are a
    /// pure function of (placedAt, now, soakHours) — no stored progress, no per-tick counter. Deterministic,
    /// engine-free, so these run headless with no clock/scene.
    /// </summary>
    public class TrapSoakTests
    {
        [TearDown]
        public void ClearServices() => GameServices.Reset();

        [SetUp]
        public void ResetServices() => GameServices.Reset();

        private const double PlacedAt = 10_000.0;   // an arbitrary placement instant (in-game seconds)
        private const float SoakHours = 12f;
        private static readonly double SoakSpan = 12.0 * 75.0;   // 900 clock seconds

        [Test]
        public void NotReady_BeforeSoakSpanElapses()
        {
            double now = PlacedAt + SoakSpan - 1.0;   // one second short
            Assert.IsFalse(TrapSoak.IsReady(PlacedAt, now, SoakHours), "a second short of the soak is not ready");
        }

        [Test]
        public void Ready_AtAndAfterSoakSpan()
        {
            Assert.IsTrue(TrapSoak.IsReady(PlacedAt, PlacedAt + SoakSpan, SoakHours), "ready exactly at the span");
            Assert.IsTrue(TrapSoak.IsReady(PlacedAt, PlacedAt + SoakSpan + 1.0, SoakHours), "still ready after");
        }

        [Test]
        public void Progress_IsClamped01_AndLinear()
        {
            Assert.AreEqual(0f, TrapSoak.Progress01(PlacedAt, PlacedAt, SoakHours), 1e-6f, "0% at placement");
            Assert.AreEqual(0.5f, TrapSoak.Progress01(PlacedAt, PlacedAt + SoakSpan * 0.5, SoakHours), 1e-6f, "50% halfway");
            Assert.AreEqual(1f, TrapSoak.Progress01(PlacedAt, PlacedAt + SoakSpan, SoakHours), 1e-6f, "100% at the span");
            Assert.AreEqual(1f, TrapSoak.Progress01(PlacedAt, PlacedAt + SoakSpan * 2, SoakHours), 1e-6f, "clamped at 100%");
        }

        [Test]
        public void Progress_NeverNegative_WhenNowBeforePlacement()
        {
            // A clock oddity (now < placedAt) reads as "just placed", never a negative progress.
            Assert.AreEqual(0f, TrapSoak.Progress01(PlacedAt, PlacedAt - 5000.0, SoakHours), 1e-6f);
            Assert.IsFalse(TrapSoak.IsReady(PlacedAt, PlacedAt - 5000.0, SoakHours));
        }

        [Test]
        public void ZeroSoak_IsReadyImmediately()
        {
            Assert.IsTrue(TrapSoak.IsReady(PlacedAt, PlacedAt, 0f), "a zero-soak trap is ready the moment it's down");
            Assert.AreEqual(1f, TrapSoak.Progress01(PlacedAt, PlacedAt, 0f), 1e-6f);
        }

        [TestCase(1800d, 900d)]
        [TestCase(86400d, 43200d)]
        public void TwelveGameHours_FinishAtHalfTheConfiguredDay(double day, double span)
        {
            Assert.AreEqual(span, TrapSoak.SoakSeconds(12f, day));
            Assert.IsFalse(TrapSoak.IsReady(PlacedAt, PlacedAt + span - 1d, 12f, day));
            Assert.IsTrue(TrapSoak.IsReady(PlacedAt, PlacedAt + span, 12f, day));
            Assert.AreEqual(0.5f, TrapSoak.Progress01(PlacedAt, PlacedAt + span / 2d, 12f, day));
            Assert.AreEqual(1f, TrapSoak.Progress01(PlacedAt, PlacedAt + span, 12f, day));
        }

        [TestCase(1800f, 900d)]
        [TestCase(86400f, 43200d)]
        public void RuntimeWrappers_ReadTheLiveClockConfiguration(float day, double span)
        {
            var previous = GameServices.Config;
            var config = ScriptableObject.CreateInstance<GameConfig>();
            try
            {
                config.SecondsPerDay = day;
                GameServices.Config = config;
                Assert.AreEqual(span, TrapSoak.SoakSeconds(12f));
                Assert.AreEqual(0.5f, TrapSoak.Progress01(PlacedAt, PlacedAt + span / 2d, 12f));
                Assert.IsFalse(TrapSoak.IsReady(PlacedAt, PlacedAt + span - 1d, 12f));
                Assert.IsTrue(TrapSoak.IsReady(PlacedAt, PlacedAt + span, 12f));
                Assert.AreEqual(0.5f, TrapFill.Fill01(PlacedAt, PlacedAt + span * 2d, 12f, 36f));
            }
            finally { GameServices.Config = previous; Object.DestroyImmediate(config); }
        }

        [Test]
        public void SoakSeconds_ConvertsHoursToSeconds()
        {
            Assert.AreEqual(900.0, TrapSoak.SoakSeconds(12f), 1e-6);
            Assert.AreEqual(0.0, TrapSoak.SoakSeconds(0f), 1e-6);
            Assert.AreEqual(0.0, TrapSoak.SoakSeconds(-3f), 1e-6, "a negative soak is treated as instant");
        }
    }
}
