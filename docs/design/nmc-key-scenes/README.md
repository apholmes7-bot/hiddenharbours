# Nine Mile Creek package contract — H1, 2026-10-06

The owner approved overhaul decision 1(b), origins with a canon amendment, and decisions
2–7(a) at 2026-10-06 11:08:49Z. The named amendment is in `docs/vision-and-pillars.md`.
P1 and P3 govern this import. H1 is data only: no Unity, scene, prefab, texture, route,
traffic, access, calendar, NPC, Words or new activity system is applied.

## Source verification

Base: `321cd0fcc92851cd738aa490175f3180daf5bea9` (#927). The two read-only roots are:

- K: `C:/hh-drops/HH-nmc-key-scenes-one-scene-reissue-2-2026-10-02/unzipped/HH-nmc-key-scenes-one-scene-reissue-2-2026-10-02/`
- G: the sibling `HH-nmc-ground-part-1-reissue-2-2026-10-02/` directory.

K's `SHA256SUMS.txt`: 336/336 pass. G's: 27/27 pass. K embeds the three ground
files, not G's auxiliary tools/notes; all three embedded files equal G byte for byte.
G is authoritative. Its identities are:

| File | Bytes | SHA256 |
|---|---:|---|
| `ground/plan.json` | 150238 | `d7a6a154b0a0ed1f6216821fdff6c885928a9fbc60e24def68ea0ebbb3faa2ad` |
| `ground/NineMileCreekSeabed_HeightTex_plan.png` | 963052 | `c6b9f08b0dbef3dbd4d0cb225f0cbec1572537ec3c80f7b03c0474ce08178095` |
| `ground/NineMileCreekSeabed_StillTex_plan.png` | 60647 | `f54a3bc66b0cb41f27cee5557490c2c8cd6b47691e9a52dd22431d77f3e36ecf` |

Independent PNG filter decoding gives 1520 × 1120 unsigned 16-bit greyscale maps.
SHA256 of decoded big-endian u16, rows top-down:

- Height: `b997bdced7cddea68dc1b5487f5664973e2a8304a54d55a50f3406de43eecc88`.
- Still: `4fccaf218f5694757539d73464da59bca0141816385a0b026115f479c2c59c67`.

The source region schema is `hidden-harbours/nmc-one-scene@1`; current scenes use
`hidden-harbours/key-scene@1` and `hidden-harbours/keyscene@2`. The explicit profile
maps `region.nmc` to the game's existing `region.nine_mile_creek` exactly once.

## Counts and identity

| Scene suffix | Source rows | Owned rows | Serialized unique pieces | Placement owner |
|---|---:|---:|---:|---|
| wharf | 134 | 134 | 133 | H5/H6; N8 fleet; N9 crane |
| fuel_stop | 30 | 30 | 30 | H9 |
| town_bridge | 55 | 55 | 55 | H7 |
| the_falls | 21 | 11 | 11 | H7 |
| landing | 25 | 25 | 25 | H8 |
| campground | 178 | 178 | 178 | H12 |
| north_creek | 53 | 53 | 53 | H11 |
| weather_face | 11 | 11 | 11 | H8 |
| main_street | 10 | 2 | 2 | H7 |
| south_shore | 8 | 8 | 8 | H8 |
| plaza | 51 | 51 | 51 | H10 |
| shore_lane | 32 | 32 | 32 | H8 |
| **Total** | **608** | **590** | **589** | |

The crane's working/night poses share one id and one piece; `nmc.variantPoses` retains
both complete poses and their disjoint variants. The Falls' ten and main_street's eight
refs are validated against both the region index and the owned rows, then stored only
as references. They spawn zero. The four superseded sources never supply placements:
the old main-street plaza, wharf fuel stop, north creek record-2c and campground record-2b.

`key-scene-id-map.json` contains 167 explicit renames: the three required names below,
plus the audit's capitals, secondary dots and coordinate labels in pieces and plan data.
Coordinate labels become stable sequential ids in source order. Route 91's number,
numbered house/lot/pitch labels and repeated trap-stack grid indices remain semantic
labels. The map is one step and injective, and applies to nested references and owner keys.

- `prop.nmc_waterfront_6_bed_35` → `prop.nmc_waterfront_6_bed_w`.
- `prop.nmc_waterfront_6_bed_29` → `prop.nmc_waterfront_6_bed_e`.
- `prop.nmc_camp_tent_a19_cabinTent` → `prop.nmc_camp_tent_a19_cabin`.

`key-scene-retired-ids.json` reserves 192 ids: renamed originals and removed identities
from the superseded sources. Identities still present in the current package are retained.
These package-wide reservations are copied into every scene's RetiredIds. Later removals
append to that list; imports reject reuse, changed kind/kit or transfer to another owner.
N3 still owns the unresolved `structure.nmc_harbourmaster` office identity; H1 does not
invent a replacement id.

The current package supplies no `mount` field. `key-scene-hosts.json` therefore has no
fabricated answers. If a future package supplies a mount, its exact words must have a
piece/host/anchor answer and a resolvable host. Absolute gull/perch points remain data;
N3 and H5/H6/H9 settle their support/anchor contracts before placement.

## Data layout and named kinds

`ImportNmc` is a pure profile in new partial files. It uses the existing KeySceneDef,
KeyScenePiece, KeySceneLight and YAML writer. No additional entity identity or terrain
implementation is created. The existing Options extension carries the additional
unplaced contracts with an `nmc.` prefix:

- Each piece: `nmc.ownerScene`, `nmc.type`, stations, footprint/footM/dims, float/groundZ,
  facing, definition and source conditions as applicable. Source options retain their keys.
- Each scene's first owned piece: `nmc.scene.*` holds the scene-level layout, refs, light,
  flora/rules, sound, ground, budgets and game conditions. `sourceRows` and `ownedRows`
  preserve the distinct counts. This carrier is deterministic; it is not a new placement.
- Wharf's first owned piece: `nmc.plan.*` holds the final ground plan's coast, water,
  roads, bridges, boardwalks, paths, lots and frozen areas. `nmc.ground.source` identifies
  the checked plan; `nmc.plan.kinds` names its contracts. These records are measured data,
  not newly instantiated roads, lots or gameplay entities.
- `nmc.household`: each of the seven creek households is on its house; each of the
  fourteen camp households is on its unit or tent-pitch piece. Text is verbatim. Eleven
  name the real origins approved in canon. All `Words` arrays remain empty.
- `nmc.visitMapping`: August → High Summer; the weekend May–October couple → Early Spring
  through The Turn on rest days. Exact windows and source wording remain data for H13.
- `nmc.deferred`: every scene names the owners of its conditional data.

The lead-architect type map names **harbour** (the wharf/marina/yard composites) and
**vehicle** (ambient and registered vehicle records), in addition to the existing kinds.
`NmcKindOf` refuses unknown defTypes and incompatible kit families; a reimport also
refuses an existing id's exact kit or kind changing. Existing building/boat/rock/gull/
setPiece kinds are reused.

The plan contract names **road**, **lot**, **bridge** and **household**, via the exhaustive
`NmcPlanKindOf`. A road carries its class, surface, width and polyline; a lot its rig
origin and extent; a bridge its span/deck/width and fitting record; a household its
verbatim description and deferred visit record. These are H1 data kinds, not new runtime
Def classes. H2 reuses RegionTerrainPlanDef/GroundFileDef, CoastSectionDef, StreamDef,
PondDef and PathDef rather than introducing parallel terrain Defs. H4 consumes the road,
lot and bridge records; H13 creates NpcDef/RoutineDef after the names slate.

Geometry stays in plan metres at PPU 32, with world y unsquashed. `footprint` is the extent;
`at` is the origin. The four nearest-corner exceptions are 2.72, 2.78, 2.02 and 2.12 m.
No new pads, terrain grading, access grant or route change is made here.

## Shared seams and region isolation

`KeySceneImport.cs` changes only these seams:

1. St Peters' pure Import excludes NMC filenames and rejects an NMC scene index entry.
2. St Peters' filesystem Run filters NMC filenames **before reading** their bytes.
3. ReadBeside accepts profile-specific map/table names for provenance and diagnostics.
4. Scene/Write append explicit reserved ids, without changing St Peters' output.

NMC filters to `KeyScene_nmc_*.asset`, then verifies the canonical region and Placed 0.
It returns only NMC output. The synthetic isolation test supplies deliberately invalid
foreign YAML in each direction and proves it is never parsed or rewritten.

At base 321cd0fc the H1 anchors were re-found: KeySceneDef `:35` / Placed `:78`, importer
`:59` and owner table `:90`, KeySceneFolder `:49`, StPetersRegionId `:50`, existing NMC
region in NineMileCreek.asset `:15` and builder `:390`, RegionTerrainPlanDef `:16`,
GroundFileImport `:212`, world-map-plan Q12 `:327` and NMC row `:448`.
The charter's Build anchor `:374` is its MenuItem; **Build is `:375`**. No other H1 anchor
used here moved. Terrain changes in #927 are St Peters work; no NMC or importer path moved.

## Verification and hand-on

Assets and asset metas are the pure ImportNmc result, written byte for byte by a .NET
harness from this checkout's cwd. KeySceneDef's guid is read from its committed .meta.
The harness references copies of terrain-pr5w's ScriptAssemblies and engine managed
assemblies; it starts no Unity process. Local first/second reports are in
`artifacts/nmc-h1/import-first.txt` and `import-second.txt` (ignored).

The first report is **12 of 12 assets changed, 12 new metas**; the second is
**0 of 12 assets changed, 0 new metas**. Both report 608 source rows, 18 refs,
590 owned rows and 589 unique owned ids. The .NET build finished with zero warnings
and errors. The reflection runner executed 21 existing importer tests and 14 new NMC
tests: **35 passed, 0 failed, 0 skipped**. It did not execute Unity-dependent tests.

The existing 21 KeySceneImportTests are unedited. NmcPackageImportTests uses synthetic
in-memory packages, plus a committed-file contract test; CI never needs the drop roots.
The new ContentValidationTests.NmcKeyScenes_Exist_AsUnplacedData_WithUniqueIdsAndResolvedRegion
loads the actual assets through Unity in CI. StPetersKeyScenesTests is unedited and is
compile-checked locally; its engine-dependent execution and content validation belong
to Phase B. No test is removed or newly skipped. Forecast baseline: main 321cd0fc push
run **37449242001**; the seat compares CI outcomes by name.

Deferred owners: N1 all scene writes; H2 ground/still water; H4 bridges/roads and their
clearances as R2, then R4 traffic; H5/H6 wharf water/land, N8 fleet, N9 crane gameplay;
H7 town/Falls/Water Street; H8 landing/weather/south shore/Shore Lane; H9 gas bar; H10
plaza/reefer seating; H11 creek/dories/pickups/door occlusion; H12 campground/cooler/
playground/bridge fit, with Camper pass 2 for the three CamperIso units; H13 household
names/routines/visit windows and lights/fire timing; N3 office and art support contracts.
The river fishery is M3. Bookings/fees, takeable ring/bell, pool/court play and playable
interiors each require separate scope. Later regions and businesses remain logged only.
