using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.UI
{
    /// <summary>
    /// <b>Where the interaction chooser's rows sit, what they say, and which one the pointer is on</b> —
    /// pure maths and strings, asserted headless (the <see cref="InteractPopupLayout"/> rule: a cloud
    /// agent can prove numbers, not pixels).
    ///
    /// <para><b>It is the popup, stacked.</b> Each row is exactly the popup's strip — same width, height,
    /// type size and inset — and the BOTTOM row stands where the popup itself would stand (helm footprint
    /// and quest note already stepped around, <see cref="InteractPopupLayout.PanelAnchoredPosition(in HelmFootprintArea, Rect, float, float)"/>).
    /// The rest climb above it, row 0 at the top. The popup is suppressed while the list is up (the list
    /// holds <see cref="InteractionGate"/>), so the list takes its corner rather than joining it: the
    /// same voice, saying a few lines instead of one.</para>
    ///
    /// <para>Reference units, top-left convention, as the popup: x right, y DOWN (negative).</para>
    /// </summary>
    public static class InteractChooserLayout
    {
        /// <summary>One row's height: the popup's.</summary>
        public const float RowHeight = InteractPopupLayout.PanelHeight;

        /// <summary>The air between two rows, reference units. A hair, so the rows read as one list and
        /// each strip is still its own target for a click.</summary>
        public const float RowGap = 4f;

        /// <summary>The distance from one row's top to the next one's.</summary>
        public const float RowPitch = RowHeight + RowGap;

        /// <summary>
        /// Row <paramref name="index"/>'s anchored position (top-left pivot) in a list of
        /// <paramref name="count"/>, given where the bottom row stands. Row 0 is the top one.
        /// </summary>
        public static Vector2 RowAnchoredPosition(Vector2 bottomRow, int index, int count)
            => new Vector2(bottomRow.x, bottomRow.y + (count - 1 - index) * RowPitch);

        /// <summary>
        /// Row <paramref name="index"/> in SCREEN pixels (bottom-left origin, y up), given the bottom
        /// row's screen rect and the canvas scale.
        /// </summary>
        public static Rect RowScreenRect(Rect bottomRowPx, int index, int count, float scale)
        {
            Rect r = bottomRowPx;
            r.y += (count - 1 - index) * RowPitch * scale;
            return r;
        }

        /// <summary>
        /// Which row the pointer is on, or -1: the pure half of "hover selects, click picks". The gaps
        /// between rows belong to no row, so a click in the seam does nothing rather than guessing.
        /// </summary>
        public static int HitRow(Vector2 pointPx, Rect bottomRowPx, int count, float scale)
        {
            for (int i = 0; i < count; i++)
                if (RowScreenRect(bottomRowPx, i, count, scale).Contains(pointPx)) return i;
            return -1;
        }

        /// <summary>
        /// The words on a row: its number key, the thing's own verb, and — for a row that cannot be
        /// picked — why, in a word. Built only when the list opens (never per frame), and fitted to the
        /// popup's one-line cap.
        /// </summary>
        public static string RowText(int index, string label, InteractChoiceBlock block)
        {
            string number = (index + 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
            string reason = ReasonFor(block);
            string text = reason == null
                ? number + "  " + label
                : number + "  " + label + " " + InteractChooserStrings.ReasonOpen + reason
                  + InteractChooserStrings.ReasonClose;
            return InteractPopupLayout.Fit(text);
        }

        /// <summary>The one word for why a row is greyed, or null for a pickable row.</summary>
        public static string ReasonFor(InteractChoiceBlock block)
        {
            switch (block)
            {
                case InteractChoiceBlock.Occupied: return InteractChooserStrings.Occupied;
                case InteractChoiceBlock.Unavailable: return InteractChooserStrings.Unavailable;
                default: return null;
            }
        }
    }

    /// <summary>
    /// The chooser's own words — the reason tags on a greyed row. One place, so localisation finds them
    /// together; the row's main words are each thing's own <see cref="IInteractable.VerbLabel"/>.
    /// </summary>
    public static class InteractChooserStrings
    {
        /// <summary>Someone is in it (a bunk, a bed).</summary>
        public const string Occupied = "taken";

        /// <summary>The thing said this one action is not on offer right now.</summary>
        public const string Unavailable = "not now";

        public const string ReasonOpen = "(";
        public const string ReasonClose = ")";
    }
}
