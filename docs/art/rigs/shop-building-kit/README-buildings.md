# Buildings — `shopBuildingRig.js`

`globalThis.ShopBuilding`. Multi-room **plans** on top of `shopInteriorRig.js`, baked as **one merged
sheet per level** — so occlusion, shading and keyline are exact across rooms instead of approximated by
stitching per-room sheets. Requires `shopInteriorRig.js` to load first; it adds no rendering of its own.

`PX 32 · CELL 0.5 m · exterior wall 0.16 · party wall 0.14 · floor deck 0.34 · elev 40° default`.
Sheet size and pivot are **per trade and per level** — ask `sheet(opts)`, don't assume.

## The plan model

A plan is a list of **room rects on a shared unit grid**, per level. Units resolve to 0.5 m cells at
bake time from the trade's own footprint, so a plan scales with `size` and can never develop a gap:
both rooms either side of a boundary map that boundary to the **same cell line**.

- `main` — the rect that **is** the shopfront mass (`Wd × Ln`), centred on the pivot. The registration contract.
- `wing` — any rect outside `main`: a rear ell (kitchen, ice house) under its own lower roof.
  `wingOf(type, size)` hands `shopfrontRig` the same numbers so the shell grows to match.

**Walls are derived, never authored.** Rooms rasterise to a cell-ownership map; every cell edge whose
two sides disagree becomes wall, contiguous edges with the same pair merge into one segment, and the
segment is classified:

- `room | outside` → **exterior** — outer face on the envelope line, 0.16 m thick, inward finish
- `roomA | roomB` → **party** — centred on the line, 0.14 m, built once, opening punched

L-plans, wings, courtyards and unequal rooms need no special cases.

**Openings.** Every party segment gets one, its kind chosen from the two room kinds — dining|kitchen →
service double, bakeFront|bakehouse → pass hatch, cutting|iceHouse → dutch door, giftFloor|stockroom →
curtain, and so on. Segments too short for their door fall back to a full-width cased opening.
Override per segment with `doors: {segKey: 'kind'}`; keys come from `walls(opts)[].key`.

| kind | clear w × h | sill |
| --- | --- | --- |
| doorway (cased) | 1.06 × 2.08 | 0 |
| slab | 0.96 × 2.05 | 0 |
| swingPair (service double) | 1.56 × 2.05 | 0 |
| hatch (pass) | 1.34 × 0.98 | 1.06 |
| arch | 2.40 × 2.32 | 0 |
| curtain | 1.06 × 2.10 | 0 |
| dutch | 1.00 × 2.05 | 0 |

## The cutaway — how party walls read at every facing

Exterior walls use the shared `keepWall` test: the two whose outward face points at the camera are
dropped (three kept on orthogonal facings, two on diagonals). A party wall has no outward side, so a
plain `keepWall` on it either hides the far room or erases the plan. Instead:

- **edge-on** to camera (`|t| ≤ 0.35`) → built solid, full height, opening punched.
- **facing** the camera (`|t| > 0.35`) → **cut back** to an architectural-cutaway read: the wall's
  footprint trace on the floor for its full length, a full-height return nib at each end, and the
  opening's casing, lintel and leaf left standing free. Nothing occludes the room behind it, and the
  plan stays legible from all eight facings.

**Ceilings are never slabbed** — a horizontal plane at ceiling height would z-sort in front of the whole
room. The deck above reads as joists plus the wall-top cap; rooms with roof over them get the same
trimmed roof underside the single-room rig uses.

## Levels and the stair

`levels.ground` / `levels.upper`, each its own rect list, each baked with its floor plane at `z = 0`.
`dims().levelZ` is the world height of that floor, so the engine stacks the two sheets on one pivot.
The upper level is constrained to the main block — wings are single-storey.

**The stair registers in both.** One authored `{room, side, along}` puts the flight in the lower level
and the matching well, trimmer and rail in the upper one, at the same cells. If the authored room has
no upper room over it, the stair migrates to one that has. `stairZone(opts)` is the keep-out — flight or
well plus the landing you step onto — and **nothing may be placed in it on either level**; the default
layout slides a blocked fixture along its wall and drops it if there is nowhere clear.

## Plans

| Trade | Ground rooms | Upper | Wing | Ground sheet (px) |
| --- | --- | --- | --- | --- |
| generalStore | salesFloor · stockroom | flat | — | 440 × 376 |
| fishMarket | counter · cutting · ice | — | ice house 2 m | 485 × 439 |
| chandlery | floor · stockroom | flat · loft | — | 428 × 369 |
| bakery | front · bakehouse | flat | — | 361 × 319 |
| restaurant | dining · bar · kitchen · stock | flat | kitchen 5 m | 723 × 595 |
| tavern | barroom · snug · kitchen | flat | kitchen 3 m | 688 × 588 |
| postOffice | lobby · sorting | flat | — | 395 × 347 |
| takeoutStand | servery | — | — | 236 × 253 |
| giftShop | floor · stockroom | flat | — | 338 × 305 |

Sheets at default `size` and elev 40°. Exact figures, pivots, room grids, wall counts and opening lists
for both levels: `shopBuilding.contract.json`.

Room **kinds** (22 of them: salesFloor, bakehouse, barroom, iceHouse, chandLoft, sorting, …) carry the
finish — floor, wall, wallpaper, wainscot, ceiling share, window density — and the fixture layout for
that use. `KINDS` and `LAY` are the tables; a room rect names a kind and inherits both.

## Builder surface

- **type** any of the nine trades (one plan each) · **level** `ground · upper` · **size** `0..1` ·
  **elev** `30..50`
- **stock** `0..1` · **weather** `0..1` · **night** · **seed** int · **beams** bool
- per-room finishes come from the room kind; building-level overrides (`floor`, `wall`, `paper`, …) apply across rooms
- **items** `[{room, k, gx, gy, rot}]` or null — manual layout, per room, in that room's own cell grid
- **rooms** `{roomId: false}` omits a room · **doors** `{segKey: 'kind'}` overrides an opening

## API

- `dims(opts)` → footprint, `levelZ`, `peakZ`, `env`, `wing`, per-room `{id, kind, label, block, ceilZ,
  cells, clear}`, wall and opening counts, stair
- `sheet(opts)` → `{W, H, cx, gy}` before any pixels · `render(dir, opts)` → `{rgba, W, H, pivot}`
- `renderLayers(dir, opts)` → the same bake split per layer `{id, kind, item, rgba}`, each keylined —
  kinds `floor · fixture · party · wall · stair · ceiling · base`, sorted back to front ·
  `ghost(rgba, keep, W, H)` for seeing the player through a wall
- `rooms(opts)` · `grid(opts, roomId)` · `items(opts)` · `walls(opts)` (every segment with its class, its
  two rooms and the opening punched in it — this is the plan diagram) · `stairOf` · `stairZone` · `env`
- `roomBox(type, room, size)` → one room's box + cell grid for `shopInteriorRig` ·
  `wingOf(type, size)` → the ell `shopfrontRig` must grow · `doorKind(kindA, kindB)`
- `anchors(dir, opts)` → `{ rooms:[{id, kind, label, centre, rect, clear}], doors:[{kind, from, to, at, w,
  seg}], fixtures:[…], occluders:[…], walkable:[…], door, backDoor, stair:{at, top, well, clear},
  lamps:[], levelZ, env, wing, pivot, W, H }` in cell px
- `project(dir, p, elev, opts)` for custom overlays

## Limits

- One plan per trade, authored in `PLANS`. A different layout is a new plan, not a parameter.
- Wings are single-storey; the upper level never leaves the main block.
- Openings are static geometry — leaves are propped, hatches are shelved, nothing animates.
- Ceilings are joists and wall caps by design; do not add a ceiling plane.
- Chalk menus, signs and shop fascias bake blank.
