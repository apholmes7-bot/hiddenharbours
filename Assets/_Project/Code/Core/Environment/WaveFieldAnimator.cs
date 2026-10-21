using System;
using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>Serialized compatibility for existing assets. Wave memory replaces parameter easing;
    /// these fields no longer affect the shared field. Output motion may still use Smooth.</summary>
    [Serializable]
    public struct WaveFieldAnimatorSettings
    {
        [Tooltip("Retained for asset compatibility; wave windows now live in WaveFieldSettings.")]
        public float ParameterSmoothingSeconds;
        [Tooltip("Retained for asset compatibility; a wholly calm memory window reaches exact glass.")]
        public float GlassSnapAmplitudeMeters;

        public static WaveFieldAnimatorSettings Default => new WaveFieldAnimatorSettings
        {
            ParameterSmoothingSeconds = 1.5f,
            GlassSnapAmplitudeMeters = 1e-4f,
        };
    }

    /// <summary>Adapter from wind memory to phase-baked trains. Current is an output, never an input:
    /// any tick, reset or cold start at the same world time returns the same sea (ADR 0018).
    /// Sample the returned trains at time zero, since travel is already in PhaseOffset.</summary>
    public sealed class WaveFieldAnimator
    {
        private const double TwoPi = Math.PI * 2.0;
        private WaveTrains _current = WaveTrains.None;
        public WaveTrains Current => _current;
        public void Reset() => _current = WaveTrains.None;

        /// <summary>Runtime consumers use the environment's history seam when available.
        /// A fixture with only a current wind declares steady weather. Delta and the old easing
        /// settings are retained for call-site compatibility and cannot change the field.</summary>
        public WaveTrains Tick(float deltaSeconds, double gameTimeSeconds,
                               Vector2 windVector, float seaState01,
                               in WaveFieldSettings fieldSettings, in WaveFieldAnimatorSettings animatorSettings)
        {
            var source = GameServices.Environment as IWaveWindSource;
            var trains = source != null
                ? WaveMemory.TrainsAt(source, gameTimeSeconds, in fieldSettings)
                : WaveMath.TrainsFrom(windVector, seaState01, in fieldSettings);
            return Bake(in trains, gameTimeSeconds, fieldSettings.Gravity);
        }

        public WaveTrains Tick(double gameTimeSeconds, IWaveWindSource source,
                               in WaveFieldSettings fieldSettings)
        {
            var trains = WaveMemory.TrainsAt(source, gameTimeSeconds, in fieldSettings);
            return Bake(in trains, gameTimeSeconds, fieldSettings.Gravity);
        }

        private WaveTrains Bake(in WaveTrains targets, double time, float gravity)
        {
            Span<WaveTrain> trains = stackalloc WaveTrain[WaveTrains.MaxTrains];
            for (int i = 0; i < targets.Count; i++)
            {
                var train = targets[i];
                double travel = Wrap(TwoPi / train.Wavelength * train.PhaseSpeed * time);
                trains[i] = new WaveTrain(train.Direction, train.Wavelength, train.Amplitude,
                    (float)Wrap(train.PhaseOffset - travel), gravity);
            }
            _current = WaveTrains.From(trains, targets.Count, targets.CrestSharpening, targets.DominantIndex);
            return _current;
        }

        public WaveSample Sample(Vector2 worldPos) => WaveMath.Sample(worldPos, 0.0, in _current);
        public WaveSample Sample(Vector2 worldPos, float fetchEnvelope01)
            => Sample(worldPos, (WaveFetchSample)fetchEnvelope01);

        public WaveSample Sample(Vector2 worldPos, WaveFetchSample fetchEnvelope01)
            => WaveMath.Sample(worldPos, 0.0, in _current, fetchEnvelope01);
        public float DominantPhaseDegrees(Vector2 worldPos) =>
            _current.Count > 0 ? WaveMath.TrainPhaseDegrees(_current.Dominant, worldPos, 0.0) : 0f;

        // ---- shared fps-independent smoothing (used here and by the motion consumers) ------------

        /// <summary>Blend factor of an fps-independent exponential ease: <c>1 − e^(−dt/τ)</c>. Two
        /// half-steps compose to exactly one full step toward a constant target (the property the
        /// EditMode tests pin), so the feel is identical at any frame rate. τ ≤ 0 → 1 (snap).</summary>
        public static float SmoothingAlpha(float deltaSeconds, float timeConstantSeconds)
        {
            if (timeConstantSeconds <= 0f) return 1f;
            return 1f - Mathf.Exp(-Mathf.Max(0f, deltaSeconds) / timeConstantSeconds);
        }

        /// <summary>One fps-independent exponential-ease step of <paramref name="current"/> toward
        /// <paramref name="target"/> — the one smoothing primitive every wave-presentation consumer
        /// shares (BoatWaveMotion's output damping uses it too).</summary>
        public static float Smooth(float current, float target, float deltaSeconds, float timeConstantSeconds)
            => current + (target - current) * SmoothingAlpha(deltaSeconds, timeConstantSeconds);

        /// <summary>World clock; bare presentation scenes use the engine clock.</summary>
        public static double GameTimeSeconds =>
            GameServices.Clock != null ? GameServices.Clock.TotalSeconds : Time.timeAsDouble;

        private static double Wrap(double radians)
            => radians - Math.Floor(radians / TwoPi) * TwoPi;
    }
}
