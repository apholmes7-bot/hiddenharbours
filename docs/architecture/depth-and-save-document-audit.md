# Depth convention and save-document audit

The executable save schema is **25** at `c626cea10d1cb496df41cca2f9ab2f580ad47b28`.
Several documents retain dated introduction versions or proposed field sets. Those
are distinguished below from statements that incorrectly describe current main.
No save or version-26 text is changed by this review.

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

The direct sentence edit is **parked** because PR 1516 owns the architecture file.
The replacement above is ready to apply with that change. It does not need a
runtime or save change. `TidalExposureTests` already tests basic subtraction and
wet/dry boundaries; four additional parameterized cases exercise the Core service
seam at signed elevations and under common datum shifts, preserving those rules.
No Unity or EditMode execution was run locally; CI results are pending.

## Executable save authority

[`SaveMigration.CurrentVersion:16`](../../Assets/_Project/Code/Core/Save/SaveMigration.cs#L16)
is 25. [`SaveData`](../../Assets/_Project/Code/Core/Save/SaveData.cs) contains the
following root members, grouped only for readability:

- `SchemaVersion`, `WorldSeed`, `GameTimeSeconds`, `Money`, `DayIndex`, `OwnedBoats`, `ActiveHullId`.
- `OnboardingFlags`, `OwnedLicenses`, `RepairedBoats`, `OwnedGear`, `PlacedTraps`, `BaitStock`, `PotStock`.
- `PlacedLonglines`, `TubStock`, `BoatHoldCatch`, `BucketCatch`, `FreezerCatch`, `SupplyStock`, `HullInstruments`.
- `HullSounderPrefs`, `HullFuel`, `NavWaypoints`, `NavRoute`, `NavTrack`, `PlayerNotes`, `WornOutfitId`.
- `HullChartplotterPrefs`, `HullRadarPrefs`, `RestRegion`, `RestPosX`, `RestPosY`, `RestLevel`, `RestBedId`.
- `RestWokeAtSeconds`, `RestAboard`, `RestBoatHullId`, `RestBoatRegion`, `RestBoatPosX`, `RestBoatPosY`, `RestBoatRotation`.
- `PlayerRecipe`, `PlayerName`, `SpentClamHoles`, `PlayerTraits`, `PlayerNeeds`, `PlayerBody`, `Messes`.
- `NextMessSerial`, `PlayerCleanliness`, `KitchenStores`, `CaughtSpecies`, `NpcGiftDays`, `CookJobs`, `HullLoranWaypoints`.
- `StoredContainers`.

Rows added at v24 are defined in `Core/Save/SaveRowsV24.cs`; container rows at v25
are in `Core/Save/SaveRowsV25.cs`. Their introduction dates are not current-version
claims. There are no `MarketStates`, `EconomyLedgerRows`, `HullSailSet` or general
physical-goods location/hand records in this root. Current player-save state must
also be distinguished from authored Defs, rig sidecar schemas and UI preferences.

PR 1516 proposes v26 and four root members: `StorePurchases`, `SpecialOrders`,
`NextSpecialOrderNumber` and `ShoppingBags`. Its architecture/data-model additions
are outside this pinned main. The v26 passages in the limited-licences and
island-economy design documents are recorded below and left untouched. Their
proposed shapes are not evidence that those fields are already in main.

## Document inventory

The scan covered documentation under `docs/` (`.md`, `.txt`, `.rst`), matching save
versions/schema language, SaveData/SaveMigration and root/row member references.
The 92 candidates below include all discovered concrete version and field-list
claims, plus incidental hits explicitly labelled as such. Line references are
for the pinned base, not a moving branch. Existing historical/proposal text is
reported, not silently rewritten as a present-day implementation claim.

| Document and evidence lines | Does it match pinned main? |
|---|---|
| [adr/0008-save-schema-and-versioning.md](../adr/0008-save-schema-and-versioning.md#L1) (1, 4, 27, 30, 35) | Historical match: v1 introduction and its eight root fields remain valid; v1 is not the current version. |
| [adr/0009-tidal-exposure-and-region-display-name-seams.md](../adr/0009-tidal-exposure-and-region-display-name-seams.md#L89) (89) | No schema inventory: explicitly adds no saved tide or display-name state. |
| [adr/0014-painted-seabed-height-authoring.md](../adr/0014-painted-seabed-height-authoring.md#L227) (227) | No schema inventory: painted elevation changes add no saved fields. |
| [adr/0016-additive-2d-lights.md](../adr/0016-additive-2d-lights.md#L893) (893) | No schema inventory: a rendering identity is explicitly unsaved. |
| [adr/0018-shared-wave-field.md](../adr/0018-shared-wave-field.md#L192) (192, 615) | No schema inventory: wave/presentation state is explicitly recomputed or unsaved. |
| [adr/0019-hand-authored-scenes-and-refresh.md](../adr/0019-hand-authored-scenes-and-refresh.md#L245) (245) | No schema inventory: editor scene placement is outside the player save. |
| [adr/0020-world-placed-object-persistence.md](../adr/0020-world-placed-object-persistence.md#L1) (1, 5, 7, 19, 23, 24) | Historical match: v2 to v3 introduced PlacedTraps; the claim that trap runtime is absent describes that old slice. |
| [adr/0021-in-engine-js-rig-baking.md](../adr/0021-in-engine-js-rig-baking.md#L273) (273) | No schema inventory: bake output is an authored asset, not save state. |
| [adr/0025-ui-rig-runtime-rendering.md](../adr/0025-ui-rig-runtime-rendering.md#L126) (126) | No schema inventory: presentation rendering adds no saved fields. |
| [adr/0029-character-colour-runtime-structure-baked.md](../adr/0029-character-colour-runtime-structure-baked.md#L214) (214) | Historical snapshot: schema 14 was the CW-01 baseline; main is 25 and PlayerRecipe now exists. |
| [adr/0030-per-hull-instrument-ownership.md](../adr/0030-per-hull-instrument-ownership.md#L3) (3, 7, 12, 30, 43, 59) | Historical match: v8 HullInstruments/HullSounderPrefs and the v9 range addition remain present. |
| [adr/0036-interior-levels-as-layers.md](../adr/0036-interior-levels-as-layers.md#L125) (125, 128) | STALE: the claim of no saved position or region is contradicted by RestRegion, RestPosX, RestPosY and RestLevel. |
| [adr/0037-rest-anchor.md](../adr/0037-rest-anchor.md#L4) (4, 5, 19, 32, 81, 111) | Historical match: the four v13 rest fields remain; v20 later extended the sleep-place record. |
| [adr/0039-quiet-hud.md](../adr/0039-quiet-hud.md#L8) (8) | No field/version inventory: references the versioning ADR for future grants. |
| [adr/0050-helm-footprint-seam.md](../adr/0050-helm-footprint-seam.md#L69) (69) | Match of absence: helm-footprint geometry is not a SaveData field. |
| [adr/0051-building-a-person-without-js.md](../adr/0051-building-a-person-without-js.md#L10) (10, 208, 262) | Historical match: v14 to v15 added PlayerRecipe; WornOutfitId remains. |
| [adr/0052-event-handler-isolation.md](../adr/0052-event-handler-isolation.md#L38) (38) | No schema inventory: handler isolation changes no saved fields. |
| [adr/0055-goods-are-carried.md](../adr/0055-goods-are-carried.md#L7) (7) | Proposal: physical goods persistence is a future contract, not an installed save-field list. |
| [adr/0060-rigged-dwelling-interiors.md](../adr/0060-rigged-dwelling-interiors.md#L232) (232) | No schema inventory: dwelling geometry adds no saved fields. |
| [adr/README.md](../adr/README.md#L17) (17) | Historical index: v1 names the original ADR, not current main. |
| [adr/drafts/campaign-coop-proposal.md](../adr/drafts/campaign-coop-proposal.md#L5) (5) | Proposal: multiplayer/session records have no installed schema here. |
| [architecture/data-model.md](../architecture/data-model.md#L27) (27, 88, 93, 105, 112, 114) | MIXED: historical v2 through v25 additions match their fields; the generic PlayerState/FleetState/EconomyState/BusinessState/WorldState rows describe unshipped shapes. Some v24 data-only labels lag their runtime bindings. File owned by PR 1516. |
| [architecture/module-coupling/README.md](../architecture/module-coupling/README.md#L34) (34) | No schema inventory: a future snapshot-boundary review is requested. |
| [architecture/module-coupling/fishing-to-economy.md](../architecture/module-coupling/fishing-to-economy.md#L58) (58) | No schema inventory: identifies responsibility for future persistence changes. |
| [architecture/region-scene-churn-audit.md](../architecture/region-scene-churn-audit.md#L315) (315) | No schema inventory: reload observations do not define new saved fields. |
| [architecture/routine-clock-reload-audit.md](../architecture/routine-clock-reload-audit.md#L50) (50) | No schema inventory: reload-risk audit explicitly does not prescribe new fields. |
| [architecture/tech-architecture.md](../architecture/tech-architecture.md#L78) (78, 357, 426, 429, 493, 495) | MIXED: v25 StoredContainers and historical v1 fields match; baseline-v14 prose is old. Section 6 overstates persisted market, business, staff, skills and property state. Depth sign also inverted. File owned by PR 1516. |
| [art/recreation-boats-intake.md](../art/recreation-boats-intake.md#L815) (815) | Match: OwnedBoats and ActiveHullId exist; no new schema is claimed. |
| [art/rigs/charters/hand-cart.md](../art/rigs/charters/hand-cart.md#L34) (34) | Proposal: cart identity, support, load and grip persistence are not installed save fields. |
| [art/rigs/modern2500-kit/README.md](../art/rigs/modern2500-kit/README.md#L15) (15) | Not a player-save version: schema v2 belongs to the vehicle sidecar format. |
| [art/rigs/modern350-kit/README.md](../art/rigs/modern350-kit/README.md#L15) (15) | Not a player-save version: schema v2 belongs to the vehicle sidecar format. |
| [authoring-scenes.md](../authoring-scenes.md#L349) (349) | Not a player-save claim: saved project assets are discussed. |
| [design/autopilot-slice-2.md](../design/autopilot-slice-2.md#L39) (39, 308, 356, 369, 370) | Match: HullInstruments and v24 HullLoranWaypoints exist with the listed waypoint members; route/hold state is not thereby saved. |
| [design/campaign-coop-plan.md](../design/campaign-coop-plan.md#L71) (71, 215) | Historical snapshot, not current: v17 baseline; the single-player money, hull, recipe, notes and rest fields still exist in v25. |
| [design/diegetic-devices.md](../design/diegetic-devices.md#L28) (28, 69, 125, 126, 198, 438) | MIXED: v8 instruments and nav/flags fields match. No task state is too broad: PlayerNotes persists IsTask and Done, though no authored TaskDef progression is installed. |
| [design/diegetic-instruments-and-consoles.md](../design/diegetic-instruments-and-consoles.md#L108) (108, 292) | Match: watch ownership uses OwnedGear. Proposed window-layout preferences are not SaveData fields; this is distinct from saved instrument preferences. |
| [design/diegetic-ui-and-inventory.md](../design/diegetic-ui-and-inventory.md#L10) (10, 39, 294, 379, 396, 509) | STALE blanket absence: placed traps/longlines and container contents now persist. General physical item/hand/support identity still needs a separate schema. |
| [design/economy-and-business.md](../design/economy-and-business.md#L417) (417, 486, 497, 508, 538) | Match: OwnedLicenses, OwnedGear, RepairedBoats and PotStock are retained; v2/v4 are introduction versions. |
| [design/economy-item-sellers.md](../design/economy-item-sellers.md#L223) (223) | Proposal: a future paid product may require ownership persistence; no installed field set is claimed. |
| [design/fish-and-content.md](../design/fish-and-content.md#L207) (207, 290) | Match: PlacedTraps exists; the no-new-fields statement concerns the described fishing slice. |
| [design/fish-plant-logic.md](../design/fish-plant-logic.md#L72) (72) | Proposal: future inventory/receipt persistence is not a shipped field declaration. |
| [design/fish-plant.md](../design/fish-plant.md#L171) (171) | Historical scope: v24 is named as separate work, not current main; no plant-specific save records exist. |
| [design/fishing-methods.md](../design/fishing-methods.md#L155) (155, 156) | Match: PlacedTraps, BaitStock and PotStock are present. |
| [design/fuel-and-refuelling.md](../design/fuel-and-refuelling.md#L465) (465, 488, 497, 1036, 1038) | Historical match: HullFuel was introduced in v14 and remains; 13 to 14 is not the next migration. |
| [design/goods-handling/00-today.md](../design/goods-handling/00-today.md#L38) (38, 53) | Match: aggregate catches retain SpeciesId, Quantity and freshness fields; individual catch identity/rolled dimensions are not saved there. |
| [design/goods-handling/03-data-model-proposal.md](../design/goods-handling/03-data-model-proposal.md#L3) (3, 36) | Proposal: item/container/location records are future fields; the three legacy catch lists exist. No version is reserved. |
| [design/goods-handling/06-phasing.md](../design/goods-handling/06-phasing.md#L9) (9, 10) | Proposal/phasing: physical goods migration is planned work, not an installed schema. |
| [design/goods-persistence-proposal.md](../design/goods-persistence-proposal.md#L3) (3, 7, 11) | Historical snapshot plus proposal: CurrentVersion 17 is not current; aggregate catch facts remain, proposed physical-goods records are absent. |
| [design/gym-workouts.md](../design/gym-workouts.md#L108) (108, 118) | MIXED: main already has v24 is stale as a current-version statement. Saved PlayerBody supports fitness; workout credit/session state remains unsaved. |
| [design/helm-audit.md](../design/helm-audit.md#L507) (507) | No save-field inventory: UI layout preferences are distinguished from schema changes. |
| [design/helm-instruments-logic.md](../design/helm-instruments-logic.md#L12) (12) | Match: per-hull ownership uses HullInstruments; no second ownership/save list is needed for the described contract. |
| [design/licence-bait-migration-decisions.md](../design/licence-bait-migration-decisions.md#L41) (41) | No schema inventory: migration decision constraints do not install another format. |
| [design/limited-licences-and-crewing.md](../design/limited-licences-and-crewing.md#L313) (313) | Match at pinned main: v25 is current. Beyond-v26 licence/crew records are proposed, not installed; leave all v26 discussion untouched. |
| [design/living-fish-capture-phases.md](../design/living-fish-capture-phases.md#L45) (45) | Proposal: capture persistence requires a later migration, not current fields. |
| [design/living-fish/02-landed-fish.md](../design/living-fish/02-landed-fish.md#L3) (3) | Proposal: fish location/vitality design explicitly is not a save-schema change. |
| [design/living-fish/08-occupant-transactions.md](../design/living-fish/08-occupant-transactions.md#L13) (13) | Historical snapshot: v17 is not current; HeldCatchDto and PlacedTrapDto field descriptions still match. |
| [design/living-fish/09-owner-questions.md](../design/living-fish/09-owner-questions.md#L16) (16) | No schema inventory: a decision page expressly grants no save-format change. |
| [design/loran-navigation.md](../design/loran-navigation.md#L29) (29, 92) | Match: v24 HullLoranWaypoints and its seven listed members are present. |
| [design/m1-progression-pacing.md](../design/m1-progression-pacing.md#L100) (100) | Historical match: SupplyStock was introduced at v7 and remains. |
| [design/market-supply-persistence.md](../design/market-supply-persistence.md#L3) (3, 14, 29) | Match of current absence plus proposal: SupplyStock is player consumables; MarketStates and the proposed market rows are not installed. |
| [design/multiplayer-scoping.md](../design/multiplayer-scoping.md#L213) (213, 326, 468, 479, 503, 523) | Historical snapshot, not current: repeated v10/current-version and v10 migration starting points must not be reused for v25. |
| [design/mussel-lease-and-longline.md](../design/mussel-lease-and-longline.md#L98) (98, 150, 238, 269, 353) | PROPOSAL / MIXED: OwnedLicenses and baited fishing-longline placement/timing are existing reusable machinery (SaveData.cs:905-920; Fishing/LonglineCatch.cs:24-77). The mussel design explicitly remains unbuilt (line 10): lease ownership, mussel placement, seed choice and crop resume (lines 115-151) are proposed, not installed persistence. Fishing hooks, bait and tubs do not store a mussel crop. |
| [design/npc-trawler-fleet-plan.md](../design/npc-trawler-fleet-plan.md#L121) (121) | Proposal: fleet/truck persistence is a future requirement, not a declared main field set. |
| [design/npcs-and-routines.md](../design/npcs-and-routines.md#L258) (258, 890) | Match: v24 NpcGiftDays has NpcId, DayIndex and Count and is used by gifting. |
| [design/progression-and-housing.md](../design/progression-and-housing.md#L415) (415) | Design intent: versioned resume is required, but no concrete current field set or version is specified. |
| [design/region-seam-audit.md](../design/region-seam-audit.md#L146) (146) | Match: legacy region ids are migrated; this is not a new version claim. |
| [design/sail-controls.md](../design/sail-controls.md#L90) (90, 507, 708, 714) | MIXED: HullFuel since v14 is correct. Proposed HullSailSet is absent; a future 14 to 15 bump is stale because 15 is already used. |
| [design/seabed-bottom-type.md](../design/seabed-bottom-type.md#L88) (88) | No schema inventory: authored seabed type is not new saved state. |
| [design/shop-catalog-and-dialogue-choices.md](../design/shop-catalog-and-dialogue-choices.md#L392) (392) | No schema inventory: a later ownership change requires its own persistence decision. |
| [design/shop-stock-rules.md](../design/shop-stock-rules.md#L62) (62, 73, 75) | Match of absence plus proposal: shelf records are session-only at this base; future stock persistence is not in SaveData. |
| [design/simulation-determinism-audit.md](../design/simulation-determinism-audit.md#L5) (5, 44) | No schema inventory: determinism audit changes no saved fields. |
| [design/st-peters-key-scenes/wave-1b-placement.md](../design/st-peters-key-scenes/wave-1b-placement.md#L9) (9) | No schema inventory: scene placement and perch runtime state add no saved fields. |
| [design/st-peters-living-economy.md](../design/st-peters-living-economy.md#L235) (235) | Proposal: next free version 26 is consistent with pinned v25, but EconomyLedgerRows is absent and differs from PR 1516. Leave v26 lines untouched. |
| [design/time-tides-weather.md](../design/time-tides-weather.md#L337) (337) | Match: SpentClamHoles stores hole id and spent game time; tide-cycle state is recomputed. |
| [design/trawl-fishing-logic.md](../design/trawl-fishing-logic.md#L168) (168) | Proposal: trawl logic snapshot is not an installed save schema. |
| [design/watch-types.md](../design/watch-types.md#L28) (28) | Match of absence: watches use existing gear ownership; no separate wearing field is installed by this slice. |
| [design/yacht-race-integration-plan.md](../design/yacht-race-integration-plan.md#L6) (6) | Proposal: race integration explicitly does not authorize saved state. |
| [design/yacht-racing.md](../design/yacht-racing.md#L74) (74) | Match of absence: race and retirement session state is not in SaveData. |
| [docs-drift-audit.md](../docs-drift-audit.md#L3) (3, 21, 22, 23, 24, 39) | Match: the prior audit correctly identifies executable v25 and stale schema summaries, at its separately stated baseline. |
| [economy/nmc-wholesalers-stock.md](../economy/nmc-wholesalers-stock.md#L222) (222, 223, 243, 245) | Match: HullInstruments, OwnedGear and ActiveHullId are existing fields; future product decisions do not add persistence. |
| [fixture-wall-reach-proposal.md](../fixture-wall-reach-proposal.md#L75) (75) | Proposal: transient interaction snapshots need no saved fields. |
| [marketing/first-trailer-assessment.md](../marketing/first-trailer-assessment.md#L210) (210) | Not a player-save claim: a capture manifest is an authored asset. |
| [roadmap.md](../roadmap.md#L156) (156, 225, 453) | Historical/design outline: v1 is a milestone and seed/time/playerState is shorthand, not the current concrete v25 field list. |
| [tooling/per-frame-cost-inventory.md](../tooling/per-frame-cost-inventory.md#L65) (65, 110, 174, 226) | Match: navigation and HullFuel references point to existing fields; HullFuelDto is a value type. |
| [tooling/settled-plate-variance-findings.md](../tooling/settled-plate-variance-findings.md#L137) (137) | No schema inventory: test measurements add no save fields. |
| [tooling/water-test-methods.md](../tooling/water-test-methods.md#L3414) (3414) | Match of absence: tide/wind/weather are excluded from the save; references test coverage rather than listing a version. |
| [verification/character-wardrobe-baseline-2026-09-13.md](../verification/character-wardrobe-baseline-2026-09-13.md#L15) (15) | Historical snapshot only: v14 and absence of PlayerRecipe described the named old baseline; main is 25 and includes PlayerRecipe. |
| [verification/fishing-behaviour-coverage.md](../verification/fishing-behaviour-coverage.md#L1) (context) | Not a schema inventory: WorldSeed and DayIndex are fixture inputs; no version claim. |
| [verification/managed-allocation-guards.md](../verification/managed-allocation-guards.md#L52) (52) | No schema inventory: allocation guards add no saved fields. |
| [world/camper-home-plan.md](../world/camper-home-plan.md#L41) (41, 125) | STALE planned step: v25 rack/container persistence is now installed as StoredContainers, with richer rows than the proposed pair; v24 cooking jobs also persist. |
| [world/kitchens-bathrooms-audit.md](../world/kitchens-bathrooms-audit.md#L481) (481) | No schema inventory: the room audit adds no saved fields. |
| [world/washrooms-plan.md](../world/washrooms-plan.md#L48) (48) | Match: remaining soap washes use SupplyStock, not a new field. |

Only this audit and the existing depth test suite change. The list is a static
comparison of field declarations and documentary claims, not a save round-trip,
player experience, or assertion that every listed subsystem captures/restores all
its runtime state. No save file was read or written.
