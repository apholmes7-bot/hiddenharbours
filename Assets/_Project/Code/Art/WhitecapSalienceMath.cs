using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// The headless C# twin of the water shader's ENVELOPE SALIENCE stage (ADR 0023 §(3)–(4) and
    /// §"Whitecap salience retune" — phase 2, step 2). LINE-FOR-LINE with the HLSL in
    /// <c>HiddenHarboursWater.shader</c> (<c>CapEnvelopeGate</c> / <c>BandValue01</c> /
    /// <c>BayerWorld</c>): change one, change both in the same commit — the WaveMath twin
    /// discipline. <c>WhitecapSalienceMathTests</c> pins this side (and scrapes the shader source
    /// for the property defaults) so the two can never drift silently.
    ///
    /// <para><b>What the retune is.</b> The flat water's whitecaps used to mark EVERY local crest
    /// with equal salience — which is exactly what hid the big one (the spike's control image: the
    /// 100%-envelope event sits in uniform speckle). The retune keys cap CORE SOLIDITY and the
    /// VALUE BANDS on height relative to the field's envelope (<c>height / TotalAmplitude</c> —
    /// the crest factor the shared wave field already publishes): ordinary chop wears thin
    /// dithered/milky streaks, only near-envelope crests wear the solid foam core and reach the
    /// top value band. Defaults are the SPIKE-TUNED thresholds
    /// (<c>spike/3d-water</c> · VERDICT.md / IsoWaterSpike.shader / SpikeWaterRenderer).</para>
    ///
    /// <para><b>Style law (ADR 0023 §(3), binding).</b> Solid bands; Bayer dither ONLY inside a
    /// window at a band/threshold EDGE (full-range dither reconstructs the smooth gradient —
    /// airbrush, forbidden); dither cells are WORLD-locked PPU-quantised (zero crawl). Shades come
    /// from the owner's palette anchors on Water.mat (shader-side; not mirrored here).</para>
    ///
    /// <para>Pure, stateless, engine-light; visual dressing only — drives no sim, saves nothing
    /// (rule 5). The shore-salience composition delegates to <see cref="ShoreFadeMath"/> (the
    /// seam's one curve — never a second contour).</para>
    /// </summary>
    public static class WhitecapSalienceMath
    {
        /// <summary>Spike-tuned crest-factor threshold where cap cores BEGIN (VERDICT.md: "cap
        /// threshold 0.62"). Below it a crest earns no solid core — ordinary chop.</summary>
        public const float DefaultEnvelopeThreshold = 0.62f;

        /// <summary>Spike-tuned margin above the threshold at which the core is FULLY solid
        /// (IsoWaterSpike <c>_CapSolid</c> = 0.3).</summary>
        public const float DefaultSolidMargin = 0.30f;

        /// <summary>Spike-tuned width of the Bayer-dithered fringe just above the threshold
        /// (IsoWaterSpike <c>_CapDither</c> = 0.25).</summary>
        public const float DefaultDitherBand = 0.25f;

        /// <summary>Spike-tuned envelope value band count (SpikeWaterRenderer set 7).</summary>
        public const float DefaultBandCount = 7f;

        /// <summary>Spike-tuned band-edge dither window, as a 0..1 fraction of a band
        /// (SpikeWaterRenderer <c>_DitherWin</c> = 0.4).</summary>
        public const float DefaultBandDitherWindow = 0.4f;

        // The rigs' 4x4 ordered-dither thresholds, (v + 0.5)/16, row-major [x, y] — byte-identical
        // to the shader's BAYER4 and the boat rigs' matrix (ADR 0022 dither discipline).
        private static readonly float[] Bayer4 =
        {
             0.5f / 16f,  8.5f / 16f,  2.5f / 16f, 10.5f / 16f,
            12.5f / 16f,  4.5f / 16f, 14.5f / 16f,  6.5f / 16f,
             3.5f / 16f, 11.5f / 16f,  1.5f / 16f,  9.5f / 16f,
            15.5f / 16f,  7.5f / 16f, 13.5f / 16f,  5.5f / 16f,
        };

        /// <summary>
        /// The Bayer threshold of a WORLD-locked pixel cell — the twin of the shader's
        /// <c>BayerWorld</c> (cell = floor(world × PPU); the &amp; wraps negatives identically in
        /// C# and HLSL, both two's complement). World-derived, so the dither cannot crawl.
        /// </summary>
        public static float BayerThreshold(int cellX, int cellY)
            => Bayer4[(cellX & 3) * 4 + (cellY & 3)];

        /// <summary>
        /// The SOLID-CORE gate (the twin of the shader's <c>CapEnvelopeGate</c>): 0 at and below
        /// the envelope threshold (ordinary chop earns NO solid core), a Bayer-dithered 0-or-1
        /// fringe across <paramref name="ditherBand"/> just above it (dither at the EDGE only —
        /// the style law), hard 1 at and past <paramref name="solidMargin"/> (a near-envelope
        /// crest wears the solid foam core). <paramref name="crestFactor"/> is the field's
        /// sharpened height/envelope (WaveMath's crest factor), so the gate is envelope-relative
        /// by construction — a bigger SEA does not fake a bigger WAVE.
        /// </summary>
        public static float CapEnvelopeGate(float crestFactor, float threshold, float solidMargin,
                                            float ditherBand, float bayer)
        {
            float sig = crestFactor - threshold;
            if (sig <= 0f) return 0f;
            if (sig >= solidMargin) return 1f;
            return (sig / Mathf.Max(ditherBand, 1e-4f)) > bayer ? 1f : 0f;
        }

        /// <summary>
        /// Posterize a 0..1 value into <paramref name="bandCount"/> SOLID steps, Bayer-dithering
        /// ONLY inside <paramref name="ditherWindow"/> (a 0..1 fraction of a band) around each
        /// rounding boundary (the twin of the shader's <c>BandValue01</c> — the spike's exact
        /// formula). Returns the quantised value in 0..1; v = 1 lands the TOP band on every dither
        /// cell, so only a near-envelope crest can reach the top shade.
        /// </summary>
        public static float BandValue01(float value01, float bandCount, float ditherWindow, float bayer)
        {
            float bands = Mathf.Max(bandCount, 2f);
            float x = Mathf.Clamp01(value01) * (bands - 1f);
            float fb = Mathf.Floor(x);
            float win = Mathf.Clamp(ditherWindow, 1e-3f, 1f);
            float e = Mathf.Clamp01(((x - fb) - (0.5f - 0.5f * win)) / win);
            return (fb + (e > bayer ? 1f : 0f)) / (bands - 1f);
        }

        /// <summary>
        /// The near-shore cap-salience fade (the twin of the shader's <c>capShoreFade</c>
        /// composition): the open-water caps die with the SEAM — the same
        /// <see cref="ShoreFadeMath.Fade01"/> curve over the same band the displaced vertex stage
        /// reads, scaled by the salience master so strength 0 is an EXACT legacy passthrough
        /// (returns 1 at any depth). The dying displaced edge must not wear open-sea caps
        /// (ADR 0023 §"Whitecap salience retune"); shore foam/swash stays the separate dressing.
        /// </summary>
        public static float CapShoreSalience(float stillDepthMeters, float bandMeters, float salienceStrength)
            => Mathf.Lerp(1f, ShoreFadeMath.Fade01(stillDepthMeters, bandMeters),
                          Mathf.Clamp01(salienceStrength));
    }
}
