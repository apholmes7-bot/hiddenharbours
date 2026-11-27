using System.Collections.Generic;
using HiddenHarbours.Core;
using Color32 = UnityEngine.Color32;

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

        /// <summary>Every species she has caught: how many, and the record. Present only once there is
        /// one, so its stub is data like the knowledge run's.</summary>
        Caught,

        /// <summary>The gear he owns, each with its picture, name and count. Present only once he owns
        /// something, like the catch page.</summary>
        Gear,

        /// <summary>The pictures he has kept (save v28), newest first. Present only once there is one,
        /// like the catch page.</summary>
        Photos,
    }

    /// <summary>One kept picture, as the album page sees it: a head and its lines, already worded by
    /// <see cref="NotebookPhotoPage"/>, and its little Polaroid (owner 2026-11-26: "yes photoalbum should
    /// save small pictures as little poloraids"): the stored thumbnail and the frame it is drawn in, all
    /// sizes in kit pixels from the camera's def. World's view shape, apart from Core's save row.</summary>
    public readonly struct NotebookPhotoEntry
    {
        public readonly int Number;
        public readonly string Head;
        public readonly IReadOnlyList<string> Lines;

        /// <summary>The stored thumbnail (<see cref="PhotoThumbnail"/>), or empty: the frame is then drawn
        /// with no picture in it.</summary>
        public readonly string Thumbnail;

        /// <summary>The Polaroid's outer size and its side and bottom borders, kit pixels. A zero width
        /// draws no Polaroid (a plain section, as before the ruling).</summary>
        public readonly int PolaroidWidthPx;
        public readonly int PolaroidHeightPx;
        public readonly int PolaroidBorderPx;
        public readonly int PolaroidBottomBorderPx;

        /// <summary>The Polaroid's white card.</summary>
        public readonly Color32 PolaroidFrameColour;

        public NotebookPhotoEntry(int number, string head, IReadOnlyList<string> lines)
            : this(number, head, lines, "", 0, 0, 0, 0, default) { }

        public NotebookPhotoEntry(int number, string head, IReadOnlyList<string> lines, string thumbnail,
                                  int polaroidWidthPx, int polaroidHeightPx, int polaroidBorderPx,
                                  int polaroidBottomBorderPx, Color32 polaroidFrameColour)
        {
            Number = number;
            Head = head ?? "";
            Lines = lines;
            Thumbnail = thumbnail ?? "";
            PolaroidWidthPx = polaroidWidthPx > 0 ? polaroidWidthPx : 0;
            PolaroidHeightPx = polaroidHeightPx > 0 ? polaroidHeightPx : 0;
            PolaroidBorderPx = polaroidBorderPx > 0 ? polaroidBorderPx : 0;
            PolaroidBottomBorderPx = polaroidBottomBorderPx > 0 ? polaroidBottomBorderPx : 0;
            PolaroidFrameColour = polaroidFrameColour;
        }

        /// <summary>Is there a Polaroid to draw beside the words?</summary>
        public bool HasPolaroid => PolaroidWidthPx > 0 && PolaroidHeightPx > 0;

        /// <summary>The Polaroid's lane beside the words, in cells: its width, then one cell of air.</summary>
        public int PolaroidLaneCells =>
            HasPolaroid ? (PolaroidWidthPx + NotebookKit.CellWidth - 1) / NotebookKit.CellWidth + 1 : 0;

        /// <summary>The fewest ruled lines the block takes, so the Polaroid never overhangs the next one.</summary>
        public int PolaroidRows =>
            HasPolaroid ? (PolaroidHeightPx + NotebookKit.Pitch - 1) / NotebookKit.Pitch : 0;
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

        /// <summary>The catch page's stub. Keyed like the others; lettered within
        /// <see cref="NotebookKit.ChipChars"/> (5) so the chip never clips it, which is why it reads
        /// CATCH and not CAUGHT.</summary>
        public const string TabCaught = "tab.caught";
        public const string LabelCaught = "CATCH";

        /// <summary>The Gear page's stub, beside the catch page. Lettered KIT and keyed apart because the
        /// shipped how-to pages already hold <c>tab.gear</c>, lettered GEAR (hand lining, setting a trap):
        /// two stubs reading GEAR would be two doors with one name.</summary>
        public const string TabGear = "tab.kit";
        public const string LabelGear = "KIT";

        /// <summary>The album's stub (save v28). Lettered within the chip's five.</summary>
        public const string TabPhotos = "tab.photos";
        public const string LabelPhotos = "PHOTO";

        /// <summary>Source-id prefix for a picture's block.</summary>
        public const string PhotoSourcePrefix = "photo.";

        /// <summary>One species' lines under its name: how many caught, the record weight (the size a
        /// catch carries: whole grams under a kilogram, kilograms to one decimal from one up), and how
        /// many were protected and put back (only when any were). Each fits the narrowest page
        /// (<see cref="NotebookKit.MinCols"/>) whole for four-digit counts and weights.</summary>
        public const string CaughtCountFormat = "Caught: {0}";
        public const string CaughtRecordPrefix = "Best: ";
        public const string CaughtReleasedFormat = "Let go: {0}";

        /// <summary>Source-id prefix for a species' block, so the presenter's selection can name it.</summary>
        public const string CaughtSourcePrefix = "caught.";

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
            IReadOnlyList<CaughtSpeciesEntry> caught = null,
            IReadOnlyList<NotebookGearHolding> gear = null,
            NotebookGearPageDef gearPage = null,
            IReadOnlyList<NotebookPhotoEntry> photos = null)
        {
            var tabs = new List<NotebookTab>
            {
                new NotebookTab(TabQuests, LabelQuests, NotebookPageKind.Quests,
                                QuestBlocks(quests, flags, cols, archived: false)),
                new NotebookTab(TabDone, LabelDone, NotebookPageKind.Done,
                                QuestBlocks(quests, flags, cols, archived: true)),
            };

            foreach (var tab in KnowledgeTabs(knowledge, cols)) tabs.Add(tab);

            // The catch page joins the fore-edge with her first catch and sits beside My Notes: it is her
            // own record, not a shipped page. Not Knowledge, so the trim below never drops it.
            if (caught != null && caught.Count > 0)
                tabs.Add(new NotebookTab(TabCaught, LabelCaught, NotebookPageKind.Caught,
                                         CatchBlocks(caught, cols)));

            // The gear page sits after it, also his own rather than shipped, also never trimmed.
            if (gear != null)
            {
                List<NotebookBlock> gearBlocks = NotebookGearPage.Blocks(gear, gearPage, cols);
                if (gearBlocks.Count > 0)
                    tabs.Add(new NotebookTab(TabGear, LabelGear, NotebookPageKind.Gear, gearBlocks));
            }

            // The album after them: his own record too, from his first kept picture, never trimmed.
            if (photos != null && photos.Count > 0)
                tabs.Add(new NotebookTab(TabPhotos, LabelPhotos, NotebookPageKind.Photos, PhotoBlocks(photos, cols)));

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
                if (quest == null || !QuestBoard.IsOpen(quest, flags)) continue;

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
        /// The catch page: one section per species in the order she first caught each (the record's
        /// order, so nothing here sorts). Each is the knowledge page's own drawing
        /// (<see cref="NotebookLayout.Section"/>): the name on the wide lane, then how many, the record
        /// weight, and how many went back when any were protected. Null or empty in, nothing out.
        /// </summary>
        public static List<NotebookBlock> CatchBlocks(IReadOnlyList<CaughtSpeciesEntry> caught, int cols)
        {
            var blocks = new List<NotebookBlock>();
            if (caught == null) return blocks;

            for (int i = 0; i < caught.Count; i++)
            {
                CaughtSpeciesEntry e = caught[i];
                if (string.IsNullOrEmpty(e.SpeciesId) || e.Count <= 0) continue;

                string head = string.IsNullOrEmpty(e.DisplayName) ? PlainName.FromId(e.SpeciesId) : e.DisplayName;
                var body = new List<string> { string.Format(Invariant, CaughtCountFormat, e.Count) };
                if (e.RecordWeightKg > 0f) body.Add(RecordText(e.RecordWeightKg));   // unknown: no line, never "0"
                if (e.ReleasedCount > 0)
                    body.Add(string.Format(Invariant, CaughtReleasedFormat, e.ReleasedCount));

                blocks.Add(NotebookLayout.Section(head, body, cols, CaughtSourcePrefix + e.SpeciesId));
            }

            return blocks;
        }

        /// <summary>The album page: one section per picture, in the order given (the page builder hands
        /// them newest first). Null or empty in, nothing out.</summary>
        public static List<NotebookBlock> PhotoBlocks(IReadOnlyList<NotebookPhotoEntry> photos, int cols)
        {
            var blocks = new List<NotebookBlock>();
            if (photos == null) return blocks;
            for (int i = 0; i < photos.Count; i++)
            {
                NotebookPhotoEntry p = photos[i];
                if (string.IsNullOrEmpty(p.Head)) continue;
                var body = new List<string>();
                if (p.Lines != null)
                    for (int l = 0; l < p.Lines.Count; l++)
                        if (!string.IsNullOrEmpty(p.Lines[l])) body.Add(p.Lines[l]);
                string source = PhotoSourcePrefix + p.Number.ToString(Invariant);
                blocks.Add(p.HasPolaroid ? PolaroidBlock(p, body, cols, source)
                                         : NotebookLayout.Section(p.Head, body, cols, source));
            }
            return blocks;
        }

        /// <summary>
        /// A picture's block with its Polaroid: the head and lines wrap in the lane past the Polaroid
        /// (<see cref="NotebookPhotoEntry.PolaroidLaneCells"/>), every row carries the picture's source id
        /// as its icon id (the presenter draws the Polaroid once, beside the first), and the block is padded
        /// to <see cref="NotebookPhotoEntry.PolaroidRows"/> lines.
        /// </summary>
        public static NotebookBlock PolaroidBlock(in NotebookPhotoEntry p, IReadOnlyList<string> body, int cols,
                                                  string source)
        {
            var rows = new List<NotebookRow>();
            int textCols = System.Math.Max(1, NotebookKit.TitleColsFor(cols) - p.PolaroidLaneCells - 1);
            foreach (string line in NotebookLayout.Wrap(p.Head, textCols))
                rows.Add(new NotebookRow(line, wide: true, hasBox: false, NotebookStepState.Todo, rows.Count > 0,
                                         source));
            if (body != null)
                for (int b = 0; b < body.Count; b++)
                    foreach (string line in NotebookLayout.Wrap(body[b], textCols))
                        rows.Add(new NotebookRow(line, wide: true, hasBox: false, NotebookStepState.Todo,
                                                 continuation: true, source));
            while (rows.Count < p.PolaroidRows)
                rows.Add(new NotebookRow("", wide: true, hasBox: false, NotebookStepState.Todo, continuation: true,
                                         source));
            return new NotebookBlock(NotebookBlockKind.Section, rows, source);
        }

        /// <summary>
        /// The record line for a positive weight, in the one weight reading every surface shares
        /// (<see cref="WeightFormat.Text"/>): whole grams under a kilogram, never below 1 g
        /// ("Best: 40 g"); a weight that rounds to 1000 g reads as a kilogram; kilograms to one decimal
        /// from one up ("Best: 7.5 kg"). Deterministic on every machine.
        /// </summary>
        public static string RecordText(float kg) => CaughtRecordPrefix + WeightFormat.Text(kg);

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
