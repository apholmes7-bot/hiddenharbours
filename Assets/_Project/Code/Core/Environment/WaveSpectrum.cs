using System;
using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// The JONSWAP-shaped wave spectrum (ADR 0027 decision (6), P2) — the pure math behind
    /// <see cref="WaveMath.TrainsFrom"/>'s spectrum branch, kept separate so every piece is
    /// headless-testable on its own rather than only through the assembled field.
    ///
    /// <para><b>What the owner asked for.</b> His P0 verdict was that the sea reads as *"a rigid
    /// pattern"* at every non-storm weather. Three named symptoms, three mechanisms here:
    /// <list type="bullet">
    /// <item><b>Variance in sizes</b> → <see cref="JonswapShape"/>: amplitudes come from a spectral
    ///   curve, not from three hand-set fractions of the primary.</item>
    /// <item><b>A continuous spread of directions</b> → <see cref="DirectionalWeight"/>: a
    ///   <c>cos^2s</c> fan about the wind, not three discrete axes.</item>
    /// <item><b>Waves that build and die</b> → <see cref="FrequencyRatio"/>: neighbouring trains sit
    ///   close enough in frequency to <b>beat</b>, which is what wave groups physically are. Grouping
    ///   is not a new oscillator and there is no code for it here — it falls out of the spacing, and
    ///   <see cref="BeatPeriodSeconds"/> exists so a test can prove the period is readable.</item>
    /// </list></para>
    ///
    /// <para><b>Explicitly NOT an FFT</b> (ADR 0027, rejected alternatives). The value is the
    /// spectrum's <em>shape</em>, which a weighted train sum carries; an FFT is what would make the
    /// headless twinning intractable. Everything here is a closed form over a slot index.</para>
    ///
    /// <para><b>Determinism (rule 5).</b> Pure, stateless, allocation-free. The only non-analytic
    /// input is the angular fan, which hashes off <c>(slot, seed)</c> through the same integer hash
    /// the phase offsets use — the WeatherModel discipline, no RNG anywhere.</para>
    /// </summary>
    public static class WaveSpectrum
    {
        /// <summary>Slot 0 carries the spectral PEAK — the frequency ratio ω/ω_p = 1 — and every other
        /// slot steps away from it in alternating directions. Two consequences worth naming: the
        /// dominant train is structurally slot 0 (so the flat-weighting convention every phase
        /// consumer was built on stays true, rather than being quietly broken), and slots 1 and 2 are
        /// the peak's NEAREST neighbours in frequency, which is where the strongest beat — the wave
        /// group — comes from.</summary>
        private static readonly int[] SlotFrequencySteps = { 0, +1, -1, +2, -2, +3, -3, +4 };

        /// <summary>Fallback for a frequency spacing that arrives as zero. ⚠️ This is not padding: a
        /// spacing of 0 puts every train at the SAME frequency, which is a single wave with N times
        /// the amplitude — the exact opposite of a spectrum. Zero is never a meaningful setting, and
        /// it is what a prefab serialized before this field existed will deserialize to.</summary>
        public const float DefaultFrequencySpacing = 0.08f;

        /// <summary>Fallback for a peak width that arrives as zero (JONSWAP's σ). 0 would make the
        /// peak-enhancement exponent divide by zero.</summary>
        public const float DefaultPeakWidth = 0.08f;

        /// <summary>
        /// The JONSWAP energy density at a frequency ratio <c>r = ω/ω_p</c>, relative and
        /// unnormalized (the constant α cancels — only the SHAPE is used):
        /// <c>S(r) = r⁻⁵·exp(−1.25·r⁻⁴)·γ^exp(−(r−1)²/(2σ²))</c>.
        ///
        /// <para>The first two factors are Pierson-Moskowitz, whose maximum is at exactly
        /// <c>r = 1</c>; the <c>γ</c> factor is JONSWAP's peak enhancement, also centred on
        /// <c>r = 1</c>. So the peak is at the peak by construction, which is what lets slot 0 stay
        /// the dominant train.</para>
        ///
        /// <para>Amplitude is <c>√(S·Δω)</c>; with the uniform spacing this module uses, Δω is common
        /// to every slot and falls out of the normalization, so callers take <c>√S</c>.</para>
        /// </summary>
        /// <param name="frequencyRatio">ω/ω_p; floored so a degenerate ratio cannot divide by zero.</param>
        /// <param name="peakEnhancement">γ. 1 = plain Pierson-Moskowitz (a broad, open-ocean swell);
        /// JONSWAP's fetch-limited standard is 3.3 — a taller, narrower peak, i.e. a sea with a more
        /// dominant wave size. Clamped ≥ 1.</param>
        /// <param name="peakWidth">σ, how wide the enhancement reaches around the peak.</param>
        public static float JonswapShape(float frequencyRatio, float peakEnhancement, float peakWidth)
        {
            float r = Mathf.Max(frequencyRatio, 1e-3f);
            float gamma = Mathf.Max(peakEnhancement, 1f);
            float sigma = peakWidth > 0f ? peakWidth : DefaultPeakWidth;

            float rInv4 = 1f / (r * r * r * r);
            float pm = rInv4 / r * Mathf.Exp(-1.25f * rInv4);        // r⁻⁵·exp(−1.25 r⁻⁴)

            float d = (r - 1f) / sigma;
            float enhancement = Mathf.Pow(gamma, Mathf.Exp(-0.5f * d * d));
            return pm * enhancement;
        }

        /// <summary>
        /// The directional weight applied to a train sitting <paramref name="angleOffsetRadians"/> off
        /// the wind: the <c>cos^2s</c> spreading function of ADR 0027 decision (6). Returned as the
        /// <b>amplitude</b> weight <c>cos^s(θ)</c> — the square root of the energy weight, because
        /// amplitude ∝ √energy.
        ///
        /// <para>Zero beyond ±90°: a train travelling against the wind carries no wind energy.
        /// <paramref name="spreadExponent"/> = 0 weights every direction equally (a fully confused
        /// sea); larger values pull the energy into a narrow following fan.</para>
        /// </summary>
        public static float DirectionalWeight(float angleOffsetRadians, float spreadExponent)
        {
            float cos = Mathf.Cos(angleOffsetRadians);
            if (cos <= 0f) return 0f;
            return Mathf.Pow(cos, Mathf.Max(spreadExponent, 0f));
        }

        /// <summary>
        /// The frequency ratio <c>ω_i/ω_p</c> for a slot: <c>1 + step(i)·spacing</c>, where the steps
        /// alternate outward from the peak (see <see cref="SlotFrequencySteps"/>). Floored well above
        /// zero so a large spacing on a low slot cannot produce a non-positive frequency.
        /// </summary>
        public static float FrequencyRatio(int slot, float relativeSpacing)
        {
            float spacing = relativeSpacing > 0f ? relativeSpacing : DefaultFrequencySpacing;
            int step = slot >= 0 && slot < SlotFrequencySteps.Length ? SlotFrequencySteps[slot] : 0;
            return Mathf.Max(0.05f, 1f + step * spacing);
        }

        /// <summary>
        /// Wavelength ratio <c>λ_i/λ_p</c> for a slot. Deep-water dispersion gives
        /// <c>ω = √(2πg/λ)</c>, so <c>λ ∝ ω⁻²</c> — the wavelength ratio is the frequency ratio
        /// squared and inverted. Derived here rather than authored, so the spectrum cannot drift out
        /// of step with the dispersion relation <see cref="WaveTrain"/> enforces.
        /// </summary>
        public static float WavelengthRatio(int slot, float relativeSpacing)
        {
            float r = FrequencyRatio(slot, relativeSpacing);
            return 1f / (r * r);
        }

        /// <summary>
        /// The angular offset (radians, signed) for a slot's train: a stratified fan across
        /// ±<paramref name="maxSpreadRadians"/>, jittered inside each stratum by the deterministic
        /// hash.
        ///
        /// <para><b>Slot 0 is exactly 0</b> — the peak runs downwind, which is what makes the whole
        /// blend continuous back to the legacy field. The rest are stratified rather than purely
        /// hashed so seven directions cannot happen to cluster on one side, and jittered rather than
        /// evenly spaced because an even fan IS a symmetric interference pattern — the very read
        /// <c>WaveFieldSettings</c>' asymmetric hand-authored angles exist to avoid.</para>
        /// </summary>
        public static float AngleOffsetRadians(int slot, int seed, float maxSpreadRadians)
        {
            if (slot <= 0) return 0f;

            int strata = Mathf.Max(1, WaveTrains.MaxTrains - 1);
            int bin = Mathf.Clamp(slot - 1, 0, strata - 1);
            float jitter = Hash01(slot, seed) - 0.5f;                 // stays inside its own stratum
            float u = (bin + 0.5f + jitter) / strata;                 // 0..1 across the fan
            return Mathf.Max(0f, maxSpreadRadians) * (u * 2f - 1f);
        }

        /// <summary>
        /// The BEAT period (seconds) between two neighbouring spectrum trains — the period of the
        /// wave GROUP, which is the thing "three big ones then a lull" actually names.
        ///
        /// <para>Two trains at ω₁ and ω₂ superpose into a carrier at their mean modulated by an
        /// envelope at <c>|ω₁−ω₂|/2</c>, so the envelope repeats every <c>2π/|ω₁−ω₂|</c>. With the
        /// peak at ω_p and neighbours a fraction <c>spacing</c> away, that is
        /// <c>2π/(ω_p·spacing)</c>. Exposed so a test can pin the period into a READABLE band rather
        /// than trusting that closely-spaced frequencies happen to beat on a human timescale — the
        /// handoff's explicit requirement.</para>
        /// </summary>
        /// <param name="dominantWavelengthMeters">λ_p (m).</param>
        /// <param name="gravity">g (m/s²) — the same one the trains disperse with.</param>
        /// <param name="relativeSpacing">Δω/ω_p between neighbouring slots.</param>
        public static float BeatPeriodSeconds(float dominantWavelengthMeters, float gravity,
                                              float relativeSpacing)
        {
            float lambda = Mathf.Max(dominantWavelengthMeters, WaveTrain.MinWavelengthMeters);
            float g = Mathf.Max(gravity, 1e-4f);
            float spacing = relativeSpacing > 0f ? relativeSpacing : DefaultFrequencySpacing;

            // ω_p = √(2πg/λ_p) — the angular frequency implied by c = √(gλ/2π) and k = 2π/λ.
            float omegaPeak = Mathf.Sqrt(2f * Mathf.PI * g / lambda);
            return (2f * Mathf.PI) / Mathf.Max(omegaPeak * spacing, 1e-6f);
        }

        /// <summary>Deterministic [0, 1) hash of (slot, seed) — the WeatherModel/`WaveMath` integer
        /// hash family, offset by a different salt so a slot's ANGLE and its PHASE are not the same
        /// number (they would otherwise correlate, tying every train's direction to its phase).</summary>
        private static float Hash01(int slot, int seed)
        {
            unchecked
            {
                int n = (slot * 83492791) ^ (seed * 19349663) ^ 0x27d4eb2d;
                n = (n << 13) ^ n;
                int m = (n * (n * n * 15731 + 789221) + 1376312589) & 0x7fffffff;
                return m / 2147483648f;
            }
        }
    }
}
