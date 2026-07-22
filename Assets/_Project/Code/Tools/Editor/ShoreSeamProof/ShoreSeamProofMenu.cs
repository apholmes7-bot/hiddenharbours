// SHORE-SEAM PROOF HARNESS (ADR 0023) — editor-only evidence tooling, never shipped in a build.
// Produces the images + numbers that answer: "does displacement reach EXACTLY zero at the walkable
// waterline, at every tide, without tearing the coast — while the open-sea readability event still
// reads at full exaggeration?" Deterministic throughout (rule 5): the spike's reference sea, fixed
// times, no RNG, no wall clock. Evidence lands in Evidence~/ next to this file (the spike pattern;
// Unity ignores ~ folders) and every number is written to proof-log.txt.
using System;
using System.Globalization;
using System.IO;
using System.Text;
using HiddenHarbours.Core;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.Tools.Editor
{
    public static class ShoreSeamProofMenu
    {
        // The 3D-water spike's deterministic scenario (branch spike/3d-water, VERDICT.md): the
        // reference sea every displaced-water number is quoted against. Envelope ≈ 1.047 m.
        private static readonly Vector2 Wind = new Vector2(-5.4f, -9.33f);
        private const float SeaState = 0.75f;
        private const double EventTime = 1513.5;                    // the 100%-envelope moment
        private static readonly Vector2 EventPos = new Vector2(-6.5f, 2.1f);
        private const float Exaggeration = 1.5f;                    // the ADR 0023 sweet spot
        private const double DominantPeriod = 3.77;                 // λ 22.17 m / c 5.88 m/s

        private const int W = 480, H = 270;                          // 15 x 8.4375 m at 32 px/m
        private static readonly float[] Tides = { -0.6f, 0f, 0.75f };

        private static string OutDir => Path.Combine(Application.dataPath,
            "_Project/Code/Tools/Editor/ShoreSeamProof/Evidence~");

        [MenuItem("Hidden Harbours/Proofs/Shore Seam (ADR 0023) — run all")]
        public static void RunAll()
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            Directory.CreateDirectory(OutDir);
            var log = new StringBuilder();
            bool failed = false;
            try
            {
                log.AppendLine($"device: {SystemInfo.graphicsDeviceName} ({SystemInfo.graphicsDeviceType})");
                using var ren = new ShoreSeamRenderer();
                log.AppendLine(ren.CalibrateDepth());

                WaveTrains trains = WaveMath.TrainsFrom(Wind, SeaState, WaveFieldSettings.Default);
                float envelope = trains.TotalAmplitude;
                float eventH = WaveMath.Sample(EventPos, EventTime, in trains).Height;
                log.AppendLine($"scenario: wind={Wind} seaState={SeaState} -> {trains.Count} trains, " +
                               $"envelope={envelope:0.000} m, sharpening={trains.CrestSharpening}");
                log.AppendLine($"event: t={EventTime} s at ({EventPos.x},{EventPos.y}) " +
                               $"h={eventH:0.000} m ({eventH / envelope:P1} of envelope)");
                log.AppendLine($"exaggeration under proof: x{Exaggeration} (the spike sweet spot)");

                // Warm the shader cache before ANY believed render (the cold-cache trap).
                ShoreSeamRenderer.PublishGlobals(in trains, 0.0);
                var beachWarm = new ShoreProfile("warmup", 0f, 0.15f);
                ren.Render(new Rect(-2f, -2f, 4f, 4f), 64, 64, in beachWarm, 0f, 0.5f,
                           Exaggeration, envelope, seamEnabled: true, maskMode: false);
                log.AppendLine("shader cache warmed");

                // ---- (1)+(2) the seam under proof: boundary vs contour, all shores, all tides ----
                var profiles = new[]
                {
                    new ShoreProfile("beach_north", 0f, 0.15f),
                    new ShoreProfile("flats_north", 0f, 0.05f),
                    new ShoreProfile("steep_north", 0f, 0.50f),
                    new ShoreProfile("beach_south", 0f, -0.15f),
                };

                log.AppendLine("\n-- seam ON: rendered water/land boundary vs the analytic waterline " +
                               "contour (tear = water past the contour toward land; gap = water edge " +
                               "short of the contour). PASS bar: |dev| <= 1 px (sub-pixel rasterization) --");
                foreach (ShoreProfile profile in profiles)
                {
                    float band = ShoreFadeMath.RecommendedBandMeters(
                        envelope, Exaggeration, Mathf.Abs(profile.Gradient));
                    foreach (float tide in Tides)
                    {
                        (int tear, int gap) = SweepBoundary(ren, in trains, in profile, tide, band,
                                                            envelope, seamEnabled: true,
                                                            out _, out _);
                        bool pass = tear <= 1 && gap <= 1;
                        failed |= !pass;
                        log.AppendLine($"  {profile.Name,-12} grad={profile.Gradient,5:+0.00;-0.00} " +
                                       $"tide={tide,5:+0.00;-0.00} band={band:0.000} m : " +
                                       $"maxTear={tear,3} px, maxGap={gap,3} px over 12 frames -> " +
                                       (pass ? "PASS" : "FAIL"));
                    }
                }

                // ---- the CONTROL: seam OFF (the naive port) — the tear made visible + numeric ----
                var beach = profiles[0];
                float beachBand = ShoreFadeMath.RecommendedBandMeters(envelope, Exaggeration, beach.Gradient);
                (int tearOff, int gapOff) = SweepBoundary(ren, in trains, in beach, 0f, beachBand,
                                                          envelope, seamEnabled: false,
                                                          out double tearFrameT, out double gapFrameT);
                log.AppendLine($"\n-- seam OFF control (beach_north, tide 0): maxTear={tearOff} px " +
                               $"(t={tearFrameT:0.00}), maxGap={gapOff} px (t={gapFrameT:0.00}) — " +
                               "the coast tears without the seam --");
                if (tearOff <= 2 && gapOff <= 2)
                {
                    failed = true;
                    log.AppendLine("  UNEXPECTED: the control did not tear — the measurement is not sensitive.");
                }

                // Beauty A/Bs at the control's worst frames: seam on vs seam off.
                WriteAb(ren, in trains, in beach, 0f, beachBand, envelope, tearFrameT,
                        Path.Combine(OutDir, "ab_beach_crest_seamOn_vs_off.png"));
                WriteAb(ren, in trains, in beach, 0f, beachBand, envelope, gapFrameT,
                        Path.Combine(OutDir, "ab_beach_trough_seamOn_vs_off.png"));

                // Beauty stills: the seam at three tides (the contour MOVES; the seam follows).
                foreach (float tide in Tides)
                    WriteBeauty(ren, in trains, in beach, tide, beachBand, envelope, EventTime,
                                Path.Combine(OutDir, $"beach_seamOn_tide{tide:+0.00;-0.00}.png"));
                float steepBand = ShoreFadeMath.RecommendedBandMeters(envelope, Exaggeration, 0.5f);
                WriteBeauty(ren, in trains, in profiles[2], 0f, steepBand, envelope, EventTime,
                            Path.Combine(OutDir, "steep_seamOn_tide+0.00.png"));
                WriteBeauty(ren, in trains, in profiles[3], 0f, beachBand, envelope, EventTime,
                            Path.Combine(OutDir, "south_seamOn_tide+0.00.png"));

                // ---- (3) open sea: the seam machinery must be pixel-invisible offshore -----------
                var openSea = new ShoreProfile("open_sea", -10f, 1e-4f);
                ShoreSeamRenderer.PublishGlobals(in trains, EventTime);
                var openView = new Rect(EventPos.x - 15f, EventPos.y - 8.4375f, 30f, 16.875f);
                Color32[] on = ren.Render(openView, 960, 540, in openSea, 0f, 1f, Exaggeration,
                                          envelope, seamEnabled: true, maskMode: false);
                Color32[] off = ren.Render(openView, 960, 540, in openSea, 0f, 1f, Exaggeration,
                                           envelope, seamEnabled: false, maskMode: false);
                int diff = ShoreSeamRenderer.DiffCount(on, off);
                failed |= diff != 0;
                log.AppendLine($"\n-- open sea (uniform deep, the envelope-event moment, x{Exaggeration}): " +
                               $"seam ON vs seam OFF differing pixels = {diff} of {960 * 540} -> " +
                               (diff == 0 ? "PASS (bit-invisible offshore)" : "FAIL"));
                ShoreSeamRenderer.WritePng(Path.Combine(OutDir, "opensea_event_x1p5_seamOn.png"), on, 960, 540);

                log.AppendLine($"\nRESULT: {(failed ? "FAIL" : "PASS")}");
            }
            catch (Exception e)
            {
                failed = true;
                log.AppendLine("FAILED: " + e);
            }
            File.WriteAllText(Path.Combine(OutDir, "proof-log.txt"), log.ToString());
            Debug.Log("[SHORE SEAM PROOF]\n" + log);
            if (Application.isBatchMode)
                EditorApplication.Exit(failed ? 1 : 0);
        }

        /// <summary>12 mask frames over one dominant period ending on the envelope event; returns
        /// the worst tear/gap in px and the frame times at which they occurred.</summary>
        private static (int tear, int gap) SweepBoundary(ShoreSeamRenderer ren, in WaveTrains trains,
                                                         in ShoreProfile profile, float tide,
                                                         float band, float envelope, bool seamEnabled,
                                                         out double tearT, out double gapT)
        {
            Rect view = ViewFor(in profile, tide);
            int maxTear = int.MinValue, maxGap = int.MinValue;
            tearT = gapT = EventTime;
            for (int j = 0; j < 12; j++)
            {
                double t = EventTime - DominantPeriod + j * (DominantPeriod / 11.0);
                ShoreSeamRenderer.PublishGlobals(in trains, t);
                Color32[] px = ren.Render(view, W, H, in profile, tide, band, Exaggeration,
                                          envelope, seamEnabled, maskMode: true);
                BoundaryStats s = ShoreSeamRenderer.MeasureBoundary(px, W, H, view, in profile, tide);
                if (s.Columns == 0) continue;
                if (s.MaxTearPx > maxTear) { maxTear = s.MaxTearPx; tearT = t; }
                if (s.MaxGapPx > maxGap) { maxGap = s.MaxGapPx; gapT = t; }
            }
            return (maxTear, maxGap);
        }

        /// <summary>Frame the shore so the contour sits ~2 m from the view's land-side edge.</summary>
        private static Rect ViewFor(in ShoreProfile profile, float tide)
        {
            float contourY = profile.WaterlineY(tide);
            float centreY = profile.Gradient > 0f ? contourY - 2f : contourY + 2f;
            return new Rect(EventPos.x - 7.5f, centreY - 4.21875f, 15f, 8.4375f);
        }

        private static void WriteBeauty(ShoreSeamRenderer ren, in WaveTrains trains,
                                        in ShoreProfile profile, float tide, float band,
                                        float envelope, double t, string path)
        {
            ShoreSeamRenderer.PublishGlobals(in trains, t);
            Color32[] px = ren.Render(ViewFor(in profile, tide), W, H, in profile, tide, band,
                                      Exaggeration, envelope, seamEnabled: true, maskMode: false);
            ShoreSeamRenderer.WritePng(path, px, W, H);
        }

        private static void WriteAb(ShoreSeamRenderer ren, in WaveTrains trains,
                                    in ShoreProfile profile, float tide, float band,
                                    float envelope, double t, string path)
        {
            Rect view = ViewFor(in profile, tide);
            ShoreSeamRenderer.PublishGlobals(in trains, t);
            Color32[] a = ren.Render(view, W, H, in profile, tide, band, Exaggeration,
                                     envelope, seamEnabled: true, maskMode: false);
            Color32[] b = ren.Render(view, W, H, in profile, tide, band, Exaggeration,
                                     envelope, seamEnabled: false, maskMode: false);
            Color32[] ab = ShoreSeamRenderer.SideBySide(a, b, W, H, out int outW);
            ShoreSeamRenderer.WritePng(path, ab, outW, H);
        }
    }
}
