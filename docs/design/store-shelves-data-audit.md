# Store shelves: data audit

The store already has bread, oatcakes, tinned beans, soap, ice, bait and fishing equipment in its sales data. It can buy the player's catch. What it cannot yet do is make those sales follow the island's supply runs. The new back-room ledger calculates nine bulk imports, but a kilogram of groceries is not yet connected to a loaf, a tin or a shelf space.

The next slice needs retail definitions for fuel and other missing goods, and an explicit conversion from each bulk good to the things the customer carries away. It also needs a rule for when the delivery reaches the shelves. Most existing goods have a price but no weight or expiry data. The storekeeper has a daily routine, but there is no opening-hours rule controlling purchases. Purchases can be deducted during a session; remembering their effect on the store after loading needs a later save change.

This is an inventory of what exists, not a shelf implementation or approval of new stock, prices, artwork or save fields. It serves P3 (a living working coast) and P4 (goods moved by hand).

## Basis and reading the tables

Snapshot: `81b0795db6bd3bd6a5352d66167d4863f0ae2f41`, 2026-11-22. The [living-economy plan](st-peters-living-economy.md) landed in PR 1455 (`b4c9f87652`); the back-room ledger landed in PR 1456 (`f62ec6b4f2`). The plan's earlier main hash and “draft” status describe when it was written, not this snapshot.

The census resolves tracked asset script GUIDs to their definition classes. It includes every authored retail supply, bait and gear offer, all foods/produce/fish, all island goods, plus the supporting item definitions below. Instruments and licences are listed as counter services, not proposed physical shelf objects. A seller in an offer means *catalog data says it stocks this*; it does not prove a placed or reachable merchant. The committed St Peters counter is checked separately below.

“Absent” means the field is not defined; it does not mean zero. “Unspecified” perishability means the definition cannot answer the question, even when its real-world counterpart spoils. No weights, retail prices or expiry rules have been invented. Money is coin (₲). File links point to the exact asset; all identifiers retain their existing spelling.

Scope boundary: boats, road vehicles, buildings, fixed furniture/appliances, industrial trawl/longline assemblies, staff and licences for businesses are not village shelf goods. Their prices are still covered by the code-price sweep. Art libraries and props are not inventory items. Carryable fuel containers and research container profiles are separately identified so their existence is not mistaken for a purchasable fuel product. There are no authored `CharacterGarmentDef` assets in this snapshot; outfit/skin assets are appearance definitions, not clothing stock.

## Existing retail offers

All rows here have absent item-weight fields. `Price` is per item except bait: `BaitDef.Price` is per bait unit and `BaitDef.LotSize` defaults to 10 (`BaitDef.cs:41`; no shipped bait asset overrides it), so the counter sells ten at once. Gear is owned by presence; supplies and bait are counted. `CarryCapacityUnits` on a bag/basket is catch capacity, not its own mass.

`SupplyDef`, `BaitDef`, `GearOffer`, `PotOffer`, `InstrumentOffer`, `LicenseDef` and `WatchDef` have no expiry fields. Durable equipment is marked “No”; foods/bait/ice are marked “Unspecified” rather than pretending to have a shelf-life. Ice protection durations exist on containers; they do not specify a bag's mass or shelf melt rate.

| Id | File | Type | Price fields (coin) | Unit | Weight | Perishable? | Already sold / used |
| --- | --- | --- | --- | --- | --- | --- | --- |
| `bait.capelin` | [Capelin](../../Assets/_Project/Data/Resources/Catalog/Bait/Capelin.asset) | BaitDef | Price 2/unit; lot 20 | 10 bait units/lot | Absent | Unspecified | `seller.leblancs`; TackleBox / fishing bait |
| `bait.fish_scrap` | [FishScrap](../../Assets/_Project/Data/Resources/Catalog/Bait/FishScrap.asset) | BaitDef | Price 1/unit; lot 10 | 10 bait units/lot | Absent | Unspecified | Unlisted in catalog; TackleBox / fishing bait |
| `bait.herring` | [Herring](../../Assets/_Project/Data/Resources/Catalog/Bait/Herring.asset) | BaitDef | Price 3/unit; lot 30 | 10 bait units/lot | Absent | Unspecified | Unlisted in catalog; TackleBox / fishing bait |
| `bait.mackerel` | [Mackerel](../../Assets/_Project/Data/Resources/Catalog/Bait/Mackerel.asset) | BaitDef | Price 4/unit; lot 40 | 10 bait units/lot | Absent | Unspecified | Unlisted in catalog; TackleBox / fishing bait |
| `bait.sea_worm` | [SeaWorm](../../Assets/_Project/Data/Resources/Catalog/Bait/SeaWorm.asset) | BaitDef | Price 4/unit; lot 40 | 10 bait units/lot | Absent | Unspecified | Unlisted in catalog; TackleBox / fishing bait |
| `bait.shucked_clam` | [ShuckedClam](../../Assets/_Project/Data/Resources/Catalog/Bait/ShuckedClam.asset) | BaitDef | Price 3/unit; lot 30 | 10 bait units/lot | Absent | Unspecified | Unlisted in catalog; TackleBox / fishing bait |
| `bait.squid_strip` | [SquidStrip](../../Assets/_Project/Data/Resources/Catalog/Bait/SquidStrip.asset) | BaitDef | Price 6/unit; lot 60 | 10 bait units/lot | Absent | Unspecified | Unlisted in catalog; TackleBox / fishing bait |
| `gear.bucket` | [ClamBucket](../../Assets/_Project/Data/Resources/Catalog/Gear/ClamBucket.asset) | GearOffer | Price 15 | one item | Absent | No (durable) | Unlisted in catalog; GearShop ownership |
| `gear.headlamp` | [Headlamp](../../Assets/_Project/Data/Resources/Catalog/Gear/Headlamp.asset) | GearOffer | Price 0 | one item | Absent | No (durable) | Unlisted in catalog; GearShop ownership |
| `gear.mussel_sack` | [MusselSack](../../Assets/_Project/Data/Resources/Catalog/Gear/MusselSack.asset) | GearOffer | Price 10 | one item | Absent | No (durable) | Unlisted in catalog; GearShop ownership; capacity 30 catch units |
| `gear.oyster_tongs` | [OysterTongs](../../Assets/_Project/Data/Resources/Catalog/Gear/OysterTongs.asset) | GearOffer | Price 45 | one item | Absent | No (durable) | Unlisted in catalog; GearShop ownership |
| `gear.rod` | [Rod](../../Assets/_Project/Data/Resources/Catalog/Gear/Rod.asset) | GearOffer | Price 60 | one item | Absent | No (durable) | `seller.leblancs`, `seller.nmc_chandlery`; GearShop ownership |
| `gear.rod_rack` | [RodRack](../../Assets/_Project/Data/Resources/Catalog/Gear/RodRack.asset) | GearOffer | Price 25 | one item | Absent | No (durable) | `seller.nmc_fishing_gear_wholesaler`; GearShop ownership |
| `gear.shellfish_basket` | [ShellfishBasket](../../Assets/_Project/Data/Resources/Catalog/Gear/ShellfishBasket.asset) | GearOffer | Price 20 | one item | Absent | No (durable) | Unlisted in catalog; GearShop ownership; capacity 20 catch units |
| `gear.shellfish_rake` | [ShellfishRake](../../Assets/_Project/Data/Resources/Catalog/Gear/ShellfishRake.asset) | GearOffer | Price 30 | one item | Absent | No (durable) | Unlisted in catalog; GearShop ownership |
| `gear.shovel` | [Shovel](../../Assets/_Project/Data/Resources/Catalog/Gear/Shovel.asset) | GearOffer | Price 25 | one item | Absent | No (durable) | Unlisted in catalog; GearShop ownership |
| `instrument.autopilot` | [AutopilotOffer](../../Assets/_Project/Data/Resources/Catalog/Instruments/AutopilotOffer.asset) | InstrumentOffer | Price 6000 | one fitting per hull | Absent | No (durable) | Unlisted in catalog; InstrumentShop fitting |
| `instrument.compass_dome` | [DomeCompassOffer](../../Assets/_Project/Data/Resources/Catalog/Instruments/DomeCompassOffer.asset) | InstrumentOffer | Price 70 | one fitting per hull | Absent | No (durable) | Unlisted in catalog; InstrumentShop fitting |
| `instrument.compass_flush` | [FlushCompassOffer](../../Assets/_Project/Data/Resources/Catalog/Instruments/FlushCompassOffer.asset) | InstrumentOffer | Price 140 | one fitting per hull | Absent | No (durable) | Unlisted in catalog; InstrumentShop fitting |
| `instrument.depth_sounder` | [DepthSounderOffer](../../Assets/_Project/Data/Resources/Catalog/Instruments/DepthSounderOffer.asset) | InstrumentOffer | Price 140 | one fitting per hull | Absent | No (durable) | `seller.leblancs`; InstrumentShop fitting |
| `instrument.fish_finder` | [FishFinderOffer](../../Assets/_Project/Data/Resources/Catalog/Instruments/FishFinderOffer.asset) | InstrumentOffer | Price 650 | one fitting per hull | Absent | No (durable) | `seller.nmc_boat_gear_wholesaler`; InstrumentShop fitting |
| `instrument.gps` | [ChartplotterOffer](../../Assets/_Project/Data/Resources/Catalog/Instruments/ChartplotterOffer.asset) | InstrumentOffer | Price 1800 | one fitting per hull | Absent | No (durable) | `seller.nmc_boat_gear_wholesaler`; InstrumentShop fitting |
| `instrument.loran` | [LoranOffer](../../Assets/_Project/Data/Resources/Catalog/Instruments/LoranOffer.asset) | InstrumentOffer | Price 900 | one fitting per hull | Absent | No (durable) | `seller.nmc_boat_gear_wholesaler`; InstrumentShop fitting |
| `instrument.radar` | [RadarOffer](../../Assets/_Project/Data/Resources/Catalog/Instruments/RadarOffer.asset) | InstrumentOffer | Price 3600 | one fitting per hull | Absent | No (durable) | Unlisted in catalog; InstrumentShop fitting |
| `instrument.wind_dial` | [WindDialOffer](../../Assets/_Project/Data/Resources/Catalog/Instruments/WindDialOffer.asset) | InstrumentOffer | Price 140 | one fitting per hull | Absent | No (durable) | Unlisted in catalog; InstrumentShop fitting |
| `instrument.wind_display` | [WindDisplayOffer](../../Assets/_Project/Data/Resources/Catalog/Instruments/WindDisplayOffer.asset) | InstrumentOffer | Price 650 | one fitting per hull | Absent | No (durable) | Unlisted in catalog; InstrumentShop fitting |
| `license.clam` | [ClamLicense](../../Assets/_Project/Data/Resources/Catalog/Licenses/ClamLicense.asset) | LicenseDef | Price 15 | one entitlement | Absent | N/A | `seller.leblancs`; catch permission |
| `license.cod` | [CodLicense](../../Assets/_Project/Data/Resources/Catalog/Licenses/CodLicense.asset) | LicenseDef | Price 120 | one entitlement | Absent | N/A | `seller.nmc_harbourmaster`; catch permission |
| `offer.crab_pot` | [CrabPotOffer](../../Assets/_Project/Data/Resources/Catalog/Shipwright/CrabPotOffer.asset) | PotOffer | Price 60 | one item | Absent | No (durable) | `seller.nmc_yard`; trap.crab / PotShop |
| `offer.lobster_pot` | [LobsterPotOffer](../../Assets/_Project/Data/Resources/Catalog/Shipwright/LobsterPotOffer.asset) | PotOffer | Price 120 | one item | Absent | No (durable) | `seller.nmc_yard`; trap.lobster / PotShop |
| `supply.bread` | [Bread](../../Assets/_Project/Data/Resources/Catalog/Supplies/Bread.asset) | SupplyDef | Price 4 | one loaf | Absent | Unspecified | `seller.leblancs`; FoodDef / eating and kitchen storage |
| `supply.ice` | [Ice](../../Assets/_Project/Data/Resources/Catalog/Supplies/Ice.asset) | SupplyDef | Price 6 | one bag | Absent | Unspecified | `seller.leblancs`; cold storage via SupplyLocker |
| `supply.oatcakes` | [Oatcakes](../../Assets/_Project/Data/Resources/Catalog/Supplies/Oatcakes.asset) | SupplyDef | Price 2 | one packet | Absent | Unspecified | `seller.leblancs`; FoodDef / eating and kitchen storage |
| `supply.soap_bar` | [SoapBar](../../Assets/_Project/Data/Resources/Catalog/Supplies/SoapBar.asset) | SupplyDef | Price 3 | one bar | Absent | No (soap) | `seller.leblancs`; washing via SupplyLocker |
| `supply.tinned_beans` | [TinnedBeans](../../Assets/_Project/Data/Resources/Catalog/Supplies/TinnedBeans.asset) | SupplyDef | Price 3 | one tin | Absent | Unspecified | `seller.leblancs`; FoodDef / eating and kitchen storage |
| `watch.harbour_digital` | [HarbourDigital](../../Assets/_Project/Data/Resources/Watches/HarbourDigital.asset) | WatchDef | Price 0 | one watch | Absent | No (durable) | StartsOwned=1; no seller; watch equipment (`gear.watch` legacy alias) |

## Food, produce and catch

`FoodDef` is an eating description keyed by `ItemId`, not a second shop item or price source. All eleven are listed. For an underlying supply, produce or fish, the price/weight below come from that underlying definition, not from `FoodDef`. The four cooked outputs have no retail offers. Prepared fillets/cleaned fish are recipe outputs, listed later; absence of an edible `FoodDef` for an intermediate is not evidence that its recipe is missing.

The kitchen has an `IFoodPerishability` seam, but its shipped default is `NothingSpoilsPerishability` (`Core/Kitchen/KitchenSeams.cs:44`). No other implementation is composed in production source. Catch freshness has its own real `FishSpeciesDef.SpoilPerDay` path; that does not automatically make a stored loaf or cooked dish age.

| Food id | File | Price fields (coin) | Unit | Weight | Perishable? | Already used |
| --- | --- | --- | --- | --- | --- | --- |
| `food.atlantic_cod` | [AtlanticCod](../../Assets/_Project/Data/Resources/Food/AtlanticCod.asset) | Underlying BaseValue 14 | one underlying item/portion | 2–12 kg/catch | Yes as catch; see fish table | Item `fish.atlantic_cod`; requires cooking; prep → `food.cod_fillet`; cook → `food.pan_fried_cod` |
| `food.bread` | [Bread](../../Assets/_Project/Data/Resources/Food/Bread.asset) | Underlying Price 4 | one underlying item/portion | Absent | Unspecified; no FoodDef rate | Item `supply.bread`; edible as held |
| `food.carrot` | [Carrot](../../Assets/_Project/Data/Resources/Food/Carrot.asset) | Underlying BaseValue 2 | one underlying item/portion | Absent | Unspecified; no FoodDef rate | Item `produce.carrot`; edible as held |
| `food.grilled_mackerel` | [GrilledMackerel](../../Assets/_Project/Data/Resources/Food/GrilledMackerel.asset) | Absent (no retail offer) | one underlying item/portion | Absent | Unspecified; no FoodDef rate | Item `food.grilled_mackerel`; edible as held |
| `food.hot_beans` | [HotBeans](../../Assets/_Project/Data/Resources/Food/HotBeans.asset) | Absent (no retail offer) | one underlying item/portion | Absent | Unspecified; no FoodDef rate | Item `food.hot_beans`; edible as held |
| `food.mackerel` | [Mackerel](../../Assets/_Project/Data/Resources/Food/Mackerel.asset) | Underlying BaseValue 10 | one underlying item/portion | 0.3–1.5 kg/catch | Yes as catch; see fish table | Item `fish.mackerel`; requires cooking; prep → `food.mackerel_cleaned`; cook → `food.grilled_mackerel` |
| `food.oatcakes` | [Oatcakes](../../Assets/_Project/Data/Resources/Food/Oatcakes.asset) | Underlying Price 2 | one underlying item/portion | Absent | Unspecified; no FoodDef rate | Item `supply.oatcakes`; edible as held |
| `food.pan_fried_cod` | [PanFriedCod](../../Assets/_Project/Data/Resources/Food/PanFriedCod.asset) | Absent (no retail offer) | one underlying item/portion | Absent | Unspecified; no FoodDef rate | Item `food.pan_fried_cod`; edible as held |
| `food.pan_fried_fish` | [PanFriedFish](../../Assets/_Project/Data/Resources/Food/PanFriedFish.asset) | Absent (no retail offer) | one underlying item/portion | Absent | Unspecified; no FoodDef rate | Item `food.pan_fried_fish`; edible as held |
| `food.tinned_beans` | [TinnedBeans](../../Assets/_Project/Data/Resources/Food/TinnedBeans.asset) | Underlying Price 3 | one underlying item/portion | Absent | Unspecified; no FoodDef rate | Item `supply.tinned_beans`; edible as held |
| `food.turnip` | [Turnip](../../Assets/_Project/Data/Resources/Food/Turnip.asset) | Underlying BaseValue 3 | one underlying item/portion | Absent | Unspecified; no FoodDef rate | Item `produce.turnip`; edible as held |

| Produce id | File | Price fields (coin) | Unit | Weight | Perishable? | Already used |
| --- | --- | --- | --- | --- | --- | --- |
| `produce.cabbage` | [Cabbage](../../Assets/_Project/Data/Resources/Farming/Produce/Cabbage.asset) | BaseValue 5 | one harvested item | Absent | Unspecified | `crop.cabbage` harvest; farm/kitchen/gift item; no catalog seller |
| `produce.carrot` | [Carrot](../../Assets/_Project/Data/Resources/Farming/Produce/Carrot.asset) | BaseValue 2 | one harvested item | Absent | Unspecified | `crop.carrot` harvest; farm/kitchen/gift item; no catalog seller |
| `produce.potato` | [Potato](../../Assets/_Project/Data/Resources/Farming/Produce/Potato.asset) | BaseValue 2 | one harvested item | Absent | Unspecified | `crop.potato` harvest; farm/kitchen/gift item; no catalog seller |
| `produce.turnip` | [Turnip](../../Assets/_Project/Data/Resources/Farming/Produce/Turnip.asset) | BaseValue 3 | one harvested item | Absent | Unspecified | `crop.turnip` harvest; farm/kitchen/gift item; no catalog seller |

All 19 `FishSpeciesDef` assets follow. These are candidate local fresh goods, **not** existing buy-from-store listings. The store's `FishBuyer` accepts catch through the common sale path. `BaseValue` is the marginal-price input per catch unit (not coin/kg); actual payment applies supply/demand, outlet level and freshness. `SellService` ignores spoiled catch. The min/max mass is per generated catch, not an authored retail pack. Each row is used by the catch/freshness system and the market buyer; the cod and mackerel additionally have the food mappings above.

| Fish id | File | BaseValue (coin/unit) | Unit | Weight kg (min–max) | Perishable? |
| --- | --- | --- | --- | --- | --- |
| `fish.american_oyster` | [AmericanOyster](../../Assets/_Project/Data/Fish/AmericanOyster.asset) | 4 | one catch unit | 0.05–0.15 | Yes; SpoilPerDay 0.35 |
| `fish.atlantic_cod` | [AtlanticCod](../../Assets/_Project/Data/Fish/AtlanticCod.asset) | 14 | one catch unit | 2–12 | Yes; SpoilPerDay 0.75 |
| `fish.atlantic_herring` | [AtlanticHerring](../../Assets/_Project/Data/Fish/AtlanticHerring.asset) | 2 | one catch unit | 0.1–0.5 | Yes; SpoilPerDay 1.5 |
| `fish.atlantic_surf_clam` | [AtlanticSurfClam](../../Assets/_Project/Data/Fish/AtlanticSurfClam.asset) | 3 | one catch unit | 0.2–0.5 | Yes; SpoilPerDay 0.3 |
| `fish.atlantic_tomcod` | [AtlanticTomcod](../../Assets/_Project/Data/Fish/AtlanticTomcod.asset) | 2 | one catch unit | 0.05–0.3 | Yes; SpoilPerDay 0.9 |
| `fish.blue_mussel` | [BlueMussel](../../Assets/_Project/Data/Fish/BlueMussel.asset) | 1 | one catch unit | 0.02–0.05 | Yes; SpoilPerDay 0.5 |
| `fish.cunner` | [Cunner](../../Assets/_Project/Data/Fish/Cunner.asset) | 3 | one catch unit | 0.1–0.6 | Yes; SpoilPerDay 0.75 |
| `fish.green_crab` | [GreenCrab](../../Assets/_Project/Data/Fish/GreenCrab.asset) | 1 | one catch unit | 0.02–0.1 | Yes; SpoilPerDay 0.4 |
| `fish.haddock` | [Haddock](../../Assets/_Project/Data/Fish/Haddock.asset) | 16 | one catch unit | 1–6 | Yes; SpoilPerDay 0.75 |
| `fish.lobster` | [AmericanLobster](../../Assets/_Project/Data/Fish/AmericanLobster.asset) | 28 | one catch unit | 0.4–4 | Yes; SpoilPerDay 0.35 |
| `fish.mackerel` | [Mackerel](../../Assets/_Project/Data/Fish/Mackerel.asset) | 10 | one catch unit | 0.3–1.5 | Yes; SpoilPerDay 1.5 |
| `fish.northern_quahog` | [NorthernQuahog](../../Assets/_Project/Data/Fish/NorthernQuahog.asset) | 3 | one catch unit | 0.1–0.3 | Yes; SpoilPerDay 0.3 |
| `fish.pollock` | [Pollock](../../Assets/_Project/Data/Fish/Pollock.asset) | 11 | one catch unit | 1.5–9 | Yes; SpoilPerDay 0.9 |
| `fish.rainbow_smelt` | [RainbowSmelt](../../Assets/_Project/Data/Fish/RainbowSmelt.asset) | 3 | one catch unit | 0.02–0.1 | Yes; SpoilPerDay 1.0 |
| `fish.rock_crab` | [RockCrab](../../Assets/_Project/Data/Fish/RockCrab.asset) | 4 | one catch unit | 0.1–0.4 | Yes; SpoilPerDay 0.4 |
| `fish.sea_scallop` | [SeaScallop](../../Assets/_Project/Data/Fish/SeaScallop.asset) | 18 | one catch unit | 0.1–0.3 | Yes; SpoilPerDay 0.5 |
| `fish.soft_shell_clam` | [SoftShellClam](../../Assets/_Project/Data/Fish/SoftShellClam.asset) | 2 | one catch unit | 0.05–0.2 | Yes; SpoilPerDay 0.5 |
| `fish.striped_bass` | [StripedBass](../../Assets/_Project/Data/Fish/StripedBass.asset) | 17 | one catch unit | 1–25 | Yes; SpoilPerDay 1 |
| `fish.winter_flounder` | [WinterFlounder](../../Assets/_Project/Data/Fish/WinterFlounder.asset) | 7 | one catch unit | 0.2–2 | Yes; SpoilPerDay 0.75 |

## Bulk imports: all nine IslandGoodDef assets

All nine have `Source=MainlandOverBar` and are referenced by `islandledger.st_peters`. None has a price field, retail listing, expiry field or pack-to-listing reference. Every row is already used in supply-plan arithmetic and ledger tests, not a live store transaction. “Carried kg/unit” is calculated as `DailyMassKg / DailyUnits`, including the authored packaging share; it is not necessarily a liquid density. Their container descriptions are text, not resolved inventory references.

| Id | File | Price | Unit | Daily units | Daily carried kg | Carried kg/unit | Daily carried L | Player allowance | Winter × | Perishable? |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| `islandgood.bait` | [Bait](../../Assets/_Project/Data/Resources/IslandSupply/Goods/Bait.asset) | Absent | kg | 10 | 10 | 1 | 12 | 0.25 | 1 | Unspecified |
| `islandgood.gasoline` | [Gasoline](../../Assets/_Project/Data/Resources/IslandSupply/Goods/Gasoline.asset) | Absent | L | 20 | 17 | 0.85 | 24 | 0 | 1 | Unspecified |
| `islandgood.groceries` | [Groceries](../../Assets/_Project/Data/Resources/IslandSupply/Goods/Groceries.asset) | Absent | kg | 18 | 18 | 1 | 40 | 0.25 | 1 | Unspecified |
| `islandgood.hardware` | [Hardware](../../Assets/_Project/Data/Resources/IslandSupply/Goods/Hardware.asset) | Absent | kg | 10 | 10 | 1 | 15 | 0.25 | 1 | Unspecified |
| `islandgood.ice` | [Ice](../../Assets/_Project/Data/Resources/IslandSupply/Goods/Ice.asset) | Absent | kg | 20 | 20 | 1 | 22 | 0.25 | 1 | Unspecified |
| `islandgood.mail_parcels` | [MailParcels](../../Assets/_Project/Data/Resources/IslandSupply/Goods/MailParcels.asset) | Absent | kg | 3 | 3 | 1 | 10 | 0 | 1 | Unspecified |
| `islandgood.medicine_misc` | [MedicineMisc](../../Assets/_Project/Data/Resources/IslandSupply/Goods/MedicineMisc.asset) | Absent | kg | 2 | 2 | 1 | 5 | 0 | 1 | Unspecified |
| `islandgood.propane` | [Propane](../../Assets/_Project/Data/Resources/IslandSupply/Goods/Propane.asset) | Absent | kg | 2.5 | 4.7 | 1.88 | 9 | 0 | 1 | Unspecified |
| `islandgood.stove_oil` | [StoveOil](../../Assets/_Project/Data/Resources/IslandSupply/Goods/StoveOil.asset) | Absent | L | 15 | 15 | 1 | 18 | 0 | 2 | Unspecified |

The totals are 99.7 kg / 155 L per ordinary day, or 114.2 kg / 177.25 L including player allowances. The 20 L gasoline line uses 17 kg gross in the import estimate; `fuelstore.gas_jerry_s20` is 17.1 kg full. Preserve that visible 0.1 kg difference until an explicit conversion policy chooses the authority. `carrier.jerry_can_20l` has 14.9 kg payload plus 2.2 kg tare; payload capacity must not be read as full-can weight. The zero player allowance on gasoline/propane is not a no-sales flag; it means the ledger reserves no extra demand for the player.

## Supporting item definitions and unsold candidates

These definitions can support a store product, but none creates a sale by itself. “No seller” is deliberate: a price on a separate offer must not be inferred from a physical or gameplay definition. Fixed stowage and appliances are excluded; the separately offered rod rack and portable cooler are included.

| Id | File | Type | Price fields | Unit | Weight | Perishable? | Already used |
| --- | --- | --- | --- | --- | --- | --- | --- |
| `bulk.fish_on_ice` | [FishOnIce](../../Assets/_Project/Data/Resources/IslandSupply/Bulk/FishOnIce.asset) | BulkMaterialDef | Absent | L of loose material | 0.85 kg/L | Unspecified | island supply bulk-load arithmetic; no seller |
| `bulk.potatoes` | [Potatoes](../../Assets/_Project/Data/Resources/IslandSupply/Bulk/Potatoes.asset) | BulkMaterialDef | Absent | L of loose material | 0.67 kg/L | Unspecified | island supply bulk-load arithmetic; no seller |
| `bulk.wet_seaweed` | [WetSeaweed](../../Assets/_Project/Data/Resources/IslandSupply/Bulk/WetSeaweed.asset) | BulkMaterialDef | Absent | L of loose material | 0.6 kg/L | Unspecified | island supply bulk-load arithmetic; no seller |
| `crop.cabbage` | [Cabbage](../../Assets/_Project/Data/Resources/Farming/Crops/Cabbage.asset) | CropDef | Absent | crop schedule (not a seed packet) | Absent | N/A; growth schedule | farm calendar → produce.cabbage; no seed item/price |
| `crop.carrot` | [Carrot](../../Assets/_Project/Data/Resources/Farming/Crops/Carrot.asset) | CropDef | Absent | crop schedule (not a seed packet) | Absent | N/A; growth schedule | farm calendar → produce.carrot; no seed item/price |
| `crop.potato` | [Potato](../../Assets/_Project/Data/Resources/Farming/Crops/Potato.asset) | CropDef | Absent | crop schedule (not a seed packet) | Absent | N/A; growth schedule | farm calendar → produce.potato; no seed item/price |
| `crop.turnip` | [Turnip](../../Assets/_Project/Data/Resources/Farming/Crops/Turnip.asset) | CropDef | Absent | crop schedule (not a seed packet) | Absent | N/A; growth schedule | farm calendar → produce.turnip; no seed item/price |
| `container.fish_tray` | [FishTray](../../Assets/_Project/Data/Boats/Containers/FishTray.asset) | DeckContainerDef | Absent | one item | Absent | No (durable) | deck fish tray; ice duration fields; no offer |
| `golf_club.driver` | [Driver](../../Assets/_Project/Data/Golf/Driver.asset) | GolfClubDef | Absent | one item | Absent | No (durable) | golf shot simulation; no catalog offer |
| `golf_club.iron` | [Iron](../../Assets/_Project/Data/Golf/Iron.asset) | GolfClubDef | Absent | one item | Absent | No (durable) | golf shot simulation; no catalog offer |
| `golf_club.putter` | [Putter](../../Assets/_Project/Data/Golf/Putter.asset) | GolfClubDef | Absent | one item | Absent | No (durable) | golf shot simulation; no catalog offer |
| `golf_club.wedge` | [Wedge](../../Assets/_Project/Data/Golf/Wedge.asset) | GolfClubDef | Absent | one item | Absent | No (durable) | golf shot simulation; no catalog offer |
| `kitchen_storage.cooler` | [Cooler](../../Assets/_Project/Data/Resources/Kitchen/Storage/Cooler.asset) | KitchenStorageDef | Absent | one item | Absent | No (durable) | kitchen cold storage, 12 items; no offer |
| `recipe.cod_fillet` | [CodFillet](../../Assets/_Project/Data/Resources/Recipes/CodFillet.asset) | RecipeDef | Absent | one recipe output | Absent | Unspecified for output | kitchen recipe → food.cod_fillet; no retail offer |
| `recipe.grilled_mackerel` | [GrilledMackerel](../../Assets/_Project/Data/Resources/Recipes/GrilledMackerel.asset) | RecipeDef | Absent | one recipe output | Absent | Unspecified for output | kitchen recipe → food.grilled_mackerel; no retail offer |
| `recipe.groundfish_fillet` | [GroundfishFillet](../../Assets/_Project/Data/Resources/Recipes/GroundfishFillet.asset) | RecipeDef | Absent | one recipe output | Absent | Unspecified for output | kitchen recipe → food.white_fish_fillet; no retail offer |
| `recipe.heated_beans` | [HeatedBeans](../../Assets/_Project/Data/Resources/Recipes/HeatedBeans.asset) | RecipeDef | Absent | one recipe output | Absent | Unspecified for output | kitchen recipe → food.hot_beans; no retail offer |
| `recipe.mackerel_cleaned` | [MackerelCleaned](../../Assets/_Project/Data/Resources/Recipes/MackerelCleaned.asset) | RecipeDef | Absent | one recipe output | Absent | Unspecified for output | kitchen recipe → food.mackerel_cleaned; no retail offer |
| `recipe.pan_fried_cod` | [PanFriedCod](../../Assets/_Project/Data/Resources/Recipes/PanFriedCod.asset) | RecipeDef | Absent | one recipe output | Absent | Unspecified for output | kitchen recipe → food.pan_fried_cod; no retail offer |
| `recipe.pan_fried_fish` | [PanFriedFish](../../Assets/_Project/Data/Resources/Recipes/PanFriedFish.asset) | RecipeDef | Absent | one recipe output | Absent | Unspecified for output | kitchen recipe → food.pan_fried_fish; no retail offer |
| `stowage.rod_rack` | [RodRack](../../Assets/_Project/Data/Stowage/RodRack.asset) | StowageDef | via gear.rod_rack (25) | one item | Absent | No (durable) | rod rack storage, 4 rods |
| `tackle.cod_jig` | [CodJig](../../Assets/_Project/Data/Tackle/CodJig.asset) | TackleDef | Absent | one item | Absent | No (durable) | TackleBox / lure selection; no catalog offer |
| `tackle.mackerel_feathers` | [MackerelFeathers](../../Assets/_Project/Data/Tackle/MackerelFeathers.asset) | TackleDef | Absent | one item | Absent | No (durable) | TackleBox / lure selection; no catalog offer |
| `tackle.soft_bait` | [SoftBait](../../Assets/_Project/Data/Tackle/SoftBait.asset) | TackleDef | Absent | one item | Absent | No (durable) | TackleBox / lure selection; no catalog offer |
| `tackle.spinner` | [Spinner](../../Assets/_Project/Data/Tackle/Spinner.asset) | TackleDef | Absent | one item | Absent | No (durable) | TackleBox / lure selection; no catalog offer |
| `tackle.spoon` | [Spoon](../../Assets/_Project/Data/Tackle/Spoon.asset) | TackleDef | Absent | one item | Absent | No (durable) | TackleBox / lure selection; no catalog offer |
| `tool.rod` | [Rod](../../Assets/_Project/Data/Tools/Rod.asset) | ToolDef | via gear.rod | one item | Absent | No (durable) | held/carried tool for gear.rod |
| `tool.shovel` | [Shovel](../../Assets/_Project/Data/Tools/Shovel.asset) | ToolDef | via gear.shovel | one item | Absent | No (durable) | held/carried tool for gear.shovel |
| `trap.crab` | [CrabTrap](../../Assets/_Project/Data/Traps/CrabTrap.asset) | TrapDef | via offer.crab_pot | one item | Absent | No (durable) | pot fishing; bait bait.fish_scrap |
| `trap.lobster` | [LobsterTrap](../../Assets/_Project/Data/Traps/LobsterTrap.asset) | TrapDef | via offer.lobster_pot | one item | Absent | No (durable) | pot fishing; bait bait.herring |

All fourteen carrier definitions are included to distinguish containers from their contents, including the larger transport kit that cannot be a shelf item. None has a price or spoilage field. Their use is supply capacity arithmetic; bags, pails, cans and carts have no retail offer just because they have a `CarrierDef`. Capacity is payload; tare is the empty carrier's mass.

| Id | File | Price | Unit | Tare kg | Payload kg / L | Perishable? / use |
| --- | --- | --- | --- | --- | --- | --- |
| `carrier.atv_front_rack` | [AtvFrontRack](../../Assets/_Project/Data/Resources/IslandSupply/Carriers/AtvFrontRack.asset) | Absent | one carrier | 0 | 45 / 50 | No; supply-plan kit |
| `carrier.atv_rear_rack` | [AtvRearRack](../../Assets/_Project/Data/Resources/IslandSupply/Carriers/AtvRearRack.asset) | Absent | one carrier | 0 | 90 / 50 | No; supply-plan kit |
| `carrier.atv_trailer` | [AtvTrailer](../../Assets/_Project/Data/Resources/IslandSupply/Carriers/AtvTrailer.asset) | Absent | one carrier | 60 | 250 / 320 | No; supply-plan kit |
| `carrier.drum_205l` | [Drum205](../../Assets/_Project/Data/Resources/IslandSupply/Carriers/Drum205.asset) | Absent | one carrier | 18 | 172.2 / 205 | No; supply-plan kit |
| `carrier.garden_wagon` | [GardenWagon](../../Assets/_Project/Data/Resources/IslandSupply/Carriers/GardenWagon.asset) | Absent | one carrier | 18 | 80 / 120 | No; supply-plan kit |
| `carrier.hand_cart` | [HandCart](../../Assets/_Project/Data/Resources/IslandSupply/Carriers/HandCart.asset) | Absent | one carrier | 12 | 70 / 150 | No; supply-plan kit |
| `carrier.hand_pail` | [HandPail](../../Assets/_Project/Data/Resources/IslandSupply/Carriers/HandPail.asset) | Absent | one carrier | 1 | 8 / 11 | No; supply-plan kit |
| `carrier.jerry_can_20l` | [JerryCan20](../../Assets/_Project/Data/Resources/IslandSupply/Carriers/JerryCan20.asset) | Absent | one carrier | 2.2 | 14.9 / 20 | No; supply-plan kit |
| `carrier.pallet_gma` | [PalletGma](../../Assets/_Project/Data/Resources/IslandSupply/Carriers/PalletGma.asset) | Absent | one carrier | 20 | 1000 / 1486 | No; supply-plan kit |
| `carrier.pickup_bed` | [PickupBed](../../Assets/_Project/Data/Resources/IslandSupply/Carriers/PickupBed.asset) | Absent | one carrier | 0 | 1000 / 1700 | No; supply-plan kit |
| `carrier.rucksack` | [Rucksack](../../Assets/_Project/Data/Resources/IslandSupply/Carriers/Rucksack.asset) | Absent | one carrier | 1.2 | 15 / 40 | No; supply-plan kit |
| `carrier.shopping_bag` | [ShoppingBag](../../Assets/_Project/Data/Resources/IslandSupply/Carriers/ShoppingBag.asset) | Absent | one carrier | 0.1 | 8 / 20 | No; supply-plan kit |
| `carrier.tractor_bucket` | [TractorBucket](../../Assets/_Project/Data/Resources/IslandSupply/Carriers/TractorBucket.asset) | Absent | one carrier | 0 | 600 / 350 | No; supply-plan kit |
| `carrier.tractor_forks` | [TractorForks](../../Assets/_Project/Data/Resources/IslandSupply/Carriers/TractorForks.asset) | Absent | one carrier | 0 | 600 / 1486 | No; supply-plan kit |

The following are all `FuelContainerDef` assets marked `Carriable=1` (36), including eight nozzles which are hardware, not fuel stock. They have physical data but no price, seller or inventory transfer. Fuel grades are `gas`, `diesel`, `mixed` and `oil`; `oil` alone does not establish a retail stove-oil product. The other 48 fuel-container assets are non-carriable drums/totes/skids/bulk/pump fixtures and are outside shelf stock. No propane `FuelContainerDef` exists.

| Id | File | Price | Unit / capacity L | Tare / full kg | Perishable? / use |
| --- | --- | --- | --- | --- | --- |
| `fuelstore.diesel_jerry_s10` | [jerry_s10_diesel](../../Assets/_Project/Data/FuelContainers/jerry_s10_diesel.asset) | Absent | one vessel / 10 | 1.3 / 9.7 | No authored decay; physical fuel prop only |
| `fuelstore.diesel_jerry_s20` | [jerry_s20_diesel](../../Assets/_Project/Data/FuelContainers/jerry_s20_diesel.asset) | Absent | one vessel / 20 | 2.2 / 19 | No authored decay; physical fuel prop only |
| `fuelstore.diesel_jerry_s25` | [jerry_s25_diesel](../../Assets/_Project/Data/FuelContainers/jerry_s25_diesel.asset) | Absent | one vessel / 25 | 2.7 / 23.7 | No authored decay; physical fuel prop only |
| `fuelstore.diesel_jerry_s5` | [jerry_s5_diesel](../../Assets/_Project/Data/FuelContainers/jerry_s5_diesel.asset) | Absent | one vessel / 5 | 0.8 / 5 | No authored decay; physical fuel prop only |
| `fuelstore.diesel_jug_s1` | [jug_s1_diesel](../../Assets/_Project/Data/FuelContainers/jug_s1_diesel.asset) | Absent | one vessel / 1 | 0.07 / 0.91 | No authored decay; physical fuel prop only |
| `fuelstore.diesel_jug_s10` | [jug_s10_diesel](../../Assets/_Project/Data/FuelContainers/jug_s10_diesel.asset) | Absent | one vessel / 10 | 0.55 / 8.95 | No authored decay; physical fuel prop only |
| `fuelstore.diesel_jug_s4` | [jug_s4_diesel](../../Assets/_Project/Data/FuelContainers/jug_s4_diesel.asset) | Absent | one vessel / 4 | 0.24 / 3.6 | No authored decay; physical fuel prop only |
| `fuelstore.diesel_nozzle_sauto` | [nozzle_sAuto_diesel](../../Assets/_Project/Data/FuelContainers/nozzle_sAuto_diesel.asset) | Absent | one vessel / 0 | 1.4 / 1.4 | No authored decay; physical fuel prop only |
| `fuelstore.diesel_nozzle_shigh` | [nozzle_sHigh_diesel](../../Assets/_Project/Data/FuelContainers/nozzle_sHigh_diesel.asset) | Absent | one vessel / 0 | 2.6 / 2.6 | No authored decay; physical fuel prop only |
| `fuelstore.gas_jerry_s10` | [jerry_s10_gas](../../Assets/_Project/Data/FuelContainers/jerry_s10_gas.asset) | Absent | one vessel / 10 | 1.3 / 8.75 | No authored decay; physical fuel prop only |
| `fuelstore.gas_jerry_s20` | [jerry_s20_gas](../../Assets/_Project/Data/FuelContainers/jerry_s20_gas.asset) | Absent | one vessel / 20 | 2.2 / 17.1 | No authored decay; physical fuel prop only |
| `fuelstore.gas_jerry_s25` | [jerry_s25_gas](../../Assets/_Project/Data/FuelContainers/jerry_s25_gas.asset) | Absent | one vessel / 25 | 2.7 / 21.32 | No authored decay; physical fuel prop only |
| `fuelstore.gas_jerry_s5` | [jerry_s5_gas](../../Assets/_Project/Data/FuelContainers/jerry_s5_gas.asset) | Absent | one vessel / 5 | 0.8 / 4.53 | No authored decay; physical fuel prop only |
| `fuelstore.gas_jug_s1` | [jug_s1_gas](../../Assets/_Project/Data/FuelContainers/jug_s1_gas.asset) | Absent | one vessel / 1 | 0.07 / 0.81 | No authored decay; physical fuel prop only |
| `fuelstore.gas_jug_s10` | [jug_s10_gas](../../Assets/_Project/Data/FuelContainers/jug_s10_gas.asset) | Absent | one vessel / 10 | 0.55 / 8 | No authored decay; physical fuel prop only |
| `fuelstore.gas_jug_s4` | [jug_s4_gas](../../Assets/_Project/Data/FuelContainers/jug_s4_gas.asset) | Absent | one vessel / 4 | 0.24 / 3.22 | No authored decay; physical fuel prop only |
| `fuelstore.gas_nozzle_sauto` | [nozzle_sAuto_gas](../../Assets/_Project/Data/FuelContainers/nozzle_sAuto_gas.asset) | Absent | one vessel / 0 | 1.4 / 1.4 | No authored decay; physical fuel prop only |
| `fuelstore.gas_nozzle_shigh` | [nozzle_sHigh_gas](../../Assets/_Project/Data/FuelContainers/nozzle_sHigh_gas.asset) | Absent | one vessel / 0 | 2.6 / 2.6 | No authored decay; physical fuel prop only |
| `fuelstore.mixed_jerry_s10` | [jerry_s10_mixed](../../Assets/_Project/Data/FuelContainers/jerry_s10_mixed.asset) | Absent | one vessel / 10 | 1.3 / 8.82 | No authored decay; physical fuel prop only |
| `fuelstore.mixed_jerry_s20` | [jerry_s20_mixed](../../Assets/_Project/Data/FuelContainers/jerry_s20_mixed.asset) | Absent | one vessel / 20 | 2.2 / 17.24 | No authored decay; physical fuel prop only |
| `fuelstore.mixed_jerry_s25` | [jerry_s25_mixed](../../Assets/_Project/Data/FuelContainers/jerry_s25_mixed.asset) | Absent | one vessel / 25 | 2.7 / 21.5 | No authored decay; physical fuel prop only |
| `fuelstore.mixed_jerry_s5` | [jerry_s5_mixed](../../Assets/_Project/Data/FuelContainers/jerry_s5_mixed.asset) | Absent | one vessel / 5 | 0.8 / 4.56 | No authored decay; physical fuel prop only |
| `fuelstore.mixed_jug_s1` | [jug_s1_mixed](../../Assets/_Project/Data/FuelContainers/jug_s1_mixed.asset) | Absent | one vessel / 1 | 0.07 / 0.82 | No authored decay; physical fuel prop only |
| `fuelstore.mixed_jug_s10` | [jug_s10_mixed](../../Assets/_Project/Data/FuelContainers/jug_s10_mixed.asset) | Absent | one vessel / 10 | 0.55 / 8.07 | No authored decay; physical fuel prop only |
| `fuelstore.mixed_jug_s4` | [jug_s4_mixed](../../Assets/_Project/Data/FuelContainers/jug_s4_mixed.asset) | Absent | one vessel / 4 | 0.24 / 3.25 | No authored decay; physical fuel prop only |
| `fuelstore.mixed_nozzle_sauto` | [nozzle_sAuto_mixed](../../Assets/_Project/Data/FuelContainers/nozzle_sAuto_mixed.asset) | Absent | one vessel / 0 | 1.4 / 1.4 | No authored decay; physical fuel prop only |
| `fuelstore.mixed_nozzle_shigh` | [nozzle_sHigh_mixed](../../Assets/_Project/Data/FuelContainers/nozzle_sHigh_mixed.asset) | Absent | one vessel / 0 | 2.6 / 2.6 | No authored decay; physical fuel prop only |
| `fuelstore.oil_jerry_s10` | [jerry_s10_oil](../../Assets/_Project/Data/FuelContainers/jerry_s10_oil.asset) | Absent | one vessel / 10 | 1.3 / 10.15 | No authored decay; physical fuel prop only |
| `fuelstore.oil_jerry_s20` | [jerry_s20_oil](../../Assets/_Project/Data/FuelContainers/jerry_s20_oil.asset) | Absent | one vessel / 20 | 2.2 / 19.9 | No authored decay; physical fuel prop only |
| `fuelstore.oil_jerry_s25` | [jerry_s25_oil](../../Assets/_Project/Data/FuelContainers/jerry_s25_oil.asset) | Absent | one vessel / 25 | 2.7 / 24.82 | No authored decay; physical fuel prop only |
| `fuelstore.oil_jerry_s5` | [jerry_s5_oil](../../Assets/_Project/Data/FuelContainers/jerry_s5_oil.asset) | Absent | one vessel / 5 | 0.8 / 5.22 | No authored decay; physical fuel prop only |
| `fuelstore.oil_jug_s1` | [jug_s1_oil](../../Assets/_Project/Data/FuelContainers/jug_s1_oil.asset) | Absent | one vessel / 1 | 0.07 / 0.96 | No authored decay; physical fuel prop only |
| `fuelstore.oil_jug_s10` | [jug_s10_oil](../../Assets/_Project/Data/FuelContainers/jug_s10_oil.asset) | Absent | one vessel / 10 | 0.55 / 9.4 | No authored decay; physical fuel prop only |
| `fuelstore.oil_jug_s4` | [jug_s4_oil](../../Assets/_Project/Data/FuelContainers/jug_s4_oil.asset) | Absent | one vessel / 4 | 0.24 / 3.78 | No authored decay; physical fuel prop only |
| `fuelstore.oil_nozzle_sauto` | [nozzle_sAuto_oil](../../Assets/_Project/Data/FuelContainers/nozzle_sAuto_oil.asset) | Absent | one vessel / 0 | 1.4 / 1.4 | No authored decay; physical fuel prop only |
| `fuelstore.oil_nozzle_shigh` | [nozzle_sHigh_oil](../../Assets/_Project/Data/FuelContainers/nozzle_sHigh_oil.asset) | Absent | one vessel / 0 | 2.6 / 2.6 | No authored decay; physical fuel prop only |

Six `GoodsContainerDef` assets are **research profiles**, not accepted retail goods. They have no prices or sellers, no decay model, and describe empty durable containers. Their prototype mass and capacity must not silently become shelf balance. Listed here for completeness, without claiming that a container model is a sale item.

| Id | File | Price | Unit | Empty mass kg | Perishable? / use |
| --- | --- | --- | --- | --- | --- |
| `container.research_fish_tub_460` | [FishTub460Research](../../Assets/_Project/Data/GoodsModelResearch/FishTub460Research.asset) | Absent | one container | 41 | No; research capacity/handling only |
| `container.research_fish_tub_660` | [FishTub660Research](../../Assets/_Project/Data/GoodsModelResearch/FishTub660Research.asset) | Absent | one container | 52 | No; research capacity/handling only |
| `container.research_hand_pail` | [HandPailResearch](../../Assets/_Project/Data/GoodsModelResearch/HandPailResearch.asset) | Absent | one container | 1 | No; research capacity/handling only |
| `container.research_low_pan` | [LowPanResearch](../../Assets/_Project/Data/GoodsModelResearch/LowPanResearch.asset) | Absent | one container | 1 | No; research capacity/handling only |
| `container.research_portable_tote_65` | [PortableTote65Research](../../Assets/_Project/Data/GoodsModelResearch/PortableTote65Research.asset) | Absent | one container | 11 | No; research capacity/handling only |
| `container.research_portable_tote_70` | [PortableTote70Research](../../Assets/_Project/Data/GoodsModelResearch/PortableTote70Research.asset) | Absent | one container | 12 | No; research capacity/handling only |

## What the shelves slice still lacks

This distinguishes a missing **retail** Def from an existing bulk or physical Def. The plan's slice 2 explicitly names gas, ice, bait and groceries, and its wider import loop includes the remaining goods below. It does not yet decide every retail product. A shelf icon or crate prop is not a product identity.

| Requested thing | Exists now | Missing for a sale from the island's shelves |
| --- | --- | --- |
| Gasoline by jerry can | `islandgood.gasoline`, physical `fuelstore.gas_jerry_s20`, carrier capacity; mainland `FuelStationDef` litre prices | No gasoline `SupplyDef`/catalog listing, retail price, pack size, can/contents ownership or transfer rule; no link from retail can to ledger litres. Mainland pump prices are not an island can price |
| Propane bottle | `islandgood.propane` with cylinder text | No propane retail Def, price, cylinder inventory definition, fill/empty/exchange rule, or kg-per-pack link. The “20 lb cylinder” is text only |
| Stove oil | `islandgood.stove_oil`; generic oil vessel variants | No stove-oil retail Def, price or mapping to a vessel/grade; no drum-to-retail-unit conversion |
| Ice bags | `supply.ice`, `islandgood.ice`, ice-chest shelf line, cold-storage durations | Not a missing retail Def. Missing kg/bag, pack volume, bulk-to-shelf link and any shelf melt/expiry policy |
| Bait lots | Seven `BaitDef`s; capelin shelf line; `islandgood.bait` | Not missing bait identity. Missing kg per bait unit/lot and allocation of the bulk top-up across species; no shelf spoilage rate |
| Groceries | `islandgood.groceries`; bread, oatcakes and tinned beans already priced and edible | No generic grocery-box sale item. The bulk description's flour, sugar, tea and dairy have no retail/food Defs. Existing foods need weights, bulk allocation and new shop lines; neither cooked recipes nor produce automatically become purchased groceries |
| Hardware, lumber and gear | `islandgood.hardware`; named fishing gear offers | No retail hardware/lumber bundle Defs or price; no mapping from bulk hardware kg to existing gear. The plan has not specified nails, rope lengths or timber sizes, so do not invent them |
| Medicine and miscellaneous | `islandgood.medicine_misc` | No retail medicine Def, price, pack or use effect. The plan names a category, not a medicine product |
| Mail/parcels | `islandgood.mail_parcels` | No parcel item/ownership/delivery definition; this is postal cargo, not an approved commodity to sell |
| Island fish and produce | All 19 fish and four produce Defs; crop/recipe/bulk definitions above | No retail purchase listing, pack conversion or store intake-to-shelf path. Not missing species/produce identities. Island-own goods are outside the current ledger's imports |
| Bulk diesel, heating oil and bait by sea (later slice) | Plan language; fuel vessel grades and bait assets | No `IslandGoodDef` assets with `Source=BySea` or `Island` at this snapshot; no sea-freight delivery into this ledger. Do not add these to slice 2 by implication |
| The shelf bridge itself | Three `ShopStockLine`s; nine bulk ledger goods | No authored listing→island-good relation, units/pack, back-room/shelf capacity split, fractional remainder rule, per-line NPC demand or price-bearing retail Def for the missing categories |

The plan says shelves fill **the day after** the run, while `IslandStoreLedger.Step` credits delivery **that morning**, then subtracts the entire day's use. `LevelAtDawn` is before delivery and `LevelAtDusk` after use; there is no delivery-hour/intermediate shelf level. Slice 2 needs an explicit timing decision, not an off-by-one guess. Villager purchases are currently one aggregate daily subtraction, not transactions at the three customer stations. Any later schedule of purchases must partition that daily use rather than subtract it twice.

The existing shelf floors (one capelin lot and one ice bag) also conflict with literal starvation: a nonzero floor can keep stock available when the bulk ledger is empty. `ShopRestock.Settle` clamps to the floor even without a delivery, and `TryTakeOne` immediately restores the floor after a take; neither is a physical receipt. A ledger-backed shelf needs one source of replenishment and a stated rounding/floor policy. The current `ShopDef.BuildSpec` permits only `GearOffer`, `BaitDef`, `SupplyDef`, and requires the bait pack size to match `LotSize`; it cannot accept an `IslandGoodDef`, recipe, fuel prop, instrument or licence as a shelf listing.

## Prices in C# rather than authored content

Search scope: all production and editor `.cs` under `Assets/_Project/Code`, monetary names (`Price`, `PricePerLitre`, `PricePerKg`, `BaseValue`, purchase/repair costs, fees, wages/rewards) and wallet debit/credit call sites. Synthetic test fixtures, performance/energy costs, enum values, zero UI placeholders, clamp bounds and neutral multipliers are not independent game prices. No fixed positive transaction price bypassing a Def/config was found in the inspected production paths. The literal monetary seeds below **are** C# values; keeping them separate avoids claiming that a field default is a hard-coded runtime checkout.

| File and line | Literal(s), coin | Role / implication |
| --- | --- | --- |
| [GreyboxBuilder.cs:617](../../Assets/_Project/Code/App/Editor/GreyboxBuilder.cs#L617) | Punt `Price=1800` | Editor `LoadOrCreate` seed; duplicates authored offer |
| [NineMileCreekBuilder.cs:424](../../Assets/_Project/Code/App/Editor/NineMileCreekBuilder.cs#L424) | Punt `Price=1800` | Editor missing-asset seed |
| [NineMileCreekBuilder.cs:432](../../Assets/_Project/Code/App/Editor/NineMileCreekBuilder.cs#L432) | Cod licence `Price=120` | Editor missing-asset seed |
| [NineMileCreekBuilder.cs:438](../../Assets/_Project/Code/App/Editor/NineMileCreekBuilder.cs#L438) | Rod `Price=60` | Editor missing-asset seed |
| [NineMileCreekBuilder.cs:444](../../Assets/_Project/Code/App/Editor/NineMileCreekBuilder.cs#L444) | Dory `Price=400`, `RepairCost=300` | Editor missing-asset seed |
| [NineMileCreekBuilder.cs:469](../../Assets/_Project/Code/App/Editor/NineMileCreekBuilder.cs#L469) | Outboard hull `Price=900`, `RepairCost=0` | Editor missing-asset seed; zero means no repair charge |
| [NineMileCreekBuilder.cs:480](../../Assets/_Project/Code/App/Editor/NineMileCreekBuilder.cs#L480) | Lobster pot `Price=120` | Editor missing-asset seed |
| [NineMileCreekBuilder.cs:486](../../Assets/_Project/Code/App/Editor/NineMileCreekBuilder.cs#L486) | Crab pot `Price=60` | Editor missing-asset seed |
| [BaitDef.cs:34](../../Assets/_Project/Code/Economy/BaitDef.cs#L34) | `Price=3` | Authoring default; actual unit price comes from asset |
| [SupplyDef.cs:39](../../Assets/_Project/Code/Economy/SupplyDef.cs#L39) | `Price=6` | Authoring default |
| [GearOffer.cs:32](../../Assets/_Project/Code/Economy/GearOffer.cs#L32) | `Price=60` | Authoring default |
| [PotOffer.cs:38](../../Assets/_Project/Code/Economy/PotOffer.cs#L38) | `Price=120` | Authoring default |
| [InstrumentOffer.cs:39](../../Assets/_Project/Code/Economy/InstrumentOffer.cs#L39) | `Price=140` | Authoring default |
| [LicenseDef.cs:35](../../Assets/_Project/Code/Economy/LicenseDef.cs#L35) | `Price=120` | Authoring default; the shipped clam licence is 15, not 120 |
| [HomeDef.cs:68](../../Assets/_Project/Code/Economy/HomeDef.cs#L68) | `Price=900` | Authoring default; property is outside shelves |
| [ShipwrightOffer.cs:22](../../Assets/_Project/Code/Economy/ShipwrightOffer.cs#L22), [33](../../Assets/_Project/Code/Economy/ShipwrightOffer.cs#L33) | `Price=1800`, `RepairCost=300` | Authoring defaults; hulls are outside shelves |
| [VehicleOffer.cs:53](../../Assets/_Project/Code/Economy/VehicleOffer.cs#L53), [63](../../Assets/_Project/Code/Economy/VehicleOffer.cs#L63) | `Price=1000`, `RepairCost=0` | Authoring defaults; vehicles are outside shelves |
| [FishSpeciesDef.cs:169](../../Assets/_Project/Code/Fishing/FishSpeciesDef.cs#L169) | `BaseValue=12` | Authoring default; all shipped fish values listed above |
| [ProduceDef.cs:34](../../Assets/_Project/Code/Core/Farming/ProduceDef.cs#L34) | `BaseValue=1` | Authoring default; all shipped produce values listed above |

For completeness, the explicit zero fallbacks are [FuelStationDef.cs:92,105](../../Assets/_Project/Code/Economy/FuelStationDef.cs#L92) and [FuelPump.cs:84](../../Assets/_Project/Code/Economy/FuelPump.cs#L84) (unavailable grade, not free fuel), and [FrontedFeeGrant.cs:71](../../Assets/_Project/Code/Economy/FrontedFeeGrant.cs#L71) (no config means no grant). `GameConfig.FrontedLicenceFee=20` at [GameConfig.cs:703](../../Assets/_Project/Code/Core/Config/GameConfig.cs#L703) is an allowed config default and a grant, not the licence price. No invented conversion should use it as the clam licence's cost.

The live purchase paths read `GearOffer.Price` (`GearShop.cs:90`), `SupplyDef.Price` (`SupplyShop.cs:97`), `LicenseDef.Price` (`LicenseVendor.cs:96`), `InstrumentOffer.Price` (`InstrumentShop.cs:114`), `PotOffer.Price` (`PotShop.cs:96`) and bait price × lot (`BaitShop.cs:62`). Pump pricing reads `FuelGradeOffer.PricePerLitre`; plant intake uses grade prices in `FishGradeDef`; market sales use the fish base value and config demand/price-level settings. None supplies the missing shelf product prices.

## The store as authored today

| Piece | Evidence | What exists / what it does not do |
| --- | --- | --- |
| Seller | [leblancs.asset](../../Assets/_Project/Data/Resources/Sellers/leblancs.asset) | `seller.leblancs`, keeper `npc.marguerite_leblanc`, `trade.general_store`; `SellerDef` has no opening-hours fields |
| Shop | [StPetersGeneral.asset](../../Assets/_Project/Data/Shops/StPetersGeneral.asset) | `shop.st_peters_general`, `premises.st_peters_general`; logical shelves only; credit disabled |
| Keeper station | [StPeters.unity:88686](../../Assets/_Project/Scenes/StPeters.unity#L88686) | `station.st_peters.store_counter`, position (2.032163, 27.905405), heading 179.17587° |
| Customer A | [StPeters.unity:88692](../../Assets/_Project/Scenes/StPeters.unity#L88692) | `station.st_peters.store_customer_a`, (0.9467809, 24.967386), heading 13.358199°; post-office worker departs for it at 12:30, leaves at 14:00 |
| Customer B | [StPeters.unity:88698](../../Assets/_Project/Scenes/StPeters.unity#L88698) | `station.st_peters.store_customer_b`, (1.8450282, 24.911245), heading 2.3005734°; runner departs for it at 12:00 (13:00 Wednesday), leaves at 14:30 |
| Customer C | [StPeters.unity:88704](../../Assets/_Project/Scenes/StPeters.unity#L88704) | `station.st_peters.store_customer_c`, (2.7432756, 24.855104), heading −8.522487°; teacher departs for it at 12:00, leaves at 13:15 |
| Station placement | [StPetersRoutines.cs:487](../../Assets/_Project/Code/App/Editor/StPetersRoutines.cs#L487) | All four are outdoor (`Interior` null), on `lane_store`; keeper behind the counter, three customers on the green side. No station action buys anything |
| Keeper's day | [MargueriteLeBlancRoutine.asset:19](../../Assets/_Project/Data/Routines/MargueriteLeBlancRoutine.asset#L19) | 07:00 counter; 13:00 post-office errand; 14:30 counter; 19:00 green; 21:00 upstairs. Walk speed 1.1 m/s, schedule jitter 5 minutes. These are departure times, not hard shop opening/closing times |
| Customer routines | [RoseMacIsaacRoutine](../../Assets/_Project/Data/Routines/RoseMacIsaacRoutine.asset), [JuniorPoirierRoutine](../../Assets/_Project/Data/Routines/JuniorPoirierRoutine.asset), [EileenDoironRoutine](../../Assets/_Project/Data/Routines/EileenDoironRoutine.asset) | Visits only; Wednesday crossing remains a routine override, independent of the two-day ledger cadence |
| Actual counter components | [StPeters.unity:69652](../../Assets/_Project/Scenes/StPeters.unity#L69652), vendor references at 69717–69955; [builder:2039](../../Assets/_Project/Code/App/Editor/StPetersBuilder.cs#L2039) | Nine vendor components: rod, capelin, ice, bread, oatcakes, beans, soap, depth sounder, clam licence; all use the store seller. Also market/buyer/sell point. The older “five vendors” description predates food and soap |
| Interaction entry | [MargueriteFirst.asset](../../Assets/_Project/Data/NPCs/Dialogue/MargueriteFirst.asset); [builder:2103](../../Assets/_Project/Code/App/Editor/StPetersBuilder.cs#L2103) | Keeper dialogue offers browse and sell. The book loads `Resources/Catalog`, filters `Catalog.IsStockedBy(seller)`, and lists authored wares independently of component count. Old P/B walk-up keys are retired |
| Selling catch | [builder:2088](../../Assets/_Project/Code/App/Editor/StPetersBuilder.cs#L2088) | `MarketId.StPetersStore`, `FishBuyer`, `WharfSellPoint`, hand-carried bucket and persistent wallet; does not add purchased fish to shelves |
| Opening hours | `SellerDef`, `ShopDef`, `ShopStockSpec`, purchase services and routines | No shop-hours Def/field, keeper-presence predicate or stock-hours gate. Being physically near enough to talk is not an economic opening-hours policy. The supply route's 06:00–20:00 working window is **runner travel data**, not shop hours |

| Shelf line | Listing / fixture | Max objects | Units per object | Delivery mask / amount | Floor |
| --- | --- | --- | --- | --- | --- |
| `line.rod` | `gear.rod` / `fixture.rod_rack` | 4 | 1 | 9 = Monday + Thursday / 4 | 0 |
| `line.capelin` | `bait.capelin` / `fixture.bait_shelf` | 20 | 10 bait units | Monday + Thursday / 20 | 1 |
| `line.ice` | `supply.ice` / `fixture.ice_chest` | 20 | 1 bag | Monday + Thursday / 20 | 1 |

`ShopStockService` and its in-memory store can lazily catch up missed weekday deliveries and record take/return counts. The code search finds no production constructor call; no counter purchase consumes this stock, no NPC visit consumes a line, and no ledger fills it. The keeper's existing ask-about-store dialogue also says a boat arrives Thursdays, whereas the shelf data says Monday/Thursday and the new ledger says every second tide-permitted day. This is a dialogue/data mismatch for the next slice to resolve. Listing a good in the catalog is therefore not evidence of finite shelves. A missing stock record means a full shelf, not a ledger-derived starting count.

## What PR 1456 already recomputes

| Input / operation | Existing result and limit |
| --- | --- |
| [StPetersIslandLedger.asset](../../Assets/_Project/Data/IslandLedgers/StPetersIslandLedger.asset) | `islandledger.st_peters`, `region.st_peters`, nine imported goods, `supplyrole.quad_runner`, `LookbackDays=28` |
| [QuadRunner.asset](../../Assets/_Project/Data/Resources/IslandSupply/Roles/QuadRunner.asset), [IslandSupplyConfig.asset](../../Assets/_Project/Data/Resources/IslandSupply/IslandSupplyConfig.asset) | Cadence 2 days, front/rear racks plus trailer = 385 kg / 420 L; buffer 4 days, daylight travel window 06:00–20:00 |
| [IslandRunCalendar.cs:38](../../Assets/_Project/Code/Economy/Island/IslandRunCalendar.cs#L38) `DaylightRunDays` | Caller supplies `waterAt(seconds)`; crossing maths checks a dry-bar round trip within a low-water/daylight window. Precomputes a finite requested day range; outside it is impossible. Caller must cover the whole replay window |
| [IslandRunCalendar.cs:21](../../Assets/_Project/Code/Economy/Island/IslandRunCalendar.cs#L21) `RunsOn` | Schedule anchored at day zero. First possible day since the cadence boundary gets the run, once; if none is possible before the next boundary that run is lost, not accumulated as backlog |
| [IslandStoreLedger.cs:69](../../Assets/_Project/Code/Economy/Island/IslandStoreLedger.cs#L69) | Keeps only non-null `MainlandOverBar` goods with positive daily use; derives carried kg/L per unit from daily data |
| `Target` / `DailyUse` at lines 121–128 | Target = buffer days × daily use × (1 + player allowance). Optional `winter(day)` applies `WinterMultiplier`; absent delegate means never winter. Player allowance raises target, not actual daily consumption |
| `Settle` at line 151 | Starts full on `max(0, day − LookbackDays)`, steps through requested day **inclusive** (29 steps for a 28-day lookback). Negative requested day clamps to zero. Answers do not depend on call order |
| `Step` at line 173 | Records dawn; on run day tops toward target, scaling all requested deliveries by the same limiting mass/volume factor; consumes the day's island use; clamps empty at zero and records unmet demand |
| Per-good and aggregate reads | Dawn/dusk units, delivered units, shortage units; aggregate delivered/shortfall kg and L; run-today flag. No coin, wages, retail purchases, individual villagers, physical loading, shelf packs, expiry, hour-of-delivery or saved inventory |
| Seed/time contract | No world-seed argument or hidden randomness in the ledger itself. Its result is pure in day + fixed data + supplied run/winter functions. A seed/time simulation follows only if those delegates are deterministic. App composition must supply tide/calendar/season; there is no production constructor or `FromDef` call today |
| Replay convergence | A complete top-up resets a good exactly to target, erasing earlier history. Finite lookback is **not** a general proof for arbitrary outages, capacities, future demand, winter boundaries or player interventions. A partially filled room need not forget its initial state |

The existing tests are evidence of the intended contracts, not results of this audit run. `IslandStoreLedgerTests` covers calendar make-up/missed runs, filtering, capacities, use, starvation and call-order determinism. `IslandLedgerShippedTests` has three cases: content wiring, real-tide run count, and lookback doubling. Its doubling case uses the shipped tide and compares days 56–111 for the current 28-day lunar month, per-good dusk and aggregate shortage mass to `1e-9`; it does **not** pass a winter delegate, vary all seeds, prove all-time convergence, or test player purchases. The five-day starvation proof is synthetic. No shortage date or guarantee should be inferred beyond those test inputs.

## What needs remembered state

The calendar can reconstruct only events whose causes are deterministic and whose starting conditions are known. A player's choice cannot be recovered from `(worldSeed, gameTime)`. “Session only until slice 8” is viable for a prototype but loses that deviation on reload; the existing player-owned supply balance may survive while the shop refills.

| Plan state / event | Recompute or save? | Reason / boundary |
| --- | --- | --- |
| NPC positions, nominal run dates, aggregate background consumption | Recompute | Given fixed routines, tide, season, data and seed; no player interruption |
| Baseline back room | Recompute with proven replay horizon | Current bounded model above; not a general event-history simulation |
| Shelf background consumption and refill | Can recompute after missing rules are authored | Pack conversion, restock timing, allocation and consumption cadence must be fixed first; no new save needed for a wholly background baseline |
| Player purchases, returns, stolen/damaged stock, catch sold into store | Save a deviation or event history to persist | Same seed/time can contain different actions. Include which good/line, quantity and settlement point; no schema is approved here |
| A player carrying an unpaid item, unloading only half a delivery, moving a crate | Save physical ownership/location/reservation if it must persist | Time alone cannot reconstruct hands, containers or partial work. Physical movement must remain real work; a ledger top-up is not permission to teleport goods |
| Shelf `ShopStockRecord` | Stateful; memory-only implementation now | Count and `SettledThroughDay` after take/return are not derivable from the calendar. Current sparse record disappears at max stock; this is a proposed seam, not SaveData integration |
| Player-owned money, gear, supply/bait counts, boats and existing container state | Existing save contracts | Reuse their actual stores; their presence does not persist the store-side stock deduction |
| Market glut caused by player sales | Needs persistence | `Market` supply is mutable and currently unsaved. Daily decay alone cannot infer the input sales |
| Pure NPC fish catch and scheduled landings | Can recompute | Seed/id/day plan and fixed route; changed or intercepted deliveries become deviations |
| Stage/plant inventories and NPC market contribution | Conditional recomputation | Needs a known initial state and replay of deterministic receipts/processing/outlets; a claimed daily flush or decaying tail is not proof that arbitrary earlier history disappears |
| Household/stage money, wages, supplier bills | Can replay from a fixed start; bounded reset not established | Persistent balances accumulate. They require either exact deterministic accounting from a known origin or saved balances/events/checkpoints; with player trade they definitely need remembered deviations |
| Player-altered routes, staffing, business ownership or demand | Save changed inputs | Later phases; do not silently widen slice 2 |
| Player draw “clears after the next full run” | Only if that run truly erases the deviation | Capacity limits or shortages may prevent a full top-up. Compare actual baseline and depleted histories before dropping a record; crossing a scheduled date is insufficient |

The plan proposes `EconomyLedgerRows` and refers to save v26. That is a proposal for later approval, not an implemented schema or a reserved next version. This audit changes no save format.

## Verification and next-use boundaries

Existing coverage was searched before considering new validation. Relevant homes are [IslandStoreLedgerTests](../../Assets/Tests/EditMode/Economy/IslandStoreLedgerTests.cs), [IslandLedgerShippedTests](../../Assets/Tests/EditMode/IslandSupply/IslandLedgerShippedTests.cs), [IslandSupplyContentValidationTests](../../Assets/Tests/EditMode/IslandSupply/IslandSupplyContentValidationTests.cs), [ShopStockContentTests](../../Assets/Tests/EditMode/Economy/ShopStockContentTests.cs), [ShopStockRulesTests](../../Assets/Tests/EditMode/Economy/ShopStockRulesTests.cs), [IslandGeneralStoreTests](../../Assets/Tests/EditMode/Economy/IslandGeneralStoreTests.cs), [FoodContentValidationTests](../../Assets/Tests/EditMode/Food/FoodContentValidationTests.cs), [RecipeContentValidationTests](../../Assets/Tests/EditMode/Food/RecipeContentValidationTests.cs), [FarmContentValidationTests](../../Assets/Tests/EditMode/Farming/FarmContentValidationTests.cs), [ContentValidationTests](../../Assets/Tests/EditMode/ContentValidationTests.cs), and [StPetersStoreCounterTests](../../Assets/Tests/EditMode/StPetersStoreCounterTests.cs). Future work should extend those contracts, not add a competing definition of stock or identity.

This change adds only this document. Verification is static: tracked asset identities and scalar fields, catalog seller tags, targeted committed scene references, code call sites, table/link/count checks and whitespace checks. No Unity editor, EditMode/PlayMode tests, gameplay session, build, performance run or visual review was run. Existing tests were read, not reported as passing. Visual shelf/transport work and save implementation remain outside this audit.
