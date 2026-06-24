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
    /// <para><b>Future work (structured, NOT built).</b> The tie target is an <see cref="IMooringAnchor"/>
    /// (today: the player's hand, or a rooted ground point) so a dedicated <b>cleat / post / placed tie
    /// item</b> can later be a target, and a <b>second line</b> (bow + stern) can attach, without reworking
    /// the rope physics. See <c>MooringAnchor.cs</c> and design/boats-and-navigation.md §9.6.</para>
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

        // The tie target. While Held this tracks the player transform; while Rooted it's a fixed spot.
        private IMooringAnchor _anchor;

        public MooringState State { get; private set; } = MooringState.Stowed;

        /// <summary>Where the rope is made fast right now (the live anchor position) — only meaningful while
        /// moored. While Held this is the player's current position; while Rooted it's the fixed ground spot.</summary>
        public Vector2 TiePoint => _anchor != null ? _anchor.Position : Vector2.zero;
        public bool IsHeld   => State == MooringState.HeldByPlayer;
        public bool IsRooted => State == MooringState.RootedToGround;
        /// <summary>True while a rope should be drawn / a hold-root prompt is relevant (moored, either state).</summary>
        public bool IsMoored => State != MooringState.Stowed;

        public float RopeLength => _ropeLength;

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
            EnsureRefs();
            _anchor = new FixedAnchor(groundPoint);
            State = MooringState.RootedToGround;
            UpdateRopeVisual();
        }

        /// <summary>
        /// Stow the rope — the player has re-boarded (the helm takes over). The mooring goes dormant and the
        /// piloted <see cref="BoatController"/> drives again. Safe to call in any state.
        /// </summary>
        public void Stow()
        {
            State = MooringState.Stowed;
            _anchor = null;
            UpdateRopeVisual();
        }

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

            // --- The sea works on the idle hull: wind + tide drift (deterministic), held or rooted alike. ---
            // Reads the hull's own drag/windage stats (data, not code) — the same model the helm uses.
            Vector2 drift = DriftForce(_rb.linearVelocity, transform.up, env.WindVector, env.CurrentVector,
                                       hull.ForwardDrag, hull.LateralDrag, hull.WindExposure);
            _rb.AddForce(drift * _driftFeelScale, ForceMode2D.Force);

            // --- The rope: a one-sided FIRM tether checks her at the end of the rope (near-rigid, not springy). ---
            Vector2 tether = TetherForce(_rb.position, tie, _ropeLength,
                                         _limitStiffness, _rb.linearVelocity, _limitDamping, _ropeGive);
            if (tether != Vector2.zero) _rb.AddForce(tether * _driftFeelScale, ForceMode2D.Force);

            // --- Hard positional clamp (inextensible): she can NEVER sit past rope-length + give. ---
            Vector2 clamped = ConstrainToRope(_rb.position, tie, _ropeLength, _ropeGive);
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

            UpdateRopeVisual();
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
            _rope.sortingOrder = 50;   // above the water, under the HUD
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
        {
            int n = buffer.Length;
            if (n == 0) return;
            if (n == 1) { buffer[0] = boatPos; return; }

            float dist = (boatPos - tiePoint).magnitude;
            float slack = Slack01(dist, ropeLength);
            float sag = slack * Mathf.Max(0f, maxSag);

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
            bool show = IsMoored && _anchor != null;
            if (_rope.enabled != show) _rope.enabled = show;
            if (!show) { _rope.positionCount = 0; return; }

            Vector2 tie = _anchor.Position;
            Vector2 boat = transform.position;

            int n = Mathf.Max(2, _ropeSegments);
            if (_curveBuffer == null || _curveBuffer.Length != n) _curveBuffer = new Vector2[n];
            SampleRopeCurve(tie, boat, _ropeLength, _slackSagAmount, _curveBuffer);

            _rope.startWidth = _ropeWidth; _rope.endWidth = _ropeWidth;
            _rope.startColor = _ropeColor; _rope.endColor = _ropeColor;
            _rope.positionCount = n;
            for (int i = 0; i < n; i++)
                _rope.SetPosition(i, new Vector3(_curveBuffer[i].x, _curveBuffer[i].y, 0f));
        }
    }
}
