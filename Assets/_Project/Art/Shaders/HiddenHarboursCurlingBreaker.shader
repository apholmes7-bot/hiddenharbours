Shader "Hidden/HiddenHarbours/CurlingBreaker"
{
    // ADR 0040 B2 — the bounded curling sheet. Two passes, drawn by CurlingBreakerPasses from the
    // pooled meshes CurlingBreakerRegion writes each frame:
    //   0 HHCurlMask  — the handoff band into the reduced R8 target the water reads; the water fades
    //                   its flat lip and barrel under it (13 lines in HiddenHarboursWater, nothing else).
    //   1 HHCurlSheet — the sheet itself, into the water's own colour and depth, so hulls and the
    //                   curl hide each other by depth, and never inside a hull (the interior guard).
    // Its colours are the sea material's surf colours, copied each frame: the curl is the surf's own
    // paint, raised. uv: x along the section (0 front .. 1 landing), y along the shore, z the
    // column's alpha (taper × presence), w how far the lip has whitened into the landing.
    Properties
    {
        _SurfLipColor ("Lip colour (the sea's)", Color) = (1, 1, 1, 1)
        _PaletteFoam ("Foam colour (the sea's)", Color) = (1, 1, 1, 1)
        _SurfBarrelColor ("Barrel colour (the sea's)", Color) = (0, 0, 0, 0)
        _SurfAgeStrength ("Foam ageing strength (the sea's)", Range(0, 1)) = 0
        _SurfBarrelShade ("Barrel shade (the sea's)", Range(0, 1)) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        Cull Off

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _SurfLipColor;
            float4 _PaletteFoam;
            float4 _SurfBarrelColor;
            float _SurfAgeStrength;
            float _SurfBarrelShade;
        CBUFFER_END

        struct Attributes
        {
            float3 positionOS : POSITION;
            float4 uv : TEXCOORD0;
        };

        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float4 uv : TEXCOORD0;
        };

        Varyings vert(Attributes input)
        {
            Varyings output;
            output.positionCS = TransformObjectToHClip(input.positionOS);
            output.uv = input.uv;
            return output;
        }
        ENDHLSL

        Pass
        {
            Name "HHCurlMask"
            ZTest Always
            ZWrite Off
            BlendOp Max
            Blend One One
            ColorMask R

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment fragMask

            float4 fragMask(Varyings input) : SV_Target
            {
                return float4(saturate(input.uv.z), 0.0, 0.0, 0.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "HHCurlSheet"
            ZTest LEqual
            ZWrite On
            Blend Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment fragSheet

            // The hull interior guard the water pass reads: bound globally by the feature, and to a
            // 1x1 black fallback by IsoFacetHullRegistry before it has ever run.
            Texture2D<float> _HHHullGuardTex;

            // The 4x4 ordered-dither matrix: the water target is opaque, so the column alpha becomes
            // pixel coverage and a tapered end thins out in whole pixels instead of blending.
            static const float kDither4x4[16] =
            {
                 0.0,  8.0,  2.0, 10.0,
                12.0,  4.0, 14.0,  6.0,
                 3.0, 11.0,  1.0,  9.0,
                15.0,  7.0, 13.0,  5.0
            };

            float4 fragSheet(Varyings input, FRONT_FACE_TYPE face : FRONT_FACE_SEMANTIC) : SV_Target
            {
                int2 pixel = int2(input.positionCS.xy);
                if (_HHHullGuardTex.Load(int3(pixel, 0)) > 0.5)
                    discard;
                float threshold = (kDither4x4[(pixel.y & 3) * 4 + (pixel.x & 3)] + 0.5) / 16.0;
                clip(saturate(input.uv.z) - threshold);

                // The lip is the sea's lip colour, walking to its foam as it lands; the underside,
                // seen where the section folds over itself, is the barrel's shade of that lip.
                float whiten = saturate(_SurfAgeStrength) * saturate(input.uv.w);
                float3 lip = whiten > 0.001 ? lerp(_SurfLipColor.rgb, _PaletteFoam.rgb, whiten)
                                            : _SurfLipColor.rgb;
                float3 colour = IS_FRONT_VFACE(face, true, false)
                    ? lip
                    : lerp(lip, _SurfBarrelColor.rgb, saturate(_SurfBarrelShade) * _SurfBarrelColor.a);
                return float4(colour, 1.0);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
