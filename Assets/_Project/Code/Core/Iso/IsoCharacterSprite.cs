using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// Draws a character from an 8-direction ¾-iso <see cref="CharacterVisualDef"/>: it picks the direction
    /// row that actually DEPICTS the way the character is travelling, picks idle / walk / run from how fast
    /// it is travelling, and advances that sheet's cycle. Drop it on any GameObject that has a
    /// <see cref="SpriteRenderer"/> and moves.
    ///
    /// <para><b>Zero coupling, by construction (rule 4).</b> It asks NOTHING of whatever moves the
    /// character — no interface to implement, no reference to wire, no <c>SendMessage</c>. It reads motion
    /// off its own <c>transform.localPosition</c>, which is the only reading that is right in both places
    /// the player exists: ashore (no parent — local == world) and ON DECK, where the ControlSwitcher parents
    /// the player to the boat's physics root and the deck controller writes position directly. Reading world
    /// motion instead would make the fisher stride on the spot every time the boat drifted underneath them.
    /// It is also what lets the SAME component serve the on-foot player (<c>HiddenHarbours.Player</c>) and,
    /// later, the harbour's NPCs (<c>world-content</c>) without either module referencing the other.</para>
    ///
    /// <para><b>The heading→row rule is not re-implemented here.</b> It is
    /// <see cref="CharacterVisualDef.FacingRowFor"/> → the shared, tested
    /// <see cref="IsoFacing.HeadingToFacingIndex"/>, carrying the def's own bake facts — including
    /// <see cref="CharacterVisualDef.FacingsAreCounterClockwise"/>, because the shipped character rigs bake
    /// their rows counter-clockwise while labelling them clockwise. That is DATA on the artwork, never a
    /// constant here: a corrected re-bake is an asset edit, not a code change.</para>
    ///
    /// <para><b>Facing is held when stopped</b> — a character that stops keeps looking where it was going
    /// rather than snapping to North. <b>Feet stay planted</b> — the sheets' pivot IS ground contact, so
    /// swapping frames never moves the character and this component never touches <c>transform</c>.</para>
    ///
    /// <para><b>Sharing the renderer.</b> Anything else that wants to drive the same
    /// <see cref="SpriteRenderer"/> for a while (the deck haul animation) calls <see cref="Suspend"/> and
    /// then <see cref="Release"/>; while suspended this component writes nothing at all, so two drivers can
    /// never fight over <c>SpriteRenderer.sprite</c>. It is a counted claim, so overlapping claimants are safe.</para>
    ///
    /// <para><b>Budget (rule 7).</b> No allocation per frame, no <c>GetComponent</c> after Awake, no LINQ:
    /// one struct subtraction, one array index and one sprite assignment — and the assignment is skipped
    /// when the cell hasn't changed.</para>
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class IsoCharacterSprite : MonoBehaviour
    {
        [Tooltip("The character's 8-direction iso skin — the sheets, the bake facts and the gait thresholds. " +
                 "Null (or a def with no idle sheet) leaves this component completely inert: it never writes " +
                 "to the renderer, so whatever drew the character before still draws it.")]
        [SerializeField] private CharacterVisualDef _visual;

        [Tooltip("Compass heading (degrees, 0 = North, CW) the character faces before it has ever moved. 180 " +
                 "= South, i.e. looking toward the camera — the friendliest way to meet a character.")]
        [SerializeField] private float _initialHeadingDegrees = 180f;

        [Tooltip("How quickly the measured speed settles, in seconds. A short filter so a collider nudge or " +
                 "one jittery physics step can't flicker the gait at a threshold; too long and the character " +
                 "keeps striding after it stops.")]
        [SerializeField, Min(0f)] private float _speedSmoothingSeconds = 0.08f;

        [Tooltip("Speed (m/s) below which motion is treated as noise and the FACING is held. Deliberately " +
                 "small and separate from the def's walk threshold: the gait may say 'idle' while the " +
                 "character is still turning, and the facing should follow that turn.")]
        [SerializeField, Min(0f)] private float _headingMinSpeed = 0.05f;

        private SpriteRenderer _renderer;
        private Vector3 _lastLocalPosition;
        private float _headingDegrees;
        private float _speed;
        private float _gaitElapsed;
        private CharacterGait _gait = CharacterGait.Idle;
        private int _facingRow;
        private int _suspendCount;
        private Sprite _lastApplied;

        /// <summary>True when a complete skin is wired and this component is actually driving the renderer.
        /// Whoever else might write the sprite reads this to decide whether to stand down.</summary>
        public bool HasArt => _visual != null && _visual.HasAnyArt();

        /// <summary>The heading (degrees, 0 = North, CW) the character is currently drawn facing.</summary>
        public float HeadingDegrees => _headingDegrees;

        /// <summary>The direction ROW of the sheet currently showing. For tests / tooling.</summary>
        public int FacingRow => _facingRow;

        /// <summary>The gait currently playing (after the def's art-availability ladder). For tests / tooling.</summary>
        public CharacterGait Gait => _gait;

        /// <summary>True while another driver has claimed the renderer via <see cref="Suspend"/>.</summary>
        public bool IsSuspended => _suspendCount > 0;

        /// <summary>Claim the renderer for another driver — this component stops writing until a matching
        /// <see cref="Release"/>. Counted, so overlapping claims nest safely.</summary>
        public void Suspend() => _suspendCount++;

        /// <summary>Give the renderer back (idempotent below zero). The character's own cell is re-asserted
        /// on the next frame, so the hand-back never leaves a stale picture.</summary>
        public void Release()
        {
            if (_suspendCount > 0) _suspendCount--;
            if (_suspendCount == 0) _lastApplied = null;   // force a re-assert next frame
        }

        /// <summary>Wire the skin in one call (the editor builder / tests) — the same seam
        /// <c>PlayerHaulAnimator.Configure</c> offers.</summary>
        public void Configure(CharacterVisualDef visual) => _visual = visual;

        /// <summary>The skin currently installed. For tests / tooling.</summary>
        public CharacterVisualDef Visual => _visual;

        private void Reset() => _renderer = GetComponent<SpriteRenderer>();

        private void Awake()
        {
            _renderer = GetComponent<SpriteRenderer>();
            _lastLocalPosition = transform.localPosition;
            _headingDegrees = _initialHeadingDegrees;
            if (_visual != null) _facingRow = _visual.FacingRowFor(_headingDegrees);
        }

        private void OnEnable()
        {
            // Don't let the gap since the last frame read as a teleport-sized stride.
            _lastLocalPosition = transform.localPosition;
            _speed = 0f;
            _lastApplied = null;
            Apply();
        }

        private void LateUpdate()
        {
            // LateUpdate so both movers have already run this frame: the walk controller's Rigidbody2D
            // (FixedUpdate) and the deck controller's direct transform write (Update).
            float dt = Time.deltaTime;
            Vector3 local = transform.localPosition;
            Vector2 delta = new Vector2(local.x - _lastLocalPosition.x, local.y - _lastLocalPosition.y);
            _lastLocalPosition = local;

            Vector2 velocity = dt > 1e-6f ? delta / dt : Vector2.zero;

            // Heading follows the RAW step (an averaged direction would lag round a corner); speed is
            // smoothed, because it is the thing compared against a threshold.
            _headingDegrees = IsoCharacterMath.HeadingFor(velocity, _headingMinSpeed, _headingDegrees);

            float instant = velocity.magnitude;
            float k = _speedSmoothingSeconds > 1e-4f
                ? 1f - Mathf.Exp(-dt / _speedSmoothingSeconds)
                : 1f;
            _speed += (instant - _speed) * Mathf.Clamp01(k);

            Apply();
        }

        /// <summary>Resolve the current row + gait + frame and push the cell (only when it changed).</summary>
        private void Apply()
        {
            if (_suspendCount > 0) return;
            if (_renderer == null || _visual == null || !_visual.HasAnyArt()) return;

            _facingRow = _visual.FacingRowFor(_headingDegrees);

            CharacterGait wanted = IsoCharacterMath.GaitFor(_speed, _visual.WalkSpeedThreshold,
                                                            _visual.RunSpeedThreshold);
            CharacterGait gait = _visual.PlayableGait(wanted);
            if (gait != _gait)
            {
                _gait = gait;
                _gaitElapsed = 0f;   // every gait change starts its cycle at frame 0 — no mid-stride pop-in
            }
            else
            {
                _gaitElapsed += Time.deltaTime;
            }

            int frame = IsoCharacterMath.FrameFor(_gaitElapsed, _visual.FramesPerSecondFor(gait),
                                                  _visual.FrameCountFor(gait));
            Sprite cell = _visual.SpriteFor(gait, _facingRow, frame);
            if (cell == null || ReferenceEquals(cell, _lastApplied)) return;

            // The 8 directions are all DRAWN — nothing here is a mirror, so any flip a previous 4-way,
            // mirrored sheet left on the renderer has to be cleared or every westward facing is inverted.
            _renderer.flipX = false;
            _renderer.sprite = cell;
            _lastApplied = cell;
        }
    }
}
