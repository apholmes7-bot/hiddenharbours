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
        /// <see cref="Submerged01"/> in the four flat steps the shader draws it in: no gradient under the
        /// line. Pure.
        /// </summary>
        public static float SubmergedStepped01(float submergenceMeters, float fullDepthMeters)
            => Mathf.Ceil(Submerged01(submergenceMeters, fullDepthMeters) * 4f) / 4f;

        // ==== THE WET FOOT (owner, 11-23: "make them look more organic and natural") ================
        // The foot is drawn on the kit's own TEXEL GRID (32 px a metre, 2 px texels: 16 texels a metre),
        // so every edge it adds is a pixel step and never a gradient. Along the shore the cell counts
        // texels of the tile (u times the tile's metres); down the face it counts texels of ELEVATION,
        // which is what the waterline is measured in. Every function below takes that cell.

        /// <summary>Texels a metre on the px kit: 32 px a metre, cut in 2 px texels.</summary>
        public const float TexelsPerMetre = 16f;

        /// <summary>Columns in one group of the wet band's ragged top: the top steps every 3 texels.</summary>
        public const float WetTopGroupTexels = 3f;

        /// <summary>
        /// The texel cell a point on the face falls in. <paramref name="u"/> is the tile coordinate along
        /// the shore, <paramref name="tileMetresS"/> the metres one tile covers, and
        /// <paramref name="elevationMetres"/> the point's elevation. Pure.
        /// </summary>
        public static Vector2 TexelCell(float u, float tileMetresS, float elevationMetres)
            => new Vector2(Mathf.Floor(u * tileMetresS * TexelsPerMetre),
                           Mathf.Floor(elevationMetres * TexelsPerMetre));

        /// <summary>
        /// The elevation the whole cell is judged at: its centre row. This is what makes every band edge
        /// land on a texel row instead of cutting a texel in two. Pure.
        /// </summary>
        public static float CellElevation(Vector2 cell) => (cell.y + 0.5f) / TexelsPerMetre;

        /// <summary>
        /// A stable 0..1 hash of a cell, with no sine in it (a sine hash drifts between GPUs). The shader's
        /// CliffHash is the same arithmetic. Pure.
        /// </summary>
        public static float Hash01(Vector2 cell, float salt)
        {
            float x = Frac((cell.x + salt * 17.0f) * 0.1031f);
            float y = Frac((cell.y + salt * 31.0f) * 0.1030f);
            float z = Frac((cell.x + salt * 17.0f) * 0.0973f);
            float d = x * (y + 33.33f) + y * (z + 33.33f) + z * (x + 33.33f);
            x += d; y += d; z += d;
            return Frac((x + y) * z);
        }

        /// <summary>The 4 by 4 ordered dither threshold of a cell, 1/32 to 31/32. Pure.</summary>
        public static float Bayer4(Vector2 cell)
        {
            int x = (int)Mod(cell.x, 4f), y = (int)Mod(cell.y, 4f);
            return (Bayer[y * 4 + x] + 0.5f) / 16f;
        }

        private static readonly float[] Bayer = { 0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5 };

        /// <summary>
        /// The ragged TOP of the wet band in this column, metres above the waterline. It steps per group
        /// of <see cref="WetTopGroupTexels"/> columns, between 0.55 and 1.45 times
        /// <paramref name="bandMetres"/>, plus or minus one texel per column, so the line the tide leaves
        /// is torn and never ruled. A zero band is an exact off switch. Pure.
        /// </summary>
        public static float WetBandTopMetres(float cellX, float bandMetres)
        {
            float band = Mathf.Max(0f, bandMetres);
            if (band <= 0f) return 0f;
            float group = Mathf.Floor(cellX / WetTopGroupTexels);
            float top = band * (0.55f + 0.9f * Hash01(new Vector2(group, 0f), 1f));
            top += (Hash01(new Vector2(cellX, 0f), 2f) - 0.5f) * 2f / TexelsPerMetre;
            return Mathf.Max(top, 1f / TexelsPerMetre);
        }

        /// <summary>
        /// The WET BAND, 0 or 1: rock the tide has just left, from the waterline up to the column's own
        /// ragged top. It is measured from the CURRENT waterline, so it rides the tide: at low water it is
        /// down on the foot, at high water up the face. Nothing under the line and nothing above the top.
        /// Pure.
        /// </summary>
        public static float WetBand01(float submergenceMetres, float cellX, float bandMetres)
        {
            float above = -submergenceMetres;
            if (above < 0f) return 0f;
            return above < WetBandTopMetres(cellX, bandMetres) ? 1f : 0f;
        }

        /// <summary>
        /// The WEED FRINGE, 0 or 1: rockweed hanging at the line, from one texel under the water to a
        /// ragged top of 0.4 to 1.6 times <paramref name="weedMetres"/>, and only on the cells its
        /// <paramref name="density"/> picks, so it reads as tufts and not a stripe. Pure.
        /// </summary>
        public static float WeedFringe01(float submergenceMetres, Vector2 cell, float weedMetres, float density)
        {
            if (weedMetres <= 0f || density <= 0f) return 0f;
            float above = -submergenceMetres;
            float top = weedMetres * (0.4f + 1.2f * Hash01(new Vector2(Mathf.Floor(cell.x / 2f), 0f), 3f));
            if (above < -1f / TexelsPerMetre || above >= top) return 0f;
            return Hash01(cell, 4f) < density ? 1f : 0f;
        }

        /// <summary>
        /// A BARNACLE fleck, 0 or 1: sparse pale cells in the strip just above the weed, up to
        /// <paramref name="barnacleMetres"/> higher. Pure.
        /// </summary>
        public static float Barnacle01(float submergenceMetres, Vector2 cell, float weedMetres,
                                       float barnacleMetres, float density)
        {
            if (barnacleMetres <= 0f || density <= 0f) return 0f;
            float above = -submergenceMetres;
            float from = Mathf.Max(0f, weedMetres) * 0.5f;
            if (above < from || above >= from + barnacleMetres) return 0f;
            return Hash01(cell, 5f) < density ? 1f : 0f;
        }

        /// <summary>
        /// Does the rock still DRAW under the water here, 0 or 1? Just under the line it mostly does, and
        /// by <paramref name="fadeMetres"/> down it has gone, in four flat steps of an ordered dither, so
        /// the sea shows through the foot and the foot dissolves into it instead of ending on a hard
        /// edge. Dry rock always draws. A zero fade keeps the old opaque foot. Pure.
        /// </summary>
        public static float SubmergedKeep01(float submergenceMetres, Vector2 cell, float fadeMetres)
        {
            if (submergenceMetres <= 0f || fadeMetres <= 0f) return 1f;
            float level = Mathf.Ceil(Mathf.Clamp01(submergenceMetres / fadeMetres) * 4f) / 4f;
            return Bayer4(cell) >= level ? 1f : 0f;
        }

        /// <summary>
        /// A soft edge's alpha turned into a PIXEL DISSOLVE, 0 or 1: a cell draws when the edge's alpha
        /// beats its threshold. The threshold is the ordered dither offset by a hash per 2 by 2 block, so
        /// a tapered run end or a seam feather crumbles like weathered rock instead of fading like glass.
        /// Alpha 1 always draws and alpha 0 never does. Pure.
        /// </summary>
        public static float Dissolve01(float alpha, Vector2 cell)
        {
            if (alpha >= 0.999f) return 1f;
            if (alpha <= 0f) return 0f;
            // The ordered dither, offset per 2 by 2 block and wrapped: still an even spread of thresholds
            // (so the covered share stays the alpha), but the pattern no longer reads as a screen.
            float th = Frac(Bayer4(cell)
                     + 0.5f * Hash01(new Vector2(Mathf.Floor(cell.x / 2f), Mathf.Floor(cell.y / 2f)), 6f));
            return alpha > th ? 1f : 0f;
        }

        /// <summary>
        /// Broken FOAM, 0 or 1: a cell of the collar shows foam when the collar's strength beats its own
        /// hash, so the collar reads as flecks of broken water and not a painted white stroke. Pure.
        /// </summary>
        public static float FoamFleck01(float submergenceMetres, Vector2 cell, float collarMetres,
                                        float belowBias, float breakUp)
        {
            float foam = FoamCollar01(submergenceMetres, collarMetres, belowBias);
            if (foam <= 0f) return 0f;
            float brk = Mathf.Clamp01(breakUp);
            // Broken even at the collar's heart: its strength is held under 1 by the break up.
            return foam * (1f - 0.4f * brk) > brk * Hash01(cell, 7f) + (1f - brk) * 0.5f ? 1f : 0f;
        }

        private static float Frac(float v) => v - Mathf.Floor(v);
        private static float Mod(float v, float m) => v - m * Mathf.Floor(v / m);

        /// <summary>
        /// The FOAM line, 0..1 — the bright collar of broken water right where the sea meets the rock. A
        /// much tighter band than the wet band and biased BELOW the line (foam gathers on the water side,
        /// not up the dry face). The shader breaks it into flecks (<see cref="FoamFleck01"/>). 0 outside
        /// the collar, and 0 for a zero band. Pure.
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
