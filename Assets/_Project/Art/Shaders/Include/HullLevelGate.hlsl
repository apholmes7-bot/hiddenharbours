#ifndef HH_HULL_LEVEL_GATE_INCLUDED
#define HH_HULL_LEVEL_GATE_INCLUDED
bool HHDiscardHullLevel(float4 lvl, float shown, uint liftLevels, uint shownRoles, float4 roleMasks[31])
{
    bool isInterior = lvl.y > 0.5;
    bool isShown = abs(lvl.x - shown) < 0.5;
    if (isInterior) return shown < 0.5 || !isShown || lvl.z < 0.5;
    uint levelBit = 1u << (uint)max(0.0, lvl.x);
    bool lifted = (liftLevels & levelBit) != 0;
    bool role = lvl.w < 0.5 || (shownRoles & (1u << (uint)(lvl.w - 1.0))) != 0;
    float2 halves = roleMasks[(uint)clamp(lvl.x, 0.0, 30.0)].xy;
    uint extra = (uint)halves.x | ((uint)halves.y << 16);
    bool roleLift = extra != 0 && (lvl.w < 0.5 || (extra & (1u << (uint)max(0.0, lvl.w - 1.0))) != 0);
    return lifted || roleLift || (shown >= 0.5 && isShown && role);
}
#endif
