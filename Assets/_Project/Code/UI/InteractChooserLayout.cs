using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.UI
{
    /// <summary>
    /// <b>Where the interaction chooser sits, how big it is, what each row says, and which row the
    /// pointer is on</b> — pure maths and strings, asserted headless (the <see cref="InteractPopupLayout"/>
    /// rule: a cloud agent can prove numbers, not pixels).
    ///
    /// <para><b>It is the speech bubble's option list</b> (owner ruling: "the same design language as our
    /// notebook and chat bubbles, so its cohesive with the game"). Every measure is the bubble kit's own,
    /// in kit pixels, times <see cref="DialogueBubbleKit.ArtScale"/> — the HUD's reference canvas is the
    /// same 1280x720 the bubble is drawn on, so one kit pixel here is one kit pixel there, and the two
    /// lists cannot drift apart: rows <see cref="DialogueBubbleKit.OptionRowHeight"/> tall, the words at
    /// <see cref="DialogueBubbleKit.OptionTextX"/>, the panel's top/bottom/right insets, the pill's inset
    /// (<c>PanelInsetL − 1</c>, as <c>DialoguePresenter</c> places it) and the hand halfway into the
    /// text gutter. The face is <see cref="HarbourType"/>, monospace, so a row's width is its length.</para>
    ///
    /// <para><b>Placement is the popup's.</b> The panel's bottom-right corner is the popup's
    /// (<see cref="InteractPopupLayout.PanelAnchoredPosition(in HelmFootprintArea, Rect, float, float)"/>,
    /// helm footprint and quest note already stepped around); the panel grows up and LEFT from it. The
    /// popup is suppressed while the list is up (the list holds <see cref="InteractionGate"/>), so the list
    /// takes its corner rather than joining it.</para>
    ///
    /// <para><b>Width follows the words</b> (2026-11-15 editor look: "Fill up with two-stroke mix" was cut
    /// at the popup's width), never narrower than the popup, never wider than the kit's widest line
    /// (<see cref="DialogueBubbleKit.MaxCols"/>). Only past that is a row cut.</para>
    ///
    /// <para>Reference units, top-left convention, as the popup: x right, y DOWN (negative).</para>
    /// </summary>
    public static class InteractChooserLayout
    {
        /// <summary>Reference units per kit pixel: the bubble's own art scale, on the same reference
        /// canvas (<see cref="HudBandLayout.RefW"/> x <see cref="HudBandLayout.RefH"/>).</summary>
        public const int Kit = DialogueBubbleKit.ArtScale;

        /// <summary>One row: the bubble's option row.</summary>
        public const float RowHeight = DialogueBubbleKit.OptionRowHeight * Kit;

        /// <summary>The panel's padding above the first row and below the last, and right of the words.</summary>
        public const float InsetTop = DialogueBubbleKit.OptionInsetT * Kit;
        public const float InsetBottom = DialogueBubbleKit.OptionInsetB * Kit;
        public const float InsetRight = DialogueBubbleKit.OptionInsetR * Kit;

        /// <summary>Where the words start, from the panel's left edge: the kit's text origin, whose gutter
        /// holds the hand.</summary>
        public const float TextX = DialogueBubbleKit.OptionTextX * Kit;

        /// <summary>
        /// Where a row's line of type starts, down from the row's top: the kit's label origin
        /// (<see cref="DialogueBubbleKit.OptionTextY"/>, from the bubble's top) less the bubble's top inset
        /// (<see cref="DialogueBubbleKit.OptionInsetT"/>), so the words are set UPPER-left a whole number
        /// of kit pixels into their row, as the speech bubble's body is, never centred. Centring the
        /// face's 10-px line in a 13-px row puts it 1.5 kit pixels down. That is half a kit pixel off the
        /// grid, a quarter pixel at 1080p's 1.5x, and the likely source of the broken glyphs in the
        /// 2026-11-15 look.
        /// </summary>
        public const float TextDrop = (DialogueBubbleKit.OptionTextY - DialogueBubbleKit.OptionInsetT) * Kit;

        /// <summary>The hand's centre, from the panel's left edge: halfway into the gutter, as the bubble.</summary>
        public const float CursorX = DialogueBubbleKit.OptionTextX * Kit * 0.5f;

        /// <summary>The gold pill's inset from each side of the panel (the bubble's <c>PanelInsetL − 1</c>).</summary>
        public const float PillInset = (DialogueBubbleKit.PanelInsetL - 1) * Kit;

        /// <summary>The face's size: <see cref="HarbourType.GlyphHeight"/> x the art scale draws the baked
        /// glyphs one kit pixel per glyph pixel (the derivation <c>BubbleFace.FontSize</c> documents).</summary>
        public const int FontSize = HarbourType.GlyphHeight * Kit;

        /// <summary>One character's advance, reference units — monospace by construction.</summary>
        public const float AdvanceUnits = HarbourType.Advance * Kit;

        /// <summary>The fallback hand's side, kit pixels, when the kit's art is not linked (a square of
        /// ink in the gutter — the same job, no new art).</summary>
        public const int FallbackCursorKitPixels = 4;

        /// <summary>The longest row drawn, number and reason included: the kit's widest line.</summary>
        public const int MaxRowCharacters = DialogueBubbleKit.MaxCols;

        /// <summary>The narrowest panel: the popup's width, so a short list still fills its corner.</summary>
        public const float MinPanelWidth = InteractPopupLayout.PanelWidth;

        /// <summary>The widest panel: the kit's widest line of words plus the gutter and right inset.</summary>
        public const float MaxPanelWidth = TextX + MaxRowCharacters * AdvanceUnits + Slack + InsetRight;

        /// <summary>Spare width after the longest row (the popup's slack, so both say it once): the
        /// 2026-11-15 look measured the pump verb at 450 of 450 units.</summary>
        public const float Slack = InteractPopupLayout.SlackKitPixels * Kit;

        /// <summary>The drawn width of <paramref name="text"/> in the face, reference units.</summary>
        public static float EstimateTextUnits(string text)
            => string.IsNullOrEmpty(text) ? 0f : text.Length * AdvanceUnits;

        /// <summary>
        /// The panel's width for its longest row's drawn width (reference units): gutter + words + right
        /// inset, rounded UP to whole kit pixels (so the 9-slice lands on the grid), clamped to
        /// [<see cref="MinPanelWidth"/>, <see cref="MaxPanelWidth"/>].
        /// </summary>
        public static float PanelWidth(float longestTextUnits)
        {
            float raw = Mathf.Clamp(TextX + longestTextUnits + Slack + InsetRight, MinPanelWidth, MaxPanelWidth);
            return Mathf.Ceil(raw / Kit - 0.001f) * Kit;
        }

        /// <summary>The panel's height for <paramref name="count"/> rows.</summary>
        public static float PanelHeight(int count) => InsetTop + Mathf.Max(0, count) * RowHeight + InsetBottom;

        /// <summary>The words' box inside a panel of <paramref name="panelWidth"/>: from
        /// <see cref="TextDrop"/> to the row's foot.</summary>
        public static Vector2 TextSize(float panelWidth)
            => new Vector2(panelWidth - TextX - InsetRight, RowHeight - TextDrop);

        /// <summary>Row <paramref name="index"/>'s words' anchored position inside the panel (top-left pivot).</summary>
        public static Vector2 TextAnchoredPosition(int index) => new Vector2(TextX, RowTop(index) - TextDrop);

        /// <summary>Row <paramref name="index"/>'s top, from the panel's top (y down, so negative).</summary>
        public static float RowTop(int index) => -(InsetTop + index * RowHeight);

        /// <summary>
        /// The panel's anchored position (top-left pivot), given where the popup would stand: its bottom-right
        /// corner on the popup's, never left of the screen's <see cref="InteractPopupLayout.MarginX"/>.
        /// </summary>
        public static Vector2 PanelAnchoredPosition(Vector2 popup, int count, float panelWidth)
            => new Vector2(LeftEdge(popup.x, panelWidth),
                           popup.y - InteractPopupLayout.PanelHeight + PanelHeight(count));

        private static float LeftEdge(float popupLeft, float width)
            => Mathf.Max(InteractPopupLayout.MarginX, popupLeft + InteractPopupLayout.PanelWidth - width);

        /// <summary>
        /// Row <paramref name="index"/> in SCREEN pixels (bottom-left origin, y up), given the popup's screen
        /// rect and the canvas scale: the full width of the panel, one row tall — the same rule as the drawn
        /// rows, and contiguous, so the pointer is always on one row or off the list.
        /// </summary>
        public static Rect RowScreenRect(Rect popupPx, int index, int count, float scale, float panelWidth)
        {
            float s = scale > 0f ? scale : 1f;
            return new Rect(LeftEdge(popupPx.x / s, panelWidth) * s,
                            popupPx.y + (InsetBottom + (count - 1 - index) * RowHeight) * s,
                            panelWidth * s, RowHeight * s);
        }

        /// <summary>Which row the pointer is on, or -1 (off the rows: the panel's insets and outside).</summary>
        public static int HitRow(Vector2 pointPx, Rect popupPx, int count, float scale, float panelWidth)
        {
            for (int i = 0; i < count; i++)
                if (RowScreenRect(popupPx, i, count, scale, panelWidth).Contains(pointPx)) return i;
            return -1;
        }

        /// <summary>
        /// The words on a row: its number key, the thing's own verb, and — for a row that cannot be
        /// picked — why, in a word. Built only when the list opens (never per frame), on one line, cut with
        /// <see cref="InteractChooserStrings.Cut"/> only past <see cref="MaxRowCharacters"/>.
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

        /// <summary>One line, at most <see cref="MaxRowCharacters"/> glyphs including the cut mark.</summary>
        public static string FitRow(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            if (text.IndexOf('\n') >= 0 || text.IndexOf('\r') >= 0) text = text.Replace('\n', ' ').Replace('\r', ' ');
            if (text.Length <= MaxRowCharacters) return text;
            string cut = InteractChooserStrings.Cut;
            return text.Substring(0, Mathf.Max(0, MaxRowCharacters - cut.Length)) + cut;
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
    /// <b>The chooser's ink</b> — every colour it paints, from the notebook's palette
    /// (<see cref="NotebookInk"/>, which IS the bubble kit's palette), and nothing new.
    ///
    /// <para><b>Selection is the bubble's</b>: the kit's gold pill behind the row and its pointing hand
    /// beside it — shapes, not a shade. The selected words are the STRONGEST ink on the panel (the 2026-11-15
    /// look found dark words reading thin on a light bar; here they are the heaviest ink there is), the
    /// other rows plain ink, a row that cannot be picked faint ink with its reason.</para>
    ///
    /// <para><b>Tints.</b> With the kit's art linked (<see cref="BubbleKitLink"/>) the sprites carry their
    /// own colour and are drawn untinted, as the bubble draws them; without it, the panel is paper and the
    /// pill is gold — the bubble's own unbaked fallback.</para>
    /// </summary>
    public static class InteractChooserStyle
    {
        /// <summary>The panel's tint.</summary>
        public static Color PanelTint(bool hasArt) => hasArt ? Color.white : NotebookInk.Paper;

        /// <summary>The selected-row pill's tint.</summary>
        public static Color PillTint(bool hasArt) => hasArt ? Color.white : NotebookInk.Gold;

        /// <summary>The hand's tint (the fallback square is strong ink).</summary>
        public static Color CursorTint(bool hasArt) => hasArt ? Color.white : NotebookInk.InkStrong;

        /// <summary>A pickable row's words.</summary>
        public static Color Words => NotebookInk.Ink;

        /// <summary>A greyed row's words.</summary>
        public static Color WordsGreyed => NotebookInk.InkFaint;

        /// <summary>The selected row's words: the strongest ink, on the gold.</summary>
        public static Color SelectedWords => NotebookInk.InkStrong;

        /// <summary>The words' colour for a row.</summary>
        public static Color WordsFor(bool selected, bool enabled)
            => selected ? SelectedWords : enabled ? Words : WordsGreyed;
    }

    /// <summary>
    /// The chooser's own words — the reason tags on a greyed row and the cut mark. One place, so
    /// localisation finds them together; the row's main words are each thing's own
    /// <see cref="IInteractable.VerbLabel"/>.
    /// </summary>
    public static class InteractChooserStrings
    {
        /// <summary>Someone is in it (a bunk, a bed).</summary>
        public const string Occupied = "taken";

        /// <summary>The thing said this one action is not on offer right now.</summary>
        public const string Unavailable = "not now";

        public const string ReasonOpen = "(";
        public const string ReasonClose = ")";

        /// <summary>The cut mark for a row past the kit's widest line. Three full stops, not "…": the
        /// face (<see cref="HarbourType"/>) has no ellipsis glyph, and a missing glyph draws nothing.</summary>
        public const string Cut = InteractPopupLayout.Ellipsis;
    }
}
