// HiddenHarboursSilhouetteMask.shader — the fisher's own shape, written into a screen mask.
//
// THE RULING (owner, 2026-08-16): dense forest must never lose the player. Foliage stays OPAQUE and
// draws in front where it genuinely is in front; the player reads as a TINTED SILHOUETTE through it.
//
// This shader is one half of that — the CHEAP half. It draws nothing the player can see: it writes
// the player's sprite coverage into a single-channel screen texture (_HHSilhouetteMaskTex) before any
// sprite of any sorting layer draws. The foliage shaders then read that texture at their own screen
// pixel and lerp toward the silhouette tint where it is set (SpriteLitDecor.hlsl's SilhouetteThrough).
//
// ⚠️ WHY THE MASK IS THE PLAYER AND NOT THE FOLIAGE, which is the whole performance argument.
// "Is foliage in front of the fisher at this pixel?" can be answered from either side. Masking the
// FOLIAGE would mean rasterising every tree and shrub a second time — at forest density that is the
// single most expensive thing on screen, doubled. Masking the PLAYER is ONE extra draw call, forever,
// however dense the woods get. The sorting then answers the "in front" half for free: foliage BEHIND
// the fisher draws before her and is painted over by her own sprite, so the tint it received is never
// seen; foliage in FRONT draws after her, keeps its tint, and that is exactly the pixels the effect
// wants. No depth buffer, no per-tree state, no sorting-order hacks.
//
// ⚠️ EVERYTHING HERE IS URP'S OWN Sprite-Unlit-Default, structurally verbatim — the same Core2D /
// 2DCommon includes, the same UnityFlipSprite and SetUpSpriteInstanceProperties. That is deliberate
// and load-bearing for the SAME reason HiddenHarboursDeckOccludedSprite.shader says it is: the mask
// must land on exactly the pixels the fisher's own sprite lands on, and any difference in how flip,
// instancing or the sprite's vertex path is handled would slide the silhouette off the body. The only
// edits are the output (flat coverage) and the absence of tint/colour, neither of which touches
// geometry. Re-derive from URP's shader, never from memory, if this ever needs to move.
//
// The target is R8_UNorm cleared to 0, and the blend is ordinary alpha-over white, so a texel ends up
// carrying the sprite's own alpha: 1 in the body, 0 outside it, and the anti-aliased rim of a rotated
// or scaled cell lands in between rather than stair-stepping. Nothing reads more than .r.
//
// SHADER CAUTIONS honoured (this project lost hours to magenta shaders): no operator characters in
// Property display strings; no loops; force-compiled headless by SilhouetteShaderCompileGuardTests so
// a break fails red rather than shipping magenta.
Shader "HiddenHarbours/SilhouetteMask"
{
    Properties
    {
        _MainTex ("Sprite Texture", 2D) = "white" {}

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

        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        Pass
        {
            // The mask pass. Recorded ONLY by SilhouetteMaskFeature, and only for renderers that also
            // carry SilhouetteRegistry.RenderingLayer — see the feature for why membership is the layer
            // bit and not this tag alone.
            Tags { "LightMode" = "HHSilhouette" }

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

            #pragma multi_compile_instancing
            #pragma multi_compile _ DEBUG_DISPLAY SKINNED_SPRITE

            // NOTE: Do not ifdef the properties here as SRP batcher can not handle different layouts.
            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
            CBUFFER_END

            Varyings UnlitVertex(Attributes input)
            {
                UNITY_SKINNED_VERTEX_COMPUTE(input);
                SetUpSpriteInstanceProperties();
                input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);

                Varyings o = CommonUnlitVertex(input);
                o.color = input.color;
                return o;
            }

            half4 UnlitFragment(Varyings input) : SV_Target
            {
                // Coverage only. The sprite's own alpha decides the shape; the renderer's tint, the
                // day/night grade and every other colour operation are deliberately NOT read — this
                // is a stencil of the body, not a picture of it, and a fisher who fades at dusk must
                // not take her silhouette down with her.
                half a = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv).a;
                return half4(a, a, a, a);
            }
            ENDHLSL
        }
    }

    Fallback "Sprites/Default"
}
