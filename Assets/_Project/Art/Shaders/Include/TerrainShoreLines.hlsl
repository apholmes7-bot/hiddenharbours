// Ground-only addressing and decals. The water's shared albedo include does not read these.
TEXTURE2D_ARRAY(_ShoreFields); SAMPLER(sampler_ShoreFields);
TEXTURE2D_ARRAY(_ShoreStrips); SAMPLER(sampler_ShoreStrips);
static const float SL_ANCHOR[5] = { 0.40, 0.24, 0.46, 0.34, 0.38 };

float SL_Step(int kind) { return kind == 4 ? _ShoreStep4 : _ShoreSteps0123[clamp(kind, 0, 3)]; }
float4 SL_LoadAddress(float2 wp, float2 uv, int kind, out float2 normal)
{
    int2 cell = clamp(int2(floor(uv * _ShoreGrid.xy)), int2(0, 0), int2(_ShoreGrid.xy) - 1);
    float4 packed = _ShoreFields.Load(int4(cell, kind, 0));
    normal = float2(cos(packed.b), sin(packed.b));
    if (abs(packed.a) < 0.5) return float4(8, 0, 1, 8);
    float2 center = _HeightWorldMin.xy + (float2(cell) + 0.5) * _ShoreGrid.zw * _HeightWorldSize.xy;
    float2 delta = wp - (center + packed.rg);
    float distance = length(delta);
    float d = distance * (dot(delta, normal) < 0.0 ? -1.0 : 1.0);
    float2 tangent = float2(-normal.y, normal.x) * sign(packed.a);
    float phase = (abs(packed.a) - 1.0 + dot(delta, tangent) / 8.0) * 6.28318530718;
    return float4(d, sin(phase), cos(phase), distance);
}
bool SL_InBand(float4 address, int kind)
{
    float t = address.r / 4.0 + SL_ANCHOR[kind];
    // Distance is to an actual curve point, never a blend between unrelated branches.
    float reach = 4.0 * (address.r < 0.0 ? SL_ANCHOR[kind] : 1.0 - SL_ANCHOR[kind]) - (1.0 / 256.0);
    return address.a >= 0.0 && address.a <= reach && SL_Step(kind) >= 0.0 && t >= 0.0 && t <= 1.0;
}

float2 SL_Prepare(float2 wp, float2 uv, out float4 addresses[5])
{
    for (int clear = 0; clear < 5; clear++) addresses[clear] = float4(8, 0, 1, 8);
    [branch] if (_ShoreLoaded < 0.5) return wp;
    float2 offset = float2(0, 0);
    float nearest = 1e6;
    [unroll] for (int kind = 0; kind < 5; kind++)
    {
        float2 normal;
        float4 a = SL_LoadAddress(wp, uv, kind, normal);
        addresses[kind] = a;
        [branch] if (SL_InBand(a, kind) && abs(a.r) < nearest)
        {
            nearest = abs(a.r);
            float halfSource = max(_ShoreSourceBlendMetres, 0.01) * 0.5;
            float ratio = max(_ShoreSourceBlendMetres, 0.01) / max(_ShoreBlendMetres, 0.01);
            float expanded = sign(a.r) * min(abs(a.r) * ratio, halfSource);
            offset = abs(a.r) < halfSource ? normal * (expanded - a.r) : float2(0, 0);
        }
    }
    return wp + offset;
}

float3 SL_Over(float3 colour, float4 addresses[5])
{
    [branch] if (_ShoreLoaded < 0.5) return colour;
    // Seaward first, landward last. Each enabled strip makes ONE PNG-array sample in its 4m band.
    [unroll] for (int order = 0; order < 5; order++)
    {
        int kind = 4 - order;
        float4 a = addresses[kind];
        [branch] if (SL_InBand(a, kind))
        {
            float s = atan2(a.g, a.b) * (1.0 / 6.28318530718);
            float t = a.r / 4.0 + SL_ANCHOR[kind];
            float slice = kind * 3 + clamp(round(SL_Step(kind)), 0, 2);
            half4 edge = SAMPLE_TEXTURE2D_ARRAY_LOD(_ShoreStrips, sampler_ShoreStrips, float2(s, t), slice, 0);
            colour = lerp(colour, edge.rgb, edge.a);
        }
    }
    return colour;
}
