# Water roadmap: HDRP study and the next ten pieces of water work (2026-11-22)

The owner, 2026-11-22: "i want to continue with water work to get to unity hdrp and other missing
water features".

Base: `main` at `f1b4ab2f59`. No editor was run for this study. Every "ours today" cell comes from
reading source, the merge history and the water register, not from a new picture. The HDRP cells
come from Unity's own documentation, linked in §7. Shader files under `Assets/_Project/Art/Shaders` are named here without their project prefix. This document updates the earlier gap table
(`Evidence~/water-hdrp/gap-table.md`, 2026-11-11, PR 1084). Most of that table's top ranks have
shipped since then.

---

## The short version (for the owner)

- **Do not switch the game to HDRP.** Unity's HDRP cannot draw this game. It has no sprites, no
  tilemaps, no 2D lights and no pixel-perfect camera. It does not support an orthographic camera for
  lit materials, which is how a ¾ top-down pixel game is drawn. It does not run on phones, which
  ends the later mobile port (ADR 0005). A switch would mean rebuilding every custom shader, every
  scene's lighting and the camera before any water work could start.
- **Our water already does more than HDRP's in several ways.** HDRP's own manual says its water has
  no breaking waves at the shore and only monochrome foam. Ours has breaking surf, coloured foam,
  waves that turn to meet each beach, wakes that other boats ride, and boats that ride exactly the
  waves you see.
- **What HDRP's water does better is mostly "the sea reads as a lit 3D body"**: the swell shows as
  shape in daylight, the sun glows through a wave's body at a low sun, a calm surface is a clean
  mirror, and a painted current bends the surface around headlands. Each of these can be built in
  our current water, one piece at a time.
- **Of the extra things you asked about:** still pools, one foam, wakes and oar splashes, whitecaps
  near shore, the dark sea in a blow, caustics, colour by depth and rain rings are **done**. The
  repeating St Peters shoreline, the calm-water stripes, the breaking wave that travels up the beach,
  visible currents, flowing brooks and wet sand that dries are **not done yet**. Ice belongs to a
  later phase.
- **Recommended next:** fix the St Peters shoreline repeat, then the calm-water mirror, then the
  travelling breaker, then the swell that reads in daylight. That keeps the order you agreed on
  2026-11-20. The full ranked list of ten is in §5.

What we would give up by not switching: HDRP's ready-made physically lit 3D water (screen-space and
ray-traced reflections, light shafts, fine close-up surface detail) and its ready-made editor tools.
Each feature we want has to be built and tuned by hand in our own water shader. That shader is
already about 7,700 lines, so every feature added to it is more upkeep.

---

## 1. What Unity's HDRP Water System offers (Unity 6, HDRP 17.4 / 17.5 docs)

The project pins URP `17.5.0` (`Packages/manifest.json`), so the matching HDRP line is 17.5. The
capabilities page was read at 17.5. The other pages were read at 17.4, the newest set the searches
returned. Links are in §7.

| HDRP feature | What it does (from the docs) |
|---|---|
| Surface types | Pool, River, and Ocean/Sea/Lake. Several surfaces can exist in one scene, and they can be synchronised. |
| Simulation bands | Ocean/Sea/Lake has two swell bands and one ripple band. River has an agitation band and a ripple band. Pool has ripples only. Swell follows distant wind; ripples follow local wind. Compute-shader simulation. |
| Simulation foam | Wind-driven whitecaps over the whole surface. Amount, smoothness, a mask, and a dimmer by distant wind speed. **Monochrome.** |
| Local foam / foam generators | Foam written locally, for example trailing a moving floating object. Foam can be added by script. |
| Water decals and deformers | A Water Decal with a "Deformer and Foam" shader graph template. Types: Box, Bow Wave (parent it to a boat), Shore Wave (wavelength, skipped waves, speed, a breaking range with foam dimmers), Texture (an amplitude texture, render textures allowed). Horizontal deformation is optional and costs an extra buffer. The deformation area is limited to the surface's area size, so a script must move it to follow a boat. |
| Water excluders | A mesh that hides the water surface inside a hull or room. Seen from above only. A simulation mask is advised for static terrain. |
| Current maps | A texture: red and green hold the direction, blue holds the influence. It moves swell or agitation and ripples. Floating objects follow the current only through your own script. Caustics ignore current maps and masks. |
| Simulation masks | Red attenuates the swell bands, green attenuates ripples. Clamp wrap for oceans. |
| Caustics | Generated from a chosen band (ripples by default) on a virtual plane, with a resolution setting. They can be projected onto objects above water with a decal projector and a script that reads `GetCausticsBuffer`. **Monochrome.** No caustics on transparent objects seen from underwater. |
| Underwater view | A full-screen effect below the surface, with analytic absorption or volumetric light shafts when volumetric fog is on, plus a waterline where the camera crosses the surface. |
| Material: scattering and absorption | Scattering colour terms (raising them makes water brighter and more opaque), an absorption distance (how far you see in), refraction maximum distance, smoothness close and distant, and a "direct light tip term" for light at wave tips (oceans only). Customisable through the Water ShaderGraph. |
| Height query (buoyancy) | `WaterSurface.ProjectPointOnWaterSurface(WaterSearchParameters, out WaterSearchResult)` projects a point onto the surface, with a Burst job version for many points. Needs Script Interactions on in the HDRP asset and on the surface. The 16.0 docs warn that masks do not affect the CPU simulation, so buoyancy is wrong on masked surfaces. |
| VFX Graph interaction | VFX Graph can sample the water. |
| Debug modes, volume override | Debug views, and a Water volume override for the rendering settings. |
| Stated limits | **No breaking waves on shorelines.** No side views, no spherical or vertical water. Foam and caustics are monochrome. No baked lighting on water. No motion vectors, so TAA and DLSS ghost. **MSAA is incompatible.** Water cannot appear in ray-traced reflections. Ordering problems behind other refractive transparents. Limited decal support. |

**What HDRP does not have that this game uses** (Unity 6.3 feature comparison and HDRP camera docs):

| Used by this game | URP | HDRP |
|---|---|---|
| Sprites, Tilemap, Sprite Shape | Yes | **No** |
| 2D Lights and 2D Shadows (the 2D Renderer) | Yes | **No** |
| Pixel Perfect camera | Yes | **No** |
| Shader Graph Sprite Lit / Custom Lit targets | Yes | **No** |
| Orthographic camera with lit materials | Yes | **No**: "Currently, HDRP doesn't support this projection mode." Lit features "don't work consistently". Only unlit materials work. |
| iOS, Android, WebGL (ADR 0005's later port) | Yes | **No**. HDRP needs compute shaders and DX11/12, Metal or Vulkan, and does not support OpenGL ES. |

**What URP has for water in Unity 6.x:** nothing built in. Every "Water system" row on the Unity 6.3
feature comparison reads "No" for URP. Unity's old Boat Attack demo had a URP water, but it is not an
officially supported package. URP water otherwise comes from the Asset Store. So there is no URP
water to adopt. The game's own water (ADR 0010 onward) is the URP water.

---

## 2. Feature by feature: HDRP against the game's water today

Sizes: **S** is about one pull request, **M** two or three, **L** a series. "Value" means what a
player would see at our ¾ top-down pixel scale.

| HDRP feature | Our water today (file) | Gap | Build it in our 2D URP water? | Player-visible value here |
|---|---|---|---|---|
| Swell + ripple bands | An 8-train wave spectrum shared by the drawn sea and the ridden sea (`Core/Environment/WaveMath.cs`, `WaveSpectrum.cs`, `Art/WaveFieldBridge.cs`). Speed by wavelength (`WaterDispersion.cs`), a capillary ripple band (`WaterRipple.cs`), a fetch law for young seas in the lee (`WaveFetch.cs`), sea memory (`WaveMemory.cs`), and a displaced surface (`DisplacedWaterSurface.cs`, ADR 0023). Waves turn along the seabed to meet each beach (`WaveBathymetry.cs`, PR 1357). | Physics: none. **Legibility:** the swell is hard to see as shape in daylight. The bounded daylight swell read ships off (`docs/design/water-daylight-swell-read.md`, register row 6 / W1). | Yes. Turn on and tune the existing swell read and face shade with plates. No new layer. **S** | **High.** This is the main thing that makes HDRP water look "real": you see the swell. |
| Simulation foam (whitecaps) | Whitecaps with onset, clumping, streaks and age colour (`_Whitecap*`, `Art/WaterFoam.cs`), convergence lines (`WaterDriftLines.cs`). They turn and fade near shore (`WhitecapShoreMath.cs`, PR 1364). All on one foam sheet (PR 1336). **Coloured**, where HDRP is monochrome. | Small: chunky whitecap ends and a weak beach turn are logged follow-ups (owner, 2026-11-21: "good enough to merge for now"). Register row 7 (dark shards in a gale) and row 24 (caps jump the age ramp) are open. | Yes. Polish the existing terms. **S** | Medium. |
| Local foam / foam generators | An advected foam buffer (`Art/FoamBuffer.cs`, `FoamBufferAdvect.shader`). Injection from the whole hull by local speed (PR 1098), from oars (PRs 1184, 1300), from hull contact (`HullContactFoam.cs`), and transported by a flow field (`FoamTransportField.cs`). One foam for wake, oars, surf, shore, caps and streaks (PR 1336). | None that a player would notice. The 8-bit decay stall was fixed in PR 756. | Done. | Already delivered. |
| Deformers: bow wave, box, texture | Real hull waves: rings from every stern and bow (`Core/Environment/HullWaveField.cs`, `HullWaveMath.cs`, PR 1302). A V wake for motored hulls, arcs for rowed ones (PR 1375). A bow mound ahead of a hull making way (PR 1157, **ships off**). | The bow mound is built but off. There is no general "authored deformer" yet, for example standing waves over a ledge or a tide rip at a channel mouth. | Yes. Turn on the bow mound with a look pass (**S**). Authored deformers could be a stamp into the existing hull-wave or foam injection seams (**M**). | Medium. |
| Shore wave deformer | Breaking surf with lip, barrel and run-up from the wave field, the painted depth and the tide (`Core/Environment/BreakerMath.cs`, ADR 0040, `CurlingBreaker*`, `CurlingBreaker.shader`). HDRP says its water has none of this. | The breaking wave as **one travelling event** is off: `_SurfFringeSequenceStrength` is 0 on all nine water materials, and 13 GPU-only `SurfSequenceTests` are red and not yet explained. | Yes. Diagnose the test rig first, then make plates and get the owner's on/off. **M to L** | **High** at every beach. |
| Excluders | An interior mask keeps the sea out of hulls (ADR 0023). No sea inside the dory, punt, console skiff or sport skiff (PRs 1326, 1331). | None. | Done. | Already delivered. |
| Current maps | One flow vector per sea (`_Flow`, `_FlowDir`), driven from the deterministic tidal current (`Environment/CurrentModel.cs` through `Art/WaterSurface.cs`). Foam rides a transport field. | No painted or derived current map. Surface detail does not bend around headlands, there are no tide rips, and brooks do not run downhill. | Yes, as a render-side flow map **derived from the sim's own current**, so what you see is what the boat feels (rule 5, ADR 0018's "one source"). **M to L.** Making the current itself richer is sim work for the gameplay team. | Medium to high in channels and creeks. This is how you read set and drift (P1). |
| Simulation masks | Fetch and shelter (`WaveFetch.cs`, `SwellShelterSettings.cs`). Sea reach: cut-off pools stay still (`Core/Environment/SeaReach.cs`, `Include/SeaReach.hlsl`, PR 1348). Still water above the tide (ADR 0046, `Include/StillWater.hlsl`). | None. | Done. | Already delivered. |
| Caustics | Caustics from the wave curvature on the floor (`Include/CausticPattern.hlsl`, `Art/CausticMath.cs`), gated by daylight, plus caustics on hulls at the waterline (`HullCaustics.hlsl`, `HullCausticLighting.cs`). **Coloured.** | A hull over clear sand casts no shadow on the floor or into the caustics. Read from greps only, not from a picture. | Yes. Stamp the hull footprint, offset by sun bearing and depth, into a term that dims the floor light. **M** | Low to medium. Only in clear shallows. |
| Underwater view | Not applicable to a fixed top-down camera. What lies under the surface is drawn through the water column: the seabed (W4-4), kelp (PR 1312), submerged props (`Art/UnderWaterRegistry.cs`), the wading or swimming player (`PlayerSubmerge.shader`, wading line PR 1341). | None for this camera. | Not needed. | n/a |
| Scattering and absorption colour | Per-channel Beer-Lambert absorption over a baked seabed colour (`Art/WaterAbsorption.cs`, `SeabedBake.cs`, `SeabedPalette.cs`), turbidity per water type (`WaterType.cs`), a weather-driven palette (ADR 0017), a palette guard rail (ADR 0015), a crest glow at low sun (`_CrestGlow*`, the "tip" term) and a sun side (`_SunSideStrength`). The sea at sunset is lit by the sky (PR 1396). | No **body** term: a swell face toward a low sun does not glow through, which HDRP's scattering does. | Yes. A body term on the existing swell slope, behind a dial at 0 and capped by the guard rail. **S** | Medium at golden hour and dusk, which is the public sunset clip. |
| Reflections (SSR, probes, ray tracing) | Sky, cloud, sun streak, moon glitter, stars (`Art/WaterReflection.cs`, `Include/ReflectMirror.hlsl`). Object reflections with warp and sink (`WaterReflectionWarp.cs`, `ReflectiveObject.cs`). Mesh hull reflections on by default (`HullMeshReflection.cs`, PR 1370, streak fade PR 1399). | The calm mirror is striped (register row 5). The mirror fades with wind, not with light (row 13). Reflection ends are ragged under square sterns (draft PR 1449, open). The moon's glitter ignores cloud (row 22; there is no cloud-cover axis in the sim, row 23). | Yes for rows 5 and 13 (**M**). Row 22 needs a cloud axis in the sim first (**M to L**, gameplay team). | **High** at glass calm and at night. "Glass calm is sacred" is the owner's own phrase. |
| Height query for buoyancy | Boats ride the same deterministic field the shader draws (`Boats/BoatWaveMotion.cs`, `SeakeepingForcesMath.cs`), and they rise on other hulls' waves (`BoatWaveMotion.cs` line 495, `HullWaveRideMeters`). Buoys use the same field (`BuoyWaveMath.cs`). | **We are ahead.** HDRP's query is a GPU/CPU twin that masks break. Ours is a function of `(worldSeed, gameTime)`. | Done. | Already delivered. |
| Water decals | Painted water slots and per-region water types (ADR 0010 §10). Floating weed and flotsam (`SeaweedPresenter.cs`). | None. | Done. | Already delivered. |
| VFX Graph interaction | Splashes and spray as pooled particle systems: wading (`WadeSplashEmitter.cs`), oars (`DoryOarSplashSystem.cs`), wake (`WakeParticleSystem.cs`, `WakeBubbleSystem.cs`), gulls (`SeagullSplashMath.cs`), traps (`TrapSplashFx.cs`). | None. | Done. | Already delivered. |

---

## 3. The honest cost of switching the project to HDRP

**What would have to be rebuilt:**

- **Every sprite, tilemap and sprite shape.** HDRP lists all three as unsupported. The ten scenes,
  their tilemaps and every prefab built from sprites would need a new representation, either 3D
  quads with HDRP materials or something custom.
- **All lighting.** The 2D Renderer, 2D lights and 2D shadows do not exist in HDRP. That covers the
  24-hour day/night overlay (ADR 0013), the additive night lights and the boat spotlight (ADR 0016),
  the lamp pools and lamp shadows, and both sprite shadow systems.
- **All 28 custom shaders.** All 28 include URP's package and 21 have a Universal2D pass. Together
  they are about 17,600 lines, with the water shader alone about 7,700. That includes the facet,
  hull, cliff, terrain, tree, grass and reflection shaders. None of them carry over.
- **The camera.** The pixel-perfect camera is URP-only. HDRP does not support orthographic projection
  for lit materials. The ¾ pixel look would have to be faked with a narrow-field perspective camera,
  rendered to a small target and scaled up. That gives up the locked pixel grid (`assetsPPU`, ADR
  0027 revision) that a large share of the water work is measured against.
- **The world's axes.** As a 2D Unity project, the world is laid out in X/Y (tilemaps, Physics2D).
  HDRP's water is a horizontal surface with Y up. Either the whole world rotates, or the water lives
  in a different frame from everything it touches.
- **CI.** CI has no GPU, and the water already has GPU-only tests that CI cannot run (the 13
  `SurfSequenceTests`). HDRP's water simulation is compute-shader based, so even more of the water
  would be untestable in CI.
- **The mobile port.** HDRP does not run on iOS or Android. ADR 0005 keeps mobile as a viable later
  port. A switch would have to supersede that ADR and drop the port.

**Risk:** very high. A pipeline switch would freeze the art, rig and water work for months. It would
throw away the measured water arc (the register, the plates and the fixture laws). And it would land
on a renderer that, by Unity's own stated limits, has no shore breaking waves and no coloured foam,
both of which this game already ships.

**Can HDRP water even be shown at a fixed ¾ top-down pixel grid?** Not as intended. Its lighting
needs a perspective camera. Its look comes from smoothness, refraction and grazing-angle scattering,
which mostly disappear when looking nearly straight down. Whatever survived pixelation would be
lighting we can reproduce in our own shader.

**Recommendation:** stay on 2D URP, and build the HDRP-class features that are visible from above
inside the current water. That is the swell read, the body glow at low sun, a clean calm mirror,
visible currents and authored deformers. The game would lose HDRP's off-the-shelf 3D lighting
(screen-space and ray-traced reflections, volumetric light shafts, tessellated close-up detail) and
its editor tooling. Those do not show at this camera, and every feature we add has to be built and
measured by hand. No ADR is drafted, because the evidence does not favour a switch.

One cheap way to still borrow from HDRP: set up a separate, throwaway HDRP sandbox project, outside
the game repo, to shoot reference pictures of particular looks (calm mirror, low-sun body glow,
current-bent ripples). Those pictures would be targets only, and nothing from that project ships.
Size **S**. It is optional and an owner call.

---

## 4. Other missing water features: status

| Feature | Status | Evidence |
|---|---|---|
| Water cut off from the sea stays still (owner 2026-11-20) | **Done** | PR 1348: `Core/Environment/SeaReach.cs`, `Include/SeaReach.hlsl`, `Art/SeaReachGlobals.cs`, tests in `SeaReachTests.cs`. The planning draft PR 1340 is still open and can likely be closed. |
| Shoreline bank repeats one scallop pattern (owner 2026-11-20, St Peters, right bank in the dusk shots) | **Missing** | No merged fix found since 2026-11-20. PR 1355 fixed a repeating notch on Rice Point's crib faces, which is a different shore. The pale wedge in the same shot (a stray displaced-water tile) is also still owed. |
| One foam for the whole game (owner 2026-11-19) | **Done** | PR 1336 (one sheet for wake, oars, surf, shore, caps and streaks), including the shore foam line unified with the wake foam. |
| Wake and oar splashes | **Done**, with polish owed | PR 1302 (real wake waves), 1368 (oar rings and a firmer splash), 1375 (V wake for motored hulls, dusk contrast "good as is"), 1300 and 1184 (oar puddles and foam). The public rowing clip reshoot is still owed after the 2026-11-21 fixes. |
| Whitecaps near shore | **Done** ("good enough for now") | PR 1364, `Art/WhitecapShoreMath.cs`. Logged follow-ups: chunky whitecap ends, weak beach turn. |
| Dark sea in a blow | **Done** ("good enough for now") | PR 1361 (relative swell read, storm floor 0.16). Logged follow-up: a GPU-only shore swirl band test. |
| Reflections | **Partly** | Done: PR 1370 (hulls reflect by default), PR 1399 (streak fade), sky, moon and stars. Open: draft PR 1449 (ragged ends), register rows 5 (striped calm mirror), 13 (mirror fades with wind, not light) and 22 (moon ignores cloud). |
| Caustics | **Done**, one gap | `Include/CausticPattern.hlsl`, `Art/CausticMath.cs`, hull caustics. Gap: no hull shadow on the floor or in the caustics (from greps only). |
| Shallows and colour by depth | **Done** | `Art/WaterAbsorption.cs`, the baked seabed colour, the floor seen through the shallows (W4-4), kelp under the water (PR 1312). The deep/shallow "wall" (register rows 9 and 10) was answered in PR 780. |
| Rain on water | **Done**, not judged in a picture | `_RainRing*` in `Water.shader`, `Art/WaterRainSignal.cs`, `Art/RainEmitter.cs`. Register row 19 notes that no plate sweep has a rain column yet. |
| Boat wakes interacting | **Done** | `Core/Environment/HullWaveField.cs` sums every hull's rings. `Boats/BoatWaveMotion.cs` makes a boat rise and fall on other hulls' wakes (register row 27's ask). |
| Tide flats wetting and drying | **Partly** | A fixed wet band of 0.45 m above the live waterline (`_WetBandMetres` and `_WetStrength` in `TerrainSplat.shader`). Sand the tide has just left does not stay dark and then dry over time. The dig hole now darkens and lasts until the tide (PR 1393). |
| Currents shown on the surface | **Partly** | One scroll vector per sea (`_Flow`, `_FlowDir`) from `Environment/CurrentModel.cs`. No tide rips, no flow that follows a channel. |
| River and creek flow | **Missing** (visually) | Brooks are still water at a fixed level (ADR 0046, `Include/StillWater.hlsl`). Nothing draws them running downhill. `World/TerrainPlan/WaterfallDef.cs` shapes channel beds only. |
| Ice | **Missing**, later phase | No ice in code. `docs/roadmap.md` places "winter freezing in some regions" in the later weather and winter wave, so it stays out of scope for now (rule 8). |
| Breaking wave that travels up the beach | **Missing** (built, switched off) | `_SurfFringeSequenceStrength` is 0 on all nine water materials. 13 GPU-only `SurfSequenceTests` are red and undiagnosed. |
| Bow mound ahead of a moving hull | **Built, off** | PR 1157 ("ships off"). |
| Swell readable in daylight | **Built, off** | `docs/design/water-daylight-swell-read.md` (W1, default off, plates pending). |
| Glow through a wave's body at low sun | **Missing** | Only the crest-top glow and the sun side exist. |

---

## 5. The ranked roadmap: the next ten pieces of water work

The order starts from the owner's own agreed order of 2026-11-20 (St Peters shoreline, waves to
every beach, dark sea, travelling breaker, calm-mirror stripes). Waves to every beach and the dark
sea have shipped. The HDRP-class looks follow, ranked by what a player sees per unit of work. Every
piece shows the owner before and after pictures before it merges.

| Rank | Work | Size | What the owner would see | Pillar | Owner decision? |
|---|---|---|---|---|---|
| 1 | **St Peters shoreline without the repeat**: break the fixed-pitch scallop on the right bank, remove the pale wedge, and check the other regions' banks for the same repeat | S to M | A natural, varied bank at noon and dusk, with no visible tiling | P1, P3 | No (the look is shown before merge) |
| 2 | **A clean calm mirror** (register rows 5 and 13): remove the ~1.6 m stripes, and let the mirror fade with light as well as wind | M | Glass-calm mornings with a true, unstriped reflection of sky and boats | P1 | Yes: the look of the mirror |
| 3 | **The travelling breaker**: diagnose the 13 GPU-only red tests, then turn the one-travelling-wave surf on with plates | M to L | Each wave breaks and runs up the beach as one event, instead of boiling in place | P1, P5 | Yes: on or off after the plates |
| 4 | **The swell reads in daylight** (W1, already built and off): turn it on and tune it with plates | S | Open-water swell visible as rolling shape at noon, the main thing HDRP water shows | P1 | Yes: the strength |
| 5 | **Low-sun glow through the wave body** (HDRP's scattering, behind a dial at 0) | S | At golden hour and sunset, wave faces toward the sun glow warm through, which helps the public sunset clip | P1 | Yes: the strength |
| 6 | **Bow mound on and wake polish**: turn on PR 1157's bow mound, merge the ragged-reflection fix (PR 1449), then reshoot the rowing clip | S | A pushed hump of water ahead of a hull making way, clean hull reflections, and the new public clip | P1, P2 | Yes: the bow mound look; the clip is his to approve |
| 7 | **A current you can see**: a render-side flow map derived from the tidal current, with ripples and foam bent around headlands and a tide rip at channel mouths | M to L | Water visibly running through channels on the flood and ebb, so you can read set and drift | P1, P5 | Yes: whether the sim's current gets richer (gameplay team), or the view only shows today's current |
| 8 | **Wet sand that dries**: the band the falling tide leaves stays dark, then dries over a tunable time, recomputed from tide history rather than saved | M | A dark, glistening flat right after the tide drops, fading pale over the hour | P1, P3 | Small: the drying time |
| 9 | **Brooks that run**: a flow direction for still-water brooks so their surface and foam move downhill (render only, the level stays still per ADR 0046) | M | Creeks visibly flowing to the sea at Nine Mile Creek and St Peters | P3, P1 | Yes: confirm brooks should flow (ADR 0046 drew them still) |
| 10 | **Hull shadow on the clear-water floor and in the caustics** | M | A boat over clear sand casts a soft shadow on the bottom, and the caustics dim under it | P1 | No (check first with whoever owns shadow work) |

**Logged, not ranked:** authored deformers (standing waves over a ledge), which could follow rank 7
on the same seam. The moon and sun glitter respecting cloud (register rows 22 and 23) needs a
cloud-cover axis in the sim first. Sea ice is later phase. The HDRP reference sandbox (§3) is an
optional owner call.

---

## 6. What this study did not do

- No editor was run, no picture was shot and no frame time was measured. Each piece of work in §5
  carries its own before and after pictures and a frame-time pair.
- The St Peters shoreline status rests on the merge log since 2026-11-20 and the open PR list, not on
  a picture. Another unmerged branch might already be working on it.
- The hull shadow gap and the absence of brook flow come from greps of the water shader and `Code/`,
  not a full read.
- The orthographic limit is quoted from the HDRP camera reference (17.4), not tested. I found no
  Unity converter from URP to HDRP, but I did not confirm whether one exists.
- The register's row statuses were read by row head. Rows 9 and 10 being answered relies on PR 780
  as recorded in the history.

---

## 7. Sources

Unity documentation, read 2026-11-22:

- HDRP 17.5, Capabilities of the water system:
  https://docs.unity3d.com/Packages/com.unity.render-pipelines.high-definition@17.5/manual/water-capabilities-of-the-water-system.html
- HDRP 17.4, Water section index:
  https://docs.unity3d.com/Packages/com.unity.render-pipelines.high-definition@17.4/manual/water.html
- HDRP 17.4, Decals and masking in the water system:
  https://docs.unity3d.com/Packages/com.unity.render-pipelines.high-definition@17.4/manual/water-decals-and-masking-in-the-water-system.html
- HDRP 17.4, Foam in the water system:
  https://docs.unity3d.com/Packages/com.unity.render-pipelines.high-definition@17.4/manual/water-foam-in-the-water-system.html
- HDRP 17.4, Deform a water surface:
  https://docs.unity3d.com/Packages/com.unity.render-pipelines.high-definition@17.4/manual/water-deform-a-water-surface.html
- HDRP 17.4, Exclude part of a water surface:
  https://docs.unity3d.com/Packages/com.unity.render-pipelines.high-definition@17.4/manual/water-exclude-part-of-the-water-surface.html
- HDRP 17.4, Create a current in the water system:
  https://docs.unity3d.com/Packages/com.unity.render-pipelines.high-definition@17.4/manual/water-create-a-current-in-the-water-system.html
- HDRP 17.4, Underwater view:
  https://docs.unity3d.com/Packages/com.unity.render-pipelines.high-definition@17.4/manual/water-underwater-view.html
- HDRP 17.4, Materials in the water system:
  https://docs.unity3d.com/Packages/com.unity.render-pipelines.high-definition@17.4/manual/water-materials-in-the-water-system.html
- HDRP 17.4, Customize caustics:
  https://docs.unity3d.com/Packages/com.unity.render-pipelines.high-definition@17.4/manual/water-caustics-in-the-water-system.html
- HDRP 17.4, Quality and performance decisions:
  https://docs.unity3d.com/Packages/com.unity.render-pipelines.high-definition@17.4/manual/water-quality-and-performance-decisions.html
- HDRP 17.0, Scripting in the water system (height query):
  https://docs.unity3d.com/Packages/com.unity.render-pipelines.high-definition@17.0/manual/water-scripting-in-the-water-system.html
  and `WaterSearchParameters`:
  https://docs.unity3d.com/Packages/com.unity.render-pipelines.high-definition@17.0/api/UnityEngine.Rendering.HighDefinition.WaterSearchParameters.html
- HDRP 17.4, Camera component reference (orthographic not supported):
  https://docs.unity3d.com/Packages/com.unity.render-pipelines.high-definition@17.4/manual/hdrp-camera-component-reference.html
- HDRP 17.4, System requirements:
  https://docs.unity3d.com/Packages/com.unity.render-pipelines.high-definition@17.4/manual/System-Requirements.html
- Unity 6.3 manual, Render pipeline feature comparison (Sprite/Tilemap/2D Lights/Pixel Perfect/Water/mobile rows):
  https://docs.unity.com/en-us/engine/6000.3/manual/render-pipelines/choose-a-render-pipeline/feature-comparison
- Unity 6.5 manual, Pixel Perfect Camera reference (URP 2D):
  https://docs.unity3d.com/6000.5/Documentation/Manual/urp/2d-pixelperfect-ref.html

Repo sources: `docs/adr/0005`, `0010`, `0012`, `0013`, `0015` to `0018`, `0023`, `0027`, `0040`,
`0046`, `0054`; `docs/design/water-fidelity-register.md`; `docs/design/water-daylight-swell-read.md`;
`Evidence~/water-hdrp/gap-table.md`; the files named in the tables above.
