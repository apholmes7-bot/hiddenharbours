Shader "HiddenHarbours/DayNight"
{
    // The global day/night MULTIPLY overlay (ADR 0013 decision (b)). A single screen-filling quad,
    // placed in front of the active camera by DayNightController and drawn ABOVE all world sprites,
    // multiplies the whole composited frame by _DayNightTint. Because every pixel the camera drew
    // (unlit sprites, tilemaps, the self-lit water + grass) is multiplied in ONE place, the scene
    // darkens and warms/cools together — no layer can drift bright while the rest goes dark. The
    // screen-space HUD canvas renders after the camera, so it is unaffected and stays readable.
    Properties
    {
        // Driven per-frame by DayNightController (global Shader.SetGlobalColor + a per-renderer
        // MaterialPropertyBlock). White = full daylight (no darkening); dark blue = night.
        _DayNightTint ("Day/Night tint (multiply)", Color) = (1, 1, 1, 1)
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Overlay" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }

        // MULTIPLY: result = SrcColor * DstColor (the frag emits the tint; the framebuffer is multiplied).
        Blend DstColor Zero
        Cull Off
        ZWrite Off
        ZTest Always

        Pass
        {
            // The URP 2D renderer draws this pass; sorted last via the renderer's high sortingOrder.
            Tags { "LightMode" = "Universal2D" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float3 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _DayNightTint;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = GetVertexPositionInputs(IN.positionOS).positionCS;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // Emit the tint as the source colour; the DstColor/Zero blend multiplies the frame by it.
                return half4(_DayNightTint.rgb, 1.0);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
