using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// Pure C# twin of catchKit.js <c>tintSpoil</c> — the ROTTEN look: a <c>spoil</c> 0..1 drives
    /// a green shift plus an ordered-dither mottle on any catch item, keylines staying dark.
    ///
    /// <para><b>What this file is:</b> the exact per-pixel recipe (<see cref="Tint"/> /
    /// <see cref="TintPixel"/>), byte-parity-tested against the live rig in
    /// <c>CatchStorageBakeTests</c>, plus the coarse per-renderer approximation
    /// (<see cref="RendererTint"/>) the fill presenter applies today. The presenter cannot do
    /// per-pixel work on a shared sprite without a shader or baked variants, so it multiplies by
    /// the recipe's UNIFORM term only — the Bayer mottle (and the green rot MOTES of
    /// <c>CatchKit.particles</c>) are deferred, flagged in the storage PR.</para>
    ///
    /// <para><b>Who sets spoil:</b> the freshness clock (M1 §7.3) — <c>HoldCatchFillSource</c> reads
    /// the hold's settled state and feeds <c>CatchFillRenderer.SetSpoil</c>; the Boats deck tray
    /// tints from the same Core <see cref="RotTint"/> the uniform term now lives in.</para>
    /// </summary>
    public static class CatchSpoilMath
    {
        /// <summary>The rot green — catchKit.js <c>SPOIL</c> / <c>SPRGB</c> (#7d9a46). Sourced from
        /// the Core <see cref="RotTint"/> so every module tints from one constant.</summary>
        public static readonly Color32 SpoilColor = RotTint.SpoilColor;

        // Recipe constants (catchKit.js tintSpoil) — parity-tested, not balance dials. The uniform
        // term lives in Core (RotTint.UniformMix) so the tray tint can never drift from the recipe.
        private const int KeylineLumFloor = 70;          // r+g+b below this = keyline, stays dark
        private const double BaseTint = RotTint.UniformMix;   // the uniform green-shift term
        private const double MottleBonus = 0.28;         // extra shift on dither-selected pixels
        private const double MottleThresholdScale = 0.55;

        /// <summary>The rig's 4×4 ordered-dither thresholds, (v+0.5)/16, indexed
        /// <c>[x&amp;3][y&amp;3]</c> exactly as the JS does (first index is X).</summary>
        private static readonly double[] Bayer = BuildBayer();

        private static double[] BuildBayer()
        {
            int[,] m = { { 0, 8, 2, 10 }, { 12, 4, 14, 6 }, { 3, 11, 1, 9 }, { 15, 7, 13, 5 } };
            var t = new double[16];
            for (int x = 0; x < 4; x++)
                for (int y = 0; y < 4; y++)
                    t[x * 4 + y] = (m[x, y] + 0.5) / 16.0;
            return t;
        }

        /// <summary>The green-mix weight at pixel (x,y): <c>spoil·(0.40 + mottle)</c>, where the
        /// mottle lands on pixels whose Bayer threshold sits under <c>spoil·0.55</c> — so the rot
        /// speckle grows denser as spoil climbs.</summary>
        public static double MixAt(int x, int y, double spoil)
        {
            if (spoil <= 0.0) return 0.0;
            double bonus = Bayer[(x & 3) * 4 + (y & 3)] < spoil * MottleThresholdScale ? MottleBonus : 0.0;
            return spoil * (BaseTint + bonus);
        }

        /// <summary>One pixel of the exact recipe. Keylines (r+g+b &lt; 70) stay dark; everything
        /// else lerps toward the rot green by <see cref="MixAt"/>, JS half-up rounding.</summary>
        public static void TintPixel(ref byte r, ref byte g, ref byte b, int x, int y, double spoil)
        {
            if (r + g + b < KeylineLumFloor) return;
            double m = MixAt(x, y, spoil);
            if (m <= 0.0) return;
            r = (byte)System.Math.Floor(r * (1 - m) + SpoilColor.r * m + 0.5);
            g = (byte)System.Math.Floor(g * (1 - m) + SpoilColor.g * m + 0.5);
            b = (byte)System.Math.Floor(b * (1 - m) + SpoilColor.b * m + 0.5);
        }

        /// <summary>
        /// The exact recipe over a whole RGBA buffer — the faithful twin of
        /// <c>tintSpoil(rgba,w,h,spoil)</c>. Like the JS: spoil ≤ 0 returns the INPUT buffer
        /// untouched; otherwise a tinted COPY (transparent pixels skipped). Editor/bake-time and
        /// test use — nothing at runtime walks pixels per frame.
        /// </summary>
        public static byte[] Tint(byte[] rgba, int width, int height, double spoil)
        {
            if (spoil <= 0.0) return rgba;
            var outp = (byte[])rgba.Clone();
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int i = (y * width + x) * 4;
                    if (outp[i + 3] == 0) continue;
                    TintPixel(ref outp[i], ref outp[i + 1], ref outp[i + 2], x, y, spoil);
                }
            }
            return outp;
        }

        /// <summary>
        /// The runtime APPROXIMATION: a multiply colour for a whole SpriteRenderer carrying the
        /// recipe's uniform term only (<c>m = spoil·0.40</c>, each channel scaled toward the rot
        /// green as a bright pixel would lerp). Reads as the same sickly green from arm's length;
        /// the per-pixel mottle needs a shader or baked spoil variants and is deferred. Delegates to
        /// the Core <see cref="RotTint"/> (the one shared formula) — kept as the Art-side name so
        /// existing callers and the parity tests are untouched.
        /// </summary>
        public static Color RendererTint(double spoil01) => RotTint.Uniform(spoil01);
    }
}
