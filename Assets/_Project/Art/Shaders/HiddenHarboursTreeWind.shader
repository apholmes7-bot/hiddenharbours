// HiddenHarboursTreeWind.shader — gentle canopy wind-sway for the trees (sibling of HiddenHarboursGrass).
//
// A custom URP 2D UNLIT sprite shader (text HLSL, builds headless). A tree sprite is planted at its TRUNK
// (bottom-centre pivot); this shader keeps the trunk rooted and sways only the CANOPY above _TrunkAnchor, off the
// SAME deterministic wind the grass and water read — GrassWindBridge publishes EnvironmentSample.WindVector into
// the GLOBAL vector _WindWorld (direction * 0..1 strength), so a gust leans the trees, ruffles the grass, and
// ripples the water TOGETHER (one world).
//
// Trees are stiffer and far taller than grass, so vs the grass shader this: (1) anchors the bend ABOVE a trunk
// fraction (smoothstep(_TrunkAnchor,1,uv.y)) instead of from the very base; (2) uses gentler, slower defaults;
// and (3) decorrelates per-tree with a SMOOTH value-noise phase (NOT grass's floor-cell phase) so a 5.5 m sprite
// never shows a hard phase seam down its trunk — the canopy flexes naturally and neighbouring trees differ.
// There is NO footstep interaction (trees don't bend underfoot) and NO loops (so no [unroll] magenta trap).
//
// SHADER CAUTIONS honoured: NO operator characters in any [Header(...)] label or Property string (ShaderLab parse
// error -> magenta); helpers declared BEFORE use; globals OUTSIDE the per-material CBUFFER, tunables INSIDE it;
// pixel-snapped + point-sampled, PPU 32. Visual-only: drives no sim, saves nothing (rule 5). The Tree material's
// shipped variant is force-compiled headless by Assets/Tests/EditMode/Art/TreeWindShaderCompileGuardTests.cs.
Shader "HiddenHarbours/TreeWind"
{
    Properties
    {
        [Header(Sprite)]
        [NoScaleOffset] _MainTex ("Tree sprite", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)
        _AlphaClip ("Alpha clip threshold", Range(0, 1)) = 0.01

        [Header(Pixelization)]
        _PixelsPerUnit ("Pixels per unit", Float) = 32

        [Header(Trunk anchor (below this the tree stays planted))]
        // uv.y below which there is NO sway (the trunk and lowest boughs stay rooted). 0 = sway from the base
        // like grass, higher = a taller planted trunk. The canopy sway ramps up from here to the crown.
        _TrunkAnchor ("Trunk anchor (uv.y, planted below)", Range(0, 0.8)) = 0.14

        [Header(Canopy wind sway (driven by the shared wind via _WindWorld))]
        _SwayAmount ("Sway amount at full wind (m)", Float) = 0.12
        // A small wind-INDEPENDENT baseline so the canopy always has a hint of life (and the look is sane before
        // the wind sim feeds it). Set to 0 for dead-still trees with no wind.
        _IdleSway ("Idle baseline sway (m)", Float) = 0.02
        // How much a STEADY wind holds the crown leaned over (0 = only gust ripple, 1 = a strong lean).
        _WindLean ("Steady lean from wind (0..1)", Range(0, 1)) = 0.35
        _SwaySpeed ("Gust temporal speed (slow for trees)", Float) = 1.1
        // Spatial frequency (per metre) of the travelling gust wave — it rolls downwind across the stand.
        _GustScale ("Gust travel scale (per m)", Float) = 0.2
        _GustStrength ("Gust ripple strength (0..1)", Range(0, 1)) = 0.6
        // Per-tree decorrelation: the spatial frequency of the SMOOTH phase noise. Small = broad, slow variation
        // (one tree flexes coherently, neighbours differ). No hard cell seams, unlike the grass.
        _PhaseScale ("Phase noise scale (per m)", Float) = 0.12
        // How much the crown DIPS in Y as it leans (foreshorten) so a big lean reads as the canopy folding over.
        _BendY ("Bend foreshorten (0..1)", Range(0, 1)) = 0.2
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

            // GLOBAL shared wind (Shader.SetGlobalVector from GrassWindBridge; NOT per-material, so OUTSIDE the
            // CBUFFER). _WindWorld = wind dir * normalized strength (0..1). Default (0,0,0,0) = no wind.
            float4 _WindWorld;

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float  _AlphaClip;
                float  _PixelsPerUnit;
                float  _TrunkAnchor;
                float  _SwayAmount;
                float  _IdleSway;
                float  _WindLean;
                float  _SwaySpeed;
                float  _GustScale;
                float  _GustStrength;
                float  _PhaseScale;
                float  _BendY;
            CBUFFER_END

            // ---- helpers (declared BEFORE use) ----------------------------------------------------------------
            // Snap a world offset to the PPU grid so the sway moves in WHOLE pixels (crisp pixel art).
            float2 PixelSnap(float2 p)
            {
                float ppu = max(_PixelsPerUnit, 1.0);
                return floor(p * ppu) / ppu;
            }

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 345.45));
                p += dot(p, p + 34.345);
                return frac(p.x * p.y);
            }

            // Smooth value noise (smoothstep interpolation) — NO hard jumps, so a tall tree's phase varies
            // gently across its body (a natural flex) instead of seaming at a cell boundary.
            float ValueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                float a = Hash21(i + float2(0, 0));
                float b = Hash21(i + float2(1, 0));
                float c = Hash21(i + float2(0, 1));
                float d = Hash21(i + float2(1, 1));
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);

                VertexPositionInputs vpos = GetVertexPositionInputs(IN.positionOS);
                float3 wp = vpos.positionWS;

                // Trunk-anchored canopy weight: 0 below _TrunkAnchor (trunk planted), easing up to 1 at the crown.
                // Squared so the motion accelerates into the upper canopy rather than hinging at the anchor.
                float bendW = smoothstep(_TrunkAnchor, 1.0, saturate(IN.uv.y));
                bendW = bendW * bendW;

                // ---- shared wind ----
                float2 windVec = _WindWorld.xy;
                float  windStr = length(windVec);
                float2 wdir = windStr > 1e-4 ? windVec / windStr : float2(1.0, 0.0);

                // smooth per-tree phase (value noise; no hard seam down a tall sprite)
                float phase = ValueNoise(wp.xy * max(_PhaseScale, 1e-3)) * 6.2831853;

                // travelling gust rolls downwind; two beats slightly out of phase read organic.
                float travel = dot(wp.xy, wdir) * _GustScale;
                float gust = sin(_Time.y * _SwaySpeed - travel + phase) * 0.6
                           + sin(_Time.y * _SwaySpeed * 1.7 - travel * 1.3 + phase) * 0.4;

                // steady lean holds the crown over in a real wind; the gust ripples around it.
                float  swayMag = _IdleSway + windStr * _SwayAmount;
                float  lean    = windStr * _WindLean;
                float2 windOffset = wdir * ((lean + gust * _GustStrength) * swayMag);

                float2 offset = windOffset * bendW;
                float  yDip   = -length(offset) * _BendY;
                offset = PixelSnap(offset);
                wp.xy += offset;
                wp.y  += PixelSnap(float2(0.0, yDip)).y;

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
                return col;
            }
            ENDHLSL
        }
    }

    Fallback Off
}
