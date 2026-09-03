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
//   _ShadowLen     (the shear distance, in the sprite's OWN local-Y units: how far ONE CASTER HEIGHT above the
//                   pivot is pushed along _ShadowDir; 0 = upright/none. The component bakes the height*length.)
//   _ShadowUV      (xy, the AFFINE MAP uv.y -> "height above the caster's PIVOT, in caster heights"; see below)
//   _EdgeSoftness  (0 = crisp pixel silhouette, up to 1 = feather the alpha toward the silhouette edge)
//
// ⚠️ THE SHEAR ANCHORS AT THE CASTER'S PIVOT, AND uv.y IS NOT THAT PIVOT. A projected shadow is anchored where
// the caster MEETS THE GROUND — its pivot, which is the feet by contract across every rig family (ADR 0026:
// trees pin the pivot at the TRUNK FOOT, characters at (0.5, GroundInsetPx/cellH), shrubs and shore plants at
// the root crown). This shader used to shear by `saturate(uv.y)`, which anchors at uv.y == 0 instead, and that
// is wrong TWICE over:
//   1. uv.y == 0 is the bottom of the sprite's CELL, not its pivot. Every rig family carries rows underneath
//      the pivot (a tree's near-root flare, a character's 10 px ground inset), so the silhouette's feet were
//      pushed off the caster's feet by len * pivotFraction.
//   2. uv IS TEXTURE SPACE, NOT CELL SPACE. Every caster in production is a sliced sheet (spriteMode 2), so a
//      cell sitting on row 3 of a 4-row shrub sheet has uv.y in [0.75, 1.0] — the shear barely varied across
//      the sprite and displaced the whole silhouette by ~0.79 of the full rake. Measured on the committed
//      geometry: up to 15.0 m on a BeakedHazelnut, 12.8 m on the fisher's iso skin.
// So the component now publishes the exact affine map from THIS sprite's uv.y to height-above-pivot:
//      upFrac = uv.y * _ShadowUV.x + _ShadowUV.y      (0 AT THE PIVOT, 1 one caster-height above it)
// derived from the sprite's own rect / pivot / sheet height / PPU. It is exact for FullRect AND Tight meshes
// (the trees are Tight) because it only inverts the sprite's texture mapping, which both share. A caster whose
// pivot IS its cell-bottom-centre AND whose sprite fills its texture gets _ShadowUV = (1, 0) — i.e. upFrac ==
// uv.y, byte-for-byte the old path. That is the negative control, and it is what the tests' fixtures are.
//
// ================================================================================================
// 🔴 ONE SHADE PER PIXEL (tree shading PR 2) — the STENCIL, and why it is render STATE and not a dial
// ================================================================================================
// Two shadows crossing used to darken the ground TWICE: alpha-over alpha-over, so a crossing read at
// 1-(1-a)^2 against a single shadow's a. Measured on the St Peters wood at 07:00, wind frozen: 7.5% of
// the frame was carrying more than twice a single shadow's darkening, which is what makes a stand's
// floor read as a patchwork of blots rather than as shade.
//
// The fix is a stencil: the FIRST shadow to reach a pixel writes 1 and draws; every later one fails the
// NotEqual test and is discarded. The ground gets one shade whoever casts it, per-caster sorting is
// untouched, and it costs a stencil test — no new pass, no buffer, no pooling.
//
// ⚠️ IT MUST BE STATE, NOT A PROPERTY BLOCK VALUE. A MaterialPropertyBlock can only feed shader
// UNIFORMS; it cannot change render state, so "stacking on/off" could never have been a per-renderer or
// per-profile dial without writing the shared material asset at runtime — which in the editor dirties
// the .mat on disk (the trap that once re-tuned Water.mat). So the stencil ships ON in the material and
// the three [HideInInspector] properties below are the escape hatch: a SECOND material with
// _StencilComp = 8 (Always) reproduces the old stacking exactly, which is what the PR's before/after
// plate is rendered with.
//
// ⚠️ THE PROJECT USES THE STENCIL NOWHERE ELSE — verified across every shader, and there is no
// SpriteMask in any scene (URP's own mask path is the other stencil consumer in a 2D project). The
// camera clears depth+stencil each frame, so the claim never outlives the frame that made it.
//
// ================================================================================================
// 🔴 GROUND CONTACT — the same material, drawing the shade a crown throws STRAIGHT DOWN
// ================================================================================================
// A sheared silhouette cannot draw it: at noon the shear is short and runs NORTH, so the trunk foot —
// the one place you are certainly under the tree — is left in full sun. _GroundContact switches this
// shader to a soft radial pool instead of a sprite silhouette, with no shear. The ELLIPSE is made by
// the component scaling the quad (x = 2r, y = 2r x 0.6428, the ground-plane squash), so nothing here
// needs to know about the camera. It writes the same stencil as the cast shadow, so a crown's pool and
// its own rake meet without doubling.
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
        // PUBLISHED per renderer by SpriteShadow, never tuned: the affine map uv.y -> height above the caster's
        // PIVOT in caster heights. Hidden because there is nothing here for the owner to set. The default is the
        // IDENTITY map (1, 0), which reproduces the old raw-uv.y shear exactly — so a material drawn with no
        // property block (an inspector preview) looks as it always did rather than collapsing to no shear.
        [HideInInspector] _ShadowUV ("Pivot map (xy, published per renderer)", Vector) = (1, 0, 0, 0)
        _EdgeSoftness ("Edge softness (0..1)", Range(0,1)) = 0
        // GROUND CONTACT: 0 = draw the caster's sheared silhouette (the cast shadow); > 0 = draw a soft
        // radial pool with NO shear, this value being its edge softness as a fraction of the radius. Per
        // RENDERER through the property block, because one caster draws both.
        [HideInInspector] _GroundContact ("Ground contact mode (0 = silhouette, >0 = pool softness)", Float) = 0
        // THE STENCIL, as material state (see the header for why it cannot be a per-renderer dial).
        // Shipped 1 / NotEqual(6) / Replace(2): first shadow at a pixel wins, later ones are discarded.
        // A material with Comp = Always(8) reproduces the pre-PR stacking exactly.
        [HideInInspector] _StencilRef  ("Stencil ref", Int) = 1
        [HideInInspector] _StencilComp ("Stencil comparison", Int) = 6
        [HideInInspector] _StencilPass ("Stencil pass op", Int) = 2
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }

        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off
        ZTest LEqual

        // ONE SHADE PER PIXEL. See the header: the first shadow to reach a pixel claims it.
        Stencil
        {
            Ref [_StencilRef]
            Comp [_StencilComp]
            Pass [_StencilPass]
        }

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
                float4 _ShadowUV;      // xy = the affine map uv.y -> height above the caster's pivot
                float  _ShadowLen;
                float  _EdgeSoftness;
                float  _GroundContact;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;

                // How far above the caster's PIVOT — the feet, where it meets the ground — this vertex sits, in
                // caster heights. THE ANCHOR IS THE PIVOT, NOT uv.y == 0 (see the header): the component
                // publishes the exact affine map for this sprite's rect / pivot / sheet height / PPU, so
                // upFrac == 0 lands precisely on the feet whatever row of whatever sheet the cell was sliced
                // from. NOT saturated: rows BELOW the pivot (a tree's near-root flare) are below the ground
                // plane and must rake the other way, which is what a negative upFrac does.
                float upFrac = IN.uv.y * _ShadowUV.x + _ShadowUV.y;

                // The ground-plane direction the shadow runs. Prefer the component's _ShadowDir (honours the
                // south-bias / noon-lift); fall back to the opposite of the published sun direction.
                float2 dir = _ShadowDir.xy;
                if (dot(dir, dir) < 1e-6)
                    dir = -_SunDir.xy;
                float dlen = length(dir);
                dir = (dlen > 1e-5) ? dir / dlen : float2(-1.0, 0.0);

                // Shear: push each vertex along 'dir' proportionally to how far above the PIVOT it is, by the
                // component-baked shear length (height * ShadowLength). The feet (upFrac 0) stay anchored ON the
                // pivot; one caster-height up (upFrac 1) lands at feet + dir * _ShadowLen. _SunElevation<=0
                // means the component sent len=0.
                //
                // A GROUND-CONTACT pool takes none of this: it is a flat ellipse already lying on the ground at
                // the caster's feet, so there is nothing to project. The branch is uniform across the draw.
                float2 shear = (_GroundContact > 0.0) ? float2(0.0, 0.0) : dir * (_ShadowLen * upFrac);

                float3 posOS = IN.positionOS;
                posOS.xy += shear;

                OUT.positionCS = GetVertexPositionInputs(posOS).positionCS;
                OUT.uv = TRANSFORM_TEX(IN.uv, _MainTex);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half mask;
                if (_GroundContact > 0.0)
                {
                    // GROUND CONTACT: a soft radial pool over the quad, which the component has already
                    // scaled into the ground-plane ellipse. d is 0 at the centre and 1 at the rim; the
                    // falloff starts one softness-fraction in, so softness 0 is a hard ellipse and 1 fades
                    // the whole way. Outside the rim smoothstep saturates and the discard below takes it.
                    half2 d2 = (IN.uv - 0.5) * 2.0;
                    half d = length(d2);
                    mask = 1.0 - smoothstep(1.0 - max(_GroundContact, 1e-3), 1.0, d);
                }
                else
                {
                    // Sample the caster's sprite — we only use its ALPHA (the silhouette mask); the colour is
                    // the flat _ShadowColor. So a textured caster casts a shadow of its own shape.
                    half srcA = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv).a;

                    // Optional edge feather: soften the silhouette mask so the shadow doesn't read as a hard
                    // cutout (still pixel-faithful when _EdgeSoftness = 0 -> crisp).
                    mask = srcA;
                    if (_EdgeSoftness > 0.0)
                        mask = smoothstep(0.0, max(_EdgeSoftness, 1e-3), srcA);
                }

                half a = _ShadowColor.a * mask;
                if (a <= 0.0) discard;   // no contribution -> skip (also the night/overcast = 0-alpha case)
                return half4(_ShadowColor.rgb, a);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
