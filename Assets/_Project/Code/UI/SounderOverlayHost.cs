using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using HiddenHarbours.Core;

namespace HiddenHarbours.UI
{
    /// <summary>
    /// The DEPTH SOUNDER's glass (ADR 0025 S2) — the first INSTRUMENT to come through the helm-overlay
    /// path S1 built for the piloting controls. While the player pilots a hull whose effective fit
    /// includes the basic sounder, it renders the live depth off the sim.
    ///
    /// <para><b>A housing in the band; raised is a choice</b> (H2, from S4.5's "shown on the dash
    /// and not blown up by default; this should be selectable"). The sounder's place on the helm is
    /// its named housing in the band (<see cref="HelmBandController"/>; on the pilot bands it carries
    /// the depth too). Pressing the housing RAISES this host's card — the standalone glass S2 built,
    /// placed by <see cref="HelmBandLayout.RaisedCardRect"/> — and only there are the three side
    /// pushers live (units, alarm ±) and a tap on the glass toggling the night backlight. Pressing the
    /// housing again, pressing away, or Esc lowers it. Unraised, this host draws nothing.</para>
    ///
    /// <para>The raise itself is <see cref="HelmInstrumentExpansion"/>: transient, shared so only one
    /// instrument is up at a time, and never persisted (rule 5 — nothing here writes a preference).
    /// The card publishes its rect there each frame it draws, so the band leaves a press inside it to
    /// this host.</para>
    ///
    /// <para><b>The reading is the sea.</b> Depth comes from
    /// <see cref="IHelmInstruments.TryReadDepth"/> — <c>waterLevel − seabedElevation</c> over the one
    /// shared height map, taken on a throttled tick and never stored (rule 5). This host holds no depth
    /// of its own and caches nothing but the last painted picture.</para>
    ///
    /// <para><b>Its own host, not a second card inside
    /// <see cref="HelmOverlayHost"/>.</b> The helm answers "how do I drive this boat"; this one
    /// answers "what is under me", and S3–S5 add three more instruments to the same helm. Separate
    /// hosts keep each instrument's lifetime, hit geometry and repaint rule its own.</para>
    ///
    /// <para><b>Self-installing</b> (the S1 pattern): a <see cref="RuntimeInitializeOnLoadMethod"/> spawns
    /// one persistent host per play session, so every already-built scene grows the instrument with no
    /// builder re-run. Headless-safe and inert without a registered instrument service.</para>
    ///
    /// <para><b>Perf (rule 7):</b> repaints are change-detected on what actually moves pixels — the two
    /// LCD strings, the palette flags, and the alarm's blink phase — and happen only while the card is
    /// raised. A quiet sounder in still water repaints roughly never; a sounding alarm repaints at
    /// <see cref="DepthSounderSettings.AlarmBlinkHz"/>×2, not per frame. One reused
    /// <see cref="DrawSurface"/> + <see cref="Texture2D"/>; zero steady-state allocation.</para>
    /// </summary>
    [DefaultExecutionOrder(-40)]
    public sealed class SounderOverlayHost : MonoBehaviour
    {
        [Header("Canvas")]
        [Tooltip("Sorting order of the sounder canvas. ABOVE the helm band's own canvas (60), so a " +
                 "raised card that reaches down over the band reads over it.")]
        [SerializeField] private int _sortingOrder = 62;

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

        // True from the second frame of a raise: the press that raised the card was the band's, so
        // the frame it rises must not read that same press as one on the card.
        private bool _wasExpanded;

        /// <summary>Which slot this host owns in the shared expansion arbiter.</summary>
        private const HelmInstrumentSlot Slot = HelmInstrumentSlot.Sounder;

        /// <summary>The one host per play session (it self-installs and survives scene loads). Exposed so
        /// a PlayMode test can drive the host that is actually running rather than a second copy — a
        /// duplicate destroys itself in <c>Awake</c>.</summary>
        public static SounderOverlayHost Instance => _instance;

        /// <summary>Raised to its own card rather than only a housing in the band. Shared state, so
        /// only one instrument is ever raised. Exposed for tests.</summary>
        public bool Expanded => HelmInstrumentExpansion.IsExpanded(Slot);

        /// <summary>True while the instrument is presented — this hull's helm actually carries the
        /// basic depth sounder AND there is a sounding to show (its housing in the band, or its card
        /// raised). Exposed for tests.</summary>
        public bool Showing { get; private set; }

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
            // The DEPTH sounder owns the housing only while the fit says Depth. A Fish fit is the
            // colour finder's (S3) — this host stands down rather than drawing the wrong instrument.
            bool fitted = instruments != null && instruments.Fit.Sounder == SounderKind.Depth;
            if (!fitted || !instruments.TryReadDepth(out float depth))
            {
                if (Expanded) HelmInstrumentExpansion.Collapse();
                _painted = false;
                StandDown();
                return;
            }

            // Hide-all (M): stand down, and lower any raised card rather than leaving an invisible
            // one owning the pointer and Esc.
            if (BoatUiWindows.AllHidden)
            {
                if (Expanded) HelmInstrumentExpansion.Collapse();
                StandDown();
                return;
            }
            Showing = true;

            // Unraised, the housing in the band is the whole presentation.
            if (!Expanded)
            {
                _wasExpanded = false;
                if (_cardGo.activeSelf) _cardGo.SetActive(false);
                return;
            }
            if (!_cardGo.activeSelf) _cardGo.SetActive(true);

            DepthSounderSettings cfg = GameServices.DepthSounder;
            SounderPrefs prefs = instruments.SounderPrefs;

            bool triggered = DepthSounder.ShallowAlarm(depth, in prefs);
            // Blink only advances while the alarm is sounding; a quiet sounder is a still picture.
            bool blink = triggered && DepthSounder.BlinkPhase(Time.time, cfg.AlarmBlinkHz);

            // The signals the glass draws are a function of the SEA, the player's prefs and the boat's
            // panel lights ONLY — the same three the band's housing reads, so the two cannot disagree.
            //
            // The backlight is the OR of the two, and that is the rigs' own contract, not a taste:
            // every helm hands its dash's night straight to the mounted instrument
            // (consoleRig.js:400, sportRig.js:353, noviRig.js:449, capeRig.js:444), so a lit panel
            // lights what is mounted in it. The glass tap survives untouched as the EARLY-ON override
            // — the skipper who wants the amber face before dusk — and, as ever, persists per hull.
            // (Known limit: once the panel is lit the tap has nothing left to turn on, so it reads as
            // inert until morning. Making it a true override wants a THIRD state — auto/day/night —
            // and that is save data, so it is not smuggled in here.)
            bool night = prefs.Night || HelmPanelLight.IsNight();
            var state = new DepthRigState(depth, prefs.Feet, night, prefs.Armed, prefs.AlarmMetres,
                                          cfg.PlaceholderWaterTempC, blink);

            Rect card = HelmBandLayout.RaisedCardRect(DepthRigRender.W, DepthRigRender.H,
                                                      HudScreen.Width, HudScreen.Height,
                                                      HudBandLayout.ReservedTopPx());
            LayoutCard(card);
            Repaint(in state);
            HelmInstrumentExpansion.PublishRaisedCard(Slot, card);

            bool raiseFrame = !_wasExpanded;
            _wasExpanded = true;
            if (raiseFrame) return;
            ReadPointer(instruments, in prefs, in cfg, card);
        }

        private void StandDown()
        {
            Showing = false;
            _wasExpanded = false;
            if (_cardGo.activeSelf) _cardGo.SetActive(false);
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

        /// <summary>
        /// The raised card's pointer (H2). The three pushers and the glass work, and a press that
        /// lands on no control does nothing rather than lowering, so a near-miss on a pusher is never
        /// punished. Esc lowers from anywhere. A press OUTSIDE the card is the band's to route while
        /// the band is up (its housings, then the click-away); with no band it is this card's
        /// click-away.
        /// </summary>
        private void ReadPointer(IHelmInstruments instruments, in SounderPrefs prefs,
                                 in DepthSounderSettings cfg, Rect card)
        {
            var kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame)
            {
                HelmInstrumentExpansion.Collapse();
                return;
            }

            var mouse = Mouse.current;
            if (mouse == null || !mouse.leftButton.wasPressedThisFrame) return;

            Vector2 pos = mouse.position.ReadValue();

            // A press on ANY window's chrome (the watch's ×) is that window's business — never this
            // instrument's controls or click-away (2026-08-07).
            if (BoatUiWindows.OverAnyChrome(pos)) return;

            if (!card.Contains(pos))
            {
                if (!HelmOverlayHost.BandUp)
                    HelmInstrumentExpansion.Click(HelmInstrumentSlot.None);   // click-away → lower
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
