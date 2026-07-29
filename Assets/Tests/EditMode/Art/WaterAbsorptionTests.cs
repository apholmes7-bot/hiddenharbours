using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Art;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// Headless twin tests for the ADR 0027 #7 law — <see cref="WaterAbsorption"/>, the C# mirror of
    /// the shader's <c>AbsorptionSigma</c> / <c>AbsorptionTransmission</c> / <c>AbsorptionBand</c> /
    /// the composite lerp (change one, change BOTH in the same PR). Contracts pinned:
    ///
    /// <list type="bullet">
    /// <item><b>the passthrough contract:</b> turbidity 0 ⇒ σ EXACTLY zero ⇒ <c>IsActive</c> false
    /// (the shader's whole block is skipped, so the render is byte-identical to today);</item>
    /// <item>transmission is monotone DECREASING in depth on every channel, and exactly 1 at the
    /// waterline (zero water ⇒ you see the ground);</item>
    /// <item><b>per-channel ordering:</b> red extinguishes before green before blue at every depth
    /// beyond the waterline — the depth-colour shift the ADR gets "for free";</item>
    /// <item><b>the 2× path is applied:</b> transmission at depth d equals a one-way transmission
    /// over 2d — pinned against an independently computed <c>exp</c>, not against the twin;</item>
    /// <item>banding quantizes onto exactly <c>bands</c> steps and stays monotone; 0 bands is the
    /// smooth value returned unchanged;</item>
    /// <item>the composite never touches the WATER's own colour where the bottom is absent
    /// (coverage 0) or fully absorbed (T → 0) — absorption applies to the TRANSMITTED SEABED only.</item>
    /// </list>
    ///
    /// Sabotage MEASURED (2026-07-29, headless runs over BOTH absorption fixtures, 22 tests — see
    /// the PR), two arms, each targeted:
    /// <list type="number">
    /// <item>dropping the down-and-back path (<c>DownAndBack</c> 2 → 1, i.e. absorbing over d
    /// instead of 2d): <b>3 of 22 fail</b> —
    /// <c>Transmission_AppliesTheDownAndBackPath</c> (0.7788008 in place of 0.6065307 at σ = 0.5,
    /// d = 0.5 — a 28% over-bright bottom), <c>Transmission_MonotoneDecreasingInDepth</c> (red not
    /// gone by 16 m: 2.76e-6 against the 1e-6 pin) and
    /// <c>Composite_DeepWaterReturnsTheWaterColour_ShallowReturnsTheBottom</c> (0.5000462 against
    /// the water's own 0.5 — the BLUE channel still leaking bottom at 80 m). The ordering, banding
    /// and passthrough arms all stay green: halving the path is invisible to every property except
    /// how FAR the bottom reaches, which is why that gets three independent pins;</item>
    /// <item>swapping the red and blue channels inside <see cref="WaterAbsorption.Sigma"/>:
    /// <b>3 of 22 fail</b> — <c>Sigma_ScalesTheRatioByTurbidity</c> (0.16 where 2.0 was expected),
    /// <c>Transmission_RedExtinguishesBeforeBlue</c> (red transmitting 0.9873 against green's
    /// 0.9716 at d = 0.1 — the sea would go RED with depth) and
    /// <c>Transmission_MonotoneDecreasingInDepth</c> (red at 0.1290 after 16 m instead of gone).
    /// The path, banding, composite and passthrough arms stay green.</item>
    /// </list>
    /// </summary>
    public class WaterAbsorptionTests
    {
        private static readonly Vector3 Ratio = WaterAbsorption.DefaultRatio;   // (1, 0.18, 0.08)

        // ===== the passthrough contract ===================================================

        [Test]
        public void Sigma_ZeroTurbidity_IsExactlyZeroAndInactive()
        {
            Vector3 sigma = WaterAbsorption.Sigma(0f, Ratio);
            Assert.AreEqual(0f, sigma.x, "turbidity 0 must be EXACTLY 0 — the passthrough gate.");
            Assert.AreEqual(0f, sigma.y);
            Assert.AreEqual(0f, sigma.z);
            Assert.IsFalse(WaterAbsorption.IsActive(sigma),
                "σ = 0 must skip the shader block entirely (byte-identical to today's look).");
        }

        [Test]
        public void Sigma_NegativeTurbidityOrRatio_ClampsToZero()
        {
            // σ < 0 would AMPLIFY the bottom with depth — physically backwards and visually a
            // blow-out. Both factors are clamped, so a mis-typed preset can only turn it off.
            Assert.IsFalse(WaterAbsorption.IsActive(WaterAbsorption.Sigma(-2f, Ratio)));
            Assert.IsFalse(WaterAbsorption.IsActive(
                WaterAbsorption.Sigma(1f, new Vector3(-1f, -1f, -1f))));
        }

        [Test]
        public void Sigma_ScalesTheRatioByTurbidity()
        {
            Vector3 sigma = WaterAbsorption.Sigma(2f, Ratio);
            Assert.AreEqual(2f * Ratio.x, sigma.x, 1e-6f);
            Assert.AreEqual(2f * Ratio.y, sigma.y, 1e-6f);
            Assert.AreEqual(2f * Ratio.z, sigma.z, 1e-6f);
            Assert.IsTrue(WaterAbsorption.IsActive(sigma));
        }

        [Test]
        public void Transmission_ZeroSigma_IsOneEverywhere()
        {
            // The degenerate case the gate above exists to intercept: with no extinction the water
            // transmits perfectly at every depth. Pinned so the gate can never be "fixed" into a
            // partial fade that quietly changes the shipped look.
            foreach (float d in new[] { 0f, 1f, 10f, 100f })
            {
                Vector3 t = WaterAbsorption.Transmission(Vector3.zero, d);
                Assert.AreEqual(1f, t.x); Assert.AreEqual(1f, t.y); Assert.AreEqual(1f, t.z);
            }
        }

        // ===== the Beer-Lambert law ========================================================

        [Test]
        public void Transmission_IsOneAtTheWaterline()
        {
            Vector3 t = WaterAbsorption.Transmission(WaterAbsorption.Sigma(3f, Ratio), 0f);
            Assert.AreEqual(1f, t.x, 1e-6f, "zero water ⇒ the ground shows — this is the effect §17.1 faked.");
            Assert.AreEqual(1f, t.y, 1e-6f);
            Assert.AreEqual(1f, t.z, 1e-6f);
        }

        [Test]
        public void Transmission_MonotoneDecreasingInDepth()
        {
            Vector3 sigma = WaterAbsorption.Sigma(0.8f, Ratio);
            Vector3 prev = WaterAbsorption.Transmission(sigma, 0f);
            foreach (float d in new[] { 0.25f, 0.5f, 1f, 2f, 4f, 8f, 16f })
            {
                Vector3 t = WaterAbsorption.Transmission(sigma, d);
                Assert.Less(t.x, prev.x, $"red must keep dying at d = {d}");
                Assert.Less(t.y, prev.y, $"green must keep dying at d = {d}");
                Assert.Less(t.z, prev.z, $"blue must keep dying at d = {d}");
                prev = t;
            }
            Assert.Less(prev.x, 1e-6f, "red must be gone by 16 m.");
        }

        [Test]
        public void Transmission_NegativeDepthClampsToTheWaterline()
        {
            // The shader feeds the COSMETIC organic-fringe depth, which dips slightly negative
            // inside the shore band. It must read as "no water", never as amplification.
            Vector3 sigma = WaterAbsorption.Sigma(1f, Ratio);
            Vector3 t = WaterAbsorption.Transmission(sigma, -0.4f);
            Assert.AreEqual(1f, t.x, 1e-6f);
        }

        [Test]
        public void Transmission_RedExtinguishesBeforeBlue()
        {
            // The whole point of going per-channel: the bottom shifts toward blue-green as it
            // deepens instead of merely dimming. Ordering holds at EVERY depth past the waterline.
            Vector3 sigma = WaterAbsorption.Sigma(0.8f, Ratio);
            foreach (float d in new[] { 0.1f, 0.5f, 1f, 3f, 6f })
            {
                Vector3 t = WaterAbsorption.Transmission(sigma, d);
                Assert.Less(t.x, t.y, $"red must transmit less than green at d = {d}");
                Assert.Less(t.y, t.z, $"green must transmit less than blue at d = {d}");
            }
        }

        [Test]
        public void Transmission_AppliesTheDownAndBackPath()
        {
            // Light DESCENDS the column and RETURNS: the path is 2d. Pinned against an
            // independently computed exp (not against the twin), so halving the path shows up here
            // and nowhere else — see the sabotage note in the class doc.
            const float sigmaR = 0.5f;
            const float depth = 0.5f;
            Vector3 t = WaterAbsorption.Transmission(new Vector3(sigmaR, 0f, 0f), depth);
            Assert.AreEqual(Mathf.Exp(-sigmaR * 2f * depth), t.x, 1e-6f);
            // and the equivalence that names the law: T(d) == a one-way transmission over 2d.
            Vector3 oneWayOverTwice =
                WaterAbsorption.Transmission(new Vector3(sigmaR * 0.5f, 0f, 0f), 2f * depth);
            Assert.AreEqual(oneWayOverTwice.x, t.x, 1e-6f);
            Assert.AreEqual(2f, WaterAbsorption.DownAndBack);
        }

        // ===== banding (the pixel-art quantization; DEFAULT ON) =============================

        [Test]
        public void BandTransmission_ZeroBands_IsTheSmoothValue()
        {
            Vector3 t = WaterAbsorption.Transmission(WaterAbsorption.Sigma(0.6f, Ratio), 1.3f);
            Vector3 banded = WaterAbsorption.BandTransmission(t, 0f);
            Assert.AreEqual(t.x, banded.x); Assert.AreEqual(t.y, banded.y); Assert.AreEqual(t.z, banded.z);
        }

        [Test]
        public void BandTransmission_QuantizesOntoExactlyNSteps()
        {
            const float bands = 6f;
            Vector3 sigma = WaterAbsorption.Sigma(0.7f, Ratio);
            for (float d = 0f; d <= 8f; d += 0.05f)
            {
                Vector3 b = WaterAbsorption.BandTransmission(
                    WaterAbsorption.Transmission(sigma, d), bands);
                foreach (float v in new[] { b.x, b.y, b.z })
                {
                    float steps = v * bands;
                    Assert.AreEqual(Mathf.Round(steps), steps, 1e-4f,
                        $"banded transmission {v} is not on a 1/{bands} step at d = {d}");
                    Assert.GreaterOrEqual(v, 0f);
                    Assert.LessOrEqual(v, 1f);
                }
            }
        }

        [Test]
        public void BandTransmission_StaysMonotoneAndActuallyBands()
        {
            const float bands = 6f;
            Vector3 sigma = WaterAbsorption.Sigma(0.7f, Ratio);
            float prev = 1f;
            var distinct = new System.Collections.Generic.HashSet<float>();
            for (float d = 0f; d <= 8f; d += 0.02f)
            {
                float v = WaterAbsorption.BandTransmission(
                    WaterAbsorption.Transmission(sigma, d), bands).x;
                Assert.LessOrEqual(v, prev + 1e-6f, $"banding must not un-darken at d = {d}");
                prev = v;
                distinct.Add(Mathf.Round(v * bands));
            }
            // A posterized ramp, not a smooth one and not a single flat step.
            Assert.GreaterOrEqual(distinct.Count, 3, "banding produced too few distinct steps.");
            Assert.LessOrEqual(distinct.Count, (int)bands + 1, "banding produced more steps than bands.");
        }

        // ===== the composite (absorption applies to the BOTTOM, never the water) ============

        [Test]
        public void Composite_NoCoverage_LeavesTheWaterColourUntouched()
        {
            // Where the bake found no bottom (the Deep / Channel types clear their tile), the sea is
            // unchanged BY CONSTRUCTION — this is what keeps open water out of the feature entirely.
            var water = new Vector3(0.1f, 0.3f, 0.5f);
            Vector3 outp = WaterAbsorption.Composite(water, new Vector3(0.9f, 0.8f, 0.6f),
                                                     Vector3.one, 0f);
            Assert.AreEqual(water.x, outp.x, 1e-6f);
            Assert.AreEqual(water.y, outp.y, 1e-6f);
            Assert.AreEqual(water.z, outp.z, 1e-6f);
        }

        [Test]
        public void Composite_DeepWaterReturnsTheWaterColour_ShallowReturnsTheBottom()
        {
            var water = new Vector3(0.1f, 0.3f, 0.5f);
            var bed = new Vector3(0.9f, 0.8f, 0.6f);
            Vector3 sigma = WaterAbsorption.Sigma(1.2f, Ratio);

            // At the waterline the bottom wins outright...
            Vector3 shallow = WaterAbsorption.Composite(
                water, bed, WaterAbsorption.Transmission(sigma, 0f), 1f);
            Assert.AreEqual(bed.x, shallow.x, 1e-6f);

            // ...and deep the painted LUT's colour is ALL that is left: the water body's own colour
            // is never absorbed, only the transmitted bottom is (ADR 0027 finding 1). 80 m is chosen
            // against the BLUE channel — the longest-lived one (σ_b = 1.2 × 0.08), which is the whole
            // point of going per-channel.
            Vector3 deep = WaterAbsorption.Composite(
                water, bed, WaterAbsorption.Transmission(sigma, 80f), 1f);
            Assert.AreEqual(water.x, deep.x, 1e-5f);
            Assert.AreEqual(water.y, deep.y, 1e-5f);
            Assert.AreEqual(water.z, deep.z, 1e-5f);
        }
    }
}
