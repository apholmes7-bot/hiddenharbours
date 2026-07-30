using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// The UNIFORM rot-tint approximation — the whole-renderer multiply colour that reads as "going
    /// off" from arm's length. Moved to Core (from Art's <c>CatchSpoilMath.RendererTint</c>, which now
    /// delegates here) so every hold-reading presenter in any module — the Art tote fill, the Boats
    /// deck tray — tints from ONE formula and can never drift apart. The exact per-pixel recipe (the
    /// Bayer mottle, the keyline guard) stays in Art, byte-parity-tested against the rig; these two
    /// constants are shared by both.
    /// </summary>
    public static class RotTint
    {
        /// <summary>The rot green — catchKit.js <c>SPOIL</c> (#7d9a46). Parity constant, not a dial.</summary>
        public static readonly Color32 SpoilColor = new Color32(125, 154, 70, 255);

        /// <summary>The recipe's uniform green-shift term (catchKit.js <c>tintSpoil</c> base 0.40).
        /// Parity constant, not a balance dial.</summary>
        public const double UniformMix = 0.40;

        /// <summary>
        /// The multiply colour for a renderer at <paramref name="spoil01"/>: each channel scaled toward
        /// the rot green as a bright pixel would lerp under the recipe's uniform term. White at 0.
        /// </summary>
        public static Color Uniform(double spoil01)
        {
            double s = spoil01 < 0 ? 0 : spoil01 > 1 ? 1 : spoil01;
            double m = s * UniformMix;
            return new Color(
                (float)(1 - m + SpoilColor.r / 255.0 * m),
                (float)(1 - m + SpoilColor.g / 255.0 * m),
                (float)(1 - m + SpoilColor.b / 255.0 * m),
                1f);
        }
    }
}
