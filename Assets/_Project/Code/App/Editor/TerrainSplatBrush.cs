#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;

namespace HiddenHarbours.App.Editor
{
    /// <summary>
    /// The PURE math of the splat-material brush (ADR 0028 PR 2 addendum) — channel packing,
    /// falloff, flow, and the dab itself, with no UnityEditor and no asset I/O, so every rule the
    /// brush enforces is testable headless (the same split the height brush's
    /// <c>PaintedHeightField</c> encode/decode enjoys).
    ///
    /// <para><b>The data model (fixed — the shader already consumes it).</b> Ten materials, one
    /// 0..1 channel each, packed across three RGBA splat maps in the CANONICAL order the shader,
    /// <see cref="HiddenHarbours.Art.Editor.TerrainTexArrayBuilder"/> and
    /// <c>TerrainSplatBandPinTests</c> all pin: A.rgba = Grass/Marram/Sand/Shingle,
    /// B.rgba = Ripple/Shelf/Silt/Dirt, C.rg = Marsh/Sedge. A channel's value is BOTH the blend
    /// weight against the height bands AND the position on that material's intensity ladder
    /// (0 = _Lo sparse · 0.5 = base · 1 = _Hi rank — the kit README §2), which is why one channel
    /// per material is enough: a footpath is a brush stroke on intensity, not a new slot.</para>
    ///
    /// <para><b>Exclusive painting.</b> The shader renormalises when channels sum past 1, but a
    /// PAINTER expects a stroke of dirt over silt to REPLACE the silt, not stack under it — so an
    /// exclusive dab fades every other channel toward 0 at the same rate the painted channel moves
    /// toward its target. Repeated strokes therefore converge on clean single-material paint.</para>
    /// </summary>
    public static class TerrainSplatBrush
    {
        /// <summary>Ten paintable materials — the canonical splat order 0..9 (never reorder: the
        /// shader's channel unpack, the pin tests and every committed splat PNG depend on it).</summary>
        public static readonly string[] MaterialNames =
            { "Grass", "Marram", "Sand", "Shingle", "Ripple", "Shelf", "Silt", "Dirt", "Marsh", "Sedge" };

        public const int MaterialCount = 10;
        public const int TextureCount = 3;

        /// <summary>The splat texture file suffixes, index-aligned with <see cref="TextureOf"/>.</summary>
        public static readonly string[] TextureSuffixes = { "A", "B", "C" };

        private static readonly string[] ChannelNames = { "r", "g", "b", "a" };

        /// <summary>Which of the three splat textures carries this material's channel (0=A 1=B 2=C).</summary>
        public static int TextureOf(int material) => material / 4;

        /// <summary>Which RGBA channel within that texture (0=r 1=g 2=b 3=a).</summary>
        public static int ChannelOf(int material) => material % 4;

        /// <summary>Inverse of <see cref="TextureOf"/>/<see cref="ChannelOf"/> — valid while the
        /// result is &lt; <see cref="MaterialCount"/> (C.b / C.a are unused).</summary>
        public static int MaterialOf(int texture, int channel) => texture * 4 + channel;

        /// <summary>Human label for the picker/tooltip, e.g. material 7 → "SplatB.a".</summary>
        public static string ChannelLabel(int material) =>
            "Splat" + TextureSuffixes[TextureOf(material)] + "." + ChannelNames[ChannelOf(material)];

        // ============================ FALLOFF + FLOW ============================

        /// <summary>
        /// Brush weight at a distance from the centre: 1 across the hard core, smoothstepping to 0
        /// at the radius. <paramref name="falloff01"/> is the FRACTION of the radius that is soft
        /// edge — 0 = a hard-edged stamp, 1 = soft from the centre out.
        /// </summary>
        public static float Weight(float distMetres, float radiusMetres, float falloff01)
        {
            if (radiusMetres <= 0f || distMetres >= radiusMetres) return 0f;
            float core = radiusMetres * (1f - Mathf.Clamp01(falloff01));
            if (distMetres <= core) return 1f;
            return 1f - Mathf.SmoothStep(0f, 1f, (distMetres - core) / Mathf.Max(radiusMetres - core, 1e-4f));
        }

        /// <summary>One flow step: lerp the channel toward the target by k (clamped). Flow &lt; 1
        /// lets the owner BUILD UP a stroke gradually; repeated dabs converge on the target.</summary>
        public static float Step(float current, float target, float k) =>
            Mathf.Lerp(current, target, Mathf.Clamp01(k));

        // ============================ THE DAB ============================

        /// <summary>Erase-all sentinel for <see cref="Dab"/>'s material argument.</summary>
        public const int EraseAllMaterials = -1;

        /// <summary>
        /// Apply one brush dab to the three splat pixel buffers (all share <paramref name="width"/>
        /// × <paramref name="height"/> over the SAME world rect as the height map). For
        /// <paramref name="material"/> ≥ 0 the material's channel lerps toward
        /// <paramref name="target"/> by flow × falloff weight; with <paramref name="exclusive"/>
        /// (and a positive target) every OTHER channel at the texel fades toward 0 at the same
        /// rate. <paramref name="material"/> = <see cref="EraseAllMaterials"/> fades ALL channels
        /// toward 0 (back to bands-only ground). Deterministic — same inputs, same buffers.
        /// </summary>
        public static void Dab(Color[] a, Color[] b, Color[] c, int width, int height,
            Vector2 worldMin, Vector2 worldSize, Vector2 center, float radiusMetres,
            float falloff01, int material, float target, float flow, bool exclusive)
        {
            if (a == null || b == null || c == null || width <= 0 || height <= 0) return;

            // The texel box the brush touches (per-axis, so a non-square map paints a true circle).
            float metresPerTexelX = worldSize.x / width;
            float metresPerTexelY = worldSize.y / height;
            int cx = Mathf.RoundToInt((center.x - worldMin.x) / worldSize.x * width - 0.5f);
            int cy = Mathf.RoundToInt((center.y - worldMin.y) / worldSize.y * height - 0.5f);
            int rx = Mathf.CeilToInt(radiusMetres / Mathf.Max(metresPerTexelX, 1e-4f));
            int ry = Mathf.CeilToInt(radiusMetres / Mathf.Max(metresPerTexelY, 1e-4f));

            for (int y = cy - ry; y <= cy + ry; y++)
            for (int x = cx - rx; x <= cx + rx; x++)
            {
                if (x < 0 || x >= width || y < 0 || y >= height) continue;
                float wx = worldMin.x + (x + 0.5f) / width * worldSize.x;
                float wy = worldMin.y + (y + 0.5f) / height * worldSize.y;
                float dist = Vector2.Distance(new Vector2(wx, wy), center);
                float k = Mathf.Clamp01(flow * Weight(dist, radiusMetres, falloff01));
                if (k <= 0f) continue;

                int idx = y * width + x;
                if (material < 0)
                {
                    // Erase ALL materials — every channel back toward "unpainted" (bands-only).
                    float keep = 1f - k;
                    a[idx] *= keep;
                    b[idx] *= keep;
                    c[idx] *= keep;
                    continue;
                }

                Color[] buf = BufferOf(a, b, c, material);
                int ch = ChannelOf(material);
                float painted = Step(GetChannel(buf[idx], ch), target, k);

                if (exclusive && target > 0f)
                {
                    // The painter's contract: what this stroke lays down, the others yield to.
                    float keep = 1f - k;
                    a[idx] *= keep;
                    b[idx] *= keep;
                    c[idx] *= keep;
                }
                buf[idx] = WithChannel(buf[idx], ch, painted);
            }
        }

        /// <summary>
        /// Stamp dabs (flow 1) along a polyline at a fixed world-metre spacing — the shared stroke
        /// code the starter paint uses, so what the menu authors and what the brush paints are the
        /// SAME footprint math. Spacing carries across vertices so the line's dab rhythm has no
        /// seam at a bend. Deterministic.
        /// </summary>
        public static void PaintPolyline(Color[] a, Color[] b, Color[] c, int width, int height,
            Vector2 worldMin, Vector2 worldSize, IReadOnlyList<Vector2> points,
            float dabSpacingMetres, float radiusMetres, float falloff01,
            int material, float target, bool exclusive)
        {
            if (points == null || points.Count == 0) return;
            float spacing = Mathf.Max(dabSpacingMetres, 0.05f);

            Dab(a, b, c, width, height, worldMin, worldSize, points[0],
                radiusMetres, falloff01, material, target, 1f, exclusive);

            float carry = 0f;
            for (int i = 1; i < points.Count; i++)
            {
                Vector2 from = points[i - 1], to = points[i];
                float len = Vector2.Distance(from, to);
                if (len <= 1e-5f) continue;
                Vector2 dir = (to - from) / len;
                float d = spacing - carry;
                while (d <= len)
                {
                    Dab(a, b, c, width, height, worldMin, worldSize, from + dir * d,
                        radiusMetres, falloff01, material, target, 1f, exclusive);
                    d += spacing;
                }
                carry = len - (d - spacing);
            }
        }

        // ============================ CHANNEL HELPERS ============================

        private static Color[] BufferOf(Color[] a, Color[] b, Color[] c, int material)
        {
            switch (TextureOf(material))
            {
                case 0: return a;
                case 1: return b;
                default: return c;
            }
        }

        /// <summary>Read one RGBA channel by index (0=r 1=g 2=b 3=a).</summary>
        public static float GetChannel(Color c, int channel)
        {
            switch (channel)
            {
                case 0: return c.r;
                case 1: return c.g;
                case 2: return c.b;
                default: return c.a;
            }
        }

        /// <summary>Return the colour with one channel replaced (0=r 1=g 2=b 3=a).</summary>
        public static Color WithChannel(Color c, int channel, float value)
        {
            switch (channel)
            {
                case 0: c.r = value; break;
                case 1: c.g = value; break;
                case 2: c.b = value; break;
                default: c.a = value; break;
            }
            return c;
        }
    }
}
#endif
