#ifndef HH_HULL_CAUSTIC_WEIGHT_INCLUDED
#define HH_HULL_CAUSTIC_WEIGHT_INCLUDED
// C# twin: HullCausticLighting.Weight. Exact zero outside the open band.
float HHHullCausticWeight(float height, float2 face, float4 settings, float sunElevation,
                         float3 dayTint, float dayGate, float chop, float amount)
{
    float width = settings.x;
    if (settings.z < 0.5 || face.x < 0.5 || face.y >= settings.w
        || height <= 0.0 || height >= width || width <= 0.0) return 0.0;
    float tintSum = dayTint.r + dayTint.g + dayTint.b;
    float sunUp = tintSum > 1e-3 ? saturate(sunElevation) : 1.0;
    float day = lerp(1.0, sunUp, saturate(dayGate));
    float fade = 1.0 - height / width;
    return fade * fade * day * (1.0 - saturate(chop)) * amount * settings.y;
}
#endif
