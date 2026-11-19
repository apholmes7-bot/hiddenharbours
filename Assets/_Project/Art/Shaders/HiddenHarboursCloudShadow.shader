Shader "HiddenHarbours/CloudShadow"
{
    Properties { _CloudNoise ("Runtime cloud noise", 2D) = "white" {} }
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
            TEXTURE2D(_CloudNoise); SAMPLER(sampler_CloudNoise);
            CBUFFER_START(UnityPerMaterial)
                float4 _CloudOffsets;
                float4 _CloudShape; // coverage, softness, alpha, secondary weight
                float4 _CloudScale; // period metres, second frequency, ground Z
            CBUFFER_END
            struct Attributes { float3 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; };
            Varyings vert(Attributes input)
            {
                Varyings output;
                // Clip fill is independent of the culling quad's transform and PPC's zoom step.
                output.positionCS = float4(input.positionOS.xy * 2, 0, 1);
                return output;
            }
            half4 frag(Varyings input) : SV_Target
            {
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
                float2 world = (nearWorld + ray * ((_CloudScale.z - nearWorld.z) / ray.z)).xy;
                float2 primary = (world - _CloudOffsets.xy) / _CloudScale.x;
                float2 secondary = (world * _CloudScale.y - _CloudOffsets.zw) / _CloudScale.x;
                float noise = lerp(SAMPLE_TEXTURE2D(_CloudNoise, sampler_CloudNoise, primary).r,
                    SAMPLE_TEXTURE2D(_CloudNoise, sampler_CloudNoise, secondary).r, _CloudShape.w);
                float edge = 1 - _CloudShape.x;
                float shade = smoothstep(edge - _CloudShape.y, edge + _CloudShape.y, noise) * _CloudShape.z;
                return half4(1 - shade, 1 - shade, 1 - shade, 1);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
