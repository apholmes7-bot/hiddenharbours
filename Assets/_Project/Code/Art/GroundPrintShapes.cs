using HiddenHarbours.Core;
using UnityEngine;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// <b>The placeholder print art, generated in code</b> (no binary art committed). One square
    /// alpha mask: the LEFT half is a boot sole (toe at the top, a heel block and an arch gap), the
    /// RIGHT half one repeat of a chevron tyre tread. White with an alpha mask, so the vertex colour
    /// (the surface's palette tint) is the only colour, and point-filtered so it stays on the pixel
    /// grid. A painted 2-frame sheet would replace this one-for-one (see the PR's art list).
    /// The texture is an ATLAS of one such square per ground style (<see cref="Atlas"/>), so each ground
    /// carries its own breakup (grass broken, mud crisp) and it is still one texture, one draw call.
    /// </summary>
    public static class GroundPrintShapes
    {
        /// <summary>The mask, row-major from the BOTTOM row (Unity texture order), <paramref name="size"/>²
        /// cells; true = ink. <paramref name="size"/> is rounded up to an even number of at least 4.</summary>
        public static bool[] Mask(int size)
        {
            size = EvenSize(size);
            int half = size / 2;
            var mask = new bool[size * size];
            for (int y = 0; y < size; y++)
            {
                float v = (y + 0.5f) / size;            // 0 heel .. 1 toe
                for (int x = 0; x < half; x++)
                {
                    float u = (x + 0.5f) / half;        // 0 .. 1 across the sole
                    mask[y * size + x] = BootInk(u, v);
                    mask[y * size + half + x] = TreadInk(u, v);
                }
            }
            return mask;
        }

        public static int EvenSize(int size) => Mathf.Max(4, size + (size & 1));

        /// <summary>
        /// <see cref="Mask(int)"/> with a ground's breakup applied: an ink cell on the OUTLINE (a 4-neighbour
        /// outside the ink) is left out when its fixed hash falls under <paramref name="edgeBreakup"/>, an
        /// inside cell under <paramref name="interiorBreakup"/>. Both 0 returns the crisp mask unchanged
        /// (mud keeps its stamp). Deterministic in (size, cell, <paramref name="seed"/>): no Random.
        /// The tread wraps top to bottom (it repeats along the mark), the boot does not.
        /// </summary>
        public static bool[] Mask(int size, float edgeBreakup, float interiorBreakup, int seed)
        {
            bool[] crisp = Mask(size);
            if (edgeBreakup <= 0f && interiorBreakup <= 0f) return crisp;
            size = EvenSize(size);
            int half = size / 2;
            var mask = (bool[])crisp.Clone();
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                int i = y * size + x;
                if (!crisp[i]) continue;
                bool tread = x >= half;
                int x0 = tread ? half : 0;
                bool edge = !Ink(crisp, size, x0, half, x - 1, y, tread) || !Ink(crisp, size, x0, half, x + 1, y, tread)
                         || !Ink(crisp, size, x0, half, x, y - 1, tread) || !Ink(crisp, size, x0, half, x, y + 1, tread);
                if (Hash01(x, y, seed) < (edge ? edgeBreakup : interiorBreakup)) mask[i] = false;
            }
            return mask;
        }

        private static bool Ink(bool[] m, int size, int x0, int half, int x, int y, bool wrapY)
        {
            if (x < x0 || x >= x0 + half) return false;
            if (y < 0 || y >= size)
            {
                if (!wrapY) return false;
                y = (y + size) % size;
            }
            return m[y * size + x];
        }

        /// <summary>A fixed per-cell value in [0, 1) (integer hash: identical on every machine).</summary>
        public static float Hash01(int x, int y, int seed)
        {
            unchecked
            {
                uint h = (uint)x * 0x8DA6B343u ^ (uint)y * 0xD8163841u ^ (uint)(seed + 1) * 0xCB1AB31Fu;
                h ^= h >> 15; h *= 0x2C1B3C6Du; h ^= h >> 12; h *= 0x297A2D39u; h ^= h >> 15;
                return (h & 0xFFFFFF) / 16777216f;
            }
        }

        /// <summary>
        /// The atlas: one <paramref name="size"/>-wide column per entry of <paramref name="styles"/>, in
        /// order (boot half then tread half), each with that ground's breakup. Row-major from the bottom,
        /// width = size x columns. No styles = one crisp column.
        /// </summary>
        public static bool[] Atlas(int size, GroundPrintSurfaceStyle[] styles, out int width)
        {
            size = EvenSize(size);
            int columns = styles != null && styles.Length > 0 ? styles.Length : 1;
            width = size * columns;
            var atlas = new bool[width * size];
            for (int c = 0; c < columns; c++)
            {
                bool[] m = styles != null && styles.Length > 0
                    ? Mask(size, styles[c].EdgeBreakup, styles[c].InteriorBreakup, c)
                    : Mask(size);
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                        atlas[y * width + c * size + x] = m[y * size + x];
            }
            return atlas;
        }

        // A boot sole: a heel block (bottom 30%), a narrow arch gap, a wide sole that tapers to the toe.
        private static bool BootInk(float u, float v)
        {
            float c = Mathf.Abs(u - 0.5f) * 2f;         // 0 centre .. 1 edge
            if (v < 0.3f) return c < 0.75f;             // heel
            if (v < 0.42f) return false;                 // arch gap between heel and sole
            if (v < 0.85f) return c < 0.95f;            // ball of the foot
            return c < 0.6f;                             // toe
        }

        // A chevron tread: two lug rows per repeat, offset across the tyre so it reads as a V.
        private static bool TreadInk(float u, float v)
        {
            float c = Mathf.Abs(u - 0.5f) * 2f;
            float phase = Mathf.Repeat(v + c * 0.25f, 0.5f);
            return phase < 0.3f;
        }

        /// <summary>Build the point-filtered atlas texture from <see cref="Atlas"/> (runtime only).</summary>
        public static Texture2D BuildTexture(int size, GroundPrintSurfaceStyle[] styles)
        {
            size = EvenSize(size);
            bool[] mask = Atlas(size, styles, out int width);
            var pixels = new Color32[mask.Length];
            for (int i = 0; i < mask.Length; i++) pixels[i] = new Color32(255, 255, 255, mask[i] ? (byte)255 : (byte)0);
            var tex = new Texture2D(width, size, TextureFormat.RGBA32, false)
            {
                name = "GroundPrintShapes",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Repeat,
                hideFlags = HideFlags.DontSave,
            };
            tex.SetPixels32(pixels);
            tex.Apply(false, true);
            return tex;
        }
    }
}
