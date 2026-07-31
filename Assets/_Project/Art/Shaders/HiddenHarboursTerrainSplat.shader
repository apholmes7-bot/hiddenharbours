// HiddenHarboursTerrainSplat.shader — the ground as a painted FIELD, not a grid of tiles (ADR 0028).
//
// One full-region quad replaces the per-cell ground/fringe tilemaps. The fragment reads the SAME
// painted height data the water shader and the walk gate read (the _HeightTex vocabulary of
// HiddenHarboursWater.shader, verbatim), classifies elevation into the StPetersShoreMap band
// ladder with SOFT metre-scale edges, and colours each band with pixel-grid grain plus a
// low-frequency macro tint. World-space value noise is aperiodic, so the repetition class of
// defect that motivated the ADR cannot occur by construction.
//
// The band constants are NOT owned here: the builder pushes StPetersShoreMap's numbers through
// TerrainSplatSurface at build time, and TerrainSplatBandPinTests holds this shader's DEFAULTS to
// the same constants so the two implementations cannot drift silently. The meander noise matches
// the CPU classifier's PARAMETERS (0.8 m @ 16 m + 0.3 m @ 6 m, feathered weather sector, the
// sandbar exempt — signage, not scenery) but is intentionally not bit-identical: the CPU remains
// authoritative for gameplay and rock/contact placement; this is the look.
//
// Sorts through a SortingGroup (mesh renderers do not compete with sprites on their own — the
// ADR 0023 pattern) BELOW the Sea plane, so the ADR 0012 tide reveal (the water clipping itself
// transparent over dry ground) keeps working unchanged. The wet band above the live waterline
// reads _WaterLevel — the same number the sea clips by.
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

        [Header(Material colours. A is base and B is grain)]
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

            CBUFFER_START(UnityPerMaterial)
                float  _HeightMin, _HeightMax;
                float4 _HeightWorldMin, _HeightWorldSize;
                float  _WaterLevel;

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

            half4 frag (Varyings i) : SV_Target
            {
                float2 wp = i.worldXY;

                // Quantise the NOISE domain to the art's pixel grid so grain reads as pixel-art
                // texture, not smooth gradient mist against 32 px/m sprites (the water's Pixelize idiom).
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

                // Two-tone grain per material, one shared field (two octaves of pixel-grid noise).
                float grain = saturate(VNoise(wpx / max(_GrainScale, 1e-3), 11.0) * 0.65
                                     + VNoise(wpx / max(_GrainScale * 3.7, 1e-3), 23.0) * 0.35);
                float3 grass   = lerp(_GrassColA.rgb,   _GrassColB.rgb,   grain);
                float3 marram  = lerp(_MarramColA.rgb,  _MarramColB.rgb,  grain);
                float3 sand    = lerp(_SandColA.rgb,    _SandColB.rgb,    grain);
                float3 shingle = lerp(_ShingleColA.rgb, _ShingleColB.rgb, grain);
                float3 ripple  = lerp(_RippleColA.rgb,  _RippleColB.rgb,  grain);
                float3 shelf   = lerp(_ShelfColA.rgb,   _ShelfColB.rgb,   grain);

                // The sheltered ladder (west/north): shelf, ripple, sand, marram, grass.
                float3 colS = shelf;
                colS = lerp(colS, ripple, Band(lookB, _FloorRipple));
                colS = lerp(colS, sand,   Band(lookB, _FloorSand));
                colS = lerp(colS, marram, Band(lookB, _FloorMarram));
                colS = lerp(colS, grass,  Band(lookB, _FloorGrass));

                // The weather ladder (south/east): shelf, shingle, grass — no dune, no beach.
                float3 colW = shelf;
                colW = lerp(colW, shingle, Band(lookB, _FloorShingle));
                colW = lerp(colW, grass,   Band(lookB, _FloorGrass));

                // Which coast: bearing against the weather facing, aspect-normalised about the island
                // centre, feathered by the same meander (StPetersShoreMap.IsWeatherCoast's shape).
                float2 d = wp - _IslandCenter.xy;
                d.y *= _IslandAspect;
                float2 dn = normalize(d + float2(1e-4, 0.0));
                float bearing = dot(dn, normalize(_WeatherFacing.xy));
                float thresh = wig * _SectorFeather;
                float ws = smoothstep(thresh - _SectorBlend, thresh + _SectorBlend, bearing);
                ws *= 1.0 - barW;
                float3 col = lerp(colS, colW, ws);

                // The bar's cobble spine — the walking line, raw elevation gated like the CPU.
                float spineW = barOn
                    * (1.0 - smoothstep(_BarSpineHalfWidth - 1.0, _BarSpineHalfWidth + 1.0, dBar))
                    * smoothstep(_BarSpineFloor - _BandBlendMetres, _BarSpineFloor + _BandBlendMetres, e)
                    * barW;
                col = lerp(col, shingle, spineW);

                // Macro variation: tens-of-metres tint drift that kills any large-scale flatness.
                float m = Fbm3(wp / max(_MacroScale, 1e-3));
                col *= lerp(1.0, _MacroTint.rgb, saturate(m) * _MacroStrength);

                // The wet band: ground just above the live waterline darkens — the tide line
                // breathes with the SAME _WaterLevel the sea clips by.
                float wet = 1.0 - smoothstep(_WaterLevel, _WaterLevel + max(_WetBandMetres, 1e-3), e);
                col = lerp(col, col * _WetTint.rgb, wet * _WetStrength);

                return half4(col, alpha);
            }
            ENDHLSL
        }
    }
}
