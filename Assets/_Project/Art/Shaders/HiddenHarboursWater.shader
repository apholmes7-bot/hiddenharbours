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

        [Header(Wind chop + syncopation (multi rate multi direction surface))]
        // Layer-2 used to scroll EVERY noise octave along _FlowDir (the tidal CURRENT, a fixed axis), so
        // the surface read as one marching grid. These break that: a wind-driven chop octave scrolled
        // along the WIND, plus a slow cross-swell on a perpendicular axis, mixed by per-octave weights.
        // _WindDir is pushed by WaterSurface.cs from EnvironmentSample.WindVector (sim-driven, varies over
        // time); the default is the calm-wind fallback (+Y) so the look is sane before the sim feeds it.
        _WindDir        ("Wind direction (xy, sim-driven)", Vector) = (0, 1, 0, 0)
        _WindChop       ("Wind chop weight (0..1)", Range(0,1)) = 0.4
        _WindChopScale  ("Wind chop noise scale", Float) = 0.7
        _WindChopSpeed  ("Wind chop scroll speed", Float) = 0.09
        _CrossSwellDir  ("Cross-swell dir (xy; 0,0 = auto perpendicular)", Vector) = (0, 0, 0, 0)
        _CrossSwellSpeed("Cross-swell scroll speed", Float) = 0.025
        _CrossSwellScale("Cross-swell noise scale (big = long swell)", Float) = 0.16
        _Octave2Weight  ("Octave 2 (wind chop) mix weight", Range(0,1)) = 0.35
        _Octave3Weight  ("Octave 3 (cross swell) mix weight", Range(0,1)) = 0.3

        [Header(FBM low freq variance (organic patches + sparkle scatter))]
        // A big-scale, slow-drifting fractal field that breaks the even grid two ways (both col.rgb-only,
        // never touching depth/clip/the gameplay waterline): a soft brightness/tint patchwork, and a GATE
        // on the specular so sparkles CLUSTER organically instead of an even posterized lattice.
        _FbmScale       ("FBM scale (big = broad patches)", Float) = 0.05
        _FbmDriftSpeed  ("FBM drift speed", Float) = 0.012
        _FbmStrength    ("FBM tint strength (0..1)", Range(0,1)) = 0.18
        _FbmTint        ("FBM tint color", Color) = (0.55, 0.72, 0.78, 1.0)
        _FbmGateLo      ("FBM spec gate low (sparkles start)", Range(0,1)) = 0.35
        _FbmGateHi      ("FBM spec gate high (sparkles full)", Range(0,1)) = 0.7
        _SpecBands      ("Specular posterize bands", Float) = 4

        [Header(Foam fringe (layer 3))]
        _FoamColor      ("Foam color", Color)           = (0.92, 0.96, 0.98, 1.0)
        _FoamWidth      ("Foam band width (m)", Float)  = 0.45
        _FoamSoftness   ("Foam edge softness (m)", Float) = 0.18
        _FoamNoise      ("Foam churn noise scale", Float) = 1.2
        _Roughness      ("Roughness / whitecaps (0..1)", Range(0,1)) = 0.2

        [Header(Beach swash   always on shoreline wash   cosmetic   foam band only)]
        _SwashAmplitude ("Swash amplitude (m, foam-band only)", Float) = 0.3
        _SwashSpeed     ("Swash speed (waves / 2pi sec)", Float)      = 0.5
        _SwashScale     ("Swash along-shore variation scale", Float)  = 0.25

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
        // Anti-tiling: hide the painted tile's repeat grid (IQ-style hash-untile + domain warp). 0 = raw
        // tiling (the grid reads at CALM), 1 = full break-up. Applied to every scrolling painted slot.
        _UntileStrength  ("Untile strength (0=raw grid, 1=broken up)", Range(0,1)) = 0.6

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
                // Wind chop + syncopation (multi-rate / multi-direction surface octaves).
                float4 _WindDir;
                float  _WindChop;
                float  _WindChopScale;
                float  _WindChopSpeed;
                float4 _CrossSwellDir;
                float  _CrossSwellSpeed;
                float  _CrossSwellScale;
                float  _Octave2Weight;
                float  _Octave3Weight;
                // FBM low-frequency variance (organic patches + sparkle scatter).
                float  _FbmScale;
                float  _FbmDriftSpeed;
                float  _FbmStrength;
                float4 _FbmTint;
                float  _FbmGateLo;
                float  _FbmGateHi;
                float  _SpecBands;
                float4 _FoamColor;
                float  _FoamWidth;
                float  _FoamSoftness;
                float  _FoamNoise;
                float  _Roughness;
                float  _SwashAmplitude;
                float  _SwashSpeed;
                float  _SwashScale;
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
                float  _UntileStrength;
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

            // ---- WIND-DRIVEN chop octave (a SEPARATE scroll from the tidal current) -------------------------
            // A 2-octave value-noise field scrolled along the WIND direction (normalize(_WindDir.xy)) at its
            // OWN rate (_WindChopSpeed) and scale (_WindChopScale) — NOT along _FlowDir. This is what lets the
            // surface follow the wind (which the sim varies over time) instead of marching down the fixed
            // current axis. Pixelized like every other octave so it reads as pixel art. Returns 0..1.
            float WindChop(float2 worldXY, float t)
            {
                float2 dir = normalize(_WindDir.xy + float2(0, 1e-4));   // +Y fallback on a zero wind dir
                float2 scroll = dir * (_WindChopSpeed * t);
                float2 p1 = Pixelize((worldXY + scroll) * _WindChopScale);
                float2 p2 = Pixelize((worldXY + scroll * 1.7) * _WindChopScale * 2.3);
                return ValueNoise(p1) * 0.6 + ValueNoise(p2) * 0.4;
            }

            // ---- FBM: fractal value-noise (low-frequency organic variance) ----------------------------------
            // A few octaves of ValueNoise (lacunarity ~2, gain ~0.5) summed to a normalized 0..1 field. Sampled
            // at a BIG scale (_FbmScale) and slowly drifted (_FbmDriftSpeed) it gives broad, slowly-moving
            // patches — used to (i) softly tint col.rgb and (ii) GATE the specular so sparkles cluster, both
            // of which break the single-direction "marching grid" read. Pixelized so it stays pixel-art.
            float Fbm(float2 p, int octaves)
            {
                float sum = 0.0;
                float amp = 0.5;
                float norm = 0.0;
                [unroll(4)]
                for (int i = 0; i < octaves; i++)
                {
                    sum  += ValueNoise(Pixelize(p)) * amp;
                    norm += amp;
                    p    *= 2.0;     // lacunarity
                    amp  *= 0.5;     // gain
                }
                return sum / max(norm, 1e-4);
            }

            // Three-octave SYNCOPATED surface noise. Each octave has a DISTINCT (direction, rate) so the
            // surface stops reading as one marching grid (the owner's "marches one direction" complaint):
            //   A = the current swell along _FlowDir @ _Flow      (the original look, the base)
            //   B = the wind chop  along _WindDir  @ _WindChopSpeed (follows the sim wind, weighted _WindChop)
            //   C = a SLOW cross-swell on a perpendicular axis @ _CrossSwellSpeed, big _CrossSwellScale
            // Octaves B and C are folded in by per-octave weights (_Octave2Weight / _Octave3Weight) so the
            // owner can dial the syncopation. Still pure value-noise + pixelize — no textures, ~no extra cost.
            float SurfaceNoise(float2 worldXY, float t)
            {
                // A — current swell along the tidal set (the existing octave; the foundation).
                float2 flowDir = normalize(_FlowDir.xy + float2(1e-4, 0));
                float2 scrollA = flowDir * (_Flow * t);
                float2 pA1 = Pixelize((worldXY + scrollA) * _NoiseScale);
                float2 pA2 = Pixelize((worldXY - scrollA * 0.6) * _NoiseScale * 2.0);
                float octaveA = ValueNoise(pA1) * 0.65 + ValueNoise(pA2) * 0.35;

                // B — wind chop along the wind direction at its own rate (raw 0..1 octave).
                float octaveB = WindChop(worldXY, t);

                // C — slow cross-swell on a perpendicular axis: either the explicit _CrossSwellDir, or (when
                // that's near-zero) the perpendicular of the average of flow & wind, so it crosses the grain.
                float2 avgDir = normalize(flowDir + normalize(_WindDir.xy + float2(0, 1e-4)) + float2(1e-4, 0));
                float2 autoCross = float2(-avgDir.y, avgDir.x);                  // rotate 90 deg
                float2 crossDir = (dot(_CrossSwellDir.xy, _CrossSwellDir.xy) > 1e-6)
                                    ? normalize(_CrossSwellDir.xy) : autoCross;
                float2 scrollC = crossDir * (_CrossSwellSpeed * t);
                float octaveC = ValueNoise(Pixelize((worldXY + scrollC) * _CrossSwellScale));

                // Weighted blend, normalized so the result stays ~0..1 regardless of the syncopation weights.
                // Each octave has ONE clear effective weight (no double-counting): the wind chop's mix weight
                // is _WindChop * _Octave2Weight (the headline wind knob × the octave-2 fine-tune); the
                // cross-swell's is _Octave3Weight. _Octave2/3Weight both default to a modest mix so the
                // syncopation reads immediately but stays dial-able to 0 (back to the single-direction look).
                float wB = _WindChop * _Octave2Weight;
                float wC = _Octave3Weight;
                float total = 1.0 + wB + wC;
                return (octaveA + octaveB * wB + octaveC * wC) / total;
            }

            // Painted-texture UV: pixelize the world position to the PPU grid, then scale to tiles/unit.
            // Repeat wrap (set in the texture import) makes a seamless ~64px tile cover the whole plane;
            // the pixelize keeps the sampled coord on the grid so painted detail reads as pixel art too.
            // `scroll` lets a layer drift the pattern with the current (pass float2(0,0) for a static tile).
            float2 PaintUV(float2 worldXY, float scale, float2 scroll)
            {
                return Pixelize(worldXY + scroll) * max(scale, 1e-4);
            }

            // 2-vector hash for the untile per-tile offset (different lattice constants from Hash21 so the
            // untile offset doesn't correlate with the surface noise).
            float2 Hash22(float2 p)
            {
                p = float2(dot(p, float2(127.1, 311.7)), dot(p, float2(269.5, 183.3)));
                return frac(sin(p) * 43758.5453);
            }

            // ---- IQ-style texture UNTILING (hide the repeat grid that reads at CALM) -------------------------
            // The painted slots are small seamless tiles on Repeat wrap, so at a glassy sea-state (no chop/flow
            // motion to mask it) the tile boundary reads as an obvious grid. This breaks it up two ways, both
            // dialed by _UntileStrength (0 = raw tiling, 1 = full break-up):
            //   1) DOMAIN WARP — nudge the sample UV by the low-freq surface ValueNoise so straight tile seams
            //      bend before they're sampled (cheap, smooth).
            //   2) HASH-UNTILE — per repeat-cell, offset the lookup by a cell hash, then blend two neighbouring
            //      offset variants by a smooth weight so adjacent cells differ yet never seam.
            // PIXEL-ART faithful: the offset is added to the WORLD coord BEFORE PaintUV pixelizes, so the
            // untiled lookup still snaps to the PPU grid and stays point-sampled. Pass scroll for the drift.
            half4 UntileSampleW(TEXTURE2D_PARAM(tex, smp), float2 worldXY, float scale, float2 scroll, float strength)
            {
                float s = saturate(strength);
                // (1) domain warp: a small world-space nudge from the surface noise, scaled by strength.
                // Two warp octaves (low-freq bend + a finer ripple) read more organic than one straight nudge;
                // both still dialed by _UntileStrength (no new knob) so 0 strength = the raw grid unchanged.
                float2 warpLo = float2(ValueNoise(worldXY * _NoiseScale * 0.5 + 3.1),
                                       ValueNoise(worldXY * _NoiseScale * 0.5 + 8.7)) - 0.5;
                float2 warpHi = float2(ValueNoise(worldXY * _NoiseScale * 1.7 + 17.3),
                                       ValueNoise(worldXY * _NoiseScale * 1.7 + 42.9)) - 0.5;
                float2 warpN = warpLo + warpHi * 0.4;
                float2 warped = worldXY + warpN * (s * 1.5);

                half4 raw = SAMPLE_TEXTURE2D(tex, smp, PaintUV(warped, scale, scroll));
                if (s <= 0.001) return raw;

                // (2) hash-untile in TILE space (uv = warped*scale; one repeat-cell == 1 unit of uv).
                float2 uv  = Pixelize(warped + scroll) * max(scale, 1e-4);
                float2 iuv = floor(uv);
                float2 fuv = frac(uv);
                // two candidate cell offsets (this cell + the diagonally-adjacent cell) so the blend never
                // shows a seam: each is hashed to a per-cell translation; world-space so PaintUV still snaps.
                float2 offA = Hash22(iuv)            * 64.0;          // a few tiles of world translation
                float2 offB = Hash22(iuv + 1.0)      * 64.0;
                half4 a = SAMPLE_TEXTURE2D(tex, smp, PaintUV(warped + offA, scale, scroll));
                half4 b = SAMPLE_TEXTURE2D(tex, smp, PaintUV(warped + offB, scale, scroll));
                // smooth blend weight across the cell so neighbours cross-fade (no hard tile edge).
                float w = smoothstep(0.2, 0.8, fuv.x) * 0.5 + smoothstep(0.2, 0.8, fuv.y) * 0.5;
                half4 untiled = lerp(a, b, w);
                // dial raw(+warp) <-> untiled by strength.
                return lerp(raw, untiled, s);
            }

            // ---- ALWAYS-ON beach swash (cosmetic waterline wash; foam band ONLY) ----------------------------
            // A fast sine on _Time that makes the wet edge advance & recede CONTINUOUSLY, independent of the
            // slow deterministic tide — the "waves crashing in and out" the procedural foam alone didn't have.
            // Returns a signed DEPTH OFFSET (metres): + pulls the wet edge inshore (advances), - pushes it
            // back. The caller GATES it to the depth~0 foam band and applies it to a LOCAL foam-only depth, so
            // it NEVER touches the real `depth` that drives clip()/the deep tint/the caustic gate, NEVER moves
            // the gameplay waterline, and saves nothing (the P1 integrity rule, CLAUDE.md rule 5). Visual-only.
            // Along-shore variation (_SwashScale over world X+Y) keeps the wash from pulsing as one flat line.
            float BeachSwash(float2 worldXY, float t)
            {
                float alongShore = (worldXY.x + worldXY.y) * _SwashScale;
                // two beats slightly out of phase read as overlapping run-up/backwash, not a metronome.
                float wave = sin(t * _SwashSpeed * 6.2831853 + alongShore) * 0.7
                           + sin(t * _SwashSpeed * 6.2831853 * 0.5 + alongShore * 1.7) * 0.3;
                return wave * _SwashAmplitude;
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
                // untile so the painted ripple's repeat grid stops reading at CALM (the headline fix).
                float surfTex = UntileSampleW(TEXTURE2D_ARGS(_SurfaceTex, sampler_SurfaceTex),
                                    worldXY, _PaintScale, sScroll, _UntileStrength).r;
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

                // ---- FBM low-frequency variance (organic patches; col.rgb ONLY — never touches depth) --------
                // One big-scale, slowly-drifting fractal field, reused below to gate the specular. It softly
                // tints the base so the sea breaks into broad slow patches instead of an even sheet — purely
                // cosmetic (col.rgb), so it cannot move the waterline/clip/deep-tint (P1 integrity, rule 5).
                float2 fbmDrift = float2(t * _FbmDriftSpeed, -t * _FbmDriftSpeed * 0.8);
                float fbm = Fbm((worldXY + fbmDrift) * _FbmScale, 4);   // 0..1
                if (_FbmStrength > 0.001)
                {
                    // signed around the patch midpoint so some areas lift toward the tint, others sit back.
                    float fbmSigned = (fbm - 0.5) * 2.0;               // -1..1
                    col.rgb = lerp(col.rgb, _FbmTint.rgb, saturate(fbmSigned) * _FbmStrength);
                    col.rgb += fbmSigned * _FbmStrength * 0.06;        // gentle brightness wobble
                }

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
                    float ct = UntileSampleW(TEXTURE2D_ARGS(_CausticTex, sampler_CausticTex),
                                   worldXY, _PaintScale * 2.0, cScroll, _UntileStrength).r
                             * UntileSampleW(TEXTURE2D_ARGS(_CausticTex, sampler_CausticTex),
                                   worldXY, _PaintScale * 2.0, -cScroll * 1.3, _UntileStrength).r;
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
                    // posterize into _SpecBands steps -> pixel sparkles (tunable band count, was a hard 4).
                    float bands = max(_SpecBands, 1.0);
                    glint = floor(glint * bands + 0.5) / bands;
                #if defined(_USE_SPARKLETEX)
                    // Painted glint pattern (white-on-black), drifted with the current and still gated by
                    // `facing` so sparkles only land where the implied sun hits (one-sun discipline, ADR 0006).
                    float2 kScroll = normalize(_FlowDir.xy + float2(1e-4, 0)) * (_Flow * t * 0.5);
                    float sparkle = UntileSampleW(TEXTURE2D_ARGS(_SparkleTex, sampler_SparkleTex),
                                        worldXY, _SparkleTexScale, kScroll, _UntileStrength).r * facing;
                    glint = lerp(glint, sparkle, _SparkleTexStrength);
                #endif
                    // FBM SCATTER: gate the glint by the low-freq field so sparkles CLUSTER in patches
                    // organically instead of an even grid (the marching-grid fix for the highlights). The
                    // gate is BEFORE the additive so it only thins col.rgb's sparkle — never the depth/clip.
                    float specGate = smoothstep(_FbmGateLo, _FbmGateHi, fbm);
                    col.rgb += _SpecColor.rgb * glint * _SpecAmount * specGate;
                }

                // ---- layer 3 foam fringe (depth ~ 0 band that hugs the moving waterline) ----------------------
                // ALWAYS-ON swash: a cosmetic, _Time-driven depth offset that advances/recedes the wet edge.
                // GATED to the depth~0 band (full at the wet edge, 0 by ~2x the foam width) and applied ONLY
                // to a LOCAL foam-only depth — the real `depth` (clip/dt/caustics) is never touched, so deep
                // water and the gameplay waterline don't move. Pure foam dressing (P1 integrity, rule 5).
                float swashReach = max(_FoamWidth, 1e-3) * 2.0 + max(abs(_SwashAmplitude), 1e-3);
                float swashGate  = 1.0 - smoothstep(0.0, swashReach, depth);   // 1 at the wet edge -> 0 deeper
                float foamDepth  = depth - BeachSwash(worldXY, t) * swashGate;  // local, foam-only
                // smoothstep across a thin band just inside the water: 1 at the wet edge -> 0 by foamWidth deep.
                float foamEdge = 1.0 - smoothstep(0.0, max(_FoamWidth, 1e-3), foamDepth);
                if (foamEdge > 0.001)
                {
                    float churn = ValueNoise(Pixelize(worldXY * _FoamNoise + float2(-t * _Flow, t * _Flow)));
                #if defined(_USE_FOAMTEX)
                    // Painted foam pattern (white-on-transparent) scrolled with the current; its coverage
                    // (alpha, falling back to luminance for an opaque tile) replaces the procedural churn so
                    // the owner's foam shape breaks the line. Still masked to the depth~0 band by foamEdge.
                    float2 fScroll = float2(-t * _Flow, t * _Flow);
                    half4 foamSample = UntileSampleW(TEXTURE2D_ARGS(_FoamTex, sampler_FoamTex),
                                           worldXY, _PaintScale, fScroll, _UntileStrength);
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
