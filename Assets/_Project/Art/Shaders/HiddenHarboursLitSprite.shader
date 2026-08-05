// HiddenHarboursLitSprite.shader — the SHARED lit sprite. Any rig family that bakes a light channel gets
// lit by attaching this material and binding its sheets; nothing here is specific to a plant, a shrub or a
// rock.
//
// A custom URP 2D UNLIT sprite shader (text HLSL, builds headless — no Shader Graph). It is deliberately
// the SMALLEST possible consumer of Include/SpriteLitDecor.hlsl: sample the albedo, add the shared
// response, done. There is no wind here (the tree and the grass own their own motion), no tide, no
// season — a consumer that needs those keeps its own shader and includes the same header, exactly as
// HiddenHarboursTreeWind does.
//
// ⚠️ WHY THIS EXISTS RATHER THAN A TREE-SHADER VARIANT. The tree shader's vertex stage requires a
// TESSELLATED (Mesh Type Tight) sprite to bend correctly, and it carries a reflection pass. The shoreline
// plants and shrubs need neither, and inheriting a bend they cannot satisfy is how a "reuse" becomes a
// silent visual bug. Sharing the LIGHTING and not the geometry is the seam that actually holds.
//
// ⚠️ WHAT A CONSUMER MUST BIND. A light sheet is required; the normal sheet and the rim gate are
// optional and their absence is a supported path, not a degraded one — see SpriteLitDecor.hlsl's header,
// which is where the per-rig channel contracts are written down. Binding is per RENDERER through
// HiddenHarbours.Art.SpriteLightBinder, never per material: every species is its own sheet.
//
// ⚠️ IT SHIPS WITH THE RESPONSE ON (_LightResponse 1), and that is the opposite of Tree.mat's call for a
// deliberate reason. Tree.mat ships at 0 because a forest was placed and approved under the FLAT look and
// turning the response on would change art the owner had already signed off. The plants and shrubs have
// never been lit at all — an unlit default here would ship the exact dead channel this work exists to
// fix. Both are one slider, both are the owner's to move.
//
// SHADER CAUTIONS honoured: NO operator characters in any [Header(...)] label or Property string
// (ShaderLab parse error -> magenta); helpers declared BEFORE use; NO loops (so no [unroll] over a runtime
// bound); globals OUTSIDE the per-material CBUFFER, tunables INSIDE it; point-sampled, PPU 32. Visual
// only: drives no sim, saves nothing (rule 5), and every number is a material property (rule 6). The
// shipped variants are force-compiled headless by
// Assets/Tests/EditMode/Art/LitSpriteShaderCompileGuardTests.cs.
Shader "HiddenHarbours/LitSprite"
{
    Properties
    {
        [Header(Sprite)]
        [NoScaleOffset] _MainTex ("Sprite", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)
        // Sprites are cut out, not blended, so a plant reads crisp at PPU 32.
        _AlphaClip ("Alpha clip", Range(0, 1)) = 0.01

        [Header(Light response master (0 is off and the sprite draws flat))]
        _LightResponse ("Light response", Range(0, 2)) = 1

        [Header(Baked channels (bound per renderer by SpriteLightBinder))]
        // R key light, G back rim, B depth, A coverage. OURS, not the reference technique's order.
        [NoScaleOffset] _LightMask ("Light sheet", 2D) = "black" {}
        // OPTIONAL. Only the tree rig bakes one; the plants and shrubs resolve their normals at bake and
        // ship the light channel alone. Unbound reads as no normal, which every term already handles.
        [NoScaleOffset] _LightNormal ("Light normal sheet (optional)", 2D) = "black" {}
        // OPTIONAL. The state sheet whose one channel says "this pixel may not rim" (255 where forbidden).
        // Which sheet and which channel is the RIG's decision, published by the binder as the selector
        // below — plants use their tide sheet's blue, shrubs their calendar sheet's red.
        [NoScaleOffset] _LightRimGate ("Rim gate sheet (optional)", 2D) = "black" {}
        [HideInInspector] _LightRimGateChannel ("Rim gate channel selector", Vector) = (0, 0, 0, 0)
        // 1 only when a renderer has bound the light sheet. The fallback texture is opaque black, which
        // would read as "coverage everywhere" — a flag is honest where sniffing is not.
        _LightChannels ("Baked channels bound", Range(0, 1)) = 0
        // PUBLISHED, not tuned. xy is where this sprite stands in world space and w is 1 once a renderer
        // has said so. A SpriteRenderer's unity_ObjectToWorld is IDENTITY, so the shader cannot work it
        // out and every sprite would light from the world origin.
        [HideInInspector] _SpriteRootWS ("Sprite root world xy (published per renderer)", Vector) = (0, 0, 0, 0)

        [Header(Response shape)]
        // 0 = brighten where the rig's fixed key already put light. 1 = recompute the catch for the LIVE
        // light. Families with no normal sheet are pinned at the baked catch whatever this says, because
        // there is no surface to recompute against — see SpriteLightKey.
        _KeyRelight ("Key relight (0 baked .. 1 live)", Range(0, 1)) = 0.75
        _RimSteerAmount ("Rim steer toward the light", Range(0, 1)) = 0.8
        // How softly a light crosses from lighting the front to rimming the back. The shipped sun stays in
        // the south all day, so this width is what buys the grazing dawn and dusk rim.
        _LightFrontBand ("Front to back changeover width", Range(0.02, 1)) = 0.6
        _LightDepthBias ("Depth bias (near masses catch more)", Range(0, 1)) = 0.45

        [Header(Sun and moon key (added PRE grade so it dims with the world))]
        _SunKeyColor ("Sun catch colour", Color) = (0.91, 0.69, 0.42, 1)
        _SunKeyStrength ("Sun key strength", Range(0, 2)) = 0.16
        _SunRimStrength ("Sun rim strength", Range(0, 2)) = 0.28

        [Header(Boat lamp (pre compensated so it survives the night grade))]
        _LampKeyStrength ("Lamp key strength", Range(0, 3)) = 0.18
        _LampRimStrength ("Lamp rim strength", Range(0, 3)) = 0.9
        // The lamp sits above the water and this decor stands on the bank: a small positive elevation
        // keeps the beam from reading as a light buried in the ground. Metres are not needed, only the sine.
        _LampElevation ("Lamp elevation (sine)", Range(0, 1)) = 0.12
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
        Blend SrcAlpha OneMinusSrcAlpha
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
            // The SHARED lit-decor consumer — the sheet declarations, the sun / day-night / boat-lamp
            // globals, the per-material rows and the assembled response. The same file the tree calls.
            #include "Assets/_Project/Art/Shaders/Include/SpriteLitDecor.hlsl"

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
                // Where this sprite STANDS, for the lamp's distance and direction. Published per renderer
                // (see _SpriteRootWS); the fallback is the fragment's own world xy, which is off by the
                // sprite's drawn height but never off by the whole scene.
                float2 rootWS     : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            // The light sheets and every lighting global come from the shared include.

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float  _AlphaClip;
                // Every lighting row, from the SHARED include. One pass here, so no cross-pass layout
                // constraint — but taking them from the macro is what keeps this shader and the tree on
                // ONE property set as the family grows.
                SPRITE_LIT_DECOR_MATERIAL_ROWS
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);

                // ⚠️ A SpriteRenderer submits vertices ALREADY IN WORLD SPACE with an IDENTITY
                // object-to-world matrix. TransformObjectToWorld is therefore the identity here — correct,
                // and the same call the tree shader makes, but do not read a stand position out of it.
                float3 wp = TransformObjectToWorld(IN.positionOS);

                OUT.positionCS = TransformWorldToHClip(wp);
                OUT.color = IN.color * _Color;
                OUT.uv = IN.uv;
                OUT.rootWS = _SpriteRootWS.w > 0.5 ? _SpriteRootWS.xy : wp.xy;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);
                half4 col = tex * IN.color;
                clip(col.a - _AlphaClip);

                // Skipped entirely unless a renderer bound the light sheet AND the master is up, so an
                // unbound sprite costs two scalar compares and nothing else.
                if (_LightResponse > 1e-4 && _LightChannels > 0.5)
                {
                    SPRITE_LIT_DECOR_PARAMS(litParams)
                    col.rgb += _LightResponse * SpriteLitDecorResponse(IN.uv, IN.rootWS, litParams);
                }

                return col;
            }
            ENDHLSL
        }
    }

    Fallback Off
}
