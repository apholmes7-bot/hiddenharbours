# The water against Unity's HDRP water: the gap table (Phase A, 2026-11-11)

The owner, 11-11: "lets continue water work to get it like unity hdrp". The game stays 2D URP,
three-quarter top-down, pixel art (ADR 0005). This table lists what HDRP's water gives a viewer, what
ours does today, how far apart they are as a player would see it, and the cheapest way to close each
gap inside our pipeline and frame budget. It then ranks the gaps, with the open lines of the water
road in the same ranking.

Base: main `d9d4b855`. No editor was used for this table: every "ours today" cell is read from source,
not from a plate. Cells marked (unread) were not opened and are a guess from names.

## 1. What the reference gives a viewer

Read from Unity's HDRP 17.0 manual (water system pages: simulation, settings and properties, foam,
caustics, current, deformers, excluders, underwater, quality, capabilities and limitations). The
scripting page returned 404 at 17.0 and was not read; the foam sub-pages and the sphere deformer page
were not fetched.

| # | HDRP feature | What a viewer gets |
|---|---|---|
| H1 | Simulation bands | Ocean: two swell bands and one ripple band. Swell follows the distant wind speed and orientation with a chaos value; ripples follow the local wind. Per-band amplitude dimmer and distance fade. |
| H2 | Current map | A texture (direction in R/G, influence in B) that carries swell, ripples and simulation foam along a painted flow. It does not move caustics. |
| H3 | Simulation foam | Whitecaps by wind speed (a dimmer curve), an amount, a tiling texture, a mask, and a current influence. Monochrome. |
| H4 | Foam and deformation decals | Local foam and height written into a region around the camera by placed or moving decals: the wake of an object, a bow wave ("Bow Wave Elevation"), a box, a texture. Foam has a persistence multiplier. |
| H5 | Shore wave deformer | A hand-placed box that sends waves along its local X with a wavelength, speed, skipped-wave share, a breaking range that makes surface foam and loses 70% of amplitude, and a deep-foam range. |
| H6 | Refraction and absorption | Refraction colour, a maximum distance (more distance, more distortion of what is below), an absorption distance. |
| H7 | Scattering | A scattering colour with ambient, height, displacement, direct-light tip and direct-light body terms: lit crests and a glowing wave body at grazing sun. |
| H8 | Caustics | Caustics from the ripple or swell band on a virtual plane, an intensity, and shadows from the directional light. A buffer to project caustics on objects above water. |
| H9 | Reflections | Sky and scene reflection through smoothness (screen-space or ray-traced). No dedicated property block. |
| H10 | Excluders | A mesh that removes the surface inside a hull or a room. |
| H11 | Underwater view | A full-screen absorption effect below the surface and a waterline. |
| H12 | Script queries | Height and current queries so objects float on the drawn surface. |
| H13 | Stated limits | No breaking waves. No side views. Foam and caustics are monochrome. |

## 2. Ours against it

| # | Ours today (file) | Distance a player sees | Cheapest close in 2D URP | Cost | Risk |
|---|---|---|---|---|---|
| H1 | Ahead on physics, behind on legibility. An 8-train spectrum shared by the drawn sea and the ridden sea (`WaveMath`, `WaveFieldBridge`), wavelength and speed by dispersion (`WaterDispersion.cs`), wind chop, a cross swell, a capillary ripple band gated by wind (`WaterRipple.cs`), a displaced mesh (`DisplacedWaterSurface.cs`). But the swell is hard to read in daylight (register rows 6 and 25: wet luma 0.06 to 0.07 in a blow) and the daylight swell read ships off (road line 5). | Medium. In HDRP you always see the swell as form. In ours, in a working blow, you see foam on dark water. | Road line 5: the swell read and face shade, behind its existing material values, with plates for the owner's ruling. No new layer. | Frame: none new (terms exist). Work: one slot of plates. | Look ruling is the owner's (register says ask, never choose). |
| H2 | One flow vector per sea (`_Flow`, `_FlowDir`) from the tidal current; foam is carried by a published transport map (`FoamTransportField.cs`). No painted current that bends the surface detail round a headland. | Small in open water, medium in a channel mouth. | Reuse the foam transport map to drift the chop and ripple phase. | Frame: one more sample in the surface stage. Work: medium. | High: a current a boat does not feel is a lie, and a current a boat feels is sim (gameplay-systems, world-content data). Not tonight. |
| H3 | Ahead. Whitecaps with onset, formation, collapse, clumping, wind streaks, age colour (`_Whitecap*`, `_Foam*`, `WaterFoam.cs`), convergence lines (`WaterDriftLines.cs`). Coloured, not monochrome. Register row 7 (dark shards in a gale) is open and a look call. | Small. | Register row 7 when the owner rules. | Small. | Look ruling. |
| H4 | Half. An advected foam buffer with injection by hull motion and by heave (`FoamBuffer.cs`, `FoamInjector.cs`, `FoamInjectionRegistry.cs`), a stern trail and a Kelvin lift train drawn in the displaced mesh (`WakeLiftMath.cs`, #837), bow spray and droplets (`BoatWakeEmitter.cs`). The owner, 11-02: foam "just comes from stern from moving forward and looks off when going in reverse". | Large, and the owner named it. HDRP's decals make water wherever the object meets it. | Road line 2b: inject foam along the whole waterline by the local velocity of each hull station through the water (bow when ahead, transom when astern, the outside of a turn, both sides when she slides), through the existing injection seam. | Frame: a few more quads into a buffer pass that already runs; expect well under 0.1 ms. Work: medium, one PR for foam, one for the lift. | Touches `BoatWakeEmitter.cs` if done there (the road says wait for Boat Drive PR 1, which is not on main). Lean: build in `Code/Art` on the injector, leave the emitter alone. |
| H5 | Ahead on main where surf exists (breaking, lip, barrel, run-up: HDRP states it has none), behind on direction: waves come from one screen direction, not in to each beach (owner playtest 10-09). Built and unpushed: `feat/water-w4-6b-waves-follow-the-seabed` (a refraction grid in Core, shared by the shader, the bore and the boat). | Large. Every beach in the game. | Road line 3: bring the branch onto main (one conflict, `SurfSequenceTests.cs`), run its native proof, push. | Frame: its own report must give the number (CPU/GPU agreement and budget are in its proof list). Work: a merge and one 140-minute slot. | It changes what a boat rides near shore (by design, same seam). Its 26 of 29 "no added arrival" rows are unexplained. It moves what `BreakerSurfDriftPlayTests` (PR 1079) and the 13 GPU-only `SurfSequenceTests` measure: numbers to the seat before push. |
| H5b | The breaking wave as one travelling event: `_SurfFringeSequenceStrength` ships at 0 on all nine materials; 9 `SurfSequenceTests` red with a GPU, cause not diagnosed (road line 4). | Large at a beach (register row 1, the charter's core). | Diagnose the shared rig first (is the measure broken or the surf), then plates, then the owner's on or off. | Frame: none new at 0. Work: unknown until diagnosed. | Must come after H5: the bore's direction changes under it. |
| H6 | Present. Per-channel Beer-Lambert absorption on a baked seabed colour (`WaterAbsorption.cs`, `_SeabedTex`), a painted depth ramp, turbidity per water type, and the floor bent by the surface slope (`_ShallowsFloorRefraction`, shader line 2382). Props under the surface read through the same column (`UnderWaterRegistry.cs`). Register row 10: the deep/shallow boundary is a wall. | Small to medium (row 10). | Register row 10. | Small. | Seabed textures are another lane's tonight (St Peters): do not move what they measure. |
| H7 | Half. The thin top of a crest glows at low sun (#1004, `_CrestGlow*`, three Defs), a sun side exists (`_SunSideStrength`). No body term: the face of a swell toward a low sun does not lighten through. | Medium at golden hour, none at noon. | A body term on the existing swell slope, capped by the palette guard rail, behind a material value at 0. | Frame: arithmetic on values already computed. Work: small, after line 5 (same darkness bill). | Palette guard rail (ADR 0015). Look ruling. |
| H8 | Present. Caustics on the floor from the wave curvature (`CausticPattern.hlsl`, `CausticMath.cs`, `_CausticCurvature*`), and on hulls at the waterline (#1030). Not found in the water shader: a hull's shadow in the caustics or on the floor (unread beyond a grep). | Small to medium in clear shallows: a boat over sand has no shadow under her. | Stamp hull footprints, offset by sun bearing and depth, into a term that dims floor light and caustics. | Frame: one low-resolution target or a reuse of the foam injection pass. Work: medium. | Overlaps the shadow overhaul (#1005 line). Ask that lane before building. |
| H9 | Ahead for the style. Sky, cloud, sun streak, moon glitter, stars, and object reflections with warp and sink (`_Reflection*`, `_ObjectReflect*`, `WaterReflectionWarp.cs`). Register rows 5 and 13 are open (the calm mirror is striped; the mirror fades with wind, not light). | Medium at glass calm (row 5, "sacred"). | Register row 5: the owner's look call on the mirror's form. | Small. | Look ruling. |
| H10 | Present. An interior mask keeps the sea out of hulls (shader line 6604, ADR 0023). | None. | Nothing. | None. | None. |
| H11 | Not applicable to a top-down camera. What it would show (things under the surface) is covered by H6. | None. | Nothing. | None. | None. |
| H12 | Ahead. The boat rides the same field the shader draws (`BoatWaveMotion.cs`, the shared sampler), a function of seed and game time. | None. | Nothing. | None. | None. |

## 3. The ranking, by visible change per cost

The open road lines keep their numbers; they are ordered here, not replaced.

| Rank | What the player gets | Road line / HDRP row | Why here |
|---|---|---|---|
| 1 | Waves come in to every beach along the seabed | Road 3 / H5 | The largest visible change, on every shore, and it is already built. Cost is a merge and one slot. Everything about surf after it builds on its direction. |
| 2 | The whole hull makes foam, and reverse reads right | Road 2b / H4 (foam) | The owner asked for it in his own words. The injection seam exists; the change is where and when it injects. No editor needed to build, one slot to prove. |
| 3 | The hull's wake lifts the water from where she pushes it (bow wave ahead, stern wave astern) | Road 2b / H4 (deformation) | Second half of the same ask. After rank 2 so the foam and the lift share one set of stations. |
| 4 | The breaking wave is one travelling event | Road 4 / H5b | Large at a beach, but its cost is unknown until the red rig is diagnosed, and it must sit on rank 1's direction. |
| 5 | The swell reads in daylight; a blow is not black | Road 5 / H1, register rows 6 and 25 | Large in open water, cheap in frame time, but the look is the owner's ruling: ships behind its values with plates. |
| 6 | The swell face lights through at low sun | H7 | Small work on top of rank 5. Behind a value at 0. |
| 7 | A hull casts a shadow on the floor and in the caustics | H8 | Medium work, visible only in clear shallows; needs a word with the shadow lane first. |
| 8 | The calm mirror, the gale shards, the deep/shallow wall, the night surf | Road 6 / register rows 5, 7, 10, 12, 13, 14 | Each is a look ruling the register already frames. Re-read each row before starting it. |
| 9 | A painted current bends the surface | H2 | Visible only in channels, and a current is sim: not a visual lane's to add. Logged, not planned. |

Road line 2 (each sea has its own water colour, the East Water included) reads as landed on main by
#1010; the seat should cross it off after checking the scene line.

The seat's expectation (waves to every beach first, the whole hull second) matches this table.

## 4. What this table did not do

- No plate was shot and no frame time was measured. Each PR down the ranking carries its own
  before and after plates in Nine Mile Creek and St Peters and a frame-time pair.
- The register was read in full for rows 1 to 8 and by row heads for the rest.
- H8's missing hull shadow and H2's single flow vector are read from greps of the water shader and
  `Code/`, not from a full read of either.
