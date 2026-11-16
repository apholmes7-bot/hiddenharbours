using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.UI
{
    /// <summary>
    /// <b>Where the interaction chooser's rows sit, how wide they are, what they say, and which one the
    /// pointer is on</b> — pure maths and strings, asserted headless (the <see cref="InteractPopupLayout"/>
    /// rule: a cloud agent can prove numbers, not pixels).
    ///
    /// <para><b>It is the popup, stacked.</b> Each row is the popup's strip — same height, type size and
    /// inset — and the BOTTOM row stands where the popup itself would stand (helm footprint and quest note
    /// already stepped around, <see cref="InteractPopupLayout.PanelAnchoredPosition(in HelmFootprintArea, Rect, float, float)"/>).
    /// The rest climb above it, row 0 at the top. The popup is suppressed while the list is up (the list
    /// holds <see cref="InteractionGate"/>), so the list takes its corner rather than joining it.</para>
    ///
    /// <para><b>Width follows the words</b> (2026-11-15 editor look: "Fill up with two-stroke mix" was cut
    /// at the popup's width). The list is as wide as its longest row needs — never narrower than the popup,
    /// never wider than <see cref="MaxListWidth"/> — and grows LEFTWARD from the popup's right edge, which
    /// is the edge that hugs the screen. Only past that cap is a row cut, with the popup's ellipsis.</para>
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

        /// <summary>The widest the list grows, reference units: room for <see cref="MaxRowCharacters"/>
        /// at the popup's type size, and still a corner note (a bit over a quarter of the 1920 reference
        /// width), not a banner.</summary>
        public const float MaxListWidth = 520f;

        /// <summary>The selected row's leading marker bar, reference units wide (inside the type's inset,
        /// so it never pushes the words).</summary>
        public const float MarkerWidth = 6f;

        /// <summary>The longest row drawn, number and reason included. Derived as the popup's own cap is:
        /// <see cref="MaxListWidth"/> minus both insets is 492 units, at a shade under
        /// <see cref="CharUnits"/> per character, with a character in hand.</summary>
        public const int MaxRowCharacters = 48;

        /// <summary>The popup's own per-character estimate at its type size (see
        /// <see cref="InteractPopupLayout.MaxCharacters"/>). A floor under the measured width, so a list
        /// is never sized narrower than its words even where no font can be measured (headless).</summary>
        public const float CharUnits = 10f;

        /// <summary>The estimated drawn width of <paramref name="text"/>, reference units.</summary>
        public static float EstimateTextUnits(string text)
            => string.IsNullOrEmpty(text) ? 0f : text.Length * CharUnits;

        /// <summary>
        /// The list's width for its longest row's drawn width (reference units): the words plus both
        /// insets, rounded up, clamped to [<see cref="InteractPopupLayout.PanelWidth"/>,
        /// <see cref="MaxListWidth"/>].
        /// </summary>
        public static float ListWidth(float longestTextUnits)
            => Mathf.Clamp(Mathf.Ceil(longestTextUnits + 2f * InteractPopupLayout.TextPadX),
                           InteractPopupLayout.PanelWidth, MaxListWidth);

        /// <summary>The type's box inside a row of <paramref name="width"/>.</summary>
        public static Vector2 TextSize(float width)
            => new Vector2(width - 2f * InteractPopupLayout.TextPadX,
                           InteractPopupLayout.PanelHeight - 2f * InteractPopupLayout.TextPadY);

        /// <summary>
        /// Row <paramref name="index"/>'s anchored position (top-left pivot) in a list of
        /// <paramref name="count"/>, given where the bottom row stands at the popup's width. Row 0 is the
        /// top one.
        /// </summary>
        public static Vector2 RowAnchoredPosition(Vector2 bottomRow, int index, int count)
            => RowAnchoredPosition(bottomRow, index, count, InteractPopupLayout.PanelWidth);

        /// <summary>
        /// As <see cref="RowAnchoredPosition(Vector2, int, int)"/> for a list <paramref name="width"/>
        /// wide: the right edge stays the popup's, the list grows left, and never past the screen's
        /// <see cref="InteractPopupLayout.MarginX"/>.
        /// </summary>
        public static Vector2 RowAnchoredPosition(Vector2 bottomRow, int index, int count, float width)
            => new Vector2(LeftEdge(bottomRow.x, width), bottomRow.y + (count - 1 - index) * RowPitch);

        private static float LeftEdge(float popupLeft, float width)
            => Mathf.Max(InteractPopupLayout.MarginX, popupLeft + InteractPopupLayout.PanelWidth - width);

        /// <summary>
        /// Row <paramref name="index"/> in SCREEN pixels (bottom-left origin, y up), given the bottom
        /// row's screen rect at the popup's width and the canvas scale.
        /// </summary>
        public static Rect RowScreenRect(Rect bottomRowPx, int index, int count, float scale)
            => RowScreenRect(bottomRowPx, index, count, scale, InteractPopupLayout.PanelWidth);

        /// <summary>As <see cref="RowScreenRect(Rect, int, int, float)"/> for a list
        /// <paramref name="width"/> reference units wide — the same rule as the drawn rows.</summary>
        public static Rect RowScreenRect(Rect bottomRowPx, int index, int count, float scale, float width)
        {
            Rect r = bottomRowPx;
            float s = scale > 0f ? scale : 1f;
            r.x = LeftEdge(bottomRowPx.x / s, width) * s;
            r.width = width * s;
            r.y += (count - 1 - index) * RowPitch * s;
            return r;
        }

        /// <summary>
        /// Which row the pointer is on, or -1: the pure half of "hover selects, click picks". The gaps
        /// between rows belong to no row, so a click in the seam does nothing rather than guessing.
        /// </summary>
        public static int HitRow(Vector2 pointPx, Rect bottomRowPx, int count, float scale)
            => HitRow(pointPx, bottomRowPx, count, scale, InteractPopupLayout.PanelWidth);

        /// <summary>As <see cref="HitRow(Vector2, Rect, int, float)"/> for a list <paramref name="width"/>
        /// wide.</summary>
        public static int HitRow(Vector2 pointPx, Rect bottomRowPx, int count, float scale, float width)
        {
            for (int i = 0; i < count; i++)
                if (RowScreenRect(bottomRowPx, i, count, scale, width).Contains(pointPx)) return i;
            return -1;
        }

        /// <summary>
        /// The words on a row: its number key, the thing's own verb, and — for a row that cannot be
        /// picked — why, in a word. Built only when the list opens (never per frame), on one line, and cut
        /// with the popup's ellipsis only past <see cref="MaxRowCharacters"/>.
        /// </summary>
        public static string RowText(int index, string label, InteractChoiceBlock block)
        {
            string number = (index + 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
            string reason = ReasonFor(block);
            string text = reason == null
                ? number + "  " + label
                : number + "  " + label + " " + InteractChooserStrings.ReasonOpen + reason
                  + InteractChooserStrings.ReasonClose;
            return FitRow(text);
        }

        /// <summary>One line, at most <see cref="MaxRowCharacters"/> glyphs including the ellipsis.</summary>
        public static string FitRow(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            if (text.IndexOf('\n') >= 0 || text.IndexOf('\r') >= 0) text = text.Replace('\n', ' ').Replace('\r', ' ');
            if (text.Length <= MaxRowCharacters) return text;
            string ellipsis = InteractPopupLayout.Ellipsis;
            return text.Substring(0, Mathf.Max(0, MaxRowCharacters - ellipsis.Length)) + ellipsis;
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
    /// <b>The chooser's look</b> — every colour it paints, from the shell's own palette
    /// (<see cref="PaperUi"/>), so the list is the game's ink and chalk and nothing new.
    ///
    /// <para><b>Selection is unmistakable</b> (2026-11-15 editor look: the old selected strip was 2-4
    /// luminance units off the rest, with the ground showing through). Every row is the shell's near-
    /// opaque frame ink, so nothing behind the list reads through it; the selected row is INVERTED —
    /// a chalk strip with frame-ink words — and carries a warm leading bar, the shell's one accent.</para>
    /// </summary>
    public static class InteractChooserStyle
    {
        /// <summary>An unselected row's strip: the shell's frame ink, near-opaque.</summary>
        public static Color RowInk => PaperUi.FrameInk;

        /// <summary>The selected row's strip: chalk, the inverse of every other row.</summary>
        public static Color SelectedRowInk => PaperUi.Chalk;

        /// <summary>A pickable row's words.</summary>
        public static Color Words => PaperUi.Chalk;

        /// <summary>A greyed row's words.</summary>
        public static Color WordsGreyed => PaperUi.ChalkFaint;

        /// <summary>The selected row's words: frame ink on the chalk strip.</summary>
        public static Color SelectedWords => new Color(PaperUi.FrameInk.r, PaperUi.FrameInk.g, PaperUi.FrameInk.b, 1f);

        /// <summary>The selected row's leading bar: the shell's warm accent.</summary>
        public static Color Marker => PaperUi.WarnInk;

        /// <summary>The strip for a row.</summary>
        public static Color StripFor(bool selected) => selected ? SelectedRowInk : RowInk;

        /// <summary>The words' colour for a row.</summary>
        public static Color WordsFor(bool selected, bool enabled)
            => selected ? SelectedWords : enabled ? Words : WordsGreyed;
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
