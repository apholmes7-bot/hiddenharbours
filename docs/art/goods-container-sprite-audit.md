# Goods, containers and cargo sprite audit

Source snapshot: `1abca5974ce3d16a3257777797bb2547c9ad8d53`. Documentation only.

Retained against main `d46bde303e6aa256d23d47a05ede5d7d6f38e51e`. All inventory counts, direct references and source observations below describe that earlier snapshot, not a new acceptance of the current appearance. The five named candidate images have no intervening change on main. The shopping bag and shelf purchases now survive a restart; the absence of a sprite field on a goods definition does not mean shelf purchasing or physical carrying is absent. See [shop at the shelf](../design/shop-at-the-shelf.md) for that implemented path. No images were rendered or judged again for this documentation refresh.

Scope: physical stock, fish deck/cradle forms and inventory icons, wharf containers and handling props, fuel containers, shore pickups, and research goods/storage definitions. Swimming/strike animations, buildings, UI chrome and fixed ground are excluded. Sizes below are the entire PNG and one imported cell, not world dimensions. Direct references are read from serialized Def/prefab GUIDs; no direct reference does not prove absence from a scene or a runtime loader.

The existing fish box equivalent is `Sprites/Gear/FishTray.png`: 160×72, a 5×3 grid of 32×24 cells (13 imported slices). Its deck container Def has an empty FillSprites array. New standalone fill-state candidates should use 32×24 canvases; this audit makes no wiring changes.

## Five candidates recorded at the source snapshot

| Rank | Sprite | Readability issue | Next action |
|---|---|---|---|
| 1 | Sprites/Crate.png | Legacy small square has a thin diagonal brace and little depth beside wharf cargo. | Redraw the same single canvas with clear slats, bracing and corner hardware. |
| 2 | Sprites/Barrel.png | Legacy barrel has a shallow silhouette and compressed hoops. | Redraw the same single canvas with a readable lid, staves and iron hoops. |
| 3 | Wharf/Decor/toteSingle.png | Solid pale top hides both the rim and its fish/ice contents in every facing. | Redraw eight facings with an open rim, handles and visible silver catch. |
| 4 | Wharf/Decor/crateStack.png | Repeated labels and busy wood texture merge the crate seams. | Future quieter grain and clearer stacked rims. |
| 5 | Wharf/Decor/ropeHanks.png | Front-facing loops are short and blend into the backboard. Rear-facing board is intentional. | Future longer separated rope loops; preserve rear occlusion. |

Ranking is based on rendered source sheets beside existing wharf props, not an in-game acceptance judgment. Existing cell layouts and pivots must survive any redraw.

## Sprite inventory

| File (relative to Assets/_Project/Art/) | PNG pixels | Cell pixels | Def or prefab direct reference |
|---|---|---|---|
| `Fishing/GreenCrabDeck.png` | 384×48 | 48×48 | `Assets/_Project/Data/Traps/PotDeckWork.asset` |
| `Fishing/Icons/FishIcon_bass.png` | 46×13 | 46×13 | `Assets/_Project/Data/Fish/StripedBass.asset`<br>`Assets/_Project/Resources/IconLibrary.asset` |
| `Fishing/Icons/FishIcon_cod.png` | 38×12 | 38×12 | `Assets/_Project/Data/Fish/AtlanticCod.asset`<br>`Assets/_Project/Resources/IconLibrary.asset` |
| `Fishing/Icons/FishIcon_cunner.png` | 45×20 | 44×18 | `Assets/_Project/Data/Fish/Cunner.asset`<br>`Assets/_Project/Resources/IconLibrary.asset` |
| `Fishing/Icons/FishIcon_flounder.png` | 40×14 | 40×14 | `Assets/_Project/Data/Fish/WinterFlounder.asset`<br>`Assets/_Project/Resources/IconLibrary.asset` |
| `Fishing/Icons/FishIcon_haddock.png` | 29×9 | 29×9 | `Assets/_Project/Data/Fish/Haddock.asset`<br>`Assets/_Project/Resources/IconLibrary.asset` |
| `Fishing/Icons/FishIcon_herring.png` | 43×10 | 43×10 | `Assets/_Project/Data/Fish/AtlanticHerring.asset`<br>`Assets/_Project/Resources/IconLibrary.asset` |
| `Fishing/Icons/FishIcon_mackerel.png` | 43×12 | 43×12 | `Assets/_Project/Data/Fish/Mackerel.asset`<br>`Assets/_Project/Resources/IconLibrary.asset` |
| `Fishing/Icons/FishIcon_pollock.png` | 34×10 | 34×10 | `Assets/_Project/Data/Fish/Pollock.asset`<br>`Assets/_Project/Resources/IconLibrary.asset` |
| `Fishing/Icons/FishIcon_smelt.png` | 45×10 | 45×10 | `Assets/_Project/Data/Fish/RainbowSmelt.asset`<br>`Assets/_Project/Resources/IconLibrary.asset` |
| `Fishing/Icons/FishIcon_tomcod.png` | 43×13 | 43×12 | `Assets/_Project/Data/Fish/AtlanticTomcod.asset`<br>`Assets/_Project/Resources/IconLibrary.asset` |
| `Fishing/Icons/GearIcon_bait_capelin.png` | 39×23 | 39×23 | `Assets/_Project/Resources/IconLibrary.asset` |
| `Fishing/Icons/GearIcon_bait_fish_scrap.png` | 40×26 | 40×26 | `Assets/_Project/Resources/IconLibrary.asset` |
| `Fishing/Icons/GearIcon_bait_herring.png` | 28×34 | 28×34 | `Assets/_Project/Resources/IconLibrary.asset` |
| `Fishing/Icons/GearIcon_bait_mackerel.png` | 42×19 | 42×19 | `Assets/_Project/Resources/IconLibrary.asset` |
| `Fishing/Icons/GearIcon_bait_sea_worm.png` | 36×22 | 36×22 | `Assets/_Project/Resources/IconLibrary.asset` |
| `Fishing/Icons/GearIcon_bait_shucked_clam.png` | 30×28 | 30×28 | `Assets/_Project/Resources/IconLibrary.asset` |
| `Fishing/Icons/GearIcon_bait_squid_strip.png` | 42×18 | 42×18 | `Assets/_Project/Resources/IconLibrary.asset` |
| `Fishing/Icons/GearIcon_cod_jig.png` | 18×30 | 18×30 | `Assets/_Project/Resources/IconLibrary.asset` |
| `Fishing/Icons/GearIcon_crab_pot.png` | 34×29 | 34×29 | `Assets/_Project/Resources/IconLibrary.asset` |
| `Fishing/Icons/GearIcon_depth_sounder.png` | 36×29 | 36×29 | `Assets/_Project/Resources/IconLibrary.asset` |
| `Fishing/Icons/GearIcon_fish_finder.png` | 28×29 | 28×29 | `Assets/_Project/Resources/IconLibrary.asset` |
| `Fishing/Icons/GearIcon_gps.png` | 34×26 | 34×26 | `Assets/_Project/Resources/IconLibrary.asset` |
| `Fishing/Icons/GearIcon_harbour_watch.png` | 22×43 | 22×43 | `Assets/_Project/Resources/IconLibrary.asset` |
| `Fishing/Icons/GearIcon_headlamp.png` | 32×30 | 32×30 | `Assets/_Project/Resources/IconLibrary.asset` |
| `Fishing/Icons/GearIcon_lobster_pot.png` | 38×34 | 38×34 | `Assets/_Project/Resources/IconLibrary.asset` |
| `Fishing/Icons/GearIcon_longline_tub.png` | 26×25 | 26×25 | `Assets/_Project/Resources/IconLibrary.asset` |
| `Fishing/Icons/GearIcon_loran.png` | 30×32 | 30×32 | `Assets/_Project/Resources/IconLibrary.asset` |
| `Fishing/Icons/GearIcon_mackerel_feathers.png` | 18×37 | 18×37 | `Assets/_Project/Resources/IconLibrary.asset` |
| `Fishing/Icons/GearIcon_mussel_sack.png` | 22×25 | 22×25 | `Assets/_Project/Resources/IconLibrary.asset` |
| `Fishing/Icons/GearIcon_oyster_tongs.png` | 49×21 | 49×21 | `Assets/_Project/Resources/IconLibrary.asset` |
| `Fishing/Icons/GearIcon_rod.png` | 47×19 | 47×19 | `Assets/_Project/Resources/IconLibrary.asset` |
| `Fishing/Icons/GearIcon_rod_rack.png` | 28×38 | 28×38 | `Assets/_Project/Resources/IconLibrary.asset` |
| `Fishing/Icons/GearIcon_shellfish_basket.png` | 28×26 | 28×26 | `Assets/_Project/Resources/IconLibrary.asset` |
| `Fishing/Icons/GearIcon_shellfish_rake.png` | 41×20 | 41×20 | `Assets/_Project/Resources/IconLibrary.asset` |
| `Fishing/Icons/GearIcon_soft_bait.png` | 34×14 | 34×14 | `Assets/_Project/Resources/IconLibrary.asset` |
| `Fishing/Icons/GearIcon_spinner.png` | 20×26 | 20×26 | `Assets/_Project/Resources/IconLibrary.asset` |
| `Fishing/Icons/GearIcon_spoon.png` | 36×18 | 36×18 | `Assets/_Project/Resources/IconLibrary.asset` |
| `Fishing/Icons/GearIcon_trawl_net.png` | 43×24 | 43×24 | `Assets/_Project/Resources/IconLibrary.asset` |
| `Fishing/Icons/ShellIcon_american_oyster.png` | 37×26 | 37×26 | `Assets/_Project/Data/Fish/AmericanOyster.asset`<br>`Assets/_Project/Resources/IconLibrary.asset` |
| `Fishing/Icons/ShellIcon_atlantic_surf_clam.png` | 40×30 | 40×30 | `Assets/_Project/Data/Fish/AtlanticSurfClam.asset`<br>`Assets/_Project/Resources/IconLibrary.asset` |
| `Fishing/Icons/ShellIcon_blue_mussel.png` | 33×14 | 33×14 | `Assets/_Project/Data/Fish/BlueMussel.asset`<br>`Assets/_Project/Resources/IconLibrary.asset` |
| `Fishing/Icons/ShellIcon_green_crab.png` | 46×30 | 46×30 | `Assets/_Project/Data/Fish/GreenCrab.asset`<br>`Assets/_Project/Resources/IconLibrary.asset` |
| `Fishing/Icons/ShellIcon_lobster.png` | 48×30 | 48×30 | `Assets/_Project/Data/Fish/AmericanLobster.asset`<br>`Assets/_Project/Resources/IconLibrary.asset` |
| `Fishing/Icons/ShellIcon_northern_quahog.png` | 32×28 | 32×28 | `Assets/_Project/Data/Fish/NorthernQuahog.asset`<br>`Assets/_Project/Resources/IconLibrary.asset` |
| `Fishing/Icons/ShellIcon_rock_crab.png` | 46×30 | 46×30 | `Assets/_Project/Data/Fish/RockCrab.asset`<br>`Assets/_Project/Resources/IconLibrary.asset` |
| `Fishing/Icons/ShellIcon_sea_scallop.png` | 36×34 | 36×34 | `Assets/_Project/Data/Fish/SeaScallop.asset`<br>`Assets/_Project/Resources/IconLibrary.asset` |
| `Fishing/Icons/ShellIcon_soft_shell_clam.png` | 32×18 | 32×18 | `Assets/_Project/Data/Fish/SoftShellClam.asset`<br>`Assets/_Project/Resources/IconLibrary.asset` |
| `Fishing/Iso/Fish_bass_cradle.png` | 128×640 | 64×80 | No direct Def/prefab sprite reference |
| `Fishing/Iso/Fish_bass_deck.png` | 256×640 | 64×80 | `Assets/_Project/Data/Art/CatchItemLibrary.asset` |
| `Fishing/Iso/Fish_bass_lg_cradle.png` | 128×640 | 64×80 | No direct Def/prefab sprite reference |
| `Fishing/Iso/Fish_bass_lg_deck.png` | 256×640 | 64×80 | `Assets/_Project/Data/Art/CatchItemLibrary.asset` |
| `Fishing/Iso/Fish_bass_sm_cradle.png` | 128×640 | 64×80 | No direct Def/prefab sprite reference |
| `Fishing/Iso/Fish_bass_sm_deck.png` | 256×640 | 64×80 | `Assets/_Project/Data/Art/CatchItemLibrary.asset` |
| `Fishing/Iso/Fish_cod_cradle.png` | 128×640 | 64×80 | No direct Def/prefab sprite reference |
| `Fishing/Iso/Fish_cod_deck.png` | 256×640 | 64×80 | `Assets/_Project/Data/Art/CatchItemLibrary.asset` |
| `Fishing/Iso/Fish_cod_lg_cradle.png` | 128×640 | 64×80 | No direct Def/prefab sprite reference |
| `Fishing/Iso/Fish_cod_lg_deck.png` | 256×640 | 64×80 | `Assets/_Project/Data/Art/CatchItemLibrary.asset` |
| `Fishing/Iso/Fish_cod_sm_cradle.png` | 128×640 | 64×80 | No direct Def/prefab sprite reference |
| `Fishing/Iso/Fish_cod_sm_deck.png` | 256×640 | 64×80 | `Assets/_Project/Data/Art/CatchItemLibrary.asset` |
| `Fishing/Iso/Fish_cunner_cradle.png` | 128×640 | 64×80 | No direct Def/prefab sprite reference |
| `Fishing/Iso/Fish_cunner_deck.png` | 256×640 | 64×80 | `Assets/_Project/Data/Art/CatchItemLibrary.asset` |
| `Fishing/Iso/Fish_cunner_lg_cradle.png` | 128×640 | 64×80 | No direct Def/prefab sprite reference |
| `Fishing/Iso/Fish_cunner_lg_deck.png` | 256×640 | 64×80 | `Assets/_Project/Data/Art/CatchItemLibrary.asset` |
| `Fishing/Iso/Fish_cunner_sm_cradle.png` | 128×640 | 64×80 | No direct Def/prefab sprite reference |
| `Fishing/Iso/Fish_cunner_sm_deck.png` | 256×640 | 64×80 | `Assets/_Project/Data/Art/CatchItemLibrary.asset` |
| `Fishing/Iso/Fish_flounder_cradle.png` | 128×640 | 64×80 | No direct Def/prefab sprite reference |
| `Fishing/Iso/Fish_flounder_deck.png` | 256×640 | 64×80 | `Assets/_Project/Data/Art/CatchItemLibrary.asset` |
| `Fishing/Iso/Fish_flounder_lg_cradle.png` | 128×640 | 64×80 | No direct Def/prefab sprite reference |
| `Fishing/Iso/Fish_flounder_lg_deck.png` | 256×640 | 64×80 | `Assets/_Project/Data/Art/CatchItemLibrary.asset` |
| `Fishing/Iso/Fish_flounder_sm_cradle.png` | 128×640 | 64×80 | No direct Def/prefab sprite reference |
| `Fishing/Iso/Fish_flounder_sm_deck.png` | 256×640 | 64×80 | `Assets/_Project/Data/Art/CatchItemLibrary.asset` |
| `Fishing/Iso/Fish_haddock_cradle.png` | 128×640 | 64×80 | No direct Def/prefab sprite reference |
| `Fishing/Iso/Fish_haddock_deck.png` | 256×640 | 64×80 | `Assets/_Project/Data/Art/CatchItemLibrary.asset` |
| `Fishing/Iso/Fish_haddock_lg_cradle.png` | 128×640 | 64×80 | No direct Def/prefab sprite reference |
| `Fishing/Iso/Fish_haddock_lg_deck.png` | 256×640 | 64×80 | `Assets/_Project/Data/Art/CatchItemLibrary.asset` |
| `Fishing/Iso/Fish_haddock_sm_cradle.png` | 128×640 | 64×80 | No direct Def/prefab sprite reference |
| `Fishing/Iso/Fish_haddock_sm_deck.png` | 256×640 | 64×80 | `Assets/_Project/Data/Art/CatchItemLibrary.asset` |
| `Fishing/Iso/Fish_herring_cradle.png` | 128×640 | 64×80 | No direct Def/prefab sprite reference |
| `Fishing/Iso/Fish_herring_deck.png` | 256×640 | 64×80 | `Assets/_Project/Data/Art/CatchItemLibrary.asset` |
| `Fishing/Iso/Fish_herring_lg_cradle.png` | 128×640 | 64×80 | No direct Def/prefab sprite reference |
| `Fishing/Iso/Fish_herring_lg_deck.png` | 256×640 | 64×80 | `Assets/_Project/Data/Art/CatchItemLibrary.asset` |
| `Fishing/Iso/Fish_herring_sm_cradle.png` | 128×640 | 64×80 | No direct Def/prefab sprite reference |
| `Fishing/Iso/Fish_herring_sm_deck.png` | 256×640 | 64×80 | `Assets/_Project/Data/Art/CatchItemLibrary.asset` |
| `Fishing/Iso/Fish_mackerel_cradle.png` | 128×640 | 64×80 | No direct Def/prefab sprite reference |
| `Fishing/Iso/Fish_mackerel_deck.png` | 256×640 | 64×80 | `Assets/_Project/Data/Art/CatchItemLibrary.asset` |
| `Fishing/Iso/Fish_mackerel_lg_cradle.png` | 128×640 | 64×80 | No direct Def/prefab sprite reference |
| `Fishing/Iso/Fish_mackerel_lg_deck.png` | 256×640 | 64×80 | `Assets/_Project/Data/Art/CatchItemLibrary.asset` |
| `Fishing/Iso/Fish_mackerel_sm_cradle.png` | 128×640 | 64×80 | No direct Def/prefab sprite reference |
| `Fishing/Iso/Fish_mackerel_sm_deck.png` | 256×640 | 64×80 | `Assets/_Project/Data/Art/CatchItemLibrary.asset` |
| `Fishing/Iso/Fish_pollock_cradle.png` | 128×640 | 64×80 | No direct Def/prefab sprite reference |
| `Fishing/Iso/Fish_pollock_deck.png` | 256×640 | 64×80 | `Assets/_Project/Data/Art/CatchItemLibrary.asset` |
| `Fishing/Iso/Fish_pollock_lg_cradle.png` | 128×640 | 64×80 | No direct Def/prefab sprite reference |
| `Fishing/Iso/Fish_pollock_lg_deck.png` | 256×640 | 64×80 | `Assets/_Project/Data/Art/CatchItemLibrary.asset` |
| `Fishing/Iso/Fish_pollock_sm_cradle.png` | 128×640 | 64×80 | No direct Def/prefab sprite reference |
| `Fishing/Iso/Fish_pollock_sm_deck.png` | 256×640 | 64×80 | `Assets/_Project/Data/Art/CatchItemLibrary.asset` |
| `Fishing/Iso/Fish_smelt_cradle.png` | 128×640 | 64×80 | No direct Def/prefab sprite reference |
| `Fishing/Iso/Fish_smelt_deck.png` | 256×640 | 64×80 | `Assets/_Project/Data/Art/CatchItemLibrary.asset` |
| `Fishing/Iso/Fish_smelt_lg_cradle.png` | 128×640 | 64×80 | No direct Def/prefab sprite reference |
| `Fishing/Iso/Fish_smelt_lg_deck.png` | 256×640 | 64×80 | `Assets/_Project/Data/Art/CatchItemLibrary.asset` |
| `Fishing/Iso/Fish_smelt_sm_cradle.png` | 128×640 | 64×80 | No direct Def/prefab sprite reference |
| `Fishing/Iso/Fish_smelt_sm_deck.png` | 256×640 | 64×80 | `Assets/_Project/Data/Art/CatchItemLibrary.asset` |
| `Fishing/Iso/Fish_tomcod_cradle.png` | 128×640 | 64×80 | No direct Def/prefab sprite reference |
| `Fishing/Iso/Fish_tomcod_deck.png` | 256×640 | 64×80 | `Assets/_Project/Data/Art/CatchItemLibrary.asset` |
| `Fishing/Iso/Fish_tomcod_lg_cradle.png` | 128×640 | 64×80 | No direct Def/prefab sprite reference |
| `Fishing/Iso/Fish_tomcod_lg_deck.png` | 256×640 | 64×80 | `Assets/_Project/Data/Art/CatchItemLibrary.asset` |
| `Fishing/Iso/Fish_tomcod_sm_cradle.png` | 128×640 | 64×80 | No direct Def/prefab sprite reference |
| `Fishing/Iso/Fish_tomcod_sm_deck.png` | 256×640 | 64×80 | `Assets/_Project/Data/Art/CatchItemLibrary.asset` |
| `Sprites/DeckGear/Fauna/baitbag.png` | 11×16 | 11×16 | No direct Def/prefab sprite reference |
| `Sprites/DeckGear/Fauna/kelp.png` | 18×20 | 18×20 | No direct Def/prefab sprite reference |
| `Sprites/DeckGear/Fauna/sculpin.png` | 18×14 | 18×14 | No direct Def/prefab sprite reference |
| `Sprites/DeckGear/Fauna/starfish.png` | 15×12 | 15×12 | No direct Def/prefab sprite reference |
| `Sprites/DeckGear/Fauna/urchin.png` | 16×13 | 16×13 | No direct Def/prefab sprite reference |
| `Sprites/DeckGear/Fauna/whelk.png` | 11×8 | 11×8 | No direct Def/prefab sprite reference |
| `Sprites/DeckGear/Gear/baitbin.png` | 176×33 | 22×33 | No direct Def/prefab sprite reference |
| `Sprites/DeckGear/Gear/baitbox.png` | 256×31 | 32×31 | No direct Def/prefab sprite reference |
| `Sprites/DeckGear/Gear/bandingtable.png` | 288×46 | 36×46 | No direct Def/prefab sprite reference |
| `Sprites/DeckGear/Gear/chopboard.png` | 208×36 | 26×36 | No direct Def/prefab sprite reference |
| `Sprites/DeckGear/Gear/haulerstation.png` | 416×60 | 52×60 | No direct Def/prefab sprite reference |
| `Sprites/DeckGear/Traps/crabCone.png` | 248×41 | 31×41 | No direct Def/prefab sprite reference |
| `Sprites/DeckGear/Traps/crabWire.png` | 256×27 | 32×27 | No direct Def/prefab sprite reference |
| `Sprites/DeckGear/Traps/wire.png` | 336×35 | 42×35 | No direct Def/prefab sprite reference |
| `Sprites/DeckGear/Traps/wood.png` | 288×36 | 36×36 | No direct Def/prefab sprite reference |
| `Sprites/DeckGear/Trays/tray.png` | 224×21 | 28×21 | No direct Def/prefab sprite reference |
| `Sprites/FuelStorage/Iso/bulk_s10k_diesel.png` | 536×645 | 134×129 | `Assets/_Project/Data/FuelContainers/bulk_s10k_diesel.asset`<br>`Assets/_Project/Prefabs/FuelStorage/bulk_s10k_diesel.prefab` |
| `Sprites/FuelStorage/Iso/bulk_s10k_gas.png` | 536×645 | 134×129 | `Assets/_Project/Data/FuelContainers/bulk_s10k_gas.asset`<br>`Assets/_Project/Prefabs/FuelStorage/bulk_s10k_gas.prefab` |
| `Sprites/FuelStorage/Iso/bulk_s10k_mixed.png` | 536×645 | 134×129 | `Assets/_Project/Data/FuelContainers/bulk_s10k_mixed.asset`<br>`Assets/_Project/Prefabs/FuelStorage/bulk_s10k_mixed.prefab` |
| `Sprites/FuelStorage/Iso/bulk_s10k_oil.png` | 536×645 | 134×129 | `Assets/_Project/Data/FuelContainers/bulk_s10k_oil.asset`<br>`Assets/_Project/Prefabs/FuelStorage/bulk_s10k_oil.prefab` |
| `Sprites/FuelStorage/Iso/bulk_s25k_diesel.png` | 664×865 | 166×173 | `Assets/_Project/Data/FuelContainers/bulk_s25k_diesel.asset`<br>`Assets/_Project/Prefabs/FuelStorage/bulk_s25k_diesel.prefab` |
| `Sprites/FuelStorage/Iso/bulk_s25k_gas.png` | 664×865 | 166×173 | `Assets/_Project/Data/FuelContainers/bulk_s25k_gas.asset`<br>`Assets/_Project/Prefabs/FuelStorage/bulk_s25k_gas.prefab` |
| `Sprites/FuelStorage/Iso/bulk_s25k_mixed.png` | 664×865 | 166×173 | `Assets/_Project/Data/FuelContainers/bulk_s25k_mixed.asset`<br>`Assets/_Project/Prefabs/FuelStorage/bulk_s25k_mixed.prefab` |
| `Sprites/FuelStorage/Iso/bulk_s25k_oil.png` | 664×865 | 166×173 | `Assets/_Project/Data/FuelContainers/bulk_s25k_oil.asset`<br>`Assets/_Project/Prefabs/FuelStorage/bulk_s25k_oil.prefab` |
| `Sprites/FuelStorage/Iso/bulk_s50k_diesel.png` | 744×1140 | 186×228 | `Assets/_Project/Data/FuelContainers/bulk_s50k_diesel.asset`<br>`Assets/_Project/Prefabs/FuelStorage/bulk_s50k_diesel.prefab` |
| `Sprites/FuelStorage/Iso/bulk_s50k_gas.png` | 744×1140 | 186×228 | `Assets/_Project/Data/FuelContainers/bulk_s50k_gas.asset`<br>`Assets/_Project/Prefabs/FuelStorage/bulk_s50k_gas.prefab` |
| `Sprites/FuelStorage/Iso/bulk_s50k_mixed.png` | 744×1140 | 186×228 | `Assets/_Project/Data/FuelContainers/bulk_s50k_mixed.asset`<br>`Assets/_Project/Prefabs/FuelStorage/bulk_s50k_mixed.prefab` |
| `Sprites/FuelStorage/Iso/bulk_s50k_oil.png` | 744×1140 | 186×228 | `Assets/_Project/Data/FuelContainers/bulk_s50k_oil.asset`<br>`Assets/_Project/Prefabs/FuelStorage/bulk_s50k_oil.prefab` |
| `Sprites/FuelStorage/Iso/drum_s205_diesel.png` | 96×170 | 24×34 | `Assets/_Project/Data/FuelContainers/drum_s205_diesel.asset`<br>`Assets/_Project/Prefabs/FuelStorage/drum_s205_diesel.prefab` |
| `Sprites/FuelStorage/Iso/drum_s205_gas.png` | 96×170 | 24×34 | `Assets/_Project/Data/FuelContainers/drum_s205_gas.asset`<br>`Assets/_Project/Prefabs/FuelStorage/drum_s205_gas.prefab` |
| `Sprites/FuelStorage/Iso/drum_s205_mixed.png` | 96×170 | 24×34 | `Assets/_Project/Data/FuelContainers/drum_s205_mixed.asset`<br>`Assets/_Project/Prefabs/FuelStorage/drum_s205_mixed.prefab` |
| `Sprites/FuelStorage/Iso/drum_s205_oil.png` | 96×170 | 24×34 | `Assets/_Project/Data/FuelContainers/drum_s205_oil.asset`<br>`Assets/_Project/Prefabs/FuelStorage/drum_s205_oil.prefab` |
| `Sprites/FuelStorage/Iso/drum_s60_diesel.png` | 72×125 | 18×25 | `Assets/_Project/Data/FuelContainers/drum_s60_diesel.asset`<br>`Assets/_Project/Prefabs/FuelStorage/drum_s60_diesel.prefab` |
| `Sprites/FuelStorage/Iso/drum_s60_gas.png` | 72×125 | 18×25 | `Assets/_Project/Data/FuelContainers/drum_s60_gas.asset`<br>`Assets/_Project/Prefabs/FuelStorage/drum_s60_gas.prefab` |
| `Sprites/FuelStorage/Iso/drum_s60_mixed.png` | 72×125 | 18×25 | `Assets/_Project/Data/FuelContainers/drum_s60_mixed.asset`<br>`Assets/_Project/Prefabs/FuelStorage/drum_s60_mixed.prefab` |
| `Sprites/FuelStorage/Iso/drum_s60_oil.png` | 72×125 | 18×25 | `Assets/_Project/Data/FuelContainers/drum_s60_oil.asset`<br>`Assets/_Project/Prefabs/FuelStorage/drum_s60_oil.prefab` |
| `Sprites/FuelStorage/Iso/jerry_s10_diesel.png` | 80×85 | 10×17 | `Assets/_Project/Data/FuelContainers/jerry_s10_diesel.asset`<br>`Assets/_Project/Prefabs/FuelStorage/jerry_s10_diesel.prefab` |
| `Sprites/FuelStorage/Iso/jerry_s10_gas.png` | 80×85 | 10×17 | `Assets/_Project/Data/FuelContainers/jerry_s10_gas.asset`<br>`Assets/_Project/Prefabs/FuelStorage/jerry_s10_gas.prefab` |
| `Sprites/FuelStorage/Iso/jerry_s10_mixed.png` | 80×85 | 10×17 | `Assets/_Project/Data/FuelContainers/jerry_s10_mixed.asset`<br>`Assets/_Project/Prefabs/FuelStorage/jerry_s10_mixed.prefab` |
| `Sprites/FuelStorage/Iso/jerry_s10_oil.png` | 80×85 | 10×17 | `Assets/_Project/Data/FuelContainers/jerry_s10_oil.asset`<br>`Assets/_Project/Prefabs/FuelStorage/jerry_s10_oil.prefab` |
| `Sprites/FuelStorage/Iso/jerry_s20_diesel.png` | 96×100 | 12×20 | `Assets/_Project/Data/FuelContainers/jerry_s20_diesel.asset`<br>`Assets/_Project/Prefabs/FuelStorage/jerry_s20_diesel.prefab` |
| `Sprites/FuelStorage/Iso/jerry_s20_gas.png` | 96×100 | 12×20 | `Assets/_Project/Data/FuelContainers/jerry_s20_gas.asset`<br>`Assets/_Project/Prefabs/FuelStorage/jerry_s20_gas.prefab` |
| `Sprites/FuelStorage/Iso/jerry_s20_mixed.png` | 96×100 | 12×20 | `Assets/_Project/Data/FuelContainers/jerry_s20_mixed.asset`<br>`Assets/_Project/Prefabs/FuelStorage/jerry_s20_mixed.prefab` |
| `Sprites/FuelStorage/Iso/jerry_s20_oil.png` | 96×100 | 12×20 | `Assets/_Project/Data/FuelContainers/jerry_s20_oil.asset`<br>`Assets/_Project/Prefabs/FuelStorage/jerry_s20_oil.prefab` |
| `Sprites/FuelStorage/Iso/jerry_s25_diesel.png` | 112×110 | 14×22 | `Assets/_Project/Data/FuelContainers/jerry_s25_diesel.asset`<br>`Assets/_Project/Prefabs/FuelStorage/jerry_s25_diesel.prefab` |
| `Sprites/FuelStorage/Iso/jerry_s25_gas.png` | 112×110 | 14×22 | `Assets/_Project/Data/FuelContainers/jerry_s25_gas.asset`<br>`Assets/_Project/Prefabs/FuelStorage/jerry_s25_gas.prefab` |
| `Sprites/FuelStorage/Iso/jerry_s25_mixed.png` | 112×110 | 14×22 | `Assets/_Project/Data/FuelContainers/jerry_s25_mixed.asset`<br>`Assets/_Project/Prefabs/FuelStorage/jerry_s25_mixed.prefab` |
| `Sprites/FuelStorage/Iso/jerry_s25_oil.png` | 112×110 | 14×22 | `Assets/_Project/Data/FuelContainers/jerry_s25_oil.asset`<br>`Assets/_Project/Prefabs/FuelStorage/jerry_s25_oil.prefab` |
| `Sprites/FuelStorage/Iso/jerry_s5_diesel.png` | 64×70 | 8×14 | `Assets/_Project/Data/FuelContainers/jerry_s5_diesel.asset`<br>`Assets/_Project/Prefabs/FuelStorage/jerry_s5_diesel.prefab` |
| `Sprites/FuelStorage/Iso/jerry_s5_gas.png` | 64×70 | 8×14 | `Assets/_Project/Data/FuelContainers/jerry_s5_gas.asset`<br>`Assets/_Project/Prefabs/FuelStorage/jerry_s5_gas.prefab` |
| `Sprites/FuelStorage/Iso/jerry_s5_mixed.png` | 64×70 | 8×14 | `Assets/_Project/Data/FuelContainers/jerry_s5_mixed.asset`<br>`Assets/_Project/Prefabs/FuelStorage/jerry_s5_mixed.prefab` |
| `Sprites/FuelStorage/Iso/jerry_s5_oil.png` | 64×70 | 8×14 | `Assets/_Project/Data/FuelContainers/jerry_s5_oil.asset`<br>`Assets/_Project/Prefabs/FuelStorage/jerry_s5_oil.prefab` |
| `Sprites/FuelStorage/Iso/jug_s10_diesel.png` | 80×65 | 10×13 | `Assets/_Project/Data/FuelContainers/jug_s10_diesel.asset`<br>`Assets/_Project/Prefabs/FuelStorage/jug_s10_diesel.prefab` |
| `Sprites/FuelStorage/Iso/jug_s10_gas.png` | 80×65 | 10×13 | `Assets/_Project/Data/FuelContainers/jug_s10_gas.asset`<br>`Assets/_Project/Prefabs/FuelStorage/jug_s10_gas.prefab` |
| `Sprites/FuelStorage/Iso/jug_s10_mixed.png` | 80×65 | 10×13 | `Assets/_Project/Data/FuelContainers/jug_s10_mixed.asset`<br>`Assets/_Project/Prefabs/FuelStorage/jug_s10_mixed.prefab` |
| `Sprites/FuelStorage/Iso/jug_s10_oil.png` | 80×65 | 10×13 | `Assets/_Project/Data/FuelContainers/jug_s10_oil.asset`<br>`Assets/_Project/Prefabs/FuelStorage/jug_s10_oil.prefab` |
| `Sprites/FuelStorage/Iso/jug_s1_diesel.png` | 32×35 | 4×7 | `Assets/_Project/Data/FuelContainers/jug_s1_diesel.asset`<br>`Assets/_Project/Prefabs/FuelStorage/jug_s1_diesel.prefab` |
| `Sprites/FuelStorage/Iso/jug_s1_gas.png` | 32×35 | 4×7 | `Assets/_Project/Data/FuelContainers/jug_s1_gas.asset`<br>`Assets/_Project/Prefabs/FuelStorage/jug_s1_gas.prefab` |
| `Sprites/FuelStorage/Iso/jug_s1_mixed.png` | 32×35 | 4×7 | `Assets/_Project/Data/FuelContainers/jug_s1_mixed.asset`<br>`Assets/_Project/Prefabs/FuelStorage/jug_s1_mixed.prefab` |
| `Sprites/FuelStorage/Iso/jug_s1_oil.png` | 32×35 | 4×7 | `Assets/_Project/Data/FuelContainers/jug_s1_oil.asset`<br>`Assets/_Project/Prefabs/FuelStorage/jug_s1_oil.prefab` |
| `Sprites/FuelStorage/Iso/jug_s4_diesel.png` | 64×50 | 8×10 | `Assets/_Project/Data/FuelContainers/jug_s4_diesel.asset`<br>`Assets/_Project/Prefabs/FuelStorage/jug_s4_diesel.prefab` |
| `Sprites/FuelStorage/Iso/jug_s4_gas.png` | 64×50 | 8×10 | `Assets/_Project/Data/FuelContainers/jug_s4_gas.asset`<br>`Assets/_Project/Prefabs/FuelStorage/jug_s4_gas.prefab` |
| `Sprites/FuelStorage/Iso/jug_s4_mixed.png` | 64×50 | 8×10 | `Assets/_Project/Data/FuelContainers/jug_s4_mixed.asset`<br>`Assets/_Project/Prefabs/FuelStorage/jug_s4_mixed.prefab` |
| `Sprites/FuelStorage/Iso/jug_s4_oil.png` | 64×50 | 8×10 | `Assets/_Project/Data/FuelContainers/jug_s4_oil.asset`<br>`Assets/_Project/Prefabs/FuelStorage/jug_s4_oil.prefab` |
| `Sprites/FuelStorage/Iso/nozzle_sAuto_diesel.png` | 96×9 | 12×9 | `Assets/_Project/Data/FuelContainers/nozzle_sAuto_diesel.asset`<br>`Assets/_Project/Prefabs/FuelStorage/nozzle_sAuto_diesel.prefab` |
| `Sprites/FuelStorage/Iso/nozzle_sAuto_gas.png` | 96×9 | 12×9 | `Assets/_Project/Data/FuelContainers/nozzle_sAuto_gas.asset`<br>`Assets/_Project/Prefabs/FuelStorage/nozzle_sAuto_gas.prefab` |
| `Sprites/FuelStorage/Iso/nozzle_sAuto_mixed.png` | 96×9 | 12×9 | `Assets/_Project/Data/FuelContainers/nozzle_sAuto_mixed.asset`<br>`Assets/_Project/Prefabs/FuelStorage/nozzle_sAuto_mixed.prefab` |
| `Sprites/FuelStorage/Iso/nozzle_sAuto_oil.png` | 96×9 | 12×9 | `Assets/_Project/Data/FuelContainers/nozzle_sAuto_oil.asset`<br>`Assets/_Project/Prefabs/FuelStorage/nozzle_sAuto_oil.prefab` |
| `Sprites/FuelStorage/Iso/nozzle_sHigh_diesel.png` | 128×11 | 16×11 | `Assets/_Project/Data/FuelContainers/nozzle_sHigh_diesel.asset`<br>`Assets/_Project/Prefabs/FuelStorage/nozzle_sHigh_diesel.prefab` |
| `Sprites/FuelStorage/Iso/nozzle_sHigh_gas.png` | 128×11 | 16×11 | `Assets/_Project/Data/FuelContainers/nozzle_sHigh_gas.asset`<br>`Assets/_Project/Prefabs/FuelStorage/nozzle_sHigh_gas.prefab` |
| `Sprites/FuelStorage/Iso/nozzle_sHigh_mixed.png` | 128×11 | 16×11 | `Assets/_Project/Data/FuelContainers/nozzle_sHigh_mixed.asset`<br>`Assets/_Project/Prefabs/FuelStorage/nozzle_sHigh_mixed.prefab` |
| `Sprites/FuelStorage/Iso/nozzle_sHigh_oil.png` | 128×11 | 16×11 | `Assets/_Project/Data/FuelContainers/nozzle_sHigh_oil.asset`<br>`Assets/_Project/Prefabs/FuelStorage/nozzle_sHigh_oil.prefab` |
| `Sprites/FuelStorage/Iso/pump_sSingle_diesel.png` | 112×50 | 28×50 | `Assets/_Project/Data/FuelContainers/pump_sSingle_diesel.asset`<br>`Assets/_Project/Prefabs/FuelStorage/pump_sSingle_diesel.prefab` |
| `Sprites/FuelStorage/Iso/pump_sSingle_gas.png` | 112×50 | 28×50 | `Assets/_Project/Data/FuelContainers/pump_sSingle_gas.asset`<br>`Assets/_Project/Prefabs/FuelStorage/pump_sSingle_gas.prefab` |
| `Sprites/FuelStorage/Iso/pump_sSingle_mixed.png` | 112×50 | 28×50 | `Assets/_Project/Data/FuelContainers/pump_sSingle_mixed.asset`<br>`Assets/_Project/Prefabs/FuelStorage/pump_sSingle_mixed.prefab` |
| `Sprites/FuelStorage/Iso/pump_sSingle_oil.png` | 112×50 | 28×50 | `Assets/_Project/Data/FuelContainers/pump_sSingle_oil.asset`<br>`Assets/_Project/Prefabs/FuelStorage/pump_sSingle_oil.prefab` |
| `Sprites/FuelStorage/Iso/pump_sTwin_diesel.png` | 128×55 | 32×55 | `Assets/_Project/Data/FuelContainers/pump_sTwin_diesel.asset`<br>`Assets/_Project/Prefabs/FuelStorage/pump_sTwin_diesel.prefab` |
| `Sprites/FuelStorage/Iso/pump_sTwin_gas.png` | 128×55 | 32×55 | `Assets/_Project/Data/FuelContainers/pump_sTwin_gas.asset`<br>`Assets/_Project/Prefabs/FuelStorage/pump_sTwin_gas.prefab` |
| `Sprites/FuelStorage/Iso/pump_sTwin_mixed.png` | 128×55 | 32×55 | `Assets/_Project/Data/FuelContainers/pump_sTwin_mixed.asset`<br>`Assets/_Project/Prefabs/FuelStorage/pump_sTwin_mixed.prefab` |
| `Sprites/FuelStorage/Iso/pump_sTwin_oil.png` | 128×55 | 32×55 | `Assets/_Project/Data/FuelContainers/pump_sTwin_oil.asset`<br>`Assets/_Project/Prefabs/FuelStorage/pump_sTwin_oil.prefab` |
| `Sprites/FuelStorage/Iso/skid_s1200_diesel.png` | 248×330 | 62×66 | `Assets/_Project/Data/FuelContainers/skid_s1200_diesel.asset`<br>`Assets/_Project/Prefabs/FuelStorage/skid_s1200_diesel.prefab` |
| `Sprites/FuelStorage/Iso/skid_s1200_gas.png` | 248×330 | 62×66 | `Assets/_Project/Data/FuelContainers/skid_s1200_gas.asset`<br>`Assets/_Project/Prefabs/FuelStorage/skid_s1200_gas.prefab` |
| `Sprites/FuelStorage/Iso/skid_s1200_mixed.png` | 248×330 | 62×66 | `Assets/_Project/Data/FuelContainers/skid_s1200_mixed.asset`<br>`Assets/_Project/Prefabs/FuelStorage/skid_s1200_mixed.prefab` |
| `Sprites/FuelStorage/Iso/skid_s1200_oil.png` | 248×330 | 62×66 | `Assets/_Project/Data/FuelContainers/skid_s1200_oil.asset`<br>`Assets/_Project/Prefabs/FuelStorage/skid_s1200_oil.prefab` |
| `Sprites/FuelStorage/Iso/skid_s2500_diesel.png` | 312×400 | 78×80 | `Assets/_Project/Data/FuelContainers/skid_s2500_diesel.asset`<br>`Assets/_Project/Prefabs/FuelStorage/skid_s2500_diesel.prefab` |
| `Sprites/FuelStorage/Iso/skid_s2500_gas.png` | 312×400 | 78×80 | `Assets/_Project/Data/FuelContainers/skid_s2500_gas.asset`<br>`Assets/_Project/Prefabs/FuelStorage/skid_s2500_gas.prefab` |
| `Sprites/FuelStorage/Iso/skid_s2500_mixed.png` | 312×400 | 78×80 | `Assets/_Project/Data/FuelContainers/skid_s2500_mixed.asset`<br>`Assets/_Project/Prefabs/FuelStorage/skid_s2500_mixed.prefab` |
| `Sprites/FuelStorage/Iso/skid_s2500_oil.png` | 312×400 | 78×80 | `Assets/_Project/Data/FuelContainers/skid_s2500_oil.asset`<br>`Assets/_Project/Prefabs/FuelStorage/skid_s2500_oil.prefab` |
| `Sprites/FuelStorage/Iso/skid_s500_diesel.png` | 192×255 | 48×51 | `Assets/_Project/Data/FuelContainers/skid_s500_diesel.asset`<br>`Assets/_Project/Prefabs/FuelStorage/skid_s500_diesel.prefab` |
| `Sprites/FuelStorage/Iso/skid_s500_gas.png` | 192×255 | 48×51 | `Assets/_Project/Data/FuelContainers/skid_s500_gas.asset`<br>`Assets/_Project/Prefabs/FuelStorage/skid_s500_gas.prefab` |
| `Sprites/FuelStorage/Iso/skid_s500_mixed.png` | 192×255 | 48×51 | `Assets/_Project/Data/FuelContainers/skid_s500_mixed.asset`<br>`Assets/_Project/Prefabs/FuelStorage/skid_s500_mixed.prefab` |
| `Sprites/FuelStorage/Iso/skid_s500_oil.png` | 192×255 | 48×51 | `Assets/_Project/Data/FuelContainers/skid_s500_oil.asset`<br>`Assets/_Project/Prefabs/FuelStorage/skid_s500_oil.prefab` |
| `Sprites/FuelStorage/Iso/tote_s1000_diesel.png` | 160×260 | 40×52 | `Assets/_Project/Data/FuelContainers/tote_s1000_diesel.asset`<br>`Assets/_Project/Prefabs/FuelStorage/tote_s1000_diesel.prefab` |
| `Sprites/FuelStorage/Iso/tote_s1000_gas.png` | 160×260 | 40×52 | `Assets/_Project/Data/FuelContainers/tote_s1000_gas.asset`<br>`Assets/_Project/Prefabs/FuelStorage/tote_s1000_gas.prefab` |
| `Sprites/FuelStorage/Iso/tote_s1000_mixed.png` | 160×260 | 40×52 | `Assets/_Project/Data/FuelContainers/tote_s1000_mixed.asset`<br>`Assets/_Project/Prefabs/FuelStorage/tote_s1000_mixed.prefab` |
| `Sprites/FuelStorage/Iso/tote_s1000_oil.png` | 160×260 | 40×52 | `Assets/_Project/Data/FuelContainers/tote_s1000_oil.asset`<br>`Assets/_Project/Prefabs/FuelStorage/tote_s1000_oil.prefab` |
| `Sprites/FuelStorage/Iso/tote_s600_diesel.png` | 136×220 | 34×44 | `Assets/_Project/Data/FuelContainers/tote_s600_diesel.asset`<br>`Assets/_Project/Prefabs/FuelStorage/tote_s600_diesel.prefab` |
| `Sprites/FuelStorage/Iso/tote_s600_gas.png` | 136×220 | 34×44 | `Assets/_Project/Data/FuelContainers/tote_s600_gas.asset`<br>`Assets/_Project/Prefabs/FuelStorage/tote_s600_gas.prefab` |
| `Sprites/FuelStorage/Iso/tote_s600_mixed.png` | 136×220 | 34×44 | `Assets/_Project/Data/FuelContainers/tote_s600_mixed.asset`<br>`Assets/_Project/Prefabs/FuelStorage/tote_s600_mixed.prefab` |
| `Sprites/FuelStorage/Iso/tote_s600_oil.png` | 136×220 | 34×44 | `Assets/_Project/Data/FuelContainers/tote_s600_oil.asset`<br>`Assets/_Project/Prefabs/FuelStorage/tote_s600_oil.prefab` |
| `Sprites/Gear/ClamBucket.png` | 32×32 | 32×32 | `Assets/_Project/Resources/IconLibrary.asset` |
| `Sprites/Gear/FishTray.png` | 160×72 | 32×24 | No direct Def/prefab sprite reference |
| `Sprites/NineMileCreek/FuelStop/SetPiece_iceBox.png` | 464×70 | 58×70 | `Assets/_Project/Data/SetPieces/SetPiece_iceBox.asset`<br>`Assets/_Project/Prefabs/Buildings/NineMileCreek/NmcFishPlant.prefab` |
| `Sprites/NineMileCreek/FuelStop/SetPiece_propane.png` | 472×82 | 59×82 | `Assets/_Project/Data/SetPieces/SetPiece_propane.asset` |
| `Sprites/Shore/Finds/Bone.png` | 44×24 | 22×12 | No direct Def/prefab sprite reference |
| `Sprites/Shore/Finds/CrabMoult.png` | 40×32 | 20×16 | No direct Def/prefab sprite reference |
| `Sprites/Shore/Finds/Driftwood.png` | 64×32 | 32×16 | No direct Def/prefab sprite reference |
| `Sprites/Shore/Finds/GullFeather.png` | 44×28 | 22×14 | No direct Def/prefab sprite reference |
| `Sprites/Shore/Finds/Mussel.png` | 36×28 | 18×14 | No direct Def/prefab sprite reference |
| `Sprites/Shore/Finds/Oyster.png` | 44×32 | 22×16 | No direct Def/prefab sprite reference |
| `Sprites/Shore/Finds/Periwinkle.png` | 32×28 | 16×14 | No direct Def/prefab sprite reference |
| `Sprites/Shore/Finds/SandDollar.png` | 32×32 | 16×16 | No direct Def/prefab sprite reference |
| `Sprites/Shore/Finds/Scallop.png` | 36×32 | 18×16 | No direct Def/prefab sprite reference |
| `Sprites/Shore/Finds/SeaGlass.png` | 28×24 | 14×12 | No direct Def/prefab sprite reference |
| `Sprites/Shore/Finds/SoftShellClam.png` | 36×28 | 18×14 | No direct Def/prefab sprite reference |
| `Sprites/Shore/Finds/Starfish.png` | 40×40 | 20×20 | No direct Def/prefab sprite reference |
| `Sprites/Shore/FindsIso/BarnacleCluster_bleached.png` | 840×33 | 35×33 | No direct Def/prefab sprite reference |
| `Sprites/Shore/FindsIso/BarnacleCluster_dry.png` | 840×33 | 35×33 | No direct Def/prefab sprite reference |
| `Sprites/Shore/FindsIso/BarnacleCluster_wet.png` | 840×33 | 35×33 | No direct Def/prefab sprite reference |
| `Sprites/Shore/FindsIso/BlueMussel_bleached.png` | 600×25 | 25×25 | No direct Def/prefab sprite reference |
| `Sprites/Shore/FindsIso/BlueMussel_dry.png` | 600×25 | 25×25 | No direct Def/prefab sprite reference |
| `Sprites/Shore/FindsIso/BlueMussel_wet.png` | 600×25 | 25×25 | No direct Def/prefab sprite reference |
| `Sprites/Shore/FindsIso/BrittleStar_bleached.png` | 888×31 | 37×31 | No direct Def/prefab sprite reference |
| `Sprites/Shore/FindsIso/BrittleStar_dry.png` | 888×31 | 37×31 | No direct Def/prefab sprite reference |
| `Sprites/Shore/FindsIso/BrittleStar_wet.png` | 888×31 | 37×31 | No direct Def/prefab sprite reference |
| `Sprites/Shore/FindsIso/CrabMoult_bleached.png` | 1128×44 | 47×44 | No direct Def/prefab sprite reference |
| `Sprites/Shore/FindsIso/CrabMoult_dry.png` | 1128×44 | 47×44 | No direct Def/prefab sprite reference |
| `Sprites/Shore/FindsIso/CrabMoult_wet.png` | 1128×44 | 47×44 | No direct Def/prefab sprite reference |
| `Sprites/Shore/FindsIso/Dogwhelk_bleached.png` | 504×26 | 21×26 | No direct Def/prefab sprite reference |
| `Sprites/Shore/FindsIso/Dogwhelk_dry.png` | 504×26 | 21×26 | No direct Def/prefab sprite reference |
| `Sprites/Shore/FindsIso/Dogwhelk_wet.png` | 504×26 | 21×26 | No direct Def/prefab sprite reference |
| `Sprites/Shore/FindsIso/DriftBranch_bleached.png` | 1188×168 | 99×84 | No direct Def/prefab sprite reference |
| `Sprites/Shore/FindsIso/DriftBranch_dry.png` | 1188×168 | 99×84 | No direct Def/prefab sprite reference |
| `Sprites/Shore/FindsIso/DriftBranch_wet.png` | 1188×168 | 99×84 | No direct Def/prefab sprite reference |
| `Sprites/Shore/FindsIso/DriftPlank_bleached.png` | 1320×180 | 110×90 | No direct Def/prefab sprite reference |
| `Sprites/Shore/FindsIso/DriftPlank_dry.png` | 1320×180 | 110×90 | No direct Def/prefab sprite reference |
| `Sprites/Shore/FindsIso/DriftPlank_wet.png` | 1320×180 | 110×90 | No direct Def/prefab sprite reference |
| `Sprites/Shore/FindsIso/DriftRoot_bleached.png` | 1260×202 | 105×101 | No direct Def/prefab sprite reference |
| `Sprites/Shore/FindsIso/DriftRoot_dry.png` | 1260×202 | 105×101 | No direct Def/prefab sprite reference |
| `Sprites/Shore/FindsIso/DriftRoot_wet.png` | 1260×202 | 105×101 | No direct Def/prefab sprite reference |
| `Sprites/Shore/FindsIso/Driftwood_bleached.png` | 1536×242 | 128×121 | No direct Def/prefab sprite reference |
| `Sprites/Shore/FindsIso/Driftwood_dry.png` | 1536×242 | 128×121 | No direct Def/prefab sprite reference |
| `Sprites/Shore/FindsIso/Driftwood_wet.png` | 1536×242 | 128×121 | No direct Def/prefab sprite reference |
| `Sprites/Shore/FindsIso/GlassBottle_bleached.png` | 1368×55 | 57×55 | No direct Def/prefab sprite reference |
| `Sprites/Shore/FindsIso/GlassBottle_dry.png` | 1368×55 | 57×55 | No direct Def/prefab sprite reference |
| `Sprites/Shore/FindsIso/GlassBottle_wet.png` | 1368×55 | 57×55 | No direct Def/prefab sprite reference |
| `Sprites/Shore/FindsIso/GreenCrabShell_bleached.png` | 864×34 | 36×34 | No direct Def/prefab sprite reference |
| `Sprites/Shore/FindsIso/GreenCrabShell_dry.png` | 864×34 | 36×34 | No direct Def/prefab sprite reference |
| `Sprites/Shore/FindsIso/GreenCrabShell_wet.png` | 864×34 | 36×34 | No direct Def/prefab sprite reference |
| `Sprites/Shore/FindsIso/GreenUrchin_bleached.png` | 552×25 | 23×25 | No direct Def/prefab sprite reference |
| `Sprites/Shore/FindsIso/GreenUrchin_dry.png` | 552×25 | 23×25 | No direct Def/prefab sprite reference |
| `Sprites/Shore/FindsIso/GreenUrchin_wet.png` | 552×25 | 23×25 | No direct Def/prefab sprite reference |
| `Sprites/Shore/FindsIso/GullFeather_bleached.png` | 1104×37 | 46×37 | No direct Def/prefab sprite reference |
| `Sprites/Shore/FindsIso/GullFeather_dry.png` | 1104×37 | 46×37 | No direct Def/prefab sprite reference |
| `Sprites/Shore/FindsIso/GullFeather_wet.png` | 1104×37 | 46×37 | No direct Def/prefab sprite reference |
| `Sprites/Shore/FindsIso/JingleShell_bleached.png` | 504×19 | 21×19 | No direct Def/prefab sprite reference |
| `Sprites/Shore/FindsIso/JingleShell_dry.png` | 504×19 | 21×19 | No direct Def/prefab sprite reference |
| `Sprites/Shore/FindsIso/JingleShell_wet.png` | 504×19 | 21×19 | No direct Def/prefab sprite reference |
| `Sprites/Shore/FindsIso/LobsterClaw_bleached.png` | 1128×47 | 47×47 | No direct Def/prefab sprite reference |
| `Sprites/Shore/FindsIso/LobsterClaw_dry.png` | 1128×47 | 47×47 | No direct Def/prefab sprite reference |
| `Sprites/Shore/FindsIso/LobsterClaw_wet.png` | 1128×47 | 47×47 | No direct Def/prefab sprite reference |
| `Sprites/Shore/FindsIso/MermaidsPurse_bleached.png` | 816×30 | 34×30 | No direct Def/prefab sprite reference |
| `Sprites/Shore/FindsIso/MermaidsPurse_dry.png` | 816×30 | 34×30 | No direct Def/prefab sprite reference |
| `Sprites/Shore/FindsIso/MermaidsPurse_wet.png` | 816×30 | 34×30 | No direct Def/prefab sprite reference |
| `Sprites/Shore/FindsIso/MoonSnail_bleached.png` | 624×28 | 26×28 | No direct Def/prefab sprite reference |
| `Sprites/Shore/FindsIso/MoonSnail_dry.png` | 624×28 | 26×28 | No direct Def/prefab sprite reference |
| `Sprites/Shore/FindsIso/MoonSnail_wet.png` | 624×28 | 26×28 | No direct Def/prefab sprite reference |
| `Sprites/Shore/FindsIso/NetFloat_bleached.png` | 792×41 | 33×41 | No direct Def/prefab sprite reference |
| `Sprites/Shore/FindsIso/NetFloat_dry.png` | 792×41 | 33×41 | No direct Def/prefab sprite reference |
| `Sprites/Shore/FindsIso/NetFloat_wet.png` | 792×41 | 33×41 | No direct Def/prefab sprite reference |
| `Sprites/Shore/FindsIso/NetScrap_bleached.png` | 1272×43 | 53×43 | No direct Def/prefab sprite reference |
| `Sprites/Shore/FindsIso/NetScrap_dry.png` | 1272×43 | 53×43 | No direct Def/prefab sprite reference |
| `Sprites/Shore/FindsIso/NetScrap_wet.png` | 1272×43 | 53×43 | No direct Def/prefab sprite reference |
| `Sprites/Shore/FindsIso/Oyster_bleached.png` | 792×34 | 33×34 | No direct Def/prefab sprite reference |
| `Sprites/Shore/FindsIso/Oyster_dry.png` | 792×34 | 33×34 | No direct Def/prefab sprite reference |
| `Sprites/Shore/FindsIso/Oyster_wet.png` | 792×34 | 33×34 | No direct Def/prefab sprite reference |
| `Sprites/Shore/FindsIso/PebbleScatter_bleached.png` | 1464×53 | 61×53 | No direct Def/prefab sprite reference |
| `Sprites/Shore/FindsIso/PebbleScatter_dry.png` | 1464×53 | 61×53 | No direct Def/prefab sprite reference |
| `Sprites/Shore/FindsIso/PebbleScatter_wet.png` | 1464×53 | 61×53 | No direct Def/prefab sprite reference |
| `Sprites/Shore/FindsIso/Periwinkle_bleached.png` | 432×21 | 18×21 | No direct Def/prefab sprite reference |
| `Sprites/Shore/FindsIso/Periwinkle_dry.png` | 432×21 | 18×21 | No direct Def/prefab sprite reference |
| `Sprites/Shore/FindsIso/Periwinkle_wet.png` | 432×21 | 18×21 | No direct Def/prefab sprite reference |
| `Sprites/Shore/FindsIso/Quahog_bleached.png` | 696×28 | 29×28 | No direct Def/prefab sprite reference |
| `Sprites/Shore/FindsIso/Quahog_dry.png` | 696×28 | 29×28 | No direct Def/prefab sprite reference |
| `Sprites/Shore/FindsIso/Quahog_wet.png` | 696×28 | 29×28 | No direct Def/prefab sprite reference |
| `Sprites/Shore/FindsIso/RazorClam_bleached.png` | 864×32 | 36×32 | No direct Def/prefab sprite reference |
| `Sprites/Shore/FindsIso/RazorClam_dry.png` | 864×32 | 36×32 | No direct Def/prefab sprite reference |
| `Sprites/Shore/FindsIso/RazorClam_wet.png` | 864×32 | 36×32 | No direct Def/prefab sprite reference |
| `Sprites/Shore/FindsIso/RopeScrap_bleached.png` | 1584×58 | 66×58 | No direct Def/prefab sprite reference |
| `Sprites/Shore/FindsIso/RopeScrap_dry.png` | 1584×58 | 66×58 | No direct Def/prefab sprite reference |
| `Sprites/Shore/FindsIso/RopeScrap_wet.png` | 1584×58 | 66×58 | No direct Def/prefab sprite reference |
| `Sprites/Shore/FindsIso/RustedCan_bleached.png` | 888×40 | 37×40 | No direct Def/prefab sprite reference |
| `Sprites/Shore/FindsIso/RustedCan_dry.png` | 888×40 | 37×40 | No direct Def/prefab sprite reference |
| `Sprites/Shore/FindsIso/RustedCan_wet.png` | 888×40 | 37×40 | No direct Def/prefab sprite reference |
| `Sprites/Shore/FindsIso/SandDollar_bleached.png` | 648×24 | 27×24 | No direct Def/prefab sprite reference |
| `Sprites/Shore/FindsIso/SandDollar_dry.png` | 648×24 | 27×24 | No direct Def/prefab sprite reference |
| `Sprites/Shore/FindsIso/SandDollar_wet.png` | 648×24 | 27×24 | No direct Def/prefab sprite reference |
| `Sprites/Shore/FindsIso/Scallop_bleached.png` | 744×30 | 31×30 | No direct Def/prefab sprite reference |
| `Sprites/Shore/FindsIso/Scallop_dry.png` | 744×30 | 31×30 | No direct Def/prefab sprite reference |
| `Sprites/Shore/FindsIso/Scallop_wet.png` | 744×30 | 31×30 | No direct Def/prefab sprite reference |
| `Sprites/Shore/FindsIso/SeaCoal_bleached.png` | 984×37 | 41×37 | No direct Def/prefab sprite reference |
| `Sprites/Shore/FindsIso/SeaCoal_dry.png` | 984×37 | 41×37 | No direct Def/prefab sprite reference |
| `Sprites/Shore/FindsIso/SeaCoal_wet.png` | 984×37 | 41×37 | No direct Def/prefab sprite reference |
| `Sprites/Shore/FindsIso/SeaGlass_bleached.png` | 456×20 | 19×20 | No direct Def/prefab sprite reference |
| `Sprites/Shore/FindsIso/SeaGlass_dry.png` | 456×20 | 19×20 | No direct Def/prefab sprite reference |
| `Sprites/Shore/FindsIso/SeaGlass_wet.png` | 456×20 | 19×20 | No direct Def/prefab sprite reference |
| `Sprites/Shore/FindsIso/SoftshellClam_bleached.png` | 720×29 | 30×29 | No direct Def/prefab sprite reference |
| `Sprites/Shore/FindsIso/SoftshellClam_dry.png` | 720×29 | 30×29 | No direct Def/prefab sprite reference |
| `Sprites/Shore/FindsIso/SoftshellClam_wet.png` | 720×29 | 30×29 | No direct Def/prefab sprite reference |
| `Sprites/Shore/FindsIso/Starfish_bleached.png` | 960×36 | 40×36 | No direct Def/prefab sprite reference |
| `Sprites/Shore/FindsIso/Starfish_dry.png` | 960×36 | 40×36 | No direct Def/prefab sprite reference |
| `Sprites/Shore/FindsIso/Starfish_wet.png` | 960×36 | 40×36 | No direct Def/prefab sprite reference |
| `Sprites/Shore/FindsIso/TrapLath_bleached.png` | 1104×148 | 92×74 | No direct Def/prefab sprite reference |
| `Sprites/Shore/FindsIso/TrapLath_dry.png` | 1104×148 | 92×74 | No direct Def/prefab sprite reference |
| `Sprites/Shore/FindsIso/TrapLath_wet.png` | 1104×148 | 92×74 | No direct Def/prefab sprite reference |
| `Sprites/Shore/FindsIso/WaveCobble_bleached.png` | 888×42 | 37×42 | No direct Def/prefab sprite reference |
| `Sprites/Shore/FindsIso/WaveCobble_dry.png` | 888×42 | 37×42 | No direct Def/prefab sprite reference |
| `Sprites/Shore/FindsIso/WaveCobble_wet.png` | 888×42 | 37×42 | No direct Def/prefab sprite reference |
| `Sprites/Shore/FindsIso/WeatheredBone_bleached.png` | 1416×54 | 59×54 | No direct Def/prefab sprite reference |
| `Sprites/Shore/FindsIso/WeatheredBone_dry.png` | 1416×54 | 59×54 | No direct Def/prefab sprite reference |
| `Sprites/Shore/FindsIso/WeatheredBone_wet.png` | 1416×54 | 59×54 | No direct Def/prefab sprite reference |
| `Sprites/Shore/FindsIso/WhelkEggMass_bleached.png` | 936×38 | 39×38 | No direct Def/prefab sprite reference |
| `Sprites/Shore/FindsIso/WhelkEggMass_dry.png` | 936×38 | 39×38 | No direct Def/prefab sprite reference |
| `Sprites/Shore/FindsIso/WhelkEggMass_wet.png` | 936×38 | 39×38 | No direct Def/prefab sprite reference |
| `Sprites/StPeters/LandingProps/SetPiece_landingTotes.png` | 240×49 | 30×49 | `Assets/_Project/Data/SetPieces/SetPiece_landingTotes.asset`<br>`Assets/_Project/Prefabs/Buildings/NineMileCreek/NmcFishPlant.prefab` |
| `Sprites/Wharf/Decor/baitBarrel.png` | 160×33 | 20×33 | No direct Def/prefab sprite reference |
| `Sprites/Wharf/Decor/bushelBaskets.png` | 352×43 | 44×43 | No direct Def/prefab sprite reference |
| `Sprites/Wharf/Decor/codFlake.png` | 752×82 | 94×82 | No direct Def/prefab sprite reference |
| `Sprites/Wharf/Decor/crateStack.png` | 240×40 | 30×40 | No direct Def/prefab sprite reference |
| `Sprites/Wharf/Decor/dockCart.png` | 368×41 | 46×41 | No direct Def/prefab sprite reference |
| `Sprites/Wharf/Decor/drumPlanter.png` | 160×27 | 20×27 | No direct Def/prefab sprite reference |
| `Sprites/Wharf/Decor/fishRack.png` | 528×95 | 66×95 | No direct Def/prefab sprite reference |
| `Sprites/Wharf/Decor/floatBags.png` | 400×43 | 50×43 | No direct Def/prefab sprite reference |
| `Sprites/Wharf/Decor/guttingTable.png` | 752×77 | 94×77 | No direct Def/prefab sprite reference |
| `Sprites/Wharf/Decor/handTruck.png` | 224×47 | 28×47 | No direct Def/prefab sprite reference |
| `Sprites/Wharf/Decor/herringSticks.png` | 512×82 | 64×82 | No direct Def/prefab sprite reference |
| `Sprites/Wharf/Decor/iceChest.png` | 416×48 | 52×48 | No direct Def/prefab sprite reference |
| `Sprites/Wharf/Decor/jerryCans.png` | 160×23 | 20×23 | No direct Def/prefab sprite reference |
| `Sprites/Wharf/Decor/netPile.png` | 576×50 | 72×50 | No direct Def/prefab sprite reference |
| `Sprites/Wharf/Decor/oilDrum.png` | 160×33 | 20×33 | No direct Def/prefab sprite reference |
| `Sprites/Wharf/Decor/pallet.png` | 352×36 | 44×36 | No direct Def/prefab sprite reference |
| `Sprites/Wharf/Decor/planterBox.png` | 384×40 | 48×40 | No direct Def/prefab sprite reference |
| `Sprites/Wharf/Decor/ropeCoil.png` | 368×31 | 46×31 | No direct Def/prefab sprite reference |
| `Sprites/Wharf/Decor/ropeHanks.png` | 400×68 | 50×68 | No direct Def/prefab sprite reference |
| `Sprites/Wharf/Decor/saltBin.png` | 480×60 | 60×60 | No direct Def/prefab sprite reference |
| `Sprites/Wharf/Decor/saltBox.png` | 384×51 | 48×51 | No direct Def/prefab sprite reference |
| `Sprites/Wharf/Decor/sortingTrough.png` | 752×72 | 94×72 | No direct Def/prefab sprite reference |
| `Sprites/Wharf/Decor/toteSingle.png` | 240×28 | 30×28 | No direct Def/prefab sprite reference |
| `Sprites/Wharf/Decor/toteStack.png` | 240×49 | 30×49 | No direct Def/prefab sprite reference |
| `Sprites/Wharf/Decor/trapSingle.png` | 288×31 | 36×31 | No direct Def/prefab sprite reference |
| `Sprites/Wharf/Decor/trapStack.png` | 288×57 | 36×57 | No direct Def/prefab sprite reference |
| `Sprites/Wharf/Decor/trashBarrel.png` | 192×34 | 24×34 | No direct Def/prefab sprite reference |
| `Sprites/Wharf/Decor/tyrePile.png` | 224×34 | 28×34 | No direct Def/prefab sprite reference |
| `Sprites/Wharf/Decor/wheelbarrow.png` | 352×43 | 44×43 | No direct Def/prefab sprite reference |
| `Sprites/Wharf/Decor/woodStack.png` | 560×62 | 70×62 | No direct Def/prefab sprite reference |

| `Sprites/Crate.png` | 32×32 | 26×26 | `Assets/_Project/Data/Traps/NmcGallantMariePotDay.asset`<br>`Assets/_Project/Data/Traps/NmcDoironYvettePotDay.asset`<br>`Assets/_Project/Data/Traps/NmcArsenaultLeoPotDay.asset` |
| `Sprites/Barrel.png` | 32×32 | 25×31 | No direct Def/prefab sprite reference |
| `Sprites/Interiors/Props/InteriorProp_seaChest.png` | 288×35 | 36×35 | No direct Def/prefab sprite reference |
| `Sprites/Fixtures/Fixture_iceCooler.png` | 240×30 | 30×30 | No direct Def/prefab sprite reference |
| `Resources/Appliances/Appliance_chestFreezer.png` | 512×546 | 64×78 | `Assets/_Project/Prefabs/Props/Kitchen/GinnyFreezer.prefab` |
| `Resources/Appliances/Appliance_chestFreezerLarge.png` | 608×595 | 76×85 | `Assets/_Project/Prefabs/Props/Kitchen/CamperFreezer.prefab` |
| `Sprites/Yard/Yard_barrelPlanter.png` | 176×30 | 22×30 | No direct Def/prefab sprite reference |

## Goods and storage definitions

Every relevant definition is listed, including research-only data. NO SPRITE means no serialized PNG reference on that Def; UI registries and separate containers may provide a representation. These must not be mistaken for a dedicated carried-goods sprite. Direct users of the legacy plain crate are listed in the sprite table.

| Definition | Kind | Sprite of its own / status |
|---|---|---|
| `Assets/_Project/Data/Boats/Containers/FishTray.asset` | DeckContainerDef | **NO SPRITE** on this definition |
| `Assets/_Project/Data/Fish/AmericanLobster.asset` | FishSpeciesDef | `Assets/_Project/Art/Fishing/Icons/ShellIcon_lobster.png` |
| `Assets/_Project/Data/Fish/AmericanOyster.asset` | FishSpeciesDef | `Assets/_Project/Art/Fishing/Icons/ShellIcon_american_oyster.png` |
| `Assets/_Project/Data/Fish/AtlanticCod.asset` | FishSpeciesDef | `Assets/_Project/Art/Fishing/Icons/FishIcon_cod.png` |
| `Assets/_Project/Data/Fish/AtlanticHerring.asset` | FishSpeciesDef | `Assets/_Project/Art/Fishing/Icons/FishIcon_herring.png` |
| `Assets/_Project/Data/Fish/AtlanticSurfClam.asset` | FishSpeciesDef | `Assets/_Project/Art/Fishing/Icons/ShellIcon_atlantic_surf_clam.png` |
| `Assets/_Project/Data/Fish/AtlanticTomcod.asset` | FishSpeciesDef | `Assets/_Project/Art/Fishing/Icons/FishIcon_tomcod.png` |
| `Assets/_Project/Data/Fish/BlueMussel.asset` | FishSpeciesDef | `Assets/_Project/Art/Fishing/Icons/ShellIcon_blue_mussel.png` |
| `Assets/_Project/Data/Fish/Cunner.asset` | FishSpeciesDef | `Assets/_Project/Art/Fishing/Icons/FishIcon_cunner.png` |
| `Assets/_Project/Data/Fish/GreenCrab.asset` | FishSpeciesDef | `Assets/_Project/Art/Fishing/Icons/ShellIcon_green_crab.png` |
| `Assets/_Project/Data/Fish/Haddock.asset` | FishSpeciesDef | `Assets/_Project/Art/Fishing/Icons/FishIcon_haddock.png` |
| `Assets/_Project/Data/Fish/Mackerel.asset` | FishSpeciesDef | `Assets/_Project/Art/Fishing/Icons/FishIcon_mackerel.png` |
| `Assets/_Project/Data/Fish/NorthernQuahog.asset` | FishSpeciesDef | `Assets/_Project/Art/Fishing/Icons/ShellIcon_northern_quahog.png` |
| `Assets/_Project/Data/Fish/Pollock.asset` | FishSpeciesDef | `Assets/_Project/Art/Fishing/Icons/FishIcon_pollock.png` |
| `Assets/_Project/Data/Fish/RainbowSmelt.asset` | FishSpeciesDef | `Assets/_Project/Art/Fishing/Icons/FishIcon_smelt.png` |
| `Assets/_Project/Data/Fish/RockCrab.asset` | FishSpeciesDef | `Assets/_Project/Art/Fishing/Icons/ShellIcon_rock_crab.png` |
| `Assets/_Project/Data/Fish/SeaScallop.asset` | FishSpeciesDef | `Assets/_Project/Art/Fishing/Icons/ShellIcon_sea_scallop.png` |
| `Assets/_Project/Data/Fish/SoftShellClam.asset` | FishSpeciesDef | `Assets/_Project/Art/Fishing/Icons/ShellIcon_soft_shell_clam.png` |
| `Assets/_Project/Data/Fish/StripedBass.asset` | FishSpeciesDef | `Assets/_Project/Art/Fishing/Icons/FishIcon_bass.png` |
| `Assets/_Project/Data/Fish/WinterFlounder.asset` | FishSpeciesDef | `Assets/_Project/Art/Fishing/Icons/FishIcon_flounder.png` |
| `Assets/_Project/Data/FishSupplyChain/bay_hotel_stock.asset` | FishStorageDef | **NO SPRITE** on this definition |
| `Assets/_Project/Data/FishSupplyChain/creek_grocery_stock.asset` | FishStorageDef | **NO SPRITE** on this definition |
| `Assets/_Project/Data/FishSupplyChain/export_customer_stock.asset` | FishStorageDef | **NO SPRITE** on this definition |
| `Assets/_Project/Data/FishSupplyChain/harbour_restaurant_stock.asset` | FishStorageDef | **NO SPRITE** on this definition |
| `Assets/_Project/Data/FishSupplyChain/island_grocery_stock.asset` | FishStorageDef | **NO SPRITE** on this definition |
| `Assets/_Project/Data/FishSupplyChain/lobster_restaurant_stock.asset` | FishStorageDef | **NO SPRITE** on this definition |
| `Assets/_Project/Data/FishSupplyChain/nmc_to_plant_cargo.asset` | FishStorageDef | **NO SPRITE** on this definition |
| `Assets/_Project/Data/FishSupplyChain/nmc_wharf.asset` | FishStorageDef | **NO SPRITE** on this definition |
| `Assets/_Project/Data/FishSupplyChain/plant_cold.asset` | FishStorageDef | **NO SPRITE** on this definition |
| `Assets/_Project/Data/FishSupplyChain/plant_dry.asset` | FishStorageDef | **NO SPRITE** on this definition |
| `Assets/_Project/Data/FishSupplyChain/plant_intake.asset` | FishStorageDef | **NO SPRITE** on this definition |
| `Assets/_Project/Data/FishSupplyChain/plant_to_bay_hotel_cargo.asset` | FishStorageDef | **NO SPRITE** on this definition |
| `Assets/_Project/Data/FishSupplyChain/plant_to_creek_grocery_cargo.asset` | FishStorageDef | **NO SPRITE** on this definition |
| `Assets/_Project/Data/FishSupplyChain/plant_to_export_customer_cargo.asset` | FishStorageDef | **NO SPRITE** on this definition |
| `Assets/_Project/Data/FishSupplyChain/plant_to_harbour_restaurant_cargo.asset` | FishStorageDef | **NO SPRITE** on this definition |
| `Assets/_Project/Data/FishSupplyChain/plant_to_island_grocery_cargo.asset` | FishStorageDef | **NO SPRITE** on this definition |
| `Assets/_Project/Data/FishSupplyChain/plant_to_lobster_restaurant_cargo.asset` | FishStorageDef | **NO SPRITE** on this definition |
| `Assets/_Project/Data/FishSupplyChain/rice_point_to_plant_cargo.asset` | FishStorageDef | **NO SPRITE** on this definition |
| `Assets/_Project/Data/FishSupplyChain/rice_point_wharf.asset` | FishStorageDef | **NO SPRITE** on this definition |
| `Assets/_Project/Data/FishSupplyChain/st_peters_to_plant_cargo.asset` | FishStorageDef | **NO SPRITE** on this definition |
| `Assets/_Project/Data/FishSupplyChain/st_peters_wharf.asset` | FishStorageDef | **NO SPRITE** on this definition |
| `Assets/_Project/Data/FuelContainers/bulk_s10k_diesel.asset` | FuelContainerDef | `Assets/_Project/Art/Sprites/FuelStorage/Iso/bulk_s10k_diesel.png` |
| `Assets/_Project/Data/FuelContainers/bulk_s10k_gas.asset` | FuelContainerDef | `Assets/_Project/Art/Sprites/FuelStorage/Iso/bulk_s10k_gas.png` |
| `Assets/_Project/Data/FuelContainers/bulk_s10k_mixed.asset` | FuelContainerDef | `Assets/_Project/Art/Sprites/FuelStorage/Iso/bulk_s10k_mixed.png` |
| `Assets/_Project/Data/FuelContainers/bulk_s10k_oil.asset` | FuelContainerDef | `Assets/_Project/Art/Sprites/FuelStorage/Iso/bulk_s10k_oil.png` |
| `Assets/_Project/Data/FuelContainers/bulk_s25k_diesel.asset` | FuelContainerDef | `Assets/_Project/Art/Sprites/FuelStorage/Iso/bulk_s25k_diesel.png` |
| `Assets/_Project/Data/FuelContainers/bulk_s25k_gas.asset` | FuelContainerDef | `Assets/_Project/Art/Sprites/FuelStorage/Iso/bulk_s25k_gas.png` |
| `Assets/_Project/Data/FuelContainers/bulk_s25k_mixed.asset` | FuelContainerDef | `Assets/_Project/Art/Sprites/FuelStorage/Iso/bulk_s25k_mixed.png` |
| `Assets/_Project/Data/FuelContainers/bulk_s25k_oil.asset` | FuelContainerDef | `Assets/_Project/Art/Sprites/FuelStorage/Iso/bulk_s25k_oil.png` |
| `Assets/_Project/Data/FuelContainers/bulk_s50k_diesel.asset` | FuelContainerDef | `Assets/_Project/Art/Sprites/FuelStorage/Iso/bulk_s50k_diesel.png` |
| `Assets/_Project/Data/FuelContainers/bulk_s50k_gas.asset` | FuelContainerDef | `Assets/_Project/Art/Sprites/FuelStorage/Iso/bulk_s50k_gas.png` |
| `Assets/_Project/Data/FuelContainers/bulk_s50k_mixed.asset` | FuelContainerDef | `Assets/_Project/Art/Sprites/FuelStorage/Iso/bulk_s50k_mixed.png` |
| `Assets/_Project/Data/FuelContainers/bulk_s50k_oil.asset` | FuelContainerDef | `Assets/_Project/Art/Sprites/FuelStorage/Iso/bulk_s50k_oil.png` |
| `Assets/_Project/Data/FuelContainers/drum_s205_diesel.asset` | FuelContainerDef | `Assets/_Project/Art/Sprites/FuelStorage/Iso/drum_s205_diesel.png` |
| `Assets/_Project/Data/FuelContainers/drum_s205_gas.asset` | FuelContainerDef | `Assets/_Project/Art/Sprites/FuelStorage/Iso/drum_s205_gas.png` |
| `Assets/_Project/Data/FuelContainers/drum_s205_mixed.asset` | FuelContainerDef | `Assets/_Project/Art/Sprites/FuelStorage/Iso/drum_s205_mixed.png` |
| `Assets/_Project/Data/FuelContainers/drum_s205_oil.asset` | FuelContainerDef | `Assets/_Project/Art/Sprites/FuelStorage/Iso/drum_s205_oil.png` |
| `Assets/_Project/Data/FuelContainers/drum_s60_diesel.asset` | FuelContainerDef | `Assets/_Project/Art/Sprites/FuelStorage/Iso/drum_s60_diesel.png` |
| `Assets/_Project/Data/FuelContainers/drum_s60_gas.asset` | FuelContainerDef | `Assets/_Project/Art/Sprites/FuelStorage/Iso/drum_s60_gas.png` |
| `Assets/_Project/Data/FuelContainers/drum_s60_mixed.asset` | FuelContainerDef | `Assets/_Project/Art/Sprites/FuelStorage/Iso/drum_s60_mixed.png` |
| `Assets/_Project/Data/FuelContainers/drum_s60_oil.asset` | FuelContainerDef | `Assets/_Project/Art/Sprites/FuelStorage/Iso/drum_s60_oil.png` |
| `Assets/_Project/Data/FuelContainers/jerry_s10_diesel.asset` | FuelContainerDef | `Assets/_Project/Art/Sprites/FuelStorage/Iso/jerry_s10_diesel.png` |
| `Assets/_Project/Data/FuelContainers/jerry_s10_gas.asset` | FuelContainerDef | `Assets/_Project/Art/Sprites/FuelStorage/Iso/jerry_s10_gas.png` |
| `Assets/_Project/Data/FuelContainers/jerry_s10_mixed.asset` | FuelContainerDef | `Assets/_Project/Art/Sprites/FuelStorage/Iso/jerry_s10_mixed.png` |
| `Assets/_Project/Data/FuelContainers/jerry_s10_oil.asset` | FuelContainerDef | `Assets/_Project/Art/Sprites/FuelStorage/Iso/jerry_s10_oil.png` |
| `Assets/_Project/Data/FuelContainers/jerry_s20_diesel.asset` | FuelContainerDef | `Assets/_Project/Art/Sprites/FuelStorage/Iso/jerry_s20_diesel.png` |
| `Assets/_Project/Data/FuelContainers/jerry_s20_gas.asset` | FuelContainerDef | `Assets/_Project/Art/Sprites/FuelStorage/Iso/jerry_s20_gas.png` |
| `Assets/_Project/Data/FuelContainers/jerry_s20_mixed.asset` | FuelContainerDef | `Assets/_Project/Art/Sprites/FuelStorage/Iso/jerry_s20_mixed.png` |
| `Assets/_Project/Data/FuelContainers/jerry_s20_oil.asset` | FuelContainerDef | `Assets/_Project/Art/Sprites/FuelStorage/Iso/jerry_s20_oil.png` |
| `Assets/_Project/Data/FuelContainers/jerry_s25_diesel.asset` | FuelContainerDef | `Assets/_Project/Art/Sprites/FuelStorage/Iso/jerry_s25_diesel.png` |
| `Assets/_Project/Data/FuelContainers/jerry_s25_gas.asset` | FuelContainerDef | `Assets/_Project/Art/Sprites/FuelStorage/Iso/jerry_s25_gas.png` |
| `Assets/_Project/Data/FuelContainers/jerry_s25_mixed.asset` | FuelContainerDef | `Assets/_Project/Art/Sprites/FuelStorage/Iso/jerry_s25_mixed.png` |
| `Assets/_Project/Data/FuelContainers/jerry_s25_oil.asset` | FuelContainerDef | `Assets/_Project/Art/Sprites/FuelStorage/Iso/jerry_s25_oil.png` |
| `Assets/_Project/Data/FuelContainers/jerry_s5_diesel.asset` | FuelContainerDef | `Assets/_Project/Art/Sprites/FuelStorage/Iso/jerry_s5_diesel.png` |
| `Assets/_Project/Data/FuelContainers/jerry_s5_gas.asset` | FuelContainerDef | `Assets/_Project/Art/Sprites/FuelStorage/Iso/jerry_s5_gas.png` |
| `Assets/_Project/Data/FuelContainers/jerry_s5_mixed.asset` | FuelContainerDef | `Assets/_Project/Art/Sprites/FuelStorage/Iso/jerry_s5_mixed.png` |
| `Assets/_Project/Data/FuelContainers/jerry_s5_oil.asset` | FuelContainerDef | `Assets/_Project/Art/Sprites/FuelStorage/Iso/jerry_s5_oil.png` |
| `Assets/_Project/Data/FuelContainers/jug_s10_diesel.asset` | FuelContainerDef | `Assets/_Project/Art/Sprites/FuelStorage/Iso/jug_s10_diesel.png` |
| `Assets/_Project/Data/FuelContainers/jug_s10_gas.asset` | FuelContainerDef | `Assets/_Project/Art/Sprites/FuelStorage/Iso/jug_s10_gas.png` |
| `Assets/_Project/Data/FuelContainers/jug_s10_mixed.asset` | FuelContainerDef | `Assets/_Project/Art/Sprites/FuelStorage/Iso/jug_s10_mixed.png` |
| `Assets/_Project/Data/FuelContainers/jug_s10_oil.asset` | FuelContainerDef | `Assets/_Project/Art/Sprites/FuelStorage/Iso/jug_s10_oil.png` |
| `Assets/_Project/Data/FuelContainers/jug_s1_diesel.asset` | FuelContainerDef | `Assets/_Project/Art/Sprites/FuelStorage/Iso/jug_s1_diesel.png` |
| `Assets/_Project/Data/FuelContainers/jug_s1_gas.asset` | FuelContainerDef | `Assets/_Project/Art/Sprites/FuelStorage/Iso/jug_s1_gas.png` |
| `Assets/_Project/Data/FuelContainers/jug_s1_mixed.asset` | FuelContainerDef | `Assets/_Project/Art/Sprites/FuelStorage/Iso/jug_s1_mixed.png` |
| `Assets/_Project/Data/FuelContainers/jug_s1_oil.asset` | FuelContainerDef | `Assets/_Project/Art/Sprites/FuelStorage/Iso/jug_s1_oil.png` |
| `Assets/_Project/Data/FuelContainers/jug_s4_diesel.asset` | FuelContainerDef | `Assets/_Project/Art/Sprites/FuelStorage/Iso/jug_s4_diesel.png` |
| `Assets/_Project/Data/FuelContainers/jug_s4_gas.asset` | FuelContainerDef | `Assets/_Project/Art/Sprites/FuelStorage/Iso/jug_s4_gas.png` |
| `Assets/_Project/Data/FuelContainers/jug_s4_mixed.asset` | FuelContainerDef | `Assets/_Project/Art/Sprites/FuelStorage/Iso/jug_s4_mixed.png` |
| `Assets/_Project/Data/FuelContainers/jug_s4_oil.asset` | FuelContainerDef | `Assets/_Project/Art/Sprites/FuelStorage/Iso/jug_s4_oil.png` |
| `Assets/_Project/Data/FuelContainers/nozzle_sAuto_diesel.asset` | FuelContainerDef | `Assets/_Project/Art/Sprites/FuelStorage/Iso/nozzle_sAuto_diesel.png` |
| `Assets/_Project/Data/FuelContainers/nozzle_sAuto_gas.asset` | FuelContainerDef | `Assets/_Project/Art/Sprites/FuelStorage/Iso/nozzle_sAuto_gas.png` |
| `Assets/_Project/Data/FuelContainers/nozzle_sAuto_mixed.asset` | FuelContainerDef | `Assets/_Project/Art/Sprites/FuelStorage/Iso/nozzle_sAuto_mixed.png` |
| `Assets/_Project/Data/FuelContainers/nozzle_sAuto_oil.asset` | FuelContainerDef | `Assets/_Project/Art/Sprites/FuelStorage/Iso/nozzle_sAuto_oil.png` |
| `Assets/_Project/Data/FuelContainers/nozzle_sHigh_diesel.asset` | FuelContainerDef | `Assets/_Project/Art/Sprites/FuelStorage/Iso/nozzle_sHigh_diesel.png` |
| `Assets/_Project/Data/FuelContainers/nozzle_sHigh_gas.asset` | FuelContainerDef | `Assets/_Project/Art/Sprites/FuelStorage/Iso/nozzle_sHigh_gas.png` |
| `Assets/_Project/Data/FuelContainers/nozzle_sHigh_mixed.asset` | FuelContainerDef | `Assets/_Project/Art/Sprites/FuelStorage/Iso/nozzle_sHigh_mixed.png` |
| `Assets/_Project/Data/FuelContainers/nozzle_sHigh_oil.asset` | FuelContainerDef | `Assets/_Project/Art/Sprites/FuelStorage/Iso/nozzle_sHigh_oil.png` |
| `Assets/_Project/Data/FuelContainers/pump_sSingle_diesel.asset` | FuelContainerDef | `Assets/_Project/Art/Sprites/FuelStorage/Iso/pump_sSingle_diesel.png` |
| `Assets/_Project/Data/FuelContainers/pump_sSingle_gas.asset` | FuelContainerDef | `Assets/_Project/Art/Sprites/FuelStorage/Iso/pump_sSingle_gas.png` |
| `Assets/_Project/Data/FuelContainers/pump_sSingle_mixed.asset` | FuelContainerDef | `Assets/_Project/Art/Sprites/FuelStorage/Iso/pump_sSingle_mixed.png` |
| `Assets/_Project/Data/FuelContainers/pump_sSingle_oil.asset` | FuelContainerDef | `Assets/_Project/Art/Sprites/FuelStorage/Iso/pump_sSingle_oil.png` |
| `Assets/_Project/Data/FuelContainers/pump_sTwin_diesel.asset` | FuelContainerDef | `Assets/_Project/Art/Sprites/FuelStorage/Iso/pump_sTwin_diesel.png` |
| `Assets/_Project/Data/FuelContainers/pump_sTwin_gas.asset` | FuelContainerDef | `Assets/_Project/Art/Sprites/FuelStorage/Iso/pump_sTwin_gas.png` |
| `Assets/_Project/Data/FuelContainers/pump_sTwin_mixed.asset` | FuelContainerDef | `Assets/_Project/Art/Sprites/FuelStorage/Iso/pump_sTwin_mixed.png` |
| `Assets/_Project/Data/FuelContainers/pump_sTwin_oil.asset` | FuelContainerDef | `Assets/_Project/Art/Sprites/FuelStorage/Iso/pump_sTwin_oil.png` |
| `Assets/_Project/Data/FuelContainers/skid_s1200_diesel.asset` | FuelContainerDef | `Assets/_Project/Art/Sprites/FuelStorage/Iso/skid_s1200_diesel.png` |
| `Assets/_Project/Data/FuelContainers/skid_s1200_gas.asset` | FuelContainerDef | `Assets/_Project/Art/Sprites/FuelStorage/Iso/skid_s1200_gas.png` |
| `Assets/_Project/Data/FuelContainers/skid_s1200_mixed.asset` | FuelContainerDef | `Assets/_Project/Art/Sprites/FuelStorage/Iso/skid_s1200_mixed.png` |
| `Assets/_Project/Data/FuelContainers/skid_s1200_oil.asset` | FuelContainerDef | `Assets/_Project/Art/Sprites/FuelStorage/Iso/skid_s1200_oil.png` |
| `Assets/_Project/Data/FuelContainers/skid_s2500_diesel.asset` | FuelContainerDef | `Assets/_Project/Art/Sprites/FuelStorage/Iso/skid_s2500_diesel.png` |
| `Assets/_Project/Data/FuelContainers/skid_s2500_gas.asset` | FuelContainerDef | `Assets/_Project/Art/Sprites/FuelStorage/Iso/skid_s2500_gas.png` |
| `Assets/_Project/Data/FuelContainers/skid_s2500_mixed.asset` | FuelContainerDef | `Assets/_Project/Art/Sprites/FuelStorage/Iso/skid_s2500_mixed.png` |
| `Assets/_Project/Data/FuelContainers/skid_s2500_oil.asset` | FuelContainerDef | `Assets/_Project/Art/Sprites/FuelStorage/Iso/skid_s2500_oil.png` |
| `Assets/_Project/Data/FuelContainers/skid_s500_diesel.asset` | FuelContainerDef | `Assets/_Project/Art/Sprites/FuelStorage/Iso/skid_s500_diesel.png` |
| `Assets/_Project/Data/FuelContainers/skid_s500_gas.asset` | FuelContainerDef | `Assets/_Project/Art/Sprites/FuelStorage/Iso/skid_s500_gas.png` |
| `Assets/_Project/Data/FuelContainers/skid_s500_mixed.asset` | FuelContainerDef | `Assets/_Project/Art/Sprites/FuelStorage/Iso/skid_s500_mixed.png` |
| `Assets/_Project/Data/FuelContainers/skid_s500_oil.asset` | FuelContainerDef | `Assets/_Project/Art/Sprites/FuelStorage/Iso/skid_s500_oil.png` |
| `Assets/_Project/Data/FuelContainers/tote_s1000_diesel.asset` | FuelContainerDef | `Assets/_Project/Art/Sprites/FuelStorage/Iso/tote_s1000_diesel.png` |
| `Assets/_Project/Data/FuelContainers/tote_s1000_gas.asset` | FuelContainerDef | `Assets/_Project/Art/Sprites/FuelStorage/Iso/tote_s1000_gas.png` |
| `Assets/_Project/Data/FuelContainers/tote_s1000_mixed.asset` | FuelContainerDef | `Assets/_Project/Art/Sprites/FuelStorage/Iso/tote_s1000_mixed.png` |
| `Assets/_Project/Data/FuelContainers/tote_s1000_oil.asset` | FuelContainerDef | `Assets/_Project/Art/Sprites/FuelStorage/Iso/tote_s1000_oil.png` |
| `Assets/_Project/Data/FuelContainers/tote_s600_diesel.asset` | FuelContainerDef | `Assets/_Project/Art/Sprites/FuelStorage/Iso/tote_s600_diesel.png` |
| `Assets/_Project/Data/FuelContainers/tote_s600_gas.asset` | FuelContainerDef | `Assets/_Project/Art/Sprites/FuelStorage/Iso/tote_s600_gas.png` |
| `Assets/_Project/Data/FuelContainers/tote_s600_mixed.asset` | FuelContainerDef | `Assets/_Project/Art/Sprites/FuelStorage/Iso/tote_s600_mixed.png` |
| `Assets/_Project/Data/FuelContainers/tote_s600_oil.asset` | FuelContainerDef | `Assets/_Project/Art/Sprites/FuelStorage/Iso/tote_s600_oil.png` |
| `Assets/_Project/Data/GoodsModelResearch/FishTub460Research.asset` | GoodsContainerDef | **NO SPRITE** on this definition |
| `Assets/_Project/Data/GoodsModelResearch/FishTub660Research.asset` | GoodsContainerDef | **NO SPRITE** on this definition |
| `Assets/_Project/Data/GoodsModelResearch/HandPailResearch.asset` | GoodsContainerDef | **NO SPRITE** on this definition |
| `Assets/_Project/Data/GoodsModelResearch/LowPanResearch.asset` | GoodsContainerDef | **NO SPRITE** on this definition |
| `Assets/_Project/Data/GoodsModelResearch/PortableTote65Research.asset` | GoodsContainerDef | **NO SPRITE** on this definition |
| `Assets/_Project/Data/GoodsModelResearch/PortableTote70Research.asset` | GoodsContainerDef | **NO SPRITE** on this definition |
| `Assets/_Project/Data/Resources/IslandSupply/Carriers/AtvFrontRack.asset` | CarrierDef | **NO SPRITE** on this definition |
| `Assets/_Project/Data/Resources/IslandSupply/Carriers/AtvRearRack.asset` | CarrierDef | **NO SPRITE** on this definition |
| `Assets/_Project/Data/Resources/IslandSupply/Carriers/AtvTrailer.asset` | CarrierDef | **NO SPRITE** on this definition |
| `Assets/_Project/Data/Resources/IslandSupply/Carriers/Drum205.asset` | CarrierDef | **NO SPRITE** on this definition |
| `Assets/_Project/Data/Resources/IslandSupply/Carriers/GardenWagon.asset` | CarrierDef | **NO SPRITE** on this definition |
| `Assets/_Project/Data/Resources/IslandSupply/Carriers/HandCart.asset` | CarrierDef | **NO SPRITE** on this definition |
| `Assets/_Project/Data/Resources/IslandSupply/Carriers/HandPail.asset` | CarrierDef | **NO SPRITE** on this definition |
| `Assets/_Project/Data/Resources/IslandSupply/Carriers/JerryCan20.asset` | CarrierDef | **NO SPRITE** on this definition |
| `Assets/_Project/Data/Resources/IslandSupply/Carriers/PalletGma.asset` | CarrierDef | **NO SPRITE** on this definition |
| `Assets/_Project/Data/Resources/IslandSupply/Carriers/PickupBed.asset` | CarrierDef | **NO SPRITE** on this definition |
| `Assets/_Project/Data/Resources/IslandSupply/Carriers/Rucksack.asset` | CarrierDef | **NO SPRITE** on this definition |
| `Assets/_Project/Data/Resources/IslandSupply/Carriers/ShoppingBag.asset` | CarrierDef | **NO SPRITE** on this definition |
| `Assets/_Project/Data/Resources/IslandSupply/Carriers/TractorBucket.asset` | CarrierDef | **NO SPRITE** on this definition |
| `Assets/_Project/Data/Resources/IslandSupply/Carriers/TractorForks.asset` | CarrierDef | **NO SPRITE** on this definition |
| `Assets/_Project/Data/Resources/IslandSupply/Goods/Bait.asset` | IslandGoodDef | **NO SPRITE** on this definition |
| `Assets/_Project/Data/Resources/IslandSupply/Goods/Gasoline.asset` | IslandGoodDef | **NO SPRITE** on this definition |
| `Assets/_Project/Data/Resources/IslandSupply/Goods/Groceries.asset` | IslandGoodDef | **NO SPRITE** on this definition |
| `Assets/_Project/Data/Resources/IslandSupply/Goods/Hardware.asset` | IslandGoodDef | **NO SPRITE** on this definition |
| `Assets/_Project/Data/Resources/IslandSupply/Goods/Ice.asset` | IslandGoodDef | **NO SPRITE** on this definition |
| `Assets/_Project/Data/Resources/IslandSupply/Goods/MailParcels.asset` | IslandGoodDef | **NO SPRITE** on this definition |
| `Assets/_Project/Data/Resources/IslandSupply/Goods/MedicineMisc.asset` | IslandGoodDef | **NO SPRITE** on this definition |
| `Assets/_Project/Data/Resources/IslandSupply/Goods/Propane.asset` | IslandGoodDef | **NO SPRITE** on this definition |
| `Assets/_Project/Data/Resources/IslandSupply/Goods/StoveOil.asset` | IslandGoodDef | **NO SPRITE** on this definition |

| `Assets/_Project/Data/Resources/Catalog/Bait/SquidStrip.asset` | BaitDef | **NO SPRITE** on this definition |
| `Assets/_Project/Data/Resources/Catalog/Bait/ShuckedClam.asset` | BaitDef | **NO SPRITE** on this definition |
| `Assets/_Project/Data/Resources/Catalog/Bait/SeaWorm.asset` | BaitDef | **NO SPRITE** on this definition |
| `Assets/_Project/Data/Resources/Catalog/Bait/Mackerel.asset` | BaitDef | **NO SPRITE** on this definition |
| `Assets/_Project/Data/Resources/Catalog/Bait/Herring.asset` | BaitDef | **NO SPRITE** on this definition |
| `Assets/_Project/Data/Resources/Catalog/Bait/FishScrap.asset` | BaitDef | **NO SPRITE** on this definition |
| `Assets/_Project/Data/Resources/Catalog/Bait/Capelin.asset` | BaitDef | **NO SPRITE** on this definition |
| `Assets/_Project/Data/Resources/Catalog/Supplies/TinnedBeans.asset` | SupplyDef | **NO SPRITE** on this definition |
| `Assets/_Project/Data/Resources/Catalog/Supplies/SoapBar.asset` | SupplyDef | **NO SPRITE** on this definition |
| `Assets/_Project/Data/Resources/Catalog/Supplies/Ice.asset` | SupplyDef | **NO SPRITE** on this definition |
| `Assets/_Project/Data/Resources/Catalog/Supplies/Bread.asset` | SupplyDef | **NO SPRITE** on this definition |
| `Assets/_Project/Data/Resources/Catalog/Supplies/Oatcakes.asset` | SupplyDef | **NO SPRITE** on this definition |
| `Assets/_Project/Data/FishSupplyChain/canned_line.asset` | FishLineDef | **NO SPRITE** on this definition |
| `Assets/_Project/Data/FishSupplyChain/lobster_line.asset` | FishLineDef | **NO SPRITE** on this definition |
| `Assets/_Project/Data/FishSupplyChain/frozen_line.asset` | FishLineDef | **NO SPRITE** on this definition |
| `Assets/_Project/Data/FishSupplyChain/fresh_fillet_line.asset` | FishLineDef | **NO SPRITE** on this definition |
| `Assets/_Project/Data/FishPlantIntake/CodFilletSlot.asset` | GradedFishLineDef | **NO SPRITE** on this definition |

## Consequences

At the source snapshot, the nine island supply goods describe containers as text and have no sprite property. The research container, carrier and fish-storage rows are planning data, not evidence of rendered fill states. Fish species and gear may resolve icons through a registry rather than their own asset. Any missing hand/deck/wharf presentation in those planning rows needs explicit connections; this is not a claim that main lacks its implemented shopping-bag path.

Review evidence is the native 2x full-sheet contact sheet retained outside version control. All sources were checked for LFS pointers first and recovered from local objects only. No scene, prefab, definition, importer or source image changed in this audit.
