using UnityEngine;
using UnityEngine.UI;

namespace HiddenHarbours.UI
{
    /// <summary>
    /// One window's CHROME visuals (2026-08-07 windowing ruling): the title strip, the collapse-tier
    /// and × buttons, and the corner resize grip. Pure presentation — every hit box comes from
    /// <see cref="BoatUiWindowLayout"/>, and this class only ever draws the same rects, so the picture
    /// and the press can never disagree.
    ///
    /// <para><b>Hover-only, deliberately.</b> The diegetic-UI canon keeps the sea clean; a permanent
    /// strip over every instrument would be a window manager wearing the game. The chrome appears
    /// while the pointer is over the window (or a drag is live), and a BAR-collapsed window shows its
    /// strip always — the strip is all there is. Greybox look (flat quads + two glyphs); the
    /// weathered skin is art-pipeline's lane.</para>
    ///
    /// <para><b>Perf (rule 7):</b> plain uGUI objects whose rects are set only when they CHANGE; no
    /// per-frame allocation, no raster — the strip is a tinted quad, not a texture.</para>
    /// </summary>
    public sealed class BoatUiWindowChrome
    {
        // Greybox chrome tints — presentation placeholders (the skin is art-pipeline's), kept
        // deliberately dim so the instruments stay the read.
        private static readonly Color BarTint = new Color(0.07f, 0.09f, 0.11f, 0.82f);
        private static readonly Color ButtonTint = new Color(0.16f, 0.19f, 0.22f, 0.9f);
        private static readonly Color GripTint = new Color(0.85f, 0.88f, 0.9f, 0.35f);
        private static readonly Color GlyphTint = new Color(0.85f, 0.88f, 0.9f, 0.95f);

        private readonly GameObject _root;
        private readonly RectTransform _bar, _collapseBtn, _hideBtn, _grip;
        private readonly Text _collapseGlyph, _hideGlyph;

        private Rect _shownCard = new Rect(float.NaN, 0f, 0f, 0f);
        private BoatUiCollapse _shownTier = (BoatUiCollapse)(-1);
        private bool _visible;

        public BoatUiWindowChrome(Transform canvasParent, string name)
        {
            _root = new GameObject(name, typeof(RectTransform));
            _root.transform.SetParent(canvasParent, false);
            var rootRect = (RectTransform)_root.transform;
            rootRect.anchorMin = rootRect.anchorMax = Vector2.zero;
            rootRect.pivot = Vector2.zero;

            _bar = MakeQuad(_root.transform, "Bar", BarTint);
            _collapseBtn = MakeQuad(_root.transform, "CollapseBtn", ButtonTint);
            _hideBtn = MakeQuad(_root.transform, "HideBtn", ButtonTint);
            _grip = MakeQuad(_root.transform, "Grip", GripTint);
            _collapseGlyph = MakeGlyph(_collapseBtn, "-");
            _hideGlyph = MakeGlyph(_hideBtn, "×");   // ×

            _root.SetActive(false);
        }

        /// <summary>Lay the chrome out for this frame. Rects are only written on a change.</summary>
        public void Show(Rect card, BoatUiCollapse tier, in HiddenHarbours.Core.BoatUiWindowSettings cfg)
        {
            if (!_visible)
            {
                _root.SetActive(true);
                _visible = true;
            }
            if (card == _shownCard && tier == _shownTier) return;
            _shownCard = card;
            _shownTier = tier;

            Rect bar = BoatUiWindowLayout.BarRect(card, in cfg);
            Place(_bar, bar);
            Place(_hideBtn, BoatUiWindowLayout.HideButtonRect(bar, in cfg));
            Place(_collapseBtn, BoatUiWindowLayout.CollapseButtonRect(bar, in cfg));

            Rect grip = BoatUiWindowLayout.GripRect(card, in cfg);
            bool hasGrip = grip.width > 0f;
            if (_grip.gameObject.activeSelf != hasGrip) _grip.gameObject.SetActive(hasGrip);
            if (hasGrip) Place(_grip, grip);

            // The tier button says where the NEXT press goes: shrink (−), fold to the bar (=), or
            // open back up (+). Glyphs, not words — no localizable strings in the chrome.
            _collapseGlyph.text = tier switch
            {
                BoatUiCollapse.Full => "-",
                BoatUiCollapse.Compact => "=",
                _ => "+",
            };
        }

        /// <summary>Take the chrome off screen.</summary>
        public void Hide()
        {
            if (!_visible) return;
            _root.SetActive(false);
            _visible = false;
            _shownCard = new Rect(float.NaN, 0f, 0f, 0f);
        }

        /// <summary>Destroy the built objects (host teardown).</summary>
        public void Destroy()
        {
            if (_root != null) Object.Destroy(_root);
        }

        private static void Place(RectTransform rt, Rect r)
        {
            rt.anchoredPosition = new Vector2(r.xMin, r.yMin);
            rt.sizeDelta = new Vector2(r.width, r.height);
        }

        private static RectTransform MakeQuad(Transform parent, string name, Color tint)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = Vector2.zero;
            rt.pivot = Vector2.zero;
            var img = go.GetComponent<Image>();
            img.color = tint;
            img.raycastTarget = false;   // the hosts hit-test themselves; never block other UI
            return rt;
        }

        private static Text MakeGlyph(RectTransform button, string glyph)
        {
            var go = new GameObject("Glyph", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(button, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var text = go.GetComponent<Text>();
            var f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (f == null) f = Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.font = f;
            text.fontSize = 12;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = GlyphTint;
            text.raycastTarget = false;
            text.text = glyph;
            return text;
        }
    }
}
