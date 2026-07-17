// SpriteObjectMatrixProbe.shader — TEST-ONLY. Not shipped: it lives under Assets/Tests/, is referenced by no
// scene, prefab or Resources folder, and is only ever found via Shader.Find from an EditMode test.
//
// It encodes the sprite's OBJECT ORIGIN in world space (unity_ObjectToWorld's translation) into the red channel
// so SpriteObjectMatrixGuardTests can read it back off a rendered RenderTexture. See that test for WHY the
// flower shader depends on this holding.
Shader "HiddenHarbours/_SpriteObjectMatrixProbe"
{
    Properties
    {
        [NoScaleOffset] _MainTex ("Sprite", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" "RenderPipeline"="UniversalPipeline" }
        Cull Off
        ZWrite Off

        Pass
        {
            Tags { "LightMode" = "Universal2D" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float3 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 probe : TEXCOORD0; };

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _Dummy;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS);
                // The sprite's OBJECT ORIGIN in world space. If SpriteRenderer geometry were baked to world
                // space with an identity object matrix, this would read (0,0) for EVERY sprite.
                OUT.probe = mul(unity_ObjectToWorld, float4(0, 0, 0, 1)).xy;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // encode root.x: x = -2 -> 0.3, x = +3 -> 0.65, x = 0 -> 0.5. Decoded by the test.
                return half4(saturate(IN.probe.x * 0.1 + 0.5), 0, 0, 1);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
