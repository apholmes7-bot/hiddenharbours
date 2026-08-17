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
    ///
    /// <para>⭐ <b>AND SHE MAY BE A MACHINE THAT SWIMS.</b> Two things vary underneath one set of
    /// controls, and they are deliberately decided by two different sources:</para>
    /// <list type="bullet">
    ///   <item><b>The DRIVETRAIN is decided by the ART.</b> A rig that publishes no lock angle has no
    ///   steering axle, so she is skid-steered and <see cref="SkidSteerMath"/> turns her; a rig that
    ///   publishes one is Ackermann and <see cref="VehicleSteeringMath"/> turns her. Measured, not
    ///   declared — the Otter's probe pins that <c>resolve({}).steer</c> does not exist, and her
    ///   sidecar's <c>_excluded.steering_geometry</c> says so in words.</item>
    ///   <item><b>The MEDIUM is decided by the KIND</b> (<see cref="VehicleKind"/>, off her def
    ///   through <see cref="VehicleKinds"/>) against the world's one deterministic depth. Past her
    ///   float draft she is afloat; back in the shallows she is on her wheels again.</item>
    /// </list>
    ///
    /// <para><b>Conflating the two would have been the easy mistake.</b> "Amphibious" is not a
    /// drivetrain and "skid steer" is not a relationship with water — a tracked amphibian and a
    /// steered one are both perfectly ordinary machines, and the day one arrives, exactly one of
    /// these two reads should change.</para>
    ///
    /// <para><b>The medium is a state on the MACHINE, never on the player.</b> A driver stays in
    /// <c>ControlMode.Driving</c> from the yard to the far shore and back: nothing here publishes a
    /// mode change, and the whole swap is <see cref="IsAfloat"/> plus the envelope it selects. The
    /// machine changes medium; the player is just driving.</para>
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
        private float _leftTrackOdometerMeters;
        private float _rightTrackOdometerMeters;

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

        /// <summary>Distance travelled, metres — signed, so reversing unwinds it. An Ackermann
        /// machine's wheels take their roll phase from this and nothing else, which is what keeps
        /// them from drifting out of step with the ground over a long drive. (A SKID machine's two
        /// sides travel different distances in a turn and take
        /// <see cref="LeftTrackOdometerMeters"/> / <see cref="RightTrackOdometerMeters"/>
        /// instead.)</summary>
        public float OdometerMeters => _odometerMeters;

        /// <summary>Distance the STREET-side (rig −x) track has travelled, metres, signed — what her
        /// left wheels' <c>rollL</c> phase is a function of.</summary>
        public float LeftTrackOdometerMeters => _leftTrackOdometerMeters;

        /// <summary>Distance the CURB-side (rig +x) track has travelled, metres, signed — her
        /// <c>rollR</c>.</summary>
        public float RightTrackOdometerMeters => _rightTrackOdometerMeters;

        /// <summary>The street-side track's speed this instant, m/s. Equal to
        /// <see cref="SpeedMetersPerSecond"/> on a machine that is not skid-steered.</summary>
        public float LeftTrackSpeedMetersPerSecond { get; private set; }

        /// <summary>The curb-side track's speed this instant, m/s.</summary>
        public float RightTrackSpeedMetersPerSecond { get; private set; }

        /// <summary>
        /// ⭐ <b>She is swimming.</b> The medium she is in — a state on the MACHINE, recomputed every
        /// tick from the world's one deterministic depth at her position (never saved; rule 5) and
        /// held across the water's edge by her def's hysteresis band.
        ///
        /// <para>False for anything that is not a <see cref="VehicleKind.AmphibiousVehicle"/>, and
        /// false for an amphibian whose rig publishes no flotation — see
        /// <see cref="VehicleGrounding.IsAfloat"/>, which is where both refusals live.</para>
        /// </summary>
        public bool IsAfloat { get; private set; }

        /// <summary>What kind of machine she is, off her def through <see cref="VehicleKinds"/>.
        /// <see cref="VehicleKind.RoadVehicle"/> with no def at all — an unassigned controller must
        /// read as the thing that stays out of the water.</summary>
        public VehicleKind Kind => _vehicle != null ? _vehicle.Kind : VehicleKind.RoadVehicle;

        /// <summary>
        /// <b>Has she a steering axle?</b> No lock angle on the rig means no kingpin, no wheel yaw,
        /// and a machine that turns by splitting her two sides instead.
        ///
        /// <para><b>An ART read, not a kind read</b> (see the class doc). The track width is required
        /// too, because a differential levers against it and a def carrying neither number is not a
        /// skid machine, it is an unbaked one — she falls through to the Ackermann path, whose own
        /// zero-lock guard leaves her driving straight rather than dividing by nothing.</para>
        /// </summary>
        public bool IsSkidSteered
        {
            get
            {
                VehicleMeshDef m = _vehicle != null ? _vehicle.Mesh : null;
                return m != null && m.MaxInnerSteerDegrees <= 0f && m.TrackWidthMeters > 0f;
            }
        }

        /// <summary>What the pedals mean right now — her afloat envelope while she is swimming, her
        /// land one otherwise. The ONE place the medium becomes a set of numbers.</summary>
        public DriveEnvelope Envelope
        {
            get
            {
                if (_vehicle == null) return default(DriveEnvelope);
                return IsAfloat ? _vehicle.AfloatEnvelope : _vehicle.LandEnvelope;
            }
        }

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

        /// <summary>
        /// Her yaw rate this instant, degrees per second.
        ///
        /// <para><b>Steered:</b> 0 at a standstill however hard the wheel is over, and reversed when
        /// she is backing — the two things a front-wheel model gets right for free.</para>
        ///
        /// <para><b>Skid-steered: neither of those.</b> She spins on the spot at a standstill (that
        /// is what a differential IS), and the stick means the same thing going astern as ahead,
        /// because the sides split the same way whichever way she is travelling. Forcing her through
        /// the truck's model would have produced a machine that cannot turn in a yard and that
        /// mirrors her own stick in reverse.</para>
        /// </summary>
        public float YawRateDegreesPerSecond
        {
            get
            {
                VehicleMeshDef m = _vehicle != null ? _vehicle.Mesh : null;
                if (m == null) return 0f;

                if (IsSkidSteered) return SkidYawRateDegreesPerSecond(m, Envelope.CeilingFor(_speed));

                return VehicleSteeringMath.YawRateDegreesPerSecond(
                    _speed, EffectiveSteer, m.MaxInnerSteerDegrees, m.WheelbaseMeters,
                    m.FrontTrackMeters);
            }
        }

        /// <summary>
        /// The skid yaw the stick is asking for, bounded by what her transmission can actually
        /// differentiate.
        ///
        /// <para>⚠️ Off <see cref="Steer"/>, deliberately NOT <see cref="EffectiveSteer"/>. The
        /// speed falloff is a correction to the geometric bicycle model, which turns tighter the
        /// faster you go; a differential has no such pathology, so applying the falloff here would
        /// invent a lag the machine does not have — and would put the drawn tracks and the physics
        /// yaw back into the disagreement the whole coupling exists to prevent.</para>
        /// </summary>
        private float SkidYawRateDegreesPerSecond(VehicleMeshDef mesh, float trackCeiling)
            => SkidSteerMath.YawRateDemandDegreesPerSecond(
                   _steer,
                   IsAfloat ? _vehicle.AfloatSpinRateDegreesPerSecond
                            : _vehicle.SpinRateDegreesPerSecond,
                   trackCeiling, mesh.TrackWidthMeters);

        /// <summary>Put her in (or take her out of) this vehicle. Resets the drive state — a swap
        /// must not carry the last one's speed, her tracks' phase, or the medium she was last in
        /// across.</summary>
        public void SetVehicle(VehicleDef vehicle)
        {
            _vehicle = vehicle;
            _steer = 0f;
            _speed = 0f;
            _leftTrackOdometerMeters = 0f;
            _rightTrackOdometerMeters = 0f;
            LeftTrackSpeedMetersPerSecond = 0f;
            RightTrackSpeedMetersPerSecond = 0f;
            IsAfloat = false;
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

            // THE MEDIUM FIRST, because everything below is read in it: which envelope the pedals
            // mean, which spin rate the stick means, and whether the land gate is her rule at all.
            IsAfloat = ReadAfloat();

            _steer = StepSteer(_steer, SteerDemand, _vehicle, deltaTime);

            // The envelope she is driving in, NARROWED by whatever her turn is spending — so the
            // wheel is moved first and the trade is priced against where the stick actually is.
            DriveEnvelope envelope = SkidNarrowed(Envelope);

            _speed = StepSpeed(_speed, Throttle, Brake, in envelope, deltaTime);
            _speed = GroundSpeed(_speed, in envelope);
            _odometerMeters += _speed * deltaTime;
            StepTrackOdometers(deltaTime);

            if (_rb == null && (_rb = GetComponent<Rigidbody2D>()) == null) return;

            _rb.linearVelocity = (Vector2)transform.up * _speed;
            _rb.angularVelocity = YawRateDegreesPerSecond;
        }

        /// <summary>
        /// Read the medium at her CURRENT position, from the live Core services (rule 5: recomputed
        /// from <c>(worldSeed, gameTime)</c>, never saved), carrying her present state in so the
        /// hysteresis has something to hold against.
        ///
        /// <para>Her ROOT is the sample point — the ground-centre of her hull footprint, which is the
        /// point her float draft is published about. Not a leading axle: the road gate probes an axle
        /// because a WHEEL leaving the gravel is what puts a truck in trouble, whereas what floats an
        /// amphibian is her hull, all of it, about her centre.</para>
        /// </summary>
        private bool ReadAfloat()
        {
            VehicleMeshDef mesh = _vehicle.Mesh;
            if (mesh == null) return false;   // no rig, no flotation data, no swimming
            Vector2 origin = _rb != null ? _rb.position : (Vector2)transform.position;
            return VehicleGrounding.IsAfloatNow(Kind, IsAfloat, origin, mesh.FloatDraftMeters,
                                                _vehicle.SwimHysteresisMeters);
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
        private float GroundSpeed(float speed, in DriveEnvelope envelope)
        {
            VehicleMeshDef mesh = _vehicle != null ? _vehicle.Mesh : null;
            if (mesh == null) return speed;

            Vector2 origin = _rb != null ? _rb.position : (Vector2)transform.position;
            // ⭐ DISPATCHED ON HER KIND. For a RoadVehicle this is the march, unchanged; for an
            // amphibian it returns +∞ and the speed passes through, because water is not her wall.
            // The braking rate comes off the envelope she is actually in — the stopping distance the
            // horizon is measured from is a fact about how she stops HERE.
            float cap = VehicleGrounding.SpeedCapNow(
                Kind, origin, transform.up, speed, mesh.FrontAxleY, mesh.RearAxleY,
                envelope.BrakingMetersPerSecondSquared, mesh.WheelRadiusMeters);

            return float.IsPositiveInfinity(cap) ? speed : Mathf.Clamp(speed, -cap, cap);
        }

        /// <summary>
        /// <b>The skid trade: turning spends the transmission she was using to go forward.</b> Narrow
        /// both of the envelope's ceilings by whatever the current turn is costing — see
        /// <see cref="SkidSteerMath.MaxBodySpeedMetersPerSecond"/>.
        ///
        /// <para><b>The CEILING, deliberately, and not a clamp on the integrated speed.</b> Clamping
        /// would be the obvious implementation and it snaps: a machine doing 8 m/s up a beach who
        /// floats on one tick would be slammed to her afloat ceiling inside that tick, and hard-over
        /// stick at speed would drop her instantly rather than let her lean into the turn. Lowering
        /// the TARGET instead leaves <see cref="StepSpeed"/>'s own <see cref="Mathf.MoveTowards"/> to
        /// carry her there at her acceleration rate, so every one of those is a smooth give-back —
        /// and the steady state is identical, because the throttle goes on asking for the ceiling
        /// every tick.</para>
        ///
        /// <para>Inert on a machine with a steering axle, whose front wheels make no such trade, and
        /// on one with no mesh to read a track width off.</para>
        /// </summary>
        private DriveEnvelope SkidNarrowed(in DriveEnvelope envelope)
        {
            VehicleMeshDef mesh = _vehicle != null ? _vehicle.Mesh : null;
            if (mesh == null || !IsSkidSteered) return envelope;

            float track = mesh.TrackWidthMeters;
            float ahead = envelope.MaxAheadMetersPerSecond;
            float astern = envelope.MaxAsternMetersPerSecond;

            return new DriveEnvelope(
                SkidSteerMath.MaxBodySpeedMetersPerSecond(
                    SkidYawRateDegreesPerSecond(mesh, ahead), track, ahead),
                SkidSteerMath.MaxBodySpeedMetersPerSecond(
                    SkidYawRateDegreesPerSecond(mesh, astern), track, astern),
                envelope.AccelerationMetersPerSecondSquared,
                envelope.BrakingMetersPerSecondSquared,
                envelope.CoastDecelerationMetersPerSecondSquared);
        }

        /// <summary>
        /// Wind the two sides on by what each actually travelled this tick — the ONE input her
        /// wheels' <c>rollL</c> / <c>rollR</c> phase is a function of, so a side that has been driven
        /// harder through a turn shows it.
        ///
        /// <para>A machine with a steering axle keeps both sides on the BODY odometer: her wheels do
        /// travel slightly different arcs through a turn, but the rig gives her one <c>roll</c> axis
        /// per wheel group and no per-side split to drive, so inventing one here would be arithmetic
        /// nothing can draw. Publishing the body distance on both keeps a single reading of "how far
        /// has this machine gone" true for every consumer.</para>
        /// </summary>
        private void StepTrackOdometers(float deltaTime)
        {
            VehicleMeshDef mesh = _vehicle != null ? _vehicle.Mesh : null;
            if (mesh == null || !IsSkidSteered)
            {
                LeftTrackSpeedMetersPerSecond = _speed;
                RightTrackSpeedMetersPerSecond = _speed;
                _leftTrackOdometerMeters = _odometerMeters;
                _rightTrackOdometerMeters = _odometerMeters;
                return;
            }

            SkidSteerMath.TrackSpeeds(_speed, YawRateDegreesPerSecond, mesh.TrackWidthMeters,
                                      out float left, out float right);
            LeftTrackSpeedMetersPerSecond = left;
            RightTrackSpeedMetersPerSecond = right;
            _leftTrackOdometerMeters += left * deltaTime;
            _rightTrackOdometerMeters += right * deltaTime;
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
            => StepSpeed(current, throttle, brake, vehicle.LandEnvelope, deltaTime);

        /// <summary>
        /// The same integrator against an explicit <see cref="DriveEnvelope"/> — what the tick calls,
        /// so an amphibian's water pedals and her gravel pedals are ONE law with two sets of
        /// constants rather than two bodies of arithmetic to keep in step by hand.
        ///
        /// <para>Afloat the brake is not refused, it is simply no stronger than the water already is
        /// (her afloat envelope carries the drag rate in both the brake and the coast slot) — a pedal
        /// that visibly ignores the player reads as a bug, and a wheel brake in the water genuinely
        /// stops nothing but the tyres.</para>
        /// </summary>
        public static float StepSpeed(float current, float throttle, bool brake,
                                      in DriveEnvelope envelope, float deltaTime)
        {
            if (brake)
                return Mathf.MoveTowards(current, 0f,
                                         envelope.BrakingMetersPerSecondSquared * deltaTime);

            throttle = Mathf.Clamp(throttle, -1f, 1f);
            if (Mathf.Approximately(throttle, 0f))
                return Mathf.MoveTowards(current, 0f,
                                         envelope.CoastDecelerationMetersPerSecondSquared * deltaTime);

            float target = throttle > 0f
                ? throttle * envelope.MaxAheadMetersPerSecond
                : throttle * envelope.MaxAsternMetersPerSecond;

            return Mathf.MoveTowards(current, target,
                                     envelope.AccelerationMetersPerSecondSquared * deltaTime);
        }
    }
}
