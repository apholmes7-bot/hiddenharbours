#ifndef HIDDEN_HARBOURS_TREE_WIND_MAPS_INCLUDED
#define HIDDEN_HARBOURS_TREE_WIND_MAPS_INCLUDED

// TreeWindMaps.hlsl — the pass-4 tree's OWN wind, flutter and snow, read from its baked maps.
//
// A port of the drop's reference shader, TreeMaps4.shade() in docs/art/rigs/treeMaps4.js. Every pixel of
// the sprite asks which REST texel the wind has carried onto it this frame and draws that texel: its
// albedo, or the snow the palette names for it, or the dark gap a leaf left behind. The wood and the dark
// between the leaves follow a smooth field (the trunk's lean and the limbs' wave); each leaf moves as ONE
// stamp, with a sub-pixel dither and, on its slots, a one-pixel flutter. It replaces the legacy vertex
// sway only where a renderer has bound the maps (_TreeMaps 1); the tree shader's own header says how.
//
// ⚠️ EVERY FUNCTION HERE HAS A LINE-FOR-LINE C# TWIN in HiddenHarbours.Art.TreeWindMath, and the tests
// hold the twin to both ends: to the JS through the repo's V8 (the source texel of every pixel of a cell,
// frame for frame) and to THIS FILE through its source (every #define and load-bearing expression below
// must still read the same). Change one, change the other in the same commit.
//
// THE MAPS. Two RGBA32 sheets laid out exactly like the albedo (one row of variant cells), imported
// LINEAR, point, uncompressed. wind: R lean · G sway · B|A the top of the packed word. phase: R wave ·
// G play · B depth (255 nearest) · A the word's low byte. The word is class 2 · gapSnow 3 · gap 3 ·
// snow 3 · stamp id + 1 13 (TreePass4Glue). The snow map is R8: the cover at which a texel turns (255
// never). The palette is 8 texels wide: row 0 the snow colours, row _TreeWind1.z this species' gaps.
// Everything is read with Load, never Sample: a texel is a record, not a colour to filter.
//
// ⚠️ THE RIG'S ROWS RUN DOWN, UNITY'S RUN UP. A rig pixel (x, y) of the cell starting at texel column
// cellX0 is texel (cellX0 + x, cellH − 1 − y). Every rig-space value here (the field's y, a flutter's
// sy −= 1) is y DOWN, so a leaf that lifts moves toward the crown on screen.

#define TREE_WIND_LOOP            16
#define TREE_WIND_X_SPAN          4.0
#define TREE_WIND_J_SPAN          0.5
#define TREE_WIND_R_MAX           1.3
#define TREE_WIND_MARGIN          3
#define TREE_WIND_REACH           2
#define TREE_WIND_ITERATIONS      2
#define TREE_WIND_FLUTTER_SLOTS   2
#define TREE_WIND_GAP_SNOW_MIN    153
#define TREE_WIND_SNOW_SCALE      254.0
#define TREE_WIND_SNOW_ON         0.02
#define TREE_WIND_CLASS_OUTSIDE   0
#define TREE_WIND_CLASS_WOOD      1
#define TREE_WIND_CLASS_BETWEEN   2
#define TREE_WIND_CLASS_LEAF      3
#define TREE_WIND_DITHER          0.8
#define TREE_WIND_FLUTTER_FRAMES  2.9
#define TREE_WIND_TAU             6.28318530718

// What TreeWindDecode picks: the source texel's own albedo, a snow-row column, or a gap-row column.
#define TREE_WIND_PICK_ALBEDO     0
#define TREE_WIND_PICK_SNOW_ROW   1
#define TREE_WIND_PICK_GAP_ROW    2

// Per renderer (TreeTrunkAnchor's property block). No samplers: Load only.
TEXTURE2D(_TreeWindTex);
TEXTURE2D(_TreePhaseTex);
TEXTURE2D(_TreeSnowTex);
TEXTURE2D(_TreePalette);

// GLOBAL, so OUTSIDE any CBUFFER: how far the snow has come, 0 bare … 1 full. Published by
// FoliageSnowBridge on DayStarted and SeasonChanged from FoliageSnowMath — recomputed from the clock and
// never saved. Named for all foliage so the shrubs and the grass can read the same number later.
float _FoliageSnow;

// The per-material rows — a MACRO, pasted into BOTH passes' UnityPerMaterial (the SRP Batcher wants one
// layout across a shader's passes). _TreeMaps, _TreeWind0, _TreeWind1 and _TreeCell are PUBLISHED per
// renderer; the last five are the material's feel.
#define TREE_WIND_MAPS_MATERIAL_ROWS \
    float  _TreeMaps; \
    float4 _TreeWind0; \
    float4 _TreeWind1; \
    float4 _TreeCell; \
    float  _TreeLoopSeconds; \
    float  _TreeGust; \
    float  _TreeWaveScale; \
    float  _TreePhaseJitter; \
    float  _TreeWindResponse;

struct TreeWindMapsParams
{
    float bendPx;       // _TreeWind0.x — trunk bend at a full gale (px)
    float limbPx;       // _TreeWind0.y — limb reach at a full gale (px)
    float bob;          // _TreeWind0.z — a limb's vertical bob, as a share of its reach
    float flutter;      // _TreeWind0.w — the species' flutter rate
    float shimmerCalm;  // _TreeWind1.x — shimmer strength × calm share: still-air flutter
    bool  conifer;      // _TreeWind1.y — a needle tuft flutters sideways, never up
    float gust;         // _TreeGust — the gust envelope's depth
    float response;     // _TreeWindResponse — _WindWorld's strength → the rig's w
};

#define TREE_WIND_MAPS_PARAMS(p) \
    TreeWindMapsParams p; \
    p.bendPx      = _TreeWind0.x; \
    p.limbPx      = _TreeWind0.y; \
    p.bob         = _TreeWind0.z; \
    p.flutter     = _TreeWind0.w; \
    p.shimmerCalm = _TreeWind1.x; \
    p.conifer     = _TreeWind1.y > 0.5; \
    p.gust        = _TreeGust; \
    p.response    = _TreeWindResponse;

// ---- the rig's arithmetic, exactly (helpers declared BEFORE use) ------------------------------------

// JavaScript's Math.round: a half rounds UP (−2.5 → −2). round() would round it to even.
int TreeJsRound(float x)
{
    float f = floor(x);
    return (int)f + ((x - f) >= 0.5 ? 1 : 0);
}

// The rig's integer hash, bit for bit (hsh in treeMaps4.js).
uint TreeHash(int n, int k)
{
    uint h = ((uint)n ^ ((uint)(k + 1) * 0x9e3779b9u)) * 0x85ebca6bu;
    h ^= h >> 13;
    h *= 0xc2b2ae35u;
    h ^= h >> 16;
    return h;
}

float TreeHashF(int n, int k)
{
    return (float)TreeHash(n, k) * 2.3283064365386963e-10;
}

float TreeSmooth(float e0, float e1, float x)
{
    float t = saturate((x - e0) / (e1 - e0));
    return t * t * (3.0 - 2.0 * t);
}

// A UNORM texel back to its byte. Exact for all 256.
uint TreeByte(float u)
{
    return (uint)(u * 255.0 + 0.5);
}

// ---- the frame: uniforms only, so arithmetic and never a texture read ------------------------------

struct TreeWindFrame
{
    float ww;        // the rig's w: 0 calm … 1 a full gale
    float dir;       // ±1: which way along x the wind blows
    float side;      // the share of the wind along x: the steady lean's weight
    int   f16;       // the loop frame, 0 … 15
    float ph;
    float env;       // the gust envelope
    float trunk;
    float la;
    float limbBias;  // the rig's +0.5, weighted by side
    float dRate;     // the chance a leaf's flutter slot fires
    float gust;
    float bob;
    bool  conifer;
    bool  still;     // calm and no shimmer: every texel draws itself
};

TreeWindFrame TreeWindFrameOf(float2 windWorld, int f16, TreeWindMapsParams p)
{
    TreeWindFrame fr;
    float len = length(windWorld);
    fr.ww = saturate(len * p.response);
    fr.dir = windWorld.x < 0.0 ? -1.0 : 1.0;
    fr.side = abs(windWorld.x) / max(len, 1e-4);
    fr.gust = saturate(p.gust);
    fr.f16 = f16;
    fr.ph = f16 / (float)TREE_WIND_LOOP;
    fr.env = 1.0 + fr.gust * 0.9 * sin(TREE_WIND_TAU * fr.ph + 0.7);
    fr.trunk = fr.ww * fr.ww * p.bendPx * (0.8 + 0.2 * fr.env) * fr.side
             + fr.ww * p.bendPx * 0.42 * sin(TREE_WIND_TAU * fr.ph + 0.3) * (0.55 + 0.45 * fr.env);
    fr.la = p.limbPx * fr.ww * (0.6 + 0.4 * fr.env);
    fr.limbBias = 0.5 * fr.side;
    float resp = TreeSmooth(0.0, 0.85, fr.ww);
    fr.dRate = min(1.0, (p.flutter * resp * 0.35 + p.shimmerCalm * 0.03) * TREE_WIND_LOOP / TREE_WIND_FLUTTER_FRAMES);
    fr.bob = p.bob;
    fr.conifer = p.conifer;
    fr.still = fr.ww <= 0.0 && fr.dRate <= 0.0;
    return fr;
}

// Where in its loop a tree is: the clock, minus a wave travelling along the wind, plus the tree's jitter.
float TreeWindLoopPos(float time, float2 root, float2 windWorld, float loopSeconds, float waveScale,
                      float noise, float jitter)
{
    float len = length(windWorld);
    float2 wdir = len > 1e-4 ? windWorld / len : float2(1.0, 0.0);
    return time / max(loopSeconds, 1e-3) - dot(root, wdir) * waveScale + noise * jitter;
}

int TreeWindF16(float loopPos)
{
    float frac01 = loopPos - floor(loopPos);
    return clamp((int)floor(frac01 * TREE_WIND_LOOP), 0, TREE_WIND_LOOP - 1);
}

// ---- one texel of the maps -------------------------------------------------------------------------

struct TreeWindTexel
{
    float lean;
    float sway;
    float x;
    float j;
    uint  word;
    uint  dep;
};

TreeWindTexel TreeWindLoad(int cellX0, int cellH, int x, int y)
{
    int2 tc = int2(cellX0 + x, cellH - 1 - y);
    float4 w = LOAD_TEXTURE2D(_TreeWindTex, tc);
    float4 f = LOAD_TEXTURE2D(_TreePhaseTex, tc);
    TreeWindTexel t;
    t.lean = w.r;
    t.sway = w.g * TREE_WIND_R_MAX;
    t.x = (f.r - 0.5) * TREE_WIND_X_SPAN;
    t.j = (f.g - 0.5) * TREE_WIND_J_SPAN;
    t.word = (TreeByte(w.b) << 16) | (TreeByte(w.a) << 8) | TreeByte(f.a);
    t.dep = TreeByte(f.b);
    return t;
}

uint TreeWindClassOf(uint word)        { return word >> 22; }
uint TreeWindGapSnowIndexOf(uint word) { return (word >> 19) & 7u; }
uint TreeWindGapIndexOf(uint word)     { return (word >> 16) & 7u; }
uint TreeWindSnowIndexOf(uint word)    { return (word >> 13) & 7u; }
uint TreeWindIdOf(uint word)           { return word & 0x1FFFu; }
int  TreeWindStampOf(uint word)        { return (int)TreeWindIdOf(word) - 1; }

// ---- the field, the offsets, the depth rule --------------------------------------------------------

float2 TreeWindField(TreeWindFrame fr, TreeWindTexel t)
{
    if (fr.ww <= 0.0) return float2(0.0, 0.0);
    float p = 2.0 * TREE_WIND_TAU * fr.ph - fr.dir * 0.9 * t.x + t.j;
    float reach = t.sway > 0.0 ? pow(abs(t.sway), 1.2) : 0.0;   // abs: a no-op here, it quiets FXC's X3571
    float fx = fr.dir * (fr.trunk * t.lean + fr.la * reach * (sin(p) + fr.limbBias));
    float fy = -fr.la * fr.bob * 0.5 * t.sway * sin(p + 1.1);
    return float2(fx, fy);
}

int2 TreeWindOffset(TreeWindFrame fr, TreeWindTexel t)
{
    float2 f = TreeWindField(fr, t);
    if (TreeWindClassOf(t.word) != TREE_WIND_CLASS_LEAF) return int2(TreeJsRound(f.x), TreeJsRound(f.y));

    int gs = TreeWindStampOf(t.word);
    int sx = TreeJsRound(f.x + (TreeHashF(gs, 8) - 0.5) * TREE_WIND_DITHER);
    int sy = TreeJsRound(f.y + (TreeHashF(gs, 9) - 0.5) * TREE_WIND_DITHER);
    if (fr.dRate > 0.0)
    {
        int dir = fr.dir < 0.0 ? -1 : 1;
        bool moved = false;
        UNITY_UNROLL
        for (int e = 0; e < TREE_WIND_FLUTTER_SLOTS; e++)
        {
            int s0 = (int)(TreeHash(gs, 10 + e) >> 28);
            int age = (int)((uint)(fr.f16 - s0 + TREE_WIND_LOOP) % (uint)TREE_WIND_LOOP);   // never negative
            int span = TreeHashF(gs, 12 + e) < 0.55 ? 1 : 2;
            float gate = fr.dRate * (1.0 + fr.gust * 0.9 * sin(TREE_WIND_TAU * s0 / TREE_WIND_LOOP + 0.7 - fr.dir * t.x * 1.3));
            if (!moved && age < span && TreeHashF(gs, 14 + e) < gate)
            {
                float r = TreeHashF(gs, 16 + e);
                if (fr.conifer) sx += r < 0.8 ? dir : -dir;
                else if (r < 0.55) sx += dir;
                else if (r < 0.75) sy -= 1;
                else if (r < 0.9) { sx += dir; sy -= 1; }
                else sx -= dir;
                moved = true;
            }
        }
    }
    return int2(sx, sy);
}

bool TreeWindFront(TreeWindTexel j, TreeWindTexel b)
{
    if (j.dep > b.dep + TREE_WIND_MARGIN) return true;
    if (b.dep > j.dep + TREE_WIND_MARGIN) return false;
    bool lj = TreeWindClassOf(j.word) == TREE_WIND_CLASS_LEAF;
    bool lb = TreeWindClassOf(b.word) == TREE_WIND_CLASS_LEAF;
    if (lj != lb) return lj;
    return lj ? TreeWindIdOf(j.word) > TreeWindIdOf(b.word) : j.dep > b.dep;
}

// ---- the gather --------------------------------------------------------------------------------------

// Which rest texel lands on rig pixel (x, y) this frame. Step twice back along the field; of the 25
// texels within REACH of where that lands, keep those whose own offset brings them here, and the frontmost
// wins. None? The texel under the landing point shows through, and if it is foliage it shows as the gap
// a leaf left (fallback). False: nothing is drawn here.
bool TreeWindGather(TreeWindFrame fr, int cellX0, int cellW, int cellH, int x, int y,
                    out int2 src, out TreeWindTexel srcT, out bool fallback)
{
    src = int2(-1, -1);
    srcT = (TreeWindTexel)0;
    fallback = false;
    bool found = false;

    // ONE exit, at the bottom: an early return past out parameters draws FXC's X4000.
    if (fr.still)
    {
        // Calm and no shimmer: every field is 0 and the dither rounds to 0, so the only texel that lands
        // here is this one. Exact, and 2 loads instead of 54.
        TreeWindTexel self = TreeWindLoad(cellX0, cellH, x, y);
        if (TreeWindClassOf(self.word) != TREE_WIND_CLASS_OUTSIDE)
        {
            src = int2(x, y);
            srcT = self;
            found = true;
        }
    }
    else
    {
        int sx = x, sy = y;
        UNITY_UNROLL
        for (int it = 0; it < TREE_WIND_ITERATIONS; it++)
        {
            float2 f = TreeWindField(fr, TreeWindLoad(cellX0, cellH, clamp(sx, 0, cellW - 1), clamp(sy, 0, cellH - 1)));
            sx = x - TreeJsRound(f.x);
            sy = y - TreeJsRound(f.y);
        }

        UNITY_LOOP
        for (int dy = -TREE_WIND_REACH; dy <= TREE_WIND_REACH; dy++)
        {
            UNITY_LOOP
            for (int dx = -TREE_WIND_REACH; dx <= TREE_WIND_REACH; dx++)
            {
                int xx = sx + dx, yy = sy + dy;
                if (xx < 0 || xx >= cellW || yy < 0 || yy >= cellH) continue;
                TreeWindTexel c = TreeWindLoad(cellX0, cellH, xx, yy);
                if (TreeWindClassOf(c.word) == TREE_WIND_CLASS_OUTSIDE) continue;
                int2 o = TreeWindOffset(fr, c);
                if (xx + o.x != x || yy + o.y != y) continue;
                if (!found || TreeWindFront(c, srcT))
                {
                    src = int2(xx, yy);
                    srcT = c;
                    found = true;
                }
            }
        }

        if (!found && sx >= 0 && sy >= 0 && sx < cellW && sy < cellH)
        {
            TreeWindTexel c = TreeWindLoad(cellX0, cellH, sx, sy);
            uint cls = TreeWindClassOf(c.word);
            if (cls != TREE_WIND_CLASS_OUTSIDE)
            {
                src = int2(sx, sy);
                srcT = c;
                fallback = cls >= TREE_WIND_CLASS_BETWEEN;
                found = true;
            }
        }
    }
    return found;
}

// ---- the colour ----------------------------------------------------------------------------------------

// The colour rule, as the drop's own check decodes it: a leaf showing through where it was carried away
// (fallback) is the dark gap, which snows from 0.6 cover; every other texel is its albedo until the cover
// reaches its snow byte. Returns a TREE_WIND_PICK_*; index is the palette column.
uint TreeWindDecode(uint word, uint snowByte, bool fallback, float cover, out uint index)
{
    cover = saturate(cover);
    uint cls = TreeWindClassOf(word);
    int kk = TreeJsRound(cover * TREE_WIND_SNOW_SCALE);
    bool snowing = cover > TREE_WIND_SNOW_ON;
    if (fallback && cls == TREE_WIND_CLASS_LEAF)
    {
        bool on = snowing && (int)max(snowByte, (uint)TREE_WIND_GAP_SNOW_MIN) <= kk;
        index = on ? TreeWindGapSnowIndexOf(word) : TreeWindGapIndexOf(word);
        return on ? TREE_WIND_PICK_SNOW_ROW : TREE_WIND_PICK_GAP_ROW;
    }
    bool lit = snowing && (int)snowByte <= kk;
    index = lit ? TreeWindSnowIndexOf(word) : 0u;
    return lit ? TREE_WIND_PICK_SNOW_ROW : TREE_WIND_PICK_ALBEDO;
}

// A sheet texel's cell and rig pixel. The sheet is ONE row of cells; cell.xy is a cell's size and
// cell.zw the sheet's, in texels.
int2 TreeWindRigOfTexel(int2 texel, float4 cell, out int cellX0)
{
    int cellW = (int)cell.x, cellH = (int)cell.y;
    cellX0 = (int)(((uint)texel.x / (uint)cellW) * (uint)cellW);   // texel.x is never negative
    return int2(clamp(texel.x - cellX0, 0, cellW - 1), cellH - 1 - clamp(texel.y, 0, cellH - 1));
}

int2 TreeWindTexelOfRig(int cellX0, int cellH, int2 rig)
{
    return int2(cellX0 + rig.x, cellH - 1 - rig.y);
}

// The sheet texel under a uv, kept on the sheet: a uv of exactly 1 would floor one texel past its edge.
int2 TreeWindTexelOfUv(float2 uv, float4 cell)
{
    int2 t = int2(floor(uv * cell.zw));
    return clamp(t, int2(0, 0), int2(cell.zw) - 1);
}

// A texel's centre as a uv: where the light sheets are read for a carried texel, so its light moves with it.
float2 TreeWindSourceUv(int2 texel, float4 cell)
{
    return (float2(texel) + 0.5) / cell.zw;
}

// The rest pose's colour at a sheet texel, for the passes that do not sway (the HHReflect mirror):
// every texel is its own source, so only the snow can change it.
float3 TreeWindRestSnow(float3 albedoRgb, int2 texel, float cover)
{
    float4 w = LOAD_TEXTURE2D(_TreeWindTex, texel);
    float4 f = LOAD_TEXTURE2D(_TreePhaseTex, texel);
    uint word = (TreeByte(w.b) << 16) | (TreeByte(w.a) << 8) | TreeByte(f.a);
    if (TreeWindClassOf(word) == TREE_WIND_CLASS_OUTSIDE) return albedoRgb;
    uint index;
    uint pick = TreeWindDecode(word, TreeByte(LOAD_TEXTURE2D(_TreeSnowTex, texel).r), false, cover, index);
    return pick == TREE_WIND_PICK_SNOW_ROW ? LOAD_TEXTURE2D(_TreePalette, int2(index, 0)).rgb : albedoRgb;
}

#endif // HIDDEN_HARBOURS_TREE_WIND_MAPS_INCLUDED
