// TidalFaceDress.hlsl — the material a wharf face shows above the water.
//
// The owner, 2026-11-25, at Nine Mile Creek: the wall above water is "very dark and plain". The H5 rig
// bakes its seaward faces nearly black (the camera looks down on them, so the bake gives them little
// light), and the sea cutting them at the waterline (TidalFaceWaterline) left a tall dark slab at low
// water. This lights the face's own picture and gives it the material its wharf type says: log courses
// on a timber crib, pour lifts and form panels on a concrete quay, the stone itself on a breakwater.
//
// THE WALL HAS TWO CLOCKS, and they are kept apart on purpose:
//   - FIXED ON THE WALL, by elevation above the datum: the barnacle line and the rockweed. They live where
//     the tide has put them for years, so a falling tide uncovers them and a rising one drowns them; they
//     never move with the water.
//   - FOLLOWING THE WATER, by height above the live waterline: the wet band and the glint at the edge,
//     recomputed every frame from the one published sea level (_HHSeaLevelWorld, never a second read).
// Both are pure functions of (world pixel, sea level): deterministic, saved nowhere (rule 5).
//
// A ROCK face (a breakwater) is not cut on the straight row at all: its waterline follows the stone.
// The light tops of the rocks stand proud of the water and the dark joints between them flood first,
// read off the picture's own luminance (smoothed over a few pixels so the cut follows rock forms rather
// than single texels), plus a little per-column jitter; a short rim of the game's foam colour sits on it.
//
// Every number is a material property on TidalFace.mat (rule 6). Only a face whose _HHFaceDress.w is 1
// reaches this file; every other face on the material draws exactly as before.
//
// Colour maths is LINEAR (the project's colour space), so the gains are stated perceptually and raised to
// 2.2 here, and the luminance used for the rock relief is the perceptual sqrt of linear luminance.

#ifndef HH_TIDAL_FACE_DRESS_INCLUDED
#define HH_TIDAL_FACE_DRESS_INCLUDED

float HHDressHash(float2 p)
{
    float3 p3 = frac(float3(p.xyx) * 0.1031);
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.x + p3.y) * p3.z);
}

// Smooth 1-D value noise in [0, 1]: hashed knots at whole x, smoothstep between them.
float HHDressNoise(float x, float seed)
{
    float i = floor(x);
    float f = x - i;
    f = f * f * (3.0 - 2.0 * f);
    return lerp(HHDressHash(float2(i, seed)), HHDressHash(float2(i + 1.0, seed)), f);
}

half HHDressLuma(half3 c)
{
    return sqrt(max(dot(c, half3(0.2126h, 0.7152h, 0.0722h)), 0.0h));
}

// c         the face's own drawn colour (linear)
// uv        the sprite uv, for the rock relief
// uvStep    fwidth(uv), taken by the caller in uniform flow
// w         the fragment's world xy
// sea       the sea level over this pixel, metres above the datum
// wl        the straight waterline row (world y) the face would be cut on
// lipY      the world y of the face's deck lip
// lipElev   that lip's elevation, metres above the datum
// hs        screen units per metre of height (IsoGround.HeightScale, pushed)
// kind      1 timber crib, 2 concrete quay, 3 rock breakwater
half3 HHDressFace(half3 c, float2 uv, float2 uvStep, float2 w, float sea, float wl, float lipY, float lipElev, float hs, float kind)
{
    float ppu = max(_DressPixelsPerUnit, 1.0);
    float h = max(hs, 1e-4);
    float2 px = floor(w * ppu);
    float rowY = (px.y + 0.5) / ppu;
    float rowM = 1.0 / (ppu * h);                 // metres of face per pixel row
    float elev = lipElev + (rowY - lipY) / h;     // the datum elevation this row stands at
    bool rock = kind > 2.5;
    bool timber = kind < 1.5;
    float waterAt = wl;

    // WHERE THE FACE BEGINS. A wall's picture draws its face from under the deck's cap timber down,
    // and the cap stands _DressUnderLip.z screen metres above the walk's seaward lip (the fender band
    // hangs in between); above that is the cap and the deck, never dressed. A breakwater's picture
    // starts its face at the lip itself.
    float faceTopY = lipY + (rock ? 0.0 : max(_DressUnderLip.z, 0.0));
    if (rowY > faceTopY) return c;
    float below = (faceTopY - rowY) / h;          // metres of face down from where the face begins

    half3 col;
    if (rock)
    {
        // THE WATERLINE FOLLOWS THE STONE.
        // Explicit-LOD reads with a step taken in uniform flow by the caller: no gradients in a branch.
        float2 du = max(uvStep * 2.0, 1e-6);
        half ls = (2.0h * HHDressLuma(SAMPLE_TEXTURE2D_LOD(_MainTex, sampler_MainTex, uv, 0).rgb)
                   + HHDressLuma(SAMPLE_TEXTURE2D_LOD(_MainTex, sampler_MainTex, uv + float2(du.x, 0.0), 0).rgb)
                   + HHDressLuma(SAMPLE_TEXTURE2D_LOD(_MainTex, sampler_MainTex, uv - float2(du.x, 0.0), 0).rgb)
                   + HHDressLuma(SAMPLE_TEXTURE2D_LOD(_MainTex, sampler_MainTex, uv + float2(0.0, du.y), 0).rgb)
                   + HHDressLuma(SAMPLE_TEXTURE2D_LOD(_MainTex, sampler_MainTex, uv - float2(0.0, du.y), 0).rgb)) / 6.0h;
        // The stones' own lie: a smooth noise along the arm (rock-sized and half-rock-sized cells),
        // since the base of the picture is one dark tone and its luminance alone cuts nearly straight.
        float lie = (HHDressNoise(px.x / 11.0, 71.0) - 0.5) + 0.5 * (HHDressNoise(px.x / 4.0, 83.0) - 0.5);
        float relief = (ls - _DressRock.z) * _DressRock.x
                     + (HHDressHash(float2(floor(px.x / 3.0), 41.0)) - 0.5) * _DressRock.y
                     + lie * _DressRock.w;
        waterAt = wl - relief * h;
        // The weed and the barnacles lie on the stone, so they follow its relief like the water does.
        elev += relief;
        clip(w.y - waterAt);
        col = c * pow(max(_DressRockGain, 0.0), 2.2);
    }
    else
    {
        col = c * pow(max(_DressGain, 0.0), 2.2);
    }

    // The material's hue, luminance kept: a tyre stays black, the wall around it turns timber or concrete.
    half3 tint = timber ? _DressTimberTint.rgb : (rock ? _DressRockTint.rgb : _DressConcreteTint.rgb);
    half tintLuma = max(dot(tint, half3(0.2126h, 0.7152h, 0.0722h)), 1e-3h);
    col *= lerp(half3(1.0h, 1.0h, 1.0h), tint / tintLuma, (half)_DressTintAmount);

    if (timber)
    {
        // LOG COURSES: a lit top row, a dark joint under it, butt ends staggered course to course.
        float courseM = max(_DressCourse.x, rowM * 3.0);
        float course = floor(below / courseM);
        float inPx = (below - course * courseM) / rowM;
        float coursePx = courseM / rowM;
        half shade = 1.0h;
        if (inPx < 1.0) shade += (half)_DressCourse.z;
        else if (inPx >= coursePx - 1.0) shade -= (half)(_DressCourse.z * 1.6);
        float logPx = max(_DressCourse.y * ppu, 4.0);
        float shifted = px.x + HHDressHash(float2(course, 3.0)) * logPx;
        float logId = floor(shifted / logPx);
        if (shifted - logId * logPx < 1.0) shade -= (half)(_DressCourse.z * 1.6);
        shade *= 1.0h + (half)((HHDressHash(float2(logId, course)) - 0.5) * 2.0 * _DressCourse.w);
        shade *= 1.0h + (half)((HHDressHash(px) - 0.5) * _DressCourse.w);
        col *= shade;
    }
    else if (!rock)
    {
        // CONCRETE: a line at each pour lift, the form-panel joints, a mottle in three-pixel cells.
        float pourM = max(_DressPour.x, rowM * 3.0);
        float lift = floor(below / pourM);
        float inPx = (below - lift * pourM) / rowM;
        half shade = 1.0h;
        if (inPx < 1.0) shade -= (half)_DressPour.z;
        float panelPx = max(_DressPour.y * ppu, 4.0);
        if (px.x - floor(px.x / panelPx) * panelPx < 1.0) shade -= (half)_DressPour.z;
        shade *= 1.0h + (half)((HHDressHash(floor(px / 3.0) + 7.0) - 0.5) * 2.0 * _DressPour.w);
        shade *= 1.0h + (half)((HHDressHash(px) - 0.5) * _DressPour.w);
        col *= shade;
    }

    if (!rock)
    {
        // THE DECK'S OVERHANG shades the top of the face.
        col *= lerp((half)_DressUnderLip.y, 1.0h, (half)saturate(below / max(_DressUnderLip.x, 1e-3)));

        // FENDER SCUFFS in the band the hulls rub: short pale smears in hashed columns.
        if (below > _DressScuff.x && below < _DressScuff.y &&
            HHDressHash(float2(floor(px.x / 5.0), 23.0)) < _DressScuff.z)
            col = lerp(col, _DressScuffColor.rgb, (half)(0.4 * HHDressHash(float2(floor(px.x / 2.0), px.y))));
    }

    // ROCKWEED, fixed on the wall: olive-brown cover with a ragged top edge.
    float weedTop = _DressWeed.y + (HHDressHash(float2(floor(px.x / 2.0), 9.0)) - 0.5) * _DressWeed.w;
    if (elev < weedTop && elev > _DressWeed.x)
    {
        // Patchy, not a painted stripe: clumps in 6 x 4 pixel cells, strands within them.
        half clump = (half)(0.35 + 0.65 * HHDressHash(floor(px / float2(6.0, 4.0)) + 13.0));
        half cover = (half)(_DressWeed.z * clump * (0.6 + 0.4 * HHDressHash(floor(px / float2(1.0, 2.0)))));
        half3 weed = _DressWeedColor.rgb * (0.75h + 0.5h * (half)HHDressHash(px + 5.0));
        col = lerp(col, weed, cover);
    }

    // THE BARNACLE LINE, fixed on the wall: pale specks, thinning toward the band's edges.
    if (elev > _DressBarnacle.x && elev < _DressBarnacle.y)
    {
        float edge = saturate(min(elev - _DressBarnacle.x, _DressBarnacle.y - elev) / 0.3);
        float density = _DressBarnacle.z * edge * (0.5 + HHDressHash(floor(px / 2.0) + 31.0));
        if (HHDressHash(px + 17.0) < density)
            col = _DressBarnacleColor.rgb * (0.8h + 0.4h * (half)HHDressHash(px + 2.0));
    }

    // THE WET BAND follows the water: darker just above the live waterline, ragged at its top.
    float above = (rowY - waterAt) / h;
    if (above >= 0.0 && above < _DressWet.x)
    {
        float ragged = (HHDressHash(float2(floor(px.x / 3.0), 51.0)) - 0.5) * 0.3 * _DressWet.x;
        float t = saturate((above + ragged) / max(_DressWet.x, 1e-3));
        col *= lerp((half)_DressWet.y, 1.0h, (half)(t * t));
    }

    // AT THE EDGE: a glint line on a wall, the game's foam on the rocks.
    if (rock)
    {
        if (above >= 0.0 && above < _DressFoam.x * rowM)
            col = lerp(col, _DressFoamColor.rgb, (half)(_DressFoam.y * (0.6 + 0.4 * HHDressHash(px + 61.0))));
    }
    else if (above >= 0.0 && above < _DressWet.z * rowM)
    {
        col = lerp(col, _DressGlintColor.rgb, 0.45h);
    }
    return col;
}

#endif
