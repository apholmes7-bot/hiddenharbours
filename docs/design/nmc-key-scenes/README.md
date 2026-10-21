# Nine Mile Creek package contract — H1b, 2026-10-11

H1b re-takes the twelve unplaced key scenes on the third issue, before H2.
P3 (the working coast), P1 (ground/water contracts) and P5 (the bluff) govern it.
No scene, prefab, rig, texture, runtime activity or player-facing Words is changed.

## Sources and counts

Base: `640e3cc7234460f49aee79defb447c5aa32485c1`. K3 and G3 are the sibling
`HH-nmc-key-scenes-one-scene-reissue-3-2026-10-07` and
`HH-nmc-ground-part-1-reissue-3-2026-10-07` directories under
`C:/hh-drops/HH-nmc-key-scenes-one-scene-reissue-3-2026-10-07/unzipped/`.
The read-only Art input is `INPUT-2026-10-07-nmc-new-areas.md`.

| Source | Bytes | SHA256 |
|---|---:|---|
| `INPUT-2026-10-07-nmc-new-areas.md` | 16333 | `1c1eddf24353a6d6bc01f07e01f50022356f71da8930f634e239d22fa3430184` |
| `HH-nmc-key-scenes-one-scene-reissue-3-2026-10-07/README.md` | 12932 | `80af6e4d64a06b34f1178f62d7c89fedde72ba84b54e6b4c5b2e995347811b51` |
| `HH-nmc-key-scenes-one-scene-reissue-3-2026-10-07/region.json` | 117892 | `239069e45e23e68a477ea60e4e1b2aaddad19f81b63fefe8df81d5522311302c` |
| `HH-nmc-key-scenes-one-scene-reissue-3-2026-10-07/hh-nmc-key-scenes/NOTES-one-scene.md` | 49162 | `a698d83f5035628034a5117a1d7adf21e9aff40dd50e6faff95fad58d5fd217b` |
| `HH-nmc-key-scenes-one-scene-reissue-3-2026-10-07/hh-nmc-key-scenes/NOTES-south-shore-redraw.md` | 15815 | `92f12a2ebd545f697e10c423f77029fcdff7190cf3f1cbde1db3dbaa797e3ff6` |
| `HH-nmc-key-scenes-one-scene-reissue-3-2026-10-07/check-nmc-one-scene.cjs` | 51287 | `9856e5f2fb76d8640a317d77a749d3bbe321e2d0bb81ddd76b731840ea97aab1` |
| `HH-nmc-ground-part-1-reissue-3-2026-10-07/ground/plan.json` | 212937 | `06d0217c8c389c6b97582e2eb6b0cc44ff5bd042bde3af538ac47fd1fe89577a` |
| `HH-nmc-ground-part-1-reissue-3-2026-10-07/ground/NineMileCreekSeabed_HeightTex_plan.png` | 985352 | `9a6c465002cb229d2853191757fb498af66cb81a635f6973f59bf95723ed9f47` |
| `HH-nmc-ground-part-1-reissue-3-2026-10-07/ground/NineMileCreekSeabed_StillTex_plan.png` | 60809 | `0046179b7e66517f79b685807357aa82f5327d70c6d48d1b8d2c566582b5406b` |

Every header hash matches. The header also names the Art relay (a5c2c8dd), old plan (d7a6a154), and old height/still pixels (b997bdce/4fccaf21); these were independently rechecked and match. K3's manifest passes 581/581; its six part lists pass
278/45/19/58/135/46 entries, with no overlap. G3 passes 18/18. K3's embedded plan and
both textures equal G3 byte for byte. Independent PNG decoding produces 1520 × 1120
big-endian u16, rows top-down: height `5924a6428d593bc80bc3eabe6c3887b39b05abe6aa5142b7ef9a0d6f9e8b02b0`,
still `2c796a61961309de81777224c5c7f089aa2d1e1d35797895808bbcb8841e0d64`. The package checker passes **38/38** on Node 24.19.0.
Mapped `idsBefore` equals all 589 committed ids on the base.

| Scene suffix | Source rows | Owned rows | Unique pieces | Placement owner |
|---|---:|---:|---:|---|
| wharf | 134 | 134 | 133 | H5/H6; N8 fleet; N9 crane |
| fuel_stop | 30 | 30 | 30 | H9 |
| town_bridge | 55 | 53 | 53 | H7 |
| the_falls | 21 | 11 | 11 | H7 |
| landing | 25 | 25 | 25 | H8 |
| campground | 178 | 178 | 178 | H12 |
| north_creek | 53 | 53 | 53 | H11 |
| weather_face | 11 | 11 | 11 | H8 |
| main_street | 131 | 91 | 91 | H7 |
| south_shore | 66 | 66 | 66 | H8 |
| plaza | 122 | 122 | 122 | H10 |
| shore_lane | 40 | 40 | 40 | H8 |
| **Total** | **866** | **814** | **813** | |

The 50 refs (40 main street, ten Falls) and two takeover records spawn zero. The crane's
night pose is the extra owned row; `nmc.variantPoses` retains both poses on one id.
Main street's 21 `drawnAt` board offsets do not replace the town bridge's owned positions.
Every asset Source names K3, its scene path, byte size and hash. Placed remains 0 everywhere.

## Identity, takeovers and retirement

The package region alias `region.nmc` maps once to `region.nine_mile_creek`.
Both scene schemas remain supported. The unchanged id map contains 167 renames;
K3 still writes 112 old owned names, all mapped. There are no K3 piece ids with `@`.
`prop.nmc_camp_tent_a19_cabinTent` still maps to `prop.nmc_camp_tent_a19_cabin`.
The prior waterfront bed renames remain. Nested references and object keys use the same map.

Two manifest-listed takeovers keep kind and kit family:

- `structure.nmc_restaurant_bar`: town_bridge (-204, 232.5) → main_street (-226, 241.26), ManorIso harbourmaster preset, call 69.
- `structure.nmc_chandlery`: town_bridge (-160, 234) → main_street (-160, 237.26), NmcSetPieces, call 80.

Each destination row carries `nmc.takeover` with id/from/to/call/why. The town bridge's
first owned piece carries `nmc.scene.takeoverRecords`, preserving both old rows as
records of its 2a boards (call 76). Neither id enters RetiredIds, on the first or repeated import.
An unlisted owner change, missing record/owner, duplicate takeover or changed kind/family refuses.

The retirement table appends exactly these fourteen entries after its existing 192:

- `boat.nmc_shore_dory` (`keyscene.nmc_south_shore`): retired by the third issue's redraw.
- `prop.nmc_bank_bench_4` (`keyscene.nmc_south_shore`): retired by the third issue's redraw.
- `prop.nmc_bank_bench_5` (`keyscene.nmc_south_shore`): retired by the third issue's redraw.
- `prop.nmc_shore_fire` (`keyscene.nmc_south_shore`): retired by the third issue's redraw.
- `structure.nmc_bank_steps_4` (`keyscene.nmc_south_shore`): retired by the third issue's redraw.
- `structure.nmc_bank_steps_5` (`keyscene.nmc_south_shore`): retired by the third issue's redraw.
- `prop.nmc_lot_pole_05` (`keyscene.nmc_plaza`): retired by the third issue's redraw.
- `prop.nmc_lot_pole_04` (`keyscene.nmc_plaza`): retired by the third issue's redraw.
- `prop.nmc_yard_fence_e` (`keyscene.nmc_plaza`): retired by the third issue's redraw.
- `prop.nmc_yard_rack_02` (`keyscene.nmc_plaza`): retired by the third issue's redraw.
- `prop.nmc_yard_rack_01` (`keyscene.nmc_plaza`): retired by the third issue's redraw.
- `structure.nmc_plaza_dock_grocery` (`keyscene.nmc_plaza`): retired by the third issue's redraw.
- `structure.nmc_plaza_dock_lumber` (`keyscene.nmc_plaza`): retired by the third issue's redraw.
- `structure.nmc_apartments_2` (`keyscene.nmc_main_street`): retired by the third issue's redraw.

All 206 reservations are present on every scene. No existing retirement entry is reordered;
no retired id is reused. Five plaza removals arrive under old names already reserved by the map.
Old main-street plaza/wharf fuel-stop sources and record-2b/c/d/e/f cannot be selected or parsed as scenes.

## Owner authorization: migrations and annotations

The owner, via coordinator v60 at **2026-10-11 22:27Z**, authorized the two migrations after
the import's identity guard stopped. The relay says **"The two migrations: AUTHORIZED by the owner,
exactly as you proposed."** This fills the amendment's omission: call 65 and its six-building
NmcShoreEstates table already expect both existing houses to become estates.

- `structure.nmc_south_shore_house_4`: `HouseIso (Art/houseIsoRig.js), its preset’s fields` → `NmcShoreEstates.estateHouse` — third issue south-shore redraw, call 65.
- `structure.nmc_south_shore_house_5`: `HouseIso (Art/houseIsoRig.js), its preset’s fields` → `NmcShoreEstates.estateHouse` — third issue south-shore redraw, call 65.

The explicit NMC-only exception table requires the named id, old HouseIso family, exact new kit,
building kind and south_shore scene. Ids and scene GUID stay stable. Other cross-family changes,
wrong scenes/kinds and reverse migrations refuse; the original refusal message is unchanged.

The same relay accepts all 29 annotation changes. **A family is the kit token before the first
space, dot, comma or parenthesis.** NMC accepts annotations within that token; St Peters retains
exact kit-string matching. The 29 concrete changes are:

| Id | Old annotation | New annotation |
|---|---|---|
| `structure.nmc_restaurant_bar` | `ManorIso (Art/manorIsoRig.js)` | `ManorIso (docs/art/rigs/village-return/houses-kit/Art/manorIsoRig.js), its harbourmaster preset` |
| `structure.nmc_plaza_grocery` | `NmcStores.grocery` | `NmcStores.grocery, o.dock east` |
| `structure.nmc_plaza_lumber` | `NmcStores.lumber` | `NmcStores.lumber, o.dock west` |
| `prop.nmc_yard_gatehouse` | `NmcStores (plaza-stores/nmcStores.js), yardOffice` | `NmcStores (plaza-stores/nmcStores.js 1.1), yardOffice` |
| `prop.nmc_yard_forklift` | `NmcStores (plaza-stores/nmcStores.js), forklift` | `NmcStores (plaza-stores/nmcStores.js 1.1), forklift` |
| `prop.nmc_yard_soil` | `NmcStores (plaza-stores/nmcStores.js), soilPallets` | `NmcStores (plaza-stores/nmcStores.js 1.1), soilPallets` |
| `prop.nmc_yard_pavers` | `NmcStores (plaza-stores/nmcStores.js), paverPallets` | `NmcStores (plaza-stores/nmcStores.js 1.1), paverPallets` |
| `prop.nmc_yard_shed` | `NmcStores (plaza-stores/nmcStores.js), shedDisplay` | `NmcStores (plaza-stores/nmcStores.js 1.1), shedDisplay` |
| `prop.nmc_yard_fence_s` | `NmcStores (plaza-stores/nmcStores.js), fence` | `NmcStores (plaza-stores/nmcStores.js 1.1), fence` |
| `prop.nmc_yard_fence_n` | `NmcStores (plaza-stores/nmcStores.js), fence` | `NmcStores (plaza-stores/nmcStores.js 1.1), fence` |
| `prop.nmc_yard_fence_e78` | `NmcStores (plaza-stores/nmcStores.js), fence` | `NmcStores (plaza-stores/nmcStores.js 1.1), fence` |
| `prop.nmc_yard_fence_e92` | `NmcStores (plaza-stores/nmcStores.js), fence` | `NmcStores (plaza-stores/nmcStores.js 1.1), fence` |
| `prop.nmc_yard_fence_w` | `NmcStores (plaza-stores/nmcStores.js), fence` | `NmcStores (plaza-stores/nmcStores.js 1.1), fence` |
| `prop.nmc_lot_strip_01` | `NmcStores (plaza-stores/nmcStores.js), island` | `NmcStores (plaza-stores/nmcStores.js 1.1), island` |
| `prop.nmc_lot_strip_02` | `NmcStores (plaza-stores/nmcStores.js), island` | `NmcStores (plaza-stores/nmcStores.js 1.1), island` |
| `prop.nmc_lot_strip_03` | `NmcStores (plaza-stores/nmcStores.js), island` | `NmcStores (plaza-stores/nmcStores.js 1.1), island` |
| `prop.nmc_lot_island_01` | `NmcStores (plaza-stores/nmcStores.js), island` | `NmcStores (plaza-stores/nmcStores.js 1.1), island` |
| `prop.nmc_lot_island_02` | `NmcStores (plaza-stores/nmcStores.js), island` | `NmcStores (plaza-stores/nmcStores.js 1.1), island` |
| `prop.nmc_lot_island_03` | `NmcStores (plaza-stores/nmcStores.js), island` | `NmcStores (plaza-stores/nmcStores.js 1.1), island` |
| `prop.nmc_lot_island_04` | `NmcStores (plaza-stores/nmcStores.js), island` | `NmcStores (plaza-stores/nmcStores.js 1.1), island` |
| `prop.nmc_lot_pole_01` | `NmcStores (plaza-stores/nmcStores.js), lotPole` | `NmcStores (plaza-stores/nmcStores.js 1.1), lotPole` |
| `prop.nmc_lot_pole_02` | `NmcStores (plaza-stores/nmcStores.js), lotPole` | `NmcStores (plaza-stores/nmcStores.js 1.1), lotPole` |
| `prop.nmc_lot_pole_03` | `NmcStores (plaza-stores/nmcStores.js), lotPole` | `NmcStores (plaza-stores/nmcStores.js 1.1), lotPole` |
| `prop.nmc_lot_corral_1` | `NmcStores (plaza-stores/nmcStores.js), cartCorral` | `NmcStores (plaza-stores/nmcStores.js 1.1), cartCorral` |
| `prop.nmc_lot_corral_2` | `NmcStores (plaza-stores/nmcStores.js), cartCorral` | `NmcStores (plaza-stores/nmcStores.js 1.1), cartCorral` |
| `prop.nmc_lot_access_14` | `NmcStores (plaza-stores/nmcStores.js), stallSign` | `NmcStores (plaza-stores/nmcStores.js 1.1), stallSign` |
| `prop.nmc_lot_access_15` | `NmcStores (plaza-stores/nmcStores.js), stallSign` | `NmcStores (plaza-stores/nmcStores.js 1.1), stallSign` |
| `prop.nmc_lot_access_3` | `NmcStores (plaza-stores/nmcStores.js), stallSign` | `NmcStores (plaza-stores/nmcStores.js 1.1), stallSign` |
| `prop.nmc_lot_access_4` | `NmcStores (plaza-stores/nmcStores.js), stallSign` | `NmcStores (plaza-stores/nmcStores.js 1.1), stallSign` |

## Kinds, hosts and data carriers

The six added pairs (87 rows) are exhaustive additions to `NmcKindOf`:

| Source defType | Kit family | Rows | Scene(s) |
|---|---|---:|---|
| BuildingDef | NmcShoreEstates | 6 | south_shore |
| SetPieceDef | NmcShoreEstates | 24 | south_shore 21; shore_lane 2; main_street 1 |
| BuildingDef | NmcMainStreet | 15 | main_street |
| SetPieceDef | NmcMainStreet | 39 | main_street |
| BuildingDef | WalkupIso | 2 | plaza: cannery8Loft, walkup8 |
| BuildingDef | RowhouseIso | 1 | plaza: coastalRow4 |

Thirteen plaza rows resolve their host by id after mapping:

| Piece | Owned host (same plaza scene) |
|---|---|
| `prop.nmc_lot_pole_01` | `prop.nmc_lot_strip_01` |
| `prop.nmc_lot_pole_02` | `prop.nmc_lot_strip_02` |
| `prop.nmc_lot_pole_03` | `prop.nmc_lot_strip_03` |
| `prop.nmc_lot_pole_strip2_w` | `prop.nmc_lot_strip2_w` |
| `prop.nmc_lot_pole_strip2_c` | `prop.nmc_lot_strip2_c` |
| `prop.nmc_lot_pole_strip2_e` | `prop.nmc_lot_strip2_e` |
| `prop.nmc_lot_pole_s2_w` | `prop.nmc_lot_island_s2_w` |
| `prop.nmc_lot_pole_s2_e` | `prop.nmc_lot_island_s2_e` |
| `prop.nmc_flats_lantern_w1` | `prop.nmc_flats_island_w1` |
| `prop.nmc_flats_lantern_w2` | `prop.nmc_flats_island_w2` |
| `prop.nmc_flats_lantern_w3` | `prop.nmc_flats_island_w3` |
| `prop.nmc_flats_lantern_e1` | `prop.nmc_flats_island_e1` |
| `prop.nmc_flats_lantern_e2` | `prop.nmc_flats_island_e2` |

A host must be another owned row of that same scene, not a ref, plan-only id, missing row or
self. MountHost and StandsOn record the relationship; no anchor is invented. Legacy `mount`
still requires its exact hosts-table answer and must agree with a simultaneous `host`.
The hosts answer file remains empty because K3 contains no mount wording.

Each piece's `nmc.*` options preserve stations, footprint, float/groundZ, definition,
conditions and applicable source fields. The first owned piece carries `nmc.scene.*`,
refs, counts, source layout/flora/light/sound and deferred owners. Wharf's first piece carries
G3's coast, water, roads, bridges, boardwalks, paths, lots, frozen areas and **areas**;
`nmc.ground.source` records G3's plan hash. `grading` remains H2's and is not carried.
Existing TerrainPlan Def kinds and explicit road/lot/bridge/household contracts remain unchanged.
Geometry is plan metres, PPU 32, y unsquashed; footprints are extents and at is the origin.

The six south-shore household strings remain verbatim on house `nmc.household`/`nmc.def`
and the corresponding `nmc.plan.lots.south_shore_houses` estate entries. Existing creek/camp
briefs remain data too. All Words arrays are empty. The plan's list also includes three Shore Lane
homes; those are not extra estate households. Numeric source reference-photo indices stay data;
no photograph, real person's name or maker branding is copied into this commit.

At **2026-10-11 17:38:40Z**, the owner ruled decisions 8 and 9: **"I agree with all leans"**.
Decision 8 assigns Dan `lot.nmc_house_south`; H10 moves the farmhouse to (-200, -8)
facing Route 91. No lane places `lot.nmc_house_north`. H1b carries the plan only.
Decision 9 extends the household-origin permission to the six south-shore households;
**H13 amends canon first**, then writes any player-facing lines. H1b writes no canon line.
The 10-06 decisions 1(b), 2–7(a), names-slate requirement and deferred visit windows stand.

## Importer seams and verification

- `ImportNmc`: validate manifest takeovers, retain old rows as records, preserve old/new ownership and kind/family, record provenance and distinguish source/ref/record/owned counts. Carry G3 `areas` alongside the existing plan keys; grading stays H2.
- `NmcSupersededFile`: exclude `record-2d`, `record-2e` and `record-2f` as well as the prior records.
- `NmcKindOf`: add the six explicit type/family pairs above; all other incompatible pairs still refuse.
- `NmcRow`: resolve `host` through the id map to another owned row in the same scene; set MountHost/StandsOn without inventing an anchor. Exact `mount` wording still needs its answer and must agree with an explicit host.
- `NmcKitFamily`, `NmcKitMigrations` and `NmcSameKit`: NMC annotation matching and the two authorized one-way migrations, with report lines.
- Shared `Write`: optional kit predicate and validated transfer set. Defaults preserve St Peters' exact kit comparison and retirement behavior; transfers are not retired.

NMC filters existing filenames before parsing. St Peters excludes NMC before reading its
assets. The isolation test supplies malformed foreign YAML in both directions and proves
neither region reads, retires or rewrites the other.

The headless .NET harness invokes this checkout's pure `ImportNmc` from this checkout's cwd.
It writes `result.Assets` and `result.Metas` byte for byte; assets and metas are never hand-edited.
The KeySceneDef script GUID comes from its committed meta. The first successful import reports
**12/12 assets changed, 0 new metas**; the second reports **0/12 changed, 0 new metas**.
All 12 existing meta files, hence their GUIDs, remain byte-identical. Both reports give
866 source rows, 50 refs, 2 takeover records, 814 owned rows and 813 unique owned ids.
Reports: `artifacts/nmc-h1b/import-first.txt` and `import-second.txt` (local, ignored).

The shared YAML writer retains its existing empty-scalar trailing spaces (1,672 added lines); all such warnings were checked, and code/docs pass whitespace checks. Generated assets are kept byte for byte.

The compile succeeds with **0 warnings, 0 errors**. All **48 headless tests pass**, with
0 failures and 0 skips: 23 unchanged KeySceneImportTests and 25 NmcPackageImportTests.
The committed-data test checks every NMC asset's region, unplaced state, ids, owner, kit,
variants, empty Words, retirement exclusion and light ownership. The unchanged
`ContentValidationTests.NmcKeyScenes_Exist_AsUnplacedData_WithUniqueIdsAndResolvedRegion`
and StPetersKeyScenesTests compile against the named DLLs; their AssetDatabase/engine
execution belongs to Phase B CI. No Unity process or CI read was used here.

Project DLLs are read-only copies from `boats-walk-b0/Library/ScriptAssemblies`, compiled
2026-10-11 13:01Z at 6fe4a0d9 (no KeyScene changes beyond this base). App.Editor is dated
13:01:24Z, World 13:01:20Z, Core 13:01:18Z, Tools.Editor 13:01:20Z; Tools.JsEngine.Editor
is 12:44:56Z. Every copied DLL's name, exact UTC timestamp and origin is recorded in
`artifacts/nmc-h1b/borrowed-dlls.csv`. Current importer/test sources are compiled directly,
so the borrowed App.Editor importer is not the tested implementation.

Eleven tests added (all in NmcPackageImportTests):

- `TheTwoRuledEstateMigrations_KeepIdKindSceneAndGuid`
- `AnUnlistedNmcIdChangingFamily_StillRefuses`
- `SameFamilyAnnotationChanges_PassOnlyOnNmc`
- `StPeters_StillRefusesACrossFamilyChange`
- `StPeters_StillRefusesASameFamilyAnnotationChange`
- `TakenOverIds_ChangeOwner_OnlyAsRegionJsonListsThem_AndNeverRetire`
- `TheNewKitFamilies_Import_AndEveryOtherPairStillRefuses`
- `HostsNamedById_ResolveToAnOwnedRow_OrRefuse`
- `TheThirdIssuesRecordFolders_AreNeverParsed`
- `TheFourteenRedrawnIds_AreRetired_AndNeverReused`
- `CommittedThirdIssue_HoldsTakeoversHostsAreasAndSixHouseholdsAsData`

Two tests adapted, retaining their earlier assertions:

- `CommittedScenes_HaveTheRuledCounts_AndPassTheDataContract`: third-issue counts/source and expanded content invariants.
- `PlanKinds_ReuseTerrainDefs_AndKeepMeasuredGeometry`: areas carried, grading absent and plan source hash retained.

No test removed, renamed or newly skipped. The stipulated baseline is main 640e3cc7 push run
38127738602: EditMode total/pass/skip/fail **14445/14180/265/0**, PlayMode **1077/949/128/0**.
This PR's forecast is **+11 EditMode tests**, hence **14456/14191/265/0** if all pass;
PlayMode unchanged. Other main commits may add their own tests. These are forecasts,
not observed CI outcomes; the seat reads CI by name.

## Deferred owners

N1 owns every scene write. H2 imports G3's ground/still water, grading and farmhouse pad;
H3 the bluff; H4 roads, lots and bridges as R2, then R4 traffic. H5/H6 place the wharf;
H7 town/Falls/main street; H8 landing/weather/south shore/Shore Lane; H9 gas bar;
H10 plaza, reefer seating and farmhouse move; H11 creek/dories; H12 campground.
H13 supplies household names, routines, visit windows, fire schedules and the south-shore
canon amendment. N3 owns the office identity; N8 fleet and N9 crane gameplay remain separate.
Art-pipeline/audio join the placement lanes for lights, flora and sound. River fishery stays M3.
Bookings/fees, takeable ring/bell, pool/court play, interiors, residents' cars, truck bays and
later regions/businesses require their own scope. The plaza's 20 homes and main street's
east side have no new households assigned here.
