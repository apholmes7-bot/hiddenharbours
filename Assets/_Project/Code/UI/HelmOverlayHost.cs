using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using HiddenHarbours.Core;

namespace HiddenHarbours.UI
{
    /// <summary>
    /// The screen-space HELM OVERLAY (ADR 0025 S1): while the player pilots a motorised hull, the
    /// hull's diegetic control — the outboard TILLER or the binnacle LEVER — draws on a small card
    /// (placeholder bottom-right; the owner repositions via <see cref="HelmOverlaySettings"/>).
    /// Clicking the card brings the instrument to a FOCUSED, enlarged state where its controls are
    /// properly clickable (owner addition 2026-08-03); Esc or a click outside returns to the small
    /// card. The full per-hull helm composition is S6 — this host is the mechanic every later
    /// instrument reuses.
    ///
    /// <para><b>One state, one owner.</b> The card renders <see cref="IHelmControl.Drive"/> — the
    /// controller's own throttle — every frame; mouse input goes back as intents
    /// (<c>DragDrive</c>/<c>EndDrag</c>, detent semantics in Core). The overlay stores NO drive.
    /// Reads/writes ride <see cref="GameServices.HelmControl"/> only — this assembly references only
    /// Core, so it structurally cannot reach the Boats module (rule 4).</para>
    ///
    /// <para><b>Self-installing</b> (the SaveService/AudioDirector pattern): a
    /// <see cref="RuntimeInitializeOnLoadMethod"/> spawns one persistent host per play session, so
    /// every already-built scene shows the helm with no builder re-run. Headless-safe and inert
    /// without a registered helm service (EditMode/PlayMode tests, on foot, rowing).</para>
    ///
    /// <para><b>Perf (rule 7):</b> repaints are change-detected — the lever redraws only when its
    /// 1/48th-quantised drive (the rig's own cache key) or finish changes; the tiller only when a
    /// band segment / gear / running flips (steering rotates the RectTransform, no re-raster). One
    /// reused <see cref="DrawSurface"/> + <see cref="Texture2D"/> per style; zero per-frame
    /// allocation in the steady state.</para>
    /// </summary>
    // -45: BEFORE the brow instrument hosts (-40), which flush-mount against the dash card rect this
    // publishes each frame (S4.5) — an ordering, not a race.
    [DefaultExecutionOrder(-45)]
    public sealed class HelmOverlayHost : MonoBehaviour
    {
        [Header("Canvas")]
        [Tooltip("Sorting order of the overlay canvas. Above the HUD band so the focused instrument " +
                 "reads over it; below modal shells.")]
        [SerializeField] private int _sortingOrder = 60;

        private static HelmOverlayHost _instance;

        private Canvas _canvas;
        private GameObject _cardGo;          // the card rect (position/size; never rotates)
        private RectTransform _cardRect;
        private RawImage _image;             // the instrument picture (tiller rotates about its clamp)
        private RectTransform _imageRect;

        private DrawSurface _surface;
        private Texture2D _texture;

        // Change-detection state (what the current texture shows).
        private HelmControlStyle _shownStyle = HelmControlStyle.None;
        private int _shownLeverKey = int.MinValue;
        private HelmLeverFinish _shownFinish = (HelmLeverFinish)(-1);
        private int _shownBandLit = -1;
        private bool _shownForward = true;

        // Interaction state.
        private bool _focused;
        private bool _dragging;              // a live control drag session (lever grip / tiller band)
        private float _dragStartDrive;
        private float _dragStartScreenY;
        private bool _dragIsTiller;

        // The composed skiff dash (S2a): compositor + per-instrument interaction.
        private readonly HelmDashController _dashCtl = new HelmDashController();
        private bool _dashShown;

        // The helm card as a WINDOW (2026-08-07 ruling): drag by the hover strip, resize by the
        // grip, collapse to Compact/Bar, hide (individually or via the hide-all key). One window
        // whichever card the hull shows — dash, lone lever, or tiller.
        private readonly BoatUiWindowController _window =
            new BoatUiWindowController(BoatUiWindowId.Helm);

        // The live dash card, published for the brow instruments that flush-mount into it (S4.5).
        // Recomputed every frame a dash draws and cleared the moment one does not, so a stale rect can
        // never leave an instrument floating over a helm that is no longer on screen.
        private static bool _dashLive;
        private static Rect _dashCard;
        private static HelmFit _dashFit;

        /// <summary>Which card the overlay shows right now (test seam).</summary>
        public enum HelmCardKind { None, Tiller, Lever, Dash }

        /// <summary>The card currently shown. Exposed for tests.</summary>
        public HelmCardKind CardKind { get; private set; }

        /// <summary>The dash compositor (test seam — wheel mirror angle, steer session).</summary>
        public HelmDashController DashController => _dashCtl;

        /// <summary>The live host instance (test seam — the bootstrap spawns exactly one).</summary>
        public static HelmOverlayHost Instance => _instance;

        /// <summary>Focused state (click-to-focus, owner addition 2026-08-03). Exposed for tests.</summary>
        public bool Focused => _focused;

        /// <summary>
        /// The dash card currently on screen and the fit it is drawing — the seam the brow instruments
        /// flush-mount against (S4.5). False whenever no dash is up (on foot, rowing, a tiller hull, or
        /// a helm with no console), in which case an instrument has no mount and falls back to its own
        /// card. Static because the hosts are independent self-installing singletons; published from
        /// <see cref="Update"/>, which runs FIRST by execution order, so a reader never sees a stale
        /// rect.
        /// </summary>
        public static bool TryDashCard(out Rect card, out HelmFit fit)
        {
            card = _dashCard;
            fit = _dashFit;
            return _dashLive;
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
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
                // The publication is only refreshed by Update, so a host that stops updating would
                // otherwise leave its last rect standing and hang the brow instruments over a helm
                // that is no longer drawn.
                _dashLive = false;
                HelmInstrumentExpansion.Collapse();
                _window.StandDown();   // same reasoning for the window's chrome publication + session
            }
            if (_texture != null) Destroy(_texture);
        }

        private void Update()
        {
            IHelmControl helm = GameServices.HelmControl;
            HelmControlStyle style = helm != null && helm.HasHelm ? helm.Style : HelmControlStyle.None;
            _dashLive = false;     // republished below only if a dash actually draws this frame

            if (style == HelmControlStyle.None)
            {
                if (_dragging && helm != null) helm.EndDrag();
                _dragging = false;
                _dashCtl.Deactivate(helm);
                _focused = false;
                HelmInstrumentExpansion.Collapse();   // losing the helm closes any expanded instrument
                _shownStyle = HelmControlStyle.None;
                CardKind = HelmCardKind.None;
                _window.StandDown();
                if (_cardGo.activeSelf) _cardGo.SetActive(false);
                return;
            }
            // The helm WINDOW is hidden (hide-all or its own ×): the card stands down exactly like
            // losing the helm, except the helm itself — drive, steer, keys — keeps working, and the
            // window state is one toggle from bringing the card back.
            if (!_window.Shown)
            {
                if (_dragging && helm != null) { helm.EndDrag(); _dragging = false; }
                _dashCtl.Deactivate(helm);
                _focused = false;
                _shownStyle = HelmControlStyle.None;
                CardKind = HelmCardKind.None;
                _window.StandDown();
                if (_cardGo.activeSelf) _cardGo.SetActive(false);
                return;
            }
            if (!_cardGo.activeSelf) _cardGo.SetActive(true);

            HelmOverlaySettings cfg = GameServices.HelmOverlay;

            // A lever hull whose console has a ported dash renderer shows the COMPOSED DASH. S2a
            // brought the two skiffs; S4 brings the two wheelhouses, so that is now every rig the
            // fleet has — a hull only falls back to the lone lever card if it has no console at all.
            HelmFit fit = helm.Fit;
            bool dash = style == HelmControlStyle.Lever && fit.Rig != ConsoleRigKind.None;
            if (dash != _dashShown)
            {
                // Crossing the dash boundary invalidates the other path's change-detection keys.
                _dashCtl.Invalidate();
                if (!dash) _dashCtl.Deactivate(helm);
                if (_dragging) { helm.EndDrag(); _dragging = false; }
                _shownStyle = HelmControlStyle.None;
                _shownLeverKey = int.MinValue;
                _shownBandLit = -1;
                _dashShown = dash;
            }

            if (dash)
            {
                CardKind = HelmCardKind.Dash;
                // The pilothouse canvas is taller than the skiffs' (600×548 vs 600×510), so the card
                // rect and the screen→rig mapping both follow the live rig, never one fixed size.
                int dashW = HelmDashGeometry.CanvasW(fit.Rig), dashH = HelmDashGeometry.CanvasH(fit.Rig);
                Rect dashCard = HelmOverlayLayout.DashCardRect(_focused, dashW, dashH, in cfg,
                                                               Screen.width, Screen.height,
                                                               HudBandLayout.ReservedTopPx());
                // The player's window layout goes over the shipped one (2026-08-07 ruling). Windowing
                // the rect HERE, before anything consumes it, is what makes the flush brow mounts and
                // the pointer mapping follow the moved/resized dash for free.
                dashCard = _window.Apply(dashCard, Screen.width, Screen.height,
                                         HudBandLayout.ReservedTopPx());
                bool cardVisible = _window.CardVisible;
                if (_image.enabled != cardVisible) _image.enabled = cardVisible;

                LayoutCard(HelmControlStyle.Lever, dashCard, dashW, dashH, 0f);
                if (cardVisible)
                    _dashCtl.UpdateAndPaint(helm, fit, SampleHeadingDegrees(), Time.deltaTime,
                                            _focused, ref _texture, _image);

                // Publish BEFORE reading the pointer: the brow instruments hang off this rect, and the
                // press routing below has to know where they are to leave their clicks alone. A
                // BAR-collapsed dash publishes nothing — its brow mounts are folded away with it.
                _dashCard = dashCard;
                _dashFit = fit;
                _dashLive = cardVisible;

                // An EXPANDED instrument owns the pointer outright (S4.5), chrome included: its card
                // sorts over the dash, so a bar the player cannot see must not swallow its clicks.
                // Collapse the instrument (click away / Esc) and the dash is draggable again.
                if (HelmInstrumentExpansion.AnyExpanded)
                {
                    _window.StandDown();
                }
                else
                {
                    _window.LayoutChrome(_canvas.transform);
                    if (_window.HandlePointer(_dashCtl.Interacting || _dragging)) return;
                }
                if (BoatUiWindows.AnySession) return;   // another window owns the pointer
                if (!cardVisible) return;               // the strip is all there is to click
                ReadDashPointer(helm, fit, dashCard, in cfg);
                return;
            }

            CardKind = style == HelmControlStyle.Lever ? HelmCardKind.Lever : HelmCardKind.Tiller;
            int rigW = style == HelmControlStyle.Lever ? LeverRigRender.W : TillerRigRender.W;
            int rigH = style == HelmControlStyle.Lever ? LeverRigRender.H : TillerRigRender.H;

            Rect card = HelmOverlayLayout.CardRect(_focused, rigW, rigH, in cfg, Screen.width, Screen.height);
            card = _window.Apply(card, Screen.width, Screen.height, HudBandLayout.ReservedTopPx());
            bool visible = _window.CardVisible;
            if (_image.enabled != visible) _image.enabled = visible;

            LayoutCard(style, card, rigW, rigH, helm.Steer);
            if (visible) Repaint(style, helm);

            // Same yield as the dash path: an expanded instrument owns the pointer, chrome included.
            if (HelmInstrumentExpansion.AnyExpanded)
            {
                _window.StandDown();
            }
            else
            {
                _window.LayoutChrome(_canvas.transform);
                if (_window.HandlePointer(_dragging)) return;
            }
            if (BoatUiWindows.AnySession) return;   // another window owns the pointer
            if (!visible) return;                   // BAR-collapsed: the strip is all there is
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

        // ---- layout -------------------------------------------------------------------------------

        private void LayoutCard(HelmControlStyle style, Rect card, int rigW, int rigH, float steer)
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

        // ---- pointer + keys ------------------------------------------------------------------------

        /// <summary>The composed dash's pointer routing (S2a): the card-level focus mechanics are
        /// S1's (click-to-focus, Esc/click-away out); WITHIN focus the press lands on the instrument
        /// under it — wheel rim grab, lever grip drag, binnacle travel-guide — via
        /// <see cref="HelmDashController"/>. Hit tests run in the dash's own rig pixels, so the
        /// card scale needs no special-casing (the S1 ScreenToRig discipline).
        ///
        /// <para>S4.5 gives the dash two things to YIELD: an expanded instrument owns the pointer and
        /// Esc outright, and a press inside a flush brow mount belongs to that instrument. Both are
        /// checked against the same shared state the instrument hosts use, never a second copy.</para></summary>
        private void ReadDashPointer(IHelmControl helm, in HelmFit fit, Rect card,
                                     in HelmOverlaySettings cfg)
        {
            ConsoleRigKind rig = fit.Rig;
            int rigW = HelmDashGeometry.CanvasW(rig), rigH = HelmDashGeometry.CanvasH(rig);
            var mouse = Mouse.current;
            bool held = mouse != null && mouse.leftButton.isPressed;

            if (_dashCtl.Interacting)
            {
                // A live drag finishes on its own terms — it started before anything expanded, and
                // dropping the wheel mid-turn would be worse than a moment of divided attention.
                // Off-card during a drag still tracks (the S1 lever precedent).
                if (mouse == null) return;
                HelmOverlayLayout.ScreenToRig(mouse.position.ReadValue(), card, rigW, rigH,
                                              out Vector2 rigPx);
                _dashCtl.Track(helm, rigPx, held, Time.deltaTime);
                return;
            }

            // An EXPANDED instrument owns the pointer and Esc (S4.5). Its card sits over the dash, so
            // without this a click meant for a MODE pusher would also toggle the dash's focus under
            // it, and Esc would close both at once. The instrument's own host handles the collapse.
            if (HelmInstrumentExpansion.AnyExpanded) return;

            var kb = Keyboard.current;
            if (_focused && kb != null && kb.escapeKey.wasPressedThisFrame)
            {
                _dashCtl.Deactivate(helm);
                _focused = false;
                return;
            }

            if (mouse == null) return;
            Vector2 pos = mouse.position.ReadValue();
            bool down = mouse.leftButton.wasPressedThisFrame;
            bool inCard = card.Contains(pos);

            // A press on a flush-mounted instrument belongs to that instrument, not to the dash
            // underneath it: at SMALL size the whole card is the focus button, so without this the
            // one click would both expand the sounder and enlarge the dash. Same pure mount rect the
            // instrument hosts hit-test against, so the two can never disagree about the boundary.
            if (down && HelmInstrumentMountLayout.TryBrowSounderRect(in fit, card, out Rect browMount)
                     && browMount.Contains(pos))
                return;

            if (!down) return;

            // A press on ANY window's chrome (another card's title strip, the watch's ×) is that
            // window's business — never this card's click-away or focus toggle (2026-08-07 ruling).
            if (BoatUiWindows.OverAnyChrome(pos)) return;

            if (!inCard)
            {
                if (_focused)
                {
                    _dashCtl.Deactivate(helm);
                    _focused = false;
                }
                return;
            }

            if (!_focused)
            {
                _focused = true;    // SMALL state: the whole card is the focus button (S1)
                return;
            }

            HelmOverlayLayout.ScreenToRig(pos, card, rigW, rigH, out Vector2 pressPx);
            _dashCtl.Press(helm, pressPx, in cfg);
        }

        private void ReadPointer(HelmControlStyle style, IHelmControl helm, Rect card,
                                 in HelmOverlaySettings cfg)
        {
            var kb = Keyboard.current;
            if (_focused && kb != null && kb.escapeKey.wasPressedThisFrame)
            {
                if (_dragging) { helm.EndDrag(); _dragging = false; }
                _focused = false;
                return;
            }

            var mouse = Mouse.current;
            if (mouse == null) return;
            Vector2 pos = mouse.position.ReadValue();
            bool down = mouse.leftButton.wasPressedThisFrame;
            bool held = mouse.leftButton.isPressed;
            bool inCard = card.Contains(pos);

            if (_dragging)
            {
                if (held)
                {
                    if (_dragIsTiller)
                    {
                        float scale = _focused ? cfg.FocusScale : cfg.SmallScale;
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

            if (!down) return;

            // A press on ANY window's chrome belongs to that window, never to this card's
            // click-away or focus toggle (2026-08-07 ruling).
            if (BoatUiWindows.OverAnyChrome(pos)) return;

            if (!inCard)
            {
                // Click-away leaves the focused state (owner addition 2026-08-03).
                if (_focused) _focused = false;
                return;
            }

            if (!_focused)
            {
                // SMALL state: the card is a button — click anywhere on it to FOCUS. Controls are
                // deliberately not draggable at dash size; the focused state is where they are
                // properly clickable (the owner's ask).
                _focused = true;
                return;
            }

            // FOCUSED: interact with the control.
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
                _dragging = true;              // tiller: vertical drag over the grip walks the drive
                _dragIsTiller = true;
                _dragStartDrive = helm.Drive;
                _dragStartScreenY = pos.y;
            }
        }
    }
}
