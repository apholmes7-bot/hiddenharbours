# Offers by seller

The island store offers basic fishing gear, bait, food, ice, soap and the clam licence. On the pinned base its depth sounder is the one existing boat-hardware exception. The separate counter relocation in #1532 moves it to the Nine Mile Creek boat/gear wholesaler. The census guard accepts those two exact single-seller states: before relocation it requires precisely the sounder exception, and afterward it requires no island hardware. It rejects a missing sounder seller, simultaneous sellers and every unrelated replacement. The damaged dory and cod licence are still sold in Nine Mile Creek on this base; the first-week island-only ruling is not yet reflected in those offers.

Census of base `541276cbc9308ba60ded8b905308366306438570`: 67 catalog assets, of which 66 name sellers and one is the free starting headlamp identity. Multiple sellers produce multiple rows. Prices are coin. Listed means the catalog flag is on; unlisted offers are plans, not proof of a working purchase. Counter names below were matched to seller components in the committed regional scenes, with the wiring declarations as a cross-check. No scene was loaded or changed.

Stock means **seller inventory**, not how much the player can own. `No` means no depletion binding, even for an owned stack or a one-time licence/hull. The live island shelves count five listings on three lines; the three foods share one grocery line on this base. The dormant `ShopDef` separately describes rod/bait/ice counts and is not evidence that the rod runs out at the live counter.

The new `CatalogListing.BoatHardware` flag defaults false and tags the ten helm instruments and the fitted dory outboard, without changing price, listing or purchase behavior. The existing Gear section also contains bait, licences and hand tools, so it cannot identify hardware. The portable rod rack remains general gear (its Def explicitly allows a shed or doorway). `SellerDef.RegionId` records existing/planned geography, with an empty default; neither field is saved player state.

## St Peters Island

| Item / source Def | Seller / counter | Price | Availability | Stock counted |
|---|---|---|---|---|
| [home.ginny_lot_camper](../../Assets/_Project/Data/Homes/GinnyLotCamper.asset) | deed till `home.ginny_lot_camper` / property door or sale sign; keeper unassigned | 900 | Purchasable | No shelf; unique deed |
| [home.st_peters_red_saltbox](../../Assets/_Project/Data/Homes/StPetersRedSaltbox.asset) | deed till `home.st_peters_red_saltbox` / property door or sale sign; keeper unassigned | 6000 | Purchasable | No shelf; unique deed |
| [bait.capelin](../../Assets/_Project/Data/Resources/Catalog/Bait/Capelin.asset) | `seller.leblancs` / `GeneralStoreCounter` | 2 each; 10 for 20 | Listed | Yes: bait line (20 initially) |
| [bait.fish_scrap](../../Assets/_Project/Data/Resources/Catalog/Bait/FishScrap.asset) | `seller.leblancs` / `GeneralStoreCounter` | 1 each; 10 for 10 | Unlisted | No |
| [bait.herring](../../Assets/_Project/Data/Resources/Catalog/Bait/Herring.asset) | `seller.leblancs` / `GeneralStoreCounter` | 3 each; 10 for 30 | Unlisted | No |
| [bait.mackerel](../../Assets/_Project/Data/Resources/Catalog/Bait/Mackerel.asset) | `seller.leblancs` / `GeneralStoreCounter` | 4 each; 10 for 40 | Unlisted | No |
| [bait.sea_worm](../../Assets/_Project/Data/Resources/Catalog/Bait/SeaWorm.asset) | `seller.leblancs` / `GeneralStoreCounter` | 4 each; 10 for 40 | Unlisted | No |
| [bait.shucked_clam](../../Assets/_Project/Data/Resources/Catalog/Bait/ShuckedClam.asset) | `seller.leblancs` / `GeneralStoreCounter` | 3 each; 10 for 30 | Unlisted | No |
| [bait.squid_strip](../../Assets/_Project/Data/Resources/Catalog/Bait/SquidStrip.asset) | `seller.leblancs` / `GeneralStoreCounter` | 6 each; 10 for 60 | Unlisted | No |
| [gear.bucket](../../Assets/_Project/Data/Resources/Catalog/Gear/ClamBucket.asset) | `seller.leblancs` / `GeneralStoreCounter` | 15 | Unlisted | No |
| [gear.mussel_sack](../../Assets/_Project/Data/Resources/Catalog/Gear/MusselSack.asset) | `seller.leblancs` / `GeneralStoreCounter` | 10 | Unlisted | No |
| [gear.oyster_tongs](../../Assets/_Project/Data/Resources/Catalog/Gear/OysterTongs.asset) | `seller.leblancs` / `GeneralStoreCounter` | 45 | Unlisted | No |
| [gear.rod](../../Assets/_Project/Data/Resources/Catalog/Gear/Rod.asset) | `seller.leblancs` / `GeneralStoreCounter` | 60 | Listed | No |
| [gear.shellfish_basket](../../Assets/_Project/Data/Resources/Catalog/Gear/ShellfishBasket.asset) | `seller.leblancs` / `GeneralStoreCounter` | 20 | Unlisted | No |
| [gear.shellfish_rake](../../Assets/_Project/Data/Resources/Catalog/Gear/ShellfishRake.asset) | `seller.leblancs` / `GeneralStoreCounter` | 30 | Unlisted | No |
| [gear.shovel](../../Assets/_Project/Data/Resources/Catalog/Gear/Shovel.asset) | `seller.leblancs` / `GeneralStoreCounter` | 25 | Unlisted | No |
| [instrument.depth_sounder](../../Assets/_Project/Data/Resources/Catalog/Instruments/DepthSounderOffer.asset) | `seller.leblancs` / `GeneralStoreCounter` | 140 | Listed on pinned base; #1532 moves this same offer to `seller.nmc_boat_gear_wholesaler` / `NmcBoatGearWholesalerCounter` at the same price | No |
| [license.clam](../../Assets/_Project/Data/Resources/Catalog/Licenses/ClamLicense.asset) | `seller.leblancs` / `GeneralStoreCounter` | 15 | Listed | No |
| [supply.bread](../../Assets/_Project/Data/Resources/Catalog/Supplies/Bread.asset) | `seller.leblancs` / `GeneralStoreCounter` | 4 | Listed | Yes: shared grocery line (18 initially) |
| [supply.ice](../../Assets/_Project/Data/Resources/Catalog/Supplies/Ice.asset) | `seller.leblancs` / `GeneralStoreCounter` | 6 | Listed | Yes: ice line (20 initially) |
| [supply.oatcakes](../../Assets/_Project/Data/Resources/Catalog/Supplies/Oatcakes.asset) | `seller.leblancs` / `GeneralStoreCounter` | 2 | Listed | Yes: shared grocery line (18 initially) |
| [supply.soap_bar](../../Assets/_Project/Data/Resources/Catalog/Supplies/SoapBar.asset) | `seller.leblancs` / `GeneralStoreCounter` | 3 | Listed | No |
| [supply.tinned_beans](../../Assets/_Project/Data/Resources/Catalog/Supplies/TinnedBeans.asset) | `seller.leblancs` / `GeneralStoreCounter` | 3 | Listed | Yes: shared grocery line (18 initially) |

## Nine Mile Creek

| Item / source Def | Seller / counter | Price | Availability | Stock counted |
|---|---|---|---|---|
| [home.nmc_shore_lane_saltbox](../../Assets/_Project/Data/Homes/NmcShoreLaneSaltbox.asset) | deed till `home.nmc_shore_lane_saltbox` / property door or sale sign; keeper unassigned | 5000 | Purchasable | No shelf; unique deed |
| [home.nmc_south_shore_estate](../../Assets/_Project/Data/Homes/NmcSouthShoreEstate.asset) | deed till `home.nmc_south_shore_estate` / property door or sale sign; keeper unassigned | 18000 | Purchasable | No shelf; unique deed |
| [home.nmc_waterfront_house](../../Assets/_Project/Data/Homes/NmcWaterfrontHouse.asset) | deed till `home.nmc_waterfront_house` / property door or sale sign; keeper unassigned | 9000 | Purchasable | No shelf; unique deed |
| [boat.dory_outboard](../../Assets/_Project/Data/Resources/Catalog/Shipwright/DoryOutboardOffer.asset) | `seller.hectors_barrel` / `NmcWharfYardOutboardTrade` | 900 | Listed | No |
| [instrument.autopilot](../../Assets/_Project/Data/Resources/Catalog/Instruments/AutopilotOffer.asset) | `seller.nmc_boat_gear_wholesaler` / `NmcBoatGearWholesalerCounter` | 6000 | Unlisted | No |
| [instrument.compass_dome](../../Assets/_Project/Data/Resources/Catalog/Instruments/DomeCompassOffer.asset) | `seller.nmc_boat_gear_wholesaler` / `NmcBoatGearWholesalerCounter` | 70 | Unlisted | No |
| [instrument.compass_flush](../../Assets/_Project/Data/Resources/Catalog/Instruments/FlushCompassOffer.asset) | `seller.nmc_boat_gear_wholesaler` / `NmcBoatGearWholesalerCounter` | 140 | Unlisted | No |
| [instrument.fish_finder](../../Assets/_Project/Data/Resources/Catalog/Instruments/FishFinderOffer.asset) | `seller.nmc_boat_gear_wholesaler` / `NmcBoatGearWholesalerCounter` | 650 | Listed | No |
| [instrument.gps](../../Assets/_Project/Data/Resources/Catalog/Instruments/ChartplotterOffer.asset) | `seller.nmc_boat_gear_wholesaler` / `NmcBoatGearWholesalerCounter` | 1800 | Listed | No |
| [instrument.loran](../../Assets/_Project/Data/Resources/Catalog/Instruments/LoranOffer.asset) | `seller.nmc_boat_gear_wholesaler` / `NmcBoatGearWholesalerCounter` | 900 | Listed | No |
| [instrument.radar](../../Assets/_Project/Data/Resources/Catalog/Instruments/RadarOffer.asset) | `seller.nmc_boat_gear_wholesaler` / `NmcBoatGearWholesalerCounter` | 3600 | Unlisted | No |
| [instrument.wind_dial](../../Assets/_Project/Data/Resources/Catalog/Instruments/WindDialOffer.asset) | `seller.nmc_boat_gear_wholesaler` / `NmcBoatGearWholesalerCounter` | 140 | Unlisted | No |
| [instrument.wind_display](../../Assets/_Project/Data/Resources/Catalog/Instruments/WindDisplayOffer.asset) | `seller.nmc_boat_gear_wholesaler` / `NmcBoatGearWholesalerCounter` | 650 | Unlisted | No |
| [gear.rod](../../Assets/_Project/Data/Resources/Catalog/Gear/Rod.asset) | `seller.nmc_chandlery` / `GeneralStore` | 60 | Listed | No |
| [bait.fish_scrap](../../Assets/_Project/Data/Resources/Catalog/Bait/FishScrap.asset) | `seller.nmc_fishing_gear_wholesaler` / `NmcFishingGearWholesalerCounter` | 1 each; 10 for 10 | Unlisted | No |
| [bait.herring](../../Assets/_Project/Data/Resources/Catalog/Bait/Herring.asset) | `seller.nmc_fishing_gear_wholesaler` / `NmcFishingGearWholesalerCounter` | 3 each; 10 for 30 | Unlisted | No |
| [bait.mackerel](../../Assets/_Project/Data/Resources/Catalog/Bait/Mackerel.asset) | `seller.nmc_fishing_gear_wholesaler` / `NmcFishingGearWholesalerCounter` | 4 each; 10 for 40 | Unlisted | No |
| [bait.sea_worm](../../Assets/_Project/Data/Resources/Catalog/Bait/SeaWorm.asset) | `seller.nmc_fishing_gear_wholesaler` / `NmcFishingGearWholesalerCounter` | 4 each; 10 for 40 | Unlisted | No |
| [bait.shucked_clam](../../Assets/_Project/Data/Resources/Catalog/Bait/ShuckedClam.asset) | `seller.nmc_fishing_gear_wholesaler` / `NmcFishingGearWholesalerCounter` | 3 each; 10 for 30 | Unlisted | No |
| [bait.squid_strip](../../Assets/_Project/Data/Resources/Catalog/Bait/SquidStrip.asset) | `seller.nmc_fishing_gear_wholesaler` / `NmcFishingGearWholesalerCounter` | 6 each; 10 for 60 | Unlisted | No |
| [gear.bucket](../../Assets/_Project/Data/Resources/Catalog/Gear/ClamBucket.asset) | `seller.nmc_fishing_gear_wholesaler` / `NmcFishingGearWholesalerCounter` | 15 | Unlisted | No |
| [gear.mussel_sack](../../Assets/_Project/Data/Resources/Catalog/Gear/MusselSack.asset) | `seller.nmc_fishing_gear_wholesaler` / `NmcFishingGearWholesalerCounter` | 10 | Unlisted | No |
| [gear.oyster_tongs](../../Assets/_Project/Data/Resources/Catalog/Gear/OysterTongs.asset) | `seller.nmc_fishing_gear_wholesaler` / `NmcFishingGearWholesalerCounter` | 45 | Unlisted | No |
| [gear.rod_rack](../../Assets/_Project/Data/Resources/Catalog/Gear/RodRack.asset) | `seller.nmc_fishing_gear_wholesaler` / `NmcFishingGearWholesalerCounter` | 25 | Listed | No |
| [gear.shellfish_basket](../../Assets/_Project/Data/Resources/Catalog/Gear/ShellfishBasket.asset) | `seller.nmc_fishing_gear_wholesaler` / `NmcFishingGearWholesalerCounter` | 20 | Unlisted | No |
| [gear.shellfish_rake](../../Assets/_Project/Data/Resources/Catalog/Gear/ShellfishRake.asset) | `seller.nmc_fishing_gear_wholesaler` / `NmcFishingGearWholesalerCounter` | 30 | Unlisted | No |
| [gear.shovel](../../Assets/_Project/Data/Resources/Catalog/Gear/Shovel.asset) | `seller.nmc_fishing_gear_wholesaler` / `NmcFishingGearWholesalerCounter` | 25 | Unlisted | No |
| [license.cod](../../Assets/_Project/Data/Resources/Catalog/Licenses/CodLicense.asset) | `seller.nmc_harbourmaster` / `NmcWharfOfficeVendor` | 120 | Listed | No |
| [vehicle.aero_semi](../../Assets/_Project/Data/Resources/Catalog/Vehicles/YardAeroSemiOffer.asset) | `seller.nmc_industrial_yard` / `planned industrial yard; no committed counter` | 25000 | Unlisted | No |
| [vehicle.cabover_box](../../Assets/_Project/Data/Resources/Catalog/Vehicles/YardCaboverBoxUsedOffer.asset) | `seller.nmc_industrial_yard` / `planned industrial yard; no committed counter` | 5000 + 4000 repair | Unlisted | No |
| [vehicle.classic_semi](../../Assets/_Project/Data/Resources/Catalog/Vehicles/YardClassicSemiOffer.asset) | `seller.nmc_industrial_yard` / `planned industrial yard; no committed counter` | 25000 | Unlisted | No |
| [vehicle.conv_box](../../Assets/_Project/Data/Resources/Catalog/Vehicles/YardConvBoxOffer.asset) | `seller.nmc_industrial_yard` / `planned industrial yard; no committed counter` | 12000 | Unlisted | No |
| [vehicle.dually_3500](../../Assets/_Project/Data/Resources/Catalog/Vehicles/DealerDually3500Offer.asset) | `seller.nmc_vehicle_dealer` / `planned dealer; no committed counter` | 7500 | Unlisted | No |
| [vehicle.enduro_250](../../Assets/_Project/Data/Resources/Catalog/Vehicles/DealerEnduro250Offer.asset) | `seller.nmc_vehicle_dealer` / `planned dealer; no committed counter` | 900 | Unlisted | No |
| [vehicle.hightop_van](../../Assets/_Project/Data/Resources/Catalog/Vehicles/DealerHightopVanOffer.asset) | `seller.nmc_vehicle_dealer` / `planned dealer; no committed counter` | 4000 | Unlisted | No |
| [vehicle.modern_2500](../../Assets/_Project/Data/Resources/Catalog/Vehicles/DealerModern2500Offer.asset) | `seller.nmc_vehicle_dealer` / `planned dealer; no committed counter` | 5500 | Unlisted | No |
| [vehicle.modern_350](../../Assets/_Project/Data/Resources/Catalog/Vehicles/DealerModern350Offer.asset) | `seller.nmc_vehicle_dealer` / `planned dealer; no committed counter` | 3500 | Unlisted | No |
| [vehicle.modern_3500](../../Assets/_Project/Data/Resources/Catalog/Vehicles/DealerModern3500Offer.asset) | `seller.nmc_vehicle_dealer` / `planned dealer; no committed counter` | 7000 | Unlisted | No |
| [vehicle.otter_8x8](../../Assets/_Project/Data/Resources/Catalog/Vehicles/DealerOtter8x8Offer.asset) | `seller.nmc_vehicle_dealer` / `planned dealer; no committed counter` | 2500 | Unlisted | No |
| [vehicle.trike_200](../../Assets/_Project/Data/Resources/Catalog/Vehicles/DealerTrike200Offer.asset) | `seller.nmc_vehicle_dealer` / `planned dealer; no committed counter` | 800 | Unlisted | No |
| [vehicle.utility_quad](../../Assets/_Project/Data/Resources/Catalog/Vehicles/DealerUtilityQuadOffer.asset) | `seller.nmc_vehicle_dealer` / `planned dealer; no committed counter` | 1200 | Unlisted | No |
| [boat.cape_islander](../../Assets/_Project/Data/Resources/Catalog/Shipwright/CapeIslanderOffer.asset) | `seller.nmc_yard` / `NmcWharfYardCatalogue / NmcWharfYardDoryTrade` | 17400 | Unlisted | No |
| [boat.console_skiff](../../Assets/_Project/Data/Resources/Catalog/Shipwright/ConsoleSkiffOffer.asset) | `seller.nmc_yard` / `NmcWharfYardCatalogue / NmcWharfYardDoryTrade` | 9700 | Unlisted | No |
| [boat.dory](../../Assets/_Project/Data/Resources/Catalog/Shipwright/DamagedDoryOffer.asset) | `seller.nmc_yard` / `NmcWharfYardCatalogue / NmcWharfYardDoryTrade` | 400 + 300 repair | Listed | No |
| [boat.lobster_boat](../../Assets/_Project/Data/Resources/Catalog/Shipwright/LobsterBoatOffer.asset) | `seller.nmc_yard` / `NmcWharfYardCatalogue / NmcWharfYardDoryTrade` | 20200 | Unlisted | No |
| [boat.lobster_inshore_hardtop_northumberland](../../Assets/_Project/Data/Resources/Catalog/Shipwright/LobsterInshoreHardtopNorthumberlandOffer.asset) | `seller.nmc_yard` / `NmcWharfYardCatalogue / NmcWharfYardDoryTrade` | 7200 | Unlisted | No |
| [boat.lobster_inshore_open_northumberland](../../Assets/_Project/Data/Resources/Catalog/Shipwright/LobsterInshoreOpenNorthumberlandOffer.asset) | `seller.nmc_yard` / `NmcWharfYardCatalogue / NmcWharfYardDoryTrade` | 7200 | Unlisted | No |
| [boat.lobster_offshore_hardtop_northumberland](../../Assets/_Project/Data/Resources/Catalog/Shipwright/LobsterOffshoreHardtopNorthumberlandOffer.asset) | `seller.nmc_yard` / `NmcWharfYardCatalogue / NmcWharfYardDoryTrade` | 37200 | Unlisted | No |
| [boat.lobster_offshore_open_northumberland](../../Assets/_Project/Data/Resources/Catalog/Shipwright/LobsterOffshoreOpenNorthumberlandOffer.asset) | `seller.nmc_yard` / `NmcWharfYardCatalogue / NmcWharfYardDoryTrade` | 37200 | Unlisted | No |
| [boat.lobster_standard_hardtop_northumberland](../../Assets/_Project/Data/Resources/Catalog/Shipwright/LobsterStandardHardtopNorthumberlandOffer.asset) | `seller.nmc_yard` / `NmcWharfYardCatalogue / NmcWharfYardDoryTrade` | 20200 | Unlisted | No |
| [boat.lobster_standard_open_northumberland](../../Assets/_Project/Data/Resources/Catalog/Shipwright/LobsterStandardOpenNorthumberlandOffer.asset) | `seller.nmc_yard` / `NmcWharfYardCatalogue / NmcWharfYardDoryTrade` | 20200 | Unlisted | No |
| [boat.punt](../../Assets/_Project/Data/Resources/Catalog/Shipwright/PuntOffer.asset) | `seller.nmc_yard` / `NmcWharfYardCatalogue / NmcWharfYardDoryTrade` | 1800 | Listed | No |
| [boat.punt_upgraded](../../Assets/_Project/Data/Resources/Catalog/Shipwright/PuntUpgradedOffer.asset) | `seller.nmc_yard` / `NmcWharfYardCatalogue / NmcWharfYardDoryTrade` | 2300 | Unlisted | No |
| [boat.side_dragger](../../Assets/_Project/Data/Resources/Catalog/Shipwright/SideDraggerOffer.asset) | `seller.nmc_yard` / `NmcWharfYardCatalogue / NmcWharfYardDoryTrade` | 216000 | Unlisted | No |
| [boat.sport_skiff](../../Assets/_Project/Data/Resources/Catalog/Shipwright/SportSkiffOffer.asset) | `seller.nmc_yard` / `NmcWharfYardCatalogue / NmcWharfYardDoryTrade` | 8200 | Unlisted | No |
| [boat.sport_skiff_mk2](../../Assets/_Project/Data/Resources/Catalog/Shipwright/SportSkiffMk2Offer.asset) | `seller.nmc_yard` / `NmcWharfYardCatalogue / NmcWharfYardDoryTrade` | 9300 | Unlisted | No |
| [boat.sport_skiff_twin](../../Assets/_Project/Data/Resources/Catalog/Shipwright/SportSkiffTwinOffer.asset) | `seller.nmc_yard` / `NmcWharfYardCatalogue / NmcWharfYardDoryTrade` | 8400 | Unlisted | No |
| [boat.zodiac_frc](../../Assets/_Project/Data/Resources/Catalog/Shipwright/ZodiacFrcOffer.asset) | `seller.nmc_yard` / `NmcWharfYardCatalogue / NmcWharfYardDoryTrade` | 8200 | Unlisted | No |
| [boat.zodiac_hurricane](../../Assets/_Project/Data/Resources/Catalog/Shipwright/ZodiacHurricaneOffer.asset) | `seller.nmc_yard` / `NmcWharfYardCatalogue / NmcWharfYardDoryTrade` | 10300 | Unlisted | No |
| [trap.crab](../../Assets/_Project/Data/Resources/Catalog/Shipwright/CrabPotOffer.asset) | `seller.nmc_yard` / `NmcWharfYardCatalogue / NmcWharfYardDoryTrade` | 60 | Listed | No |
| [trap.lobster](../../Assets/_Project/Data/Resources/Catalog/Shipwright/LobsterPotOffer.asset) | `seller.nmc_yard` / `NmcWharfYardCatalogue / NmcWharfYardDoryTrade` | 120 | Listed | No |
| [diesel](../../Assets/_Project/Data/FuelStations/NmcWharfPumps.asset) | station till `station.nmc_wharf_pumps` / Wharf Pumps | 1.85 per litre | Available | No station stock count |
| [gas](../../Assets/_Project/Data/FuelStations/NmcWharfPumps.asset) | station till `station.nmc_wharf_pumps` / Wharf Pumps | 2.05 per litre | Available | No station stock count |
| [mixed](../../Assets/_Project/Data/FuelStations/NmcWharfPumps.asset) | station till `station.nmc_wharf_pumps` / Wharf Pumps | 2.95 per litre | Unavailable | No station stock count |
| [oil](../../Assets/_Project/Data/FuelStations/NmcWharfPumps.asset) | station till `station.nmc_wharf_pumps` / Wharf Pumps | 10.5 per litre | Unavailable | No station stock count |
| [diesel](../../Assets/_Project/Data/FuelStations/Route91Station.asset) | station till `station.route_91` / Route 91 Service Station | 1.4 per litre | Available | No station stock count |
| [gas](../../Assets/_Project/Data/FuelStations/Route91Station.asset) | station till `station.route_91` / Route 91 Service Station | 1.55 per litre | Available | No station stock count |
| [mixed](../../Assets/_Project/Data/FuelStations/Route91Station.asset) | station till `station.route_91` / Route 91 Service Station | 2.35 per litre | Available | No station stock count |
| [oil](../../Assets/_Project/Data/FuelStations/Route91Station.asset) | station till `station.route_91` / Route 91 Service Station | 8.5 per litre | Available | No station stock count |
| [stove_oil](../../Assets/_Project/Data/FuelStations/Route91Station.asset) | station till `station.route_91` / Route 91 Service Station | 1.2 per litre | Available | No station stock count |

## Cumberland

| Item / source Def | Seller / counter | Price | Availability | Stock counted |
|---|---|---|---|---|
| [home.cumberland_route_bungalow](../../Assets/_Project/Data/Homes/CumberlandRouteBungalow.asset) | deed till `home.cumberland_route_bungalow` / property door or sale sign; keeper unassigned | 4500 | Purchasable | No shelf; unique deed |
| [home.cumberland_two_storey](../../Assets/_Project/Data/Homes/CumberlandTwoStorey.asset) | deed till `home.cumberland_two_storey` / property door or sale sign; keeper unassigned | 7500 | Purchasable | No shelf; unique deed |

## Rice Point

| Item / source Def | Seller / counter | Price | Availability | Stock counted |
|---|---|---|---|---|
| [home.rice_point_point_house](../../Assets/_Project/Data/Homes/RicePointPointHouse.asset) | deed till `home.rice_point_point_house` / property door or sale sign; keeper unassigned | 8000 | Purchasable | No shelf; unique deed |

## Coverage and counting rules

- `seller.nmc_wharf_market` has no outgoing catalog offer. It buys catches at `NmcWharfMarketCounter`. All ten SellerDefs are accounted for above or here.
- Fuel and property tills identify themselves by station/home id, not by a SellerDef. The two fuel sites are Nine Mile Creek sites; they have no named keeper. These are explicit non-catalog transactions, covered by their existing content tests.
- `gear.headlamp` is an unlisted, zero-price label for starting gear and names no seller. `watch.harbour_digital` is owned from the start, price zero, no seller. Neither is a sale. Tackle, longline tubs, portable fuel containers and golf clubs have no offer type; see [the existing exemption audit](economy-item-sellers.md). IslandGoodDefs describe logistics, not retail prices: groceries, gasoline, stove oil, propane, hardware, ice, bait, medicine/misc and mail/parcels are not nine additional retail offers.
- No mending-material offer exists. The dory has a single repair charge of 300, not an authored shopping list. Boat instruments and the fitted outboard are hardware; complete hulls and portable fishing pots are separately identified products.

### Live counted items

| Line | Listings | Initial count / shelf maximum | Restock rule |
|---|---|---|---|
| `line.grocery_box` | bread, oatcakes, tinned beans | floor(18 daily units × 1.25 allowance × 4 buffer days / 5 per item) = 18 / 18 | Every two days if the route is possible; top up toward buffer, scaled by carrying capacity |
| `line.ice_bag` | ice | min(20, floor(20 × 1.25 × 4 / 5)) = 20 / 20 | Same route, own good balance |
| `line.bait_box` | capelin | min(20, floor(10 × 1.25 × 4 / 2.5)) = 20 / 20 | Same route, own good balance |

The gas-can display is also counted but has no counter listing: its initial count is min(4, floor(20 × 1.25 × 4 / 20)); it is not a purchasable gasoline offer. Initial stock is derived, not an explicit saved field. The island ledger starts full, subtracts resident use through opening hours and player purchases, and delivers only on permitted runs. The route buffer is 4 days and ledger lookback 28 days. The exact cadence and capacity live on the runner and carrier Defs.

The dormant `ShopDef` initializes rod/capelin/ice to 4/20/20, with Monday and Thursday deliveries of 4/20/20, capacities 4/20/20 and floors 0/1/1. Its positive floors make bait and ice inexhaustible in that model. This differs from the live island shelves; do not describe either model as proof of separate food stock.

### Guards and limits

`SellerContentTests` now checks every catalog offer, including unlisted plans, against real sellers and rejects duplicate (seller, actual item) pairs. Vehicle offer ids are normalized to vehicle ids; pot offers to trap ids. Its exact island-hardware set contains only the current sounder pair before #1532, and is empty after that relocation. The sounder must name exactly the island seller or exactly the mainland boat/gear wholesaler; every other seller set fails. All instrument offers and the fitted dory outboard must carry the tag. Cloned-offer tests exercise both relocation states, reject missing/duplicate/unrelated sounder sellers, reject an untagged outboard and reject an island outboard seller. Seller regions must resolve to RegionDefs.

`ShopStockContentTests` retains the existing ShopDef validation and adds an island-shelf check for a real seller/listing, derived positive starting count, positive shelf capacity and pack units, ledger membership and a positive runner cadence. No existing assertion was relaxed. No editor, compiler, scene playthrough or EditMode run was performed locally; CI must compile and execute these tests. No save-format change is needed for classification metadata; saving shelf purchases remains a separate feature.

The four unlisted mending-material supplies proposed in #1508 are absent from this pinned base. Their empty seller arrays would fail both this census and `EveryItemHasASellerTests`; they need explicit material sourcing before integration. Unlisted offers remain subject to the same real-seller rule.
