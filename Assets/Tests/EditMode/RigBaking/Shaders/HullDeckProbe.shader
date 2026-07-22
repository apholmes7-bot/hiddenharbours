// TEST-ONLY probe for the deck-occupant depth contract (ADR 0022 phase 3, owner decision
// 2026-07-21): a flat quad drawn through IsoFacetHullFeature's HHHullDeck renderer list, i.e.
// depth-tested per-pixel against the hull's PRIVATE z-buffer exactly as a future
// character-on-deck billboard will be. Honours the facet MRT contract (colour+id, darkened,
// keyline, true depth). Plain ZTest LEqual — the render-graph camera-matrices path handles
// reversed-Z automatically; the probe test fails loudly in both directions if it does not.
Shader "HiddenHarbours/_HullDeckProbe"
{
    Properties
    {
        _ProbeColor ("Probe colour, pre linearised", Color) = (1, 0, 1, 1)
        _KeyColor ("Keyline colour, pre linearised", Color) = (0, 0, 0, 1)
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }

        Pass
        {
            Name "HHHullDeckProbe"
            Tags { "LightMode" = "HHHullDeck" }

            Cull Off
            ZWrite On
            ZTest LEqual
            Blend Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            float4 _ProbeColor;
            float4 _KeyColor;
            float  _HullId;      // carrying hull's id / 255, via MaterialPropertyBlock

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float depth : TEXCOORD0;
            };
            struct FragOut
            {
                float4 facet : SV_Target0;
                float4 dark  : SV_Target1;
                float4 key   : SV_Target2;
                float  depth : SV_Target3;
            };

            Varyings vert (Attributes v)
            {
                Varyings o;
                float3 wp = mul(unity_ObjectToWorld, float4(v.positionOS.xyz, 1.0)).xyz;
                o.depth = wp.z;
                o.positionCS = TransformWorldToHClip(wp);
                return o;
            }

            FragOut frag (Varyings i)
            {
                FragOut o;
                o.facet = float4(_ProbeColor.rgb, _HullId);
                o.dark  = float4(_ProbeColor.rgb, 1.0);
                o.key   = float4(_KeyColor.rgb, 1.0);
                o.depth = i.depth;
                return o;
            }
            ENDHLSL
        }
    }
}
