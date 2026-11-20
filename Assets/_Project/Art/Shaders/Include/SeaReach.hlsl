#ifndef HIDDEN_HARBOURS_SEA_REACH_INCLUDED
#define HIDDEN_HARBOURS_SEA_REACH_INCLUDED

// SeaReach.hlsl - how much of the sea's SWELL reaches a point (owner, 2026-11-19: "once they arent
// attached to the waves they should remain still pools of water"). Included by the water (its wave lift,
// breathing edge, surf and shore foam) and the foam buffer's advect pass (the surf's deposit), so both
// read one map.
//
// ONE MAP, THE SIM'S OWN. HiddenHarbours.Art.SeaReachGlobals publishes these globals from the SAME
// HiddenHarbours.Core.SeaReachField the CPU readers ask through SeaReach.FactorAt: the region's seabed,
// flooded once from the open sea, storing per cell the lowest water level at which that cell joins the
// sea (its SILL). Nothing is flooded per frame; the tide moves, the map never does.
//
//        _HHSeaReachTex     R = the sill (m above datum); -60000 = always open, +60000 = never reached
//                           G = the cell's seabed height (m above datum), for the wetness below
//        _HHSeaReachRect    (minX, minY, sizeX, sizeY), world metres
//        _HHSeaReachParams  (blend metres, bound 0/1, width cells, height cells)
//        _HHSeaReachWet     (wet band metres, 0, 0, 0)
//
// TWIN: HiddenHarbours.Core.SeaReachMath.Factor01 and SeaReachField.FactorAt (same cells, same weights,
// same off-map rule, same smoothstep). Change one, change both in the same PR.
//
// POINT READS, NEVER A FILTERED SAMPLE: four Load()s of the nearest cells, blended bilinearly between
// their centres in code, each weighted by how WET the cell stands at this level. Dry ground never votes,
// so a cut-off pool's rim stays still and the sea beside a dry bar keeps its swell, while a reconnecting
// pool's outline is feathered over a cell instead of drawn in cell steps. A hardware filter would let a
// dry cell's value into the water. No sampler: legal in the vertex stage and inside a branch.
//
// UNSET IS A PIXEL-IDENTICAL PASSTHROUGH: with no map bound (a region with no seabed, the setting off)
// SeaReach01 returns exactly 1 without a texture read, and every multiply by it is the older picture.
// Off the map's rectangle it is 1 too: that water is not this map's to judge. Where all four cells are
// open it is exactly 1.
//
// Globals, declared OUTSIDE any per-material CBUFFER (the SRP batcher's layout rule).

Texture2D<float2> _HHSeaReachTex;
float4 _HHSeaReachRect;
float4 _HHSeaReachParams;
float4 _HHSeaReachWet;

// The swell factor of one sill: 0 at or below it, 1 one blend above it, smoothstep between.
float SeaReachFactorOfSill(float sill, float level)
{
    if (sill < -59000.0) return 1.0;
    float u = saturate((level - sill) / max(_HHSeaReachParams.x, 1e-4));
    return u * u * (3.0 - 2.0 * u);
}

// A fraction within 1e-4 of a cell centre IS the centre (twin: SeaReachField.SnapFraction).
float SeaReachSnap(float f, inout int cell)
{
    if (f > 1.0 - 1e-4) { cell += 1; return 0.0; }
    return f < 1e-4 ? 0.0 : f;
}

// One cell's vote: its factor, weighted by its bilinear share and by how wet it stands.
void SeaReachVote(float2 sf, float weight, float level, inout float num, inout float den)
{
    float w = weight * saturate((level - sf.y) / max(_HHSeaReachWet.x, 1e-4));
    num += w * SeaReachFactorOfSill(sf.x, level);
    den += w;
}

// The swell factor (0..1) at a world position for a water level.
float SeaReach01(float2 worldXY, float level)
{
    if (_HHSeaReachParams.y < 0.5) return 1.0;
    float2 uv = (worldXY - _HHSeaReachRect.xy) / max(_HHSeaReachRect.zw, float2(1e-3, 1e-3));
    if (any(uv < 0.0) || any(uv >= 1.0)) return 1.0;
    int2 size = int2(_HHSeaReachParams.zw + 0.5);
    int2 own = min(int2(uv * _HHSeaReachParams.zw), size - int2(1, 1));

    float2 p = uv * _HHSeaReachParams.zw - 0.5;
    int x0 = (int)floor(p.x), y0 = (int)floor(p.y);
    float fx = SeaReachSnap(p.x - floor(p.x), x0);
    float fy = SeaReachSnap(p.y - floor(p.y), y0);
    int xa = clamp(x0, 0, size.x - 1), xb = clamp(x0 + 1, 0, size.x - 1);
    int ya = clamp(y0, 0, size.y - 1), yb = clamp(y0 + 1, 0, size.y - 1);

    float2 s00 = _HHSeaReachTex.Load(int3(xa, ya, 0));
    float2 s10 = _HHSeaReachTex.Load(int3(xb, ya, 0));
    float2 s01 = _HHSeaReachTex.Load(int3(xa, yb, 0));
    float2 s11 = _HHSeaReachTex.Load(int3(xb, yb, 0));
    if (max(max(s00.x, s10.x), max(s01.x, s11.x)) < -59000.0) return 1.0;   // all open: the older picture

    float num = 0.0, den = 0.0;
    SeaReachVote(s00, (1.0 - fx) * (1.0 - fy), level, num, den);
    SeaReachVote(s10, fx * (1.0 - fy), level, num, den);
    SeaReachVote(s01, (1.0 - fx) * fy, level, num, den);
    SeaReachVote(s11, fx * fy, level, num, den);
    if (den <= 1e-6) return SeaReachFactorOfSill(_HHSeaReachTex.Load(int3(own, 0)).x, level);
    return num / den;
}

#endif // HIDDEN_HARBOURS_SEA_REACH_INCLUDED