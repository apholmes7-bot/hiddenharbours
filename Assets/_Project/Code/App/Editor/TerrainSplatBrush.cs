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
    /// <para><b>The data model (fixed — the shader already consumes it).</b> Eighteen materials, one
    /// 0..1 channel each, packed across five RGBA splat maps in the CANONICAL order the shader,
    /// <see cref="HiddenHarbours.Art.Editor.TerrainTexArrayBuilder"/> and
    /// <c>TerrainSplatBandPinTests</c> all pin: A.rgba = Grass/Marram/Sand/Shingle,
    /// B.rgba = Ripple/Shelf/Silt/Dirt, C.rgba = Marsh/Sedge/Foreshore/Talus,
    /// D.rgba = Ledge/Rockweed/Musselbed/Oysterreef, E.rg = Eelgrass/Irishmoss. A channel's value
    /// is BOTH the blend weight against the height bands AND the position on that material's
    /// intensity ladder (0 = _Lo sparse · 0.5 = base · 1 = _Hi rank — the kit README §2), which is
    /// why one channel per material is enough: a footpath is a brush stroke on intensity, not a new
    /// slot — and so is a raked-over oyster bottom.</para>
    ///
    /// <para><b>Exclusive painting.</b> The shader renormalises when channels sum past 1, but a
    /// PAINTER expects a stroke of dirt over silt to REPLACE the silt, not stack under it — so an
    /// exclusive dab fades every other channel toward 0 at the same rate the painted channel moves
    /// toward its target. Repeated strokes therefore converge on clean single-material paint.</para>
    /// </summary>
    public static class TerrainSplatBrush
    {
        /// <summary>Eighteen paintable materials — the canonical splat order 0..17 (APPEND ONLY,
        /// never reorder: the shader's channel unpack, the pin tests and every committed splat PNG
        /// depend on it). 10..13 arrived with kit v2, 14..17 with kit v3's REEF BEDS; the kit's
        /// cliff FACE materials (Sandstone, Bank) are deliberately absent — they are not painted on
        /// the ground.
        ///
        /// <para>The four beds are ground MATERIALS, not props or scatter, and that is the kit's
        /// own ruling (README §6): at 32 px/m a mussel is two texels long, so what reads is the
        /// grain, the clumping and the gaps — the animals ARE the substrate. Each bed's floor is
        /// baked into its own tile (Musselbed's anoxic mud, Eelgrass's silted sand, Irishmoss's
        /// held-down cobble), which is why a SPARSE bed is a low rung on the ladder and never a
        /// half-weight wash of shell colour over clean sand.</para></summary>
        public static readonly string[] MaterialNames =
        {
            "Grass", "Marram", "Sand", "Shingle", "Ripple", "Shelf", "Silt",
            "Dirt", "Marsh", "Sedge", "Foreshore", "Talus", "Ledge", "Rockweed",
            "Musselbed", "Oysterreef", "Eelgrass", "Irishmoss",
            "Lawn",
        };

        public const int MaterialCount = 19;

        /// <summary>Five RGBA splat maps = 20 channels for 19 materials. The fifth (E) arrived with
        /// kit v3: the two slots D.b/D.a left free at v2 took Musselbed and Oysterreef, and the
        /// remaining two beds needed a new map. <b>E.b went to Lawn on 2026-08-26, so E.a is the
        /// LAST free slot in the kit</b> — the next material after it needs a sixth splat map, which
        /// is a bigger change than it sounds (every region's committed PNGs, the surface's binding,
        /// and the byte-zero gate all move).</summary>
        public const int TextureCount = 5;

        /// <summary>The splat texture file suffixes, index-aligned with <see cref="TextureOf"/>.</summary>
        public static readonly string[] TextureSuffixes = { "A", "B", "C", "D", "E" };

        private static readonly string[] ChannelNames = { "r", "g", "b", "a" };

        /// <summary>Which of the five splat textures carries this material's channel (0=A .. 4=E).</summary>
        public static int TextureOf(int material) => material / 4;

        /// <summary>Which RGBA channel within that texture (0=r 1=g 2=b 3=a).</summary>
        public static int ChannelOf(int material) => material % 4;

        /// <summary>Inverse of <see cref="TextureOf"/>/<see cref="ChannelOf"/> — valid while the
        /// result is &lt; <see cref="MaterialCount"/> (E.b / E.a are the two channels still
        /// unused: 20 slots exist, 18 are spoken for).</summary>
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
        /// Apply one brush dab to the splat pixel buffers — <paramref name="layers"/> holds one
        /// <see cref="TextureCount"/>-length array per splat map, all sharing <paramref name="width"/>
        /// × <paramref name="height"/> over the SAME world rect as the height map. For
        /// <paramref name="material"/> ≥ 0 the material's channel lerps toward
        /// <paramref name="target"/> by flow × falloff weight; with <paramref name="exclusive"/>
        /// (and a positive target) every OTHER channel at the texel fades toward 0 at the same
        /// rate. <paramref name="material"/> = <see cref="EraseAllMaterials"/> fades ALL channels
        /// toward 0 (back to bands-only ground). Deterministic — same inputs, same buffers.
        ///
        /// <para>Layers rather than named a/b/c arguments: the exclusive and erase paths must touch
        /// EVERY map, so a map added by hand at one of the two sites and forgotten at the other
        /// would leak paint the painter thought it had replaced. The loop cannot forget one.</para>
        /// </summary>
        public static void Dab(Color[][] layers, int width, int height,
            Vector2 worldMin, Vector2 worldSize, Vector2 center, float radiusMetres,
            float falloff01, int material, float target, float flow, bool exclusive)
        {
            if (layers == null || layers.Length < TextureCount || width <= 0 || height <= 0) return;
            for (int t = 0; t < TextureCount; t++)
                if (layers[t] == null) return;

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

                WriteTexel(layers, y * width + x, material, target, k, exclusive);
            }
        }

        /// <summary>
        /// One texel's worth of the painting contract, in ONE place: the erase-all path, the
        /// exclusive fade, and the flow lerp. <see cref="Dab"/> reaches it through a radial falloff
        /// and <see cref="PaintField"/> through a per-texel coverage map, but neither owns the rule
        /// — a second copy is how "exclusive" comes to mean two different things.
        /// </summary>
        private static void WriteTexel(Color[][] layers, int idx, int material,
            float target, float k, bool exclusive)
        {
            if (material < 0)
            {
                // Erase ALL materials — every channel back toward "unpainted" (bands-only).
                float eraseKeep = 1f - k;
                for (int t = 0; t < TextureCount; t++) layers[t][idx] *= eraseKeep;
                return;
            }

            Color[] buf = layers[TextureOf(material)];
            int ch = ChannelOf(material);
            float painted = Step(GetChannel(buf[idx], ch), target, k);

            if (exclusive && target > 0f)
            {
                // The painter's contract: what this stroke lays down, the others yield to.
                float keep = 1f - k;
                for (int t = 0; t < TextureCount; t++) layers[t][idx] *= keep;
            }
            buf[idx] = WithChannel(buf[idx], ch, painted);
        }

        /// <summary>
        /// Paint one material across the WHOLE map from a per-texel coverage map — the band-shaped
        /// sibling of <see cref="Dab"/>, for ground that is placed by a rule rather than by a
        /// stroke (St Peters' intertidal families: a foreshore is a zone, not a blob).
        ///
        /// <para><paramref name="coverage"/> is width × height in the SAME texel order as the splat
        /// buffers, and plays exactly the role a dab's falloff weight does: it is the fraction of
        /// the texel this material takes over, so a coverage that eases to 0 at a band's edge
        /// feathers the paint AND lets the other materials keep their share there. The painted
        /// channel value is <paramref name="target"/> × coverage, which is what makes one number
        /// serve as both blend weight and ladder position (the kit README §2 contract).</para>
        ///
        /// <para>Deterministic: no hashing, no ordering subtleties — texel i reads coverage[i].</para>
        /// </summary>
        public static void PaintField(Color[][] layers, int width, int height,
            int material, float target, float[] coverage, bool exclusive)
        {
            if (layers == null || layers.Length < TextureCount || width <= 0 || height <= 0) return;
            for (int t = 0; t < TextureCount; t++)
                if (layers[t] == null) return;
            if (coverage == null || coverage.Length < width * height) return;

            int count = width * height;
            for (int i = 0; i < count; i++)
            {
                float k = Mathf.Clamp01(coverage[i]);
                if (k <= 0f) continue;
                WriteTexel(layers, i, material, target, k, exclusive);
            }
        }

        /// <summary>
        /// Stamp dabs (flow 1) along a polyline at a fixed world-metre spacing — the shared stroke
        /// code the starter paint uses, so what the menu authors and what the brush paints are the
        /// SAME footprint math. Spacing carries across vertices so the line's dab rhythm has no
        /// seam at a bend. Deterministic.
        /// </summary>
        public static void PaintPolyline(Color[][] layers, int width, int height,
            Vector2 worldMin, Vector2 worldSize, IReadOnlyList<Vector2> points,
            float dabSpacingMetres, float radiusMetres, float falloff01,
            int material, float target, bool exclusive)
        {
            if (points == null || points.Count == 0) return;
            float spacing = Mathf.Max(dabSpacingMetres, 0.05f);

            Dab(layers, width, height, worldMin, worldSize, points[0],
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
                    Dab(layers, width, height, worldMin, worldSize, from + dir * d,
                        radiusMetres, falloff01, material, target, 1f, exclusive);
                    d += spacing;
                }
                carry = len - (d - spacing);
            }
        }

        // ============================ CHANNEL HELPERS ============================

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
