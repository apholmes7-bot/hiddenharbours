// HiddenHarboursFlower.shader — the PEI wildflowers, swaying to the real sim wind. Sibling of
// HiddenHarboursGrass / HiddenHarboursTreeWind: a custom URP 2D UNLIT sprite ShaderLab/HLSL shader (NOT a Shader
// Graph — authored as text so it builds headless).
//
// WHY A FLOWER SHADER AND NOT JUST THE GRASS ONE. Two reasons, both fatal to reusing Grass.mat:
//
//  (1) THE ATLAS UV TRAP. The grass shader bends by `saturate(IN.uv.y)`, which assumes uv.y runs 0->1 across the
//      sprite. That is true for grass (spriteMode Single, the whole texture). The flower sheets are spriteMode
//      MULTIPLE, so uv.y is the ATLAS SUB-RECT coordinate: on a 4x3 Single sheet the bottom row spans uv.y
//      0.000..0.333 and the top row 0.667..1.000. Dropped on Grass.mat, top-row flowers would tear their roots out
//      of the ground while bottom-row flowers barely twitched. Here the bend weight is the CELL-LOCAL v,
//      `frac(uv.y * _Rows)`, which is 0 at every cell's base and 1 at its tip regardless of row — and, unlike an
//      object-space-height scheme, it needs no per-tier height constant and survives any scaling of the flower.
//
//  (2) THE ART IS ALREADY ANIMATED. The art director drew each flower as 4 side-by-side sway poses. Measured
//      across all 33 sheets / 66 rows, the columns are a clean sine cycle — col 0 neutral, col 1 leaned right,
//      col 2 PIXEL-IDENTICAL to col 0, col 3 leaned left. So we SELECT THE COLUMN in-shader rather than throw 75%
//      of the art away (or introduce an Animator per flower, which would batch-break and, worse, could not react
//      to the wind at all). Because every tier is exactly 4 columns, a cell is exactly 1/_Cols wide in UV, so
//      `frac(uv.x + k / _Cols)` picks pose (c+k) mod 4 OF THE SAME ROW with ZERO per-instance data. It batches.
//
// THE TWO MOTIONS COMPOSE. The drawn poses give the flower's own hand-articulated sway (a fixed few pixels); the
// vertex bend adds the wind's FORCE on top (amplitude grows with wind, and a steady wind holds a lean). Both are
// driven by the SAME deterministic sim wind every other surface reads: GrassWindBridge (the project's only wind
// publisher, despite the name) pushes EnvironmentSample.WindVector into the GLOBAL _WindWorld, so a gust leans the
// flowers, ruffles the grass, bends the trees and ripples the water together. Declaring `float4 _WindWorld` OUTSIDE
// the per-material CBUFFER is the whole of the wiring — there is no flower wind component.
//
// PER-FLOWER PHASE COMES FROM THE OBJECT ORIGIN, NOT THE VERTEX. The column select is QUANTISED, so its input must
// be identical for all 4 vertices of the quad or the flower renders half in one pose and half in another (a torn
// flower). Vertex world position — what grass and trees phase from — varies across the quad and would do exactly
// that. `mul(unity_ObjectToWorld, float4(0,0,0,1)).xy` is the sprite's root and is constant per flower.
// Assets/Tests/EditMode/Art/SpriteObjectMatrixGuardTests.cs PROVES a batched SpriteRenderer keeps that matrix
// (measured: two sprites sharing one material read their true origins). If that ever regressed, every flower would
// read root (0,0) and the meadow would flip poses in lockstep — that guard is what catches it.
//
// THE PATCH TIER IS DELIBERATELY DIFFERENT. A Patch is flat ground-cover on a CENTRE pivot seen near-top-down;
// hinging it at its bottom edge would read as a broken flap. _RootedBend is the knob: 1 = hinge at the root
// (Single / Clump, which are bottom-centre pivoted stems), 0 = the whole sprite drifts as one (Patch). And the
// art agrees — the Patch columns are drawn as a whole-sprite lateral shimmer, so on Patch the DRAWN poses are the
// sway and the geometric bend is turned down to a whisper. See Flower_Patch.mat.
//
// SHADER CAUTIONS honoured (this project lost hours to a magenta shader): NO operator characters in ANY
// [Header(...)] label or Property display string (ShaderLab parse error -> magenta); NO [unroll] over a RUNTIME
// loop bound (the footstep loop's TRAIL_N is a compile-time #define); helpers declared BEFORE use; globals OUTSIDE
// the CBUFFER and tunables INSIDE it. Pixel-art faithful: the bend offset is SNAPPED to the PPU grid, point
// sampled, PPU 32. Visual-only: drives no sim, saves nothing (rule 5). Every amplitude, speed and radius is a
// material property (rule 6). The three shipped Flower_*.mat variants are force-compiled headless by
// Assets/Tests/EditMode/Art/FlowerShaderCompileGuardTests.cs so a broken flower shader fails CI red.
//
// ONE STANDING HAZARD, CALLED OUT LOUDLY: the `frac(uv.x + k / _Cols)` column select holds because a sprite's UVs
// map DIRECTLY onto its source texture — there is NO SpriteAtlas in this project today. If a SpriteAtlas is ever
// introduced and it packs these sheets, sprite UVs are remapped into the atlas page, cells stop being 1/_Cols
// apart, and the column select will sample NEIGHBOURING FLOWERS' pixels. Exclude Art/Foliage/Flowers from any
// atlas, or replace this with a per-sprite rect uniform.
Shader "HiddenHarbours/FlowerWind"
{
    Properties
    {
        [Header(Sprite)]
        [NoScaleOffset] _MainTex ("Flower sheet", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)
        _AlphaClip ("Alpha clip threshold", Range(0, 1)) = 0.01

        [Header(Pixelization)]
        _PixelsPerUnit ("Pixels per unit", Float) = 32

        [Header(Sheet grid (must match the slicer for this tier))]
        // The sheet's cell grid. _Cols is also the number of drawn sway poses (every tier is 4 columns of sway).
        // _Rows is what makes the bend weight cell local: Single 3 bloom rows, Clump 1, Patch 2 variant rows.
        // WRONG VALUES HERE bend the flower about the wrong line and sample the wrong pose — they are asserted
        // against the slicer's own tier table by FlowerMaterialTests.
        _Cols ("Sway pose columns", Float) = 4
        _Rows ("Rows on the sheet", Float) = 3

        [Header(Drawn sway poses (the art directors 4 hand drawn frames))]
        // How fast the flower steps through its 4 drawn poses. This is the flower's OWN articulation; the wind
        // sets the RATE (calm sways slowly, a gale hurries) while the vertex bend below supplies the amplitude.
        // Idle is a wind INDEPENDENT baseline so a flower always has a little life. NOTE: with BOTH of these at 0
        // the pose freezes per flower at an arbitrary one of the 4 (each flower holds its own phase) rather than
        // snapping to neutral. Set _PoseWindSpeed to 0 and _PoseIdleSpeed low for near stillness instead.
        _PoseIdleSpeed ("Pose cycle idle speed", Float) = 1.1
        _PoseWindSpeed ("Pose cycle speed added at full wind", Float) = 3.4

        [Header(Bend shape)]
        // 1 = ROOTED: the base stays planted and the tip does the moving (stems: Single and Clump).
        // 0 = WHOLE SPRITE: the sprite drifts bodily, no hinge (flat ground cover: Patch).
        _RootedBend ("Rooted hinge (1) versus whole sprite drift (0)", Range(0, 1)) = 1

        [Header(Wind sway (driven by the shared wind via _WindWorld))]
        _SwayAmount ("Sway amount at full wind (m)", Float) = 0.1
        _IdleSway ("Idle baseline sway (m)", Float) = 0.02
        _WindLean ("Steady lean from wind (0..1)", Range(0, 1)) = 0.5
        _SwaySpeed ("Gust temporal speed", Float) = 2
        _GustScale ("Gust travel scale (per m)", Float) = 0.35
        _GustStrength ("Gust ripple strength (0..1)", Range(0, 1)) = 0.7
        // Per flower decorrelation: the spatial frequency of the SMOOTH phase noise (as the trees do it, not the
        // grass cell hash) so neighbouring flowers differ while a drift of blooms still reads as one meadow.
        _PhaseScale ("Phase noise scale (per m)", Float) = 0.5
        _BendY ("Bend foreshorten (0..1)", Range(0, 1)) = 0.2

        [Header(Footstep trail (the player brushes past and the flowers give))]
        // Same trail the grass reads (GrassFootstep on the player publishes it). Tested at the flower ROOT, so a
        // flower gives as a whole rather than folding in half.
        _FootRadius ("Footstep radius (m)", Float) = 0.5
        _FootStrength ("Footstep push strength (m)", Float) = 0.35
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
            // _GrassTrail = the player's recent PATH (GrassFootstep, via SetGlobalVectorArray): xy = world pos,
            // z = recency 0..1, w = the heading angle (radians) the player was moving when the footprint was laid.
            // TRAIL_N is a COMPILE-TIME constant so the [unroll] below has a fixed bound.
            #define TRAIL_N 24
            float4 _GrassTrail[TRAIL_N];
            float _PlayerMoving;

            #define TAU 6.2831853

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float  _AlphaClip;
                float  _PixelsPerUnit;
                float  _Cols;
                float  _Rows;
                float  _PoseIdleSpeed;
                float  _PoseWindSpeed;
                float  _RootedBend;
                float  _SwayAmount;
                float  _IdleSway;
                float  _WindLean;
                float  _SwaySpeed;
                float  _GustScale;
                float  _GustStrength;
                float  _PhaseScale;
                float  _BendY;
                float  _FootRadius;
                float  _FootStrength;
                float  _FootDirSoftness;
            CBUFFER_END

            // ---- helpers (declared BEFORE use) ----------------------------------------------------------------

            // Snap a world offset to the PPU grid so the bend moves in WHOLE pixels (crisp pixel art).
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

            // Smooth value noise — neighbouring flowers get different phases without the hard cell seams the
            // grass hash produces.
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

                // The flower's ROOT in world space — CONSTANT across the quad (see the header: this is what makes
                // the quantised pose select tear free, and it is guarded by SpriteObjectMatrixGuardTests).
                float2 root = mul(unity_ObjectToWorld, float4(0.0, 0.0, 0.0, 1.0)).xy;

                // CELL LOCAL uv: the sheet is a _Cols x _Rows grid, so frac(uv * grid) is 0..1 WITHIN this flower's
                // own cell — 0 at its base, 1 at its tip — whatever row of the sheet it came from.
                float2 cellUV = frac(IN.uv * float2(max(_Cols, 1.0), max(_Rows, 1.0)));

                // Bend weight. Rooted: squared cell-local v, so the base stays planted and the tip moves most.
                // Whole sprite: a flat 1, so the sprite drifts bodily with no hinge (the Patch tier).
                float rooted = saturate(cellUV.y);
                rooted = rooted * rooted;
                float bendW = lerp(1.0, rooted, saturate(_RootedBend));

                // ---- WIND (the same shared _WindWorld the grass, trees and water read) ----
                float2 windVec = _WindWorld.xy;
                float  windStr = length(windVec);
                float2 wdir = windStr > 1e-4 ? windVec / windStr : float2(1.0, 0.0);

                // Per flower smooth phase + the travelling gust, both off the ROOT so one flower is one phase.
                float phase  = ValueNoise(root * max(_PhaseScale, 1e-3)) * TAU;
                float travel = dot(root, wdir) * _GustScale;
                float gust = sin(_Time.y * _SwaySpeed - travel + phase) * 0.6
                           + sin(_Time.y * _SwaySpeed * 1.7 - travel * 1.3 + phase) * 0.4;

                float  swayMag = _IdleSway + windStr * _SwayAmount;
                float  lean    = windStr * _WindLean;
                float2 windOffset = wdir * ((lean + gust * _GustStrength) * swayMag);

                // ---- FOOTSTEP TRAIL ----
                // Tested at the flower's ROOT (not per vertex) so a flower gives as a whole. Take the STRONGEST
                // nearby footprint (max, not sum) so overlapping prints don't stack into a bulge. Bends only
                // BEHIND the heading each print was laid with while the player moves; symmetric when they stand.
                float  bestFp  = 0.0;
                float2 bestDir = float2(0.0, 1.0);
                [unroll]
                for (int ti = 0; ti < TRAIL_N; ti++)
                {
                    float2 to = root - _GrassTrail[ti].xy;        // footprint -> flower root
                    float  d  = length(to);
                    float  reach = (1.0 - smoothstep(0.0, max(_FootRadius, 1e-3), d)) * saturate(_GrassTrail[ti].z);

                    float2 fwd = float2(cos(_GrassTrail[ti].w), sin(_GrassTrail[ti].w));
                    float  ahead = dot(to, fwd);
                    float  behind = 1.0 - smoothstep(0.0, max(_FootDirSoftness, 1e-4), ahead);
                    float  gate = lerp(1.0, behind, saturate(_PlayerMoving));

                    float fp = reach * gate;
                    if (fp > bestFp)
                    {
                        bestFp  = fp;
                        bestDir = d > 1e-4 ? to / d : float2(0.0, 1.0);
                    }
                }
                float2 footOffset = bestDir * (bestFp * _FootStrength);

                // combine, weight by the bend shape, foreshorten in Y, and pixel-snap.
                float2 offset = (windOffset + footOffset) * bendW;
                float  yDip   = -length(offset) * _BendY;
                offset = PixelSnap(offset);
                wp.xy += offset;
                wp.y  += PixelSnap(float2(0.0, yDip)).y;

                // ---- DRAWN POSE SELECT ----
                // Step through the _Cols hand-drawn sway poses (measured: neutral, right, neutral, left — a sine
                // cycle). The rate rises with the wind; the phase is the same per flower value the bend uses, so
                // a flower's drawn pose and its wind lean agree instead of fighting. floor() of a 0..1 fraction
                // scaled by _Cols gives an integer 0.._Cols-1 that is IDENTICAL at all four vertices.
                float poseRate  = _PoseIdleSpeed + windStr * _PoseWindSpeed;
                float posePhase = _Time.y * poseRate - travel + phase;
                float k = floor(frac(posePhase / TAU) * max(_Cols, 1.0));
                // Cells are exactly 1/_Cols apart in UV, so adding k/_Cols and wrapping with frac lands on pose
                // (c + k) mod _Cols OF THE SAME ROW. uv.y is untouched, so the bloom stage never changes.
                OUT.uv = float2(frac(IN.uv.x + k / max(_Cols, 1.0)), IN.uv.y);

                OUT.positionCS = TransformWorldToHClip(wp);
                OUT.color = IN.color * _Color;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);
                half4 col = tex * IN.color;
                // discard near-transparent texels so the silhouette stays clean and sorts without a quad halo.
                clip(col.a - _AlphaClip);
                return col;
            }
            ENDHLSL
        }
    }

    Fallback Off
}
