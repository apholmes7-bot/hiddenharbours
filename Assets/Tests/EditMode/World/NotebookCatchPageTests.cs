using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.World;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.World.EditMode
{
    /// <summary>
    /// <b>THE CATCH PAGE</b>: every species she has caught, with how many and the record weight, drawn as
    /// knowledge sections on their own stub, present only once there is one.
    /// </summary>
    public class NotebookCatchPageTests
    {
        const int Cols = 31;

        readonly List<GameObject> _spawned = new List<GameObject>();

        static CaughtSpeciesEntry Entry(string id, string name, int count, int released, float kg)
            => new CaughtSpeciesEntry(id, name, count, released, kg);

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
        public void NothingCaughtMeansNoStub()
        {
            var none = NotebookContent.Build(null, null, null, Flags(), Cols);
            var empty = NotebookContent.Build(null, null, null, Flags(), Cols, null, new List<CaughtSpeciesEntry>());

            Assert.That(none.Any(t => t.Key == NotebookContent.TabCaught), Is.False);
            Assert.That(empty.Any(t => t.Key == NotebookContent.TabCaught), Is.False);
        }

        [Test]
        public void TheFirstCatchAddsTheStubBesideMyNotes()
        {
            var caught = new[] { Entry("fish.atlantic_cod", "Atlantic Cod", 1, 0, 3.2f) };
            var tabs = NotebookContent.Build(null, null, null, Flags(), Cols, null, caught);

            Assert.That(tabs.Select(t => t.Key).ToArray(),
                        Is.EqualTo(new[] { "tab.quests", "tab.done", NotebookContent.TabCaught, "tab.notes" }));

            NotebookTab tab = tabs.First(t => t.Key == NotebookContent.TabCaught);
            Assert.That(tab.Kind, Is.EqualTo(NotebookPageKind.Caught));
            Assert.That(tab.Label.Length, Is.LessThanOrEqualTo(NotebookKit.ChipChars), "the chip never clips it");
        }

        [Test]
        public void AFullForeEdgeDropsKnowledgeNotTheCatchPage()
        {
            var knowledge = new List<KnowledgePageDef>();
            for (int i = 0; i < 10; i++) knowledge.Add(Page($"k.{i}", $"tab.{i}"));
            var caught = new[] { Entry("fish.atlantic_cod", "Atlantic Cod", 1, 0, 3.2f) };

            var dropped = new List<string>();
            var tabs = NotebookContent.Build(null, knowledge, null, Flags(), Cols, dropped, caught);

            Assert.That(tabs.Count, Is.EqualTo(NotebookKit.MaxTabs));
            Assert.That(tabs.Any(t => t.Key == NotebookContent.TabCaught), Is.True);
            Assert.That(tabs.Last().Key, Is.EqualTo("tab.notes"), "My Notes must stay reachable");
            Assert.That(dropped.Contains(NotebookContent.TabCaught), Is.False);
        }

        // ---- the page ---------------------------------------------------------------------------------

        [Test]
        public void OneSectionPerSpeciesInRecordOrderWithCountAndRecord()
        {
            var caught = new[]
            {
                Entry("fish.atlantic_cod", "Atlantic Cod", 3, 0, 7.5f),
                Entry("fish.atlantic_wolffish", "Wolffish", 2, 2, 9f),
            };

            var blocks = NotebookContent.CatchBlocks(caught, Cols);

            Assert.That(blocks.Count, Is.EqualTo(2));
            Assert.That(blocks.All(b => b.Kind == NotebookBlockKind.Section), Is.True,
                        "the knowledge page's own drawing, not a new one");
            Assert.That(blocks[0].SourceId, Is.EqualTo(NotebookContent.CaughtSourcePrefix + "fish.atlantic_cod"));
            Assert.That(blocks[0].Rows.Select(r => r.Text).ToArray(),
                        Is.EqualTo(new[] { "Atlantic Cod", "Caught: 3", "Best: 7.5 kg" }),
                        "no released line when none went back");
            Assert.That(blocks[1].Rows.Select(r => r.Text).ToArray(),
                        Is.EqualTo(new[] { "Wolffish", "Caught: 2", "Best: 9.0 kg", "Let go: 2" }));
        }

        [Test]
        public void ANamelessRowFallsBackToItsIdAndAnEmptyRowIsSkipped()
        {
            var caught = new[]
            {
                Entry("fish.atlantic_cod", "", 1, 0, 1f),
                Entry("", "Ghost", 1, 0, 1f),
                Entry("fish.none", "None", 0, 0, 1f),
            };

            var blocks = NotebookContent.CatchBlocks(caught, Cols);

            Assert.That(blocks.Count, Is.EqualTo(1));
            Assert.That(blocks[0].Rows[0].Text, Is.EqualTo("fish.atlantic_cod"));
        }

        [Test]
        public void EveryDataLineFitsTheNarrowestPageWhole()
        {
            // The widest reading: four-digit counts and a four-digit record weight, measured against the
            // kit's narrowest text lane, so no number ever wraps away from its word.
            var caught = new[] { Entry("fish.bluefin_tuna", "Tuna", 9999, 9999, 9999.9f) };

            var blocks = NotebookContent.CatchBlocks(caught, NotebookKit.MinCols);

            foreach (NotebookRow row in blocks[0].Rows.Skip(1))
                Assert.That(row.Text.Length, Is.LessThanOrEqualTo(NotebookKit.MinCols), row.Text);
            Assert.That(blocks[0].Rows.Count, Is.EqualTo(4), "name, caught, best, let go: nothing wrapped");
        }

        // ---- the binding --------------------------------------------------------------------------------

        [Test]
        public void TheBookReadsTheCatchSupplierEveryTimeItRebuilds()
        {
            var record = new List<CaughtSpeciesEntry>();
            var go = new GameObject("book");
            _spawned.Add(go);
            var book = go.AddComponent<NotebookPresenter>();
            book.WireContent(() => new List<QuestDef>(), () => new List<KnowledgePageDef>(),
                             () => new List<NotebookNote>(), new InMemoryFlagStore(), () => record);

            book.RebuildModel();
            Assert.That(book.Tabs.Any(t => t.Key == NotebookContent.TabCaught), Is.False, "nothing caught yet");

            record.Add(Entry("fish.atlantic_cod", "Atlantic Cod", 1, 0, 2f));
            book.RebuildModel();
            NotebookTab tab = book.Tabs.FirstOrDefault(t => t.Key == NotebookContent.TabCaught);
            Assert.That(tab, Is.Not.Null, "a catch made while the book was shut is there the next time");
            Assert.That(tab.Blocks.Count, Is.EqualTo(1));
        }

        [Test]
        public void ABookWiredWithoutTheSupplierShowsNoCatchPage()
        {
            var go = new GameObject("book");
            _spawned.Add(go);
            var book = go.AddComponent<NotebookPresenter>();
            book.WireContent(() => new List<QuestDef>(), () => new List<KnowledgePageDef>(),
                             () => new List<NotebookNote>(), new InMemoryFlagStore());

            book.RebuildModel();
            Assert.That(book.Tabs.Any(t => t.Key == NotebookContent.TabCaught), Is.False);
        }
    }
}
