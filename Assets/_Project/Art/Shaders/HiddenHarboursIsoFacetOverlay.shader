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

        // ================================================================================================
        // HHReflect — this hull's REFLECTION in the water (ADR 0027 #8)
        // ================================================================================================
        // Recorded by IsoFacetHullFeature into _HHReflectTex and sampled by the water shader, which warps
        // the lookup by the SAME wave field the hull rides.
        //
        // ⚠️ THE REFLECTION IS THE OVERLAY'S JOB, NOT THE FACET PASS'S. A mesh hull's visible image is not
        // what the facet MRT holds — it is what the KEYLINE RESOLVE made of it, which this quad re-composes
        // from _HHHullScreenTex. Mirroring the facet pass would reflect raw facet colour with no keyline,
        // no darkening and no hull-id filter: recognisably the wrong boat. So the reflection mirrors THIS
        // quad and re-composes from the SAME resolved texture.
        //
        // ⚠️ …which means the quad rasterises MIRRORED but must SAMPLE UNMIRRORED. _HHHullScreenTex holds
        // the hull where the hull actually is; there is nothing at the reflection's screen position. The
        // mirror is a vertical flip about the pivot's screen row, so the fetch flips back about that same
        // row: sampleY = 2*pivotRow - fragmentY. The pivot's row is computed ONCE in the vertex stage
        // through ComputeScreenPos (which carries the render target's Y-flip convention) and is constant
        // across the quad under this project's unrotated orthographic camera.
        //
        // ⚠️ MEMBERSHIP IS THE RENDERING-LAYER BIT, NOT THIS TAG — every mesh hull shares this shader.
        // Only hulls carrying ReflectionRegistry.RenderingLayer (i.e. those a ReflectiveObject was put on)
        // are in the filtered list.
        Pass
        {
            Name "HHHullReflect"
            Tags { "LightMode" = "HHReflect" }

            Blend One OneMinusSrcAlpha   // premultiplied: overlapping reflectors composite in ONE target
            ZWrite Off
            ZTest Always
            Cull Off

            HLSLPROGRAM
            #pragma vertex reflectVert
            #pragma fragment reflectFrag
            #pragma target 3.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Assets/_Project/Art/Shaders/Include/ReflectMirror.hlsl"

            Texture2D<float4> _HHHullScreenTex;

            float  _HullId;
            float4 _HHReflectOrigin;   // xy = the published ground-contact pivot, w = 1 when published
            float  _HHReflectLit;      // 1 = night light content (rides the post-grade bucket, §11.6)
            float4 _DayNightTint;      // GLOBAL (DayNightController) — the overlay this pass compensates for

            struct ReflectAttributes { float4 positionOS : POSITION; };

            struct ReflectVaryings
            {
                float4 positionCS : SV_POSITION;
                // The screen ROW of the mirror axis, in the same pixel space as SV_POSITION. One scalar,
                // constant across the quad — the fetch flips back about it.
                float  pivotRow   : TEXCOORD0;
            };

            ReflectVaryings reflectVert (ReflectAttributes v)
            {
                ReflectVaryings o;
                float3 wp = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = HHReflectPositionCS(wp, _HHReflectOrigin);

                // The axis, projected. ComputeScreenPos carries the render-target Y flip, so this stays
                // the same row on every graphics API.
                float4 axisCS = TransformWorldToHClip(float3(_HHReflectOrigin.xy, wp.z));
                float4 axisSP = ComputeScreenPos(axisCS);
                o.pivotRow = (axisSP.y / max(axisSP.w, 1e-6)) * _ScreenParams.y;
                return o;
            }

            float4 reflectFrag (ReflectVaryings i) : SV_Target
            {
                // Flip the fetch back about the axis row: this fragment is drawing the reflection of the
                // hull pixel that sits as far ABOVE the waterline as this one is below it.
                int2 px = int2(i.positionCS.x, 2.0 * i.pivotRow - i.positionCS.y);
                if (any(px < 0) || any(px >= int2(_ScreenParams.xy))) discard;

                float4 c = _HHHullScreenTex.Load(int3(px, 0));
                // Only THIS hull's pixels — the same id filter the in-scene pass uses, for the same
                // reason: two overlapping hulls must not reflect each other's image.
                clip(0.5 - abs(c.a - _HullId) * 255.0);
                clip(c.a - 0.5 / 255.0);

                // The resolved texture stores the hull OPAQUE (the in-scene pass returns alpha 1), so the
                // reflection's coverage is 1 wherever the id matched.
                return HHReflectPremultiply(c.rgb, 1.0, _HHReflectLit, _DayNightTint.rgb);
            }
            ENDHLSL
        }
    }
}
