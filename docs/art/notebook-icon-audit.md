# Notebook icon readability audit

Base: `1abca5974ce3d16a3257777797bb2547c9ad8d53`. Source-pixel review; no runtime acceptance is claimed.

The evidence sheet `notebook-12-all.png` contains all 49 mapped KIT and CAUGHT identities at native 1x and nearest-neighbour 3x on paper RGB (235, 226, 204), rounded from `NotebookInk.Paper`. KIT follows `GearPage.asset`: Rods and tackle (7), Pots and lines (3), Bait (7), Shore tools (6), Boat electronics (4), Carried (2), then Other gear (1). CAUGHT includes 19 species, sorted by id for a stable sheet; the game uses first-caught order. The six missing instrument identities have no source icon and are listed separately in the missing-icon audit. Other library keys (boats, currency, hold, buoy and licence) are not KIT/CAUGHT content.

The native-size audit is not a simulation of the 18x18 Notebook slot or book scaling. Thin equipment and long fish may lose additional detail there. The sheet remains review evidence outside version control.

## Ten weakest eligible icons

Rank prioritizes recognition at 1x, then contour consistency and contrast against paper. Green lobster and rock crab are protected. Herring bait, mackerel bait and squid-strip bait are excluded because PR 1376 is open and absent from this base. They remain visible in the full sheet.

| Rank | Identity | File | Canvas | Observed fault |
|---:|---|---|---|---|
| 1 | `tackle.cod_jig` | `Assets/_Project/Art/Fishing/Icons/GearIcon_cod_jig.png` | 18 x 30 | The pale narrow body and open hook dissolve into paper at 1x; no dark edge separates metal from stock. |
| 2 | `gear.oyster_tongs` | `Assets/_Project/Art/Fishing/Icons/GearIcon_oyster_tongs.png` | 49 x 21 | Both pale handles blend into the paper; the small jaw reads as a spoon rather than opposing tines. |
| 3 | `tackle.spoon` | `Assets/_Project/Art/Fishing/Icons/GearIcon_spoon.png` | 36 x 18 | The broad gold body has almost no dark contour; the hook and attachment vanish at 1x. |
| 4 | `gear.shellfish_rake` | `Assets/_Project/Art/Fishing/Icons/GearIcon_shellfish_rake.png` | 41 x 20 | The handle reads, but the bright perforated head loses its teeth and looks like a flat shovel. |
| 5 | `tackle.spinner` | `Assets/_Project/Art/Fishing/Icons/GearIcon_spinner.png` | 20 x 26 | The pale shaft and loose coloured beads do not form a continuous readable lure; hook is faint. |
| 6 | `tackle.mackerel_feathers` | `Assets/_Project/Art/Fishing/Icons/GearIcon_mackerel_feathers.png` | 18 x 37 | Thin pale leader fragments around the feathers; coloured marks read as disconnected confetti at 1x. |
| 7 | `fish.rainbow_smelt` | `Assets/_Project/Art/Fishing/Icons/FishIcon_smelt.png` | 45 x 10 | Very low contrast belly and slender tail fade into paper; the silhouette reads less firmly than mackerel. |
| 8 | `bait.capelin` | `Assets/_Project/Art/Fishing/Icons/GearIcon_bait_capelin.png` | 39 x 23 | Overlapping pale fish form a fork-like spray at 1x; heads and tails are difficult to separate. |
| 9 | `bait.shucked_clam` | `Assets/_Project/Art/Fishing/Icons/GearIcon_bait_shucked_clam.png` | 30 x 28 | Bright yellow contents dominate the cup and read like kernels; flesh lobes lack a clear silhouette. |
| 10 | `tackle.soft_bait` | `Assets/_Project/Art/Fishing/Icons/GearIcon_soft_bait.png` | 34 x 14 | Square jig head is much heavier than the curl; bright body texture obscures its contour. |

Ranks 1-3 form the first redraw set; ranks 4-6 form the second. Rod and rod rack provide the tackle contour reference; shovel and bucket provide the shore-equipment reference. Use the actual neighbouring PNG colours, native dimensions and unchanged import metadata.

This ranking is a visual judgement for review, not an accepted new look. Long fish and small tackle remain intrinsically difficult at the actual Notebook slot size.
