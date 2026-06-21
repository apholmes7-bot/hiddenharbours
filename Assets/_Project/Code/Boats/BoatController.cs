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
    /// Greybox tuning note: the 0.01/0.001 factors translate the design-unit stats on the hull into
    /// a good 2D-physics feel; tune to taste. Real input arrives via DevBoatInput for now and an
    /// InputService later (ui-ux).
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(CapsuleCollider2D))]   // hull collider: bumps shore + dock pilings (cozy, no damage)
    public class BoatController : MonoBehaviour
    {
        /// <summary>Astern thrust as a fraction of ahead, like a real prop pushes backward weaker (~40%).</summary>
        public const float DefaultAsternFactor = 0.4f;

        [SerializeField] private BoatHullDef _hull;
        [Tooltip("Placeholder local seabed depth (m). Replaced by a real seabed/depth map later.")]
        [SerializeField] private float _localSeabedDepth = 3f;
        [Tooltip("Astern thrust as a fraction of ahead thrust. A real propeller pushes backward weaker " +
                 "(~40%) — enough to back off the dock without turning the dory into a reversing car.")]
        [SerializeField] private float _asternThrustFactor = DefaultAsternFactor;

        private Rigidbody2D _rb;
        private float _throttle;   // -1..1 (negative = astern)
        private float _steer;      // -1..1

        public BoatHullDef Hull => _hull;
        public bool IsAground { get; private set; }
        public Vector2 Velocity => _rb != null ? _rb.linearVelocity : Vector2.zero;

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

            // --- Engine (no drive when hard aground). Ahead full, astern weaker (real-prop feel). ---
            if (!IsAground)
            {
                float thrust = EngineThrust(_throttle, _hull.EnginePower, _asternThrustFactor) * 0.01f;
                _rb.AddForce(fwd * thrust, ForceMode2D.Force);
            }

            // --- Rudder: authority grows with speed; almost nil at a standstill or aground ---
            float speed = vel.magnitude;
            float steerAuthority = _hull.RudderAuthority * 0.001f
                                   * Mathf.Clamp01(speed / 2f)
                                   * (IsAground ? 0.2f : 1f);
            _rb.AddTorque(-_steer * steerAuthority);

            // --- Hull drag RELATIVE TO THE WATER (tidal current is the ambient water velocity) ---
            Vector2 throughWater = vel - env.CurrentVector;
            Vector2 along = fwd * Vector2.Dot(throughWater, fwd);
            Vector2 sideways = throughWater - along;
            Vector2 drag = -(along * (_hull.ForwardDrag * 0.01f) + sideways * (_hull.LateralDrag * 0.01f));
            _rb.AddForce(drag, ForceMode2D.Force);

            // --- Wind shove (Pillar 1: the dory gets pushed around) ---
            _rb.AddForce(env.WindVector * (_hull.WindExposure * 0.01f), ForceMode2D.Force);
        }
    }
}
