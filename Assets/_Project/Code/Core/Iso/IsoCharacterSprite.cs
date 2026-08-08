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
    /// <para><b>…and both reads can be OVERRIDDEN by whoever knows better</b> (<see cref="HoldHeading"/> /
    /// <see cref="HoldSpeed"/>). The local-position read is right in the two frames the player lives in, but
    /// it measures the frame's ROTATION as motion: a boat turning under a motionless fisher moves them in
    /// her frame with no step taken, so an un-overridden read would swing their facing round and march them
    /// on the spot. On a deck the <c>DeckRiderVisual</c> therefore states both — the facing composed from
    /// the deck bearing and the hull's heading, the speed measured in metres of DECK. Ashore nothing holds
    /// either and the motion read stands exactly as it always did.</para>
    ///
    /// <para><b>Two axes, not one.</b> Besides the gait it also carries a <see cref="Stance"/> — braced on
    /// a deck, at a helm, at the oars — and the def resolves the pair in one call
    /// (<see cref="CharacterVisualDef.Playable"/>). The default <see cref="CharacterStance.Free"/> routes
    /// straight back to the flat idle/walk/run fields, so a character with no stance art draws exactly what
    /// it drew before stances existed. <see cref="HoldHeading"/> and <see cref="HoldSpeed"/> cover the other
    /// half of the same story: a character standing on something that MOVES has no honest motion of its own
    /// to read, so whoever owns that frame supplies both the facing and the speed.</para>
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
        private CharacterStance _stance = CharacterStance.Free;
        private CharacterStance _drawnStance = CharacterStance.Free;
        private int _facingRow;
        private int _suspendCount;
        private Sprite _lastApplied;
        private bool _headingHeld;
        private float _heldHeadingDegrees;
        private bool _speedHeld;
        private float _heldSpeed;

        /// <summary>True when a complete skin is wired and this component is actually driving the renderer.
        /// Whoever else might write the sprite reads this to decide whether to stand down.</summary>
        public bool HasArt => _visual != null && _visual.HasAnyArt();

        /// <summary>The heading (degrees, 0 = North, CW) the character is currently drawn facing.</summary>
        public float HeadingDegrees => _headingDegrees;

        /// <summary>The direction ROW of the sheet currently showing. For tests / tooling.</summary>
        public int FacingRow => _facingRow;

        /// <summary>The gait currently playing (after the def's art-availability ladder). For tests / tooling.</summary>
        public CharacterGait Gait => _gait;

        /// <summary>
        /// <b>What the body is doing besides travelling</b> — braced on a deck, at a helm, at the oars, or
        /// free (the default, and byte-identical to the pre-stance presenter). Set by whoever knows the
        /// context: the <c>ControlSwitcher</c>'s deck rider on boarding, an NPC's routine later.
        ///
        /// <para>A stance is a REQUEST, not a guarantee: the def resolves it against what art is actually
        /// wired (<see cref="CharacterVisualDef.Playable"/>), so asking for a stance the kit never baked
        /// simply draws the free body. Read <see cref="DrawnStance"/> for what is really on screen.</para>
        /// </summary>
        public CharacterStance Stance
        {
            get => _stance;
            set => _stance = value;
        }

        /// <summary>The stance actually being DRAWN this frame, after the def's availability ladder —
        /// which may be <see cref="CharacterStance.Free"/> even while <see cref="Stance"/> asks for more.
        /// For tests / tooling.</summary>
        public CharacterStance DrawnStance => _drawnStance;

        /// <summary>True while another driver has claimed the renderer via <see cref="Suspend"/>.</summary>
        public bool IsSuspended => _suspendCount > 0;

        /// <summary>True while the facing is pinned by <see cref="HoldHeading"/> rather than following motion.</summary>
        public bool IsHeadingHeld => _headingHeld;

        /// <summary>
        /// PIN the facing to a compass heading and stop reading it off motion — for a character whose
        /// direction is decided by something other than where they are walking. The pilot at the helm is
        /// exactly that: they stand still while the hull turns under them, so motion says "no turn" and
        /// only the HULL knows which way they are looking.
        ///
        /// <para>Idempotent and cheap; call it every frame with the live heading. <see cref="ReleaseHeading"/>
        /// hands the facing back to motion, keeping whatever direction was last held (a character who steps
        /// back from the helm keeps looking where the boat was pointed rather than snapping to North).</para>
        /// </summary>
        public void HoldHeading(float headingDegrees)
        {
            _headingHeld = true;
            _heldHeadingDegrees = headingDegrees;
        }

        /// <summary>Give the facing back to motion (idempotent). The held direction is kept as the current
        /// one, so releasing never snaps the character round.</summary>
        public void ReleaseHeading()
        {
            if (!_headingHeld) return;
            _headingHeld = false;
            _headingDegrees = _heldHeadingDegrees;
        }

        /// <summary>True while the GAIT's speed is stated by <see cref="HoldSpeed"/> rather than measured.</summary>
        public bool IsSpeedHeld => _speedHeld;

        /// <summary>
        /// STATE the travelling speed (m/s) the gait should be chosen from, instead of measuring it — the
        /// twin of <see cref="HoldHeading"/>, and needed for the same reason. The local-position read
        /// measures the parent frame's ROTATION as motion, so a fisher standing on a turning deck (or a
        /// pilot standing at a turning helm) would be measured as walking and would draw a stride they are
        /// not taking. Whoever moves them in that frame knows the honest number: on a deck it is metres of
        /// DECK per second.
        ///
        /// <para>Taken as stated, with no smoothing: this IS the truth, not a sample of it. Idempotent and
        /// cheap; call it every frame. <see cref="ReleaseSpeed"/> hands the gait back to measurement,
        /// keeping the last stated value as the current one so the hand-back never pops a stride.</para>
        /// </summary>
        public void HoldSpeed(float metresPerSecond)
        {
            _speedHeld = true;
            _heldSpeed = Mathf.Max(0f, metresPerSecond);
        }

        /// <summary>Give the gait back to measured motion (idempotent), keeping the last stated speed.</summary>
        public void ReleaseSpeed()
        {
            if (!_speedHeld) return;
            _speedHeld = false;
            _speed = _heldSpeed;
        }

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
            // smoothed, because it is the thing compared against a threshold. A HELD heading overrides the
            // read entirely rather than blending with it: the pilot faces where the hull points, and a
            // drifting boat's sideways slip must not swing them round.
            _headingDegrees = _headingHeld
                ? _heldHeadingDegrees
                : IsoCharacterMath.HeadingFor(velocity, _headingMinSpeed, _headingDegrees);

            // A HELD speed replaces the read outright rather than being smoothed toward: it is a statement
            // of fact from whoever is moving the character, and filtering it would only add lag to a number
            // that has none. The local position is still tracked above, so releasing the hold never reads
            // the accumulated gap as one enormous stride.
            if (_speedHeld)
            {
                _speed = _heldSpeed;
            }
            else
            {
                float instant = velocity.magnitude;
                float k = _speedSmoothingSeconds > 1e-4f
                    ? 1f - Mathf.Exp(-dt / _speedSmoothingSeconds)
                    : 1f;
                _speed += (instant - _speed) * Mathf.Clamp01(k);
            }

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
            // ONE resolution call owns both ladders (stance first at the wanted gait, else the free body
            // down the run → walk → idle ladder). A def with no stance art always answers Free, so this is
            // the same answer the pre-stance presenter gave.
            CharacterPose pose = _visual.Playable(_stance, wanted);
            CharacterGait gait = pose.Gait;
            if (gait != _gait || pose.Stance != _drawnStance)
            {
                _gait = gait;
                _drawnStance = pose.Stance;
                _gaitElapsed = 0f;   // every gait OR stance change starts its cycle at frame 0 — no mid-stride pop-in
            }
            else
            {
                _gaitElapsed += Time.deltaTime;
            }

            int frame = IsoCharacterMath.FrameFor(_gaitElapsed,
                                                  _visual.FramesPerSecondFor(_drawnStance, gait),
                                                  _visual.FrameCountFor(_drawnStance, gait));
            Sprite cell = _visual.SpriteFor(_drawnStance, gait, _facingRow, frame);
            if (cell == null || ReferenceEquals(cell, _lastApplied)) return;

            // The 8 directions are all DRAWN — nothing here is a mirror, so any flip a previous 4-way,
            // mirrored sheet left on the renderer has to be cleared or every westward facing is inverted.
            _renderer.flipX = false;
            _renderer.sprite = cell;
            _lastApplied = cell;
        }
    }
}
