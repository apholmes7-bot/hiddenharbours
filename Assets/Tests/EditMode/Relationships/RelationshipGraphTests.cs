using System;
using System.Collections.Generic;
using HiddenHarbours.Core;
using NUnit.Framework;
using UnityEngine;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// Relationships slice 1: the graph as arithmetic (seeding, symmetry, determinism, caps, floors, stage
    /// crossings fired once, unknown ids, no romance) and the service over injected data (seed order,
    /// EventBus announcements). Every rule and tie here is made by the test, so a retune of the shipped
    /// config can never break these.
    /// </summary>
    public class RelationshipGraphTests
    {
        private const string Ann = "npc.ann";
        private const string Bob = "npc.bob";
        private const string Cy = "npc.cy";
        private const string Dee = "npc.dee";

        private static RelationshipStageThresholds Edges => new RelationshipStageThresholds
        {
            MinStrength = -100, MaxStrength = 100, RivalAtOrBelow = -40, StrainedAtOrBelow = -15,
            AcquaintanceFrom = 10, FriendFrom = 35, CloseFrom = 70,
        };

        private static RelationshipRule[] Rules => new[]
        {
            new RelationshipRule(RelationshipEventKind.WorkedTogether, 2, 2, false),
            new RelationshipRule(RelationshipEventKind.SharedEmployerRestored, 15, 15, false),
            new RelationshipRule(RelationshipEventKind.CustomGiven, 1, 5, false),
            new RelationshipRule(RelationshipEventKind.CustomDiverted, -1, 3, false),
            new RelationshipRule(RelationshipEventKind.Introduced, 12, 12, false),
        };

        private static RelationshipGraph Graph(params RelationshipSeed[] seeds) => new RelationshipGraph(Edges, Rules, seeds);

        private readonly List<RelationshipStageChanged> _heard = new List<RelationshipStageChanged>();
        private void Hear(RelationshipStageChanged e) => _heard.Add(e);
        private readonly List<UnityEngine.Object> _made = new List<UnityEngine.Object>();

        [SetUp]
        public void SetUp()
        {
            GameServices.Reset();
            _heard.Clear();
            EventBus.Subscribe<RelationshipStageChanged>(Hear);
        }

        [TearDown]
        public void TearDown()
        {
            EventBus.Unsubscribe<RelationshipStageChanged>(Hear);
            GameServices.Reset();
            foreach (UnityEngine.Object o in _made) UnityEngine.Object.DestroyImmediate(o);
            _made.Clear();
        }

        // ---- seeding and symmetry ---------------------------------------------------------------

        [Test]
        public void Seeds_SetStartingStrength_AndEveryoneElseIsAStranger()
        {
            RelationshipGraph g = Graph(new RelationshipSeed(Ann, Bob, 40), new RelationshipSeed(Cy, Ann, -20));
            Assert.AreEqual(40, g.Strength(Ann, Bob));
            Assert.AreEqual(RelationshipStage.Friend, g.Stage(Ann, Bob));
            Assert.AreEqual(RelationshipStage.Strained, g.Stage(Ann, Cy));
            Assert.AreEqual(RelationshipStage.Stranger, g.Stage(Bob, Cy), "no seed, no history: strangers");
            Assert.AreEqual(RelationshipStage.Stranger, g.Stage(Dee, Ann), "a villager with no ties is a stranger to all");
        }

        [Test]
        public void Seeds_AreClamped_AndARepeatedPairKeepsTheFirst()
        {
            RelationshipGraph g = Graph(new RelationshipSeed(Ann, Bob, 500), new RelationshipSeed(Bob, Ann, 5),
                new RelationshipSeed(Cy, Dee, -500));
            Assert.AreEqual(100, g.Strength(Ann, Bob));
            Assert.AreEqual(-100, g.Strength(Cy, Dee));
            Assert.AreEqual(2, g.Snapshot().Count);
        }

        [Test]
        public void APair_IsSymmetric_ForSeedsEventsAndStages()
        {
            RelationshipGraph g = Graph(new RelationshipSeed(Bob, Ann, 8));
            Assert.AreEqual(g.Strength(Ann, Bob), g.Strength(Bob, Ann));
            g.Record(new RelationshipEvent(RelationshipEventKind.WorkedTogether, Ann, Bob));
            g.Record(new RelationshipEvent(RelationshipEventKind.WorkedTogether, Cy, Dee));
            g.ApplyDay();
            Assert.AreEqual(10, g.Strength(Bob, Ann));
            Assert.AreEqual(g.Stage(Ann, Bob), g.Stage(Bob, Ann));
            Assert.AreEqual(g.Strength(Dee, Cy), g.Strength(Cy, Dee));
            List<RelationshipPairState> snap = g.Snapshot();
            Assert.AreEqual(Ann, snap[0].A, "a pair is stored once, ids in ordinal order");
            Assert.AreEqual(Bob, snap[0].B);
        }

        // ---- determinism ------------------------------------------------------------------------

        private static RelationshipEvent[][] Season => new[]
        {
            new[]
            {
                new RelationshipEvent(RelationshipEventKind.WorkedTogether, Ann, Bob),
                new RelationshipEvent(RelationshipEventKind.CustomGiven, RelationshipGraph.PlayerId, Cy, 3),
                new RelationshipEvent(RelationshipEventKind.CustomDiverted, Cy, Dee, 3),
            },
            new[]
            {
                new RelationshipEvent(RelationshipEventKind.Introduced, Bob, Dee),
                new RelationshipEvent(RelationshipEventKind.WorkedTogether, Bob, Ann),
                new RelationshipEvent(RelationshipEventKind.SharedEmployerRestored, Ann, Cy),
            },
        };

        private static List<RelationshipPairState> Play(RelationshipEvent[][] days, bool reverseWithinDay)
        {
            RelationshipGraph g = Graph(new RelationshipSeed(Ann, Bob, 30), new RelationshipSeed(Cy, Dee, -10));
            foreach (RelationshipEvent[] day in days)
            {
                for (int i = 0; i < day.Length; i++) g.Record(day[reverseWithinDay ? day.Length - 1 - i : i]);
                g.ApplyDay();
            }
            return g.Snapshot();
        }

        [Test]
        public void SameSeedsAndEvents_GiveTheSameGraph()
        {
            List<RelationshipPairState> first = Play(Season, false);
            List<RelationshipPairState> second = Play(Season, false);
            AssertSame(first, second);
        }

        [Test]
        public void OrderOfRecording_WithinADay_DoesNotMatter()
        {
            AssertSame(Play(Season, false), Play(Season, true));
        }

        private static void AssertSame(List<RelationshipPairState> x, List<RelationshipPairState> y)
        {
            Assert.AreEqual(x.Count, y.Count);
            for (int i = 0; i < x.Count; i++)
            {
                Assert.AreEqual(x[i].A, y[i].A);
                Assert.AreEqual(x[i].B, y[i].B);
                Assert.AreEqual(x[i].Strength, y[i].Strength, x[i].A + "/" + x[i].B);
            }
        }

        // ---- rules: caps, floors, the queue -----------------------------------------------------

        [Test]
        public void EventsMoveNothing_UntilTheDayIsApplied()
        {
            RelationshipGraph g = Graph();
            Assert.IsTrue(g.Record(new RelationshipEvent(RelationshipEventKind.Introduced, Ann, Bob)));
            Assert.AreEqual(1, g.PendingCount);
            Assert.AreEqual(0, g.Strength(Ann, Bob));
            g.ApplyDay();
            Assert.AreEqual(12, g.Strength(Ann, Bob));
            Assert.AreEqual(0, g.PendingCount);
            Assert.IsEmpty(g.ApplyDay(), "an empty day changes nothing");
            Assert.AreEqual(12, g.Strength(Ann, Bob));
        }

        [Test]
        public void TheDailyCap_HoldsPerPairPerKind()
        {
            RelationshipGraph g = Graph();
            g.Record(new RelationshipEvent(RelationshipEventKind.CustomGiven, RelationshipGraph.PlayerId, Cy, 40));
            g.Record(new RelationshipEvent(RelationshipEventKind.CustomGiven, Cy, RelationshipGraph.PlayerId, 40));
            g.Record(new RelationshipEvent(RelationshipEventKind.WorkedTogether, Ann, Bob, 30));
            g.ApplyDay();
            Assert.AreEqual(5, g.Strength(RelationshipGraph.PlayerId, Cy), "80 purchases in a day still move 5");
            Assert.AreEqual(2, g.Strength(Ann, Bob));
        }

        [Test]
        public void ACoolingRule_ThatMayNotMakeRivals_StopsAboveTheRivalEdge()
        {
            RelationshipGraph g = Graph(new RelationshipSeed(Cy, Dee, -38));
            for (int day = 0; day < 10; day++)
            {
                g.Record(new RelationshipEvent(RelationshipEventKind.CustomDiverted, Cy, Dee, 3));
                g.ApplyDay();
            }
            Assert.AreEqual(-39, g.Strength(Cy, Dee));
            Assert.AreEqual(RelationshipStage.Strained, g.Stage(Cy, Dee));
        }

        [Test]
        public void ACoolingRule_ThatMayMakeRivals_ReachesRivalButNotPastTheBound()
        {
            var rules = new[] { new RelationshipRule(RelationshipEventKind.CustomDiverted, -10, 10, true) };
            var g = new RelationshipGraph(Edges, rules, new[] { new RelationshipSeed(Cy, Dee, -35) });
            for (int day = 0; day < 20; day++)
            {
                g.Record(new RelationshipEvent(RelationshipEventKind.CustomDiverted, Cy, Dee));
                g.ApplyDay();
            }
            Assert.AreEqual(-100, g.Strength(Cy, Dee));
            Assert.AreEqual(RelationshipStage.Rival, g.Stage(Cy, Dee));
        }

        [Test]
        public void ACoolingRule_NeverLiftsAPairAlreadyRivals()
        {
            RelationshipGraph g = Graph(new RelationshipSeed(Cy, Dee, -60));
            g.Record(new RelationshipEvent(RelationshipEventKind.CustomDiverted, Cy, Dee));
            g.ApplyDay();
            Assert.AreEqual(-60, g.Strength(Cy, Dee));
        }

        [Test]
        public void AKindWithNoRule_IsRecordedButMovesNothing()
        {
            var g = new RelationshipGraph(Edges, new RelationshipRule[0]);
            Assert.IsTrue(g.Record(new RelationshipEvent(RelationshipEventKind.Introduced, Ann, Bob)));
            Assert.IsEmpty(g.ApplyDay());
            Assert.AreEqual(0, g.Strength(Ann, Bob));
        }

        [Test]
        public void TheOwnersThreeExamples_AreExpressible()
        {
            RelationshipGraph g = Graph();
            // Two hires on one boat, a season of days together.
            for (int day = 0; day < 20; day++)
            {
                g.Record(new RelationshipEvent(RelationshipEventKind.WorkedTogether, Ann, Bob));
                g.ApplyDay();
            }
            Assert.AreEqual(RelationshipStage.Friend, g.Stage(Ann, Bob));

            // The cannery restored: its two hands grow closer.
            g.Record(new RelationshipEvent(RelationshipEventKind.SharedEmployerRestored, Cy, Dee));
            g.ApplyDay();
            Assert.AreEqual(RelationshipStage.Acquaintance, g.Stage(Cy, Dee));

            // Buying mostly from Cy over Dee: the player's custom with Cy grows, Dee cools toward Cy.
            for (int day = 0; day < 10; day++)
            {
                g.Record(new RelationshipEvent(RelationshipEventKind.CustomGiven, RelationshipGraph.PlayerId, Cy, 4));
                g.Record(new RelationshipEvent(RelationshipEventKind.CustomDiverted, Cy, Dee, 4));
                g.ApplyDay();
            }
            Assert.Greater(g.Strength(RelationshipGraph.PlayerId, Cy), g.Strength(RelationshipGraph.PlayerId, Dee),
                "the favoured merchant has more custom than the one passed over (the later 'less busy' read)");
            Assert.Less(g.Strength(Cy, Dee), 15, "the passed-over merchant cooled");
        }

        // ---- stage crossings --------------------------------------------------------------------

        [Test]
        public void ACrossing_IsReportedOnce_AndStayingInAStageReportsNothing()
        {
            RelationshipGraph g = Graph(new RelationshipSeed(Ann, Bob, 8));
            g.Record(new RelationshipEvent(RelationshipEventKind.WorkedTogether, Ann, Bob));
            List<RelationshipStageChanged> first = g.ApplyDay();
            Assert.AreEqual(1, first.Count);
            Assert.AreEqual(RelationshipStage.Stranger, first[0].From);
            Assert.AreEqual(RelationshipStage.Acquaintance, first[0].To);

            g.Record(new RelationshipEvent(RelationshipEventKind.WorkedTogether, Ann, Bob));
            Assert.IsEmpty(g.ApplyDay(), "10 → 12 is still Acquaintance: no second report");
        }

        [Test]
        public void ABigDay_ReportsOneCrossing_FromTheOldStageToTheNew()
        {
            RelationshipGraph g = Graph(new RelationshipSeed(Ann, Bob, 50));
            g.Record(new RelationshipEvent(RelationshipEventKind.SharedEmployerRestored, Ann, Bob));
            g.Record(new RelationshipEvent(RelationshipEventKind.Introduced, Ann, Bob));
            g.Record(new RelationshipEvent(RelationshipEventKind.WorkedTogether, Ann, Bob));
            List<RelationshipStageChanged> changes = g.ApplyDay();
            Assert.AreEqual(1, changes.Count, "one pair, one report, however many kinds moved it");
            Assert.AreEqual(RelationshipStage.Friend, changes[0].From);
            Assert.AreEqual(RelationshipStage.Close, changes[0].To);
        }

        [Test]
        public void TheService_PublishesEachCrossingOnTheEventBus_Once()
        {
            RelationshipTieDef tie = Tie("tie.ann__bob", Ann, Bob, 8);
            var service = new RelationshipService(() => new[] { tie }, () => Config());
            service.Record(new RelationshipEvent(RelationshipEventKind.WorkedTogether, Bob, Ann));
            Assert.AreEqual(1, service.ApplyDay());
            Assert.AreEqual(1, _heard.Count);
            Assert.AreEqual(Ann, _heard[0].A);
            Assert.AreEqual(RelationshipStage.Acquaintance, _heard[0].To);

            service.Record(new RelationshipEvent(RelationshipEventKind.WorkedTogether, Bob, Ann));
            Assert.AreEqual(0, service.ApplyDay());
            Assert.AreEqual(1, _heard.Count);
        }

        [Test]
        public void TheService_SeedsInIdOrder_WhateverOrderTheTiesLoad()
        {
            RelationshipTieDef first = Tie("tie.ann__bob", Ann, Bob, 40);
            RelationshipTieDef dupe = Tie("tie.zz_duplicate", Bob, Ann, -40);
            var a = new RelationshipService(() => new[] { first, dupe }, () => Config());
            var b = new RelationshipService(() => new[] { dupe, first }, () => Config());
            Assert.AreEqual(RelationshipStage.Friend, a.Stage(Ann, Bob));
            Assert.AreEqual(a.Stage(Ann, Bob), b.Stage(Bob, Ann));
        }

        [Test]
        public void TheService_WithNoDataAtAll_AnswersStranger()
        {
            var service = new RelationshipService(() => null, () => null);
            Assert.AreEqual(RelationshipStage.Stranger, service.Stage(Ann, Bob));
            Assert.IsTrue(service.Record(new RelationshipEvent(RelationshipEventKind.Introduced, Ann, Bob)));
            Assert.AreEqual(0, service.ApplyDay(), "no config: no rules, nothing moves");
        }

        [Test]
        public void GameServices_HandsOutADefault_AndResetDropsIt()
        {
            IRelationshipService first = GameServices.Relationships;
            Assert.IsNotNull(first);
            Assert.AreSame(first, GameServices.Relationships);
            GameServices.Reset();
            Assert.AreNotSame(first, GameServices.Relationships);
        }

        // ---- safety -----------------------------------------------------------------------------

        [Test]
        public void UnknownAndBadIds_AreSafe()
        {
            RelationshipGraph g = Graph(new RelationshipSeed(null, Bob, 50), new RelationshipSeed(Ann, Ann, 50),
                new RelationshipSeed("", Bob, 50));
            Assert.IsEmpty(g.Snapshot(), "a seed with a missing or repeated id is skipped");
            Assert.AreEqual(RelationshipStage.Stranger, g.Stage(null, null));
            Assert.AreEqual(RelationshipStage.Stranger, g.Stage(Ann, Ann));
            Assert.AreEqual(RelationshipStage.Stranger, g.Stage("npc.nobody", "npc.never_shipped"));
            Assert.IsFalse(g.Record(new RelationshipEvent(RelationshipEventKind.Introduced, null, Bob)));
            Assert.IsFalse(g.Record(new RelationshipEvent(RelationshipEventKind.Introduced, Ann, Ann)));
            Assert.IsFalse(g.Record(new RelationshipEvent(RelationshipEventKind.Introduced, Ann, Bob, 0)));
            Assert.IsFalse(g.Record(new RelationshipEvent(RelationshipEventKind.Introduced, Ann, Bob, -3)));
            Assert.IsTrue(g.Record(new RelationshipEvent((RelationshipEventKind)999, Ann, Bob)),
                "a kind from a newer build is queued");
            Assert.DoesNotThrow(() => g.ApplyDay(), "and skipped at apply, never a crash");
            Assert.AreEqual(0, g.Strength(Ann, Bob));
        }

        [Test]
        public void InvalidThresholds_FallBackToTheDefaults()
        {
            var bad = new RelationshipStageThresholds { MinStrength = 0, MaxStrength = 0 };
            Assert.IsFalse(bad.IsValid);
            var g = new RelationshipGraph(bad, Rules, new[] { new RelationshipSeed(Ann, Bob, 40) });
            Assert.AreEqual(RelationshipStageThresholds.Default.FriendFrom, g.Thresholds.FriendFrom);
            Assert.AreEqual(RelationshipStage.Friend, g.Stage(Ann, Bob));
            Assert.IsTrue(RelationshipStageThresholds.Default.IsValid);
        }

        [Test]
        public void SnapshotAndRestore_RoundTrip_TheSliceTwoSeam()
        {
            List<RelationshipPairState> played = Play(Season, false);
            RelationshipGraph fresh = Graph();
            fresh.Restore(played);
            AssertSame(played, fresh.Snapshot());
        }

        [Test]
        public void NoRomanceStage_Exists()
        {
            string[] forbidden = { "romance", "romantic", "love", "lover", "dating", "date", "partner", "spouse",
                "married", "sweetheart", "courting", "crush" };
            foreach (string name in Enum.GetNames(typeof(RelationshipStage)))
                foreach (string word in forbidden)
                    StringAssert.DoesNotContain(word, name.ToLowerInvariant(),
                        "owner ruling 2026-11-14: no romance yet (" + name + ")");
            CollectionAssert.AreEquivalent(
                new[] { "Rival", "Strained", "Stranger", "Acquaintance", "Friend", "Close" },
                Enum.GetNames(typeof(RelationshipStage)));
        }

        // ---- helpers ----------------------------------------------------------------------------

        private RelationshipTieDef Tie(string id, string a, string b, int strength)
        {
            var tie = ScriptableObject.CreateInstance<RelationshipTieDef>();
            tie.Id = id;
            tie.NpcA = a;
            tie.NpcB = b;
            tie.StartingStrength = strength;
            _made.Add(tie);
            return tie;
        }

        private RelationshipConfig Config()
        {
            var config = ScriptableObject.CreateInstance<RelationshipConfig>();
            config.Thresholds = Edges;
            config.Rules = Rules;
            _made.Add(config);
            return config;
        }
    }
}
