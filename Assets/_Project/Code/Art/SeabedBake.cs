using System;
using UnityEngine;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// The pure, headless-testable core of the <c>_SeabedTex</c> bake (ADR 0027 #7) — the
    /// <b>world ↔ texel mapping</b> the editor tool writes with and the water shader reads with.
    /// Both sides must agree exactly or the bottom slides against the coast it belongs to, so the
    /// mapping lives HERE, in one place, with tests — the same discipline
    /// <c>World.PaintedHeightField</c> keeps for the height map it mirrors.
    ///
    /// <para><b>The rect is the height map's rect.</b> <c>_SeabedTex</c> is baked over the SAME
    /// world rect as <c>_HeightTex</c> — <c>_HeightWorldMin</c> / <c>_HeightWorldSize</c>, ADR
    /// 0014's established pattern — so the shader needs <b>no new uniform</b> to place it: the two
    /// textures index off one rect and the bottom is registered to the elevation that decides how
    /// deep it is. (Like the height map it is square-over-a-non-square rect by convention; the
    /// per-axis metres/texel differ and that is fine — the sample is point-filtered and posterized
    /// downstream anyway.)</para>
    ///
    /// <para><b>Alpha is COVERAGE, not opacity.</b> A texel's alpha says "the bake found a bottom
    /// here". Where the owner's terrain painted no ground tile (the Deep / Channel types
    /// deliberately CLEAR tiles so the water shows), coverage is 0 and the shader composites
    /// nothing — so open water with no baked bed is unchanged by construction, and the σ → 0 edge
    /// only ever reaches the shallow band that actually has a painted bottom.</para>
    ///
    /// <para>Authored DATA, read at runtime and never written (rule 5): the bake is an editor step
    /// like the painted height map, and nothing here enters the save.</para>
    /// </summary>
    public static class SeabedBake
    {
        /// <summary>Floor for the world rect size so a degenerate rect can never divide by zero
        /// (mirrors the shader's <c>max(_HeightWorldSize.xy, 1e-3)</c>).</summary>
        public const float MinWorldSize = 1e-3f;

        /// <summary>The bake resolution the tool defaults to. See the design doc §17.7 for the
        /// measured budget; 512² RGBA32 point-filtered, no mips.</summary>
        public const int DefaultResolution = 512;

        /// <summary>
        /// The world position a texel's CENTRE samples — <c>worldMin + (i + 0.5)/res · worldSize</c>.
        /// This is the exact inverse of the shader's
        /// <c>uv = (worldXY − _HeightWorldMin) / _HeightWorldSize</c> followed by a point fetch, so
        /// baking at these positions puts each painted bottom cell under the water column that is
        /// actually above it.
        /// </summary>
        public static Vector2 TexelCenterWorld(int x, int y, int width, int height,
                                               Vector2 worldMin, Vector2 worldSize)
        {
            int w = Mathf.Max(width, 1);
            int h = Mathf.Max(height, 1);
            Vector2 size = SafeSize(worldSize);
            return new Vector2(worldMin.x + (x + 0.5f) / w * size.x,
                               worldMin.y + (y + 0.5f) / h * size.y);
        }

        /// <summary>
        /// The shader's own read, mirrored: world → uv over the rect. Outside 0..1 the shader zeroes
        /// coverage rather than smearing the clamped edge texel across the whole sea
        /// (<see cref="InRect"/>).
        /// </summary>
        public static Vector2 WorldToUv(Vector2 worldXY, Vector2 worldMin, Vector2 worldSize)
        {
            Vector2 size = SafeSize(worldSize);
            return new Vector2((worldXY.x - worldMin.x) / size.x,
                               (worldXY.y - worldMin.y) / size.y);
        }

        /// <summary>Whether a uv falls inside the baked rect (the shader's coverage guard).</summary>
        public static bool InRect(Vector2 uv)
        {
            return uv.x >= 0f && uv.x <= 1f && uv.y >= 0f && uv.y <= 1f;
        }

        /// <summary>
        /// Build the bake buffer: one <paramref name="sampleWorld"/> call per texel centre, in the
        /// row-major bottom-up order <c>Texture2D.SetPixels32</c> expects. The sampler returns the
        /// bottom's ALBEDO in rgb and its COVERAGE in a; <paramref name="fill"/> is what an
        /// uncovered texel gets (default transparent = "no bottom here").
        /// </summary>
        /// <exception cref="ArgumentNullException">No sampler supplied.</exception>
        public static Color32[] Build(int width, int height, Vector2 worldMin, Vector2 worldSize,
                                      Func<Vector2, Color> sampleWorld, Color fill)
        {
            if (sampleWorld == null) throw new ArgumentNullException(nameof(sampleWorld));
            int w = Mathf.Max(width, 1);
            int h = Mathf.Max(height, 1);
            var pixels = new Color32[w * h];
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                Vector2 world = TexelCenterWorld(x, y, w, h, worldMin, worldSize);
                Color c = sampleWorld(world);
                if (c.a <= 0f) c = fill;
                pixels[y * w + x] = c;
            }
            return pixels;
        }

        private static Vector2 SafeSize(Vector2 worldSize)
        {
            return new Vector2(Mathf.Max(Mathf.Abs(worldSize.x), MinWorldSize),
                               Mathf.Max(Mathf.Abs(worldSize.y), MinWorldSize));
        }
    }
}
