using HiddenHarbours.Core;
using UnityEngine;

namespace HiddenHarbours.Vehicles
{
    /// <summary>
    /// <b>Driving a road vehicle (ADR 0035)</b> — the throttle, the brake, the wheel, and the one
    /// thing the rig explicitly refuses to do for us: turning the MACHINE when the WHEELS turn.
    ///
    /// <para>Lives on the physics root, exactly where <c>BoatController</c> lives and for the same
    /// reason: heading is a fact about the root, and everything that follows the nose rides the
    /// root. <c>transform.up</c> is the nose, the same convention the whole boat fleet uses, so the
    /// heading readers do not need a second case.</para>
    ///
    /// <para><b>The model is kinematic, not force-based, and that is a deliberate difference from
    /// the boats.</b> A hull is pushed through a fluid that pushes back differently in every
    /// direction — thrust, lateral drag and wind genuinely are forces, so <c>BoatController</c>
    /// integrates them. A truck's tyres do not let her slide sideways at the speeds this game runs
    /// at; her motion is a curve her front wheels choose. Modelling that as forces would mean adding
    /// a lateral grip term large enough to suppress the sliding the force model invents — a fake
    /// number cancelling a fake behaviour. Speed along the nose and yaw rate from the steering
    /// geometry is the honest shape, and it is the same model the rig's own published turning radii
    /// are computed under.</para>
    ///
    /// <para>She still carries a <see cref="Rigidbody2D"/> and still collides: the velocity and
    /// angular velocity are written each FixedUpdate and the solver resolves contacts against them,
    /// so she stops at a wall rather than driving through it.</para>
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [DisallowMultipleComponent]
    public class VehicleController : MonoBehaviour
    {
        [SerializeField] private VehicleDef _vehicle;

        private Rigidbody2D _rb;
        private float _steer;
        private float _speed;
        private float _odometerMeters;

        /// <summary>The vehicle being driven. Null = parked and inert.</summary>
        public VehicleDef Vehicle => _vehicle;

        /// <summary>Throttle demand, −1 (full astern) … +1 (full ahead). Written by whatever is
        /// driving — a dev harness today, the player's control mode when that lands.</summary>
        public float Throttle { get; set; }

        /// <summary>The brake, hard on or off. Distinct from a negative throttle: braking stops her,
        /// reverse drives her backwards, and a driver wants both on separate controls.</summary>
        public bool Brake { get; set; }

        /// <summary>Where the driver is holding the wheel, −1 (full right) … +1 (full left). The
        /// wheel does not jump there — see <see cref="Steer"/>.</summary>
        public float SteerDemand { get; set; }

        /// <summary>
        /// <b>Where the wheel ACTUALLY is</b>, −1 … +1 — what the front wheels are drawn at and what
        /// the yaw rate is solved from, so the picture and the physics cannot disagree. Moves toward
        /// <see cref="SteerDemand"/> at the def's steer rate and self-centres when released.
        /// </summary>
        public float Steer => _steer;

        /// <summary>Signed speed along the nose, metres per second. Negative = reversing.</summary>
        public float SpeedMetersPerSecond => _speed;

        /// <summary>Distance travelled, metres — signed, so reversing unwinds it. The wheels' roll
        /// phase is a function of this and nothing else, which is what keeps them from drifting out
        /// of step with the ground over a long drive.</summary>
        public float OdometerMeters => _odometerMeters;

        /// <summary>
        /// The steer the GEOMETRY sees: the wheel position, reduced at speed.
        ///
        /// <para><b>Why the falloff is applied here rather than to the yaw rate.</b> The pure
        /// geometric model turns tighter the faster you go, which is exactly backwards from how a
        /// vehicle feels, so something has to soften it. Softening the YAW alone would leave the
        /// front wheels drawn hard over while the truck barely turned — precisely the disagreement
        /// the rig's own sidecar warns about ("a game that turns the wheels without yawing… will
        /// look wrong, and the rig will not stop it"). Reducing the STEER instead is speed-sensitive
        /// steering, which is a real thing on a real truck, and it keeps one number feeding both the
        /// picture and the physics.</para>
        /// </summary>
        public float EffectiveSteer
        {
            get
            {
                float half = _vehicle != null ? _vehicle.SteerFalloffHalfSpeedMetersPerSecond : 0f;
                if (half <= 0f) return _steer;
                return _steer / (1f + Mathf.Abs(_speed) / half);
            }
        }

        /// <summary>Her yaw rate this instant, degrees per second — 0 at a standstill however hard
        /// the wheel is over, and reversed when she is backing.</summary>
        public float YawRateDegreesPerSecond
        {
            get
            {
                VehicleMeshDef m = _vehicle != null ? _vehicle.Mesh : null;
                if (m == null) return 0f;
                return VehicleSteeringMath.YawRateDegreesPerSecond(
                    _speed, EffectiveSteer, m.MaxInnerSteerDegrees, m.WheelbaseMeters,
                    m.FrontTrackMeters);
            }
        }

        /// <summary>Put her in (or take her out of) this vehicle. Resets the drive state — a swap
        /// must not carry the last one's speed across.</summary>
        public void SetVehicle(VehicleDef vehicle)
        {
            _vehicle = vehicle;
            _steer = 0f;
            _speed = 0f;
            Throttle = 0f;
            SteerDemand = 0f;
            Brake = false;
        }

        private void Awake() => _rb = GetComponent<Rigidbody2D>();

        private void FixedUpdate() => StepPhysics(Time.fixedDeltaTime);

        /// <summary>
        /// One physics tick — the FixedUpdate body, callable directly so EditMode tests (where the
        /// player loop does not run) drive the exact production path.
        /// </summary>
        public void StepPhysics(float deltaTime)
        {
            if (_vehicle == null || deltaTime <= 0f) return;

            _steer = StepSteer(_steer, SteerDemand, _vehicle, deltaTime);
            _speed = GroundSpeed(StepSpeed(_speed, Throttle, Brake, _vehicle, deltaTime));
            _odometerMeters += _speed * deltaTime;

            if (_rb == null && (_rb = GetComponent<Rigidbody2D>()) == null) return;

            _rb.linearVelocity = (Vector2)transform.up * _speed;
            _rb.angularVelocity = YawRateDegreesPerSecond;
        }

        /// <summary>
        /// <b>The land gate</b> (<see cref="VehicleGrounding"/>): cap the speed at what she could still
        /// stop from before her leading wheel leaves the gravel. Applied to the speed the pedals have just
        /// ASKED for, before it becomes motion — so the look-ahead reads the direction she is about to go,
        /// and backing off the water's edge is always allowed.
        ///
        /// <para><b>A cap, not a refusal</b>, and the difference is the whole behaviour. Zeroing her when a
        /// probe lands on water sets up a limit cycle — stopped, she has no stopping distance, so the probe
        /// pulls back onto dry ground, so the throttle is allowed, so she accelerates until it trips again,
        /// several times a second at the water's edge. The cap falls continuously to zero exactly as the
        /// clear road does, so what the driver sees is a truck braking smoothly to a halt on the gravel.</para>
        ///
        /// <para>Self-disabling: no mesh (so no axle geometry) or no authored terrain means no gate, and
        /// the speed passes through untouched.</para>
        /// </summary>
        private float GroundSpeed(float speed)
        {
            VehicleMeshDef mesh = _vehicle != null ? _vehicle.Mesh : null;
            if (mesh == null) return speed;

            Vector2 origin = _rb != null ? _rb.position : (Vector2)transform.position;
            float cap = VehicleGrounding.SpeedCapNow(
                origin, transform.up, speed, mesh.FrontAxleY, mesh.RearAxleY,
                _vehicle.BrakingMetersPerSecondSquared, mesh.WheelRadiusMeters);

            return float.IsPositiveInfinity(cap) ? speed : Mathf.Clamp(speed, -cap, cap);
        }

        /// <summary>
        /// Move the wheel one tick toward the driver's demand. Pure, so the feel is pinned by test
        /// rather than felt for.
        ///
        /// <para>Released (<paramref name="demand"/> 0) the wheel self-centres at its own, faster
        /// rate and <b>lands exactly on centre</b> rather than creeping toward it — a wheel that
        /// asymptotes never quite stops turning the truck, and over a long straight that is a slow
        /// drift the player cannot correct because the input is already neutral.</para>
        /// </summary>
        public static float StepSteer(float current, float demand, VehicleDef vehicle, float deltaTime)
        {
            demand = Mathf.Clamp(demand, -1f, 1f);
            bool returning = Mathf.Approximately(demand, 0f);
            float rate = returning ? vehicle.SteerReturnFullLocksPerSecond
                                   : vehicle.SteerRateFullLocksPerSecond;
            return Mathf.MoveTowards(current, demand, rate * deltaTime);
        }

        /// <summary>
        /// Integrate speed one tick: brake first, then throttle, then coast.
        ///
        /// <para><b>The brake wins over the throttle</b>, and it brakes toward a dead stop rather
        /// than through it — standing on both pedals holds a truck still, it does not reverse her.
        /// With neither pedal she coasts down to zero at the def's coast rate; <see cref="Mathf.MoveTowards"/>
        /// throughout, so every one of these lands exactly on its target instead of asymptoting.</para>
        /// </summary>
        public static float StepSpeed(float current, float throttle, bool brake, VehicleDef vehicle,
                                      float deltaTime)
        {
            if (brake)
                return Mathf.MoveTowards(current, 0f,
                                         vehicle.BrakingMetersPerSecondSquared * deltaTime);

            throttle = Mathf.Clamp(throttle, -1f, 1f);
            if (Mathf.Approximately(throttle, 0f))
                return Mathf.MoveTowards(current, 0f,
                                         vehicle.CoastDecelerationMetersPerSecondSquared * deltaTime);

            float target = throttle > 0f
                ? throttle * vehicle.MaxSpeedMetersPerSecond
                : throttle * vehicle.MaxReverseSpeedMetersPerSecond;

            return Mathf.MoveTowards(current, target,
                                     vehicle.AccelerationMetersPerSecondSquared * deltaTime);
        }
    }
}
