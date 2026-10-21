using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using HiddenHarbours.Core;

namespace HiddenHarbours.UI
{
    /// <summary>
    /// The screen-space HELM OVERLAY (ADR 0025 S1, the band since H2): while the player pilots a
    /// motorised hull, the hull's diegetic controls draw at the bottom of the screen. A hull with a
    /// console draws the HELM BAND (the owner's 2026-09-24 ruling): a full-width strip 144·k tall
    /// whose pieces are the dash's own renders, laid out by <see cref="HelmBandLayout"/> and painted
    /// by <see cref="HelmBandController"/>. A hull without one draws a single card at the bottom
    /// centre: the outboard TILLER, or the lone binnacle LEVER.
    ///
    /// <para><b>No focus.</b> The click-to-focus state (owner addition 2026-08-03) retired with the
    /// dash card: the band's wheel, lever and anchor switch are live at the band's own scale, and a
    /// press anywhere on the tiller's card starts its throttle drag. A fitted instrument's housing in
    /// the band raises that instrument's own card (<see cref="HelmInstrumentExpansion"/>); while one
    /// is raised a press on the band away from every housing lowers it, and the card's own host
    /// takes Esc and every press inside the card.</para>
    ///
    /// <para><b>One state, one owner.</b> The band and the card render <see cref="IHelmControl.Drive"/>
    /// — the controller's own throttle — every frame; mouse input goes back as intents
    /// (<c>DragDrive</c>/<c>EndDrag</c>, detent semantics in Core). The overlay stores NO drive.
    /// Reads/writes ride <see cref="GameServices.HelmControl"/> only — this assembly references only
    /// Core, so it structurally cannot reach the Boats module (rule 4).</para>
    ///
    /// <para><b>Self-installing</b> (the SaveService/AudioDirector pattern): a
    /// <see cref="RuntimeInitializeOnLoadMethod"/> spawns one persistent host per play session, so
    /// every already-built scene shows the helm with no builder re-run. Headless-safe and inert
    /// without a registered helm service (EditMode/PlayMode tests, on foot, rowing).</para>
    ///
    /// <para><b>Perf (rule 7):</b> the band's pieces repaint and upload only on their own change keys
    /// (<see cref="HelmBandController"/>); the panel and the caps are untextured tinted quads, retinted
    /// only when the panel lights switch; piece rects move only when the screen or the rig does. The
    /// lone lever redraws only when its 1/48th-quantised drive or finish changes; the tiller only when
    /// a band segment or the gear flips (steering rotates the RectTransform, no re-raster). Zero
    /// per-frame allocation in the steady state.</para>
    /// </summary>
    // -45: BEFORE the instrument card hosts (-40), which read BandUp and the press routing this host
    // settles each frame — an ordering, not a race.
    [DefaultExecutionOrder(-45)]
    public sealed class HelmOverlayHost : MonoBehaviour
    {
        [Header("Canvas")]
        [Tooltip("Sorting order of the overlay canvas. Above the HUD band; below the raised " +
                 "instrument cards (62-64) and modal shells.")]
        [SerializeField] private int _sortingOrder = 60;

        private static HelmOverlayHost _instance;

        // True on a frame the band is drawn; cleared at the top of every Update, so a card host that
        // runs after this one never reads a band that is no longer on screen.
        private static bool _bandUp;

        private Canvas _canvas;
        private GameObject _cardGo;          // the card rect (position/size; never rotates)
        private RectTransform _cardRect;
        private RawImage _image;             // the instrument picture (tiller rotates about its clamp)
        private RectTransform _imageRect;

        private DrawSurface _surface;
        private Texture2D _texture;

        // The band: a tinted panel under one image per piece (the caps are tinted, untextured).
        private GameObject _bandGo;
        private RawImage _panel;
        private RectTransform _panelRect;
        private readonly RawImage[] _pieceImages = new RawImage[HelmBandGeometry.MaxPieces];
        private readonly RectTransform[] _pieceRects = new RectTransform[HelmBandGeometry.MaxPieces];

        // What the band's images show (applied only when these move).
        private int _appliedW = -1, _appliedH = -1;
        private ConsoleRigKind _appliedRig = ConsoleRigKind.None;
        private int _appliedUploads = -1;
        private int _appliedNight = -1;

        // Change-detection state (what the card's texture shows).
        private HelmControlStyle _shownStyle = HelmControlStyle.None;
        private int _shownLeverKey = int.MinValue;
        private HelmLeverFinish _shownFinish = (HelmLeverFinish)(-1);
        private int _shownBandLit = -1;
        private bool _shownForward = true;

        // Interaction state (the card).
        private bool _dragging;              // a live control drag session (lever grip / tiller band)
        private float _dragStartDrive;
        private float _dragStartScreenY;
        private bool _dragIsTiller;

        // The band (H2): painter + per-piece interaction.
        private readonly HelmBandController _bandCtl = new HelmBandController();
        private bool _bandShown;

        // True while what the Core footprint seam says is this host's statement (ADR 0050). Only an
        // owner clears it, so a host with no card up never stomps on anybody else's.
        private bool _footprintOwned;

        /// <summary>Which card the overlay shows right now (test seam).</summary>
        public enum HelmCardKind { None, Tiller, Lever, Band }

        /// <summary>The card currently shown. Exposed for tests.</summary>
        public HelmCardKind CardKind { get; private set; }

        /// <summary>The band's painter and controls (test seam: pieces, wheel angle, steer session).</summary>
        public HelmBandController BandController => _bandCtl;

        /// <summary>The live host instance (test seam — the bootstrap spawns exactly one).</summary>
        public static HelmOverlayHost Instance => _instance;

        /// <summary>
        /// True while the helm band is on screen this frame. The instrument card hosts read it: with
        /// the band up, a press away from a raised card is the band's to route (a housing raises its
        /// own card, anywhere else lowers it), so the card host does not also treat it as its
        /// click-away. Static because the hosts are independent self-installing singletons; set from
        /// <see cref="Update"/>, which runs FIRST by execution order, so a reader never sees a stale
        /// band.
        /// </summary>
        public static bool BandUp => _bandUp;

        /// <summary>
        /// What the helm covers, as the Core footprint seam states it (ADR 0050): a wheel helm's band
        /// is a full-width band 144k tall on the bottom edge; the tiller's or the lone lever's card is
        /// that card alone (the title strip above it retired with the windows, H2). Nothing for no
        /// card, or for a card the screen gave no room. Pure, so EditMode pins it.
        /// </summary>
        public static HelmFootprintArea FootprintOf(HelmCardKind kind, Rect card, int screenH)
        {
            switch (kind)
            {
                case HelmCardKind.Band:
                    return HelmFootprintArea.FullWidthBand(HelmBandLayout.BandHeightPx(screenH));
                case HelmCardKind.Tiller:
                case HelmCardKind.Lever:
                    return HelmFootprintArea.OfCard(card);
                default:
                    return HelmFootprintArea.None;
            }
        }

        // Say what the helm covers, as this host. Change-detected in Core, so a band at rest costs one
        // struct compare a frame.
        private void PublishFootprint(in HelmFootprintArea area)
        {
            if (area.IsNone && !_footprintOwned) return;
            _footprintOwned = !area.IsNone;
            HelmFootprint.Publish(in area);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (_instance != null) return;
            var go = new GameObject("HelmOverlayHost");
            _instance = go.AddComponent<HelmOverlayHost>();
            DontDestroyOnLoad(go);
        }

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;

            var canvasGo = new GameObject("HelmOverlayCanvas");
            canvasGo.transform.SetParent(transform, false);
            _canvas = canvasGo.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = _sortingOrder;
            canvasGo.AddComponent<CanvasScaler>();   // constant pixel size — the rigs are pixel art

            _cardGo = new GameObject("InstrumentCard");
            _cardGo.transform.SetParent(canvasGo.transform, false);
            _cardRect = _cardGo.AddComponent<RectTransform>();
            _cardRect.anchorMin = _cardRect.anchorMax = new Vector2(0f, 0f);
            _cardRect.pivot = new Vector2(0f, 0f);

            var imageGo = new GameObject("Instrument");
            imageGo.transform.SetParent(_cardGo.transform, false);
            _imageRect = imageGo.AddComponent<RectTransform>();
            _image = imageGo.AddComponent<RawImage>();
            _image.raycastTarget = false;   // we hit-test ourselves; never block other UI

            _cardGo.SetActive(false);

            _bandGo = new GameObject("HelmBand");
            _bandGo.transform.SetParent(canvasGo.transform, false);
            var bandRect = _bandGo.AddComponent<RectTransform>();
            bandRect.anchorMin = bandRect.anchorMax = Vector2.zero;
            bandRect.pivot = Vector2.zero;
            bandRect.anchoredPosition = Vector2.zero;
            bandRect.sizeDelta = Vector2.zero;
            _panel = NewBandImage("Panel", out _panelRect);
            for (int i = 0; i < _pieceImages.Length; i++)
            {
                _pieceImages[i] = NewBandImage("Piece" + i, out _pieceRects[i]);
                _pieceImages[i].gameObject.SetActive(false);
            }
            _bandGo.SetActive(false);
        }

        private RawImage NewBandImage(string name, out RectTransform rect)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_bandGo.transform, false);
            rect = go.AddComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = Vector2.zero;
            rect.pivot = Vector2.zero;
            var image = go.AddComponent<RawImage>();
            image.raycastTarget = false;    // we hit-test ourselves; never block other UI
            return image;
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
                // The publication is only refreshed by Update, so a host that stops updating would
                // otherwise leave its band standing for the card hosts and keep a card raised over a
                // helm that is no longer drawn.
                _bandUp = false;
                HelmInstrumentExpansion.Collapse();
            }
            PublishFootprint(HelmFootprintArea.None);   // and for the footprint (a no-op unless ours)
            if (_texture != null) Destroy(_texture);
            _bandCtl.ReleaseTextures();
        }

        // A disabled host draws nothing, so it covers nothing.
        private void OnDisable()
        {
            if (_instance == this) _bandUp = false;
            PublishFootprint(HelmFootprintArea.None);
        }

        private void Update()
        {
            // ⚠ IsPlayerHelm, not HasHelm — the helm is drawn for the hand ON the wheel. HasHelm only
            // says an engine hull is under way, which is equally true of a boat somebody else is
            // steering (the owner's 2026-08-21 playtest: wheel and gauges drawn to a passenger).
            IHelmControl helm = GameServices.HelmControl;
            HelmControlStyle style = helm != null && helm.IsPlayerHelm ? helm.Style : HelmControlStyle.None;
            _bandUp = false;       // republished below only if the band actually draws this frame

            // No helm, or hide-all (M): stand down. With hide-all the helm itself — drive, steer,
            // keys — keeps working, and one more press brings the band back.
            if (style == HelmControlStyle.None || BoatUiWindows.AllHidden)
            {
                if (_dragging && helm != null) helm.EndDrag();
                _dragging = false;
                _bandCtl.Deactivate(helm);
                HelmInstrumentExpansion.Collapse();   // nothing raised over a helm that is not drawn
                _shownStyle = HelmControlStyle.None;
                CardKind = HelmCardKind.None;
                PublishFootprint(HelmFootprintArea.None);
                if (_cardGo.activeSelf) _cardGo.SetActive(false);
                if (_bandGo.activeSelf) _bandGo.SetActive(false);
                return;
            }

            HelmOverlaySettings cfg = GameServices.HelmOverlay;

            // A lever hull with a console draws the BAND (every rig the fleet has); a hull only falls
            // back to the lone lever card if it has no console at all.
            HelmFit fit = helm.Fit;
            bool band = style == HelmControlStyle.Lever && HelmBandGeometry.HasBand(fit.Rig);
            if (band != _bandShown)
            {
                // Crossing the band boundary invalidates the other path's change-detection keys.
                _bandCtl.Invalidate();
                if (!band) _bandCtl.Deactivate(helm);
                if (_dragging) { helm.EndDrag(); _dragging = false; }
                _shownStyle = HelmControlStyle.None;
                _shownLeverKey = int.MinValue;
                _shownBandLit = -1;
                _appliedRig = ConsoleRigKind.None;
                _bandShown = band;
            }

            if (band)
            {
                CardKind = HelmCardKind.Band;
                if (_cardGo.activeSelf) _cardGo.SetActive(false);
                if (!_bandGo.activeSelf) _bandGo.SetActive(true);

                int sw = HudScreen.Width, sh = HudScreen.Height;
                _bandCtl.Layout(fit.Rig, sw, sh);
                HelmBandGlance glance = HelmDashGeometry.IsPilothouse(fit.Rig) ? Glance() : HelmBandGlance.None;
                _bandCtl.UpdateAndPaint(helm, in fit, SampleHeadingDegrees(), Time.deltaTime, in glance);
                ApplyBand(sw, sh);

                _bandUp = true;
                PublishFootprint(FootprintOf(HelmCardKind.Band, default, sh));
                ReadBandPointer(helm, in fit, in cfg);
                return;
            }

            if (_bandGo.activeSelf) _bandGo.SetActive(false);
            if (!_cardGo.activeSelf) _cardGo.SetActive(true);

            CardKind = style == HelmControlStyle.Lever ? HelmCardKind.Lever : HelmCardKind.Tiller;
            int rigW = style == HelmControlStyle.Lever ? LeverRigRender.W : TillerRigRender.W;
            int rigH = style == HelmControlStyle.Lever ? LeverRigRender.H : TillerRigRender.H;

            Rect card = HelmOverlayLayout.CardRect(rigW, rigH, in cfg, HudScreen.Width, HudScreen.Height);
            PublishFootprint(FootprintOf(CardKind, card, HudScreen.Height));

            LayoutCard(style, card, helm.Steer);
            Repaint(style, helm);
            ReadPointer(style, helm, card, in cfg);
        }

        /// <summary>The compass's heading source: the PHYSICS ROOT's bow through the Core kinematics
        /// seam (<see cref="GameServices.ActiveBoat"/> — 0 = N, clockwise; never the counter-rotating
        /// visual child). 0 (North) when no probe is registered.</summary>
        private static float SampleHeadingDegrees()
        {
            IActiveBoatService probe = GameServices.ActiveBoat;
            if (probe == null) return 0f;
            BoatKinematics k = probe.Sample();
            return k.HasBoat ? k.HeadingDegrees : 0f;
        }

        /// <summary>What the pilot bands' sounder housing shows: the depth through the Core instrument
        /// seam (the sounder's own throttled read), the hull's units, and the shallow alarm's blink —
        /// the same three things the sounder's glass draws, so the housing and the raised card cannot
        /// disagree.</summary>
        private static HelmBandGlance Glance()
        {
            IHelmInstruments instruments = GameServices.HelmInstruments;
            if (instruments == null || instruments.Fit.Sounder == SounderKind.None
                || !instruments.TryReadDepth(out float depth))
                return HelmBandGlance.None;
            SounderPrefs prefs = instruments.SounderPrefs;
            bool lit = DepthSounder.ShallowAlarm(depth, in prefs)
                       && DepthSounder.BlinkPhase(Time.time, GameServices.DepthSounder.AlarmBlinkHz);
            return new HelmBandGlance(true, depth, prefs.Feet, lit);
        }

        // ---- layout -------------------------------------------------------------------------------

        // Hand the band's pieces to their images: rects only when the screen or the rig moved,
        // textures only when something was uploaded, tints only when the panel lights switched.
        private void ApplyBand(int sw, int sh)
        {
            int count = _bandCtl.PieceCount;
            bool moved = sw != _appliedW || sh != _appliedH || _bandCtl.Rig != _appliedRig;
            if (moved)
            {
                Place(_panelRect, HelmBandLayout.BandRect(sw, sh));
                for (int i = 0; i < _pieceImages.Length; i++)
                {
                    bool on = i < count;
                    GameObject go = _pieceImages[i].gameObject;
                    if (go.activeSelf != on) go.SetActive(on);
                    if (!on) continue;
                    Place(_pieceRects[i], _bandCtl.PieceRect(i));
                    bool cap = _bandCtl.Piece(i).Kind == HelmBandPieceKind.Cap;
                    if (cap) _pieceImages[i].texture = null;
                    else _pieceImages[i].color = Color.white;
                }
                _appliedW = sw;
                _appliedH = sh;
                _appliedRig = _bandCtl.Rig;
                _appliedUploads = -1;
                _appliedNight = -1;
            }

            if (_bandCtl.UploadCount != _appliedUploads)
            {
                for (int i = 0; i < count; i++)
                {
                    if (_bandCtl.Piece(i).Kind == HelmBandPieceKind.Cap) continue;
                    Texture2D t = _bandCtl.TextureOf(i);
                    if (_pieceImages[i].texture != t) _pieceImages[i].texture = t;
                }
                _appliedUploads = _bandCtl.UploadCount;
            }

            int night = _bandCtl.NightShown ? 1 : 0;
            if (night != _appliedNight)
            {
                bool lit = night == 1;
                _panel.color = HelmBandPlateRender.Panel(lit);
                Color32 capTint = HelmBandPlateRender.Cap(lit);
                for (int i = 0; i < count; i++)
                    if (_bandCtl.Piece(i).Kind == HelmBandPieceKind.Cap) _pieceImages[i].color = capTint;
                _appliedNight = night;
            }
        }

        private static void Place(RectTransform rt, Rect r)
        {
            rt.anchoredPosition = new Vector2(r.xMin, r.yMin);
            rt.sizeDelta = new Vector2(r.width, r.height);
        }

        private void LayoutCard(HelmControlStyle style, Rect card, float steer)
        {
            _cardRect.anchoredPosition = new Vector2(card.xMin, card.yMin);
            _cardRect.sizeDelta = new Vector2(card.width, card.height);

            _imageRect.anchorMin = Vector2.zero;
            _imageRect.anchorMax = Vector2.one;
            _imageRect.offsetMin = Vector2.zero;
            _imageRect.offsetMax = Vector2.zero;

            if (style == HelmControlStyle.Tiller)
            {
                // The rig draws the tiller STRAIGHT UP; the caller rotates it about the clamp pivot
                // (tillerRig.js:37-45). Pivot in UI space: x centred, y measured from the BOTTOM.
                _imageRect.pivot = new Vector2(TillerRigRender.PivotX / (float)TillerRigRender.W,
                                               1f - TillerRigRender.PivotY / (float)TillerRigRender.H);
                // The PHYSICAL tiller sense (owner fix 2026-08-03): steer to starboard = handle to
                // port — the deliberate inverse of the preview harness's display-joystick rotation.
                // See HelmOverlayLayout.TillerHandleAngleZDeg for the full convention citation.
                _imageRect.localEulerAngles =
                    new Vector3(0f, 0f, HelmOverlayLayout.TillerHandleAngleZDeg(steer));
            }
            else
            {
                _imageRect.pivot = new Vector2(0.5f, 0.5f);
                _imageRect.localEulerAngles = Vector3.zero;
            }
        }

        // ---- painting (change-detected, rule 7) ----------------------------------------------------

        private void Repaint(HelmControlStyle style, IHelmControl helm)
        {
            float drive = helm.Drive;
            if (style == HelmControlStyle.Lever)
            {
                int key = LeverRigGeometry.QuantizeKey(drive);
                HelmLeverFinish finish = helm.LeverFinish;
                if (style == _shownStyle && key == _shownLeverKey && finish == _shownFinish) return;
                EnsureSurface(LeverRigRender.W, LeverRigRender.H);
                LeverRigRender.Render(_surface, drive, finish);
                _surface.ToTexture(ref _texture);
                _image.texture = _texture;
                _shownStyle = style;
                _shownLeverKey = key;
                _shownFinish = finish;
            }
            else
            {
                // The band lights round(|drive|*10) segments (tillerRig.js:80) — repaint on that,
                // the gear side, or first show. running is true at a manned helm (no engine start/stop
                // state exists yet); warn/press/blink stay off in S1.
                float throttle = Mathf.Abs(drive);
                int lit = DrawSurface.JsRound(Mathf.Clamp01(throttle) * 10.0);
                bool forward = drive >= 0f;
                if (style == _shownStyle && lit == _shownBandLit && forward == _shownForward) return;
                EnsureSurface(TillerRigRender.W, TillerRigRender.H);
                TillerRigRender.Render(_surface, throttle, running: true, press: false,
                                       forward: forward, warn: false, blink: false);
                _surface.ToTexture(ref _texture);
                _image.texture = _texture;
                _shownStyle = style;
                _shownBandLit = lit;
                _shownForward = forward;
            }
        }

        private void EnsureSurface(int w, int h)
        {
            if (_surface == null || _surface.Width != w || _surface.Height != h)
                _surface = new DrawSurface(w, h);
        }

        // ---- pointer -------------------------------------------------------------------------------

        /// <summary>
        /// The band's pointer routing (H2). A live wheel or lever drag finishes on its own terms, off
        /// the band or not. Otherwise a press goes, in order: nowhere while the shell blocks the world
        /// or the press is on a window's chrome; to the raised card's own host when it lands inside
        /// that card; to a fitted housing, which raises its card (or lowers it, pressed again); to
        /// lowering the raised card, anywhere else; and only then to the band's controls. This host
        /// reads no keyboard: Esc is the raised card's, and the keys steer through the controller.
        /// </summary>
        private void ReadBandPointer(IHelmControl helm, in HelmFit fit, in HelmOverlaySettings cfg)
        {
            var mouse = Mouse.current;
            if (mouse == null) return;
            Vector2 pos = mouse.position.ReadValue();

            if (_bandCtl.Interacting)
            {
                _bandCtl.Track(helm, pos, mouse.leftButton.isPressed, Time.deltaTime);
                return;
            }

            if (!mouse.leftButton.wasPressedThisFrame) return;
            if (ShellFlow.WorldInputBlocked) return;

            // A raised card typing a name holds the keyboard (the plotter's editor): it owns the
            // pointer outright, so its click commits the edit rather than this one lowering it.
            if (HelmKeyCapture.IsCapturing) return;

            // A press on ANY window's chrome (the watch's ×) is that window's business — never this
            // band's (2026-08-07 ruling).
            if (BoatUiWindows.OverAnyChrome(pos)) return;

            // Inside a raised card the card's controls are live; its host routes the press.
            if (HelmInstrumentExpansion.TryRaisedCard(out Rect raised) && raised.Contains(pos)) return;

            HelmInstrumentSlot slot = _bandCtl.PlateAt(in fit, pos);
            if (slot != HelmInstrumentSlot.None)
            {
                HelmInstrumentExpansion.Click(slot);   // raise it, switch to it, or lower it again
                return;
            }
            if (HelmInstrumentExpansion.AnyExpanded)
            {
                HelmInstrumentExpansion.Collapse();    // the click-away
                return;
            }
            if (_bandCtl.Contains(pos)) _bandCtl.PressControls(helm, pos, in cfg);
        }

        /// <summary>
        /// The card's pointer (a tiller hull, or a lever hull with no console). No focus: a press
        /// anywhere on the tiller's card starts its throttle drag, and a press on the lone lever is a
        /// grip drag or a travel-guide jump, live at the card's own scale.
        /// </summary>
        private void ReadPointer(HelmControlStyle style, IHelmControl helm, Rect card,
                                 in HelmOverlaySettings cfg)
        {
            var mouse = Mouse.current;
            if (mouse == null) return;
            Vector2 pos = mouse.position.ReadValue();
            bool down = mouse.leftButton.wasPressedThisFrame;
            bool held = mouse.leftButton.isPressed;

            if (_dragging)
            {
                if (held)
                {
                    if (_dragIsTiller)
                    {
                        // The drag is measured in the card's own px, so it divides by the scale the
                        // card is DRAWN at, HUD factor included.
                        float scale = cfg.SmallScale * HudBandLayout.ScaleFactor(HudScreen.Width, HudScreen.Height);
                        helm.DragDrive(HelmOverlayLayout.TillerDragDrive(
                            _dragStartDrive, pos.y - _dragStartScreenY, scale, cfg.TillerDragFullDrivePx));
                    }
                    else
                    {
                        // Off-card during a drag still tracks (the JS preview's behaviour: the grip
                        // follows the pointer's nearest travel point) — ScreenToRig's rigPx stays
                        // valid outside 0..1, only the containment bool goes false.
                        HelmOverlayLayout.ScreenToRig(pos, card, LeverRigRender.W, LeverRigRender.H,
                                                      out Vector2 rigPx);
                        helm.DragDrive(HelmOverlayLayout.LeverSigAt(rigPx));
                    }
                }
                else
                {
                    helm.EndDrag();
                    _dragging = false;
                }
                return;
            }

            if (!down || !card.Contains(pos)) return;
            if (ShellFlow.WorldInputBlocked) return;

            // A press on ANY window's chrome belongs to that window, never to this card
            // (2026-08-07 ruling).
            if (BoatUiWindows.OverAnyChrome(pos)) return;

            if (style == HelmControlStyle.Lever)
            {
                HelmOverlayLayout.ScreenToRig(pos, card, LeverRigRender.W, LeverRigRender.H, out Vector2 rigPx);
                if (HelmOverlayLayout.IsOnGrip(rigPx, helm.Drive, cfg.GrabRadiusPx))
                {
                    _dragging = true;          // drag the grip — continuous drive, live while dragging
                    _dragIsTiller = false;
                    helm.DragDrive(HelmOverlayLayout.LeverSigAt(rigPx));
                }
                else
                {
                    // Click the travel guide: jump-to-sig, then the lever holds (neutral snap applies).
                    helm.DragDrive(HelmOverlayLayout.LeverSigAt(rigPx));
                    helm.EndDrag();
                }
            }
            else
            {
                _dragging = true;              // tiller: vertical drag over the card walks the drive
                _dragIsTiller = true;
                _dragStartDrive = helm.Drive;
                _dragStartScreenY = pos.y;
            }
        }
    }
}
