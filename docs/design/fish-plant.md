# Fish plant: intake, grades and line work

Status: isolated M1 logic foundation; no scene wiring or live economy change.
Serves P3 (a working coast) and P4 (deliver by hand, learn work, later hire).

## What main already supplies

Audit base after review merge: `236b10e3c5f99cce0e745e42a838cc5a72592931`.

| Concern | Existing files and reuse |
|---|---|
| Selling | `Assets/_Project/Code/Economy/SellService.cs` quotes and sells individual catch through Core `IHold` / `IWallet`, preserves refused fish, registers category supply and raises `CatchSold`. `CarriedHold.cs`, `CounterSellDesk.cs` and `WharfSellPoint.cs` provide physical delivery precedents. Reuse these contracts in a future adapter; do not call both selling paths for the same receipt. |
| Market | `Economy/Market.cs`, `MarketMath.cs` and `SellPricing.cs` provide category supply/demand, outlet price levels, marginal glut pricing and daily settlement. A plant's provisional price per kg is a separate buying contract, not a replacement for per-fish market prices. Future market reporting must register the accepted fish once, not once per tick or again on dispatch. |
| Catch and size | `Core/Economy/CatchItem.cs` retains species, individual weight, value and freshness state without referencing Fishing. `Fishing/FishSpeciesDef.cs` authors min/max weight, base value and spoil rate. Weight is size evidence here; length and legal-size grading are not invented. Keep one fish per delivered receipt; never average different sizes into an anonymous lot. |
| Freshness | `Core/Economy/Freshness.cs` computes spoil from explicit game time, species rate, storage mode and policy; `Core/Economy/SpoilContext.cs` applies the live refusal gate. A future adapter must settle expiring ice protection and reject unsellable fish before converting remaining freshness to integer ppm for intake. This component accepts the already evaluated freshness, never reads a global clock. |
| Production | `Economy/SupplyChain/FishPlantDef.cs`, `FishLineDef.cs`, `FishChainSpec.cs`, `FishChainModel.cs` and `FishLotStore.cs` already implement an isolated NPC chain with bounded stores, yields, workers and hand receipts. Its 41 unplaced assets and `FishChainUnityTests` remain unchanged. It does not carry freshness or grade settlement; do not discard provenance to feed graded lots into it. `Core/Economy/IFishChainWork.cs` supplies the reusable `FishProduct` identity and future physical hand/carrier seams. |
| Staff | `Core/Crew/StaffRoleDef.cs`, `StaffRoleBook.cs`, `ICrewRoster.cs` and `CrewPolicy.cs` describe boat roles and task membership. They are not plant jobs, wages, hiring or shift schedules. Reuse the Core task-contract approach later rather than declaring a boat skipper to be a fillet worker. |
| Cannery beat | `App/Editor/StPetersCannery.cs` documents and places the derelict island cannery; `Core/Relationships/RelationshipEvent.cs` reserves the restored-employer event. `docs/roadmap.md` M3 and `docs/design/npcs-and-routines.md` reserve enterprise/restoration. This is the long-arc introduction, not an existing playable factory job. The operating town plant and the derelict island cannery have distinct identities. |

Code paths above abbreviate `Assets/_Project/Code/` after the first row.
See also [the supply-chain design](fish-plant-supply-chain.md) and
[its implementation notes](fish-plant-logic.md). This narrow component adds grading,
not a second fleet, warehouse or market simulation.

## Two fish-plant models

Both models are isolated proposals; neither runs the plant scene today.

| Model | Already built | Missing before scene use |
|---|---|---|
| `Economy/SupplyChain`: `FishPlantDef`, `FishLineDef`, `FishChainModel`, `FishLotStore` | A fixed slow step, hand-transfer receipts, species/size lots, authored yields, workers, bounded mass/volume stores, carriers and outlet demand. | Individual receipt identity, freshness, grades and player intake settlement. Equal-sized lots merge without those distinctions. Physical hands/carrier/market adapters, supported storage and waste removal, and persistence remain uninstalled. |
| `Economy/PlantIntake`: graded intake and line | Per-fish hand-delivery receipt deduplication, grade and price, freshness carried on the source receipt, one-time loading, timed work and exact product/byproduct mass with provenance. | Freshness decay during storage/work, physical handover and collection, wallet/market settlement, volume-bounded queues, workers, carriers, outlet demand and persistence. It is one slot, not the larger supply chain. |

A bridge needs one processing authority to prevent double payment or cutting.
Estimated scope starts with four existing runtime files:
`Core/Economy/IFishChainWork.cs`, `SupplyChain/FishLotStore.cs`,
`SupplyChain/FishChainModel.cs` and `PlantIntake/FishPlantIntake.cs`, plus a new
Economy adapter, App composition, both model test suites and new bridge tests.
Physical goods/carrier adapters add further files. New reviewed save rows must
atomically retain receipts/settlement, individual lot/grade identity, freshness
and protection, locations/mass/waste, recipes/work completion, workers, demand
and carrier progress. Merged lots cannot recover lost provenance.

Retirement scope: SupplyChain has eight source files, 41 Def assets and two test
fixtures; PlantIntake has five source files, five assets and two fixtures, plus
metas and identity declarations. Audit references, preserve recorded ids/GUIDs
under the identity contract and retain shared Core `FishProduct` uses. Neither
has saved plant state today. After installation, replacement needs a migration
for receipts, inventory and work in flight, or an approved retirement policy.

**Recommendation for the first plant scene:** use the graded intake and single
line after physical delivery, settlement and persistence review. It fits hand
intake and preserves grade, freshness and individual provenance. Keep SupplyChain
uninstalled for later logistics pending a bridge or retirement. The seat and
owner decide; this recommendation does not approve scene wiring.

There is also a third "fillet" on main: the `Core/Food` prep-station recipes
`recipe.cod_fillet` and `recipe.groundfish_fillet` output `food.cod_fillet` and
`food.white_fish_fillet`. `RecipeRunner` runs that cooking/prep path; those food
ids differ from plant `FishProduct.FreshFillet`; conversion needs quantity, yield,
provenance and saved-cooking-job rules.

## Player path and the 21 zones

The route follows the shell proposed in PR 1276, whose files are untouched.
Enter reception, speak at the office counter, carry a catch/container along the
receiving aisle to intake I1, and hand it over at the delivery edge. Staff weigh
each fish and evaluate freshness. The quote names species, weight, grade and
payment; refused fish stay with the player. Acceptance consumes the physical
delivery receipt exactly once and produces the settlement and a graded lot.
The player receives payment before leaving. Nothing pulls fish from a distant
boat, pocket, store or truck.

M1 below means the intended first intake interaction, not an interaction shipped
by this PR. All 21 floor zones are design assignments; no art is accepted here.

| Shell zone | Job | Phase |
|---|---|---|
| Reception | Explain today's accepted species and intake terms | M1 intake |
| Office | Counter quote and payment; later records and hiring | M1 counter; records/hiring later |
| ReceivingAisle | Walk the carried load to I1 without entering production | M1 intake |
| IntakeI1 | Hand-delivered small catches and empty-container return | M1 intake |
| DeliveryEdge | Supported handover between visitor and receiving worker | M1 intake |
| WeighAndGrade | Weigh, inspect freshness, accept/refuse and issue receipt | M1 intake |
| RawChilledBuffer | Finite raw storage; preserve fish identities and cold history | Later |
| FilletLine | Hand-load graded fish, cut over game time, retain product and waste | M1 logic primitive only; staffed scene later |
| CanningLine | Separate recipe, packaging inputs and heat process | Later |
| Packing | Pack and label physical products by grade and provenance | Later |
| DispatchStaging | Hold supported outbound packages until hand collection | Later |
| FinishedColdStore | Finite cold storage with actual refrigeration policy | Later |
| LobsterCell | Live/cooked shellfish handling, separate acceptance and welfare | Later |
| Refrigeration | Power, plant cooling and breakdown consequences | Later |
| DryStore | Packaging materials and shelf-stable finished cans | Later |
| ReturnsWash | Return and wash empty totes; never consume the player's bucket | Later |
| IntakeI2 | Bulk receiving from actual arriving carriers | Later |
| OutboundDock | Load dispatch carriers by hand and acknowledge departure | Later |
| PlantServices | Supported utilities, maintenance and physical waste removal | Later |
| StaffEntranceWash | Job access, changing and hygiene | Later |
| StaffRoom | Breaks and shift routines | Later |

Later, the player can apply at the counter, wash in, learn a line by doing its
hand tasks, and only then employ workers. Whether the player can take paid shifts
is an owner question, not an enabled job system. No employee, shift or wage is
created by the M1 component.

## Data and runtime contract

New data types: `FishGradeDef` (`fishgrade.*`), `FishIntakeDef`
(`fishintake.*`), and `GradedFishLineDef` (`gradedfishline.*`). Each entity is
one nonvisual asset. Grades author minimum freshness ppm and integer currency
units per kg. The intake references grade assets in strictly descending order,
accepted species ids and maximum individual delivery grams. No grade threshold,
species, price, capacity, duration or yield is hard-coded in logic.

The sample accepts only `fish.atlantic_cod`. Its prime/standard/low grades start
at 850000/500000/150000 remaining-freshness ppm, paying 12/8/3 currency units per
kg. These are provisional isolated test content, not approved prices or a licence
to buy spoiled fish. The live refusal gate can be stricter. Round the total
down once per fish (`grams * price / 1000`), allowing a sub-unit payment of zero;
do not impose a hidden minimum price. Weight and freshness endpoints are exact.
Equal thresholds, duplicate grade/species ids, ascending prices and malformed
data are refused at construction. Runtime specs detach from editable Def arrays.

`FishPlantIntake.TryAccept` accepts an explicit completed-hand-delivery receipt,
not an inventory search. It refuses duplicates, invalid/nonpositive/over-capacity
mass, unknown species, invalid freshness and fish below all bands. Refusal creates
no lot or payment and does not burn a receipt id. Acceptance returns a graded
single-fish lot retaining source receipt, species, exact grams, freshness, grade,
delivery time and settlement amount. It calculates money; it does not mutate a
wallet, hold or market. One intake instance owns its session's receipt registry.
The caller must never fabricate a new receipt for the same fish.

`GradedFishLine` is a single explicitly loaded work slot. It consumes a graded
lot once, including across two line instances. Its separate Def authors species,
product, capacity, yield, process seconds and slow-tick seconds. It produces at
the first absolute slow-tick boundary at or after load time plus process duration.
Polling more often cannot accelerate it; sparse polling reaches the same result.
Time reversal is refused. Seed has no influence because this transform has no
randomness. Recipe data is snapshotted at setup, not read every frame.

Durations and timestamps are **clock seconds** on `IGameClock.TotalSeconds`,
the same convention as `Core/Food/RecipeDef.WorkSeconds`. The historical
`ProcessGameSeconds` and `ReadyAtGameSeconds` names do not mean seconds on an
86400-second day. With `GameConfig.SecondsPerDay = 1800`, a 60-clock-second
process lasts `60 / 1800 * 24 * 60 = 48` game minutes; a 5-clock-second slow tick
spans 4 game minutes. At normal speed while unpaused, 60 clock seconds take
60 real seconds; pause and time scale govern clock advancement. Completion can
wait until the next slow-tick boundary after that duration.

Completion keeps output in the slot until an explicit collection call. A full
slot refuses new input. Product grams are floored from source grams times yield
ppm, and the exact complement is retained as byproduct grams; source grade and
identity survive. This is production inside the slot, not a storage or transport
operation. Collection is a logic acknowledgement only; a future scene adapter
must prove completed hand work, reach, support and capacity before calling it.
No clock event moves goods between zones. No live adapter exists in this PR.

## Built, designed only, and persistence boundary

Built: grade/price and recipe Defs, detached validated specs, receipt-based intake
settlement, a single graded processing slot, logic tests and content validators.
The sample line makes cod fresh fillet with 450000 ppm yield, 60 clock seconds
duration, a 5-clock-second slow tick and a 20000-gram individual capacity. Values are
proposal data and no asset is loaded by a runtime bootstrap or Resources loader.

Designed only: the visitor/counter interaction, all hand work and visible storage,
wallet and supply registration, refrigerated queues, bulk delivery, dispatch,
packaging, waste handling, plant employment and ownership. Existing NPC-chain
assets remain a separate proposal until a grade-preserving bridge is designed.

No save fields or migrations are added (save v24 / PR 1277 stays separate).
Live integration would need persistent accepted receipt ids and settlement status
atomically with catch removal and wallet credit; unique catch-to-lot provenance;
raw/product/waste locations and quantities; freshness/protection history; line
input identity, recipe identity/version, load/completion times and collected state.
Hiring would additionally need job assignments, shifts, wages and permissions.
Do not install this session-only model in normal play before those decisions.

Open owner questions: approve per-grade prices and freshness bands; choose the
accepted species (shellfish need separate handling); decide whether a player can
work a paid line shift and the unlock/wage. No question blocks this isolated core.
