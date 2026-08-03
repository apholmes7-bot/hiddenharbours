using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using HiddenHarbours.Core;

namespace HiddenHarbours.UI
{
    /// <summary>
    /// The screen-space DEPTH SOUNDER card (ADR 0025 S2) — the first INSTRUMENT to come through the
    /// helm-overlay path S1 built for the piloting controls. While the player pilots a hull whose
    /// effective fit includes the basic sounder, a small card renders the live depth off the sim;
    /// clicking it enlarges it to a FOCUSED state where the three side pushers work (units, alarm ±) and
    /// a tap on the glass toggles the night backlight. Esc or a click outside returns.
    ///
    /// <para><b>The reading is the sea.</b> Depth comes from
    /// <see cref="IHelmInstruments.TryReadDepth"/> — <c>waterLevel − seabedElevation</c> over the one
    /// shared height map, taken on a throttled tick and never stored (rule 5). This host holds no depth
    /// of its own and caches nothing but the last painted picture.</para>
    ///
    /// <para><b>Its own host, not a second card inside
    /// <see cref="HelmOverlayHost"/>.</b> The control card answers "how do I drive this boat"; this one
    /// answers "what is under me", and S3–S5 add three more instruments to the same brow. Separate hosts
    /// keep each instrument's lifetime, hit geometry and repaint rule its own — and let the console dash
    /// (S2a/S6) mount <see cref="DepthRigRender.DrawUnit"/> into its brow without inheriting a card.</para>
    ///
    /// <para><b>Self-installing</b> (the S1 pattern): a <see cref="RuntimeInitializeOnLoadMethod"/> spawns
    /// one persistent host per play session, so every already-built scene grows the instrument with no
    /// builder re-run. Headless-safe and inert without a registered instrument service.</para>
    ///
    /// <para><b>Perf (rule 7):</b> repaints are change-detected on what actually moves pixels — the two
    /// LCD strings, the palette flags, and the alarm's blink phase. A quiet sounder in still water
    /// repaints roughly never; a sounding alarm repaints at
    /// <see cref="DepthSounderSettings.AlarmBlinkHz"/>×2, not per frame. One reused
    /// <see cref="DrawSurface"/> + <see cref="Texture2D"/>; zero steady-state allocation.</para>
    /// </summary>
    [DefaultExecutionOrder(-40)]
    public sealed class SounderOverlayHost : MonoBehaviour
    {
        [Header("Canvas")]
        [Tooltip("Sorting order of the sounder canvas. Beside the helm control card, above the HUD band.")]
        [SerializeField] private int _sortingOrder = 60;

        private static SounderOverlayHost _instance;

        private GameObject _cardGo;
        private RectTransform _cardRect;
        private RawImage _image;
        private RectTransform _imageRect;

        private DrawSurface _surface;
        private Texture2D _texture;

        // Change-detection state — what the current texture actually shows.
        private bool _painted;
        private string _shownDepth, _shownAlarm, _shownTemp;
        private bool _shownFeet, _shownNight, _shownArmed, _shownTriggered, _shownBlink;

        private bool _focused;

        /// <summary>The one host per play session (it self-installs and survives scene loads). Exposed so
        /// a PlayMode test can drive the host that is actually running rather than a second copy — a
        /// duplicate destroys itself in <c>Awake</c>.</summary>
        public static SounderOverlayHost Instance => _instance;

        /// <summary>Focused state (click-to-focus). Exposed for tests.</summary>
        public bool Focused => _focused;

        /// <summary>True while the card is on screen — i.e. this hull's helm actually carries the basic
        /// depth sounder AND there is a sounding to show. Exposed for tests.</summary>
        public bool Showing => _cardGo != null && _cardGo.activeSelf;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (_instance != null) return;
            var go = new GameObject("SounderOverlayHost");
            _instance = go.AddComponent<SounderOverlayHost>();
            DontDestroyOnLoad(go);
        }

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;

            var canvasGo = new GameObject("SounderOverlayCanvas");
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = _sortingOrder;
            canvasGo.AddComponent<CanvasScaler>();   // constant pixel size — the rigs are pixel art

            _cardGo = new GameObject("SounderCard");
            _cardGo.transform.SetParent(canvasGo.transform, false);
            _cardRect = _cardGo.AddComponent<RectTransform>();
            _cardRect.anchorMin = _cardRect.anchorMax = new Vector2(0f, 0f);
            _cardRect.pivot = new Vector2(0f, 0f);

            var imageGo = new GameObject("Sounder");
            imageGo.transform.SetParent(_cardGo.transform, false);
            _imageRect = imageGo.AddComponent<RectTransform>();
            _image = imageGo.AddComponent<RawImage>();
            _image.raycastTarget = false;   // we hit-test ourselves; never block other UI

            _cardGo.SetActive(false);
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
            if (_texture != null) Destroy(_texture);
        }

        private void Update()
        {
            IHelmInstruments instruments = GameServices.HelmInstruments;
            // The DEPTH sounder owns the brow only while the fit says Depth. A Fish fit is the colour
            // finder's cutout (S3) — this card stands down rather than drawing the wrong instrument.
            bool fitted = instruments != null && instruments.Fit.Sounder == SounderKind.Depth;
            if (!fitted || !instruments.TryReadDepth(out float depth))
            {
                _focused = false;
                _painted = false;
                if (_cardGo.activeSelf) _cardGo.SetActive(false);
                return;
            }
            if (!_cardGo.activeSelf) _cardGo.SetActive(true);

            DepthSounderSettings cfg = GameServices.DepthSounder;
            SounderPrefs prefs = instruments.SounderPrefs;

            bool triggered = DepthSounder.ShallowAlarm(depth, in prefs);
            // Blink only advances while the alarm is sounding; a quiet sounder is a still picture.
            bool blink = triggered && DepthSounder.BlinkPhase(Time.time, cfg.AlarmBlinkHz);

            var state = new DepthRigState(depth, prefs.Feet, prefs.Night, prefs.Armed, prefs.AlarmMetres,
                                          cfg.PlaceholderWaterTempC, blink);

            Rect card = SounderOverlayLayout.CardRect(_focused, DepthRigRender.W, DepthRigRender.H,
                                                      in cfg, Screen.width, Screen.height);
            LayoutCard(card);
            Repaint(in state);
            ReadPointer(instruments, in prefs, in cfg, card);
        }

        // ---- layout -------------------------------------------------------------------------------

        private void LayoutCard(Rect card)
        {
            _cardRect.anchoredPosition = new Vector2(card.xMin, card.yMin);
            _cardRect.sizeDelta = new Vector2(card.width, card.height);
            _imageRect.anchorMin = Vector2.zero;
            _imageRect.anchorMax = Vector2.one;
            _imageRect.offsetMin = Vector2.zero;
            _imageRect.offsetMax = Vector2.zero;
            _imageRect.pivot = new Vector2(0.5f, 0.5f);
        }

        // ---- painting (change-detected, rule 7) ----------------------------------------------------

        private void Repaint(in DepthRigState state)
        {
            // Compare what the GLASS shows, not the raw floats: a centimetre of tide that does not move
            // a digit must not cost a raster (rule 7).
            string depthStr = DepthRigGeometry.FmtDepth(state.Depth, state.Feet);
            string alarmStr = DepthRigGeometry.FmtSet(state.Alarm, state.Feet);
            string tempStr = DepthRigGeometry.FixedOne(state.TempC);
            bool triggered = state.Triggered;

            if (_painted && depthStr == _shownDepth && alarmStr == _shownAlarm && tempStr == _shownTemp
                && state.Feet == _shownFeet && state.Night == _shownNight && state.Armed == _shownArmed
                && triggered == _shownTriggered && state.Blink == _shownBlink)
                return;

            if (_surface == null) _surface = new DrawSurface(DepthRigRender.W, DepthRigRender.H);
            DepthRigRender.Render(_surface, in state);
            _surface.ToTexture(ref _texture);
            _image.texture = _texture;

            _painted = true;
            _shownDepth = depthStr; _shownAlarm = alarmStr; _shownTemp = tempStr;
            _shownFeet = state.Feet; _shownNight = state.Night; _shownArmed = state.Armed;
            _shownTriggered = triggered; _shownBlink = state.Blink;
        }

        // ---- pointer + keys ------------------------------------------------------------------------

        private void ReadPointer(IHelmInstruments instruments, in SounderPrefs prefs,
                                 in DepthSounderSettings cfg, Rect card)
        {
            var kb = Keyboard.current;
            if (_focused && kb != null && kb.escapeKey.wasPressedThisFrame) { _focused = false; return; }

            var mouse = Mouse.current;
            if (mouse == null || !mouse.leftButton.wasPressedThisFrame) return;

            Vector2 pos = mouse.position.ReadValue();
            bool inCard = card.Contains(pos);

            if (!inCard)
            {
                if (_focused) _focused = false;   // click-away leaves the focused state
                return;
            }

            if (!_focused)
            {
                // SMALL state: the card is a button — click anywhere on it to FOCUS. The pushers are
                // deliberately not live at dash size (the S1 rule).
                _focused = true;
                return;
            }

            HelmOverlayLayout.ScreenToRig(pos, card, DepthRigRender.W, DepthRigRender.H,
                                          out Vector2 rigPx);
            Apply(instruments, in prefs, in cfg, SounderOverlayLayout.HitTest(rigPx));
        }

        /// <summary>Turn a hit into a preference change and persist it (the pushers' whole behaviour —
        /// <c>depth-finder/README.md</c>: buttons[0] units, [1] alarm +step, [2] alarm −step, glass tap
        /// = night). Public so a PlayMode test can drive the controls without synthesising a mouse.</summary>
        public void Apply(IHelmInstruments instruments, in SounderPrefs prefs,
                          in DepthSounderSettings cfg, int hit)
        {
            if (instruments == null || hit < 0) return;
            SounderPrefs next = prefs;
            switch (hit)
            {
                case 0: next.Feet = !prefs.Feet; break;
                case 1: next.AlarmMetres = DepthSounder.StepAlarm(prefs.AlarmMetres, +1, in cfg); break;
                case 2: next.AlarmMetres = DepthSounder.StepAlarm(prefs.AlarmMetres, -1, in cfg); break;
                case SounderOverlayLayout.LcdHit: next.Night = !prefs.Night; break;
                default: return;
            }
            instruments.SetSounderPrefs(in next);
        }
    }
}
