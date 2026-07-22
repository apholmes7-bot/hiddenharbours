// HiddenHarboursIsoFacetOverlay.shader — the in-scene face of a mesh hull (ADR 0022 phase 3).
//
// A cell-sized quad drawn by the 2D renderer's ordinary Universal2D pass. It re-composes THIS
// hull's pixels (keyline included) from the feature's resolved screen texture
// (_HHHullScreenTex, alpha = hull id/255) at its own SV_Position — a 1:1 texel fetch that is
// convention-proof, because the offscreen passes and this quad render through the same camera
// projection and viewport transform.
//
// The quad is the thing that SORTS: it sits in the hull's SortingGroup like a sprite would, so
// a crew sprite above the boat covers hull AND keyline, and the water below is covered by both
// — whole-object sorting, exactly as good as the baked-sprite path (ADR 0022 "Unchanged").
//
// The hull-id filter is what keeps two OVERLAPPING mesh hulls honest: each quad re-composes
// only its own hull's pixels, so hull A's image never rides along at hull B's sorting position.
Shader "HiddenHarbours/IsoFacetOverlay"
{
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }

        Pass
        {
            Name "HHHullOverlay"
            Tags { "LightMode" = "Universal2D" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            // Global, bound by IsoFacetHullFeature after the keyline resolve. Before the feature
            // has ever run, IsoFacetHullRegistry binds a 1x1 CLEAR fallback so this discards
            // everywhere instead of sampling the grey unbound placeholder.
            Texture2D<float4> _HHHullScreenTex;

            // Per draw via MaterialPropertyBlock: this hull's id, already divided by 255.
            float _HullId;

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
                float4 c = _HHHullScreenTex.Load(int3(int2(i.positionCS.xy), 0));
                // Only THIS hull's pixels (ids are small integers over 255 — compare in id space
                // with a half-step tolerance so 8-bit alpha rounding cannot drop pixels).
                clip(0.5 - abs(c.a - _HullId) * 255.0);
                clip(c.a - 0.5 / 255.0);
                return float4(c.rgb, 1.0);
            }
            ENDHLSL
        }
    }
}
