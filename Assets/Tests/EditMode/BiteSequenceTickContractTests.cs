using HiddenHarbours.Fishing;
using NUnit.Framework;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>Real-time bite policy: a new tell gets a fresh phase clock. The counterexample
    /// deliberately disproves partition invariance without leaving an intentionally red test.</summary>
    public class BiteSequenceTickContractTests
    {
        private static BiteSequenceSim Sequence(int nibbles = 0, bool returns = false)
            => new BiteSequenceSim(new BiteProfile(nibbles, nibbles, 0.25f, 0.25f,
                0.125f, 0.5f, returns ? 1f : 0f, 0.125f, 0.125f, returns ? 2 : 1),
                new System.Random(42));

        private static void FourQuarterTicks(BiteSequenceSim sequence)
        {
            for (int i = 0; i < 4; i++) sequence.Tick(0.25f, false);
        }

        [Test]
        public void F6_EqualElapsedTime_WithDifferentTickPartitions_HasDifferentOutcomes()
        {
            var large = Sequence();
            var partitioned = Sequence();
            large.Tick(1f, false);
            FourQuarterTicks(partitioned);

            Assert.AreEqual(BitePhase.TakeOpen, large.Phase);
            Assert.AreEqual(0f, large.PhaseSeconds);
            Assert.AreEqual(0f, large.TakeWindow01);
            Assert.AreEqual(0, large.PassIndex);
            Assert.AreEqual(BiteMiss.None, large.LastMiss);
            Assert.IsFalse(large.IsOver);
            Assert.AreEqual(BitePhase.Gone, partitioned.Phase);
            Assert.AreEqual(0.5f, partitioned.PhaseSeconds);
            Assert.AreEqual(1, partitioned.PassIndex);
            Assert.AreEqual(BiteMiss.Late, partitioned.LastMiss);
            Assert.IsTrue(partitioned.IsOver);
            Assert.AreNotEqual(large.Phase, partitioned.Phase,
                "An equality assertion here is the F6 failing partition-invariance case.");
        }

        [Test]
        public void F6_StrikeAfterEqualElapsedTime_HooksOnlyTheStillOpenTake()
        {
            var large = Sequence();
            var partitioned = Sequence();
            large.Tick(1f, false);
            FourQuarterTicks(partitioned);
            large.Tick(0f, true);
            partitioned.Tick(0f, true);

            Assert.AreEqual(BitePhase.Hooked, large.Phase);
            Assert.AreEqual(BiteMiss.None, large.LastMiss);
            Assert.AreEqual(BitePhase.Gone, partitioned.Phase);
            Assert.AreEqual(BiteMiss.Late, partitioned.LastMiss);
        }

        [TestCase(0.375f)]
        [TestCase(100f)]
        public void ApproachOverflow_IsDiscarded_AndTheNewTakeKeepsItsFullWindow(float dt)
        {
            var sequence = Sequence();
            sequence.Tick(dt, false);
            Assert.AreEqual(BitePhase.TakeOpen, sequence.Phase);
            Assert.AreEqual(0f, sequence.PhaseSeconds);
            sequence.Tick(0f, false);
            Assert.AreEqual(0f, sequence.PhaseSeconds, "No deferred time debt on the next call.");
            sequence.Tick(0.25f, false);
            Assert.AreEqual(BitePhase.TakeOpen, sequence.Phase);
            Assert.AreEqual(0.25f, sequence.PhaseSeconds);
            Assert.AreEqual(0.5f, sequence.TakeWindow01);
            sequence.Tick(0.25f, false);
            Assert.AreEqual(BitePhase.Gone, sequence.Phase);
            Assert.AreEqual(BiteMiss.Late, sequence.LastMiss);
        }

        [Test]
        public void HugeQuietTicks_VisitTheNibbleGapAndTake_OneTransitionPerCall()
        {
            var sequence = Sequence(nibbles: 1);
            sequence.Tick(100f, false);
            Assert.AreEqual(BitePhase.NibbleDip, sequence.Phase);
            Assert.AreEqual(0f, sequence.PhaseSeconds);
            Assert.AreEqual(0, sequence.NibbleIndex);
            sequence.Tick(100f, false);
            Assert.AreEqual(BitePhase.Approach, sequence.Phase);
            Assert.AreEqual(0f, sequence.PhaseSeconds);
            Assert.AreEqual(1, sequence.NibbleIndex);
            sequence.Tick(100f, false);
            Assert.AreEqual(BitePhase.TakeOpen, sequence.Phase);
            Assert.AreEqual(0f, sequence.PhaseSeconds);
            Assert.AreEqual(0, sequence.PassIndex);
            sequence.Tick(100f, true);
            Assert.AreEqual(BitePhase.Hooked, sequence.Phase);
        }

        [Test]
        public void StrikeBeforeTheTake_IsEarly_EvenWhenElapsedTimeWouldReachIt()
        {
            var sequence = Sequence();
            sequence.Tick(100f, true);
            Assert.AreEqual(BitePhase.Gone, sequence.Phase);
            Assert.AreEqual(BiteMiss.Early, sequence.LastMiss);
            Assert.AreEqual(1, sequence.PassIndex);
            Assert.AreEqual(0f, sequence.PhaseSeconds);
        }

        [Test]
        public void AwayOverflow_ReturnsToAFreshApproach_WithoutReusingTheStrike()
        {
            var sequence = Sequence(returns: true);
            sequence.Tick(0.25f, false);
            sequence.Tick(0.5f, false);
            Assert.AreEqual(BitePhase.Away, sequence.Phase);
            Assert.AreEqual(BiteMiss.Late, sequence.LastMiss);
            sequence.Tick(100f, true);
            Assert.AreEqual(BitePhase.Approach, sequence.Phase);
            Assert.AreEqual(0f, sequence.PhaseSeconds);
            Assert.AreEqual(1, sequence.PassIndex);
            Assert.AreEqual(BiteMiss.None, sequence.LastMiss);
            sequence.Tick(0f, false);
            Assert.AreEqual(BitePhase.Approach, sequence.Phase);
            Assert.AreEqual(0f, sequence.PhaseSeconds);
        }
    }
}
