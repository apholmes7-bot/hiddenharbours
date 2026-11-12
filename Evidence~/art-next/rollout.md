# Rolling the next iteration out: what a yes would cost

Nothing here is done. The two skins and three species sit beside the shipped art, drawn by nothing. This
note says what each yes would set in motion, so the owner can say yes to one line and no to the next.
The critique (`critique.md`) says what each change looks like and why.

## What exists today

| Piece | Where | Drawn by the game |
|---|---|---|
| Character rig 11.0 (source, checks, two gameplay sidecars) | `docs/art/rigs/character/rig11/` | no |
| `charskin.fisher_rig11`, `charskin.nan_rig11` | `Assets/_Project/Data/Characters/SkinNext/` | no |
| Tree rig 5 (source) | `docs/art/rigs/treeIsoRig5.js` | no |
| RedMaple, RedSpruce, WhiteCedar: mature, summer, 4 variants each, 6 channels | `Assets/_Project/Art/Foliage/TreesNext/` | no |

## Characters: three ways to say yes

1. **Whole cast to rig 11 (recommended if the look is liked).** Bake the other eight presets from rig 11,
   point the live kit at rig 11, re-bake the ten shipped skins in place. Ids and guids do not move, so no
   scene, save or Def reference changes. Cost: ten ~2 MB text assets rewritten, eight more gameplay
   sidecars, and every pin that reads a rig-10 number re-proven (seated helm, ladder, carry, hand marks:
   rig 11 changes head, hand and limb sizes, so reach and hold points shift by a pixel or two). One PR
   for the bake, one for the pins. No texture memory: figures are meshes.
2. **Player only.** Swap the fisher alone. Rejected as a recommendation: rig 11 lights a figure from the
   left and rig 10 from above, so the player would be lit differently from everyone they stand beside.
3. **Keep rig 10, take the tone gain only.** The cloth-shading change (critique row 3) can be made inside
   rig 10 as 10.4 without touching proportions, so no gameplay pin moves. Cheapest; loses the light side
   and the larger head and hands.

Not in any of these: boots, the walk clip, nan's kerchief from behind (critique rows 5 to 7). Each wants
its own look.

## Trees: per species

The three are independent. Each yes re-bakes that species' sheets in the shipped kit from rig 5 (every season
and stage the kit carries for it, not only mature summer) and leaves the others on rig 4.

| Species | Planted in St Peters | Cell today → next | Texture memory | Note |
|---|---|---|---|---|
| RedMaple | 76 | 301x321 → 308x314 | about the same (+0.1%) | the biggest visible change; a closed crown |
| RedSpruce | 33 | 244x332 → 271x331 | +10.7% for this species | boughs with shadow between them |
| WhiteCedar | 4 | 140x228 → 160x226 | +13.3% for this species | a cone with a foot, not a post |

Texture memory is over budget already, so the spruce and cedar are a spend, the maple is not. A cell that
changes size moves the trunk anchor in the contract; placement reads the anchor from the contract, so
planted trees stay on their roots (the in-game plates were shot that way), but the kit's pinned file
count, sheet sizes and digests all move and need re-pinning in one PR per species.

The rest of the kit (seven more species) stays on rig 4 until someone looks at each one. Rig 5 was
written for these three; what it does to the other seven has not been looked at.

Not shot in-engine: the trees in a gale. The wind maps bake through the same pass as rig 4's, and the
rig-renderer sheet shows the loop, but nobody has watched the new crowns blow in the game.

## If the answer is no

Delete `SkinNext/`, `TreesNext/`, `rig11/`, `treeIsoRig5.js`, the rig 11 lines in the
kit table and the rig catalog, and the proposal scope in the tree baker. Nothing else refers to them. The two ledger rows are the one judgment call: ids are append-only, so
either the rows stay as retired ids or lead-architect rules that a never-shipped id may be removed.
