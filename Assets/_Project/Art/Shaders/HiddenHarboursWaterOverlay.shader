// HiddenHarboursWaterOverlay.shader — the in-scene face of the DISPLACED water surface
// (ADR 0023 phase 2, the ADR 0022 overlay pattern verbatim).
//
// The displaced water mesh draws OFF-SCREEN (the water pass of IsoFacetHullFeature, sharing the
// facet passes' private depth buffer) into _HHWaterScreenTex; this quad re-composes those pixels
// in-scene at its own SV_Position — a 1:1 texel fetch that is convention-proof, because the
// off-screen pass and this quad render through the same camera projection and viewport transform.
//
// The quad is the thing that SORTS: it sits in a SortingGroup ("sort as 2D" — mesh renderers do
// NOT sort against sprites by sortingOrder on their own) at the exact sorting slot the flat water
// sprite occupies, so boats/characters/props stack against the displaced sea exactly as they stack
// against the flat one. Alpha carries the water's own TRANSLUCENCY (see-through shallows), not an
// id — unlike the hull overlay there is only one sea, so no id filter is needed; the blend below
// reproduces the flat pass's SrcAlpha composite over the terrain.
Shader "HiddenHarbours/WaterOverlay"
{
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }

        Pass
        {
            Name "HHWaterOverlay"
            Tags { "LightMode" = "Universal2D" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            // Global, bound by IsoFacetHullFeature after the water pass. Before the feature has
            // ever run (or with it disabled) DisplacedWaterRegistry binds a 1x1 CLEAR fallback so
            // this samples transparent instead of the grey unbound placeholder.
            Texture2D<float4> _HHWaterScreenTex;

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings vert (Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                return o;
            }

            float4 frag (Varyings i) : SV_Target
            {
                // Straight rgba: rgb = the displaced surface's painted colour, a = its own
                // translucency (0 where no water fragment landed — the blend draws nothing there).
                return _HHWaterScreenTex.Load(int3(int2(i.positionCS.xy), 0));
            }
            ENDHLSL
        }
    }
}
