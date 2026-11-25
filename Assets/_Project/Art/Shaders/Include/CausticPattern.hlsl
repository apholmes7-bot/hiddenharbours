#ifndef HH_CAUSTIC_PATTERN_INCLUDED
#define HH_CAUSTIC_PATTERN_INCLUDED

// Optical value only. Callers supply the independent/painted value and five heights
// from the same sea, world grid and published phases. C# twin: CausticMath.Pattern.
// Depth, day, tint and transmission stay at their existing composite locations.
float HHCausticPattern(float independentPattern, float centre, float positiveX, float negativeX,
                      float positiveY, float negativeY, float step, float gain,
                      float blend, float waveEnvelope)
{
    if (blend > 0.001)
    {
        float spacing = max(step, 1e-3);
        float laplacian = (positiveX + negativeX + positiveY + negativeY - 4.0 * centre)
                          / (spacing * spacing);
        float focused = saturate(-laplacian * max(gain, 0.0));
        float fieldLive = saturate(waveEnvelope * 40.0);
        return lerp(independentPattern, focused, saturate(blend) * fieldLive);
    }
    return independentPattern;
}

#endif

// Private pure vein helpers retain the sea's arithmetic verbatim. The water's general
// helpers remain in place for its other layers and their existing source contracts.
// No seabed, wave, fetch, shelter or wake sampler is included here.
#if defined(HH_CAUSTIC_VEINS) && !defined(HH_CAUSTIC_VEINS_INCLUDED)
#define HH_CAUSTIC_VEINS_INCLUDED
float2 HHCausticPixelize(float2 p)
{
    float ppu = max(_PixelsPerUnit, 1.0);
    return floor(p * ppu) / ppu;
}

float HHCausticBandFreq(float scale)
{
    return scale / (1.0 + max(_BandScaleResponse, 0.0) * saturate(_Chop));
}

float HHCausticHash21(float2 p)
{
    p = frac(p * float2(123.34, 345.45));
    p += dot(p, p + 34.345);
    return frac(p.x * p.y);
}

float2 HHCausticHash22(float2 p)
{
    p = float2(dot(p, float2(127.1, 311.7)), dot(p, float2(269.5, 183.3)));
    return frac(sin(p) * 43758.5453);
}

float HHCausticValueNoise(float2 p)
{
    float2 i = floor(p);
    float2 f = frac(p);
    float2 u = f * f * (3.0 - 2.0 * f);   // smoothstep weights
    float a = HHCausticHash21(i + float2(0, 0));
    float b = HHCausticHash21(i + float2(1, 0));
    float c = HHCausticHash21(i + float2(0, 1));
    float d = HHCausticHash21(i + float2(1, 1));
    return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
}

float2 HHCausticPaintUV(float2 worldXY, float scale, float2 scroll)
{
    return HHCausticPixelize(worldXY + scroll) * max(scale, 1e-4);
}

half4 HHCausticUntileSampleW(TEXTURE2D_PARAM(tex, smp), float2 worldXY, float scale, float2 scroll, float strength)
{
    float s = saturate(strength);
    // (1) domain warp: a small world-space nudge from the surface noise, scaled by strength.
    // Two warp octaves (low-freq bend + a finer ripple) read more organic than one straight nudge;
    // both still dialed by _UntileStrength (no new knob) so 0 strength = the raw grid unchanged.
    // ADR 0027 #4: the untile warp fields read the surface band's EFFECTIVE frequency
    // (HHCausticBandFreq) so the painted-texture break-up coarsens with the same growing sea —
    // one uniform, one meaning (bit-exact at response 0: division by exactly 1.0).
    float2 warpLo = float2(HHCausticValueNoise(worldXY * HHCausticBandFreq(_NoiseScale) * 0.5 + 3.1),
                           HHCausticValueNoise(worldXY * HHCausticBandFreq(_NoiseScale) * 0.5 + 8.7)) - 0.5;
    float2 warpHi = float2(HHCausticValueNoise(worldXY * HHCausticBandFreq(_NoiseScale) * 1.7 + 17.3),
                           HHCausticValueNoise(worldXY * HHCausticBandFreq(_NoiseScale) * 1.7 + 42.9)) - 0.5;
    float2 warpN = warpLo + warpHi * 0.4;
    float2 warped = worldXY + warpN * (s * 1.5);

    half4 raw = SAMPLE_TEXTURE2D(tex, smp, HHCausticPaintUV(warped, scale, scroll));
    if (s <= 0.001) return raw;

    // (2) hash-untile in TILE space (uv = warped*scale; one repeat-cell == 1 unit of uv).
    float2 uv  = HHCausticPixelize(warped + scroll) * max(scale, 1e-4);
    float2 iuv = floor(uv);
    float2 fuv = frac(uv);
    // The FOUR cell-corner offsets, each hashed to a per-cell world translation (world-space,
    // so HHCausticPaintUV still snaps every lookup to the PPU grid). Corner (i+1,j) is the SAME corner
    // the next cell along x calls its own (i,j) — that shared identity is what closes the seam.
    float2 off00 = HHCausticHash22(iuv + float2(0.0, 0.0)) * 64.0;   // a few tiles of world translation
    float2 off10 = HHCausticHash22(iuv + float2(1.0, 0.0)) * 64.0;
    float2 off01 = HHCausticHash22(iuv + float2(0.0, 1.0)) * 64.0;
    float2 off11 = HHCausticHash22(iuv + float2(1.0, 1.0)) * 64.0;
    half4 v00 = SAMPLE_TEXTURE2D(tex, smp, HHCausticPaintUV(warped + off00, scale, scroll));
    half4 v10 = SAMPLE_TEXTURE2D(tex, smp, HHCausticPaintUV(warped + off10, scale, scroll));
    half4 v01 = SAMPLE_TEXTURE2D(tex, smp, HHCausticPaintUV(warped + off01, scale, scroll));
    half4 v11 = SAMPLE_TEXTURE2D(tex, smp, HHCausticPaintUV(warped + off11, scale, scroll));
    // Bilinear corner weights on a smoothstep ramp: a partition of unity (so the blend can
    // never brighten or darken the slot) whose members are each 0 on the two edges they do not
    // touch. Twin: WaterUntile.CornerWeights.
    float bx = smoothstep(0.0, 1.0, fuv.x);
    float by = smoothstep(0.0, 1.0, fuv.y);
    half4 untiled = lerp(lerp(v00, v10, bx), lerp(v01, v11, bx), by);
    // dial raw(+warp) <-> untiled by strength.
    return lerp(raw, untiled, s);
}

float HHCausticVeins(float2 worldXY, float t, float painted,
                     TEXTURE2D_PARAM(causticTexture, causticSampler))
{
    float2 cp = HHCausticPixelize(worldXY * _CausticScale + float2(t * _Flow, -t * _Flow * 0.7));
    float ca = HHCausticValueNoise(cp);
    float cb = HHCausticValueNoise(cp * 1.7 + 11.1);
    float caustic = pow(saturate(1.0 - abs(ca - cb) * 3.0), 2.0);
    if (painted > 0.5)
    {
        float2 cScroll = float2(t * _Flow * 0.6, -t * _Flow * 0.4);
        float ct = HHCausticUntileSampleW(TEXTURE2D_ARGS(causticTexture, causticSampler),
                       worldXY, _PaintScale * 2.0, cScroll, _UntileStrength).r
                 * HHCausticUntileSampleW(TEXTURE2D_ARGS(causticTexture, causticSampler),
                       worldXY, _PaintScale * 2.0, -cScroll * 1.3, _UntileStrength).r;
        caustic = lerp(caustic, ct * 2.0, _CausticTexStrength);
    }
    return caustic;
}
#endif
