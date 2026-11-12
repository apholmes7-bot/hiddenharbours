# Fish plant logic proposal

This is an isolated NPC supply-chain model and unplaced content. It implements
the logic foundation of the [fish plant design proposal](https://github.com/apholmes7-bot/hiddenharbours/pull/1126).
Nothing instantiates it in normal play; no scene, prefab, save format, existing
Def value, appearance or player economy changes. The full plant's location is
`region.nine_mile_creek` only, with no coordinates.

`FishPlantDef.BuildSpec` takes managed snapshots of the individual storage, line,
outlet and truck run assets and validates their references. It copies row fields
directly and detaches outlet calendar arrays, without a JSON serialization roundtrip.
Construct `FishChainModel` once and
feed `AdvanceTo(IGameClock.TotalSeconds)` rounded down to an integral game second
from the future slow-tick composition root. It catches up every fixed step, in
order, and rejects time reversal. Day zero is weekday index zero. Calendar length
and the starting season/day offset come from the referenced shared `GameConfig`;
season labels use Core's existing `GameCalendar.AtDay`. The initial data uses the
existing 1800-second day, rather than assuming 86400 seconds per game day. Fractional
day seconds or a non-seven-day week are explicitly refused during setup. All
capacity/labour/price tunables are asset data. Definitions
and model state must be confined to the sim thread; a constructed spec is immutable.

Replay needs the same initial lots, time-indexed landings, worker assignments,
carrier acknowledgements and hand receipts as well as seed and target time.
This is path-dependent inventory, not a world-seed-only fish generator. Equivalent
slow-tick polling frequencies run the same fixed steps. The seed rotates delivery
priority by day. Truck loading also rotates lot priority to avoid starving a
species behind another species' large lot. Line priority advances after a batch starts so
small incoming loads cannot permanently starve the frozen or canned line behind
the first eligible line. Setup may allocate; normal ticks do
bounded array scans without LINQ, new collections or transient strings.

Fish lots retain species, individual source mass, individual current mass,
individual occupied volume and count. Different sizes cannot merge. Processed
counts mean source-fish equivalents, not can counts: packaging is a later
extension. Yield rounding is integral per individual; its complement enters a
species byproduct mass ledger. Products, work in progress, delivered stock and
consumed product retain source counts. Byproduct mass is counted once, so both
fish equivalents and mass remain conserved. The byproduct ledger is terminal
accounting; a physical waste store and removal route are required before live
operation. Freshness and unique catch identities must remain in the future
physical goods adapter, rather than being averaged away in a lot.

Wharf, cargo, intake, cold, dry and outlet storages have independent mass, volume
and lot-slot limits. Line work slots have mass, volume and batch-count limits.
Capacity is checked before a hand receipt is requested. Workers start empty;
insufficient workers pause the line's time and hand work. A full output store
retains its uncompleted raw batch. Demand is an integer per day after applying
the weekday schedule and four seasonal multipliers; shortages expire at midnight.
Outbound loads reserve only the day's outstanding demand. A delayed truck may
carry an earlier day's stock, which can be consumed against the current day's
demand after delivery. Revenue is booked once on accepted delivery; no wallet is
credited. Export is a boundary arrival on its run, with a separate cumulative
export counter; the destination has no fabricated region asset.

Core exposes `IFishChainHands`, `IFishChainCarrier`, `IFishChainMarket` and the
cumulative `FishChainPressure` snapshot. All inventory transfers require completed
physical hand work; time alone cannot load or unload. The carrier must permit
loading and separately acknowledge arrival after the authored minimum travel
time. Returned trucks must complete their return before loading again. Export
arrival means the carrier actually left the island. A receipt cannot exceed the
requested fish count. Interfaces must be deterministic for their recorded inputs,
nonthrowing, and allocation-free on a tick.

## Parked integration contracts

| Implementer | Required implementation / exact remaining choice |
|---|---|
| Economy market / buyers | Implement `IFishChainMarket` using `Market.RegisterSale` deltas, category mapping, existing demand and quote APIs; decide the authored demand-pressure tuning. Snapshots must never register the same landing twice. Gate buyer payment and removal on a successful physical intake transaction. |
| Vehicles and World | Implement `IFishChainCarrier` for valid docked trucks, cargo ownership, return legs and export boundary crossing. Supply the actual freight route ids and inter-region access gates. In particular, what valid road/ferry/transfer route lets the St Peters run reach the mainland? The walking sandbar is not permission for a truck. |
| Player / goods | Implement `IFishChainHands` using actual workers, reachable supported storages, real totes and fish identities; preserve freshness and enforce tote opening, occupied space and hand-load limits. |
| App / architecture | Compose a slow-tick instance after these ports exist; define persistence and replay of inventory/receipts in a separately reviewed save-format change. No save writer is included. |

All new route ids are deliberately blank: there are village `RouteDef` assets,
but none is a plant freight route. A production carrier must refuse blank or
unresolved routes. Vehicle references resolve `vehicle.dually_3500`; the NMC
inbound run references `trip.nmc_fish_buyer_run` as a timetable precedent only.
It does not take over that driver's live schedule. Each new run has exclusive
cargo and each outlet has one incoming run. These ports and missing routes are
PARKED for their module owners; they do not justify cross-module concrete calls.

The new industrial capacity and yield values are provisional logic data. They do
not change the measured dimensions or balance of an existing Def, and they are
not proof that a pictured truck or store can carry those loads. Physical packing,
waste handling, freshness, accepted art and live travel are gates before activation.
Player plant sales, truck/line ownership, hiring, contract UI and fleet automation
remain LATER under the enterprise and freight phases.

## Verification

`FishChainModelTests` covers per-tick conservation of both species counts and
grams, deterministic replay across polling intervals, empty/full plants, exact
daily/seasonal demand at all six outlets, boundary-gated export, missing hands or
routes, blocked output, unstaffed lines, storage limits, and snapshot field preservation
and isolation from source rows and calendar arrays. Its allocation test
first calibrates the counter with a retained allocation, then measures busy
transfer/processing/demand steps; an unusable counter is a reported skip.

`FishChainUnityTests` loads the actual 41 new Def assets, checks the six outlet
kinds/counts, stable ids and existing species/vehicle/trip/region references. It also
compares every constructed row field with the corresponding imported Def and checks
that row objects and calendar arrays are detached. Its
full-plant test drives the existing `Market` API via a test adapter and proves a
lower quote plus exactly-once supply registration. The adapter is test-only; live
integration remains parked. No existing rule assert or plate is weakened.

With no editor lease, pure tests run through a temporary native .NET NUnit harness;
Unity managed assemblies provide an additional compile check. A disk audit checks
asset/script GUIDs and existing content ids, and runs `FishChainSpec.Validate` on
the parsed authored content in that same native harness. Unity's actual asset
import and `Market` lifecycle tests remain CI proof pending until their cases run.
