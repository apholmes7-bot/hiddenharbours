using UnityEngine;
using UnityEngine.Rendering.Universal; // PixelPerfectCamera — per-tier reference for the data-driven zoom
using HiddenHarbours.Core;

namespace HiddenHarbours.App
{
    /// <summary>
    /// Greybox follow-cam: smoothly tracks the dory with a slight look-ahead, framed for an intimate
    /// PC-first LANDSCAPE view so the boat reads large instead of getting lost in open blue. The
    /// framing is now DATA-DRIVEN per boat (P2 scale fantasy): each hull declares how much world height
    /// the camera should show (<c>BoatHullDef.CameraWorldHeightMeters</c>), and on an upgrade the view
    /// zooms out a touch — bigger boat, more water. The camera reads this only through the Core
    /// <see cref="ActiveBoatChanged"/> signal, so it never references the Boats module.
    ///
    /// Pixel-perfect is preserved at each discrete tier (the Pixel-Perfect reference resolution is
    /// bumped to the tier — NOT a continuous lerp); the upgrade transition briefly eases the zoom for a
    /// tangible beat. The framing helpers below are the single source of truth shared by the greybox
    /// builder and the EditMode tests. A fuller rig (bounds) is later ui-ux/world work. PC-first (ADR 0005).
    ///
    /// ON-DECK ZOOM (owner playtest 2026-07-08): stepping onto the DECK steps the camera IN one
    /// pixel-perfect step past the on-foot framing, so the boat fills the screen and deck work reads in
    /// detail — and (tunably) one step closer again while a trap haul is LIVE, releasing on surface/idle.
    /// The decisions live in <see cref="CameraZoomPolicy"/> (a tested POCO with a commit hold so rapid
    /// helm⇄deck hops don't thrash); inputs arrive only via the Core <see cref="ControlModeChanged"/> /
    /// <see cref="TrapHaulStateChanged"/> signals — App never references Player/Boats/Fishing (rule 4).
    /// </summary>
    public class CameraFollow : MonoBehaviour
    {
        // ---- framing (single source of truth; PC-first landscape) ---------------------------

        /// <summary>Intimate default zoom: ~14 m of world HEIGHT visible (the Dory's default).</summary>
        public const float DefaultWorldHeightMeters = 14f;

        /// <summary>Tighter on-foot framing: ~9 m of world height so the ~1.8 m walking fisher reads
        /// large. Uses the same data-driven, pixel-perfect mapping as the boat tiers.</summary>
        public const float OnFootWorldHeightMeters = 9f;

        /// <summary>Default ON-DECK framing: exactly the next pixel-perfect step inside on-foot (×5 at
        /// the design screen = 1080 / (5 × 32) = 6.75 m), so deck work reads in detail. Default for the
        /// serialized owner-tunable <c>_deckWorldHeightMeters</c>.</summary>
        public const float DeckWorldHeightMeters = DesignScreenHeightPx / (5f * AssetsPPU);

        /// <summary>Default LIVE-HAUL framing: one pixel-perfect step tighter than the deck (×6 at the
        /// design screen = 1080 / (6 × 32) = 5.625 m) — the rope is the star while a pot comes up.
        /// Default for the serialized owner-tunable <c>_haulWorldHeightMeters</c>.</summary>
        public const float HaulWorldHeightMeters = DesignScreenHeightPx / (6f * AssetsPPU);

        /// <summary>VS-23 locked assets PPU (mirrors ArtCameraSetup.AssetsPPU; one PPU never changes).</summary>
        public const int AssetsPPU = 32;

        /// <summary>The desktop screen height the discrete tiers are tuned to be pixel-perfect at.</summary>
        public const int DesignScreenHeightPx = 1080;

        /// <summary>
        /// The Pixel-Perfect 16:9 LANDSCAPE reference for the DEFAULT (Dory) framing, in reference
        /// pixels at the locked PPU. Equals <see cref="ReferenceResolutionForWorldHeight"/> of
        /// <see cref="DefaultWorldHeightMeters"/>; kept as named constants for callers/tests.
        /// </summary>
        public const int ReferenceWidthPx = 640;
        public const int ReferenceHeightPx = 360;

        /// <summary>Orthographic size is half the visible world height.</summary>
        public static float OrthoSizeForWorldHeight(float worldHeightMeters)
            => Mathf.Max(0.01f, worldHeightMeters * 0.5f);

        /// <summary>Visible world height for an orthographic size.</summary>
        public static float WorldHeightForOrthoSize(float orthographicSize)
            => orthographicSize * 2f;

        /// <summary>Visible world width for a height at an aspect (16:9 ≈ 1.778 for PC landscape).</summary>
        public static float WorldWidthForHeight(float worldHeightMeters, float aspect)
            => worldHeightMeters * aspect;

        /// <summary>
        /// The integer pixel-perfect zoom URP's Pixel Perfect Camera picks for a screen vs a reference
        /// (mirrors its documented min-of-axes floor, clamped to ≥1). Lets us reason about and test the
        /// live framing without entering play mode.
        /// </summary>
        public static int PixelPerfectZoom(int screenWidthPx, int screenHeightPx, int refWidthPx, int refHeightPx)
            => Mathf.Max(1, Mathf.Min(screenWidthPx / Mathf.Max(1, refWidthPx),
                                      screenHeightPx / Mathf.Max(1, refHeightPx)));

        /// <summary>
        /// World height the pixel-snapping camera actually shows at a screen height for a given integer
        /// zoom and PPU (= screenHeight / (zoom × ppu)).
        /// </summary>
        public static float WorldHeightAtZoom(int screenHeightPx, int zoom, int pixelsPerUnit)
            => screenHeightPx / (float)(Mathf.Max(1, zoom) * Mathf.Max(1, pixelsPerUnit));

        /// <summary>
        /// The 16:9 Pixel-Perfect reference resolution to frame a given world height, DISCRETELY (not a
        /// continuous lerp). It picks the integer zoom whose live height is closest to the request at
        /// the design screen, then returns the reference that yields that zoom — so each boat tier is
        /// crisp/pixel-perfect and a bigger boat shows more water. (Locked PPU 32 quantises to steps, so
        /// the live height is the nearest step to the requested value, not exact.)
        /// </summary>
        public static void ReferenceResolutionForWorldHeight(float worldHeightMeters,
            out int refWidthPx, out int refHeightPx, int ppu = AssetsPPU, int designScreenHeightPx = DesignScreenHeightPx)
        {
            float wanted = Mathf.Max(0.5f, worldHeightMeters);
            int bestZoom = 1;
            float bestErr = float.MaxValue;
            for (int z = 1; z <= 8; z++)
            {
                float live = designScreenHeightPx / (float)(z * Mathf.Max(1, ppu));
                float err = Mathf.Abs(live - wanted);
                if (err < bestErr) { bestErr = err; bestZoom = z; }
            }
            refHeightPx = Mathf.Max(1, designScreenHeightPx / bestZoom);
            refWidthPx = Mathf.RoundToInt(refHeightPx * 16f / 9f);
        }

        // ---- live framing (data-driven per boat) --------------------------------------------

        [Header("Framing")]
        [Tooltip("World height (m) the camera frames for the ACTIVE boat. Set initially by the greybox " +
                 "builder from the Dory hull, then updated on an upgrade swap via ActiveBoatChanged.")]
        [SerializeField] private float _worldHeightMeters = DefaultWorldHeightMeters;

        [Tooltip("Seconds to ease the zoom on a boat/on-foot framing change (an upgrade, stepping ashore). " +
                 "0 = hard-cut straight to the pixel-perfect step.")]
        [SerializeField] private float _framingTweenSeconds = 0.4f;

        // ---- deck zoom (control-mode-keyed; owner playtest 2026-07-08) ------------------------

        [Header("Deck Zoom")]
        [Tooltip("World height (m) framed while ON DECK — one pixel-perfect step closer than on foot so " +
                 "the boat fills the screen and deck work reads in detail. Default 6.75 = exactly the ×5 " +
                 "PPU-32 step at 1080p (requests quantise to the nearest step regardless).")]
        [SerializeField] private float _deckWorldHeightMeters = DeckWorldHeightMeters;

        [Tooltip("World height (m) framed while a trap haul is LIVE on deck — one step closer again so " +
                 "the rope-and-buoy action is the star. Default 5.625 = exactly the ×6 PPU-32 step at 1080p.")]
        [SerializeField] private float _haulWorldHeightMeters = HaulWorldHeightMeters;

        [Tooltip("Tighten that extra step while a trap haul is live (released the moment the pot surfaces " +
                 "or the haul goes idle). Untick to keep the plain deck framing throughout the haul.")]
        [SerializeField] private bool _haulTightensZoom = true;

        [Tooltip("Seconds to ease a deck zoom step (deck/haul). 0 = snap instantly to the pixel-perfect " +
                 "step. Either way the zoom LANDS exactly on a crisp integer step.")]
        [SerializeField] private float _deckZoomTweenSeconds = 0.25f;

        [Tooltip("Minimum seconds between committed zoom changes — rapid helm⇄deck hops inside this window " +
                 "collapse into a single re-zoom (a there-and-back hop re-zooms zero times). 0 = no hold.")]
        [SerializeField] private float _zoomHoldSeconds = 0.35f;

        // ---- follow behaviour ---------------------------------------------------------------

        [Header("Follow")]
        [Tooltip("The transform currently followed (player on foot, or the boat when aboard).")]
        public Transform Target;

        [Tooltip("Follow target while ON FOOT (the player). The control switcher picks between this and " +
                 "the boat target via the Core ControlModeChanged signal — the camera never references Player/Boats.")]
        [SerializeField] private Transform _onFootTarget;
        [Tooltip("Follow target while ABOARD (the boat).")]
        [SerializeField] private Transform _boatTarget;

        [Tooltip("Follow stiffness — higher snaps to the target faster.")]
        public float Smooth = 6f;

        [Tooltip("Seconds of the target's motion to lead by (look-ahead). 0 = locked on the boat.")]
        [SerializeField] private float _lookaheadSeconds = 0.35f;

        [Tooltip("Maximum look-ahead offset (metres) so a fast boat doesn't shove the camera too far.")]
        [SerializeField] private float _lookaheadMaxMeters = 2.5f;

        [Tooltip("How quickly the look-ahead offset eases in and out.")]
        [SerializeField] private float _lookaheadSmooth = 3f;

        private Camera _cam;
        private PixelPerfectCamera _ppc;

        private Vector3 _lastTargetPos;
        private Vector2 _lookahead;   // current (smoothed) look-ahead offset
        private bool _hasLast;

        // framing-tween state (an upgrade zoom-out / a deck zoom step)
        private bool _tweening;
        private float _tweenElapsed;
        private float _tweenSeconds;             // duration of the ACTIVE tween (upgrade vs deck step use different dials)
        private float _tweenFromOrtho, _tweenToOrtho;
        private int _pendingRefW, _pendingRefH;

        // zoom-policy state — the POCO decides WHICH discrete framing shows; this component only applies it
        private readonly CameraZoomPolicy _zoomPolicy = new CameraZoomPolicy();
        private ControlMode _mode;
        private bool _modeKnown;                 // no policy ticks until control declares itself — the builder-authored initial framing rules
        private bool _haulLive;                  // TrapHaulPhase.Hauling is live (via TrapHaulStateChanged)
        private float _boatWorldHeightMeters = DefaultWorldHeightMeters; // last ActiveBoatChanged hull framing (Dory fallback, mirrors ControlSwitcher's)

        private void Awake()
        {
            _cam = GetComponent<Camera>();
            _ppc = GetComponent<PixelPerfectCamera>();
            ApplyFramingHard(_worldHeightMeters); // initial framing from the active (Dory) hull
        }

        private void OnEnable()
        {
            EventBus.Subscribe<ActiveBoatChanged>(OnActiveBoatChanged);
            EventBus.Subscribe<ControlModeChanged>(OnControlModeChanged);
            EventBus.Subscribe<TrapHaulStateChanged>(OnTrapHaulStateChanged);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<ActiveBoatChanged>(OnActiveBoatChanged);
            EventBus.Unsubscribe<ControlModeChanged>(OnControlModeChanged);
            EventBus.Unsubscribe<TrapHaulStateChanged>(OnTrapHaulStateChanged);
        }

        // An active-boat change carries the hull's data-driven framing. It is only PUBLISHED while
        // piloting (a helm-take, or an upgrade granted at the helm — OwnedFleet stays quiet on a wharf
        // buy), so: store it always; re-apply now only if the boat framing is the one on screen (the
        // tangible bigger-boat beat). On a helm-take this arrives one signal BEFORE ControlModeChanged
        // (Aboard) — the stored height is fresh when the zoom policy commits the Boat framing.
        // Public so EditMode tests can drive the flow without the play-mode lifecycle (OwnedFleet pattern).
        public void OnActiveBoatChanged(ActiveBoatChanged e)
        {
            _boatWorldHeightMeters = e.CameraWorldHeightMeters;
            if (_zoomPolicy.HasCommitted && _zoomPolicy.Committed == CameraFraming.Boat)
                SetFraming(_boatWorldHeightMeters, _framingTweenSeconds);
        }

        // Switching control retargets the follow-cam IMMEDIATELY (the subject changed); the ZOOM follows
        // via the policy tick (same frame), which owns the discrete step choice and the anti-thrash hold.
        // Only the HELM (Aboard) gets the boat target; on foot AND on deck the camera follows the visible,
        // walking player — the deck-walking fisher is the subject, the boat just happens to be under them
        // (Build 5 on-deck state). Public for EditMode tests (see OnActiveBoatChanged).
        public void OnControlModeChanged(ControlModeChanged e)
        {
            Transform next = e.Mode == ControlMode.Aboard ? _boatTarget : _onFootTarget;
            if (next != null) Target = next;
            _mode = e.Mode;
            _modeKnown = true;
            if (e.Mode != ControlMode.OnDeck) _haulLive = false; // the haul is deck work — leaving the deck releases the tighten
        }

        // The trap haul's live phase drives the optional extra tighten: Hauling = live; Surfaced / Empty /
        // Idle release it. Value-struct payload, no GC. Public for EditMode tests (see OnActiveBoatChanged).
        public void OnTrapHaulStateChanged(TrapHaulStateChanged e)
            => _haulLive = e.State.Phase == TrapHaulPhase.Hauling;

        /// <summary>
        /// Frame the camera for a world height. <paramref name="animate"/> eases the zoom (the upgrade
        /// beat) then snaps the Pixel-Perfect reference to the new tier; otherwise it's a hard-cut.
        /// Public so the greybox builder / tests can set framing directly.
        /// </summary>
        public void SetFraming(float worldHeightMeters, bool animate)
            => SetFraming(worldHeightMeters, animate ? _framingTweenSeconds : 0f);

        /// <summary>
        /// Frame the camera for a world height, easing over <paramref name="tweenSeconds"/> (≤ 0 = hard
        /// snap). Either way the endpoint is a crisp pixel-perfect step — the ease only bridges frames.
        /// </summary>
        public void SetFraming(float worldHeightMeters, float tweenSeconds)
        {
            _worldHeightMeters = Mathf.Max(0.5f, worldHeightMeters);
            if (_cam == null) _cam = GetComponent<Camera>();
            if (_ppc == null) _ppc = GetComponent<PixelPerfectCamera>();

            if (tweenSeconds <= 0f || _cam == null || !Application.isPlaying)
            {
                ApplyFramingHard(_worldHeightMeters);
                return;
            }

            // Ease the orthographic zoom for the beat, then snap the Pixel-Perfect reference to the new
            // tier so the endpoint is crisp. PPC is paused during the ease so the lerp is actually
            // visible (it would otherwise re-impose its integer zoom each frame); the few non-snapped
            // frames are an acceptable trade for a smooth zoom beat.
            ReferenceResolutionForWorldHeight(_worldHeightMeters, out _pendingRefW, out _pendingRefH, CurrentPpu());
            _tweenFromOrtho = _cam.orthographicSize;
            _tweenToOrtho = OrthoSizeForWorldHeight(_worldHeightMeters);
            _tweenSeconds = tweenSeconds;
            _tweenElapsed = 0f;
            _tweening = true;
            if (_ppc != null) _ppc.enabled = false;
        }

        private void ApplyFramingHard(float worldHeightMeters)
        {
            _tweening = false;
            if (_cam == null) _cam = GetComponent<Camera>();
            if (_ppc == null) _ppc = GetComponent<PixelPerfectCamera>();

            ReferenceResolutionForWorldHeight(worldHeightMeters, out int rw, out int rh, CurrentPpu());
            if (_ppc != null)
            {
                _ppc.refResolutionX = rw;
                _ppc.refResolutionY = rh;
                _ppc.enabled = true;
            }
            if (_cam != null) _cam.orthographicSize = OrthoSizeForWorldHeight(worldHeightMeters);
        }

        private int CurrentPpu() => (_ppc != null && _ppc.assetsPPU > 0) ? _ppc.assetsPPU : AssetsPPU;

        private void LateUpdate()
        {
            FollowTarget();
            TickZoom(Time.timeAsDouble);
            if (_tweening) TickFramingTween();
        }

        /// <summary>
        /// One zoom-policy tick: mode (+ live haul) → the discrete framing to show, gated by the commit
        /// hold. Runs every LateUpdate — not in the event handlers — so a HELD change keeps being fed and
        /// lands the moment the hold expires (event-driven inputs, polled commit; enums/floats only, no
        /// per-frame allocation). Public with explicit time so EditMode tests drive the full
        /// signal→decision→framing flow without play mode.
        /// </summary>
        public void TickZoom(double nowSeconds)
        {
            if (!_modeKnown) return; // hold the builder-authored initial framing until control declares itself
            CameraFraming desired = CameraZoomPolicy.DesiredFraming(_mode, _haulLive, _haulTightensZoom);
            if (_zoomPolicy.TryCommit(desired, nowSeconds, _zoomHoldSeconds))
                SetFraming(WorldHeightFor(desired), TweenSecondsFor(desired));
        }

        /// <summary>The world height (m) a framing context maps to — the owner-tunable step table
        /// (Boat = the last <see cref="ActiveBoatChanged"/> hull height). Public for tests/tools.</summary>
        public float WorldHeightFor(CameraFraming framing)
        {
            switch (framing)
            {
                case CameraFraming.Boat: return _boatWorldHeightMeters;
                case CameraFraming.Deck: return _deckWorldHeightMeters;
                case CameraFraming.DeckHaul: return _haulWorldHeightMeters;
                default: return OnFootWorldHeightMeters;
            }
        }

        // Deck steps use the (snappier) deck dial; boat/on-foot keep the original upgrade-beat dial.
        private float TweenSecondsFor(CameraFraming framing)
            => framing == CameraFraming.Deck || framing == CameraFraming.DeckHaul
                ? _deckZoomTweenSeconds : _framingTweenSeconds;

        private void FollowTarget()
        {
            if (Target == null) return;

            Vector3 tp = Target.position;

            // Estimate target velocity from frame-to-frame motion (no coupling to the boat's body).
            Vector2 velocity = (_hasLast && Time.deltaTime > 0f)
                ? (Vector2)(tp - _lastTargetPos) / Time.deltaTime
                : Vector2.zero;
            _lastTargetPos = tp;
            _hasLast = true;

            // Lead slightly in the direction of travel, capped so it never throws the boat off-screen.
            Vector2 desiredLookahead = Vector2.ClampMagnitude(velocity * _lookaheadSeconds, _lookaheadMaxMeters);
            _lookahead = Vector2.Lerp(_lookahead, desiredLookahead,
                                      1f - Mathf.Exp(-_lookaheadSmooth * Time.deltaTime));

            Vector3 goal = tp + (Vector3)_lookahead;
            goal.z = transform.position.z; // keep the camera's depth
            transform.position = Vector3.Lerp(transform.position, goal,
                                              1f - Mathf.Exp(-Smooth * Time.deltaTime));
        }

        private void TickFramingTween()
        {
            _tweenElapsed += Time.deltaTime;
            float t = _tweenSeconds > 0f ? Mathf.Clamp01(_tweenElapsed / _tweenSeconds) : 1f;
            if (_cam != null)
                _cam.orthographicSize = Mathf.Lerp(_tweenFromOrtho, _tweenToOrtho, Mathf.SmoothStep(0f, 1f, t));

            if (t >= 1f)
            {
                _tweening = false;
                if (_ppc != null)
                {
                    _ppc.refResolutionX = _pendingRefW;
                    _ppc.refResolutionY = _pendingRefH;
                    _ppc.enabled = true; // snap to the crisp, pixel-perfect new tier
                }
                if (_cam != null) _cam.orthographicSize = _tweenToOrtho;
            }
        }
    }
}
