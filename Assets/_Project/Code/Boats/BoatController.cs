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
    [RequireComponent(typeof(BoatMooring))]         // the rope/mooring (tie-up vs drift) rides on every boat
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

        [Header("Seakeeping (ADR 0018 B3 — the sea pushes the boat)")]
        [Tooltip("Optional shared GameConfig. When wired, its Seakeeping policy (master switch, bite strength, " +
                 "exposure falloff, per-axis weights) drives the environmental force — no magic numbers (rule 6). " +
                 "Left null (EditMode / unwired greybox) the serialized fallback below applies, which mirrors the " +
                 "config default.")]
        [SerializeField] private GameConfig _config;
        [Tooltip("Fallback seakeeping policy when no GameConfig is wired (mirrors SeakeepingSettings.Default). " +
                 "The FIELD shape is the same WaveFieldSettings the visual rock (BoatWaveMotion) and the shader " +
                 "bridge use — keep them identical until GameConfig unifies them (ADR 0018 note).")]
        [SerializeField] private SeakeepingSettings _seakeeping = SeakeepingSettings.Default;
        [Tooltip("The shared deterministic wave field's shape (ADR 0018). SIM path — the pure WaveMath the boat " +
                 "is pushed by, NOT the presentation animator (addendum). Keep identical to BoatWaveMotion's copy " +
                 "so the hull is shoved by the same waves it visibly rocks on, until GameConfig unifies them.")]
        [SerializeField] private WaveFieldSettings _waveField = WaveFieldSettings.Default;
        [Tooltip("Shallows SLOW the boat — they never cut the helm. When the hull is aground/touching " +
                 "bottom this design-unit drag opposes its motion through the water, so the boat feels " +
                 "heavy and sluggish (the desired 'teeth'), but throttle, oars and rudder stay FULLY " +
                 "responsive — the player can always work their way out (P5, never-punishing). Friction, " +
                 "not a dead helm. Tune to taste.")]
        [SerializeField] private float _groundedSlowdownDrag = 900f;
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

        /// <summary>
        /// Helm state (-1 hard a-port .. 0 amidships .. +1 hard a-starboard) — the rudder input the Engine
        /// branch steers on, as set by <see cref="SetControl"/>. Symmetric with <see cref="LeftOar"/>/
        /// <see cref="RightOar"/>: the drive state a presentation layer must read to draw the boat honestly.
        ///
        /// <para>The outboard overlay (<c>OutboardMotorLayer</c>) PULLS this rather than being pushed a copy of
        /// it every frame. Pull is self-sourcing — the picture cannot fall out of step because some driving
        /// system forgot to write it, which is exactly the dropped-state blind spot #205 fixed for the oars.
        /// Read-only: presentation never writes back into the sim (rule 5).</para>
        /// </summary>
        public float Steer => _steer;

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
            // INTERPOLATE (ADR 0022 phase 5, the second half of the owner's "stuttery" rocking).
            // Physics steps at the fixed rate; the hull is DRAWN every render frame. Uninterpolated,
            // this body's transform is a 50 Hz staircase read by a 60 Hz renderer — some frames the
            // boat does not move, others it moves twice. A baked sprite hull hid that (it snaps to 8
            // or 32 facings and to 8 rock frames anyway); a mesh hull draws the staircase, because
            // drawing exactly what the transform says is the whole point of it. Measured on the wave
            // rider's own harness: the 50 Hz staircase TRIPLES the applied roll's frame-to-frame
            // acceleration ratio (1.57 → 4.59) even once the phase source is smooth, and on the old
            // phase it drove reconstruction sign-reversals from 1.7% of frames to 15.5%.
            // Set here, beside the other physics-policy normalisation, so every ALREADY-BUILT scene
            // self-heals on load — the owner does not have to re-run a builder to stop the stutter.
            // Visual-only: interpolation moves the rendered transform, never the simulated body, so
            // helm feel, colliders, mooring and the deck-walk clamp are untouched.
            _rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            if (_hull != null) _rb.mass = Mathf.Max(1f, _hull.MassKg / 100f);

            // Seakeeping policy: prefer the shared GameConfig (no magic numbers, rule 6); the serialized
            // field is the fallback (it mirrors the config default) so EditMode/unwired scenes still work.
            if (_config != null) _seakeeping = _config.Seakeeping;
        }

        /// <summary>
        /// The active seakeeping policy this controller is running (config-preferred, else the serialized
        /// fallback). Exposed for tests / dev rigs; resolved once at <see cref="Awake"/>.
        /// </summary>
        public SeakeepingSettings SeakeepingPolicy => _seakeeping;

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
        /// in the water; give a burst of throttle to turn) and rises + saturates with speed. Positive helm →
        /// bow right (negative torque), matching the oar-yaw sign. The outboard's "speed-scaled rudder" from
        /// boats-and-navigation.md §2. Pure + static.
        ///
        /// Grounding never touches rudder authority: a boat in the shallows is SLOWED (the grounded-slowdown
        /// drag, <see cref="GroundedSlowdownForce"/>), not steering-crippled — so the helm never "cuts out"
        /// and the player can always steer their way back to deep water (P5, never-punishing). As speed
        /// bleeds off in the shallows the rudder naturally loses bite through the WAY term, which is the
        /// honest, recoverable feel; it is not zeroed by a grounding flag.
        /// </summary>
        public static float RudderTorque(float helm, float rudderAuthority, float wayMetersPerSec)
            => -Mathf.Clamp(helm, -1f, 1f) * rudderAuthority
               * Mathf.Clamp01(Mathf.Abs(wayMetersPerSec) / 2f);

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
        /// The <b>grounded-slowdown</b> force (design-unit, before the feel scale): when the hull is
        /// aground/touching bottom, a drag opposes its motion THROUGH THE WATER so the boat feels heavy and
        /// sluggish in the shallows — the desired "teeth". Crucially this is the <i>only</i> thing grounding
        /// does to handling: it <b>SLOWS</b> the boat, it never zeroes thrust or rudder authority. The player
        /// keeps full throttle/oar/helm response and can always work back to deep water (P5, never-punishing
        /// — the helm never cuts out). Symmetric in direction (unlike <see cref="ShallowsHoldForce"/>, which
        /// is one-way): it resists motion either way, just adding friction, so retreating is sluggish but
        /// always possible. Returns zero when not aground or when drag is non-positive. Pure + static so it's
        /// EditMode-testable without the physics step.
        /// </summary>
        /// <param name="throughWater">The hull's velocity relative to the ambient water (m/s).</param>
        /// <param name="aground">True when the keel is on/below the bottom (draught exceeds water depth).</param>
        /// <param name="slowdownDrag">Drag strength against the through-water velocity while aground.</param>
        public static Vector2 GroundedSlowdownForce(Vector2 throughWater, bool aground, float slowdownDrag)
        {
            if (!aground || slowdownDrag <= 0f) return Vector2.zero;
            return -throughWater * slowdownDrag;
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
        /// Swap the active hull (the player buys up the ladder — VS-16, driven by OwnedFleet; or the dev
        /// boat picker cycles hulls at the helm). Re-derives the rigidbody mass from the new displacement so
        /// feel tracks the bigger boat — WITHOUT it a swap is cosmetic, and a 1200 kg console would still
        /// shove around like a 400 kg dory. A small public setter so the swapper doesn't reach into the
        /// private serialized field.
        ///
        /// <para>Resolves the body LAZILY, exactly as <see cref="Stop"/> does and for the same reason:
        /// <see cref="Awake"/> may not have cached <c>_rb</c> yet (EditMode, or a swap wired up before the
        /// first tick). Going through the cache alone would silently drop the mass write in those cases —
        /// no throw, no warning, just a boat that weighs whatever the last one did.</para>
        /// </summary>
        public void SetHull(BoatHullDef hull)
        {
            _hull = hull;
            if (_hull == null) return;
            var rb = _rb != null ? _rb : GetComponent<Rigidbody2D>();
            if (rb != null) rb.mass = Mathf.Max(1f, _hull.MassKg / 100f);
        }

        private void FixedUpdate()
        {
            if (_hull == null) return;

            EnvironmentSample env = GameServices.Environment != null
                ? GameServices.Environment.Sample()
                : default;

            // --- Grounding: water depth = seabed depth + tide height; aground if draught exceeds it (P5). ---
            // NOTE: grounding is NON-killing. It SLOWS the boat (the grounded-slowdown drag below) so the
            // shallows feel heavy and sluggish — the "teeth" — but it NEVER zeroes thrust or cuts the rudder.
            // The helm/throttle/oars stay fully responsive so the player can always steer/power their way back
            // to deep water (P5, never-punishing). This matters under St Peters' big tide: at low water the
            // region-wide TideHeight alone can trip this (the depth here is still flat & time-based, not
            // per-position) — so it MUST stay a slowdown, not a helm-cut, or a transient tide phase would kill
            // control independent of where you are.
            float waterDepth = _localSeabedDepth + env.TideHeight;
            bool agroundNow = waterDepth < _hull.DraughtMeters;
            if (agroundNow && !IsAground)
                EventBus.Publish(new BoatGrounded(this, Mathf.Clamp01(_hull.DraughtMeters - waterDepth)));
            IsAground = agroundNow;

            Vector2 fwd = transform.up;            // bow direction (top-down view)
            Vector2 vel = _rb.linearVelocity;

            // --- Propulsion (data-driven, ADR 0003): hand-rowed oars (Dory) or an engine helm (Punt). ---
            // Always driven — drive authority is NOT gated on grounding (the helm never cuts out).
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

            // --- Grounded slowdown (P5 teeth, NOT a kill): aground → an extra through-water drag makes the
            // hull heavy and sluggish in the shallows, but input authority is untouched. Forgiving by design. ---
            Vector2 groundedSlow = GroundedSlowdownForce(throughWater, IsAground, _groundedSlowdownDrag);
            if (groundedSlow != Vector2.zero) _rb.AddForce(groundedSlow * ForceFeelScale, ForceMode2D.Force);

            // --- Wind shove (Pillar 1: the dory gets pushed around) ---
            _rb.AddForce(env.WindVector * (_hull.WindExposure * ForceFeelScale), ForceMode2D.Force);

            // --- Seakeeping: THE SEA PUSHES THE BOAT (ADR 0018 B3, P1/P5). On top of everything above,
            // never replacing it. Head sea slows headway + a pitching shove; a beam sea shoves sideways +
            // yaws (demands the helm); a following sea surges + can slew. Scaled by SeaState01 (TIME) ×
            // exposure (PLACE) × the per-hull response, so calm sheltered water is UNCHANGED by
            // construction — and the whole thing is off when the policy switch is off. Gentle-to-medium
            // for M1: it makes rough/exposed water a real challenge, it does NOT capsize/swamp (M2). ---
            ApplySeakeeping(fwd, env);

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
        ///
        /// Throttle + rudder are applied REGARDLESS of grounding — the helm never cuts out. Aground the
        /// boat is merely SLOWED by the grounded-slowdown drag (FixedUpdate); the player keeps full throttle
        /// and steering to work back to deep water (P5, never-punishing).
        /// </summary>
        private void ApplyEngineDrive(Vector2 fwd, Vector2 vel, EnvironmentSample env)
        {
            // --- Engine thrust along the hull. Ahead full, astern weaker. Always live (never killed by ground). ---
            float thrust = EngineThrust(_throttle, _hull.EnginePower, _asternThrustFactor) * ForceFeelScale;
            _rb.AddForce(fwd * thrust, ForceMode2D.Force);

            // --- Rudder: authority scales with WAY (forward speed through the water, §2) — nil at rest. ---
            float way = Vector2.Dot(vel - env.CurrentVector, fwd);   // forward way through the moving water
            _rb.AddTorque(RudderTorque(_steer, _hull.RudderAuthority, way) * RudderFeelScale);
        }

        /// <summary>
        /// Differential hand-rowing. Each oar's forward pull acts at a lateral offset, so a one-sided
        /// stroke gives forward thrust AND a yaw torque about the hull centre (left oar → bow swings
        /// right); both oars equal → straight, no fighting. Space braces the oars for a strong braking
        /// drag. A forgiving feel prototype — tune via the hull's oar stats.
        ///
        /// Oar drive is applied REGARDLESS of grounding — the oars never stop answering. Aground the dory is
        /// just SLOWED by the grounded-slowdown drag (FixedUpdate), so you can always row off a soft bottom
        /// (P5, never-punishing; the dory's shallow draught + oars are exactly the "work your way out" tool).
        /// </summary>
        private void ApplyOarDrive(Vector2 fwd, Vector2 vel, EnvironmentSample env)
        {
            // Oar thrust + yaw always live — never killed by grounding (the player can always pull the oars).
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

        /// <summary>
        /// The per-hull seakeeping response for a hull (how much the sea moves + slews it, and how it
        /// self-damps) — the small value <see cref="SeakeepingForcesMath.Resolve"/> takes. Pulled from the
        /// hull's seakeeping data (rule 2 — a dory corks about, a heavy hull shrugs). Null hull → inert.
        /// </summary>
        public static SeakeepingResponse ResponseFor(BoatHullDef hull)
            => hull == null
                ? SeakeepingResponse.Inert
                : SeakeepingForcesMath.ResponseFrom(hull.SeakeepingMassFactor, hull.SeakeepingLiveliness, hull.SeakeepingDamping);

        /// <summary>
        /// Assemble and apply the sea's force + yaw on the hull this tick (ADR 0018 B3). Samples the shared
        /// deterministic wave field under the hull via the PURE SIM PATH — <see cref="WaveMath.TrainsFrom"/>
        /// + <see cref="WaveMath.Sample"/> at the clock's game-time, NOT the presentation animator (the ADR
        /// addendum boundary: gameplay-consequential reads stay on the pure, gameTime-deterministic path).
        /// Exposure (PLACE) comes from the water depth under the hull (the same boat-cross signal — seabed +
        /// tide); open water reads fully exposed. The result rides on top of the existing forces at the
        /// shared feel-scale, and a light hull-specific damping settles the wave-driven motion between
        /// crests. A disabled policy / glass / full shelter / null hull all short-circuit to no force, so
        /// today's handling is preserved exactly.
        /// </summary>
        private void ApplySeakeeping(Vector2 fwd, EnvironmentSample env)
        {
            if (!_seakeeping.Enabled) return;

            double now = GameServices.Clock != null ? GameServices.Clock.TotalSeconds : 0.0;
            Vector2 pos = _rb.position;

            // The field this tick (SIM path — pure function of the deterministic wind + sea state).
            WaveTrains trains = WaveMath.TrainsFrom(env.WindVector, env.SeaState01, in _waveField);
            WaveSample wave = WaveMath.Sample(pos, now, in trains);

            // Exposure (PLACE): deeper/further offshore = full sea; the shallow lee = sheltered. Uses the
            // same depth the boat-cross gate reads; open water (no seabed map) = +Inf depth = fully exposed.
            float depth = BoatCrossing.DepthAt(GameServices.TidalTerrain, GameServices.Environment, now, pos);
            float exposure = SeakeepingForcesMath.Exposure01(
                depth, _seakeeping.ShelterDepthMeters, _seakeeping.FullExposureDepthMeters);

            SeakeepingResponse response = ResponseFor(_hull);
            SeakeepingForce sea = SeakeepingForcesMath.Resolve(
                in wave, fwd, exposure, env.SeaState01, in response, in _seakeeping);
            if (sea.Force == Vector2.zero && sea.Torque == 0f) return;

            _rb.AddForce(sea.Force * ForceFeelScale, ForceMode2D.Force);
            _rb.AddTorque(sea.Torque * ForceFeelScale);

            // Light hull-specific damping: a steadier hull settles faster between crests, so it wanders off
            // course less in a beam sea. Only while the sea is actually working the boat (scaled by the same
            // exposure), so calm/sheltered water is untouched. Opposes linear + angular motion.
            if (response.Damping > 0f)
            {
                float damp = response.Damping * exposure * ForceFeelScale;
                _rb.AddForce(-_rb.linearVelocity * damp, ForceMode2D.Force);
                _rb.AddTorque(-_rb.angularVelocity * damp);
            }
        }
    }
}
