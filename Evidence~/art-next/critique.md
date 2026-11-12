# Characters and trees: what a player sees today, and the next iteration

Written 2026-11-11 from the rigs' own reference renderers (character rig 10.3, tree rig 4) at game scale,
on grass, wet sand, planks and water, at dawn, noon and dusk. Most visible first. Each point: what the
player sees, the cause in the rig, the fix in the new variant, and what it costs.

Nothing that ships is touched. The fixes live in two new rig sources beside the old ones:
`docs/art/rigs/character/rig11/` (global `CharacterIso11`, revision 11.0) and
`docs/art/rigs/treeIsoRig5.js` (global `TreeRig5`). No baker, Def, scene or sheet reads them yet.

## Characters (fisher: the player; nan: the elder woman, full frame)

| # | What the player sees | Cause in rig 10.3 | Fix in rig 11.0 | Cost |
|---|---|---|---|---|
| 1 | A figure has no light side. Boats and trees beside it are lit from the upper left; the person is lit from straight above, so shirt, apron and trousers read as flat cut-outs. | `SHADING.key` is the fleet key with its left component removed, so that east mirrors west to the pixel. | The key keeps 62% of the fleet's left component. Screen left is now the lit side at every facing. | None at run time: the mesh figure reads the key as data (`KeyScreen`), the shader is unchanged. East no longer mirrors west in shading; the outline still does (checked). |
| 2 | The head and hands are small; at 64 px the face is two dark dots and the figure reads as a thin stick. | True proportions: head scale 0.65, palm 0.065 m, limb radii at life size. The art bible asks for a marginally larger head and hands and a chunkier silhouette. | Head 0.73 (+12%), palm 0.076 m, hand radius x1.16, limb radius x1.12, shoulders and hips a touch wider. Nan's face is now readable under the kerchief. | Same triangle count (fisher 1,165, nan 1,252), same 28 bones, same clips. The crown rises 3 cm (fisher 1.792 m to 1.822 m): hat and head sockets move with it, hand and foot sockets do not. |
| 3 | Cloth is one tone. The white shirt is a white patch; the dark apron is a dark patch. | The tone gain (2.4) is too low for the turn of a torso to cross a step. | Gain 3.0, same four-tone window per material, so the rig's own four-tone rule still holds. | None. |
| 4 | Skin and pale cloth melt into wet sand at noon. | No outline and no contact shadow in the figure itself; separation relies on the ground. | Partly helped by 1 and 3 (the shade side now carries a darker edge). Not solved: a tinted keyline exists in the rig (`keyline`, off by default) but the mesh figure has no outline pass. | Left for a later pass; an outline in the figure shader is a shared-shader change and would need a switch. |
| 5 | Boots are dark blobs with spiky toes on the diagonals. | Boot geometry: a long toe box at 32 px per metre rasterises to a 1 px spike. | Not changed in 11.0. | A geometry edit to the boot, cheap to bake, needs its own look. |
| 6 | The walk is stiff: elbows held out, no turn in the shoulders. | Walk clip keys in the poses file. | Not changed in 11.0: the clips are shared by every gameplay pin (ladder, helm, carry) and a walk change needs its own pass with the pins re-proven. | Deferred on purpose. |
| 7 | From behind, nan's kerchief shows a pale band that reads like a face. | The kerchief knot is drawn in the collar tone. | Not changed in 11.0. | A material change on one part. |

Taste calls, stated plainly:
- The side share is 0.62, not the full fleet key. At 1.0 the right arm and right leg drop out of their
  base tone and the figure looks half in shadow at noon. At 0 (today) there is no light side at all.
- The head is 12% larger, not the 20 to 25% of a Stardew figure. Larger than this the fisher no longer
  matches the boats, which are drawn at true scale, and the helm and ladder poses start to clip.

Rig 11 passes all of its inherited golden checks on both builds. One check was restated, because rig 10's
version asserts the very thing point 1 removes (east equals west mirrored, to the pixel). Rig 11's version
asserts instead: one outline east and west, a key that leans left, and screen left measurably the lit side.

## Trees (red maple, red spruce, white cedar: the three commonest species across the two key scenes)

| # | What the player sees | Cause in rig 4 | Fix in rig 5 | Cost |
|---|---|---|---|---|
| 1 | The red maple (the commonest tree in St Peters, second in Nine Mile Creek) is a fan of bare sticks holding separate leaf puffs. You see through the middle of the crown. It reads as a sapling or a bonsai, not a 12 m tree. | The crown is a single ring of 4 to 5 leaf masses on the rim of an ellipse; the centre holds only a small dark heart. The fork is high and the lower crown is open. | Five more leaf masses across the face of the crown, each its own mass (so it takes its own light and moves on its own in wind); a slightly lower, deeper crown; one more primary limb; a second hanging mass. Gaps stay at the rim where sky belongs. | Cell 301x321 to 308x314: the same texture memory to within 0.1%. Leaf stamps roughly double (6,728 to 12,557 over four variants): bake time up 7% (747 ms to 798 ms for four variants, lit view only). No change at run time. |
| 2 | Crowns have no light side. A stand is one even mid-dark green. | The normal of each stamp is blended leaf, floret, crown; the crown's share is 36%. | Crown share raised to 46 to 50% for these three species, so each tree has a lit shoulder and a shade side before it has leaves. | None: a bake constant. |
| 3 | The maple's trunk is a pencil under a wide crown. | Trunk diameter is the true 0.60 m. | 0.74 m: the same heroic allowance the art bible gives people. | None. Known wart: on the thicker trunk the crown's shadow band reads as a hard dark block; it wants a softer edge. |
| 4 | The red spruce is a smooth dark cone. No boughs. | 17 thin tiers, evenly spaced, overlap into one surface. | 15 tiers bunched toward the top, bigger plates, more droop with lifted tips, more missing boughs and a stronger lean to one side. It now has shelves with shadow between them. | Cell 244x332 to 271x331: +10.7% texture memory per map (1,266 KiB to 1,402 KiB uncompressed for the four-variant sheet). Fewer parts, so the bake is 19% faster. |
| 5 | The white cedar is a dull green post with no foot. | Taper 0.45 gives a column; the crown starts at 10% of the height; 20 tiers of small sprays. | A cone that is widest low (taper 0.66), fewer and larger sprays, the crown lifted to show the shredded trunk, twin stems drawn closer so they read as one tree. | Cell 140x228 to 160x226: +13.3% per map (499 KiB to 565 KiB). Bake 15% faster. |
| 6 | In a stand the same four shapes repeat. | Four variants per species, fixed. | Not changed. | More variants cost memory in direct proportion; rotation and scale jitter at placement would be cheaper. |

All twelve new trees (three species, four variants) pass the rig's own mass audit, 4 of 4 each.

Taste calls:
- The maple keeps visible limbs under the crown. The alternative, a closed lollipop crown down to the
  fork, reads cleaner at a distance but loses the species; red maples do show their forks.
- The spruce and cedar are allowed to cost 11% and 13% more texture memory. The alternative that fits the
  old cell (keep the crown width, change only the tiers) was tried first and still read as a cone. Texture
  memory is over budget, so this is for the owner to accept or refuse per species; the maple costs nothing.

## What is and is not shown

The contact sheets are drawn by each rig's own reference renderer in Node: the same code the bakers
evaluate, same camera, scale, sky model and wind loop. They are not in-engine plates. The new variants
are not baked into Unity assets yet. For characters that needs a bake entry that takes a rig kit and
writes one preset to its own path and id (the baker reads only the live kit today), and the committed-skin
guard test read per kit. For trees it needs the pass-4 baker and maps file, which name rig 4 directly,
cloned or parameterised, and its own folder and contract (the shipped folder is pinned at 126 files).
