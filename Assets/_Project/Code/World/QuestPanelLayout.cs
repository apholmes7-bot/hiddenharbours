using System.Collections.Generic;
using UnityEngine;

namespace HiddenHarbours.World
{
    /// <summary>The corner note's size, chosen for a screen. Kit pixels, integer scale.</summary>
    public readonly struct QuestPanelFit
    {
        /// <summary>Whole-number magnification. Never fractional — see <see cref="QuestPanelLayout"/>.</summary>
        public readonly int Scale;

        /// <summary>Characters across the writing area.</summary>
        public readonly int Cols;

        /// <summary>Ruled lines the note is tall — the rows the text actually needs, not a constant.</summary>
        public readonly int Lines;

        /// <summary>
        /// True when the text did not fit in <see cref="QuestPanelLayout.MaxLines"/> and rows were
        /// dropped. Reported rather than swallowed: a hint that silently loses its last line reads as a
        /// writing mistake, and the writing lane has no way to see it otherwise.
        /// </summary>
        public readonly bool Overflowed;

        public QuestPanelFit(int scale, int cols, int lines, bool overflowed)
        {
            Scale = scale; Cols = cols; Lines = lines; Overflowed = overflowed;
        }

        /// <summary>The note's width in KIT pixels (before <see cref="Scale"/>).</summary>
        public int WidthPx => NotebookKit.PageWidthFor(Cols);

        /// <summary>The note's height in KIT pixels (before <see cref="Scale"/>).</summary>
        public int HeightPx => NotebookKit.PageHeightFor(Lines);

        /// <summary>What it actually occupies on screen.</summary>
        public int ScreenWidthPx => WidthPx * Scale;

        /// <summary>What it actually occupies on screen.</summary>
        public int ScreenHeightPx => HeightPx * Scale;
    }

    /// <summary>
    /// <b>Where the current quest sits, and how big</b> — a leaf off the notebook, pinned in the
    /// LOWER-RIGHT corner (the owner's 2026-08-22 diegetic ruling).
    ///
    /// <para>It replaced a bottom-CENTRE banner of plain white text. The banner was the last thing on
    /// screen that told the player something in the game's own voice while pretending to be the game's
    /// own UI; drawn as a page in her hand, in the notebook's stock and ink, it says the same words as
    /// something she wrote down. The corner is chosen and not merely free: bottom-centre is the nav
    /// cluster's and the helm dash's, and the note must never be the thing sitting on the compass.</para>
    ///
    /// <para><b>Integer scale only</b>, for the same reason the book has it — a pixel face on a
    /// fractional scale shimmers, and the note is set in the book's baked face.</para>
    ///
    /// <para>Pure: no Unity objects, no screen reads. <see cref="QuestPanelPresenter"/> holds the
    /// drawing, so every number here is provable headlessly.</para>
    /// </summary>
    public static class QuestPanelLayout
    {
        /// <summary>
        /// Characters across. 30 holds the longest shipped nudge — "Walk the bared sandbar east to Nine
        /// Mile Creek before the tide floods it back.", 77 characters — in three lines, with a fourth in
        /// hand. Wider would start to read as a paragraph rather than a note.
        /// </summary>
        public const int Cols = 30;

        /// <summary>
        /// The most ruled lines the note may grow to. A corner note that runs past four lines has
        /// stopped being a glance and become something to read, which is what the book itself is for.
        /// </summary>
        public const int MaxLines = 4;

        /// <summary>How much of the screen the note may occupy, width and height. Small on purpose: the
        /// picture is the game.</summary>
        public const float RoomWidthFraction = 0.34f;
        public const float RoomHeightFraction = 0.30f;

        /// <summary>The inset from the screen corner, in the note's OWN pixels — so it sits eight of its
        /// own pixels off the edge at every scale, rather than drifting to the rim on a big screen.</summary>
        public const int MarginPx = 8;

        /// <summary>
        /// The note's lines for a hint, wrapped at <see cref="Cols"/> and capped at
        /// <see cref="MaxLines"/>. Empty (never null) for empty text — "nothing to say" is a real answer
        /// and the presenter draws nothing at all for it.
        /// </summary>
        public static List<string> Rows(string text)
        {
            if (string.IsNullOrEmpty(text)) return new List<string>();

            List<string> rows = NotebookLayout.Wrap(text, Cols);
            if (rows.Count > MaxLines) rows.RemoveRange(MaxLines, rows.Count - MaxLines);
            return rows;
        }

        /// <summary>
        /// The largest note that fits its corner of the screen at whole magnification.
        ///
        /// <para>Search order is the book's: biggest scale first, because at these sizes legibility is
        /// the scarce thing. A screen too small for even scale 1 still gets scale 1 — a note slightly
        /// over its allowance is better than no quest at all, and the fractions are a budget rather than
        /// a hard edge.</para>
        /// </summary>
        public static QuestPanelFit Fit(int rowCount, int screenW, int screenH)
        {
            bool overflowed = rowCount > MaxLines;
            int lines = Mathf.Clamp(rowCount, 1, MaxLines);

            int roomW = Mathf.FloorToInt(Mathf.Max(0, screenW) * RoomWidthFraction);
            int roomH = Mathf.FloorToInt(Mathf.Max(0, screenH) * RoomHeightFraction);

            int w = NotebookKit.PageWidthFor(Cols);
            int h = NotebookKit.PageHeightFor(lines);

            for (int scale = NotebookKit.MaxScale; scale > 1; scale--)
            {
                if (w * scale <= roomW && h * scale <= roomH)
                    return new QuestPanelFit(scale, Cols, lines, overflowed);
            }

            return new QuestPanelFit(1, Cols, lines, overflowed);
        }

        /// <summary>
        /// Where the note's LOWER-RIGHT corner goes, for a rect anchored and pivoted to the screen's
        /// lower-right: left by the margin, up by the margin. Both in screen pixels, so the margin
        /// scales with the note.
        /// </summary>
        public static Vector2 AnchoredPosition(in QuestPanelFit fit)
            => new Vector2(-MarginPx * fit.Scale, MarginPx * fit.Scale);
    }
}
