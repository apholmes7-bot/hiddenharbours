# Nine Mile Creek fish plant: key-scene brief

Status: documents-only handoff for the scene queue. Audited at
`00b030cf8d286bc9d3b1464acd55c025e5a26a32`; the industrial logic from PR 1127
is present, but is not registered in the live game. Site A is selected:
parcel centre **(-255,-40)**, site **x [-295,-215], y [-70,-10]**, building
**x [-289,-241], y [-54,-26]**. This is an inland, truck-fed working plant
selling to groceries, restaurants, hotels and abroad. It is not another
landing quay or a replacement for the wharf's small cold store.

The [court study](nmc-fish-plant-truck-court.md) proves a small-truck envelope
on paper without moving the building. **Build toward its rank-1 layout:**
26 m east court, 12 m clear gate at (-215,-40), one moving truck, one waiting
on the widened spur to Route 91 at (-190,-40). The forward-only horseshoe is
an explicit fallback, not an extra simultaneous route.

This brief follows the [H-series package ownership contract](nmc-key-scenes/README.md#deferred-owners)
and the scale/working-frontage treatment in the
[wharf layout](nine-mile-creek-wharf.md#3-layout) and
[yard intake](nine-mile-creek-yard-intake.md). It does not add a thirteenth
placed key-scene record, revise the H1 identity map or take a scene writer's
lease. H5/scene queue owns placement; H13 owns eventual cast/routines; art
owns the look. All footprints and times below are proposed authoring values,
not new Def rows or installed behaviour.

## 1. Programme within the real footprint

Use the court study's local frame: **u = x + 241, v = y + 40**. The east
facade is u 0; the building is **u [-48,0], v [-14,14]**, one storey,
**1,344 m² gross** at 1 world unit per metre. The table partitions that
rectangle without overlap. Areas include local circulation and partition
allowances; they are not net machinery floor areas. Keep a minimum proposed
1.5 m clear walking/carrying aisle within each work zone when detailing it.
This is a game-space brief, not a certified factory process design.

| Zone | Local rectangle (u; v) | Gross footprint | Function / proposed player access |
|---|---|---:|---|
| Plant services | [-48,-38]; [-14,-8] | 10 x 6 = 60 m² | Water, drains, cleaning stores; outside-only service door. |
| Staff entrance, changing and wash | [-38,-28]; [-14,-8] | 10 x 6 = 60 m² | South threshold (-33,-14); staff/work access, no public shortcut through the line. |
| Staff room | [-28,-18]; [-14,-8] | 10 x 6 = 60 m² | Shift breaks; enterable as a staff/work interior when that phase is enabled. |
| Office | [-18,-8]; [-14,-8] | 10 x 6 = 60 m² | Receiving clerk, dispatch paperwork; public counter side enterable. |
| Reception / delivery lobby | [-8,0]; [-14,-8] | 8 x 6 = 48 m² | Public south door (-4,-14), delivery appointment and viewing point. |
| Raw chilled buffer | [-48,-34]; [-8,-2] | 14 x 6 = 84 m² | Raw fish after grade, bounded intake stock; staff/work access. |
| Finished cold store / freezing cell | [-48,-34]; [-2,6] | 14 x 8 = 112 m² | Fresh and frozen product separated internally; freezing work position; staff/work access. |
| Lobster handling cell | [-48,-34]; [6,10] | 14 x 4 = 56 m² | Species-separated handling and product storage; staff/work access. Do not invent a live-tank simulation. |
| Preparation / fresh fillet line | [-34,-18]; [-8,0] | 16 x 8 = 128 m² | Washing, grading follow-through, cutting and fresh line; controlled work interior. |
| Processing / canning line | [-34,-18]; [0,10] | 16 x 10 = 160 m² | Preparation, filling, process equipment and cooling positions; controlled work interior. |
| Weigh and grade | [-18,-6]; [-8,2] | 12 x 10 = 120 m² | Scale, species/size lots and intake receipt desk; delivery strip enterable, work area staff-only. |
| Packing | [-18,-6]; [2,10] | 12 x 8 = 96 m² | Packing tables and returnable containers; staff/work access. |
| Front receiving aisle | [-6,0]; [-8,-2] | 6 x 6 = 36 m² | Connect lobby to weighing and the intake thresholds; player delivery route. |
| Intake I1 | [-6,0]; [-2,2] | 6 x 4 = 24 m² | 4 m facade opening centred v 0, rear truck bumper at (0.5,0); delivery access when truck stopped. |
| Returns / container wash | [-6,0]; [2,6] | 6 x 4 = 24 m² | Separate dirty returns from finished product staging; staff/work access. |
| Intake I2 | [-6,0]; [6,10] | 6 x 4 = 24 m² | 4 m opening centred v 8; overflow intake, also the timed outbound truck standing position. |
| Refrigeration / plant equipment | [-48,-34]; [10,14] | 14 x 4 = 56 m² | Exterior service access only; no player machinery controls implied. |
| Dry store | [-34,-18]; [10,14] | 16 x 4 = 64 m² | Canned product, packaging separated by racks; staff/work access. |
| Finished dispatch staging | [-18,-6]; [10,14] | 12 x 4 = 48 m² | Labelled customer lots awaiting physical loading; staff/work access. |
| Outbound dock | [-6,0]; [10,14] | 6 x 4 = 24 m² | Door centred v 12; grocery, restaurant and hotel vans, plus the small export truck, use I2's standing position at v 8 and a 4 m hand-carry leg inside the front dock strip. |

The six-metre dock depths are **inside** the building. Trucks stand outside,
so the programme does not hide a vehicle footprint inside processing space.
The I2 standing position is shared by intake and dispatch **at different
times**, with cleaning and re-staging between uses. It is not two trucks
backed into the same eight-metre gap. Retain both intake openings, but admit
only one truck to the court. A third arrival stays at its origin.

Provide a supported hand-transfer connection across the 0.5 m bumper gap,
with its final height and liftgate arrangement chosen against the selected
truck. The model supplies neither that deck height nor a ramp. Keep the
facade work apron **u [0.5,3.0], v [-4,14]** closed to walking while a truck
moves. At dispatch, workers carry from v 12 to v 8 **inside** the front dock
strip, reserving **u [-3,-1.5], v [6,14]** as their 1.5 m aisle, then cross
the supported I2 connection into the rear. They never route through the
parked body rectangle. Doors and rear hardware must open into the stationary
work reservation.
They carry the goods into the vehicle, which must visibly fill.

### Ground outside the shell

These uses belong **inside the site box but outside the building box**;
parking is not counted as factory floor area:

- Staff parking: **u [-44,-24], v [22,27]**, 20 x 5 m, eight nominal
  2.5 x 5 m spaces; access aisle **u [-46,-22], v [16,22]**, 24 x 6 m.
  Final accessible-space dimensions and car manoeuvres are a detailing task,
  not certified by the truck proof. Staff arrive before the truck window;
  vehicle and pedestrian conflicts need the scene lane's access pass.
- Northern public/staff walking strip: **v [26,28] across u [0,26]**.
  Connect it around the north side of the building into the west strip
  **u [-53,-49]**, then the southern footpath **v [-18,-16]** to the
  reception and staff thresholds. The north link skirts the parking row
  on **v [28,30]**. No visitor walks across the active truck court to find
  the front door. Worker crossings of the parking aisle occur outside truck
  movements and need a clear, authored crossing.
- Southern service strip: retain access for cleaning/byproducts inside the
  site; no loose fish or waste stock along the estate-facing boundary.
  Reserve v [-30,-24] as the quiet southern edge in rank 1, not extra truck
  parking. The estate reservation begins at v -37: its existing 7 m external
  separation remains. The fallback loop needs its own southern exit and
  supersedes the rank-1 service circulation, so it requires an access redraw.
- North and west edges remain farm-facing buffers. The future farmhouse
  at (-200,-8), the plaza, and the southern estates keep their lots. The
  selected plant pad and original spur samples were +6 m in the siting audit;
  that is not a new terrain survey of every widened-spur corner.

## 2. Seamless interiors and what is outside-only

Apply [ADR 0036](../adr/0036-interior-levels-as-layers.md): one physical
footprint, same region, no teleport, scene load, separate screen or camera
cut at the threshold. This programme has **one ground-floor layer**. It
does not invent a larger pocket factory or an upstairs management room.

The initial player route is reception -> office counter -> receiving aisle
-> delivery edge of weigh/grade -> I1 when the truck is stopped. Openings,
partitions and furniture must leave that continuous path in the allocated
rectangles. The working production floor can be read from the delivery edge;
players do not walk through running equipment. Cold storage, packing, dry
store, staff rooms and processing have real allocated interiors, but their
staff/work access is a **later job/ownership hook**, not a new permission
system in this scene task. Closed thresholds give an honest boundary until
that work is commissioned. Services and refrigeration equipment remain
outside-only, even then. Loading court, parking and exterior service doors
are ordinary exterior ground, not interior layers.

The art lane decides the shell, roof removal, machines, carried containers,
fill states and sound. The brief requests readable physical work, but accepts
no appearance and supplies no new plate baseline. No storage prop claims to
contain more than its physical capacity or silently moves inventory.

## 3. A working day, derived from the game clock

The following is an **ASSUMED authoring schedule**, f(clock), not a timetable
already installed in an NPC Def. Use half-open time windows on
`IGameClock.HourOfDay`; a role's base station is a pure function of day/hour.
Actual carrying and driving also depend on recorded work and arrival facts.
Seeded variation must derive from `(worldSeed, dayIndex, roleId)`, never wall
time or global randomness. A missed slot cannot manufacture a completed trip.

| Clock slot | Routine / station | Freight rule |
|---|---|---|
| 05:30–06:00 | Opening shift arrives at staff entrance; supervisor checks intake, cold store and court | Staff cars finish access before freight. No unattended machine start. |
| 06:00–06:30 | Weigh/grade and cold-store preparation; clerk at office | Admit only a ready, physically supported vehicle/driver. |
| 06:30–07:30 | First local wharf load, I1; intake pair weigh and carry | One truck on site, one on spur. Other regions have no arrival slot until their route exists. |
| 07:30–10:00 | Fresh, frozen, lobster and canning crews at their work cells | Lines require workers, real input and free output space; empty lines stay idle. |
| 10:00–10:30 | Clean/re-stage I2, load restaurant van | Finished stock only, carried to vehicle. The buyer's wharf trip is not reassigned. |
| 10:30–11:00 | Grocery van at I2 | Separate customer lot and confirmed departure. |
| 11:00–12:00 | Second local intake window at I1 | Dispatch vehicle leaves before the next truck enters. |
| 12:00–13:00 | Staggered breaks / office receiving | Required-worker threshold applies to each line; a short crew pauses that line. |
| 13:00–14:00 | Processing and hotel van window; I2 only after intake clears | A driver waits for product, never loads an empty stock marker. |
| 14:00–15:00 | Last local intake window | No spillover queue on the road; failed admission stays at origin. |
| 15:00–16:00 | Pack and load export straight truck at I2 | PARKED until an actual off-island carrier route exists; no export from a visual departure alone. |
| 16:00–17:00 | Finish/stage work, clean intake, count retained cargo | Unfinished work remains in process; a full truck stays loaded. |
| 17:00–18:00 | Line crews leave, cleaning/closing shift checks cold store and drains | Handover maintains staffing for any still-active batch; otherwise pause it. |
| 18:00–05:30 | Closed reception; cold stores retain stock, night caretaker check | No invented night production or auto-unloading. Refrigeration is not a new power simulation. |

Roles, pending the H13 cast slate: **2 intake/weigh hands**, **3 fresh-line
workers**, **2 freezing workers**, **3 lobster workers**, **4 canning workers**,
**2 pack/dispatch hands**, **1 clerk**, **1 supervisor/quality role** and
**1 cleaning/maintenance role** = **19 on the full working roster**, plus
visiting drivers. Eight parking places do not imply nineteen cars: walking,
shared rides and shift overlap are the proposed routine choices. Do not assign
an existing named seller to a full plant shift without reconciling their
current routine. Every truck has a driver and every hand transfer a present
worker; driver disappearance during travel is not a second worker on the line.

The 3/2/3/4 production counts match `RequiredWorkers` in the existing fresh,
frozen, lobster and canned line assets. Hours above do not override those
thresholds. `GameConfig` currently uses `SecondsPerDay: 1800`, `DaysPerWeek: 7`,
`DaysPerSeason: 28`. Use that clock's units when translating slots to the
model; do not feed 86,400-second civil days into its 1,800-second day budget.
Catch-up must replay worker changes and receipts at their proper game times,
not apply today's staff count retroactively to every missed step.

## 4. Economy boundary: present foundation and parked integration

The [fish-chain design](fish-plant-supply-chain.md) and
[`Economy/SupplyChain`](../../Assets/_Project/Code/Economy/SupplyChain/)
define `FishPlantDef.BuildSpec`, `FishChainSpec`, `FishChainModel`,
`FishStorageDef`, `FishLineDef`, `FishOutletDef`, `FishTruckRunDef`,
`FishLotStore` and `FishLot`. The chain is an isolated model with explicit
ports, not a placed industrial business. `FishStoreKind`, `FishOutletKind`
and `FishRunPhase` describe stock, customers and transport state within Economy.

The existing plant asset is `fishplant.nmc_industrial`, region
`region.nine_mile_creek`, in
[`Data/FishSupplyChain`](../../Assets/_Project/Data/FishSupplyChain/).
Its `StepSeconds` is **5**, `DailyIntakeGrams` **24,000,000**. Storage caps are:

| Asset / storage | `MassCapacityGrams` | `VolumeCapacityMl` | `LotSlots` |
|---|---:|---:|---:|
| `plant_intake.asset` / `fishstore.plant_intake` | 4,000,000 | 5,000,000 | 128 |
| `plant_cold.asset` / `fishstore.plant_cold` | 12,000,000 | 18,000,000 | 128 |
| `plant_dry.asset` / `fishstore.plant_dry` | 8,000,000 | 12,000,000 | 128 |

The rooms do not derive industrial throughput from their floor area. These
are prototype stock limits requiring physical container validation. The
physical cold/raw zones share the appropriate logical intake/cold capacities;
separate rooms do not multiply those caps. The four
line assets use `BatchFish: 30`; fresh/frozen/canned/lobster
`ProcessSeconds` are **15/20/30/20**, and `YieldPpm` are
**450000/850000/650000/350000**. Canned output goes to dry storage; the other
products go to cold storage. Individual size/species and explicit byproducts
remain conserved; a bin of anonymous mass is not a replacement for fish.

All nine current run assets have `HandFishPerStep: 12`, `HandSecondsPerFish: 1`
and `TravelSeconds: 30`. With a five-second step, their effective hand ceiling
is **min(12,5/1) = 5 fish per step**. `FishChainModel.Run` departs after the
first positive loading step, not after filling a truck. Thus neither a
tonne-capacity cargo Def nor the planned half-hour dock slot proves a full
truckload. Bulk-load dwell/dispatch policy is a named integration question;
do not animate a full vehicle for five transferred fish or silently retune
these values while placing the scene.

Cross-module work must use **Core interfaces**, currently
[`Core/Economy/IFishChainWork.cs`](../../Assets/_Project/Code/Core/Economy/IFishChainWork.cs),
and Core time/inventory contracts. Scene-facing modules must not call an
Economy, Vehicles, Player or World concrete class across their boundary.

| Plant reads / writes | Existing contract and exact boundary |
|---|---|
| Read time, worker attendance and landed catch | App will compose the model from `IGameClock.TotalSeconds` and seed. Economy's `AdvanceTo`, `SetWorkers` and `TryLand` are internal integration entry points, not permission for World/Player to reference `FishChainModel`. Individual catch provenance must be supplied through an approved Core seam; one for staffing/presentation still needs design. |
| Request physical load, unload, feed and take-away work | Core `FishChainTransfer` carries work/source/destination/species/product, individual masses/volume, requested count and game seconds. `IFishChainHands.ConfirmTransfer` returns completed, previously unreported hand transfers, bounded by the request. Zero means goods stay where they are. |
| Read route permission and actual arrival | Core `IFishChainCarrier.CanLoad` refuses an absent vehicle/driver, unresolved route or inaccessible tide gate. `HasArrived` confirms outbound/return arrival, including the island boundary for export. A timer is only a lower bound. |
| Write wharf pressure to Economy | Core `FishChainPressure` and `IFishChainMarket.Report` carry cumulative landed/waiting fish, free intake and blocked state. Economy's adapter computes deltas and uses the existing `Market` methods; repeated reports cannot register the same fish again. |
| Read/write stock and finished product | Economy owns bounded lots and `FishProduct` transitions. Hand adapters enforce supported containers, worker reach, capacity and visible fill. A Core read-only stock/presentation contract is still needed; scene props must not inspect `FishLotStore` directly. |
| Write accepted sales / export bookkeeping | Existing model `Revenue`, `OutletConsumed` and `Exported` distinguish chain revenue, consumption and boundary delivery. They do not credit `IWallet`. Buyer payment and NPC funding remain Economy-owned integration, not a scene event that mints money. |

The six outlet assets already distinguish **creek grocery (fresh)**,
**island grocery (canned)**, **harbour restaurant (fresh)**,
**lobster restaurant (lobster)**, **bay hotel (frozen)** and
**export customer (frozen)**. Demand is daily/seasonal and consumed from
delivered stock; the scene must not make shops pull remotely from the plant.
Export is a road-fed customer chain; no private plant wharf or NMC deepwater
freighter berth is implied.

### PARKED for the responsible scene/integration lane

| Dependency | Required work before claiming a live plant |
|---|---|
| Vehicles + Core | Choose the refrigerated vehicle and physical envelope; reverse-leg/dock control or the forward loop; stop/queue arbitration, arrival receipts and loaded support. Preserve the buyer's existing trip. |
| World + scene queue | Author the widened spur, gate, support, road access and valid route identity. Resolve Rice Point/Cumberland freight and St Peters transfer/tide access separately; the foot bar is not a truck road. No blank route id is considered reachable. |
| Player/goods + Core | Persistent individual catch provenance, containers, hand reach, load/unload receipts and stock presentation. Carry every transfer and show storage filling; no teleport or unrestricted loading animation. |
| Economy + Core | Idempotent market adapter using `Market.RegisterSale`, `SupplyOf`, `DemandFor`, `SetCategoryDemand`, `NextUnitPrice` / `SellPricing`; buyer acceptance/payment; explicit bulk-load departure policy. Existing tests' rule assertions must remain strict when semantics move. |
| App + Core + NPC routines | Compose the slow tick, staffing and scene-facing read contracts; replay time-stamped attendance/receipts for catch-up. There is no permission here to introduce a new save format. |
| Art + scene lane | Shell/interior assets, dock height, doors/liftgate, carried loads, fill states, machines and supported ground. Ground/scene ownership and look acceptance remain with those lanes. |

These are parked implementation boundaries, not reasons to leave this brief
unfinished. M2 may establish the working place and hand-delivery scene when
commissioned. Plant ownership, hired automation and contracts remain M3;
the full export/freight enterprise remains M4. The [roadmap](../roadmap.md)
is not advanced by reserving rooms for those later uses.

## 5. Arrival and the pillars

**By road:** from the southern Route 91 approach the plant is on the west,
beyond the shore estates; from the wharf the turn leaves the road south of
the plaza. The court faces the road. The intended read is a driver waiting
off the carriageway, someone weighing a delivered load, and finished lots
being carried out to a customer van. Staff have their own path and entrance.
At a quiet hour the same space holds closed intake doors and retained stock,
not an endless animation of fish arriving. These are scene intentions,
not verified views or a promise that the southern truck turn is already solved.

**By water:** Site A is inland, southwest of the wharf. The first working
arrival remains the harbour landing; fish leave it on a truck before reaching
the plant. Roof visibility beyond the farms/estates is not established by
plan coordinates. Do not move the plant to the water, clear a sightline or
invent a private wharf to make a silhouette. Let the road journey reveal its
scale. Boats stay supported by water or their proper stands throughout the
existing harbour sequence.

The brief serves [P2, P3 and P4](../vision-and-pillars.md#3-the-five-pillars):
the player can see a larger rung of the trade, a town keeping working hours,
and goods physically carried before later delegation. P1 affects the catch
and route availability upstream; it does not invent flooding at this +6 m
planning pad. P5 comes through waiting, finite cold storage and a lost or
late load, without making a quiet factory visit an invented hazard minigame.

## 6. Open choices, parked without blocking the handoff

| Question | Choices | Lean / consequence |
|---|---|---|
| Which first refrigerated vehicle? | Small cabover-style reefer within the 8 x 2.5 m envelope; larger 9.6 m conventional truck | **Small reefer.** Keep the court proof; selecting the larger truck requires new swept-path work. No vehicle asset substitution is authorized by this lean. |
| Reverse bays or forward-only commissioning? | Implement explicit reverse legs for rank 1; use rank 2 with a second exit and side hand service | **Rank 1** as the lasting scene. If reverse work cannot land first, use rank 2 explicitly and redraw access, never an in-place pivot. |
| Concurrent trucks at two intake positions? | One active court truck plus one spur queue; two docked trucks with independent manoeuvres | **One active truck.** The latter is unproven and must be remeasured; two openings do not grant simultaneous access. |
| Public experience beyond delivery? | Reception/office/intake now, work rooms later; full factory access from opening | **Delivery route now.** Reserve honest rooms without shipping later enterprise/permission systems. |
| Dispatch after five fish or after a bulk load? | Preserve current model departure; introduce explicit completed-load/dwell policy through the ports | **Explicit completed-load policy before live industrial traffic.** No silent value change or fictional full-truck animation. |
| How does export leave the island? | Small truck to a valid transfer/port route; larger direct vehicle/ferry chain | **Small-truck transfer first**, after World supplies a real route and boundary receipt. No transfer destination is invented by this document. |
| Who works the shifts, and what is the business called? | New roster through the cast slate; reconcile existing NPC availability and identities | **Cast-slate assignment**, preserving current sellers. Leave the public business name open; role labels here are design labels, not signs or dialogue. |

Before scene acceptance, run the court study's complete supported-vehicle
sequence and confirm the public threshold route fits the same footprint.
The two documents provide coordinates, room areas, routines and module
boundaries; they do not claim Unity proof, a functioning industrial economy,
or an accepted new look.
