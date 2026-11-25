using System;
using NUnit.Framework;
using HiddenHarbours.Fishing;

namespace HiddenHarbours.Tests.EditMode
{
    public class TrawlDeckBatchTests
    {
        [Test]
        public void Sorting_IsManualTimedAndConservesEveryIndividualOnDeckOrReleased()
        {
            var catchProposal = TrawlTestData.Resolve();
            var deck = new TrawlDeckBatch(catchProposal, TrawlTestData.Stern, 30d);
            Assert.AreEqual(12, deck.UnsortedCount); Assert.IsFalse(deck.ReadyToCarry);
            Assert.AreEqual(0, deck.KeptOnDeckCount); Assert.AreEqual(0, deck.ReleasedCount);
            Assert.IsFalse(deck.SortByHand(31.99d, 0, 4, TrawlSortDestination.KeepOnDeck));
            Assert.AreEqual(12, deck.UnsortedAt(0));
            Assert.IsTrue(deck.SortByHand(32d, 0, 4, TrawlSortDestination.KeepOnDeck));
            Assert.AreEqual(8, deck.UnsortedAt(0)); Assert.AreEqual(4, deck.KeptOnDeckAt(0));
            Assert.IsFalse(deck.SortByHand(32d, 0, 8, TrawlSortDestination.Release));
            Assert.IsTrue(deck.SortByHand(36d, 0, 8, TrawlSortDestination.Release));
            Assert.IsTrue(deck.ReadyToCarry); Assert.AreEqual(0, deck.UnsortedCount);
            Assert.AreEqual(4, deck.KeptOnDeckCount); Assert.AreEqual(8, deck.ReleasedCount);
            Assert.AreEqual(12, deck.UnsortedAt(0) + deck.KeptOnDeckAt(0) + deck.ReleasedAt(0));
            Assert.AreEqual(12, catchProposal.Count, "sorting cannot mutate the replayable proposal");
        }

        [Test]
        public void Bycatch_IsPresentForSortingAndCanBeReleasedByHand()
        {
            var survey = new TrawlTestData.Survey { Populations = new[]
            { TrawlTestData.Population(), TrawlTestData.Population("fish.test_haddock", 2, 5) } };
            var r = TrawlTestData.Resolve(survey);
            var deck = new TrawlDeckBatch(r, TrawlTestData.Stern, 0d);
            Assert.IsFalse(r[0].IsBycatch); Assert.IsTrue(r[1].IsBycatch);
            Assert.IsTrue(deck.SortByHand(6d, 0, 12, TrawlSortDestination.KeepOnDeck));
            Assert.IsTrue(deck.SortByHand(8.5d, 1, 5, TrawlSortDestination.Release));
            Assert.AreEqual(12, deck.KeptOnDeckCount); Assert.AreEqual(5, deck.ReleasedCount);
            Assert.IsTrue(deck.ReadyToCarry);
        }

        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        [TestCase(-1d)]
        public void InvalidClockOrQuantity_ChangesNothing(double time)
        {
            var deck = new TrawlDeckBatch(TrawlTestData.Resolve(), TrawlTestData.Stern, 0d);
            Assert.IsFalse(deck.SortByHand(time, 0, 1, TrawlSortDestination.KeepOnDeck));
            Assert.IsFalse(deck.SortByHand(10d, 0, 0, TrawlSortDestination.KeepOnDeck));
            Assert.IsFalse(deck.SortByHand(10d, 0, 13, TrawlSortDestination.KeepOnDeck));
            Assert.IsFalse(deck.SortByHand(10d, -1, 1, TrawlSortDestination.KeepOnDeck));
            Assert.IsFalse(deck.SortByHand(10d, 0, 1, (TrawlSortDestination)99));
            Assert.AreEqual(12, deck.UnsortedCount); Assert.AreEqual(0, deck.KeptOnDeckCount);
            Assert.IsTrue(deck.SortByHand(0.5d, 0, 1, TrawlSortDestination.KeepOnDeck));
        }

        [Test]
        public void SortedCatch_CannotBeSortedAgainOrReversedThroughATimeRewind()
        {
            var deck = new TrawlDeckBatch(TrawlTestData.Resolve(), TrawlTestData.Stern, 0d);
            Assert.IsTrue(deck.SortByHand(5d, 0, 4, TrawlSortDestination.KeepOnDeck));
            Assert.IsFalse(deck.SortByHand(4d, 0, 4, TrawlSortDestination.Release));
            Assert.IsTrue(deck.SortByHand(9d, 0, 8, TrawlSortDestination.Release));
            Assert.IsFalse(deck.SortByHand(100d, 0, 1, TrawlSortDestination.KeepOnDeck));
            Assert.AreEqual(12, deck.KeptOnDeckCount + deck.ReleasedCount);
        }

        [Test]
        public void FailedSurveyOrInvalidHandling_CannotBecomeADeckBatch()
        {
            var r = TrawlTestData.Resolve(new TrawlTestData.Survey { ReturnCount = -1 });
            Assert.Throws<ArgumentException>(() => new TrawlDeckBatch(r, TrawlTestData.Stern, 0d));
            Assert.Throws<ArgumentException>(() => new TrawlDeckBatch(TrawlTestData.Resolve(), default, 0d));
            Assert.Throws<ArgumentOutOfRangeException>(() => new TrawlDeckBatch(TrawlTestData.Resolve(), TrawlTestData.Stern, double.NaN));
        }

        [Test]
        public void EmptyCodend_HasNothingToSortAndCreatesNoKeptCatch()
        {
            var r = TrawlTestData.Resolve(new TrawlTestData.Survey { Populations = Array.Empty<TrawlPopulationSample>() });
            var deck = new TrawlDeckBatch(r, TrawlTestData.Stern, 0d);
            Assert.IsTrue(deck.ReadyToCarry); Assert.AreEqual(0, deck.KeptOnDeckCount);
            Assert.IsFalse(deck.SortByHand(100d, 0, 1, TrawlSortDestination.KeepOnDeck));
        }

        [Test]
        public void ShootTowHaulSort_ComposesWithoutAnySceneInventoryOrClockService()
        {
            var op = new TrawlOperation(TrawlTestData.Stern);
            Assert.IsTrue(op.BeginShooting(0d, 0d, 0d, TrawlWorkStation.Astern));
            Assert.IsTrue(op.CompleteShooting(4d));
            Assert.IsTrue(op.BeginHauling(14d, 0d, 0d, TrawlWorkStation.Astern));
            var track = TrawlTestData.Track(start: op.TowStartGameSeconds, duration: op.TowEndGameSeconds - op.TowStartGameSeconds);
            var r = TrawlTestData.Resolve(track: track);
            Assert.IsFalse(op.CompleteHauling(19d)); Assert.IsTrue(op.CompleteHauling(20d));
            var deck = new TrawlDeckBatch(r, TrawlTestData.Stern, 20d);
            Assert.IsTrue(deck.SortByHand(26d, 0, 12, TrawlSortDestination.KeepOnDeck));
            Assert.IsTrue(deck.ReadyToCarry); Assert.AreEqual(12, deck.KeptOnDeckCount);
        }
    }
}
