Shader "Hidden/HiddenHarbours/FoamTransport"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        ZTest Always
        ZWrite Off
        Cull Off
        Blend Off
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
        #define F2_CODES 65535u
        #define F2_MAX_COURANT 0.25
        Texture2D<float4> _F2Prev;
        Texture2D<float2> _F2Velocity;
        Texture2D<float> _F2Mask;
        float4 _F2Grid;       // xy resolution, z cell metres, w substep seconds
        float4 _F2World;      // xy frozen draw origin, zw absolute lattice cell origin
        float4 _F2FlowWorld;  // xy velocity node origin, z inverse spacing, w node resolution

        float2 VelocityAt(float2 world)
        {
            float2 p=(world-_F2FlowWorld.xy)*_F2FlowWorld.z;
            int2 a=int2(floor(p)); float2 t=frac(p);
            // CPU allocates the halo; clamping here is only a last address guard, never a history sample.
            a=clamp(a,int2(0,0),int2(_F2FlowWorld.ww)-2);
            float2 lo=lerp(_F2Velocity.Load(int3(a,0)),_F2Velocity.Load(int3(a+int2(1,0),0)),t.x);
            float2 hi=lerp(_F2Velocity.Load(int3(a+int2(0,1),0)),_F2Velocity.Load(int3(a+int2(1,1),0)),t.x);
            return lerp(lo,hi,t.y);
        }

        float4 Exchange(Varyings input, int axis, int parity) : SV_Target
        {
            int2 cell=int2(input.positionCS.xy), res=int2(_F2Grid.xy);
            int2 stepCell=axis==0?int2(1,0):int2(0,1);
            int absoluteCell=(axis==0?cell.x:cell.y)+(axis==0?(int)_F2World.z:(int)_F2World.w);
            bool isA=(absoluteCell&1)==parity;
            int2 a=isA?cell:cell-stepCell, b=a+stepCell;
            float2 own=_F2Prev.Load(int3(cell,0)).rg;
            if(any(a<0) || any(b>=res)) return float4(own,0,1);
            if(_F2Mask.Load(int3(a,0))>0 || _F2Mask.Load(int3(b,0))>0) return float4(own,0,1);
            // Both endpoints evaluate this identical ordering, including freshness integer division.
            float2 pa=isA?own:_F2Prev.Load(int3(a,0)).rg;
            float2 pb=isA?_F2Prev.Load(int3(b,0)).rg:own;
            uint2 ca=(uint2)round(saturate(pa)*65535.0), cb=(uint2)round(saturate(pb)*65535.0);
            float2 face=_F2World.xy+(float2(a)+0.5+float2(stepCell)*0.5)*_F2Grid.z;
            float2 velocity=VelocityAt(face);
            float speed=axis==0?velocity.x:velocity.y;
            bool reverse=speed<0;
            uint2 donor=reverse?cb:ca, receiver=reverse?ca:cb;
            float c=min(abs(speed)*_F2Grid.w/_F2Grid.z,F2_MAX_COURANT);
            uint q=min((uint)floor(c*donor.x),F2_CODES-receiver.x);
            if(q==0u) return float4(own,0,1);
            uint total=receiver.x+q;
            uint fresh=(receiver.x*receiver.y+q*donor.y+total/2u)/total;
            uint retained=donor.x-q;
            uint2 newDonor=uint2(retained,retained==0u?0u:donor.y);
            uint2 newReceiver=uint2(total,fresh);
            uint2 result=(isA!=reverse)?newDonor:newReceiver;
            return float4(float2(result)/65535.0,0,1);
        }
        float4 XEven(Varyings i):SV_Target { return Exchange(i,0,0); }
        float4 XOdd (Varyings i):SV_Target { return Exchange(i,0,1); }
        float4 YEven(Varyings i):SV_Target { return Exchange(i,1,0); }
        float4 YOdd (Varyings i):SV_Target { return Exchange(i,1,1); }
        ENDHLSL
        Pass
        {
            Name "F2XEven"
            HLSLPROGRAM
            #pragma target 4.0
            #pragma vertex Vert
            #pragma fragment XEven
            ENDHLSL
        }
        Pass
        {
            Name "F2XOdd"
            HLSLPROGRAM
            #pragma target 4.0
            #pragma vertex Vert
            #pragma fragment XOdd
            ENDHLSL
        }
        Pass
        {
            Name "F2YEven"
            HLSLPROGRAM
            #pragma target 4.0
            #pragma vertex Vert
            #pragma fragment YEven
            ENDHLSL
        }
        Pass
        {
            Name "F2YOdd"
            HLSLPROGRAM
            #pragma target 4.0
            #pragma vertex Vert
            #pragma fragment YOdd
            ENDHLSL
        }
    }
}
