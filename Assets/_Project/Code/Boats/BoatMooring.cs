using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Boats
{
    /// <summary>
    /// The state of a disembarked boat's rope (the mooring mechanic, P1 "the sea has moods" /
    /// P5 "cozy, but with teeth"). A boat is only ever moored when nobody's aboard — while you
    /// pilot it, the <see cref="BoatController"/> drives and this stays dormant.
    /// </summary>
    public enum MooringState
    {
        /// <summary>The boat is crewed/under way (or not yet disembarked). The rope is stowed; this does nothing.</summary>
        Stowed,
        /// <summary>Disembarked + TIED to shore. The boat still feels wind + tide (it bobs/swings on its
        /// leash) but a rope tether holds it within rope-length of the tie point — it can't float away.</summary>
        Tethered,
        /// <summary>Disembarked + cast off (untied). The boat DRIFTS FREE on wind + tide (the teeth) — it
        /// bumps shore, no destruction; re-tie or re-board to recover.</summary>
        AdriftUntied,
    }

    /// <summary>
    /// The rope / mooring mechanic — "tie up your boat so the sea doesn't take it" (owner spec; P1 + P5).
    /// Lives on the boat (Boats lane) and is dormant while the boat is crewed; it wakes when the player
    /// disembarks near shore (driven by the Player lane's <c>ControlSwitcher</c> via Core only — it never
    /// references the Player module).
    ///
    /// <list type="bullet">
    ///   <item><b>Tethered (tied).</b> On disembark near shore the rope auto-attaches and the boat is tied
    ///   to the shore spot by default (cozy — a quick hop-off never loses the boat). It still drifts on the
    ///   same deterministic wind + tidal-current force model the boat physics use, but is constrained within
    ///   <see cref="_ropeLength"/> of the tie point: it swings/bobs on its leash, and a soft one-sided rope
    ///   spring pulls it back when it reaches the end of the rope. It cannot float away.</item>
    ///   <item><b>Adrift / untied (the teeth).</b> Cast off with the dedicated key and the boat drifts free
    ///   on wind + tide — the deliberate consequence. Recoverable, not punishing: it bumps shore (the hull
    ///   collider), no damage, no stranding; re-tie or re-board to take her out again.</item>
    /// </list>
    ///
    /// <para><b>Determinism (CLAUDE.md rule 5).</b> Drift uses ONLY the deterministic
    /// <see cref="EnvironmentSample"/> (wind + current) read through the Core service — no hidden RNG. The
    /// tether is a pure physics constraint (a one-sided distance spring + damping), nothing saved. The
    /// constraint and drift math are pure static helpers so they're EditMode-testable without the physics
    /// loop. <b>No magic numbers</b>: rope length, stiffness, and snub damping are serialized owner-editable
    /// fields.</para>
    ///
    /// <para><b>Greybox visual.</b> A placeholder <see cref="LineRenderer"/> rope from the tie point toward
    /// the boat (drawn while moored). The FEEL — tethered-and-swinging vs drifting-away — is the point; the
    /// pretty rope is a later art pass.</para>
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class BoatMooring : MonoBehaviour
    {
        [Header("Rope tether (owner-editable feel)")]
        [Tooltip("How long the mooring rope is (m). While tied, the boat is free to bob/swing anywhere " +
                 "within this radius of the tie point; past it the rope goes taut and pulls her back. " +
                 "Bigger = more leash (she ranges further on wind/tide before the rope bites).")]
        [SerializeField] private float _ropeLength = 4f;
        [Tooltip("How firmly a taut rope pulls the boat back toward the tie point (spring stiffness, " +
                 "design-unit force per metre past rope-length). Higher = a snappier, shorter swing; lower = " +
                 "a softer, longer surge before she's checked. The rope only ever PULLS (never pushes), like " +
                 "a real line — inside rope-length it does nothing and the boat bobs free.")]
        [SerializeField] private float _tetherStiffness = 90f;
        [Tooltip("Snub damping on the taut rope (design-unit force per m/s of the boat's outward speed). A " +
                 "gentle shock-absorber so a boat surging onto the end of the rope eases to a stop instead " +
                 "of twanging back and forth forever. 0 = a bare spring (springy). Forgiving by default.")]
        [SerializeField] private float _tetherSnubDamping = 18f;

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

        private Rigidbody2D _rb;
        private BoatController _boat;
        private LineRenderer _rope;
        private Vector2 _tiePoint;

        public MooringState State { get; private set; } = MooringState.Stowed;

        /// <summary>Where the rope is made fast ashore (only meaningful while moored).</summary>
        public Vector2 TiePoint => _tiePoint;
        public bool IsTethered => State == MooringState.Tethered;
        public bool IsAdrift   => State == MooringState.AdriftUntied;
        /// <summary>True while a rope should be drawn / a tie-untie prompt is relevant (moored, either state).</summary>
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
        /// Make fast to shore at <paramref name="tiePoint"/> — the cozy default when you disembark near land
        /// (one press ties her). The boat will bob/swing on the rope but stay within rope-length, held by the
        /// tether against wind + tide. Idempotent: re-tying just updates the tie point (e.g. you stepped off
        /// somewhere new). Also clears any held helm input so an unmanned tethered boat sits quiet.
        /// </summary>
        public void TieTo(Vector2 tiePoint)
        {
            EnsureRefs();
            _tiePoint = tiePoint;
            State = MooringState.Tethered;
            if (_boat != null) _boat.Stop();   // drop velocity + held input; the rope keeps her here
            UpdateRopeVisual();
        }

        /// <summary>
        /// Cast off (the teeth): the rope is let go and the boat is left to DRIFT FREE on wind + tide. A
        /// deliberate choice with consequences — she sets off with the weather until you re-board or re-tie.
        /// Keeps the current velocity (a real cast-off doesn't stop the boat; the sea takes over).
        /// </summary>
        public void Untie()
        {
            State = MooringState.AdriftUntied;
            UpdateRopeVisual();
        }

        /// <summary>
        /// Stow the rope — the player has re-boarded (the helm takes over). The mooring goes dormant and the
        /// piloted <see cref="BoatController"/> drives again. Safe to call in any state.
        /// </summary>
        public void Stow()
        {
            State = MooringState.Stowed;
            UpdateRopeVisual();
        }

        /// <summary>
        /// Toggle tie ⇄ untie for the on-foot interaction. From Tethered → Untie (cast off, drift); from
        /// AdriftUntied → re-tie at <paramref name="tiePointIfTying"/> (make fast again). A no-op when stowed
        /// (you're aboard / not moored). Returns the new state so the UI can phrase its prompt.
        /// </summary>
        public MooringState ToggleTie(Vector2 tiePointIfTying)
        {
            switch (State)
            {
                case MooringState.Tethered:      Untie(); break;
                case MooringState.AdriftUntied:  TieTo(tiePointIfTying); break;
            }
            return State;
        }

        // ---- pure tether + drift math (EditMode-testable, no physics loop, no RNG) --------------------

        /// <summary>
        /// True when the boat sits beyond the end of the rope (its distance from the tie point exceeds
        /// <paramref name="ropeLength"/>) — i.e. the rope has gone taut and the tether is checking her.
        /// Inside rope-length the rope is slack and the boat bobs free. Pure + static.
        /// </summary>
        public static bool IsBeyondRope(Vector2 boatPos, Vector2 tiePoint, float ropeLength)
            => (boatPos - tiePoint).sqrMagnitude > ropeLength * ropeLength;

        /// <summary>
        /// The one-sided rope-tether force (design-unit, before the feel scale). A real rope only PULLS:
        /// <list type="bullet">
        ///   <item>Inside rope-length (slack) → <see cref="Vector2.zero"/>: the boat bobs/swings freely on
        ///   wind + tide within the circle.</item>
        ///   <item>At/beyond rope-length (taut) → a spring pulling the boat back toward the tie point,
        ///   proportional to how far past the rope it is (× <paramref name="stiffness"/>), PLUS a snub that
        ///   damps only the OUTWARD speed (× <paramref name="snubDamping"/>) so she eases onto the rope
        ///   instead of twanging. The snub never adds outward force (it can't fling the boat off the rope).</item>
        /// </list>
        /// The result is always non-positive along the outward radial — the rope can never push the boat
        /// away from the tie point. Pure + static so the "stays within rope length under force" guarantee is
        /// unit-testable.
        /// </summary>
        public static Vector2 TetherForce(Vector2 boatPos, Vector2 tiePoint, float ropeLength,
                                          float stiffness, Vector2 velocity, float snubDamping)
        {
            Vector2 toBoat = boatPos - tiePoint;
            float dist = toBoat.magnitude;
            if (dist <= ropeLength || dist < 1e-5f) return Vector2.zero;   // slack rope → no force
            Vector2 outward = toBoat / dist;                               // unit radial, away from the tie

            float overshoot = dist - ropeLength;
            Vector2 spring = -outward * (overshoot * Mathf.Max(0f, stiffness));

            // Snub only the OUTWARD component of velocity (surging away). Never pull inward past rest, and
            // never push outward (a rope can't shove) — so clamp the damped speed to outbound only.
            float outwardSpeed = Vector2.Dot(velocity, outward);
            Vector2 snub = outwardSpeed > 0f
                ? -outward * (outwardSpeed * Mathf.Max(0f, snubDamping))
                : Vector2.zero;

            return spring + snub;
        }

        /// <summary>
        /// The deterministic environmental DRIFT force on an unmanned hull (design-unit, before the feel
        /// scale): the boat floats in moving water (tidal current) and is shoved by wind — exactly the model
        /// <see cref="BoatController"/> applies with the helm let go (P1: an idle boat SETS with the weather).
        /// Hull drag is taken relative to the water (current), and anisotropic just like under way (it
        /// resists beam-on more than end-on). Pure + static (engine-light, no <c>Rigidbody2D</c>) so the
        /// "untied boat drifts" behaviour is EditMode-testable against a fake sample.
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
            if (_rb == null || hull == null) return;   // no rigidbody/hull stats → nothing to drift (no magic numbers)

            EnvironmentSample env = GameServices.Environment != null
                ? GameServices.Environment.Sample()
                : default;

            // --- The sea works on the idle hull: wind + tide drift (deterministic), same for tied/untied. ---
            // Reads the hull's own drag/windage stats (data, not code) — the same model the helm uses.
            Vector2 drift = DriftForce(_rb.linearVelocity, transform.up, env.WindVector, env.CurrentVector,
                                       hull.ForwardDrag, hull.LateralDrag, hull.WindExposure);
            _rb.AddForce(drift * _driftFeelScale, ForceMode2D.Force);

            // --- The rope (tied only): a one-sided tether holds her within rope-length of the tie point. ---
            if (State == MooringState.Tethered)
            {
                Vector2 tether = TetherForce(_rb.position, _tiePoint, _ropeLength,
                                             _tetherStiffness, _rb.linearVelocity, _tetherSnubDamping);
                if (tether != Vector2.zero) _rb.AddForce(tether * _driftFeelScale, ForceMode2D.Force);
            }

            UpdateRopeVisual();
        }

        // ---- greybox rope visual (placeholder LineRenderer) ------------------------------------------

        private void BuildRopeVisual()
        {
            var go = new GameObject("MooringRope");
            go.transform.SetParent(transform, false);
            _rope = go.AddComponent<LineRenderer>();
            _rope.useWorldSpace = true;
            _rope.positionCount = 2;
            _rope.numCapVertices = 2;
            _rope.startWidth = _ropeWidth;
            _rope.endWidth = _ropeWidth;
            _rope.material = new Material(Shader.Find("Sprites/Default"));
            _rope.startColor = _ropeColor;
            _rope.endColor = _ropeColor;
            _rope.sortingOrder = 50;   // above the water, under the HUD
            _rope.enabled = false;
        }

        private void UpdateRopeVisual()
        {
            if (_rope == null) return;
            bool show = IsMoored;
            if (_rope.enabled != show) _rope.enabled = show;
            if (!show) return;

            // Tied → a line from the shore tie point to the boat (taut/slack as she swings). Untied → a
            // short rope trailing off the bow into the water (cast off), a clear "she's loose" tell.
            _rope.startWidth = _ropeWidth; _rope.endWidth = _ropeWidth;
            if (State == MooringState.Tethered)
            {
                _rope.startColor = _ropeColor; _rope.endColor = _ropeColor;
                _rope.SetPosition(0, _tiePoint);
                _rope.SetPosition(1, transform.position);
            }
            else // AdriftUntied — a loose end dangling off the bow, dimmed, no shore anchor.
            {
                Color loose = new Color(_ropeColor.r, _ropeColor.g, _ropeColor.b, 0.55f);
                _rope.startColor = loose; _rope.endColor = loose;
                Vector3 bow = transform.position + transform.up * 0.5f;
                _rope.SetPosition(0, transform.position);
                _rope.SetPosition(1, bow - transform.up * Mathf.Min(1.5f, _ropeLength * 0.5f));
            }
        }
    }
}
