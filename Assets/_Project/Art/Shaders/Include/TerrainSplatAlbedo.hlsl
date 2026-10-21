#ifndef HH_TERRAIN_SPLAT_ALBEDO_INCLUDED
#define HH_TERRAIN_SPLAT_ALBEDO_INCLUDED

// TerrainSplatAlbedo.hlsl — the GROUND'S OWN ALBEDO, one source for the two shaders that draw it (W4-4,
// ADR 0028). HiddenHarboursTerrainSplat.shader draws the ground; HiddenHarboursWater.shader draws the SAME
// ground as the floor of clear shallow water (W4-4b, behind its _ShallowsFloor switch). Both call these
// functions, so the floor under the sea is the ground by construction rather than a second copy that could
// drift: one band ladder, one meander, one weather sector, one bar, one splat unpack, one kit ladder, one
// macro drift and one wet band. The relight (terrain pass 9) stays the terrain's own: the floor takes the
// albedo path, and the sea's absorption and caustics are the light it is seen by.
//
// Moved here from the terrain statement for statement; only the names gained the TSA_ prefix (the water
// shader has its own Hash21 on other constants, and both must keep theirs).
//
// ⚠️ WHAT AN INCLUDER DECLARES FIRST:
//   - the uniforms these read, by the terrain's names and types: _FloorPaint, _FloorRipple, _FloorSand,
//     _FloorMarram, _FloorGrass, _FloorShingle, _BandBlendMetres, _EdgeFadeMetres, _BandWiggleMetres,
//     _BandWiggleScale, _BandDetailMetres, _BandDetailScale, _IslandCenter, _WeatherFacing, _IslandAspect,
//     _SectorFeather, _SectorBlend, _BarFrom, _BarTo, _BarHalfWidth, _BarSpineHalfWidth, _BarSpineFloor,
//     _BarEdgeBlend, _DetailOffsetCellMetres, _MacroScale, _MacroStrength, _MacroTint, _WetBandMetres,
//     _WetStrength and _WetTint;
//   - the textures _SplatA.._SplatF and the kit array _DetailArr256;
//   - TSA_SAMPLE_SPLAT(tex, smp, uv): one splat read, a float4;
//   - TSA_SAMPLE_KIT(uv, slice, duvx, duvy): one kit read with explicit gradients, a float4.
// The terrain reads each texture through its own sampler (the import state). The sea reads through URP's
// inline sampler_LinearClamp and sampler_PointRepeat (Core.hlsl's GlobalSamplers), which are the same
// states: the splats import bilinear, clamped and without mips, and the kit's contract is Point and
// Repeat (TerrainArrayAssets.RequireArray). Seven more samplers of its own would pass the sixteen a D3D11
// stage allows.
//
// THE TABLES. The terrain keeps MAT_SLICE, MAT_METRES and MAT_OFFSET literal in its own source, where
// TerrainSplatBandPinTests parse them, and defines TSA_TABLES_DEFINED after them. Any other includer gets
// the copy below, which ShallowsFloorShaderTests hold equal to the terrain's, entry for entry. Their
// meaning is documented beside the terrain's.
//
// Visual only: drives no sim, saves nothing (rule 5).

#ifndef TSA_TABLES_DEFINED
#define TSA_TABLES_DEFINED
static const float MAT_SLICE[21]  = { 0, 3, 6, 36, 39, 9, 42, 12, 15, 18, 45, 48, 21, 24, 51, 54, 27, 30, 33, 57, 60 };
static const float MAT_METRES[21] = { 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8 };
static const float MAT_OFFSET[21] = { 1, 0, 1, 1, 0, 1, 1, 1, 1, 1, 0, 1, 0, 0, 0, 1, 0, 1, 1, 1, 1 };
#define HH_MAT_COUNT 21
#endif

// The ground's hash: the meander's lattice, the kit's per-cell offsets. Not the water's Hash21, whose
// constants differ — this one is the ground's, wherever the ground is drawn.
float TSA_Hash21(float2 p)
{
    p = frac(p * float2(123.34, 456.21));
    p += dot(p, p + 45.32);
    return frac(p.x * p.y);
}

// Coherent value noise in [0,1]; the salt shifts the lattice so independent fields never
// correlate. Smoothstep-eased bilinear — meander, not speckle (StPetersShoreMap.Wiggle's
// shape, different hash; look-only by design, see the terrain shader's header).
float TSA_VNoise(float2 p, float salt)
{
    float2 i = floor(p) + salt * 101.7;
    float2 f = frac(p);
    float2 u = f * f * (3.0 - 2.0 * f);
    float a = TSA_Hash21(i);
    float b = TSA_Hash21(i + float2(1, 0));
    float c = TSA_Hash21(i + float2(0, 1));
    float d = TSA_Hash21(i + float2(1, 1));
    return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
}

// Fixed three octaves, written out — NEVER a loop with a runtime bound under [unroll]
// (the water shader's magenta classic; see WaterShaderCompileGuardTests).
float TSA_Fbm3(float2 p)
{
    float v = TSA_VNoise(p, 31.0) * 0.5;
    v += TSA_VNoise(p * 2.03, 37.0) * 0.3;
    v += TSA_VNoise(p * 4.01, 41.0) * 0.2;
    return v;
}

float TSA_DistToSegment(float2 p, float2 a, float2 b)
{
    float2 ab = b - a;
    float t = saturate(dot(p - a, ab) / max(dot(ab, ab), 1e-4));
    return length(p - a - ab * t);
}

// Soft threshold: the CPU ladder's ">= floor" with a metre-scale blend instead of a cliff.
float TSA_Band(float e, float floorM)
{
    return smoothstep(floorM - _BandBlendMetres, floorM + _BandBlendMetres, e);
}

// The painting's outer hem: 1 above the paint floor, fading out below it over _EdgeFadeMetres, where
// the ground hands off to the sea's own seabed. Raw elevation — the footprint is a ruling, not scenery
// (StPetersShoreMap's clamp comment). The terrain's alpha; the sea's share of the ground over its
// palette bed.
float TSA_Hem(float e)
{
    return smoothstep(_FloorPaint - _EdgeFadeMetres, _FloorPaint, e);
}

// A material's tile coordinates at a world position. The hashed whole-material offset
// applies per _DetailOffsetCellMetres cell, all steps together — offsetting steps apart
// would cross-fade misaligned tiles (README §4). The albedo and the relight share it, so a
// relit texel is the texel the albedo would have shown.
float2 TSA_MatUV(int i, float2 wp)
{
    float2 uv = wp / MAT_METRES[i];
    if (MAT_OFFSET[i] > 0.5)
    {
        float2 cell = floor(wp / max(_DetailOffsetCellMetres, 1.0));
        uv += float2(TSA_Hash21(cell), TSA_Hash21(cell + 17.0));
    }
    return uv;
}

// One material from the kit at a given intensity: bracket the ladder (README §2's HLSL,
// verbatim in spirit) and lerp two adjacent steps of the SAME material.
//
// Takes the world-position screen derivatives EXPLICITLY (SampleGrad): this is called
// inside a divergent [branch], where implicit-gradient sampling is illegal — the
// derivatives are computed once, outside all flow control, and scaled per material.
float3 TSA_SampleMat(int i, float2 wp, float intensity, float2 dwx, float2 dwy)
{
    float metres = MAT_METRES[i];
    float2 uv = TSA_MatUV(i, wp);
    float2 duvx = dwx / metres;
    float2 duvy = dwy / metres;

    float f = saturate(intensity) * 2.0;
    float a = floor(min(f, 1.999));
    float k = f - a;
    float s0 = MAT_SLICE[i] + a;
    float s1 = min(s0 + 1.0, MAT_SLICE[i] + 2.0);

    // One array since the px flip: the 512 branch retired with its array.
    float3 cA = TSA_SAMPLE_KIT(uv, s0, duvx, duvy).rgb;
    float3 cB = TSA_SAMPLE_KIT(uv, s1, duvx, duvy).rgb;
    return lerp(cA, cB, k);
}

// The ground's material weights and ladder intensities at a world position: the height-derived bands
// (the CPU ladders as soft gates) under the painted splat overrides. wp is the world position, wpx the
// same on the ground's pixel grid (the noise domain), uv the height map's (and the splats') coordinate
// there and e the raw elevation. Every one of the HH_MAT_COUNT entries of both arrays is written.
void TSA_GroundWeights(float2 wp, float2 wpx, float2 uv, float e,
                       out float w[HH_MAT_COUNT], out float intensity[HH_MAT_COUNT])
{
    // The meander: same two octaves as the CPU (0.8 m @ 16 m + 0.3 m @ 6 m), clamped up
    // to the paint floor so the wiggle moves ground BETWEEN bands, never out of the painting.
    float wig  = TSA_VNoise(wpx / max(_BandWiggleScale, 1e-3), 3.0) * 2.0 - 1.0;
    float wigD = TSA_VNoise(wpx / max(_BandDetailScale, 1e-3), 7.0) * 2.0 - 1.0;
    float look = max(_FloorPaint, e + wig * _BandWiggleMetres + wigD * _BandDetailMetres);

    // THE BAR — exempt from the wiggle (a path is signage, not scenery): inside it the
    // ladder reads the RAW elevation, and the sector is forced sheltered.
    float dBar = TSA_DistToSegment(wp, _BarFrom.xy, _BarTo.xy);
    float barOn = step(0.01, _BarHalfWidth);
    float barW = barOn * (1.0 - smoothstep(_BarHalfWidth - _BarEdgeBlend,
                                           _BarHalfWidth + _BarEdgeBlend, dBar));
    float lookB = lerp(look, max(_FloorPaint, e), barW);

    // --- HEIGHT-DERIVED BAND WEIGHTS (the CPU ladders as products of soft gates) --------
    float bg = TSA_Band(lookB, _FloorGrass);
    float bm = TSA_Band(lookB, _FloorMarram);
    float bs = TSA_Band(lookB, _FloorSand);
    float br = TSA_Band(lookB, _FloorRipple);
    float bh = TSA_Band(lookB, _FloorShingle);

    // Sheltered (west/north): shelf, ripple, sand, marram, grass.
    float sGrass  = bg;
    float sMarram = bm * (1.0 - bg);
    float sSand   = bs * (1.0 - bm) * (1.0 - bg);
    float sRipple = br * (1.0 - bs) * (1.0 - bm) * (1.0 - bg);
    float sShelf  = (1.0 - br) * (1.0 - bs) * (1.0 - bm) * (1.0 - bg);

    // Weather (south/east): shelf, shingle, grass — no dune, no beach.
    float wShingle = bh * (1.0 - bg);
    float wShelf   = (1.0 - bh) * (1.0 - bg);

    // Which coast: bearing against the weather facing, aspect-normalised about the island
    // centre, feathered by the same meander (StPetersShoreMap.IsWeatherCoast's shape).
    float2 d = wp - _IslandCenter.xy;
    d.y *= _IslandAspect;
    float2 dn = normalize(d + float2(1e-4, 0.0));
    float bearing = dot(dn, normalize(_WeatherFacing.xy));
    float thresh = wig * _SectorFeather;
    float ws = smoothstep(thresh - _SectorBlend, thresh + _SectorBlend, bearing);
    ws *= 1.0 - barW;

    // The bar's cobble spine — the walking line, raw elevation gated like the CPU.
    float spineW = barOn
        * (1.0 - smoothstep(_BarSpineHalfWidth - 1.0, _BarSpineHalfWidth + 1.0, dBar))
        * smoothstep(_BarSpineFloor - _BandBlendMetres, _BarSpineFloor + _BandBlendMetres, e)
        * barW;

    // Base weights in canonical material order (only the six band materials are nonzero).
    w[0] = sGrass;                                   // grass — same cap both coasts
    w[1] = sMarram * (1.0 - ws);
    w[2] = sSand   * (1.0 - ws);
    w[3] = wShingle * ws;
    w[4] = sRipple * (1.0 - ws);
    w[5] = lerp(sShelf, wShelf, ws);
    w[6] = 0.0; w[7] = 0.0; w[8] = 0.0; w[9] = 0.0;
    w[10] = 0.0; w[11] = 0.0; w[12] = 0.0; w[13] = 0.0;   // paint-only, like 6..9
    // The four v3 beds are paint-only too — and MUST be. A bed is a place, not an
    // elevation: the height ladder can say "this is low, wet, sheltered ground", but only
    // the painter knows a bed grew there. Deriving one from the bands would carpet every
    // metre of the same elevation on the island in mussels.
    w[14] = 0.0; w[15] = 0.0; w[16] = 0.0; w[17] = 0.0;
    // ⚠ AND THE LAWN, WHICH IS THE MOST PAINT-ONLY MATERIAL IN THE KIT. Nothing about an
    // elevation says somebody mows it; a lawn is a property boundary's answer. Leaving
    // this one unassigned is not a silent zero either — HLSL refuses the whole program
    // with "variable 'w' used without having been completely initialized", which is the
    // MAGENTA class, so every entry added to HH_MAT_COUNT needs a line here.
    w[18] = 0.0;
    // Mud (19, the px kit) is paint-only for the same reason: a cracked skin or a flooded
    // churn is where carts turned and cattle stood, and no elevation knows that.
    w[19] = 0.0;
    // Path (20, terrain pass 9) is paint-only too: a track is where feet went, and no
    // elevation knows that either.
    w[20] = 0.0;
    {
        float keep = 1.0 - spineW;
        for (int bi = 0; bi < HH_MAT_COUNT; bi++) w[bi] *= keep;
        w[3] += spineW;
    }

    // --- PAINTED OVERRIDES: twenty-one channels, value = weight AND ladder intensity ---
    float4 pA = TSA_SAMPLE_SPLAT(_SplatA, sampler_SplatA, uv);
    float4 pB = TSA_SAMPLE_SPLAT(_SplatB, sampler_SplatB, uv);
    float4 pC = TSA_SAMPLE_SPLAT(_SplatC, sampler_SplatC, uv);
    float4 pD = TSA_SAMPLE_SPLAT(_SplatD, sampler_SplatD, uv);
    float4 pE = TSA_SAMPLE_SPLAT(_SplatE, sampler_SplatE, uv);
    float4 pF = TSA_SAMPLE_SPLAT(_SplatF, sampler_SplatF, uv);
    float p[HH_MAT_COUNT];
    p[0]  = pA.r; p[1]  = pA.g; p[2]  = pA.b; p[3]  = pA.a;
    p[4]  = pB.r; p[5]  = pB.g; p[6]  = pB.b; p[7]  = pB.a;
    p[8]  = pC.r; p[9]  = pC.g; p[10] = pC.b; p[11] = pC.a;
    p[12] = pD.r; p[13] = pD.g; p[14] = pD.b; p[15] = pD.a;
    p[16] = pE.r; p[17] = pE.g; p[18] = pE.b; p[19] = pE.a;
    p[20] = pF.r;

    // ⚠ D.a IS NOW READ. It was a free slot until v3, which means the Properties default
    // of "black" — opaque, ALPHA 1 — used to be harmless here and no longer is: a
    // material rendered without the surface's MPB push would read a full-weight oyster
    // reef over the entire region. TerrainSplatSurface binds a transparent 1x1
    // (ClearSplat) for every map precisely so the default is never what samples, and a
    // test pins that it binds all SIX (_SplatF since terrain pass 9). The committed
    // StPetersSplatD.png was verified zero in .b/.a before these two slots were adopted —
    // taking over a channel that already has bytes in it is how a kit upgrade repaints a
    // region silently.
    // E.a joined it with Mud (2026-09-17): both committed SplatE PNGs (St Peters and Nine
    // Mile Creek) were verified zero in .a before that slot was adopted too.
    // F.r joined them with Path (terrain pass 9): the sixth map arrived blank in every
    // region, so no committed byte changed meaning. F.g, F.b and F.a are not read.
    // The sea's floor binds the same six through TerrainSplatSurface.WriteGroundFloor, the
    // same writer, so it never samples a default either.

    float paintSum = p[0]  + p[1]  + p[2]  + p[3]  + p[4]  + p[5]  + p[6]
                   + p[7]  + p[8]  + p[9]  + p[10] + p[11] + p[12] + p[13]
                   + p[14] + p[15] + p[16] + p[17] + p[18] + p[19] + p[20];
    float paintTotal = saturate(paintSum);
    // The painted share (paintTotal) is distributed by each channel's fraction of the
    // whole (p / paintSum) — in BOTH regimes, so the weights below always sum to 1.
    // Normalising only above 1 would leave partial paint summing to
    // 1 - paintSum + paintSum^2: every feathered stroke edge up to 25% darker than
    // either source material.
    float norm = 1.0 / max(paintSum, 1e-4);

    {
        float baseKeep = 1.0 - paintTotal;
        for (int mi = 0; mi < HH_MAT_COUNT; mi++)
        {
            float basePart  = w[mi] * baseKeep;          // band ground, ladder base (0.5)
            float paintPart = p[mi] * norm * paintTotal; // painted, ladder = channel value
            w[mi] = basePart + paintPart;
            intensity[mi] = w[mi] > 1e-4
                ? (0.5 * basePart + saturate(p[mi]) * paintPart) / w[mi]
                : 0.5;
        }
    }
}

// The kit's albedo alone: every material present, each at its own ladder intensity, summed by weight.
// The terrain's path while the relight maps are not bound, and the sea's floor always.
float3 TSA_KitAlbedo(float2 wp, float w[HH_MAT_COUNT], float intensity[HH_MAT_COUNT], float2 dwx, float2 dwy)
{
    float3 col = float3(0, 0, 0);
    for (int si = 0; si < HH_MAT_COUNT; si++)
    {
        [branch]
        if (w[si] > 0.004)
            col += w[si] * TSA_SampleMat(si, wp, intensity[si], dwx, dwy);
    }
    return col;
}

// Macro variation: tens-of-metres tint drift that kills any large-scale flatness.
// The kit flattens each tile's low-frequency mean on purpose (README §4) — this
// layer is the shader's half of that bargain.
float3 TSA_Macro(float3 col, float2 wp)
{
    float m = TSA_Fbm3(wp / max(_MacroScale, 1e-3));
    col *= lerp(1.0, _MacroTint.rgb, saturate(m) * _MacroStrength);
    return col;
}

// The wet band: ground just above the live waterline darkens — the tide line breathes with
// the SAME level the sea clips by. (Live water/wet effects are deliberately NOT in the kit's
// albedo — README §1.) Under water the ground is wet through: e is below the level, the band
// reads 1.
float3 TSA_Wet(float3 col, float e, float level)
{
    float wet = 1.0 - smoothstep(level, level + max(_WetBandMetres, 1e-3), e);
    col = lerp(col, col * _WetTint.rgb, wet * _WetStrength);
    return col;
}

#endif // HH_TERRAIN_SPLAT_ALBEDO_INCLUDED
