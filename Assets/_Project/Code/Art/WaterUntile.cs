using System;
using UnityEngine;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// Pure, headless-testable twin of the water shader's texture UNTILING blend
    /// (<c>UntileSampleW</c> in <c>HiddenHarboursWater.shader</c>; change one, change BOTH in the
    /// same PR, and <c>WaterUntileTests</c> reads the shader source to prove it).
    ///
    /// <para><b>🔴 The defect this class exists to name: an untile blend that is DISCONTINUOUS at
    /// the repeat-cell boundary draws a straight-line GRID onto the sea.</b> The owner reported it
    /// on 2026-08-06 as "thin, non-organic straight lines" in otherwise-good shore foam, and it was
    /// never the seabed, the swash or the foam-edge dither. It was the untiler itself.</para>
    ///
    /// <para><b>What was wrong.</b> The shipped blend picked TWO variants per repeat-cell — the
    /// cell's own hashed translation and the DIAGONAL neighbour's — and mixed them by
    /// <c>w = smoothstep(0.2,0.8,fx)·0.5 + smoothstep(0.2,0.8,fy)·0.5</c>. Cross a cell boundary in
    /// x and three things change at once: the cell index steps, so BOTH hashed variants are replaced
    /// by two unrelated ones, and <c>w</c> jumps by 0.5. The two sides of the boundary therefore
    /// share no variant and no weight — the blended value simply jumps. At the shipped
    /// <c>_PaintScale</c> 0.25 that is a hard seam every 4 metres, in both axes, on EVERY painted
    /// water slot (surface, foam, whitecaps, sparkle, caustics). It reads worst in the shore foam
    /// band because that is where the painted slot carries the most contrast. The domain warp
    /// applied before the cell lookup BENDS those seams a little, which is why they read as
    /// near-straight lines rather than as a perfect grid — and why they never looked like a tile
    /// repeat to the eye. See <see cref="LegacyTwoVariantWeight"/>, kept ONLY so the tests can
    /// MEASURE the jump rather than assert an adjective (the <c>FoamBuffer.CameraRelativeOrigin</c>
    /// idiom).</para>
    ///
    /// <para><b>The fix, and why it is the standard one.</b> Blend the FOUR cell-corner variants by
    /// bilinear weights that VANISH at the boundary they are farthest from. Then the two sides of
    /// any edge agree by construction: at <c>fx → 1</c> only the <c>(i+1,·)</c> corners survive, and
    /// those are exactly the <c>(i',·) = (i+1,·)</c> corners the next cell reads at <c>fx → 0</c>.
    /// Using a smoothstep ramp (whose derivative is 0 at both ends) makes the join C¹, so no
    /// gradient edge is left behind either.</para>
    ///
    /// <para><b>⚠️ Cost, stated rather than hidden (rule 7).</b> Four corner taps instead of two, so
    /// a painted slot costs <b>5</b> texture fetches instead of 3 while <c>_UntileStrength</c> &gt; 0
    /// (the un-untiled <c>raw</c> tap is unchanged and still the ONLY tap at strength 0). That is
    /// +2 fetches per painted slot; the correctness is not obtainable with two variants, because two
    /// variants cannot cover the four corners a 2-D lattice joins at. Dropping
    /// <c>_UntileStrength</c> to 0 restores the single-tap path exactly.</para>
    ///
    /// <para>Everything here is pure, allocation-free and deterministic in its own arguments — the
    /// blend is a property of the LATTICE, not of any particular texture, which is what lets the
    /// tests pin continuity with an arbitrary variant field instead of a hash the shader would have
    /// to agree with. Visual-only: nothing in this path touches depth, <c>clip()</c>,
    /// <c>_WaterLevel</c> or the sim (P1 integrity, CLAUDE.md rule 5).</para>
    /// </summary>
    public static class WaterUntile
    {
        /// <summary>
        /// The per-cell world translation applied to a corner variant, in metres — the shader's
        /// <c>Hash22(corner) * 64.0</c>. Several tiles' worth, so neighbouring corners land on
        /// decorrelated parts of the tile.
        /// </summary>
        public const float VariantOffsetMeters = 64f;

        /// <summary>
        /// <c>smoothstep(0,1,x)</c> — <c>x²(3−2x)</c>, clamped. The ramp the corner weights are built
        /// from. Its derivative is 0 at BOTH ends, which is what makes the cell join C¹ and not just
        /// C⁰: the seam leaves no gradient edge behind either.
        /// </summary>
        public static float SmoothStep01(float x)
        {
            float t = Mathf.Clamp01(x);
            return t * t * (3f - 2f * t);
        }

        /// <summary>
        /// The four corner weights for a position <paramref name="frac"/> inside its repeat-cell
        /// (both components in 0..1), ordered <c>(00, 10, 01, 11)</c> to match the corner offsets
        /// <c>(i,j)</c>, <c>(i+1,j)</c>, <c>(i,j+1)</c>, <c>(i+1,j+1)</c>.
        ///
        /// <para>They are a partition of unity (they always sum to exactly 1, so the blend can never
        /// brighten or darken the slot), non-negative, and each one is 0 on the two cell edges it
        /// does not touch. That last property is the whole fix: it is what makes the value at an
        /// edge depend ONLY on the two corners the two cells share.</para>
        /// </summary>
        public static void CornerWeights(Vector2 frac,
                                         out float w00, out float w10, out float w01, out float w11)
        {
            float bx = SmoothStep01(frac.x);
            float by = SmoothStep01(frac.y);
            w00 = (1f - bx) * (1f - by);
            w10 = bx * (1f - by);
            w01 = (1f - bx) * by;
            w11 = bx * by;
        }

        /// <summary>
        /// The blended value of a per-cell variant field at a tile-space coordinate — the shader's
        /// untiled result with the texture fetch abstracted away.
        ///
        /// <para><paramref name="variantValue"/> stands in for "what the tile looks like when
        /// translated by this corner's hash": the blend's continuity must hold for EVERY such field,
        /// so the tests hand it an arbitrary hash rather than trying to reproduce the shader's
        /// <c>Hash22</c>. Pure; the delegate is the caller's.</para>
        /// </summary>
        public static float Blend(Vector2 tileUv, Func<Vector2Int, float> variantValue)
        {
            if (variantValue == null) return 0f;
            Vector2Int cell = CellOf(tileUv);
            Vector2 frac = FracOf(tileUv);
            CornerWeights(frac, out float w00, out float w10, out float w01, out float w11);
            return variantValue(cell) * w00
                 + variantValue(new Vector2Int(cell.x + 1, cell.y)) * w10
                 + variantValue(new Vector2Int(cell.x, cell.y + 1)) * w01
                 + variantValue(new Vector2Int(cell.x + 1, cell.y + 1)) * w11;
        }

        /// <summary>
        /// ⚠️ <b>THE WRONG ANSWER, kept so the tests can measure how wrong.</b> The shipped
        /// two-variant blend weight, verbatim: <c>smoothstep(0.2,0.8,fx)·0.5 +
        /// smoothstep(0.2,0.8,fy)·0.5</c>. It never reaches 0 or 1 on an edge, so the two cells
        /// meeting there weight their (entirely different) variant pairs differently — the seam.
        /// Nothing ships calling this. Sibling of <c>FoamBuffer.CameraRelativeOrigin</c>.
        /// </summary>
        public static float LegacyTwoVariantWeight(Vector2 frac)
        {
            return SmoothStepRange(0.2f, 0.8f, frac.x) * 0.5f
                 + SmoothStepRange(0.2f, 0.8f, frac.y) * 0.5f;
        }

        /// <summary>
        /// ⚠️ <b>The shipped two-variant blend, whole</b> — the cell's own variant lerped toward the
        /// DIAGONAL neighbour's by <see cref="LegacyTwoVariantWeight"/>. Kept beside the new
        /// <see cref="Blend"/> so a test can put the two on the same variant field and report the
        /// boundary jump as a NUMBER. Nothing ships calling this.
        /// </summary>
        public static float LegacyBlend(Vector2 tileUv, Func<Vector2Int, float> variantValue)
        {
            if (variantValue == null) return 0f;
            Vector2Int cell = CellOf(tileUv);
            Vector2 frac = FracOf(tileUv);
            float w = LegacyTwoVariantWeight(frac);
            float a = variantValue(cell);
            float b = variantValue(new Vector2Int(cell.x + 1, cell.y + 1));
            return Mathf.Lerp(a, b, w);
        }

        /// <summary>Which repeat-cell a tile-space coordinate falls in (the shader's
        /// <c>floor(uv)</c>). Negative coordinates floor away from zero, like HLSL's.</summary>
        public static Vector2Int CellOf(Vector2 tileUv)
            => new Vector2Int(Mathf.FloorToInt(tileUv.x), Mathf.FloorToInt(tileUv.y));

        /// <summary>The position inside the cell, 0..1 in both axes (the shader's
        /// <c>frac(uv)</c>).</summary>
        public static Vector2 FracOf(Vector2 tileUv)
            => new Vector2(tileUv.x - Mathf.Floor(tileUv.x), tileUv.y - Mathf.Floor(tileUv.y));

        /// <summary>HLSL's three-argument <c>smoothstep</c>, for the legacy weight only.</summary>
        private static float SmoothStepRange(float edge0, float edge1, float x)
        {
            float t = Mathf.Clamp01((x - edge0) / Mathf.Max(edge1 - edge0, 1e-6f));
            return t * t * (3f - 2f * t);
        }
    }
}
