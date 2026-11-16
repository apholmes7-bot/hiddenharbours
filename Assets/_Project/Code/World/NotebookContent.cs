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

        /// <summary>The protected fish she documented and put back. Present only once there is one,
        /// so its stub is data like the knowledge run's.</summary>
        Released,
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

        /// <summary>The released page's stub. Keyed like the others; lettered within
        /// <see cref="NotebookKit.ChipChars"/> so the chip never clips it.</summary>
        public const string TabReleased = "tab.released";
        public const string LabelReleased = "FREED";

        /// <summary>The released page's opening section: what the page is, in her book's voice.</summary>
        public const string ReleasedIntroHead = "Let go";
        public const string ReleasedIntroBody = "Protected fish I caught, wrote down and put back.";

        /// <summary>One species' lines. <c>{0}</c> is a count; <c>{0}</c>/<c>{1}</c> in the times are the
        /// day number and the clock. Each time line fits the narrowest page
        /// (<see cref="NotebookKit.MinCols"/>) whole for a day up to three digits, so the day and its
        /// clock never wrap apart.</summary>
        public const string ReleasedCountFormat = "Let go: {0}";
        public const string ReleasedFirstFormat = "First d{0} {1}";
        public const string ReleasedLastFormat = "Last d{0} {1}";
        public const string ReleasedOnlyFormat = "Day {0}, {1}";

        /// <summary>Source-id prefix for a species' block, so the presenter's selection can name it.</summary>
        public const string ReleasedSourcePrefix = "released.";
        public const string ReleasedIntroSourceId = "released.intro";

        private const int MinutesPerHour = 60;
        private const int HoursPerDay = 24;
        private const int MinutesPerDay = MinutesPerHour * HoursPerDay;
        private const int SecondsPerMinute = 60;
        private const int ClockSecondsPerDay = MinutesPerDay * SecondsPerMinute;

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
            List<string> droppedTabs = null,
            IReadOnlyList<ReleasedCatchEntry> released = null,
            float secondsPerDay = GameConfig.DefaultSecondsPerDay)
        {
            var tabs = new List<NotebookTab>
            {
                new NotebookTab(TabQuests, LabelQuests, NotebookPageKind.Quests,
                                QuestBlocks(quests, flags, cols, archived: false)),
                new NotebookTab(TabDone, LabelDone, NotebookPageKind.Done,
                                QuestBlocks(quests, flags, cols, archived: true)),
            };

            foreach (var tab in KnowledgeTabs(knowledge, cols)) tabs.Add(tab);

            // The released page joins the fore-edge the first time she lets one go, and sits beside
            // My Notes: it is her own record, not a shipped page. Not Knowledge, so the trim below
            // never drops it.
            if (released != null && released.Count > 0)
                tabs.Add(new NotebookTab(TabReleased, LabelReleased, NotebookPageKind.Released,
                                         ReleasedBlocks(released, cols, secondsPerDay)));

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

        /// <summary>
        /// The released page: an opening section, then one section per species in the order she first
        /// let each go (the record's order, so nothing here sorts). Each is the knowledge page's own
        /// drawing (<see cref="NotebookLayout.Section"/>): the name on the wide lane, then how many and
        /// when. A single release names its one day; more name the first and the last.
        /// Empty or null in, the opening section alone out.
        /// </summary>
        public static List<NotebookBlock> ReleasedBlocks(IReadOnlyList<ReleasedCatchEntry> released, int cols,
                                                         float secondsPerDay)
        {
            var blocks = new List<NotebookBlock>
            {
                NotebookLayout.Section(ReleasedIntroHead, new[] { ReleasedIntroBody }, cols, ReleasedIntroSourceId),
            };
            if (released == null) return blocks;

            for (int i = 0; i < released.Count; i++)
            {
                ReleasedCatchEntry e = released[i];
                if (string.IsNullOrEmpty(e.SpeciesId) || e.Count <= 0) continue;

                string head = string.IsNullOrEmpty(e.DisplayName) ? e.SpeciesId : e.DisplayName;
                var body = new List<string> { string.Format(Invariant, ReleasedCountFormat, e.Count) };

                if (e.Count == 1)
                {
                    body.Add(When(ReleasedOnlyFormat, e.FirstSeconds, secondsPerDay));
                }
                else
                {
                    body.Add(When(ReleasedFirstFormat, e.FirstSeconds, secondsPerDay));
                    body.Add(When(ReleasedLastFormat, e.LastSeconds, secondsPerDay));
                }

                blocks.Add(NotebookLayout.Section(head, body, cols, ReleasedSourcePrefix + e.SpeciesId));
            }

            return blocks;
        }

        /// <summary>The 1-based day since a new game for a master-clock reading. A non-positive day
        /// length falls back to the shipped default rather than dividing by it.</summary>
        public static int DayNumber(double gameSeconds, float secondsPerDay)
        {
            double day = SafeDay(secondsPerDay);
            double s = gameSeconds > 0d ? gameSeconds : 0d;
            return (int)System.Math.Floor(s / day) + 1;
        }

        /// <summary>The clock within that day, "HH:MM". The reading is rounded to the nearest clock
        /// second first, then truncated to the minute, so float error just under a minute (06:30:59.999
        /// for an exact 06:31) cannot show the minute before. Pure and deterministic.</summary>
        public static string ClockText(double gameSeconds, float secondsPerDay)
        {
            double day = SafeDay(secondsPerDay);
            double s = gameSeconds > 0d ? gameSeconds : 0d;
            double into = s - System.Math.Floor(s / day) * day;
            double clockSecond = System.Math.Round(into / day * ClockSecondsPerDay,
                                                   System.MidpointRounding.AwayFromZero);
            int minute = (int)(clockSecond / SecondsPerMinute);
            if (minute >= MinutesPerDay) minute = MinutesPerDay - 1;
            return (minute / MinutesPerHour).ToString("00", Invariant) + ":"
                 + (minute % MinutesPerHour).ToString("00", Invariant);
        }

        private static string When(string format, double gameSeconds, float secondsPerDay)
            => string.Format(Invariant, format, DayNumber(gameSeconds, secondsPerDay),
                             ClockText(gameSeconds, secondsPerDay));

        private static double SafeDay(float secondsPerDay)
            => secondsPerDay > 0f ? secondsPerDay : GameConfig.DefaultSecondsPerDay;

        private static readonly System.Globalization.CultureInfo Invariant =
            System.Globalization.CultureInfo.InvariantCulture;
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
