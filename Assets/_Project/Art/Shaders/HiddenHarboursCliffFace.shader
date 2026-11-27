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
//
// THE PX LOOK (keyword _HH_CLIFF_PX; OFF in CliffFace.mat as shipped, ON after Hidden Harbours, Dev,
// Bake Cliff Kit px). The px cliff kit (docs/art/rigs/px-cliff-face-kit) is not lit by multiplying a
// colour. It STEPS THE INDEX the texel was cut from and looks the result up in the rock's own palette,
// so every colour on screen is one a pixel artist put in the ramp: the kit's
// shaders/relight_palette.glsl, ported in CliffPxRelight below.
//   The px bake puts the kit's _index channel in the _Unlit slot (R the palette row, G the band 0 to 4,
//   B the tier shiftable flag, A coverage), so no wall is rebuilt to change look. The live sun moves a
//   texel ONE band up or down; a cast shadow moves a rock texel one or two TIERS down.
//   The kit reads that shadow from an engine shadow map this project does not have for a wall, so it
//   is RECOVERED: the px rig bakes mask.R as N dot L times (1 minus 0.78 times the shadow) at its own
//   key, and dividing that N dot L back out leaves the shadow. The divide is guarded near zero, where
//   the mask carries no shadow at all. That key is _PxKey, tipped by the batter and turned into the
//   frame the normals are packed in (y UP the face, where the rig's key runs y down). It is not
//   _BakeL, which is the v10 kit's key and stays the v10 path's.
//   The kit's _AspectShift is 0 here, as its own ASPECT note asks when L is built per wall.
//   SAMPLES: three textures (index, normal, mask) and one LUT tap, where the kit's glsl reads two and
//   one, because the shadow comes from the mask instead of a shadow map.
//   A brow or toe decal is the px rig's own pre lit strip. The wall flags it (_PxDecal) and it is
//   drawn as baked.
//   The night stays the overlay's, exactly as for v10: this path reads ONE daylight table, and with
//   the sun down there is no direct light, which in a palette is one band down everywhere.
//   Mirrored term for term by Assets/_Project/Art/Editor/CliffPxRelightMath.cs.
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

        [Header(Px look. Read only when the px keyword is on)]
        // The px kit's palette, 8 by 32: the row is the rock and its tier, the column the band. Set on
        // the MATERIAL by the px bake (CliffBakeMenu.SetLook), never per wall.
        [NoScaleOffset] _Palette ("Px palette LUT 8 by 32", 2D) = "black" {}
        // The px rig's key light UNTIPPED, PxLang.LIGHT.key. The shader tips it by _Batter the way the
        // rig's setSlope does before it bakes, so one number serves every batter.
        _PxKey ("Px rig key light, untipped", Vector) = (-0.5477, -0.6572, 0.5178, 0)
        // 1 on a brow or toe decal, pushed per renderer by CliffWallSurface.
        [HideInInspector] _PxDecal ("Px decal flag", Float) = 0
        // THE CLIFF MIX (owner 11 25): a second palette the same faces are drawn in on the stretches the
        // region's CliffFaceMixDef picks. Pushed per renderer by CliffWallSurface, never on the material.
        // uv3.x is the stretch's weight; _VariantBlock is the block the change gives way in, in texels.
        [HideInInspector][NoScaleOffset] _PaletteVariant ("Px variant palette LUT 8 by 32", 2D) = "black" {}
        [HideInInspector] _VariantOn ("Px variant palette on", Float) = 0
        [HideInInspector] _VariantBlock ("Px variant block, texels", Vector) = (6, 3, 0, 0)
        // The variant skin's own FACES (a CliffFaceStyleDef: the boards' coursed rock), where the band's kit
        // face has one. Same rows and bands as the kit face, so they draw through either palette.
        [HideInInspector] _VariantFaceOn ("Px variant face on", Float) = 0
        // Ground texels one BASE face texel covers a side: 2 for a face painted in 2 px texels that a mix
        // wants drawn on the ground's grid, 1 as mapped. Pushed per renderer by CliffWallSurface.
        [HideInInspector] _BaseTexelScale ("Base face texel scale", Float) = 1
        // THE FAULTED BEDDING (owner 11 26: rough and jagged, no mixels), on a face laid on the screen grid.
        // The region's CliffFaceMixDef numbers, pushed per renderer by CliffWallSurface; _FaultB.w 0 = off.
        // A: block width, tier height, throw, slide (ground texels). B: crack run, crack share, facet degrees, on.
        [HideInInspector] _FaultA ("Fault: block, tier, throw, slide", Vector) = (0, 0, 0, 0)
        [HideInInspector] _FaultB ("Fault: crack run, crack share, facet deg, on", Vector) = (4, 0, 0, 0)
        // C: on the BASE skin's face (broad beds, flat inside a block), the block's size and its throw are scaled.
        // Height on a wall laid on the screen grid (CliffHeight); _JointB.y and _FaceRamp.w 0 = off.
        [HideInInspector] _JointA ("Joints: spacing, run, run share, step share", Vector) = (0, 24, 0, 0)
        [HideInInspector] _JointB ("Joints: lit edge, on", Vector) = (0, 0, 0, 0)
        [HideInInspector] _FaceRamp ("Face ramp: top share, foot share, jitter, on", Vector) = (0, 0, 0, 0)
        [HideInInspector] _FaultC ("Fault: variant face block scale, variant face throw scale", Vector) = (1, 1, 0, 0)
        [HideInInspector][NoScaleOffset] _UnlitVariant ("Px variant face index", 2D) = "black" {}
        [HideInInspector][NoScaleOffset] _NormalVariant ("Px variant face normal", 2D) = "bump" {}
        [HideInInspector][NoScaleOffset] _MaskVariant ("Px variant face mask", 2D) = "white" {}

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
        // The wet band's mean height above the line. Its top is torn per column, 0.55 to 1.45 of this.
        _DampBandMetres ("Wet band height above the line, metres", Float) = 0.55
        _DampDarken ("How much the wet band darkens the rock", Range(0, 1)) = 0.45
        _FoamCollarMetres ("Foam collar half width, metres", Float) = 0.16
        _FoamCollarBias ("How far the collar sits into the water", Range(0, 1)) = 0.45
        _FoamColour ("Collar colour where sea meets rock", Color) = (0.92, 0.96, 0.98, 1)
        _SurgeGain ("How much of the swell the waterline rides", Range(0, 2)) = 1

        [Header(The WET FOOT. All on the kit texel grid. Owner 11 23)]
        // The foot dissolves into the sea over this depth in four dithered steps. 0 keeps it opaque.
        _WetFadeMetres ("Depth the foot dissolves over, metres", Float) = 0.5
        // Rockweed at the line: its mean height, how many cells it covers, and the tint it puts on the rock.
        _WeedMetres ("Weed fringe height, metres", Float) = 0.22
        _WeedDensity ("Weed fringe cover", Range(0, 1)) = 0.6
        _WeedTint ("Weed tint on the lit rock", Color) = (0.42, 0.56, 0.34, 1)
        // Barnacle flecks just above the weed: the strip's height, cover, and how far they lift to pale grey.
        _BarnacleMetres ("Barnacle strip height, metres", Float) = 0.3
        _BarnacleDensity ("Barnacle cover", Range(0, 1)) = 0.12
        _BarnacleLift ("Barnacle lift to pale grey", Float) = 1.45
        // How broken the foam collar is. 0 is a solid stroke, 1 is flecks.
        _FoamBreak ("Foam collar break up", Range(0, 1)) = 0.85

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
            // The px look (header). A FEATURE keyword: the material says which look it ships, and a
            // player build keeps only the variant a material uses.
            #pragma shader_feature_local _ _HH_CLIFF_PX

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
                // x is the cliff mix's weight at the station's toe (0 the base palette, 1 the variant),
                // y is 1 when the wall was built with a mix. Absent (zero) on a wall with none.
                float2 uv3        : TEXCOORD3;
                // x is the band's own parameter (0 at its top row, 1 at its foot; the mesh is laid a little
                // past both), y is 1 when the band's edges are cut on the ground's cells. Absent (zero) on a
                // wall that is not laid on the screen grid, and then nothing is cut.
                // z is the chunk's own parameter, 0 at its first station and 1 at its last.
                // w is the WHOLE face's depth, 0 at the brow and 1 at the foot (the ramp, CliffHeightMath).
                float4 uv4        : TEXCOORD4;
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
                // The cliff mix's weight and flag, straight from uv3.
                float2 mix        : TEXCOORD2;
                // The fragment's place in the world, metres: the ground's texel grid is cut on it.
                float2 world      : TEXCOORD3;
                // The band's parameter and its flag, straight from uv4.
                float4 band       : TEXCOORD4;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            TEXTURE2D(_Unlit);  SAMPLER(sampler_Unlit);
            TEXTURE2D(_Normal); SAMPLER(sampler_Normal);
            TEXTURE2D(_Mask);   SAMPLER(sampler_Mask);
            TEXTURE2D(_Palette); SAMPLER(sampler_Palette);   // the px look only
            TEXTURE2D(_PaletteVariant); SAMPLER(sampler_PaletteVariant);   // the px look's cliff mix only
            TEXTURE2D(_UnlitVariant);  SAMPLER(sampler_UnlitVariant);      // the cliff mix's variant face
            TEXTURE2D(_NormalVariant); SAMPLER(sampler_NormalVariant);
            TEXTURE2D(_MaskVariant);   SAMPLER(sampler_MaskVariant);

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
                float4 _WeedTint;
                float  _WetFadeMetres;
                float  _WeedMetres;
                float  _WeedDensity;
                float  _BarnacleMetres;
                float  _BarnacleDensity;
                float  _BarnacleLift;
                float  _FoamBreak;
                // The px look's two, declared in BOTH variants so the per material buffer has one
                // layout whichever look the material ships.
                float4 _PxKey;
                float  _PxDecal;
                // The cliff mix's two (see the property note), in both variants for the same reason.
                float4 _VariantBlock;
                float  _VariantOn;
                float  _VariantFaceOn;
                // How many of the ground's texels one texel of the BASE face covers a side (see the grid note).
                float  _BaseTexelScale;
                // The faulted bedding's two (see the property note), in both variants for the same reason.
                float4 _FaultA;
                float4 _FaultB;
                float4 _FaultC;
                float4 _JointA;
                float4 _JointB;
                float4 _FaceRamp;
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
            void WallBasis(float azimuthDegrees, out float3 tangent, out float3 up, out float3 normalW)
            {
                float az = radians(azimuthDegrees);
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

            // ---- THE PX LOOK (see the header) ---------------------------------------------------------
            // The kit's LIGHTING.thresholds verbatim, and the depth the rig cuts its cast shadow into
            // mask.R with. Mirrored by CliffPxRelightMath and pinned equal to it by CliffWaterlineTests.
            #define CLIFF_PX_BAND_UP      0.74
            #define CLIFF_PX_BAND_DOWN    0.40
            #define CLIFF_PX_SHADOW_ONE   0.45
            #define CLIFF_PX_SHADOW_TWO   0.85
            #define CLIFF_PX_SHADOW_DEPTH 0.78
            #define CLIFF_PX_SHADOW_GUARD 0.02
            #define CLIFF_PX_TIERS        6.0
            #define CLIFF_PX_BANDS        5.0
            #define CLIFF_PX_LUT_W        8.0
            #define CLIFF_PX_LUT_H        32.0

            // The px rig's key at this wall's batter, in the frame _Normal is packed in. The rig tips its
            // key by the slope before it bakes (PxCliffFace.keyFor after setSlope, clamped 30 to 90), and
            // its key runs y DOWN the face where the normal it packs runs y UP, hence the sign on y.
            // Twin: CliffPxRelightMath.PackedFrame(CliffPxRelightMath.BakeKeyAt(key, batter)).
            float3 CliffPxBakeKey()
            {
                float3 k  = normalize(_PxKey.xyz);
                float  th = radians(90.0 - clamp(_Batter, 30.0, 90.0));
                float  ct = cos(th);
                float  st = sin(th);
                return float3(k.x, -(k.y * ct + k.z * st), k.z * ct - k.y * st);
            }

            // The palette relight on one texel: the kit's relight_palette.glsl with its shadow map
            // replaced by the RECOVERED shadow. idx is the _index texel, ndl the live N dot L, bakeNdl the
            // same normal against CliffPxBakeKey, maskR the baked key light with its cast shadow.
            // Twin: CliffPxRelightMath.RecoverShadow, then CliffPxRelightMath.Relight, then LutUv.
            float3 CliffPxRelightShift(float4 idx, float ndl, float bakeNdl, float maskR, bool variant, float shift)
            {
                float row  = floor(idx.r * 255.0 + 0.5);   // 0 to 24: the rock and its tier
                float band = floor(idx.g * 255.0 + 0.5);   // 0 to 4
                bool  rock = idx.b > 0.5;                  // accessory rows never take a tier step

                // The detail light: ONE band step, never more. The law that keeps it pixel art.
                if (ndl > CLIFF_PX_BAND_UP) band += 1.0;
                else if (ndl < CLIFF_PX_BAND_DOWN) band -= 1.0;
                // The face ramp's step (CliffHeightMath.RampShift), 0 where the wall has none.
                band += shift;

                // Cast shadow moves the TIER, and only on the rock rows: lichen does not go the colour
                // of shadowed rock. The divide is guarded where the bake's own N dot L is near zero,
                // because there the rig wrote mask.R 0 whatever the shadow was.
                if (rock)
                {
                    float shadow = bakeNdl > CLIFF_PX_SHADOW_GUARD
                                 ? saturate((1.0 - saturate(maskR / bakeNdl)) / CLIFF_PX_SHADOW_DEPTH)
                                 : 0.0;
                    float tier = fmod(row, CLIFF_PX_TIERS);
                    float rockRow = row - tier;            // 0 sandstone, 6 till, 12 basalt
                    tier -= shadow > CLIFF_PX_SHADOW_TWO ? 2.0 : (shadow > CLIFF_PX_SHADOW_ONE ? 1.0 : 0.0);
                    row = rockRow + clamp(tier, 0.0, CLIFF_PX_TIERS - 1.0);
                }

                band = clamp(band, 0.0, CLIFF_PX_BANDS - 1.0);
                // v counts DOWN from the top: Unity's v 0 is the BOTTOM of the PNG, whose row 0 is its
                // top. Read it the glsl's way round and sandstone samples the empty padding rows.
                float2 lutUv = float2((band + 0.5) / CLIFF_PX_LUT_W, 1.0 - (row + 0.5) / CLIFF_PX_LUT_H);
                // The cliff mix draws the SAME row and band from its second palette: the beds, joints and
                // shadows stay the face's own, only the colours they name change.
                return variant ? SAMPLE_TEXTURE2D_LOD(_PaletteVariant, sampler_PaletteVariant, lutUv, 0).rgb
                               : SAMPLE_TEXTURE2D_LOD(_Palette, sampler_Palette, lutUv, 0).rgb;
            }

            float3 CliffPxRelight(float4 idx, float ndl, float bakeNdl, float maskR, bool variant)
            {
                return CliffPxRelightShift(idx, ndl, bakeNdl, maskR, variant, 0.0);
            }

            // ---- THE TEXEL GRID (owner 11 23: organic, natural, no hard cuts) ---------------------------
            // The wet foot and the pixel dissolve are cut on the GROUND's texel grid, 32 texels a metre, so
            // every edge they add is a pixel step the size of the ground's and never a gradient.
            //
            // THE GROUND'S GRID (owner 11 26: no mixels). A face is a sheared, foreshortened sheet, so its own
            // texels land on screen as slanted cells of every size. The fragment stage therefore shades the
            // GROUND's cell it falls in, once: every input (uv, elevation, mix weight, vertex alpha) is carried
            // along the triangle's own plane to the centre of the 1/32 m world cell, and the whole cell takes
            // that one answer. A texel on the face is then a square the size of the meadow's, a shear moves
            // whole cells, and nothing is stretched. _BaseTexelScale 2 draws a face painted in 2 px texels one
            // of ITS texels to a ground texel (the art keeps its pixel count and halves in metres); 1, the
            // default, draws a face as it is mapped.
            // Twin: CliffWaterlineMath.TexelsPerMetre, TexelCell, Hash01, Bayer4.
            #define CLIFF_TEXELS_PER_METRE 32.0
            #define CLIFF_WET_TOP_GROUP    6.0

            // A stable 0 to 1 hash of a cell, with no sine (a sine hash drifts between GPUs).
            float CliffHash(float2 cell, float salt)
            {
                float x = frac((cell.x + salt * 17.0) * 0.1031);
                float y = frac((cell.y + salt * 31.0) * 0.1030);
                float z = frac((cell.x + salt * 17.0) * 0.0973);
                float d = x * (y + 33.33) + y * (z + 33.33) + z * (x + 33.33);
                x += d; y += d; z += d;
                return frac((x + y) * z);
            }

            // ---- HEIGHT (owner 11 27: the walls lost their height; keep the new look) -------------------
            // Upright joints and a top light, foot dark ramp on a wall laid on the screen grid, both on the
            // ground's cells. wc is the world cell (texels of world x and y). Twin: CliffHeightMath.
            float CliffJointColumn(float zone, float run)
            {
                if (CliffHash(float2(zone, run), 41.0) >= _JointA.z) return -1e9;
                float place = 1.0 + floor(CliffHash(float2(zone, 0.0), 44.0) * (_JointA.x - 2.0));
                float step  = CliffHash(float2(zone, run), 42.0) < _JointA.w
                            ? (CliffHash(float2(zone, run), 43.0) < 0.5 ? -1.0 : 1.0) : 0.0;
                return zone * _JointA.x + place + step;
            }

            // 0 none, 1 a crack, 2 the lit edge right of one. Twin: CliffHeightMath.JointAt.
            float CliffJoint(float2 wc)
            {
                float s    = max(_JointA.x, 3.0);
                float runT = max(_JointA.y, 1.0);
                float zone = floor(wc.x / s);
                for (int dz = 0; dz >= -1; dz--)
                {
                    float z   = zone + dz;
                    float run = floor((wc.y + floor(CliffHash(float2(z, 1.0), 45.0) * runT)) / runT);
                    float jx  = CliffJointColumn(z, run);
                    if (wc.x == jx) return 1.0;
                    if (_JointB.x > 0.5 && wc.x == jx + 1.0) return 2.0;
                }
                return 0.0;
            }

            // The band step at whole face depth t (0 brow, 1 foot) in cell column x. Twin: CliffHeightMath.RampShift.
            float CliffRampShift(float t, float x)
            {
                float wander = (CliffHash(float2(x, 2.0), 46.0) - 0.5) * 2.0 * _FaceRamp.z;
                if (_FaceRamp.x > 0.0 && t < _FaceRamp.x + wander) return 1.0;
                if (_FaceRamp.y > 0.0 && t > 1.0 - _FaceRamp.y + wander) return -1.0;
                return 0.0;
            }

            // The 4 by 4 ordered dither threshold of a cell.
            static const float CLIFF_BAYER[16] = { 0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5 };
            float CliffBayer4(float2 cell)
            {
                float2 m = cell - 4.0 * floor(cell / 4.0);
                int i = clamp((int)m.y * 4 + (int)m.x, 0, 15);
                return (CLIFF_BAYER[i] + 0.5) / 16.0;
            }

            // A soft edge's alpha as a pixel dissolve: the ordered dither offset per 2 by 2 block by a
            // hash and wrapped, so the covered share stays the alpha but the edge crumbles irregularly.
            // Twin: CliffWaterlineMath.Dissolve01.
            float CliffDissolve(float alpha, float2 cell)
            {
                if (alpha >= 0.999) return 1.0;
                if (alpha <= 0.0) return 0.0;
                float th = frac(CliffBayer4(cell) + 0.5 * CliffHash(floor(cell / 2.0), 6.0));
                return alpha > th ? 1.0 : 0.0;
            }

            // ---- THE FAULTED BEDDING (owner 11 26: rough and jagged, no mixels) -------------------------
            // A face laid on the screen grid is cut into blocks on the ground's texel grid; each block's rock
            // is thrown and slid by WHOLE texels and its edges carry a stair-stepped crack. Nothing rotates,
            // shears or moves by part of a texel. The block a WORLD cell is in: x the column piece, y the tier.
            // Twin: CliffFaultMath.BlockOf (and Stepped, At, FacetAzimuth for the fragment lines that use it).
            float2 CliffFaultBlock(float2 c, float2 size)
            {
                float bw   = max(size.x, 2.0);
                float th   = max(size.y, 2.0);
                float pair = bw * 4.0;
                float k    = floor(c.x / pair);
                float split = floor((k + 0.25 + 0.5 * CliffHash(float2(k, 0.0), 20.0)) * pair);
                bool  right = c.x >= split;
                float m  = 2.0 * k + (right ? 1.0 : 0.0);
                float lo = right ? split : k * pair;
                float hi = right ? (k + 1.0) * pair : split;

                float tpair  = th * 2.0;
                float yk     = c.y + floor(CliffHash(float2(m, 0.0), 21.0) * tpair);
                float tk     = floor(yk / tpair);
                float tsplit = floor((tk + 0.25 + 0.5 * CliffHash(float2(tk, m), 22.0)) * tpair);
                float tier   = 2.0 * tk + (yk >= tsplit ? 1.0 : 0.0);

                float cut = floor(lo + (0.3 + 0.4 * CliffHash(float2(m, tier), 23.0)) * (hi - lo));
                return float2(m * 2.0 + (c.x >= cut ? 1.0 : 0.0), tier);
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
                OUT.positionCS = TransformObjectToHClip(IN.positionOS);
                OUT.color = IN.color * _Color;
                OUT.uv = IN.uv;
                OUT.mix = IN.uv3;
                OUT.band = IN.uv4;
                OUT.world = TransformObjectToWorld(IN.positionOS).xy;

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

                // THE GROUND'S GRID (see the note above). s is the screen step from this fragment to the centre
                // of its world cell; the ortho camera makes world a plane of the screen, so the solve is exact.
                float2 gridW  = IN.world;
                float2 gridD  = (floor(gridW * CLIFF_TEXELS_PER_METRE) + 0.5) / CLIFF_TEXELS_PER_METRE - gridW;
                float2 gridWx = ddx(gridW);
                float2 gridWy = ddy(gridW);
                float  gridDet = gridWx.x * gridWy.y - gridWx.y * gridWy.x;
                float2 gridS  = abs(gridDet) > 1e-12
                              ? float2(gridD.x * gridWy.y - gridD.y * gridWy.x, gridWx.x * gridD.y - gridWx.y * gridD.x) / gridDet
                              : float2(0.0, 0.0);
                float2 cellUv        = IN.uv + ddx(IN.uv) * gridS.x + ddy(IN.uv) * gridS.y;
                float3 cellWaterline = IN.waterline + ddx(IN.waterline) * gridS.x + ddy(IN.waterline) * gridS.y;
                float2 cellMix       = IN.mix + ddx(IN.mix) * gridS.x + ddy(IN.mix) * gridS.y;
                float  cellAlpha     = saturate(IN.color.a + ddx(IN.color.a) * gridS.x + ddy(IN.color.a) * gridS.y);
                // The whole face's depth at the cell's centre, for the ramp (0 where the mesh carries none).
                float  cellFaceT     = IN.band.w + ddx(IN.band.w) * gridS.x + ddy(IN.band.w) * gridS.y;

                // A BAND'S EDGES, CUT ON THE CELLS. A wall on the screen grid lays each band a little past its
                // top and foot; a cell is drawn only when the band's parameter at the cell's CENTRE is inside
                // the band, so a cell is wholly one band or wholly the next and no edge is a smooth diagonal.
                // Twin: CliffWallGeometry.CellIsInBand (0.05 is its BandEdgeCellShare).
                // The chunk's two END edges are cut the same way, by its own parameter (z: 0 at its first
                // station, 1 at its last), so two chunks that wear different rock meet on cells too.
                if (IN.band.y > 0.5)
                {
                    float2 bandDx   = float2(ddx(IN.band.x), ddy(IN.band.x));
                    float2 endDx    = float2(ddx(IN.band.z), ddy(IN.band.z));
                    float  cellBand = IN.band.x + bandDx.x * gridS.x + bandDx.y * gridS.y;
                    float  cellEnd  = IN.band.z + endDx.x * gridS.x + endDx.y * gridS.y;
                    float  perPixel = max(length(gridWx) * CLIFF_TEXELS_PER_METRE, 1e-6);
                    float  slack    = 0.05 * length(bandDx) / perPixel;
                    float  endSlack = 0.05 * length(endDx) / perPixel;
                    if (cellBand < -slack || cellBand >= 1.0 + slack) discard;
                    if (cellEnd < -endSlack || cellEnd >= 1.0 + endSlack) discard;
                }

                // THE FAULTED BEDDING (see CliffFaultBlock). faceUv is the rock this cell draws: its own, moved
                // by its block's whole texels. cellUv stays the cell's own, for the mix, the wet foot and the
                // dissolve, which belong to the wall and not to a block of it.
                // THE TEXEL CELL this fragment falls in: texels of the tile along the shore, and texels of
                // ELEVATION down the face where the wall has one (texels of the tile where it does not).
                // Twin: CliffWaterlineMath.TexelCell.
                float2 cell = float2(floor(cellUv.x * _TileMetresS * CLIFF_TEXELS_PER_METRE),
                                     cellWaterline.z > 0.5
                                         ? floor(cellWaterline.x * CLIFF_TEXELS_PER_METRE)
                                         : floor(cellUv.y * _TileMetresT * CLIFF_TEXELS_PER_METRE));
                // THE CLIFF MIX: whether this texel draws in the variant skin. Whole blocks of the rock's own
                // texels give way where the weight is between 0 and 1, so a change of skin is a crumbling edge
                // of pixel blocks, never a fade. Twin: CliffFaceMixMath.PicksVariant. Found here, outside the
                // look branch, because the fault's blocks go by the skin (_VariantOn is 0 off the px look).
                float  mixW    = cellMix.x;
                float2 mixBlk  = max(_VariantBlock.xy, float2(1.0, 1.0));
                bool   variant = _VariantOn > 0.5 && cellMix.y > 0.5
                              && (mixW >= 0.999 || (mixW > 0.0 && mixW > CliffHash(floor(cell / mixBlk), 8.0)));
                bool   variantFace = variant && _VariantFaceOn > 0.5;

                float2 faceUv     = cellUv;
                float  faultAz    = _WallAzimuth;
                bool   faultCrack = false;
                if (_FaultB.w > 0.5 && _PxDecal < 0.5)
                {
                    // The variant skin's own face is drawn at the tile's texel, so its courses are taller than the
                    // base skin's beds: its blocks and their throw are scaled, or a throw that breaks the base
                    // skin leaves it in long level slabs. Twin: CliffFault.OnVariantSkin.
                    float2 fsize  = variantFace ? max(floor(_FaultA.xy * _FaultC.x), float2(2.0, 2.0)) : _FaultA.xy;
                    float  fthrow = variantFace ? round(_FaultA.z * _FaultC.y) : _FaultA.z;
                    float2 wc    = floor(gridW * CLIFF_TEXELS_PER_METRE);
                    float  frun  = floor(wc.y / max(_FaultB.x, 1.0));
                    float  fzone = floor(wc.x / (max(fsize.x, 2.0) * 4.0));
                    float2 fs    = float2(wc.x + floor(CliffHash(float2(fzone, frun), 25.0) * 3.0) - 1.0, wc.y);
                    float2 fblk  = CliffFaultBlock(fs, fsize);
                    float2 fleft = CliffFaultBlock(fs - float2(1.0, 0.0), fsize);
                    // A crack is carried run by run of rows, not block by block: it breaks off and picks up again.
                    faultCrack   = fleft.x != fblk.x
                                && CliffHash(float2(fblk.x + frun, fblk.y - frun), 26.0) < _FaultB.y;
                    float2 fmove = float2(floor(CliffHash(fblk, 27.0) * (_FaultA.w + 1.0)),
                                          round((CliffHash(fblk, 28.0) * 2.0 - 1.0) * fthrow));
                    faceUv = cellUv + fmove / (float2(_TileMetresS, _TileMetresT) * CLIFF_TEXELS_PER_METRE);
                    if (_FaultB.z > 0.0)
                    {
                        float fa = _WallAzimuth / _FaultB.z;
                        float fw = floor(fa);
                        faultAz  = _FaultB.z * (fw + (CliffHash(fblk, 29.0) < fa - fw ? 1.0 : 0.0));
                    }
                }
                float2 baseUv        = _PxDecal > 0.5 ? cellUv : faceUv * max(_BaseTexelScale, 1.0);

                // HEIGHT (see CliffJoint, CliffRampShift): only on a face band laid on the screen grid.
                float2 heightCell = floor(gridW * CLIFF_TEXELS_PER_METRE);
                float  joint      = (_JointB.y > 0.5 && _PxDecal < 0.5) ? CliffJoint(heightCell) : 0.0;
                float  rampShift  = (_FaceRamp.w > 0.5 && _PxDecal < 0.5 && IN.band.y > 0.5)
                                  ? CliffRampShift(cellFaceT, heightCell.x) : 0.0;

                half4 unlit = SAMPLE_TEXTURE2D_LOD(_Unlit,  sampler_Unlit,  baseUv, 0);
                half4 nrm   = SAMPLE_TEXTURE2D_LOD(_Normal, sampler_Normal, baseUv, 0);
                half4 msk   = SAMPLE_TEXTURE2D_LOD(_Mask,   sampler_Mask,   baseUv, 0);

                float3 N  = normalize(nrm.xyz * 2.0 - 1.0);
                float  ao = saturate(nrm.w);

                float3 Ts, Up, Nw;
                WallBasis(faultAz, Ts, Up, Nw);

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

            #if defined(_HH_CLIFF_PX)
                // The cell and the mix's pick were found above, before the fault (its blocks go by the skin).
                // The variant skin's own FACE, where it has one: the same texel of the boards' coursed rock,
                // and its normal and mask with it (so N and the AO are re-read too). Point-sampled with no mips
                // (the kit's import law), so LOD 0 is the texel the plain sample takes.
                if (variant && _VariantFaceOn > 0.5)
                {
                    unlit = SAMPLE_TEXTURE2D_LOD(_UnlitVariant,  sampler_UnlitVariant,  faceUv, 0);
                    nrm   = SAMPLE_TEXTURE2D_LOD(_NormalVariant, sampler_NormalVariant, faceUv, 0);
                    msk   = SAMPLE_TEXTURE2D_LOD(_MaskVariant,   sampler_MaskVariant,   faceUv, 0);
                    N     = normalize(nrm.xyz * 2.0 - 1.0);
                    ao    = saturate(nrm.w);
                }

                // THE PX LOOK (header). The same live sun in the same frame. With the cycle off it falls
                // back to the px rig's own key, which draws the face the way the rig baked it; with the
                // sun down there is no direct light, the px twin of the v10 day term below.
                float3 Lpx = CliffPxBakeKey();
                float3 L   = cycleOn ? normalize(float3(dot(S, Ts), dot(S, Up), dot(S, Nw))) : Lpx;
                float  ndl = (cycleOn && e <= 0.0) ? 0.0 : dot(N, L);

                // THE CLIFF MIX: `variant` (decided above, with the face) picks the palette too.
                float3 lit = unlit.rgb;   // a decal: the px rig's own pre lit strip, drawn as baked
                // A fault crack is the same texel in full cast shadow and turned from the light: two tiers and
                // one band down, colours the palette already holds.
                if (_PxDecal < 0.5)
                    lit = CliffPxRelightShift(unlit, ndl, dot(N, Lpx), msk.r, variant, rampShift);
                // An upright joint is drawn as a crack is; its lit edge is the same texel turned to the light.
                if (faultCrack || joint > 0.5 && joint < 1.5)
                    lit = CliffPxRelight(unlit, -1.0, 1.0, 0.0, variant);
                else if (joint > 1.5)
                    lit = CliffPxRelightShift(unlit, 1.0, 1.0, 1.0, variant, rampShift);
            #else
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

                float sun = faultCrack ? 0.0 : ndl * cast * day * _SunStrength;   // a fault crack takes no sun

                float3 lit = unlit.rgb * (sky * _SkyColour.rgb
                                        + bnc * _BounceColour.rgb
                                        + sun * _SunColour.rgb);
            #endif

                // ---- THE WATERLINE (owner ask 2026 08 06) --------------------------------------------
                // Gated THREE times over, and every gate is an exact passthrough: the material's own
                // strength; _HHSeaLevelWorld.w, which is 0 whenever no water surface has published; and
                // the mesh's own elevation flag, which is 0 on a wall built before the waterline
                // existed. An art scene, a rig fixture, a stopped play session or an un rebuilt region
                // therefore renders the wall exactly as it did before, instead of drowning it to the
                // brow off a number that only looks like chart datum.
                // Mirrored by CliffWaterlineMath: ElevationAt, SubmergenceMetres, Submerged01,
                // WetBand01, WeedFringe01, Barnacle01, SubmergedKeep01, FoamFleck01, HasPublishedSea.
                // The texel cell was found above, before the px relight (the cliff mix picks by it too).
                float keep = 1.0;   // 0 where the foot has dissolved into the sea

                if (_WaterlineStrength > 0.001 && _HHSeaLevelWorld.w > 0.5 && cellWaterline.z > 0.5)
                {
                    // Judged at the cell's centre row, so every band edge lands on a texel row.
                    // Twin: CliffWaterlineMath.CellElevation, SubmergenceMetres.
                    float submergence = cellWaterline.y - (cell.y + 0.5) / CLIFF_TEXELS_PER_METRE;
                    float strength = saturate(_WaterlineStrength);
                    float above = -submergence;   // metres above the line

                    // SEEN THROUGH WATER below the line: the column ABSORBS, so the rock keeps its shape
                    // and takes the water's colour, then detail is LOST into it with depth, floored by
                    // _SubmergedFloor. In four flat steps, never a gradient.
                    float drown = submergence > 0.0
                                ? ceil(saturate(submergence / max(_SubmergedDepth, 1e-3)) * 4.0) / 4.0 : 0.0;
                    float3 wet = lit * _SubmergedTint.rgb;
                    lit = lerp(lit, wet, drown * strength);
                    float lost = drown * (1.0 - saturate(_SubmergedFloor));
                    lit = lerp(lit, _SubmergedTint.rgb * 0.6, lost * strength);

                    // THE FOOT DISSOLVES into the sea: four dithered steps over _WetFadeMetres, so the
                    // water shows through and the toe has no hard line. The quay's tidal face discards
                    // below its line for the same reason. Twin: CliffWaterlineMath.SubmergedKeep01.
                    if (submergence > 0.0 && _WetFadeMetres > 0.0)
                    {
                        float level = ceil(saturate(submergence / _WetFadeMetres) * 4.0) / 4.0;
                        keep = CliffBayer4(cell) >= level ? 1.0 : 0.0;
                    }

                    // THE WET BAND: rock the tide has just left, from the CURRENT line up to a torn top
                    // that steps every few columns. It rides the tide because it is measured from the
                    // line. Twin: CliffWaterlineMath.WetBandTopMetres, WetBand01.
                    float band = max(_DampBandMetres, 0.0);
                    if (band > 0.0 && above >= 0.0)
                    {
                        float group = floor(cell.x / CLIFF_WET_TOP_GROUP);
                        float top = band * (0.55 + 0.9 * CliffHash(float2(group, 0.0), 1.0))
                                  + (CliffHash(float2(cell.x, 0.0), 2.0) - 0.5) * 2.0 / CLIFF_TEXELS_PER_METRE;
                        top = max(top, 1.0 / CLIFF_TEXELS_PER_METRE);
                        if (above < top)
                            lit *= 1.0 - saturate(_DampDarken) * strength;
                    }

                    // ROCKWEED at the line, in tufts: from a texel under the water to a torn top, on the
                    // cells its cover picks. A tint on the LIT rock, so it darkens with the day.
                    // Twin: CliffWaterlineMath.WeedFringe01.
                    float weedTop = _WeedMetres * (0.4 + 1.2 * CliffHash(float2(floor(cell.x / 2.0), 0.0), 3.0));
                    if (_WeedMetres > 0.0 && above >= -1.0 / CLIFF_TEXELS_PER_METRE && above < weedTop
                        && CliffHash(cell, 4.0) < _WeedDensity)
                        lit = lerp(lit, lit * _WeedTint.rgb, strength);

                    // BARNACLE flecks just above the weed: sparse cells lifted to a pale grey of their own
                    // light. Twin: CliffWaterlineMath.Barnacle01.
                    float barnFrom = max(_WeedMetres, 0.0) * 0.5;
                    if (_BarnacleMetres > 0.0 && above >= barnFrom && above < barnFrom + _BarnacleMetres
                        && CliffHash(cell, 5.0) < _BarnacleDensity)
                    {
                        float luma = dot(lit, float3(0.299, 0.587, 0.114));
                        lit = lerp(lit, saturate(luma * _BarnacleLift * float3(1.0, 0.98, 0.92)), strength);
                    }

                    // THE FOAM COLLAR in flecks: tighter than the wet band and biased DOWN into the
                    // water, broken so it reads as broken water and not a painted stroke.
                    // Twin: CliffWaterlineMath.FoamCollar01, FoamFleck01.
                    float collar = max(_FoamCollarMetres, 0.0);
                    if (collar > 0.0)
                    {
                        float c = abs(submergence - collar * saturate(_FoamCollarBias));
                        float ct = saturate(1.0 - c / collar);
                        float foam = ct * ct * (3.0 - 2.0 * ct);
                        float brk = saturate(_FoamBreak);
                        float fleck = foam * (1.0 - 0.4 * brk) > brk * CliffHash(cell, 7.0) + (1.0 - brk) * 0.5
                                    ? 1.0 : 0.0;
                        lit = lerp(lit, _FoamColour.rgb, fleck * strength * _FoamColour.a);
                        keep = max(keep, fleck);   // foam sits ON the water, so it draws where the foot has gone
                    }
                }

                // Coverage rides in mask.A — full on a face, meaningful on the brow and toe decals. The
                // wall's soft edges (seam feathers, tapered run ends, the brow's fade) arrive in vertex
                // alpha and are drawn as a PIXEL DISSOLVE, never as glass.
                half4 col = half4(lit * IN.color.rgb, unlit.a * msk.a * CliffDissolve(cellAlpha, cell) * keep);
                clip(col.a - _AlphaClip);
                return col;
            }
            ENDHLSL
        }
    }

    Fallback Off
}
