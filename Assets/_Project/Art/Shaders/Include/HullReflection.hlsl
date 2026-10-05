#ifndef HH_HULL_MESH_REFLECTION_INCLUDED
#define HH_HULL_MESH_REFLECTION_INCLUDED
// Reflection-only storage protocol: untouched = 0, blocked = 128/255, solid = 1.
#define HH_MESH_BLOCKER (128.0 / 255.0)
float4x4 _HHMeshReflectMatrix;
float4 _HHMeshReflectPlane;
float4 _HHMeshReflectSize;
float4 _HHMeshReflectDepthRange;
float3 HHMeshReflectPoint(float3 p) { return mul(_HHMeshReflectMatrix, float4(p, 1)).xyz; }
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
