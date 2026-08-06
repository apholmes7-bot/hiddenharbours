# Rooms — `shopInteriorRig.js`

`globalThis.ShopInterior`. One parametric commercial **room** plus the placeable **fixture catalog**,
and — because `shopBuildingRig.js` composes it — the shared rasterizer for everything in this kit.

Sheet: **1180 × 900**, pivot **590, 560**, 8 facings **N NE E SE S SW W NW**, elev 40° (30–50).
32 px = 1 m, floor cells 0.5 m, wall thickness 0.16 m.

**The shop is the shopfront seen from inside.** Footprint, wall height, floor height and roofline are
read from `shopfrontRig.js`'s types by the same formula, so a `bakery` room registers under a `bakery`
shell. The **+Y wall is the street elevation from the inside** — its glazing, bulkhead and shop door
are the ones the exterior rig builds.

**Open-dollhouse cutaway**: the two walls whose outward face points at the camera are dropped —
three walls stand on orthogonal facings, two on diagonals.

## Rooms per trade

| Trade | Rooms (`room:`) |
| --- | --- |
| generalStore | salesFloor · stockroom · flat |
| fishMarket | counter · cutting · ice |
| chandlery | floor · stockroom · loft · flat |
| bakery | front · bakehouse · flat |
| restaurant | dining · bar · kitchen · stock · flat |
| tavern | barroom · snug · kitchen · flat |
| postOffice | lobby · sorting · flat |
| takeoutStand | servery |
| giftShop | floor · stockroom · flat |

Room ids are the **shared vocabulary**: `shopBuildingRig` names the same rooms in its plans, so the
engine can ask either rig for `chandlery/loft` and get the same room.

**Plan adoption.** Name a room and, if `shopBuildingRig.js` is loaded, the rig renders that room at the
size it has in the building plan (`ShopBuilding.roomBox`) instead of the whole shell — same clear rect,
same cell grid, so a layout authored here drops straight into the building. `dims().planned` tells you
whether that happened; `dims().sides` says which of the four walls is exterior (glazed) and which is a
party wall. No room named means the whole shell, which is what `shopBuildingRig` itself asks for.

## Builder surface

- **type** the nine trades · **room** per-type room (reseeds the layout) · **storey** `ground · upper`
  (upper = the flat above the shop: knee walls, sloped ceiling)
- **shape** `gable · shed · gambrel · falseFront` · **pitch** `0..2` · **size** `0..1`
- **floor** `plank · wideBoard · checker · stoneFlag · painted · sawdust`
- **wall** `plaster · wainscot · board · stud · brick · block · tile · panel` · **paper** BODY key ·
  **wainscot** bool · **trimTone** BODY key or null (painted joinery)
- **windows** `sixOverSix · twoOverTwo · oneOverOne · industrial` · **winDensity** `0..1` ·
  **storefront** (matches the shell) · **dividers** `0..2`
- **stock** `0..1` (bare fixtures → packed shelves) · **counterTone** BODY key · **beams** · **stove**
- **weather** `0..1` · **night** · **seed** int
- **items** `[{k,gx,gy,rot}]` or null (null = `defaultItems` for this type + room)
- **shell** bool, default true. False renders **only the placed fixtures**, at their true positions on
  the same pivot — how the engine gets a per-fixture sprite to y-sort and ghost.

## Fixtures

**Built-ins are baked, loose props are sprites.** Every fixture is geometry in the same 3D pass, so
shading, z-order, keyline and occlusion are exact — but each one also lands on its own layer, and
`renderItem(name, dir, opts)` bakes any single fixture as a standalone sprite. `*` marks a built-in.

| Category | Fixtures |
| --- | --- |
| service | counter\* · counterShort\* · displayCase\* · bar\* · backBar\* · wicket\* |
| shelving | wallShelf\* · gondola\* · stockShelf\* · binRack\* · pigeonholes\* · ropeRack\* · larder\* |
| cold | coldTable\* · iceChest\* |
| kitchen | pass\* · ovenBank\* · prepTable\* · sinkRun\* |
| seating | table · roundTable · chair · stool · booth · bench |
| stock | crateStack · barrelStack · sackPile |
| decor | potbelly · rug · chalkMenu · plantTub |

**Placement is manual.** `grid(opts)` hands back the floor cell grid `{cell, nx, ny, ox, oy}`; items are
`[{k, gx, gy, rot}]` in cell coordinates, `rot` 0..3. `fixtureFoot(k, rot)` gives the metre footprint,
`itemCells(k, rot)` the cell footprint, `cellWorld` the conversion. `defaultItems(opts)` seeds a full,
trade-correct layout to start from.

## API

`dims · grid · defaultItems · fixtureFoot · itemCells · cellWorld · list · render · renderItem ·
renderLayers · ghost · anchors · project`, plus tables `TYPES · ROOMS · ROOM_LABEL · FLOORS · WALLS ·
WINDOWS · SHAPES · STOREYS · FIXTURES · CATS · LAYOUTS · BODY · TRIM · KEY`.

`anchors(dir, opts)` → `{ floor, door, backDoor, till, queue, browse:[], stools:[], pass, stove,
lamps:[], occluders:[], fixtures:[{k,x,y,rot,px,py}], Wd, Ln, fH, storeyZ, plate, peakZ, type, room,
ext, storey, shape }` in cell px.

`ShopInterior._i` is the internal builder surface (`F`, `box`, `slab`, `finishBands`, `placeItem`,
`paint`, `post`, …). It exists so `shopBuildingRig.js` can build whole plans through the same
rasterizer. **Not public API** — it will change shape without notice.

## Limits

- Rooms are static: lamplight, the stove fire and heat-lamp glow are runtime overlays via `anchors`.
- Chalk menus and signage bake blank.
- One room per bake. For the whole building in one merged sheet, use `shopBuildingRig.js`.
