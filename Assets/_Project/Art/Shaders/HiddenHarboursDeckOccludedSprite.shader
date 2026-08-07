// HiddenHarboursDeckOccludedSprite.shader — a sprite that a BOAT can stand in front of.
//
// THE DEFECT (owner playtest 2026-08-07): "rider/player sprites visible THROUGH closed cabins" on
// hulls with a cockpit and doors. The figure on deck is an ordinary sprite Y-sorted above the
// hull's whole-object sorting slot, so no sorting order can ever put a wheelhouse in front of them:
// sorting is per OBJECT, and "is the boat between the camera and the fisher?" is per PIXEL. Only
// the hull can answer that, and she already does — her facet pass runs against a private z-buffer.
//
// So the hull answers it there (HiddenHarboursIsoFacet.shader's deck-occupant split): geometry
// NEARER the camera than the figure standing on her deck is written into the resolved screen
// texture's alpha with that hull's SECOND id. This shader reads that alpha at its own screen pixel
// and DISCARDS where it matches. The figure keeps her sorting slot, her Y-sort, her flip and her
// tint; she simply is not drawn where the boat is genuinely in front of her.
//
// ⚠️ EVERYTHING ELSE IS URP'S OWN Sprite-Unlit-Default, structurally verbatim — the same
// Core2D/2DCommon includes, the same UnityFlipSprite and SetUpSpriteInstanceProperties, the same
// `input.color * _Color * unity_SpriteColor`, the same blend, the same legacy fallback properties.
// That is deliberate and load-bearing: this material replaces the one the on-deck figure draws
// through, so ANY difference in how it handles tint, flip, instancing or premultiplication would
// show up as the fisher changing appearance the moment she steps aboard. The only edit is the
// discard. Re-derive from URP's shader, never from memory, if this ever needs to move.
//
// _HHDeckOccluderId is written per RENDERER through a MaterialPropertyBlock (DeckRiderVisual) and
// is 0 whenever there is nothing to hide behind — ashore, on a sprite hull, on a hull with no
// occupant. Hull ids start at 1, so 0 can never match: at 0 this is byte-for-byte an unlit sprite,
// which is both the A/B and the failure mode if the write is ever missed.
//
// SHADER CAUTIONS honoured (this project lost hours to magenta shaders): no operator characters in
// Property display strings; no [unroll] over runtime bounds; force-compiled headless by
// IsoFacetShaderCompileGuardTests so a break fails CI red rather than shipping magenta.
Shader "HiddenHarbours/DeckOccludedSprite"
{
    Properties
    {
        _MainTex ("Sprite Texture", 2D) = "white" {}
        [MaterialToggle] _ZWrite("ZWrite", Float) = 0

        // Written per renderer by DeckRiderVisual: the id of the hull geometry that hides this
        // sprite, already divided by 255. 0 means nothing does.
        [HideInInspector] _HHDeckOccluderId ("Deck occluder id over 255", Float) = 0

        // Legacy properties, kept exactly as URP's own sprite shader keeps them, so a material using
        // this can gracefully fall back to the legacy sprite shader.
        [HideInInspector] _Color ("Tint", Color) = (1,1,1,1)
        [HideInInspector] PixelSnap ("Pixel snap", Float) = 0
        [HideInInspector] _RendererColor ("RendererColor", Color) = (1,1,1,1)
        [HideInInspector] _AlphaTex ("External Alpha", 2D) = "white" {}
        [HideInInspector] _EnableExternalAlpha ("Enable External Alpha", Float) = 0
    }

    SubShader
    {
        Tags {"Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" }

        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        Cull Off
        ZWrite [_ZWrite]

        Pass
        {
            Tags { "LightMode" = "Universal2D" }

            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"

            #pragma vertex UnlitVertex
            #pragma fragment UnlitFragment

            struct Attributes
            {
                COMMON_2D_INPUTS
                half4 color : COLOR;
                UNITY_SKINNED_VERTEX_INPUTS
            };

            struct Varyings
            {
                COMMON_2D_OUTPUTS
                half4 color : COLOR;
            };

            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/2DCommon.hlsl"

            // GPU Instancing
            #pragma multi_compile_instancing
            #pragma multi_compile _ DEBUG_DISPLAY SKINNED_SPRITE

            // The hull screen texture the facet pass resolves — bound globally BEFORE any sprite
            // draws (IsoFacetHullFeature injects at BeforeRenderingSprites on the lowest sorting
            // layer), with a 1x1 CLEAR fallback bound before it has ever run, so an early Load reads
            // alpha 0 and matches nothing.
            Texture2D<float4> _HHHullScreenTex;

            // NOTE: Do not ifdef the properties here as SRP batcher can not handle different layouts.
            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
            CBUFFER_END

            // Per RENDERER (MaterialPropertyBlock), deliberately outside UnityPerMaterial: it is a
            // genuine per-draw value and folding it into the batched block would share one fisher's
            // occluder with every sprite on the material.
            float _HHDeckOccluderId;

            Varyings UnlitVertex(Attributes input)
            {
                UNITY_SKINNED_VERTEX_COMPUTE(input);
                SetUpSpriteInstanceProperties();
                input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);

                Varyings o = CommonUnlitVertex(input);
                o.color = input.color *_Color * unity_SpriteColor;
                return o;
            }

            half4 UnlitFragment(Varyings input) : SV_Target
            {
                // THE OCCLUSION, and the only line that is not URP's. Hull ids are small integers
                // over 255, so the compare is done in id space with a half-step tolerance — the same
                // rule the hull's own overlay quad uses, because 8-bit alpha rounding must not be
                // what decides whether a fisher is visible. Guarded on the id being set at all, so
                // an ordinary sprite never pays for a texture read it cannot use.
                if (_HHDeckOccluderId > 0.0)
                {
                    float hullAlpha = _HHHullScreenTex.Load(int3(int2(input.positionCS.xy), 0)).a;
                    if (abs(hullAlpha - _HHDeckOccluderId) * 255.0 < 0.5) discard;
                }

                return CommonUnlitFragment(input, input.color);
            }
            ENDHLSL
        }
    }

    Fallback "Sprites/Default"
}
