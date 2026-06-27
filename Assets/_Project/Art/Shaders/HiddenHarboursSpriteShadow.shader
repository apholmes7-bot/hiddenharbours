// HiddenHarboursSpriteShadow.shader — the stylized PROJECTED SPRITE SHADOW (PR 2, ADR 0013 §"Projected shadows").
//
// Draws a flat, dark, semi-transparent SILHOUETTE of a caster's sprite, SHEARED + LENGTH-SCALED in the VERTEX
// stage so it rakes away from the sun along the ground plane: long WEST at dawn, a short NORTHWARD stub at noon,
// long EAST at dusk — the player "reads the time from their shadow" (P1 "The Sea Has Moods"). This is the
// ADR-preferred stylized approach over URP's ShadowCaster2D: one extra sprite draw, pixel-snappable, no lights,
// no per-sprite Sprite-Lit migration.
//
// The shear is driven by the globals the DayNightController already publishes — no new wiring:
//   _SunDir        (xy, ground-plane direction TOWARD the sun; the shadow runs the OTHER way)
//   _SunElevation  (1 noon .. 0 horizon .. <=0 night; shorter shadow as the sun climbs)
// and by per-renderer tunables the SpriteShadow component pushes via a MaterialPropertyBlock (so every caster
// shares this ONE material — GPU-instance / batch friendly, CLAUDE.md rule 7):
//   _ShadowColor   (the flat dark colour, .a = the already-computed ShadowStrength*maxAlpha)
//   _ShadowDir     (xy, the ground-plane direction the shadow runs — away from the sun; from the component so
//                   the south-bias / noon-lift tuning is honoured, falls back to -_SunDir if unset)
//   _ShadowLen     (the shear distance, in the sprite's OWN local-Y units: how far the TOP of the silhouette
//                   is pushed along _ShadowDir; 0 = upright/none. The component bakes the height*length here.)
//   _EdgeSoftness  (0 = crisp pixel silhouette, up to 1 = feather the alpha toward the silhouette edge)
//
// The mesh is the sprite quad with the pivot at the FEET (bottom-centre), so local Y in [0..1] runs feet->head;
// the vertex shear scales by that Y, anchoring the silhouette at the ground and laying it out flat.
//
// Visual-only: drives no sim, saves nothing (rule 5). Force-compiled by WaterShaderCompileGuardTests via the
// shipped Resources/SpriteShadow.mat (the magenta guard scans every project material) — a break fails CI RED.
Shader "HiddenHarbours/SpriteShadow"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite (caster)", 2D) = "white" {}
        _ShadowColor  ("Shadow color (a = strength*maxAlpha)", Color) = (0, 0, 0, 0.45)
        _ShadowDir    ("Shadow direction (xy, away from sun)", Vector) = (-1, 0, 0, 0)
        _ShadowLen    ("Shear length (local-Y units)", Float) = 0
        _EdgeSoftness ("Edge softness (0..1)", Range(0,1)) = 0
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }

        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off
        ZTest LEqual

        Pass
        {
            Tags { "LightMode" = "Universal2D" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float3 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            // Globals published by DayNightController (read-only here).
            float4 _SunDir;
            float  _SunElevation;

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _ShadowColor;
                float4 _ShadowDir;
                float  _ShadowLen;
                float  _EdgeSoftness;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;

                // Pivot is at the FEET (bottom-centre), so positionOS.y runs 0 -> spriteHeight feet -> head.
                // Normalise by the local sprite extent so the shear is "fraction of the way up" * shear length.
                // The sprite quad's local height is its bounds; we approximate via the UV.y (0 at feet, 1 at top)
                // which is robust to the sprite's PPU and pivot. uv.y == 0 at the anchored feet.
                float upFrac = saturate(IN.uv.y);

                // The ground-plane direction the shadow runs. Prefer the component's _ShadowDir (honours the
                // south-bias / noon-lift); fall back to the opposite of the published sun direction.
                float2 dir = _ShadowDir.xy;
                if (dot(dir, dir) < 1e-6)
                    dir = -_SunDir.xy;
                float dlen = length(dir);
                dir = (dlen > 1e-5) ? dir / dlen : float2(-1.0, 0.0);

                // Shear: push each vertex along 'dir' proportionally to how far up the sprite it is, by the
                // component-baked shear length (height * ShadowLength). Feet (upFrac 0) stay anchored; the head
                // (upFrac 1) lands at feet + dir * _ShadowLen. _SunElevation<=0 means the component sent len=0.
                float2 shear = dir * (_ShadowLen * upFrac);

                float3 posOS = IN.positionOS;
                posOS.xy += shear;

                OUT.positionCS = GetVertexPositionInputs(posOS).positionCS;
                OUT.uv = TRANSFORM_TEX(IN.uv, _MainTex);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // Sample the caster's sprite — we only use its ALPHA (the silhouette mask); the colour is the
                // flat _ShadowColor. So a textured caster casts a shadow of its own shape.
                half srcA = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv).a;

                // Optional edge feather: soften the silhouette mask so the shadow doesn't read as a hard cutout
                // (still pixel-faithful when _EdgeSoftness = 0 -> crisp).
                half mask = srcA;
                if (_EdgeSoftness > 0.0)
                    mask = smoothstep(0.0, max(_EdgeSoftness, 1e-3), srcA);

                half a = _ShadowColor.a * mask;
                if (a <= 0.0) discard;   // no contribution -> skip (also the night/overcast = 0-alpha case)
                return half4(_ShadowColor.rgb, a);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
