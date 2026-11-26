using System;
using HiddenHarbours.Core;
using NUnit.Framework;

namespace HiddenHarbours.Tests.EditMode
{
    public partial class SleepFlowTests
    {
        private const string OtherBunk = "fixture.hull_instance.wheelhouse_bench";

        private static RestParticipant[] SeparateBunks() => new[]
        {
            new RestParticipant(0, Bed, Region),
            new RestParticipant(1, OtherBunk, "region.fixture_sea")
        };

        private static RestDecision Decide(params RestParticipant[] players)
        {
            IRestQuorum rule = new RestQuorumRule();
            return rule.Evaluate(players);
        }

        [Test]
        public void OnePlayerInABunk_MayPassTheNight()
        {
            RestDecision decision = Decide(new RestParticipant(0, Bed, Region));
            Assert.IsTrue(decision.MayPassNight);
            Assert.AreEqual(string.Empty, decision.ReasonKey);
        }

        [Test]
        public void TwoPlayersInSeparateBunks_MayPassTheNight()
        {
            RestDecision decision = Decide(new RestParticipant(0, Bed, Region), new RestParticipant(1, OtherBunk, Region));
            Assert.IsTrue(decision.MayPassNight);
            Assert.AreEqual(string.Empty, decision.ReasonKey);
        }

        [Test]
        public void AWheelhouseBenchAndABedAshore_InDifferentRegions_AreSeparateBunks()
        {
            RestParticipant[] players = SeparateBunks();
            Assert.AreNotEqual(players[0].RegionId, players[1].RegionId);
            RestDecision decision = Decide(players);
            Assert.IsTrue(decision.MayPassNight);
            Assert.AreEqual(string.Empty, decision.ReasonKey);
        }

        [Test]
        public void TwoPlayersClaimingTheSamePhysicalBunk_AreRefused()
        {
            RestDecision decision = Decide(new RestParticipant(0, Bed, Region), new RestParticipant(1, Bed, Region));
            Assert.IsFalse(decision.MayPassNight);
            Assert.AreEqual(RestQuorumRule.SharedBunk, decision.ReasonKey);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase(" ")]
        public void OneLyingAndOneStanding_ReturnsThePlainReason(string absentBunk)
        {
            RestDecision decision = Decide(new RestParticipant(0, Bed), new RestParticipant(1, absentBunk));
            Assert.IsFalse(decision.MayPassNight);
            Assert.AreEqual("sleep.player_not_in_bunk", decision.ReasonKey);
            Assert.AreEqual(0, _saved.Count, "a rule does not publish a banner");
            Assert.AreEqual(0, _changes.Count);
        }

        [Test]
        public void NoPlayers_IsNotAVacuouslyReadyNight()
        {
            Assert.AreEqual(RestQuorumRule.NoPlayers, Decide().ReasonKey);
            Assert.IsFalse(Decide().MayPassNight);
            Assert.AreEqual(RestQuorumRule.NoPlayers, Decide(null).ReasonKey);
            Assert.IsFalse(Decide(null).MayPassNight);
        }

        [Test]
        public void CountingTheSamePlayerTwice_DoesNotMakeAQuorum()
        {
            RestDecision decision = Decide(new RestParticipant(0, Bed), new RestParticipant(0, OtherBunk));
            Assert.IsFalse(decision.MayPassNight);
            Assert.AreEqual(RestQuorumRule.RepeatedPlayer, decision.ReasonKey);
        }

        [Test]
        public void EveryParticipantIsChecked_NotJustTheFirstTwo()
        {
            RestDecision decision = Decide(new RestParticipant(0, Bed), new RestParticipant(1, OtherBunk),
                new RestParticipant(2, null));
            Assert.IsFalse(decision.MayPassNight);
            Assert.AreEqual(RestQuorumRule.PlayerNotInBunk, decision.ReasonKey);
        }

        [Test]
        public void TheRefusalAndInput_AreStableAcrossRosterOrderAndRepeatedReads()
        {
            var players = new[] { new RestParticipant(0, Bed), new RestParticipant(1, Bed), new RestParticipant(2, null) };
            var reversed = new[] { players[2], players[1], players[0] };
            Assert.AreEqual(RestQuorumRule.PlayerNotInBunk, Decide(players).ReasonKey);
            Assert.AreEqual(Decide(players).ReasonKey, Decide(reversed).ReasonKey);
            Assert.AreEqual(Decide(players).ReasonKey, Decide(players).ReasonKey);
            Assert.AreEqual(Bed, players[0].BunkId);
            Assert.AreEqual(Bed, players[1].BunkId);
            Assert.IsNull(players[2].BunkId);
        }

        [Test]
        public void ASessionSnapshot_IsDetachedFromItsOccupancySource()
        {
            RestParticipant[] players = SeparateBunks();
            RestQuorum.ParticipantSource = () => players;
            RestParticipant[] snapshot = RestQuorum.SnapshotFor(Bed);
            players[1] = new RestParticipant(1, null);
            Assert.AreEqual(OtherBunk, snapshot[1].BunkId);
            Assert.IsTrue(Decide(snapshot).MayPassNight);
            Assert.IsFalse(RestQuorum.EvaluateForRest(Bed).MayPassNight);
        }

        [Test]
        public void AConfiguredSourceReturningNoRoster_DoesNotFallBackToOnePlayer()
        {
            RestQuorum.ParticipantSource = () => null;
            RestDecision decision = RestQuorum.EvaluateForRest(Bed);
            Assert.IsFalse(decision.MayPassNight);
            Assert.AreEqual(RestQuorumRule.NoPlayers, decision.ReasonKey);
            RestQuorum.Reset();
            Assert.IsNull(RestQuorum.ParticipantSource);
            Assert.IsTrue(RestQuorum.EvaluateForRest(Bed).MayPassNight);
        }

        [Test]
        public void AReleasedLocalBunk_IsRecheckedAtConfirmation()
        {
            Rig rig = World(22d);
            Assert.IsTrue(SleepFlow.Begin(Request()));
            RestQuorum.Release(Bed, RestQuorum.LocalSleeper);
            SleepFlow.Confirm();
            Assert.AreEqual(SleepPhase.Choosing, SleepFlow.Phase);
            Assert.AreEqual(RestQuorumRule.PlayerNotInBunk, SleepFlow.LastReadiness.ReasonKey);
            Assert.AreEqual(At(0, 22d), rig.Clock.TotalSeconds);
            Assert.AreEqual(0, rig.Save.Saves);
            Assert.AreEqual(0, _saved.Count);
        }

        [Test]
        public void APartnerWhoLeavesBeforeConfirmation_PreventsTheNight()
        {
            Rig rig = World(22d);
            RestParticipant[] players = SeparateBunks();
            RestQuorum.ParticipantSource = () => players;
            Assert.IsTrue(SleepFlow.Begin(Request()));
            players[1] = new RestParticipant(1, null);
            SleepFlow.Confirm();
            Assert.AreEqual(SleepPhase.Choosing, SleepFlow.Phase);
            Assert.AreEqual(RestQuorumRule.PlayerNotInBunk, SleepFlow.LastReadiness.ReasonKey);
            Assert.AreEqual(At(0, 22d), rig.Clock.TotalSeconds);
            Assert.AreEqual(0, rig.Save.Saves);
            Assert.AreEqual(0, _saved.Count);
        }

        [Test]
        public void APartnerWhoLeavesAsTheNightBegins_PreventsEvenTheFirstClockStep()
        {
            Rig rig = World(22d);
            RestParticipant[] players = SeparateBunks();
            RestQuorum.ParticipantSource = () => players;
            Action<SleepFlowChanged> leave = change =>
            {
                if (change.Phase == SleepPhase.Asleep) players[1] = new RestParticipant(1, null);
            };
            EventBus.Subscribe(leave);
            try
            {
                Assert.IsTrue(SleepFlow.Begin(Request()));
                SleepFlow.Confirm();
                Assert.AreEqual(SleepPhase.Asleep, SleepFlow.Phase);
                SleepFlow.Tick(1f);
                Assert.AreEqual(SleepPhase.Awake, SleepFlow.Phase);
                Assert.AreEqual(RestQuorumRule.PlayerNotInBunk, SleepFlow.LastReadiness.ReasonKey);
                Assert.AreEqual(At(0, 22d), rig.Clock.TotalSeconds);
                Assert.AreEqual(0, rig.Save.Saves);
                Assert.AreEqual(0, _saved.Count);
                Assert.IsFalse(RestQuorum.IsOccupied(Bed));
            }
            finally { EventBus.Unsubscribe(leave); }
        }

        [Test]
        public void APartnerLeavingAfterSomeSleep_StopsThereAndKeepsOneEarlyWake()
        {
            Rig rig = World(22d);
            RestParticipant[] players = SeparateBunks();
            RestQuorum.ParticipantSource = () => players;
            SleepFlow.Begin(Request());
            SleepFlow.Confirm();
            SleepFlow.Tick(1f);
            double stoppedAt = rig.Clock.TotalSeconds;
            Assert.AreEqual(At(0, 23d), stoppedAt, 1e-6);
            players[1] = new RestParticipant(1, null);
            SleepFlow.Tick(1f);
            Assert.AreEqual(stoppedAt, rig.Clock.TotalSeconds);
            Assert.AreEqual(SleepPhase.Awake, SleepFlow.Phase);
            Assert.AreEqual(RestQuorumRule.PlayerNotInBunk, SleepFlow.LastReadiness.ReasonKey);
            Assert.AreEqual(1, rig.Save.Saves);
            Assert.AreEqual(SleepFlow.WokeEarlyText, _saved[0].Reason);
        }

        [Test]
        public void TwoSeparateBunks_StillUseTheExistingEightHourCapAndOneCheckpoint()
        {
            Rig rig = World(12d);
            _config.SleepMaxHours = 20f;
            RestQuorum.ParticipantSource = SeparateBunks;
            Assert.IsTrue(SleepFlow.Begin(Request()));
            SleepFlow.Adjust(1);
            Assert.AreEqual(8f, SleepFlow.Hours);
            SleepFlow.Confirm();
            Assert.IsTrue(SleepFlow.LastReadiness.MayPassNight);
            SleepThrough(0.25f);
            Assert.AreEqual(At(0, 20d), rig.Clock.TotalSeconds, 1e-6);
            Assert.AreEqual(1, rig.Save.Saves);
            Assert.AreEqual(1, _saved.Count);
        }

        [Test]
        public void TheInstantFallback_CannotSleepOrSavePastAStandingPartner()
        {
            Rig rig = World(22d);
            RestQuorum.ParticipantSource = () => new[] { new RestParticipant(0, Bed), new RestParticipant(1, null) };
            GameSaved outcome = RestSaveResponder.Fulfil(Request());
            Assert.IsFalse(outcome.Ok);
            Assert.AreEqual(RestQuorumRule.PlayerNotInBunk, outcome.Reason);
            Assert.AreEqual(At(0, 22d), rig.Clock.TotalSeconds);
            Assert.AreEqual(0, rig.Save.Saves);
            Assert.AreEqual(0, _saved.Count, "a refusal key is not a new HUD event");
        }

        [Test]
        public void TheInstantFallback_RechecksAfterTheCoverStarts()
        {
            Rig rig = World(22d);
            RestParticipant[] players = SeparateBunks();
            RestQuorum.ParticipantSource = () => players;
            int covers = 0;
            Action<RegionTravelCover> leave = cover =>
            {
                covers++;
                players[1] = new RestParticipant(1, null);
            };
            EventBus.Subscribe(leave);
            try
            {
                GameSaved outcome = RestSaveResponder.Fulfil(Request());
                Assert.IsFalse(outcome.Ok);
                Assert.AreEqual(RestQuorumRule.PlayerNotInBunk, outcome.Reason);
                Assert.AreEqual(At(0, 22d), rig.Clock.TotalSeconds);
                Assert.AreEqual(0, rig.Save.Saves);
                Assert.AreEqual(0, _saved.Count);
                Assert.AreEqual(2, covers, "the cover is released even when the second decision refuses");
            }
            finally { EventBus.Unsubscribe(leave); }
        }

        [Test]
        public void TheInstantFallback_WithTwoSeparateBunks_UsesTheSameCap()
        {
            Rig rig = World(12d);
            _config.SleepMaxHours = 20f;
            RestQuorum.ParticipantSource = SeparateBunks;
            RestSaveResponder.Fulfil(Request());
            Assert.AreEqual(At(0, 20d), rig.Clock.TotalSeconds, 1e-6);
            Assert.AreEqual(1, rig.Save.Saves);
        }
    }
}
