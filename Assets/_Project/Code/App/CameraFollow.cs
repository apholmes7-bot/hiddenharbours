using UnityEngine;

namespace HiddenHarbours.App
{
    /// <summary>
    /// Greybox follow-cam: smoothly tracks the dory with a slight look-ahead in the direction of
    /// travel, framed for an intimate PC-first LANDSCAPE view (~14 m of world height) so the 4.5 m
    /// Dory reads large instead of getting lost in open blue. The framing constants/helpers below are
    /// the single source of truth shared by the greybox builder (which sets the camera + the
    /// Pixel-Perfect reference) and the EditMode framing tests. A fuller rig (bounds, zoom-by-boat-
    /// size) is later ui-ux/world work; this is just enough to play and read clearly. PC-first per
    /// ADR 0005.
    /// </summary>
    public class CameraFollow : MonoBehaviour
    {
        // ---- framing (single source of truth; PC-first landscape) ---------------------------

        /// <summary>Intimate default zoom: ~14 m of world HEIGHT visible (the design's default zoom).</summary>
        public const float DefaultWorldHeightMeters = 14f;

        /// <summary>
        /// The Pixel-Perfect 16:9 LANDSCAPE reference (PC-first), in reference pixels at the locked
        /// PPU (32). At 1920×1080 this resolves to an exact ×3 pixel-perfect zoom (no sub-pixel
        /// shimmer) and an intimate live framing; it also holds an integer zoom in smaller desktop
        /// windows instead of collapsing to an over-wide view like the old portrait reference.
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

        // ---- follow behaviour ---------------------------------------------------------------

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

        private Vector3 _lastTargetPos;
        private Vector2 _lookahead;   // current (smoothed) look-ahead offset
        private bool _hasLast;

        private void LateUpdate()
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
    }
}
