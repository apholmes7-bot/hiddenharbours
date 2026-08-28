namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>THE BUBBLE KIT'S OWN NUMBERS</b>, as C#. Every value here is copied from
    /// <c>docs/art/rigs/dialogue-bubble-kit/dialogueBubble.contract.json</c>, which the art lane
    /// GENERATES from the rig and never hand-edits.
    ///
    /// <para><b>Why a C# copy at all, when the JSON is right there.</b> The presenter is runtime code
    /// with no asset to load and no file to read — it builds its canvas in <c>Awake</c> and must work
    /// headless (that is what makes every behaviour in <c>DialoguePresenter</c> testable without a
    /// key press). Reaching for a <c>TextAsset</c> here would buy a wiring step and an I/O failure
    /// mode in exchange for nothing. So the numbers live here, and <c>BubbleKitContractTests</c>
    /// READS THE KIT'S JSON and fails if a single one of them drifts. The JSON stays the oracle; this
    /// is a checked transcription, not a second source of truth. Same bargain as
    /// <see cref="HiddenHarbours.Tools.RigBaking.NavBuoyContract"/>: "the contract is the oracle".</para>
    ///
    /// <para><b>The grid is 32, and the kit says why.</b> The two-grid question (24 vs 32) that the
    /// tripwire told the import PR to settle is settled IN THE KIT: 32 px = 1 m is the assets grid
    /// every shipped rig here bakes at, and the bubble is authored on it because the bubble is an
    /// object IN the scene. 24 px/m is the camera-side number. Authoring at 24 would put UI pixels on
    /// a different pitch from world pixels and the bubble would read as an overlay — "the one thing
    /// this slice must not do".</para>
    /// </summary>
    public static class DialogueBubbleKit
    {
        /// <summary>Pixels per metre the kit is authored at. NOT the camera-side 24.</summary>
        public const int PixelsPerUnit = 32;

        // ---- the text cell -------------------------------------------------------------------
        //
        // Monospace by construction: one cell per character, so the per-character fill never
        // reflows and the caret can own a cell. The cell went 4×8 → 5×10 when the owner ruled for
        // mixed case (2026-08-17) — that ruling is already priced into every number below.

        /// <summary>Horizontal advance per character, px. Also the cell width.</summary>
        public const int CellWidth = 5;

        /// <summary>Line pitch, px — the glyph box (8) plus leading (2).</summary>
        public const int CellHeight = 10;

        /// <summary>The drawn glyph box inside the cell, px.</summary>
        public const int GlyphWidth = 4;
        public const int GlyphHeight = 8;

        public const int CapHeight = 6;
        public const int XHeight = 4;
        public const int Ascender = 6;
        public const int Descender = 2;
        public const int BaselineRow = 5;
        public const int Leading = 2;

        // ---- the panel -----------------------------------------------------------------------

        /// <summary>The 9-slice source tile is square: <see cref="PanelSlice"/> × <see cref="PanelSlice"/>.</summary>
        public const int PanelSlice = 18;

        /// <summary>The 9-slice corner, px — the border on all four sides of the slice tile.
        /// Corners are copied, edges repeat 1 px, the centre is flat paper.</summary>
        public const int PanelCorner = 6;

        public const int PanelBorder = 1;
        public const int PanelChamfer = 2;

        /// <summary>The text inset on each side, px: the rect text may fill is
        /// <c>x+5, y+5, w-10, h-10</c>. Measured, not derived.</summary>
        public const int PanelInsetL = 5;
        public const int PanelInsetT = 5;
        public const int PanelInsetR = 5;
        public const int PanelInsetB = 5;

        public const int MinCols = 8;
        public const int MinLines = 1;
        public const int MaxCols = 34;
        public const int MaxLines = 4;

        /// <summary>Panel width for a given column count, px — the kit's own formula
        /// <c>insetL + (cols × 5 − 1) + insetR</c>. The trailing −1 is the last cell's advance gap,
        /// which no glyph occupies.</summary>
        public static int PanelWidthFor(int cols) => PanelInsetL + (cols * CellWidth - 1) + PanelInsetR;

        /// <summary>Panel height for a given line count, px — <c>insetT + (lines − 1) × 10 + 8 + insetB</c>.
        /// The last line costs the GLYPH box (8), not a full line pitch: there is no leading under it.</summary>
        public static int PanelHeightFor(int lines) =>
            PanelInsetT + (lines - 1) * CellHeight + GlyphHeight + PanelInsetB;

        /// <summary>How many characters fit on a line of a text box <paramref name="textBoxPx"/> wide —
        /// the kit's <c>BubbleKit.colsFor</c>, <c>floor((px + 1) / 5)</c>. The +1 is the same missing
        /// trailing advance as in <see cref="PanelWidthFor"/>.</summary>
        public static int ColsFor(int textBoxPx) => (textBoxPx + 1) / CellWidth;

        // ---- the tails -----------------------------------------------------------------------
        //
        // Six, and the UP SET SHIPS DRAWN — it is not the down set flipped at import. A tail is two
        // colours with no shading so a y-flip would fight nothing, but the kit performs the flip so
        // the import never reasons about it AND so the tip pixel is measured in both directions
        // rather than derived. Do not "save" three sprites by flipping here.

        /// <summary>Every tail is this wide, px.</summary>
        public const int TailWidth = 7;

        /// <summary>How far the tip stops CLEAR OF THE SPEAKER'S INK, px — not clear of the anchor.
        /// The head anchor is the head CENTRE, so a tip placed 3 px off the anchor buries itself in
        /// the crown; the mount takes an anchor→ink distance and this gap is applied past it.</summary>
        public const int TailGap = 3;

        // ---- the name chip -------------------------------------------------------------------

        public const int ChipHeight = 12;
        public const int ChipPadX = 4;
        public const int ChipMountX = 4;

        /// <summary>How far the chip overlaps the panel edge it rides, px.</summary>
        public const int ChipOverlap = 4;

        /// <summary>Chip width for a label of <paramref name="chars"/> characters, px —
        /// <c>padX × 2 + (chars × 5 − 1)</c>.</summary>
        public static int ChipWidthFor(int chars) => ChipPadX * 2 + (chars * CellWidth - 1);

        // ---- the continue marker -------------------------------------------------------------

        /// <summary>The marker's drawn size, px. It appears only when the fill completes.</summary>
        public const int MarkerWidth = 7;
        public const int MarkerHeight = 4;

        /// <summary>
        /// ⚠️ <b>The BAKED cell is a row taller than the drawn marker</b> — 7 × 5, not 7 × 4.
        ///
        /// <para>The bob is +1 px on frame 1, and the kit's own <c>render('marker')</c> allocates
        /// <c>h + 1</c> so BOTH frames share one cell and register against each other. Bake frame 0
        /// at its natural top and frame 1 one row down, in a common 7 × 5 cell, and the two-frame
        /// bob is a straight sprite swap. Cut them to their own ink extents instead and the bob
        /// gains a second, unintended pixel of travel — which is precisely the sort of thing that
        /// looks like "the animation is wrong" and is actually the crop.</para>
        /// </summary>
        public const int MarkerCellHeight = MarkerHeight + 1;

        /// <summary>Inset from the panel's right edge to the marker, px (<c>x + w − 11</c>).</summary>
        public const int MarkerInsetR = 11;

        /// <summary>Drop below the panel's bottom edge, px (<c>y + h − 2</c>).</summary>
        public const int MarkerDropB = 2;

        /// <summary>Milliseconds per bob frame.</summary>
        public const int MarkerFrameMs = 420;

        // ---- the caret -----------------------------------------------------------------------

        /// <summary>A 1 × 6 ink bar in the cell the next character will land in. Shown WHILE FILLING
        /// ONLY — it hands off to the continue marker when the line completes.</summary>
        public const int CaretWidth = 1;
        public const int CaretHeight = 6;
        public const int CaretBlinkMs = 260;

        // ---- the option bubble ---------------------------------------------------------------

        public const int OptionRowHeight = 13;
        public const int OptionGutter = 11;
        public const int OptionInsetT = 4;
        public const int OptionInsetB = 4;
        public const int OptionInsetR = 6;
        public const int OptionMinRows = 2;
        public const int OptionMaxRows = 4;
        public const int OptionPillChamfer = 1;

        /// <summary>Where the row label starts inside the option bubble, px from its top-left.</summary>
        public const int OptionTextX = 16;
        public const int OptionTextY = 8;

        /// <summary>
        /// How far the speech panel, name chip and option bubble stay clear of the speaker's sprite
        /// rect, px.
        ///
        /// <para><b>This number is one implementation of the invariant, not the invariant.</b> The
        /// RULE is that those three pieces never occlude the speaker — because the character IS the
        /// portrait and nothing may cover her. The tail is the deliberate exception: it is the
        /// pointer, it reaches toward her, and it stops <see cref="TailGap"/> px clear of her ink.</para>
        /// </summary>
        public const int SpeakerClear = 20;

        // ---- case ----------------------------------------------------------------------------

        /// <summary>
        /// <b>Speech body is SENTENCE CASE</b> (owner ruling, 2026-08-17): both reference games set
        /// speech mixed, and that is why their dialogue reads as voice rather than signage. The kit
        /// ships a full a–z to pay for it — a 4-row x-height, ascenders to cap height, two descender
        /// rows. The name chip and the option rows stay CAPS: those are labels, not voice.
        ///
        /// <para>Nothing in this file uppercases anything. The constant exists so the ruling is
        /// discoverable from the code that implements it, and so a future "let's just caps it all"
        /// has something to argue with.</para>
        /// </summary>
        public const bool BodyIsSentenceCase = true;

        /// <summary>Labels — the name chip and the option rows — are CAPS.</summary>
        public const bool LabelsAreCaps = true;

        // ---- the baked pieces ----------------------------------------------------------------
        //
        // ⚠️ THE KIT BAKES NOTHING OF ITS OWN. Every piece is live-drawn from dialogueBubbleRig.js
        // ("exactly like the wall calendar's face"), so these PNGs do not arrive with the kit — they
        // are produced by BubbleKitBaker running the rig through the repo's V8 host. That is why the
        // import step here is a BAKE, not a copy: there is no source PNG anywhere to copy from.
        //
        // Two of the pieces are 9-SLICE TILES rather than fixed sprites, because the rig draws their
        // shapes at whatever size the content needs:
        //
        //   Panel — the cream stock. Serves the speech bubble AND the option bubble; options() lays
        //           its rows inside the same panel() the speech body uses, so one tile dresses both.
        //   Gold  — chamfer-1 gold. Serves the name chip AND the selected-row pill: chip() is
        //           literally panel() with the gold ramp, and the pill is the same call at rowH.
        //
        // Everything else is a fixed-size stamp and bakes 1:1.

        public const string PiecePanel = "Bubble_Panel";
        public const string PieceGold = "Bubble_Gold";

        public const string PieceTailLeft = "Bubble_Tail_Left";
        public const string PieceTailCentre = "Bubble_Tail_Centre";
        public const string PieceTailRight = "Bubble_Tail_Right";
        public const string PieceTailLeftUp = "Bubble_Tail_LeftUp";
        public const string PieceTailCentreUp = "Bubble_Tail_CentreUp";
        public const string PieceTailRightUp = "Bubble_Tail_RightUp";

        public const string PieceMarker0 = "Bubble_Marker_0";
        public const string PieceMarker1 = "Bubble_Marker_1";
        public const string PieceCaret = "Bubble_Caret";

        public const string PieceCursorHand = "Bubble_Cursor_Hand";
        public const string PieceCursorGlove = "Bubble_Cursor_Glove";
        public const string PieceCursorTack = "Bubble_Cursor_Tack";
        public const string PieceCursorHook = "Bubble_Cursor_Hook";

        /// <summary>
        /// The six tails, in the kit's own order, as (piece name, rig key). The UP set is drawn, not
        /// flipped — see the note on <see cref="TailWidth"/>.
        /// </summary>
        public static readonly string[] TailPieces =
        {
            PieceTailLeft, PieceTailCentre, PieceTailRight,
            PieceTailLeftUp, PieceTailCentreUp, PieceTailRightUp,
        };

        /// <summary>The rig keys for <see cref="TailPieces"/>, index-for-index.</summary>
        public static readonly string[] TailKeys =
        {
            "left", "centre", "right", "leftUp", "centreUp", "rightUp",
        };

        /// <summary>Each tail's height, px, index-for-index with <see cref="TailPieces"/>. The centre
        /// pair are a row shorter than the corner pair.</summary>
        public static readonly int[] TailHeights = { 7, 6, 7, 7, 6, 7 };

        /// <summary>
        /// Each tail's TIP PIXEL — x, index-for-index with <see cref="TailPieces"/>.
        /// <b>The tip pixel IS the anchor</b>: the presenter aims it at the speaker's own anchor, and
        /// everything else about the tail's placement follows from that.
        /// </summary>
        public static readonly int[] TailTipX = { 1, 3, 5, 1, 3, 5 };

        /// <summary>Each tail's tip pixel — y, from the tail's TOP-left. The down set tips at the
        /// bottom (6/5/6), the up set at the top (0).</summary>
        public static readonly int[] TailTipY = { 6, 5, 6, 0, 0, 0 };

        /// <summary>How far in from the panel's edge a corner tail sits, px — the kit's
        /// <c>corner + 2</c>. Past this the tail would ride the chamfer.</summary>
        public const int TailInsetFromEdge = PanelCorner + 2;

        /// <summary>Index of the first UP tail in <see cref="TailPieces"/>.</summary>
        public const int FirstUpTail = 3;

        /// <summary>
        /// Which of the six tails a placement wants: the nearest of the kit's three mount positions
        /// to where the tail actually needs to leave the panel, in the up set or the down set.
        ///
        /// <para>Pure arithmetic on purpose — the tail a given placement chooses is a STATE a
        /// headless test can assert, where a screenshot of it is not.</para>
        /// </summary>
        /// <param name="pointsDown">The placement's own answer: false when the bubble had to flip
        /// under a speaker near the top of the screen.</param>
        /// <param name="tailRootX">Where the tail must meet the panel, in the same frame as
        /// <paramref name="bubbleLeftX"/>.</param>
        /// <param name="bubbleLeftX">The panel's left edge.</param>
        /// <param name="bubbleWidth">The panel's width, same units.</param>
        /// <param name="unitsPerKitPixel">How many of those units one kit pixel is — normally
        /// <see cref="ArtScale"/>. Passed rather than inferred: the inset is a fixed count of KIT
        /// pixels, and a panel's width alone cannot say what scale it is drawn at.</param>
        /// <returns>An index into <see cref="TailPieces"/>.</returns>
        public static int TailIndexFor(bool pointsDown, float tailRootX, float bubbleLeftX,
                                       float bubbleWidth, float unitsPerKitPixel = ArtScale)
        {
            float local = tailRootX - bubbleLeftX;

            // The kit's three mount positions, in panel-local units. Clamped to the half-width so a
            // panel narrower than two insets cannot put "left" to the right of "centre", which would
            // make the nearest-of-three answer nonsense at the minimum 8-column size.
            float inset = TailInsetFromEdge * unitsPerKitPixel;
            float centrePos = bubbleWidth * 0.5f;
            if (inset > centrePos) inset = centrePos;

            float leftPos = inset;
            float rightPos = bubbleWidth - inset;

            float dl = Abs(local - leftPos);
            float dc = Abs(local - centrePos);
            float dr = Abs(local - rightPos);

            int index = 1;                                   // centre, unless a side is nearer
            if (dl <= dc && dl <= dr) index = 0;
            else if (dr < dc) index = 2;

            return pointsDown ? index : index + FirstUpTail;
        }

        static float Abs(float v) => v < 0f ? -v : v;

        /// <summary>
        /// The four cursors, shipped pick FIRST. All four bake — the alternates are one constant
        /// away (<see cref="ShippedCursor"/>), which is what "worth a live trial" needs to cost
        /// nothing. Same for the stocks and the row highlights: the kit draws every option so the
        /// owner can choose against pixels rather than against a description.
        /// </summary>
        public static readonly string[] CursorPieces =
        {
            PieceCursorHand, PieceCursorGlove, PieceCursorTack, PieceCursorHook,
        };

        /// <summary>The rig keys for <see cref="CursorPieces"/>, index-for-index.</summary>
        public static readonly string[] CursorKeys = { "hand", "glove", "tack", "hook" };

        /// <summary>⚑ OWNER TASTE — the pointing hand ships (owner's pick, 2026-08-17). The wool
        /// glove is his "most ours" and is drawn, measured and one edit away.</summary>
        public const string ShippedCursor = PieceCursorHand;

        /// <summary>⚑ OWNER TASTE — warm cream paper ships. Sailcloth is the strongest alternate
        /// "if cream ever feels too AC".</summary>
        public const string ShippedStock = "cream";

        /// <summary>⚑ OWNER TASTE — the gold pill ships. The pressed row is the only alternative
        /// that costs no colour, if a grade ever fights the gold.</summary>
        public const string ShippedHighlight = "pill";

        /// <summary>Every piece the bake writes, in bake order. The importer, the lookup and the
        /// landed-art test all close over THIS list, so a piece cannot be added in one place and
        /// forgotten in another.</summary>
        public static readonly string[] Pieces =
        {
            PiecePanel, PieceGold,
            PieceTailLeft, PieceTailCentre, PieceTailRight,
            PieceTailLeftUp, PieceTailCentreUp, PieceTailRightUp,
            PieceMarker0, PieceMarker1, PieceCaret,
            PieceCursorHand, PieceCursorGlove, PieceCursorTack, PieceCursorHook,
        };

        // ---- the 9-slice borders -------------------------------------------------------------

        /// <summary>The cream panel's 9-slice source tile is <see cref="PanelSlice"/> square with a
        /// <see cref="PanelCorner"/> border on every side.</summary>
        public const int PanelTileSize = PanelSlice;

        /// <summary>
        /// The gold tile is the smallest square that carries a chamfer-1 panel's whole vocabulary:
        /// cap row · light band · paper · shade band · cap row. Five rows, and a 2 px border keeps
        /// the cap and its band on both sides while the middle stretches.
        ///
        /// <para>It must 9-slice on BOTH axes because its two consumers are different heights — the
        /// name chip is 12 px and the selected-row pill is 13.</para>
        /// </summary>
        public const int GoldTileSize = 5;
        public const int GoldCorner = 2;

        /// <summary>
        /// How many canvas units one kit pixel draws as. The kit is authored at 32 px/m and the
        /// bubble canvas references 1280×720, so an integer multiplier is the only value that keeps
        /// pixel art on the pixel grid — a fractional one resamples the 1 px ink ring into mush at
        /// exactly the zoom tiers ruling #327 exists to keep crisp.
        ///
        /// <para>3 puts the widest panel (119 px) at 357 canvas units, comfortably inside the
        /// greybox's 460 and close to the frame the owner has been reading during playtests.
        /// ⚑ This is the one dial if the bubble reads too small or too large.</para>
        /// </summary>
        public const int ArtScale = 3;
    }
}
