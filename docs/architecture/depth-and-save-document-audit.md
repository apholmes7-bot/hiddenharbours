# Depth convention and save-document audit

The executable save schema is **26** at `d46bde303e6aa256d23d47a05ede5d7d6f38e51e`.
Several documents retain dated introduction versions or proposed field sets. Those
are distinguished below from statements that incorrectly describe current main.
This audit changes no save field, format, capture path or player save.

## Depth correction, pending the owning change

`docs/architecture/tech-architecture.md:106` reverses the subtraction. Its corrected
sentence is:

> Local water depth = waterLevel - terrainElevation; positive depth means water
> above the bed, zero means the surface meets the bed, and negative depth means
> exposed ground. Both input elevations are metres above the same chart datum.

The source is the Core `float` helper
[`TidalExposure.WaterDepth`, lines 54-60](../../Assets/_Project/Code/Core/Environment/TidalExposure.cs#L54).
`IEnvironmentService.WaterLevelAt(double)` supplies the water-surface elevation and
by default returns `TideHeightAt`. A seabed elevation below datum is negative;
water depth is positive when the surface is above that elevation. A rising surface
increases depth; a rising bed decreases it.

PR 1516 has merged, but the direct sentence edit remains **parked** because open
PRs 1025, 1182, 1183 and 1243 also change the architecture file. The replacement
above is ready for that file's next authorized change. It does not need a
runtime or save change. `TidalExposureTests` already tests basic subtraction and
wet/dry boundaries; four additional parameterized cases exercise the Core service
seam at signed elevations and under common datum shifts, preserving those rules.
The previous head passed automated run 45343967952. No local compilation or
EditMode execution ran for this update; the merged head needs fresh execution.

## Executable save authority

[`SaveMigration.CurrentVersion:16`](../../Assets/_Project/Code/Core/Save/SaveMigration.cs#L16)
is 26. [`SaveData`](../../Assets/_Project/Code/Core/Save/SaveData.cs) contains the
following 61 root members, grouped only for readability:

- `SchemaVersion`, `WorldSeed`, `GameTimeSeconds`, `Money`, `DayIndex`, `OwnedBoats`, `ActiveHullId`.
- `OnboardingFlags`, `OwnedLicenses`, `RepairedBoats`, `OwnedGear`, `PlacedTraps`, `BaitStock`, `PotStock`.
- `PlacedLonglines`, `TubStock`, `BoatHoldCatch`, `BucketCatch`, `FreezerCatch`, `SupplyStock`, `HullInstruments`.
- `HullSounderPrefs`, `HullFuel`, `NavWaypoints`, `NavRoute`, `NavTrack`, `PlayerNotes`, `WornOutfitId`.
- `HullChartplotterPrefs`, `HullRadarPrefs`, `RestRegion`, `RestPosX`, `RestPosY`, `RestLevel`, `RestBedId`.
- `RestWokeAtSeconds`, `RestAboard`, `RestBoatHullId`, `RestBoatRegion`, `RestBoatPosX`, `RestBoatPosY`, `RestBoatRotation`.
- `PlayerRecipe`, `PlayerName`, `SpentClamHoles`, `PlayerTraits`, `PlayerNeeds`, `PlayerBody`, `Messes`.
- `NextMessSerial`, `PlayerCleanliness`, `KitchenStores`, `CaughtSpecies`, `NpcGiftDays`, `CookJobs`, `HullLoranWaypoints`.
- `StoredContainers`.
- `StorePurchases`, `SpecialOrders`, `NextSpecialOrderNumber`, `ShoppingBags`.

Rows added at v24 are defined in `Core/Save/SaveRowsV24.cs`; container rows at v25
are in `Core/Save/SaveRowsV25.cs`. Their introduction dates are not current-version
claims. There are no `MarketStates`, `EconomyLedgerRows`, `HullSailSet` or general
physical-goods location/hand records in this root. Current player-save state must
also be distinguished from authored Defs, rig sidecar schemas and UI preferences.

PR 1516 merged as `5ac27e352b`: its four v26 root members are now installed, with
their row types in `Core/Save/SaveRowsV26.cs`. PR 1543 (`328ff145c9`) then wired
store purchases and shopping bags to capture and restore without another bump.
Special-order rows and their sequence number remain reserved on this base; the
order-book behavior in open PR 1503 is not part of this audit's baseline. V26 did
not add general economy-ledger, crew, limited-licence-holder or physical-hand
records. The island-economy and limited-licence designs must be read with that
narrow shipped scope, rather than treating all their proposed records as saved.

## Document inventory

The scan covered documentation under `docs/` (`.md`, `.txt`, `.rst`), matching save
versions/schema language, SaveData/SaveMigration and root/row member references.
The original 92 candidates are retained, with three current references added for
the first-week rule and installed shelf/bag persistence. The affected rows are
reconciled to the pinned base above. Historical introduction versions remain
historical; a declaration alone does not prove that runtime capture is wired.

| Document | Does it match pinned main? |
|---|---|
| [adr/0008-save-schema-and-versioning.md](../adr/0008-save-schema-and-versioning.md) | Historical match: v1 introduction and its eight root fields remain valid; v1 is not the current version. |
| [adr/0009-tidal-exposure-and-region-display-name-seams.md](../adr/0009-tidal-exposure-and-region-display-name-seams.md) | No schema inventory: explicitly adds no saved tide or display-name state. |
| [adr/0014-painted-seabed-height-authoring.md](../adr/0014-painted-seabed-height-authoring.md) | No schema inventory: painted elevation changes add no saved fields. |
| [adr/0016-additive-2d-lights.md](../adr/0016-additive-2d-lights.md) | No schema inventory: a rendering identity is explicitly unsaved. |
| [adr/0018-shared-wave-field.md](../adr/0018-shared-wave-field.md) | No schema inventory: wave/presentation state is explicitly recomputed or unsaved. |
| [adr/0019-hand-authored-scenes-and-refresh.md](../adr/0019-hand-authored-scenes-and-refresh.md) | No schema inventory: editor scene placement is outside the player save. |
| [adr/0020-world-placed-object-persistence.md](../adr/0020-world-placed-object-persistence.md) | Historical match: v2 to v3 introduced PlacedTraps; the claim that trap runtime is absent describes that old slice. |
| [adr/0021-in-engine-js-rig-baking.md](../adr/0021-in-engine-js-rig-baking.md) | No schema inventory: bake output is an authored asset, not save state. |
| [adr/0025-ui-rig-runtime-rendering.md](../adr/0025-ui-rig-runtime-rendering.md) | No schema inventory: presentation rendering adds no saved fields. |
| [adr/0029-character-colour-runtime-structure-baked.md](../adr/0029-character-colour-runtime-structure-baked.md) | Historical snapshot: schema 14 was the CW-01 baseline; main is 26 and PlayerRecipe now exists. |
| [adr/0030-per-hull-instrument-ownership.md](../adr/0030-per-hull-instrument-ownership.md) | Historical match: v8 HullInstruments/HullSounderPrefs and the v9 range addition remain present. |
| [adr/0036-interior-levels-as-layers.md](../adr/0036-interior-levels-as-layers.md) | STALE: the claim of no saved position or region is contradicted by RestRegion, RestPosX, RestPosY and RestLevel. |
| [adr/0037-rest-anchor.md](../adr/0037-rest-anchor.md) | Historical match: the four v13 rest fields remain; v20 later extended the sleep-place record. |
| [adr/0039-quiet-hud.md](../adr/0039-quiet-hud.md) | No field/version inventory: references the versioning ADR for future grants. |
| [adr/0050-helm-footprint-seam.md](../adr/0050-helm-footprint-seam.md) | Match of absence: helm-footprint geometry is not a SaveData field. |
| [adr/0051-building-a-person-without-js.md](../adr/0051-building-a-person-without-js.md) | Historical match: v14 to v15 added PlayerRecipe; WornOutfitId remains. |
| [adr/0052-event-handler-isolation.md](../adr/0052-event-handler-isolation.md) | No schema inventory: handler isolation changes no saved fields. |
| [adr/0055-goods-are-carried.md](../adr/0055-goods-are-carried.md) | Proposal: physical goods persistence is a future contract, not an installed save-field list. |
| [adr/0060-rigged-dwelling-interiors.md](../adr/0060-rigged-dwelling-interiors.md) | No schema inventory: dwelling geometry adds no saved fields. |
| [adr/README.md](../adr/README.md) | Historical index: v1 names the original ADR, not current main. |
| [adr/drafts/campaign-coop-proposal.md](../adr/drafts/campaign-coop-proposal.md) | Proposal: multiplayer/session records have no installed schema here. |
| [architecture/data-model.md](../architecture/data-model.md) | MIXED: historical v2 through v26 additions match their fields. V26 correctly distinguishes installed bag/purchase capture from reserved special orders. Generic PlayerState/FleetState/EconomyState/BusinessState/WorldState shapes remain broader than the shipped root. The file is also changed by open PR 1553 and is left untouched here. |
| [architecture/module-coupling/README.md](../architecture/module-coupling/README.md) | No schema inventory: a future snapshot-boundary review is requested. |
| [architecture/module-coupling/fishing-to-economy.md](../architecture/module-coupling/fishing-to-economy.md) | No schema inventory: identifies responsibility for future persistence changes. |
| [architecture/region-scene-churn-audit.md](../architecture/region-scene-churn-audit.md) | No schema inventory: reload observations do not define new saved fields. |
| [architecture/routine-clock-reload-audit.md](../architecture/routine-clock-reload-audit.md) | No schema inventory: reload-risk audit explicitly does not prescribe new fields. |
| [architecture/tech-architecture.md](../architecture/tech-architecture.md) | MIXED: v25 containers and the v26 root declarations match. The v26 claim that no gameplay reads or writes the new rows is stale after #1543: purchases and bags are wired; special orders remain reserved. Section 6 overstates market, business, staff, skills and property persistence. Depth subtraction is still inverted. Open PRs 1025, 1182, 1183 and 1243 also change this file. |
| [art/recreation-boats-intake.md](../art/recreation-boats-intake.md) | Match: OwnedBoats and ActiveHullId exist; no new schema is claimed. |
| [art/rigs/charters/hand-cart.md](../art/rigs/charters/hand-cart.md) | Proposal: cart identity, support, load and grip persistence are not installed save fields. |
| [art/rigs/modern2500-kit/README.md](../art/rigs/modern2500-kit/README.md) | Not a player-save version: schema v2 belongs to the vehicle sidecar format. |
| [art/rigs/modern350-kit/README.md](../art/rigs/modern350-kit/README.md) | Not a player-save version: schema v2 belongs to the vehicle sidecar format. |
| [authoring-scenes.md](../authoring-scenes.md) | Not a player-save claim: saved project assets are discussed. |
| [design/autopilot-slice-2.md](../design/autopilot-slice-2.md) | Match: HullInstruments and v24 HullLoranWaypoints exist with the listed waypoint members; route/hold state is not thereby saved. |
| [design/campaign-coop-plan.md](../design/campaign-coop-plan.md) | Historical snapshot, not current: v17 baseline; the single-player money, hull, recipe, notes and rest fields still exist in v26. |
| [design/diegetic-devices.md](../design/diegetic-devices.md) | MIXED: v8 instruments and nav/flags fields match. No task state is too broad: PlayerNotes persists IsTask and Done, though no authored TaskDef progression is installed. |
| [design/diegetic-instruments-and-consoles.md](../design/diegetic-instruments-and-consoles.md) | Match: watch ownership uses OwnedGear. Proposed window-layout preferences are not SaveData fields; this is distinct from saved instrument preferences. |
| [design/diegetic-ui-and-inventory.md](../design/diegetic-ui-and-inventory.md) | STALE blanket absence: placed traps/longlines and container contents now persist. General physical item/hand/support identity still needs a separate schema. |
| [design/economy-and-business.md](../design/economy-and-business.md) | Match: OwnedLicenses, OwnedGear, RepairedBoats and PotStock are retained; v2/v4 are introduction versions. |
| [design/economy-item-sellers.md](../design/economy-item-sellers.md) | Proposal: a future paid product may require ownership persistence; no installed field set is claimed. |
| [design/fish-and-content.md](../design/fish-and-content.md) | Match: PlacedTraps exists; the no-new-fields statement concerns the described fishing slice. |
| [design/fish-plant-logic.md](../design/fish-plant-logic.md) | Proposal: future inventory/receipt persistence is not a shipped field declaration. |
| [design/fish-plant.md](../design/fish-plant.md) | Historical scope: v24 is named as separate work, not current main; no plant-specific save records exist. |
| [design/fishing-methods.md](../design/fishing-methods.md) | Match: PlacedTraps, BaitStock and PotStock are present. |
| [design/fuel-and-refuelling.md](../design/fuel-and-refuelling.md) | Historical match: HullFuel was introduced in v14 and remains; 13 to 14 is not the next migration. |
| [design/goods-handling/00-today.md](../design/goods-handling/00-today.md) | Match: aggregate catches retain SpeciesId, Quantity and freshness fields; individual catch identity/rolled dimensions are not saved there. |
| [design/goods-handling/03-data-model-proposal.md](../design/goods-handling/03-data-model-proposal.md) | Proposal: item/container/location records are future fields; the three legacy catch lists exist. No version is reserved. |
| [design/goods-handling/06-phasing.md](../design/goods-handling/06-phasing.md) | Proposal/phasing: physical goods migration is planned work, not an installed schema. |
| [design/goods-persistence-proposal.md](../design/goods-persistence-proposal.md) | Historical snapshot plus proposal: CurrentVersion 17 is not current; aggregate catch facts remain, proposed physical-goods records are absent. |
| [design/gym-workouts.md](../design/gym-workouts.md) | MIXED: main already has v24 is stale as a current-version statement. Saved PlayerBody supports fitness; workout credit/session state remains unsaved. |
| [design/helm-audit.md](../design/helm-audit.md) | No save-field inventory: UI layout preferences are distinguished from schema changes. |
| [design/helm-instruments-logic.md](../design/helm-instruments-logic.md) | Match: per-hull ownership uses HullInstruments; no second ownership/save list is needed for the described contract. |
| [design/licence-bait-migration-decisions.md](../design/licence-bait-migration-decisions.md) | No schema inventory: migration decision constraints do not install another format. |
| [design/limited-licences-and-crewing.md](../design/limited-licences-and-crewing.md) | STALE current-version statement: main is now v26, not v25. The proposed licence/crew records are still absent and require a separately approved later save change; v26 added none of them. |
| [design/living-fish-capture-phases.md](../design/living-fish-capture-phases.md) | Proposal: capture persistence requires a later migration, not current fields. |
| [design/living-fish/02-landed-fish.md](../design/living-fish/02-landed-fish.md) | Proposal: fish location/vitality design explicitly is not a save-schema change. |
| [design/living-fish/08-occupant-transactions.md](../design/living-fish/08-occupant-transactions.md) | Historical snapshot: v17 is not current; HeldCatchDto and PlacedTrapDto field descriptions still match. |
| [design/living-fish/09-owner-questions.md](../design/living-fish/09-owner-questions.md) | No schema inventory: a decision page expressly grants no save-format change. |
| [design/loran-navigation.md](../design/loran-navigation.md) | Match: v24 HullLoranWaypoints and its seven listed members are present. |
| [design/m1-progression-pacing.md](../design/m1-progression-pacing.md) | Historical match: SupplyStock was introduced at v7 and remains. |
| [design/market-supply-persistence.md](../design/market-supply-persistence.md) | Match of current absence plus proposal: SupplyStock is player consumables; MarketStates and the proposed market rows are not installed. |
| [design/multiplayer-scoping.md](../design/multiplayer-scoping.md) | Historical snapshot, not current: repeated v10/current-version and v10 migration starting points must not be reused for v26. |
| [design/mussel-lease-and-longline.md](../design/mussel-lease-and-longline.md) | PROPOSAL / MIXED: OwnedLicenses and baited fishing-longline placement/timing are existing reusable machinery (SaveData.cs:905-920; Fishing/LonglineCatch.cs:24-77). The mussel design explicitly remains unbuilt (line 10): lease ownership, mussel placement, seed choice and crop resume (lines 115-151) are proposed, not installed persistence. Fishing hooks, bait and tubs do not store a mussel crop. |
| [design/npc-trawler-fleet-plan.md](../design/npc-trawler-fleet-plan.md) | Proposal: fleet/truck persistence is a future requirement, not a declared main field set. |
| [design/npcs-and-routines.md](../design/npcs-and-routines.md) | Match: v24 NpcGiftDays has NpcId, DayIndex and Count and is used by gifting. |
| [design/progression-and-housing.md](../design/progression-and-housing.md) | Design intent: versioned resume is required, but no concrete current field set or version is specified. |
| [design/region-seam-audit.md](../design/region-seam-audit.md) | Match: legacy region ids are migrated; this is not a new version claim. |
| [design/sail-controls.md](../design/sail-controls.md) | MIXED: HullFuel since v14 is correct. Proposed HullSailSet is absent; a future 14 to 15 bump is stale because 15 is already used. |
| [design/seabed-bottom-type.md](../design/seabed-bottom-type.md) | No schema inventory: authored seabed type is not new saved state. |
| [design/shop-catalog-and-dialogue-choices.md](../design/shop-catalog-and-dialogue-choices.md) | No schema inventory: a later ownership change requires its own persistence decision. |
| [design/shop-stock-rules.md](../design/shop-stock-rules.md) | DORMANT MODEL: MemoryShopStockStateStore and its proposed count/settled-day persistence remain unconnected. This is separate from the installed island-shelf purchase rows in StorePurchases; it must not be read as saying all store purchases disappear on reload. Retirement is proposed separately in #1524. |
| [design/simulation-determinism-audit.md](../design/simulation-determinism-audit.md) | No schema inventory: determinism audit changes no saved fields. |
| [design/st-peters-key-scenes/wave-1b-placement.md](../design/st-peters-key-scenes/wave-1b-placement.md) | No schema inventory: scene placement and perch runtime state add no saved fields. |
| [design/st-peters-living-economy.md](../design/st-peters-living-economy.md) | MIXED HISTORICAL/SHIPPED: the original next-free-v26 wording is explicitly qualified by the appended built-state note. StorePurchases, shopping bags and reserved special-order rows are installed; general EconomyLedgerRows, daily glut and broader NPC income records remain absent and would need a later version. |
| [design/time-tides-weather.md](../design/time-tides-weather.md) | Match: SpentClamHoles stores hole id and spent game time; tide-cycle state is recomputed. |
| [design/trawl-fishing-logic.md](../design/trawl-fishing-logic.md) | Proposal: trawl logic snapshot is not an installed save schema. |
| [design/watch-types.md](../design/watch-types.md) | Match of absence: watches use existing gear ownership; no separate wearing field is installed by this slice. |
| [design/yacht-race-integration-plan.md](../design/yacht-race-integration-plan.md) | Proposal: race integration explicitly does not authorize saved state. |
| [design/yacht-racing.md](../design/yacht-racing.md) | Match of absence: race and retirement session state is not in SaveData. |
| [docs-drift-audit.md](../docs-drift-audit.md) | Match: the prior audit correctly identifies executable v25 and stale schema summaries, at its separately stated baseline. |
| [economy/nmc-wholesalers-stock.md](../economy/nmc-wholesalers-stock.md) | Match: HullInstruments, OwnedGear and ActiveHullId are existing fields; future product decisions do not add persistence. |
| [fixture-wall-reach-proposal.md](../fixture-wall-reach-proposal.md) | Proposal: transient interaction snapshots need no saved fields. |
| [marketing/first-trailer-assessment.md](../marketing/first-trailer-assessment.md) | Not a player-save claim: a capture manifest is an authored asset. |
| [roadmap.md](../roadmap.md) | Historical/design outline: v1 is a milestone and seed/time/playerState is shorthand, not the current concrete v26 field list. |
| [tooling/per-frame-cost-inventory.md](../tooling/per-frame-cost-inventory.md) | Match: navigation and HullFuel references point to existing fields; HullFuelDto is a value type. |
| [tooling/settled-plate-variance-findings.md](../tooling/settled-plate-variance-findings.md) | No schema inventory: test measurements add no save fields. |
| [tooling/water-test-methods.md](../tooling/water-test-methods.md) | Match of absence: tide/wind/weather are excluded from the save; references test coverage rather than listing a version. |
| [verification/character-wardrobe-baseline-2026-09-13.md](../verification/character-wardrobe-baseline-2026-09-13.md) | Historical snapshot only: v14 and absence of PlayerRecipe described the named old baseline; main is 26 and includes PlayerRecipe. |
| [verification/fishing-behaviour-coverage.md](../verification/fishing-behaviour-coverage.md) | Not a schema inventory: WorldSeed and DayIndex are fixture inputs; no version claim. |
| [verification/managed-allocation-guards.md](../verification/managed-allocation-guards.md) | No schema inventory: allocation guards add no saved fields. |
| [world/camper-home-plan.md](../world/camper-home-plan.md) | STALE planned step: v25 rack/container persistence is now installed as StoredContainers, with richer rows than the proposed pair; v24 cooking jobs also persist. |
| [world/kitchens-bathrooms-audit.md](../world/kitchens-bathrooms-audit.md) | No schema inventory: the room audit adds no saved fields. |
| [world/washrooms-plan.md](../world/washrooms-plan.md) | Match: remaining soap washes use SupplyStock, not a new field. |

| [adr/0062-the-first-week-is-closed-by-weather.md](../adr/0062-the-first-week-is-closed-by-weather.md) | Existing progress flags carry the proposed first-week facts; this ADR introduces no new save version or root member. The next-morning gate still needs runtime wiring. |
| [design/island-supply.md](../design/island-supply.md) | MATCH: player purchases are stored as StorePurchaseRowDto rows and replayed into the deterministic shelf model. The shelves themselves are recomputed, not saved as stock snapshots. |
| [design/shop-at-the-shelf.md](../design/shop-at-the-shelf.md) | MIXED HISTORICAL/SHIPPED: the top retains the original v26 plan; later sections describe the now-saved shopping bag. Special-order behavior is still marked unbuilt, while its DTOs and sequence number exist. |

Only this audit and the existing depth test suite change. The list is a static
comparison of field declarations and documentary claims, not a save round-trip,
player experience, or assertion that every listed subsystem captures/restores all
its runtime state. No save file was read or written.
