# Fishing gear art: where it shows and what is owed (2026-11-20)

Catalog icons for the gear below were baked by `docs/art/rigs/gear-icons/bakeGearIcons.js` into
`Art/Fishing/Icons/GearIcon_<leaf>.png` and wired by id in `Resources/IconLibrary.asset`
(`GearIconTests` pins each entry, its import settings and that the id is declared by shipped data).
None of these Defs carries a sprite field: the icon library is the only icon slot gear has.

## Where each piece shows today

| Gear (id) | Def / offer | Shop catalog | Notebook | In hand | On deck |
|---|---|---|---|---|---|
| Rod (`gear.rod`, `tool.rod`) | `Catalog/Gear/Rod`, `Tools/Rod` | Listed, text row | KIT page, when owned | the held mesh (`HeldMeshes`); `Sprites/Gear/Rod.png` stays the world/fallback sprite | rod rack |
| Oyster tongs (`gear.oyster_tongs`) | `Catalog/Gear/OysterTongs` | unlisted | KIT page, when owned | none (no `ToolDef`) | none |
| Shellfish rake (`gear.shellfish_rake`) | `Catalog/Gear/ShellfishRake` | unlisted | KIT page, when owned | none (no `ToolDef`) | none |
| Mussel sack (`gear.mussel_sack`) | `Catalog/Gear/MusselSack` | unlisted | KIT page, when owned | none (carry capacity only) | none |
| Peck basket (`gear.shellfish_basket`) | `Catalog/Gear/ShellfishBasket` | unlisted | KIT page, when owned | none (carry capacity only) | none |
| Lures and jigs (`tackle.*`, five) | `Data/Tackle/*` | not sold as items | KIT page, when owned | none | none |
| Longline tub (`longline.groundfish_tub`) | `Data/Longlines/GroundfishTub` | not sold | KIT page, when owned | none | `deckwork.longline_tub` is named but no deck-work Def exists |
| Trawl net (`trawl_gear.bottom_otter_prototype`) | `Data/Trawls/BottomOtterPrototype` (unreviewed prototype) | not sold | none | none | the trawler hulls draw their own net piles |

The Notebook's KIT page (the Gear page) is the first screen that reads a gear id from the icon
library: it draws each owned piece with its icon, or a paper placeholder where an id has none. The
catalog book is still text only, and the sell screen, catch card and hold list read species ids.

## Owed (not in the icon pass)

- Held meshes for the tongs, the rake, the sack and the basket, with `ToolDef`s, once they are used by hand.
- Deck overlays: the longline tub (and its `deckwork.longline_tub` Def), a trawl net on the reel or pile for the small boats.
- Lures on the line: a jig, spoon, spinner, feathers and soft bait drawn at the rod tip in the fight.
- A picture slot in the catalog book, so these icons show where the gear is bought.

## Pass 2: the ids the Notebook's KIT page still lettered (2026-11-20)

The same bake (akeGearIcons.js, the PASS 2 block) draws fifteen more icons, wired by id in the icon
library and pinned by GearIconTests like the first pass. The extra ramps are lifted from the rigs that
own those things (watch face, helm sonar, trap rig, the NMC store's meat) and are handed only to the
pass-2 pieces, so the first-pass icons re-bake byte for byte.

| Id | Icon | What it shows |
|---|---|---|
| `watch.harbour_digital` | `GearIcon_harbour_watch` | the black resin digital watch, grey face ring, LCD reading 12, strap up and down |
| `gear.rod_rack` | `GearIcon_rod_rack` | four rods upright in a wooden rack, cork grips and reels low |
| `trap.lobster` | `GearIcon_lobster_pot` | trapIsoRig's wooden bow-top build (the world `PotWood`), bare oak |
| `trap.crab` | `GearIcon_crab_pot` | trapIsoRig's wire crab pot build (the world `PotWire`), black vinyl |
| `bait.herring` | `GearIcon_bait_herring` | a net bait bag of herring, tails out of the tied neck |
| `bait.mackerel` | `GearIcon_bait_mackerel` | three cut chunks, barred green skin on top, red cut faces |
| `bait.capelin` | `GearIcon_bait_capelin` | a fanned bunch of small slim fish |
| `bait.squid_strip` | `GearIcon_bait_squid_strip` | three white strips on a cutting board |
| `bait.sea_worm` | `GearIcon_bait_sea_worm` | two red-brown worms on a pad of rockweed |
| `bait.shucked_clam` | `GearIcon_bait_shucked_clam` | a white tub heaped with clam meats |
| `bait.fish_scrap` | `GearIcon_bait_fish_scrap` | a wooden fish box of heads, tails and a frame |
| `instrument.depth_sounder` | `GearIcon_depth_sounder` | a graphite head on its bracket, grey LCD depth, transducer puck on its cable |
| `instrument.fish_finder` | `GearIcon_fish_finder` | a colour sonar screen: surface line, two fish arches, banded bottom |
| `instrument.loran` | `GearIcon_loran` | a deep set with amber digits, a keypad and the antenna lead |
| `instrument.gps` | `GearIcon_gps` | a landscape chartplotter: blue water, tan coast, the boat and its track |

Still owed after pass 2: the headlamp has no icon (nothing records it as owned, so the KIT page does
not list it), and any owned id the KIT page Def does not list falls back to the placeholder.
