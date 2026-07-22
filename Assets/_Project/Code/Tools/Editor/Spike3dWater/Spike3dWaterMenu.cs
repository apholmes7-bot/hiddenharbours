// ⚠️⚠️ THROWAWAY SPIKE CODE — spike/3d-water. NOT A PIPELINE. NOT FOR MERGE AS-IS. ⚠️⚠️
// Produces the images that answer: "does 3D-displaced water make wave formations READABLE?"
// (the owner's ask: 'visually SEE the wave formations and tell larger waves — currently it's
// hard to tell' — a P1 legibility question, not a looks question).
using System;
using System.Globalization;
using System.IO;
using System.Text;
using HiddenHarbours.Core;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.Tools.Spike3dWater
{
    public static class Spike3dWaterMenu
    {
        static string OutDir =>
            Environment.GetEnvironmentVariable("HH_SPIKE_OUT") ??
            Path.Combine(Path.GetTempPath(), "hh3dwater");

        // The A/B viewport: 30 x 16.875 m = 960 x 540 px at 32 px/m — a production-like framing.
        const int W = 960, H = 540;
        static readonly Rect View = new Rect(-15f, -8.4375f, 30f, 16.875f);
        const float GridCell = 0.125f;          // 4 px per grid cell at 32 px/m

        [MenuItem("Hidden Harbours/Spikes/3D Water — run all")]
        public static void RunAll()
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            Directory.CreateDirectory(OutDir);
            var log = new StringBuilder();
            try
            {
                log.AppendLine($"device: {SystemInfo.graphicsDeviceName} ({SystemInfo.graphicsDeviceType})");
                using var ren = new SpikeWaterRenderer();
                log.AppendLine(ren.CalibrateDepth());
                log.AppendLine($"palette from Water.mat: deep={ren.PaletteDeep} mid={ren.PaletteMid} " +
                               $"shallow={ren.PaletteShallow} foam={ren.PaletteFoam} light={ren.LightDir2D}");
                log.AppendLine($"hull def: {ren.HullDef.Id} elev={ren.HullDef.ElevationDeg} " +
                               $"ppm={ren.HullDef.PxPerMetre} ccw={ren.HullDef.AzimuthCounterClockwise}");

                // ---- the deterministic sea --------------------------------------------------------
                WaveTrains trains = SpikeWaveScenario.Trains();
                float totalAmp = trains.TotalAmplitude;
                log.AppendLine($"\nscenario: wind={SpikeWaveScenario.Wind} ({SpikeWaveScenario.Wind.magnitude:0.00} m/s) " +
                               $"seaState={SpikeWaveScenario.SeaState} -> {trains.Count} trains, " +
                               $"totalAmp={totalAmp:0.000} m, sharpening={trains.CrestSharpening}");
                for (int i = 0; i < trains.Count; i++)
                {
                    WaveTrain t = trains[i];
                    log.AppendLine($"  train{i}: dir=({t.Direction.x:0.000},{t.Direction.y:0.000}) " +
                                   $"lambda={t.Wavelength:0.00} m amp={t.Amplitude:0.000} m c={t.PhaseSpeed:0.00} m/s");
                }

                // Warm the shader cache before ANY timed/believed render (the cold-cache trap).
                WarmUp(ren, trains, log);

                // ---- find the big one (deterministic scan, not authored) --------------------------
                var scanRect = Rect.MinMaxRect(View.xMin + 2f, View.yMin + 2f, View.xMax - 2f, View.yMax - 2f);
                SpikeWaveScenario.FindBigWave(in trains, scanRect, 0.0, 1800.0, 0.5, 0.5f,
                    out double bigT, out Vector2 bigPos, out float bigH, out float rmsCrest);
                log.AppendLine($"\nbig wave: t={bigT:0.0} s at ({bigPos.x:0.0},{bigPos.y:0.0}) " +
                               $"h={bigH:0.000} m ({bigH / totalAmp:P0} of envelope); " +
                               $"rms in-view frame max over the window = {rmsCrest:0.000} m " +
                               $"(the big one is x{bigH / Mathf.Max(rmsCrest, 1e-4f):0.00} the typical tallest)");

                // ---- A/B stills at the big-wave moment --------------------------------------------
                Stills(ren, trains, bigT, bigPos, totalAmp, log);

                // ---- the approach sequence (watch it come) ----------------------------------------
                Sequence(ren, trains, bigT, totalAmp, log);

                // ---- the waterline-on-hull probe --------------------------------------------------
                Probe(ren, trains, bigT, totalAmp, log);

                // ---- honest perf ballpark ---------------------------------------------------------
                Perf(ren, trains, bigT, totalAmp, log);
            }
            catch (Exception e)
            {
                log.AppendLine("FAILED: " + e);
            }
            File.WriteAllText(Path.Combine(OutDir, "spike-log.txt"), log.ToString());
            Debug.Log("[3D WATER SPIKE]\n" + log);
        }

        static void WarmUp(SpikeWaterRenderer ren, in WaveTrains trains, StringBuilder log)
        {
            PublishGlobals(in trains, 0.0);
            var small = new Rect(-2f, -1f, 4f, 2f);
            ren.RenderProduction(small, 64, 32, SpikeWaveScenario.SeaState, SpikeWaveScenario.Wind, 0.0, out _);
            ren.RenderDisplaced(small, 64, 32, 1f, Vector2.one, Vector2.zero, 0.25f,
                                trains.TotalAmplitude, out _, out _);
            ren.RenderProbe(small, 64, 32, Vector2.zero, 2f, 0.5f, 1f, 0.25f,
                            trains.TotalAmplitude, out _);
            log.AppendLine("shader cache warmed (production water + spike + facet each rendered once)");
        }

        static void PublishGlobals(in WaveTrains trains, double t)
        {
            SpikeWaveScenario.PackAtTime(in trains, t,
                out Vector4 t0, out Vector4 t1, out Vector4 t2, out Vector4 t3,
                out Vector4 phases, out Vector4 fieldParams);
            Shader.SetGlobalVector("_WaveTrain0", t0);
            Shader.SetGlobalVector("_WaveTrain1", t1);
            Shader.SetGlobalVector("_WaveTrain2", t2);
            Shader.SetGlobalVector("_WaveTrain3", t3);
            Shader.SetGlobalVector("_WavePhases", phases);
            Shader.SetGlobalVector("_WaveFieldParams", fieldParams);
            // The "unset" convention for everything else (cycle off, no boat light, no moon): the
            // production water reads full-day fallbacks, exactly like a bare art scene.
            Shader.SetGlobalVector("_DayNightTint", Vector4.zero);
            Shader.SetGlobalVector("_SunDir", Vector4.zero);
            Shader.SetGlobalVector("_MoonDir", Vector4.zero);
            Shader.SetGlobalVector("_MoonPhaseState", Vector4.zero);
            Shader.SetGlobalVector("_BoatLightParams", Vector4.zero);
            Shader.SetGlobalVector("_WindWorld", new Vector4(
                SpikeWaveScenario.Wind.normalized.x, SpikeWaveScenario.Wind.normalized.y,
                Mathf.Clamp01(SpikeWaveScenario.Wind.magnitude / 12f), 0f));
        }

        static (int x, int y) ToPixel(Rect view, Vector2 world, float lift)
        {
            int px = Mathf.RoundToInt((world.x - view.xMin) * SpikeWaterRenderer.PPU);
            int py = Mathf.RoundToInt((view.yMax - (world.y + lift)) * SpikeWaterRenderer.PPU);
            return (px, py);
        }

        static void Stills(SpikeWaterRenderer ren, in WaveTrains trains, double bigT, Vector2 bigPos,
                           float totalAmp, StringBuilder log)
        {
            PublishGlobals(in trains, bigT);
            log.AppendLine("\n-- A/B stills at the big-wave moment --");

            Color32[] a = ren.RenderProduction(View, W, H, SpikeWaveScenario.SeaState,
                                               SpikeWaveScenario.Wind, bigT, out double msA);
            SpikeWaterRenderer.WritePng(Path.Combine(OutDir, "still_A_production.png"), a, W, H);
            (int ax, int ay) = ToPixel(View, bigPos, 0f);
            SpikeWaterRenderer.WritePng(Path.Combine(OutDir, "still_A_production_annotated.png"),
                SpikeWaterRenderer.Annotate(a, W, H, ax, ay), W, H);
            log.AppendLine($"A production: {msA:0.00} ms  big wave at px ({ax},{ay})");

            // Control: the water shader at its own DEFAULTS (no owner-painted textures) — separates
            // "the owner's tuned look" from "the shader's procedural look" in the A-side reading.
            Color32[] a2 = ren.RenderProduction(View, W, H, SpikeWaveScenario.SeaState,
                                                SpikeWaveScenario.Wind, bigT, out _, useDefaultMaterial: true);
            SpikeWaterRenderer.WritePng(Path.Combine(OutDir, "still_A_production_defaults.png"), a2, W, H);

            float bigH = SpikeWaveScenario.Sample(in trains, bigPos, bigT).Height;
            foreach (float s in new[] { 1f, 1.5f, 2f, 3f })
            {
                Color32[] b = ren.RenderDisplaced(View, W, H, s, Vector2.one, View.center,
                                                  GridCell, totalAmp, out double msB, out int verts);
                string tag = s.ToString("0.0", CultureInfo.InvariantCulture).Replace('.', 'p');
                SpikeWaterRenderer.WritePng(Path.Combine(OutDir, $"still_B_displaced_x{tag}.png"), b, W, H);
                (int bx, int by) = ToPixel(View, bigPos, bigH * s);
                SpikeWaterRenderer.WritePng(Path.Combine(OutDir, $"still_B_displaced_x{tag}_annotated.png"),
                    SpikeWaterRenderer.Annotate(b, W, H, bx, by), W, H);
                Color32[] sbs = SpikeWaterRenderer.SideBySide(a, b, W, H, out int sbsW);
                SpikeWaterRenderer.WritePng(Path.Combine(OutDir, $"still_AB_x{tag}.png"), sbs, sbsW, H);
                log.AppendLine($"B displaced x{s:0.0}: {msB:0.00} ms, {verts} grid verts");
            }
        }

        static void Sequence(SpikeWaterRenderer ren, in WaveTrains trains, double bigT,
                             float totalAmp, StringBuilder log)
        {
            const int NF = 40;
            const double Dt = 0.35;
            double tStart = bigT - Dt * (NF - 8);   // the big one arrives near the end
            log.AppendLine($"\n-- approach sequence: {NF} frames, dt={Dt} s, t={tStart:0.0}..{tStart + Dt * (NF - 1):0.0} --");

            foreach (string d in new[] { "frames/A_production", "frames/B_x1p0", "frames/B_x2p0" })
                Directory.CreateDirectory(Path.Combine(OutDir, d));

            var stripsA = new System.Collections.Generic.List<Color32[]>();
            var stripsB1 = new System.Collections.Generic.List<Color32[]>();
            var stripsB2 = new System.Collections.Generic.List<Color32[]>();

            for (int f = 0; f < NF; f++)
            {
                double t = tStart + f * Dt;
                PublishGlobals(in trains, t);
                Color32[] a = ren.RenderProduction(View, W, H, SpikeWaveScenario.SeaState,
                                                   SpikeWaveScenario.Wind, t, out _);
                Color32[] b1 = ren.RenderDisplaced(View, W, H, 1f, Vector2.one, View.center,
                                                   GridCell, totalAmp, out _, out _);
                Color32[] b2 = ren.RenderDisplaced(View, W, H, 2f, Vector2.one, View.center,
                                                   GridCell, totalAmp, out _, out _);
                SpikeWaterRenderer.WritePng(Path.Combine(OutDir, $"frames/A_production/f{f:00}.png"), a, W, H);
                SpikeWaterRenderer.WritePng(Path.Combine(OutDir, $"frames/B_x1p0/f{f:00}.png"), b1, W, H);
                SpikeWaterRenderer.WritePng(Path.Combine(OutDir, $"frames/B_x2p0/f{f:00}.png"), b2, W, H);
                if (f % 6 == 3)
                {
                    stripsA.Add(Downscale(a, W, H, 3));
                    stripsB1.Add(Downscale(b1, W, H, 3));
                    stripsB2.Add(Downscale(b2, W, H, 3));
                }
            }

            WriteStrip(Path.Combine(OutDir, "filmstrip_A_production.png"), stripsA, W / 3, H / 3);
            WriteStrip(Path.Combine(OutDir, "filmstrip_B_x1p0.png"), stripsB1, W / 3, H / 3);
            WriteStrip(Path.Combine(OutDir, "filmstrip_B_x2p0.png"), stripsB2, W / 3, H / 3);
            log.AppendLine("frames + filmstrips written");
        }

        static void Probe(SpikeWaterRenderer ren, in WaveTrains trains, double bigT,
                          float totalAmp, StringBuilder log)
        {
            // Broadside lobster boat, fixed in place (NO heave compensation — that is the point:
            // the WATER moves on the HULL), sunk half a metre of draft, sea at scale 1.
            var view = new Rect(-11.25f, -6.5f, 22.5f, 15f);   // 720 x 480 px
            const int PW = 720, PH = 480;
            var hullPos = Vector2.zero;
            const float dirUnits = 2f;      // rig dir 2 = a broadside facing (visual choice only)
            const float draft = 0.5f;

            log.AppendLine("\n-- waterline-on-hull probe (rig-true iso frame, shared z-buffer) --");
            double period = 22.17 / 5.88;   // dominant train, logged above; framing only
            const int NF = 12;
            Directory.CreateDirectory(Path.Combine(OutDir, "frames/probe_fixed"));
            Directory.CreateDirectory(Path.Combine(OutDir, "frames/probe_riding"));
            double msSum = 0;
            for (int f = 0; f < NF; f++)
            {
                double t = bigT + f * (period / NF);
                PublishGlobals(in trains, t);

                // (1) hull FIXED: the waterline visibly climbs and falls on the planking.
                Color32[] p = ren.RenderProbe(view, PW, PH, hullPos, dirUnits, draft, 1f,
                                              0.0625f, totalAmp, out double ms);
                msSum += ms;
                SpikeWaterRenderer.WritePng(Path.Combine(OutDir, $"frames/probe_fixed/f{f:00}.png"), p, PW, PH);
                if (f == 0)
                    SpikeWaterRenderer.WritePng(Path.Combine(OutDir, "probe_waterline_on_hull.png"),
                                                p, PW, PH, 2);

                // (2) hull RIDING the swell (rig-z lifted by the wave height under the pivot — the
                // 3D composition of the existing see==feel heave): the waterline stays near the
                // planking mark while boat AND sea move together.
                float hHull = SpikeWaveScenario.Sample(in trains, hullPos, t).Height;
                Color32[] p2 = ren.RenderProbe(view, PW, PH, hullPos, dirUnits, draft - hHull, 1f,
                                               0.0625f, totalAmp, out _);
                SpikeWaterRenderer.WritePng(Path.Combine(OutDir, $"frames/probe_riding/f{f:00}.png"), p2, PW, PH);
                if (f == 0)
                    SpikeWaterRenderer.WritePng(Path.Combine(OutDir, "probe_riding_hull.png"),
                                                p2, PW, PH, 2);
            }
            log.AppendLine($"probe: {NF} frames over one dominant period, avg submit {msSum / NF:0.00} ms; " +
                           "fixed variant = waterline climbs the hull; riding variant = hull heaves with the field");
        }

        static void Perf(SpikeWaterRenderer ren, in WaveTrains trains, double bigT,
                         float totalAmp, StringBuilder log)
        {
            log.AppendLine("\n-- perf ballpark (960x540, CommandBuffer submit+flush ms, 5 reps each; " +
                           "crude CPU-side timing, see verdict caveats) --");
            PublishGlobals(in trains, bigT);
            foreach (float cell in new[] { 0.5f, 0.25f, 0.125f, 0.0625f })
            {
                double best = double.MaxValue, sum = 0;
                int verts = 0;
                for (int r = 0; r < 5; r++)
                {
                    ren.RenderDisplaced(View, W, H, 1f, Vector2.one, View.center, cell,
                                        totalAmp, out double ms, out verts);
                    best = Math.Min(best, ms);
                    sum += ms;
                }
                log.AppendLine($"grid {cell:0.0000} m ({cell * SpikeWaterRenderer.PPU:0.#} px): " +
                               $"{verts} verts, {verts * 2 / 1000}k tris approx, " +
                               $"best {best:0.00} ms, avg {sum / 5:0.00} ms");
            }
        }

        static Color32[] Downscale(Color32[] px, int w, int h, int f)
        {
            int W2 = w / f, H2 = h / f;
            var outPx = new Color32[W2 * H2];
            for (int y = 0; y < H2; y++)
                for (int x = 0; x < W2; x++)
                    outPx[y * W2 + x] = px[(y * f) * w + x * f];
            return outPx;
        }

        static void WriteStrip(string path, System.Collections.Generic.List<Color32[]> tiles, int w, int h)
        {
            if (tiles.Count == 0) return;
            const int gap = 2;
            int outW = tiles.Count * w + (tiles.Count - 1) * gap;
            var outPx = new Color32[outW * h];
            for (int i = 0; i < tiles.Count; i++)
            {
                int x0 = i * (w + gap);
                for (int y = 0; y < h; y++)
                    Array.Copy(tiles[i], y * w, outPx, y * outW + x0, w);
            }
            SpikeWaterRenderer.WritePng(path, outPx, outW, h);
        }
    }
}
