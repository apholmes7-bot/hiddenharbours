using UnityEngine;
using UnityEngine.UI;
using HiddenHarbours.Core;

namespace HiddenHarbours.World
{
    /// <summary>
    /// <b>WHICH FONT THE SPEECH BUBBLE WEARS, AND AT WHAT SIZE</b> — one answer, for both bubble
    /// surfaces.
    ///
    /// <para><b>Why one place.</b> There are two presenters over one bubble: the modal conversation and
    /// the ambient pool. If each decided its own font, the first villager to speak near a conversation
    /// would put two different faces on one screen. So the decision lives here and they both ask.</para>
    ///
    /// <para><b>The size is DERIVED, and that is the whole trick.</b> <see cref="HarbourType"/> is baked
    /// at <see cref="HarbourType.GlyphHeight"/> px with a monospace advance of
    /// <see cref="HarbourType.Advance"/>, and Unity draws a font at <c>fontSize / font.fontSize</c> times
    /// its baked size. So a <c>fontSize</c> of <c>GlyphHeight × ArtScale</c> draws the face at exactly
    /// <see cref="DialogueBubbleKit.ArtScale"/> — one glyph pixel per kit pixel per art pixel — and the
    /// type lands on the same grid the panel is drawn on. One character then measures
    /// <c>Advance × ArtScale</c> canvas units, which is what lets the caret own a cell and what makes a
    /// line of <see cref="DialogueBubbleKit.MaxCols"/> characters fill the kit's widest panel exactly.</para>
    ///
    /// <para><b>⚠ The number this replaces was already 24, and it was right by luck rather than by
    /// derivation.</b> The greybox picked 24 for the built-in font because it looked correct next to the
    /// art; 8 × 3 is 24 because the face and the kit were drawn for each other. The name chip and the
    /// option rows were NOT right — 17 and 22 are not multiples of the baked 8, so the face would have
    /// been resampled at a fractional scale and every glyph in them softened. Deriving the size is what
    /// makes that a rule instead of a coincidence.</para>
    ///
    /// <para><b>The fallback is exact.</b> With the face not imported the bubble draws in Unity's
    /// built-in font at the same size, which is the greybox every other bubble fixture runs against.</para>
    /// </summary>
    public static class BubbleFace
    {
        /// <summary>
        /// The <c>Text.fontSize</c> that draws the baked face at the bubble's own art scale.
        ///
        /// <para>Unity scales a font by <c>fontSize / font.fontSize</c>, and the face is baked at
        /// <see cref="HarbourType.GlyphHeight"/>; so this is <c>ArtScale</c>, exactly, and one kit pixel
        /// of type is one kit pixel of panel. Measured in the live editor: ten characters span 50 canvas
        /// units at <c>fontSize 8</c> and 150 at <c>fontSize 24</c>.</para>
        /// </summary>
        public const int FontSize = HarbourType.GlyphHeight * DialogueBubbleKit.ArtScale;

        /// <summary>How wide one character is on screen, in canvas units — the monospace cell the caret
        /// owns, and the number that makes <see cref="DialogueBubbleKit.MaxCols"/> characters fill
        /// <c>PanelWidthFor(MaxCols)</c>.</summary>
        public const int AdvanceUnits = HarbourType.Advance * DialogueBubbleKit.ArtScale;

        private static Font _face;
        private static bool _tried;
        private static bool _plateFallback;

        /// <summary>
        /// The baked face, or null when it is not imported. Loaded once per session and cached — a
        /// <c>Resources.Load</c> per bubble piece would be a file lookup per piece.
        ///
        /// <para><b>⚠ Explicit <c>== null</c>, never <c>??</c></b>: a destroyed or unloaded asset is
        /// fake-null and the null-propagating operators sail straight past Unity's overloaded <c>==</c>,
        /// so a cached corpse would be handed out forever.</para>
        /// </summary>
        public static Font Face
        {
            get
            {
                if (_plateFallback) return null;
                if (_face == null && !_tried)
                {
                    _tried = true;
                    _face = Resources.Load<Font>(HarbourType.ResourceKey);
                }
                return _face != null ? _face : null;
            }
        }

        /// <summary>The built-in font the bubble falls back to when the face is not imported.</summary>
        public static Font Fallback()
        {
            Font f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return f != null ? f : Resources.GetBuiltinResource<Font>("Arial.ttf");
        }

        /// <summary>The font a bubble should draw in: the baked face when it is there, the built-in one
        /// when it is not. Never null in a working project.</summary>
        public static Font Resolve()
        {
            Font face = Face;
            return face != null ? face : Fallback();
        }

        /// <summary>True when <paramref name="font"/> IS the baked face — object identity, not a name
        /// match, because a look-alike named the same would still be a different set of glyphs.</summary>
        public static bool IsFace(Font font) => font != null && ReferenceEquals(font, Face);

        /// <summary>
        /// Dress <paramref name="text"/> in the bubble's font at the bubble's size.
        ///
        /// <para>One call so the font and the size cannot disagree — and no transform scaling anywhere,
        /// because <c>fontSize</c> already does it. (An earlier draft of this file scaled the rect as
        /// well and drew every line nine times too large; the plate is what caught it.)</para>
        /// </summary>
        public static void Wear(Text text)
        {
            if (text == null) return;
            text.font = Resolve();
            text.fontSize = FontSize;
        }

        /// <summary>Forget the cached face (fixtures, so one test's "not imported" cannot leak into the
        /// next). The game never calls it.</summary>
        public static void ForgetCache()
        {
            _face = null;
            _tried = false;
            _plateFallback = false;
        }

        /// <summary>
        /// Pretend the face is not imported, so a plate can photograph the BEFORE state in the same
        /// editor session as the after. The game never calls it, nothing reads it but <see cref="Face"/>,
        /// and <see cref="ForgetCache"/> clears it.
        /// </summary>
        public static void UseFallbackForPlates(bool on)
        {
            _plateFallback = on;
            _face = null;
            _tried = false;
        }
    }
}
