using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Environment
{
    /// <summary>
    /// Assembles tide + weather + tidal current into an <see cref="EnvironmentSample"/> for the
    /// active region. Deterministic from (WorldSeed, clock time), so nothing here is saved.
    /// Implements <see cref="IEnvironmentService"/>; registered into <see cref="GameServices"/>.
    /// </summary>
    public class EnvironmentService : MonoBehaviour, IEnvironmentService
    {
        [SerializeField] private GameConfig _config;
        [SerializeField] private int _worldSeed = 12345;
        [SerializeField] private TideProfile _activeTideProfile = TideProfile.CoddleCove;
        [SerializeField] private WindProfile _activeWindProfile = WindProfile.CoddleCove;   // VS-05

        [Header("Tidal current")]
        [Tooltip("Prevailing axis the flood tide runs along for the active region — the bearing the wander " +
                 "leans off. Ebb reverses it; magnitude follows the tide rate.")]
        [SerializeField] private Vector2 _channelAxis = Vector2.right;
        [Tooltip("Scales tide rate-of-change into a current speed (m/s).")]
        [SerializeField] private float _currentFactor = 25f;
        [Tooltip("Gentle bearing-wander of the current: its OWN slow timescale (DriftHours) + small amplitude " +
                 "(WanderDeg), so the set of the tide evolves at its own rate — distinct from the wind and the " +
                 "~12.42 h tide. Small by design: the current also drives boat drift, so this is a sailing-feel knob.")]
        [SerializeField] private CurrentProfile _activeCurrentProfile = CurrentProfile.CoddleCove;

        [Header("Dev sea-state override (ADR 0027 P0 — view the sea in a storm)")]
        [Tooltip("EDITOR / DEVELOPMENT BUILD ONLY (a release build ignores it entirely). Forces the " +
                 "sampled WIND STRENGTH to whatever produces the target sea-state below, so the owner " +
                 "can view the sea in a STORM — which the M1 calm-band wind clamp never produces " +
                 "naturally. The deterministic wind DIRECTION is kept; the sea-state enum, the " +
                 "continuous SeaState01 and everything derived from the wind stay mutually consistent. " +
                 "Nothing is saved; with the toggle OFF the sample is byte-identical to the pure sim.")]
        [SerializeField] private bool _devForceSeaState = false;
        [Tooltip("Target continuous sea-state while the override is ON (0 = glass .. 1 = storm).")]
        [Range(0f, 1f)] [SerializeField] private float _devSeaState01 = 1f;

        private IGameClock _clock;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>Dev-only runtime access to the storm override (mirrors the Inspector fields), so a
        /// debug key or a test can drive it. Compiled out of release builds with the override itself.</summary>
        public bool DevForceSeaState { get => _devForceSeaState; set => _devForceSeaState = value; }
        /// <summary>Dev-only target sea-state for <see cref="DevForceSeaState"/> (0 glass .. 1 storm).</summary>
        public float DevSeaState01 { get => _devSeaState01; set => _devSeaState01 = Mathf.Clamp01(value); }
#endif

        public int WorldSeed => _worldSeed;
        public TideProfile ActiveTideProfile { get => _activeTideProfile; set => _activeTideProfile = value; }
        public WindProfile ActiveWindProfile { get => _activeWindProfile; set => _activeWindProfile = value; }
        public CurrentProfile ActiveCurrentProfile { get => _activeCurrentProfile; set => _activeCurrentProfile = value; }

        private void Awake()
        {
            if (_config == null)
                Debug.LogError("[EnvironmentService] No GameConfig assigned.", this);
        }

        private IGameClock Clock => _clock ??= GameServices.Clock;

        public EnvironmentSample Sample()
        {
            double t = Clock?.TotalSeconds ?? 0.0;
            WeatherModel.Sample(t, _worldSeed, _config, _activeWindProfile, out Vector2 wind, out SeaState sea, out float vis);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // ---- DEV SEA-STATE OVERRIDE (ADR 0027 P0) — editor/dev builds only, default OFF ----------
            // Rescale the deterministic wind to the strength that produces the target sea-state
            // (WeatherModel.WindStrengthFor — the exact SeaState01 inverse), keeping the sim's wind
            // DIRECTION, then re-derive the enum from the SAME thresholds. Every consumer downstream
            // (sea01 below, _Chop/_Roughness pushes, WaveMath.TrainsFrom, the mood palette) reads the
            // one overridden wind, so the forced storm is internally consistent — no forked truth.
            // Deterministic given the toggle (no hidden randomness, rule 5); never saved; compiled out
            // of release builds. Tide, current and fog stay the pure sim.
            if (_devForceSeaState)
            {
                float forcedStrength = WeatherModel.WindStrengthFor(Mathf.Clamp01(_devSeaState01));
                Vector2 dir = wind.sqrMagnitude > 1e-8f ? wind.normalized : Vector2.up;
                wind = dir * forcedStrength;
                sea = WeatherModel.SeaFromWind(forcedStrength);
            }
#endif

            // The continuous sea-state axis rides alongside the stepped enum: same wind, same thresholds,
            // just not quantized — presentation consumers ease across the bands instead of popping 1/7.
            float sea01 = WeatherModel.SeaState01(wind.magnitude);
            float tide = TideModel.Height(t, _activeTideProfile, _config);

            // Flood runs along +channelAxis (ebb reverses), strongest at mid-tide (max rate). The channel
            // BEARING gently wanders on its own slow timescale so the set of the tide evolves at its own
            // rate — deterministic from (seed, time), nothing saved (CLAUDE.md rule 5).
            float rate = TideModel.Rate(t, _activeTideProfile, _config);
            Vector2 current = CurrentModel.SampleCurrent(
                t, _worldSeed, _config.SecondsPerHour, _channelAxis, _currentFactor, rate, _activeCurrentProfile);

            return new EnvironmentSample(wind, current, tide, sea, vis, sea01);
        }

        public float TideHeightAt(double totalSeconds) =>
            TideModel.Height(totalSeconds, _activeTideProfile, _config);

        /// <summary>
        /// The continuous sea state at an ARBITRARY time (<see cref="IEnvironmentService.SeaState01At"/>) —
        /// the same pure <see cref="WeatherModel"/> evaluation <see cref="Sample"/> performs, just at the
        /// requested instant instead of now. Deterministic from <c>(worldSeed, t)</c> and allocation-free,
        /// so a consumer reasoning about a SPAN of time (the fish-school windows) can hold the weather
        /// still instead of re-deciding against a drifting wind every frame.
        ///
        /// <para><b>The dev storm override applies here too</b>, and must: it exists so the owner can VIEW
        /// the sea in a storm, and if this read stayed on the pure sim the fish would still be shoaling
        /// like a calm day while the sea around them raged. One overridden truth, every consumer — the
        /// discipline <see cref="Sample"/>'s own note sets out. Editor/dev builds only; a release build is
        /// the pure sim.</para>
        /// </summary>
        public float SeaState01At(double totalSeconds)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (_devForceSeaState) return Mathf.Clamp01(_devSeaState01);
#endif
            float secondsPerHour = _config != null
                ? _config.SecondsPerHour
                : GameConfig.DefaultSecondsPerDay / 24f;
            Vector2 wind = WeatherModel.SampleWind(totalSeconds, _worldSeed, secondsPerHour,
                                                   _activeWindProfile);
            return WeatherModel.SeaState01(wind.magnitude);
        }
    }
}
