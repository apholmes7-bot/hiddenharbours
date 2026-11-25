using UnityEngine;

namespace HiddenHarbours.Boats
{
    /// <summary>
    /// The PURE, deterministic maths behind the wave-driven BUOY (Build 1 of the trap-fishing arc,
    /// <b>visual-only</b>). Given the live wave <b>Height</b> under the buoy (from
    /// <c>WaveMath.Sample</c> — metres about the tide level, positive on a crest, negative in a
    /// trough) it decides the three reads the <see cref="BuoyWaveVisual"/> shell turns into pixels:
    /// how high the buoy <b>bobs</b>, how far up its side the <b>waterline</b> sits, and — under a
    /// big enough crest — whether it <b>vanishes</b> entirely (fades to alpha 0), reappearing as the
    /// crest passes. Split out (like <see cref="BoatWaveMotionMath"/> / <c>PlayerSubmergeMath</c>) so
    /// the mapping is EditMode-testable headless (rule 5) and the MonoBehaviour stays thin. No RNG,
    /// no side effects, no <c>Time</c> — a pure function of its inputs, all keyed off the live
    /// <c>Height</c> read with NO stored state (so it is as deterministic as the field it samples).
    ///
    /// <para><b>The waterline keys on the WAVE surface, not the tide depth</b> (unlike
    /// <c>PlayerSubmergeMath</c>, whose input is depth-over-feet). A buoy floats: it always sits low
    /// and wet at a baseline (<paramref name="floatLineFrac"/>), and the line rides UP its side as
    /// the local crest lifts past it and DOWN into a trough — <c>floatLineFrac + (height −
    /// restOffset) / buoyHeightMeters</c>, clamped to [0, 1]. So the same rising crest that lifts the
    /// buoy (<see cref="BobOffset"/>) also climbs its flank — one <c>Height</c> read drives both.</para>
    /// </summary>
    public static class BuoyWaveMath
    {
        /// <summary>
        /// Screen-vertical bob offset (world units) for a wave <paramref name="height"/> (m) under
        /// the buoy — the crest lifts it, the trough drops it: <c>height · bobPerMeter</c>, clamped to
        /// ±<paramref name="maxBob"/> so a freak sum of trains can't fling the sprite. Glass
        /// (<paramref name="height"/> 0) → exactly 0 (glass is sacred). Deterministic; NaN-safe
        /// (garbage clamps, never propagates).
        /// </summary>
        public static float BobOffset(float height, float bobPerMeter, float maxBob)
        {
            if (float.IsNaN(height)) return 0f;
            float cap = Mathf.Max(0f, maxBob);
            return Mathf.Clamp(height * bobPerMeter, -cap, cap);
        }

        /// <summary>
        /// The waterline fraction (0 = the buoy's base/bottom, 1 = its top) the water reaches, given
        /// the live wave <paramref name="height"/> (m) under the buoy:
        /// <list type="bullet">
        /// <item><description><b>Baseline</b> (<paramref name="height"/> = <paramref name="restOffset"/>)
        /// → <paramref name="floatLineFrac"/>: the resting draught, the buoy always low and wet.</description></item>
        /// <item><description><b>On a crest</b> (height &gt; restOffset) → the line rides UP toward the
        /// top as the crest climbs the flank.</description></item>
        /// <item><description><b>In a trough</b> (height &lt; restOffset) → the line drops toward the
        /// base, more of the buoy standing proud of the water.</description></item>
        /// </list>
        /// <c>floatLineFrac + (height − restOffset) / buoyHeightMeters</c>, clamped to [0, 1].
        /// Monotonic non-decreasing in height; deterministic; NaN/degenerate-safe
        /// (<paramref name="buoyHeightMeters"/> ≤ 0 collapses to the clamped baseline rather than
        /// dividing by zero).
        /// </summary>
        /// <param name="height">Live wave surface height under the buoy (m, about the tide level).</param>
        /// <param name="floatLineFrac">Resting draught 0..1 — how far up the buoy the water sits in calm (≈0.4: low + wet).</param>
        /// <param name="restOffset">The wave height (m) treated as "rest": the line is exactly floatLineFrac here (≈0, the mean surface).</param>
        /// <param name="buoyHeightMeters">The buoy's height (m) the crest climb is measured against — bigger = the same crest climbs a smaller fraction.</param>
        public static float WaterlineFraction(float height, float floatLineFrac, float restOffset, float buoyHeightMeters)
        {
            float baseFrac = Mathf.Clamp01(floatLineFrac);
            if (float.IsNaN(height)) return baseFrac;

            float rise = buoyHeightMeters > 1e-4f ? (height - restOffset) / buoyHeightMeters : 0f;
            return Mathf.Clamp01(baseFrac + rise);
        }

        /// <summary>
        /// The <b>swamp threshold</b> (metres of wave height) at/above which the buoy is under enough
        /// water to VANISH — scaled off the field's <paramref name="totalAmplitude"/> (the sum of the
        /// live trains' amplitudes, <c>WaveTrains.TotalAmplitude</c>) so it stays meaningful across sea
        /// states: <c>totalAmplitude · thresholdFraction</c>, but never below
        /// <paramref name="minThresholdMeters"/> (a floor so a tiny calm sea's near-zero amplitude
        /// can't make an infinitesimal crest "swamp" the buoy). A crest taller than this buries the
        /// buoy; a smaller sea (lower amplitude) lowers the bar proportionally, so the biggest crests
        /// of ANY sea state still read as the ones that go over the top.
        /// </summary>
        /// <param name="totalAmplitude">The field's height envelope (m) — <c>WaveTrains.TotalAmplitude</c>.</param>
        /// <param name="thresholdFraction">Fraction of the envelope a crest must reach to swamp (0..~1; ≈0.7 = only the near-peak crests).</param>
        /// <param name="minThresholdMeters">Absolute floor (m) so a near-glass sea never swamps on a nothing crest.</param>
        public static float SwampThreshold(float totalAmplitude, float thresholdFraction, float minThresholdMeters)
        {
            float scaled = Mathf.Max(0f, totalAmplitude) * Mathf.Max(0f, thresholdFraction);
            return Mathf.Max(scaled, Mathf.Max(0f, minThresholdMeters));
        }

        /// <summary>
        /// Sprite alpha (0 = fully vanished under the crest, 1 = fully visible) for a wave
        /// <paramref name="height"/> (m) versus the <paramref name="swampThreshold"/> (from
        /// <see cref="SwampThreshold"/>). Below the threshold the buoy is fully visible; as the crest
        /// climbs from the threshold through <c>threshold + fadeBandMeters</c> the alpha fades linearly
        /// to 0 — a continuous vanish/reappear as the crest passes, no popping, no stored state. The
        /// fade band keeps the last pixels from snapping off; set it small for a crisp duck-under.
        /// Deterministic; NaN → fully visible (fail safe: a garbage read never hides the buoy).
        /// </summary>
        /// <param name="height">Live wave surface height under the buoy (m).</param>
        /// <param name="swampThreshold">The height (m) at which the vanish begins (see <see cref="SwampThreshold"/>).</param>
        /// <param name="fadeBandMeters">Extra crest height (m) over the threshold across which alpha fades 1→0 (small ⇒ crisp).</param>
        public static float VanishAlpha(float height, float swampThreshold, float fadeBandMeters)
        {
            if (float.IsNaN(height)) return 1f;
            if (height <= swampThreshold) return 1f;

            float band = Mathf.Max(1e-4f, fadeBandMeters);
            float over = (height - swampThreshold) / band;   // 0 at the threshold, 1 a full band above
            return Mathf.Clamp01(1f - over);
        }

        /// <summary>True once the crest has fully buried the buoy — <see cref="VanishAlpha"/> at 0.
        /// Sugar for tests / tooling; the shell drives the sprite off the continuous alpha, not this.</summary>
        public static bool IsSwamped(float height, float swampThreshold, float fadeBandMeters)
            => VanishAlpha(height, swampThreshold, fadeBandMeters) <= 0f;

        /// <summary>
        /// <b>The waterline wraps a round body.</b> A float of radius <paramref name="radiusMeters"/>
        /// floating upright meets the water in a horizontal circle; the 3/4 camera squashes the ground
        /// plane by <paramref name="groundDepthScale"/> (<c>IsoGround.GroundDepthScale</c>), so the circle
        /// draws as an ellipse whose SIDES sit on the float line and whose NEAR point sits
        /// <c>groundDepthScale · radius</c> lower. This packs that ellipse into the shader's
        /// <c>_WaterlineArc</c> in the sprite's own texture space:
        /// <c>(drop in uv.y, centre u, 1 / half-width in u, texture width px)</c>.
        ///
        /// <para>Everything is read off the sprite (its texture rect, pivot and pixels-per-unit) and the
        /// hull's own radius, so an atlas cell of an 8-facing sheet works the same as a lone sprite. The
        /// body is centred on the sprite's pivot column. A radius of 0 (or any degenerate input) returns
        /// <see cref="Vector4.zero"/>, which the shader reads as the straight row, bit for bit.</para>
        /// </summary>
        /// <param name="radiusMeters">The body's radius at the waterline (m). 0 = straight.</param>
        /// <param name="pixelsPerUnit">The sprite's pixels per unit.</param>
        /// <param name="textureRectPx">The sprite's rect inside its texture (px).</param>
        /// <param name="pivotPx">The sprite's pivot, px from the rect's bottom-left.</param>
        /// <param name="textureSizePx">The texture's width and height (px).</param>
        /// <param name="groundDepthScale">How far up the screen a metre of northward ground draws.</param>
        public static Vector4 WaterlineArc(float radiusMeters, float pixelsPerUnit, Rect textureRectPx,
                                           Vector2 pivotPx, Vector2 textureSizePx, float groundDepthScale)
        {
            if (!(radiusMeters > 0f) || !(pixelsPerUnit > 0f) || !(groundDepthScale > 0f)) return Vector4.zero;
            if (!(textureSizePx.x >= 1f) || !(textureSizePx.y >= 1f)) return Vector4.zero;
            if (float.IsInfinity(radiusMeters) || float.IsInfinity(pixelsPerUnit)) return Vector4.zero;

            float radiusPx = radiusMeters * pixelsPerUnit;
            float dropV = groundDepthScale * radiusPx / textureSizePx.y;
            float centreU = (textureRectPx.x + pivotPx.x) / textureSizePx.x;
            float invHalfU = textureSizePx.x / radiusPx;
            return new Vector4(dropV, centreU, invHalfU, textureSizePx.x);
        }

        /// <summary>
        /// The shader's <c>WaterlineCut</c>, line for line: where the water meets the body at texture
        /// column <paramref name="u"/>, as a uv.y, given the straight float line
        /// <paramref name="waterlineFrac"/> and the packed <paramref name="arc"/>. The column is snapped
        /// to its texel centre; the drop is the near half of the ellipse, zero beyond the body, and
        /// never more than what is left above the line (so a buoy awash still goes fully under).
        /// A zero arc returns <paramref name="waterlineFrac"/> unchanged.
        /// </summary>
        public static float WaterlineCut(float u, float waterlineFrac, Vector4 arc)
        {
            float texW = Mathf.Max(arc.w, 1f);
            float uSnap = (Mathf.Floor(u * texW) + 0.5f) / texW;
            float t = (uSnap - arc.y) * arc.z;
            float drop = arc.x * Mathf.Sqrt(Mathf.Clamp01(1f - t * t));
            return waterlineFrac - Mathf.Min(drop, Mathf.Max(0f, 1f - waterlineFrac));
        }
    }
}
