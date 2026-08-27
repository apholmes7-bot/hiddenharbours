using System;
using System.Collections.Generic;

namespace HiddenHarbours.Core
{
    /// <summary>What a block of the book IS — the kit's four block kinds, verbatim.</summary>
    public enum NotebookBlockKind
    {
        /// <summary>A quest: title row, optional context line, then ordered steps.</summary>
        Quest,

        /// <summary>A knowledge page's section: running head, then body lines.</summary>
        Section,

        /// <summary>One of the player's own lines — prose, or a task carrying the shared checkbox.</summary>
        Note,

        /// <summary>The write-on row at the end of My Notes. One line, always last, never split.</summary>
        NewNote,
    }

    /// <summary>
    /// A step's state. <b>Derived, never stored</b> — <see cref="QuestBoard"/> reads it off the flags
    /// the world already keeps (#564), so this enum is a rendering fact and not a save surface.
    /// </summary>
    public enum NotebookStepState
    {
        Todo,
        Current,
        Done,
    }

    /// <summary>One ruled line, already wrapped and already assigned its lane.</summary>
    public readonly struct NotebookRow
    {
        /// <summary>The text on this line, wrapped to fit — never clipped.</summary>
        public readonly string Text;

        /// <summary>True when the line runs the TITLE lane (text lane + box lane), which is exactly two
        /// cells wider than a step because nothing is checked off beside it.</summary>
        public readonly bool Wide;

        /// <summary>Does this line carry the shared checkbox?</summary>
        public readonly bool HasBox;

        /// <summary>Meaningful only when <see cref="HasBox"/>.</summary>
        public readonly NotebookStepState State;

        /// <summary>True for the second and later lines of a wrapped step — the box is drawn once, on
        /// the first line, and the continuation is indented under it rather than re-boxed.</summary>
        public readonly bool Continuation;

        public NotebookRow(string text, bool wide, bool hasBox, NotebookStepState state, bool continuation)
        {
            Text = text ?? "";
            Wide = wide;
            HasBox = hasBox;
            State = state;
            Continuation = continuation;
        }
    }

    /// <summary>
    /// One thing on the page that is kept whole — a quest, a knowledge section, a note.
    ///
    /// <para><b>Blocks are the unit of flow, and that is the whole reason this type exists.</b> The
    /// kit's rule is that a quest which fits a leaf but not the remaining rows moves WHOLE to the
    /// facing leaf; splitting a quest's steps away from its title is the failure mode the rule exists
    /// to prevent. A row list alone could not express that, because nothing would say where one
    /// quest ended and the next began.</para>
    /// </summary>
    public sealed class NotebookBlock
    {
        public NotebookBlockKind Kind { get; }

        /// <summary>The rows this block occupies, in order, already wrapped for the column count it
        /// was measured at.</summary>
        public IReadOnlyList<NotebookRow> Rows { get; }

        /// <summary>Stable handle back to what produced this block — a quest id, a knowledge page id,
        /// or the note's index in her list. The presenter uses it for selection and the hit box; the
        /// layout never reads it.</summary>
        public string SourceId { get; }

        /// <summary>True when every step is done — the Done tab's population, and what the finished
        /// treatment is drawn over.</summary>
        public bool Archived { get; }

        public int Height => Rows.Count;

        public NotebookBlock(NotebookBlockKind kind, IReadOnlyList<NotebookRow> rows,
                             string sourceId = null, bool archived = false)
        {
            Kind = kind;
            Rows = rows ?? Array.Empty<NotebookRow>();
            SourceId = sourceId ?? "";
            Archived = archived;
        }
    }

    /// <summary>A block as placed on a leaf: which rows of it, starting at which ruled line.</summary>
    public readonly struct NotebookPlacement
    {
        public readonly NotebookBlock Block;

        /// <summary>Ruled line this placement starts on, 0-based from the top of the leaf.</summary>
        public readonly int Row;

        /// <summary>First row of the block drawn here. Non-zero only for the tail of a SPLIT block.</summary>
        public readonly int FromRow;

        /// <summary>How many of the block's rows are drawn here.</summary>
        public readonly int RowCount;

        /// <summary>True when this placement is only part of its block — the kit allows a split ONLY
        /// for a block that fits no empty leaf at all, "because the alternative is not drawing it".</summary>
        public bool IsSplit => FromRow > 0 || RowCount < Block.Height;

        public NotebookPlacement(NotebookBlock block, int row, int fromRow, int rowCount)
        {
            Block = block;
            Row = row;
            FromRow = fromRow;
            RowCount = rowCount;
        }
    }

    /// <summary>One leaf — half a spread, <c>lines</c> ruled rows deep.</summary>
    public sealed class NotebookLeaf
    {
        public List<NotebookPlacement> Placements { get; } = new List<NotebookPlacement>();

        /// <summary>Ruled lines actually used.</summary>
        public int Used { get; internal set; }
    }

    /// <summary>The open book: two leaves. A page turn moves to the next one.</summary>
    public sealed class NotebookSpread
    {
        public NotebookLeaf Left { get; } = new NotebookLeaf();
        public NotebookLeaf Right { get; } = new NotebookLeaf();
    }

    /// <summary>The integer scale and the cols/lines a room affords.</summary>
    public readonly struct NotebookFit
    {
        public readonly int Scale, Cols, Lines;

        /// <summary>True when the room could not hold even the smallest legal book, so the returned
        /// size is the minimum rather than something that fits.
        ///
        /// <para><b>Read this rather than clamping a returned rect yourself</b> — the kit's own
        /// instruction. A caller that clamps silently turns the flag into a lie about what is on
        /// screen.</para></summary>
        public readonly bool Clamped;

        public int SpreadWidth => NotebookKit.SpreadWidthFor(Cols) * Scale;
        public int SpreadHeight => NotebookKit.SpreadHeightFor(Lines) * Scale;

        public NotebookFit(int scale, int cols, int lines, bool clamped)
        {
            Scale = scale;
            Cols = cols;
            Lines = lines;
            Clamped = clamped;
        }
    }

    /// <summary>
    /// <b>THE BOOK'S LAYOUT — pure, engine-free, and the only place that decides what lands where.</b>
    ///
    /// <para>Every number here comes from <see cref="NotebookKit"/>, which is generated from the kit's
    /// own contract. Nothing in this file tunes a constant; if a rect looks wrong the fix is in the
    /// rig, then the contract, then <see cref="NotebookKit"/> — never here.</para>
    ///
    /// <para><b>⭐ OVERFLOW IS MORE PAGES.</b> There is no scroll state in the kit and there is none
    /// here. A block that will not fit the remaining rows moves whole to the facing leaf; when the
    /// spread is full the rest becomes the NEXT spread, and the answer for the player is a page turn.
    /// <see cref="Paginate"/> therefore returns a list and can never return a truncated one — the
    /// count of blocks in equals the count of blocks out, which is the property
    /// <c>NotebookLayoutTests</c> pins.</para>
    ///
    /// <para><b>Why a POCO and not a MonoBehaviour.</b> All of this has to be testable headlessly —
    /// the cloud lane cannot open Unity and the coordinator's suite runs in batch mode. Keeping the
    /// flow in plain C# means the hard part (wrapping, splitting, pagination) is proven without a
    /// canvas, and the presenter is left with nothing but drawing.</para>
    /// </summary>
    public static class NotebookLayout
    {
        /// <summary>
        /// The largest legal book that fits a room, at INTEGER scale only.
        ///
        /// <para><b>Integer scale is not a preference.</b> A fractional scale puts the pointing hand on
        /// a half cell, and the wobble stops reading as a hand and starts reading as a bug — the kit
        /// says so and the ladder is built on it.</para>
        ///
        /// <para>Search order matters: the biggest scale wins first, then the most columns, then the
        /// most lines. A bigger scale on a smaller book beats a smaller scale on a bigger one, because
        /// at these sizes legibility is the scarce thing, not row count.</para>
        /// </summary>
        public static NotebookFit Fit(int roomWidth, int roomHeight)
        {
            for (int scale = NotebookKit.MaxScale; scale >= 1; scale--)
            {
                for (int cols = NotebookKit.MaxCols; cols >= NotebookKit.MinCols; cols--)
                {
                    if (NotebookKit.SpreadWidthFor(cols) * scale > roomWidth) continue;

                    for (int lines = NotebookKit.MaxLines; lines >= NotebookKit.MinLines; lines--)
                    {
                        if (NotebookKit.SpreadHeightFor(lines) * scale > roomHeight) continue;
                        return new NotebookFit(scale, cols, lines, clamped: false);
                    }
                }
            }

            // Nothing legal fits. Return the smallest book and SAY so, rather than quietly drawing
            // something that runs off the screen.
            return new NotebookFit(1, NotebookKit.MinCols, NotebookKit.MinLines, clamped: true);
        }

        /// <summary>
        /// Monospace word wrap at <paramref name="cols"/> characters.
        ///
        /// <para><b>Wrapping, never clipping.</b> A step longer than the column count eats a second
        /// line — the copy budget (31 characters at the closest tier) is advice to the writing lane,
        /// not a limit this enforces. A word longer than a whole line is hard-broken, because the
        /// alternative is a line that overhangs the margin rule.</para>
        /// </summary>
        public static List<string> Wrap(string text, int cols)
        {
            var lines = new List<string>();
            if (cols <= 0) return lines;

            if (string.IsNullOrEmpty(text))
            {
                lines.Add("");
                return lines;
            }

            int i = 0;
            bool afterBreak = false;
            while (i < text.Length)
            {
                // Skip the run of spaces a previous break left behind — but ONLY after a break. The
                // spaces she typed at the START of a note are hers: #565's ruling is that her prose is
                // the one field whose only correct transformation is none, and an indent silently
                // swallowed here would be a normalisation by the back door.
                if (afterBreak)
                    while (i < text.Length && text[i] == ' ') i++;
                afterBreak = true;
                if (i >= text.Length) break;

                int remaining = text.Length - i;
                if (remaining <= cols)
                {
                    lines.Add(text.Substring(i));
                    break;
                }

                // Break at the last space that fits; if there is none, the word is longer than a line
                // and is hard-broken at the margin.
                int breakAt = -1;
                for (int k = i + cols; k > i; k--)
                {
                    if (text[k] == ' ') { breakAt = k; break; }
                }

                if (breakAt < 0)
                {
                    lines.Add(text.Substring(i, cols));
                    i += cols;
                }
                else
                {
                    lines.Add(text.Substring(i, breakAt - i));
                    i = breakAt + 1;
                }
            }

            if (lines.Count == 0) lines.Add("");
            return lines;
        }

        /// <summary>
        /// Build a quest's block: title (wide lane), optional context line (wide lane), then one boxed
        /// row per step with wrapped continuations indented under the box.
        /// </summary>
        public static NotebookBlock Quest(string title, string context,
                                          IReadOnlyList<(string Text, NotebookStepState State)> steps,
                                          int cols, string sourceId = null, bool archived = false)
        {
            int titleCols = NotebookKit.TitleColsFor(cols);
            var rows = new List<NotebookRow>();

            foreach (string line in Wrap(title, titleCols))
                rows.Add(new NotebookRow(line, wide: true, hasBox: false, NotebookStepState.Todo, continuation: false));

            if (!string.IsNullOrEmpty(context))
                foreach (string line in Wrap(context, titleCols))
                    rows.Add(new NotebookRow(line, wide: true, hasBox: false, NotebookStepState.Todo, continuation: false));

            if (steps != null)
            {
                foreach (var step in steps)
                {
                    var wrapped = Wrap(step.Text, cols);
                    for (int k = 0; k < wrapped.Count; k++)
                    {
                        // The box is drawn ONCE, on the step's first line. A continuation that re-boxed
                        // itself would read as two steps.
                        rows.Add(new NotebookRow(wrapped[k], wide: false, hasBox: k == 0,
                                                 step.State, continuation: k > 0));
                    }
                }
            }

            return new NotebookBlock(NotebookBlockKind.Quest, rows, sourceId, archived);
        }

        /// <summary>A knowledge section: running head on the wide lane, body on the text lane.</summary>
        public static NotebookBlock Section(string head, IReadOnlyList<string> body, int cols,
                                            string sourceId = null)
        {
            int titleCols = NotebookKit.TitleColsFor(cols);
            var rows = new List<NotebookRow>();

            foreach (string line in Wrap(head, titleCols))
                rows.Add(new NotebookRow(line, wide: true, hasBox: false, NotebookStepState.Todo, continuation: false));

            if (body != null)
            {
                foreach (string paragraph in body)
                    foreach (string line in Wrap(paragraph, cols))
                        rows.Add(new NotebookRow(line, wide: false, hasBox: false, NotebookStepState.Todo, continuation: false));
            }

            return new NotebookBlock(NotebookBlockKind.Section, rows, sourceId);
        }

        /// <summary>
        /// One of her own lines. A PROSE line carries no box and runs the title width; a TASK line
        /// carries the shared checkbox and sits at step width — two different drawings, per the kit,
        /// and not merely two states of one.
        /// </summary>
        public static NotebookBlock Note(string text, bool isTask, bool done, int cols, string sourceId = null)
        {
            int lane = isTask ? cols : NotebookKit.TitleColsFor(cols);
            var rows = new List<NotebookRow>();
            var wrapped = Wrap(text, lane);

            for (int k = 0; k < wrapped.Count; k++)
            {
                rows.Add(new NotebookRow(wrapped[k], wide: !isTask, hasBox: isTask && k == 0,
                                         done ? NotebookStepState.Done : NotebookStepState.Todo,
                                         continuation: k > 0));
            }

            return new NotebookBlock(NotebookBlockKind.Note, rows, sourceId);
        }

        /// <summary>The write-on row. Exactly one line, so it never splits and never overflows alone.</summary>
        public static NotebookBlock NewNote()
        {
            var rows = new List<NotebookRow>
            {
                new NotebookRow("", wide: false, hasBox: false, NotebookStepState.Todo, continuation: false),
            };
            return new NotebookBlock(NotebookBlockKind.NewNote, rows, "notes.new");
        }

        /// <summary>
        /// <b>The flow.</b> Blocks in, spreads out — and every block comes out somewhere.
        ///
        /// <para>Three cases, in the kit's own order:
        /// <list type="number">
        ///   <item>It fits the rows left on this leaf — place it.</item>
        ///   <item>It does not, but it would fit an EMPTY leaf — move it whole to the facing leaf
        ///   (turning the spread if that leaf was the right-hand one).</item>
        ///   <item>It fits no empty leaf at all — SPLIT it, "because the alternative is not drawing
        ///   it". This is the only split the kit permits, and it can only happen to a block taller
        ///   than a whole leaf.</item>
        /// </list></para>
        ///
        /// <para><b>Never truncates.</b> The returned spreads carry every row of every input block:
        /// there is no cap, no "first N", no scroll. That is the property worth pinning, because a
        /// silent cap here would read on screen exactly like a short list.</para>
        /// </summary>
        public static List<NotebookSpread> Paginate(IReadOnlyList<NotebookBlock> blocks, int cols, int lines)
        {
            var spreads = new List<NotebookSpread>();
            if (lines <= 0) return spreads;

            var spread = new NotebookSpread();
            spreads.Add(spread);
            NotebookLeaf leaf = spread.Left;
            bool onLeft = true;

            void TurnLeaf()
            {
                if (onLeft)
                {
                    leaf = spread.Right;
                    onLeft = false;
                }
                else
                {
                    spread = new NotebookSpread();
                    spreads.Add(spread);
                    leaf = spread.Left;
                    onLeft = true;
                }
            }

            if (blocks != null)
            {
                foreach (var block in blocks)
                {
                    if (block == null || block.Height == 0) continue;

                    int placedRows = 0;
                    while (placedRows < block.Height)
                    {
                        int room = lines - leaf.Used;
                        int left = block.Height - placedRows;

                        if (room <= 0)
                        {
                            TurnLeaf();
                            continue;
                        }

                        if (left <= room)
                        {
                            leaf.Placements.Add(new NotebookPlacement(block, leaf.Used, placedRows, left));
                            leaf.Used += left;
                            placedRows += left;
                            break;
                        }

                        // Does not fit here. If it would fit an empty leaf, move it whole rather than
                        // splitting — case 2. Otherwise it is taller than any leaf and must split.
                        if (block.Height <= lines && placedRows == 0)
                        {
                            TurnLeaf();
                            continue;
                        }

                        leaf.Placements.Add(new NotebookPlacement(block, leaf.Used, placedRows, room));
                        leaf.Used += room;
                        placedRows += room;
                        TurnLeaf();
                    }
                }
            }

            return spreads;
        }

        /// <summary>Total rows across both leaves of a spread — the occupancy readout, and what a test
        /// counts to prove nothing was dropped.</summary>
        public static int RowsOn(NotebookSpread spread)
        {
            if (spread == null) return 0;
            return spread.Left.Used + spread.Right.Used;
        }
    }
}
