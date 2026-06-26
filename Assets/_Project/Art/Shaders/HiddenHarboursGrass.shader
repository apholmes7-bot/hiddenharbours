// HiddenHarboursGrass.shader — living grass for the on-foot areas (St Peters clearings / forest).
//
// A custom URP 2D UNLIT sprite ShaderLab/HLSL shader (NOT a Shader Graph — authored as text so it builds
// headless, mirroring HiddenHarboursWater). A grass-tuft sprite is planted at its BASE (bottom-centre pivot);
// this shader bends the TOP of the sprite in the VERTEX stage while the base stays rooted, so hundreds of
// tufts sway + recover entirely on the GPU (no CPU per-blade animation — CLAUDE.md rule 7). Two forces:
//
//   (1) WIND SWAY — read off the SAME deterministic wind the water reads. HiddenHarbours.Art.GrassWindBridge
//       pushes EnvironmentSample.WindVector into the GLOBAL vector _WindWorld (direction * 0..1 strength) on a
//       throttled tick, so a gust leans the grass AND ripples the water together (the cohesion the owner asked
//       for). A steady lean (holds the blades leaned in a strong wind) PLUS a travelling gust ripple that moves
//       DOWNWIND across the field, decorrelated per-tuft so the field never sways in lockstep.
//
//   (2) FOOTSTEP BEND — grass within _FootRadius of the player bends AWAY from them and springs back once they
//       leave. HiddenHarbours.Art.GrassFootstep pushes the player position into the GLOBAL vector _PlayerWorld;
//       the bend is (1 - smoothstep(0, radius, dist)) away from the player. Recovery is automatic: it tracks the
//       LIVE player position, so no per-blade state is stored (CLAUDE.md rule 5 — no hidden simulation state).
//
// The bend weight is the sprite UV.y (0 at the root -> 1 at the tip), squared so the base stays planted and the
// tip moves most. EVERY amplitude / speed / radius is a material or global property (rule 6). Pixel-art faithful:
// the bend offset is SNAPPED to the PPU grid like the water shader, point-sampled, PPU 32. Visual-only: drives no
// sim, saves nothing (rule 5).
//
// SHADER CAUTIONS honoured (this project lost hours to a magenta shader): NO '+' or other operator characters in
// ANY [Header(...)] label or Property display string (ShaderLab parse error -> magenta); NO [unroll] over a
// runtime loop bound (this shader has no loops). The grass material's shipped variant is force-compiled headless
// by Assets/Tests/EditMode/Art/GrassShaderCompileGuardTests.cs so a broken grass shader fails CI red.
Shader "HiddenHarbours/GrassWind"
{
    Properties
    {
        [Header(Sprite)]
        [NoScaleOffset] _MainTex ("Grass tuft sprite", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)
        _AlphaClip ("Alpha clip threshold", Range(0, 1)) = 0.01

        [Header(Pixelization)]
        _PixelsPerUnit ("Pixels per unit", Float) = 32

        [Header(Wind sway (driven by the shared wind via _WindWorld))]
        // _SwayAmount: extra tip sway (metres) at full wind strength, on top of the idle baseline.
        _SwayAmount ("Sway amount at full wind (m)", Float) = 0.22
        // _IdleSway: a small wind-INDEPENDENT baseline so the grass always has a little life (and the demo
        // shows motion even before the sim feeds wind). Set to 0 for dead-calm when there is no wind.
        _IdleSway ("Idle baseline sway (m)", Float) = 0.04
        // _WindLean: how much a STEADY wind holds the tips leaned over (0 = only gust ripple, 1 = strong lean).
        _WindLean ("Steady lean from wind (0..1)", Range(0, 1)) = 0.6
        _SwaySpeed ("Gust temporal speed", Float) = 2.2
        // _GustScale: spatial frequency (per metre) of the travelling gust wave — it rolls DOWNWIND across the
        // field so the same gust crosses the whole patch (cohesion), like the water swell.
        _GustScale ("Gust travel scale (per m)", Float) = 0.35
        _GustStrength ("Gust ripple strength (0..1)", Range(0, 1)) = 0.7
        // _PhaseGrid: tuft-decorrelation cell size (m). Tufts in different cells get a different gust phase so
        // the field never sways as one flat sheet. Roughly your tuft spacing.
        _PhaseGrid ("Phase decorrelation grid (m)", Float) = 1.0
        // _BendY: how much the tip DIPS in Y as it bends sideways (foreshorten) so a hard bend reads as the
        // blade folding over rather than stretching. Small.
        _BendY ("Bend foreshorten (0..1)", Range(0, 1)) = 0.25

        [Header(Footstep bend (driven by the player via _PlayerWorld))]
        _FootRadius ("Footstep radius (m)", Float) = 1.4
        _FootStrength ("Footstep push strength (m)", Float) = 0.5
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        Pass
        {
            // Unlit transparent, drawn by the 2D renderer (one implied sun elsewhere; grass is flat-lit here).
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

            // GLOBAL sim/player inputs (Shader.SetGlobalVector from the bridges; not per-material, so they are
            // OUTSIDE the per-material CBUFFER). _WindWorld = wind dir * normalized strength (0..1). _PlayerWorld
            // = player world position (xy). Default (0,0,0,0): no wind, player effectively far away.
            float4 _WindWorld;
            float4 _PlayerWorld;

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float  _AlphaClip;
                float  _PixelsPerUnit;
                float  _SwayAmount;
                float  _IdleSway;
                float  _WindLean;
                float  _SwaySpeed;
                float  _GustScale;
                float  _GustStrength;
                float  _PhaseGrid;
                float  _BendY;
                float  _FootRadius;
                float  _FootStrength;
            CBUFFER_END

            // Snap a world offset to the PPU grid so the bend moves in WHOLE pixels (crisp pixel art, no
            // sub-pixel shimmer) — the same discipline the water shader applies to its sample coords.
            float2 PixelSnap(float2 p)
            {
                float ppu = max(_PixelsPerUnit, 1.0);
                return floor(p * ppu) / ppu;
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);

                VertexPositionInputs vpos = GetVertexPositionInputs(IN.positionOS);
                float3 wp = vpos.positionWS;

                // Bend weight: 0 at the root (uv.y = 0) -> 1 at the tip. Squared so the base stays planted and
                // the displacement concentrates toward the tip.
                float bendW = saturate(IN.uv.y);
                bendW = bendW * bendW;

                // ---- WIND ----
                // _WindWorld is direction * normalized-strength (0..1) from GrassWindBridge.
                float2 windVec = _WindWorld.xy;
                float  windStr = length(windVec);
                float2 wdir = windStr > 1e-4 ? windVec / windStr : float2(1.0, 0.0);

                // per-tuft decorrelated phase (different gust phase per PhaseGrid cell so the field is not in lockstep)
                float2 cell  = floor(wp.xy / max(_PhaseGrid, 0.01));
                float  phase = cell.x * 12.9898 + cell.y * 78.233;

                // travelling gust: rolls DOWNWIND (projection along wind advances with time) so one gust crosses
                // the whole patch — the cohesion with the water swell. Two beats slightly out of phase read organic.
                float travel = dot(wp.xy, wdir) * _GustScale;
                float gust = sin(_Time.y * _SwaySpeed - travel + phase) * 0.6
                           + sin(_Time.y * _SwaySpeed * 1.7 - travel * 1.3 + phase) * 0.4;

                // steady lean holds the blades over in a real wind; the gust ripples around it. Amplitude grows
                // with wind strength, plus the wind-independent idle baseline.
                float  swayMag = _IdleSway + windStr * _SwayAmount;
                float  lean    = windStr * _WindLean;
                float2 windOffset = wdir * ((lean + gust * _GustStrength) * swayMag);

                // ---- FOOTSTEP ----
                // bend AWAY from the live player position; recovery is automatic (tracks _PlayerWorld each frame).
                float2 toBlade = wp.xy - _PlayerWorld.xy;
                float  d   = length(toBlade);
                float  fp  = 1.0 - smoothstep(0.0, max(_FootRadius, 1e-3), d);
                float2 fdir = d > 1e-4 ? toBlade / d : float2(0.0, 1.0);
                float2 footOffset = fdir * (fp * _FootStrength);

                // combine, weight by the tip bend, foreshorten in Y, and pixel-snap.
                float2 offset = (windOffset + footOffset) * bendW;
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
                // discard near-transparent texels so the tuft silhouette stays clean and sorts without a quad halo.
                clip(col.a - _AlphaClip);
                return col;
            }
            ENDHLSL
        }
    }

    Fallback Off
}
