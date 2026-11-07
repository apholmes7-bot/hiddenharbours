// Ground-only addressing and decals. The water's shared albedo include does not read these.
TEXTURE2D_ARRAY(_ShoreFields); SAMPLER(sampler_ShoreFields);
TEXTURE2D_ARRAY(_ShoreStrips); SAMPLER(sampler_ShoreStrips);
static const float SL_ANCHOR[5] = { 0.40, 0.24, 0.46, 0.34, 0.38 };

float SL_Step(int kind) { return kind == 4 ? _ShoreStep4 : _ShoreSteps0123[clamp(kind, 0, 3)]; }
bool SL_InBand(float4 address, int kind)
{
    float t = address.r / 4.0 + SL_ANCHOR[kind];
    return address.a > 0.99 && SL_Step(kind) >= 0.0 && t >= 0.0 && t <= 1.0;
}

// A distance derivative transformed from screen to world, independent of camera rotation/zoom.
float2 SL_Normal(float d, float2 dx, float2 dy)
{
    float det = dx.x * dy.y - dx.y * dy.x;
    float ddxD = ddx(d), ddyD = ddy(d);
    float2 gradient = float2(ddxD * dy.y - dx.y * ddyD, dx.x * ddyD - ddxD * dy.x);
    return gradient * sign(det) / max(length(gradient), 1e-6);
}

float2 SL_Prepare(float2 wp, float2 uv, out float4 addresses[5])
{
    for (int clear = 0; clear < 5; clear++) addresses[clear] = float4(8, 0, 1, 0);
    [branch] if (_ShoreLoaded < 0.5) return wp;
    float2 dx = ddx(wp), dy = ddy(wp), offset = float2(0, 0);
    float nearest = 1e6;
    [unroll] for (int kind = 0; kind < 5; kind++)
    {
        float4 a = SAMPLE_TEXTURE2D_ARRAY_LOD(_ShoreFields, sampler_ShoreFields, uv, kind, 0);
        addresses[kind] = a;
        // Derivatives stay outside spatially divergent flow.
        float2 normal = SL_Normal(a.r, dx, dy);
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
