using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.UI
{
    /// <summary>
    /// <b>Where the interaction popup sits and what fits in it</b> — the whole geometry and the whole
    /// text rule, as pure static maths with no canvas anywhere near it.
    ///
    /// <para><b>Why it is its own file.</b> The same reason <see cref="HudBandLayout"/> is: a cloud agent
    /// with no editor can prove numbers, and cannot prove pixels. Everything about this popup that can be
    /// wrong in a way a test can catch — that it is in the lower right, that it clears the screen edge,
    /// that a long label is cut rather than wrapped — lives here and is asserted headless. What is left in
    /// <see cref="InteractPopup"/> is wiring.</para>
    ///
    /// <para><b>Reference units, top-left convention.</b> Same frame as <see cref="PaperUi"/> and
    /// <c>TidePanel</c>: a 1280×720 host, pivot and anchors at the TOP-LEFT, x growing right and y growing
    /// DOWN (negative). The popup is lower-right, so its anchored position is a large positive x and a
    /// large negative y — which reads oddly the first time and is the convention every other page here
    /// already speaks.</para>
    /// </summary>
    public static class InteractPopupLayout
    {
        /// <summary>The reference the popup lays out in — the project's landscape PC-first canvas, shared
        /// with <see cref="PaperUi.ReferenceResolution"/> and <see cref="HudBandLayout"/>.</summary>
        public const float RefW = HudBandLayout.RefW, RefH = HudBandLayout.RefH;

        /// <summary>
        /// Above the HUD band (100) and the market screens (200/205), below the tide table (210) and the
        /// whole shell (300+).
        ///
        /// <para><b>Why below the table and the shell rather than above everything.</b> The popup names an
        /// interaction in the WORLD, and while a page is out or the game is paused there is no world to
        /// interact with — the feeders have already gone quiet under
        /// <see cref="Core.InteractionGate"/> and the shell's input block, so nothing should be drawn. The
        /// sort order is the belt to that braces: even if a feeder were ever late to clear, the popup
        /// cannot land on top of the paper.</para>
        /// </summary>
        public const int SortingOrder = 208;

        /// <summary>Panel size in reference units. One line of type with a little air, wide enough for
        /// "Set the fuel can down" at <see cref="FontSize"/> and no wider — a corner note, not a
        /// banner.</summary>
        public const float PanelWidth = 300f, PanelHeight = 44f;

        /// <summary>How far the panel's outer edges sit from the screen's, in reference units. Matched to
        /// the HUD band's own <see cref="HudBandLayout.SidePaddingRef"/> half-gap (16) so the popup's right
        /// edge lines up with the band's above it.</summary>
        public const float MarginX = 16f, MarginY = 16f;

        /// <summary>Type size in reference units. Smaller than a menu line (24) and larger than the shell's
        /// faint detail (16): it must be readable at a glance in peripheral vision without becoming a
        /// second HUD.</summary>
        public const int FontSize = 20;

        /// <summary>Inset from the panel's edge to its type, in reference units.</summary>
        public const float TextPadX = 14f, TextPadY = 6f;

        /// <summary>
        /// The longest label the popup will draw. Anything longer is cut with an ellipsis by
        /// <see cref="Fit"/> rather than wrapped or shrunk.
        ///
        /// <para>Derived, not guessed: <see cref="PanelWidth"/> minus both <see cref="TextPadX"/> insets is
        /// 272 reference units, and the shell's face at <see cref="FontSize"/> averages a shade under 10
        /// units per character in this copy's mix of letters — so 28 characters is the honest fit with a
        /// character in hand. It is a CAP on authored copy, not a layout engine: a label that needs
        /// trimming is a label that wanted rewriting, and the cut is what makes that obvious in the
        /// build rather than in a review.</para>
        /// </summary>
        public const int MaxCharacters = 28;

        /// <summary>What a cut label ends with.</summary>
        public const string Ellipsis = "…";

        /// <summary>
        /// The panel's anchored position in the top-left-pivoted reference host — lower right, inset by
        /// <see cref="MarginX"/> / <see cref="MarginY"/> from the two edges it hugs.
        /// </summary>
        public static Vector2 PanelAnchoredPosition()
            => new Vector2(RefW - MarginX - PanelWidth, -(RefH - MarginY - PanelHeight));

        /// <summary>
        /// The panel's anchored position kept out of what the helm's UI covers (ADR 0050,
        /// <see cref="HelmFootprint"/>): straight up, its column kept, until it clears the covered
        /// area's top by its own <see cref="MarginY"/> — above a full-width band, and above a card that
        /// reaches it. Exactly <see cref="PanelAnchoredPosition()"/> with nothing covered, and whenever
        /// the covered area does not reach it.
        /// </summary>
        public static Vector2 PanelAnchoredPosition(in HelmFootprintArea covered, float screenW, float screenH)
        {
            Vector2 home = PanelAnchoredPosition();
            if (covered.IsNone) return home;
            float s = HudBandLayout.ScaleFactor(screenW, screenH);
            float lift = covered.LiftToClear(PanelScreenRect(screenW, screenH), MarginY * s);
            return lift > 0f ? new Vector2(home.x, home.y + lift / s) : home;
        }

        /// <summary>
        /// Where the panel lands at home, in SCREEN pixels (bottom-left origin, y up), on a
        /// <paramref name="screenW"/>×<paramref name="screenH"/> screen: the reference host is pinned
        /// to the screen's TOP-LEFT and scaled by the canvas's own factor, so on a screen that is not
        /// 16:9 the panel is where the host puts it, not in the screen's corner.
        /// </summary>
        public static Rect PanelScreenRect(float screenW, float screenH)
        {
            float s = HudBandLayout.ScaleFactor(screenW, screenH);
            Vector2 a = PanelAnchoredPosition();
            float top = screenH + a.y * s;
            return new Rect(a.x * s, top - PanelHeight * s, PanelWidth * s, PanelHeight * s);
        }

        /// <summary>
        /// The panel's anchored position kept out of the helm's UI AND off the quest note (TASKS,
        /// <see cref="QuestNoteFootprint"/>): the owner's "player tips/actions also cover the ui for
        /// objectives". Exactly <see cref="PanelAnchoredPosition(in HelmFootprintArea, float, float)"/>
        /// when no note is drawn, and whenever the note does not reach the panel.
        /// </summary>
        /// <param name="tasksPx">The note's box in screen pixels (bottom-left origin); zero-sized for
        /// no note.</param>
        public static Vector2 PanelAnchoredPosition(in HelmFootprintArea covered, Rect tasksPx,
                                                    float screenW, float screenH)
        {
            if (QuestNoteFootprint.IsEmpty(tasksPx)) return PanelAnchoredPosition(in covered, screenW, screenH);
            Vector2 home = PanelAnchoredPosition();
            float s = HudBandLayout.ScaleFactor(screenW, screenH);
            Vector2 movePx = PlacementOffsetPx(in covered, tasksPx, screenW, screenH);
            return new Vector2(home.x + movePx.x / s, home.y + movePx.y / s);
        }

        /// <summary>Where the panel actually lands, in SCREEN pixels, for what the helm covers and
        /// where the quest note is — <see cref="PanelScreenRect(float, float)"/> moved by the same
        /// rule <see cref="PanelAnchoredPosition(in HelmFootprintArea, Rect, float, float)"/> applies.</summary>
        public static Rect PanelScreenRect(in HelmFootprintArea covered, Rect tasksPx,
                                           float screenW, float screenH)
        {
            Rect at = PanelScreenRect(screenW, screenH);
            Vector2 movePx = PlacementOffsetPx(in covered, tasksPx, screenW, screenH);
            at.x += movePx.x;
            at.y += movePx.y;
            return at;
        }

        /// <summary>
        /// How far the panel moves from home, in screen pixels (x negative = left, y positive = up).
        ///
        /// <para>First the helm's rule, unchanged. Then, if the panel would come within its own margin
        /// of the quest note, the SHORTER of two moves — whichever leaves it nearer the lower right:
        /// <b>up</b>, its column kept, to stand <see cref="MarginY"/> above the note; or <b>beside</b>,
        /// its row kept, to stand <see cref="MarginX"/> left of it. Beside is only taken when the panel
        /// still has its <see cref="MarginX"/> from the screen's left edge and does not land on the
        /// helm's card; up is taken on a tie. Up never comes back down onto the helm: a card standing
        /// above the note is stepped over as well.</para>
        /// </summary>
        public static Vector2 PlacementOffsetPx(in HelmFootprintArea covered, Rect tasksPx,
                                                float screenW, float screenH)
        {
            float s = HudBandLayout.ScaleFactor(screenW, screenH);
            float gapX = MarginX * s, gapY = MarginY * s;

            Rect at = PanelScreenRect(screenW, screenH);
            float helmLift = covered.IsNone ? 0f : Mathf.Max(0f, covered.LiftToClear(at, gapY));
            at.y += helmLift;

            if (QuestNoteFootprint.IsEmpty(tasksPx)) return new Vector2(0f, helmLift);

            bool reaches = at.xMin < tasksPx.xMax + gapX && at.xMax > tasksPx.xMin - gapX
                        && at.yMin < tasksPx.yMax + gapY && at.yMax > tasksPx.yMin - gapY;
            if (!reaches) return new Vector2(0f, helmLift);

            // Up: above the note, and above a helm card if one stands there.
            Rect up = at;
            up.y = tasksPx.yMax + gapY;
            if (covered.Overlaps(up)) up.y += Mathf.Max(0f, covered.LiftToClear(up, gapY));
            float rise = up.y - at.y;

            // Beside: left of the note, on the row the helm's rule already gave it.
            Rect beside = at;
            beside.x = tasksPx.xMin - gapX - at.width;
            float slide = at.x - beside.x;
            bool besideFits = beside.xMin >= gapX && !covered.Overlaps(beside);
            bool upFits = up.yMax <= screenH;

            if (besideFits && (slide < rise || !upFits)) return new Vector2(-slide, helmLift);
            return new Vector2(0f, helmLift + rise);
        }

        /// <summary>The panel's size, as one value for the caller to apply.</summary>
        public static Vector2 PanelSize() => new Vector2(PanelWidth, PanelHeight);

        /// <summary>The type's anchored position INSIDE the panel (the panel is itself top-left
        /// pivoted).</summary>
        public static Vector2 TextAnchoredPosition() => new Vector2(TextPadX, -TextPadY);

        /// <summary>The type's box inside the panel.</summary>
        public static Vector2 TextSize()
            => new Vector2(PanelWidth - TextPadX * 2f, PanelHeight - TextPadY * 2f);

        /// <summary>
        /// One line, and only one — the label as it will actually be drawn.
        ///
        /// <para><b>Newlines are collapsed to a space</b> before anything else. The popup is a single line
        /// by the owner's ruling; a label with a line break in it would silently grow the panel's
        /// occupied box and put type outside the paper.</para>
        ///
        /// <para><b>Over-length is CUT, never wrapped and never shrunk.</b> Wrapping makes a one-line
        /// surface a two-line one; shrinking makes the type size depend on the copy, so two fixtures
        /// standing side by side would be lettered differently. The cut keeps <see cref="MaxCharacters"/>
        /// glyphs INCLUDING the ellipsis, so the drawn width is bounded whatever arrives.</para>
        ///
        /// <para>Returns the input unchanged when it already fits — the common case, and it must not
        /// allocate, because the popup is handed a label every time the offer changes.</para>
        /// </summary>
        public static string Fit(string label)
        {
            if (string.IsNullOrEmpty(label)) return "";

            bool hasBreak = label.IndexOf('\n') >= 0 || label.IndexOf('\r') >= 0;
            if (!hasBreak && label.Length <= MaxCharacters) return label;   // the common path: no alloc

            if (hasBreak) label = label.Replace('\n', ' ').Replace('\r', ' ');
            if (label.Length <= MaxCharacters) return label;

            return label.Substring(0, Mathf.Max(0, MaxCharacters - Ellipsis.Length)) + Ellipsis;
        }
    }
}
