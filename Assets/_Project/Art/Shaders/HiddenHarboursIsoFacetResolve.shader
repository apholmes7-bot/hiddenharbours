// HiddenHarboursIsoFacetResolve.shader — the rig's keyline post-pass as a FULLSCREEN shader
// (ADR 0022 phase 3; the spike ran this on the CPU for exactness, production wants it here).
//
// Two rules, verbatim from the rigs' shared rasteriser:
//   1. DEPTH-EDGE DARKENING (doEdge): where two adjacent solid pixels differ in TRUE view depth
//      by more than 0.30 m, the FAR one is replaced by its colour darkened two RINDEX ramp
//      steps. The darkened colour was precomputed per (material, ramp index) by
//      IsoFacetMath.BuildDarkenedRamps and drawn into _HHDarkTex by the facet pass, so here it
//      is a plain load — RINDEX's aliased-ramp resolution included.
//   2. 1 PX KEYLINE: an empty pixel with any solid 4-neighbour becomes the neighbour's keyline
//      colour (and carries that neighbour's hull id, so the right overlay quad re-composes it).
//
// Both rules are neighbour-symmetric, so render-target y-orientation cannot change the result.
// Output alpha carries the hull id (id/255); 0 = nothing here. Runs once per camera into the
// persistent _HHHullScreenTex that the in-scene overlay quads sample.
Shader "Hidden/HiddenHarbours/IsoFacetResolve"
{
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }

        Pass
        {
            Name "HHHullKeylineResolve"
            ZTest Always
            ZWrite Off
            Cull Off
            Blend Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment frag
            #pragma target 3.5

            // The canonical URP blit include stack (see e.g. URP's own Bloom.shader): URP Core
            // must precede Blit.hlsl or its TEXTURE2D_X macros are undefined.
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            Texture2D<float4> _HHFacetTex;   // rgb = facet colour, a = hull id
            Texture2D<float4> _HHDarkTex;    // rgb = RINDEX darkened colour
            Texture2D<float4> _HHKeyTex;     // rgb = keyline colour
            Texture2D<float>  _HHDepthTex;   // true unbiased view depth, metres

            // The rig's doEdge threshold — a property of the art director's renderer being
            // transcribed (like GAIN and BIAS), not a game tunable.
            #define HH_EDGE_DEPTH_MIN 0.30

            float4 frag (Varyings input) : SV_Target
            {
                uint w, h;
                _HHFacetTex.GetDimensions(w, h);
                int2 p = int2(input.positionCS.xy);

                // 4-neighbourhood offsets. Fixed bound — never an [unroll] over a runtime count.
                const int2 offs[4] = { int2(1, 0), int2(-1, 0), int2(0, 1), int2(0, -1) };

                float4 c = _HHFacetTex.Load(int3(p, 0));
                if (c.a > 0)
                {
                    // Solid: darken if ANY in-bounds solid 4-neighbour is nearer by more than the
                    // threshold (equivalent to the rig's pairwise right/down sweep, which darkens
                    // the far side of each qualifying pair).
                    float d = _HHDepthTex.Load(int3(p, 0));
                    bool darken = false;
                    [unroll]
                    for (int k = 0; k < 4; k++)
                    {
                        int2 q = p + offs[k];
                        if (q.x < 0 || q.y < 0 || q.x >= (int)w || q.y >= (int)h) continue;
                        if (_HHFacetTex.Load(int3(q, 0)).a <= 0) continue;
                        float dn = _HHDepthTex.Load(int3(q, 0));
                        if (abs(d - dn) > HH_EDGE_DEPTH_MIN && d > dn) darken = true;
                    }
                    float3 rgb = darken ? _HHDarkTex.Load(int3(p, 0)).rgb : c.rgb;
                    return float4(rgb, c.a);
                }

                // Empty: flood the 1 px keyline from the first solid 4-neighbour, carrying that
                // neighbour's keyline colour AND hull id.
                [unroll]
                for (int k = 0; k < 4; k++)
                {
                    int2 q = p + offs[k];
                    if (q.x < 0 || q.y < 0 || q.x >= (int)w || q.y >= (int)h) continue;
                    float na = _HHFacetTex.Load(int3(q, 0)).a;
                    if (na > 0)
                        return float4(_HHKeyTex.Load(int3(q, 0)).rgb, na);
                }
                return float4(0, 0, 0, 0);
            }
            ENDHLSL
        }
    }
}
