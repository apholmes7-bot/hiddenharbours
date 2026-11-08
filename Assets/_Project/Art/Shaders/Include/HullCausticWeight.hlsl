#ifndef HH_HULL_CAUSTIC_WEIGHT_INCLUDED
#define HH_HULL_CAUSTIC_WEIGHT_INCLUDED
// Undo only the iso screen projection, retaining the hull's live heading, roll and pitch.
float2 HHHullHorizontalNormal(float3 worldNormal, float2 isoSinCos)
{
    float2 horizontal = float2(worldNormal.x, dot(worldNormal.yz, isoSinCos));
    return horizontal * rsqrt(max(dot(horizontal, horizontal), 1e-12));
}
// C# twin: CausticMath.EncodeAlpha. Gain precedes the existing 8-bit alpha quantizer.
float HHHullCausticEncodeAlpha(float gain, float pattern, float weight)
{
    return max(128.0 / 255.0, 1.0 - saturate(gain * pattern * weight) * 0.5);
}
// C# twin: HullCausticLighting.Weight. Exact zero outside the open band.
float HHHullCausticWeight(float height, float4 face, float4 settings, float sunElevation,
                         float3 dayTint, float dayGate, float chop, float amount, float4 optics,
                         float2 sunHorizontalDir, float awayFloor, float shadowStrength, float2 irradianceSettings)
{
    float width = settings.x;
    if (settings.z < 0.5 || face.x < 0.5 || face.y >= settings.w
        || height <= 0.0 || height >= width || width <= 0.0 || sunElevation <= 0.0) return 0.0;
    float sunUp = saturate(sunElevation);
    float irradiance = max(saturate(irradianceSettings.y), pow(sunUp, max(irradianceSettings.x, 0.01)));
    float fade = 1.0 - height / width;
    float incidence = clamp(1.5707963267948966 - asin(sunUp) - radians(max(optics.y, 0.0)) * saturate(chop),
                            0.0, 1.5707963267948966);
    float reflectance = optics.x + (1.0 - optics.x) * pow(1.0 - cos(incidence), max(optics.z, 1.0));
    // Hull slope: a wall receives full light; upward faces ease to zero at the existing cap.
    float hullSlope = saturate(1.0 - face.y / settings.w);
    float facing = max(saturate(dot(face.zw, sunHorizontalDir)), saturate(awayFloor) * saturate(chop));
    // PR 2: a cast shadow on the water before the face removes this light; a shadow on the face alone does not.
    return fade * fade * irradiance * reflectance * hullSlope
        * (1.0 - optics.w * saturate(chop)) * amount * settings.y * facing * saturate(shadowStrength);
}
#endif
