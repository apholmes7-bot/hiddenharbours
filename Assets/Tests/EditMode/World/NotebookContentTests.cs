using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.World;

namespace HiddenHarbours.Tests.World.EditMode
{
    /// <summary>
    /// <b>THE BOOK'S CONTENTS — that they are a VIEW and nothing else.</b>
    ///
    /// <para>Every assertion here is really the same one: the page is a function of the flags the
    /// world already keeps, so setting a flag is the ONLY way the book changes and there is no second
    /// copy of quest state to fall out of step. That is what makes a quest authored later
    /// retroactively correct on an old save.</para>
    /// </summary>
    public class NotebookContentTests
    {
        const int Cols = 31;   // the closest tier, where the copy budget is quoted

        static QuestDef Quest(string id, string title, params string[] stepFlags)
        {
            var def = ScriptableObject.CreateInstance<QuestDef>();
            def.Id = id;
            def.Title = title;
            def.Context = "";
            def.Steps = new List<QuestStep>();
            foreach (string f in stepFlags) def.Steps.Add(new QuestStep { Label = f, Flag = f });
            return def;
        }

        static KnowledgePageDef Page(string id, string tabId, string tabLabel, string head)
        {
            var def = ScriptableObject.CreateInstance<KnowledgePageDef>();
            def.Id = id;
            def.TabId = tabId;
            def.TabLabel = tabLabel;
            def.Head = head;
            def.Body = new List<string> { "a line of it" };
            return def;
        }

        static IFlagStore Flags(params string[] set)
        {
            var s = new InMemoryFlagStore();
            foreach (string f in set) s.Set(f, true);
            return s;
        }

        // ---- the quest view -----------------------------------------------------------------------

        [Test]
        public void AnUntouchedQuestIsActiveAndItsFirstStepIsCurrent()
        {
            var quests = new[] { Quest("quest.a", "Row out", "a", "b") };

            var active = NotebookContent.QuestBlocks(quests, Flags(), Cols, archived: false);
            var done = NotebookContent.QuestBlocks(quests, Flags(), Cols, archived: true);

            Assert.That(active.Count, Is.EqualTo(1));
            Assert.That(done, Is.Empty);

            var boxed = active[0].Rows.Where(r => r.HasBox).ToList();
            Assert.That(boxed[0].State, Is.EqualTo(NotebookStepState.Current), "the first unset step is current");
            Assert.That(boxed[1].State, Is.EqualTo(NotebookStepState.Todo));
        }

        [Test]
        public void SettingAFlagMovesTheCurrentMarkWithNothingElseTouched()
        {
            var quests = new[] { Quest("quest.a", "Row out", "a", "b", "c") };

            var blocks = NotebookContent.QuestBlocks(quests, Flags("a"), Cols, archived: false);
            var boxed = blocks[0].Rows.Where(r => r.HasBox).ToList();

            Assert.That(boxed[0].State, Is.EqualTo(NotebookStepState.Done));
            Assert.That(boxed[1].State, Is.EqualTo(NotebookStepState.Current));
            Assert.That(boxed[2].State, Is.EqualTo(NotebookStepState.Todo));
        }

        [Test]
        public void EveryFlagSetMovesTheQuestToDone()
        {
            var quests = new[] { Quest("quest.a", "Row out", "a", "b") };

            Assert.That(NotebookContent.QuestBlocks(quests, Flags("a", "b"), Cols, archived: false), Is.Empty);

            var done = NotebookContent.QuestBlocks(quests, Flags("a", "b"), Cols, archived: true);
            Assert.That(done.Count, Is.EqualTo(1));
            Assert.That(done[0].Archived, Is.True, "the Done treatment is drawn over an archived block");
            Assert.That(done[0].Rows.Where(r => r.HasBox).All(r => r.State == NotebookStepState.Done), Is.True);
        }

        [Test]
        public void AQuestWithNoStepsStaysActiveRatherThanReadingAsFinished()
        {
            // QuestBoard's ruling, carried into the page: a stepless quest is a content bug, and shown
            // on Done it reads as a thing she finished and never did — an invisible lie.
            var quests = new[] { Quest("quest.empty", "Nothing here") };

            Assert.That(NotebookContent.QuestBlocks(quests, Flags(), Cols, archived: false).Count, Is.EqualTo(1));
            Assert.That(NotebookContent.QuestBlocks(quests, Flags(), Cols, archived: true), Is.Empty);
        }

        [Test]
        public void TheViewReadsTheFlagsAndNeverWritesThem()
        {
            // ⭐ The whole architecture, in one assertion: building the page must not touch the store.
            var store = new InMemoryFlagStore();
            var quests = new[] { Quest("quest.a", "Row out", "a", "b") };

            NotebookContent.QuestBlocks(quests, store, Cols, archived: false);
            NotebookContent.QuestBlocks(quests, store, Cols, archived: true);

            Assert.That(store.Get("a"), Is.False, "reading the book set a flag");
            Assert.That(store.Get("b"), Is.False, "reading the book set a flag");
        }

        // ---- the tabs -----------------------------------------------------------------------------

        [Test]
        public void TheTabCountIsDrivenByTheDataAndNotByAConstant()
        {
            var knowledge = new[]
            {
                Page("k.tide", "tab.sea", "SEA", "Reading the tide"),
                Page("k.rod", "tab.gear", "GEAR", "Rod basics"),
                Page("k.trap", "tab.gear", "GEAR", "Setting a trap"),
            };

            var tabs = NotebookContent.Build(null, knowledge, null, Flags(), Cols);

            // TASKS, DONE, then one per DISTINCT knowledge tab id, then MINE.
            Assert.That(tabs.Select(t => t.Key).ToArray(),
                        Is.EqualTo(new[] { "tab.quests", "tab.done", "tab.sea", "tab.gear", "tab.notes" }));

            var gear = tabs.First(t => t.Key == "tab.gear");
            Assert.That(gear.Blocks.Count, Is.EqualTo(2), "two pages share the GEAR stub");
        }

        [Test]
        public void KnowledgeTabsKeepTheirAuthoredOrder()
        {
            var knowledge = new[]
            {
                Page("k.z", "tab.z", "Z", "last authored"),
                Page("k.a", "tab.a", "A", "first authored"),
            };

            var tabs = NotebookContent.KnowledgeTabs(knowledge, Cols);
            Assert.That(tabs.Select(t => t.Key).ToArray(), Is.EqualTo(new[] { "tab.z", "tab.a" }),
                "first-seen order, so the Data folder's order IS the fore-edge's order");
        }

        [Test]
        public void PastTheForeEdgesCapaCityTabsAreDroppedLoudlyAndMyNotesSurvives()
        {
            // The sabotage arm: eight stubs fit the fore-edge. A ninth must be REPORTED, because a
            // stub that vanishes with no word is indistinguishable from one never authored.
            var knowledge = new List<KnowledgePageDef>();
            for (int i = 0; i < 10; i++) knowledge.Add(Page($"k.{i}", $"tab.{i}", $"T{i}", $"page {i}"));

            var dropped = new List<string>();
            var tabs = NotebookContent.Build(null, knowledge, null, Flags(), Cols, dropped);

            Assert.That(tabs.Count, Is.EqualTo(NotebookKit.MaxTabs));
            Assert.That(dropped.Count, Is.EqualTo(10 + 3 - NotebookKit.MaxTabs));
            Assert.That(tabs.Last().Key, Is.EqualTo("tab.notes"), "My Notes must stay reachable");
            Assert.That(tabs[0].Key, Is.EqualTo("tab.quests"));
        }

        // ---- her own lines ---------------------------------------------------------------------------

        [Test]
        public void NotesKeepHerOrderAndTheWriteOnRowIsAlwaysLast()
        {
            var notes = new[]
            {
                new NotebookNote("second thought", false, false),
                new NotebookNote("first thought", false, false),
                new NotebookNote("a job", true, true),
            };

            var blocks = NotebookContent.NoteBlocks(notes, Cols);

            Assert.That(blocks.Count, Is.EqualTo(4), "three lines plus the write-on row");
            Assert.That(blocks[0].Rows[0].Text, Is.EqualTo("second thought"), "nothing here sorts");
            Assert.That(blocks[2].Rows[0].State, Is.EqualTo(NotebookStepState.Done));
            Assert.That(blocks.Last().Kind, Is.EqualTo(NotebookBlockKind.NewNote));
        }

        [Test]
        public void AnEmptyNotebookStillOffersTheWriteOnRow()
        {
            var blocks = NotebookContent.NoteBlocks(null, Cols);

            Assert.That(blocks.Count, Is.EqualTo(1));
            Assert.That(blocks[0].Kind, Is.EqualTo(NotebookBlockKind.NewNote));
        }

        [Test]
        public void HerProseIsNeverNormalised()
        {
            // #565's ruling, carried into the view: the one field that is her own prose, and the only
            // correct transformation is none.
            const string messy = "  gulls   again ...  ";
            var blocks = NotebookContent.NoteBlocks(new[] { new NotebookNote(messy, false, false) }, 60);

            Assert.That(blocks[0].SourceId, Is.EqualTo("note.0"));
            Assert.That(blocks[0].Rows[0].Text, Is.EqualTo(messy),
                "her indent and her run of spaces are hers — a line that fits is passed through whole");
        }
    }
}
