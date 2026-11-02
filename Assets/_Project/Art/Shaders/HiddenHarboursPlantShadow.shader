// Field sun shade shares the plants' wind. Foot bending deliberately does not move the shadow:
// a plant parting around a boot does not justify running the walker/trail loop again here.
Shader "HiddenHarbours/PlantShadow"
{
    Properties
    {
        [NoScaleOffset] _MainTex ("Caster page", 2D) = "white" {}
        _Sway ("Wind bend metres", Float) = 0.16
        _ShadowColor ("Shadow", Color) = (0.04,0.05,0.10,0.45)
        _ShadowDir ("Shadow direction", Vector) = (-1,0,0,0)
        _ShadowLen ("Shadow length multiple", Float) = 0
        _EdgeSoftness ("Edge softness", Float) = 0
        [HideInInspector] _ScreenShade ("Screen shade", Float) = 1
        [HideInInspector] _StencilRef ("Stencil ref", Int) = 1
        [HideInInspector] _StencilComp ("Stencil comparison", Int) = 6
        [HideInInspector] _StencilPass ("Stencil pass op", Int) = 2
        [HideInInspector] _SrcBlend ("Source blend", Int) = 0
        [HideInInspector] _DstBlend ("Destination blend", Int) = 3
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Blend [_SrcBlend] [_DstBlend]
        Cull Off ZWrite Off ZTest LEqual
        Stencil { Ref [_StencilRef] Comp [_StencilComp] Pass [_StencilPass] }
        Pass
        {
            Tags { "LightMode"="Universal2D" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Assets/_Project/Art/Shaders/Include/PlantWind.hlsl"
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
            float4 _ShadowColor, _ShadowDir;
            float _ShadowLen, _EdgeSoftness, _Sway;
            CBUFFER_END
            float4 _ShadowUVPerUnit, _WindWorld;
            float _FoliageSnow;
            struct Input { float3 vertex : POSITION; float2 uv : TEXCOORD0; float4 root : TEXCOORD1; float4 cell : TEXCOORD2; };
            struct Output { float4 vertex : SV_POSITION; float2 uv : TEXCOORD0; float2 shearUV : TEXCOORD1; float4 cell : TEXCOORD2; };
            Output Vert(Input v)
            {
                Output o;
                float3 wp = TransformObjectToWorld(v.vertex);
                float bend = v.root.z * v.root.z;
                wp.xy += PlantWindOffset(v.root.xy, _Time.y, _WindWorld.xy, _Sway) * bend * (1 - saturate(_FoliageSnow) * 0.8);
                float2 shear = _ShadowDir.xy * (_ShadowLen * (wp.y - v.root.y));
                wp.xy += shear;
                o.vertex = TransformWorldToHClip(wp); o.uv = v.uv;
                o.shearUV = shear * _ShadowUVPerUnit.xy; o.cell = v.cell; return o;
            }
            half4 Frag(Output i) : SV_Target
            {
                half srcA = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv).a;
                half mask = srcA;
                if (_EdgeSoftness > 0.0) mask = smoothstep(0.0, max(_EdgeSoftness, 1e-3), srcA);
                float2 uvSelf = i.uv + i.shearUV;
                if (all(uvSelf >= i.cell.xy) && all(uvSelf <= i.cell.xy + i.cell.zw) &&
                    SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uvSelf).a > 0.5) discard;
                half a = _ShadowColor.a * mask;
                if (a <= 0.0) discard;
                return half4(lerp(half3(1.0,1.0,1.0), _ShadowColor.rgb, a), 1.0);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
