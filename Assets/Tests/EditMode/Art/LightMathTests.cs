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

        // ---- WATER spotlight term (lit from within the water shader; ADR 0016) ---------------------------
        // The water shader mirrors WaterConeTerm + the night-gate EXACTLY, so guarding the maths here guards
        // the in-shader beam-on-water. A cone with halfAngle 30deg, softness ~0.4: cosHalf and cosInner.

        private const float CosHalf30  = 0.8660254f;   // cos(30 deg)
        // inner = 30 * (1 - 0.4) = 18 deg
        private const float CosInner18 = 0.9510565f;   // cos(18 deg)

        [Test]
        public void CosFromHalfAngleDeg_MatchesCosine()
        {
            Assert.AreEqual(1f, LightMath.CosFromHalfAngleDeg(0f), Eps, "0 deg -> cos 1 (on axis)");
            Assert.AreEqual(CosHalf30, LightMath.CosFromHalfAngleDeg(30f), Eps);
            Assert.AreEqual(0f, LightMath.CosFromHalfAngleDeg(90f), Eps, "90 deg -> cos 0");
        }

        [Test]
        public void ConeFalloffCos_FullOnAxis_ZeroOutsideCone()
        {
            // cosAngle 1 (on axis) -> full; cosAngle == cosHalf (the half-angle edge) -> 0; less than cosHalf
            // (a wider angle than the half-angle) -> 0.
            Assert.AreEqual(1f, LightMath.ConeFalloffCos(1f, CosHalf30, CosInner18), Eps, "on axis = full");
            Assert.AreEqual(0f, LightMath.ConeFalloffCos(CosHalf30, CosHalf30, CosInner18), Eps, "at the edge = 0");
            float outside = LightMath.ConeFalloffCos(LightMath.CosFromHalfAngleDeg(45f), CosHalf30, CosInner18);
            Assert.AreEqual(0f, outside, Eps, "beyond the half-angle = 0");
        }

        [Test]
        public void WaterConeTerm_LitOnAxisInsideRange_DarkOutside()
        {
            // Lamp at origin, beam pointing +Y, range 6. A pixel 3 m straight ahead (on-axis, mid-range) is lit;
            // a pixel BEHIND the lamp (-Y) is outside the cone (dark); a pixel beyond the range is dark.
            float onAxis = LightMath.WaterConeTerm(0f, 0f, 0f, 1f,  /*pixel*/ 0f, 3f, 6f, CosHalf30, CosInner18, 0.6f);
            Assert.Greater(onAxis, 0f, "on-axis, in-range -> lit");

            float behind = LightMath.WaterConeTerm(0f, 0f, 0f, 1f,  0f, -3f, 6f, CosHalf30, CosInner18, 0.6f);
            Assert.AreEqual(0f, behind, Eps, "behind the lamp -> outside the cone -> dark");

            float beyond = LightMath.WaterConeTerm(0f, 0f, 0f, 1f,  0f, 9f, 6f, CosHalf30, CosInner18, 0.6f);
            Assert.AreEqual(0f, beyond, Eps, "beyond the range -> dark");
        }

        [Test]
        public void WaterConeTerm_FadesWithDistanceAlongAxis()
        {
            // On the beam axis, the term must NOT increase as the pixel moves further from the lamp (radial fade).
            float prev = 2f;
            for (float d = 0.1f; d < 6f; d += 0.3f)
            {
                float v = LightMath.WaterConeTerm(0f, 0f, 0f, 1f, 0f, d, 6f, CosHalf30, CosInner18, 0.6f);
                Assert.LessOrEqual(v, prev + Eps, $"beam brightened with distance at d={d}");
                Assert.GreaterOrEqual(v, 0f); Assert.LessOrEqual(v, 1f);
                prev = v;
            }
        }

        [Test]
        public void WaterConeTerm_DimmerOffAxisThanOnAxis()
        {
            // At the same distance, a pixel near the cone edge is dimmer than one on the axis (angular falloff).
            // On-axis at (0,3); off-axis at the same radius but swung ~25 deg (inside the 30-deg cone, near edge).
            float onAxis = LightMath.WaterConeTerm(0f, 0f, 0f, 1f, 0f, 3f, 6f, CosHalf30, CosInner18, 0.6f);
            float ang = 25f * Mathf.Deg2Rad;
            float ox = 3f * Mathf.Sin(ang), oy = 3f * Mathf.Cos(ang);
            float offAxis = LightMath.WaterConeTerm(0f, 0f, 0f, 1f, ox, oy, 6f, CosHalf30, CosInner18, 0.6f);
            Assert.Greater(onAxis, offAxis, "the beam axis should be brighter than its feathered edge");
            Assert.Greater(offAxis, 0f, "25 deg is still inside the 30-deg cone -> some light");
        }

        [Test]
        public void WaterConeTerm_AtLampReadsAsCore()
        {
            // A pixel AT the lamp (degenerate direction) must read as the bright on-axis core, not NaN/0.
            float atLamp = LightMath.WaterConeTerm(0f, 0f, 0f, 1f, 0f, 0f, 6f, CosHalf30, CosInner18, 0.6f);
            Assert.AreEqual(1f, atLamp, Eps, "at the lamp -> full (radial 1 × on-axis cone 1)");
        }

        [Test]
        public void WaterTerm_NightGate_OffByDay_FullByNight()
        {
            // The water term reuses LightMath.NightGate (the same as the land cone). Bright day -> off; deep
            // night -> full. (The shader multiplies the cone shape by this same gate.)
            float day = LightMath.NightGate(LightMath.Luminance(Gray(1f)), 0.12f, 0.35f);
            Assert.AreEqual(0f, day, Eps, "bright day -> water beam gated off");
            float night = LightMath.NightGate(LightMath.Luminance(Gray(0.05f)), 0.12f, 0.35f);
            Assert.AreEqual(1f, night, 0.05f, "deep night -> water beam full");
        }

        // ---- CompensateForDayNightTint (the complete-dark crush fix) --------------------------------------
        // The day/night overlay MULTIPLIES the frame by _DayNightTint after the water renders; the water's own
        // additive light content (beam / moon / stars) is pre-compensated by dividing by max(tint, minChannel)
        // so the multiply cancels. These pins guard the shader's post-grade divide via its headless twin.

        // The shipped deepest-night tint: skyTint(0.12, 0.16, 0.34) × the intensity floor 0.18. Every channel
        // exceeds the 0.02 divide floor, so cancellation at deepest night must be EXACT (no hue shift).
        private static readonly Color DeepestNightTint =
            new Color(0.12f * 0.18f, 0.16f * 0.18f, 0.34f * 0.18f, 1f);

        // A representative authored beam colour (warm lamp) the compensation must preserve on screen.
        private static readonly Color WarmBeam = new Color(1.0f, 0.86f, 0.6f, 1f);

        private static Color OnScreen(Color preOverlay, Color tint)   // what the overlay's multiply leaves
            => new Color(preOverlay.r * tint.r, preOverlay.g * tint.g, preOverlay.b * tint.b, preOverlay.a);

        [Test]
        public void Compensate_ExactCancellationAtTheDeepestNightDefaultTint()
        {
            // At the shipped deepest night, every tint channel (~0.022, 0.029, 0.061) exceeds the 0.02 floor,
            // so compensated × tint == the authored term exactly — the beam/moon read at authored brightness
            // and hue in complete dark (the headline fix).
            Color comp = LightMath.CompensateForDayNightTint(
                WarmBeam, DeepestNightTint, LightMath.DayNightCompensationMinChannel);
            Color onScreen = OnScreen(comp, DeepestNightTint);
            Assert.AreEqual(WarmBeam.r, onScreen.r, 1e-4f, "R survives complete dark exactly");
            Assert.AreEqual(WarmBeam.g, onScreen.g, 1e-4f, "G survives complete dark exactly");
            Assert.AreEqual(WarmBeam.b, onScreen.b, 1e-4f, "B survives complete dark exactly (no blue shift)");
        }

        [Test]
        public void Compensate_OnScreenContributionIsConstantAcrossTintLuminance()
        {
            // Once the night gate has saturated, the ON-SCREEN contribution (compensated term × tint) must be
            // ~the same at every darkness level — dusk, midnight, deepest night — whenever every tint channel
            // is at/above the divide floor. (This is what "the light survives the multiply" means.)
            Color[] tints =
            {
                new Color(1f, 1f, 1f, 1f),          // full day (also the no-op case)
                new Color(0.8f, 0.7f, 0.6f, 1f),    // golden dusk
                new Color(0.3f, 0.35f, 0.5f, 1f),   // early night
                new Color(0.05f, 0.06f, 0.09f, 1f), // deep night
                DeepestNightTint,                    // the shipped deepest dark
            };
            foreach (Color tint in tints)
            {
                Color comp = LightMath.CompensateForDayNightTint(
                    WarmBeam, tint, LightMath.DayNightCompensationMinChannel);
                Color onScreen = OnScreen(comp, tint);
                Assert.AreEqual(WarmBeam.r, onScreen.r, 1e-4f, $"R constant on screen at tint {tint}");
                Assert.AreEqual(WarmBeam.g, onScreen.g, 1e-4f, $"G constant on screen at tint {tint}");
                Assert.AreEqual(WarmBeam.b, onScreen.b, 1e-4f, $"B constant on screen at tint {tint}");
            }
        }

        [Test]
        public void Compensate_IsUntouchedWhenTheCycleIsOff()
        {
            // Cycle off/unset -> the tint global is near-black (sum <= 1e-3). There is NO overlay multiplying
            // the frame, so there is nothing to compensate: the term must come back untouched (preserves the
            // edit-mode / bare-art-scene / demo look, the same unset-tint convention the shader branches on).
            Color comp = LightMath.CompensateForDayNightTint(
                WarmBeam, new Color(0f, 0f, 0f, 0f), LightMath.DayNightCompensationMinChannel);
            Assert.AreEqual(WarmBeam.r, comp.r, 0f, "cycle off -> untouched (no boost)");
            Assert.AreEqual(WarmBeam.g, comp.g, 0f, "cycle off -> untouched (no boost)");
            Assert.AreEqual(WarmBeam.b, comp.b, 0f, "cycle off -> untouched (no boost)");
        }

        [Test]
        public void Compensate_IsANoOpInFullDaylight()
        {
            // Tint ~1 (full day) -> divide by 1 -> the daylight look is pixel-identical (the compensation must
            // not brighten anything by day; the beam is also night-gated to 0 there anyway).
            Color comp = LightMath.CompensateForDayNightTint(
                WarmBeam, Color.white, LightMath.DayNightCompensationMinChannel);
            Assert.AreEqual(WarmBeam.r, comp.r, 1e-5f);
            Assert.AreEqual(WarmBeam.g, comp.g, 1e-5f);
            Assert.AreEqual(WarmBeam.b, comp.b, 1e-5f);
        }

        [Test]
        public void Compensate_BoostIsBoundedAt50xByTheChannelFloor()
        {
            // A pathological tint with near-zero / zero channels (but a live cycle: sum > 1e-3) must not explode
            // the divide: the floor (0.02) caps the per-channel boost at 1/0.02 = 50x — never infinity/NaN.
            Assert.AreEqual(0.02f, LightMath.DayNightCompensationMinChannel, 0f,
                "the floor constant pins the 50x bound (and mirrors the shader's DN_COMP_MIN_CHANNEL)");
            var nastyTint = new Color(0.001f, 0f, 0.05f, 1f);       // sum 0.051 > 1e-3 -> cycle counts as ON
            Color comp = LightMath.CompensateForDayNightTint(
                WarmBeam, nastyTint, LightMath.DayNightCompensationMinChannel);
            Assert.AreEqual(WarmBeam.r * 50f, comp.r, 1e-3f, "0.001 channel floors at 0.02 -> exactly 50x");
            Assert.AreEqual(WarmBeam.g * 50f, comp.g, 1e-3f, "zero channel floors at 0.02 -> exactly 50x");
            Assert.AreEqual(WarmBeam.b / 0.05f, comp.b, 1e-3f, "an above-floor channel divides normally");
            Assert.LessOrEqual(comp.r, WarmBeam.r * 50f + Eps, "boost never exceeds the 50x bound");
            Assert.LessOrEqual(comp.g, WarmBeam.g * 50f + Eps, "boost never exceeds the 50x bound");
            Assert.LessOrEqual(comp.b, WarmBeam.b * 50f + Eps, "boost never exceeds the 50x bound");
            Assert.IsFalse(float.IsNaN(comp.r) || float.IsInfinity(comp.r), "no NaN/Inf from a zero channel");
        }
    }
}
