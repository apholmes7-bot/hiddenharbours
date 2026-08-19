namespace HiddenHarbours.World
{
    /// <summary>
    /// <b>THE ONE FACE — where the game's type lives once it is a Unity asset.</b>
    ///
    /// <para><b>Why this is not in <see cref="NotebookKit"/>.</b> The face is not the notebook's. It
    /// belongs to the dialogue bubble kit, which drew it, and the notebook imports it by reference —
    /// the rig's own probe reports <c>sameFontObject: true</c>, which is literal object identity
    /// rather than a coincidence of numbers. A player reads a bubble and a page in the same voice
    /// because it is the same glyph set, so the baked asset has one home belonging to neither
    /// consumer.</para>
    ///
    /// <para><b>Why it is not in the notebook's art folder.</b> <c>NotebookArtTests</c> asserts that
    /// folder holds EXACTLY what <see cref="NotebookKit.Pieces"/> bakes — a good reverse arm, and one
    /// a stray atlas would break for no better reason than that it happened to be baked at the same
    /// time. Still under <c>Assets/_Project/Art/</c>, so <c>ArtImportPipeline</c>'s import lock (PPU 32
    /// · Point · Uncompressed · no mips) reaches it exactly as it reaches every other piece.</para>
    ///
    /// <para><b>The metrics are the bubble kit's, aliased and never redeclared</b> — the same
    /// discipline <see cref="NotebookKit"/> applies to the cell. If the kit's cell changes, this
    /// follows it, and the baker's contract check catches the day it does not.</para>
    /// </summary>
    public static class HarbourType
    {
        /// <summary>The face's own folder — see the class remarks for why it is not the notebook's.</summary>
        public const string ArtFolder = "Assets/_Project/Art/UI/Type";

        /// <summary>The glyph atlas, drawn by the rig's own <c>text()</c>.</summary>
        public const string AtlasName = "HarbourType";

        /// <summary>The custom <c>Font</c> asset built over the atlas. <c>.fontsettings</c> is Unity's
        /// extension for a legacy custom font and is not a free choice.</summary>
        public const string FontName = "HarbourType";

        public static string AtlasPath => $"{ArtFolder}/{AtlasName}.png";
        public static string FontPath => $"{ArtFolder}/{FontName}.fontsettings";

        // ---- the cell, imported from the kit that drew it ------------------------------------------

        /// <summary>Cell advance — one cell per character, monospace by construction.</summary>
        public const int Advance = DialogueBubbleKit.CellWidth;

        /// <summary>The drawn glyph box: 4 wide inside a 5 wide cell. The 1 px of slack is where the
        /// crowding that reads as an uneven hand lives.</summary>
        public const int GlyphWidth = DialogueBubbleKit.GlyphWidth;

        /// <summary>8 rows: 6 on and above the baseline, 2 of descender.</summary>
        public const int GlyphHeight = DialogueBubbleKit.GlyphHeight;

        /// <summary>Line box — what a Font asset calls line spacing.</summary>
        public const int LineHeight = DialogueBubbleKit.CellHeight;

        /// <summary>
        /// Rows on and above the baseline — the Font's ascent.
        ///
        /// <para><b>Derived, and the derivation matters.</b> The rig draws a glyph's rows from the CAP
        /// TOP downwards and calls row <see cref="DialogueBubbleKit.BaselineRow"/> the baseline, which
        /// is the LAST row of a capital rather than the first row under it. So the distance from the
        /// cap top to the underside of the baseline row is that index plus one — off by one here would
        /// sit every line of the book one pixel into its rule.</para>
        /// </summary>
        public const int Ascent = DialogueBubbleKit.BaselineRow + 1;

        /// <summary>Rows below the baseline. Negative, as a Font asset expects a descent to be.</summary>
        public const int Descent = Ascent - GlyphHeight;

        /// <summary>How many glyph cells go across the atlas. A grid rather than one long strip so the
        /// texture stays close to square at any glyph count.</summary>
        public const int AtlasColumns = 16;
    }
}
