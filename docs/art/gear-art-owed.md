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
