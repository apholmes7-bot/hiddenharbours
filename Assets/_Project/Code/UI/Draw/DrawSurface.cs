using UnityEngine;

namespace HiddenHarbours.UI
{
    /// <summary>
    /// <b>HHDraw</b> — the minimal C# 2D raster layer the live UI-rig ports draw into (ADR 0025,
    /// Option A: live C# renderers inside the no-JS fence). Deliberately ONLY what the lever + tiller
    /// need now: an integer-raster pixel surface (<c>fillRect</c> spans, constant-alpha source-over
    /// blends, whole-surface composites) and a nearest-neighbour upload into a reused
    /// <see cref="Texture2D"/>. No anti-aliasing anywhere — the rigs' own rule
    /// (<c>ctx.imageSmoothingEnabled = false</c>); no 'lighter', no gradients, no paths until a rig
    /// actually needs them (the sounder/radar slices grow this layer then).
    ///
    /// <para><b>Grown by S2 (the depth sounder):</b> a radial wash (<see cref="BlendRadial"/> — the
    /// night backlight bloom) and a two-stop colour interpolation (<see cref="Lerp"/>) that callers
    /// apply per span, so a linear gradient follows a rounded rect's row insets without a path+clip
    /// pipeline. Still no anti-aliasing anywhere: the one deliberate deviation from the source (canvas
    /// anti-aliases its rounded LCD clip; we do not) is documented at the call site.</para>
    ///
    /// <para><b>Perf discipline (rule 7, the ADR's own consequence note):</b> one reused
    /// <see cref="Color32"/> buffer per surface, zero per-frame allocation once constructed; callers
    /// repaint only on change-detection, never unconditionally. Coordinates are CANVAS convention —
    /// row 0 is the TOP, y grows downward, exactly like the rig source — and
    /// <see cref="ToTexture"/> does the one flip into Unity's bottom-up texture rows.</para>
    /// </summary>
    public sealed class DrawSurface
    {
        public readonly int Width;
        public readonly int Height;

        /// <summary>Row-major, row 0 = TOP (canvas convention). Mutate via the fill/blend methods.</summary>
        public readonly Color32[] Pixels;

        public DrawSurface(int width, int height)
        {
            Width = width;
            Height = height;
            Pixels = new Color32[width * height];
        }

        /// <summary>Fully transparent black (a fresh canvas). <c>Array.Clear</c> rather than a per-element
        /// loop: a fully transparent <see cref="Color32"/> IS all-zero bytes, so this is a memset, and on
        /// the fish finder's 480×660 canvas the difference is measurable in the repaint budget (rule 7).</summary>
        public void Clear() => System.Array.Clear(Pixels, 0, Pixels.Length);

        /// <summary>Opaque axis-aligned fill (the canvas <c>fillRect</c> with a solid colour), clipped.
        /// Row spans go through <c>Array.Fill</c>, which vectorises where the manual loop did not — the
        /// rigs' painters are almost entirely made of these, so it is the hottest line in the layer.</summary>
        public void FillRect(int x, int y, int w, int h, Color32 c)
        {
            int x0 = x < 0 ? 0 : x, y0 = y < 0 ? 0 : y;
            int x1 = x + w, y1 = y + h;
            if (x1 > Width) x1 = Width;
            if (y1 > Height) y1 = Height;
            int span = x1 - x0;
            if (span <= 0) return;
            for (int yy = y0; yy < y1; yy++)
                System.Array.Fill(Pixels, c, yy * Width + x0, span);
        }

        /// <summary>Constant-alpha source-over fill (the canvas <c>rgba(...)</c> fillRect), clipped.
        /// <paramref name="alpha01"/> is the CSS alpha; the fill colour is treated as opaque.</summary>
        public void BlendRect(int x, int y, int w, int h, Color32 c, float alpha01)
        {
            if (alpha01 <= 0f) return;
            if (alpha01 >= 1f) { FillRect(x, y, w, h, c); return; }
            int a = (int)(alpha01 * 255f + 0.5f);
            int x0 = x < 0 ? 0 : x, y0 = y < 0 ? 0 : y;
            int x1 = x + w, y1 = y + h;
            if (x1 > Width) x1 = Width;
            if (y1 > Height) y1 = Height;
            for (int yy = y0; yy < y1; yy++)
            {
                int row = yy * Width;
                for (int xx = x0; xx < x1; xx++) Pixels[row + xx] = Over(Pixels[row + xx], c, a);
            }
        }

        /// <summary>Composite <paramref name="src"/> (same dimensions) over this surface — the canvas
        /// <c>drawImage</c> with an optional <c>globalAlpha</c>. Source pixels carry their own alpha;
        /// <paramref name="globalAlpha01"/> scales it (the shadow pass draws at 0.24).</summary>
        public void Composite(DrawSurface src, float globalAlpha01 = 1f)
        {
            int ga = (int)(Mathf.Clamp01(globalAlpha01) * 255f + 0.5f);
            if (ga <= 0) return;
            for (int i = 0; i < Pixels.Length; i++)
            {
                Color32 s = src.Pixels[i];
                if (s.a == 0) continue;
                int a = ga == 255 ? s.a : (s.a * ga + 127) / 255;
                if (a == 0) continue;
                Pixels[i] = a == 255 ? new Color32(s.r, s.g, s.b, 255) : Over(Pixels[i], s, a);
            }
        }

        /// <summary>
        /// A radial-gradient WASH over a rect — the canvas
        /// <c>createRadialGradient(cx,cy,r0, cx,cy,r1)</c> with an inner colour stop fading to fully
        /// transparent at <paramref name="r1"/> (depthRig.js:215-218, the night backlight bloom). Alpha
        /// is <paramref name="innerAlpha01"/> inside <paramref name="r0"/> and falls linearly to 0 at
        /// <paramref name="r1"/>; outside <paramref name="r1"/> nothing is drawn (canvas extends the last
        /// stop, which is transparent).
        /// </summary>
        public void BlendRadial(int x, int y, int w, int h, double cx, double cy, double r0, double r1,
                                Color32 color, float innerAlpha01)
        {
            if (innerAlpha01 <= 0f || r1 <= r0) return;
            int x0 = x < 0 ? 0 : x, y0 = y < 0 ? 0 : y;
            int x1 = x + w, y1 = y + h;
            if (x1 > Width) x1 = Width;
            if (y1 > Height) y1 = Height;
            double span = r1 - r0;
            for (int yy = y0; yy < y1; yy++)
            {
                int row = yy * Width;
                double dy = yy + 0.5 - cy;
                for (int xx = x0; xx < x1; xx++)
                {
                    double dx = xx + 0.5 - cx;
                    double d = System.Math.Sqrt(dx * dx + dy * dy);
                    if (d >= r1) continue;
                    double t = d <= r0 ? 0.0 : (d - r0) / span;
                    int a = (int)(innerAlpha01 * (1.0 - t) * 255.0 + 0.5);
                    if (a <= 0) continue;
                    Pixels[row + xx] = Over(Pixels[row + xx], color, a);
                }
            }
        }

        /// <summary>
        /// ADDITIVE constant wash over a rect — the canvas
        /// <c>globalCompositeOperation='lighter'</c> + <c>globalAlpha</c> + an opaque fill, which the
        /// pilothouse dashes use for their deck/spot/night working-light washes (noviRig.js:428-437,
        /// capeRig.js:427-432). Grown here by S4 rather than copied a third time: CompassRigRender
        /// has carried a private version since S2a and the two new dashes both need it.
        ///
        /// <para>Pixels that are still fully transparent are SKIPPED — 'lighter' over nothing stays
        /// invisible once the card is composited over an opaque background, and adding into a
        /// transparent pixel would otherwise bloom the card's own cut-outs.</para>
        /// </summary>
        public void AddRect(int x, int y, int w, int h, Color32 c, float alpha01)
        {
            if (alpha01 <= 0f) return;
            int x0 = x < 0 ? 0 : x, y0 = y < 0 ? 0 : y;
            int x1 = x + w, y1 = y + h;
            if (x1 > Width) x1 = Width;
            if (y1 > Height) y1 = Height;
            double ar = c.r * alpha01, ag = c.g * alpha01, ab = c.b * alpha01;
            for (int yy = y0; yy < y1; yy++)
            {
                int row = yy * Width;
                for (int xx = x0; xx < x1; xx++) AddPx(row + xx, ar, ag, ab);
            }
        }

        /// <summary>
        /// ADDITIVE multi-stop radial wash — the canvas
        /// <c>createRadialGradient(cx,cy,r0, cx,cy,r1)</c> under <c>'lighter'</c>. Colour AND alpha
        /// interpolate between the stops (<paramref name="stopT"/> ascending, 0..1 across r0→r1); the
        /// wash is restricted to the rect and to a disc of <paramref name="clipRad"/> (the rigs'
        /// <c>save/arc/clip</c> around a gauge dial — noviRig.js:205). Beyond r1 nothing is drawn:
        /// the canvas extends the last stop, which these rigs always author fully transparent.
        /// </summary>
        public void AddRadial(int x, int y, int w, int h, double cx, double cy, double r0, double r1,
                              double clipRad, double[] stopT, Color32[] stopCol, float[] stopA,
                              float globalAlpha01 = 1f)
        {
            if (globalAlpha01 <= 0f || r1 <= r0 || stopT.Length == 0) return;
            int x0 = x < 0 ? 0 : x, y0 = y < 0 ? 0 : y;
            int x1 = x + w, y1 = y + h;
            if (x1 > Width) x1 = Width;
            if (y1 > Height) y1 = Height;
            double rMax = clipRad < r1 ? clipRad : r1;
            double span = r1 - r0;
            for (int yy = y0; yy < y1; yy++)
            {
                int row = yy * Width;
                double dy = yy + 0.5 - cy;
                for (int xx = x0; xx < x1; xx++)
                {
                    double dx = xx + 0.5 - cx;
                    double d = System.Math.Sqrt(dx * dx + dy * dy);
                    if (d > rMax) continue;
                    double t = d <= r0 ? 0.0 : (d - r0) / span;
                    int i = 0;                                   // the stop pair straddling t
                    while (i < stopT.Length - 1 && t > stopT[i + 1]) i++;
                    Color32 ca = stopCol[i];
                    float aa = stopA[i];
                    double f = 0.0;
                    if (i < stopT.Length - 1)
                    {
                        double lo = stopT[i], hi = stopT[i + 1];
                        f = hi > lo ? (t - lo) / (hi - lo) : 0.0;
                        if (f < 0.0) f = 0.0; else if (f > 1.0) f = 1.0;
                    }
                    Color32 cb = i < stopCol.Length - 1 ? stopCol[i + 1] : ca;
                    float a2 = i < stopA.Length - 1 ? stopA[i + 1] : aa;
                    double a = (aa + (a2 - aa) * f) * globalAlpha01;
                    if (a <= 0.0) continue;
                    AddPx(row + xx, (ca.r + (cb.r - ca.r) * f) * a,
                                    (ca.g + (cb.g - ca.g) * f) * a,
                                    (ca.b + (cb.b - ca.b) * f) * a);
                }
            }
        }

        private void AddPx(int i, double rr, double gg, double bb)
        {
            Color32 d = Pixels[i];
            if (d.a == 0) return;
            int r = d.r + (int)(rr + 0.5); if (r > 255) r = 255;
            int g = d.g + (int)(gg + 0.5); if (g > 255) g = 255;
            int b = d.b + (int)(bb + 0.5); if (b > 255) b = 255;
            Pixels[i] = new Color32((byte)r, (byte)g, (byte)b, d.a);
        }

        /// <summary>Linear colour blend between two opaque stops — the canvas linear gradient's
        /// interpolation, done per span by the caller so a gradient can follow a rounded-rect's row
        /// insets without a path rasteriser (depthRig.js:232-240).</summary>
        public static Color32 Lerp(Color32 a, Color32 b, double t)
        {
            if (t <= 0.0) return a;
            if (t >= 1.0) return b;
            return new Color32((byte)(a.r + (b.r - a.r) * t + 0.5),
                               (byte)(a.g + (b.g - a.g) * t + 0.5),
                               (byte)(a.b + (b.b - a.b) * t + 0.5),
                               (byte)(a.a + (b.a - a.a) * t + 0.5));
        }

        /// <summary>Source-over: <c>src</c> (treated opaque) at coverage <paramref name="a"/> (0..255)
        /// over <c>dst</c>. Integer maths, straight alpha.</summary>
        private static Color32 Over(Color32 dst, Color32 src, int a)
        {
            int ia = 255 - a;
            int outA = a + (dst.a * ia + 127) / 255;
            if (outA == 0) return new Color32(0, 0, 0, 0);
            // Straight-alpha source-over on the colour channels, weighted by coverage.
            int r = (src.r * a * 255 + dst.r * dst.a * ia + 127 * outA) / (outA * 255);
            int g = (src.g * a * 255 + dst.g * dst.a * ia + 127 * outA) / (outA * 255);
            int b = (src.b * a * 255 + dst.b * dst.a * ia + 127 * outA) / (outA * 255);
            return new Color32((byte)(r > 255 ? 255 : r), (byte)(g > 255 ? 255 : g),
                               (byte)(b > 255 ? 255 : b), (byte)outA);
        }

        /// <summary>
        /// Upload into a reused point-filtered RGBA32 texture (created on first call / size change),
        /// flipping rows once from the canvas's top-down convention into Unity's bottom-up rows. The
        /// scratch row buffer is reused — zero steady-state allocation (rule 7).
        /// </summary>
        public void ToTexture(ref Texture2D texture)
        {
            if (texture == null || texture.width != Width || texture.height != Height)
            {
                texture = new Texture2D(Width, Height, TextureFormat.RGBA32, false)
                {
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp,
                    name = "HHDrawSurface",
                };
            }
            if (_flipped == null || _flipped.Length != Pixels.Length)
                _flipped = new Color32[Pixels.Length];
            for (int y = 0; y < Height; y++)
                System.Array.Copy(Pixels, y * Width, _flipped, (Height - 1 - y) * Width, Width);
            texture.SetPixels32(_flipped);
            texture.Apply(false, false);
        }

        private Color32[] _flipped;

        /// <summary>JS <c>Math.round</c> — half-up toward +∞ (C#'s default rounds half-to-even, which
        /// would shift scanline spans a pixel against the rig source).</summary>
        public static int JsRound(double v) => (int)System.Math.Floor(v + 0.5);
    }
}
