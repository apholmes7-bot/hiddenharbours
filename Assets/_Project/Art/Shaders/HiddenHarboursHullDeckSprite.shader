// HiddenHarboursHullDeckSprite.shader — A PERSON STANDING ON A MESH HULL (ADR 0022 phase 3's deck
// contract, owner decision 2026-07-21; owner playtest 2026-08-07 item 4: "sprites visible THROUGH
// closed cabins").
//
// WHAT IT IS FOR. A mesh hull's in-scene face is ONE cell-sized overlay quad at ONE sorting order
// (IsoFacetOverlay), so a crew SPRITE sorted against it is either wholly in front of the boat or
// wholly behind her — and behind is invisible, since the sole they stand on is hull pixels too.
// Every shipped BoatVisualDef carries SortingOrder 1, below the decor band's floor, so the
// Y-sorted fisher won that comparison everywhere and was drawn straight over the wheelhouse.
//
// No sorting order can fix it, because sorting is per OBJECT and the answer is per PIXEL: under a
// 3/4 bake a point's screen height is alongView*sin(elev) + height*cos(elev) while its distance is
// alongView*cos(elev) - height*sin(elev) — independent combinations of the same pair, so no cut
// through the picture separates "nearer than the fisher" from "further". The feature already built
// the way out: its facet pass keeps a PRIVATE z-buffer attached for a second renderer list, and any
// shader with an HHHullDeck pass is depth-tested per-pixel against the hull geometry and writes
// depth back. So the crew is not sorted against the boat at all — they are composited INTO her, and
// the wheelhouse occludes them for the same reason it occludes her own gunwale.
//
// THE MRT CONTRACT (IsoFacetHullFeature, verbatim — a deck pass must honour all four):
//   SV_Target0 = (colour, carrying hull's id / 255)   SV_Target1 = darkened colour
//   SV_Target2 = keyline colour                       SV_Target3 = TRUE unbiased view depth (world z, m)
// The id matters: the resolve keeps it in alpha and each hull's overlay quad re-composes only its
// OWN id, so the crew must carry the id of the boat they are standing on or they are dropped.
//
// THE SLOPE IS THE POINT. The quad is upright on SCREEN but a standing body is NOT at one distance:
// a fisher's head is nearer the camera than their boots by height*sin(elev). So depth is
// interpolated up the card — _FeetZ at the boots, falling by _DepthPerScreenRise per metre of screen
// rise (DeckAreaMath.DepthPerScreenRise = tan(elev)). A flat card would make a wheelhouse that
// clears the boots clear the head too, which is the same whole-object answer in a new place.
//
// ALPHA IS A CUTOUT, NEVER A BLEND. SV_Target0's alpha is the hull id, so it cannot also carry
// coverage, and the MRT is Blend Off. The character sheets are pixel art with binary alpha (no AA),
// so a clip is exact rather than a compromise.
//
// LIGHTING. None, deliberately: the crew is composited into the hull's image and shares its
// treatment — the fixed screen-space key light of the facet pass, then the world day/night tint
// which draws over every world sprite afterwards. A crew lit by the sprite path's point lights while
// the hull under them was not would read as a cut-out pasted on the boat.
//
// SHADER CAUTIONS honoured (this project has lost hours to magenta shaders): no operator characters
// in Property display strings; no [unroll] over runtime bounds; force-compiled headless by
// HullDeckSpriteShaderCompileGuardTests so a break fails red instead of shipping magenta.
Shader "HiddenHarbours/HullDeckSprite"
{
    Properties
    {
        [NoScaleOffset] _MainTex ("Character sheet", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)
        _Cutoff ("Alpha cutoff", Range(0, 1)) = 0.5
        _KeyColor ("Keyline colour, pre linearised", Color) = (0, 0, 0, 1)
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }

        Pass
        {
            Name "HHHullDeckSprite"
            // Drawn ONLY by IsoFacetHullFeature's deck renderer list, against the hulls' private
            // z-buffer. It must never appear in the 2D renderer's own draw: writing the scene's
            // shared depth buffer punches holes in every later sprite that z-tests.
            Tags { "LightMode" = "HHHullDeck" }

            Cull Off
            ZWrite On
            // Plain LEqual with clear-depth-1 — the STANDARD render-graph camera-matrices path,
            // where Unity handles reversed-Z itself. The spike's hand-built command buffer needed
            // GEqual and a 0 clear; that convention does NOT carry over here, and
            // IsoFacetUrpPassTests' deck probe fails loudly in both directions if it is got wrong.
            ZTest LEqual
            Blend Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            // Per draw via MaterialPropertyBlock (HullDeckOccupant.Apply).
            float4 _Color;
            float  _Cutoff;
            float4 _KeyColor;
            float  _HullId;              // the carrying hull's id / 255 — the facet alpha
            float  _FeetZ;               // view depth (world z, m) of the boots, in the HULL's frame
            float  _FeetY;               // world y the boots stand at, the slope's origin
            float  _DepthPerScreenRise;  // tan(elev): how much nearer one metre UP THE SCREEN is

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float  depth      : TEXCOORD1;   // true unbiased view depth, metres
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

                // The body's own lean into the scene. Derived from SCREEN rise rather than from a
                // separate height input so the lean survives the ride: whatever moves the card up
                // the screen — the deck's heave, the rider's lift, the sheet's own pivot — carries
                // exactly the depth that rise is worth, with nothing to keep in step by hand.
                o.depth = _FeetZ - (wp.y - _FeetY) * _DepthPerScreenRise;

                // The z-TEST must use the same number the buffer is judged on, so the depth goes
                // through the projection rather than alongside it.
                o.positionCS = TransformWorldToHClip(float3(wp.xy, o.depth));
                o.uv = v.uv;
                return o;
            }

            FragOut frag (Varyings i)
            {
                float4 c = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv) * _Color;
                // Cutout, not blend — see the header. Below the cutoff nothing is written at all,
                // so an empty sheet pixel leaves the hull behind it untouched AND leaves the
                // private z-buffer alone, which is what lets the hull draw through the gaps in a
                // figure rather than being punched out by their bounding card.
                clip(c.a - _Cutoff);

                FragOut o;
                o.facet = float4(c.rgb, _HullId);
                // A sprite has no RINDEX ramp to step down, so it offers its own colour as the
                // "darkened" one. That makes the resolve's depth-edge rule a no-op ON THE CREW's
                // pixels while leaving it fully live on the hull's — honest, and strictly better
                // than inventing a darkening factor the rig never specified.
                o.dark  = float4(c.rgb, 1.0);
                o.key   = float4(_KeyColor.rgb, 1.0);
                o.depth = i.depth;
                return o;
            }
            ENDHLSL
        }
    }
}
