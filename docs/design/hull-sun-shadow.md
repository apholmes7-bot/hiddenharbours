# A mesh hull's sun shadow

Implementation on the original `f3ce747a` base. The owner skipped the broad comparison phase: the cached mesh route is built directly, with six before/after proof frames and one counter read under the Phase C lease. The historical map and route analysis below explain the decisions. This serves P1, the hour's light on the fleet, and P5, a consistent picture for figures and boats. The rig's fixed lighting, rigs, kits, boat defs, camera ladder and lamps stay outside this change.

## Built amendment: the house shades its own deck

There are two complementary receivers. D4a remains the late water/wharf shadow: its fragment rejects this hull's main id and fore block before claiming the shared shade stencil. D4b adds the own deck: only this hull's id block is accepted, and only existing deck triangles are drawn. Neighbours, water and wharf cannot receive this second draw. Both use the same one-claim stencil and hard multiply, so they cannot claim a pixel twice. A figure within the deck footprint may receive the shade; a protruding figure outside it does not receive this deck shade.

The plane comes from the existing `WatertightDeckHeightMeters`, carried read-only through the renderer snapshot. Receiver triangles must be upward-facing and lie at that height; their lowest tag identifies the deck level. Cape Islander and Tanker both identify level 1 this way. Dory has no faces tagged above its deck level, so it creates no receiver renderer, GPU buffers or extra draw. There is no new def field, inferred height literal, rig edit or kit edit.

`HullDeckShadowMesh` caches the deck triangles and a stackless bounding-volume tree of the borrowed hull and both door leaves. A shared-material variant of `HiddenHarboursHullSunShadow` draws the receiver once. Each deck fragment checks toward the camera for a nearer visible hull surface; walls and roof therefore hide deck behind them. It then checks the inverse projection ray toward the published sun against faces tagged above the deck level. This is equivalent to projecting those house faces onto the deck plane, without an extra mask target or a sun-direction depth pass. The tree and triangle buffers are built only when geometry/height changes. Warm updates reuse them and one property block; disable releases them, the receiver mesh and draw object. The last user releases the shared deck material variants.

The inverse ray uses the same sprite-profile length law and current heading, roll and pitch. The receiver follows the actual posed deck transform, including visual heave. The camera test and house test both use the displayed door marker and the same shared level/role predicate as the facet shader. A lifted house contributes no intersections. Sun at or below the horizon disables both receivers; the fixed key, dither and night lamps retain their existing behavior. The shade has one hard threshold, with no distance ramp, blur or gradient.

Failure modes are explicit: the flat declared plane must have matching deck triangles; a mesh without them gets no deck draw. The cached tree models the hull and its displayed door leaf, not independently animated fittings. Camera visibility uses source geometry rather than a new depth target; coincident faces and their tiny depth biases retain a possible edge disagreement to check in the small render guard. The own-id test is a final footprint fence. Any changed deck-proof pixel outside that footprint is a stop, not grounds to widen the mask. The deck variant requires structured-buffer shader support; the GPU proof uses the supported desktop renderer.

Scope: the mesh's tagged house, including its cabin, hardtop, flybridge and carried mast, shades its own deck. Railings and thin fittings are excluded. A separate sun-direction depth pass for arbitrary overhangs remains outside this change.

Budget is structural. The water route has one cached combined mesh and one draw per visible hull. A hull with a house adds one cached deck receiver draw; a houseless hull adds none. Materials are shared and warm ticks allocate zero managed bytes under the guards. The two named draw/batch counters were not offered by the batch harness; their deltas are unavailable. The failed counter probe is retained as evidence and is not rerun for this amendment.

## Current path, by symbol

All code paths below start at `Assets/_Project/Code/`; shader paths start at `Assets/_Project/Art/Shaders/` unless stated otherwise.

| Concern | Base anchor and behavior |
|---|---|
| Clock and sun | `Art/DayNightMath.cs:35,52,74,94`: solar noon is `(sunrise + sunset)/2`; elevation is `cos(SolarX * pi/2)`; `SunDirection` is opposite `ShadowDirection`. This elevation is a normalized driver, not an altitude in radians. |
| Published weather | `Art/DayNightController.cs:175-195` publishes `_SunDir`, `_SunElevation`, `_ShadowStrength`. `Art/DayNightMath.cs:216-245` folds visibility and continuous sea-state gloom into strength. Casters reuse those globals rather than reading Environment again. |
| Sprite projection | `Art/SpriteShadow.cs:79-81,587-639`: cast direction is opposite `_SunDir`; height scales the profile length; alpha is `ShadowAlpha(MaxAlpha, strength)`; RGB comes from the shared profile. Bare art scenes have a fallback hour. |
| Shared appearance | `Art/SpriteShadowProfile.cs:125-154`, `Assets/_Project/Resources/SpriteShadowProfile.asset:15-25`: max alpha 0.45; colour `(0.04,0.05,0.10)`; noon length 0.35, horizon length 5, cap 3; `ScreenSpaceShade` ships on. |
| Length and darkness | `Art/DayNightMath.cs:238-272`: `e <= 0` gives zero strength and length. Otherwise length per height is `min(lerp(5,0.35,saturate(e)),3)`. With the shipped weather profile, full fog/storm produces weather dim 0.6 and multiplies clear strength by `1 - 0.6*0.85 = 0.49`; it does not erase every shadow. |
| Actual shade draw | `Art/SpriteShadow.cs:656-673` puts the multiply material on Default sorting layer, order `SortingBands.SunShade`. `HiddenHarboursSpriteShadow.shader:155-178,285-306` is an ordinary `Universal2D` draw, `Blend Zero SrcColor`, with own-caster discard and a one-shade stencil. |
| Hull lamp caster | `Art/HullLampShadowCaster.cs:29-57` registers with `LampShadowSystem`, supplies the overlay rectangle and reflection waterline contact. `Art/IsoFacetHullPresentationService.cs:70` installs it. |
| Lamp projection | `Art/LampShadowSystem.cs:532-565` supplies main id and the fore-id block to a pooled quad for each selected lamp/caster pair. `HiddenHarboursLampShadow.shader:134-185` inverse-shears into `_HHHullScreenTex`, tests id membership, and discards the caster's own pixels. The pair pool controls the total draws; this is not one draw per hull independent of lamps. |
| Resolved hull image | `Art/IsoFacetHullFeature.cs:155-159,1291-1324` resolves before the lowest-layer sprite draw and publishes `_HHHullScreenTex`. `GetResolveTarget:1416-1424` requests persistent ARGB32 with sRGB enabled; transient colour targets use `R8G8B8A8_SRGB` at `:1408`. Alpha encodes hull membership, not coverage opacity. |
| Pose | `Art/IsoFacetHullRenderer.cs:846-861,920-968` applies heading, roll and pitch, then heave and the displaced-water depth calibration; the overlay window separately follows ride. `Art/IsoFacetMath.cs:65-105` contains the pre-view rotations and fixed projection. |
| Open house and door | `Art/IsoFacetHullRenderer.cs:247-314,738-749,987-994` drives level/role masks and a separately swapped door leaf. `HiddenHarboursIsoFacet.shader:538-566` discards hidden faces. The main mesh's positions alone do not describe an open house. |
| Fixed hull key, held | `Art/IsoFacetHullRenderer.cs:675` writes `IsoFacetShaderIds.LightN`; its actual uniform is `_LN` (`Art/IsoFacetHullRegistry.cs:309`). `Art/IsoFacetMath.cs:136-137` negates the rig key's z; `HiddenHarboursIsoFacet.shader:335` uses it. The old `_LightN` spelling and line 673 are stale. |

### What “shade buffer” means here

There is no dedicated sampled sun-shade texture on this base. `ScreenSpaceShade` selects a late multiply over the assembled camera colour. The `Universal2D` renderer draws it into its active camera colour attachment, with the active depth/stencil attachment. The target format comes from the camera descriptor, not from `SpriteShadowProfile`; HDR is supported in `Assets/Settings/UniversalRP.asset:26-27`. Phase B records the actual camera format. A fixed R8 shade-buffer format would be an invented claim.

The framework entry is `DrawRenderer2DPass`, tag `Universal2D`, which binds active colour and depth. The project has no separate sun-shadow render feature. `SpriteShadowShade.mat` carries `_ScreenShade = 1`, blend Zero/SrcColor and stencil ref 1 / NotEqual / Replace. A successful fragment claims the pixel; overlapping triangles or casters must not multiply it again. Self-discard must happen before that claim.

## Receiver and composite order

No receiver below samples a sun-shade texture. Each is affected after its visible colour reaches the camera target. “Receives” in this table means the final multiply affects that pixel.

| Surface or intermediate | Samples shade today? | How it reaches the frame, then receives |
|---|---|---|
| Water | No; final colour receives | `HiddenHarboursWater.shader:1108-1109,4763`: transparent world water; shade lands after its colour, reflections and foam. Its custom water prepass is not a shade consumer. |
| Ground | No; final colour receives | `HiddenHarboursTerrainSplat.shader:201-208,405`; `Art/TerrainSplatSurface.cs:31,463-467` sorts ground at -21, below the sea. |
| Wharf deck and faces | No; visible final colour receives | Ground tops use terrain surfaces; sprite/mesh faces use `HiddenHarboursTidalFace.shader:84-92,158` and lit-sprite paths. Raised wharf/cliff geometry also uses `CliffFace`. A shadow does not independently relight a physical vertical wall. |
| Cliff faces | No; final colour receives | `HiddenHarboursCliffFace.shader:150-157,404`; `Art/CliffWallSurface.cs:445-450,985-990` sorts its mesh groups in the world band. |
| Sprite figures | No; final colour receives | `HiddenHarboursLitSprite.shader:114-121,187`. A visible figure at the shaded screen point darkens with the ground. |
| Art-pixel mesh figures | No | `HiddenHarboursFigurePixelTarget.shader:55-80` exports figure colour/coverage; `Art/IsoFacetFigureTarget.cs:411-428` publishes that target. The final `HHFigureOver` in `HiddenHarboursIsoFacetOverlay.shader:67,112` assembles the visible figure before shade. The low-resolution target itself is not relit by the sun caster. |
| Other hulls: facet and resolve | No | `HiddenHarboursIsoFacet.shader:319-322,736` writes private MRTs; `HiddenHarboursIsoFacetResolve.shader:224,289` resolves them. These intermediates remain unchanged. |
| Other hulls: visible overlay | No; final colour receives | `HiddenHarboursIsoFacetOverlay.shader:31-33,110-112` samples the resolved id image and composes figures. Shade then multiplies that final visible pixel. |
| Deck-occluded sprites | No; surviving final colour receives | `HiddenHarboursDeckOccludedSprite.shader:65-71,126-138` uses the hull image for occlusion, then draws in world sort before shade. Hidden pixels remain hidden. |
| Casting hull's own deck | Explicit new exclusion | Reject its main and fore-block ids in `_HHHullScreenTex`, using the lamp membership rule. This prevents its own sun caster darkening its deck. Neighbour hulls remain receivers. |

`Core/Art/SortingBands.cs:114-144` fixes the ladder:

1. World water, ground, faces, figures and hull overlays.
2. WorldTint, 32760: day/night multiply.
3. SunShadePool, 32762: existing crown-contact shade.
4. SunShade, 32763: existing sprite silhouettes and the proposed hull silhouette, one shared stencil law.
5. Lamp glow, 32767, followed by lamp-shadow multiply at the same order with its existing depth offset.

Sun shade therefore cannot dim the lamps' later additive glow. It darkens the visible screen surface, including raised neighbours or roof edges; it is not a receiver-height-aware shadow map. Existing canopy-contact shade is unchanged. D4 excludes only this hull's own cast shade; shade cast by neighbours remains allowed. A figure protruding beyond the hull id footprint is a separate receiver, not automatically part of its self-exclusion mask.

## Two routes

| | Posed mesh flattened to the waterline: lean | Resolved-screen silhouette: alternative |
|---|---|---|
| Daytime draws | One main-hull draw using one cached shadow proxy, one submesh and one shared material. A naive separate door-leaf draw would make it two; the proposed proxy contains tagged closed/open leaf geometry and selects the displayed one in the shader. | One bounded inverse-shear quad per hull, independent of lamp count. Reuses the resolved id texture already produced each frame. |
| Allocation | Build and own the proxy at Configure/shape replacement; share the material; cache one property block and conservative bounds. No render target, material, mesh, collection or vertex array is created during warm updates. Release at disable/destruction as ownership requires. | Pool the quad and property block, share material. No silhouette capture or new target. The resolved texture already exists. No warm-update allocations. |
| What is available | Real vertex height and full mesh, including geometry hidden from the camera. Heading, roll, pitch and heave come from the renderer's current pose. | The exact visible contour after heading, roll, heave, cutaway, doors, fittings, resolve and water clipping is already in the image. |
| What needs work | Apply the same level/role discard as the visible facet pass. Include the selected door leaf in the cached proxy. Mesh reuse alone gives neither property for free. Projection must use pre-view geometry, not calibrated private depth as physical altitude. | It has no reliable per-pixel physical height, so inverse-shearing a whole image is a 2D approximation. Hidden backs, camera-occluded parts, off-window geometry and submerged/discarded image portions cannot cast. |
| Limits | A flat receiving plane, with final screen-space multiply. No self-shadow, wall/receiver height solution or wave-conforming shadow. Separately articulated fittings are not magically included in the one main-hull draw; adding them changes proxy data, cost and scope. | A sheared camera silhouette rather than the hull's true ground projection. Sideways spread and foreshortening can be wrong, especially for large/tall hulls; cannot reconstruct open-house depth. |
| Cost to measure in B | One draw/hull; shadow-proxy bytes, triangles, overdraw and GPU time. CPU work is constant-sized state and bounds math, not a mesh scan per frame. | One draw/hull; quad bounds/overdraw and two id-image reads around the inverse shear. Camera clipping and silhouette loss are plate findings. |

### Projection contract for the lean

Use `DayNightMath` and the live sprite-shadow profile, not the reference board's sun. With `e = _SunElevation`, `d = normalize(-_SunDir.xy)` and `k = ShadowLength(e, profile...)`, project the posed point at height h above the live waterline along a bounded effective sun ray. In the unprojected plane, its form is `q.xy = p.xy + dGround*k*h`, `q.z = waterline`. Convert d into that frame so the final displayed displacement follows the existing sprite direction and drawn-height convention; do not apply the fixed view elevation twice. The original physical `1/tan(altitude)` is deliberately replaced by the profile law.

Use the pre-view rotation described by `IsoFacetMath.RigToWorld`, then the existing fixed iso projection for output. The renderer's artificial depth shear is not geometry altitude. Derive presentation heave and the receiving plane together so displaced-water ride is not counted twice and rock still changes the shape. Clamp below-plane geometry rather than reflecting it into an opposite shadow. Waterline points remain fixed. Both the 32 px/m hulls and Tanker's 16 px/m must use their def scale.

The proxy carries original level tags and a leaf-part marker. Share the visibility predicate with the facet shader rather than maintaining two subtly different cutaways; a small HLSL extraction is part of the proposed C file set. No rig or baked asset changes. Cache a conservative projection extent from bounds, the profile cap and current pose; do not retain the unprojected mesh bounds and let culling cut the rake off.

At e <= 0 or zero strength, submit no sun contribution. Keep lamp registration, pairing, materials and timing unchanged. Strength and colour remain the shared sprite values. Fog and overcast use the controller's published strength; no second weather model, new weather subscription or independent tint dial. Respect `ScreenSpaceShade`; B should also show the legacy/off arm, with a cached alpha-over material sorted under its hull rather than a new receiver texture.

## Reference: the board's look beside the game's law

Reference labels here identify read-only packages; no package document, rig or image is imported into the repo. Rice Point one-scene, dated 2026-10-11, `tools/rptKeyScenes.js:294-346` supplies piece sampling boxes. Its `:315-317` wharf comment names a 20:30 sun 5.9 degrees above the horizon and an eastward rake reaching x -25. These are sampling bounds and an example reach, not a sun formula or a length cap.

The actual call chain is `tools/rptSceneBake.cjs:24-29` -> `StpPano.skyOf` (`kits/.../pass2/stpPano.js:225-226`) -> `WeatherSky.at` (`kits/export/wharf-rig-kit-v2/lib/weatherSky.js:64-98`). The sky uses azimuth/elevation keys, spherical interpolation, weather dimming and a later moon transition. `StpPano.shadeFrom:304-312` samples terrain/tide height in the piece boxes; `WharfRig2.shadeAt:328-334` and `CoastalPass.light.shadeAt:571-574` test geometry visibility and retain ambient occlusion. This is richer than a flat screen multiply.

Board coordinates are east/south/up. The table converts south to north before comparing shadow headings (degrees clockwise from north). Board length is the geometric horizontal/vertical sun-vector ratio, before finite sampling-box clipping. Game length is the styled profile multiplier; the normalized game elevation is not a degree angle. Values below are source calculations, not rendered measurements.

| Hour | Game e; shadow heading; length/height | Board solar altitude; shadow heading; geometric length/height |
|---|---|---|
| 08:00 | 0.4339; 302.62 degrees; 2.9824 | 31.39 degrees; 269.70 degrees; 1.6389 |
| 12:00 | 0.9749; 351.63 degrees; 0.4666 | 73.19 degrees; 184.68 degrees; 0.3020 |
| 12:30, reference noon board | 0.9937; 356.05 degrees; 0.3792 | 72.50 degrees; 155.57 degrees; 0.3153 |
| 13:00, game's solar noon | 1; 0 degrees; 0.35 | 68.29 degrees; 133.22 degrees; 0.3981 |
| 17:00 | 0.6235; 44.29 degrees; 2.1008 | 30.25 degrees; 104.34 degrees; 1.7147 |
| 19:30 | 0.1120; 74.11 degrees; 3, capped | 10.92 degrees; 105.17 degrees; 5.1814 |
| 20:30 | -0.1120; no sun shadow | 5.86 degrees; 112.56 degrees; 9.7390 |

Finding for B: moving the game to this board would turn the 12:00 shadow about 167 degrees, change its length from 0.4666 to 0.3020 per height, remove the game's 19:30 cap difference (3 versus 5.1814), and retain sunlight after the game's 20:00 sunset. The lean is to move neither model in this PR. Keep the game's law and show the reference as a look comparison with these differences labelled. In particular, a board labelled night can still have a low sun; its label cannot override the game's horizon gate. Moon-driven reference light is not a sun caster.

## Historical Phase B proposal: cancelled before any launch

Use the eight T0-T7 coverage classes named in `Art`'s fleet catalogue, `Tools/Editor/RigBaking/HullMeshFleet.cs:247-305`. Actual boat-def lengths, rather than approximate catalogue labels, set the framing.

| Class | Boat def; hull mesh def | Length; camera world height |
|---|---|---|
| T0 | `Dory.asset`; `DoryIsoHullMesh.asset` | 4.5 m; 14 m |
| T1 | `Punt.asset`; `PuntIsoHullMesh.asset` | 5.2 m; 17 m |
| T2 | `CapeIslander.asset`; `CapeIslanderIsoHullMesh.asset` | 12.9 m; 24 m |
| T3 | `LobsterBoat.asset`; `LobsterBoatIsoHullMesh.asset` | 12 m; 23 m |
| T4 | `SideDragger.asset`; `SideDraggerIsoHullMesh.asset` | 25 m; 40 m |
| T5 | `SternTrawler.asset`; `SternTrawlerIsoHullMesh.asset` | 38 m; 60 m |
| T6 | `CoastalPacket.asset`; `CoastalPacketIsoHullMesh.asset` | 60 m; 90 m |
| T7 | `Tanker.asset`; `TankerIsoHullMesh.asset` | 110 m; 160 m; 16 px/m |

Boat defs are in `Assets/_Project/Data/Boats/`, mesh defs in its `HullMeshes/`. The length is at line 19 in each listed boat def; camera height is at line 47. Test the berth (water beside a ground top, wharf face, figure and neighbouring hull) and open sea (water, reflection, foam and displaced ride). Reuse one fixed, reported evidence frame per hull/location; widen the evidence camera if the capped rake requires it, and label that frame. Do not change the runtime camera ladder.

| Game capture | Read-only comparison package, board and true hour |
|---|---|
| Fair 08:00 | Rice Point one-scene 2026-10-11, `keyscene.rpt_strand_crossing/boards/strand_morning.png`, 08:00. |
| Fair 12:00 | Same package, `keyscene.rpt_point_ledges/boards/point_noon.png`, actually 12:30; label the 30-minute offset. Add a 12:30 check if B needs an exact clock match. |
| Fair 17:00 | NMC one-scene reissue 3, 2026-10-07, `keyscene.nmc_landing/boards/landing_low.png`, 17:00. |
| Fair 19:30 | Rice Point package, `keyscene.rpt_back_shore/boards/back_evening.png`, 19:30, windy. Label the weather difference on the fair control. |
| Fog 06:30 | NMC reissue 3, `keyscene.nmc_the_falls/boards/falls_morning.png`, 06:30, fog. Use game visibility at the full-fog profile threshold. |
| Overcast 15:00 | NMC reissue 3, `keyscene.nmc_weather_face/boards/bluff_squall.png`, 15:00, squall. Compare hour exactly, label the squall versus overcast difference. |
| Night 22:30 | NMC reissue 3, `keyscene.nmc_town_bridge/boards/bridge_night.png`, 22:30. Compare lamps with sun casting disabled. |

Rice Point additionally has 05:30, 08:15, 14:30, 16:30 and 20:30 boards. The NMC reissue also supplies 10:30, 11:00, 13:00, 14:30, 16:30, 18:42 and 21:00. Older key-scenes r2/r3 packages contain boards but lack the per-board `.render.json` records used for this verified-hour selection; do not infer an hour from a name alone. The evidence inventory retains those packages and marks unknown hours.

Planned capture budget: 8 hulls * 2 locations * 7 light/weather states = 112 frozen states. Current and mesh prototype give 224 images. Add screen-route comparisons for Cape Islander and Tanker at both locations/four fair hours: 16 more images. Add 8 hulls * 2 locations * 2 heading/rock/heave stress poses, paired: 64 images. Add Cape Islander and Tanker * 2 locations * 2 house/door states at 17:00, paired: 16 images. Add those two hulls at both locations at 12:00 and 17:00 with the profile's shade arm off, paired: 16 images. Total 336 raw PNGs arranged as 160 comparison plates; 16 plates also carry the alternate route. Reference panels reuse the named board without rebaking or changing it. Every capture records heading, roll, pitch, heave, ride, cutaway, door, hour, weather, camera rectangle, scale, format and frame index.

Reserve one `boats-look-b2` lease for 110 minutes, one editor launch: 15 minutes warm refresh/import allowance, 15 prototype/harness setup, 35 capture, 15 image/cost checks, 10 clean shutdown/return, 20 reserve. This is an estimate, not a measured duration. Stream PNGs, retain no raw RGBA copies, budget 4 GB of evidence. B's paste must explicitly authorise the temporary prototype and collector; none exists in production from Phase A. B records actual draws, warmed CPU allocations, GPU cost per hull, proxy memory and peak target memory. No second launch without the next paste's authority. Return the lease and prove launched processes gone.

## C's files, overlap boundary and gates

This is the implementation edit set. New Unity assets below carry their matching `.meta`; the docs folder has no sibling metas. The historical matrix is superseded by the six Cape Islander/Dory proof pairs at 08:00, 17:00 and 22:30, plus the optional Tanker pair. No proxy-versus-quad comparison is required.

- New `Assets/_Project/Code/Art/HullSunShadowCaster.cs`, `HullSunShadowMath.cs`, `HullSunShadowMesh.cs`.
- Modify `Assets/_Project/Code/Art/IsoFacetHullRenderer.cs` only to expose/synchronise a read-only pose, cutaway and leaf snapshot; `IsoFacetHullPresentationService.cs` to fit the caster.
- New `Assets/_Project/Art/Shaders/HiddenHarboursHullSunShadow.shader`, `Include/HullLevelGate.hlsl`; modify `HiddenHarboursIsoFacet.shader` to use the shared visibility predicate. Keep `_LN`, the dither threshold and existing shading unchanged.
- New `Assets/_Project/Resources/HullSunShadow.mat` and `HullSunShadowLegacy.mat`, the shared multiply and legacy alpha-over arms.
- New `Assets/Tests/EditMode/Art/HullSunShadowMathTests.cs`, `HullSunShadowCasterTests.cs`, `HullSunShadowRenderTests.cs`; extend `IsoFacetShaderCompileGuardTests.cs` for the new shader. Update this design note with the measured result.

No listed file intersects the supplied #992 WATER GAINS edit set. In particular, no edit is planned to `HiddenHarboursWater.shader`, `WaterSurface.cs`, water defs/plumes, `IVisibilitySource`, `IWorldTimeSignal`, `WaveMemory` or `EnvironmentService.cs`. Receivers do not need a shade sample. If B reveals a required receiver change or the chosen route changes this list, C waits for the seat's overlap ruling. Plant and catch files, rigs and kits remain untouched.

| Gate shape for C | Location and evidence |
|---|---|
| Same clock/pose/profile gives the same projection; 08:00/12:00/17:00/19:30 use `DayNightMath` | `HullSunShadowMathTests.Projection_UsesThePublishedSunAndProfile` parameter cases; matrix and projected point assertions. |
| Plane points fixed; below-plane points do not produce reversed shadows; heading, roll, pitch and heave transform consistently; both 16 and 32 px/m | `HullSunShadowMathTests.Waterline_IsFixed`, `BelowWater_DoesNotCastBackwards`, `Pose_UsesPreViewGeometry`, `PixelScale_ComesFromDef`; synthetic geometry, expected values independent of the implementation. |
| Horizon/night and zero strength contribute nothing | `HullSunShadowMathTests.SunAtOrBelowHorizon_IsDisabled`; rendering no-contribution image comparisons, including 20:30 and 22:30. Test actual float horizon behavior rather than a double-precision transcription at exactly 20:00. |
| Own main/fore ids are excluded before stencil; neighbouring id receives; overlapping projected triangles shade once | `HullSunShadowCasterTests.OwnIdBlock_IsExcluded`; `HullSunShadowRenderTests.Neighbour_Receives`, `Overlap_MultipliesOnce`, pixel masks and counts. |
| Open house and selected leaf agree with displayed geometry | `HullSunShadowRenderTests.CutawayAndDoor_MatchTheDisplayedMask`, closed/open synthetic fixtures and paired B plates; no hidden roof casting. |
| Profile, on/off arm and clock updates do not clone materials; warmed updates allocate zero managed bytes | `HullSunShadowCasterTests.Profile_IsShared`, `WarmUpdates_AllocateZeroManagedBytes`, `Disable_ReleasesOwnedResources`; warm first, measure repeated state updates and material/mesh counts. Also report proxy bytes at configuration. |
| Frame order preserves water/wharf/figure/other-hull coverage, art-pixel edges and lamps; no new sun at night | `HullSunShadowRenderTests.ReceiverOrder_PreservesLampsAndFigures`, `Night_LampsAreUnchanged`; paired image masks and Frame Debugger draw counts. |
| Shader and existing art guards remain green | `IsoFacetShaderCompileGuardTests`, existing lamp/sprite/figure/hull suites and full CI by exact case names. No Unity or tests are run in A. |

Baseline for the later forecast is EditMode 15,432 total / 15,162 passed / 270 skipped / 0 failed; PlayMode 1,181 / 1,012 / 169 / 0. A adds no executable test. C's forecast must add the final new case names to the then-settled main names rather than guessing a total now.

## Decisions for the plate review

The standing leans support the mesh route, the existing sprite profile (D2), and exclusion of the casting hull's own id without deck self-shadow (D4). B must show whether the one-draw cached proxy merits its configuration memory compared with the cheaper screen quad. Lean: keep the geometric route if its masks, bounds and measured cost hold; report separately any demand to include articulated fittings. The reference sun discrepancy is a look finding, not authority to change time or lighting. None of these decisions prevents the Phase A note; production remains for the later paste.
