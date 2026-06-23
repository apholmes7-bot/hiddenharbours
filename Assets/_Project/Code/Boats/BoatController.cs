using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Boats
{
    /// <summary>
    /// The dory's feel (Pillar 1). Each physics tick it reads the live <see cref="EnvironmentSample"/>
    /// (via the Core service contract — Boats never references the Environment module directly) and
    /// applies engine, rudder, hull drag relative to the water, wind shove, and a grounding check
    /// against tide. Built on Unity 6's Box2D-v3 2D physics. See design/boats-and-navigation.md.
    ///
    /// Greybox tuning note: the <see cref="ForceFeelScale"/>/<see cref="RudderFeelScale"/> constants
    /// translate the design-unit stats on the hull into a good 2D-physics feel; tune to taste. Real input
    /// arrives via DevBoatInput for now and an InputService later (ui-ux).
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(CapsuleCollider2D))]   // hull collider: bumps shore + dock pilings (cozy, no damage)
    public class BoatController : MonoBehaviour
    {
        /// <summary>Astern thrust as a fraction of ahead, like a real prop pushes backward weaker (~40%).</summary>
        public const float DefaultAsternFactor = 0.4f;

        /// <summary>
        /// Greybox feel-scale that translates the hulls' design-unit FORCE stats (EnginePower, drag,
        /// windage, oar power) into a good 2D-physics feel. Shared by every force path so they stay in
        /// proportion to one another.
        /// </summary>
        private const float ForceFeelScale = 0.01f;

        /// <summary>
        /// Greybox feel-scale for the engine RUDDER torque. Matched to <see cref="ForceFeelScale"/> so the
        /// outboard's speed-scaled rudder has turning authority comparable to the Dory's differential-oar
        /// yaw on the same hull — i.e. the Punt actually answers the helm while making way (the §2 outboard
        /// turn), instead of the imperceptible swing the older 10×-smaller scale gave the larger hull. The
        /// "no pivot dead in the water" feel is unchanged: it lives in <see cref="RudderTorque"/>'s
        /// way-scaling, not here. Tune to taste.
        /// </summary>
        private const float RudderFeelScale = 0.01f;

        [SerializeField] private BoatHullDef _hull;
        [Tooltip("Placeholder local seabed depth (m). Replaced by a real seabed/depth map later.")]
        [SerializeField] private float _localSeabedDepth = 3f;
        [Tooltip("Gate crossing on the authored tidal terrain (St Peters): the boat can only pass where " +
                 "the water is deeper than its draught, so the sandbar channel closes as the tide falls. " +
                 "Non-punishing — the hull eases to a stop at the shallows, no grounding/damage. " +
                 "Off-by-default; self-disables anyway where no TidalTerrain is wired (open water).")]
        [SerializeField] private bool _tideGatedCrossing = false;
        [Tooltip("How firmly the boat is held out of water too shallow to float it (design-unit drag " +
                 "against the into-shallows velocity). Forgiving: it stops you entering, it doesn't punish.")]
        [SerializeField] private float _shallowsHoldDrag = 600f;
        [Tooltip("Astern thrust as a fraction of ahead thrust. A real propeller pushes backward weaker " +
                 "(~40%) — enough to back off the dock without turning the dory into a reversing car.")]
        [SerializeField] private float _asternThrustFactor = DefaultAsternFactor;

        private Rigidbody2D _rb;
        private float _throttle;   // Engine: -1..1 (negative = astern)
        private float _steer;      // Engine: -1..1 (rudder)
        private float _leftOar;    // Oars: -1..1 (port-oar pull; negative = back-water)
        private float _rightOar;   // Oars: -1..1 (starboard-oar pull)
        private bool _brace;       // Oars: oars planted in the water = strong braking drag

        public BoatHullDef Hull => _hull;
        public bool IsAground { get; private set; }
        public Vector2 Velocity => _rb != null ? _rb.linearVelocity : Vector2.zero;

        /// <summary>Port-oar stroke state (-1 back-water .. +1 forward), for the per-oar row rig animation.</summary>
        public float LeftOar => _leftOar;
        /// <summary>Starboard-oar stroke state (-1 back-water .. +1 forward), for the per-oar row rig animation.</summary>
        public float RightOar => _rightOar;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            _rb.gravityScale = 0f;
            _rb.linearDamping = 0.2f;
            _rb.angularDamping = 2.5f;
            // Don't tunnel the thin shore-edge / dock colliders when nudging up to the dock.
            _rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            if (_hull != null) _rb.mass = Mathf.Max(1f, _hull.MassKg / 100f);
        }

        /// <summary>
        /// Set helm input. Throttle is -1..1: ahead (forward) or astern (reverse, for backing onto the
        /// dock). Called by DevBoatInput now, by the InputService later.
        /// </summary>
        public void SetControl(float throttle, float steer)
        {
            _throttle = Mathf.Clamp(throttle, -1f, 1f);
            _steer = Mathf.Clamp(steer, -1f, 1f);
        }

        /// <summary>
        /// Effective engine thrust (design-unit force, before the physics-feel scale) for a throttle in
        /// -1..1. Ahead is full power; astern (throttle &lt; 0) is weaker by <paramref name="asternFactor"/>,
        /// like a real propeller — so the result is negative and smaller in magnitude than ahead. Pure +
        /// static so it's unit-testable without the physics step.
        /// </summary>
        public static float EngineThrust(float throttle, float enginePower, float asternFactor)
        {
            throttle = Mathf.Clamp(throttle, -1f, 1f);
            float factor = throttle < 0f ? asternFactor : 1f;
            return throttle * enginePower * factor;
        }

        /// <summary>
        /// THE propulsion branch (data-driven, ADR 0003): which control scheme a hull uses. Engine hulls
        /// (e.g. the Punt) take the outboard helm — <see cref="SetControl"/> (throttle + speed-scaled
        /// rudder, <see cref="ApplyEngineDrive"/>); Oars hulls (the Dory) take per-oar hand-rowing —
        /// <see cref="SetOarInput"/> (<see cref="ApplyOarDrive"/>). One place so the controller and the
        /// input layer (DevBoatInput) never drift apart. Pure + static.
        /// </summary>
        public static bool UsesEngineHelm(PropulsionType propulsion) => propulsion == PropulsionType.Engine;

        /// <summary>
        /// Engine rudder torque (design-unit, before the physics-feel scale) for a helm in -1..1. Authority
        /// scales with WAY — the forward speed through the water — so it is ~0 at rest (you CAN'T pivot dead
        /// in the water; give a burst of throttle to turn) and rises + saturates with speed; aground it's
        /// heavily damped. Positive helm → bow right (negative torque), matching the oar-yaw sign. The
        /// outboard's "speed-scaled rudder" from boats-and-navigation.md §2. Pure + static.
        /// </summary>
        public static float RudderTorque(float helm, float rudderAuthority, float wayMetersPerSec, bool aground)
            => -Mathf.Clamp(helm, -1f, 1f) * rudderAuthority
               * Mathf.Clamp01(Mathf.Abs(wayMetersPerSec) / 2f)
               * (aground ? 0.2f : 1f);

        /// <summary>
        /// Set per-oar rowing input (Propulsion = Oars). leftOar/rightOar in -1..1 (forward pull positive,
        /// back-water negative); a DIFFERENCE between the two yaws the boat. brace = oars planted for a
        /// strong braking drag. This is the per-oar control surface a real InputService/gamepad maps to —
        /// keyboard drives it now via DevBoatInput. Ignored by the Engine path (which reads SetControl).
        /// </summary>
        public void SetOarInput(float leftOar, float rightOar, bool brace)
        {
            _leftOar = Mathf.Clamp(leftOar, -1f, 1f);
            _rightOar = Mathf.Clamp(rightOar, -1f, 1f);
            _brace = brace;
        }

        /// <summary>
        /// Net forward oar thrust (design-unit force, before the physics-feel scale) from both oars.
        /// Both oars = 2× a single oar; astern (negative inputs) yields negative thrust. Pure + static.
        /// </summary>
        public static float OarThrust(float leftOar, float rightOar, float oarPower)
            => (leftOar + rightOar) * oarPower;

        /// <summary>
        /// Net yaw torque from the oar differential — the moment of the two offset oar forces about the
        /// hull centre. A one-sided forward stroke swings the bow to the OTHER side: left oar forward →
        /// negative torque → clockwise → bow yaws right (starboard), matching the engine steer-sign
        /// convention (positive steer → bow right → negative torque). Both oars equal → zero. Pure + static.
        /// </summary>
        public static float OarYawTorque(float leftOar, float rightOar, float oarPower, float lateralOffset)
            => -(leftOar - rightOar) * oarPower * lateralOffset;

        /// <summary>
        /// Extra drag force (design-unit, before the feel scale) when the oars are braced in the water —
        /// a strong brake opposing motion THROUGH THE WATER. Zero when braceDrag is 0. Pure + static.
        /// </summary>
        public static Vector2 BraceDragForce(Vector2 throughWater, float braceDrag)
            => -throughWater * braceDrag;

        /// <summary>
        /// The non-punishing <b>shallows-hold</b> force (design-unit, before the feel scale): when the boat
        /// is heading into water too shallow to float it (the boat-cross gate, <see cref="BoatCrossing"/>),
        /// a drag opposes ONLY the component of velocity pushing further into the shallows, easing the hull
        /// to a stop at the channel edge. It never opposes motion BACK toward deep water (so you can always
        /// retreat), and it does no damage — too-shallow water just can't be entered (P5 forgiving). Returns
        /// zero unless heading into the shallows. Pure + static so it's EditMode-testable.
        /// </summary>
        /// <param name="velocity">The hull's current velocity (m/s).</param>
        /// <param name="aheadShallow">True when the water just ahead of the bow is too shallow to float the hull.</param>
        /// <param name="towardShallow">Unit (or any) vector pointing from the hull toward the shallow water ahead.</param>
        /// <param name="holdDrag">Drag strength against the into-shallows velocity component.</param>
        public static Vector2 ShallowsHoldForce(Vector2 velocity, bool aheadShallow, Vector2 towardShallow, float holdDrag)
        {
            if (!aheadShallow || holdDrag <= 0f) return Vector2.zero;
            Vector2 dir = towardShallow.sqrMagnitude > 1e-6f ? towardShallow.normalized : Vector2.zero;
            if (dir == Vector2.zero) return Vector2.zero;
            float into = Vector2.Dot(velocity, dir);   // speed component heading into the shallows
            if (into <= 0f) return Vector2.zero;        // moving away / parallel → don't impede retreat
            return -dir * (into * holdDrag);
        }

        /// <summary>
        /// Bring the boat to rest where it sits — zero its linear and angular velocity and clear any held
        /// control input. Called on disembark so a boat LEFT NEAR SHORE <b>parks where it's left</b> and
        /// doesn't coast on after the helm is dropped (the disembark-anywhere safety: an un-crewed boat is
        /// safe-moored, never strands itself). This is NOT the wind/tide mooring-drift mechanic (a separate
        /// follow-up); it is the deliberate "boat stays put" guarantee. Null-safe before <see cref="Awake"/>.
        /// </summary>
        public void Stop()
        {
            _throttle = 0f; _steer = 0f;
            _leftOar = 0f; _rightOar = 0f; _brace = false;
            // Resolve the body lazily so Stop() works even before Awake has run (EditMode / on-arrival
            // before the first physics tick) — Awake may not have cached _rb yet.
            var rb = _rb != null ? _rb : GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                rb.linearVelocity = Vector2.zero;
                rb.angularVelocity = 0f;
            }
        }

        /// <summary>
        /// Swap the active hull (e.g. when the player buys up the ladder — VS-16, driven by OwnedFleet).
        /// Re-derives the rigidbody mass from the new displacement so feel tracks the bigger boat.
        /// A small public setter so the swapper doesn't reach into the private serialized field.
        /// </summary>
        public void SetHull(BoatHullDef hull)
        {
            _hull = hull;
            if (_rb != null && _hull != null) _rb.mass = Mathf.Max(1f, _hull.MassKg / 100f);
        }

        private void FixedUpdate()
        {
            if (_hull == null) return;

            EnvironmentSample env = GameServices.Environment != null
                ? GameServices.Environment.Sample()
                : default;

            // --- Grounding: water depth = seabed depth + tide height; ground if draught exceeds it (P5) ---
            float waterDepth = _localSeabedDepth + env.TideHeight;
            bool agroundNow = waterDepth < _hull.DraughtMeters;
            if (agroundNow && !IsAground)
                EventBus.Publish(new BoatGrounded(this, Mathf.Clamp01(_hull.DraughtMeters - waterDepth)));
            IsAground = agroundNow;

            Vector2 fwd = transform.up;            // bow direction (top-down view)
            Vector2 vel = _rb.linearVelocity;

            // --- Propulsion (data-driven, ADR 0003): hand-rowed oars (Dory) or an engine helm (Punt). ---
            if (UsesEngineHelm(_hull.Propulsion))
                ApplyEngineDrive(fwd, vel, env);
            else
                ApplyOarDrive(fwd, vel, env);

            // --- Hull drag RELATIVE TO THE WATER (tidal current is the ambient water velocity) ---
            Vector2 throughWater = vel - env.CurrentVector;
            Vector2 along = fwd * Vector2.Dot(throughWater, fwd);
            Vector2 sideways = throughWater - along;
            Vector2 drag = -(along * (_hull.ForwardDrag * ForceFeelScale) + sideways * (_hull.LateralDrag * ForceFeelScale));
            _rb.AddForce(drag, ForceMode2D.Force);

            // --- Wind shove (Pillar 1: the dory gets pushed around) ---
            _rb.AddForce(env.WindVector * (_hull.WindExposure * ForceFeelScale), ForceMode2D.Force);

            // --- Boat-cross gate (St Peters, P1/P5): can't pass water too shallow to float the hull. ---
            // The inverse of the on-foot walkability gate, over the SAME authored terrain + tide. Forgiving:
            // it eases the hull to a stop at the shallows — no grounding, no damage (that's a later wave).
            // Self-disables in open water (no TidalTerrain wired).
            if (_tideGatedCrossing)
            {
                // Probe just ahead of the bow; one boat-length is a sensible, draught-independent look-ahead.
                Vector2 ahead = _rb.position + fwd * Mathf.Max(0.5f, _hull.LengthMeters * 0.5f);
                bool aheadShallow = !BoatCrossing.CanFloatNow(ahead, _hull.DraughtMeters);
                Vector2 hold = ShallowsHoldForce(vel, aheadShallow, ahead - _rb.position, _shallowsHoldDrag);
                if (hold != Vector2.zero) _rb.AddForce(hold * ForceFeelScale, ForceMode2D.Force);
            }
        }

        /// <summary>
        /// Engine helm (the outboard model, boats-and-navigation.md §2): throttle thrust along the hull
        /// (ahead full, astern weaker) + a rudder whose authority scales with WAY — the forward speed
        /// through the water. At rest there's ~no authority, so an outboard CANNOT pivot dead in the water
        /// (you give a burst of throttle to turn in tight quarters); it bites as you make speed. This is
        /// the Engine branch (the Punt); the Oars branch (the Dory) keeps per-oar rowing.
        /// </summary>
        private void ApplyEngineDrive(Vector2 fwd, Vector2 vel, EnvironmentSample env)
        {
            // --- Engine thrust along the hull (no drive when hard aground). Ahead full, astern weaker. ---
            if (!IsAground)
            {
                float thrust = EngineThrust(_throttle, _hull.EnginePower, _asternThrustFactor) * ForceFeelScale;
                _rb.AddForce(fwd * thrust, ForceMode2D.Force);
            }

            // --- Rudder: authority scales with WAY (forward speed through the water, §2) — nil at rest. ---
            float way = Vector2.Dot(vel - env.CurrentVector, fwd);   // forward way through the moving water
            _rb.AddTorque(RudderTorque(_steer, _hull.RudderAuthority, way, IsAground) * RudderFeelScale);
        }

        /// <summary>
        /// Differential hand-rowing. Each oar's forward pull acts at a lateral offset, so a one-sided
        /// stroke gives forward thrust AND a yaw torque about the hull centre (left oar → bow swings
        /// right); both oars equal → straight, no fighting. Space braces the oars for a strong braking
        /// drag. No drive when hard aground. A forgiving feel prototype — tune via the hull's oar stats.
        /// </summary>
        private void ApplyOarDrive(Vector2 fwd, Vector2 vel, EnvironmentSample env)
        {
            if (!IsAground)
            {
                float thrust = OarThrust(_leftOar, _rightOar, _hull.OarPower) * ForceFeelScale;
                float yaw    = OarYawTorque(_leftOar, _rightOar, _hull.OarPower, _hull.OarLateralOffset) * ForceFeelScale;
                _rb.AddForce(fwd * thrust, ForceMode2D.Force);
                _rb.AddTorque(yaw);
            }

            // Brace: oars planted = a strong extra drag relative to the water (brake/stop). Forgiving.
            if (_brace)
            {
                Vector2 throughWater = vel - env.CurrentVector;
                _rb.AddForce(BraceDragForce(throughWater, _hull.OarBraceDrag) * ForceFeelScale, ForceMode2D.Force);
            }
        }
    }
}
