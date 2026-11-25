Shader "HiddenHarbours/CloudShadow"
{
    Properties { _CloudShadeNoise ("Runtime cloud noise", 2D) = "white" {} }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Overlay" "RenderPipeline"="UniversalPipeline" }
        // Independent multiply. It never reads, writes or consumes the cast shadows' stencil.
        Blend DstColor Zero
        Cull Off
        ZWrite Off
        ZTest Always
        Pass
        {
            Tags { "LightMode"="Universal2D" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_CloudShadeNoise); SAMPLER(sampler_CloudShadeNoise);
            CBUFFER_START(UnityPerMaterial)
                float4 _CloudShadeOffsets; // primary xy, secondary zw (metres)
                float4 _CloudShadeShape;   // coverage, softness, strength, secondary weight
                float4 _CloudShadeScale;   // repeat metres, secondary frequency, ground Z
                float4 _CloudShadeColour;  // the multiply at full strength
            CBUFFER_END
            struct Attributes { float3 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; };
            Varyings vert(Attributes input)
            {
                Varyings output;
                // Clip fill is independent of the culling quad's transform and the camera's zoom step.
                output.positionCS = float4(input.positionOS.xy * 2, 0, 1);
                return output;
            }
            half4 frag(Varyings input) : SV_Target
            {
                // World-anchored: rebuild the ground point under this pixel from the camera's own matrices.
                float2 uv = GetNormalizedScreenSpaceUV(input.positionCS);
                float3 nearWorld = ComputeWorldSpacePosition(uv, UNITY_NEAR_CLIP_VALUE, UNITY_MATRIX_I_VP);
                #if UNITY_REVERSED_Z
                    float farDepth = 0;
                #else
                    float farDepth = 1;
                #endif
                float3 farWorld = ComputeWorldSpacePosition(uv, farDepth, UNITY_MATRIX_I_VP);
                float3 ray = farWorld - nearWorld;
                if (ray.z == 0) return half4(1, 1, 1, 1);
                float2 world = (nearWorld + ray * ((_CloudShadeScale.z - nearWorld.z) / ray.z)).xy;
                float2 primary = (world - _CloudShadeOffsets.xy) / _CloudShadeScale.x;
                float2 secondary = (world * _CloudShadeScale.y - _CloudShadeOffsets.zw) / _CloudShadeScale.x;
                float noise = lerp(SAMPLE_TEXTURE2D(_CloudShadeNoise, sampler_CloudShadeNoise, primary).r,
                    SAMPLE_TEXTURE2D(_CloudShadeNoise, sampler_CloudShadeNoise, secondary).r, _CloudShadeShape.w);
                float edge = 1 - _CloudShadeShape.x;
                float shade = smoothstep(edge - _CloudShadeShape.y, edge + _CloudShadeShape.y, noise) * _CloudShadeShape.z;
                return half4(lerp(float3(1, 1, 1), _CloudShadeColour.rgb, shade), 1);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
