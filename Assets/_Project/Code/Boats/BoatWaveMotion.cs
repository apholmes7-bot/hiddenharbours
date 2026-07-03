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
    /// <para><b>Determinism &amp; rules.</b> Pure function of (wind, seaState01, position, time) —
    /// no state, no RNG, nothing saved (rule 5); allocation-free structs on a throttled train
    /// cadence + one <see cref="WaveMath.Sample"/> per frame (rule 7); reads the sim only through
    /// <see cref="GameServices"/> (rule 4 — Boats references Core only).</para>
    ///
    /// <para>⚠ <b>Settings parity (ADR 0018 §(4)).</b> <see cref="_settings"/> starts from
    /// <see cref="WaveFieldSettings.Default"/> — the SAME defaults the Art-side WaveFieldBridge (B1)
    /// publishes to the water shader, so the hull rocks on the same waves the player sees because
    /// both sides derive from the same (wind, seaState01, settings). B3/GameConfig will unify the
    /// two settings instances into ONE owner-tunable source; until then, tune the FIELD's shape
    /// there (or identically in both places), and only the RESPONSE amplitudes here.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public class BoatWaveMotion : MonoBehaviour
    {
        [Header("Master")]
        [Tooltip("Master strength of the whole visual rock. 0 = off (visual restored untouched); 1 = the tuned read; >1 exaggerates for tuning sessions.")]
        [SerializeField] private float _masterStrength = 1f;

        [Header("Roll (beam sea → the deck tilts)")]
        [Tooltip("Degrees of visual z-rotation per unit of beam-axis wave slope. The slope of the Default field peaks around ~0.5–1 in a full gale, so ~8 reads as a clear-but-small roll.")]
        [SerializeField] private float _rollDegreesPerSlope = 8f;
        [Tooltip("Hard cap on the visual roll (degrees) — pixel art hates large rotations; ±3–6° at full sea is the readable band.")]
        [SerializeField] private float _maxRollDegrees = 4.5f;

        [Header("Pitch (head sea → bow rides the face)")]
        [Tooltip("Screen-vertical offset (world units) per unit of bow-axis wave slope — the sprite riding up the face (+) and dipping into the trough (−).")]
        [SerializeField] private float _pitchOffsetPerSlope = 0.05f;
        [Tooltip("Hard cap on the pitch offset (world units). Keep SMALL — this is a read, not a jump.")]
        [SerializeField] private float _maxPitchOffset = 0.08f;
        [Tooltip("Vertical squash per unit of |bow-axis slope| — a subtle foreshortening as the hull tips through a wave. 0 disables the squash entirely.")]
        [SerializeField] private float _pitchSquashPerSlope = 0.05f;
        [Tooltip("Hard cap on the squash (fraction of the visual's base y-scale). Keep well under 0.1 or the sprite visibly 'breathes'.")]
        [SerializeField] private float _maxPitchSquash = 0.06f;

        [Header("Bob (the crest lifts the whole boat)")]
        [Tooltip("Screen-vertical lift (world units) per metre of wave height under the hull. The Default field's envelope tops out ~1.5 m in a full gale.")]
        [SerializeField] private float _bobPerHeightMeter = 0.06f;
        [Tooltip("Hard cap on the bob (world units).")]
        [SerializeField] private float _maxBob = 0.1f;

        [Header("Cadence")]
        [Tooltip("How often (Hz) the wave TRAINS are re-derived from the weather (wind + sea state change slowly). The surface itself is sampled every frame — this only throttles the derivation.")]
        [SerializeField] private float _trainsRefreshHz = 4f;

        [Header("Wiring (the builder sets these)")]
        [Tooltip("The child VISUAL transform the motion is applied to (e.g. the FishingBoatVisual sprite child). NEVER the physics root — this is visual-only; the body and colliders must not move. Null = the component idles.")]
        [SerializeField] private Transform _visual;
        [Tooltip("Optional: the DirectionalBoatSprite driving the same visual. It force-resets the visual's rotation every LateUpdate, so the roll MUST route through its VisualTiltDegrees hook. If null, the roll is written to the visual's localRotation directly.")]
        [SerializeField] private DirectionalBoatSprite _directionalSprite;

        [Header("Wave field (parity: keep identical to the shader bridge until GameConfig unifies them — see class doc)")]
        [SerializeField] private WaveFieldSettings _settings = WaveFieldSettings.Default;

        private WaveTrains _trains = WaveTrains.None;
        private float _trainsTimer;

        private bool _baseCached;
        private Vector3 _baseLocalPosition;
        private Vector3 _baseLocalScale;
        private bool _applied;

        /// <summary>Master strength, settable at runtime (dev rigs / feel sessions). 0 = off.</summary>
        public float MasterStrength
        {
            get => _masterStrength;
            set => _masterStrength = Mathf.Max(0f, value);
        }

        /// <summary>Configure the wiring from code (the builders' path — mirrors
        /// <see cref="DirectionalBoatSprite.Configure"/> so a non-dev owner needn't touch the Inspector).</summary>
        public void Configure(Transform visual, DirectionalBoatSprite directionalSprite)
        {
            RestoreVisual();          // if re-wired live, leave the old visual clean
            _visual = visual;
            _directionalSprite = directionalSprite;
            _baseCached = false;
        }

        private void OnEnable()
        {
            _trainsTimer = 0f;        // re-derive immediately on wake
        }

        private void OnDisable()
        {
            RestoreVisual();
        }

        private void LateUpdate()
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
                RestoreVisual();      // 0 = off, and the visual sits exactly where it was built
                return;
            }

            // Re-derive the trains from the weather on the throttled cadence (they only change as
            // the wind/sea state drift); sample the surface itself every frame.
            _trainsTimer -= Time.deltaTime;
            if (_trainsTimer <= 0f)
            {
                _trainsTimer = _trainsRefreshHz > 0f ? 1f / _trainsRefreshHz : 0f;
                var env = GameServices.Environment;
                if (env != null)
                {
                    EnvironmentSample sample = env.Sample();
                    _trains = WaveMath.TrainsFrom(sample.WindVector, sample.SeaState01, in _settings);
                }
                else
                {
                    _trains = WaveTrains.None;   // no sim, no sea — dead still, never stale
                }
            }

            double time = GameServices.Clock != null ? GameServices.Clock.TotalSeconds : Time.timeAsDouble;
            WaveSample wave = WaveMath.Sample((Vector2)transform.position, time, in _trains);
            BoatWaveMotionSample motion =
                BoatWaveMotionMath.Decompose(wave.Slope, wave.Height, (Vector2)transform.up, _masterStrength);

            Apply(motion);
        }

        /// <summary>Map the raw decomposition onto the visual: roll → additive z-rotation (through the
        /// DirectionalBoatSprite hook when present — it stomps rotation every LateUpdate); pitch+bob →
        /// a screen-vertical (world +Y) offset so the lift always reads UP on screen regardless of the
        /// body's physics yaw; |pitch| → a subtle y-squash. Everything clamped to its cap.</summary>
        private void Apply(in BoatWaveMotionSample motion)
        {
            float rollDegrees = Mathf.Clamp(motion.Roll * _rollDegreesPerSlope, -_maxRollDegrees, _maxRollDegrees);
            float pitchOffset = Mathf.Clamp(motion.Pitch * _pitchOffsetPerSlope, -_maxPitchOffset, _maxPitchOffset);
            float bob = Mathf.Clamp(motion.Bob * _bobPerHeightMeter, -_maxBob, _maxBob);
            float squash = Mathf.Min(Mathf.Abs(motion.Pitch) * Mathf.Max(0f, _pitchSquashPerSlope),
                                     Mathf.Max(0f, _maxPitchSquash));

            if (_directionalSprite != null)
            {
                _directionalSprite.VisualTiltDegrees = rollDegrees;   // composed after its rotation reset
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
            if (_directionalSprite != null) _directionalSprite.VisualTiltDegrees = 0f;
            if (!_applied) return;
            _applied = false;
            if (_visual == null || !_baseCached) return;
            _visual.localPosition = _baseLocalPosition;
            _visual.localScale = _baseLocalScale;
            if (_directionalSprite == null) _visual.localRotation = Quaternion.identity;
        }
    }
}
