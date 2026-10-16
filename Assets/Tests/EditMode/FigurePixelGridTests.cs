using System;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools.Constraints;
using HiddenHarbours.App;
using HiddenHarbours.Core;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// The lattice of the figure pixel look (route 1, <see cref="FigurePixelGrid"/>, behind
    /// <see cref="GameConfig.FigurePixelTarget"/>; docs/design/figure-pixel-target.md): the figure
    /// target's size at every ladder step and the screen's own size where no step magnifies, texel edges
    /// on whole art pixels in the world, a composite that moves by whole screen pixels, the ashore snap
    /// inert until a camera publishes a grid, and planning a frame allocating nothing (rule 7).
    ///
    /// <para>Pure arithmetic: every view and projection here is built by hand (Unity's own conventions:
    /// the view looks down −z, the projection is GL's), so no camera, device or scene is needed.</para>
    /// </summary>
    public class FigurePixelGridTests
    {
        private const int Ppu = CameraFollow.AssetsPPU;
        private const double Texel = FigurePixelGrid.ArtTexelMetres;
        private const int Guard = FigurePixelGrid.GuardTexels;

        // The shipped 16:9 sizes, plus an odd one so a parity slip in the size or the centre shows.
        private static readonly (int w, int h)[] Screens = { (1920, 1080), (1280, 720), (2560, 1440), (1365, 767) };

        private static object _sink;
        private static float _acc;

        // ==================================================================== the size, per ladder step

        [Test]
        public void TheLattice_IsTheArtsOwnGrid()
        {
            Assert.AreEqual(Ppu, FigurePixelGrid.ArtPixelsPerMetre,
                            "the figure lattice is the locked assets PPU (VS-23), not a second number that can drift");
            Assert.AreEqual(1d / Ppu, Texel, 0d, "one texel is one art pixel");
        }

        [Test]
        public void EveryUpscaleStep_PlansOneTexelPerArtPixel()
        {
            foreach (var (w, h) in Screens)
            {
                for (int step = 2; step <= CameraZoomPolicy.MaxStep; step++)
                {
                    string what = $"{w}x{h} step {step}";
                    double perPixel = CameraZoomPolicy.WorldUnitsPerRenderedPixel(step, Ppu, h);
                    Assert.IsTrue(FigurePixelGrid.TryPlan(w, h, 3.37d, -5.11d, perPixel, out FigurePixelPlan plan),
                                  $"{what}: a whole magnification must plan");
                    Assert.IsTrue(plan.Active, $"{what}: active");
                    Assert.AreEqual(step, plan.Magnification, $"{what}: one texel is a block of step x step pixels");
                    Assert.AreEqual((w + step - 1) / step + 2 * Guard + 1, plan.Width,
                                    $"{what}: ceil(screen/m), a guard texel each side, one for the floor");
                    Assert.AreEqual((h + step - 1) / step + 2 * Guard + 1, plan.Height, $"{what}: the same, upward");
                    Assert.AreEqual(new Vector4(plan.OffsetX, plan.OffsetY, 1f / step, step), plan.PixelMap,
                                    $"{what}: the shaders' map is (offset x, offset y, 1/m, m)");
                }
            }

            // 1920x1080 by hand, so the formula above is not the only witness.
            int[,] full = { { 2, 963, 543 }, { 3, 643, 363 }, { 4, 483, 273 }, { 5, 387, 219 },
                            { 6, 323, 183 }, { 7, 278, 158 }, { 8, 243, 138 } };
            for (int i = 0; i < full.GetLength(0); i++)
            {
                int step = full[i, 0];
                double perPixel = CameraZoomPolicy.WorldUnitsPerRenderedPixel(step, Ppu, 1080);
                Assert.IsTrue(FigurePixelGrid.TryPlan(1920, 1080, -0.4d, 0.7d, perPixel, out FigurePixelPlan plan));
                Assert.AreEqual(full[i, 1], plan.Width, $"1920x1080 step {step}: width in texels");
                Assert.AreEqual(full[i, 2], plan.Height, $"1920x1080 step {step}: height in texels");
            }
        }

        [Test]
        public void StepOneAndEveryDownscaleStep_KeepTheScreensOwnSize()
        {
            foreach (var (w, h) in Screens)
            {
                for (int step = CameraZoomPolicy.MinStep; step <= 1; step++)
                {
                    if (step == 0 || step == -1) continue;   // not ladder steps (CameraZoomPolicy's const doc)
                    string what = $"{w}x{h} step {step}";
                    double perPixel = CameraZoomPolicy.WorldUnitsPerRenderedPixel(step, Ppu, h);
                    Assert.IsFalse(FigurePixelGrid.TryPlan(w, h, 3.37d, -5.11d, perPixel, out FigurePixelPlan plan),
                                   $"{what}: one screen pixel is one art pixel or more; nothing to draw small");
                    AssertNative(plan, w, h, what);
                }
            }
        }

        [Test]
        public void AFramingBetweenSteps_StaysNative_AndFloatNoiseOnAStep_Plans()
        {
            foreach (double magnification in new[] { 1d, 1.5d, 2.5d, 3.01d, 4.75d, 7.98d })
            {
                string what = $"magnification {magnification}";
                Assert.IsFalse(FigurePixelGrid.TryPlan(1920, 1080, 0d, 0d, Texel / magnification, out FigurePixelPlan plan),
                               $"{what}: no whole block size, so the camera's own size (a feel effect mid-zoom)");
                AssertNative(plan, 1920, 1080, what);
            }

            foreach (var (magnification, whole) in new[] { (2d, 2), (3d * (1d + 1e-7d), 3), (5d * (1d - 1e-7d), 5),
                                                           (8d * (1d + 1e-6d), 8) })
            {
                Assert.IsTrue(FigurePixelGrid.TryPlan(1920, 1080, 0d, 0d, Texel / magnification, out FigurePixelPlan plan),
                              $"magnification {magnification}: within float noise of {whole}");
                Assert.AreEqual(whole, plan.Magnification, $"magnification {magnification}: the whole step");
            }
        }

        [Test]
        public void TheSize_DoesNotChangeAsTheCameraPans()
        {
            foreach (var (w, h) in Screens)
            {
                for (int step = 2; step <= CameraZoomPolicy.MaxStep; step++)
                {
                    double perPixel = Texel / step;
                    Assert.IsTrue(FigurePixelGrid.TryPlan(w, h, 0d, 0d, perPixel, out FigurePixelPlan first));
                    for (int i = 0; i < 97; i++)
                    {
                        double left = -40d + i * 0.8371d, bottom = 25d - i * 0.5113d;
                        Assert.IsTrue(FigurePixelGrid.TryPlan(w, h, left, bottom, perPixel, out FigurePixelPlan plan));
                        Assert.AreEqual(first.Width, plan.Width, $"{w}x{h} step {step} at ({left}, {bottom}): width");
                        Assert.AreEqual(first.Height, plan.Height, $"{w}x{h} step {step} at ({left}, {bottom}): height");
                    }
                }
            }
        }

        // ==================================================================== the lattice

        [Test]
        public void TexelEdges_LieOnWholeArtPixelsInTheWorld_ThroughTheCamerasOwnMatrices()
        {
            // On and off the lattice, both sides of zero, near and far from the origin.
            float[] xs = { 0f, 3.3f, -7.1f, 517.3f, -0.015625f, 12.34375f };
            float[] ys = { 0f, -2.2f, 41.7f, -300.03f, 0.03125f, 9.99f };
            foreach (var (w, h) in Screens)
            {
                for (int step = 2; step <= CameraZoomPolicy.MaxStep; step++)
                {
                    for (int c = 0; c < xs.Length; c++)
                    {
                        string what = $"{w}x{h} step {step} camera ({xs[c]}, {ys[c]})";
                        Matrix4x4 view = View(xs[c], ys[c]);
                        Matrix4x4 projection = Ortho(w, h, Texel / step, c * 0.37d, -c * 0.21d);
                        Assert.IsTrue(FigurePixelGrid.TryPlanCamera(w, h, view, projection, out FigurePixelPlan plan),
                                      $"{what}: an unrotated orthographic camera on a step plans");
                        Assert.AreEqual(step, plan.Magnification, $"{what}: the step");
                        Matrix4x4 low = FigurePixelGrid.TargetProjection(projection, view, in plan);
                        foreach (int k in new[] { 0, 1, plan.Width / 2, plan.Width - 1, plan.Width })
                        {
                            double ndc = -1d + 2d * k / plan.Width;
                            double world = (ndc - low.m03) / low.m00 - view.m03;
                            Assert.AreEqual((plan.OriginTexelX + k) * Texel, world, 1e-3d * Texel,
                                            $"{what}: texel column edge {k} is not on the world's art lattice");
                        }
                        foreach (int k in new[] { 0, 1, plan.Height / 2, plan.Height - 1, plan.Height })
                        {
                            double ndc = -1d + 2d * k / plan.Height;
                            double world = (ndc - low.m13) / low.m11 - view.m13;
                            Assert.AreEqual((plan.OriginTexelY + k) * Texel, world, 1e-3d * Texel,
                                            $"{what}: texel row edge {k} is not on the world's art lattice");
                        }
                    }
                }
            }
        }

        [Test]
        public void TheCompositeOffset_IsTheLatticesOwnPosition_RoundedToAWholePixel()
        {
            foreach (var (w, h) in Screens)
            {
                for (int step = 2; step <= CameraZoomPolicy.MaxStep; step++)
                {
                    double perPixel = Texel / step;
                    for (int i = 0; i < 64; i++)
                    {
                        double left = -13.7d + i * 0.4127d, bottom = 8.9d - i * 0.2931d;
                        string what = $"{w}x{h} step {step} at ({left}, {bottom})";
                        Assert.IsTrue(FigurePixelGrid.TryPlan(w, h, left, bottom, perPixel, out FigurePixelPlan plan));
                        double exactX = (plan.OriginTexelX * Texel - left) / perPixel;
                        double exactY = (plan.OriginTexelY * Texel - bottom) / perPixel;
                        double bound = 0.5d + FigurePixelGrid.OffsetRoundingBias + 1e-9d;
                        Assert.LessOrEqual(Math.Abs(plan.OffsetX - exactX), bound, $"{what}: x offset is the nearest whole pixel");
                        Assert.LessOrEqual(Math.Abs(plan.OffsetY - exactY), bound, $"{what}: y offset is the nearest whole pixel");
                        Assert.LessOrEqual(plan.OffsetX, -step, $"{what}: texel 0 starts a whole texel or more left of pixel 0 (the guard)");
                        Assert.LessOrEqual(plan.OffsetY, -step, $"{what}: texel 0 starts a whole texel or more below pixel 0 (the guard)");
                        Assert.AreEqual(new Vector4(plan.OffsetX, plan.OffsetY, 1f / step, step), plan.PixelMap,
                                        $"{what}: the map carries the offsets exactly");
                    }
                }
            }
        }

        [Test]
        public void AWorldFixedTexel_MovesExactlyWithAWholePixelPan()
        {
            foreach (var (w, h) in Screens)
            {
                for (int step = 2; step <= CameraZoomPolicy.MaxStep; step++)
                {
                    double perPixel = Texel / step;
                    foreach (double start in new[] { 0d, 517 * perPixel, -12.3456d })
                    {
                        Assert.IsTrue(FigurePixelGrid.TryPlan(w, h, start, start, perPixel, out FigurePixelPlan home));
                        long texelX = home.OriginTexelX + Guard + 3, texelY = home.OriginTexelY + Guard + 3;
                        long homeX = home.OffsetX + (texelX - home.OriginTexelX) * step;
                        long homeY = home.OffsetY + (texelY - home.OriginTexelY) * step;
                        for (int pan = -9; pan <= 9; pan++)
                        {
                            double moved = start + pan * perPixel;
                            Assert.IsTrue(FigurePixelGrid.TryPlan(w, h, moved, moved, perPixel, out FigurePixelPlan plan));
                            long x = plan.OffsetX + (texelX - plan.OriginTexelX) * step;
                            long y = plan.OffsetY + (texelY - plan.OriginTexelY) * step;
                            Assert.AreEqual(homeX - pan, x, $"{w}x{h} step {step} from {start}, pan {pan} px: the texel slid in x");
                            Assert.AreEqual(homeY - pan, y, $"{w}x{h} step {step} from {start}, pan {pan} px: the texel slid in y");
                        }
                    }
                }
            }
        }

        [Test]
        public void EveryScreenPixel_ReadsAnInteriorTexel_InBlocksOfExactlyM()
        {
            foreach (var (w, h) in Screens)
            {
                for (int step = 2; step <= CameraZoomPolicy.MaxStep; step++)
                {
                    for (int phase = 0; phase < 16; phase++)
                    {
                        double left = -3.25d + phase * Texel / 16d, bottom = 7.5d - phase * Texel / 16d;
                        Assert.IsTrue(FigurePixelGrid.TryPlan(w, h, left, bottom, Texel / step, out FigurePixelPlan plan));
                        string what = $"{w}x{h} step {step} phase {phase}/16";
                        AssertAxis(w, plan.Width, step, plan.TexelOfScreenX, plan.SampleScreenX, what + " x");
                        AssertAxis(h, plan.Height, step, plan.TexelOfScreenY, plan.SampleScreenY, what + " y");
                    }
                }
            }
        }

        private static void AssertAxis(int screen, int size, int m, Func<int, int> texelOf, Func<int, int> sampleOf,
                                       string what)
        {
            int first = texelOf(0), last = texelOf(screen - 1);
            Assert.GreaterOrEqual(first, Guard, $"{what}: pixel 0 reads a guard texel");
            Assert.Less(last, size - Guard, $"{what}: the last pixel reads a guard texel");
            int run = 0, previous = first;
            for (int px = 0; px < screen; px++)
            {
                int t = texelOf(px);
                if (t == previous) { run++; continue; }
                Assert.AreEqual(previous + 1, t, $"{what}: the texels step by one at pixel {px}");
                if (previous != first) Assert.AreEqual(m, run, $"{what}: texel {previous} covers {run} pixels, not {m}");
                previous = t;
                run = 1;
            }
            Assert.LessOrEqual(run, m, $"{what}: the last block is longer than {m}");
            for (int t = first; t <= last; t++)
                Assert.AreEqual(t, texelOf(sampleOf(t)), $"{what}: texel {t}'s depth sample is not inside its own block");
        }

        // ==================================================================== cameras with no lattice

        [Test]
        public void ACameraWithNoArtLattice_StaysNative_AndKeepsItsProjection()
        {
            const int w = 1920, h = 1080;
            double perPixel = Texel / 3;
            Matrix4x4 view = View(1.5f, -2.5f), projection = Ortho(w, h, perPixel);
            Assert.IsTrue(FigurePixelGrid.TryPlanCamera(w, h, view, projection, out _), "control: the plain camera plans");

            Matrix4x4 rolled = view;   // 10 degrees of roll
            rolled.m00 = 0.98480775f; rolled.m01 = -0.17364818f;
            rolled.m10 = 0.17364818f; rolled.m11 = 0.98480775f;
            Matrix4x4 perspective = projection;
            perspective.m32 = -1f;
            perspective.m33 = 0f;
            Matrix4x4 stretched = projection;
            stretched.m00 *= 1.01f;
            Matrix4x4 sheared = projection;
            sheared.m01 = 0.01f;

            AssertNativeCamera(w, h, rolled, projection, "a rolled view");
            AssertNativeCamera(w, h, view, perspective, "a perspective projection");
            AssertNativeCamera(w, h, view, stretched, "non-square pixels");
            AssertNativeCamera(w, h, view, sheared, "a sheared projection");
            AssertNativeCamera(0, h, view, projection, "a zero-width target");
            AssertNativeCamera(w, 0, view, projection, "a zero-height target");

            foreach (double bad in new[] { 0d, -perPixel, double.NaN, double.PositiveInfinity })
            {
                Assert.IsFalse(FigurePixelGrid.TryPlan(w, h, 0d, 0d, bad, out FigurePixelPlan plan), $"pixel size {bad}");
                AssertNative(plan, w, h, $"pixel size {bad}");
            }
            Assert.IsFalse(FigurePixelGrid.TryPlan(w, h, double.NaN, 0d, perPixel, out FigurePixelPlan lost), "a NaN edge");
            AssertNative(lost, w, h, "a NaN edge");
        }

        [Test]
        public void TheTargetsProjection_MovesOnlyTheXAndYBox()
        {
            foreach (var (w, h) in Screens)
            {
                for (int step = 2; step <= CameraZoomPolicy.MaxStep; step++)
                {
                    string what = $"{w}x{h} step {step}";
                    Matrix4x4 view = View(-4.4f, 6.6f), projection = Ortho(w, h, Texel / step, 0.25d, -0.75d);
                    Assert.IsTrue(FigurePixelGrid.TryPlanCamera(w, h, view, projection, out FigurePixelPlan plan), what);
                    Matrix4x4 low = FigurePixelGrid.TargetProjection(projection, view, in plan);
                    Assert.AreEqual(projection.GetRow(2), low.GetRow(2),
                                    $"{what}: depth row 2 moved, so the figures' depth would not compare with the scene's");
                    Assert.AreEqual(projection.GetRow(3), low.GetRow(3), $"{what}: row 3 moved");
                    Assert.AreEqual(projection.m01, low.m01, 0f, $"{what}: m01");
                    Assert.AreEqual(projection.m02, low.m02, 0f, $"{what}: m02");
                    Assert.AreEqual(projection.m10, low.m10, 0f, $"{what}: m10");
                    Assert.AreEqual(projection.m12, low.m12, 0f, $"{what}: m12");
                }
            }
        }

        // ==================================================================== the ashore snap

        [Test]
        public void TheAshoreSnap_IsInert_UntilACameraPublishesAGrid_OrWhileOff()
        {
            var roots = new[] { new Vector3(1.234567f, -7.654321f, 0.5f), new Vector3(-0.015f, 0.016f, -3f),
                                new Vector3(1000.01f, 0f, 0f) };
            foreach (Vector3 root in roots)
            {
                AssertSameBits(root, FigurePixelGrid.SnapDrawnRoot(root, true, 0f), $"ON, no camera has published, {root}");
                AssertSameBits(root, FigurePixelGrid.SnapDrawnRoot(root, true, -1f), $"ON, a negative grid, {root}");
                AssertSameBits(root, FigurePixelGrid.SnapDrawnRoot(root, true, float.NaN), $"ON, a NaN grid, {root}");
                AssertSameBits(root, FigurePixelGrid.SnapDrawnRoot(root, false, 1f / 96f), $"OFF with a grid, {root}");
            }
        }

        [Test]
        public void WithAGrid_TheAshoreSnap_PutsTheDrawnRootOnTheArtLattice()
        {
            float grid = CameraZoomPolicy.WorldUnitsPerRenderedPixel(3, Ppu, 1080);
            for (int i = 0; i < 400; i++)
            {
                var root = new Vector3(-9.87f + i * 0.04937f, 4.321f - i * 0.02711f, 0.25f + i);
                Vector3 drawn = FigurePixelGrid.SnapDrawnRoot(root, true, grid);
                double ax = drawn.x * Ppu, ay = drawn.y * Ppu;
                Assert.AreEqual(Math.Round(ax), ax, 0d, $"{root}: x is not on a whole art pixel");
                Assert.AreEqual(Math.Round(ay), ay, 0d, $"{root}: y is not on a whole art pixel");
                Assert.LessOrEqual(Math.Abs(drawn.x - root.x), Texel / 2 + 1e-6d, $"{root}: x moved more than half a texel");
                Assert.LessOrEqual(Math.Abs(drawn.y - root.y), Texel / 2 + 1e-6d, $"{root}: y moved more than half a texel");
                Assert.AreEqual(BitConverter.SingleToInt32Bits(root.z), BitConverter.SingleToInt32Bits(drawn.z),
                                $"{root}: z is never touched");
            }
        }

        [Test]
        public void TheAshoreSnap_ReadsTheSwitchAndTheGrid_ThroughGameServices()
        {
            GameConfig previousConfig = GameServices.Config;
            float previousGrid = GameServices.WorldUnitsPerRenderedPixel;
            var config = ScriptableObject.CreateInstance<GameConfig>();
            try
            {
                var root = new Vector3(2.0123f, -3.0456f, 1f);
                GameServices.Config = config;
                config.FigurePixelTarget = true;
                GameServices.WorldUnitsPerRenderedPixel = 0f;
                AssertSameBits(root, SnapThroughServices(root), "ON before any camera has published");

                GameServices.WorldUnitsPerRenderedPixel = 1f / 96f;
                Vector3 drawn = SnapThroughServices(root);
                Assert.AreEqual(Mathf.Round(root.x * Ppu) / Ppu, drawn.x, 0f, "ON with a grid: x on the lattice");
                Assert.AreEqual(Mathf.Round(root.y * Ppu) / Ppu, drawn.y, 0f, "ON with a grid: y on the lattice");

                config.FigurePixelTarget = false;
                AssertSameBits(root, SnapThroughServices(root), "OFF with a grid");

                GameServices.Config = null;
                AssertSameBits(root, SnapThroughServices(root), "no config: the code default, OFF");
            }
            finally
            {
                GameServices.Config = previousConfig;
                GameServices.WorldUnitsPerRenderedPixel = previousGrid;
                UnityEngine.Object.DestroyImmediate(config);
            }
        }

        private static Vector3 SnapThroughServices(Vector3 root)
            => FigurePixelGrid.SnapDrawnRoot(root, GameServices.FigurePixelTarget, GameServices.WorldUnitsPerRenderedPixel);

        // ==================================================================== rule 7

        [Test]
        public void PlanningAFrame_AllocatesNothing()
        {
            Matrix4x4 view = View(3.3f, -7.1f), projection = Ortho(1920, 1080, Texel / 3);
            Assert.IsTrue(FigurePixelGrid.TryPlanCamera(1920, 1080, view, projection, out _),
                          "the measured loop must run the planned path, not a refusal");
            _acc = Frames(view, projection, 4);   // warm-up

            // The recorder must see an allocation here, or the negative below would pass on anything.
            Assert.That(() => { _sink = new object[64]; }, Is.AllocatingGCMemory(),
                        "harness: the GC.Alloc recorder saw no allocation at all");
            Assert.That(() => { _acc = Frames(view, projection, 64); }, Is.Not.AllocatingGCMemory(),
                        "planning a frame allocated (rule 7: nothing per frame)");

            // The same loop on the thread's allocation counter, where this runtime keeps one.
            long before = GC.GetAllocatedBytesForCurrentThread();
            _sink = new object[64];
            if (GC.GetAllocatedBytesForCurrentThread() - before <= 0)
            {
                Debug.Log("[FigurePixelGridTests] GC.GetAllocatedBytesForCurrentThread counts nothing on this " +
                          "runtime, so its arm is skipped. The GC.Alloc recorder above is the measurement.");
                return;
            }
            before = GC.GetAllocatedBytesForCurrentThread();
            _acc = Frames(view, projection, 64);
            Assert.AreEqual(0L, GC.GetAllocatedBytesForCurrentThread() - before,
                            "planning a frame allocated bytes on the thread's own counter");
        }

        private static float Frames(Matrix4x4 view, Matrix4x4 projection, int frames)
        {
            float acc = 0f;
            for (int i = 0; i < frames; i++)
            {
                view.m03 -= 0.0123f;   // the camera pans
                if (FigurePixelGrid.TryPlanCamera(1920, 1080, view, projection, out FigurePixelPlan plan))
                {
                    Matrix4x4 low = FigurePixelGrid.TargetProjection(projection, view, in plan);
                    Vector4 map = plan.PixelMap;
                    acc += low.m03 + map.x + plan.TexelOfScreenX(i) + plan.SampleScreenY(i);
                }
                acc += FigurePixelGrid.SnapDrawnRoot(new Vector3(i * 0.013f, 2f, 0f), true, 1f / 96f).x;
                acc += FigureHeadingDial.Quantize(i * 7.3f, 16) + FigureHeadingDial.DeckYaw(i * 3.1f, 45f, 8);
            }
            return acc;
        }

        // ==================================================================== helpers

        private static void AssertNative(in FigurePixelPlan plan, int w, int h, string what)
        {
            Assert.IsFalse(plan.Active, $"{what}: inactive");
            Assert.AreEqual(1, plan.Magnification, $"{what}: magnification 1");
            Assert.AreEqual(Math.Max(0, w), plan.Width, $"{what}: the screen's own width");
            Assert.AreEqual(Math.Max(0, h), plan.Height, $"{what}: the screen's own height");
            Assert.AreEqual(Vector4.zero, plan.PixelMap, $"{what}: a zero map is the overlays' untouched path");
        }

        private static void AssertNativeCamera(int w, int h, Matrix4x4 view, Matrix4x4 projection, string what)
        {
            Assert.IsFalse(FigurePixelGrid.TryPlanCamera(w, h, view, projection, out FigurePixelPlan plan),
                           $"{what}: no art lattice on the screen, so no plan");
            AssertNative(plan, w, h, what);
            Matrix4x4 low = FigurePixelGrid.TargetProjection(projection, view, in plan);
            for (int i = 0; i < 16; i++)
                Assert.AreEqual(projection[i], low[i], 0f, $"{what}: element {i} of the projection moved");
        }

        private static void AssertSameBits(Vector3 expected, Vector3 actual, string what)
        {
            Assert.AreEqual(BitConverter.SingleToInt32Bits(expected.x), BitConverter.SingleToInt32Bits(actual.x), $"{what}: x");
            Assert.AreEqual(BitConverter.SingleToInt32Bits(expected.y), BitConverter.SingleToInt32Bits(actual.y), $"{what}: y");
            Assert.AreEqual(BitConverter.SingleToInt32Bits(expected.z), BitConverter.SingleToInt32Bits(actual.z), $"{what}: z");
        }

        /// <summary>An unrotated camera at (x, y, −10): Unity's worldToCameraMatrix, which looks down −z.</summary>
        private static Matrix4x4 View(float x, float y)
        {
            Matrix4x4 v = Matrix4x4.identity;
            v.m03 = -x;
            v.m13 = -y;
            v.m22 = -1f;
            v.m23 = -10f;
            return v;
        }

        /// <summary>GL's orthographic box of <paramref name="w"/>×<paramref name="h"/> pixels of
        /// <paramref name="perPixel"/> metres, its centre moved by a fraction of a pixel if asked.</summary>
        private static Matrix4x4 Ortho(int w, int h, double perPixel, double shiftX = 0d, double shiftY = 0d)
        {
            double l = (-w / 2d + shiftX) * perPixel, r = l + w * perPixel;
            double b = (-h / 2d + shiftY) * perPixel, t = b + h * perPixel;
            const double near = 0.3d, far = 1000d;
            Matrix4x4 p = Matrix4x4.zero;
            p.m00 = (float)(2d / (r - l));
            p.m03 = (float)(-(r + l) / (r - l));
            p.m11 = (float)(2d / (t - b));
            p.m13 = (float)(-(t + b) / (t - b));
            p.m22 = (float)(-2d / (far - near));
            p.m23 = (float)(-(far + near) / (far - near));
            p.m33 = 1f;
            return p;
        }
    }
}
