using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.World;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.World.EditMode
{
    /// <summary>
    /// <b>THE RELEASED PAGE</b>: the protected fish she documented and put back, drawn as knowledge
    /// sections on their own stub, present only once there is one.
    /// </summary>
    public class NotebookReleasedPageTests
    {
        const int Cols = 31;
        const float Day = GameConfig.DefaultSecondsPerDay;

        readonly List<GameObject> _spawned = new List<GameObject>();

        static ReleasedCatchEntry Entry(string id, string name, int count, double first, double last)
            => new ReleasedCatchEntry(id, name, count, first, last);

        static IFlagStore Flags() => new InMemoryFlagStore();

        static KnowledgePageDef Page(string id, string tabId)
        {
            var def = ScriptableObject.CreateInstance<KnowledgePageDef>();
            def.Id = id;
            def.TabId = tabId;
            def.TabLabel = "K";
            def.Head = id;
            def.Body = new List<string> { "a line" };
            return def;
        }

        [SetUp]
        public void SetUp()
        {
            InteractionGate.Reset();
            MoveActionClaim.Reset();
            CancelKeyClaim.Reset();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _spawned) if (go != null) Object.DestroyImmediate(go);
            _spawned.Clear();
            InteractionGate.Reset();
            MoveActionClaim.Reset();
            CancelKeyClaim.Reset();
        }

        // ---- the stub ---------------------------------------------------------------------------------

        [Test]
        public void NothingLetGoMeansNoStub()
        {
            var none = NotebookContent.Build(null, null, null, Flags(), Cols);
            var empty = NotebookContent.Build(null, null, null, Flags(), Cols, null,
                                              new List<ReleasedCatchEntry>(), Day);

            Assert.That(none.Any(t => t.Key == NotebookContent.TabReleased), Is.False);
            Assert.That(empty.Any(t => t.Key == NotebookContent.TabReleased), Is.False);
        }

        [Test]
        public void OneReleaseAddsTheStubBesideMyNotes()
        {
            var released = new[] { Entry("fish.wolffish", "Wolffish", 1, 0d, 0d) };
            var tabs = NotebookContent.Build(null, null, null, Flags(), Cols, null, released, Day);

            Assert.That(tabs.Select(t => t.Key).ToArray(),
                        Is.EqualTo(new[] { "tab.quests", "tab.done", NotebookContent.TabReleased, "tab.notes" }));

            NotebookTab tab = tabs.First(t => t.Key == NotebookContent.TabReleased);
            Assert.That(tab.Kind, Is.EqualTo(NotebookPageKind.Released));
            Assert.That(tab.Label.Length, Is.LessThanOrEqualTo(NotebookKit.ChipChars), "the chip never clips it");
        }

        [Test]
        public void AFullForeEdgeDropsKnowledgeNotTheReleasedPage()
        {
            var knowledge = new List<KnowledgePageDef>();
            for (int i = 0; i < 10; i++) knowledge.Add(Page($"k.{i}", $"tab.{i}"));
            var released = new[] { Entry("fish.wolffish", "Wolffish", 1, 0d, 0d) };

            var dropped = new List<string>();
            var tabs = NotebookContent.Build(null, knowledge, null, Flags(), Cols, dropped, released, Day);

            Assert.That(tabs.Count, Is.EqualTo(NotebookKit.MaxTabs));
            Assert.That(tabs.Any(t => t.Key == NotebookContent.TabReleased), Is.True);
            Assert.That(tabs.Last().Key, Is.EqualTo("tab.notes"), "My Notes must stay reachable");
            Assert.That(dropped.All(k => k.StartsWith("tab.") && k != NotebookContent.TabReleased), Is.True);
        }

        // ---- the page ---------------------------------------------------------------------------------

        [Test]
        public void ThePageOpensOnItsIntroThenOneSectionPerSpeciesInRecordOrder()
        {
            var released = new[]
            {
                Entry("fish.wolffish", "Wolffish", 1, 0d, 0d),
                Entry("fish.porbeagle", "Porbeagle", 2, 0d, Day),
            };

            var blocks = NotebookContent.ReleasedBlocks(released, Cols, Day);

            Assert.That(blocks.Count, Is.EqualTo(3));
            Assert.That(blocks.All(b => b.Kind == NotebookBlockKind.Section), Is.True,
                        "the knowledge page's own drawing, not a new one");
            Assert.That(blocks[0].SourceId, Is.EqualTo(NotebookContent.ReleasedIntroSourceId));
            Assert.That(blocks[1].Rows[0].Text, Is.EqualTo("Wolffish"));
            Assert.That(blocks[1].SourceId, Is.EqualTo(NotebookContent.ReleasedSourcePrefix + "fish.wolffish"));
            Assert.That(blocks[2].Rows[0].Text, Is.EqualTo("Porbeagle"));
        }

        [Test]
        public void ASingleReleaseNamesItsDayAndManyNameTheFirstAndTheLast()
        {
            // Day length 1800 s: 2700 s is day 2 at 12:00; 3 days + 450 s is day 4 at 06:00.
            var released = new[]
            {
                Entry("fish.wolffish", "Wolffish", 1, Day * 1.5, Day * 1.5),
                Entry("fish.porbeagle", "Porbeagle", 3, Day * 1.5, Day * 3.25),
            };

            var blocks = NotebookContent.ReleasedBlocks(released, Cols, Day);
            var one = blocks[1].Rows.Select(r => r.Text).ToArray();
            var many = blocks[2].Rows.Select(r => r.Text).ToArray();

            Assert.That(one, Is.EqualTo(new[] { "Wolffish", "Let go: 1", "Day 2, 12:00" }));
            Assert.That(many, Is.EqualTo(new[] { "Porbeagle", "Let go: 3", "First: day 2, 12:00", "Last: day 4, 06:00" }));
        }

        [Test]
        public void ANamelessRowFallsBackToItsIdAndAnEmptyRowIsSkipped()
        {
            var released = new[]
            {
                Entry("fish.wolffish", "", 1, 0d, 0d),
                Entry("", "Ghost", 1, 0d, 0d),
                Entry("fish.none", "None", 0, 0d, 0d),
            };

            var blocks = NotebookContent.ReleasedBlocks(released, Cols, Day);

            Assert.That(blocks.Count, Is.EqualTo(2), "the intro and the one real row");
            Assert.That(blocks[1].Rows[0].Text, Is.EqualTo("fish.wolffish"));
        }

        [Test]
        public void TheClockIsTheMinuteWithinTheDayAndABadDayLengthFallsBack()
        {
            Assert.That(NotebookContent.DayNumber(0d, Day), Is.EqualTo(1));
            Assert.That(NotebookContent.ClockText(0d, Day), Is.EqualTo("00:00"));
            Assert.That(NotebookContent.ClockText(Day - 0.001d, Day), Is.EqualTo("23:59"));
            Assert.That(NotebookContent.DayNumber(Day, Day), Is.EqualTo(2));
            Assert.That(NotebookContent.ClockText(Day * 0.25d, 0f), Is.EqualTo("06:00"),
                        "a zero day length reads as the shipped default");
            Assert.That(NotebookContent.DayNumber(-5d, Day), Is.EqualTo(1), "time never runs before the start");
        }

        // ---- the binding --------------------------------------------------------------------------------

        NotebookPresenter StandABook(System.Func<IReadOnlyList<ReleasedCatchEntry>> released)
        {
            var go = new GameObject("book");
            _spawned.Add(go);
            var book = go.AddComponent<NotebookPresenter>();
            book.WireContent(() => new List<QuestDef>(), () => new List<KnowledgePageDef>(),
                             () => new List<NotebookNote>(), new InMemoryFlagStore(), released);
            return book;
        }

        [Test]
        public void TheBookReadsTheReleasedSupplierEveryTimeItRebuilds()
        {
            var record = new List<ReleasedCatchEntry>();
            var book = StandABook(() => record);

            book.RebuildModel();
            Assert.That(book.Tabs.Any(t => t.Key == NotebookContent.TabReleased), Is.False,
                        "nothing let go yet, so no stub");

            record.Add(Entry("fish.wolffish", "Wolffish", 1, 0d, 0d));
            book.RebuildModel();
            NotebookTab tab = book.Tabs.FirstOrDefault(t => t.Key == NotebookContent.TabReleased);
            Assert.That(tab, Is.Not.Null, "a release made while the book was shut is there the next time");
            Assert.That(tab.Blocks.Count, Is.EqualTo(2));
        }

        [Test]
        public void ABookWiredWithoutTheSupplierShowsNoReleasedPage()
        {
            var go = new GameObject("book");
            _spawned.Add(go);
            var book = go.AddComponent<NotebookPresenter>();
            book.WireContent(() => new List<QuestDef>(), () => new List<KnowledgePageDef>(),
                             () => new List<NotebookNote>(), new InMemoryFlagStore());

            book.RebuildModel();
            Assert.That(book.Tabs.Any(t => t.Key == NotebookContent.TabReleased), Is.False);
        }
    }
}
