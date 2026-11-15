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
    /// applied by <see cref="InteractChooserControls"/>; where the rows sit and what they say is
    /// <see cref="InteractChooserLayout"/>. This builds the strips once, feeds the intents, and repaints
    /// when <see cref="InteractChooser.Revision"/> moves.</para>
    ///
    /// <para><b>The popup's look, stacked</b>: each row is the popup's own strip and chalk line, in its
    /// corner. No bars, no numbers but the key you press. The selected row's strip is a touch stronger;
    /// a row that cannot be picked is faint chalk with its reason ("Sleep (taken)").</para>
    ///
    /// <para><b>Rule 7.</b> Rows are built ONCE (<see cref="InteractChoiceList.HardCap"/> of them) and
    /// reused. Row words are written only when the list opens; a selection change writes colours; a quiet
    /// frame reads one int and one intents struct, and computes the pointer's row with a handful of rect
    /// compares. Nothing allocates per frame.</para>
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
        private RectTransform _host;
        private readonly RectTransform[] _rows = new RectTransform[InteractChoiceList.HardCap];
        private readonly Image[] _strips = new Image[InteractChoiceList.HardCap];
        private readonly Text[] _labels = new Text[InteractChoiceList.HardCap];
        private readonly string[] _painted = new string[InteractChoiceList.HardCap];

        private readonly AxisLatch _latch = new AxisLatch();
        private IControlIntentSource<ChooserIntents> _source;

        private bool _shownOpen;
        private int _paintedRevision = -1;
        private int _paintedCount;
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
                Rect bottom = InteractPopupLayout.PanelScreenRect(HelmFootprint.Current, QuestNoteFootprint.Current,
                                                                  w, h);
                hitRow = InteractChooserLayout.HitRow(intents.Point, bottom, InteractChooser.Rows.Count,
                                                      HudBandLayout.ScaleFactor(w, h));
                moved = _hasLastPoint && intents.Point != _lastPoint;
            }
            _lastPoint = intents.Point;
            _hasLastPoint = true;

            InteractChooserControls.Apply(intents, _latch, hitRow, moved);
            Repaint();
        }

        private void Repaint()
        {
            if (_host == null) return;
            bool open = InteractChooser.IsOpen;
            if (open == _shownOpen && InteractChooser.Revision == _paintedRevision) return;
            _paintedRevision = InteractChooser.Revision;

            if (!open)
            {
                if (_shownOpen) _host.gameObject.SetActive(false);
                _shownOpen = false;
                return;
            }

            InteractChoiceList rows = InteractChooser.Rows;
            if (!_shownOpen)
            {
                // The list just went up: words and places, once for the life of this list (it is frozen).
                _paintedCount = Mathf.Min(rows.Count, _rows.Length);
                int w = HudScreen.Width, h = HudScreen.Height;
                Vector2 bottom = InteractPopupLayout.PanelAnchoredPosition(HelmFootprint.Current,
                                                                           QuestNoteFootprint.Current, w, h);
                for (int i = 0; i < _rows.Length; i++)
                {
                    bool used = i < _paintedCount;
                    if (_rows[i].gameObject.activeSelf != used) _rows[i].gameObject.SetActive(used);
                    if (!used) continue;
                    _rows[i].anchoredPosition = InteractChooserLayout.RowAnchoredPosition(bottom, i, _paintedCount);
                    InteractChoice c = rows[i];
                    string text = InteractChooserLayout.RowText(i, c.Label, c.Block);
                    if (!string.Equals(text, _painted[i], System.StringComparison.Ordinal))
                    {
                        _painted[i] = text;
                        _labels[i].text = text;
                    }
                }
                _host.gameObject.SetActive(true);
                _shownOpen = true;
            }

            for (int i = 0; i < _paintedCount; i++)
            {
                InteractChoice c = rows[i];
                bool selected = i == rows.Selected;
                _strips[i].color = selected ? SelectedInk : PanelInk;
                _labels[i].color = c.Enabled ? PaperUi.Chalk : PaperUi.ChalkFaint;
            }
        }

        // Awake builds in play; an EditMode test's AddComponent gets no Awake, so the first Tick does.
        private void EnsureBuilt()
        {
            if (_host == null) Build();
        }

        private void Build()
        {
            RectTransform screen = PaperUi.MakeScreen(transform, "InteractChooser_Canvas",
                                                      InteractPopupLayout.SortingOrder);
            _canvasGo = screen.parent.gameObject;

            var hostGo = new GameObject("Rows", typeof(RectTransform));
            hostGo.transform.SetParent(screen, false);
            _host = hostGo.GetComponent<RectTransform>();
            _host.anchorMin = Vector2.zero;
            _host.anchorMax = Vector2.one;
            _host.offsetMin = Vector2.zero;
            _host.offsetMax = Vector2.zero;

            for (int i = 0; i < _rows.Length; i++)
            {
                var go = new GameObject("Row" + (i + 1), typeof(RectTransform), typeof(Image));
                go.transform.SetParent(_host, false);
                RectTransform rt = go.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0f, 1f);
                rt.anchorMax = new Vector2(0f, 1f);
                rt.pivot = new Vector2(0f, 1f);
                rt.sizeDelta = InteractPopupLayout.PanelSize();
                _rows[i] = rt;

                Image strip = go.GetComponent<Image>();
                strip.color = PanelInk;
                // The pointer is hit-tested against the layout's own rects, never by a raycaster, so no
                // graphic here can eat a click meant for anything else.
                strip.raycastTarget = false;
                _strips[i] = strip;

                Text label = PaperUi.MakeText(rt, "", InteractPopupLayout.FontSize, TextAnchor.MiddleLeft,
                                              InteractPopupLayout.TextAnchoredPosition().x,
                                              InteractPopupLayout.TextAnchoredPosition().y,
                                              InteractPopupLayout.TextSize().x,
                                              InteractPopupLayout.TextSize().y,
                                              PaperUi.Chalk);
                label.horizontalOverflow = HorizontalWrapMode.Overflow;
                label.verticalOverflow = VerticalWrapMode.Truncate;
                label.raycastTarget = false;
                _labels[i] = label;
            }

            _host.gameObject.SetActive(false);
        }

        /// <summary>The popup's own strip tint, so a row and the popup are one material.</summary>
        private static readonly Color PanelInk = new Color(0.043f, 0.063f, 0.086f, 0.72f);

        /// <summary>The selected row: the same ink, a shade stronger and warmer. A change of weight, not a
        /// new colour, which is how the shell's menus mark the live line.</summary>
        private static readonly Color SelectedInk = new Color(0.18f, 0.2f, 0.22f, 0.9f);

        // ---- test seam --------------------------------------------------------------------------

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
