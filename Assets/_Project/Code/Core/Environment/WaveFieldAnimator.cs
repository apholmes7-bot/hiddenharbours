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
    /// never free), and (c) evaluates each train's travel phase in CLOSED FORM at the game clock
    /// (<c>ω·t</c>, accumulated in double and wrapped before it drops to float) so it is a pure
    /// function of <c>(worldSeed, gameTime)</c>. That phase is baked into each returned train's
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
    /// <para>✅ <b>THE PHASE IS NO LONGER A LEAK (PR E, 2026-09-09).</b> This paragraph used to
    /// confess that the class was <b>not a pure function of gameTime</b> — that two machines at
    /// different frame rates accumulated along different paths, and that a save/load did not
    /// reproduce the sea the player left. That was true while the travel phase was a running total.
    /// It is not any more: the phase is <c>ω·t</c> at the clock, so a cold start at any instant
    /// draws what an hour-long run would have drawn, and 30 fps and 60 fps agree exactly.
    /// <c>WaveFieldPhaseIsClosedFormTests</c> asserts all three, with a dead control that shows the
    /// retired accumulator failing the same comparisons.</para>
    ///
    /// <para>⚠ <b>Why the accumulator existed, and why its reason expired.</b> It was there because
    /// <c>ω</c> came from <c>λ_p(U)</c>: every mood change moved it, and <c>Δω·t</c> threw the sea
    /// across the wave — register row 34, measured at up to 474 858 radians at thirty hours.
    /// <b>PR B put the bins on a fixed ladder</b>, so <c>λ</c> no longer depends on the wind and
    /// <c>ω·t</c> is continuous by itself. ⚠ <b>That dependency is load-bearing:</b> put the wind
    /// back into the wavelength and this class's phase becomes discontinuous again. It is asserted,
    /// in <c>ThisOnlyWorksBecauseTheBinsAreFIXED_AndThatIsAssertedHere</c>.</para>
    ///
    /// <para>⚠ <b>What is still stateful:</b> the AMPLITUDE / wavelength / direction easing. A mood
    /// should arrive over a second and a half rather than in a frame, and that easing is still
    /// shaped by the tick sequence. It is presentation only and it converges, so two frame rates
    /// reach the same sea and merely take slightly different routes to it. The ADR 0018 sim
    /// contract is unchanged: B3 seakeeping FORCES and anything gameplay-consequential read the
    /// pure <c>WaveMath.TrainsFrom</c> + <c>Sample(pos, gameTime)</c> path. The difference since
    /// PR E is that the drawn sea and the ridden sea are now the same function of the same clock
    /// (P1, SEE == FEEL) rather than two paths that happen to agree.</para>
    /// </summary>
    public sealed class WaveFieldAnimator
    {
        private const double TwoPi = Math.PI * 2.0;

        // Eased per-slot state (WaveTrains.MaxTrains slots; only [0, count) are live in a tick).
        private readonly Vector2[] _direction = new Vector2[WaveTrains.MaxTrains];
        private readonly float[] _wavelength = new float[WaveTrains.MaxTrains];
        private readonly float[] _amplitude = new float[WaveTrains.MaxTrains];
        // ⚠ RETIRED 2026-09-09 (PR E): the travel phase was ACCUMULATED here. Double so a long session
        // never grinds the wrap through float precision.
        private readonly double[] _phase = new double[WaveTrains.MaxTrains];

        /// <summary>Scratch for the eased trains of the tick in progress — reused every tick so the
        /// per-frame path stays allocation-free (rule 7). Only [0, count) is meaningful.</summary>
        private readonly WaveTrain[] _eased = new WaveTrain[WaveTrains.MaxTrains];

        private WaveTrains _current = WaveTrains.None;
        private bool _initialized;

        /// <summary>How many slots have ever been snapped to a target. Slots at or above this index
        /// hold no meaningful eased state, so they must be SNAPPED rather than eased the first time
        /// the field grows into them (see <see cref="Tick"/>).</summary>
        private int _initializedCount;

        /// <summary>The trains the last <see cref="Tick"/> produced — phase-continuous, to be
        /// sampled at <c>timeSeconds = 0</c> (the phase AT THE GAME CLOCK rides in each train's
        /// <see cref="WaveTrain.PhaseOffset"/>). <see cref="WaveTrains.None"/> before the first tick.</summary>
        public WaveTrains Current => _current;

        /// <summary>Forget all eased state; the next <see cref="Tick"/> snaps to its targets (phases
        /// restart at the deterministic hash offsets). Call when the consumer re-wakes after a gap
        /// it does not want eased across (e.g. a region teleport).</summary>
        public void Reset()
        {
            _initialized = false;
            _initializedCount = 0;
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
        public WaveTrains Tick(float deltaSeconds, double gameTimeSeconds,
                               Vector2 windVector, float seaState01,
                               in WaveFieldSettings fieldSettings, in WaveFieldAnimatorSettings animatorSettings)
        {
            WaveTrains targets = WaveMath.TrainsFrom(windVector, seaState01, in fieldSettings);
            int count = targets.Count;
            float dt = Mathf.Max(0f, deltaSeconds);
            float glassFloor = Mathf.Max(0f, animatorSettings.GlassSnapAmplitudeMeters);

            // Seed any slot that has never been eased before — on the first tick that is all of them,
            // and thereafter it is any slot the field has just GROWN into.
            //
            // ⚠️ The growth case is not hypothetical: turning the ADR 0027 spectrum blend off zero
            // takes the live count from 4 to 8, and that is exactly the dial the owner turns to judge
            // the feel. Easing a never-initialized slot would start it from a ZERO wavelength — which
            // the WaveTrain floor turns into λ = 1 cm, i.e. k ≈ 628 — so the new trains would arrive
            // as a burst of shrieking high-frequency slope before settling. Wavelength and direction
            // therefore SNAP, which is invisible: a train carrying zero amplitude has no shape to see.
            //
            // ⚠️ AMPLITUDE is the one that must NOT snap on growth. Seeding it at the target made a
            // grown train appear at full height in a single tick — a step in the sea's actual SHAPE,
            // under the boat as much as under the eye, and the sharpest edge of the owner's "choppy
            // when transitioning between states". So: the FIRST init snaps everything (waking to the
            // live weather is a legitimate discontinuity — Reset() exists to ask for exactly that),
            // while GROWTH seeds amplitude at zero and lets the ordinary ease below carry it up over
            // τ. The comment here used to CLAIM the amplitude already eased in from zero; the line
            // below said otherwise, and the line is what shipped.
            if (!_initialized || count > _initializedCount)
            {
                bool growth = _initialized;
                for (int i = growth ? _initializedCount : 0; i < count; i++)
                {
                    WaveTrain target = targets[i];
                    _direction[i] = target.Direction;
                    _wavelength[i] = target.Wavelength;
                    _amplitude[i] = growth ? 0f : target.Amplitude;
                    // (no phase to seed since PR E: it is ω·t at the clock, not a running total)
                }
                _initialized = true;
                _initializedCount = Mathf.Max(_initializedCount, count);
            }

            float alpha = SmoothingAlpha(dt, animatorSettings.ParameterSmoothingSeconds);

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

                // (c) 🔴 THE CLOSED FORM, not an accumulator (PR E). The travel phase is
                // Φ(t) = ω·t evaluated at the game clock, wrapped to [0, 2π) IN DOUBLE before it
                // drops to float — which is exactly what WaveMath.Sample does, and for the same
                // reason: ω·t reaches millions of radians in a long session and float32 would lose
                // the wave to rounding. Baked into PhaseOffset as (φ_hash − Φ) so that sampling
                // the returned train at t = 0 reads k·d·pos − ω·t + φ: the closed form, exactly.
                //
                // ⚠️ WHY THIS IS SAFE NOW AND WAS NOT BEFORE. The accumulator existed because a
                // parameter change jumped the phase: ω came from λ_p(U), so every mood change moved it
                // and Δω·t threw the sea across the wave (register row 34 — up to 474 858 radians at
                // thirty hours). PR B put the bins on a FIXED ladder: λ no longer depends on the wind,
                // so ω is constant and ω·t is continuous by itself. The accumulator is no longer what
                // makes the phase continuous — it is only a rule-5 leak, because an accumulated phase
                // is a function of the frame sequence rather than of (worldSeed, gameTime).
                double waveNumber = TwoPi / train.Wavelength;
                double travelPhase = Wrap(waveNumber * train.PhaseSpeed * gameTimeSeconds);
                float phaseOffset = (float)Wrap(target.PhaseOffset - travelPhase);

                // ⚠️ Written into the slot it BELONGS to. This was a four-way switch whose `default`
                // arm caught slot 3; at MaxTrains = 4 that was correct, but it silently made every
                // slot from 3 up land on top of each other the moment the field could be wider —
                // the eased sea would then have lost trains the derived sea still had, and only the
                // hull-vs-water parity test would ever have noticed.
                _eased[i] = new WaveTrain(train.Direction, train.Wavelength, train.Amplitude,
                                          phaseOffset, fieldSettings.Gravity);
            }

            // The peak rides through the easing untouched: the animator changes WHEN a train is,
            // never WHICH train is the biggest (it eases each slot toward its own target).
            _current = WaveTrains.From(_eased, count, targets.CrestSharpening, targets.DominantIndex);
            return _current;
        }

        /// <summary>Sample the eased surface at a world position — sugar for
        /// <c>WaveMath.Sample(worldPos, 0.0, Current)</c> (time 0 because the accumulated phase is
        /// already baked into the trains). The pure <see cref="WaveMath.Sample"/> stays the single
        /// evaluator on both sides of the HLSL twin.</summary>
        public WaveSample Sample(Vector2 worldPos) => WaveMath.Sample(worldPos, 0.0, in _current);

        /// <summary>The same sugar through the <b>wind-fetch envelope</b> (ADR 0027 #1) — see
        /// <see cref="WaveMath.Sample(Vector2, double, in WaveTrains, float)"/>. Resolve the envelope
        /// ONCE per consumer per tick (<see cref="WaveFetch.EnvelopeAt"/> at the hull's centre) and
        /// pass it to every probe: the envelope turns over the fetch scale, tens of metres, so it does
        /// not meaningfully vary across a ~5 m hull, and a 24-step march per probe would be paid for
        /// nothing. Passing 1 is the exact passthrough.</summary>
        public WaveSample Sample(Vector2 worldPos, float fetchEnvelope01)
            => WaveMath.Sample(worldPos, 0.0, in _current, fetchEnvelope01);

        /// <summary>
        /// The DOMINANT train's own phase at a world position (degrees; crest 90°, trough 270°) —
        /// <see cref="WaveMath.TrainPhaseDegrees"/> against <c>Current.Dominant</c> — the SPECTRAL
        /// PEAK, deliberately, not slot 0 by convention (see <see cref="WaveTrains.DominantIndex"/>:
        /// a re-weighting moves the peak, and this consumer must follow it) — at time 0, the same
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

        /// <summary>Wrap a phase to [0, 2π) in double (float wrap would chew precision over hours).</summary>
        /// <summary>
        /// The clock the drawn sea reads — and it is deliberately <b>the same one the RIDDEN sea
        /// reads</b>. <c>BoatController</c> samples <c>WaveMath.Sample(pos, GameServices.Clock.TotalSeconds)</c>;
        /// since PR E the presentation path evaluates ω·t at this same value, so the two are one
        /// function of one clock rather than two paths that happen to agree (P1, SEE == FEEL).
        ///
        /// <para>⚠️ Falls back to <c>Time.timeAsDouble</c> only where no game clock is installed —
        /// an EditMode fixture or a scene without the persistent core. That fallback is NOT
        /// deterministic and must never be what ships; <c>GameServices.Clock</c> is.</para>
        /// </summary>
        public static double GameTimeSeconds =>
            GameServices.Clock != null ? GameServices.Clock.TotalSeconds : Time.timeAsDouble;

        private static double Wrap(double radians)
        {
            radians -= Math.Floor(radians / TwoPi) * TwoPi;
            return radians;
        }
    }
}
