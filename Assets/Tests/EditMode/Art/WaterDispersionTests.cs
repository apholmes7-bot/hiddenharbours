using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Art;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// Headless twin tests for the ADR 0027 #4 + #9 laws — <see cref="WaterDispersion"/>, the C#
    /// mirror of the shader's <c>BandFreq</c> / <c>DispersionDeepSpeed</c> /
    /// <c>DispersionPhaseSpeed</c> / <c>DispersionBandSpeed</c> / (the swell band's native-rate lerp)
    /// / <c>DispersionShoalShift</c> (change one, change BOTH in the same PR). Contracts pinned:
    ///
    /// <list type="bullet">
    /// <item>#4: response 0 ⇒ the frequency factor is EXACTLY 1 (fixed wavelengths — passthrough);
    /// wavelength grows linearly in chop;</item>
    /// <item>#9: phase speed is monotone increasing in wavelength (long waves outrun short ones);
    /// the shallow branch slows toward zero depth (→ √(g·d), exactly 0 at d = 0); deep and shallow
    /// forms agree at their transition (one continuous formula — pinned at both limits);</item>
    /// <item><b>the passthrough contract:</b> <c>_DispersionScale = 0</c> reproduces the three legacy
    /// hand-set speeds (0.09 / 0.025 / 0.018) BIT-FOR-BIT (exact equality, no tolerance);</item>
    /// <item>the 0.06 default multiplier anchors the wind-chop band at its legacy speed (full
    /// dispersion re-ties the slower bands to physics around an unchanged fastest band);</item>
    /// <item>the shoal shift is 0 in deep water, monotone as the water shallows, bounded by
    /// bunch·λ, and 0 at zero bunch/master.</item>
    /// </list>
    ///
    /// Sabotage MEASURED (2026-07-28, headless runs — see the PR), three arms, each targeted:
    /// <list type="number">
    /// <item>inverting the tanh depth direction (<c>tanh(k/d)</c> — the swapped deep/shallow
    /// behaviour): 3 of 11 fail — the shallow branch SPEEDS UP as depth falls (first trip at d = 2),
    /// the limit-agreement envelope breaks, and the shoal shift loses its monotonicity;</item>
    /// <item>flipping the lerp endpoints in <c>BandSpeed</c>: 3 of 11 fail — the BIT-exact scale-0
    /// contract catches 0.0896080 in place of 0.0900000 (a 0.4% error the anchor makes nearly
    /// invisible — exactly why the contract is exact equality, not a tolerance), plus the
    /// full-dispersion pin and the ordering test;</item>
    /// <item>offsetting the band multiplier by +0.01 inside <c>BandSpeed</c>: EXACTLY 1 of 11 fails —
    /// the full-dispersion pin, 0.10454 vs 0.08961 (17% off).</item>
    /// </list>
    /// </summary>
    public class WaterDispersionTests
    {
        // The three legacy hand-set speeds (native units) and the default band wavelengths
        // (λ = 1/scale of the shipped octave scales 0.7 / 0.16 / 0.025).
        const float LegacyChopSpeed = 0.09f;
        const float LegacyCrossSpeed = 0.025f;
        const float LegacySwellRate = 0.018f;
        const float ChopLambda = 1f / 0.7f;
        const float CrossLambda = 1f / 0.16f;
        const float SwellLambda = 1f / 0.025f;

        // ===== #4 — the band frequency factor ============================================

        [Test]
        public void FrequencyFactor_ResponseZero_IsExactlyOne()
        {
            foreach (float chop in new[] { 0f, 0.3f, 1f })
                Assert.AreEqual(1f, WaterDispersion.BandFrequencyFactor(chop, 0f),
                    "response 0 must be EXACTLY 1 — the fixed-wavelength passthrough.");
        }

        [Test]
        public void FrequencyFactor_WavelengthGrowsLinearlyInChop()
        {
            // λ_eff = λ / factor = λ·(1 + response·chop) — the growth law, pinned.
            const float response = 1.5f;
            foreach (float chop in new[] { 0f, 0.25f, 0.5f, 1f })
                Assert.AreEqual(1f + response * chop,
                    1f / WaterDispersion.BandFrequencyFactor(chop, response), 1e-5f);
            // and monotone: rougher sea, longer waves (smaller frequency factor).
            Assert.Less(WaterDispersion.BandFrequencyFactor(0.8f, response),
                        WaterDispersion.BandFrequencyFactor(0.2f, response));
        }

        // ===== #9 — the dispersion relation ==============================================

        [Test]
        public void PhaseSpeed_MonotoneIncreasingInWavelength()
        {
            foreach (float depth in new[] { 0.5f, 2f, 100f })
            {
                float prev = 0f;
                for (float lambda = 0.5f; lambda <= 80f; lambda *= 1.3f)
                {
                    float c = WaterDispersion.PhaseSpeed(lambda, depth);
                    Assert.Greater(c, prev,
                        $"long waves must outrun short ones (λ {lambda:F2}, d {depth}).");
                    prev = c;
                }
            }
        }

        [Test]
        public void PhaseSpeed_ShallowSlowsTowardZeroDepth()
        {
            const float lambda = 10f;
            float prev = float.MaxValue;
            foreach (float depth in new[] { 5f, 2f, 1f, 0.5f, 0.2f, 0.05f, 0.01f })
            {
                float c = WaterDispersion.PhaseSpeed(lambda, depth);
                Assert.Less(c, prev, $"the shallow branch must slow as depth falls (d {depth}).");
                prev = c;
            }
            Assert.AreEqual(0f, WaterDispersion.PhaseSpeed(lambda, 0f),
                "zero depth is zero speed — the depth-limited form's limit.");
        }

        [Test]
        public void DeepAndShallowForms_AgreeAtTheirLimits()
        {
            // Shallow limit (k·d small): c → √(g·d). At λ = 40 m, d = 1 m (k·d ≈ 0.157) the full
            // formula must sit within 0.5% of the depth-limited form.
            float shallow = WaterDispersion.PhaseSpeed(40f, 1f);
            float gd = Mathf.Sqrt(WaterDispersion.Gravity * 1f);
            Assert.Less(Mathf.Abs(shallow - gd) / gd, 0.005f,
                $"√(g·d) limit broken: full {shallow:F4} vs √(g·d) {gd:F4}.");

            // Deep limit (k·d large): c → √(gλ/2π). At λ = 8 m, d = 8 m (k·d ≈ 6.3) within 0.1%.
            float deep = WaterDispersion.PhaseSpeed(8f, 8f);
            float cDeep = WaterDispersion.DeepPhaseSpeed(8f);
            Assert.Less(Mathf.Abs(deep - cDeep) / cDeep, 0.001f,
                $"deep-water limit broken: full {deep:F4} vs deep {cDeep:F4}.");

            // One continuous formula — between the limits the curve must hug the smooth tanh
            // envelope: never ABOVE min(√(g·d), c_deep) (tanh(x) ≤ min(x, 1)), and never more than
            // 15% BELOW it (tanh's maximum sag vs that min-envelope is ~13%, at k·d ≈ 1 — the
            // transition itself). A per-step Δc bound is deliberately NOT used: √(g·d) has unbounded
            // slope at d → 0, so a legitimate depth-limited curve fails any fixed step bound there.
            float cDeep12 = WaterDispersion.DeepPhaseSpeed(12f);
            float prev = 0f;
            for (float d = 0.05f; d <= 24f; d += 0.05f)
            {
                float c = WaterDispersion.PhaseSpeed(12f, d);
                float envelope = Mathf.Min(Mathf.Sqrt(WaterDispersion.Gravity * d), cDeep12);
                Assert.LessOrEqual(c, envelope + 1e-4f,
                    $"above the limiting envelope at depth {d} — not the tanh relation.");
                Assert.GreaterOrEqual(c, 0.85f * envelope,
                    $"sagging below the smooth tanh transition at depth {d} — a seam or a wrong branch.");
                Assert.GreaterOrEqual(c, prev, $"speed regressed at depth {d}.");
                prev = c;
            }
        }

        [Test]
        public void GoldenValues_PinTheArithmetic()
        {
            Assert.AreEqual(3.5342f, WaterDispersion.DeepPhaseSpeed(8f), 2e-3f);
            Assert.AreEqual(3.1195f, WaterDispersion.PhaseSpeed(40f, 1f), 2e-3f);
        }

        // ===== the passthrough contract ==================================================

        [Test]
        public void DispersionScaleZero_ReproducesTheLegacySpeeds_BitForBit()
        {
            // EXACT equality, no tolerance — the handoff's contract. λ/mult arbitrary: at scale 0
            // they must not matter.
            Assert.AreEqual(LegacyChopSpeed,
                WaterDispersion.BandSpeed(LegacyChopSpeed, 0.06f, ChopLambda, 0f));
            Assert.AreEqual(LegacyCrossSpeed,
                WaterDispersion.BandSpeed(LegacyCrossSpeed, 0.06f, CrossLambda, 0f));
            Assert.AreEqual(LegacySwellRate,
                WaterDispersion.SwellPhaseRate(LegacySwellRate, 0.06f, SwellLambda, 0.025f, 0f));
            // and a negative master clamps to the same exact passthrough.
            Assert.AreEqual(LegacyChopSpeed,
                WaterDispersion.BandSpeed(LegacyChopSpeed, 0.06f, ChopLambda, -1f));
        }

        [Test]
        public void FullDispersion_LongWavesOutrunShort_TheLegacyOrderingWasBackwards()
        {
            // The legacy speeds are ANTI-dispersive (swell 0.018·world < cross < chop). At scale 1
            // with one common multiplier the ordering must invert: the 40 m band outruns the 6.25 m
            // band outruns the 1.4 m band — the stacked-layers fix.
            const float mult = 0.06f;
            float chop = WaterDispersion.BandSpeed(LegacyChopSpeed, mult, ChopLambda, 1f);
            float cross = WaterDispersion.BandSpeed(LegacyCrossSpeed, mult, CrossLambda, 1f);
            float swellWorld = WaterDispersion.SwellPhaseRate(
                LegacySwellRate, mult, SwellLambda, 0.025f, 1f) / 0.025f;   // native → world m/s
            Assert.Greater(cross, chop, "the 6.25 m band must outrun the 1.4 m band at full dispersion.");
            Assert.Greater(swellWorld, cross, "the 40 m band must outrun the 6.25 m band at full dispersion.");
        }

        [Test]
        public void DefaultMultiplier_AnchorsTheChopBand_OnItsLegacySpeed()
        {
            // The 0.06 default is not arbitrary: at full dispersion the WIND-CHOP band at its
            // default wavelength lands on its hand-set 0.09 m/s (within 0.5%), so dialing the
            // master up re-ties the other bands to physics around an unchanged fastest band.
            float derived = 0.06f * WaterDispersion.DeepPhaseSpeed(ChopLambda);
            Assert.Less(Mathf.Abs(derived - LegacyChopSpeed) / LegacyChopSpeed, 0.005f,
                $"the anchor broke: 0.06·c_deep(1/0.7) = {derived:F4} vs legacy 0.09.");
            // and BandSpeed at full dispersion must land EXACTLY on bandMult·c_deep — the pin a
            // band-multiplier offset inside BandSpeed cannot slip past.
            Assert.AreEqual(derived,
                WaterDispersion.BandSpeed(LegacyChopSpeed, 0.06f, ChopLambda, 1f), 1e-6f,
                "full dispersion must return bandMult·c_deep(λ), nothing else.");
        }

        // ===== the shoal shift ===========================================================

        [Test]
        public void ShoalShift_ZeroInDeepWater_GrowsMonotonicallyOverTheShoal()
        {
            const float lambda = 40f, bunch = 1f;
            Assert.Less(WaterDispersion.ShoalShift(lambda, lambda, bunch, 1f), 0.001f * lambda,
                "at d = λ the shift must be effectively zero (deep water).");
            float prev = -1f;
            foreach (float depth in new[] { 20f, 10f, 5f, 2f, 1f, 0.5f, 0.1f, 0f })
            {
                float s = WaterDispersion.ShoalShift(lambda, depth, bunch, 1f);
                Assert.Greater(s, prev, $"the shift must grow as the water shallows (d {depth}).");
                prev = s;
            }
            Assert.AreEqual(bunch * lambda, WaterDispersion.ShoalShift(lambda, 0f, bunch, 1f), 1e-3f,
                "at zero depth the shift reaches its bound: bunch·λ.");
        }

        [Test]
        public void ShoalShift_IsBoundedAndGated()
        {
            foreach (float depth in new[] { 0f, 0.3f, 2f, 50f })
            {
                float s = WaterDispersion.ShoalShift(12f, depth, 2f, 1f);
                Assert.GreaterOrEqual(s, 0f);
                Assert.LessOrEqual(s, 2f * 12f + 1e-4f, "the shift must never exceed bunch·λ.");
            }
            Assert.AreEqual(0f, WaterDispersion.ShoalShift(12f, 0.5f, 0f, 1f), "zero bunch = no shift.");
            Assert.AreEqual(0f, WaterDispersion.ShoalShift(12f, 0.5f, 2f, 0f),
                "master 0 = no shift — the passthrough gate.");
        }
    }
}
