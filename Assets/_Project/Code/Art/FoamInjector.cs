using HiddenHarbours.Core;
using UnityEngine;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// Drop this on a hull and it churns the advected foam buffer (ADR 0027 #6) where it works
    /// against the water. It is the item's INJECTION seam, and it reads BOTH components of
    /// hull-vs-water relative motion:
    ///
    /// <list type="number">
    /// <item><b>Horizontal</b> — speed through the water (world velocity minus the tidal current, so
    /// a boat drifting WITH the stream churns nothing while a boat holding station against it
    /// does). The classic wake case.</item>
    /// <item><b>Vertical</b> — the hull's heave RATE relative to the local wave surface. This is the
    /// one nothing served before, and it is why the owner moved this item forward from the fleet era
    /// (2026-08-01): <i>"The hull doesn't create realistic foam when bobbing etc."</i> A dory at her
    /// mooring in a chop has zero speed and should still churn.</item>
    /// </list>
    ///
    /// <para><b>How the bob signal is obtained, stated plainly.</b> A hull that tracked the surface
    /// perfectly would displace nothing and churn nothing — the churn IS the hull's inertia losing
    /// the race with the wave face. So this samples the same displaced sea the ride already samples
    /// (<c>WaveFieldAnimator</c> + <see cref="ShoreFadeMath.DisplacedHeight"/>, through the
    /// <see cref="DisplacedSea"/> Core seam, at the surface's own <c>FreqScale</c> so it reads the sea
    /// as DRAWN) and models the hull's response as a first-order lag,
    /// <see cref="FoamBuffer.FollowSurface"/>. <b>It never writes anything back to Boats</b> — no
    /// force, no pose, no heave — and it holds no reference to a boat class at all, so a buoy, a raft
    /// or a swimmer can carry one later without touching this file. Setting
    /// <c>hullResponseSeconds</c> to 0 makes the hull track the surface exactly and the bob channel
    /// falls silent, which is a real off switch rather than an approximation of one.</para>
    ///
    /// <para><b>Zero cost when idle.</b> Off water (the hull hauled out, or over dry ground at low
    /// tide) it unregisters, so <see cref="FoamInjectionRegistry.Count"/> falls to 0 and
    /// <see cref="IsoFacetHullFeature"/> records no pass at all. Disabled likewise.</para>
    ///
    /// <para><b>Presentation only</b> (rule 5): it reads the sim, feeds no sim, and saves nothing.
    /// Everything downstream is accumulated visual state — see <see cref="FoamBuffer"/>'s
    /// determinism-boundary note.</para>
    ///
    /// <para>ℹ️ This does NOT replace <c>BoatWakeEmitter</c>. The emitter's pooled sprite trail is the
    /// young, bright churn right at the stern; the buffer is the mark left on the sea that persists
    /// and drifts downwind after the boat has gone, plus the bobbing case the emitter has no signal
    /// for. They add.</para>
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hidden Harbours/Art/Foam Injector (advected foam buffer)")]
    public sealed class FoamInjector : MonoBehaviour
    {
        [Header("Master")]
        [Tooltip("This hull's contribution scale. 0 = it churns nothing (but still costs its slot — " +
                 "disable the component instead to give the slot back).")]
        [Range(0f, 4f)] [SerializeField] private float _strength = 1f;

        [Tooltip("How much foam a fully-churning hull lays per SECOND (0..1 of a cell's capacity). " +
                 "dt-scaled, so the deposit is frame-rate independent.")]
        [Range(0.1f, 10f)] [SerializeField] private float _depositPerSecond = 2.5f;

        [Tooltip("Half-width of the churned band, metres. Roughly the hull's beam — a dory wants " +
                 "~0.9, a coastal packet several times that.")]
        [Min(0.05f)] [SerializeField] private float _radiusMeters = 0.9f;

        [Tooltip("Move further than this in ONE frame and it is treated as a teleport (a scene load, " +
                 "a respawn, a debug warp), not as way through the water: the history re-primes and " +
                 "nothing is laid. Without it a teleport paints one capsule of foam straight across " +
                 "the map. Well above any sailed frame step — the fastest hull at 60fps moves ~0.2 m.")]
        [Min(0.5f)] [SerializeField] private float _teleportMeters = 8f;

        [Header("Wake channel — speed through the water")]
        [Tooltip("Speed (m/s) at which the wake channel saturates. The dory's cruise, near enough.")]
        [Min(0.01f)] [SerializeField] private float _wakeSpeedKnee = 3f;
        [Tooltip("Shaping exponent for speed. 1 = linear (a wake builds evenly with way).")]
        [Min(1f)] [SerializeField] private float _wakeExponent = 1f;
        [Tooltip("Weight of the wake channel in the sum. 0 = bobbing only.")]
        [Range(0f, 2f)] [SerializeField] private float _wakeWeight = 1f;

        [Header("Bob channel — the owner's ask (hull vs wave surface)")]
        [Tooltip("The hull's vertical INERTIA: the time constant of its lag behind the wave surface. " +
                 "0 = the hull tracks the surface exactly and never slaps, so this channel goes " +
                 "silent. The default matches BoatWaveMotion's own motion smoothing.")]
        [Min(0f)] [SerializeField] private float _hullResponseSeconds = 0.2f;
        [Tooltip("Relative vertical speed (m/s) at which the bob channel saturates.")]
        [Min(0.01f)] [SerializeField] private float _slapRateKnee = 0.5f;
        [Tooltip("Shaping exponent for the bob. ABOVE 1 on purpose: a hard slap must churn " +
                 "disproportionately more than a gentle rise-and-fall, which is a super-linear curve, " +
                 "not a steeper straight line.")]
        [Min(1f)] [SerializeField] private float _slapExponent = 2.5f;
        [Tooltip("Weight of the bob channel in the sum. 0 = wake only (today's behaviour).")]
        [Range(0f, 2f)] [SerializeField] private float _slapWeight = 1f;

        // The injector's own eased view of the shared field — the SeaweedPresenter / BoatWakeEmitter
        // pattern (each floater owns one; they all read the same GameServices settings, so they agree).
        private readonly WaveFieldAnimator _animator = new WaveFieldAnimator();

        private bool _registered;
        private bool _primed;
        private Vector2 _previousPosition;
        private float _hullY;
        private float _surfaceY;
        private float _previousHullY;
        private float _previousSurfaceY;

        private FoamInjection _pending;
        private bool _hasPending;
        private int _pendingFrame = -1;

        /// <summary>The churned band's half-width (m) — read by the feature when it packs the slot.</summary>
        public float RadiusMeters => Mathf.Max(0.05f, _radiusMeters);

        /// <summary>
        /// Set the churned band's half-width from the hull that carries it.
        /// <see cref="IsoFacetHullPresentationService"/> calls this with the def's
        /// <c>WatertightHalfBeamMeters</c> when it fits an injector to a mesh hull, so the band is the
        /// hull's own beam rather than a constant repeated per boat (rule 6). Floored, so a bad number
        /// degrades to a thin ribbon rather than to a divide.
        /// </summary>
        public void ConfigureRadius(float halfBeamMeters)
        {
            _radiusMeters = Mathf.Max(0.05f, halfBeamMeters);
        }

        private void OnEnable()
        {
            _primed = false;
            _hasPending = false;
            _pendingFrame = -1;
        }

        private void OnDisable()
        {
            Leave();
            _primed = false;
            _hasPending = false;
            _pendingFrame = -1;
        }

        // LateUpdate: after physics has moved the boat and after BoatWaveMotion has run, so the
        // position this reads is the one the frame actually drew.
        private void LateUpdate()
        {
            float dt = Time.deltaTime;
            var position = (Vector2)transform.position;

            var env = GameServices.Environment;
            IGameClock clock = GameServices.Clock;
            ITidalTerrain terrain = GameServices.TidalTerrain;
            if (env == null || clock == null || terrain == null)
            {
                Leave();
                _primed = false;
                return;
            }

            double now = clock.TotalSeconds;
            // The game's one depth rule, computed from Core directly (Art cannot reach Boats'
            // BoatCrossing, and must not — rule 4).
            float depth = TidalExposure.WaterDepth(env.WaterLevelAt(now), terrain.ElevationAt(position));
            if (depth <= 0f)
            {
                // Off the water: hauled out, or over ground that has bared on the ebb. Give the slot
                // back so a scene whose only injector is ashore records no pass at all.
                Leave();
                _primed = false;
                return;
            }
            Join();

            // ---- the sea this hull rides, sampled as the surface DRAWS it -------------------------
            EnvironmentSample sample = env.Sample();
            WaveFieldSettings field = GameServices.WaveField;
            WaveFieldAnimatorSettings smoothing = GameServices.WaveFieldAnimator;
            _animator.Tick(dt, sample.WindVector, sample.SeaState01, in field, in smoothing);

            float exaggeration = 1f;
            float bandMeters = 0f;
            float freqScale = 1f;
            if (DisplacedSea.TryGet(out DisplacedSeaState sea))
            {
                exaggeration = sea.Exaggeration;
                bandMeters = sea.ShoreFadeBandMeters;
                // ⚠️ The DRAWN sea's wavelengths, not the raw field's. Sampling at scale 1 while the
                // surface draws at 2.8 reads a different wave entirely — the documented
                // _OceanSwellScale defect class (see DisplacedSeaState.FreqScale).
                freqScale = sea.FreqScale;
            }
            Vector2 samplePos = freqScale == 1f ? position : position * freqScale;
            float rawHeight = _animator.Sample(samplePos, GameServices.FetchEnvelopeAt(position)).Height;
            float surfaceY = ShoreFadeMath.DisplacedHeight(rawHeight, depth, bandMeters, exaggeration);

            // A teleport is not way through the water. Re-prime rather than laying one capsule of
            // foam straight across the map (scene load, respawn, a debug warp).
            bool teleported = _primed &&
                              (position - _previousPosition).sqrMagnitude >
                              _teleportMeters * _teleportMeters;

            if (!_primed || teleported || dt <= 0f)
            {
                // First tick after (re)entering the water: seed the history so the hull does not
                // register a phantom slap from a standing start, and lay nothing this frame.
                _previousPosition = position;
                _surfaceY = surfaceY;
                _hullY = surfaceY;
                _previousSurfaceY = surfaceY;
                _previousHullY = surfaceY;
                _primed = true;
                _hasPending = false;
                return;
            }

            _previousSurfaceY = _surfaceY;
            _previousHullY = _hullY;
            _surfaceY = surfaceY;
            _hullY = FoamBuffer.FollowSurface(_previousHullY, surfaceY, _hullResponseSeconds, dt);

            // ---- the two motion channels ----------------------------------------------------------
            // Speed THROUGH THE WATER, not over the ground: a boat carried along by the stream is
            // stationary relative to the water she floats on and leaves no wake in it.
            Vector2 groundVelocity = (position - _previousPosition) / dt;
            float horizontalSpeed = (groundVelocity - sample.CurrentVector).magnitude;
            float verticalRate = FoamBuffer.RelativeHeaveRate(_surfaceY, _hullY,
                                                              _previousSurfaceY, _previousHullY, dt);

            float rate01 = FoamBuffer.Injection01(horizontalSpeed, verticalRate,
                                                  _wakeSpeedKnee, _wakeExponent, _wakeWeight,
                                                  _slapRateKnee, _slapExponent, _slapWeight);
            float amount = rate01 * Mathf.Max(_depositPerSecond, 0f) * dt * Mathf.Max(_strength, 0f);

            // The capsule from last frame's position to this one — continuous at any frame rate.
            _pending = new FoamInjection(_previousPosition, position, RadiusMeters, Mathf.Clamp01(amount));
            _hasPending = amount > 1e-5f;
            _pendingFrame = Time.frameCount;
            _previousPosition = position;
        }

        /// <summary>
        /// This frame's deposit, or false if there is none. Read by the feature once per camera —
        /// deliberately NOT consumed, because a second camera rendering the same frame owns its own
        /// buffer and needs the same deposit. The frame stamp is what stops a stale deposit being
        /// re-applied on a frame this injector never ticked.
        /// </summary>
        internal bool TryTakeInjection(out FoamInjection injection)
        {
            injection = _pending;
            return _hasPending && _pendingFrame == Time.frameCount;
        }

        private void Join()
        {
            if (_registered) return;
            FoamInjectionRegistry.Register(this);
            _registered = true;
        }

        private void Leave()
        {
            if (!_registered) return;
            FoamInjectionRegistry.Unregister(this);
            _registered = false;
            _hasPending = false;
        }
    }
}
