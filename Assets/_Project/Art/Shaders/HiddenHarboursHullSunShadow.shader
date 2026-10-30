Shader "HiddenHarbours/HullSunShadow"
{
    Properties
    {
        [Toggle] _CastSunShadow ("Cast the sun's shadow", Float) = 0
        [HideInInspector] _SrcBlend ("Source blend", Float) = 0
        [HideInInspector] _DstBlend ("Destination blend", Float) = 3
        [HideInInspector] _ScreenShade ("Late shade arm", Float) = 1
        [HideInInspector] _StencilRef ("Shade stencil", Float) = 1
        [HideInInspector] _StencilComp ("Stencil comparison", Float) = 6
        [HideInInspector] _StencilPass ("Stencil operation", Float) = 2
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Blend [_SrcBlend] [_DstBlend]
        ZWrite Off
        ZTest Always
        Cull Off
        Stencil { Ref [_StencilRef] Comp [_StencilComp] Pass [_StencilPass] }
        Pass
        {
            Tags { "LightMode"="Universal2D" }
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Include/HullLevelGate.hlsl"
            Texture2D<float4> _HHHullScreenTex;
            float4x4 _HHSunPose;
            float4 _HHSunProjection, _ShadowColor, _HullIds;
            float _HHSunWaterline, _HHSunLevelGate, _HHSunDoorOpen;
            float _HHLevelShown, _CastSunShadow, _ScreenShade;
            uint _HHLiftLevels, _HHShownRoles;
            float4 _HHRoleLiftMasks[31];
            struct Attributes { float3 positionOS:POSITION; float4 level:TEXCOORD0; float2 part:TEXCOORD1; };
            struct Varyings { float4 positionCS:SV_POSITION; nointerpolation float4 level:TEXCOORD0; nointerpolation float part:TEXCOORD1; };
            Varyings vert(Attributes v)
            {
                Varyings o;
                float3 p = mul(_HHSunPose,float4(v.positionOS,1)).xyz;
                float height = max(0.0,p.z + _HHSunProjection.w);
                float2 flat = float2(p.x,p.y*_HHSunProjection.z + _HHSunWaterline) + height*_HHSunProjection.xy;
                o.positionCS = TransformObjectToHClip(float3(flat,0));
                o.level = v.level; o.part = v.part.x;
                return o;
            }
            half4 frag(Varyings i):SV_Target
            {
                if (_CastSunShadow <= 0 || _ShadowColor.a <= 0) discard;
                if ((i.part > 0.5 && i.part < 1.5 && _HHSunDoorOpen > 0.5) || (i.part > 1.5 && _HHSunDoorOpen < 0.5)) discard;
                if (_HHSunLevelGate > 0.5 && HHDiscardHullLevel(i.level,_HHLevelShown,_HHLiftLevels,_HHShownRoles,_HHRoleLiftMasks)) discard;
                int2 px = int2(i.positionCS.xy);
                float a = _HHHullScreenTex.Load(int3(px,0)).a;
                bool own = abs(a-_HullIds.x)*255.0 < 0.5;
                float fore = (a-_HullIds.y)*255.0;
                own = own || (_HullIds.y > 0 && fore > -0.5 && fore < _HullIds.z-0.5);
                if (a > 0.5/255.0 && own) discard;
                if (_ScreenShade > 0.5) return half4(lerp(half3(1,1,1),_ShadowColor.rgb,_ShadowColor.a),1);
                return _ShadowColor;
            }
            ENDHLSL
        }
    }
    Fallback Off
}
