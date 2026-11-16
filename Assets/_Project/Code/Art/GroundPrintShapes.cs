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
            => Mask(size, edgeBreakup, interiorBreakup, edgeBreakup, interiorBreakup, seed);

        /// <summary>
        /// The same with the boot half and the tread half broken by their own amounts. The boot's HEEL
        /// (<see cref="IsHeel"/>) is never broken: at 8 cells a broken heel leaves a detached speck and
        /// the print stops reading as a boot (editor look of 7c48fcb).
        /// </summary>
        public static bool[] Mask(int size, float bootEdge, float bootInterior, float treadEdge, float treadInterior, int seed)
        {
            bool[] crisp = Mask(size);
            if (bootEdge <= 0f && bootInterior <= 0f && treadEdge <= 0f && treadInterior <= 0f) return crisp;
            size = EvenSize(size);
            int half = size / 2;
            var mask = (bool[])crisp.Clone();
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                int i = y * size + x;
                if (!crisp[i]) continue;
                bool tread = x >= half;
                if (!tread && IsHeel(size, y)) continue;
                int x0 = tread ? half : 0;
                bool edge = !Ink(crisp, size, x0, half, x - 1, y, tread) || !Ink(crisp, size, x0, half, x + 1, y, tread)
                         || !Ink(crisp, size, x0, half, x, y - 1, tread) || !Ink(crisp, size, x0, half, x, y + 1, tread);
                float cut = tread ? (edge ? treadEdge : treadInterior) : (edge ? bootEdge : bootInterior);
                if (Hash01(x, y, seed) < cut) mask[i] = false;
            }
            return mask;
        }

        /// <summary>Is row <paramref name="y"/> of a <paramref name="size"/> mask in the boot's heel block?</summary>
        public static bool IsHeel(int size, int y) => (y + 0.5f) / EvenSize(size) < HeelTop;

        // The boot's proportions (fractions of its length, heel at 0): the placeholder art's shape.
        private const float HeelTop = 0.3f, ArchTop = 0.42f, BallTop = 0.85f;

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
                    ? Mask(size, styles[c].EdgeBreakup, styles[c].InteriorBreakup,
                           styles[c].TireEdgeBreakup, styles[c].TireInteriorBreakup, c)
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
            if (v < HeelTop) return c < 0.75f;          // heel
            if (v < ArchTop) return false;               // arch gap between heel and sole
            if (v < BallTop) return c < 0.95f;          // ball of the foot
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
