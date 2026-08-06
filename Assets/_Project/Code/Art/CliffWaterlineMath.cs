using UnityEngine;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// Pure, headless-testable twin of the CLIFF WATERLINE — the band of <c>HiddenHarboursCliffFace.shader</c>
    /// that puts the sea onto the standing rock (change one, change BOTH in the same PR, and
    /// <c>CliffWaterlineTests</c> reads the shader source to prove it).
    ///
    /// <para><b>The owner's ask (2026-08-06): "water should RISE against the cliff faces".</b> #439 stood
    /// the coast up — face quads generated from the region's own plan, sorted by their own feet — and the
    /// sea knew nothing about them. A cliff that plunges into deep water was drawn dry rock all the way
    /// down, with the water plane simply passing behind it. Deep-shore cliffs are tide-worked BY DESIGN
    /// (it was the ask that opened the cliff arc), so the rock has to carry the sea's mark: wet below the
    /// line, damp at it, dry above it, and the line itself riding the tide and the waves.</para>
    ///
    /// <para><b>🔴 The one rule this class exists to keep: the cliff does not get its own sea.</b> The
    /// water shader OWNS the waterline. So the tide here is not re-read from the environment, and the
    /// swell is not re-modelled — both arrive on the ONE global <c>WaterSurface</c> publishes
    /// (<c>_HHSeaLevelWorld</c>: the same eased level the water material is handed, plus the DRAWN sea's
    /// frequency scale and exaggeration), and the surge is the shared wave field evaluated through the
    /// SAME transcription the hull clamp uses (<see cref="WaveFieldBridge.ShaderTwinSample"/>). Two
    /// consumers, one sea, closed at the globals — because a second opinion about where the water is, is
    /// exactly how <c>_OceanSwellScale</c> once drew the sea at 2.8× while the clamp scanned at 1.</para>
    ///
    /// <para><b>Presentation only</b> (rule 5): the wet band is colour on rock. It moves no walkability,
    /// no <c>clip()</c> contour, no <c>_WaterLevel</c>, and it saves nothing. Where the player may stand
    /// on a cliff is the terrain's own slope gate, exactly as <c>CliffWallSurface</c> says.</para>
    /// </summary>
    public static class CliffWaterlineMath
    {
        /// <summary>
        /// The elevation (metres) at a point down a face, <paramref name="t01"/> 0 at the brow and 1 at
        /// the toe. Linear in the surface parameter, which is what the wall's own vertex rows are laid
        /// on — so the waterline crossing lands on the same rows the geometry is built from. Pure.
        /// </summary>
        public static float ElevationAt(float browElevation, float dropMetres, float t01)
            => browElevation - Mathf.Max(0f, dropMetres) * Mathf.Clamp01(t01);

        /// <summary>
        /// The wave SURGE (metres, signed) at a plan position — the shared field's height, through the
        /// DRAWN sea's own frequency scale.
        ///
        /// <para>This deliberately delegates to <see cref="WaveFieldBridge.ShaderTwinSample"/> rather than
        /// transcribing the train loop a second time: that method is already the line-by-line twin of the
        /// water shader's <c>WaveFieldSample()</c> and is already pinned against
        /// <c>WaveMath.Sample</c>. A fresh copy here would be a third place for the sea to drift.</para>
        /// </summary>
        public static float SurgeMetres(Vector2 planPosition, in PackedWaveField field, float freqScale)
            => WaveFieldBridge.ShaderTwinSample(planPosition, in field, Mathf.Max(freqScale, 1e-3f)).Height;

        /// <summary>
        /// Where the DRAWN sea stands at this stretch of wall, in metres of elevation: the published tide
        /// plus the surge, exaggerated by the same factor the displaced surface lifts its own crests by
        /// (so the cliff meets the water the player SEES, not the water the sim computes), and dialled by
        /// the material's surge gain. Pure.
        /// </summary>
        public static float SeaElevation(float tideLevelMeters, float surgeMeters,
                                         float exaggeration, float surgeGain)
            => tideLevelMeters + surgeMeters * Mathf.Max(0f, exaggeration) * Mathf.Max(0f, surgeGain);

        /// <summary>
        /// How far UNDER the sea a point on the face is, metres. Positive = submerged, 0 = exactly at the
        /// waterline, negative = dry rock above it. Pure.
        /// </summary>
        public static float SubmergenceMetres(float elevationMeters, float seaElevationMeters)
            => seaElevationMeters - elevationMeters;

        /// <summary>
        /// How strongly the rock reads as SEEN THROUGH WATER, 0..1: 0 at and above the waterline, rising
        /// with depth and saturating by <paramref name="fullDepthMeters"/>. It is what turns the plunge
        /// into a submerged foot instead of dry rock painted over the sea. Monotonic non-decreasing in
        /// submergence; a non-positive depth scale saturates immediately rather than dividing. Pure.
        /// </summary>
        public static float Submerged01(float submergenceMeters, float fullDepthMeters)
        {
            if (submergenceMeters <= 0f) return 0f;
            float depth = Mathf.Max(fullDepthMeters, 1e-3f);
            return Mathf.Clamp01(submergenceMeters / depth);
        }

        /// <summary>
        /// The DAMP BAND, 0..1 — the darkened, sea-worked strip the tide keeps wet, centred on the
        /// waterline and reaching <paramref name="bandMeters"/> either side of it. 1 exactly at the line,
        /// falling to 0 at the band's edges, and 0 everywhere outside. Symmetric on purpose: rock just
        /// above the line is still wet from the last wave, and rock just below it is still being worked.
        /// A zero band is an exact off switch (0 everywhere). Pure.
        /// </summary>
        public static float DampBand01(float submergenceMeters, float bandMeters)
        {
            float band = Mathf.Max(0f, bandMeters);
            if (band <= 0f) return 0f;
            float d = Mathf.Abs(submergenceMeters);
            if (d >= band) return 0f;
            float t = 1f - d / band;
            return t * t * (3f - 2f * t);          // smoothstep — no ruled edge on either side
        }

        /// <summary>
        /// The FOAM line, 0..1 — the bright collar of broken water right where the sea meets the rock. A
        /// much tighter band than <see cref="DampBand01"/> and biased BELOW the line (foam gathers on the
        /// water side, not up the dry face), so the two read as a wet strip with a lit edge rather than
        /// as one fat glow. 0 outside the collar, and 0 for a zero band. Pure.
        /// </summary>
        public static float FoamCollar01(float submergenceMeters, float collarMeters, float belowBias)
        {
            float collar = Mathf.Max(0f, collarMeters);
            if (collar <= 0f) return 0f;
            // Shift the collar's centre DOWN into the water by the bias (a fraction of its own width).
            float centred = submergenceMeters - collar * Mathf.Clamp01(belowBias);
            float d = Mathf.Abs(centred);
            if (d >= collar) return 0f;
            float t = 1f - d / collar;
            return t * t * (3f - 2f * t);
        }

        /// <summary>
        /// ⚠️ <b>Is there a published sea at all?</b> <c>_HHSeaLevelWorld.w</c> is the "published" flag
        /// (see <c>WaterSurface.PublishSeaLevel</c>); an unset global is all-zero and MUST read as "there
        /// is no sea here", not as "the sea is at chart datum". A scene with no water surface — an art
        /// scene, a rig fixture, a stopped play session — would otherwise draw every cliff drowned to its
        /// brow, which is the loudest possible failure for a silent global. Pure.
        /// </summary>
        public static bool HasPublishedSea(Vector4 seaLevelWorld) => seaLevelWorld.w > 0.5f;
    }
}
