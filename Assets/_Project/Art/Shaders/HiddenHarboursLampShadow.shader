// HiddenHarboursLampShadow.shader — a LAMP-CAST SHADOW (ADR 0016, lights PR B): the owner's
// "the spotlights and headlights need to put shadows ... the light needs to affect the environment".
//
// WHY A MULTIPLY DRAWN ABOVE THE GLOW, NOT A DARK SPRITE UNDER THE CASTER. The sun shadow
// (HiddenHarboursSpriteShadow) is a dark alpha-blended silhouette sorted one order UNDER its caster,
// and that is right for the sun because the world itself is what the sun lights. A lamp's light is
// not in the world: it is ADDED after ADR 0013's whole-frame multiply — the additive quad above the
// overlay on land, the pre-compensated in-shader beam on water. A dark sprite in the world sort is
// crushed by the night multiply along with everything else and the glow is then added on top of it,
// unchanged: at night such a shadow is invisible by construction. So this draws ABOVE every glow
// (sorting order short.MaxValue, depth pinned nearer the camera than the light quads — see
// LampShadowSystem) and MULTIPLIES the frame down: Blend Zero SrcColor, dst *= rgb, where rgb is
// lerp(1, shadow tint, alpha). Whatever light is at the pixel — quad glow, water beam, lit decor —
// loses the same fraction, and an unlit pixel loses nothing it had. At alpha 0 rgb is exactly 1 and
// the fragment discards before it is ever written: strength 0 is today's frame, byte for byte.
//
// THE SILHOUETTE COMES FROM THE CASTER, PER PIXEL, THROUGH THE INVERSE SHEAR. The quad rasterised is
// the axis-aligned box of the caster's SHEARED image. Each fragment runs the shear BACKWARDS
// (HHUnshear — the exact twin of LampShadowMath.Unshear, pinned by a source guard) to find which
// point on the caster it is the shadow of, and asks the caster's silhouette whether that point is
// opaque:
//   - a SPRITE caster: sample the caster's own sheet, mapping world -> cell -> texture uv
//     (_SpriteRectWorld / _SpriteRectUV, published per renderer);
//   - a MESH HULL (HH_LAMP_SHADOW_HULL): load the feature's resolved screen texture
//     _HHHullScreenTex at that point's screen pixel and test the hull's id block — the same
//     either-id filter the hull's own overlay and reflection passes use, so two overlapping boats
//     never shadow each other's image.
// And a shadow never darkens ITS OWN CASTER: a fragment lying on the caster's own opaque pixels
// discards first (the sun shadow gets the same effect by sorting under its caster).
//
// The approximation, stated: this is 2D iso. A shadow is the caster's skewed silhouette — one
// direction per caster, parallel edges, screen height standing in for world height — not a
// raycast. It is the SpriteShadow model with the sun replaced by a point at a height.
//
// multi_compile, not shader_feature: the two materials are shipped assets, but the rule here is
// that any keyword a runtime-built material could ever want is compiled for both variants.
// Visual-only: drives no sim, saves nothing (rule 5). Force-compiled by the magenta guard via the
// shipped Resources/LampShadow.mat + LampShadowHull.mat — a break fails CI RED.
Shader "HiddenHarbours/LampShadow"
{
    Properties
    {
        [PerRendererData] _MainTex ("Silhouette sheet (the caster sprite; unused for a hull)", 2D) = "white" {}
        _ShadowColor  ("Shadow colour (rgb = fully shadowed tint, a = alpha)", Color) = (0.04, 0.05, 0.10, 0)
        _ShadowDir    ("Shadow direction (xy, away from the lamp)", Vector) = (0, -1, 0, 0)
        [HideInInspector] _ShadowFoot ("Feet xy, shear per metre z (published per renderer)", Vector) = (0, 0, 0, 0)
        [HideInInspector] _SpriteRectWorld ("Sprite world rect: min xy, inverse size zw", Vector) = (0, 0, 1, 1)
        [HideInInspector] _SpriteRectUV ("Sprite uv rect: origin xy, extent zw", Vector) = (0, 0, 1, 1)
        [HideInInspector] _HullIds ("Hull id, fore block base, fore span (ids over 255)", Vector) = (0, 0, 0, 0)
        _EdgeSoftness ("Edge softness (0..1, sprites only)", Range(0,1)) = 0
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }

        // MULTIPLY: dst *= src.rgb. The fragment outputs lerp(1, tint, alpha), so an alpha of 0 is
        // the identity and full alpha pulls the pixel to the tint.
        Blend Zero SrcColor
        Cull Off
        ZWrite Off
        ZTest Always   // a compositing element above the world, like the light quads it darkens

        Pass
        {
            Tags { "LightMode" = "Universal2D" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5
            #pragma multi_compile_local _ HH_LAMP_SHADOW_HULL

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            // GLOBAL, bound by IsoFacetHullFeature after the keyline resolve (a 1x1 clear fallback
            // before the feature has ever run): every mesh hull's pixels, alpha = her id over 255.
            Texture2D<float4> _HHHullScreenTex;

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _ShadowColor;
                float4 _ShadowDir;
                float4 _ShadowFoot;
                float4 _SpriteRectWorld;
                float4 _SpriteRectUV;
                float4 _HullIds;
                float  _EdgeSoftness;
            CBUFFER_END

            struct Attributes
            {
                float3 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionWS = TransformObjectToWorld(IN.positionOS);
                OUT.positionCS = TransformWorldToHClip(OUT.positionWS);
                return OUT;
            }

            // The inverse shear — LampShadowMath.Unshear, verbatim. A shadow pixel p is the shadow of
            // the caster point c with c + dir * L * (c.y - foot.y) == p.
            float2 HHUnshear(float2 p, float2 foot, float2 dir, float L)
            {
                float denom = 1.0 + dir.y * L;
                float h = (p.y - foot.y) / denom;
                return float2(p.x - dir.x * L * h, foot.y + h);
            }

            // SPRITE silhouette: the caster's alpha at a world point, 0 outside its cell.
            float HHSpriteCoverage(float2 worldPoint)
            {
                float2 t = (worldPoint - _SpriteRectWorld.xy) * _SpriteRectWorld.zw;
                if (any(t < 0.0) || any(t > 1.0)) return 0.0;
                float2 uv = _SpriteRectUV.xy + t * _SpriteRectUV.zw;
                return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv).a;
            }

            #ifdef HH_LAMP_SHADOW_HULL
            // The hull's membership test, restated here from the overlay pass (the two do not share
            // a block): her own id, or any id in her contiguous FORE block (the deck-occupant split).
            bool HHIsThisHull(float a)
            {
                if (abs(a - _HullIds.x) * 255.0 < 0.5) return true;
                if (_HullIds.y <= 0.0) return false;
                float d = (a - _HullIds.y) * 255.0;
                return d > -0.5 && d < _HullIds.z - 0.5;
            }

            bool HHHullCoverageAtPixel(int2 px)
            {
                if (any(px < 0) || any(px >= int2(_ScreenParams.xy))) return false;
                float4 c = _HHHullScreenTex.Load(int3(px, 0));
                return c.a > 0.5 / 255.0 && HHIsThisHull(c.a);
            }

            // A world point's screen pixel in ComputeScreenPos's convention. Only DIFFERENCES between
            // two of these are used (see frag), so the render target's Y-flip convention cancels
            // against the fragment's own SV_Position, which is the ground truth for the texel grid.
            float2 HHScreenPixelOf(float2 worldPoint, float depth)
            {
                float4 cs = TransformWorldToHClip(float3(worldPoint, depth));
                float4 sp = ComputeScreenPos(cs);
                return (sp.xy / max(sp.w, 1e-6)) * _ScreenParams.xy;
            }
            #endif

            half4 frag(Varyings IN) : SV_Target
            {
                float a = _ShadowColor.a;
                if (a <= 0.0) discard;   // strength 0 / gated off: write nothing at all

                float2 p = IN.positionWS.xy;
                float2 foot = _ShadowFoot.xy;
                float2 dir = _ShadowDir.xy;
                float L = _ShadowFoot.z;
                float2 c = HHUnshear(p, foot, dir, L);

                float mask;
                #ifdef HH_LAMP_SHADOW_HULL
                    int2 selfPx = int2(IN.positionCS.xy);
                    // Never darken the hull herself.
                    if (HHHullCoverageAtPixel(selfPx)) discard;
                    // The caster pixel: this fragment's own pixel plus the world offset to the caster
                    // point, taken in screen space. If ComputeScreenPos runs the other way up from
                    // SV_Position on this target, its own reading of THIS fragment says so, and the
                    // vertical offset is flipped to match.
                    float2 own = HHScreenPixelOf(p, IN.positionWS.z);
                    float2 cast = HHScreenPixelOf(c, IN.positionWS.z);
                    float2 delta = cast - own;
                    if (abs(own.y - IN.positionCS.y) > 1.0) delta.y = -delta.y;
                    int2 px = int2(floor(float2(selfPx) + delta + 0.5));
                    mask = HHHullCoverageAtPixel(px) ? 1.0 : 0.0;
                #else
                    // Never darken the caster's own pixels.
                    if (HHSpriteCoverage(p) > 0.5) discard;
                    mask = HHSpriteCoverage(c);
                    if (_EdgeSoftness > 0.0)
                        mask = smoothstep(0.0, max(_EdgeSoftness, 1e-3), mask);
                #endif

                float k = a * mask;
                if (k <= 0.0) discard;   // outside the silhouette: nothing to darken

                // dst *= rgb: full alpha pulls the pixel to the tint, zero leaves it alone.
                float3 rgb = lerp(float3(1.0, 1.0, 1.0), _ShadowColor.rgb, k);
                return half4(rgb, 1.0);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
