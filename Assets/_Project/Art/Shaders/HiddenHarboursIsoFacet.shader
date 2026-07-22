// HiddenHarboursIsoFacet.shader — the mesh-hull facet pass (ADR 0022 phase 3).
//
// Reproduces the art director's rig rasteriser on the GPU: flat per-FACE normal, fixed
// screen-space key light, palette-RAMP lookup (not continuous lighting), ordered Bayer dither
// between adjacent ramp indices, no AA, no filtering. Descends from the measured spike shader
// (spike/3d-boats, ADR 0022: 1.3–4.4% px vs the rig's own render, dither crawl 0.00%).
//
// The hull mesh is placed in world space ALREADY iso-rotated (IsoFacetMath.RigToWorld — the
// rig's projection baked into the object transform), so the game's straight-down orthographic
// 2D camera reproduces the rig's exact projection AND z-buffer. That collapses the rig's
// shadeOf() to a plain dot(worldNormal, LN): the key light is fixed in SCREEN space, which is
// what makes the shading read as pixel art rather than as lit 3D. _LN arrives with its z
// NEGATED (IsoFacetMath.ShaderLightVector) because the object matrix is a REFLECTION of the
// rig's right-handed frame — measured in the spike; do not "fix" the sign.
//
// ⚠️ The ONLY pass has LightMode "HHHullFacet", which the 2D renderer's own draw does NOT pick
// up (deliberately: a mesh writing the scene's shared depth buffer punches holes in every later
// sprite that z-tests). IsoFacetHullFeature draws it off-screen into a 4-target MRT with a
// private depth buffer; IsoFacetOverlay re-composes the resolved image in-scene.
//
// Dither is indexed in the HULL-CELL frame derived from world position — NOT SV_Position — so
// it cannot crawl when the hull translates (the 13–16% class ADR 0022 measured for
// screen-pinned dither) and needs no per-render-target phase calibration (the spike's
// _DitherPhase probe becomes unnecessary: world-derived cell coordinates are y-flip-proof).
//
// SHADER CAUTIONS honoured (this project lost hours to magenta shaders): no operator characters
// in Property display strings; no [unroll] over runtime bounds; force-compiled headless by
// IsoFacetShaderCompileGuardTests so a break fails CI red.
Shader "HiddenHarbours/IsoFacet"
{
    Properties
    {
        [NoScaleOffset] _RampTex ("Palette ramps by material", 2D) = "white" {}
        [NoScaleOffset] _DarkRampTex ("RINDEX darkened ramps by material", 2D) = "white" {}
        _KeyColor ("Keyline colour, pre linearised", Color) = (0.05, 0.08, 0.09, 1)
        _Gain ("Rig GAIN", Float) = 0
        _Bias ("Rig BIAS", Float) = 0
        _PivotPx ("Cell pivot px from top left", Vector) = (0, 0, 0, 0)
        _PixelsPerMetre ("Pixels per metre", Float) = 32
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }

        Pass
        {
            Name "HHHullFacet"
            // Drawn ONLY by IsoFacetHullFeature's renderer list — see the header comment.
            Tags { "LightMode" = "HHHullFacet" }

            Cull Off            // the rig z-buffers everything; it never backface-culls
            ZWrite On
            ZTest LEqual
            Blend Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            // Palette lookups are integer Loads — never filtered, never mipped.
            Texture2D<float4> _RampTex;
            Texture2D<float4> _DarkRampTex;

            // Set once per hull material (IsoFacetHullRenderer.Configure). Arrays cannot be
            // Properties; they are plain uniforms via SetVectorArray. Not SRP-batcher packed —
            // hulls are few and each is one draw in a private pass.
            float4 _LN;                 // rig LN, z pre-negated for the reflected frame
            float  _Gain, _Bias;
            float4 _Bayer[4];           // BAYER[x&3][y&3], values already (v+0.5)/16, row = x
            float4 _RampMeta[16];       // per material: x = ramp length, y = index offset
            float4 _KeyColor;           // pre-linearised keyline colour
            float4 _PivotPx;            // xy = cell pivot in px from the cell's top-left
            float  _PixelsPerMetre;

            // Per draw via MaterialPropertyBlock (IsoFacetHullRenderer.ApplyPose).
            float4 _HullOrigin;         // xy = world position of the rig origin (unheaved root)
            float  _HullId;             // hull id already divided by 255; the facet alpha

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 attrs      : TEXCOORD0;   // x = matId  y = faceBias b  z = depthBias db
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                // Face-flat by construction (every vertex of a face carries the face's values);
                // nointerpolation keeps them exact across the fan.
                nointerpolation float fidx : TEXCOORD0;
                nointerpolation float mat  : TEXCOORD1;
                float3 wpos : TEXCOORD2;         // xy = dither frame  z = TRUE unbiased depth
            };

            struct FragOut
            {
                float4 facet : SV_Target0;       // rgb = facet colour, a = hull id
                float4 dark  : SV_Target1;       // rgb = RINDEX darkened colour
                float4 key   : SV_Target2;       // rgb = keyline colour
                float  depth : SV_Target3;       // true unbiased view depth (world z, metres)
            };

            Varyings vert (Attributes v)
            {
                Varyings o;
                float3 wp = mul(unity_ObjectToWorld, float4(v.positionOS.xyz, 1.0)).xyz;
                // The object matrix is orthogonal (rotation times mirror), so inverse-transpose
                // equals the matrix itself — the plain mul is exact, as in the spike.
                float3 wn = normalize(mul((float3x3)unity_ObjectToWorld, v.normalOS));

                // The rig: sh = shadeOf(n, se, ce). In iso-rotated world space that is exactly a
                // dot with LN — see the header comment.
                float sh = dot(wn, _LN.xyz);
                // "if(sh<0 && f.b<=-1) sh = shadeOf(-n)*0.9" — the rig's interior/backface rescue.
                if (sh < 0 && v.attrs.y <= -1) sh = -sh * 0.9;

                o.fidx = sh * _Gain + _Bias + v.attrs.y;
                o.mat  = v.attrs.x;
                o.wpos = wp;                     // camera looks along +Z; larger z = further
                wp.z  -= v.attrs.z;              // f.db pulls the face toward the camera
                o.positionCS = TransformWorldToHClip(wp);
                return o;
            }

            FragOut frag (Varyings i)
            {
                // The hull-cell pixel this fragment lands on, derived from WORLD position: the
                // rig's screen grid is just world metres times PPU with y down and the pivot as
                // origin. Locked to the hull, immune to render-target conventions.
                float2 cellF = float2(
                    (i.wpos.x - _HullOrigin.x) * _PixelsPerMetre + _PivotPx.x,
                    _PivotPx.y - (i.wpos.y - _HullOrigin.y) * _PixelsPerMetre);
                int2 cell = int2(floor(cellF));
                float bay = _Bayer[cell.x & 3][cell.y & 3];

                int m    = (int)round(i.mat);
                int len  = (int)_RampMeta[m].x;
                int off  = (int)_RampMeta[m].y;
                float fbase = floor(i.fidx);
                int idx = (int)fbase + ((i.fidx - fbase) > bay ? 1 : 0) + off;
                idx = clamp(idx, 0, len - 1);

                FragOut o;
                o.facet = float4(_RampTex.Load(int3(idx, m, 0)).rgb, _HullId);
                o.dark  = float4(_DarkRampTex.Load(int3(idx, m, 0)).rgb, 1.0);
                o.key   = float4(_KeyColor.rgb, 1.0);
                o.depth = i.wpos.z;
                return o;
            }
            ENDHLSL
        }
    }
}
