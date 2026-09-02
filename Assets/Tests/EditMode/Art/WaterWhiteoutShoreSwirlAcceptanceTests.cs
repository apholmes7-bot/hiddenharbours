using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using HiddenHarbours.Art;
using HiddenHarbours.Core;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// Owner playtest 2026-07-23 — two defects in the production water shader, adjudicated in
    /// rendered pixels through the REAL flat pass (<c>Camera.Render()</c>, the project's 2D
    /// renderer, a copy of the shipped Water.mat's tuning):
    ///
    /// <para><b>(a) "sometimes the whole sea becomes white"</b> — from dusk on, the sea
    /// collapsed into a near-uniform bright field (all detail gone). Two shader causes, both
    /// pinned here with ON-SCREEN (day/night-multiplied) histograms:
    /// <list type="number">
    /// <item>the ADR 0015 floor pre-compensation SATURATED through dusk — at a dusk tint it
    /// clamped most of the sea's pre-overlay values to one high floor (the dusk-storm repro
    /// measured 99.7% flat), holding daylight-floor brightness while the scene dimmed. Fixed by
    /// the floor's DAY KNEE (<c>_PaletteFloorKnee</c>; twin <c>WaterPaletteGrade</c>).</item>
    /// <item>the clouds' NIGHT share rode the compensated post-grade bucket at FULL authored
    /// strength — daylight-strength milky bands over a sea the overlay had dimmed to a few
    /// percent. Fixed by the MOONLIT gate (<c>_CloudMoonlitVis</c>; twin
    /// <c>WaterReflection.MoonlitCloudVisibility</c>).</item>
    /// </list></para>
    ///
    /// <para><b>(b) "shoreline looks a bit swirly"</b> — on a GENTLY painted beach the shore
    /// wore metres-wide worm/swirl contours. Two shader causes, pinned on a synthetic
    /// gradient coast (the ShoreSeamProof pattern — the harness owns the bake, so depth at
    /// every pixel is exact):
    /// <list type="number">
    /// <item>the ADR 0023 §23 envelope value bands did not fade with the shore seam (the caps
    /// DO) — band-edge dither worms crowded the shallows. Fixed by <c>bandSeam</c> (the same
    /// <c>ShoreFade01</c>/<c>_ShoreFadeBand</c> the caps read; twin
    /// <c>WhitecapSalienceMath.BandShoreSalience</c>).</item>
    /// <item>the shore-cosmetic DEPTH offsets (beach swash + the §ADR 0012 fringe wiggle) were
    /// slope-blind: visible contour excursion = amplitude ÷ slope, so constants tuned on a
    /// steep edge painted 5× excursions on a 0.18 m/m bar. Fixed by scaling both offsets by
    /// the LOCAL painted slope (<c>SeabedSlopeMag</c>, saturated at the 1 m/m authoring
    /// reference) — the authored amplitudes now mean CONTOUR metres on any coast.</item>
    /// </list></para>
    ///
    /// <para><b>Harness traps honoured:</b> a COPY of the shipped Water.mat with its baked
    /// St Peters height map overridden (uniform-deep: keyword off AND a black height texture;
    /// the shore repro swaps in the synthetic bake), shader warm-up before every measurement
    /// (the cold-cache trap), Null-Device gate FIRST (CI has no GPU and would CRASH, not
    /// fail), all pushed globals cleared in teardown. Every bar below was MEASURED on the
    /// fixed repro (RTX 4060, D3D12, 2026-07-24), then pinned with headroom; the sabotage
    /// arms re-enable each defect through its legacy dial and prove the same assert goes red.
    ///
    /// <para><b>⚠️ Bars are measurements, and measurements expire.</b> The white-out bars were
    /// re-measured 2026-08-01 after #382/#383/#385 legitimately enriched the dusk-storm frame
    /// and left the 2026-07-24 absolutes so slack that the sabotage arm cleared them (the
    /// assert had stopped being able to see its own defect). If a sabotage arm ever reports
    /// NOT DETECTED, that is this failure mode, not a green light — re-measure the pair and
    /// re-place the bar between them. Prefer bars on the defect's MECHANISM over bars on its
    /// downstream symptoms: the mechanism holds still while the look keeps improving.</para></para>
    /// </summary>
    public class WaterWhiteoutShoreSwirlAcceptanceTests
    {
        const int ProbeLayer = 31;
        const int FrameW = 768;
        const int FrameH = 576;
        const float ViewHalfHeightMeters = 9f;   // 18 m tall, 24 m wide at 4:3

        static readonly Vector2 ReferenceWind = new Vector2(-5.4f, -9.33f);
        const float ReferenceSeaState = 0.75f;

        // The dusk/night repro tints (luma 0.335 / 0.167): a clear dusk and a storm-dimmed dusk.
        static readonly Color DuskTint = new Color(0.50f, 0.28f, 0.18f, 1f);
        static readonly Color DuskStormTint = new Color(0.17f, 0.16f, 0.18f, 1f);

        /// <summary>Must be the FIRST statement of every GPU test — on a Null Device the crash
        /// happens in native rendering code no assertion can intercept.</summary>
        static void RequireAGraphicsDevice()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
            {
                Assert.Ignore(
                    "SKIPPED, NOT VERIFIED — this run has no graphics device (Renderer: Null " +
                    "Device), so the white-out / shore-swirl repro could not render and proved " +
                    "nothing. Expected on CI; these pixels only run on a machine with a GPU.");
            }
        }

        [TearDown]
        public void ClearGlobals()
        {
            // The harness pushes the sim/day-night globals directly; never leak them into
            // other tests (the bridge convention: zero = unset).
            // ⚠ Through the bridge's publisher, so the teardown clears the WHOLE field. Zeroing a
            // hand-written list of slot names would have left every slot the list did not mention
            // holding the last scenario's trains — a leak that only shows up as another test's
            // rendering drifting for no reason it can see.
            WaveFieldBridge.PublishGlobals(PackedWaveField.Empty);
            Shader.SetGlobalColor("_DayNightTint", new Color(0, 0, 0, 0));
            Shader.SetGlobalVector("_SunDir", Vector4.zero);
            Shader.SetGlobalFloat("_SunElevation", 0f);
            Shader.SetGlobalVector("_WindWorld", Vector4.zero);
            Shader.SetGlobalVector("_MoonDir", Vector4.zero);
            Shader.SetGlobalVector("_MoonPhaseState", Vector4.zero);
        }

        // ------------------------------------------------------------- the sea publisher

        /// <summary>Publish the field for a game time through the production packing — phases
        /// baked at t in DOUBLE (the WaveFieldBridge discipline; HullWaterlineAcceptanceTests'
        /// twin of ShoreSeamProof's).</summary>
        static void PublishSea(in WaveTrains trains, double timeSeconds)
        {
            const double twoPi = Math.PI * 2.0;
            WaveTrains src = trains;
            WaveTrain Shifted(int i)
            {
                WaveTrain tr = src[i];
                double k = twoPi / tr.Wavelength;
                double phase = tr.PhaseOffset - k * tr.PhaseSpeed * timeSeconds;
                phase -= Math.Floor(phase / twoPi) * twoPi;
                return new WaveTrain(tr.Direction, tr.Wavelength, tr.Amplitude, (float)phase,
                                     WaveFieldSettings.Default.Gravity);
            }

            int n = trains.Count;
            var buffer = new WaveTrain[WaveTrains.MaxTrains];
            for (int i = 0; i < n; i++) buffer[i] = Shifted(i);
            var shifted = WaveTrains.From(buffer, n, trains.CrestSharpening, trains.DominantIndex);
            // Through the bridge's own publisher — never a hand-written copy of the uniform names,
            // or this harness starts driving a narrower field than the shipped shader reads.
            WaveFieldBridge.PublishGlobals(WaveFieldBridge.Pack(in shifted));
        }

        // ------------------------------------------------------------- conditions

        /// <summary>One rendered scenario: the sim pushes WaterSurface would make, plus the
        /// day/night globals DayNightController would publish.</summary>
        struct SeaCondition
        {
            public string Name;
            public Vector2 Wind;          // m/s — trains + _WindDir + _Roughness (mag/12, the shipped windForFullRoughness)
            public float SeaState01;      // trains + _Chop (Choppiness == identity)
            public Color DayNightTint;    // the overlay multiply colour (a=1); (0,0,0,0) = cycle off
            public float SunElevation;    // 1 noon .. 0 horizon .. <0 night
            public float RainIntensity;   // the C#-derived _RainIntensity push
            public Vector4 MoonPhaseState;// (phase, terminator, brightness, aboveHorizon)
            public double TimeSeconds;    // the game time the trains' phases are baked at
        }

        static SeaCondition At(string name, Vector2 wind, float seaState, Color tint,
                               float sunElev, float rain = 0f, float moonUp = 0f,
                               double t = 900.0)
            => new SeaCondition
            {
                Name = name,
                Wind = wind,
                SeaState01 = seaState,
                DayNightTint = tint,
                SunElevation = sunElev,
                RainIntensity = rain,
                MoonPhaseState = moonUp > 0f ? new Vector4(0.5f, 0f, 1f, moonUp) : Vector4.zero,
                TimeSeconds = t,
            };

        static void Apply(Material mat, in SeaCondition c)
        {
            WaveTrains trains = WaveMath.TrainsFrom(c.Wind, c.SeaState01, WaveFieldSettings.Default);
            PublishSea(in trains, c.TimeSeconds);

            Vector2 windDir = c.Wind.sqrMagnitude > 1e-6f ? c.Wind.normalized : Vector2.up;
            mat.SetVector("_WindDir", new Vector4(windDir.x, windDir.y, 0f, 0f));
            mat.SetFloat("_Roughness", Mathf.Clamp01(c.Wind.magnitude / 12f)); // WaterSurface.Roughness default
            mat.SetFloat("_Chop", Mathf.Clamp01(c.SeaState01));                // WaterSurface.Choppiness
            mat.SetFloat("_RainIntensity", Mathf.Clamp01(c.RainIntensity));

            Shader.SetGlobalColor("_DayNightTint", c.DayNightTint);
            Shader.SetGlobalFloat("_SunElevation", c.SunElevation);
            Shader.SetGlobalVector("_SunDir", new Vector4(-0.6f, 0.8f, 0f, 0f));
            Shader.SetGlobalVector("_WindWorld",
                new Vector4(windDir.x, windDir.y, 0f, 0f) * Mathf.Clamp01(c.Wind.magnitude / 12f));
            Shader.SetGlobalVector("_MoonDir", new Vector4(0.35f, 0.85f, 0f, 0f));
            Shader.SetGlobalVector("_MoonPhaseState", c.MoonPhaseState);
        }

        // ------------------------------------------------------------- metrics

        struct FrameStats
        {
            public float Mean, Std, P05, P50, P95;
            public float BrightFrac;   // on-screen sRGB luma > 0.75
            public float FlatFrac;     // within ±0.05 of the median
            public float Spread => P95 - P05;
            public override string ToString() =>
                string.Format(CultureInfo.InvariantCulture,
                    "mean {0:F3} std {1:F3} p05 {2:F3} p50 {3:F3} p95 {4:F3} " +
                    "spread {5:F3} bright {6:P1} flat {7:P1}",
                    Mean, Std, P05, P50, P95, Spread, BrightFrac, FlatFrac);
        }

        /// <summary>On-screen statistics: the linear frame is multiplied by the day/night tint
        /// (the ADR 0013 overlay's multiply — the last thing that happens to the sea before the
        /// player sees it), clamped like the backbuffer, converted to sRGB, then histogrammed.
        /// A tint of (0,0,0,0) (cycle off) means "no overlay runs" — multiply by 1.</summary>
        static FrameStats Stats(Color[] linearFrame, Color tint)
        {
            bool cycleOn = (tint.r + tint.g + tint.b) > 1e-3f;
            int n = linearFrame.Length;
            var lumas = new float[n];
            int bright = 0;
            double sum = 0, sumSq = 0;
            for (int i = 0; i < n; i++)
            {
                Color c = linearFrame[i];
                float r = Mathf.LinearToGammaSpace(Mathf.Clamp01(cycleOn ? c.r * tint.r : c.r));
                float g = Mathf.LinearToGammaSpace(Mathf.Clamp01(cycleOn ? c.g * tint.g : c.g));
                float b = Mathf.LinearToGammaSpace(Mathf.Clamp01(cycleOn ? c.b * tint.b : c.b));
                float l = 0.299f * r + 0.587f * g + 0.114f * b;
                lumas[i] = l;
                sum += l;
                sumSq += l * l;
                if (l > 0.75f) bright++;
            }
            Array.Sort(lumas);
            float mean = (float)(sum / n);
            float variance = Mathf.Max(0f, (float)(sumSq / n) - mean * mean);
            float median = lumas[n / 2];
            int flat = 0;
            for (int i = 0; i < n; i++)
                if (Mathf.Abs(lumas[i] - median) < 0.05f) flat++;
            return new FrameStats
            {
                Mean = mean,
                Std = Mathf.Sqrt(variance),
                P05 = lumas[(int)(0.05f * (n - 1))],
                P50 = median,
                P95 = lumas[(int)(0.95f * (n - 1))],
                BrightFrac = bright / (float)n,
                FlatFrac = flat / (float)n,
            };
        }

        static float Luma(Color c) => 0.299f * c.r + 0.587f * c.g + 0.114f * c.b;

        static int CountDiffering(Color[] a, Color[] b)
        {
            int n = 0;
            for (int i = 0; i < a.Length; i++)
                if (!Mathf.Approximately(a[i].r, b[i].r) || !Mathf.Approximately(a[i].g, b[i].g)
                    || !Mathf.Approximately(a[i].b, b[i].b)) n++;
            return n;
        }

        /// <summary>Mean absolute luma difference between two frames inside a still-depth zone
        /// (the harness owns the synthetic bake, so depth at every pixel is exact).</summary>
        static void ImprintZone(SeaFrameScene scene, Color[] a, Color[] b, float dLo, float dHi,
                                out float imprint, out int px)
        {
            double sum = 0;
            px = 0;
            for (int y = 0; y < FrameH; y++)
                for (int x = 0; x < FrameW; x++)
                {
                    float depth = scene.StillDepthAtPixel(x, y);
                    if (depth <= dLo || depth > dHi) continue;
                    int i = y * FrameW + x;
                    sum += Mathf.Abs(Luma(a[i]) - Luma(b[i]));
                    px++;
                }
            imprint = px > 0 ? (float)(sum / px) : 0f;
        }

        /// <summary>The fraction of a zone's pixels a layer visibly repaints (|ΔL| above a small
        /// deadband) — the AREA a cosmetic offset owns, i.e. the worm-tongue footprint.</summary>
        static float RepaintFraction(SeaFrameScene scene, Color[] a, Color[] b, float dLo, float dHi)
        {
            int painted = 0, total = 0;
            for (int y = 0; y < FrameH; y++)
                for (int x = 0; x < FrameW; x++)
                {
                    float depth = scene.StillDepthAtPixel(x, y);
                    if (depth <= dLo || depth > dHi) continue;
                    int i = y * FrameW + x;
                    total++;
                    if (Mathf.Abs(Luma(a[i]) - Luma(b[i])) > 0.02f) painted++;
                }
            return total > 0 ? painted / (float)total : 0f;
        }

        // ------------------------------------------------------------- evidence dump

        static string EvidenceDir =>
            Environment.GetEnvironmentVariable("HH_WATER_EVIDENCE");

        static void DumpPng(string name, Color[] linearFrame, Color tint)
        {
            string dir = EvidenceDir;
            if (string.IsNullOrEmpty(dir)) return;
            bool cycleOn = (tint.r + tint.g + tint.b) > 1e-3f;
            var tex = new Texture2D(FrameW, FrameH, TextureFormat.RGBA32, false);
            var px = new Color32[FrameW * FrameH];
            for (int i = 0; i < linearFrame.Length; i++)
            {
                Color c = linearFrame[i];
                px[i] = new Color32(
                    (byte)(Mathf.LinearToGammaSpace(Mathf.Clamp01(cycleOn ? c.r * tint.r : c.r)) * 255f + 0.5f),
                    (byte)(Mathf.LinearToGammaSpace(Mathf.Clamp01(cycleOn ? c.g * tint.g : c.g)) * 255f + 0.5f),
                    (byte)(Mathf.LinearToGammaSpace(Mathf.Clamp01(cycleOn ? c.b * tint.b : c.b)) * 255f + 0.5f),
                    255);
            }
            tex.SetPixels32(px);
            tex.Apply(false);
            Directory.CreateDirectory(dir);
            File.WriteAllBytes(Path.Combine(dir, name + ".png"), tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }

        // ============================================================================
        // (a) THE WHITE-OUT
        // ============================================================================

        /// <summary>
        /// The dusk-storm repro (the owner's "whole sea becomes white"): the ON-SCREEN frame
        /// must keep its value structure. The SABOTAGE arm re-enables the defect through the
        /// legacy dial (<c>_PaletteFloorKnee</c> = 0 — the exact pre-fix curve) and the same
        /// bars go red, proving the assert can see this defect.
        ///
        /// <para>Bars re-measured 2026-08-01 (RTX 4060, D3D12) — fixed vs knee-0 sabotage:
        /// spread 0.288 / 0.173, flat 28.4% / 51.9%, p05 0.029 / 0.148. The ORIGINAL 2026-07-24
        /// numbers (fixed spread 0.076 / flat 94.5%; defect 0.037 / 99.7%) describe a frame that
        /// no longer exists: #382's dark knobs, #383's foam and #385's tide pacing gave this sea
        /// far more crest/trough structure. The look improved; the absolutes went stale, and the
        /// sabotage arm started sailing over them. See the bar block in the body for the full
        /// reasoning and for why <b>p05</b> — the mechanism-direct measure (the floor clamps the
        /// sea's dark end UP) — now carries this pin rather than the downstream spread/flatness.</para>
        /// </summary>
        [Test]
        public void WhiteOut_DuskStorm_KeepsValueStructure_OnScreen()
        {
            RequireAGraphicsDevice();

            SeaCondition c = At("dusk_storm", new Vector2(-8.0f, -13.9f), 1.0f,
                                DuskStormTint, 0.06f, rain: 1f);
            using var scene = new SeaFrameScene(withShoreGradient: false);
            Apply(scene.WaterMat, c);

            Color[] fixedFrame = scene.Render();
            FrameStats fixedStats = Stats(fixedFrame, c.DayNightTint);
            Debug.Log($"[white-out] dusk_storm FIXED: {fixedStats}");
            DumpPng("whiteout_duskstorm_after", fixedFrame, c.DayNightTint);

            // ⚠️ RECALIBRATED 2026-08-01. The bars below were absolute constants measured on the
            // 2026-07-24 dusk-storm frame (fixed: spread 0.076, flat 94.5%; defect: 0.037, 99.7%).
            // That frame no longer exists: #382's dark knobs + #383's foam + #385's tide pacing
            // (amplitude 2.2) gave the dusk-storm sea FAR more crest/trough structure — the same
            // repro now measures spread 0.288 / flat 28.4%, i.e. ~4x the value range and a third of
            // the flatness. The shipped look did NOT regress; it improved. But the old absolutes
            // then sat so far below the new fixed frame that the SABOTAGE arm sailed straight over
            // them (knee 0 measured spread 0.173 / flat 51.9% — comfortably "healthy" by 2026-07-24
            // standards), so the assert stopped being able to see the defect it exists to pin.
            //
            // Re-measured pair (RTX 4060, D3D12, 2026-08-01), bars placed at the MIDPOINT of each:
            //                     FIXED        SABOTAGE (knee 0)     bar
            //   spread (p95-p05)  0.288        0.173                 0.23
            //   flat fraction     28.4%        51.9%                 40%
            //   p05               0.029        0.148                 0.08
            //
            // ⭐ p05 IS THE NEW BAR, and it is the one that should keep this pin honest. Spread and
            // flatness are DOWNSTREAM consequences of the white-out, so their absolute values track
            // however much structure the sea happens to have that month — which is exactly why this
            // guard rotted twice. p05 measures the defect's MECHANISM directly: the floor pre-
            // compensation clamps the sea's low values UP to one high floor, so the dark end stops
            // being dark. That signal separates the two states by 5x (0.029 vs 0.148) where spread
            // manages only 1.7x, and it stays true no matter how rich the sea gets.
            Assert.Greater(fixedStats.Spread, 0.23f,
                $"the dusk-storm sea's on-screen value spread (p95−p05) is {fixedStats.Spread:F3} " +
                "— the crest/trough/foam structure has collapsed toward the white-out sheet " +
                "(knee-0 sabotage: 0.173; healthy: 0.288).");
            Assert.Less(fixedStats.FlatFrac, 0.40f,
                $"the dusk-storm sea is {fixedStats.FlatFrac:P1} flat — a near-uniform sheet. " +
                "The owner's white-out is back (knee-0 sabotage measures 51.9%; healthy 28.4%): the " +
                "palette floor's dusk clamp (or another whole-sea layer) is flattening the structure.");
            Assert.Less(fixedStats.P05, 0.08f,
                $"the dusk-storm sea's DARK end has been lifted to {fixedStats.P05:F3} — the bottom " +
                "of the value distribution is being clamped to a floor instead of riding down with " +
                "the scene (knee-0 sabotage: 0.148; healthy: 0.029). This is the white-out's " +
                "mechanism, not merely its symptom: the ADR 0015 floor pre-compensation is " +
                "saturating through dusk again.");

            // ---- SABOTAGE: the legacy floor curve must trip the SAME bars -----------------
            scene.WaterMat.SetFloat("_PaletteFloorKnee", 0f);   // the pre-fix saturating curve
            Color[] legacy = scene.Render();
            FrameStats legacyStats = Stats(legacy, c.DayNightTint);
            Debug.Log($"[white-out] dusk_storm SABOTAGE (knee 0): {legacyStats}");
            DumpPng("whiteout_duskstorm_before", legacy, c.DayNightTint);
            // The SAME three bars the assert above uses, in the same direction — an OR, because the
            // three Asserts are an AND, so "the assert would have gone red" is "ANY bar trips".
            // Keep this list in lockstep with the bars above: a bar added there and forgotten here
            // is a bar whose sabotage arm proves nothing.
            bool sabotageTripped = legacyStats.Spread <= 0.23f
                                || legacyStats.FlatFrac >= 0.40f
                                || legacyStats.P05 >= 0.08f;
            Assert.IsTrue(sabotageTripped,
                "SABOTAGE NOT DETECTED — with the legacy floor curve (knee 0) the dusk-storm " +
                $"frame still passed the structure bars ({legacyStats}). The assert cannot see " +
                "the white-out it exists to pin, and the green run above is worth less than it looks.");
        }

        /// <summary>
        /// The dusk-calm repro: on a MOONLESS dusk the compensated night-cloud share must be
        /// gone (clouds are a reflection, not a light source — nothing lights them), and under
        /// a FULL HIGH MOON a faint moonlit share must remain (the owner's approved night sky
        /// content is preserved) — well below the pre-fix full strength, whose veil this pin
        /// exists to keep dead. The moon/star/sun-glitter elements are zeroed so the sky
        /// imprint isolates the CLOUDS.
        /// </summary>
        [Test]
        public void WhiteOut_DuskClouds_AreMoonlit_NotAVeil()
        {
            RequireAGraphicsDevice();

            SeaCondition c = At("dusk_calm", new Vector2(-1.5f, -2.6f), 0.2f, DuskTint, 0.06f);
            using var scene = new SeaFrameScene(withShoreGradient: false);
            Apply(scene.WaterMat, c);
            Material mat = scene.WaterMat;
            mat.SetFloat("_MoonStrength", 0f);      // isolate the CLOUD share of the sky content
            mat.SetFloat("_MoonGlitter", 0f);
            mat.SetFloat("_StarStrength", 0f);
            mat.SetFloat("_SunGlitterStrength", 0f);

            // MOON DOWN (published state: below the horizon — a real pre-moonrise dusk).
            Shader.SetGlobalVector("_MoonPhaseState", new Vector4(0.5f, 0f, 0f, 0f));
            float moonDown = CloudImprint(scene, mat, c, "moon_down");

            // MOON UP + FULL: the faint moonlit share must survive.
            Shader.SetGlobalVector("_MoonPhaseState", new Vector4(0.5f, 0f, 1f, 1f));
            float moonUp = CloudImprint(scene, mat, c, "moon_up");

            // LEGACY (sabotage the same assert): _CloudMoonlitVis = 1 is the exact pre-fix
            // full-strength night share — the veil magnitude.
            mat.SetFloat("_CloudMoonlitVis", 1f);
            float legacyVeil = CloudImprint(scene, mat, c, "legacy_veil");
            Debug.Log($"[white-out] dusk cloud imprint: moonDown {moonDown:F4}, moonUp {moonUp:F4}, " +
                      $"legacy veil {legacyVeil:F4}");

            Assert.Less(moonDown, 0.01f,
                $"on a MOONLESS dusk the night-cloud share still painted a {moonDown:F4} mean-|ΔL| " +
                "veil over the sea — unlit clouds must not glow (the 2026-07-23 white-veil defect).");
            Assert.Greater(moonUp, 0.004f,
                $"under a full high moon the moonlit cloud share vanished ({moonUp:F4}) — the " +
                "owner's approved night sky content must survive the veil fix, merely FAINT.");
            Assert.Greater(legacyVeil, moonUp * 1.5f,
                "SABOTAGE NOT DETECTED — the legacy full-strength night share " +
                $"({legacyVeil:F4}) does not exceed the moonlit share ({moonUp:F4}) — the " +
                "cloud imprint cannot see the veil it exists to pin.");
        }

        /// <summary>The clouds' imprint = |sky content on − off| (with moon/stars/sun glitter
        /// zeroed, the difference IS the clouds), measured as mean |ΔL| over the frame.</summary>
        static float CloudImprint(SeaFrameScene scene, Material mat, in SeaCondition c, string tag)
        {
            mat.SetFloat("_SkyReflectionStrength", 0.7f);   // the shipped master
            Color[] on = scene.Render();
            mat.SetFloat("_SkyReflectionStrength", 0f);
            Color[] off = scene.Render();
            mat.SetFloat("_SkyReflectionStrength", 0.7f);
            double sum = 0;
            for (int i = 0; i < on.Length; i++)
                sum += Mathf.Abs(Luma(on[i]) - Luma(off[i]));
            DumpPng($"clouds_{tag}_on", on, c.DayNightTint);
            return (float)(sum / on.Length);
        }

        /// <summary>
        /// The displaced pass must wear the SAME final grade as the flat pass (the ADR 0023
        /// one-sea rule; the diagnosis lead asked for an A/B of the same scene state). The two
        /// sides share one fragment, so their on-screen statistics must agree — the displaced
        /// side merely lifts pixels. Rendered through the production seam: the off-screen
        /// HHWater pass via IsoFacetHullFeature + the WaterOverlay quad, exactly as
        /// DisplacedWaterSurface wires it in play.
        /// </summary>
        [Test]
        public void DisplacedAndFlat_ShareTheGrade_AtDusk()
        {
            RequireAGraphicsDevice();

            SeaCondition c = At("dusk_ab", ReferenceWind, ReferenceSeaState, DuskTint, 0.06f);
            using var scene = new SeaFrameScene(withShoreGradient: false);
            Apply(scene.WaterMat, c);

            Color[] flat = scene.Render();
            FrameStats flatStats = Stats(flat, c.DayNightTint);

            scene.AttachDisplaced();
            Color[] displaced = scene.Render();
            FrameStats dispStats = Stats(displaced, c.DayNightTint);
            scene.DetachDisplaced();

            Debug.Log($"[displaced-A/B] flat: {flatStats}");
            Debug.Log($"[displaced-A/B] displaced: {dispStats}");
            DumpPng("ab_dusk_flat", flat, c.DayNightTint);
            DumpPng("ab_dusk_displaced", displaced, c.DayNightTint);

            Assert.Less(Mathf.Abs(dispStats.Mean - flatStats.Mean), 0.03f,
                "the displaced sea's on-screen brightness drifted from the flat sea's — the two " +
                "passes are one fragment and must wear one grade (mean " +
                $"{dispStats.Mean:F3} vs {flatStats.Mean:F3}).");
            Assert.Less(Mathf.Abs(dispStats.Std - flatStats.Std), 0.03f,
                "the displaced sea's on-screen contrast drifted from the flat sea's (std " +
                $"{dispStats.Std:F3} vs {flatStats.Std:F3}) — one of the two sides is being " +
                "graded/composited differently (the ARGBHalf overlay route must stay a plain re-compose).");
        }

        // ============================================================================
        // (b) THE SHORE SWIRL
        // ============================================================================

        /// <summary>
        /// The envelope value bands fade with the SHORE SEAM: on the synthetic gentle coast,
        /// the bands' imprint INSIDE the seam band must be a small fraction of their open-water
        /// imprint (pre-fix the two were comparable — worm contours crowding the shallows), and
        /// open water must KEEP its bands (the §23 style survives). Static sea (drift-checked),
        /// the seam band set to the scale the displaced push derives at this sea (~2.5 m).
        /// SABOTAGE: a degenerate band (≈0 ⇒ ShoreFade01 degrades to no-fade — the documented
        /// legacy contract) must trip the same in-seam bar.
        /// </summary>
        [Test]
        public void ShoreSwirl_EnvelopeBands_FadeWithTheSeam()
        {
            RequireAGraphicsDevice();

            using var scene = new SeaFrameScene(withShoreGradient: true);
            Material mat = scene.WaterMat;
            SeaCondition c = At("shore_bands", ReferenceWind, ReferenceSeaState, Color.white, 0.85f);
            Apply(mat, c);
            scene.MakeStatic();
            mat.SetFloat("_ShoreFadeBand", 2.5f);   // the derived-scale band at this sea
            // Isolate the BANDS from the shore foam that otherwise paints over them in the
            // shallows (the imprint would measure foam, not bands): no wind froth, no fringe.
            // The trains (and so the band layer) are published independently of _Roughness.
            mat.SetFloat("_Roughness", 0f);
            mat.SetFloat("_FoamWidth", 0.001f);

            // Sanity: a static sea must render byte-identically twice, or the imprint diff lies.
            Color[] s1 = scene.Render();
            Color[] s2 = scene.Render();
            Assert.AreEqual(0, CountDiffering(s1, s2),
                "the 'static' shore scene drifted between renders — a time-driven layer is " +
                "still live and the band-imprint difference below would be noise.");

            float inSeam = BandImprint(scene, mat, 0.2f, 1.0f, out int inPx);
            float outSeam = BandImprint(scene, mat, 2.75f, 3.4f, out int outPx);
            Debug.Log($"[shore-bands] in-seam (0.2..1.0m) {inSeam:F4} ({inPx} px); " +
                      $"open (2.75..3.4m) {outSeam:F4} ({outPx} px)");

            Assert.Greater(outSeam, 0.010f,
                $"open water lost its envelope bands ({outSeam:F4}) — the seam fade must never " +
                "reach past the band (the §23 marked-wave style is owner-approved).");
            Assert.Less(inSeam, outSeam * 0.35f,
                $"the envelope bands still draw inside the seam band ({inSeam:F4} vs open " +
                $"{outSeam:F4}) — the band worms are back on the dying displaced edge " +
                "(the 2026-07-23 swirl defect).");

            // ---- SABOTAGE: a no-fade band must trip the in-seam bar -----------------------
            mat.SetFloat("_ShoreFadeBand", 0.0001f);   // degrades to "no fade" (the legacy contract)
            float inSeamLegacy = BandImprint(scene, mat, 0.2f, 1.0f, out _);
            float outSeamLegacy = BandImprint(scene, mat, 2.75f, 3.4f, out _);
            Debug.Log($"[shore-bands] SABOTAGE (no-fade band): in-seam {inSeamLegacy:F4}, " +
                      $"open {outSeamLegacy:F4}");
            Assert.GreaterOrEqual(inSeamLegacy, outSeamLegacy * 0.35f,
                "SABOTAGE NOT DETECTED — with the seam fade disabled the in-seam band imprint " +
                $"({inSeamLegacy:F4}) still sits below the bar relative to open water " +
                $"({outSeamLegacy:F4}); the assert cannot see the unfaded worms it exists to pin.");
        }

        static float BandImprint(SeaFrameScene scene, Material mat, float dLo, float dHi, out int px)
        {
            float prev = mat.GetFloat("_EnvelopeBandStrength");
            Color[] on = scene.Render();
            mat.SetFloat("_EnvelopeBandStrength", 0f);
            Color[] off = scene.Render();
            mat.SetFloat("_EnvelopeBandStrength", prev);
            ImprintZone(scene, on, off, dLo, dHi, out float imprint, out px);
            return imprint;
        }

        /// <summary>
        /// The shore-cosmetic offsets (beach swash + the fringe wiggle) are SLOPE-TRUE: on the
        /// gentle 0.18 m/m coast their repaint footprint in the wet-edge zone must be a modest
        /// strip (authored ±0.3 m of contour excursion), not the metres-wide worm-tongue field
        /// the slope-blind constants painted (pre-fix they repainted the bulk of the zone).
        /// SABOTAGE: re-inflating the amplitudes to the pre-fix VISIBLE excursion
        /// (amplitude ÷ slope — what the un-scaled shader effectively rendered) must trip the
        /// same bar, proving the footprint metric sees the defect magnitude.
        /// </summary>
        [Test]
        public void ShoreSwirl_CosmeticContourExcursion_IsSlopeTrue()
        {
            RequireAGraphicsDevice();

            using var scene = new SeaFrameScene(withShoreGradient: true);
            Material mat = scene.WaterMat;
            SeaCondition c = At("shore_swash", ReferenceWind, ReferenceSeaState, Color.white, 0.85f);
            Apply(mat, c);
            scene.MakeStatic();
            // Swash ON but FROZEN (speed 0 keeps the spatial tongue field, drops the animation)
            // at the shipped amplitude; the fringe wiggle at the shipped strength.
            mat.SetFloat("_SwashAmplitude", 0.3f);
            mat.SetFloat("_ShoreNoise", 0.75f);

            Color[] s1 = scene.Render();
            Color[] s2 = scene.Render();
            Assert.AreEqual(0, CountDiffering(s1, s2),
                "the 'static' swash scene drifted between renders — the footprint diff would be noise.");

            float footprint = CosmeticFootprint(scene, mat, out Color[] baseFrame);
            Debug.Log($"[shore-swash] cosmetic repaint beyond the band's reach (1.8..2.1m): {footprint:P1}");
            DumpPng("swirl_shore_after", baseFrame, Color.white);

            // The zone sits just BEYOND the shipped foam band (_FoamWidth 1.74 m of depth) plus
            // the slope-true offsets' depth reach (authored ±0.3 m of contour ⇒ ±0.054 m of
            // depth): inside the band, toggling the offsets re-thresholds the churn's wide
            // milky transition in place (the band's normal life — measured bins die by 1.8 m).
            // Pixels repainted DEEPER are genuine worm-tongue REACH: the slope-true offsets
            // leave the zone untouched (measured 0.1%); the pre-fix slope-blind swash
            // (±0.3 m of DEPTH once re-inflated) pushes the churn boundary through it
            // (measured 37.4%).
            Assert.Less(footprint, 0.05f,
                $"the swash + fringe repaint {footprint:P1} of the water beyond their slope-true " +
                "reach on a gentle 0.18 m/m beach — metres-wide worm tongues (the 2026-07-23 " +
                "swirl). The authored amplitudes must read as CONTOUR metres (slope-scaled), " +
                "not depth metres.");

            // ---- SABOTAGE: the pre-fix visible excursion must trip the same bar -----------
            mat.SetFloat("_SwashAmplitude", 0.3f / 0.18f);   // what slope-blind constants rendered
            mat.SetFloat("_ShoreNoise", 0.75f / 0.18f);
            float legacyFootprint = CosmeticFootprint(scene, mat, out Color[] legacyFrame);
            Debug.Log($"[shore-swash] SABOTAGE (pre-fix excursion): footprint {legacyFootprint:P1}");
            DumpPng("swirl_shore_before", legacyFrame, Color.white);
            Assert.GreaterOrEqual(legacyFootprint, 0.05f,
                "SABOTAGE NOT DETECTED — at the pre-fix visible excursion the footprint " +
                $"({legacyFootprint:P1}) stayed under the bar; the metric cannot see the worm " +
                "tongues it exists to pin.");
        }

        /// <summary>The cosmetic footprint: base frame vs both shore cosmetics zeroed — the
        /// repainted fraction of the 1.9..2.6 m zone, BEYOND the shipped 1.74 m foam band plus
        /// the slope-true offsets' reach (in-band re-thresholding is the band's normal life and
        /// doesn't count; only genuine tongue REACH past the band does).</summary>
        static float CosmeticFootprint(SeaFrameScene scene, Material mat, out Color[] baseFrame)
        {
            float prevSwash = mat.GetFloat("_SwashAmplitude");
            float prevNoise = mat.GetFloat("_ShoreNoise");
            baseFrame = scene.Render();
            mat.SetFloat("_SwashAmplitude", 0f);
            mat.SetFloat("_ShoreNoise", 0f);
            Color[] off = scene.Render();
            mat.SetFloat("_SwashAmplitude", prevSwash);
            mat.SetFloat("_ShoreNoise", prevNoise);
            for (float lo = 0f; lo < 3f; lo += 0.3f)
                Debug.Log($"[shore-swash]   bin {lo:F1}..{lo + 0.3f:F1}m repaint " +
                          $"{RepaintFraction(scene, baseFrame, off, lo, lo + 0.3f):P1}");
            DumpDiffPng("swirl_footprint_diff", baseFrame, off);
            return RepaintFraction(scene, baseFrame, off, 1.8f, 2.1f);
        }

        static void DumpDiffPng(string name, Color[] a, Color[] b)
        {
            string dir = EvidenceDir;
            if (string.IsNullOrEmpty(dir)) return;
            var tex = new Texture2D(FrameW, FrameH, TextureFormat.RGBA32, false);
            var px = new Color32[a.Length];
            for (int i = 0; i < a.Length; i++)
            {
                byte v = (byte)(Mathf.Clamp01(Mathf.Abs(Luma(a[i]) - Luma(b[i])) * 4f) * 255f + 0.5f);
                px[i] = new Color32(v, v, v, 255);
            }
            tex.SetPixels32(px);
            tex.Apply(false);
            Directory.CreateDirectory(dir);
            File.WriteAllBytes(Path.Combine(dir, name + ".png"), tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }

        // ============================================================================
        // the condition sweep (regression radar + evidence frames; logs only)
        // ============================================================================

        [Test]
        public void ConditionSweep_LogsOnScreenHistograms()
        {
            RequireAGraphicsDevice();

            Color noon = Color.white;
            Color overcastDay = new Color(0.42f, 0.44f, 0.47f, 1f);
            Color nightMoon = new Color(0.05f, 0.06f, 0.10f, 1f);
            Color nightDeep = new Color(0.022f, 0.029f, 0.061f, 1f);
            Vector2 calmWind = new Vector2(-1.5f, -2.6f);
            Vector2 stormWind = new Vector2(-8.0f, -13.9f);

            var conditions = new[]
            {
                At("noon_calm", calmWind, 0.2f, noon, 0.85f),
                At("noon_ref_sea", ReferenceWind, ReferenceSeaState, noon, 0.85f),
                At("noon_storm", stormWind, 1.0f, overcastDay, 0.85f, rain: 0.9f),
                At("dusk_calm", calmWind, 0.2f, DuskTint, 0.06f),
                At("dusk_ref_sea", ReferenceWind, ReferenceSeaState, DuskTint, 0.06f),
                At("dusk_storm", stormWind, 1.0f, DuskStormTint, 0.06f, rain: 1f),
                At("night_calm_fullmoon", calmWind, 0.2f, nightMoon, -0.5f, moonUp: 1f),
                At("night_storm", stormWind, 1.0f, nightDeep, -0.5f, rain: 1f),
            };

            using var scene = new SeaFrameScene(withShoreGradient: false);
            foreach (var c in conditions)
            {
                Apply(scene.WaterMat, c);
                Color[] frame = scene.Render();
                Debug.Log($"[white-sea sweep] {c.Name}: {Stats(frame, c.DayNightTint)}");
                DumpPng("sweep_" + c.Name, frame, c.DayNightTint);
            }
        }

        // ------------------------------------------------------------- the harness scene

        /// <summary>
        /// A self-cleaning water-only scene rendered through the production 2D renderer: one
        /// quad carrying a COPY of the shipped Water.mat (the owner's real tuning — payload of
        /// both defects), its baked St Peters height map overridden (the ADR 0023 harness
        /// trap): uniform-deep for the white-out repro, a synthetic wiggly gentle coast for the
        /// shore repro (the ShoreSeamProof pattern — the harness KNOWS the depth at every
        /// pixel). <see cref="AttachDisplaced"/> wires the production displaced seam (registry
        /// + off-screen HHWater pass + WaterOverlay quad) for the A/B parity pin.
        /// </summary>
        sealed class SeaFrameScene : IDisposable
        {
            readonly GameObject _camGo;
            readonly Camera _cam;
            readonly RenderTexture _rt;
            readonly GameObject _waterGo;
            readonly Mesh _quad;
            readonly Texture2D _heightTex;
            public readonly Material WaterMat;

            GameObject _dispGo;
            GameObject _overlayGo;
            DisplacedWaterSurface _surface;
            Material _dispMat;
            Material _overlayMat;
            Mesh _gridMesh;
            Mesh _overlayQuad;

            readonly Rect _rect;
            readonly bool _shore;
            bool _warm;

            const float HeightMin = -8f;
            const float HeightMax = 4f;
            const float BeachSlope = 0.18f;    // the gentle painted bar (m elevation per m ground)

            public SeaFrameScene(bool withShoreGradient)
            {
                _shore = withShoreGradient;
                float halfH = ViewHalfHeightMeters;
                float halfW = halfH * FrameW / FrameH;
                _rect = Rect.MinMaxRect(-halfW - 3f, -halfH - 3f, halfW + 3f, halfH + 3f);

                _camGo = new GameObject("WaterFrameCam");
                _cam = _camGo.AddComponent<Camera>();
                _cam.orthographic = true;
                _cam.orthographicSize = halfH;
                _cam.transform.position = new Vector3(0f, 0f, -100f);
                _cam.nearClipPlane = 1f;
                _cam.farClipPlane = 400f;
                _cam.clearFlags = CameraClearFlags.SolidColor;
                _cam.backgroundColor = new Color(0.1f, 0.1f, 0.1f, 1f);   // "terrain" behind the blend
                _cam.cullingMask = 1 << ProbeLayer;
                _cam.allowHDR = true;    // the production URP asset is HDR — pre-compensated >1 light must survive
                _cam.allowMSAA = false;

                _rt = new RenderTexture(FrameW, FrameH, 24, RenderTextureFormat.ARGBFloat)
                {
                    filterMode = FilterMode.Point,
                };
                _cam.targetTexture = _rt;

                var shipped = AssetDatabase.LoadAssetAtPath<Material>(
                    "Assets/_Project/Art/Materials/Water.mat");
                Assert.IsNotNull(shipped, "shipped Water.mat missing");
                WaterMat = new Material(shipped) { hideFlags = HideFlags.HideAndDontSave };

                // Override the baked St Peters height read (the harness trap): either force
                // uniform-deep, or bake the synthetic coast this harness owns.
                _heightTex = _shore ? BuildCoastTexture(_rect) : BuildBlackTexture();
                WaterMat.SetTexture("_HeightTex", _heightTex);
                WaterMat.SetFloat("_HeightMin", HeightMin);
                WaterMat.SetFloat("_HeightMax", HeightMax);
                WaterMat.SetVector("_HeightWorldMin", new Vector4(_rect.xMin, _rect.yMin, 0f, 0f));
                WaterMat.SetVector("_HeightWorldSize", new Vector4(_rect.width, _rect.height, 0f, 0f));
                WaterMat.SetFloat("_WaterLevel", 0f);
                if (_shore)
                {
                    WaterMat.EnableKeyword("_USE_HEIGHTTEX");
                    WaterMat.SetFloat("_UseHeightTex", 1f);
                }
                else
                {
                    WaterMat.DisableKeyword("_USE_HEIGHTTEX");
                    WaterMat.SetFloat("_UseHeightTex", 0f);
                }

                _quad = new Mesh { name = "WaterFrameQuad" };
                _quad.SetVertices(new[]
                {
                    new Vector3(_rect.xMin, _rect.yMin, 0f), new Vector3(_rect.xMax, _rect.yMin, 0f),
                    new Vector3(_rect.xMax, _rect.yMax, 0f), new Vector3(_rect.xMin, _rect.yMax, 0f),
                });
                _quad.SetUVs(0, new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up });
                _quad.SetTriangles(new[] { 0, 1, 2, 0, 2, 3 }, 0);
                _waterGo = new GameObject("WaterFrameSea") { layer = ProbeLayer };
                _waterGo.AddComponent<MeshFilter>().sharedMesh = _quad;
                var mr = _waterGo.AddComponent<MeshRenderer>();
                mr.sharedMaterial = WaterMat;
                mr.shadowCastingMode = ShadowCastingMode.Off;
                mr.receiveShadows = false;
                mr.lightProbeUsage = LightProbeUsage.Off;
            }

            /// <summary>The synthetic coast: land rises on the RIGHT at the GENTLE painted-bar
            /// slope; the depth-0 contour wiggles along shore (a deterministic sinusoid mix —
            /// the worm bed of the swirl defect).</summary>
            static Texture2D BuildCoastTexture(Rect rect)
            {
                const int N = 512;
                var tex = new Texture2D(N, N, TextureFormat.RFloat, false, true)
                {
                    hideFlags = HideFlags.HideAndDontSave,
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp,
                };
                var px = new Color[N * N];
                for (int y = 0; y < N; y++)
                {
                    float v = y / (float)(N - 1);
                    float worldY = rect.yMin + v * rect.height;
                    float wiggle = CoastWiggle(worldY);
                    for (int x = 0; x < N; x++)
                    {
                        float u = x / (float)(N - 1);
                        float worldX = rect.xMin + u * rect.width;
                        float shoreX = rect.xMin + 0.62f * rect.width + wiggle;
                        float elev = (worldX - shoreX) * BeachSlope;
                        float r = Mathf.InverseLerp(HeightMin, HeightMax, Mathf.Clamp(elev, HeightMin, HeightMax));
                        px[y * N + x] = new Color(r, 0f, 0f, 1f);
                    }
                }
                tex.SetPixels(px);
                tex.Apply(false, false);
                return tex;
            }

            static float CoastWiggle(float worldY)
                => 1.1f * Mathf.Sin(worldY * 0.9f) + 0.6f * Mathf.Sin(worldY * 2.3f + 1.7f);

            static Texture2D BuildBlackTexture()
            {
                var tex = new Texture2D(1, 1, TextureFormat.RFloat, false, true)
                {
                    hideFlags = HideFlags.HideAndDontSave,
                };
                tex.SetPixel(0, 0, Color.black);
                tex.Apply(false, false);
                return tex;
            }

            /// <summary>The exact still depth the shader reads at a rendered pixel (same rect
            /// mapping, same min/max, same wiggle) — the harness owns the bake, so no readback
            /// guesswork. Ignores the chop warp (zones only bucket pixels).</summary>
            public float StillDepthAtPixel(int x, int y)
            {
                float halfH = ViewHalfHeightMeters;
                float halfW = halfH * FrameW / FrameH;
                float worldX = -halfW + (x + 0.5f) / FrameW * (2f * halfW);
                float worldY = -halfH + (y + 0.5f) / FrameH * (2f * halfH);
                float shoreX = _rect.xMin + 0.62f * _rect.width + CoastWiggle(worldY);
                float elev = Mathf.Clamp((worldX - shoreX) * BeachSlope, HeightMin, HeightMax);
                return 0f - elev;
            }

            /// <summary>Zero every time-driven scroll/boil/shimmer so consecutive renders are
            /// byte-identical — the precondition of the imprint/footprint differences. The
            /// layers zeroed here are NOT the suspects (reflections/sky content die at shore
            /// anyway; the drift/scroll speeds only animate patterns).</summary>
            public void MakeStatic()
            {
                WaterMat.SetFloat("_Flow", 0f);
                WaterMat.SetFloat("_WindChopSpeed", 0f);
                WaterMat.SetFloat("_CrossSwellSpeed", 0f);
                WaterMat.SetFloat("_OceanSwellSpeed", 0f);
                WaterMat.SetFloat("_FbmDriftSpeed", 0f);
                WaterMat.SetFloat("_FoamEvolveSpeed", 0f);
                WaterMat.SetFloat("_SwashSpeed", 0f);
                WaterMat.SetFloat("_SwashAmplitude", 0f);
                WaterMat.SetFloat("_ReflectionStrength", 0f);
                WaterMat.SetFloat("_SkyReflectionStrength", 0f);
                WaterMat.SetFloat("_SpecAmount", 0f);
                WaterMat.SetFloat("_CausticAmount", 0f);
                WaterMat.SetFloat("_RainRingStrength", 0f);
                WaterMat.SetFloat("_DriftLineStrength", 0f);
                WaterMat.SetFloat("_StormFoamLaneStrength", 0f);
                // ⚠️ Added 2026-08-01, after three tests in this class were found FAILING ON MAIN at their
                // byte-identical-twice precondition — i.e. asserting nothing at all — since before the shore
                // work in this PR touched them.
                //
                // ⭐ THE ROOT CAUSE, and it is worth understanding rather than pattern-matching: zeroing the
                // hand-set speeds above STOPPED BEING SUFFICIENT. Every band's scroll rate is now
                //     rate = lerp(<the hand-set speed>, <a speed DERIVED from the dispersion relation>,
                //                 _DispersionScale)
                // and while _DispersionScale's own shader default is 0 — its label still reads "0 = today's
                // hand-set speeds EXACTLY" — #382 shipped it at 0.5 on Water.mat. At 0.5 the derived half of
                // that lerp is completely independent of the knobs this method zeroes, so the swell and the
                // cross-chop kept scrolling however hard we set their speeds to 0. Restoring the documented
                // passthrough is the whole fix; the two knobs below are genuinely-missing extras found while
                // chasing it.
                //   _DispersionScale      — THE one: 0 restores "the hand-set speeds are the speeds".
                //   _RippleSpeed          — the capillary ripple band scrolls with _Time.
                //   _WhitecapCollapseRate — the whitecap form/collapse lifecycle advances with _Time.
                // Anything NEW that animates off _Time must be added here, or the guards below silently stop
                // guarding rather than fail loudly — which is exactly what happened.
                WaterMat.SetFloat("_DispersionScale", 0f);
                WaterMat.SetFloat("_RippleSpeed", 0f);
                WaterMat.SetFloat("_WhitecapCollapseRate", 0f);
                // ⚠️ …and the ripple band's STRENGTH, not merely its speed — the lesson above, learned twice.
                // Zeroing _RippleSpeed freezes the ripple's CREST TRAVEL (the `- speed*t` inside its phase) and
                // nothing else. The very same line carries a SECOND time term the speed knob cannot reach:
                //     wander = ValueNoise(p * (RIPPLE_WANDER_FREQ / lambda) + t * RIPPLE_WANDER_DRIFT)
                // RIPPLE_WANDER_DRIFT is a hard-coded #define (0.05), NOT a uniform, so at _RippleSpeed = 0 the
                // crests stand still while the wander field keeps crawling underneath them and the phase keeps
                // wobbling. Only the layer's MASTER reaches it: at _RippleStrength = 0 the amplitude is exactly
                // 0 and the whole block is skipped — the shader's own documented passthrough contract.
                //
                // Why this survived #387, and why it broke only ONE of the two shore guards: the band is WIND-
                // gated (RippleWindGate = smoothstep(_RippleWindOnset 0.05, _RippleWindFull 0.45, _Roughness)),
                // and #382 shipped _RippleStrength at 0.45 on Water.mat over a shader default of 0. The bands
                // guard sets _Roughness = 0 to isolate the bands from foam, which slams that gate shut and kills
                // the crawl as a SIDE EFFECT; the swash guard leaves _Roughness at the reference sea's ~0.9,
                // which opens the gate fully. Hence one guard went green on the speed-only fix and the other
                // kept drifting. The drift is small (~200-400 px) only because _RippleBands = 3 POSTERIZES the
                // band: the crawl is invisible except on pixels sitting exactly astride a quantization step —
                // which is precisely why it read as "something small still animates" rather than a live layer.
                //
                // THE RULE, restated: zero the layer's STRENGTH, not its speed. A speed knob only proves ONE
                // time term is dead; the strength knob is what the shader guarantees is an exact passthrough.
                WaterMat.SetFloat("_RippleStrength", 0f);
                // …and the ADR 0027 #6 advected foam BUFFER, which is a different animal from everything
                // else here: not a scroll rate but a render target ADVECTED AND ACCUMULATED across frames,
                // so it cannot be static by construction no matter what speed it runs at. It ships at 0, but
                // a scene that turns it on would drift for a reason no "zero the speeds" reading of this
                // method would ever suggest.
                WaterMat.SetFloat("_WakeFoamStrength", 0f);
                // …and the four dials ADR 0040 revision 3 added (#699), every one of which animates off
                // _Time through the bore's own clock — the sheet born at each front and aged behind it,
                // the wet edge riding the run-up up the beach and draining, the bore face swinging under
                // the light, and the deposit, which is the _WakeFoamStrength animal again (a target
                // ADVECTED and ACCUMULATED across frames, static by no speed setting whatever).
                //
                // They ship at 0 on every water material today, so nothing here is drifting yet. That is
                // exactly why they go in now: the general law above ("anything NEW that animates off
                // _Time must be added here") was written after three guards were found silently not
                // guarding, and the day the owner turns these up is the day this method has to already
                // know about them.
                WaterMat.SetFloat("_SurfBeatStrength", 0f);
                WaterMat.SetFloat("_SurfRunUpStrength", 0f);
                WaterMat.SetFloat("_SurfFrontSlope", 0f);
                WaterMat.SetFloat("_SurfDepositStrength", 0f);
            }

            /// <summary>
            /// Wire the DISPLACED side exactly as DisplacedWaterSurface does in play (the
            /// HullWaterlineAcceptanceTests seam, water-only): a world-metre grid carrying a
            /// clone of the CURRENT water material (same uniforms) with the flat pass disabled,
            /// on the displaced rendering layer; the in-scene WaterOverlay quad; the registry
            /// registration + iso-depth frame. The flat quad is hidden — the A/B shows one side.
            /// </summary>
            public void AttachDisplaced()
            {
                var overlayShader = Shader.Find("HiddenHarbours/WaterOverlay");
                Assert.IsNotNull(overlayShader, "HiddenHarbours/WaterOverlay shader missing");

                _dispMat = new Material(WaterMat) { hideFlags = HideFlags.HideAndDontSave };
                _dispMat.SetShaderPassEnabled("Universal2D", false);   // off-screen pass only

                _gridMesh = BuildGrid(_rect, cell: 0.25f);
                _dispGo = new GameObject("DisplacedSea") { layer = ProbeLayer };
                _dispGo.AddComponent<MeshFilter>().sharedMesh = _gridMesh;
                var mr = _dispGo.AddComponent<MeshRenderer>();
                mr.sharedMaterial = _dispMat;
                mr.shadowCastingMode = ShadowCastingMode.Off;
                mr.receiveShadows = false;
                mr.lightProbeUsage = LightProbeUsage.Off;
                mr.renderingLayerMask = DisplacedWaterRegistry.RenderingLayer;

                _overlayQuad = new Mesh { name = "WaterOverlayQuad" };
                _overlayQuad.SetVertices(new[]
                {
                    new Vector3(_rect.xMin, _rect.yMin, 0f), new Vector3(_rect.xMax, _rect.yMin, 0f),
                    new Vector3(_rect.xMax, _rect.yMax, 0f), new Vector3(_rect.xMin, _rect.yMax, 0f),
                });
                _overlayQuad.SetTriangles(new[] { 0, 1, 2, 0, 2, 3 }, 0);
                _overlayMat = new Material(overlayShader) { hideFlags = HideFlags.HideAndDontSave };
                _overlayGo = new GameObject("WaterOverlay") { layer = ProbeLayer };
                _overlayGo.AddComponent<MeshFilter>().sharedMesh = _overlayQuad;
                var omr = _overlayGo.AddComponent<MeshRenderer>();
                omr.sharedMaterial = _overlayMat;
                omr.shadowCastingMode = ShadowCastingMode.Off;
                omr.receiveShadows = false;
                omr.lightProbeUsage = LightProbeUsage.Off;
                var group = _overlayGo.AddComponent<SortingGroup>();
                group.sortingOrder = -10;
                omr.sortingOrder = -10;

                _surface = _dispGo.AddComponent<DisplacedWaterSurface>();
                DisplacedWaterRegistry.Register(_surface);
                Vector4 isoDepth = _dispMat.GetVector("_WaterIsoDepth");
                Vector4 heightMin = _dispMat.GetVector("_HeightWorldMin");
                DisplacedWaterRegistry.PublishIsoDepthFrame(_surface,
                    new WaterIsoDepthFrame(heightMin.y, isoDepth.x, isoDepth.y,
                                           _dispGo.transform.position.z));

                _waterGo.SetActive(false);   // the A/B: exactly one side renders
                _warm = false;               // the HHWater/overlay variants may need compiling
            }

            public void DetachDisplaced()
            {
                if (_surface != null) DisplacedWaterRegistry.Unregister(_surface);
                _surface = null;
                if (_dispGo != null) Object.DestroyImmediate(_dispGo);
                if (_overlayGo != null) Object.DestroyImmediate(_overlayGo);
                if (_dispMat != null) Object.DestroyImmediate(_dispMat);
                if (_overlayMat != null) Object.DestroyImmediate(_overlayMat);
                if (_gridMesh != null) Object.DestroyImmediate(_gridMesh);
                if (_overlayQuad != null) Object.DestroyImmediate(_overlayQuad);
                _waterGo.SetActive(true);
            }

            static Mesh BuildGrid(Rect rect, float cell)
            {
                int nx = Mathf.Max(1, Mathf.CeilToInt(rect.width / cell));
                int ny = Mathf.Max(1, Mathf.CeilToInt(rect.height / cell));
                var verts = new Vector3[(nx + 1) * (ny + 1)];
                for (int j = 0; j <= ny; j++)
                    for (int i = 0; i <= nx; i++)
                        verts[j * (nx + 1) + i] = new Vector3(
                            rect.xMin + rect.width * (i / (float)nx),
                            rect.yMin + rect.height * (j / (float)ny), 0f);
                var tris = new int[nx * ny * 6];
                int t = 0;
                for (int j = 0; j < ny; j++)
                    for (int i = 0; i < nx; i++)
                    {
                        int a = j * (nx + 1) + i, b = a + 1, cIdx = a + nx + 1, d = cIdx + 1;
                        tris[t++] = a; tris[t++] = cIdx; tris[t++] = b;
                        tris[t++] = b; tris[t++] = cIdx; tris[t++] = d;
                    }
                var mesh = new Mesh { indexFormat = IndexFormat.UInt32, name = "DisplacedSeaGrid" };
                mesh.SetVertices(verts);
                mesh.SetTriangles(tris, 0);
                mesh.RecalculateBounds();
                Bounds bnds = mesh.bounds;
                bnds.Expand(8f);   // lifted crests must not be frustum-culled off the flat rect
                mesh.bounds = bnds;
                return mesh;
            }

            public Color[] Render()
            {
                EnsureVariantsCompiled();
                _cam.Render();
                var prev = RenderTexture.active;
                RenderTexture.active = _rt;
                var tex = new Texture2D(FrameW, FrameH, TextureFormat.RGBAFloat, false, true);
                tex.ReadPixels(new Rect(0, 0, FrameW, FrameH), 0, 0);
                tex.Apply();
                RenderTexture.active = prev;
                Color[] px = tex.GetPixels();
                Object.DestroyImmediate(tex);
                return px;
            }

            void EnsureVariantsCompiled()
            {
                if (_warm) return;
                const double timeoutSeconds = 180.0;
                const int maxWarmUps = 10;
                var clock = Stopwatch.StartNew();
                int renders = 0;
                for (; renders < maxWarmUps; renders++)
                {
                    _cam.Render();
                    if (!ShaderUtil.anythingCompiling) break;
                    while (ShaderUtil.anythingCompiling && clock.Elapsed.TotalSeconds < timeoutSeconds)
                        Thread.Sleep(25);
                }
                if (ShaderUtil.anythingCompiling || renders >= maxWarmUps)
                    Assert.Fail(
                        "SHADERS NEVER FINISHED COMPILING — this is NOT a water regression. " +
                        $"After {renders} warm-up render(s) and {clock.Elapsed.TotalSeconds:F1}s " +
                        "the compiler was still busy; a measuring render would land on the async " +
                        "placeholder (the cold-cache trap). Re-run with a warm cache.");
                _warm = true;
            }

            public void Dispose()
            {
                DetachDisplaced();
                RenderTexture.active = null;
                if (_cam != null) _cam.targetTexture = null;
                if (_camGo != null) Object.DestroyImmediate(_camGo);
                if (_waterGo != null) Object.DestroyImmediate(_waterGo);
                if (_rt != null) { _rt.Release(); Object.DestroyImmediate(_rt); }
                if (WaterMat != null) Object.DestroyImmediate(WaterMat);
                if (_quad != null) Object.DestroyImmediate(_quad);
                if (_heightTex != null) Object.DestroyImmediate(_heightTex);
            }
        }
    }
}
