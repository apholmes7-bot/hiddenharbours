using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Art;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// The wave-field bridge (ADR 0018 B1) — the packing contract the water shader's HLSL twin reads,
    /// pinned headless. Three layers of guard:
    ///
    /// <para><b>(1) The packing layout</b> (<see cref="WaveFieldBridge.Pack"/>): per train
    /// (dir.x, dir.y, k = 2π/λ, amplitude) + a wrapped phase, plus (count, crestSharpening,
    /// totalAmplitude). Dead slots publish zero; an empty field publishes ALL zeros (the shader's
    /// "no trains → legacy look" convention).</para>
    ///
    /// <para><b>(2) Twin parity</b>: <see cref="WaveFieldBridge.ShaderTwinSample"/> — the C# mirror of
    /// the shader's <c>WaveFieldSample()</c> — must agree with the reference <see cref="WaveMath.Sample"/>
    /// across the sweep, BOTH for raw <see cref="WaveMath.TrainsFrom"/> trains and through the full
    /// runtime path (<see cref="WaveFieldAnimator"/> ticks → <c>Pack</c> → reconstruct), so the water
    /// pixels provably read the SAME eased sea the hull (<c>BoatWaveMotion</c>) rocks on. Epsilon
    /// philosophy per the ADR §(4): visual parity (well under a pixel-visible difference), not bitwise —
    /// the deliberate mirror deviations are documented on <c>ShaderTwinSample</c>.</para>
    ///
    /// <para><b>(3) The new lifecycle gate math</b>: a byte-for-byte C# twin of the shader's
    /// <c>WhitecapLifecycleWave()</c> (the FoamDensityLifecycleTests convention — if the shader math
    /// changes, update the twin here in the SAME PR) pinning the form → break → fade mechanism:
    /// foam forms on the wave's FRONT face, breaks crisp at the tip, fades to milky residual BEHIND
    /// the crest, and dies entirely in the troughs / at zero density.</para>
    /// </summary>
    public class WaveFieldBridgeTests
    {
        // The WaveMathTests sweep, reused (same winds/seas/positions so the grids line up).
        private static readonly Vector2[] Winds =
            { new Vector2(3f, 1f), new Vector2(-6f, 4f), new Vector2(0f, -11f) };
        private static readonly float[] SeaStates = { 0.25f, 0.6f, 1f };
        private static readonly float[] GridCoords = { -40f, -15f, 0f, 12.5f, 37.25f };

        private const float ParityTolerance = 1e-3f;   // ADR 0018 §(4): visual parity, not bit-exactness

        // ===== (1) THE PACKING LAYOUT =====================================================================

        [Test]
        public void Pack_PublishesDirectionWaveNumberAmplitude_PerTrain()
        {
            var settings = WaveFieldSettings.Default;
            var trains = WaveMath.TrainsFrom(new Vector2(5f, 2f), 0.6f, in settings);
            WaveFieldBridge.Pack(in trains,
                out Vector4 t0, out Vector4 t1, out Vector4 t2, out Vector4 t3,
                out Vector4 phases, out Vector4 fieldParams);

            var packed = new[] { t0, t1, t2, t3 };
            var packedPhases = new[] { phases.x, phases.y, phases.z, phases.w };
            for (int i = 0; i < trains.Count; i++)
            {
                Assert.AreEqual(trains[i].Direction.x, packed[i].x, 1e-6f, $"train {i}: dir.x in .x");
                Assert.AreEqual(trains[i].Direction.y, packed[i].y, 1e-6f, $"train {i}: dir.y in .y");
                Assert.AreEqual((2f * Mathf.PI) / trains[i].Wavelength, packed[i].z, 1e-5f,
                    $"train {i}: .z is the wave number k = 2π/λ (precomputed — the shader never divides)");
                Assert.AreEqual(trains[i].Amplitude, packed[i].w, 1e-6f, $"train {i}: amplitude in .w");
                Assert.GreaterOrEqual(packedPhases[i], 0f, $"train {i}: phase wrapped ≥ 0");
                Assert.Less(packedPhases[i], 2f * Mathf.PI + 1e-4f, $"train {i}: phase wrapped < 2π");
            }

            Assert.AreEqual(trains.Count, fieldParams.x, 1e-6f, "params.x = live train count");
            Assert.AreEqual(trains.CrestSharpening, fieldParams.y, 1e-6f, "params.y = crest sharpening p");
            Assert.AreEqual(trains.TotalAmplitude, fieldParams.z, 1e-5f,
                "params.z = total amplitude (the crest-factor normalizer)");
        }

        [Test]
        public void Pack_EmptyField_PublishesAllZeros_TheLegacyLookConvention()
        {
            WaveTrains none = WaveTrains.None;
            WaveFieldBridge.Pack(in none,
                out Vector4 t0, out Vector4 t1, out Vector4 t2, out Vector4 t3,
                out Vector4 phases, out Vector4 fieldParams);

            Assert.AreEqual(Vector4.zero, t0, "an empty field publishes silence (count 0 → legacy path)");
            Assert.AreEqual(Vector4.zero, t1);
            Assert.AreEqual(Vector4.zero, t2);
            Assert.AreEqual(Vector4.zero, t3);
            Assert.AreEqual(Vector4.zero, phases);
            Assert.AreEqual(Vector4.zero, fieldParams);
        }

        [Test]
        public void Pack_DeadSlots_PublishZero_NeverUndefinedContents()
        {
            var settings = WaveFieldSettings.Default;
            settings.SecondaryTrainCount = 1;               // 2 live trains, slots 2/3 undefined
            var trains = WaveMath.TrainsFrom(new Vector2(5f, 2f), 0.6f, in settings);
            Assert.AreEqual(2, trains.Count, "sanity: two live trains");

            WaveFieldBridge.Pack(in trains,
                out _, out _, out Vector4 t2, out Vector4 t3,
                out Vector4 phases, out Vector4 fieldParams);

            Assert.AreEqual(Vector4.zero, t2, "slot 2 is dead — packed as zero");
            Assert.AreEqual(Vector4.zero, t3, "slot 3 is dead — packed as zero");
            Assert.AreEqual(0f, phases.z, "dead slot phase is zero");
            Assert.AreEqual(0f, phases.w, "dead slot phase is zero");
            Assert.AreEqual(2f, fieldParams.x, 1e-6f, "count says 2");
        }

        [Test]
        public void Pack_IsDeterministic_SameTrainsSameVectors()
        {
            var settings = WaveFieldSettings.Default;
            var trains = WaveMath.TrainsFrom(new Vector2(-6f, 4f), 0.8f, in settings);
            WaveFieldBridge.Pack(in trains, out Vector4 a0, out Vector4 a1, out Vector4 a2,
                                 out Vector4 a3, out Vector4 aPhases, out Vector4 aParams);
            WaveFieldBridge.Pack(in trains, out Vector4 b0, out Vector4 b1, out Vector4 b2,
                                 out Vector4 b3, out Vector4 bPhases, out Vector4 bParams);
            Assert.AreEqual(a0, b0); Assert.AreEqual(a1, b1); Assert.AreEqual(a2, b2);
            Assert.AreEqual(a3, b3); Assert.AreEqual(aPhases, bPhases); Assert.AreEqual(aParams, bParams);
        }

        // ===== (2) TWIN PARITY — the packed field reconstructs the reference surface ======================

        [Test]
        public void ShaderTwin_AgreesWithWaveMathSample_AcrossTheSweep()
        {
            var settings = WaveFieldSettings.Default;
            foreach (var wind in Winds)
            foreach (var sea in SeaStates)
            {
                var trains = WaveMath.TrainsFrom(wind, sea, in settings);
                WaveFieldBridge.Pack(in trains,
                    out Vector4 t0, out Vector4 t1, out Vector4 t2, out Vector4 t3,
                    out Vector4 phases, out Vector4 fieldParams);

                foreach (var x in GridCoords)
                foreach (var y in GridCoords)
                {
                    var pos = new Vector2(x, y);
                    // TrainsFrom offsets are the t = 0 phases, so the reference frame is t = 0.
                    WaveSample reference = WaveMath.Sample(pos, 0.0, in trains);
                    WaveSample twin = WaveFieldBridge.ShaderTwinSample(pos, t0, t1, t2, t3,
                                                                       phases, fieldParams);
                    Assert.AreEqual(reference.Height, twin.Height, ParityTolerance,
                        $"height parity at ({x},{y}) wind {wind} sea {sea}");
                    Assert.AreEqual(reference.Slope.x, twin.Slope.x, ParityTolerance,
                        $"slope.x parity at ({x},{y})");
                    Assert.AreEqual(reference.Slope.y, twin.Slope.y, ParityTolerance,
                        $"slope.y parity at ({x},{y})");
                    Assert.AreEqual(reference.CrestFactor, twin.CrestFactor, ParityTolerance,
                        $"crest-factor parity at ({x},{y})");
                }
            }
        }

        [Test]
        public void ShaderTwin_AgreesWithTheAnimatorSurface_TheRuntimePath()
        {
            // The bridge's ACTUAL runtime path: tick the shared WaveFieldAnimator (the same class
            // BoatWaveMotion ticks), pack Current, reconstruct — the twin must read the SAME surface
            // Animator.Sample gives the hull. Ticked over a long, uneven frame sequence so the
            // accumulated (double, wrapped) phase is exercised well past a trivial t.
            var fieldSettings = WaveFieldSettings.Default;
            var animatorSettings = WaveFieldAnimatorSettings.Default;
            var animator = new WaveFieldAnimator();
            var wind = new Vector2(5f, 2f);

            float[] frameDts = { 0.016f, 0.033f, 0.008f, 0.1f, 0.016f };
            for (int frame = 0; frame < 5000; frame++)
                animator.Tick(frameDts[frame % frameDts.Length], wind, 0.6f,
                              in fieldSettings, in animatorSettings);

            WaveTrains eased = animator.Current;
            WaveFieldBridge.Pack(in eased,
                out Vector4 t0, out Vector4 t1, out Vector4 t2, out Vector4 t3,
                out Vector4 phases, out Vector4 fieldParams);

            foreach (var x in GridCoords)
            foreach (var y in GridCoords)
            {
                var pos = new Vector2(x, y);
                WaveSample hull = animator.Sample(pos);   // what BoatWaveMotion reads
                WaveSample water = WaveFieldBridge.ShaderTwinSample(pos, t0, t1, t2, t3,
                                                                    phases, fieldParams);
                Assert.AreEqual(hull.Height, water.Height, ParityTolerance,
                    $"the water pixel and the hull must ride the same sea at ({x},{y})");
                Assert.AreEqual(hull.Slope.x, water.Slope.x, ParityTolerance, "slope.x");
                Assert.AreEqual(hull.Slope.y, water.Slope.y, ParityTolerance, "slope.y");
                Assert.AreEqual(hull.CrestFactor, water.CrestFactor, ParityTolerance, "crest factor");
            }

            // And the published phases stayed wrapped — the double-accumulate discipline held.
            foreach (float phase in new[] { phases.x, phases.y, phases.z, phases.w })
            {
                Assert.GreaterOrEqual(phase, 0f, "phase wrapped ≥ 0 after a long session");
                Assert.LessOrEqual(phase, 2f * Mathf.PI + 1e-4f, "phase wrapped ≤ 2π after a long session");
            }
        }

        [Test]
        public void GlassCalm_PacksASilentField_AndTheTwinReadsFlat()
        {
            // Glass is sacred (ADR 0018 §(1)): sea state 0 → zero amplitudes → the twin reads a dead
            // flat surface with crest factor 0 — no swell brightness, no foam, the full mirror.
            var settings = WaveFieldSettings.Default;
            var trains = WaveMath.TrainsFrom(new Vector2(4f, -3f), 0f, in settings);
            WaveFieldBridge.Pack(in trains,
                out Vector4 t0, out Vector4 t1, out Vector4 t2, out Vector4 t3,
                out Vector4 phases, out Vector4 fieldParams);

            Assert.AreEqual(0f, fieldParams.z, 1e-7f, "total amplitude is exactly 0 at glass");
            foreach (var x in GridCoords)
            foreach (var y in GridCoords)
            {
                WaveSample s = WaveFieldBridge.ShaderTwinSample(new Vector2(x, y),
                                                                t0, t1, t2, t3, phases, fieldParams);
                Assert.AreEqual(0f, s.Height, "glass means glass: height exactly 0");
                Assert.AreEqual(0f, s.CrestFactor, "crest factor exactly 0 — nothing for foam to ride");
            }
        }

        // ===== (3) THE WHITECAP LIFECYCLE GATE (WhitecapLifecycleWave) ====================================
        // C# twin of the shader's WhitecapLifecycleWave() — kept byte-for-byte aligned (the
        // FoamDensityLifecycleTests convention). If the shader math changes, update this together.

        /// <summary>Twin of the shader <c>WhitecapLifecycleWave(crest, primCos, density)</c>. The
        /// material uniforms it reads (_WhitecapFormSharpness/_WhitecapCollapseRate/_Roughness) are
        /// parameters here.</summary>
        private static void WhitecapLifecycleWave(float crest, float primCos, float density,
                                                  float formSharpness, float collapseRate, float roughness,
                                                  out float breakCore, out float residual)
        {
            float c = Mathf.Clamp01(crest);
            float building = Mathf.Clamp01(-primCos);   // 1 on the front face (the crest is arriving)
            float passed = Mathf.Clamp01(primCos);      // 1 behind the crest (it has passed)

            float breakLo = Mathf.Max(Mathf.Lerp(0.3f, 0.8f, Mathf.Clamp01(formSharpness))
                                      - Mathf.Clamp01(roughness) * 0.35f, 0.05f);
            float breakHi = Mathf.Min(breakLo + Mathf.Lerp(0.3f, 0.1f, Mathf.Clamp01(formSharpness)), 1f);
            float breaking = SmoothStep(breakLo, breakHi, c);
            float forming = SmoothStep(breakLo * 0.5f, breakLo, c) * building * 0.6f;
            breakCore = Mathf.Clamp01(Mathf.Max(breaking, forming) * Mathf.Clamp01(density));

            residual = Mathf.Clamp01(Mathf.Pow(Mathf.Max(c, 1e-4f), Mathf.Max(collapseRate, 0.05f))
                                     * passed * Mathf.Clamp01(density));
        }

        /// <summary>HLSL smoothstep (Hermite), NOT Mathf.SmoothStep (which is a smoothed lerp).</summary>
        private static float SmoothStep(float lo, float hi, float x)
        {
            float t = Mathf.Clamp01((x - lo) / Mathf.Max(hi - lo, 1e-6f));
            return t * t * (3f - 2f * t);
        }

        // The shipped material's tuned values (Water.mat) — the gates must behave at HIS settings.
        private const float DefForm = 0.5f;        // _WhitecapFormSharpness
        private const float DefCollapse = 1.5f;    // _WhitecapCollapseRate
        private const float DefDensity = 0.9f;     // FoamDensity() at a working sea

        [Test]
        public void Lifecycle_FoamFormsOnTheFrontFace_NotBehindTheCrest()
        {
            // Same building crest BELOW the break band (c = 0.3 sits under breakLo at these settings),
            // front face vs back face: the FORMING share only exists ahead of the crest (the wave
            // that is about to break whitens as it builds); behind it only the residual remains.
            WhitecapLifecycleWave(0.3f, -0.9f, DefDensity, DefForm, DefCollapse, 0.5f,
                                  out float frontCore, out float frontResidual);
            WhitecapLifecycleWave(0.3f, 0.9f, DefDensity, DefForm, DefCollapse, 0.5f,
                                  out float backCore, out float backResidual);

            Assert.Greater(frontCore, backCore,
                "a building crest whitens on its FRONT face — foam forms where the crest is arriving");
            Assert.AreEqual(0f, frontResidual, 1e-5f, "no residual ahead of the crest");
            Assert.Greater(backResidual, 0f, "the milky residual trails BEHIND the crest");
        }

        [Test]
        public void Lifecycle_BreaksAtTheCrestTip_BrightAndFullDensity()
        {
            // At the very tip (crest factor 1) the break band saturates regardless of face sign
            // (primCos ~ 0 at the tip): the newborn cap at full density.
            WhitecapLifecycleWave(1f, 0f, 1f, DefForm, DefCollapse, 0.5f,
                                  out float breakCore, out float residual);
            Assert.AreEqual(1f, breakCore, 1e-4f, "the tip breaks at full core");

            // A higher form sharpness narrows the band: a mid crest that breaks at soft settings
            // must NOT break at sharp settings (the crisp narrow band the owner dials).
            WhitecapLifecycleWave(0.55f, 0f, 1f, 0f, DefCollapse, 0f, out float softCore, out _);
            WhitecapLifecycleWave(0.55f, 0f, 1f, 1f, DefCollapse, 0f, out float sharpCore, out _);
            Assert.Greater(softCore, 0f, "soft form: the band reaches down the crest");
            Assert.AreEqual(0f, sharpCore, 1e-5f, "sharp form: only the very tip breaks");
        }

        [Test]
        public void Lifecycle_ResidualFadesBehindTheCrest_FasterAtHigherCollapseRate()
        {
            // Behind the crest, a falling crest factor = the wave moving on: the residual decays,
            // and a higher collapse rate kills it sooner (less trailing milk).
            WhitecapLifecycleWave(0.6f, 1f, DefDensity, DefForm, 0.5f, 0.5f, out _, out float slowFade);
            WhitecapLifecycleWave(0.6f, 1f, DefDensity, DefForm, 3.5f, 0.5f, out _, out float fastFade);
            Assert.Greater(slowFade, fastFade, "higher collapse rate → the residual dies sooner");

            WhitecapLifecycleWave(0.6f, 1f, DefDensity, DefForm, DefCollapse, 0.5f, out _, out float near);
            WhitecapLifecycleWave(0.15f, 1f, DefDensity, DefForm, DefCollapse, 0.5f, out _, out float far);
            Assert.Greater(near, far, "the residual fades as the crest drops away behind the wave");
        }

        [Test]
        public void Lifecycle_TroughsAndZeroDensity_MakeNoFoamAtAll()
        {
            // The trough (crest factor 0) carries nothing — front or back. Zero density (dead calm
            // coupling) silences everything even ON a crest. Glass stays glass.
            WhitecapLifecycleWave(0f, -1f, DefDensity, DefForm, DefCollapse, 0.5f,
                                  out float coreFront, out float residualFront);
            WhitecapLifecycleWave(0f, 1f, DefDensity, DefForm, DefCollapse, 0.5f,
                                  out float coreBack, out float residualBack);
            Assert.AreEqual(0f, coreFront, 1e-5f);
            Assert.AreEqual(0f, coreBack, 1e-5f);
            Assert.AreEqual(0f, residualFront, 1e-5f);
            Assert.Less(residualBack, 1e-4f, "a trough behind a wave keeps (at most) vanishing residual");

            WhitecapLifecycleWave(1f, 0f, 0f, DefForm, DefCollapse, 1f,
                                  out float coreNoDens, out float residualNoDens);
            Assert.AreEqual(0f, coreNoDens, 1e-5f, "zero density: no core even at the tip");
            Assert.AreEqual(0f, residualNoDens, 1e-5f, "zero density: no residual either");
        }

        [Test]
        public void Lifecycle_WindLowersTheBreakBand_AGaleBreaksMoreCrests()
        {
            // The same _Roughness discipline as the cap threshold: wind lowers the break band, so a
            // mid crest that stays clean in light air breaks in a gale — marching whitecaps.
            WhitecapLifecycleWave(0.45f, 0f, 1f, DefForm, DefCollapse, 0f, out float calmCore, out _);
            WhitecapLifecycleWave(0.45f, 0f, 1f, DefForm, DefCollapse, 1f, out float galeCore, out _);
            Assert.Greater(galeCore, calmCore, "wind widens the breaking population of crests");
        }
    }
}
