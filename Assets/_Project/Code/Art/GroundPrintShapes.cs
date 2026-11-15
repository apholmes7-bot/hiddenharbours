using UnityEngine;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// <b>The placeholder print art, generated in code</b> (no binary art committed). One square
    /// alpha mask: the LEFT half is a boot sole (toe at the top, a heel block and an arch gap), the
    /// RIGHT half one repeat of a chevron tyre tread. White with an alpha mask, so the vertex colour
    /// (the surface's palette tint) is the only colour, and point-filtered so it stays on the pixel
    /// grid. A painted 2-frame sheet would replace this one-for-one (see the PR's art list).
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

        /// <summary>Build the point-filtered texture from <see cref="Mask"/> (runtime only).</summary>
        public static Texture2D BuildTexture(int size)
        {
            size = EvenSize(size);
            bool[] mask = Mask(size);
            var pixels = new Color32[mask.Length];
            for (int i = 0; i < mask.Length; i++) pixels[i] = new Color32(255, 255, 255, mask[i] ? (byte)255 : (byte)0);
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
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
