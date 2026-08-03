using System.Collections.Generic;
using System.Diagnostics;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Core;
using Debug = UnityEngine.Debug;

namespace HiddenHarbours.UI.Editor
{
    /// <summary>
    /// MEASURES what one fish-finder repaint costs (ADR 0025 S3b, rule 7). The finder is the one rig ADR
    /// 0025 says must be a live renderer, and its scan is O(sonar width) in <c>fillRect</c> spans plus a
    /// whole-texture upload — so "is Option A affordable, or does this rig want Option B's editor bake?"
    /// is a question that has to be answered with a NUMBER, not an estimate.
    ///
    /// <para>Reports raster-only and raster+upload at both shipped sizes. Run it from the menu, or
    /// headless with <c>-executeMethod HiddenHarbours.UI.Editor.FishFinderRepaintBench.Measure</c>, and
    /// read <c>[fish-finder-bench]</c> out of the log.</para>
    /// </summary>
    public static class FishFinderRepaintBench
    {
        private const int Warmup = 20;
        private const int Iterations = 200;

        [MenuItem("Hidden Harbours/Dev/Measure Fish Finder Repaint (ms per raster)")]
        public static void Measure()
        {
            FishFinderSettings cfg = FishFinderSettings.Default;
            // The shipped surface: ONE native render for BOTH states — the card is a scaled blit of it,
            // so card and focused cost exactly the same raster. Measured once and reported as both.
            Bench("card AND focused (the shipped native surface)", FishRigRender.W, FishRigRender.H);
            // The rejected alternative, for comparison: rendering the card at half resolution would be
            // cheaper but breaks the rig's own typography (see FishFinderOverlayLayout).
            Bench("half-res card (REJECTED — typography breaks)", FishRigRender.W / 2, FishRigRender.H / 2);
            // What S6 would pay for a flush brow mount.
            Bench("300x120 console brow (S6)", 300, 120, fullBleed: true);
            Debug.Log($"[fish-finder-bench] cadence: WaterfallHz={cfg.WaterfallHz} ⇒ the steady-state cost " +
                      $"is that many rasters per second, not one per frame. Frame budget at 60 fps is " +
                      $"16.67 ms.");
        }

        private static void Bench(string what, int w, int h, bool fullBleed = false)
        {
            var surface = new DrawSurface(w, h);
            List<SonarMark> marks = FishFinderSheetEyeball.SampleMarks();
            Texture2D tex = null;

            void Paint(int i)
            {
                if (fullBleed)
                {
                    surface.Clear();
                    FishRigRender.DrawUnit(surface, 0, 0, w, h, State(i), marks);
                }
                else FishRigRender.Render(surface, State(i), marks);
            }

            for (int i = 0; i < Warmup; i++) Paint(i);

            var raster = new Stopwatch();
            raster.Start();
            for (int i = 0; i < Iterations; i++) Paint(i);
            raster.Stop();

            var upload = new Stopwatch();
            surface.ToTexture(ref tex);                       // create the texture outside the timer
            upload.Start();
            for (int i = 0; i < Iterations; i++) surface.ToTexture(ref tex);
            upload.Stop();

            double rasterMs = raster.Elapsed.TotalMilliseconds / Iterations;
            double uploadMs = upload.Elapsed.TotalMilliseconds / Iterations;
            Debug.Log($"[fish-finder-bench] {what} {w}x{h}: raster {rasterMs:F3} ms + upload " +
                      $"{uploadMs:F3} ms = {rasterMs + uploadMs:F3} ms per repaint " +
                      $"({Iterations} iterations).");
            if (tex != null) Object.DestroyImmediate(tex);
        }

        private static FishRigState State(int i)
            => new FishRigState(9.4f + (i % 7) * 0.1f, 12f, 20f, 3f, false, i % 2 == 0, true, true,
                                false, false, i * 0.125, FishRigAdjust.Range, 0.75f, true, 0.8f, 4f);
    }
}
