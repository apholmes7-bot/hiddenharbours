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
        public const int TitleSortingOrder    = 300;
        public const int PauseSortingOrder    = 310;
        public const int SettingsSortingOrder = 320;   // opens over either of the two above

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

        // ---- the title's wash (§7.8's "the title image is nearly free") ----------------------
        //
        // The title page is a COMPOSED SHOT of the harbour it is standing on, not a dimmed pause. The world
        // is already rendering behind it at the authored start hour — the save is not applied until the
        // player chooses, so the light behind the title is always first light — and the page's job is to
        // stay out of its way. So the wash is not flat: it is heavy down the LEFT, where the wordmark and
        // the menu are and chalk has to read, and nearly gone by the right-hand side, where the harbour is
        // left alone. Same hue at both ends, deliberately: the dawn is the world's, and a scrim that tinted
        // it would be painting over the one picture we have.

        /// <summary>The type edge of the title wash — solid enough that chalk reads on it.</summary>
        public static readonly Color WashNear = new Color(0.035f, 0.055f, 0.082f, 0.90f);

        /// <summary>The far edge, where the harbour is left to breathe. Deliberately thin: "the picture is
        /// the game", and an opaque page would be a loading screen with a menu on it.</summary>
        public static readonly Color WashFar  = new Color(0.035f, 0.055f, 0.082f, 0.26f);

        /// <summary>The letterbox bars. Near-black rather than black so they read as a frame around a
        /// picture rather than as the screen having stopped.</summary>
        public static readonly Color FrameInk = new Color(0.016f, 0.024f, 0.035f, 0.96f);

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

        /// <summary>
        /// Show or hide a whole page — its content AND its scrim, which is why this toggles the CANVAS
        /// rather than the content host. Two stacked scrims read as near-black, so a page that opens over
        /// another (the settings sheet) hides the one underneath entirely rather than dimming it twice.
        /// </summary>
        public static void SetPageVisible(RectTransform host, bool visible)
        {
            if (host == null || host.parent == null) return;
            var canvas = host.parent.gameObject;
            if (canvas.activeSelf != visible) canvas.SetActive(visible);
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
        /// A menu line that also shows a value on its right — "Display … Windowed". Activating it changes
        /// the value; <paramref name="valueLabel"/> is the handle to write the new one into. A row rather
        /// than a checkbox because a chalk line reading "Windowed" says what it IS, where a tick box only
        /// says whether something abstract is on.
        /// </summary>
        public static Button MakeValueRow(RectTransform parent, string name, string label, string value,
                                          float x, float y, float w, float h, UnityAction onActivate,
                                          out Text valueLabel)
        {
            Button btn = MakeMenuItem(parent, name, label, x, y, w, h, onActivate);
            valueLabel = MakeText((RectTransform)btn.transform, value, 26, TextAnchor.MiddleRight,
                                  0f, 0f, w - 24f, h, ChalkFaint);
            return btn;
        }

        /// <summary>
        /// A labelled fader: the name on the left, a bar in the middle, the number on the right. The
        /// number is there because a bar alone is not a readable value, and because "is my music actually
        /// off?" should be answerable without leaning at the screen (§8's redundant coding).
        ///
        /// <para>Keyboard and gamepad drive it for free — a uGUI <see cref="Slider"/> takes left/right when
        /// selected. The readout rebuilds only when the whole percent changes, so a drag does not allocate
        /// a string a frame.</para>
        /// </summary>
        public static Slider MakeSlider(RectTransform parent, string name, string label, float value01,
                                        float x, float y, float w, float h, UnityAction<float> onChanged)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Slider));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta = new Vector2(w, h);

            MakeText(rt, label, 28, TextAnchor.MiddleLeft, 0f, 0f, LabelW, h, Chalk);
            Text readout = MakeText(rt, ShellStrings.VolumePercent(value01), 26, TextAnchor.MiddleRight,
                                    w - ReadoutW, 0f, ReadoutW, h, ChalkFaint);

            // The bar: a dim trough with a chalk fill inside it. No handle — the fill's own edge is the
            // position, which is quieter and one less thing to draw. The fill must be a CHILD of the
            // trough: uGUI's Slider drives the fill's anchors within its parent, and maps a click to a
            // value using that same parent's rectangle.
            float barX = LabelW + 12f;
            float barW = w - LabelW - ReadoutW - 24f;
            // White at full alpha, so the ColorBlock below carries the whole normal/hover/selected story
            // in one channel — the same trick the menu lines use.
            RectTransform trough = MakeBar(rt, "Trough", barX, -(h - BarH) * 0.5f, barW, BarH, Color.white);
            RectTransform fill = MakeStretchedFill(trough, Chalk);

            var slider = go.GetComponent<Slider>();
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.wholeNumbers = false;
            slider.fillRect = fill;
            slider.handleRect = null;
            slider.targetGraphic = trough.GetComponent<Image>();
            slider.transition = Selectable.Transition.ColorTint;
            slider.colors = new ColorBlock
            {
                normalColor      = TroughInk,
                highlightedColor = TroughLit,
                pressedColor     = TroughLit,
                selectedColor    = TroughLit,
                disabledColor    = new Color(1f, 1f, 1f, 0.05f),
                colorMultiplier  = 1f,
                fadeDuration     = 0.08f,
            };
            slider.SetValueWithoutNotify(value01);

            int lastPercent = Mathf.RoundToInt(value01 * 100f);
            slider.onValueChanged.AddListener(v =>
            {
                onChanged?.Invoke(v);
                int percent = Mathf.RoundToInt(v * 100f);
                if (percent == lastPercent) return;     // same number → same string → nothing to build
                lastPercent = percent;
                readout.text = ShellStrings.VolumePercent(v);
            });

            go.AddComponent<MenuItemMarker>().Bind(
                MakeText(rt, string.Empty, 26, TextAnchor.MiddleCenter, -34f, 0f, 30f, h, Chalk));
            return slider;
        }

        private const float LabelW   = 200f;   // the fader's name column
        private const float ReadoutW = 80f;    // "100%" and no wider
        private const float BarH     = 10f;

        private static readonly Color TroughInk = new Color(1f, 1f, 1f, 0.13f);
        private static readonly Color TroughLit = new Color(1f, 1f, 1f, 0.24f);

        /// <summary>A flat bar, top-left anchored. The fill one is handed to the Slider, which resizes it.</summary>
        private static RectTransform MakeBar(RectTransform parent, string name, float x, float y,
                                             float w, float h, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta = new Vector2(w, h);

            var img = go.GetComponent<Image>();
            img.color = color;
            img.raycastTarget = true;
            return rt;
        }

        /// <summary>The chalk inside the trough: stretched to its parent, since the Slider expresses the
        /// value by driving this rect's ANCHORS (and leaves its offsets alone).</summary>
        private static RectTransform MakeStretchedFill(RectTransform parent, Color color)
        {
            var go = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;

            var img = go.GetComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return rt;
        }

        /// <summary>
        /// Wire a column of menu lines into an explicit up/down loop and give the first one the selection.
        /// Explicit rather than uGUI's automatic navigation: automatic infers neighbours from screen
        /// geometry, which quietly changes meaning when a line is hidden (no save → no Continue) — the
        /// order the page was built in is the order the player should walk.
        /// </summary>
        public static void WireVerticalNavigation(Selectable[] items, int selectIndex = 0)
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

        // ---- framing the shot ----------------------------------------------------------------

        /// <summary>
        /// The title's wash: <see cref="MakeScrim"/>'s job, done as a ramp instead of a flat tint, so the
        /// page can be dense under the type and thin over the harbour. <paramref name="horizontal"/> runs
        /// it left→right (the title's case — the type column is on the left); otherwise bottom→top.
        /// </summary>
        public static Image MakeWash(RectTransform host, Color near, Color far, bool horizontal)
        {
            Image img = MakeScrim(host, Color.white);   // white, so the ramp carries the whole colour
            img.gameObject.name = "Wash";
            img.gameObject.AddComponent<ShellGradient>().Set(near, far, horizontal);
            return img;
        }

        /// <summary>
        /// Two bars across the top and bottom of the screen — the frame that says "this is a picture of the
        /// harbour" rather than "the game has stopped and here is a menu". Sized as a FRACTION of screen
        /// height rather than in reference pixels, so the frame is the same shape on every window.
        ///
        /// <para>Anchored to the CANVAS, like the scrim, so an odd aspect cannot leave a sliver of
        /// unframed game outside the 1280x720 reference rectangle. Drawn under the page's content, which
        /// is why the shell keeps its footer line clear of the lower bar rather than over it.</para>
        /// </summary>
        public static void MakeLetterbox(RectTransform host, float heightFraction, Color color)
        {
            if (host == null || host.parent == null) return;
            float f = Mathf.Clamp(heightFraction, 0f, 0.4f);
            if (f <= 0f) return;

            MakeBand(host.parent, "LetterboxTop", new Vector2(0f, 1f - f), Vector2.one, color);
            MakeBand(host.parent, "LetterboxBottom", Vector2.zero, new Vector2(1f, f), color);
        }

        /// <summary>A full-width band pinned by normalised anchors to the canvas — no fixed sizes, so it
        /// stretches with the window.</summary>
        private static void MakeBand(Transform canvas, string name, Vector2 anchorMin, Vector2 anchorMax,
                                     Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(canvas, false);
            go.transform.SetSiblingIndex(1);   // over the scrim/wash, under the page's content

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;

            var img = go.GetComponent<Image>();
            img.color = color;
            img.raycastTarget = false;   // the bars are decoration; the scrim under them eats the clicks
        }
    }
}
