# Living fish: behaviour model

Status: proposed design plus an inactive, engine-light model. Existing fish, sonar, bites, sprites and saves keep their current behaviour. New Def settings default to disabled. No renderer or scene is connected by this change.

## A small vocabulary

| Kind | Movement and grouping | Disturbance and food |
|---|---|---|
| Schooling | Persistent cell/species identity; a smooth patrol centre with a tight local group. Members form around hashed offsets, hold station, scatter and re-form. | Hull approach, propeller noise or a hooked mate spreads the group; matching lure presentation can attract within its certified patch. |
| Ambusher | A single fish holds near cover with a small slow patrol. | Faces prey; a bounded short approach to a matching lure, then returns to cover. |
| BottomDweller | Solitary or a loose small group close above the bed. | Small escape along the bottom; bait attention without midwater jumps. |
| Crawler | Lobster/crab stay on supported bed geometry with slow tangential movement. | Retreat into cover under hull/noise; approach bait on the substrate. No swimming shoal or levitation. |
| Sessile | Clams, mussels and oysters have fixed bed attachment positions. | A visual shell closure/retraction can respond to disturbance; position never scatters. Wild bed occupants and grown lease crops remain separate systems. |

The implemented field answers a group's centre, maximum member footprint, depth and tightness. Individual animation, steering toward a lure, bed-following crawlers and shell closure are later consumers, not systems secretly enabled now. An ambusher/sessile group's count is one; other group counts come from the species school range. A renderer must keep every member within the returned footprint, rather than adding an unrestricted swim offset.

## Data and the missing geography

Add a serializable `LivingBehaviour` profile to FishSpeciesDef. Disabled is the zero/default value. It carries kind, allowed habitat mask, preferred vegetation mask and preference multiplier, minimum/maximum metre depth, bed clearance, patrol radius/speed, normal/scattered group footprint, depth cycle strengths, scatter distance and response/re-form duration. Units are metres and the **existing Core game-clock seconds**. Copy the real clock's SecondsPerDay; do not assume an astronomical 86,400 seconds. Main compresses its day to the configured seconds, despite older prose describing a full SI-seconds clock.

Keep existing ids, Regions, Seasons, time/tide bite windows, DepthBands, lure affinity and school-size/density fields. Bite availability and physical presence are separate: a fish may remain in its habitat off the feed. Do not use a daily bite-window boundary to replace its physical identity. Explicit metre depth bounds are occupancy constraints; the current resolver's depth-band weights remain catch preferences. Content review must make the two descriptions agree before activation.

Habitat flags: OpenWater, Sand, Mud, Rock, WharfPilings. Vegetation flags: Eelgrass, Kelp. Unknown is zero, never silently equivalent to sand or open water. A homogeneous patch can carry several verified habitat tags; at least one must match the species' allowed mask. Known absence of plants is distinct from unknown plant coverage; unknown earns no preference bonus.

Main can answer bed elevation and tide/standing-water level using ITidalTerrain, IEnvironmentService and IStillWater. Terrain plan paint strings and plant payloads exist, but there is no Core ecology query for those habitat/vegetation tags, oxygen, temperature, salinity, crevices or piling cover. Ground paint is not proof of underwater vegetation. World-content must certify a static safe disc per region/species/cell: centre, radius, tags and a conservative **highest bed elevation anywhere in the disc**. No certificate means no new-field fish. This is stricter than checking just the centre or rejecting an unsafe moving point each tick, which can hide/reappear across a shoreline.

The first model accepts such a patch as value data supplied by its caller. It has no concrete World dependency. A future cross-module adapter must use a reviewed Core contract. Authoring ecological masks, rebuilding ground and occupying another lane's terrain files are outside this change.

## Arithmetic

All periods and speeds are inputs. Let `T` be the double Core clock. Let `K = Finalize(FNV(seed, regionId, cellX, cellY, speciesId))`; fold each decision's permanent numbered stream into K. Use uint hash arithmetic, never string.GetHashCode, a global RNG or wall time. A fixed density draw determines cell occupancy: `chance = clamp(densityPerKm2 * cellSizeMetres² / 1,000,000 * vegetationPreference, 0, 1)`. Zero density in an opted-in profile yields no school, rather than inventing a global density.

For a certified disc `(A, R)`, reserve member footprint `F = scatteredFootprint` and reaction displacement `B = scatterDistance`. Choose `r = min(patrolRadius, max(0, R-F-B))`. Sessile has `r=B=0`; the first code also returns no position disturbance for sessile organisms. With `ω = patrolSpeed/r` when r>0, `θ = phase(K) + (T mod period)*ω`, the patrol is:

`P0(T) = A + r*(cos θ, sin θ)`.

One complete turn takes `period=2πr/patrolSpeed`. The modulo is applied in double before conversion to vector floats, avoiding float-time loss on long saves. The path is continuous across loop, day, tide and old slot boundaries, with base speed at most patrolSpeed. It deliberately stays in its home certified disc in this slice. Cross-cell migration needs a connected corridor certificate; do not snap the key to whichever cell the moving centre enters.

Depth uses the same deterministic tide as the world, not a second tide oscillator. Intersect the authored interval `[dMin,dMax]` with `[0, max(0, waterLevel(T)-highestBed-clearance)]`. If the interval is empty, the patch cannot hold that species now. For swimming kinds:

`d(T) = lo + (hi-lo)*clamp01(0.5 + dailyStrength*0.5*cos(2π*T/daySeconds + phase) + tideStrength*(tide01(T)-0.5))`.

Caller supplies normalized local tide and water level for that same T. Daily/light response is a bounded diel approximation in the first code; actual light attenuation, moonlight and shade are later data. Bottom/crawler/sessile depth uses bed level at the actual position, passed by a future bed-following adapter. The first field reports the conservative deepest usable depth in the patch for these kinds; it is **not** a grounded crawler renderer and must not be presented as one.

For that later light-aware slice, author a species depth-fraction curve against shared light01 and a flood/ebb depth offset against the existing signed tide-rate sample. Evaluate `d=lo+(hi-lo)*clamp01(curve(light01)+tidalOffset(tideRate01))`; cap the curves' derivatives and use the shared daylight schedule and water transmission. This lets a day-deep species differ from a night-deep species without giving each cell an arbitrary biological clock. The foundation's hashed daily phase is variety for the inactive model, not proof of a light-aware migration system.

## Bounded local reaction

A reaction value is `(eventGameSeconds, unitDirection, strength01)`, keyed to a school, supplied explicitly to the query and never mutated by querying. The caller chooses direction away from hull/hooked mate/noise; lure attraction chooses toward a valid lure. This decouples boat modules from Fishing. Until wiring exists, the default reaction has strength zero. A full integration keeps a fixed-capacity ring of local events, chooses an event deterministically by priority/time/id, and never restarts a pulse at its peak: replacements wait for a zero envelope or blend with a proven speed budget. Drop events after their finite lifetime. A query is reproducible for the same field inputs **and reaction value**, irrespective of call order. Player-caused reactions cannot be derived from world seed alone.

The later reaction adapter needs per-species alert radius, hull/noise sensitivity, lure attention radius and depth tolerance. These are proposed additions, not fields implemented by this foundation. For source distance `q`, alert radius `a` and normalized noise/speed stimulus `n`, use `strength=clamp01(1-q/a)*clamp01(n*sensitivity)`; zero radius disables that source. A hooked mate uses the same finite radius with stimulus one. A lure applies `attention=clamp01(1-q/lureRadius)*clamp01(1-depthDifference/depthTolerance)` only for a matching existing FavoredLures mask; the direction points toward the lure and shares the same bounded pulse. Species sensitivity zero means no reaction. Hull/mate escape outranks lure attention. All source speeds/depths are sampled at the event instant, so later queries cannot change a recorded pulse.

To form a reproducible group, member j derives `(u,v)` from `(K,j)` and uses the unit-disc offset `sqrt(u)*(cos(2πv), sin(2πv))`, scaled by the field's current footprint. Tightness therefore controls separation through the same footprint, rather than changing school identity or count when scattering. Align sprites to a deterministic tangent/leader heading. A future member-motion path must include its own offset derivative in the species speed budget; the first code and tests bound the **group centre** only. Do not claim those tests already prove individual collision avoidance or all member speeds.

For age `a=T-eventTime`, use envelope `e=sin²(π*a/duration)` on `[0,duration]`, zero elsewhere. Its maximum is one and `|e'|≤π/duration`. Apply `P=P0 + unitDirection*B*strength*e`. Tightness `=1-strength*e`, footprint `=lerp(normalFootprint, scatteredFootprint, strength*e)`. All positions including the member footprint stay within R by the triangle inequality. Maximum centre speed is `patrolSpeed + B*π/duration`; depth changes do not count as planar motion. The caller cannot gain a teleport by altering a reaction value between two samples of the same path. Tests compare a stable event across its start, peak and end.

That pulse models form → hold → scatter → re-form without storing an unbounded population. More realistic flee/hold/re-form envelopes can replace it with the same endpoint and derivative guarantees. Physical hooked/landed individuals leave this recomputed field and become explicit state; excluding a hooked visual needs a bounded reservation id in the later integration. Never serialize the background schools.

## Cadence and scope

Sample the active cells on the existing slow environment cadence, into caller-owned fixed buffers; interpolate for drawing without allocating each frame. Cost is O(species in requested cell), constant arithmetic per result, with no scene objects, renderer creation or per-read lists. The current presenter remains pooled and untouched. Measure CPU, allocations and GPU together before activation; no 60 fps claim follows from arithmetic alone. M1 can evaluate this data/logic foundation; reactive physical occupancy, handling, ecological authoring and migration belong in phased follow-ups described in the backlog.
