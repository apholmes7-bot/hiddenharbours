# Hidden Harbours — Wharf Building Rig Set

The net-shed / storage-barn / fish-plant family for the working waterfront, built the SAME way as the
fleet and the iso houses: **one parametric 3D building** baked through the shared ¾ turntable, so all
**8 facings** fall out of one model. This is the wharf-buildings sibling of `houseIsoRig.js` — identical
camera, shading and face/paint code; different massing, industrial cladding and wharf fittings.

Conventions (ADR-0006 bake): **32 px = 1 m**, ¾ camera in 45° steps at **elev 40°** (the fleet's
turntable), flat-facet shading from the fixed upper-LEFT key, z-buffered, ordered dither, per-face
siding texture, 1px keyline, **no AA**. Buildings sit true on the Wharf tile kit and composite with the
boats and houses.

## Files

- **wharfBuildingRig.js** → `globalThis.WharfBuilding` — the parametric source of truth. Plain browser
  script, no deps. Re-bakes any build, any facing, on demand.
- **WharfBuildingIso_shack.png** — canonical **8-direction reference sheet** for the `shack` default:
  8 × (1200 × 1160), pivot pinned each cell, order **N NE E SE S SW W NW**. This is exactly what the
  demo's "8-DIR SHEET" button bakes — every other build/type bakes the same way from the rig.
- **_preview-wharf.png** — reference only: the seven presets on one ground line, each the same model
  re-posed.

## The builder surface (every axis resolved per render — no re-modelling)

- **type** `shack · storage · processing` — net shed / storage barn / fish plant; seeds every axis below
- **shape** `gable · gambrel · shed` — massing / roofline
- **size** `0..1` — small → large (per-type metre range)
- **siding** `shingle · clapboard · boardBatten · corrugated` — corrugated reads raw galvanised on a
  `galv` / `rustMetal` body, painted otherwise
- **base** `none · block` — cinderblock wainscot on the lower wall
- **body** `greyShingle · white · cream · red · sage · blue · rustOrange · mustard · teal · galv · rustMetal`
- **roof** `asphaltGrey · asphaltBrown · metalSeam · corrugated · rusted`
- **door** `doubleBarn · slidingBarn · plank · rollUp · personnel` (main door on the +Y gable)
- **windows** `twoOverTwo · sixOverSix · oneOverOne · industrial` · **winDensity** `0..1`
- **cupola** `none · cupola · monitor` · **loft** `none · window · door` (gable peak)
- **fittings** (bool): `dock` (raised loading dock + roll-up bays) · `hvac` · `stacks` 0..3 · `vents` ·
  `sign` (blank gable board — letter separately) · `boom` (roof hoist davit)
- **weather** `0..1` — paint fade + shingle greying + roof moss/rust · **night** — warm-lit windows
- **elev** — camera elevation (default 40°; match the fleet)

**Presets**: `netShed · redShed · tealShack · gambrelBarn · iceHouse · fishPlant · cannery`.

## Rig API (`globalThis.WharfBuilding`)

- Geometry: `W=1200 · H=1160 · PX=32` (32 px = 1 m) · `pivot={x:600,y:780}` (ground centre) ·
  `order=['N','NE','E','SE','S','SW','W','NW']` · `defaultElev=40`.
- `render(dir, opts)` → `Uint8ClampedArray(W*H*4)` RGBA, where `dir` is the facing **index 0..7** and
  `opts` is the builder surface above plus `{ elev, night }`. Wrap as `new ImageData(data, W, H)`.
- `anchors(dir, opts)` → `{ stacks:[{x,y}], door:{x,y}, ridge:{x,y}, Wd, Ln }` in cell px — the runtime
  overlay anchors (chimney smoke, lit-window glow, sign lettering) so the static bake stays static.
- `project(dir, p, elev)` → screen-space helper for custom overlays.
- Data tables: `TYPES · SHAPES · SIDINGS · ROOFS · BODY · TRIM · DOORS · WINDOWS · CUPOLAS · PRESETS`.

## Wiring cheat-sheet

1. **Bake** the build you want: for each of the 8 facings,
   `new ImageData(WharfBuilding.render(d, opts), 1200, 1160)` → a sheet cell, or bake on demand per
   placed building.
2. **Place** by the pivot: draw the cell so `(600, 780)` lands on the building's ground point. The pivot
   is identical across all 8 cells, so rotation never shifts the footprint.
3. **Overlays**: read `anchors(dir, opts)` and layer smoke off `stacks`, a warm glow on windows when
   `night`, and sign lettering at the gable — all at cell px, no per-build nudging.
4. **Scale**: 32 px = 1 m — the same metre as the fleet and the wharf tiles, so buildings, boats and
   deck tiles all register.

## Known limits

- Sign boards bake blank — letter them as a separate decal layer.
- Smoke and lit windows are **runtime overlays** (via `anchors`), not baked into the sheet.
- One model per bake; there is no interior. Doors and bays are façade decals.

## Demo page (in the main project, not this zip)

`Wharf Building Iso.dc.html` — the live parametric builder: turntable, every axis as chips/sliders, the
8-dir sheet + single-cell PNG downloads, the preset wharf row, and the rig source.
