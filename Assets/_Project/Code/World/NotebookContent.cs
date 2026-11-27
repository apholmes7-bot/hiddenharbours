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

    /// <summary>How the album draws its little Polaroids, all from the camera's def, kit pixels: the card's
    /// outer size and borders, its colour, the least air between two cards side by side, and the ruled
    /// lines left empty under each row of cards. A zero width draws no Polaroid.</summary>
    public readonly struct NotebookPolaroidStyle
    {
        public readonly int WidthPx;
        public readonly int HeightPx;
        public readonly int BorderPx;
        public readonly int BottomBorderPx;
        public readonly Color32 FrameColour;
        public readonly int MinGapPx;
        public readonly int AirRows;

        public NotebookPolaroidStyle(int widthPx, int heightPx, int borderPx, int bottomBorderPx, Color32 frameColour,
                                     int minGapPx, int airRows)
        {
            WidthPx = widthPx > 0 ? widthPx : 0;
            HeightPx = heightPx > 0 ? heightPx : 0;
            BorderPx = borderPx > 0 ? borderPx : 0;
            BottomBorderPx = bottomBorderPx > 0 ? bottomBorderPx : 0;
            FrameColour = frameColour;
            MinGapPx = minGapPx > 0 ? minGapPx : 0;
            AirRows = airRows > 0 ? airRows : 0;
        }

        public bool IsDrawn => WidthPx > 0 && HeightPx > 0;

        /// <summary>The ruled lines a card covers, so the caption starts on the line below it.</summary>
        public int CardRows => IsDrawn ? (HeightPx + NotebookKit.Pitch - 1) / NotebookKit.Pitch : 0;
    }

    /// <summary>One kept picture, as the album page sees it, already worded by <see cref="NotebookPhotoPage"/>:
    /// its little Polaroid (owner 2026-11-26: "yes photoalbum should save small pictures as little
    /// poloraids") with a short caption under it (what and how many, then the day and place), plus the
    /// longer head and lines a page without Polaroids letters instead. World's view shape, apart from
    /// Core's save row.</summary>
    public readonly struct NotebookPhotoEntry
    {
        public readonly int Number;
        public readonly string Head;
        public readonly IReadOnlyList<string> Lines;

        /// <summary>The caption's first line ("4 grey seals") and its second ("Day 1, St Peters").</summary>
        public readonly string Caption;
        public readonly string CaptionWhen;

        /// <summary>The stored thumbnail (<see cref="PhotoThumbnail"/>), or empty: the card is then drawn
        /// with no picture in it.</summary>
        public readonly string Thumbnail;

        public readonly NotebookPolaroidStyle Style;

        public NotebookPhotoEntry(int number, string head, IReadOnlyList<string> lines)
            : this(number, head, lines, "", "", "", default) { }

        public NotebookPhotoEntry(int number, string head, IReadOnlyList<string> lines, string caption,
                                  string captionWhen, string thumbnail, NotebookPolaroidStyle style)
        {
            Number = number;
            Head = head ?? "";
            Lines = lines;
            Caption = caption ?? "";
            CaptionWhen = captionWhen ?? "";
            Thumbnail = thumbnail ?? "";
            Style = style;
        }

        /// <summary>Is there a Polaroid to draw?</summary>
        public bool HasPolaroid => Style.IsDrawn;
    }

    /// <summary>
    /// Where a row of the album's Polaroids sits on a leaf (pure; the page builder and the presenter both
    /// read it, so what is measured is what is drawn). The title lane is split into equal slots, as many as
    /// hold a card with the least air beside it; each card is centred in its slot and its caption lettered
    /// under it across the slot's width, word-wrapped and never broken inside a word.
    /// </summary>
    public readonly struct NotebookPhotoGrid
    {
        public readonly int Across;
        public readonly int SlotWidthPx;
        public readonly int SlotCols;

        public NotebookPhotoGrid(int across, int slotWidthPx, int slotCols)
        {
            Across = across;
            SlotWidthPx = slotWidthPx;
            SlotCols = slotCols;
        }

        /// <summary>The grid for <paramref name="style"/> on a book <paramref name="cols"/> wide.</summary>
        public static NotebookPhotoGrid For(in NotebookPolaroidStyle style, int cols)
        {
            int laneW = NotebookKit.CellWidth * NotebookKit.TitleColsFor(cols) - 1;
            int across = style.WidthPx + style.MinGapPx > 0 ? laneW / (style.WidthPx + style.MinGapPx) : 1;
            if (across < 1) across = 1;
            int slot = laneW / across;
            int slotCols = (slot + 1) / NotebookKit.CellWidth;   // a run of n cells is 5n - 1 px wide
            return new NotebookPhotoGrid(across, slot, slotCols < 1 ? 1 : slotCols);
        }

        /// <summary>The caption's lines for <paramref name="photo"/>: its first line, then its second, each
        /// wrapped at the slot at spaces only. A word longer than the slot keeps a line of its own whole
        /// (never hard-broken): the content test holds the shipped words inside the narrowest slot.</summary>
        public List<string> CaptionLines(in NotebookPhotoEntry photo)
        {
            var lines = new List<string>();
            WrapWords(photo.Caption, SlotCols, lines);
            WrapWords(photo.CaptionWhen, SlotCols, lines);
            return lines;
        }

        /// <summary>
        /// Wrap at spaces only, appending to <paramref name="into"/>; nothing for an empty text. A line
        /// breaks after a comma first, so "Day 12, St Peters" in a narrow slot reads "Day 12," over
        /// "St Peters", never "Day 12, St" over "Peters"; a part longer than the line wraps word by word.
        /// </summary>
        public static void WrapWords(string text, int cols, List<string> into)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            var line = new System.Text.StringBuilder();
            string[] parts = text.Split(new[] { ", " }, System.StringSplitOptions.None);
            for (int p = 0; p < parts.Length; p++)
            {
                string group = parts[p].Trim();
                if (group.Length == 0) continue;
                if (p < parts.Length - 1) group += ",";   // every part but the last keeps its comma

                if (line.Length > 0 && line.Length + 1 + group.Length <= cols)
                {
                    line.Append(' ').Append(group);
                    continue;
                }
                if (line.Length > 0)
                {
                    into.Add(line.ToString());
                    line.Clear();
                }
                foreach (string word in group.Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries))
                {
                    if (line.Length > 0 && line.Length + 1 + word.Length > cols)
                    {
                        into.Add(line.ToString());
                        line.Clear();
                    }
                    if (line.Length > 0) line.Append(' ');
                    line.Append(word);
                }
            }
            if (line.Length > 0) into.Add(line.ToString());
        }
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

        /// <summary>The icon id every row of an album grid row carries: this, then the pictures' numbers
        /// joined by <see cref="PhotoGridSeparator"/> ("photo_grid.12_11"). The presenter reads the numbers
        /// back on the first row and draws each card and its caption once.</summary>
        public const string PhotoGridPrefix = "photo_grid.";
        public const char PhotoGridSeparator = '_';

        /// <summary>
        /// The album page, in the order given (the page builder hands them newest first). Pictures with a
        /// Polaroid are laid out as an album: straight cards in rows, as many across as
        /// <see cref="NotebookPhotoGrid.For"/> fits, each with its caption under it. One row of cards is one
        /// block, so a page turn moves whole rows of cards, the same flow as every other tab. A picture with
        /// no Polaroid to draw is a plain section. Null or empty in, nothing out.
        /// </summary>
        public static List<NotebookBlock> PhotoBlocks(IReadOnlyList<NotebookPhotoEntry> photos, int cols)
        {
            var blocks = new List<NotebookBlock>();
            if (photos == null) return blocks;
            var run = new List<NotebookPhotoEntry>();
            for (int i = 0; i < photos.Count; i++)
            {
                NotebookPhotoEntry p = photos[i];
                if (string.IsNullOrEmpty(p.Head)) continue;
                if (p.HasPolaroid)
                {
                    NotebookPhotoGrid grid = NotebookPhotoGrid.For(p.Style, cols);
                    if (run.Count > 0 && (run.Count >= grid.Across || !SameStyle(run[0].Style, p.Style)))
                    {
                        blocks.Add(PolaroidRowBlock(run, cols));
                        run.Clear();
                    }
                    run.Add(p);
                    continue;
                }
                if (run.Count > 0)
                {
                    blocks.Add(PolaroidRowBlock(run, cols));
                    run.Clear();
                }
                var body = new List<string>();
                if (p.Lines != null)
                    for (int l = 0; l < p.Lines.Count; l++)
                        if (!string.IsNullOrEmpty(p.Lines[l])) body.Add(p.Lines[l]);
                blocks.Add(NotebookLayout.Section(p.Head, body, cols, PhotoSourcePrefix + p.Number.ToString(Invariant)));
            }
            if (run.Count > 0) blocks.Add(PolaroidRowBlock(run, cols));
            return blocks;
        }

        /// <summary>
        /// One row of the album's Polaroids: as many ruled lines as the card covers, then the longest
        /// caption's lines, then the air. Every row is blank text (the presenter letters the captions in
        /// each card's own slot) and carries the grid icon id; the first is the block's start, where the
        /// cards are drawn. The block's source id is the first picture's.
        /// </summary>
        public static NotebookBlock PolaroidRowBlock(IReadOnlyList<NotebookPhotoEntry> row, int cols)
        {
            NotebookPolaroidStyle style = row[0].Style;
            NotebookPhotoGrid grid = NotebookPhotoGrid.For(style, cols);
            int captionRows = 0;
            var icon = new System.Text.StringBuilder(PhotoGridPrefix);
            for (int k = 0; k < row.Count; k++)
            {
                captionRows = System.Math.Max(captionRows, grid.CaptionLines(row[k]).Count);
                if (k > 0) icon.Append(PhotoGridSeparator);
                icon.Append(row[k].Number.ToString(Invariant));
            }
            int height = style.CardRows + captionRows + style.AirRows;
            string iconId = icon.ToString();
            var rows = new List<NotebookRow>(height);
            for (int r = 0; r < height; r++)
                rows.Add(new NotebookRow("", wide: true, hasBox: false, NotebookStepState.Todo, continuation: r > 0,
                                         iconId));
            return new NotebookBlock(NotebookBlockKind.Section, rows, PhotoSourcePrefix + row[0].Number.ToString(Invariant));
        }

        /// <summary>The picture numbers a grid icon id names, in order; empty for any other id.</summary>
        public static List<int> PhotoGridNumbers(string iconId)
        {
            var numbers = new List<int>();
            if (string.IsNullOrEmpty(iconId) || !iconId.StartsWith(PhotoGridPrefix, System.StringComparison.Ordinal))
                return numbers;
            foreach (string part in iconId.Substring(PhotoGridPrefix.Length).Split(PhotoGridSeparator))
                if (int.TryParse(part, System.Globalization.NumberStyles.Integer, Invariant, out int n)) numbers.Add(n);
            return numbers;
        }

        private static bool SameStyle(in NotebookPolaroidStyle a, in NotebookPolaroidStyle b) =>
            a.WidthPx == b.WidthPx && a.HeightPx == b.HeightPx && a.MinGapPx == b.MinGapPx && a.AirRows == b.AirRows;

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
