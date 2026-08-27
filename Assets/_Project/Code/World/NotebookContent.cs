using System.Collections.Generic;
using HiddenHarbours.Core;

namespace HiddenHarbours.World
{
    /// <summary>Which family a tab belongs to. The four are permanent furniture; knowledge arrives as
    /// sub-tabs, so its COUNT is data and its labels are runtime text.</summary>
    public enum NotebookPageKind
    {
        Quests,
        Done,
        Knowledge,
        Notes,
    }

    /// <summary>One stub off the fore-edge.</summary>
    public sealed class NotebookTab
    {
        public string Key { get; }

        /// <summary>What is lettered on the chip. <b>Clipped at the chip, not here</b> — the kit clips
        /// a label at 5 characters and the presenter does the clipping, so the full label stays
        /// available for anything that wants to read it.</summary>
        public string Label { get; }

        public NotebookPageKind Kind { get; }

        /// <summary>The blocks this tab's leaves are flowed from.</summary>
        public IReadOnlyList<NotebookBlock> Blocks { get; }

        public NotebookTab(string key, string label, NotebookPageKind kind, IReadOnlyList<NotebookBlock> blocks)
        {
            Key = key ?? "";
            Label = label ?? "";
            Kind = kind;
            Blocks = blocks ?? new List<NotebookBlock>();
        }
    }

    /// <summary>
    /// <b>THE BOOK'S CONTENTS — assembled from what the world already keeps, and nothing else.</b>
    ///
    /// <para><b>⭐ Quests are a VIEW, not a store.</b> Every quest row here is derived from
    /// <see cref="QuestBoard"/> reading the flags the world already sets (#564). Nothing in this file
    /// writes a flag, and no quest state is saved anywhere — which is why a quest authored later is
    /// retroactively correct on an old save, and why opening the book cannot corrupt anything.</para>
    ///
    /// <para><b>Her notes are the exception, and only because nothing else could reconstruct them</b>
    /// (#565, SaveData v11). They arrive here already loaded; this file does not touch the save
    /// service.</para>
    ///
    /// <para>Pure and engine-free on purpose — the tab set, the ordering and the block contents are
    /// all provable headlessly, so the presenter is left holding nothing but the drawing.</para>
    /// </summary>
    public static class NotebookContent
    {
        public const string TabQuests = "tab.quests";
        public const string TabDone = "tab.done";
        public const string TabNotes = "tab.notes";

        /// <summary>Labels are CAPS because the kit letters chips as labels, not as voice.</summary>
        public const string LabelQuests = "TASKS";
        public const string LabelDone = "DONE";
        public const string LabelNotes = "MINE";

        /// <summary>
        /// Build the whole tab set.
        ///
        /// <para><b>Tab COUNT is driven by data</b>, exactly as the kit requires: the three permanent
        /// stubs plus one per distinct knowledge tab id, in first-seen order. The kit fits eight stubs
        /// on the fore-edge at any legal size, so anything past <see cref="NotebookKit.MaxTabs"/> is
        /// dropped and REPORTED through <paramref name="droppedTabs"/> rather than silently trimmed —
        /// a stub that vanishes with no word is indistinguishable from one that was never authored.</para>
        /// </summary>
        public static List<NotebookTab> Build(
            IReadOnlyList<QuestDef> quests,
            IReadOnlyList<KnowledgePageDef> knowledge,
            IReadOnlyList<NotebookNote> notes,
            IFlagStore flags,
            int cols,
            List<string> droppedTabs = null)
        {
            var tabs = new List<NotebookTab>
            {
                new NotebookTab(TabQuests, LabelQuests, NotebookPageKind.Quests,
                                QuestBlocks(quests, flags, cols, archived: false)),
                new NotebookTab(TabDone, LabelDone, NotebookPageKind.Done,
                                QuestBlocks(quests, flags, cols, archived: true)),
            };

            foreach (var tab in KnowledgeTabs(knowledge, cols)) tabs.Add(tab);

            tabs.Add(new NotebookTab(TabNotes, LabelNotes, NotebookPageKind.Notes, NoteBlocks(notes, cols)));

            if (tabs.Count > NotebookKit.MaxTabs)
            {
                // Trim from the knowledge run — the permanent four are furniture and My Notes must stay
                // reachable, so the sub-tabs are the only ones it is ever right to drop.
                int excess = tabs.Count - NotebookKit.MaxTabs;
                for (int i = tabs.Count - 2; i >= 0 && excess > 0; i--)
                {
                    if (tabs[i].Kind != NotebookPageKind.Knowledge) continue;
                    droppedTabs?.Add(tabs[i].Key);
                    tabs.RemoveAt(i);
                    excess--;
                }
            }

            return tabs;
        }

        /// <summary>
        /// The quest blocks for one side of the ledger.
        ///
        /// <para>Active or archived is <see cref="QuestBoard"/>'s answer, never a stored field, and the
        /// CURRENT step is the first unset one — so the arrow moves the moment the world sets a flag,
        /// with nothing to keep in sync.</para>
        /// </summary>
        public static List<NotebookBlock> QuestBlocks(IReadOnlyList<QuestDef> quests, IFlagStore flags,
                                                      int cols, bool archived)
        {
            var blocks = new List<NotebookBlock>();
            if (quests == null) return blocks;

            foreach (var quest in quests)
            {
                if (quest == null) continue;

                QuestReading reading = QuestBoard.Read(quest, flags);
                if (reading.Archived != archived) continue;

                var steps = new List<(string, NotebookStepState)>();
                for (int i = 0; i < quest.Steps.Count; i++)
                {
                    QuestStep step = quest.Steps[i];
                    if (step == null) continue;

                    NotebookStepState state =
                        QuestBoard.IsDone(step, flags) ? NotebookStepState.Done
                        : i == reading.CurrentStepIndex ? NotebookStepState.Current
                        : NotebookStepState.Todo;

                    steps.Add((step.Label, state));
                }

                blocks.Add(NotebookLayout.Quest(quest.Title, quest.Context, steps, cols,
                                                quest.Id, reading.Archived));
            }

            return blocks;
        }

        /// <summary>One tab per distinct <see cref="KnowledgePageDef.TabId"/>, in first-seen order —
        /// so the authoring order in the Data folder is the order on the fore-edge, and no separate
        /// ordering field can drift out of step with it.</summary>
        public static List<NotebookTab> KnowledgeTabs(IReadOnlyList<KnowledgePageDef> pages, int cols)
        {
            var tabs = new List<NotebookTab>();
            if (pages == null) return tabs;

            var order = new List<string>();
            var byTab = new Dictionary<string, List<NotebookBlock>>();
            var labels = new Dictionary<string, string>();

            foreach (var page in pages)
            {
                if (page == null || string.IsNullOrEmpty(page.TabId)) continue;

                if (!byTab.TryGetValue(page.TabId, out var blocks))
                {
                    blocks = new List<NotebookBlock>();
                    byTab[page.TabId] = blocks;
                    order.Add(page.TabId);
                    labels[page.TabId] = page.TabLabel;
                }

                blocks.Add(NotebookLayout.Section(page.Head, page.Body, cols, page.Id));
            }

            foreach (string tabId in order)
                tabs.Add(new NotebookTab(tabId, labels[tabId], NotebookPageKind.Knowledge, byTab[tabId]));

            return tabs;
        }

        /// <summary>Her lines, in HER order, with the write-on row last. Order in the list IS the order
        /// on the page (#565's ruling), so nothing here sorts.</summary>
        public static List<NotebookBlock> NoteBlocks(IReadOnlyList<NotebookNote> notes, int cols)
        {
            var blocks = new List<NotebookBlock>();

            if (notes != null)
            {
                for (int i = 0; i < notes.Count; i++)
                {
                    NotebookNote note = notes[i];
                    blocks.Add(NotebookLayout.Note(note.Text, note.IsTask, note.Done, cols,
                                                   "note." + i.ToString()));
                }
            }

            blocks.Add(NotebookLayout.NewNote());
            return blocks;
        }
    }

    /// <summary>
    /// One of the player's own lines, as the book sees it.
    ///
    /// <para><b>A deliberate twin of <c>PlayerNoteDto</c> rather than a reuse of it.</b> The DTO is
    /// Core's save shape and is append-only forever; this is World's view shape. Keeping them apart
    /// means the presenter cannot accidentally pin the save format, and the one place they meet is the
    /// conversion — which is the place a test can stand.</para>
    /// </summary>
    public readonly struct NotebookNote
    {
        public readonly string Text;
        public readonly bool IsTask;
        public readonly bool Done;

        public NotebookNote(string text, bool isTask, bool done)
        {
            Text = text ?? "";
            IsTask = isTask;
            Done = done;
        }
    }
}
