using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Boats
{
    /// <summary>
    /// The state of a disembarked boat's mooring line (the rope mechanic, P1 "the sea has moods" /
    /// P5 "cozy, but with teeth"). A boat is only ever moored when nobody's aboard — while you pilot it,
    /// the <see cref="BoatController"/> drives and this stays dormant.
    ///
    /// <para>The owner's refinement (replaces the old auto-tie-on-disembark): on disembark the player
    /// <b>holds</b> the rope in hand; pressing the root key <b>roots</b> it to the ground so they can roam;
    /// re-boarding stows it.</para>
    /// </summary>
    public enum MooringState
    {
        /// <summary>The boat is crewed/under way (or not yet disembarked). The rope is stowed; this does nothing.</summary>
        Stowed,
        /// <summary>Disembarked, rope IN HAND. The line is made fast to the player's own position, so the boat
        /// is tethered to the player and follows them on the leash as they move. It still feels wind + tide
        /// (it bobs/swings) but can never pull past rope-length of the player's hand.</summary>
        HeldByPlayer,
        /// <summary>Disembarked, rope ROOTED to a fixed ground spot. The boat is tethered to that point and the
        /// player is free to walk away; the boat still drifts on wind + tide but stays within rope-length of
        /// the rooted spot.</summary>
        RootedToGround,

        /// <summary>
        /// <b>MADE FAST between two cleats</b> (M2-38) — the seamanlike moor, as opposed to dropping the
        /// painter on the ground. The line runs from one of this hull's own rig cleats to a shore cleat,
        /// and the player chose its SCOPE. Unlike the two states above, this one is <b>tidal</b>: the
        /// shore end holds still while the boat's end rides the water, so a falling tide steadily eats the
        /// line's reach across the water and a line paid out too short will hang her and slip.
        /// </summary>
        MadeFastToCleat,
    }

    /// <summary>
    /// The rope / mooring mechanic — "tie up your boat so the sea doesn't take it" (owner spec; P1 + P5).
    /// Lives on the boat (Boats lane) and is dormant while the boat is crewed; it wakes when the player
    /// disembarks onto land (driven by the Player lane's <c>ControlSwitcher</c> via Core types only — it
    /// never references the Player module).
    ///
    /// <list type="bullet">
    ///   <item><b>Held (rope in hand).</b> On disembark the player holds the line: the boat is tethered to
    ///   the player's live position and trails them on the leash. A quick hop-off never loses the boat.</item>
    ///   <item><b>Rooted (made fast to the ground).</b> Press the root key and the line is dropped to a fixed
    ///   spot at the player's feet; the boat tethers there and the player roams free. Press again to take the
    ///   line back in hand.</item>
    /// </list>
    ///
    /// <para><b>It behaves like a ROPE, not a rubber band.</b> Inside rope-length the line is SLACK and does
    /// nothing — the boat moves freely on wind + tide (it bobs and swings). At the end of the rope it hits a
    /// FIRM, near-inextensible limit: a stiff constraint with only a tiny configurable give plus strong
    /// damping checks her almost rigidly, rather than a soft springy pull-back that grows with stretch. A
    /// position clamp guarantees she can never sit more than <see cref="_ropeGive"/> past the rope — the
    /// "inextensible" part. The greybox <see cref="LineRenderer"/> renders the slack rope as a drooping
    /// catenary that straightens and goes taut only at the limit.</para>
    ///
    /// <para><b>Determinism (CLAUDE.md rule 5).</b> Drift uses ONLY the deterministic
    /// <see cref="EnvironmentSample"/> (wind + current) read through the Core service — no hidden RNG. The
    /// tether is a pure physics constraint (firm limit + damping + a positional clamp), nothing saved. The
    /// constraint and drift math are pure static helpers so they're EditMode-testable without the physics
    /// loop. <b>No magic numbers</b>: rope length, the firm-limit give/stiffness/damping, and the slack-sag
    /// amount are serialized owner-editable fields.</para>
    ///
    /// <para><b>The third state: MADE FAST between two cleats</b> (M2-38, 2026-08-06 — the seam below
    /// predicted it and it landed exactly there). The line runs from one of this hull's own rig
    /// <c>CLEATS</c> to a shore cleat, with a SCOPE the player pays out or hauls in. Two things make it
    /// different in kind from the painter states above:
    /// <list type="bullet">
    ///   <item><b>It is tidal.</b> The shore end holds still; the boat's end floats. The vertical gap
    ///   between them grows as the water leaves, and every metre of that gap is a metre the line no longer
    ///   has to reach ACROSS the water (<see cref="MooringLineMath.HorizontalReach"/>). Leave her tight on
    ///   a falling tide and the reach collapses to nothing.</item>
    ///   <item><b>It can be lost.</b> Once the drop alone out-runs the whole scope she is hanging on the
    ///   rope, and past the working load the loop SLIPS — she goes quietly adrift, undamaged. No parted
    ///   rope, no damage: the cozy fail the backlog names, and the reason scope is a decision.</item>
    /// </list>
    /// The constraint itself is the very same firm tether + inextensible clamp below, handed a
    /// tide-derived effective length instead of a fixed one — the sim keeps computing and the rope
    /// restrains the result (rule 5), never freezing her.</para>
    ///
    /// <para><b>Still future work (structured, NOT built).</b> A <b>second line</b> (bow + stern) is a
    /// matter of holding two <see cref="BoatMooring"/>/anchor pairs rather than a new mechanic; a
    /// <b>winch</b> that pays out scope on the tide for you is P4 and much later. See
    /// <c>MooringAnchor.cs</c> and design/boats-and-navigation.md §9.6.</para>
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class BoatMooring : MonoBehaviour
    {
        [Header("Rope length (owner-editable feel)")]
        [Tooltip("How long the mooring rope is (m). While moored, the boat is free to bob/swing anywhere " +
                 "within this radius of the tie point; at the end the rope goes taut and checks her firmly. " +
                 "Bigger = more leash (she ranges further on wind/tide before the rope bites).")]
        [SerializeField] private float _ropeLength = 4f;

        [Header("Firm limit — a near-inextensible rope, not a rubber band")]
        [Tooltip("How much the rope is allowed to over-stretch past its length (m) before the firm limit " +
                 "snaps her back. SMALL by design (a near-rigid rope barely gives). The boat can never sit " +
                 "more than this far past rope-length — a hard positional clamp enforces it. 0 = perfectly " +
                 "inextensible (use a hair of give so it reads as a rope easing onto the limit, not a wall).")]
        [SerializeField] private float _ropeGive = 0.15f;
        [Tooltip("How firmly the taut rope checks the boat at the limit (design-unit force per metre past " +
                 "the allowed give). HIGH by design so the stop is near-rigid — the boat hits the end of the " +
                 "rope and is held, NOT pulled back softly in proportion to stretch. Higher = a harder, " +
                 "more rigid stop.")]
        [SerializeField] private float _limitStiffness = 1200f;
        [Tooltip("Damping on the rope at the limit (design-unit force per m/s of the boat's OUTWARD speed). " +
                 "Strong by default so a boat surging onto the end of the rope is arrested quickly and cleanly " +
                 "(near-critically damped) instead of bouncing. Only ever brakes outward motion — a rope " +
                 "can't shove the boat off its tie.")]
        [SerializeField] private float _limitDamping = 120f;

        [Header("Drift while unmanned (the deterministic wind/tide model)")]
        [Tooltip("Feel-scale that translates the hull's design-unit drag/windage stats into a good 2D-physics " +
                 "drift feel — matched to BoatController.ForceFeelScale so an unmanned boat sets with the " +
                 "weather exactly as a piloted one would with the helm let go.")]
        [SerializeField] private float _driftFeelScale = 0.01f;

        [Header("Greybox rope visual")]
        [Tooltip("Width of the placeholder rope line (m). Visual only.")]
        [SerializeField] private float _ropeWidth = 0.12f;
        [Tooltip("Colour of the placeholder rope line. Visual only.")]
        [SerializeField] private Color _ropeColor = new Color(0.78f, 0.65f, 0.42f, 1f);
        [Tooltip("How far a SLACK rope droops/sags at its slackest (m), drawn as a catenary belly between the " +
                 "tie point and the boat. Scales with how slack the rope is: full sag when she sits on top of " +
                 "the tie, none when the rope is taut at the limit. Visual only (a coiled/drooping line so a " +
                 "slack rope reads as slack, not a taut straight band).")]
        [SerializeField] private float _slackSagAmount = 0.8f;
        [Tooltip("How many segments the drooping rope is drawn with (more = a smoother sag curve). Visual only.")]
        [SerializeField] private int _ropeSegments = 12;

        private Rigidbody2D _rb;
        private BoatController _boat;
        private LineRenderer _rope;

        // The tie target. While Held this tracks the player transform; while Rooted it's a fixed spot;
        // while MadeFastToCleat it is the SHORE cleat (the end that holds still).
        private IMooringAnchor _anchor;

        // ---- made-fast-to-cleat state (M2-38) --------------------------------------------------------
        // The two ends of the line. The shore cleat is also what _anchor points at; the boat cleat is kept
        // separately because the constraint acts between the two FITTINGS, not between the wharf and the
        // hull's origin — a 13 m Cape Islander tied by the stern is held quite differently from one tied
        // by the bow, and that difference is the whole reason the rig exports named cleats.
        private IMooringCleat _boatCleat;
        private IMooringCleat _shoreCleat;
        // How much line is paid out. The player's choice, and the thing the tide tests.
        private float _scopeMetres;
        // How long the line has been over its working load — the slip grace (a single snatching wave must
        // not cast her off; a tide that has out-run the scope must).
        private float _overloadSeconds;
        // Last computed load, published on the slip beat and read by the taut/slack visual.
        private float _load01;

        public MooringState State { get; private set; } = MooringState.Stowed;

        /// <summary>Where the rope is made fast right now (the live anchor position) — only meaningful while
        /// moored. While Held this is the player's current position; while Rooted it's the fixed ground spot.</summary>
        public Vector2 TiePoint => _anchor != null ? _anchor.Position : Vector2.zero;
        public bool IsHeld   => State == MooringState.HeldByPlayer;
        public bool IsRooted => State == MooringState.RootedToGround;
        /// <summary>
        /// True while the PAINTER is out — held in hand or rooted to the ground — i.e. while the
        /// hold/root prompt is relevant. <b>Deliberately false for a line made fast to a cleat</b>: the
        /// root key has nothing to offer a properly moored boat (you work her at the cleat, with the rope
        /// verb), and reporting otherwise would put a prompt on screen that does nothing when pressed.
        /// Use <see cref="IsMadeFast"/> for the cleat moor and <see cref="HasLineOut"/> for "is there a
        /// rope to draw at all".
        /// </summary>
        public bool IsMoored => State == MooringState.HeldByPlayer || State == MooringState.RootedToGround;

        /// <summary>True whenever a rope of any kind should be drawn — painter or made-fast line.</summary>
        public bool HasLineOut => State != MooringState.Stowed;

        public float RopeLength => _ropeLength;

        // ---- made-fast reads (M2-38) ----------------------------------------------------------------

        /// <summary>True when a line is made fast between two cleats (the seamanlike moor).</summary>
        public bool IsMadeFast => State == MooringState.MadeFastToCleat;

        /// <summary>This hull's end of the line; null unless made fast.</summary>
        public IMooringCleat BoatCleat => _boatCleat;

        /// <summary>The shore end of the line; null unless made fast.</summary>
        public IMooringCleat ShoreCleat => _shoreCleat;

        /// <summary>How much line is paid out (m) — the player's scope choice.</summary>
        public float ScopeMetres => _scopeMetres;

        /// <summary>How hard the line is working right now: 0 slack, 1 bar-taut, &gt;1 overloaded. The live
        /// read the rope visual grades its taut/slack look off, and the creak-audio hook.</summary>
        public float Load01 => _load01;

        /// <summary>
        /// How much of the scope is still available to reach ACROSS the water, after the tide-driven
        /// vertical drop between the two cleats has taken its share. <b>The tide law's live value</b>
        /// (<see cref="MooringLineMath.HorizontalReach"/>): this shrinks as the water falls away from a
        /// wharf-height cleat, and it is what the constraint actually holds her inside of. 0 when the drop
        /// alone has eaten the whole line — she is hanging, and the loop is about to go.
        /// </summary>
        public float HorizontalReachMetres
            => IsMadeFast
                ? MooringLineMath.HorizontalReach(_scopeMetres, VerticalDropMetres)
                : 0f;

        /// <summary>The tide-driven vertical gap between the two cleats (m) — fixed shore end, floating
        /// boat end. 0 when no line is made fast.</summary>
        public float VerticalDropMetres
            => _boatCleat != null && _shoreCleat != null
                ? MooringLineMath.VerticalDrop(_boatCleat.ElevationMeters, _shoreCleat.ElevationMeters)
                : 0f;

        private void Awake()
        {
            EnsureRefs();
            BuildRopeVisual();
        }

        /// <summary>Resolve the sibling rigidbody/controller lazily so the transition methods work even
        /// before <see cref="Awake"/> has run (EditMode / first-tick wiring) — Unity doesn't call Awake on
        /// an AddComponent in edit mode, mirroring <see cref="BoatController.Stop"/>'s lazy lookup.</summary>
        private void EnsureRefs()
        {
            if (_rb == null) _rb = GetComponent<Rigidbody2D>();
            if (_boat == null) _boat = GetComponent<BoatController>();
        }

        // ---- mooring transitions (called by the Player lane's ControlSwitcher, via Core types only) ----

        /// <summary>
        /// Take the rope IN HAND — the default on disembark (the player holds the line). The boat is tethered
        /// to <paramref name="player"/>'s live position and trails them on the leash; it bobs/swings on wind +
        /// tide but stays within rope-length of the player's hand. Idempotent: re-holding just re-points the
        /// hand. Brings the boat to rest so an unmanned, just-disembarked boat sits quiet under the player's hand.
        /// </summary>
        public void Hold(Transform player)
        {
            // Already properly moored to a cleat? Then stepping ashore does not put the painter in your
            // hand — she is tied, and taking the line back is CastOff at the cleat (M2-38).
            if (State == MooringState.MadeFastToCleat) return;
            EnsureRefs();
            _anchor = new TransformAnchor(player);
            State = MooringState.HeldByPlayer;
            if (_boat != null) _boat.Stop();   // drop velocity + held input; the rope keeps her here
            UpdateRopeVisual();
        }

        /// <summary>
        /// ROOT the rope to a fixed ground spot — the player drops the line at <paramref name="groundPoint"/>
        /// (their feet) and is free to roam. The boat now tethers to that fixed point. Idempotent: re-rooting
        /// just updates the spot.
        /// </summary>
        public void Root(Vector2 groundPoint)
        {
            if (State == MooringState.MadeFastToCleat) return;   // she is on a cleat, not on the ground
            EnsureRefs();
            _anchor = new FixedAnchor(groundPoint);
            State = MooringState.RootedToGround;
            UpdateRopeVisual();
        }

        /// <summary>
        /// Stow the PAINTER — the player has re-boarded (the helm takes over). The held/rooted rope goes
        /// dormant and the piloted <see cref="BoatController"/> drives again. Safe to call in any state.
        ///
        /// <para><b>A line MADE FAST between cleats survives this, on purpose</b> (M2-38). Stowing is what
        /// happens to the painter in your hand when you step aboard; it is not untying. A boat properly
        /// moored to a wharf stays moored while you climb aboard and stow your gear — and casting off is
        /// then a deliberate act at the cleat (<see cref="CastOff"/>), which is the entire point of the
        /// verb. Handling it here rather than at the call sites means the control switcher needs no
        /// knowledge of the difference.</para>
        /// </summary>
        public void Stow()
        {
            if (State == MooringState.MadeFastToCleat) return;   // she is properly moored; leave her tied
            State = MooringState.Stowed;
            _anchor = null;
            ClearLine();
            UpdateRopeVisual();
        }

        // ---- made fast between two cleats (M2-38) ----------------------------------------------------

        /// <summary>
        /// <b>Make the line fast</b> between one of this hull's cleats and a shore cleat, with
        /// <paramref name="scopeMetres"/> of line paid out. The thrown loop caught; from here the sea keeps
        /// working her and the rope restrains her, within whatever reach the tide leaves that scope.
        ///
        /// <para>Returns false (and changes nothing) unless both ends are present and on OPPOSING sides —
        /// a line runs boat-to-shore, never boat-to-boat (rafting is out of scope) and never
        /// shore-to-shore.</para>
        /// </summary>
        public bool MakeFast(IMooringCleat boatCleat, IMooringCleat shoreCleat, float scopeMetres)
        {
            if (boatCleat == null || shoreCleat == null) return false;
            if (boatCleat.Side != CleatSide.Boat || shoreCleat.Side != CleatSide.Shore) return false;

            EnsureRefs();
            _boatCleat = boatCleat;
            _shoreCleat = shoreCleat;
            _scopeMetres = ClampScope(scopeMetres);
            _overloadSeconds = 0f;
            _anchor = new CleatAnchor(shoreCleat);        // the end that holds still
            State = MooringState.MadeFastToCleat;

            _load01 = ComputeLoad01();
            Publish(MooringLineEvent.MadeFast);
            UpdateRopeVisual();
            return true;
        }

        /// <summary>
        /// <b>Cast off</b> — the player lets the line go deliberately. She is free, and the sea has her.
        /// A no-op unless a line is actually made fast (so a stray key press near a cleat can't "untie" a
        /// boat that was never tied).
        /// </summary>
        public bool CastOff()
        {
            if (State != MooringState.MadeFastToCleat) return false;
            Publish(MooringLineEvent.CastOff);
            State = MooringState.Stowed;
            _anchor = null;
            ClearLine();
            UpdateRopeVisual();
            return true;
        }

        /// <summary>
        /// Tighten (negative <paramref name="steps"/>) or slacken (positive) the made-fast line by whole
        /// config steps. Returns the new scope. A no-op unless made fast.
        ///
        /// <para><b>This is the player's whole lever on the tide.</b> Slacken before an ebb and she rides
        /// it out; leave her tight and the falling water hangs her. Stepped so the choice is countable.</para>
        /// </summary>
        public float AdjustScope(int steps)
        {
            if (State != MooringState.MadeFastToCleat) return _scopeMetres;
            MooringLineSettings s = Settings;
            float next = MooringLineMath.StepScope(_scopeMetres, steps, s.ScopeStepMetres,
                                                   s.MinScopeMetres, s.MaxScopeMetres);
            if (Mathf.Approximately(next, _scopeMetres)) return _scopeMetres;   // already at a stop

            _scopeMetres = next;
            _overloadSeconds = 0f;      // a fresh judgement deserves a fresh grace period
            _load01 = ComputeLoad01();
            Publish(MooringLineEvent.ScopeChanged);
            UpdateRopeVisual();
            return _scopeMetres;
        }

        /// <summary>Set the scope directly (builders / tests / a future winch), clamped to the config's
        /// limits. A no-op unless made fast.</summary>
        public float SetScope(float scopeMetres)
        {
            if (State != MooringState.MadeFastToCleat) return _scopeMetres;
            _scopeMetres = ClampScope(scopeMetres);
            _overloadSeconds = 0f;
            _load01 = ComputeLoad01();
            Publish(MooringLineEvent.ScopeChanged);
            UpdateRopeVisual();
            return _scopeMetres;
        }

        /// <summary>The shared owner tuning, falling back to the reference values when no config is wired
        /// (EditMode / pre-bootstrap) — the established gate-off shape, never zeros (⚠ a YAML-omitted
        /// struct deserialises to C# defaults, which is why the fallback is <c>Default</c> and not
        /// <c>default</c>).</summary>
        private static MooringLineSettings Settings
            => GameServices.Config != null ? GameServices.Config.MooringLine : MooringLineSettings.Default;

        private static float ClampScope(float scope)
        {
            MooringLineSettings s = Settings;
            return Mathf.Clamp(float.IsNaN(scope) ? s.DefaultScopeMetres : scope,
                               Mathf.Max(0f, s.MinScopeMetres),
                               Mathf.Max(s.MinScopeMetres, s.MaxScopeMetres));
        }

        private void ClearLine()
        {
            _boatCleat = null;
            _shoreCleat = null;
            _scopeMetres = 0f;
            _overloadSeconds = 0f;
            _load01 = 0f;
        }

        /// <summary>The line's live load: the 3D span between the two cleats against the scope. 0 when no
        /// line is made fast.</summary>
        private float ComputeLoad01()
        {
            if (_boatCleat == null || _shoreCleat == null) return 0f;
            float span = MooringLineMath.Span(_boatCleat.WorldPosition, _boatCleat.ElevationMeters,
                                              _shoreCleat.WorldPosition, _shoreCleat.ElevationMeters);
            return MooringLineMath.Load01(span, _scopeMetres);
        }

        private void Publish(MooringLineEvent evt)
            => EventBus.Publish(new MooringLineChanged(
                   evt,
                   _boatCleat != null ? _boatCleat.Id : "",
                   _shoreCleat != null ? _shoreCleat.Id : "",
                   _scopeMetres, _load01));

        /// <summary>
        /// Toggle HOLD ⇄ ROOT for the on-foot interaction (the root key). From Held → Root the line at
        /// <paramref name="groundPointIfRooting"/> (drop it at the player's feet); from Rooted → take it back
        /// in hand (held by <paramref name="playerIfHolding"/>). A no-op when stowed (you're aboard / not
        /// moored). Returns the new state so the UI can phrase its prompt.
        /// </summary>
        public MooringState ToggleRoot(Vector2 groundPointIfRooting, Transform playerIfHolding)
        {
            switch (State)
            {
                case MooringState.HeldByPlayer:   Root(groundPointIfRooting); break;
                case MooringState.RootedToGround: Hold(playerIfHolding); break;
            }
            return State;
        }

        // ---- pure tether + drift math (EditMode-testable, no physics loop, no RNG) --------------------

        /// <summary>
        /// True when the boat sits beyond the end of the rope (its distance from the tie point exceeds
        /// <paramref name="ropeLength"/>) — i.e. the rope has gone taut and the firm limit is checking her.
        /// Inside rope-length the rope is slack and the boat bobs free. Pure + static.
        /// </summary>
        public static bool IsBeyondRope(Vector2 boatPos, Vector2 tiePoint, float ropeLength)
            => (boatPos - tiePoint).sqrMagnitude > ropeLength * ropeLength;

        /// <summary>
        /// The FIRM-LIMIT rope force (design-unit, before the feel scale) — a near-inextensible rope, NOT a
        /// rubber band. A real rope only PULLS, and a taut rope barely gives:
        /// <list type="bullet">
        ///   <item>Inside rope-length, OR within the small <paramref name="give"/> past it (slack / the rope's
        ///   tiny stretch) → <see cref="Vector2.zero"/>: the boat bobs/swings freely.</item>
        ///   <item>Past rope-length + give (the rope is genuinely taut and over-stretched) → a STIFF restoring
        ///   force on only the excess past the allowed give (× <paramref name="limitStiffness"/>, high), PLUS
        ///   strong damping on only the OUTWARD speed (× <paramref name="limitDamping"/>) so she is arrested
        ///   cleanly at the limit instead of springing back. The damping never adds outward force (a rope
        ///   can't shove).</item>
        /// </list>
        /// Because the stiffness acts only on the excess past <c>ropeLength + give</c> (not the whole stretch)
        /// and is large, the boat is held essentially AT the end of the rope — a firm stop, not a soft
        /// proportional pull. The result is always non-positive along the outward radial. Pure + static so the
        /// firm-limit guarantee is unit-testable.
        /// </summary>
        public static Vector2 TetherForce(Vector2 boatPos, Vector2 tiePoint, float ropeLength,
                                          float limitStiffness, Vector2 velocity, float limitDamping,
                                          float give)
        {
            Vector2 toBoat = boatPos - tiePoint;
            float dist = toBoat.magnitude;
            float limit = ropeLength + Mathf.Max(0f, give);
            if (dist <= limit || dist < 1e-5f) return Vector2.zero;   // slack / within the rope's tiny give
            Vector2 outward = toBoat / dist;                          // unit radial, away from the tie

            float excess = dist - limit;                              // only the over-stretch past the give
            Vector2 spring = -outward * (excess * Mathf.Max(0f, limitStiffness));

            // Damp only the OUTWARD component of velocity (surging away). Never pull inward past rest, and
            // never push outward (a rope can't shove) — so clamp the damped speed to outbound only.
            float outwardSpeed = Vector2.Dot(velocity, outward);
            Vector2 damp = outwardSpeed > 0f
                ? -outward * (outwardSpeed * Mathf.Max(0f, limitDamping))
                : Vector2.zero;

            return spring + damp;
        }

        /// <summary>
        /// The hard positional clamp that makes the rope INEXTENSIBLE: if the boat has been pushed past
        /// <c>ropeLength + give</c> this returns the corrected position pulled back onto the limit circle
        /// (same bearing from the tie point, distance = <c>ropeLength + give</c>); otherwise the position is
        /// returned unchanged. This is the "near-rigid stop" guarantee — even a violent shove can't stretch
        /// the rope past its give. Pure + static so it's unit-testable.
        /// </summary>
        public static Vector2 ConstrainToRope(Vector2 boatPos, Vector2 tiePoint, float ropeLength, float give)
        {
            Vector2 toBoat = boatPos - tiePoint;
            float dist = toBoat.magnitude;
            float limit = ropeLength + Mathf.Max(0f, give);
            if (dist <= limit || dist < 1e-5f) return boatPos;
            return tiePoint + toBoat / dist * limit;
        }

        /// <summary>
        /// The deterministic environmental DRIFT force on an unmanned hull (design-unit, before the feel
        /// scale): the boat floats in moving water (tidal current) and is shoved by wind — exactly the model
        /// <see cref="BoatController"/> applies with the helm let go (P1: an idle boat SETS with the weather).
        /// Hull drag is taken relative to the water (current), and anisotropic just like under way (it
        /// resists beam-on more than end-on). Pure + static (engine-light, no <c>Rigidbody2D</c>) so the
        /// "moored boat drifts on its leash" behaviour is EditMode-testable against a fake sample.
        /// </summary>
        /// <param name="velocity">Hull velocity (m/s).</param>
        /// <param name="forward">Hull bow direction (unit-ish).</param>
        /// <param name="wind">Wind vector (m/s) from the environment sample.</param>
        /// <param name="current">Tidal current vector (m/s) from the environment sample.</param>
        /// <param name="forwardDrag">Hull end-on drag stat.</param>
        /// <param name="lateralDrag">Hull beam-on drag stat (&gt; forwardDrag → tracks, skids reluctantly).</param>
        /// <param name="windExposure">Hull windage stat (small boats high, big ships low).</param>
        public static Vector2 DriftForce(Vector2 velocity, Vector2 forward, Vector2 wind, Vector2 current,
                                         float forwardDrag, float lateralDrag, float windExposure)
        {
            Vector2 fwd = forward.sqrMagnitude > 1e-6f ? forward.normalized : Vector2.up;
            Vector2 throughWater = velocity - current;
            Vector2 along = fwd * Vector2.Dot(throughWater, fwd);
            Vector2 sideways = throughWater - along;
            Vector2 drag = -(along * forwardDrag + sideways * lateralDrag);
            Vector2 windShove = wind * windExposure;
            return drag + windShove;
        }

        // ---- per-tick physics (only while unmanned/moored) -------------------------------------------

        private void FixedUpdate()
        {
            if (State == MooringState.Stowed) return;
            EnsureRefs();
            BoatHullDef hull = _boat != null ? _boat.Hull : null;
            if (_rb == null || hull == null || _anchor == null) return;   // nothing to drift/tether against

            EnvironmentSample env = GameServices.Environment != null
                ? GameServices.Environment.Sample()
                : default;

            Vector2 tie = _anchor.Position;

            // --- The sea works on the idle hull: wind + tide drift (deterministic), in every moored state. ---
            // Reads the hull's own drag/windage stats (data, not code) — the same model the helm uses.
            // NOTE the ordering that matters for M2-38: the drift is applied FIRST and unconditionally.
            // The sim keeps computing and the rope is a RESTRAINT on the result, never a freeze (rule 5).
            Vector2 drift = DriftForce(_rb.linearVelocity, transform.up, env.WindVector, env.CurrentVector,
                                       hull.ForwardDrag, hull.LateralDrag, hull.WindExposure);
            _rb.AddForce(drift * _driftFeelScale, ForceMode2D.Force);

            // --- What the rope can actually reach, and from where. ------------------------------------
            // HELD/ROOTED: the painter runs from the hull's origin to the player's hand or a ground spot,
            // and its whole length lies flat on the water — reach IS rope length.
            //
            // MADE FAST: the line runs between two FITTINGS, and it has to climb the gap between them
            // first. So (a) the effective reach is what the tide leaves of the scope
            // (MooringLineMath.HorizontalReach — the one place that law is written), and (b) the circle is
            // centred so that the BOAT'S CLEAT, not her origin, is the end being held. Shifting the tie
            // by the cleat's own offset is exactly equivalent to constraining the cleat point, and it
            // lets the tested tether/clamp helpers below stay untouched.
            float reach = _ropeLength;
            bool hanging = false;
            if (State == MooringState.MadeFastToCleat)
            {
                if (_boatCleat == null || _shoreCleat == null) { CastOff(); return; }
                reach = HorizontalReachMetres;
                tie -= _boatCleat.WorldPosition - _rb.position;   // centre on the hull, hold the cleat
                // The drop alone has eaten the whole line: there is no horizontal reach left and she is
                // HANGING on the rope rather than swinging on it.
                hanging = reach <= 0f;
            }

            // --- The rope: a one-sided FIRM tether checks her at the end of the rope (near-rigid, not springy). ---
            Vector2 tether = TetherForce(_rb.position, tie, reach,
                                         _limitStiffness, _rb.linearVelocity, _limitDamping, _ropeGive);
            if (tether != Vector2.zero) _rb.AddForce(tether * _driftFeelScale, ForceMode2D.Force);

            // --- Hard positional clamp (inextensible): she can NEVER sit past rope-length + give. -------
            // SKIPPED while hanging, and that exception is the whole difference between a mooring that
            // reads and one that snaps. The clamp expresses "this rope does not stretch", which is only
            // meaningful while the rope can still REACH: with a reach of zero the clamp would teleport a
            // 13 m hull onto the bollard the instant a falling tide crossed the threshold. What actually
            // happens to a hung boat is that the line comes up hard and HAULS her in — which is exactly
            // the firm tether force above, finite and visible — until the loop lets go a moment later.
            if (!hanging)
            {
                Vector2 clamped = ConstrainToRope(_rb.position, tie, reach, _ropeGive);
                if (clamped != _rb.position)
                {
                    _rb.position = clamped;
                    // Kill the outward radial velocity so the clamp doesn't fight the integrator next tick.
                    Vector2 outward = clamped - tie;
                    if (outward.sqrMagnitude > 1e-6f)
                    {
                        outward.Normalize();
                        float outwardSpeed = Vector2.Dot(_rb.linearVelocity, outward);
                        if (outwardSpeed > 0f) _rb.linearVelocity -= outward * outwardSpeed;
                    }
                }
            }

            // --- THE COZY FAIL: a line worked past its working load long enough loses the loop. --------
            if (State == MooringState.MadeFastToCleat && TickSlip(Time.fixedDeltaTime)) return;

            UpdateRopeVisual();
        }

        /// <summary>
        /// Grade the made-fast line's load and, if it has been over its working load for longer than the
        /// grace period, <b>slip the loop</b>: she goes quietly adrift, undamaged, and the player coils the
        /// line and tries again. Returns true when she slipped (the caller stops touching the line).
        ///
        /// <para><b>When this actually fires.</b> While the drop between the two cleats is smaller than the
        /// scope, the clamp above holds her inside the reach circle and the 3D span can never exceed the
        /// scope — the line simply restrains her, which is the whole point. It is when the TIDE has opened
        /// the two cleats further apart VERTICALLY than the whole line is long that the span overruns the
        /// scope no matter where she sits: she is hanging on the rope, and the loop gives up. That is
        /// exactly the seamanship P1 is teaching — too short a line for the tide you left her in.</para>
        ///
        /// <para>The grace period is why a single snatching wave is not a lost boat: the overload has to
        /// be SUSTAINED, and a tide that has out-run the scope sustains it.</para>
        /// </summary>
        private bool TickSlip(float dt)
        {
            MooringLineSettings s = Settings;
            _load01 = ComputeLoad01();

            if (_load01 <= Mathf.Max(1f, s.WorkingLoadFactor))
            {
                _overloadSeconds = 0f;
                return false;
            }

            _overloadSeconds += Mathf.Max(0f, dt);
            if (_overloadSeconds < Mathf.Max(0f, s.SlipGraceSeconds)) return false;

            Publish(MooringLineEvent.Slipped);
            State = MooringState.Stowed;
            _anchor = null;
            ClearLine();
            UpdateRopeVisual();
            return true;
        }

        // ---- greybox rope visual (placeholder LineRenderer; slack = drooping catenary, taut = straight) ----

        private void BuildRopeVisual()
        {
            var go = new GameObject("MooringRope");
            go.transform.SetParent(transform, false);
            _rope = go.AddComponent<LineRenderer>();
            _rope.useWorldSpace = true;
            _rope.numCapVertices = 2;
            _rope.startWidth = _ropeWidth;
            _rope.endWidth = _ropeWidth;
            _rope.material = new Material(Shader.Find("Sprites/Default"));
            _rope.startColor = _ropeColor;
            _rope.endColor = _ropeColor;
            // Above ALL decor whatever its Y — a mooring line reads as tied ON TOP of the wharf it crosses.
            // Was a literal 50, which cleared the decor band's old ceiling of 40; the band now reaches 2402
            // (it used to resolve 9.5 m of a 520 m region — ADR 0032), so this follows the band by name.
            _rope.sortingOrder = SortingBands.AboveDecor;
            _rope.positionCount = 0;
            _rope.enabled = false;
        }

        /// <summary>
        /// How slack the rope is, 0 (taut at the limit) → 1 (fully slack, boat on top of the tie). Pure +
        /// static so the catenary-belly visual and any future "rope is taut/slack" tells are testable.
        /// </summary>
        public static float Slack01(float distance, float ropeLength)
        {
            if (ropeLength <= 1e-5f) return 0f;
            return Mathf.Clamp01(1f - distance / ropeLength);
        }

        /// <summary>
        /// Sample the drooping rope (a catenary-ish belly) between <paramref name="tiePoint"/> and
        /// <paramref name="boatPos"/> into <paramref name="buffer"/>. The belly sags by
        /// <c>slack01 * maxSag</c> at the rope's midpoint and tapers to zero at both ends, so a slack rope
        /// reads as a drooping/coiled line and a taut rope as a straight one. Sag droops straight down (−y).
        /// Pure + static (writes a caller-owned buffer; no allocation) so the curve is unit-testable.
        /// </summary>
        public static void SampleRopeCurve(Vector2 tiePoint, Vector2 boatPos, float ropeLength,
                                           float maxSag, Vector2[] buffer)
            => SampleRopeCurveBySlack(tiePoint, boatPos, Slack01((boatPos - tiePoint).magnitude, ropeLength),
                                      maxSag, buffer);

        /// <summary>
        /// The same drooping curve, but told HOW SLACK the rope is rather than re-deriving it from a
        /// distance. The made-fast line needs this: its slackness is <c>1 − Load01</c> against a scope the
        /// tide is eating, which a flat screen-distance cannot see. One belly function, two ways of
        /// knowing the slack — never two bellies. Pure + static; writes a caller-owned buffer (no alloc).
        /// </summary>
        /// <param name="slack01">0 = bar-taut (draw it straight), 1 = fully slack (full belly).</param>
        public static void SampleRopeCurveBySlack(Vector2 tiePoint, Vector2 boatPos, float slack01,
                                                  float maxSag, Vector2[] buffer)
        {
            int n = buffer.Length;
            if (n == 0) return;
            if (n == 1) { buffer[0] = boatPos; return; }

            float sag = Mathf.Clamp01(float.IsNaN(slack01) ? 0f : slack01) * Mathf.Max(0f, maxSag);

            for (int i = 0; i < n; i++)
            {
                float t = i / (float)(n - 1);
                Vector2 p = Vector2.Lerp(tiePoint, boatPos, t);
                // Belly: a parabola peaking at the midpoint (4 t (1-t)), drooping straight down (−y).
                float belly = sag * (4f * t * (1f - t));
                p += Vector2.down * belly;
                buffer[i] = p;
            }
        }

        private Vector2[] _curveBuffer;

        private void UpdateRopeVisual()
        {
            if (_rope == null) return;
            bool show = HasLineOut && _anchor != null;
            if (_rope.enabled != show) _rope.enabled = show;
            if (!show) { _rope.positionCount = 0; return; }

            Vector2 tie = _anchor.Position;
            Vector2 boat = transform.position;

            // A MADE-FAST line is drawn between the two FITTINGS — from the hull's own cleat to the
            // bollard, not from the middle of the boat to the middle of the wharf. And its sag is graded
            // by the SAME load the physics is grading (MooringLineMath.Load01, via _load01) rather than by
            // a second distance measurement, so a rope that LOOKS bar-taut is a rope that IS: the
            // never-compute-one-quantity-two-ways rule the flick-cast preview already lives under.
            float slack;
            if (State == MooringState.MadeFastToCleat && _boatCleat != null && _shoreCleat != null)
            {
                boat = _boatCleat.WorldPosition;
                tie = _shoreCleat.WorldPosition;
                slack = Mathf.Clamp01(1f - _load01);
            }
            else
            {
                slack = Slack01((boat - tie).magnitude, _ropeLength);
            }

            int n = Mathf.Max(2, _ropeSegments);
            if (_curveBuffer == null || _curveBuffer.Length != n) _curveBuffer = new Vector2[n];
            SampleRopeCurveBySlack(tie, boat, slack, _slackSagAmount, _curveBuffer);

            _rope.startWidth = _ropeWidth; _rope.endWidth = _ropeWidth;
            _rope.startColor = _ropeColor; _rope.endColor = _ropeColor;
            _rope.positionCount = n;
            for (int i = 0; i < n; i++)
                _rope.SetPosition(i, new Vector3(_curveBuffer[i].x, _curveBuffer[i].y, 0f));
        }
    }
}
