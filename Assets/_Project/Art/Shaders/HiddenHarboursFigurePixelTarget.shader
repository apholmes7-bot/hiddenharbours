// HiddenHarboursFigurePixelTarget.shader — the small passes of the figure pixel look's route 1
// (docs/design/figure-pixel-target.md §3; IsoFacetFigureTarget records them, GameConfig
// .FigurePixelTarget switches them, ship default OFF — with the switch OFF nothing binds this shader).
//
// The figures' own target holds one texel per art pixel (1/32 m); a texel covers m × m screen pixels,
// laid out by _HHFigurePixelMap = (offset x, offset y, 1/m, m) — screen pixel = offset + texel × m.
// Its stencil, as the facet pass leaves it: 1 where a figure shows, 2 a hull or deck alone (context),
// 3 a figure some nearer hull or deck covers, 0 nothing (or a figure's own ink ring).
//   0. DEPTH: the hull feature's private z-buffer (written by the displaced water before any hull)
//      read at the screen pixel under each texel's centre, so a figure the lifted sea covers stays
//      covered at the new size. A raw depth copy: the target's projection keeps the camera's depth
//      rows, so the same point lands at the same depth in both.
//   1. EXPORT: the resolved low picture copied out wherever the stencil's context bit is clear —
//      0 and 1. 2 (a hull with no figure behind it) stays out, so the full-size hulls are never
//      replaced; 3 is pass 3's.
//   2. COVERAGE: 1 on each texel where a figure shows (stencil 1), else the clear 0 — her visible
//      art pixels, which pass 3 reads.
//   3. EDGE: stencil 3 copied out only on a texel that shares an edge with one where she shows (the
//      4-neighbourhood): the covering hull's own texel squares off the occlusion edge beside her
//      visible pixels, one art pixel deep (the owner's D4 of 10-15). Deeper under the house nothing
//      is exported, so the full-size house draws over her hidden body exactly as it does with the
//      switch OFF (the owner's ruling of 10-16).
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
            Texture2D<float>  _HHFigureCoverageTex; // pass 2's output: 1 where a figure shows, texel size

            // The edge's neighbourhood: the four texels that share an edge with this one. Fixed bound.
            static const int2 kBeside[4] = { int2(1, 0), int2(-1, 0), int2(0, 1), int2(0, -1) };

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

            float fragCoverage (Varyings input) : SV_Target
            {
                return 1;
            }

            float4 fragEdge (Varyings input) : SV_Target
            {
                int2 t = int2(input.positionCS.xy);
                uint w, h;
                _HHFigureCoverageTex.GetDimensions(w, h);
                bool beside = false;
                [unroll]
                for (int k = 0; k < 4; k++)
                {
                    int2 q = t + kBeside[k];
                    if (q.x >= 0 && q.y >= 0 && q.x < (int)w && q.y < (int)h &&
                        _HHFigureCoverageTex.Load(int3(q, 0)) >= 0.5)
                        beside = true;
                }
                if (!beside) discard;
                return _HHFigureLowTex.Load(int3(t, 0));
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
                Ref 0
                ReadMask 2
                Comp Equal
            }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment fragExport
            #pragma target 3.5
            ENDHLSL
        }

        Pass
        {
            Name "HHFigureTargetCoverage"
            ZTest Always
            ZWrite Off
            Cull Off
            Blend Off
            Stencil
            {
                Ref 1
                ReadMask 3
                Comp Equal
            }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment fragCoverage
            #pragma target 3.5
            ENDHLSL
        }

        Pass
        {
            Name "HHFigureTargetEdge"
            ZTest Always
            ZWrite Off
            Cull Off
            Blend Off
            Stencil
            {
                Ref 3
                ReadMask 3
                Comp Equal
            }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment fragEdge
            #pragma target 3.5
            ENDHLSL
        }
    }
}
