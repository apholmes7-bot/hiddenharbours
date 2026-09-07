using UnityEngine;
using UnityEngine.UI;
using HiddenHarbours.Core;

namespace HiddenHarbours.World
{
    /// <summary>
    /// <b>THE BUBBLE WEARS THE FACE ITS OWN RIG DREW</b> — and the arithmetic that has to come with it.
    ///
    /// <para><b>Why this is not one line.</b> <see cref="HarbourType"/> is a Unity LEGACY (non-dynamic)
    /// <c>Font</c>, and uGUI passes <c>fontSize = 0</c> for one of those: <b>the face ignores
    /// <c>Text.fontSize</c> entirely</b> and draws every glyph at its baked size — advance 5, line height
    /// 10, glyph box 4×8, all pinned by <c>HarbourTypeBakeTests</c>. The bubble's art, meanwhile, is drawn
    /// at <see cref="DialogueBubbleKit.ArtScale"/>. So swapping the font on its own would put every line
    /// of dialogue on screen at a THIRD of its size, inside a panel three times too big for it.</para>
    ///
    /// <para><b>The compensation is the one the notebook already ships</b> (<c>CatalogBookPresenter</c>:
    /// <i>"the kit's bitmap face ignores fontSize and sets at its baked pixel size; the whole spread is
    /// scaled by Scale() instead"</i>): scale the TEXT, and give its rect the area it must fill divided by
    /// that scale. Doing it here rather than at eight call sites is the point — the conversion is one
    /// function with one test, instead of eight chances to divide the wrong way.</para>
    ///
    /// <para><b>The fallback stays exact.</b> With the face not imported the scale is 1 and the rect is
    /// the area unchanged, so the greybox is byte-for-byte the layout it was before this file existed —
    /// which is what makes the change safe to land before anybody has looked at it.</para>
    /// </summary>
    public static class BubbleFace
    {
        /// <summary>What a Text drawn in the BUILT-IN font is scaled by: nothing. A dynamic font honours
        /// <c>fontSize</c>, so the greybox needs no compensation and must not get one.</summary>
        public const float FallbackScale = 1f;

        /// <summary>What a Text drawn in the baked face is scaled by, so one glyph unit is one kit pixel
        /// at the bubble's own art scale.</summary>
        public const float FaceScale = DialogueBubbleKit.ArtScale;

        private static Font _face;
        private static bool _tried;

        /// <summary>
        /// The baked face, or null when it is not imported. Loaded once per session and cached — a
        /// <c>Resources.Load</c> per Text would be a file lookup per bubble piece.
        ///
        /// <para><b>⚠ Explicit <c>== null</c> on the cache, never <c>??</c></b>: a destroyed or unloaded
        /// asset is fake-null and the null-propagating operators sail straight past Unity's overloaded
        /// <c>==</c>, so a cached corpse would be handed out forever.</para>
        /// </summary>
        public static Font Face
        {
            get
            {
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
        /// match, because a look-alike font with the right name would still draw at the wrong size.</summary>
        public static bool IsFace(Font font) => font != null && ReferenceEquals(font, Face);

        /// <summary>How much a Text in <paramref name="font"/> must be scaled by. See the class remarks:
        /// the baked face draws at kit pixels and the built-in one honours <c>fontSize</c>.</summary>
        public static float ScaleFor(Font font) => IsFace(font) ? FaceScale : FallbackScale;

        /// <summary>
        /// The size a Text's rect needs, in its OWN units, to cover <paramref name="areaCanvasUnits"/>
        /// once it is drawn at <paramref name="scale"/>.
        ///
        /// <para>The whole conversion, and the reason it is a named function rather than a division at
        /// each call site: getting it upside down does not throw, does not fail to compile, and does not
        /// show up in any test that is not looking for it — it just makes the words nine times too big
        /// or nine times too small.</para>
        /// </summary>
        public static Vector2 RectFor(Vector2 areaCanvasUnits, float scale)
            => scale > 0f ? areaCanvasUnits / scale : areaCanvasUnits;

        /// <summary>The inverse: how much canvas area a Text of <paramref name="rect"/> covers when drawn
        /// at <paramref name="scale"/>. What a measurement off <c>preferredWidth</c> has to go through
        /// before it can be compared with a panel size.</summary>
        public static Vector2 AreaFor(Vector2 rect, float scale) => rect * scale;

        /// <summary>
        /// Dress <paramref name="text"/> in the bubble's font and apply the matching scale, in one call
        /// so the two can never disagree.
        ///
        /// <para><paramref name="fallbackFontSize"/> is set regardless and is meaningful ONLY on the
        /// built-in font; the baked face ignores it. Setting it anyway keeps the greybox exactly as it
        /// was, and keeps the value visible at the call site where a reader expects to find it.</para>
        /// </summary>
        public static void Wear(Text text, int fallbackFontSize)
        {
            if (text == null) return;
            Font font = Resolve();
            text.font = font;
            text.fontSize = fallbackFontSize;
            float scale = ScaleFor(font);
            text.rectTransform.localScale = new Vector3(scale, scale, 1f);
        }

        /// <summary>
        /// Give <paramref name="text"/> the rect it needs to cover <paramref name="areaCanvasUnits"/>,
        /// anchored at <paramref name="cornerAnchoredPosition"/> in its parent, top-left pivoted.
        ///
        /// <para>A top-left pivot on purpose: <c>localScale</c> grows a rect about its PIVOT, so a
        /// top-left one grows right and down — which is where a line of text goes, and which keeps the
        /// first glyph on the panel's inset corner whatever the scale.</para>
        /// </summary>
        public static void FitTopLeft(Text text, Vector2 areaCanvasUnits, Vector2 cornerAnchoredPosition)
        {
            if (text == null) return;
            RectTransform rt = text.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = RectFor(areaCanvasUnits, ScaleFor(text.font));
            rt.anchoredPosition = cornerAnchoredPosition;
        }

        /// <summary>Forget the cached face (fixtures, so one test's "not imported" cannot leak into the
        /// next). The game never calls it.</summary>
        public static void ForgetCache()
        {
            _face = null;
            _tried = false;
        }
    }
}
