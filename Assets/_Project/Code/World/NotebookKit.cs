namespace HiddenHarbours.World
{
    /// <summary>
    /// <b>THE NOTEBOOK KIT'S DECLARED NUMBERS</b> — the pixel counts, lanes and piece names the
    /// player's notebook is drawn from, carried into C# so the presenter and the baker measure
    /// against one ruler.
    ///
    /// <para><b>The kit is the oracle, not this file.</b> Every value here is generated into
    /// <c>docs/art/rigs/notebook-kit/notebook.contract.json</c> by the rig itself, and
    /// <c>NotebookKitContract.Disagreements()</c> diffs the two before a bake is allowed. If they
    /// ever part, the JSON is right and this is wrong — fix the constant, never the contract.</para>
    ///
    /// <para><b>⭐ THE FACE IS NOT OURS AND IS NOT REDECLARED.</b> The notebook ships no type of its
    /// own: <c>CELL</c> and <c>FONT</c> come from the dialogue bubble kit by reference, and the rig's
    /// <c>probeType()</c> fails if they ever diverge — it reports <c>sameFontObject: true</c>, which
    /// is literal object identity, not a coincidence of numbers. So the cell metrics below are
    /// deliberately expressed against <see cref="DialogueBubbleKit"/> rather than copied: one voice
    /// across the talking UI and the book UI, enforced by the compiler as well as by the probe.</para>
    ///
    /// <para><b>Overflow is MORE PAGES.</b> There is no scroll state anywhere in this kit and none
    /// should appear here. A block that will not fit moves whole to the facing leaf; what is left
    /// over is counted, not shrunk, and the answer is a page turn.</para>
    /// </summary>
    public static class NotebookKit
    {
        /// <summary>32 px = 1 m, the assets grid — the same grid as every shipped rig and the bubble
        /// kit. 24 px/m is the camera-side number; authoring there would put UI pixels on a different
        /// pitch from world pixels.</summary>
        public const int PixelsPerUnit = 32;

        /// <summary>
        /// Where the baked pieces live. Under <c>Assets/_Project/Art/</c> deliberately, so
        /// <c>ArtImportPipeline</c>'s import lock (PPU 32 · Point · Uncompressed · no mips) reaches
        /// them and the kit's importer only has to witness it rather than restate it.
        ///
        /// <para>It lives on the KIT rather than on a presenter because the bake and the import both
        /// need it and neither should have to reference a view to find out where art goes.</para>
        /// </summary>
        public const string ArtFolder = "Assets/_Project/Art/UI/Notebook";

        // ---- type: IMPORTED from the bubble kit, never redeclared ------------------------------

        /// <summary>Cell advance — 5 px. Aliased from the bubble kit so a change there cannot leave
        /// the book set in a different rhythm from the speech it quotes.</summary>
        public const int CellWidth = DialogueBubbleKit.CellWidth;

        /// <summary>Line box — 10 px (glyph box 8 + leading 2).</summary>
        public const int CellHeight = DialogueBubbleKit.CellHeight;

        public const int GlyphWidth = DialogueBubbleKit.GlyphWidth;
        public const int GlyphHeight = DialogueBubbleKit.GlyphHeight;
        public const int BaselineRow = DialogueBubbleKit.BaselineRow;
        public const int Leading = DialogueBubbleKit.Leading;

        /// <summary>Row pitch down the page — one ruled line to the next.</summary>
        public const int Pitch = 10;

        /// <summary>The printed rule sits on row 7 of the line box, two under the baseline, so
        /// descenders cross it as they do on real ruled stock.</summary>
        public const int RuleRow = 7;

        /// <summary>1 px of slack inside the cell (glyph 4 wide in a cell 5 wide) — where the
        /// crowding that reads as an uneven hand lives.</summary>
        public const int Slack = 1;

        // ---- the leaf's lanes ------------------------------------------------------------------

        public const int PadL = 4;
        public const int PadR = 4;
        public const int PadB = 8;

        /// <summary>The margin lane a row cursor rests in — 12 px, sized for the bubble kit's 10 px
        /// pointing hand.</summary>
        public const int CursorLane = 12;

        /// <summary>The lane a step's checkbox sits in. Exactly two cells wide, which is the whole
        /// reason <see cref="TitleColsFor"/> is <c>cols + 2</c> and not a tuned number.</summary>
        public const int BoxLane = 10;

        /// <summary>The checkbox itself, 6 × 6 — drawn once and reused verbatim by the player's own
        /// note lines: same function, same rect, different ink weight.</summary>
        public const int BoxSize = 6;

        public const int HeadHeight = 16;
        public const int RuleInset = 5;

        // ---- the book as an object ---------------------------------------------------------------

        public const int CoverPad = 3;
        public const int Edge = 1;
        public const int Gutter = 6;

        /// <summary>The tab column off the fore-edge.</summary>
        public const int TabCol = 28;

        public const int TabHeight = 12;
        public const int TabPad = 3;
        public const int TabGap = 3;
        public const int TabTop = 7;

        /// <summary>Eight stubs fit the fore-edge at any legal size. Past that the tab column needs a
        /// second bank, which is a layout change and not a label change.</summary>
        public const int MaxTabs = 8;

        /// <summary>The chip is the tab column plus the 9 px it stands proud of the fore-edge.</summary>
        public const int TabChipWidth = TabCol + 9;

        /// <summary>A chip letters at most this many characters — CLIPPED, never shrunk, so a longer
        /// label loses its tail rather than its legibility. The kit's published number: content
        /// validation holds authored labels to it and the presenter clips at it, and both read it from
        /// HERE — a second spelling of this cap is exactly how a label that validates fine ends up
        /// clipped on the page.</summary>
        public const int ChipChars = 5;

        public const int RibbonWidth = 3;
        public const int PlateMinLines = 5;

        /// <summary>A page turn is a FLIP, not a scroll: lift off the right leaf, stand at the gutter,
        /// land on the left.</summary>
        public const int TurnFrames = 3;

        /// <summary>~70 ms a frame. Three frames at this speed is a turn you can read; slower reads as
        /// a cutscene.</summary>
        public const int TurnFrameMs = 70;

        // ---- the legal size range ----------------------------------------------------------------

        public const int MinCols = 16;
        public const int MaxCols = 40;
        public const int MinLines = 6;
        public const int MaxLines = 22;

        /// <summary>INTEGER scale only. A fractional scale puts the hand on a half cell and the wobble
        /// stops reading as a hand and starts reading as a bug.</summary>
        public const int MaxScale = 6;

        // ---- the formulae, which are the whole layout --------------------------------------------

        /// <summary><c>padL + cursorLane + boxLane + (cols × 5 − 1) + padR</c> = <c>5c + 29</c>.</summary>
        public static int PageWidthFor(int cols) => 5 * cols + 29;

        /// <summary><c>headH + lines × 10 + padB</c> = <c>10L + 24</c>.</summary>
        public static int PageHeightFor(int lines) => 10 * lines + 24;

        /// <summary><c>2 × pageW + gutter + 2 × (coverPad + edge) + tabCol</c> = <c>10c + 100</c>.
        /// One more column is +10 px of width, on both leaves.</summary>
        public static int SpreadWidthFor(int cols) => 10 * cols + 100;

        /// <summary><c>pageH + 2 × (coverPad + edge)</c> = <c>10L + 32</c>. One more line is +10 px.</summary>
        public static int SpreadHeightFor(int lines) => 10 * lines + 32;

        /// <summary>A title runs the text lane AND the box lane, because nothing is checked off beside
        /// it — so it is exactly two cells wider than a step. Not a tuned number: the box lane is
        /// <see cref="BoxLane"/> = 10 px = two 5 px cells, for that reason and no other.</summary>
        public static int TitleColsFor(int cols) => cols + 2;

        /// <summary>Cells on a whole spread — both leaves.</summary>
        public static int CellsPerSpread(int cols, int lines) => 2 * cols * lines;

        // ---- the read budget ---------------------------------------------------------------------

        /// <summary>
        /// At the CLOSEST zoom tier (4×, a 480 × 270 screen) the open book is 410 × 232 px — <b>73.4%
        /// of the screen</b>, leaving 26.6% of the world visible. It DOMINATES by design: it is held
        /// up in front of her, not docked in a corner. The number is published so the presenter
        /// chooses it knowingly rather than discovering it.
        /// </summary>
        public const int ClosestTierCols = 31;

        /// <inheritdoc cref="ClosestTierCols"/>
        public const int ClosestTierLines = 20;

        /// <summary>
        /// ⚠️ THE NUMBER THE WRITING LANE NEEDS: at the closest tier a step line holds 31 characters
        /// and a title line 33, with 20 ruled lines a leaf. A longer step is NOT clipped — it wraps
        /// and eats a line, and a quest whose block outgrows a leaf moves whole to the facing leaf.
        /// Write to 31 and nothing surprises anybody.
        /// </summary>
        public const int StepCharsAtClosestTier = ClosestTierCols;

        // ---- the baked pieces ----------------------------------------------------------------------

        public const string PieceStockRuled = "Notebook_Stock_Ruled";
        public const string PieceStockSquared = "Notebook_Stock_Squared";
        public const string PieceStockMine = "Notebook_Stock_Mine";

        public const string PieceBoxPencil = "Notebook_Box_Pencil";
        public const string PieceBoxPrinted = "Notebook_Box_Printed";
        public const string PieceBoxRound = "Notebook_Box_Round";
        public const string PieceBoxTicked = "Notebook_Box_Ticked";

        public const string PieceCurrentArrow = "Notebook_Current_Arrow";
        public const string PieceCurrentPill = "Notebook_Current_Pill";
        public const string PieceCurrentDot = "Notebook_Current_Dot";

        public const string PieceCaret = "Notebook_Caret";
        public const string PieceStampDone = "Notebook_Stamp_Done";

        public const string PieceCursorHand = "Notebook_Cursor_Hand";
        public const string PieceCursorTack = "Notebook_Cursor_Tack";

        public const string PieceTabCard = "Notebook_Tab_Card";
        public const string PieceTabTape = "Notebook_Tab_Tape";
        public const string PieceTabCloth = "Notebook_Tab_Cloth";

        public const string PieceMarkNew = "Notebook_Mark_New";
        public const string PieceMarkNext = "Notebook_Mark_Next";
        public const string PieceMarkPrev = "Notebook_Mark_Prev";
        public const string PieceDogear = "Notebook_Dogear";
        public const string PieceRibbon = "Notebook_Ribbon";

        public const string PieceSelectPill = "Notebook_Select_Pill";
        public const string PieceSelectBracket = "Notebook_Select_Bracket";
        public const string PieceSelectRule = "Notebook_Select_Rule";
        public const string PieceSelectPress = "Notebook_Select_Press";

        /// <summary>
        /// Every baked piece, in bake order. ONE table, because the bake, the size cross-check and the
        /// importer all have to agree about what exists — a second list elsewhere is how a piece gets
        /// baked and never imported.
        ///
        /// <para><b>⚠️ `cursor.cuff` IS DELIBERATELY ABSENT, and that is a finding rather than an
        /// omission.</b> The kit declares <c>CURSOR_ORDER = ['hand','cuff','tack']</c>, but the bubble
        /// kit ships <c>['hand','glove','tack','hook']</c> — there is no <c>cuff</c>. The rig resolves
        /// cursors as <c>BK.CURSORS[kind] || BK.CURSORS.hand</c>, so <c>cuff</c> silently falls back to
        /// HAND's mask, and the notebook's own <c>CURSORS.cuff.ramp</c> is identical to
        /// <c>CURSORS.hand.ramp</c> — so it renders <b>byte-for-byte identical to hand</b> (verified
        /// under Node against the shipping rigs). Baking it would ship a duplicate PNG wearing a name
        /// that implies a second cursor. <c>NotebookKitCuffTrapTests</c> pins this: the day the kit
        /// gives cuff a real mask or its own ramp, that test goes red and cuff earns its own bake.
        /// The likely intent is the bubble kit's <c>glove</c> (10 × 10), and the <c>|| hand</c>
        /// fallback is what hid the slip.</para>
        /// </summary>
        public static readonly string[] Pieces =
        {
            PieceStockRuled, PieceStockSquared, PieceStockMine,
            PieceBoxPencil, PieceBoxPrinted, PieceBoxRound, PieceBoxTicked,
            PieceCurrentArrow, PieceCurrentPill, PieceCurrentDot,
            PieceCaret, PieceStampDone,
            PieceCursorHand, PieceCursorTack,
            PieceTabCard, PieceTabTape, PieceTabCloth,
            PieceMarkNew, PieceMarkNext, PieceMarkPrev, PieceDogear, PieceRibbon,
            PieceSelectPill, PieceSelectBracket, PieceSelectRule, PieceSelectPress,
        };

        // ---- what the owner picked, and what is one constant away ----------------------------------

        /// <summary>PRINTED RULED ships. Her own leaves are <see cref="PieceStockMine"/> — the same
        /// paper, ruled by HER in pencil, with no printed margin rule. Distinct by drawing, not by a
        /// new colour, so My Notes cannot read as another printed tab.</summary>
        public const string ShippedStock = PieceStockRuled;

        /// <summary>PENCIL SQUARE ships — it has to survive both the printed voice and hers.</summary>
        public const string ShippedBox = PieceBoxPencil;

        /// <summary>
        /// INK ARROW ships for the current step. Gold already means SELECTED, and spending it twice on
        /// one page is how a colour stops meaning anything. ⚠️ If the owner wants the gold-pill
        /// emphasis instead, this is the one string to change — and then selection needs its own mark
        /// (the pencil bracket is drawn and measured, <see cref="PieceSelectBracket"/>).
        /// </summary>
        public const string ShippedCurrentMark = PieceCurrentArrow;

        /// <summary>HAND-LETTERED CARD ships for the tab family.</summary>
        public const string ShippedTab = PieceTabCard;

        /// <summary>GOLD PILL ships for selection — the bubble kit's cove gold, one selection
        /// vocabulary across the talking UI and the book UI.</summary>
        public const string ShippedSelect = PieceSelectPill;
    }
}
