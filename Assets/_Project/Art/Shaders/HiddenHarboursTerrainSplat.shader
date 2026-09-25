// HiddenHarboursTerrainSplat.shader — the ground as a painted FIELD, not a grid of tiles (ADR 0028).
//
// One full-region quad replaces the per-cell ground/fringe tilemaps. The fragment reads the SAME
// painted height data the water shader and the walk gate read (the _HeightTex vocabulary of
// HiddenHarboursWater.shader, verbatim), classifies elevation into the StPetersShoreMap band
// ladder with SOFT metre-scale edges, and shades each material from the terrain material kit
// (docs/art/rigs/terrain — 20 plan-projection materials x 3 intensity steps, packed into ONE
// Texture2DArray by TerrainTexArrayBuilder). World-space sampling with per-cell hashed offsets
// on the kit's offset-allowed materials means repetition cannot align by construction.
//
// PAINTED OVERRIDES (PR 2): five splat maps carry twenty 0..1 channels, one per material. A
// channel's value is BOTH the blend weight against the height-derived bands AND the position on
// that material's intensity ladder (_Lo -> base -> _Hi; the kit designs low intensity to READ
// sparse — README §2 — so one number does both jobs honestly). Unpainted ground renders the
// height bands at the ladder's base step. The kit's two FACE-projection materials (Sandstone,
// Bank — cliff faces) are imported but deliberately not wired here; they await cliff geometry.
//
// KIT V2 added four shoreline materials (Foreshore, Talus, Ledge, Rockweed) at indices 10..13.
// Ten channels no longer fit three RGBA maps, so _SplatD joined A/B/C.
//
// KIT V3 added the four REEF BEDS (Musselbed, Oysterreef, Eelgrass, Irishmoss) at 14..17. They
// took D's two free slots and needed _SplatE for the rest (E.b went to the mown Lawn, 18, on
// 2026-08-26). A bed is a ground MATERIAL, not scatter, and that is the kit's ruling (README §6): at
// 32 px/m a mussel is two texels long, so the animals ARE the substrate and what reads is grain,
// clumping and gaps. Nothing here is bed-specific — they sample through the same ladder as every
// other material, which is exactly why four new materials cost this shader four table entries.
//
// THE PX FLIP (2026-09-17, owner rulings A2 and M1). The live albedo is the greenery px kit's bytes,
// and that kit ships every plan material at 256 px / 8 m, so the seven that were 512 px / 16 m
// (Shingle, Ripple, Silt, Foreshore, Talus, Musselbed, Oysterreef) moved into the one 256 array and
// the 512 array retired: one array, one sampler, 8 m tiles everywhere. The kit's Mud took index 19,
// E.a, the last channel of the five maps; the next material (the kit's Path) needs a _SplatF.
//
// TERRAIN PASS 9 (PR 2, the cliff & rock kit v6). The kit's tiles now carry baked MAPS beside their
// albedo (the normal and pond depth, sky visibility and height, the marks and their tips) and a palette
// ramp, and TerrainLight6 relights a tile per texel under the sky: Include/TerrainLight6.hlsl, statement
// for statement from docs/art/rigs/terrain/pass9/terrainLight6.js, whose C# twin the tests hold to the
// rig byte for byte. With the maps bound (_RelightLoaded, pushed by TerrainSplatSurface.ConfigureRelight)
// a material whose tiles have maps renders the rig's bytes for each ladder step, lerped by the albedo's
// own k: the ladder is kept, and a channel still picks the step. A tile without maps (the lawn's) and
// every material while _RelightLoaded is 0 take the albedo path unchanged. Left out on purpose: ground
// snow (the plan's decision 4) and the shoreline foam and tide (decision 5: the sea plane owns them). The
// three inputs TerrainLight6 added default to unset: occ 0 and skyv 1 (_TLOccSkyv black), seaDir 1
// (_TLSeaDir 0). _SplatF is declared and bound for the kit's Path, whose slot joins the tables below
// when Path's tiles join the array.
//
// The kit's five EDGE STRIPS (the sod lip, scarp, wrack line, weed line, reef margin) are imported
// under Terrain/Edges but not sampled here — they are decals laid along a spline by signed
// distance, which is a different addressing scheme than this world-XZ tiling
// (docs/design/terrain-edge-strips.md).
//
// The band constants are NOT owned here: the builder pushes StPetersShoreMap's numbers through
// TerrainSplatSurface at build time, and TerrainSplatBandPinTests holds this shader's DEFAULTS to
// the same constants so the two implementations cannot drift silently. The material/slice tables
// below are mirrored by TerrainTexArrayBuilder — the same pin tests hold those together too.
//
// Sorts through a SortingGroup (the ADR 0023 pattern) BELOW the Sea plane, so the ADR 0012 tide
// reveal keeps working unchanged. The wet band above the live waterline reads _WaterLevel — the
// same number the sea clips by. With no detail arrays bound (_DetailLoaded = 0) the shader falls
// back to the PR-1 flat two-tone colours, so a fresh checkout renders sanely before the first
// array build.
Shader "HiddenHarbours/TerrainSplat"
{
    Properties
    {
        [Header(Height field. Shared vocabulary with the water shader)]
        [NoScaleOffset] _HeightTex ("Seabed height map. R is elevation", 2D) = "black" {}
        _HeightMin      ("Height map min in metres", Float) = -4.0
        _HeightMax      ("Height map max in metres", Float) = 6.0
        _HeightWorldMin ("Height map world min xy", Vector) = (-380, -260, 0, 0)
        _HeightWorldSize("Height map world size xy", Vector) = (760, 520, 0, 0)
        _WaterLevel     ("Water level in metres. Sim driven", Float) = 0.5

        [Header(Detail texture array. Built by TerrainTexArrayBuilder)]
        [NoScaleOffset] _DetailArr256 ("Detail array 256 class", 2DArray) = "" {}
        _DetailLoaded ("Detail arrays loaded", Float) = 0.0
        _DetailOffsetCellMetres ("Hashed offset cell in metres", Float) = 32.0

        [Header(Terrain light 6. The kit relit from its baked maps. Built by TerrainTexArrayBuilder)]
        [NoScaleOffset] _RelightNormal ("Relight normal and pond", 2DArray) = "" {}
        [NoScaleOffset] _RelightLight  ("Relight sky visibility and height", 2DArray) = "" {}
        [NoScaleOffset] _RelightDetail ("Relight marks and tips", 2DArray) = "" {}
        [NoScaleOffset] _RelightRamp   ("Relight palettes and slice parameters", 2D) = "black" {}
        _RelightLoaded ("Relight maps loaded", Float) = 0.0
        [NoScaleOffset] _TLOccSkyv ("Occlusion R and hidden sky G in height uv. Black is unset", 2D) = "black" {}

        [Header(Terrain light 6 sky. The rig afternoon until the engine feeds it)]
        _TLSunF         ("Sun toward floor xyz with y south. Used while no cycle runs", Vector) = (-0.538986, -0.196175, 0.819152, 0)
        _TLSunI         ("Sun intensity", Float) = 1.0
        _TLSkyI         ("Sky intensity", Float) = 0.6
        _TLExpo         ("Exposure", Float) = 1.0
        _TLWet          ("Wet", Range(0, 1)) = 0.0
        _TLRain         ("Rain", Range(0, 1)) = 0.0
        _TLFog          ("Fog", Range(0, 1)) = 0.0
        _TLWind         ("Wind", Range(0, 1)) = 0.22
        _TLGrade        ("Grade on", Float) = 1.0
        _TLKey          ("Key colour in sRGB bytes", Vector) = (255, 240, 207, 0)
        _TLAmbient      ("Ambient colour in sRGB bytes", Vector) = (29, 59, 74, 0)
        _TLWash         ("Wash colour in sRGB bytes", Vector) = (255, 244, 221, 0)
        _TLFogColour    ("Fog colour in sRGB bytes", Vector) = (186, 203, 211, 0)
        _TLSkyColour    ("Sky colour in sRGB bytes", Vector) = (176, 201, 216, 0)
        _TLGradeWeights ("Grade weights aa amb ka wa", Vector) = (0.3, 0.62, 0.16, 0.03)
        _TLSeaDir       ("Swell direction. Zero is unset and reads as one", Float) = 0.0
        _TLFrameRate    ("Loop frames per second", Float) = 8.0
        _TLOrigin       ("Pixel grid origin xy in world metres", Vector) = (0, 0, 0, 0)

        [Header(Painted splat maps. Twenty channels across five textures)]
        [NoScaleOffset] _SplatA ("Splat A. Grass Marram Sand Shingle", 2D) = "black" {}
        [NoScaleOffset] _SplatB ("Splat B. Ripple Shelf Silt Dirt", 2D) = "black" {}
        [NoScaleOffset] _SplatC ("Splat C. Marsh Sedge Foreshore Talus", 2D) = "black" {}
        [NoScaleOffset] _SplatD ("Splat D. Ledge Rockweed Musselbed Oysterreef", 2D) = "black" {}
        [NoScaleOffset] _SplatE ("Splat E. Eelgrass Irishmoss Lawn Mud", 2D) = "black" {}
        [NoScaleOffset] _SplatF ("Splat F. Path", 2D) = "black" {}

        [Header(Band floors in metres. Builder pushes StPetersShoreMap)]
        _FloorPaint   ("Paint floor", Float) = -1.95
        _FloorRipple  ("Ripple floor", Float) = -1.7
        _FloorSand    ("Sand floor", Float) = -0.4
        _FloorMarram  ("Marram floor", Float) = 1.6
        _FloorGrass   ("Grass floor", Float) = 4.2
        _FloorShingle ("Shingle floor. Weather coast", Float) = -0.4
        _BandBlendMetres ("Band edge blend in metres", Float) = 0.35
        _EdgeFadeMetres  ("Fade below paint floor in metres", Float) = 0.6

        [Header(Band meander. Same parameters as the CPU classifier)]
        _BandWiggleMetres ("Wiggle amplitude in metres", Float) = 0.8
        _BandWiggleScale  ("Wiggle scale in metres", Float) = 16.0
        _BandDetailMetres ("Detail amplitude in metres", Float) = 0.3
        _BandDetailScale  ("Detail scale in metres", Float) = 6.0

        [Header(Weather sector)]
        _IslandCenter  ("Island centre xy", Vector) = (70, 0, 0, 0)
        _IslandAspect  ("Island aspect. RadiusX over radiusY", Float) = 1.7308
        _WeatherFacing ("Weather facing xy", Vector) = (1, -1, 0, 0)
        _SectorFeather ("Sector feather", Float) = 0.12
        _SectorBlend   ("Sector blend width", Float) = 0.08

        [Header(Sandbar. Half width zero disables)]
        _BarFrom          ("Bar from xy", Vector) = (0, 0, 0, 0)
        _BarTo            ("Bar to xy", Vector) = (0, 0, 0, 0)
        _BarHalfWidth     ("Bar half width in metres", Float) = 0.0
        _BarSpineHalfWidth("Spine half width in metres", Float) = 8.0
        _BarSpineFloor    ("Spine floor in metres", Float) = 0.377
        _BarEdgeBlend     ("Bar edge blend in metres", Float) = 2.0

        [Header(Grain and macro variation)]
        _PixelsPerMetre ("Pixels per metre", Float) = 32.0
        _GrainScale     ("Grain cell in metres", Float) = 0.75
        _MacroScale     ("Macro scale in metres", Float) = 48.0
        _MacroStrength  ("Macro strength", Range(0, 1)) = 0.22
        _MacroTint      ("Macro tint", Color) = (0.72, 0.66, 0.55, 1)

        [Header(Wet band above the waterline)]
        _WetBandMetres ("Wet band in metres", Float) = 0.45
        _WetStrength   ("Wet strength", Range(0, 1)) = 0.45
        _WetTint       ("Wet tint", Color) = (0.45, 0.52, 0.58, 1)

        [Header(Fallback colours when no detail arrays. A base and B grain)]
        _GrassColA   ("Grass A", Color) = (0.30, 0.46, 0.22, 1)
        _GrassColB   ("Grass B", Color) = (0.38, 0.55, 0.27, 1)
        _MarramColA  ("Marram A", Color) = (0.47, 0.52, 0.28, 1)
        _MarramColB  ("Marram B", Color) = (0.56, 0.60, 0.33, 1)
        _SandColA    ("Sand A", Color) = (0.80, 0.72, 0.48, 1)
        _SandColB    ("Sand B", Color) = (0.87, 0.79, 0.56, 1)
        _ShingleColA ("Shingle A", Color) = (0.48, 0.47, 0.44, 1)
        _ShingleColB ("Shingle B", Color) = (0.58, 0.56, 0.52, 1)
        _RippleColA  ("Ripple A", Color) = (0.56, 0.31, 0.20, 1)
        _RippleColB  ("Ripple B", Color) = (0.66, 0.40, 0.26, 1)
        _ShelfColA   ("Shelf A", Color) = (0.30, 0.29, 0.25, 1)
        _ShelfColB   ("Shelf B", Color) = (0.37, 0.35, 0.30, 1)
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }

        Pass
        {
            Name "HHTerrainSplat"
            Tags { "LightMode" = "Universal2D" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_HeightTex); SAMPLER(sampler_HeightTex);
            TEXTURE2D(_SplatA); SAMPLER(sampler_SplatA);
            TEXTURE2D(_SplatB); SAMPLER(sampler_SplatB);
            TEXTURE2D(_SplatC); SAMPLER(sampler_SplatC);
            TEXTURE2D(_SplatD); SAMPLER(sampler_SplatD);
            TEXTURE2D(_SplatE); SAMPLER(sampler_SplatE);
            TEXTURE2D(_SplatF); SAMPLER(sampler_SplatF);
            TEXTURE2D_ARRAY(_DetailArr256); SAMPLER(sampler_DetailArr256);
            TEXTURE2D(_TLOccSkyv); SAMPLER(sampler_TLOccSkyv);

            CBUFFER_START(UnityPerMaterial)
                float  _HeightMin, _HeightMax;
                float4 _HeightWorldMin, _HeightWorldSize;
                float  _WaterLevel;

                float _DetailLoaded, _DetailOffsetCellMetres;

                float  _RelightLoaded;
                float4 _TLSunF;
                float  _TLSunI, _TLSkyI, _TLExpo, _TLWet, _TLRain, _TLFog, _TLWind, _TLGrade;
                float4 _TLKey, _TLAmbient, _TLWash, _TLFogColour, _TLSkyColour, _TLGradeWeights;
                float  _TLSeaDir, _TLFrameRate;
                float4 _TLOrigin;

                float _FloorPaint, _FloorRipple, _FloorSand, _FloorMarram, _FloorGrass, _FloorShingle;
                float _BandBlendMetres, _EdgeFadeMetres;
                float _BandWiggleMetres, _BandWiggleScale, _BandDetailMetres, _BandDetailScale;

                float4 _IslandCenter, _WeatherFacing;
                float  _IslandAspect, _SectorFeather, _SectorBlend;

                float4 _BarFrom, _BarTo;
                float  _BarHalfWidth, _BarSpineHalfWidth, _BarSpineFloor, _BarEdgeBlend;

                float  _PixelsPerMetre, _GrainScale, _MacroScale, _MacroStrength;
                float4 _MacroTint;
                float  _WetBandMetres, _WetStrength;
                float4 _WetTint;

                float4 _GrassColA, _GrassColB, _MarramColA, _MarramColB, _SandColA, _SandColB;
                float4 _ShingleColA, _ShingleColB, _RippleColA, _RippleColB, _ShelfColA, _ShelfColB;
            CBUFFER_END

            // The day/night cycle's sun, DayNightController's globals (set per frame, never material
            // properties, so they live outside UnityPerMaterial, as in CliffFace and SpriteLitDecor):
            // _SunDir.xy the unit ground direction TOWARD the sun, +y north, and (0, 0) while no cycle
            // runs; _SunElevation 1 at noon, 0 at the horizon, negative at night; _ShadowStrength the
            // elevation folded with the live weather (DayNightMath.ShadowStrength).
            float4 _SunDir;
            float  _SunElevation;
            float  _ShadowStrength;

            #include "Assets/_Project/Art/Shaders/Include/TerrainLight6.hlsl"

            // =========================================================================================
            //  THE MATERIAL TABLE — canonical order 0..19. Mirrored by TerrainTexArrayBuilder (C#) and
            //  by the splat channel packing (A.rgba, B.rgba, C.rgba, D.rgba, E.rgba). A pin test holds
            //  all three together. APPEND ONLY: committed splat PNGs and this unpack agree on index
            //  meaning.
            //    0 grass   1 marram   2 sand       3 shingle   4 ripple     5 shelf      6 silt
            //    7 dirt    8 marsh    9 sedge     10 foreshore 11 talus    12 ledge     13 rockweed
            //   14 musselbed  15 oysterreef  16 eelgrass  17 irishmoss          (kit v3 reef beds)
            //   18 lawn                                                          (the mown dooryard)
            //   19 mud                                                           (the px kit, E.a)
            //  ONE array since the px flip (2026-09-17, owner ruling A2): every material samples
            //  _DetailArr256 at 8 m, and the old MAT_ARRAY selector retired with the 512 array.
            //  MAT_SLICE: base slice (the _Lo step; +1 base, +2 _Hi — the kit ladder, README §2). The
            //  seven that left the 512 array were appended after lawn (36..54); mud is 57.
            //  MAT_OFFSET: hashed per-cell UV offset allowed (README §4: NEVER on a directional
            //  material — an offset slices a ripple train, a wind-combed stand, a bedding plane, a
            //  lie of fronds, a MUSSEL LIE or an EELGRASS RIBBON apart at the cell border. That list
            //  is ripple, marram, foreshore, ledge, rockweed, musselbed and eelgrass; all seven carry
            //  enough low-frequency variation to hide the repeat alone. Oysterreef and Irishmoss DO
            //  take an offset — oyster clusters are near-isotropic and moss cushions scatter — which
            //  is why the four beds do not share one flag. Mud takes one too, like dirt: a cracked
            //  skin or a churned flood has no lie to slice.)
            //  The relight's maps (terrain pass 9) are built in the albedo array's slice order, so
            //  MAT_SLICE addresses them too, and MatUV's offset moves a material's maps with its albedo.
            // =========================================================================================
            static const float MAT_SLICE[20]  = { 0, 3, 6, 36, 39, 9, 42, 12, 15, 18, 45, 48, 21, 24, 51, 54, 27, 30, 33, 57 };
            static const float MAT_METRES[20] = { 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8 };
            static const float MAT_OFFSET[20] = { 1, 0, 1, 1, 0, 1, 1, 1, 1, 1, 0, 1, 0, 0, 0, 1, 0, 1, 1, 1 };

            // The one place the material count lives for the fragment's local arrays and loops. The
            // three tables above must stay LITERAL-sized (the pin test parses their declared length
            // out of this source), but an array declared 18 and walked to 14 renders nothing for the
            // beds and says nothing about it — so the loops read this instead of a repeated digit.
            #define HH_MAT_COUNT 20

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 worldXY    : TEXCOORD0;
            };

            Varyings vert (Attributes v)
            {
                Varyings o;
                float3 posWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(posWS);
                o.worldXY = posWS.xy;
                return o;
            }

            // Same hash family as the water shader (kept local: the water deliberately owns its own
            // copy inside its two-pass HLSLINCLUDE; a shared include is a refactor for a 4th consumer).
            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            // Coherent value noise in [0,1]; the salt shifts the lattice so independent fields never
            // correlate. Smoothstep-eased bilinear — meander, not speckle (StPetersShoreMap.Wiggle's
            // shape, different hash; look-only by design, see the header).
            float VNoise(float2 p, float salt)
            {
                float2 i = floor(p) + salt * 101.7;
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                float a = Hash21(i);
                float b = Hash21(i + float2(1, 0));
                float c = Hash21(i + float2(0, 1));
                float d = Hash21(i + float2(1, 1));
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            // Fixed three octaves, written out — NEVER a loop with a runtime bound under [unroll]
            // (the water shader's magenta classic; see WaterShaderCompileGuardTests).
            float Fbm3(float2 p)
            {
                float v = VNoise(p, 31.0) * 0.5;
                v += VNoise(p * 2.03, 37.0) * 0.3;
                v += VNoise(p * 4.01, 41.0) * 0.2;
                return v;
            }

            float DistToSegment(float2 p, float2 a, float2 b)
            {
                float2 ab = b - a;
                float t = saturate(dot(p - a, ab) / max(dot(ab, ab), 1e-4));
                return length(p - a - ab * t);
            }

            // Soft threshold: the CPU ladder's ">= floor" with a metre-scale blend instead of a cliff.
            float Band(float e, float floorM)
            {
                return smoothstep(floorM - _BandBlendMetres, floorM + _BandBlendMetres, e);
            }

            // A material's tile coordinates at a world position. The hashed whole-material offset
            // applies per _DetailOffsetCellMetres cell, all steps together — offsetting steps apart
            // would cross-fade misaligned images (README §4). The albedo and the relight share it, so a
            // relit texel is the texel the albedo would have shown.
            float2 MatUV(int i, float2 wp)
            {
                float2 uv = wp / MAT_METRES[i];
                if (MAT_OFFSET[i] > 0.5)
                {
                    float2 cell = floor(wp / max(_DetailOffsetCellMetres, 1.0));
                    uv += float2(Hash21(cell), Hash21(cell + 17.0));
                }
                return uv;
            }

            // One material from the kit at a given intensity: bracket the ladder (README §2's HLSL,
            // verbatim in spirit) and lerp two neighbouring steps of the SAME material.
            //
            // Takes the world-position screen derivatives EXPLICITLY (SampleGrad): this is called
            // inside a divergent [branch], where implicit-gradient sampling is illegal — the
            // derivatives are computed once, outside all flow control, and scaled per material.
            float3 SampleMat(int i, float2 wp, float intensity, float2 dwx, float2 dwy)
            {
                float metres = MAT_METRES[i];
                float2 uv = MatUV(i, wp);
                float2 duvx = dwx / metres;
                float2 duvy = dwy / metres;

                float f = saturate(intensity) * 2.0;
                float a = floor(min(f, 1.999));
                float k = f - a;
                float s0 = MAT_SLICE[i] + a;
                float s1 = min(s0 + 1.0, MAT_SLICE[i] + 2.0);

                // One array since the px flip: the 512 branch retired with its array.
                float3 cA = SAMPLE_TEXTURE2D_ARRAY_GRAD(_DetailArr256, sampler_DetailArr256, uv, s0, duvx, duvy).rgb;
                float3 cB = SAMPLE_TEXTURE2D_ARRAY_GRAD(_DetailArr256, sampler_DetailArr256, uv, s1, duvx, duvy).rgb;
                return lerp(cA, cB, k);
            }

            // The same material RELIT (terrain pass 9): SampleMat's ladder bracket, each step's texel
            // relit by TerrainLight6 from the kit's maps, and the two steps lerped by the same k. The
            // rig returns sRGB bytes; the albedo array samples sRGB into linear, so the bytes are
            // linearised to land where the albedo would. The maps are LOADed whole texels (data, never
            // filtered), so no derivatives are needed inside the branch.
            float3 RelightMat(int i, float2 wp, float intensity, TL6Sky sky, int2 W, float occ, float skyv)
            {
                float2 t = floor(frac(MatUV(i, wp)) * TL6_TILE);
                int2 T = int2((int)t.x, (TL6_TILE - 1) - (int)t.y);   // the array's rows run up the tile, the rig's down

                float f = saturate(intensity) * 2.0;
                float a = floor(min(f, 1.999));
                float k = f - a;
                int s0 = (int)(MAT_SLICE[i] + a);
                int s1 = (int)min(s0 + 1.0, MAT_SLICE[i] + 2.0);

                // One relight body, run once or twice: the relight is large, and two inlined copies
                // would double the program for a step most texels never blend.
                int steps = k > 1e-3 ? 2 : 1;
                float3 c = float3(0, 0, 0);
                [loop]
                for (int st = 0; st < steps; st++)
                {
                    float share = steps == 1 ? 1.0 : (st == 0 ? 1.0 - k : k);
                    c += share * TL6SrgbToLinear(TL6Relight(st == 0 ? s0 : s1, T, W, sky, occ, skyv));
                }
                return c;
            }

            // The relight's sky, once per fragment. The sun is the day/night cycle's while it runs,
            // turned into the rig's floor space (x east, y SOUTH, z up; the elevation as the sine), and
            // the material's _TLSunF while it does not. The cycle's weather rides on the sun's strength:
            // _ShadowStrength is the elevation times what the weather lets through, so over the
            // elevation it is that share. The loop's frame follows the clock at the kit's rate.
            TL6Sky TerrainSky()
            {
                float3 l = _TLSunF.xyz;
                float sunI = _TLSunI;
                if (length(_SunDir.xy) > 1e-4)
                {
                    float e = _SunElevation;
                    float2 g = normalize(_SunDir.xy) * sqrt(saturate(1.0 - e * e));
                    l = float3(g.x, -g.y, e);
                    sunI *= e > 0.0 ? saturate(_ShadowStrength / e) : 1.0;
                }
                int fi = (int)floor(_Time.y * _TLFrameRate) & (TL6_LOOP - 1);
                return TL6MakeSky(l, sunI, _TLSkyI, _TLExpo, _TLWet, _TLRain, _TLFog, _TLWind, _TLGrade > 0.5,
                                  _TLKey.rgb, _TLAmbient.rgb, _TLWash.rgb, _TLFogColour.rgb, _TLSkyColour.rgb,
                                  _TLGradeWeights, _TLSeaDir, fi);
            }

            half4 frag (Varyings i) : SV_Target
            {
                float2 wp = i.worldXY;

                // Quantise the NOISE domain to the art's pixel grid so procedural grain reads as
                // pixel-art texture (the kit itself is sampled Point, already on the grid).
                float ppm = max(_PixelsPerMetre, 1.0);
                float2 wpx = floor(wp * ppm) / ppm;

                // Elevation from the shared painted field — the water shader's mapping, verbatim.
                float2 uv = (wp - _HeightWorldMin.xy) / max(_HeightWorldSize.xy, float2(1e-3, 1e-3));
                float r = SAMPLE_TEXTURE2D(_HeightTex, sampler_HeightTex, uv).r;
                float e = lerp(_HeightMin, _HeightMax, r);

                // The painting's outer hem: fade out below the paint floor, where the ground hands
                // off to the sea's own seabed rendering. Raw elevation — the footprint is a ruling,
                // not scenery (StPetersShoreMap's clamp comment).
                float alpha = smoothstep(_FloorPaint - _EdgeFadeMetres, _FloorPaint, e);
                clip(alpha - 0.003);

                // The meander: same two octaves as the CPU (0.8 m @ 16 m + 0.3 m @ 6 m), clamped up
                // to the paint floor so the wiggle moves ground BETWEEN bands, never out of the painting.
                float wig  = VNoise(wpx / max(_BandWiggleScale, 1e-3), 3.0) * 2.0 - 1.0;
                float wigD = VNoise(wpx / max(_BandDetailScale, 1e-3), 7.0) * 2.0 - 1.0;
                float look = max(_FloorPaint, e + wig * _BandWiggleMetres + wigD * _BandDetailMetres);

                // THE BAR — exempt from the wiggle (a path is signage, not scenery): inside it the
                // ladder reads the RAW elevation, and the sector is forced sheltered.
                float dBar = DistToSegment(wp, _BarFrom.xy, _BarTo.xy);
                float barOn = step(0.01, _BarHalfWidth);
                float barW = barOn * (1.0 - smoothstep(_BarHalfWidth - _BarEdgeBlend,
                                                       _BarHalfWidth + _BarEdgeBlend, dBar));
                float lookB = lerp(look, max(_FloorPaint, e), barW);

                // --- HEIGHT-DERIVED BAND WEIGHTS (the CPU ladders as products of soft gates) --------
                float bg = Band(lookB, _FloorGrass);
                float bm = Band(lookB, _FloorMarram);
                float bs = Band(lookB, _FloorSand);
                float br = Band(lookB, _FloorRipple);
                float bh = Band(lookB, _FloorShingle);

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
                float w[HH_MAT_COUNT];
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
                {
                    float keep = 1.0 - spineW;
                    for (int bi = 0; bi < HH_MAT_COUNT; bi++) w[bi] *= keep;
                    w[3] += spineW;
                }

                // --- PAINTED OVERRIDES: twenty channels, value = weight AND ladder intensity -------
                float4 pA = SAMPLE_TEXTURE2D(_SplatA, sampler_SplatA, uv);
                float4 pB = SAMPLE_TEXTURE2D(_SplatB, sampler_SplatB, uv);
                float4 pC = SAMPLE_TEXTURE2D(_SplatC, sampler_SplatC, uv);
                float4 pD = SAMPLE_TEXTURE2D(_SplatD, sampler_SplatD, uv);
                float4 pE = SAMPLE_TEXTURE2D(_SplatE, sampler_SplatE, uv);
                float p[HH_MAT_COUNT];
                p[0]  = pA.r; p[1]  = pA.g; p[2]  = pA.b; p[3]  = pA.a;
                p[4]  = pB.r; p[5]  = pB.g; p[6]  = pB.b; p[7]  = pB.a;
                p[8]  = pC.r; p[9]  = pC.g; p[10] = pC.b; p[11] = pC.a;
                p[12] = pD.r; p[13] = pD.g; p[14] = pD.b; p[15] = pD.a;
                p[16] = pE.r; p[17] = pE.g; p[18] = pE.b; p[19] = pE.a;

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

                float paintSum = p[0]  + p[1]  + p[2]  + p[3]  + p[4]  + p[5]  + p[6]
                               + p[7]  + p[8]  + p[9]  + p[10] + p[11] + p[12] + p[13]
                               + p[14] + p[15] + p[16] + p[17] + p[18] + p[19];
                float paintTotal = saturate(paintSum);
                // The painted share (paintTotal) is distributed by each channel's fraction of the
                // whole (p / paintSum) — in BOTH regimes, so the weights below always sum to 1.
                // Normalising only above 1 would leave partial paint summing to
                // 1 - paintSum + paintSum^2: every feathered stroke edge up to 25% darker than
                // either source material.
                float norm = 1.0 / max(paintSum, 1e-4);

                float intensity[HH_MAT_COUNT];
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

                // --- SHADE ------------------------------------------------------------------------
                float3 col = float3(0, 0, 0);
                if (_DetailLoaded > 0.5)
                {
                    // The kit path: sample only the materials actually present (paint is spatially
                    // coherent, so the branch stays cheap — most pixels carry one or two materials).
                    // Derivatives once, outside all flow control (see SampleMat).
                    float2 dwx = ddx(wp);
                    float2 dwy = ddy(wp);
                    [branch]
                    if (_RelightLoaded > 0.5)
                    {
                        // Terrain pass 9, the maps bound: the relight's sky, the texel on the world's
                        // pixel grid (the rig's X east and Y SOUTH, where it anchors its noise and
                        // hashes) and the two per-texel inputs, once per fragment. Then every material
                        // present, relit when its tiles have maps and from the albedo when not (the
                        // lawn's). A real loop: unrolled, the relight would be inlined twenty times.
                        TL6Sky sky = TerrainSky();
                        int2 W = int2((int)floor((wp.x - _TLOrigin.x) * ppm), (int)floor((_TLOrigin.y - wp.y) * ppm));
                        float2 os = SAMPLE_TEXTURE2D_LOD(_TLOccSkyv, sampler_TLOccSkyv, uv, 0).rg;
                        float occ = os.r, skyv = 1.0 - os.g;
                        // The loop indexes its arrays at run time, which puts them in indexable memory:
                        // copied HERE, so that cost stays inside this branch and the albedo path below
                        // keeps w and intensity in registers, as before pass 9.
                        float rw[HH_MAT_COUNT], ri[HH_MAT_COUNT];
                        for (int ci = 0; ci < HH_MAT_COUNT; ci++) { rw[ci] = w[ci]; ri[ci] = intensity[ci]; }
                        [loop]
                        for (int mi = 0; mi < HH_MAT_COUNT; mi++)
                        {
                            [branch]
                            if (rw[mi] > 0.004)
                            {
                                [branch]
                                if (TL6Params((int)MAT_SLICE[mi]).w > 0.5)
                                    col += rw[mi] * RelightMat(mi, wp, ri[mi], sky, W, occ, skyv);
                                else
                                    col += rw[mi] * SampleMat(mi, wp, ri[mi], dwx, dwy);
                            }
                        }
                    }
                    else
                    {
                        // The kit's albedo alone, as before pass 9 (and so no relight map is ever
                        // loaded while _RelightLoaded is 0).
                        for (int si = 0; si < HH_MAT_COUNT; si++)
                        {
                            [branch]
                            if (w[si] > 0.004)
                                col += w[si] * SampleMat(si, wp, intensity[si], dwx, dwy);
                        }
                    }
                }
                else
                {
                    // PR-1 fallback: flat two-tone colours so the ground renders before the first
                    // array build (fresh checkout, or the guard test's bare material).
                    float grain = saturate(VNoise(wpx / max(_GrainScale, 1e-3), 11.0) * 0.65
                                         + VNoise(wpx / max(_GrainScale * 3.7, 1e-3), 23.0) * 0.35);
                    col += w[0] * lerp(_GrassColA.rgb,   _GrassColB.rgb,   grain);
                    col += w[1] * lerp(_MarramColA.rgb,  _MarramColB.rgb,  grain);
                    col += w[2] * lerp(_SandColA.rgb,    _SandColB.rgb,    grain);
                    col += w[3] * lerp(_ShingleColA.rgb, _ShingleColB.rgb, grain);
                    col += w[4] * lerp(_RippleColA.rgb,  _RippleColB.rgb,  grain);
                    col += w[5] * lerp(_ShelfColA.rgb,   _ShelfColB.rgb,   grain);
                    // The twelve paint-only materials borrow their nearest band cousin's colours —
                    // the kit's own substrate pairings (README §5): foreshore is on the Island sand
                    // ramp, talus/ledge/rockweed on the red-bed rock ramps, and the four beds sit on
                    // the anoxic bed mud (mussel/oyster), the silted sand ramp (eelgrass) and the red
                    // cobble (irish moss). Approximate by construction — this path only renders on a
                    // fresh checkout, before the first array build.
                    col += w[6]  * lerp(_RippleColA.rgb,  _RippleColB.rgb,  grain);   // silt
                    col += w[7]  * lerp(_ShelfColA.rgb,   _SandColB.rgb,    grain);   // dirt
                    col += w[8]  * lerp(_GrassColA.rgb,   _MarramColB.rgb,  grain);   // marsh
                    col += w[9]  * lerp(_MarramColA.rgb,  _GrassColB.rgb,   grain);   // sedge
                    col += w[10] * lerp(_SandColA.rgb,    _RippleColB.rgb,  grain);   // foreshore
                    col += w[11] * lerp(_ShingleColA.rgb, _ShelfColB.rgb,   grain);   // talus
                    col += w[12] * lerp(_ShelfColA.rgb,   _ShingleColB.rgb, grain);   // ledge
                    col += w[13] * lerp(_ShelfColA.rgb,   _GrassColB.rgb,   grain);   // rockweed
                    col += w[14] * lerp(_ShelfColA.rgb,   _ShelfColB.rgb,   grain);   // musselbed
                    col += w[15] * lerp(_ShelfColA.rgb,   _ShingleColB.rgb, grain);   // oysterreef
                    col += w[16] * lerp(_MarramColA.rgb,  _GrassColB.rgb,   grain);   // eelgrass
                    col += w[17] * lerp(_RippleColA.rgb,  _ShelfColB.rgb,   grain);   // irishmoss
                    col += w[19] * lerp(_ShelfColA.rgb,   _SandColB.rgb,    grain);   // mud, on dirt's pair
                }

                // Macro variation: tens-of-metres tint drift that kills any large-scale flatness.
                // The kit flattens each tile's low-frequency mean on purpose (README §4) — this
                // layer is the shader's half of that bargain.
                float m = Fbm3(wp / max(_MacroScale, 1e-3));
                col *= lerp(1.0, _MacroTint.rgb, saturate(m) * _MacroStrength);

                // The wet band: ground just above the live waterline darkens — the tide line
                // breathes with the SAME _WaterLevel the sea clips by. (Live water/wet effects are
                // deliberately NOT in the kit's albedo — README §1.)
                float wet = 1.0 - smoothstep(_WaterLevel, _WaterLevel + max(_WetBandMetres, 1e-3), e);
                col = lerp(col, col * _WetTint.rgb, wet * _WetStrength);

                return half4(col, alpha);
            }
            ENDHLSL
        }
    }
}
