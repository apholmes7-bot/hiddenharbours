# Wharf pass 2 fleet comparison

Measured against d0488e18. Values are metres, in LOA / beam / draught order. LOA and draught come from BoatHullDef. Beam is the gameplay footprint: twice `Visual.HullMesh.WatertightHalfBeamMeters`; BoatHullDef has no beam member. It is not necessarily the artist's maximum hull breadth. Kit values come from `WharfGeo2.HULLS`. No Def or kit value is changed. This is an audit, not a production mapping reader.

| Kit hull | Boat id | Kit L / B / D | Game L / B / D | Differences |
|---|---|---|---|---|
| otter | vehicle | 3.1 / 1.56 / 0.52 | — | Owner decision |
| dory | `boat.dory` | 4.5 / 1.5 / 0.26 | 4.5 / 1.7 / 0.3 | beam 1.5 → 1.7; draft 0.26 → 0.3 |
| dory | `boat.dory_outboard` | 4.5 / 1.5 / 0.26 | 4.5 / 1.7 / 0.3 | beam 1.5 → 1.7; draft 0.26 → 0.3 |
| punt | `boat.punt` | 5.2 / 1.58 / 0.28 | 5.2 / 1.8 / 0.5 | beam 1.58 → 1.8; draft 0.28 → 0.5 |
| punt | `boat.punt_upgraded` | 5.2 / 1.58 / 0.28 | 5.2 / 1.8 / 0.55 | beam 1.58 → 1.8; draft 0.28 → 0.55 |
| bowrider | no boat id | 5.64 / 2.29 / 0.4 | — | Owner decision |
| frc | `boat.zodiac_frc` | 6.85 / 2.5 / 0.4 | 6.66 / 3.04 / 0.8 | loa 6.85 → 6.66; beam 2.5 → 3.04; draft 0.4 → 0.8 |
| skiff | `boat.console_skiff` | 7 / 2.3 / 0.42 | 7 / 2.5 / 0.55 | beam 2.3 → 2.5; draft 0.42 → 0.55 |
| sportSkiff | `boat.sport_skiff` | 7 / 2.54 / 0.45 | 7 / 2.5 / 0.5 | beam 2.54 → 2.5; draft 0.45 → 0.5 |
| sportSkiff | `boat.sport_skiff_mk2` | 7 / 2.54 / 0.45 | 7 / 2.76 / 0.95 | beam 2.54 → 2.76; draft 0.45 → 0.95 |
| sportSkiff | `boat.sport_skiff_twin` | 7 / 2.54 / 0.45 | 7 / 2.5 / 0.55 | beam 2.54 → 2.5; draft 0.45 → 0.55 |
| hurricane | `boat.zodiac_hurricane` | 7.47 / 2.6 / 0.45 | 7.28 / 3.22 / 0.85 | loa 7.47 → 7.28; beam 2.6 → 3.22; draft 0.45 → 0.85 |
| lobsterIn | `boat.lobster_inshore_hardtop_fundy` | 8.6 / 3.55 / 0.95 | 8.6 / 3.9 / 1.15 | beam 3.55 → 3.9; draft 0.95 → 1.15 |
| lobsterIn | `boat.lobster_inshore_hardtop_newfoundland` | 8.6 / 3.55 / 0.95 | 8.6 / 4.08 / 1.15 | beam 3.55 → 4.08; draft 0.95 → 1.15 |
| lobsterIn | `boat.lobster_inshore_hardtop_northumberland` | 8.6 / 3.55 / 0.95 | 8.6 / 4 / 1.15 | beam 3.55 → 4; draft 0.95 → 1.15 |
| lobsterIn | `boat.lobster_inshore_open_fundy` | 8.6 / 3.55 / 0.95 | 8.6 / 3.9 / 1.15 | beam 3.55 → 3.9; draft 0.95 → 1.15 |
| lobsterIn | `boat.lobster_inshore_open_newfoundland` | 8.6 / 3.55 / 0.95 | 8.6 / 4.08 / 1.15 | beam 3.55 → 4.08; draft 0.95 → 1.15 |
| lobsterIn | `boat.lobster_inshore_open_northumberland` | 8.6 / 3.55 / 0.95 | 8.6 / 4 / 1.15 | beam 3.55 → 4; draft 0.95 → 1.15 |
| sloop30 | `boat.sloop_30` | 9.4 / 2.98 / 1.9 | 9.4 / 3 / 1.9 | beam 2.98 → 3 |
| goFast | no boat id | 11.9 / 2.5 / 0.8 | — | Owner decision |
| lobster | `boat.lobster_boat` | 12 / 4.44 / 1.35 | 12 / 5 / 1.3 | beam 4.44 → 5; draft 1.35 → 1.3 |
| lobster | `boat.lobster_standard_hardtop_fundy` | 12 / 4.44 / 1.35 | 12 / 4.86 / 1.3 | beam 4.44 → 4.86; draft 1.35 → 1.3 |
| lobster | `boat.lobster_standard_hardtop_newfoundland` | 12 / 4.44 / 1.35 | 12 / 5.1 / 1.3 | beam 4.44 → 5.1; draft 1.35 → 1.3 |
| lobster | `boat.lobster_standard_hardtop_northumberland` | 12 / 4.44 / 1.35 | 12 / 5 / 1.3 | beam 4.44 → 5; draft 1.35 → 1.3 |
| lobster | `boat.lobster_standard_open_fundy` | 12 / 4.44 / 1.35 | 12 / 4.86 / 1.3 | beam 4.44 → 4.86; draft 1.35 → 1.3 |
| lobster | `boat.lobster_standard_open_newfoundland` | 12 / 4.44 / 1.35 | 12 / 5.1 / 1.3 | beam 4.44 → 5.1; draft 1.35 → 1.3 |
| lobster | `boat.lobster_standard_open_northumberland` | 12 / 4.44 / 1.35 | 12 / 5 / 1.3 | beam 4.44 → 5; draft 1.35 → 1.3 |
| cape | `boat.cape_islander` | 12.8 / 4.4 / 1.4 | 12.9 / 4.8 / 1.4 | loa 12.8 → 12.9; beam 4.4 → 4.8 |
| lobsterOff | `boat.lobster_offshore_hardtop_fundy` | 14.6 / 5.05 / 1.55 | 14.6 / 5.54 / 1.45 | beam 5.05 → 5.54; draft 1.55 → 1.45 |
| lobsterOff | `boat.lobster_offshore_hardtop_newfoundland` | 14.6 / 5.05 / 1.55 | 14.6 / 5.8 / 1.45 | beam 5.05 → 5.8; draft 1.55 → 1.45 |
| lobsterOff | `boat.lobster_offshore_hardtop_northumberland` | 14.6 / 5.05 / 1.55 | 14.6 / 5.7 / 1.45 | beam 5.05 → 5.7; draft 1.55 → 1.45 |
| lobsterOff | `boat.lobster_offshore_open_fundy` | 14.6 / 5.05 / 1.55 | 14.6 / 5.54 / 1.45 | beam 5.05 → 5.54; draft 1.55 → 1.45 |
| lobsterOff | `boat.lobster_offshore_open_newfoundland` | 14.6 / 5.05 / 1.55 | 14.6 / 5.8 / 1.45 | beam 5.05 → 5.8; draft 1.55 → 1.45 |
| lobsterOff | `boat.lobster_offshore_open_northumberland` | 14.6 / 5.05 / 1.55 | 14.6 / 5.7 / 1.45 | beam 5.05 → 5.7; draft 1.55 → 1.45 |
| sportFisher | `boat.sport_fisher_convertible` | 16.2 / 5.16 / 1.4 | 16.2 / 5.82 / 1.75 | beam 5.16 → 5.82; draft 1.4 → 1.75 |
| dragger | `boat.side_dragger` | 25 / 7 / 2.55 | 25 / 7 / 2.9 | draft 2.55 → 2.9 |
| sloop88 | `boat.sloop_88` | 27 / 6.7 / 4.6 | 27 / 6.8 / 4.6 | beam 6.7 → 6.8 |
| skybridge | `boat.sport_fisher_skybridge` | 27.4 / 7.32 / 1.9 | 27.4 / 8.24 / 2.95 | beam 7.32 → 8.24; draft 1.9 → 2.95 |
| trawler | `boat.stern_trawler` | 38 / 9 / 3.9 | 38 / 9.4 / 4.2 | beam 9 → 9.4; draft 3.9 → 4.2 |
| trawlerMk2 | `boat.stern_trawler_mk2` | 38 / 9 / 3.9 | 38 / 9.4 / 4.3 | beam 9 → 9.4; draft 3.9 → 4.3 |
| packet | `boat.coastal_packet` | 60 / 10.4 / 4.2 | 60 / 11 / 5 | beam 10.4 → 11; draft 4.2 → 5 |
| tanker | `boat.tanker` | 110 / 17.4 / 6.2 | 110 / 18 / 6.5 | beam 17.4 → 18; draft 6.2 → 6.5 |

All 39 committed boat ids are mapped exactly once through 19 kit categories. All 39 differ in at least one of these dimensions. The two boat-less categories are bowrider and goFast; Otter is a vehicle.

The outboard dory matches its base hull. The upgraded punt has a deeper draught (0.55 vs 0.50); the twin sport skiff also draws 0.55 vs 0.50; the Mk II sport skiff has a wider footprint and deeper draught (2.76 / 0.95 vs 2.50 / 0.50). They must not blindly inherit identical berth limits.

## Source paths

- `boat.dory`: [BoatHullDef](../../Assets/_Project/Data/Boats/Dory.asset), [beam source](../../Assets/_Project/Data/Boats/HullMeshes/DoryIsoHullMesh.asset).
- `boat.dory_outboard`: [BoatHullDef](../../Assets/_Project/Data/Boats/DoryOutboard.asset), [beam source](../../Assets/_Project/Data/Boats/HullMeshes/DoryIsoHullMesh.asset).
- `boat.punt`: [BoatHullDef](../../Assets/_Project/Data/Boats/Punt.asset), [beam source](../../Assets/_Project/Data/Boats/HullMeshes/PuntIsoHullMesh.asset).
- `boat.punt_upgraded`: [BoatHullDef](../../Assets/_Project/Data/Boats/PuntUpgraded.asset), [beam source](../../Assets/_Project/Data/Boats/HullMeshes/PuntIsoHullMesh.asset).
- `boat.zodiac_frc`: [BoatHullDef](../../Assets/_Project/Data/Boats/ZodiacFrc.asset), [beam source](../../Assets/_Project/Data/Boats/HullMeshes/ZodiacFrcIsoHullMesh.asset).
- `boat.console_skiff`: [BoatHullDef](../../Assets/_Project/Data/Boats/ConsoleSkiff.asset), [beam source](../../Assets/_Project/Data/Boats/HullMeshes/ConsoleIsoHullMesh.asset).
- `boat.sport_skiff`: [BoatHullDef](../../Assets/_Project/Data/Boats/SportSkiff.asset), [beam source](../../Assets/_Project/Data/Boats/HullMeshes/SportSkiffIsoHullMesh.asset).
- `boat.sport_skiff_mk2`: [BoatHullDef](../../Assets/_Project/Data/Boats/SportSkiffMk2.asset), [beam source](../../Assets/_Project/Data/Boats/HullMeshes/SportSkiffMk2IsoHullMesh.asset).
- `boat.sport_skiff_twin`: [BoatHullDef](../../Assets/_Project/Data/Boats/SportSkiffTwin.asset), [beam source](../../Assets/_Project/Data/Boats/HullMeshes/SportSkiffIsoHullMesh.asset).
- `boat.zodiac_hurricane`: [BoatHullDef](../../Assets/_Project/Data/Boats/ZodiacHurricane.asset), [beam source](../../Assets/_Project/Data/Boats/HullMeshes/ZodiacHurricaneIsoHullMesh.asset).
- `boat.lobster_inshore_hardtop_fundy`: [BoatHullDef](../../Assets/_Project/Data/Boats/LobsterInshoreHardtopFundy.asset), [beam source](../../Assets/_Project/Data/Boats/HullMeshes/LobsterInshoreHardtopFundyIsoHullMesh.asset).
- `boat.lobster_inshore_hardtop_newfoundland`: [BoatHullDef](../../Assets/_Project/Data/Boats/LobsterInshoreHardtopNewfoundland.asset), [beam source](../../Assets/_Project/Data/Boats/HullMeshes/LobsterInshoreHardtopNewfoundlandIsoHullMesh.asset).
- `boat.lobster_inshore_hardtop_northumberland`: [BoatHullDef](../../Assets/_Project/Data/Boats/LobsterInshoreHardtopNorthumberland.asset), [beam source](../../Assets/_Project/Data/Boats/HullMeshes/LobsterInshoreHardtopNorthumberlandIsoHullMesh.asset).
- `boat.lobster_inshore_open_fundy`: [BoatHullDef](../../Assets/_Project/Data/Boats/LobsterInshoreOpenFundy.asset), [beam source](../../Assets/_Project/Data/Boats/HullMeshes/LobsterInshoreOpenFundyIsoHullMesh.asset).
- `boat.lobster_inshore_open_newfoundland`: [BoatHullDef](../../Assets/_Project/Data/Boats/LobsterInshoreOpenNewfoundland.asset), [beam source](../../Assets/_Project/Data/Boats/HullMeshes/LobsterInshoreOpenNewfoundlandIsoHullMesh.asset).
- `boat.lobster_inshore_open_northumberland`: [BoatHullDef](../../Assets/_Project/Data/Boats/LobsterInshoreOpenNorthumberland.asset), [beam source](../../Assets/_Project/Data/Boats/HullMeshes/LobsterInshoreOpenNorthumberlandIsoHullMesh.asset).
- `boat.sloop_30`: [BoatHullDef](../../Assets/_Project/Data/Boats/Sloop30.asset), [beam source](../../Assets/_Project/Data/Boats/HullMeshes/SloopIsoHullMesh.asset).
- `boat.lobster_boat`: [BoatHullDef](../../Assets/_Project/Data/Boats/LobsterBoat.asset), [beam source](../../Assets/_Project/Data/Boats/HullMeshes/LobsterBoatIsoHullMesh.asset).
- `boat.lobster_standard_hardtop_fundy`: [BoatHullDef](../../Assets/_Project/Data/Boats/LobsterStandardHardtopFundy.asset), [beam source](../../Assets/_Project/Data/Boats/HullMeshes/LobsterStandardHardtopFundyIsoHullMesh.asset).
- `boat.lobster_standard_hardtop_newfoundland`: [BoatHullDef](../../Assets/_Project/Data/Boats/LobsterStandardHardtopNewfoundland.asset), [beam source](../../Assets/_Project/Data/Boats/HullMeshes/LobsterStandardHardtopNewfoundlandIsoHullMesh.asset).
- `boat.lobster_standard_hardtop_northumberland`: [BoatHullDef](../../Assets/_Project/Data/Boats/LobsterStandardHardtopNorthumberland.asset), [beam source](../../Assets/_Project/Data/Boats/HullMeshes/LobsterStandardHardtopNorthumberlandIsoHullMesh.asset).
- `boat.lobster_standard_open_fundy`: [BoatHullDef](../../Assets/_Project/Data/Boats/LobsterStandardOpenFundy.asset), [beam source](../../Assets/_Project/Data/Boats/HullMeshes/LobsterStandardOpenFundyIsoHullMesh.asset).
- `boat.lobster_standard_open_newfoundland`: [BoatHullDef](../../Assets/_Project/Data/Boats/LobsterStandardOpenNewfoundland.asset), [beam source](../../Assets/_Project/Data/Boats/HullMeshes/LobsterStandardOpenNewfoundlandIsoHullMesh.asset).
- `boat.lobster_standard_open_northumberland`: [BoatHullDef](../../Assets/_Project/Data/Boats/LobsterStandardOpenNorthumberland.asset), [beam source](../../Assets/_Project/Data/Boats/HullMeshes/LobsterStandardOpenNorthumberlandIsoHullMesh.asset).
- `boat.cape_islander`: [BoatHullDef](../../Assets/_Project/Data/Boats/CapeIslander.asset), [beam source](../../Assets/_Project/Data/Boats/HullMeshes/CapeIslanderIsoHullMesh.asset).
- `boat.lobster_offshore_hardtop_fundy`: [BoatHullDef](../../Assets/_Project/Data/Boats/LobsterOffshoreHardtopFundy.asset), [beam source](../../Assets/_Project/Data/Boats/HullMeshes/LobsterOffshoreHardtopFundyIsoHullMesh.asset).
- `boat.lobster_offshore_hardtop_newfoundland`: [BoatHullDef](../../Assets/_Project/Data/Boats/LobsterOffshoreHardtopNewfoundland.asset), [beam source](../../Assets/_Project/Data/Boats/HullMeshes/LobsterOffshoreHardtopNewfoundlandIsoHullMesh.asset).
- `boat.lobster_offshore_hardtop_northumberland`: [BoatHullDef](../../Assets/_Project/Data/Boats/LobsterOffshoreHardtopNorthumberland.asset), [beam source](../../Assets/_Project/Data/Boats/HullMeshes/LobsterOffshoreHardtopNorthumberlandIsoHullMesh.asset).
- `boat.lobster_offshore_open_fundy`: [BoatHullDef](../../Assets/_Project/Data/Boats/LobsterOffshoreOpenFundy.asset), [beam source](../../Assets/_Project/Data/Boats/HullMeshes/LobsterOffshoreOpenFundyIsoHullMesh.asset).
- `boat.lobster_offshore_open_newfoundland`: [BoatHullDef](../../Assets/_Project/Data/Boats/LobsterOffshoreOpenNewfoundland.asset), [beam source](../../Assets/_Project/Data/Boats/HullMeshes/LobsterOffshoreOpenNewfoundlandIsoHullMesh.asset).
- `boat.lobster_offshore_open_northumberland`: [BoatHullDef](../../Assets/_Project/Data/Boats/LobsterOffshoreOpenNorthumberland.asset), [beam source](../../Assets/_Project/Data/Boats/HullMeshes/LobsterOffshoreOpenNorthumberlandIsoHullMesh.asset).
- `boat.sport_fisher_convertible`: [BoatHullDef](../../Assets/_Project/Data/Boats/SportFisherConvertible.asset), [beam source](../../Assets/_Project/Data/Boats/HullMeshes/SportFisherConvertibleIsoHullMesh.asset).
- `boat.side_dragger`: [BoatHullDef](../../Assets/_Project/Data/Boats/SideDragger.asset), [beam source](../../Assets/_Project/Data/Boats/HullMeshes/SideDraggerIsoHullMesh.asset).
- `boat.sloop_88`: [BoatHullDef](../../Assets/_Project/Data/Boats/Sloop88.asset), [beam source](../../Assets/_Project/Data/Boats/HullMeshes/Sloop88IsoHullMesh.asset).
- `boat.sport_fisher_skybridge`: [BoatHullDef](../../Assets/_Project/Data/Boats/SportFisherSkybridge.asset), [beam source](../../Assets/_Project/Data/Boats/HullMeshes/SportFisherSkybridgeIsoHullMesh.asset).
- `boat.stern_trawler`: [BoatHullDef](../../Assets/_Project/Data/Boats/SternTrawler.asset), [beam source](../../Assets/_Project/Data/Boats/HullMeshes/SternTrawlerIsoHullMesh.asset).
- `boat.stern_trawler_mk2`: [BoatHullDef](../../Assets/_Project/Data/Boats/SternTrawlerMk2.asset), [beam source](../../Assets/_Project/Data/Boats/HullMeshes/SternTrawlerMk2IsoHullMesh.asset).
- `boat.coastal_packet`: [BoatHullDef](../../Assets/_Project/Data/Boats/CoastalPacket.asset), [beam source](../../Assets/_Project/Data/Boats/HullMeshes/CoastalPacketIsoHullMesh.asset).
- `boat.tanker`: [BoatHullDef](../../Assets/_Project/Data/Boats/Tanker.asset), [beam source](../../Assets/_Project/Data/Boats/HullMeshes/TankerIsoHullMesh.asset).
