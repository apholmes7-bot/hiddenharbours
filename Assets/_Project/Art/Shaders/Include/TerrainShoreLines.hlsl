// Ground-only addressing and strips. The water's floor does not read these.
TEXTURE2D_ARRAY(_ShoreFields); SAMPLER(sampler_ShoreFields);
TEXTURE2D_ARRAY(_ShoreStrips); SAMPLER(sampler_ShoreStrips);
Texture2D<float4> _ShoreSegments;
Texture2D<float> _ShoreIndexList;
Texture2D<float4> _ShoreWallMask;
static const float SL_ANCHOR[5] = { 0.40, 0.24, 0.46, 0.34, 0.38 };
float SL_Step(int kind) { return kind == 4 ? _ShoreStep4 : _ShoreSteps0123[clamp(kind, 0, 3)]; }

float4 SL_LoadAddress(float2 wp, float2 uv, int kind, out float2 normal)
{
    float4 best=float4(8,0,0,8);normal=float2(0,1);
    int2 cell=clamp(int2(floor(uv*_ShoreGrid.xy)),int2(0,0),int2(_ShoreGrid.xy)-1);
    float4 packed=_ShoreFields.Load(int4(cell,kind,0));
    int start=(int)round(packed.r)+1024*(int)round(packed.g)+1048576*(int)round(packed.b);
    int count=(int)round(packed.a);
    // The load-time list contains every segment that can win anywhere in this cell.
    [loop] for(int candidate=0;candidate<count;candidate++)
    {
        int offset=start+candidate;
        int id=(int)round(_ShoreIndexList.Load(int3(offset%1024,offset/1024,0)));
        float4 ends=_ShoreSegments.Load(int3(id,0,0));
        float4 segment=_ShoreSegments.Load(int3(id,1,0));
        float2 ab=ends.zw-ends.xy;
        float t=saturate(dot(wp-ends.xy,ab)/max(dot(ab,ab),1e-8));
        float2 delta=wp-(ends.xy+ab*t);float distance=length(delta);
        if(distance<best.a){normal=segment.xy;best=float4(distance*(dot(delta,normal)<0?-1:1),segment.z+t*length(ab),segment.w,distance);}
    }
    return best;
}
bool SL_InBand(float4 address, int kind)
{
    float t = address.r / 4.0 + SL_ANCHOR[kind];
    float reach = 4.0 * (address.r < 0.0 ? SL_ANCHOR[kind] : 1.0 - SL_ANCHOR[kind]) - (1.0 / 256.0);
    return address.a >= 0.0 && address.a <= reach && SL_Step(kind) >= 0.0 && t >= 0.0 && t <= 1.0;
}
float SL_BandFade(float4 address, int kind)
{
    float land = address.r + SL_ANCHOR[kind] * 4.0;
    float sea = (1.0 - SL_ANCHOR[kind]) * 4.0 - address.r;
    return smoothstep(0, _ShoreBlendMetres, min(land, sea));
}
uint SL_Hash(uint x)
{
    x ^= x >> 16; x *= 0x7feb352du; x ^= x >> 15; x *= 0x846ca68bu; return x ^ (x >> 16);
}
float SL_Random(int cell, uint salt)
{
    return (SL_Hash(asuint(cell) ^ asuint(_ShoreSeed) ^ (salt * 0x9e3779b9u)) >> 8) * (1.0 / 16777216.0);
}
float SL_Noise(float p, uint salt)
{
    int cell = (int)floor(p); float t = frac(p); t = t * t * (3.0 - 2.0 * t);
    return lerp(SL_Random(cell, salt), SL_Random(cell + 1, salt), t);
}
uint SL_Variant(int cell)
{
    return (asuint(cell) & 1u) | ((SL_Hash(asuint(cell) ^ asuint(_ShoreSeed)) & 1u) << 1);
}
float2 SL_VaryUV(float4 address, int kind)
{
    float along = address.g;
    return float2(along * address.b / 8.0 + SL_Noise(along / 3.7, 31u + kind) * 1.7,
        address.r / 4.0 + SL_ANCHOR[kind] + (SL_Noise(along / 2.3, 47u + kind) - 0.5) * 0.06);
}
float SL_Wrack(float4 address, out float2 uv)
{
    float along = address.g, pitch = _ShoreWrack.z;
    int cell = (int)floor(along / pitch);
    float center = (cell + 0.5) * pitch + (SL_Random(cell, 3u) * 2 - 1) * _ShoreWrack.w;
    float length = lerp(_ShoreWrack.x, _ShoreWrack.y, SL_Random(cell, 5u));
    float across = _ShorePattern.x;
    float2 local = float2((along - center) / (length * 0.5), address.r / (across * 0.5));
    // Alternating parity gives four variants and prevents identical neighbouring variants.
    uint variant = SL_Variant(cell);
    float angle = SL_Random(cell, 7u) * 6.28318530718;
    float2 rotated = float2(local.x * cos(angle) - local.y * sin(angle), local.x * sin(angle) + local.y * cos(angle));
    if ((variant & 2u) != 0u) rotated.x = -rotated.x;
    uv = frac(float2(SL_Random(cell, 11u), SL_Random(cell, 13u)) + rotated * lerp(0.19, 0.43, SL_Random(cell, 17u)));
    float ragged = 0.82 + 0.18 * SL_Noise(along * 3.1, 19u);
    float ellipse = dot(local, local);
    float fade = max(0.03, _ShoreBlendMetres / max(0.5, length * 0.5));
    return (1 - smoothstep(ragged - fade, ragged, ellipse)) * step(abs(local.x), 1) * step(abs(local.y), 1);
}
float SL_Lip(float4 address)
{
    float along = address.g;
    int cell = (int)floor(along / _ShoreWrack.z);
    float gap = lerp(_ShoreLip.z, _ShoreLip.w, SL_Random(cell, 61u));
    float center = (cell + 0.5) * _ShoreWrack.z + (SL_Random(cell, 67u) - 0.5);
    float broken = smoothstep(gap * 0.5, gap * 0.5 + _ShoreBlendMetres, abs(along - center));
    float tooth = SL_Noise(along / max(_ShorePattern.y, 0.1), 71u) * 0.65
        + SL_Noise(along / max(_ShorePattern.y * 2.7, 0.1), 73u) * 0.35;
    float depth = lerp(_ShoreLip.x, _ShoreLip.y, tooth);
    float landward = -address.r;
    return broken * smoothstep(0, _ShoreBlendMetres, landward)
        * (1 - smoothstep(max(0, depth - _ShoreBlendMetres), depth, landward));
}

float SL_WallProtected(float2 uv)
{
    // seat ruling s12, 11-07: the load-time triangle mask already includes one address texel.
    // Painted Talus/Ledge/Shelf on the exposed foreshore are not wall faces.
    if (_ShoreWallsLoaded < 0.5) return 0;
    int2 cell = clamp(int2(floor(uv * _ShoreWallGrid.xy)), int2(0, 0), int2(_ShoreWallGrid.xy) - 1);
    return _ShoreWallMask.Load(int3(cell, 0)).r;
}
float2 SL_Prepare(float2 wp, float2 uv, float protectedFoot, float organic, float beach, out float4 addresses[5])
{
    for (int clear = 0; clear < 5; clear++) addresses[clear] = float4(8, 0, 0, 8);
    [branch] if (_ShoreLoaded < 0.5 || SL_WallProtected(uv) > 0.5) return wp;
    float2 offset = float2(0, 0); float nearest = 1e6;
    [unroll] for (int kind = 0; kind < 5; kind++)
    {
        float2 normal;
        float4 a = SL_LoadAddress(wp, uv, kind, normal);
        if (kind < 2 && (organic < 0.5 || beach >= 0.01)) a = float4(8, 0, 0, 8);
        addresses[kind] = a;
        [branch] if (SL_InBand(a, kind) && abs(a.r) < nearest)
        {
            nearest = abs(a.r);
            float halfSource = max(_ShoreSourceBlendMetres, 0.01) * 0.5;
            float ratio = max(_ShoreSourceBlendMetres, 0.01) / max(_ShoreBlendMetres, 0.01);
            float expanded = sign(a.r) * min(abs(a.r) * ratio, halfSource);
            float coverage = kind == 0 ? SL_Lip(a) : 1;
            if (kind == 2) { float2 unusedUV; coverage = SL_Wrack(a, unusedUV); }
            offset = abs(a.r) < halfSource ? normal * (expanded - a.r) * coverage * SL_BandFade(a, kind) : float2(0, 0);
        }
    }
    return wp + offset;
}
float SL_Layer(float4 a, int kind, float3 colour, out float3 layerColour)
{
    layerColour = colour;
    [branch] if (SL_InBand(a, kind))
    {
        float2 uv = SL_VaryUV(a, kind);
        float opacity = SL_BandFade(a, kind);
        if (kind == 2) opacity *= SL_Wrack(a, uv);
        if (kind == 0)
        {
            opacity *= SL_Lip(a);
            uv.y = 0.45 + saturate(-a.r / max(_ShoreLip.y, 0.1)) * 0.3;
        }
        float slice = kind * 3 + clamp(round(SL_Step(kind)), 0, 2);
        [branch] if (opacity > 0)
        {
            half4 edge = SAMPLE_TEXTURE2D_ARRAY_LOD(_ShoreStrips, sampler_ShoreStrips, uv, slice, 0);
            if (kind == 0)
            {
                float3 underside = min(colour * 0.18, _ShoreLipShadow.rgb);
                float grey = dot(underside, float3(0.2126, 0.7152, 0.0722));
                edge.rgb = lerp(grey.xxx, underside, 0.25);
            }
            layerColour = edge.rgb;
            return edge.a * opacity;
        }
    }
    return 0;
}
float3 SL_Over(float3 colour, float4 addresses[5])
{
    [branch] if (_ShoreLoaded < 0.5) return colour;
    // Seaward first. One PNG-array sample, only inside the four-metre envelope.
    [unroll] for (int order = 0; order < 5; order++)
    {
        int kind = 4 - order;
        float3 layerColour;
        float opacity = SL_Layer(addresses[kind], kind, colour, layerColour);
        if (opacity > 0) colour = lerp(colour, layerColour, opacity);
    }
    return colour;
}
