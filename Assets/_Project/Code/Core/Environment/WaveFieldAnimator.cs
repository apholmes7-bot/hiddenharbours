using System;
using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// The animator's own tunables (rule 6) — how fast the eased train parameters chase their
    /// <see cref="WaveMath.TrainsFrom"/> targets, and the tiny floor below which a dying amplitude
    /// snaps to exactly 0 so a calm sea still reaches true glass. Serializable so consumers
    /// (BoatWaveMotion now, the Art-side shader bridge next) surface it in the Inspector /
    /// GameConfig; start from <see cref="Default"/>.
    /// </summary>
    [Serializable]
    public struct WaveFieldAnimatorSettings
    {
        [Tooltip("Time constant τ (seconds) of the fps-independent exponential ease that chases each train's wavelength/amplitude/direction toward its weather-derived target. Bigger = the sea's character changes more languidly; 0 = snap instantly (the old, jittery behaviour). The wind itself drifts over minutes, so a few seconds is invisible lag.")]
        public float ParameterSmoothingSeconds;

        [Tooltip("GLASS IS SACRED (ADR 0018): easing an amplitude toward 0 is asymptotic and would never quite land, so when a train's TARGET amplitude is at/below this floor and its eased value has decayed to it, the eased amplitude snaps to exactly 0 — dead glass, the full mirror. Metres; keep it far below anything visible (default 0.0001 m).")]
        public float GlassSnapAmplitudeMeters;

        /// <summary>The reference tuning: a 1.5 s parameter ease (the weather drifts over minutes, so
        /// the chase is invisible) and a 0.1 mm glass snap floor.</summary>
        public static WaveFieldAnimatorSettings Default => new WaveFieldAnimatorSettings
        {
            ParameterSmoothingSeconds = 1.5f,
            GlassSnapAmplitudeMeters = 1e-4f,
        };
    }

    /// <summary>
    /// <b>PRESENTATION-side smoother for the shared wave field (ADR 0018 addendum)</b> — the small
    /// stateful helper that makes the field's parameters move <i>continuously</i> for anything the
    /// player watches. Both presentation consumers share it: <c>BoatWaveMotion</c> (B2) now, the
    /// Art-side shader bridge (B1) next — one smoothing implementation, so the hull and the water
    /// pixels stay on the same eased sea.
    ///
    /// <para><b>The problem it solves.</b> <see cref="WaveMath.TrainsFrom"/> is a pure function of
    /// the continuously drifting wind, so every re-derivation moves the dominant WAVELENGTH — and
    /// with it the wave number k and the dispersion-derived phase speed c. The closed-form phase
    /// <c>θ = k·(d·pos − c·t) + φ</c> multiplies those by a LARGE running t, so even a tiny k/c
    /// change JUMPS the phase at every refresh: the visible pop/jitter, proportionally worst on a
    /// small-amplitude calm sea. This class removes the jump <b>by construction</b>: each
    /// <see cref="Tick"/> (a) eases every train's wavelength/amplitude/direction toward its
    /// <c>TrainsFrom</c> target with an fps-independent exponential (time constant
    /// <see cref="WaveFieldAnimatorSettings.ParameterSmoothingSeconds"/>), (b) re-derives the phase
    /// speed from the EASED wavelength through the canon dispersion relation <c>c = √(g·λ/2π)</c>
    /// (via the <see cref="WaveTrain"/> constructor — the one place that formula lives; speed is
    /// never free), and (c) advances each train's phase INCREMENTALLY
    /// (<c>phase += k·c·dt</c>, wrapped) so the phase is continuous no matter how the parameters
    /// move. The accumulated phase is baked into each returned train's
    /// <see cref="WaveTrain.PhaseOffset"/>, so the pure <see cref="WaveMath.Sample"/> remains the
    /// single evaluator — <b>sample the returned trains at <c>timeSeconds = 0</c></b> (or call
    /// <see cref="Sample"/>, which does exactly that).</para>
    ///
    /// <para><b>GLASS IS SACRED.</b> Exponential easing toward zero is asymptotic; a calm sea must
    /// still reach true glass. When a train's target amplitude is at/below
    /// <see cref="WaveFieldAnimatorSettings.GlassSnapAmplitudeMeters"/> and its eased amplitude has
    /// decayed to that floor, it snaps to exactly 0 — the field flattens to the full mirror, the
    /// owner's ruling intact.</para>
    ///
    /// <para>⚠ <b>Honesty about determinism (read before reusing).</b> This class is STATEFUL and
    /// presentation-only. It is deterministic given the same tick sequence (same deltas, same
    /// inputs → same output, no RNG), but it is <b>NOT a pure function of gameTime</b>: two
    /// machines running different frame rates ease and accumulate along different paths, and a
    /// save/load does not reproduce its state (nothing here is saved — rule 5 — it just re-eases
    /// in). That is fine for pixels and sprite tilt; it is <b>not fine for simulation</b>. The
    /// ADR 0018 sim contract — B3 seakeeping FORCES and anything gameplay-consequential — must keep
    /// reading the pure <c>WaveMath.TrainsFrom</c> + <c>Sample(pos, gameTime)</c> path, the sim
    /// reference; pointing B3 at this class requires a lead-architect decision first (ADR 0018
    /// addendum records this boundary).</para>
    /// </summary>
    public sealed class WaveFieldAnimator
    {
        private const double TwoPi = Math.PI * 2.0;

        // Eased per-slot state (WaveTrains.MaxTrains slots; only [0, count) are live in a tick).
        private readonly Vector2[] _direction = new Vector2[WaveTrains.MaxTrains];
        private readonly float[] _wavelength = new float[WaveTrains.MaxTrains];
        private readonly float[] _amplitude = new float[WaveTrains.MaxTrains];
        // Accumulated travel phase Φ_i = Σ k_i·c_i·dt, wrapped to [0, 2π). Double so a long session
        // never grinds the wrap through float precision.
        private readonly double[] _phase = new double[WaveTrains.MaxTrains];

        private WaveTrains _current = WaveTrains.None;
        private bool _initialized;

        /// <summary>The trains the last <see cref="Tick"/> produced — phase-continuous, to be
        /// sampled at <c>timeSeconds = 0</c> (the accumulated phase rides in each train's
        /// <see cref="WaveTrain.PhaseOffset"/>). <see cref="WaveTrains.None"/> before the first tick.</summary>
        public WaveTrains Current => _current;

        /// <summary>Forget all eased state; the next <see cref="Tick"/> snaps to its targets (phases
        /// restart at the deterministic hash offsets). Call when the consumer re-wakes after a gap
        /// it does not want eased across (e.g. a region teleport).</summary>
        public void Reset()
        {
            _initialized = false;
            _current = WaveTrains.None;
        }

        /// <summary>
        /// Advance the eased field by one presentation tick and return the phase-continuous trains.
        /// Derives the targets via <see cref="WaveMath.TrainsFrom"/>(<paramref name="windVector"/>,
        /// <paramref name="seaState01"/>, <paramref name="fieldSettings"/>), eases toward them, and
        /// accumulates phase — see the class doc for the three steps. The first tick snaps straight
        /// to the targets (no ease-in from a zeroed sea). <paramref name="deltaSeconds"/> is the
        /// GAME-time step since the last tick (clamped ≥ 0): a paused clock (dt 0) freezes the sea.
        /// </summary>
        public WaveTrains Tick(float deltaSeconds, Vector2 windVector, float seaState01,
                               in WaveFieldSettings fieldSettings, in WaveFieldAnimatorSettings animatorSettings)
        {
            WaveTrains targets = WaveMath.TrainsFrom(windVector, seaState01, in fieldSettings);
            int count = targets.Count;
            float dt = Mathf.Max(0f, deltaSeconds);
            float glassFloor = Mathf.Max(0f, animatorSettings.GlassSnapAmplitudeMeters);

            if (!_initialized)
            {
                for (int i = 0; i < count; i++)
                {
                    WaveTrain target = targets[i];
                    _direction[i] = target.Direction;
                    _wavelength[i] = target.Wavelength;
                    _amplitude[i] = target.Amplitude;
                    _phase[i] = 0.0; // travel starts here; the hash offset φ still de-syncs the trains
                }
                _initialized = true;
            }

            float alpha = SmoothingAlpha(dt, animatorSettings.ParameterSmoothingSeconds);

            WaveTrain eased0 = default, eased1 = default, eased2 = default, eased3 = default;
            for (int i = 0; i < count; i++)
            {
                WaveTrain target = targets[i];

                // (a) fps-independent exponential ease of the parameters. Direction eases per
                // component then renormalizes (the WaveTrain ctor does it; a through-zero flip
                // falls back to a defined +Y for the frames it is degenerate).
                _direction[i] += (target.Direction - _direction[i]) * alpha;
                _wavelength[i] += (target.Wavelength - _wavelength[i]) * alpha;
                _amplitude[i] += (target.Amplitude - _amplitude[i]) * alpha;

                // GLASS IS SACRED: the asymptote never lands, so land it. Snap only when the TARGET
                // itself is (near-)silent — a small-but-real sea is never zeroed.
                if (target.Amplitude <= glassFloor && _amplitude[i] <= glassFloor)
                    _amplitude[i] = 0f;

                // (b) re-derive k and c from the EASED wavelength through the canon dispersion
                // relation — the WaveTrain ctor is the single place that formula lives, so build
                // the train and read the derived speed back (never re-type √(g·λ/2π) here).
                var train = new WaveTrain(_direction[i], _wavelength[i], _amplitude[i],
                                          0f, fieldSettings.Gravity);

                // (c) incremental phase: Φ += k·c·dt — continuous by construction however k and c
                // moved this tick. Baked into PhaseOffset as (φ_hash − Φ) so that sampling the
                // returned train at t = 0 reads k·d·pos − Φ + φ: exactly the closed form's phase,
                // minus its discontinuity.
                double waveNumber = TwoPi / train.Wavelength;
                _phase[i] = Wrap(_phase[i] + waveNumber * train.PhaseSpeed * dt);
                float phaseOffset = (float)Wrap(target.PhaseOffset - _phase[i]);

                var easedTrain = new WaveTrain(train.Direction, train.Wavelength, train.Amplitude,
                                               phaseOffset, fieldSettings.Gravity);
                switch (i)
                {
                    case 0: eased0 = easedTrain; break;
                    case 1: eased1 = easedTrain; break;
                    case 2: eased2 = easedTrain; break;
                    default: eased3 = easedTrain; break;
                }
            }

            _current = new WaveTrains(eased0, eased1, eased2, eased3,
                                      count, targets.CrestSharpening);
            return _current;
        }

        /// <summary>Sample the eased surface at a world position — sugar for
        /// <c>WaveMath.Sample(worldPos, 0.0, Current)</c> (time 0 because the accumulated phase is
        /// already baked into the trains). The pure <see cref="WaveMath.Sample"/> stays the single
        /// evaluator on both sides of the HLSL twin.</summary>
        public WaveSample Sample(Vector2 worldPos) => WaveMath.Sample(worldPos, 0.0, in _current);

        /// <summary>
        /// The DOMINANT train's own phase at a world position (degrees; crest 90°, trough 270°) —
        /// <see cref="WaveMath.TrainPhaseDegrees"/> against <c>Current[0]</c> at time 0, the same
        /// "the accumulated travel already rides in PhaseOffset" sugar as <see cref="Sample"/>.
        ///
        /// <para><b>The smooth rock channel (ADR 0022 phase 5).</b> This class already guarantees the
        /// phase moves CONTINUOUSLY however the weather drifts — that is its entire reason to exist —
        /// so reading it forward yields a rock angle that advances at a dead-constant rate. Deriving
        /// a phase from the sampled SURFACE instead throws that guarantee away; see
        /// <see cref="WaveMath.TrainPhaseDegrees"/> for what that cost in measured stutter. Returns 0
        /// on an empty field (before the first <see cref="Tick"/>, or dead glass) — callers gate calm
        /// on <see cref="WaveTrains.TotalAmplitude"/>, exactly as they already do.</para>
        /// </summary>
        public float DominantPhaseDegrees(Vector2 worldPos) =>
            _current.Count > 0 ? WaveMath.TrainPhaseDegrees(_current[0], worldPos, 0.0) : 0f;

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

        /// <summary>Wrap a phase to [0, 2π) in double (float wrap would chew precision over hours).</summary>
        private static double Wrap(double radians)
        {
            radians -= Math.Floor(radians / TwoPi) * TwoPi;
            return radians;
        }
    }
}
