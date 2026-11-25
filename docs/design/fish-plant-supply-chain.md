# Fish plant and supply chain

Status: logic and data proposal, 2026-11-12. The industrial plant belongs in the
Nine Mile Creek region (`region.nine_mile_creek`). This document chooses no site,
building, road geometry, sprite, or scene placement. The isolated model is a
foundation for the requested NPC chain; player enterprise remains LATER. It is
not registered in the live game, and does not change the opening economy.

## Main audit

Audited base: `12bc04b19a2efb33e352b4eea58104f7a5599be1`.

| Area | Existing classes / Defs and actual behaviour |
|---|---|
| Prices | Economy's `Market`, `MarketMath`, `SellPricing`, `SellService`, `MarketId`; Core's `GameConfig` and `FishCategory`. `Market.RegisterSale(category, count)` adds supply; `SupplyOf`, `DemandFor`, `NextUnitPrice` expose it. `SettleDaily` clears a configured fraction on `DayStarted`. Supply is category-wide, not species-specific. Price level and glut absorption are separate. |
| Species | Fishing's `FishSpeciesDef` (`AtlanticCod`, `AmericanLobster`, among others) supplies stable species id, base value and elasticity. Core's `CatchItem` keeps each fish's species, weight and freshness. A lot must retain individual size; a tonne of anonymous fish is insufficient. |
| Buyer | `FishBuyer`, `WharfSellPoint`, `CounterSellDesk`, `SellService`; the Nine Mile Creek builder configures `MarketId.NineMileCreek`. The wharf's lobster buyer has an NPC routine and a truck, but there is no separate lobster production/buyer Def. Buyer identity is not a new market implementation. |
| Money and NPC sales | `SellService.SellSpecies`/`SellAll` remove catch from `IHold`, register market supply, credit `IWallet.Add`, and publish `CatchSold`. These are the only production callers of `Market.RegisterSale` on this base. NPC fleet landings and the buyer's scheduled truck do not currently perform an inventory sale, debit an NPC account, or alter this market. Never describe their motion as an existing supply-chain transaction. |
| Hands / catch | Core's `ICarriable`, `ICarrier`, `IHandLoad`, `IHold`; Player's `CarriableCatch`, `CarriableBucket`, `ClamBucket`; Economy's `CarriedHold.From` finds the container actually in hand. `ShipHold` is boat inventory. Counter sales use delivered pails and retain the empty pail. |
| Storage | `GinnyFreezer` is a finite `IHold` with frozen freshness, not an industrial warehouse. `DeckContainerDef` / `DeckContainerPresenter` (including `container.fish_tray`) expose fill and ice/lid policy; they do not supply truck payload or industrial storage dimensions. `Freshness`, `HoldFreshness`, `StorageMode` remain the cold-chain groundwork. |
| Tote groundwork | Editor-only `GoodsContainerDef`, `GoodsContainerSpec`, `GoodsHandlingDef`, `GoodsCarryModel`, `GoodsVehicleLoad` and research assets `PortableTote65Research`, `PortableTote70Research` model volume, mass, opening, hands and support. Editor-only `LandedCatchProfile`/`LandedCatchState` study live/dead catch. These are research, not runtime tote ownership or production inventory. Do not import their editor assembly into runtime. |
| Trucks | `VehicleDef` assets include `Dually3500` (`vehicle.dually_3500`) and `ConvBox` (`vehicle.conv_box`). `VehicleTripDef` / `FishBuyerRun` (`trip.nmc_fish_buyer_run`), `ScheduledTrip`, Core's `VehicleTripPlan` describe a deterministic timetable, driver walks and travel. They own no cargo. `RouteDriver` is the live driving path, separate from the scheduled pose path. |
| Roads / routes | World's `RouteDef` exists, with St Peters walking/lane assets. Nine Mile Creek's truck paths are derived by `NineMileCreekTrips`, `NineMileCreekRoads`, `NineMileCreekGroundPlan` and the road-network data. No committed `RouteDef` represents a wharf-to-plant or inter-region freight road. A footpath must not be passed off as one. Existing buyer trip is a reference precedent, not permission to redirect its driver. |
| Production | `Data/Recipes` has no production content; there is no runtime `ProductionRecipe` or processing service. The economy design describes those future systems. Terrain/bake recipe classes are unrelated. |
| Clock | `IGameClock.TotalSeconds`, `DayIndex`, `Season` and Core's `GameTime` are the time seam. There is no shared economy slow-tick scheduler on this base. The model accepts absolute game seconds and runs fixed data-configured steps, including catch-up; it never reads wall time. |

## Inventory contract and physical work

A named storage has finite gross mass, usable volume and lot-slot capacity. Truck
cargo is itself a named storage. A typed lot groups only fish of the same species,
individual raw mass, processing form and product mass. A production adapter must
retain the individual catch records behind that lot; size must not be averaged
away. Processing produces product and an explicit species-mass byproduct ledger.
Input fish equivalents are counted once throughout, including work in progress
and consumed/exported product. Product mass plus byproducts must equal landed mass.

The permitted path is wharf -> truck cargo -> intake -> work in progress -> cold
or dry store -> outbound truck -> outlet stock -> consumption. Export enters a
separate off-island ledger only after the loaded carrier crosses the boundary.
There is no direct wharf-to-plant or plant-to-outlet operation.

Every load/unload needs a carrier's explicit hand-transfer receipt, docked at the
correct end. A receipt covers completed work by a named worker, limited by the
run's per-step hand capacity. Time passing alone cannot move cargo. Departure and
arrival are separate acknowledgements; unloading cannot occur in the loading tick.
Blocked deliveries remain on the truck. Empty runs cannot conjure goods.

The model's volume and mass limits are provisional data for logic proof, not
accepted tote geometry or a new physical packing standard. Before live wiring,
the goods adapter must enforce actual tote dimensions, gross hand load, support,
fill sprites and worker reach; then issue receipts. No model API grants a truck
permission to drive over the St Peters sandbar. That run waits for a world-owned
road/ferry/accessible transfer route and suitable tide gate.

## Incoming runs and intake

Three separately named landing storages and truck runs originate at Nine Mile
Creek, Rice Point (`region.rice_point`) and St Peters (`region.st_peters`). Runs
reference an existing vehicle id. They may reference an existing trip id as a
precedent, but route geometry remains unresolved until World supplies a valid
freight route; an unresolved route blocks the live adapter. No new roads are built.

Intake is a fourth bounded storage in the Nine Mile Creek plant. A daily intake
mass budget is distinct from its instantaneous storage capacity. The budget resets
on the absolute game-day boundary. Trucks wait when either limit is exhausted;
partial unloading is permitted only for individually counted fish that fit.
The wharf buyer declines physical intake when its own storage cannot accept it.

## Processing and stores

One `FishPlantDef` references line, outlet, storage and run Defs. Every entity has
its own asset and stable id. Tunables live in these Defs, including fixed-step
duration, calendar length, daily capacity, yield, batch size, labour, process time,
storage limits, hand-transfer rates and delivery time.

| Line | Input / output | Store | Data |
|---|---|---|---|
| Fresh fillet | Groundfish individuals -> fillet portions retaining source size/species | Cold | Yield in integer parts per million, seconds per batch, worker count, hand time |
| Frozen | Groundfish individuals -> frozen fish packs | Cold | Independent yield, time and labour |
| Canned | Groundfish individuals -> canned fish lots | Dry | Independent yield, time and labour; packaging consumables are a later extension, not invented inputs |
| Lobster | Lobster individuals -> cooked/chilled lobster lots | Cold | Separate species restriction, yield, time and labour; never feed lobster through the fillet line |

Only a staffed line starts. Its input stays in its bounded work slot until time
has elapsed AND output space is free. A blocked output cannot discard fish or
make the line available early. Byproducts have an explicit terminal accounting
ledger in the initial model; physical waste storage/removal is required before
live operation. No spoilage is silently destroyed; future spoilage adds a named
disposal ledger and freshness provenance, not a count decrement.

Cold and dry stores have separate capacity. Cold products cannot be routed to a
dry store. Lot slots, mass and volume must all fit; smaller yield does not create
more fish. The first implementation must prove per-species fish counts AND grams,
not merely a combined all-species total that can hide substitution.

## Outlets and money

At least six independent `FishOutletDef` assets: two groceries, two restaurants,
one hotel and one export customer. Each owns a bounded stock storage, one product
and species demand, a seven-day demand schedule, four seasonal demand multipliers,
and price paid per source-fish product unit. The initial content deliberately
includes fresh, frozen, canned and lobster buyers. Fractional demand is rounded
once on day start. Unsatisfied demand expires at midnight; it does not accumulate
unbounded orders. Consumption is limited to delivered stock and that day's demand.

Outbound deliveries are runs with finite cargo and explicit hand work, just like
incoming runs. Local outlets consume after delivery; they never pull directly
from the plant. The export run retains its cargo on the island until its carrier
reports arrival beyond the island boundary. Off-island deliveries and later export
consumption remain distinguishable. Revenue is booked once on accepted delivery,
using the outlet's authored price. This is a chain ledger, not a credit to the
player wallet or an unbacked transfer from an NPC account.

The seed can choose deterministic priority among equivalent outlet requests;
canonical fixed steps make results independent of slow-tick polling frequency.
Replay requires the same initial lots and time-stamped hand/arrival receipts as
well as seed and target game time: inventory is path-dependent, like the market.
No save format or save writer is introduced by this proposal.

## Full and starved plant: market seam

At the end of a step publish each wharf's species backlog, available acceptance,
and blocked state through a Core market interface. An Economy-owned adapter maps
species to `FishCategory`, and new landed supply to `Market.RegisterSale` exactly
once. It uses `SupplyOf`, `DemandFor` and `SetCategoryDemand` for pressure, and
`NextUnitPrice` / `SellPricing` for the quote. Do not invent a second pricing curve.

A full plant blocks trucks, then fills wharf storage. Accumulating landed supply
depresses the buyer's quote through the existing glut formula; constrained demand
can strengthen that pressure. A starved plant starts no batches and sells no
imaginary output; its adapter can restore authored category demand as capacity
becomes available. Daily recovery remains owned by the existing market. Repeated
backlog snapshots must never call `RegisterSale` again for the same fish. A test
adapter must exercise the existing `Market` API to prove that the backed-up quote
falls; a fake hand-written price formula is insufficient.

## Integration ownership and proof gates

Core owns market-pressure and hand-carrier contracts. Economy must implement the
market adapter and buyer acceptance/payment transaction. Vehicles must implement
the dock, travel and boundary receipts. World must provide valid route ids and
route access, particularly for St Peters; Player/goods must provide physical hand
transfers and individual catch provenance. App owns a future slow-tick composition
root. These remain PARKED integration work; no feature module reaches into another
module's concrete classes. No scene, prefab or existing Def value changes here.

Named EditMode proof: conservation on every tick (all species and grams), replay
determinism across polling frequencies, starvation, full-plant wharf backup and
price fall through `Market`, each outlet kind's daily/seasonal demand, export
boundary, and a busy zero-allocation tick. Content tests load the actual new assets,
check unique ids and all references, and validate bounds/compatibility. With no
editor lease, native C# proof covers the pure model; Unity asset loading and
production `Market` tests must be reported separately as CI proof pending.

## Player entry points — LATER

- Sell delivered individual catch to the plant intake once licensed and reachable.
- Own and drive trucks, then hire drivers after doing loading and unloading by hand.
- Lease or own a processing line, then manage staffed lines and store upgrades.
- Negotiate grocery, restaurant, hotel and export contracts with real deliveries.

These are M3 enterprise/M4 freight design hooks. No ownership, staff hiring,
management UI, player payout, automated route or export scene is shipped here.
