# Supply-run tide source audit

At base `832c6f78f9e607cfbdede4845a3d205551439c91`, the shipped St Peters sea and the
ledger tests agree: mean **0 m**, amplitude **2.2 m**, phase **1 game hour**, using the
same shipped clock and tide configuration. There is no tide-source correction to park
for this start scene. This is a source and arithmetic finding; the new EditMode test
has not been executed locally.

The ledger and shelves added in #1456 and #1473 are still unconnected to gameplay at
this base. They accept a day-feasibility delegate, not a water service. Their shipped
tests derive that delegate from the region profile through `IslandSupplyTestData.Water`.
Thus agreement today does **not** establish that a future runtime supply runner is
wired to the shared sea. Keep that distinction when connecting the store.

## Read inventory

Locations below refer to the audited base unless labelled new. `C/` means
`Assets/_Project/Code/`; `T/` means `Assets/Tests/EditMode/`.

| Reader / handoff | File and lines | Water source or behavior |
|---|---|---|
| Window scan | `C/Core/IslandSupply/CrossingMath.cs:51-104` | `FindWindows` reads the injected `waterAt` at 64, 71; `BarWindows` supplies the crest threshold, `GutWindows` supplies bed plus wade limit. |
| Window edge refinement | `C/Core/IslandSupply/CrossingMath.cs:107-115` | Reads the same delegate at 109, 113, with 40 bisections per edge. |
| Crossing safety | `C/Core/IslandSupply/CrossingMath.cs:142-163` | Reads water along the whole leg (157-158) and through the gut including endpoints (162-163). |
| Trip searches | `C/Core/IslandSupply/CrossingMath.cs:173-244` | Round-trip and one-way queries delegate to `BarLegFits` at 187-188, 215; daylight search delegates at 244. No independent tide. |
| Run calendar | `C/Economy/Island/IslandRunCalendar.cs:38-53` | Passes injected water to `BarWindows` at 48-49 and daylight round-trip search at 52. Pads the search by a day to include windows across midnight. |
| Ledger | `C/Economy/Island/IslandStoreLedger.cs:68-110,154,186` | Stores `runPossible`, forwards it through `FromDef`, then reads `IslandRunCalendar.RunsOn`; no water-height call or profile. |
| Shelves | `C/Economy/Island/IslandStoreShelves.cs:99-105,144-166` | Forwards feasibility into its ledger; `RunCameOn`, `DaysSinceRun`, and settlement read the ledger. No water-height call or profile. |
| Shared shipped-test source | `T/IslandSupply/IslandSupplyTestData.cs:53-57` | Resolves `route.IslandRegionId`, constructs the region's profile, then calls `TideModel.Height(t, profile, cfg)` directly. |
| Supply-plan fixture | `T/IslandSupply/IslandSupplyPlanTests.cs:30-36,64,76,98,113-115,132-138` | Loads `Water` at 34; all bar/gut windows, repeatability, one-way capacity, round trips and daylight queries use that delegate. |
| Shipped ledger fixture | `T/IslandSupply/IslandLedgerShippedTests.cs:60-62,81-87` | Both existing tide tests pass `Water` into `DaylightRunDays`, then inject the result into the ledger. |
| Shipped shelves fixture | `T/IslandSupply/IslandShelvesShippedTests.cs:37-41,89,118,130` | Every `Build` call supplies the same helper through `DaylightRunDays`. |
| Synthetic ledger tests | `T/Economy/IslandStoreLedgerTests.cs:47,61,71,143,161,177,197` | Constant or arithmetic day predicates; no water sampling. These verify scheduling and stock rules, not service agreement. |
| Synthetic shelves tests | `T/Economy/IslandStoreShelvesTests.cs:26,62-72,84,125,184,242` | Constant or arithmetic day predicates; no water sampling. |
| Content validator | `T/IslandSupply/IslandSupplyContentValidationTests.cs:118-124` | Pins route geometry to builder geometry; does not read tide height. |

Repository-wide call-site searches for the crossing methods, `DaylightRunDays`, the
ledger/shelves constructors and `FromDef` found no runtime consumer outside these
calculator classes. `SupplyPlanMath` only computes loads/capacity and never samples water.
The source guidance in `CrossingMath.cs:10-15`, `IslandSupplyConfig.cs:10-13`, and
[island-supply.md](island-supply.md) describes the region-profile path, not a service binding.

## What the shared sea reads

- `C/App/GameRoot.cs:36,62` publishes its serialized environment reference to
  `GameServices.Environment`. `ProjectSettings/EditorBuildSettings.asset:8-10` starts
  with St Peters. Its serialized service at `Assets/_Project/Scenes/StPeters.unity:90649-90654`
  has the shipped config, seed 12345 and profile `(0, 2.2, 1)`.
- `C/Core/Services/IEnvironmentService.cs:41` defaults `WaterLevelAt(t)` to
  `TideHeightAt(t)`. `C/Environment/EnvironmentService.cs:152-153` evaluates that with
  its active profile and config; its current sample uses the same model at 106.
- `Assets/_Project/Data/Regions/StPeters.asset:29-31` independently stores `(0, 2.2, 1)`.
  `Assets/_Project/Data/Config/GameConfig.asset:17,42-44` supplies 1800 seconds/day,
  12.4206 hours/tide, 28 days/lunar month and 0.45 neap fraction to both paths.
- `C/Environment/TideModel.cs:21-37` is a function of time, profile and config only.
  It has **no seed input**. Both paths therefore describe the same world's tide at
  identical times; the service's seed affects weather, not this water-height function.
- `C/App/Editor/PersistentCoreBuilder.cs:142-145,209-211` documents that the start
  region's profile persists across region hops. The new guard covers the shipped
  St Peters start, not a standalone launch from every other region or future water offsets.

## One additional guard and verification limits

Existing coverage was found in `IslandLedgerShippedTests`, `IslandShelvesShippedTests`
and `IslandSupplyPlanTests` before editing. The one new test extends
`T/IslandSupply/IslandLedgerShippedTests.cs`:
`ShippedSeaAndLedger_HaveIdenticalCrossingWindowsAcrossALunarMonth`.

It reads the actual service referenced by the shipped scene's core in a temporary
preview scene, independently of the region-profile helper. It calls the service
through `IEnvironmentService.WaterLevelAt`; it neither rewrites the profile nor runs
the clock. The preview is closed in `finally`, without saving. Both sources feed the
same crossing algorithm over `[0, LunarMonthDays * SecondsPerDay)` at the route's
one-game-minute search step. A month covers two complete spring-neap envelopes.
The test requires nonempty bar and runner-gut windows, exactly equal window counts
and every start/end time, then equal daylight feasibility and ledger delivery decisions
for every day. Failures report profiles, seed, the queried span and actual differing
window edges or day. Existing assertions and pinned supply rules remain intact.

Offline execution of the unchanged `TideModel`, `CrossingMath` and `IslandRunCalendar`
sources, with data-only stand-ins loaded from the shipped assets, found **54 identical
bar windows, 54 identical runner-gut windows and 28 identical daily decisions**. Neither
source permits a daylight trip on days **12, 13, 14, 27**; scheduled deliveries occur on
**0, 2, 4, 6, 8, 10, 15, 16, 18, 20, 22, 24, 26**. Shifting only the service-side profile
by one hour changes window edges, confirming the comparison is sensitive to phase drift.

That offline check did not instantiate the environment service or load a scene in Unity.
No local Unity/EditMode run was authorized for this audit; CI must establish that the
new test actually ran (one case, four cases total in `IslandLedgerShippedTests`). No
game code, asset, scene, configuration value, save format or existing expectation changed.
