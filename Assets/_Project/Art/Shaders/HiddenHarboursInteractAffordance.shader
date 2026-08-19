// HiddenHarboursInteractAffordance.shader — THE INTERACT AFFORDANCE, as two tiny draws over a sprite the
// shader does not own and never modifies.
//
// WHAT THIS IS FOR. At interior closeups an interactable fixture vanishes into the room: a stairwell, a
// bed and the freezer are invisible-until-pressed. This draws the "you could work this" cue on whatever
// the offer channel says the player is facing. It is a CUE, not a style: exactly one thing in the world
// wears it at a time, and only while the popup names that same thing.
//
// WHY THIS IS AN OVERLAY AND NOT A PROPERTY ON THE FIXTURE'S OWN MATERIAL. Interior props are placed by
// InteriorCatalog.ConfigureProp, which assigns NO material — they draw on Unity's default sprite
// material, as does Ginny's freezer. There is therefore no property to set, and reaching for one would
// mean swapping the material of every fixture in the game and changing how it draws when nothing is
// highlighted at all. Overlaying leaves the fixture's own draw untouched (which is also the only way to
// honour "never dirty the shared asset"), and it composites over whatever material and whatever 2D
// lighting that fixture happens to have.
//
// THE LIFT IS ADDITIVE, AND THAT IS THE WHOLE REASON IT IS SAFE. An overlay that REPLACED the fixture's
// pixels would also replace its lighting, so a highlighted bed would visibly flatten in a lit room.
// Adding scaled-by-its-own-colour keeps every existing term and raises it: the output is
// base * (1 + lift), which in linear space is a step UP THE SAME RAMP with hue and saturation
// mathematically unchanged. That is the palette-safe reading of "lift one palette step", and it is NOT a
// tint multiply — nothing here has a colour of its own to drag the art toward.
//
// THE OUTLINE'S COLOUR IS THE SPRITE'S OWN DARKEST STEP, PER PIXEL — col.rgb * _OutlineDarken, so it is
// by construction in the family of the very pixel it edges. A fixed outline colour would be a second
// palette entering the room by the back door.
//
// NO UV DILATION HERE, DELIBERATELY. The obvious outline is an 8-tap alpha dilation, and it is WRONG on
// this art: every prop sheet is imported spriteMode Multiple, so a tap outside a sprite's own UV rect
// samples the NEIGHBOURING FRAME and the "outline" becomes a smear of the next facing. The edge is made
// instead by drawing this same sprite slightly larger BEHIND the fixture, which cannot leave the sprite's
// own UVs and so cannot bleed. The presenter does that geometry; this shader only colours it.
//
// SHADER CAUTIONS honoured: NO operator characters in any Header or Property string (ShaderLab parse
// error -> magenta); helpers declared BEFORE use; NO loops; globals OUTSIDE the per-material CBUFFER,
// tunables INSIDE it. Visual only: drives no sim, saves nothing (rule 5), and every number is a material
// property (rule 6).
Shader "HiddenHarbours/InteractAffordance"
{
    Properties
    {
        [Header(Sprite)]
        [NoScaleOffset] _MainTex ("Sprite", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)
        _AlphaClip ("Alpha clip", Range(0, 1)) = 0.01

        [Header(Mode (0 is the additive lift and 1 is the outline))]
        _Mode ("Mode", Range(0, 1)) = 0

        [Header(Strength (the variant scales this and the pulse rides it))]
        _Strength ("Strength", Range(0, 2)) = 1

        [Header(Lift (fraction of the sprites own colour added back on top))]
        _LiftAmount ("Lift amount", Range(0, 1)) = 0.35

        [Header(Outline (how far down its own ramp the edge sits))]
        _OutlineDarken ("Outline darken", Range(0, 1)) = 0.18

        [Header(Blend (set per material by the presenter))]
        [HideInInspector] _SrcBlend ("Src blend", Float) = 1
        [HideInInspector] _DstBlend ("Dst blend", Float) = 1
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
        Blend [_SrcBlend] [_DstBlend]
        Cull Off
        ZWrite Off

        Pass
        {
            Tags { "LightMode" = "Universal2D" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float3 positionOS : POSITION;
                float4 color      : COLOR;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 color      : COLOR;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float  _AlphaClip;
                float  _Mode;
                float  _Strength;
                float  _LiftAmount;
                float  _OutlineDarken;
                float  _SrcBlend;
                float  _DstBlend;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);

                // A SpriteRenderer submits vertices ALREADY IN WORLD SPACE with an IDENTITY
                // object-to-world matrix (memory: spriterenderer-objecttoworld-is-identity). The call
                // below is therefore the identity — kept because it is the pipeline's own spelling of
                // "to world", not because it transforms anything.
                float3 wp = TransformObjectToWorld(IN.positionOS);

                OUT.positionCS = TransformWorldToHClip(wp);
                OUT.color = IN.color * _Color;
                OUT.uv = IN.uv;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);

                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);
                half4 col = tex * IN.color;
                clip(col.a - _AlphaClip);

                half strength = (half)max(_Strength, 0.0);

                // ---- the outline: this sprite in its own darkest step ------------------------------
                // Drawn BEHIND the fixture and slightly larger, so only the fringe is ever seen. Alpha
                // carries the pulse, which is why the edge breathes rather than the colour shifting.
                if (_Mode > 0.5)
                {
                    half3 edge = col.rgb * (half)_OutlineDarken;
                    return half4(edge, col.a * strength);
                }

                // ---- the lift: a step up this pixel's OWN ramp -------------------------------------
                // Additive, so the fixture underneath keeps every term it already had. Premultiplied by
                // alpha because the additive blend ignores the alpha channel: without this the lift
                // would spill at full weight across a cut-out sprite's transparent corners.
                half3 lift = col.rgb * (half)_LiftAmount * strength * col.a;
                return half4(lift, 0.0h);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
