// ShadowOnWater.hlsl — how a cast sun shadow reads where it lies on open water.
//
// A shadow on a sea surface is not a hard dark bar: the ripples scatter the light the shadow takes away,
// so the shade comes apart into short dashes along the wave rows. ShadowOnWater (C#) decides per caster
// whether its foot is over water at the live tide and how much to break it; this file is the one place
// the breakup pattern is spelled, so the sprite casters and the mesh hulls break the same way.
//
// The pattern is a pure function of the world pixel (no time, no RNG): deterministic, the same in every
// frame, and it does not swim when the camera moves. Rows run east-west like the wave rows the camera
// sees; each row is cut into dashes of hashed length and offset.

#ifndef HH_SHADOW_ON_WATER_INCLUDED
#define HH_SHADOW_ON_WATER_INCLUDED

// Dave Hoskins' hash without sine: stable across GPUs for integer-valued inputs.
float HHWaterShadowHash(float2 p)
{
    float3 p3 = frac(float3(p.xyx) * 0.1031);
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.x + p3.y) * p3.z);
}

// The fraction of the shadow's darkness kept at this world point: 1 everywhere when amount is 0 (the
// land case, pixel-identical), and between 1 - amount and 1 on water.
//   worldXY        the fragment's world position
//   amount         0..1, published per renderer by ShadowOnWater
//   pixelsPerUnit  the art grid, so the dashes are whole pixels
//   rowPixels      the height of one wave row in pixels
//   dashPixels     the mean dash length in pixels
half HHShadowWaterKeep(float2 worldXY, float amount, float pixelsPerUnit, float rowPixels, float dashPixels)
{
    if (amount <= 0.0) return 1.0;
    float2 px = floor(worldXY * pixelsPerUnit);
    float row = floor(px.y / max(rowPixels, 1.0));
    float rowPhase = HHWaterShadowHash(float2(row, 17.0));
    float len = max(dashPixels, 1.0) * (0.6 + 0.8 * HHWaterShadowHash(float2(row, 5.0)));
    float dash = floor((px.x + rowPhase * 211.0) / len);
    float lit = HHWaterShadowHash(float2(dash, row));
    // About a third of each row's dashes let the light through; the rest keep a hashed share of the shade.
    half keep = lit < 0.33 ? 0.0 : (half)(0.55 + 0.45 * HHWaterShadowHash(float2(dash + 3.0, row + 11.0)));
    return lerp(1.0h, keep, (half)saturate(amount));
}

#endif
