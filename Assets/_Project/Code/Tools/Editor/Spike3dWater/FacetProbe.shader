// ⚠️⚠️ THROWAWAY SPIKE CODE — spike/3d-water. NOT A PIPELINE. NOT FOR MERGE AS-IS. ⚠️⚠️
//
// VERBATIM copy of the production HiddenHarbours/IsoFacet pass (ADR 0022 phase 3) with exactly
// TWO changes, both required to draw it through this spike's hand-built CommandBuffer:
//   1. ZTest is a property ([_ZTestOp]) instead of the hardcoded LEqual — a raw CommandBuffer
//      with explicit GPU projection matrices does NOT get Unity's reversed-Z ZTest translation,
//      and this machine calibrated to GEqual/clear-0 (measured in run 1: the hull was invisible
//      behind the hardcoded LEqual). Production inside URP's frame is untouched and correct.
//   2. LightMode tag dropped (the spike draws the pass explicitly; no feature list involved).
// Everything else — shading, dither, MRT layout — is the production pass, so the probe image
// speaks the shipped facet language.
Shader "Hidden/HH/Spike3dWater/FacetProbe"
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
        _ZTestOp ("Z test op", Float) = 4
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }

        Pass
        {
            Name "SpikeFacetProbe"
            Cull Off
            ZWrite On
            ZTest [_ZTestOp]
            Blend Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            Texture2D<float4> _RampTex;
            Texture2D<float4> _DarkRampTex;

            float4 _LN;                 // rig LN, z pre-negated for the reflected frame
            float  _Gain, _Bias;
            float4 _Bayer[4];           // BAYER[x&3][y&3], values already (v+0.5)/16, row = x
            float4 _RampMeta[16];       // per material: x = ramp length, y = index offset
            float4 _KeyColor;
            float4 _PivotPx;
            float  _PixelsPerMetre;
            float  _ZTestOp;

            float4 _HullOrigin;         // xy = world position of the rig origin (unheaved root)
            float  _HullId;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 attrs      : TEXCOORD0;   // x = matId  y = faceBias b  z = depthBias db
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                nointerpolation float fidx : TEXCOORD0;
                nointerpolation float mat  : TEXCOORD1;
                float3 wpos : TEXCOORD2;
            };

            struct FragOut
            {
                float4 facet : SV_Target0;
                float4 dark  : SV_Target1;
                float4 key   : SV_Target2;
                float  depth : SV_Target3;
            };

            Varyings vert (Attributes v)
            {
                Varyings o;
                float3 wp = mul(unity_ObjectToWorld, float4(v.positionOS.xyz, 1.0)).xyz;
                float3 wn = normalize(mul((float3x3)unity_ObjectToWorld, v.normalOS));

                float sh = dot(wn, _LN.xyz);
                if (sh < 0 && v.attrs.y <= -1) sh = -sh * 0.9;

                o.fidx = sh * _Gain + _Bias + v.attrs.y;
                o.mat  = v.attrs.x;
                o.wpos = wp;
                wp.z  -= v.attrs.z;
                o.positionCS = TransformWorldToHClip(wp);
                return o;
            }

            FragOut frag (Varyings i)
            {
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
