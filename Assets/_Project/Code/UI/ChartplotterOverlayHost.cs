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
    /// <para><b>The expanded face is the SAME console face, larger</b> — not the rig's MAX face. The
    /// rig's second face is advanced kit (a layer/tool rail, a waypoint and route manager, a measure
    /// line, a depth-profile strip) and is its own slice; nothing here forecloses it. See
    /// <see cref="NavRigRender"/>'s remarks, which say the same thing from the renderer's side.</para>
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

        private DrawSurface _surface;
        private Texture2D _texture;
        private readonly NavChartSource _chart = new NavChartSource();

        // Caller-owned read buffers — NavLocker fills these, so a repaint allocates nothing (rule 7).
        private readonly List<NavWaypoint> _waypoints = new();
        private readonly List<Vector2> _route = new();
        private readonly List<Vector2> _track = new();

        // Change-detection state — the quantized picture the current texture actually shows.
        private bool _painted;
        private long _shownPos;
        private int _shownHeading, _shownSogTenths, _shownDepthTenths, _shownNavRev;
        // The range as drawn. Compared with float equality on purpose and safely: it is recomputed by
        // the same ChartplotterSettings.RangeNMAt(step) every frame, so an unchanged rung is bit-equal.
        private float _shownRange;
        private bool _shownHeadUp, _shownNight, _shownHasBoat;
        private string _shownRegion;

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
            if (_texture != null) Destroy(_texture);
        }

        private void Update()
        {
            IHelmInstruments instruments = GameServices.HelmInstruments;
            bool fitted = instruments != null && instruments.Fit.Gps;
            if (!fitted)
            {
                if (Expanded) HelmInstrumentExpansion.Collapse();
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

            NavRigState state = BuildState(instruments, in prefs, in cfg, night);
            ReadNav(save, regionId);

            bool expanded = Expanded;
            Rect card = GlassRect(expanded);
            LayoutCard(card);
            Repaint(in state, regionId);
            ReadPointer(instruments, save, regionId, in prefs, in cfg, in state, card, expanded);
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
        /// <para><b>Tide is passed as nothing on purpose.</b> <see cref="NavRigState"/> carries a tide
        /// height and set because the rig's own top bar has an optional TIDE field (navRig.js:399), but
        /// PR 2's console <c>TopBar</c> does not port it — no shipped pixel reads either value. Feeding
        /// them a live tide would put a continuously-moving number into the repaint key to draw
        /// precisely nothing, so they stay zero until the field itself is ported.</para>
        /// </summary>
        private NavRigState BuildState(IHelmInstruments instruments, in ChartplotterPrefs prefs,
                                       in ChartplotterSettings cfg, bool night)
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

            return new NavRigState(
                pos, hasBoat, heading, sogKnots, prefs.RangeNM(in cfg),
                prefs.HeadUp ? NavRigGeometry.Orient.Head : NavRigGeometry.Orient.North,
                night, showTrack: true, tideMetres: 0f, tideRising: false);
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
        private Rect GlassRect(bool expanded)
        {
            if (!expanded && HelmOverlayHost.TryDashCard(out Rect dash, out HelmFit fit)
                          && HelmInstrumentMountLayout.TryBrowGpsRect(in fit, dash, out Rect mount))
            {
                FlushMounted = true;
                return mount;
            }
            FlushMounted = false;
            Rect card = ChartplotterOverlayLayout.ExpandedCardRect(Screen.width, Screen.height);
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
        private void Repaint(in NavRigState st, string regionId)
        {
            long pos = ChartplotterOverlayLayout.QuantizePosition(st.Boat, st.RangeNM);
            int heading = Mathf.RoundToInt(NavMath.Norm360(st.HeadingDeg));
            int sogTenths = Mathf.RoundToInt(st.SpeedKnots * 10f);
            float depth = st.HasBoat ? _chart.DepthAt(st.Boat) : float.NaN;
            int depthTenths = float.IsNaN(depth) ? int.MinValue : Mathf.RoundToInt(depth * 10f);
            int navRev = NavRevision();
            bool headUp = st.Orient == NavRigGeometry.Orient.Head;

            if (_painted && pos == _shownPos && heading == _shownHeading && sogTenths == _shownSogTenths
                && depthTenths == _shownDepthTenths && st.RangeNM == _shownRange
                && navRev == _shownNavRev && headUp == _shownHeadUp && st.Night == _shownNight
                && st.HasBoat == _shownHasBoat && regionId == _shownRegion)
                return;

            // Always the rig's NATIVE size — the card is a scaled blit of it, never a smaller drawing
            // (the mount layout's letterbox contract).
            _surface ??= new DrawSurface(NavRigGeometry.ConsoleW, NavRigGeometry.ConsoleH);
            NavRigRender.Render(_surface, 0, 0, NavRigGeometry.ConsoleW, NavRigGeometry.ConsoleH, in st,
                                _chart, _waypoints, _route, _track);
            _surface.ToTexture(ref _texture);
            _image.texture = _texture;
            RepaintCount++;

            _painted = true;
            _shownPos = pos; _shownHeading = heading; _shownSogTenths = sogTenths;
            _shownDepthTenths = depthTenths; _shownRange = st.RangeNM; _shownNavRev = navRev;
            _shownHeadUp = headUp; _shownNight = st.Night; _shownHasBoat = st.HasBoat;
            _shownRegion = regionId;
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
                                 in NavRigState st, Rect card, bool expanded)
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

            HelmOverlayLayout.ScreenToRig(pos, card, NavRigGeometry.ConsoleW, NavRigGeometry.ConsoleH,
                                          out Vector2 rigPx);
            Apply(instruments, save, regionId, in prefs, in cfg, in st, rigPx,
                  ChartplotterOverlayLayout.HitTest(rigPx));
        }

        /// <summary>
        /// Turn a hit into an action. Public so a PlayMode test can drive the controls without
        /// synthesising a mouse — the sounder's <c>Apply</c> precedent.
        ///
        /// <para><b>The interaction scope is exactly this and no more</b> (S6 PR 3): mark a waypoint,
        /// remove one, build or clear the single route, step the range, toggle the orientation, and the
        /// night backlight tap. No measure tool, no route editing beyond append-and-clear, no autopilot —
        /// those live with the MAX face, in its own slice.</para>
        /// </summary>
        public bool Apply(IHelmInstruments instruments, SaveData save, string regionId,
                          in ChartplotterPrefs prefs, in ChartplotterSettings cfg,
                          in NavRigState st, Vector2 rigPx, int hit)
        {
            switch (hit)
            {
                case ChartplotterOverlayLayout.KeyMax:
                    HelmInstrumentExpansion.Click(Slot);       // toggles: expanded → collapsed
                    return true;

                case ChartplotterOverlayLayout.KeyMark:
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

                case ChartplotterOverlayLayout.ChartHit:
                    return RemoveAt(save, regionId, in st, rigPx);

                default:
                    return false;
            }
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
