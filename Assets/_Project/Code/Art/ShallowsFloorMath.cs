using UnityEngine;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// Pure maths behind the floor in the shallows (W4-4): which ground a sea is drawn over. Engine-light,
    /// stateless, allocation-free; the headless twin the EditMode tests pin (<c>ShallowsFloorTests</c>).
    ///
    /// <para><b>One frame, two readers.</b> The sea and the splat ground of a region are fed the SAME painted
    /// height texture over the same world rectangle and elevation range (ADR 0014: render == sim by
    /// construction). That shared frame is how a sea finds its own ground (<see cref="TerrainSplatSurface.FindSharing"/>),
    /// so the floor the sea draws is that ground's look and never a guess: no scene field, no new id.</para>
    /// </summary>
    public static class ShallowsFloorMath
    {
        /// <summary>How far two frames' corners, sizes and elevation bounds (all metres) may differ and still
        /// be one frame: a millimetre. Far under a height-map texel (half a metre on both shipped maps, 1520
        /// texels over 760 m) and far over float round-off at map scale (760 m × 2^-23 ≈ 0.09 mm).</summary>
        public const float SameFrameToleranceMetres = 1e-3f;

        /// <summary>Do two height frames (corner, size, elevation range) describe the same map placement?</summary>
        public static bool SameFrame(Vector2 minA, Vector2 sizeA, float lowA, float highA,
                                     Vector2 minB, Vector2 sizeB, float lowB, float highB) =>
            Near(minA.x, minB.x) && Near(minA.y, minB.y) &&
            Near(sizeA.x, sizeB.x) && Near(sizeA.y, sizeB.y) &&
            Near(lowA, lowB) && Near(highA, highB);

        private static bool Near(float a, float b) => System.Math.Abs(a - b) <= SameFrameToleranceMetres;
    }
}
