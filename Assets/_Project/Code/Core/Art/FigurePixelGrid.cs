using System;
using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>The art-pixel lattice the figure pixel look draws on</b> (route 1 of
    /// <c>docs/design/figure-pixel-target.md</c>, behind <see cref="GameConfig.FigurePixelTarget"/>).
    ///
    /// <para>The mesh figures are drawn into a small target of their own at exactly one texel per art
    /// pixel (<see cref="ArtTexelMetres"/>, the locked 32 px/m), and that target is laid back over the
    /// screen with hard edges, so a figure reads like the rig's own sprite frames at every zoom step instead
    /// of like a smooth mesh. Everything here is the arithmetic of that round trip, kept engine-light
    /// so it can be pinned in EditMode (rule 7: plain numbers in, plain numbers out, no allocation).</para>
    ///
    /// <list type="bullet">
    ///   <item><b>Texel EDGES sit on whole multiples of 1/32 m in the world</b>, never the texel
    ///   centres: the target's lower-left corner is the visible corner floored to the lattice, less
    ///   <see cref="GuardTexels"/>. So the lattice is the world's, not the camera's, and a panning
    ///   camera never slides the art under the figure.</item>
    ///   <item><b>The composite moves by whole screen pixels only.</b> Where the lattice falls inside
    ///   a screen pixel is rounded once, to the nearest whole pixel; each screen pixel then reads the
    ///   texel under its centre, <c>floor((pixel + ½ − offset) / m)</c>: on a ladder step, a block of
    ///   exactly m×m pixels.</item>
    ///   <item><b>The size does not depend on where the camera is</b>: <c>ceil(screen/s)</c> plus a
    ///   guard texel each side plus one for the floor, s the whole step at or below m, so panning never
    ///   reallocates, nor does a zoom easing between two steps until it crosses one.</item>
    ///   <item><b>Every magnification above 1 is planned.</b> A framing between steps (CameraFollow's
    ///   feel: the push-in, the shake, the speed pull-back held while she drives fast, a framing's ease;
    ///   the PixelPerfectCamera paused for all of them) keeps the art pixels at the camera's own m, in
    ///   blocks of floor(m) and ceil(m) pixels as the world's own sprites draw in those frames (the
    ///   design note's off-ladder rule). Step 1 already draws one screen pixel per art pixel and a
    ///   downscale step draws fewer: both keep the camera's own size and draw exactly as the switch-OFF
    ///   build does.</item>
    /// </list>
    ///
    /// <para>Presentation only (rule 5): nothing here reads or writes a simulated body.</para>
    /// </summary>
    public static class FigurePixelGrid
    {
        /// <summary>Art pixels per world metre: the locked assets PPU (VS-23, the same number as
        /// <c>CameraFollow.AssetsPPU</c>). The art's own grid, not a tunable.</summary>
        public const int ArtPixelsPerMetre = 32;

        /// <summary>One art pixel in world metres: the lattice step.</summary>
        public const double ArtTexelMetres = 1d / ArtPixelsPerMetre;

        /// <summary>Texels kept beyond the visible edge on every side, so a texel at the screen's edge
        /// still has the neighbours the resolve's depth-edge and ink rules read.</summary>
        public const int GuardTexels = 1;

        /// <summary>How near a whole number the magnification must be, relative to it, to be that ladder
        /// step and draw its exact m×m blocks. The ladder's steps land within float noise of a whole
        /// number; a framing mid-effect lands nowhere near, and draws at its own m.</summary>
        public const double MagnificationTolerance = 1e-3;

        /// <summary>Added before the composite offset is floored, so a residual sitting a hair either
        /// side of a half pixel rounds the same way every frame instead of flickering. On a ladder step
        /// only: between steps the offset rounds half up exactly, which keeps pixel 0's centre in texel
        /// <see cref="GuardTexels"/> or beyond at any m.</summary>
        public const double OffsetRoundingBias = 1e-3;

        /// <summary>How far from the identity the camera's rotation may be, and how far from a pure
        /// orthographic box its projection: float noise, nothing more.</summary>
        public const float CameraTolerance = 1e-5f;

        /// <summary>
        /// The plan for one camera frame, from the visible world rectangle. Inactive (the camera's own
        /// size, no composite) where one screen pixel covers a whole art pixel or more (step 1, every
        /// downscale); otherwise active at m screen pixels per art pixel: the step's whole m on the
        /// ladder, the camera's own between steps.
        /// </summary>
        /// <param name="screenWidth">Camera target width, screen pixels.</param>
        /// <param name="screenHeight">Camera target height, screen pixels.</param>
        /// <param name="worldLeft">World x of the screen's left edge.</param>
        /// <param name="worldBottom">World y of the screen's bottom edge.</param>
        /// <param name="worldUnitsPerPixel">World metres one screen pixel covers.</param>
        /// <param name="plan">The plan; <see cref="FigurePixelPlan.Native"/> when this returns false.</param>
        public static bool TryPlan(int screenWidth, int screenHeight, double worldLeft, double worldBottom,
                                   double worldUnitsPerPixel, out FigurePixelPlan plan)
        {
            plan = FigurePixelPlan.Native(screenWidth, screenHeight);
            if (screenWidth <= 0 || screenHeight <= 0) return false;
            if (!(worldUnitsPerPixel > 0d) || double.IsInfinity(worldUnitsPerPixel)) return false;
            if (!IsFinite(worldLeft) || !IsFinite(worldBottom)) return false;

            double magnification = ArtTexelMetres / worldUnitsPerPixel;
            double whole = Math.Round(magnification);
            bool onAStep = Math.Abs(magnification - whole) <= MagnificationTolerance * whole;
            if (onAStep) magnification = whole;                         // float noise on a step: that step
            // Step 1 and every downscale stay native: a screen pixel already covers an art pixel or more.
            if (!(magnification > 1d) || magnification > ushort.MaxValue) return false;

            // Between steps the target keeps the size of the whole step below, and the offset rounds
            // without the step's bias (OffsetRoundingBias). On a step, sizeStep is m itself.
            int sizeStep = (int)Math.Floor(magnification);
            double bias = onAStep ? OffsetRoundingBias : 0d;
            Axis(screenWidth, worldLeft, magnification, sizeStep, bias, out long originX, out int offsetX, out int width);
            Axis(screenHeight, worldBottom, magnification, sizeStep, bias, out long originY, out int offsetY, out int height);
            plan = new FigurePixelPlan(true, magnification, width, height, originX, originY, offsetX, offsetY);
            return true;
        }

        /// <summary>
        /// <see cref="TryPlan"/> from a camera's own matrices (the GL projection, as URP hands it to
        /// <c>SetViewProjectionMatrices</c>). Refuses a rotated view, a perspective or sheared
        /// projection and non-square pixels: none of them has an art lattice on the screen.
        /// </summary>
        public static bool TryPlanCamera(int screenWidth, int screenHeight, Matrix4x4 view, Matrix4x4 projection,
                                         out FigurePixelPlan plan)
        {
            plan = FigurePixelPlan.Native(screenWidth, screenHeight);
            if (screenWidth <= 0 || screenHeight <= 0) return false;
            // The camera's x and y are the world's: the iso look lives in the meshes, never in the camera.
            if (!Near(view.m00, 1f) || !Near(view.m11, 1f) || !Near(view.m01, 0f) || !Near(view.m10, 0f) ||
                !Near(view.m02, 0f) || !Near(view.m12, 0f) || !Near(view.m20, 0f) || !Near(view.m21, 0f))
                return false;
            // An orthographic box: x from x alone, y from y alone, w = 1.
            if (!Near(projection.m30, 0f) || !Near(projection.m31, 0f) || !Near(projection.m32, 0f) ||
                !Near(projection.m33, 1f) || !Near(projection.m01, 0f) || !Near(projection.m10, 0f) ||
                !Near(projection.m02, 0f) || !Near(projection.m12, 0f))
                return false;
            if (!(projection.m00 > 0f) || !(projection.m11 > 0f)) return false;

            double viewWidth = 2d / projection.m00, viewHeight = 2d / projection.m11;
            double viewLeft = (-1d - projection.m03) / projection.m00;
            double viewBottom = (-1d - projection.m13) / projection.m11;
            double perPixelX = viewWidth / screenWidth, perPixelY = viewHeight / screenHeight;
            if (Math.Abs(perPixelX - perPixelY) > MagnificationTolerance * perPixelY) return false;
            // An unrotated view only translates, so world = view − translation.
            return TryPlan(screenWidth, screenHeight, viewLeft - view.m03, viewBottom - view.m13, perPixelY, out plan);
        }

        /// <summary>
        /// The figure target's own projection: the camera's, with its x and y box moved onto the
        /// planned texels. Depth (rows 2 and 3) is untouched, so a depth written through either
        /// projection compares with the other. Inactive plan: the camera's projection, unchanged.
        /// </summary>
        public static Matrix4x4 TargetProjection(Matrix4x4 projection, Matrix4x4 view, in FigurePixelPlan plan)
        {
            if (!plan.Active) return projection;
            double left = plan.OriginTexelX * ArtTexelMetres + view.m03;     // world → view is a translation
            double bottom = plan.OriginTexelY * ArtTexelMetres + view.m13;
            double width = plan.Width * ArtTexelMetres, height = plan.Height * ArtTexelMetres;
            Matrix4x4 p = projection;
            p.m00 = (float)(2d / width);
            p.m03 = (float)(-(2d * left + width) / width);
            p.m11 = (float)(2d / height);
            p.m13 = (float)(-(2d * bottom + height) / height);
            return p;
        }

        /// <summary>
        /// The drawn root of a figure standing ashore, put on the art lattice while the look is
        /// <paramref name="on"/> and a camera has published a grid (<see cref="GameServices.WorldUnitsPerRenderedPixel"/>
        /// above 0). Inert otherwise — an EditMode fixture or a bare art scene draws the float it had.
        /// Z is never touched (<see cref="PixelGrid.Snap(Vector3,float)"/>).
        /// </summary>
        public static Vector3 SnapDrawnRoot(Vector3 root, bool on, float worldUnitsPerRenderedPixel)
            => on && worldUnitsPerRenderedPixel > 0f ? PixelGrid.Snap(root, (float)ArtTexelMetres) : root;

        private static void Axis(int screen, double worldMin, double m, int sizeStep, double bias,
                                 out long origin, out int offset, out int size)
        {
            double edge = worldMin / ArtTexelMetres;                 // the screen's edge, in texels
            origin = (long)Math.Floor(edge) - GuardTexels;
            double offsetPixels = ((double)origin - edge) * m;      // texel 0's edge, screen pixels from pixel 0
            offset = (int)Math.Floor(offsetPixels + 0.5d + bias);
            size = (screen + sizeStep - 1) / sizeStep + 2 * GuardTexels + 1;
        }

        private static bool Near(float value, float target) => Math.Abs(value - target) <= CameraTolerance;

        private static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }

    /// <summary>
    /// One camera frame's figure target: its size, where its texels sit in the world, and how the
    /// screen reads them back. A plain value (rule 7: no allocation per frame).
    /// </summary>
    public readonly struct FigurePixelPlan
    {
        /// <summary>True when the figures draw at art-pixel size this frame.</summary>
        public readonly bool Active;
        /// <summary>Screen pixels per art pixel, each way: the step's whole m on the ladder, the camera's
        /// own between steps (blocks of floor(m) and ceil(m) pixels). 1 while inactive.</summary>
        public readonly double Magnification;
        /// <summary>Target size in texels; the camera's own size while inactive.</summary>
        public readonly int Width, Height;
        /// <summary>World position of texel 0's lower-left corner, in whole art pixels.</summary>
        public readonly long OriginTexelX, OriginTexelY;
        /// <summary>Screen pixel at which texel 0's lower-left corner lands (whole pixels; negative).</summary>
        public readonly int OffsetX, OffsetY;

        public FigurePixelPlan(bool active, double magnification, int width, int height,
                               long originTexelX, long originTexelY, int offsetX, int offsetY)
        {
            Active = active;
            Magnification = magnification;
            Width = width;
            Height = height;
            OriginTexelX = originTexelX;
            OriginTexelY = originTexelY;
            OffsetX = offsetX;
            OffsetY = offsetY;
        }

        /// <summary>No target: the camera's own size, drawn as the switch-OFF build draws.</summary>
        public static FigurePixelPlan Native(int screenWidth, int screenHeight)
            => new FigurePixelPlan(false, 1, Math.Max(0, screenWidth), Math.Max(0, screenHeight), 0, 0, 0, 0);

        /// <summary>The shaders' map, <c>(offsetX, offsetY, 1/m, m)</c>. Zero while inactive: w = 0 is
        /// what every reader takes for "no figure target this frame".</summary>
        public Vector4 PixelMap => Active
            ? new Vector4(OffsetX, OffsetY, 1f / (float)Magnification, (float)Magnification)
            : Vector4.zero;

        /// <summary>The texel column a screen pixel column reads (the overlay's lookup): the one under the
        /// pixel's centre, on a step exactly <c>floor((pixel − offset) / m)</c>.</summary>
        public int TexelOfScreenX(int pixel) => TexelUnder(pixel - OffsetX);

        /// <summary>The texel row a screen pixel row reads.</summary>
        public int TexelOfScreenY(int pixel) => TexelUnder(pixel - OffsetY);

        /// <summary>The screen pixel column a texel samples the scene's depth at: the one under its centre.</summary>
        public int SampleScreenX(int texel) => OffsetX + PixelUnderCentre(texel);

        /// <summary>The screen pixel row a texel samples the scene's depth at.</summary>
        public int SampleScreenY(int texel) => OffsetY + PixelUnderCentre(texel);

        private int TexelUnder(int fromOffset) => (int)Math.Floor((fromOffset + 0.5d) / Magnification);

        private int PixelUnderCentre(int texel) => (int)Math.Floor((texel + 0.5d) * Magnification);
    }
}
