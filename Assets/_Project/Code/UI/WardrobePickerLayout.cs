using UnityEngine;

namespace HiddenHarbours.UI
{
    /// <summary>Where the wardrobe panel ended up, and which way it leaned to get there.</summary>
    public readonly struct WardrobePickerPlacement
    {
        /// <summary>The panel's centre, in canvas units from the canvas's bottom-left.</summary>
        public readonly Vector2 PanelCentre;

        /// <summary>True when the panel took the RIGHT of the wardrobe (the preferred side).</summary>
        public readonly bool OnRight;

        public WardrobePickerPlacement(Vector2 panelCentre, bool onRight)
        {
            PanelCentre = panelCentre;
            OnRight = onRight;
        }
    }

    /// <summary>
    /// <b>Puts the wardrobe's panel BESIDE the wardrobe, never over the fisher — and says where every row
    /// in it sits.</b> The pure half of the picker's geometry, so which side it takes, what happens at a
    /// screen edge, and which row the mouse is over are all EditMode assertions rather than things you
    /// check by walking to a cottage.
    ///
    /// <para><b>Why beside, and why that is the whole placement constraint.</b> The player character IS
    /// the preview (the diegetic-UI canon: no paper-doll screen, no mannequin render). A panel that
    /// covered them would be a clothes menu that hides the clothes. So the list is offset clear of the
    /// anchor rather than centred on it, and the fisher — who stands in front of and below the fixture
    /// she is using — is left in the open.</para>
    ///
    /// <para><b>Right by default, left when the right would not fit.</b> Not a preference: a wardrobe
    /// sits against a wall and the free floor is whichever side the room has. Trying the preferred side
    /// and taking the other when it would cross the margin is the rule <c>DialogueBubbleLayout</c>
    /// already applies to a bubble near an edge, and it means no room's geometry can push the list
    /// off-screen.</para>
    ///
    /// <para><b>One row geometry, used twice.</b> <see cref="RowOffsetY"/> is what the builder positions a
    /// row by AND what <see cref="RowIndexAt"/> hit-tests the mouse against — deliberately one function
    /// rather than a layout pass and a matching hit-test, because two spellings of the same offset is
    /// exactly how a list ends up selecting the row above the one you clicked.</para>
    /// </summary>
    public static class WardrobePickerLayout
    {
        // ---- the panel's parts, in canvas units ----------------------------------------------
        //
        // Layout, not balance: these are the panel's construction, in the same class of constant as
        // DialoguePresenter's row height and PaperUi's rules. Nothing here is a number the owner tunes
        // to change how the game plays (rule 6) — the tunable content is the wardrobe asset's list.

        /// <summary>The panel's width. Wide enough for the longest shipped label ("Rust and Cream") beside
        /// its chip, and no wider — a narrow column reads as a card in the room rather than a screen.</summary>
        public const float PanelWidth = 232f;

        /// <summary>One outfit row.</summary>
        public const float RowHeight = 26f;

        /// <summary>The heading strip above the rows.</summary>
        public const float TitleHeight = 30f;

        /// <summary>The controls line below them.</summary>
        public const float HintHeight = 24f;

        /// <summary>Inset from the panel's edges to its contents.</summary>
        public const float PadX = 10f;

        /// <summary>Inset above the heading and below the hint.</summary>
        public const float PadY = 8f;

        /// <summary>Clear space between the wardrobe and the panel's near edge.</summary>
        public const float Gap = 26f;

        /// <summary>How close to a screen edge the panel may come.</summary>
        public const float ScreenMargin = 12f;

        /// <summary>The colour chip's side.</summary>
        public const float ChipSize = 14f;

        /// <summary>The full panel for a list of <paramref name="rowCount"/> outfits.</summary>
        public static Vector2 PanelSize(int rowCount) => new Vector2(
            PanelWidth,
            PadY + TitleHeight + Mathf.Max(0, rowCount) * RowHeight + HintHeight + PadY);

        /// <summary>
        /// The CENTRE of row <paramref name="index"/>, as an offset in y from the panel's centre —
        /// negative going down the list. The panel's own frame, so it survives the panel moving.
        /// </summary>
        public static float RowOffsetY(Vector2 panelSize, int index) =>
            panelSize.y * 0.5f - PadY - TitleHeight - (index + 0.5f) * RowHeight;

        /// <summary>
        /// Which row a canvas-space <paramref name="point"/> is over, or -1 for none — the mouse's half of
        /// the same geometry the builder laid the rows out with.
        /// </summary>
        public static int RowIndexAt(Vector2 point, Vector2 panelCentre, Vector2 panelSize, int rowCount)
        {
            if (rowCount <= 0) return -1;

            float dx = point.x - panelCentre.x;
            if (Mathf.Abs(dx) > panelSize.x * 0.5f - PadX) return -1;

            float dy = point.y - panelCentre.y;
            // Rows run downward from the heading; solve RowOffsetY for the index and take the band it
            // lands in, so the two can never drift apart.
            float top = panelSize.y * 0.5f - PadY - TitleHeight;
            float below = top - dy;
            if (below < 0f) return -1;

            int index = (int)(below / RowHeight);
            return index >= 0 && index < rowCount ? index : -1;
        }

        /// <summary>
        /// Solve the panel's position beside <paramref name="anchorPoint"/>.
        /// </summary>
        /// <param name="anchorPoint">The wardrobe, in canvas units from the bottom-left.</param>
        /// <param name="panelSize">The panel's full width and height in canvas units.</param>
        /// <param name="canvasSize">The canvas.</param>
        /// <param name="margin">How close to an edge the panel may come.</param>
        /// <param name="gap">Clear space between the anchor and the panel's near edge.</param>
        public static WardrobePickerPlacement Solve(
            Vector2 anchorPoint, Vector2 panelSize, Vector2 canvasSize, float margin, float gap)
        {
            float half = panelSize.x * 0.5f;

            // The preferred side: to the right of the wardrobe, cleared by the gap.
            float rightCentre = anchorPoint.x + gap + half;
            bool onRight = rightCentre + half <= canvasSize.x - margin;

            float x = onRight ? rightCentre : anchorPoint.x - gap - half;

            // Whichever side was taken, keep the panel inside the margins. Max-then-min so a canvas
            // narrower than the panel resolves to the left margin rather than to an inverted range.
            float minX = margin + half;
            float maxX = canvasSize.x - margin - half;
            x = Mathf.Min(Mathf.Max(x, minX), Mathf.Max(minX, maxX));

            float halfH = panelSize.y * 0.5f;
            float minY = margin + halfH;
            float maxY = canvasSize.y - margin - halfH;
            float y = Mathf.Min(Mathf.Max(anchorPoint.y, minY), Mathf.Max(minY, maxY));

            return new WardrobePickerPlacement(new Vector2(x, y), onRight);
        }

        /// <summary>
        /// Is the wardrobe far enough inside the view to hang a panel off at all? The guard the dialogue
        /// bubble applies to a speaker: a fixture that has gone off-screen (the player was moved, the
        /// camera cut) must let the picker close rather than pin a list to the edge of the frame.
        /// </summary>
        public static bool AnchorIsOnScreen(Vector2 anchorPoint, Vector2 canvasSize, float margin) =>
            anchorPoint.x >= -margin && anchorPoint.x <= canvasSize.x + margin &&
            anchorPoint.y >= -margin && anchorPoint.y <= canvasSize.y + margin;
    }
}
