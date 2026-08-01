// HiddenHarboursTerrainSplat.shader — the ground as a painted FIELD, not a grid of tiles (ADR 0028).
//
// One full-region quad replaces the per-cell ground/fringe tilemaps. The fragment reads the SAME
// painted height data the water shader and the walk gate read (the _HeightTex vocabulary of
// HiddenHarboursWater.shader, verbatim), classifies elevation into the StPetersShoreMap band
// ladder with SOFT metre-scale edges, and shades each material from the terrain material kit
// (docs/art/rigs/terrain — 14 plan-projection materials x 3 intensity steps, packed into two
// Texture2DArrays by TerrainTexArrayBuilder). World-space sampling with per-cell hashed offsets
// on the kit's offset-allowed materials means repetition cannot align by construction.
//
// PAINTED OVERRIDES (PR 2): four splat maps carry fourteen 0..1 channels, one per material. A
// channel's value is BOTH the blend weight against the height-derived bands AND the position on
// that material's intensity ladder (_Lo -> base -> _Hi; the kit designs low intensity to READ
// sparse — README §2 — so one number does both jobs honestly). Unpainted ground renders the
// height bands at the ladder's base step. The kit's two FACE-projection materials (Sandstone,
// Bank — cliff faces) are imported but deliberately not wired here; they await cliff geometry.
//
// KIT V2 added four shoreline materials (Foreshore, Talus, Ledge, Rockweed) at indices 10..13.
// Ten channels no longer fit three RGBA maps, so _SplatD joined A/B/C; D.b and D.a are the two
// slots still free. The kit's four EDGE STRIPS (the sod lip, scarp, wrack line, weed line) are
// imported under Terrain/Edges but not sampled here — they are decals laid along the shoreline
// spline by signed distance, which is a different addressing scheme than this world-XZ tiling
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

        [Header(Detail texture arrays. Built by TerrainTexArrayBuilder)]
        [NoScaleOffset] _DetailArr256 ("Detail array 256 class", 2DArray) = "" {}
        [NoScaleOffset] _DetailArr512 ("Detail array 512 class", 2DArray) = "" {}
        _DetailLoaded ("Detail arrays loaded", Float) = 0.0
        _DetailOffsetCellMetres ("Hashed offset cell in metres", Float) = 32.0

        [Header(Painted splat maps. Fourteen channels across four textures)]
        [NoScaleOffset] _SplatA ("Splat A. Grass Marram Sand Shingle", 2D) = "black" {}
        [NoScaleOffset] _SplatB ("Splat B. Ripple Shelf Silt Dirt", 2D) = "black" {}
        [NoScaleOffset] _SplatC ("Splat C. Marsh Sedge Foreshore Talus", 2D) = "black" {}
        [NoScaleOffset] _SplatD ("Splat D. Ledge Rockweed. b and a free", 2D) = "black" {}

        [Header(Band floors in metres. Builder pushes StPetersShoreMap)]
        _FloorPaint   ("Paint floor", Float) = -2.6
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
        _BarSpineFloor    ("Spine floor in metres", Float) = 0.6
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
            TEXTURE2D_ARRAY(_DetailArr256); SAMPLER(sampler_DetailArr256);
            TEXTURE2D_ARRAY(_DetailArr512); SAMPLER(sampler_DetailArr512);

            CBUFFER_START(UnityPerMaterial)
                float  _HeightMin, _HeightMax;
                float4 _HeightWorldMin, _HeightWorldSize;
                float  _WaterLevel;

                float _DetailLoaded, _DetailOffsetCellMetres;

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

            // =========================================================================================
            //  THE MATERIAL TABLE — canonical order 0..13. Mirrored by TerrainTexArrayBuilder (C#) and
            //  by the splat channel packing (A.rgba, B.rgba, C.rgba, D.rg). A pin test holds all three
            //  together. APPEND ONLY: committed splat PNGs and this unpack agree on index meaning.
            //    0 grass   1 marram   2 sand       3 shingle   4 ripple    5 shelf   6 silt
            //    7 dirt    8 marsh    9 sedge     10 foreshore 11 talus   12 ledge  13 rockweed
            //  MAT_ARRAY: 0 = the 256 array (8 m tiles), 1 = the 512 array (16 m tiles).
            //  MAT_SLICE: base slice (the _Lo step; +1 base, +2 _Hi — the kit ladder, README §2).
            //  MAT_OFFSET: hashed per-cell UV offset allowed (README §4: NEVER on a directional
            //  material — an offset slices a ripple train, a wind-combed stand, a bedding plane or a
            //  lie of fronds apart at the cell border. That list is ripple, marram, foreshore, ledge
            //  and rockweed; all five carry enough low-frequency variation to hide the repeat alone).
            // =========================================================================================
            static const float MAT_ARRAY[14]  = { 0, 0, 0, 1, 1, 0, 1, 0, 0, 0, 1, 1, 0, 0 };
            static const float MAT_SLICE[14]  = { 0, 3, 6, 0, 3, 9, 6, 12, 15, 18, 9, 12, 21, 24 };
            static const float MAT_METRES[14] = { 8, 8, 8, 16, 16, 8, 16, 8, 8, 8, 16, 16, 8, 8 };
            static const float MAT_OFFSET[14] = { 1, 0, 1, 1, 0, 1, 1, 1, 1, 1, 0, 1, 0, 0 };

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

            // One material from the kit at a given intensity: bracket the ladder (README §2's HLSL,
            // verbatim in spirit) and lerp two neighbouring steps of the SAME material. The hashed
            // whole-material offset applies per _DetailOffsetCellMetres cell, all steps together —
            // offsetting steps apart would cross-fade misaligned images (README §4).
            //
            // Takes the world-position screen derivatives EXPLICITLY (SampleGrad): this is called
            // inside a divergent [branch], where implicit-gradient sampling is illegal — the
            // derivatives are computed once, outside all flow control, and scaled per material.
            float3 SampleMat(int i, float2 wp, float intensity, float2 dwx, float2 dwy)
            {
                float metres = MAT_METRES[i];
                float2 uv = wp / metres;
                if (MAT_OFFSET[i] > 0.5)
                {
                    float2 cell = floor(wp / max(_DetailOffsetCellMetres, 1.0));
                    uv += float2(Hash21(cell), Hash21(cell + 17.0));
                }
                float2 duvx = dwx / metres;
                float2 duvy = dwy / metres;

                float f = saturate(intensity) * 2.0;
                float a = floor(min(f, 1.999));
                float k = f - a;
                float s0 = MAT_SLICE[i] + a;
                float s1 = min(s0 + 1.0, MAT_SLICE[i] + 2.0);

                float3 cA, cB;
                if (MAT_ARRAY[i] > 0.5)
                {
                    cA = SAMPLE_TEXTURE2D_ARRAY_GRAD(_DetailArr512, sampler_DetailArr512, uv, s0, duvx, duvy).rgb;
                    cB = SAMPLE_TEXTURE2D_ARRAY_GRAD(_DetailArr512, sampler_DetailArr512, uv, s1, duvx, duvy).rgb;
                }
                else
                {
                    cA = SAMPLE_TEXTURE2D_ARRAY_GRAD(_DetailArr256, sampler_DetailArr256, uv, s0, duvx, duvy).rgb;
                    cB = SAMPLE_TEXTURE2D_ARRAY_GRAD(_DetailArr256, sampler_DetailArr256, uv, s1, duvx, duvy).rgb;
                }
                return lerp(cA, cB, k);
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
                float w[14];
                w[0] = sGrass;                                   // grass — same cap both coasts
                w[1] = sMarram * (1.0 - ws);
                w[2] = sSand   * (1.0 - ws);
                w[3] = wShingle * ws;
                w[4] = sRipple * (1.0 - ws);
                w[5] = lerp(sShelf, wShelf, ws);
                w[6] = 0.0; w[7] = 0.0; w[8] = 0.0; w[9] = 0.0;
                w[10] = 0.0; w[11] = 0.0; w[12] = 0.0; w[13] = 0.0;   // paint-only, like 6..9
                {
                    float keep = 1.0 - spineW;
                    for (int bi = 0; bi < 14; bi++) w[bi] *= keep;
                    w[3] += spineW;
                }

                // --- PAINTED OVERRIDES: fourteen channels, value = weight AND ladder intensity ------
                float4 pA = SAMPLE_TEXTURE2D(_SplatA, sampler_SplatA, uv);
                float4 pB = SAMPLE_TEXTURE2D(_SplatB, sampler_SplatB, uv);
                float4 pC = SAMPLE_TEXTURE2D(_SplatC, sampler_SplatC, uv);
                float4 pD = SAMPLE_TEXTURE2D(_SplatD, sampler_SplatD, uv);
                float p[14];
                p[0]  = pA.r; p[1]  = pA.g; p[2]  = pA.b; p[3]  = pA.a;
                p[4]  = pB.r; p[5]  = pB.g; p[6]  = pB.b; p[7]  = pB.a;
                p[8]  = pC.r; p[9]  = pC.g; p[10] = pC.b; p[11] = pC.a;
                p[12] = pD.r; p[13] = pD.g;   // D.b / D.a: the two slots still free

                float paintSum = p[0] + p[1] + p[2]  + p[3]  + p[4]  + p[5]  + p[6]
                               + p[7] + p[8] + p[9]  + p[10] + p[11] + p[12] + p[13];
                float paintTotal = saturate(paintSum);
                // The painted share (paintTotal) is distributed by each channel's fraction of the
                // whole (p / paintSum) — in BOTH regimes, so the weights below always sum to 1.
                // Normalising only above 1 would leave partial paint summing to
                // 1 - paintSum + paintSum^2: every feathered stroke edge up to 25% darker than
                // either source material.
                float norm = 1.0 / max(paintSum, 1e-4);

                float intensity[14];
                {
                    float baseKeep = 1.0 - paintTotal;
                    for (int mi = 0; mi < 14; mi++)
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
                    for (int si = 0; si < 14; si++)
                    {
                        [branch]
                        if (w[si] > 0.004)
                            col += w[si] * SampleMat(si, wp, intensity[si], dwx, dwy);
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
                    // The eight paint-only materials borrow their nearest band cousin's colours —
                    // the kit's own substrate pairings (README §5): foreshore is on the Island sand
                    // ramp, talus/ledge/rockweed on the red-bed rock ramps.
                    col += w[6]  * lerp(_RippleColA.rgb,  _RippleColB.rgb,  grain);   // silt
                    col += w[7]  * lerp(_ShelfColA.rgb,   _SandColB.rgb,    grain);   // dirt
                    col += w[8]  * lerp(_GrassColA.rgb,   _MarramColB.rgb,  grain);   // marsh
                    col += w[9]  * lerp(_MarramColA.rgb,  _GrassColB.rgb,   grain);   // sedge
                    col += w[10] * lerp(_SandColA.rgb,    _RippleColB.rgb,  grain);   // foreshore
                    col += w[11] * lerp(_ShingleColA.rgb, _ShelfColB.rgb,   grain);   // talus
                    col += w[12] * lerp(_ShelfColA.rgb,   _ShingleColB.rgb, grain);   // ledge
                    col += w[13] * lerp(_ShelfColA.rgb,   _GrassColB.rgb,   grain);   // rockweed
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
