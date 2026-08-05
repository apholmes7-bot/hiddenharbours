using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using HiddenHarbours.Core;

namespace HiddenHarbours.UI
{
    /// <summary>
    /// The screen-space FISH FINDER card (ADR 0025 S3b) — the colour-sonar upgrade that supersedes the
    /// depth sounder in the same brow cutout. While the player pilots a hull whose effective fit resolves
    /// to <see cref="SounderKind.Fish"/>, this card paints the live water column, with the three side
    /// pushers and the three glass regions live at the big size.
    ///
    /// <para><b>S4.5 — on a consoled hull this card is the EXPANDED state, not the default.</b> The
    /// owner's brow-squash ruling: the glance read is the FLUSH face the dash compositor paints into the
    /// brow mount (<see cref="HelmDashController"/> — the rig's own <c>paintInto</c> idiom at the mount
    /// box); this card appears only when the player selects the instrument on the focused dash
    /// (<see cref="HelmInstrumentExpansion"/>), and then straight at its big, controls-live size. On a
    /// helm with no console the pre-S4.5 small/focus card behaviour still stands.</para>
    ///
    /// <para><b>Its own host, not a branch inside <see cref="SounderOverlayHost"/>.</b> That file's own
    /// reasoning applies again and harder: each instrument's lifetime, hit geometry and repaint rule is its
    /// own, and these two differ in every one of the three — the sounder is landscape and repaints on
    /// change-detected strings (still water ⇒ roughly never), the finder is portrait and repaints on a
    /// quantized scan cadence. A shared host would carry both rules behind an <c>if</c> and would have to
    /// pick one card rect for two aspect ratios. The gate is already mutually exclusive:
    /// <c>BoatEquipment.EffectiveFit</c> resolves the cutout to Depth OR Fish, never both, and
    /// <see cref="SounderOverlayHost"/> already stands down unless the fit says Depth.</para>
    ///
    /// <para><b>The fish are the Core seam's, not ours.</b> Marks come from
    /// <see cref="IFishSchools.MarksAt"/> — the same schools the fishing path fishes, which is the owner's
    /// honesty invariant (what the glass shows IS what changes the bite). This host never creates, mutates
    /// or caches a school; <see cref="GameServices.FishSchools"/> is never null and is the EMPTY SEA until
    /// a model registers, so the finder draws an honest empty sonar with no fish model in the project at
    /// all. Depths arrive in METRES and are divided by the player's RANGE at paint time — a normalised
    /// depth would silently mean something else the moment the RANGE pushers moved.</para>
    ///
    /// <para><b>Perf (rule 7) — the whole story is the cadence.</b> The rig's scan phase free-runs and
    /// <c>sonarView</c> is O(sonar width) in spans plus a full texture upload, so a naive port repaints
    /// every frame. Instead the phase is quantized to <see cref="FishFinderSettings.WaterfallHz"/>, the
    /// bucket is part of the change key, and the fish query only runs when the bucket flips. Everything
    /// else in the key is player input or the throttled sounding, so the steady-state raster rate is
    /// <c>WaterfallHz</c> (4/s at the shipped tuning) plus at most the sounding tick — never the frame
    /// rate. One repaint is a MEASURED ~4.8 ms at 480×660, which is exactly why that number is data and
    /// not a frame counter. The alarm's blink rides the SAME key (<c>WaterfallHz</c> is a multiple of
    /// 2×<c>AlarmBlinkHz</c>, so its flips land on bucket boundaries), so a sounding alarm and a scrolling
    /// scan do not each buy their own repaint. ONE reused
    /// <see cref="DrawSurface"/> + <see cref="Texture2D"/> at the rig's native size for every state — the
    /// card is a scaled blit of it, not a second drawing — so no per-frame allocation, no reallocation on
    /// focus, and no string formatting on the change-detection path at all.</para>
    ///
    /// <para><b>Self-installing</b> (the S1/S2 pattern): a <see cref="RuntimeInitializeOnLoadMethod"/>
    /// spawns one persistent host per play session, so every already-built scene grows the instrument with
    /// no builder re-run. Headless-safe and inert without a registered instrument service.</para>
    /// </summary>
    [DefaultExecutionOrder(-40)]
    public sealed class FishFinderOverlayHost : MonoBehaviour
    {
        [Header("Canvas")]
        [Tooltip("Sorting order of the finder canvas. The same brow slot as the depth sounder — they are " +
                 "never on screen together.")]
        [SerializeField] private int _sortingOrder = 60;

        private static FishFinderOverlayHost _instance;

        private GameObject _cardGo;
        private RectTransform _cardRect;
        private RawImage _image;
        private RectTransform _imageRect;
        private Canvas _canvas;

        private DrawSurface _surface;
        private Texture2D _texture;

        // S4.5: true while this card is up as the dash's EXPANDED instrument (the standalone card is
        // no longer a default presentation on a consoled hull — the flush face on the dash is).
        private bool _expandedMode;

        // Change detection — the whole painted frame as one comparable value plus the marks' revision.
        private bool _painted;
        private FishRigState _shownState;
        private int _shownMarksRev;

        // Transient UI state (Ruling A) — deliberately NOT persisted. Which mode the ± pushers are in is
        // where the player's thumb is, not a preference; fish-ID is a look-at-it-now toggle.
        private FishRigAdjust _adjust = FishRigAdjust.Range;
        private bool _fishId;
        private bool _fishIdInit;
        private bool _focused;

        // The read path's reusable buffers (rule 7 — the seam fills a caller-owned list).
        private readonly List<FishMark> _seamMarks = new List<FishMark>(16);
        private readonly List<SonarMark> _drawMarks = new List<SonarMark>(48);
        private long _scanBucket = long.MinValue;
        private int _marksRev;

        /// <summary>The one host per play session (it self-installs and survives scene loads). Exposed so
        /// a PlayMode test can drive the host that is actually running rather than a second copy — a
        /// duplicate destroys itself in <c>Awake</c>.</summary>
        public static FishFinderOverlayHost Instance => _instance;

        /// <summary>Focused state (click-to-focus). Exposed for tests.</summary>
        public bool Focused => _focused;

        /// <summary>What the ▲/▼ pushers are currently adjusting (Ruling A). Exposed for tests.</summary>
        public FishRigAdjust Adjust => _adjust;

        /// <summary>Are the per-mark depth tags drawn? Exposed for tests.</summary>
        public bool FishId => _fishId;

        /// <summary>True while the card is on screen — i.e. this hull's helm actually carries the fish
        /// finder AND there is a sounding to scale the picture against. Exposed for tests.</summary>
        public bool Showing => _cardGo != null && _cardGo.activeSelf;

        /// <summary>How many rasters this host has done. The cadence guard's evidence — exposed so a test
        /// can prove the scan repaints at <c>WaterfallHz</c> and not per frame.</summary>
        public int RepaintCount { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (_instance != null) return;
            var go = new GameObject("FishFinderOverlayHost");
            _instance = go.AddComponent<FishFinderOverlayHost>();
            DontDestroyOnLoad(go);
        }

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;

            var canvasGo = new GameObject("FishFinderOverlayCanvas");
            canvasGo.transform.SetParent(transform, false);
            _canvas = canvasGo.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = _sortingOrder;
            canvasGo.AddComponent<CanvasScaler>();   // constant pixel size — the rigs are pixel art

            _cardGo = new GameObject("FishFinderCard");
            _cardGo.transform.SetParent(canvasGo.transform, false);
            _cardRect = _cardGo.AddComponent<RectTransform>();
            _cardRect.anchorMin = _cardRect.anchorMax = new Vector2(0f, 0f);
            _cardRect.pivot = new Vector2(0f, 0f);

            var imageGo = new GameObject("FishFinder");
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
            // The FINDER owns the brow only while the fit says Fish. A Depth fit is the plain sounder's
            // cutout — this card stands down rather than drawing the wrong instrument.
            bool fitted = instruments != null && instruments.Fit.Sounder == SounderKind.Fish;
            if (!fitted || !instruments.TryReadDepth(out float depth))
            {
                _focused = false;
                _painted = false;
                // Drop the marks and the bucket too: coming back (a hull swap, a re-entered region) must
                // re-ask the seam rather than paint the last boat's fish for a scan step.
                _scanBucket = long.MinValue;
                _drawMarks.Clear();
                if (_cardGo.activeSelf) _cardGo.SetActive(false);
                return;
            }

            // S4.5 — the owner's brow-squash ruling: while the helm's instruments are DASH-MOUNTED,
            // the glance read is the FLUSH face on the dash and this standalone card is the EXPANDED
            // state only. Expansion transitions belong to the dash host (HelmOverlayHost); this host
            // only reads the state. A helm with no console keeps the S3b behaviour below, untouched.
            bool dashMounted = HelmInstrumentExpansion.DashCarriesBrow(GameServices.HelmControl);
            _expandedMode = dashMounted;
            if (dashMounted && HelmInstrumentExpansion.Current != DashInstrument.Sounder)
            {
                _focused = false;
                _painted = false;
                _scanBucket = long.MinValue;
                _drawMarks.Clear();
                if (_cardGo.activeSelf) _cardGo.SetActive(false);
                return;
            }
            if (!_cardGo.activeSelf) _cardGo.SetActive(true);
            // The expanded card reads OVER the dash card (120); the small legacy card keeps its
            // authored order beside the helm.
            _canvas.sortingOrder = _expandedMode ? _sortingOrder + 70 : _sortingOrder;

            FishFinderSettings finder = GameServices.FishFinder;
            DepthSounderSettings sounder = GameServices.DepthSounder;
            SounderPrefs prefs = instruments.SounderPrefs;
            if (!_fishIdInit) { _fishId = finder.DefaultFishId; _fishIdInit = true; }

            // ONE alarm rule: the sounder's, unchanged (Ruling E). Never re-derived here.
            bool triggered = DepthSounder.ShallowAlarm(depth, in prefs);
            bool blink = triggered && DepthSounder.BlinkPhase(Time.time, sounder.AlarmBlinkHz);

            // The scan's cadence — and, on its flip, the ONE place the fish are asked for.
            long bucket = FishFinderOverlayLayout.PhaseBucket(Time.time, finder.WaterfallHz);
            if (bucket != _scanBucket)
            {
                _scanBucket = bucket;
                RefreshMarks(instruments, in finder);
            }

            float range = FishRigGeometry.SafeRange(prefs.RangeMetres, finder.DefaultRangeMetres);
            var state = new FishRigState(
                depth, sounder.PlaceholderWaterTempC, range, prefs.AlarmMetres,
                prefs.Feet, prefs.Night, _fishId, prefs.Armed, triggered, blink,
                FishFinderOverlayLayout.PhaseSeconds(bucket, finder.WaterfallHz), _adjust,
                finder.PlaceholderSens01, finder.PlaceholderLink, finder.PlaceholderBatt01,
                finder.PlaceholderVolts);

            bool big = _focused || _expandedMode;   // expansion IS the focused presentation
            Rect card = FishFinderOverlayLayout.CardRect(big, in finder,
                                                         Screen.width, Screen.height);
            LayoutCard(card);
            Repaint(in state);
            ReadPointer(instruments, in prefs, in sounder, in finder, card);
        }

        // ---- the fish read (a PURE read of the Core seam) -------------------------------------------

        /// <summary>Re-query the schools under the transducer and turn them into icons. Runs once per scan
        /// bucket, never per frame; both lists are reused, so this allocates nothing in the steady state.
        /// With no fish model registered <see cref="GameServices.FishSchools"/> is the empty sea and this
        /// honestly produces nothing.</summary>
        private void RefreshMarks(IHelmInstruments instruments, in FishFinderSettings finder)
        {
            _seamMarks.Clear();
            IGameClock clock = GameServices.Clock;
            if (clock != null && instruments.TryReadPosition(out Vector2 worldPos))
                GameServices.FishSchools.MarksAt(worldPos, clock.TotalSeconds, _seamMarks);

            FishMarkPresenter.Build(_seamMarks, in finder, _drawMarks);
            _marksRev++;
        }

        // ---- layout -----------------------------------------------------------------------------------

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

        // ---- painting (change-detected on the scan cadence, rule 7) -----------------------------------

        private void Repaint(in FishRigState state)
        {
            // ONE comparison for the whole frame: every signal the painter reads is a field of the state,
            // the scan phase in it is already quantized, and the marks carry a revision. Nothing here
            // formats a string or allocates — a frame that changes nothing costs a struct compare. (The
            // sounder compares LCD strings; it can afford to, because its picture is text. The finder's
            // is a scan, so its key is numeric and its cadence is what bounds the cost.)
            if (_painted && _shownMarksRev == _marksRev && state.Equals(_shownState)) return;

            // Always the rig's NATIVE size — the card is a scaled blit of it, never a smaller drawing
            // (see FishFinderOverlayLayout for why a half-size render breaks the rig's own typography).
            _surface ??= new DrawSurface(FishRigRender.W, FishRigRender.H);

            FishRigRender.Render(_surface, in state, _drawMarks);
            _surface.ToTexture(ref _texture);
            _image.texture = _texture;

            _painted = true;
            _shownState = state;
            _shownMarksRev = _marksRev;
            RepaintCount++;
        }

        // ---- pointer + keys ---------------------------------------------------------------------------

        private void ReadPointer(IHelmInstruments instruments, in SounderPrefs prefs,
                                 in DepthSounderSettings sounder, in FishFinderSettings finder,
                                 Rect card)
        {
            if (_expandedMode)
            {
                // S4.5: as the dash's EXPANDED instrument, Esc and click-away belong to the dash host
                // (the one owner of expansion transitions — see HelmOverlayHost.ReadDashPointer).
                // This host answers only clicks INSIDE its card: pushers + the three glass regions.
                var m = Mouse.current;
                if (m == null || !m.leftButton.wasPressedThisFrame) return;
                Vector2 p = m.position.ReadValue();
                if (!card.Contains(p)) return;
                HelmOverlayLayout.ScreenToRig(p, card, FishRigRender.W, FishRigRender.H,
                                              out Vector2 px);
                Apply(instruments, in prefs, in sounder, in finder, FishFinderOverlayLayout.HitTest(px));
                return;
            }

            var kb = Keyboard.current;
            if (_focused && kb != null && kb.escapeKey.wasPressedThisFrame) { _focused = false; return; }

            var mouse = Mouse.current;
            if (mouse == null || !mouse.leftButton.wasPressedThisFrame) return;

            Vector2 pos = mouse.position.ReadValue();
            if (!card.Contains(pos))
            {
                if (_focused) _focused = false;   // click-away leaves the focused state
                return;
            }

            if (!_focused)
            {
                // SMALL state: the card is a button — click anywhere on it to FOCUS. The controls are
                // deliberately not live at dash size (the S1 rule).
                _focused = true;
                return;
            }

            HelmOverlayLayout.ScreenToRig(pos, card, FishRigRender.W, FishRigRender.H,
                                          out Vector2 rigPx);
            Apply(instruments, in prefs, in sounder, in finder, FishFinderOverlayLayout.HitTest(rigPx));
        }

        /// <summary>
        /// Turn a hit into an action — the owner's <b>Ruling A</b> control mapping (ADR 0025 S3,
        /// 2026-08-03), verbatim and improvising nothing:
        /// <list type="bullet">
        /// <item><c>buttons[0]</c> MODE — cycles what ▲/▼ adjust: RANGE → ALARM → RANGE.</item>
        /// <item><c>buttons[1]</c>/<c>[2]</c> ▲/▼ — step the ACTIVE mode: the rig's own
        /// <c>RANGE_STEPS</c> in RANGE, <see cref="DepthSounder.StepAlarm"/> in ALARM.</item>
        /// <item>a tap on the SONAR toggles fish-ID; on the STATUS strip, the night backlight; on the
        /// RULER, feet vs metres.</item>
        /// <item>▼ past <c>MinAlarmMetres</c> DISARMS; ▲ from disarmed re-arms at the minimum — using
        /// <see cref="DepthSounder.StepAlarm"/>'s existing clamp, so there is no new key, no new UI and no
        /// second alarm rule.</item>
        /// </list>
        /// The active ± mode and fish-ID are transient: they are not preferences, so nothing here writes
        /// them to the save. Public so a PlayMode test can drive the controls without synthesising a mouse.
        /// </summary>
        public void Apply(IHelmInstruments instruments, in SounderPrefs prefs,
                          in DepthSounderSettings sounder, in FishFinderSettings finder, int hit)
        {
            if (instruments == null || hit < 0) return;
            SounderPrefs next = prefs;
            switch (hit)
            {
                case 0: _adjust = FishFinderControls.CycleMode(_adjust); return;   // no preference moves
                case 1:
                    if (!FishFinderControls.StepActive(_adjust, ref next, +1, in sounder, in finder)) return;
                    break;
                case 2:
                    if (!FishFinderControls.StepActive(_adjust, ref next, -1, in sounder, in finder)) return;
                    break;
                case FishFinderOverlayLayout.SonarHit: _fishId = !_fishId; return;
                case FishFinderOverlayLayout.StatusHit: next.Night = !prefs.Night; break;
                case FishFinderOverlayLayout.RulerHit: next.Feet = !prefs.Feet; break;
                default: return;
            }
            instruments.SetSounderPrefs(in next);             // range/alarm/units/night DO persist
        }
    }
}
