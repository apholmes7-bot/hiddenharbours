using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Player
{
    /// <summary>What the hauling fisher's body is DOING this beat — which part of the owner's PlayerHaul
    /// sheet to show. Derived purely from consecutive haul snapshots (see <see cref="PlayerHaulAnimMath"/>).</summary>
    public enum HaulPose
    {
        /// <summary>No live haul — the normal standing/walking sprite owns the renderer.</summary>
        None = 0,
        /// <summary>Line is coming IN (holding on the lift / the calm wind-in) — play the hand-over-hand cycle.</summary>
        Pull = 1,
        /// <summary>Line is SLIPPING back (holding through the drop — the rope is fighting) — the strain frame.</summary>
        Strain = 2,
        /// <summary>The line is holding still (the pawl has it; the player eased off) — the ease frame.</summary>
        Ease = 3,
    }

    /// <summary>
    /// The PURE maths behind the deck hauler's animation — haul snapshot → pose → frame. Split out (the
    /// <c>PlayerSubmergeMath</c> / <c>TrapHaulMath</c> pattern) so the whole state→frame mapping is
    /// EditMode-testable headless. No engine state, no <c>Time</c>, no RNG.
    /// </summary>
    public static class PlayerHaulAnimMath
    {
        /// <summary>
        /// The pose for a haul snapshot, read off the LINE'S MOTION between consecutive snapshots — the same
        /// diegetic read the rope gives the player: line gaining → the hand-over-hand <see cref="HaulPose.Pull"/>;
        /// line slipping back → <see cref="HaulPose.Strain"/> (leaning into a loaded rope); line holding
        /// still → <see cref="HaulPose.Ease"/> (the pawl has it). Any non-hauling phase (idle / surfaced /
        /// empty) → <see cref="HaulPose.None"/>: give the renderer back to the walk sprite.
        /// </summary>
        /// <param name="phase">The snapshot's haul phase.</param>
        /// <param name="lineDelta">line01 change since the previous live snapshot (0 on the first).</param>
        /// <param name="epsilon">Dead-band below which the line reads as holding still.</param>
        public static HaulPose PoseFor(TrapHaulPhase phase, float lineDelta, float epsilon)
        {
            if (phase != TrapHaulPhase.Hauling) return HaulPose.None;
            float eps = Mathf.Max(0f, epsilon);
            if (lineDelta > eps) return HaulPose.Pull;
            if (lineDelta < -eps) return HaulPose.Strain;
            return HaulPose.Ease;
        }

        /// <summary>
        /// Which cycle frame (0..<paramref name="cycleFrameCount"/>−1) the hand-over-hand pull shows at a
        /// haul progress of <paramref name="line01"/>. The cycle is keyed DIRECTLY to the line hauled —
        /// <paramref name="cycleFramesPerLine"/> frames pass over a full 0→1 haul — so the hands literally
        /// move with the rope: a brisk take on a big lift runs the cycle fast, the calm wind-in strolls it,
        /// and when the line stops the hands stop (the pose logic swaps to strain/ease there). Deterministic
        /// from the snapshot alone — no timers to drift. Negative-safe; count ≤ 0 returns 0.
        /// </summary>
        public static int CycleFrame(float line01, float cycleFramesPerLine, int cycleFrameCount)
        {
            if (cycleFrameCount <= 0) return 0;
            float phase = Mathf.Max(0f, line01) * Mathf.Max(0f, cycleFramesPerLine);
            return Mathf.FloorToInt(phase) % cycleFrameCount;
        }

        /// <summary>
        /// Whether the haul sprite mirrors (flipX) so its ROPE SIDE points at the worked buoy.
        /// <paramref name="buoyDx"/> is buoy.x − player.x; <paramref name="artRopeSideIsLeft"/> says which
        /// side the SHEET draws the rope on (the owner's art holds it to the sprite's left). A buoy dead on
        /// the player's x keeps the un-flipped art. Pure + static.
        /// </summary>
        public static bool FlipXFor(float buoyDx, bool artRopeSideIsLeft)
            => artRopeSideIsLeft ? buoyDx > 0f : buoyDx < 0f;
    }

    /// <summary>
    /// Plays the owner's PLAYER HAUL sheet on the deck-walking fisher while a trap haul is live — the
    /// body language of haul-with-the-swell (#184): the hand-over-hand cycle while line comes in, the
    /// STRAIN frame while the rope fights back on a drop, the EASE frame while the pawl holds, and the
    /// sprite mirrored (flipX) so the rope side faces the worked buoy. When the haul ends (surfaced /
    /// empty / let go) the walk sprite is restored exactly as it was.
    ///
    /// <para><b>Cross-module via Core only (rule 4).</b> It never references Fishing: everything it needs
    /// arrives in the <see cref="TrapHaulStateChanged"/> snapshots (phase, line01, strain, the buoy's
    /// position) — published on quantized line/strain steps and hold/release edges, never per frame. The
    /// pose is derived from the line's motion between snapshots (<see cref="PlayerHaulAnimMath.PoseFor"/>),
    /// and the pull cycle is keyed straight to line01 (<see cref="PlayerHaulAnimMath.CycleFrame"/>) so the
    /// cycle rate breathes with the take rate for free — no Update loop, no timers, no per-frame work at
    /// all (rule 7): the component only runs when a snapshot lands.</para>
    ///
    /// <para><b>Frames.</b> Wired by the builder from the sliced <c>PlayerHaul.png</c> in slice order —
    /// frames 0..5 the hand-over-hand cycle, 6 STRAIN (leaning back, rope loaded), 7 EASE (letting the
    /// pawl hold) — the owner's sheet spec. The indices are serialized tunables so the owner can remap
    /// without code if the sheet evolves. Missing art → the component is inert (null-safe greybox rule).</para>
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class PlayerHaulAnimator : MonoBehaviour
    {
        // Line-motion dead-band for the pose read: matches the take epsilon the haul itself uses to call a
        // tick "gained" (a read threshold, not a balance number).
        private const float LineDeltaEpsilon = 1e-5f;

        [Header("The owner's haul sheet (slice order)")]
        [Tooltip("PlayerHaul frames in slice order: 0..5 = the hand-over-hand pull cycle, 6 = STRAIN " +
                 "(leaning back, rope loaded), 7 = EASE (relaxed, the pawl holds). Wired by the builder " +
                 "from the sliced sheet; empty = the animator is inert.")]
        [SerializeField] private Sprite[] _frames;

        [Header("Frame mapping (owner-remappable, rule 6)")]
        [Tooltip("How many leading frames form the hand-over-hand pull cycle.")]
        [SerializeField, Min(1)] private int _cycleFrameCount = 6;
        [Tooltip("Frame index shown while the rope FIGHTS (line slipping back on a held drop).")]
        [SerializeField, Min(0)] private int _strainFrame = 6;
        [Tooltip("Frame index shown while the pawl holds the line (player eased off).")]
        [SerializeField, Min(0)] private int _easeFrame = 7;
        [Tooltip("Cycle frames that pass over a FULL haul (line 0→1) — the hands move with the rope, so " +
                 "the cycle rate breathes with the take rate. 24 = four full 6-frame cycles per haul.")]
        [SerializeField, Min(0f)] private float _cycleFramesPerLine = 24f;
        [Tooltip("Which side the SHEET draws the rope on (the owner's art pulls at the sprite's LEFT). " +
                 "The sprite mirrors so this side faces the worked buoy.")]
        [SerializeField] private bool _artRopeSideIsLeft = true;

        private SpriteRenderer _renderer;
        private bool _active;             // a haul owns the renderer right now
        private Sprite _restoreSprite;    // the walk sprite to hand back when the haul ends
        private bool _restoreFlipX;
        private float _lastLine01;
        private bool _hasLastLine;        // the previous snapshot was a live-haul one (delta is meaningful)

        /// <summary>The pose currently shown (None when the walk sprite owns the renderer). For tests/tooling.</summary>
        public HaulPose Pose { get; private set; } = HaulPose.None;

        private void OnEnable() => EventBus.Subscribe<TrapHaulStateChanged>(OnHaulStateChanged);

        private void OnDisable()
        {
            EventBus.Unsubscribe<TrapHaulStateChanged>(OnHaulStateChanged);
            EndHaul();   // disabling mid-haul hands the renderer back (never a stuck haul frame)
        }

        /// <summary>Public so tests can drive the mapping through the same path the bus uses (the
        /// TrapDeckGating convention — no play-mode lifecycle needed).</summary>
        public void OnHaulStateChanged(TrapHaulStateChanged e)
        {
            TrapHaulState s = e.State;
            float delta = _hasLastLine ? s.Line01 - _lastLine01 : 0f;
            _lastLine01 = s.Line01;
            _hasLastLine = s.Phase == TrapHaulPhase.Hauling;

            HaulPose pose = PlayerHaulAnimMath.PoseFor(s.Phase, delta, LineDeltaEpsilon);
            if (pose == HaulPose.None) { EndHaul(); return; }

            if (_renderer == null) _renderer = GetComponent<SpriteRenderer>();
            if (_renderer == null || _frames == null || _frames.Length == 0) return;   // inert without art

            if (!_active)
            {
                // Take the renderer over: remember the walk sprite so the haul ends exactly where it began.
                _restoreSprite = _renderer.sprite;
                _restoreFlipX = _renderer.flipX;
                _active = true;
            }

            // Face the buoy: the rope side of the sheet points at the pot being worked.
            _renderer.flipX = PlayerHaulAnimMath.FlipXFor(s.BuoyX - transform.position.x, _artRopeSideIsLeft);

            int idx = pose switch
            {
                HaulPose.Pull => PlayerHaulAnimMath.CycleFrame(s.Line01, _cycleFramesPerLine, _cycleFrameCount),
                HaulPose.Strain => _strainFrame,
                _ => _easeFrame,
            };
            if (idx >= 0 && idx < _frames.Length && _frames[idx] != null)
                _renderer.sprite = _frames[idx];
            Pose = pose;
        }

        /// <summary>Hand the renderer back to the walk sprite (idempotent — safe on disable / repeat ends).</summary>
        private void EndHaul()
        {
            Pose = HaulPose.None;
            _hasLastLine = false;
            if (!_active) return;
            _active = false;
            if (_renderer == null) return;
            _renderer.sprite = _restoreSprite;
            _renderer.flipX = _restoreFlipX;
            _restoreSprite = null;
        }

        /// <summary>Wire the animator in one call (tests / editor builder): the sliced haul frames, and
        /// optionally a remapped cycle/strain/ease layout (negatives leave the serialized defaults).</summary>
        public void Configure(Sprite[] frames, int cycleFrameCount = -1, int strainFrame = -1, int easeFrame = -1,
                              float cycleFramesPerLine = -1f)
        {
            _frames = frames;
            if (cycleFrameCount > 0) _cycleFrameCount = cycleFrameCount;
            if (strainFrame >= 0) _strainFrame = strainFrame;
            if (easeFrame >= 0) _easeFrame = easeFrame;
            if (cycleFramesPerLine >= 0f) _cycleFramesPerLine = cycleFramesPerLine;
        }
    }
}
