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
#ifdef HH_REFLECTION_RESOLVE
bool HHMeshBlockerAt(int2 p) { return HHMeshIsBlocker(_HHKeyTex.Load(int3(p, 0)).a); }
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
