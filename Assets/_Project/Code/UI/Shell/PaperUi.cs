using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace HiddenHarbours.UI
{
    /// <summary>
    /// The shell's drawing kit (M1 §7.8) — the small set of builders every shell page is made of, so the
    /// title, the settings sheet and the pause menu are one surface rather than three that drifted.
    ///
    /// <para><b>The sensibility is paper and chalk, not chrome.</b> No panels-within-panels, no gloss, no
    /// AAA frame: a dusk scrim over the live harbour, chalk-coloured type on it, hairline rules, and menu
    /// lines that are text with a faint strip behind them. The tide table is ink on warm stock because it
    /// is an object in the world; the shell is the quiet dark around the game, so it is the same hand in a
    /// different light.</para>
    ///
    /// <para><b>Built in code, like every other screen here</b> (HudController, TidePanel, the market
    /// screens) — no prefab to author, nothing for a scene builder to re-bake, and it works headless in a
    /// PlayMode test. Every page is built ONCE when it opens and does no per-frame work (rule 7): the
    /// world behind it is stopped, so there is nothing to repaint.</para>
    ///
    /// <para>Layout convention, shared with <c>TidePanel</c>: every builder anchors to its parent's
    /// TOP-LEFT with a matching pivot, so x grows right and y grows DOWN (negative) from the corner.</para>
    /// </summary>
    public static class PaperUi
    {
        // Above the HUD (100), the market screens (200/205) and the tide table (210): the shell is the
        // frame around all of it and nothing may draw over it.
        public const int TitleSortingOrder = 300;
        public const int PauseSortingOrder = 310;

        /// <summary>Dusk over the harbour — dims the live view the page sits on without hiding it.</summary>
        public static readonly Color Scrim      = new Color(0.043f, 0.063f, 0.086f, 0.78f);
        /// <summary>Chalk: the shell's ink, read against the scrim.</summary>
        public static readonly Color Chalk      = new Color(0.937f, 0.914f, 0.855f, 1f);
        /// <summary>Chalk gone quiet — hints, detail lines, anything that must not compete.</summary>
        public static readonly Color ChalkFaint = new Color(0.937f, 0.914f, 0.855f, 0.55f);
        /// <summary>A hairline rule under a heading.</summary>
        public static readonly Color RuleInk    = new Color(0.937f, 0.914f, 0.855f, 0.22f);
        /// <summary>The warning colour for a line that destroys something (dried rust, as the tide table's
        /// low water). Always paired with words that say the same thing — never colour alone (§8).</summary>
        public static readonly Color WarnInk    = new Color(0.847f, 0.588f, 0.451f, 1f);

        // A menu line's strip. White at low alpha, so the Button's ColorBlock carries the whole
        // normal/hover/selected/pressed story in one channel.
        private static readonly Color StripNormal      = new Color(1f, 1f, 1f, 0.05f);
        private static readonly Color StripHighlighted = new Color(1f, 1f, 1f, 0.14f);
        private static readonly Color StripPressed     = new Color(1f, 1f, 1f, 0.22f);
        private static readonly Color StripDisabled    = new Color(1f, 1f, 1f, 0.02f);

        /// <summary>The reference the shell lays out in — the project's landscape PC-first canvas.</summary>
        public static readonly Vector2 ReferenceResolution = new Vector2(1280f, 720f);

        /// <summary>
        /// A full-screen overlay canvas with a TOP-LEFT-pivoted content host filling it, returned ready to
        /// lay out in (x right, y down). <paramref name="sortingOrder"/> places it in the stack above.
        /// </summary>
        public static RectTransform MakeScreen(Transform parent, string name, int sortingOrder)
        {
            var canvasGo = new GameObject(name,
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(parent, false);

            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = ReferenceResolution;
            scaler.matchWidthOrHeight = 0.5f;

            // The host is the reference rectangle itself, corner-pinned — so a page's coordinates mean the
            // same thing on any window size and every builder below can speak in one frame.
            var host = new GameObject("Content", typeof(RectTransform));
            host.transform.SetParent(canvasGo.transform, false);
            var rt = host.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = ReferenceResolution;
            return rt;
        }

        /// <summary>The dusk wash over the live world, sized to the whole screen (not the reference host)
        /// so no sliver of undimmed game shows at an odd aspect. It eats clicks meant for the world.</summary>
        public static Image MakeScrim(RectTransform host, Color color)
        {
            var go = new GameObject("Scrim", typeof(RectTransform), typeof(Image));
            // Parented to the CANVAS rather than the fixed-size host, then stretched — the host is a
            // 1280x720 rectangle, and stretching inside it would leave the letterboxed remainder bare.
            go.transform.SetParent(host.parent, false);
            go.transform.SetAsFirstSibling();
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;

            var img = go.GetComponent<Image>();
            img.color = color;
            img.raycastTarget = true;
            return img;
        }

        /// <summary>A label, top-left anchored (x right, y down).</summary>
        public static Text MakeText(RectTransform parent, string text, int fontSize, TextAnchor align,
                                    float x, float y, float w, float h, Color color)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta = new Vector2(w, h);

            var t = go.GetComponent<Text>();
            t.text = text;
            t.font = DefaultFont();
            t.fontSize = fontSize;
            t.alignment = align;
            t.color = color;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            return t;
        }

        /// <summary>A hairline rule — the only decoration the shell owns.</summary>
        public static void MakeRule(RectTransform parent, float x, float y, float w)
        {
            var go = new GameObject("Rule", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta = new Vector2(w, 2f);

            var img = go.GetComponent<Image>();
            img.color = RuleInk;
            img.raycastTarget = false;
        }

        /// <summary>
        /// A menu line: a word on a faint strip, with a chalk tick that appears beside it when it takes
        /// the selection. The tick is the SHAPE channel — keyboard and gamepad users must be able to see
        /// which line is theirs without relying on the strip's tint alone (charter DoD, §8).
        /// </summary>
        public static Button MakeMenuItem(RectTransform parent, string name, string label,
                                          float x, float y, float w, float h, UnityAction onClick,
                                          Color? labelColor = null)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta = new Vector2(w, h);

            var img = go.GetComponent<Image>();
            img.color = Color.white;      // the ColorBlock below carries the whole state story
            img.raycastTarget = true;

            var btn = go.GetComponent<Button>();
            btn.targetGraphic = img;
            btn.transition = Selectable.Transition.ColorTint;
            btn.colors = new ColorBlock
            {
                normalColor      = StripNormal,
                highlightedColor = StripHighlighted,
                pressedColor     = StripPressed,
                selectedColor    = StripHighlighted,
                disabledColor    = StripDisabled,
                colorMultiplier  = 1f,
                fadeDuration     = 0.08f,
            };
            if (onClick != null) btn.onClick.AddListener(onClick);

            Text marker = MakeText(rt, string.Empty, 26, TextAnchor.MiddleCenter, 0f, 0f, 34f, h, Chalk);
            MakeText(rt, label, 30, TextAnchor.MiddleLeft, 36f, 0f, w - 48f, h,
                     labelColor ?? Chalk);

            go.AddComponent<MenuItemMarker>().Bind(marker);
            return btn;
        }

        /// <summary>
        /// Wire a column of menu lines into an explicit up/down loop and give the first one the selection.
        /// Explicit rather than uGUI's automatic navigation: automatic infers neighbours from screen
        /// geometry, which quietly changes meaning when a line is hidden (no save → no Continue) — the
        /// order the page was built in is the order the player should walk.
        /// </summary>
        public static void WireVerticalNavigation(Button[] items, int selectIndex = 0)
        {
            if (items == null || items.Length == 0) return;

            for (int i = 0; i < items.Length; i++)
            {
                if (items[i] == null) continue;
                var nav = new Navigation { mode = Navigation.Mode.Explicit };
                nav.selectOnUp   = items[(i - 1 + items.Length) % items.Length];
                nav.selectOnDown = items[(i + 1) % items.Length];
                items[i].navigation = nav;
            }

            var es = EventSystem.current;
            if (es != null && selectIndex >= 0 && selectIndex < items.Length && items[selectIndex] != null)
                es.SetSelectedGameObject(items[selectIndex].gameObject);
        }

        /// <summary>
        /// Make sure something is listening for clicks and for the keyboard/gamepad. Same module and
        /// default actions the tide table and the market screens install (new Input System — the
        /// project's backend; legacy <c>UnityEngine.Input</c> compiles here and then throws at runtime).
        /// A failure to bind the default actions degrades the input, never the page.
        /// </summary>
        public static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;

            var es = new GameObject("EventSystem", typeof(EventSystem));
            var module = es.AddComponent<InputSystemUIInputModule>();
            try { module.AssignDefaultActions(); }
            catch (System.Exception e) { Debug.LogWarning("[Shell] No default UI actions: " + e.Message); }
            Object.DontDestroyOnLoad(es);
        }

        /// <summary>Unity 6 removed Arial.ttf from Resources; LegacyRuntime.ttf is the built-in fallback.
        /// The shell's real face arrives with the wordmark art, not before the playtest verdict.</summary>
        public static Font DefaultFont()
        {
            var f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (f == null) f = Resources.GetBuiltinResource<Font>("Arial.ttf");
            return f;
        }
    }
}
