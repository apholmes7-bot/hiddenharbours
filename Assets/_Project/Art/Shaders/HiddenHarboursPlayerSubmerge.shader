// HiddenHarboursPlayerSubmerge.shader — the ON-FOOT SUBMERSION + UNDERWATER REFRACTION effect (pairs with the
// #163 wade model + #164 wade splashes). As the fisher wades off the drying sandbar into deeper water, a
// WATERLINE rises up their body: feet hidden first, then shins, knees, chest. The part BELOW the line reads
// UNDERWATER — tinted toward a water colour, DIMMED (submerged-but-visible), and REFRACTED (a small animated
// horizontal wobble, pixel-snapped so it stays pixel-art) — with a bright FOAM/RIPPLE line right at the
// waterline. The part ABOVE the line is the sprite UNCHANGED. Your own body becomes the depth gauge (P1 "The
// Sea Has Moods", P5 "Cozy but with Teeth" — wading out on a falling tide feels physical and a little risky).
//
// This is the PLAYER'S OWN shader (the water plane can't warp a different sprite that sorts above it), a plain
// UNLIT Universal2D pass — a straight clone of HiddenHarboursSpriteShadow.shader's structure (Tags/Blend, the
// [PerRendererData] _MainTex so it renders the CURRENT animation frame automatically, per-material CBUFFER
// uniforms). It stays overlay-compatible: the day/night MULTIPLY overlay re-darkens it afterward exactly like
// it does the normal Sprite-Unlit player and the SpriteShadow / wade-splash sprites.
//
// The pivot is the FEET (bottom-centre), so uv.y 0 = FEET, uv.y 1 = HEAD (FisherSheet 32×64 = 1×2 m). The
// PlayerSubmergeVisual component pushes the tunables + the live waterline via a per-player MaterialPropertyBlock
// (so only the player is affected — everyone else keeps the default sprite material and stays batched):
//   _WaterlineFrac        (0 dry .. up to ~0.85 neck; the fraction of the way up the body the water reaches)
//   _SubmergeTint         (the underwater water colour the submerged body tints toward)
//   _SubmergeTintAmount   (0 no tint .. 1 fully the tint colour)
//   _SubmergeDim          (0 no dim .. 1 fully dark; how much the submerged part darkens — dim-but-VISIBLE)
//   _RefractAmount        (horizontal wobble amplitude of the underwater refraction, in UV units)
//   _RefractFrequency     (how many wobble cycles up the submerged body)
//   _RefractSpeed         (how fast the wobble animates)
//   _WaterlineFoam        (0..1 brightness of the thin foam/ripple band at the waterline)
//   _WaterlineFoamWidth   (half-height of the foam band, in uv.y)
//   _PixelsPerUnit + _SpriteHeightPx  (pixel-snap the refraction offset to the sprite's pixel grid)
//
// CRITICAL: _WaterlineFrac == 0 is a PIXEL-IDENTICAL passthrough of the normal unlit sprite (dry land looks
// EXACTLY as today) — the whole underwater branch is gated on being below a positive waterline, so at frac 0
// nothing is below it and the fragment returns the raw _MainTex sample untouched. flipX-AGNOSTIC: the Right
// facing is mirrored on the MESH (SpriteRenderer.flipX), and this shader only ever reads/clips on uv.y
// (feet→head) — never uv.x sign — so a mirrored sprite submerges identically.
//
// Visual-only: drives no sim, saves nothing (rule 5). HLSL-trap-safe: no [unroll] over a runtime count, no
// dynamic-length loop, a single texture fetch (the refraction just offsets the sample coord), all branches on
// per-material uniforms. Force-compiled by WaterShaderCompileGuardTests via the shipped
// Resources/PlayerSubmerge.mat (the magenta guard scans every project material) — a break fails CI RED.
Shader "HiddenHarbours/PlayerSubmerge"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite (player)", 2D) = "white" {}
        _WaterlineFrac      ("Waterline fraction (0 dry .. ~0.85 neck)", Range(0,1)) = 0
        _SubmergeTint       ("Underwater tint colour", Color) = (0.16, 0.34, 0.44, 1)
        _SubmergeTintAmount ("Underwater tint amount (0..1)", Range(0,1)) = 0.45
        _SubmergeDim        ("Underwater dim (0 none .. 1 dark)", Range(0,1)) = 0.35
        _RefractAmount      ("Refraction wobble amplitude (uv)", Range(0,0.2)) = 0.012
        _RefractFrequency   ("Refraction wobble cycles up body", Float) = 9
        _RefractSpeed       ("Refraction wobble speed", Float) = 2.2
        _WaterlineFoam      ("Waterline foam brightness (0..1)", Range(0,1)) = 0.7
        _WaterlineFoamWidth ("Waterline foam half-width (uv.y)", Range(0.001,0.2)) = 0.03
        _PixelsPerUnit      ("Pixels per unit (pixel snap)", Float) = 32
        _SpriteHeightPx     ("Sprite height (px, pixel snap)", Float) = 64
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
                float4 color      : COLOR;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 color      : COLOR;
                float2 uv         : TEXCOORD0;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float  _WaterlineFrac;
                float4 _SubmergeTint;
                float  _SubmergeTintAmount;
                float  _SubmergeDim;
                float  _RefractAmount;
                float  _RefractFrequency;
                float  _RefractSpeed;
                float  _WaterlineFoam;
                float  _WaterlineFoamWidth;
                float  _PixelsPerUnit;
                float  _SpriteHeightPx;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = GetVertexPositionInputs(IN.positionOS).positionCS;
                OUT.color = IN.color;                       // honour the SpriteRenderer tint/vertex colour
                OUT.uv = TRANSFORM_TEX(IN.uv, _MainTex);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float2 uv = IN.uv;

                // Feet→head fraction (pivot at the feet: uv.y 0 = feet, 1 = head). Robust to PPU/pivot.
                float bodyFrac = saturate(uv.y);

                // How far BELOW the waterline this fragment is (0 at/above the line, up to 1 at the feet). At
                // _WaterlineFrac == 0 this is 0 everywhere → the whole underwater branch contributes nothing,
                // so the fragment is a PIXEL-IDENTICAL passthrough of the normal unlit sprite (dry = today).
                float below = saturate(_WaterlineFrac - bodyFrac);   // >0 only strictly under the line
                bool submerged = below > 0.0;

                // --- REFRACTION: offset the SAMPLE horizontally by a per-row animated sine of uv.y + _Time,
                // pixel-snapped to the sprite's pixel grid so the wobble reads as chunky pixel-art shimmer, not
                // a smooth smear. Only the submerged rows are offset (above the line: zero offset). ---
                float wob = sin(uv.y * _RefractFrequency * 6.2831853 + _Time.y * _RefractSpeed);
                // Deeper (nearer the feet) shimmers a touch more — scale by how far under the line we are.
                float amp = _RefractAmount * (0.5 + 0.5 * below) * (submerged ? 1.0 : 0.0);
                float uOff = wob * amp;
                // Snap the horizontal offset to whole texture pixels so it stays pixel-art crisp (no sub-pixel
                // crawl). One texel in uv.x == 1/spriteWidthPx; approximate spriteWidthPx from the height and a
                // square-ish texel assumption isn't safe, so snap in uv directly against the pixel grid derived
                // from the sprite's own pixel height (feet→head is _SpriteHeightPx tall; texels are square).
                float texelU = (_SpriteHeightPx > 0.5) ? (1.0 / _SpriteHeightPx) : (1.0 / 64.0);
                uOff = (texelU > 1e-6) ? (round(uOff / texelU) * texelU) : uOff;
                float2 sampleUV = float2(uv.x + uOff, uv.y);

                // Sample the CURRENT animation frame (refracted coord under the line, raw coord above it).
                half4 src = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, sampleUV);
                src *= IN.color;                               // fold in the SpriteRenderer colour/alpha

                // Above the line (or dry): return the sprite UNCHANGED. This early-out guarantees the exact
                // passthrough — no tint, no dim, no offset (uOff was 0 there anyway) touches the dry sprite.
                if (!submerged)
                {
                    if (src.a <= 0.0) discard;
                    return src;
                }

                // --- UNDERWATER LOOK on the submerged part ---
                half3 col = src.rgb;

                // (a) Tint toward the water colour.
                col = lerp(col, _SubmergeTint.rgb, saturate(_SubmergeTintAmount));

                // (b) Dim it (darken, keeping it VISIBLE — a multiply, never to full black at sane tunables).
                col *= (1.0 - saturate(_SubmergeDim));

                half a = src.a;

                // (c) A bright foam / ripple line right AT the waterline (a thin band centred on _WaterlineFrac).
                // Distance in uv.y from the waterline; brightest at the line, feathering over the half-width.
                float dLine = abs(bodyFrac - _WaterlineFrac);
                float foam = saturate(1.0 - dLine / max(_WaterlineFoamWidth, 1e-4));
                foam = foam * foam * (3.0 - 2.0 * foam);       // smoothstep-ish soft band
                foam *= saturate(_WaterlineFoam) * a;          // only where the sprite is opaque
                col = saturate(col + foam);                     // lift toward white at the ripple line

                half4 outCol = half4(col, a);
                if (outCol.a <= 0.0) discard;
                return outCol;
            }
            ENDHLSL
        }
    }
    Fallback Off
}
