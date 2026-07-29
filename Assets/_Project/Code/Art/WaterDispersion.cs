using UnityEngine;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// Pure, headless-testable twins of the water shader's ADR 0027 #4 + #9 laws — band wavelengths
    /// that scale with sea state, and band speeds DERIVED from wavelength by the dispersion relation
    /// (mirrored in <c>HiddenHarboursWater.shader</c>: <c>BandFreq</c> / <c>DispersionDeepSpeed</c> /
    /// <c>DispersionPhaseSpeed</c> / <c>DispersionBandSpeed</c> / <c>DispersionSwellRate</c> /
    /// <c>DispersionShoalShift</c> — change one, change BOTH in the same PR).
    ///
    /// <para><b>#4 — wavelength grows with sea state.</b> Real seas grow in WAVELENGTH as they build,
    /// not only in amplitude; the visual noise octaves' spatial frequencies were fixed. Each band's
    /// effective frequency is its authored scale × <see cref="BandFrequencyFactor"/> —
    /// <c>1 / (1 + response·chop)</c>, i.e. the band's wavelength grows linearly in the already-pushed
    /// <c>_Chop</c>. Response 0 = exactly 1 = today's fixed wavelengths (the passthrough contract).
    /// The wave FIELD's trains are deliberately untouched: <c>WaveMath.TrainsFrom</c> already grows
    /// the dominant wavelength with wind (<c>DominantWavelengthPerWindSpeed</c>) — applying this
    /// factor to <c>waveFreqScale</c> would double-apply the law AND move drawn geometry the
    /// interior-mask/clamp stack guards (the <c>_OceanSwellScale</c> incident class; see the PR's
    /// consumer audit).</para>
    ///
    /// <para><b>#9 — speed from wavelength.</b> Each octave carried an independent hand-tuned speed
    /// with no relationship to its wavelength — a large part of why the multi-scale sea reads as
    /// stacked layers sliding over each other (the legacy speeds are ANTI-dispersive: the 40 m swell
    /// band scrolls slower than the 1.4 m chop). <see cref="PhaseSpeed"/> is the full
    /// finite-depth dispersion relation <c>c = √(g·λ/2π · tanh(2π·d/λ))</c>: the deep-water
    /// <c>√(gλ/2π)</c> when <c>d ≫ λ</c>, the depth-limited <c>√(g·d)</c> as <c>d → 0</c>, one
    /// continuous formula so the two forms agree at the transition by construction.
    /// <see cref="BandSpeed"/> blends each band from its legacy hand-set speed toward
    /// <c>bandMult × deep-water c(λ)</c> by the master <c>_DispersionScale</c>;
    /// <c>_DispersionScale = 0</c> returns the legacy speed EXACTLY (bit-for-bit — the lerp's 0
    /// endpoint), which is the contract the handoff pins. The per-band multipliers keep the owner's
    /// art direction (rule 6); their default 0.06 is anchored so the WIND-CHOP band at its default
    /// wavelength (1/0.7 m) lands on its legacy 0.09 m/s at full dispersion — dialing the master up
    /// re-ties the other bands to physics around an unchanged fastest band.</para>
    ///
    /// <para><b>Why the temporal rate uses the DEEP form and the shallow physics enters as a bounded
    /// STATIC shift (<see cref="ShoalShift"/>).</b> A per-pixel depth-dependent scroll RATE on an
    /// absolute-world-coordinate band accumulates unbounded domain shear: the pattern offset between
    /// two depths differs by <c>Δc·t</c>, which grows without limit — measured against the legacy
    /// swell band, ~26× the base wavenumber of spurious frequency after 600 s at a typical shoal.
    /// The stable equivalent: shift the band's sample position ALONG its travel direction by
    /// <c>bunch × λ × (1 − c(λ,d)/c_deep(λ))</c> — bounded (≤ bunch·λ), static in time, derived from
    /// the SAME read-only depth every other layer consumes. Where that shift's along-travel gradient
    /// compresses the domain by a factor m, the local wavelength shortens by m AND the apparent
    /// phase speed drops by m — waves genuinely slow and bunch approaching shore, with zero
    /// time-accumulating artefacts. (For a band travelling offshore the same term stretches instead;
    /// the §5.12 shoreward bias already steers the bands shoreward near the coast, and
    /// <c>_ShorewardBias</c> itself is deliberately untouched — whether #9 subsumes part of it is an
    /// open question to be measured later, per the ADR.)</para>
    ///
    /// <para>Everything here drives VISUAL octaves only — never <c>depth</c>/<c>clip()</c>/
    /// <c>_WaterLevel</c>/the height read/<c>_WaveFieldParams</c>/anything the hulls ride
    /// (P1 integrity, CLAUDE.md rule 5; the see/feel gap is accepted and explicit for P1).</para>
    /// </summary>
    public static class WaterDispersion
    {
        /// <summary>Standard gravity (m/s²) — the one physical constant of the dispersion relation
        /// (mirrored as <c>WATER_GRAVITY</c> in the shader). Not a tunable: feel lives in
        /// <c>_DispersionScale</c> and the per-band multipliers (rule 6).</summary>
        public const float Gravity = 9.81f;

        /// <summary>Floor for wavelength arguments so a degenerate scale can never divide by zero
        /// (mirrors the shader's <c>max(λ, 1e-3)</c>).</summary>
        public const float MinWavelength = 1e-3f;

        /// <summary>
        /// #4 — the band frequency factor: <c>1 / (1 + response·chop)</c>. A band's effective spatial
        /// frequency is its authored scale × this (the shader divides by the denominator directly —
        /// same value; at response 0 both are exactly 1, the passthrough). Wavelength therefore grows
        /// linearly in sea state: <c>λ_eff = λ·(1 + response·chop)</c>.
        /// </summary>
        /// <param name="chop">The sim-pushed sea state (<c>_Chop</c>, 0 glass .. 1 storm; clamped).</param>
        /// <param name="response">The owner's growth dial (<c>_BandScaleResponse</c>; 0 = fixed).</param>
        public static float BandFrequencyFactor(float chop, float response)
        {
            return 1f / (1f + Mathf.Max(response, 0f) * Mathf.Clamp01(chop));
        }

        /// <summary>Deep-water phase speed <c>c = √(g·λ/2π)</c> (m/s).</summary>
        public static float DeepPhaseSpeed(float wavelength)
        {
            float l = Mathf.Max(wavelength, MinWavelength);
            return Mathf.Sqrt(Gravity * l / (2f * Mathf.PI));
        }

        /// <summary>
        /// The finite-depth phase speed <c>c = √(g·λ/2π · tanh(2π·d/λ))</c> (m/s) — THE #9 twin.
        /// Monotone increasing in wavelength (long waves outrun short ones); the shallow branch slows
        /// toward zero depth (<c>c → √(g·d)</c> as <c>d → 0</c>, exactly the depth-limited form);
        /// deep and shallow forms agree at the transition because this is one continuous formula.
        /// </summary>
        /// <param name="wavelength">Crest-to-crest wavelength λ (m; floored).</param>
        /// <param name="depth">Still-water depth (m; clamped ≥ 0 — the same read-only depth every
        /// other layer consumes).</param>
        public static float PhaseSpeed(float wavelength, float depth)
        {
            float l = Mathf.Max(wavelength, MinWavelength);
            float k = 2f * Mathf.PI / l;
            float t = (float)System.Math.Tanh(k * Mathf.Max(depth, 0f));
            return DeepPhaseSpeed(l) * Mathf.Sqrt(Mathf.Clamp01(t));
        }

        /// <summary>
        /// #9 — a WORLD-SPEED band's effective scroll speed: the legacy hand-set speed blended toward
        /// <c>bandMult × c_deep(λ)</c> by the master. <b>Scale 0 returns
        /// <paramref name="legacySpeed"/> EXACTLY</b> (bit-for-bit; the lerp's 0 endpoint) — the
        /// passthrough contract. The temporal rate deliberately uses the DEEP form (uniform per band
        /// per frame); the shallow physics enters via <see cref="ShoalShift"/> (see the class doc).
        /// </summary>
        public static float BandSpeed(float legacySpeed, float bandMult, float wavelength,
                                      float dispersionScale)
        {
            float target = Mathf.Max(bandMult, 0f) * DeepPhaseSpeed(wavelength);
            return Mathf.Lerp(legacySpeed, target, Mathf.Clamp01(dispersionScale));
        }

        /// <summary>
        /// #9 — the legacy ocean-swell band's effective PHASE RATE, in its native units (the phase is
        /// <c>dot(p·scale, dir) − t·rate</c>, so world speed = rate/scale). The blend happens at the
        /// NATIVE-rate level so scale 0 reproduces the hand-set 0.018 bit-for-bit (converting
        /// world→native→world would round). Target = <c>bandMult × c_deep(λ) × scaleEff</c>.
        /// </summary>
        public static float SwellPhaseRate(float legacyRate, float bandMult, float wavelength,
                                           float scaleEff, float dispersionScale)
        {
            float target = Mathf.Max(bandMult, 0f) * DeepPhaseSpeed(wavelength)
                         * Mathf.Max(scaleEff, 0f);
            return Mathf.Lerp(legacyRate, target, Mathf.Clamp01(dispersionScale));
        }

        /// <summary>
        /// #9's shallow half — the bounded, static along-travel drift (m) that bunches a band's
        /// wavefronts over a shoal: <c>saturate(s) · bunch · λ · (1 − c(λ,d)/c_deep(λ))</c>.
        /// 0 in deep water; → <c>s·bunch·λ</c> at zero depth; monotone as the water shallows; 0 at
        /// zero bunch or zero master. Where its along-travel gradient compresses the domain by m the
        /// band's local wavelength AND apparent speed both drop by m — slow-and-bunch off the same
        /// read-only depth, with no time-accumulating shear (the class doc's stability argument).
        /// </summary>
        public static float ShoalShift(float wavelength, float depth, float bunch,
                                       float dispersionScale)
        {
            float l = Mathf.Max(wavelength, MinWavelength);
            float slow = 1f - PhaseSpeed(l, depth) / DeepPhaseSpeed(l);
            return Mathf.Clamp01(dispersionScale) * Mathf.Max(bunch, 0f) * l
                 * Mathf.Clamp01(slow);
        }
    }
}
