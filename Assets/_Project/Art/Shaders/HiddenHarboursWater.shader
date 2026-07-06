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
        _OceanSwellSharpness("Ocean swell crest sharpness (1 = round; higher = narrow crest over broad trough)", Float) = 2.2

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

        [Header(Shared wave field (ADR 0018   trains published by WaveFieldBridge))]
        // When HiddenHarbours.Art.WaveFieldBridge publishes live wave trains (the shared deterministic
        // wave field — the SAME field the seakeeping sim samples), the swell brightness + whitecap
        // lifecycle re-key onto the REAL ADVANCING CRESTS (see WaveFieldSample below). The legacy
        // noise-swell path stays intact behind the "no trains published" fallback (edit mode / a bare
        // art scene / cycle off), and the owner's tuned _OceanSwell* values map onto the field (§(6)
        // of the ADR): _OceanSwellStrength = the brightness amplitude (unchanged role),
        // _OceanSwellSharpness = the crest-shaping exponent on the 0..1 crest signal (unchanged role),
        // _OceanSwellScale = a VISUAL wavelength scale normalized to its shipped default 0.025 (0.025
        // renders the field's TRUE wavelengths; bigger = shorter waves, the knob's legacy sense).
        //   _WhitecapOnsetAmp — the total train amplitude (m) at which whitecaps reach FULL presence
        //       (first foam from ~10% of it). This is the sea-state coupling of the reworked caps:
        //       glass = zero amplitude = zero foam, automatically; a gale = full marching whitecaps.
        _WhitecapOnsetAmp ("Whitecap onset amplitude (m of total wave amplitude for full caps)", Float) = 0.5

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

        [Header(Sky CONTENT reflection (clouds   moon glitter   stars   day night driven))]
        // This is a three-quarter top-down game: the player never sees the sky directly, so the WATER's reflection is the
        // ONLY place the sky appears. On top of the sky-COLOUR mirror + sun glint above, this reflects SKY
        // CONTENT — drifting CLOUDS (day + night), the MOON with a shimmering vertical glitter PATH (night), and
        // faint STAR sparkle (night). All of it INHERITS the existing sea-state fade (strong on CALM/glassy
        // water, gone in chop/storm — a storm doesn't mirror) and rides the surface ripple; the moon/stars
        // additionally GATE ON by night (darkness from the day/night _DayNightTint), clouds read day and night.
        // The clouds DRIFT along the shared sim wind (_WindWorld, the SAME global the grass + water read) so the
        // sky moves cohesively with the scene. col.rgb ONLY: it ADDS to the colour like every other water layer
        // and NEVER touches depth/clip/the deep tint/the caustic gate/_WaterLevel (P1 integrity, CLAUDE.md
        // rule 5); _SkyReflectionStrength = 0 returns the exact pre-feature (sky-colour + sun) look.
        //   _SkyReflectionStrength — master for ALL sky-content (clouds + moon + stars); 0 = today's look.
        //   _CloudStrength/_CloudScale/_CloudDriftSpeed/_CloudSoftness/_CloudColor — the drifting cloud bands.
        //   _MoonStrength/_MoonSize/_MoonGlitter/_MoonGlitterLength/_MoonColor — the moon disc + glitter path.
        //   _StarStrength/_StarDensity/_StarTwinkleSpeed — the faint twinkling star sparkle (night).
        //   _NightStart/_NightSoftness — the darkness (from _DayNightTint) at which the moon/stars fade in.
        //   _SunGlitterStrength/_SunGlitterColor — the SUN glitter path (the moon column's golden-hour twin):
        //       a warm glitter column toward the LOW sun at dawn/dusk, gone by high noon and below the horizon
        //       (SunGlitterGate over _SunElevation). Shares the moon's geometry knobs (_MoonGlitterLength =
        //       reach, _MoonSize = column width basis) so the two paths stay visually consistent (rule 6).
        _SkyReflectionStrength ("Sky content reflection master (0 = off / today's look)", Range(0,1)) = 0.7
        _CloudStrength    ("Cloud reflection strength", Range(0,1)) = 0.5
        _CloudScale       ("Cloud reflection scale (small = bigger clouds)", Float) = 0.06
        _CloudDriftSpeed  ("Cloud drift speed (along the wind)", Float) = 0.06
        _CloudSoftness    ("Cloud edge softness (0 = crisp, 1 = wispy)", Range(0,1)) = 0.6
        _CloudColor       ("Cloud color (pale; tinted warm at dusk by the sky)", Color) = (0.86, 0.88, 0.92, 1.0)
        _MoonStrength     ("Moon reflection strength (night)", Range(0,2)) = 0.9
        _MoonSize         ("Moon reflected disc size (m)", Float) = 1.2
        _MoonGlitter      ("Moon glitter path intensity", Range(0,2)) = 1.0
        _MoonGlitterLength("Moon glitter path length (m, descending column)", Float) = 9.0
        _MoonColor        ("Moon color (cool silver)", Color) = (0.78, 0.84, 0.95, 1.0)
        _StarStrength     ("Star sparkle strength (night, faint)", Range(0,1)) = 0.18
        _StarDensity      ("Star sparkle density (higher = more, smaller stars)", Float) = 7.0
        _StarTwinkleSpeed ("Star twinkle speed", Float) = 1.4
        _NightStart       ("Night content start (darkness 0..1 where moon/stars fade in)", Range(0,1)) = 0.35
        _NightSoftness    ("Night content dusk ramp width", Range(0,1)) = 0.3
        _SunGlitterStrength ("Sun glitter path intensity (golden hour; 0 = off)", Range(0,2)) = 0.6
        _SunGlitterColor  ("Sun glitter color (warm gold)", Color) = (1.0, 0.82, 0.55, 1.0)

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

        [Header(See through shallows and day gated caustics (Arc C   col.a plus col.rgb only))]
        // TWO owner-opt-in shallow-water effects, both default OFF (today's look byte-identical):
        //  (1) SEE-THROUGH SHALLOWS lowers col.a in a thin band right at the shore so the SEABED sprite
        //      drawn behind (lower sorting) BLEEDS THROUGH the transparent water (Blend SrcAlpha
        //      OneMinusSrcAlpha). Only col.a is touched, and only AFTER the depth-colour block settles alpha
        //      and BEFORE the shoreline foam re-opacifies it — never depth/clip/_WaterLevel/the sim (P1, rule 5).
        //  (2) DAY-GATED CAUSTICS multiplies the existing shallow caustic add by a DAY factor
        //      (saturate(_SunElevation): peaks at noon, naturally 0 at night) so the sun-dappled light nets
        //      only show by day. When the day/night cycle is NOT running (_DayNightTint sum ~ 0: editor /
        //      bare art scene) it treats the world as full day — the same "unset" convention NightFactor /
        //      the palette grade use (NOT _SunElevation == 0, which is a real horizon value at sunrise/sunset).
        // Because see-through lowers alpha in the SAME shallow band the caustics live in, the lowered alpha
        // partly FADES the caustic-lit water under the blend — keep _ShallowMinAlpha conservative (> 0.5: the
        // seabed shows UNGRADED so it must read as a HINT, not a hole) and optionally bias caustics deeper.
        _ShallowTranslucency  ("Shallow see-through amount (0 = off / today)", Range(0,1)) = 0.0
        _ShallowSeeThroughDepth("Shallow see-through band depth (m)", Float) = 0.6
        _ShallowMinAlpha      ("Shallow min alpha at the waterline (keep above 0.5)", Range(0,1)) = 0.65
        _CausticDayGate       ("Caustic day gate (0 = off / always on, 1 = day only)", Range(0,1)) = 0.0
        _CausticShallowBias   ("Caustic band deepen bias (m; push dapple off the very edge)", Float) = 0.0

        [Header(Current drift lines (Arc C   col.rgb only   reads the tidal set   default OFF))]
        // Faint foam STREAKS aligned with the tidal CURRENT so the player can READ which way the sea is
        // setting (P1 Sea Has Moods). Built from the SAME _FlowDir/_Flow the surface scroll uses — those are
        // pushed from EnvironmentSample.CurrentVector (the tide's SMOOTHED set), so the lines "read the tide"
        // for FREE (no new C# uniform push). Thin ridged-noise lanes ACROSS the flow, stretched ALONG it and
        // advanced downstream over time, tinted toward the foam colour, faint. Added in the same pre-grade
        // dressing zone the foam + whitecaps occupy so the palette guard-rail bounds them. col.rgb ONLY:
        // never depth/clip/_WaterLevel/the height read/the sim (P1 integrity, CLAUDE.md rule 5).
        // SEA-STATE WINDOW (a BELL, not a fade): the lines PEAK on calm-to-moderate water and are ZERO on dead
        // glass (a mirror stays a mirror) AND ZERO in a storm's chaos — a band over _Chop (rises from Lo, holds,
        // falls to 0 by Hi). They also fade DOWN as wind roughness (_Roughness) rises so they don't fight foam.
        // _DriftLineStrength = 0 is an EXACT passthrough (opt-in, revertible — rule 6): today's look byte-identical.
        _DriftLineStrength   ("Drift line strength (0 = off / today)", Range(0,1)) = 0.0
        _DriftLineSpeed      ("Drift line downstream speed (x _Flow)", Float) = 0.5
        _DriftLineStretch    ("Drift line along-flow stretch (thin lanes)", Float) = 5.0
        _DriftLineScale      ("Drift line scale (lanes/unit)", Float) = 0.3
        _DriftLineSeaStateLo ("Drift line sea-state rise (_Chop; 0 = glass has none)", Range(0,1)) = 0.05
        _DriftLineSeaStateHi ("Drift line sea-state gone (_Chop; storm has none)", Range(0,1)) = 0.6
        _DriftLineColor      ("Drift line colour (a=0 reuses foam colour)", Color) = (0.92, 0.96, 0.98, 0.0)

        [Header(Surface RAIN RINGS (dimple rings   night visible   Arc C   default OFF))]
        // Expanding concentric dimple RINGS stippled over the water where rain strikes the surface (P1 Sea
        // Has Moods). Pixelized value-noise seeds the ring CENTRES per cell; each ring expands (frac-phase
        // radius) with a thin bright edge, gated by _RainIntensity (DERIVED in C# from sea-state + visibility
        // via AmbientParticleMath.RainIntensity and pushed as the _RainIntensity uniform — NOT re-derived here,
        // NOT hand-tuned). Masked to open water via the READ-ONLY depth key. col.rgb ONLY: never depth/clip/
        // _WaterLevel/the height read/the sim (P1 integrity, CLAUDE.md rule 5). OWNER RULING (2026-07-05): the
        // rings are added in the POST-GRADE OVERLAY-COMPENSATED block (beside the boat beam / moon glitter) and
        // divided by max(_DayNightTint.rgb) so the downstream night MULTIPLY (ADR 0013) cancels — a night squall
        // still shows rain on black water. _RainRingStrength = 0 is an EXACT passthrough (opt-in, revertible).
        _RainIntensity     ("Rain intensity (0..1; DERIVED in C# — not hand-tuned)", Range(0,1)) = 0.0
        _RainRingStrength  ("Rain ring strength (0 = off / today)", Range(0,2)) = 0.0
        _RainRingScale     ("Rain ring cell scale (cells/unit; BIGGER = smaller rings)", Float) = 6.0
        _RainRingDensity   ("Rain ring density (fraction of cells that ring)", Range(0,1)) = 0.35
        _RainRingSpeed     ("Rain ring expansion speed (rings/sec)", Float) = 1.5
        _RainRingColor     ("Rain ring colour (pale cool white)", Color) = (0.86, 0.92, 0.98, 1.0)

        [Header(STORM FOAM LANES (downwind foam streaks in a blow   Arc C   default OFF))]
        // Long downwind foam streaks that come up in a building sea (P1) — the storm sibling of the drift
        // lines, but keyed to the WIND (the _WindDir aniso basis, reused from the whitecaps) not the current,
        // and gated by _Roughness so they are STRONG in a blow and GONE on calm (not a bell — a monotone rise).
        // Reuse the EvolvingField + the ridged-lane streak idiom, stretched ALONG the wind so a round cell
        // reads as a long thin lane. Placed PRE-grade next to the whitecaps so they DIM with the night like the
        // rest of the foam (opposite of the night-visible rain rings). col.rgb ONLY: never depth/clip/
        // _WaterLevel/the height read/the sim (P1 integrity, rule 5). _StormFoamLaneStrength = 0 = today.
        _StormFoamLaneStrength ("Storm foam lane strength (0 = off / today)", Range(0,2)) = 0.0
        _StormFoamLaneStretch  ("Storm foam lane along-wind stretch (thin lanes)", Float) = 6.0
        _StormFoamLaneScale    ("Storm foam lane scale (lanes/unit)", Float) = 0.3

        [Header(BOAT SPOTLIGHT REVEAL (searchlight not floodlamp   owner tunes here or on BoatSpotlight))]
        // The boat beam REVEALS the water rather than painting an amber slab on it: inside the cone the water's
        // OWN colour is multiply-brightened (col.rgb *= 1 + weight*brighten, so crests/foam/troughs scale up
        // TOGETHER, still readable, merely lit), plus a FAINT warm additive tint. See BoatLightTerm + the
        // composite (ADR 0016). All three are per-material so the owner can dial the look on Water.mat directly.
        // col.rgb ONLY (P1, rule 5).
        _BoatLightBrighten   ("Beam brighten (multiply-lift of the water inside the cone)", Range(0,8)) = 2.5
        _BoatLightTintAmount ("Beam warm tint (faint additive warmth inside the cone)", Range(0,2)) = 0.25
        _BoatLightGain       ("Beam cone weight gain (shapes the cone weight before the lift)", Range(0,4)) = 0.5
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

            // DAY/NIGHT PRE-COMPENSATION per-channel floor (the complete-dark fix; see the post-grade add in
            // frag()). The day/night overlay MULTIPLIES the whole frame by _DayNightTint AFTER this shader runs
            // (ADR 0013), which crushed the in-water light content (boat beam, moon/glitter/stars) to ~3-6% at
            // deep night. The fix divides those additive terms by max(_DayNightTint.rgb, DN_COMP_MIN_CHANNEL)
            // BEFORE the overlay so the multiply cancels — the same pre-compensation pattern the palette
            // guard-rail's PaletteValueFloorDayNight already uses (ADR 0015). The floor bounds the boost at
            // <= 1/0.02 = 50x so a near-zero tint channel can't explode the divide toward infinity; the shipped
            // deepest-night tint channels (~0.022, 0.029, 0.061 = skyTint(0.12,0.16,0.34) x intensity floor 0.18)
            // all EXCEED the floor, so at deepest night the cancellation is exact — no hue shift, no clipping.
            // HDR DEPENDENCY: this only works because the URP asset has HDR ON (UniversalRP.asset
            // m_SupportsHDR: 1) — the compensated values are far above 1 and must SURVIVE the framebuffer to
            // reach the overlay's multiply. If a later mobile port turns HDR off, the buffer clamps to 1 and the
            // lights silently go dim again — re-check this fix there. Mirrors
            // LightMath.DayNightCompensationMinChannel / CompensateForDayNightTint (the headless twin).
            #define DN_COMP_MIN_CHANNEL 0.02

            // GLOBAL shared SIM WIND (published by HiddenHarbours.Art.GrassWindBridge via Shader.SetGlobalVector,
            // the SAME global the grass shader reads). _WindWorld.xy = the wind DIRECTION × a 0..1 strength, so a
            // gust leans the grass AND drifts the water's CLOUD reflections TOGETHER (cohesive sky/scene motion).
            // (0,0,0,0) when nothing publishes it -> the cloud drift falls back to a gentle fixed +X creep, so an
            // empty material / a bare art scene still reads sensibly (never a frozen or NaN drift). A GLOBAL
            // (outside the per-material CBUFFER) like _SunDir; reading it adds NO new C# uniform push to this shader.
            float4 _WindWorld;

            // GLOBAL LIVING-MOON state (published by HiddenHarbours.Art.MoonCycle via Shader.SetGlobalVector).
            // The moon RISES/ARCS/SETS across the night and cycles through its PHASES (tied to the same lunar
            // period as the spring/neap tides). The water reflection reads these to POSITION + SHAPE the
            // reflected moon. GLOBALS (outside the per-material CBUFFER), so an empty material still compiles and
            // a no-MoonCycle scene reads them as zero -> the reflection falls back to a fixed opposite-sun moon.
            //   _MoonDir.xy        — the moon's CURRENT reflected ground direction (sweeps east->west); (0,0) = down.
            //   _MoonPhaseState    — x = phase 0..1 (0 new, 0.5 full), y = signed terminator (the crescent mask),
            //                        z = live brightness (illuminated-fraction × presence), w = above-horizon 0..1.
            float4 _MoonDir;
            float4 _MoonPhaseState;

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

            // GLOBAL SHARED WAVE FIELD (ADR 0018 B1) — published by HiddenHarbours.Art.WaveFieldBridge via
            // Shader.SetGlobalVector, EVERY FRAME. The bridge ticks the SAME WaveFieldAnimator the boat's
            // rocking (BoatWaveMotion, B2) ticks — eased train parameters, dispersion speed re-derived from
            // the EASED wavelength in C#, phase accumulated INCREMENTALLY in double and BAKED into the
            // published phase — so the water pixels and the hull ride the IDENTICAL eased sea, and there is
            // NO time uniform here at all: WaveFieldSample() below evaluates theta = k*(dir.worldPos) + phi.
            // The shader NEVER re-derives the phase speed (dispersion lives in C# only, ADR 0018 §(4)).
            // GLOBALS (outside the per-material CBUFFER) like _SunDir/_WindWorld/_MoonDir: an empty material
            // still compiles, and a no-bridge scene (edit mode / bare art scene / cycle off) reads them as
            // ZERO -> count 0 -> the legacy noise-swell path holds (the "unset" convention).
            float4 _WaveTrain0;      // xy = unit travel direction, z = wave number k = 2pi/lambda, w = amplitude (m)
            float4 _WaveTrain1;
            float4 _WaveTrain2;
            float4 _WaveTrain3;
            float4 _WavePhases;      // per-train phase (rad), accumulated in C# DOUBLE + wrapped to [0, 2pi)
            float4 _WaveFieldParams; // x = live train count (0 = not published), y = crest sharpening p,
                                     // z = total amplitude (m; the crest normalizer), w = reserved

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
                // Shared wave field (ADR 0018 B1): the whitecap sea-state onset over total train amplitude.
                float  _WhitecapOnsetAmp;
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
                // Sky-content reflection (clouds + moon glitter + stars; col.rgb-only dressing, day/night-driven).
                float  _SkyReflectionStrength;
                float  _CloudStrength;
                float  _CloudScale;
                float  _CloudDriftSpeed;
                float  _CloudSoftness;
                float4 _CloudColor;
                float  _MoonStrength;
                float  _MoonSize;
                float  _MoonGlitter;
                float  _MoonGlitterLength;
                float4 _MoonColor;
                float  _StarStrength;
                float  _StarDensity;
                float  _StarTwinkleSpeed;
                float  _NightStart;
                float  _NightSoftness;
                float  _SunGlitterStrength;
                float4 _SunGlitterColor;
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
                // See-through shallows (col.a) + day-gated caustics (col.rgb) — Arc C, all default OFF.
                float  _ShallowTranslucency;
                float  _ShallowSeeThroughDepth;
                float  _ShallowMinAlpha;
                float  _CausticDayGate;
                float  _CausticShallowBias;
                // Current drift lines (col.rgb; keyed to _FlowDir/_Flow — the tidal set) — Arc C, default OFF.
                float  _DriftLineStrength;
                float  _DriftLineSpeed;
                float  _DriftLineStretch;
                float  _DriftLineScale;
                float  _DriftLineSeaStateLo;
                float  _DriftLineSeaStateHi;
                float4 _DriftLineColor;
                // Surface rain rings (col.rgb; _RainIntensity is DERIVED in C# and pushed) — Arc C, default OFF.
                float  _RainIntensity;
                float  _RainRingStrength;
                float  _RainRingScale;
                float  _RainRingDensity;
                float  _RainRingSpeed;
                float4 _RainRingColor;
                // Storm foam lanes (col.rgb; keyed to _WindDir/_Roughness — the blow) — Arc C, default OFF.
                float  _StormFoamLaneStrength;
                float  _StormFoamLaneStretch;
                float  _StormFoamLaneScale;
                // Boat spotlight REVEAL (searchlight not floodlamp; ADR 0016). Per-material so the owner tunes the
                // look on Water.mat: how strongly the cone multiply-lifts the water's own colour, the faint warm
                // additive tint, and a gain that shapes the cone weight before the lift. col.rgb ONLY (P1, rule 5).
                float  _BoatLightBrighten;
                float  _BoatLightTintAmount;
                float  _BoatLightGain;
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

            // ====================================================================================================
            // THE SHARED WAVE FIELD — the HLSL twin of WaveMath.Sample (ADR 0018 §(4), Arc B1).
            // A line-by-line transcription of the C# reference (Core/Environment/WaveMath.cs `Sample`,
            // mirrored headless by WaveFieldBridge.ShaderTwinSample — change one, change ALL in the same PR),
            // reading the packed globals the WaveFieldBridge publishes (see the _WaveTrain* declarations).
            // theta = k*(dir.worldPos) + phi: the phi already carries the advancing time (accumulated in C#
            // DOUBLE by the shared WaveFieldAnimator and wrapped to [0, 2pi) before the float cast), so the
            // position math here is plain float (world coords are small) and NO time uniform exists. The
            // shader never re-derives the phase speed — dispersion lives in C# only.
            //   worldXY   — the sample position (pass it PIXELIZED so the field reads as pixel art, §3).
            //   freqScale — a VISUAL wavelength scale on k (the legacy _OceanSwellScale mapping; 1 = the
            //               field's TRUE wavelengths — what the hull rocks on).
            //   height    — surface offset (m) about the tide level (sharpened sine, narrow crests over
            //               broad troughs). col.rgb DRESSING only downstream — never depth/clip/_WaterLevel.
            //   slopeXY   — the ANALYTIC gradient of height (kept for twin completeness/parity; the B2 hull
            //               tilt reads the C# side of this same formula).
            //   crestF    — 0..1, the crest factor (height normalized by the amplitude envelope, sharpened):
            //               the whitecap driver. 0 through the troughs and on dead glass.
            //   primaryCos— cos(theta) of the PRIMARY train: NEGATIVE on the wave's FRONT face (this point
            //               crests next — foam FORMS), POSITIVE behind the crest (it just passed — foam
            //               FADES). The fore/aft asymmetry the whitecap lifecycle keys on.
            // HLSL discipline: a FIXED [unroll] bound of WAVE_MAX_TRAINS with the live count masked INSIDE
            // (NEVER [unroll] a runtime count — the #96 magenta trap); pow bases floored at 1e-6 because
            // HLSL pow(0, 0) is NaN on some GPUs (the deviation lives where cos(theta) ~ 0 — invisible).
            // ====================================================================================================
            #define WAVE_MAX_TRAINS 4
            void WaveFieldSample(float2 worldXY, float freqScale,
                                 out float height, out float2 slopeXY, out float crestF, out float primaryCos)
            {
                height = 0.0;
                slopeXY = float2(0.0, 0.0);
                crestF = 0.0;
                primaryCos = 0.0;

                float4 trains[WAVE_MAX_TRAINS] = { _WaveTrain0, _WaveTrain1, _WaveTrain2, _WaveTrain3 };
                float phis[WAVE_MAX_TRAINS] = { _WavePhases.x, _WavePhases.y, _WavePhases.z, _WavePhases.w };
                int count = (int)(_WaveFieldParams.x + 0.5);
                float p = max(_WaveFieldParams.y, 1.0);            // crest sharpening (>= 1, like the C# clamp)
                float totalAmp = _WaveFieldParams.z;
                float fs = max(freqScale, 1e-3);

                [unroll]
                for (int i = 0; i < WAVE_MAX_TRAINS; i++)          // FIXED bound; the count masks inside
                {
                    float amplitude = trains[i].w;
                    if (i < count && amplitude > 0.0)              // a dead/silent slot contributes nothing
                    {
                        float k = trains[i].z * fs;                // published k = 2pi/lambda, visually scaled
                        float theta = k * dot(trains[i].xy, worldXY) + phis[i];
                        float sinT = sin(theta);
                        float cosT = cos(theta);

                        float s = (sinT + 1.0) * 0.5;              // 0 in the trough .. 1 at the crest
                        float shaped = pow(max(s, 1e-6), p);       // pinch: narrow crest, broad trough
                        height += amplitude * (2.0 * shaped - 1.0);

                        // the ANALYTIC derivative (chain rule) of the height term — the C# reference's slope.
                        float slopeMag = amplitude * p * pow(max(s, 1e-6), p - 1.0) * cosT * k;
                        slopeXY += slopeMag * trains[i].xy;

                        if (i == 0) primaryCos = cosT;             // the primary train's face sign (see doc)
                    }
                }

                if (totalAmp > 1e-6)                               // WaveMath.GlassAmplitudeMeters guard
                    crestF = pow(saturate(height / totalAmp), p);
            }

            // ---- whitecap LIFECYCLE on the REAL wave field (ADR 0018 B1): form -> BREAK -> fade ---------------
            // The trains-live re-key of WhitecapLifecycle() above — same tunables, but the crest now has a
            // POSITION, a DIRECTION and a LIFETIME (it advances with the train), so the foam visibly forms,
            // breaks, streaks and dies ON a travelling wave instead of gating on noise (the "foggy white
            // soup" fix at the root). Inputs: crest = the twin's crestFactor (0..1); primCos = the primary
            // train's face sign (negative = front face, the crest is arriving; positive = behind, it has
            // passed); density = FoamDensity() (the sea-state coupling, unchanged).
            // The legacy lifecycle tunables carry over, re-keyed:
            //   _WhitecapFormSharpness — how tightly the BREAKING band hugs the crest tip (higher = a
            //                            crisper, narrower break). Wind (_Roughness) lowers the band the
            //                            same way it lowers the cap threshold — a gale breaks more crests.
            //   _WhitecapPeakDensity   — applied by the CALLER to the break core (the newborn opacity).
            //   _WhitecapCollapseRate  — how fast the milky residual dies behind the crest (higher = a
            //                            shorter trailing tail).
            // Two outputs so the caller composites them differently (crisp core vs milky residual):
            //   breakCore — 0..1: the dense breaking cap at/approaching the crest tip (crisp, bright).
            //   residual  — 0..1: the aged milky remnant trailing BEHIND the crest (the caller's wind-aniso
            //               coord streaks it downwind — _FoamStreakStretch, reused).
            // col.rgb-only dressing — drives no depth/clip/_WaterLevel/sim (P1 integrity, rule 5).
            void WhitecapLifecycleWave(float crest, float primCos, float density,
                                       out float breakCore, out float residual)
            {
                float c = saturate(crest);
                float building = saturate(-primCos);   // 1 on the front face (the crest is arriving)
                float passed   = saturate(primCos);    // 1 behind the crest (it has passed)

                // BREAK: a tight band at the crest tip — crisp edges over the pixelized cap field, not a
                // wash. FormSharpness slides the band's lower edge toward the tip AND narrows it; wind
                // lowers it (rougher => more crests break), the capThr discipline reused.
                float breakLo = max(lerp(0.3, 0.8, saturate(_WhitecapFormSharpness))
                                    - saturate(_Roughness) * 0.35, 0.05);
                float breakHi = min(breakLo + lerp(0.3, 0.1, saturate(_WhitecapFormSharpness)), 1.0);
                float breaking = smoothstep(breakLo, breakHi, c);
                // FORM: on the FRONT face the foam whitens in early as the crest builds toward the break.
                float forming = smoothstep(breakLo * 0.5, breakLo, c) * building * 0.6;
                breakCore = saturate(max(breaking, forming) * saturate(density));

                // FADE: behind the crest the cap ages to milky residual, dying at the collapse rate.
                residual = saturate(pow(max(c, 1e-4), max(_WhitecapCollapseRate, 0.05))
                                    * passed * saturate(density));
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

            // ---- night factor: how DARK is the sky right now? (gates the moon + stars) -----------------------
            // Mirrors WaterReflection.NightFactor (the headless determinism twin) AND the boat-light night gate
            // convention: Rec.601 luma of the day/night tint -> darkness -> smoothstep over the dusk ramp. 0 in
            // full daylight, 1 at deep night, a smooth dusk rise between (the moon/stars fade in as the sky
            // darkens). When the day/night cycle is NOT running the tint is near-black/unset; we treat that as
            // DAY (returns 0 -> no phantom night moon in a bare art scene / editor preview), the same "unset"
            // convention the reflection/specular/palette layers use. col.rgb dressing only (P1, rule 5).
            float NightFactor()
            {
                float tintSum = _DayNightTint.r + _DayNightTint.g + _DayNightTint.b;
                if (tintSum <= 1e-3)
                    return 0.0;                                   // cycle off / unset -> treat as day (no moon)
                float tintLum = max(0.0, dot(_DayNightTint.rgb, float3(0.299, 0.587, 0.114)));
                float darkness = saturate(1.0 - tintLum);
                float lo = saturate(_NightStart);
                float hi = saturate(_NightStart + max(_NightSoftness, 1e-4));
                return smoothstep(lo, hi, darkness);
            }

            // ---- sun glitter gate: the GOLDEN-HOUR window over the sun's elevation ---------------------------
            // The daytime/dusk twin of NightFactor: a smooth 0..1 window over _SunElevation that peaks when the
            // sun is LOW but UP (the long glitter path across the water at dawn/dusk), fading to 0 by high sun
            // (a high sun glints via the specular layer, not a column) and 0 below the horizon (the moon's
            // glitter takes over at night). The window: rises 0 -> 1 across elevation 0..RISE_END, holds 1
            // through the golden-hour band, falls 1 -> 0 across FALL_START..FALL_END. When the day/night cycle
            // is NOT running _SunElevation is 0 (unset) -> the gate is 0 -> no phantom glitter in a bare art
            // scene / editor preview (the same "unset" convention the moon/night content uses). Mirrors
            // WaterReflection.SunGlitterGate EXACTLY (the headless determinism twin; window constants pinned
            // in WaterReflectionTests). col.rgb dressing only (P1, rule 5).
            #define SUN_GLITTER_RISE_END   0.02
            #define SUN_GLITTER_FALL_START 0.35
            #define SUN_GLITTER_FALL_END   0.5
            float SunGlitterGate(float sunElevation)
            {
                float rise = smoothstep(0.0, SUN_GLITTER_RISE_END, sunElevation);
                float fall = 1.0 - smoothstep(SUN_GLITTER_FALL_START, SUN_GLITTER_FALL_END, sunElevation);
                return saturate(rise * fall);
            }

            // ---- moon direction: the LIVING moon's current arc position (or a fallback) ----------------------
            // Prefer the published _MoonDir global (the MoonCycle service sweeps it east->west across the night,
            // so the reflected disc + glitter TRAVEL over the water). When no MoonCycle is running (_MoonDir == 0,
            // e.g. a bare art scene / editor preview) fall back to a believable FIXED moon roughly OPPOSITE the
            // sun (negated _SunDir), or a +Y night arc if the sun dir is unset too. Mirrors
            // WaterReflection.MoonDirection for the fallback branch (the headless determinism twin). Normalized.
            float2 MoonDir()
            {
                if (dot(_MoonDir.xy, _MoonDir.xy) > 1e-6)
                    return normalize(_MoonDir.xy);               // the live, moving moon (MoonCycle)
                float2 opp = -_SunDir.xy;                        // fallback: opposite the sun
                return dot(opp, opp) > 1e-6 ? normalize(opp) : float2(0, 1);
            }

            // ---- SKY CONTENT reflection: drifting CLOUDS + the MOON glitter path + faint STARS ---------------
            // The ¾ top-down camera never shows the sky, so the water's reflection is the ONLY window onto it.
            // This composes three additive col.rgb layers ON TOP of the sky-COLOUR + sun mirror (SkyReflection):
            //   (1) CLOUDS  — soft elongated pale bands scrolling along the SHARED sim wind (_WindWorld) so the
            //                 sky drifts WITH the grass/water; tinted by the current sky (warm at dusk). Day+night.
            //   (2) MOON    — a brighter reflected disc + a shimmering VERTICAL GLITTER PATH (the classic
            //                 moonlight-on-water column: broken, wavy, animated highlights descending toward the
            //                 viewer from the moon's reflected position). NIGHT-gated; reads on CALM night water.
            //   (3) STARS   — tiny twinkling glints, very sparse + faint. NIGHT-gated.
            // ALL of it inherits the existing sea-state fade (strong on CALM, gone in a storm) via ReflectionStrength
            // and the sharpness smear, and the moon/stars additionally gate by night. Everything is pixelized
            // (pixel-art faithful, §3) and ADDED to col.rgb — it NEVER touches depth/clip/the deep tint/the
            // caustic gate/_WaterLevel (P1 integrity, CLAUDE.md rule 5). _SkyReflectionStrength = 0 = today's look.
            //
            // OUTPUT SPLIT (the complete-dark fix): the content comes back in TWO parts so the caller composites
            // each where it SURVIVES the day/night multiply overlay (ADR 0013):
            //   dayRGB   — the daylit share (the clouds' day portion). Added PRE-grade, exactly where the whole
            //              layer used to sit, so the DAYLIGHT look is pixel-identical to before the split
            //              (night = 0 puts 100% of the content here).
            //   nightRGB — the COMPENSATED share: the NIGHT-GATED content (moon disc + glitter path + stars +
            //              the clouds' night portion) PLUS the golden-hour SUN glitter path (sun-gated, not
            //              night-gated — it rides this bucket so the dusk tint's multiply can't mute its warm
            //              gold; at midday the tint is ~1 so the compensation is a natural no-op, and the gate
            //              is ~0 there anyway). Added AFTER the palette grade, PRE-COMPENSATED by the
            //              divide-by-tint pattern (see DN_COMP_MIN_CHANNEL above) so complete dark doesn't
            //              crush the moon/stars to ~3%, and so the grade's saturated deep-night floor can't
            //              re-flatten them either.
            //   The two parts always SUM to the layer's original value, so dusk carries no discontinuity in the
            //   pre-compensation content — only the compensation boost changes as the night gate rises.
            //
            //   worldXY    — pixel world position (pixelized inside each layer for the pixel-art read).
            //   surf       — the layer-2 surface noise (0..1) so the sky ripples WITH the swell at calm.
            //   swellCrest — the rolling-swell crest factor (0..1) so the sky brightens on the lit swell faces.
            //   t          — _Time.y (clouds drift, the moon glitter shimmers, stars twinkle).
            void SkyContentReflection(float2 worldXY, float surf, float swellCrest, float t,
                                      out float3 dayRGB, out float3 nightRGB)
            {
                // out params must be fully written on EVERY path (HLSL) — zero them before any early return.
                dayRGB = float3(0, 0, 0);
                nightRGB = float3(0, 0, 0);

                float master = saturate(_SkyReflectionStrength);
                if (master <= 0.001)
                    return;                                       // sky content off -> the pre-feature look

                // The SAME sea-state fade + sharpness the sky-colour mirror uses: clouds/moon/stars die in chop.
                float seaState = ReflectionStrength();            // strong on glass -> 0 by the fade-chop / storm
                if (seaState <= 0.001)
                    return;                                       // a storm doesn't mirror the sky either
                float sharp = ReflectionSharpness();              // 1 = crisp mirror, 0 = smeared
                float night = NightFactor();                      // 0 day .. 1 deep night (moon/stars gate)

                // the current reflected SKY colour (warm dusk / dark night / bright noon) — clouds borrow it so
                // they tint with the time of day; reuse the SkyReflection fallback convention for an unset cycle.
                float tintSum = _DayNightTint.r + _DayNightTint.g + _DayNightTint.b;
                bool cycleOn = tintSum > 1e-3;
                float3 sky = cycleOn ? lerp(_ReflectionColor.rgb, _DayNightTint.rgb, saturate(_ReflectionSkyTint))
                                     : _ReflectionColor.rgb;

                float2 pp = Pixelize(worldXY);

                // ---- (1) drifting CLOUDS (day + night) ------------------------------------------------------
                // Soft, elongated pale bands scrolled along the shared sim wind. Built from a couple of FBM
                // samples on a coord COMPRESSED across the wind so the cloud cells elongate into wisps ALONG it
                // (like the wind-streaked foam). _CloudSoftness widens the soft edge (crisp puffs -> wispy veil).
                if (_CloudStrength > 0.001)
                {
                    float2 wind = _WindWorld.xy;
                    float2 wdir = dot(wind, wind) > 1e-6 ? normalize(wind) : float2(1, 0);   // +X creep fallback
                    float2 wperp = float2(-wdir.y, wdir.x);
                    float2 drift = wdir * (_CloudDriftSpeed * t);
                    // CAMERA-ANCHORED like the moon disc below (float2 anchor = _WorldSpaceCameraPos.xy): distant
                    // clouds are a reflection of the sky at infinity, so they must STAY PUT as the follow-cam
                    // tracks the sailing boat and drift ONLY with the wind at _CloudDriftSpeed. Sampling the FBM
                    // on the raw worldXY made the pattern scroll past at BOAT speed — which is why lowering
                    // _CloudDriftSpeed never fixed it (that dial only rode ON TOP of the boat-motion scroll).
                    // Subtracting the camera ground position cancels the boat motion; _WorldSpaceCameraPos is a
                    // URP built-in already read by the moon anchor — no new uniform. col.rgb-only, deterministic.
                    // anisotropic cloud coord: stretch ALONG the wind (compress the cross axis) so cells elongate.
                    float2 cp = ((worldXY - _WorldSpaceCameraPos.xy) + drift) * max(_CloudScale, 1e-4);
                    float2 capr = float2(dot(cp, wdir), dot(cp, wperp) * 2.5);
                    float clouds = Fbm(Pixelize(capr));            // 0..1 broad fractal field (pixelized inside Fbm)
                    // shape into bands: a soft threshold makes pale clumps with gaps of clear sky between.
                    float soft = lerp(0.05, 0.4, saturate(_CloudSoftness));
                    float cloudMask = smoothstep(0.5 - soft, 0.5 + soft, clouds);
                    // the clouds ripple a touch with the surface at calm, and catch a little more light on crests.
                    cloudMask *= lerp(0.85, 1.15, surf) * lerp(0.9, 1.1, swellCrest);
                    // pale cloud colour, gently tinted toward the current sky (warm at dusk, cool at night).
                    float3 cloudCol = lerp(_CloudColor.rgb, sky, 0.35);
                    float3 cloudTerm = cloudCol * cloudMask * _CloudStrength;
                    // SPLIT by the night factor: the day share stays in the pre-grade composite (daylight is
                    // pixel-identical — night = 0 routes ALL of it here); the night share joins the compensated
                    // post-grade add so the clouds keep reading as the overlay darkens (they were crushed with
                    // the moon before). The shares sum to the original term — no dusk discontinuity.
                    dayRGB += cloudTerm * (1.0 - night);
                    nightRGB += cloudTerm * night;
                }

                // ---- (2) the LIVING MOON: a reflected disc (phase-shaped) + a vertical GLITTER PATH (night) --
                // The moon RISES/ARCS/SETS across the night (its direction comes from MoonCycle via _MoonDir) and
                // changes shape over the lunar month (the crescent/gibbous TERMINATOR comes from _MoonPhaseState),
                // dimming to a thin crescent at new moon. When no MoonCycle runs, fall back to a fixed full moon
                // opposite the sun so a bare scene still shows one.
                // moonBright: live brightness (illuminated-fraction × presence) — fall back to 1 (full) if unset.
                // moonPresence: 0..1 above-horizon (fades the moon at the horizons) — fall back to 1 if unset.
                bool moonStateOn = (abs(_MoonPhaseState.x) + abs(_MoonPhaseState.y)
                                  + _MoonPhaseState.z + _MoonPhaseState.w) > 1e-4;
                float moonBright   = moonStateOn ? _MoonPhaseState.z : 1.0;
                float moonPresence = moonStateOn ? _MoonPhaseState.w : 1.0;
                float terminator   = moonStateOn ? _MoonPhaseState.y : -1.0;   // -1 = full disc by default
                // the moon reads when the SKY is dark (day/night) AND the moon is up + lit.
                float moonGate = night * moonPresence;
                if (moonGate > 0.001 && moonBright > 0.001 && (_MoonStrength > 0.001 || _MoonGlitter > 0.001))
                {
                    // Place the moon's reflected position out along the (current arc) moon direction from the
                    // CAMERA's ground position, so the reflection TRAVELS WITH THE VIEWER like a real reflection
                    // of a body at infinity (the classic "the moon follows you along the shore") and always lands
                    // on water NEAR the play area. (It was anchored at the height-map world centre — for St
                    // Peters that is world (0,0), the middle of the SANDBAR, bared at most tides ~40 m from the
                    // play area, so the owner literally never saw the moon.) The disc still rises/arcs/sets via
                    // the moon DIRECTION below; per-pixel it stays stable (it moves with the camera, not the pixel).
                    float2 anchor = _WorldSpaceCameraPos.xy;
                    float2 mdir = MoonDir();
                    float moonReach = max(_MoonGlitterLength, 1e-3);
                    float2 moonPos = anchor + mdir * moonReach * 0.5;     // the reflected moon disc's centre

                    // --- the disc: a soft bright spot at the reflected moon position, rippled by the surface ---
                    float2 toMoon = pp - moonPos;
                    float dMoon = length(toMoon);
                    float discR = max(_MoonSize, 1e-3);
                    float disc = 1.0 - smoothstep(discR * 0.5, discR, dMoon);
                    // the surface breaks the disc edge so it shimmers rather than reading as a hard circle.
                    disc *= lerp(0.7, 1.0, surf);
                    // PHASE / terminator: carve the lit crescent. Project the in-disc offset along the moon
                    // direction (the lit limb faces the sun); the terminator (-1 full .. +1 new) is the cut line
                    // in that normalized along-axis coord. limbT > terminator stays lit; below is the dark limb.
                    float limbT = discR > 1e-4 ? dot(toMoon, mdir) / discR : 0.0;   // -1..1 across the disc
                    float litLimb = smoothstep(terminator - 0.25, terminator + 0.25, limbT);
                    disc *= litLimb;

                    // --- the GLITTER PATH: the classic moonlight column descending toward the viewer ----------
                    // Build a coord along the moon axis (the column runs from the moon toward the camera/bottom).
                    // alongMoon grows from the moon outward; crossMoon is the lateral distance from the column.
                    float along = dot(toMoon, mdir);                      // <0 between the moon and the viewer
                    float cross = dot(toMoon, float2(-mdir.y, mdir.x));   // signed lateral offset from the column
                    // the column lives on the viewer side of the moon (along < 0) and fades over its length.
                    float colN = saturate(-along / moonReach);           // 0 at the moon -> 1 at the far end
                    float colSpan = 1.0 - colN;                          // bright near the moon, fading out
                    // the column WIDENS as it descends (a fan of glints), and the surface chop scatters it.
                    float halfWidth = discR * (0.6 + colN * 2.2) * lerp(1.0, 2.2, 1.0 - sharp);
                    float lateral = 1.0 - smoothstep(0.0, max(halfWidth, 1e-3), abs(cross));
                    // BROKEN, WAVY, ANIMATED highlights: ridge two scrolling noise samples so only bright lanes
                    // show (the glints), the lanes WAVERING with the surface and TWINKLING over time. Pixelized.
                    float2 gUV = float2(along, cross) * 0.6 + float2(-t * 0.6, sin(t * 0.7) * 0.5);
                    float g1 = ValueNoise(Pixelize(gUV));
                    float g2 = ValueNoise(Pixelize(gUV * 1.7 + 4.2));
                    float glints = pow(saturate(1.0 - abs(g1 - g2) * 2.2), 3.0);
                    // a fast shimmer flicker so the path twinkles (broken light on moving water).
                    float shimmer = 0.6 + 0.4 * sin(t * 3.1 + (along + cross) * 1.3);
                    float pathMask = colSpan * lateral * glints * shimmer;

                    // dim the whole moon by its live brightness (thin crescent / new moon = dim) and the night-up gate.
                    // NIGHT-gated content -> the compensated post-grade part (it must survive the overlay).
                    float3 moonCol = _MoonColor.rgb;
                    nightRGB += moonCol * disc * _MoonStrength * moonGate * moonBright;
                    nightRGB += moonCol * pathMask * _MoonGlitter * moonGate * moonBright;
                }

                // ---- (3) faint STAR sparkle (night) ---------------------------------------------------------
                // Tiny, sparse, twinkling glints scattered on the surface. A high-frequency hash field thresholded
                // hard (few cells light), each cell twinkling on its OWN phase so they don't pulse together. Very
                // subtle (small default strength), gated by night. Pixelized so the stars read as single pixels.
                if (_StarStrength > 0.001 && night > 0.001)
                {
                    float2 sp = Pixelize(worldXY * max(_StarDensity, 1e-3));
                    float2 cell = floor(sp);
                    float h = Hash21(cell);                              // per-cell "is there a star here" + phase
                    // only the brightest few cells host a star (sparse); the rest are dark sky.
                    float star = smoothstep(0.985, 1.0, h);
                    if (star > 0.0)
                    {
                        // each star twinkles on its own phase (hash drives the phase offset), 0..1 brightness.
                        float phase = Hash21(cell + 1.7) * 6.2831853;
                        float twinkle = 0.4 + 0.6 * (0.5 + 0.5 * sin(t * max(_StarTwinkleSpeed, 0.0) + phase));
                        // NIGHT-gated content -> the compensated post-grade part (stars must survive the overlay).
                        nightRGB += _MoonColor.rgb * star * twinkle * _StarStrength * night;
                    }
                }

                // ---- (4) the SUN GLITTER PATH: the moon column's GOLDEN-HOUR twin (dawn / dusk) -------------
                // A warm golden glitter column toward the LOW sun — the classic "path of light to the sun" that
                // stretches across calm water at dawn and dusk. Same structure as the moon's glitter path above
                // (a camera-anchored column of broken, wavy, animated glints; decorrelated noise offsets so the
                // two paths never read as copies), but gated by SunGlitterGate over _SunElevation instead of
                // night: it peaks while the sun is LOW but UP, is gone by high sun (the specular + sun streak
                // carry a high sun) and gone below the horizon (the moon takes over). Reuses the moon's geometry
                // knobs (_MoonGlitterLength = reach, _MoonSize = width basis) so the two paths stay visually
                // consistent with ONE set of tunables (rule 6). Routed into nightRGB — the COMPENSATED post-grade
                // bucket — so the dusk tint's downstream multiply can't mute the authored warm gold (at midday
                // the tint is ~1 and the compensation is a no-op; the gate is ~0 there anyway).
                float sunGate = SunGlitterGate(_SunElevation);
                if (_SunGlitterStrength > 0.001 && sunGate > 0.001)
                {
                    // direction TOWARD the sun; fall back to the material's authored light dir like the specular
                    // (the gate already returns 0 when the cycle is off, so the fallback is belt-and-braces).
                    float2 sunXY = dot(_SunDir.xy, _SunDir.xy) > 1e-6 ? _SunDir.xy : _LightDir.xy;
                    float2 sdir = normalize(sunXY + float2(1e-4, 0));
                    // CAMERA-ANCHORED like the moon (PR #143): the glitter column travels WITH the viewer like a
                    // real reflection of a body at infinity, so it always lands on water near the play area.
                    float2 sunAnchor = _WorldSpaceCameraPos.xy;
                    float sunReach = max(_MoonGlitterLength, 1e-3);
                    float2 sunPos = sunAnchor + sdir * sunReach * 0.5;   // the reflected sun's spot (no disc drawn
                                                                         // — the sun is too bright to read as one;
                                                                         // the column IS the reflection)
                    float2 toSun = pp - sunPos;
                    float sunWidthR = max(_MoonSize, 1e-3);              // shared column width basis
                    // the column runs from the sun spot toward the viewer; sAlong < 0 on the viewer side.
                    float sAlong = dot(toSun, sdir);
                    float sCross = dot(toSun, float2(-sdir.y, sdir.x));  // signed lateral offset from the column
                    float sColN = saturate(-sAlong / sunReach);          // 0 at the sun spot -> 1 at the far end
                    float sColSpan = 1.0 - sColN;                        // bright near the sun, fading out
                    // the column WIDENS as it descends and the surface chop scatters it (the sharpness smear,
                    // exactly like the moon's column — a storm doesn't mirror a sun path either).
                    float sHalfWidth = sunWidthR * (0.6 + sColN * 2.2) * lerp(1.0, 2.2, 1.0 - sharp);
                    float sLateral = 1.0 - smoothstep(0.0, max(sHalfWidth, 1e-3), abs(sCross));
                    // BROKEN, WAVY, ANIMATED glints (ridged noise lanes), pixelized; offset constants differ
                    // from the moon's so the two glitter fields are decorrelated.
                    float2 sgUV = float2(sAlong, sCross) * 0.6 + float2(-t * 0.6, sin(t * 0.7) * 0.5);
                    float sg1 = ValueNoise(Pixelize(sgUV + 13.7));
                    float sg2 = ValueNoise(Pixelize(sgUV * 1.7 + 9.1));
                    float sGlints = pow(saturate(1.0 - abs(sg1 - sg2) * 2.2), 3.0);
                    // a fast shimmer flicker so the path twinkles (broken light on moving water).
                    float sShimmer = 0.6 + 0.4 * sin(t * 3.1 + (sAlong + sCross) * 1.3);
                    float sunPathMask = sColSpan * sLateral * sGlints * sShimmer;
                    // ripple a touch with the surface at calm, like the rest of the sky content.
                    sunPathMask *= lerp(0.85, 1.15, surf);

                    // sun-gated content -> the compensated post-grade bucket (survives the dusk tint).
                    nightRGB += _SunGlitterColor.rgb * sunPathMask * _SunGlitterStrength * sunGate;
                }

                // master + the SAME sea-state fade the sky-colour mirror gets (clouds/moon/stars/sun glitter
                // all die in chop).
                dayRGB *= master * seaState;
                nightRGB *= master * seaState;
            }

            // ---- the BOAT SPOTLIGHT term: REVEAL the WATER from WITHIN this shader (ADR 0016) -----------------
            // The boat's additive QUAD lights LAND, but the URP 2D renderer draws this water shader OVER the quad
            // regardless of sorting order — so the water lights ITSELF from the published globals (_BoatLight*).
            // For this water pixel's worldXY it computes the cone WEIGHT (a SCALAR 0..1+: lamp->pixel within range +
            // within the cone half-angle, radial × angular falloff × intensity × gain), scales it by the SAME
            // night-gate the land cone uses (off by day, full at deep night, off-by-dawn), and RETURNS THAT WEIGHT.
            //
            // WHY A WEIGHT, NOT A COLOUR (owner night playtest, 2026-07-05): the old term returned a water-INDEPENDENT
            // amber colour slab (_BoatLightColor.rgb × intensity×shape×gate) that the caller added PURELY ADDITIVELY.
            // At the effective drive that over-wrote the few-percent night sea — a flat amber SLAB that OBSCURED the
            // waves/foam/depth instead of revealing them. Now the caller MULTIPLY-BRIGHTENS the water's OWN col.rgb
            // by this weight (crests/foam/troughs/depth all scale up TOGETHER, still readable, merely LIT), plus a
            // faint warm tint bias — a searchlight that reveals, not a floodlamp that paints.
            //
            // Sorting-INDEPENDENT by construction (it is part of the water's own fragment), so it cannot fail the way
            // the quad did. Mirrors LightMath.WaterConeTerm + LightMath.NightGate EXACTLY (the headless twins).
            // col.rgb ONLY — it never touches depth/clip/_WaterLevel/the height read/the sim (P1 integrity, rule 5).
            //
            //   worldXY — this water pixel's world position (pixelized inside, so the pool of light reads pixel-art).
            //   RETURNS the cone WEIGHT (>=0; 0 outside the cone / by day / no boat), NOT a colour.
            float BoatLightTerm(float2 worldXY)
            {
                float intensity = _BoatLightParams.x;
                if (intensity <= 0.001)
                    return 0.0;                             // light off / not lighting water / no boat -> nothing

                float range    = max(_BoatLightParams.y, 1e-4);
                float cosHalf  = _BoatLightParams.z;
                float cosInner = max(_BoatLightParams.w, cosHalf + 1e-4);
                float edgeSoft = _BoatLightParams2.x;

                // pixel-snap the world position so the lit pool reads as pixel art like every other layer (§3).
                float2 p = Pixelize(worldXY);
                float2 toPixel = p - _BoatLightPos.xy;
                float dist = length(toPixel);
                if (dist >= range)
                    return 0.0;                             // beyond the throw -> dark

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
                    return 0.0;

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

                // Return the cone WEIGHT (a scalar), NOT a colour: intensity × shape × gate, scaled by the
                // per-material gain the owner tunes (how strongly the cone weight ramps before the caller's
                // multiply-brighten lift). >= 0; the caller lifts the water's OWN col.rgb by this (reveal, not
                // paint). max() keeps it non-negative even if a tunable is set negative in the inspector.
                return max(0.0, intensity * shape * gate * max(_BoatLightGain, 0.0));
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

            // ---- SURFACE RAIN RINGS (col.rgb-only dressing; NIGHT-VISIBLE via post-grade compensation) -------
            // Expanding concentric dimple RINGS where rain strikes the sea (P1). _RainIntensity is DERIVED in
            // C# (AmbientParticleMath.RainIntensity of sea-state + visibility) and pushed as the uniform - this
            // helper NEVER re-derives it (WaterSurface owns the physics; the shader just draws). Mechanism, all
            // deterministic (reuses the shader ValueNoise/Hash21/Pixelize/_Time.y - no new RNG, rule 5):
            //   * CELLS: a pixelized grid at _RainRingScale; each cell that passes the _RainRingDensity lottery
            //     (a stable per-cell Hash21) hosts one raindrop strike, its CENTRE jittered inside the cell and
            //     its phase offset per-cell so the rings do not pulse in lockstep.
            //   * RINGS: RAINRING_TAPS concentric rings expand from the centre - radius = frac(strike phase) so
            //     each ring is born at the centre, grows, then recycles; a thin bright edge (a narrow band around
            //     the growing radius) is the ring line, fading as the ring grows (a dying ripple).
            //   * The tap count is a COMPILE-TIME constant (RAINRING_TAPS), NEVER an [unroll] over a runtime
            //     count - the #96 magenta trap. Masked to open water by the READ-ONLY depth key (dt passed in).
            // Returns the additive RGB (BEFORE the day/night compensation the caller applies). col.rgb ONLY:
            // never depth/clip/_WaterLevel/the height read/the sim (P1 integrity, CLAUDE.md rule 5).
            #define RAINRING_TAPS 3
            float3 RainRings(float2 worldXY, float dt, float t)
            {
                if (_RainRingStrength <= 0.001 || _RainIntensity <= 0.001)
                    return float3(0, 0, 0);                 // EXACT passthrough - opt-in (rule 6): today's look

                float2 pp   = Pixelize(worldXY * max(_RainRingScale, 1e-4));
                float2 cell = floor(pp);                    // the ring-centre cell
                float2 fr   = pp - cell;                    // 0..1 position inside the cell

                // Per-cell strike: a stable lottery (density) + a jittered centre + a phase offset (no lockstep).
                float present = step(1.0 - saturate(_RainRingDensity), Hash21(cell + 0.5));
                float2 centre = float2(Hash21(cell + 1.3), Hash21(cell + 7.9));   // jittered inside the cell
                float phase0  = Hash21(cell + 3.7);                               // per-cell phase offset
                float d = length(fr - centre);              // distance (cell units) from this drop's strike

                // A family of concentric ripples at different points in their life (compile-time tap count).
                float rings = 0.0;
                [unroll]                                     // bare [unroll] over the #define bound (the FBM idiom; not a runtime count => no #96)
                for (int i = 0; i < RAINRING_TAPS; i++)
                {
                    float life   = frac(phase0 + t * max(_RainRingSpeed, 0.0) + (float)i / RAINRING_TAPS);
                    float radius = life * 0.5;              // grow from centre out to ~half a cell
                    float edge   = 1.0 - saturate(abs(d - radius) / 0.05);  // narrow band around the radius
                    edge = pow(edge, 3.0);                  // thin the ring line to a crisp stipple
                    rings += edge * (1.0 - life);           // a dying ripple fades as it expands
                }

                // Masked to OPEN water via the READ-ONLY depth key so rings do not stipple the dry shore.
                float openWater = saturate(dt);
                float amount = rings * present * openWater
                             * saturate(_RainIntensity) * saturate(_RainRingStrength);
                return _RainRingColor.rgb * amount;
            }

            // ---- STORM FOAM LANES (col.rgb-only dressing; DIMS with the night like the rest of the foam) -----
            // Long downwind foam streaks that come up in a building sea (P1) - the storm sibling of DriftLines,
            // but keyed to the WIND (the _WindDir aniso basis reused from the whitecaps) not the current, and
            // gated by _Roughness (a MONOTONE rise: gone on calm, strong in a blow - not a bell). Reuses the
            // EvolvingField (the living whitecap field) + the pow(saturate(1-|g1-g2|k)) ridged-lane streak idiom,
            // the coord STRETCHED along the wind by _StormFoamLaneStretch so a round cell reads as a long thin
            // lane. Depth is read ONLY via dt (the depth key). Placed PRE-grade next to the whitecaps so it dims
            // with the night like the foam it belongs to (opposite of the night-visible rain rings). Returns the
            // additive RGB (tinted to the foam colour). col.rgb ONLY: never depth/clip/_WaterLevel/the height
            // read/the sim (P1 integrity, rule 5). Deterministic (ValueNoise/EvolvingField, no RNG).
            float3 StormFoamLanes(float2 worldXY, float dt, float t)
            {
                if (_StormFoamLaneStrength <= 0.001)
                    return float3(0, 0, 0);                 // EXACT passthrough - opt-in (rule 6): today's look

                // MONOTONE wind gate: gone on calm, rising with _Roughness (the wind uniform), eased in so the
                // lanes come up as the blow builds rather than snapping on.
                float blow = saturate(_Roughness);
                blow = blow * blow;                          // ease-in: they belong to a real wind, not a breeze
                if (blow <= 0.001)
                    return float3(0, 0, 0);

                // wind aniso basis (same idiom as the whitecaps): keep along-wind, compress cross-wind.
                float2 wdir  = normalize(_WindDir.xy + float2(0, 1e-4));   // safe axis on a zero wind
                float2 wperp = float2(-wdir.y, wdir.x);
                float2 pp = Pixelize(worldXY * max(_StormFoamLaneScale, 1e-4));

                // WANDER: a slow low-freq noise nudge along the wind so lanes drift/bend, not a ruler grid.
                float wander = (ValueNoise(Pixelize(worldXY * max(_StormFoamLaneScale, 1e-4) * 0.35)) - 0.5) * 2.0;

                // advance ALONG the wind over time; stretch the along-axis so lanes read long + thin.
                float stretch = max(_StormFoamLaneStretch, 1.0);
                float laneAlong  = (dot(pp, wdir) + wander * 0.6) / stretch - t * _Flow * 0.6;   // stream downwind
                float laneAcross = dot(pp, wperp);
                float2 laneUV = float2(laneAlong, laneAcross);

                // THIN RIDGED-NOISE LANES across the wind (the pow(saturate(1-|g1-g2|k)) streak idiom), the
                // pattern EVOLVING in place (the boil) via EvolvingField so lanes are not a fixed sliding stamp.
                float g1 = EvolvingField(laneUV, float2(0, 0), 1.0, _FoamEvolveSpeed, t);
                float g2 = ValueNoise(Pixelize(laneUV * 1.7 + 5.1));
                float lanes = pow(saturate(1.0 - abs(g1 - g2) * 2.2), 5.0);   // higher exp => thinner, more defined veins

                // gates: the wind blow (monotone) * open-water (fade at the wet shore edge, dt read-only).
                float openWater = saturate(dt);
                float amount = lanes * blow * openWater * saturate(_StormFoamLaneStrength);
                return _FoamColor.rgb * amount * 0.25;                        // crisp streaks (was 0.4), tinted to the foam
            }

            // ---- CURRENT DRIFT LINES (col.rgb-only dressing; reads the tidal set — Arc C, default OFF) --------
            // Faint foam streaks aligned with the tidal CURRENT so the player reads which way the sea is setting
            // (P1). The aniso basis is built from _FlowDir (the CURRENT axis, NOT the wind) — _FlowDir/_Flow are
            // already pushed from the SMOOTHED EnvironmentSample.CurrentVector, so the lines track the tide's set
            // for free (no new C# push). Depth is read ONLY via `dt` (the depth key) — never depth/clip/
            // _WaterLevel/the height read/the sim (P1 integrity, CLAUDE.md rule 5).
            //   * ALONG the flow: advance the sample downstream over time (t * _Flow * _DriftLineSpeed).
            //   * ACROSS the flow: thin ridged-noise lanes (the pow(saturate(1-|g1-g2|k)) streak idiom), the
            //     coord STRETCHED along-flow by _DriftLineStretch so a round cell reads as a long thin lane.
            //   * WANDER: a low-freq ValueNoise nudges the along-coord so the lanes aren't a marching ruler grid.
            //   * SEA-STATE WINDOW (a BELL, not a fade): 0 on dead glass, peak on calm-to-moderate, 0 by storm —
            //     rises from _DriftLineSeaStateLo, holds, falls to 0 by _DriftLineSeaStateHi over _Chop.
            //   * FOAM-DODGE: fade down as wind roughness (_Roughness) rises (in scope; foamCoverage is not).
            //   * DEPTH: fade out at the very shore (dt) so the lines live on open, navigable water, not the wet
            //     foam edge. All coords Pixelized (pixel-art faithful); noise is the shader's own ValueNoise
            //     (deterministic — no new RNG). Returns the additive RGB (faint, tinted toward the foam colour).
            float3 DriftLines(float2 worldXY, float dt, float t)
            {
                if (_DriftLineStrength <= 0.001)
                    return float3(0, 0, 0);                 // EXACT passthrough — opt-in (rule 6): today's look

                // (1) SEA-STATE BELL over _Chop: rise (Lo -> mid), hold, fall to 0 by Hi. Zero on glass + storm.
                float lo = saturate(_DriftLineSeaStateLo);
                float hi = max(_DriftLineSeaStateHi, lo + 1e-3);
                float mid = (lo + hi) * 0.5;
                float rise = smoothstep(lo, mid, _Chop);            // 0 below Lo -> 1 at the middle
                float fall = 1.0 - smoothstep(mid, hi, _Chop);      // 1 at the middle -> 0 by Hi
                float seaState = rise * fall;                       // a band (bell), NOT a monotone fade
                if (seaState <= 0.001)
                    return float3(0, 0, 0);                 // dead glass or full storm => no lines

                // (2) the flow (CURRENT) aniso basis — the wind-aniso idiom keyed to _FlowDir, not _WindDir.
                float2 flowdir = normalize(_FlowDir.xy + float2(1e-4, 0));  // safe axis on a zero flow
                float2 flowperp = float2(-flowdir.y, flowdir.x);
                float2 pp = Pixelize(worldXY * _DriftLineScale);

                // WANDER: a slow low-freq noise nudge along the flow so the lanes drift/bend, not a ruler grid.
                float wander = (ValueNoise(Pixelize(worldXY * _DriftLineScale * 0.35)) - 0.5) * 2.0;

                // (3) advance ALONG the flow over time; stretch the along-axis so lanes read long + thin.
                float stretch = max(_DriftLineStretch, 1.0);
                float along = (dot(pp, flowdir) + wander * 0.6) / stretch
                              - t * _Flow * _DriftLineSpeed;         // downstream drift (with the current)
                float across = dot(pp, flowperp);
                float2 lineUV = float2(along, across);

                // (4) THIN RIDGED-NOISE LANES across the flow (the pow(saturate(1-|g1-g2|k)) streak idiom).
                float g1 = ValueNoise(Pixelize(lineUV));
                float g2 = ValueNoise(Pixelize(lineUV * 1.7 + 3.3));
                float lanes = pow(saturate(1.0 - abs(g1 - g2) * 2.4), 3.0);   // bright thin veins => streaks

                // (5) gates: sea-state bell * foam-dodge (fade down as wind rises) * open-water (fade at shore).
                float windDodge = 1.0 - saturate(_Roughness) * 0.7;          // ease off so they don't fight foam
                float openWater = saturate(dt);                              // ~0 at the wet edge -> 1 offshore
                float amount = lanes * seaState * windDodge * openWater * saturate(_DriftLineStrength);

                // (6) faint tint toward the foam colour (a=0 on _DriftLineColor reuses _FoamColor — rule 6 knob).
                float3 tint = _DriftLineColor.a > 0.001 ? _DriftLineColor.rgb : _FoamColor.rgb;
                return tint * amount * 0.35;                                  // faint: streaks, not a paint layer
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

                // ---- SEE-THROUGH SHALLOWS (col.a ONLY; Arc C, default OFF) -------------------------------------
                // Lower the water's ALPHA in a thin band right at the shore so the SEABED sprite drawn behind
                // the Sea plane (lower sorting) bleeds through under the Blend SrcAlpha OneMinusSrcAlpha above.
                // Applied HERE — after whatever alpha the depth block settled (the _USE_DEPTHRAMP sample OR the
                // _ShallowColor/_DeepColor lerp), and BEFORE the shoreline foam re-opacifies col.a (the max()
                // below) so the wet foam edge stays solid. `depth` is READ-ONLY here (the sim waterline is
                // untouched — never depth/clip/_WaterLevel/the height read; P1 integrity, CLAUDE.md rule 5).
                // _ShallowTranslucency = 0 is an EXACT passthrough (col.a unchanged = today's opaque look).
                if (_ShallowTranslucency > 0.001)
                {
                    float shallowT = 1.0 - saturate(depth / max(_ShallowSeeThroughDepth, 1e-3));  // 1 at edge -> 0 deep
                    col.a *= lerp(1.0, _ShallowMinAlpha, shallowT * saturate(_ShallowTranslucency));
                }

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
                // ---- THE SHARED WAVE FIELD (ADR 0018 B1) — the PRIMARY swell source when trains are live ------
                // WaveFieldBridge publishes the eased, phase-continuous trains (count >= 1) whenever the sim
                // runs; count 0 (edit mode / bare art scene / cycle off) keeps the LEGACY SwellField path
                // below byte-for-byte, so the pre-B1 look is always reachable (ADR 0018 §(6): replace over a
                // transition, the tuned look survives). The owner's tuned _OceanSwell* values MAP onto the
                // field instead of resetting:
                //   _OceanSwellStrength  -> the brightness amplitude (identical role and scale to legacy);
                //   _OceanSwellSharpness -> the crest-shaping exponent on the 0..1 crest signal (its exact
                //                           legacy role — it shaped SwellField's crest the same way);
                //   _OceanSwellScale     -> a VISUAL wavelength scale, normalized to the property's shipped
                //                           default 0.025 so that default renders the field's TRUE
                //                           wavelengths (= what the hull rocks on); bigger = shorter waves,
                //                           the knob's legacy sense (SMALL = long wavelength).
                // NOT carried over (out of Arc B scope, ADR §(5): shore breakers are a later arc): the
                // legacy path's shoreward crest-bias — the trains run downwind everywhere. The foam DRIFT
                // shoreward bias below is untouched. All of it col.rgb-only dressing (P1, rule 5).
                #define WAVE_LEGACY_SCALE_REF 0.025
                float waveHeight;
                float2 waveSlope;
                float waveCrest;
                float wavePrimCos;
                float waveFreqScale = max(_OceanSwellScale, 1e-4) / WAVE_LEGACY_SCALE_REF;
                WaveFieldSample(Pixelize(worldXY), waveFreqScale,
                                waveHeight, waveSlope, waveCrest, wavePrimCos);
                bool trainsLive = _WaveFieldParams.x >= 0.5;

                float swellCrest;   // the 0..1 crest driver every downstream layer reads (spec bias,
                                    // whitecap crest gate, sky reflection lit faces)
                float swellSigned;  // the -1..1 brightness modulation (crests lighter, troughs darker)
                if (trainsLive)
                {
                    float waveTotalAmp = max(_WaveFieldParams.z, 1e-5);
                    float waveHN = saturate(waveHeight / waveTotalAmp);          // the 0..1 crest signal
                    swellCrest = pow(max(waveHN, 1e-6), max(_OceanSwellSharpness, 0.05));
                    // Brightness reads the SHARPENED crest (not raw height): a narrow bright ridge over a
                    // broad dark trough = the defined-crest look, instead of 4 summed trains smearing into a
                    // wide soft "white cloud". swellCrest is the already-sharpened 0..1 crest from the line
                    // above; remap 0..1 -> -1..1 so troughs still darken. This local feeds ONLY the
                    // brightness add below (no other consumer reads swellSigned — verified).
                    //   GLASS IS SACRED (ADR 0018 §(1)): on a truly flat field the remap would floor at -1
                    //   (waveHN 0 => swellCrest ~0 => -1) and paint a uniform dim wash on the mirror. Gate by
                    //   the field's UN-CLAMPED total amplitude (_WaveFieldParams.z, metres; 0 = dead glass)
                    //   so the band eases to 0 as the sea eases to glass. ~0.025 m of swell fully engages it;
                    //   any real sea reads the full defined-crest look. One madd + saturate, no new uniform.
                    float swellLive = saturate(_WaveFieldParams.z * 40.0);
                    swellSigned = (swellCrest * 2.0 - 1.0) * swellLive;
                }
                else
                {
                    // LEGACY noise swell — the cycle-off fallback, unchanged.
                    swellCrest = SwellField(worldXY, depth, t);   // 0..1 (rolls IN near shore)
                    swellSigned = (swellCrest - 0.5) * 2.0;       // -1..1
                }
                if (_OceanSwellStrength > 0.001)
                {
                    // 0.30 (was 0.25): a pinched crest covers less area than the old wide band, so a touch
                    // more gain restores the punch without a black sea (max swing = +/-0.30*_OceanSwellStrength;
                    // at the 0.16 default that is +/-0.048 — a defined ridge, not an over-dark trough).
                    col.rgb += swellSigned * _OceanSwellStrength * 0.30;
                }

                // ---- layer 5 caustics (shallows only; under the foam/spec so it reads as the seabed) ----------
                // Optional _CausticShallowBias pushes the caustic band a little DEEPER off the very edge (m),
                // so the day-dapple doesn't fight the see-through band where lowered alpha would fade it. 0 =
                // today's band (the veins still gate off at _CausticDepth). col.rgb dressing only (rule 5).
                float causticDepth = depth - _CausticShallowBias;
                float causticGate = 1.0 - saturate(causticDepth / max(_CausticDepth, 1e-3));   // 1 shallow -> 0 deep
                causticGate = saturate(causticGate);
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
                    // DAY GATE (Arc C, default OFF): fade the sun-dappled caustic add out at night so the light
                    // nets only show when the sun is UP. Driver is saturate(_SunElevation) — 1 at noon, 0 below
                    // the horizon (the RIGHT curve; NOT SunGlitterGate, which peaks at golden hour and is 0 by
                    // high sun, backwards for caustics). When the day/night cycle is NOT running (_DayNightTint
                    // sum ~ 0: editor / bare art scene) treat as full day — the same "unset" convention as
                    // NightFactor / the palette grade (NOT _SunElevation == 0, a real value at sunrise/sunset).
                    // _CausticDayGate = 0 = OFF (caustics always on = today's look). col.rgb only (rule 5).
                    float causticDnSum = _DayNightTint.r + _DayNightTint.g + _DayNightTint.b;
                    float causticSunUp = (causticDnSum > 1e-3) ? saturate(_SunElevation) : 1.0;
                    float causticDay = lerp(1.0, causticSunUp, saturate(_CausticDayGate));
                    col.rgb += _CausticColor.rgb * caustic * _CausticAmount * causticGate * causticDay;
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

                // ---- SKY CONTENT: drifting CLOUDS + the living MOON glitter path + faint STARS ----------------
                // This is a ¾ top-down game, so the water's reflection is the ONLY place the sky appears. On top
                // of the sky-COLOUR + sun mirror above, reflect SKY CONTENT: clouds drifting along the shared sim
                // wind (day + night), the MOON (a phase-shaped disc + a shimmering vertical glitter path that
                // RISES/ARCS/SETS across the night), and faint twinkling STARS. The moon/stars gate ON by night
                // (darkness from _DayNightTint); clouds read day + night. ALL of it inherits the SAME sea-state
                // fade as the mirror (strong on CALM/glassy water, gone in chop/storm — a storm doesn't mirror).
                // col.rgb ONLY, never depth/clip/the deep tint/the caustic gate/_WaterLevel (P1 integrity,
                // rule 5). The whole layer dials to nothing with _SkyReflectionStrength = 0.
                //
                // The content comes back SPLIT (see SkyContentReflection() above): the DAY share is added here —
                // after the sky-colour mirror but BEFORE the foam (whitecaps read over the sky), exactly where
                // the whole layer used to sit, so daylight is pixel-identical. The COMPENSATED share (the night
                // content: moon/glitter/stars + the clouds' night portion, PLUS the golden-hour SUN glitter
                // path, which is sun-gated rather than night-gated) is held back and added AFTER the palette
                // grade, compensated for the day/night multiply overlay — the complete-dark fix (see the
                // post-grade add below).
                float3 skyDayRGB;
                float3 skyNightRGB;
                SkyContentReflection(worldXY, surf, swellCrest, t, skyDayRGB, skyNightRGB);
                col.rgb += skyDayRGB;

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
                #if defined(_USE_WHITECAPTEX)
                    // Painted whitecap pattern (white-on-transparent) drifted WITH the body (the foam drift
                    // blend, not a fixed current scroll). Routed through UntileSampleW (like the other painted
                    // slots) so the small seamless tile's REPEAT GRID stops reading — dialed by _UntileStrength,
                    // kept pixel-snapped (PaintUV inside). Sampled ONCE here; each path below folds it in.
                    half4 capSample = UntileSampleW(TEXTURE2D_ARGS(_WhitecapTex, sampler_WhitecapTex),
                                          worldXY, _PaintScale, capDrift, _UntileStrength);
                    float capPat = max(capSample.a, dot(capSample.rgb, float3(0.299, 0.587, 0.114)));
                #endif
                    // SWELL-CREST GATE: lift the caps toward the swell crests so the foam rides the swell
                    // instead of speckling evenly. _FoamCrestGate dials it (0 = even, 1 = crest-only). With
                    // live trains, swellCrest IS the real advancing crest, so the tuned value now reads as
                    // "how tightly the foam hugs the moving crest" — the same knob, a truer crest.
                    float crestGate = lerp(1.0, swellCrest, saturate(_FoamCrestGate));
                    float capDens = FoamDensity();
                    // peak opacity ceiling for a NEWBORN cap (replaces the old hard 0.6); the milky residual
                    // sits below it.
                    float capPeak = saturate(_WhitecapPeakDensity);

                    float capOpacity;
                    if (trainsLive)
                    {
                        // ==== ADR 0018 B1 — WHITECAPS RIDE REAL CRESTS (the "foggy white soup" fix) ===========
                        // The LIFECYCLE places the foam on the advancing wave — FORMS on the front face as the
                        // crest builds, BREAKS crisp and bright at the tip, FADES to milky residual behind —
                        // and the evolving wind-streaked cap field TEXTURES it (patches along the crest line;
                        // the aniso coord above streaks the residual downwind — _FoamStreakStretch, reused).
                        // Because the crests ADVANCE (they ride the published trains), the foam visibly
                        // TRAVELS with the wave. Nothing here is a field-wide veil: every term is keyed to the
                        // crest's position and life, which is exactly what kills the static-soup read.
                        // The cap field TEXTURE source; the painted slot replaces it at its blend strength.
                        float capField = cap;
                    #if defined(_USE_WHITECAPTEX)
                        capField = lerp(capField, capPat, _WhitecapTexStrength);
                    #endif
                        float capMilkyT = smoothstep(capThr - capSoft, capThr + capSoft, capField);
                        float capCoreT  = SolidCore(capField, capThr, capDens);  // the dense, crisp-edged heart
                        float breakCore;
                        float residualLife;
                        WhitecapLifecycleWave(waveCrest, wavePrimCos, capDens, breakCore, residualLife);
                        // sea-state coupling THROUGH THE TRAINS' AMPLITUDES: full caps by _WhitecapOnsetAmp of
                        // total amplitude, first foam from ~10% of it. Glass = zero amplitude = zero foam,
                        // automatically (and crestF is already exactly 0 on a dead-glass sea).
                        float waveGate = smoothstep(_WhitecapOnsetAmp * 0.1, max(_WhitecapOnsetAmp, 1e-3),
                                                    _WaveFieldParams.z);
                        // BREAK: bright and crisp on the crest tip — the solid core's tight edge over the
                        // pixelized field reads as pixel-art foam edges, not soft alpha fog.
                        float solidPart = capCoreT * breakCore * capPeak;
                        // RESIDUAL: milky, trailing BEHIND the crest, streaked downwind by the aniso coord.
                        float milkyPart = capMilkyT * residualLife * lerp(0.45, capPeak, capDens);
                        capOpacity = saturate(max(solidPart, milkyPart)) * crestGate * waveGate * saturate(dt);
                    }
                    else
                    {
                        // ==== LEGACY path (no trains published — edit mode / bare art scene / cycle off) ======
                        // The pre-B1 noise-keyed dual-zone + lifecycle, unchanged (design doc §5.11): kept
                        // intact through the ADR 0018 §(6) transition so the tuned look is always reachable.
                        float capMask = capMilky * saturate(dt);  // deeper water
                    #if defined(_USE_WHITECAPTEX)
                        // coverage SCALES BY ROUGHNESS (the wind uniform) so caps appear/intensify with wind.
                        float capTexMask = capPat * saturate(_Roughness) * saturate(dt);
                        capMask = lerp(capMask, capTexMask, _WhitecapTexStrength);
                    #endif
                        capMask *= crestGate;
                        // DUAL-ZONE DENSITY + WAVE LIFECYCLE (form -> peak -> collapse) off the noise swell:
                        // a SOLID-WHITE CORE where the cap field is WELL above threshold, lifted by sea-state
                        // DENSITY, shaped by WhitecapLifecycle — born dense on the crest, aging into milky
                        // residual. col.rgb-only dressing — drives no depth/clip/_WaterLevel (P1, rule 5).
                        float capCore   = SolidCore(cap, capThr, capDens);            // 0..1: the dense solid heart
                        float life      = WhitecapLifecycle(swellCrest, capDens);     // form/peak/collapse density scale
                        float capSolid  = capCore * life * capPeak;                   // dense white on the breaking crest
                        float capMilkyOpacity = capMask * lerp(0.45, capPeak, capDens); // milky residual (scales gently with sea-state)
                        capOpacity = saturate(max(capMilkyOpacity, capMask * capSolid));
                    }
                    col.rgb = lerp(col.rgb, _FoamColor.rgb, capOpacity);
                }

                // ---- STORM FOAM LANES: long downwind foam streaks in a blow (col.rgb ONLY; Arc C, default OFF)
                // Added in the SAME pre-grade dressing zone the foam + whitecaps occupy (so the palette
                // guard-rail below bounds them AND so they DIM with the night like the rest of the foam - the
                // opposite of the night-visible rain rings added post-grade below). Keyed to the WIND
                // (_WindDir/_Roughness - the blow), a MONOTONE gate: gone on calm, strong in a gale. dt (the
                // depth key) is READ-ONLY here (never depth/clip/_WaterLevel/the sim - P1, rule 5).
                col.rgb += StormFoamLanes(worldXY, dt, t);

                // ---- CURRENT DRIFT LINES: faint streaks tracing the tidal set (col.rgb ONLY; Arc C, default OFF)
                // Added in the same pre-grade dressing zone the foam + whitecaps occupy, so the palette guard-rail
                // below bounds them. Reads the CURRENT (_FlowDir/_Flow — the SMOOTHED tidal set) so the lines
                // "read the tide" for free; a BELL over _Chop keeps them off dead glass AND out of a storm.
                // dt (the depth key) is READ-ONLY here (never depth/clip/_WaterLevel/the sim — P1, rule 5).
                col.rgb += DriftLines(worldXY, dt, t);

                // ---- PALETTE GUARD-RAIL: the final soft grade of the SEA itself (col.rgb ONLY; ADR 0015) -------
                // Bound + gently pull the composited colour into the art-directed palette so it never washes out
                // or goes muddy, while keeping the dynamic diversity. The value FLOOR is DAY/NIGHT-AWARE — it
                // pre-compensates for the day/night overlay's downstream MULTIPLY (ADR 0013) so daylight never
                // goes muddy while true night still goes genuinely dark. dayNightLuma is the luminance of the
                // global _DayNightTint the overlay multiplies the frame by; when the cycle is NOT running the
                // global is near-black (the same "unset" convention the reflection/specular use) -> treat it as
                // full daylight (dnLuma = 1, the daylight rail) so a bare art scene / editor preview grades to
                // the daylight palette, never a phantom-dark one. col.rgb ONLY: this never touches depth/clip()/
                // _WaterLevel/the height read/the sim (P1 integrity, CLAUDE.md rule 5). Strength 0 = today.
                // (dnSum/dayNightLuma are computed BEFORE the light content below — both stages read them.)
                float dnSum = _DayNightTint.r + _DayNightTint.g + _DayNightTint.b;
                float dayNightLuma = (dnSum > 1e-3)
                    ? PaletteLuma(_DayNightTint.rgb)   // cycle running: the real multiply luminance (1 day .. ~0 night)
                    : 1.0;                             // cycle off / unset: full daylight rail (no phantom dark floor)
                col.rgb = PaletteGrade(col.rgb, dayNightLuma);

                // ---- THE BOAT SPOTLIGHT: REVEAL the water inside the cone (searchlight, not floodlamp) ----------
                // The beam no longer PAINTS an amber slab (the old purely-additive _BoatLightColor term that
                // over-wrote the few-percent night sea into a flat wash — the owner's 2026-07-05 night playtest).
                // Instead it REVEALS: BoatLightTerm returns a SCALAR cone weight, and we MULTIPLY-BRIGHTEN the
                // water's OWN col.rgb inside the cone — so crests/foam/troughs/depth all scale up TOGETHER and stay
                // readable, merely LIT. A FAINT warm additive tint (scaled by the same weight) rides the SAME
                // post-grade overlay-compensated bucket as the sky content below (so it survives the deep-night
                // multiply). The multiply-lift itself operates on the ALREADY-COMPOSITED (post-grade) water and is
                // NOT separately compensated: a multiply of the water scales with the water through the downstream
                // day/night overlay, so lit water tracks the sea it lights (a floodlamp-flat compensation would
                // re-introduce the wash). Weight 0 (by day / outside the cone / no boat) => an EXACT passthrough.
                // col.rgb ONLY — never depth/clip/_WaterLevel/the height read/the sim (P1 integrity, rule 5).
                float beamW = BoatLightTerm(worldXY);
                col.rgb *= (1.0 + beamW * max(_BoatLightBrighten, 0.0));   // the REVEAL: lift the water's own colour

                // ---- LIGHT CONTENT, post-grade + overlay-compensated: BEAM WARM TINT + the NIGHT SKY ------------
                // The beam's faint warm TINT (a small additive warmth biased by the cone weight, NOT a slab) and
                // the compensated sky share (the night content — moon disc/glitter/stars + the clouds' night share
                // — plus the golden-hour SUN glitter path, which rides this bucket so the dusk tint can't mute its
                // warm gold) are added LAST, after the palette grade, pre-compensated for the day/night multiply
                // overlay — the complete-dark fix. Two crushers demanded this exact position:
                //  (1) The OVERLAY: the whole frame is multiplied by _DayNightTint after this shader (ADR 0013);
                //      at deepest night that is ~(0.022, 0.029, 0.061) — an uncompensated add survived at ~3-6%,
                //      blue-shifted (the owner's "spotlight/moon vanish in complete dark"). Dividing the add by
                //      max(_DayNightTint.rgb, DN_COMP_MIN_CHANNEL) cancels the multiply exactly at the shipped
                //      deepest night (all channels exceed the floor; see the constant's comment + HDR dependency).
                //  (2) The GRADE: at deep night PaletteValueFloorDayNight saturates (floorPre = 1) and pulls ALL
                //      pre-overlay water toward luma 1 at _PaletteGradeStrength — lit and unlit alike — which
                //      FLATTENS the beam/moon against their surroundings. Post-grade, the lit pool keeps its
                //      authored contrast; the rail still bounds the SEA the light sits on. (With HDR on, the >1
                //      compensated values also must NOT pass through the grade's value ceiling.)
                // The cycle-off branch (dnSum ~ 0: edit mode / bare art scene / demo) adds the content RAW — no
                // overlay is running, so there is nothing to compensate (preserves the tuning/preview look).
                // Every term is its own gate: the beam's warm tint carries the night-gate + intensity-0 the cone
                // weight already applied (0 by day / no boat); skyNightRGB is 0 at HIGH SUN (the night content
                // gates off by day, the sun glitter gates off by ~0.5 elevation) — so at MIDDAY this whole block
                // adds 0 and the look is pixel-identical; at golden hour it carries the intended sun glitter, at
                // night the moon/stars + the beam's faint warmth.
                // Sorting-INDEPENDENT (part of the water's own frag) — it cannot fail the way the land quad did
                // over water. col.rgb ONLY — never depth/clip/_WaterLevel/the height read/the sim (P1, rule 5).
                // OWNER RULING (2026-07-05): the SURFACE RAIN RINGS join this POST-GRADE, overlay-COMPENSATED
                // bucket (beside the beam's warm tint + moon/sun glitter) - NOT the pre-grade dressing - so the
                // downstream day/night MULTIPLY (ADR 0013) cancels and a night squall STILL shows rain on black
                // water (day AND night). They ride the exact same dnSum branch below: compensated when the cycle
                // runs (divided by max(_DayNightTint.rgb, DN_COMP_MIN_CHANNEL)), raw when it is off (edit mode /
                // bare art / demo). _RainIntensity is the C#-DERIVED gate (0 => the rings add nothing, so a
                // clear/calm sea is pixel-identical). col.rgb ONLY (P1, rule 5).
                // The beam's WARM TINT is the cone weight × _BoatLightColor × _BoatLightTintAmount — a faint warmth
                // biased to the lit pool, NOT the old colour slab; the REVEAL (multiply-lift) already happened above.
                float3 beamTint = _BoatLightColor.rgb * (beamW * max(_BoatLightTintAmount, 0.0));
                float3 lightContent = beamTint + skyNightRGB + RainRings(worldXY, dt, t);
                col.rgb += (dnSum > 1e-3)
                    ? lightContent / max(_DayNightTint.rgb,
                                         float3(DN_COMP_MIN_CHANNEL, DN_COMP_MIN_CHANNEL, DN_COMP_MIN_CHANNEL))
                    : lightContent;

                return col;
            }
            ENDHLSL
        }
    }

    Fallback Off
}
