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

        [Header(DEEP BLUE enrichment (bounded pull toward a rich navy in DEEP water   col.rgb only))]
        // The owner's "deep blues" ask (2026-07-08). The shipped material reads its base colour from his
        // HAND-PAINTED _DepthRamp (_USE_DEPTHRAMP is ON), whose deep end lands on a muted slate-teal — so
        // this pulls the settled BASE colour toward a rich navy as the water deepens WITHOUT repainting his
        // art. Keyed to the READ-ONLY deep fraction (dt), applied BEFORE every additive layer (the #182
        // swell-read, the swell bands, spec and foam all ride on top at FULL amplitude — nothing is washed
        // out) and BEFORE the palette guard-rail (ADR 0015), which stays the final colour owner and bounds
        // the result like every other layer. Pre-grade water colour (not light content), so it dims with
        // the night overlay like the rest of the sea. col.rgb ONLY — never depth/clip()/dt/_WaterLevel/the
        // sim (P1 integrity, CLAUDE.md rule 5). _DeepBlueStrength = 0 is an EXACT passthrough.
        _DeepBlueStrength ("Deep blue strength (0 = the painted ramp as-is)", Range(0,1)) = 0.45
        _DeepBlueColor    ("Deep blue target (rich navy)", Color) = (0.02, 0.09, 0.30, 1)
        _DeepBlueStart    ("Deep blue onset (fraction of the deep ramp)", Range(0,1)) = 0.25

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

        // ---- ADR 0027 #4 + #9: band wavelengths scale with SEA STATE; speeds DERIVED by dispersion ----
        // (both default OFF = today's fixed wavelengths + hand-set speeds EXACTLY — the passthrough
        // contract; C# twin: WaterDispersion, change one change BOTH in the same PR).
        // #4 — real seas grow in WAVELENGTH as they build, not only amplitude: each visual noise band's
        // spatial frequency divides by (1 + response * _Chop), so its wavelength grows linearly in the
        // already-pushed sea state. Applies to the VISUAL octaves only (_NoiseScale / _WindChopScale /
        // _CrossSwellScale and the legacy _OceanSwellScale band): the wave-field trains already carry
        // this law at the sim level (WaveMath grows the dominant wavelength with wind), and the field's
        // freqScale mapping moves drawn geometry the interior-mask/clamp stack guards — deliberately
        // untouched (see the PR's consumer audit + the _OceanSwellScale incident).
        _BandScaleResponse ("Band wavelength growth with sea state (0 = fixed / today)", Float) = 0.0
        // #9 — each octave carried an independent hand-tuned speed with NO relation to its wavelength
        // (the legacy set is ANTI-dispersive: the 40 m swell band scrolls slower than the 1.4 m chop) —
        // a large part of why the sea reads as stacked layers sliding over each other. The master blends
        // each band from its legacy speed toward bandMult * sqrt(g*lambda/2pi); the per-band multipliers
        // keep the owner's art direction (their 0.06 default anchors the WIND-CHOP band on its legacy
        // 0.09 m/s at full dispersion, so the master re-ties the slower bands to physics around an
        // unchanged fastest band). Shallow water: the bounded static shoal drift below bunches + slows
        // wavefronts off the SAME read-only depth every other layer consumes.
        // ⚠️ The PROPERTY DEFAULT below is 0, but the SHIPPED materials carry 0.5 (#382 turned this on).
        // Read every "at _DispersionScale = 0" note in this file as describing the PASSTHROUGH CONTRACT,
        // never the live sea. Three comments used to say "(the shipped value)" beside that 0 and were
        // simply wrong; the gap cost real time, because it is what silently un-froze three rendering
        // acceptance guards — they zero the hand-set speeds, which stops mattering the moment this master
        // blends half of each band's rate toward the derived one (see MakeStatic in
        // WaterWhiteoutShoreSwirlAcceptanceTests).
        _DispersionScale     ("Dispersion blend (0 = hand-set speeds EXACTLY, 1 = derived; SHIPPED 0.5)", Range(0,1)) = 0.0
        _DispersionChopMult  ("Dispersion feel mult: wind chop band", Float) = 0.06
        _DispersionCrossMult ("Dispersion feel mult: cross-swell band", Float) = 0.06
        _DispersionSwellMult ("Dispersion feel mult: legacy ocean-swell band", Float) = 0.06
        _DispersionShoalBunch ("Shoal bunching (x wavelength of drift at zero depth)", Range(0,4)) = 1.0

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

        [Header(Swell READ legibility (crest trough VALUE contrast   time the heave))]
        // THE lever for "I can see the swell rise and pass under the boat." The passing swell IS the
        // shared deterministic wave field the hull rocks on and the trap-haul times against
        // (WaveFieldSample reads the trains WaveFieldBridge publishes from the sim — ADR 0018). The stock
        // crest/trough brightness (_OceanSwell*) is tuned SUBTLE (and pinched sharp), so a rising swell is
        // nearly invisible — you cannot time a heave to a wave you cannot read. This is a dedicated,
        // ON-by-default legibility knob that AMPLIFIES the crest->trough VALUE contrast of that SAME crest
        // signal so a swell reads as a raised, MOVING band of light over dark that lifts and passes under
        // the boat. Value contrast is the single biggest readability win and it works on calm water too.
        // It keys the BROAD normalized crest (not the pinched spike) so the swell reads as the water
        // RISING, not a thin line. Keyed to the REAL advancing crest -> SEE == FEEL (P1). Its own gate,
        // INDEPENDENT of _OceanSwellStrength, so it reads even where the owner dialed the stock swell down.
        // GLASS STAYS GLASS: it inherits the wave field's amplitude gate, so a dead-flat sea shows no band —
        // and the CALM GATE below (_SwellReadSeaStateLo/Hi) melts the read away over the whole glass-to-calm
        // range, because the amplitude gate alone engages at ~0.025 m and let a calm day read banded.
        // col.rgb ONLY: it ADDS to the colour like every water layer and NEVER touches depth/clip/the deep
        // tint/_WaterLevel/the sim wave field (P1 integrity, CLAUDE.md rule 5) — the waterline the player
        // wades and the crest the haul samples are byte-identical. _SwellReadStrength = 0 = EXACT passthrough.
        //   _SwellReadStrength — master contrast amount (0 = off / stock look; ~0.35 = a clearly legible swell).
        //   _SwellReadBands    — posterize the moving band into N discrete VALUE steps for a crisp pixel-art
        //                        marching-contour read (0 = smooth). Mirrors _DepthBands / _SpecBands.
        _SwellReadStrength ("Swell read contrast (0 = off, ~0.35 = legible)", Range(0,1)) = 0.35
        _SwellReadBands    ("Swell read posterize bands (0 = smooth)", Float) = 0

        [Header(Swell FACE SHADING (lit face   shaded back   the modelled wave))]
        // The owner's "better looking waves" ask (2026-07-08). The shared wave field's ANALYTIC slope
        // (waveSlope — already computed by WaveFieldSample, previously unused in the composite) tilts each
        // swell face toward or away from the ONE implied sun (_SunDir, falling back to the material's
        // _LightDir — the ADR 0006 single-light discipline, the same fallback the specular uses). A face
        // whose downhill side looks at the sun is LIT; the back of the crest is SHADED. Where the #182
        // swell-read band is SYMMETRIC (crest bright / trough dark), this is ANTISYMMETRIC (lit face vs
        // shaded back) — the two COMPOSE into a modelled, directional wave instead of doubling one band
        // into glare. Self-gating: dead glass publishes zero amplitude => zero slope => zero term, and the
        // legacy no-trains path leaves waveSlope at 0 (the pre-B1 look is untouched there). col.rgb ONLY —
        // never depth/clip()/the deep tint/_WaterLevel/the sim wave field (P1 integrity, CLAUDE.md
        // rule 5). _SwellFaceShade = 0 is an EXACT passthrough.
        _SwellFaceShade ("Swell face shading (0 = off / flat bands)", Range(0,1)) = 0.22

        [Header(Modelled swell CALM gate (glassy calm shows no read))]
        // Owner playtest (2026-07-08): "i can definitely notice the swells now better. although i still do
        // see them at calm." The wave field's own amplitude gate (swellLive) fully engages at ~0.025 m, so a
        // small-but-real calm-day swell still earns the full ~3x legibility contrast — a calm sea read as
        // moving bands, not glass. This gate melts BOTH modelled-swell reads (the #182 legibility band AND
        // the #185 face shading — they are one modelled swell, so one shared pair) away as the sea flattens:
        // a smoothstep RISE over _Chop (== EnvironmentSample.SeaState01, the same axis the drift-line window
        // keys — the _DriftLineSeaStateLo/Hi precedent). MONOTONE, not the drift-line BELL: drift lines are
        // delicate texture a storm erases, but the swell read must SURVIVE a heavy sea — the haul is timed
        // against the swell precisely when the sea is up. Defaults key the canon sea-state bands (SeaState01
        // equals enum/7 at every band edge): Lo 0.28 sits just under the Calm-to-Light edge (2/7) so ALL of
        // Glass + Calm shows essentially no read; Hi 0.45 sits just past the Light-to-Moderate edge (3/7) so
        // the read ramps in across Light and a moderate sea keeps today's full read (gate ~0.96 at the
        // Moderate onset). Set BOTH to 0 to disable the gate (today's pre-gate look on any non-glass sea).
        // col.rgb ONLY: it merely scales the two existing pre-grade adds — never depth/clip/the deep tint/
        // _WaterLevel/the sim wave field (P1 integrity, CLAUDE.md rule 5); the stock _OceanSwellStrength
        // band is NOT gated (that is the owner's tuned-subtle base look, not the amplified read).
        _SwellReadSeaStateLo ("Swell read sea-state rise (_Chop; at or below = glassy)", Range(0,1)) = 0.28
        _SwellReadSeaStateHi ("Swell read sea-state full (_Chop; above = today's read)", Range(0,1)) = 0.45

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

        [Header(Foam CLUMPING (windrows and rafts   foam gathers in patches   col.rgb only))]
        // The owner's "better clumping of foam" ask (2026-07-08): the open-water whitecaps read as an EVEN
        // SPRINKLE — statistically uniform speckle however organic each fleck is. Real foam GATHERS: wind
        // rows (long lanes of foam down the wind) and rafts shed by breaking crests, with stretches of
        // bare water between. This is a second, much BROADER and SLOWER evolving field — stretched along
        // the wind like the caps and drifting with the same foam drift, so the patches travel with the
        // weather — that REDISTRIBUTES the cap coverage: inside a patch the caps read a touch denser,
        // between patches they thin toward bare water. The same foam, gathered instead of sprinkled.
        // Reuses EvolvingField + Pixelize (no new noise machinery; pixel-art faithful, §3). col.rgb/col.a
        // foam dressing ONLY — never depth/clip()/the deep tint/_WaterLevel/the sim (P1 integrity,
        // CLAUDE.md rule 5). _FoamClumpStrength = 0 is an EXACT passthrough (today's even sprinkle).
        //   _FoamClumpStrength — master: how strongly the foam gathers (0 = even sprinkle, 1 = fully
        //                        patch-gated with bare lanes between rafts).
        //   _FoamClumpScale    — the patch frequency (patches/unit). SMALLER = broader rafts and wider
        //                        clear lanes; larger = tighter, busier patchwork.
        //   _FoamClumpStretch  — anisotropy along the WIND (1 = round rafts; higher = long thin windrows).
        _FoamClumpStrength ("Foam clump strength (0 = even sprinkle / today)", Range(0,1)) = 0.55
        _FoamClumpScale    ("Foam clump scale (patches/unit; smaller = broader rafts)", Float) = 0.10
        _FoamClumpStretch  ("Foam clump wind stretch (1 = round rafts, higher = windrows)", Float) = 2.5

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

        // ---- ADR 0027 #3: CONVERGENCE (Jacobian) foam gate (default OFF = today) ----------------------
        // Foam today is tall-wave only (_FoamCrestGate + the whitecap lifecycle above), so CROSSING
        // trains never foam at their intersections. Where the surface PINCHES — the Jacobian of the
        // Gerstner-style drift toward crests dropping below 1 — an ADDITIONAL placement driver opens
        // ALONGSIDE the crest factor, never replacing it (the confused-sea read). The convergence term
        // is finite differences of the SAME WaveFieldSample the crests ride (C# twin:
        // WaterFoam.Convergence — change one, change BOTH in the same PR); its output is textured by
        // the existing thresholded/banded cap field, so it inherits the existing quantization.
        _FoamConvergenceStrength ("Convergence foam strength (0 = off / today)", Range(0,1)) = 0.0
        _FoamConvergencePinch    ("Convergence pinch (m; ~Gerstner Q/k — drift toward crests)", Float) = 4.0
        _FoamConvergenceStep     ("Convergence sample step (m)", Float) = 0.5

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

        [Header(Beach swash   always on shoreline wash   the water runs IN and OUT)]
        _SwashAmplitude    ("Swash amplitude (m of contour excursion)", Float)   = 0.3
        _SwashSpeed        ("Swash speed (run-ups per sec; 0.16 ~ one every 6 s)", Float) = 0.5
        _SwashWavelength   ("Swash shoreward wave spacing (per m depth)", Float) = 1.2
        _SwashAlongShoreVary ("Swash along-shore desync (0..1, subtle)", Range(0,1)) = 0.35
        // How much of the swash reaches the DRAWN water edge rather than only the foam band.
        // 0 = the pre-2026-08-01 behaviour exactly (foam moves, the water's edge does not, so nothing
        // reads as water running in and out — the owner's report). Above 0 the wet edge itself advances
        // and recedes, BOUNDED by _SwashMaxEdgeShift metres of equivalent level. This is a deliberate,
        // bounded SEE != FEEL divergence: gameplay never reads the fragment, and the cap sits well inside
        // the standing "wade ~0.5 m" tolerance, so nowhere the player can stand changes meaning.
        _SwashEdgeShift    ("Swash moves the DRAWN edge (0 = foam only)", Range(0,1)) = 0.6
        _SwashMaxEdgeShift ("Swash drawn-edge cap (m of level; keep under the wade tolerance)", Range(0,1)) = 0.35
        // Glass should be near-still. Fades the swash toward this floor as the sea-state falls, reusing
        // the swell-read gate's axis (_SwellReadSeaStateLo/Hi) rather than inventing a second one.
        // 0 = no calm fade (swash identical at every sea-state); 1 = dead-still on glass.
        _SwashCalmGate     ("Swash calm fade (0 = same at all seas, 1 = still on glass)", Range(0,1)) = 0.7

        [Header(BREAKING WAVES (ADR 0040)   surf where the painted bottom and the tide say so)]
        // WHERE the surf is, is not tunable here and deliberately so: it is decided by the painted
        // seabed and the tide (depth = _WaterLevel - seabed) through the contour BreakerMath solves on
        // the sim tick. These knobs are the LOOK only — how the surf reads once physics has put it
        // somewhere. Widening or shifting the band from here would be painting surf on, which is the
        // one thing this arc exists not to do.
        _SurfStrength      ("Surf strength (0 = OFF, the exact passthrough)", Range(0,1)) = 1
        _SurfColor         ("Surf / whitewater colour", Color) = (1,1,1,1)
        // The break line itself is denser than the whitewater trailing off it — a spilling crest
        // crumbles white at the top and thins as the bore runs in. 1 = flat (no crest emphasis).
        _SurfCrestBoost    ("Break-line density boost (1 = flat band)", Range(1,3)) = 1.6
        _SurfCrestWidth    ("Break-line width (m past the break the boost fades over)", Float) = 3
        _SurfNoiseScale    ("Surf churn blob scale", Float) = 0.9
        _SurfEvolveSpeed   ("Surf churn boil rate", Float) = 0.5
        _SurfThreshold     ("Surf churn threshold (metaball merge point)", Range(0,1)) = 0.42
        _SurfThresholdSoft ("Surf churn soft band", Range(0.001,0.5)) = 0.16
        // Posterize like every other band in this shader, with the same world-Bayer dither at the
        // edges — surf that ramps smoothly reads as airbrushed 3D, not this game.
        _SurfBands         ("Surf posterize bands (0 = smooth)", Float) = 4
        _SurfBandDither    ("Surf band dither (0 = hard steps)", Range(0,1)) = 0.7
        // Owner ruling 2026-08-28: where the sea is actually breaking, the computed whitewater takes the
        // shore fringe's place — the fringe was always the geometric stand-in for it. 0 = keep both
        // (today's look exactly, the passthrough); 1 = physics wins wherever the whitewater is alive.
        _SurfSupersedeFringe ("Surf supersedes the shore fringe (0 = keep both)", Range(0,1)) = 1

        [Header(PLUNGING anatomy   the lip the barrel the pocket   ONLY where the slope earns it)]
        // These dress a break the SEABED has already classified as plunging (Iribarren, Battjes'
        // thresholds, published in _BreakerAnatomy). None of them can put a barrel anywhere: at
        // spilling slopes the plunging weight is 0 and every term below multiplies out. That is the
        // claim ADR 0040 makes and these knobs must never be able to relax it.
        _SurfPlungeStrength ("Plunging anatomy strength (0 = spilling look everywhere)", Range(0,1)) = 1
        // The crest of a plunging breaker outruns its own base and lands AHEAD of it. Metres of throw
        // per metre of (depth-limited) wave height.
        _SurfLipThrow      ("Lip throw (m of forward throw per m of wave height)", Range(0,3)) = 1.1
        _SurfLipWidth      ("Lip width (m)", Float) = 1.2
        _SurfLipColor      ("Lip colour (the thrown crest, brightest thing in the surf)", Color) = (1,1,1,1)
        // The BARREL is the hollow the thrown lip encloses — it is dark because it is in the lip's
        // shadow, which is the whole reason a tube reads as a tube.
        _SurfBarrelShade   ("Barrel shading (how dark the hollow under the lip goes)", Range(0,1)) = 0.55
        _SurfBarrelColor   ("Barrel colour (the shadow inside the curl)", Color) = (0.16,0.26,0.34,1)
        // The POCKET is the powerful peeling zone beside the curl: breaking hard, broken JUST now.
        _SurfPocketWidth   ("Pocket width (m past the break it stays powerful)", Float) = 4
        _SurfPocketBoost   ("Pocket density boost", Range(1,3)) = 1.7

        [Header(THE BORE   ADR 0040 rev 3   one crest at a time   all three ship at 0 which is today)]
        // The surf's CLOCK (BreakerMath's bore: the field's published phase at the break line, carried
        // inshore by the march's own travel time) exists per pixel whether or not these dials use it.
        // Each dial is an exact passthrough at 0 — the steady boil, today's look — and the check-in
        // plates were shot with all three at 1. The physics (pulse sharpness, sets, Hunt's run-up and
        // its cap) are GameConfig.Breakers, published in _BreakerBore; these are the look only.
        _SurfBeatStrength  ("Bore beat: the sheet and the lip/barrel/pocket pulse with the crest (0 = steady)", Range(0,1)) = 0
        _SurfRunUpStrength ("Bore run-up moves the drawn wet edge where a bore is alive (0 = the cosmetic swash only)", Range(0,1)) = 0
        _SurfFrontSlope    ("Bore front relief: the breaking face for the lamp and the sun (0 = no face)", Range(0,2)) = 0
        // Read by C# only (WaterSurface mirrors it to the foam registry like _WakeFoamStrength): the
        // ADVECTED wake buffer gains a source under every bore front, so a wash leaves foam BEHIND it
        // that ages, drifts and dies on the buffer's own clocks. Needs _WakeFoamStrength > 0 to draw.
        _SurfDepositStrength ("Bore foam DEPOSIT into the advected wake buffer (0 = none)", Range(0,2)) = 0

        [Header(Shore band quantization   the 8 bit height map is why the foam edge draws LINES)]
        // The foam band's edge is an ISO-CONTOUR of a depth that inherits the seabed height texture's
        // 8-bit quantization (3.91 cm per code over the -4..+6 m range, bilinear at ~2 px/m). Where the
        // seabed is near-flat — the sandbar flats, exactly where the owner photographed the defect — one
        // code step spans METRES, so an entire texel row crosses the smoothstep at once and the edge
        // snaps to the texel lattice as a straight line. Offsetting the contour per Bayer cell scatters
        // the crossing depth across the row, which is the same instrument every OTHER band in this shader
        // already uses. In metres of depth; ~half a height code is the natural size. 0 = the old contour.
        _FoamEdgeDither    ("Foam edge dither (m of depth; 0 = the old lattice-locked contour)", Float) = 0.02
        // The shore cosmetics (fringe wiggle + swash) scale their depth offsets by the LOCAL seabed slope
        // so the authored amplitudes read as CONTOUR metres on any coast (the 2026-07-23 swirl fix). On
        // the flats the measured gradient is ~0 — the height map is literally flat across whole texel
        // runs — which multiplied both to nothing precisely where they were needed. This floors the slope
        // the cosmetics scale by, so a flat beach behaves like a flat beach (long, low run-up) instead of
        // a dead one. Deliberately below the 0.18 m/m reference coast the swirl guard renders, so every
        // slope that guard measures is untouched. 0 = the old slope-blind-on-flats behaviour.
        _ShoreSlopeFloor   ("Shore cosmetic slope floor (m/m; flats keep breakup + swash)", Range(0,1)) = 0.15

        [Header(Organic shore fringe (LOOK ONLY prototype   default OFF   ADR 0012))]
        // A revertible, defaults-off COSMETIC prototype (ADR 0012 exploration) so the owner can SEE an
        // organic, wiggly coast on the glassy-calm St Peters bar and judge it by feel. It perturbs a
        // LOCAL, foam-only "cosmetic depth" (depthC) near the waterline with pixel-grid-quantized noise,
        // and feeds THAT into the VISIBLE shore read ONLY — the see-through-shallows alpha fringe + the
        // foam/shallow band. It NEVER touches the real `depth`/`clip()` (the gameplay waterline where the
        // player wades), the deep tint, the caustic gate, or _WaterLevel — so the sim/walkability edge is
        // untouched by construction (P1 integrity, CLAUDE.md rule 5). Unlike the chop-gated `warp`, the
        // wiggle is ALWAYS-ON (not sea-state-gated) so it reads even on dead-calm glass — that is the point.
        // Sim-true promotion (mirroring this into the real clip contour / PaintedHeightField) is a SEPARATE
        // owner-gated decision (ADR 0012), deliberately NOT done here.
        //   _ShoreNoise      — cosmetic fringe amplitude (m). 0 = byte-identical to today's clean iso-contour.
        //   _ShoreNoiseScale — the pixel-grid noise frequency (bigger = finer, busier wiggle; smaller = broad lobes).
        //   _ShoreNoiseBand  — the depth (m) half-band around the waterline the fringe lives in (0 outside it).
        _ShoreNoise        ("Shore fringe amount (m, 0 = off / today's clean edge)", Float) = 0
        _ShoreNoiseScale   ("Shore fringe noise scale (bigger = finer wiggle)", Float)      = 0.6
        _ShoreNoiseBand    ("Shore fringe depth band (m around the waterline)", Float)      = 0.8

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

        // ---- THE MIRROR'S FORM (owner ruling 2026-09-02: "we need reflections on water") ---------------
        // The reflected CONTENT was never the problem — the sky colour, the clouds, the moon's disc and its
        // glitter path, the stars, the object reflections are all what he means by reflections, and they
        // stay. The FORM was: at calm the sheen above is a sin() of world-Y at a FIXED 1.6 m wavelength,
        // cubed — a striped rug, and the plate diagnostic showed the stripes ARE the reflection (zeroing
        // _ReflectionStrength took the glass sea's mean luma from 0.46 to 0.05; register row 5).
        //
        // A mirror does not have a wavelength. It has a SURFACE: a level facet returns the sky to the eye,
        // a tilted one returns something else, so the mirror breaks up exactly where the water TILTS. That
        // is what these drive it off — the shared wave field's OWN analytic slope, the same one the swell
        // face shading and the lamp's relief read (ADR 0018: one field, one slope, one computation), at the
        // drawn frequency scale and already pixelized in world space. A dead calm has no tilt anywhere and
        // reads as a sheet of sky; a light air puts a slow, broad, organic shimmer on it; chop dissolves it
        // without any need for a chop RAMP, because chop IS tilt (register row 13).
        //
        // _MirrorForm 0 = today's stripe, EXACTLY. col.rgb ONLY (P1, rule 5).
        _MirrorForm      ("Mirror form (0 = the shipped 1.6 m stripe; 1 = the surface's own tilt)", Range(0,1)) = 1
        _MirrorSheen     ("Mirror sheen (how much sky a LEVEL facet returns)", Range(0,2)) = 0.3125
        _MirrorTiltScale ("Mirror tilt break-up (how fast a tilted facet stops returning the sky)", Range(0,40)) = 6
        _ReflectionSunStreak   ("Reflection sun streak intensity", Range(0,2)) = 0.9
        _ReflectionSunSharp    ("Reflection sun streak sharpness", Float) = 6.0

        [Header(Object reflections (ADR 0027 num 8)   the HHReflect list warped by the wave field)]
        // Boats, wharf and bankside trees REFLECT in the water. _HHReflectTex is a fourth filtered
        // renderer list (LightMode HHReflect, gated on ReflectionRegistry.RenderingLayer) drawn
        // MIRRORED about each renderer's published ground-contact pivot (ADR 0026) by
        // IsoFacetHullFeature; this shader samples it with the lookup WARPED by the SAME
        // WaveFieldSample() the hull rides, so a reflection wobbles on the very crests the boat is
        // riding. No new sim uniform: the warp reuses the shared field (ADR 0018).
        //   ⚠️ THE LOOKUP SNAPS IN WORLD SPACE, never screen/RT space. The RT is screen-space by
        //   nature, and with CameraFollow panning behind the boat a screen-snapped lookup CRAWLS on
        //   every pan — the one artefact that would make this read as a screen filter instead of a
        //   reflection. Twin: WaterReflectionWarp.WarpedSampleWorld (and ScreenSnappedSampleWorld,
        //   which exists only so the tests can measure the crawl it causes).
        //   Sea-state response is INHERITED, not re-invented: ReflectionStrength()/Sharpness() above.
        //   Zero cost when idle: with no ReflectiveObject alive the feature enqueues no pass, the
        //   fallback 1x1 clear texture is bound, and the block below is skipped at strength 0.
        _ObjectReflectStrength ("Object reflection strength (0 = off / today)", Range(0,1)) = 0.0
        _ObjectReflectWarp     ("Object reflection wave warp (m per unit slope)", Float) = 0.35
        _ObjectReflectSink     ("Object reflection sink with depth (0 = no fade)", Range(0,1)) = 0.35

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
        // The clouds' NIGHT share is MOONLIT (owner playtest 2026-07-23: the compensated full-strength night
        // clouds veiled the dimmed sea white). Scaled by the moon's presence x brightness; this dials the
        // faint moonlit remainder. 1 = the pre-fix full-strength night clouds EXACTLY; 0 = clouds gone at night.
        _CloudMoonlitVis  ("Cloud night visibility under a lit moon (1 = legacy full)", Range(0,1)) = 0.35
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
        [NoScaleOffset] _WhitecapTex ("Whitecap STAMP SHEET (white-on-transparent, seamless 256)", 2D) = "white" {}
        _WhitecapTexStrength ("Whitecap tex blend (0=proc, 1=painted)", Range(0,1)) = 1.0
        // ⚠️ The whitecap slot has its OWN scale, and it is not a mood — it is what stops the caps
        // being a GRID (owner, 2026-08-05: "some of the whitecaps have a square pattern I recognize
        // from earlier builds"). At _WhitecapTexStrength 0.865 this slot, not the procedural evolving
        // field, is what PLACES open-water caps; the art in it used to be ONE mark in a 64 px tile on
        // the shared _PaintScale 0.25 grid, i.e. one identical whitecap every 4 m, in rows. Untiling
        // cannot fix that — UntileSampleW hides a repeat by TRANSLATING whole cells, which moves a
        // lattice rather than breaking it (it works on _FoamTex because that tile is busy and varied;
        // a tile whose whole content is one dot has nothing to hide behind). The slot now samples a
        // 256 px SHEET of 16 scattered stamps, and this scale is the value that keeps the owner's
        // mark exactly the size it already was: 256 x 0.0625 = 64 x 0.25 = 16 texels per metre, the
        // SAME pixel grid and the same coverage — only the placement stops being periodic.
        // Twin: WhitecapStampSheetMath.SheetScale / .TexelsPerMetre (WhitecapStampSheetTests pins the
        // equality, so changing _PaintScale or the sheet size without the other fails headless).
        _WhitecapTexScale ("Whitecap sheet scale (tiles/unit; 0.0625 = one 16 m sheet)", Float) = 0.0625

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
        // The floor's DAY KNEE (owner playtest 2026-07-23: the saturating pre-compensation held the dusk sea
        // at daylight-floor brightness and clamped its value structure flat — the "whole sea becomes white"
        // defect). At/above this day/night luma the floor pre-compensates exactly as ADR 0015 shipped; below
        // it the on-screen floor rides DOWN with the scene. 0 = the pre-fix saturating curve EXACTLY.
        _PaletteFloorKnee     ("Palette floor day knee (dnLuma; below it the floor dims with the scene)", Range(0,1)) = 0.45
        _PaletteDeep    ("Palette anchor   deep", Color)    = (0.05, 0.135, 0.205, 1)
        _PaletteMid     ("Palette anchor   mid", Color)     = (0.14, 0.30, 0.38, 1)
        _PaletteShallow ("Palette anchor   shallow", Color) = (0.34, 0.60, 0.62, 1)
        _PaletteFoam    ("Palette anchor   foam", Color)    = (0.92, 0.96, 0.98, 1)

        [Header(Day gated caustics (Arc C   col.rgb only))]
        // DAY-GATED CAUSTICS multiplies the existing shallow caustic add by a DAY factor
        // (saturate(_SunElevation): peaks at noon, naturally 0 at night) so the sun-dappled light nets
        // only show by day. When the day/night cycle is NOT running (_DayNightTint sum ~ 0: editor /
        // bare art scene) it treats the world as full day — the same "unset" convention NightFactor /
        // the palette grade use (NOT _SunElevation == 0, which is a real horizon value at sunrise/sunset).
        // NOTE (ADR 0027 num 7): the Arc C SEE-THROUGH SHALLOWS that used to sit here are RETIRED, not
        // revived. _ShallowTranslucency / _ShallowSeeThroughDepth / _ShallowMinAlpha faked the bottom by
        // LOWERING col.a so an ungraded sprite bled through the alpha blend; a scalar alpha cannot carry
        // per-channel transmission, and the lowered alpha partly CANCELLED these caustics (the old §17.3
        // tune-around). Seabed absorption below composites the bottom in the shader instead, so col.a
        // stays opaque and that cancellation is gone BY CONSTRUCTION.
        _CausticDayGate       ("Caustic day gate (0 = off / always on, 1 = day only)", Range(0,1)) = 0.0
        _CausticShallowBias   ("Caustic band deepen bias (m; push dapple off the very edge)", Float) = 0.0

        [Header(Seabed absorption (ADR 0027 num 7)   the bottom seen THROUGH the column   col.rgb only)]
        // The painted _DepthRamp stays the colour authority for the WATER BODY (ADR 0027 finding 1: a
        // hand-painted LUT is strictly more general than any closed-form e^(-sigma d), and
        // _DeepBlueStrength 0.45 is standing evidence the physical answer was already overridden by hand).
        // Absorption applies ONLY to the bottom seen through the column, which the LUT cannot express at
        // all (finding 3). Hand the shader the bottom's ALBEDO in _SeabedTex, baked over the SAME world
        // rect as _HeightTex (_HeightWorldMin / _HeightWorldSize, ADR 0014's pattern, so NO new uniform),
        // then transmit it by per-channel Beer-Lambert T = exp(-sigma_rgb * 2d) — path 2d because light
        // descends and returns. Red dies first, so the depth-colour shift comes free.
        //   PASSTHROUGH, twice over: the toggle is OFF (the whole block compiles out = byte-identical), and
        //   inside it _Turbidity = 0 skips the block anyway. sigma = 0 means "no absorption model", NOT
        //   "perfectly clear water" (which would show the bottom at full strength everywhere) — clear water
        //   is not a sea state; useful sigma starts near 0.05 (design doc §17.7).
        //   _Turbidity is MOOD-EASED (ADR 0017), so its per-weather values live in the eight
        //   Art/Materials/WaterPresets materials, not here — that is what makes a murky sea a DERIVED
        //   state instead of a hand-picked colour. The per-channel RATIO stays authored art here.
        // Twin: WaterAbsorption (Sigma / Transmission / BandTransmission / Composite) — change one,
        // change BOTH in the same PR. col.rgb only, never depth/clip/_WaterLevel/the height read/the sim.
        [Toggle(_USE_SEABEDTEX)] _UseSeabedTex ("Use baked seabed texture", Float) = 0
        [NoScaleOffset] _SeabedTex ("Seabed albedo over the height world rect (A equals coverage)", 2D) = "black" {}
        _Turbidity        ("Turbidity sigma (1 per m; 0 = off / today)", Range(0,5)) = 0.0
        _AbsorptionRatio  ("Per channel extinction ratio (rgb, red = 1)", Vector) = (1, 0.18, 0.08, 0)
        _AbsorptionBands  ("Transmission posterize bands (0 = smooth; ON by default)", Range(0,32)) = 6

        // ---- ADR 0027 #2: caustics DRIVEN BY THE SHARED WAVE FIELD (default OFF = today) ---------------
        // The seabed shimmer derives from the local CURVATURE of WaveFieldSample() — brightest where the
        // surface is locally CONVEX toward the sun (a dome focuses light) — instead of scrolling an
        // independent noise, so the dapple finally belongs to the swell rolling above it. The blend
        // replaces only the vein VALUE inside the existing _CausticDepth gate, _CausticDayGate sun gate,
        // _CausticAmount/_CausticColor multipliers and the pixelized world grid (all kept). Gated by the
        // field's amplitude (the swellLive idiom): no live trains (edit mode / bare art scene) or dead
        // glass eases back to the noise, so a bare scene never loses its dapple. NO new sim-pushed
        // uniform — the curvature needs no new data; these three are material props (rule 6).
        _CausticCurvatureBlend ("Caustic curvature blend (0 = independent noise / today, 1 = wave-field driven)", Range(0,1)) = 0.0
        _CausticCurvatureStep  ("Caustic curvature sample step (m)", Float) = 0.5
        _CausticCurvatureGain  ("Caustic curvature gain (contrast of the field-driven veins)", Float) = 12.0

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
        // ---- the Arc C UPGRADE: the lines join the SHARED drift + the SHARED field ------------------
        // Three knobs, each defaulting to the SHIPPED behaviour bit-for-bit. Twin: WaterDriftLines.
        //  (1) _DriftLineFoamDrift  — the streak basis, dialled from today's raw current toward the
        //      SAME FoamDriftDir() the foam and whitecaps already drift along (a wind/current blend
        //      that also carries the shoreward bias). §18.1 keyed this to the current ALONE and said so
        //      deliberately; that was right about wind-vs-current and still left the lines reading a
        //      different direction from the foam they are made of. Now a dial, not a hard choice.
        //  (2) _DriftLineConvergence — a drift line is floating material COLLECTED on a convergence
        //      line, which is why real ones sit in bands with clean water between them. Reuses ADR
        //      0027 num 3's ConvergenceGate off the shared field — no second opinion about the same
        //      physics. Costs 4 WaveFieldSample taps, and only above 0.
        //  (3) _DriftLineGrid — the layer's own pixel cell, as a multiple of the PPU cell (ADR 0027's
        //      scale-hierarchy note: deliberately DIFFERENT grids, not one shared lattice). A drift
        //      lane is metres long, so a coarser cell reads as lane texture rather than pixel noise.
        _DriftLineFoamDrift  ("Drift line follows shared foam drift (0 = current only / today)", Range(0,1)) = 0.0
        _DriftLineConvergence("Drift line gathers on convergence lines (0 = off / today)", Range(0,1)) = 0.0
        _DriftLineGrid       ("Drift line pixel grid (multiples of the PPU cell; 1 = today)", Range(1,8)) = 1.0
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

        // ---- LIT WATER: the lamp ADDS light to the sea it finds, it does not only scale it -------------
        // Owner ruling 2026-09-02: *"dark enough at night that the player feels the need to use radar and
        // the lighting."* The reveal above is a MULTIPLY of the water's own colour — and the frame is
        // multiplied AGAIN by the day/night tint downstream (ADR 0013), which at 02:00 is ~(0.016, 0.020,
        // 0.040). A dark sea times 3.5 times 0.02 is still a dark sea: the shipped 1.5 / 9 m searchlight
        // "lights nothing you can name" (register row 12). A lamp has to ADD.
        //
        // So the cone also lays the lamp's colour ON the water's own albedo — the composited sea BEFORE the
        // palette grade, so foam, whitecaps and the surf's white keep the contrast the sea authored — and
        // that term rides the post-grade, overlay-COMPENSATED bucket beside the moon's glitter (#143's
        // precedent). Surf and caps therefore LIGHT UP inside the beam while the open body takes a faint
        // sheen, and the whole thing survives the multiply instead of being crushed by it.
        //
        // 0 is the pre-PR look EXACTLY (the multiply-only reveal). col.rgb ONLY (P1, rule 5).
        // MEASURED, not guessed (the plate instrument's night column, the shipped 1.5 x 0.8 / 9 m lamp on a
        // moonless clear midnight): the term adds about 0.08 of luma inside the cone per unit of this dial,
        // so 0.06 moved the lit pool by 9 % — still "lights nothing you can name". 1.6 carries it to roughly
        // the luma a MOONLIT sea reads at, which is the bar the ruling sets for a lamp on a black night.
        _BeamLitStrength     ("Beam LIT water (additive: the lamp on the sea's own albedo; 0 = the shipped multiply-only reveal)", Range(0,4)) = 1.6

        [Header(BEAM WAVE RELIEF (the beam lights the SEAS SHAPE   owner mandate 2026 08 28))]
        // "it should highlight the water at crests and be shadowed at the valleys of waves unless the proper
        // light angle exposes them." The cone weight above is radial x angular and blind to the sea under it;
        // these scale it by the wave field's own relief (N.L against the SAME analytic waveSlope the swell
        // FACE SHADING rides). Strength 0 is an EXACT passthrough — the shipped ADR 0016 cone, unchanged.
        // col.rgb ONLY (P1, rule 5).
        _BeamReliefStrength     ("Beam wave relief (0 = the flat shipped cone; 1 = full physical relief)", Range(0,2)) = 1.0
        _BeamReliefMaxGain      ("Beam relief max gain (bounds crest sparkle on a face turned into the beam)", Range(1,6)) = 2.5
        _BeamReliefMinElevation ("Beam relief grazing floor (min normalized lamp elevation; lower = harder rake)", Range(0.02,1)) = 0.15
        [Header(DISPLACED SURFACE (ADR 0023   the sea as real geometry))]
        // Read by the HHWater pass's vertex stage only — the flat Universal2D pass ignores them.
        // Exaggeration and band are PUSHED per tick by DisplacedWaterSurface (band is DERIVED via
        // ShoreFadeMath.RecommendedBandMeters — rule 6: never a free magic number), so the defaults
        // here matter to harnesses and bare materials only. GameConfig exposure is arc step 3.
        _WaveExaggeration ("Displacement exaggeration (1 = sim true, 1.5 = ADR 0023 default)", Float) = 1.5
        _ShoreFadeBand    ("Shore fade band (m of depth, derived and pushed)", Float) = 0.5
        _WaterIsoDepth    ("Iso depth factors (cos elev, sin elev)", Vector) = (0.766, 0.643, 0, 0)

        [Header(ENVELOPE SALIENCE (ADR 0023 step 2   the big wave wears the foam and the shade))]
        // The whitecap-salience retune + the envelope-relative value bands (ADR 0023 §(3)-(4) and
        // §"Whitecap salience retune") — shared by BOTH passes (flat and displaced; one fragment).
        // Yesterday's caps marked EVERY crest with equal salience, which is exactly what hid the
        // big one (the spike's control image: the 100%-envelope event sits in uniform speckle).
        // Now cap CORE SOLIDITY keys on the crest factor — height relative to the field's envelope
        // (_WaveFieldParams.z), not absolute height: ordinary chop wears thin dithered/milky
        // streaks; only near-envelope crests wear the solid foam core. The VALUE BANDS mark the
        // same axis by shade (posterized to the owner's palette anchors, Bayer-dithered at band
        // EDGES only on world-locked PPU cells — the style law). Defaults are the SPIKE-TUNED
        // values (spike/3d-water VERDICT.md: cap threshold 0.62, solid margin 0.3, dither fringe
        // 0.25, bands 7, edge window 0.4), pinned by WhitecapSalienceMathTests against the C#
        // twin (WhitecapSalienceMath). GameConfig plumbing is arc step 3 — material-level here.
        // Every term is col.rgb-only dressing — never depth/clip()/_WaterLevel/the height read/
        // the sim (P1 integrity, CLAUDE.md rule 5). Both masters at 0 = today's look EXACTLY.
        _CapSalienceStrength   ("Cap envelope salience (0 = legacy even salience)", Range(0,1)) = 1.0
        _CapEnvelopeThreshold  ("Cap envelope threshold (crest factor where cores begin)", Range(0,1)) = 0.62
        _CapSolidMargin        ("Cap solid core margin (above threshold = fully solid)", Range(0,1)) = 0.3
        _CapDitherBand         ("Cap dither fringe width (crest factor above threshold)", Range(0.01,1)) = 0.25
        _EnvelopeBandStrength  ("Envelope band strength (0 = off / today)", Range(0,1)) = 0.35
        _EnvelopeBands         ("Envelope value bands (solid steps)", Float) = 7
        _EnvelopeBandDitherWin ("Envelope band edge dither window (0..1 of a band)", Range(0,1)) = 0.4
        // ---- DE-REGULARIZING THE BANDS (owner judge pass 2026-08-01: "the large white bands are too
        // regular like a pattern"). The wave field is <=4 sine trains sharing one downwind axis at FIXED
        // angular offsets and FIXED wavelength ratios — a deterministic quasi-periodic interference whose
        // envelope repeats at evenly spaced diagonals. FOUR features read that same envelope and reinforce
        // into one band set, and the only randomization anywhere in the chain is edge Bayer. #382 raised
        // the band strength to 0.35, which is what made the ruler legible.
        //
        // ⚠️ The FIELD is not touched — the owner locked that spectrum (#372/#382) and the hulls ride it.
        // De-regularizing happens at the cosmetic band READ, which is why both knobs below are pure
        // col.rgb and both are exact passthroughs at their off value.
        //   _EnvelopeBandPatchScale — cycles per metre of the patch mask (0.025 ~ 40 m patches). Bands then
        //                             surface in PATCHES instead of wall-to-wall, the way a real sea shows
        //                             its structure in some places and not others.
        //   _EnvelopeBandPatchMin   — how much band survives BETWEEN patches. 1 = no patchiness (today).
        //   _EnvelopeBandWarp       — wanders the band VALUE axis by a low-frequency amount, so the band
        //                             boundaries meander instead of running as parallel rulers. Deliberately
        //                             a warp of the value axis and NOT a second WaveFieldSample at a warped
        //                             position: that would double the field cost per pixel to move contours
        //                             that this moves for one noise read. 0 = today's exact boundaries.
        _EnvelopeBandPatchScale ("Band patch scale (cycles/m; 0.025 ~ 40 m patches)", Range(0.002,0.2)) = 0.025
        // ⚠️ The floor is NOT free to go as low as it looks. ShoreSwirl_EnvelopeBands_FadeWithTheSeam
        // measures that open water still CARRIES its envelope bands (the §23 marked-wave style is owner
        // approved), and it caught 0.35 taking the open-water imprint under its bar — de-regularizing had
        // quietly started deleting the feature instead of breaking up its spacing. 0.5 keeps a 2:1 contrast
        // between patch and gap, which is plenty of irregularity, while the bands stay present everywhere.
        _EnvelopeBandPatchMin   ("Band patch floor (1 = wall-to-wall, today)", Range(0,1)) = 0.5
        _EnvelopeBandWarp       ("Band boundary wander (0 = ruler-straight, today)", Range(0,0.5)) = 0.18

        [Header(CAPILLARY RIPPLES (ADR 0027 num 10)   the finest band   col.rgb only   default OFF)]
        // The finest thing this shader drew was _WindChopScale 0.7 -- a 1.4 m band, which is CHOP, not
        // ripples. This is the ~0.08-0.15 m octave riding ON the larger waves: what makes water read as
        // water close up. TIER A PERMANENTLY -- col.rgb only, never depth/clip()/_WaterLevel/the height
        // read/_WaveFieldParams/anything the hulls ride. A ripple is surface texture, not a force.
        // C# twin: HiddenHarbours.Art.WaterRipple (change one, change BOTH in the same PR).
        //
        // THREE GATES, each with an explicit off end:
        //   (1) WIND      -- _Roughness (sim-pushed, never authored) between onset and full. No wind, no
        //                    ripples; glass stays glass.
        //   (2) WINDWARD  -- the shared field's slope projected on the wind. Going DOWNWIND you climb the
        //                    windward face, so dot(slope, wind) > 0 IS that face; ripples sit there and
        //                    thin out in the lee behind a crest. No new uniform (the slope is already
        //                    sampled). Gate 0 = ripples everywhere.
        //   (3) FRAMING   -- the anti-DENSITY guard. See the block comment on RippleFramingFade below:
        //                    the ADR's per-zoom-tier amplitude fade DOES NOT EXIST and must not be built.
        //
        // _RippleStrength = 0 is an EXACT passthrough (the whole block is skipped) -- the shipped look is
        // byte-identical until the owner dials it in, the discipline every ADR-0010 addendum has kept.
        _RippleStrength      ("Ripple strength (0 = OFF / today exactly)", Range(0,1)) = 0.0
        // ⚠️ CORRECTED 2026-08-01 (owner: "too regular like a pattern"). The note here used to reason at
        // PPU 32 and conclude 0.12 m = 3.84 cells per cycle, "comfortably" past the ~4-cells-per-cycle
        // moire floor. But the GRID THIS BAND IS QUANTIZED TO is the shader's own _PixelsPerUnit, which
        // SHIPS AT 24, not the camera's assetsPPU 32 (those are different quantities — RipplePixelFootprint
        // Tests measures the SCREEN footprint against assetsPPU and is unaffected by this). At PPU 24 the
        // world cell is 4.17 cm, so 0.12 m bought only 2.88 cells per cycle — well UNDER the shader's own
        // stated floor, and an under-sampled sine beating against a regular grid is textbook moire: extra
        // striping that reads as exactly the "regular pattern" complaint.
        // 0.17 m = 4.08 cells per cycle at PPU 24 — back over the floor, still inside the ADR's
        // 0.08-0.15 m intent at the coarse end. The value now ships on the materials rather than relying
        // on this default, so applying a preset cannot silently take the band back under the floor.
        _RippleWavelength    ("Ripple wavelength (m; 0.17 = 4.1 cells at the shipped PPU 24)", Range(0.04,0.4)) = 0.17
        // World m/s ALONG the wind. Default 0.09 = the wind-chop band's own speed, so the ripple sits in
        // the sea's established (deliberately slow) feel family at ~0.75 wavelengths/sec. The PHYSICAL
        // deep-water speed for 0.12 m is 0.43 m/s -- ~5x this, which at 3.8 px/cycle reads as temporal
        // shimmer rather than ripple. The dispersion master re-ties it like every other band (below).
        _RippleSpeed         ("Ripple scroll speed (m per sec along the wind)", Float) = 0.09
        // The #9 per-band feel multiplier, same 0.06 convention as the chop/cross/swell bands, so
        // dialling _DispersionScale up does not leave the ripple as the one band ignoring dispersion.
        // At _DispersionScale = 0 this is bit-exact _RippleSpeed — the PASSTHROUGH contract, not the
            // shipped sea: the materials carry 0.5, so the live ripple speed is already half-derived.
        _DispersionRippleMult("Dispersion feel mult: ripple band", Float) = 0.06
        _RippleWindOnset     ("Wind (_Roughness) at or below which there are NO ripples", Range(0,1)) = 0.05
        _RippleWindFull      ("Wind at or above which the ripple gate is fully open", Range(0,1)) = 0.45
        _RippleWindwardGate  ("Windward face bias (0 = everywhere, 1 = windward faces only)", Range(0,1)) = 0.7
        _RippleLeeFloor      ("Lee floor (how much ripple survives behind a crest)", Range(0,1)) = 0.15
        // The layer's OWN quantization, DEFAULT ON (ADR 0027's condition). Matters most here because
        // _DepthBands is 0, so the base ramp lends no pixel character and a smooth ripple would read as
        // airbrushed shimmer. Dither at the band EDGE only -- full-range dither dissolves the steps.
        _RippleBands         ("Ripple posterize steps (below 2 = smooth)", Float) = 3
        _RippleDitherWin     ("Ripple step edge dither window (0..1 of a step)", Range(0,1)) = 0.5
        // The FRAMING fade, in metres of sea on screen (_SeaFramingHeight). The tightest framing the game
        // has is 5.625 m and the widest 33.75 m; 16 -> 30 holds the band full through the small-boat
        // tiers and eases it toward the floor as the trawler/packet framings pile on cycles.
        _RippleFadeNear      ("Framing (m of sea on screen) at or below which ripples are FULL", Float) = 16
        _RippleFadeFar       ("Framing at or above which ripples reach the floor", Float) = 30
        _RippleFadeFloor     ("Floor the framing fade never goes below (0 = gone when wide)", Range(0,1)) = 0.2

        [Header(ADVECTED FOAM BUFFER (ADR 0027 num 6)   wake as a mark on the sea   default OFF)]
        // IsoFacetHullFeature keeps a persistent single-channel buffer of "how much churned foam is on
        // this patch of sea": scrolled in WHOLE WORLD CELLS, decayed exponentially, and injected
        // wherever a hull works against the water. This block reads it as a coverage mask that ADDS to
        // the foam already computed above -- it never replaces the fringe foam, the whitecaps, or the
        // BoatWakeEmitter's sprite trail.
        //
        // What it adds that nothing else can: foam that PERSISTS and DRIFTS after the boat has gone,
        // and churn around a hull that is merely BOBBING -- the owner's 2026-08-01 ask, which the
        // emitter has no signal for (it keys on speed, and a moored dory has none).
        //
        // ⚠️ Zero crawl BY CONSTRUCTION: the lookup is a WORLD position mapped through the buffer's
        // own cell-snapped window (_HHFoamBufferWorld), and the target is point-filtered, so a foam
        // cell belongs to a place on the water and stays there under any pan. There is no screen-space
        // step anywhere in this read -- see the crawl law in the ADR's Pixelation section.
        //
        // ⚠️ 0 is an EXACT passthrough (the whole block is skipped). It SHIPPED at 0 through #383 --
        // step one of a deliberate two-step dial-in -- and 2026-08-05 is step two: the owner asked for
        // "another pass" on the foam, and step one had left the persistent foam invisible AND
        // unsourced (no prefab carried a FoamInjector either). Both ends are now live, so the sea
        // actually keeps a mark. This is the ONE value in this PR the owner may simply want lower.
        _WakeFoamStrength  ("Wake foam strength (0 = OFF)", Range(0,2)) = 0.85
        // Coverage below this is bare water: a trail's fringe holds a lot of very faint foam, and
        // drawing all of it reads as a grey wash rather than as churn.
        _WakeFoamThreshold ("Wake foam threshold (buffer value where foam begins)", Range(0,1)) = 0.12
        _WakeFoamSoftness  ("Wake foam edge softness", Range(0.01,1)) = 0.18
        // The layer's OWN quantization, DEFAULT ON (ADR 0027's condition). The buffer's cells are
        // already 4 screen px, so this posterizes VALUE rather than position: churn reads as a few
        // solid tones instead of an airbrushed gradient.
        _WakeFoamBands     ("Wake foam posterize steps (below 2 = smooth)", Float) = 3
        // ---- LACE: the "like real foam" half of the 2026-08-05 pass -------------------------------
        // The buffer stores COVERAGE — how much churn is on this patch of sea — which is the right
        // QUANTITY and the wrong SHAPE. Thresholding a smooth accumulation yields a rounded, solid
        // patch with a clean outline, and that is what reads as a decal rather than as foam. Real
        // foam is a torn mat: dense hearts, holes through it, and a fringe drawn out downwind.
        //
        // So the stored coverage is TORN by the same evolving foam field the whitecaps ride, on the
        // same wind-stretched basis (_FoamStreakStretch, reused) — the tearing therefore boils and
        // streaks with the rest of the sea's foam instead of being a second, unrelated texture. It
        // multiplies the STORED value (before the threshold), so it removes foam and never invents
        // any: the dense heart of a wake survives, its fringe breaks into lace and dies sooner.
        // 0 = the #383 read, EXACTLY.
        _WakeFoamLace      ("Wake foam lace (0 = solid patch, 1 = fully torn)", Range(0,1)) = 0.7
        // The tear's own blob scale, as a multiple of the whitecap field's. ABOVE 1 = finer holes
        // than the caps carry, which is what keeps churn reading as churn next to a whitecap rather
        // than as another whitecap.
        _WakeFoamLaceScale ("Wake foam lace scale (x the whitecap blob scale)", Float) = 1.8

        // ---- THE WAKE FOAM'S AGE RAMP (owner ask 2026-08-27) ----------------------------------
        // "the wakes behind the boat are still a solid white foam from wherever the boat interacts
        // with, this should churn through different shades of blue, distort and fade into the
        // ambient ocean over time."
        //
        // The age indexes the sea's OWN palette ramp (_PaletteFoam -> _PaletteShallow ->
        // _PaletteMid, ADR 0015) exactly as the particle wake does, so both halves of the wake age
        // through the same blues. The dissolve into the ambient sea is free: the lerp WEIGHT is the
        // COVERAGE, so the oldest foam barely tints the water it is lying on.
        //
        // 🔴 ROUND 2 (owner eyeball, 2026-08-27): the age is read from the buffer's FRESHNESS
        // channel, not from its coverage. Coverage cannot carry age - it saturates within ~0.4 s of
        // deposit and this shader then thresholds and posterizes it, so the value the proxy received
        // could only ever be one of three ({0, 0.425, 0.85}) and 72-81% of a visible wake drew at
        // age exactly 0. That is "stays white, never disperses", and no threshold retune can undo
        // it. Freshness is a clock the injection MAXes and time decays, so it never clamps.
        //
        // C# twin: WakeFoamAgeing.Age01FromFreshness / Knots / Ramp3 (Core). Change one, change BOTH
        // in the same PR - a source-scrape test reads these lines and fails red on drift.
        // 0 = a BIT-EXACT passthrough to the single-white compose (the A/B).
        _WakeFoamAgeStrength ("Wake foam age ramp (0 = one flat white, the shipped look)", Range(0,1)) = 1
        // The buffer FRESHNESS at/above which a texel is churning right now and draws pure white.
        // At 1 that is the instant of churn alone and the white HOLD is _WakeFoamWhiteHold's job -
        // one knob for one idea. Lower it to let recently-churned water stay white a little longer.
        _WakeFoamFreshFloor  ("Wake foam freshness that still reads as fresh churn", Range(0.05,1)) = 1
        // The same three knots the particle ramp uses, in AGE (0 = just churned, 1 = old).
        _WakeFoamWhiteHold   ("Wake foam: age it stays white until", Range(0,1)) = 0.12
        _WakeFoamBlueReach   ("Wake foam: age it reaches the shallow blue", Range(0,1)) = 0.45
        _WakeFoamDeepReach   ("Wake foam: age it reaches the mid blue", Range(0,1)) = 0.85
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        // ==== THE SHARED WATER PROGRAM ========================================================
        // Every declaration, helper and the FULL fragment stage live in this SubShader-scope
        // HLSLINCLUDE, so both passes compile the SAME code: the flat in-scene pass
        // (Universal2D) and the ADR 0023 displaced off-screen pass (HHWater) are one program
        // with two vertex stages. The ONE-SEA rule made structural — the displaced surface
        // cannot drift from the flat water, because it IS the flat water's fragment.
        HLSLINCLUDE
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
            // The BAKED SEABED (ADR 0027 #7). NOT a tiling painted slot: it is baked ONCE over the
            // _HeightTex world rect, so it is sampled by world position (not _PaintScale) with the
            // importer's Point filter + Clamp wrap — pixel art, and no smearing between bottom cells.
            TEXTURE2D(_SeabedTex);    SAMPLER(sampler_SeabedTex);
            // ADR 0027 #8: the OBJECT REFLECTION target, written by IsoFacetHullFeature's fourth
            // renderer list and published globally. Read with Load() at integer pixel coordinates —
            // it is exactly camera-render-resolution, so the map is 1:1 and Load shares SV_POSITION's
            // coordinate convention, which sidesteps every render-target Y-flip ambiguity a uv fetch
            // would introduce. ReflectionRegistry binds a 1x1 CLEAR fallback before the pass has ever
            // run (an unbound sampler's grey placeholder has alpha ~0.5 and would smear a flat
            // half-mirror over the whole sea on the first frame of every scene).
            TEXTURE2D(_HHReflectTex);

            // ADR 0027 #6: the ADVECTED FOAM BUFFER, written by IsoFacetHullFeature's ping-ponged
            // blit and published globally. Single channel; one texel IS one world cell, point
            // filtered. FoamInjectionRegistry binds a BLACK 1x1 fallback before the pass has ever run
            // (an unbound sampler's grey placeholder would read as ~0.5 coverage and wash the entire
            // sea with foam on the first frame of every scene — the same lesson as the interior
            // guard's black 1x1 and the reflection target's clear one).
            // Its own sampler (the repo convention above), so the read inherits the target's POINT /
            // CLAMP state — a bilinear read would blend across cells and undo the crisp world grid.
            TEXTURE2D(_HHFoamBufferTex); SAMPLER(sampler_HHFoamBufferTex);

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

            // ---- MANY WATER LIGHTS (the array the ADR 0016 note above reserved) ---------------------------
            // Published by HiddenHarbours.Art.WaterLightBridge (the WaveFieldBridge pattern: self-installing,
            // ONE owner of the globals, every frame). The bridge picks the nearest WATER_MAX_LIGHTS emitters to
            // the camera and publishes them here; _WaterLightCount is how many slots are live.
            //
            // The SINGLETON ABOVE IS NOT SUMMED WHEN THESE ARE LIVE. _BoatLight* stays published because the
            // OTHER lit path reads it — SpriteLitDecor.hlsl lights trees/shrubs/plants from that one lamp
            // (TWO lit paths BY DESIGN; this PR touches only the water). Summing both would double-count the
            // primary lamp on the water. Count 0 => no bridge running (a bare art scene, a legacy scene, a
            // harness) => the water falls back to the singleton, which is EXACTLY the shipped ADR 0016 path.
            //
            // .z of a position is the lamp HEIGHT above mean water in metres — the lever the wave relief turns
            // a uniform disc into a raking beam with (a high lamp flattens, a low one rakes). 0 = not published,
            // which the relief reads as "no height known" and skips, leaving the flat cone exactly as it was.
            #define WATER_MAX_LIGHTS 4
            float4 _WaterLightPos[WATER_MAX_LIGHTS];      // xy = world lamp pos, z = lamp height (m)
            float4 _WaterLightDir[WATER_MAX_LIGHTS];      // xy = world beam axis (~unit)
            float4 _WaterLightColor[WATER_MAX_LIGHTS];    // rgb = beam colour
            float4 _WaterLightParams[WATER_MAX_LIGHTS];   // x = intensity, y = range, z = cos(half), w = cos(inner)
            float4 _WaterLightParams2[WATER_MAX_LIGHTS];  // x = edge soft, y = gate thr, z = gate soft, w = fallback
            float  _WaterLightCount;                      // live slots; 0 = no bridge -> legacy singleton path

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
            // ADR 0027 P2 widened the field 4 -> 8 (the ADR 0018 amendment): a JONSWAP spectrum needs
            // enough frequencies to carry a SHAPE and to let neighbours beat into wave GROUPS. Slots
            // beyond the live count publish zero, so a pre-P2 bridge (or none) still reads silent.
            float4 _WaveTrain4;
            float4 _WaveTrain5;
            float4 _WaveTrain6;
            float4 _WaveTrain7;
            float4 _WavePhases;      // per-train phase (rad) for trains 0-3, accumulated in C# DOUBLE + wrapped
            float4 _WavePhases2;     // ... and for trains 4-7
            float4 _WaveFieldParams; // x = live train count (0 = not published), y = crest sharpening p,
                                     // z = total amplitude (m; the crest normalizer),
                                     // w = the DOMINANT (spectral-peak) slot index -- was `reserved`,
                                     //     published as 0, which is what the flat weighting still sends

            // GLOBAL CAMERA FRAMING (ADR 0027 #10) — how many METRES of sea the camera currently shows down
            // the screen, published by HiddenHarbours.Art.WaterSurface via Shader.SetGlobalFloat (the
            // WaveFieldBridge pattern). ONLY the ripple band's density fade reads it. A GLOBAL (outside the
            // per-material CBUFFER) like _SunDir/_WindWorld/_WaveFieldParams, so an empty material still
            // compiles; UNSET reads as 0, and the fade's `<= 0` branch turns that into NO fade rather than a
            // silently blank band (see RippleFramingFade).
            //
            // ⚠️ It is a derived-physics PUSH, NOT a mood colour: it must never enter
            // WaterSurface.MoodFloatNames, which would ease it from the eight preset materials and
            // double-drive it — the same discipline that keeps _Chop/_Roughness/_Flow out of that list.
            float _SeaFramingHeight;

            // GLOBAL FOAM-BUFFER WINDOW (ADR 0027 #6) — where in the WORLD the advected foam buffer
            // currently sits, published by IsoFacetHullFeature alongside the target itself:
            //   xy = the CELL-SNAPPED lower-left corner of the window (world m)
            //   z  = the window extent (m)   w = 1/extent
            //
            // 🔴 This vector IS the crawl law made concrete. Because the origin is snapped to a world
            // cell lattice on the C# side and the buffer scrolls only in WHOLE cells, mapping a world
            // position through it always lands on the same texel for the same patch of sea — so a
            // wake stays painted where the boat went while the camera pans over it. Reading the RT on
            // its own screen-space grid instead is exactly the artefact the ADR warns about.
            // Twin: FoamBuffer.SampleUv / FoamBuffer.WorldCellOrigin.
            //
            // A GLOBAL (outside the per-material CBUFFER) like _SunDir/_SeaFramingHeight: the window
            // belongs to the CAMERA, not to any one water renderer. UNSET reads as all-zero, and
            // z <= 0 is the "never published" branch — no foam, never a grey wash.
            float4 _HHFoamBufferWorld;

            // ---- WIND FETCH (ADR 0027 #1) — globals, published by WaveFieldBridge.PublishFetchGlobals ------
            // How far the wind has blown over open water before it reaches this pixel. Lee shores go calm,
            // exposed shores build.
            //   _WaveFetchParams  : x = strength (0 = OFF, the shipped passthrough — the march is skipped
            //                       entirely), y = march step (metres), z = lee floor, w = response exponent.
            //   _WaveFetchParams2 : x = shore band (metres), y = posterize bands (< 2 = smooth),
            //                       zw = the UPWIND unit direction to march along (zero = dead calm/unset).
            //
            // ⚠️ TIER B, and the only item on the realness pass that is. This envelope scales the field the
            // HULLS RIDE, not just col.rgb — deliberately. A shader-only damp here would be the
            // _OceanSwellScale incident by construction: glass drawn behind the headland, open-water swell
            // still boarding the boat in it. C# twin: HiddenHarbours.Core.WaveFetch (change one, change both).
            //
            // ⚠️ PUSHES, not mood floats — never add these to WaterSurface.MoodFloatNames (they would then be
            // eased from the preset materials, driving the drawn sea from a source the hull cannot read).
            //
            // Both default to 0 when unpublished: strength 0 => envelope 1 => the legacy sea, so a bare
            // material or an art scene with no bridge is unaffected (the _DayNightTint/_MoonDir convention).
            float4 _WaveFetchParams;
            float4 _WaveFetchParams2;

            // ---- BREAKING WAVES (ADR 0040), published by WaveFieldBridge.PublishBreakerGlobals -------
            // The CONTOUR, solved once per tick rather than per pixel. The forward criterion costs a
            // tanh, two pows, a sinh and a sqrt, and the whitewater march below needs it SURF_MARCH_STEPS
            // times per pixel; inverting it on the CPU turns that into "is the water shallower than the
            // break depth?" — one smoothstep, no transcendentals. See Core/Environment/BreakerContour.cs.
            //
            // _BreakerDepths : xyz = break depth (m) at fetch envelope 1 / mid / lee floor, w = lee floor
            // _BreakerOuter  : xyz = the gate's outer depth at the same three, w = 1 if this sea breaks
            // _BreakerParams : x = march step (m), y = whitewater decay tau (s), z = g, w = H0 (m)
            //
            // ⚠️ PUSHES, not mood floats — never add these to WaterSurface.MoodFloatNames, for exactly
            // the reason the fetch params carry: they would then be eased from the preset materials and
            // the DRAWN surf would leave the sea the hull is actually in.
            //
            // All default to 0 when unpublished, and _BreakerOuter.w = 0 means "breaks nowhere" — so a
            // bare material or an art scene with no bridge draws no surf at all (the _DayNightTint /
            // _MoonDir "unset" convention).
            float4 _BreakerDepths;
            float4 _BreakerOuter;
            float4 _BreakerParams;
            float4 _BreakerAnatomy;   // x = L0 (m), y = gamma, z = spilling limit, w = plunging limit
            // ADR 0040 rev 3 — the BORE's dials from GameConfig.Breakers (WaveFieldBridge.PublishBreakerGlobals):
            // x = pulse sharpness (0 = no pulse: the steady state), y = set strength (0 = every bore born at
            // full energy), z = Hunt's run-up coefficient, w = the run-up cap (m of level). All-zero = a stale
            // config = the surf exactly as revision 2 shipped it.
            float4 _BreakerBore;

            // SRP-batcher friendly: every per-material property in one CBUFFER (the runtime sets these via a
            // MaterialPropertyBlock; the sim-driven ones change on the slow tick, not per frame).
            CBUFFER_START(UnityPerMaterial)
                float4 _ShallowColor;
                float4 _DeepColor;
                float  _ShallowDepth;
                float  _DeepDepth;
                float  _DepthBands;
                // Deep-blue enrichment (bounded base-colour pull toward a rich navy in deep water).
                float  _DeepBlueStrength;
                float4 _DeepBlueColor;
                float  _DeepBlueStart;
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
                // ADR 0027 #4 + #9 — band scaling with sea state + dispersion-derived speeds (default OFF).
                float  _BandScaleResponse;
                float  _DispersionScale;
                float  _DispersionChopMult;
                float  _DispersionCrossMult;
                float  _DispersionSwellMult;
                float  _DispersionShoalBunch;
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
                // Swell READ legibility (crest/trough VALUE contrast; col.rgb-only, its own gate).
                float  _SwellReadStrength;
                float  _SwellReadBands;
                // Swell FACE shading (lit face / shaded back off the wave field's analytic slope).
                float  _SwellFaceShade;
                // Modelled-swell CALM gate (shared by the read band + the face shade; keys _Chop).
                float  _SwellReadSeaStateLo;
                float  _SwellReadSeaStateHi;
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
                // Foam clumping (broad slow patch field: windrows + crest-shed rafts, not an even sprinkle).
                float  _FoamClumpStrength;
                float  _FoamClumpScale;
                float  _FoamClumpStretch;
                // Shared wave field (ADR 0018 B1): the whitecap sea-state onset over total train amplitude.
                float  _WhitecapOnsetAmp;
                // ADR 0027 #3 — convergence (Jacobian) foam gate (default OFF).
                float  _FoamConvergenceStrength;
                float  _FoamConvergencePinch;
                float  _FoamConvergenceStep;
                // Shoreward swell/foam bias (near-coast roll-in; visual direction only).
                float  _ShorewardBias;
                float  _ShorewardFalloff;
                float  _ShoreSampleStep;
                float  _SwashAmplitude;
                float  _SwashSpeed;
                float  _SwashWavelength;
                float  _SwashAlongShoreVary;
                float  _SwashEdgeShift;
                float  _SwashMaxEdgeShift;
                float  _SwashCalmGate;
                // Shore band quantization: the foam-edge dither and the cosmetic slope floor.
                float  _FoamEdgeDither;
                float  _ShoreSlopeFloor;
                // BREAKING WAVES (ADR 0040) — the LOOK knobs only. WHERE the surf is comes from the
                // painted seabed + the tide via _BreakerDepths/_BreakerOuter (globals, above); nothing
                // in this block can move the break line, and that is the point.
                float  _SurfStrength;
                float4 _SurfColor;
                float  _SurfCrestBoost;
                float  _SurfCrestWidth;
                float  _SurfNoiseScale;
                float  _SurfEvolveSpeed;
                float  _SurfThreshold;
                float  _SurfThresholdSoft;
                float  _SurfBands;
                float  _SurfBandDither;
                float  _SurfSupersedeFringe;
                float  _SurfPlungeStrength;
                float  _SurfLipThrow;
                float  _SurfLipWidth;
                float4 _SurfLipColor;
                float  _SurfBarrelShade;
                float4 _SurfBarrelColor;
                float  _SurfPocketWidth;
                float  _SurfPocketBoost;
                // The bore's look dials (ADR 0040 rev 3) — 0 = the exact passthrough, each.
                float  _SurfBeatStrength;
                float  _SurfRunUpStrength;
                float  _SurfFrontSlope;
                float  _SurfDepositStrength;   // C#-read; see the property
                // Organic shore fringe (LOOK-ONLY prototype; cosmetic, foam/alpha band only — ADR 0012).
                float  _ShoreNoise;
                float  _ShoreNoiseScale;
                float  _ShoreNoiseBand;
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
                // The mirror's FORM (the 2026-09-02 ruling): driven off the field's own slope, not a
                // wavelength. _MirrorForm 0 = the shipped stripe, exactly. col.rgb ONLY (P1, rule 5).
                float  _MirrorForm;
                float  _MirrorSheen;
                float  _MirrorTiltScale;
                float  _ReflectionSunStreak;
                float  _ReflectionSunSharp;
                // ADR 0027 #8 — object reflections (the HHReflect list, warped by the wave field).
                float  _ObjectReflectStrength;
                float  _ObjectReflectWarp;
                float  _ObjectReflectSink;
                // Sky-content reflection (clouds + moon glitter + stars; col.rgb-only dressing, day/night-driven).
                float  _SkyReflectionStrength;
                float  _CloudStrength;
                float  _CloudScale;
                float  _CloudDriftSpeed;
                float  _CloudSoftness;
                float4 _CloudColor;
                float  _CloudMoonlitVis;
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
                float  _WhitecapTexScale;
                // Palette guard-rail (the final soft grade; col.rgb-only — ADR 0015).
                float  _PaletteGradeStrength;
                float  _PaletteValueFloor;
                float  _PaletteValueCeil;
                float  _PaletteSatCap;
                float  _PalettePullStrength;
                float  _PaletteNightFloor;
                float  _PaletteFloorKnee;
                float4 _PaletteDeep;
                float4 _PaletteMid;
                float4 _PaletteShallow;
                float4 _PaletteFoam;
                // Day-gated caustics (col.rgb) — Arc C, default OFF. (The Arc C see-through-shallows
                // props that sat here are RETIRED by ADR 0027 #7's seabed absorption below.)
                float  _CausticDayGate;
                float  _CausticShallowBias;
                // ADR 0027 #2 — field-driven caustics (curvature of the shared wave field; default OFF).
                float  _CausticCurvatureBlend;
                float  _CausticCurvatureStep;
                float  _CausticCurvatureGain;
                // ADR 0027 #7 — seabed absorption. sigma = _Turbidity (mood-eased, 1/m) x
                // _AbsorptionRatio.rgb (fixed per-channel character). Twin: WaterAbsorption.
                float  _Turbidity;
                float4 _AbsorptionRatio;
                float  _AbsorptionBands;
                // Current drift lines (col.rgb; keyed to _FlowDir/_Flow — the tidal set) — Arc C, default OFF.
                float  _DriftLineStrength;
                float  _DriftLineSpeed;
                float  _DriftLineStretch;
                float  _DriftLineScale;
                float  _DriftLineFoamDrift;
                float  _DriftLineConvergence;
                float  _DriftLineGrid;
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
                // LIT WATER (the night ruling): the lamp ADDS its colour on the sea's own albedo, in the
                // overlay-compensated bucket, so a black sea can be lit at all. 0 = the pre-PR multiply-only
                // reveal, exactly. col.rgb ONLY (P1, rule 5).
                float  _BeamLitStrength;
                // Beam WAVE RELIEF (owner mandate 2026-08-28): how hard the wave field's own slope shapes
                // the cone. Strength 0 = the flat shipped cone, EXACTLY. col.rgb ONLY (P1, rule 5).
                float  _BeamReliefStrength;
                float  _BeamReliefMaxGain;
                float  _BeamReliefMinElevation;
                // ---- displaced surface (ADR 0023; read by the HHWater pass's vertex stage) ----
                float  _WaveExaggeration;
                float  _ShoreFadeBand;
                float4 _WaterIsoDepth;
                // ---- envelope salience (ADR 0023 phase 2 step 2; spike-tuned — see Properties) ----
                float  _CapSalienceStrength;
                float  _CapEnvelopeThreshold;
                float  _CapSolidMargin;
                float  _CapDitherBand;
                float  _EnvelopeBandStrength;
                float  _EnvelopeBands;
                float  _EnvelopeBandPatchScale;
                float  _EnvelopeBandPatchMin;
                float  _EnvelopeBandWarp;
                float  _EnvelopeBandDitherWin;
                // ADR 0027 #6 — the advected foam buffer's compose (default OFF: _WakeFoamStrength 0).
                float  _WakeFoamStrength;
                float  _WakeFoamThreshold;
                float  _WakeFoamSoftness;
                float  _WakeFoamBands;
                float  _WakeFoamLace;
                float  _WakeFoamLaceScale;
                float  _WakeFoamAgeStrength;
                float  _WakeFoamFreshFloor;
                float  _WakeFoamWhiteHold;
                float  _WakeFoamBlueReach;
                float  _WakeFoamDeepReach;
                // ADR 0027 #10 — the capillary ripple band (default OFF: _RippleStrength 0).
                float  _RippleStrength;
                float  _RippleWavelength;
                float  _RippleSpeed;
                float  _DispersionRippleMult;
                float  _RippleWindOnset;
                float  _RippleWindFull;
                float  _RippleWindwardGate;
                float  _RippleLeeFloor;
                float  _RippleBands;
                float  _RippleDitherWin;
                float  _RippleFadeNear;
                float  _RippleFadeFar;
                float  _RippleFadeFloor;
            CBUFFER_END

            // ---- pixelize: snap a world coord to the PPU grid so every layer reads as pixel art (ADR 0010 (2)) ----
            float2 Pixelize(float2 p)
            {
                float ppu = max(_PixelsPerUnit, 1.0);
                return floor(p * ppu) / ppu;
            }

            // ---- a layer's OWN pixel grid: the PPU cell times a divisor (ADR 0027's scale hierarchy) ----
            // Divisor 1 is bit-identical to Pixelize above — the passthrough every consumer defaults to.
            // Twin: WaterDriftLines.PixelizeGrid.
            float2 PixelizeGrid(float2 p, float divisor)
            {
                float ppu = max(_PixelsPerUnit, 1.0) / max(divisor, 1.0);
                return floor(p * ppu) / ppu;
            }

            // ==== ADR 0027 #7 — seabed absorption (per-channel Beer-Lambert on the TRANSMITTED bottom) ========
            // C#-twinned by WaterAbsorption (Sigma / Transmission / BandTransmission / Composite — change
            // one, change BOTH in the same PR). col.rgb ONLY: it never touches depth / clip() / _WaterLevel /
            // the height read / the sim (P1 integrity, rule 5), and it never touches the WATER's own colour —
            // the painted _DepthRamp keeps that authority (ADR 0027 finding 1).
            #define ABSORPTION_PATH 2.0   // light descends the column AND returns: the path is 2d, not d
                                          // (twin: WaterAbsorption.DownAndBack). Not a tunable — turbidity
                                          // is the dial (rule 6).
            #define ABSORPTION_EPS  1e-4  // below this TOTAL sigma the block is skipped: the exact
                                          // sigma = 0 passthrough (twin: WaterAbsorption.MinSigmaSum).

            // sigma (1/m, per channel) = ONE mood-eased turbidity scalar x the fixed per-channel ratio.
            // Factoring it this way is what lets ADR 0017 ease turbidity per weather through a FLOAT in
            // WaterSurface.MoodFloatNames while the per-channel character stays authored art (rule 6).
            float3 AbsorptionSigma()
            {
                return max(_Turbidity, 0.0) * max(_AbsorptionRatio.rgb, 0.0);
            }

            // T = exp(-sigma * 2d): monotone DECREASING in depth on every channel, 1 at the waterline
            // (zero water = you see the ground, which is what §17.1 was faking), and -> 0 with depth in the
            // per-channel order sigma dictates (red first at the default ratio, so the depth-colour shift
            // comes free). `depth` is READ-ONLY here.
            float3 AbsorptionTransmission(float3 sigma, float depth)
            {
                return exp(-sigma * (ABSORPTION_PATH * max(depth, 0.0)));
            }

            // Posterize transmission into N discrete steps (the shader's existing _DepthBands/_SpecBands
            // idiom), DEFAULT ON — the ADR's "every layer carries its own quantization control", which
            // matters here precisely because _DepthBands is 0 so the base ramp contributes no pixel
            // character of its own. Quantizing T (not depth) makes the steps crowd where the bottom is
            // actually fading. bands < 1 = smooth.
            float3 AbsorptionBand(float3 t, float bands)
            {
                if (bands < 1.0) return t;
                return floor(saturate(t) * bands + 0.5) / bands;
            }

            // ==== ADR 0027 #4 + #9 — band scaling with sea state + dispersion-derived speeds ==================
            // C#-twinned by WaterDispersion (BandFrequencyFactor / DeepPhaseSpeed / PhaseSpeed / BandSpeed /
            // SwellPhaseRate / ShoalShift — change one, change BOTH in the same PR). Everything here drives
            // the VISUAL noise octaves only — never depth/clip()/_WaterLevel/the height read/_WaveFieldParams/
            // anything the hulls ride (P1 integrity, rule 5; the promotion into the field is P2, gated on an
            // ADR 0018 amendment).
            #define WATER_GRAVITY 9.81   // standard gravity (m/s^2) — the dispersion relation's ONE physical
                                         // constant (twin: WaterDispersion.Gravity). Not a tunable: feel
                                         // lives in _DispersionScale + the per-band multipliers (rule 6).

            // #4 — a band's EFFECTIVE spatial frequency: its authored scale over (1 + response * chop), so
            // its wavelength grows linearly in the already-pushed sea state (_Chop is sim-pushed — read
            // here, never authored in a material, §12.1). Response 0 divides by EXACTLY 1.0 — today's
            // fixed wavelengths bit-for-bit (the passthrough contract).
            float BandFreq(float scale)
            {
                return scale / (1.0 + max(_BandScaleResponse, 0.0) * saturate(_Chop));
            }

            // #9 — deep-water phase speed c = sqrt(g * lambda / 2pi) (m/s).
            float DispersionDeepSpeed(float lambda)
            {
                float l = max(lambda, 1e-3);
                return sqrt(WATER_GRAVITY * l / 6.2831853);
            }

            // #9 — the finite-depth dispersion relation c = sqrt(g*lambda/2pi * tanh(2pi*d/lambda)): the
            // deep-water form when d >> lambda, sqrt(g*d) as d -> 0, ONE continuous formula (deep and
            // shallow agree at the transition by construction — no branch seam).
            float DispersionPhaseSpeed(float lambda, float depth)
            {
                float l = max(lambda, 1e-3);
                float k = 6.2831853 / l;
                return DispersionDeepSpeed(l) * sqrt(saturate(tanh(k * max(depth, 0.0))));
            }

            // #9 — a world-speed band's scroll rate: the legacy hand-set speed blended toward
            // bandMult * c_deep(lambda) by the master. _DispersionScale = 0 returns the legacy speed
            // EXACTLY (the lerp's 0 endpoint — the passthrough contract). The temporal rate is
            // depth-UNIFORM by design: a per-pixel rate on an absolute-world-coordinate band accumulates
            // UNBOUNDED domain shear (pattern offset between two depths differs by dc*t — on the legacy
            // swell band ~26x the base wavenumber of spurious frequency after 600 s at a typical shoal;
            // the arithmetic is in the twin's doc). The shallow physics enters via the bounded STATIC
            // DispersionShoalShift below instead.
            float DispersionBandSpeed(float legacySpeed, float bandMult, float lambda)
            {
                float target = max(bandMult, 0.0) * DispersionDeepSpeed(lambda);
                return lerp(legacySpeed, target, saturate(_DispersionScale));
            }

            // #9 (shallow) — the bounded, STATIC along-travel drift (m) that bunches a band's wavefronts
            // over a shoal: saturate(master) * bunch * lambda * (1 - c(lambda, d)/c_deep). Where its
            // along-travel gradient compresses the domain by a factor m, the band's local wavelength AND
            // its apparent phase speed both drop by m — waves genuinely slow and bunch approaching shore,
            // off the SAME read-only depth every other layer consumes, with zero time-accumulating
            // artefacts. 0 in deep water; bounded by bunch*lambda at the wet edge; 0 at master 0.
            float DispersionShoalShift(float lambda, float depth)
            {
                float l = max(lambda, 1e-3);
                float slow = 1.0 - DispersionPhaseSpeed(l, depth) / DispersionDeepSpeed(l);
                return saturate(_DispersionScale) * max(_DispersionShoalBunch, 0.0) * l * saturate(slow);
            }

            // ---- world-locked ordered dither (the rigs' own 4x4 Bayer; ADR 0023 §(3) style law) -------------
            // Thresholds are (v + 0.5)/16 exactly as the boat rigs and the facet pass hold them, indexed by
            // the PPU-quantised WORLD cell — world-derived dither cannot crawl under camera translation
            // (the ADR 0022 facet discipline; zero crawl by construction). Used ONLY inside a window around
            // a band/threshold EDGE: full-range Bayer dissolves the quantised bands back into a smooth
            // gradient and the surface reads as airbrushed 3D, not this game (spike run-1, measured).
            static const float BAYER4[4][4] =
            {
                {  0.5/16.0,  8.5/16.0,  2.5/16.0, 10.5/16.0 },
                { 12.5/16.0,  4.5/16.0, 14.5/16.0,  6.5/16.0 },
                {  3.5/16.0, 11.5/16.0,  1.5/16.0,  9.5/16.0 },
                { 15.5/16.0,  7.5/16.0, 13.5/16.0,  5.5/16.0 },
            };
            float BayerWorld(float2 worldXY)
            {
                int2 cell = int2(floor(worldXY * max(_PixelsPerUnit, 1.0)));
                return BAYER4[cell.x & 3][cell.y & 3];   // int & wraps negatives (two's complement)
            }

            // ==== ENVELOPE SALIENCE (ADR 0023 §(4) + §"Whitecap salience retune" — phase 2 step 2) ==========
            // The C# twin is HiddenHarbours.Art.WhitecapSalienceMath — LINE-FOR-LINE; change one, change both
            // in the same commit (the WaveMath twin discipline). WhitecapSalienceMathTests pins the twin to
            // the spike-tuned defaults and to the reference sea's 100%-envelope event (t = 1513.5 s).

            // The SOLID-CORE gate: 0 below the envelope threshold (ordinary chop earns NO solid core), a
            // Bayer-dithered fringe across `ditherBand` just above it (dither at the EDGE only — the style
            // law), hard 1 past `solidMargin` (near-envelope crests wear the solid foam core). The caller
            // feeds `crestFactor` = the field's sharpened height/envelope (WaveFieldSample's crestF), so the
            // gate is envelope-relative by construction — a bigger SEA does not fake a bigger WAVE.
            float CapEnvelopeGate(float crestFactor, float threshold, float solidMargin,
                                  float ditherBand, float bayer)
            {
                float sig = crestFactor - threshold;
                if (sig <= 0.0) return 0.0;
                if (sig >= solidMargin) return 1.0;
                return (sig / max(ditherBand, 1e-4)) > bayer ? 1.0 : 0.0;
            }

            // Posterize a 0..1 value into `bandCount` SOLID steps, Bayer-dithering ONLY inside `ditherWin`
            // (a 0..1 fraction of a band) around each rounding boundary — outside the window the step is
            // hard (solid bands, dithered edges; the spike's exact formula). v = 1 lands the TOP band on
            // every dither cell, so only a near-envelope crest can reach the top shade.
            float BandValue01(float v01, float bandCount, float ditherWin, float bayer)
            {
                float bands = max(bandCount, 2.0);
                float x = saturate(v01) * (bands - 1.0);
                float fb = floor(x);
                float win = clamp(ditherWin, 1e-3, 1.0);
                float e = saturate(((x - fb) - (0.5 - 0.5 * win)) / win);
                return (fb + (e > bayer ? 1.0 : 0.0)) / (bands - 1.0);
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
            // ADR 0027 #4 + #9: the band's frequency scales with sea state (BandFreq) and its speed derives
            // from its wavelength (lambda = 1/scale_eff — the noise cell size in metres) through the
            // dispersion blend; the bounded static shoal drift bunches + slows it over the shallows
            // (shoalDepth = the STILL unwarped depth the caller reads once, guarded — 1e5 = deep = no-op).
            // At the shipped defaults every term is bit-exact passthrough (response 0 divides by exactly
            // 1.0; the dispersion lerp's 0 endpoint; a zero shift adds exactly 0).
            float WindChop(float2 worldXY, float t, float shoalDepth)
            {
                float2 dir = normalize(_WindDir.xy + float2(0, 1e-4));   // +Y fallback on a zero wind dir
                float scaleEff = BandFreq(_WindChopScale);
                float lambda = 1.0 / max(scaleEff, 1e-4);
                float speed = DispersionBandSpeed(_WindChopSpeed, _DispersionChopMult, lambda);
                float2 scroll = dir * (speed * t + DispersionShoalShift(lambda, shoalDepth));
                float2 p1 = Pixelize((worldXY + scroll) * scaleEff);
                float2 p2 = Pixelize((worldXY + scroll * 1.7) * scaleEff * 2.3);
                return ValueNoise(p1) * 0.6 + ValueNoise(p2) * 0.4;
            }

            // ==== ADR 0027 #10 — the CAPILLARY RIPPLE band ====================================================
            // C#-twinned by WaterRipple (WindGate01 / WindwardGate01 / FramingFade01 / Band01 / Amplitude01 /
            // SignedAdd — change one, change BOTH in the same PR). TIER A PERMANENTLY: col.rgb only, never
            // depth/clip()/_WaterLevel/the height read/_WaveFieldParams/the sim (P1 integrity, rule 5). A
            // ripple is surface TEXTURE, not a force — it must never enter the field the hulls ride.
            #define RIPPLE_SLOPE_NORM 2.0     // physical along-wind slope -> a legible 0..1 face signal. The
                                              // SAME 2.0 the swell FACE SHADING uses on the SAME slope, so the
                                              // ripples sit on the face the shading draws (twin:
                                              // WaterRipple.SlopeNormalize). Not a tunable — the dial is
                                              // _RippleWindwardGate (rule 6).
            #define RIPPLE_ADD_CEIL   0.10    // brightness-swing ceiling, below the swell-read band's 0.25 and
                                              // the face shading's 0.15: this is the finest layer on the sea
                                              // and the one most able to turn into glare (twin: AddCeiling).
            #define RIPPLE_WANDER_FREQ   0.35 // the wander field's frequency as a fraction of the ripple's own
                                              // (1/lambda), so one wander cell spans ~3 ripple wavelengths —
                                              // the SwellField "not ruler-straight" idiom at this band's scale.
            #define RIPPLE_WANDER_DRIFT  0.05 // how fast that wander crawls (very slow: the crests wobble, the
                                              // pattern does not boil).
            #define RIPPLE_WANDER_RAD    2.5  // how far it may push the phase (radians) — under a half cycle, so
                                              // the wavefronts stay recognisably wavefronts.

            // (1) THE WIND GATE — 0 at/below the onset, 1 at/above full. Monotone and SATURATING: a gale gets no
            // more ripple than a blow, because by then the whitecaps own the surface. _Roughness is sim-pushed
            // (read here, never authored in a material, §12.1); at 0 this is EXACTLY 0 — glass stays glass.
            float RippleWindGate()
            {
                float lo = saturate(_RippleWindOnset);
                float hi = max(_RippleWindFull, lo + 1e-3);   // never a degenerate smoothstep interval
                return smoothstep(lo, hi, saturate(_Roughness));
            }

            // (2) THE WINDWARD-FACE GATE. slopeAlongWind = dot(waveSlope, windDir) off the SHARED wave field:
            // POSITIVE where the surface climbs as you travel downwind (the windward face of the wave ahead),
            // negative in the lee behind a crest — which is exactly where real wind ripples are and are not.
            // No new uniform: the slope is already sampled by WaveFieldSample for the face shading.
            // _RippleWindwardGate = 0 returns EXACTLY 1 (ripples everywhere); the lee floor keeps a sheltered
            // lee sheltered rather than glass.
            // ⚠️ A dead field publishes zero slope, which would read as "not a windward face" and erase the
            // band — the caller therefore skips this gate entirely when the trains are not live.
            float RippleWindwardGate(float slopeAlongWind)
            {
                float steep = saturate(slopeAlongWind * RIPPLE_SLOPE_NORM);
                return lerp(1.0, max(steep, saturate(_RippleLeeFloor)), saturate(_RippleWindwardGate));
            }

            // (3) THE FRAMING FADE — the anti-DENSITY guard.
            //
            // ⚠️ ADR 0027 asked for a per-DISCRETE-ZOOM-TIER amplitude fade here, on the premise that "at a
            // wider zoom tier a ripple falls below one pixel". THAT FADE DOES NOT EXIST AND MUST NOT BE BUILT.
            // PixelPerfectCamera.assetsPPU is LOCKED at 32 and CameraFollow frames a tier by changing the
            // REFERENCE RESOLUTION, never world-metres-per-pixel — so one pixel is 1/32 m at every framing the
            // game has and a 0.12 m ripple is 3.8 px from the 5.625 m live-haul framing to the 90 m Coastal
            // Packet. It never goes sub-pixel (pinned by RipplePixelFootprintTests; the ADR's #10 amendment
            // carries the ruling).
            //
            // What DOES vary is the CYCLE COUNT: the widest framing shows 33.75 m of sea against the tightest's
            // 5.625 m, so ~6x more ripple cycles at the same pixel size — texture on the swell up close, a dense
            // field competing with the swell bands wide open. Hence a fade over how much sea is on screen.
            //
            // _SeaFramingHeight <= 0 means the global was never published (a bare material, an art scene with no
            // WaterSurface, an inspector preview) => NO fade. Treating unset as "infinitely wide" would render
            // the band blank exactly where someone is trying to look at it.
            float RippleFramingFade()
            {
                if (_SeaFramingHeight <= 0.0) return 1.0;
                float lo = max(_RippleFadeNear, 0.0);
                float hi = max(_RippleFadeFar, lo + 1e-3);
                return lerp(1.0, saturate(_RippleFadeFloor), smoothstep(lo, hi, _SeaFramingHeight));
            }

            // THE BAND ITSELF — fine wavefronts running downwind, broken by a slow pixelized wander so they are
            // not a ruled grating (the SwellField idiom at this band's scale). Returns 0..1.
            //
            // THE CRAWL LAW: the sample position is snapped on the WORLD PPU grid FIRST (Pixelize, then scale),
            // so a ripple cell belongs to a place on the water and stays there while the camera pans — the
            // world-derived discipline the Bayer dither and the ADR 0022 facet pass hold. Never screen space.
            //
            // Speed rides the #9 dispersion blend like every other band, so dialling _DispersionScale up does
            // not leave the ripple as the one band ignoring the relation. At _DispersionScale = 0
            // DispersionBandSpeed returns _RippleSpeed bit-exactly — the PASSTHROUGH contract. The shipped
            // materials carry 0.5, so the live ripple speed is NOT _RippleSpeed.
            float RippleField(float2 worldXY, float t, float2 windN)
            {
                float lambda = max(_RippleWavelength, 0.01);
                float k = 6.2831853 / lambda;
                float speed = DispersionBandSpeed(_RippleSpeed, _DispersionRippleMult, lambda);
                float2 p = Pixelize(worldXY);
                float wander = ValueNoise(p * (RIPPLE_WANDER_FREQ / lambda) + t * RIPPLE_WANDER_DRIFT) - 0.5;
                float phase = (dot(p, windN) - speed * t) * k + wander * RIPPLE_WANDER_RAD;
                return sin(phase) * 0.5 + 0.5;
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
            // shoalDepth: the STILL (unwarped) depth at this pixel, read ONCE by the fragment when the
            // dispersion shoal terms are dialed in (1e5 = deep = every shift is a no-op; ADR 0027 #9).
            float SurfaceNoise(float2 worldXY, float t, float shoalDepth)
            {
                // A — current swell along the tidal set (the existing octave; the foundation). Its scroll
                // rate is _Flow — the sim-pushed TIDAL CURRENT, not a wave phase speed — so #9 leaves it
                // alone; #4 still scales its spatial frequency with the sea state like every band.
                float2 flowDir = normalize(_FlowDir.xy + float2(1e-4, 0));
                float2 scrollA = flowDir * (_Flow * t);
                float2 pA1 = Pixelize((worldXY + scrollA) * BandFreq(_NoiseScale));
                float2 pA2 = Pixelize((worldXY - scrollA * 0.6) * BandFreq(_NoiseScale) * 2.0);
                float octaveA = ValueNoise(pA1) * 0.65 + ValueNoise(pA2) * 0.35;

                // B — wind chop along the wind direction at its own rate (raw 0..1 octave).
                float octaveB = WindChop(worldXY, t, shoalDepth);

                // C — slow cross-swell on a perpendicular axis: either the explicit _CrossSwellDir, or (when
                // that's near-zero) the perpendicular of the average of flow & wind, so it crosses the grain.
                // ADR 0027 #4 + #9: sea-state-scaled frequency, dispersion-derived speed, shoal drift —
                // the same law as the wind-chop band (bit-exact passthrough at the defaults).
                float2 avgDir = normalize(flowDir + normalize(_WindDir.xy + float2(0, 1e-4)) + float2(1e-4, 0));
                float2 autoCross = float2(-avgDir.y, avgDir.x);                  // rotate 90 deg
                float2 crossDir = (dot(_CrossSwellDir.xy, _CrossSwellDir.xy) > 1e-6)
                                    ? normalize(_CrossSwellDir.xy) : autoCross;
                float scaleEffC = BandFreq(_CrossSwellScale);
                float lambdaC = 1.0 / max(scaleEffC, 1e-4);
                float speedC = DispersionBandSpeed(_CrossSwellSpeed, _DispersionCrossMult, lambdaC);
                float2 scrollC = crossDir * (speedC * t + DispersionShoalShift(lambdaC, shoalDepth));
                float octaveC = ValueNoise(Pixelize((worldXY + scrollC) * scaleEffC));

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

            // The VERTEX-STAGE twin of SeabedElevation (ADR 0023): identical math, sampled with
            // SAMPLE_TEXTURE2D_LOD (level 0) because the vertex stage has no derivatives for the
            // implicit-LOD sampler and would not compile with it. _HeightTex is imported without
            // mips, so LOD 0 IS the texture and the two reads return byte-identical elevations —
            // the displaced surface's seam and the fragment's clip() read the SAME seabed by
            // construction (one height map; ADR 0009/0010/0014's P1 integrity rule).
            float SeabedElevationLod(float2 worldXY)
            {
            #if defined(_USE_HEIGHTTEX)
                float2 uv = (worldXY - _HeightWorldMin.xy) / max(_HeightWorldSize.xy, float2(1e-3, 1e-3));
                float r = SAMPLE_TEXTURE2D_LOD(_HeightTex, sampler_HeightTex, uv, 0).r;
                return lerp(_HeightMin, _HeightMax, r);
            #else
                return _HeightMin;   // no height map => everywhere deep (matches SeabedElevation)
            #endif
            }

            // The HLSL TWIN of HiddenHarbours.Core.ShoreFadeMath.Fade01 (ADR 0023) — the SHORE SEAM:
            // exactly 0 at and beyond the walkable waterline (depth <= 0), smoothstep up through the
            // falloff band, exactly 1 at and past it. LINE-FOR-LINE with the C# (1e-4 mirrors
            // ShoreFadeMath.MinBandMeters): any change there changes this in the SAME commit — the
            // WaveMath twin discipline. ShoreFadeMathTests pins the C# side; the ShoreSeamProof
            // editor harness carries this same twin and measured 0 px of coast tear with it.
            // Zero at the depth-0 contour is what keeps the displaced surface from ever
            // contradicting the clip() waterline in frag() — the P1 integrity law of the arc.
            float ShoreFade01(float depth, float band)
            {
                if (depth <= 0.0) return 0.0;
                float t = saturate(depth / max(band, 1e-4));
                return t * t * (3.0 - 2.0 * t);
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
            // ==== TWIN A (begin): the shore helpers, VERBATIM in HiddenHarboursFoamBufferAdvect.shader (BreakerDepositTests pins the copy) ====
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

            // ---- LOCAL seabed slope magnitude (m elevation per m ground) — the shore-cosmetic scale ---------
            // The same central difference ShoreDir reads (the painted-height gradient over ±_ShoreSampleStep),
            // kept as a MAGNITUDE: how steeply the painted beach climbs here. The fragment scales the two
            // cosmetic DEPTH offsets (the §"organic shore fringe" wiggle and the beach swash) by this,
            // saturated at the 1 m/m authoring reference, so their VISIBLE contour excursion equals the
            // authored metres on any painted slope (a gentle bar no longer multiplies them into metres-wide
            // swirl tongues — the 2026-07-23 owner defect). No height map ⇒ 0 (uniform deep has no shore to
            // dress). READ-ONLY of the height field — never depth/clip()/_WaterLevel/the sim (P1, rule 5).
            float SeabedSlopeMag(float2 worldXY)
            {
            #if defined(_USE_HEIGHTTEX)
                float h = max(_ShoreSampleStep, 1e-3);
                float ex = SeabedElevation(worldXY + float2(h, 0)) - SeabedElevation(worldXY - float2(h, 0));
                float ey = SeabedElevation(worldXY + float2(0, h)) - SeabedElevation(worldXY - float2(0, h));
                return length(float2(ex, ey)) / (2.0 * h);
            #else
                return 0.0;                                   // no height map => no shore, nothing to scale
            #endif
            }

            // ==== TWIN A (end) ====

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
                // ADR 0027 #4 + #9 (the LEGACY band — trains-dead path only): sea-state-scaled frequency
                // (BandFreq), a NATIVE-rate dispersion blend (the phase is dot(p*scale, dir) - t*rate, so
                // world speed = rate/scale; blending at the native level keeps scale 0 reproducing the
                // hand-set 0.018 BIT-for-bit — twin: WaterDispersion.SwellPhaseRate), and the bounded
                // static shoal drift (depth here is the fragment's READ-ONLY depth key — rule 5). Every
                // read of _OceanSwellScale/_OceanSwellSpeed in this band carries the same effective
                // values (the wander line included) — one uniform, one meaning.
                float scaleEff = BandFreq(_OceanSwellScale);
                float lambda = 1.0 / max(scaleEff, 1e-4);
                float rate = lerp(_OceanSwellSpeed,
                                  max(_DispersionSwellMult, 0.0) * DispersionDeepSpeed(lambda) * scaleEff,
                                  saturate(_DispersionScale));
                float2 shoaled = worldXY + dir * DispersionShoalShift(lambda, depth);
                // distance projected ALONG the swell axis, advanced slowly with time (long rolling wave).
                float phase = dot(Pixelize(shoaled) * scaleEff, dir) - t * rate;
                // base sine wave (0..1), plus a slow value-noise wander so the bands read organic, not ruled.
                float wave = sin(phase * 6.2831853) * 0.5 + 0.5;
                float wander = ValueNoise(Pixelize(worldXY * scaleEff * 1.3) + t * rate * 0.5);
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
            //   primaryCos— cos(theta) of the DOMINANT (spectral-peak) train, whose slot arrives in
            //               _WaveFieldParams.w: NEGATIVE on the wave's FRONT face (this point crests next —
            //               foam FORMS), POSITIVE behind the crest (it just passed — foam FADES). The
            //               fore/aft asymmetry the whitecap lifecycle keys on.
            //               ⚠️ It used to be slot 0 unconditionally, which was right only because a flat
            //               weighting always puts the biggest train there. A spectrum moves the peak, and
            //               foam breaking on the face of a train that is no longer the big one is a defect
            //               with nothing red to show for it.
            // HLSL discipline: a FIXED [unroll] bound of WAVE_MAX_TRAINS with the live count masked INSIDE
            // (NEVER [unroll] a runtime count — the #96 magenta trap); pow bases floored at 1e-6 because
            // HLSL pow(0, 0) is NaN on some GPUs (the deviation lives where cos(theta) ~ 0 — invisible).
            // ====================================================================================================
            // ==== TWIN B (begin): the fetch march, the breaker contour, the surf march and the bore, VERBATIM in HiddenHarboursFoamBufferAdvect.shader ====
            // ⚠️ WAVE_MAX_TRAINS is ONE HALF OF A SEAM. Its C# counterparts are
            // WaveTrains.MaxTrains, PackedWaveField.MaxTrains and the bridge's uniform push; they
            // move together in one commit or the hull rides a sea the shader is not drawing.
            #define WAVE_MAX_TRAINS 8

            // ==== WIND FETCH (ADR 0027 #1) — the HLSL twin of HiddenHarbours.Core.WaveFetch ==================
            // How far the wind has blown over open water before it reaches this position: march UPWIND over the
            // authored seabed, count how much of that reach was water, and scale the wave field's amplitude by
            // it. Lee shores go calm, exposed shores build — visible before it is felt, and (unlike every other
            // item on the realness pass) ALSO felt, because this envelope multiplies the field the hulls ride.
            //
            // ⚠️ FIXED iteration count, stated by the ADR as an implementation constraint rather than a
            // preference: WaterShaderCompileGuardTests guards the magenta class and [unroll] over a RUNTIME
            // bound is one of its known traps (the #96 magenta incident). The reach is tuned through the STEP
            // LENGTH (_WaveFetchParams.y), never by marching a variable number of steps. This constant is one
            // half of a seam — its C# counterpart is WaveFetch.MarchSteps; they move together in one commit.
            #define FETCH_MARCH_STEPS 24

            // The march's own quantization grid — ⚠️ DELIBERATELY NOT _PixelsPerUnit.
            //
            // Pixelize() divides by the MATERIAL property _PixelsPerUnit, which is an ART knob: the shipped
            // Water.mat sets it 24 and the presets set 12, so the Properties-block default of 32 never ships.
            // The C# twin cannot read a material, so if the march quantized through Pixelize the two sides
            // would snap to grids 4-8 cm apart per step. At a hard painted coast one side's step flips
            // wet->dry where the other's does not, the product accumulator shadows a different step count,
            // and the DRAWN lee boundary lands on a different line than the FELT one — precisely the
            // seen != felt split this item exists to close, reintroduced by a tuning knob.
            //
            // So the fetch march owns its grid, fixed at the PPU the project is actually locked to
            // (PixelPerfectCamera.assetsPPU = 32). Tuning the art knob can no longer move the lee.
            // This constant is one half of a seam: its C# counterpart is WaveFetch.PixelsPerUnit, pinned by
            // MarchPixelGrid_MatchesTheShader, the FETCH_MARCH_STEPS pattern.
            #define FETCH_MARCH_PPU 32.0

            // Twin: WaveFetch.Pixelize. floor(p*ppu)/ppu on the WORLD grid, so the fetch cannot crawl under
            // camera translation (the crawl law) and both sides march the same points.
            float2 FetchPixelize(float2 p)
            {
                return floor(p * FETCH_MARCH_PPU) / FETCH_MARCH_PPU;
            }

            // Fetch -> amplitude multiplier: lerp(leeFloor, 1, fetch^exponent). Exactly the lee floor at fetch
            // 0, exactly 1 at fetch 1 — so a fully exposed shore is untouched and the open sea stays the sea
            // the field was tuned against. Twin: WaveFetch.Amplitude01.
            float FetchAmplitude01(float fetch01)
            {
                float f = pow(max(saturate(fetch01), 1e-6), max(_WaveFetchParams.w, 0.01));
                return lerp(saturate(_WaveFetchParams.z), 1.0, f);
            }

            // The model's own quantization (_WaveFetchParams2.y). ⚠️ DEFAULTS OFF, unlike the col.rgb layers:
            // this envelope is not drawn, it scales GEOMETRY that already-pixelized layers draw downstream, so
            // banding it would stair-step the surface the hull rides for no pixel-character gain. Deliberately
            // NO dither either (unlike RippleBandValue) — a dithered amplitude would have neighbouring pixels
            // of the sea riding different wave heights, and the C# hull twin cannot dither at all.
            // Twin: WaveFetch.Band01.
            float FetchBand01(float v01)
            {
                float bands = _WaveFetchParams2.y;
                if (bands < 2.0) return saturate(v01);
                float steps = bands - 1.0;
                // ⚠️ floor(x + 0.5), NOT round(): HLSL's round() is half-away-from-zero while C#'s
                // Mathf.Round is banker's rounding (half-to-EVEN), so an exact half-band input — which the
                // march reaches whenever the smoothstep saturates and the fraction lands on n/24 — would
                // pick ADJACENT bands on the two sides. Spelled identically in WaveFetch.Band01.
                return floor(saturate(v01) * steps + 0.5) / steps;
            }

            // The finished envelope from an already-marched fetch. strength 0 returns EXACTLY 1 (the shipped
            // passthrough); bounded to (0, 1] for any input, which is what keeps the C# watertight hull clamp
            // a valid bound at every strength. Twin: WaveFetch.Envelope01.
            float FetchEnvelopeFrom(float fetch01)
            {
                float strength = saturate(_WaveFetchParams.x);
                if (strength <= 0.0) return 1.0;
                return lerp(1.0, FetchAmplitude01(FetchBand01(fetch01)), strength);
            }

            // THE MARCH, written ONCE and instantiated against each stage's seabed sampler below — the same
            // two-instantiation pattern SeabedElevation/SeabedElevationLod already uses, and for the same
            // reason: the vertex stage has no derivatives, so it cannot call the implicit-LOD sampler.
            //
            // Land SHADOWS everything behind it: the accumulator is a PRODUCT of per-step wetness, not a count
            // of wet samples, so open water on the far side of an island is not fetch for this position. That
            // is also what keeps the loop branch-free with no early exit — the fixed [unroll] contract.
            // The wetness gate is a smoothstep over _WaveFetchParams2.x metres of depth, not a hard cutoff, so
            // the envelope cannot POP as the tide crosses a shoal (a discontinuity in the waves the hull rides,
            // arriving on the tide's schedule). Sample coords go through FetchPixelize — the march's OWN grid,
            // not the material's _PixelsPerUnit: the fetch must not crawl under camera translation (the crawl
            // law), and the C# twin, which cannot read a material, quantizes identically so both sides march
            // the same points regardless of how the art knob is tuned. Twin: WaveFetch.Fetch01.
            #define FETCH_MARCH_BODY(ELEV_FN)                                                          \
                float strength = saturate(_WaveFetchParams.x);                                         \
                if (strength <= 0.0) return 1.0;              /* OFF: not one texture tap */           \
                float2 upwind = _WaveFetchParams2.zw;                                                  \
                if (dot(upwind, upwind) < 1e-8) return 1.0;   /* dead calm / unpublished */            \
                float stepLen = max(_WaveFetchParams.y, 0.05);                                         \
                float band    = max(_WaveFetchParams2.x, 1e-3);                                        \
                float open = 0.0;                                                                      \
                float blocked = 1.0;                                                                   \
                [unroll]                                                                               \
                for (int fi = 1; fi <= FETCH_MARCH_STEPS; fi++)   /* FIXED bound — never a variable */ \
                {                                                                                      \
                    float2 fp = FetchPixelize(worldXY + upwind * (stepLen * fi));                       \
                    float fdepth = _WaterLevel - ELEV_FN(fp);                                          \
                    blocked *= smoothstep(0.0, band, fdepth);    /* land shadows everything beyond */  \
                    open += blocked;                                                                   \
                }                                                                                      \
                return FetchEnvelopeFrom(open / FETCH_MARCH_STEPS);

            // FRAGMENT stage (implicit-LOD sampler).
            float FetchEnvelope01(float2 worldXY)    { FETCH_MARCH_BODY(SeabedElevation) }
            // VERTEX stage (explicit LOD 0 — _HeightTex has no mips, so the two read byte-identical
            // elevations and the displaced surface rides the SAME lee the fragment draws).
            float FetchEnvelope01Lod(float2 worldXY) { FETCH_MARCH_BODY(SeabedElevationLod) }

            // ==== BREAKING WAVES (ADR 0040) — the HLSL twin ==============================================
            // C# stays the PINNED REFERENCE (BreakerMath + BreakerContour, BreakerMathTests). Change one,
            // change both in the same PR — the WaveMath/WaveFetch discipline. Parity is held at a visual
            // epsilon and in ULP, never bit equality: two transcriptions of one formula cannot be made
            // bit-identical, and pretending otherwise is how a twin test starts lying.
            //
            // Twin: BreakerMath.MarchSteps. FIXED, because [unroll] over a RUNTIME bound is one of the
            // known magenta traps (WaterShaderCompileGuardTests). The reach is tuned through the STEP
            // LENGTH (_BreakerParams.x), never by marching a variable number of steps.
            #define SURF_MARCH_STEPS 16

            // Twin: BreakerMath.DepthAtEnvelope + MidEnvelopeFor. A lee shore's smaller wave carries
            // further in before it breaks, so the break line MOVES with the fetch envelope; the contour is
            // solved at three envelopes and read back piecewise here. A lee floor of 1 (fetch dialled off)
            // collapses to the single anchor, so the whole interpolation is a no-op in that case.
            float BreakerDepthAtEnv(float3 depths, float lee, float e)
            {
                if (lee >= 1.0 - 1e-4) return depths.x;
                float mid = (1.0 + lee) * 0.5;
                if (e >= mid) return lerp(depths.y, depths.x, saturate((e - mid) / max(1.0 - mid, 1e-4)));
                return lerp(depths.z, depths.y, saturate((e - lee) / max(mid - lee, 1e-4)));
            }

            // Twin: BreakerMath.Breaking01FromContour. The smooth break GATE — 1 where the water is
            // shallower than the break depth, 0 out past the gate's outer edge.
            // ⚠️ A GATE, never a scale on the whitewater age. It saturates at 1, which is correct for
            // "is it breaking" and fatal for "how long ago did it break" — the living-wake defect, one
            // level down. The age comes from the march below and nothing multiplies it by this.
            float SurfBreaking01(float depth, float fetchEnv)
            {
                if (_BreakerOuter.w < 0.5) return 0.0;          // this sea breaks nowhere (glass, or off)
                if (depth <= 0.0) return 0.0;                   // dry ground breaks nothing
                float lee = _BreakerDepths.w;
                float bd = BreakerDepthAtEnv(_BreakerDepths.xyz, lee, fetchEnv);
                if (bd <= 0.0) return 0.0;
                float od = max(BreakerDepthAtEnv(_BreakerOuter.xyz, lee, fetchEnv), bd + 1e-3);
                return 1.0 - smoothstep(bd, od, depth);        // shallower = more broken
            }

            // Twin: BreakerMath.MetersSinceBreakAlong. March back UPWAVE accumulating a running PRODUCT
            // of the gate — the WaveFetch land-shadow idiom, so the moment the march steps out of breaking
            // water nothing beyond it counts and a shorebreak never inherits an outer bar's dead foam
            // across a lagoon. Branch-free, which is what keeps the fixed [unroll] with no early exit.
            //
            // ⚠️ Linear in position with no clamp, threshold or posterize before the decay consumes it.
            // Deep inside the surf zone every gate is exactly 1, so the sum would sit on the march grid —
            // what supplies the sub-step fraction is the PARTIAL gate at the surf-zone boundary, which
            // exists only because the gate is a smoothstep. Measured, not argued:
            // BreakerWhitewaterAgeMeasurementTests holds 128 distinct ages against a sabotage arm's 29.
            //
            // Coords go through FetchPixelize — the march's own world grid (FETCH_MARCH_PPU), not the
            // material's _PixelsPerUnit — so the surf cannot crawl under camera translation and the C#
            // twin, which cannot read a material, marches the identical points.
            // Twin: BreakerMath.MarchSinceBreakAlong (ADR 0040 rev 3). ONE loop, BOTH integrals: the metres
            // (Σ contiguous·Δs, exactly the sum this march has always returned) and the SECONDS the bore has
            // been running (Σ contiguous·Δs/√(g·dᵢ), the bore speed at each tap's own depth). A second march
            // was refused; the partial gate at the surf-zone boundary supplies the sub-step fraction to both
            // — and for the clock that is what keeps a bore front off the 2 m grid (BreakerBoreTests: worst
            // neighbour increment 1.04x the smooth Δs/√(g·d) shipped, 4.22x under a hard gate).
            void SurfMarch(float2 worldXY, float2 travelDir, float fetchEnv, out float ageM, out float travelS)
            {
                ageM = 0.0;
                travelS = 0.0;
                if (_BreakerOuter.w < 0.5) return;              // breaks nowhere: not one tap
                float2 back = -travelDir;
                if (dot(back, back) < 1e-12) return;            // no heading, no bore
                back = normalize(back);
                float stepLen = max(_BreakerParams.x, 0.05);
                float g = max(_BreakerParams.z, 0.0);
                float contiguous = 1.0;
                float age = 0.0;
                float seconds = 0.0;
                [unroll]
                for (int si = 1; si <= SURF_MARCH_STEPS; si++)  // FIXED bound — never a variable
                {
                    float2 sp = FetchPixelize(worldXY + back * (stepLen * si));
                    float sdepth = _WaterLevel - SeabedElevation(sp);
                    contiguous *= SurfBreaking01(sdepth, fetchEnv);
                    age += contiguous;
                    float bore = sqrt(g * max(sdepth, 0.02));   // BreakerMath.MinDepthMeters floor
                    seconds += bore > 1e-6 ? contiguous / bore : 0.0;
                }
                ageM = stepLen * age;
                travelS = stepLen * seconds;
            }

            // Twin: BreakerMath.MetersSinceBreakAlong — the metres alone, through the same loop.
            float SurfAgeMeters(float2 worldXY, float2 travelDir, float fetchEnv)
            {
                float ageM, travelS;
                SurfMarch(worldXY, travelDir, fetchEnv, ageM, travelS);
                return ageM;
            }

            // Twin: BreakerMath.WhitewaterEnergy01. exp(-t/tau) on a REAL clock: the age is the marched
            // DISTANCE past the break line divided by the bore's own shallow-water speed sqrt(g*d). The
            // distance is geometry and the speed is physics, so retuning tau moves the whole streak
            // instead of choosing which flat shade it draws in.
            float SurfWhitewater01(float ageMeters, float depth)
            {
                float d = max(depth, 0.02);                     // BreakerMath.MinDepthMeters
                float bore = sqrt(max(_BreakerParams.z, 0.0) * d);
                if (bore <= 1e-6) return 0.0;
                float tau = max(_BreakerParams.y, 1e-3);
                return exp(-(max(ageMeters, 0.0) / bore) / tau);
            }

            // Twin: BreakerMath.WhitewaterByTravel01 - the whitewater by the seconds the bore has actually
            // RUN (the march's travel integral), not by its metres over the LOCAL speed, which at the wet
            // edge is near zero and pronounces every wash dead before the sand. The run-up rides this;
            // the drawn sheet blends toward it only as the run-up dial comes up (today's sheet at 0).
            float SurfWhitewaterByTravel01(float travelS)
            {
                float tau = max(_BreakerParams.y, 1e-3);
                return exp(-max(travelS, 0.0) / tau);
            }

            // ==== THE BORE (ADR 0040 rev 3): one crest at a time — twins of BreakerMath's bore functions ====
            // Nothing here is accumulated, reconstructed or saved: the bore's phase is the field's PUBLISHED
            // phase at the break line, read FORWARD at minus the travel time the march just integrated
            // (sampling the published trains at a negative time is legal — the animator bakes the travel
            // into _WavePhases and the shader samples at t = 0, so t = -tau is the same field tau seconds
            // ago). No _Time anywhere in it: the bore advances because the bridge advances the phases.

            // The dominant train's angular frequency, from the packed wave number and the field's gravity:
            // omega = k * c = k * sqrt(g/k) = sqrt(g*k) (the deep-water dispersion relation WaveTrain's
            // constructor carries; the period is conserved through shoaling, so this is the bore's beat on
            // every depth). Twin: 2*pi / BreakerMath.PeriodSeconds.
            float SurfBoreOmega()
            {
                float4 trains[WAVE_MAX_TRAINS] = { _WaveTrain0, _WaveTrain1, _WaveTrain2, _WaveTrain3,
                                                   _WaveTrain4, _WaveTrain5, _WaveTrain6, _WaveTrain7 };
                int dominant = clamp((int)(_WaveFieldParams.w + 0.5), 0, WAVE_MAX_TRAINS - 1);
                float k = max(trains[dominant].z, 1e-6);
                return sqrt(max(_BreakerParams.z, 0.0) * k);
            }

            // Twin: BreakerMath.BorePhaseDegrees(train, breakLinePoint, travelSeconds, freqScale) — degrees
            // in [0, 360). The published phase FALLS with time (theta = k*(d*pos) - omega*t + phi), so the
            // phase tau seconds AGO is theta + omega*tau. 90 degrees is the crest — the bore's FRONT — because
            // the field's profile is (1 + sin theta)/2. freqScale is the DRAWN scale (_OceanSwellScale /
            // 0.025), applied to the position exactly as the C# does, so the bore leaves the break line with
            // the crest the eye sees arrive there.
            float SurfBorePhaseDeg(float2 breakLinePoint, float travelS, float freqScale)
            {
                float4 trains[WAVE_MAX_TRAINS] = { _WaveTrain0, _WaveTrain1, _WaveTrain2, _WaveTrain3,
                                                   _WaveTrain4, _WaveTrain5, _WaveTrain6, _WaveTrain7 };
                float phis[WAVE_MAX_TRAINS] = { _WavePhases.x,  _WavePhases.y,  _WavePhases.z,  _WavePhases.w,
                                                _WavePhases2.x, _WavePhases2.y, _WavePhases2.z, _WavePhases2.w };
                int dominant = clamp((int)(_WaveFieldParams.w + 0.5), 0, WAVE_MAX_TRAINS - 1);
                float k = trains[dominant].z;
                float fs = max(freqScale, 1e-3);
                float theta = k * dot(trains[dominant].xy, breakLinePoint * fs) + phis[dominant]
                            + SurfBoreOmega() * max(travelS, 0.0);
                float deg = theta * 57.29577951;
                return deg - 360.0 * floor(deg / 360.0);
            }

            // Twin: BreakerMath.BorePulse01 — a SMOOTH periodic hump peaking at the front (90 degrees),
            // ((1 + sin theta)/2)^sharpness. sharpness <= 0 returns exactly 1: no pulse, the steady state.
            float SurfBorePulse01(float phaseDeg, float sharpness)
            {
                if (sharpness <= 0.0) return 1.0;
                float s = (sin(phaseDeg * 0.01745329252) + 1.0) * 0.5;
                return pow(saturate(s), sharpness);
            }

            // Twin: BreakerMath.SecondsSinceTheCrest — how long ago the crest that owns this bore passed
            // the break line, counted back from the bore's own birth: Repeat(90 - phase, 360)/360 * T.
            float SurfSecondsSinceCrest(float phaseDeg, float periodS)
            {
                float d = 90.0 - phaseDeg;
                d = d - 360.0 * floor(d / 360.0);
                return d / 360.0 * max(periodS, 0.0);
            }

            // Twin: BreakerMath.SignedSecondsFromCrest - positive behind the front, negative ahead of it,
            // in (-T/2, T/2]: the coordinate the travelling anatomy measures the lip, barrel and pocket on.
            float SurfSignedSecondsFromCrest(float phaseDeg, float periodS)
            {
                float d = 90.0 - phaseDeg + 180.0;
                d = d - 360.0 * floor(d / 360.0) - 180.0;
                return d / 360.0 * max(periodS, 0.0);
            }

            // Twin: BreakerMath.BoreSheet01. The SHEET is born at the front and ages behind it on the
            // whitewater's own seconds (_BreakerParams.y) - one decay law for "how far" and "how long ago".
            float SurfBoreSheet01(float phaseDeg, float periodS)
            {
                float tau = max(_BreakerParams.y, 1e-3);
                return exp(-SurfSecondsSinceCrest(phaseDeg, periodS) / tau);
            }

            // Twin: WaveMath.Sample(pos*fs, -timeBack, field).CrestFactor — the field's crest factor at a
            // position TIME-SHIFTED into the past. The same eight-train unrolled loop WaveFieldSample runs
            // (height + crest factor only, no slope), with each train's phase advanced by its own omega =
            // sqrt(g*k) (unscaled k: the drawn scale changes the wavelength on screen, never the beat).
            // ⚠ COST: this is a tenth train-loop call site per pixel (WaveFieldCostReport counts it), paid
            // only where the surf is alive and _BreakerBore.y > 0.
            float WaveFieldSampleAt(float2 worldXY, float freqScale, float fetchEnv, float timeBack)
            {
                float4 trains[WAVE_MAX_TRAINS] = { _WaveTrain0, _WaveTrain1, _WaveTrain2, _WaveTrain3,
                                                   _WaveTrain4, _WaveTrain5, _WaveTrain6, _WaveTrain7 };
                float phis[WAVE_MAX_TRAINS] = { _WavePhases.x,  _WavePhases.y,  _WavePhases.z,  _WavePhases.w,
                                                _WavePhases2.x, _WavePhases2.y, _WavePhases2.z, _WavePhases2.w };
                int count = (int)(_WaveFieldParams.x + 0.5);
                float p = max(_WaveFieldParams.y, 1.0);
                // Twin: BreakerMath.BoreBirthEnergy01 - the height at the crest's passage against the
                // DOMINANT's own amplitude (a total-normalized crest factor can never birth a full bore
                // in a many-train sea; measured ~0.1 on the shot sea).
                int dominant = clamp((int)(_WaveFieldParams.w + 0.5), 0, WAVE_MAX_TRAINS - 1);
                float dominantAmp = max(trains[dominant].w, 1e-6);
                float fs = max(freqScale, 1e-3);
                float g = max(_BreakerParams.z, 0.0);
                float height = 0.0;
                [unroll]
                for (int i = 0; i < WAVE_MAX_TRAINS; i++)          // FIXED bound; the count masks inside
                {
                    float amplitude = trains[i].w;
                    if (i < count && amplitude > 0.0)
                    {
                        float k = trains[i].z;
                        float theta = k * fs * dot(trains[i].xy, worldXY) + phis[i] + sqrt(g * k) * timeBack;
                        float s = (sin(theta) + 1.0) * 0.5;
                        height += amplitude * (2.0 * pow(max(s, 1e-6), p) - 1.0);
                    }
                }
                height *= saturate(fetchEnv);
                return saturate(height / dominantAmp);
            }

            // Twin: BreakerMath.BoreBirthEnergy01 — how big a crest this bore was born from: the field's
            // crest factor at the break line when THAT crest passed it (the bore's birth, minus the time
            // since its crest), blended toward 1 by the set strength. A lone train reads exactly 1.
            float SurfBoreBirth01(float2 breakLinePoint, float travelS, float phaseDeg, float periodS,
                                  float freqScale, float fetchEnv)
            {
                float setStrength = saturate(_BreakerBore.y);
                if (setStrength <= 0.0) return 1.0;
                float back = max(travelS, 0.0) + SurfSecondsSinceCrest(phaseDeg, periodS);
                float crest = saturate(WaveFieldSampleAt(breakLinePoint, freqScale, fetchEnv, back));
                return lerp(1.0, crest, setStrength);
            }

            // Twin: BreakerMath.RunUpMeters — Hunt (1959): R = coefficient * xi * H, xi clamped at the law's
            // measured 2.3, capped at the drawn-edge ceiling, scaled by what is left of the bore and pulsing
            // with it. Metres of LEVEL, applied to the same local foam-only depth the swash moves.
            float SurfRunUpMeters(float standingAtBreak, float alive, float xi, float bore)
            {
                float coefficient = max(_BreakerBore.z, 0.0);
                float cap = max(_BreakerBore.w, 0.0);
                if (coefficient <= 0.0 || cap <= 0.0) return 0.0;
                float x = clamp(xi, 0.0, 2.3);                  // BreakerMath.HuntIribarrenLimit
                float reach = coefficient * x * max(standingAtBreak, 0.0);
                return min(cap, reach) * saturate(alive) * saturate(bore);
            }

            // Twin: BreakerMath.BoreFrontSlope — the derivative of the bore's own height profile
            // H * pulse(psi) along the travel direction: d pulse/d psi = p*s^(p-1)*cos(psi)/2 with
            // s = (1 + sin psi)/2, and the phase advances along the path at omega / sqrt(g*d) — the clock's
            // own rate. This is the FACE the relief light and the sun's shade could not see before (register
            // row 1: "the light cannot see a crash"). Metres of rise per metre; 0 with no pulse.
            float SurfFrontSlope(float phaseDeg, float sharpness, float frontHeightM, float boreSpeed, float omega)
            {
                if (sharpness <= 0.0 || frontHeightM <= 0.0 || boreSpeed <= 1e-6) return 0.0;
                float rad = phaseDeg * 0.01745329252;
                float s = (sin(rad) + 1.0) * 0.5;
                if (s <= 1e-6) return 0.0;
                float dPulse = sharpness * pow(s, sharpness - 1.0) * cos(rad) * 0.5;
                return frontHeightM * dPulse * (omega / boreSpeed);
            }

            // ==== TWIN B (end) ====

            // Posterize the surf like every other band here, dithering the step edges off the same
            // world-locked Bayer cell the foam edge and the ripple bands use. Smooth surf reads as
            // airbrushed 3D; hard steps read as banding; dithered steps read as pixel art.
            // Twin: BreakerMath.Iribarren. The surf-similarity number xi = tanB / sqrt(H0/L0) — the one
            // dimensionless number that decides what KIND of breaker a place makes. tanB comes from the
            // painted bed's own gradient (SeabedSlopeMag, which this shader already derives for the
            // shore cosmetics), so the classification reads the same height map everything else does.
            float SurfIribarren(float2 worldXY)
            {
                float h0 = max(_BreakerParams.w, 0.0);
                float l0 = max(_BreakerAnatomy.x, 1e-3);
                float steepness = max(h0 / l0, 1e-6);
                return SeabedSlopeMag(worldXY) / sqrt(steepness);
            }

            // Twin: BreakerMath.PlungingWeight01. ClassFor's hard table, softened into a weight so the
            // anatomy fades instead of snapping along a contour as the sampled bed gradient crosses a
            // threshold. WARNING: this widens NOTHING — at the band's centre it agrees with the hard
            // classification exactly, and it is 0 wherever the table says spilling. Barrels appear only
            // where the bathymetry earns them, and no knob in this shader can change that.
            float SurfPlunging01(float xi)
            {
                float spilling = max(_BreakerAnatomy.z, 0.0);
                float plunging = max(spilling + 1e-3, _BreakerAnatomy.w);
                // STRADDLING each threshold, not starting at it: the half-weight crossing has to land on
                // Battjes' number or the softening has quietly moved the classification. (It did, in the
                // first version — ξ 0.641 against a published 0.5, caught by the C# side's own test.)
                float half = (plunging - spilling) * 0.05;
                float risingIn = smoothstep(spilling - half, spilling + half, xi);
                float fallingOut = smoothstep(plunging - half, plunging + half, xi);
                return saturate(risingIn * (1.0 - fallingOut));
            }

            float SurfBandValue(float v01, float bay)
            {
                float bands = _SurfBands;
                if (bands < 2.0) return saturate(v01);
                float steps = bands - 1.0;
                float dithered = saturate(v01) + (bay - 0.5) * (saturate(_SurfBandDither) / max(steps, 1.0));
                return floor(saturate(dithered) * steps + 0.5) / steps;
            }

            // ⚠️ COST, stated rather than hidden: the march is FETCH_MARCH_STEPS height-map taps. Resolve it
            // ONCE per pixel / per vertex and pass the result into every WaveFieldSample below — the finite-
            // difference taps (foam convergence, caustic curvature, drift lines) sample a few centimetres
            // apart, and the envelope turns over TENS OF METRES, so re-marching for each of them would pay
            // ~13x for a number that does not change. That is also why the envelope is a PARAMETER here and
            // not an internal call: the discipline is visible at every call site.
            void WaveFieldSample(float2 worldXY, float freqScale, float fetchEnv,
                                 out float height, out float2 slopeXY, out float crestF, out float primaryCos)
            {
                height = 0.0;
                slopeXY = float2(0.0, 0.0);
                crestF = 0.0;
                primaryCos = 0.0;

                float4 trains[WAVE_MAX_TRAINS] = { _WaveTrain0, _WaveTrain1, _WaveTrain2, _WaveTrain3,
                                                   _WaveTrain4, _WaveTrain5, _WaveTrain6, _WaveTrain7 };
                float phis[WAVE_MAX_TRAINS] = { _WavePhases.x,  _WavePhases.y,  _WavePhases.z,  _WavePhases.w,
                                                _WavePhases2.x, _WavePhases2.y, _WavePhases2.z, _WavePhases2.w };
                int count = (int)(_WaveFieldParams.x + 0.5);
                float p = max(_WaveFieldParams.y, 1.0);            // crest sharpening (>= 1, like the C# clamp)
                float totalAmp = _WaveFieldParams.z;
                // The SPECTRAL PEAK's slot, not slot 0. Under the flat weighting these are the same
                // train and .w publishes 0; once JONSWAP re-weights, the whitecap lifecycle must key
                // on the face of the biggest wave, not on whichever train happens to sit first.
                int dominant = (int)(_WaveFieldParams.w + 0.5);
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

                        if (i == dominant) primaryCos = cosT;      // the DOMINANT train's face sign (see doc)
                    }
                }

                // The FETCH envelope (ADR 0027 #1), applied where the C# twins apply it: scaling height and
                // slope, BEFORE the crest factor. totalAmp deliberately does NOT scale, so a lee shore loses
                // its whitecaps for free — correct, and the reason fetch must never be folded into the trains.
                // Only the E*grad(h) term of grad(E*h) is taken; E turns over the fetch scale (tens of metres)
                // and h over the wavelength (metres), so the dropped term is small by construction.
                // Twins: WaveMath.Sample and WaveFieldBridge.ShaderTwinSample carry this identical block.
                float fetchE = saturate(fetchEnv);
                height  *= fetchE;
                slopeXY *= fetchE;

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

            // ---- ADR 0027 #3: the CONVERGENCE (Jacobian) gate ------------------------------------------------
            // C#-twinned by WaterFoam.Convergence (change one, change BOTH in the same PR). Approximates
            // the Gerstner horizontal drift toward crests as D = pinch * (grad-derived), whose Jacobian is
            //   J = (1 + q*hxx)(1 + q*hyy) - (q*hxy)^2
            // — curvature is NEGATIVE at a crest, so a crest CONVERGES (J < 1); hxy is the cross term two
            // CROSSING trains write (it enters squared). saturate(1 - J): 0 on a flat sea, 0 at zero
            // pinch, positive where the surface pinches, saturating as it folds. Visual-only downstream
            // (feeds the existing thresholded cap field; never depth/clip()/_WaterLevel/the sim — rule 5).
            float ConvergenceGate(float hxx, float hyy, float hxy, float pinch)
            {
                float q = max(pinch, 0.0);
                float jxx = 1.0 + q * hxx;
                float jyy = 1.0 + q * hyy;
                float jxy = q * hxy;
                float J = jxx * jyy - jxy * jxy;
                return saturate(1.0 - J);
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
            //   2) HASH-UNTILE — per repeat-cell, offset the lookup by a cell hash, then blend the FOUR
            //      cell-corner variants by bilinear weights so adjacent cells differ yet never seam.
            // PIXEL-ART faithful: the offset is added to the WORLD coord BEFORE PaintUV pixelizes, so the
            // untiled lookup still snaps to the PPU grid and stays point-sampled. Pass scroll for the drift.
            //
            // 🔴 THE SEAM THIS BLEND EXISTS TO NOT DRAW (owner playtest 2026-08-06: "thin, non-organic
            // straight lines" in otherwise-good shore foam). The blend used to pick TWO variants — this
            // cell's hashed translation and the DIAGONAL neighbour's — and mix them by
            // w = smoothstep(0.2,0.8,fx)*0.5 + smoothstep(0.2,0.8,fy)*0.5. Cross a cell boundary and the
            // cell index steps, so BOTH variants are replaced by two unrelated ones, while w jumps by 0.5:
            // the two sides of that edge share no variant and no weight, so the value simply JUMPS. At the
            // shipped _PaintScale 0.25 that is a hard seam every 4 m, in both axes, on every painted slot —
            // bent (not straightened) by the domain warp above, which is exactly why it read as wandering
            // thin lines rather than as a tile grid, and why no amount of foam noise hid it.
            //
            // The fix is the standard four-corner blend: weights that VANISH on the two edges they do not
            // touch, so at fx -> 1 only the (i+1,.) corners survive and those are precisely the corners the
            // next cell reads at fx -> 0. Continuity is then structural, not tuned. smoothstep's zero
            // end-derivative makes the join C1, so no gradient edge is left behind either.
            //
            // ⚠️ COST, stated rather than hidden (rule 7): 4 corner taps instead of 2, i.e. 5 fetches per
            // painted slot while _UntileStrength > 0 (the raw tap is unchanged and is still the ONLY tap at
            // strength 0). Two variants cannot cover the four corners a 2-D lattice joins at, so the
            // correctness is not available more cheaply here.
            // Twin: HiddenHarbours.Art.WaterUntile (CornerWeights / Blend, and the deliberately wrong
            // LegacyTwoVariantWeight the tests measure the old jump against).
            half4 UntileSampleW(TEXTURE2D_PARAM(tex, smp), float2 worldXY, float scale, float2 scroll, float strength)
            {
                float s = saturate(strength);
                // (1) domain warp: a small world-space nudge from the surface noise, scaled by strength.
                // Two warp octaves (low-freq bend + a finer ripple) read more organic than one straight nudge;
                // both still dialed by _UntileStrength (no new knob) so 0 strength = the raw grid unchanged.
                // ADR 0027 #4: the untile warp fields read the surface band's EFFECTIVE frequency
                // (BandFreq) so the painted-texture break-up coarsens with the same growing sea —
                // one uniform, one meaning (bit-exact at response 0: division by exactly 1.0).
                float2 warpLo = float2(ValueNoise(worldXY * BandFreq(_NoiseScale) * 0.5 + 3.1),
                                       ValueNoise(worldXY * BandFreq(_NoiseScale) * 0.5 + 8.7)) - 0.5;
                float2 warpHi = float2(ValueNoise(worldXY * BandFreq(_NoiseScale) * 1.7 + 17.3),
                                       ValueNoise(worldXY * BandFreq(_NoiseScale) * 1.7 + 42.9)) - 0.5;
                float2 warpN = warpLo + warpHi * 0.4;
                float2 warped = worldXY + warpN * (s * 1.5);

                half4 raw = SAMPLE_TEXTURE2D(tex, smp, PaintUV(warped, scale, scroll));
                if (s <= 0.001) return raw;

                // (2) hash-untile in TILE space (uv = warped*scale; one repeat-cell == 1 unit of uv).
                float2 uv  = Pixelize(warped + scroll) * max(scale, 1e-4);
                float2 iuv = floor(uv);
                float2 fuv = frac(uv);
                // The FOUR cell-corner offsets, each hashed to a per-cell world translation (world-space,
                // so PaintUV still snaps every lookup to the PPU grid). Corner (i+1,j) is the SAME corner
                // the next cell along x calls its own (i,j) — that shared identity is what closes the seam.
                float2 off00 = Hash22(iuv + float2(0.0, 0.0)) * 64.0;   // a few tiles of world translation
                float2 off10 = Hash22(iuv + float2(1.0, 0.0)) * 64.0;
                float2 off01 = Hash22(iuv + float2(0.0, 1.0)) * 64.0;
                float2 off11 = Hash22(iuv + float2(1.0, 1.0)) * 64.0;
                half4 v00 = SAMPLE_TEXTURE2D(tex, smp, PaintUV(warped + off00, scale, scroll));
                half4 v10 = SAMPLE_TEXTURE2D(tex, smp, PaintUV(warped + off10, scale, scroll));
                half4 v01 = SAMPLE_TEXTURE2D(tex, smp, PaintUV(warped + off01, scale, scroll));
                half4 v11 = SAMPLE_TEXTURE2D(tex, smp, PaintUV(warped + off11, scale, scroll));
                // Bilinear corner weights on a smoothstep ramp: a partition of unity (so the blend can
                // never brighten or darken the slot) whose members are each 0 on the two edges they do not
                // touch. Twin: WaterUntile.CornerWeights.
                float bx = smoothstep(0.0, 1.0, fuv.x);
                float by = smoothstep(0.0, 1.0, fuv.y);
                half4 untiled = lerp(lerp(v00, v10, bx), lerp(v01, v11, bx), by);
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
            //
            // The CALM fade for the swash: a mirror-still bay should not have surf running up it, but a
            // chop should wash properly. Reuses the swell read's sea-state axis (_SwellReadSeaStateLo/Hi)
            // rather than inventing a second pair of thresholds — one axis, one place to tune. Returns
            // 1 at/above the "full" threshold and (1 - _SwashCalmGate) at/below the glassy one, so
            // _SwashCalmGate = 0 is "identical at every sea-state" (the pre-fix behaviour).
            // Twin: HiddenHarbours.Art.WaterSurface.SwashSeaStateGate.
            float SwashSeaStateGate()
            {
                float lo = saturate(_SwellReadSeaStateLo);
                float hi = max(saturate(_SwellReadSeaStateHi), lo + 1e-3);
                float sea = smoothstep(lo, hi, saturate(_Chop));
                return lerp(1.0 - saturate(_SwashCalmGate), 1.0, sea);
            }

            // SHOREWARD PHASE (the fix): the crest travels IN from the sea toward the beach, not around it.
            // The old phase advanced along a FIXED WORLD DIAGONAL (world X+Y): on the round island's ring-
            // shaped foam band a crest moving in one compass direction sweeps AROUND the ring's circumference
            // — reading as the foam "rotating" around the island. Real run-up rolls SHOREWARD, perpendicular
            // to the local coast, everywhere. So we drive the phase by the SHOREWARD coordinate: `depth`
            // (which decreases toward shore). A crest sits at constant total phase theta = t*speed*2pi +
            // depth*wavelength; holding theta as time grows forces depth to SHRINK, so over time each crest
            // marches to ever-shallower water — i.e. IN toward the beach — the SAME radial motion everywhere.
            //   depth  — the LOCAL (visual) depth at this pixel; shoreward = decreasing depth. Never the real
            //            clip depth (caller passes the same read-only depth it feeds the foam gate). P1-safe.
            // A subtle along-shore DESYNC (value-noise sampled along the shore TANGENT, low _SwashAlongShoreVary)
            // breaks neighbouring stretches slightly out of sync so the wash isn't one flat pulsing line —
            // WITHOUT a single world direction, so it never becomes a coherent wave travelling around the ring.
            float BeachSwash(float2 worldXY, float depth, float t)
            {
                // shore-normal (toward land) from the seabed gradient; tangent = along the coast.
                float2 shore = ShoreDir(worldXY);
                float2 tangent = float2(-shore.y, shore.x);   // 90deg rotation; length matches shore (0 if flat)
                // low-amplitude desync sampled ALONG the coast (per-stretch offset), so adjacent bits of beach
                // break a touch out of phase — organic, but carries no fixed travel direction around the ring.
                float alongCoord = dot(Pixelize(worldXY), tangent) * 0.35;
                float desync = (ValueNoise(float2(alongCoord, alongCoord * 0.7 + 11.3)) - 0.5)
                               * _SwashAlongShoreVary * 6.2831853;
                // On the real coast the phase RUNS UP the beach. A crest sits at a constant total phase
                // theta = t*w + depth*k; holding theta as t grows forces depth to SHRINK, i.e. the crest
                // marches to ever-shallower water = IN toward the beach (the same radial run-up everywhere on
                // the ring). On flat seabed (ShoreDir == 0, open deep water / no height map) there is no
                // shoreward axis — fall back to a gentle time-only pulse so the wet edge still animates, but
                // with NO travelling term (so no fixed-direction sweep can circle the island there either).
                bool haveShore = dot(shore, shore) > 1e-6;
                float shoreward = haveShore ? (max(depth, 0.0) * _SwashWavelength) : 0.0;
                float base = t * _SwashSpeed * 6.2831853 + shoreward;
                // two beats slightly out of phase read as overlapping run-up/backwash, not a metronome.
                float wave = sin(base + desync) * 0.7
                           + sin(base * 0.5 + desync * 1.7) * 0.3;
                return wave * _SwashAmplitude * SwashSeaStateGate();
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

            // ==== ADR 0027 #8 — OBJECT REFLECTIONS: the HHReflect list, warped by the shared wave field ========
            // IsoFacetHullFeature draws every reflective renderer MIRRORED about its own published
            // ground-contact pivot (ADR 0026) into _HHReflectTex — a fourth filtered renderer list, one
            // ARGBHalf target at camera render resolution, zero cost when nothing is reflective. This reads
            // it back with the lookup displaced by the SAME WaveFieldSample() the hull is riding, so a
            // reflection wobbles on the very crests the boat rides. No new sim uniform (ADR 0018 reused).
            //
            // ⚠️ THE LOOKUP SNAPS IN WORLD SPACE. A render target is screen-space by nature, and that is
            // exactly the trap: with CameraFollow panning continuously behind the boat, a lookup quantized on
            // the RT's own grid CRAWLS on every pan — the one artefact that would make this read as a screen
            // filter rather than a reflection. Snapping the SAMPLE POSITION on the world PPU grid (Pixelize,
            // the crawl law §3) means a reflection cell belongs to a place on the water and stays there while
            // the camera moves. Twin: WaterReflectionWarp.WarpedSampleWorld — and its deliberately wrong
            // sibling ScreenSnappedSampleWorld, which exists only so the tests can MEASURE the crawl.
            //
            // ⚠️ Read with Load(), not a uv sample. The target is exactly camera-render-resolution so the map
            // is 1:1, and Load shares SV_POSITION's coordinate convention — which removes the render-target
            // Y-flip ambiguity a uv fetch would smuggle in (the class of bug that renders correctly on one
            // graphics API and upside down on the next).
            //
            // Sea-state response is INHERITED from the shipped §11 curves, not re-invented: sharp on glass,
            // broken in chop, gone in a storm. col.rgb ONLY — never depth/clip()/_WaterLevel/the height read/
            // the sim (P1 integrity, rule 5). _ObjectReflectStrength = 0 skips the whole block.
            //
            // Returns the sample SPLIT (the SkyContentReflection idiom):
            //   `pre`  — rgb = the PREMULTIPLIED reflected surface, a = its coverage. The caller composites
            //            it with the premultiplied over-operator BEFORE the palette grade, so a reflected
            //            hull COVERS the sky mirror under it and dims with the night like the rest of the sea.
            //   `post` — the PRE-COMPENSATED light content (a lit wheelhouse), ADDED after the grade exactly
            //            as the moon glitter is; otherwise the day/night multiply crushes it to ~3%.
            // The split needs no flag channel: an ordinary reflection's premultiplied rgb can never exceed
            // its coverage, and a compensated one exceeds it by precisely its light content (see
            // HHReflectPremultiply; twin: WaterReflectionWarp.SplitLitShare).
            void ObjectReflection(float2 worldXY, float2 waveSlope, float2 screenPx, float depth,
                                  out float4 pre, out float3 post)
            {
                pre = 0.0;
                post = 0.0;
                if (_ObjectReflectStrength <= 0.001) return;

                float strength = ReflectionStrength() * saturate(_ObjectReflectStrength);
                if (strength <= 0.001) return;

                // WHERE on the water this fragment reads its reflection from: displaced by the wave slope,
                // then snapped on the WORLD grid. Sharpness widens the displacement as the sea breaks up —
                // the same "a rough sea scatters the mirror" law the sky reflection already follows.
                float scatter = 2.0 - ReflectionSharpness();          // 1 at glass -> 2 when fully broken
                float2 sampleWorld = Pixelize(worldXY + waveSlope * _ObjectReflectWarp * scatter);

                // World delta -> PIXEL delta. For this project's unrotated orthographic camera the view
                // matrix is a pure translation, so the projection's diagonal alone converts metres to clip,
                // and _ScreenParams turns clip into pixels. _ProjectionParams.x carries the render-target Y
                // flip (it is -1 when the projection is flipped), which is what keeps the wobble leaning the
                // same way on every graphics API.
                float2 pxPerMetre = float2(UNITY_MATRIX_P[0][0] * _ScreenParams.x,
                                           UNITY_MATRIX_P[1][1] * _ScreenParams.y) * 0.5;
                float2 dPix = (sampleWorld - worldXY) * pxPerMetre;
                dPix.y *= _ProjectionParams.x;

                int2 px = int2(screenPx + dPix);
                // Off-target reads would wrap or clamp a stale edge column across the sea; a reflection that
                // has left the screen simply is not there.
                if (any(px < 0) || any(px >= int2(_ScreenParams.xy))) return;
                float4 refl = LOAD_TEXTURE2D(_HHReflectTex, px);

                // A reflection SINKS as the water deepens under it: a mirrored hull reads crisply against a
                // shallow bottom and dissolves over the dark. Cheap, optional (0 = no fade), and it keeps the
                // reflective set from painting hard silhouettes across open water.
                float sink = 1.0 - saturate(_ObjectReflectSink) * saturate(depth * 0.25);

                // The premultiplied split (twin: WaterReflectionWarp.SplitLitShare). min/max against the
                // coverage separates ordinary reflected surface from pre-compensated light content with no
                // flag channel, no second target and no extra uniform. By day the compensation factor is ~1,
                // so a lit source stays under coverage and the whole sample lands in `pre` — daylight is
                // unchanged, which is correct rather than a special case.
                float  cov      = saturate(refl.a);
                float3 ordinary = min(refl.rgb, cov.xxx);
                float3 lit      = max(refl.rgb - cov.xxx, 0.0);
                float  weight   = strength * sink;

                pre  = float4(ordinary * weight, cov * weight);
                post = lit * weight;
            }

            // ==== ADR 0027 #6 — THE ADVECTED FOAM BUFFER: read the mark left on this patch of sea ==========
            // IsoFacetHullFeature keeps one persistent single-channel buffer, ping-ponged per frame:
            // scrolled in WHOLE world cells (camera window + wind/current drift), decayed on a half-life,
            // and injected wherever a hull works against the water — BOTH horizontally (speed through the
            // water) and VERTICALLY (the hull's heave rate relative to the local wave surface, which is the
            // bobbing case the owner asked for on 2026-08-01 and which BoatWakeEmitter has no signal for).
            //
            // This returns a COVERAGE the caller ADDS to the foam it has already composed. It never
            // replaces the fringe foam, the whitecaps, or the emitter's sprite trail — the buffer's job is
            // the foam none of those can make: marks that PERSIST and DRIFT after the boat has gone.
            //
            // 🔴 ZERO CRAWL BY CONSTRUCTION, and it is the reason this item is built the way it is. The
            // lookup is a WORLD position mapped through the buffer's own CELL-SNAPPED window: the origin
            // only ever moves in whole world cells, and the target is point-filtered, so a given patch of
            // sea always resolves to the same texel. There is no screen-space step anywhere in this read.
            // Sampling the RT on its own camera-relative grid — the natural thing to reach for once a
            // wake lives in a render target — is precisely the artefact the ADR warns about: the whole
            // trail would slide under every pan and read as a screen filter.
            // Twin: FoamBuffer.SampleUv / FoamBuffer.WorldCellOrigin (and its deliberately wrong sibling
            // CameraRelativeOrigin, which exists only so the tests can MEASURE the crawl).
            //
            // The value is POSTERIZED with an edge dither (the ADR's per-layer quantization condition,
            // default ON) on the SAME world-locked Bayer cell every other banded layer uses.
            // col.rgb / col.a dressing ONLY — never depth/clip()/_WaterLevel/the height read/the sim
            // (P1 integrity, rule 5). _WakeFoamStrength = 0 skips the whole block.
            float WakeFoamCoverage(float2 worldXY, float bayer, out float freshness)
            {
                freshness = 0.0;
                if (_WakeFoamStrength <= 0.001) return 0.0;
                // z = the window extent; <= 0 means the feature has never published one (no injector, or
                // the pass disabled), so there is no foam rather than a grey wash from an unbound read.
                if (_HHFoamBufferWorld.z <= 0.0) return 0.0;

                // WARNING .xy is the DRAW origin: the cell lattice PLUS the sub-cell drift the buffer
                // has banked but not yet spent as a whole-cell scroll (FoamBuffer.DrawOrigin). Reading
                // through the bare lattice is what made the whole band teleport a cell at a time
                // instead of drifting - the owner's "they shift in large groups".
                float2 uv = (worldXY - _HHFoamBufferWorld.xy) * _HHFoamBufferWorld.w;
                // Outside the window there is simply no record of this water. Clamping instead would
                // smear the edge texel across the open sea as a hard band.
                if (any(uv < 0.0) || any(uv > 1.0)) return 0.0;

                // ONE fetch, both channels: r = coverage (how much churn is on this water),
                // g = freshness (how recently it was churned - the age clock).
                float2 buffered = SAMPLE_TEXTURE2D(_HHFoamBufferTex, sampler_HHFoamBufferTex, uv).rg;
                float stored = buffered.x;
                freshness = buffered.y;

                // ---- LACE (2026-08-05, the owner's "it needs another pass") -------------------------
                // TEAR the stored coverage before it is thresholded. The buffer records how much churn
                // is on this patch of sea, which is the right quantity; a smooth accumulation
                // thresholded is a solid patch with a clean outline, which is the wrong shape — a
                // decal, not foam. Real foam is a torn mat with holes through it and a fringe drawn
                // out downwind.
                //
                // The tear comes from the SAME EvolvingField the whitecaps ride, on the same
                // wind-stretched basis (_FoamStreakStretch, reused — no second stretch knob), so it
                // BOILS and STREAKS with the rest of the sea's foam rather than reading as an
                // unrelated overlay. It is a MULTIPLY on the stored value, so it can only remove foam,
                // never invent it: a wake's dense heart survives, its fringe breaks up and dies
                // sooner — which is also what makes an old wake fade into lace instead of shrinking
                // as a disc. Its frequency rides BandFreq like every other band, so the tear coarsens
                // with the growing sea (ADR 0027 #4). _WakeFoamLace = 0 is a BIT-EXACT passthrough.
                // col.rgb dressing only, and it reads no sim state (rule 5).
                if (_WakeFoamLace > 0.001)
                {
                    float2 lwdir  = normalize(_WindDir.xy + float2(0, 1e-4));
                    float2 lwperp = float2(-lwdir.y, lwdir.x);
                    float2 lace   = float2(dot(worldXY, lwdir),
                                           dot(worldXY, lwperp) * max(_FoamStreakStretch, 1.0));
                    float torn = EvolvingField(lace, float2(0, 0),
                                               BandFreq(_NoiseScale) * 3.0 * _FoamBlobScale
                                               * max(_WakeFoamLaceScale, 1e-3),
                                               _FoamEvolveSpeed, _Time.y);
                    // The field centres near 0.5, so 2x lands it about 1: hearts keep their value,
                    // the low half of the field is what tears through. Dialed by the master.
                    stored *= lerp(1.0, saturate(torn * 2.0), saturate(_WakeFoamLace));
                }

                // A trail's fringe holds a lot of very faint foam; drawn in full it reads as a grey wash
                // over the sea rather than as churn. Threshold it, then soften the edge.
                float thr  = saturate(_WakeFoamThreshold);
                float soft = max(_WakeFoamSoftness, 1e-3);
                float cover = smoothstep(thr, min(thr + soft, 1.0), stored);

                // The layer's own quantization (default ON). The cells are already 4 screen px, so this
                // posterizes VALUE — solid tones of churn instead of an airbrushed gradient.
                if (_WakeFoamBands >= 2.0)
                    cover = BandValue01(cover, _WakeFoamBands, 0.5, bayer);

                return saturate(cover * _WakeFoamStrength);
            }

            // ---- THE WAKE FOAM'S AGE RAMP (owner ask 2026-08-27) -----------------------------------------
            // TWIN of WakeFoamAgeing.Knots (Core/Environment/WakeFoamAgeing.cs). LINE-FOR-LINE with the C#
            // apart from the language: change one, change BOTH in the same PR. WakeFoamAgeingShaderTests
            // scrapes these two functions out of this file and compares them numerically against the twin,
            // so a silent drift here goes red on CPU-only CI.
            //
            // The three-knot piecewise-linear age curve: 0 = the foam anchor (the white of the churn),
            // 0.5 = the shallow anchor, 1 = the mid anchor. The knots are re-ordered defensively so a
            // mis-tuned material can never invert the ramp or divide by zero.
            float WakeFoamKnots(float t01, float whiteHold, float blueReach, float deepReach)
            {
                float t    = saturate(t01);
                float hold = saturate(whiteHold);
                float blue = clamp(blueReach, hold + 1e-4, 1.0);
                float deep = clamp(deepReach, blue + 1e-4, 1.0 + 1e-3);

                if (t <= hold) return 0.0;
                if (t <  blue) return 0.5 * (t - hold) / (blue - hold);
                if (t <  deep) return 0.5 + 0.5 * (t - blue) / (deep - blue);
                return 1.0;
            }

            // TWIN of WakeFoamAgeing.Ramp3. The three-stop lookup across the sea's OWN palette anchors, so
            // every value the wake can take is a convex combination of colours the art direction already
            // owns (ADR 0015) - the wake cannot leave the palette even at a mis-tuned age.
            float3 WakeFoamRamp3(float age01, float3 foam, float3 shallow, float3 mid)
            {
                float t = saturate(age01);
                return t <= 0.5 ? lerp(foam, shallow, t * 2.0)
                                : lerp(shallow, mid, (t - 0.5) * 2.0);
            }

            // TWIN of WakeFoamAgeing.Age01FromFreshness. How OLD this patch of churn is, from the
            // buffer's FRESHNESS channel: 1 = churning right now, decaying toward 0 on its own
            // half-life. Floored off zero so a mis-tuned material divides by nothing.
            float WakeFoamAge01(float freshness, float freshFloor)
            {
                float fresh = max(freshFloor, 1e-4);
                return saturate(1.0 - freshness / fresh);
            }

            // The colour this patch of wake foam should be composed toward, given how recently it was
            // churned.
            //
            // THE AGE COMES FROM THE FRESHNESS CHANNEL, NOT THE COVERAGE. #665 used the coverage on the
            // reasoning that a decaying buffer's surviving coverage is its age. Measured, that cannot
            // work: coverage saturates at 1.0 within ~0.4 s of deposit, and WakeFoamCoverage above has
            // already thresholded and posterized it before anything can read it - so the proxy's input
            // could only ever take three values ({0, 0.425, 0.85} at the shipped material) and 72-81% of
            // a visible wake drew at age exactly 0, pure white, at every speed. That is precisely the
            // owner's "the big foam band stays white - never disperses", and it is not tunable: any
            // threshold over three values still yields one flat colour for the whole band. The buffer
            // now carries a freshness CLOCK the injection MAXes and time decays, which cannot clamp
            // (FoamBuffer.Freshness). WakeFoamAgeingMeasurementTests keeps the old compression red.
            //
            // _WakeFoamAgeStrength = 0 returns _FoamColor.rgb unchanged: the shipped single-white compose,
            // bit-exact.
            float3 WakeFoamAgedColor(float freshness)
            {
                float strength = saturate(_WakeFoamAgeStrength);
                if (strength <= 0.001) return _FoamColor.rgb;

                float age   = WakeFoamAge01(freshness, _WakeFoamFreshFloor);
                float t     = WakeFoamKnots(age, _WakeFoamWhiteHold, _WakeFoamBlueReach, _WakeFoamDeepReach);
                float3 ramp = WakeFoamRamp3(t, _PaletteFoam.rgb, _PaletteShallow.rgb, _PaletteMid.rgb);
                return lerp(_FoamColor.rgb, ramp, strength);
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
            float3 SkyReflection(float2 worldXY, float2 waveSlopeXY, float surf, float swellCrest, float t)
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

                // ---- THE MIRROR (owner ruling 2026-09-02) -------------------------------------------------
                // The band above is a stripe of a FIXED wavelength; a mirror has none. What a mirror has is a
                // SURFACE: a level facet returns the sky to the eye and a tilted one returns something else,
                // so the sheen breaks up precisely where the water tilts — and nowhere else.
                //
                // The tilt is the SHARED wave field's own analytic slope (ADR 0018: one field, one slope, one
                // computation — never a second sampler and never a re-derived phase), read at the drawn
                // frequency scale and already pixelized on the world PPU grid, so the break-up is pixel-art by
                // inheritance like every other layer.
                //
                // 1/(1 + k·|slope|): exactly _MirrorSheen on dead-flat water, falling smoothly and without a
                // knee as the surface tilts, never negative and never clipping. Chop dissolves it for free —
                // chop IS tilt — which is register row 13 ("the mirror fades with the wind, not with the sea
                // under it") answered by construction rather than by another ramp over _Chop.
                //
                // _MirrorSheen's default is not a taste: (0.5 + 0.5·sin)³ averages 0.3125 over a period, so a
                // level facet returning that much sky puts the SAME light on a calm sea as the stripe it
                // replaces. The change is the form, not the exposure — the owner asked for a mirror, not a
                // brighter sea, and a knob is there if he wants one.
                float tilt = length(waveSlopeXY) * max(_MirrorTiltScale, 0.0);
                float mirrorShare = 1.0 / (1.0 + tilt);          // 1 on dead-flat water, falling with tilt
                float tiltShare = 1.0 - mirrorShare;             // …and its exact complement: the two sum to 1
                band = lerp(band, max(_MirrorSheen, 0.0) * mirrorShare, saturate(_MirrorForm));
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
                    // ---- GLITTER NEEDS RIPPLES (the same ruling, the other half of the surface) -----------
                    // A glint is a facet turned by chance to send the SUN at the eye, which is the one thing a
                    // LEVEL facet cannot do — it is already sending the sky. So the streak rides tiltShare,
                    // the exact complement of the mirror above: the two are one surface accounted for once.
                    //
                    // This is not a tidy identity, it is what the first mirror plate demanded. With the 1.6 m
                    // stripe gone the glitter was left as the loudest thing on a dead calm — a dense field of
                    // fine vertical glints over the whole frame, which reads as RAIN, not as a mirror. It was
                    // always there; the rug was hiding it. A dead calm now returns the sky and almost no
                    // glitter, a light air breaks both ways, and the path lights up as soon as there is a
                    // ripple to light it. _MirrorForm 0 leaves the shipped all-over glitter untouched.
                    float glitterTilt = lerp(1.0, tiltShare, saturate(_MirrorForm));
                    reflectionRGB += sky * streak * _ReflectionSunStreak * sunUp * glitterTilt;
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

                // ---- the LIVING MOON's state, read FIRST (the clouds' night share below is moonlit) ---------
                // moonBright: live brightness (illuminated-fraction × presence) — fall back to 1 (full) if unset.
                // moonPresence: 0..1 above-horizon (fades the moon at the horizons) — fall back to 1 if unset.
                bool moonStateOn = (abs(_MoonPhaseState.x) + abs(_MoonPhaseState.y)
                                  + _MoonPhaseState.z + _MoonPhaseState.w) > 1e-4;
                float moonBright   = moonStateOn ? _MoonPhaseState.z : 1.0;
                float moonPresence = moonStateOn ? _MoonPhaseState.w : 1.0;
                float terminator   = moonStateOn ? _MoonPhaseState.y : -1.0;   // -1 = full disc by default

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
                    // pixel-identical — night = 0 routes ALL of it here). The night share joins the compensated
                    // post-grade add — but MOONLIT and FAINT (owner playtest 2026-07-23, the "whole sea becomes
                    // white" defect): the compensated bucket cancels the day/night multiply EXACTLY, so a
                    // full-strength night share painted daylight-strength cloud bands over a sea the overlay had
                    // dimmed to a few percent — a milky veil that smothered every water detail from dusk on
                    // (rendered-frame evidence in the fix PR). Clouds are a REFLECTION of the sky, not a light
                    // source: at night they read only by MOONLIGHT — gated by the moon's presence × live
                    // brightness (the same _MoonPhaseState the disc reads; the no-MoonCycle fallback of 1 keeps
                    // a bare-scene preview sane) and scaled to _CloudMoonlitVis (default 0.35: faint moonlit
                    // bands under a full moon, gone on a moonless night). _CloudMoonlitVis = 1 restores the
                    // pre-fix full-strength night share EXACTLY (the legacy passthrough). The moon disc /
                    // glitter / stars / beam / rain rings are genuine light content and keep their bucket
                    // untouched. C# twin: WaterReflection.MoonlitCloudVisibility — change both in lockstep.
                    dayRGB += cloudTerm * (1.0 - night);
                    float moonlitVis = saturate(moonPresence * moonBright)
                                     * saturate(_CloudMoonlitVis);
                    nightRGB += cloudTerm * night * moonlitVis;
                }

                // ---- (2) the LIVING MOON: a reflected disc (phase-shaped) + a vertical GLITTER PATH (night) --
                // The moon RISES/ARCS/SETS across the night (its direction comes from MoonCycle via _MoonDir) and
                // changes shape over the lunar month (the crescent/gibbous TERMINATOR comes from _MoonPhaseState),
                // dimming to a thin crescent at new moon. When no MoonCycle runs, fall back to a fixed full moon
                // opposite the sun so a bare scene still shows one. (The moon STATE — moonStateOn / moonBright /
                // moonPresence / terminator — is read at the top of this function now, because the clouds' night
                // share above is moonlit-gated by it.)
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
            // ---- WAVE RELIEF: the beam lights the SEA'S SHAPE (owner mandate, 2026-08-28) -------------------
            // *"it should highlight the water at crests and be shadowed at the valleys of waves unless the
            // proper light angle exposes them."* The cone weight above is radial x angular and blind to the
            // sea under it -- the owner's "one uniform shape with a gentle gradient". This scales it by the
            // wave field's OWN relief, using the SAME analytic waveSlope the swell FACE SHADING rides (ADR
            // 0018: one field, one slope, one computation -- never a re-derivation of phase).
            //
            // A LAMP IS NOT THE SUN. The sun is a direction at infinity, so the face shading uses one world
            // vector for the whole sea; a lamp is a POINT at a HEIGHT, so the incidence direction differs at
            // every pixel -- steep under the lamp, grazing at the far end of the throw. Everything the owner
            // asked for is that one fact, with no special cases: crest faces turned toward the lamp gain,
            // trough walls and back slopes lose, a LOW lamp separates them hard and reaches INTO troughs
            // while a HIGH one flattens the sea toward a disc, and the far end of a beam rakes more than its
            // near end because the elevation to a fixed-height lamp falls off with distance.
            //
            // EXACTLY 1 ON FLAT WATER, BY CONSTRUCTION (the load-bearing property): N.L is divided by the
            // dot product a FLAT facet would have had at the SAME pixel (lz, floored ONCE and reused in both
            // places), so zero slope cancels to exactly 1 for ANY lamp geometry. A searchlight sweeping a
            // dead-calm sea therefore leaves the §11 mirror exactly as it is today. Twin: LightMath.
            // WaveReliefFactor (guarded against drift by a source assertion on this file's text).
            //
            //   slopeXY -- the shared field's analytic dh/dx, dh/dy at this pixel.
            //   toLamp  -- pixel -> lamp, UNNORMALIZED, metres; .z = lamp height above THIS pixel's surface.
            //   RETURNS the relief factor: 1 = as flat water, >1 = tilted into the beam, 0 = turned away.
            float BeamRelief(float2 slopeXY, float3 toLamp)
            {
                float lenSq = dot(toLamp, toLamp);
                if (lenSq < 1e-10) return 1.0;              // at the lamp itself the direction is undefined
                float3 L = toLamp * rsqrt(lenSq);

                // Floor the elevation ONCE and reuse the SAME lz below, so the zero-slope cancellation stays
                // EXACT even for a lamp under the floor (a grazing beam). Bounds the runaway contrast of a
                // light sinking to the water plane.
                float lz = max(L.z, max(_BeamReliefMinElevation, 1e-4));

                // N ∝ (-sx, -sy, 1)  =>  N.L = (lz - slope.L_xy) / |N|, then / lz (the flat-water reference).
                float sDotL = dot(slopeXY, L.xy);
                float invN  = rsqrt(1.0 + dot(slopeXY, slopeXY));
                return clamp((lz - sDotL) * invN / lz, 0.0, max(_BeamReliefMaxGain, 1.0));
            }

            float BoatLightWeight(float4 lightPos, float4 lightDir, float4 lightParams, float4 lightParams2,
                                  float2 worldXY, float2 waveSlopeXY, float waveHeightM)
            {
                float intensity = lightParams.x;
                if (intensity <= 0.001)
                    return 0.0;                             // light off / not lighting water / no boat -> nothing

                float range    = max(lightParams.y, 1e-4);
                float cosHalf  = lightParams.z;
                float cosInner = max(lightParams.w, cosHalf + 1e-4);
                float edgeSoft = lightParams2.x;

                // pixel-snap the world position so the lit pool reads as pixel art like every other layer (§3).
                float2 p = Pixelize(worldXY);
                float2 toPixel = p - lightPos.xy;
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
                float2 bdir = normalize(lightDir.xy + float2(0, 1e-4));
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
                float threshold = lightParams2.y;
                float softness  = lightParams2.z;
                float fallback  = lightParams2.w;
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
                // ---- WAVE RELIEF: the beam lights the SEA'S SHAPE, not a flat disc ------------------------
                // Scale the cone weight by how this pixel's wave facet is turned relative to THIS lamp (see
                // BeamRelief). TWO independent EXACT passthroughs guard the shipped look: a lamp with no
                // published height (.z == 0 — a legacy publisher, a bare material) and a strength of 0 both
                // leave the weight bit-identical to the flat cone above. Twin: LightMath.WaveReliefFactor +
                // LightMath.ApplyReliefStrength.
                float relief = 1.0;
                float reliefStrength = max(_BeamReliefStrength, 0.0);
                if (lightPos.z > 1e-4 && reliefStrength > 0.001)
                {
                    // toLamp.z is the lamp's height above THIS pixel's OWN water surface, so a crest riding up
                    // toward the lamp is genuinely closer to it than the trough beside it.
                    float3 toLamp = float3(lightPos.xy - p, lightPos.z - waveHeightM);
                    relief = 1.0 + (BeamRelief(waveSlopeXY, toLamp) - 1.0) * reliefStrength;
                }

                return max(0.0, intensity * shape * gate * relief * max(_BoatLightGain, 0.0));
            }

            // The TOTAL water-light cone weight at this pixel: every live light the bridge published, summed.
            // Count 0 => no bridge is running (a bare art scene, a legacy scene, an EditMode harness) => fall
            // back to the ONE _BoatLight* singleton, which is EXACTLY the ADR 0016 path this file shipped with.
            // The singleton is deliberately NOT added on top of the array — it is the same primary lamp, kept
            // published for SpriteLitDecor.hlsl's sake (the other lit path), so summing both would double it.
            // ⚠️ It also returns the COLOUR-WEIGHTED sum, and that is not a convenience: the WEIGHT comes
            // from the array while the singleton _BoatLightColor is published only by BoatSpotlight, so a
            // consumer that took the weight from here and the colour from the singleton would be reading two
            // different publishers. In a scene driven purely by the bridge (an EditMode harness, the plate
            // sweep) the singleton is simply UNSET — the new lit-water term measured exactly zero that way
            // before this out-parameter existed — and in a world with two lamps of different colours it
            // would paint both of them in the first lamp's colour. Summed here, per light, once.
            float BoatLightTerm(float2 worldXY, float2 waveSlopeXY, float waveHeightM, out float3 litColor)
            {
                int n = (int)(_WaterLightCount + 0.5);
                if (n <= 0)
                {
                    float one = BoatLightWeight(_BoatLightPos, _BoatLightDir, _BoatLightParams, _BoatLightParams2,
                                                worldXY, waveSlopeXY, waveHeightM);
                    litColor = _BoatLightColor.rgb * one;
                    return one;
                }

                float total = 0.0;
                litColor = float3(0.0, 0.0, 0.0);
                [unroll]
                for (int i = 0; i < WATER_MAX_LIGHTS; i++)      // FIXED bound; the count masks inside
                {
                    if (i < n)
                    {
                        float w = BoatLightWeight(_WaterLightPos[i], _WaterLightDir[i], _WaterLightParams[i],
                                                  _WaterLightParams2[i], worldXY, waveSlopeXY, waveHeightM);
                        total += w;
                        litColor += _WaterLightColor[i].rgb * w;
                    }
                }
                return total;
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
            // ON-SCREEN water lands at ~paletteFloor in daylight, yet dusk and night ride DOWN with the scene.
            // The overlay multiplies the whole frame by _DayNightTint AFTER the water renders (ADR 0013), so we
            // floor the water's PRE-overlay value at min(1, paletteFloor / max(dayNightLuma, KNEE)):
            //   * dnLuma >= the knee (daylight + overcast): == paletteFloor / dnLuma — the exact ADR 0015
            //     pre-compensation; on-screen the floor lands at paletteFloor. Never muddy.
            //   * dnLuma < the knee (dusk .. night): the divisor HOLDS at the knee, so the pre-overlay floor
            //     stops growing and the ON-SCREEN floor rides down with the scene (× dnLuma/knee).
            // WHY THE KNEE (owner playtest 2026-07-23, "the whole sea becomes white"): the un-kneed quotient
            // saturated toward 1 through dusk — at a dusk tint (~0.17..0.34 luma) it clamped MOST of the sea's
            // pre-overlay values to one high floor, so the on-screen sea held DAYLIGHT-floor brightness while
            // the whole scene dimmed around it AND lost its value structure to the clamp — a uniform flat
            // bright sheet (rendered-frame evidence in the fix PR: the dusk-storm frame was 99.7% flat at the
            // floor). With the knee the clamp level stays at the BOTTOM of the value distribution, so dusk
            // keeps its crest/trough/foam structure and genuinely darkens. _PaletteFloorKnee = 0 restores the
            // pre-fix saturating curve EXACTLY (max(dn, 0) == dn — the legacy passthrough contract).
            // nightFloor (on-screen) is UNTOUCHED: its whole job is to survive deep night, so it keeps the
            // saturating divide. Mirrors WaterPaletteGrade.ValueFloorDayNight — change both in lockstep.
            float PaletteValueFloorDayNight(float paletteFloor, float dayNightLuma, float nightFloor)
            {
                float dn = max(dayNightLuma, 1e-3);
                float kneeDn   = max(dn, saturate(_PaletteFloorKnee));
                float dayPre   = min(1.0, max(paletteFloor, 0.0) / kneeDn);
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
            float3 DriftLines(float2 worldXY, float dt, float depth, float t, float waveFreqScale,
                              float waveFetchEnv)
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

                // (2) the aniso basis. TODAY: the raw CURRENT (_FlowDir) — §18.1's deliberate correction
                // against using _WindDir. THE UPGRADE: dial toward the SHARED FoamDriftDir(), the same
                // wind/current blend (plus shoreward bias) that the foam and whitecaps on this very
                // surface already drift along. §18.1 was right that the wind is not the answer, and
                // incomplete in that neither is the current alone: a real windrow follows the blend, and
                // until now the lines and the foam they are MADE of read two different directions.
                // Blend 0 returns the normalized current EXACTLY (twin: WaterDriftLines.DriftDirection).
                float2 flowdir = normalize(_FlowDir.xy + float2(1e-4, 0));  // safe axis on a zero flow
                if (_DriftLineFoamDrift > 0.001)
                {
                    float2 sharedDrift = FoamDriftDir(worldXY, depth);
                    flowdir = normalize(lerp(flowdir, sharedDrift, saturate(_DriftLineFoamDrift))
                                        + float2(1e-4, 0));
                }
                float2 flowperp = float2(-flowdir.y, flowdir.x);
                // (2b) THIS LAYER'S GRID. Divisor 1 = the shipped grid bit-for-bit. Note what the shipped
                // numbers already were: the pixelize runs on the SCALED coord, so the world cell is
                // 1/(ppu x _DriftLineScale) = 10.4 cm at PPU 32 and scale 0.3 — already 3.3x coarser than
                // the caustics' 3.1 cm raw-world cell. That hierarchy partly existed by accident; the
                // divisor makes it a choice (twin: WaterDriftLines.WorldCellMetres).
                float2 pp = PixelizeGrid(worldXY * _DriftLineScale, _DriftLineGrid);

                // WANDER: a slow low-freq noise nudge along the flow so the lanes drift/bend, not a ruler grid.
                float wander = (ValueNoise(PixelizeGrid(worldXY * _DriftLineScale * 0.35,
                                                        _DriftLineGrid)) - 0.5) * 2.0;

                // (3) advance ALONG the flow over time; stretch the along-axis so lanes read long + thin.
                float stretch = max(_DriftLineStretch, 1.0);
                float along = (dot(pp, flowdir) + wander * 0.6) / stretch
                              - t * _Flow * _DriftLineSpeed;         // downstream drift (with the current)
                float across = dot(pp, flowperp);
                float2 lineUV = float2(along, across);

                // (4) THIN RIDGED-NOISE LANES across the flow (the pow(saturate(1-|g1-g2|k)) streak idiom).
                float g1 = ValueNoise(PixelizeGrid(lineUV, _DriftLineGrid));
                float g2 = ValueNoise(PixelizeGrid(lineUV * 1.7 + 3.3, _DriftLineGrid));
                float lanes = pow(saturate(1.0 - abs(g1 - g2) * 2.4), 3.0);   // bright thin veins => streaks

                // (4b) SCUM GATHERS WHERE THE SURFACE CONVERGES. A drift line is not a long texture — it
                // is floating material COLLECTED on a convergence line, which is why real ones sit in
                // bands with clean water between them. Reuse ADR 0027 num 3 ConvergenceGate off the SAME
                // WaveFieldSample the foam's convergence term reads (four taps, central-differenced into
                // the second derivatives) so the lines and the convergence FOAM agree about where the
                // surface folds, instead of holding two opinions about one piece of physics. Weight 0
                // returns exactly 1 = today (twin: WaterDriftLines.ConvergenceWeight); the whole branch
                // is unreachable at the shipped default.
                if (_DriftLineConvergence > 0.001)
                {
                    float de = max(_FoamConvergenceStep, 1e-3);
                    float dhpx, dhmx, dhpy, dhmy, dcrF, dprF;
                    float2 dspx, dsmx, dspy, dsmy;
                    WaveFieldSample(Pixelize(worldXY + float2(de, 0.0)), waveFreqScale, waveFetchEnv,
                                    dhpx, dspx, dcrF, dprF);
                    WaveFieldSample(Pixelize(worldXY - float2(de, 0.0)), waveFreqScale, waveFetchEnv,
                                    dhmx, dsmx, dcrF, dprF);
                    WaveFieldSample(Pixelize(worldXY + float2(0.0, de)), waveFreqScale, waveFetchEnv,
                                    dhpy, dspy, dcrF, dprF);
                    WaveFieldSample(Pixelize(worldXY - float2(0.0, de)), waveFreqScale, waveFetchEnv,
                                    dhmy, dsmy, dcrF, dprF);
                    float dHxx = (dspx.x - dsmx.x) / (2.0 * de);
                    float dHyy = (dspy.y - dsmy.y) / (2.0 * de);
                    float dHxy = ((dspx.y - dsmx.y) + (dspy.x - dsmy.x)) / (4.0 * de);
                    float conv = ConvergenceGate(dHxx, dHyy, dHxy, _FoamConvergencePinch);
                    lanes *= lerp(1.0, conv, saturate(_DriftLineConvergence));
                }

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

                // The WIND-FETCH envelope (ADR 0027 #1) — marched ONCE for this pixel and handed to every
                // WaveFieldSample below, including the finite-difference taps a few centimetres away (see the
                // cost note on WaveFieldSample). Exactly 1, and not one texture tap, while the model is off.
                float waveFetchEnv = FetchEnvelope01(worldXY);

                // ---- layer 2 surface (computed first; warps the coords every other layer reads) -------------
                // ADR 0027 #9: the octaves' shoal depth — ONE guarded, STILL (unwarped) seabed read. The
                // surf warp below feeds the depth read, so the octaves cannot consume the fragment's own
                // depth (circular); the shoal drift keys the still read instead. Free at the shipped
                // defaults (the branch never runs; 1e5 = deep = every shift term is exactly 0). READ-only
                // use of the height map — the clip()/waterline read below is untouched (rule 5).
                float dispShoalDepth = 1e5;
                if (_DispersionScale > 0.001 && _DispersionShoalBunch > 0.001)
                    dispShoalDepth = max(_WaterLevel - SeabedElevation(worldXY), 0.0);
                float surf = SurfaceNoise(worldXY, t, dispShoalDepth);   // 0..1
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
                // ⚠️ ADR 0027 #4 deliberately does NOT scale THIS _NoiseScale read: the warp feeds
                // SeabedElevation -> depth -> clip() — the gameplay waterline. Changing its frequency with
                // sea state would move where the waterline shimmers, and Tier A must never touch the
                // height read/clip (the consumer audit's one excluded read; see the PR).
                float2 warp = float2(swell, ValueNoise(Pixelize(worldXY * _NoiseScale + 7.3)) - 0.5)
                              * _Chop * 0.5;

                // ---- layer 1 depth gradient -------------------------------------------------------------------
                float elevation = SeabedElevation(worldXY + warp);
                float depth = _WaterLevel - elevation;             // metres; <= 0 means dry/exposed

                // Dry ground: the shader hands off to the terrain tiles below (draw nothing).
                //
                // COARSE PRE-CLIP. The swash below may advance the DRAWN wet edge onto ground the real
                // depth calls dry, by at most _SwashMaxEdgeShift metres of level — so everything beyond
                // that is unconditionally dry and can be thrown away here, before any shore work. The
                // EXACT edge is clipped once the swash is known (a few lines down). Splitting it this way
                // keeps the cheap rejection for the whole dry region and pays for the shore-gradient taps
                // only inside the narrow band the swash can actually reach.
                //
                // NOTE the swash reaches ONLY this fragment's drawn edge. The gameplay waterline —
                // ITidalTerrain / the walkability sim / _WaterLevel itself — is untouched, and the shift
                // is capped well inside the standing "wade ~0.5 m" tolerance (P1 integrity, rule 5).
                // ADR 0040 rev 3: the bore's run-up is the second thing allowed to move the drawn edge, and
                // it shares the swash's hard cap - folded into the reach here, so the pre-clip below keeps
                // its shape and the bound stays structural. _SurfRunUpStrength 0 = the swash's reach alone.
                float swashEdgeReach = max(saturate(_SwashMaxEdgeShift) * step(1e-6, saturate(_SwashEdgeShift)),
                                           saturate(_SwashMaxEdgeShift) * saturate(_SurfRunUpStrength));
                clip(depth + swashEdgeReach + 1e-4);

                // ---- ORGANIC SHORE FRINGE (LOOK-ONLY prototype; cosmetic depthC — ADR 0012) ------------------
                // A revertible, defaults-off wiggle so the visible water-meets-land edge reads like a natural
                // coast even on glassy calm. `depthC` is a LOCAL cosmetic twin of `depth`, perturbed by
                // pixel-grid-quantized noise ONLY inside a thin band around the waterline. It feeds the VISIBLE
                // shore read (see-through alpha fringe + foam/shallow band) BELOW — never clip()/dt/the deep
                // tint/the caustic gate/_WaterLevel. At _ShoreNoise = 0 (the shipped property default) depthC ==
                // depth byte-for-byte, so every other material is unchanged. ALWAYS-ON (not chop-gated like the
                // `warp` above) so it reads on dead-calm glass — the whole point of the prototype.
                float shoreEdge = 1.0 - smoothstep(0.0, max(_ShoreNoiseBand, 1e-3), abs(depth));  // 1 at edge -> 0 outside the band
                // Pixel-grid-quantized value noise (organic SHAPE at the pixel scale, not sub-pixel smoothness —
                // the pixelization principle, ADR 0010 (2)); reuse the existing Pixelize + ValueNoise helpers.
                float shoreN = ValueNoise(Pixelize(worldXY) * max(_ShoreNoiseScale, 1e-3)) - 0.5;  // -0.5..0.5
                // ---- SLOPE-AWARE shore cosmetics (owner playtest 2026-07-23: "shoreline looks a bit
                // swirly"). The fringe wiggle and the beach swash below offset a cosmetic DEPTH, so their
                // VISIBLE contour excursion is amplitude ÷ the local beach slope — slope-blind constants
                // tuned on a steep edge painted METRES-wide swinging worm tongues on a gently painted bar
                // (excursion 5× the authored value at a 0.2 m/m beach). Scaling the depth offsets by the
                // LOCAL painted slope (saturated at the 1 m/m authoring reference) makes the authored
                // amplitudes read as CONTOUR metres on ANY coast: _SwashAmplitude / _ShoreNoise now mean
                // "metres of visible wet-edge excursion", steep shores keep today's look. Cosmetic only —
                // never depth/clip()/_WaterLevel/the sim (P1, rule 5); gated to the shore bands so the
                // gradient taps don't run on open water.
                float shoreCosmeticReach = max(max(_ShoreNoiseBand, 1e-3),
                                               max(_FoamWidth, 1e-3) * 2.0 + abs(_SwashAmplitude));
                float shoreSlope = (depth < shoreCosmeticReach)
                                       ? saturate(SeabedSlopeMag(worldXY)) : 1.0;
                // ⚠️ The FRINGE NOISE keeps the RAW slope, deliberately. Flooring it here would put the
                // 2026-07-23 "swirly shoreline" defect straight back on the flats: that defect was chaotic
                // noise painting metres-wide worm tongues, and on a near-flat bar a floored slope is exactly
                // the licence to do it again. The lines are killed by DITHERING the band edge (below), which
                // addresses the actual cause — texture quantization — instead of drowning it in noise.
                float depthC = depth + shoreN * _ShoreNoise * shoreSlope * shoreEdge;   // cosmetic; == depth when _ShoreNoise = 0

                // The SWASH, by contrast, does get a floored slope (owner judge pass 2026-08-01). The slope
                // scaling is right in principle — it makes the authored amplitude read as CONTOUR metres on
                // any coast — but it assumed a slope that is always measurably non-zero. On the sandbar flats
                // the 8-bit height map is literally FLAT across whole texel runs, so the central difference
                // returns 0 and the swash was multiplied to nothing precisely where the owner was standing
                // when he said he could not see it. A flat beach should get a LONG, LOW run-up, not a dead
                // one. Unlike the fringe noise this is safe to floor because the swash is a COHERENT
                // travelling sine, not noise: it reads as a wash, never as worm tongues. The floor sits below
                // the 0.18 m/m reference coast the swirl guard renders, so nothing that guard measures moves.
                float swashSlope = max(shoreSlope, saturate(_ShoreSlopeFloor));

                // ==== THE SURF, RESOLVED BEFORE THE EDGE (ADR 0040 rev 3) ==================================
                // Moved up from after the sky layers so the BORE's run-up (below) can move the drawn wet
                // edge's clip and the fringe's foam depth. Its outputs are read by the fringe and the surf
                // compose further down exactly as before; nothing it reads is computed later than here.
                // ---- BREAKING WAVES (ADR 0040), resolved BEFORE the fringe so the fringe can yield ----
                // Owner ruling, 2026-08-28: "surf supersedes the fringe" where the sea is actually breaking.
                //
                // The shore-foam fringe is a band drawn at a fixed width off the waterline — it has always
                // been the STAND-IN for whitewater, drawn geometrically because nothing knew where waves
                // really break. Now something does. So where the computed whitewater is alive it takes the
                // fringe's place, and everywhere it is not (calm water, sheltered water, a coast too deep
                // to break on) the fringe is untouched.
                //
                // WARNING: it yields to `alive`, NOT to `breaking`. Breaking is 1 all the way up the beach,
                // so superseding on that would delete the foam at the water's edge — where a bore that has
                // already died becomes swash, and where there really is white. Yielding to the whitewater's
                // ENERGY hands the fringe over exactly where the physical foam exists and hands it back
                // where the bore has spent itself. The white is RELOCATED to where physics puts it, not
                // removed.
                float surfBreaking = 0.0, surfAlive = 0.0, surfCover = 0.0;
                float surfXi = 0.0, surfPlunge = 0.0, surfLip = 0.0, surfBarrel = 0.0;
                float ageM = 0.0;
                float2 surfDir = float2(0.0, 0.0);
                // The BORE (ADR 0040 rev 3): its pulse here (1 = a crest's front is passing), the wash's
                // reach in metres of level, and the breaking face's slope for the light. All three stay at
                // their zeros — no pulse read, no reach, no face — unless their look dials are up.
                float surfBore = 1.0, surfSheet = 1.0, surfRunUpM = 0.0, anatomyAge = 0.0;
                float2 surfFrontSlope = float2(0.0, 0.0);
                // ADR 0040 rev 3: with the run-up dial up, the BEACH BAND - the metres of level the wash can
                // reach above the still-water line - is evaluated too, or the drawn edge could never move
                // onto the sand. A beach pixel reads the surf at its own WATERLINE point (projected down the
                // floored shore slope, the swash's own metres-of-contour idiom), so it inherits that bore's
                // age, phase and reach instead of marching from dry ground; the clip below then decides
                // whether the wash has actually reached it. At the dial's 0 the reach is 0 and the gate is
                // the previous 'depth > 0' exactly. The C# probe (BreakerMath.SurfAt) keeps its dry-ground
                // refusal: it answers for hulls, and a hull is never on the beach.
                float surfBeachReach = saturate(_SurfRunUpStrength) * saturate(_SwashMaxEdgeShift);
                float2 surfEvalXY = worldXY;
                float surfEvalDepth = depth;
                if (_SurfStrength > 0.001 && _BreakerOuter.w > 0.5 && depth > -surfBeachReach)
                {
                    if (depth <= 0.0)
                    {
                        surfEvalXY = worldXY + ShoreDir(worldXY) * (depth / max(swashSlope, 1e-3));
                        surfEvalDepth = 0.02;                       // BreakerMath.MinDepthMeters
                    }
                    // A shoaling wave REFRACTS toward shore-normal, which is why surf runs in parallel to
                    // the depth contours however the swell was heading offshore. The seabed gradient IS
                    // that direction and this shader already derives it per pixel. Refraction is not
                    // otherwise modelled; this is where it enters, and BreakerMath.MetersSinceBreakAlong
                    // takes the heading as a parameter so the C# reference can be handed the same one.
                    surfDir = ShoreDir(surfEvalXY);
                    surfBreaking = SurfBreaking01(surfEvalDepth, waveFetchEnv);
                    if (surfBreaking > 0.002 && dot(surfDir, surfDir) > 1e-6)
                    {
                        float travelS = 0.0;
                        SurfMarch(surfEvalXY, surfDir, waveFetchEnv, ageM, travelS);
                        // The whitewater: the shipped local-speed law, blending toward the travel-time law
                        // as the run-up dial comes up (a wash that could never reach the sand is not a
                        // run-up; at 0 the sheet is today's exactly - see SurfWhitewaterByTravel01).
                        float aliveLocal = SurfWhitewater01(ageM, surfEvalDepth);
                        float aliveTravel = SurfWhitewaterByTravel01(travelS);
                        surfAlive = lerp(aliveLocal, aliveTravel, saturate(_SurfRunUpStrength));
                        anatomyAge = ageM;   // the steady anatomy stands a fixed distance past the break line

                        // ---- THE BORE'S CLOCK (ADR 0040 rev 3) --------------------------------------
                        // The break line this bore was born on is the marched distance back upwave; the
                        // field's PUBLISHED phase there, read forward at minus the travel time, is the
                        // bore's phase here. One pulse per wave period, advancing at sqrt(g*d): a crest
                        // ARRIVES, peaks and passes. Evaluated at the DRAWN scale so it leaves with the
                        // crest the eye sees. Cost when the beat dial is 0: the phase read (a dot and a
                        // wrap) and nothing else — the birth sample is skipped below.
                        // The surf-similarity number, read ONCE here (the anatomy below used to read it):
                        // the bore's run-up and the plunging weight both key on it.
                        surfXi = SurfIribarren(surfEvalXY);
                        float boreBeat = saturate(_SurfBeatStrength);
                        float boreEdge = saturate(_SurfRunUpStrength);
                        float boreFace = max(_SurfFrontSlope, 0.0);
                        if (boreBeat > 0.001 || boreEdge > 0.001 || boreFace > 0.001)
                        {
                            float surfFreqScale = max(_OceanSwellScale, 1e-4) / 0.025;   // WAVE_LEGACY_SCALE_REF
                            float2 breakLinePt = surfEvalXY - surfDir * ageM;
                            float omega = SurfBoreOmega();
                            float periodS = omega > 1e-6 ? 6.2831853 / omega : 0.0;
                            float borePhase = SurfBorePhaseDeg(breakLinePt, travelS, surfFreqScale);
                            float birth = SurfBoreBirth01(breakLinePt, travelS, borePhase, periodS,
                                                          surfFreqScale, waveFetchEnv);
                            surfBore = SurfBorePulse01(borePhase, _BreakerBore.x) * birth;
                            surfSheet = SurfBoreSheet01(borePhase, periodS) * birth;

                            // The wash's reach: Hunt's law on the height the bore was BORN with (the
                            // depth-limited height at the break line, not the local one, which is 0 at the
                            // very edge where the reach matters), what is left of it, and its pulse.
                            float breakDepth = BreakerDepthAtEnv(_BreakerDepths.xyz, _BreakerDepths.w, waveFetchEnv);
                            float standingAtBreak = max(_BreakerAnatomy.y, 0.0) * max(breakDepth, 0.0);
                            surfRunUpM = SurfRunUpMeters(standingAtBreak, surfAlive, surfXi, surfBore) * boreEdge;

                            // The breaking FACE: the bore's own height profile, differentiated along the
                            // travel direction, as a slope the relief light and the sun's shade can see.
                            float boreSpeed = sqrt(max(_BreakerParams.z, 0.0) * max(surfEvalDepth, 0.02));
                            // Gated by the break gate like everything else in the surf: seaward of the
                            // break line the gate is barely open and there is no front to shade.
                            surfFrontSlope = surfDir * SurfFrontSlope(borePhase, _BreakerBore.x,
                                                                      standingAtBreak * birth * surfAlive * surfBreaking * boreFace,
                                                                      boreSpeed, omega);

                            // With the beat up the anatomy TRAVELS: the lip is thrown AHEAD of the front,
                            // the barrel hollows under it and the pocket peels at the curl, all measured in
                            // metres from the front (its seconds times its speed) rather than from the
                            // break line - so the event arrives with the crest and leaves with it.
                            float aheadOfFrontM = -SurfSignedSecondsFromCrest(borePhase, periodS) * boreSpeed;
                            anatomyAge = lerp(ageM, aheadOfFrontM, boreBeat);
                        }
                        // The BEAT: where the dial is up, the sheet is born at the front and ages behind
                        // it, and the anatomy below is an EVENT at the front rather than a standing band.
                        // 0 = the steady state the surf shipped with, exactly.
                        // Two-fold: the SHEET is born at the front and ages behind it; the anatomy is an
                        // EVENT at the front (the pulse). Both blend from EXACTLY 1, the steady state.
                        float beat = lerp(1.0, surfSheet, boreBeat);
                        float boreEvent = lerp(1.0, surfBore, boreBeat);

                        // The break line is denser than the bore trailing off it. This reads the SAME
                        // marched age — it is not a second clock — so retuning the decay moves both.
                        float crest = 1.0 - smoothstep(0.0, max(_SurfCrestWidth, 0.05), ageM);
                        float density = surfAlive * lerp(1.0, max(_SurfCrestBoost, 1.0), crest) * beat;

                        // Break the band up with the SAME evolving-churn language the fringe and the
                        // whitecaps use, drifting shoreward with the bore rather than scrolling against it,
                        // so the surf reads as this sea and not as a decal laid over it.
                        float2 surfDrift = surfDir * (_Flow * t * 0.5);
                        float churn = EvolvingField(worldXY, surfDrift,
                                                    max(_SurfNoiseScale, 1e-3), _SurfEvolveSpeed, t);

                        // Metaball soft-threshold, lifted by how live the water is here: at the break the
                        // field clears the threshold almost everywhere (solid white), and as the bore ages
                        // only the field's peaks still clear it, so the sheet breaks into drifting patches
                        // and dies. That IS the dispersal, and it is the age doing it.
                        float field = saturate(churn + density * 0.75);
                        float thr = saturate(_SurfThreshold);
                        float soft = max(_SurfThresholdSoft, 1e-3);
                        surfCover = smoothstep(thr - soft, thr + soft, field) * saturate(surfBreaking * density);

                        // ---- PLUNGING ANATOMY: the LIP, the BARREL, the POCKET ---------------------
                        // Owner's three words, and each one is a read of quantities already computed
                        // rather than a fourth model. All of them multiply by surfPlunge, which the
                        // SEABED sets: on a gentle shoal it is 0 and this whole block is inert, which is
                        // how "barrels only where the bathymetry earns them" is enforced rather than
                        // promised.
                        //
                        // The stages are keyed to ageM — metres past the break line — which is the
                        // marched geometry, not a reconstructed phase. atan2(height, slope*d/k) is exact
                        // for one pure sine and is not a phase at all when fed the real four-train
                        // sharpened field, so nothing here reconstructs one.
                        // With the beat up, the lip is thrown AT the crest's arrival and travels with the
                        // bore; the barrel's hollow shades under it and the pocket peels beside it, then all
                        // of it collapses into the bore's back. Only where xi earns it — unchanged.
                        surfPlunge = SurfPlunging01(surfXi) * saturate(_SurfPlungeStrength) * boreEvent;
                        if (surfPlunge > 0.002)
                        {
                            // A broken wave stands at gamma*d — it is only as tall as the water it is
                            // running over, which is why a big day and a small day throw the same lip in
                            // the last few metres.
                            float standing = max(_BreakerAnatomy.y, 0.0) * surfEvalDepth;
                            float throwM = standing * surfPlunge * max(_SurfLipThrow, 0.0);

                            // THE LIP: the crest outruns its base and lands AHEAD of it. A band centred
                            // throwM metres past the break line is that, stated directly.
                            surfLip = surfBreaking * surfPlunge
                                    * (1.0 - smoothstep(0.0, max(_SurfLipWidth, 0.05), abs(anatomyAge - throwM)));

                            // THE BARREL: the hollow the thrown lip encloses, between the break line and
                            // where the lip lands. Dark because it is in the lip's shadow — that shadow
                            // IS what makes a tube read as a tube.
                            surfBarrel = surfBreaking * surfPlunge
                                       * (1.0 - smoothstep(throwM * 0.75, throwM * 1.15, anatomyAge))
                                       * smoothstep(0.0, max(throwM * 0.35, 0.05), anatomyAge);

                            // THE POCKET: the powerful peeling zone beside the curl — breaking hard and
                            // broken JUST now. It reads the SAME marched age the lip and barrel do.
                            float pocket = surfPlunge * surfBreaking
                                         * (1.0 - smoothstep(0.0, max(_SurfPocketWidth, 0.1), anatomyAge));
                            surfCover = saturate(surfCover * lerp(1.0, max(_SurfPocketBoost, 1.0), pocket));
                        }
                    }
                }
                // How much of the fringe the physical whitewater has taken over here.
                float surfSupersede = saturate(surfBreaking * surfAlive * saturate(_SurfStrength)
                                               * saturate(_SurfSupersedeFringe));

                // ---- THE DRAWN WET EDGE runs in and out (owner: "i would like the swash in and out of the
                // water along with the tides"). Until now the swash moved ONLY the foam band — the water's
                // own edge never budged, so nothing read as water advancing and retreating. Apply the same
                // swash offset to the drawn edge, HARD-CAPPED at _SwashMaxEdgeShift metres of level.
                //
                // ⚠️ This is the deliberate, bounded SEE != FEEL divergence. It moves the drawn edge only:
                // gameplay never reads this fragment, the iso-depth frame and WaveFieldAnimator are
                // untouched, and the vertex/displaced pass and the fetch march keep the clean _WaterLevel.
                // The cap is well inside the standing "wade ~0.5 m" tolerance, so no ground the player can
                // stand on changes meaning. _SwashEdgeShift = 0 restores the previous edge exactly.
                float edgeSwash = 0.0;
                if (swashEdgeReach > 0.0 && depth < shoreCosmeticReach)
                {
                    // Scaled by the SAME floored slope the foam band uses, so the drawn edge and the foam on
                    // it travel together and the authored amplitude keeps meaning "metres of contour", then
                    // hard-capped. Without the slope term a steep rock shore and a tidal flat would both get
                    // the full cap of LEVEL, which on the flat is an unbounded horizontal sweep — the very
                    // thing the 2026-07-23 guard exists to forbid.
                    float cap = saturate(_SwashMaxEdgeShift);
                    edgeSwash = clamp(BeachSwash(worldXY, max(depth, 0.0), t)
                                          * swashSlope * saturate(_SwashEdgeShift),
                                      -cap, cap);
                }
                // The EXACT edge. Everything past the coarse pre-clip above that the swash has not actually
                // run up onto is dry after all.
                // ADR 0040 rev 3: where a bore is alive, the drawn edge rides its RUN-UP — metres of level,
                // already capped at the swash's own ceiling — and drains between crests; elsewhere the
                // cosmetic swash keeps its beat (the fringe-supersede precedent: yield on the bore, not on
                // the gate). _SurfRunUpStrength = 0 (or no surf here) is the previous edge exactly.
                float boreEdgeBlend = saturate(surfBreaking * saturate(_SurfStrength) * saturate(_SurfRunUpStrength));
                float boreEdgeShift = clamp(surfRunUpM, -saturate(_SwashMaxEdgeShift), saturate(_SwashMaxEdgeShift));
                edgeSwash = lerp(edgeSwash, boreEdgeShift, boreEdgeBlend);
                clip(depth + edgeSwash + 1e-4);

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

                // ---- DEEP BLUE enrichment (owner mandate: "deep blues"; col.rgb ONLY) -------------------------
                // Pull the settled BASE colour toward a rich navy as the water deepens. The shipped material
                // reads its base from the owner's HAND-PAINTED _DepthRamp (its deep end is a muted slate-teal);
                // this deepens that read WITHOUT repainting his art. Keyed to the READ-ONLY deep fraction dt
                // and applied BEFORE every additive layer — the #182 swell-read, the swell bands, spec and
                // foam all ride on top at FULL amplitude (nothing is washed out) — and BEFORE the palette
                // guard-rail (ADR 0015), which stays the final colour owner and bounds this like every other
                // layer. Pre-grade WATER colour (not light content), so it dims with the night overlay like
                // the rest of the sea. smoothstep from _DeepBlueStart leaves the shallows and the mid ramp
                // untouched. col.rgb ONLY — never depth/clip()/dt/_WaterLevel/the height read/the sim
                // (P1 integrity, CLAUDE.md rule 5). _DeepBlueStrength = 0 is an EXACT passthrough.
                if (_DeepBlueStrength > 0.001)
                {
                    // onset capped below 1 so the smoothstep interval can never degenerate (edge0 == edge1
                    // is NaN territory on some GPUs); at the cap the pull applies only at the very deepest dt.
                    float deepT = smoothstep(min(saturate(_DeepBlueStart), 0.99), 1.0, dt);
                    col.rgb = lerp(col.rgb, _DeepBlueColor.rgb, deepT * saturate(_DeepBlueStrength));
                }

            #if defined(_USE_SEABEDTEX)
                // ---- SEABED ABSORPTION (col.rgb ONLY; ADR 0027 #7 — SUPERSEDES the Arc C see-through) --------
                // The bottom is composited HERE by the shader itself, so col.a stays OPAQUE and the alpha-blend
                // dependency §17.1 relied on is gone. That is what dissolves §17.3's caustic/see-through
                // cancellation BY CONSTRUCTION: the caustic add below is no longer faded by a lowered alpha.
                //
                // _SeabedTex is baked over the SAME world rect as _HeightTex (ADR 0014's _HeightWorldMin /
                // _HeightWorldSize), so the bottom is registered to the elevation that decides how deep it is
                // and NO new uniform is needed. Its ALPHA is COVERAGE, not opacity: where the owner's terrain
                // painted no ground tile (the Deep / Channel types deliberately CLEAR theirs) coverage is 0 and
                // nothing is composited — open water with no baked bed is unchanged by construction.
                //
                // The sample coordinate is snapped on the WORLD PPU grid (Pixelize — the crawl law, §3), so a
                // bottom cell belongs to a place on the seabed and stays there while the camera pans; the
                // texture itself is imported Point + Clamp so nothing smears between cells.
                //
                // Applied AFTER the depth block settles the base colour (the _USE_DEPTHRAMP sample OR the
                // _ShallowColor/_DeepColor lerp) and after the deep-blue enrichment, and BEFORE every additive
                // layer — so swell tint, fbm, spec, caustics and foam all ride ON TOP of the composited bottom,
                // which is where they physically belong. depth/depthC are READ-ONLY (the sim waterline is
                // untouched — never depth/clip/_WaterLevel/the height read; P1 integrity, CLAUDE.md rule 5).
                // _Turbidity = 0 skips the whole block — the EXACT passthrough (and the toggle above compiles
                // it out entirely on the shipped material).
                float3 absSigma = AbsorptionSigma();
                if (dot(absSigma, float3(1.0, 1.0, 1.0)) > ABSORPTION_EPS)
                {
                    // depthC = the cosmetic organic-fringe depth (== depth when _ShoreNoise = 0), so the bottom
                    // fades WITH the visible shore instead of following the clean iso-contour.
                    float2 bedP  = Pixelize(worldXY);
                    float2 bedUV = (bedP - _HeightWorldMin.xy)
                                 / max(abs(_HeightWorldSize.xy), float2(1e-3, 1e-3));
                    // Off-rect reads would smear the Clamp edge texel across the whole sea — zero the coverage
                    // instead, so a region larger than its bake simply has no bottom outside it.
                    float bedIn = (bedUV.x >= 0.0 && bedUV.x <= 1.0 &&
                                   bedUV.y >= 0.0 && bedUV.y <= 1.0) ? 1.0 : 0.0;
                    half4 bed = SAMPLE_TEXTURE2D(_SeabedTex, sampler_SeabedTex, bedUV);

                    float3 bedT = AbsorptionTransmission(absSigma, max(depthC, 0.0));
                    bedT = AbsorptionBand(bedT, _AbsorptionBands);
                    col.rgb = lerp(col.rgb, bed.rgb, saturate(bedT) * saturate(bed.a) * bedIn);
                }
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
                WaveFieldSample(Pixelize(worldXY), waveFreqScale, waveFetchEnv,
                                waveHeight, waveSlope, waveCrest, wavePrimCos);
                bool trainsLive = _WaveFieldParams.x >= 0.5;

                float swellCrest;   // the 0..1 crest driver every downstream layer reads (spec bias,
                                    // whitecap crest gate, sky reflection lit faces)
                float swellSigned;  // the -1..1 brightness modulation (crests lighter, troughs darker)
                float swellReadSigned; // the -1..1 BROAD, glass-gated crest signal for the legibility band
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
                    // The LEGIBILITY band reads the BROAD normalized crest (waveHN, pre-sharpen) not the
                    // pinched swellCrest, so the swell reads as the water RISING/FALLING (a wide moving
                    // band) rather than a thin spike — much easier to time a heave against. Same glass gate.
                    swellReadSigned = (waveHN * 2.0 - 1.0) * swellLive;
                }
                else
                {
                    // LEGACY noise swell — the cycle-off fallback, unchanged.
                    swellCrest = SwellField(worldXY, depth, t);   // 0..1 (rolls IN near shore)
                    swellSigned = (swellCrest - 0.5) * 2.0;       // -1..1
                    swellReadSigned = swellSigned;                // legacy path has no separate broad signal
                }
                if (_OceanSwellStrength > 0.001)
                {
                    // 0.30 (was 0.25): a pinched crest covers less area than the old wide band, so a touch
                    // more gain restores the punch without a black sea (max swing = +/-0.30*_OceanSwellStrength;
                    // at the 0.16 default that is +/-0.048 — a defined ridge, not an over-dark trough).
                    col.rgb += swellSigned * _OceanSwellStrength * 0.30;
                }

                // ---- MODELLED-SWELL CALM GATE (owner playtest 2026-07-08: "i still do see them at calm") ----
                // Shared by the SWELL READ band and the SWELL FACE SHADING below — they are one modelled
                // swell, so one gate. The wave field's amplitude gate (swellLive) engages by ~0.025 m, so a
                // small-but-real calm-day swell still earned the full amplified read; this smoothstep RISE
                // over _Chop (== SeaState01 — the drift-line window's axis) melts both terms away toward
                // glass. MONOTONE, not the drift-line bell: the read must survive a heavy sea (the haul is
                // timed against the swell exactly when the sea is up). The property-block comment carries the
                // canon band mapping for the defaults. Scales two existing pre-grade adds only (col.rgb;
                // rule 5). Both-props-0 disables the gate (hi clamps to lo + 1e-3 => 1 on any non-glass sea).
                float srLo = saturate(_SwellReadSeaStateLo);
                float srHi = max(_SwellReadSeaStateHi, srLo + 1e-3);
                float swellReadGate = smoothstep(srLo, srHi, _Chop);

                // ---- SWELL READ (legibility): make the passing swell VISIBLE so the player can time the
                // heave. Amplifies the crest->trough VALUE contrast of the SAME shared wave field the hull
                // rocks on and the haul times against (swellReadSigned = the broad, glass-gated crest signal),
                // so the swell reads as a raised, MOVING band of light over dark that lifts and passes under
                // the boat — SEE == FEEL (P1). Independent of _OceanSwellStrength (reads even where the stock
                // swell is dialed down). col.rgb ONLY — never depth/clip/the deep tint/_WaterLevel/the sim
                // wave field (P1 integrity, CLAUDE.md rule 5). _SwellReadStrength = 0 is an EXACT passthrough.
                if (_SwellReadStrength > 0.001 && swellReadGate > 0.001)
                {
                    float readBand = swellReadSigned;                 // -1..1, already travels with the real crest
                    // Optional pixel-art posterize: quantize the moving band into N discrete VALUE steps so
                    // it reads as a crisp marching contour (0 = smooth). Done in 0..1 space like _DepthBands.
                    if (_SwellReadBands >= 1.0)
                    {
                        float b01 = readBand * 0.5 + 0.5;
                        b01 = floor(b01 * _SwellReadBands + 0.5) / _SwellReadBands;
                        readBand = b01 * 2.0 - 1.0;
                    }
                    // 0.25 ceiling: at the 0.35 default the swing is +/-0.0875 (a clearly legible band, ~3x
                    // the owner's tuned stock swell); the palette guard-rail's value floor/ceiling bounds the
                    // extremes so troughs never go muddy nor crests blow out. A dedicated add (not a bump to
                    // _OceanSwellStrength) so the owner has ONE clear "how readable is the swell" knob.
                    // The calm gate scales the FINISHED layer (after the posterize), so on a falling sea the
                    // whole contour fades as one — the quantized steps never re-shuffle mid-melt.
                    col.rgb += readBand * _SwellReadStrength * swellReadGate * 0.25;
                }

                // ---- SWELL FACE SHADING (owner mandate: "better looking waves"; col.rgb ONLY) -----------------
                // The shared wave field's ANALYTIC slope (waveSlope — computed by WaveFieldSample above and
                // previously unused in the composite) tilts each swell face toward or away from the ONE implied
                // sun (_SunDir, falling back to the material's _LightDir — the ADR 0006 single-light discipline,
                // the exact fallback the specular layer uses below). The surface normal's ground component is
                // MINUS the height gradient, so a face is LIT where -slope·light is positive (its downhill side
                // looks at the sun) and SHADED behind the crest. Where the #182 swell-read band is SYMMETRIC
                // (crest bright / trough dark), this is ANTISYMMETRIC (lit face vs shaded back) — the two
                // COMPOSE into a modelled, directional wave instead of doubling one band into glare. Self-
                // gating: dead glass publishes zero amplitude => zero slope => zero term (the §11 mirror is
                // untouched), and the legacy no-trains path leaves waveSlope at 0 (the pre-B1 look is unchanged
                // there). col.rgb ONLY — never depth/clip()/the deep tint/_WaterLevel/the sim wave field
                // (P1 integrity, CLAUDE.md rule 5). _SwellFaceShade = 0 is an EXACT passthrough. Shares the
                // calm gate with the read band above — one modelled swell melts away as one on a glassy calm.
                if (_SwellFaceShade > 0.001 && swellReadGate > 0.001)
                {
                    float2 shadeSunXY = dot(_SunDir.xy, _SunDir.xy) > 1e-6 ? _SunDir.xy : _LightDir.xy;
                    float2 shadeLd = normalize(shadeSunXY + float2(1e-4, 0));
                    // x2 normalizes the field's small physical slopes (amp x k, ~0.1..0.8 in a real sea,
                    // already carrying the _OceanSwellScale visual-frequency factor) to a legible -1..1
                    // signal; the clamp bounds a heavy sea. 0.15 is the add ceiling (the swell-read idiom):
                    // at the 0.22 default the swing is +/-0.033 — shading, not glare — and the §13 palette
                    // rail bounds the extremes like every other layer.
                    // + the bore front's own face (ADR 0040 rev 3; zero unless _SurfFrontSlope is up).
                    float faceSigned = clamp(-dot(waveSlope + surfFrontSlope, shadeLd) * 2.0, -1.0, 1.0);
                    col.rgb += faceSigned * saturate(_SwellFaceShade) * swellReadGate * 0.15;
                }

                // ---- ENVELOPE VALUE BANDS (ADR 0023 §(4) — the big wave is marked by SHADE as well as foam)
                // The displaced-water arc's named shading component: posterize the field's ENVELOPE-RELATIVE
                // height (waveHeight / _WaveFieldParams.z — the same normalizer the crest factor uses) into
                // SOLID value steps shaded from the owner's palette anchors (_PaletteDeep/Mid/Shallow — the
                // ADR 0015 anchors, so the bands wear his colours and the guard-rail below still bounds
                // them), Bayer-dithered ONLY at the band edges on world-locked PPU cells (the style law
                // §(3): full-range dither = airbrush, forbidden — spike-measured). Because the value axis is
                // envelope-relative, the TOP band is reachable only by a near-envelope crest — the rare big
                // wave is marked by shade even before its foam core (the spike's still-frame keystone; bands
                // 7 and window 0.4 are the spike's tuned values). SHARED fragment: the flat pass gains the
                // banded read, the displaced pass shades its own lifted geometry with the same bands.
                // Gates: trains only (no envelope without the field), the glass gate (dead-flat sea shows no
                // bands — the swellSigned constant, reused) and the shared modelled-swell CALM gate (the
                // bands melt away with the swell they mark; one modelled swell, one gate). col.rgb ONLY —
                // never depth/clip()/the deep tint/_WaterLevel/the height read/the sim (P1 integrity,
                // CLAUDE.md rule 5). _EnvelopeBandStrength = 0 is an EXACT passthrough (today's look).
                float bay = BayerWorld(worldXY);   // ONE world-locked dither read; the caps below reuse it
                if (_EnvelopeBandStrength > 0.001 && trainsLive && swellReadGate > 0.001)
                {
                    float bandLive = saturate(_WaveFieldParams.z * 40.0);   // the glass gate (~0.025 m engages)
                    if (bandLive > 0.001)
                    {
                        // 0 = full trough .. 0.5 = mean level .. 1 = the full envelope crest.
                        float vN = saturate(waveHeight / max(_WaveFieldParams.z, 1e-5) * 0.5 + 0.5);
                        // ---- DE-REGULARIZE (owner: "the large white bands are too regular like a pattern")
                        // TWO low-frequency world noise reads (patch mask + value warp, different UVs).
                        // Wind-STRETCHED: patches on a real sea are drawn out downwind, and stretching also
                        // stops the mask itself reading as a second regular grid laid over the first.
                        float2 patchAxis = normalize(_WindDir.xy + float2(1e-4, 0));
                        float2 patchPerp = float2(-patchAxis.y, patchAxis.x);
                        float patchFreq = max(_EnvelopeBandPatchScale, 1e-4);
                        // 0.45 along-wind = patches ~2.2x longer downwind than across it.
                        float2 patchUV = float2(dot(worldXY, patchAxis) * patchFreq * 0.45,
                                                dot(worldXY, patchPerp) * patchFreq);
                        // (a) WANDER the value axis before posterizing. A slowly-varying offset moves where
                        // each band boundary falls, so the contours meander instead of running parallel.
                        // The field itself is untouched — bands still mark the real crests, they just stop
                        // marking them on a ruler.
                        vN = saturate(vN + (ValueNoise(patchUV * 2.3 + 31.7) - 0.5) * _EnvelopeBandWarp);
                        float q = BandValue01(vN, _EnvelopeBands, _EnvelopeBandDitherWin, bay);
                        // (b) PATCHINESS of the band's local contribution: full inside a patch, fading to
                        // _EnvelopeBandPatchMin between them. At patchMin = 1 this is exactly 1 everywhere,
                        // i.e. today's wall-to-wall banding, bit for bit.
                        float patchMask = lerp(saturate(_EnvelopeBandPatchMin), 1.0,
                                               smoothstep(0.35, 0.75, ValueNoise(patchUV)));
                        float3 bandShade = q < 0.5
                            ? lerp(_PaletteDeep.rgb, _PaletteMid.rgb, q * 2.0)
                            : lerp(_PaletteMid.rgb, _PaletteShallow.rgb, (q - 0.5) * 2.0);
                        // ---- the SEAM fades the bands, exactly as it fades the caps (owner playtest
                        // 2026-07-23: "shoreline looks a bit swirly"). The displaced surface dies at the
                        // walkable waterline (ShoreFade01 — the seam twin, §21), so envelope-relative
                        // shade on the dying edge marked waves that visibly are not there — the band-edge
                        // dither drew worm contours crowding along the shore over the bright shallow ramp.
                        // Same curve, same band as capShoreFade below (_ShoreFadeBand: pushed DERIVED per
                        // tick on the displaced pass; the material default 0.5 m gives the flat pass a
                        // thin graceful band; a zeroed band degrades to "no fade", never a divide). The
                        // C# twin is WhitecapSalienceMath.BandShoreSalience — change both in lockstep.
                        // Depth is READ-ONLY here (P1, rule 5).
                        float bandSeam = ShoreFade01(depth, _ShoreFadeBand);
                        col.rgb = lerp(col.rgb, bandShade,
                                       saturate(_EnvelopeBandStrength) * swellReadGate * bandLive
                                           * bandSeam * patchMask);
                    }
                }

                // ---- CAPILLARY RIPPLES (ADR 0027 #10; col.rgb BRIGHTNESS only) --------------------------------
                // The finest band on the sea: a ~0.12 m octave riding ON the larger waves, which is what makes
                // water read as WATER close up (the shader's previous finest layer, _WindChopScale 0.7, is a
                // 1.4 m band — chop, not ripples). Drawn AFTER the envelope value bands deliberately: those
                // LERP col.rgb toward the palette anchors, so ripples added before them would be partly washed
                // away — texture belongs on top of shade. Still under the foam/whitecaps/specular below, which
                // is where breaking water and glints physically belong.
                //
                // TIER A PERMANENTLY (ADR 0027's own words): this never touches depth/clip()/_WaterLevel/the
                // height read/_WaveFieldParams/the sim — a ripple is surface texture, not a force, and must
                // never enter the field the hulls ride (P1 integrity, CLAUDE.md rule 5). Nothing is saved.
                // Three gates, each with an explicit off end, all twinned by WaterRipple.
                // _RippleStrength = 0 is an EXACT passthrough: the whole block is skipped.
                if (_RippleStrength > 0.001)
                {
                    float rWind = RippleWindGate();          // (1) no wind, no ripples — glass stays glass
                    if (rWind > 0.001)
                    {
                        float2 rWindN = normalize(_WindDir.xy + float2(0, 1e-4));
                        // (2) the windward-face term reads the SHARED field's slope — waveSlope, already
                        // sampled above for the face shading, so no new uniform and no second opinion about
                        // the same surface. SKIPPED when the trains are not live: a dead field publishes ZERO
                        // slope, which the gate would read as "not a windward face" and would erase the band
                        // on the legacy / edit-mode / bare-art-scene path.
                        float rFace = trainsLive ? RippleWindwardGate(dot(waveSlope, rWindN)) : 1.0;
                        // (3) the framing fade — the anti-density guard that REPLACED the ADR's per-zoom-tier
                        // amplitude fade (see RippleFramingFade: there is nothing sub-pixel to fade).
                        float rAmp = saturate(_RippleStrength) * rWind * rFace * RippleFramingFade();
                        if (rAmp > 0.001)
                        {
                            float rBand = RippleField(worldXY, t, rWindN);
                            // The layer's OWN quantization (ADR 0027's condition), DEFAULT ON — it matters
                            // most here because _DepthBands is 0, so the base ramp lends no pixel character
                            // and a smooth ripple would read as airbrushed shimmer. Dithered at the step EDGE
                            // only, on the world-locked Bayer cell already read above (bay) — zero crawl by
                            // construction. Below 2 steps the band is left smooth (the _DepthBands idiom).
                            if (_RippleBands >= 2.0)
                                rBand = BandValue01(rBand, _RippleBands, _RippleDitherWin, bay);
                            // Remap 0..1 -> -1..1 so troughs darken as well as crests lightening, bounded by
                            // RIPPLE_ADD_CEIL; the ADR 0015 palette guard-rail still owns the final colour.
                            col.rgb += (rBand * 2.0 - 1.0) * rAmp * RIPPLE_ADD_CEIL;
                        }
                    }
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
                    // ---- ADR 0027 #2: FIELD-DRIVEN caustics (default OFF = the independent noise above) ----
                    // Caustics are focused light: brightest where the surface is locally CONVEX toward the
                    // sun (a dome focuses). The local curvature is the finite-difference LAPLACIAN of the
                    // SAME WaveFieldSample() the swell bands/whitecaps/hull ride — 4 axis taps at
                    // _CausticCurvatureStep metres around the centre height already sampled above
                    // (waveHeight, at Pixelize(worldXY) and the SAME waveFreqScale), each tap itself on the
                    // pixelized world grid (the crawl law, §3) — so the seabed shimmer finally belongs to
                    // the swell rolling over it. The curvature signal replaces only the vein VALUE via the
                    // blend: the _CausticDepth gate, the _CausticDayGate sun gate below, _CausticAmount,
                    // _CausticColor and the painted-tex blend above all keep their exact roles. Gated by
                    // the field's amplitude (the swellLive idiom): no live trains (edit mode / a bare art
                    // scene) or a dead-glass sea eases the blend back to the noise — a bare scene never
                    // loses its dapple, and physically a flat surface focuses nothing. col.rgb only —
                    // never depth/clip()/_WaterLevel/the height read/the sim (P1 integrity, rule 5). NO
                    // new sim-pushed uniform (ADR 0027's "no new uniform": the curvature needs no new
                    // data; the three knobs are material props, rule 6). Cost at the shipped default: the
                    // whole block is unreachable (_CausticCurvatureBlend = 0) — exact passthrough.
                    if (_CausticCurvatureBlend > 0.001)
                    {
                        float ce = max(_CausticCurvatureStep, 1e-3);
                        float chx1, chx0, chy1, chy0, ccrestT, cprimT;
                        float2 cslopeT;
                        WaveFieldSample(Pixelize(worldXY + float2(ce, 0.0)), waveFreqScale, waveFetchEnv,
                                        chx1, cslopeT, ccrestT, cprimT);
                        WaveFieldSample(Pixelize(worldXY - float2(ce, 0.0)), waveFreqScale, waveFetchEnv,
                                        chx0, cslopeT, ccrestT, cprimT);
                        WaveFieldSample(Pixelize(worldXY + float2(0.0, ce)), waveFreqScale, waveFetchEnv,
                                        chy1, cslopeT, ccrestT, cprimT);
                        WaveFieldSample(Pixelize(worldXY - float2(0.0, ce)), waveFreqScale, waveFetchEnv,
                                        chy0, cslopeT, ccrestT, cprimT);
                        // Laplacian < 0 = locally convex UP (a dome toward the sun) -> focused light.
                        float lap = (chx1 + chx0 + chy1 + chy0 - 4.0 * waveHeight) / (ce * ce);
                        float curvBright = saturate(-lap * max(_CausticCurvatureGain, 0.0));
                        float fieldLive = saturate(_WaveFieldParams.z * 40.0);
                        caustic = lerp(caustic, curvBright,
                                       saturate(_CausticCurvatureBlend) * fieldLive);
                    }
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
                    // ADR 0027 #4: the spec's normal-tilt read follows the surface band's effective
                    // frequency (BandFreq) — glints coarsen with the same growing sea, one meaning.
                    float2 gp = Pixelize(worldXY * BandFreq(_NoiseScale));
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
                col.rgb += SkyReflection(worldXY, waveSlope, surf, swellCrest, t);

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

                // ---- OBJECT REFLECTIONS (ADR 0027 #8; col.rgb only) --------------------------------------------
                // OVER the sky mirror (a boat reads on top of reflected cloud — hence the premultiplied
                // over-operator here rather than an add) and UNDER the foam below (whitecaps read on top of the
                // boat). PRE-grade, so it dims with the night like the rest of the sea — except the night-lit
                // share, which is held back and added AFTER the palette grade with the moon glitter (§11.6).
                // The lookup is warped by the SAME wave field the hull rides and snapped on the WORLD grid.
                // Zero cost at the shipped default (_ObjectReflectStrength = 0 returns immediately), and zero
                // cost even when dialled in until something in the scene carries a ReflectiveObject.
                float4 objReflPre;
                float3 objReflPost;
                ObjectReflection(worldXY, waveSlope, IN.positionCS.xy, depth, objReflPre, objReflPost);
                col.rgb = col.rgb * (1.0 - objReflPre.a) + objReflPre.rgb;

                // ---- BREAKING WAVES (ADR 0040): evaluated EARLIER now (before the drawn edge's clip), so the
                // bore's run-up can move the wet edge. See 'THE SURF, RESOLVED BEFORE THE EDGE' above.

                // ---- layer 3 foam fringe (depth ~ 0 band that hugs the moving waterline) ----------------------
                // ALWAYS-ON swash: a cosmetic, _Time-driven depth offset that advances/recedes the wet edge.
                // GATED to the depth~0 band (full at the wet edge, 0 by ~2x the foam width) and applied ONLY
                // to a LOCAL foam-only depth — the real `depth` (clip/dt/caustics) is never touched, so deep
                // water and the gameplay waterline don't move. Pure foam dressing (P1 integrity, rule 5).
                // depthC (the cosmetic organic-fringe depth; == depth when _ShoreNoise = 0) drives the VISIBLE
                // foam band so the wet edge reads wiggly on calm water — while the real `depth`/clip() (the
                // gameplay waterline) stays the clean iso-contour. depthC is local + foam-only (P1, rule 5).
                float swashReach = max(_FoamWidth, 1e-3) * 2.0 + max(abs(_SwashAmplitude), 1e-3);
                float swashGate  = 1.0 - smoothstep(0.0, swashReach, depthC);   // 1 at the wet edge -> 0 deeper
                // shoreSlope (computed with the fringe above) turns the swash's depth amplitude into a
                // CONTOUR excursion in metres — on a gently painted bar the run-up no longer sweeps a
                // metres-wide worm tongue (the 2026-07-23 swirl defect); a steep edge keeps today's look.
                float foamDepth  = depthC - lerp(BeachSwash(worldXY, depthC, t) * swashSlope * swashGate,  // local, foam-only
                                                 surfRunUpM, boreEdgeBlend);   // …and the foam rides the bore's wash too
                // ---- DITHER the band edge (owner judge pass 2026-08-01: "the shoreline foam sometimes gets
                // these artifact lines"). foamEdge is an ISO-CONTOUR of foamDepth, and foamDepth descends
                // from the seabed height TEXTURE — 8 bits over a -4..+6 m range, i.e. 3.91 cm per code,
                // bilinear at ~2 px/m. Where the seabed is near-flat (the sandbar flats — the exact spot in
                // his screenshot) a single code step spans METRES of ground, so an entire texel row crosses
                // the smoothstep at the same instant and the band edge snaps to the texture lattice: a
                // straight, axis-aligned line that no amount of foam noise hides, because the noise rides
                // ON the contour rather than across it.
                //
                // Offsetting the contour by a world-locked Bayer cell scatters the crossing depth across the
                // row, so the lattice row breaks into a dithered edge. This is the SAME instrument the
                // envelope bands, the ripple posterize, the cap gate and the wake coverage already use —
                // the shore path was the one band in the shader with no dither at all. Reuses the single
                // `bay` read from above (no second BayerWorld tap). _FoamEdgeDither = 0 = the old contour.
                float foamEdgeDither = (bay - 0.5) * _FoamEdgeDither;
                // smoothstep across a thin band just inside the water: 1 at the wet edge -> 0 by foamWidth deep.
                float foamEdge = 1.0 - smoothstep(0.0, max(_FoamWidth, 1e-3), foamDepth + foamEdgeDither);
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
                    // THE FRINGE YIELDS to the physical whitewater (owner ruling 2026-08-28). At
                    // _SurfSupersedeFringe = 0 this is exactly today's fringe — the passthrough the dial
                    // ships under, and the A/B a reviewer can take.
                    foamCoverage *= (1.0 - surfSupersede);
                    col.rgb = lerp(col.rgb, _FoamColor.rgb, foamCoverage * _FoamColor.a);
                    col.a = max(col.a, foamCoverage * _FoamColor.a);
                }

                // ---- BREAKING WAVES (ADR 0040): draw the surf the block above resolved -------------
                // Owner, 2026-08-27: "our waves are missing something. i want them to be even more
                // physics based." Nothing here decides WHERE the surf is — the painted seabed and the
                // tide do, through the contour solved on the sim tick. This only dresses it.
                //
                // The band is brightest AT the break line and thins shoreward as the bore ages, which is
                // a spilling breaker: the crest crumbles white at the top and runs in as whitewater. The
                // plunging anatomy (the lip thrown forward, the barrel, the pocket) is the second drop.
                //
                // The WIDTH is not a knob, deliberately (owner ruling 2026-08-28: "leave it — the
                // bathymetry decides"). It is the depth band divided by the local slope, so a gentle
                // shoal gets wide surf and a steep edge gets a thin line, and that difference is
                // information the player can read straight off the water.
                //
                // Glass stays sacred: _BreakerOuter.w is 0 on a dead-calm sea, so surfCover is 0 and this
                // is one compare and out — no taps, no cost, no surf.
                if (surfCover > 0.001 || surfBarrel > 0.001)
                {
                    float strength = saturate(_SurfStrength);

                    // THE BARREL FIRST, under everything: it is a hollow in the water, so it shades the
                    // sea itself before any foam is laid on top. Drawn as a colour rather than a
                    // brightness scale so it stays inside the ADR 0015 water grade like every other
                    // layer here.
                    float barrel = SurfBandValue(saturate(surfBarrel * saturate(_SurfBarrelShade)), bay) * strength;
                    col.rgb = lerp(col.rgb, _SurfBarrelColor.rgb, barrel * _SurfBarrelColor.a);

                    // THE WHITEWATER: the sheet, pocket-boosted where it is young and violent.
                    float cover = SurfBandValue(surfCover, bay) * strength;
                    col.rgb = lerp(col.rgb, _SurfColor.rgb, cover * _SurfColor.a);
                    col.a   = max(col.a, cover * _SurfColor.a);

                    // THE LIP LAST, over the hollow it is throwing across — the brightest thing in the
                    // surf, and the thing that reads as the wave pitching forward.
                    float lip = SurfBandValue(saturate(surfLip), bay) * strength;
                    col.rgb = lerp(col.rgb, _SurfLipColor.rgb, lip * _SurfLipColor.a);
                    col.a   = max(col.a, lip * _SurfLipColor.a);
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
                    // ADR 0027 #4: the cap field's blob scale follows the surface band's effective
                    // frequency (BandFreq) — storm foam blobs grow with the same growing sea.
                    float cap = EvolvingField(aniso, float2(0, 0), BandFreq(_NoiseScale) * 3.0 * _FoamBlobScale,
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
                    // Painted whitecap STAMP SHEET (white-on-transparent) drifted WITH the body (the foam
                    // drift blend, not a fixed current scroll). Routed through UntileSampleW (like the other
                    // painted slots) so what remains of the sheet's repeat stops reading — dialed by
                    // _UntileStrength, kept pixel-snapped (PaintUV inside). Sampled ONCE here; each path
                    // below folds it in.
                    // ⚠️ _WhitecapTexScale, NOT _PaintScale: this slot PLACES the caps, so its repeat period
                    // IS the whitecap lattice the owner saw. See the property's note — the sheet is 4x the
                    // old tile in each axis at 1/4 the scale, so the mark's size, coverage and pixel grid are
                    // unchanged and only the periodicity is gone. Untiling never fixed this and never could.
                    half4 capSample = UntileSampleW(TEXTURE2D_ARGS(_WhitecapTex, sampler_WhitecapTex),
                                          worldXY, _WhitecapTexScale, capDrift, _UntileStrength);
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

                    // ---- FOAM CLUMPING (owner mandate: rafts + windrows, not an even sprinkle) ----------------
                    // A second, much BROADER and SLOWER evolving field REDISTRIBUTES the cap coverage so the
                    // foam gathers into patches with bare water between, instead of speckling uniformly. It is
                    // stretched along the WIND like the caps (windrows are wind-aligned lanes) and sampled on
                    // the same drifted coord (wp = worldXY + capDrift) so the rafts TRAVEL with the foam and
                    // reorient as the weather wanders. Evolves at 0.35x the foam boil rate — big rafts morph
                    // slower than the flecks riding them (a documented shaping constant, like the lifecycle's
                    // band edges). Reuses EvolvingField + Pixelize: no new noise machinery, pixel-art faithful
                    // (§3). col.rgb/col.a foam dressing ONLY — never depth/clip()/dt/_WaterLevel/the sim
                    // (P1 integrity, CLAUDE.md rule 5). _FoamClumpStrength = 0 is an EXACT passthrough.
                    float clumpGate = 1.0;
                    if (_FoamClumpStrength > 0.001)
                    {
                        float2 clumpAniso = float2(dot(wp, wdir), dot(wp, wperp) * max(_FoamClumpStretch, 1.0));
                        float clumpField = EvolvingField(clumpAniso, float2(0, 0), max(_FoamClumpScale, 1e-4),
                                                         _FoamEvolveSpeed * 0.35, t);
                        // A soft patch mask around the evolving field's midline (its values centre on ~0.5, so
                        // the 0.35..0.65 band yields roughly half patch coverage at full strength): high field
                        // = a foam raft, low field = the clear lane between windrows.
                        float patch = smoothstep(0.35, 0.65, clumpField);
                        // REDISTRIBUTE rather than merely thin: in-patch coverage lifts x1.25 (denser hearts —
                        // the caller saturates), between-patch coverage falls toward bare water, the whole gate
                        // dialed by the master strength so 0 = today's even sprinkle.
                        clumpGate = lerp(1.0, patch * 1.25, saturate(_FoamClumpStrength));
                    }

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
                        // ==== ENVELOPE SALIENCE (ADR 0023 phase 2 step 2 — retire the uniform speckle) ====
                        // Yesterday every local crest tip earned the same dense core, which is exactly what
                        // hid the big one (the spike's control image). The SOLID core is now RESERVED for
                        // near-envelope crests: waveCrest (the sharpened crest factor — already
                        // height/envelope) gates it at the spike-tuned threshold (0.62) — hard past the
                        // solid margin, Bayer-dithered across the fringe (edges only, the style law), zero
                        // below. Ordinary chop keeps only the thin milky residual streaks (milkyPart below,
                        // itself already envelope-keyed through crestF). _CapSalienceStrength 0 restores the
                        // legacy even salience EXACTLY.
                        float envGate = CapEnvelopeGate(waveCrest, _CapEnvelopeThreshold, _CapSolidMargin,
                                                        _CapDitherBand, bay);
                        float solidPart = capCoreT * breakCore * capPeak
                                          * lerp(1.0, envGate, saturate(_CapSalienceStrength));
                        // RESIDUAL: milky, trailing BEHIND the crest, streaked downwind by the aniso coord.
                        float milkyPart = capMilkyT * residualLife * lerp(0.45, capPeak, capDens);
                        // ---- ADR 0027 #3: CONVERGENCE (Jacobian) FOAM — an ADDITIONAL placement driver
                        // ALONGSIDE the crest-keyed lifecycle, never replacing it. Where crossing trains
                        // PINCH the surface (J < 1) foam may now appear even off the primary crest — the
                        // confused-sea read the tall-wave-only gate could not produce. Four taps of the
                        // SAME WaveFieldSample (at the same waveFreqScale, each on the pixelized world
                        // grid) central-difference the field's ANALYTIC slope into the three second
                        // derivatives; ConvergenceGate (C#-twinned by WaterFoam.Convergence) turns them
                        // into the 0..1 pinch term. The term is TEXTURED by the same thresholded cap
                        // field (capMilkyT — already banded/dithered), so it feeds the existing foam
                        // threshold and inherits the existing quantization (no new one). Still inside
                        // waveGate (glass = zero foam) and the shore fade below; col.rgb-only dressing
                        // (rule 5). At the shipped default (_FoamConvergenceStrength = 0) the branch is
                        // unreachable and the composite below is BIT-IDENTICAL to the pre-#3 line
                        // (same left-to-right multiply order).
                        float lifecyclePart = saturate(max(solidPart, milkyPart)) * crestGate;
                        if (_FoamConvergenceStrength > 0.001)
                        {
                            float fe = max(_FoamConvergenceStep, 1e-3);
                            float chpx, chmx, chpy, chmy, ccrF, cprF;
                            float2 cspx, csmx, cspy, csmy;
                            WaveFieldSample(Pixelize(worldXY + float2(fe, 0.0)), waveFreqScale, waveFetchEnv,
                                            chpx, cspx, ccrF, cprF);
                            WaveFieldSample(Pixelize(worldXY - float2(fe, 0.0)), waveFreqScale, waveFetchEnv,
                                            chmx, csmx, ccrF, cprF);
                            WaveFieldSample(Pixelize(worldXY + float2(0.0, fe)), waveFreqScale, waveFetchEnv,
                                            chpy, cspy, ccrF, cprF);
                            WaveFieldSample(Pixelize(worldXY - float2(0.0, fe)), waveFreqScale, waveFetchEnv,
                                            chmy, csmy, ccrF, cprF);
                            // central differences of the ANALYTIC slope -> the second derivatives
                            // (hxy averaged from its two estimates — symmetric by construction).
                            float convHxx = (cspx.x - csmx.x) / (2.0 * fe);
                            float convHyy = (cspy.y - csmy.y) / (2.0 * fe);
                            float convHxy = ((cspx.y - csmx.y) + (cspy.x - csmy.x)) / (4.0 * fe);
                            float conv = ConvergenceGate(convHxx, convHyy, convHxy, _FoamConvergencePinch)
                                       * saturate(_FoamConvergenceStrength);
                            lifecyclePart = max(lifecyclePart, capMilkyT * conv);
                        }
                        capOpacity = lifecyclePart * waveGate * saturate(dt);
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
                    // FOAM CLUMPING: gather the coverage into the rafts/windrows (both paths; see the gate
                    // above). The saturate bounds the x1.25 in-patch lift so the lerp below never overshoots.
                    // ---- the SEAM fades the caps (ADR 0023 §"Whitecap salience retune") -------------------
                    // Near shore the displaced surface dies at the walkable waterline (ShoreFade01 — the
                    // seam twin, design doc §21); its dying edge must not wear open-sea caps. The cap
                    // opacity fades with the SAME curve over the SAME band the displaced vertex stage reads
                    // (_ShoreFadeBand: pushed DERIVED per tick on the displaced pass; the material default
                    // 0.5 m gives the flat pass a thin graceful band, and ShoreFade01's own floor degrades a
                    // zeroed band to "no fade", never a divide). Shore foam/swash above is the separate
                    // production dressing layer — untouched. Scaled by the master so _CapSalienceStrength 0
                    // remains an EXACT legacy passthrough. Depth is READ-ONLY here (P1, rule 5).
                    float capShoreFade = lerp(1.0, ShoreFade01(depth, _ShoreFadeBand),
                                              saturate(_CapSalienceStrength));
                    capOpacity = saturate(capOpacity * clumpGate) * capShoreFade;
                    col.rgb = lerp(col.rgb, _FoamColor.rgb, capOpacity);
                }

                // ---- ADVECTED FOAM BUFFER (ADR 0027 #6): the wake, as a mark left on the sea ----------------
                // Added in the SAME pre-grade dressing zone the fringe foam + whitecaps occupy, so the
                // palette guard-rail below bounds it AND it dims with the night like the rest of the foam.
                // It is placed AFTER both, deliberately: this layer ADDS foam the field cannot produce
                // (a trail that persists and drifts after the boat has gone; churn around a hull that is
                // merely bobbing at her mooring) and must never replace either — see the ADR's #6 note that
                // BoatWakeEmitter stays. It lerps toward the SAME _FoamColor every other foam layer uses, so
                // wake foam and sea foam are one material rather than two whites.
                //
                // col.rgb ONLY — the whitecap convention, deliberately: never depth/clip()/_WaterLevel/the
                // height read/the sim (P1 integrity, rule 5), and it leaves col.a's transmission contract
                // (ADR 0027 #7) untouched. _WakeFoamStrength = 0 makes WakeFoamCoverage return 0 on its
                // first line, so the shipped look is byte-identical until the owner dials it in.
                float wakeFresh;
                float wakeFoam = WakeFoamCoverage(worldXY, bay, wakeFresh);
                if (wakeFoam > 0.001)
                {
                    // AGED (owner ask 2026-08-27): white only where the hull is working the water right
                    // now, then down the sea's own ramp as the freshness clock runs down. The two
                    // channels do different jobs and must not be confused: FRESHNESS picks the colour,
                    // COVERAGE is the weight - so the oldest foam is both the bluest and the faintest,
                    // which is what "fades into the ambient ocean over time" is.
                    col.rgb = lerp(col.rgb, WakeFoamAgedColor(wakeFresh), saturate(wakeFoam) * _FoamColor.a);
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
                col.rgb += DriftLines(worldXY, dt, depth, t, waveFreqScale, waveFetchEnv);

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

                // THE SEA'S OWN ALBEDO, kept from BEFORE the grade — what a lamp actually has to light.
                // Pre-grade on purpose: at deep night the guard-rail's value floor saturates and pulls every
                // pre-overlay pixel toward luma 1, lit and unlit alike, so a lit term built on the GRADED
                // colour would light foam and open water by nearly the same amount. The un-graded composite
                // still carries the contrast the sea authored — white surf against a dark body — which is
                // the whole difference between a beam that reveals a break line and a beam that paints a
                // disc. Read-only; the grade below is unchanged.
                float3 waterAlbedo = col.rgb;

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
                // The lamp's relief sees the bore front's face as well as the swell's (ADR 0040 rev 3).
                float3 beamLitColor;
                float beamW = BoatLightTerm(worldXY, waveSlope + surfFrontSlope, waveHeight, beamLitColor);
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
                // ---- LIT WATER (the night ruling): the lamp's colour laid ON the sea's own albedo --------
                // The reveal above MULTIPLIES, and a multiply cannot light a black sea: the downstream
                // day/night overlay takes ~(0.016, 0.020, 0.040) of whatever it produced. This term ADDS,
                // in the compensated bucket below, so it survives — and because it carries waterAlbedo it
                // is not a slab: surf, foam and whitecaps (bright in the sea's own colour) light up hard,
                // the open body takes a faint sheen, and troughs stay dark. The 2026-07-05 floodlamp
                // complaint is answered by that factor and not by turning the brightness down.
                //
                // It is strongest where the night is blackest, by construction: the compensation divides by
                // the tint, so a moonlit night (a brighter tint) needs less lamp — which is the physics and
                // the ruling agreeing. 0 = the pre-PR look, exactly. col.rgb ONLY (P1, rule 5).
                float3 beamLit = beamLitColor * waterAlbedo * max(_BeamLitStrength, 0.0);
                // ADR 0027 #8: the NIGHT-LIT share of an object reflection joins this bucket for exactly the
                // reason the moon glitter does. A lit wheelhouse reflected in black water is LIGHT content;
                // left in the pre-grade composite the downstream multiply would crush it to ~3% and the boat
                // would appear to have doused her lamps in her own reflection. Ordinary reflected surfaces
                // (hull planking, a tree, a wharf pile) are NOT light and stay pre-grade, dimming with the
                // sea — that split is `post` vs `pre` from ObjectReflection, and it is 0 here by day because
                // the compensation factor is ~1 then and the whole sample lands in `pre`.
                float3 lightContent = beamTint + beamLit + skyNightRGB + objReflPost + RainRings(worldXY, dt, t);
                col.rgb += (dnSum > 1e-3)
                    ? lightContent / max(_DayNightTint.rgb,
                                         float3(DN_COMP_MIN_CHANNEL, DN_COMP_MIN_CHANNEL, DN_COMP_MIN_CHANNEL))
                    : lightContent;

                return col;
            }
        ENDHLSL

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
            #pragma shader_feature_local _ _USE_SEABEDTEX
            #pragma multi_compile_instancing
            ENDHLSL
        }

        // ==== PASS 2: THE DISPLACED SURFACE (ADR 0023 phase 2 — off-screen only) ==============
        // The sea as a real displaced mesh, drawn ONLY by IsoFacetHullFeature's HHWater renderer
        // list into the ADR 0022 off-screen recording: its own colour target (_HHWaterScreenTex;
        // alpha = the water's own translucency) sharing the facet pass's PRIVATE depth buffer —
        // never the scene depth buffer (a depth-writing mesh there punches holes in every later
        // sprite that z-tests; the ADR 0022 lesson, verbatim). The in-scene face of this pass is
        // the WaterOverlay quad (HiddenHarboursWaterOverlay.shader), which re-composes these
        // pixels through a SortingGroup exactly where the flat sprite sorts. The 2D renderer
        // never draws this pass (unknown LightMode) and the flat pass never draws off-screen —
        // the A/B toggle (DisplacedWaterSurface) shows exactly one of the two.
        // ZTest LEqual / clear-1 semantics on purpose: this is the STANDARD camera-matrices
        // RenderGraph path, where URP handles reversed-Z. The GEqual / clear-0 convention
        // belongs to raw hand-built command-buffer harnesses ONLY (ADR 0023's calibrated trap).
        Pass
        {
            Name "HHWaterDisplaced"
            Tags { "LightMode" = "HHWater" }
            Blend Off
            ZWrite On
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex vertDisplaced
            #pragma fragment fragDisplaced
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
            #pragma shader_feature_local _ _USE_SEABEDTEX
            #pragma multi_compile_instancing

            // ==== THE DISPLACED VERTEX STAGE (ADR 0023) ===============================================
            // Each vertex lifts by
            //     lift = height * _WaveExaggeration * ShoreFade01(stillDepth, _ShoreFadeBand)
            // — Core ShoreFadeMath.DisplacedHeight, verbatim (the twin). The height comes from the
            // SAME WaveFieldSample the fragment paints with (the ONE-SEA rule: one deterministic
            // field, two sampling densities — the vertex grid instead of the pixel grid), at the
            // SAME visual frequency scale, so the geometry's crests sit under the painted crests.
            // The still depth is the SAME painted-seabed read the fragment's clip() gates on.
            //
            // THE SEAM (P1 integrity): the fade is EXACTLY 0 at and beyond the walkable waterline,
            // so displacement dies before it can contradict the clip() contour — which still reads
            // the UNDISPLACED ground position (worldXY carries the ground coords, not the lifted
            // ones). The walkable waterline and the clip contour stay byte-identical to the flat
            // pass's, at every tide, by construction.
            //
            // View z: a lifted crest also steps NEARER in the private z-buffer and the ground y
            // recedes at the iso elevation's cosine (_WaterIsoDepth = (cos elev, sin elev), the
            // spike's convention) — so if the screen mapping ever overlaps (it is provably monotone
            // at x1.5 for the reference sea, ADR 0023 §(2)), the nearer surface wins. Calibrating
            // hull-vs-water z (the waterline climbing the planking) is phase 3's work; within the
            // water itself this ordering is already correct. The y reference is the height map's
            // world-rect min (_HeightWorldMin) — ONE constant for the whole sea, so chunked meshes
            // share a continuous depth ramp with no seams at chunk borders.
            Varyings vertDisplaced(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
                VertexPositionInputs pos = GetVertexPositionInputs(IN.positionOS);
                float3 ws = pos.positionWS;
                float2 ground = ws.xy;                       // the UNDISPLACED ground position

                float vHeight;
                float2 vSlope;
                float vCrest;
                float vPrimCos;
                float vFreqScale = max(_OceanSwellScale, 1e-4) / WAVE_LEGACY_SCALE_REF;
                // The fetch envelope marched with the EXPLICIT-LOD seabed sampler — the vertex stage has no
                // derivatives. _HeightTex carries no mips, so LOD 0 IS the texture and this reads the same
                // elevations the fragment does: the displaced surface rides the lee the fragment draws.
                float vFetchEnv = FetchEnvelope01Lod(ground);
                WaveFieldSample(ground, vFreqScale, vFetchEnv, vHeight, vSlope, vCrest, vPrimCos);

                float stillDepth = _WaterLevel - SeabedElevationLod(ground);
                float lift = vHeight * _WaveExaggeration * ShoreFade01(stillDepth, _ShoreFadeBand);

                ws.y += lift;
                ws.z += (ground.y - _HeightWorldMin.y) * _WaterIsoDepth.x - lift * _WaterIsoDepth.y;

                OUT.positionCS = TransformWorldToHClip(ws);
                OUT.uv = IN.uv;
                OUT.worldXY = ground;   // frag paints AND clips at the ground position — the lift
                return OUT;             // moves pixels, never the waterline contour.
            }

            // ---- THE INTERIOR MASK (ADR 0023) -----------------------------------------------
            // The sea may not draw inside a boat. IsoFacetHullFeature's guard pass has already
            // written, for every pixel, whether the NEAREST hull surface there is an open interior
            // (cockpit sole, hold floor, working deck); this discards against it.
            //
            // `discard` kills the depth write as well as the colour, which is the whole point: the
            // pixel keeps the hull's depth, the hull fragment survives its own z-test, and the hull
            // overlay composes it — an interior that simply cannot be reached by the sea, at any
            // heading, with no footprint scan and no radius to guess wrong.
            //
            // Everything OUTSIDE the boat is deliberately left alone, so the waterline climbs her
            // planking truthfully and an outboard's leg and prop stay wettable. Declared in THIS
            // pass only — never at SubShader scope, where it would reach the Universal2D pass that
            // draws the in-scene Sea sprite and the owner's eight water presets.
            //
            // ⚠️ Reads a GLOBAL bound by the feature (and to a 1x1 BLACK fallback by
            // IsoFacetHullRegistry before it has ever run). Unbound, a sampler returns Unity's grey
            // placeholder ~0.5 and this test becomes a coin flip on whether the whole sea vanishes.
            Texture2D<float> _HHHullGuardTex;

            half4 fragDisplaced (Varyings IN) : SV_Target
            {
                if (_HHHullGuardTex.Load(int3(int2(IN.positionCS.xy), 0)) > 0.5)
                    discard;
                return frag(IN);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
