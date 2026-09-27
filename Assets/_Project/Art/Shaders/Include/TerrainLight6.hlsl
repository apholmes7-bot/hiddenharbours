#ifndef HIDDEN_HARBOURS_TERRAIN_LIGHT6_INCLUDED
#define HIDDEN_HARBOURS_TERRAIN_LIGHT6_INCLUDED

// TerrainLight6.hlsl — the terrain pass 9 kit's floor light, relit from the kit's baked maps.
//
// terrainLight6.js's relight (docs/art/rigs/terrain/pass9, the cliff & rock kit v6), statement for
// statement, for ONE texel of ONE kit tile. The splat shader calls it per painted material and per
// ladder step (HiddenHarboursTerrainSplat.shader, RelightMat). Its C# twin is
// HiddenHarbours.Art.TerrainLight6, which TerrainLight6TwinTests hold to the rig byte for byte on the
// kit's own G-buffers; this file is that twin in float32, reading the G-buffer back from the maps.
//
// WHAT THE MAPS CARRY (bakePass9.js writes them; TerrainTexArrayBuilder packs one slice per kit tile,
// in the albedo array's slice order):
//   _RelightNormal  RGB  the normal * 0.5 + 0.5, x east, y NORTH (the contract's y-up), z up
//                   A    the pond depth over the tile's largest (pondMax)
//   _RelightLight   R    sky visibility (G.ao)      G  porosity (unread: the class carries it)
//                   B    height over the tile's range (heightMin, heightRange)
//                   A    palette * 8 + authored band
//   _RelightDetail  RG   the mark id, low byte first      B  the surface class * 28
//                   A    bit d set: this texel is its mark's tip toward direction d (0 east, then
//                        clockwise in the rig's floor, 45 degrees apart) and stands proud of it
//   _RelightRamp    row = slice. x 0..79: palette p's band b at x = p * 5 + b, as sRGB bytes 0..255.
//                   x 80: (heightMin, heightRange, pondMax, 1); a slice with no maps is all zero there.
// Everything is read with LOAD (no filtering, mip 0). The rig's texel (x, y) has y running DOWN the
// tile, as the PNG stores it; the array stores rows bottom up (GetPixels32), so row = 255 - y.
//
// WHAT IS LEFT OUT, AND WHY:
//   * snow (the pass 9 plan's decision 4: ground snow comes with M2's winter wave);
//   * the tide block: the sea below the tide, its swash, sheen and shoreline foam. The sea is the water
//     plane's (ADR 0012) and so is the foam (decision 5); the shore's wetness is the splat shader's own
//     wet band on _WaterLevel;
//   * the canopy levels (o.lv): nothing in the engine feeds them yet, so they read 0 as in a lone tile.
// A lone tile has no scene fields, so here as there: no elevation, slope or distance, fetch 1, and
// the tile's own water is 0.2 deep.
//
// THE THREE INPUTS TerrainLight6 ADDED to TerrainLight5, each defaulting to "unset", as the rig reads
// an absent one: occ (a texel shaded from the sun by something above it) 0; skyv (the share of the
// sky it sees) 1; seaDir (which way the swell runs) 1. With all three unset the light is
// TerrainLight5's, pixel for pixel (the pass 9 README's proof).
//
// THE RIG'S NUMBERS ARE ITS LOOK. They are ported, not tuned: a change belongs in the rig, a re-bake
// and the twin's fixture, never here alone.

#define TL6_LOOP     16
#define TL6_TAU      6.28318530717959
#define TL6_CL_WATER 5
#define TL6_RAMP_PARAMS 80
#define TL6_TILE     256            // a kit tile's texels a side: the maps are the albedo's size

// The sky, and what the relight derives from it once per fragment (TL6MakeSky).
struct TL6Sky
{
    float3 l;                      // sunF: toward the sun, floor space (x east, y SOUTH, z up)
    float  sunI, skyI, expo;
    float  wet, rain, fog, wind;
    bool   grade;
    float3 kc, ac, wc, fc, sc;     // key, ambient, wash, fog and sky colours, sRGB bytes
    float  aa, amb, ka, wa;        // the grade's four weights
    float  seaDir;
    int    fi;                     // the frame, 0..15 on the loop
    // derived
    bool   sunOn;
    int    ax, ay;                 // the neighbour a seam is read against, from the floor sun
    int    tipDir;                 // the baked tip direction nearest the floor sun, 0..7
};

// CLASSES: porosity, gloss and how readily a hollow pools, per surface class (veg, soil, sand, rock,
// mud, water, weed, shell, litter). Hold is snow's, which is left out.
static const float TL6_POR[9]    = { 0.10, 0.30, 0.36, 0.20, 0.12, 0.0, 0.14, 0.16, 0.22 };
static const float TL6_GLOSS[9]  = { 0.10, 0.10, 0.22, 0.60, 0.75, 1.0, 0.85, 0.70, 0.00 };
static const float TL6_PUDDLE[9] = { 0.5, 1.0, 0.7, 1.0, 1.0, 0.0, 0.3, 0.2, 0.3 };

// sunVis's march, in texels.
static const float TL6_SSTEP[11] = { 1, 2, 3, 4, 6, 8, 11, 15, 20, 27, 36 };

// FOAMR, the whitecaps' ramp.
static const float3 TL6_FOAM[5] =
{
    float3(109, 138, 144), float3(154, 180, 182), float3(198, 215, 214), float3(226, 235, 233), float3(246, 250, 248)
};
static const float3 TL6_DEEP  = float3(35, 75, 88);     // #234b58
static const float3 TL6_SHOAL = float3(58, 106, 108);   // #3a6a6c
static const float3 TL6_COLD  = float3(29, 59, 74);     // #1d3b4a

// Toward the camera, floor space: 40 degrees up, from the south.
static const float3 TL6_VIEW = float3(0.0, 0.766044443118978, 0.642787609686539);

TEXTURE2D_ARRAY(_RelightNormal);
TEXTURE2D_ARRAY(_RelightLight);
TEXTURE2D_ARRAY(_RelightDetail);
TEXTURE2D(_RelightRamp);

// ---- helpers ------------------------------------------------------------------------------------

// Math.round: halves up.
float TL6Round(float v) { return floor(v + 0.5); }

// r2h's rounding: each channel to a byte.
float3 TL6Byte(float3 r) { return clamp(floor(r + 0.5), 0.0, 255.0); }

// mix(), a blend of two byte colours, rounded to bytes.
float3 TL6Mix(float3 a, float3 b, float t) { return TL6Byte(a + (b - a) * t); }

// PxLang.hash2: 32-bit integer arithmetic, to [0, 1). The top 24 bits, so the float is exact and
// never reaches 1; the rig's double differs by less than 2^-24.
float TL6Hash2(int x, int y, int s)
{
    uint h = (uint)x * 374761393u + (uint)y * 668265263u + (uint)s * 1442695041u;
    h ^= h >> 13;
    h *= 1274126177u;
    h ^= h >> 16;
    return (float)(h >> 8) * (1.0 / 16777216.0);
}

// vn: value noise on the integer lattice, smoothstep between the corners.
float TL6Vn(float x, float y, int s)
{
    float xi = floor(x), yi = floor(y), fx = x - xi, fy = y - yi;
    float u = fx * fx * (3.0 - 2.0 * fx), v = fy * fy * (3.0 - 2.0 * fy);
    int ix = (int)xi, iy = (int)yi;
    float a = TL6Hash2(ix, iy, s), b = TL6Hash2(ix + 1, iy, s), c = TL6Hash2(ix, iy + 1, s), d = TL6Hash2(ix + 1, iy + 1, s);
    return a + (b - a) * u + (c - a) * v + (a - b - c + d) * u * v;
}

// bandOff: the band step for an exposure.
int TL6BandOff(float e)
{
    float r = e / 0.995;
    return r >= 1.25 ? 1 : (r >= 0.30 ? 0 : (r >= 0.14 ? -1 : (r >= 0.06 ? -2 : -3)));
}

// ---- the maps -----------------------------------------------------------------------------------

// The rig's tile texel (x east, y down the tile, wrapping as a lone tile's G.ix does) to the array's.
int2 TL6Texel(int2 T) { return int2(T.x & (TL6_TILE - 1), (TL6_TILE - 1) - (T.y & (TL6_TILE - 1))); }

float4 TL6Bytes(float4 v) { return floor(v * 255.0 + 0.5); }

float4 TL6LoadNormal(int s, int2 T) { return TL6Bytes(LOAD_TEXTURE2D_ARRAY(_RelightNormal, TL6Texel(T), s)); }
float4 TL6LoadLight(int s, int2 T)  { return TL6Bytes(LOAD_TEXTURE2D_ARRAY(_RelightLight, TL6Texel(T), s)); }
float4 TL6LoadDetail(int s, int2 T) { return TL6Bytes(LOAD_TEXTURE2D_ARRAY(_RelightDetail, TL6Texel(T), s)); }

// The slice's (heightMin, heightRange, pondMax, has maps).
float4 TL6Params(int s) { return LOAD_TEXTURE2D(_RelightRamp, int2(TL6_RAMP_PARAMS, s)); }

// Palette p's band b of slice s, sRGB bytes.
float3 TL6Pal(int s, int p, int b) { return LOAD_TEXTURE2D(_RelightRamp, int2(p * 5 + b, s)).rgb; }

float TL6Height(int s, int2 T, float4 prm) { return prm.x + TL6LoadLight(s, T).b / 255.0 * prm.y; }
float TL6Pond(int s, int2 T, float4 prm)   { return TL6LoadNormal(s, T).a / 255.0 * prm.z; }
int   TL6Mark(int s, int2 T)               { float4 d = TL6LoadDetail(s, T); return (int)d.r + (int)d.g * 256; }

// ---- the sky ------------------------------------------------------------------------------------

TL6Sky TL6MakeSky(float3 l, float sunI, float skyI, float expo, float wet, float rain, float fog, float wind,
                  bool grade, float3 kc, float3 ac, float3 wc, float3 fc, float3 sc, float4 gradeWeights,
                  float seaDir, int fi)
{
    TL6Sky k;
    k.l = l; k.sunI = sunI; k.skyI = skyI; k.expo = expo;
    k.wet = wet; k.rain = rain; k.fog = fog; k.wind = wind; k.grade = grade;
    k.kc = kc; k.ac = ac; k.wc = wc; k.fc = fc; k.sc = sc;
    k.aa = gradeWeights.x; k.amb = gradeWeights.y; k.ka = gradeWeights.z; k.wa = gradeWeights.w;
    k.seaDir = seaDir == 0.0 ? 1.0 : seaDir;        // o.seaDir || 1
    k.fi = fi;
    k.sunOn = sunI > 0.01 && l.z > 0.02;
    // the sun's direction on the floor, for tips and seams
    float lx = l.x, ly = l.y, ll = length(float2(lx, ly));
    if (ll < 0.18) { lx = -0.6; ly = -0.8; } else { lx /= ll; ly /= ll; }
    k.ax = lx > 0.38 ? -1 : (lx < -0.38 ? 1 : 0);
    k.ay = ly > 0.38 ? -1 : (ly < -0.38 ? 1 : 0);
    // the bake's tips are the rig's argmax at eight directions; take the nearest (bakePass9.js TIP_DIRS)
    k.tipDir = ((int)TL6Round(atan2(ly, lx) / (TL6_TAU / 8.0)) + 8) & 7;
    return k;
}

// WaterRamp(body): five steps from the body colour toward the cold dark and the sky.
float3 TL6WaterRamp(float3 body, int b, TL6Sky k)
{
    if (b == 0) return TL6Mix(TL6Mix(body, float3(0, 0, 0), 0.42), TL6_COLD, 0.30);
    if (b == 1) return TL6Mix(TL6Mix(body, float3(0, 0, 0), 0.20), TL6_COLD, 0.14);
    if (b == 2) return body;
    if (b == 3) return TL6Mix(body, k.sc, 0.42);
    return TL6Mix(TL6Mix(k.sc, float3(255, 255, 255), 0.30), k.kc, 0.20);
}

// colour(): a ramp step, its specular kind, then the sky's grade. rb = ramp[bi], r4 = ramp[4].
float3 TL6Colour(float3 rb, float3 r4, int bi, int lit, int fq, int spc, int wk, TL6Sky k)
{
    float3 r = rb;
    if (spc == 1) r = r4 + (k.kc - r4) * 0.55;
    else if (spc == 2) r = r4 + (255.0 - r4) * 0.6;
    else if (spc == 3) r += (k.sc - r) * 0.30;
    if (k.grade)
    {
        float u = bi / 4.0, ta = k.aa * (1.0 - u) * (1.0 - k.amb * 0.35), tk = k.ka * u * (lit != 0 ? 1.0 : 0.3);
        float wd = wk == 2 ? 0.30 : (wk == 1 ? 0.12 : 0.0);
        r += (k.ac - r) * ta;
        r += (k.kc - r) * tk;
        r *= 1.0 - wd;
        if (k.wa != 0.0) r += (k.wc - r) * k.wa;
        if (fq != 0) r += (k.fc - r) * fq * 0.17;
    }
    return TL6Byte(r);
}

// sunVis: 0 when the tile's own relief blocks the sun at T, else 1.
float TL6SunVis(int s, int2 T, float3 l, float4 prm)
{
    float lh = length(l.xy);
    if (l.z <= 0.02) return 0.0;
    if (lh < 1e-3) return 1.0;
    float ux = l.x / lh, uy = l.y / lh, tn = l.z / lh;
    float h0 = TL6Height(s, T, prm) + 0.25, room = prm.x + prm.y - h0;   // hmax: the decoded top
    [loop] for (int n = 0; n < 11; n++)
    {
        float st = TL6_SSTEP[n];
        if (tn * st > room) break;
        int2 q = int2((int)TL6Round(T.x + ux * st), (int)TL6Round(T.y + uy * st));
        if (TL6Height(s, q, prm) > h0 + tn * st) return 0.0;
    }
    return 1.0;
}

// isPud: a hollow broad enough to hold water, by class.
bool TL6IsPud(int s, int2 T, int c, float wetT, float4 prm)
{
    float cp = TL6_PUDDLE[c];
    if (cp == 0.0) return false;
    float h = TL6Pond(s, T, prm), fill = (wetT - 0.25) / 0.75;
    if (h * cp <= (1.0 - fill) * 1.1 + 0.18) return false;
    float lo = h * 0.55;
    return TL6Pond(s, T + int2(1, 0), prm) > lo && TL6Pond(s, T + int2(-1, 0), prm) > lo
        && TL6Pond(s, T + int2(0, 1), prm) > lo && TL6Pond(s, T + int2(0, -1), prm) > lo;
}

// ---- relight ------------------------------------------------------------------------------------

// One texel of kit tile s, relit. T: the texel in the tile, the rig's orientation (0..255, y down the
// tile). W: the same texel on the world's pixel grid, x east and y SOUTH, where a scene anchors the
// rig's noise and hashes (the rig's X, Y). occ, skyv: the two per-texel inputs (unset = 0 and 1).
// Returns the rig's bytes, sRGB 0..255.
float3 TL6Relight(int s, int2 T, int2 W, TL6Sky k, float occ, float skyv)
{
    float4 prm = TL6Params(s);
    float4 nm = TL6LoadNormal(s, T), li = TL6LoadLight(s, T), de = TL6LoadDetail(s, T);
    float3 n = float3(nm.r / 255.0 * 2.0 - 1.0, -(nm.g / 255.0 * 2.0 - 1.0), nm.b / 255.0 * 2.0 - 1.0);
    n /= length(n);
    float ao = li.r / 255.0;
    int palIx = (int)li.a >> 3, band0 = (int)li.a & 7;
    int mark = (int)de.r + (int)de.g * 256, c = min((int)TL6Round(de.b / 28.0), 8), bits = (int)de.a;
    int i = T.y * TL6_TILE + T.x;
    float X = W.x, Y = W.y;
    int fq = k.fog > 0.01 ? (int)TL6Round(saturate(k.fog * 0.85) * 4.0) : 0;   // far = 0: no scene

    // light (A, B, D)
    float nl = dot(n, k.l);
    float vis = 0.0;                   // an if, not ?: (HLSL evaluates both sides of ?:, and the march is long)
    [branch] if (k.sunOn && nl > 0.0 && occ < 0.5) vis = TL6SunVis(s, T, k.l, prm);
    float sun = k.sunI * (nl > 0.0 ? pow(abs(nl), 1.25) : 0.0) * vis;
    float e0 = (0.36 * k.skyI * (0.30 + 0.70 * ao) * skyv * (0.45 + 0.55 * n.z) + sun) * k.expo;
    int db = k.grade ? TL6BandOff(e0) : (ao < 0.7 ? -1 : 0);
    int lit = sun > 0.22 ? 1 : 0;

    // water: the tile's own, and puddles (G)
    float wetT = k.wet;
    bool isW = c == TL6_CL_WATER;
    float depth = 0.2;
    if (!isW && wetT > 0.25 && nm.a / 255.0 * prm.z > 0.18)
    {
        if (TL6IsPud(s, T, c, wetT, prm)) { isW = true; depth = 0.05; }
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
            if (k.sunI > 0.08 && sp > 0.955 - 0.05 * k.wind && TL6Hash2(W.x, W.y, 11 + k.fi) < 0.55) { bi = 4; spc = 2; }
            else if (k.sunI > 0.08 && sp > 0.86 && crest > 0.5 && mk > 0.5 && TL6Hash2(W.x, W.y, 17 + k.fi) < 0.35) bi = 4;
        }
        if (k.wind > 0.55 && fetch > 0.5 && crest > 0.95 && mk > 0.62 && TL6Hash2(W.x >> 1, W.y, 23 + k.fi) < (k.wind - 0.55) * 1.1)
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
            float3 gnd = TL6Colour(TL6Pal(s, palIx, b0), TL6Pal(s, palIx, 4), b0, lit, fq, 0, 2, k);
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
            float hi = TL6Height(s, T, prm);
            if (k.ax != 0)
            {
                int2 j = T + int2(k.ax, 0);
                if (TL6Mark(s, j) != mark && hi - TL6Height(s, j, prm) > 0.2) seam = true;
            }
            if (!seam && k.ay != 0)
            {
                int2 j = T + int2(0, k.ay);
                if (TL6Mark(s, j) != mark && hi - TL6Height(s, j, prm) > 0.2) seam = true;
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
        rgb = TL6Colour(TL6Pal(s, palIx, bi), TL6Pal(s, palIx, 4), bi, lit, fq, spc, wk, k);
    }
    return rgb;
}

// The rig's bytes to the linear colour the albedo array's sRGB sampling would give.
float3 TL6SrgbToLinear(float3 bytes)
{
    float3 c = bytes / 255.0;
    float3 lo = c / 12.92, hi = pow(abs((c + 0.055) / 1.055), 2.4);
    return float3(c.r <= 0.04045 ? lo.r : hi.r, c.g <= 0.04045 ? lo.g : hi.g, c.b <= 0.04045 ? lo.b : hi.b);
}

#endif // HIDDEN_HARBOURS_TERRAIN_LIGHT6_INCLUDED
