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
            #pragma target 4.5 HH_DECK_SHADOW
            #pragma multi_compile_local _ HH_DECK_SHADOW
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Include/HullLevelGate.hlsl"
            Texture2D<float4> _HHHullScreenTex;
            float4x4 _HHSunPose, _HHSunViewPose;
            float4 _HHSunProjection, _ShadowColor, _HullIds;
            float _HHSunWaterline, _HHSunLevelGate, _HHSunDoorOpen;
            float _HHLevelShown, _CastSunShadow, _ScreenShade;
            uint _HHLiftLevels, _HHShownRoles;
            float4 _HHRoleLiftMasks[31];
            struct Attributes { float3 positionOS:POSITION; float3 normalOS:NORMAL; float4 level:TEXCOORD0; float2 part:TEXCOORD1; };
            struct Varyings { float4 positionCS:SV_POSITION; nointerpolation float4 level:TEXCOORD0; nointerpolation float part:TEXCOORD1; float3 rig:TEXCOORD2; };
#ifdef HH_DECK_SHADOW
            struct DeckTriangle { float4 a,b,c,n; };
            struct DeckNode { float4 low,high; };
            StructuredBuffer<DeckTriangle> _HHDeckTriangles;
            StructuredBuffer<DeckNode> _HHDeckNodes;
            int _HHDeckNodeCount;
            float3 _HHDeckSunRay;
            float _HHDeckLevel;
            bool DeckVisible(DeckTriangle t)
            {
                if ((t.a.w > 0.5 && t.a.w < 1.5 && _HHSunDoorOpen > 0.5) || (t.a.w > 1.5 && _HHSunDoorOpen < 0.5)) return false;
                float3 n=TransformObjectToWorldNormal(t.n.xyz);
                float4 level=float4(t.b.w,t.c.w,dot(n,UNITY_MATRIX_V[2].xyz)>=0 ? 1:0,t.n.w);
                return _HHSunLevelGate<0.5 || !HHDiscardHullLevel(level,_HHLevelShown,_HHLiftLevels,_HHShownRoles,_HHRoleLiftMasks);
            }
            bool DeckRayHit(float3 origin,float3 ray,bool houseOnly)
            {
                float3 inverseRay=1.0/(ray+float3(abs(ray.x)<1e-8?1e-8:0,abs(ray.y)<1e-8?1e-8:0,abs(ray.z)<1e-8?1e-8:0));
                int node=0;
                [loop] while(node<_HHDeckNodeCount)
                {
                    DeckNode b=_HHDeckNodes[node];
                    float3 x=(b.low.xyz-origin)*inverseRay,y=(b.high.xyz-origin)*inverseRay;
                    float3 low=min(x,y),high=max(x,y);
                    float near=max(low.x,max(low.y,low.z)),far=min(high.x,min(high.y,high.z));
                    if(far<max(near,0.0)){node=(int)b.low.w;continue;}
                    node++;
                    if(b.high.w<0)continue;
                    DeckTriangle t=_HHDeckTriangles[(int)b.high.w];
                    if((houseOnly&&t.b.w<=_HHDeckLevel)||!DeckVisible(t))continue;
                    float3 ab=t.b.xyz-t.a.xyz,ac=t.c.xyz-t.a.xyz,p=cross(ray,ac);
                    float det=dot(ab,p);
                    if(abs(det)<1e-7)continue;
                    float3 offset=origin-t.a.xyz;
                    float u=dot(offset,p)/det;
                    float3 q=cross(offset,ab);
                    float v=dot(ray,q)/det, distance=dot(ac,q)/det;
                    if(u>=0&&v>=0&&u+v<=1&&distance>1e-4)return true;
                }
                return false;
            }
#endif
            Varyings vert(Attributes v)
            {
                Varyings o;
#ifdef HH_DECK_SHADOW
                o.positionCS=TransformObjectToHClip(v.positionOS);
                float3 wn=TransformObjectToWorldNormal(v.normalOS);
#else
                float3 p = mul(_HHSunPose,float4(v.positionOS,1)).xyz;
                float height = max(0.0,p.z + _HHSunProjection.w);
                float2 flat = float2(p.x,p.y*_HHSunProjection.z + _HHSunWaterline) + height*_HHSunProjection.xy;
                o.positionCS = TransformObjectToHClip(float3(flat,0));
                float3 wn = TransformObjectToWorldNormal(mul((float3x3)_HHSunViewPose,v.normalOS));
#endif
                o.level = float4(v.level.xy,dot(wn,UNITY_MATRIX_V[2].xyz) >= 0.0 ? 1.0 : 0.0,v.level.z);
                o.part = v.part.x;
                o.rig=v.positionOS;
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
#ifdef HH_DECK_SHADOW
                if(a<=0.5/255.0||!own)discard;
                float3 towardEye=normalize(mul((float3x3)unity_WorldToObject,UNITY_MATRIX_V[2].xyz));
                if(DeckRayHit(i.rig,towardEye,false))discard;
                if(!DeckRayHit(i.rig,_HHDeckSunRay,true))discard;
#else
                if (a > 0.5/255.0 && own) discard;
#endif
                if (_ScreenShade > 0.5) return half4(lerp(half3(1,1,1),_ShadowColor.rgb,_ShadowColor.a),1);
                return _ShadowColor;
            }
            ENDHLSL
        }
    }
    Fallback Off
}
