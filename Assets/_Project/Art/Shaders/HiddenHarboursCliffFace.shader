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
// NO [unroll] over a runtime loop bound — the one loop here (CliffWaveHeight, added 2026 08 06 for the
// waterline) runs to the COMPILE TIME constant CLIFF_WAVE_MAX_TRAINS with the live train count masking
// inside it, which is the exact shape HiddenHarboursWater.shader's WaveFieldSample uses. The shipped
// CliffFace.mat variant is force compiled headless by
// Assets/Tests/EditMode/Art/CliffShaderCompileGuardTests.cs.
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

        [Header(The WATERLINE. The sea rises against the rock. Owner ask 2026 08 06)]
        // #439 stood the coast up and the sea knew nothing about it: a cliff plunging into deep water
        // was drawn dry rock all the way down, with the water plane passing behind it. Deep shore
        // cliffs are tide worked BY DESIGN, so the rock carries the mark: seen through water below the
        // line, damp at it, dry above it, and the line itself riding the tide and the waves.
        //
        // THE CLIFF DOES NOT GET ITS OWN SEA. The tide is not re read from the sim and the swell is not
        // re modelled. Both arrive on the one global WaterSurface publishes, _HHSeaLevelWorld, and the
        // surge is the SAME shared wave field the water shader samples, at the DRAWN sea's own
        // frequency scale. Two consumers, one sea, closed at the globals. Mirrored term for term by
        // Assets/_Project/Code/Art/CliffWaterlineMath.cs.
        //
        // Presentation only: colour on rock. It moves no walkability, no clip contour, no water level.
        // _WaterlineStrength 0 is an EXACT passthrough, and so is a scene with no water surface at all.
        _WaterlineStrength ("Waterline strength. 0 is off", Range(0, 1)) = 1
        _SubmergedTint ("Rock seen through water", Color) = (0.30, 0.46, 0.52, 1)
        _SubmergedDepth ("Depth over which the rock drowns, metres", Float) = 2.2
        _SubmergedFloor ("How much rock survives at full depth", Range(0, 1)) = 0.18
        _DampBandMetres ("Damp band half width, metres", Float) = 0.55
        _DampDarken ("How much the damp band darkens the rock", Range(0, 1)) = 0.45
        _FoamCollarMetres ("Foam collar half width, metres", Float) = 0.16
        _FoamCollarBias ("How far the collar sits into the water", Range(0, 1)) = 0.45
        _FoamColour ("Collar colour where sea meets rock", Color) = (0.92, 0.96, 0.98, 1)
        _SurgeGain ("How much of the swell the waterline rides", Range(0, 2)) = 1

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
                // x is this vertex's ELEVATION in metres (brow elevation minus the drop it has fallen).
                // y is 1 when that elevation is REAL and 0 when the wall has none authored, which is a
                // scene saved before the waterline existed. The flag is not decoration: elevation 0 with
                // no flag would read as metres under water at any flood tide and draw the whole cliff
                // drowned, so a missing elevation has to be able to say so rather than look like datum.
                float2 uv1        : TEXCOORD1;
                // The station's TOE position IN PLAN, world XY. This, not the drawn vertex position, is
                // where the sea actually is: the drawn toe has already been pushed down screen by the
                // drop, so sampling the wave field at it would read the swell at the wrong place.
                float2 uv2        : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 color      : COLOR;
                float2 uv         : TEXCOORD0;
                // x is the vertex ELEVATION, y is the DRAWN SEA's elevation at this stretch of wall
                // (tide plus surge), z is 1 when this wall has a real elevation to compare them with.
                // x and y are metres, and both are resolved in the vertex stage: the sea only varies
                // along the shore, so a per column value interpolated down the face is exact, and the
                // eight train evaluation is paid once per vertex instead of per pixel.
                float3 waterline  : TEXCOORD1;
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

            // THE PUBLISHED WATERLINE, set by WaterSurface.PublishSeaLevel (Shader.SetGlobalVector, so
            // OUTSIDE the per material CBUFFER like _SunDir).
            //   x  the DRAWN sea level in metres. The same eased number the water material is handed as
            //      _WaterLevel, never a second read of the tide.
            //   y  the frequency scale the DRAWN sea runs its wave field at. The water's vertex stage
            //      samples at _OceanSwellScale over its shipped default, and a consumer that assumed 1
            //      while the sea drew at 2.8 is this repo's most expensive art defect. Borrowed, never
            //      re declared.
            //   z  the displaced sea's exaggeration, for the same reason.
            //   w  1 when a water surface has published at all. ZERO is the unset state and MUST read as
            //      there is no sea here: an art scene or a stopped play session would otherwise draw
            //      every cliff drowned to its brow.
            float4 _HHSeaLevelWorld;

            // The ONE shared deterministic wave field, published by WaveFieldBridge. Same globals, same
            // packing and the same evaluation the water shader's WaveFieldSample uses, so the surge on
            // the rock is the swell on the sea. Count 0 (a bare scene, edit mode) means silence.
            float4 _WaveTrain0; float4 _WaveTrain1; float4 _WaveTrain2; float4 _WaveTrain3;
            float4 _WaveTrain4; float4 _WaveTrain5; float4 _WaveTrain6; float4 _WaveTrain7;
            float4 _WavePhases;        // trains 0 to 3
            float4 _WavePhases2;       // trains 4 to 7
            float4 _WaveFieldParams;   // x count, y crest sharpening, z total amplitude, w dominant slot

            #define CLIFF_WAVE_MAX_TRAINS 8

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float4 _SunColour;
                float4 _SkyColour;
                float4 _BounceColour;
                float4 _BakeL;
                float4 _SubmergedTint;
                float4 _FoamColour;
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
                float  _WaterlineStrength;
                float  _SubmergedDepth;
                float  _SubmergedFloor;
                float  _DampBandMetres;
                float  _DampDarken;
                float  _FoamCollarMetres;
                float  _FoamCollarBias;
                float  _SurgeGain;
            CBUFFER_END

            // The shared field's HEIGHT at a plan position, transcribed from the water shader's
            // WaveFieldSample: same trains, same published phases, same k times freqScale, same crest
            // pinch. Height only, because a waterline needs where the surface IS and not which way it
            // leans. The loop bound is the COMPILE TIME constant with the live count masking inside it,
            // which is the shape the water shader uses and the one that keeps unroll legal.
            // Twin: CliffWaterlineMath.SurgeMetres, which delegates to WaveFieldBridge.ShaderTwinSample.
            float CliffWaveHeight(float2 planXY, float freqScale)
            {
                float4 trains[CLIFF_WAVE_MAX_TRAINS] = { _WaveTrain0, _WaveTrain1, _WaveTrain2, _WaveTrain3,
                                                         _WaveTrain4, _WaveTrain5, _WaveTrain6, _WaveTrain7 };
                float phis[CLIFF_WAVE_MAX_TRAINS] = { _WavePhases.x,  _WavePhases.y,  _WavePhases.z,  _WavePhases.w,
                                                      _WavePhases2.x, _WavePhases2.y, _WavePhases2.z, _WavePhases2.w };
                int count = (int)(_WaveFieldParams.x + 0.5);
                float p = max(_WaveFieldParams.y, 1.0);
                float fs = max(freqScale, 1e-3);
                float height = 0.0;

                [unroll]
                for (int i = 0; i < CLIFF_WAVE_MAX_TRAINS; i++)   // FIXED bound; the count masks inside
                {
                    float amplitude = trains[i].w;
                    if (i < count && amplitude > 0.0)
                    {
                        float k = trains[i].z * fs;
                        float theta = k * dot(trains[i].xy, planXY) + phis[i];
                        float s = (sin(theta) + 1.0) * 0.5;
                        float shaped = pow(max(s, 1e-6), p);
                        height += amplitude * (2.0 * shaped - 1.0);
                    }
                }
                return height;
            }

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

                // Resolve the sea ONCE per vertex. It varies along the shore and not down the face, so
                // interpolating a per column value down the column is exact rather than approximate, and
                // the eight train evaluation is paid per vertex instead of per pixel.
                float surge = CliffWaveHeight(IN.uv2, _HHSeaLevelWorld.y);
                float seaElevation = _HHSeaLevelWorld.x
                                   + surge * max(_HHSeaLevelWorld.z, 0.0) * max(_SurgeGain, 0.0);
                OUT.waterline = float3(IN.uv1.x, seaElevation, IN.uv1.y);
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

                // ---- THE WATERLINE (owner ask 2026 08 06) --------------------------------------------
                // Gated THREE times over, and every gate is an exact passthrough: the material's own
                // strength; _HHSeaLevelWorld.w, which is 0 whenever no water surface has published; and
                // the mesh's own elevation flag, which is 0 on a wall built before the waterline
                // existed. An art scene, a rig fixture, a stopped play session or an un rebuilt region
                // therefore renders the wall exactly as it did before, instead of drowning it to the
                // brow off a number that only looks like chart datum.
                // Mirrored by CliffWaterlineMath: ElevationAt, SubmergenceMetres, Submerged01,
                // DampBand01, FoamCollar01, HasPublishedSea.
                if (_WaterlineStrength > 0.001 && _HHSeaLevelWorld.w > 0.5 && IN.waterline.z > 0.5)
                {
                    float submergence = IN.waterline.y - IN.waterline.x;   // metres under the sea
                    float strength = saturate(_WaterlineStrength);

                    // SEEN THROUGH WATER below the line, in two honest steps: the column ABSORBS, so
                    // the rock keeps its shape and takes the water's colour; and detail is then LOST
                    // into that colour with depth, floored by _SubmergedFloor so a plunging foot never
                    // flattens into a painted silhouette.
                    float drown = submergence > 0.0
                                ? saturate(submergence / max(_SubmergedDepth, 1e-3)) : 0.0;
                    float3 wet = lit * _SubmergedTint.rgb;
                    lit = lerp(lit, wet, drown * strength);
                    float lost = drown * (1.0 - saturate(_SubmergedFloor));
                    lit = lerp(lit, _SubmergedTint.rgb * 0.6, lost * strength);

                    // THE DAMP BAND: the strip the tide keeps wet, centred ON the line and reaching
                    // either side of it. Rock just above is wet from the last wave; rock just below is
                    // still being worked. Symmetric, smoothstepped, so neither edge is ruled.
                    float band = max(_DampBandMetres, 0.0);
                    if (band > 0.0)
                    {
                        float d = abs(submergence);
                        float t = saturate(1.0 - d / band);
                        float damp = t * t * (3.0 - 2.0 * t);
                        lit *= lerp(1.0, 1.0 - saturate(_DampDarken), damp * strength);
                    }

                    // THE FOAM COLLAR: the bright edge where sea meets rock. Tighter than the damp band
                    // and biased DOWN into the water, because foam gathers on the water side; the two
                    // together read as a wet strip with a lit edge instead of one fat glow.
                    float collar = max(_FoamCollarMetres, 0.0);
                    if (collar > 0.0)
                    {
                        float c = abs(submergence - collar * saturate(_FoamCollarBias));
                        float ct = saturate(1.0 - c / collar);
                        float foam = ct * ct * (3.0 - 2.0 * ct);
                        lit = lerp(lit, _FoamColour.rgb, foam * strength * _FoamColour.a);
                    }
                }

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
