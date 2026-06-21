using UnityEngine;

namespace HiddenHarbours.Boats
{
    /// <summary>
    /// Animates the rowed dory by swapping the SpriteRenderer through the sliced DoryRow oar-cycle sheet
    /// (a symmetric both-oars cycle — 3 poses, idle at frame 0) while the boat is being rowed. Frame-swap
    /// only — no Animator, no architecture change: it reads the boat's ROWING ACTIVITY (oar effort) each
    /// frame, scales the oar tempo to it, runs the cycle FORWARD when rowing ahead and REVERSED when
    /// rowing astern, and rests on the idle frame when the oars are still. Lives on the boat GO beside the
    /// <see cref="BoatController"/> + SpriteRenderer; the greybox builder wires the frames (LoadSheetFrames).
    ///
    /// The sheet has no left-only/right-only poses, so a one-sided stroke still plays the symmetric cycle —
    /// a true single-oar VISUAL needs new art (art-pipeline follow-up), not faked here.
    ///
    /// It only drives the sprite while the home hull (the dory) is the active hull, so a bought-boat swap
    /// (OwnedFleet → a Punt with its own static sprite) isn't clobbered each frame. The frame/tempo logic
    /// is pure static helpers so it's deterministically unit-testable without the play-mode lifecycle.
    /// </summary>
    [RequireComponent(typeof(BoatController))]
    [RequireComponent(typeof(SpriteRenderer))]
    public class BoatRowAnimator : MonoBehaviour
    {
        [Tooltip("The DoryRow oar-cycle frames in slice order (_0.._2); frame 0 is the oars-shipped idle " +
                 "pose. Wired by the greybox builder from the sliced sheet.")]
        [SerializeField] private Sprite[] _frames;

        [Tooltip("Frame shown at rest (oars shipped). Index into _frames.")]
        [SerializeField] private int _idleFrame = 0;

        [Tooltip("Rowing effort (0..1) below which the oars are considered still and show the idle frame.")]
        [SerializeField] private float _rowThreshold = 0.1f;

        [Tooltip("Oar-cycle frames-per-second per unit of rowing effort (row harder → faster oars).")]
        [SerializeField] private float _fpsPerRow = 6f;

        [Tooltip("Cap on oar-cycle fps so hard rowing doesn't strobe.")]
        [SerializeField] private float _maxFps = 12f;

        private BoatController _boat;
        private SpriteRenderer _renderer;
        private BoatHullDef _homeHull;   // the hull this row sheet belongs to (recorded at start)
        private float _phase;            // accumulated oar-cycle phase (whole numbers = frames)

        // ---- pure logic (unit-testable) -----------------------------------------------------

        /// <summary>Are the oars being worked (rowing) rather than still? Direction-agnostic (ahead or astern).</summary>
        public static bool IsMakingWay(float drive, float threshold)
            => Mathf.Abs(drive) >= threshold;

        /// <summary>Oar-cycle frames per second for a rowing-effort signal, scaled and capped so it never strobes.</summary>
        public static float CycleFps(float drive, float fpsPerUnit, float maxFps)
            => Mathf.Clamp(Mathf.Abs(drive) * fpsPerUnit, 0f, Mathf.Max(0f, maxFps));

        /// <summary>Advance the oar-cycle phase by one frame at this rowing effort (deterministic in its inputs).</summary>
        public static float AdvancePhase(float phase, float drive, float fpsPerUnit, float maxFps, float dt)
            => phase + CycleFps(drive, fpsPerUnit, maxFps) * Mathf.Max(0f, dt);

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

            float drive = _boat.RowDrive;   // signed rowing effort: + rowing ahead, - rowing astern
            if (IsMakingWay(drive, _rowThreshold))
            {
                _phase = AdvancePhase(_phase, drive, _fpsPerRow, _maxFps, Time.deltaTime);
                ApplyFrame(FrameForPhase(_phase, _frames.Length, drive < 0f));
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
