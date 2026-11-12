using System;
using NUnit.Framework;
using HiddenHarbours.Fishing;

namespace HiddenHarbours.Tests.EditMode
{
    public class TrawlOperationTests
    {
        [Test]
        public void DefaultDefinitions_AreDisabledAndCannotStartAnOperation()
        {
            Assert.IsFalse(default(TrawlGearDef).IsValid);
            Assert.IsFalse(default(TrawlHandlingDef).IsValid);
            Assert.Throws<ArgumentException>(() => new TrawlOperation(default));
        }

        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        [TestCase(double.NegativeInfinity)]
        [TestCase(-1d)]
        public void Gear_RejectsNonFiniteAndNegativeTuning(double invalid)
        {
            var d = TrawlTestData.Gear; Assert.IsTrue(d.IsValid);
            d.SampleGameSeconds = invalid; Assert.IsFalse(d.IsValid);
            d = TrawlTestData.Gear; d.CaptureFraction01 = invalid; Assert.IsFalse(d.IsValid);
            d = TrawlTestData.Gear; d.DamagePerHardMetre = invalid; Assert.IsFalse(d.IsValid);
            d = TrawlTestData.Gear; d.MaxWarpDepthRatio = invalid; Assert.IsFalse(d.IsValid);
        }

        [Test]
        public void Gear_RejectsInvertedWindowsAndAbsentBudgets()
        {
            var d = TrawlTestData.Gear; d.MaxDepthMetres = 9d; Assert.IsFalse(d.IsValid);
            d = TrawlTestData.Gear; d.MinWarpDepthRatio = 0.9d; Assert.IsFalse(d.IsValid);
            d = TrawlTestData.Gear; d.MaxWarpDepthRatio = 1d; Assert.IsFalse(d.IsValid);
            d = TrawlTestData.Gear; d.MaxTowMetresPerGameSecond = 0.5d; Assert.IsFalse(d.IsValid);
            d = TrawlTestData.Gear; d.MaxDoorSpreadMetres = 3d; Assert.IsFalse(d.IsValid);
            d = TrawlTestData.Gear; d.CaptureFraction01 = 1.01d; Assert.IsFalse(d.IsValid);
            d = TrawlTestData.Gear; d.MaxSamples = 0; Assert.IsFalse(d.IsValid);
            d = TrawlTestData.Gear; d.MaxSchoolsPerSample = 0; Assert.IsFalse(d.IsValid);
            d = TrawlTestData.Gear; d.MaxEncounteredSchools = 0; Assert.IsFalse(d.IsValid);
            d = TrawlTestData.Gear; d.MaxTrackKnots = 1; Assert.IsFalse(d.IsValid);
            d = TrawlTestData.Gear; d.CodendCapacityIndividuals = 0; Assert.IsFalse(d.IsValid);
        }

        [Test]
        public void Handling_RequiresAnExplicitMatchingWorkStationAndFiniteWorkRates()
        {
            var d = TrawlTestData.Stern; Assert.IsTrue(d.IsValid);
            d.WorkStation = TrawlWorkStation.Port; Assert.IsFalse(d.IsValid);
            d.Kind = TrawlHandlingKind.Side; Assert.IsTrue(d.IsValid);
            d.WorkStation = TrawlWorkStation.Unstated; Assert.IsFalse(d.IsValid);
            d = TrawlTestData.Side; Assert.IsTrue(d.IsValid, "zero speed means stopped work, not invalid tuning");
            d.SortIndividualsPerGameSecond = 0d; Assert.IsFalse(d.IsValid);
            d = TrawlTestData.Stern; d.HaulingGameSeconds = double.NaN; Assert.IsFalse(d.IsValid);
            d = TrawlTestData.Stern; d.MaxWorkingSeaState01 = 1.01d; Assert.IsFalse(d.IsValid);
        }

        [Test]
        public void Stern_RequiresShootingAndHaulingWorkBeforeCodendReachesDeck()
        {
            var op = new TrawlOperation(TrawlTestData.Stern);
            Assert.AreEqual(TrawlPhase.Stowed, op.Phase);
            Assert.IsFalse(op.BeginHauling(1d, 0d, 0d, TrawlWorkStation.Astern));
            Assert.IsTrue(op.BeginShooting(10d, 2d, 0.5d, TrawlWorkStation.Astern));
            Assert.AreEqual(TrawlPhase.Shooting, op.Phase);
            Assert.IsFalse(op.CompleteShooting(13.999d));
            Assert.IsFalse(op.CompleteHauling(14d));
            Assert.IsTrue(op.CompleteShooting(14d));
            Assert.AreEqual(14d, op.TowStartGameSeconds);
            Assert.AreEqual(TrawlPhase.Towing, op.Phase);
            Assert.IsFalse(op.BeginHauling(14d, 1d, 0d, TrawlWorkStation.Astern), "zero-duration tow");
            Assert.IsTrue(op.BeginHauling(24d, 1d, 0.5d, TrawlWorkStation.Astern));
            Assert.AreEqual(24d, op.TowEndGameSeconds);
            Assert.AreEqual(TrawlPhase.Hauling, op.Phase);
            Assert.IsFalse(op.CompleteHauling(29.999d));
            Assert.IsTrue(op.CompleteHauling(30d));
            Assert.AreEqual(TrawlPhase.CodendOnDeck, op.Phase);
            Assert.IsFalse(op.CompleteHauling(31d));
            Assert.IsFalse(op.BeginShooting(31d, 0d, 0d, TrawlWorkStation.Astern));
        }

        [Test]
        public void Side_UsesItsOwnSideStopRequirementAndLongerWorkTimes()
        {
            var op = new TrawlOperation(TrawlTestData.Side);
            Assert.IsFalse(op.BeginShooting(0d, 0d, 0d, TrawlWorkStation.Astern));
            Assert.IsFalse(op.BeginShooting(0d, 0.01d, 0d, TrawlWorkStation.Starboard));
            Assert.IsTrue(op.BeginShooting(0d, 0d, 0d, TrawlWorkStation.Starboard));
            Assert.IsFalse(op.CompleteShooting(4d));
            Assert.IsTrue(op.CompleteShooting(8d));
            Assert.IsFalse(op.BeginHauling(18d, 0.01d, 0d, TrawlWorkStation.Starboard));
            Assert.IsTrue(op.BeginHauling(18d, 0d, 0d, TrawlWorkStation.Starboard));
            Assert.IsFalse(op.CompleteHauling(24d));
            Assert.IsTrue(op.CompleteHauling(30d));
        }

        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        [TestCase(-1d)]
        public void RejectedCommands_DoNotAdvanceTheJobOrItsClock(double bad)
        {
            var op = new TrawlOperation(TrawlTestData.Stern);
            Assert.IsFalse(op.BeginShooting(bad, 0d, 0d, TrawlWorkStation.Astern));
            Assert.IsFalse(op.BeginShooting(0d, bad, 0d, TrawlWorkStation.Astern));
            Assert.IsFalse(op.BeginShooting(0d, 0d, bad, TrawlWorkStation.Astern));
            Assert.IsTrue(op.BeginShooting(0d, 0d, 0d, TrawlWorkStation.Astern));
            Assert.IsFalse(op.CompleteShooting(bad));
            Assert.AreEqual(TrawlPhase.Shooting, op.Phase);
            Assert.IsTrue(op.CompleteShooting(4d));
        }

        [Test]
        public void LatePoll_PreservesDeadlineAndRejectsBackwardsCommands()
        {
            var op = new TrawlOperation(TrawlTestData.Stern);
            Assert.IsTrue(op.BeginShooting(100d, 0d, 0d, TrawlWorkStation.Astern));
            Assert.IsTrue(op.CompleteShooting(110d));
            Assert.AreEqual(104d, op.TowStartGameSeconds);
            Assert.IsFalse(op.BeginHauling(109d, 0d, 0d, TrawlWorkStation.Astern));
            Assert.IsFalse(op.BeginHauling(111d, 0d, 0.5001d, TrawlWorkStation.Astern));
            Assert.IsTrue(op.BeginHauling(110d, 0d, 0d, TrawlWorkStation.Astern));
        }

        [Test]
        public void UnrepresentableWorkDeadline_FailsWithoutStarting()
        {
            var op = new TrawlOperation(TrawlTestData.Stern);
            Assert.IsFalse(op.BeginShooting(double.MaxValue, 0d, 0d, TrawlWorkStation.Astern));
            Assert.AreEqual(TrawlPhase.Stowed, op.Phase);
        }
    }
}
