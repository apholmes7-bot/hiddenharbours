using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using HiddenHarbours.Core;

namespace HiddenHarbours.UI
{
    /// <summary>
    /// The CHARTPLOTTER's glass at the helm (ADR 0025 S6 PR 3) — the last mile that turns the renderer
    /// PR 2 shipped into an instrument you buy, mount and use. While the player pilots a hull whose
    /// effective fit carries a GPS, the plotter draws the real surveyed seabed under the boat, with the
    /// player's own waypoints, route and track on it.
    ///
    /// <para><b>Flush in the brow's GPS slot by default; expanded is a choice</b> (the S4.5 rule, and
    /// the sounder/finder precedent). The instrument paints into
    /// <see cref="HelmInstrumentMountLayout.TryBrowGpsRect"/> — the pilothouse brow's third slot, which
    /// only the Novi and the Cape have. Clicking it EXPANDS it to a centred card, and only there are the
    /// controls live. Clicking away, clicking the mount again, or Esc collapses it.</para>
    ///
    /// <para><b>Expanded shows the console face larger; the MAX/CNSL pusher shows the rig's full
    /// kit</b> (S6 PR 4). Click-to-expand keeps exactly the meaning it had — the same face, bigger,
    /// the sounder/finder consistency ruling — and the MAX face is a SEPARATE, deliberate press of
    /// <c>keys[0]</c>, which is the key the rig labels MAX/CNSL for precisely that (navRig.js:518).
    /// The MAX face carries the layer/tool rail, the waypoint and route manager, the range/bearing
    /// measure line and the depth-profile strip.</para>
    ///
    /// <para><b>The face is transient and only means anything while expanded.</b> The brow's GPS
    /// mount is a landscape slot sized for the console view (the rig's README says so), and the MAX
    /// face's 3×5 rail would be unreadable there — so flush is always console, and collapsing drops
    /// back to it. Which view you are looking through is where your eyes are, not a preference
    /// (<see cref="HelmInstrumentExpansion"/>'s standing rule).</para>
    ///
    /// <para><b>Typing a waypoint name takes the keyboard off the helm</b> — W/A/S/D are live helm
    /// keys and also four letters. The editor holds <see cref="HelmKeyCapture"/> for as long as it is
    /// open and releases it on commit AND on cancel; the gamepad is untouched, since you cannot type
    /// on one. See that class for why the suppression is on the raw key read.</para>
    ///
    /// <para><b>One raster, two presentations.</b> The flush face and the expanded card are the same
    /// texture at two rects, so they cannot disagree about where the boat is — the honesty invariant
    /// this arc has applied to every instrument.</para>
    ///
    /// <para><b>The state is never this host's.</b> Every waypoint, route point and crumb is read and
    /// written through <see cref="NavLocker"/> (which owns the caps), and every display preference
    /// through <see cref="InstrumentLocker"/>. The glass holds no nav state of its own, which is what
    /// makes a dropped waypoint survive a save/load rather than a scene reload. The TRACK is not written
    /// here at all — <see cref="NavTrackRecorder"/> accrues it unconditionally through its own host,
    /// whether or not a plotter is fitted, and this only draws it.</para>
    ///
    /// <para><b>Perf (rule 7).</b> The repaint key is quantized to what the glass can actually show: own
    /// ship to a whole CHART PIXEL, heading to a whole degree, speed and depth to the tenth their fields
    /// print. A boat sailing steadily therefore repaints at a bounded rate rather than per frame, and
    /// the chart BASE never re-bakes at all while sailing — <see cref="NavChartSource"/> is a pure
    /// function of (region rect × palette × terrain), none of which move under way. Both are asserted on
    /// this live host in PlayMode, not merely claimed. One reused <see cref="DrawSurface"/> +
    /// <see cref="Texture2D"/>; the nav reads fill caller-owned lists, so the steady state allocates
    /// nothing.</para>
    ///
    /// <para><b>Self-installing</b> (the S1 pattern): a <see cref="RuntimeInitializeOnLoadMethod"/> spawns
    /// one persistent host per play session, so every already-built scene grows the instrument with no
    /// builder re-run. Headless-safe and inert without a registered instrument service.</para>
    ///
    /// <para><b>Known limit, inherited and unchanged:</b> overlay clicks are not yet exclusive with other
    /// mouse gameplay, and the steer-session arbitration is the existing hosts'. Solving input
    /// exclusivity globally is explicitly not this slice.</para>
    /// </summary>
    [DefaultExecutionOrder(-40)]
    public sealed class ChartplotterOverlayHost : MonoBehaviour
    {
        [Header("Canvas")]
        [Tooltip("Sorting order of the chartplotter canvas. Above the helm dash's own canvas (60) and " +
                 "clear of the sounder/finder (62) — the plotter mounts in a DIFFERENT brow slot, so " +
                 "both can be on screen at once and neither should ever z-fight the other.")]
        [SerializeField] private int _sortingOrder = 63;

        private static ChartplotterOverlayHost _instance;

        private GameObject _cardGo;
        private RectTransform _cardRect;
        private RawImage _image;
        private RectTransform _imageRect;

        // One surface + texture PER FACE. The two faces are different native sizes (760×480 against
        // 980×648), and DrawSurface.ToTexture allocates a fresh Texture2D whenever the size changes —
        // sharing one would churn a texture on every MAX/CNSL press for no gain.
        private DrawSurface _surface, _maxSurface;
        private Texture2D _texture, _maxTexture;
        private readonly NavChartSource _chart = new NavChartSource();
        private readonly NavDepthProfile _profile = new NavDepthProfile();

        // Caller-owned read buffers — NavLocker fills these, so a repaint allocates nothing (rule 7).
        private readonly List<NavWaypoint> _waypoints = new();
        private readonly List<Vector2> _route = new();
        private readonly List<Vector2> _track = new();

        // ---- the MAX face's own state (transient — a tool is where your hand is, not a preference) --
        private bool _maxFace;
        private NavChartTool _tool = NavChartTool.Pan;
        private NavChartLayers _layers = NavChartLayers.All;
        private int _selWpt = -1;
        private NavMeasureState _measure;
        private Vector2 _measureFrom, _measureTo;
        private bool _measureDragging;
        private readonly NavNameEditor _editor = new NavNameEditor();
        private bool _holdingKeyboard;

        // Change-detection state — the quantized picture the current texture actually shows.
        private bool _painted;
        private long _shownPos;
        private int _shownHeading, _shownSogTenths, _shownDepthTenths, _shownNavRev;
        // The range as drawn. Compared with float equality on purpose and safely: it is recomputed by
        // the same ChartplotterSettings.RangeNMAt(step) every frame, so an unchanged rung is bit-equal.
        private float _shownRange;
        private bool _shownHeadUp, _shownNight, _shownHasBoat;
        private string _shownRegion;

        // …and the MAX face's terms, quantized to what each pane actually prints. Every one of these
        // is a control that can silently do nothing, so every one is in the key AND in a differential
        // test (the #421 lesson).
        private bool _shownMaxFace, _shownEditing;
        private int _shownTool, _shownLayers, _shownSel, _shownMeasureState;
        private long _shownMeasureFrom, _shownMeasureTo;
        private string _shownEditText = "";
        private int _shownTideTenths, _shownSetDeg, _shownDriftTenths;
        private bool _shownTideRising;

        // Bumped by every mutation THIS host makes. Together with the three list counts and the newest
        // crumb it is the whole "has the nav data changed" question, answered in O(1).
        private int _navRev;

        /// <summary>Which slot this host owns in the shared expansion arbiter.</summary>
        private const HelmInstrumentSlot Slot = HelmInstrumentSlot.Chartplotter;

        /// <summary>The one host per play session (it self-installs and survives scene loads). Exposed so
        /// a PlayMode test can drive the host that is actually running rather than a second copy — a
        /// duplicate destroys itself in <c>Awake</c>.</summary>
        public static ChartplotterOverlayHost Instance => _instance;

        /// <summary>Blown up to its own card rather than flush in the brow's GPS slot. Shared state, so
        /// only one instrument is ever expanded. Exposed for tests.</summary>
        public bool Expanded => HelmInstrumentExpansion.IsExpanded(Slot);

        /// <summary>True while the instrument is on screen — i.e. this hull's helm actually carries a
        /// GPS. Exposed for tests.</summary>
        public bool Showing => _cardGo != null && _cardGo.activeSelf;

        /// <summary>True while the chart is drawing FLUSH in the brow's authored GPS mount (the default),
        /// false while it is expanded or has fallen back to its own card. Exposed for tests.</summary>
        public bool FlushMounted { get; private set; }

        /// <summary>How many rasters this host has done — the repaint-cost guard's evidence, so a test
        /// can prove steady sailing costs a BOUNDED number of repaints rather than one per frame.</summary>
        public int RepaintCount { get; private set; }

        /// <summary>Showing the rig's MAXIMIZED face (rail + manager + profile) rather than the console
        /// face. Only ever true while <see cref="Expanded"/> is. Exposed for tests.</summary>
        public bool MaxFace => _maxFace && Expanded;

        /// <summary>
        /// Whether the texture ON SCREEN RIGHT NOW is the MAX face — the same differential seam as
        /// <see cref="NightShown"/>, for the flag this whole slice turns on. Reading what the last
        /// raster used, rather than what the state says, is the only thing that can catch a face that
        /// is selected and never drawn.
        /// </summary>
        public bool MaxFaceShown => _painted && _shownMaxFace;

        /// <summary>How many times the depth-ahead profile has been re-sampled. Must stay far below
        /// <see cref="RepaintCount"/> while sailing and must not move at all for a repaint the line did
        /// not move (rule 7 — <see cref="NavDepthProfile"/>).</summary>
        public int ProfileBakeCount => _profile.BakeCount;

        /// <summary>The waypoint the manager has selected, or −1. Exposed so a test can drive the
        /// column without synthesising a mouse.</summary>
        public int SelectedWaypoint => _selWpt;

        /// <summary>The rail's selected tool. Exposed for tests.</summary>
        public NavChartTool Tool => _tool;

        /// <summary>Which layers the rail has switched on. Exposed for tests.</summary>
        public NavChartLayers Layers => _layers;

        /// <summary>True while the waypoint-name editor is taking characters — and therefore while the
        /// helm's keys are deaf (<see cref="HelmKeyCapture"/>). Exposed for tests.</summary>
        public bool Editing => _editor.IsOpen;

        /// <summary>The name being typed. Exposed for tests.</summary>
        public string EditText => _editor.Text;

        /// <summary>How many times the chart BASE has been re-baked. The stronger half of the same guard:
        /// this must not move at all while sailing, however far the boat goes.</summary>
        public int BaseBakeCount { get; private set; }

        /// <summary>
        /// Whether the texture ON SCREEN RIGHT NOW was painted with the night palette — not what the
        /// rule would compute, but what the last raster actually used.
        ///
        /// <para>This is the seam the #421 lesson demands. A plumbed flag that compiles, passes, and
        /// quietly changes nothing is exactly how the helm's night panel shipped dead TWICE; the only
        /// assertion that can catch it is a DIFFERENTIAL one taken from the real production path, with
        /// a negative control proving the probe can also report the other answer. Reading the field the
        /// repaint guard itself compares means a night that never reaches the raster cannot show up
        /// here as true.</para>
        /// </summary>
        public bool NightShown => _painted && _shownNight;

        /// <summary>The orientation the current texture was painted with — the same differential seam as
        /// <see cref="NightShown"/>, for the flag that is just as easy to plumb and ignore.</summary>
        public bool HeadUpShown => _painted && _shownHeadUp;

        /// <summary>The range in NM the current texture was painted at, or 0 before the first raster.
        /// The third of the same family.</summary>
        public float RangeShown => _painted ? _shownRange : 0f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (_instance != null) return;
            var go = new GameObject("ChartplotterOverlayHost");
            _instance = go.AddComponent<ChartplotterOverlayHost>();
            DontDestroyOnLoad(go);
        }

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;

            var canvasGo = new GameObject("ChartplotterOverlayCanvas");
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = _sortingOrder;
            canvasGo.AddComponent<CanvasScaler>();   // constant pixel size — the rigs are pixel art

            _cardGo = new GameObject("ChartplotterCard");
            _cardGo.transform.SetParent(canvasGo.transform, false);
            _cardRect = _cardGo.AddComponent<RectTransform>();
            _cardRect.anchorMin = _cardRect.anchorMax = new Vector2(0f, 0f);
            _cardRect.pivot = new Vector2(0f, 0f);

            var imageGo = new GameObject("Chartplotter");
            imageGo.transform.SetParent(_cardGo.transform, false);
            _imageRect = imageGo.AddComponent<RectTransform>();
            _image = imageGo.AddComponent<RawImage>();
            _image.raycastTarget = false;   // we hit-test ourselves; never block other UI

            _cardGo.SetActive(false);
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
            // A host torn down mid-edit must never leave the helm deaf — that is a boat you cannot
            // steer, which is worse than the bug the gate prevents (HelmKeyCapture's remarks).
            ReleaseKeyboard();
            if (_texture != null) Destroy(_texture);
            if (_maxTexture != null) Destroy(_maxTexture);
        }

        private void Update()
        {
            IHelmInstruments instruments = GameServices.HelmInstruments;
            bool fitted = instruments != null && instruments.Fit.Gps;
            if (!fitted)
            {
                if (Expanded) HelmInstrumentExpansion.Collapse();
                CloseMaxFace();                 // losing the glass must not strand the keyboard
                _painted = false;
                FlushMounted = false;
                if (_cardGo.activeSelf) _cardGo.SetActive(false);
                return;
            }
            if (!_cardGo.activeSelf) _cardGo.SetActive(true);

            ChartplotterSettings cfg = GameServices.Chartplotter;
            SaveData save = GameServices.Save?.Current;
            string regionId = GameServices.CurrentRegionId;
            ChartplotterPrefs prefs = Prefs(save, instruments.HullId, in cfg);

            // ONE backlight rule, the standing one (#421): a lit panel lights what is mounted in it, and
            // the strip tap stays the EARLY-ON override. Never re-derived here.
            bool night = prefs.Night || HelmPanelLight.IsNight();

            // The survey. A pure function of (region rect × palette × terrain), so this is four
            // comparisons on a steady frame and a bake only when the region or the lights change.
            if (_chart.EnsureBaked(GameServices.CurrentRegionBounds, night, GameServices.TidalTerrain))
                BaseBakeCount++;

            bool expanded = Expanded;
            if (!expanded) CloseMaxFace();      // collapsed is always the console face (class remarks)
            bool max = _maxFace;

            NavRigState state = BuildState(instruments, in prefs, in cfg, night, max);
            ReadNav(save, regionId);
            ClampSelection();

            Rect card = GlassRect(expanded, max);
            LayoutCard(card);
            Repaint(in state, regionId, max);
            ReadPointer(instruments, save, regionId, in prefs, in cfg, in state, card, expanded, max);
        }

        /// <summary>
        /// Drop the MAX face and everything that only exists on it. Called whenever the instrument
        /// stops being expanded, by any route — the pusher, Esc, a click away, or losing the GPS with
        /// the boat.
        ///
        /// <para>The measure line goes with it because it is transient by contract (nothing about it
        /// is saved); the SELECTION goes because a manager row means nothing without a manager. The
        /// tool and the layer switches are kept: they are how this skipper works the chart, and having
        /// them reset every time the card closed would be a small, constant annoyance.</para>
        /// </summary>
        private void CloseMaxFace()
        {
            if (_editor.IsOpen) _editor.Close();
            ReleaseKeyboard();
            _maxFace = false;
            _selWpt = -1;
            _measure = NavMeasureState.Off;
            _measureDragging = false;
        }

        /// <summary>A selection can only ever point at a row that exists: waypoints are deleted from
        /// the column and marked from the chart, and a stale index would rename the wrong one.</summary>
        private void ClampSelection()
        {
            if (_selWpt >= _waypoints.Count) _selWpt = -1;
            if (_editor.IsOpen && (_editor.Target < 0 || _editor.Target >= _waypoints.Count))
            {
                _editor.Close();
                ReleaseKeyboard();
            }
        }

        /// <summary>The MAX face's pure state for this frame, composed from the persistent bits this
        /// host holds and the tide the environment publishes.</summary>
        private NavMaxState BuildMax(bool max, float setDeg, float driftKnots)
        {
            if (!max) return NavMaxState.Default;
            return new NavMaxState(_tool, _layers, _selWpt, _measure, _measureFrom, _measureTo,
                                   setDeg, driftKnots, _editor.IsOpen, _editor.Text);
        }

        /// <summary>This hull's stored plotter preferences, or the owner's defaults for a hull that has
        /// never been touched. Read through <see cref="InstrumentLocker"/> rather than through
        /// <see cref="IHelmInstruments"/> because the plotter's preferences did not exist when that seam
        /// was cut; the host already holds the save for its nav reads, so this costs nothing extra.
        /// FLAG lead-architect: if the seam should carry these the way it carries the sounder's, that is
        /// two members on the interface and one relay implementation, and this method is the only
        /// caller.</summary>
        private static ChartplotterPrefs Prefs(SaveData save, string hullId,
                                               in ChartplotterSettings cfg)
            => InstrumentLocker.ChartplotterPrefsFor(save, hullId, ChartplotterPrefs.FromDefaults(in cfg));

        /// <summary>
        /// Everything the glass draws this frame, gathered from the seams that already publish it.
        ///
        /// <para><b>The chart is turned by the BOW bearing, not by course-over-ground</b>, even though
        /// the strip labels the field COG. A GPS-derived course is undefined at a standstill
        /// (<see cref="BoatKinematics.CourseOverGroundDegrees"/> resolves to North there, and
        /// <see cref="CompassReadout.UnderwaySpeedMps"/> exists to say so), and a head-up chart fed an
        /// undefined course spins on the spot at every wharf. The bow bearing is always defined and is
        /// what the own-ship glyph must point along in any case. A real plotter switches to true COG
        /// once underway; doing that here would snap the chart at the threshold, so it is deliberately
        /// not done in this slice.</para>
        ///
        /// <para><b>Tide is now REAL, and only on the MAX face.</b> PR 2 fed
        /// <see cref="NavRigState.TideMetres"/> zero because no shipped pixel read it, and said the
        /// field would stay that way "until the field itself is ported". This is that slice: the MAX
        /// top bar's TIDE field (navRig.js:399) and the manager's TIDE pane (js:443) both print it. It
        /// is still passed as <see cref="float.NaN"/> on the CONSOLE face — feeding a continuously
        /// moving number into the repaint key to draw nothing is precisely the trade PR 2 refused, and
        /// refusing it is what keeps the console face's repaint pin green.</para>
        /// </summary>
        private NavRigState BuildState(IHelmInstruments instruments, in ChartplotterPrefs prefs,
                                       in ChartplotterSettings cfg, bool night, bool max)
        {
            bool hasBoat = instruments.TryReadPosition(out Vector2 pos) && NavMath.IsFinite(pos);

            float heading = 0f, sogKnots = 0f;
            IActiveBoatService probe = GameServices.ActiveBoat;
            if (probe != null)
            {
                BoatKinematics k = probe.Sample();
                if (k.HasBoat)
                {
                    heading = k.HeadingDegrees;
                    sogKnots = NavMath.MetresPerSecondToKnots(k.SpeedOverGround);
                }
            }

            float tide = float.NaN;
            bool rising = false;
            if (max) ReadTide(out tide, out rising, out _lastSetDeg, out _lastDriftKnots);
            else { _lastSetDeg = float.NaN; _lastDriftKnots = float.NaN; }

            return new NavRigState(
                pos, hasBoat, heading, sogKnots, prefs.RangeNM(in cfg),
                prefs.HeadUp ? NavRigGeometry.Orient.Head : NavRigGeometry.Orient.North,
                night, showTrack: true, tideMetres: tide, tideRising: rising);
        }

        // The set as last read — held so BuildState can fill it in the same pass that reads the tide
        // (they come from ONE EnvironmentSample) without the state struct having to carry it.
        private float _lastSetDeg = float.NaN, _lastDriftKnots = float.NaN;

        /// <summary>
        /// The tide height and the tidal SET, both from the environment service, in one sample.
        ///
        /// <para><b>The set is real sim data, not a placeholder.</b>
        /// <see cref="EnvironmentSample.CurrentVector"/> is the deterministic tidal current the whole
        /// game already runs on — <c>BoatController</c> sails on <c>vel − CurrentVector</c>, the water
        /// surface flows along it, the wake and the weed ride it. The plotter reads that same vector,
        /// so its SET/DRIFT is the current the hull is actually in rather than a number invented for
        /// the glass. (The S6 handoff expected no current source to exist and budgeted a config
        /// placeholder on the <c>PlaceholderWaterTempC</c> precedent; it does exist, so none is used.)</para>
        ///
        /// <para><b>Rising is a forward difference</b>, the same test <c>TideReadout</c> and
        /// <c>TideModel</c> use, at the same step: five per cent of an in-game hour. The plotter does
        /// not print a time-to-turn, so it does not scan for one.</para>
        ///
        /// <para>Everything is <see cref="float.NaN"/> with no service registered — the honest "--"
        /// the depth field already prints off-survey, never a confident zero.</para>
        /// </summary>
        private static void ReadTide(out float heightMetres, out bool rising, out float setDeg,
                                     out float driftKnots)
        {
            heightMetres = float.NaN;
            rising = false;
            setDeg = float.NaN;
            driftKnots = float.NaN;

            IEnvironmentService env = GameServices.Environment;
            if (env == null) return;

            EnvironmentSample sample = env.Sample();
            heightMetres = sample.TideHeight;

            IGameClock clock = GameServices.Clock;
            if (clock != null)
            {
                GameConfig config = GameServices.Config;
                double dt = config != null && config.SecondsPerHour > 0f
                    ? config.SecondsPerHour * 0.05 : 1.0;
                double now = clock.TotalSeconds;
                rising = env.TideHeightAt(now + dt) > env.TideHeightAt(now);
            }

            Vector2 current = sample.CurrentVector;
            // Slack water is a real reading, not a missing one: 000°/0.0KN is the truth there.
            setDeg = BoatKinematics.BearingDegrees(current);
            driftKnots = NavMath.MetresPerSecondToKnots(current.magnitude);
        }

        /// <summary>Refill the three draw buffers from the save. Each clears and refills a caller-owned
        /// list, so this allocates nothing after the first frame.</summary>
        private void ReadNav(SaveData save, string regionId)
        {
            NavLocker.WaypointsIn(save, regionId, _waypoints);
            NavLocker.RouteIn(save, regionId, _route);
            NavLocker.TrackIn(save, regionId, _track);
        }

        /// <summary>
        /// Where the glass draws this frame. FLUSH in the brow's authored GPS mount is the default
        /// (S4.5); EXPANDED is a centred card at the largest integer scale that fits.
        ///
        /// <para>The expanded rect is also the FALLBACK for a frame with no dash published — unreachable
        /// in shipped play, since the fit resolves through a <c>HelmConsoleDef</c> and no console means
        /// no GPS, but better than vanishing at boot or on a test rig. See
        /// <see cref="SounderOverlayHost"/> for the same reasoning at length.</para>
        /// </summary>
        private Rect GlassRect(bool expanded, bool max)
        {
            if (!expanded && HelmOverlayHost.TryDashCard(out Rect dash, out HelmFit fit)
                          && HelmInstrumentMountLayout.TryBrowGpsRect(in fit, dash, out Rect mount))
            {
                FlushMounted = true;
                return mount;
            }
            FlushMounted = false;
            Rect card = ChartplotterOverlayLayout.ExpandedCardRect(
                Screen.width, Screen.height,
                max ? NavRigGeometry.Face.Max : NavRigGeometry.Face.Console);
            // Keep the expanded glass out from under the always-on band (S4.5) — it slides down before
            // it shrinks, because this is the state the instrument is READ in.
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

        // ---- painting (change-detected on what the glass can SHOW, rule 7) -------------------------

        /// <summary>
        /// Repaint only when the picture would differ. Every term is quantized to the granularity the
        /// glass actually prints, which is what makes steady sailing cost a bounded number of rasters
        /// instead of one per frame:
        /// own ship to a whole CHART PIXEL, heading to a whole degree, SOG and datum depth to the tenth
        /// their fields show.
        ///
        /// <para>Deliberately NOT <c>state.Equals(_shownState)</c>, the finder's shape:
        /// <see cref="NavRigState"/> has no <c>IEquatable</c> implementation, so that call would go
        /// through <c>ValueType.Equals</c> — reflection, and a box, every frame (rule 7). Explicit
        /// fields also make the quantization visible, which is the point of the guard.</para>
        /// </summary>
        private void Repaint(in NavRigState st, string regionId, bool max)
        {
            long pos = ChartplotterOverlayLayout.QuantizePosition(st.Boat, st.RangeNM);
            int heading = Mathf.RoundToInt(NavMath.Norm360(st.HeadingDeg));
            int sogTenths = Mathf.RoundToInt(st.SpeedKnots * 10f);
            float depth = st.HasBoat ? _chart.DepthAt(st.Boat) : float.NaN;
            int depthTenths = float.IsNaN(depth) ? int.MinValue : Mathf.RoundToInt(depth * 10f);
            int navRev = NavRevision();
            bool headUp = st.Orient == NavRigGeometry.Orient.Head;

            // The MAX face's terms, each quantized to what its pane prints. They are ALL folded to a
            // constant on the console face, which is what lets the tide — a number that moves every
            // frame — be live on one face without costing the other a single extra raster.
            int tool = max ? (int)_tool : -1;
            int layers = max ? (int)_layers : -1;
            int sel = max ? _selWpt : -1;
            int measureState = max ? (int)_measure : -1;
            long measureFrom = max && _measure != NavMeasureState.Off
                ? ChartplotterOverlayLayout.QuantizePosition(_measureFrom, st.RangeNM) : 0L;
            long measureTo = max && _measure == NavMeasureState.Complete
                ? ChartplotterOverlayLayout.QuantizePosition(_measureTo, st.RangeNM) : 0L;
            bool editing = max && _editor.IsOpen;
            string editText = editing ? _editor.Text : "";
            int tideTenths = max && !float.IsNaN(st.TideMetres)
                ? Mathf.RoundToInt(st.TideMetres * 10f) : int.MinValue;
            bool tideRising = max && st.TideRising;
            int setDeg = max && !float.IsNaN(_lastSetDeg)
                ? Mathf.RoundToInt(NavMath.Norm360(_lastSetDeg)) : int.MinValue;
            int driftTenths = max && !float.IsNaN(_lastDriftKnots)
                ? Mathf.RoundToInt(_lastDriftKnots * 10f) : int.MinValue;

            if (_painted && pos == _shownPos && heading == _shownHeading && sogTenths == _shownSogTenths
                && depthTenths == _shownDepthTenths && st.RangeNM == _shownRange
                && navRev == _shownNavRev && headUp == _shownHeadUp && st.Night == _shownNight
                && st.HasBoat == _shownHasBoat && regionId == _shownRegion
                && max == _shownMaxFace && tool == _shownTool && layers == _shownLayers
                && sel == _shownSel && measureState == _shownMeasureState
                && measureFrom == _shownMeasureFrom && measureTo == _shownMeasureTo
                && editing == _shownEditing && editText == _shownEditText
                && tideTenths == _shownTideTenths && tideRising == _shownTideRising
                && setDeg == _shownSetDeg && driftTenths == _shownDriftTenths)
                return;

            // Always the rig's NATIVE size — the card is a scaled blit of it, never a smaller drawing
            // (the mount layout's letterbox contract).
            if (max)
            {
                _maxSurface ??= new DrawSurface(NavRigGeometry.MaxW, NavRigGeometry.MaxH);
                NavMaxState mx = BuildMax(true, _lastSetDeg, _lastDriftKnots);

                // The profile is sampled BEFORE the raster and only when its line has moved, so a
                // repaint the line did not cause costs nothing here (rule 7 — NavDepthProfile).
                NavRigGeometry.Layout L = ChartplotterOverlayLayout.NativeLayout(NavRigGeometry.Face.Max);
                _profile.EnsureBaked(_chart, st.Boat, st.HasBoat, st.HeadingDeg, st.RangeNM,
                                     Mathf.Min(NavDepthProfile.MaxSamples, Mathf.Max(0, L.Profile.width)));

                NavRigRender.RenderMax(_maxSurface, 0, 0, NavRigGeometry.MaxW, NavRigGeometry.MaxH,
                                       in st, in mx, _chart, _waypoints, _route, _track, _profile);
                _maxSurface.ToTexture(ref _maxTexture);
                _image.texture = _maxTexture;
            }
            else
            {
                _surface ??= new DrawSurface(NavRigGeometry.ConsoleW, NavRigGeometry.ConsoleH);
                NavRigRender.Render(_surface, 0, 0, NavRigGeometry.ConsoleW, NavRigGeometry.ConsoleH,
                                    in st, _chart, _waypoints, _route, _track);
                _surface.ToTexture(ref _texture);
                _image.texture = _texture;
            }
            RepaintCount++;

            _painted = true;
            _shownPos = pos; _shownHeading = heading; _shownSogTenths = sogTenths;
            _shownDepthTenths = depthTenths; _shownRange = st.RangeNM; _shownNavRev = navRev;
            _shownHeadUp = headUp; _shownNight = st.Night; _shownHasBoat = st.HasBoat;
            _shownRegion = regionId;
            _shownMaxFace = max; _shownTool = tool; _shownLayers = layers; _shownSel = sel;
            _shownMeasureState = measureState; _shownMeasureFrom = measureFrom;
            _shownMeasureTo = measureTo; _shownEditing = editing; _shownEditText = editText;
            _shownTideTenths = tideTenths; _shownTideRising = tideRising;
            _shownSetDeg = setDeg; _shownDriftTenths = driftTenths;
        }

        /// <summary>
        /// An O(1) fingerprint of the nav data on the glass. The three list counts catch every add and
        /// remove; <see cref="_navRev"/> catches the mutations this host makes that leave a count alone.
        ///
        /// <para>The newest crumb is folded in for one specific reason: the track is a RING BUFFER, so
        /// once it is full a new crumb drops the oldest and the COUNT never moves again. Without this
        /// term the breadcrumb would visibly freeze the moment it filled, which is exactly the sort of
        /// bug that only appears after an hour of sailing.</para>
        /// </summary>
        private int NavRevision()
        {
            int h = _navRev * 397;
            h = (h * 31) ^ _waypoints.Count;
            h = (h * 31) ^ _route.Count;
            h = (h * 31) ^ _track.Count;
            if (_track.Count > 0)
            {
                Vector2 newest = _track[_track.Count - 1];
                h = (h * 31) ^ newest.x.GetHashCode();
                h = (h * 31) ^ newest.y.GetHashCode();
            }
            return h;
        }

        // ---- pointer + keys ------------------------------------------------------------------------

        /// <summary>
        /// The selection model, the sounder's exactly (S4.5). FLUSH: the whole face is one button — click
        /// it to EXPAND, and nothing else is live, so a glance at the dash can never be a stray waypoint.
        /// EXPANDED: the four pushers, the two strips and the chart all work, and a click ANYWHERE
        /// outside collapses. A click inside that lands on no control does nothing rather than closing,
        /// so a near-miss on a pusher is never punished. Esc collapses from anywhere.
        /// </summary>
        private void ReadPointer(IHelmInstruments instruments, SaveData save, string regionId,
                                 in ChartplotterPrefs prefs, in ChartplotterSettings cfg,
                                 in NavRigState st, Rect card, bool expanded, bool max)
        {
            var kb = Keyboard.current;

            // While the name editor is open it owns the keyboard entirely: Esc cancels the EDIT (not
            // the card — you would lose the mark you were naming), Enter commits, and every other key
            // is a character. Nothing else on the instrument is live, so a stray press cannot drop a
            // waypoint halfway through typing one's name.
            if (max && _editor.IsOpen)
            {
                ReadNameKeys(kb, save, regionId);
                if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
                    CommitName(save, regionId);       // one click anywhere finishes the edit
                return;
            }

            if (expanded && kb != null && kb.escapeKey.wasPressedThisFrame)
            {
                // Esc steps back one level: out of the MAX face first, then out of the card. Closing
                // the whole instrument from the face you were working in would lose the tool, the
                // selection and the measure line in one keystroke.
                if (max) CloseMaxFace();
                else HelmInstrumentExpansion.Collapse();
                return;
            }

            var mouse = Mouse.current;
            if (mouse == null) return;

            Vector2 pos = mouse.position.ReadValue();
            bool inCard = card.Contains(pos);
            int faceW = max ? NavRigGeometry.MaxW : NavRigGeometry.ConsoleW;
            int faceH = max ? NavRigGeometry.MaxH : NavRigGeometry.ConsoleH;
            HelmOverlayLayout.ScreenToRig(pos, card, faceW, faceH, out Vector2 rigPx);

            // The measure DRAG runs before everything else and swallows the whole gesture, so a drag
            // that wanders off the chart mid-pull cannot also read as a click-away collapse.
            if (max && _tool == NavChartTool.Measure
                && ReadMeasureDrag(mouse, in st, rigPx, inCard))
                return;

            if (!mouse.leftButton.wasPressedThisFrame) return;

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

            // The three numbers the hit map gets are EXACTLY the three the renderer got (RenderFace) —
            // the column is a running cursor, so a different answer to "is a row selected" would put
            // the buttons in one place and the hit boxes in another.
            int hit = max
                ? ChartplotterOverlayLayout.HitTestMax(rigPx, _waypoints.Count, _route.Count,
                                                       _editor.IsOpen || _selWpt >= 0)
                : ChartplotterOverlayLayout.HitTest(rigPx);
            Apply(instruments, save, regionId, in prefs, in cfg, in st, rigPx, hit);
        }

        // ---- the measure line (transient — never saved, by contract) --------------------------------

        /// <summary>
        /// Press, pull, release: the range/bearing line. Returns true while the gesture owns the
        /// pointer, so the caller stops there.
        ///
        /// <para><b>Dragged rather than the rig's two taps</b> (js:440 "TAP TWO POINTS"). The source is
        /// written for a touch plotter; this ships on a desktop with a held button, where a drag is
        /// both the natural gesture and the one that lets you SEE the bearing swing as you pull.</para>
        ///
        /// <para><b>Quantized to whole chart pixels while dragging</b> — the own-ship precedent. A
        /// mouse reports sub-pixel motion every frame; repainting the whole face for a move the glass
        /// cannot show would put a per-frame raster right back into the steady state (rule 7).</para>
        ///
        /// <para>A press that never moves clears the line rather than leaving a zero-length one: a
        /// click is how you put the tool away.</para>
        /// </summary>
        private bool ReadMeasureDrag(Mouse mouse, in NavRigState st, Vector2 rigPx, bool inCard)
        {
            if (mouse.leftButton.wasPressedThisFrame && inCard
                && ChartplotterOverlayLayout.TryChartTapToWorld(rigPx, in st, NavRigGeometry.Face.Max,
                                                                out Vector2 from, out _))
            {
                _measureDragging = true;
                _measureFrom = from;
                _measureTo = from;
                _measure = NavMeasureState.Anchored;
                return true;
            }

            if (!_measureDragging) return false;

            if (mouse.leftButton.isPressed)
            {
                if (ChartplotterOverlayLayout.TryChartTapToWorld(rigPx, in st, NavRigGeometry.Face.Max,
                                                                 out Vector2 to, out _))
                {
                    long had = ChartplotterOverlayLayout.QuantizePosition(_measureTo, st.RangeNM);
                    long now = ChartplotterOverlayLayout.QuantizePosition(to, st.RangeNM);
                    if (had != now) _measureTo = to;      // a sub-pixel wiggle is not a change
                }
                return true;
            }

            // Released.
            _measureDragging = false;
            float span = NavMath.DistanceMetres(_measureFrom, _measureTo);
            float quantum = ChartplotterOverlayLayout.MetresPerChartPixel(st.RangeNM);
            _measure = span > quantum * MinMeasureDragPx
                ? NavMeasureState.Complete
                : NavMeasureState.Off;
            return true;
        }

        /// <summary>How far (in chart pixels) a drag must run before it is a measurement rather than a
        /// click. Three: enough that a shaky click still puts the tool away, small enough that a
        /// deliberate short measurement is never swallowed.</summary>
        private const float MinMeasureDragPx = 3f;

        // ---- the name editor (and the helm it takes the keyboard from) ------------------------------

        /// <summary>Open the editor on a waypoint and take the keyboard off the helm.</summary>
        private void BeginName(int index)
        {
            if (index < 0 || index >= _waypoints.Count) return;
            _editor.Open(index, _waypoints[index].Name);
            if (_holdingKeyboard) return;
            HelmKeyCapture.Begin();
            _holdingKeyboard = true;

            // Subscribe to the DEVICE we will unsubscribe from — Keyboard.current can be replaced
            // while the editor is open (a keyboard unplugged mid-edit), and unsubscribing from a
            // different device would leave this handler attached to the old one forever.
            _typingOn = Keyboard.current;
            if (_typingOn != null) _typingOn.onTextInput += OnTextInput;
        }

        /// <summary>Give the keyboard back. Idempotent, and called from every exit — commit, cancel,
        /// collapse, losing the GPS, teardown — because the one bug this must not have is a helm left
        /// deaf.</summary>
        private void ReleaseKeyboard()
        {
            if (_typingOn != null) { _typingOn.onTextInput -= OnTextInput; _typingOn = null; }
            if (!_holdingKeyboard) return;
            HelmKeyCapture.End();
            _holdingKeyboard = false;
        }

        private Keyboard _typingOn;

        private void ReadNameKeys(Keyboard kb, SaveData save, string regionId)
        {
            // No keyboard is NO INPUT, not a cancel. Discarding a half-typed name because a device went
            // away for a frame would be the worst possible reading of "nothing happened" — and headless
            // batchmode has no keyboard at all, so it is also the difference between this field being
            // testable and not.
            if (kb == null) return;

            if (kb.escapeKey.wasPressedThisFrame) { CancelName(); return; }
            if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame)
            {
                CommitName(save, regionId);
                return;
            }
            if (kb.backspaceKey.wasPressedThisFrame) _editor.Backspace();
        }

        /// <summary>
        /// One typed character, from the Input System's text event rather than from key codes — which
        /// is what makes the field work on a keyboard layout that is not the developer's.
        /// Subscribed for the editor's lifetime only.
        /// </summary>
        private void OnTextInput(char ch)
        {
            if (!_editor.IsOpen) return;
            _editor.Type(ch);          // NavNameEditor owns the charset rule; a rejected key is a no-op
        }

        private bool CommitName(SaveData save, string regionId)
        {
            int target = _editor.Target;
            bool ok = _editor.TryCommit(out string name);
            ReleaseKeyboard();
            if (!ok || save == null) return false;
            if (!NavLocker.RenameWaypoint(save, regionId, target, name)) return false;
            Committed();
            return true;
        }

        /// <summary>
        /// Type into the open name field, exactly as the keyboard's text event does — the same
        /// <see cref="NavNameEditor"/> call, the same charset rule, no shortcut past either.
        ///
        /// <para>Public for the reason <see cref="Apply"/> is: headless batchmode drops key events (the
        /// limit <c>DevBoatInput</c> documents for its own arbitration table), so a PlayMode test cannot
        /// press W and watch the buffer. Driving the production path from here is what keeps the
        /// assertion about the real code rather than about a test double.</para>
        /// </summary>
        public void TypeName(string text)
        {
            if (text == null) return;
            foreach (char ch in text) OnTextInput(ch);
        }

        /// <summary>Finish the edit and store the name, as Enter does. Returns true iff a row changed.</summary>
        public bool CommitName() => CommitName(GameServices.Save?.Current, GameServices.CurrentRegionId);

        /// <summary>Abandon the edit and give the keyboard back, as Esc does.</summary>
        public void CancelName()
        {
            _editor.Close();
            ReleaseKeyboard();
        }

        /// <summary>
        /// Turn a hit into an action. Public so a PlayMode test can drive the controls without
        /// synthesising a mouse — the sounder's <c>Apply</c> precedent.
        ///
        /// <para><b>The MAX/CNSL pusher no longer collapses.</b> On the console face it opens the MAX
        /// face (the label the rig prints on it — navRig.js:518); on the MAX face it goes back. Closing
        /// the card is what click-away and Esc are for, and they are unchanged. This is the one
        /// behaviour S6 PR 3 deliberately mapped to the wrong thing while the MAX face did not exist,
        /// and it is the reason this key was left alone then.</para>
        ///
        /// <para><b>What the CHART tap means is now the rail's business.</b> With a tool selected the
        /// tap places a mark or a route point (the rig's own instruction, js:415); with the neutral
        /// PAN tool it SELECTS the waypoint under it for the manager. The console face keeps the only
        /// gesture it ever had — remove-under-the-fingertip — because it has no rail to mean anything
        /// else.</para>
        /// </summary>
        public bool Apply(IHelmInstruments instruments, SaveData save, string regionId,
                          in ChartplotterPrefs prefs, in ChartplotterSettings cfg,
                          in NavRigState st, Vector2 rigPx, int hit)
        {
            int railSlot = ChartplotterOverlayLayout.RailSlotOf(hit);
            if (railSlot >= 0) return ApplyRail(railSlot);

            int row = ChartplotterOverlayLayout.WaypointRowOf(hit);
            if (row >= 0) return SelectWaypoint(row);

            switch (hit)
            {
                case ChartplotterOverlayLayout.KeyMax:
                    return ToggleMaxFace();

                case ChartplotterOverlayLayout.KeyMark:
                    // On the MAX face this pusher IS the rail's MRK tool — the rig lights it from the
                    // tool, not from an action (js:519), so pressing it must pick the tool up and put
                    // it down again rather than drop a mark and leave the rail lying.
                    if (_maxFace) return ApplyTool(NavChartTool.Mark);
                    return MarkHere(save, regionId, in st, in cfg);

                case ChartplotterOverlayLayout.KeyIn:
                    return SetPrefs(instruments, save, prefs.SteppedRange(+1, in cfg));

                case ChartplotterOverlayLayout.KeyOut:
                    return SetPrefs(instruments, save, prefs.SteppedRange(-1, in cfg));

                case ChartplotterOverlayLayout.OrientHit:
                    return SetPrefs(instruments, save, prefs.WithHeadUp(!prefs.HeadUp));

                case ChartplotterOverlayLayout.NightHit:
                    return SetPrefs(instruments, save, prefs.WithNight(!prefs.Night));

                case ChartplotterOverlayLayout.RouteHit:
                    return ToggleRoute(save, regionId, in cfg);

                case ChartplotterOverlayLayout.ActionName:
                    if (_selWpt < 0) return false;
                    BeginName(_selWpt);
                    return true;

                case ChartplotterOverlayLayout.ActionDelete:
                    return DeleteSelected(save, regionId);

                case ChartplotterOverlayLayout.ChartHit:
                    return _maxFace ? ChartToolTap(save, regionId, in st, in cfg, rigPx)
                                    : RemoveAt(save, regionId, in st, rigPx);

                default:
                    return false;                 // ProfileHit / ManagerHit / NoHit are inert by design
            }
        }

        // ---- the MAX face's actions ------------------------------------------------------------------

        /// <summary>MAX/CNSL. Only meaningful while expanded — the flush mount is always the console
        /// face, and the pusher is not live there anyway (the whole flush face is one click target).</summary>
        private bool ToggleMaxFace()
        {
            if (!Expanded) return false;
            if (_maxFace) { CloseMaxFace(); return true; }
            _maxFace = true;
            return true;
        }

        /// <summary>
        /// A rail press: pick a tool, or switch a layer.
        ///
        /// <para>The four DORMANT layers (LAND, ROCK, BUOY, TRFC) refuse the press rather than toggling
        /// a flag that no pixel reads. That is the #421 lesson applied to a switch instead of a
        /// setting: a control that lights up and changes nothing is indistinguishable from one that is
        /// broken, and the honest version simply does not move. <see cref="NavChartLayers"/> says which
        /// is which and why.</para>
        /// </summary>
        private bool ApplyRail(int slot)
        {
            if (!_maxFace) return false;
            if (NavRigGeometry.RailSlotIsTool(slot))
                return ApplyTool(NavRigGeometry.RailTool(slot));

            NavChartLayers layer = NavRigGeometry.RailLayer(slot);
            if ((layer & NavChartLayers.Live) == 0) return false;
            _layers ^= layer;
            return true;
        }

        /// <summary>Pick a tool, or put the selected one down (press it again → back to PAN). Dropping
        /// the measure line with the measure tool means the transient line never outlives the tool that
        /// drew it.</summary>
        private bool ApplyTool(NavChartTool tool)
        {
            if (!_maxFace) return false;
            NavChartTool next = _tool == tool ? NavChartTool.Pan : tool;
            if (next == _tool) return false;
            _tool = next;
            if (_tool != NavChartTool.Measure)
            {
                _measure = NavMeasureState.Off;
                _measureDragging = false;
            }
            return true;
        }

        private bool SelectWaypoint(int row)
        {
            if (!_maxFace || row >= _waypoints.Count) return false;
            _selWpt = _selWpt == row ? -1 : row;      // press the lit row again to deselect
            return true;
        }

        private bool DeleteSelected(SaveData save, string regionId)
        {
            if (_selWpt < 0 || _selWpt >= _waypoints.Count) return false;
            if (!NavLocker.RemoveWaypointAt(save, regionId, _selWpt)) return false;
            _selWpt = -1;
            Committed();
            return true;
        }

        /// <summary>
        /// A chart tap on the MAX face, read through the rail's selected tool. MEASURE never arrives
        /// here — its whole gesture is consumed by the drag handler upstream.
        /// </summary>
        private bool ChartToolTap(SaveData save, string regionId, in NavRigState st,
                                  in ChartplotterSettings cfg, Vector2 rigPx)
        {
            if (!ChartplotterOverlayLayout.TryChartTapToWorld(rigPx, in st, NavRigGeometry.Face.Max,
                                                              out Vector2 world, out float radius))
                return false;

            switch (_tool)
            {
                case NavChartTool.Mark:
                {
                    // Tap-to-place at last — the gesture the console face documented as belonging to
                    // this face (MarkHere's remarks). Named on the same auto scheme a MARK press uses,
                    // and renameable from the column the moment it lands.
                    var wpt = new NavWaypoint(regionId, NextMarkName(), world, NavWaypointKind.Mark);
                    if (!NavLocker.AddWaypoint(save, in wpt, in cfg)) return false;
                    Committed();
                    return true;
                }

                case NavChartTool.Route:
                    if (!NavLocker.AddRoutePoint(save, regionId, world, in cfg)) return false;
                    Committed();
                    return true;

                default:
                {
                    // PAN: select the mark under the fingertip, or clear the selection on open water.
                    int found = NearestWaypoint(world, radius);
                    if (found == _selWpt) return false;
                    _selWpt = found;
                    return true;
                }
            }
        }

        /// <summary>The waypoint nearest a tap within the fingertip's radius, or −1 — the same
        /// nearest-within-radius rule <see cref="NavLocker.RemoveWaypointNear"/> uses, run over the
        /// list already in hand so a selection costs no extra walk of the save.</summary>
        private int NearestWaypoint(Vector2 world, float radiusMetres)
        {
            int best = -1;
            float bestSq = radiusMetres * radiusMetres;
            for (int i = 0; i < _waypoints.Count; i++)
            {
                float sq = (_waypoints[i].Pos - world).sqrMagnitude;
                if (sq > bestSq) continue;
                bestSq = sq;
                best = i;
            }
            return best;
        }

        // ---- the six actions (every one of them through NavLocker / InstrumentLocker) ---------------

        /// <summary>
        /// Mark a waypoint AT OWN SHIP — the console face's MARK pusher (navRig.js:519).
        ///
        /// <para><b>At the boat, not at a tap, and the rig is why.</b> The rig does author tap-to-place,
        /// but only through the MAX face's tool rail: its own empty-route legend reads "SELECT ROUTE TOOL
        /// &amp; TAP THE CHART" (navRig.js:415) and <c>tool</c> is one of pan/mark/route/measure
        /// (README, the params table). The console face has no rail — <see cref="NavRigGeometry.Layout"/>
        /// gives <c>Rail</c> a zero size outside <see cref="NavRigGeometry.Face.Max"/> — so there is no
        /// selected tool for a chart tap to mean anything against. A bare MARK key with no tool
        /// selected can only mean "mark here", which is also what the key does on every real plotter.
        /// Tap-to-place arrives with the rail, in the MAX slice.</para>
        ///
        /// <para>Refused, honestly, at the waypoint cap — <see cref="NavLocker.AddWaypoint"/> owns that
        /// rule and returns false rather than silently evicting the oldest.</para>
        /// </summary>
        private bool MarkHere(SaveData save, string regionId, in NavRigState st,
                              in ChartplotterSettings cfg)
        {
            if (!st.HasBoat) return false;          // no fix, nothing to mark
            var wpt = new NavWaypoint(regionId, NextMarkName(), st.Boat, NavWaypointKind.Mark);
            if (!NavLocker.AddWaypoint(save, in wpt, in cfg)) return false;
            Committed();
            return true;
        }

        /// <summary>The next auto name, following the rig's own sample rows ("MARK 1", navRig.js:160).
        /// Numbered from how many are already on THIS region's chart, so the names read in the order you
        /// laid them down on the water in front of you.</summary>
        private string NextMarkName() => "MARK " + (_waypoints.Count + 1);

        /// <summary>Remove the waypoint under the fingertip. <see cref="NavLocker.RemoveWaypointNear"/>
        /// is nearest-within-radius precisely because the caller is a fingertip on a chart rather than an
        /// index, and the radius is converted from chart pixels so the gesture feels the same at every
        /// range.</summary>
        private bool RemoveAt(SaveData save, string regionId, in NavRigState st, Vector2 rigPx)
        {
            if (!ChartplotterOverlayLayout.TryChartTapToWorld(rigPx, in st, out Vector2 world,
                                                              out float radius))
                return false;
            if (!NavLocker.RemoveWaypointNear(save, regionId, world, radius)) return false;
            Committed();
            return true;
        }

        /// <summary>
        /// The route strip's tap: lay the route through this region's waypoints in the order they were
        /// marked, or clear it if one is already laid.
        ///
        /// <para><b>Why the strip and not a pusher.</b> The rig builds a route with the MAX rail's route
        /// tool (navRig.js:415), and that rail is deferred with the rest of the MAX face; the console
        /// face's four pushers are already MAX/MARK/IN/OUT and none of them may be repurposed. The route
        /// STRIP is the honest home for the gesture in the meantime: it is the element that already
        /// reports the route ("ROUTE n LEGS" / "- NO ACTIVE ROUTE -"), so the thing you click is the
        /// thing that tells you what happened. It retires when the rail lands.</para>
        /// </summary>
        private bool ToggleRoute(SaveData save, string regionId, in ChartplotterSettings cfg)
        {
            if (_route.Count > 0)
            {
                if (!NavLocker.ClearRoute(save)) return false;
                Committed();
                return true;
            }

            if (_waypoints.Count < 2) return false;      // a single point is not a passage
            bool any = false;
            for (int i = 0; i < _waypoints.Count; i++)
            {
                if (!NavLocker.AddRoutePoint(save, regionId, _waypoints[i].Pos, in cfg))
                    break;                                // at the leg cap — keep the legs that fit
                any = true;
            }
            if (!any) return false;
            Committed();
            return true;
        }

        /// <summary>Persist a preference change, on the relay's own cadence: a deliberate act is worth an
        /// I/O, and the locker's no-op comparison means an unchanged press never writes.</summary>
        private bool SetPrefs(IHelmInstruments instruments, SaveData save, in ChartplotterPrefs next)
        {
            if (instruments == null) return false;
            if (!InstrumentLocker.SetChartplotterPrefs(save, instruments.HullId, in next)) return false;
            GameServices.Save?.Save();
            _navRev++;                                   // the picture can change without a list moving
            return true;
        }

        /// <summary>A nav mutation landed: force the next frame to repaint and put it on disk. Marking a
        /// waypoint is a deliberate act, so it takes the same save-on-write cadence a fitted instrument
        /// does — unlike the track, which accrues continuously and rides the normal save cadence.</summary>
        private void Committed()
        {
            _navRev++;
            GameServices.Save?.Save();
        }
    }
}
