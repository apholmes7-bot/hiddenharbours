# Outdoor prop sprite audit

Base: `1abca5974ce3d16a3257777797bb2547c9ad8d53`.

Scope: loose outdoor props, yard and wharf furniture, utilities, fuel containers, signs, navigation buoys, deck traps, landing fittings and boats on stocks. Large buildings, wharf surfaces, terrain, height maps and indoor fixtures are excluded. Whole-sheet dimensions are reported; facing count comes from importer rectangles. Each PNG is one row.

Prefab column reports direct serialized texture GUID references across Assets. “None” means no direct prefab reference, not proof that a builder or data definition never loads it. This audit does not modify placement.

No source filenames in this scope contain placeholder, greybox, standin or temp. Weak flat-form candidates below are visual judgements, not provenance claims. Low colour count alone is not a placeholder: ropes, tyres and simple metal fittings can be intentionally restrained. Blank reverse faces of signs and buoy walls are not missing front artwork.

## Five weakest

| Rank | Source | Reason |
|---|---|---|
| 1 | `Crate.png` | Weak: planar rectangle; diagonal brace barely separates from planks. |
| 2 | `SetPiece_bench.png` | Weak: flat seat slab; little slat or joinery information. |
| 3 | `SetPiece_picnic.png` | Weak: nearly solid tabletop; no clear board joints. |
| 4 | `SetPiece_iceBox.png` | Weak: broad flat panels; handles and hinges missing. |
| 5 | `WharfPost.png` | Weak: straight shaded bar; minimal end grain and fastening. |

## Inventory

| File | Sheet px | Facings | Prefab uses | Assessment |
|---|---:|---:|---|---|
| `Assets/_Project/Art/Sprites/Barrel.png` | 32 × 32 | 1 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/Crate.png` | 32 × 32 | 1 | None (direct) | Weak: planar rectangle; diagonal brace barely separates from planks. |
| `Assets/_Project/Art/Sprites/DeckGear/Buoys/CapeIslander.png` | 80 × 32 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/DeckGear/Buoys/CoastalPacket.png` | 80 × 32 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/DeckGear/Buoys/Dory.png` | 80 × 32 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/DeckGear/Buoys/LobsterBoat.png` | 80 × 32 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/DeckGear/Buoys/Punt.png` | 80 × 32 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/DeckGear/Buoys/SideDragger.png` | 80 × 32 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/DeckGear/Buoys/SternTrawler.png` | 80 × 32 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/DeckGear/Buoys/Tanker.png` | 80 × 32 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/DeckGear/Traps/crabCone.png` | 248 × 41 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/DeckGear/Traps/crabWire.png` | 256 × 27 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/DeckGear/Traps/wire.png` | 336 × 35 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/DeckGear/Traps/wood.png` | 288 × 36 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/FuelStorage/Iso/bulk_s10k_diesel.png` | 536 × 645 | 20 | `Assets/_Project/Prefabs/FuelStorage/bulk_s10k_diesel.prefab` | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/FuelStorage/Iso/bulk_s10k_gas.png` | 536 × 645 | 20 | `Assets/_Project/Prefabs/FuelStorage/bulk_s10k_gas.prefab` | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/FuelStorage/Iso/bulk_s10k_mixed.png` | 536 × 645 | 20 | `Assets/_Project/Prefabs/FuelStorage/bulk_s10k_mixed.prefab` | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/FuelStorage/Iso/bulk_s10k_oil.png` | 536 × 645 | 20 | `Assets/_Project/Prefabs/FuelStorage/bulk_s10k_oil.prefab` | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/FuelStorage/Iso/bulk_s25k_diesel.png` | 664 × 865 | 20 | `Assets/_Project/Prefabs/FuelStorage/bulk_s25k_diesel.prefab` | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/FuelStorage/Iso/bulk_s25k_gas.png` | 664 × 865 | 20 | `Assets/_Project/Prefabs/FuelStorage/bulk_s25k_gas.prefab` | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/FuelStorage/Iso/bulk_s25k_mixed.png` | 664 × 865 | 20 | `Assets/_Project/Prefabs/FuelStorage/bulk_s25k_mixed.prefab` | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/FuelStorage/Iso/bulk_s25k_oil.png` | 664 × 865 | 20 | `Assets/_Project/Prefabs/FuelStorage/bulk_s25k_oil.prefab` | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/FuelStorage/Iso/bulk_s50k_diesel.png` | 744 × 1140 | 20 | `Assets/_Project/Prefabs/FuelStorage/bulk_s50k_diesel.prefab` | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/FuelStorage/Iso/bulk_s50k_gas.png` | 744 × 1140 | 20 | `Assets/_Project/Prefabs/FuelStorage/bulk_s50k_gas.prefab` | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/FuelStorage/Iso/bulk_s50k_mixed.png` | 744 × 1140 | 20 | `Assets/_Project/Prefabs/FuelStorage/bulk_s50k_mixed.prefab` | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/FuelStorage/Iso/bulk_s50k_oil.png` | 744 × 1140 | 20 | `Assets/_Project/Prefabs/FuelStorage/bulk_s50k_oil.prefab` | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/FuelStorage/Iso/drum_s205_diesel.png` | 96 × 170 | 20 | `Assets/_Project/Prefabs/FuelStorage/drum_s205_diesel.prefab` | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/FuelStorage/Iso/drum_s205_gas.png` | 96 × 170 | 20 | `Assets/_Project/Prefabs/FuelStorage/drum_s205_gas.prefab` | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/FuelStorage/Iso/drum_s205_mixed.png` | 96 × 170 | 20 | `Assets/_Project/Prefabs/FuelStorage/drum_s205_mixed.prefab` | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/FuelStorage/Iso/drum_s205_oil.png` | 96 × 170 | 20 | `Assets/_Project/Prefabs/FuelStorage/drum_s205_oil.prefab` | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/FuelStorage/Iso/drum_s60_diesel.png` | 72 × 125 | 20 | `Assets/_Project/Prefabs/FuelStorage/drum_s60_diesel.prefab` | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/FuelStorage/Iso/drum_s60_gas.png` | 72 × 125 | 20 | `Assets/_Project/Prefabs/FuelStorage/drum_s60_gas.prefab` | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/FuelStorage/Iso/drum_s60_mixed.png` | 72 × 125 | 20 | `Assets/_Project/Prefabs/FuelStorage/drum_s60_mixed.prefab` | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/FuelStorage/Iso/drum_s60_oil.png` | 72 × 125 | 20 | `Assets/_Project/Prefabs/FuelStorage/drum_s60_oil.prefab` | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/FuelStorage/Iso/jerry_s10_diesel.png` | 80 × 85 | 40 | `Assets/_Project/Prefabs/FuelStorage/jerry_s10_diesel.prefab` | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/FuelStorage/Iso/jerry_s10_gas.png` | 80 × 85 | 40 | `Assets/_Project/Prefabs/FuelStorage/jerry_s10_gas.prefab` | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/FuelStorage/Iso/jerry_s10_mixed.png` | 80 × 85 | 40 | `Assets/_Project/Prefabs/FuelStorage/jerry_s10_mixed.prefab` | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/FuelStorage/Iso/jerry_s10_oil.png` | 80 × 85 | 40 | `Assets/_Project/Prefabs/FuelStorage/jerry_s10_oil.prefab` | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/FuelStorage/Iso/jerry_s20_diesel.png` | 96 × 100 | 40 | `Assets/_Project/Prefabs/FuelStorage/jerry_s20_diesel.prefab` | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/FuelStorage/Iso/jerry_s20_gas.png` | 96 × 100 | 40 | `Assets/_Project/Prefabs/FuelStorage/jerry_s20_gas.prefab` | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/FuelStorage/Iso/jerry_s20_mixed.png` | 96 × 100 | 40 | `Assets/_Project/Prefabs/FuelStorage/jerry_s20_mixed.prefab` | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/FuelStorage/Iso/jerry_s20_oil.png` | 96 × 100 | 40 | `Assets/_Project/Prefabs/FuelStorage/jerry_s20_oil.prefab` | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/FuelStorage/Iso/jerry_s25_diesel.png` | 112 × 110 | 40 | `Assets/_Project/Prefabs/FuelStorage/jerry_s25_diesel.prefab` | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/FuelStorage/Iso/jerry_s25_gas.png` | 112 × 110 | 40 | `Assets/_Project/Prefabs/FuelStorage/jerry_s25_gas.prefab` | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/FuelStorage/Iso/jerry_s25_mixed.png` | 112 × 110 | 40 | `Assets/_Project/Prefabs/FuelStorage/jerry_s25_mixed.prefab` | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/FuelStorage/Iso/jerry_s25_oil.png` | 112 × 110 | 40 | `Assets/_Project/Prefabs/FuelStorage/jerry_s25_oil.prefab` | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/FuelStorage/Iso/jerry_s5_diesel.png` | 64 × 70 | 40 | `Assets/_Project/Prefabs/FuelStorage/jerry_s5_diesel.prefab` | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/FuelStorage/Iso/jerry_s5_gas.png` | 64 × 70 | 40 | `Assets/_Project/Prefabs/FuelStorage/jerry_s5_gas.prefab` | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/FuelStorage/Iso/jerry_s5_mixed.png` | 64 × 70 | 40 | `Assets/_Project/Prefabs/FuelStorage/jerry_s5_mixed.prefab` | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/FuelStorage/Iso/jerry_s5_oil.png` | 64 × 70 | 40 | `Assets/_Project/Prefabs/FuelStorage/jerry_s5_oil.prefab` | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/FuelStorage/Iso/jug_s10_diesel.png` | 80 × 65 | 40 | `Assets/_Project/Prefabs/FuelStorage/jug_s10_diesel.prefab` | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/FuelStorage/Iso/jug_s10_gas.png` | 80 × 65 | 40 | `Assets/_Project/Prefabs/FuelStorage/jug_s10_gas.prefab` | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/FuelStorage/Iso/jug_s10_mixed.png` | 80 × 65 | 40 | `Assets/_Project/Prefabs/FuelStorage/jug_s10_mixed.prefab` | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/FuelStorage/Iso/jug_s10_oil.png` | 80 × 65 | 40 | `Assets/_Project/Prefabs/FuelStorage/jug_s10_oil.prefab` | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/FuelStorage/Iso/jug_s1_diesel.png` | 32 × 35 | 40 | `Assets/_Project/Prefabs/FuelStorage/jug_s1_diesel.prefab` | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/FuelStorage/Iso/jug_s1_gas.png` | 32 × 35 | 40 | `Assets/_Project/Prefabs/FuelStorage/jug_s1_gas.prefab` | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/FuelStorage/Iso/jug_s1_mixed.png` | 32 × 35 | 40 | `Assets/_Project/Prefabs/FuelStorage/jug_s1_mixed.prefab` | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/FuelStorage/Iso/jug_s1_oil.png` | 32 × 35 | 40 | `Assets/_Project/Prefabs/FuelStorage/jug_s1_oil.prefab` | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/FuelStorage/Iso/jug_s4_diesel.png` | 64 × 50 | 40 | `Assets/_Project/Prefabs/FuelStorage/jug_s4_diesel.prefab` | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/FuelStorage/Iso/jug_s4_gas.png` | 64 × 50 | 40 | `Assets/_Project/Prefabs/FuelStorage/jug_s4_gas.prefab` | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/FuelStorage/Iso/jug_s4_mixed.png` | 64 × 50 | 40 | `Assets/_Project/Prefabs/FuelStorage/jug_s4_mixed.prefab` | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/FuelStorage/Iso/jug_s4_oil.png` | 64 × 50 | 40 | `Assets/_Project/Prefabs/FuelStorage/jug_s4_oil.prefab` | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/FuelStorage/Iso/nozzle_sAuto_diesel.png` | 96 × 9 | 8 | `Assets/_Project/Prefabs/FuelStorage/nozzle_sAuto_diesel.prefab` | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/FuelStorage/Iso/nozzle_sAuto_gas.png` | 96 × 9 | 8 | `Assets/_Project/Prefabs/FuelStorage/nozzle_sAuto_gas.prefab` | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/FuelStorage/Iso/nozzle_sAuto_mixed.png` | 96 × 9 | 8 | `Assets/_Project/Prefabs/FuelStorage/nozzle_sAuto_mixed.prefab` | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/FuelStorage/Iso/nozzle_sAuto_oil.png` | 96 × 9 | 8 | `Assets/_Project/Prefabs/FuelStorage/nozzle_sAuto_oil.prefab` | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/FuelStorage/Iso/nozzle_sHigh_diesel.png` | 128 × 11 | 8 | `Assets/_Project/Prefabs/FuelStorage/nozzle_sHigh_diesel.prefab` | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/FuelStorage/Iso/nozzle_sHigh_gas.png` | 128 × 11 | 8 | `Assets/_Project/Prefabs/FuelStorage/nozzle_sHigh_gas.prefab` | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/FuelStorage/Iso/nozzle_sHigh_mixed.png` | 128 × 11 | 8 | `Assets/_Project/Prefabs/FuelStorage/nozzle_sHigh_mixed.prefab` | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/FuelStorage/Iso/nozzle_sHigh_oil.png` | 128 × 11 | 8 | `Assets/_Project/Prefabs/FuelStorage/nozzle_sHigh_oil.prefab` | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/FuelStorage/Iso/pump_sSingle_diesel.png` | 112 × 50 | 4 | `Assets/_Project/Prefabs/FuelStorage/pump_sSingle_diesel.prefab` | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/FuelStorage/Iso/pump_sSingle_gas.png` | 112 × 50 | 4 | `Assets/_Project/Prefabs/FuelStorage/pump_sSingle_gas.prefab` | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/FuelStorage/Iso/pump_sSingle_mixed.png` | 112 × 50 | 4 | `Assets/_Project/Prefabs/FuelStorage/pump_sSingle_mixed.prefab` | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/FuelStorage/Iso/pump_sSingle_oil.png` | 112 × 50 | 4 | `Assets/_Project/Prefabs/FuelStorage/pump_sSingle_oil.prefab` | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/FuelStorage/Iso/pump_sTwin_diesel.png` | 128 × 55 | 4 | `Assets/_Project/Prefabs/FuelStorage/pump_sTwin_diesel.prefab` | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/FuelStorage/Iso/pump_sTwin_gas.png` | 128 × 55 | 4 | `Assets/_Project/Prefabs/FuelStorage/pump_sTwin_gas.prefab` | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/FuelStorage/Iso/pump_sTwin_mixed.png` | 128 × 55 | 4 | `Assets/_Project/Prefabs/FuelStorage/pump_sTwin_mixed.prefab` | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/FuelStorage/Iso/pump_sTwin_oil.png` | 128 × 55 | 4 | `Assets/_Project/Prefabs/FuelStorage/pump_sTwin_oil.prefab` | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/FuelStorage/Iso/skid_s1200_diesel.png` | 248 × 330 | 20 | `Assets/_Project/Prefabs/FuelStorage/skid_s1200_diesel.prefab` | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/FuelStorage/Iso/skid_s1200_gas.png` | 248 × 330 | 20 | `Assets/_Project/Prefabs/FuelStorage/skid_s1200_gas.prefab` | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/FuelStorage/Iso/skid_s1200_mixed.png` | 248 × 330 | 20 | `Assets/_Project/Prefabs/FuelStorage/skid_s1200_mixed.prefab` | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/FuelStorage/Iso/skid_s1200_oil.png` | 248 × 330 | 20 | `Assets/_Project/Prefabs/FuelStorage/skid_s1200_oil.prefab` | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/FuelStorage/Iso/skid_s2500_diesel.png` | 312 × 400 | 20 | `Assets/_Project/Prefabs/FuelStorage/skid_s2500_diesel.prefab` | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/FuelStorage/Iso/skid_s2500_gas.png` | 312 × 400 | 20 | `Assets/_Project/Prefabs/FuelStorage/skid_s2500_gas.prefab` | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/FuelStorage/Iso/skid_s2500_mixed.png` | 312 × 400 | 20 | `Assets/_Project/Prefabs/FuelStorage/skid_s2500_mixed.prefab` | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/FuelStorage/Iso/skid_s2500_oil.png` | 312 × 400 | 20 | `Assets/_Project/Prefabs/FuelStorage/skid_s2500_oil.prefab` | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/FuelStorage/Iso/skid_s500_diesel.png` | 192 × 255 | 20 | `Assets/_Project/Prefabs/FuelStorage/skid_s500_diesel.prefab` | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/FuelStorage/Iso/skid_s500_gas.png` | 192 × 255 | 20 | `Assets/_Project/Prefabs/FuelStorage/skid_s500_gas.prefab` | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/FuelStorage/Iso/skid_s500_mixed.png` | 192 × 255 | 20 | `Assets/_Project/Prefabs/FuelStorage/skid_s500_mixed.prefab` | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/FuelStorage/Iso/skid_s500_oil.png` | 192 × 255 | 20 | `Assets/_Project/Prefabs/FuelStorage/skid_s500_oil.prefab` | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/FuelStorage/Iso/tote_s1000_diesel.png` | 160 × 260 | 20 | `Assets/_Project/Prefabs/FuelStorage/tote_s1000_diesel.prefab` | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/FuelStorage/Iso/tote_s1000_gas.png` | 160 × 260 | 20 | `Assets/_Project/Prefabs/FuelStorage/tote_s1000_gas.prefab` | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/FuelStorage/Iso/tote_s1000_mixed.png` | 160 × 260 | 20 | `Assets/_Project/Prefabs/FuelStorage/tote_s1000_mixed.prefab` | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/FuelStorage/Iso/tote_s1000_oil.png` | 160 × 260 | 20 | `Assets/_Project/Prefabs/FuelStorage/tote_s1000_oil.prefab` | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/FuelStorage/Iso/tote_s600_diesel.png` | 136 × 220 | 20 | `Assets/_Project/Prefabs/FuelStorage/tote_s600_diesel.prefab` | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/FuelStorage/Iso/tote_s600_gas.png` | 136 × 220 | 20 | `Assets/_Project/Prefabs/FuelStorage/tote_s600_gas.prefab` | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/FuelStorage/Iso/tote_s600_mixed.png` | 136 × 220 | 20 | `Assets/_Project/Prefabs/FuelStorage/tote_s600_mixed.prefab` | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/FuelStorage/Iso/tote_s600_oil.png` | 136 × 220 | 20 | `Assets/_Project/Prefabs/FuelStorage/tote_s600_oil.prefab` | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/GasStation/Iso/dispenser_sDock.png` | 240 × 55 | 8 | `Assets/_Project/Prefabs/GasStation/dispenser_sDock.prefab` | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/GasStation/Iso/dispenser_sKerb.png` | 160 × 47 | 8 | `Assets/_Project/Prefabs/GasStation/dispenser_sKerb.prefab` | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/GasStation/Iso/dispenser_sLock.png` | 216 × 58 | 8 | `Assets/_Project/Prefabs/GasStation/dispenser_sLock.prefab` | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/GasStation/Iso/dispenser_sMpd.png` | 368 × 74 | 8 | `Assets/_Project/Prefabs/GasStation/dispenser_sMpd.prefab` | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/GasStation/Iso/dispenser_sTruck.png` | 416 × 68 | 8 | `Assets/_Project/Prefabs/GasStation/dispenser_sTruck.prefab` | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/GasStation/Iso/dispenser_sTwin.png` | 288 × 62 | 8 | `Assets/_Project/Prefabs/GasStation/dispenser_sTwin.prefab` | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/GasStation/Iso/dispenser_sVintage.png` | 208 × 51 | 8 | `Assets/_Project/Prefabs/GasStation/dispenser_sVintage.prefab` | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/GasStation/Iso/fillport_sCluster.png` | 624 × 52 | 8 | `Assets/_Project/Prefabs/GasStation/fillport_sCluster.prefab` | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/GasStation/Iso/fillport_sVent.png` | 352 × 100 | 8 | `Assets/_Project/Prefabs/GasStation/fillport_sVent.prefab` | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/GasStation/Iso/sign_sAframe.png` | 368 × 48 | 8 | `Assets/_Project/Prefabs/GasStation/sign_sAframe.prefab` | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/GasStation/Iso/sign_sPylon.png` | 544 × 192 | 8 | `Assets/_Project/Prefabs/GasStation/sign_sPylon.prefab` | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/GasStation/Iso/sign_sTotem.png` | 408 × 85 | 8 | `Assets/_Project/Prefabs/GasStation/sign_sTotem.prefab` | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/LobsterBuoy.png` | 16 × 32 | 1 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/LobsterTrap.png` | 32 × 32 | 1 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/NavBuoys/Iso/NavBuoy_CardinalE_s12.png` | 304 × 114 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/NavBuoys/Iso/NavBuoy_CardinalE_s18.png` | 448 × 166 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/NavBuoys/Iso/NavBuoy_CardinalE_s20.png` | 512 × 193 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/NavBuoys/Iso/NavBuoy_CardinalE_s24.png` | 624 × 234 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/NavBuoys/Iso/NavBuoy_CardinalE_s30.png` | 768 × 298 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/NavBuoys/Iso/NavBuoy_CardinalN_s12.png` | 304 × 117 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/NavBuoys/Iso/NavBuoy_CardinalN_s18.png` | 448 × 171 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/NavBuoys/Iso/NavBuoy_CardinalN_s20.png` | 512 × 198 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/NavBuoys/Iso/NavBuoy_CardinalN_s24.png` | 624 × 241 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/NavBuoys/Iso/NavBuoy_CardinalN_s30.png` | 768 × 305 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/NavBuoys/Iso/NavBuoy_CardinalS_s12.png` | 304 × 124 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/NavBuoys/Iso/NavBuoy_CardinalS_s18.png` | 448 × 181 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/NavBuoys/Iso/NavBuoy_CardinalS_s20.png` | 512 × 210 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/NavBuoys/Iso/NavBuoy_CardinalS_s24.png` | 624 × 254 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/NavBuoys/Iso/NavBuoy_CardinalS_s30.png` | 768 × 323 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/NavBuoys/Iso/NavBuoy_CardinalW_s12.png` | 304 × 121 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/NavBuoys/Iso/NavBuoy_CardinalW_s18.png` | 448 × 176 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/NavBuoys/Iso/NavBuoy_CardinalW_s20.png` | 512 × 205 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/NavBuoys/Iso/NavBuoy_CardinalW_s24.png` | 624 × 248 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/NavBuoys/Iso/NavBuoy_CardinalW_s30.png` | 768 × 315 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/NavBuoys/Iso/NavBuoy_Isolated_s12.png` | 304 × 119 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/NavBuoys/Iso/NavBuoy_Isolated_s18.png` | 448 × 175 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/NavBuoys/Iso/NavBuoy_Isolated_s20.png` | 512 × 203 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/NavBuoys/Iso/NavBuoy_Isolated_s24.png` | 624 × 246 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/NavBuoys/Iso/NavBuoy_Isolated_s30.png` | 768 × 312 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/NavBuoys/Iso/NavBuoy_Mooring_s12.png` | 320 × 46 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/NavBuoys/Iso/NavBuoy_Mooring_s18.png` | 480 × 68 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/NavBuoys/Iso/NavBuoy_Mooring_s20.png` | 544 × 79 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/NavBuoys/Iso/NavBuoy_Mooring_s24.png` | 656 × 98 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/NavBuoys/Iso/NavBuoy_Mooring_s30.png` | 816 × 126 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/NavBuoys/Iso/NavBuoy_PortCan_s12.png` | 304 × 63 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/NavBuoys/Iso/NavBuoy_PortCan_s18.png` | 448 × 91 | 8 | `Assets/_Project/Prefabs/Decor/NavBuoy.prefab` | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/NavBuoys/Iso/NavBuoy_PortCan_s20.png` | 512 × 106 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/NavBuoys/Iso/NavBuoy_PortCan_s24.png` | 624 × 132 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/NavBuoys/Iso/NavBuoy_PortCan_s30.png` | 768 × 168 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/NavBuoys/Iso/NavBuoy_PortLit_s12.png` | 304 × 117 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/NavBuoys/Iso/NavBuoy_PortLit_s18.png` | 448 × 172 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/NavBuoys/Iso/NavBuoy_PortLit_s20.png` | 512 × 199 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/NavBuoys/Iso/NavBuoy_PortLit_s24.png` | 624 × 242 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/NavBuoys/Iso/NavBuoy_PortLit_s30.png` | 768 × 308 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/NavBuoys/Iso/NavBuoy_StbdLit_s12.png` | 304 × 114 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/NavBuoys/Iso/NavBuoy_StbdLit_s18.png` | 448 × 167 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/NavBuoys/Iso/NavBuoy_StbdLit_s20.png` | 512 × 193 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/NavBuoys/Iso/NavBuoy_StbdLit_s24.png` | 624 × 235 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/NavBuoys/Iso/NavBuoy_StbdLit_s30.png` | 768 × 299 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/NavBuoys/Iso/NavBuoy_StbdNun_s12.png` | 304 × 64 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/NavBuoys/Iso/NavBuoy_StbdNun_s18.png` | 448 × 92 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/NavBuoys/Iso/NavBuoy_StbdNun_s20.png` | 512 × 107 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/NavBuoys/Iso/NavBuoy_StbdNun_s24.png` | 624 × 132 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/NavBuoys/Iso/NavBuoy_StbdNun_s30.png` | 768 × 169 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/NineMileCreek/FuelStop/SetPiece_air.png` | 208 × 48 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/NineMileCreek/FuelStop/SetPiece_barSign.png` | 720 × 117 | 8 | None (direct) | Flat-form candidate; inspect all facings before replacement. |
| `Assets/_Project/Art/Sprites/NineMileCreek/FuelStop/SetPiece_bench.png` | 416 × 53 | 8 | None (direct) | Weak: flat seat slab; little slat or joinery information. |
| `Assets/_Project/Art/Sprites/NineMileCreek/FuelStop/SetPiece_bin.png` | 144 × 35 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/NineMileCreek/FuelStop/SetPiece_dumpster.png` | 1152 × 139 | 8 | `Assets/_Project/Prefabs/Buildings/NineMileCreek/NmcFishPlant.prefab` | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/NineMileCreek/FuelStop/SetPiece_firewood.png` | 552 × 82 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/NineMileCreek/FuelStop/SetPiece_iceBox.png` | 464 × 70 | 8 | `Assets/_Project/Prefabs/Buildings/NineMileCreek/NmcFishPlant.prefab` | Weak: broad flat panels; handles and hinges missing. |
| `Assets/_Project/Art/Sprites/NineMileCreek/FuelStop/SetPiece_lampPole.png` | 536 × 235 | 8 | `Assets/_Project/Prefabs/Buildings/NineMileCreek/NmcFishPlant.prefab` | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/NineMileCreek/FuelStop/SetPiece_picnic.png` | 624 × 60 | 8 | None (direct) | Weak: nearly solid tabletop; no clear board joints. |
| `Assets/_Project/Art/Sprites/NineMileCreek/FuelStop/SetPiece_propane.png` | 472 × 82 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/NineMileCreek/FuelStop/SetPiece_pylon.png` | 1024 × 266 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/NineMileCreek/FuelStop/SetPiece_stopSign.png` | 176 × 60 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/NineMileCreek/FuelStop/SetPiece_vacuum.png` | 328 × 89 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/NineMileCreek/FuelStop/SetPiece_wheelStop.png` | 464 × 40 | 8 | `Assets/_Project/Prefabs/Buildings/NineMileCreek/NmcFishPlant.prefab` | Flat-form candidate; inspect all facings before replacement. |
| `Assets/_Project/Art/Sprites/NineMileCreek/SetPieces/SetPiece_harbourLight.png` | 1312 × 182 | 16 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/NineMileCreek/SetPieces/SetPiece_wharfCrane.png` | 3648 × 301 | 16 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/NineMileCreek/Yard/SetPiece_fence.png` | 2436 × 3340 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/NineMileCreek/Yard/SetPiece_floodE.png` | 512 × 215 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/NineMileCreek/Yard/SetPiece_floodW.png` | 512 × 215 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/NineMileCreek/Yard/SetPiece_hardA.png` | 3176 × 340 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/NineMileCreek/Yard/SetPiece_hardB.png` | 1896 × 202 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/NineMileCreek/Yard/SetPiece_hardC.png` | 1656 × 180 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/NineMileCreek/Yard/SetPiece_props.png` | 2236 × 3068 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/Shipyard/Iso/cradle.png` | 948 × 1264 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/Shipyard/Iso/davitPair.png` | 1602 × 1233 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/Shipyard/Iso/drums.png` | 560 × 764 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/Shipyard/Iso/fenceRun.png` | 1550 × 2192 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/Shipyard/Iso/gantryCrane.png` | 1563 × 1380 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/Shipyard/Iso/jibCrane.png` | 2742 × 2904 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/Shipyard/Iso/keelBlocks.png` | 948 × 1264 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/Shipyard/Iso/sawhorse.png` | 470 × 648 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/Shipyard/Iso/sparRack.png` | 832 × 1116 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/Shipyard/Iso/staging.png` | 1083 × 1023 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/Shipyard/Iso/timberStack.png` | 650 × 880 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/Shipyard/Iso/travelLift.png` | 1743 × 1446 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/StPeters/LandingFittings/SetPiece_landingRope.png` | 368 × 21 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/StPeters/LandingFittings/SetPiece_landingTyreEast.png` | 160 × 21 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/StPeters/LandingFittings/SetPiece_landingTyreWest.png` | 160 × 21 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/StPeters/LandingProps/SetPiece_landingCart.png` | 368 × 50 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/StPeters/LandingProps/SetPiece_landingLadder.png` | 208 × 215 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/StPeters/LandingProps/SetPiece_landingTotes.png` | 240 × 49 | 8 | `Assets/_Project/Prefabs/Buildings/NineMileCreek/NmcFishPlant.prefab` | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/StPeters/LandingProps/SetPiece_landingTraps.png` | 288 × 57 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/StPeters/SetPieces/SetPiece_boilerStack.png` | 1456 × 418 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/StPeters/SetPieces/SetPiece_conveyor.png` | 1600 × 198 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/StPeters/SetPieces/SetPiece_doorNotice.png` | 568 × 84 | 8 | None (direct) | Flat-form candidate; inspect all facings before replacement. |
| `Assets/_Project/Art/Sprites/StPeters/SetPieces/SetPiece_fallenSign.png` | 1280 × 138 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/StPeters/SetPieces/SetPiece_harbourVane.png` | 912 × 146 | 24 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/StPeters/SetPieces/SetPiece_slipTideBoard.png` | 168 × 127 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/StPeters/SetPieces/SetPiece_trolley.png` | 448 × 41 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/StPeters/SetPieces/SetPiece_trolleyLine.png` | 1580 × 530 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/Utility/alarmBox.png` | 160 × 53 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/Utility/beacon.png` | 400 × 123 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/Utility/bulkTank.png` | 1104 × 147 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/Utility/catchBasin.png` | 448 × 44 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/Utility/cistern.png` | 784 × 99 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/Utility/cleanout.png` | 208 × 21 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/Utility/coalBin.png` | 848 × 86 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/Utility/crossBox.png` | 528 × 67 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/Utility/culvert.png` | 1104 × 92 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/Utility/curbStop.png` | 112 × 11 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/Utility/drumRack.png` | 624 × 66 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/Utility/fillPipes.png` | 256 × 41 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/Utility/floodMast.png` | 464 × 219 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/Utility/fuelPump.png` | 304 × 58 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/Utility/genset.png` | 704 × 77 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/Utility/guyAnchor.png` | 672 × 103 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/Utility/handPump.png` | 304 × 54 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/Utility/hFrame.png` | 1504 × 289 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/Utility/hydrant.png` | 240 × 33 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/Utility/liftStation.png` | 944 × 99 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/Utility/manhole.png` | 368 × 31 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/Utility/meterBank.png` | 464 × 80 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/Utility/oilTank.png` | 576 × 79 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/Utility/outfall.png` | 800 × 75 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/Utility/padTransformer.png` | 800 × 78 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/Utility/pedestal.png` | 400 × 45 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/Utility/powerPole.png` | 1328 × 291 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/Utility/propaneTank.png` | 960 × 96 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/Utility/radioMast.png` | 1408 × 304 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/Utility/septicLids.png` | 800 × 74 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/Utility/serviceMast.png` | 304 × 130 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/Utility/standpipe.png` | 528 × 56 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/Utility/streetLamp.png` | 512 × 137 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/Utility/switchgear.png` | 896 × 92 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/Utility/telecomPed.png` | 208 × 37 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/Utility/trenchDrain.png` | 640 × 55 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/Utility/valveVault.png` | 432 × 54 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/Utility/ventStack.png` | 288 × 82 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/Utility/waterTank.png` | 944 × 159 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/Utility/wellCap.png` | 448 × 39 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/Utility/yardHydrant.png` | 192 × 49 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/Utility/yardLight.png` | 976 × 227 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/Wharf/Decor/anchorProp.png` | 384 × 64 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/Wharf/Decor/baitBarrel.png` | 160 × 33 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/Wharf/Decor/bench.png` | 480 × 60 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/Wharf/Decor/bicycle.png` | 448 × 46 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/Wharf/Decor/blockTackle.png` | 544 × 102 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/Wharf/Decor/bootRack.png` | 272 × 34 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/Wharf/Decor/bunting.png` | 896 × 131 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/Wharf/Decor/buoyRack.png` | 528 × 81 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/Wharf/Decor/buoyWall.png` | 576 × 93 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/Wharf/Decor/bushelBaskets.png` | 352 × 43 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/Wharf/Decor/capstan.png` | 240 × 33 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/Wharf/Decor/chalkSign.png` | 208 × 38 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/Wharf/Decor/codFlake.png` | 752 × 82 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/Wharf/Decor/crateStack.png` | 240 × 40 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/Wharf/Decor/davitHoist.png` | 768 × 110 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/Wharf/Decor/deckChair.png` | 272 × 47 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/Wharf/Decor/dockCart.png` | 368 × 41 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/Wharf/Decor/dogBowl.png` | 64 × 8 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/Wharf/Decor/drumPlanter.png` | 160 × 27 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/Wharf/Decor/fireCabinet.png` | 192 × 52 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/Wharf/Decor/fishRack.png` | 528 × 95 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/Wharf/Decor/flagpole.png` | 544 × 178 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/Wharf/Decor/floatBags.png` | 400 × 43 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/Wharf/Decor/gantryFrame.png` | 736 × 117 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/Wharf/Decor/guttingTable.png` | 752 × 77 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/Wharf/Decor/handTruck.png` | 224 × 47 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/Wharf/Decor/harbourSign.png` | 624 × 101 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/Wharf/Decor/herringSticks.png` | 512 × 82 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/Wharf/Decor/iceChest.png` | 416 × 48 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/Wharf/Decor/jerryCans.png` | 160 × 23 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/Wharf/Decor/lanternPost.png` | 208 × 71 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/Wharf/Decor/netFrame.png` | 688 × 107 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/Wharf/Decor/netPile.png` | 576 × 50 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/Wharf/Decor/netReel.png` | 560 × 74 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/Wharf/Decor/noticeBoard.png` | 384 × 74 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/Wharf/Decor/oilDrum.png` | 160 × 33 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/Wharf/Decor/oilskinLine.png` | 656 × 98 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/Wharf/Decor/pallet.png` | 352 × 36 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/Wharf/Decor/picnicTable.png` | 704 × 65 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/Wharf/Decor/planterBox.png` | 384 × 40 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/Wharf/Decor/potHauler.png` | 224 × 44 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/Wharf/Decor/rescueLadder.png` | 192 × 63 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/Wharf/Decor/ringPost.png` | 256 × 43 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/Wharf/Decor/ringStation.png` | 240 × 61 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/Wharf/Decor/ropeCoil.png` | 368 × 31 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/Wharf/Decor/ropeFence.png` | 848 × 92 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/Wharf/Decor/ropeHanks.png` | 400 × 68 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/Wharf/Decor/saltBin.png` | 480 × 60 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/Wharf/Decor/saltBox.png` | 384 × 51 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/Wharf/Decor/sortingTrough.png` | 752 × 72 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/Wharf/Decor/tideStaff.png` | 80 × 85 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/Wharf/Decor/toteSingle.png` | 240 × 28 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/Wharf/Decor/toteStack.png` | 240 × 49 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/Wharf/Decor/trapSingle.png` | 288 × 31 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/Wharf/Decor/trapStack.png` | 288 × 57 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/Wharf/Decor/trashBarrel.png` | 192 × 34 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/Wharf/Decor/trawlDoor.png` | 464 × 62 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/Wharf/Decor/tyrePile.png` | 224 × 34 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/Wharf/Decor/weighScale.png` | 352 × 69 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/Wharf/Decor/wheelbarrow.png` | 352 × 43 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/Wharf/Decor/woodStack.png` | 560 × 62 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/WharfPost.png` | 16 × 40 | 1 | None (direct) | Weak: straight shaded bar; minimal end grain and fastening. |
| `Assets/_Project/Art/Sprites/Yard/Yard_anchorSet.png` | 448 × 66 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/Yard/Yard_barrelPlanter.png` | 176 × 30 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/Yard/Yard_borderBed.png` | 880 × 78 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/Yard/Yard_buoyPost.png` | 128 × 58 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/Yard/Yard_clothesline.png` | 1136 × 137 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/Yard/Yard_doryPlanter.png` | 600 × 61 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/Yard/Yard_farmStand.png` | 384 × 64 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/Yard/Yard_fenceCorner.png` | 320 × 47 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/Yard/Yard_firePit.png` | 768 × 66 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/Yard/Yard_flagpole.png` | 512 × 194 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/Yard/Yard_hedgeCorner.png` | 384 × 46 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/Yard/Yard_hedgeRun.png` | 672 × 69 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/Yard/Yard_oilTank.png` | 352 × 50 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/Yard/Yard_picketGate.png` | 352 × 60 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/Yard/Yard_picketPanel.png` | 640 × 76 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/Yard/Yard_postRail.png` | 736 × 86 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/Yard/Yard_potatoDrills.png` | 848 × 72 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/Yard/Yard_sawhorses.png` | 544 × 60 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/Yard/Yard_splitRail.png` | 832 × 89 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/Yard/Yard_stonePillar.png` | 240 × 49 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/Yard/Yard_stoneWall.png` | 664 × 67 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/Yard/Yard_swingSet.png` | 560 × 92 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/Yard/Yard_trapBench.png` | 392 × 41 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/Yard/Yard_vegRows.png` | 768 × 68 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |
| `Assets/_Project/Art/Sprites/Yard/Yard_wireFence.png` | 816 × 93 | 8 | None (direct) | Detailed / recognizable; no obvious placeholder flag. |

## Missing categories and limits

No standalone mailbox or kennel PNG was found by filename or the yard/wharf inventory. Existing wood stacks and clotheslines are present. Tyre piles and landing tyre fittings exist; bollard-like mooring fittings may also be embedded in the large wharf assemblies excluded above. Boats on stocks are the three `SetPiece_hardA/B/C` sheets.

The evidence contact sheet shows every inventory entry at exactly 2× nearest-neighbour on sampled grass and deck colours, using the first importer facing and cropping transparent margins only. Other facings remain in the sources; sign backs need that context. The selected weak forms are also inspected across all facings. This is a sprite audit, not an in-engine lighting or placement acceptance.
