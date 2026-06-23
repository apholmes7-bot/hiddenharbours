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
| `RecipeDef` | `Data/Recipes/` | inputs → facility/process → time → outputs, value multiplier (salt cod, smoked, canned, oil, meal, packs) | `design/economy-and-business.md` |
| `FacilityDef` | `Data/Commodities/Facilities/` | processing/storage buildings: throughput, capacity, staff slots, cost | `design/economy-and-business.md` |
| `StaffRoleDef` | `Data/Staff/` | role (deckhand, skipper, processor, hauler, seller, manager), wage band, skills, AI routine template | `design/economy-and-business.md` |
| `BuyerDef` / `ContractDef` | `Data/Commodities/Buyers/` | market buyers, standing contracts, reputation requirements | `design/economy-and-business.md` |
| `LicenseDef` | `Data/Licenses/` | id, display name, fee (₲), permitted species/gear (St Peters opening — the minimal money-only licence; eligibility tower is later) | `design/progression-and-housing.md` §2.2 |
| `ShipwrightOffer` | `Data/Shipwright/` | a boat offered for sale: boat id, price, `StartsDamaged` + `RepairCost` (the damaged-dory buy+repair) | `design/economy-and-business.md` |
| `GearOffer` | `Data/Gear/` | the *economic* side of a purchasable gear item: id, display name, price (the rod/shovel/bucket). The gear *capability* (Gear flag, hold capacity) is gameplay-systems' | `design/economy-and-business.md` |
| `BaitDef` / `GearDef` | `Data/Gear/`, `Data/Bait/` | gear/bait that gate catch resolution | `design/fish-and-content.md` |
| `PropertyDef` | `Data/Regions/Property/` | houses & commercial lots: purchase, upgrade tiers, furnishing slots, comfort | `design/progression-and-housing.md` |
| `GameConfig` | `Data/Config/` | global tunables: day length, tide constants, season length, economy constants, stamina rates | several |

> `GameConfig` centralises balance numbers so the owner / `economy-sim` can tune feel without
> touching code. Treat magic numbers as a smell — promote them here.

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

**Recomputed, never saved:** tide height, wind, weather, sea state, visibility (from
`worldSeed + gameTime`), dormant NPC positions, authored geometry. This is the determinism
dividend — small, robust saves.

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
4. If new art is needed, coordinate with `art-pipeline` (placeholder is fine in M0).
5. Run the EditMode content-validation test (`qa-test` provides one: checks unique ids, resolvable
   references, required fields). Green = safe to commit.

## 7. Open questions
- Spawn tables: embedded in `RegionDef` vs separate `SpawnTableDef` assets — separate scales
  better once content grows (`economy-sim` + `world-content` to align).
- Localization: keep all display strings in a string-table keyed by id from the start so text can
  be localized later without touching Defs.
