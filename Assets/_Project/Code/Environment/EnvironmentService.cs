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

        private IGameClock _clock;

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
            float tide = TideModel.Height(t, _activeTideProfile, _config);

            // Flood runs along +channelAxis (ebb reverses), strongest at mid-tide (max rate). The channel
            // BEARING gently wanders on its own slow timescale so the set of the tide evolves at its own
            // rate — deterministic from (seed, time), nothing saved (CLAUDE.md rule 5).
            float rate = TideModel.Rate(t, _activeTideProfile, _config);
            Vector2 current = CurrentModel.SampleCurrent(
                t, _worldSeed, _config.SecondsPerHour, _channelAxis, _currentFactor, rate, _activeCurrentProfile);

            return new EnvironmentSample(wind, current, tide, sea, vis);
        }

        public float TideHeightAt(double totalSeconds) =>
            TideModel.Height(totalSeconds, _activeTideProfile, _config);
    }
}
