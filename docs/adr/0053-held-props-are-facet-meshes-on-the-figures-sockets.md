# ADR 0053 — Held props are facet meshes on the figure's sockets

- **Status: PROPOSED** (2026-10-13). The owner ruled that held props should always be mesh, and
  ruled the ten decisions in §3 as recommended ("yes to ten"), both on 2026-10-08. This ADR needs
  the owner's approval in its PR under `CLAUDE.md` §7. Still open, and not this ADR's: whether that
  ruling covers the whole figure or only what it holds.
- **Date:** 2026-10-13
- **Decision owner:** owner; authored by `tools-editor` (held props PR 2, the bake). Review:
  `lead-architect` for the new Core type and this ADR; `art-pipeline` signs the shader half (PR 3);
  `gameplay-systems` reviews the Player half (PR 0, PR 4).
- **Serves:** P3 Living Working Coast: the work reads in the hands (a rod, a clam spade, a fish by
  the tail, a pail at a fill). P2 Dory to Dynasty: the rod's three tiers are three meshes.
- **Extends:** [ADR 0022](0022-3d-boat-hulls.md) phase 7 (the fittings) from the hull to the
  figure, under [ADR 0044](0044-characters-are-meshes.md) (characters are meshes) and
  [ADR 0003](0003-data-driven-content.md) (content is data).

## 1. Why

A figure that draws as a mesh (ADR 0044) still holds sprites. The rod, the spade, a fish and a
bucket are pinned to a sprite socket, so they sit off the mesh hand and share neither its light,
its depth nor its facing. The fittings solved the same problem for a hull: ADR 0022 phase 7 bakes a
rig's articulated part (an oar, an outboard) into a committed facet mesh with its own palette and
pivot, drawn by the facet path in its parent's frame. A hand is a socket, as an oarlock is.

## 2. Decision

1. **Every held prop with a 3D rig is a facet mesh**, baked from that rig by the fittings' own
   extractor and mesh builder into a **`HeldPropMeshDef`** (`Core/Iso`, D3). One asset per held
   STATE, under `Data/Characters/HeldProps/`, with an id `heldprop.<snake_case>` minted from the
   rig's family and state. Ids are append-only.
2. **The rigs enumerate the states, not C#.** Each held rig names the states it can draw, and the
   bake reads them with each state's faces, palette, pose and grip. Today that is 50 states: the
   rod's three tiers (cane, coast, deep), the clam spade, seven fish held by the tail, and three
   bucket tiers (pail, tote, tray) at thirteen fills each (empty; few, half, full or brim of fish,
   shell or crust), mirroring the sprite cells.
3. **The pose is the rig's (D1).** A state is baked at the rig's own held pose with the grip at
   the mesh origin (`GripLocalMeters`). It rides the hand's position and keeps that pose relative
   to the body.
4. **Thin parts are lines, recorded as data (D2).** The rigs plot the rod blank, the shovel shaft
   and the bail instead of building them as faces. The def records those plotted lines (`Lines`,
   `LineRamps`), so that a 1–2 px line path in the facet shader (PR 3) can draw them with depth.
   A fish's two eye dots ride the same record.
5. **Only the ramps a state paints**, in the rig's table order, at most 16
   (`HullMeshDef.HullRampSlots`). A triangle the rig stores as a quad with a repeated corner bakes
   as a triangle.
6. **Provenance on every def:** the rig's path and its LF-normalised SHA-256, the face builder,
   the material table and the pose it was baked from, and the rig's face count.
7. **The held items point at their meshes, as data.** `ToolDef.HeldMeshes` on the rod and the
   spade carries every state of the family in the rig's order; which tier a figure holds is the
   holder's to say. `CatchItemLibrary` carries the fish (per kind) and the buckets (per tier, fill
   band and catch), set by its builder. The bucket data lives in scenes, so the buckets reach their
   meshes through the library their presenter already holds, and no scene or prefab changes.
8. **The fittings do not move.** The held bake adds a branch the fittings never take.
   `HullPropBakeUnchangedTests` re-bakes all six and compares them with the committed defs, byte
   for byte.

## 3. The ten rulings (2026-10-08, as recommended)

| # | Question | Ruled |
|---|---|---|
| D1 | How a carried tool sits in the hand while she walks and runs | The prop keeps its rig's own held pose relative to the body and rides the hand's position |
| D2 | The thin parts (rod blank, shovel shaft, bucket bail) | A 1–2 px line in the facet shader that keeps depth; art-pipeline signs the shader |
| D3 | The data type | A new `HeldPropMeshDef` in `Core/Iso`, not the boats' `HullPropMeshDef` |
| D4 | Items with no 3D rig (the clam now) | Keep the sprite, pinned at the mesh hand whenever the body is a mesh |
| D5 | The cast and the fight | Hand back to sprites, body and rod, until a mesh fishing performance is its own phase |
| D6 | The dock-fight bug | Fixed first, as PR 0, with gameplay-systems reviewing |
| D7 | The fuel can | Not now; when it comes, filter its ramps |
| D8 | The knife and the gaff | Nothing to convert; rigged in 3D from the start when those features come |
| D9 | Who builds PR 1, 3 and 4 | The held props lane, in order; lead-architect reviews PR 1 and this ADR, art-pipeline signs the shader, gameplay-systems reviews PR 0 and PR 4 |
| D10 | How many ADRs | One: this one |

## 4. The order

- **PR 0**, `fix/mesh-fight-gate` (D6): any time; it waits on the owner.
- **PR 2**, `feat/held-prop-bake`, the data: **first**, ahead of the character 2b lane (ruled
  2026-10-12). It shares no file with 2b, nothing reads its meshes, and it carries this ADR because
  it brings the new Core type.
- **The character 2b lane** merges.
- **PR 1**, `feat/figure-held-mount`: the seam that lets a facet prop take a figure as its
  carrier.
- **PR 3**, `feat/facet-thin-lines`: the line path (D2).
- **PR 4**, `feat/held-props-mesh`: mounts the meshes on the figure's sockets and hides the
  sprite. It is the first of these that anyone can see.

## 5. What stays a sprite

- **The rod in the cast and the fight (D5).** The fishing performance is a sprite act today, and
  body and rod hand back to sprites for it.
- **The clam and other shellfish (D4).** There is no 3D source, and a clam is 2–3 px at 32 px/m.
  The sprite stays, pinned at the mesh hand (PR 4).
- **The fuel can (D7), the knife and the gaff (D8),** rope, the rod carried on the back, and paper:
  none is in the ruling.
- **Anything held while the figure itself is a sprite.**

## 6. Consequences

- 50 committed defs, about 0.8 MB of mesh in all (19,236 vertices, 9,688 triangles, 16-bit
  indices), and roughly twice that as committed text, since the YAML stores mesh bytes as hex. The
  buckets are most of it. Nothing loads them until PR 4.
- **The fish's back-lit fins.** The fish rig re-lights a back-lit face by a per-face flag (`ds`)
  that the fittings' mesh layout has no channel for. The fish bakes the hull rule instead (a
  back-lit face is re-lit only when its bias is −1 or less): its body is right, and a back-lit fin
  may read darker than the sprite. The proof sheet judges it; carrying the flag is a later choice.
- **Every held mesh turns counter-clockwise.** The flag is a fact about the mesh's frame and is
  not copied from the rig catalogue. The mesh is the rig's own faces in its model frame; the facet
  path turns it counter-clockwise as dir grows, while headings and every shipped sheet run
  clockwise. The catalogue's convention describes each rig's sprite cell order, which the sprite
  bakes need, and it stays as it is. A bake test measures the turn in pixels: each lopsided state
  (the coast rod, the flounder, the empty tray and the spade) must sit nearer today's sprite at
  facing d than at its mirror, 8 − d.
- **A rig edit means a re-bake.** A held rig that changes its faces, palette or pose reds the bake
  guards until its states are re-baked. A rig that renames a state keeps or retires the old id
  (content validation holds the ledger).

## 7. Progress (2026-11-19)

- **The rod in the fishing clips, PR 1 and PR 4 for the rod alone.** While the mesh body plays a
  fishing clip (`GameConfig.MeshFishingAndDig` had already lifted D5 for the body), the cane is drawn
  as its mesh on `tool_R`, aimed by the clip's own tool track (`HeldPropPose`), and the sprite rod is
  off (`GameConfig.MeshRodInHand`, `ToolDef.DrawHeldMeshOnFigure`). D5 is therefore lifted for the
  rod in those clips too: a mesh body with a sprite rod did not follow the hand.
- **D2 stands in.** Until the line pass exists, the blank is drawn as thin square tubes along the
  rig's own samples, in the rig's line colours (`IsoFacetHeldPropMesh`). PR 3 replaces them, and it
  still needs art-pipeline's signature.
- **Still a sprite:** the rod carried while walking or standing (those clips carry no tool track),
  the rod on the rack, the bend under load, the coast and deep tiers' selection, and every other
  held prop.

## 8. Progress (2026-11-20): the carried rod and the rod on his back

- **The carried rod is a mesh, at an aim from data.** On a free gait (`ToolDef.CarriedInHandClips`:
  idle, walk, run) the cane is drawn on `tool_R` at `ToolDef.CarriedPitchDeg` / `CarriedYawDeg`. D1's
  "the rig's own held pose" was the fishing aim (pitch 56, yaw 16), which pointed the blank down the
  camera's line of sight from the front and read as a stub. The grip is still the palm socket on
  the frame drawn, every frame; `GameConfig.HeldRodGripToleranceMetres` is the test's margin.
- **The rod on his back** (`ToolDef.StowOnBack`). When he has the rod but is not using it, the mesh
  rides the rig's `back` socket (the slung-rod mount on the chest, its +y the slung axis): on every
  clip that aims no tool and is not a free gait (the oars, the helm, a carry, the swim, when
  `StowWhileSwimming`), and whenever the other hand is busy or the rod is tucked away for a catch.
  A fishing state or a clip that aims the rod always puts it in his hand. The move is a short blend
  (`StowBlendSeconds`), since the rig bakes no draw clip. Decided from the hands and the clip each
  frame; nothing is saved.
- **NPC anglers** draw no rod mesh: no NPC path mounts a held prop yet.
