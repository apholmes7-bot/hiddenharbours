# St Peters routine routes against painted paths

The committed village has **6 resident routines and 30 ordinary-day legs**. **0 of 30 authored routes lie wholly on Path paint**, and 15 are at least half on it. The requested straight-line comparison gives 0 wholly painted and 4 at least half painted. These are measurements of the serialized paint mask, not a judgement of the rendered appearance.

**14 straight-line legs cut a fence or an unrelated building footprint.** 14 intersect fence lines; 3 also cross buildings other than their origin or destination. The authored routes reduce this to **7 legs**, all with fence intersections. No measured leg intersects any of the 210 active serialized cliff-wall strips. A straight line is a comparison, not the movement rule: residents use the lane tree and indoor door approaches.

The worst long direct lines are the two sage-home/wharf journeys. The authored lane removes their unrelated building and fence crossings. The plot journeys still intersect fences in the authored route. The slip/wharf deck legs have no Path paint, which alone is not a defect. Indoor home and work approaches also make 100% paint coverage an unsuitable universal pass criterion.

All residents have a resolved Home activity station. The only station unused by these routines is `green_b`. There are 0 unresolved local station references. The Wednesday quad trip additionally names two mainland station IDs absent from this scene; those are explicitly cross-region destinations, listed below rather than called local wiring faults.

## Snapshot and method

- Baseline: `81b0795db6bd3bd6a5352d66167d4863f0ae2f41`. This report describes the committed scene, not a rebuilt scene or an open change. Input hashes below identify the measurement inputs.
- One leg starts at the previous entry's station and departs at the current entry's `StartHour`. The first leg starts at the last block's station. Hours show departure through the next departure (a block window, not travel duration); overnight windows wrap midnight. These are authored hours before seed-based jitter.
- Distances are Euclidean scene XY metres, matching `RoutineLaneTree.PolylineLength` and the routine engine. They are not unsquashed ground distances or distances along the elevation surface. Building rotation happens before its serialized ground-depth squash, as in `InteriorFootprint.ModelToWorld`.
- Paint is the scene-bound `StPetersSplatF.png` red channel (Path, material index 20): 1520 × 1040 pixels across 760 × 520 scene metres, centre (0, 0). Each route is split into 0.5 m chainage intervals; each midpoint samples its containing pixel, with a shortened last interval weighted by its length. A nonzero red byte counts as paint. PNG row zero is north. Metres and percentages estimate coverage, not a continuous-area integral; reverse journeys can differ slightly because the final partial interval starts at the other end.
- No dilation, bilinear filtering, shader warp, terrain material fallback, or visual acceptance is applied. Paint under indoor footprints is included. Splat B alpha is the older Dirt material and is not treated as proof of an authored Path. The two named old starter paths survive as sources of lane bends, but the current Path mask is authoritative for these measurements.
- Crossings use continuous segment intersection, not the 0.5 m samples, so thin barriers cannot fall between samples. Fence runs come from the seven active `PropertyBoundary._runs` arrays, transformed to world coordinates; the authored gate gaps stay open. Fence thickness and actor radius are not added. Endpoint contact and collinear overlap count. Cliff checks use each adjacent brow/toe quad in world plan coordinates, without projecting the drawn face or inferring a physics collider.
- Building coverage includes 17 footprints: ten serialized `BuildingInterior` ground floors plus seven scenery buildings from `Buildings.json` footprint dimensions and the scene sprite's facing. These include the cannery, plot sheds, bait store, net loft and net shed. Roof overhangs, furniture, trees, and unrelated props are outside the building-footprint question. `B*` marks a footprint containing an endpoint; this is expected for indoor departures/arrivals and excluded from the headline unrelated-building count. It does not prove that a straight line enters through the door.
- The authored comparison reproduces `RoutinePlanner.BuildFromStops`: station, exit door if indoors, lane nodes and child-to-parent via points (reversed when travelling outward), entrance door, destination. Adjacent points within 1 mm collapse. A via count of zero does not mean no authored routing: junctions and door approaches still exist. Waypoint columns count intermediate points after this collapse.
- No Unity/editor, PlayMode, rendering, navigation collision run, full retired-export suite, or CI completion is claimed. Conversation holds, tide waits, region transitions, beach-day substitutions, and seed/day jitter are dynamic and not synthesized. Every static ordinary-day entry and every Wednesday entry is accounted for. Four Wednesday legs include off-island stations and have no single St Peters-local line or mask coverage; their metrics are explicitly N/A below.

## Worst ten direct-line legs

Rank: most unrelated building footprints, then most fence lines, then greatest unpainted direct distance. This ranks geometry for review, not severity of observed gameplay. `*` is an endpoint footprint.

| Rank | Resident | Departure | From → to | Direct m | Path m / % | Direct intersections | Authored intersections |
|---:|---|---|---|---:|---:|---|---|
| 1 | basil_samson | 18:00 | wharf_head → home_sage_b | 231.8 | 29.5 / 12.7% | B2, B5, B10*, B11, B16, F1, F4, F5, F6 | B10* |
| 2 | basil_samson | 05:00 | home_sage_b → wharf_head | 231.8 | 30.0 / 12.9% | B2, B5, B10*, B11, B16, F1, F4, F5, F6 | B10* |
| 3 | rose_macisaac | 08:30 | saltbox_dooryard → post_office_counter | 33.4 | 3.5 / 10.5% | B6*, B12, F3, F6 | B6*, F3 |
| 4 | aunt_ginny | 17:00 | ginny_village_mark → cottage_garden | 80.6 | 2.0 / 2.5% | F1, F3 | F1, F3 |
| 5 | junior_poirier | 21:00 | green_d → home_sage_a | 41.2 | 7.5 / 18.2% | B10*, F4, F5 | B10* |
| 6 | aunt_ginny | 04:30 | cottage_step → ginny_village_mark | 74.3 | 9.0 / 12.1% | F3 | F3 |
| 7 | eileen_doiron | 17:30 | school_desk → flats_lookout | 51.1 | 6.5 / 12.7% | B11*, F5 | B11* |
| 8 | rose_macisaac | 18:00 | post_office_counter → green_a | 37.8 | 8.8 / 23.3% | B6*, F6 | B6* |
| 9 | eileen_doiron | 08:15 | farmhouse_dooryard → school_desk | 30.2 | 10.0 / 33.1% | B11*, F5 | B11* |
| 10 | rose_macisaac | 21:30 | green_a → home_saltbox | 20.7 | 6.5 / 31.4% | B9*, F3 | B9*, F3 |

## Every ordinary-day leg

Station names omit `station.st_peters.`; resident names omit `routine.`. Intersections list building and fence keys from the inventories below; no cliff intersections were found.

| Resident | Block hours | From → to | Direct m | Direct Path m / % | Intermediate / via points | Authored m | Authored Path m / % | Direct intersections | Authored intersections |
|---|---|---|---:|---:|---:|---:|---:|---|---|
| aunt_ginny | 04:30–17:00 | cottage_step → ginny_village_mark | 74.3 | 9.0 / 12.1% | 1 / 0 | 80.9 | 7.0 / 8.6% | F3 | F3 |
| aunt_ginny | 17:00–20:00 | ginny_village_mark → cottage_garden | 80.6 | 2.0 / 2.5% | 2 / 0 | 89.8 | 7.5 / 8.3% | F1, F3 | F1, F3 |
| aunt_ginny | 20:00–04:30 | cottage_garden → cottage_step | 8.9 | 0.4 / 4.4% | 0 / 0 | 8.9 | 0.4 / 4.4% | F1 | F1 |
| basil_samson | 05:00–12:00 | home_sage_b → wharf_head | 231.8 | 30.0 / 12.9% | 10 / 6 | 247.9 | 170.0 / 68.6% | B2, B5, B10*, B11, B16, F1, F4, F5, F6 | B10* |
| basil_samson | 12:00–15:00 | wharf_head → slip_head | 23.5 | 0.0 / 0.0% | 0 / 0 | 23.5 | 0.0 / 0.0% | none | none |
| basil_samson | 15:00–18:00 | slip_head → wharf_head | 23.5 | 0.0 / 0.0% | 0 / 0 | 23.5 | 0.0 / 0.0% | none | none |
| basil_samson | 18:00–05:00 | wharf_head → home_sage_b | 231.8 | 29.5 / 12.7% | 10 / 6 | 247.9 | 170.0 / 68.6% | B2, B5, B10*, B11, B16, F1, F4, F5, F6 | B10* |
| eileen_doiron | 07:30–08:15 | home_farmhouse → farmhouse_dooryard | 4.5 | 0.0 / 0.0% | 1 / 0 | 4.6 | 0.0 / 0.0% | B12*, F6 | B12*, F6 |
| eileen_doiron | 08:15–12:00 | farmhouse_dooryard → school_desk | 30.2 | 10.0 / 33.1% | 3 / 0 | 32.9 | 18.0 / 54.7% | B11*, F5 | B11* |
| eileen_doiron | 12:00–13:15 | school_desk → store_customer_c | 16.7 | 8.2 / 49.1% | 3 / 0 | 20.5 | 17.5 / 85.4% | B11*, F5 | B11* |
| eileen_doiron | 13:15–17:30 | store_customer_c → school_desk | 16.7 | 8.0 / 47.9% | 3 / 0 | 20.5 | 17.0 / 83.0% | B11*, F5 | B11* |
| eileen_doiron | 17:30–20:30 | school_desk → flats_lookout | 51.1 | 6.5 / 12.7% | 7 / 2 | 99.7 | 71.0 / 71.2% | B11*, F5 | B11* |
| eileen_doiron | 20:30–07:30 | flats_lookout → home_farmhouse | 73.6 | 9.5 / 12.9% | 7 / 2 | 102.5 | 61.0 / 59.5% | B12* | B12*, F6 |
| junior_poirier | 06:00–06:30 | home_sage_a → sage_dooryard | 4.5 | 4.0 / 88.9% | 1 / 0 | 4.7 | 4.2 / 89.3% | B10* | B10* |
| junior_poirier | 06:30–12:00 | sage_dooryard → flats_head | 46.9 | 3.5 / 7.5% | 3 / 2 | 95.6 | 47.5 / 49.7% | none | none |
| junior_poirier | 12:00–14:30 | flats_head → store_customer_b | 61.1 | 4.1 / 6.7% | 4 / 2 | 82.4 | 59.9 / 72.7% | none | none |
| junior_poirier | 14:30–19:00 | store_customer_b → flats_head | 61.1 | 4.0 / 6.5% | 4 / 2 | 82.4 | 60.0 / 72.8% | none | none |
| junior_poirier | 19:00–21:00 | flats_head → green_d | 60.4 | 2.5 / 4.1% | 3 / 2 | 61.3 | 39.5 / 64.5% | none | none |
| junior_poirier | 21:00–06:00 | green_d → home_sage_a | 41.2 | 7.5 / 18.2% | 3 / 0 | 44.6 | 15.0 / 33.6% | B10*, F4, F5 | B10* |
| marguerite_leblanc | 07:00–13:00 | home_store → store_counter | 5.1 | 4.6 / 90.2% | 1 / 0 | 5.1 | 4.6 / 90.2% | B5* | B5* |
| marguerite_leblanc | 13:00–14:30 | store_counter → post_office_door | 13.2 | 2.5 / 18.9% | 0 / 0 | 13.2 | 2.5 / 18.9% | none | none |
| marguerite_leblanc | 14:30–19:00 | post_office_door → store_counter | 13.2 | 2.7 / 20.5% | 0 / 0 | 13.2 | 2.7 / 20.5% | none | none |
| marguerite_leblanc | 19:00–21:00 | store_counter → green_c | 20.5 | 14.0 / 68.3% | 1 / 0 | 22.3 | 20.3 / 91.0% | none | none |
| marguerite_leblanc | 21:00–07:00 | green_c → home_store | 25.5 | 15.0 / 58.7% | 3 / 0 | 27.4 | 25.0 / 91.2% | B5* | B5* |
| rose_macisaac | 07:00–08:30 | home_saltbox → saltbox_dooryard | 4.4 | 0.0 / 0.0% | 1 / 0 | 4.4 | 0.0 / 0.0% | B9* | B9* |
| rose_macisaac | 08:30–12:30 | saltbox_dooryard → post_office_counter | 33.4 | 3.5 / 10.5% | 4 / 0 | 55.9 | 24.5 / 43.8% | B6*, B12, F3, F6 | B6*, F3 |
| rose_macisaac | 12:30–14:00 | post_office_counter → store_customer_a | 22.5 | 5.5 / 24.5% | 3 / 0 | 23.1 | 4.0 / 17.3% | B6* | B6* |
| rose_macisaac | 14:00–18:00 | store_customer_a → post_office_counter | 22.5 | 5.5 / 24.5% | 3 / 0 | 23.1 | 4.5 / 19.5% | B6* | B6* |
| rose_macisaac | 18:00–21:30 | post_office_counter → green_a | 37.8 | 8.8 / 23.3% | 4 / 0 | 42.3 | 22.8 / 53.9% | B6*, F6 | B6* |
| rose_macisaac | 21:30–07:00 | green_a → home_saltbox | 20.7 | 6.5 / 31.4% | 3 / 0 | 20.8 | 4.5 / 21.6% | B9*, F3 | B9*, F3 |

## Wednesday replacement day

The `Weekday: 2` override replaces the ordinary day for `junior_poirier`. Its six wholly local legs are below; the four remaining legs follow in a separate table. The late store visit departs at 13:00 instead of 12:00.

| Resident | Block hours | From → to | Direct m | Direct Path m / % | Intermediate / via points | Authored m | Authored Path m / % | Direct intersections | Authored intersections |
|---|---|---|---:|---:|---:|---:|---:|---|---|
| junior_poirier | 06:00–06:30 | home_sage_a → sage_dooryard | 4.5 | 4.0 / 88.9% | 1 / 0 | 4.7 | 4.2 / 89.3% | B10* | B10* |
| junior_poirier | 06:30–08:30 | sage_dooryard → flats_head | 46.9 | 3.5 / 7.5% | 3 / 2 | 95.6 | 47.5 / 49.7% | none | none |
| junior_poirier | 13:00–14:30 | flats_head → store_customer_b | 61.1 | 4.1 / 6.7% | 4 / 2 | 82.4 | 59.9 / 72.7% | none | none |
| junior_poirier | 14:30–19:00 | store_customer_b → flats_head | 61.1 | 4.0 / 6.5% | 4 / 2 | 82.4 | 60.0 / 72.8% | none | none |
| junior_poirier | 19:00–21:00 | flats_head → green_d | 60.4 | 2.5 / 4.1% | 3 / 2 | 61.3 | 39.5 / 64.5% | none | none |
| junior_poirier | 21:00–06:00 | green_d → home_sage_a | 41.2 | 7.5 / 18.2% | 3 / 0 | 44.6 | 15.0 / 33.6% | B10*, F4, F5 | B10* |

| Block hours | From | To | Distance / paint / intersections |
|---|---|---|---|
| 08:30–10:00 | `station.st_peters.flats_head` | `station.nine_mile_creek.bar_landing` | N/A: cross-region or mainland leg |
| 10:00–11:00 | `station.nine_mile_creek.bar_landing` | `station.nine_mile_creek.fuel_stop` | N/A: cross-region or mainland leg |
| 11:00–11:30 | `station.nine_mile_creek.fuel_stop` | `station.nine_mile_creek.bar_landing` | N/A: cross-region or mainland leg |
| 11:30–13:00 | `station.nine_mile_creek.bar_landing` | `station.st_peters.flats_head` | N/A: cross-region or mainland leg |

IDs absent from the St Peters scene: `station.nine_mile_creek.bar_landing` and `station.nine_mile_creek.fuel_stop`. The asset explicitly supplies `RegionId: region.nine_mile_creek`; outbound and return crossings name `vehicle.utility_quad` at 5.5 m/s. No mainland geometry or painted-path claim is made by this local audit.

## Residents, homes and source files

| Resident | Scene object | Ordinary blocks | Home activity stations | Jitter ± minutes | Asset |
|---|---|---:|---|---:|---|
| aunt_ginny | `AuntGinny` | 3 | cottage_step | 3 | [AuntGinnyRoutine.asset](../../Assets/_Project/Data/Routines/AuntGinnyRoutine.asset) |
| basil_samson | `IslandInhabitants/BasilSamson` | 4 | home_sage_b | 8 | [BasilSamsonRoutine.asset](../../Assets/_Project/Data/Routines/BasilSamsonRoutine.asset) |
| eileen_doiron | `IslandInhabitants/EileenDoiron` | 6 | farmhouse_dooryard, home_farmhouse | 4 | [EileenDoironRoutine.asset](../../Assets/_Project/Data/Routines/EileenDoironRoutine.asset) |
| junior_poirier | `IslandInhabitants/JuniorPoirier` | 6 | sage_dooryard, home_sage_a | 7 | [JuniorPoirierRoutine.asset](../../Assets/_Project/Data/Routines/JuniorPoirierRoutine.asset) |
| marguerite_leblanc | `IslandInhabitants/MargueriteLeBlanc` | 5 | home_store | 5 | [MargueriteLeBlancRoutine.asset](../../Assets/_Project/Data/Routines/MargueriteLeBlancRoutine.asset) |
| rose_macisaac | `IslandInhabitants/RoseMacIsaac` | 6 | saltbox_dooryard, home_saltbox | 6 | [RoseMacIsaacRoutine.asset](../../Assets/_Project/Data/Routines/RoseMacIsaacRoutine.asset) |

A Home activity can be an outdoor step or dooryard; it does not require an interior. No resident lacks a home station under that data definition. The plot resident's `cottage_step` is outdoors. No station membership or identity was inferred from NPC display names.

## Scene station table

| Station | XY | Lane node | Interior | Used by a routine |
|---|---|---|---|---|
| green_a | 1.182, 6.529 | green | no | yes |
| green_b | 2.500, 7.000 | green | no | no |
| green_c | 3.818, 7.471 | green | no | yes |
| green_d | 5.137, 7.942 | green | no | yes |
| ginny_village_mark | 4.000, 11.000 | yard_cottage | no | yes |
| cottage_step | 76.737, 26.129 | yard_ginny_plot | no | yes |
| cottage_garden | 84.000, 21.000 | yard_ginny_plot | no | yes |
| saltbox_dooryard | 17.507, 7.667 | yard_saltbox | no | yes |
| farmhouse_dooryard | 15.768, 20.626 | lane_farmhouse | no | yes |
| sage_dooryard | -21.500, 35.380 | yard_sage_cottage | no | yes |
| store_counter | 2.032, 27.905 | lane_store | no | yes |
| store_customer_a | 0.947, 24.967 | lane_store | no | yes |
| store_customer_b | 1.845, 24.911 | lane_store | no | yes |
| store_customer_c | 2.743, 24.855 | lane_store | no | yes |
| post_office_door | 12.540, 35.915 | lane_post_office | no | yes |
| flats_head | -55.000, 2.500 | flats_head | no | yes |
| flats_lookout | -52.009, 2.734 | flats_head | no | yes |
| slip_head | 183.500, 4.393 | slip_head | no | yes |
| wharf_head | 207.000, 4.752 | wharf_head | no | yes |
| home_saltbox | 21.860, 8.000 | yard_saltbox | yes | yes |
| home_farmhouse | 18.334, 24.286 | lane_farmhouse | yes | yes |
| home_sage_a | -20.900, 39.827 | yard_sage_cottage | yes | yes |
| home_sage_b | -22.100, 39.827 | yard_sage_cottage | yes | yes |
| home_store | 2.500, 32.979 | lane_store | yes | yes |
| school_desk | -11.400, 33.742 | lane_school | yes | yes |
| post_office_counter | 16.680, 41.039 | lane_post_office | yes | yes |

## Lane nodes and authored bends

All node and via coordinates are scene-world XY. Via points are stored child → parent. A spare ninth point exists after the referenced via slices and is not part of any edge.

| Node | XY | Parent | Referenced via points |
|---|---|---|---|
| green | 2.500, 7.000 | root | none |
| lane_store | 2.032, 27.905 | green | none |
| lane_school | -11.400, 29.985 | lane_store | none |
| lane_farmhouse | 15.768, 20.626 | lane_store | none |
| lane_post_office | 12.540, 35.915 | lane_store | none |
| yard_saltbox | 17.507, 7.667 | green | none |
| yard_sage_cottage | -21.500, 35.380 | green | none |
| yard_cottage | 4.000, 11.000 | green | none |
| yard_ginny_plot | 76.737, 26.129 | green | none |
| flats_head | -55.000, 2.500 | green | (-29.141, 2.157); (-12.874, 1.549) |
| slip_head | 183.500, 4.393 | green | (183.000, 4.400); (182.000, 4.000); (181.000, 3.000); (143.165, 2.813); (95.992, -3.408); (49.160, -0.518) |
| wharf_head | 207.000, 4.752 | slip_head | none |

## Footprint and boundary keys

| Key | Scene object | Source |
|---|---|---|
| B1 | `GinnyPlot/leanTo` | bake footprint + scene facing |
| B2 | `GinnyPlot/netStore` | bake footprint + scene facing |
| B3 | `GinnyPlot/sageCottage` | BuildingInterior ground floor |
| B4 | `GinnyPlot/woodshed` | bake footprint + scene facing |
| B5 | `IslandShops/generalStore` | BuildingInterior ground floor |
| B6 | `IslandShops/postOffice` | BuildingInterior ground floor |
| B7 | `IslandVillage/creamSaltbox` | BuildingInterior ground floor |
| B8 | `IslandVillage/harbourGable` | BuildingInterior ground floor |
| B9 | `IslandVillage/redSaltbox` | BuildingInterior ground floor |
| B10 | `IslandVillage/sageCottage` | BuildingInterior ground floor |
| B11 | `IslandVillage/school` | BuildingInterior ground floor |
| B12 | `IslandVillage/whiteFarmhouse` | BuildingInterior ground floor |
| B13 | `IslandVillage/whiteGable` | BuildingInterior ground floor |
| B14 | `StPetersCannery` | bake footprint + scene facing |
| B15 | `StPetersKeyScenes/keyscene.stp_east_end/structure.stp_net_loft` | bake footprint + scene facing |
| B16 | `StPetersKeyScenes/keyscene.stp_east_end/structure.stp_net_shed` | bake footprint + scene facing |
| B17 | `StPetersKeyScenes/keyscene.stp_landing/structure.stp_bait_store` | bake footprint + scene facing |
| F1 | `Yards/GinnyPlot/Boundary` | serialized open fence runs |
| F2 | `Yards/HarbourGable/Boundary` | serialized open fence runs |
| F3 | `Yards/RedSaltbox/Boundary` | serialized open fence runs |
| F4 | `Yards/SageCottage/Boundary` | serialized open fence runs |
| F5 | `Yards/School/Boundary` | serialized open fence runs |
| F6 | `Yards/WhiteFarmhouse/Boundary` | serialized open fence runs |
| F7 | `Yards/WhiteGable/Boundary` | serialized open fence runs |

## Sources and reproduction

- [Scene](../../Assets/_Project/Scenes/StPeters.unity): `RoutineStations`, `RoutineLanes`, `VillagerRoutine`, `BuildingInterior`, `PropertyBoundary`, `CliffWallSurface`, `TerrainSplatSurface`.
- [Routine planner](../../Assets/_Project/Code/World/Routines/RoutinePlanner.cs), [lane traversal](../../Assets/_Project/Code/World/Routines/RoutineLaneTree.cs), [weekly/cross-region planner](../../Assets/_Project/Code/World/Routines/RoutineLegs.cs).
- Painted path authoring: [23 village route assets](../../Assets/_Project/Data/Regions/StPetersVillage/), [terrain PathDef assets](../../Assets/_Project/Data/Terrain/StPetersPlan/) and [starter centreline declarations](../../Assets/_Project/Code/App/Editor/StPetersStarterSplat.cs). These are polylines/declared line sources, not an authored navigation spline. The mask can diverge from the lane tree; the audit measures both without rebuilding either.
- [Path-channel binding](../../Assets/_Project/Code/Art/TerrainSplatSurface.cs) and [station paint sampling convention](../../Assets/_Project/Code/App/Editor/StPetersVillageStations.cs).
- Existing coverage found before extending the audit: [StPetersRoutineContentTests.cs](../../Assets/Tests/EditMode/StPetersRoutineContentTests.cs) already tests station membership, lane-tree arrays, painted bend points and per-block travel fit. [RoutineLaneTreeTests.cs](../../Assets/Tests/EditMode/World/RoutineLaneTreeTests.cs) tests route point ordering. These were not duplicated or changed.
- Reused the existing [scene exporter](../../tools/scene-export/hh_scene_export.py), its Force Text reader and hierarchy transforms, adding an isolated audit option. The retired package exporter stays retired; no package regeneration or CI restoration is needed. The reader now consumes wrapped unquoted notes so later schedule entries are not lost. Existing parser tests were extended alongside audit geometry/sampling checks.

Requires Python 3.10+ and Pillow, plus the committed Path PNG bytes. Run from the repository root:

```powershell
python tools/scene-export/hh_scene_export.py --routine-audit Evidence~/gpt-goods/routine-audit.json --routine-audit-doc docs/world/st-peters-routine-routes-audit.md
python -m unittest discover -s tools/scene-export/tests -p test_export.py -k UnityYamlTests -v
python -m unittest discover -s tools/scene-export/tests -p test_export.py -k RoutineAuditTests -v
```

Validation: six parser cases and four audit cases passed. The audit was run twice with byte-identical JSON and Markdown. No game data or visible assets were changed.

### Input SHA-256

| Input | SHA-256 |
|---|---|
| `Assets/_Project/Art/Sprites/Buildings/Village/Buildings.json` | `7d8b23e5610a4998ae0e09dfae90cd3af7a234c524d0aaaeca1ad88d89289334` |
| `Assets/_Project/Art/Sprites/Buildings/Village/Village_ginnyLeanTo.png.meta` | `0b6bf9684c4a03e2e95a14591f59e5c9a71d85b54c9e281e3e3c1e9fd9acfe4a` |
| `Assets/_Project/Art/Sprites/Buildings/Village/Village_ginnyNetStore.png.meta` | `995f8850cba87bc8cbce70ce24d395056c7ac1732afc2d18f72e678f4a5c2712` |
| `Assets/_Project/Art/Sprites/Buildings/Village/Village_ginnyWoodshed.png.meta` | `678c3858b39563918c2c8357515625deed4cc8fe5ef2bef9b9eb74260cf0fc59` |
| `Assets/_Project/Art/Sprites/Buildings/Village/Village_stPetersBaitStore.png.meta` | `5ab238aa87a39d0d1081a80e10faa61ddb5bc1b0d8817988f1194bbb32a096ce` |
| `Assets/_Project/Art/Sprites/Buildings/Village/Village_stPetersCannery.png.meta` | `2d73a78af8d2a0530f82f6bfe80f57605eab716c90b4419122ea0feb45d925c2` |
| `Assets/_Project/Art/Sprites/Buildings/Village/Village_stPetersNetLoft.png.meta` | `24f9b28ca1c382797cba8200ad198cfcca6856202d84ddb3629d07e026b82091` |
| `Assets/_Project/Art/Sprites/Buildings/Village/Village_stPetersNetShed.png.meta` | `0381e062f90d73b2423cf34f2aa92f9472646b666d98c9366f241a372390b0a1` |
| `Assets/_Project/Data/Routines/AuntGinnyRoutine.asset` | `3556f7161ea32943921e05fc168d4c39e581d5153c5d05782ec73a672ab76263` |
| `Assets/_Project/Data/Routines/AuntGinnyRoutine.asset.meta` | `eda76bc79dbdae9e3a7270970e9f66195d1a6c3056cdba231fa7320f30debd4b` |
| `Assets/_Project/Data/Routines/BasilSamsonRoutine.asset` | `45dd34df0f56973556ef93c14130ed3cf3264a39560502d2bf572c2ad8a747b1` |
| `Assets/_Project/Data/Routines/BasilSamsonRoutine.asset.meta` | `2d79869ef2b5ebeecbcb149d1b1c9fa7981e6abd6f9735162d7deca6be9af663` |
| `Assets/_Project/Data/Routines/EileenDoironRoutine.asset` | `333db2592e3f8ccf1d98999308a8364b7a16cc4e428c34454dd957bbcf59079e` |
| `Assets/_Project/Data/Routines/EileenDoironRoutine.asset.meta` | `b81417a120b08223ecf7371cd636833a27422333d9f6c3c1f6d7ba2bf25b0105` |
| `Assets/_Project/Data/Routines/JuniorPoirierRoutine.asset` | `51d59060705b2faa2585429c0d4e6615327b5afb57b5283750bd5746e91deca7` |
| `Assets/_Project/Data/Routines/JuniorPoirierRoutine.asset.meta` | `51f902752b841b5693d99db357410347e100afa92f6f06acc74b5aba0e2d92d6` |
| `Assets/_Project/Data/Routines/MargueriteLeBlancRoutine.asset` | `7f3f68ddc6e85df055c6a647fc9862ecaa2075f0fa0db205a23c5f2a4099287c` |
| `Assets/_Project/Data/Routines/MargueriteLeBlancRoutine.asset.meta` | `b3655900f414c870bebdcde96e9dc9a5229d51263713abc15e516155fa02b1ea` |
| `Assets/_Project/Data/Routines/RoseMacIsaacRoutine.asset` | `bbe3008fa6feca1c02ee7ea6103bde6718b40db17bd5df56cfb042ef5a2b1fe4` |
| `Assets/_Project/Data/Routines/RoseMacIsaacRoutine.asset.meta` | `874b2fd904c00fde8656d51a0497eb06c21c335f7c4c81f72103fc86628d6e1d` |
| `Assets/_Project/Data/Terrain/StPetersSplatF.png` | `0563d67578ebd03e9654f6cd2c3ec940502b6d6118e7e508ba0df87dc4ade500` |
| `Assets/_Project/Data/Terrain/StPetersSplatF.png.meta` | `b3a100377c823fe31c1553458fa38a065b6a402b01d7833713cc03b61a385d5c` |
| `Assets/_Project/Scenes/StPeters.unity` | `ca167c4c94b43b72ef8bf438acfbb39b3c2c2226eb8898dc5afadd9612f62fb0` |
