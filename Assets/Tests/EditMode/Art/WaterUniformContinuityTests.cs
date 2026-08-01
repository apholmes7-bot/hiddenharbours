using System.Reflection;
using HiddenHarbours.Art;
using HiddenHarbours.Core;
using NUnit.Framework;
using UnityEngine;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// The PARAMETER-CONTINUITY guard for the water push (the owner's 2026-08-01 judge pass: "the water
    /// is choppy when transitioning between states, it gets stuttery … the boat physics still feel jerky
    /// and not smooth at times").
    ///
    /// <para><b>The defect these pin.</b> <c>WaterSurface</c> sampled the sim on a throttled tick and
    /// pushed several of the sampled values RAW, so every state change reached the shader as a staircase
    /// of ~125 ms steps. Two of the stepped values are spatial FREQUENCIES — <c>_Chop</c> (through
    /// <c>_BandScaleResponse</c>, which scales every band's wavelength and, via the dispersion relation,
    /// its scroll speed) and <c>_OceanSwellScale</c> (which the displaced vertex stage turns into
    /// <c>waveFreqScale</c>) — and a step in a spatial frequency translates the whole pattern by an
    /// amount proportional to distance from the world origin. At St Peters' ±380 m that is tens of
    /// radians of phase per step, on the surface AND, because <c>DisplacedSea</c> re-broadcasts the
    /// stepped freq scale to <c>BoatWaveMotion</c>, under the hull.</para>
    ///
    /// <para><b>The fix these pin.</b> Sampling stays throttled (the tide and the weather are slow);
    /// EASING and PUSHING moved to every frame. So the properties to guard are the two that make an
    /// exponential ease the right instrument: a <b>bounded per-frame step</b> (there is no staircase to
    /// see) and a <b>bounded steady-state lag</b> (the drawn value never drifts away from the sampled
    /// one — SEE==FEEL survives in substance).</para>
    ///
    /// <para>All pure math, headless, deterministic — no GPU, no sim, no scene beyond one component
    /// whose serialized defaults are read to prove the SHIPPED dials satisfy the bounds.</para>
    /// </summary>
    public class WaterUniformContinuityTests
    {
        private const float Dt60 = 1f / 60f;

        /// <summary>The old push cadence — the staircase step width these tests exist to have removed.</summary>
        private const float SampleDt = 1f / 8f;

        /// <summary>
        /// A DELIBERATE CONSERVATIVE CEILING on the peak vertical tide rate — not the live number, and
        /// kept at 3.5 cm/s on purpose.
        ///
        /// <para>It was the live number when written: TideAmplitude 3.5 m over a 12.4206 h period on a
        /// 1200 s day gives <c>A·2π/T/SecondsPerHour</c> ≈ 3.5 cm/s. The owner's 2026-08-01 tide-pacing
        /// ruling (amplitude → 2.2 m, day → 1800 s) cut the real peak to <b>≈ 1.48 cm/s</b>, so this now
        /// sits 2.4× above the fastest the sea actually moves.</para>
        ///
        /// <para><b>Why not re-derive it.</b> Every bound below is <c>rate × τ</c>, so a HIGHER rate makes
        /// them all STRICTER: at 3.5 cm/s the shipped τ = 0.5 s must keep the trail under 2 cm, which caps
        /// <c>_waterLevelResponseTime</c> at ~0.57 s; at the real 1.48 cm/s the same bound would permit
        /// ~1.35 s. Re-deriving would therefore LOOSEN the guard by 2.4× in exchange for a prettier
        /// number. The headroom also means the guard survives the amplitude being raised again — it stays
        /// a true worst case for any amplitude up to ~5.2 m at the shipped day length.</para>
        ///
        /// <para>⚠ It is a ceiling, so it must never be lowered to track a smaller tide. If the amplitude
        /// ever goes ABOVE ~5.2 m, this stops being conservative and has to be re-derived.
        /// (<c>TidePacingInvariantTests</c> owns the live rate and pins it under 1.6 cm/s.)</para>
        /// </summary>
        private const float TidePeakRate = 0.035f;

        /// <summary>
        /// One code of the 8-bit seabed height texture over its −4…+6 m range: 3.92 cm. The natural
        /// unit for "does the drawn waterline disagree with the sampled one" — a lag under one code
        /// cannot even be represented in the depth the shore band reads.
        /// </summary>
        private const float SeabedHeightCode = 10f / 255f;

        // ==== (1) the ease is bounded per step — the staircase is structurally gone =================

        [Test]
        public void SteppedTarget_MovesOnlyABoundedFractionPerFrame_NotTheWholeGapAtOnce()
        {
            // The distilled defect: a raw sample-and-hold push moved 100% of the gap in ONE step, eight
            // times a second. The ease can move at most (1 − exp(−dt/τ)) of the remaining gap.
            const float tau = 0.5f;
            float alpha = 1f - Mathf.Exp(-Dt60 / tau);
            Assert.Less(alpha, 0.05f,
                "sanity: at 60 fps and τ = 0.5 s a single step is a few percent of the gap");

            float value = 0f;
            const float target = 1f;      // the sharpest possible step: 0 → 1 in one sample
            float maxStep = 0f;
            for (int i = 0; i < 600; i++)
            {
                float next = WaterSurface.SmoothScalarToward(value, target, tau, Dt60);
                maxStep = Mathf.Max(maxStep, Mathf.Abs(next - value));
                value = next;
            }

            Assert.LessOrEqual(maxStep, alpha * Mathf.Abs(target) + 1e-6f,
                "no single frame may move more than the bound τ and dt imply");
            Assert.Less(maxStep, 0.05f,
                "a full-range state change must arrive as a ramp, not as a visible step");
        }

        [Test]
        public void SteppedTarget_ConvergesToIt()
        {
            // Bounded steps are only half the contract: the value must actually ARRIVE, or the sea
            // would draw a state the sim is not in.
            const float tau = 0.5f;
            float value = 0f;
            for (int i = 0; i < 600; i++)   // 10 s = 20 τ
                value = WaterSurface.SmoothScalarToward(value, 1f, tau, Dt60);

            Assert.AreEqual(1f, value, 1e-4f, "the eased value must converge on its target");
        }

        [Test]
        public void TheEase_IsFrameRateIndependent()
        {
            // A 30 fps machine and a 144 fps machine must reach the same place at the same wall-clock
            // moment — the (1 − exp) factors compose exactly under sub-stepping.
            const float tau = 2f;
            float coarse = 0f, fine = 0f;
            for (int i = 0; i < 60; i++)  coarse = WaterSurface.SmoothScalarToward(coarse, 1f, tau, 1f / 30f);
            for (int i = 0; i < 288; i++) fine   = WaterSurface.SmoothScalarToward(fine,   1f, tau, 1f / 144f);

            Assert.AreEqual(coarse, fine, 1e-4f,
                "two frame rates covering the same 2 s must land on the same eased value");
        }

        [Test]
        public void ZeroResponseTime_Snaps_SoTheEasingCanBeDialledOut()
        {
            // τ = 0 is the pre-fix behaviour, kept reachable from the Inspector without a code change.
            Assert.AreEqual(1f, WaterSurface.SmoothScalarToward(0f, 1f, 0f, Dt60),
                "τ ≤ 0 must snap — the escape hatch back to the old push");
        }

        // ==== (2) an 8 Hz staircase in, a continuous ramp out ======================================

        [Test]
        public void AnEightHertzSampledStaircase_IsSmoothedIntoAContinuousRamp()
        {
            // The real shape of the input: the sim is SAMPLED at 8 Hz, so the target the ease chases is
            // itself a staircase. Feeding that staircase through the per-frame ease must leave no step
            // anywhere near the staircase's own height.
            const float tau = 2f;              // the shipped _Chop response
            const float rampPerSecond = 0.5f;  // a brisk sea-state transition: glass → half a gale in 2 s

            float sampledTarget = 0f;
            float eased = 0f;
            float sinceSample = 0f;
            float maxOutputStep = 0f;
            float maxInputStep = 0f;
            float previousTarget = 0f;

            for (int frame = 0; frame < 600; frame++)   // 10 s at 60 fps
            {
                sinceSample += Dt60;
                if (sinceSample >= SampleDt)            // the throttled sim read
                {
                    sinceSample -= SampleDt;
                    sampledTarget = Mathf.Min(1f, sampledTarget + rampPerSecond * SampleDt);
                    maxInputStep = Mathf.Max(maxInputStep, Mathf.Abs(sampledTarget - previousTarget));
                    previousTarget = sampledTarget;
                }

                float next = WaterSurface.SmoothScalarToward(eased, sampledTarget, tau, Dt60);
                maxOutputStep = Mathf.Max(maxOutputStep, Mathf.Abs(next - eased));
                eased = next;
            }

            Assert.Greater(maxInputStep, 0.05f,
                "sanity: the 8 Hz sampled target really does arrive in visible steps");
            Assert.Less(maxOutputStep, maxInputStep * 0.2f,
                "the per-frame ease must break the sampled staircase into steps far smaller than " +
                "its own — that difference IS the removed stutter");
            Assert.AreEqual(sampledTarget, eased, 0.05f,
                "and it must still be tracking the sea it was handed, not lagging behind the ramp");
        }

        // ==== (3) _WaterLevel: smoothed WITHOUT drifting off the gameplay waterline ================

        [Test]
        public void WaterLevel_AtPeakTidalRate_TrailsBySignificantlyUnderOneSeabedHeightCode()
        {
            // The SEE==FEEL argument for easing the drawn water level at all. An exponential ease
            // trailing a target that ramps at a constant rate settles at exactly rate × τ. Against the
            // 3.5 cm/s CEILING (see TidePeakRate — the live tide runs at 1.48 cm/s since the 2026-08-01
            // pacing ruling) the shipped τ = 0.5 s trails the sampled level by 1.75 cm — under one code
            // of the 8-bit seabed height map, i.e. below the resolution the shore band can represent. At
            // the live rate it is 0.74 cm. The gameplay waterline is untouched either way; this bounds
            // how far the DRAWN one may sit from it.
            float tau = ShippedFloatField("_waterLevelResponseTime");
            float level = 0f, target = 0f;

            for (int i = 0; i < 1800; i++)   // 30 s of a running tide — well past steady state
            {
                target += TidePeakRate * Dt60;
                level = WaterSurface.SmoothScalarToward(level, target, tau, Dt60);
            }

            float lag = target - level;
            Assert.AreEqual(TidePeakRate * tau, lag, 1e-3f,
                "the steady-state trail behind a constant ramp is exactly rate × τ");
            Assert.Less(lag, 0.02f,
                "the drawn waterline must trail the sampled tide by under 2 cm at the peak spring rate");
            Assert.Less(lag, SeabedHeightCode,
                "…which is under ONE code of the 8-bit seabed height map — a disagreement the shore " +
                "band could not represent even if it wanted to");
        }

        [Test]
        public void WaterLevel_WhenTheTideStops_TheDrawnLevelCatchesUpExactly()
        {
            // The lag is a TRAIL, not an offset: the moment the tide slacks, the drawn level lands on it.
            float tau = ShippedFloatField("_waterLevelResponseTime");
            float level = 0f;
            const float slackLevel = 1.4f;
            for (int i = 0; i < 600; i++)
                level = WaterSurface.SmoothScalarToward(level, slackLevel, tau, Dt60);

            Assert.AreEqual(slackLevel, level, 1e-4f,
                "at slack water the drawn level must equal the sampled one — no residual offset");
        }

        // ==== (4) the SHIPPED dials, not just the math =============================================

        [Test]
        public void TheShippedResponseTimes_TurnTheEasingOn_AndKeepTheLevelLagInsideItsBound()
        {
            // The math above is only worth anything at the values that actually ship. A future edit
            // that zeroes the response times (re-introducing the staircase) or inflates the water-level
            // one (letting the drawn waterline drift off the gameplay one) breaks here.
            float chopTau = ShippedFloatField("_chopResponseTime");
            float levelTau = ShippedFloatField("_waterLevelResponseTime");

            Assert.Greater(chopTau, 0f,
                "_Chop must ship EASED — it scales every band's spatial frequency, so a stepped _Chop " +
                "slides the whole pattern by a distance proportional to |worldPos|");
            Assert.Greater(levelTau, 0f, "_WaterLevel must ship EASED — the flats turn mm of level " +
                "into decimetres of horizontal travel, so a stepped level marches the waterline in jerks");

            Assert.LessOrEqual(TidePeakRate * levelTau, 0.02f,
                "the shipped _WaterLevel response must keep the peak-rate trail under 2 cm; raising it " +
                "beyond ~0.57 s breaks the SEE==FEEL argument this smoothing rests on");

            // A single 60 fps frame must not be able to move either value by a visible fraction.
            Assert.Less(1f - Mathf.Exp(-Dt60 / chopTau), 0.05f, "_Chop steps must stay small per frame");
            Assert.Less(1f - Mathf.Exp(-Dt60 / levelTau), 0.05f, "_WaterLevel steps must stay small per frame");
        }

        /// <summary>
        /// Read a serialized private float off a live <see cref="WaterSurface"/> — its SHIPPED default.
        /// Bound by name deliberately: if the field is renamed or removed this fails loudly rather than
        /// silently testing a constant that no longer drives anything.
        /// </summary>
        private static float ShippedFloatField(string field)
        {
            GameServices.Reset();   // the edit-mode path (no sim), like a fresh EditMode scene
            var go = new GameObject("WaterUniformContinuityTests.Water", typeof(MeshRenderer));
            try
            {
                var surface = go.AddComponent<WaterSurface>();
                FieldInfo f = typeof(WaterSurface).GetField(field,
                    BindingFlags.NonPublic | BindingFlags.Instance);
                Assert.IsNotNull(f, $"WaterSurface.{field} not found — the continuity dials moved; " +
                    "update this test with them (and re-check the lag bound).");
                return (float)f.GetValue(surface);
            }
            finally
            {
                Object.DestroyImmediate(go);
                GameServices.Reset();
            }
        }
    }
}
