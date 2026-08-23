using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// THE ONE PLACE a RENDERED position is put back on the pixel grid.
    ///
    /// <para><b>Why anything needs this at all.</b> The locked Pixel Perfect Camera (VS-23, bible
    /// §9.2) is set to <c>GridSnapping.PixelSnapping</c>, and that mode does exactly one thing: it
    /// publishes <c>PixelPerfectRendering.pixelSnapSpacing</c>, which <b>SpriteRenderers</b> — and
    /// nothing else — snap to. It does not move the camera, and it does not touch a MeshRenderer. So
    /// two rendered positions in this game are still free-floating floats:</para>
    /// <list type="number">
    ///   <item><b>The camera itself.</b> Sprites snap to a WORLD grid; the camera then samples that
    ///   world from an arbitrary sub-pixel offset, so an asset texel straddles screen pixels and the
    ///   upscale gives it 2 screen pixels on one frame and 3 on the next. That is the "goes soft
    ///   while moving" read — uneven texel sizes wobbling frame to frame, not a blur filter.</item>
    ///   <item><b>Every MESH hull and vehicle</b> (ADR 0022 phase 3 — <c>IsoFacetHullRenderer</c>
    ///   draws through a MeshRenderer, so the sprite-only snap never reaches it).</item>
    /// </list>
    ///
    /// <para><b>The grid is a CAMERA property, not a constant.</b> One rendered pixel is
    /// <c>worldHeight / screenHeightPx</c> at the framing currently shown — <c>1/(ppu·step)</c> on an
    /// integer upscale, <c>(−step)/ppu</c> on an integer downscale (<c>CameraZoomPolicy</c>'s ladder;
    /// the App side derives it and publishes it as
    /// <see cref="GameServices.WorldUnitsPerRenderedPixel"/>). Snapping to the ASSET grid (1/ppu)
    /// instead would be wrong at both ends: too coarse to move smoothly when the view is magnified,
    /// and too fine to actually land on a screen pixel when it is shrunk.</para>
    ///
    /// <para><b>Presentation only (rule 5).</b> Nothing here may touch a simulated body. The physics
    /// position stays the honest float it always was; only the transform that gets DRAWN is rounded,
    /// the same discipline <c>BoatController</c> states for rigidbody interpolation ("interpolation
    /// moves the rendered transform, never the simulated body"). Determinism is untouched — no sim
    /// system reads a snapped value.</para>
    ///
    /// <para>Allocation-free and engine-light: floats in, floats out (rule 7).</para>
    /// </summary>
    public static class PixelGrid
    {
        /// <summary>
        /// Round one world axis onto a grid of <paramref name="worldUnitsPerPixel"/>.
        ///
        /// <para><b>A non-positive grid is INERT</b> and returns the input untouched. That is the
        /// unwired state, not an error: an EditMode fixture, a bare art scene or any frame before a
        /// camera has published its framing has no grid to snap to, and the honest answer there is
        /// the float that was already there.</para>
        ///
        /// <para>ROUND, not floor. <c>Floor</c> is right for the grass bend
        /// (<c>GrassWindMath.PixelSnap</c>), where the offset is a one-way displacement off a fixed
        /// root; a camera or a hull is tracking a moving target through the grid in both directions,
        /// and flooring it would hold every position half a pixel low — a constant bias that shows up
        /// as the whole world sitting a pixel off its authored place.</para>
        /// </summary>
        public static float Snap(float world, float worldUnitsPerPixel)
            => worldUnitsPerPixel > 0f
                ? Mathf.Round(world / worldUnitsPerPixel) * worldUnitsPerPixel
                : world;

        /// <inheritdoc cref="Snap(float,float)"/>
        public static Vector2 Snap(Vector2 world, float worldUnitsPerPixel)
            => worldUnitsPerPixel > 0f
                ? new Vector2(Snap(world.x, worldUnitsPerPixel), Snap(world.y, worldUnitsPerPixel))
                : world;

        /// <summary>
        /// <inheritdoc cref="Snap(float,float)"/>
        ///
        /// <para><b>Z IS NEVER TOUCHED.</b> Under the ortho camera depth is the sorting axis, not a
        /// visible dimension — and for a mesh hull it carries a calibrated iso-depth convention
        /// (ADR 0033's shear compensation) whose units are nothing to do with world metres. Rounding
        /// it would re-sort the scene, or throw a hull clean out of frame.</para>
        /// </summary>
        public static Vector3 Snap(Vector3 world, float worldUnitsPerPixel)
            => worldUnitsPerPixel > 0f
                ? new Vector3(Snap(world.x, worldUnitsPerPixel), Snap(world.y, worldUnitsPerPixel), world.z)
                : world;
    }
}
