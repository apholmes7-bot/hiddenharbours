using UnityEngine;
using UnityEngine.UI;
using HiddenHarbours.Core;

namespace HiddenHarbours.Fishing
{
    /// <summary>
    /// The TRANSIENT rod gauge for the fishing fight (VS-13) — distinct from the persistent
    /// clock/tide/money HUD (ui-ux's, VS-17). It shows only during a bite/fight and reads the
    /// interaction purely through the Core <see cref="FishingStateChanged"/> signal, so it touches no
    /// Fishing internals — which is exactly how ui-ux will re-skin/relocate it into the formal HUD later
    /// (VS-14) without changing the logic. Flagged in the PR as art/ui-ux polish territory.
    ///
    /// Self-contained &amp; code-driven (mirrors HudController): builds its own ScreenSpaceOverlay Canvas
    /// in Awake from the imported UI art (TensionGauge / LineHook / FishOnSilhouette), with null-safe
    /// coloured-rect fallbacks so it still builds before any art exists. The live tension/landing bars
    /// fill via RectTransform anchors (no sprite needed), so they animate even headless.
    /// </summary>
    [DefaultExecutionOrder(-40)]
    public sealed class RodGaugeView : MonoBehaviour
    {
        [Header("Art (assigned by the greybox builder; null = coloured-rect fallback)")]
        [SerializeField] private Sprite _gaugeSprite;     // TensionGauge.png — strain meter + snap zone
        [SerializeField] private Sprite _lineHookSprite;  // LineHook.png — line/hook motif
        [SerializeField] private Sprite _fishSprite;      // FishOnSilhouette.png — the fish on the line

        [Header("Display tuning")]
        [Tooltip("Tension fraction at/above which the strain bar reads as the red SNAP zone (shape+colour+text).")]
        [SerializeField] private float _snapZone = 0.75f;

        [Header("On-the-line struggle (VS-14 catch-feel; gameplay-systems FYI)")]
        [Tooltip("Jitter amplitude (px) of the on-line fish at calm tension vs. at full frantic.")]
        [SerializeField] private float _struggleAmpCalm = 3f;
        [SerializeField] private float _struggleAmpFrantic = 26f;
        [Tooltip("Shake frequency of the on-line fish at calm tension vs. at full frantic.")]
        [SerializeField] private float _struggleFreqCalm = 5f;
        [SerializeField] private float _struggleFreqFrantic = 22f;
        [Tooltip("Peak lurch rotation (degrees) of the on-line fish at full frantic.")]
        [SerializeField] private float _struggleMaxRotation = 14f;
        [Tooltip("How strongly a sudden RISE in live tension (a surge) spikes the franticness.")]
        [SerializeField] private float _surgeGain = 6f;
        [Tooltip("How fast a surge calms back to zero (per real second).")]
        [SerializeField] private float _surgeDecay = 1.6f;

        private RectTransform _panel;
        private Image _tensionFill;
        private Image _landingFill;
        private Image _fishIcon;
        private Text _statusLabel;

        // ---- live struggle state (drives the per-frame fish animation; all GC-free) ----------
        private Vector2 _fishHome;                          // the fish's resting anchored position (jitter origin)
        private FishingPhase _lastPhase = FishingPhase.Idle;
        private float _lastTension;                         // previous snapshot's strain — for surge detection
        private float _surge;                               // 0..1 decaying "a surge just hit" intensity

        private static readonly Color CalmTension  = new Color(0.45f, 0.85f, 0.55f);
        private static readonly Color HotTension   = new Color(0.92f, 0.30f, 0.25f);
        private static readonly Color LandingColor = new Color(0.45f, 0.70f, 0.95f);

        private void Awake() => Build();
        private void OnEnable()  => EventBus.Subscribe<FishingStateChanged>(OnState);
        private void OnDisable() => EventBus.Unsubscribe<FishingStateChanged>(OnState);

        private void OnState(FishingStateChanged e)
        {
            FeedStruggle(e.State);
            Render(e.State);
        }

        // ---- on-the-line struggle (Update-driven, no per-frame GC) ---------------------------

        /// <summary>
        /// Track live strain and detect surges from the same <see cref="FishingState"/> the gauge already
        /// consumes — a sudden RISE in tension reads as the fish lurching, which spikes the franticness.
        /// </summary>
        private void FeedStruggle(FishingState s)
        {
            bool fighting = s.Phase == FishingPhase.Fighting || s.Phase == FishingPhase.Tending;
            if (fighting)
            {
                float rise = s.Tension01 - _lastTension; // a positive jump = the fish just surged
                if (rise > 0f) _surge = Mathf.Clamp01(_surge + rise * _surgeGain);
            }
            else
            {
                _surge = 0f; // bite/result/idle: nothing's pulling
            }
            _lastTension = s.Tension01;
            _lastPhase = s.Phase;
        }

        /// <summary>
        /// Animate the on-line fish so it feels alive while hooked: a fast tug/jitter plus a slower
        /// lurch, both scaled by the live tension and amplified by a surge. Settles to rest the instant
        /// the fight ends (landed/snapped). Allocation-free (struct maths only) — CLAUDE.md rule 7.
        /// </summary>
        private void Update()
        {
            if (_fishIcon == null || _panel == null || !_panel.gameObject.activeSelf) return;

            var rt = _fishIcon.rectTransform;

            bool fighting = _lastPhase == FishingPhase.Fighting || _lastPhase == FishingPhase.Tending;
            if (!fighting || !_fishIcon.enabled)
            {
                // At rest (bite / result beat / idle): the fish sits still — no struggle.
                if (_surge != 0f) _surge = 0f;
                rt.anchoredPosition = _fishHome;
                rt.localRotation = Quaternion.identity;
                return;
            }

            float dt = Time.unscaledDeltaTime;
            _surge = Mathf.MoveTowards(_surge, 0f, _surgeDecay * dt);

            // Franticness: the live strain plus any unspent surge from a recent tension rise.
            float frantic = Mathf.Clamp01(_lastTension + _surge);
            float t = Time.unscaledTime;

            float freq = Mathf.Lerp(_struggleFreqCalm, _struggleFreqFrantic, frantic);
            float amp  = Mathf.Lerp(_struggleAmpCalm,  _struggleAmpFrantic,  frantic);

            // Organic tug/jitter: two detuned sines for the shake + a slower yank a surge amplifies.
            float shakeX = Mathf.Sin(t * freq) * amp;
            float shakeY = Mathf.Sin(t * freq * 1.43f + 1.1f) * amp * 0.7f;
            float lurch  = Mathf.Sin(t * 2.7f) * _surge * amp; // big, slow yank during a surge
            rt.anchoredPosition = _fishHome + new Vector2(shakeX + lurch, shakeY);

            // Lurch rotation — the fish twisting on the line, scaled by franticness.
            float rot = Mathf.Sin(t * freq * 0.8f + 0.5f) * _struggleMaxRotation * frantic;
            rt.localRotation = Quaternion.Euler(0f, 0f, rot);
        }

        // ---- rendering ----------------------------------------------------------------------

        private void Render(FishingState s)
        {
            if (_panel == null) return;

            bool show = s.IsActive;
            if (_panel.gameObject.activeSelf != show) _panel.gameObject.SetActive(show);
            if (!show) return;

            // Strain bar: fill + a red SNAP-zone tint (redundant with the status text, never colour alone).
            SetFill(_tensionFill, s.Tension01);
            _tensionFill.color = s.Tension01 >= _snapZone ? HotTension : CalmTension;

            // Landing gauge: how landed the fish is.
            SetFill(_landingFill, s.Landing01);

            // Fish silhouette: shown once hooked, sized by weight so a bigger fish reads bigger.
            if (_fishIcon != null)
            {
                _fishIcon.enabled = s.FishId != null;
                float scale = Mathf.Clamp(s.WeightKg / 6f, 0.5f, 1.6f);
                _fishIcon.rectTransform.localScale = new Vector3(scale, scale, 1f);
            }

            if (_statusLabel != null) _statusLabel.text = StatusText(s);
        }

        private static string StatusText(FishingState s) => s.Phase switch
        {
            FishingPhase.Waiting  => "Line out…",
            FishingPhase.Bite     => "A bite! Hook it!",
            FishingPhase.Fighting => s.DisplayName != null ? "Fighting " + s.DisplayName + "…" : "Fighting…",
            FishingPhase.Tending  => "Gathering…",
            FishingPhase.Landed   => s.DisplayName != null ? "Landed " + s.DisplayName + "!" : "Landed!",
            FishingPhase.Snapped  => "It threw the hook!",
            FishingPhase.NoBite   => "Nothing biting…",
            _                     => string.Empty,
        };

        private static void SetFill(Image fill, float amount)
        {
            if (fill == null) return;
            var rt = fill.rectTransform;
            rt.anchorMax = new Vector2(Mathf.Clamp01(amount), 1f); // left-anchored bar grows rightward
        }

        // ---- construction (code-driven, no prefab) ------------------------------------------

        private void Build()
        {
            var canvasGo = new GameObject("RodGauge_Canvas",
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);

            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 90; // below the persistent top-band HUD (100), above the world

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0.5f;

            // Panel: lower-centre, clear of the top read-band and the bottom thumb controls.
            var panelGo = new GameObject("Panel", typeof(RectTransform));
            panelGo.transform.SetParent(canvasGo.transform, false);
            _panel = panelGo.GetComponent<RectTransform>();
            _panel.anchorMin = new Vector2(0.5f, 0f);
            _panel.anchorMax = new Vector2(0.5f, 0f);
            _panel.pivot = new Vector2(0.5f, 0f);
            _panel.anchoredPosition = new Vector2(0f, 420f); // above the bottom thumb band
            _panel.sizeDelta = new Vector2(760f, 280f);

            // Fish silhouette on the line (left), shown once hooked.
            _fishIcon = MakeImage(_panel, "FishSilhouette", _fishSprite, Color.white,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(70f, -70f), new Vector2(120f, 120f));
            _fishIcon.preserveAspect = true;
            _fishIcon.enabled = false;
            _fishHome = _fishIcon.rectTransform.anchoredPosition; // jitter origin for the struggle

            // Strain (tension) bar with its snap-zone frame.
            MakeImage(_panel, "TensionFrame", _gaugeSprite, new Color(0.12f, 0.14f, 0.16f, 0.9f),
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -36f), new Vector2(-200f, 44f),
                preserveAspect: false);
            var tensionTrack = MakeTrack(_panel, "TensionTrack",
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(8f, -40f), new Vector2(-216f, 36f));
            _tensionFill = MakeBarFill(tensionTrack, CalmTension);

            // Landing gauge.
            MakeTrack(_panel, "LandingFrame",
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -100f), new Vector2(-200f, 44f),
                bg: new Color(0.10f, 0.12f, 0.16f, 0.9f));
            var landingTrack = MakeTrack(_panel, "LandingTrack",
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(8f, -104f), new Vector2(-216f, 36f));
            _landingFill = MakeBarFill(landingTrack, LandingColor);

            // Line/hook motif (right) — flavour.
            MakeImage(_panel, "LineHook", _lineHookSprite, Color.white,
                new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-70f, -70f), new Vector2(110f, 110f));

            // Status text under the bars.
            _statusLabel = MakeLabel(_panel, "Status", TextAnchor.LowerCenter,
                new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 8f), new Vector2(-24f, 64f), 36);

            _panel.gameObject.SetActive(false); // transient: hidden until a cast goes out
        }

        private static RectTransform MakeTrack(RectTransform parent, string name,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPos, Vector2 size, Color? bg = null)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = anchoredPos; rt.sizeDelta = size;
            var img = go.GetComponent<Image>();
            img.color = bg ?? new Color(0.06f, 0.07f, 0.09f, 0.9f);
            img.raycastTarget = false;
            return rt;
        }

        private static Image MakeBarFill(RectTransform track, Color color)
        {
            var go = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(track, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(0f, 1f);  // grows via SetFill (anchorMax.x)
            rt.pivot = new Vector2(0f, 0.5f);
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            var img = go.GetComponent<Image>();
            img.color = color; img.raycastTarget = false;
            return img;
        }

        private static Image MakeImage(RectTransform parent, string name, Sprite sprite, Color color,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPos, Vector2 size, bool preserveAspect = true)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = anchoredPos; rt.sizeDelta = size;
            var img = go.GetComponent<Image>();
            img.sprite = sprite;                  // null → solid colour rect (still renders)
            img.color = color;
            img.preserveAspect = preserveAspect;
            img.raycastTarget = false;            // display-only; never eat gameplay touches
            return img;
        }

        private static Text MakeLabel(RectTransform parent, string name, TextAnchor align,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPos, Vector2 size, int fontSize)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text), typeof(Outline));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
            rt.pivot = new Vector2(0f, 0f);
            rt.anchoredPosition = anchoredPos; rt.sizeDelta = size;

            var text = go.GetComponent<Text>();
            text.font = DefaultFont();
            text.fontSize = fontSize;
            text.alignment = align;
            text.color = Color.white;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;

            var outline = go.GetComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
            outline.effectDistance = new Vector2(2f, -2f);
            return text;
        }

        // Unity 6 removed Arial.ttf from Resources; LegacyRuntime.ttf is the built-in fallback.
        private static Font DefaultFont()
        {
            var f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (f == null) f = Resources.GetBuiltinResource<Font>("Arial.ttf");
            return f;
        }
    }
}
