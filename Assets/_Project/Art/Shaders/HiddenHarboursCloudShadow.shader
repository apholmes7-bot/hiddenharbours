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
            struct Varyings { float4 positionCS : SV_POSITION; float2 world : TEXCOORD0; };
            Varyings vert(Attributes input)
            {
                Varyings output;
                // The quad is laid in the camera plane, sized to the view (with a margin) each frame. The
                // ground point under each corner is found along the camera's own forward ray (an orthographic
                // camera: every ray is parallel), so the pattern is pinned to the world, not to the screen.
                // The map is linear in the corner, so the interpolated point is exact at every pixel.
                float3 corner = TransformObjectToWorld(input.positionOS);
                float3 forward = -UNITY_MATRIX_V[2].xyz;
                float along = abs(forward.z) > 1e-5 ? (_CloudShadeScale.z - corner.z) / forward.z : 0;
                output.world = (corner + forward * along).xy;
                output.positionCS = TransformWorldToHClip(corner);
                return output;
            }
            half4 frag(Varyings input) : SV_Target
            {
                float2 primary = (input.world - _CloudShadeOffsets.xy) / _CloudShadeScale.x;
                float2 secondary = (input.world * _CloudShadeScale.y - _CloudShadeOffsets.zw) / _CloudShadeScale.x;
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
