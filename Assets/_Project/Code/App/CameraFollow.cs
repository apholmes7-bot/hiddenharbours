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
    /// </summary>
    public class CameraFollow : MonoBehaviour
    {
        // ---- framing (single source of truth; PC-first landscape) ---------------------------

        /// <summary>Intimate default zoom: ~14 m of world HEIGHT visible (the Dory's default).</summary>
        public const float DefaultWorldHeightMeters = 14f;

        /// <summary>Tighter on-foot framing: ~9 m of world height so the ~1.8 m walking fisher reads
        /// large. Uses the same data-driven, pixel-perfect mapping as the boat tiers.</summary>
        public const float OnFootWorldHeightMeters = 9f;

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

        [Tooltip("Seconds to ease the zoom when the active boat changes (an upgrade). 0 = hard-cut.")]
        [SerializeField] private float _framingTweenSeconds = 0.4f;

        // ---- follow behaviour ---------------------------------------------------------------

        [Header("Follow")]
        [Tooltip("The transform to follow (the dory).")]
        public Transform Target;

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

        // framing-tween state (an upgrade zoom-out)
        private bool _tweening;
        private float _tweenElapsed;
        private float _tweenFromOrtho, _tweenToOrtho;
        private int _pendingRefW, _pendingRefH;

        private void Awake()
        {
            _cam = GetComponent<Camera>();
            _ppc = GetComponent<PixelPerfectCamera>();
            ApplyFramingHard(_worldHeightMeters); // initial framing from the active (Dory) hull
        }

        private void OnEnable()  => EventBus.Subscribe<ActiveBoatChanged>(OnActiveBoatChanged);
        private void OnDisable() => EventBus.Unsubscribe<ActiveBoatChanged>(OnActiveBoatChanged);

        private void OnActiveBoatChanged(ActiveBoatChanged e)
            => SetFraming(e.CameraWorldHeightMeters, animate: true);

        /// <summary>
        /// Frame the camera for a world height. <paramref name="animate"/> eases the zoom (the upgrade
        /// beat) then snaps the Pixel-Perfect reference to the new tier; otherwise it's a hard-cut.
        /// Public so the greybox builder / tests can set framing directly.
        /// </summary>
        public void SetFraming(float worldHeightMeters, bool animate)
        {
            _worldHeightMeters = Mathf.Max(0.5f, worldHeightMeters);
            if (_cam == null) _cam = GetComponent<Camera>();
            if (_ppc == null) _ppc = GetComponent<PixelPerfectCamera>();

            if (!animate || _framingTweenSeconds <= 0f || _cam == null || !Application.isPlaying)
            {
                ApplyFramingHard(_worldHeightMeters);
                return;
            }

            // Ease the orthographic zoom for the beat, then snap the Pixel-Perfect reference to the new
            // tier so the endpoint is crisp. PPC is paused during the ease so the lerp is actually
            // visible (it would otherwise re-impose its integer zoom each frame); the few non-snapped
            // frames are an acceptable trade for a smooth "bigger boat" zoom-out.
            ReferenceResolutionForWorldHeight(_worldHeightMeters, out _pendingRefW, out _pendingRefH, CurrentPpu());
            _tweenFromOrtho = _cam.orthographicSize;
            _tweenToOrtho = OrthoSizeForWorldHeight(_worldHeightMeters);
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
            if (_tweening) TickFramingTween();
        }

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
            float t = _framingTweenSeconds > 0f ? Mathf.Clamp01(_tweenElapsed / _framingTweenSeconds) : 1f;
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
