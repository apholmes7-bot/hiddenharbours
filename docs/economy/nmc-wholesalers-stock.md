# Nine Mile Creek wholesalers: stock and price proposals

Status: **design proposal for owner decisions, 2026-11-16**. This document changes no prices, sellers, purchase behaviour, buildings or assets. Two separate new businesses are required: a boat/gear wholesaler for electronics and boat equipment, and a fishing-gear wholesaler for fishing stock. The boatyard keeps hull sales, repairs and engine upgrades. Autopilot is an expensive optional purchase on anything without an outboard; the rowed dory has no autopilot.

## 1. Evidence and inventory

Baseline: main `32dfdecdf3c36167598b97401bc8cfda336583b9`. Draft changes below are evidence of proposed behaviour, not claims about main:

| Draft | Reviewed head | Relevant change |
|---|---|---|
| [#1269](https://github.com/apholmes7-bot/hiddenharbours/pull/1269) | `92a982eb7c34b1122c1ab24e6b2442743bf55666` | Loran joins the island general store at its existing placeholder price of ₲380. |
| [#1270](https://github.com/apholmes7-bot/hiddenharbours/pull/1270) | `f83bed33a26e03675c956df4ba3ce8eb12f5edd2` | Heading hold; per-hull `AutopilotOffered`, 35 eligible hulls; purchase and price absent. |
| [#1271](https://github.com/apholmes7-bot/hiddenharbours/pull/1271) | `3019d93dc202a8b735f671e96e611ef10805e09e` | 17 new offers and seller tags for 11 existing offers; new listings remain unlisted. |
| [#1276](https://github.com/apholmes7-bot/hiddenharbours/pull/1276) | `a851f0942a3a18403b158bc919df55e6b59707c0` | Both wholesaler buildings are missing; proposes possible sites requiring a census. |

**Counting rule:** 98 distinct catalogue/equipment identities: 46 hull ids (including the outboard-dory refit), 13 vehicles, 8 gear, 7 bait, 9 instrument ids, 1 proposed autopilot product, 2 traps, 2 licences, 4 supplies, 5 tackle and 1 longline tub. A capability Def or a second seller does not add another item. Property, fuel grades, starting/borrowed items and fixtures are recorded separately in §1.5–1.6 and excluded from these totals.

**Availability is two different questions.** An offer can have a price and seller but remain unlisted. A shelf also needs the appropriate vendor component at that seller's counter. Main has **42 priced catalogue offers, 30 with a named seller, 14 unique shelf items**. The combined drafts have **59 priced offers with named sellers, 15 shelf items**, and **39 identities sold nowhere because they have no seller offer**. Another **44** have offers but no working shelf/dealer path: **83/98 cannot yet be bought at a committed counter** under that combined snapshot. These counts describe catalogue/vendor evidence, not a new playtest. Main alone has 68 identities without a named seller offer and 84 without a working shelf path.

The source of committed shop arms is [StoreWiringPatch](../../Assets/_Project/Code/App/Editor/StoreWiringPatch.cs) lines 97–120, checked against the offer listing fields. Seller ids below omit the common `seller.` prefix. Their definitions are under [Resources/Sellers](../../Assets/_Project/Data/Resources/Sellers): `leblancs` = St Peters general store, `nmc_chandlery` = current NMC rod counter, `hectors_barrel` = current outboard counter, `nmc_yard` = boatyard. Neither new wholesaler has a SellerDef yet.

### 1.1 Boat electronics: four priced offers and six missing products

| Item / id | Def or evidence | Main price ₲ | Seller on main / state | Draft change |
|---|---|---:|---|---|
| Chartplotter · `instrument.gps` | [ChartplotterOffer.asset](../../Assets/_Project/Data/Resources/Catalog/Instruments/ChartplotterOffer.asset) | 650 | **none** / unlisted | #1271: `nmc_chandlery`, unlisted |
| Depth Sounder · `instrument.depth_sounder` | [DepthSounderOffer.asset](../../Assets/_Project/Data/Resources/Catalog/Instruments/DepthSounderOffer.asset) | 140 | `leblancs` / shelf | unchanged |
| Fish Finder · `instrument.fish_finder` | [FishFinderOffer.asset](../../Assets/_Project/Data/Resources/Catalog/Instruments/FishFinderOffer.asset) | 480 | **none** / unlisted | #1271: `leblancs`, unlisted |
| Loran-C Receiver · `instrument.loran` | [LoranOffer.asset](../../Assets/_Project/Data/Resources/Catalog/Instruments/LoranOffer.asset) | 380 | **none** / unlisted | #1269: `leblancs`, shelf |
| Dome compass · `instrument.compass_dome` | [equipment id](../../Assets/_Project/Code/Boats/BoatEquipment.cs); no offer Def | — | **none / sold nowhere** | No offer in reviewed drafts |
| Flush compass · `instrument.compass_flush` | [equipment id](../../Assets/_Project/Code/Boats/BoatEquipment.cs); no offer Def | — | **none / sold nowhere** | No offer in reviewed drafts |
| Radar · `instrument.radar` | [equipment id](../../Assets/_Project/Code/Boats/BoatEquipment.cs); no offer Def | — | **none / sold nowhere** | No offer in reviewed drafts |
| Wind dial · `instrument.wind_dial` | [equipment id](../../Assets/_Project/Code/Boats/BoatEquipment.cs); no offer Def | — | **none / sold nowhere** | No offer in reviewed drafts |
| Wind display · `instrument.wind_display` | [equipment id](../../Assets/_Project/Code/Boats/BoatEquipment.cs); no offer Def | — | **none / sold nowhere** | No offer in reviewed drafts |
| Autopilot · product id not authored | [AutopilotOffered (#1270)](https://github.com/apholmes7-bot/hiddenharbours/blob/f83bed33a26e03675c956df4ba3ce8eb12f5edd2/Assets/_Project/Code/Boats/BoatHullDef.cs) | — | **none / sold nowhere** | Eligibility and temporary outright fitment; no paid product |

The five reserved instrument ids already affect [BoatEquipment.EffectiveFit](../../Assets/_Project/Code/Boats/BoatEquipment.cs); this does not create an offer. A compass already supplied with a console or radar supplied with a trawler is included in that hull, not a separate sale. Heading is read through the compass/motion fit, not a tenth instrument offer. [ConsoleSkiffHelm.asset](../../Assets/_Project/Data/Boats/Helms/ConsoleSkiffHelm.asset) and [SportSkiffHelm.asset](../../Assets/_Project/Data/Boats/Helms/SportSkiffHelm.asset) include a dome compass; [SternTrawler.asset](../../Assets/_Project/Data/Boats/InstrumentFits/SternTrawler.asset) includes fish finder, radar, GPS and flush compass. The Loran tuning definition is navigation data, not another receiver for sale.

### 1.2 Fishing gear: 17 priced offers and six products without offers

All bait prices are **per unit**; the current default lot is ten, so counter payment is ten times the price. No reviewed bait asset overrides that lot size. Sources: [BaitDef.cs](../../Assets/_Project/Code/Economy/BaitDef.cs) and [LotPriceOf](../../Assets/_Project/Code/Economy/BaitShop.cs).

| Item / id | Offer Def | Main price ₲ | Seller on main / state | Draft change |
|---|---|---:|---|---|
| Capelin · `bait.capelin` | [Capelin.asset](../../Assets/_Project/Data/Resources/Catalog/Bait/Capelin.asset) | 2 | `leblancs` / shelf | unchanged |
| Fish Scrap · `bait.fish_scrap` | [FishScrap.asset](../../Assets/_Project/Data/Resources/Catalog/Bait/FishScrap.asset) | 1 | **none** / unlisted | #1271: `leblancs`, unlisted |
| Herring · `bait.herring` | [Herring.asset](../../Assets/_Project/Data/Resources/Catalog/Bait/Herring.asset) | 3 | **none** / unlisted | #1271: `leblancs`, `nmc_chandlery`, unlisted |
| Mackerel · `bait.mackerel` | [Mackerel.asset](../../Assets/_Project/Data/Resources/Catalog/Bait/Mackerel.asset) | 4 | **none** / unlisted | #1271: `leblancs`, `nmc_chandlery`, unlisted |
| Sea Worm · `bait.sea_worm` | [SeaWorm.asset](../../Assets/_Project/Data/Resources/Catalog/Bait/SeaWorm.asset) | 4 | **none** / unlisted | #1271: `leblancs`, unlisted |
| Shucked Clam · `bait.shucked_clam` | [ShuckedClam.asset](../../Assets/_Project/Data/Resources/Catalog/Bait/ShuckedClam.asset) | 3 | **none** / unlisted | #1271: `leblancs`, unlisted |
| Squid Strip · `bait.squid_strip` | [SquidStrip.asset](../../Assets/_Project/Data/Resources/Catalog/Bait/SquidStrip.asset) | 6 | **none** / unlisted | #1271: `leblancs`, `nmc_chandlery`, unlisted |
| Clam Bucket · `gear.bucket` | [ClamBucket.asset](../../Assets/_Project/Data/Resources/Catalog/Gear/ClamBucket.asset) | 15 | **none** / unlisted | #1271: `leblancs`, unlisted |
| Mussel Sack · `gear.mussel_sack` | [MusselSack.asset](../../Assets/_Project/Data/Resources/Catalog/Gear/MusselSack.asset) | 10 | `leblancs` / unlisted | unchanged |
| Oyster Tongs · `gear.oyster_tongs` | [OysterTongs.asset](../../Assets/_Project/Data/Resources/Catalog/Gear/OysterTongs.asset) | 45 | `leblancs` / unlisted | unchanged |
| Fishing Rod · `gear.rod` | [Rod.asset](../../Assets/_Project/Data/Resources/Catalog/Gear/Rod.asset) | 60 | `leblancs`, `nmc_chandlery` / shelf | unchanged |
| Rod Rack · `gear.rod_rack` | [RodRack.asset](../../Assets/_Project/Data/Resources/Catalog/Gear/RodRack.asset) | 25 | **none** / unlisted | #1271: `nmc_chandlery`, unlisted |
| Peck Basket · `gear.shellfish_basket` | [ShellfishBasket.asset](../../Assets/_Project/Data/Resources/Catalog/Gear/ShellfishBasket.asset) | 20 | `leblancs` / unlisted | unchanged |
| Shellfish Rake · `gear.shellfish_rake` | [ShellfishRake.asset](../../Assets/_Project/Data/Resources/Catalog/Gear/ShellfishRake.asset) | 30 | `leblancs` / unlisted | unchanged |
| Clam Shovel · `gear.shovel` | [Shovel.asset](../../Assets/_Project/Data/Resources/Catalog/Gear/Shovel.asset) | 25 | **none** / unlisted | #1271: `leblancs`, unlisted |
| Crab Pot · `trap.crab` | [CrabPotOffer.asset](../../Assets/_Project/Data/Resources/Catalog/Shipwright/CrabPotOffer.asset) | 60 | `nmc_yard` / shelf | unchanged |
| Lobster Pot · `trap.lobster` | [LobsterPotOffer.asset](../../Assets/_Project/Data/Resources/Catalog/Shipwright/LobsterPotOffer.asset) | 120 | `nmc_yard` / shelf | unchanged |

Pot offers name [LobsterTrap.asset](../../Assets/_Project/Data/Traps/LobsterTrap.asset) / [CrabTrap.asset](../../Assets/_Project/Data/Traps/CrabTrap.asset) through `TrapDefId`; offer ids are `offer.lobster_pot` and `offer.crab_pot`. The rod, shovel and rack also have capability definitions: [Rod.asset](../../Assets/_Project/Data/Tools/Rod.asset), [Shovel.asset](../../Assets/_Project/Data/Tools/Shovel.asset), [RodRack.asset](../../Assets/_Project/Data/Stowage/RodRack.asset). Their `GearId` points to the economic item; they are not additional products.

| Item / id | Capability Def | Main price / seller | Draft state |
|---|---|---|---|
| Groundfish longline tub · `longline.groundfish_tub` | [GroundfishTub.asset](../../Assets/_Project/Data/Longlines/GroundfishTub.asset) | — / **none, sold nowhere** | #1271 exempts: counted tub stock, no offer type |
| Cod Jig · `tackle.cod_jig` | [CodJig.asset](../../Assets/_Project/Data/Tackle/CodJig.asset) | — / **none, sold nowhere** | #1271 exempts: rod currently supplies lure choices; no per-lure purchase path |
| Mackerel Feathers · `tackle.mackerel_feathers` | [MackerelFeathers.asset](../../Assets/_Project/Data/Tackle/MackerelFeathers.asset) | — / **none, sold nowhere** | #1271 exempts: rod currently supplies lure choices; no per-lure purchase path |
| Soft Bait · `tackle.soft_bait` | [SoftBait.asset](../../Assets/_Project/Data/Tackle/SoftBait.asset) | — / **none, sold nowhere** | #1271 exempts: rod currently supplies lure choices; no per-lure purchase path |
| Spinner · `tackle.spinner` | [Spinner.asset](../../Assets/_Project/Data/Tackle/Spinner.asset) | — / **none, sold nowhere** | #1271 exempts: rod currently supplies lure choices; no per-lure purchase path |
| Spoon · `tackle.spoon` | [Spoon.asset](../../Assets/_Project/Data/Tackle/Spoon.asset) | — / **none, sold nowhere** | #1271 exempts: rod currently supplies lure choices; no per-lure purchase path |

[TackleBox](../../Assets/_Project/Code/Core/Save/TackleBox.cs) already has `OwnsTackle/GrantTackle` using `OwnedGear`; the missing piece is a paid offer and rod selection that respects ownership, not necessarily a new save list. The five lure choices must not be called purchasable merely because those helpers exist.

### 1.3 Hulls and engine upgrades: remain yard/dealer business

Every row is assigned to the **boatyard/hull dealer** category, never either wholesaler. A dash or motor included in a hull is not an independently priced item. #1271 adds only 16 hull offers; its extrapolated prices are draft tuning seeds. The 27 other hulls are explicitly **sold nowhere**, not presumed to be stocked by the NMC yard.

| Hull id | Hull Def; offer if any | Main price ₲ | Main seller / state | Draft price ₲; seller / state |
|---|---|---:|---|---|
| `boat.bowrider` | [hull](../../Assets/_Project/Data/Boats/Bowrider.asset) | — | **none / sold nowhere** | —; **none / sold nowhere** |
| `boat.cape_islander` | [hull](../../Assets/_Project/Data/Boats/CapeIslander.asset); [draft offer](https://github.com/apholmes7-bot/hiddenharbours/blob/3019d93dc202a8b735f671e96e611ef10805e09e/Assets/_Project/Data/Resources/Catalog/Shipwright/CapeIslanderOffer.asset) | — | **none / sold nowhere** | 17400; `nmc_yard` / unlisted |
| `boat.coastal_packet` | [hull](../../Assets/_Project/Data/Boats/CoastalPacket.asset) | — | **none / sold nowhere** | —; **none / sold nowhere** |
| `boat.console_skiff` | [hull](../../Assets/_Project/Data/Boats/ConsoleSkiff.asset); [draft offer](https://github.com/apholmes7-bot/hiddenharbours/blob/3019d93dc202a8b735f671e96e611ef10805e09e/Assets/_Project/Data/Resources/Catalog/Shipwright/ConsoleSkiffOffer.asset) | — | **none / sold nowhere** | 9700; `nmc_yard` / unlisted |
| `boat.dory` | [hull](../../Assets/_Project/Data/Boats/Dory.asset); [offer](../../Assets/_Project/Data/Resources/Catalog/Shipwright/DamagedDoryOffer.asset) | 400 + 300 repair | `nmc_yard` / shelf | unchanged |
| `boat.dory_outboard` | [hull](../../Assets/_Project/Data/Boats/DoryOutboard.asset); [offer](../../Assets/_Project/Data/Resources/Catalog/Shipwright/DoryOutboardOffer.asset) | 900 | `hectors_barrel` / shelf | unchanged |
| `boat.expedition_yacht_120` | [hull](../../Assets/_Project/Data/Boats/ExpeditionYacht120.asset) | — | **none / sold nowhere** | —; **none / sold nowhere** |
| `boat.express_cruiser` | [hull](../../Assets/_Project/Data/Boats/ExpressCruiser.asset) | — | **none / sold nowhere** | —; **none / sold nowhere** |
| `boat.go_fast` | [hull](../../Assets/_Project/Data/Boats/GoFast.asset) | — | **none / sold nowhere** | —; **none / sold nowhere** |
| `boat.jet_ski` | [hull](../../Assets/_Project/Data/Boats/JetSki.asset) | — | **none / sold nowhere** | —; **none / sold nowhere** |
| `boat.lobster_boat` | [hull](../../Assets/_Project/Data/Boats/LobsterBoat.asset); [draft offer](https://github.com/apholmes7-bot/hiddenharbours/blob/3019d93dc202a8b735f671e96e611ef10805e09e/Assets/_Project/Data/Resources/Catalog/Shipwright/LobsterBoatOffer.asset) | — | **none / sold nowhere** | 20200; `nmc_yard` / unlisted |
| `boat.lobster_inshore_hardtop_fundy` | [hull](../../Assets/_Project/Data/Boats/LobsterInshoreHardtopFundy.asset) | — | **none / sold nowhere** | —; **none / sold nowhere** |
| `boat.lobster_inshore_hardtop_newfoundland` | [hull](../../Assets/_Project/Data/Boats/LobsterInshoreHardtopNewfoundland.asset) | — | **none / sold nowhere** | —; **none / sold nowhere** |
| `boat.lobster_inshore_hardtop_northumberland` | [hull](../../Assets/_Project/Data/Boats/LobsterInshoreHardtopNorthumberland.asset); [draft offer](https://github.com/apholmes7-bot/hiddenharbours/blob/3019d93dc202a8b735f671e96e611ef10805e09e/Assets/_Project/Data/Resources/Catalog/Shipwright/LobsterInshoreHardtopNorthumberlandOffer.asset) | — | **none / sold nowhere** | 7200; `nmc_yard` / unlisted |
| `boat.lobster_inshore_open_fundy` | [hull](../../Assets/_Project/Data/Boats/LobsterInshoreOpenFundy.asset) | — | **none / sold nowhere** | —; **none / sold nowhere** |
| `boat.lobster_inshore_open_newfoundland` | [hull](../../Assets/_Project/Data/Boats/LobsterInshoreOpenNewfoundland.asset) | — | **none / sold nowhere** | —; **none / sold nowhere** |
| `boat.lobster_inshore_open_northumberland` | [hull](../../Assets/_Project/Data/Boats/LobsterInshoreOpenNorthumberland.asset); [draft offer](https://github.com/apholmes7-bot/hiddenharbours/blob/3019d93dc202a8b735f671e96e611ef10805e09e/Assets/_Project/Data/Resources/Catalog/Shipwright/LobsterInshoreOpenNorthumberlandOffer.asset) | — | **none / sold nowhere** | 7200; `nmc_yard` / unlisted |
| `boat.lobster_offshore_hardtop_fundy` | [hull](../../Assets/_Project/Data/Boats/LobsterOffshoreHardtopFundy.asset) | — | **none / sold nowhere** | —; **none / sold nowhere** |
| `boat.lobster_offshore_hardtop_newfoundland` | [hull](../../Assets/_Project/Data/Boats/LobsterOffshoreHardtopNewfoundland.asset) | — | **none / sold nowhere** | —; **none / sold nowhere** |
| `boat.lobster_offshore_hardtop_northumberland` | [hull](../../Assets/_Project/Data/Boats/LobsterOffshoreHardtopNorthumberland.asset); [draft offer](https://github.com/apholmes7-bot/hiddenharbours/blob/3019d93dc202a8b735f671e96e611ef10805e09e/Assets/_Project/Data/Resources/Catalog/Shipwright/LobsterOffshoreHardtopNorthumberlandOffer.asset) | — | **none / sold nowhere** | 37200; `nmc_yard` / unlisted |
| `boat.lobster_offshore_open_fundy` | [hull](../../Assets/_Project/Data/Boats/LobsterOffshoreOpenFundy.asset) | — | **none / sold nowhere** | —; **none / sold nowhere** |
| `boat.lobster_offshore_open_newfoundland` | [hull](../../Assets/_Project/Data/Boats/LobsterOffshoreOpenNewfoundland.asset) | — | **none / sold nowhere** | —; **none / sold nowhere** |
| `boat.lobster_offshore_open_northumberland` | [hull](../../Assets/_Project/Data/Boats/LobsterOffshoreOpenNorthumberland.asset); [draft offer](https://github.com/apholmes7-bot/hiddenharbours/blob/3019d93dc202a8b735f671e96e611ef10805e09e/Assets/_Project/Data/Resources/Catalog/Shipwright/LobsterOffshoreOpenNorthumberlandOffer.asset) | — | **none / sold nowhere** | 37200; `nmc_yard` / unlisted |
| `boat.lobster_standard_hardtop_fundy` | [hull](../../Assets/_Project/Data/Boats/LobsterStandardHardtopFundy.asset) | — | **none / sold nowhere** | —; **none / sold nowhere** |
| `boat.lobster_standard_hardtop_newfoundland` | [hull](../../Assets/_Project/Data/Boats/LobsterStandardHardtopNewfoundland.asset) | — | **none / sold nowhere** | —; **none / sold nowhere** |
| `boat.lobster_standard_hardtop_northumberland` | [hull](../../Assets/_Project/Data/Boats/LobsterStandardHardtopNorthumberland.asset); [draft offer](https://github.com/apholmes7-bot/hiddenharbours/blob/3019d93dc202a8b735f671e96e611ef10805e09e/Assets/_Project/Data/Resources/Catalog/Shipwright/LobsterStandardHardtopNorthumberlandOffer.asset) | — | **none / sold nowhere** | 20200; `nmc_yard` / unlisted |
| `boat.lobster_standard_open_fundy` | [hull](../../Assets/_Project/Data/Boats/LobsterStandardOpenFundy.asset) | — | **none / sold nowhere** | —; **none / sold nowhere** |
| `boat.lobster_standard_open_newfoundland` | [hull](../../Assets/_Project/Data/Boats/LobsterStandardOpenNewfoundland.asset) | — | **none / sold nowhere** | —; **none / sold nowhere** |
| `boat.lobster_standard_open_northumberland` | [hull](../../Assets/_Project/Data/Boats/LobsterStandardOpenNorthumberland.asset); [draft offer](https://github.com/apholmes7-bot/hiddenharbours/blob/3019d93dc202a8b735f671e96e611ef10805e09e/Assets/_Project/Data/Resources/Catalog/Shipwright/LobsterStandardOpenNorthumberlandOffer.asset) | — | **none / sold nowhere** | 20200; `nmc_yard` / unlisted |
| `boat.motor_yacht_84` | [hull](../../Assets/_Project/Data/Boats/MotorYacht84.asset) | — | **none / sold nowhere** | —; **none / sold nowhere** |
| `boat.punt` | [hull](../../Assets/_Project/Data/Boats/Punt.asset); [offer](../../Assets/_Project/Data/Resources/Catalog/Shipwright/PuntOffer.asset) | 1800 | `nmc_yard` / shelf | unchanged |
| `boat.punt_upgraded` | [hull](../../Assets/_Project/Data/Boats/PuntUpgraded.asset); [draft offer](https://github.com/apholmes7-bot/hiddenharbours/blob/3019d93dc202a8b735f671e96e611ef10805e09e/Assets/_Project/Data/Resources/Catalog/Shipwright/PuntUpgradedOffer.asset) | — | **none / sold nowhere** | 2300; `nmc_yard` / unlisted |
| `boat.side_dragger` | [hull](../../Assets/_Project/Data/Boats/SideDragger.asset); [draft offer](https://github.com/apholmes7-bot/hiddenharbours/blob/3019d93dc202a8b735f671e96e611ef10805e09e/Assets/_Project/Data/Resources/Catalog/Shipwright/SideDraggerOffer.asset) | — | **none / sold nowhere** | 216000; `nmc_yard` / unlisted |
| `boat.sloop_30` | [hull](../../Assets/_Project/Data/Boats/Sloop30.asset) | — | **none / sold nowhere** | —; **none / sold nowhere** |
| `boat.sloop_88` | [hull](../../Assets/_Project/Data/Boats/Sloop88.asset) | — | **none / sold nowhere** | —; **none / sold nowhere** |
| `boat.sport_fisher_convertible` | [hull](../../Assets/_Project/Data/Boats/SportFisherConvertible.asset) | — | **none / sold nowhere** | —; **none / sold nowhere** |
| `boat.sport_fisher_skybridge` | [hull](../../Assets/_Project/Data/Boats/SportFisherSkybridge.asset) | — | **none / sold nowhere** | —; **none / sold nowhere** |
| `boat.sport_skiff` | [hull](../../Assets/_Project/Data/Boats/SportSkiff.asset); [draft offer](https://github.com/apholmes7-bot/hiddenharbours/blob/3019d93dc202a8b735f671e96e611ef10805e09e/Assets/_Project/Data/Resources/Catalog/Shipwright/SportSkiffOffer.asset) | — | **none / sold nowhere** | 8200; `nmc_yard` / unlisted |
| `boat.sport_skiff_mk2` | [hull](../../Assets/_Project/Data/Boats/SportSkiffMk2.asset); [draft offer](https://github.com/apholmes7-bot/hiddenharbours/blob/3019d93dc202a8b735f671e96e611ef10805e09e/Assets/_Project/Data/Resources/Catalog/Shipwright/SportSkiffMk2Offer.asset) | — | **none / sold nowhere** | 9300; `nmc_yard` / unlisted |
| `boat.sport_skiff_twin` | [hull](../../Assets/_Project/Data/Boats/SportSkiffTwin.asset); [draft offer](https://github.com/apholmes7-bot/hiddenharbours/blob/3019d93dc202a8b735f671e96e611ef10805e09e/Assets/_Project/Data/Resources/Catalog/Shipwright/SportSkiffTwinOffer.asset) | — | **none / sold nowhere** | 8400; `nmc_yard` / unlisted |
| `boat.stern_trawler` | [hull](../../Assets/_Project/Data/Boats/SternTrawler.asset) | — | **none / sold nowhere** | —; **none / sold nowhere** |
| `boat.stern_trawler_mk2` | [hull](../../Assets/_Project/Data/Boats/SternTrawlerMk2.asset) | — | **none / sold nowhere** | —; **none / sold nowhere** |
| `boat.tanker` | [hull](../../Assets/_Project/Data/Boats/Tanker.asset) | — | **none / sold nowhere** | —; **none / sold nowhere** |
| `boat.yacht_tender` | [hull](../../Assets/_Project/Data/Boats/YachtTender.asset) | — | **none / sold nowhere** | —; **none / sold nowhere** |
| `boat.zodiac_frc` | [hull](../../Assets/_Project/Data/Boats/ZodiacFrc.asset); [draft offer](https://github.com/apholmes7-bot/hiddenharbours/blob/3019d93dc202a8b735f671e96e611ef10805e09e/Assets/_Project/Data/Resources/Catalog/Shipwright/ZodiacFrcOffer.asset) | — | **none / sold nowhere** | 8200; `nmc_yard` / unlisted |
| `boat.zodiac_hurricane` | [hull](../../Assets/_Project/Data/Boats/ZodiacHurricane.asset); [draft offer](https://github.com/apholmes7-bot/hiddenharbours/blob/3019d93dc202a8b735f671e96e611ef10805e09e/Assets/_Project/Data/Resources/Catalog/Shipwright/ZodiacHurricaneOffer.asset) | — | **none / sold nowhere** | 10300; `nmc_yard` / unlisted |

The sold-nowhere hulls break down as 12 regional lobster variants (Fundy/Newfoundland), 8 pleasure craft, 3 large yachts and 4 ships; the intended future sellers in [#1271's inventory](https://github.com/apholmes7-bot/hiddenharbours/blob/3019d93dc202a8b735f671e96e611ef10805e09e/docs/design/economy-item-sellers.md) are regional yards, a pleasure-boat dealer, a yacht broker and a ship broker. This document does not authorize those businesses or a later-phase fleet economy. The existing `boat.dory_outboard` offer is an engine refit represented as a second hull id, not a loose outboard in inventory; move the trade to the yard when that counter is ready.

### 1.4 Other catalogue stock: 19 items stay with their current trade

| Item / id | Offer Def | Main price ₲ | Seller / state | Draft change; reason it stays |
|---|---|---:|---|---|
| Shellfish Licence · `license.clam` | [ClamLicense.asset](../../Assets/_Project/Data/Resources/Catalog/Licenses/ClamLicense.asset) | 15 | `leblancs` / shelf | licensing authority; legal permission |
| Cod Fishing License · `license.cod` | [CodLicense.asset](../../Assets/_Project/Data/Resources/Catalog/Licenses/CodLicense.asset) | 60 | `nmc_harbourmaster`, `leblancs` / shelf | licensing authority; legal permission |
| Loaf of Bread · `supply.bread` | [Bread.asset](../../Assets/_Project/Data/Resources/Catalog/Supplies/Bread.asset) | 4 | `leblancs` / shelf | island provisions; food |
| Bag of Ice · `supply.ice` | [Ice.asset](../../Assets/_Project/Data/Resources/Catalog/Supplies/Ice.asset) | 6 | `leblancs` / shelf | island provisions; retain early freshness lesson |
| Oatcakes · `supply.oatcakes` | [Oatcakes.asset](../../Assets/_Project/Data/Resources/Catalog/Supplies/Oatcakes.asset) | 2 | `leblancs` / shelf | island provisions; food |
| Tin of Beans · `supply.tinned_beans` | [TinnedBeans.asset](../../Assets/_Project/Data/Resources/Catalog/Supplies/TinnedBeans.asset) | 3 | `leblancs` / shelf | island provisions; food |
| Dually 3500 · `vehicle.dually_3500` | [DealerDually3500Offer.asset](../../Assets/_Project/Data/Resources/Catalog/Vehicles/DealerDually3500Offer.asset) | 7500 | `nmc_vehicle_dealer` / unlisted | vehicle dealer / industrial yard, road transport |
| Enduro 250 · `vehicle.enduro_250` | [DealerEnduro250Offer.asset](../../Assets/_Project/Data/Resources/Catalog/Vehicles/DealerEnduro250Offer.asset) | 900 | `nmc_vehicle_dealer` / unlisted | vehicle dealer / industrial yard, road transport |
| Hightop Van · `vehicle.hightop_van` | [DealerHightopVanOffer.asset](../../Assets/_Project/Data/Resources/Catalog/Vehicles/DealerHightopVanOffer.asset) | 4000 | `nmc_vehicle_dealer` / unlisted | vehicle dealer / industrial yard, road transport |
| Modern 2500 · `vehicle.modern_2500` | [DealerModern2500Offer.asset](../../Assets/_Project/Data/Resources/Catalog/Vehicles/DealerModern2500Offer.asset) | 5500 | `nmc_vehicle_dealer` / unlisted | vehicle dealer / industrial yard, road transport |
| Modern 3500 · `vehicle.modern_3500` | [DealerModern3500Offer.asset](../../Assets/_Project/Data/Resources/Catalog/Vehicles/DealerModern3500Offer.asset) | 7000 | `nmc_vehicle_dealer` / unlisted | vehicle dealer / industrial yard, road transport |
| Modern 350 · `vehicle.modern_350` | [DealerModern350Offer.asset](../../Assets/_Project/Data/Resources/Catalog/Vehicles/DealerModern350Offer.asset) | 3500 | `nmc_vehicle_dealer` / unlisted | vehicle dealer / industrial yard, road transport |
| Trike 200 · `vehicle.trike_200` | [DealerTrike200Offer.asset](../../Assets/_Project/Data/Resources/Catalog/Vehicles/DealerTrike200Offer.asset) | 800 | `nmc_vehicle_dealer` / unlisted | vehicle dealer / industrial yard, road transport |
| Utility Quad 4x4 · `vehicle.utility_quad` | [DealerUtilityQuadOffer.asset](../../Assets/_Project/Data/Resources/Catalog/Vehicles/DealerUtilityQuadOffer.asset) | 1200 | `nmc_vehicle_dealer` / unlisted | vehicle dealer / industrial yard, road transport |
| Aero Sleeper Semi · `vehicle.aero_semi` | [YardAeroSemiOffer.asset](../../Assets/_Project/Data/Resources/Catalog/Vehicles/YardAeroSemiOffer.asset) | 25000 | `nmc_industrial_yard` / unlisted | vehicle dealer / industrial yard, road transport |
| Cabover Box Truck (needs work) · `vehicle.cabover_box` | [YardCaboverBoxUsedOffer.asset](../../Assets/_Project/Data/Resources/Catalog/Vehicles/YardCaboverBoxUsedOffer.asset) | 5000 + 4000 repair | `nmc_industrial_yard` / unlisted | vehicle dealer / industrial yard, road transport |
| Classic Long-Nose Semi · `vehicle.classic_semi` | [YardClassicSemiOffer.asset](../../Assets/_Project/Data/Resources/Catalog/Vehicles/YardClassicSemiOffer.asset) | 25000 | `nmc_industrial_yard` / unlisted | vehicle dealer / industrial yard, road transport |
| Conventional Box Truck · `vehicle.conv_box` | [YardConvBoxOffer.asset](../../Assets/_Project/Data/Resources/Catalog/Vehicles/YardConvBoxOffer.asset) | 12000 | `nmc_industrial_yard` / unlisted | vehicle dealer / industrial yard, road transport |
| Otter 8x8 · `vehicle.otter_8x8` | [DealerOtter8x8Offer.asset](https://github.com/apholmes7-bot/hiddenharbours/blob/3019d93dc202a8b735f671e96e611ef10805e09e/Assets/_Project/Data/Resources/Catalog/Vehicles/DealerOtter8x8Offer.asset) | — | **none on main** | #1271: 2500, `nmc_vehicle_dealer`, unlisted; vehicle dealer / industrial yard, road transport |

Vehicle seller tags are offer records, not evidence of a built dealer; [BuyCatalog.cs](../../Assets/_Project/Code/Economy/BuyCatalog.cs) has seven stock arms and no vehicle arm. No food, licence or road vehicle migrates to a marine wholesaler. An additional ice outlet at the fishing wholesaler is an owner choice, not a removal of the island supply.

### 1.5 Other payments and non-merchant items (outside the 98-item totals)

These prevent “every purchasable thing” from silently omitting purchases outside the catalogue. They do not become wholesale stock.

| Property | Def | Current price ₲ | Seller / purchase seam; destination |
|---|---|---:|---|
| `home.cumberland_route_bungalow` | [CumberlandRouteBungalow.asset](../../Assets/_Project/Data/Homes/CumberlandRouteBungalow.asset) | 4500 | Property's `HomeDoor` / deed counterparty, not a catalogue SellerDef; stays property business |
| `home.cumberland_two_storey` | [CumberlandTwoStorey.asset](../../Assets/_Project/Data/Homes/CumberlandTwoStorey.asset) | 7500 | Property's `HomeDoor` / deed counterparty, not a catalogue SellerDef; stays property business |
| `home.ginny_lot_camper` | [GinnyLotCamper.asset](../../Assets/_Project/Data/Homes/GinnyLotCamper.asset) | 900 | Property's `HomeDoor` / deed counterparty, not a catalogue SellerDef; stays property business |
| `home.nmc_shore_lane_saltbox` | [NmcShoreLaneSaltbox.asset](../../Assets/_Project/Data/Homes/NmcShoreLaneSaltbox.asset) | 5000 | Property's `HomeDoor` / deed counterparty, not a catalogue SellerDef; stays property business |
| `home.nmc_south_shore_estate` | [NmcSouthShoreEstate.asset](../../Assets/_Project/Data/Homes/NmcSouthShoreEstate.asset) | 18000 | Property's `HomeDoor` / deed counterparty, not a catalogue SellerDef; stays property business |
| `home.nmc_waterfront_house` | [NmcWaterfrontHouse.asset](../../Assets/_Project/Data/Homes/NmcWaterfrontHouse.asset) | 9000 | Property's `HomeDoor` / deed counterparty, not a catalogue SellerDef; stays property business |
| `home.rice_point_point_house` | [RicePointPointHouse.asset](../../Assets/_Project/Data/Homes/RicePointPointHouse.asset) | 8000 | Property's `HomeDoor` / deed counterparty, not a catalogue SellerDef; stays property business |
| `home.st_peters_red_saltbox` | [StPetersRedSaltbox.asset](../../Assets/_Project/Data/Homes/StPetersRedSaltbox.asset) | 6000 | Property's `HomeDoor` / deed counterparty, not a catalogue SellerDef; stays property business |

[HomeDoor.cs](../../Assets/_Project/Code/Economy/HomeDoor.cs) charges `HomeDef.Price` and records a deed; a priced HomeDef alone does not prove its door has been placed. Keep all eight authored prices as **owner proposals to retain**; progression ranges from the ₲900 camper to the ₲18,000 estate and remains separate from equipment savings.

Fuel is sold by grade and litre, not as another boat electronic. Current [Route 91](../../Assets/_Project/Data/FuelStations/Route91Station.asset) prices are gas **1.55**, diesel **1.40**, mixed **2.35**, oil **8.50**, stove oil **1.20** ₲/L. [Wharf Pumps](../../Assets/_Project/Data/FuelStations/NmcWharfPumps.asset) sells gas **2.05** and diesel **1.85** ₲/L; mixed **2.95** and oil **10.50** are authored but `Available: 0`, and stove oil is absent. **Owner proposal: retain every price and availability flag**; the cheaper road supply versus convenient wharf supply supports manual fuel transport and recurring operating costs. See [FuelStationDef.cs](../../Assets/_Project/Code/Economy/FuelStationDef.cs) / [FuelPump.cs](../../Assets/_Project/Code/Economy/FuelPump.cs) for charging. Neither wholesaler replaces a fuel station.

| Existing non-merchant item family | Evidence | Current price / seller; recommendation |
|---|---|---|
| Harbour digital watch (1) | [HarbourDigital.asset](../../Assets/_Project/Data/Resources/Watches/HarbourDigital.asset) | Price 0, starts owned; no replacement seller. Retain starting gift; replacement shop and price deferred. |
| Golf clubs (4) | [Driver.asset](../../Assets/_Project/Data/Golf/Driver.asset), [Iron.asset](../../Assets/_Project/Data/Golf/Iron.asset), [Putter.asset](../../Assets/_Project/Data/Golf/Putter.asset), [Wedge.asset](../../Assets/_Project/Data/Golf/Wedge.asset) | No price/offer; course loans them (#1271 exemption). Stay at course; pro-shop sales deferred. |
| Carriable fuel containers/nozzles (36) | [FuelContainers definitions](../../Assets/_Project/Data/FuelContainers); [Carriable flag](../../Assets/_Project/Code/Art/FuelContainerDef.cs); [#1271 exemption](https://github.com/apholmes7-bot/hiddenharbours/blob/3019d93dc202a8b735f671e96e611ef10805e09e/Assets/Tests/EditMode/Economy/EveryItemHasASellerTests.cs) | No empty-container price or offer; station stock. Stay station equipment; purchasable replacement can needs an offer and till. |

The 41 starting/borrowed/station items above are also **not sold by a merchant**, but are excluded from the 39 missing offers in the equipment/catalogue count. Thus the broader ownable audit has **80 without seller offers (39 + 41)**. Caught fish, crops, recipe outputs and art definitions are not retail offers; their sale values are not purchase prices.

### 1.6 Radio, heading instruments, lamps, oars, lines, nets and deck fittings

| Thing examined | Current Def / evidence | Current price and seller | Proposed trade / limit |
|---|---|---|---|
| VHF radio / barometer | [Diegetic devices](../design/diegetic-devices.md), offshore direction / M2-09; no InstrumentOffer | None; not purchasable | Boat/gear wholesaler when defined; do not invent an approved price or shipped radio from design text. |
| Heading, RPM, speed, fuel, trim and clear-water readouts | [WorkingHelmFitDef.cs](../../Assets/_Project/Code/Boats/WorkingHelmFitDef.cs), [WorkingHelm.cs](../../Assets/_Project/Code/Core/Boats/WorkingHelm.cs) and [factory fits](../../Assets/_Project/Data/Boats/InstrumentFits) | Hull fit/readout flags, no independent product price or seller | Boat/gear for future separately fitted gauges; heading currently comes from compass/motion fit. |
| Navigation, cabin, search and working lamps | [HullLampKind](../../Assets/_Project/Code/Core/Boats/HullLamp.cs); hull visual lamp anchors / existing lamp components | Included fittings, no purchase Def, price or seller | Boat/gear for future replaceable fixtures; existing hull lights stay included. No look change is proposed. |
| Oars | [Dory.asset](../../Assets/_Project/Data/Boats/Dory.asset); [Propulsion/OarPower/CanLimpHome](../../Assets/_Project/Code/Boats/BoatHullDef.cs) | Included with rowed hull; no separate item/price/seller | Boat/gear for replacement oars, because they propel the boat; do not charge a starter for already supplied oars. |
| Rod line, loose hooks, rope, swivels and floats | [TackleDef.cs](../../Assets/_Project/Code/Fishing/TackleDef.cs), [GroundfishTub.asset](../../Assets/_Project/Data/Longlines/GroundfishTub.asset) | Bundled in lure/tub/rod capability, no independent retail Def or seller | Fishing gear if made individual stock; no fictional unit price. |
| Trawl net, doors and handling rig | [BottomOtterPrototype.asset](../../Assets/_Project/Data/Trawls/BottomOtterPrototype.asset) (`trawl_gear.bottom_otter_prototype`), [SideStarboardPrototype.asset](../../Assets/_Project/Data/Trawls/SideStarboardPrototype.asset), [SternPrototype.asset](../../Assets/_Project/Data/Trawls/SternPrototype.asset) | No price/offer/seller; all three unreviewed prototypes | Fishing wholesaler for eventual net/door stock; keep unavailable pending reviewed gear, compatibility and purchase ownership. |
| Hauler, davit, banding table, bait/chopping box, tub rack and hull storage | [CapeIslanderDeckGear.asset](../../Assets/_Project/Data/Boats/DeckGear/CapeIslanderDeckGear.asset), [LobsterBoatDeckGear.asset](../../Assets/_Project/Data/Boats/DeckGear/LobsterBoatDeckGear.asset), [DeckLoopKit.asset](../../Assets/_Project/Data/Boats/DeckGear/DeckLoopKit.asset); [DeckContainerDef.cs](../../Assets/_Project/Code/Boats/DeckContainerDef.cs) | Included deck fixtures/containers or art kit entries, no retail offer or separate price | Fishing wholesaler for future working gear; boat/gear for generic boat storage. No double charge for fitted fixtures. |

These are unpriced families or fittings, not extra counted products with invented identities. Their absence is a data/model gap rather than evidence of a shop shelf.

## 2. Routing and stock at the two new businesses

The following is the proposed destination for every counted identity. “Ready shape” means an existing offer type can describe it, not that the new building or a physical fulfilment path exists.

| Destination | Count | Ready shape / gated | Exact stock and migration |
|---|---:|---|---|
| Boat/gear wholesaler | 10 | Four existing InstrumentOffers; six without offers | Depth sounder, fish finder, chartplotter, Loran; later dome compass, flush compass, radar, wind dial, wind display and paid autopilot. Move sounder off `leblancs`; redirect #1269's Loran from `leblancs`, #1271's finder from `leblancs` and chartplotter from `nmc_chandlery`. The six missing products have no seller to move from. |
| Fishing-gear wholesaler | 23 | 17 existing priced offers; six without a purchase path | All eight `gear.*` rows in §1.2, all seven bait, both pots, all five tackle and the groundfish tub. Move the NMC rod offer from `nmc_chandlery`, both pots from `nmc_yard`; redirect #1271's rack and NMC bait tags from `nmc_chandlery`. Add a wholesaler seller to the island-only shovel, bucket, shellfish equipment and bait offers. The tackle/tub six remain unavailable until their purchase paths exist. |
| Boatyard / hull dealer | 46 | 19 priced offers in combined snapshot, 27 no-offer hulls | All hull/refit rows in §1.3. Dory, repair and punt stay at `nmc_yard`; move the outboard upgrade from `hectors_barrel` to `nmc_yard`. Preserve #1271's proposed yard offers; missing regional/pleasure/yacht/ship sellers remain deferred, not silently added to NMC. |
| Stays with other trade | 19 | 19 priced offers; 13 vehicles still lack dealer access | Four island supplies, two licences, 13 vehicles (§1.4). Food/ice serve the opening, licences are permission, vehicles belong to their dealers. |
| Total | 98 | 59 offers, 39 missing offers | No double count for a product offered at two counters. |

**Recommended island overlap:** retain the island rod and capelin counter, the clam licence and all four provisions. Preserve the proposed island sellers for replacement shovel/bucket, shellfish equipment and other bait; those additional rows remain unlisted until their capability and counter are ready. The island's rod and bait are essential before the first crossing. The fishing wholesaler becomes the full NMC range, not a reason to remove the early loop. Marine electronics are exclusive to the new boat/gear business under the current ruling; no electronics remain at the island placeholder after migration.

**Fishing-stock boundary:** rod rack belongs with rods, not electronics. Replacement oars would belong with boat gear because they propel a hull; loose line/hooks/rope and nets would belong with fishing gear. Ice stays at the island store; an additional fishing-wholesaler ice outlet can be approved independently. Lamps, radio, gauges and fitted deck gear have the future destinations in §1.6, but none is promoted into stock without an actual item/offer and supported capability.

**Buildings remain separate.** [#1276's buildings inventory](https://github.com/apholmes7-bot/hiddenharbours/blob/a851f0942a3a18403b158bc919df55e6b59707c0/docs/world/nmc-buildings-inventory.md) proposes a vacant Water Street storefront near the chandlery lot (-160, 237) for boat/gear and a truck-served warehouse by the lumber shed (-279, 111) for fishing gear. These are reference points requiring a lot census, not approved footprints or placements. The building inventory owns that proposal; no scene, builder, ground or art change is part of this document.

## 3. How the current data can carry the stock

Proposed new identities (not authored here): `seller.nmc_boat_gear_wholesaler` and `seller.nmc_fishing_gear_wholesaler`. Each needs its own [SellerDef](../../Assets/_Project/Code/Economy/SellerDef.cs): `Id`, `KeeperNpcId`, `TradeId`. Keepers and trade identities need content review; do not rename a yard or reuse the island store id to make a missing business appear to exist.

| Stock | Existing Def / fields | Vendor and current ownership | Proposed use |
|---|---|---|---|
| Four priced electronics; five reserved instrument products later | [InstrumentOffer](../../Assets/_Project/Code/Economy/InstrumentOffer.cs): `Id`, `DisplayName`, `Flavor`, `Price`, `Catalog` | `InstrumentShop`; `InstrumentLocker` / `SaveData.HullInstruments` per hull | Change seller tags for the four existing offers; create independent offers for the five missing ids only after prices and capabilities are accepted. Keep ids stable. |
| Eight fishing gear items | [GearOffer](../../Assets/_Project/Code/Economy/GearOffer.cs): `Id`, `Price`, `CarryCapacityUnits`, `Catalog` | `GearShop`; presence in `SaveData.OwnedGear` | Add fishing-wholesaler seller; retain island overlap as §2. `ToolDef.GearId` / `StowageDef.GearId` link the same economic item. |
| Seven bait products | [BaitDef](../../Assets/_Project/Code/Economy/BaitDef.cs): `Id`, `Price`, `LotSize`, `Catalog` | `BaitShop`; counted `BaitStock` through `TackleBox` | Same bait ids and unit prices; show lot quantity and full payment. No implicit bulk discount. |
| Two pots | [PotOffer](../../Assets/_Project/Code/Economy/PotOffer.cs): `Id`, `TrapDefId`, `Price`, `Catalog` | `PotShop`; `PotLocker` / `PotStock` counted per trap | Retag from yard to fishing wholesaler; preserve owned/deployed/aboard distinction. |
| Five tackle types | [TackleDef](../../Assets/_Project/Code/Fishing/TackleDef.cs): `Id`, `Lure`, `JigStyle`, species preferences | `TackleBox` has ownership helpers; no existing retail offer/vendor | A proposed tackle offer should name the capability id. Do not assume the documented but absent `TackleOffer` already exists. |
| Longline tub | [LonglineDef](../../Assets/_Project/Code/Fishing/LonglineDef.cs): `Id`, `HooksPerTub`, `HooksPerBaitUnit`, `MaxTubsPerSet` | [TubLocker](../../Assets/_Project/Code/Core/Save/TubLocker.cs) counts stock; no offer/vendor | A counted tub offer naming the longline id is missing; a presence-only gear sale cannot represent repeated tub purchases. |
| Hull sale, damage/repair and outboard upgrade | [ShipwrightOffer](../../Assets/_Project/Code/Economy/ShipwrightOffer.cs): `BoatId`, `Price`, `StartsDamaged`, `RepairCost`, `Catalog` | `Shipwright`; existing fleet/repair ledgers | Remain yard business. Preserve damage and refit behaviour; the engine offer is not a marine-electronics product. |
| Autopilot | #1270 `BoatHullDef.AutopilotOffered` (eligibility only) | Current draft supplies heading hold outright on eligible hulls, not a paid ownership record | A priced per-hull product, an accepted ownership/fitment design and purchase gating are still required. |

Every catalogue offer uses [CatalogListing](../../Assets/_Project/Code/Economy/CatalogListing.cs): `Listed`, `Section`, `Sellers[]`, `SortOrder`. A seller array supports island overlap; it does **not** support different prices at different sellers. Existing gear/instrument shelf sections suffice; the two businesses do not require new enum values. The [catalogue source](../../Assets/_Project/Code/Economy/CatalogSource.cs) scans the catalogue folder; an asset stored elsewhere is not automatically stock.

The boat/gear counter needs an `InstrumentShop` arm. Fishing needs `GearShop`, `BaitShop`, `PotShop`, plus a tub/tackle transaction design before those products can be listed; add `SupplyShop` only if ice is approved there. Wire `_sellerId`, `_offer`/`_bait`/other wares reference, `_walletProvider` and the seller's catalogue presenter/dialogue to the matching new identity. [BuyCatalog.ArmsFor](../../Assets/_Project/Code/Economy/BuyCatalog.cs) discovers one arm per kind and can quote that kind's listings; nevertheless [StoreWiringTests](../../Assets/Tests/EditMode/StoreWiringTests.cs) requires one committed vendor stock entry for every listed (item, seller) pair. Retagging alone will fail that contract. Update listing, counter wiring and dialogue together in the eventual implementation; do not weaken the guard.

### 3.1 Gaps that must remain explicit

| Gap | Evidence and consequence | Smallest follow-up boundary |
|---|---|---|
| Two missing businesses | #1276 records no wholesaler buildings; no SellerDef, keeper, counter or dialogue for either | Separate content work with the building/scene owners; census proposed lots before placement. |
| Five reserved electronics have no offers | `BoatEquipment` ids exist, but only four InstrumentOffer assets exist | New one-entity offers with accepted prices and identity additions; no changes to the frozen identity ledger. |
| Radio, lamps, gauges, oars and net/deck gear lack retail identities | §1.6 distinguishes fitted capability/art/prototypes from merchandise | Define ownership and supported products in phase; never infer retail stock from a rendered fitting. |
| Fit compatibility is not a purchase check | [InstrumentShop.TryBuy](../../Assets/_Project/Code/Economy/InstrumentShop.cs) checks an active hull, duplicate id and money, but not slot compatibility or factory equipment; `BoatEquipment.EffectiveFit` can ignore unsupported purchases | A Core-facing eligibility quote should reject unsupported or already factory-fitted equipment before payment. Loran currently fits only `helm.cape_islander`; a null-console dory cannot use a sounder. |
| Inland counter versus active hull | `InstrumentShop.TargetHull` uses only `SaveData.ActiveHullId`; no explicit owned/moored-hull selection | Define which boat the goods will be installed on while shopping ashore. Do not guess that walking into a shop preserves a valid target. |
| Physical fulfilment and storage | Current instrument purchase immediately adds a fitted id; gear/pot/bait purchases update global ownership/counts, not a counter pickup and carried load | Goods must be collected, carried, loaded and unloaded by hand; hull upgrades require delivery and installation. No remote fitting, teleported purchases or warehouse-to-boat transfer is authorized. Existing ownership ledgers can record entitlements but cannot prove where an object is. Coordinate with [goods-handling phasing](../design/goods-handling/06-phasing.md), without editing saves here. |
| Paid autopilot not built | #1270's 35 offered hulls temporarily receive it outright; neither price nor purchase is defined | Agree a per-hull product and ownership/installation contract. Fitted must mean eligible **and** purchased/installed; do not sell an upgrade already granted outright. Save-format work belongs to its separately authorized implementation. |
| Tackle/tub commerce incomplete | Existing ownership helpers / counted tub ledger do not expose the necessary paid rows; rod selection currently bypasses lure ownership | Add the narrow retail and selection seams; preserve starter play. Do not sell another presence flag for a counted tub. |
| Wholesale quantity, local stock, seller-specific price and buyback absent | `Price` is on the product; `Sellers[]` has no price/quantity fields. Global gear presence cannot sell multiple replacement racks; bait has lots, not price tiers. No general equipment buyback in these vendors | “Wholesaler” initially names the full-range trade. Any finite stock, multi-unit replacement purchase, bulk discounts or resale rules need an explicit later design, not a label-only promise. |

Autopilot eligibility must retain #1270's full rule: 35 non-outboard hulls, including both sloops, three sterndrives and the jet ski; ten outboard hulls and the rowed dory excluded. `Propulsion = Engine` alone cannot decide this: it includes outboards and inboards. Use the reviewed offered-hull data rather than inventing a new drive inference. The purchase design must also accommodate eligible large hulls without a current console pointer; `SupportsLoran` is not an autopilot eligibility test.

## 4. Prices for owner review

**Every number in a “proposed” column, including a recommendation to keep a current value, is an unaccepted proposal for the owner.** Main values and draft values remain as authored. “—” means no current price, not free. Future products stay unavailable until their data and purchase path exist. Prices below are game currency, not real-world equipment prices.

### 4.1 Income anchors and limits

[Economy and business §8](../design/economy-and-business.md#8-the-economic-progression-curve) targets net-worth bands of about ₲0–1,000 for the dory, ₲1,000–8,000 for first boat/gear, ₲8,000–40,000 for workboat/first hire, then ₲40,000–150,000 for processing. **Net worth is not daily take-home income.** [Progression pacing §2–4](../design/m1-progression-pacing.md) targets the rod around day 2–3, the bought-and-repaired dory around days 6–9, pots around days 11–13 and the outboard around days 13–15. Those are design targets, not verified forecasts for these shops.

| Income anchor | Authored base value ₲ per landed unit | Def |
|---|---:|---|
| Soft-shell clam | 2 | [SoftShellClam.asset](../../Assets/_Project/Data/Fish/SoftShellClam.asset) |
| Mackerel | 10 | [Mackerel.asset](../../Assets/_Project/Data/Fish/Mackerel.asset) |
| Atlantic cod | 14 | [AtlanticCod.asset](../../Assets/_Project/Data/Fish/AtlanticCod.asset) |
| Haddock | 16 | [Haddock.asset](../../Assets/_Project/Data/Fish/Haddock.asset) |
| American lobster | 28 | [AmericanLobster.asset](../../Assets/_Project/Data/Fish/AmericanLobster.asset) |
| Rock crab | 4 | [RockCrab.asset](../../Assets/_Project/Data/Fish/RockCrab.asset) |

Actual sale income is lower with supply pressure, spoilage, a cheaper outlet and operating expenses. [SellPricing](../../Assets/_Project/Code/Economy/SellPricing.cs) prices successive units along the curve with integer rounding; [GameConfig](../../Assets/_Project/Data/Config/GameConfig.asset) sets the island price level to 0.6 versus NMC's 1.0. Cod also requires the ₲120 licence. A ₲60 rod represents 30 clams or 4.3 cod **at nominal base values**, before any of those deductions; a new seller does not make that work disappear.

For scale only, six cod in the six-unit dory have nominal value ₲84. Conditional on spending one ₲2 capelin per landed fish and one ₲6 ice bag, the arithmetic remainder is ₲66 **before supply pressure, freshness, fuel or other costs**. A 60-unit Cape full of cod similarly gives nominal gross ₲840 and a conditional remainder of ₲714 after ₲120 bait and ₲6 ice. These are illustrative cargo budgets, not measured trip yields or a day-income model. Existing scene rods with no bait configured currently spend none; [pacing §7.9](../design/m1-progression-pacing.md#79-enabled-policy--an-illustrative-dory-day) states that limitation. Therefore no claimed “pays back in X days” is justified here.

### 4.2 Boat/gear wholesaler price proposals

A modest basic sounder remains attainable after the first boat. More capable navigation competes with the outboard/punt saving goals; convenience automation belongs against workboat money. Factory-fitted equipment must not be charged twice.

| Item | Current main ₲ | Proposed ₲ (owner proposal) | Reason against income / progression |
|---|---:|---:|---|
| Depth sounder | 140 | 140 | Ten nominal cod; keep a first-boat instrument attainable, below the ₲700 bought/repaired dory. |
| Fish finder | 480 | 650 | 46.4 nominal cod, 3.7% of the draft ₲17,400 Cape; a meaningful upgrade over depth without approaching workboat cost. |
| Loran-C receiver | 380 placeholder | 900 | 64.3 nominal cod, 5.2% of the Cape; move beyond a casual early purchase while staying below the richer chartplotter. |
| Chartplotter | 650 | 1,800 | Equal to the current punt price and 10.3% of the Cape; convenient position plotting becomes a deliberate workboat saving choice. |
| Dome compass | — | 80 | 5.7 nominal cod; affordable basic navigation, only for an otherwise bare compatible helm. |
| Flush compass | — | 180 | 12.9 nominal cod; upgraded fitted format without another early boat-sized gate. |
| Radar | — | 3,000 | 17.2% of the Cape; fog capability competes with savings toward a hire/storage at stage 2. |
| Wind dial | — | 220 | 15.7 nominal cod; useful weather reading fits between a basic compass and the more capable display. |
| Wind display | — | 600 | 42.9 nominal cod; improved wind information is a saving choice but below plotted navigation. |
| Autopilot | — | 6,000 candidate B | 34.5% of the Cape / 29.7% of the draft ₲20,200 lobster boat; an expensive convenience after a workboat is earned. |

#### Autopilot: three candidates, none approved

The comparison boats use #1271's **draft** prices, not an established shipped mid-game income balance. The price is a per-hull purchase; no transferable fleet-wide unlock or eligibility exception is proposed.

| Candidate | Proposed price ₲ | % of ₲17,400 Cape | % of ₲20,200 lobster boat | Nominal ₲14 cod equivalents | Consequence |
|---|---:|---:|---:|---:|---|
| A | 3,500 | 20.1% | 17.3% | 250 | Expensive relative to ordinary instruments, but more reachable soon after the first workboat. |
| **B (recommendation)** | **6,000** | **34.5%** | **29.7%** | **428.6** | Strong optional saving target within stage 2; costs several nominal full-workboat cargoes even before expenses. |
| C | 9,000 | 51.7% | 44.6% | 642.9 | Delays convenience toward the top of the workboat band and competes strongly with first storage/hire. |

At the illustrative ₲714 Cape remainder these prices correspond to about **4.9 / 8.4 / 12.6 full-load equivalents**, before the omitted costs; these are arithmetic equivalents, not promised profitable trips. There is no expectation that the player funds this from the dory's early income. A flat price is the simplest first offer; whether later fleet sizes need separate prices is a future economy decision, not hidden scaling here.

### 4.3 Fishing-gear wholesaler price proposals

Existing basic gear and recurring bait stay at their current values while the counter changes. This preserves the opening's saving ladder and follows [pacing §7.9](../design/m1-progression-pacing.md#79-enabled-policy--an-illustrative-dory-day)'s recommendation to observe outings before compensating for the bait-loss policy with price changes. New tackle/tub prices are conditional design seeds, not a request to ship those systems now.

| Item | Current main ₲ | Proposed ₲ (owner proposal) | Reason against income / progression |
|---|---:|---:|---|
| Rod | 60 | 60 | Preserve the day-2–3 target; 30 nominal clams before outlet deductions. |
| Shovel | 25 | 25 | Replacement costs 12.5 nominal clams; retain the starting gift so digging is never purchase-gated. |
| Clam bucket | 15 | 15 | 7.5 nominal clams; starter handling stays inexpensive. |
| Rod rack | 25 | 25 | Below one rod, 1.8 nominal cod; storage convenience should not rival a propulsion upgrade. |
| Shellfish rake | 30 | 30 | Fifteen nominal clams; hold off sale until its gathering action is real. |
| Oyster tongs | 45 | 45 | Below the rod; specialised gathering stays an early optional purchase, not a boat-scale gate. |
| Mussel sack | 10 | 10 | Five nominal clams; a low-cost handling container, capability still gated. |
| Shellfish basket | 20 | 20 | Ten nominal clams; above sack capacity/convenience without displacing boat saving. |
| Capelin bait | 2/unit; 20/ten | 2/unit; 20/ten | One nominal clam per bait; preserve the current cheap recurring option. |
| Fish scrap bait | 1/unit; 10/ten | 1/unit; 10/ten | Half a nominal clam; keep a cheap pot-bait option. |
| Herring bait | 3/unit; 30/ten | 3/unit; 30/ten | A ten-pack costs less than one nominal lobster; affordable stocking of lobster pots. |
| Mackerel bait | 4/unit; 40/ten | 4/unit; 40/ten | A ten-pack is four nominal mackerel; do not change unit/lot accounting during migration. |
| Sea worm bait | 4/unit; 40/ten | 4/unit; 40/ten | Retain the differentiated bait cost; 2.9 nominal cod fund a lot before costs. |
| Shucked clam bait | 3/unit; 30/ten | 3/unit; 30/ten | Keep existing spend and the cheap-versus-targeted bait choice. |
| Squid strip bait | 6/unit; 60/ten | 6/unit; 60/ten | Premium lot costs one rod; requires worthwhile target catch and should be observed before retuning. |
| Lobster pot | 120 | 120 | 4.3 nominal lobsters at ₲28 each before bait/pressure; preserve counted expansion around the pot rung. |
| Crab pot | 60 | 60 | Fifteen nominal rock crab at ₲4; preserve price but review species mix/payback before claiming parity with lobster pots. |
| Cod jig | — | 18 | Below one third of a rod; a specialist choice can be saved for without another multi-day boat gate. |
| Mackerel feathers | — | 16 | 1.6 nominal mackerel; affordable schooling-fish presentation. |
| Soft bait | — | 12 | Less than one nominal cod; an accessible alternative once paid tackle selection exists. |
| Spinner | — | 14 | One nominal cod; modest general-purpose tackle step. |
| Spoon | — | 10 | One nominal mackerel; cheapest entry lure rather than a second rod-sized barrier. |
| Groundfish longline tub (ten hooks) | — | 120 | Two rods / one lobster pot per tub; three-tub set costs ₲360 before bait, below the outboard and after basic fishing. |

The five proposed tackle purchases total ₲70, slightly more than the ₲60 rod. Approving paid tackle must include a usable starter presentation and preservation of existing ownership; it must not remove every working lure from a player who could fish before. The longline price is per counted tub, not per hook and not an unlimited unlock. Spare rope/hooks are bundled until separately modelled. None of the three unreviewed trawl prototypes is priced for sale.

The economy document's older “about two good hauls” pot rule is a tuning intention, not proof that these exact crab/lobster species mixes currently pay back together. Retaining pot prices is the narrow migration proposal; the owner can request a separate soak/yield and running-market projection before accepting either as balanced.

### 4.4 Prices outside the wholesalers: owner proposals to retain

These prices remain with their existing trade. For a new #1271 offer, “draft” distinguishes its proposed price from main; retaining it here does not validate its income model. No new prices are invented for the 27 unsold hulls in §1.3: regional yards and later brokers need their own approved progression scope first.

| Item | Main price ₲ | Draft price ₲ (where new) | Proposed ₲ (owner proposal) | Reason against income / progression |
|---|---:|---:|---:|---|
| `license.clam` | 15 | — | 15 | Fronted opening fee; retaining ₲15 preserves the licence-grant guard. |
| `license.cod` | 60 (was 120; first-week price pass) | — | 60 | Preserve the day-5 access gate and include it in any nominal cod-income comparison. |
| `boat.dory` | 400 + 300 repair | — | 400 + 300 repair | Preserve the ₲700 buy-and-repair saving beat around days 6–9. |
| `boat.dory_outboard` | 900 | — | 900 | Keep the day-13–15 target below the ₲1,800 replacement punt. |
| `boat.punt` | 1800 | — | 1800 | Retain the first engine-boat ladder above the dory; no seller-move surcharge. |
| `supply.bread` | 4 | — | 4 | Small recurring food cost, below one nominal mackerel; retain opening stamina budget. |
| `supply.ice` | 6 | — | 6 | Recurring freshness cost; six coin per bag buys time rather than a free income multiplier. |
| `supply.oatcakes` | 2 | — | 2 | Small recurring food cost, below one nominal mackerel; retain opening stamina budget. |
| `supply.tinned_beans` | 3 | — | 3 | Small recurring food cost, below one nominal mackerel; retain opening stamina budget. |
| `vehicle.dually_3500` | 7500 | — | 7500 | Transport purchase competes with boat/business capital; retain dealer seed pending usable delivery routes. |
| `vehicle.enduro_250` | 900 | — | 900 | Transport purchase competes with boat/business capital; retain dealer seed pending usable delivery routes. |
| `vehicle.hightop_van` | 4000 | — | 4000 | Transport purchase competes with boat/business capital; retain dealer seed pending usable delivery routes. |
| `vehicle.modern_2500` | 5500 | — | 5500 | Transport purchase competes with boat/business capital; retain dealer seed pending usable delivery routes. |
| `vehicle.modern_3500` | 7000 | — | 7000 | Transport purchase competes with boat/business capital; retain dealer seed pending usable delivery routes. |
| `vehicle.modern_350` | 3500 | — | 3500 | Transport purchase competes with boat/business capital; retain dealer seed pending usable delivery routes. |
| `vehicle.trike_200` | 800 | — | 800 | Transport purchase competes with boat/business capital; retain dealer seed pending usable delivery routes. |
| `vehicle.utility_quad` | 1200 | — | 1200 | Transport purchase competes with boat/business capital; retain dealer seed pending usable delivery routes. |
| `vehicle.aero_semi` | 25000 | — | 25000 | Transport purchase competes with boat/business capital; retain dealer seed pending usable delivery routes. |
| `vehicle.cabover_box` | 5000 + 4000 repair | — | 5000 + 4000 repair | Transport purchase competes with boat/business capital; retain dealer seed pending usable delivery routes. |
| `vehicle.classic_semi` | 25000 | — | 25000 | Transport purchase competes with boat/business capital; retain dealer seed pending usable delivery routes. |
| `vehicle.conv_box` | 12000 | — | 12000 | Transport purchase competes with boat/business capital; retain dealer seed pending usable delivery routes. |
| `boat.cape_islander` | — | 17400 | 17400 | Draft price sits in the stage-2 ₲8k–40k workboat band; preserve as an unvalidated seed. |
| `boat.console_skiff` | — | 9700 | 9700 | Draft skiff price is near the top of first-boat/gear money; defer balance to hull review. |
| `boat.lobster_boat` | — | 20200 | 20200 | Draft price sits in the stage-2 ₲8k–40k workboat band; preserve as an unvalidated seed. |
| `boat.lobster_inshore_hardtop_northumberland` | — | 7200 | 7200 | Draft skiff price is near the top of first-boat/gear money; defer balance to hull review. |
| `boat.lobster_inshore_open_northumberland` | — | 7200 | 7200 | Draft skiff price is near the top of first-boat/gear money; defer balance to hull review. |
| `boat.lobster_offshore_hardtop_northumberland` | — | 37200 | 37200 | Draft price sits in the stage-2 ₲8k–40k workboat band; preserve as an unvalidated seed. |
| `boat.lobster_offshore_open_northumberland` | — | 37200 | 37200 | Draft price sits in the stage-2 ₲8k–40k workboat band; preserve as an unvalidated seed. |
| `boat.lobster_standard_hardtop_northumberland` | — | 20200 | 20200 | Draft price sits in the stage-2 ₲8k–40k workboat band; preserve as an unvalidated seed. |
| `boat.lobster_standard_open_northumberland` | — | 20200 | 20200 | Draft price sits in the stage-2 ₲8k–40k workboat band; preserve as an unvalidated seed. |
| `boat.punt_upgraded` | — | 2300 | 2300 | Retain the first engine-boat ladder above the dory; no seller-move surcharge. |
| `boat.side_dragger` | — | 216000 | 216000 | Draft ₲216k sits in the stage-4 offshore ₲150k–600k band; needs its own balance proof. |
| `boat.sport_skiff_mk2` | — | 9300 | 9300 | Draft skiff price is near the top of first-boat/gear money; defer balance to hull review. |
| `boat.sport_skiff` | — | 8200 | 8200 | Draft skiff price is near the top of first-boat/gear money; defer balance to hull review. |
| `boat.sport_skiff_twin` | — | 8400 | 8400 | Draft skiff price is near the top of first-boat/gear money; defer balance to hull review. |
| `boat.zodiac_frc` | — | 8200 | 8200 | Draft skiff price is near the top of first-boat/gear money; defer balance to hull review. |
| `boat.zodiac_hurricane` | — | 10300 | 10300 | Draft skiff price is near the top of first-boat/gear money; defer balance to hull review. |
| `vehicle.otter_8x8` | — | 2500 | 2500 | Transport purchase competes with boat/business capital; retain dealer seed pending usable delivery routes. |

## 5. Owner decision page

The two separate wholesalers, the yard's hull/engine role, manual goods transport and the non-outboard autopilot eligibility rule are the given direction. The choices below settle prices and delivery detail; no implementation is approved by this document.

| Decision | Choices for the owner |
|---|---|
| Electronics prices | **Recommended: §4.2 proposals** (sounder 140, finder 650, Loran 900, plotter 1,800; new compass/radar/wind prices as tabled); or retain the four current placeholder prices and specify the missing product prices individually. |
| Expensive autopilot | A **₲3,500**; **B ₲6,000 recommended**; C **₲9,000**. All per eligible hull, sold only once paid ownership and installation replace the draft's outright fitment. |
| Island overlap and ice | **Recommended: keep rod/capelin/provisions and the proposed basic replacement/bait sellers on the island; full NMC fishing range**. Alternatively retain only today's island listings and defer additional island stock. Separately choose an extra fishing-wholesaler ice outlet, or island ice only. Electronics move to boat/gear in either choice. |
| Fishing prices and paid tackle/tubs | **Recommended: retain all 17 existing prices and keep missing/gated products unavailable**; approve the six tackle/tub price seeds for a later purchase slice, or request new prices before that slice. Keep a working starter lure if paid tackle is scheduled. For pots, retain 120/60 for migration, or request a species/soak/payback projection first. |
| Meaning of wholesale | **Recommended first scope: full-range catalogue at the tabled unit prices and bait lots**; or commission a separate finite-stock, bulk-quantity/discount and equipment-buyback design before promising those features. |
| Keepers and sites | **Recommended: census the two neighbourhoods proposed in #1276 and allocate two distinct seller identities**. Choose existing shopkeepers moving into those businesses, or new keeper characters; choose different lots if the census rules the proposed ones out. The buildings remain separate in all cases. |
| Goods delivery and fitting | **Recommended: counter pickup, manual carry to storage/boat, then compatible-hull installation**; or a local installation service after the player hand-delivers the goods to its loading point. Both require explicit target-boat selection, no double charge for factory fittings and a physical goods record; neither permits instant remote fitment. |
