// HiddenHarboursCliffFace.shader — a cliff face lit LIVE by the game's own sun (ADR 0013).
//
// A custom URP 2D ShaderLab/HLSL shader, authored as TEXT so it builds headless (no Shader Graph),
// mirroring HiddenHarboursWater / HiddenHarboursGrass. It renders the Cliff Face kit v10
// (docs/art/rigs/cliff-face-kit) on a wall quad: a periodic 12 by 9 m material addressed in SURFACE
// metres, tiled Repeat with Point filtering, and shaded from the deterministic 24 hour sun.
//
// WHY LIVE AND NOT PRE-LIT. The kit bakes two paths and forbids mixing them on one wall. The cheap
// path is a pre-lit albedo at the aspect's fixed key, which is legitimate for a face that never
// rotates relative to the camera. Hidden Harbours does not get to use it: the whole point of the
// day/night cycle is that light is a force the player reads, and a wall lit at a fixed key would sit
// visibly still while the sea, the sod and the boats around it moved. So this samples the three data
// channels instead:
//
//   _Unlit   albedo with NON directional cavity and macro AO only. No sun in it at all.
//   _Normal  tangent space. R along s, G up the face, B out of the face, A is cavity plus macro AO.
//   _Mask    R is the baked key light INCLUDING the form cast shadow, G is sky occlusion,
//            B is depth, A is coverage. Packed exactly like TreeRig2.packMask.
//
// mask.R is the one term a normal map cannot reproduce: a CAST shadow, thrown by the buttress ribs
// and the bench lips. It is baked at the aspect's own key, so it is un divided by that key's N dot L
// and re applied against the live sun, scaled by _CastStrength.
//
// HOW THIS JOINS THE PROJECT'S DAY AND NIGHT. ADR 0013 puts one global MULTIPLY overlay over the
// whole composited frame, and DayNightMath already folds the moonlight lift into that tint. So this
// shader does NOT tint for time of day and does NOT read the moon. It answers one question, how the
// sun strikes THIS wall right now, and the overlay darkens and moon lifts the answer along with
// everything else. The one thing it must never do is reach zero on its own: a face that goes black
// before the overlay has nothing left for the moonlight to lift, and a moonlit cliff would read as a
// hole in the world. _SkyFloor is what stops that, and CliffLightMathTests pins it.
//
// THE BATTER ROTATES THE FRAME. A tipped face is not a vertical face times a constant. L is resolved
// in the tipped basis, so a shaded east bank genuinely catches a high sun that a vertical east wall
// does not (the kit's rule 11, and its bedding respacing is already baked into the texture).
//
// The maths here is mirrored, term for term, by Assets/_Project/Code/Art/CliffLightMath.cs — the
// testable twin, because a compile guard proves a shader builds and never that it is right.
//
// SHADER CAUTIONS honoured (this project lost hours to a magenta shader): NO plus or other operator
// characters in ANY [Header(...)] label or property display string (ShaderLab parse error, magenta);
// NO [unroll] over a runtime loop bound (this shader has no loops). The shipped CliffFace.mat variant
// is force compiled headless by Assets/Tests/EditMode/Art/CliffShaderCompileGuardTests.cs.
Shader "HiddenHarbours/CliffFace"
{
    Properties
    {
        [Header(Kit channels. Import normal and mask with sRGB OFF)]
        [NoScaleOffset] _Unlit  ("Unlit albedo with AO", 2D) = "grey" {}
        [NoScaleOffset] _Normal ("Tangent normal. A is AO", 2D) = "bump" {}
        [NoScaleOffset] _Mask   ("Mask. R key light, G sky, B depth, A coverage", 2D) = "white" {}

        [Header(Wall placement. Set per sector by the builder)]
        // Compass azimuth of the wall's OUTWARD normal, N is 0 and it runs clockwise — the rig's own
        // convention (CliffCatalog.AspectAzimuths). The sector's snapped aspect chooses the TEXTURE;
        // this is the true unsnapped bearing, so lighting stays continuous along a curving coast.
        _WallAzimuth ("Outward normal azimuth (deg)", Float) = 180
        // 90 is a vertical wall, 48 a bank. Must match the batter the face texture was baked at, or
        // the bedding spacing in the pixels disagrees with the frame the light is resolved in.
        _Batter ("Batter angle from horizontal (deg)", Range(30, 90)) = 90
        // Surface metres the texture covers, for the world space tiling. 12 by 9 in kit v10, and it is
        // SURFACE not height: an 8 m bank at 48 deg needs 8 over sin(48) of t.
        _TileMetresS ("Tile metres along the shore", Float) = 12
        _TileMetresT ("Tile metres down the surface", Float) = 9

        [Header(Bake key. The tangent space light this aspect was baked at)]
        // Used ONLY to un divide the baked cast shadow in mask.R. Pushed per sector alongside the
        // face texture; the default is the kit's S aspect key.
        _BakeL ("Bake light in tangent space", Vector) = (-0.6, -0.55, 0.58, 0)

        [Header(Shade. Defaults mirror CliffLightMath and are pinned by test)]
        _SkyFloor ("Sky light at full occlusion", Range(0, 1)) = 0.34
        _Ambient ("Sky dome strength", Float) = 0.62
        _Bounce ("Ground bounce strength", Float) = 0.28
        _CastStrength ("Baked cast shadow borrow", Range(0, 1)) = 0.7
        _SunStrength ("Direct sun strength", Float) = 1.0

        [Header(Light colours)]
        _SunColour ("Sun colour", Color) = (1.0, 0.96, 0.88, 1)
        _SkyColour ("Sky dome colour", Color) = (0.72, 0.80, 0.95, 1)
        _BounceColour ("Ground bounce colour", Color) = (0.85, 0.74, 0.62, 1)

        [Header(Sprite common)]
        _Color ("Tint", Color) = (1, 1, 1, 1)
        _AlphaClip ("Alpha clip threshold", Range(0, 1)) = 0.01
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

            TEXTURE2D(_Unlit);  SAMPLER(sampler_Unlit);
            TEXTURE2D(_Normal); SAMPLER(sampler_Normal);
            TEXTURE2D(_Mask);   SAMPLER(sampler_Mask);

            // GLOBALS set by DayNightController (Shader.SetGlobal*), OUTSIDE the per material CBUFFER
            // exactly like _SunDir in the water shader — so an art scene with no day/night controller
            // reads them as zero and the fallback below keeps the wall lit rather than black.
            //   _SunDir.xy      ground plane direction TOWARD the sun. (0,0) means the cycle is not running.
            //   _SunElevation   SIN of the sun's elevation angle, in minus 1 to 1. 1 at zenith, 0 at the
            //                   horizon, negative at night (DayNightMath).
            float4 _SunDir;
            float  _SunElevation;

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float4 _SunColour;
                float4 _SkyColour;
                float4 _BounceColour;
                float4 _BakeL;
                float  _AlphaClip;
                float  _WallAzimuth;
                float  _Batter;
                float  _TileMetresS;
                float  _TileMetresT;
                float  _SkyFloor;
                float  _Ambient;
                float  _Bounce;
                float  _CastStrength;
                float  _SunStrength;
            CBUFFER_END

            // The wall's tangent basis from its plan azimuth and batter. World Z is up.
            // Mirrors CliffLightMath.WallBasis.
            void WallBasis(out float3 tangent, out float3 up, out float3 normalW)
            {
                float az = radians(_WallAzimuth);
                float b  = radians(_Batter);
                // Compass bearing to a ground vector: N (0 deg) is plus Y, E (90 deg) is plus X.
                float2 planN = float2(sin(az), cos(az));
                tangent = float3(planN.y, -planN.x, 0.0);
                // Tip the outward normal up out of the ground plane by 90 minus batter.
                normalW = float3(planN.x * sin(b), planN.y * sin(b), cos(b));
                // ORDER MATTERS. cross(normalW, tangent) gives a DOWNWARD up axis, which flips the
                // sign of every texel's t component: the ground bounce would light the sky facing
                // texels instead of the foreshore facing ones and the relief reads inside out under a
                // high sun. Invisible on a texel pointing straight out of the wall, so it is pinned by
                // CliffLightMathTests with a tilted one.
                up = cross(tangent, normalW);
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
                OUT.positionCS = TransformObjectToHClip(IN.positionOS);
                OUT.color = IN.color * _Color;
                OUT.uv = IN.uv;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);

                half4 unlit = SAMPLE_TEXTURE2D(_Unlit,  sampler_Unlit,  IN.uv);
                half4 nrm   = SAMPLE_TEXTURE2D(_Normal, sampler_Normal, IN.uv);
                half4 msk   = SAMPLE_TEXTURE2D(_Mask,   sampler_Mask,   IN.uv);

                float3 N  = normalize(nrm.xyz * 2.0 - 1.0);
                float  ao = saturate(nrm.w);

                float3 Ts, Up, Nw;
                WallBasis(Ts, Up, Nw);

                // The sun as a world vector. _SunElevation is SIN of the elevation angle, so its
                // horizontal companion is sqrt(1 minus e squared) — no trig, and exactly consistent
                // with DayNightMath's range.
                float  e     = clamp(_SunElevation, -1.0, 1.0);
                float  horiz = sqrt(saturate(1.0 - e * e));
                float2 gdir  = _SunDir.xy;
                float  glen  = length(gdir);

                // Cycle not running (_SunDir is zero, the project's established unset convention):
                // fall back to the aspect's own bake key so a fresh material or an art scene renders
                // the wall the way the rig drew it, instead of flat or black.
                bool   cycleOn = glen > 1e-4;
                float3 S = cycleOn ? float3(gdir / glen * horiz, e) : float3(0, 0, 1);
                float3 L = cycleOn
                         ? float3(dot(S, Ts), dot(S, Up), dot(S, Nw))
                         : normalize(_BakeL.xyz);
                float  day = cycleOn ? saturate(e) : 1.0;

                L = normalize(L);
                float ndl = saturate(dot(N, L));

                // Borrow the baked cast shadow. mask.R already holds N dot Lbake TIMES the cast
                // shadow, so dividing by the bake's own N dot L leaves the shadow by itself. Floored
                // so a texel facing away from the bake key cannot blow the ratio up.
                float bakeNdl = max(saturate(dot(N, normalize(_BakeL.xyz))), 0.02);
                float cast    = lerp(1.0, saturate(msk.r / bakeNdl), _CastStrength);

                // Sky dome. NEVER reaches zero — see the header note; this is the floor the whole
                // multiply chain stands on, and it is what leaves a night wall something for the
                // day/night overlay's moonlight lift to act on.
                float sky = (_SkyFloor + ao * (1.0 - _SkyFloor)) * _Ambient;

                // Ground bounce lights texels facing DOWN the face, and fades as the wall tips back
                // and stops looking at the foreshore.
                float bnc = saturate(-N.y * 0.85 + 0.15) * (0.35 + ao * 0.5) * _Bounce;

                float sun = ndl * cast * day * _SunStrength;

                float3 lit = unlit.rgb * (sky * _SkyColour.rgb
                                        + bnc * _BounceColour.rgb
                                        + sun * _SunColour.rgb);

                // Coverage rides in mask.A — full on a face, meaningful on the brow and toe decals.
                half4 col = half4(lit, unlit.a * msk.a) * IN.color;
                clip(col.a - _AlphaClip);
                return col;
            }
            ENDHLSL
        }
    }

    Fallback Off
}
