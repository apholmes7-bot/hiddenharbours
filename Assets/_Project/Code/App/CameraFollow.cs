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
    /// builder and the EditMode tests. PC-first (ADR 0005).
    ///
    /// WORLD BOUNDS (scene-sizing §6 item 4): the view is clamped inside the region's authored
    /// rectangle — see <see cref="CameraBounds"/> and <see cref="ConfigureBounds"/>. Unconfigured
    /// (zero size) means unclamped, so this is inert until a region builder wires its extent.
    ///
    /// ON-DECK ZOOM (owner playtest 2026-07-08): stepping onto the DECK steps the camera IN one
    /// pixel-perfect step past the on-foot framing, so the boat fills the screen and deck work reads in
    /// detail — and (tunably) one step closer again while a trap haul is LIVE, releasing on surface/idle.
    /// The decisions live in <see cref="CameraZoomPolicy"/> (a tested POCO with a commit hold so rapid
    /// helm⇄deck hops don't thrash); inputs arrive only via the Core <see cref="ControlModeChanged"/> /
    /// <see cref="TrapHaulStateChanged"/> signals — App never references Player/Boats/Fishing (rule 4).
    ///
    /// PLAYER ZOOM (owner ruling 2026-08-19, "the wheel is the player's eye"): the mouse wheel walks the
    /// view up and down the same integer ladder — closer to read an interior, wider to see where you are
    /// going. It is a second hand on one ladder, not a second zoom system.
    ///
    /// ON FOOT she owns the RUNG outright, between the owner's two metre clamps, and it survives the
    /// whole voyage: stepping ashore simply lands back on the rung the walker left, because that rung IS
    /// the on-foot framing.
    ///
    /// ABOARD AND ON DECK (owner ruling 2026-08-22) the wheel works too, but there she owns an OFFSET in
    /// whole rungs from whatever the context ruled, bounded by the band on <c>GameConfig.PlayerZoom</c>
    /// and RELEASED on every tier change — a mode commit, a new hull. That is what keeps the framing the
    /// hull's: §9.8's "whole vessel visible" derivation and the deck step are still what she is handed
    /// each time she arrives, and the wheel is a look around from there. A live haul and a road vehicle
    /// stay ruled outright. Every stop, at every framing, is an integer pixel-perfect step.
    ///
    /// The range, the band and the wheel's feel are owner-tunable on <c>GameConfig.PlayerZoom</c>
    /// (rule 6); the device is read by <see cref="CameraZoomInput"/>, so this class still knows nothing
    /// about input.
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

        [Tooltip("HELM framing margin (owner ruling 2026-07-29 §9.8, 'the whole vessel visible, WITH " +
                 "margin'): how much more than the hull's own footprint the view must show. 1 = the " +
                 "hull exactly fills the short axis at its worst heading; 1.4 leaves a comfortable " +
                 "band of water around it. Only ever pushes the framing OUT — a small boat keeps its " +
                 "authored intimate framing.")]
        [Min(1f)] [SerializeField] private float _helmFitMarginFactor = 1.4f;

        [Tooltip("The fleet's iso elevation (degrees) — the rigs bake at 40. Bow-on, a hull is " +
                 "foreshortened into sin(elevation) of the screen's SHORT axis, which is what makes " +
                 "the fit requirement 0.64x the hull's length rather than 1x. Matches " +
                 "HullMeshDef.ElevationDeg; change both together or the framing over-zooms.")]
        [SerializeField] private float _isoElevationDegrees = 40f;

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

        // ---- world bounds (scene-sizing §6 item 4) -------------------------------------------
        //
        // ⚠️ DELIBERATELY NOT SERIALIZED HERE. This camera lives on the PERSISTENT CORE — it survives
        // every region hop — so a rectangle baked onto it at build time would be the START region's
        // extent forever, and travelling to a bigger region would clamp the view to a box in the
        // middle of it. The bounds must change WITH the region, so they arrive through the same seam
        // the region id does: RegionAnchor publishes them on enable (which covers boot AND travel,
        // where the travel coordinator alone would miss boot), and this reads them live.

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
        private bool _carriedAboard;             // riding someone else's deck as a passenger (CarriedAboardChanged)
        private float _boatWorldHeightMeters = DefaultWorldHeightMeters; // last ActiveBoatChanged hull framing (Dory fallback, mirrors ControlSwitcher's)
        // Last ActiveVehicleChanged framing. The fallback is the on-foot step rather than a made-up number:
        // with no vehicle ever announced this framing is unreachable (nothing can be Driving), and if it
        // somehow were reached, showing what a walker sees is the harmless answer.
        private float _vehicleWorldHeightMeters = OnFootWorldHeightMeters;

        // PLAYER ZOOM (the wheel, owner ruling 2026-08-19) — the walker's chosen rung on the SAME
        // ladder every other framing lands on. Seeded lazily from the authored on-foot height so a
        // camera nobody ever scrolls behaves byte-for-byte as it did before this existed. Not saved
        // and never read by the sim: this is presentation (rule 5), the same as the deck step above.
        private int _playerZoomStep;
        private bool _playerZoomStepKnown;

        // THE ABOARD BAND (owner ruling 2026-08-22) — her offset in whole rungs from whatever the helm
        // or the deck ruled. Transient by design and RESET on every tier change (a commit, a new hull),
        // which is what keeps §9.8's per-hull derivation and the deck step authoritative: arriving
        // anywhere hands her the ruled framing, and the wheel is a look around from there. Zero is the
        // ruled framing exactly, byte-for-byte — see CameraZoomPolicy.BandWorldHeightMeters.
        private int _playerBandOffset;

        private void Awake()
        {
            _cam = GetComponent<Camera>();
            _ppc = GetComponent<PixelPerfectCamera>();
            ApplyFramingHard(_worldHeightMeters); // initial framing from the active (Dory) hull
        }

        private void OnEnable()
        {
            EventBus.Subscribe<ActiveBoatChanged>(OnActiveBoatChanged);
            EventBus.Subscribe<ActiveVehicleChanged>(OnActiveVehicleChanged);
            EventBus.Subscribe<ControlModeChanged>(OnControlModeChanged);
            EventBus.Subscribe<TrapHaulStateChanged>(OnTrapHaulStateChanged);
            EventBus.Subscribe<CarriedAboardChanged>(OnCarriedAboardChanged);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<ActiveBoatChanged>(OnActiveBoatChanged);
            EventBus.Unsubscribe<ActiveVehicleChanged>(OnActiveVehicleChanged);
            EventBus.Unsubscribe<ControlModeChanged>(OnControlModeChanged);
            EventBus.Unsubscribe<CarriedAboardChanged>(OnCarriedAboardChanged);
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
            // The owner's §9.8 ruling: the authored framing is FLOORED by what it takes to show the
            // whole vessel. A floor, not a replacement — small boats keep their intimate framing.
            float ruled = CameraZoomPolicy.HelmWorldHeightMeters(
                e.CameraWorldHeightMeters, e.HullLengthMeters,
                _helmFitMarginFactor, _isoElevationDegrees, CurrentAspect());

            // A DIFFERENT HULL IS A TIER CHANGE, so the player's band offset is released here as surely
            // as it is on a mode commit. Keeping it would frame the new boat at the old boat's zoom —
            // the exact defect §9.8 exists to kill, arriving through the back door. Guarded on the
            // framing actually CHANGING because this signal is also re-published verbatim on every
            // region arrival (ControlSwitcher.ReassertControlMode), and a hop across a passage is not
            // a new boat: snapping her band on every landfall would be a twitch with no cause.
            if (!Mathf.Approximately(ruled, _boatWorldHeightMeters)) _playerBandOffset = 0;
            _boatWorldHeightMeters = ruled;

            if (_zoomPolicy.HasCommitted && _zoomPolicy.Committed == CameraFraming.Boat)
                SetFraming(WorldHeightFor(CameraFraming.Boat), _framingTweenSeconds);
        }

        // A vehicle taken over carries her own framing, exactly as a hull does. No length term and so no
        // "whole vessel visible" floor: the §9.8 ruling exists because big hulls outgrew their authored
        // framing, and a road vehicle is many times smaller than the view she asks for.
        // Stored ALWAYS, re-applied now only if the vehicle framing is already the one on screen — the
        // ActiveBoatChanged rule, for the ActiveBoatChanged reason (this arrives one signal BEFORE the mode
        // change on taking a wheel, so the stored height is fresh when the policy commits).
        // Public so EditMode tests can drive the flow without the play-mode lifecycle.
        public void OnActiveVehicleChanged(ActiveVehicleChanged e)
        {
            _vehicleWorldHeightMeters = Mathf.Max(0.5f, e.CameraWorldHeightMeters);
            if (_zoomPolicy.HasCommitted && _zoomPolicy.Committed == CameraFraming.Vehicle)
                SetFraming(_vehicleWorldHeightMeters, _framingTweenSeconds);
        }

        // Switching control retargets the follow-cam IMMEDIATELY (the subject changed); the ZOOM follows
        // via the policy tick (same frame), which owns the discrete step choice and the anti-thrash hold.
        // Only the HELM (Aboard) gets the boat target; on foot AND on deck the camera follows the visible,
        // walking player — the deck-walking fisher is the subject, the boat just happens to be under them
        // (Build 5 on-deck state). Public for EditMode tests (see OnActiveBoatChanged).
        //
        // ⚠️ DRIVING deliberately takes the on-foot target too, and that is not an oversight to tidy up:
        // the driver is SEATED on the machine's root every frame by ControlSwitcher, so following the
        // player IS following the truck — and it keeps working when she is despawned under him, where a
        // captured vehicle transform would leave the camera parked on a destroyed object.
        public void OnControlModeChanged(ControlModeChanged e)
        {
            Transform next = e.Mode == ControlMode.Aboard ? _boatTarget : _onFootTarget;
            if (next != null) Target = next;
            _mode = e.Mode;
            _modeKnown = true;
            // Both deck-context flags are released by leaving the deck. The haul because it is deck
            // work; the carry because a passenger who is no longer on the deck is no longer a passenger
            // — and a carry flag left standing would frame every later boarding as a passage. Its
            // publisher clears it too; this is the belt that cannot be forgotten.
            if (e.Mode != ControlMode.OnDeck) { _haulLive = false; _carriedAboard = false; }
        }

        // She is a PASSENGER on the deck under her, not a hand working it — so the view is the vessel's
        // rather than the workbench's. Value-struct payload, no GC. Public for EditMode tests.
        public void OnCarriedAboardChanged(CarriedAboardChanged e) => _carriedAboard = e.Carried;

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

            // Land on a LADDER STEP — never an arbitrary ortho size (the ruling's standing constraint).
            int ppu = CurrentPpu();
            int step = CameraZoomPolicy.StepForWorldHeight(worldHeightMeters, ppu, DesignScreenHeightPx);
            float stepHeight = CameraZoomPolicy.WorldHeightForStep(step, ppu, DesignScreenHeightPx);

            if (CameraZoomPolicy.StepIsPixelPerfectUpscale(step))
            {
                // ⚠️ UNCHANGED PATH, deliberately byte-for-byte. Every framing that fits inside the
                // 1:1 pivot — on foot, on deck, hauling, and every hull up to the dragger — goes
                // through exactly the code it always did, including setting the ortho from the RAW
                // request rather than the snapped step (PixelPerfectCamera re-imposes the step at
                // runtime anyway). The ruling is the HELM only; snapping the ortho here as well
                // looked tidier and moved the on-foot and deck framings, which two existing tests
                // caught immediately.
                ReferenceResolutionForWorldHeight(worldHeightMeters, out int rw, out int rh, ppu);
                if (_ppc != null)
                {
                    _ppc.refResolutionX = rw;
                    _ppc.refResolutionY = rh;
                    _ppc.enabled = true;
                }
                if (_cam != null) _cam.orthographicSize = OrthoSizeForWorldHeight(worldHeightMeters);
                return;
            }

            if (_ppc != null)
            {
                // ⚠️ A DOWNSCALE step cannot go through PixelPerfectCamera: its zoom is
                // max(1, min(screen/ref)), so a reference LARGER than the screen still renders 1:1 and
                // the framing would silently snap back to 33.75 m — the exact cap this work exists to
                // remove. Drive the ortho directly instead; the ratio is still a clean integer (2x2
                // asset px to one screen px), shrinking rather than magnifying.
                _ppc.enabled = false;
            }

            if (_cam != null) _cam.orthographicSize = OrthoSizeForWorldHeight(stepHeight);
        }

        private int CurrentPpu() => (_ppc != null && _ppc.assetsPPU > 0) ? _ppc.assetsPPU : AssetsPPU;

        /// <summary>The live viewport aspect, falling back to the 16:9 design reference before a
        /// camera exists (EditMode, a test rig). An ultrawide window shows more world WIDTH at the
        /// same zoom, so a beam-on hull needs less height there — the fit derivation reads it.</summary>
        private float CurrentAspect()
        {
            if (_cam == null) _cam = GetComponent<Camera>();
            return (_cam != null && _cam.aspect > 0f)
                ? _cam.aspect : ReferenceWidthPx / (float)ReferenceHeightPx;
        }

        private void LateUpdate()
        {
            FollowTarget();
            TickZoom(Time.timeAsDouble);
            if (_tweening) TickFramingTween();
            // ⚠️ The clamp runs LAST, after the zoom has settled for this frame. Its allowance is a
            // function of the half-extents, so clamping before TickZoom/TickFramingTween would size
            // the allowance to the PREVIOUS frame's zoom — visible as a one-frame overshoot past the
            // map edge on every zoom step, which is exactly where a bounds rig gets noticed.
            ApplyBounds();
        }

        /// <summary>
        /// The rectangle the view is kept inside — the ACTIVE region's authored extent, read live
        /// from the region seam (<c>GameServices.CurrentRegionBounds</c>, published by
        /// <see cref="RegionAnchor"/> from <c>RegionDef.WorldCenter</c>/<c>WorldSizeMeters</c>). No
        /// region reporting an extent (a bare test scene, a region built before this rig, EditMode)
        /// means zero size, which means unbounded — so nothing changes anywhere until a region
        /// publishes one.
        /// </summary>
        public CameraBounds Bounds
        {
            get
            {
                Rect r = GameServices.CurrentRegionBounds;
                return new CameraBounds(r.center, r.size);
            }
        }

        /// <summary>
        /// Half-extents of what the camera shows RIGHT NOW, in world metres — orthographic size is
        /// half the visible height, and the width follows the viewport aspect. Read live rather than
        /// derived from the framing constants so it is correct mid-tween and at whatever aspect the
        /// window happens to be (an ultrawide monitor sees more world width at the same zoom, and must
        /// be held further from the edge for it).
        /// </summary>
        private bool TryGetHalfExtents(out float halfWidth, out float halfHeight)
        {
            if (_cam == null) _cam = GetComponent<Camera>();
            if (_cam == null) { halfWidth = halfHeight = 0f; return false; }

            halfHeight = _cam.orthographicSize;
            float aspect = _cam.aspect > 0f ? _cam.aspect : (ReferenceWidthPx / (float)ReferenceHeightPx);
            halfWidth = halfHeight * aspect;
            return true;
        }

        private void ApplyBounds()
        {
            CameraBounds bounds = Bounds;
            if (!bounds.IsBounded) return;
            if (!TryGetHalfExtents(out float halfWidth, out float halfHeight)) return;

            Vector3 p = transform.position;
            Vector2 clamped = CameraBounds.Clamp(new Vector2(p.x, p.y), in bounds, halfWidth, halfHeight);
            if (clamped.x != p.x || clamped.y != p.y)
                transform.position = new Vector3(clamped.x, clamped.y, p.z);   // depth is never touched
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
            CameraFraming desired = CameraZoomPolicy.DesiredFraming(_mode, _haulLive, _haulTightensZoom,
                                                                    _carriedAboard);
            if (!_zoomPolicy.TryCommit(desired, nowSeconds, _zoomHoldSeconds)) return;

            // ⭐ A COMMITTED FRAMING CHANGE IS A TIER CHANGE, and a tier change releases the player's
            // aboard band (owner ruling 2026-08-22). Ashore ⇄ helm ⇄ deck, and a haul starting or
            // ending, all land here — each of them hands her a framing somebody else ruled, and she
            // gets it as ruled. Her WALKING rung is untouched by this and survives the whole voyage;
            // the two are different kinds of state for exactly this reason.
            _playerBandOffset = 0;
            SetFraming(WorldHeightFor(desired), TweenSecondsFor(desired));
        }

        /// <summary>The framing the camera has actually committed to right now, and whether it has
        /// committed at all. Read-only; public so a test can ask what the player is being shown rather
        /// than inferring it from an orthographic size that has been snapped to a ladder step.</summary>
        public CameraFraming Framing => _zoomPolicy.Committed;

        /// <summary>False until control has declared itself and the first framing has committed.</summary>
        public bool FramingKnown => _zoomPolicy.HasCommitted;

        /// <summary>The world height (m) a framing context maps to — the owner-tunable step table
        /// (Boat = the last <see cref="ActiveBoatChanged"/> hull height), with the player's own hand
        /// on it: her rung on foot, her band offset at the helm and on deck. Public for tests/tools.</summary>
        public float WorldHeightFor(CameraFraming framing)
        {
            switch (framing)
            {
                case CameraFraming.Boat: return BandedHeight(_boatWorldHeightMeters);
                case CameraFraming.Deck: return BandedHeight(_deckWorldHeightMeters);
                case CameraFraming.DeckHaul: return _haulWorldHeightMeters;   // ruled outright
                case CameraFraming.Vehicle: return _vehicleWorldHeightMeters; // ruled outright
                default: return PlayerZoomWorldHeightMeters;                  // the walker's own rung
            }
        }

        // Deck steps use the (snappier) deck dial; boat/on-foot keep the original upgrade-beat dial.
        private float TweenSecondsFor(CameraFraming framing)
            => framing == CameraFraming.Deck || framing == CameraFraming.DeckHaul
                ? _deckZoomTweenSeconds : _framingTweenSeconds;

        // ================= PLAYER ZOOM: the wheel is the player's eye (owner ruling 2026-08-19) =====
        //
        // The rules are all in CameraZoomPolicy (pure, EditMode-tested); this half only APPLIES them,
        // exactly as the deck step above does. Four facts worth stating here because they are
        // properties of the wiring rather than of the rules:
        //
        //  1. THE WHEEL DOES NOT GO THROUGH TickZoom. The policy commits on a CHANGE of framing, and a
        //     wheel turn changes nothing about WHICH framing is wanted — only which rung of it, or how
        //     far off the ruled one she is looking. So a nudge re-frames directly.
        //  2. DISEMBARKING RESTORES THE WALKER'S TIER FOR FREE. Stepping ashore commits OnFoot, and
        //     TickZoom asks WorldHeightFor(OnFoot) — which is the remembered rung. Nothing saves,
        //     reinstates, or even notices the restore; there is simply no other on-foot height.
        //  3. THE CLAMPS ARE READ LIVE, not cached. The owner tunes them on the GameConfig asset while
        //     the game runs (rule 6), and a range edited to exclude the current rung must pull the view
        //     back inside on the very next nudge rather than at the next scene load. The aboard band's
        //     two allowances are read the same way, through the same live property.
        //  4. THE BAND IS RELEASED IN TWO PLACES, and both are tier changes rather than wheel events:
        //     a committed framing change (TickZoom) and a hull whose framing actually differs
        //     (OnActiveBoatChanged). Nothing else may clear it, and nothing else needs to.

        /// <summary>The owner's walking-zoom range and wheel feel, live from the config asset — with
        /// the shipped defaults when no asset is wired (EditMode, a bare test rig), the same
        /// null-tolerant read every other consumer of <see cref="GameServices.Config"/> makes.</summary>
        private static PlayerZoomSettings ZoomSettings
            => GameServices.Config != null
                ? GameServices.Config.PlayerZoom.Sanitized()
                : PlayerZoomSettings.Default;

        /// <summary>How many crisp stops the wheel may take a RULED framing closer / wider — the
        /// owner's aboard band, live from the config asset (rule 6). Magnitudes: a negative typed into
        /// either simply means "no stops that way".</summary>
        private static int BandStopsCloser => Mathf.Abs(ZoomSettings.AboardStopsCloser);

        /// <inheritdoc cref="BandStopsCloser"/>
        private static int BandStopsWider => Mathf.Abs(ZoomSettings.AboardStopsWider);

        /// <summary>The player's band offset, held live inside the owner's allowance — so a config
        /// edited to a narrower band pulls the view back in on the very next read, not at the next
        /// scene load (the same live-clamp discipline <see cref="PlayerZoomStep"/> keeps).</summary>
        public int PlayerBandOffset
            => CameraZoomPolicy.ClampBandOffset(_playerBandOffset, BandStopsCloser, BandStopsWider);

        /// <summary>A ruled framing with the player's band offset applied — the helm's and the deck's
        /// heights as she is actually being shown them. At offset zero this is the ruled height
        /// byte-for-byte; see <see cref="CameraZoomPolicy.BandWorldHeightMeters"/> for why that
        /// matters more than it looks.</summary>
        private float BandedHeight(float ruledWorldHeightMeters)
            => CameraZoomPolicy.BandWorldHeightMeters(ruledWorldHeightMeters, _playerBandOffset,
                                                     BandStopsCloser, BandStopsWider,
                                                     CurrentPpu(), DesignScreenHeightPx);

        /// <summary>The RULED height a banded framing is measured from — the hull's or the deck's own
        /// number, before the player's offset. A framing with no band answers whatever
        /// <see cref="WorldHeightFor"/> would, so the two can never disagree about it.</summary>
        private float RuledHeightFor(CameraFraming framing)
        {
            switch (framing)
            {
                case CameraFraming.Boat: return _boatWorldHeightMeters;
                case CameraFraming.Deck: return _deckWorldHeightMeters;
                default: return WorldHeightFor(framing);
            }
        }

        /// <summary>The ladder step the walking view sits on. Seeded from the authored on-foot framing
        /// the first time it is asked for, so a camera nobody scrolls frames exactly as it always
        /// did.</summary>
        public int PlayerZoomStep
        {
            get
            {
                if (!_playerZoomStepKnown)
                {
                    _playerZoomStep = CameraZoomPolicy.StepForWorldHeight(
                        OnFootWorldHeightMeters, CurrentPpu(), DesignScreenHeightPx);
                    _playerZoomStepKnown = true;
                }
                return CameraZoomPolicy.ClampPlayerStep(_playerZoomStep, ClosestStep, FarthestStep);
            }
        }

        /// <summary>The rung the walking view sits on with the wheel untouched — the ladder step the
        /// AUTHORED on-foot framing quantises to. The wheel's home, and the rung a fresh camera
        /// starts on.</summary>
        private int DefaultPlayerZoomStep => CameraZoomPolicy.StepForWorldHeight(
            OnFootWorldHeightMeters, CurrentPpu(), DesignScreenHeightPx);

        /// <summary>
        /// World height (m) the walking view currently frames — the player's rung of the ladder. This
        /// IS the on-foot framing; there is no second, unzoomed one behind it.
        ///
        /// <para>⚠️ <b>At the home rung it answers the AUTHORED height, not the rung's height.</b> The
        /// two frame identically (both quantise to the same pixel-perfect step, and
        /// <c>PixelPerfectCamera</c> re-imposes it every frame), but <see cref="ApplyFramingHard"/>
        /// deliberately drives the raw orthographic size from the REQUEST rather than the snapped step
        /// — its own comment records that snapping it "moved the on-foot and deck framings, which two
        /// existing tests caught immediately". Returning 8.4375 where the camera has always asked for
        /// 9 would move the standing view for every player who never touches the wheel. So the wheel
        /// at home is byte-for-byte the camera that shipped, and only a player who actually scrolls
        /// leaves the authored number behind.</para>
        /// </summary>
        public float PlayerZoomWorldHeightMeters
        {
            get
            {
                int step = PlayerZoomStep;
                return step == DefaultPlayerZoomStep
                    ? OnFootWorldHeightMeters
                    : CameraZoomPolicy.WorldHeightForStep(step, CurrentPpu(), DesignScreenHeightPx);
            }
        }

        /// <summary>How much raw scroll earns one tier (owner-tunable). Read by the wheel reader so
        /// there is one config lookup for the whole rig, not one per component.</summary>
        public float WheelUnitsPerNotch => Mathf.Max(1f, ZoomSettings.WheelUnitsPerNotch);

        /// <summary>
        /// Whether a wheel notch would do anything RIGHT NOW: the wheel is enabled, a modal is not
        /// holding the interaction gate, and the framing on screen is one the player may move (on foot,
        /// at the helm or on deck — never a live haul or a road vehicle). The reader checks this before
        /// touching the device so a blocked wheel also banks no scroll.
        ///
        /// <para>⚠️ <b>"On screen", not "committed".</b> An un-committed camera is a WALKING camera —
        /// the on-foot dead path the owner hit on 2026-08-22, written up in full on
        /// <see cref="CameraZoomPolicy.FramingOnScreen"/>.</para>
        /// </summary>
        public bool WheelIsLive
            => ZoomSettings.WheelEnabled
               && CameraZoomPolicy.WheelIsLive(_zoomPolicy.Committed, _zoomPolicy.HasCommitted,
                                               InteractionGate.IsBlocked);

        /// <summary>
        /// Step the view by whole wheel notches — <b>+1 = one stop CLOSER</b>. What moves depends on
        /// what she is looking at: on foot her RUNG, at the helm or on deck her OFFSET from the framing
        /// that context ruled. Returns true when the view actually moved (false at a clamp, at the end
        /// of the ladder, or when <see cref="WheelIsLive"/> is not). Public so the wheel reader, the
        /// tests and any future in-world control all drive the one entry point.
        /// </summary>
        public bool NudgePlayerZoom(int notches)
        {
            if (notches == 0 || !WheelIsLive) return false;

            // The framing she is LOOKING AT, which before the first commit is the walker's — the
            // builders author the on-foot height into the camera and she cannot be at a helm she has
            // not taken. See CameraZoomPolicy.FramingOnScreen for the playtest bug that lived here.
            CameraFraming framing = CameraZoomPolicy.FramingOnScreen(_zoomPolicy.Committed,
                                                                    _zoomPolicy.HasCommitted);
            return framing == CameraFraming.OnFoot
                ? NudgeWalkersRung(notches)
                : NudgeAboardBand(framing, notches);
        }

        /// <summary>ON FOOT: she owns the rung outright, between the owner's two metre clamps.</summary>
        private bool NudgeWalkersRung(int notches)
        {
            int from = PlayerZoomStep;          // also seeds the rung on the first ever nudge
            int to = CameraZoomPolicy.StepPlayerZoom(from, notches, ClosestStep, FarthestStep);
            if (to == from) return false;       // saturated at a clamp — no re-frame, no tween

            _playerZoomStep = to;
            // Through WorldHeightFor, not the ladder directly: scrolling back to the home rung must
            // land on the AUTHORED on-foot height, exactly where a player who never scrolled sits.
            SetFraming(WorldHeightFor(CameraFraming.OnFoot), Mathf.Max(0f, ZoomSettings.StepSeconds));
            return true;
        }

        /// <summary>
        /// ABOARD / ON DECK: she owns an OFFSET from the framing that context ruled, inside the band
        /// the owner allows (owner ruling 2026-08-22). Refused — with no re-frame at all — when the
        /// notch would change no step, whether because the allowance saturated or because the ladder
        /// itself ran out under a very wide hull.
        /// </summary>
        private bool NudgeAboardBand(CameraFraming framing, int notches)
        {
            int to = CameraZoomPolicy.StepBandOffset(
                _playerBandOffset, notches, RuledHeightFor(framing),
                BandStopsCloser, BandStopsWider, CurrentPpu(), DesignScreenHeightPx, out bool moved);
            if (!moved) return false;

            _playerBandOffset = to;
            SetFraming(WorldHeightFor(framing), Mathf.Max(0f, ZoomSettings.StepSeconds));
            return true;
        }

        /// <summary>The owner's CLOSEST clamp as a ladder step. Metres in the config, steps here — the
        /// conversion is the ladder's own nearest-step search, so a hand-typed height can only ever
        /// pick a neighbouring crisp stop, never a blurry framing.</summary>
        private int ClosestStep => CameraZoomPolicy.StepForWorldHeight(
            ZoomSettings.ClosestWorldHeightMeters, CurrentPpu(), DesignScreenHeightPx);

        /// <inheritdoc cref="ClosestStep"/>
        private int FarthestStep => CameraZoomPolicy.StepForWorldHeight(
            ZoomSettings.FarthestWorldHeightMeters, CurrentPpu(), DesignScreenHeightPx);

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
