// HiddenHarboursFigurePixelTarget.shader — the two small passes of the figure pixel look's route 1
// (docs/design/figure-pixel-target.md §3; IsoFacetFigureTarget records them, GameConfig
// .FigurePixelTarget switches them, ship default OFF — with the switch OFF nothing binds this shader).
//
// The figures' own target holds one texel per art pixel (1/32 m); a texel covers m × m screen pixels,
// laid out by _HHFigurePixelMap = (offset x, offset y, 1/m, m) — screen pixel = offset + texel × m.
//   0. DEPTH: the hull feature's private z-buffer (written by the displaced water before any hull)
//      read at the screen pixel under each texel's centre, so a figure the lifted sea covers stays
//      covered at the new size. A raw depth copy: the target's projection keeps the camera's depth
//      rows, so the same point lands at the same depth in both.
//   1. EXPORT: the resolved low picture copied out wherever the stencil is not "context alone" —
//      0 (empty, or a figure's own ink ring), 1 (a figure) and 3 (a figure some nearer hull or deck
//      covers: that hull's own texel, the art-pixel occlusion edge of the owner's D4 of 10-15).
//      2 (a hull with no figure behind it) stays out, so the full-size hulls are never replaced.
Shader "Hidden/HiddenHarbours/FigurePixelTarget"
{
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }

        HLSLINCLUDE
            // The canonical URP blit include stack (as the resolve shader has it): URP Core must
            // precede Blit.hlsl or its TEXTURE2D_X macros are undefined.
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float4 _HHFigurePixelMap;               // (offset x, offset y, 1/m, m); w = 0: no target
            Texture2D<float>  _HHFigureSceneDepth;  // the private z-buffer, full size, raw device depth
            Texture2D<float4> _HHFigureLowTex;      // the resolved figures and context, texel size

            float fragDepth (Varyings input) : SV_Depth
            {
                // positionCS.xy is the texel's centre (t + 0.5); the screen pixel under it is
                // offset + t·m + m/2, floored — the pixel FigurePixelPlan.SampleScreenX/Y names.
                int2 p = int2(floor(_HHFigurePixelMap.xy + input.positionCS.xy * _HHFigurePixelMap.w));
                uint w, h;
                _HHFigureSceneDepth.GetDimensions(w, h);
                if (any(p < 0) || p.x >= (int)w || p.y >= (int)h) return UNITY_RAW_FAR_CLIP_VALUE;
                return _HHFigureSceneDepth.Load(int3(p, 0));
            }

            float4 fragExport (Varyings input) : SV_Target
            {
                return _HHFigureLowTex.Load(int3(int2(input.positionCS.xy), 0));
            }
        ENDHLSL

        Pass
        {
            Name "HHFigureTargetDepth"
            ZTest Always
            ZWrite On
            Cull Off
            ColorMask 0
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment fragDepth
            #pragma target 3.5
            ENDHLSL
        }

        Pass
        {
            Name "HHFigureTargetExport"
            ZTest Always
            ZWrite Off
            Cull Off
            Blend Off
            Stencil
            {
                Ref 2
                ReadMask 3
                Comp NotEqual
            }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment fragExport
            #pragma target 3.5
            ENDHLSL
        }
    }
}
