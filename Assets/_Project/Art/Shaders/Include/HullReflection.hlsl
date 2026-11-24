#ifndef HH_HULL_MESH_REFLECTION_INCLUDED
#define HH_HULL_MESH_REFLECTION_INCLUDED
// Reflection-only storage protocol: untouched = 0, blocked = 128/255, solid = 1.
#define HH_MESH_BLOCKER (128.0 / 255.0)
float4x4 _HHMeshReflectMatrix;
float4 _HHMeshReflectPlane;
float4 _HHMeshReflectSize;
float4 _HHMeshReflectDepthRange;
// THE FADE (the sunset rowing clip, 11-22): x = metres below the waterline the reflection starts to
// thin, y = 1 / the metres it takes to be gone (0 = no fade, every fragment drawn as before). The
// mirrored point is as far below the line as its source stands above it, so this is the source
// height. z = the streak length in the hull's own pixels: each pixel row of the reflection is cut
// into runs that thin out together, so it breaks into horizontal streaks the way a reflection on
// moving water does (0 = the hull's 4x4 ordered dither instead). Twin: HullMeshReflection.FadeKeep /
// FadeThreshold / FadeBlocks.
float4 _HHMeshReflectFade;
float3 HHMeshReflectPoint(float3 p) { return mul(_HHMeshReflectMatrix, float4(p, 1)).xyz; }
float HHMeshReflectFadeKeep(float height)
{
    return 1.0 - saturate((height - _HHMeshReflectFade.x) * _HHMeshReflectFade.y);
}
// Interleaved gradient noise on whole cells: a fixed, even spread of thresholds in [0,1).
float HHMeshReflectCellNoise(float2 c) { return frac(52.9829189 * frac(dot(c, float2(0.06711056, 0.00583715)))); }
// The threshold a fragment's kept share is measured against: the run of its row it falls in when
// streaks are on (each row's runs start at their own offset), else the ordered dither it was given.
float HHMeshReflectFadeThreshold(int2 cell, float bayer)
{
    float len = _HHMeshReflectFade.z;
    if (len < 1.0) return bayer;
    float row = (float)cell.y;
    float run = floor(((float)cell.x + HHMeshReflectCellNoise(float2(0.0, row)) * len) / len);
    return HHMeshReflectCellNoise(float2(run, row));
}
bool HHMeshIsBlocker(float a) { return a > 0.25 && a < 0.75; }

// THE RAGGED ENDS (11-22): under a square stern or bow every streak row of the reflection ended on
// the same column, a straight vertical edge that travelled with the boat. Each row now stops short
// of the hull's outline by its own amount and frays over a few pixels inside that, all on the
// hull's own cell grid so nothing crawls as she moves. x = the most hull pixels a row may stop
// short (each row draws 0..x), y = the pixels its end thins over, z = the shortest row (in hull
// pixels) that frays at all, so an oar, rod or mast keeps its ends; w = 1 while on (0 = off, and
// the reflection pass writes the keyline colour as before). While on, the reflection pass stores
// each fragment's hull cell (x & 255, y & 255, 255) in the key target's colour, which the
// resolve reads back to measure a row's end. Twin: HullMeshReflection.PackEnds / EndHash01 /
// EndThins / EndDrops.
float4 _HHMeshReflectEnds;
// The key target is R8G8B8A8_SRGB: a byte written as its linear value is stored exactly.
float3 HHMeshReflectLinearOfBytes(float3 bytes)
{
    float3 s = bytes / 255.0;
    return s <= 0.04045 ? s / 12.92 : pow((max(s, 0.04045) + 0.055) / 1.055, 2.4);
}
float3 HHMeshReflectBytesOfLinear(float3 lin)
{
    float3 s = lin <= 0.0031308 ? lin * 12.92 : 1.055 * pow(max(lin, 0.0031308), 1.0 / 2.4) - 0.055;
    return floor(s * 255.0 + 0.5);
}
float3 HHMeshReflectCellKey(int2 cell)
{
    return HHMeshReflectLinearOfBytes(float3(cell.x & 255, cell.y & 255, 255));
}
// lowbias32: an integer hash with no visible pattern between neighbouring rows, which the
// interleaved gradient noise above has (its rows step by a fixed 0.31).
uint HHMeshReflectHash(uint x)
{
    x ^= x >> 16; x *= 0x7feb352du;
    x ^= x >> 15; x *= 0x846ca68bu;
    x ^= x >> 16;
    return x;
}
float HHMeshReflectEndHash01(uint key) { return (float)(HHMeshReflectHash(key) >> 8) * (1.0 / 16777216.0); }
// Whether a payload cell d hull pixels in from its row's end (d = 0: the outermost) is dropped.
// row and cellX are the hull cell's low bytes; side 0 = the row's right end, 1 = its left.
bool HHMeshReflectEndThins(int d, uint row, uint cellX, uint side)
{
    int cut = (int)floor(HHMeshReflectEndHash01(row | (side << 8) | (1u << 10)) * (_HHMeshReflectEnds.x + 1.0));
    if (d < cut) return true;
    float t = (float)(d - cut + 1) / (_HHMeshReflectEnds.y + 1.0);
    return t < 1.0 && HHMeshReflectEndHash01(cellX | (row << 8) | (side << 16) | (1u << 17)) >= t;
}
#ifdef HH_REFLECTION_RESOLVE
bool HHMeshBlockerAt(int2 p) { return HHMeshIsBlocker(_HHKeyTex.Load(int3(p, 0)).a); }
// The longest scan along a row, in target texels: a bound on the loop, never reached at the game's
// zooms (a hull pixel is a few texels); a scan that hits it reads as "no end near".
#define HH_MESH_REFLECT_END_SCAN 96
// Hull pixels from p to its row's end in direction dir (0 = p is the row's last covered cell), or
// -1 when no end lies within `limit` hull pixels. Covered = a payload or a blocker; a screen edge is
// no end. Cell x is compared in its low byte, so the step is taken modulo 256.
int HHMeshReflectEndDistance(int2 p, int dir, int limit, int cellX, uint w)
{
    int last = cellX;
    [loop]
    for (int s = 1; s <= HH_MESH_REFLECT_END_SCAN; s++)
    {
        int2 q = int2(p.x + dir * s, p.y);
        if (q.x < 0 || q.x >= (int)w) return -1;
        float4 k = _HHKeyTex.Load(int3(q, 0));
        int d = ((last - cellX) * dir) & 255;
        if (k.a <= 0.25) return d < limit ? d : -1;
        last = (int)HHMeshReflectBytesOfLinear(k.rgb).x;
        if ((((last - cellX) * dir) & 255) >= limit) return -1;
    }
    return -1;
}
// The resolve's half of the ragged ends: true when this payload texel is dropped. A dropped texel
// draws nothing, exactly as a blocker does: nothing mirrored behind it can show.
bool HHMeshReflectEndDrops(int2 p, uint w)
{
    if (_HHMeshReflectEnds.w < 0.5) return false;
    float3 b = HHMeshReflectBytesOfLinear(_HHKeyTex.Load(int3(p, 0)).rgb);
    if (b.z != 255.0) return false;
    int cellX = (int)b.x;
    uint row = (uint)b.y;
    int band = (int)(_HHMeshReflectEnds.x + _HHMeshReflectEnds.y);
    int run = (int)_HHMeshReflectEnds.z;
    int dR = HHMeshReflectEndDistance(p, 1, band, cellX, w);
    int dL = HHMeshReflectEndDistance(p, -1, band, cellX, w);
    if (dR < 0 && dL < 0) return false;
    // A short row (an oar, a rod, a mast) keeps its ends.
    if (dL < 0) dL = HHMeshReflectEndDistance(p, -1, run, cellX, w);
    else if (dR < 0) dR = HHMeshReflectEndDistance(p, 1, run, cellX, w);
    if (dL >= 0 && dR >= 0 && dL + dR + 1 < run) return false;
    return (dR >= 0 && dR < band && HHMeshReflectEndThins(dR, row, (uint)cellX, 0u))
        || (dL >= 0 && dL < band && HHMeshReflectEndThins(dL, row, (uint)cellX, 1u));
}
#endif
float HHMeshReflectClipDepth(float worldDepth)
{
    float d = saturate((worldDepth - _HHMeshReflectDepthRange.x) * _HHMeshReflectDepthRange.y);
#if UNITY_REVERSED_Z
    return 1.0 - d;
#else
    return lerp(UNITY_NEAR_CLIP_VALUE, 1.0, d);
#endif
}
#endif
