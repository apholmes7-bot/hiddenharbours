using HiddenHarbours.Core;
using HiddenHarbours.Fishing;
using NUnit.Framework;

namespace HiddenHarbours.Tests.EditMode
{
    public class FishingSequenceBoundaryTests
    {
        private static BiteProfile Bite() => new BiteProfile(0, 0, 0.25f, 0.25f,
            0.1f, 0.5f, 0f, 0f, 0f, 1);

        [TestCase(BitePhase.Approach, FishingPhase.Waiting)]
        [TestCase(BitePhase.NibbleDip, FishingPhase.BiteNibble)]
        [TestCase(BitePhase.TakeOpen, FishingPhase.Bite)]
        [TestCase(BitePhase.Away, FishingPhase.Waiting)]
        [TestCase(BitePhase.Hooked, FishingPhase.Waiting)]
        [TestCase(BitePhase.Gone, FishingPhase.Waiting)]
        public void BiteWireMapping_PinsEveryCurrentPhase(BitePhase phase, FishingPhase expected)
            => Assert.AreEqual(expected, BitePhases.ToFishingPhase(phase));

        [TestCase(0f)]
        [TestCase(-1f)]
        public void Bite_QuietTicksWithNonPositiveTime_DoNotAdvance(float dt)
        {
            var sequence = new BiteSequenceSim(Bite(), new System.Random(42));
            sequence.Tick(dt, false);
            Assert.AreEqual(BitePhase.Approach, sequence.Phase);
            Assert.AreEqual(0f, sequence.PhaseSeconds);
            Assert.AreEqual(0, sequence.PassIndex);
            Assert.AreEqual(BiteMiss.None, sequence.LastMiss);
        }

        [TestCase(0f)]
        [TestCase(-1f)]
        public void Bite_StrikeIsJudgedEvenWithNonPositiveTime(float dt)
        {
            var sequence = new BiteSequenceSim(Bite(), new System.Random(42));
            sequence.Tick(0.25f, false);
            Assert.AreEqual(BitePhase.TakeOpen, sequence.Phase);
            sequence.Tick(dt, true);
            Assert.AreEqual(BitePhase.Hooked, sequence.Phase);
            Assert.IsTrue(sequence.IsOver);
            Assert.AreEqual(BiteMiss.None, sequence.LastMiss);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void Bite_TerminalOutcomeAndFeedback_SurviveFurtherTicks(bool hook)
        {
            var sequence = new BiteSequenceSim(Bite(), new System.Random(42));
            sequence.Tick(0.25f, false);
            sequence.Tick(0.5f, hook);
            BitePhase phase = hook ? BitePhase.Hooked : BitePhase.Gone;
            BiteMiss miss = hook ? BiteMiss.None : BiteMiss.Late;
            int pass = sequence.PassIndex;
            float seconds = sequence.PhaseSeconds;
            sequence.Tick(100f, true);
            sequence.Tick(100f, false);
            Assert.AreEqual(phase, sequence.Phase);
            Assert.AreEqual(miss, sequence.LastMiss);
            Assert.AreEqual(pass, sequence.PassIndex);
            Assert.AreEqual(seconds, sequence.PhaseSeconds);
            Assert.IsTrue(sequence.IsOver);
            Assert.AreEqual(0f, sequence.TakeWindow01);
        }

        [TestCase(0f)]
        [TestCase(-1f)]
        public void Fight_NonPositiveTime_DoesNotReelOrStartASurge(float dt)
        {
            var tuning = new FishFightTuning(1f, 1f, 1f, 0.1f, 1f, 0.1f, true);
            var fight = new FishFight(tuning, new System.Random(42));
            fight.Tick(dt, true);
            Assert.AreEqual(0f, fight.Tension01);
            Assert.AreEqual(0f, fight.Landing01);
            Assert.AreEqual(FishFightResult.None, fight.Result);
            Assert.IsFalse(fight.IsSurging);
        }

        [TestCase(true, FishFightResult.Snapped)]
        [TestCase(false, FishFightResult.Landed)]
        public void Fight_SimultaneousFullMeters_GiveSnapPriorityWhenEnabled(bool snapping, FishFightResult expected)
        {
            var tuning = new FishFightTuning(1f, 1f, 1f, 0f, 0f, 0f, snapping);
            var fight = new FishFight(tuning, new System.Random(42));
            fight.Tick(1f, true);
            Assert.AreEqual(1f, fight.Tension01);
            Assert.AreEqual(1f, fight.Landing01);
            Assert.AreEqual(expected, fight.Result);
            Assert.IsTrue(fight.IsOver);
            fight.Tick(10f, false);
            fight.Tick(10f, true);
            Assert.AreEqual(expected, fight.Result);
            Assert.AreEqual(1f, fight.Tension01);
            Assert.AreEqual(1f, fight.Landing01);
        }
    }
}
