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
//   (2) FOOTSTEP TRAIL — the grass parts along a walker's recent PATH and springs back behind them, a trodden
//       trail rather than a halo circling them. HiddenHarbours.Art.GrassFootstep uploads the last POINTS_N world
//       positions into the GLOBAL array _GrassTrail (xy = pos, z = recency 0..1, w = the heading angle the walker
//       was moving when the footprint was laid); each disturbs only a footprint-sized _FootRadius, fades by
//       recency, and (while that walker moves) bends only grass BEHIND the heading — grass ahead stays upright
//       until trodden. No per-blade state is stored (CLAUDE.md rule 5).
//
//       ⚠️ MANY WALKERS, NOT ONE (2026-08-13). _GrassTrail is a POOL: WALKERS_N segments of POINTS_N points, one
//       segment per GrassFootstep. It used to be a single 24-point array every instance wrote, so a second walker
//       did not add a second trail — it ERASED the first every frame (last writer wins), which is why the village
//       routines had to leave the component off the villagers. The companion global _GrassWalkers carries one
//       record per segment: xy = the bounding-circle centre of that walker's live trail, z = its radius (NEGATIVE
//       marks an unclaimed slot), w = that walker's 0..1 speed factor for the behind-only gate (this replaces the
//       old scalar _PlayerMoving, which could only ever describe one walker). The circle lets a blade skip a
//       whole segment it cannot be reached by — pure optimisation, since a culled point's falloff is already 0.
//
// The bend weight is the sprite UV.y (0 at the root -> 1 at the tip), squared so the base stays planted and the
// tip moves most. EVERY amplitude / speed / radius is a material or global property (rule 6). Pixel-art faithful:
// the bend offset is SNAPPED to the PPU grid like the water shader, point-sampled, PPU 32. Visual-only: drives no
// sim, saves nothing (rule 5).
//
// SHADER CAUTIONS honoured (this project lost hours to a magenta shader): NO '+' or other operator characters in
// ANY [Header(...)] label or Property display string (ShaderLab parse error -> magenta); NO [unroll] over a
// runtime loop bound (both footstep bounds are compile-time #defines — the inner one is [unroll]ed, the outer
// walker sweep is deliberately [loop]ed so the early-out actually skips work instead of being unrolled flat).
// The grass material's shipped variant is force-compiled headless by
// Assets/Tests/EditMode/Art/GrassShaderCompileGuardTests.cs so a broken grass shader fails CI red.
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

        [Header(Footstep trail (walkers tread paths the grass springs back from))]
        // A footprint-sized disturbance (NOT a wide halo): each point of a walker's recent PATH parts the grass
        // within this radius. Keep it near a walker's footprint so only grass actually walked over reacts. It is
        // also the slack on the per-walker cull circle, so a bigger radius costs a little more work.
        _FootRadius ("Footstep radius (m)", Float) = 0.5
        _FootStrength ("Footstep push strength (m)", Float) = 0.4
        // Directional gate: while a walker is MOVING, only grass BEHIND each of their footprints (relative to the
        // direction they were heading when they made it) bends — grass AHEAD of the foot stays upright until it
        // is actually trodden, so the parting trails the walk instead of bulging ahead of it. This is the
        // transition width (m) of that front-to-back cut. When a walker is still, their gate relaxes to symmetric.
        _FootDirSoftness ("Footstep behind only softness (m)", Float) = 0.12
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

            // GLOBAL sim/player inputs (set by the bridges; not per-material, so OUTSIDE the per-material CBUFFER).
            // _WindWorld = wind dir * normalized strength (0..1). Default (0,0,0,0): no wind.
            float4 _WindWorld;
            // _GrassTrail = the walkers' recent PATHS (GrassFootstep, via SetGlobalVectorArray), as WALKERS_N
            // fixed-stride SEGMENTS of POINTS_N points. Per point: xy = world pos, z = recency 0..1 (1 just
            // stepped, fading to 0 as the grass springs back), w = the heading ANGLE (radians) that walker was
            // moving when this footprint was laid (so the bend can be gated to BEHIND the direction of travel).
            // All z = 0 by default → no bend until someone actually walks.
            // _GrassWalkers = one record per segment: xy = the bounding-circle CENTRE of that walker's live trail,
            // z = its RADIUS in metres (NEGATIVE = the slot is unclaimed), w = that walker's 0..1 speed factor, so
            // the directional behind-only gate fades in while THAT walker is moving and relaxes to symmetric when
            // they stand still (grass underfoot still parts when idle). One gate per walker, not one for the scene.
            // WALKERS_N / POINTS_N are COMPILE-TIME constants (so the [unroll] below has a fixed bound — never an
            // [unroll] over a runtime count) and MUST match GrassFootstep.MaxWalkers / PointsPerWalker; the pair is
            // pinned across C# and BOTH shaders by Assets/Tests/EditMode/Art/GrassTrailPoolTests.cs.
            #define WALKERS_N 8
            #define POINTS_N 24
            #define TRAIL_N (WALKERS_N * POINTS_N)
            float4 _GrassTrail[TRAIL_N];
            float4 _GrassWalkers[WALKERS_N];

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
                float  _FootDirSoftness;
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
                // ⚠️ This shaping happens PER VERTEX, so it only survives on a TESSELLATED sprite. On a FullRect
                // quad it is evaluated at uv.y 0 and 1 only and interpolates linearly, and the squaring does
                // nothing at all (0^2 = 0, 1^2 = 1). The grass tufts import Tight, so it works here — the trees
                // did not, which is the defect WindBendTessellationTests now pins for both.
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

                // ---- FOOTSTEP TRAIL ----
                // Bend away from each walker's recent PATH, not a single point: every _GrassTrail point disturbs
                // only a footprint-sized radius (_FootRadius) and fades by its recency (z), so the grass parts
                // ALONG the path that was walked and recovers behind — a trodden trail, not a halo circling anyone.
                // BEHIND-ONLY: while a walker moves (their _GrassWalkers record's w), gate each of THEIR footprints
                // so it bends grass only BEHIND the heading it was laid with (the point's w) — grass AHEAD of the
                // foot stays upright until trodden. The gate relaxes to symmetric when that walker is still. Take
                // the STRONGEST nearby point across ALL walkers (max, not sum) so overlapping footprints — one
                // walker's or two walkers' — never stack into a bulge.
                //
                // The outer sweep is [loop] (NOT unrolled) so its early-out is worth having: one bounding-circle
                // test per walker skips that walker's whole POINTS_N segment. That cull can only ever discard
                // points whose falloff is already 0, so it changes cost, never picture — and it makes a lone
                // walker in a big region CHEAPER per blade than the old single un-culled 24-point loop.
                float  bestFp  = 0.0;
                float2 bestDir = float2(0.0, 1.0);
                float  foot    = max(_FootRadius, 1e-3);
                [loop]
                for (int wi = 0; wi < WALKERS_N; wi++)
                {
                    float4 wk = _GrassWalkers[wi];
                    if (wk.z < 0.0) continue;                 // unclaimed slot
                    float2 toC  = wp.xy - wk.xy;              // trail centre -> blade
                    float  cull = wk.z + foot;                // no point of this walker can reach further
                    if (dot(toC, toC) > cull * cull) continue;

                    int   pBase  = wi * POINTS_N;
                    float moving = saturate(wk.w);
                    [unroll]
                    for (int pi = 0; pi < POINTS_N; pi++)
                    {
                        float4 tp = _GrassTrail[pBase + pi];
                        float2 to = wp.xy - tp.xy;            // footprint -> blade
                        float  d  = length(to);
                        float  reach = (1.0 - smoothstep(0.0, foot, d)) * saturate(tp.z);

                        // directional gate: ahead = component of `to` along the walker's heading at this footprint.
                        // Blades ahead (ahead > 0) are cut out; blades behind (ahead <= 0) bend fully. Blended
                        // toward 1 (symmetric) by (1 - moving) so a standing walker still flattens grass underfoot.
                        float2 fwd = float2(cos(tp.w), sin(tp.w));
                        float  ahead = dot(to, fwd);
                        float  behind = 1.0 - smoothstep(0.0, max(_FootDirSoftness, 1e-4), ahead);
                        float  gate = lerp(1.0, behind, moving);

                        float fp = reach * gate;
                        if (fp > bestFp)
                        {
                            bestFp  = fp;
                            bestDir = d > 1e-4 ? to / d : float2(0.0, 1.0);
                        }
                    }
                }
                float2 footOffset = bestDir * (bestFp * _FootStrength);

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
