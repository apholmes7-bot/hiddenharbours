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
    /// this component reads the dominant swell's phase forward out of the animator and rounds it to a
    /// frame (<see cref="DoryRockMath"/>) — crest → 2, trough → 6 — so the hand-drawn roll/pitch/heave tracks the swell in
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
    /// <para><b>ONE settings instance (ADR 0018 §(5)).</b> The field's shape comes from
    /// <c>GameConfig.WaveField</c> through <see cref="GameServices"/> — the very instance the Art-side
    /// <c>WaveFieldBridge</c> publishes to the water shader — so the hull rocks on the waves the
    /// player sees by construction. Tune the FIELD on the config; only the RESPONSE amplitudes (how
    /// hard this hull answers) live here.</para>
    ///
    /// <para><b>THE STORM ANSWERS BACK (ADR 0018 B2.5 — owner 2026-08-05: "the boat just seemed to
    /// not be responsive correctly to the waves… it must stay smooth and obey gravity though").</b>
    /// Every path above had a fixed amplitude the sea could not grow: the rock grid and the mesh's
    /// def rock cycle are PHASE-only (a gale drew the chop's attitude, only slower — the storm's
    /// longer swell literally read calmer), and the transform gains pinned against caps sized on a
    /// calm-tuned feel pass while the field's slope itself saturates (wavelength grows with wind, so
    /// amplitude × wave number flattens above mid-sea). B2.5 scales the whole read by the
    /// deterministic sea-state axis (<see cref="StormRockMath"/>, policy on
    /// <c>GameConfig.StormRock</c>): gains AND caps grow toward a storm ceiling, a mesh hull gains
    /// REAL heading-decomposed storm attitude through <see cref="IBoatHullPresenter.SetStormRock"/>
    /// (a head sea pitches the bow — continuously, the one path that can draw it), a sprite-frame
    /// hull gains the honest surge (offset + squash UNDER the frames — the baked attitude itself
    /// cannot scale; a steeper bake is an art-director call), output smoothing tightens with the
    /// blend (velvet is for calm), and the displaced ride carries WEIGHT — a gravity-capped
    /// spring-damper chase (<see cref="StormRockMath.StepHeaveWeight"/>): over a sharpened crest the
    /// surface can drop faster than g and the hull now unweights, falls at g, and lands, instead of
    /// being bolted to it. Below the storm-start sea state the blend is exactly 0 and every output
    /// is byte-identical to the owner's tuned calm — the negative control the EditMode tests pin.
    /// Per-hull character rides the hull's existing <c>BoatHullDef</c> seakeeping data via the
    /// sibling <see cref="BoatController"/>; a boat with none reads neutral.</para>
    ///
    /// <para><b>AND SHE SETTLES AT HER WATERLINE (owner playtest 2026-08-07: "the hulls of most
    /// boats do not tend to stay at the waterline level … generally they should level out at the
    /// boats water line").</b> The bounce was never the defect — the owner said in the same breath
    /// that heaving out of the water is normal and wanted, and B2.5's loft is untouched. What was
    /// wrong was the LEVEL she returns to, in two independent ways, both of them MEAN errors that no
    /// amount of heave averages away. (1) The sink was applied raw, but the shared z-buffer draws the
    /// sea climbing a PROJECTION GAIN of planking per metre of sink (<c>sin·(cos+sin)</c> = 0.9056
    /// at the fleet's 40° bake since ADR 0033 re-derived it; <c>(cos+sin)/(cos²+sin)</c> = 1.1457
    /// before that), so every hull floated at a multiple of what her own data claimed —
    /// <see cref="HullSettleMath"/> inverts the projection, whichever the gain is. (2) This component ticked its OWN
    /// <see cref="WaveFieldAnimator"/>, whose accumulated travel phase starts at zero on ITS wake
    /// while the water's starts at zero on the bridge's, so the hull heaved on a sea correct in every
    /// respect except WHEN — it now reads the published field through
    /// <see cref="SharedWaveField"/> and rides the very trains the shader was handed.</para>
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

        // ---- the MESH hull's ordinary-sea head-sea pitch (owner report 2026-08-11: "the fore/aft
        // rocking reads as minimal") ------------------------------------------------------------
        //
        // The four knobs ABOVE are the SPRITE path's: a screen-vertical offset plus a squash, applied
        // to the transform. A mesh hull never sees them — DriveRockFrame stops applying the transform
        // roll/pitch/bob entirely ("the hull owns the rock"), so its whole ordinary-sea pitch is the
        // rig's CANNED cycle, and that cycle has two properties the owner is reading:
        //
        //   1. It is roll-DOMINANT by authorship. Every rig declares pitchA ≈ 0.55–0.60 × rollA
        //      (dory 3.0/5.0, punt 2.4/4.2, sport skiff 2.2/3.8, lobster boat 1.6/2.8, side dragger
        //      1.1/2.0, tanker 0.40/0.85). At the fleet's 40° bake a dory's 3° over her 2.25 m
        //      half-length lifts the stem 2.25·sin3°·cos40° ≈ 0.090 m ≈ 2.9 px; the lobster boat's
        //      1.6° over 6.0 m ≈ 4.1 px. A ~6–8 px see-saw on a hull 150–370 px long.
        //   2. It is heading-BLIND. RockPose is roll = rollA·sin(θ), pitch = pitchA·cos(θ) — a fixed
        //      corkscrew that draws the same pitch bows-on to the swell as beam-on to it.
        //
        // The honest heading-decomposed attitude the sea deserves DOES exist (the B2.5 storm extras
        // below, at MeshStormPitchDegreesPerSlope) — but it is multiplied by StormBlend01, which is
        // exactly 0 below StormStartSeaState01 = 0.4. So in ordinary seas it never reaches the pose.
        //
        // These two fields open that channel below the storm gate, and NOTHING ELSE: the term rides
        // cos() of the very phase the canned pitch is already posed at, so the sum is pure AMPLITUDE
        // modulation of the existing waveform — no new frequency content for the smoothness pins to
        // catch (MeshRockSmoothness), which is the trap the first B2.5 cut fell into by driving the
        // extras from a different frequency mix.
        //
        // ⚠️ The rig's own RockPitchDegrees is NOT the knob to turn: HullMeshDef files it under
        // "Pose facts (per-artwork; measured or read off the rig — NEVER tuned)". Raising it would
        // falsify the transcription the golden master is built on. Per-hull scaling instead comes
        // from SeakeepingResponse.Response (BoatHullDef's own liveliness/mass factor, already in
        // DriveRockFrame's hand) — a dory answers, a laden trader shrugs.
        [Header("Mesh pitch (head sea → the hull's bow rides the face, below the storm gate)")]
        [Tooltip("Degrees of MESH-hull pitch per unit of bow-axis wave slope, in ORDINARY seas — the " +
                 "half the canned rock cycle cannot draw, because the cycle is heading-blind.\n\n" +
                 "0 = OFF, and off is byte-identical to before this existed: the decomposition is not " +
                 "even computed, and the pose is the canned cycle alone. That is the owner's A/B — " +
                 "set it to 0 and the fleet pitches exactly as it shipped.\n\n" +
                 "Scale: the shipped field's dominant slope runs ~0.1 in a working sea and ~0.5–1 in " +
                 "a gale, so 14 (the storm channel's own tuned degrees-per-slope) adds ≈1.4° bows-on " +
                 "to a moderate swell — roughly doubling a lobster boat's 1.6° canned pitch and making " +
                 "it answer the heading. Scaled per hull by SeakeepingResponse and clamped below.")]
        [SerializeField, Min(0f)] private float _meshHeadSeaPitchDegreesPerSlope = 0f;
        [Tooltip("Hard cap on the ordinary-sea mesh pitch (degrees), before the storm extras add on " +
                 "top. Caps the AMPLITUDE, never the instantaneous value — clipping the wave itself " +
                 "would flat-top it and the smoothness metrics would read the harmonics as a pop.")]
        [SerializeField, Min(0f)] private float _maxMeshHeadSeaPitchDegrees = 4f;

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
        // The wave-field animator's smoothing comes from GameConfig.WaveFieldAnimator via GameServices
        // (ADR 0018 §(5)). _motionSmoothingSeconds above stays per-component: it is this component's
        // OUTPUT damping on the roll/pitch/bob read, not a property of the sea.

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

        // Wave field: GameConfig.WaveField via GameServices (ADR 0018 §(5)) — the hull now rocks on the
        // shader bridge's sea by construction, which is what the old "keep identical" header asked for
        // and could not enforce.

        private readonly WaveFieldAnimator _animator = new WaveFieldAnimator();
        private bool _hasLastTime;
        private double _lastTimeSeconds;

        // fps-independently smoothed motion reads (raw sim units, pre-mapping — the caps stay hard).
        private float _smoothedPitch;
        private float _smoothedRoll;
        private float _smoothedBob;

        // The weight filter's per-boat state (ADR 0018 B2.5) — presentation-only, reset on wake.
        private HeaveWeightState _heaveWeight;

        // The sibling BoatController (same root), resolved lazily ONCE per wiring — the hull's
        // seakeeping character (BoatHullDef data) scales the storm read per hull. Null (an ambient
        // boat, a bare rig) reads neutral. UnityEngine.Object null semantics respected (no ?.).
        private BoatController _controller;
        private bool _controllerResolved;

        private bool _baseCached;
        private Vector3 _baseLocalPosition;
        private Vector3 _baseLocalScale;
        private bool _applied;

        // The rock frame currently drawn (frame-driven path). −1 = static/level hull (calm or off).
        private int _currentRockFrame = -1;

        // The rock PHASE the hull is drawing this tick — the read anything standing ON this deck needs.
        // DeckRideMath.LevelPhaseDegrees while calm / off / disabled.
        private float _rockPhaseDegrees = DeckRideMath.LevelPhaseDegrees;

        /// <summary>
        /// <b>The rock this hull is DRAWING right now, as a wave phase</b> (degrees; crest 90°, trough
        /// 270°) — or <see cref="DeckRideMath.LevelPhaseDegrees"/> when she is level: glass calm, the
        /// master strength off, or this component disabled.
        ///
        /// <para><b>Why the phase and not the pose.</b> This component is the one place that knows which of
        /// the rock paths is live (baked frames or continuous mesh), but the visible AMPLITUDE is an art
        /// fact of whatever is being drawn — the dory's sheet bakes 5° of roll and 1.6 px of heave, a mesh
        /// hull's is a transform. So the PHASE is what travels, and every rider owns its own tuned
        /// amplitudes against it, exactly as <see cref="DoryOarLayer"/> already does for the oars. One
        /// clock, many hands.</para>
        ///
        /// <para>Read-only and presentation-only: nothing here feeds the sim (rule 5). It is published from
        /// the same tick that poses the hull, so a reader ordered AFTER this component (−120) sees a
        /// settled pose rather than last frame's — the same discipline the oar layers keep.</para>
        ///
        /// <para><b>A component that is not RUNNING reports level, whatever its last field held.</b>
        /// <c>OnDisable</c> already clears the field, but the gate is what makes the answer true rather
        /// than merely usually-true: it holds for a component disabled before it ever woke, for one on a
        /// deactivated GameObject, and for any reader that polls at a moment the callback has not reached.
        /// A hull that is not ticking cannot be drawing a rock, so it must not be able to say it is —
        /// otherwise a stale phase leans a passenger on a deck nothing is moving.</para>
        /// </summary>
        public float RockPhaseDegrees =>
            isActiveAndEnabled ? _rockPhaseDegrees : DeckRideMath.LevelPhaseDegrees;

        /// <summary>True when this hull is actually rocking — i.e. <see cref="RockPhaseDegrees"/> names a
        /// live point in the cycle rather than the level pose. Reads the GATED property, not the raw field,
        /// so the two can never disagree about a component that is not running.</summary>
        public bool IsRocking => DeckRideMath.IsRocking(RockPhaseDegrees);

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
            _controllerResolved = false;   // a re-wire may be a different boat — re-find its helm
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
            _heaveWeight = default;   // the weight chase wakes ON the live water, never a stale sea
            _currentRockFrame = -1;   // wake on the static/level hull; the first tick picks the wave frame
            _rockPhaseDegrees = DeckRideMath.LevelPhaseDegrees;   // …and so does anyone standing on her
        }

        private void OnDisable()
        {
            SetRockFrame(-1);         // disabled → static level hull, never a frozen rock frame
            _currentRockFrame = -1;
            _rockPhaseDegrees = DeckRideMath.LevelPhaseDegrees;   // never a frozen LEAN on her passengers either
            var hull = Hull;
            if (hull != null)
            {
                hull.SetDisplacedHeaveMeters(0f);   // never a frozen ride either (draft is the driver's own gate)
                hull.SetStormRock(1f, 0f, 0f);      // …and never a frozen storm pose (1/0/0 = the exact neutral)
                // …and nobody left standing on a crest that is gone. A MESH hull ignores this by
                // contract and goes on reporting the waterline her driver still draws her at, which
                // is right: her picture did not move just because this component stopped ticking.
                hull.SetDrawnRideMeters(0f);
            }
            _heaveWeight = default;
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
                _rockPhaseDegrees = DeckRideMath.LevelPhaseDegrees;   // off = a level deck to stand on, too
                if (DrivingRockFrames) SetRockFrame(-1);   // off → static/level hull, not a frozen frame
                var offHull = Hull;
                if (offHull != null)
                {
                    offHull.SetDisplacedHeaveMeters(0f);   // off = no ride either (the whole read is off)
                    offHull.SetStormRock(1f, 0f, 0f);      // off = no storm pose either (the exact neutral)
                    offHull.SetDrawnRideMeters(0f);        // off = a passenger stands where they were
                }
                _heaveWeight = default;
                RestoreVisual();      // 0 = off, and the visual sits exactly where it was built
                return;
            }

            // Game-time delta for this presentation tick (the clock advances smoothly per frame;
            // a paused clock yields dt 0 and the sea freezes with it, correctly).
            double time = GameServices.Clock != null ? GameServices.Clock.TotalSeconds : Time.timeAsDouble;
            float dt = _hasLastTime ? Mathf.Max(0f, (float)(time - _lastTimeSeconds)) : Time.deltaTime;
            _lastTimeSeconds = time;
            _hasLastTime = true;

            // Tick the LOCAL eased field every frame (see the class doc — this replaced the 4 Hz
            // TrainsFrom snapshot whose phase jumped at every refresh). It is now the FALLBACK: the
            // sea actually ridden is whatever the bridge published to SharedWaveField (see
            // DrawnTrains), and this tick exists so a run with no bridge behaves byte-identically
            // and so the fallback is never a stale sea the moment a publisher goes away.
            WaveSample wave;
            float seaState01 = 0f;                            // no sim = glass = no storm (blend 0)
            float fetchEnvelope = 1f;                         // resolved ONCE per tick (rule 7)
            WaveFieldSettings field = GameServices.WaveField; // also supplies g to the weight filter
            var env = GameServices.Environment;
            if (env != null)
            {
                EnvironmentSample sample = env.Sample();
                seaState01 = sample.SeaState01;
                WaveFieldAnimatorSettings smoothing = GameServices.WaveFieldAnimator;
                _animator.Tick(dt, sample.WindVector, sample.SeaState01, in field, in smoothing);
                // The WIND-FETCH envelope (ADR 0027 #1), resolved once at the hull's centre and
                // shared by the surface sample below AND the storm-attitude envelope — the march is
                // real terrain reads, and the two consumers must see the same lee.
                fetchEnvelope = GameServices.FetchEnvelopeAt((Vector2)transform.position);
                wave = SampleTheDrawnSea((Vector2)transform.position, fetchEnvelope);
            }
            else
            {
                wave = WaveSample.Flat;   // no sim, no sea — dead still, never stale
            }

            // THE SHARED HEAVE (ADR 0023 phase 3 step 2): while the displaced sea is active, every
            // hull's vertical ride is the SAME displaced height rule the surface lifts with — the
            // one wave sample already in hand, through ShoreFadeMath.DisplacedHeight with the
            // surface's own published exaggeration + shore-fade band (the Core seam; never a
            // per-consumer copy — the overlay-pose lesson). Depth is the game's one depth rule
            // (BoatCrossing.DepthAt: water level − painted ground; open water = +∞ ⇒ fade 1), so a
            // boat nosing into the shallows settles exactly as the water under it does. Displaced
            // OFF ⇒ ride 0 and rideActive false: every path below is byte-identical to before.
            float rideMeters = DisplacedRideMeters(wave.Height, time, out bool rideActive);

            // THE STORM READ (ADR 0018 B2.5). One blend off the deterministic sea-state axis drives
            // everything below: 0 at/below the storm start (every output byte-identical to the tuned
            // calm — multiplying by exactly 1f is the identity), growing to the storm ceiling at sea
            // state 1. The WEIGHT filter then puts mass under the displaced ride: a gravity-capped
            // spring chase of the surface (the owner's "obey gravity"), engaged only with the blend,
            // exact passthrough otherwise. Displaced OFF keeps the raw path AND parks the filter, so
            // the A/B off side stays byte-identical and no stale chase leaks into the next activation.
            StormRockSettings storm = GameServices.StormRock;
            float stormBlend = StormRockMath.StormBlend01(seaState01, in storm);
            float response = StormRockMath.ResponseMultiplier(stormBlend, in storm);
            SeakeepingResponse hullCharacter = ResolveHullCharacter();
            if (rideActive)
            {
                rideMeters = StormRockMath.StepHeaveWeight(
                    ref _heaveWeight, rideMeters, dt, field.Gravity,
                    stormBlend * Mathf.Clamp01(storm.HeaveWeight01), in hullCharacter, in storm);
            }
            else
            {
                _heaveWeight = default;
            }

            // ---- THE BORE'S LIFT (ADR 0040 revision 3) -------------------------------------------
            // Broken water does not only shove a hull shoreward, it picks her up: the wash arrives, the
            // level under her rises, she goes up with it and comes down as it drains. That rise is the
            // bore's run-up level, solved on the physics tick and read here (never re-solved — see
            // BoatController.SurfUnderHull), and it goes onto the SAME ride the displaced sea already
            // moves her by, so her passengers get it through DrawnRideMeters without knowing it exists.
            //
            // AFTER the weight filter on purpose. That filter is a gravity-capped spring chase of the
            // SWELL, engaged only in a storm; the bore is already a smooth periodic pulse with its own
            // physical clock, and running it through a stateful smoother would hand it a second one — a
            // smoother agrees only with itself. Gated on rideActive so the displaced-OFF side of the A/B
            // stays byte-identical, exactly as every other term of this ride is.
            if (rideActive) rideMeters += SurfLiftMeters();

            // Decomposed + smoothed BEFORE the path split: the transform path maps these onto the
            // visual as before, and the hull-owned paths now read the same smoothed slope for their
            // storm attitude/surge. The smoothing TIGHTENS with the storm blend (velvet is for calm;
            // a storm is violent) — still fps-independent, still continuous, and at storm periods
            // (~4 s) even the calm constant attenuates nothing: the tightening sharpens response to
            // the short chop riding on the swell, it never launders the storm amplitude away.
            BoatWaveMotionSample motion =
                BoatWaveMotionMath.Decompose(wave.Slope, wave.Height, (Vector2)transform.up, _masterStrength);
            float smoothingSeconds =
                StormRockMath.TightenedSmoothingSeconds(_motionSmoothingSeconds, stormBlend, in storm);
            _smoothedPitch = WaveFieldAnimator.Smooth(_smoothedPitch, motion.Pitch, dt, smoothingSeconds);
            _smoothedRoll = WaveFieldAnimator.Smooth(_smoothedRoll, motion.Roll, dt, smoothingSeconds);
            _smoothedBob = WaveFieldAnimator.Smooth(_smoothedBob, motion.Bob, dt, smoothingSeconds);

            // THE WAVE-COUPLED SPRITE ROCK (the iso dory): when the wired DirectionalBoatSprite carries a
            // rock grid, the visible rock is DRAWN by frame — select the frame from the dominant swell's
            // phase under the hull and STOP applying the transform rock below (the frames own the
            // roll/pitch/heave; applying both would double-rock). The animator was ticked above; the
            // hull path reads its phase forward rather than the sampled surface.
            if (DrivingRockFrames)
            {
                DriveRockFrame(rideMeters, stormBlend, response, in hullCharacter, in storm,
                               fetchEnvelope);
                return;
            }

            // The LEGACY TRANSFORM path draws no rock CYCLE — it maps the smoothed slope straight onto the
            // visual — so there is no phase to publish and none is invented. (Reconstructing one from the
            // sampled surface is the atan2 inversion ADR 0022 phase 5 measured as non-monotone; a rider
            // fed that would judder.) A deck rider on such a hull reads the applied tilt off the presenter
            // instead — see DeckRiderVisual.
            _rockPhaseDegrees = DeckRideMath.LevelPhaseDegrees;

            Apply(new BoatWaveMotionSample(_smoothedPitch, _smoothedRoll, _smoothedBob),
                  rideMeters, rideActive, response);
        }

        /// <summary>
        /// The hull's seakeeping character for the VISUAL storm read — the sibling
        /// <see cref="BoatController"/>'s <c>BoatHullDef</c> data through the same
        /// <see cref="BoatController.ResponseFor"/> resolution B3 uses (one per-hull truth, never a
        /// second knob). A boat with no controller or no hull def (the ambient fleet's decor rigs, a
        /// bare test rig) reads NEUTRAL (response 1) — deliberately not
        /// <see cref="SeakeepingResponse.Inert"/>, which is the FORCE model's "unmoved by the sea"
        /// and would zero the storm read on every extra. Resolved once per wiring
        /// (<see cref="Configure"/> re-arms it); plain <c>!= null</c> against the component so a
        /// destroyed sibling degrades to neutral instead of throwing (the Unity fake-null rule).
        /// </summary>
        private SeakeepingResponse ResolveHullCharacter()
        {
            if (!_controllerResolved)
            {
                _controller = GetComponent<BoatController>();
                _controllerResolved = true;
            }
            return _controller != null && _controller.Hull != null
                ? BoatController.ResponseFor(_controller.Hull)
                : new SeakeepingResponse(1f, 0f);
        }

        /// <summary>
        /// The metres of lift the bore under this hull is giving her this frame (ADR 0040 revision 3) —
        /// <see cref="SeakeepingForcesMath.SurfLiftMeters"/> on the surf state her own
        /// <see cref="BoatController"/> solved on its last physics tick, under that controller's resolved
        /// seakeeping policy (the owner's dials, wherever they came from — the wired GameConfig, the
        /// shared one, or the serialized fallback; a runtime-spawned hull follows the asset since #682).
        ///
        /// <para>Read, never re-solved. Solving the surf again here would mean a second contour inversion
        /// and a second 16-tap march per hull per FRAME — and, worse, a lift that could disagree with the
        /// shove it is supposed to be the other half of.</para>
        ///
        /// <para>0 for a hull with no controller (the ambient fleet's decor rigs, a bare test rig): they
        /// are not in anyone's surf, and the same neutral reading <see cref="ResolveHullCharacter"/>
        /// gives them. Plain <c>!= null</c> against the component — the Unity fake-null rule.</para>
        /// </summary>
        private float SurfLiftMeters()
        {
            if (!_controllerResolved)
            {
                _controller = GetComponent<BoatController>();
                _controllerResolved = true;
            }
            if (_controller == null) return 0f;
            return SeakeepingForcesMath.SurfLiftMeters(_controller.SurfUnderHull, _controller.SeakeepingPolicy);
        }

        /// <summary>
        /// Sample the wave field <b>as the surface DRAWS it</b> — at the displaced sea's published
        /// frequency scale, not at 1.
        ///
        /// <para>⚠️ <b>This is the "boats are nearly submerged" fix</b> (owner, 2026-07-27). The
        /// surface's vertex stage samples the field at <c>_OceanSwellScale / 0.025</c> — <b>2.8</b> at
        /// the owner's tuned materials. This read was at 1. Same amplitude envelope, wavelengths 2.8×
        /// shorter: the hull was riding a <em>different wave</em> from the one drawn around it, so the
        /// two agreed only by coincidence and the drawn water routinely stood at a crest while the
        /// hull sat in a trough — and climbed over it. It is the same <c>_OceanSwellScale</c> incident
        /// that already bit the watertight clamp (fixed there in ADR 0023 phase 3, scanning at
        /// <c>WaterIsoDepthFrame.FreqScale</c>); the RIDE was the half that was never closed, because
        /// its only route to the value is the Core seam and the seam did not carry it until now.</para>
        ///
        /// <para><b>Why scale the POSITION rather than pass a scale.</b> The phase is
        /// <c>θ = k·s·(dir·pos) + φ</c>, which is identically <c>θ</c> at <c>pos·s</c> — so sampling
        /// the field at a scaled position gives the drawn wave exactly, with no change to
        /// <c>WaveMath</c> (a shared water file this lane must not touch). The HEIGHT and the crest
        /// factor come out exact; only the SLOPE needs the chain-rule factor <c>s</c> put back, since
        /// <c>d/dpos</c> of a scaled argument loses it.</para>
        ///
        /// <para>Displaced sea off ⇒ scale 1 ⇒ byte-identical to before (the A/B contract).</para>
        /// </summary>
        private WaveSample SampleTheDrawnSea(Vector2 worldPos, float fetchEnvelope01)
        {
            float s = DisplacedSea.TryGet(out DisplacedSeaState sea) ? sea.FreqScale : 1f;
            WaveTrains trains = DrawnTrains;
            // The WIND-FETCH envelope (ADR 0027 #1) — resolved by the CALLER at the TRUE world
            // position, never the freqScale-scaled one: freqScale is a wavelength trick, fetch is a
            // real distance over real water, and marching the scaled position would shelter the hull
            // behind a headland that is not there. Exactly 1 (and no terrain reads at all) while the
            // model is off. Hoisted to Tick so the storm-attitude envelope shares the same resolve
            // (one march per tick — rule 7).
            //
            // Sampled at timeSeconds = 0: the accumulated travel already rides in each train's
            // PhaseOffset (the animator's output convention, and the convention the published field
            // inherits). Passing the real game time would add the travel a second time.
            if (s == 1f) return WaveMath.Sample(worldPos, 0.0, in trains, fetchEnvelope01);

            WaveSample raw = WaveMath.Sample(worldPos * s, 0.0, in trains, fetchEnvelope01);
            return new WaveSample(raw.Height, raw.Slope * s, raw.CrestFactor);
        }

        /// <summary>
        /// <b>The eased trains this hull actually rides</b> — the field the <c>WaveFieldBridge</c>
        /// published to <see cref="SharedWaveField"/> beside the shader push, or, when nothing has
        /// published, this component's own local animator exactly as before.
        ///
        /// <para>⚠️ <b>This is the second half of the one-sea rule, and it is about WHEN, not what</b>
        /// (owner playtest 2026-08-07). <see cref="WaveFieldAnimator"/> accumulates travel phase from
        /// zero at its own first tick and at every <c>Reset</c> — and this component resets in
        /// <c>OnEnable</c>: on skin, on region load, on hull swap, on any re-enable. The bridge resets
        /// on ITS wake and on every scene load. Two accumulators seeded at two different moments hold
        /// two different Φ, so the hull rode a sea of exactly the right size, period and direction, at
        /// an arbitrary phase offset from the one drawn around her — she lifted as the water fell, and
        /// no flotation datum on earth could make her settle at her waterline. ADR 0023 closed the
        /// WAVELENGTH half of this at <see cref="DisplacedSeaState.FreqScale"/> (#331); this is the
        /// phase half, and it is the last one.</para>
        ///
        /// <para>The local animator is still ticked every frame, so the fallback is never stale and a
        /// run with no bridge (EditMode, a bare demo, an edit-time builder) behaves byte-identically
        /// to before the seam. Cost is unchanged: it is the tick this component already paid.</para>
        /// </summary>
        private WaveTrains DrawnTrains =>
            SharedWaveField.TryGet(out WaveTrains published) ? published : _animator.Current;

        /// <summary>The DOMINANT (spectral-peak) train's own phase at a world position, off
        /// <see cref="DrawnTrains"/> — character-for-character <see cref="WaveFieldAnimator.DominantPhaseDegrees"/>,
        /// pointed at the shared sea. Returns 0 on an empty field, exactly as that method does;
        /// callers gate calm on the envelope, not on this.</summary>
        private float DrawnDominantPhaseDegrees(Vector2 worldPos)
        {
            WaveTrains trains = DrawnTrains;
            return trains.Count > 0 ? WaveMath.TrainPhaseDegrees(trains.Dominant, worldPos, 0.0) : 0f;
        }

        /// <summary>
        /// The displaced-sea ride under this hull (metres of screen lift): 0 with the displaced
        /// sea OFF (<paramref name="active"/> false — the A/B contract), else the ONE shared rule
        /// every displaced-water consumer draws with, on the same wave sample the rock already
        /// reads (the ONE-SEA rule — never a second sim).
        /// </summary>
        private float DisplacedRideMeters(float waveHeightMeters, double totalSeconds, out bool active)
        {
            active = DisplacedSea.TryGet(out DisplacedSeaState sea);
            if (!active) return 0f;
            float depth = BoatCrossing.DepthAt(GameServices.TidalTerrain, GameServices.Environment,
                                               totalSeconds, (Vector2)transform.position);
            return ShoreFadeMath.DisplacedHeight(waveHeightMeters, depth,
                                                 sea.ShoreFadeBandMeters, sea.Exaggeration);
        }

        /// <summary>
        /// <b>The level a TRANSFORM-ridden hull settles at</b> — the sea's lift under her, less the
        /// sink her own design waterline demands (<see cref="HullSettleMath"/>, off the presenter
        /// seam's <see cref="IBoatHullPresenter.DesignWaterlineMeters"/> and
        /// <see cref="IBoatHullPresenter.BakeElevationDegrees"/>).
        ///
        /// <para><b>Only the transform paths route through here.</b> A MESH hull's ride leaves this
        /// component as the raw sea lift through
        /// <see cref="IBoatHullPresenter.SetDisplacedHeaveMeters"/> and is sunk inside
        /// <see cref="MeshHullDriver"/> — deliberately, so a mesh hull with no wave motion wired at
        /// all still floats at her waterline. Sinking it here as well would sink her twice.</para>
        ///
        /// <para>A null presenter, or one carrying no datum (0 — every sprite compass shipped so
        /// far, whose art is already drawn at her waterline), returns the lift unchanged: the
        /// byte-identical pre-fix path.</para>
        /// </summary>
        private static float SettleRideMeters(float surfaceLiftMeters, IBoatHullPresenter hull)
            => hull == null
                ? surfaceLiftMeters
                : HullSettleMath.SettleRideMeters(surfaceLiftMeters, hull.DesignWaterlineMeters,
                                                  hull.BakeElevationDegrees);

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
        /// same wave, same swell, no steps). The hull owns the rock, so no roll/pitch/bob is applied
        /// to the transform (no double-rock) and the roll hook is held at 0; the transform writes on
        /// the sprite path are the displaced-sea RIDE (ADR 0023 phase 3 step 2 — zero with the
        /// displaced sea off, so the flat-water pose stays at base exactly as before) plus, in a
        /// storm only, the B2.5 SURGE offset + squash layered UNDER the frames (exactly zero below
        /// the storm-start sea state — the calm pose is untouched).
        ///
        /// <para><b>Both paths take that phase from the SAME PLACE: the dominant train's OWN phase,
        /// read forward out of the animator</b> (<see cref="WaveFieldAnimator.DominantPhaseDegrees"/> —
        /// strictly monotone and constant-rate by construction, because making the phase continuous
        /// is exactly what the animator is for). The mesh poses it directly; the sprite rounds it to
        /// the sheet's frames through <see cref="DoryRockMath.AdvanceFrame"/>, whose quantisation
        /// (frame count, calibration, hysteresis) is untouched.</para>
        /// <para><b>History (ADR 0022 phase 5 — the owner's "the rocking was a little stuttery").</b>
        /// Both paths once RECONSTRUCTED the phase from the sampled surface
        /// (<see cref="DoryRockMath.PhaseDegrees"/>).
        /// The reconstruction is an <c>atan2</c> that is only exact for a single PURE SINE
        /// train, and the shipped field is four superposed trains with crest sharpening p = 2.2.
        /// Measured over a 10 s sail it is not monotone at all: it advances 6.4× faster at some
        /// moments than others, reverses on 1.7% of frames, and carries 13.9× the frame-to-frame
        /// ACCELERATION of a clean sinusoid (21.80 vs π/2) — acceleration being what the eye reads as
        /// a pop, the same metric ADR 0022 judged the spike on. An 8-frame quantiser plus 8° of
        /// hysteresis absorbs MOST of that — but not all: pushed through the 8-frame rounding the
        /// reconstruction dwells and even steps the frame BACKWARDS instead of cycling
        /// (<c>SpriteRockFrameCycleTests</c> measures it — the dory sitting on one or two rock frames
        /// the owner reported). #243 moved the mesh to the forward read; on the owner's 2026-07-22
        /// ruling the sprite path followed. Only the phase SOURCE changed — both hulls still ride the
        /// same field and the same dominant swell, so the ADR's "mesh and sprite rock on the same
        /// sea" holds.</para>
        /// </summary>
        private void DriveRockFrame(float rideMeters, float stormBlend, float responseMultiplier,
                                    in SeakeepingResponse hullCharacter, in StormRockSettings storm,
                                    float fetchEnvelope01)
        {
            var hull = Hull;

            // The hull owns the rock: keep the additive roll hook neutral.
            hull.VisualTiltDegrees = 0f;

            WaveTrains field = DrawnTrains;   // the published sea, or the local animator's fallback
            bool calm = field.Count <= 0 ||
                        field.TotalAmplitude <= Mathf.Max(0f, _calmAmplitudeThreshold);

            // The shared displaced ride (ADR 0023 phase 3 step 2; 0 with the sea off — and now
            // WEIGHTED by the caller in a storm). A continuous-rock hull (the mesh) takes it
            // through the presenter seam — the driver folds it into the heave-pixels channel, so
            // the screen lift and the calibrated waterline z move together — and its transform
            // stays at base (routing it through the transform TOO would double-ride). A sprite hull
            // has no waterline clipping: its ride is a plain screen-vertical lift of the visual,
            // applied here, so the fleet never splits into two seas.
            if (hull.SupportsContinuousRock)
            {
                // THE MESH STORM ATTITUDE (B2.5), PHASE-LOCKED — the MeshRockSmoothness lesson
                // (CI run 30968839931). The first cut drove these extras from the smoothed
                // MULTI-TRAIN slope, which is a different frequency mix from the canned cycle: the
                // renderer's roll/pitch stopped being one cycle's sine/cosine pair, and the
                // pre-existing smoothness pin caught it (phase reversals 0.3%, accel ratio 3.68 at
                // sea 0.7). The storm attitude therefore takes its AMPLITUDE from slowly-varying
                // envelopes only — the EASED dominant train's pure-sine slope envelope A·k (at the
                // drawn frequency scale, through the shared fetch envelope), split by heading (a
                // head sea pitches, a beam sea rolls — the honesty the canned cycle is blind to) —
                // and its WAVEFORM from cos(the SAME phase the canned rock is posed at) (a train's
                // slope is A·k·cosθ of its own height phase — WaveMath's convention, crest at 90°).
                // The summed channels stay a single-cycle pair at any amplitude: the recovered
                // phase atan2(a·sinθ + b·cosθ, c·cosθ) has dψ/dθ = a·c/(u²+v²), strictly one-signed
                // — smooth BY ALGEBRA, not by tuning. Amplitudes are clamped, never the
                // instantaneous value (clipping the wave would flat-top it and the smoothness
                // metrics would read the harmonics as a pop); the blend fades the whole thing —
                // exactly 0 below the storm start.
                float extraRoll = 0f, extraPitch = 0f;
                float phaseDegrees = 0f;
                if (!calm)
                {
                    phaseDegrees = DrawnDominantPhaseDegrees((Vector2)transform.position)
                                 + _crestFrameCalibrationDegrees;
                    // THE ORDINARY-SEA HEAD-SEA PITCH (owner report 2026-08-11) rides the SAME
                    // decomposition the storm extras do — so the block below now runs when EITHER
                    // channel is live. At the shipped default (_meshHeadSeaPitchDegreesPerSlope 0)
                    // the condition reduces to the old `stormBlend > 0f` exactly, so a calm sea
                    // computes nothing it did not compute before (rule 7) and the pose is
                    // bit-identical to before this existed.
                    bool headSeaPitch = _meshHeadSeaPitchDegreesPerSlope > 0f;
                    if (stormBlend > 0f || headSeaPitch)
                    {
                        WaveTrain dominant = field.Dominant;
                        float freqScale = DisplacedSea.TryGet(out DisplacedSeaState sea)
                            ? sea.FreqScale : 1f;
                        float slopeEnvelope = dominant.Amplitude
                                            * ((2f * Mathf.PI) / dominant.Wavelength)
                                            * freqScale * Mathf.Clamp01(fetchEnvelope01);

                        Vector2 up = (Vector2)transform.up;
                        float sqr = up.x * up.x + up.y * up.y;
                        Vector2 bow = sqr < BoatWaveMotionMath.MinHeadingSqrMagnitude
                            ? Vector2.up
                            : up * (1f / Mathf.Sqrt(sqr));
                        Vector2 starboard = BoatWaveMotionMath.Starboard(bow);
                        float bowShare = dominant.Direction.x * bow.x + dominant.Direction.y * bow.y;
                        float beamShare = dominant.Direction.x * starboard.x
                                        + dominant.Direction.y * starboard.y;

                        float hullScale = StormRockMath.HullAttitudeScale(in hullCharacter, in storm);
                        float rollAmp = Mathf.Clamp(
                            slopeEnvelope * beamShare * storm.MeshStormRollDegreesPerSlope * hullScale,
                            -storm.MeshStormMaxRollDegrees, storm.MeshStormMaxRollDegrees);
                        float pitchAmp = Mathf.Clamp(
                            slopeEnvelope * bowShare * storm.MeshStormPitchDegreesPerSlope * hullScale,
                            -storm.MeshStormMaxPitchDegrees, storm.MeshStormMaxPitchDegrees);

                        // The ordinary-sea head-sea pitch: the same slope × bow-share × per-hull
                        // response the storm channel uses, on its OWN serialized degrees-per-slope
                        // and cap, and — the point — NOT multiplied by stormBlend. So a hull answers
                        // the swell she is actually pointing into at every sea state, and keeps
                        // answering as the storm channel fades up on top of her.
                        //
                        // It is an AMPLITUDE, added to a pitch that already rides cos(phaseDegrees):
                        // MeshHullDriver poses the canned cycle as RockPitchDegrees·cos(phase) and
                        // adds this straight onto it, so the drawn pitch stays one clean cycle whose
                        // envelope grew. Clamped like its storm twin — the cap bounds the amplitude,
                        // never the instantaneous value.
                        float headSeaPitchAmp = headSeaPitch
                            ? Mathf.Clamp(
                                slopeEnvelope * bowShare * _meshHeadSeaPitchDegreesPerSlope * hullScale,
                                -_maxMeshHeadSeaPitchDegrees, _maxMeshHeadSeaPitchDegrees)
                            : 0f;

                        float slopeWave = Mathf.Cos(phaseDegrees * Mathf.Deg2Rad);
                        extraRoll = rollAmp * slopeWave * stormBlend;
                        extraPitch = (pitchAmp * stormBlend + headSeaPitchAmp) * slopeWave;
                    }
                }

                hull.SetStormRock(responseMultiplier, extraRoll, extraPitch);
                hull.SetDisplacedHeaveMeters(rideMeters);
                RestoreVisual();

                if (calm)
                {
                    _currentRockFrame = -1;       // glass / near-calm → static level hull, no phantom rock
                    _rockPhaseDegrees = DeckRideMath.LevelPhaseDegrees;
                    hull.RockFrame = -1;
                    return;
                }

                // A mesh hull poses CONTINUOUSLY, so her passengers ride the continuous phase — the very
                // number the hull is posed at (calibration included).
                _rockPhaseDegrees = phaseDegrees;

                // Continuous: the dominant swell's own phase — no frame rounding and no hysteresis
                // (hysteresis exists to stop frame FLIP-FLOP, and there are no frames to flip
                // between). The calibration nudge still applies: it is the art's crest alignment,
                // and both paths share it — the storm extras above ride cos() of this same number.
                hull.SetRockPhaseDegrees(phaseDegrees);
                _currentRockFrame = -1;
                return;
            }

            // THE SPRITE STORM SURGE (B2.5): the baked frames draw ONE attitude — that cannot
            // scale (a steeper bake is an art-director call, flagged in the PR) — but the hull
            // can honestly SURGE: a screen-vertical offset from the bow-axis slope (riding up
            // the face, dipping into the trough) plus the foreshortening squash, layered UNDER
            // the frames. Continuous between the 45° frame steps, so the storm also stops
            // reading notchy. Gains and caps are the transform path's own tuned language × the
            // same storm multiplier; the blend zeroes it exactly in the calm band. (This path
            // keeps the smoothed FULL-field slope: an offset has no sine/cosine pair to keep
            // pure, and the multi-train read is the honest one for translation.)
            hull.SetStormRock(1f, 0f, 0f);   // sprite presenters ignore it; keep the channel defined
            hull.SetDisplacedHeaveMeters(0f);
            float surge = Mathf.Clamp(
                _smoothedPitch * _pitchOffsetPerSlope * responseMultiplier,
                -_maxPitchOffset * responseMultiplier, _maxPitchOffset * responseMultiplier) * stormBlend;
            float squash = Mathf.Min(
                Mathf.Abs(_smoothedPitch) * Mathf.Max(0f, _pitchSquashPerSlope) * responseMultiplier,
                Mathf.Max(0f, _maxPitchSquash) * responseMultiplier) * stormBlend;
            // THE SETTLE LEVEL (owner playtest 2026-08-07): a sprite hull rides the sea's lift and
            // then sinks to her own design waterline, exactly as the mesh path does inside
            // MeshHullDriver — one law (HullSettleMath), two application sites, so the fleet cannot
            // split into hulls that settle and hulls that surf. Every shipped compass carries 0
            // (the art is already drawn at her waterline), and a 0 datum sinks by exactly 0, so
            // this line is byte-identical for today's assets.
            float drawnRide = SettleRideMeters(rideMeters, hull);

            // …AND PUBLISHED, because on this path THIS component is the applier (a mesh hull's
            // driver publishes its own — see IBoatHullPresenter.DrawnRideMeters). It is what the
            // fisher standing on this deck moves by, so it is the number that went into the
            // transform write below and not a re-derivation of it.
            //
            // The RIDE only, deliberately: the storm SURGE is part of this hull's rock language —
            // her passengers draw the rock from the published PHASE at their own amplitudes, and
            // adding the surge here would give them a share of it twice. Displaced sea off ⇒ ride
            // 0 ⇒ published exactly 0 (the A/B contract reaches her passengers too).
            hull.SetDrawnRideMeters(drawnRide);

            ApplyRide(drawnRide + surge, squash);

            if (calm)
            {
                _currentRockFrame = -1;           // glass / near-calm → static level hull, no phantom rock
                _rockPhaseDegrees = DeckRideMath.LevelPhaseDegrees;
                hull.RockFrame = -1;
                return;
            }

            // Quantised: the SAME forward phase as the mesh path (the owner's 2026-07-22 ruling —
            // see the doc above), rounded to the sheet's frames exactly as before.
            float phaseDeg = DrawnDominantPhaseDegrees((Vector2)transform.position);

            _currentRockFrame = DoryRockMath.AdvanceFrame(
                _currentRockFrame, phaseDeg, _rockFrameCount, _crestFrameCalibrationDegrees, _frameHysteresisDegrees);
            hull.RockFrame = _currentRockFrame;

            // A passenger rides the FRAME ACTUALLY DRAWN, not the continuous phase behind it — the picture
            // steps in 45° jumps with hysteresis, and a rider on the smooth phase would slide off the hull
            // between steps. Same choice, for the same reason, as DoryOarLayer reading hull.RockFrame.
            _rockPhaseDegrees = DeckRideMath.PhaseForRockFrame(_currentRockFrame, _rockFrameCount);
        }

        private void SetRockFrame(int frame)
        {
            var hull = Hull;
            if (hull != null) hull.RockFrame = frame;
        }

        /// <summary>Map the smoothed decomposition onto the visual: roll → additive z-rotation (through
        /// the DirectionalBoatSprite hook when present — it stomps rotation every LateUpdate); pitch+bob →
        /// a screen-vertical (world +Y) offset so the lift always reads UP on screen regardless of the
        /// body's physics yaw; |pitch| → a subtle y-squash. Everything clamped to its cap — EXCEPT the
        /// displaced-sea ride: with <paramref name="rideActive"/> the bob term IS
        /// <paramref name="rideMeters"/> settled onto this hull's design waterline
        /// (<see cref="HullSettleMath"/>) — the shared displaced height, which must not be re-capped
        /// or re-scaled per consumer (it is bounded by envelope × exaggeration by construction), less
        /// a CONSTANT per-hull sink, which moves where she floats and never how she moves.
        /// <paramref name="responseMultiplier"/> is the B2.5 storm growth: it scales every gain AND
        /// every cap together (the tuned shape grows instead of pinning against a calm-sized
        /// ceiling), and it is EXACTLY 1 below the storm-start sea state — ×1f is the float
        /// identity, so the calm read is byte-identical on both sides of the A/B.</summary>
        private void Apply(in BoatWaveMotionSample motion, float rideMeters, bool rideActive,
                           float responseMultiplier)
        {
            float r = responseMultiplier;
            float rollDegrees = Mathf.Clamp(motion.Roll * _rollDegreesPerSlope * r,
                                            -_maxRollDegrees * r, _maxRollDegrees * r);
            float pitchOffset = Mathf.Clamp(motion.Pitch * _pitchOffsetPerSlope * r,
                                            -_maxPitchOffset * r, _maxPitchOffset * r);
            float bob = rideActive
                ? rideMeters
                : Mathf.Clamp(motion.Bob * _bobPerHeightMeter * r, -_maxBob * r, _maxBob * r);
            float squash = Mathf.Min(Mathf.Abs(motion.Pitch) * Mathf.Max(0f, _pitchSquashPerSlope) * r,
                                     Mathf.Max(0f, _maxPitchSquash) * r);

            var hull = Hull;
            if (rideActive) bob = SettleRideMeters(bob, hull);   // ride the sea, sit at her waterline
            if (hull != null)
            {
                hull.VisualTiltDegrees = rollDegrees;   // composed after the hull's rotation reset
                // The DISPLACED term of the offset written below, published for her passengers —
                // this component is the applier on the transform path too, and a fisher on a hull
                // that draws no rock cycle at all still has to ride the sea under her. Only the
                // displaced term: the legacy bob/pitch gains ARE this hull's rock, drawn as a
                // translation, and the rider takes its share of that from the tilt hook.
                hull.SetDrawnRideMeters(rideActive ? bob : 0f);
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

        /// <summary>The sprite-hull transform under the frames (the rock-grid path — the frames own
        /// the drawn roll/pitch/heave): base pose plus a screen-vertical (world +Y) lift — the
        /// displaced ride and, in a storm, the B2.5 surge — exactly like <see cref="Apply"/>'s
        /// offset, plus the storm squash (0 outside a storm). Everything 0 restores the visual, so
        /// the displaced-OFF calm frame is byte-identical to before this step.</summary>
        private void ApplyRide(float offsetMeters, float squash01)
        {
            if (offsetMeters == 0f && squash01 <= 0f)
            {
                RestoreVisual();
                return;
            }
            _visual.localPosition = _baseLocalPosition;
            _visual.position += new Vector3(0f, offsetMeters, 0f);
            _visual.localScale = new Vector3(_baseLocalScale.x,
                                             _baseLocalScale.y * (1f - Mathf.Clamp01(squash01)),
                                             _baseLocalScale.z);
            _applied = true;
        }

        /// <summary>Put the visual back exactly as built (and zero the tilt hook, and the ride the
        /// visual is no longer carrying — a picture at base is riding nothing, and her passengers
        /// must be told so). Idempotent. A MESH hull ignores the ride clear by contract: her image is
        /// moved by her driver, not by this transform.</summary>
        private void RestoreVisual()
        {
            var hull = Hull;
            if (hull != null)
            {
                hull.VisualTiltDegrees = 0f;
                hull.SetDrawnRideMeters(0f);
            }
            if (!_applied) return;
            _applied = false;
            if (_visual == null || !_baseCached) return;
            _visual.localPosition = _baseLocalPosition;
            _visual.localScale = _baseLocalScale;
            if (hull == null) _visual.localRotation = Quaternion.identity;
        }
    }
}
