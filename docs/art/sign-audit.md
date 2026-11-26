# Sign and shopfront lettering audit

Baseline: `1abca5974ce3d16a3257777797bb2547c9ad8d53`. Documentation only. P3: a living working coast.

Scope: tracked runtime PNGs under `Assets`, their importer slices, serialized GUID references, word sidecars, shopfront recipes and the 148 tracked prefabs. Reference renders under documentation are not runtime sprites. Sheet size below is the full PNG; multi-facing sheets contain several smaller sprites. A direct scene reference is reported separately from a prefab. Blank panels are not counted as readable signs.

## Existing signs, boards and shopfront faces

| Sprite file (under `Assets/_Project/Art/`) | Pixels | Prefab / scene / building use | What it actually says |
|---|---|---|---|
| `Sprites/Buildings/FishBuyerStall.png` | 128 Ã— 160 | `Greywick.unity` | Baked pixel letters: FISH, COD, HADDOCK; coin dots. Legacy buyer stall. |
| `Sprites/Buildings/NineMileCreek/NmcCommunityHall_exterior.png` | 463 Ã— 581 | No serialized sprite GUID consumer found; retained art/catalog output; corresponding building sidecar reserves word panels (does not prove the exterior PNG is wired) | Blank name and notice panels; words.nmc_community_hall and words.nmc_community_hall_notice anchors. |
| `Sprites/Buildings/NineMileCreek/NmcFishPlant_exterior.png` | 1653 Ã— 788 | No serialized sprite GUID consumer found; retained art/catalog output; corresponding building sidecar reserves word panels (does not prove the exterior PNG is wired) | Blank name, notice and gable panels; words.nmc_fish_plant, words.nmc_fish_plant_notice and words.nmc_fish_plant_gable anchors. |
| `Sprites/Buildings/Shops/Shopfront_fishMarket.png` | 4064 Ã— 402 | No serialized sprite GUID consumer found; retained art/catalog output | Blank fascia and oval bracket board; no FISH MARKET letters. |
| `Sprites/Buildings/Shops/Shopfront_generalStore.png` | 3864 Ã— 1030 | `StPeters.unity` | Blank fascia and hanging bracket board; no GENERAL STORE letters. |
| `Sprites/Buildings/Shops/Shopfront_postOffice.png` | 3968 Ã— 475 | `StPeters.unity` | Blank fascia; no POST OFFICE letters. |
| `Sprites/Buildings/Shops/Shopfront_restaurant.png` | 3888 Ã— 1164 | No serialized sprite GUID consumer found; retained art/catalog output | Blank fascia and bracket board; no RESTAURANT letters. |
| `Sprites/Buildings/Village/Village_generalStore.png` | 1648 Ã— 858 | `Village_generalStore.prefab` | Store building with blank frontage; no readable name. |
| `Sprites/Buildings/Village/Village_stPetersBaitStore.png` | 1824 Ã— 630 | `Village_stPetersBaitStore.prefab`, `StPeters.unity` | Blank gable panel; intended BAIT & ICE is separate word data. |
| `Sprites/Buildings/Village/Village_stPetersCannery.png` | 3568 Ã— 1362 | `Village_stPetersCannery.prefab`, `StPeters.unity` | Ruined building with blank panel; companion fallenSign and doorNotice carry separate word anchors. |
| `Sprites/GasStation/Iso/sign_sAframe.png` | 368 Ã— 48 | `sign_sAframe.asset`, `sign_sAframe.prefab` | Fuel bands and price-like blocks; no readable trade name. |
| `Sprites/GasStation/Iso/sign_sPylon.png` | 544 Ã— 192 | `sign_sPylon.asset`, `sign_sPylon.prefab` | Fuel bands and price-like blocks; no readable trade name. |
| `Sprites/GasStation/Iso/sign_sTotem.png` | 408 Ã— 85 | `sign_sTotem.asset`, `sign_sTotem.prefab` | Fuel bands and price-like blocks; no readable trade name. |
| `Sprites/NineMileCreek/FuelStop/SetPiece_barSign.png` | 720 Ã— 117 | `SetPiece_barSign.asset` | Baked worn road lettering: ST PETERS / BAR CROSSING / ON FOOT AT LOW TIDE, plus NO TRUCKS (source strings; the rendered third line is truncated). Text is clipped/worn in the rendered face; retained kind without a placement row. |
| `Sprites/NineMileCreek/FuelStop/SetPiece_stopSign.png` | 176 Ã— 60 | `SetPiece_stopSign.asset`, `NineMileCreek.unity` | Baked STOP pixels, with weak contrast and an edge-clipped S in the inspected facing. |
| `Sprites/NineMileCreek/SetPieces/SetPiece_fishMarket.png` | 2352 Ã— 266 | `SetPiece_fishMarket.asset`, `NineMileCreek.unity` | Blank name and day-price boards; reserved FISH MARKET and TODAY. |
| `Sprites/NineMileCreek/SetPieces/SetPiece_officeTrailer.png` | 2544 Ã— 271 | `SetPiece_officeTrailer.asset`, `NineMileCreek.unity` | Blank office/licence boards; reserved HARBOURMASTER and LICENCES Â· BERTHS Â· WHARFAGE. |
| `Sprites/StPeters/SetPieces/SetPiece_doorNotice.png` | 568 Ã— 84 | `SetPiece_doorNotice.asset`, `StPeters.unity` | Blank notice. Reserved heading NOTICE and notice.cannery_for_sale body. |
| `Sprites/StPeters/SetPieces/SetPiece_fallenSign.png` | 1280 Ã— 138 | `SetPiece_fallenSign.asset`, `StPeters.unity` | Blank fallen cannery board. Reserved text: ST PETERS PACKING CO. (words.cannery_sign). |
| `Sprites/StPeters/SetPieces/SetPiece_slipTideBoard.png` | 168 Ã— 127 | `SetPiece_slipTideBoard.asset`, `StPeters.unity` | Gauge marks; numerals 1â€“4 are word anchors, not baked lettering. |
| `Sprites/Wharf/Decor/chalkSign.png` | 208 Ã— 38 | `StPeters.unity` | Four chalk strokes, not readable letters. |
| `Sprites/Wharf/Decor/harbourSign.png` | 624 Ã— 101 | `NineMileCreek.unity`, `StPeters.unity` | Blank timber name panel with decorative bars; no words. |
| `Sprites/Wharf/Decor/noticeBoard.png` | 384 Ã— 74 | `NineMileCreek.unity`, `StPeters.unity` | Pinned blank notices; no readable heading or body. |
| `UI/SellChalkboard.png` | 208 Ã— 144 | No serialized sprite GUID consumer found; retained art/catalog output | Baked MARKET heading on a UI board background; not an in-world shop sign. |
| `Sprites/NineMileCreek/SetPieces/SetPiece_restaurant.png` | 3532 Ã— 1330 | `NineMileCreek.unity`, `SetPiece_restaurant.asset` | Blank name and menu boards; reserved RESTAURANT and CHOWDER Â· FISH & CHIPS Â· LOBSTER ROLLS. |
| `Sprites/GasStation/Iso/store_sStore.png` | 1112 Ã— 1724 | `store_sStore.prefab`, `store_sStore.asset` | Fuel-store fascia without readable trade lettering. |
| `Sprites/GasStation/Iso/store_sKiosk.png` | 1984 Ã— 210 | `store_sKiosk.prefab`, `store_sKiosk.asset` | Kiosk fascia without readable trade lettering. |

## Shop and work prefabs without readable signage

The following are the complete shop/work-building prefab families in this baseline. None contains a serialized TextMesh/TextMeshPro text value. A blank panel or art intended for a building is not a readable sign on its prefab. Homes are outside this table; attached work sheds are included.

| Prefab path under `Assets/_Project/Prefabs/` | Sign status |
|---|---|
| `Buildings/NineMileCreek/NmcFishPlant.prefab` | No readable name; exterior art has blank word-anchor panels. |
| `Buildings/NineMileCreek/NmcCommunityHall.prefab` | No readable name; exterior art has blank word-anchor panels. |
| `Buildings/NineMileCreek/NmcBoatGearWholesaler.prefab` | Greybox shell; no name-board sprite. |
| `Buildings/NineMileCreek/NmcFishingGearWholesaler.prefab` | Greybox shell; no name-board sprite. |
| `Decor/Buildings/Village/Village_generalStore.prefab` | No readable shop/work name; blank panels where present. |
| `Decor/Buildings/Village/Village_school.prefab` | No readable shop/work name; blank panels where present. |
| `Decor/Buildings/Village/Village_stPetersBaitStore.prefab` | No readable shop/work name; blank panels where present. |
| `Decor/Buildings/Village/Village_stPetersCannery.prefab` | No readable shop/work name; blank panels where present. |
| `Decor/Buildings/Village/Village_stPetersIceHouse.prefab` | No readable shop/work name; blank panels where present. |
| `Decor/Buildings/Village/Village_stPetersNetLoft.prefab` | No readable shop/work name; blank panels where present. |
| `Decor/Buildings/Village/Village_stPetersNetShed.prefab` | No readable shop/work name; blank panels where present. |
| `Decor/Buildings/Village/Village_ginnyNetStore.prefab` | No readable shop/work name; blank panels where present. |
| `Decor/Buildings/Village/Village_ginnyWoodshed.prefab` | No readable shop/work name; blank panels where present. |
| `Decor/Buildings/Village/Village_ginnyLeanTo.prefab` | No readable shop/work name; blank panels where present. |
| `GasStation/store_sStore.prefab` | No readable trade name; interior is included as a work-space prefab. |
| `GasStation/store_sKiosk.prefab` | No readable trade name; interior is included as a work-space prefab. |
| `GasStation/interior_sStore.prefab` | No readable trade name; interior is included as a work-space prefab. |

No gym, vehicle-dealer, boatyard, independent wharf fish-buyer, restaurant, or post-office **building prefab** exists in this baseline. Some roles exist as direct scene sprites, set-piece assets, or greybox scene objects. The legacy `ShipwrightShed.png` (256 Ã— 224) in `Greywick.unity` has no sign; it is not a building prefab. The legacy buyer stall already has the baked FISH/COD/HADDOCK panel listed above. The wharf sheds and cold/trap stores are direct scene sprites, not missing prefab rows.

## How words reach the picture

- **Baked pixels:** the legacy fish-buyer stall, stop marker and crossing road board have lettering inside their PNGs; the UI chalkboard has MARKET. There is no separate reusable shop-letter sprite/font sheet in the runtime art inventory. Wharf chalk strokes, fuel price blocks and abstract shopfront bands must not be reported as legible text.
- **Blank geometry or pixels plus word anchors:** `Code/World/WordsTableDef.cs:7â€“13` explicitly says boards bake blank and no board-lettering path is implemented there. `Data/Words/WordsTable_StPeters.asset` stores BAIT & ICE, the cannery name, NOTICE, crossing hours, REFUGE and a direction word. Set-piece and building JSON sidecars reserve corners and ids; those strings alone do not make visible words. The shopfront source describes its signs as abstract blanks (`docs/art/rigs/shop-building-kit/shopfrontRig.js:28,318`).
- **Text components elsewhere:** `Code/Boats/DeckFreshnessTag.cs:156` creates a world-space TextMesh for catch information. This is not a shop-sign renderer. Prefab searches found no serialized `m_Text`/`m_text` value. UI labels and the sell chalkboard are a separate screen interface.
- **Additional board data without a standalone name-board sprite:** crossing/refuge/direction words in the words table, and the plant/hall sidecar ids, remain data. This audit does not convert them into baked lettering or connect the proposed additions to them.

## Evidence and limitations

The review sheet `12-sign-audit-2x.png` shows representative facing pixels at 2Ã— nearest-neighbour, plus the existing harbour and notice boards. Full sheets, GUID consumers and sizes were recorded in the evidence inventory. This is a source-art inspection, not a new gameplay capture. Before and after are identical because this change edits documentation only. No editor or native tests ran. Some faces remain abstract at native scale; picture acceptance and any eventual placement are separate review work.
