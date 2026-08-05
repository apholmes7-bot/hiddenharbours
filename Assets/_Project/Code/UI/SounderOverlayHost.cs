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
    /// <para><b>Flush on the dash by default; expanded is a choice</b> (S4.5, the owner's "shown on the
    /// dash and not blown up by default; this should be selectable"). The instrument paints into its
    /// authored brow mount — the skiffs' <c>SounderCutout</c>, the pilothouse's brow slot — at whatever
    /// size the dash card happens to be, which is where a sounder actually lives. Clicking it EXPANDS
    /// it to the standalone card this host has always drawn, and only there are the three side pushers
    /// live (units, alarm ±) and a tap on the glass toggling the night backlight. Clicking it again,
    /// clicking away, or Esc collapses it back to the dash.</para>
    ///
    /// <para><b>One raster, two presentations.</b> The flush face and the expanded card are the SAME
    /// texture at two rects — <see cref="HelmInstrumentMountLayout"/> — so they cannot disagree about
    /// the depth, and the shallow alarm's flash (Ruling E) is visible in both states for free. The
    /// expansion itself is <see cref="HelmInstrumentExpansion"/>: transient, shared so only one
    /// instrument opens at a time, and never persisted (rule 5 — nothing here writes a preference).</para>
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
        [Tooltip("Sorting order of the sounder canvas. ABOVE the helm dash's own canvas (60), because " +
                 "the instrument now paints its glass into the dash's brow mount and the chrome draws " +
                 "the bezel around it.")]
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

        /// <summary>Which slot this host owns in the shared expansion arbiter.</summary>
        private const HelmInstrumentSlot Slot = HelmInstrumentSlot.Sounder;

        /// <summary>The one host per play session (it self-installs and survives scene loads). Exposed so
        /// a PlayMode test can drive the host that is actually running rather than a second copy — a
        /// duplicate destroys itself in <c>Awake</c>.</summary>
        public static SounderOverlayHost Instance => _instance;

        /// <summary>Blown up to its own card (S4.5) rather than flush in the dash's brow. Shared state,
        /// so only one instrument is ever expanded. Exposed for tests.</summary>
        public bool Expanded => HelmInstrumentExpansion.IsExpanded(Slot);

        /// <summary>S2's name for <see cref="Expanded"/> — the enlarged, controls-live state. Kept
        /// because that is what it still is, and what the shipped tests read it as.</summary>
        public bool Focused => Expanded;

        /// <summary>True while the instrument is on screen — i.e. this hull's helm actually carries the
        /// basic depth sounder AND there is a sounding to show. Exposed for tests.</summary>
        public bool Showing => _cardGo != null && _cardGo.activeSelf;

        /// <summary>True while the glass is drawing FLUSH in the dash's authored brow mount (the
        /// default), false while it is expanded or has fallen back to its own card. Exposed for
        /// tests.</summary>
        public bool FlushMounted { get; private set; }

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
                if (Expanded) HelmInstrumentExpansion.Collapse();
                _painted = false;
                FlushMounted = false;
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
            // panel lights ONLY — never of which presentation is up. That is what makes the flush face
            // and the expanded card incapable of disagreeing, and it is why the shallow alarm flashes
            // in both (Ruling E).
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

            bool expanded = Expanded;
            Rect card = GlassRect(expanded, in cfg);
            LayoutCard(card);
            Repaint(in state);
            ReadPointer(instruments, in prefs, in cfg, card, expanded);
        }

        /// <summary>
        /// Where the glass draws this frame. FLUSH in the dash's authored brow mount is the default
        /// (S4.5); EXPANDED is the standalone card S2 built, on the owner's own placement dials.
        ///
        /// <para>The standalone rect is also the FALLBACK for the case where there is no dash to mount
        /// into. In shipped play that cannot happen — the fit resolves through a
        /// <c>HelmConsoleDef</c>, so no console means no sounder to draw — but a boot frame before the
        /// helm host has published, or a rig assembled by a test, would otherwise leave the instrument
        /// with nowhere to be. Falling back to the card it has always had beats vanishing.</para>
        /// </summary>
        private Rect GlassRect(bool expanded, in DepthSounderSettings cfg)
        {
            if (!expanded && HelmOverlayHost.TryDashCard(out Rect dash, out HelmFit fit)
                          && HelmInstrumentMountLayout.TryBrowSounderRect(in fit, dash, out Rect mount))
            {
                FlushMounted = true;
                return mount;
            }
            FlushMounted = false;
            Rect card = SounderOverlayLayout.CardRect(expanded, DepthRigRender.W, DepthRigRender.H,
                                                     in cfg, Screen.width, Screen.height);
            // Keep the expanded glass out from under the always-on band (S4.5): the owner's centred
            // 2× card reached into it at every resolution, and the band sorts above the instrument.
            // It slides down before it shrinks — this is the state the instrument is READ in.
            return HudBandLayout.FitBelowBand(card, Screen.width, Screen.height,
                                              HudBandLayout.ReservedTopPx());
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
        /// The selection model (S4.5). FLUSH: the whole face is one button — click it to EXPAND, and
        /// nothing else on it is live, so a glance at the dash can never be a mis-set alarm (the S1
        /// small-card rule's shape). EXPANDED: the three pushers and the glass work, and a click
        /// ANYWHERE outside collapses — which is both the click-away gesture and, wherever the owner's
        /// dialled card leaves it uncovered, "click the mount again". A click inside that lands on no
        /// control does nothing rather than closing, so a near-miss on a pusher is never punished.
        /// Esc collapses from anywhere.
        /// </summary>
        private void ReadPointer(IHelmInstruments instruments, in SounderPrefs prefs,
                                 in DepthSounderSettings cfg, Rect card, bool expanded)
        {
            var kb = Keyboard.current;
            if (expanded && kb != null && kb.escapeKey.wasPressedThisFrame)
            {
                HelmInstrumentExpansion.Collapse();
                return;
            }

            var mouse = Mouse.current;
            if (mouse == null || !mouse.leftButton.wasPressedThisFrame) return;

            Vector2 pos = mouse.position.ReadValue();
            bool inCard = card.Contains(pos);

            if (!expanded)
            {
                if (inCard) HelmInstrumentExpansion.Click(Slot);
                return;
            }

            if (!inCard)
            {
                HelmInstrumentExpansion.Click(HelmInstrumentSlot.None);   // click-away → collapse
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
