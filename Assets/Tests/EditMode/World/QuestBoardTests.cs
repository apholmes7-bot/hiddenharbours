using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.World;

namespace HiddenHarbours.Tests.World.EditMode
{
    /// <summary>
    /// <b>The quest view: flags in, active/current/archived out.</b> Pure state, no assets, no scene —
    /// runs anywhere.
    ///
    /// <para>The thing under test is the claim that a quest needs NO saved state. Every assertion here
    /// is of the form "given these flags, the book says this" — if any of them ever needed a stored
    /// value to pass, the design would have been broken.</para>
    /// </summary>
    public class QuestBoardTests
    {
        static QuestDef Quest(string id, params string[] stepFlags)
        {
            var def = ScriptableObject.CreateInstance<QuestDef>();
            def.Id = id;
            def.Title = id;
            def.Steps = new List<QuestStep>();
            foreach (string f in stepFlags) def.Steps.Add(new QuestStep { Label = f, Flag = f });
            return def;
        }

        static IFlagStore Flags(params string[] set)
        {
            var s = new InMemoryFlagStore();
            foreach (string f in set) s.Set(f, true);
            return s;
        }

        [Test]
        public void NothingDoneYet_IsActive_AndTheCurrentStepIsTheFirst()
        {
            var r = QuestBoard.Read(Quest("q", "a", "b", "c"), Flags());

            Assert.That(r.Active, Is.True);
            Assert.That(r.Archived, Is.False);
            Assert.That(r.CurrentStepIndex, Is.EqualTo(0));
            Assert.That(r.DoneCount, Is.EqualTo(0));
            Assert.That(r.StepCount, Is.EqualTo(3));
        }

        [Test]
        public void EveryFlagSet_IsArchived_AndHasNoCurrentStep()
        {
            var r = QuestBoard.Read(Quest("q", "a", "b", "c"), Flags("a", "b", "c"));

            Assert.That(r.Archived, Is.True);
            Assert.That(r.Active, Is.False);
            Assert.That(r.CurrentStepIndex, Is.EqualTo(-1), "an archived quest has nothing current");
            Assert.That(r.DoneCount, Is.EqualTo(3));
        }

        [Test]
        public void TheCurrentStepIsTheFirstUNSETStep_NotTheLastDoneOne()
        {
            var r = QuestBoard.Read(Quest("q", "a", "b", "c"), Flags("a"));

            Assert.That(r.CurrentStepIndex, Is.EqualTo(1));
            Assert.That(r.DoneCount, Is.EqualTo(1));
            Assert.That(r.Active, Is.True);
        }

        /// <summary>
        /// ⚠ THE BRANCH THAT LOOKS LIKE THE OTHERS AND IS NOT. Flags are set by the world in whatever
        /// order the player earns them, so "done" is NOT a prefix of the step list — step 3 can be
        /// done while step 2 is not. The current step must be the first UNSET one, not "the one after
        /// the last done one", and a count-based implementation gets this wrong while passing every
        /// other test in this file.
        /// </summary>
        [Test]
        public void DoneStepsNeedNotBeContiguous()
        {
            var r = QuestBoard.Read(Quest("q", "a", "b", "c"), Flags("a", "c"));

            Assert.That(r.CurrentStepIndex, Is.EqualTo(1),
                "step c being done must not drag the cursor past the unfinished step b");
            Assert.That(r.DoneCount, Is.EqualTo(2));
            Assert.That(r.Archived, Is.False, "two of three done is not done");
        }

        /// <summary>
        /// The design's headline claim, tested rather than asserted in a comment: a quest authored
        /// TODAY about beats the player finished last week opens already archived, because it reads
        /// flags that were set at the time. A saved-progress model would show it untouched and lie.
        /// </summary>
        [Test]
        public void AQuestAddedAfterTheFactIsRetroactivelyCorrect()
        {
            // The world set these long before the quest asset existed.
            var world = Flags("read_logbook", "met_ginny");

            var writtenLater = Quest("quest.written_later", "read_logbook", "met_ginny");

            Assert.That(QuestBoard.IsArchived(writtenLater, world), Is.True,
                "a quest over already-set flags must open DONE, not fresh");
        }

        [Test]
        public void TwoQuestsMayReadTheSameFlag()
        {
            // Flags are shared truths, not per-quest progress. met_ginny advances both books at once,
            // which is the point — and the starter content relies on it.
            var flags = Flags("met_ginny");
            var a = Quest("quest.a", "met_ginny");
            var b = Quest("quest.b", "met_ginny", "something_else");

            Assert.That(QuestBoard.IsArchived(a, flags), Is.True);
            Assert.That(QuestBoard.Read(b, flags).CurrentStepIndex, Is.EqualTo(1));
        }

        // ---- the degenerate and defensive cases -------------------------------------------------

        /// <summary>
        /// A stepless quest is a content bug (validation forbids it). This pins HOW it fails: Active
        /// with nothing to do — visibly broken — rather than Archived, which would put a thing the
        /// player never did onto the Done tab and read as finished. "All steps set" is vacuously true
        /// of an empty list, so this is a real fork, not a formality.
        /// </summary>
        [Test]
        public void AQuestWithNoStepsIsActive_NotVacuouslyArchived()
        {
            var r = QuestBoard.Read(Quest("q"), Flags());

            Assert.That(r.Archived, Is.False, "an empty step list must not read as DONE");
            Assert.That(r.CurrentStepIndex, Is.EqualTo(-1));
            Assert.That(r.StepCount, Is.EqualTo(0));
        }

        [Test]
        public void AnEmptyFlagIdIsNeverDone_SoTheQuestStaysVisiblyActive()
        {
            var def = ScriptableObject.CreateInstance<QuestDef>();
            def.Id = "quest.half_authored";
            def.Steps = new List<QuestStep> { new QuestStep { Label = "unfinished", Flag = "" } };

            var r = QuestBoard.Read(def, Flags());

            Assert.That(r.Archived, Is.False);
            Assert.That(r.CurrentStepIndex, Is.EqualTo(0),
                "a half-authored step must sit there visibly, not complete itself");
        }

        [Test]
        public void NullsAreToleratedRatherThanThrown()
        {
            Assert.That(QuestBoard.Read(null, Flags()).StepCount, Is.EqualTo(0));
            Assert.That(QuestBoard.Read(null, null).Archived, Is.False);

            // No flag store at all (an EditMode rig, or a scene opened without the bootstrap) reads
            // as "nothing done yet" rather than throwing.
            var r = QuestBoard.Read(Quest("q", "a", "b"), null);
            Assert.That(r.DoneCount, Is.EqualTo(0));
            Assert.That(r.CurrentStepIndex, Is.EqualTo(0));
        }

        // ---- the two tabs --------------------------------------------------------------------------

        [Test]
        public void SortSplitsTheTabsAndKeepsTheGivenOrder()
        {
            var flags = Flags("a");
            var done = Quest("quest.done", "a");
            var open1 = Quest("quest.open_1", "b");
            var open2 = Quest("quest.open_2", "a", "c");

            var active = new List<QuestDef>();
            var archived = new List<QuestDef>();
            QuestBoard.Sort(new[] { open1, done, open2 }, flags, active, archived);

            Assert.That(active, Is.EqualTo(new[] { open1, open2 }));
            Assert.That(archived, Is.EqualTo(new[] { done }));
        }

        [Test]
        public void SortClearsItsOutputsAndSkipsNullEntries()
        {
            var active = new List<QuestDef> { Quest("stale") };
            var archived = new List<QuestDef> { Quest("stale") };

            QuestBoard.Sort(new QuestDef[] { null, Quest("quest.ok", "a") }, Flags(), active, archived);

            Assert.That(active.Count, Is.EqualTo(1), "the stale entry must be cleared, the null skipped");
            Assert.That(archived, Is.Empty);
        }

        [Test]
        public void SortOnNoQuestsIsAnEmptyBookRatherThanAThrow()
        {
            var active = new List<QuestDef>();
            var archived = new List<QuestDef>();

            Assert.DoesNotThrow(() => QuestBoard.Sort(null, Flags(), active, archived));
            Assert.That(active, Is.Empty);
            Assert.That(archived, Is.Empty);
        }
    }
}
