using UnityEngine;
using UnityEngine.InputSystem;

namespace HiddenHarbours.Player
{
    /// <summary>4-way facing for the on-foot fisher — matches the FisherSheet row order.</summary>
    public enum Facing { Down = 0, Up = 1, Left = 2, Right = 3 }

    /// <summary>
    /// On-foot, top-down WASD walk (step 1 of the on-foot player; boarding/sailing is the next step).
    /// Drives a Rigidbody2D (gravityScale 0) from WASD/arrows and animates the sliced FisherSheet
    /// (3×4 of 32×64 px: rows Down/Up/Left/Right, cols idle/walk1/walk2) on a gentle [walk1, idle,
    /// walk2, idle] cycle (~230 ms/frame). Honest metric scale — the 32×64 frame is 1×2 m at PPU 32
    /// (~1.8 m of fisher within it); never rescaled. A footprint collider (added in the scene) plus the
    /// island's shore edge keep the player out of open water.
    ///
    /// Input is read here for the greybox (matching DevBoatInput/DevFishingInput, new Input System); a
    /// real InputService replaces it later (ui-ux). The movement/facing/animation logic is pure static
    /// helpers so it is fully unit-testable without the play-mode lifecycle.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(SpriteRenderer))]
    public class PlayerWalkController : MonoBehaviour
    {
        [Tooltip("Walk speed on foot (m/s).")]
        [SerializeField] private float _moveSpeed = 3f;

        [Tooltip("Seconds per animation frame (~230 ms — a gentle, readable walk).")]
        [SerializeField] private float _frameSeconds = 0.23f;

        [Tooltip("The 12 FisherSheet frames in slice order (_0.._11): rows Down/Up/Left/Right, cols " +
                 "idle/walk1/walk2. Wired by the greybox builder from the sliced sheet.")]
        [SerializeField] private Sprite[] _frames;

        // Walk cycle as COLUMN indices within a facing's 3 frames: walk1, idle, walk2, idle.
        private static readonly int[] WalkCycle = { 1, 0, 2, 0 };

        private Rigidbody2D _rb;
        private SpriteRenderer _renderer;
        private Facing _facing = Facing.Down;
        private Vector2 _moveInput;
        private float _animTimer;
        private int _animStep;

        public Facing CurrentFacing => _facing;

        // ---- pure logic (unit-testable) -----------------------------------------------------

        /// <summary>Desired top-down velocity for a move input (diagonals clamped so they're not faster).</summary>
        public static Vector2 VelocityFor(Vector2 moveInput, float speed)
            => Vector2.ClampMagnitude(moveInput, 1f) * Mathf.Max(0f, speed);

        /// <summary>Facing from the move direction; keeps the current facing when input is ~zero.</summary>
        public static Facing FacingFor(Vector2 moveInput, Facing current)
        {
            if (moveInput.sqrMagnitude < 0.0001f) return current;
            if (Mathf.Abs(moveInput.x) > Mathf.Abs(moveInput.y))
                return moveInput.x > 0f ? Facing.Right : Facing.Left;
            return moveInput.y > 0f ? Facing.Up : Facing.Down;
        }

        /// <summary>Sheet frame index for a facing + column (0 = idle, 1 = walk1, 2 = walk2).</summary>
        public static int FrameIndex(Facing facing, int column)
            => (int)facing * 3 + Mathf.Clamp(column, 0, 2);

        /// <summary>The column to show at a given step of the walk cycle (wraps; negative-safe).</summary>
        public static int WalkCycleColumn(int step)
            => WalkCycle[((step % WalkCycle.Length) + WalkCycle.Length) % WalkCycle.Length];

        // ---- lifecycle ----------------------------------------------------------------------

        private void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            _renderer = GetComponent<SpriteRenderer>();
            _rb.gravityScale = 0f;
            _rb.freezeRotation = true;
            _rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous; // don't tunnel the shore edge
            ApplyFrame(_facing, 0);
        }

        private void Update()
        {
            _moveInput = ReadInput();
            _facing = FacingFor(_moveInput, _facing);

            int column;
            if (_moveInput.sqrMagnitude > 0.0001f)
            {
                _animTimer += Time.deltaTime;
                while (_frameSeconds > 0f && _animTimer >= _frameSeconds)
                {
                    _animTimer -= _frameSeconds;
                    _animStep++;
                }
                column = WalkCycleColumn(_animStep);
            }
            else
            {
                _animTimer = 0f;
                _animStep = 0;
                column = 0; // idle frame when still
            }
            ApplyFrame(_facing, column);
        }

        private void FixedUpdate()
        {
            if (_rb != null) _rb.linearVelocity = VelocityFor(_moveInput, _moveSpeed);
        }

        private static Vector2 ReadInput()
        {
            var kb = Keyboard.current;
            if (kb == null) return Vector2.zero;
            Vector2 m = Vector2.zero;
            if (kb.wKey.isPressed || kb.upArrowKey.isPressed) m.y += 1f;
            if (kb.sKey.isPressed || kb.downArrowKey.isPressed) m.y -= 1f;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) m.x += 1f;
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) m.x -= 1f;
            return m;
        }

        private void ApplyFrame(Facing facing, int column)
        {
            if (_renderer == null || _frames == null) return;
            int idx = FrameIndex(facing, column);
            if (idx >= 0 && idx < _frames.Length && _frames[idx] != null)
                _renderer.sprite = _frames[idx];
        }
    }
}
