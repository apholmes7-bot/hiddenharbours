using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using HiddenHarbours.Core;

namespace HiddenHarbours.UI
{
    /// <summary>
    /// THE RADAR at the helm (ADR 0025 S5) — the slice that turns the brow's STANDBY placeholder into a
    /// live PPI. While the player pilots a hull whose effective fit carries a radar, the scope sweeps
    /// the real world: the ambient fleet, the buoys (theirs and the player's), and the coast as it
    /// stands at this state of tide.
    ///
    /// <para><b>No synthetic contacts, ever.</b> The rig ships a hand-authored demo scene
    /// (<c>radarRig.js:136-146</c>) and it is not used anywhere in the shipped path: vessels and buoys
    /// come off the Core seam (<see cref="IRadarContacts"/>) and the coast off the terrain
    /// (<see cref="RadarLandEcho"/>). An empty scope over open water is the truth, and it is what this
    /// draws.</para>
    ///
    /// <para><b>A housing in the band; raised is a choice</b> (H2, the sounder/finder/plotter
    /// precedent). The radar's place on the helm is its named housing in the pilot bands, and pressing
    /// it RAISES a centred card (<see cref="HelmBandLayout.RaisedCardRect"/>), where alone the pushers
    /// are live. Pressing away, pressing the housing again, or Esc lowers it. Unraised, this host
    /// draws nothing and turns no aerial. The band gives the compass and the radar a piece each, so
    /// the dash's old displacement (a dome binnacle taking the radar's brow station,
    /// <c>noviRig.js:453</c>) no longer leaves a radar without a housing.</para>
    ///
    /// <para><b>Perf (rule 7) — the one thing a radar makes harder than a chart.</b> A turning sweep
    /// changes the picture every frame, and so, more quietly, does a drifting boat re-measuring every
    /// contact — so pure change-detection (the plotter's answer) would repaint per frame. The rate is
    /// capped instead, at <see cref="RadarSettings.SweepRepaintHz"/>, which is the answer ADR 0025
    /// already recorded for the fish finder's free-running scan
    /// (<c>FishFinderSettings.WaterfallHz</c>): the repaint RATE is data, not the frame rate.</para>
    ///
    /// <para><b>⚠ The budget governs the WORLD, never a press.</b> A cap on everything would make a
    /// pusher take up to an eighth of a second to answer, which reads as a dropped input. So the change
    /// key is split: a CONTROL term (range, mode, night, cursor, which boat) repaints immediately, and
    /// the WORLD's own motion (sweep, heading, contacts, sea state) waits its turn. STANDBY is not a
    /// special case — it simply removes the sweep as a source of change, so a set standing by over
    /// quiet water settles to no repaints at all. The land scan is separately bounded and does not run
    /// per repaint (<see cref="RadarLandEcho.ScanCount"/>).</para>
    ///
    /// <para><b>What one repaint costs, and the lever that is deliberately NOT pulled.</b> ADR 0025's
    /// measured figure for this exact canvas (480×660) is <b>4.79 ms</b> — and the split is
    /// <b>4.507 ms raster against 0.282 ms upload</b>, so the FILL dominates sixteen to one. The shipped
    /// 8 Hz is therefore about 38 ms per second, ~3.8% of a 60 fps budget, in the same order as the
    /// finder's 1.9%. Because the fill dominates, the ADR's own named next step really would help:
    /// caching the static chrome (case, bezel, pushers, brand — they change only with night and mode)
    /// is roughly a 2× win and is what to reach for if the owner wants 12 Hz. It is not done here for
    /// the reason the ADR gives: it forks further from the rig source, and this slice has no measurement
    /// saying 8 Hz is too slow to look at.</para>
    ///
    /// <para><b>Self-installing</b> (the S1 pattern): one persistent host per play session, so every
    /// already-built scene grows the instrument with no builder re-run. Headless-safe and inert without
    /// a registered instrument service.</para>
    ///
    /// <para><b>Known limit, inherited and unchanged:</b> overlay clicks are not yet exclusive with
    /// other mouse gameplay, and the steer-session arbitration is the existing hosts'.</para>
    /// </summary>
    [DefaultExecutionOrder(-40)]
    public sealed class RadarOverlayHost : MonoBehaviour
    {
        [Header("Canvas")]
        [Tooltip("Sorting order of the radar canvas. Above the helm band's own canvas (60), the " +
                 "sounder/finder (62) and the chartplotter (63), so a raised card never z-fights " +
                 "another instrument's.")]
        [SerializeField] private int _sortingOrder = 64;

        private static RadarOverlayHost _instance;

        private GameObject _cardGo;
        private RectTransform _cardRect;
        private RawImage _image;
        private RectTransform _imageRect;

        private DrawSurface _surface;
        private Texture2D _texture;
        private readonly RadarLandEcho _land = new RadarLandEcho();

        // Caller-owned read buffers — the seam fills these, so a repaint allocates nothing (rule 7).
        private readonly List<RadarContact> _contacts = new();
        private readonly List<RadarBlip> _blips = new();

        // The aerial's angle. Presentation state, never saved, and re-seeded from zero on a fresh host.
        private float _sweepDeg;
        private float _nextRepaintAt;

        // The EBL/VRM cursor — transient by contract (a cursor is where your hand is, not a preference).
        private bool _cursorOn;
        private float _cursorBearing, _cursorRangeNM;

        // Change-detection state — the quantized picture the current texture actually shows.
        private bool _painted;
        private int _shownSweepStep, _shownHeading, _shownRings, _shownContactHash;
        private float _shownRange, _shownGain, _shownSea;
        private bool _shownNight, _shownHeadUp, _shownTx, _shownHasBoat, _shownCursor;
        private int _shownCursorBrg, _shownCursorRange;
        private string _shownRegion;

        /// <summary>Which slot this host owns in the shared expansion arbiter.</summary>
        private const HelmInstrumentSlot Slot = HelmInstrumentSlot.Radar;

        // True from the second frame of a raise: the press that raised the card was the band's, so
        // the frame it rises must not read that same press as one on the card.
        private bool _wasExpanded;

        /// <summary>The one host per play session (it self-installs and survives scene loads). Exposed so
        /// a PlayMode test can drive the host that is actually running rather than a second copy — a
        /// duplicate destroys itself in <c>Awake</c>.</summary>
        public static RadarOverlayHost Instance => _instance;

        /// <summary>Raised to its own card rather than only a housing in the band. Shared state, so
        /// only one instrument is ever raised. Exposed for tests.</summary>
        public bool Expanded => HelmInstrumentExpansion.IsExpanded(Slot);

        /// <summary>True while the instrument is presented — this hull's helm actually carries a radar
        /// (its housing in the band, or its card raised). Exposed for tests.</summary>
        public bool Showing { get; private set; }

        /// <summary>How many rasters this host has done — the repaint-cost guard's evidence.</summary>
        public int RepaintCount { get; private set; }

        /// <summary>How many times the coastline has been scanned. Must stay far below
        /// <see cref="RepaintCount"/> while sailing, and must not move for a repaint that only turned
        /// the sweep (rule 7 — <see cref="RadarLandEcho"/>).</summary>
        public int LandScanCount => _land.ScanCount;

        // ---- the DIFFERENTIAL probes (the #421 lesson) --------------------------------------------
        // Every one of these reads what the LAST RASTER actually used, not what the rule would compute.
        // A plumbed flag that compiles, passes and quietly changes nothing is how the helm's night panel
        // shipped dead TWICE in this arc; the only assertion that can catch it is taken from the real
        // production path, with a negative control proving the probe can report the other answer too.

        /// <summary>Whether the texture ON SCREEN RIGHT NOW was painted with the night phosphor.</summary>
        public bool NightShown => _painted && _shownNight;

        /// <summary>The orientation the current texture was painted with (true = head-up).</summary>
        public bool HeadUpShown => _painted && _shownHeadUp;

        /// <summary>Whether the current texture was painted TRANSMITTING. The flag this whole slice
        /// turns on: a standby-vs-TX that never reaches the raster cannot show up here as true.</summary>
        public bool TransmittingShown => _painted && _shownTx;

        /// <summary>The range in NM the current texture was painted at, or 0 before the first raster.</summary>
        public float RangeShown => _painted ? _shownRange : 0f;

        /// <summary>The sea-clutter strength the current texture was painted with — the weather's own
        /// differential probe, since nothing else on the glass proves the sea state reached it.</summary>
        public float SeaClutterShown => _painted ? _shownSea : 0f;

        /// <summary>How many echoes the current texture was painted with. Reads the list the raster
        /// consumed, so "the contacts arrived but were never drawn" cannot pass.</summary>
        public int BlipsShown { get; private set; }

        /// <summary>The blips as last painted — exposed so a test can assert WHERE a known contact
        /// landed on the scope rather than only that one existed.</summary>
        public IReadOnlyList<RadarBlip> Blips => _blips;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (_instance != null) return;
            var go = new GameObject("RadarOverlayHost");
            _instance = go.AddComponent<RadarOverlayHost>();
            DontDestroyOnLoad(go);
        }

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;

            var canvasGo = new GameObject("RadarOverlayCanvas");
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = _sortingOrder;
            canvasGo.AddComponent<CanvasScaler>();   // constant pixel size — the rigs are pixel art

            _cardGo = new GameObject("RadarCard");
            _cardGo.transform.SetParent(canvasGo.transform, false);
            _cardRect = _cardGo.AddComponent<RectTransform>();
            _cardRect.anchorMin = _cardRect.anchorMax = new Vector2(0f, 0f);
            _cardRect.pivot = new Vector2(0f, 0f);

            var imageGo = new GameObject("Radar");
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
            bool fitted = instruments != null && instruments.Fit.Radar;
            if (!fitted)
            {
                if (Expanded) HelmInstrumentExpansion.Collapse();
                ForgetCursor();
                _painted = false;
                _land.Forget();
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

            // Unraised, the housing in the band is the whole presentation: no sweep, no contacts.
            if (!Expanded)
            {
                _wasExpanded = false;
                if (_cardGo.activeSelf) _cardGo.SetActive(false);
                return;
            }
            if (!_cardGo.activeSelf) _cardGo.SetActive(true);

            RadarSettings cfg = GameServices.Radar;
            SaveData save = GameServices.Save?.Current;
            string regionId = GameServices.CurrentRegionId;
            RadarPrefs prefs = Prefs(save, instruments.HullId, in cfg);

            // ONE backlight rule, the standing one (#421): a lit panel lights what is mounted in it, and
            // the strip tap stays the EARLY-ON override. Never re-derived here.
            bool night = prefs.Night || HelmPanelLight.IsNight();

            AdvanceSweep(prefs.Transmitting, in cfg);

            RadarRigState state = BuildState(instruments, in prefs, in cfg, night, out Vector2 boat,
                                             out bool hasBoat);
            ReadContacts(boat, hasBoat, in state, in cfg);

            Rect card = HelmBandLayout.RaisedCardRect(RadarRigRender.W, RadarRigRender.H,
                                                      HudScreen.Width, HudScreen.Height,
                                                      HudBandLayout.ReservedTopPx());
            LayoutCard(card);
            Repaint(in state, regionId, hasBoat, in cfg);
            HelmInstrumentExpansion.PublishRaisedCard(Slot, card);

            bool raiseFrame = !_wasExpanded;
            _wasExpanded = true;
            if (raiseFrame) return;
            ReadPointer(instruments, save, in prefs, in cfg, in state, card);
        }

        private void StandDown()
        {
            Showing = false;
            _wasExpanded = false;
            if (_cardGo.activeSelf) _cardGo.SetActive(false);
        }

        /// <summary>
        /// Turn the aerial. Presentation, not simulation — so it runs on the FRAME clock rather than on
        /// <c>IGameClock</c>.
        ///
        /// <para><b>Why not game seconds.</b> The world clock is compressed (a day is
        /// <c>GameConfig.SecondsPerDay</c> of real time), so an aerial driven by it would spin dozens of
        /// times faster than any radar ever built. A scanner is a motor in the room with the player, and
        /// it turns at its own rate.</para>
        ///
        /// <para><b>Why scaled <c>deltaTime</c> and not unscaled.</b> A paused game freezes the aerial
        /// AND the repaints that come with it, which is both the right picture behind a menu and the
        /// right rule-7 behaviour. At normal play <c>timeScale</c> is 1, so this is real seconds.</para>
        ///
        /// <para>A set in STANDBY does not turn at all: the picture on the glass is the last one it
        /// painted, which is the truth about a set that is not radiating.</para>
        /// </summary>
        private void AdvanceSweep(bool transmitting, in RadarSettings cfg)
        {
            if (!transmitting) return;
            _sweepDeg = NavMath.Norm360(_sweepDeg + cfg.SweepDegreesPerSecond * Time.deltaTime);
        }

        /// <summary>This hull's stored radar preferences, or the owner's defaults for a hull that has
        /// never been touched.
        ///
        /// <para>Read through <see cref="InstrumentLocker"/> rather than through
        /// <see cref="IHelmInstruments"/> — the CHARTPLOTTER's choice, deliberately matched. That seam
        /// carries <c>SounderPrefs</c> only, and its standing
        /// <c>FLAG lead-architect</c> asks whether it should carry the plotter's too. Adding the radar's
        /// while the plotter's stayed off would leave the seam carrying one instrument's preferences out
        /// of three, which is worse than either consistent answer. FLAG lead-architect: the fork is now
        /// TWO instruments wide; if the seam should carry these, it is two members per instrument and
        /// one relay implementation, and these two methods are the only callers. See the PR body.</para>
        /// </summary>
        private static RadarPrefs Prefs(SaveData save, string hullId, in RadarSettings cfg)
            => InstrumentLocker.RadarPrefsFor(save, hullId, RadarPrefs.FromDefaults(in cfg));

        /// <summary>Write this hull's radar preferences back (a pusher press). Persists through the save
        /// service when one is wired; a no-op with no hull. Returns whether anything changed.</summary>
        private static bool SetPrefs(SaveData save, string hullId, in RadarPrefs prefs)
            => InstrumentLocker.SetRadarPrefs(save, hullId, in prefs);

        /// <summary>
        /// Everything the glass draws this frame, gathered from the seams that already publish it.
        ///
        /// <para><b>SEA clutter is the real sea state, not a constant.</b>
        /// <see cref="EnvironmentSample.SeaState01"/> is the deterministic sea the whole game already
        /// runs on, so the scope speckles up in a blow and cleans off in a calm — the instrument
        /// answering to the weather (P1) rather than to a dial nobody can turn.</para>
        ///
        /// <para><b>RAIN clutter is zero, and that is a decision.</b> There is no precipitation model to
        /// read, and the one atmospheric field that does exist —
        /// <see cref="EnvironmentSample.Visibility"/> — is FOG. Feeding fog into the rain dial would
        /// invert the instrument's entire meaning, because seeing through fog is exactly what a radar is
        /// for; the Smother's payoff (canon M4) is the slice that gives this a real source.</para>
        ///
        /// <para><b>The picture is turned by the BOW bearing</b>, the plotter's own ruling: a
        /// GPS-derived course is undefined at a standstill and a head-up picture fed an undefined course
        /// spins on the spot at every wharf.</para>
        /// </summary>
        private RadarRigState BuildState(IHelmInstruments instruments, in RadarPrefs prefs,
                                         in RadarSettings cfg, bool night,
                                         out Vector2 boat, out bool hasBoat)
        {
            hasBoat = instruments.TryReadPosition(out boat) && NavMath.IsFinite(boat);

            float heading = 0f;
            IActiveBoatService probe = GameServices.ActiveBoat;
            if (probe != null)
            {
                BoatKinematics k = probe.Sample();
                if (k.HasBoat) heading = k.HeadingDegrees;
            }

            float sea = 0f;
            IEnvironmentService env = GameServices.Environment;
            if (env != null)
                sea = Mathf.Clamp01(env.Sample().SeaState01) * Mathf.Clamp01(cfg.SeaClutterAtFullSeaState);

            var st = new RadarRigState(prefs.RangeNM(in cfg), cfg.SafeRings, prefs.HeadUp, heading,
                                       Mathf.Clamp01(cfg.Gain), sea, rain: 0f,
                                       transmitting: prefs.Transmitting, trails: true, night: night,
                                       sweepDeg: _sweepDeg, blink: false);
            return _cursorOn ? st.WithCursor(true, _cursorBearing, _cursorRangeNM) : st;
        }

        /// <summary>
        /// Refill the blip list: the Core seam's contacts plus the coastline scan, each measured from own
        /// ship through the ONE bearing convention.
        ///
        /// <para><b>This is the only place a world position becomes a bearing.</b> Both sources publish
        /// world metres, and <see cref="NavMath.BearingDegrees"/> is the single conversion — which is
        /// what keeps a second, silently-flipped bearing out of the codebase
        /// (<see cref="RadarRigGeometry"/>'s founding note).</para>
        ///
        /// <para>With no boat there is nothing to measure FROM, so the scope draws empty rather than
        /// measuring from the origin.</para>
        /// </summary>
        private void ReadContacts(Vector2 boat, bool hasBoat, in RadarRigState st, in RadarSettings cfg)
        {
            _blips.Clear();
            if (!hasBoat) { _contacts.Clear(); _land.Forget(); return; }

            float rangeMetres = NavMath.NMToMetres(st.RangeNM);
            double gameSeconds = GameServices.Clock != null ? GameServices.Clock.TotalSeconds : 0.0;

            // The coast — a bounded scan, not re-run per repaint.
            IEnvironmentService env = GameServices.Environment;
            float water = env != null ? env.WaterLevelAt(gameSeconds) : 0f;
            _land.EnsureScanned(boat, rangeMetres, water, GameServices.TidalTerrain, in cfg);
            IReadOnlyList<RadarContact> coast = _land.Echoes;
            for (int i = 0; i < coast.Count; i++) AddBlip(boat, coast[i], st.RangeNM);

            // Vessels and buoys. Never null — an unregistered producer is the honest quiet sea.
            GameServices.RadarContacts.ContactsAt(boat, rangeMetres, gameSeconds, _contacts);
            for (int i = 0; i < _contacts.Count; i++) AddBlip(boat, _contacts[i], st.RangeNM);
        }

        private void AddBlip(Vector2 boat, in RadarContact c, float rangeNM)
        {
            float bearing = NavMath.BearingDegrees(boat, c.WorldPos);
            float rangeNMOfContact = NavMath.MetresToNM(NavMath.DistanceMetres(boat, c.WorldPos));
            float trail = c.IsMakingWay
                ? Mathf.Clamp(c.SpeedMps / RadarRigRender.TrailReferenceSpeedMps, 0f, 2f)
                : 0f;
            _blips.Add(new RadarBlip(bearing, rangeNMOfContact, c.Size, c.Kind,
                                     c.IsMakingWay ? c.CourseDegrees : float.NaN, trail));
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

        // ---- painting -----------------------------------------------------------------------------

        /// <summary>
        /// Repaint when the picture would differ AND the rate budget allows it.
        ///
        /// <para><b>Two gates, because a radar has two kinds of change.</b> Everything that is not the
        /// sweep — the range, the mode, the night palette, the contacts — goes through the plotter's own
        /// change-detection, quantized to what the glass can actually show. The SWEEP is different: it
        /// moves every frame by construction, so it is gated on a wall-clock budget instead
        /// (<see cref="RadarSettings.SweepRepaintHz"/>). A set in STANDBY has no moving sweep, so the
        /// budget never fires and the instrument costs exactly what the chartplotter costs.</para>
        ///
        /// <para>Deliberately NOT <c>state.Equals(_shown)</c>: <see cref="RadarRigState"/> has no
        /// <c>IEquatable</c>, so that call would box and reflect every frame (rule 7), and explicit
        /// fields make the quantization visible — which is the point of the guard.</para>
        /// </summary>
        private void Repaint(in RadarRigState st, string regionId, bool hasBoat, in RadarSettings cfg)
        {
            // The sweep quantized to the repaint budget's own step, so the comparison below is about the
            // picture rather than about a float that never repeats.
            float hz = cfg.SafeSweepRepaintHz;
            int sweepStep = st.Transmitting
                ? Mathf.FloorToInt(st.SweepDeg / Mathf.Max(1f, cfg.SweepDegreesPerSecond / hz))
                : -1;
            int heading = Mathf.RoundToInt(NavMath.Norm360(st.HeadingDeg));
            int contactHash = ContactHash();
            int cursorBrg = _cursorOn ? Mathf.RoundToInt(NavMath.Norm360(_cursorBearing)) : int.MinValue;
            int cursorRange = _cursorOn ? Mathf.RoundToInt(_cursorRangeNM * 1000f) : int.MinValue;

            // A CONTROL the player just worked — a pusher, the cursor, the panel light, boarding a
            // different boat. Rare, deliberate, and it must answer at once.
            bool controlSame = st.RangeNM == _shownRange && st.Night == _shownNight
                && st.HeadUp == _shownHeadUp && st.Transmitting == _shownTx
                && st.Rings == _shownRings && Mathf.Approximately(st.Gain, _shownGain)
                && hasBoat == _shownHasBoat && regionId == _shownRegion
                && _cursorOn == _shownCursor && cursorBrg == _shownCursorBrg
                && cursorRange == _shownCursorRange;

            // The WORLD moving under the boat — the sweep turning, the fleet steaming, the sea getting
            // up. Continuous by nature, so this is what the rate budget exists for.
            bool worldSame = sweepStep == _shownSweepStep && heading == _shownHeading
                && contactHash == _shownContactHash && Mathf.Approximately(st.Sea, _shownSea);

            if (_painted && controlSame && worldSame) return;

            // ⚠ The budget applies to the WORLD's motion, never to a press. A cap on everything would
            // make a pusher take up to 1/Hz of a second to answer, which reads as a dropped input; a cap
            // on nothing would repaint every frame, because a drifting boat re-measures every contact.
            // So: a control change repaints NOW, and the world waits its turn.
            if (_painted && controlSame && Time.unscaledTime < _nextRepaintAt) return;
            _nextRepaintAt = Time.unscaledTime + 1f / Mathf.Max(1f, hz);

            // Always the rig's NATIVE size — the card is a scaled blit of it, never a smaller drawing
            // (the mount layout's letterbox contract).
            _surface ??= new DrawSurface(RadarRigRender.W, RadarRigRender.H);
            RadarRigRender.RenderStandalone(_surface, in st, _blips);
            _surface.ToTexture(ref _texture);
            _image.texture = _texture;
            RepaintCount++;

            _painted = true;
            _shownSweepStep = sweepStep; _shownHeading = heading; _shownRings = st.Rings;
            _shownContactHash = contactHash; _shownRange = st.RangeNM; _shownGain = st.Gain;
            _shownSea = st.Sea; _shownNight = st.Night; _shownHeadUp = st.HeadUp;
            _shownTx = st.Transmitting; _shownHasBoat = hasBoat; _shownRegion = regionId;
            _shownCursor = _cursorOn; _shownCursorBrg = cursorBrg; _shownCursorRange = cursorRange;
            BlipsShown = _blips.Count;
        }

        /// <summary>
        /// An O(1)-ish fingerprint of what is on the scope. The COUNT alone would miss a fleet that
        /// merely moved, so each blip folds in its bearing to the degree and its range to a thousandth of
        /// a mile — the granularity the glass can actually resolve, so a boat drifting sub-pixel does not
        /// force a raster.
        /// </summary>
        private int ContactHash()
        {
            int h = 17;
            for (int i = 0; i < _blips.Count; i++)
            {
                RadarBlip b = _blips[i];
                h = h * 31 ^ Mathf.RoundToInt(NavMath.Norm360(b.BearingTrue));
                h = h * 31 ^ Mathf.RoundToInt(b.RangeNM * 1000f);
                h = h * 31 ^ (int)b.Kind;
            }
            return h;
        }

        // ---- pointer + keys ------------------------------------------------------------------------

        /// <summary>
        /// The raised card's pointer, the sounder's and the plotter's exactly (H2): the three pushers
        /// and the scope work, and a press inside that lands on no control does nothing rather than
        /// lowering, so a near-miss on a pusher is never punished. Esc lowers from anywhere. A press
        /// OUTSIDE the card is the band's to route while the band is up; with no band it is this card's
        /// click-away.
        ///
        /// <para><b>A click on the scope drops the EBL/VRM cursor</b> — the marine "where is that, and
        /// how far?" read, and the direct analogue of the chartplotter's measure line (S6 PR 4). The rig
        /// authors both markers and its own <c>pickAt</c> inverse for exactly this; a click at the centre
        /// puts the cursor away, the same "click to stow the tool" gesture the measure line uses.</para>
        /// </summary>
        private void ReadPointer(IHelmInstruments instruments, SaveData save, in RadarPrefs prefs,
                                 in RadarSettings cfg, in RadarRigState st, Rect card)
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

            // A press on ANY window's chrome is that window's business — never this instrument's
            // controls or click-away (2026-08-07 ruling).
            if (BoatUiWindows.OverAnyChrome(pos)) return;

            if (!card.Contains(pos))
            {
                if (!HelmOverlayHost.BandUp)
                    HelmInstrumentExpansion.Click(HelmInstrumentSlot.None);   // click-away → lower
                return;
            }

            HelmOverlayLayout.ScreenToRig(pos, card, RadarRigRender.W, RadarRigRender.H,
                                          out Vector2 rigPx);
            RadarRigLayout L = RadarRigGeometry.StandaloneLayout();

            // The three pushers.
            if (L.Button0.Contains(rigPx.x, rigPx.y))
            {
                Apply(instruments, save, prefs.SteppedMode());
                return;
            }
            if (L.Button1.Contains(rigPx.x, rigPx.y))
            {
                Apply(instruments, save, prefs.SteppedRange(-1, in cfg));   // ▲ = OUT, a wider range
                return;
            }
            if (L.Button2.Contains(rigPx.x, rigPx.y))
            {
                Apply(instruments, save, prefs.SteppedRange(+1, in cfg));   // ▼ = IN, a closer range
                return;
            }

            // The scope: drop (or stow) the EBL/VRM cursor.
            if (!L.ScopeContains(rigPx.x, rigPx.y)) return;
            RadarRigGeometry.PickAt(L.ScopeCx, L.ScopeCy, L.ScopeR, rigPx.x, rigPx.y,
                                    st.HeadUp, st.HeadingDeg,
                                    out float bearing, out float frac, out bool inside);
            if (!inside) return;
            if (frac < CursorStowFraction) { ForgetCursor(); return; }
            _cursorOn = true;
            _cursorBearing = bearing;
            _cursorRangeNM = frac * st.RangeNM;
        }

        /// <summary>How close to own ship a click has to land to STOW the cursor rather than move it.
        /// A twentieth of the range is the own-ship glyph and a comfortable margin around it — small
        /// enough that a real measurement close aboard is still possible.</summary>
        private const float CursorStowFraction = 0.05f;

        /// <summary>
        /// Write a stepped preference through and FLUSH it — the plotter's own two lines
        /// (<c>ChartplotterOverlayHost:1157-1158</c>).
        ///
        /// <para>The flush is the load-bearing half. <see cref="InstrumentLocker"/> writes into the
        /// in-memory <see cref="SaveData"/>; without <c>ISaveService.Save()</c> a setting would look
        /// perfectly persistent all session and be gone on the next load. The write is skipped — and so
        /// is the flush — when nothing actually moved, so a pusher press at the end of the range ladder
        /// never dirties the save.</para>
        /// </summary>
        public bool Apply(IHelmInstruments instruments, SaveData save, RadarPrefs next)
        {
            if (instruments == null) return false;
            if (!SetPrefs(save, instruments.HullId, in next)) return false;
            GameServices.Save?.Save();
            return true;
        }

        /// <summary>Put the cursor away — on losing the glass, and on a click at own ship.</summary>
        private void ForgetCursor()
        {
            _cursorOn = false;
            _cursorBearing = 0f;
            _cursorRangeNM = 0f;
        }

        /// <summary>Drive one frame's worth of state from a test without a canvas or an input device.
        /// Exposed so the PlayMode differential probes can pin a specific picture rather than waiting for
        /// the world to produce one.</summary>
        public void StepCursorForTest(bool on, float bearing, float rangeNM)
        {
            _cursorOn = on;
            _cursorBearing = bearing;
            _cursorRangeNM = rangeNM;
        }
    }
}
