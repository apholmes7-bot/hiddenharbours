# Walk-sourced hulls: separate berths, prep and cooking

Read-only design audit against main `f5be0efa36eea29a5e93f5cce6212e7af1fa9142`.
Supports P2 (boat progression), P3 (inhabited working boats), P4 (prepare food by hand)
and P5 (a comfortable place to return to). This document proposes layouts; it does
not accept a new look or change a rig, generated sidecar, asset or save format.

**Result: eight hulls are candidates for two separate single berths within their
existing hull envelopes; Go Fast is not recommended for that arrangement.** Three
workboats already publish separate singles with recorded 30/30 sleep fits. The
other five candidates need layout, access or fit work before that capacity can be
called usable. None of the nine has imported mattress metadata or a prep action.
There is no claim that eight currently support a complete two-player sleep flow.

## 1. Which nine, and what actually selects their source

The exact nine are named in
[`BoatInteriorPrepAndBunkTests.OwedByTheRigWalk`](../../Assets/Tests/EditMode/RigBaking/BoatInteriorPrepAndBunkTests.cs#L29-L50).
Independently, selecting committed `BoatInteriorDef` assets whose `SourceSidecar`
ends in `.walk.json` produces exactly the same nine rows:

| Hull | Interior asset in `Assets/_Project/Data/Boats/Interiors/` | Walk in `docs/art/rigs/gameplay/` | Sleep / cook / prep actions |
| --- | --- | --- | --- |
| Side Dragger, 25 m | [SideDraggerIso.asset](../../Assets/_Project/Data/Boats/Interiors/SideDraggerIso.asset) | [sideDraggerIsoRig.side_dragger.walk.json](../art/rigs/gameplay/sideDraggerIsoRig.side_dragger.walk.json) | 4 / 1 / 0 |
| Stern Trawler, 38 m | [SternTrawlerIso.asset](../../Assets/_Project/Data/Boats/Interiors/SternTrawlerIso.asset) | [sternTrawlerIsoRig.stern_trawler.walk.json](../art/rigs/gameplay/sternTrawlerIsoRig.stern_trawler.walk.json) | 4 / 1 / 0 |
| Stern Trawler Mk2, 38 m | [SternTrawlerMk2Iso.asset](../../Assets/_Project/Data/Boats/Interiors/SternTrawlerMk2Iso.asset) | [sternTrawlerMk2IsoRig.stern_trawler_mk2.walk.json](../art/rigs/gameplay/sternTrawlerMk2IsoRig.stern_trawler_mk2.walk.json) | 4 / 1 / 0 |
| Sport Fisher Convertible (53), 16.2 m | [SportFisherConvertibleIso.asset](../../Assets/_Project/Data/Boats/Interiors/SportFisherConvertibleIso.asset) | [sportFisherIsoRig2.convertible.walk.json](../art/rigs/gameplay/sportFisherIsoRig2.convertible.walk.json) | 3 / 1 / 0 |
| Sport Fisher Skybridge (90), 27.4 m | [SportFisherSkybridgeIso.asset](../../Assets/_Project/Data/Boats/Interiors/SportFisherSkybridgeIso.asset) | [sportFisherIsoRig2.skybridge.walk.json](../art/rigs/gameplay/sportFisherIsoRig2.skybridge.walk.json) | 4 / 1 / 0 |
| Express Cruiser, 10.2 m | [ExpressCruiserIso.asset](../../Assets/_Project/Data/Boats/Interiors/ExpressCruiserIso.asset) | [cruiserIsoRig.express_cruiser.walk.json](../art/rigs/gameplay/cruiserIsoRig.express_cruiser.walk.json) | 2 / 1 / 0 |
| Go Fast, 12.36 m | [GoFastIso.asset](../../Assets/_Project/Data/Boats/Interiors/GoFastIso.asset) | [goFastIsoRig.go_fast.walk.json](../art/rigs/gameplay/goFastIsoRig.go_fast.walk.json) | 1 / 0 / 0 |
| Motor Yacht 84, 25.5 m | [MotorYacht84Iso.asset](../../Assets/_Project/Data/Boats/Interiors/MotorYacht84Iso.asset) | [motorYacht84IsoRig.motor_yacht_84.walk.json](../art/rigs/gameplay/motorYacht84IsoRig.motor_yacht_84.walk.json) | 6 / 1 / 0 |
| Expedition Yacht 120, 36.75 m | [ExpeditionYacht120Iso.asset](../../Assets/_Project/Data/Boats/Interiors/ExpeditionYacht120Iso.asset) | [expeditionYacht120IsoRig.expedition_yacht_120.walk.json](../art/rigs/gameplay/expeditionYacht120IsoRig.expedition_yacht_120.walk.json) | 8 / 5 / 0 |

The selection is executable, not just that test list:

1. [`RigMeshExtractor.ExtractFrom`](../../Assets/_Project/Code/Tools/Editor/RigBaking/RigMeshExtractor.cs#L1825-L1826)
   asks `HullWalkRigExtractor.FindWalk` for the selected rig scope.
2. [`FindWalk`](../../Assets/_Project/Code/Tools/Editor/RigBaking/HullWalkRigExtractor.cs#L26-L35)
   looks for `<rig stem>.<scope.id>.walk.json`. A matching file selects the walk
   extraction path. The two sport fishers share a script but have different scope ids.
3. [`RigMeshAssetBaker`](../../Assets/_Project/Code/Tools/Editor/RigBaking/RigMeshAssetBaker.cs#L943-L952)
   appends the walk's interior faces, and its
   [import step](../../Assets/_Project/Code/Tools/Editor/RigBaking/RigMeshAssetBaker.cs#L1443-L1449)
   calls `HullWalkSidecarImporter`.
4. [`Populate`](../../Assets/_Project/Code/Tools/Editor/RigBaking/HullWalkSidecarImporter.cs#L138-L162)
   stamps the source and builds indoor levels from walk areas on positive cut
   levels. Its [anchor mapping](../../Assets/_Project/Code/Tools/Editor/RigBaking/HullWalkSidecarImporter.cs#L185-L191)
   copies only `id`, `action` and `reach`. Existing `anchors.berths[]` polygons,
   mattress tops and head directions do not become `BunkLevel`, `BunkObstruction`,
   `MattressCentre` or `PillowEnd`.

Thus these boats do have bunk-related source data: **36 sleep actions, 12 cook
actions and berth polygons across nine walks**. What is missing is prep and the
imported mattress contract. Go Fast even retains a sleep action while its imported
`Levels` is empty. That action is not evidence of an accessible cabin. The current
prep/bunk tests explicitly exempt these sources; their green result cannot prove
these nine satisfy the rules.

## 2. Dimensions and proposed layouts

All coordinates below are hull-local metres: `+x` starboard, `+y` bow, `+z` up.
Each linked walk declares this frame. `x=[a,b], y=[c,d]` describes a footprint,
not a pair of interaction points. A mattress top is distinct from the sole where
the player stands.

The dimension table measures bounding boxes of the committed **player-centre walk
outlines**. They are already inset by the walk radius (0.23 m in these walks),
exclude furniture and can join several rooms. They are neither clear rectangular
rooms nor hull beams. The berth polygons supply the separate furniture dimensions.
Headroom comes from the walk/rig, not an image or a hull length estimate.

| Hull | Relevant imported walk bounds: x; y; sole z | Readable clear height / special restriction |
| --- | --- | --- |
| Side Dragger | `house`: [-1.34,1.34]; [-9.29,-5.43]; 2.07. `below`: [0.23,1.97]; [-9.17,-5.23]; 0.30. | House 2.37 m; below only 1.65 m. The actual lower cabin is 4.40 by 4.40 m (`BL.hx=2.20`, y=-9.4..-5.0), much wider than its aisle. |
| Stern Trawler | `house`: [-2.39,2.39]; [1.81,8.77]; 3.52. `below`: [-2.12,2.67]; [2.33,8.67]; 1.15. | House 2.92 m; below 2.23 m. Actual lower cabin 5.80 by 6.80 m; central table is a walk hole. |
| Stern Trawler Mk2 | `house`: [-2.24,2.24]; [1.81,8.77]; 3.52. `below`: [-2.12,2.67]; [2.33,8.67]; 1.15. | Same 2.92 / 2.23 m heights and lower cabin as Stern Trawler; narrower house and bridge. |
| Convertible | `house_sole`: [-1.61,1.19]; [-4.66,-0.59]; 1.72. `below_sole`: [-0.33,1.002]; [-0.25,4.15]; 0.55. Raised `vip`: [-0.18,0.748]; [4.35,4.52]; 0.98. | Salon 2.02 m; ordinary lower level nominally 2.05 m. Narrow, partitioned lower cabins; the VIP walk patch is only 0.17 m long. |
| Skybridge | `house_sole`: [-2.875,2.875]; [-8.60,2.31]; 2.88. `below_sole`: [-2.118,2.118]; [-2.76,4.65]; 0.82. Raised `vip`: [-1.649,1.649]; [4.95,7.025]; 1.12. | Salon 2.20 m; ordinary lower level 1.94 m. Do not apply that height to the raised VIP without its ceiling profile. |
| Express Cruiser | `cabin`: [-0.19,0.19]; [0.59,0.98]; 0.22. | Only 0.38 by 0.39 m of player-centre standing patch; 1.888..1.914 m headroom. Aft berth has 0.56 m above its mattress; access waits on crawl. |
| Go Fast | No imported interior level. The rig declares cuddy sole 0.26, nominal ceiling 1.543. | Excluded cuddy standing patch has 1.283..1.290 m headroom and waits on duck/crawl; do not treat it as an upright galley. |
| Motor Yacht 84 | `saloon_sole`: [-2.04,2.04]; [-6.85,1.15]; 3.75. `lower_sole`: [-1.607,1.603]; [-6.03,0.41]; 1.50. | Saloon walk headroom at least 2.01 m; lower level 2.10 m. The guest approach patch is only 0.61 by 0.68 m; the garage is not spare accommodation. |
| Expedition Yacht 120 | `below_sole`: [-3.71,3.71]; [-1.09,10.32]; 1.90. `house_sole`: [-3.82,3.82]; [-11.10,10.32]; 4.30. `lazarette`: [-3.76,3.76]; [-15.81,-6.48]; 2.08. | Individual lower rooms usually publish at least 2.05 m standing headroom. Main galley walk bounds [0.77,3.82]; [-1.60,1.70], at least 2.042 m high. Lazarette is a separate aft compartment, not a rectangle available for beds. |

For a new/refitted single, use roughly 2.1..2.2 m length and 0.74..0.80 m width as
a **design allowance**, motivated by the existing workboat singles, not a new
runtime rule or a guarantee for every character build. Keep independent mattress
polygons, supports, pillows and reach points. A double with two anchors is not two
single berths. Retain stairs, door sweeps, aisle continuity and the hand-carried
raw-food -> prep -> cooktop journey. The existing
[`PrepToCookMaxMetres=1.5`](../../Assets/_Project/Code/Core/Boats/BoatInteriorDef.cs#L214-L245)
also requires a nearby prep station on the cooktop's own level; do not satisfy it
by reaching through a wall or from a stair.

### Side Dragger

Keep two of the four existing transverse port-side singles: `berth_aft` at
x=[-2.14,-0.03], y=[-9.35,-8.61], and `berth_fwd` at the same x,
y=[-6.65,-5.91]. Each mattress is 2.11 by 0.74 m, top z=0.85, head to port;
their reach points are (0.32,-8.98,0.30) and (0.32,-6.28,0.30). This retains the
existing repeated joinery bays and starboard aisle; no new bed shape is needed.
All four recorded sleep fixtures are 30/30, but standing below is only 12/30:
the 1.65 m cabin needs a truthful low-headroom access policy.

Keep the forward-wall range, reach (-0.03,-5.46,2.07), and put prep on its port
counter **beside**, not on, the sink. A candidate reach is (-0.65,-5.55,2.07),
0.63 m from the range reach; it is inside the current `house_sole` walk. The
[range/counter/sink construction](../art/rigs/sideDraggerIsoRig.js#L1198-L1209)
leaves the inboard counter end beside the range as the first place to investigate.
No shortage of bed or galley area; low cabin headroom is the constraint.

### Stern Trawler

Keep `berth_aft_port` and `berth_aft_stbd`: x=[-2.85,-0.68] and [0.68,2.85],
both y=[2.13,2.87], tops z=1.70, each 2.17 by 0.74 m. Their existing reaches
are on opposite sides of the central aisle, with a forward pair also available.
The paired joinery, lockers and central table already give the accommodation an
intentional arrangement. All four recorded sleep fits are 30/30.

Retain the range reach (-0.10,8.60,3.52). Use the port L-counter's inboard end
for prep, candidate reach (-0.95,8.60,3.52), 0.85 m away, keeping the sink farther
outboard. The [counter geometry](../art/rigs/sternTrawlerIsoRig.js#L1285-L1299)
already separates range, sink and side return. No added berth or cooker geometry
is required for this proposal; prep needs its own action and a measured free patch.

### Stern Trawler Mk2

Use the same two independent aft singles and mattress coordinates as Stern
Trawler; the lower cabin and all four 30/30 sleep fixtures match. Retain the
existing central aisle and the table hole. Do not copy the other ship's house
outline: this hull's imported house is 0.30 m narrower overall.

Keep the existing cook reach (-0.10,8.60,3.52), and investigate prep at
(-0.95,8.60,3.52) on this rig's own forward port counter. Its existing range,
sink and counter are in
[`interiorFaces`](../art/rigs/sternTrawlerMk2IsoRig.js#L1263).
There is room for both singles and both food stages without taking working deck
space. The repeated shipboard furniture is the design rationale, not a new bed
placed beside a helm.

### Sport Fisher Convertible (53)

The present three mattresses have recorded sleep fits **0/30 each**. The master
is only 1.69 m long, the starboard single 1.44 m and the VIP 1.09 m. These are not
three usable adult berths. Start with the two separate lower cabin bays in the
[variant's `below` data](../art/rigs/sportFisherIsoRig2.js#L1796-L1801): narrow
the port master to one longitudinal single and extend the starboard single,
with separate lockers and pillows. Investigate pad zones near x=[-1.80,-1.04]
and [1.02,1.78], each approximately y=[0.10,2.30], top z=1.11. This requires
moving the short forward partitions (currently near y=2.02 and 2.35), checking
the tapered liner and retaining both inboard approaches. These rectangles are
refit targets, not measurements of empty space. If the liner or head compartment
prevents them, a salon bench berth may replace one target; do not shorten the bed
or certify the present VIP instead. The hull is a refit candidate, not fit-proven.

Retain the port salon galley, cook reach (-1.095,-1.205,1.72). Its 1.25 by 0.67 m
counter is recorded in the
[salon fit-out](../art/rigs/sportFisherIsoRig2.js#L1773-L1779).
Allocate one end for a hob and the other for a cutting board, with two measured
reaches from the salon, within 1.5 m, clear of the companionway panel. There is
space for a compact two-stage counter, but the source currently calls the tallest
generic counter a stove: an art pass must make those surfaces legible. Preserve
the existing 0.48 m way between counter and companionway.

### Sport Fisher Skybridge (90)

Use the two existing lower single cabins, rather than the master double. Port
`single_port` is x=[-2.27,-1.38], y=[0.18,2.32], 2.14 by 0.89 m; starboard
`single_starboard` is x=[1.08,1.72], y=[0.13,2.17], 2.04 by 0.64 m; both top
z=1.38, heads aft. Their distinct doorways and central passage already provide
privacy. Refit the narrow starboard pad toward 0.75 m width and 2.1..2.2 m length,
and validate the port pad too: **both current single fixtures are 0/30** despite
their superficially plausible lengths. The source also records master 6/30 and
VIP 28/30. No inference of a successful fit follows from those bounding boxes.
Keep the [cabin doors and forward stair](../art/rigs/sportFisherIsoRig2.js#L1703-L1714).

Keep the starboard salon galley at cook reach (2.16,-0.95,2.88). The tall counter
already runs x=2.40..3.00, y=-2.50..0.60; reserve a board section near y=-1.80,
candidate prep reach (1.90,-1.80,2.88), 0.89 m from cook. Verify the walk holes
and counter edge before authoring it. This is a proper paired cabin/galley layout
within a large hull, not a capacity problem; fitting and imported metadata remain.

### Express Cruiser

Make one single pad in each **already separate** sleeping area: the forward
V-berth polygon spans x=[-0.929,0.929], y=[1.43,3.65], top z=0.80; the aft
athwartships berth spans x=[-1.193,1.193], y=[-2.26,-0.86], top z=0.55. Keep
the forward pad along the V's centre, feet toward the bow; use a roughly 0.75 m
fore/aft strip of the transverse aft pad, head to port. Put the spare width into
built-in stowage, not a second sleeper on either pad. Two different cabins make
the choice deliberate and preserve the little boat's scale. Existing full-pad
sleep fixtures are both 30/30; narrowed single pads need their own fit check.

The aft area is excluded pending crawl: only 0.56 m above its mattress and about
0.169 m beside the stair at sole height. The forward step also waits on crawl.
Keep those restrictions. Two physical singles are plausible; a two-person
accessible sleep loop is not supplied by the present standing patch.

Keep the port hob x=[-0.92,-0.50], y=[0.70,1.08] and sink immediately forward,
y=[1.08,1.38], from the
[galley construction](../art/rigs/cruiserIsoRig.js#L1727-L1738).
There is **no generous spare prep counter** between them. Propose a fold-over
board on the sink, used for prep before cooking, with its own action and reach
near (-0.17,0.95,0.22), 0.06 m from the present cook reach. A board is a real
future geometry/state change, not permission to label an occupied sink as a
board today. Separate functions may share that tiny standing patch sequentially;
do not claim room for two cooks or a full-size additional worktop. No upright
second deck or raised roof is proposed.

### Go Fast

**Do not promise two independent full single berths or an interior prep/cooktop
pair within the current envelope.** It has one V mattress, 2.85 m long, top
z=0.62, width tapering from 1.634 to 1.304 m. Two 0.74 m singles plus even a
small separation cannot follow that taper over an adult-length run; a transverse
single is too short across the available width. Stacking would spend the roughly
0.94..0.97 m above the existing mattress, with no suitable standing access.
The present single shared V has a recorded 30/30 sleep fixture, not a second
independent bed.

The [cuddy exclusions](../art/rigs/goFastIsoRig.js#L1693-L1719) require duck/crawl,
and the imported interior has no levels. The aft cuddy pockets are already the
cooler and head. They do not provide an upright working galley. No enclosed
wheelhouse bench is available as an alternative. Keep it a day boat: prepare
and cook ashore, and seek another sleeping place when two people need separate
berths. A second berth or an interior galley would require a different brief
for the shell/cockpit, not extra interaction anchors. That redesign is outside
this recommendation; there is no claim of a naval-architecture impossibility
under an entirely different design or smaller mattress allowance.

### Motor Yacht 84

Use the guest cabin's **existing two singles**, not the master/VIP doubles or the
low crew stack. `berth_guest_out` is x=[-2.60,-1.88], y=[-3.18,-1.20], and
`berth_guest_in` x=[-1.60,-0.84] with the same y; tops z=2.12, heads aft.
The [room record](../art/rigs/motorYacht84IsoRig.js#L1057-L1062) explicitly calls
this two single berths. The gap and separate approaches already make a twin
cabin. Extend their 1.98 m lengths toward 2.1..2.2 m within a revised cabin
fit-out, keeping the forward guest approach, plinth supports and corridor door.
Both current guest fixtures are 23/30; master is 25/30, VIP 23/30 and crew
lower/upper only 3/30 and 4/30. This hull has room, not certified adult fits.

Keep the saloon's port hob (cook reach -1.385,-0.325,3.75); add prep on the clear
counter strip forward of it and before the sink, candidate reach
(-1.385,0.125,3.75), 0.45 m away. The
[counter construction](../art/rigs/motorYacht84IsoRig.js#L1218-L1223) puts the hob
at y=-0.60..-0.05 and sink at y=0.30..0.80. Check the usable board depth in the
0.35 m gap, or shift the sink within this counter instead of borrowing the
spiral stair's well. The twin cabin below and domestic counter above preserve
the yacht's intended room hierarchy.

### Expedition Yacht 120

Choose one independent single in each mirrored lower VIP suite. The existing
port and starboard mattress polygons are x=[-3.18,-1.62] and [1.62,3.18], both
y=[0.54,2.43], top z=2.20. Replace those broad 1.89 m beds with narrower,
longer single pads, approximately x=[-2.80,-2.02] / [2.02,2.80],
y=[0.54,2.74], keeping the pillow end aft and adjusting the bases and foot-end
furniture. Their
[suite envelopes](../art/rigs/expeditionYacht120IsoRig.js#L1354-L1368) run
y=0.43..4.07, leaving a refit opportunity without consuming the foyer stair or
en-suites. The mirrored private suites make separate accommodation part of the
plan. These are proposed changes, not empty rectangles or new measured fits.

All current guest/VIP/crew/captain sleep fixtures are 0/30; the master is 29/30.
Some writer pivots also differ from the apparent offset berth centres, so a
future fit repair must distinguish harness placement from genuinely short pads.
Do not enlarge a hull to make a faulty fixture pass or count its eight actions
as eight fit-proven beds.

The existing main galley has a cutting-board station, but `chop_galley` is
incorrectly exported as `cook` and its reach is `offWalk`. Change it to `prep`
and make a real walkable reach. Retain/designate a cooktop on the starboard
counter at `bench_galley`, reach (3.00,1.00,4.30). The existing board reach
(2.05,-0.75,4.30) is **1.99 m away**, beyond 1.5 m. Relocate the board to a
clear stretch of that counter with a measured prep reach near (3.00,0.20,4.30),
0.80 m from cook, or refit the island to bring both reaches together. This must
be checked against the detailed galley walk, which already excludes furniture;
it is not a certified free point. The
[galley fit-out](../art/rigs/expeditionYacht120IsoRig.js#L1428-L1434) supplies the
counter, island and board locations.

The crew pantry, sky bar and fly bar are also exported as `cook`. Decide each
by its actual appliance: give every retained cooktop a same-level prep surface,
or correct an erroneous action if it is only a serving bar. Keep stable ids;
record any action correction as a rule migration. A prep point in the main
galley cannot satisfy a cook action on another deck. There is ample overall
space; station semantics, fit and access are the unresolved issues.

## 3. Exact future files and what forces regeneration

**This PR changes only this document.** The following is the implementation
handoff, not authorization to edit another active art lane's files. Each row
names the actual source and generated outputs. Prefixes used to make the paths
unambiguous:

- `R` = `docs/art/rigs/`
- `W` = `docs/art/rigs/gameplay/`
- `I` = `Assets/_Project/Data/Boats/Interiors/`
- `D` = `Assets/_Project/Data/Boats/Decks/`
- `M` = `Assets/_Project/Data/Boats/HullMeshes/`

| Hull | Rig source to author | Generated walk to regenerate/re-stamp | Generated interior, deck and mesh output files | Required work / mesh geometry consequence |
| --- | --- | --- | --- | --- |
| Side Dragger | `R/sideDraggerIsoRig.js` | `W/sideDraggerIsoRig.side_dragger.walk.json` | `I/SideDraggerIso.asset`; `D/SideDraggerIso.asset`; `M/SideDraggerIsoHullMesh.asset` | Add measured prep interaction; import existing singles. Re-stamp yes. No new berth/cooktop geometry required; pipeline bake/import still runs. |
| Stern Trawler | `R/sternTrawlerIsoRig.js` | `W/sternTrawlerIsoRig.stern_trawler.walk.json` | `I/SternTrawlerIso.asset`; `D/SternTrawlerIso.asset`; `M/SternTrawlerIsoHullMesh.asset` | Same metadata/interaction work; re-stamp yes, new mesh geometry not inherently required. |
| Stern Trawler Mk2 | `R/sternTrawlerMk2IsoRig.js` | `W/sternTrawlerMk2IsoRig.stern_trawler_mk2.walk.json` | `I/SternTrawlerMk2Iso.asset`; `D/SternTrawlerMk2Iso.asset`; `M/SternTrawlerMk2IsoHullMesh.asset` | Same, measured against this rig; re-stamp yes, new mesh geometry not inherently required. |
| Convertible | `R/sportFisherIsoRig2.js` | **Both** `W/sportFisherIsoRig2.convertible.walk.json` and `W/sportFisherIsoRig2.skybridge.walk.json` | `I/SportFisherConvertibleIso.asset`; `D/SportFisherConvertibleIso.asset`; `M/SportFisherConvertibleIsoHullMesh.asset`, plus the three Skybridge outputs below | Bed, partition and galley fit-out changes force a geometry re-bake for the edited variant. Both sidecars must be re-stamped because their source script is shared. |
| Skybridge | `R/sportFisherIsoRig2.js` | **Both** sport-fisher walks above | `I/SportFisherSkybridgeIso.asset`; `D/SportFisherSkybridgeIso.asset`; `M/SportFisherSkybridgeIsoHullMesh.asset`, plus the three Convertible outputs above | Single-bed refit forces a geometry re-bake; both variants need source-stamp refresh/import even if one variant's faces are unchanged. Coordinate one shared-script change. |
| Express Cruiser | `R/cruiserIsoRig.js` | `W/cruiserIsoRig.express_cruiser.walk.json` | `I/ExpressCruiserIso.asset`; `D/ExpressCruiserIso.asset`; `M/ExpressCruiserIsoHullMesh.asset` | Single-pad/joinery and real prep-board changes force a geometry re-bake; re-stamp yes. Crawl access remains a separate prerequisite. |
| Go Fast | **None for the recommended day-boat outcome.** A rejected redesign would begin at `R/goFastIsoRig.js`. | **None required by this audit.** If redesigned: `W/goFastIsoRig.go_fast.walk.json`. | If redesigned: `I/GoFastIso.asset`; `D/GoFastIso.asset`; `M/GoFastIsoHullMesh.asset`. | No pretend second bunk/prep anchor. A different shell/accommodation brief would force geometry re-bake, walk regeneration and re-stamp. Importer-only mattress metadata could be added separately without changing the rig digest. |
| Motor Yacht 84 | `R/motorYacht84IsoRig.js` | `W/motorYacht84IsoRig.motor_yacht_84.walk.json` | `I/MotorYacht84Iso.asset`; `D/MotorYacht84Iso.asset`; `M/MotorYacht84IsoHullMesh.asset` | Twin-pad/base fit changes force a geometry re-bake; prep interaction and all source pins re-stamped. |
| Expedition Yacht 120 | `R/expeditionYacht120IsoRig.js` | `W/expeditionYacht120IsoRig.expedition_yacht_120.walk.json` | `I/ExpeditionYacht120Iso.asset`; `D/ExpeditionYacht120Iso.asset`; `M/ExpeditionYacht120IsoHullMesh.asset` | Single-pad/base and board/station changes force a geometry re-bake; all retained cooktop/prep pairs and source pins regenerated. |

Shared logic/data tooling needed before closing any of these source exemptions:

- [`HullWalkSidecarImporter.cs`](../../Assets/_Project/Code/Tools/Editor/RigBaking/HullWalkSidecarImporter.cs):
  join each `sleep` interaction to `anchors.berths[].interact`, carry the
  independent mattress polygon/top/head into the existing bunk fields, and
  supply a measured obstruction on the correct imported level. The walk levels
  currently carry holes, not all furniture boxes. Do not invent a level for an
  excluded crawl space or treat the mattress as standing sole. If the current
  schema cannot express required furniture semantics, propose that contract
  explicitly rather than patching generated assets.
- [`BoatInteriorPrepAndBunkTests.cs`](../../Assets/Tests/EditMode/RigBaking/BoatInteriorPrepAndBunkTests.cs):
  remove each hull from `OwedByTheRigWalk` only after its real data satisfies
  same-level prep distance, clear reaches and mattress containment; keep every
  assertion. Add logic cases to
  [`HullWalkImportParityTests.cs`](../../Assets/Tests/EditMode/RigBaking/HullWalkImportParityTests.cs)
  for the imported mattress fields and multi-berth identity, and integration
  coverage in
  [`HullWalkImportWiringTests.cs`](../../Assets/Tests/EditMode/RigBaking/HullWalkImportWiringTests.cs).
- [`tools/rig-recipes/hull-walk.mjs`](../../tools/rig-recipes/hull-walk.mjs#L32-L41):
  its default `BOATS` table currently covers **five** of these nine: Go Fast,
  Express Cruiser, Side Dragger and the two Stern Trawlers. Add the two
  sport-fisher variants and both yachts, or explicitly
  call exported `deriveWalk` with those four descriptors. A `--hull` selector
  for an absent descriptor processes zero hulls and is not regeneration proof.
  Also preserve the correct character-fit writer revision: the committed Motor
  Yacht walk carries **10.4** QA, while this script still exports **10.3**.
  Extending the descriptor list alone must not overwrite that newer QA with an
  older character contract. Reconcile the writer before regenerating that hull.

### Digest and bake contract

The pin is the **entire rig file**, not a room hash:

1. [`deriveWalk`](../../tools/rig-recipes/hull-walk.mjs#L315-L326) calls the rig's
   own `walk()` and stamps `derivedFromRigSha256` with its LF-normalized SHA-256.
   Therefore even a metadata-only change to `walk()` requires regeneration and
   re-stamping. Never hand-edit the generated JSON or its digest.
2. [`HullWalkSidecarReader.Read`](../../Assets/_Project/Code/Tools/Editor/RigBaking/HullWalkSidecarReader.cs#L64-L69)
   refuses a nonmatching rig digest. The
   [hash helper](../../Assets/_Project/Code/Tools/Editor/RigBaking/DeckSidecarReader.cs#L131-L154)
   accepts equivalent LF/CRLF checkout forms, not arbitrary stale rig contents.
3. [`Populate`](../../Assets/_Project/Code/Tools/Editor/RigBaking/HullWalkSidecarImporter.cs#L104-L108)
   stores that digest in `BoatDeckDef.DerivedFromRigSha256`; its
   [interior stamp](../../Assets/_Project/Code/Tools/Editor/RigBaking/HullWalkSidecarImporter.cs#L138-L142)
   writes the same value to both `HullRigSha256` and `InteriorRigSha256` and
   records the hull script as both source rigs. Those serialized outputs must
   be refreshed together. A shared sport-fisher script consequently invalidates
   both variants' walk/deck/interior pins.
4. [`RigMeshAssetBaker`](../../Assets/_Project/Code/Tools/Editor/RigBaking/RigMeshAssetBaker.cs#L1405-L1449)
   stores the main mesh and door meshes as subassets in the named `HullMesh.asset`
   and then imports the walk. This is the existing bake/import route; no separate
   loose `.mesh` file or sprite-sheet re-bake is implied. `HullMeshDef` carries
   the source path/face builder and layout schema, not an independent rig SHA
   that forces vertices to change after a metadata-only edit.

Distinguish **running the bake/import pipeline** from **changing mesh geometry**.
Prep-only source edits and deriving existing mattress metadata force source-pin
refresh/import, but may yield identical mesh geometry. Changing a pad, support,
partition, hob or board forces a genuine geometry re-bake and later art review.
An importer-only improvement can populate existing berth data with no rig edit
and therefore no sidecar re-stamp. Existing visual references are preserved by
[`Import`](../../Assets/_Project/Code/Tools/Editor/RigBaking/HullWalkSidecarImporter.cs#L35-L65);
the already-wired `Assets/_Project/Data/Boats/Visuals/<asset stem>.asset` files
need no manual changes for these proposals. Include them only if a measured
reference change is actually required. Preserve existing `.meta` identities.

## 4. Evidence limits and acceptance for a later implementation

This audit reads source data and evaluates each hull's `geometry()` without
rendering. Recorded `qa.character` counts above belong to the committed walk
fixtures (Motor Yacht 84 uses character 10.4; the other eight use 10.3); they are
not new Unity test results. A recorded 30/30 sleeping-body
fit does not prove standing access, two occupants, collision clearance or a
working gameplay sequence. No Unity editor, plate, art approval or CI result is
claimed for this document-only change.

All nine walk rig digests match their current rig source bytes, allowing LF
normalization. A read-only call to the writer with explicit descriptors also
exposes the Motor Yacht QA mismatch: its existing 10.4 metadata differs from
what this 10.3 writer derives. The difference is a tooling prerequisite to
record and resolve, not permission to re-stamp or downgrade the committed fit.

All seven proposed prep reaches with explicit coordinates above were checked
against the current same-height walk polygons and their holes: each is inside
the named galley/salon/cabin/house area. This proves a player-centre point is
currently admitted, not that a useful cutting-board surface, appliance clearance,
or route with carried goods has been fitted. The Convertible counter needs its
own two measured station points as part of the refit.

Before implementing a candidate, the responsible art/tooling work must demonstrate
two separate supported mattresses with stable fixture identities, correct pillow
ends, independent accessible reaches, and no collision with doors, stairs or
goods routes. Refitted sleep fixtures must fit all supported builds, including
the offset/rotated berths the current writer can misplace. Every retained cook
action needs a nearby same-level prep action on a real surface; food is carried
between them by hand. Explicit off-walk/crawl restrictions remain until the
movement prerequisite exists. The three workboats can retain their existing
single-bed geometry; the five leisure candidates need the changes described
above. Go Fast remains the one rejected two-single/interior-galley layout in
this brief. No art asset or test expectation is re-baselined by this audit.
