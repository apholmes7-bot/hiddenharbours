#ifndef HIDDEN_HARBOURS_STILL_WATER_INCLUDED
#define HIDDEN_HARBOURS_STILL_WATER_INCLUDED

// StillWater.hlsl — the SHARED read of the region's STILL WATER above the tide (ADR 0046): ponds and a
// brook's fresh reach, standing ABOVE the sea. Included by the two shaders that draw a waterline — the
// water (its drawn edge) and the tidal faces (where a face stops being wet) — so they read one level.
//
// ⭐ ONE MAP, THE SIM'S OWN. The globals are published by HiddenHarbours.Art.StillWaterGlobals from the
// Core seam's own map (GameServices.StillWater.Map): the SAME texture, rect and range that
// PaintedStillWater decodes for the on-foot sim. The pond this draws is the pond the fisher wades.
//
//        _HHStillTex    R = the still level on the map's own range; 0 = no still water here
//        _HHStillRect   (minX, minY, sizeX, sizeY), world metres
//        _HHStillRange  (min level, max level, bound 0/1, 0), metres above datum
//
// ⚠️ TWINNED: HiddenHarbours.Core.StillWaterLevels.DecodeCode01 (r > 0 → lerp(min, max, r), else none)
// and PaintedStillWater.StillLevelAt (outside the rect → none; codes filtered, THEN decoded). Change one,
// change all three in the same PR. The water composes as max(tide, still) — StillWaterLevels.Compose.
//
// UNSET IS A PIXEL-IDENTICAL PASSTHROUGH: with no still map (every region today) the publisher binds a
// 1x1 black fallback, a zero rect and bound = 0, and StillLevelAt answers -1e30, so max(tide, -1e30) is
// the tide bit for bit and both shaders draw exactly the picture they drew before this seam.
//
// Globals, declared OUTSIDE any per-material CBUFFER (the SRP batcher's layout rule). Explicit LOD 0:
// the still map carries no mips, the call is legal in a vertex stage and inside a branch, and the
// fragment and vertex stages read the same level. Visual only: drives no sim, saves nothing (rule 5).

TEXTURE2D(_HHStillTex);   SAMPLER(sampler_HHStillTex);
float4 _HHStillRect;
float4 _HHStillRange;

// The still level (metres above datum) at a world position, or -1e30 where there is none.
float StillLevelAt(float2 worldXY)
{
    if (_HHStillRange.z < 0.5) return -1e30;
    float2 uv = (worldXY - _HHStillRect.xy) / max(_HHStillRect.zw, float2(1e-3, 1e-3));
    if (any(uv < 0.0) || any(uv > 1.0)) return -1e30;
    float r = SAMPLE_TEXTURE2D_LOD(_HHStillTex, sampler_HHStillTex, uv, 0).r;
    return r > 0.0 ? lerp(_HHStillRange.x, _HHStillRange.y, r) : -1e30;
}

#endif // HIDDEN_HARBOURS_STILL_WATER_INCLUDED
