# Hidden Harbours — Shop Building Kit

The commercial block: **shell, room and whole building**, three rigs that share one camera, one
palette and one set of numbers. Nine trades — general store, fish market, chandlery, bakery,
restaurant, tavern, post office, takeout stand, gift shop — each with an exterior, a set of interior
rooms, and a multi-room plan per level.

Conventions (ADR-0006 bake): **32 px = 1 m**, ¾ camera in 45° steps at **elev 40°** (the fleet's
turntable), flat-facet shading from the fixed upper-LEFT key, z-buffered, ordered dither, per-face
texture, depth-edge darkening, 1px keyline, **no AA**, binary alpha. Buildings register with the
fleet, the wharf tiles and the iso houses.

## Files

| File | What it is |
| --- | --- |
| `shopfrontRig.js` → `globalThis.Shopfront` | **Exteriors.** The commercial shell — massing, cladding, storefront, awning, signage, street furniture. |
| `shopInteriorRig.js` → `globalThis.ShopInterior` | **Rooms.** One parametric commercial room + the 32-fixture catalog. Also the shared rasterizer. |
| `shopBuildingRig.js` → `globalThis.ShopBuilding` | **Buildings.** Multi-room plans on the 0.5 m grid, one merged bake per level. Requires the interior rig. |
| `shopBuilding.contract.json` | Every number in this kit, machine-readable: per-trade shell dims, wings, plans, room grids, wall/opening lists, sheet sizes and pivots, the fixture catalog, room kinds, door specs. |
| `harness.html` | Standalone load-order + bake harness. Open it in a browser: no build, no deps, no project. |
| `ShopBuildingIso_restaurant_ground.png` | 8-dir reference sheet, 8 × (723 × 595), pivot 361,356. Four rooms, wing, stair. |
| `ShopBuildingIso_restaurant_upper.png` | The floor above the same building: 8 × (496 × 455), pivot 248,290 — stair well over the stair. |
| `ShopBuildingIso_fishMarket_ground.png` | A no-upper plan with an ice-house wing: 8 × (485 × 439), pivot 242,277. |
| `ShopfrontIso_restaurant.png` | Exterior 8-dir sheet, 8 × (1320 × 1180), pivot 660,800. |
| `ShopInteriorIso_generalStore_salesFloor.png` | Single-room 8-dir sheet, 8 × (1180 × 900), pivot 590,560. |

All sheets run **N NE E SE S SW W NW**, pivot pinned identically in every cell.

Per-rig detail: **[exteriors](README-exteriors.md)** · **[rooms](README-rooms.md)** ·
**[buildings](README-buildings.md)**.

## Load order

```html
<script src="shopfrontRig.js"></script>     <!-- optional for interiors, needed for shells -->
<script src="shopInteriorRig.js"></script>  <!-- must precede the building rig -->
<script src="shopBuildingRig.js"></script>  <!-- throws without ShopInterior -->
```

`shopBuildingRig` composes `shopInteriorRig`'s own builders (`ShopInterior._i`) rather than
re-implementing them, so a whole-plan bake is the same rasterizer, palette, texture and keyline as a
single room. Nothing is stitched from per-room sheets — occlusion and shading are exact across rooms
because the level is one merged pass.

## The registration contract

One formula produces the numbers all three rigs use, so a trade's rooms fit inside that trade's shell:

- `Shopfront.dims(type)` and `ShopInterior.dims(type)` resolve the same `Wd · Ln · wallH · fH · shopH`.
  Footprints are snapped to the 0.5 m cell grid once, in the interior rig, and read back by the other two.
- `ShopBuilding.PLANS[type].main` **is** the shopfront mass, centred on the pivot.
- `ShopBuilding.wingOf(type, size)` is the rear ell the shell must grow to cover the plan; `Shopfront`
  reads the same object (`dims().wing`). Fish market, restaurant and tavern have wings; the rest don't.
- `ShopBuilding.roomBox(type, room, size)` hands the interior rig one room's box and cell grid, so a
  fixture layout authored in the single-room editor drops into that room of the building unchanged.
- `z = 0` is the pavement for the shell. Each level bakes its own floor plane at `z = 0`;
  `dims().levelZ` says where that floor sits in world height.

## Wiring cheat-sheet

1. **Bake** per level: `ShopBuilding.render(dir, {type, level})` → `{rgba, W, H, pivot}` for each of
   the 8 facings. `sheet(opts)` gives the cell size and pivot up front, before any pixels.
2. **Place** by the pivot: draw the cell so `pivot` lands on the building's ground point. Identical in
   all 8 cells, so rotation never shifts the footprint. Stack ground and upper on the same pivot,
   offset by `levelZ × 32 px × 0.766` (the camera's height scale).
3. **Shell**: bake `Shopfront.render(dir, {type})` on its own pivot for the closed exterior. Cut to the
   interior bake when the player enters — same trade, same footprint, same ground point.
4. **Overlays** read `anchors(dir, opts)`: lamps per room, stove and oven fire, lit glass at night,
   stack smoke, sign lettering. None of it is baked — the sheet stays static.
5. **Layers**: `renderLayers(dir, opts)` returns the same bake split per floor / wall / party wall /
   ceiling / stair / fixture, each already keylined, for y-sorting and ghosting the player behind
   walls (`ghost(rgba, keep, W, H)`).

## Known limits

- Signs, fascias and menu boards bake **blank** — letter them as a decal layer.
- Smoke, lamplight, fire and lit windows are runtime overlays, not pixels in the sheet.
- Rooms are static geometry: no doors that open, no drawers, no destructible fixtures.
- One plan per trade. Room rects are authored in `PLANS`; anything else is a new entry, not an option.
- The upper level is constrained to the main block — wings are single-storey by construction.

## Demo pages (in the main project, not this kit)

`Shopfront Iso.dc.html` · `Shop Interior Iso.dc.html` · `Shop Building Iso.dc.html` — live builders
with the turntable, every axis as chips and sliders, the plan diagram, and the 8-dir sheet / single
cell / plan PNG downloads.
