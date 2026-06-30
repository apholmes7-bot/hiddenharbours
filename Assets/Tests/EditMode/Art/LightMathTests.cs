using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Art;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// Determinism + correctness guard for the pure additive-light maths (ADR 0016, CLAUDE.md rule 5). These run
    /// headless — no scene, no GPU — and pin the NIGHT-GATE ramp (invisible day → full night, monotonic), the
    /// CONE/RADIAL falloff + angle math, and the deterministic FLICKER. The shader mirrors these exact functions
    /// and the components are thin shells over them, so guarding the maths guards the system. Mirrors
    /// <see cref="DayNightMathTests"/>'s style.
    /// </summary>
    public class LightMathTests
    {
        private const float Eps = 1e-3f;

        private static Color Gray(float v) => new Color(v, v, v, 1f);

        // ---- NightGate -----------------------------------------------------------------------------------

        [Test]
        public void NightGate_IsOffInBrightDay_FullInDarkNight()
        {
            // Bright noon tint (white-ish) → frame is bright → gate ~0 (the light can't wash daytime out).
            float day = LightMath.NightGate(LightMath.Luminance(Gray(1f)), 0.12f, 0.35f);
            Assert.AreEqual(0f, day, Eps, "bright day -> light off");

            // Deep-dark night tint → frame is dark → gate ~1 (the light cuts through).
            float night = LightMath.NightGate(LightMath.Luminance(Gray(0.05f)), 0.12f, 0.35f);
            Assert.AreEqual(1f, night, 0.05f, "dark night -> light full");
        }

        [Test]
        public void NightGate_IsMonotonicInDarkness()
        {
            // As the frame gets DARKER (luminance falls), the gate must never decrease.
            float prev = -1f;
            for (float lum = 1f; lum >= 0f; lum -= 0.05f)
            {
                float g = LightMath.NightGate(lum, 0.12f, 0.35f);
                Assert.GreaterOrEqual(g + Eps, prev, $"gate dropped as it got darker at lum={lum}");
                prev = g;
                Assert.GreaterOrEqual(g, 0f); Assert.LessOrEqual(g, 1f);
            }
        }

        [Test]
        public void NightGate_ThresholdGatesDaytimeHard()
        {
            // Just below the darkness threshold the light stays off; comfortably above it climbs.
            float belowThreshold = LightMath.NightGate(LightMath.Luminance(Gray(0.95f)), 0.2f, 0.3f); // darkness ~0.05
            Assert.AreEqual(0f, belowThreshold, Eps);
            float aboveThreshold = LightMath.NightGate(LightMath.Luminance(Gray(0.3f)), 0.2f, 0.3f);   // darkness ~0.7
            Assert.Greater(aboveThreshold, 0.5f);
        }

        [Test]
        public void NightGateWithFallback_ShowsWhenNoCycle()
        {
            // No cycle (tint unset/black) → return the fallback (default 1 = show), regardless of luminance, so
            // the demo + edit-mode preview render. With a cycle, it's the normal gate.
            float noCycle = LightMath.NightGateWithFallback(0f, 0.12f, 0.35f, cycleActive: false, fallbackWhenNoCycle: 1f);
            Assert.AreEqual(1f, noCycle, Eps, "no cycle -> show (fallback)");

            float noCycleHidden = LightMath.NightGateWithFallback(0f, 0.12f, 0.35f, cycleActive: false, fallbackWhenNoCycle: 0f);
            Assert.AreEqual(0f, noCycleHidden, Eps, "no cycle, fallback 0 -> hidden");

            float withCycleDay = LightMath.NightGateWithFallback(LightMath.Luminance(Gray(1f)), 0.12f, 0.35f, cycleActive: true, 1f);
            Assert.AreEqual(0f, withCycleDay, Eps, "cycle on, bright -> off");
        }

        // ---- RadialFalloff -------------------------------------------------------------------------------

        [Test]
        public void RadialFalloff_IsOneAtCentreZeroAtEdge()
        {
            Assert.AreEqual(1f, LightMath.RadialFalloff(0f, 0.6f), Eps, "centre = full");
            Assert.AreEqual(0f, LightMath.RadialFalloff(1f, 0.6f), Eps, "edge = none");
            Assert.AreEqual(0f, LightMath.RadialFalloff(1.5f, 0.6f), Eps, "beyond edge = none (clamped)");
        }

        [Test]
        public void RadialFalloff_IsMonotonicallyDecreasing()
        {
            float prev = 2f;
            for (float d = 0f; d <= 1f; d += 0.05f)
            {
                float v = LightMath.RadialFalloff(d, 0.6f);
                Assert.LessOrEqual(v, prev + Eps, $"radial increased with distance at d={d}");
                prev = v;
            }
        }

        // ---- ConeFalloff ---------------------------------------------------------------------------------

        [Test]
        public void ConeFalloff_FullOnAxis_ZeroOutsideCone()
        {
            Assert.AreEqual(1f, LightMath.ConeFalloff(0f, 30f, 0.4f), Eps, "on axis = full");
            Assert.AreEqual(0f, LightMath.ConeFalloff(45f, 30f, 0.4f), Eps, "beyond the half-angle = none");
        }

        [Test]
        public void ConeFalloff_HalfAngle180IsFullRadial()
        {
            // A 180-degree (or wider) half-angle is a round glow: lit at any angle.
            Assert.AreEqual(1f, LightMath.ConeFalloff(0f, 180f, 0.4f), Eps);
            Assert.AreEqual(1f, LightMath.ConeFalloff(170f, 180f, 0.4f), Eps);
            Assert.AreEqual(1f, LightMath.ConeFalloff(90f, 200f, 0.4f), Eps);
        }

        [Test]
        public void ConeFalloff_IsNonIncreasingOffAxis()
        {
            float prev = 2f;
            for (float a = 0f; a <= 40f; a += 2f)
            {
                float v = LightMath.ConeFalloff(a, 30f, 0.4f);
                Assert.LessOrEqual(v, prev + Eps, $"cone increased swinging off-axis at {a} deg");
                prev = v;
                Assert.GreaterOrEqual(v, 0f); Assert.LessOrEqual(v, 1f);
            }
        }

        [Test]
        public void ConeFalloff_NarrowerConeLightsLessAtAGivenAngle()
        {
            // At 20 deg off-axis, a 40-deg cone lights it; a 15-deg cone does not.
            float wide = LightMath.ConeFalloff(20f, 40f, 0.3f);
            float narrow = LightMath.ConeFalloff(20f, 15f, 0.3f);
            Assert.Greater(wide, narrow);
            Assert.AreEqual(0f, narrow, Eps);
        }

        // ---- ShapeIntensity ------------------------------------------------------------------------------

        [Test]
        public void ShapeIntensity_IsRadialTimesCone()
        {
            float r = LightMath.RadialFalloff(0.5f, 0.6f);
            float c = LightMath.ConeFalloff(10f, 30f, 0.4f);
            float shape = LightMath.ShapeIntensity(0.5f, 0.6f, 10f, 30f, 0.4f);
            Assert.AreEqual(r * c, shape, Eps);
        }

        // ---- Flicker (deterministic) ---------------------------------------------------------------------

        [Test]
        public void Flicker_IsDeterministic_SameInputsSameOutput()
        {
            for (float t = 0f; t < 5f; t += 0.37f)
            {
                float a = LightMath.Flicker(1234, t, 0.5f, 1f);
                float b = LightMath.Flicker(1234, t, 0.5f, 1f);
                Assert.AreEqual(a, b, 0f, $"flicker not reproducible at t={t}");
            }
        }

        [Test]
        public void Flicker_ZeroAmountIsSteadyOne()
        {
            for (float t = 0f; t < 5f; t += 0.5f)
                Assert.AreEqual(1f, LightMath.Flicker(7, t, 0f, 2f), Eps, $"steady flicker should be 1 at t={t}");
        }

        [Test]
        public void Flicker_StaysWithinAmountBand()
        {
            // amount=0.5 -> the multiplier must stay within [1-0.5, 1] = [0.5, 1].
            const float amount = 0.5f;
            for (float t = 0f; t < 20f; t += 0.13f)
            {
                float v = LightMath.Flicker(99, t, amount, 1.5f);
                Assert.GreaterOrEqual(v, 1f - amount - Eps, $"flicker dipped below the band at t={t}");
                Assert.LessOrEqual(v, 1f + Eps, $"flicker rose above 1 at t={t}");
            }
        }

        [Test]
        public void Flicker_DifferentSeedsAreOutOfPhase()
        {
            // Two seeds should generally differ at a fixed time (the per-light phase offset). Check a few times
            // and assert they're not all identical (a degenerate hash would make every light flicker in lockstep).
            bool anyDifferent = false;
            for (float t = 0.1f; t < 3f; t += 0.31f)
            {
                if (Mathf.Abs(LightMath.Flicker(1, t, 0.6f, 1f) - LightMath.Flicker(2, t, 0.6f, 1f)) > 1e-3f)
                { anyDifferent = true; break; }
            }
            Assert.IsTrue(anyDifferent, "two seeds flicker in lockstep — the phase offset isn't working");
        }

        // ---- AdditiveContribution (the whole pipeline) ---------------------------------------------------

        [Test]
        public void AdditiveContribution_IsInvisibleByDay_FullByNight()
        {
            var warm = new Color(1f, 0.86f, 0.6f, 1f);

            // Daytime gate = 0 -> no contribution at all (won't wash out the day).
            Color day = LightMath.AdditiveContribution(warm, 1.5f, shape: 1f, nightGate: 0f, flicker: 1f);
            Assert.AreEqual(0f, day.r, Eps); Assert.AreEqual(0f, day.g, Eps);
            Assert.AreEqual(0f, day.b, Eps); Assert.AreEqual(0f, day.a, Eps);

            // Night gate = 1, full shape -> the full coloured contribution (premultiplied; a = the scalar).
            Color night = LightMath.AdditiveContribution(warm, 1.5f, shape: 1f, nightGate: 1f, flicker: 1f);
            Assert.Greater(night.r, 0f);
            Assert.AreEqual(warm.r * 1.5f, night.r, Eps);
            Assert.AreEqual(1.5f, night.a, Eps);
        }

        [Test]
        public void AdditiveContribution_ScalesWithShapeAndGate()
        {
            var c = Color.white;
            Color half = LightMath.AdditiveContribution(c, 1f, shape: 0.5f, nightGate: 1f, flicker: 1f);
            Assert.AreEqual(0.5f, half.a, Eps);
            Color quarter = LightMath.AdditiveContribution(c, 1f, shape: 0.5f, nightGate: 0.5f, flicker: 1f);
            Assert.AreEqual(0.25f, quarter.a, Eps);
        }

        // ---- CameraDepthZ (the over-water compositing fix; ADR 0016) -------------------------------------

        [Test]
        public void CameraDepthZ_PlacesQuadJustInFrontOfA2DOrthoCamera()
        {
            // The persistent-core camera sits at z = -10 looking toward +Z (forward.z = +1), near clip ~0.3.
            // The light quad must land a small step IN FRONT of it (a more-positive z), i.e. closer to the scene,
            // so its depth beats the big water/ground sprite at world z = 0. = camZ + (near + offset).
            float z = LightMath.CameraDepthZ(cameraZ: -10f, cameraForwardZ: 1f, nearClip: 0.3f, offset: 0.1f);
            Assert.AreEqual(-9.6f, z, Eps, "quad should sit just in front of the camera, ahead of world-z 0 sprites");
            Assert.Greater(z, -10f, "the quad must be in FRONT of the camera, not behind it");
            Assert.Less(z, 0f, "still behind the scene plane at z=0 (it's a small step from the camera)");
        }

        [Test]
        public void CameraDepthZ_GrowsWithOffsetAndNearClip()
        {
            float a = LightMath.CameraDepthZ(-10f, 1f, 0.3f, 0.1f);
            float b = LightMath.CameraDepthZ(-10f, 1f, 0.3f, 0.5f);  // bigger offset -> further from the camera
            Assert.Greater(b, a, "a larger depth offset moves the quad further toward the scene");
            float c = LightMath.CameraDepthZ(-10f, 1f, 1.0f, 0.1f);  // bigger near clip -> further from the camera
            Assert.Greater(c, a, "a larger near clip moves the quad further toward the scene");
        }

        [Test]
        public void CameraDepthZ_NegativeOffsetAndNearAreClampedToZero()
        {
            // Defensive: a (mis)negative offset/near must never pull the quad BEHIND the camera.
            float z = LightMath.CameraDepthZ(-10f, 1f, -5f, -5f);
            Assert.AreEqual(-10f, z, Eps, "negative near/offset clamp to 0 -> the quad sits at the camera, never behind");
        }

        // ---- BoatSpotlight way-gate ----------------------------------------------------------------------

        [Test]
        public void BoatSpotlight_WayBrightness_FullUnderWay_FloorAtStandstill()
        {
            Assert.AreEqual(1f, BoatSpotlight.WayBrightness(2f, 1.2f, 0.15f), Eps, "above full-speed -> full");
            Assert.AreEqual(1f, BoatSpotlight.WayBrightness(1.2f, 1.2f, 0.15f), Eps, "at full-speed -> full");
            Assert.AreEqual(0.15f, BoatSpotlight.WayBrightness(0f, 1.2f, 0.15f), Eps, "stopped -> the floor");
        }

        [Test]
        public void BoatSpotlight_WayBrightness_IsMonotonicInSpeed()
        {
            float prev = -1f;
            for (float s = 0f; s <= 2f; s += 0.1f)
            {
                float v = BoatSpotlight.WayBrightness(s, 1.2f, 0.15f);
                Assert.GreaterOrEqual(v + Eps, prev, $"way-brightness dropped as speed rose at s={s}");
                Assert.GreaterOrEqual(v, 0.15f - Eps); Assert.LessOrEqual(v, 1f + Eps);
                prev = v;
            }
        }
    }
}
