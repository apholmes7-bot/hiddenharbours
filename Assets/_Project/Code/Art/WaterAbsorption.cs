using UnityEngine;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// Pure, headless-testable twin of the water shader's ADR 0027 #7 law — <b>per-channel
    /// Beer-Lambert absorption applied to the TRANSMITTED SEABED, never to the water's own
    /// colour</b> (mirrored in <c>HiddenHarboursWater.shader</c>: <c>AbsorptionSigma</c> /
    /// <c>AbsorptionTransmission</c> / <c>AbsorptionBand</c> / the composite lerp — change one,
    /// change BOTH in the same PR).
    ///
    /// <para><b>Why absorption does NOT drive the water body.</b> The painted <c>_DepthRamp</c> LUT
    /// stays the colour authority for the column (ADR 0027 finding 1: a hand-painted 1D LUT is
    /// strictly more general than any closed-form <c>e^(−σd)</c>, and <c>_DeepBlueStrength: 0.45</c>
    /// is standing evidence the owner already overrode the physical answer). What the LUT cannot
    /// express is the <b>bottom seen through the column</b> (finding 3): §17.1 showed the seabed by
    /// LOWERING <c>col.a</c> so an ungraded sprite bled through the alpha blend, and a <b>scalar</b>
    /// alpha cannot carry <b>per-channel</b> transmission at all. With <c>_SeabedTex</c> the shader
    /// composites the bottom itself, so <c>col.a</c> stays opaque and §17.3's caustic/see-through
    /// cancellation dissolves BY CONSTRUCTION rather than being tuned around.</para>
    ///
    /// <para><b>The law.</b> <c>T = exp(−σ_rgb · 2d)</c>. The path is <b>2d</b> — light descends the
    /// column, reflects off the bottom and returns (<see cref="DownAndBack"/>); a one-way path would
    /// make the water read half as turbid as it is. σ is per-channel extinction in 1/m, factored as
    /// ONE mood-eased turbidity scalar times a fixed per-channel ratio (<see cref="Sigma"/>): red
    /// extinguishes first, so the characteristic shift with depth comes free and <b>no second depth
    /// constant is tuned independently</b> (σ replaces both <c>_ShallowSeeThroughDepth</c> = 0.6 m
    /// and the ramp's own 0.15/4.0 m axis, which stays the water body's).</para>
    ///
    /// <para><b>Passthrough.</b> <c>turbidity = 0</c> ⇒ σ = 0 ⇒ <see cref="IsActive"/> false ⇒ the
    /// shader's whole block is skipped and the render is byte-identical to today. That is the rule-6
    /// contract every ADR 0010 addendum keeps. Note the deliberate discontinuity this implies:
    /// σ = 0 means "no absorption model", NOT "perfectly clear water" (which would show the bottom
    /// at full strength everywhere). Perfectly clear water is not a sea state; useful σ starts
    /// around 0.05 and the shipped mood presets carry real values. See the design doc §17.7.</para>
    ///
    /// <para><b>Pixel-art faithfulness.</b> <see cref="BandTransmission"/> posterizes transmission
    /// into discrete steps (<c>_AbsorptionBands</c>, <b>default ON</b>) — the concrete form of the
    /// ADR's "every layer carries its own quantization control", since <c>_DepthBands: 0</c> means
    /// the base ramp contributes no pixel character of its own. The seabed SAMPLE itself is snapped
    /// on the world PPU grid in the shader (the crawl law, §3), which has no C# twin because it is
    /// the existing <c>Pixelize</c> helper.</para>
    ///
    /// <para>Everything here drives <c>col.rgb</c> only — never <c>depth</c>/<c>clip()</c>/
    /// <c>_WaterLevel</c>/the height read/<c>_WaveFieldParams</c>/anything the hulls ride
    /// (P1 integrity, CLAUDE.md rule 5). Nothing is saved (rule 5 / ADR 0008).</para>
    /// </summary>
    public static class WaterAbsorption
    {
        /// <summary>
        /// The optical path multiplier: light travels DOWN to the bottom and BACK to the eye, so a
        /// column of depth <c>d</c> attenuates over <c>2d</c>. Mirrored as
        /// <c>ABSORPTION_PATH</c> in the shader. Not a tunable — turbidity is the dial (rule 6).
        /// </summary>
        public const float DownAndBack = 2f;

        /// <summary>
        /// Below this total σ the shader skips the whole absorption block (the exact-passthrough
        /// gate; mirrors <c>ABSORPTION_EPS</c>). Compared against the SUM of the three channels so a
        /// ratio that zeroes one channel can never switch the feature off on its own.
        /// </summary>
        public const float MinSigmaSum = 1e-4f;

        /// <summary>
        /// The default per-channel extinction ratio (relative to red = 1), mirroring the shader
        /// property <c>_AbsorptionRatio</c>'s default. Roughly coastal seawater: red dies fastest,
        /// blue survives longest — which is what produces the depth-colour shift for free.
        /// </summary>
        public static readonly Vector3 DefaultRatio = new Vector3(1f, 0.18f, 0.08f);

        /// <summary>
        /// σ (1/m, per channel) = <b>one</b> mood-eased turbidity scalar × a fixed per-channel
        /// ratio. Factoring it this way is what lets ADR 0017 ease turbidity per weather through
        /// <c>WaterSurface.MoodFloatNames</c> (a float) while the per-channel character stays
        /// authored art (rule 6) — and it is why <c>Water_StirredBrown</c> becomes a DERIVED state
        /// rather than a hand-picked colour. Negatives are clamped away (σ &lt; 0 would AMPLIFY the
        /// bottom with depth).
        /// </summary>
        public static Vector3 Sigma(float turbidity, Vector3 ratio)
        {
            float k = Mathf.Max(turbidity, 0f);
            return new Vector3(k * Mathf.Max(ratio.x, 0f),
                               k * Mathf.Max(ratio.y, 0f),
                               k * Mathf.Max(ratio.z, 0f));
        }

        /// <summary>
        /// The passthrough gate: true only when σ has any extinction at all. False ⇒ the shader's
        /// block never runs ⇒ today's look, byte-identical.
        /// </summary>
        public static bool IsActive(Vector3 sigma)
        {
            return Mathf.Max(sigma.x, 0f) + Mathf.Max(sigma.y, 0f) + Mathf.Max(sigma.z, 0f)
                   > MinSigmaSum;
        }

        /// <summary>
        /// THE #7 twin — per-channel Beer-Lambert transmission <c>T = exp(−σ · 2d)</c> over the
        /// down-and-back path. Monotone DECREASING in depth on every channel; 1 at the waterline
        /// (zero water ⇒ you see the ground, which is exactly what §17.1 was faking); → 0 with
        /// depth in the per-channel order σ dictates (red first for the default ratio).
        /// </summary>
        /// <param name="sigma">Per-channel extinction (1/m) — see <see cref="Sigma"/>.</param>
        /// <param name="depth">Still-water depth (m; clamped ≥ 0 — the same read-only depth every
        /// other layer consumes, never written).</param>
        public static Vector3 Transmission(Vector3 sigma, float depth)
        {
            float path = DownAndBack * Mathf.Max(depth, 0f);
            return new Vector3(Mathf.Exp(-Mathf.Max(sigma.x, 0f) * path),
                               Mathf.Exp(-Mathf.Max(sigma.y, 0f) * path),
                               Mathf.Exp(-Mathf.Max(sigma.z, 0f) * path));
        }

        /// <summary>
        /// Posterize transmission into <paramref name="bands"/> discrete steps (round-to-nearest,
        /// the shader's existing <c>_DepthBands</c>/<c>_SpecBands</c> idiom). <c>bands &lt; 1</c> is
        /// smooth (the un-quantized value, returned unchanged). Quantizing T rather than depth is
        /// deliberate: the visible step spacing then follows the exponential, so the bands crowd
        /// where the bottom is actually fading instead of where the metre axis happens to sit.
        /// </summary>
        public static Vector3 BandTransmission(Vector3 transmission, float bands)
        {
            if (bands < 1f) return transmission;
            return new Vector3(Band(transmission.x, bands),
                               Band(transmission.y, bands),
                               Band(transmission.z, bands));
        }

        private static float Band(float v, float bands)
        {
            return Mathf.Floor(Mathf.Clamp01(v) * bands + 0.5f) / bands;
        }

        /// <summary>
        /// The composite: the bottom is admitted in proportion to its own transmission, and what it
        /// does not fill in is the water body's own (painted-LUT) colour — <b>which absorption never
        /// touches</b>. <paramref name="coverage"/> is <c>_SeabedTex</c>'s alpha (0 where the bake
        /// found no bottom, so open water with no baked bed is unchanged by construction).
        /// </summary>
        public static Vector3 Composite(Vector3 waterRgb, Vector3 seabedRgb, Vector3 transmission,
                                        float coverage)
        {
            float w = Mathf.Clamp01(coverage);
            return new Vector3(
                Mathf.Lerp(waterRgb.x, seabedRgb.x, Mathf.Clamp01(transmission.x) * w),
                Mathf.Lerp(waterRgb.y, seabedRgb.y, Mathf.Clamp01(transmission.y) * w),
                Mathf.Lerp(waterRgb.z, seabedRgb.z, Mathf.Clamp01(transmission.z) * w));
        }
    }
}
