using UnityEngine;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// Pure, headless-testable twin of ADR 0027 <b>#10 — the capillary RIPPLE band</b> (mirrored in
    /// <c>HiddenHarboursWater.shader</c>'s <c>RippleWindGate</c> / <c>RippleWindwardGate</c> /
    /// <c>RippleFramingFade</c> / <c>RippleBandValue</c> / <c>RippleAmplitude</c> — change one, change
    /// BOTH in the same PR).
    ///
    /// <para><b>What the band is.</b> The finest thing the shader drew was <c>_WindChopScale</c> at 0.7 —
    /// a 1.4 m band, which is CHOP, not ripples. #10 adds a ~0.08–0.15 m octave riding <i>on</i> the
    /// larger waves: the thing that makes water read as water close up. It is <b>Tier A permanently</b> —
    /// <c>col.rgb</c> only, never <c>depth</c>/<c>clip()</c>/<c>_WaterLevel</c>/the height read/
    /// <c>_WaveFieldParams</c>/anything the hulls ride. A ripple is surface texture, not a force.</para>
    ///
    /// <para><b>The three gates, and why each exists.</b></para>
    /// <list type="number">
    /// <item><b>Wind</b> (<see cref="WindGate01"/>) — no wind, no ripples; glass stays glass. Reads the
    /// sim-pushed <c>_Roughness</c>, so it is never authored in a material (§12.1).</item>
    /// <item><b>Windward face</b> (<see cref="WindwardGate01"/>) — real wind ripples sit on the faces
    /// looking INTO the wind and are absent in the lee of a crest. The term is the slope of the shared
    /// wave field projected on the wind, so it needs no new uniform: going downwind you climb the
    /// windward (upwind) face, so <c>dot(slope, wind) &gt; 0</c> IS that face.</item>
    /// <item><b>Framing</b> (<see cref="FramingFade01"/>) — <b>the finding that replaced the ADR's kill
    /// condition.</b> See below.</item>
    /// </list>
    ///
    /// <para><b>⚠️ Why the fade is keyed to the FRAMING and not to the zoom tier.</b> ADR 0027 wrote #10's
    /// kill condition as a sub-pixel problem — *"at a wider zoom tier a ripple falls below one pixel …
    /// its amplitude scales down per discrete zoom tier"* — and told the build to drop the item if that
    /// per-tier fade could not be made stable. Measured rather than assumed (the spike, pinned by
    /// <c>RipplePixelFootprintTests</c>): <c>PixelPerfectCamera.assetsPPU</c> is LOCKED at 32 and the
    /// camera frames a tier by changing its REFERENCE RESOLUTION, so world-metres-per-pixel is 1/32
    /// everywhere and a 0.10 m ripple is <b>3.2 px at every framing the game has</b>. It never goes
    /// sub-pixel. <b>There is no per-tier amplitude fade to build, and none must be.</b>
    /// What DOES vary is the CYCLE COUNT: the widest framing shows 33.75 m of sea against the tightest's
    /// 5.625 m, so ~6× more ripple cycles at the same pixel size — texture on the swell up close, a dense
    /// competing field wide open. Hence a fade over <i>how much sea is on screen</i>.</para>
    ///
    /// <para><b>The framing input is a PUSH, not a mood float.</b> <c>_SeaFramingHeight</c> is derived
    /// from the camera (published by <see cref="WaterSurface"/>, the <c>WaveFieldBridge</c> pattern), so
    /// it must never enter <c>WaterSurface.MoodFloatNames</c> — anything in that list is eased from the
    /// eight preset materials and would double-drive this. Same discipline as <c>_Chop</c>/
    /// <c>_Roughness</c>/<c>_Flow</c>.</para>
    ///
    /// <para><b>⚠️ The C#-twin trap this class was written around.</b> <c>Mathf.SmoothStep(a, b, t)</c> is a
    /// smooth LERP between two VALUES — it is NOT HLSL's <c>smoothstep(e0, e1, x)</c>, which is an EDGE
    /// GATE returning 0..1. Every gate here spells the edge form out explicitly in
    /// <see cref="SmoothstepEdge"/>; using the Mathf overload would silently return the wrong number
    /// everywhere and the twin tests would pin the wrong law.</para>
    ///
    /// <para><b>Passthrough contract.</b> <see cref="Amplitude01"/> at <c>strength = 0</c> is exactly 0
    /// and the shader's whole block is skipped, so the shipped <c>Water.mat</c> look is byte-identical
    /// until the owner dials it in (rule 6, the discipline every ADR-0010 addendum has kept).</para>
    /// </summary>
    public static class WaterRipple
    {
        /// <summary>Narrowest band a gate's two edges may collapse to (mirrors the shader's
        /// <c>max(hi, lo + 1e-3)</c> guards). An <c>smoothstep</c> with <c>e0 == e1</c> is undefined on
        /// some GPUs, so neither side is ever allowed to degenerate.</summary>
        public const float MinGateBand = 1e-3f;

        /// <summary>
        /// Normalizer from the wave field's physical along-wind slope to a legible 0..1 face signal.
        /// <b>2.0, matching the swell FACE SHADING</b> (<c>clamp(-dot(waveSlope, light) * 2, -1, 1)</c>)
        /// — the two read the same slope off the same field, so they must scale it the same way or the
        /// ripples would sit on a differently-shaped "face" than the shading that draws it. Not a
        /// tunable: the windward gate's dial is <c>_RippleWindwardGate</c> (rule 6).
        /// </summary>
        public const float SlopeNormalize = 2f;

        /// <summary>Fewest posterize steps that mean anything (mirrors <c>BandValue01</c>'s
        /// <c>max(bandCount, 2)</c>). Below this the band is left SMOOTH — the <c>_DepthBands</c> /
        /// <c>_SpecBands</c> / <c>_AbsorptionBands</c> "0 = off" idiom.</summary>
        public const float MinBands = 2f;

        /// <summary>
        /// Ceiling on the band's brightness swing, so a ripple reads as TEXTURE and never as a lit wave.
        /// Sits below the swell-read band's 0.25 and the face shading's 0.15 by design: this is the
        /// finest layer on the sea and the one most able to turn into glare. At the recommended 0.45
        /// strength the swing is ±0.045 — visible corrugation, not a second specular.
        /// </summary>
        public const float AddCeiling = 0.10f;

        /// <summary>
        /// HLSL's <c>smoothstep(e0, e1, x)</c>, spelled out. <b>NOT <c>Mathf.SmoothStep</c></b> — see the
        /// class note. Returns 0 at/below <paramref name="edge0"/>, 1 at/above <paramref name="edge1"/>,
        /// Hermite between. Callers guarantee <c>edge1 &gt; edge0</c>; the max() is belt-and-braces.
        /// </summary>
        public static float SmoothstepEdge(float edge0, float edge1, float x)
        {
            float t = Mathf.Clamp01((x - edge0) / Mathf.Max(edge1 - edge0, MinGateBand));
            return t * t * (3f - 2f * t);
        }

        /// <summary>
        /// (1) THE WIND GATE — 0 at/below <paramref name="onset"/>, rising to 1 at/above
        /// <paramref name="full"/>. Monotone and saturating: a gale does not get more ripple than a
        /// blow, because by then the whitecaps own the surface. <paramref name="roughness"/> is the
        /// sim-pushed <c>_Roughness</c> (0..1); at 0 the result is EXACTLY 0 — glass stays glass.
        /// </summary>
        public static float WindGate01(float roughness, float onset, float full)
        {
            float lo = Mathf.Clamp01(onset);
            float hi = Mathf.Max(full, lo + MinGateBand);
            return SmoothstepEdge(lo, hi, Mathf.Clamp01(roughness));
        }

        /// <summary>
        /// (2) THE WINDWARD-FACE GATE. <paramref name="slopeAlongWind"/> is
        /// <c>dot(waveSlope, windDir)</c> off the shared wave field: POSITIVE where the surface climbs
        /// as you travel downwind — the windward (upwind) face of the wave ahead — and NEGATIVE in the
        /// lee behind a crest.
        ///
        /// <para><paramref name="gate"/> 0 puts ripples EVERYWHERE (returns exactly 1, the "off" end);
        /// 1 confines them to the windward faces, with <paramref name="leeFloor"/> deciding how much
        /// survives in the lee — never a hard zero, because a real lee is sheltered, not glass.</para>
        ///
        /// <para>⚠️ A dead field (no live trains, or a glassy sea) publishes zero slope, which reads as
        /// "not a windward face" and would erase the band. The shader therefore skips this gate entirely
        /// when the trains are not live; this function is only ever asked about a live field.</para>
        /// </summary>
        public static float WindwardGate01(float slopeAlongWind, float gate, float leeFloor)
        {
            float steep = Mathf.Clamp01(slopeAlongWind * SlopeNormalize);
            return Mathf.Lerp(1f, Mathf.Max(steep, Mathf.Clamp01(leeFloor)), Mathf.Clamp01(gate));
        }

        /// <summary>
        /// (3) THE FRAMING FADE — the anti-DENSITY guard that replaced the ADR's per-zoom-tier amplitude
        /// fade (see the class note; there is nothing sub-pixel to fade). Full strength at/below
        /// <paramref name="near"/> metres of sea on screen, easing to <paramref name="floor"/> at/above
        /// <paramref name="far"/>.
        ///
        /// <para><b><paramref name="framingMetres"/> ≤ 0 means the global was never published</b> — a bare
        /// material, an art scene with no <c>WaterSurface</c>, an inspector preview — and returns
        /// <b>1</b>, i.e. NO fade. The alternative (treating unset as "infinitely wide") would silently
        /// render the band blank exactly where someone is trying to look at it.</para>
        /// </summary>
        public static float FramingFade01(float framingMetres, float near, float far, float floor)
        {
            if (framingMetres <= 0f) return 1f;
            float lo = Mathf.Max(near, 0f);
            float hi = Mathf.Max(far, lo + MinGateBand);
            return Mathf.Lerp(1f, Mathf.Clamp01(floor), SmoothstepEdge(lo, hi, framingMetres));
        }

        /// <summary>
        /// The band's own QUANTIZATION — ADR 0027's "every layer carries its own quantization control,
        /// defaulting ON", which matters most here because <c>_DepthBands</c> is 0, so the base ramp
        /// contributes no pixel character of its own and an un-posterized ripple would read as a smooth
        /// airbrushed shimmer.
        ///
        /// <para>Posterizes 0..1 into <paramref name="bands"/> SOLID steps, Bayer-dithering ONLY inside
        /// <paramref name="ditherWin"/> (a fraction of a step) around each boundary — the shader's
        /// <c>BandValue01</c>, line for line, and the same style law the envelope bands hold:
        /// <b>dither at the EDGE only</b>, because full-range dither dissolves the steps back into the
        /// smooth gradient this game is not. Fewer than <see cref="MinBands"/> steps leaves the value
        /// smooth (the "0 = off" idiom).</para>
        ///
        /// <para><paramref name="bayer"/> is the world-locked 4×4 ordered-dither threshold at this pixel
        /// (<c>BayerWorld</c>), indexed by the PPU-quantized WORLD cell — so the dither cannot crawl
        /// under camera translation. That is the crawl law, and it is why this takes the threshold as an
        /// argument rather than deriving one.</para>
        /// </summary>
        public static float Band01(float v01, float bands, float ditherWin, float bayer)
        {
            if (bands < MinBands) return Mathf.Clamp01(v01);
            float x = Mathf.Clamp01(v01) * (bands - 1f);
            float fb = Mathf.Floor(x);
            float win = Mathf.Clamp(ditherWin, 1e-3f, 1f);
            float e = Mathf.Clamp01(((x - fb) - (0.5f - 0.5f * win)) / win);
            return (fb + (e > bayer ? 1f : 0f)) / (bands - 1f);
        }

        /// <summary>
        /// The finished amplitude the band is drawn at: the master strength through all three gates.
        /// Pins the composition — a PRODUCT, so <b>any single gate at zero kills the layer</b> (no wind,
        /// or the deep lee with a zero floor, or a framing past the far edge with a zero floor), and
        /// <c>strength = 0</c> is the EXACT passthrough the shipped material sits at.
        /// </summary>
        public static float Amplitude01(float strength, float windGate, float windwardGate, float framingFade)
            => Mathf.Clamp01(strength) * Mathf.Clamp01(windGate)
             * Mathf.Clamp01(windwardGate) * Mathf.Clamp01(framingFade);

        /// <summary>
        /// The signed brightness the shader adds to <c>col.rgb</c>: the posterized 0..1 band remapped to
        /// −1..1 (so troughs darken as well as crests lightening), scaled by
        /// <see cref="Amplitude01"/> and the <see cref="AddCeiling"/>. Bounded to ±<c>AddCeiling</c> by
        /// construction; the ADR 0015 palette guard-rail still owns the final colour downstream.
        /// </summary>
        public static float SignedAdd(float band01, float amplitude01)
            => (Mathf.Clamp01(band01) * 2f - 1f) * Mathf.Clamp01(amplitude01) * AddCeiling;
    }
}
