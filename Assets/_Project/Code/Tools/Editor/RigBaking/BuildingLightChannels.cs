using System;
using HiddenHarbours.Art;
using UnityEngine;

namespace HiddenHarbours.Tools.RigBaking
{
    /// <summary>
    /// <b>The L2 light channels of one building facing</b> (village return, drop 14): the rig's own
    /// G-buffer turned into the three sheets the lit sprite path reads beside the albedo. Pure — no
    /// engine, no rig host — so every law in it is an EditMode test away.
    ///
    /// <list type="bullet">
    ///   <item><b>The mask</b>, in the TREES' order: R key, G back rim, B depth, A coverage
    ///   (<see cref="SpriteLightMath.MaskKey"/> … <see cref="SpriteLightMath.MaskCoverage"/>). Every
    ///   channel is the tree rig's own law (<c>docs/art/rigs/treeIsoRig2.js</c>: the key and depth at
    ///   its pass A, the rim at its pass B, the distance field and the thickness above them), fed the
    ///   house's normals instead of a crown's. The shader cannot tell a house texel from a leaf, which
    ///   is the point: one light law, one mask order.</item>
    ///   <item><b>The normal</b>: view space, y up, z toward the camera, <c>(n·½+½)·255</c> with
    ///   A = 255 on coverage — the tree rig's <c>normalView</c>, rounded the way its
    ///   <c>Uint8ClampedArray</c> rounds (half to even).</item>
    ///   <item><b>The emitter byte</b>: the rig's own night glow with every light in the house on, as
    ///   a LEVEL (the glow's linear luminance over the brightest WARM step) and a SOURCE (which light
    ///   feeds it). Written through <see cref="SpriteLightMath.EncodeEmit"/>, the one encoder.</item>
    /// </list>
    ///
    /// <para><b>Where the G-buffer comes from.</b> <c>CoastalPass.light.frame</c> rasterises the
    /// model once per facing: coverage, the face normal in MODEL-LOCAL metres (flipped to face the
    /// camera), and depth along the view axis. <see cref="ViewNormal"/> takes that normal into the
    /// view frame through the rig's own projection (<c>sx = ox + xr·S</c>,
    /// <c>sy = oy − (yr·se + z·ce)·S</c>, depth <c>yr·ce − z·se</c>). The glow is read off the rig's
    /// own relight at <c>NIGHT_SKY</c> — see <see cref="BuildingLightFrame"/>.</para>
    ///
    /// <para>Arithmetic is in <c>double</c>, as the rig's JS is, and every rounding is the JS one it
    /// copies: <see cref="JsRound"/> for <c>Math.round</c> (ties up),
    /// <see cref="ClampedArrayByte"/> for a <c>Uint8ClampedArray</c> store (ties to even) and
    /// <see cref="JsHypot(double, double)"/> for V8's <c>Math.hypot</c>. The field and the thickness
    /// are <c>float</c> because the rig keeps them in <c>Float32Array</c>s.</para>
    /// </summary>
    public static class BuildingLightChannels
    {
        // =========================================================================================
        //  the tree rig's mask law, stated once for the buildings
        // =========================================================================================

        /// <summary><c>Math.pow(…, 1.35)</c>: the key's falloff — the tree rig's literal, in double.
        /// <see cref="SpriteLightMath.KeyExponent"/> is its float twin, which the shader uses; the bake
        /// uses the literal so its bytes round as the rig's do.</summary>
        public const double KeyExponent = 1.35;

        /// <summary><c>Math.pow(back, 1.15)</c>: how sharply the rim falls off as the surface turns
        /// away from the rim light's screen direction.</summary>
        public const double RimExponent = 1.15;

        /// <summary><c>Math.pow(1 - clamp(nz, 0, 1), 1.7)</c>: the rim lives where the surface grazes
        /// the view.</summary>
        public const double FresnelExponent = 1.7;

        /// <summary><c>smooth(4.0, 5.2, TH)</c>: a mass thinner than this carries no rim (the tree's
        /// RULE 3).</summary>
        public const double ThickFrom = 4.0, ThickTo = 5.2;

        /// <summary><c>smooth(3.6, 0.8, d)</c>: the rim band, in px from the silhouette.</summary>
        public const double EdgeFrom = 3.6, EdgeTo = 0.8;

        /// <summary><c>RAD = 6</c>: the thickness is the distance field's max over a 13 px window.</summary>
        public const int ThicknessRadius = 6;

        /// <summary>The chamfer weights (<c>+3</c> orthogonal, <c>+4</c> diagonal), and the
        /// <c>/= 3</c> that turns them into px.</summary>
        public const int ChamferOrthogonal = 3, ChamferDiagonal = 4;

        /// <summary>
        /// The tree rig's key light, <c>nrm([-0.55, -0.66, 0.52])</c> in its y-DOWN frame, here in the
        /// view frame (y up): the same light as <see cref="SpriteLightMath.RigKeyDirection"/>, in
        /// double so the key channel rounds as the rig's does.
        /// </summary>
        public static readonly Vector3d KeyDirection = Vector3d.Normalized(-0.55, 0.66, 0.52);

        /// <summary>
        /// The tree rig's rim light <c>nrm([0.48, -0.28, -0.83])</c>, reduced as the rig reduces it:
        /// its screen xy, normalised (<c>RS</c>), in the view frame (y up).
        /// </summary>
        public static readonly double RimScreenX, RimScreenY;

        static BuildingLightChannels()
        {
            Vector3d r = Vector3d.Normalized(0.48, -0.28, -0.83);
            double l = JsHypot(r.X, r.Y);
            if (l == 0) l = 1;
            RimScreenX = r.X / l;
            RimScreenY = -r.Y / l;           // the rig's y points DOWN the screen
        }

        // =========================================================================================
        //  the glow
        // =========================================================================================

        /// <summary>The brightest step of CoastalPass's WARM ramp, <c>#fff0c2</c>: the glow a
        /// full-level window reaches. The emitter LEVEL is a luminance over this.</summary>
        public static readonly Color32 WarmTop = new Color32(0xff, 0xf0, 0xc2, 0xff);

        /// <summary>The window glow the houses' material carries in <c>_EmitColor</c>,
        /// <c>#ffc673</c> — the rig's own window lamp colour.</summary>
        public static readonly Color32 GlowColour = new Color32(0xff, 0xc6, 0x73, 0xff);

        /// <summary>
        /// <c>_EmitStrength</c> for the houses: the brightest WARM step's linear luminance over the
        /// glow colour's, so a texel at level 1 lights to exactly the rig's brightest window.
        /// ≈ 1.3910.
        /// </summary>
        public static double GlowStrength => LinearLuminance(WarmTop) / LinearLuminance(GlowColour);

        /// <summary>The sRGB transfer, decoded (IEC 61966-2-1).</summary>
        public static double SrgbToLinear(byte v)
        {
            double c = v / 255.0;
            return c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
        }

        /// <summary>Rec. 709 luminance of an sRGB colour, in linear light: how bright the colour is
        /// once the renderer (Linear colour space) has decoded it.</summary>
        public static double LinearLuminance(Color32 c) =>
            0.2126 * SrgbToLinear(c.r) + 0.7152 * SrgbToLinear(c.g) + 0.0722 * SrgbToLinear(c.b);

        /// <summary>
        /// The emitter LEVEL of one glowing texel, 0..1: its colour's linear luminance over
        /// <see cref="WarmTop"/>'s. The shader multiplies it by the glow colour and
        /// <see cref="GlowStrength"/>, so the luminance it draws is the rig's own.
        /// </summary>
        public static double GlowLevel(Color32 glow) =>
            Math.Min(1.0, Math.Max(0.0, LinearLuminance(glow) / LinearLuminance(WarmTop)));

        // =========================================================================================
        //  the G-buffer
        // =========================================================================================

        /// <summary>The rig's projection, as its light frame was handed it (<c>C</c>).</summary>
        public readonly struct Camera
        {
            public readonly double Ct, St, Se, Ce, Scale;

            public Camera(double ct, double st, double se, double ce, double scale)
            {
                Ct = ct; St = st; Se = se; Ce = ce; Scale = scale;
            }
        }

        /// <summary>
        /// One facing's G-buffer, NATIVE cell, top-left-origin rows — exactly what the rig's light
        /// frame holds, plus the glow its relight draws at night with every light on.
        /// </summary>
        public sealed class GBuffer
        {
            public int Width, Height;

            /// <summary><c>gb.a</c>: 1 where a face drew.</summary>
            public byte[] Coverage;

            /// <summary><c>gb.nx/ny/nz</c>: the face normal in model-local metres, camera-facing.</summary>
            public float[] NormalX, NormalY, NormalZ;

            /// <summary><c>gb.d</c>: metres along the view axis, SMALLER = NEARER.</summary>
            public float[] Depth;

            /// <summary>RGBA: the rig's night glow where a light shows (A = 255), else 0.</summary>
            public byte[] Glow;

            /// <summary>The light feeding each glowing texel, as a <c>SpriteLightMath.EmitSource*</c>
            /// code.</summary>
            public byte[] Source;

            public Camera Camera;
        }

        /// <summary>
        /// A G-buffer normal (model-local, camera-facing) in the VIEW frame: x right, y up the
        /// screen, z toward the camera. The rig's own rotation (<c>xr = x·ct − y·st</c>,
        /// <c>yr = x·st + y·ct</c>) and then its camera: up is <c>(0, se, ce)</c> and toward the
        /// camera is <c>(0, −ce, se)</c> in the rotated frame.
        /// </summary>
        public static Vector3d ViewNormal(double nx, double ny, double nz, in Camera c)
        {
            double xr = nx * c.Ct - ny * c.St;
            double yr = nx * c.St + ny * c.Ct;
            return new Vector3d(xr, yr * c.Se + nz * c.Ce, -yr * c.Ce + nz * c.Se);
        }

        // =========================================================================================
        //  the field and the thickness (tree rig: distField, and the RULE 3 window)
        // =========================================================================================

        /// <summary>
        /// Chamfer 3-4 distance from each covered pixel to the nearest uncovered one, in px — the tree
        /// rig's <c>distField</c>, pass for pass. Uncovered pixels are 0. A covered region that
        /// touches no uncovered pixel keeps the rig's <c>1e6 / 3</c>.
        ///
        /// <para>Returned as <c>float</c> because the rig keeps it in a <c>Float32Array</c>: the sums
        /// are exact integers either way, but <c>/ 3</c> is not, and the rim reads the stored value.</para>
        /// </summary>
        public static float[] DistanceField(byte[] coverage, int w, int h)
        {
            if (coverage == null || coverage.Length != w * h)
                throw new ArgumentException($"coverage must be {w}×{h}.", nameof(coverage));

            const double Big = 1e6;
            var d = new double[w * h];
            for (int i = 0; i < d.Length; i++) d[i] = coverage[i] != 0 ? Big : 0;

            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int i = y * w + x;
                    if (d[i] == 0) continue;
                    double v = d[i];
                    if (y > 0)
                    {
                        v = Math.Min(v, d[i - w] + ChamferOrthogonal);
                        if (x > 0) v = Math.Min(v, d[i - w - 1] + ChamferDiagonal);
                        if (x < w - 1) v = Math.Min(v, d[i - w + 1] + ChamferDiagonal);
                    }
                    if (x > 0) v = Math.Min(v, d[i - 1] + ChamferOrthogonal);
                    d[i] = v;
                }

            for (int y = h - 1; y >= 0; y--)
                for (int x = w - 1; x >= 0; x--)
                {
                    int i = y * w + x;
                    if (d[i] == 0) continue;
                    double v = d[i];
                    if (y < h - 1)
                    {
                        v = Math.Min(v, d[i + w] + ChamferOrthogonal);
                        if (x > 0) v = Math.Min(v, d[i + w - 1] + ChamferDiagonal);
                        if (x < w - 1) v = Math.Min(v, d[i + w + 1] + ChamferDiagonal);
                    }
                    if (x < w - 1) v = Math.Min(v, d[i + 1] + ChamferOrthogonal);
                    d[i] = v;
                }

            var field = new float[w * h];
            for (int i = 0; i < d.Length; i++) field[i] = (float)(d[i] / ChamferOrthogonal);
            return field;
        }

        /// <summary>
        /// Local mass thickness: the distance field's max over a (2·<see cref="ThicknessRadius"/>+1)²
        /// window, horizontal pass then vertical — the tree rig's <c>TH</c>, which gates the rim.
        /// </summary>
        public static float[] Thickness(float[] field, int w, int h)
        {
            var tmp = new float[w * h];
            var th = new float[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float m = 0;
                    for (int k = -ThicknessRadius; k <= ThicknessRadius; k++)
                    {
                        int jx = x + k;
                        if (jx < 0 || jx >= w) continue;
                        float v = field[y * w + jx];
                        if (v > m) m = v;
                    }
                    tmp[y * w + x] = m;
                }
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float m = 0;
                    for (int k = -ThicknessRadius; k <= ThicknessRadius; k++)
                    {
                        int jy = y + k;
                        if (jy < 0 || jy >= h) continue;
                        float v = tmp[jy * w + x];
                        if (v > m) m = v;
                    }
                    th[y * w + x] = m;
                }
            return th;
        }

        // =========================================================================================
        //  the per-texel law
        // =========================================================================================

        /// <summary>R, the key: <c>max(0, n·K)^1.35</c> (<see cref="KeyExponent"/>), 0..1, before
        /// the byte.</summary>
        public static double Key(in Vector3d n) =>
            Math.Pow(Math.Max(0.0, n.X * KeyDirection.X + n.Y * KeyDirection.Y + n.Z * KeyDirection.Z),
                     KeyExponent);

        /// <summary>G, the back rim: <c>back^1.15 · fres · thick · smooth(3.6, 0.8, d)</c>, 0..1.</summary>
        public static double Rim(in Vector3d n, double field, double thickness)
        {
            double nl = JsHypot(n.X, n.Y);
            if (nl == 0) nl = 1;
            double back = Math.Max(0.0, (n.X * RimScreenX + n.Y * RimScreenY) / nl);
            double fres = Math.Pow(1 - Clamp01(n.Z), FresnelExponent);
            double thick = Smooth(ThickFrom, ThickTo, thickness);
            return Math.Pow(back, RimExponent) * fres * thick * Smooth(EdgeFrom, EdgeTo, field);
        }

        /// <summary>The tree rig's <c>smooth(e0, e1, x)</c> — a smoothstep that also runs
        /// backwards when <c>e1 &lt; e0</c>.</summary>
        public static double Smooth(double e0, double e1, double x)
        {
            double t = Clamp01((x - e0) / (e1 - e0));
            return t * t * (3 - 2 * t);
        }

        /// <summary>JS <c>Math.round</c>: nearest integer, ties toward +∞ (exact for x ≥ 0).</summary>
        public static double JsRound(double x)
        {
            double f = Math.Floor(x);
            return x - f >= 0.5 ? f + 1 : f;
        }

        /// <summary><c>clamp(Math.round(v · 255), 0, 255)</c> — how the tree rig writes a mask byte.</summary>
        public static byte MaskByte(double v) => (byte)Math.Min(255.0, Math.Max(0.0, JsRound(v * 255)));

        /// <summary>A <c>Uint8ClampedArray</c> store: clamp to 0..255, ties to even, NaN to 0 — how the
        /// tree rig's normal view rounds.</summary>
        public static byte ClampedArrayByte(double v)
        {
            if (double.IsNaN(v)) return 0;
            if (v <= 0) return 0;
            if (v >= 255) return 255;
            return (byte)Math.Round(v, MidpointRounding.ToEven);
        }

        /// <summary>One normal channel, <c>(n·½ + ½)·255</c>.</summary>
        public static byte NormalByte(double n) => ClampedArrayByte((n * 0.5 + 0.5) * 255);

        /// <summary>
        /// V8's <c>Math.hypot(a, b)</c>: each magnitude over the largest, squared and summed with
        /// Kahan compensation, rooted and scaled back. Not <c>√(a² + b²)</c>, which can differ from it
        /// in the last bit — and the rig's <c>nrm</c> and rim both divide by it.
        /// </summary>
        public static double JsHypot(double a, double b)
        {
            double x = Math.Abs(a), y = Math.Abs(b);
            if (double.IsInfinity(x) || double.IsInfinity(y)) return double.PositiveInfinity;
            if (double.IsNaN(x) || double.IsNaN(y)) return double.NaN;
            double max = Math.Max(x, y);
            if (max == 0) return 0;
            double sum = 0, compensation = 0;
            KahanAddSquare(x / max, ref sum, ref compensation);
            KahanAddSquare(y / max, ref sum, ref compensation);
            return Math.Sqrt(sum) * max;
        }

        /// <summary>V8's <c>Math.hypot(a, b, c)</c> — see <see cref="JsHypot(double, double)"/>. Its
        /// own overload, because a third argument of 0 is not free under the compensation.</summary>
        public static double JsHypot(double a, double b, double c)
        {
            double x = Math.Abs(a), y = Math.Abs(b), z = Math.Abs(c);
            if (double.IsInfinity(x) || double.IsInfinity(y) || double.IsInfinity(z)) return double.PositiveInfinity;
            if (double.IsNaN(x) || double.IsNaN(y) || double.IsNaN(z)) return double.NaN;
            double max = Math.Max(x, Math.Max(y, z));
            if (max == 0) return 0;
            double sum = 0, compensation = 0;
            KahanAddSquare(x / max, ref sum, ref compensation);
            KahanAddSquare(y / max, ref sum, ref compensation);
            KahanAddSquare(z / max, ref sum, ref compensation);
            return Math.Sqrt(sum) * max;
        }

        static void KahanAddSquare(double n, ref double sum, ref double compensation)
        {
            double summand = n * n - compensation;
            double preliminary = sum + summand;
            compensation = (preliminary - sum) - summand;
            sum = preliminary;
        }

        // =========================================================================================
        //  the pack
        // =========================================================================================

        /// <summary>What <see cref="Pack"/> writes: three NATIVE-cell RGBA buffers, top-left rows, so
        /// the baker crops and flips them through the same <c>BlitCropped</c> as the albedo. The
        /// emitter byte sits in R of <see cref="Emit"/>; G, B and A are 0.</summary>
        public sealed class Channels
        {
            public byte[] Mask, Normal, Emit;

            /// <summary>Texels whose emitter byte is not 0.</summary>
            public int GlowTexels;

            /// <summary>The highest LEVEL written, 0..31.</summary>
            public int MaxLevel;
        }

        /// <summary>
        /// Pack one facing. <paramref name="albedo"/> is the same facing's RGBA as the bake draws it:
        /// the mask's A is its alpha (the tree rule), and a G-buffer whose coverage disagrees with it
        /// is refused — the channels would light pixels the sprite does not draw, or leave drawn
        /// pixels dark.
        /// </summary>
        public static Channels Pack(GBuffer gb, byte[] albedo)
        {
            if (gb == null) throw new ArgumentNullException(nameof(gb));
            int w = gb.Width, h = gb.Height, n = w * h;
            if (albedo == null || albedo.Length != n * 4)
                throw new ArgumentException($"the albedo must be {w}×{h} RGBA.", nameof(albedo));
            Require(gb.Coverage, n, "Coverage"); Require(gb.NormalX, n, "NormalX");
            Require(gb.NormalY, n, "NormalY"); Require(gb.NormalZ, n, "NormalZ");
            Require(gb.Depth, n, "Depth"); Require(gb.Source, n, "Source");
            if (gb.Glow == null || gb.Glow.Length != n * 4)
                throw new ArgumentException($"Glow must be {w}×{h} RGBA.", nameof(gb));

            int disagree = 0, firstDisagree = -1;
            for (int i = 0; i < n; i++)
                if ((albedo[i * 4 + 3] != 0) != (gb.Coverage[i] != 0))
                {
                    if (disagree++ == 0) firstDisagree = i;
                }
            if (disagree > 0)
                throw new InvalidOperationException(
                    $"The light frame covers different pixels from the albedo: {disagree} texel(s) " +
                    $"disagree, the first at ({firstDisagree % w}, {firstDisagree / w}). The channels " +
                    "would light pixels the sprite does not draw. Refusing rather than packing them.");

            float[] field = DistanceField(gb.Coverage, w, h);
            float[] thickness = Thickness(field, w, h);

            // Depth, the tree's way: z grows TOWARD the camera, in px, normalised over this facing.
            double zmin = double.MaxValue, zmax = double.MinValue;
            for (int i = 0; i < n; i++)
            {
                if (gb.Coverage[i] == 0) continue;
                double z = -gb.Depth[i] * gb.Camera.Scale;
                if (z < zmin) zmin = z;
                if (z > zmax) zmax = z;
            }
            double zr = Math.Max(1.0, zmax - zmin);

            var ch = new Channels { Mask = new byte[n * 4], Normal = new byte[n * 4], Emit = new byte[n * 4] };
            for (int i = 0; i < n; i++)
            {
                if (gb.Coverage[i] == 0) continue;
                int o = i * 4;

                Vector3d v = ViewNormal(gb.NormalX[i], gb.NormalY[i], gb.NormalZ[i], gb.Camera);

                ch.Mask[o + SpriteLightMath.MaskKey] = MaskByte(Key(v));
                ch.Mask[o + SpriteLightMath.MaskRim] = MaskByte(Rim(v, field[i], thickness[i]));
                ch.Mask[o + SpriteLightMath.MaskDepth] =
                    MaskByte((-gb.Depth[i] * gb.Camera.Scale - zmin) / zr);
                ch.Mask[o + SpriteLightMath.MaskCoverage] = albedo[o + 3];

                ch.Normal[o] = NormalByte(v.X);
                ch.Normal[o + 1] = NormalByte(v.Y);
                ch.Normal[o + 2] = NormalByte(v.Z);
                ch.Normal[o + 3] = 255;

                if (gb.Glow[o + 3] == 0) continue;
                var glow = new Color32(gb.Glow[o], gb.Glow[o + 1], gb.Glow[o + 2], 255);
                byte e = SpriteLightMath.EncodeEmit((float)GlowLevel(glow), gb.Source[i]);
                ch.Emit[o] = e;
                if (e != 0)
                {
                    ch.GlowTexels++;
                    ch.MaxLevel = Math.Max(ch.MaxLevel, e % SpriteLightMath.EmitSourceStride);
                }
            }
            return ch;
        }

        static void Require<T>(T[] a, int n, string name)
        {
            if (a == null || a.Length != n)
                throw new ArgumentException($"{name} must hold {n} texels, got {(a == null ? 0 : a.Length)}.");
        }

        static double Clamp01(double v) => v < 0 ? 0 : v > 1 ? 1 : v;

        /// <summary>A double-precision 3-vector, so the law's arithmetic matches the rig's JS.</summary>
        public readonly struct Vector3d
        {
            public readonly double X, Y, Z;

            public Vector3d(double x, double y, double z) { X = x; Y = y; Z = z; }

            /// <summary>The rig's <c>nrm</c>: divide by <c>Math.hypot</c>, or by 1 when it is 0.</summary>
            public static Vector3d Normalized(double x, double y, double z)
            {
                double l = JsHypot(x, y, z);
                if (l == 0) l = 1;
                return new Vector3d(x / l, y / l, z / l);
            }

            public double Length => Math.Sqrt(X * X + Y * Y + Z * Z);
        }
    }
}
