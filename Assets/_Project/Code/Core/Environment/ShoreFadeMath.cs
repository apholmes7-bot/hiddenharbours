using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// The SHORE SEAM of the displaced-water arc (ADR 0023): the one pure rule that lets the sea
    /// become a vertically displaced surface WITHOUT ever tearing the coast. Wave displacement is
    /// multiplied by a smooth fade that is <b>exactly 0 at and beyond the walkable waterline</b>
    /// (depth ≤ 0) and exactly 1 past a shallow falloff band — so the visible waterline stays
    /// byte-identical to the sim's wet/dry contour (the P1 integrity rule of ADR 0009/0010/0014),
    /// while the open sea keeps the full readability exaggeration the 3D-water spike proved.
    ///
    /// <para><b>Why depth is the right axis (tide-consistency by construction).</b> The input depth
    /// is the game's one depth rule, <c>depth = IEnvironmentService.WaterLevelAt(t) −
    /// ITidalTerrain.ElevationAt(pos)</c> — the same painted-seabed read the water shader, the
    /// walkability gate, and boat-crossing already share (one height map, three consumers). The
    /// fade's zero set is therefore the depth-0 iso-contour ITSELF: as the tide moves the walkable
    /// waterline, the seam moves with it, with no second contour to drift (rule 5 — everything here
    /// is recomputed from <c>(worldSeed, gameTime)</c> inputs; nothing is saved).</para>
    ///
    /// <para><b>The ONE-SEA rule (ADR 0023).</b> This class shapes the presentation of the ONE
    /// shared deterministic wave field (<see cref="WaveMath"/>, ADR 0018) — it never invents
    /// height. Callers pass a <see cref="WaveSample.Height"/> sampled from the production field;
    /// <see cref="DisplacedHeight"/> only scales it.</para>
    ///
    /// <para><b>The SHARED-EXAGGERATION contract (the overlay-pose lesson: never rescale one
    /// consumer alone).</b> Every consumer that turns wave metres into screen metres — the displaced
    /// surface's vertex lift, a mesh hull's visual heave, buoy/wake/oar anchors riding the surface —
    /// must read its displaced height through <see cref="DisplacedHeight"/> with the SAME
    /// exaggeration constant, so a boat's heave rides exactly the sea it is drawn on. The
    /// exaggeration value itself is owner-tunable data (GameConfig plumbing lands with the
    /// production-surface phase); this class deliberately takes it as a parameter.</para>
    ///
    /// <para><b>The HLSL twin discipline (ADR 0018 §(4)).</b> The displaced-water shader carries a
    /// line-for-line HLSL transcription of <see cref="Fade01"/> (a saturate + smoothstep — cheap).
    /// Any change to the math here must change the shader twin in the same PR, exactly as
    /// <see cref="WaveMath.Sample"/> and its twin are kept in lockstep. The
    /// <c>ShoreFadeMathTests</c> pinned numbers are what a twin review diffs against.</para>
    ///
    /// <para><b>Why the band size is DERIVED, not free (rule 6 — and the tear-safety proof).</b>
    /// Displacement moves water pixels along screen-y; land tiles do not move. Water drawn past the
    /// waterline contour = a torn coast. Two analytic hazards bound the falloff band B (full
    /// derivation: ADR 0023):
    /// <list type="bullet">
    /// <item><b>Overlap:</b> a crest's lift must stay smaller than its ground distance to the
    /// contour. With lift ≤ A·s·smoothstep(d/B) and distance ≥ d/g (A = field envelope, s =
    /// exaggeration, g = max seabed gradient in the band), the worst ratio is 1.125·A·s·g/B —
    /// safe iff B ≥ 1.125·A·s·g.</item>
    /// <item><b>Fold:</b> the screen-y mapping y + s·h·fade must stay monotone in ground-y or the
    /// surface shears over itself. The worst in-band term (the full envelope crest parked at the
    /// fade's steepest point) is s·A·(1.5/B)·g — B = 1.5·A·s·g is exactly marginal there.</item>
    /// </list>
    /// <see cref="RecommendedBandCoefficient"/> = 2 covers both with real margin;
    /// <see cref="RecommendedBandMeters"/> turns it into metres from the live envelope,
    /// exaggeration and the shore's painted gradient. The band's on-screen footprint is then
    /// B/g = 2·A·s ground-metres — INDEPENDENT of shore steepness (≈3.1 m at the reference sea's
    /// 1.047 m envelope × 1.5), so every coast wears the same narrow seam.</para>
    ///
    /// <para>Pure, stateless, allocation-free, engine-light (Mathf only) — same inputs, same output,
    /// forever, on every machine.</para>
    /// </summary>
    public static class ShoreFadeMath
    {
        /// <summary>Guard floor for the falloff band (metres) so a zeroed band degrades to a hard
        /// step at the waterline instead of dividing by zero. A guard, not a tunable.</summary>
        public const float MinBandMeters = 1e-4f;

        /// <summary>The derived safety coefficient in <see cref="RecommendedBandMeters"/>:
        /// band = coefficient × envelope × exaggeration × shoreGradient. Analytic floor is 1.125
        /// (overlap) and 1.5 is exactly marginal against the worst-case in-band fold (see the class
        /// doc); 2 holds both with margin. Verified numerically by <c>ShoreFadeMathTests</c> against
        /// the reference sea, including the 100%-envelope event parked mid-band.</summary>
        public const float RecommendedBandCoefficient = 2f;

        /// <summary>
        /// The shore fade: 0 at and beyond the waterline (depth ≤ 0), smoothstep up through the
        /// falloff band, exactly 1 at and past <paramref name="bandMeters"/> of depth. This is the
        /// seam's whole contract: multiply any visual wave displacement by this factor and the
        /// displaced surface pins to the walkable waterline while open water is untouched.
        /// </summary>
        /// <param name="depthMeters">Local still-water depth in metres:
        /// <c>WaterLevelAt(t) − ElevationAt(pos)</c>. ≤ 0 means dry/exposed ground.</param>
        /// <param name="bandMeters">The falloff band B (metres of depth over which displacement
        /// ramps in). Derive it via <see cref="RecommendedBandMeters"/>; clamped ≥
        /// <see cref="MinBandMeters"/>.</param>
        public static float Fade01(float depthMeters, float bandMeters)
        {
            if (depthMeters <= 0f) return 0f;
            float band = Mathf.Max(bandMeters, MinBandMeters);
            float t = Mathf.Clamp01(depthMeters / band);
            return t * t * (3f - 2f * t);   // smoothstep(0, band, depth) — the HLSL twin's exact shape
        }

        /// <summary>
        /// The displaced height (metres of screen lift) every displaced-water consumer draws:
        /// the ONE shared wave height × the ONE shared exaggeration × the shore fade. Exactly 0 at
        /// the waterline for any wave and any tide; exactly <c>waveHeight × exaggeration</c> in open
        /// water (depth ≥ band), so the seam machinery is invisible offshore.
        /// </summary>
        /// <param name="waveHeightMeters">The production field's height at this position/time
        /// (<see cref="WaveSample.Height"/> — never a foreign sim).</param>
        /// <param name="depthMeters">Local still-water depth (see <see cref="Fade01"/>).</param>
        /// <param name="bandMeters">The falloff band B (see <see cref="Fade01"/>).</param>
        /// <param name="exaggeration">The SHARED readability exaggeration (ADR 0023 sweet spot
        /// ×1.5; ×1 = sim-true). All consumers must pass the same value.</param>
        public static float DisplacedHeight(float waveHeightMeters, float depthMeters,
                                            float bandMeters, float exaggeration)
            => waveHeightMeters * exaggeration * Fade01(depthMeters, bandMeters);

        /// <summary>
        /// The tear-safe falloff band for a coast:
        /// <see cref="RecommendedBandCoefficient"/> × envelope × exaggeration × gradient (see the
        /// class doc for the derivation). Steeper shores need a deeper band; the resulting seam is
        /// always the same width on the ground (<see cref="GroundFootprintMeters"/>).
        /// </summary>
        /// <param name="envelopeMeters">The field's height envelope
        /// (<see cref="WaveTrains.TotalAmplitude"/>) — the tallest possible crest.</param>
        /// <param name="exaggeration">The shared exaggeration constant.</param>
        /// <param name="maxShoreGradient">The largest |∇elevation| (m per m, dimensionless) of the
        /// painted seabed within the shallow band — how steeply this coast shelves.</param>
        public static float RecommendedBandMeters(float envelopeMeters, float exaggeration,
                                                  float maxShoreGradient)
            => RecommendedBandCoefficient
               * Mathf.Max(0f, envelopeMeters)
               * Mathf.Max(0f, exaggeration)
               * Mathf.Max(0f, maxShoreGradient);

        /// <summary>The seam's width on the ground (metres from the waterline to full displacement)
        /// when the band comes from <see cref="RecommendedBandMeters"/>: band/gradient =
        /// coefficient × envelope × exaggeration — independent of shore steepness.</summary>
        public static float GroundFootprintMeters(float envelopeMeters, float exaggeration)
            => RecommendedBandCoefficient
               * Mathf.Max(0f, envelopeMeters)
               * Mathf.Max(0f, exaggeration);
    }
}
