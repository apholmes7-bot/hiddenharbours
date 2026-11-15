# Data Model

> The catalog of data-driven content types (ScriptableObjects) and the runtime save model.
> Full field-level designs live in the matching `design/` docs; this is the architectural map
> that keeps them consistent and parallel-authorable. See `adr/0003-data-driven-content.md`.

## 1. Authoring rule (read first)

**One entity = one ScriptableObject asset = one file.** New content is a new file, never an edit
to a shared list. This is what lets many agents add fish, boats, NPCs, and recipes at the same
time without merge conflicts. Every Def has a stable **string `id`** (e.g., `fish.atlantic_cod`,
`boat.cape_islander`) that is the canonical reference used in save data and cross-references —
**never reference content by Unity object name or file path.**

## 2. Definition types (the content catalog)

| Def (ScriptableObject) | Folder | Owns | Detailed design |
|------------------------|--------|------|-----------------|
| `FishSpeciesDef` | `Data/Fish/` | id, category, rarity, regions, depth band, tide/time/season/weather windows, gear/bait, size range, base value, supply elasticity, art ref, flavor, behavior flags | `design/fish-and-content.md` |
| `BoatHullDef` / `EngineDef` / `GearDef` / `InstrumentDef` | `Data/Boats/` | the composable parts of the 8-tier ladder: length, draught, hold (HU), crew slots, seaworthiness, handling, thrust, fuel (FU), cost, unlock | `design/boats-and-navigation.md` |
| `RegionDef` | `Data/Regions/` | id, display name, unlock gate, scene ref, tide profile, depth/seabed map ref, spawn tables, hazards, NPCs present, mood/palette grade | `design/world-and-regions.md` |
| `NpcDef` | `Data/NPCs/` | id, name, role, personality, art/paperdoll, home region, relationship config, quest hooks | `design/npcs-and-routines.md` |
| `ScheduleDef` | `Data/NPCs/Schedules/` | conditional 24h schedule blocks (time + tide/weather/season/weekday/story conditions) | `design/npcs-and-routines.md` |
| `CommodityDef` | `Data/Commodities/` | id, category, base price, demand curve, elasticity, perishability/spoilage | `design/economy-and-business.md` |
| `RecipeDef` | `Data/Resources/Recipes/` | **Shipped (cooking slice, 2026-11-14):** one recipe (`recipe.snake_case`): `Station` (open string id: `station.prep`, `station.cooktop`; later `station.salt_house` etc.), one input (`InputKind`: a `fish.*` species, a `FishCategory`, or an earlier recipe's output id), `OutputId` (`food.snake_case`), `WorkSeconds`, `HungerRelief` (0..1 of a meal, cooktop outputs only). Two-step cooking (owner ruling): prep takes raw catch, the cooktop takes a prep output. Looked up through the pure `RecipeBook` (species beats category, ties by lowest id). **Planned:** several inputs with quantities and a value multiplier for the facility recipes (salt cod, smoked, canned, oil, meal, packs), added append-only | `design/economy-and-business.md` §9.1 |
| `FoodDef` | `Data/Resources/Food/` | **Shipped (first food slice, 2026-11-15):** one edible thing (`food.snake_case`) describing an item by `ItemId` (`supply.*` shop food in the supply locker, `produce.*`, `fish.*`, or a `food.*` recipe output): `EdibleRaw`, `HungerRelief` (0..1 of a meal, eating one unit as it is; 0 when not edible raw), `PreppedId`/`CookedId` (recipe output ids; required cooked form when not edible raw). Produce foods must agree with their `ProduceDef` raw figures. Eaten through Core `FoodEater` (`GameServices.Food`), which publishes `AteFood`/`EatRefused` (reason code, no text) | — |
| `FacilityDef` | `Data/Commodities/Facilities/` | processing/storage buildings: throughput, capacity, staff slots, cost | `design/economy-and-business.md` |
| `StaffRoleDef` (Core) | `Data/Resources/Staff/` | **Shipped (crew slice R2, 2026-11-15):** one role aboard a boat (`role.snake_case`): `DisplayName`, `Summary`, `TaskIds` (`task.snake_case`: what the holder does; `task.helm` is the one the voyage calculator reads) and `WorkIds` (`work.snake_case`: the fisheries or cargo it is the specialist for). Twelve roles: skipper, mate, engineer, deckhand, cook, sternman, winchman, gallows hand, hatch hand, shucker, striker, lookout. Loaded through the pure `StaffRoleBook` (`FromResources`). Players and hired hands hold the same roles. **Planned (M3-05/06):** wage band, skills, AI routine template, and the shore roles (processor, hauler, seller, manager), appended | `design/crew-roles-and-trip-limits.md`; `design/economy-and-business.md` §5.1 |
| `BoatVoyageDef` (Core) | `Data/Boats/Voyages/` | **Shipped (crew slice R2):** one per hull (`voyage.<hull>`), naming its hull by `HullId`: `LongestTrip` (`TripKind` Day/Overnight/Week/Month, before bunks), `MinCrewDay/Overnight/Week/Month`, and `Work[]` (`VoyageWorkRequirement`: `WorkId`, `MinCrew`, `RequiredRoleIds`), plus two notes for the owner (`RealWorldCrew`, `LimitedBy`). It authors **no maximum**: the day limit is `BoatHullDef.CrewSlots` and the overnight limit is the hull interior's `sleep` anchors read through `BunkCount.Of`, so a new berth (as PR 1228's second berths did) moves every limit without editing a record. Joined to its hull by the Boats `BoatVoyageCatalog` into a plain `VoyageSpec` that the pure `VoyageLimits.Check` and the in-memory `ICrewRoster` (`GameServices.Crew`, not saved yet) read. Policy knobs (hand bunk-sharing, shipped ON by owner ruling 2026-11-15; nights per trip kind; month needs a hired skipper, shipped OFF by the same ruling) are the `GameConfig.Crew` block | `design/crew-roles-and-trip-limits.md` |
| `BuyerDef` / `ContractDef` | `Data/Commodities/Buyers/` | market buyers, standing contracts, reputation requirements | `design/economy-and-business.md` |
| `LicenseDef` | `Data/Licenses/` | id, display name, fee (₲), permitted species/gear (St Peters opening — the minimal money-only licence; eligibility tower is later) | `design/progression-and-housing.md` §2.2 |
| `ShipwrightOffer` | `Data/Shipwright/` | a boat offered for sale: boat id, price, `StartsDamaged` + `RepairCost` (the damaged-dory buy+repair) | `design/economy-and-business.md` |
| `GearOffer` | `Data/Gear/` | the *economic* side of a purchasable gear item: id, display name, price (the rod/shovel/bucket). The gear *capability* (Gear flag, hold capacity) is gameplay-systems' | `design/economy-and-business.md` |
| `BaitDef` / `GearDef` | `Data/Gear/`, `Data/Bait/` | gear/bait that gate catch resolution | `design/fish-and-content.md` |
| `PropertyDef` | `Data/Regions/Property/` | houses & commercial lots: purchase, upgrade tiers, furnishing slots, comfort | `design/progression-and-housing.md` |
| `KeySceneDef` | `Data/KeyScenes/` | one key scene's placements, per variant (`today` first): piece id, kit and piece, at, z and what it stands on (ground, deck), facing, sort line, the words ids its boards carry. Placements belong to the scene, not the piece's Def, so one piece type can stand in many places; `StPetersLayerRefresh` writes them into the scene | the art desk's key-scene return (`scene.json`) |
| `WordsTableDef` | `Data/Words/` | the words a sign, name board or notice shows, by string id (`words.snake_case`); boards bake blank, so renaming a place changes a string, never a pixel | — |
| `RegionTerrainPlanDef` | `Data/Terrain/StPetersPlan/` | one region's terrain plan: seed, reference line, section feathers and warp, tide frame, bed rules, the lists of its coast, pond, stream, creek, pan, path and biome Defs, and the live walls' chunk cut (`WallToeSpanMetres`: St Peters 1 m; 2 m, the builder's own, elsewhere) | `design/st-peters-terrain-pass-9.md` §8.1 |
| `CliffWallDef` | `Data/Terrain/StPetersWalls/` | one cliff wall (`wall.stp_NNN`; append-only: a cut piece records `SplitFrom`, a retired or held id keeps its file): status, rock, class, aspect, batter, the wall it follows; per station its brow, toe, drop and toe height, its k on the wall it came from (`Stations`) and where its lines came from (`Kept`, `Measured`, `Interpolated`); optionally `TextureBrow`, the line its texture runs along where a top moved out | the class's own summary |
| `TraitDef` | `Data/Resources/Traits/` | one character trait (`trait.snake_case`): display name, one-line description, and a list of `TraitModifier`s, each a `TraitStat` (append-only enum: `WalkSpeed` 0, `SellPrice` 1, `BiteWait` 2), a kind (`Multiplier`: the value is a factor; `Additive`: the value is added to the factor) and the value. Additives on one stat sum, then multipliers apply; no drawbacks (the content validation test refuses one). Read only through `ITraitService` (owner-directed 2026-11-14) | `architecture/tech-architecture.md` §3, "Character traits" |
| `CropDef` | `Data/Resources/Farming/Crops/` | one crop (`crop.snake_case`): sowing and harvest windows as season + day (never wrapping the year), days to mature, till lead, tend interval, harvest spread, stage thresholds, per-cell visual wobble, yield per cell, the `produce.*` item it yields. A plot's stage is a pure function of (plot, crop, world seed, day) through `FarmMath`; nothing is saved | `design/npcs-and-routines.md` |
| `FarmPlotDef` | `Data/Resources/Farming/Plots/` | one farm plot (`plot.snake_case`; the id also seeds its sowing days): region, area (empty + `AreaIsPlaceholder` until placed), cells, crop rotation by year (empty entry = rest year), tender NPC ids (`TendersArePlaceholder` while uncast), player access (`TendersOnly` 0, `OpenToHelp` 1, `SideJobByApproval` 2, `ForPurchase` 3; append-only) with approver and price | `design/npcs-and-routines.md` |
| `ProduceDef` | `Data/Resources/Farming/Produce/` | one farm produce item (`produce.snake_case`): display name, base value (₲), eaten raw?, raw hunger relief and the prepped relief it must stay under (shares of one meal, the recipe scale). Storage, eating and recipes join later | — |
| `IslandGoodDef` | `Data/Resources/IslandSupply/Goods/` | one good St Peters uses and does not make (`islandgood.snake_case`): source (`MainlandOverBar` 0, `BySea` 1, `Island` 2; append-only), unit label, daily island use in that unit, daily carried mass (kg) and volume (L) packaging included, player allowance as a share of island use, winter factor, real container, basis. Summed by the Core `SupplyPlanMath`; nothing consumes or stocks it yet | `design/island-supply.md` |
| `CarrierDef` | `Data/Resources/IslandSupply/Carriers/` | one carrier (`carrier.snake_case`): kind (`Carried` 0, `Pulled` 1, `VehicleRack` 2, `Trailer` 3, `VehicleBed` 4, `LoaderAttachment` 5, `Container` 6, `Pallet` 7; append-only), practical load kg, maker's rating kg (≥ practical), volume L, tare kg, basis. A load fits only when both mass and volume do | `design/island-supply.md` |
| `SupplyRoleDef` | `Data/Resources/IslandSupply/Roles/` | one supply run (`supplyrole.snake_case`): crew NpcDef ids (+ placeholder flag), people crossing, carrier ids (repeat for two), trip mode (`SameWindowRoundTrip` 0, `StayOver` 1, `MainlandOnly` 2; append-only), cadence days (0 = as needed), market day?, sand and road speed (m per real second), gut wade limit (m), town loading time (game h) | `design/island-supply.md` |
| `BulkMaterialDef` | `Data/Resources/IslandSupply/Bulk/` | one loose material for a bucket or tub (`bulk.snake_case`): bulk density kg/L. A fill is min(volume × density, mass limit) | `design/island-supply.md` |
| `IslandSupplyConfig` | `Data/Resources/IslandSupply/` | the one St Peters supply route config (no id): the island region whose tide gates the bar, bar length, crest and gut-bed elevations (pinned equal to `StPetersBuilder`), gut position and width, island path and mainland road legs, the run day's start and end hours, the store buffer days, the search step. The windows come from the region's tide through `TideModel`, never a copy | `design/island-supply.md` |
| `RelationshipTieDef` | `Data/Resources/Relationships/Ties/` | one starting tie between two villagers (`tie.<a>__<b>`, the `npc.` prefixes dropped, `NpcA` sorting before `NpcB` so a pair is authored once): the two `NpcDef` ids, a hidden `StartingStrength` and a writer's note. Villagers with no tie start strangers to all. Read only through `IRelationshipService`; never shown to the player as a number, bar or hearts (owner ruling 2026-11-14) | `architecture/tech-architecture.md` §3, "NPC relationships" |
| `RelationshipConfig` | `Data/Resources/Relationships/` | the one relationship config: strength bounds and stage edges (`Rival`, `Strained`, `Stranger`, `Acquaintance`, `Friend`, `Close`; no romance stage) and the rule table, one `RelationshipRule` per `RelationshipEventKind` (points per unit, a per-pair daily cap, whether it may make rivals) | `architecture/tech-architecture.md` §3, "NPC relationships" |
| `GameConfig` | `Data/Config/` | global tunables: day length, tide constants, season length, economy constants, stamina rates | several |

> `GameConfig` centralises balance numbers so the owner / `economy-sim` can tune feel without
> touching code. Treat magic numbers as a smell — promote them here.

> **Wall format change (terrain PR 5w, St Peters' 1 m cut; made under the `lead-architect` hat).**
> `CliffWallDef.Stations` went from `int[]` to `float[]`: a k that is not whole is an *interpolated*
> station, set between the whole stations either side of it, and its `BrowFrom`/`ToeFrom` read
> `CliffStationSource.Interpolated` (new, `= 2`). It adds no face of its own: `CliffWallSurface` draws
> it as a slice of the face its whole neighbours draw, so a re-cut moves no rock. `CliffWallDef.TextureBrow`
> (optional) and `RegionTerrainPlanDef.WallToeSpanMetres` are new. No asset needed a migration: Unity
> reads an old asset's whole ks into the float field as they were, an absent `TextureBrow` reads empty
> (the texture runs along the brow), and an absent `WallToeSpanMetres` reads 2 m, the cut every wall had.
> Nine Mile Creek's walls and plan are untouched.

## 3. Cross-references use ids, and resolve through `ContentDatabase`

A `RegionDef` lists the fish in its spawn tables by **id**, not by direct object reference where
it would create asset-coupling that complicates parallel authoring. `ContentDatabase` loads all
Defs at boot and resolves ids → objects. This keeps any one asset editable without dirtying others.
(Direct `ScriptableObject` references are fine for tightly-coupled, same-owner data; use ids across
module/owner boundaries.)

## 4. Runtime / save state (what's mutable vs authored)

Defs are **immutable authored data** (never written to at runtime). Mutable runtime state lives in
plain serializable DTOs owned by `SaveService` (`architecture/tech-architecture.md` §6):

| Saved state | Holds | Keyed by |
|-------------|-------|----------|
| `PlayerState` | position, region, money (₲), stamina, skills, licenses, hold/inventory | — |
| `FleetState` | owned boats: hullId, fitted engine/gear/instrument ids, upgrades, damage, hold contents | boat instance id |

> **Save schema v2 (St Peters opening)** — `SaveData` gained three append-only lists at v2:
> `OwnedLicenses[]` (the licence wallet, backing `ILicenseService`), `RepairedBoats[]` (which owned
> hulls have been repaired → usable; a boat bought damaged is owned but unusable until its id lands
> here), and `OwnedGear[]` (purchased gear ids: rod/shovel/bucket). The `v1→v2` migration is additive
> (empty new lists) and marks every already-owned boat repaired so a pre-v2 save's boat stays usable.
> Cross-lane (`SaveData`/`SaveMigration` are Core/lead-architect) — flagged for review.
| `EconomyState` | per-commodity supply level & price history, active contracts, business ledger | commodityId |
| `BusinessState` | owned facilities, staff roster (roleId, skill, morale, wage, assignment), production queues, logistics routes | facility/staff instance id |
| `WorldState` | region reveal/fog, quest & story flags, NPC relationship scores | regionId / npcId / flagId |
| `WorldClock` | `worldSeed`, `gameTime`, season/year | — |

> **Crew roster (crew slice R2, 2026-11-15): NOT saved yet.** `ICrewRoster` (`GameServices.Crew`) holds
> who is aboard which boat (a caller-chosen boat key, the hull id) and in which role (`CrewMember`:
> member id, `Player` or `HiredHand`, role id), in memory only. A later save bump owns persisting it;
> when it does, the record is the boat key, the hull id and the member list, and nothing the calculator
> derives (day max, overnight max, bunks) is stored: those are recomputed from the Defs.

> **Save schema v8 (helm instruments, ADR 0030)** — `SaveData` gained two append-only lists at v8:
> `HullInstruments[]` (flat `(hullId, instrumentId)` rows — the first PER-HULL ownership record; an
> instrument is bolted into one boat's dash, not carried) and `HullSounderPrefs[]` (per-hull sounder
> preferences: set-point, armed, units, night). Both are **sparse**: a hull with no rows carries its
> `HelmConsoleDef`'s authored default fit, so the `v7→v8` migration is a pure addition and every
> existing boat comes out exactly as authored. Read/written only through `InstrumentLocker` (Core).
> The **resolved fit** (`BoatEquipment.EffectiveFit`) and the **depth reading** are recomputed, never
> stored. Cross-lane (`SaveData`/`SaveMigration` are Core/lead-architect) — ADR 0030.

> **Save schema v9 (the fish finder's range, ADR 0025 S3 / ADR 0030 amendment)** — `SounderPrefsDto`
> gained ONE field at v9: `RangeMetres`, the finder's vertical sonar scale in metres. No new list and no
> new shape — fitting a fish finder is still one more `(hullId, instrumentId)` row in the v8 shape. The
> `v8→v9` step **heals** rather than merely defaults: a v8 row read under the v9 struct arrives with
> `RangeMetres = 0`, and zero is a divide-by-zero (every mark and the bottom contour are drawn at
> `depth / range`), so the step repairs any non-positive range to
> `FishFinderSettings.Default.DefaultRangeMetres`. The finder keeps the depth sounder's shallow alarm
> unchanged, which is why the bump is exactly one field.

> **Save schema v13 (the rest anchor, [ADR 0037](../adr/0037-rest-anchor.md))** — `SaveData` gained
> FOUR append-only scalars at v13, which only mean anything together: `RestRegion`, `RestLevel`,
> `RestPosX`, `RestPosY` — where the player last went to sleep, so a load wakes them there instead of
> at the authored spawn. Read and written only through `RestLocker`, as one `RestAnchor`.
> **Region-stamped for the reason every position in this file is** (ADR 0004 — a world position only
> means anything inside its own region's frame), and **storey-stamped** because ADR 0036 made a second
> floor a *layer* over the same footprint: the one authored player bed is upstairs at Ginny's, directly
> above her front room, so a position-only anchor would restore the right coordinates into the wrong
> room. `RestRegion == ""` is **"has never turned in"** — a new game, every save older than v13, and
> the fallback for an unreadable anchor — under which the player wakes at the spawn exactly as before.
> The `v12→v13` step **drops but does not repair**: an anchor whose position is not a finite number is
> cleared (there is no honest place to invent), while a region or storey the loader merely cannot vouch
> for is left alone and declined at the point of use.

> **Save schema v15 (the player's own build, [ADR 0051](../adr/0051-building-a-person-without-js.md))** —
> `SaveData` gained ONE field at v15: `PlayerRecipe`, a `CharacterRecipe` of rig keys (a build id, a fit
> id and the rig's twenty fields, as strings). It is written only through `IPlayerAppearance.Commit`; a
> preview never reaches it. **Keys, not colours**, for the `WornOutfitId` reason: a ramp the art
> director retunes follows into every save. EMPTY is the body as baked, which is a new game and every
> save older than v15, so the `v14→v15` step adds the empty recipe and nothing else. `WornOutfitId` (v12)
> stays where it is, because it recolours the sprite layer, which the mesh never drew. No recipe is
> healed against shipped content: the builder refuses a stale key out loud at the point of use (the
> v13 reasoning). What a recipe resolves to, a baked body and its ramps, is recomputed from the
> shipped tables and never stored.

> **Save schema v18 (character traits, owner-directed 2026-11-14)** — `SaveData` gained ONE field at
> v18: `PlayerTraits`, the ids of the `TraitDef`s the player picked (two, `GameConfig.TraitPicksPerCharacter`),
> in pick order. It is written only through `ITraitService.SetPlayerTraits`. **Ids, not numbers**: the
> bonus is read from the shipped Def every time, so the owner retuning a trait follows into every save.
> EMPTY is no traits, which is a new game and every save older than v18, so the `v17→v18` step adds the
> empty list and nothing else. An id no shipped trait claims is ignored by the maths, never healed away.
> In co-op each player will hold their own list; only the acting player's traits apply to an action.

> **Save schema v20 (the whole sleep place, owner ruling 2026-11-15: "Reloading restarts the day where
> you last slept. A sleep is a save.")** — `SaveData` gained EIGHT append-only scalars beside the v13
> anchor: `RestBedId` (the bed or bunk's stable fixture id), `RestWokeAtSeconds` (the game time the
> sleep ended), and, for a bunk aboard, `RestAboard`, `RestBoatHullId`, `RestBoatRegion`,
> `RestBoatPosX`, `RestBoatPosY`, `RestBoatRotation` (the boat root's pose as its transform held it).
> Read and written only through `RestLocker.Place` / `RestLocker.StampPlace`, as one `SleepPlace`
> (Core/Save/SleepPlace.cs); `RestLocker.Forget` clears all twelve. **Every position is in its named
> region's own scene frame**, so a shared world frame (a per-region offset) maps it with no new field.
> The `v19→v20` step reinterprets nothing: a v13..v19 bed anchor still wakes the player in that bed, a
> save with no anchor still wakes at the default spawn, and the new fields take honest empties (no bed
> id, woke-at 0 = unknown, not aboard). On load `RestWakeRestorer` applies a same-region place ashore;
> a place in ANOTHER region, or ABOARD a boat, is not applied in the wrong frame but handed to the
> `SleepPlaceWakeRequested` signal for the travel rig to honour, and the spawn stands until it does.

> **The chosen sleep (no schema step; owner ruling 2026-11-15: "Player chooses sleep duration. Time
> moves fast … cancel with a button. Maximum 8 hours")** — the save gains nothing: the wake instant is
> already `gameTime`, and `SleepPlace.WokeAtSeconds` records it. The flow is `SleepFlow` (Core/Save):
> lie down → choose → asleep (the clock is fast-forwarded with `IGameClock.FastForwardTo` at
> `GameConfig.SleepGameHoursPerRealSecond`) → wake on its own at the chosen instant or early on a
> press, and EVERY wake writes the save. Tunables: `GameConfig.SleepMinHours`, `SleepMaxHours` (held
> under `SleepPlan.RulingMaxHours` = 8), `SleepStepHours`, `SleepGameHoursPerRealSecond`;
> `NeedsSettings.SleepReliefPerGameHour` (tiredness falls this much per hour actually slept, from its
> bedtime level, stamped at the wake; hunger keeps its own stamp and rises on the clock). **Mid-sleep
> the save service refuses every write**, so a quit or an autosave during a sleep keeps the last
> completed sleep. `RestQuorum` (Core/Save) is the multiplayer seam: one of one today, one sleeper per
> bed id.

**Recomputed, never saved:** tide height, wind, weather, sea state, visibility (from
`worldSeed + gameTime`), dormant NPC positions, authored geometry, **a boat's effective helm fit and
its depth-sounder reading** (ADR 0030), **which end of a bed the pillow is on** (see below), and
**fish schools** — where the fish are is a function of `(worldSeed, gameTime)` + place + weather like
the tide is, so the finder's marks are never stored (ADR 0025 S3). This is the determinism dividend —
small, robust saves.

> **A bed's PILLOW SIDE is content, not save state — and so the rest anchor did NOT grow a field.**
> A bed declares which end its pillow is on (`PillowSide`, in the interior-prop def's furnishing row);
> the builder resolves it against the room's own facing and hands the bed a `PillowAnchor`, and the
> sleeping pose lays the fisher head-to-pillow from it. It is **declared, never inferred from pixels**
> — the same rule as a house's door anchor, and for the same reason: the bake reports a bed as a
> plain `width × depth` rectangle that is identical at both ends, so the head end only exists because
> the rig says so (`interiorPropRig.js`: *"pillow (head end, −Y front)"*). None of it enters
> `SaveData`: the whole declaration is re-derived from content every time a room is stood, and the v13
> anchor stays exactly what ADR 0037 made it — a region, a storey and the player's own feet, none of
> which moves because a pillow is at one end or the other. **A bed with no declaration fails content
> validation**, because the failure is otherwise silent: the pose falls back to one fixed heading,
> which is right in an unturned room and wrong at the other seven facings, and a sleeper lying the
> wrong way round reads as bad art rather than as a bug.

**Deliberately NOT in that list:** where the player chose to go to sleep (v13 above). Rule 5 keeps
tide/wind/weather out of the save because they are *derivable* from `(worldSeed, gameTime)`; a bed is
derivable from nothing. It is an authored decision by the player, the same family as `WornOutfitId`
(v12) and `PlacedTraps` (v3) — storing it is not a rule-5 violation, and recomputing it is not
possible.

## 5. ID & naming conventions

- Def id format: `type.snake_case_name` → `fish.atlantic_cod`, `boat.cape_islander`,
  `region.coddle_cove`, `recipe.salt_cod`, `npc.uncle_ned`.
- Asset file name: PascalCase of the entity (`AtlanticCod.asset`), with `id` set in the asset.
- ids are **append-only and stable** — once shipped in a save, never renamed (add a new one + a
  migration instead).

## 6. How agents add content safely in parallel

1. Create a new asset file in the right `Data/...` folder (one entity per file).
2. Set a unique `id` (check the existing ids; ids are append-only).
3. Fill fields per the relevant `design/` doc; reference other content by id.
4. Register the asset's exact full Def type, existing Unity GUID and stable id in a new
   `Assets/Tests/EditMode/Fixtures/DefIdentityAdditions/<guid>.json` file. A new id-bearing Def
   type also gets `<full-type-name>.type.json`, including a type with no assets yet; an existing
   type needs no second declaration. Follow the [addition-file shapes and metadata rules](../../Assets/Tests/EditMode/Fixtures/DefIdentityAdditions/README.md).
   New registrations use these independent files instead of appending to the shared baseline.
   Both identity contract readers validate the baseline plus additions as one inventory, rejecting
   duplicate GUIDs, `(Type, Id)` pairs and type declarations with their source files named.
5. If new art is needed, coordinate with `art-pipeline` (placeholder is fine in M0).
6. Run `DefIdentityLedgerTests`, `DefIdentityContractTests` and `NmcH7ContentValidationTests`,
   plus the EditMode content-validation test (`qa-test` provides one: checks unique ids, resolvable
   references, required fields). Green = safe to commit.

The existing baseline rows remain in place. Converting pending PRs and adding a rule that
enforces a baseline freeze are deferred to a separate integration step.

## 7. Open questions
- Spawn tables: embedded in `RegionDef` vs separate `SpawnTableDef` assets — separate scales
  better once content grows (`economy-sim` + `world-content` to align).
- Localization: keep all display strings in a string-table keyed by id from the start so text can
  be localized later without touching Defs.
