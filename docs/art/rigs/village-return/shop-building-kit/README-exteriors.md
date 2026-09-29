# Exteriors — `shopfrontRig.js`

`globalThis.Shopfront`. The commercial shell: nine businesses, each seeding massing, cladding,
storefront type, awning, signage and the street furniture that belongs to that trade. Third building
rig beside `houseIsoRig` (dwellings) and `wharfBuildingRig` (sheds and plants), on the same turntable.

Sheet: **1320 × 1180**, pivot **660, 800** (ground centre), 8 facings **N NE E SE S SW W NW**,
elev 40° default (30–50). 32 px = 1 m. `z = 0` is the pavement.

## Registration

Every number below comes from one formula, shared with `shopInteriorRig.js`:

- footprint `Wd · Ln` from the type's ranges, snapped to the 0.5 m plan cell
- `wallH` grade → eave · `fH` grade → shop floor · `ridgeZ = wallH + rise`, `rise = (Wd/2) × pitch`
- shop door on the **+Y street elevation** at a storefront-dependent `doorX`
- bake-oven / kitchen flue on the −Y gable at `x = 0`
- sash 0.82 × 1.15 m, sill at its storey floor + 1.0
- `dims().wing` is the rear ell — the same object `ShopBuilding.wingOf(type, size)` returns. When the
  plan has rooms behind the main block (fish market, restaurant, tavern) the shell grows to cover them.

## Builder surface

- **type** `generalStore · fishMarket · chandlery · bakery · restaurant · tavern · postOffice ·
  takeoutStand · giftShop` — seeds every axis below
- **shape** `gable · shed · gambrel · falseFront` · **pitch** `0..2` · **size** `0..1`
- **siding** `clapboard · shingle · boardBatten · corrugated` · **body** BODY key · **roof**
  `asphaltGrey · asphaltBrown · metalSeam · corrugated · rusted`
- **storefront** `bay · plate · smallPane · hatch · narrow` · **winDensity** `0..1` · **windows** sash style
- **awning** `none · straight · scallop` · **awnExtend** `0..1` · **awnCols** `redCream · greenCream ·
  blueCream · goldCream · tealCream · plain`
- **fascia** (painted signboard band) · **bracket** (hanging bracket sign) · **sign** `board · oval ·
  shield · pennant` · **signTone** BODY key — all signage bakes as **abstract blanks, no lettering**
- **flat** (flat above the shop: upper sash + laundry line) · **stall** (trestle + crates on the walk) ·
  **patio** · **sandwich** (A-frame chalkboard) · **planters** · **load** (loading door on +X) ·
  **scale** (platform scale) · **stacks** `0..2`
- **weather** `0..1` · **night** (lit glass + door lamp) · **elev** `30..50`

BODY: `greyShingle · white · cream · red · sage · blue · gold · plum · rustOrange · mustard · teal ·
galv · rustMetal`.

**Presets**: `harbourStore · coopFishHouse · shipChandler · quaysideBakery · wharfDiner ·
theAnchorInn · villagePost · chipStand · lighthouseGift`.

## API

- `dims(opts)` → `{ Wd, Ln, wallH, fH, shopH, eaveZ, ridgeZ, storeyZ, shape, pitch, type, label, wing }`
- `render(dir, opts)` → `Uint8ClampedArray(W*H*4)` RGBA. `dir` is the facing index 0..7. Wrap as
  `new ImageData(data, 1320, 1180)`.
- `anchors(dir, opts)` → `{ door, queue, hatch, sign, bracket, awning, stall, patio, loadDoor, lamp,
  stacks:[], ridge, wing, Wd, Ln, fH, wallH, storeyZ, type, shape }` in cell px — where to hang smoke,
  lit glass, the door lamp and sign lettering.
- `project(dir, p, elev)` → screen-space helper for custom overlays.
- Tables: `TYPES · SHAPES · SIDINGS · ROOFS · STOREFRONTS · SIGNS · AWNINGS · WINDOWS · BODY · TRIM ·
  PRESETS`.

## Limits

- Signs and fascias are blank boards. Letter them as a separate decal layer.
- Stack smoke, lit glass and the lamp are runtime overlays via `anchors`, not baked.
- The shell has no interior. Cut to `ShopBuilding` (whole plan) or `ShopInterior` (one room) on entry.
