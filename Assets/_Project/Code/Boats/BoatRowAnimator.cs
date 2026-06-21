using UnityEngine;

namespace HiddenHarbours.Boats
{
    /// <summary>
    /// Animates the rowed dory by swapping the SpriteRenderer through the sliced DoryRow oar-cycle sheet
    /// (6 frames, idle at frame 0) while the boat is making way. Frame-swap only — no Animator, no
    /// architecture change: it reads the boat's velocity along the bow each frame, scales the oar tempo
    /// to speed, runs the cycle FORWARD when going ahead and REVERSED when going astern, and rests on the
    /// idle frame at a standstill. Lives on the boat GO beside the <see cref="BoatController"/> +
    /// SpriteRenderer; the greybox builder wires the frames from the sliced sheet (LoadSheetFrames).
    ///
    /// It only drives the sprite while the home hull (the dory) is the active hull, so a bought-boat swap
    /// (OwnedFleet → a Punt with its own static sprite) isn't clobbered each frame. The frame/tempo logic
    /// is pure static helpers so it's deterministically unit-testable without the play-mode lifecycle.
    /// </summary>
    [RequireComponent(typeof(BoatController))]
    [RequireComponent(typeof(SpriteRenderer))]
    public class BoatRowAnimator : MonoBehaviour
    {
        [Tooltip("The DoryRow oar-cycle frames in slice order (_0.._5); frame 0 is the oars-shipped idle " +
                 "pose. Wired by the greybox builder from the sliced sheet.")]
        [SerializeField] private Sprite[] _frames;

        [Tooltip("Frame shown at rest (oars shipped). Index into _frames.")]
        [SerializeField] private int _idleFrame = 0;

        [Tooltip("Bow speed (m/s) below which the dory is at rest and shows the idle frame.")]
        [SerializeField] private float _wayThreshold = 0.15f;

        [Tooltip("Oar-cycle frames-per-second per m/s of bow speed (the faster you row, the faster the oars).")]
        [SerializeField] private float _fpsPerSpeed = 4f;

        [Tooltip("Cap on oar-cycle fps so a fast boat doesn't strobe.")]
        [SerializeField] private float _maxFps = 12f;

        private BoatController _boat;
        private SpriteRenderer _renderer;
        private BoatHullDef _homeHull;   // the hull this row sheet belongs to (recorded at start)
        private float _phase;            // accumulated oar-cycle phase (whole numbers = frames)

        // ---- pure logic (unit-testable) -----------------------------------------------------

        /// <summary>Is the boat making way (rowing) rather than resting? Direction-agnostic.</summary>
        public static bool IsMakingWay(float bowSpeed, float wayThreshold)
            => Mathf.Abs(bowSpeed) >= wayThreshold;

        /// <summary>Oar-cycle frames per second for a bow speed, scaled by speed and capped so it never strobes.</summary>
        public static float CycleFps(float bowSpeed, float fpsPerSpeed, float maxFps)
            => Mathf.Clamp(Mathf.Abs(bowSpeed) * fpsPerSpeed, 0f, Mathf.Max(0f, maxFps));

        /// <summary>Advance the oar-cycle phase by one frame at this bow speed (deterministic in its inputs).</summary>
        public static float AdvancePhase(float phase, float bowSpeed, float fpsPerSpeed, float maxFps, float dt)
            => phase + CycleFps(bowSpeed, fpsPerSpeed, maxFps) * Mathf.Max(0f, dt);

        /// <summary>
        /// Frame index for a cycle phase. Ahead runs 0→count-1 and wraps; astern reverses it
        /// (count-1→0) for a clear backing stroke. Negative-phase-safe.
        /// </summary>
        public static int FrameForPhase(float phase, int frameCount, bool astern)
        {
            if (frameCount <= 0) return 0;
            int f = Mathf.FloorToInt(phase) % frameCount;
            if (f < 0) f += frameCount;
            return astern ? (frameCount - 1 - f) : f;
        }

        // ---- lifecycle ----------------------------------------------------------------------

        private void Awake()
        {
            _boat = GetComponent<BoatController>();
            _renderer = GetComponent<SpriteRenderer>();
            _homeHull = _boat != null ? _boat.Hull : null;   // the hull whose oar sheet we hold
            ApplyFrame(_idleFrame);
        }

        private void Update()
        {
            if (_renderer == null || _boat == null || _frames == null || _frames.Length == 0) return;
            // Don't fight a bought-boat swap: only drive the sprite while our home hull is active.
            if (_homeHull != null && _boat.Hull != _homeHull) return;

            float bowSpeed = Vector2.Dot(_boat.Velocity, (Vector2)transform.up);  // + ahead, - astern
            if (IsMakingWay(bowSpeed, _wayThreshold))
            {
                _phase = AdvancePhase(_phase, bowSpeed, _fpsPerSpeed, _maxFps, Time.deltaTime);
                ApplyFrame(FrameForPhase(_phase, _frames.Length, bowSpeed < 0f));
            }
            else
            {
                _phase = 0f;
                ApplyFrame(_idleFrame);   // oars shipped at rest
            }
        }

        private void ApplyFrame(int index)
        {
            if (_renderer == null || _frames == null || _frames.Length == 0) return;
            int i = Mathf.Clamp(index, 0, _frames.Length - 1);
            if (_frames[i] != null) _renderer.sprite = _frames[i];
        }
    }
}
