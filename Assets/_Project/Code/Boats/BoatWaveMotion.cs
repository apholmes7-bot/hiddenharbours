using HiddenHarbours.Core;
using UnityEngine;

namespace HiddenHarbours.Boats
{
    /// <summary>
    /// THE BOAT ROCKS ON THE WAVES — ADR 0018 Arc B2, <b>visual-only</b> (P1 "The Sea Has Moods",
    /// P5 "Cozy but with Teeth"). Each frame this samples the shared deterministic wave field
    /// (<see cref="WaveMath"/>) under the hull and decomposes the surface slope against the hull's
    /// heading (<see cref="BoatWaveMotionMath"/>): a beam sea ROLLS the vessel, a head sea PITCHES
    /// bow and stern, a quartering sea does both off-beat — and the split retargets live as the
    /// player turns. Glass calm is dead still (the field's amplitudes are exactly 0 at sea state 0).
    ///
    /// <para><b>Two output paths.</b> When the wired <see cref="DirectionalBoatSprite"/> carries a
    /// wave-coupled ROCK GRID (the iso dory), the visible rock is <b>drawn by swapping rock frames</b>:
    /// this component reconstructs the wave phase under the hull (<see cref="DoryRockMath"/>) and picks
    /// the frame — crest → 2, trough → 6 — so the hand-drawn roll/pitch/heave tracks the swell in
    /// lockstep (owner: <i>"I want the rock animation to correspond to the waves"</i>). The frames OWN
    /// the rock, so the transform roll/pitch/bob below is <b>not applied</b> (no double-rock) and the
    /// tilt hook is held at 0. With no rock grid the legacy TRANSFORM rock (next paragraph) drives the
    /// visual instead — one sampling path, two ways to show it, chosen by <c>_driveRockFrames</c>.</para>
    ///
    /// <para><b>Visual-only by design (the ADR's phasing).</b> The physics body, colliders and
    /// <see cref="BoatController"/> forces are untouched — the rock is applied to the boat's child
    /// VISUAL so the owner can feel the read before it bites. B3 (seakeeping forces, per-hull
    /// response on <c>BoatHullDef</c>, behind a <c>GameConfig</c> toggle) builds on this same read.</para>
    ///
    /// <para><b>How the motion is drawn (¾ top-down pixel art — small and readable beats big):</b>
    /// ROLL is a small additive z-rotation of the visual, routed through
    /// <see cref="DirectionalBoatSprite.VisualTiltDegrees"/> because that component force-resets the
    /// visual's rotation every LateUpdate (the stomp that once ate the boat spotlight) — an external
    /// rotation write would be overwritten, the hook composes AFTER the reset. PITCH is a subtle
    /// screen-vertical offset plus a tiny y-squash (the sprite riding up the face / dipping into the
    /// trough). BOB is a small screen-vertical lift with the crest. Every amplitude is tunable here,
    /// with a master strength (0 = off, restores the untouched visual).</para>
    ///
    /// <para><b>Smooth by construction (the owner's feel pass on B2).</b> The trains ride a
    /// per-frame <see cref="WaveFieldAnimator"/> tick instead of a throttled
    /// <see cref="WaveMath.TrainsFrom"/> snapshot: the old 4 Hz re-derivation moved the dominant
    /// wavelength (and its dispersion-derived speed) under a large running t, JUMPING the phase at
    /// every refresh — the jitter, proportionally worst on a calm sea. The animator eases the train
    /// parameters and accumulates phase incrementally, so the surface is continuous no matter how
    /// the weather drifts; a short fps-independent output smoothing
    /// (<see cref="_motionSmoothingSeconds"/>) then velvets what little residual noise remains.
    /// Presentation-only statefulness lives in the animator (see its class doc) — the sim reference
    /// stays the pure <see cref="WaveMath"/> path.</para>
    ///
    /// <para><b>Determinism &amp; rules.</b> The wave FIELD stays deterministic
    /// (wind, seaState01, position, time) — no RNG, nothing saved (rule 5); the smoothing layer is
    /// stateful but presentation-only and never feeds back into the sim. Allocation-free structs,
    /// one field tick + one <see cref="WaveMath.Sample"/> per frame (rule 7); reads the sim only
    /// through <see cref="GameServices"/> (rule 4 — Boats references Core only).</para>
    ///
    /// <para>⚠ <b>Settings parity (ADR 0018 §(4)).</b> <see cref="_settings"/> starts from
    /// <see cref="WaveFieldSettings.Default"/> — the SAME defaults the Art-side WaveFieldBridge (B1)
    /// publishes to the water shader, so the hull rocks on the same waves the player sees because
    /// both sides derive from the same (wind, seaState01, settings). B3/GameConfig will unify the
    /// two settings instances into ONE owner-tunable source; until then, tune the FIELD's shape
    /// there (or identically in both places), and only the RESPONSE amplitudes here.</para>
    /// </summary>
    [DisallowMultipleComponent]
    // THE WRITER of DirectionalBoatSprite.RockFrame, so it runs FIRST of the boat's visual chain
    // (this −120 → DirectionalBoatSprite −110 → the overlay layers −100). All three work in LateUpdate, and
    // with no explicit order Unity picks one arbitrarily: a writer landing between two readers leaves them a
    // frame apart, which on the 8-frame rock cycle is 45° of wave phase — the hull leaning one way and its
    // engine another. It resolves per boat at build time, so it would have been permanently fine or
    // permanently broken with nothing in the code to say which. These three attributes are ONE decision.
    [DefaultExecutionOrder(-120)]
    public class BoatWaveMotion : MonoBehaviour
    {
        [Header("Master")]
        [Tooltip("Master strength of the whole visual rock. 0 = off (visual restored untouched); 1 = the tuned read; >1 exaggerates for tuning sessions.")]
        [SerializeField] private float _masterStrength = 1f;

        [Header("Roll (beam sea → the deck tilts)")]
        [Tooltip("Degrees of visual z-rotation per unit of beam-axis wave slope. The slope of the Default field peaks around ~0.5–1 in a full gale, so ~16 reads as a clear roll (owner feel pass: doubled from 8).")]
        [SerializeField] private float _rollDegreesPerSlope = 16f;
        [Tooltip("Hard cap on the visual roll (degrees). Owner feel pass: doubled from 4.5 — ±9° at full sea is the readable band he asked for.")]
        [SerializeField] private float _maxRollDegrees = 9f;

        [Header("Pitch (head sea → bow rides the face)")]
        [Tooltip("Screen-vertical offset (world units) per unit of bow-axis wave slope — the sprite riding up the face (+) and dipping into the trough (−). Owner feel pass: doubled from 0.05.")]
        [SerializeField] private float _pitchOffsetPerSlope = 0.1f;
        [Tooltip("Hard cap on the pitch offset (world units). Keep SMALL — this is a read, not a jump. Owner feel pass: doubled from 0.08.")]
        [SerializeField] private float _maxPitchOffset = 0.16f;
        [Tooltip("Vertical squash per unit of |bow-axis slope| — a subtle foreshortening as the hull tips through a wave. 0 disables the squash entirely. Owner feel pass: doubled from 0.05.")]
        [SerializeField] private float _pitchSquashPerSlope = 0.1f;
        [Tooltip("Hard cap on the squash (fraction of the visual's base y-scale). Owner feel pass: doubled from 0.06 — past ~0.15 the sprite visibly 'breathes'.")]
        [SerializeField] private float _maxPitchSquash = 0.12f;

        [Header("Bob (the crest lifts the whole boat)")]
        [Tooltip("Screen-vertical lift (world units) per metre of wave height under the hull. The Default field's envelope tops out ~1.5 m in a full gale. Owner feel pass: doubled from 0.06.")]
        [SerializeField] private float _bobPerHeightMeter = 0.12f;
        [Tooltip("Hard cap on the bob (world units). Owner feel pass: doubled from 0.1.")]
        [SerializeField] private float _maxBob = 0.2f;

        [Header("Rock frame (wave-coupled SPRITE rock — the iso dory owns the visible rock)")]
        [Tooltip("When the wired DirectionalBoatSprite carries a rock grid (the iso dory), the visible rock is " +
                 "DRAWN by swapping rock frames — this component then selects the frame from the wave PHASE " +
                 "under the hull (crest → 2, trough → 6) and STOPS applying the transform roll/pitch/bob below " +
                 "(the frames own the rock; no double-rock). Off → the legacy transform rock (roll/pitch/bob) " +
                 "still drives the visual, for a hull with no rock sheet.")]
        [SerializeField] private bool _driveRockFrames = true;
        [Tooltip("Frames per heading in the rock sheet (DoryIsoRock ships 8). Must match the grid the " +
                 "DirectionalBoatSprite was configured with.")]
        [SerializeField] private int _rockFrameCount = 8;
        [Tooltip("Degrees added to the reconstructed wave phase before it is rounded to a frame — the crest-frame " +
                 "calibration. 0 puts the crest on frame 2 / trough on frame 6 (the README's sync); nudge only if " +
                 "the art is ever re-baked off phase.")]
        [SerializeField] private float _crestFrameCalibrationDegrees = 0f;
        [Tooltip("Wave-height envelope (metres) below which the sea is treated as calm: the dory holds its static " +
                 "level hull (RockFrame −1) so there is no phantom rocking on glass. Above it, the frames track " +
                 "the swell.")]
        [SerializeField] private float _calmAmplitudeThreshold = 0.02f;
        [Tooltip("Hysteresis band (degrees) around each frame boundary so cross-chop can't flip-flop the frame at " +
                 "an edge. ~half a frame step reads steady without lagging the rock. 0 = pick the nearest frame every " +
                 "tick (may jitter on a noisy sea).")]
        [SerializeField] private float _frameHysteresisDegrees = 8f;

        [Header("Smoothing (the owner's 'smooth rock, especially in calm seas')")]
        [Tooltip("Output damping (seconds, exponential time constant) on the roll/pitch/bob reads — fps-independent. ~0.2 velvets residual noise without the boat feeling laggy; 0 = raw samples.")]
        [SerializeField] private float _motionSmoothingSeconds = 0.2f;
        [Tooltip("The wave-field animator's own smoothing: how languidly the train parameters chase the drifting weather, and the glass snap floor (glass is sacred — see WaveFieldAnimator).")]
        [SerializeField] private WaveFieldAnimatorSettings _animatorSettings = WaveFieldAnimatorSettings.Default;

        [Header("Wiring (the builder sets these)")]
        [Tooltip("The child VISUAL transform the motion is applied to (e.g. the FishingBoatVisual sprite child). NEVER the physics root — this is visual-only; the body and colliders must not move. Null = the component idles.")]
        [SerializeField] private Transform _visual;
        [Tooltip("Legacy wiring: the DirectionalBoatSprite driving the same visual. Kept for scene-serialised " +
                 "rigs; at runtime this component reads/writes the hull through IBoatHullPresenter (ADR 0022 " +
                 "phase 4) and this field is only a fallback to wrap. It force-resets the visual's rotation " +
                 "every LateUpdate, so the roll MUST route through the presenter's VisualTiltDegrees hook. If " +
                 "neither is present, the roll is written to the visual's localRotation directly.")]
        [SerializeField] private DirectionalBoatSprite _directionalSprite;

        // The hull as the seam describes it (ADR 0022 phase 4): the rock-grid gate, the RockFrame /
        // continuous-phase channel and the tilt hook all go through here. Set by Configure; a
        // scene-serialised component lazily wraps its legacy _directionalSprite field instead.
        private IBoatHullPresenter _presenter;

        [Header("Wave field (parity: keep identical to the shader bridge until GameConfig unifies them — see class doc)")]
        [SerializeField] private WaveFieldSettings _settings = WaveFieldSettings.Default;

        private readonly WaveFieldAnimator _animator = new WaveFieldAnimator();
        private bool _hasLastTime;
        private double _lastTimeSeconds;

        // fps-independently smoothed motion reads (raw sim units, pre-mapping — the caps stay hard).
        private float _smoothedPitch;
        private float _smoothedRoll;
        private float _smoothedBob;

        private bool _baseCached;
        private Vector3 _baseLocalPosition;
        private Vector3 _baseLocalScale;
        private bool _applied;

        // The rock frame currently drawn (frame-driven path). −1 = static/level hull (calm or off).
        private int _currentRockFrame = -1;

        /// <summary>Master strength, settable at runtime (dev rigs / feel sessions). 0 = off.</summary>
        public float MasterStrength
        {
            get => _masterStrength;
            set => _masterStrength = Mathf.Max(0f, value);
        }

        /// <summary>Configure the wiring from code — the skinner's path (ADR 0022 phase 4): the hull
        /// arrives as the PRESENTER seam, so a sprite compass and a mesh hull are the same call.</summary>
        public void Configure(Transform visual, IBoatHullPresenter hull)
        {
            RestoreVisual();          // if re-wired live, leave the old visual clean
            _visual = visual;
            _presenter = hull;
            // A POCO presenter does not survive scene serialization, and the builders call this at
            // EDIT time — persist the concrete compass (when the presenter wraps one) so a reloaded
            // scene lazily re-wraps it instead of waking with no hull channel and losing the
            // frame-driven rock. A mesh presenter serialises nothing: its rig is runtime-installed
            // by the skinner every time.
            _directionalSprite = (hull as SpriteHullPresenter)?.Directional;
            _baseCached = false;
        }

        /// <summary>Legacy overload (pre-seam callers and tests): wraps the concrete component.</summary>
        public void Configure(Transform visual, DirectionalBoatSprite directionalSprite)
            => Configure(visual, directionalSprite != null
                ? new SpriteHullPresenter(directionalSprite)
                : (IBoatHullPresenter)null);

        /// <summary>The hull presenter this component drives — the configured one, or a lazy wrap of the
        /// legacy serialized <see cref="DirectionalBoatSprite"/> (scene-serialised rigs). May be null.</summary>
        private IBoatHullPresenter Hull =>
            _presenter ??= (_directionalSprite != null ? new SpriteHullPresenter(_directionalSprite) : null);

        private void OnEnable()
        {
            _hasLastTime = false;     // fresh dt baseline — never ease across a disabled gap
            _animator.Reset();        // snap to the live weather on wake, don't chase a stale sea
            _smoothedPitch = 0f;
            _smoothedRoll = 0f;
            _smoothedBob = 0f;
            _currentRockFrame = -1;   // wake on the static/level hull; the first tick picks the wave frame
        }

        private void OnDisable()
        {
            SetRockFrame(-1);         // disabled → static level hull, never a frozen rock frame
            _currentRockFrame = -1;
            RestoreVisual();
        }

        private void LateUpdate() => Tick();

        /// <summary>One wave-motion tick — the LateUpdate body, callable directly so EditMode tests
        /// (where the player loop does not run) can drive the exact production path against a
        /// scripted clock. Same precedent, and same reason, as <see cref="MeshHullDriver.Drive"/>:
        /// the smoothness of this chain is a measured property (ADR 0022 phase 5), and measuring a
        /// re-implementation of it would prove nothing about the component that ships.</summary>
        public void Tick()
        {
            if (_visual == null) return;

            if (!_baseCached)
            {
                _baseLocalPosition = _visual.localPosition;
                _baseLocalScale = _visual.localScale;
                _baseCached = true;
            }

            if (_masterStrength <= 0f)
            {
                if (DrivingRockFrames) SetRockFrame(-1);   // off → static/level hull, not a frozen frame
                RestoreVisual();      // 0 = off, and the visual sits exactly where it was built
                return;
            }

            // Game-time delta for this presentation tick (the clock advances smoothly per frame;
            // a paused clock yields dt 0 and the sea freezes with it, correctly).
            double time = GameServices.Clock != null ? GameServices.Clock.TotalSeconds : Time.timeAsDouble;
            float dt = _hasLastTime ? Mathf.Max(0f, (float)(time - _lastTimeSeconds)) : Time.deltaTime;
            _lastTimeSeconds = time;
            _hasLastTime = true;

            // Tick the eased, phase-continuous field every frame (see the class doc — this replaced
            // the 4 Hz TrainsFrom snapshot whose phase jumped at every refresh). The accumulated
            // phase is baked into the trains, so the surface is sampled at time 0.
            WaveSample wave;
            var env = GameServices.Environment;
            if (env != null)
            {
                EnvironmentSample sample = env.Sample();
                _animator.Tick(dt, sample.WindVector, sample.SeaState01, in _settings, in _animatorSettings);
                wave = _animator.Sample((Vector2)transform.position);
            }
            else
            {
                wave = WaveSample.Flat;   // no sim, no sea — dead still, never stale
            }

            // THE WAVE-COUPLED SPRITE ROCK (the iso dory): when the wired DirectionalBoatSprite carries a
            // rock grid, the visible rock is DRAWN by frame — select the frame from the wave phase under
            // the hull and STOP applying the transform rock below (the frames own the roll/pitch/heave;
            // applying both would double-rock). This keeps the wave SAMPLING above (the input) and only
            // changes the OUTPUT from a transform to a frame index.
            if (DrivingRockFrames)
            {
                DriveRockFrame(wave);
                return;
            }

            BoatWaveMotionSample motion =
                BoatWaveMotionMath.Decompose(wave.Slope, wave.Height, (Vector2)transform.up, _masterStrength);

            // fps-independent output damping (owner: "a smooth rock") — on the raw reads, so the
            // degree/pixel caps in Apply stay hard.
            _smoothedPitch = WaveFieldAnimator.Smooth(_smoothedPitch, motion.Pitch, dt, _motionSmoothingSeconds);
            _smoothedRoll = WaveFieldAnimator.Smooth(_smoothedRoll, motion.Roll, dt, _motionSmoothingSeconds);
            _smoothedBob = WaveFieldAnimator.Smooth(_smoothedBob, motion.Bob, dt, _motionSmoothingSeconds);

            Apply(new BoatWaveMotionSample(_smoothedPitch, _smoothedRoll, _smoothedBob));
        }

        /// <summary>True when the visible rock is drawn BY THE HULL ITSELF — swapping rock frames on a
        /// sprite hull with a grid, or posing the mesh continuously — rather than transforming the
        /// visual. The gate between the hull-owned rock and the legacy transform-rock path.</summary>
        private bool DrivingRockFrames
        {
            get { var hull = Hull; return _driveRockFrames && hull != null && hull.HasRockGrid; }
        }

        /// <summary>
        /// Drive the hull's own rock from the wave under it. Calm (or no live field) holds the
        /// static/level hull (frame −1); otherwise a phase (crest → 90°, trough → 270°) is handed to
        /// the hull — QUANTISED to the nearest frame (with hysteresis) for a sprite hull's baked
        /// grid, or CONTINUOUSLY for a presenter that supports it (the mesh path, ADR 0022 phase 4:
        /// same wave, same swell, no steps). The transform is left at its base pose — the hull owns
        /// the rock, so no roll/pitch/bob is applied (no double-rock), and the roll hook is held at 0.
        ///
        /// <para><b>⚠ The two paths take that phase from DIFFERENT PLACES, deliberately (ADR 0022
        /// phase 5 — the owner's "the rocking was a little stuttery").</b></para>
        /// <list type="bullet">
        ///   <item><b>Continuous (mesh):</b> the dominant train's OWN phase, read forward out of the
        ///   animator (<see cref="WaveFieldAnimator.DominantPhaseDegrees"/>). Strictly monotone and
        ///   constant-rate by construction, because making the phase continuous is exactly what the
        ///   animator is for.</item>
        ///   <item><b>Quantised (sprite):</b> the phase RECONSTRUCTED from the sampled surface
        ///   (<see cref="DoryRockMath.PhaseDegrees"/>), unchanged.</item>
        /// </list>
        /// <para>The reconstruction is an <c>atan2</c> that is only exact for a single PURE SINE
        /// train, and the shipped field is four superposed trains with crest sharpening p = 2.2.
        /// Measured over a 10 s sail it is not monotone at all: it advances 6.4× faster at some
        /// moments than others, reverses on 1.7% of frames, and carries 13.9× the frame-to-frame
        /// ACCELERATION of a clean sinusoid (21.80 vs π/2) — acceleration being what the eye reads as
        /// a pop, the same metric ADR 0022 judged the spike on. An 8-frame quantiser plus 8° of
        /// hysteresis absorbs almost all of that, which is why a sprite hull never showed it and a
        /// mesh hull did the moment it posed the number directly. Forward-reading drops the applied
        /// roll's acceleration ratio to 1.54 — a clean sinusoid measures 1.57.</para>
        /// <para><b>Why the sprite path was left on the reconstruction:</b> moving it would change
        /// the shipped dory's rock, which is a feel the owner has already signed off on — an
        /// owner-visible change, not a defect fix, and so not this PR's to make (rule 8). The numbers
        /// are in the PR so he can call it. Both paths still ride the same field and the same
        /// dominant swell, so the ADR's "mesh and sprite rock on the same sea" holds.</para>
        /// </summary>
        private void DriveRockFrame(in WaveSample wave)
        {
            var hull = Hull;

            // The hull owns the rock: keep the additive roll hook and the transform pose neutral.
            hull.VisualTiltDegrees = 0f;
            RestoreVisual();

            WaveTrains field = _animator.Current;
            if (field.Count <= 0 || field.TotalAmplitude <= Mathf.Max(0f, _calmAmplitudeThreshold))
            {
                _currentRockFrame = -1;           // glass / near-calm → static level hull, no phantom rock
                hull.RockFrame = -1;
                return;
            }

            if (hull.SupportsContinuousRock)
            {
                // Continuous: the dominant swell's own phase — no frame rounding and no hysteresis
                // (hysteresis exists to stop frame FLIP-FLOP, and there are no frames to flip
                // between). The calibration nudge still applies: it is the art's crest alignment,
                // and both paths share it.
                hull.SetRockPhaseDegrees(
                    _animator.DominantPhaseDegrees((Vector2)transform.position) + _crestFrameCalibrationDegrees);
                _currentRockFrame = -1;
                return;
            }

            WaveTrain dominant = field[0];
            float waveNumber = (2f * Mathf.PI) / Mathf.Max(WaveTrain.MinWavelengthMeters, dominant.Wavelength);
            float phaseDeg = DoryRockMath.PhaseDegrees(wave.Height, wave.Slope, dominant.Direction, waveNumber);

            _currentRockFrame = DoryRockMath.AdvanceFrame(
                _currentRockFrame, phaseDeg, _rockFrameCount, _crestFrameCalibrationDegrees, _frameHysteresisDegrees);
            hull.RockFrame = _currentRockFrame;
        }

        private void SetRockFrame(int frame)
        {
            var hull = Hull;
            if (hull != null) hull.RockFrame = frame;
        }

        /// <summary>Map the smoothed decomposition onto the visual: roll → additive z-rotation (through
        /// the DirectionalBoatSprite hook when present — it stomps rotation every LateUpdate); pitch+bob →
        /// a screen-vertical (world +Y) offset so the lift always reads UP on screen regardless of the
        /// body's physics yaw; |pitch| → a subtle y-squash. Everything clamped to its cap.</summary>
        private void Apply(in BoatWaveMotionSample motion)
        {
            float rollDegrees = Mathf.Clamp(motion.Roll * _rollDegreesPerSlope, -_maxRollDegrees, _maxRollDegrees);
            float pitchOffset = Mathf.Clamp(motion.Pitch * _pitchOffsetPerSlope, -_maxPitchOffset, _maxPitchOffset);
            float bob = Mathf.Clamp(motion.Bob * _bobPerHeightMeter, -_maxBob, _maxBob);
            float squash = Mathf.Min(Mathf.Abs(motion.Pitch) * Mathf.Max(0f, _pitchSquashPerSlope),
                                     Mathf.Max(0f, _maxPitchSquash));

            var hull = Hull;
            if (hull != null)
            {
                hull.VisualTiltDegrees = rollDegrees;   // composed after the hull's rotation reset
            }
            else
            {
                _visual.localRotation = Quaternion.Euler(0f, 0f, rollDegrees);
            }

            // Base local pose first, then the offset in WORLD Y: the visual child may be
            // counter-rotated to stay screen-aligned while its parent (the physics body) carries
            // yaw, so a local-Y offset would swing with the hull — screen-up must be world-up.
            _visual.localPosition = _baseLocalPosition;
            _visual.position += new Vector3(0f, bob + pitchOffset, 0f);
            _visual.localScale = new Vector3(_baseLocalScale.x, _baseLocalScale.y * (1f - squash), _baseLocalScale.z);
            _applied = true;
        }

        /// <summary>Put the visual back exactly as built (and zero the tilt hook). Idempotent.</summary>
        private void RestoreVisual()
        {
            var hull = Hull;
            if (hull != null) hull.VisualTiltDegrees = 0f;
            if (!_applied) return;
            _applied = false;
            if (_visual == null || !_baseCached) return;
            _visual.localPosition = _baseLocalPosition;
            _visual.localScale = _baseLocalScale;
            if (hull == null) _visual.localRotation = Quaternion.identity;
        }
    }
}
