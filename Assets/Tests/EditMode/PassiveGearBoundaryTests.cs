using System;
using HiddenHarbours.Core;
using HiddenHarbours.Fishing;
using NUnit.Framework;

namespace HiddenHarbours.Tests.EditMode
{
    public class PassiveGearBoundaryTests
    {
        [SetUp] public void SetUp() => GameServices.Reset();
        [TearDown] public void TearDown() => GameServices.Reset();

        [TestCase(0d)]
        [TestCase(-1d)]
        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        [TestCase(double.NegativeInfinity)]
        public void InvalidDayLength_UsesTheReferenceClockScale(double secondsPerDay)
        {
            Assert.AreEqual(GameConfig.DefaultSecondsPerDay / 2d, TrapSoak.SoakSeconds(12f, secondsPerDay));
            Assert.AreEqual(0.5f, TrapSoak.Progress01(0d, GameConfig.DefaultSecondsPerDay / 4d, 12f, secondsPerDay));
            Assert.IsFalse(TrapSoak.IsReady(0d, GameConfig.DefaultSecondsPerDay / 4d, 12f, secondsPerDay));
            Assert.IsTrue(TrapSoak.IsReady(0d, GameConfig.DefaultSecondsPerDay / 2d, 12f, secondsPerDay));
        }

        [TestCase(0f)]
        [TestCase(-2f)]
        public void NonPositiveSoak_IsImmediatelyReady_EvenBeforePlacementTime(float hours)
        {
            Assert.AreEqual(0d, TrapSoak.SoakSeconds(hours, 1800d));
            Assert.AreEqual(1f, TrapSoak.Progress01(100d, 99d, hours, 1800d));
            Assert.IsTrue(TrapSoak.IsReady(100d, 99d, hours, 1800d));
        }

        [TestCase(0d)]
        [TestCase(1000000000000d)]
        public void Readiness_KeepsFractionalSeconds_AtLongGameTimes(double placed)
        {
            // One authored hour is 0.75 seconds in this fixture, exactly representable at both epochs.
            Assert.AreEqual(0.5d, TrapSoak.ElapsedSeconds(placed, placed + 0.5d));
            Assert.AreEqual(2f / 3f, TrapSoak.Progress01(placed, placed + 0.5d, 1f, 18d));
            Assert.IsFalse(TrapSoak.IsReady(placed, placed + 0.5d, 1f, 18d));
            Assert.IsTrue(TrapSoak.IsReady(placed, placed + 0.75d, 1f, 18d));
        }

        [TestCase(-4f, 1)]
        [TestCase(0f, 1)]
        [TestCase(1f, 6)]
        [TestCase(4f, 6)]
        public void FillCount_ClampsTheFraction_BeforeResolvingSlots(float fill, int expected)
            => Assert.AreEqual(expected, TrapFill.ResolveCount(fill, 6, 42, "trap.fixture", 123.125d));

        [TestCase(-3)]
        [TestCase(0)]
        [TestCase(1)]
        public void NonPositiveCapacity_UsesTheExistingReadyFloor(int capacity)
        {
            Assert.AreEqual(1, TrapFill.ResolveCount(0f, capacity, 42, "trap.fixture", 0d));
            Assert.AreEqual(1, TrapFill.ResolveCount(1f, capacity, 42, "trap.fixture", 0d));
        }

        [Test]
        public void SlotAtItsExactFillThreshold_IsStillEmpty_ThenFillsAboveIt()
        {
            float threshold = DeckWork.U01(TrapFill.SlotHash(42, "trap.fixture", 123.125d, 1));
            Assert.Greater(threshold, 0f);
            Assert.Less(threshold, 1f);
            Assert.AreEqual(1, TrapFill.ResolveCount(threshold, 2, 42, "trap.fixture", 123.125d));
            Assert.AreEqual(2, TrapFill.ResolveCount(threshold + 1f / 16777216f, 2, 42, "trap.fixture", 123.125d));
        }

        [Test]
        public void ExplicitSoakScale_DoesNotDependOnTheLiveService()
        {
            Assert.AreEqual(24d, TrapSoak.SoakSeconds(12f, 48d));
            Assert.AreEqual(0.5f, TrapSoak.Progress01(100d, 112d, 12f, 48d));
            Assert.IsTrue(TrapSoak.IsReady(100d, 124d, 12f, 48d));
            Assert.AreEqual(GameConfig.DefaultSecondsPerDay / 24d, TrapSoak.SecondsPerHour);
        }
    }
}
