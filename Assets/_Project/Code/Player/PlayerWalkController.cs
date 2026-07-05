using UnityEngine;
using UnityEngine.InputSystem;
using HiddenHarbours.Core;

namespace HiddenHarbours.Player
{
    /// <summary>4-way facing for the on-foot fisher — matches the FisherSheet row order.</summary>
    public enum Facing { Down = 0, Up = 1, Left = 2, Right = 3 }

    /// <summary>
    /// On-foot, top-down WASD walk (step 1 of the on-foot player; boarding/sailing is the next step).
    /// Drives a Rigidbody2D (gravityScale 0) from WASD/arrows and animates the sliced FisherSheet
    /// (3×4 of 32×64 px: rows Down/Up/Side, cols idle/walk1/walk2) on a gentle [walk1, idle,
    /// walk2, idle] cycle (~230 ms/frame). Honest metric scale — the 32×64 frame is 1×2 m at PPU 32
    /// (~1.8 m of fisher within it); never rescaled. A footprint collider (added in the scene) plus the
    /// island's shore edge keep the player out of open water.
    ///
    /// <para><b>Three facings of art, four facings on screen.</b> The fisher art carries only THREE drawn
    /// rows — Down (row 0), Up (row 1) and ONE Side (row 2, drawn facing LEFT). The OTHER side is a
    /// guaranteed-matched MIRROR: Right reuses the same Side-row frames with <c>SpriteRenderer.flipX</c>.
    /// So only three facings ever need to be authored and the fourth can never drift from its mirror.
    /// (The sheet keeps the historical 12-cell/96×256 shape — row 3 is a harmless un-flipped copy of the
    /// Side row, never read here — so the FisherSheet asset and the scene builders stay byte-for-byte
    /// shaped like the classic and a revert is a one-file swap.) The flip mirrors about the cell's
    /// horizontal centre because the slices use a BottomCentre pivot, so the mirrored Right facing lands
    /// exactly where Left/Down/Up do — no sideways jump.</para>
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

        [Tooltip("Enforce the falling-tide walkability gate (St Peters): the fisher may only step onto " +
                 "ground exposed by the tide; a move into the water gently stops at the wading edge. " +
                 "Off-by-default so non-tidal land scenes (no terrain wired) need no change; the St " +
                 "Peters scene turns it on. Pure inland scenes can also just not register a TidalTerrain " +
                 "— the gate self-disables when no height map is present.")]
        [SerializeField] private bool _tideGatedWalk = false;

        [Tooltip("Optional shared GameConfig — the owner's wade tunables (WadeDepth / SwimLimit / " +
                 "WadeSlowFactor / SwimSlowFactor). When wired, its values override the serialized " +
                 "fallbacks below at Awake so there are no magic numbers in the build (rule 6). Left null " +
                 "in EditMode / non-tidal scenes the fallbacks apply (which mirror the config defaults).")]
        [SerializeField] private GameConfig _config;

        [Header("Wade model (owner-tunable; mirror GameConfig)")]
        [Tooltip("Deepest water still walkable on foot (m). Fallback for _config.WadeDepth.")]
        [SerializeField, Min(0f)] private float _wadeDepth = 0.5f;
        [Tooltip("Deepest water the player can move through on foot (m) — the escape-valve limit; deeper " +
                 "is boat-only (soft wall). Fallback for _config.SwimLimit.")]
        [SerializeField, Min(0f)] private float _swimLimit = 2.0f;
        [Tooltip("Move-speed multiplier at the deep edge of the wade band (0..1). Fallback for _config.WadeSlowFactor.")]
        [SerializeField, Range(0f, 1f)] private float _wadeSlowFactor = 0.6f;
        [Tooltip("Move-speed multiplier in the slow-swim band (0..1). Fallback for _config.SwimSlowFactor.")]
        [SerializeField, Range(0f, 1f)] private float _swimSlowFactor = 0.25f;

        [Tooltip("Seconds per animation frame (~230 ms — a gentle, readable walk).")]
        [SerializeField] private float _frameSeconds = 0.23f;

        [Tooltip("The FisherSheet frames in slice order (_0..): rows Down/Up/Side(+padding), cols " +
                 "idle/walk1/walk2. Wired by the greybox builder from the sliced sheet. Only the first " +
                 "three rows (9 frames) are read — Right mirrors the Side row via flipX.")]
        [SerializeField] private Sprite[] _frames;

        // Walk cycle as COLUMN indices within a facing's 3 frames: walk1, idle, walk2, idle.
        private static readonly int[] WalkCycle = { 1, 0, 2, 0 };

        private Rigidbody2D _rb;
        private SpriteRenderer _renderer;
        private Facing _facing = Facing.Down;
        private Vector2 _moveInput;
        private float _animTimer;
        private int _animStep;

        // On-foot water state — the last band we published, so the signal fires only on a genuine change.
        private OnFootWaterState _waterState = OnFootWaterState.Dry;
        private bool _waterStateInit;

        public Facing CurrentFacing => _facing;

        /// <summary>The player's current on-foot water state (Dry/Wade/Swim) — public so the HUD/VFX can
        /// read it directly as well as via the <see cref="OnFootWaterStateChanged"/> signal. Deep never
        /// occurs on foot (it is soft-walled off).</summary>
        public OnFootWaterState WaterState => _waterState;

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

        /// <summary>
        /// Which SHEET ROW a facing draws from. Down/Up have their own rows (0, 1); Left and Right BOTH
        /// draw the single Side row (2) — Right is the mirror (see <see cref="FlipXFor"/>). This is what
        /// lets the art carry only three facings while the screen shows four.
        /// </summary>
        public static int SheetRow(Facing facing) => facing switch
        {
            Facing.Down => 0,
            Facing.Up => 1,
            _ => 2, // Left and Right share the Side row; Right flips it.
        };

        /// <summary>True when the facing should be drawn mirrored (flipX). The Side art is drawn facing
        /// LEFT, so only Right is flipped — the matched mirror of the drawn side.</summary>
        public static bool FlipXFor(Facing facing) => facing == Facing.Right;

        /// <summary>Sheet frame index for a facing + column (0 = idle, 1 = walk1, 2 = walk2). Left and
        /// Right resolve to the SAME Side-row frames; Right is differentiated by <see cref="FlipXFor"/>.</summary>
        public static int FrameIndex(Facing facing, int column)
            => SheetRow(facing) * 3 + Mathf.Clamp(column, 0, 2);

        /// <summary>The column to show at a given step of the walk cycle (wraps; negative-safe).</summary>
        public static int WalkCycleColumn(int step)
            => WalkCycle[((step % WalkCycle.Length) + WalkCycle.Length) % WalkCycle.Length];

        /// <summary>
        /// Resolve a tide-gated move into a wading-edge stop (St Peters falling-tide walkability, P1).
        /// Given the would-be velocity and a per-axis "is the position a small step along this axis
        /// walkable?" probe, this zeroes any velocity component that would step the fisher off exposed
        /// ground into the water — a gentle stop at the wading edge, never a teleport. The X and Y axes
        /// are gated independently so the player can still slide ALONG a shoreline (only the into-water
        /// component is cut), which keeps the emerging-sandbar walk feeling smooth rather than sticky.
        ///
        /// <para>Pure + static so it is fully EditMode-testable with a fake walkability probe — the probe
        /// is "would a step of <paramref name="probeDistance"/> m in this direction land on exposed
        /// ground?". The controller supplies a probe backed by <see cref="TidalWalkability"/>.</para>
        /// </summary>
        /// <param name="desiredVelocity">The velocity the input wants this tick (m/s).</param>
        /// <param name="origin">The fisher's current world position.</param>
        /// <param name="walkableAt">Probe: is this world position currently walkable (exposed)?</param>
        /// <param name="probeDistance">How far ahead to test along each axis (m). A small look-ahead so we
        /// stop at the edge before the body slides into the water.</param>
        public static Vector2 ApplyWadingEdge(Vector2 desiredVelocity, Vector2 origin,
                                              System.Func<Vector2, bool> walkableAt, float probeDistance)
        {
            if (walkableAt == null) return desiredVelocity;

            // If the fisher isn't on exposed ground right now (e.g. the tide rose under them), don't trap
            // them: allow any move so they can wade back toward dry ground. The gate only blocks STEPPING
            // OFF exposed ground INTO water, never escaping water you're already in (P5 forgiving).
            if (!walkableAt(origin)) return desiredVelocity;

            Vector2 v = desiredVelocity;
            float look = Mathf.Max(0f, probeDistance);

            // Gate each axis independently so you can slide along the shoreline.
            if (v.x != 0f)
            {
                Vector2 probe = origin + new Vector2(Mathf.Sign(v.x) * look, 0f);
                if (!walkableAt(probe)) v.x = 0f;
            }
            if (v.y != 0f)
            {
                Vector2 probe = origin + new Vector2(0f, Mathf.Sign(v.y) * look);
                if (!walkableAt(probe)) v.y = 0f;
            }
            return v;
        }

        /// <summary>
        /// Resolve a move against the owner's three-band wade model (P1/P5), depth-aware. Unlike the binary
        /// <see cref="ApplyWadingEdge"/>, this reads the water <b>depth</b> along each axis so it can (a)
        /// soft-wall only the boat-only band (depth &gt; <paramref name="swimLimit"/>) while <em>allowing</em>
        /// wade + swim (depth ≤ swimLimit), and (b) scale the whole velocity by the depth at the fisher's
        /// feet via <see cref="TidalExposure.MoveScaleForDepth"/> (full on dry, heavier as it deepens).
        ///
        /// <para><b>Never trapped (P5).</b> If the fisher's ORIGIN is already deeper than the wade band
        /// (swim or deep — caught by a rising tide, or just disembarked), the boat-only wall is lifted for
        /// this tick so they can always move OUT toward shallower ground (they just move at the slow-swim
        /// crawl). The wall only ever blocks STEPPING FROM shallow-enough water INTO boat-only depth.</para>
        ///
        /// <para>Pure + static, fully EditMode-testable with a fake depth probe (depth in metres at a world
        /// position; ≤ 0 is dry). The controller supplies a probe backed by <see cref="TidalWalkability.DepthNow"/>.</para>
        /// </summary>
        /// <param name="desiredVelocity">The velocity the input wants this tick (m/s).</param>
        /// <param name="origin">The fisher's current world position.</param>
        /// <param name="depthAt">Probe: water depth (m) at a world position (≤ 0 = dry).</param>
        /// <param name="probeDistance">Look-ahead along each axis (m) for the soft wall.</param>
        /// <param name="wadeDepth">Deepest walkable-wade water (m).</param>
        /// <param name="swimLimit">Deepest on-foot water (m) — beyond is boat-only.</param>
        /// <param name="wadeSlowFactor">Speed multiplier at the deep edge of the wade band (0..1).</param>
        /// <param name="swimSlowFactor">Speed multiplier in the slow-swim band (0..1).</param>
        public static Vector2 ApplyWaterEdge(Vector2 desiredVelocity, Vector2 origin,
                                             System.Func<Vector2, float> depthAt, float probeDistance,
                                             float wadeDepth, float swimLimit,
                                             float wadeSlowFactor, float swimSlowFactor)
        {
            if (depthAt == null) return desiredVelocity;

            float originDepth = depthAt(origin);
            // Scale the whole move by how deep the fisher is standing (feel curve; full on dry ground).
            float scale = TidalExposure.MoveScaleForDepth(originDepth, wadeDepth, swimLimit, wadeSlowFactor, swimSlowFactor);
            Vector2 v = desiredVelocity * scale;

            // If the fisher is ALREADY beyond the wade band (swim/deep), never trap them: lift the wall so
            // any direction is allowed — they swim OUT (at the slow-swim crawl already applied above).
            bool alreadyBeyondWade = originDepth > wadeDepth;
            if (alreadyBeyondWade) return v;

            float look = Mathf.Max(0f, probeDistance);

            // Soft-wall the boat-only band (depth > swimLimit) per axis, so you can still slide along shore.
            if (v.x != 0f)
            {
                Vector2 probe = origin + new Vector2(Mathf.Sign(v.x) * look, 0f);
                if (depthAt(probe) > swimLimit) v.x = 0f;
            }
            if (v.y != 0f)
            {
                Vector2 probe = origin + new Vector2(0f, Mathf.Sign(v.y) * look);
                if (depthAt(probe) > swimLimit) v.y = 0f;
            }
            return v;
        }

        /// <summary>The on-foot <see cref="OnFootWaterState"/> for a <see cref="DepthBand"/> — Deep folds to
        /// Swim because the player is soft-walled out of the deep band, so on foot they are only ever
        /// Dry/Wade/Swim.</summary>
        public static OnFootWaterState StateForBand(DepthBand band) => band switch
        {
            DepthBand.Dry => OnFootWaterState.Dry,
            DepthBand.Wade => OnFootWaterState.Wade,
            _ => OnFootWaterState.Swim, // Swim and (soft-walled) Deep both read as "in the water, swim out".
        };

        // ---- lifecycle ----------------------------------------------------------------------

        private void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            _renderer = GetComponent<SpriteRenderer>();
            _rb.gravityScale = 0f;
            _rb.freezeRotation = true;
            _rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous; // don't tunnel the shore edge

            // Owner's wade tunables: prefer the shared GameConfig (no magic numbers, rule 6); the serialized
            // fields are the fallback (they mirror the config defaults) so EditMode/non-tidal scenes work unwired.
            if (_config != null)
            {
                _wadeDepth = _config.WadeDepth;
                _swimLimit = _config.SwimLimit;
                _wadeSlowFactor = _config.WadeSlowFactor;
                _swimSlowFactor = _config.SwimSlowFactor;
            }

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

        [Tooltip("Wading-edge look-ahead (m): how far ahead of the fisher the tide gate probes for exposed " +
                 "ground, so movement stops just before stepping into the water. A little more than one " +
                 "tick of travel keeps the stop clean without feeling sticky.")]
        [SerializeField] private float _wadeProbeDistance = 0.5f;

        private void FixedUpdate()
        {
            if (_rb == null) return;
            Vector2 desired = VelocityFor(_moveInput, _moveSpeed);

            // The owner's three-band wade model (St Peters, P1/P5): full speed on dry ground, slowed as you
            // wade, a crawl in the slow-swim escape band, and a soft wall at boat-only depth — but NEVER a
            // trap (already-deep → free to swim out). Depth-aware, per-axis so you slide along the shore.
            // Pure helper + a Core-seam depth probe (TidalWalkability.DepthNow), self-disabling where no
            // terrain/tide is wired (DepthNow returns -inf → dry → full speed, no wall).
            if (_tideGatedWalk)
            {
                desired = ApplyWaterEdge(desired, _rb.position, TidalWalkability.DepthNow, _wadeProbeDistance,
                                         _wadeDepth, _swimLimit, _wadeSlowFactor, _swimSlowFactor);
                UpdateWaterState(TidalWalkability.DepthNow(_rb.position));
            }

            _rb.linearVelocity = desired;
        }

        /// <summary>
        /// Track the on-foot water band at the fisher's feet and publish <see cref="OnFootWaterStateChanged"/>
        /// on a genuine transition (never per frame) — the single edge the HUD hooks for the canon "flood
        /// making — head in" warning and audio/VFX for a wade splash. Deep folds to Swim (soft-walled).
        /// </summary>
        private void UpdateWaterState(float depth)
        {
            var band = TidalExposure.BandForDepth(depth, _wadeDepth, _swimLimit);
            var next = StateForBand(band);
            if (_waterStateInit && next == _waterState) return;

            var prev = _waterState;
            _waterState = next;
            bool firstEdge = !_waterStateInit;
            _waterStateInit = true;
            if (firstEdge && next == OnFootWaterState.Dry) return; // don't announce "already dry" at spawn

            bool deepening = (int)next > (int)prev;
            EventBus.Publish(new OnFootWaterStateChanged(next, prev, deepening, Mathf.Max(0f, depth)));
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
            // Mirror the Side row for Right so the fourth facing is a guaranteed match of the drawn side.
            // (Set the flip even when the frame is missing so a half-wired sheet still faces correctly.)
            _renderer.flipX = FlipXFor(facing);
            int idx = FrameIndex(facing, column);
            if (idx >= 0 && idx < _frames.Length && _frames[idx] != null)
                _renderer.sprite = _frames[idx];
        }
    }
}
