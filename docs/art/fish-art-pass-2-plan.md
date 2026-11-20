# Fish art, pass 2: tight icons, a size floor, and what is owed (2026-11-17)

Pass 1 (PR 1308) put the new fish art everywhere a fish is drawn, at its own size. Four things
still read badly. This note covers what this pass fixes, what it measured, and the plan for the rest.
It marks each step that needs a warm editor (a bake or a look) and each step that does not.

| # | Problem | This pass | Still owed |
|---|---|---|---|
| a | The icons draw at about 40% of the slot width | **Fixed:** a separate icon trimmed tight (§1) | Nothing: herring and mackerel icon-scale art landed 2026-11-20 (§1.3) |
| b | The small fish barely read in his hands | **A size floor** shared by line, hands and tote (§2) | Contrast at the small rungs, the flounder face-on, mesh held props (§2.3, §3) |
| c | No fish at the stove, locker or freezer | Nothing | The plan in §4 |
| d | No held-prop meshes per species and size | Nothing | The plan in §3 (ADR 0053 PRs 1, 3 and 4) |

## 1. The icons

### 1.1 Why the icons came out small

The catch card (64 px) and the sell screen (40 px) draw the icon in a square slot with
`preserveAspect`. Pass 1 pointed the icon at the large rung's deck cell
(`Fish_<sp>_lg_deck_d2_f0`). That cell is 64x80 with the fish drawn in its middle, so the slot
fitted the cell's 80 px height (a 0.8 scale) and the fish came out at 18-58% of the slot's width.

### 1.2 The fix

`FishIcon_<sp>.png` under `Art/Fishing/Icons/` is the same cell, cut to the fish's opaque bounds,
with the locked import settings (32 PPU, Point, uncompressed, no mips, Single). The deck sheet is
not touched, because the slicer owns its rects and the slice tests pin its sprite names and count.
`FishIconBuilder` (menu *Art/Import (after a new drop)/Build Fish Icons*) cuts the icons and wires
the `IconLibrary` entries (seven fish and the cod licence) and each species def's `Sprite`. The
tests compare every committed icon with its cell's trim, pixel for pixel, so a re-bake that is not
followed by a re-cut fails CI.

| Species | Icon (px) | Width in a 64 px slot, before → after |
|---|---|---|
| cod | 38x12 | 48% → 100% |
| haddock | 29x9 | 36% → 100% |
| pollock | 34x10 | 43% → 100% |
| mackerel | 19x6 | 24% → 100% |
| bass | 46x13 | 58% → 100% |
| flounder | 21x7 | 26% → 100% |
| herring | 14x3 | 18% → 100% |

The test floor is 80% of the slot width.

### 1.3 Owed: icon-scale art for the herring (and probably the mackerel)

A 14x3 herring drawn in a 64 px slot is scaled 4.6x, so it reads as a few chunky pixels. The fix
is art, not code: an icon-scale render from the rig, the way the shellfish keep their flat icons
until icon-scale shell art exists (PR 1308). The art-pipeline rule "never scale a sprite to fake
size" applies to the world. An icon is a picture of the fish, so a separate icon render is fair.

**Done (2026-11-20).** `docs/art/rigs/owed-icons/bakeOwedIcons.js` renders the same deck lay (row 2,
frame 0) from the catch pass 2 rig at icon scale (herring 5.0, mackerel 2.8) into
`docs/art/rigs/coast-species-kit/icon-cells/FishIconCell_<sp>.png`. `FishIconBuilder.TrimmedIcon`
reads that cell before the deck sheet, so Build Fish Icons cuts and wires them as before and the
pixel-for-pixel test still holds. Herring 14x3 → 43x10, mackerel 19x6 → 43x12. The deck sheets are
untouched. The same bake draws the crustacean and shellfish icons (`ShellIcon_<id leaf>.png`): the
lobster, rock crab and soft-shell clam from the rig's own plates, and the oyster, blue mussel and sea
scallop, which had no icon at all. The flounder (21x7) is the one small icon left; it was not in scope.

## 2. The size floor in the hands

### 2.1 What was measured

The held length is the longest side of the opaque bounds on the tail sheet, frame 0, taken over
the eight facings. The builder records it on each sized kind as `HeldLengthPx`.

| Species | small | middle | large |
|---|---|---|---|
| cod | 18 @ 2 kg | 26 @ 5.6 | 34 @ 12 |
| haddock | 13 @ 1 | 19 @ 2.8 | 24 @ 6 |
| pollock | 16 @ 1.5 | 22 @ 4.2 | 30 @ 9 |
| mackerel | 10 @ 0.3 | 13 @ 0.75 | 16 @ 1.5 |
| bass | 13 @ 1 | 26 @ 7.6 | 39 @ 25 |
| flounder | 8 @ 0.2 | 13 @ 0.78 | 17 @ 2 |
| herring | 7 @ 0.1 | 9 @ 0.25 | 11 @ 0.5 |

### 2.2 The rule (this pass)

`GameConfig.MinDrawnFishLengthPx` (default **12**, 0 turns it off). No fish is drawn from a rung
shorter than the floor while its species has a rung that reaches it. The weight used for the
picture is lifted to the lightest rung that reads; if none reaches the floor, the species' longest
rung is used. Only the picture changes: the weight, the price and the notebook record stay true.

There is one rule (`CatchSize.DrawnLengthFloor`) in three places, so the fish on the line, in his
hands and in the tote are always the same picture:

- On the line: `FishArtLibrary.LineRungFor` → `RodFightPresenter`.
- In his hands and in the tote: `CatchItemLibrary.SizedKindFor`.

What the default changes:

| Floor | Lifted |
|---|---|
| 12 | mackerel small → middle; flounder small → middle; herring small and middle → large (11 px, the longest it has) |
| 16 | also haddock small (13) → middle, bass small (13) → middle, mackerel middle (13) → large, flounder middle (13) → large. Cod and pollock are never lifted. |

### 2.3 What the floor does not fix (the honest part)

The three fish in the report are mostly a **contrast** problem, not a length problem:

- A 2.2 kg cod is the small rung, 18 px. That is over any sensible floor.
- A 0.9 kg mackerel is the middle rung, 13 px.
- A 1.6 kg flounder is already the large rung, 17 px. But it is held **edge-on**: in facings
  d2, d3, d6 and d7 it is 2-5 px wide, a dark sliver against his coat.

The floor leaves all three alone at 12. The fixes that make them read are owed:

1. **A baked contrast keyline on the small rungs' held sheets** (art-pipeline, a re-bake). It must
   not be the interact outline: that shader's outline is the "you can use this" cue
   (`outline-interaction-language.md`), so a code rim on a held fish would say the wrong thing. A
   one-pixel lighter belly line or a darker back line, baked into `Fish_<sp>_sm_tail`, is the art
   answer. **Needs a warm box** (rig edit plus the fish sheet bake).
2. **The flounder held face-on.** The flatfish should hang with its flat side to the camera (a
   held-pose roll for flatfish in `fishIsoRig2.js`, then a re-bake of the flounder tail sheets at
   all three rungs). **Needs a warm box.** After it, re-run *Build Catch Item Library* so
   `HeldLengthPx` follows the new sheets (a test fails until it does).
3. **The mesh held props (§3).** A mesh fish shares the hand's light and depth, which is most of
   why a 13 px fish vanishes against a sprite coat today.

**Landed (2026-11-18), the smaller options for items 1 and 2:**

- *Item 1, at runtime instead of baked.* `CarriableCatch` draws two darker copies of the held
  sprite one sprite pixel either side, behind the fish, in one `SortingGroup`. It is only drawn
  when `HeldLengthPx` is at or under `GameConfig.HeldFishKeylineMaxLengthPx` (16); the shade and
  offsets are `GameConfig` tunables too. No rig edit, no shader, no re-bake. A baked line from
  the rig can still replace it (set the max length to 0 to switch it off).
- *Item 2, a measured facing remap instead of a rig roll.* `FishSpeciesDef.HeldBroadSideOn`
  (the winter flounder) makes *Build Catch Item Library* measure each held facing's drawn area
  and map every facing to its broadest neighbour (`KindEntry.HeldFacingMap`). A hanging fish is
  nearly vertical, so the neighbouring facing is a roll about its length. Hands only: the
  fight's landing pose is unchanged and still owed.

## 3. Held-prop meshes per species and size (ADR 0053 PRs 1, 3 and 4)

### 3.1 Where it stands

- **PR 2 (the bake) is merged.** `Data/Characters/HeldProps/` holds seven fish meshes
  (`Fish<Sp>HeldMesh`), baked from `catch-pass-2-kit/Art/fishIsoRig2.js` at the tail rest, frame 0,
  **at scale 1: one per species, not per rung.** `CatchItemLibrary` points all three rungs of a
  species at that one mesh.
- **Nothing draws them.** `CatchItemLibrary.HeldMeshFor` has no caller: PRs 1, 3 and 4 are unbuilt.

### 3.2 The plan

| Step | What | Owner | Warm box? |
|---|---|---|---|
| 3a | **Per-rung held states.** The fish family in `HeldPropFleet` bakes each species at each rung's scale (`range` from the rig's species table, the same scales the sized sprite sheets use), minting `heldprop.fish_<sp>_sm` and `heldprop.fish_<sp>_lg` beside the existing ids (append-only: the scale-1 id stays and becomes the middle rung). `CatchItemLibraryBuilder` wires `HeldMesh` per rung instead of per species. Tests: each rung's mesh bounds scale with its rung, and `HullPropBakeUnchangedTests` stays byte-identical. | tools-editor | **Yes:** the bake runs the rig (*the game menu → Dev/3D Hulls (ADR 0022, experimental)/Bake ALL held props (rods, spade, fish, buckets)*, or `-executeMethod HiddenHarbours.App.Editor.HeldPropBakeCli.BakeAndWireCli`). About 14 new defs, small. |
| 3b | **PR 1, `feat/figure-held-mount`:** the seam that lets a facet prop take a figure as its carrier (the hand socket as the oarlock). | held props lane; lead-architect reviews | No for the code; a look needs one. |
| 3c | **PR 3, `feat/facet-thin-lines`:** the 1-2 px line path in the facet shader. For a fish it draws the two eye dots and keeps them at the right depth. | art-pipeline signs | **Yes:** a shader change is judged on a plate. |
| 3d | **PR 4, `feat/held-props-mesh`:** mount the mesh on the hand socket and hide the sprite. `CarryHands` asks `CatchItemLibrary.HeldMeshFor(SizedKindFor(id, kg))`, so the size floor in §2 applies to the mesh too and the line, the hands and the tote still agree. The cast and the fight stay sprites (D5); the shellfish stay sprites pinned at the hand (D4). | held props lane; gameplay-systems reviews | **Yes:** a PlayMode plate of him holding each species at each rung, all eight facings, against the coat. |
| 3e | **The back-lit fin.** The fish's per-face `ds` flag has no channel in the mesh, so a back-lit fin may read darker than the sprite (ADR 0053 §6). Judge it on the 3d plate; carry the flag only if it shows. | art-pipeline | Yes (the same plate). |

Order: 3a can land any time (data only, nothing reads it). Then 3b → 3c → 3d, as ADR 0053 §4 says.

## 4. Fish pictures at the stove, the locker and the freezer

### 4.1 Where it stands

None of these draws a fish today. The fixtures are logic only, and none has a renderer:

| Fixture | Code | What it holds | Draws a fish? |
|---|---|---|---|
| Stove, microwave, prep counter | `CookingStationFixture` + `CookingStationDef` (`CookingStationKind`) | a cook job (recipe, start time) | No |
| Supply locker | `Core/Save/SupplyLocker` (data) | bought supplies | No |
| Deck ice box | `Boats/DeckIceBox` | ice and a lid over the deck container | No (the container under it may) |
| Ship's hold | `Boats/ShipHold` (`IHold`) | the catch | No (it has no lid to open) |
| Ginny's freezer | `Player/GinnyFreezer` (`IHold`) | the frozen catch | No |

The fill pieces are already built and data-driven: `HoldCatchFillSource` reads any `IHold` through
Core and feeds every `ICatchFillTarget` on the object (`CatchFillRenderer` for the tote's measured
slots, `BucketFillPresenter` for a baked bucket fill). The freezer and the hold are `IHold`, so they
can show their catch through the same bridge with no new Core contract.

### 4.2 The plan

| Step | What | Owner | Warm box? |
|---|---|---|---|
| 4a | **Ginny's freezer, lid open.** A `HoldCatchFillSource` plus a fill target on the freezer prefab. The target needs slot points, so a freezer rig (or the tote rig's slot table reused at the freezer's footprint) publishes them. The fish lie in the freezer at their own sized kind, so a big cod is big there too. A frost look is an art call (a tint, or a frosted ramp in the rig). Shows only while the lid is open (the player is at it), to keep the draw calls down. | world-content (prefab), art-pipeline (rig slots, frost) | **Yes:** a rig bake for the slot points, a prefab edit and a plate. |
| 4b | **The stove and the prep counter: the fish being cooked.** A `CookJob` names its recipe; the recipe's fish input picks a sized kind, and the station draws that fish on the pan or the board (`Fish_<sp>_deck` lay at the station's facing) for the length of the job. It needs a "cooked" look later (a colour ramp swap in the rig, not a new sheet per species). The station def says where the pan sits (data, rule 6). | world-content + art-pipeline | **Yes:** a pan anchor on each station prefab and a plate. The cooked ramp is a rig change and a bake. |
| 4c | **The supply locker.** It holds supplies, not fish (bait, ice, line). A picture of bait would come from the bait items' own art, not the fish sheets. Out of scope for fish art; listed so nobody looks for it. | — | — |
| 4d | **The deck ice box and the hold.** Check first whether the deck container under the ice box already fills through a tote target on its prefab (the fill pieces support it; not checked here). The hold has no lid the player opens today, so its catch is read in lists, where the new tight icons now read. If a hatch is ever opened, it follows 4a's pattern. | world-content (check) | The check: no. A hatch: yes. |

## 5. Exact editor steps after any fish re-bake

1. *the game menu → Art/Import (after a new drop)/Build Fish Art Library*.
2. *the game menu → Art/Import (after a new drop)/Build Catch Item Library*. This re-measures
   `HeldLengthPx` from the tail sheets.
3. *the game menu → Art/Import (after a new drop)/Build Fish Icons*. This re-cuts the icons from
   the large deck cell and re-wires the icon library and the species defs.
4. Run the EditMode class `FishArtEverywhereTests`. Its tests catch a stale icon or a stale held
   length.
5. Commit the changed PNGs, metas and assets.
6. A look at St Peters: catch a small mackerel and a small flounder on the dock, carry each, and
   drop them in the tote. The line, the hands and the tote should show the same picture. Open the
   catch card and the sell screen: each fish should fill its icon slot.
