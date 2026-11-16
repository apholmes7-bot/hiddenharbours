using UnityEngine;
using UnityEngine.UI;
using HiddenHarbours.Core;

namespace HiddenHarbours.UI
{
    /// <summary>
    /// <b>The interaction chooser, drawn</b> — the small list the owner asked for on 2026-11-15: "when a
    /// player is standing in a place with multiple options a choice will appear … to select the desired
    /// action with keys or mouse".
    ///
    /// <para><b>It decides nothing.</b> The rows, their order, which are greyed and which is selected all
    /// live in Core (<see cref="InteractChooser"/>, <see cref="InteractChoiceList"/>); the hands are
    /// applied by <see cref="InteractChooserControls"/>; where things sit and what the rows say is
    /// <see cref="InteractChooserLayout"/>. This builds the pieces once, feeds the intents, and repaints
    /// when <see cref="InteractChooser.Revision"/> moves.</para>
    ///
    /// <para><b>The speech bubble's option list</b> (owner ruling: "the same design language as our
    /// notebook and chat bubbles"): one cream bubble panel with its ink ring and chamfered corners, the
    /// rows in the game's one face (<see cref="HarbourType"/>) in notebook ink, and the selected row
    /// carried by the kit's gold pill and pointing hand — the same three sprites the conversation bubble
    /// draws with, reached through <see cref="BubbleKitLink"/> (references, never copies). No tail: the
    /// list stands in the popup's screen corner, away from the player and the thing, and a tail would point
    /// at nothing.</para>
    ///
    /// <para><b>Rule 7.</b> Pieces are built ONCE (<see cref="InteractChoiceList.HardCap"/> rows) and
    /// reused. Row words are written only when the list opens; a selection change moves the pill and the
    /// hand and writes colours; a quiet frame reads one int and one intents struct, and computes the
    /// pointer's row with a handful of rect compares. Nothing allocates per frame.</para>
    ///
    /// <para>Self-installing like <see cref="InteractPopup"/>, for the same reason: the press it serves is
    /// built at runtime, so a scene that needed a builder re-run to show the list would be a scene where
    /// the list silently never appears.</para>
    /// </summary>
    [DefaultExecutionOrder(-41)]
    public sealed class InteractChooserView : MonoBehaviour
    {
        private static InteractChooserView _instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        public static void Install()
        {
            if (_instance != null) return;
            var go = new GameObject("InteractChooser");
            _instance = go.AddComponent<InteractChooserView>();
        }

        /// <summary>The installed view, or null.</summary>
        public static InteractChooserView Instance => _instance;

        [Tooltip("Persist across scene loads like the popup.")]
        [SerializeField] private bool _persistAcrossScenes = true;

        private GameObject _canvasGo;
        private RectTransform _panel;
        private Image _panelImage;
        private Image _pill;
        private Image _cursor;
        private bool _hasPanelArt, _hasPillArt, _hasCursorArt;
        private readonly Text[] _labels = new Text[InteractChoiceList.HardCap];
        private float _width = InteractChooserLayout.MinPanelWidth;
        private readonly string[] _painted = new string[InteractChoiceList.HardCap];

        private readonly AxisLatch _latch = new AxisLatch();
        private IControlIntentSource<ChooserIntents> _source;

        private bool _shownOpen;
        private int _paintedRevision = -1;
        private int _paintedCount;
        private int _paintedSelected = -1;
        private Vector2 _lastPoint;
        private bool _hasLastPoint;

        private void Awake()
        {
            if (_instance == null) _instance = this;
            EnsureBuilt();
            if (_persistAcrossScenes && Application.isPlaying) DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        /// <summary>Swap the intent source (a test's held source). Null returns to the device's.</summary>
        public void ConfigureChooserInput(IControlIntentSource<ChooserIntents> source) => _source = source;

        private void Update() => Tick();

        /// <summary>One frame: read the hands, apply them, repaint if anything moved. Public so an
        /// EditMode test can drive the real view without a player loop.</summary>
        public void Tick()
        {
            EnsureBuilt();
            IControlIntentSource<ChooserIntents> source = _source ?? ChooserInput.Device;
            ChooserIntents intents = source != null ? source.Read() : ChooserIntents.None;

            int hitRow = -1;
            bool moved = false;
            if (InteractChooser.IsOpen)
            {
                int w = HudScreen.Width, h = HudScreen.Height;
                Rect popup = InteractPopupLayout.PanelScreenRect(HelmFootprint.Current, QuestNoteFootprint.Current,
                                                                 w, h);
                hitRow = InteractChooserLayout.HitRow(intents.Point, popup, InteractChooser.Rows.Count,
                                                      HudBandLayout.ScaleFactor(w, h), _width);
                moved = _hasLastPoint && intents.Point != _lastPoint;
            }
            _lastPoint = intents.Point;
            _hasLastPoint = true;

            InteractChooserControls.Apply(intents, _latch, hitRow, moved);
            Repaint();
        }

        private void Repaint()
        {
            if (_panel == null) return;
            bool open = InteractChooser.IsOpen;
            if (open == _shownOpen && InteractChooser.Revision == _paintedRevision) return;
            _paintedRevision = InteractChooser.Revision;

            if (!open)
            {
                if (_shownOpen) _panel.gameObject.SetActive(false);
                _shownOpen = false;
                return;
            }

            InteractChoiceList rows = InteractChooser.Rows;
            if (!_shownOpen)
            {
                // The list just went up: words and places, once for the life of this list (it is frozen).
                _paintedCount = Mathf.Min(rows.Count, _labels.Length);
                float longest = 0f;
                for (int i = 0; i < _paintedCount; i++)
                {
                    InteractChoice c = rows[i];
                    string text = InteractChooserLayout.RowText(i, c.Label, c.Block);
                    if (!string.Equals(text, _painted[i], System.StringComparison.Ordinal))
                    {
                        _painted[i] = text;
                        _labels[i].text = text;
                    }
                    // The face is monospace, so its width is its length; the measured width is a floor for
                    // the built-in fallback font, which is not.
                    longest = Mathf.Max(longest, Mathf.Max(_labels[i].preferredWidth,
                                                           InteractChooserLayout.EstimateTextUnits(text)));
                }
                _width = InteractChooserLayout.PanelWidth(longest);

                int w = HudScreen.Width, h = HudScreen.Height;
                Vector2 popup = InteractPopupLayout.PanelAnchoredPosition(HelmFootprint.Current,
                                                                          QuestNoteFootprint.Current, w, h);
                // On whole SCREEN pixels (the speech bubble's own rule), so the face starts on the grid:
                // the right edge first, exactly as the popup snaps it, so the two end on one column; then
                // the left from it, and the top.
                Vector2 at = InteractChooserLayout.PanelAnchoredPosition(popup, _paintedCount, _width);
                float s = HudBandLayout.ScaleFactor(w, h);
                Vector2 span = InteractPopupLayout.SnapSpan(at.x, _width, s);
                _panel.sizeDelta = new Vector2(span.y, InteractChooserLayout.PanelHeight(_paintedCount));
                _panel.anchoredPosition = new Vector2(span.x, InteractPopupLayout.SnapToScreen(at.y, s));

                Vector2 textSize = InteractChooserLayout.TextSize(_width);
                for (int i = 0; i < _labels.Length; i++)
                {
                    bool used = i < _paintedCount;
                    GameObject row = _labels[i].gameObject;
                    if (row.activeSelf != used) row.SetActive(used);
                    if (used) _labels[i].rectTransform.sizeDelta = textSize;
                }
                _paintedSelected = -1;
                _panel.gameObject.SetActive(true);
                _shownOpen = true;
            }

            for (int i = 0; i < _paintedCount; i++)
                _labels[i].color = InteractChooserStyle.WordsFor(i == rows.Selected, rows[i].Enabled);

            int selected = rows.Selected;
            if (selected != _paintedSelected)
            {
                _paintedSelected = selected;
                bool valid = selected >= 0 && selected < _paintedCount;
                if (_pill.enabled != valid) _pill.enabled = valid;
                if (_cursor.enabled != valid) _cursor.enabled = valid;
                if (valid)
                {
                    float top = InteractChooserLayout.RowTop(selected);
                    // Both hang off the SAME row maths the words use, so the marks cannot drift from the row.
                    // x is the left inset: on a stretched rect with a left pivot, anchoredPosition.x IS
                    // offsetMin.x, so 0 here would slide the pill out of the panel's ring.
                    _pill.rectTransform.anchoredPosition = new Vector2(InteractChooserLayout.PillInset, top);
                    _cursor.rectTransform.anchoredPosition =
                        new Vector2(InteractChooserLayout.CursorX, top - InteractChooserLayout.RowHeight * 0.5f);
                }
            }
        }

        // Awake builds in play; an EditMode test's AddComponent gets no Awake, so the first Tick does.
        private void EnsureBuilt()
        {
            if (_panel == null) Build();
        }

        private void Build()
        {
            BubbleKitLink kit = BubbleKitLink.Shipped;
            Sprite panelArt = kit != null ? kit.Panel : null;
            Sprite pillArt = kit != null ? kit.Gold : null;
            Sprite cursorArt = kit != null ? kit.Cursor : null;
            _hasPanelArt = panelArt != null;
            _hasPillArt = pillArt != null;
            _hasCursorArt = cursorArt != null;

            RectTransform screen = PaperUi.MakeScreen(transform, "InteractChooser_Canvas",
                                                      InteractPopupLayout.SortingOrder);
            _canvasGo = screen.parent.gameObject;
            // A kit pixel is ArtScale reference units, so a 9-slice border of N kit pixels must draw N x
            // ArtScale units: the canvas's reference PPU is the sprites' PPU times the art scale (the
            // bubble's own canvas rule). The scaler owns that number and writes it to the canvas each
            // frame, so it is set on the scaler.
            float refPpu = DialogueBubbleKit.PixelsPerUnit * DialogueBubbleKit.ArtScale;
            CanvasScaler scaler = _canvasGo.GetComponent<CanvasScaler>();
            if (scaler != null) scaler.referencePixelsPerUnit = refPpu;
            Canvas canvas = _canvasGo.GetComponent<Canvas>();
            if (canvas != null) canvas.referencePixelsPerUnit = refPpu;

            var panelGo = new GameObject("Panel", typeof(RectTransform), typeof(Image));
            panelGo.transform.SetParent(screen, false);
            _panel = panelGo.GetComponent<RectTransform>();
            TopLeft(_panel, new Vector2(0f, 1f));
            _panel.sizeDelta = new Vector2(_width, InteractChooserLayout.PanelHeight(1));
            _panelImage = panelGo.GetComponent<Image>();
            Dress(_panelImage, panelArt, InteractChooserStyle.PanelTint(_hasPanelArt));

            // The pill first, so the words draw over it; stretched across the panel inside the kit's inset.
            var pillGo = new GameObject("Pill", typeof(RectTransform), typeof(Image));
            pillGo.transform.SetParent(_panel, false);
            RectTransform prt = pillGo.GetComponent<RectTransform>();
            prt.anchorMin = new Vector2(0f, 1f);
            prt.anchorMax = new Vector2(1f, 1f);
            prt.pivot = new Vector2(0f, 1f);
            prt.offsetMin = new Vector2(InteractChooserLayout.PillInset, -InteractChooserLayout.RowHeight);
            prt.offsetMax = new Vector2(-InteractChooserLayout.PillInset, 0f);
            _pill = pillGo.GetComponent<Image>();
            Dress(_pill, pillArt, InteractChooserStyle.PillTint(_hasPillArt));
            _pill.enabled = false;

            Font face = Resources.Load<Font>(HarbourType.ResourceKey);
            if (face == null) face = PaperUi.DefaultFont();
            for (int i = 0; i < _labels.Length; i++)
            {
                var go = new GameObject("Row" + (i + 1), typeof(RectTransform), typeof(Text));
                go.transform.SetParent(_panel, false);
                RectTransform rt = go.GetComponent<RectTransform>();
                TopLeft(rt, new Vector2(0f, 1f));
                rt.anchoredPosition = InteractChooserLayout.TextAnchoredPosition(i);
                rt.sizeDelta = InteractChooserLayout.TextSize(_width);

                Text label = go.GetComponent<Text>();
                label.font = face;
                label.fontSize = InteractChooserLayout.FontSize;
                // UPPER-left a whole number of kit pixels into the row, as the speech bubble's body is set;
                // never middle-aligned (see InteractChooserLayout.TextDrop).
                label.alignment = TextAnchor.UpperLeft;
                label.horizontalOverflow = HorizontalWrapMode.Overflow;
                label.verticalOverflow = VerticalWrapMode.Overflow;
                label.supportRichText = false;
                label.color = InteractChooserStyle.Words;
                // The pointer is hit-tested against the layout's own rects, never by a raycaster, so no
                // graphic here can eat a click meant for anything else.
                label.raycastTarget = false;
                _labels[i] = label;
                go.SetActive(false);
            }

            // The hand last, over everything, centred in the gutter.
            var cursorGo = new GameObject("Cursor", typeof(RectTransform), typeof(Image));
            cursorGo.transform.SetParent(_panel, false);
            RectTransform crt = cursorGo.GetComponent<RectTransform>();
            TopLeft(crt, new Vector2(0.5f, 0.5f));
            crt.sizeDelta = _hasCursorArt
                ? cursorArt.rect.size * InteractChooserLayout.Kit
                : Vector2.one * (InteractChooserLayout.FallbackCursorKitPixels * InteractChooserLayout.Kit);
            _cursor = cursorGo.GetComponent<Image>();
            _cursor.sprite = cursorArt;
            _cursor.type = Image.Type.Simple;
            _cursor.color = InteractChooserStyle.CursorTint(_hasCursorArt);
            _cursor.raycastTarget = false;
            _cursor.enabled = false;

            _panel.gameObject.SetActive(false);
        }

        private static void TopLeft(RectTransform rt, Vector2 pivot)
        {
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = pivot;
        }

        // The bubble's rule (DialoguePresenter.SetSprite): sliced only when the sprite carries a border.
        private static void Dress(Image image, Sprite art, Color tint)
        {
            image.sprite = art;
            image.type = art != null && art.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
            image.color = tint;
            image.raycastTarget = false;
        }

        // ---- test seam --------------------------------------------------------------------------

        /// <summary>The panel's drawn width, reference units (meaningful while showing).</summary>
        public float ShownWidth => _width;

        /// <summary>True when all three of the kit's pieces were linked (the panel, the pill, the hand).</summary>
        public bool HasKitArt => _hasPanelArt && _hasPillArt && _hasCursorArt;

        /// <summary>The sprites the panel, the pill and the hand wear (null where unlinked).</summary>
        public void ShownArt(out Sprite panel, out Sprite pill, out Sprite cursor)
        {
            panel = _panelImage != null ? _panelImage.sprite : null;
            pill = _pill != null ? _pill.sprite : null;
            cursor = _cursor != null ? _cursor.sprite : null;
        }

        /// <summary>Which drawn row the pill and the hand sit on (-1 when hidden), from their placed
        /// positions — the look, as numbers.</summary>
        public void ShownSelection(out int pillRow, out int cursorRow)
        {
            pillRow = _shownOpen && _pill.enabled ? RowAt(_pill.rectTransform.anchoredPosition.y) : -1;
            cursorRow = _shownOpen && _cursor.enabled
                ? RowAt(_cursor.rectTransform.anchoredPosition.y + InteractChooserLayout.RowHeight * 0.5f)
                : -1;
        }

        private int RowAt(float top)
        {
            for (int i = 0; i < _paintedCount; i++)
                if (Mathf.Abs(InteractChooserLayout.RowTop(i) - top) < 0.01f) return i;
            return -1;
        }

        /// <summary>Drawn row <paramref name="index"/>'s words colour.</summary>
        public Color ShownWords(int index)
            => _shownOpen && index >= 0 && index < _paintedCount ? _labels[index].color : default;

        /// <summary>The face the rows are set in.</summary>
        public Font ShownFace => _labels[0] != null ? _labels[0].font : null;

        /// <summary>True while the list is drawn.</summary>
        public bool IsShowing => _shownOpen;

        /// <summary>How many rows are drawn.</summary>
        public int ShownCount => _shownOpen ? _paintedCount : 0;

        /// <summary>The words on drawn row <paramref name="index"/>, or "".</summary>
        public string ShownRow(int index)
            => _shownOpen && index >= 0 && index < _paintedCount ? _painted[index] ?? "" : "";

        /// <summary>The canvas this view built, for teardown in one call.</summary>
        public GameObject CanvasObject => _canvasGo;
    }
}
