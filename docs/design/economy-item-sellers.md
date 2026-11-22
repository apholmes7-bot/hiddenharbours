# Who sells every item (audit and guard)

> **Status: audit of `main` at 32dfdecdf3, 2026-11-16.** Owner ask 2026-11-15: all items can be bought
> somewhere, and some can be sold back. This file lists every item Def, who offers it, at what price, and
> whether it is on a shelf today. The guard is
> `Assets/Tests/EditMode/Economy/EveryItemHasASellerTests.cs`.
>
> **Updated 2026-11-17 (main 1631a4ec9f): the two Nine Mile Creek wholesalers.** Owner ruling 11-16 puts
> the Loran and every boat electronic at a new boat/gear wholesaler and fishing stock at a separate fishing
> gear wholesaler; the prices and routing are the ones approved from
> [`docs/economy/nmc-wholesalers-stock.md`](../economy/nmc-wholesalers-stock.md) (§2, §4.2, §4.3). The
> wholesalers' stock is pinned by `Assets/Tests/EditMode/Economy/WholesalerStockTests.cs`. Both counters
> and keepers stand in `NineMileCreek.unity`; the Loran, fish finder, chartplotter and rod rack are listed
> there, and the rest of their stock stays on file, unlisted (§5.1 says why).

## 1. What counts

**An item** is a Def the player can own: tools and stowage (by their gear id), tackle, traps, hulls,
vehicles, the watch, golf clubs, longline tubs, carriable fuel cans, and every catalog listing Def
(gear, bait, supplies, instruments, licences).

**A seller offer** is an offer asset that names the item and at least one real `SellerDef`
(`Data/Resources/Sellers`). There are two steps to an item being for sale:

1. **Offer on file**: the asset exists, has its price, and names its seller. This is data only. The
   guard checks this step.
2. **On the shelf**: the listing is ticked `Listed` and the seller's scene has a committed vendor
   component stocking it. `StoreWiringTests` guard 4 requires both together, so ticking `Listed`
   without the store patch breaks the build. Listing an item means one tick on the asset, a row in
   `StoreWiringPatch`, and the scene the patch writes. That needs the editor, so it is not done here.

Vehicles never go on a book shelf: `BuyCatalog` has no vehicle arm. A `VehicleOffer` names its dealer
in `SellerId`, and the dealer key scenes are not built yet.

**Not item Defs, so not guarded:** held-prop meshes (`HeldPropMeshDef`, the art for items defined
elsewhere), clothing (`CharacterGarmentDef`, picked in the creator), gym equipment (fixtures in the
gym), deck containers (part of the hull), food (`FoodDef` points at an item id; supply-backed food is
already guarded by `FoodContentValidationTests`, caught fish and grown produce come from play), and the
Loran receiver's tuning Def.

## 2. Sellers and the counters they have in a scene

| Seller | Keeper | Trade | Vendor arms committed in a scene |
|---|---|---|---|
| `seller.leblancs` | Marguerite LeBlanc | general store (St Peters) | bait, gear, instrument, licence, supply |
| `seller.nmc_chandlery` | Claudette Boudreau | chandlery (NMC) | gear |
| `seller.hectors_barrel` | Hector Bernard | chandlery (NMC) | shipwright |
| `seller.nmc_yard` | the yard | boatyard (NMC) | shipwright, pots |
| `seller.nmc_harbourmaster` | harbourmaster | licensing (NMC) | licence |
| `seller.nmc_wharf_market` | Wendell Arsenault | fish buyer | none (buys only) |
| `seller.nmc_boat_gear_wholesaler` | not authored | boat/gear wholesale (NMC, Water Street east block) | none yet (greybox shell #1292; no counter vendor, no keeper) |
| `seller.nmc_fishing_gear_wholesaler` | not authored | fishing gear wholesale (NMC, by the lumber yard) | none yet (greybox shell #1292; no counter vendor, no keeper) |
| `seller.nmc_vehicle_dealer` | not authored | vehicles | none (key scene not built) |
| `seller.nmc_industrial_yard` | not authored | vehicles | none (key scene not built) |

## 3. Pricing rules used

- **Counter gear, bait, instruments:** the price already authored on each asset. This change adds no
  new counter prices; it only names sellers.
- **Boat electronics at the boat/gear wholesaler (2026-11-17):** the approved prices from
  `nmc-wholesalers-stock.md` §4.2: Loran 900 (was the 380 placeholder), fish finder 650 (was 480),
  chartplotter 1,800 (was 650). The depth sounder stays 140. Fishing stock keeps its prices (§4.3).
- **Hulls at the yard:** the yard's one existing new-hull price is the punt, ₲1,800 for an engine power
  of 650. Each new hull is priced at the same rate: `price = EnginePower × 1800 / 650`, rounded to the
  nearest ₲100 (nearest ₲1,000 above ₲100,000). It lands each hull in the net-worth band of its
  stage in `economy-and-business.md` §8: skiffs ₲8–10k at the top of stage 1, the Cape Islander and
  lobster boats ₲17–37k inside stage 2 (₲8k–40k), the side dragger ₲216k inside stage 4
  (₲150k–600k). Open and hardtop builds have the same stats, so they have the same price. These are
  starting values for the owner to tune on the assets.
- **Otter 8x8 at the dealer:** the utility quad's rate, ₲1,200 for 300 kg = ₲4/kg, times the Otter's
  620 kg = ₲2,480, rounded to ₲2,500. That puts it above the quad and below the cheapest truck
  (Modern 350, ₲3,500).

## 4. The audit

Legend: **shelf** = listed and stocked by a committed vendor; **offer** = on file, unlisted, waiting
for the store patch or the dealer scene; **added** = added by this change; **exempt** = no offer, with
the reason recorded in the guard.

### Counter gear, bait, supplies, instruments, licences

| Item | Price ₲ | Seller | State |
|---|---|---|---|
| `gear.rod` | 60 | leblancs, nmc_chandlery | shelf (fishing gear wholesaler joins when its counter stands) |
| `gear.shovel` | 25 | leblancs, nmc_fishing_gear_wholesaler | offer (also starting gear) |
| `gear.bucket` (clam bucket) | 15 | leblancs, nmc_fishing_gear_wholesaler | offer (also starting gear) |
| `gear.rod_rack` | 25 | nmc_fishing_gear_wholesaler (moved from nmc_chandlery) | offer |
| `gear.shellfish_rake` | 30 | leblancs, nmc_fishing_gear_wholesaler | offer (held back on purpose until hand-gathering is built) |
| `gear.oyster_tongs` | 45 | leblancs, nmc_fishing_gear_wholesaler | offer (same) |
| `gear.mussel_sack` | 10 | leblancs, nmc_fishing_gear_wholesaler | offer (same) |
| `gear.shellfish_basket` | 20 | leblancs, nmc_fishing_gear_wholesaler | offer (same) |
| `bait.capelin` | 2 | leblancs | shelf (fishing gear wholesaler joins when its counter stands) |
| `bait.herring` | 3 | leblancs, nmc_fishing_gear_wholesaler (moved from nmc_chandlery) | offer |
| `bait.mackerel` | 4 | leblancs, nmc_fishing_gear_wholesaler (moved from nmc_chandlery) | offer |
| `bait.squid_strip` | 6 | leblancs, nmc_fishing_gear_wholesaler (moved from nmc_chandlery) | offer |
| `bait.sea_worm` | 4 | leblancs, nmc_fishing_gear_wholesaler | offer |
| `bait.shucked_clam` | 3 | leblancs, nmc_fishing_gear_wholesaler | offer |
| `bait.fish_scrap` | 1 | leblancs, nmc_fishing_gear_wholesaler | offer |
| `supply.ice` / `bread` / `tinned_beans` / `oatcakes` | 6 / 4 / 3 / 2 | leblancs | shelf |
| `supply.soap_bar` | 3 | leblancs (8 showers or baths a bar; see `docs/world/washrooms-plan.md` §1a) | shelf |
| `instrument.depth_sounder` | 140 | leblancs | shelf (moves to the boat/gear wholesaler with its counter; see §5) |
| `instrument.fish_finder` | 650 | nmc_boat_gear_wholesaler (moved from leblancs; same dash cutout as the sounder) | offer |
| `instrument.gps` (chartplotter) | 1,800 | nmc_boat_gear_wholesaler (moved from nmc_chandlery; wants a wheelhouse boat) | offer |
| `instrument.loran` | 900 | nmc_boat_gear_wholesaler only (the leblancs placeholder and its St Peters vendor are gone; wants a wheelhouse boat) | offer |
| `license.clam` | 15 | leblancs | shelf |
| `license.cod` | 120 | nmc_harbourmaster | shelf |

### Traps

| Item | Price ₲ | Seller | State |
|---|---|---|---|
| `trap.lobster` | 120 | nmc_yard | shelf |
| `trap.crab` | 60 | nmc_yard | shelf |

### Hulls

| Item | Price ₲ | Seller | State |
|---|---|---|---|
| `boat.dory` | 400 + 300 repair | nmc_yard | shelf (also inherited) |
| `boat.dory_outboard` | 900 | hectors_barrel | shelf |
| `boat.punt` | 1,800 | nmc_yard | shelf |
| `boat.punt_upgraded` | 2,300 | nmc_yard | offer **added** |
| `boat.sport_skiff` | 8,200 | nmc_yard | offer **added** |
| `boat.sport_skiff_twin` | 8,400 | nmc_yard | offer **added** |
| `boat.sport_skiff_mk2` | 9,300 | nmc_yard | offer **added** |
| `boat.zodiac_frc` | 8,200 | nmc_yard | offer **added** |
| `boat.zodiac_hurricane` | 10,300 | nmc_yard | offer **added** |
| `boat.console_skiff` | 9,700 | nmc_yard | offer **added** |
| `boat.lobster_inshore_open_northumberland` / `_hardtop_` | 7,200 each | nmc_yard | offer **added** |
| `boat.cape_islander` | 17,400 | nmc_yard | offer **added** |
| `boat.lobster_boat` | 20,200 | nmc_yard | offer **added** |
| `boat.lobster_standard_open_northumberland` / `_hardtop_` | 20,200 each | nmc_yard | offer **added** |
| `boat.lobster_offshore_open_northumberland` / `_hardtop_` | 37,200 each | nmc_yard | offer **added** |
| `boat.side_dragger` | 216,000 | nmc_yard | offer **added** |
| Fundy lobster builds (6) | none | none | exempt, needs shop: a Bay of Fundy boatyard |
| Newfoundland lobster builds (6) | none | none | exempt, needs shop: a Newfoundland boatyard |
| `boat.bowrider`, `express_cruiser`, `go_fast`, `jet_ski`, `yacht_tender`, `sport_fisher_convertible`, `sport_fisher_skybridge`, `sloop_30` | none | none | exempt, needs shop: a pleasure-boat dealer |
| `boat.sloop_88`, `motor_yacht_84`, `expedition_yacht_120` | none | none | exempt, needs shop: a yacht broker |
| `boat.stern_trawler`, `stern_trawler_mk2`, `coastal_packet`, `tanker` | none | none | exempt, needs shop: a ship broker |

### Vehicles

| Item | Price ₲ | Seller | State |
|---|---|---|---|
| `vehicle.trike_200` | 800 | nmc_vehicle_dealer | offer |
| `vehicle.enduro_250` | 900 | nmc_vehicle_dealer | offer |
| `vehicle.utility_quad` | 1,200 | nmc_vehicle_dealer | offer |
| `vehicle.otter_8x8` | 2,500 | nmc_vehicle_dealer | offer **added** |
| `vehicle.modern_350` | 3,500 | nmc_vehicle_dealer | offer |
| `vehicle.hightop_van` | 4,000 | nmc_vehicle_dealer | offer |
| `vehicle.modern_2500` | 5,500 | nmc_vehicle_dealer | offer |
| `vehicle.modern_3500` | 7,000 | nmc_vehicle_dealer | offer |
| `vehicle.dually_3500` | 7,500 | nmc_vehicle_dealer | offer |
| `vehicle.cabover_box` (needs work) | 5,000 | nmc_industrial_yard | offer |
| `vehicle.conv_box` | 12,000 | nmc_industrial_yard | offer |
| `vehicle.classic_semi` / `aero_semi` | 25,000 each | nmc_industrial_yard | offer |

### Everything else

| Item | State |
|---|---|
| `watch.harbour_digital` | exempt: starting item; needs shop: a watch seller for a replacement |
| `gear.headlamp` | exempt: starting item, never bought (owner ruling 2026-11-22); its unlisted `Catalog/Gear/Headlamp` entry names no seller and exists only to give the KIT page its name and icon |
| `tackle.*` (cod jig, feathers, soft bait, spinner, spoon) | exempt: the rod's tackle box gives the player every lure, so there is no per-lure ownership to sell yet; needs that seam, then the NMC fishing gear wholesaler |
| `longline.groundfish_tub` | exempt: tub stock is counted but has no offer type; needs a tub offer, then the NMC fishing gear wholesaler |
| `golf_club.*` (4) | exempt: lent at the course; needs shop: a pro shop |
| `fuelstore.*` carriable cans and nozzles (36) | exempt: station stock at the pump; an empty can needs an offer type and a fuel-station till |

## 5. What is left

- **Put the waiting offers on a shelf:** run the store patch for each (seller, item) pair in §4 marked
  *offer*, then tick `Listed`. This needs the editor (world-content or tools-editor).
- **New shops the owner would need to approve:** a pleasure-boat dealer and a yacht broker, a ship
  broker, Fundy and Newfoundland yards (only if those regions arrive), a golf pro shop, a watch seller.
- **New offer types (code):** longline tubs, empty fuel cans, and per-lure tackle once tackle is owned.

### 5.1 Putting the two wholesalers on a shelf (steps 1 to 3 done in PR 1301; step 4 owed)

The offers are on file (data), so the rest is the counter. `StoreWiringPatch` can only give an existing
vendor its seller id, or add a `SupplyShop`/`InstrumentShop` as a copy of one the same root already holds;
it cannot create a counter from nothing, and neither wholesaler shell has a vendor to copy. So:

1. **A keeper and a way to open the book (world-content). Done (PR 1301).** Lionel Cormier keeps the
   boat/gear counter (`npc.lionel_cormier`, `dialogue.lionel_first`, `routine.lionel_cormier`: the counter
   from about 08:00, home from about 18:00) and Bernadette Pitre the fishing gear counter
   (`npc.bernadette_pitre`, `dialogue.bernadette_first`, `routine.bernadette_pitre`: 06:00 to 17:00). Both
   sellers name them as `KeeperNpcId`. Each dialogue opens with the browse row (added by step 2), then a
   question row.
2. **The counters, the keepers in the scene, the ticks and the browse rows (one editor run). Done (PR 1301).**
   `NmcWholesalerKeepers.Place` (*World > Nine Mile Creek Buildings > Place the Wholesaler Counters and
   Keepers*, or `-executeMethod HiddenHarbours.App.Editor.NmcWholesalerKeepers.PlaceAndSaveBatch`) does all
   of it in `NineMileCreek.unity`, inside the greybox shells (steps 22 and 23):
   - one root per counter (`NmcBoatGearWholesalerCounter`, `NmcFishingGearWholesalerCounter`) at the shell's
     `TradeCounter` block, holding one vendor per row of `StoreWiringPatch.NineMileCreekWholesalers`, each
     selling as its wholesaler with the chandlery `GearShop`'s wallet;
   - one root per keeper with its station table (counter indoors, in the shell's `BuildingInterior`; home out
     of sight, a short walk off from the door) and lane tree, and the keeper dressed, talkable and walking
     their day; each keeper joins the creek's `WorldInteractor`;
   - `Listed` ticked on each row's offer, and the keeper's browse row added first.

   `NineMileCreekWholesalers` is folded into `StoreWiringPatch.NineMileCreek`, and *World > Store Wiring >
   Apply the Store Patches* plans nothing (`StoreWiringTests` 1 and 3). An editor save re-serialises the
   whole creek scene (stale and new fields on every component), so only the step's additions were kept
   in the committed text: its new documents, the two keepers in the interactor's list, and the four new
   scene roots. A re-run should be spliced the same way.
3. **What the run lists.** `Listed` is one flag per offer, and guard 4 needs a committed vendor for every
   seller a listed offer names. The Loran, fish finder, chartplotter and rod rack name only a wholesaler, so
   they are the rows. The six baits, the clam shovel and the clam bucket also name `leblancs`, so listing
   them needs a St Peters `BaitShop`/`GearShop` too (the patch cannot copy either) or `leblancs` dropped
   from them; the rake, tongs, sack and basket wait for hand-gathering as well
   (`NmcWholesalerKeepers.Waiting`). A seat or owner call; a new row is all the step needs.
4. **Moves that wait for that counter, so nothing goes unbuyable in between:** the depth sounder off
   `leblancs` (approved: electronics are the boat/gear wholesaler's alone; it is the St Peters counter's
   `InstrumentShop` template today, and the StPeters builder and `StoreClerkContentTests` name it), and
   the fishing gear wholesaler joining the listed rod, capelin and both pots (the rod then leaves
   `nmc_chandlery`, the pots leave `nmc_yard`, per §2 of the stock proposal).

### 5.2 Wholesaler items that do not exist yet (owed; no offer invented here)

| Wholesaler | Missing item | Approved price ₲ | What it needs first |
|---|---|---|---|
| boat/gear | dome compass, flush compass, radar, wind dial, wind display | 80, 180, 3,000, 220, 600 | an `InstrumentOffer` per id (`BoatEquipment` already reserves the ids) and a purchase check that refuses factory-fitted or unsupported fits |
| boat/gear | autopilot | 6,000 (recommended candidate B) | a paid per-hull ownership record; today eligible hulls get heading hold outright (`BoatHullDef.AutopilotOffered`), so a sale would charge for something already granted. Needs a save step (v25, the owner's call) |
| fishing gear | cod jig, mackerel feathers, soft bait, spinner, spoon | 18, 16, 12, 14, 10 | an owned-tackle seam and a tackle offer type (the rod hands every lure today) |
| fishing gear | groundfish longline tub | 120 per tub | a counted tub offer type (`TubLocker` counts stock; there is no offer to buy it through) |

