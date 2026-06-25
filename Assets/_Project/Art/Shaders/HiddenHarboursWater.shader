// HiddenHarboursWater.shader — the layered, SIM-DRIVEN water surface (ADR 0010 / design/water-rendering.md).
//
// A custom URP 2D unlit ShaderLab/HLSL shader (NOT a Shader Graph — authored as text so it builds headless).
// It draws the hero water look as PIXEL ART (every layer pixelizes world coords to the PPU grid) and reads the
// SAME deterministic water level + seabed height the gameplay reads, so the visible waterline == the physical
// waterline (the P1 integrity rule). The runtime feeds it the sim each throttled tick (HiddenHarbours.Art.WaterSurface):
//
//   _WaterLevel   <- IEnvironmentService.WaterLevelAt(t)   (metres above chart datum; the moving shoreline)
//   _FlowDir/_Flow <- EnvironmentSample.CurrentVector       (tidal set -> surface scroll dir/speed)
//   _Roughness    <- EnvironmentSample.WindVector           (wind -> surface roughness / whitecaps)
//   _Chop         <- EnvironmentSample.SeaState             (sea-state -> swell amplitude / choppiness)
//   _HeightTex    <- ITidalTerrain.ElevationAt baked over the plane (the first-pass DEPTH source)
//
// EVERY colour / scroll-speed / foam-width / threshold is a material property so the owner art-directs in the
// Inspector with no graph editing (CLAUDE.md rule 6). Visual-only: drives no sim, saves nothing (rule 5).
Shader "HiddenHarbours/Water"
{
    Properties
    {
        [Header(Depth gradient (layer 1))]
        _ShallowColor   ("Shallow water", Color)        = (0.35, 0.62, 0.66, 0.95)
        _DeepColor      ("Deep water", Color)           = (0.06, 0.16, 0.24, 1.0)
        _ShallowDepth   ("Shallow depth (m)", Float)    = 0.15
        _DeepDepth      ("Deep depth (m)", Float)       = 3.0
        _DepthBands     ("Depth posterize bands (0=off)", Float) = 5

        [Header(Pixelization (all layers))]
        _PixelsPerUnit  ("Pixels per unit", Float)      = 32

        [Header(Surface distortion (layer 2))]
        _NoiseScale     ("Surface noise scale", Float)  = 0.35
        _Flow           ("Flow speed (scroll)", Float)  = 0.06
        _FlowDir        ("Flow direction (xy)", Vector) = (1, 0, 0, 0)
        _Chop           ("Choppiness (0..1)", Range(0,1)) = 0.25
        _SurfaceTint    ("Surface tint strength", Range(0,1)) = 0.18

        [Header(Foam fringe (layer 3))]
        _FoamColor      ("Foam color", Color)           = (0.92, 0.96, 0.98, 1.0)
        _FoamWidth      ("Foam band width (m)", Float)  = 0.45
        _FoamSoftness   ("Foam edge softness (m)", Float) = 0.18
        _FoamNoise      ("Foam churn noise scale", Float) = 1.2
        _Roughness      ("Roughness / whitecaps (0..1)", Range(0,1)) = 0.2

        [Header(Specular glints (layer 4))]
        _SpecColor      ("Specular color", Color)       = (1.0, 0.98, 0.86, 1.0)
        _SpecAmount     ("Specular amount (0..1)", Range(0,1)) = 0.35
        _SpecSharpness  ("Specular sharpness", Float)   = 18
        _LightDir       ("Implied light dir (xy)", Vector) = (-0.6, 0.8, 0, 0)

        [Header(Caustics (layer 5  shallows))]
        _CausticColor   ("Caustic color", Color)        = (0.75, 0.95, 0.92, 1.0)
        _CausticAmount  ("Caustic amount (0..1)", Range(0,1)) = 0.3
        _CausticScale   ("Caustic scale", Float)        = 0.9
        _CausticDepth   ("Caustic max depth (m)", Float) = 1.4

        [Header(Depth source)]
        _WaterLevel     ("Water level (m, sim-driven)", Float) = 0.0
        [NoScaleOffset] _HeightTex ("Seabed height map (R=elevation)", 2D) = "black" {}
        _HeightMin      ("Height map min (m)", Float)   = -4.0
        _HeightMax      ("Height map max (m)", Float)   = 6.0
        _HeightWorldMin ("Height map world min (xy)", Vector) = (-80, -60, 0, 0)
        _HeightWorldSize("Height map world size (xy)", Vector) = (160, 120, 0, 0)
        [Toggle(_USE_HEIGHTTEX)] _UseHeightTex ("Use baked height map", Float) = 0

        // ---------------------------------------------------------------------------------------------
        // OWNER-PAINTED TEXTURE SLOTS (optional art-direction over the procedural look).
        // Every slot is OFF by default (its _Use* toggle = 0), so an EMPTY material renders the shipped
        // first-pass PROCEDURAL look unchanged. Assign a texture AND tick its toggle to blend with /
        // override the matching procedural layer. Every slot samples on the PIXEL grid (PPU-snapped
        // world coords) with Repeat wrap + POINT (no-AA) filtering — set the texture import settings to
        // match (Filter Mode = Point, Wrap Mode = Repeat). Each carries a [0..1] strength/blend so the
        // owner dials procedural<->painted. Spec + suggested dims: design/water-rendering.md
        // "Owner-painted texture slots".
        [Header(Painted textures   optional   blend or override procedural)]
        _PaintScale      ("Painted texture scale (tiles/unit)", Float) = 0.25

        [Toggle(_USE_SURFACETEX)] _UseSurfaceTex ("Use surface ripple texture", Float) = 0
        [NoScaleOffset] _SurfaceTex ("Surface ripple/detail (grayscale, seamless ~64)", 2D) = "gray" {}
        _SurfaceTexStrength ("Surface tex blend (0=proc, 1=painted)", Range(0,1)) = 1.0

        [Toggle(_USE_FOAMTEX)] _UseFoamTex ("Use foam texture", Float) = 0
        [NoScaleOffset] _FoamTex ("Foam pattern (white-on-transparent, seamless ~64)", 2D) = "white" {}
        _FoamTexStrength ("Foam tex blend (0=proc churn, 1=painted)", Range(0,1)) = 1.0

        [Toggle(_USE_CAUSTICTEX)] _UseCausticTex ("Use caustic texture", Float) = 0
        [NoScaleOffset] _CausticTex ("Caustics (grayscale, seamless ~64)", 2D) = "black" {}
        _CausticTexStrength ("Caustic tex blend (0=proc, 1=painted)", Range(0,1)) = 1.0

        [Toggle(_USE_SPARKLETEX)] _UseSparkleTex ("Use sparkle texture", Float) = 0
        [NoScaleOffset] _SparkleTex ("Specular glint pattern (white-on-black, seamless ~32)", 2D) = "black" {}
        _SparkleTexStrength ("Sparkle tex blend (0=proc, 1=painted)", Range(0,1)) = 1.0
        _SparkleTexScale ("Sparkle texture scale (tiles/unit)", Float) = 0.5

        [Toggle(_USE_DEPTHRAMP)] _UseDepthRamp ("Use depth colour ramp", Float) = 0
        [NoScaleOffset] _DepthRamp ("Depth ramp (1D, shallow u=0 -> deep u=1)", 2D) = "white" {}

        [Toggle(_USE_WHITECAPTEX)] _UseWhitecapTex ("Use whitecap texture", Float) = 0
        [NoScaleOffset] _WhitecapTex ("Whitecap pattern (white-on-transparent, seamless ~64)", 2D) = "white" {}
        _WhitecapTexStrength ("Whitecap tex blend (0=proc, 1=painted)", Range(0,1)) = 1.0
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        Pass
        {
            // Unlit transparent: the 2D renderer draws this; we light it ourselves (one implied sun, ADR 0006).
            Tags { "LightMode" = "Universal2D" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            // multi_compile (not shader_feature) so the height-map branch is ALWAYS compiled — WaterSurface
            // toggles _USE_HEIGHTTEX at runtime after baking, and a shader_feature variant absent from the
            // build would silently fall back to the off (uniform-deep) path.
            #pragma multi_compile_local _ _USE_HEIGHTTEX
            // Painted-texture branches: shader_feature (not multi_compile) — these toggles are baked into
            // the MATERIAL by the owner in the Inspector (NOT flipped by a runtime script, unlike
            // _USE_HEIGHTTEX), so only the combinations a shipped material actually uses need to compile.
            // shader_feature keeps the variant count minimal AND preserves the material's chosen keywords;
            // every branch is still syntax-checked by the importer. One _local keyword per slot.
            #pragma shader_feature_local _ _USE_SURFACETEX
            #pragma shader_feature_local _ _USE_FOAMTEX
            #pragma shader_feature_local _ _USE_CAUSTICTEX
            #pragma shader_feature_local _ _USE_SPARKLETEX
            #pragma shader_feature_local _ _USE_DEPTHRAMP
            #pragma shader_feature_local _ _USE_WHITECAPTEX
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float3 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float2 worldXY    : TEXCOORD1;   // world-space XY (metres; 1 unit = 1 m at PPU 32)
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            TEXTURE2D(_HeightTex);
            SAMPLER(sampler_HeightTex);

            // Owner-painted slots. Each uses its own sampler so the texture's import settings (author
            // them Point filter + Repeat wrap for the pixel look) drive sampling — no forced inline state.
            TEXTURE2D(_SurfaceTex);   SAMPLER(sampler_SurfaceTex);
            TEXTURE2D(_FoamTex);      SAMPLER(sampler_FoamTex);
            TEXTURE2D(_CausticTex);   SAMPLER(sampler_CausticTex);
            TEXTURE2D(_SparkleTex);   SAMPLER(sampler_SparkleTex);
            TEXTURE2D(_DepthRamp);    SAMPLER(sampler_DepthRamp);
            TEXTURE2D(_WhitecapTex);  SAMPLER(sampler_WhitecapTex);

            // SRP-batcher friendly: every per-material property in one CBUFFER (the runtime sets these via a
            // MaterialPropertyBlock; the sim-driven ones change on the slow tick, not per frame).
            CBUFFER_START(UnityPerMaterial)
                float4 _ShallowColor;
                float4 _DeepColor;
                float  _ShallowDepth;
                float  _DeepDepth;
                float  _DepthBands;
                float  _PixelsPerUnit;
                float  _NoiseScale;
                float  _Flow;
                float4 _FlowDir;
                float  _Chop;
                float  _SurfaceTint;
                float4 _FoamColor;
                float  _FoamWidth;
                float  _FoamSoftness;
                float  _FoamNoise;
                float  _Roughness;
                float4 _SpecColor;
                float  _SpecAmount;
                float  _SpecSharpness;
                float4 _LightDir;
                float4 _CausticColor;
                float  _CausticAmount;
                float  _CausticScale;
                float  _CausticDepth;
                float  _WaterLevel;
                float  _HeightMin;
                float  _HeightMax;
                float4 _HeightWorldMin;
                float4 _HeightWorldSize;
                // Painted-texture blend strengths + tiling (the _Use* toggle floats live only as keyword
                // drivers — like _UseHeightTex — and are intentionally NOT in the CBUFFER).
                float  _PaintScale;
                float  _SurfaceTexStrength;
                float  _FoamTexStrength;
                float  _CausticTexStrength;
                float  _SparkleTexStrength;
                float  _SparkleTexScale;
                float  _WhitecapTexStrength;
            CBUFFER_END

            // ---- pixelize: snap a world coord to the PPU grid so every layer reads as pixel art (ADR 0010 (2)) ----
            float2 Pixelize(float2 p)
            {
                float ppu = max(_PixelsPerUnit, 1.0);
                return floor(p * ppu) / ppu;
            }

            // ---- cheap value noise (hash-lattice, smooth interpolation). Deterministic, no textures. ----
            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 345.45));
                p += dot(p, p + 34.345);
                return frac(p.x * p.y);
            }

            float ValueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);   // smoothstep weights
                float a = Hash21(i + float2(0, 0));
                float b = Hash21(i + float2(1, 0));
                float c = Hash21(i + float2(0, 1));
                float d = Hash21(i + float2(1, 1));
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            // Two-octave scrolling noise (richer than one), each octave pixelized so the swell snaps to the grid.
            float SurfaceNoise(float2 worldXY, float t)
            {
                float2 dir = normalize(_FlowDir.xy + float2(1e-4, 0));
                float2 scroll = dir * (_Flow * t);
                float2 p1 = Pixelize((worldXY + scroll) * _NoiseScale);
                float2 p2 = Pixelize((worldXY - scroll * 0.6) * _NoiseScale * 2.0);
                return ValueNoise(p1) * 0.65 + ValueNoise(p2) * 0.35;
            }

            // Painted-texture UV: pixelize the world position to the PPU grid, then scale to tiles/unit.
            // Repeat wrap (set in the texture import) makes a seamless ~64px tile cover the whole plane;
            // the pixelize keeps the sampled coord on the grid so painted detail reads as pixel art too.
            // `scroll` lets a layer drift the pattern with the current (pass float2(0,0) for a static tile).
            float2 PaintUV(float2 worldXY, float scale, float2 scroll)
            {
                return Pixelize(worldXY + scroll) * max(scale, 1e-4);
            }

            // Sample the seabed elevation (metres above datum) at a world position. With the baked height map
            // the depth gradient + foam band match TidalTerrain exactly; without it, the plane reads as uniform
            // deep water (a safe fallback before a region bakes its height).
            float SeabedElevation(float2 worldXY)
            {
            #if defined(_USE_HEIGHTTEX)
                float2 uv = (worldXY - _HeightWorldMin.xy) / max(_HeightWorldSize.xy, float2(1e-3, 1e-3));
                float r = SAMPLE_TEXTURE2D(_HeightTex, sampler_HeightTex, uv).r;
                return lerp(_HeightMin, _HeightMax, r);
            #else
                return _HeightMin;   // no height map => everywhere deep (uniform tint, no false shoreline)
            #endif
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
                VertexPositionInputs pos = GetVertexPositionInputs(IN.positionOS);
                OUT.positionCS = pos.positionCS;
                OUT.uv = IN.uv;
                OUT.worldXY = pos.positionWS.xy;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                float t = _Time.y;
                float2 worldXY = IN.worldXY;

                // ---- layer 2 surface (computed first; warps the coords every other layer reads) -------------
                float surf = SurfaceNoise(worldXY, t);             // 0..1
            #if defined(_USE_SURFACETEX)
                // Painted ripple/detail (grayscale) scrolled with the current; blend over the procedural
                // noise. At strength 1 it fully replaces the procedural surface; at 0 it's pure procedural.
                float2 sScroll = normalize(_FlowDir.xy + float2(1e-4, 0)) * (_Flow * t);
                float surfTex = SAMPLE_TEXTURE2D(_SurfaceTex, sampler_SurfaceTex,
                                    PaintUV(worldXY, _PaintScale, sScroll)).r;
                surf = lerp(surf, surfTex, _SurfaceTexStrength);
            #endif
                float swell = (surf - 0.5) * 2.0;                  // -1..1
                // chop pushes a small world-space warp into the depth read so the waterline shimmers with swell
                float2 warp = float2(swell, ValueNoise(Pixelize(worldXY * _NoiseScale + 7.3)) - 0.5)
                              * _Chop * 0.5;

                // ---- layer 1 depth gradient -------------------------------------------------------------------
                float elevation = SeabedElevation(worldXY + warp);
                float depth = _WaterLevel - elevation;             // metres; <= 0 means dry/exposed

                // Dry ground: the shader hands off to the terrain tiles below (draw nothing).
                clip(depth + 1e-4);

                float dt = saturate((depth - _ShallowDepth) / max(_DeepDepth - _ShallowDepth, 1e-3));
                // Posterize the depth ramp into N bands for the pixel read (0 bands = smooth).
                if (_DepthBands >= 1.0)
                    dt = floor(dt * _DepthBands + 0.5) / _DepthBands;
            #if defined(_USE_DEPTHRAMP)
                // Owner hand-paints the exact shallow->deep colours in a 1D ramp (shallow at u=0). When
                // assigned this REPLACES the _ShallowColor/_DeepColor lerp; alpha comes from the ramp too.
                // v=0.5 stays mid-texel on a 1px-tall ramp (Repeat wrap; clamp-equivalent for the single row).
                half4 col = SAMPLE_TEXTURE2D(_DepthRamp, sampler_DepthRamp, float2(dt, 0.5));
            #else
                half4 col = lerp(_ShallowColor, _DeepColor, dt);
            #endif

                // Tint the base by the surface so the swell is visible even in flat light.
                col.rgb += swell * _SurfaceTint * 0.15;

                // ---- layer 5 caustics (shallows only; under the foam/spec so it reads as the seabed) ----------
                float causticGate = 1.0 - saturate(depth / max(_CausticDepth, 1e-3));   // 1 shallow -> 0 deep
                if (causticGate > 0.001 && _CausticAmount > 0.001)
                {
                    float2 cp = Pixelize(worldXY * _CausticScale + float2(t * _Flow, -t * _Flow * 0.7));
                    float ca = ValueNoise(cp);
                    float cb = ValueNoise(cp * 1.7 + 11.1);
                    float caustic = pow(saturate(1.0 - abs(ca - cb) * 3.0), 2.0);   // ridged -> bright veins
                #if defined(_USE_CAUSTICTEX)
                    // Painted caustics (grayscale), distorted by time, blended over the procedural veins;
                    // still depth-gated to the shallows by causticGate. Two counter-scrolling samples mul
                    // to a moving ripple so a static tile still "swims".
                    float2 cScroll = float2(t * _Flow * 0.6, -t * _Flow * 0.4);
                    float ct = SAMPLE_TEXTURE2D(_CausticTex, sampler_CausticTex,
                                   PaintUV(worldXY, _PaintScale * 2.0, cScroll)).r
                             * SAMPLE_TEXTURE2D(_CausticTex, sampler_CausticTex,
                                   PaintUV(worldXY, _PaintScale * 2.0, -cScroll * 1.3)).r;
                    caustic = lerp(caustic, ct * 2.0, _CausticTexStrength);   // *2: counter-mul darkens, restore range
                #endif
                    col.rgb += _CausticColor.rgb * caustic * _CausticAmount * causticGate;
                }

                // ---- layer 4 specular glints (implied single sun; pixelized so it sparkles, not smears) -------
                if (_SpecAmount > 0.001)
                {
                    float2 ld = normalize(_LightDir.xy + float2(1e-4, 0));
                    // a cheap surface "normal tilt" from the noise gradient, facing the implied light
                    float2 gp = Pixelize(worldXY * _NoiseScale);
                    float nx = ValueNoise(gp + float2(0.05, 0)) - ValueNoise(gp - float2(0.05, 0));
                    float ny = ValueNoise(gp + float2(0, 0.05)) - ValueNoise(gp - float2(0, 0.05));
                    float facing = saturate(dot(normalize(float2(nx, ny) + 1e-4), ld) * 0.5 + 0.5);
                    float glint = pow(facing, max(_SpecSharpness, 1.0));
                    glint = floor(glint * 4.0 + 0.5) / 4.0;        // posterize -> pixel sparkles
                #if defined(_USE_SPARKLETEX)
                    // Painted glint pattern (white-on-black), drifted with the current and still gated by
                    // `facing` so sparkles only land where the implied sun hits (one-sun discipline, ADR 0006).
                    float2 kScroll = normalize(_FlowDir.xy + float2(1e-4, 0)) * (_Flow * t * 0.5);
                    float sparkle = SAMPLE_TEXTURE2D(_SparkleTex, sampler_SparkleTex,
                                        PaintUV(worldXY, _SparkleTexScale, kScroll)).r * facing;
                    glint = lerp(glint, sparkle, _SparkleTexStrength);
                #endif
                    col.rgb += _SpecColor.rgb * glint * _SpecAmount;
                }

                // ---- layer 3 foam fringe (depth ~ 0 band that hugs the moving waterline) ----------------------
                // smoothstep across a thin band just inside the water: 1 at the wet edge -> 0 by foamWidth deep.
                float foamEdge = 1.0 - smoothstep(0.0, max(_FoamWidth, 1e-3), depth);
                if (foamEdge > 0.001)
                {
                    float churn = ValueNoise(Pixelize(worldXY * _FoamNoise + float2(-t * _Flow, t * _Flow)));
                #if defined(_USE_FOAMTEX)
                    // Painted foam pattern (white-on-transparent) scrolled with the current; its coverage
                    // (alpha, falling back to luminance for an opaque tile) replaces the procedural churn so
                    // the owner's foam shape breaks the line. Still masked to the depth~0 band by foamEdge.
                    float2 fScroll = float2(-t * _Flow, t * _Flow);
                    half4 foamSample = SAMPLE_TEXTURE2D(_FoamTex, sampler_FoamTex,
                                           PaintUV(worldXY, _PaintScale, fScroll));
                    float foamPat = max(foamSample.a, dot(foamSample.rgb, float3(0.299, 0.587, 0.114)));
                    churn = lerp(churn, foamPat, _FoamTexStrength);
                #endif
                    // wind roughness raises the foam threshold (whitecaps reach further in); churn breaks the line
                    float foamMask = saturate(foamEdge + _Roughness * 0.4) * (0.5 + 0.5 * churn);
                    foamMask = smoothstep(0.35, 0.65, foamMask);   // crisp pixel foam, not a soft smear
                    col.rgb = lerp(col.rgb, _FoamColor.rgb, foamMask * _FoamColor.a);
                    col.a = max(col.a, foamMask * _FoamColor.a);
                }

                // Whitecaps out on open water when it's rough (wind-driven), pixelized speckle.
                if (_Roughness > 0.01)
                {
                    float cap = ValueNoise(Pixelize(worldXY * _NoiseScale * 3.0 + t * _Flow));
                    float capMask = step(1.0 - _Roughness * 0.25, cap) * saturate(dt);  // only on deeper water
                #if defined(_USE_WHITECAPTEX)
                    // Painted whitecap pattern (white-on-transparent) drifted with the current; its coverage
                    // SCALES BY ROUGHNESS (the wind uniform) so caps appear/intensify with wind, and is gated
                    // to deeper water by dt. Blends over the procedural speckle.
                    float2 wScroll = normalize(_FlowDir.xy + float2(1e-4, 0)) * (_Flow * t);
                    half4 capSample = SAMPLE_TEXTURE2D(_WhitecapTex, sampler_WhitecapTex,
                                          PaintUV(worldXY, _PaintScale, wScroll));
                    float capPat = max(capSample.a, dot(capSample.rgb, float3(0.299, 0.587, 0.114)));
                    float capTexMask = capPat * saturate(_Roughness) * saturate(dt);
                    capMask = lerp(capMask, capTexMask, _WhitecapTexStrength);
                #endif
                    col.rgb = lerp(col.rgb, _FoamColor.rgb, capMask * 0.6);
                }

                return col;
            }
            ENDHLSL
        }
    }

    Fallback Off
}
