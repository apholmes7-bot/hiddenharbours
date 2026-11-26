using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>Whose float it is, as a ramp swap.</b> The pot floats are baked ONCE by the pot-float rig
    /// (<c>docs/art/rigs/pot-float-kit/potFloatRig.js</c>) in two key coats, and every owner's colours
    /// are swapped in at draw time by the float's own shader (<c>HiddenHarbours/PlayerSubmerge</c>, the
    /// ADR 0029 exact-colour substitution in <c>Include/CharacterPalette.hlsl</c>). One sheet, any owner,
    /// colour from data: no file per colour.
    ///
    /// <para><b>The arithmetic is the rig's, to the byte.</b> A coat is dimmed by the nav kit's 'working'
    /// wear (x0.92, 16% toward its own luminance) and laddered into five steps
    /// (<c>rampOf</c>: x0.42-6, x0.62-2, base, x1.14+10, x1.30+26), rounding half up as JavaScript does.
    /// <c>PotFloatArtTests</c> runs the rig and holds this class to its output, so a key ramp computed
    /// here is exactly the colours sitting in the sheet.</para>
    ///
    /// <para>Pure. Called when a float is configured, never per frame (rule 7).</para>
    /// </summary>
    public static class PotFloatPaint
    {
        /// <summary>The body coat the sheet is baked in (the rig's <c>BAKED.body</c>).</summary>
        public static readonly Color32 BakedBody = new Color32(0xe0, 0xb1, 0x3a, 0xff);

        /// <summary>The band coat the sheet is baked in (the rig's <c>BAKED.band</c>).</summary>
        public static readonly Color32 BakedBand = new Color32(0x9c, 0x4a, 0x3c, 0xff);

        /// <summary>Steps in one coat's ramp.</summary>
        public const int RampSteps = 5;

        /// <summary>Swaps one float can carry: two coats of five steps.</summary>
        public const int MaxSwaps = RampSteps * 2;

        private const double WornMul = 0.92, WornDesat = 0.16;

        /// <summary>A colour no sprite can hold, so an unused slot never matches a pixel.</summary>
        public static readonly Vector4 NoColour = new Vector4(-1f, -1f, -1f, 0f);

        /// <summary>The rig's <c>shift(hex, mul, lift, desat)</c>.</summary>
        public static Color32 Shift(Color32 c, double mul, double lift, double desat)
        {
            double l = 0.299 * c.r + 0.587 * c.g + 0.114 * c.b;
            byte M(byte v) => Round(((v + (l - v) * desat) * mul) + lift);
            return new Color32(M(c.r), M(c.g), M(c.b), 0xff);
        }

        private static byte Round(double v)
        {
            double r = System.Math.Floor(v + 0.5);   // JavaScript Math.round: half up, never half-even
            return (byte)(r < 0 ? 0 : r > 255 ? 255 : r);
        }

        /// <summary>The rig's <c>paintRamp(hex)</c>: a coat as it is painted on a working float,
        /// dark to light. Writes <see cref="RampSteps"/> entries into <paramref name="into"/>.</summary>
        public static void PaintRamp(Color32 coat, Color32[] into)
        {
            Color32 b = Shift(coat, WornMul, 0, WornDesat);
            into[0] = Shift(b, 0.42, -6, 0);
            into[1] = Shift(b, 0.62, -2, 0);
            into[2] = b;
            into[3] = Shift(b, 1.14, 10, 0);
            into[4] = Shift(b, 1.30, 26, 0);
        }

        /// <summary>
        /// Fill the shader's swap arrays (linear space, as the fragment sees an sRGB sprite) so the
        /// baked coats become <paramref name="body"/> and <paramref name="band"/>. Identity steps are
        /// skipped; unused slots are <see cref="NoColour"/>. Returns the live count. The arrays must
        /// hold at least <see cref="MaxSwaps"/> entries (they may be longer: the rest are cleared).
        /// </summary>
        public static int BuildSwaps(Color body, Color band, Vector4[] from, Vector4[] to)
        {
            var bakedRamp = new Color32[RampSteps];
            var wantRamp = new Color32[RampSteps];
            int n = 0;
            n = AddCoat(BakedBody, body, bakedRamp, wantRamp, from, to, n);
            n = AddCoat(BakedBand, band, bakedRamp, wantRamp, from, to, n);
            for (int i = n; i < from.Length; i++) { from[i] = NoColour; to[i] = NoColour; }
            return n;
        }

        private static int AddCoat(Color32 baked, Color want, Color32[] bakedRamp, Color32[] wantRamp,
                                   Vector4[] from, Vector4[] to, int n)
        {
            Color32 w = want;
            w.a = 0xff;
            if (w.r == baked.r && w.g == baked.g && w.b == baked.b) return n;
            PaintRamp(baked, bakedRamp);
            PaintRamp(w, wantRamp);
            for (int i = 0; i < RampSteps; i++)
            {
                from[n] = Linear(bakedRamp[i]);
                to[n] = Linear(wantRamp[i]);
                n++;
            }
            return n;
        }

        /// <summary>The colour as the fragment shader sees it (the project renders in Linear space).</summary>
        public static Vector4 Linear(Color32 c)
        {
            Color lin = ((Color)c).linear;
            return new Vector4(lin.r, lin.g, lin.b, 1f);
        }
    }
}
