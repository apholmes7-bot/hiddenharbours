#ifndef HIDDEN_HARBOURS_ROAD_FLOOR4_INCLUDED
#define HIDDEN_HARBOURS_ROAD_FLOOR4_INCLUDED

// RoadFloor4.hlsl — a region's road floor (road / path kit v4, NMC H4), relit per texel from the chunks
// RoadFloorStreamer keeps resident, under the ground's own sky (TerrainSky). Needs Include/TerrainLight6.hlsl
// first: the sky, the colour, the hashes and the rig's tables are its, unchanged.
//
// THE BAKE (tools/nmc-roads/bake-chunks.cjs) runs the kit as its own scenes do, over windows of 8 m chunks,
// and writes each chunk's G-buffer in SCREEN space: the floor texel each pixel shows after the kit's faces'
// projection (a kerb top shows 3 px north of its plan texel, a boardwalk 9 px), or the face itself. 256 px a
// side, 32 px = 1 m, rows from the north. The streamer loads three of its maps per chunk into a slot of three
// arrays, writes the slot's row of the road ramp, and the chunk's slot into the index:
//   _RoadNormal  RGB the normal * 0.5 + 0.5, x east, y NORTH, z up      A the pond over the chunk's largest
//   _RoadLight   R sky visibility (G.ao)      B the height over the chunk's range
//                G the ROLE: 255 a route's floor, 170 a face, 85 a face under a deck, 0 not the road (a texel
//                its neighbours' relight reads, never drawn)          A the chunk's palette * 8 + band
//   _RoadDetail  RG the mark id, low byte first (16 bits, as the kit's tile holds it)   B the class * 28
//                A bit d: the texel is its mark's tip toward direction d (TL6Sky.tipDir) and stands proud
//   _RoadRamp    row = slot. x 0..159: palette p's band b at x = p * 5 + b, sRGB bytes 0..255 (32 palettes: the
//                owner's ruling Q1 a widened the ramp for road chunks, St Peters' 16 stay as they are).
//                x 160: (heightMin, heightRange, pondMax, heightMaxNear), the last the largest height within
//                the sun march's reach of the chunk.
//   _RoadIndex   texel (cx, cy) = the slot of chunk (cx, cy) + 1, 0 while it is not resident
//   _RoadOrigin  xy the grid's north-west corner in world metres, z px per metre;  _RoadGrid xy the grid in chunks
// Everything is read with LOAD. A chunk's texel (x, y) has y running DOWN the chunk, as the PNG stores it; the
// arrays store rows bottom up, so row = 255 - y (TL6Texel's flip). Every slot a load takes is clamped to 0
// first, so a read the branch should have skipped is in bounds on every graphics API.
//
// THE RELIGHT is TL6Relight, TL6SunVis and TL6IsPud, statement for statement, with their reads swapped for the
// road's (RoadFloor4SourceTests holds each pair equal save the swaps it names):
//   * a texel is on the region's ONE plan grid G (x east, y south of _RoadOrigin), not a tile: a neighbour's
//     read crosses into its own chunk's slot, and an absent chunk reads as nothing there (no blocker, no pond,
//     no seam). The bake keeps every chunk a covered pixel's relight reads (its REACH, 36 px).
//   * each chunk has its own height range, and the march stops at heightMaxNear, the early out the kit's
//     window gives (its hmax), never later. (A neighbour's height, decoded from its own byte, can stand over
//     it by half a byte of that chunk's range, 0.05 px at the most.)
//   * X and Y (the noise and the hashes) are G, the region's grid, where the kit's were its bake window's:
//     the same water, glints and wet sheen, phased once for the region instead of once per window.
//   * a face (the roles 170 and 85) is relightFaces' texel: no sun march (the face buffer's hmax is 0), and no
//     pond or mark (the bake writes it none); under a deck occ 1 and skyv 0.78, the kit's shade level 3.
// WHAT IS LEFT OUT is TerrainLight6's (snow, the tide, the canopy levels), for its reasons. The projection's own
// cost, and the 8-bit normal's, were measured against the kit (H4's read-model): 0 to 0.15% of covered pixels
// off by the projection in clear skies, 2.6% at puddle edges in rain; the 8-bit normal up to 9% (dusk, rain).

#define RF4_CHUNK       256            // a chunk's texels a side (RoadFloorMaps.ChunkTexels)
#define RF4_PALETTES    32             // a chunk's palettes at most (RoadFloorMaps.MaxPalettes)
#define RF4_RAMP_PARAMS 160            // the ramp's parameter column, RF4_PALETTES * 5 (RoadFloorMaps.RampWidth - 1)
#define RF4_ROLE_NONE   0              // the roles, as the bake writes them in _RoadLight's G (RoadFloorMaps.Role*)
#define RF4_ROLE_AIR    85
#define RF4_ROLE_FACE   170
#define RF4_ROLE_FLOOR  255
#define RF4_AIR_SKYV    0.78           // the kit's shade level 1 and over: (lvl >= 1 ? 0.78 : 1) on the sky term
#define RF4_NOTHING     -1e9           // the height of an absent chunk: no step of the march reaches it

TEXTURE2D_ARRAY(_RoadNormal);
TEXTURE2D_ARRAY(_RoadLight);
TEXTURE2D_ARRAY(_RoadDetail);
TEXTURE2D(_RoadRamp);
TEXTURE2D(_RoadIndex);

// ---- the grid and the slots ---------------------------------------------------------------------------------

// The plan grid's width in texels, for the hashes' texel index.
int RF4Width() { return (int)_RoadGrid.x * RF4_CHUNK; }

// The plan texel under world position wp: x east, y SOUTH of the grid's north-west corner.
int2 RF4Pixel(float2 wp)
{
    return int2((int)floor((wp.x - _RoadOrigin.x) * _RoadOrigin.z), (int)floor((_RoadOrigin.y - wp.y) * _RoadOrigin.z));
}

// The slot holding G's chunk, or -1: off the grid, or not resident.
int RF4Slot(int2 G)
{
    int2 n = int2((int)_RoadGrid.x, (int)_RoadGrid.y);
    int2 c = int2(G.x >> 8, G.y >> 8);   // floor(G / RF4_CHUNK), RF4_CHUNK being 2^8, negative G included
    bool on = c.x >= 0 && c.y >= 0 && c.x < n.x && c.y < n.y;
    int s = (int)LOAD_TEXTURE2D(_RoadIndex, clamp(c, int2(0, 0), max(n - 1, int2(0, 0)))).r - 1;
    return on ? s : -1;
}

// G's texel in its chunk's slice: x east, and the row flipped as the arrays store it.
int2 RF4Texel(int2 G) { return int2(G.x & (RF4_CHUNK - 1), (RF4_CHUNK - 1) - (G.y & (RF4_CHUNK - 1))); }

float4 RF4LoadNormal(int s, int2 G) { return TL6Bytes(LOAD_TEXTURE2D_ARRAY(_RoadNormal, RF4Texel(G), max(s, 0))); }
float4 RF4LoadLight(int s, int2 G)  { return TL6Bytes(LOAD_TEXTURE2D_ARRAY(_RoadLight, RF4Texel(G), max(s, 0))); }
float4 RF4LoadDetail(int s, int2 G) { return TL6Bytes(LOAD_TEXTURE2D_ARRAY(_RoadDetail, RF4Texel(G), max(s, 0))); }

// The slot's chunk's (heightMin, heightRange, pondMax, heightMaxNear).
float4 RF4Params(int s) { return LOAD_TEXTURE2D(_RoadRamp, int2(RF4_RAMP_PARAMS, max(s, 0))); }

// Palette p's band b of the slot's chunk, sRGB bytes.
float3 RF4Pal(int s, int p, int b) { return LOAD_TEXTURE2D(_RoadRamp, int2(p * 5 + b, max(s, 0))).rgb; }

// G's height in its own (resident) chunk s.
float RF4HeightIn(int s, int2 G, float4 prm) { return prm.x + RF4LoadLight(s, G).b / 255.0 * prm.y; }

// G's height wherever it lies; nothing there when its chunk is absent.
float RF4Height(int2 G)
{
    int s = RF4Slot(G);
    float h = RF4HeightIn(s, G, RF4Params(s));
    return s < 0 ? RF4_NOTHING : h;
}

// G's pond depth wherever it lies; none when its chunk is absent.
float RF4Pond(int2 G)
{
    int s = RF4Slot(G);
    float p = RF4LoadNormal(s, G).a / 255.0 * RF4Params(s).z;
    return s < 0 ? 0.0 : p;
}

// G's mark wherever it lies; an absent chunk reads as the mark asked about (same), so it is never a seam.
int RF4Mark(int2 G, int same)
{
    int s = RF4Slot(G);
    float4 d = RF4LoadDetail(s, G);
    return s < 0 ? same : (int)d.r + (int)d.g * 256;
}

// ---- the relight: TerrainLight6's, on the road's reads ------------------------------------------------------

// sunVis: 0 when the floor's own relief blocks the sun at G, else 1.
float RF4SunVis(int s, int2 G, float3 l, float4 prm)
{
    float lh = length(l.xy);
    if (l.z <= 0.02) return 0.0;
    if (lh < 1e-3) return 1.0;
    float ux = l.x / lh, uy = l.y / lh, tn = l.z / lh;
    float h0 = RF4HeightIn(s, G, prm) + 0.25, room = prm.w - h0;   // hmax: the largest height within the march's reach
    [loop] for (int n = 0; n < 11; n++)
    {
        float st = TL6_SSTEP[n];
        if (tn * st > room) break;
        int2 q = int2((int)TL6Round(G.x + ux * st), (int)TL6Round(G.y + uy * st));
        if (RF4Height(q) > h0 + tn * st) return 0.0;
    }
    return 1.0;
}

// isPud: a hollow broad enough to hold water, by class.
bool RF4IsPud(int2 G, int c, float wetT)
{
    float cp = TL6_PUDDLE[c];
    if (cp == 0.0) return false;
    float h = RF4Pond(G), fill = (wetT - 0.25) / 0.75;
    if (h * cp <= (1.0 - fill) * 1.1 + 0.18) return false;
    float lo = h * 0.55;
    return RF4Pond(G + int2(1, 0)) > lo && RF4Pond(G + int2(-1, 0)) > lo
        && RF4Pond(G + int2(0, 1)) > lo && RF4Pond(G + int2(0, -1)) > lo;
}

// One texel of the road floor, relit: G on the plan grid, s its chunk's slot, role its role (never none).
// Returns the rig's bytes, sRGB 0..255.
float3 RF4Relight(int s, int2 G, TL6Sky k, int role)
{
    float4 prm = RF4Params(s);
    float occ = role == RF4_ROLE_AIR ? 1.0 : 0.0, skyv = role == RF4_ROLE_AIR ? RF4_AIR_SKYV : 1.0;
    float4 nm = RF4LoadNormal(s, G), li = RF4LoadLight(s, G), de = RF4LoadDetail(s, G);
    float3 n = float3(nm.r / 255.0 * 2.0 - 1.0, -(nm.g / 255.0 * 2.0 - 1.0), nm.b / 255.0 * 2.0 - 1.0);
    n /= length(n);
    float ao = li.r / 255.0;
    int palIx = (int)li.a >> 3, band0 = (int)li.a & 7;
    int mark = (int)de.r + (int)de.g * 256, c = min((int)TL6Round(de.b / 28.0), 8), bits = (int)de.a;
    int i = G.y * RF4Width() + G.x;
    float X = G.x, Y = G.y;
    int fq = k.fog > 0.01 ? (int)TL6Round(saturate(k.fog * 0.85) * 4.0) : 0;   // far = 0: no scene

    // light (A, B, D)
    float nl = dot(n, k.l);
    float vis = 0.0;                   // an if, not ?: (HLSL evaluates both sides of ?:, and the march is long)
    [branch] if (k.sunOn && nl > 0.0 && occ < 0.5) { vis = 1.0; [branch] if (role == RF4_ROLE_FLOOR) vis = RF4SunVis(s, G, k.l, prm); }
    float sun = k.sunI * (nl > 0.0 ? pow(abs(nl), 1.25) : 0.0) * vis;
    float e0 = (0.36 * k.skyI * (0.30 + 0.70 * ao) * skyv * (0.45 + 0.55 * n.z) + sun) * k.expo;
    int db = k.grade ? TL6BandOff(e0) : (ao < 0.7 ? -1 : 0);
    int lit = sun > 0.22 ? 1 : 0;

    // water: the floor's own, and puddles (G)
    float wetT = k.wet;
    bool isW = c == TL6_CL_WATER;
    float depth = 0.2;
    if (!isW && wetT > 0.25 && nm.a / 255.0 * prm.z > 0.18)
    {
        if (RF4IsPud(G, c, wetT)) { isW = true; depth = 0.05; }
    }

    int bi, spc = 0, wk = 0;
    float3 rgb;
    [branch] if (isW)
    {
        // waves: a swell running to the shore on the loop, a chop on top, both with the wind
        float fetch = 1.0;
        float amp = (0.16 + 0.84 * k.wind) * clamp(depth / 0.8, 0.12, 1.0) * (0.2 + 0.8 * fetch), ph = TL6_TAU * k.fi / TL6_LOOP;
        float q1 = Y * 0.62 + 1.6 * sin(X * 0.043 + Y * 0.017) + 3.1 * TL6Vn(X / 58.0, Y / 21.0, 13) - ph * k.seaDir;
        float q2 = X * 0.37 + Y * 0.81 + 2.2 * TL6Vn(X / 17.0, Y / 9.0, 31) + 2.0 * ph;
        float cw = sin(q1), dcy = cos(q1) * 0.62, dcx = cos(q1) * 0.07 + 0.35 * k.wind * cos(q2) * 0.37;
        float3 wv = float3(-amp * dcx, -amp * (dcy + 0.35 * k.wind * cos(q2) * 0.81), 1.0);
        wv /= length(wv);
        float nv = dot(wv, TL6_VIEW);
        float3 rv = 2.0 * nv * wv - TL6_VIEW;
        // crests broken into dashes by a mask that circles through noise once per loop
        float mk = TL6Vn(X / 7.0 + 2.5 * cos(ph), Y / 2.5 + 2.5 * sin(ph), 47), ak = clamp(amp / 0.45, 0.15, 1.4);
        float crest = (cw + 0.45 * k.wind * sin(q2)) * ak;
        bi = crest > 0.8 && mk > 0.5 ? 3 : (crest < -0.85 && mk < 0.42 ? 1 : 2);
        bi = clamp(bi + (db <= -2 ? -1 : 0), 0, 4);
        int ramp = depth < 0.6 ? 1 : 0;                  // 1 the shoal ramp, 0 the deep; 2 the foam
        if (k.sunOn)
        {
            float sp = dot(rv, k.l);
            if (k.sunI > 0.08 && sp > 0.955 - 0.05 * k.wind && TL6Hash2(G.x, G.y, 11 + k.fi) < 0.55) { bi = 4; spc = 2; }
            else if (k.sunI > 0.08 && sp > 0.86 && crest > 0.5 && mk > 0.5 && TL6Hash2(G.x, G.y, 17 + k.fi) < 0.35) bi = 4;
        }
        if (k.wind > 0.55 && fetch > 0.5 && crest > 0.95 && mk > 0.62 && TL6Hash2(G.x >> 1, G.y, 23 + k.fi) < (k.wind - 0.55) * 1.1)
        {
            ramp = 2; bi = 3; spc = 0;
        }
        if (k.rain > 0.04)
        {
            // rain rings, one drop per 7x7 cell per loop
            int cx = (int)floor(X / 7.0), cy = (int)floor(Y / 7.0);
            float h = TL6Hash2(cx, cy, 41);
            if (h < k.rain * 0.9)
            {
                float ox = cx * 7 + 1 + TL6Hash2(cx, cy, 42) * 5.0, oy = cy * 7 + 1 + TL6Hash2(cx, cy, 43) * 5.0;
                int age = (k.fi + (int)floor(TL6Hash2(cx, cy, 44) * TL6_LOOP)) & (TL6_LOOP - 1);   // both terms >= 0
                float r = age * 0.45;
                if (r < 3.2 && abs(length(float2((X - ox) * 0.8, Y - oy)) - r) < 0.55) { bi = min(4, bi + 1); spc = 0; }
            }
        }
        float3 rb, r4;
        if (ramp == 2) { rb = TL6_FOAM[bi]; r4 = TL6_FOAM[4]; }
        else
        {
            float3 body = ramp == 1 ? TL6_SHOAL : TL6_DEEP;
            rb = TL6WaterRamp(body, bi, k); r4 = TL6WaterRamp(body, 4, k);
        }
        rgb = TL6Colour(rb, r4, bi, lit, fq, spc, 0, k);
        if (depth < 0.16)
        {
            // the shallows: the bottom through three steps of water (a puddle, here)
            int b0 = clamp(band0 + db, 0, 4);
            float3 gnd = TL6Colour(RF4Pal(s, palIx, b0), RF4Pal(s, palIx, 4), b0, lit, fq, 0, 2, k);
            float f = depth < 0.04 ? 0.3 : (depth < 0.09 ? 0.5 : 0.7);
            rgb = floor(gnd + (rgb - gnd) * f + 0.5);
        }
    }
    else
    {
        bi = band0 + db;
        // live tip and seam (C)
        if (mark != 0)
        {
            bool seam = false;
            float hi = RF4HeightIn(s, G, prm);
            if (k.ax != 0)
            {
                int2 j = G + int2(k.ax, 0);
                if (RF4Mark(j, mark) != mark && hi - RF4Height(j) > 0.2) seam = true;
            }
            if (!seam && k.ay != 0)
            {
                int2 j = G + int2(0, k.ay);
                if (RF4Mark(j, mark) != mark && hi - RF4Height(j) > 0.2) seam = true;
            }
            if (seam && bi >= 1) bi -= 1;
            else if (lit != 0 && ((bits >> k.tipDir) & 1) != 0) bi += 1;
        }
        // wet (E)
        float por = TL6_POR[c], gloss = TL6_GLOSS[c], wp = wetT * por;
        wk = wp > 0.21 ? 2 : (wp > 0.06 ? 1 : 0);
        if (wetT > 0.3 && gloss > 0.3)
        {
            if (lit != 0 && bi >= 3 && TL6Hash2(mark != 0 ? mark : i, 3, 17) < wetT * gloss * 0.8) spc = 2;
            else if (n.z > 0.9 && TL6Vn(X / 7.0, Y / 5.0, 19) > 1.0 - wetT * gloss * 0.42) spc = 3;
        }
        bi = clamp(bi, 0, 4);
        rgb = TL6Colour(RF4Pal(s, palIx, bi), RF4Pal(s, palIx, 4), bi, lit, fq, spc, wk, k);
    }
    return rgb;
}

// ---- the overlay --------------------------------------------------------------------------------------------

// The road over the ground's colour at world position wp: the ground where no road covers the pixel or its
// chunk is not resident (past the pool's slots, at a wide framing, the ground shows: RoadFloorSlots' overflow),
// else the kit's bytes relit and linearised to land where the ground's relit materials do (RelightMat).
float3 RF4Over(float3 ground, float2 wp, TL6Sky sky)
{
    int2 G = RF4Pixel(wp);
    int s = RF4Slot(G);
    int role = (int)RF4LoadLight(s, G).g;
    [branch] if (s < 0 || role == RF4_ROLE_NONE) return ground;
    return TL6SrgbToLinear(RF4Relight(s, G, sky, role));
}

#endif // HIDDEN_HARBOURS_ROAD_FLOOR4_INCLUDED
