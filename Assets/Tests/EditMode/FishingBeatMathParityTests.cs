using System;
using NUnit.Framework;
using HiddenHarbours.Core;
using HiddenHarbours.Player;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>One phase-to-clip mapping</b> (ADR 0053 §11): <see cref="FishingBeatMath"/> moved down to Core, and
    /// Player's <see cref="PlayerFishingAnimMath"/> keeps forwarders. The forwarders answer exactly what Core
    /// answers for every phase, moving or still, across the strike boundary; the pose values the rod tables
    /// index by are pinned; and the state-key table is pinned.
    /// </summary>
    public sealed class FishingBeatMathParityTests
    {
        private static readonly float[] FightSeconds = { -1f, 0f, 0.1f, 0.299f, 0.3f, 0.31f, 2f, 60f };
        private const float StrikeSeconds = 0.3f;

        [Test]
        public void TheForwarders_AnswerWhatCoreAnswers_ForEveryPhaseAndEveryBeat()
        {
            foreach (FishingPhase phase in (FishingPhase[])Enum.GetValues(typeof(FishingPhase)))
            foreach (float t in FightSeconds)
            foreach (bool still in new[] { true, false })
            {
                FishingPose core = FishingBeatMath.PoseFor(phase, t, StrikeSeconds, still);
                Assert.AreEqual(core, PlayerFishingAnimMath.PoseFor(phase, t, StrikeSeconds, still),
                                $"{phase} at {t}s, stationary {still}");
                if (still)
                    Assert.AreEqual(core, PlayerFishingAnimMath.PoseFor(phase, t, StrikeSeconds),
                                    $"{phase} at {t}s: the three-argument overload is the stationary one");
            }
            foreach (FishingPose pose in (FishingPose[])Enum.GetValues(typeof(FishingPose)))
                Assert.AreEqual(FishingBeatMath.StateKeyFor(pose), PlayerFishingAnimMath.StateKeyFor(pose), pose.ToString());
        }

        [Test]
        public void ThePoseValues_AreTheOnesTheRodTablesIndexBy()
        {
            Assert.AreEqual(0, (int)FishingPose.None);
            Assert.AreEqual(1, (int)FishingPose.Bite);
            Assert.AreEqual(2, (int)FishingPose.Strike);
            Assert.AreEqual(3, (int)FishingPose.Reel);
            Assert.AreEqual(4, (int)FishingPose.Land);
            Assert.AreEqual(5, (int)FishingPose.Hold);
            Assert.AreEqual(6, (int)FishingPose.CastBack);
            Assert.AreEqual(7, (int)FishingPose.CastRelease);
            Assert.AreEqual(8, Enum.GetValues(typeof(FishingPose)).Length, "a pose was added: pin it here");
        }

        [Test]
        public void TheStateKeyTable_IsPinned()
        {
            Assert.IsNull(FishingBeatMath.StateKeyFor(FishingPose.None));
            Assert.AreEqual("bite", FishingBeatMath.StateKeyFor(FishingPose.Bite));
            Assert.AreEqual("strike", FishingBeatMath.StateKeyFor(FishingPose.Strike));
            Assert.AreEqual("reel", FishingBeatMath.StateKeyFor(FishingPose.Reel));
            Assert.AreEqual("land", FishingBeatMath.StateKeyFor(FishingPose.Land));
            Assert.AreEqual("hold", FishingBeatMath.StateKeyFor(FishingPose.Hold));
            Assert.AreEqual("castBack", FishingBeatMath.StateKeyFor(FishingPose.CastBack));
            Assert.AreEqual("castRelease", FishingBeatMath.StateKeyFor(FishingPose.CastRelease));
        }

        [Test]
        public void AFight_OpensWithTheStrike_ThenReels_AndAMovingAnglerHoldsNoRodPose()
        {
            Assert.AreEqual(FishingPose.Strike, FishingBeatMath.PoseFor(FishingPhase.FightDeep, 0.29f, StrikeSeconds, true));
            Assert.AreEqual(FishingPose.Reel, FishingBeatMath.PoseFor(FishingPhase.FightDeep, 0.3f, StrikeSeconds, true));
            Assert.AreEqual(FishingPose.Reel, FishingBeatMath.PoseFor(FishingPhase.FightSurface, 5f, StrikeSeconds, true));
            Assert.AreEqual(FishingPose.Hold, FishingBeatMath.PoseFor(FishingPhase.Waiting, 0f, StrikeSeconds, true));
            Assert.AreEqual(FishingPose.None, FishingBeatMath.PoseFor(FishingPhase.Waiting, 0f, StrikeSeconds, false));
            Assert.AreEqual(FishingPose.None, FishingBeatMath.PoseFor(FishingPhase.NoBite, 0f, StrikeSeconds, true));
            Assert.AreEqual(FishingPose.Reel, FishingBeatMath.PoseFor(FishingPhase.Fighting, 1f, -1f, true),
                            "a negative strike is no strike");
        }
    }
}
