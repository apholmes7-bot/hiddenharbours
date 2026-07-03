using System;
using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// One directional wave train — a single member of the shared deterministic wave field
    /// (ADR 0018): a travel direction, a wavelength, an amplitude and a phase offset.
    ///
    /// <para><b>Dispersion is canon (owner ruling, ADR 0018 §(1)).</b> <see cref="PhaseSpeed"/> is
    /// <b>derived</b> in the constructor from the deep-water dispersion relation
    /// <c>c = √(g·λ / 2π)</c> — "the larger the distance between crests, the faster the wave" — so
    /// long swells visibly outrun short chop and a mixed sea reads true. A train carries its
    /// wavelength; its speed is <b>never an independent tunable</b>. There is deliberately no way to
    /// construct a train with a free-standing speed.</para>
    /// </summary>
    public readonly struct WaveTrain
    {
        /// <summary>Guard floor for <see cref="Wavelength"/> (metres) so a degenerate/zeroed input can
        /// never divide by zero in the wave number <c>k = 2π/λ</c>. A guard, not a tunable.</summary>
        public const float MinWavelengthMeters = 0.01f;

        /// <summary>Unit direction of travel in world space — the way the crests advance
        /// (downwind for the primary train). Normalized by the constructor; a (near-)zero input falls
        /// back to +Y (north) as a defined, deterministic direction.</summary>
        public readonly Vector2 Direction;

        /// <summary>Crest-to-crest wavelength λ (metres), clamped ≥ <see cref="MinWavelengthMeters"/>.</summary>
        public readonly float Wavelength;

        /// <summary>Amplitude (metres) — half the crest-to-trough height, clamped ≥ 0. Exactly 0 means
        /// this train contributes nothing (glass calm is sacred: no minimum swell — ADR 0018 §(1)).</summary>
        public readonly float Amplitude;

        /// <summary>Phase speed c (m/s) — <b>derived</b> from <see cref="Wavelength"/> via
        /// <c>c = √(g·λ / 2π)</c> at construction. Read-only by design; see the struct doc.</summary>
        public readonly float PhaseSpeed;

        /// <summary>Phase offset (radians) — deterministic variety so the trains do not all crest
        /// together at t = 0. Hashed from the train index + a seed, never random (rule 5).</summary>
        public readonly float PhaseOffset;

        /// <summary>
        /// Build a train. <paramref name="direction"/> is normalized (near-zero → +Y), the wavelength
        /// and amplitude are clamped to sane floors, and the phase speed is derived from
        /// <paramref name="gravity"/> + wavelength via the dispersion relation — the one place that
        /// formula lives on the C# side (the HLSL twin receives the derived speed as data and never
        /// re-derives it, so the relation cannot fork).
        /// </summary>
        public WaveTrain(Vector2 direction, float wavelengthMeters, float amplitudeMeters,
                         float phaseOffsetRadians, float gravity)
        {
            float sqrMagnitude = direction.x * direction.x + direction.y * direction.y;
            if (sqrMagnitude < 1e-12f)
            {
                Direction = Vector2.up;
            }
            else
            {
                float invMagnitude = 1f / Mathf.Sqrt(sqrMagnitude);
                Direction = new Vector2(direction.x * invMagnitude, direction.y * invMagnitude);
            }

            Wavelength = Mathf.Max(MinWavelengthMeters, wavelengthMeters);
            Amplitude = Mathf.Max(0f, amplitudeMeters);
            PhaseSpeed = Mathf.Sqrt(Mathf.Max(0f, gravity) * Wavelength / (2f * Mathf.PI));
            PhaseOffset = phaseOffsetRadians;
        }
    }

    /// <summary>
    /// The whole wave field for one moment's weather: a fixed, allocation-free container of up to
    /// <see cref="MaxTrains"/> <see cref="WaveTrain"/>s plus the crest-sharpening factor they share.
    /// Produced by <see cref="WaveMath.TrainsFrom"/> on the throttled sim cadence and handed to
    /// <see cref="WaveMath.Sample"/> (sim side) or published as shader globals by the Art-side bridge
    /// (B1) — the same trains on both sides, by construction.
    /// </summary>
    public readonly struct WaveTrains
    {
        /// <summary>Hard capacity of the container (and of the shader-global arrays the HLSL twin
        /// reads). ADR 0018 fixes the model at 3–4 trains; 4 is the ceiling on both sides.</summary>
        public const int MaxTrains = 4;

        private readonly WaveTrain _train0;
        private readonly WaveTrain _train1;
        private readonly WaveTrain _train2;
        private readonly WaveTrain _train3;

        /// <summary>How many of the slots are live, in [0, <see cref="MaxTrains"/>]. Slots at or
        /// beyond this index are undefined and must not be read.</summary>
        public readonly int Count;

        /// <summary>Crest sharpening p ≥ 1 (clamped at construction): the cheap shaping exponent that
        /// pinches crests narrow above broad troughs (ADR 0018 §(1)). 1 = pure sine, higher = spikier
        /// crests. Also concentrates <see cref="WaveSample.CrestFactor"/> toward the crest tips.</summary>
        public readonly float CrestSharpening;

        /// <summary>Assemble a field. <paramref name="count"/> is clamped into
        /// [0, <see cref="MaxTrains"/>]; <paramref name="crestSharpening"/> is clamped ≥ 1. Unused
        /// slots may be passed as <c>default</c>.</summary>
        public WaveTrains(in WaveTrain train0, in WaveTrain train1, in WaveTrain train2,
                          in WaveTrain train3, int count, float crestSharpening)
        {
            _train0 = train0;
            _train1 = train1;
            _train2 = train2;
            _train3 = train3;
            Count = Mathf.Clamp(count, 0, MaxTrains);
            CrestSharpening = Mathf.Max(1f, crestSharpening);
        }

        /// <summary>The train in a live slot (index in [0, <see cref="Count"/>)).</summary>
        public WaveTrain this[int index] => index switch
        {
            0 => _train0,
            1 => _train1,
            2 => _train2,
            3 => _train3,
            _ => throw new ArgumentOutOfRangeException(nameof(index), index,
                     "WaveTrains holds at most " + MaxTrains + " trains."),
        };

        /// <summary>Sum of the live trains' amplitudes (metres) — the field's height envelope: the
        /// tallest possible crest above (and deepest trough below) the tide level. 0 = dead glass.
        /// The same normalizer <see cref="WaveMath.Sample"/> uses for the crest factor.</summary>
        public float TotalAmplitude
        {
            get
            {
                float total = 0f;
                for (int i = 0; i < Count; i++) total += this[i].Amplitude;
                return total;
            }
        }

        /// <summary>The empty field (no trains, dead glass). Equivalent to <c>default</c>.</summary>
        public static readonly WaveTrains None = default;
    }

    /// <summary>
    /// The wave surface at one position and time — what a hull probe or a water pixel reads.
    /// Heights are metres <b>about the tide level</b>: the field rides ON
    /// <c>IEnvironmentService.WaterLevelAt(t)</c> and never moves the walkability waterline
    /// (the ADR 0009 tide/exposure seam is untouched by waves).
    /// </summary>
    public readonly struct WaveSample
    {
        /// <summary>Surface offset (metres) about the tide level: positive on a crest, negative in a
        /// trough, bounded by ±<see cref="WaveTrains.TotalAmplitude"/>.</summary>
        public readonly float Height;

        /// <summary>Surface gradient ∂Height/∂(x,y) — the analytic spatial derivative of
        /// <see cref="Height"/>, not a finite difference. The "which way does the deck tilt" read:
        /// B2 rolls a beam sea and pitches a head sea from this against the hull's heading.</summary>
        public readonly Vector2 Slope;

        /// <summary>0..1, concentrated at sharpened crest tips and 0 through the troughs — the foam
        /// driver B1 keys the whitecap lifecycle off (form → break → streak → fade). Exactly 0 on a
        /// dead-glass sea.</summary>
        public readonly float CrestFactor;

        public WaveSample(float height, Vector2 slope, float crestFactor)
        {
            Height = height;
            Slope = slope;
            CrestFactor = crestFactor;
        }

        /// <summary>The flat, dead-calm surface (glass). Equivalent to <c>default</c>.</summary>
        public static readonly WaveSample Flat = default;
    }

    /// <summary>
    /// Every constant of the wind → wave-train derivation, named and owner-tunable (rule 6) — how
    /// many secondary trains, where they sit relative to the wind, how their wavelengths and
    /// amplitudes scale off the primary, how the dominant wavelength grows with wind speed, the
    /// crest sharpening, and g. Serializable so the Arc B consumer PRs can surface it on
    /// <c>GameConfig</c> for the owner; until then <see cref="Default"/> is the reference tuning
    /// (start from it — a zeroed struct is a dead flat sea).
    /// </summary>
    [Serializable]
    public struct WaveFieldSettings
    {
        [Tooltip("Gravitational acceleration g (m/s²) for the deep-water dispersion relation c = √(g·λ/2π). Earth = 9.81. Not a style knob — change only if the sea should read heavier/lighter wholesale.")]
        public float Gravity;

        [Tooltip("How many cross-chop trains ride with the primary downwind train (0–3). 3 → the full 4-train field; 2 → 3 trains (cheaper, reads slightly cleaner). ADR 0018 leaves 3-vs-4 to B1 tuning.")]
        public int SecondaryTrainCount;

        [Tooltip("Dominant (primary-train) wavelength in metres when the wind is dead calm.")]
        public float DominantWavelengthBase;

        [Tooltip("Extra dominant wavelength (metres) per m/s of wind speed — stronger wind builds a longer, faster primary swell.")]
        public float DominantWavelengthPerWindSpeed;

        [Tooltip("Ceiling on the dominant wavelength (metres) so a gale cannot stretch the swell absurdly.")]
        public float DominantWavelengthMax;

        [Tooltip("Primary-train amplitude (metres) at full sea state (SeaState01 = 1). Everything scales down from here; at SeaState01 = 0 all amplitudes are exactly 0 (glass is sacred).")]
        public float PrimaryAmplitude;

        [Tooltip("Response curve of amplitude to SeaState01: amplitudeScale = SeaState01^exponent. 1 = linear; >1 keeps low sea states gentler and lets the top end arrive late.")]
        public float SeaStateAmplitudeExponent;

        [Tooltip("Crest sharpening p (≥1): pinches crests narrow above broad troughs. 1 = pure sine mush; ~2–3 reads as real crests.")]
        public float CrestSharpening;

        [Tooltip("Deterministic seed for the trains' phase offsets (hashed, never random). Consumers may feed the world seed here; the default 0 is fine — it only de-synchronizes the trains' crests.")]
        public int PhaseSeed;

        [Tooltip("Secondary train 1: signed angle off the downwind direction, degrees (counter-clockwise positive, math convention).")]
        public float Secondary1AngleDegrees;
        [Tooltip("Secondary train 1: wavelength as a fraction of the dominant wavelength (<1 = shorter chop).")]
        public float Secondary1WavelengthRatio;
        [Tooltip("Secondary train 1: amplitude as a fraction of the primary amplitude.")]
        public float Secondary1AmplitudeRatio;

        [Tooltip("Secondary train 2: signed angle off the downwind direction, degrees.")]
        public float Secondary2AngleDegrees;
        [Tooltip("Secondary train 2: wavelength fraction of the dominant.")]
        public float Secondary2WavelengthRatio;
        [Tooltip("Secondary train 2: amplitude fraction of the primary.")]
        public float Secondary2AmplitudeRatio;

        [Tooltip("Secondary train 3: signed angle off the downwind direction, degrees.")]
        public float Secondary3AngleDegrees;
        [Tooltip("Secondary train 3: wavelength fraction of the dominant.")]
        public float Secondary3WavelengthRatio;
        [Tooltip("Secondary train 3: amplitude fraction of the primary.")]
        public float Secondary3AmplitudeRatio;

        /// <summary>
        /// The reference tuning (ADR 0018 Arc B starting point): a 4-train field — the primary
        /// downwind swell plus three shorter, smaller cross-chop trains at asymmetric offsets
        /// (+32°, −47°, +11°) so the sea never reads as a symmetric interference pattern. Dominant
        /// wavelength 6 m in calm air growing 1.5 m per m/s of wind (capped 40 m); primary amplitude
        /// 0.8 m at full sea; amplitude response SeaState01^1.35; crest sharpening 2.2.
        /// </summary>
        public static WaveFieldSettings Default => new WaveFieldSettings
        {
            Gravity = 9.81f,
            SecondaryTrainCount = 3,
            DominantWavelengthBase = 6f,
            DominantWavelengthPerWindSpeed = 1.5f,
            DominantWavelengthMax = 40f,
            PrimaryAmplitude = 0.8f,
            SeaStateAmplitudeExponent = 1.35f,
            CrestSharpening = 2.2f,
            PhaseSeed = 0,
            Secondary1AngleDegrees = 32f,
            Secondary1WavelengthRatio = 0.55f,
            Secondary1AmplitudeRatio = 0.45f,
            Secondary2AngleDegrees = -47f,
            Secondary2WavelengthRatio = 0.38f,
            Secondary2AmplitudeRatio = 0.30f,
            Secondary3AngleDegrees = 11f,
            Secondary3WavelengthRatio = 0.22f,
            Secondary3AmplitudeRatio = 0.18f,
        };
    }

    /// <summary>
    /// The <b>one shared deterministic wave field</b> (ADR 0018): a small sum of directional wave
    /// trains derived purely from the deterministic wind + the continuous sea-state axis, sampled by
    /// BOTH the simulation (seakeeping — a beam sea rocks the vessel, P1/P5) and the water shader
    /// (swell displacement, whitecaps riding real crests). It lives in Core because two feature
    /// lanes consume it (Boats and Art each reference only Core — rule 4); the placement precedent
    /// is <see cref="TidalExposure"/> / <see cref="BoatKinematics"/>.
    ///
    /// <para><b>Determinism (rule 5).</b> Pure, stateless, allocation-free: same
    /// <c>(windVector, seaState01)</c> → the same trains; same <c>(position, time, trains)</c> → the
    /// same sample — forever, on every machine. The wind is already deterministic from
    /// <c>(worldSeed, gameTime)</c> (WeatherModel), so the field is too. No RNG (phase variety is an
    /// integer hash, the WeatherModel discipline), <b>nothing is ever saved</b> — waves are
    /// recomputed, like tide and wind. Consumers read wind + sea state from
    /// <c>EnvironmentSample</c> (<c>WindVector</c>, <c>SeaState01</c>) via <c>GameServices</c>;
    /// this class deliberately takes plain values so it stays engine-light and headless-testable.</para>
    ///
    /// <para><b>The HLSL twin contract (ADR 0018 §(4)).</b> This class is the <b>reference
    /// implementation</b>. The water shader carries a line-by-line HLSL transcription of
    /// <see cref="Sample"/> (landing with the B1 swell/whitecap rework) reading the same trains as
    /// packed shader globals published by the Art-side bridge — <b>any change to the math here must
    /// change the shader twin in the same PR</b>, exactly as <c>DayNightMath</c>/<c>MoonMath</c>/
    /// <c>WaterReflection</c> are kept in lockstep. The twin receives each train's
    /// <see cref="WaveTrain.PhaseSpeed"/> as data and never re-derives it. Parity is visual, not
    /// bitwise (the GPU runs fast-math float): the pinned-grid EditMode tests
    /// (<c>WaveMathTests</c>) are the numbers a twin review diffs against.</para>
    ///
    /// <para><b>Owner rulings baked in (ADR 0018, 2026-07-02):</b> dispersion is canon (speed derives
    /// from wavelength — see <see cref="WaveTrain"/>); glass calm is sacred (at
    /// <c>seaState01 = 0</c> every amplitude is exactly 0 — no minimum swell, the sea is the full
    /// mirror for the reflection layers).</para>
    ///
    /// <para><b>What waves are NOT.</b> The height is an offset <em>about</em> the tide level —
    /// <see cref="TidalExposure"/> and the walkability waterline never read it. Note the sharpened
    /// profile spends longer in its broad troughs than at its narrow crests, so the time-average
    /// surface sits a touch below the tide level at high sea states — a look/feel detail, invisible
    /// to the exposure seam by construction.</para>
    /// </summary>
    public static class WaveMath
    {
        /// <summary>Below this total amplitude (metres) the sea is treated as dead glass and
        /// <see cref="WaveSample.CrestFactor"/> is exactly 0 (guards the 0/0 of normalizing height by
        /// the amplitude envelope). A guard, not a tunable.</summary>
        public const float GlassAmplitudeMeters = 1e-6f;

        private const double TwoPi = Math.PI * 2.0;

        /// <summary>
        /// Derive the wave field from the weather — the pure function at the heart of ADR 0018 §(1).
        /// The primary train runs <b>downwind</b> at the dominant wavelength (which grows with wind
        /// speed); up to three secondary trains sit at the settings' angular offsets with shorter
        /// wavelengths and smaller amplitudes (the cross-chop that makes a real sea read). Every
        /// amplitude scales with <c>seaState01^exponent</c> and is <b>exactly 0 at
        /// <paramref name="seaState01"/> = 0</b> (glass is sacred). Phase offsets hash off the train
        /// index + <see cref="WaveFieldSettings.PhaseSeed"/> — deterministic, no RNG.
        /// </summary>
        /// <param name="windVector">Wind as direction × strength (m/s) — <c>EnvironmentSample.WindVector</c>.
        /// A dead-calm (zero) wind keeps a defined downwind of +Y; amplitudes are what silence a calm sea.</param>
        /// <param name="seaState01">The continuous sea-state axis, 0 = glass .. 1 = full storm
        /// (<c>EnvironmentSample.SeaState01</c>). Clamped to [0, 1].</param>
        /// <param name="settings">The derivation constants — start from <see cref="WaveFieldSettings.Default"/>.</param>
        public static WaveTrains TrainsFrom(Vector2 windVector, float seaState01, in WaveFieldSettings settings)
        {
            float sea = Mathf.Clamp01(seaState01);

            // Glass calm is sacred: 0^e == 0 exactly for e > 0, so the whole field flattens to a
            // true mirror at sea state 0 — no minimum swell, no floor (owner ruling, ADR 0018 §(1)).
            float amplitudeScale = Mathf.Pow(sea, Mathf.Max(0.01f, settings.SeaStateAmplitudeExponent));

            float windSpeed = Mathf.Sqrt(windVector.x * windVector.x + windVector.y * windVector.y);
            Vector2 downwind = windSpeed > 1e-6f
                ? new Vector2(windVector.x / windSpeed, windVector.y / windSpeed)
                : Vector2.up;

            float wavelengthCeiling = Mathf.Max(WaveTrain.MinWavelengthMeters, settings.DominantWavelengthMax);
            float dominantWavelength = Mathf.Clamp(
                settings.DominantWavelengthBase + settings.DominantWavelengthPerWindSpeed * windSpeed,
                WaveTrain.MinWavelengthMeters, wavelengthCeiling);

            float primaryAmplitude = Mathf.Max(0f, settings.PrimaryAmplitude) * amplitudeScale;
            float gravity = settings.Gravity;

            var primary = new WaveTrain(
                downwind, dominantWavelength, primaryAmplitude,
                PhaseOffsetRadians(0, settings.PhaseSeed), gravity);

            var secondary1 = new WaveTrain(
                Rotate(downwind, settings.Secondary1AngleDegrees),
                dominantWavelength * settings.Secondary1WavelengthRatio,
                primaryAmplitude * Mathf.Max(0f, settings.Secondary1AmplitudeRatio),
                PhaseOffsetRadians(1, settings.PhaseSeed), gravity);

            var secondary2 = new WaveTrain(
                Rotate(downwind, settings.Secondary2AngleDegrees),
                dominantWavelength * settings.Secondary2WavelengthRatio,
                primaryAmplitude * Mathf.Max(0f, settings.Secondary2AmplitudeRatio),
                PhaseOffsetRadians(2, settings.PhaseSeed), gravity);

            var secondary3 = new WaveTrain(
                Rotate(downwind, settings.Secondary3AngleDegrees),
                dominantWavelength * settings.Secondary3WavelengthRatio,
                primaryAmplitude * Mathf.Max(0f, settings.Secondary3AmplitudeRatio),
                PhaseOffsetRadians(3, settings.PhaseSeed), gravity);

            int count = 1 + Mathf.Clamp(settings.SecondaryTrainCount, 0, WaveTrains.MaxTrains - 1);
            return new WaveTrains(primary, secondary1, secondary2, secondary3, count, settings.CrestSharpening);
        }

        /// <summary>
        /// Sample the wave surface at a world position and time — ADR 0018 §(2), the read both a hull
        /// probe (B2/B3) and, via the HLSL twin, every water pixel (B1) perform. Pure, stateless,
        /// allocation-free. Per train the phase is <c>θ = k·(d·pos − c·t) + φ</c>; the profile is the
        /// sharpened sine <c>A·(2·((sin θ + 1)/2)^p − 1)</c> (p = <see cref="WaveTrains.CrestSharpening"/>
        /// — narrow crests over broad troughs, the cheap Gerstner-style read sanctioned by the ADR);
        /// the slope is its <b>analytic</b> gradient <c>A·p·s^(p−1)·cos θ·k·d</c>, summed over trains;
        /// and the crest factor is the height normalized by the amplitude envelope, sharpened by the
        /// same p and clamped to [0, 1] — 1 only where the trains crest together, exactly 0 through
        /// the troughs and on a dead-glass sea.
        /// </summary>
        /// <param name="worldPos">World-space position (the same frame the wind field uses).</param>
        /// <param name="timeSeconds">Game time in seconds (<c>IClockService</c> total seconds). Double
        /// on purpose: the phase is accumulated in double and wrapped to [0, 2π) BEFORE dropping to
        /// float trig, so a long-running game never loses the wave to float drift.</param>
        /// <param name="trains">The field to sample — derive it via <see cref="TrainsFrom"/>.</param>
        public static WaveSample Sample(Vector2 worldPos, double timeSeconds, in WaveTrains trains)
        {
            float height = 0f;
            float slopeX = 0f;
            float slopeY = 0f;
            float totalAmplitude = 0f;
            float sharpening = trains.CrestSharpening;
            int count = trains.Count;

            for (int i = 0; i < count; i++)
            {
                WaveTrain train = trains[i];
                float amplitude = train.Amplitude;
                totalAmplitude += amplitude;
                if (amplitude <= 0f) continue; // a silent train contributes exactly nothing

                float waveNumber = (2f * Mathf.PI) / train.Wavelength;

                // Phase in double, wrapped to [0, 2π), THEN float trig — see timeSeconds doc.
                double travel = (double)train.Direction.x * worldPos.x
                              + (double)train.Direction.y * worldPos.y
                              - (double)train.PhaseSpeed * timeSeconds;
                double phase = waveNumber * travel + train.PhaseOffset;
                phase -= Math.Floor(phase / TwoPi) * TwoPi;
                float theta = (float)phase;

                float sin = Mathf.Sin(theta);
                float cos = Mathf.Cos(theta);

                float s = (sin + 1f) * 0.5f;                       // 0 in the trough .. 1 at the crest
                float shaped = Mathf.Pow(s, sharpening);            // pinch: narrow crest, broad trough
                height += amplitude * (2f * shaped - 1f);

                // d/dpos of the height term above — the ANALYTIC derivative (chain rule), not a
                // finite difference. At p = 1 this collapses to the pure-sine slope A·cosθ·k·d.
                float slopeMagnitude = amplitude * sharpening * Mathf.Pow(s, sharpening - 1f) * cos * waveNumber;
                slopeX += slopeMagnitude * train.Direction.x;
                slopeY += slopeMagnitude * train.Direction.y;
            }

            float crestFactor = 0f;
            if (totalAmplitude > GlassAmplitudeMeters)
            {
                // Height normalized by the envelope: 1 only where every train crests at once. The
                // clamp zeroes everything below the mean surface; the pow concentrates what's left
                // toward the crest tips (the whitecap driver, B1).
                crestFactor = Mathf.Pow(Mathf.Clamp01(height / totalAmplitude), sharpening);
            }

            return new WaveSample(height, new Vector2(slopeX, slopeY), crestFactor);
        }

        // ---- deterministic helpers (no RNG anywhere — rule 5) -----------------------------------

        /// <summary>Deterministic phase offset in [0, 2π) for a train slot: the WeatherModel-style
        /// integer hash (same constants, same discipline — one deterministic noise family across the
        /// sim), mapped to an angle. Same (index, seed) → same phase, forever.</summary>
        private static float PhaseOffsetRadians(int trainIndex, int seed)
        {
            unchecked
            {
                int n = (trainIndex * 73856093) ^ (seed * 19349663) ^ 0x5f3759df;
                n = (n << 13) ^ n;
                int m = (n * (n * n * 15731 + 789221) + 1376312589) & 0x7fffffff;
                return (m / 2147483648f) * (2f * Mathf.PI); // [0, 1) → [0, 2π)
            }
        }

        /// <summary>Rotate a vector by a signed angle in degrees (counter-clockwise positive, the
        /// standard math convention — a NEGATIVE settings angle therefore reads clockwise on screen).</summary>
        private static Vector2 Rotate(Vector2 v, float degrees)
        {
            float radians = degrees * Mathf.Deg2Rad;
            float cos = Mathf.Cos(radians);
            float sin = Mathf.Sin(radians);
            return new Vector2(cos * v.x - sin * v.y, sin * v.x + cos * v.y);
        }
    }
}
