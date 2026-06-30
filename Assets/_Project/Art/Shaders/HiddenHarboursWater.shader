// HiddenHarboursWater.shader — the layered, SIM-DRIVEN water surface (ADR 0010 / design/water-rendering.md).
//
// A custom URP 2D unlit ShaderLab/HLSL shader (NOT a Shader Graph — authored as text so it builds headless).
// It draws the hero water look as PIXEL ART (every layer pixelizes world coords to the PPU grid) and reads the
// SAME deterministic water level + seabed height the gameplay reads, so the visible waterline == the physical
// waterline (the P1 integrity rule). The runtime feeds it the sim each throttled tick (HiddenHarbours.Art.WaterSurface):
//
//   _WaterLevel   <- IEnvironmentService.WaterLevelAt(t)   (metres above chart datum; the moving shoreline)
//   _FlowDir/_Flow <- EnvironmentSample.CurrentVector       (tidal set -> surface scroll dir/speed)
//   _Roughness    <- EnvironmentSample.WindVector           (wind -> surface roughness / whitecaps)
//   _Chop         <- EnvironmentSample.SeaState             (sea-state -> swell amplitude / choppiness)
//   _HeightTex    <- ITidalTerrain.ElevationAt baked over the plane (the first-pass DEPTH source)
//
// EVERY colour / scroll-speed / foam-width / threshold is a material property so the owner art-directs in the
// Inspector with no graph editing (CLAUDE.md rule 6). Visual-only: drives no sim, saves nothing (rule 5).
Shader "HiddenHarbours/Water"
{
    Properties
    {
        [Header(Depth gradient (layer 1))]
        _ShallowColor   ("Shallow water", Color)        = (0.35, 0.62, 0.66, 0.95)
        _DeepColor      ("Deep water", Color)           = (0.06, 0.16, 0.24, 1.0)
        _ShallowDepth   ("Shallow depth (m)", Float)    = 0.15
        _DeepDepth      ("Deep depth (m)", Float)       = 3.0
        _DepthBands     ("Depth posterize bands (0=off)", Float) = 5

        [Header(Pixelization (all layers))]
        _PixelsPerUnit  ("Pixels per unit", Float)      = 32

        [Header(Surface distortion (layer 2))]
        _NoiseScale     ("Surface noise scale", Float)  = 0.35
        _Flow           ("Flow speed (scroll)", Float)  = 0.06
        _FlowDir        ("Flow direction (xy)", Vector) = (1, 0, 0, 0)
        _Chop           ("Choppiness (0..1)", Range(0,1)) = 0.25
        _SurfaceTint    ("Surface tint strength", Range(0,1)) = 0.18

        [Header(Wind chop and syncopation (multi rate multi direction surface))]
        // Layer-2 used to scroll EVERY noise octave along _FlowDir (the tidal CURRENT, a fixed axis), so
        // the surface read as one marching grid. These break that: a wind-driven chop octave scrolled
        // along the WIND, plus a slow cross-swell on a perpendicular axis, mixed by per-octave weights.
        // _WindDir is pushed by WaterSurface.cs from EnvironmentSample.WindVector (sim-driven, varies over
        // time); the default is the calm-wind fallback (+Y) so the look is sane before the sim feeds it.
        _WindDir        ("Wind direction (xy, sim-driven)", Vector) = (0, 1, 0, 0)
        _WindChop       ("Wind chop weight (0..1)", Range(0,1)) = 0.4
        _WindChopScale  ("Wind chop noise scale", Float) = 0.7
        _WindChopSpeed  ("Wind chop scroll speed", Float) = 0.09
        _CrossSwellDir  ("Cross-swell dir (xy; 0,0 = auto perpendicular)", Vector) = (0, 0, 0, 0)
        _CrossSwellSpeed("Cross-swell scroll speed", Float) = 0.025
        _CrossSwellScale("Cross-swell noise scale (big = long swell)", Float) = 0.16
        _Octave2Weight  ("Octave 2 (wind chop) mix weight", Range(0,1)) = 0.35
        _Octave3Weight  ("Octave 3 (cross swell) mix weight", Range(0,1)) = 0.3

        [Header(FBM low freq variance (organic patches and sparkle scatter))]
        // A big-scale, slow-drifting fractal field that breaks the even grid two ways (both col.rgb-only,
        // never touching depth/clip/the gameplay waterline): a soft brightness/tint patchwork, and a GATE
        // on the specular so sparkles CLUSTER organically instead of an even posterized lattice.
        _FbmScale       ("FBM scale (big = broad patches)", Float) = 0.05
        _FbmDriftSpeed  ("FBM drift speed", Float) = 0.012
        _FbmStrength    ("FBM tint strength (0..1)", Range(0,1)) = 0.18
        _FbmTint        ("FBM tint color", Color) = (0.55, 0.72, 0.78, 1.0)
        _FbmGateLo      ("FBM spec gate low (sparkles start)", Range(0,1)) = 0.35
        _FbmGateHi      ("FBM spec gate high (sparkles full)", Range(0,1)) = 0.7
        _SpecBands      ("Specular posterize bands", Float) = 4

        [Header(Rolling ocean swell (large scale cohesion   col.rgb only))]
        // The KEYSTONE of the cohesion pass. ONE big, long-wavelength swell field over worldXY that
        // modulates the BASE-COLOUR brightness (crests lighter, troughs darker) so broad light/dark bands
        // roll across the WHOLE surface — the small variance rides on top, and the sea reads as ONE
        // connected body. col.rgb ONLY: never touches depth/clip/the deep tint/the caustic gate/_WaterLevel.
        // Direction is derived in-shader from _WindDir when _OceanSwellDir is auto (0,0) — no C# change.
        _OceanSwellDir      ("Ocean swell dir (xy; 0,0 = auto from wind)", Vector) = (0, 0, 0, 0)
        _OceanSwellScale    ("Ocean swell scale (SMALL = long wavelength)", Float) = 0.025
        _OceanSwellSpeed    ("Ocean swell scroll speed (slow)", Float) = 0.018
        _OceanSwellStrength ("Ocean swell brightness amplitude (0..1)", Range(0,1)) = 0.16
        _OceanSwellSharpness("Ocean swell crest sharpness (1 = round)", Float) = 1.4

        [Header(Foam fringe (layer 3))]
        _FoamColor      ("Foam color", Color)           = (0.92, 0.96, 0.98, 1.0)
        _FoamWidth      ("Foam band width (m)", Float)  = 0.45
        _FoamSoftness   ("Foam edge softness (m)", Float) = 0.18
        _FoamNoise      ("Foam churn noise scale", Float) = 1.2
        _Roughness      ("Roughness / whitecaps (0..1)", Range(0,1)) = 0.2

        [Header(Wind streaked foam and swell coupling (whitecaps ride the swell))]
        // Open-water whitecaps stretched into long thin streaks ALONG the wind (anisotropic: sample the
        // whitecap noise at a coord COMPRESSED perpendicular to the wind so features elongate along it),
        // and preferentially placed on swell CRESTS (gate the cap mask by the swell field's high values)
        // so the foam rides the rolling swell instead of speckling evenly. All col.rgb-only dressing.
        _FoamStreakStretch  ("Foam streak stretch (1 = round, higher = streaks)", Float) = 3.5
        _FoamCrestGate      ("Whitecaps on swell crests (0 = even, 1 = crest-only)", Range(0,1)) = 0.6
        _SpecSwellBias      ("Specular bias toward lit swell faces (0..1)", Range(0,1)) = 0.35
        // The foam churn / whitecap scroll used to counter-move on a fixed diagonal (against the surface).
        // Now it drifts WITH the body along a BLEND of the wind (_WindDir) and the tidal current (_FlowDir)
        // — both sim-driven and time-wandering — so the foam flows with the one connected surface and
        // reorients as the weather shifts. 0 = pure current-led, 1 = pure wind-led; default a sensible mix.
        _FoamDriftWindVsCurrent ("Foam drift wind vs current (0 = current, 1 = wind)", Range(0,1)) = 0.6

        [Header(Living foam (evolving field   merge separate   not just scroll))]
        // The whitecaps + foam-fringe churn used to sample ONE ValueNoise that only TRANSLATED — a fixed-shape
        // stamp sliding across the surface, so it read as a REPEATING pattern whose blobs never changed shape.
        // These make the underlying field EVOLVE IN PLACE: bright spots appear, grow, drift, shrink and vanish.
        //   _FoamEvolveSpeed   — how fast the foam field BOILS / morphs (0 = frozen shapes, just drift).
        //   _FoamBlobScale     — the foam-blob size for the evolving field (smaller = larger blobs).
        //   _FoamThreshold     — the SOFT-THRESHOLD level on the evolving field: above it = foam. Higher = less foam.
        //   _FoamThresholdSoft — the smoothstep width around the threshold. This is what makes blobs MERGE (the
        //                        valley between two rising maxima crosses the threshold) and SEPARATE (it dips
        //                        back below) and fade in/out — metaball-like, organic, instead of a hard edge.
        _FoamEvolveSpeed    ("Foam evolve / boil speed (0 = frozen)", Float) = 0.25
        _FoamBlobScale      ("Foam blob scale (smaller = bigger blobs)", Float) = 2.2
        _FoamThreshold      ("Foam soft-threshold level (higher = less foam)", Range(0,1)) = 0.55
        _FoamThresholdSoft  ("Foam threshold softness (merge / separate band)", Range(0,1)) = 0.18

        [Header(Foam DENSITY (dual zone   solid white core plus milky soft edge))]
        // The SOFT (metaball) threshold made the foam MILKY EVERYWHERE — even at high field values the
        // smoothstep only gives partial coverage, so the owner's painted solid-white _FoamTex never reads
        // dense. These RESTORE a SOLID-WHITE CORE where the evolving field is WELL above threshold (full
        // opacity, the painted solid white showing through), keeping the milky smoothstep ONLY as the soft
        // edge near the threshold boundary. Result: a dense solid heart with soft milky edges.
        //   _FoamSolidThreshold — the field level ABOVE _FoamThreshold at which foam becomes SOLID (full
        //                         opacity). Between _FoamThreshold and here = the milky soft band; above = dense.
        //   _FoamDensity        — master: how strongly the solid core lifts opacity to full (0 = always milky
        //                         like before, 1 = full dense core). Drives calm(milky) to rough(solid).
        //   _FoamDensityWind    — how much wind/roughness RAISES density plus widens the solid zone, so a
        //                         building sea automatically gets denser, more widespread whitecaps (the
        //                         owner's milky-for-some-conditions, dense-for-others happens with the weather).
        _FoamSolidThreshold ("Foam SOLID-core level (above the soft band = dense white)", Range(0,1)) = 0.78
        _FoamDensity        ("Foam density master (0 = milky like before, 1 = solid core)", Range(0,1)) = 0.6
        _FoamDensityWind    ("Foam density wind coupling (rough means denser, wider)", Range(0,1)) = 0.5

        [Header(Whitecap LIFECYCLE (form on crest   peak   collapse to milky residual))]
        // A natural wave lifecycle for the OPEN-WATER whitecaps, keyed off the rolling-swell CREST factor
        // (SwellField, reused). Foam FORMS as the swell crest builds, PEAKS into a dense solid whitecap near
        // the crest MAXIMUM (the breaking crest), then COLLAPSES into milky residual (fading plus spreading
        // downwind via _FoamStreakStretch) as the crest passes. All col.rgb-only dressing (P1, rule 5).
        //   _WhitecapFormSharpness — how ABRUPTLY foam breaks at the crest top (0 = forms gradually across the
        //                            whole crest, 1 = a sharp narrow breaking band only at the very crest).
        //   _WhitecapPeakDensity   — the opacity of a NEWBORN whitecap on the breaking crest (the dense peak;
        //                            also the open-water cap opacity ceiling, replacing the old hard 0.6).
        //   _WhitecapCollapseRate  — how fast the cap AGES to milky residual as the crest drops away from the
        //                            peak (higher = collapses faster, more milky residual off the crests).
        _WhitecapFormSharpness ("Whitecap form sharpness at crest (0 = soft, 1 = sharp break)", Range(0,1)) = 0.5
        _WhitecapPeakDensity   ("Whitecap peak density (newborn crest opacity)", Range(0,1)) = 0.95
        _WhitecapCollapseRate  ("Whitecap collapse rate (age to milky off-crest)", Range(0,4)) = 1.5

        [Header(Shoreward swell and foam bias (waves roll IN near the coast))]
        // The rolling swell + the foam drift used to follow ONLY the wandering WIND (and the tidal current),
        // and the wind blows OFFSHORE part of the time — so near the beach the wave trains and foam streamed
        // OUT to sea ("foam blowing out of the sand"). Real swell is generated far offshore and rolls
        // SHOREWARD regardless of the local wind. These BIAS the swell + foam-drift direction toward the
        // shore NEAR the coast, fading back to the wind/current direction in deep water (the open sea keeps
        // its existing wind-driven cohesion). The shore direction is derived per-pixel from the SEABED HEIGHT
        // GRADIENT (shallower = toward land), so it reads the SAME baked height map the depth/foam already
        // use — a purely VISUAL direction; it NEVER touches depth/clip/the deep tint/_WaterLevel (P1, rule 5).
        //   _ShorewardBias      — master strength (0 = old wind-led behaviour, 1 = full roll-in at the shore).
        //   _ShorewardFalloff   — the depth (m) over which the bias fades from full (at the wet edge) to none
        //                         (deep water). Smaller = the roll-in hugs the very edge; larger = it reaches
        //                         further out before the open-sea wind cohesion takes over.
        //   _ShoreSampleStep    — the world-space step (m) the gradient is sampled over. Larger = a smoother,
        //                         broader shore direction (less sensitive to height-texel noise); smaller =
        //                         it follows finer coast shape. A few decimetres reads well over the coarse bake.
        _ShorewardBias    ("Shoreward bias strength (0 = wind-led, 1 = roll in)", Range(0,1)) = 0.7
        _ShorewardFalloff ("Shoreward falloff depth (m, fades to wind out at sea)", Float) = 2.5
        _ShoreSampleStep  ("Shore gradient sample step (m)", Float) = 0.4

        [Header(Beach swash   always on shoreline wash   cosmetic   foam band only)]
        _SwashAmplitude ("Swash amplitude (m, foam-band only)", Float) = 0.3
        _SwashSpeed     ("Swash speed (waves / 2pi sec)", Float)      = 0.5
        _SwashScale     ("Swash along-shore variation scale", Float)  = 0.25

        [Header(Specular glints (layer 4))]
        _SpecColor      ("Specular color", Color)       = (1.0, 0.98, 0.86, 1.0)
        _SpecAmount     ("Specular amount (0..1)", Range(0,1)) = 0.35
        _SpecSharpness  ("Specular sharpness", Float)   = 18
        _LightDir       ("Implied light dir (xy)", Vector) = (-0.6, 0.8, 0, 0)

        [Header(Caustics (layer 5  shallows))]
        _CausticColor   ("Caustic color", Color)        = (0.75, 0.95, 0.92, 1.0)
        _CausticAmount  ("Caustic amount (0..1)", Range(0,1)) = 0.3
        _CausticScale   ("Caustic scale", Float)        = 0.9
        _CausticDepth   ("Caustic max depth (m)", Float) = 1.4

        [Header(Sky reflections (sea state driven   STRONG sharp on CALM   gone in a storm))]
        // A FAKED reflection layer (single-pass, in-shader — NO reflection camera / extra render pass, which
        // would need wiring we cannot verify and blow the perf budget). On CALM/glassy water it adds a clean,
        // mirror-like sheen: it reflects the CURRENT SKY colour (the day/night _DayNightTint global — warm at
        // dusk, dark at night, bright at noon) smeared down the surface as a vertical-ish band (the stylized
        // mirror cue), plus a BRIGHTER sun streak/glitter sitting where the global sun is (_SunDir/_SunElevation).
        // As the sea-state (_Chop) rises the reflection SHARPNESS drops (it smears/scatters across the chop) and
        // its STRENGTH falls, reaching ~0 by _ReflectionFadeChop (a storm doesn't mirror); wind (_Roughness)
        // additionally dims + scatters it. So calm => strong+sharp, lively => broken+dim, gale => gone. col.rgb
        // ONLY: it adds to the colour like every other water layer and NEVER touches depth/clip/the deep tint/
        // the caustic gate/_WaterLevel (P1 integrity, CLAUDE.md rule 5). Master 0 = off = today's look.
        //   _ReflectionStrength   — master opacity dial (0 = OFF / today's look, 1 = full strong mirror at calm).
        //   _ReflectionFadeChop   — the _Chop sea-state at which the reflection has fully faded to nothing.
        //   _ReflectionWindFade   — how much wind/_Roughness ADDITIONALLY dims the reflection (a breezy sea).
        //   _ReflectionChopScatter/_ReflectionWindScatter — how much chop / wind SMEAR (soften) the reflection.
        //   _ReflectionSkyTint    — how much of the reflection is the current SKY colour (the _DayNightTint).
        //   _ReflectionColor      — the base reflected-sky colour used when the day/night cycle is not running.
        //   _ReflectionSmear      — the vertical smear length of a SHARP (calm) reflection, in metres.
        //   _ReflectionSunStreak  — intensity of the brighter sun glitter/streak that sits toward the sun.
        //   _ReflectionSunSharp   — how tight the sun streak reads at calm (higher = a narrower hotter streak).
        _ReflectionStrength    ("Reflection strength (master; 0 = off)", Range(0,1)) = 0.6
        _ReflectionFadeChop    ("Reflection fade-out sea-state (_Chop where it is gone)", Range(0,1)) = 0.6
        _ReflectionWindFade    ("Reflection wind dim (0 = wind ignored, 1 = wind kills it)", Range(0,1)) = 0.5
        _ReflectionChopScatter ("Reflection chop scatter (chop smears it)", Range(0,4)) = 1.5
        _ReflectionWindScatter ("Reflection wind scatter (wind smears it)", Range(0,4)) = 0.8
        _ReflectionSkyTint     ("Reflection sky tint weight (use the day/night sky)", Range(0,1)) = 0.85
        _ReflectionColor       ("Reflection base sky color (cycle-off fallback)", Color) = (0.62, 0.74, 0.86, 1.0)
        _ReflectionSmear       ("Reflection vertical smear length (m, at calm)", Float) = 1.6
        _ReflectionSunStreak   ("Reflection sun streak intensity", Range(0,2)) = 0.9
        _ReflectionSunSharp    ("Reflection sun streak sharpness", Float) = 6.0

        [Header(Depth source)]
        _WaterLevel     ("Water level (m, sim-driven)", Float) = 0.0
        [NoScaleOffset] _HeightTex ("Seabed height map (R=elevation)", 2D) = "black" {}
        _HeightMin      ("Height map min (m)", Float)   = -4.0
        _HeightMax      ("Height map max (m)", Float)   = 6.0
        _HeightWorldMin ("Height map world min (xy)", Vector) = (-80, -60, 0, 0)
        _HeightWorldSize("Height map world size (xy)", Vector) = (160, 120, 0, 0)
        [Toggle(_USE_HEIGHTTEX)] _UseHeightTex ("Use baked height map", Float) = 0

        // ---------------------------------------------------------------------------------------------
        // OWNER-PAINTED TEXTURE SLOTS (optional art-direction over the procedural look).
        // Every slot is OFF by default (its _Use* toggle = 0), so an EMPTY material renders the shipped
        // first-pass PROCEDURAL look unchanged. Assign a texture AND tick its toggle to blend with /
        // override the matching procedural layer. Every slot samples on the PIXEL grid (PPU-snapped
        // world coords) with Repeat wrap + POINT (no-AA) filtering — set the texture import settings to
        // match (Filter Mode = Point, Wrap Mode = Repeat). Each carries a [0..1] strength/blend so the
        // owner dials procedural<->painted. Spec + suggested dims: design/water-rendering.md
        // "Owner-painted texture slots".
        [Header(Painted textures   optional   blend or override procedural)]
        _PaintScale      ("Painted texture scale (tiles/unit)", Float) = 0.25
        // Anti-tiling: hide the painted tile's repeat grid (IQ-style hash-untile + domain warp). 0 = raw
        // tiling (the grid reads at CALM), 1 = full break-up. Applied to every scrolling painted slot.
        _UntileStrength  ("Untile strength (0=raw grid, 1=broken up)", Range(0,1)) = 0.6

        [Toggle(_USE_SURFACETEX)] _UseSurfaceTex ("Use surface ripple texture", Float) = 0
        [NoScaleOffset] _SurfaceTex ("Surface ripple/detail (grayscale, seamless ~64)", 2D) = "gray" {}
        _SurfaceTexStrength ("Surface tex blend (0=proc, 1=painted)", Range(0,1)) = 1.0

        [Toggle(_USE_FOAMTEX)] _UseFoamTex ("Use foam texture", Float) = 0
        [NoScaleOffset] _FoamTex ("Foam pattern (white-on-transparent, seamless ~64)", 2D) = "white" {}
        _FoamTexStrength ("Foam tex blend (0=proc churn, 1=painted)", Range(0,1)) = 1.0

        [Toggle(_USE_CAUSTICTEX)] _UseCausticTex ("Use caustic texture", Float) = 0
        [NoScaleOffset] _CausticTex ("Caustics (grayscale, seamless ~64)", 2D) = "black" {}
        _CausticTexStrength ("Caustic tex blend (0=proc, 1=painted)", Range(0,1)) = 1.0

        [Toggle(_USE_SPARKLETEX)] _UseSparkleTex ("Use sparkle texture", Float) = 0
        [NoScaleOffset] _SparkleTex ("Specular glint pattern (white-on-black, seamless ~32)", 2D) = "black" {}
        _SparkleTexStrength ("Sparkle tex blend (0=proc, 1=painted)", Range(0,1)) = 1.0
        _SparkleTexScale ("Sparkle texture scale (tiles/unit)", Float) = 0.5

        [Toggle(_USE_DEPTHRAMP)] _UseDepthRamp ("Use depth colour ramp", Float) = 0
        [NoScaleOffset] _DepthRamp ("Depth ramp (1D, shallow u=0 -> deep u=1)", 2D) = "white" {}

        [Toggle(_USE_WHITECAPTEX)] _UseWhitecapTex ("Use whitecap texture", Float) = 0
        [NoScaleOffset] _WhitecapTex ("Whitecap pattern (white-on-transparent, seamless ~64)", 2D) = "white" {}
        _WhitecapTexStrength ("Whitecap tex blend (0=proc, 1=painted)", Range(0,1)) = 1.0

        [Header(Palette guard rail (final soft grade   col.rgb only   ADR 0015))]
        // THE LAST STAGE before return: a SOFT guard-rail that keeps the composited water colour inside an
        // art-directed palette so it can never wash out (too bright) or go muddy (too dark), while preserving
        // the dynamic, sea-state-driven diversity. The owner chose SOFT rails — bound the extremes and gently
        // PULL toward the palette, NOT a hard lock. Three coupled ops, all on col.rgb ONLY (never depth/clip/
        // _WaterLevel/the sim — P1 integrity, CLAUDE.md rule 5), scaled by the master so 0 = exactly today.
        //   (1) VALUE floor + ceiling  — no mud, no blowout. The FLOOR is DAY/NIGHT-AWARE: it pre-compensates
        //       for the day/night overlay's downstream MULTIPLY so daylight never goes muddy while true night
        //       still goes genuinely dark (it reads the global _DayNightTint; see WaterPaletteGrade.cs + ADR 0015).
        //   (2) SATURATION cap         — pull chroma toward grey only above the cap.
        //   (3) ANCHOR pull            — gently lerp toward the nearest palette anchor (deep/mid/shallow/foam,
        //                                chosen by luminance) at a soft strength (a rail, not a cage).
        // _PaletteGradeStrength = 0 is an EXACT passthrough (opt-in, revertible). The four anchors + the bounds
        // are per-material, so a Water variant carries its palette (North Atlantic / Stirred Brown / Deep Blue /
        // Tropical / the mood variants). Mirrored headless by WaterPaletteGrade for the determinism guard.
        _PaletteGradeStrength ("Palette grade strength (master; 0 = today's look)", Range(0,1)) = 0.35
        _PaletteValueFloor    ("Palette value floor (daylight; no mud)", Range(0,1)) = 0.10
        _PaletteValueCeil     ("Palette value ceiling (no blowout)", Range(0,1)) = 0.85
        _PaletteSatCap        ("Palette saturation cap", Range(0,1)) = 0.55
        _PalettePullStrength  ("Palette anchor pull (soft; 0.3..0.4 is a rail)", Range(0,1)) = 0.35
        _PaletteNightFloor    ("Palette night floor (on-screen; 0 = night goes dark)", Range(0,1)) = 0.0
        _PaletteDeep    ("Palette anchor   deep", Color)    = (0.05, 0.135, 0.205, 1)
        _PaletteMid     ("Palette anchor   mid", Color)     = (0.14, 0.30, 0.38, 1)
        _PaletteShallow ("Palette anchor   shallow", Color) = (0.34, 0.60, 0.62, 1)
        _PaletteFoam    ("Palette anchor   foam", Color)    = (0.92, 0.96, 0.98, 1)
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        Pass
        {
            // Unlit transparent: the 2D renderer draws this; we light it ourselves (one implied sun, ADR 0006).
            Tags { "LightMode" = "Universal2D" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            // multi_compile (not shader_feature) so the height-map branch is ALWAYS compiled — WaterSurface
            // toggles _USE_HEIGHTTEX at runtime after baking, and a shader_feature variant absent from the
            // build would silently fall back to the off (uniform-deep) path.
            #pragma multi_compile_local _ _USE_HEIGHTTEX
            // Painted-texture branches: shader_feature (not multi_compile) — these toggles are baked into
            // the MATERIAL by the owner in the Inspector (NOT flipped by a runtime script, unlike
            // _USE_HEIGHTTEX), so only the combinations a shipped material actually uses need to compile.
            // shader_feature keeps the variant count minimal AND preserves the material's chosen keywords;
            // every branch is still syntax-checked by the importer. One _local keyword per slot.
            #pragma shader_feature_local _ _USE_SURFACETEX
            #pragma shader_feature_local _ _USE_FOAMTEX
            #pragma shader_feature_local _ _USE_CAUSTICTEX
            #pragma shader_feature_local _ _USE_SPARKLETEX
            #pragma shader_feature_local _ _USE_DEPTHRAMP
            #pragma shader_feature_local _ _USE_WHITECAPTEX
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float3 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float2 worldXY    : TEXCOORD1;   // world-space XY (metres; 1 unit = 1 m at PPU 32)
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            TEXTURE2D(_HeightTex);
            SAMPLER(sampler_HeightTex);

            // Owner-painted slots. Each uses its own sampler so the texture's import settings (author
            // them Point filter + Repeat wrap for the pixel look) drive sampling — no forced inline state.
            TEXTURE2D(_SurfaceTex);   SAMPLER(sampler_SurfaceTex);
            TEXTURE2D(_FoamTex);      SAMPLER(sampler_FoamTex);
            TEXTURE2D(_CausticTex);   SAMPLER(sampler_CausticTex);
            TEXTURE2D(_SparkleTex);   SAMPLER(sampler_SparkleTex);
            TEXTURE2D(_DepthRamp);    SAMPLER(sampler_DepthRamp);
            TEXTURE2D(_WhitecapTex);  SAMPLER(sampler_WhitecapTex);

            // GLOBAL sun direction from the day/night cycle (Shader.SetGlobalVector by DayNightController,
            // ADR 0013). NOT per-material, so it lives OUTSIDE the per-material CBUFFER (like the grass
            // shader's _WindWorld). _SunDir.xy = the ground-plane direction TOWARD the sun; (0,0,0,0) when
            // the cycle is not running, in which case the specular falls back to the material's hand-authored
            // _LightDir below. This makes the sea's glints agree with where the global light comes from.
            float4 _SunDir;
            // The day/night SKY/scene colour the controller multiplies the whole frame by (Shader.SetGlobalColor,
            // ADR 0013) — warm at dusk, dark at night, bright at noon. The reflection layer reflects THIS as the
            // sky colour so the mirror reads the current sky. (1,1,1,1) when the cycle is not running (full day),
            // in which case the reflection falls back to the material's _ReflectionColor. Also a GLOBAL (outside
            // the per-material CBUFFER) — both the day/night overlay shader and this one read the same value.
            float4 _DayNightTint;
            // The sun's height: 1 at noon, 0 at the horizon, <=0 at night (ADR 0013). The sun streak fades out as
            // the sun sets (no glitter under a set sun). 0 when the cycle is not running -> handled by the fallback.
            float  _SunElevation;

            // GLOBAL BOAT SPOTLIGHT (ADR 0016) — published by HiddenHarbours.Art.BoatSpotlight via Shader.SetGlobal*.
            // The boat's additive QUAD lights LAND, but the URP 2D renderer draws this custom-shader WATER OVER the
            // quad regardless of sorting order (two quad-sort fixes failed). So the water LIGHTS ITSELF: the frag
            // reads these globals and ADDS the cone illumination to its own col.rgb (NO sorting dependency — it
            // cannot fail like the quad did, and composes with the reflection/foam/palette). ONE light for now
            // (the boat spotlight is THE night-nav light); the clean extension to many is to publish ARRAYS +
            // a count and loop — the single-light path is a count-1 case of that. GLOBALS (outside the per-material
            // CBUFFER) like _SunDir, so an empty material still compiles and a no-boat scene reads them as zero.
            float4 _BoatLightPos;       // xy = world lamp position (the bow anchor)
            float4 _BoatLightDir;       // xy = world beam axis (the boat heading; ~unit length)
            float4 _BoatLightColor;     // rgb = beam colour
            float4 _BoatLightParams;    // x = intensity (<=0 means OFF), y = range (m), z = cos(halfAngle), w = cos(innerAngle)
            float4 _BoatLightParams2;   // x = radial edge softness, y = gate threshold, z = gate softness, w = cycle-off fallback

            // SRP-batcher friendly: every per-material property in one CBUFFER (the runtime sets these via a
            // MaterialPropertyBlock; the sim-driven ones change on the slow tick, not per frame).
            CBUFFER_START(UnityPerMaterial)
                float4 _ShallowColor;
                float4 _DeepColor;
                float  _ShallowDepth;
                float  _DeepDepth;
                float  _DepthBands;
                float  _PixelsPerUnit;
                float  _NoiseScale;
                float  _Flow;
                float4 _FlowDir;
                float  _Chop;
                float  _SurfaceTint;
                // Wind chop + syncopation (multi-rate / multi-direction surface octaves).
                float4 _WindDir;
                float  _WindChop;
                float  _WindChopScale;
                float  _WindChopSpeed;
                float4 _CrossSwellDir;
                float  _CrossSwellSpeed;
                float  _CrossSwellScale;
                float  _Octave2Weight;
                float  _Octave3Weight;
                // FBM low-frequency variance (organic patches + sparkle scatter).
                float  _FbmScale;
                float  _FbmDriftSpeed;
                float  _FbmStrength;
                float4 _FbmTint;
                float  _FbmGateLo;
                float  _FbmGateHi;
                float  _SpecBands;
                // Rolling ocean swell (large-scale cohesion; col.rgb-only brightness bands).
                float4 _OceanSwellDir;
                float  _OceanSwellScale;
                float  _OceanSwellSpeed;
                float  _OceanSwellStrength;
                float  _OceanSwellSharpness;
                float4 _FoamColor;
                float  _FoamWidth;
                float  _FoamSoftness;
                float  _FoamNoise;
                float  _Roughness;
                // Wind-streaked foam + swell coupling + the foam-drift wind/current blend.
                float  _FoamStreakStretch;
                float  _FoamCrestGate;
                float  _SpecSwellBias;
                float  _FoamDriftWindVsCurrent;
                // Living foam: the evolving-field boil + the soft-threshold (merge/separate) levers.
                float  _FoamEvolveSpeed;
                float  _FoamBlobScale;
                float  _FoamThreshold;
                float  _FoamThresholdSoft;
                // Dual-zone density (solid-white core + milky soft edge) + the condition coupling.
                float  _FoamSolidThreshold;
                float  _FoamDensity;
                float  _FoamDensityWind;
                // Whitecap lifecycle (form on the crest -> peak -> collapse to milky residual).
                float  _WhitecapFormSharpness;
                float  _WhitecapPeakDensity;
                float  _WhitecapCollapseRate;
                // Shoreward swell/foam bias (near-coast roll-in; visual direction only).
                float  _ShorewardBias;
                float  _ShorewardFalloff;
                float  _ShoreSampleStep;
                float  _SwashAmplitude;
                float  _SwashSpeed;
                float  _SwashScale;
                float4 _SpecColor;
                float  _SpecAmount;
                float  _SpecSharpness;
                float4 _LightDir;
                float4 _CausticColor;
                float  _CausticAmount;
                float  _CausticScale;
                float  _CausticDepth;
                // Sky reflections (sea-state-driven; col.rgb-only dressing).
                float  _ReflectionStrength;
                float  _ReflectionFadeChop;
                float  _ReflectionWindFade;
                float  _ReflectionChopScatter;
                float  _ReflectionWindScatter;
                float  _ReflectionSkyTint;
                float4 _ReflectionColor;
                float  _ReflectionSmear;
                float  _ReflectionSunStreak;
                float  _ReflectionSunSharp;
                float  _WaterLevel;
                float  _HeightMin;
                float  _HeightMax;
                float4 _HeightWorldMin;
                float4 _HeightWorldSize;
                // Painted-texture blend strengths + tiling (the _Use* toggle floats live only as keyword
                // drivers — like _UseHeightTex — and are intentionally NOT in the CBUFFER).
                float  _PaintScale;
                float  _UntileStrength;
                float  _SurfaceTexStrength;
                float  _FoamTexStrength;
                float  _CausticTexStrength;
                float  _SparkleTexStrength;
                float  _SparkleTexScale;
                float  _WhitecapTexStrength;
                // Palette guard-rail (the final soft grade; col.rgb-only — ADR 0015).
                float  _PaletteGradeStrength;
                float  _PaletteValueFloor;
                float  _PaletteValueCeil;
                float  _PaletteSatCap;
                float  _PalettePullStrength;
                float  _PaletteNightFloor;
                float4 _PaletteDeep;
                float4 _PaletteMid;
                float4 _PaletteShallow;
                float4 _PaletteFoam;
            CBUFFER_END

            // ---- pixelize: snap a world coord to the PPU grid so every layer reads as pixel art (ADR 0010 (2)) ----
            float2 Pixelize(float2 p)
            {
                float ppu = max(_PixelsPerUnit, 1.0);
                return floor(p * ppu) / ppu;
            }

            // ---- cheap value noise (hash-lattice, smooth interpolation). Deterministic, no textures. ----
            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 345.45));
                p += dot(p, p + 34.345);
                return frac(p.x * p.y);
            }

            // 2-vector hash (different lattice constants from Hash21 so the untile/swell offsets don't
            // correlate with the surface noise). Defined up here with the other hash helpers because the
            // swell/foam evolving-field code below CALLS it — HLSL/D3D needs definition before use.
            float2 Hash22(float2 p)
            {
                p = float2(dot(p, float2(127.1, 311.7)), dot(p, float2(269.5, 183.3)));
                return frac(sin(p) * 43758.5453);
            }

            float ValueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);   // smoothstep weights
                float a = Hash21(i + float2(0, 0));
                float b = Hash21(i + float2(1, 0));
                float c = Hash21(i + float2(0, 1));
                float d = Hash21(i + float2(1, 1));
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            // ---- WIND-DRIVEN chop octave (a SEPARATE scroll from the tidal current) -------------------------
            // A 2-octave value-noise field scrolled along the WIND direction (normalize(_WindDir.xy)) at its
            // OWN rate (_WindChopSpeed) and scale (_WindChopScale) — NOT along _FlowDir. This is what lets the
            // surface follow the wind (which the sim varies over time) instead of marching down the fixed
            // current axis. Pixelized like every other octave so it reads as pixel art. Returns 0..1.
            float WindChop(float2 worldXY, float t)
            {
                float2 dir = normalize(_WindDir.xy + float2(0, 1e-4));   // +Y fallback on a zero wind dir
                float2 scroll = dir * (_WindChopSpeed * t);
                float2 p1 = Pixelize((worldXY + scroll) * _WindChopScale);
                float2 p2 = Pixelize((worldXY + scroll * 1.7) * _WindChopScale * 2.3);
                return ValueNoise(p1) * 0.6 + ValueNoise(p2) * 0.4;
            }

            // ---- FBM: fractal value-noise (low-frequency organic variance) ----------------------------------
            // A few octaves of ValueNoise (lacunarity ~2, gain ~0.5) summed to a normalized 0..1 field. Sampled
            // at a BIG scale (_FbmScale) and slowly drifted (_FbmDriftSpeed) it gives broad, slowly-moving
            // patches — used to (i) softly tint col.rgb and (ii) GATE the specular so sparkles cluster, both
            // of which break the single-direction "marching grid" read. Pixelized so it stays pixel-art.
            //
            // Octave count is a COMPILE-TIME CONSTANT (FBM_OCTAVES), NOT a runtime parameter: an [unroll] over a
            // loop whose bound is a runtime value fails to compile on some shader targets/variants (that broke
            // the painted-keyword variant => magenta). A literal trip count lets [unroll] resolve cleanly.
            #define FBM_OCTAVES 4
            float Fbm(float2 p)
            {
                float sum = 0.0;
                float amp = 0.5;
                float norm = 0.0;
                [unroll]
                for (int i = 0; i < FBM_OCTAVES; i++)
                {
                    sum  += ValueNoise(Pixelize(p)) * amp;
                    norm += amp;
                    p    *= 2.0;     // lacunarity
                    amp  *= 0.5;     // gain
                }
                return sum / max(norm, 1e-4);
            }

            // ---- EVOLVING (pseudo-3D) noise FIELD — the LIVING-FOAM keystone ---------------------------------
            // The old whitecaps/foam churn sampled ONE ValueNoise that only TRANSLATED (a fixed-shape stamp
            // sliding across the surface) — so it read as a REPEATING pattern whose blobs never changed shape.
            // This returns a field that EVOLVES IN PLACE: bright spots appear, grow, drift, shrink and vanish.
            //
            // Mechanism (cheapest that reads well): a pseudo-3D ValueNoise built by BLENDING TWO time-offset
            // ValueNoise samples of the SAME coord, where the MIX itself animates. As the mix sweeps 0->1 a
            // local maximum from sample-1 fades while a (differently placed) maximum from sample-2 rises — so the
            // field MORPHS rather than sliding. Two such "boil" pairs half a step out of phase (a smoothed
            // crossfade) keep the morph continuous and seamless (no popping when one pair re-randomizes). A slow
            // `drift` (passed in, = wind+current) is layered ON TOP so the evolving field STILL travels with the
            // weather — the owner keeps the wind-direction drift; the evolution is added, not a replacement.
            //
            // `evolveSpeed` sets how fast the field boils (morph rate); `worldXY*scale` is the blob size. Pure
            // value-noise + pixelize (pixel-art faithful, §3), no textures, a few noise taps. Returns ~0..1.
            // Drives ONLY col.rgb foam dressing — never depth/clip/_WaterLevel (P1 integrity, CLAUDE.md rule 5).
            float EvolvingField(float2 worldXY, float2 drift, float scale, float evolveSpeed, float t)
            {
                // the field coord: pixelized world position (with the slow weather drift) at the blob scale.
                float2 p = Pixelize((worldXY + drift) * max(scale, 1e-4));

                // a slow "boil" clock; z is the pseudo-third axis the lattice is offset along.
                float z = t * max(evolveSpeed, 0.0);
                float zi = floor(z);
                float zf = z - zi;                       // 0..1 within the current boil step

                // Two decorrelated lattice offsets per integer boil step (hash the step so each step's pair of
                // maxima sit in DIFFERENT places — that's what makes spots move/merge as the mix sweeps).
                float2 oA = Hash22(float2(zi,        37.2)) * 8.0;   // a few cells of lattice shift
                float2 oB = Hash22(float2(zi + 1.0,  37.2)) * 8.0;
                // crossfade the two samples by the smoothed sub-step phase: maxima from A fade as B's rise.
                float fade = zf * zf * (3.0 - 2.0 * zf);             // smoothstep(0,1,zf) — no popping at step edges
                float pair = lerp(ValueNoise(p + oA), ValueNoise(p + oB), fade);

                // A SECOND boil pair half a step out of phase, averaged in, so the morph never momentarily freezes
                // at a step boundary (when one pair's fade hits an endpoint the other is mid-sweep). Cheap continuity.
                float z2  = z + 0.5;
                float zi2 = floor(z2);
                float zf2 = z2 - zi2;
                float2 oC = Hash22(float2(zi2,       91.7)) * 8.0;
                float2 oD = Hash22(float2(zi2 + 1.0, 91.7)) * 8.0;
                float fade2 = zf2 * zf2 * (3.0 - 2.0 * zf2);
                float pair2 = lerp(ValueNoise(p + oC), ValueNoise(p + oD), fade2);

                // average the two out-of-phase pairs => a continuously MORPHING ~0..1 field (in-place evolution).
                return (pair + pair2) * 0.5;
            }

            // Three-octave SYNCOPATED surface noise. Each octave has a DISTINCT (direction, rate) so the
            // surface stops reading as one marching grid (the owner's "marches one direction" complaint):
            //   A = the current swell along _FlowDir @ _Flow      (the original look, the base)
            //   B = the wind chop  along _WindDir  @ _WindChopSpeed (follows the sim wind, weighted _WindChop)
            //   C = a SLOW cross-swell on a perpendicular axis @ _CrossSwellSpeed, big _CrossSwellScale
            // Octaves B and C are folded in by per-octave weights (_Octave2Weight / _Octave3Weight) so the
            // owner can dial the syncopation. Still pure value-noise + pixelize — no textures, ~no extra cost.
            float SurfaceNoise(float2 worldXY, float t)
            {
                // A — current swell along the tidal set (the existing octave; the foundation).
                float2 flowDir = normalize(_FlowDir.xy + float2(1e-4, 0));
                float2 scrollA = flowDir * (_Flow * t);
                float2 pA1 = Pixelize((worldXY + scrollA) * _NoiseScale);
                float2 pA2 = Pixelize((worldXY - scrollA * 0.6) * _NoiseScale * 2.0);
                float octaveA = ValueNoise(pA1) * 0.65 + ValueNoise(pA2) * 0.35;

                // B — wind chop along the wind direction at its own rate (raw 0..1 octave).
                float octaveB = WindChop(worldXY, t);

                // C — slow cross-swell on a perpendicular axis: either the explicit _CrossSwellDir, or (when
                // that's near-zero) the perpendicular of the average of flow & wind, so it crosses the grain.
                float2 avgDir = normalize(flowDir + normalize(_WindDir.xy + float2(0, 1e-4)) + float2(1e-4, 0));
                float2 autoCross = float2(-avgDir.y, avgDir.x);                  // rotate 90 deg
                float2 crossDir = (dot(_CrossSwellDir.xy, _CrossSwellDir.xy) > 1e-6)
                                    ? normalize(_CrossSwellDir.xy) : autoCross;
                float2 scrollC = crossDir * (_CrossSwellSpeed * t);
                float octaveC = ValueNoise(Pixelize((worldXY + scrollC) * _CrossSwellScale));

                // Weighted blend, normalized so the result stays ~0..1 regardless of the syncopation weights.
                // Each octave has ONE clear effective weight (no double-counting): the wind chop's mix weight
                // is _WindChop * _Octave2Weight (the headline wind knob × the octave-2 fine-tune); the
                // cross-swell's is _Octave3Weight. _Octave2/3Weight both default to a modest mix so the
                // syncopation reads immediately but stays dial-able to 0 (back to the single-direction look).
                float wB = _WindChop * _Octave2Weight;
                float wC = _Octave3Weight;
                float total = 1.0 + wB + wC;
                return (octaveA + octaveB * wB + octaveC * wC) / total;
            }

            // Sample the seabed elevation (metres above datum) at a world position. With the baked height map
            // the depth gradient + foam band match TidalTerrain exactly; without it, the plane reads as uniform
            // deep water (a safe fallback before a region bakes its height). Defined HERE (above the swell/foam
            // direction helpers) because the shoreward-bias code below reads the height GRADIENT through it.
            float SeabedElevation(float2 worldXY)
            {
            #if defined(_USE_HEIGHTTEX)
                float2 uv = (worldXY - _HeightWorldMin.xy) / max(_HeightWorldSize.xy, float2(1e-3, 1e-3));
                float r = SAMPLE_TEXTURE2D(_HeightTex, sampler_HeightTex, uv).r;
                return lerp(_HeightMin, _HeightMax, r);
            #else
                return _HeightMin;   // no height map => everywhere deep (uniform tint, no false shoreline)
            #endif
            }

            // ---- SHOREWARD direction: which way is LAND? (from the seabed height gradient) -------------------
            // Real ocean swell is generated far offshore and rolls SHOREWARD regardless of the local wind; foam
            // at the wet edge runs UP the beach. The wind (the swell/foam driver below) WANDERS and blows
            // offshore part of the time, which made the wave trains + foam stream OUT from the beach ("foam
            // blowing out of the sand"). This derives the shoreward direction PER PIXEL from the baked height
            // map: the elevation rises toward land, so the GRADIENT of elevation points toward shallower water =
            // toward the shore. We sample the seabed at +/- a small world step on each axis (central difference)
            // and normalize. Returns float2(0,0) on flat seabed (no height map / open deep water) so the caller
            // keeps the pure wind/current direction there. VISUAL-only — never touches depth/clip (P1, rule 5).
            float2 ShoreDir(float2 worldXY)
            {
            #if defined(_USE_HEIGHTTEX)
                float h = max(_ShoreSampleStep, 1e-3);
                float ex = SeabedElevation(worldXY + float2(h, 0)) - SeabedElevation(worldXY - float2(h, 0));
                float ey = SeabedElevation(worldXY + float2(0, h)) - SeabedElevation(worldXY - float2(0, h));
                float2 grad = float2(ex, ey);                 // points toward HIGHER (shallower) ground = shoreward
                float g = length(grad);
                return g > 1e-5 ? grad / g : float2(0, 0);    // flat seabed => no shore preference
            #else
                return float2(0, 0);                          // no height map => no shoreward bias (open water)
            #endif
            }

            // ---- near-shore WEIGHT: how strongly to steer toward shore at this depth ------------------------
            // Full at the wet edge (depth ~ 0), fading to 0 by _ShorewardFalloff metres deep, scaled by the
            // master _ShorewardBias. So waves/foam roll IN near the coast and the OPEN sea keeps its existing
            // wind-driven cohesion. 0 everywhere when _ShorewardBias = 0 (the old behaviour, dial-able off).
            float ShorewardWeight(float depth)
            {
                float falloff = max(_ShorewardFalloff, 1e-3);
                float near = 1.0 - smoothstep(0.0, falloff, max(depth, 0.0));   // 1 at the edge -> 0 deep
                return saturate(_ShorewardBias) * near;
            }

            // ---- bias a base (wind/current) direction toward the shore by a weight -------------------------
            // Mirrors WaterSurface.BiasTowardShore (the headless determinism twin). lerp(base, shore, w) then
            // re-normalize; when shore is zero (flat seabed) or w is 0 the base direction is returned unchanged.
            // Pure direction math — NaN-safe, unit-length out. The shoreward bias is a VISUAL steer only.
            float2 BiasTowardShore(float2 baseDir, float2 shoreDir, float w)
            {
                if (w <= 1e-4 || dot(shoreDir, shoreDir) < 1e-6)
                    return baseDir;
                float2 blended = lerp(baseDir, shoreDir, saturate(w));
                float m = length(blended);
                return m > 1e-5 ? blended / m : baseDir;
            }

            // ---- swell direction: wind generates swell, so default to the (time-wandering) WIND axis ---------
            // _OceanSwellDir (0,0) = auto-from-wind; an explicit override wins. Normalized, +Y fallback so the
            // bands never freeze to a NaN axis. As the sim wanders _WindDir, the swell bands REORIENT with it.
            // NEAR the coast the direction is BIASED toward the shore (waves roll IN) by ShorewardWeight(depth);
            // in deep water (depth past the falloff) the pure wind/override axis is kept (open-sea cohesion).
            float2 SwellDir(float2 worldXY, float depth)
            {
                float2 d = (dot(_OceanSwellDir.xy, _OceanSwellDir.xy) > 1e-6)
                             ? _OceanSwellDir.xy : _WindDir.xy;
                float2 baseDir = normalize(d + float2(0, 1e-4));
                return BiasTowardShore(baseDir, ShoreDir(worldXY), ShorewardWeight(depth));
            }

            // ---- ROLLING OCEAN SWELL (the cohesion keystone) -------------------------------------------------
            // ONE big, long-wavelength swell field over worldXY: a low-frequency directional wave (a sine ALONG
            // the swell axis, broken up by a slow value-noise so the bands aren't ruler-straight), scrolling
            // SLOWLY along that axis. Returns a 0..1 crest factor — high on crests, low in troughs. The caller
            // uses it to modulate ONLY col.rgb brightness so broad light/dark bands roll across the WHOLE
            // surface (the small variance riding on top), and reuses the SAME field to ride the whitecaps on the
            // crests + bias the specular. Pixelized so it stays pixel-art. Drives no depth/clip/sim (P1, rule 5).
            // `depth` lets the swell axis curve SHOREWARD near the coast (the roll-in) while the open sea keeps
            // the wind axis — so the crest BANDS advance toward the beach instead of streaming offshore.
            float SwellField(float2 worldXY, float depth, float t)
            {
                float2 dir = SwellDir(worldXY, depth);
                // distance projected ALONG the swell axis, advanced slowly with time (long rolling wave).
                float phase = dot(Pixelize(worldXY) * _OceanSwellScale, dir) - t * _OceanSwellSpeed;
                // base sine wave (0..1), plus a slow value-noise wander so the bands read organic, not ruled.
                float wave = sin(phase * 6.2831853) * 0.5 + 0.5;
                float wander = ValueNoise(Pixelize(worldXY * _OceanSwellScale * 1.3) + t * _OceanSwellSpeed * 0.5);
                float crest = saturate(wave * 0.75 + wander * 0.25);
                // sharpen the crest so the light bands read as crests sitting above broad troughs (1 = round).
                return pow(crest, max(_OceanSwellSharpness, 0.05));
            }

            // ---- foam DRIFT direction: a BLEND of the (wandering) wind and the (wandering) tidal current ------
            // Real surface foam follows both forces. _FoamDriftWindVsCurrent dials wind-led (1) vs current-led
            // (0). Both axes are sim-driven and drift over time, so the foam reorients as the weather shifts.
            // This replaces the old fixed counter-diagonal so the foam flows WITH the one connected body.
            // NEAR the coast the drift is BIASED toward the shore (foam runs UP the beach) by ShorewardWeight;
            // deep-water foam keeps the wind/current blend (so the open sea is unchanged). The shoreward steer
            // is what stops the foam streaming OUT of the sand when the wind happens to blow offshore.
            float2 FoamDriftDir(float2 worldXY, float depth)
            {
                float2 wind    = normalize(_WindDir.xy + float2(0, 1e-4));
                float2 current = normalize(_FlowDir.xy + float2(1e-4, 0));
                float2 blend   = lerp(current, wind, saturate(_FoamDriftWindVsCurrent));
                float2 baseDir = normalize(blend + float2(1e-4, 1e-4));
                return BiasTowardShore(baseDir, ShoreDir(worldXY), ShorewardWeight(depth));
            }

            // ---- foam DENSITY: how solid/widespread the foam reads, driven by sea-state (wind/roughness) -------
            // The #101 soft threshold reads MILKY everywhere — accurate for calm/dissipating foam, wrong for a
            // building/rough sea that needs SOLID-white density. This returns an effective density 0..1 from the
            // master _FoamDensity lifted by wind (_Roughness × _FoamDensityWind): CALM => low (milky), ROUGH =>
            // high (solid, widespread). The caller uses it to (a) lift the solid-core opacity and (b) widen the
            // solid zone. So the owner's "milky for some conditions, dense for others" tracks the weather for free.
            float FoamDensity()
            {
                return saturate(_FoamDensity + _Roughness * _FoamDensityWind);
            }

            // ---- dual-zone SOLID CORE: a dense solid-white heart with a soft milky edge --------------------------
            // Given a foam FIELD value and its soft threshold, returns a SOLID-CORE weight 0..1 that is 1 where the
            // field is WELL above threshold (the dense heart, full opacity, the painted solid white showing
            // through) and 0 near the threshold boundary (where the existing milky smoothstep still owns the look).
            // The solid level is _FoamSolidThreshold, but DENSITY pulls it DOWN toward the threshold as the sea
            // roughens, so a rough sea turns more of the field solid (denser, more widespread caps). col.rgb/col.a
            // dressing only — never depth/clip/_WaterLevel (P1 integrity, CLAUDE.md rule 5).
            float SolidCore(float field, float thr, float density)
            {
                float d = saturate(density);
                // solid level slides from _FoamSolidThreshold (calm: only the very brightest cores are solid)
                // DOWN toward just above the threshold (rough: most of the foam reads solid). Kept above `thr`
                // so the soft milky band between `thr` and the solid level never vanishes (dense heart + soft edge).
                float solidLvl = lerp(saturate(_FoamSolidThreshold), thr + 0.02, d);
                solidLvl = max(solidLvl, thr + 0.01);              // guard: solid level stays above the threshold
                return smoothstep(thr, solidLvl, field);
            }

            // ---- whitecap LIFECYCLE: form on the crest -> peak (dense) -> collapse to milky residual -------------
            // A natural wave lifecycle from the rolling-swell CREST factor (0..1; 1 = the breaking crest top).
            // Returns a DENSITY SCALE 0..1 the caller multiplies into the solid-core lift, so the cap is BORN dense
            // & solid on the breaking crest and AGES into milky residual as the crest passes:
            //   BREAK  — a sharp band at the crest top (_WhitecapFormSharpness narrows it) where foam newly breaks:
            //            full peak density (_WhitecapPeakDensity).
            //   COLLAPSE— away from the crest the cap ages: crest^_WhitecapCollapseRate falls off (faster = more
            //            milky residual off-crest), so troughs keep only a faint milky remnant (the soft mask
            //            survives there, but the SOLID lift fades — milky residual, exactly the dissipating look).
            // The downwind SPREAD of the residual is the existing _FoamStreakStretch (the cap coord is already
            // wind-streaked at the call site). col.rgb-only dressing — drives no sim/clip/_WaterLevel (P1, rule 5).
            float WhitecapLifecycle(float crest, float density)
            {
                float c = saturate(crest);
                // the breaking band at the very crest: _WhitecapFormSharpness (0..1) raises the band's lower edge
                // toward 1 so a higher value = a sharper, narrower break only at the crest top.
                float breakLo = lerp(0.0, 0.9, saturate(_WhitecapFormSharpness));
                float breakBand = smoothstep(breakLo, 1.0, c);
                // newborn dense peak on the break band, scaled by the live density (rough seas break denser).
                float newborn = breakBand * saturate(_WhitecapPeakDensity) * saturate(density);
                // aged milky residual everywhere the crest is non-zero, decaying away from the peak.
                float aged = pow(c, max(_WhitecapCollapseRate, 0.05));
                // the cap is born dense on the crest, aging into milky residual — take the stronger of the two.
                return saturate(max(newborn, aged * saturate(density)));
            }

            // Painted-texture UV: pixelize the world position to the PPU grid, then scale to tiles/unit.
            // Repeat wrap (set in the texture import) makes a seamless ~64px tile cover the whole plane;
            // the pixelize keeps the sampled coord on the grid so painted detail reads as pixel art too.
            // `scroll` lets a layer drift the pattern with the current (pass float2(0,0) for a static tile).
            float2 PaintUV(float2 worldXY, float scale, float2 scroll)
            {
                return Pixelize(worldXY + scroll) * max(scale, 1e-4);
            }

            // ---- IQ-style texture UNTILING (hide the repeat grid that reads at CALM) -------------------------
            // The painted slots are small seamless tiles on Repeat wrap, so at a glassy sea-state (no chop/flow
            // motion to mask it) the tile boundary reads as an obvious grid. This breaks it up two ways, both
            // dialed by _UntileStrength (0 = raw tiling, 1 = full break-up):
            //   1) DOMAIN WARP — nudge the sample UV by the low-freq surface ValueNoise so straight tile seams
            //      bend before they're sampled (cheap, smooth).
            //   2) HASH-UNTILE — per repeat-cell, offset the lookup by a cell hash, then blend two neighbouring
            //      offset variants by a smooth weight so adjacent cells differ yet never seam.
            // PIXEL-ART faithful: the offset is added to the WORLD coord BEFORE PaintUV pixelizes, so the
            // untiled lookup still snaps to the PPU grid and stays point-sampled. Pass scroll for the drift.
            half4 UntileSampleW(TEXTURE2D_PARAM(tex, smp), float2 worldXY, float scale, float2 scroll, float strength)
            {
                float s = saturate(strength);
                // (1) domain warp: a small world-space nudge from the surface noise, scaled by strength.
                // Two warp octaves (low-freq bend + a finer ripple) read more organic than one straight nudge;
                // both still dialed by _UntileStrength (no new knob) so 0 strength = the raw grid unchanged.
                float2 warpLo = float2(ValueNoise(worldXY * _NoiseScale * 0.5 + 3.1),
                                       ValueNoise(worldXY * _NoiseScale * 0.5 + 8.7)) - 0.5;
                float2 warpHi = float2(ValueNoise(worldXY * _NoiseScale * 1.7 + 17.3),
                                       ValueNoise(worldXY * _NoiseScale * 1.7 + 42.9)) - 0.5;
                float2 warpN = warpLo + warpHi * 0.4;
                float2 warped = worldXY + warpN * (s * 1.5);

                half4 raw = SAMPLE_TEXTURE2D(tex, smp, PaintUV(warped, scale, scroll));
                if (s <= 0.001) return raw;

                // (2) hash-untile in TILE space (uv = warped*scale; one repeat-cell == 1 unit of uv).
                float2 uv  = Pixelize(warped + scroll) * max(scale, 1e-4);
                float2 iuv = floor(uv);
                float2 fuv = frac(uv);
                // two candidate cell offsets (this cell + the diagonally-adjacent cell) so the blend never
                // shows a seam: each is hashed to a per-cell translation; world-space so PaintUV still snaps.
                float2 offA = Hash22(iuv)            * 64.0;          // a few tiles of world translation
                float2 offB = Hash22(iuv + 1.0)      * 64.0;
                half4 a = SAMPLE_TEXTURE2D(tex, smp, PaintUV(warped + offA, scale, scroll));
                half4 b = SAMPLE_TEXTURE2D(tex, smp, PaintUV(warped + offB, scale, scroll));
                // smooth blend weight across the cell so neighbours cross-fade (no hard tile edge).
                float w = smoothstep(0.2, 0.8, fuv.x) * 0.5 + smoothstep(0.2, 0.8, fuv.y) * 0.5;
                half4 untiled = lerp(a, b, w);
                // dial raw(+warp) <-> untiled by strength.
                return lerp(raw, untiled, s);
            }

            // ---- ALWAYS-ON beach swash (cosmetic waterline wash; foam band ONLY) ----------------------------
            // A fast sine on _Time that makes the wet edge advance & recede CONTINUOUSLY, independent of the
            // slow deterministic tide — the "waves crashing in and out" the procedural foam alone didn't have.
            // Returns a signed DEPTH OFFSET (metres): + pulls the wet edge inshore (advances), - pushes it
            // back. The caller GATES it to the depth~0 foam band and applies it to a LOCAL foam-only depth, so
            // it NEVER touches the real `depth` that drives clip()/the deep tint/the caustic gate, NEVER moves
            // the gameplay waterline, and saves nothing (the P1 integrity rule, CLAUDE.md rule 5). Visual-only.
            // Along-shore variation (_SwashScale over world X+Y) keeps the wash from pulsing as one flat line.
            float BeachSwash(float2 worldXY, float t)
            {
                float alongShore = (worldXY.x + worldXY.y) * _SwashScale;
                // two beats slightly out of phase read as overlapping run-up/backwash, not a metronome.
                float wave = sin(t * _SwashSpeed * 6.2831853 + alongShore) * 0.7
                           + sin(t * _SwashSpeed * 6.2831853 * 0.5 + alongShore * 1.7) * 0.3;
                return wave * _SwashAmplitude;
            }

            // ---- REFLECTION sea-state response: how STRONG + how SHARP at this sea-state ---------------------
            // Twins of WaterReflection.ReflectionStrength / ReflectionSharpness (the headless determinism guard).
            // Both read the already-pushed sea-state uniforms — _Chop (0 glass .. 1 storm; WaterSurface sets it
            // from the sea-state) and _Roughness (the wind whitecap scalar) — so there is NO new C# uniform push.
            //
            // ReflectionStrength: 1 on glassy/CALM water, fading to 0 by _ReflectionFadeChop (a storm doesn't
            //   mirror), further dimmed by wind whitecaps (_ReflectionWindFade), scaled by the master dial.
            // ReflectionSharpness: 1 = a clean mirror at CALM, falling toward 0 (smeared/scattered) as chop +
            //   wind rise (the reflection breaks up across the chop). The caller widens the smear by 1/sharpness.
            // Both col.rgb-only — they only shape the additive reflection, never depth/clip/_WaterLevel (P1).
            float ReflectionStrength()
            {
                float fade = max(_ReflectionFadeChop, 1e-3);
                float chopFalloff = 1.0 - smoothstep(0.0, fade, max(_Chop, 0.0));   // 1 at glass -> 0 by fadeChop
                float windDim = 1.0 - saturate(_Roughness) * saturate(_ReflectionWindFade);
                return saturate(_ReflectionStrength) * chopFalloff * windDim;
            }
            float ReflectionSharpness()
            {
                float agitation = max(_Chop, 0.0) * max(_ReflectionChopScatter, 0.0)
                                + saturate(_Roughness) * max(_ReflectionWindScatter, 0.0);
                return saturate(1.0 - agitation);
            }

            // ---- the FAKED sky reflection (single-pass, in-shader; col.rgb dressing ONLY) --------------------
            // Returns an additive RGB contribution: a clean mirror-like sheen on CALM water that reflects the
            // CURRENT SKY (the day/night _DayNightTint) as a vertical-ish smear, plus a brighter SUN STREAK /
            // glitter sitting toward the global sun (_SunDir), the whole thing fading + smearing as the sea
            // roughens (strength/sharpness above). NO reflection camera / extra pass: the "reflection" is the
            // sky colour stamped down the surface as a stylized vertical band — the pixel-art cue for a mirror.
            //   worldXY   — pixel-snapped world position (for the smear band + glitter noise; pixelized inside).
            //   surf      — the layer-2 surface noise (0..1) so the reflection ripples WITH the swell at calm.
            //   swellCrest— the rolling-swell crest factor (0..1) so the mirror brightens on the lit swell faces.
            //   t         — _Time.y (the glitter twinkles).
            // Everything here is pixelized (pixel-art faithful, §3) and additive to col.rgb (P1, rule 5).
            float3 SkyReflection(float2 worldXY, float surf, float swellCrest, float t)
            {
                float strength = ReflectionStrength();
                if (strength <= 0.001)
                    return float3(0, 0, 0);                 // master 0 / storm => no reflection (today's look)
                float sharp = ReflectionSharpness();        // 1 = mirror, 0 = smeared

                // (1) the reflected SKY colour: the current day/night sky (_DayNightTint) when the cycle runs,
                // else the material's authored _ReflectionColor. _ReflectionSkyTint dials how much of the live
                // sky vs the base colour shows, so the mirror reads warm at dusk / dark at night / bright at noon.
                // The global defaults to (0,0,0,0) when the day/night controller is NOT running (e.g. a bare art
                // scene / editor preview); a near-zero sum therefore means "unset" -> fall back to _ReflectionColor
                // (NOT a black sky). This mirrors the specular's `_SunDir == 0` fallback convention above.
                float tintSum = _DayNightTint.r + _DayNightTint.g + _DayNightTint.b;
                bool cycleOn = tintSum > 1e-3;                                    // controller is pushing a real tint
                float3 sky = cycleOn ? lerp(_ReflectionColor.rgb, _DayNightTint.rgb, saturate(_ReflectionSkyTint))
                                     : _ReflectionColor.rgb;

                // (2) the vertical-ish SMEAR: a stylized mirror stamps the sky DOWN the surface as a soft band.
                // A SHARP (calm) reflection is a tight band; a smeared (rough) one is broad. We build a 0..1
                // band factor from the pixelized world-Y modulated by the surface ripple (so the mirror wavers
                // with the swell at calm) — widen it as sharpness drops so it scatters across the chop.
                float smearLen = max(_ReflectionSmear, 1e-3) * lerp(4.0, 1.0, sharp);   // soft => longer smear
                float2 pp = Pixelize(worldXY);
                // a slow vertical wander so the reflected band isn't a ruler-flat line (rides the surface noise).
                float bandPhase = (pp.y + (surf - 0.5) * smearLen) / smearLen;
                float band = 0.5 + 0.5 * sin(bandPhase * 6.2831853);                    // 0..1 vertical smear
                // sharpen the band toward a crisp mirror streak at calm; flatten (more uniform) when smeared.
                band = pow(saturate(band), lerp(0.4, 3.0, sharp));
                // the rolling swell's lit faces catch more sky (one body catching one sky), modest weight.
                float skyFace = lerp(0.8, 1.2, swellCrest);
                float3 reflectionRGB = sky * band * skyFace;

                // (3) the SUN STREAK / glitter: a BRIGHTER smear of broken glints STRETCHED along the sun
                // direction (the classic "path of light to the sun" on calm water), fading out as the sun sets
                // (_SunElevation) and as the sea roughens. Uses the same _SunDir the specular does.
                if (_ReflectionSunStreak > 0.001)
                {
                    float2 sunXY = dot(_SunDir.xy, _SunDir.xy) > 1e-6 ? _SunDir.xy : _LightDir.xy;
                    float2 sd = normalize(sunXY + float2(1e-4, 0));
                    float2 sperp = float2(-sd.y, sd.x);
                    // ANISOTROPIC glitter coord: keep the along-sun axis, COMPRESS the cross-sun axis by the
                    // streak sharpness so a round noise cell reads as a long thin glint ELONGATED toward the sun
                    // (a tight streak at calm; broadening as sharpness drops -> the glints scatter when choppy).
                    float streakSharp = max(_ReflectionSunSharp, 0.5) * lerp(0.15, 1.0, sharp);
                    float alongSun = dot(pp, sd);
                    float crossSun = dot(pp, sperp) * streakSharp;
                    float2 sunUV = float2(alongSun, crossSun) + float2(t * 0.5, -t * 0.3);  // drift -> it twinkles
                    // a sharp, sparse glint field: ridge two pixelized noise samples so only the bright lanes show.
                    float g1 = ValueNoise(Pixelize(sunUV * 0.7));
                    float g2 = ValueNoise(Pixelize(sunUV * 1.6 + 5.3));
                    float streak = pow(saturate(1.0 - abs(g1 - g2) * 2.0), max(_ReflectionSunSharp, 1.0));
                    // only when the sun is up (or the cycle is off, in which case _SunElevation is 0 -> treat as day).
                    float sunUp = cycleOn ? saturate(_SunElevation) : 1.0;
                    reflectionRGB += sky * streak * _ReflectionSunStreak * sunUp;
                }

                return reflectionRGB * strength;
            }

            // ---- the BOAT SPOTLIGHT term: light the WATER from WITHIN this shader (ADR 0016) -----------------
            // The boat's additive QUAD lights LAND, but the URP 2D renderer draws this water shader OVER the quad
            // regardless of sorting order — so the water lights ITSELF from the published globals (_BoatLight*).
            // For this water pixel's worldXY it computes the cone contribution (lamp->pixel within range + within
            // the cone half-angle, radial × angular falloff), scales it by the SAME night-gate the land cone uses
            // (off by day, full at deep night, off-by-dawn), and the caller ADDS it to col.rgb. Sorting-INDEPENDENT
            // by construction (it is part of the water's own fragment), so it cannot fail the way the quad did.
            // Mirrors LightMath.WaterConeTerm + LightMath.NightGate EXACTLY (the headless determinism twins).
            // col.rgb ONLY — it never touches depth/clip/_WaterLevel/the height read/the sim (P1 integrity, rule 5).
            //
            //   worldXY — this water pixel's world position (pixelized inside, so the pool of light reads pixel-art).
            float3 BoatLightTerm(float2 worldXY)
            {
                float intensity = _BoatLightParams.x;
                if (intensity <= 0.001)
                    return float3(0, 0, 0);                 // light off / not lighting water / no boat -> nothing

                float range    = max(_BoatLightParams.y, 1e-4);
                float cosHalf  = _BoatLightParams.z;
                float cosInner = max(_BoatLightParams.w, cosHalf + 1e-4);
                float edgeSoft = _BoatLightParams2.x;

                // pixel-snap the world position so the lit pool reads as pixel art like every other layer (§3).
                float2 p = Pixelize(worldXY);
                float2 toPixel = p - _BoatLightPos.xy;
                float dist = length(toPixel);
                if (dist >= range)
                    return float3(0, 0, 0);                 // beyond the throw -> dark

                // RADIAL falloff (mirrors LightMath.RadialFalloff): (1 - d)^power, power eased by edge softness.
                float nd = saturate(dist / range);
                float power = lerp(2.0, 0.6, saturate(edgeSoft));
                float radial = pow(saturate(1.0 - nd), power);

                // ANGULAR (cone) falloff in COSINE space (mirrors LightMath.ConeFalloffCos): on-axis = full, at
                // the half-angle = 0. At the lamp itself the direction is undefined -> treat as on-axis (the core).
                float2 ndir = dist > 1e-5 ? toPixel / dist : float2(0, 0);
                float2 bdir = normalize(_BoatLightDir.xy + float2(0, 1e-4));
                float cosAngle = dist > 1e-5 ? dot(ndir, bdir) : 1.0;
                float cone = smoothstep(cosHalf, cosInner, cosAngle);

                float shape = saturate(radial * cone);
                if (shape <= 0.0)
                    return float3(0, 0, 0);

                // NIGHT-GATE (mirrors LightMath.NightGate): off by day so the beam can't wash daylight water out,
                // full at deep night, off-by-dawn. Reads the SAME global day/night tint the land cone gates on, so
                // tuning the day/night cycle fades land + water together. When the cycle is NOT running the tint is
                // near-black (unset) -> use the cycle-off FALLBACK (default 1 = show, for tuning / the demo), the
                // same convention the reflection/palette layers use for an unset tint.
                float threshold = _BoatLightParams2.y;
                float softness  = _BoatLightParams2.z;
                float fallback  = _BoatLightParams2.w;
                float dnSum = _DayNightTint.r + _DayNightTint.g + _DayNightTint.b;
                float gate;
                if (dnSum > 1e-3)
                {
                    // Rec.601 luma inline (PaletteLuma is defined later in the file; HLSL needs define-before-use).
                    float tintLum = max(0.0, dot(_DayNightTint.rgb, float3(0.299, 0.587, 0.114)));
                    float darkness = saturate(1.0 - tintLum);
                    gate = smoothstep(saturate(threshold), saturate(threshold + max(softness, 1e-4)), darkness);
                }
                else
                {
                    gate = saturate(fallback);             // no cycle -> show (tuning / demo / edit-mode preview)
                }

                return _BoatLightColor.rgb * (intensity * shape * gate);
            }

            // ====================================================================================================
            // PALETTE GUARD-RAIL — the final soft colour-grade stage (ADR 0015). Mirrors WaterPaletteGrade.cs
            // exactly (the headless determinism twin). Everything here is col.rgb-ONLY: it bounds + nudges the
            // composited colour and NEVER touches depth/clip/_WaterLevel/the height read/the sim (P1 integrity,
            // CLAUDE.md rule 5). _PaletteGradeStrength = 0 is an EXACT passthrough (today's look).
            // ====================================================================================================

            // Rec.601 luma — the SAME weights the painted-foam luminance fallback uses, so look stays consistent.
            float PaletteLuma(float3 rgb) { return dot(rgb, float3(0.299, 0.587, 0.114)); }

            // DAY/NIGHT-AWARE value floor: pre-compensate for the day/night overlay's downstream MULTIPLY so the
            // ON-SCREEN water lands at ~paletteFloor in daylight, yet true night still goes genuinely dark. The
            // overlay multiplies the whole frame by _DayNightTint AFTER the water renders (ADR 0013), so we floor
            // the water's PRE-overlay value at min(1, paletteFloor / dayNightLuma): in daylight dnLuma~1 => ~floor;
            // at deep night dnLuma is small => the quotient saturates at 1 (water full-bright pre-overlay) and the
            // overlay still darkens it to genuine dark. nightFloor (on-screen) optionally keeps a faint night sea.
            float PaletteValueFloorDayNight(float paletteFloor, float dayNightLuma, float nightFloor)
            {
                float dn = max(dayNightLuma, 1e-3);
                float dayPre   = min(1.0, max(paletteFloor, 0.0) / dn);
                float nightPre = min(1.0, max(nightFloor, 0.0) / dn);
                return min(1.0, max(dayPre, nightPre));
            }

            // Re-scale rgb so its luminance moves to `toLuma` while keeping hue/chroma ratios (multiplicative,
            // not a desaturating lerp). A (near) black pixel is lifted to a neutral grey of the target luma.
            float3 PaletteScaleToLuma(float3 rgb, float fromLuma, float toLuma)
            {
                if (fromLuma <= 1e-4) return float3(toLuma, toLuma, toLuma);
                return rgb * (toLuma / fromLuma);
            }

            // HSV-style saturation: (max - min) / max, 0 for black.
            float PaletteSaturation(float3 rgb)
            {
                float mx = max(rgb.r, max(rgb.g, rgb.b));
                float mn = min(rgb.r, min(rgb.g, rgb.b));
                return mx <= 1e-5 ? 0.0 : (mx - mn) / mx;
            }

            // Cap saturation at `satCap`: pull every channel toward the colour's own grey (its luminance) by
            // exactly the amount that lands the resulting HSV-style saturation ON the cap (closed form, so the
            // cap is EXACT, not approximate). Pulling toward the LUMINANCE preserves perceived brightness — the
            // cap desaturates without darkening. Mirrors WaterPaletteGrade.CapSaturation.
            float3 PaletteCapSaturation(float3 rgb, float satCap)
            {
                float cap = saturate(satCap);
                float mx = max(rgb.r, max(rgb.g, rgb.b));
                float mn = min(rgb.r, min(rgb.g, rgb.b));
                float chroma = mx - mn;
                float sat = mx <= 1e-5 ? 0.0 : chroma / mx;
                if (sat <= cap || sat <= 1e-5) return rgb;
                float grey = PaletteLuma(rgb);
                // f solves newSat == cap: f = (chroma - cap*mx) / (chroma - cap*(mx - grey)).
                float denom = chroma - cap * (mx - grey);
                float f = abs(denom) < 1e-6 ? 1.0 : saturate((chroma - cap * mx) / denom);
                return lerp(rgb, float3(grey, grey, grey), f);
            }

            // Pick the palette ANCHOR to pull toward, by luminance: darkest -> deep, then mid, shallow, foam.
            // A piecewise-linear blend across the four anchors (continuous, no banding); breakpoints are the
            // anchors' own luminances, forced strictly increasing so the lerps are stable.
            float3 PaletteAnchorForLuma(float luma)
            {
                float lDeep    = PaletteLuma(_PaletteDeep.rgb);
                float lMid     = max(PaletteLuma(_PaletteMid.rgb),     lDeep + 1e-3);
                float lShallow = max(PaletteLuma(_PaletteShallow.rgb), lMid  + 1e-3);
                float lFoam    = max(PaletteLuma(_PaletteFoam.rgb),    lShallow + 1e-3);
                if (luma <= lDeep)    return _PaletteDeep.rgb;
                if (luma <  lMid)     return lerp(_PaletteDeep.rgb,    _PaletteMid.rgb,     (luma - lDeep)    / (lMid - lDeep));
                if (luma <  lShallow) return lerp(_PaletteMid.rgb,     _PaletteShallow.rgb, (luma - lMid)     / (lShallow - lMid));
                if (luma <  lFoam)    return lerp(_PaletteShallow.rgb, _PaletteFoam.rgb,    (luma - lShallow) / (lFoam - lShallow));
                return _PaletteFoam.rgb;
            }

            // The full soft palette guard-rail: value clamp (day/night-aware floor + ceiling) -> sat cap ->
            // anchor pull, the whole thing lerped back toward the raw colour by the master strength so 0 = today.
            // dayNightLuma is the luminance of the day/night multiply tint (1 = full daylight; the global falls
            // back to (1,1,1,1) when the cycle is not running -> dnLuma 1 -> the daylight rail, never a dark one).
            float3 PaletteGrade(float3 rgb, float dayNightLuma)
            {
                float strength = saturate(_PaletteGradeStrength);
                if (strength <= 0.0) return rgb;           // EXACT passthrough — opt-in, revertible (rule 6)

                float3 graded = rgb;

                // (1) VALUE clamp: day/night-aware floor + ceiling (no mud, no blowout).
                float luma = PaletteLuma(graded);
                float floorPre = PaletteValueFloorDayNight(_PaletteValueFloor, dayNightLuma, _PaletteNightFloor);
                // NOTE: not named `ceil` — that shadows the HLSL ceil() intrinsic (a magenta-class trap).
                float ceilLvl = max(_PaletteValueCeil, floorPre);       // ceiling never below the floor
                float targetLuma = clamp(luma, floorPre, ceilLvl);
                graded = PaletteScaleToLuma(graded, luma, targetLuma);

                // (2) SATURATION cap.
                graded = PaletteCapSaturation(graded, _PaletteSatCap);

                // (3) ANCHOR pull (soft, by luminance).
                float3 anchor = PaletteAnchorForLuma(PaletteLuma(graded));
                graded = lerp(graded, anchor, saturate(_PalettePullStrength));

                // master strength: lerp the whole grade back toward the raw colour (the soft rail).
                return lerp(rgb, graded, strength);
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
                VertexPositionInputs pos = GetVertexPositionInputs(IN.positionOS);
                OUT.positionCS = pos.positionCS;
                OUT.uv = IN.uv;
                OUT.worldXY = pos.positionWS.xy;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                float t = _Time.y;
                float2 worldXY = IN.worldXY;

                // ---- layer 2 surface (computed first; warps the coords every other layer reads) -------------
                float surf = SurfaceNoise(worldXY, t);             // 0..1
            #if defined(_USE_SURFACETEX)
                // Painted ripple/detail (grayscale) scrolled with the current; blend over the procedural
                // noise. At strength 1 it fully replaces the procedural surface; at 0 it's pure procedural.
                float2 sScroll = normalize(_FlowDir.xy + float2(1e-4, 0)) * (_Flow * t);
                // untile so the painted ripple's repeat grid stops reading at CALM (the headline fix).
                float surfTex = UntileSampleW(TEXTURE2D_ARGS(_SurfaceTex, sampler_SurfaceTex),
                                    worldXY, _PaintScale, sScroll, _UntileStrength).r;
                surf = lerp(surf, surfTex, _SurfaceTexStrength);
            #endif
                float swell = (surf - 0.5) * 2.0;                  // -1..1
                // chop pushes a small world-space warp into the depth read so the waterline shimmers with swell
                float2 warp = float2(swell, ValueNoise(Pixelize(worldXY * _NoiseScale + 7.3)) - 0.5)
                              * _Chop * 0.5;

                // ---- layer 1 depth gradient -------------------------------------------------------------------
                float elevation = SeabedElevation(worldXY + warp);
                float depth = _WaterLevel - elevation;             // metres; <= 0 means dry/exposed

                // Dry ground: the shader hands off to the terrain tiles below (draw nothing).
                clip(depth + 1e-4);

                float dt = saturate((depth - _ShallowDepth) / max(_DeepDepth - _ShallowDepth, 1e-3));
                // Posterize the depth ramp into N bands for the pixel read (0 bands = smooth).
                if (_DepthBands >= 1.0)
                    dt = floor(dt * _DepthBands + 0.5) / _DepthBands;
            #if defined(_USE_DEPTHRAMP)
                // Owner hand-paints the exact shallow->deep colours in a 1D ramp (shallow at u=0). When
                // assigned this REPLACES the _ShallowColor/_DeepColor lerp; alpha comes from the ramp too.
                // v=0.5 stays mid-texel on a 1px-tall ramp (Repeat wrap; clamp-equivalent for the single row).
                half4 col = SAMPLE_TEXTURE2D(_DepthRamp, sampler_DepthRamp, float2(dt, 0.5));
            #else
                half4 col = lerp(_ShallowColor, _DeepColor, dt);
            #endif

                // Tint the base by the surface so the swell is visible even in flat light.
                col.rgb += swell * _SurfaceTint * 0.15;

                // ---- FBM low-frequency variance (organic patches; col.rgb ONLY — never touches depth) --------
                // One big-scale, slowly-drifting fractal field, reused below to gate the specular. It softly
                // tints the base so the sea breaks into broad slow patches instead of an even sheet — purely
                // cosmetic (col.rgb), so it cannot move the waterline/clip/deep-tint (P1 integrity, rule 5).
                float2 fbmDrift = float2(t * _FbmDriftSpeed, -t * _FbmDriftSpeed * 0.8);
                float fbm = Fbm((worldXY + fbmDrift) * _FbmScale);   // 0..1 (FBM_OCTAVES octaves)
                if (_FbmStrength > 0.001)
                {
                    // signed around the patch midpoint so some areas lift toward the tint, others sit back.
                    float fbmSigned = (fbm - 0.5) * 2.0;               // -1..1
                    col.rgb = lerp(col.rgb, _FbmTint.rgb, saturate(fbmSigned) * _FbmStrength);
                    col.rgb += fbmSigned * _FbmStrength * 0.06;        // gentle brightness wobble
                }

                // ---- ROLLING OCEAN SWELL (the cohesion keystone; col.rgb brightness ONLY) ---------------------
                // ONE big, long-wavelength swell field rolling slowly across the WHOLE surface. It lightens the
                // crests and darkens the troughs so broad light/dark BANDS read as one connected body, with the
                // small variance (above) riding on top. Computed once here and REUSED below to ride the
                // whitecaps on the crests and bias the specular. col.rgb-only — it never touches depth/clip/the
                // deep tint/the caustic gate/_WaterLevel, so the cohesion cannot move the gameplay waterline
                // (P1 integrity, CLAUDE.md rule 5). Direction comes from the (wandering) wind, so the bands
                // reorient as the weather shifts.
                float swellCrest = SwellField(worldXY, depth, t);     // 0..1: 1 on crests, 0 in troughs (rolls IN near shore)
                if (_OceanSwellStrength > 0.001)
                {
                    // signed around the midpoint: crests brighten, troughs darken, by the amplitude.
                    float swellSigned = (swellCrest - 0.5) * 2.0;     // -1..1
                    col.rgb += swellSigned * _OceanSwellStrength * 0.25;
                }

                // ---- layer 5 caustics (shallows only; under the foam/spec so it reads as the seabed) ----------
                float causticGate = 1.0 - saturate(depth / max(_CausticDepth, 1e-3));   // 1 shallow -> 0 deep
                if (causticGate > 0.001 && _CausticAmount > 0.001)
                {
                    float2 cp = Pixelize(worldXY * _CausticScale + float2(t * _Flow, -t * _Flow * 0.7));
                    float ca = ValueNoise(cp);
                    float cb = ValueNoise(cp * 1.7 + 11.1);
                    float caustic = pow(saturate(1.0 - abs(ca - cb) * 3.0), 2.0);   // ridged -> bright veins
                #if defined(_USE_CAUSTICTEX)
                    // Painted caustics (grayscale), distorted by time, blended over the procedural veins;
                    // still depth-gated to the shallows by causticGate. Two counter-scrolling samples mul
                    // to a moving ripple so a static tile still "swims".
                    float2 cScroll = float2(t * _Flow * 0.6, -t * _Flow * 0.4);
                    float ct = UntileSampleW(TEXTURE2D_ARGS(_CausticTex, sampler_CausticTex),
                                   worldXY, _PaintScale * 2.0, cScroll, _UntileStrength).r
                             * UntileSampleW(TEXTURE2D_ARGS(_CausticTex, sampler_CausticTex),
                                   worldXY, _PaintScale * 2.0, -cScroll * 1.3, _UntileStrength).r;
                    caustic = lerp(caustic, ct * 2.0, _CausticTexStrength);   // *2: counter-mul darkens, restore range
                #endif
                    col.rgb += _CausticColor.rgb * caustic * _CausticAmount * causticGate;
                }

                // ---- layer 4 specular glints (implied single sun; pixelized so it sparkles, not smears) -------
                if (_SpecAmount > 0.001)
                {
                    // Prefer the LIVE day/night sun (_SunDir, pushed by DayNightController) so the glints face
                    // the same sun that casts the shadows; fall back to the material's authored _LightDir when
                    // the cycle is not running (_SunDir == 0). ADR 0013.
                    float2 sunXY = dot(_SunDir.xy, _SunDir.xy) > 1e-6 ? _SunDir.xy : _LightDir.xy;
                    float2 ld = normalize(sunXY + float2(1e-4, 0));
                    // a cheap surface "normal tilt" from the noise gradient, facing the implied light
                    float2 gp = Pixelize(worldXY * _NoiseScale);
                    float nx = ValueNoise(gp + float2(0.05, 0)) - ValueNoise(gp - float2(0.05, 0));
                    float ny = ValueNoise(gp + float2(0, 0.05)) - ValueNoise(gp - float2(0, 0.05));
                    float facing = saturate(dot(normalize(float2(nx, ny) + 1e-4), ld) * 0.5 + 0.5);
                    float glint = pow(facing, max(_SpecSharpness, 1.0));
                    // posterize into _SpecBands steps -> pixel sparkles (tunable band count, was a hard 4).
                    float bands = max(_SpecBands, 1.0);
                    glint = floor(glint * bands + 0.5) / bands;
                #if defined(_USE_SPARKLETEX)
                    // Painted glint pattern (white-on-black), drifted with the current and still gated by
                    // `facing` so sparkles only land where the implied sun hits (one-sun discipline, ADR 0006).
                    float2 kScroll = normalize(_FlowDir.xy + float2(1e-4, 0)) * (_Flow * t * 0.5);
                    float sparkle = UntileSampleW(TEXTURE2D_ARGS(_SparkleTex, sampler_SparkleTex),
                                        worldXY, _SparkleTexScale, kScroll, _UntileStrength).r * facing;
                    glint = lerp(glint, sparkle, _SparkleTexStrength);
                #endif
                    // FBM SCATTER: gate the glint by the low-freq field so sparkles CLUSTER in patches
                    // organically instead of an even grid (the marching-grid fix for the highlights). The
                    // gate is BEFORE the additive so it only thins col.rgb's sparkle — never the depth/clip.
                    float specGate = smoothstep(_FbmGateLo, _FbmGateHi, fbm);
                    // SWELL-FACE BIAS: lean the sparkle toward the lit faces of the rolling swell so the glints
                    // ride the same bands as the cohesion brightness (one body catching one sun). The swell
                    // crest factor stands in for "this face rises toward the light"; _SpecSwellBias dials how
                    // much (0 = even across the swell, 1 = crest-led). col.rgb-only, like every spec term.
                    float swellSpec = lerp(1.0, swellCrest, saturate(_SpecSwellBias));
                    col.rgb += _SpecColor.rgb * glint * _SpecAmount * specGate * swellSpec;
                }

                // ---- SKY REFLECTIONS (sea-state-driven; col.rgb-only dressing) ---------------------------------
                // A faked, single-pass mirror sheen: STRONG + SHARP on glassy/CALM water (reflects the current
                // day/night sky + a sun streak as a vertical smear), breaking up and FADING toward NONE as the
                // sea-state (_Chop) rises (a storm doesn't mirror), wind (_Roughness) dimming/scattering it
                // further. Added AFTER caustics + specular (so the mirror sits over them) but BEFORE the foam
                // (so whitecaps/fringe read on top of the reflection). col.rgb ONLY — it never touches depth/
                // clip()/the deep tint/the caustic gate/_WaterLevel (P1 integrity, CLAUDE.md rule 5). The whole
                // layer dials to nothing with _ReflectionStrength = 0 (today's look). See SkyReflection() above.
                col.rgb += SkyReflection(worldXY, surf, swellCrest, t);

                // ---- layer 3 foam fringe (depth ~ 0 band that hugs the moving waterline) ----------------------
                // ALWAYS-ON swash: a cosmetic, _Time-driven depth offset that advances/recedes the wet edge.
                // GATED to the depth~0 band (full at the wet edge, 0 by ~2x the foam width) and applied ONLY
                // to a LOCAL foam-only depth — the real `depth` (clip/dt/caustics) is never touched, so deep
                // water and the gameplay waterline don't move. Pure foam dressing (P1 integrity, rule 5).
                float swashReach = max(_FoamWidth, 1e-3) * 2.0 + max(abs(_SwashAmplitude), 1e-3);
                float swashGate  = 1.0 - smoothstep(0.0, swashReach, depth);   // 1 at the wet edge -> 0 deeper
                float foamDepth  = depth - BeachSwash(worldXY, t) * swashGate;  // local, foam-only
                // smoothstep across a thin band just inside the water: 1 at the wet edge -> 0 by foamWidth deep.
                float foamEdge = 1.0 - smoothstep(0.0, max(_FoamWidth, 1e-3), foamDepth);
                if (foamEdge > 0.001)
                {
                    // FOAM FLOWS WITH THE BODY: the churn drifts along FoamDriftDir() — a blend of the wind and
                    // the tidal current (both sim-driven, both wandering) — NOT the old fixed counter-diagonal
                    // float2(-t*_Flow, t*_Flow) that scrolled AGAINST the surface. So the foam moves with the
                    // one connected surface and reorients as the weather shifts.
                    float2 foamDrift = FoamDriftDir(worldXY, depth) * (_Flow * t);
                    // LIVING foam: the churn is now an EVOLVING field (boils in place) instead of one ValueNoise
                    // that only slid rigidly — so the fringe foam shapes MORPH (appear/grow/shrink/vanish) while
                    // still drifting with the body. _FoamBlobScale sizes the blobs; _FoamEvolveSpeed the boil rate.
                    float churn = EvolvingField(worldXY, foamDrift, _FoamNoise * _FoamBlobScale * 0.5,
                                                _FoamEvolveSpeed, t);
                #if defined(_USE_FOAMTEX)
                    // Painted foam pattern (white-on-transparent) scrolled WITH the body (same FoamDriftDir as
                    // the churn); its coverage (alpha, falling back to luminance for an opaque tile) replaces the
                    // procedural churn so the owner's foam shape breaks the line. Still masked to the band.
                    float2 fScroll = foamDrift;
                    half4 foamSample = UntileSampleW(TEXTURE2D_ARGS(_FoamTex, sampler_FoamTex),
                                           worldXY, _PaintScale, fScroll, _UntileStrength);
                    float foamPat = max(foamSample.a, dot(foamSample.rgb, float3(0.299, 0.587, 0.114)));
                    churn = lerp(churn, foamPat, _FoamTexStrength);
                #endif
                    // SOFT-THRESHOLD (metaball merge/separate): build the foam from a smoothstep around a
                    // threshold on the evolving field, NOT a hard step. Wind roughness + the depth-band edge LIFT
                    // the field (more foam reaches in when it's rough / right at the wet edge) so the threshold is
                    // crossed by more of the field there. As two field maxima grow toward each other the valley
                    // between them rises above (thr - soft) and the blobs MERGE; when the field dips below they
                    // SEPARATE — organic, in-place, not a sliding stamp. col.a only blends the foam (P1, rule 5).
                    float foamField = saturate(churn + foamEdge * 0.5 + _Roughness * 0.4);
                    float thr  = saturate(_FoamThreshold);
                    float soft = max(_FoamThresholdSoft, 1e-3);
                    float bandGate = saturate(foamEdge + _Roughness * 0.4);
                    // the MILKY soft mask (the #101 metaball look): partial coverage across the soft band — kept
                    // as the LIGHT/dissipating end (the soft edge of every blob).
                    float milky = smoothstep(thr - soft, thr + soft, foamField);
                    // DUAL-ZONE: a SOLID-WHITE CORE where the field is WELL above threshold lifts the coverage to
                    // FULL (the painted solid-white _FoamTex shows through at the heart), leaving the milky band
                    // only near the boundary. Density (driven by sea-state) sets how strongly the core lifts and
                    // how wide the solid zone is: CALM => barely lifted (milky), ROUGH => a dense solid heart.
                    float dens = FoamDensity();
                    float core = SolidCore(foamField, thr, dens);
                    float foamCoverage = lerp(milky, 1.0, core) * bandGate;
                    col.rgb = lerp(col.rgb, _FoamColor.rgb, foamCoverage * _FoamColor.a);
                    col.a = max(col.a, foamCoverage * _FoamColor.a);
                }

                // Whitecaps out on open water when it's rough (wind-driven). WIND-STREAKED + swell-coupled:
                // the speckle is sampled on a coord COMPRESSED perpendicular to the wind, so features
                // ELONGATE into long thin streaks ALONG the wind (wind rows) instead of round speckle; it
                // drifts WITH the body (the foam drift blend, not a counter-scroll); and it is preferentially
                // placed on the swell CRESTS so the foam rides the rolling swell. All col.rgb-only dressing.
                if (_Roughness > 0.01)
                {
                    // wind-aligned anisotropic basis: keep the along-wind axis, COMPRESS the cross-wind axis
                    // by _FoamStreakStretch so a round noise cell reads as a streak stretched down the wind.
                    float2 wdir   = normalize(_WindDir.xy + float2(0, 1e-4));
                    float2 wperp  = float2(-wdir.y, wdir.x);
                    float2 capDrift = FoamDriftDir(worldXY, depth) * (_Flow * t);
                    float2 wp = worldXY + capDrift;
                    float stretch = max(_FoamStreakStretch, 1.0);
                    // project onto the wind basis: along-wind unchanged, cross-wind multiplied (compressed UV).
                    // The drift is folded into wp here, so the field both EVOLVES and travels along the wind/current.
                    float2 aniso = float2(dot(wp, wdir), dot(wp, wperp) * stretch);
                    // LIVING whitecaps: the cap field now EVOLVES IN PLACE (the boil) instead of one ValueNoise
                    // that only TRANSLATED — that fixed-shape sliding stamp was exactly the "repeating pattern /
                    // shapes never change" the owner saw. Built on the wind-streaked aniso coord so the streaks
                    // are preserved while the whitecaps morph (appear/grow/drift/shrink/vanish). drift=0 here
                    // because it is already baked into wp above (avoids double-drifting the coord).
                    float cap = EvolvingField(aniso, float2(0, 0), _NoiseScale * 3.0 * _FoamBlobScale,
                                              _FoamEvolveSpeed, t);
                    // SOFT-THRESHOLD (metaball merge/separate) instead of the hard step(): smoothstep around a
                    // threshold lowered by wind (rougher => more sea is above the threshold => more caps). As the
                    // evolving field's maxima rise toward each other the valley crosses (thr - soft) and caps
                    // MERGE; when it dips they SEPARATE and fade — organic whitecaps, not a sliding speckle grid.
                    float capThr  = saturate(_FoamThreshold - _Roughness * 0.25);
                    float capSoft = max(_FoamThresholdSoft, 1e-3);
                    // the MILKY soft coverage (the #101 metaball look): the merge/separate band, kept as the
                    // light/dissipating end. The SOLID core (below) lifts it to dense white on the breaking crest.
                    float capMilky = smoothstep(capThr - capSoft, capThr + capSoft, cap);
                    float capMask = capMilky * saturate(dt);  // deeper water
                #if defined(_USE_WHITECAPTEX)
                    // Painted whitecap pattern (white-on-transparent) drifted WITH the body (the foam drift
                    // blend, not a fixed current scroll); coverage SCALES BY ROUGHNESS (the wind uniform) so
                    // caps appear/intensify with wind, gated to deeper water by dt. Blends over the speckle.
                    // Routed through UntileSampleW (like the other painted slots) so the small seamless tile's
                    // REPEAT GRID stops reading — the painted-texture culprit behind any residual tiling the
                    // owner still sees — dialed by _UntileStrength, kept pixel-snapped (PaintUV inside).
                    half4 capSample = UntileSampleW(TEXTURE2D_ARGS(_WhitecapTex, sampler_WhitecapTex),
                                          worldXY, _PaintScale, capDrift, _UntileStrength);
                    float capPat = max(capSample.a, dot(capSample.rgb, float3(0.299, 0.587, 0.114)));
                    float capTexMask = capPat * saturate(_Roughness) * saturate(dt);
                    capMask = lerp(capMask, capTexMask, _WhitecapTexStrength);
                #endif
                    // SWELL-CREST GATE: lift the caps toward the swell crests so the foam rides the rolling
                    // swell instead of speckling evenly. _FoamCrestGate dials it (0 = even, 1 = crest-only).
                    float crestGate = lerp(1.0, swellCrest, saturate(_FoamCrestGate));
                    capMask *= crestGate;
                    // ---- DUAL-ZONE DENSITY + WAVE LIFECYCLE (form -> peak -> collapse) ------------------------
                    // The cap was capped at a flat 0.6 opacity (always milky). Now: a SOLID-WHITE CORE where the
                    // cap field is WELL above threshold, lifted by sea-state DENSITY, and shaped by the wave
                    // LIFECYCLE off the swell crest — BORN dense & solid on the breaking crest, AGING into milky
                    // residual as the crest passes (the residual spreads downwind via the wind-streaked aniso
                    // coord above). col.rgb-only dressing — drives no depth/clip/_WaterLevel (P1, rule 5).
                    float capDens   = FoamDensity();
                    float capCore   = SolidCore(cap, capThr, capDens);            // 0..1: the dense solid heart
                    float life      = WhitecapLifecycle(swellCrest, capDens);     // form/peak/collapse density scale
                    // peak opacity ceiling for a NEWBORN cap (replaces the old hard 0.6); the milky residual sits
                    // below it. The solid core × the lifecycle drives the dense white; the milky coverage carries
                    // the soft, aged remnant so off-crest the cap reads thin/milky, not a hard speckle.
                    float capPeak   = saturate(_WhitecapPeakDensity);
                    float capSolid  = capCore * life * capPeak;                   // dense white on the breaking crest
                    float capMilkyOpacity = capMask * lerp(0.45, capPeak, capDens); // milky residual (scales gently with sea-state)
                    float capOpacity = saturate(max(capMilkyOpacity, capMask * capSolid));
                    col.rgb = lerp(col.rgb, _FoamColor.rgb, capOpacity);
                }

                // ---- BOAT SPOTLIGHT: light the WATER from WITHIN this shader (ADR 0016) -------------------------
                // The boat's additive QUAD lights LAND, but the URP 2D renderer draws this water OVER the quad
                // regardless of sorting order — so the water lights ITSELF here from the published _BoatLight*
                // globals. ADD the cone's night-gated illumination to col.rgb AFTER the foam/reflection (so the
                // beam reads over the surface dressing) but BEFORE the palette guard-rail (so the rail still
                // BOUNDS the lit pool — it can't blow out). Sorting-INDEPENDENT (part of the water's own frag), so
                // it cannot fail the way the quad did over water. col.rgb ONLY — it never touches depth/clip/
                // _WaterLevel/the height read/the sim (P1 integrity, CLAUDE.md rule 5). Off when no boat publishes
                // a beam (intensity 0) or in daylight (the night-gate). See BoatLightTerm() above.
                col.rgb += BoatLightTerm(worldXY);

                // ---- PALETTE GUARD-RAIL: the final soft grade (col.rgb ONLY; ADR 0015) -------------------------
                // The LAST thing before return: bound + gently pull the composited colour into the art-directed
                // palette so it never washes out or goes muddy, while keeping the dynamic diversity. The value
                // FLOOR is DAY/NIGHT-AWARE — it pre-compensates for the day/night overlay's downstream MULTIPLY
                // (ADR 0013) so daylight never goes muddy while true night still goes genuinely dark. dayNightLuma
                // is the luminance of the global _DayNightTint the overlay multiplies the frame by; when the cycle
                // is NOT running the global is near-black (the same "unset" convention the reflection/specular use)
                // -> treat it as full daylight (dnLuma = 1, the daylight rail) so a bare art scene / editor preview
                // grades to the daylight palette, never a phantom-dark one. col.rgb ONLY: this never touches depth/
                // clip()/_WaterLevel/the height read/the sim (P1 integrity, CLAUDE.md rule 5). Strength 0 = today.
                float dnSum = _DayNightTint.r + _DayNightTint.g + _DayNightTint.b;
                float dayNightLuma = (dnSum > 1e-3)
                    ? PaletteLuma(_DayNightTint.rgb)   // cycle running: the real multiply luminance (1 day .. ~0 night)
                    : 1.0;                             // cycle off / unset: full daylight rail (no phantom dark floor)
                col.rgb = PaletteGrade(col.rgb, dayNightLuma);

                return col;
            }
            ENDHLSL
        }
    }

    Fallback Off
}
